using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;
using Godot;

namespace Domina.Game;

/// <summary>
/// Faz 1'in olay akışını izlenebilir bir dövüşe çeviren sahne.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ok tek yönlü:</b> bu sınıf çekirdeği tüketir, çekirdek bu sınıftan habersizdir.
/// Görselleştirme dövüşün sonucunu değiştiremez — tek istisna oyuncunun "çek" komutu,
/// o da <see cref="Battle.CommandRetreat"/> üzerinden geçer (bkz. CLAUDE.md → Mimari kuralı).
/// </para>
/// <para>
/// Dövüş <b>gerçek zamanla adımlanır</b>, önceden koşturulup kaydı oynatılmaz: oyuncu
/// dövüş sürerken müdahale edebildiği için karar canlı simülasyona işlemek zorunda.
/// </para>
/// <para>
/// Görsel iki kanaldan sürülür: <b>sürekli</b> hal anlık görüntülerden (duruş, can,
/// stamina), <b>anlık</b> tepkiler olay akışından (vuruş sarsıntısı, uzuv kopması,
/// ölüm). Bu ayrım korunmalı — olaylar tek seferliktir, tekrar oynatılamaz.
/// </para>
/// </remarks>
public sealed partial class BattleArena : Node2D
{
    private static readonly Color PlayerTint = new(0.42f, 0.62f, 0.86f);
    private static readonly Color EnemyTint = new(0.80f, 0.38f, 0.34f);

    private const float GroundY = 720f;
    private const float LaneGap = 140f;
    private const float SideOffset = 150f;

    /// <summary>Vuruş anında hedefe bırakılan mesafe — silah boyu kadar.</summary>
    private const float MeleeRange = 110f;

    private readonly Dictionary<WarriorId, WarriorRig> _rigs = [];
    private readonly Dictionary<WarriorId, Vector2> _homes = [];

    private BattleSetup _setup = null!;
    private Battle _battle = null!;
    private BattleHud _hud = null!;
    private double _accumulator;
    private int _consumedEvents;
    private bool _reported;

    /// <summary>Dövüşün seed'i. Aynı seed aynı dövüşü verir — tekrar izlemek bedava.</summary>
    [Export]
    public long Seed { get; set; } = 20260806;

    /// <summary>Oynatma hızı. 1 = gerçek zaman; denge bakarken hızlandırmak için.</summary>
    [Export]
    public double SpeedMultiplier { get; set; } = 1.0;

    public override void _Ready()
    {
        // Kadro bir kez kurulur: rig'ler ile dövüş aynı savaşçı nesnelerini görmeli.
        _setup = DemoSetup();
        Seed = SeedFromCommandLine() ?? Seed;
        _battle = new Battle(_setup, new SeededRandom((ulong)Seed));

        BuildArena();
        SpawnRigs();

        _hud = new BattleHud();
        AddChild(_hud);
        _hud.Build(_battle, _setup, Seed, CommandRetreat);

        GD.Print($"Dövüş başladı: seed {Seed}, {_rigs.Count} savaşçı sahnede.");
    }

    public override void _Process(double delta)
    {
        AdvanceBattle(delta);
        ConsumeEvents();
        DriveRigs(delta);
        _hud.Refresh(_battle);
    }

    // ------------------------------------------------------------- simülasyon

    /// <summary>
    /// Gerçek zamanı çekirdeğin sabit adımına çevirir.
    /// </summary>
    /// <remarks>
    /// Kare süresi değişken, çözümleyicinin adımı sabit (<c>TickSeconds</c>). Biriktirip
    /// sabit adımlarla ilerletmek determinizmi korur: aynı seed, kare hızından bağımsız
    /// olarak aynı dövüşü verir.
    /// </remarks>
    private void AdvanceBattle(double delta)
    {
        if (_battle.IsFinished)
        {
            return;
        }

        double tick = CombatTuning.Default.TickSeconds;
        _accumulator += delta * SpeedMultiplier;

        // Uzun bir takılmadan sonra tek karede yüzlerce adım atıp dövüşü ışınlamamak
        // için üst sınır: fazlası düşürülür.
        int budget = 20;

        while (_accumulator >= tick && budget-- > 0)
        {
            _accumulator -= tick;

            if (!_battle.Step())
            {
                break;
            }
        }

        if (_accumulator > tick * 4)
        {
            _accumulator = 0;
        }
    }

