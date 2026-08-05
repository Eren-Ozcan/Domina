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

        foreach (Combatant c in _combatants)
        {
            c.StateTimer = SpacingSeconds(c);
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
    public IReadOnlyList<CombatantSnapshot> Snapshots() =>
        _combatants.ConvertAll(c => new CombatantSnapshot(
            c.Id,
            c.Team,
            c.State,
            Math.Max(0, c.Health),
            c.Stamina,
            c.Warrior.EffectiveStats.MaxHealth,
            c.Warrior.EffectiveStats.MaxStamina,
            c.RetreatRequested));

    /// <summary>Tek bir savaşçının anlık hali.</summary>
    public CombatantSnapshot SnapshotOf(WarriorId id)
    {
        Combatant c = _combatants.Find(x => x.Id == id)
                      ?? throw new ArgumentException($"Dövüşte böyle bir savaşçı yok: {id}", nameof(id));

        return new CombatantSnapshot(
            c.Id,
            c.Team,
            c.State,
            Math.Max(0, c.Health),
            c.Stamina,
            c.Warrior.EffectiveStats.MaxHealth,
            c.Warrior.EffectiveStats.MaxStamina,
            c.RetreatRequested);
    }

    /// <summary>
    /// Oyuncunun "çek" tuşu. Savaşçı saldırıya kilitliyse komut buffer'lanır ve
    /// mevcut hareket bitince kaçış başlar (bkz. docs/GDD.md §5).
    /// </summary>
    /// <returns>Komut kabul edildiyse true; savaşçı zaten çekiliyor/ölmüşse false.</returns>
    public bool CommandRetreat(WarriorId id)
    {
        Combatant? c = _combatants.Find(x => x.Id == id);
        if (c is null || !c.IsActive || c.State == CombatState.Retreating || c.RetreatRequested)
        {
            return false;
        }

        c.RetreatRequested = true;
        Emit(new RetreatCommanded(ElapsedSeconds, id));

        if (c.IsCancellable)
        {
            BeginRetreat(c);
        }
        else
        {
            // Kılıç havada — mevcut vuruş tamamlanmadan kaçamaz.
            Emit(new RetreatBuffered(ElapsedSeconds, id));
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

                c.State = CombatState.Idle;
                c.StateTimer = SpacingSeconds(c);
                break;

            case CombatState.Retreating:
                c.State = CombatState.Escaped;
                Emit(new WarriorEscaped(ElapsedSeconds, c.Id));
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
        if (target is null)
        {
            c_Wait(attacker);
            return;
        }

        attacker.State = CombatState.AttackWindup;
        attacker.StateTimer = attacker.Warrior.UsableWeapon.AttackSeconds * _tuning.WindupFraction;
        Emit(new AttackStarted(ElapsedSeconds, attacker.Id, target.Id));
    }

    private void ResolveWindupEnd(Combatant attacker)
    {
        Combatant? target = FindTarget(attacker);
        if (target is not null)
        {
            attacker.Stamina = Math.Max(0, attacker.Stamina - _tuning.AttackStaminaCost);
            attacker.AttacksMade++;
            ResolveStrike(attacker, target);
        }

        if (attacker.State != CombatState.Dead)
        {
            attacker.State = CombatState.AttackRecovery;
            attacker.StateTimer =
                attacker.Warrior.UsableWeapon.AttackSeconds * (1 - _tuning.WindupFraction);
        }
    }

    private void BeginRetreat(Combatant c)
    {
        c.State = CombatState.Retreating;
        c.StateTimer = _tuning.RetreatSeconds;
        Emit(new RetreatStarted(ElapsedSeconds, c.Id));

        // Kaçan avın arkasından bedava vuruş: "tuşa bastım = güvendeyim" olmasın.
        Combatant? hunter = FindTarget(c);
        if (hunter is not null && hunter.IsActive)
        {
            Emit(new OpportunityAttack(ElapsedSeconds, hunter.Id, c.Id));
            hunter.AttacksMade++;
            ResolveStrike(hunter, c);
        }
    }

    private static void c_Wait(Combatant c)
    {
        c.State = CombatState.Idle;
        c.StateTimer = 0.2;
    }

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
        double hitChance = (_tuning.BaseHitChance + (atkStats.Accuracy * _tuning.AccuracyHitBonus))
                           * staminaFactor;

        if (!defender.CanDefend)
        {
            hitChance += _tuning.RetreatingHitBonus;
        }

        if (!_rng.Chance(Math.Clamp(hitChance, 0.05, 0.98)))
        {
            Emit(new AttackMissed(ElapsedSeconds, attacker.Id, defender.Id));
            return;
        }

        // 2) Kaçınma — çekilirken kaçınılamaz, ayrıca stamina ister
        if (defender.CanDefend && defender.Stamina >= _tuning.DodgeStaminaCost)
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

        // 3) Hasar
        Weapon weapon = attacker.Warrior.UsableWeapon;
        double raw = weapon.Damage
                     * (1 + (atkStats.Strength / 100.0 * _tuning.StrengthDamageBonusAtMax))
                     * staminaFactor;

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
        if (TryGrievousBlow(weapon, defender, damage, defStats))
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
    private bool TryGrievousBlow(Weapon weapon, Combatant defender, double damage, WarriorStats defStats)
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
            BodyPart part = ChooseBodyPart(defender);
            defender.LostLimb = true;
            _lostParts[defender.Id] = part;
            Emit(new WarriorDismembered(ElapsedSeconds, defender.Id, part));

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

    private BodyPart ChooseBodyPart(Combatant defender)
    {
        BodyPart[] available =
            Enum.GetValues<BodyPart>()
                .Where(p => !defender.Warrior.HasDisability(p)
                            && !(_lostParts.TryGetValue(defender.Id, out BodyPart lost) && lost == p))
                .ToArray();

        return available.Length == 0
            ? BodyPart.Eye
            : available[_rng.NextInt(available.Length)];
    }

    private void Die(Combatant c, DeathCause cause)
    {
        c.State = CombatState.Dead;
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
            CommandRetreat(c.Id);
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

    private Combatant? FindTarget(Combatant attacker) =>
        _combatants.Find(c => c.Team != attacker.Team && c.IsActive);

    private int CountActive(int team) => _combatants.Count(c => c.Team == team && c.IsActive);

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
            Complete(BattleOutcome.PlayerDefeat);
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
