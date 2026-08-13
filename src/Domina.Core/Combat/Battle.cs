using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Combat;

/// <summary>
/// Tek bir dövüşün deterministik simülasyonu.
/// </summary>
/// <remarks>
/// <para>
/// Adım adım ilerler (<see cref="Step"/>). Godot katmanı bunu animasyonla eş
/// zamanlı adımlar; toplu simülasyon <see cref="Run"/> ile sonuna kadar koşturur.
/// Aynı seed + aynı girdi = aynı sonuç.
/// </para>
/// <para>
/// <b>Savaşçıların kalıcı halini DEĞİŞTİRMEZ.</b> Ölüm ve sakatlık sonuçları
/// <see cref="BattleResult"/> içinde raporlanır; kalıcı hale işlemek meta katmanın
/// işidir. Aksi hâlde aynı savaşçı nesnesiyle on binlerce dövüş simüle edilemezdi.
/// </para>
/// </remarks>
public sealed class Battle
{
    private readonly List<Combatant> _combatants = [];
    private readonly List<BattleEvent> _events = [];
    private readonly BattleSetup _setup;
    private readonly CombatTuning _tuning;
    private readonly IRandomSource _rng;
    private readonly Dictionary<WarriorId, BodyPart> _lostParts = [];

    public Battle(BattleSetup setup, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(rng);

        if (setup.PlayerSide.Count == 0 || setup.EnemySide.Count == 0)
        {
            throw new ArgumentException("Her iki tarafta da en az bir savaşçı olmalı.", nameof(setup));
        }

        _setup = setup;
        _tuning = setup.Tuning;
        _rng = rng;

        foreach (Warrior w in setup.PlayerSide)
        {
            _combatants.Add(new Combatant(w, PlayerTeam));
        }

        foreach (Warrior w in setup.EnemySide)
        {
            _combatants.Add(new Combatant(w, EnemyTeam));
        }

        PlaceCombatants();

        foreach (Combatant c in _combatants)
        {
            c.BeginState(CombatState.Idle, SpacingSeconds(c));
        }

        Emit(new BattleStarted(0));
    }

    public const int PlayerTeam = 0;
    public const int EnemyTeam = 1;

    public double ElapsedSeconds { get; private set; }

    public bool IsFinished { get; private set; }

    /// <summary>Dövüş bittiğinde dolar.</summary>
    public BattleResult? Result { get; private set; }

    /// <summary>
    /// Üretilen olay akışı. <see cref="BattleSetup.CollectEvents"/> kapalıysa boştur.
    /// </summary>
    public IReadOnlyList<BattleEvent> Events => _events;

    /// <summary>
    /// Savaşçıların anlık hali. Godot katmanı bunu HUD'a basar (can/stamina barı,
    /// "çek" tuşunun hangi savaşçı için aktif olduğu).
    /// </summary>
    public IReadOnlyList<CombatantSnapshot> Snapshots() => _combatants.ConvertAll(Snapshot);

    /// <summary>Tek bir savaşçının anlık hali.</summary>
    public CombatantSnapshot SnapshotOf(WarriorId id)
    {
        Combatant c = _combatants.Find(x => x.Id == id)
                      ?? throw new ArgumentException($"Dövüşte böyle bir savaşçı yok: {id}", nameof(id));

        return Snapshot(c);
    }

    private static CombatantSnapshot Snapshot(Combatant c) => new(
        c.Id,
        c.Team,
        c.State,
        Math.Max(0, c.Health),
        c.Stamina,
        c.Warrior.EffectiveStats.MaxHealth,
        c.Warrior.EffectiveStats.MaxStamina,
        c.RetreatRequested,
        c.StateProgress,
        c.IsCancellable,
        c.Target is { IsActive: true } t ? t.Id : null,
        c.Position,
        c.Facing,
        c.SpeedThisTick);

    /// <summary>
    /// Oyuncunun "çek" tuşu — <b>tüm ekibi</b> çeker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tek bir savaşçı ayrıca çekilemez (bkz. docs/GDD.md §5). Savaşçı bazlı olsaydı
    /// doğru oynanış "yara alanı hemen çek, kalanla devam et" olurdu — kayıpsız,
    /// sürekli tekrarlanan küçük bir optimizasyon. Ekip bazlı komut kararı nadir ve
    /// ağır yapar.
    /// </para>
    /// <para>
    /// Komut her savaşçı için <b>ayrı ayrı</b> çözülür: kılıcı havada olan buffer'lanır,
    /// boşta olan hemen kaçmaya başlar. Yani tek tuş, üç farklı anda devreye girebilir.
    /// </para>
    /// </remarks>
    /// <returns>En az bir savaşçı komutu kabul ettiyse true.</returns>
    public bool CommandRetreat()
    {
        bool accepted = false;

        foreach (Combatant c in _combatants)
        {
            if (c.Team == PlayerTeam)
            {
                accepted |= CommandRetreat(c);
            }
        }

        return accepted;
    }

