using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;
using Domina.Presentation;
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
/// Görsel iki kanaldan sürülür: <b>sürekli</b> hal anlık görüntülerden (duruş, konum,
/// can), <b>anlık</b> tepkiler olay akışından (<see cref="ReactionReader"/>). Bu ayrım
/// korunmalı — olaylar tek seferliktir, tekrar oynatılamaz.
/// </para>
/// <para>
/// Kararların kendisi burada değil <c>Domina.Presentation</c>'da: kim nerede durur,
/// hangi olay hangi tepkiyi doğurur, tuşta ne yazar. Bu sınıfa kalan iş sahneyi kurmak
/// ve sonucu düğümlere uygulamak — böylece sunum mantığı motor açmadan test edilebiliyor.
/// </para>
/// </remarks>
public sealed partial class BattleArena : Node2D
{
    private static readonly Color PlayerTint = new(0.42f, 0.62f, 0.86f);
    private static readonly Color EnemyTint = new(0.80f, 0.38f, 0.34f);

    private readonly Dictionary<WarriorId, WarriorRig> _rigs = [];
    private readonly ReactionReader _reactions = new();

    private ArenaChoreography _choreography = null!;
    private BattleSetup _setup = null!;
    private Battle _battle = null!;
    private BattleHud _hud = null!;
    private double _accumulator;
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
        _setup = DemoRoster.Setup();

        ArenaArguments arguments = ArenaArguments.Parse(OS.GetCmdlineUserArgs());
        Seed = arguments.Seed ?? Seed;
        SpeedMultiplier = arguments.SpeedMultiplier ?? SpeedMultiplier;

        _battle = new Battle(_setup, new SeededRandom((ulong)Seed));
        _choreography = new ArenaChoreography(new ArenaLayout());

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
        PlayReactions();
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

    // ------------------------------------------------------------ olay akışı

    /// <summary>Son kareden beri üretilen olayların görsel tepkilerini oynatır.</summary>
    private void PlayReactions()
    {
        foreach (RigReaction reaction in _reactions.Drain(_battle.Events))
        {
            Rig(reaction.Warrior)?.React(reaction);
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

            rig.Advance(snapshot.State, snapshot.StateProgress, delta);

            // Konum, ölçek ve çizim sırası derinlikten gelir: savaşçı arena düzleminde
            // gerçekten yürüyor, kamera hâlâ yandan bakıyor.
            ScenePoint spot = _choreography.PositionFor(snapshot);
            float scale = _choreography.ScaleFor(snapshot);

            rig.Position = new Vector2(spot.X, spot.Y);

            // Yön kökün aynalanmasıyla veriliyor (duruş kodu daima sağa bakar sayar),
            // derinlik ölçeği de aynı Scale'e biniyor.
            rig.Scale = new Vector2(ArenaChoreography.FacingOf(snapshot) * scale, scale);
            rig.ZIndex = ArenaChoreography.DrawOrderFor(snapshot);
        }
    }

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

        for (int i = 0; i < side.Count; i++)
        {
            Warrior warrior = side[i];

            // Başlangıç konumunu çekirdek verir; burada yalnızca düğüm kuruluyor.
            var rig = new WarriorRig();
            AddChild(rig);
            rig.Build(warrior, isPlayer ? PlayerTint : EnemyTint, isPlayer ? 1f : -1f);

            _rigs[warrior.Id] = rig;
        }
    }

    private void BuildArena()
    {
        ArenaLayout layout = _choreography.Layout;

        var ground = new Line2D
        {
            Points = [new Vector2(0, layout.FrontGroundY), new Vector2(layout.Width, layout.FrontGroundY)],
            Width = 4f,
            DefaultColor = new Color(0.32f, 0.30f, 0.28f),
            ZIndex = -100,
        };

        AddChild(ground);
    }
}