    /// <summary>"Çek" tuşu — komut ekibin tamamını kapsar (bkz. docs/GDD.md §5).</summary>
    private void CommandRetreat() => _battle.CommandRetreat();

    /// <summary>
    /// <c>-- --seed 52</c> ile verilen seed'i okur.
    /// </summary>
    /// <remarks>
    /// Determinizmin pratik karşılığı: toplu simülasyon ilginç bir dövüş bildirdiğinde
    /// ("52 numaralı seed'de savaşçı kolunu kaybediyor") o dövüş burada birebir
    /// izlenebilir. Hata ayıklamanın ana yolu bu.
    /// </remarks>
    private static long? SeedFromCommandLine()
    {
        string[] args = OS.GetCmdlineUserArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--seed" && long.TryParse(args[i + 1], out long seed))
            {
                return seed;
            }
        }

        return null;
    }

    // ------------------------------------------------------------ olay akışı

    /// <summary>
    /// Son kareden beri üretilen olayları görsel tepkilere çevirir.
    /// </summary>
    /// <remarks>
    /// Olay listesi yalnızca büyür; tükettiğimiz yeri sayaçla takip etmek yeterli.
    /// Yeni bir olay türü eklendiğinde burada karşılığı yoksa sessizce yok sayılır —
    /// çekirdek görselleştirmeyi beklemek zorunda değil.
    /// </remarks>
    private void ConsumeEvents()
    {
        IReadOnlyList<BattleEvent> events = _battle.Events;

        for (; _consumedEvents < events.Count; _consumedEvents++)
        {
            switch (events[_consumedEvents])
            {
                case AttackLanded landed:
                    Rig(landed.Defender)?.Flinch();
                    break;

                case WarriorDismembered lost:
                    Rig(lost.Warrior)?.Dismember(lost.Part);
                    break;

                case WarriorDied died:
                    Rig(died.Warrior)?.Die();
                    break;

                default:
                    break;
            }
        }

        if (_battle.IsFinished && !_reported)
        {
            _reported = true;
            GD.Print($"Dövüş bitti: {_battle.Result!.Outcome} ({_battle.Result.ElapsedSeconds:F1} sn)");
        }
    }

    private void DriveRigs(double delta)
    {
        IReadOnlyList<CombatantSnapshot> snapshots = _battle.Snapshots();

        foreach (CombatantSnapshot snapshot in snapshots)
        {
            if (!_rigs.TryGetValue(snapshot.Id, out WarriorRig? rig))
            {
                continue;
            }

            rig.ApplyState(snapshot.State, snapshot.StateProgress, delta);
            rig.Position = PositionFor(snapshot, snapshots);
        }
    }

    /// <summary>
    /// Savaşçının sahnedeki yeri: hattında bekler, vururken hedefe dalar,
    /// çekilirken arenadan çıkar.
    /// </summary>
    /// <remarks>
    /// Hamle sabit bir mesafe değil, <b>hedefe kadar</b>: çekirdek herkesi aynı ön
    /// sıradaki düşmana yönelttiği için arkadaki savaşçılar sabit hamleyle boşluğa
    /// kılıç sallardı. Mesafeyi hedeften hesaplamak, çözümlemedeki hedef seçimiyle
    /// ekrandaki hareketi aynı şeye bağlar.
    /// </remarks>
    private Vector2 PositionFor(CombatantSnapshot snapshot, IReadOnlyList<CombatantSnapshot> all)
    {
        Vector2 home = _homes[snapshot.Id];
        float facing = snapshot.Team == Battle.PlayerTeam ? 1 : -1;
        float progress = (float)snapshot.StateProgress;

        switch (snapshot.State)
        {
            case CombatState.AttackWindup:
                // Kılıcı toplarken hafif geri yaslanma.
                return home - new Vector2(facing * 16f * Ease(progress), 0);

            case CombatState.AttackRecovery:
            {
                // Vuruş erken iner, kalanı geri çekilmedir — hamle de aynı ritmi izler.
                const float strikeAt = 0.35f;
                float lunge = progress < strikeAt
                    ? progress / strikeAt
                    : 1f - ((progress - strikeAt) / (1f - strikeAt));

                return home.Lerp(StrikePosition(snapshot, all, home, facing), Ease(Math.Clamp(lunge, 0, 1)));
            }

            case CombatState.Retreating:
                return home - new Vector2(facing * 460f * Ease(progress), 0);

            default:
                return home;
        }
    }

    /// <summary>
    /// Vuruşun ineceği nokta: çekirdeğin seçtiği hedefin bir silah boyu önü.
    /// </summary>
    /// <remarks>
    /// Hedef seçimi <c>Battle.FindTarget</c> ile aynı kuralı izler — karşı taraftaki
    /// <b>ilk ayakta</b> savaşçı. Anlık görüntüler kurulum sırasında geldiği için
    /// listedeki sıra o kuralla birebir aynı.
    /// </remarks>
    private Vector2 StrikePosition(
        CombatantSnapshot attacker,
        IReadOnlyList<CombatantSnapshot> all,
        Vector2 home,
        float facing)
    {
        foreach (CombatantSnapshot other in all)
        {
            if (other.Team != attacker.Team && other.IsActive)
            {
                return new Vector2(_homes[other.Id].X - (facing * MeleeRange), home.Y);
            }
        }

        return home;
    }

    private static float Ease(float t) => t * t * (3f - (2f * t));

    private WarriorRig? Rig(WarriorId id) => _rigs.GetValueOrDefault(id);

    // --------------------------------------------------------------- kurulum

    private void SpawnRigs()
    {
        Spawn(_setup.PlayerSide, Battle.PlayerTeam);
        Spawn(_setup.EnemySide, Battle.EnemyTeam);
    }

    private void Spawn(IReadOnlyList<Warrior> side, int team)
    {
        bool isPlayer = team == Battle.PlayerTeam;
        float direction = isPlayer ? -1 : 1;

        for (int i = 0; i < side.Count; i++)
        {
            Warrior warrior = side[i];

            // Ön sıradaki savaşçı hedefe en yakın; çekirdek de sırayla hedef seçiyor.
            var home = new Vector2(
                (960f + (direction * SideOffset)) + (direction * i * LaneGap),
                GroundY);

            var rig = new WarriorRig { Position = home, ZIndex = -i };
            AddChild(rig);
            rig.Build(warrior, isPlayer ? PlayerTint : EnemyTint, isPlayer ? 1 : -1);

            _rigs[warrior.Id] = rig;
            _homes[warrior.Id] = home;
        }
    }

    private void BuildArena()
    {
        var ground = new Line2D
        {
            Points = [new Vector2(0, GroundY), new Vector2(1920, GroundY)],
            Width = 4f,
            DefaultColor = new Color(0.32f, 0.30f, 0.28f),
            ZIndex = -100,
        };

        AddChild(ground);
    }

    /// <summary>
    /// Geçici kadro. Faz 3'te gerçek roster meta katmandan gelecek; şimdilik
    /// görselleştirmeyi çalıştırmaya yetecek kadarı burada duruyor.
    /// </summary>
    private static BattleSetup DemoSetup() => new(
        [
            new Warrior(new WarriorId(1), "Acemi", WarriorStats.Recruit(), Weapon.Katana(), Armor.Light()),
            new Warrior(
                new WarriorId(2),
                "Kıdemli",
                WarriorStats.Recruit() with { Strength = 55, Accuracy = 62, Defense = 45 },
                Weapon.Nodachi(),
                Armor.Medium()),
            new Warrior(
                new WarriorId(3),
                "Mızrakçı",
                WarriorStats.Recruit() with { Evasion = 50, Aggression = 50 },
                Weapon.Yari(),
                Armor.Light()),
        ],
        [
            Yokai(101, "Oni", 150, 55, 30, 15, 60, Weapon.Tetsubo()),
            Yokai(102, "Kappa", 85, 65, 15, 35, 35, Weapon.Katana()),
            Yokai(103, "Tengu", 90, 70, 10, 50, 40, Weapon.Katana()),
        ])
    {
        // Oyuncu tuşuyla oynanıyor: politika yok.
        RetreatPolicy = null,
    };

    private static Warrior Yokai(
        int id,
        string name,
        double health,
        double aggression,
        double defense,
        double evasion,
        double strength,
        Weapon weapon) =>
        new(
            new WarriorId(id),
            name,
            new WarriorStats(health, aggression, defense, evasion, strength, Accuracy: 58, MaxStamina: 100),
            weapon);
}