    private bool CommandRetreat(Combatant c)
    {
        if (!c.IsActive || c.State == CombatState.Retreating || c.RetreatRequested)
        {
            return false;
        }

        c.RetreatRequested = true;
        Emit(new RetreatCommanded(ElapsedSeconds, c.Id));

        if (c.IsCancellable)
        {
            BeginRetreat(c);
        }
        else
        {
            // Kılıç havada — mevcut vuruş tamamlanmadan kaçamaz.
            Emit(new RetreatBuffered(ElapsedSeconds, c.Id));
        }

        return true;
    }

    /// <summary>Bir adım ilerletir.</summary>
    /// <returns>Dövüş devam ediyorsa true, bittiyse false.</returns>
    public bool Step()
    {
        if (IsFinished)
        {
            return false;
        }

        ElapsedSeconds += _tuning.TickSeconds;

        Move();

        foreach (Combatant c in _combatants)
        {
            if (!c.IsActive)
            {
                continue;
            }

            RegenerateStamina(c);
            ConsultRetreatPolicy(c);
            AdvanceState(c);

            if (Finish())
            {
                return false;
            }
        }

        if (ElapsedSeconds >= _tuning.MaxBattleSeconds)
        {
            Complete(BattleOutcome.TimeLimit);
            return false;
        }

        return true;
    }

    /// <summary>Dövüşü sonuna kadar koşturur.</summary>
    public BattleResult Run()
    {
        while (Step())
        {
            // Step() bitiş koşulunu kendi kontrol eder.
        }

        return Result!;
    }

    // --------------------------------------------------------------- hareket

    /// <summary>
    /// Başlangıç düzeni: iki taraf karşılıklı, kendi içlerinde derinliğe yayılmış.
    /// </summary>
    private void PlaceCombatants()
    {
        double centerX = _tuning.ArenaWidth / 2;
        double centerY = _tuning.ArenaDepth / 2;

        int player = 0;
        int enemy = 0;

        foreach (Combatant c in _combatants)
        {
            bool isPlayer = c.Team == PlayerTeam;
            int index = isPlayer ? player++ : enemy++;
            int count = isPlayer ? _setup.PlayerSide.Count : _setup.EnemySide.Count;

            // Kadroyu derinlikte ortalar: 3 kişi -1, 0, +1 aralığına dağılır.
            double lane = index - ((count - 1) / 2.0);

            c.Position = new ArenaPoint(
                centerX + (isPlayer ? -_tuning.StartOffsetX : _tuning.StartOffsetX),
                centerY + (lane * _tuning.StartSpacingY));

            c.Facing = isPlayer ? 1 : -1;
        }
    }

    /// <summary>
    /// Herkesi bir tick ilerletir: hedefe yaklaşma, kaçış, ve üst üste binmeyi
    /// engelleyen itme.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Saldırıya kilitli savaşçı yürümez.</b> Vuruş taahhüdünün bedeli budur:
    /// hamleyi başlattıysan hedef kaçsa bile yerinde çakılırsın.
    /// </para>
    /// <para>
    /// Fizik motoru yok, kendi kinematiğimiz var — Godot'un çarpışma çözümü sürümden
    /// sürüme değişir ve "aynı seed = aynı dövüş" garantisini bozardı.
    /// </para>
    /// </remarks>
    private void Move()
    {
        double step = _tuning.MoveSpeed * _tuning.TickSeconds;

        foreach (Combatant c in _combatants)
        {
            ArenaPoint before = c.Position;

            if (!c.IsActive)
            {
                c.SpeedThisTick = 0;
                continue;
            }

            if (c.State is CombatState.Retreating)
            {
                // Hedef, çıkış eşiğinin de ötesi: savaşçı eşiği geçerken durmasın.
                double exitX = c.Team == PlayerTeam
                    ? -(_tuning.ExitMargin * 2)
                    : _tuning.ArenaWidth + (_tuning.ExitMargin * 2);

                c.Position = c.Position.MovedToward(new ArenaPoint(exitX, c.Position.Y), step);
            }
            else if (c.State is CombatState.Idle && FindTarget(c) is Combatant target)
            {
                FaceToward(c, target);

                double reach = c.Warrior.UsableWeapon.Reach * _tuning.PreferredReachFraction;
                double gap = c.Position.DistanceTo(target.Position);

                if (gap > reach)
                {
                    c.Position = c.Position.MovedToward(target.Position, Math.Min(step, gap - reach));
                }
            }

            Separate(c);
            Clamp(c);
            c.SpeedThisTick = before.DistanceTo(c.Position) / _tuning.TickSeconds;
        }
    }

    /// <summary>Üst üste binen savaşçıları iter. Kaçan itilmez — yolu kesilemesin.</summary>
    private void Separate(Combatant c)
    {
        if (c.State is CombatState.Retreating)
        {
            return;
        }

        foreach (Combatant other in _combatants)
        {
            if (ReferenceEquals(other, c) || !other.IsActive)
            {
                continue;
            }

            double gap = c.Position.DistanceTo(other.Position);
            if (gap >= _tuning.PersonalSpace)
            {
                continue;
            }

            // Tam üst üste düşen iki savaşçıyı ayırmak için sabit bir yön gerekir;
            // aksi hâlde yön vektörü sıfır olur ve ikisi de kilitlenir.
            c.Position = gap <= double.Epsilon
                ? new ArenaPoint(c.Position.X - (_tuning.PersonalSpace / 2), c.Position.Y)
                : c.Position.MovedAwayFrom(other.Position, _tuning.PersonalSpace - gap);
        }
    }

    /// <summary>Derinlik arenanın dışına taşamaz; hat boyunca yalnızca kaçan çıkar.</summary>
    private void Clamp(Combatant c)
    {
        if (c.State is CombatState.Retreating)
        {
            return;
        }

        double y = Math.Clamp(c.Position.Y, 0, _tuning.ArenaDepth);
        double x = Math.Clamp(c.Position.X, 0, _tuning.ArenaWidth);
        c.Position = new ArenaPoint(x, y);
    }

    private static void FaceToward(Combatant c, Combatant target)
    {
        double dx = target.Position.X - c.Position.X;
        if (Math.Abs(dx) > double.Epsilon)
        {
            c.Facing = dx >= 0 ? 1 : -1;
        }
    }

    /// <summary>Saldıran, savunanın arkasında mı?</summary>
    /// <remarks>
    /// Kuşatmanın mekanik karşılığı: çevrildiğinde birileri mutlaka arkanda kalır ve
    /// onun vuruşu hem daha isabetli hem daha ağır olur.
    /// </remarks>
    private static bool IsFlanking(Combatant attacker, Combatant defender)
    {
        double dx = attacker.Position.X - defender.Position.X;
        return Math.Abs(dx) > double.Epsilon && Math.Sign(dx) != defender.Facing;
    }

    /// <summary>Savaşçı hedefine vurabilecek kadar yakın mı?</summary>
    private static bool InReach(Combatant attacker, Combatant target) =>
        attacker.Position.DistanceTo(target.Position) <= attacker.Warrior.UsableWeapon.Reach;

    /// <summary>
    /// Kaçan savaşçı arenayı gerçekten terk etti mi? Kadrajın kenarı yetmez — ekranda
    /// gözden kaybolması için biraz daha gitmesi gerekir.
    /// </summary>
    private bool HasLeftArena(Combatant c) =>
        c.Team == PlayerTeam
            ? c.Position.X <= -_tuning.ExitMargin
            : c.Position.X >= _tuning.ArenaWidth + _tuning.ExitMargin;

    // ---------------------------------------------------------------- durum

    private void AdvanceState(Combatant c)
    {
        c.StateTimer -= _tuning.TickSeconds;
        if (c.StateTimer > 0)
        {
            return;
        }

        switch (c.State)
        {
            case CombatState.Idle:
                if (c.RetreatRequested)
                {
                    BeginRetreat(c);
                    return;
                }

                StartAttack(c);
                break;

            case CombatState.AttackWindup:
                ResolveWindupEnd(c);
                break;

            case CombatState.AttackRecovery:
                // Toparlanma bitti — buffer'lanmış kaçış komutu şimdi işlenir.
                if (c.RetreatRequested)
                {
                    BeginRetreat(c);
                    return;
                }

                c.BeginState(CombatState.Idle, SpacingSeconds(c));
                break;

            case CombatState.Retreating:
                // Kaçış artık sayaçla değil mesafeyle biter: gerçekten arenayı
                // terk etmesi gerekiyor.
                if (HasLeftArena(c))
                {
                    c.BeginState(CombatState.Escaped, 0);
                    Emit(new WarriorEscaped(ElapsedSeconds, c.Id));
                }
                else
                {
                    c.BeginState(CombatState.Retreating, _tuning.TickSeconds);
                }

                break;

            case CombatState.Escaped:
            case CombatState.Dead:
            default:
                break;
        }
    }

    private void StartAttack(Combatant attacker)
    {
        Combatant? target = FindTarget(attacker);

        // Menzil dışındaysa saldırı başlamaz — savaşçı yaklaşmaya devam eder.
        if (target is null || !InReach(attacker, target))
        {
            Wait(attacker);
            return;
        }

        attacker.BeginState(
            CombatState.AttackWindup,
            attacker.Warrior.UsableWeapon.AttackSeconds * _tuning.WindupFraction);

        Emit(new AttackStarted(ElapsedSeconds, attacker.Id, target.Id));
    }

    private void ResolveWindupEnd(Combatant attacker)
    {
        Combatant? target = FindTarget(attacker);
        if (target is not null)
        {
            attacker.Stamina = Math.Max(0, attacker.Stamina - _tuning.AttackStaminaCost);
            attacker.AttacksMade++;

            // Hedef hamle sırasında menzilden çıktıysa kılıç boşluğa iner. Vuruşa
            // kilitlenmenin bedeli: hedef kaçarken sen yerinde çakılısın.
            if (InReach(attacker, target))
            {
                ResolveStrike(attacker, target);
            }
            else
            {
                Emit(new AttackMissed(ElapsedSeconds, attacker.Id, target.Id));
            }
        }

        if (attacker.State != CombatState.Dead)
        {
            attacker.BeginState(
                CombatState.AttackRecovery,
                attacker.Warrior.UsableWeapon.AttackSeconds * (1 - _tuning.WindupFraction));
        }
    }

    /// <summary>
    /// Kaçış başlar; <b>menzilinde bulunan her düşman</b> bedava bir vuruş kazanır.
    /// </summary>
    /// <remarks>
    /// "Tuşa bastım = güvendeyim" olmasın diye vardı; uzam gelince asıl anlamını
    /// kazandı: <b>çevrildiysen kaçmanın bedeli üç bedava vuruştur.</b> Kuşatılmadan
    /// önce çekilmek artık gerçek bir karar.
    /// </remarks>
    private void BeginRetreat(Combatant c)
    {
        c.BeginState(CombatState.Retreating, _tuning.TickSeconds);
        Emit(new RetreatStarted(ElapsedSeconds, c.Id));

        foreach (Combatant hunter in _combatants)
        {
            if (hunter.Team == c.Team || !hunter.IsActive || !InReach(hunter, c))
            {
                continue;
            }

            Emit(new OpportunityAttack(ElapsedSeconds, hunter.Id, c.Id));
            hunter.AttacksMade++;
            ResolveStrike(hunter, c);

            if (!c.IsActive)
            {
                return;
            }
        }
    }

    /// <summary>Vuracak kimse yok — kısa bir süre bekleyip yeniden bakar.</summary>
    private static void Wait(Combatant c) => c.BeginState(CombatState.Idle, 0.2);

    // -------------------------------------------------------------- çözümleme

    private void ResolveStrike(Combatant attacker, Combatant defender)
    {
        if (!defender.IsActive)
        {
            return;
        }

        WarriorStats atkStats = attacker.Warrior.EffectiveStats;
        WarriorStats defStats = defender.Warrior.EffectiveStats;
        double staminaFactor = StaminaFactor(attacker);

        // 1) İsabet
        bool flanking = IsFlanking(attacker, defender);
        double hitChance = (_tuning.BaseHitChance + (atkStats.Accuracy * _tuning.AccuracyHitBonus))
                           * staminaFactor;

        if (!defender.CanDefend)
        {
            hitChance += _tuning.RetreatingHitBonus;
        }

        if (flanking)
        {
            hitChance += _tuning.FlankHitBonus;
        }

        if (!_rng.Chance(Math.Clamp(hitChance, 0.05, 0.98)))
        {
            Emit(new AttackMissed(ElapsedSeconds, attacker.Id, defender.Id));
            return;
        }

        // 2) Kaçınma — çekilirken kaçınılamaz, arkadan gelen vuruş da kaçınılamaz
        if (!flanking && defender.CanDefend && defender.Stamina >= _tuning.DodgeStaminaCost)
        {
            double evasionChance = defStats.Evasion / 100.0 * _tuning.MaxEvasionChance;
            if (_rng.Chance(evasionChance))
            {
                defender.Stamina -= _tuning.DodgeStaminaCost;
                defender.DodgesPerformed++;
                Emit(new AttackDodged(ElapsedSeconds, attacker.Id, defender.Id));
                return;
            }
        }

        // 3) Hasar — darbe önce bir bölgeye iner (zırh ve kopma oradan okunur)
        HitLocation location = RollHitLocation();
        Weapon weapon = attacker.Warrior.UsableWeapon;
        double raw = weapon.Damage
                     * (1 + (atkStats.Strength / 100.0 * _tuning.StrengthDamageBonusAtMax))
                     * staminaFactor
                     * (flanking ? _tuning.FlankDamageMultiplier : 1.0);

        double afterDefense = raw * (1 - (defStats.Defense / 100.0 * _tuning.MaxDefenseReduction));
        double damage = Math.Max(
            _tuning.MinimumDamage,
            afterDefense - defender.Warrior.Armor.DamageReduction);

        defender.Health -= damage;
        defender.TimesHit++;
        defender.DamageTaken += damage;
        attacker.HitsLanded++;
        attacker.DamageDealt += damage;

        Emit(new AttackLanded(
            ElapsedSeconds, attacker.Id, defender.Id, damage, Math.Max(0, defender.Health)));

        // 4) Ağır darbe → uzuv kopma zarı (düşük can ÖN KOŞUL DEĞİL)
        if (TryGrievousBlow(weapon, defender, damage, defStats, location))
        {
            return;
        }

        if (defender.Health <= 0)
        {
            Die(defender, DeathCause.Wounds);
        }
    }

    /// <summary>
    /// Ağır darbe sonucunu çözer.
    /// </summary>
    /// <returns>Ağır darbe tetiklendiyse (ölüm veya uzuv kaybı) true.</returns>
    private bool TryGrievousBlow(
        Weapon weapon,
        Combatant defender,
        double damage,
        WarriorStats defStats,
        HitLocation location)
    {
        double severity = damage / defStats.MaxHealth;
        if (severity < _tuning.GrievousSeverityThreshold)
        {
            return false;
        }

        double chance = _tuning.BaseDismembermentChance
                        * weapon.DismembermentFactor
                        * (1 - defender.Warrior.Armor.DismembermentResistance);

        if (!_rng.Chance(chance))
        {
            return false;
        }

        // Sonuç ağacı (docs/GDD.md §7): oyuncu müdahale ettiyse uzvunu kaybederek
        // yaşar, etmediyse ölür. Tuşa basmak hayat kurtarır ama bedelsiz değildir.
        if (defender.PlayerIntervened)
        {
            if (SeverablePart(defender, location) is BodyPart part)
            {
                defender.LostLimb = true;
                _lostParts[defender.Id] = part;
                Emit(new WarriorDismembered(ElapsedSeconds, defender.Id, part));
            }

            // Uzvunu kaybetse de canı bitmişse yine ölür.
            if (defender.Health <= 0)
            {
                Die(defender, DeathCause.Wounds);
            }

            return true;
        }

        Die(defender, DeathCause.GrievousBlow);
        return true;
    }

    /// <summary>
    /// Ölümden dönen savaşçının hangi uzvunu kaybettiği. Kaybedecek uzvu kalmadıysa
    /// <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Darbe gövdeye inmişse bile <b>bir uzuv gider</b>: bölge yalnızca hasarı ve zırhı
    /// ilgilendirir, sonuç ağacını değil. Aksi hâlde gövdeye inen darbelerde "çek" tuşu
    /// <b>bedava</b> olurdu — ölümden dönersin, hiçbir şey kaybetmezsin. GDD §7'nin
    /// vaadi tam tersi: "tuşa basmak hayat kurtarır ama bedelsiz değildir".
    /// </para>
    /// <para>
    /// Ölçüldü: gövde vuruşları koparmasın denince oyuncu zaferi %36'dan %53'e çıktı,
    /// yani müdahale neredeyse risksiz hâle geldi.
    /// </para>
    /// <para>
    /// Kalan uzuvlar arasında seçim, bölge ağırlıklarının kendisiyle yapılır — gövde
    /// hariç. Aynı uzuv iki kez kopmaz.
    /// </para>
    /// </remarks>
    private BodyPart? SeverablePart(Combatant defender, HitLocation location)
    {
        BodyPart? direct = location switch
        {
            HitLocation.Arm => BodyPart.Arm,
            HitLocation.Leg => BodyPart.Leg,
            HitLocation.Head => BodyPart.Eye,
            _ => null,
        };

        if (direct is BodyPart hit && !AlreadyLost(defender, hit))
        {
            return hit;
        }

        // Gövdeye indi ya da o uzuv zaten yok: kalanlardan ağırlıklı seçim.
        double arm = AlreadyLost(defender, BodyPart.Arm) ? 0 : _tuning.ArmHitWeight;
        double leg = AlreadyLost(defender, BodyPart.Leg) ? 0 : _tuning.LegHitWeight;
        double eye = AlreadyLost(defender, BodyPart.Eye) ? 0 : _tuning.HeadHitWeight;
        double total = arm + leg + eye;

        if (total <= 0)
        {
            return null;
        }

        double roll = _rng.NextDouble() * total;
        if (roll < arm)
        {
            return BodyPart.Arm;
        }

        return roll < arm + leg ? BodyPart.Leg : BodyPart.Eye;
    }

    private bool AlreadyLost(Combatant defender, BodyPart part) =>
        defender.Warrior.HasDisability(part)
        || (_lostParts.TryGetValue(defender.Id, out BodyPart lost) && lost == part);

    /// <summary>Darbenin nereye indiği — ağırlıklı zar, tek RNG çağrısı.</summary>
    private HitLocation RollHitLocation()
    {
        double torso = _tuning.TorsoHitWeight;
        double leg = torso + _tuning.LegHitWeight;
        double arm = leg + _tuning.ArmHitWeight;
        double total = arm + _tuning.HeadHitWeight;

        double roll = _rng.NextDouble() * total;
        if (roll < torso)
        {
            return HitLocation.Torso;
        }

        if (roll < leg)
        {
            return HitLocation.Leg;
        }

        return roll < arm ? HitLocation.Arm : HitLocation.Head;
    }

    private void Die(Combatant c, DeathCause cause)
    {
        c.BeginState(CombatState.Dead, 0);
        c.Health = 0;
        Emit(new WarriorDied(ElapsedSeconds, c.Id, cause));
    }

    // ------------------------------------------------------------- yardımcılar

    private void RegenerateStamina(Combatant c)
    {
        double max = c.Warrior.EffectiveStats.MaxStamina;
        c.Stamina = Math.Min(max, c.Stamina + (_tuning.StaminaRegenPerSecond * _tuning.TickSeconds));
    }

    private void ConsultRetreatPolicy(Combatant c)
    {
        if (_setup.RetreatPolicy is null || c.RetreatRequested || c.Team != PlayerTeam)
        {
            return;
        }

        WarriorStats stats = c.Warrior.EffectiveStats;
        var context = new RetreatContext(
            c.Id,
            c.Health / stats.MaxHealth,
            c.Stamina / stats.MaxStamina,
            ElapsedSeconds,
            CountActive(PlayerTeam),
            CountActive(EnemyTeam));

        if (_setup.RetreatPolicy.ShouldRetreat(in context))
        {
            // Politika tek bir savaşçıya bakar ama komut ekibin tamamını kapsar —
            // oyuncunun tuşuyla aynı kural (bkz. docs/GDD.md §5).
            CommandRetreat();
        }
    }

    private double StaminaFactor(Combatant c)
    {
        double fraction = c.Stamina / c.Warrior.EffectiveStats.MaxStamina;
        return fraction < _tuning.LowStaminaThreshold ? _tuning.LowStaminaPenalty : 1.0;
    }

    private double SpacingSeconds(Combatant c)
    {
        double t = Math.Clamp(c.Warrior.EffectiveStats.Aggression / 100.0, 0, 1);
        return _tuning.SpacingSecondsAtZeroAggression
               + ((_tuning.SpacingSecondsAtMaxAggression - _tuning.SpacingSecondsAtZeroAggression) * t);
    }

    /// <remarks>
    /// LINQ yerine düz döngü: bu iki yardımcı her tick'te her savaşçı için çağrılıyor
    /// ve lambda'ların yakaladığı değişkenler dövüş başına yüz binlerce bayt ayırıyordu.
    /// Toplu simülasyon on binlerce dövüş koşturuyor; sıcak döngü ayırma yapmamalı
    /// (bkz. <c>ThroughputTests</c>).
    /// </remarks>
    /// <summary>
    /// Saldıranın hedefi: elindeki hedef hâlâ ayaktaysa o, değilse <b>en yakın</b> düşman.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hedef <b>yapışkandır</b>: bir kez seçilince düşman ölene ya da kaçana kadar
    /// korunur. Her tick'te en yakına dönseydi savaşçılar iki düşman arasında salınır,
    /// hiç vuramazdı.
    /// </para>
    /// <para>
    /// Uzam gelmeden önce hedef rastgele seçiliyordu — mesafe diye bir şey olmadığı için
    /// başka anlamlı kural yoktu. Artık kural Domina'daki gibi uzamdan çıkıyor: kılıcın
    /// erişebileceği en yakın düşman.
    /// </para>
    /// </remarks>
    private Combatant? FindTarget(Combatant attacker)
    {
        if (attacker.Target is { IsActive: true } current)
        {
            return current;
        }

        Combatant? nearest = null;
        double best = double.MaxValue;

        foreach (Combatant c in _combatants)
        {
            if (c.Team == attacker.Team || !c.IsActive)
            {
                continue;
            }

            double distance = attacker.Position.SquaredDistanceTo(c.Position);
            if (distance < best)
            {
                best = distance;
                nearest = c;
            }
        }

        attacker.Target = nearest;
        return nearest;
    }

    /// <summary>Karşı taraftaki ayakta savaşçılardan rastgele biri; yoksa <c>null</c>.</summary>
    /// <remarks>
    /// İki geçiş yapar ve dizi ayırmaz — sıcak döngüde çağrıldığı için
    /// (bkz. <c>ThroughputTests.PerBattleAllocationStaysSmallWithoutEvents</c>).
    /// </remarks>
    private Combatant? RandomEnemy(int team)
    {
        int count = 0;
        foreach (Combatant c in _combatants)
        {
            if (c.Team != team && c.IsActive)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        int wanted = _rng.NextInt(count);
        foreach (Combatant c in _combatants)
        {
            if (c.Team != team && c.IsActive && wanted-- == 0)
            {
                return c;
            }
        }

        return null;
    }

    /// <inheritdoc cref="FindTarget"/>
    private int CountActive(int team)
    {
        int count = 0;
        foreach (Combatant c in _combatants)
        {
            if (c.Team == team && c.IsActive)
            {
                count++;
            }
        }

        return count;
    }

    private void Emit(BattleEvent e)
    {
        if (_setup.CollectEvents)
        {
            _events.Add(e);
        }
    }

    private bool Finish()
    {
        if (CountActive(PlayerTeam) == 0)
        {
            // Çekilmekle kırılmak aynı şey değil: biri seferi harcar, diğeri roster'ı.
            bool anyoneEscaped = _combatants.Exists(
                c => c.Team == PlayerTeam && c.State == CombatState.Escaped);

            Complete(anyoneEscaped ? BattleOutcome.PlayerWithdrawal : BattleOutcome.PlayerWipe);
            return true;
        }

        if (CountActive(EnemyTeam) == 0)
        {
            Complete(BattleOutcome.PlayerVictory);
            return true;
        }

        return false;
    }

    private void Complete(BattleOutcome outcome)
    {
        IsFinished = true;
        Emit(new BattleEnded(ElapsedSeconds, outcome));

        var summaries = _combatants.ConvertAll(c => new WarriorBattleSummary(
            c.Id,
            c.Warrior.Name,
            c.Team,
            c.State,
            Math.Max(0, c.Health),
            c.AttacksMade,
            c.HitsLanded,
            c.TimesHit,
            c.DodgesPerformed,
            c.DamageDealt,
            c.DamageTaken,
            c.LostLimb)
        {
            LostPart = _lostParts.TryGetValue(c.Id, out BodyPart p) ? p : null,
        });

        Result = new BattleResult(outcome, ElapsedSeconds, summaries);
    }
}
