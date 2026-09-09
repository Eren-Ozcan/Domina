using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The scene that turns phase 1's event stream into a watchable fight.
/// </summary>
/// <remarks>
/// <para>
/// <b>The arrow points one way:</b> this class consumes the core, the core knows nothing of this class.
/// The visualisation cannot change the fight's outcome — the single exception is the player's "pull
/// out" command, and that goes through <see cref="Battle.CommandRetreat"/> (see CLAUDE.md → Architecture rule).
/// </para>
/// <para>
/// The fight is <b>stepped in real time</b>, not run in advance and replayed: because the player can
/// intervene while the fight runs, the decision has to land in the live simulation.
/// </para>
/// <para>
/// The visuals are driven by two channels: the <b>continuous</b> state from the snapshots (pose,
/// position, health), the <b>instant</b> reactions from the event stream (<see cref="ReactionReader"/>).
/// That separation must hold — events are one-off and cannot be replayed.
/// </para>
/// <para>
/// The decisions themselves are not here but in <c>Domina.Presentation</c>: who stands where, which
/// event produces which reaction, what the key says. What is left to this class is building the scene
/// and applying the result to the nodes — so the presentation logic can be tested without opening the engine.
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

    /// <summary>The fight's seed. The same seed gives the same fight — watching it again is free.</summary>
    [Export]
    public long Seed { get; set; } = 20260806;

    /// <summary>Playback speed. 1 = real time; for speeding up while looking at balance.</summary>
    [Export]
    public double SpeedMultiplier { get; set; } = 1.0;

    /// <summary>
    /// The fight to play. If <c>null</c>, the demo roster is built.
    /// </summary>
    /// <remarks>
    /// The expedition layer sets the fight up with <c>Expedition.Prepare</c> and hands it here; the
    /// arena does no setup, it only plays. If the arena built its own roster, the fight watched and the
    /// fight the dojo closed its books on would be two different fights.
    /// </remarks>
    public BattleSetup? Bout { get; set; }

    /// <summary>
    /// Called when the fight ends — this is the side that closes the books.
    /// </summary>
    /// <remarks>
    /// The arena writes no accounting (death, infirmary, reward, day): <c>Expedition.Settle</c> does
    /// that. The only thing that comes out of here is the <b>raw result</b>.
    /// </remarks>
    public Action<BattleResult>? Finished { get; set; }

    public override void _Ready()
    {
        // The roster is built once: the rigs and the fight must see the same warrior objects.
        _setup = Bout ?? DemoRoster.Setup();

        // The command line only drives the demo fight: the seed of a fight coming from the expedition
        // layer is the day's seed and cannot be changed from outside — the same save must give the same fight.
        if (Bout is null)
        {
            ArenaArguments arguments = ArenaArguments.Parse(OS.GetCmdlineUserArgs());
            Seed = arguments.Seed ?? Seed;
            SpeedMultiplier = arguments.SpeedMultiplier ?? SpeedMultiplier;
        }

        _battle = new Battle(_setup, new SeededRandom((ulong)Seed));
        _choreography = new ArenaChoreography(new ArenaLayout());

        BuildArena();
        SpawnRigs();

        _hud = new BattleHud();
        AddChild(_hud);
        _hud.Build(_battle, _setup, Seed, CommandRetreat);

        GD.Print($"Fight started: seed {Seed}, {_rigs.Count} warriors on the field.");
    }

    public override void _Process(double delta)
    {
        AdvanceBattle(delta);
        PlayReactions();
        DriveRigs(delta);
        _hud.Refresh(_battle);
    }

    // ------------------------------------------------------------- simulation

    /// <summary>
    /// Converts real time into the core's fixed step.
    /// </summary>
    /// <remarks>
    /// The frame duration varies, the resolver's step is fixed (<c>TickSeconds</c>). Accumulating and
    /// advancing in fixed steps preserves determinism: the same seed gives the same fight independently
    /// of the frame rate.
    /// </remarks>
    private void AdvanceBattle(double delta)
    {
        if (_battle.IsFinished)
        {
            return;
        }

        double tick = CombatTuning.Default.TickSeconds;
        _accumulator += delta * SpeedMultiplier;

        // An upper bound so that after a long stall we do not take hundreds of steps in one frame and
        // teleport the fight: the excess is dropped.
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

    /// <summary>The "pull out" key — the command covers the whole party (see docs/GDD.md §5).</summary>
    private void CommandRetreat() => _battle.CommandRetreat();

    // ------------------------------------------------------------- event stream

    /// <summary>Plays the visual reactions of the events produced since the last frame.</summary>
    private void PlayReactions()
    {
        foreach (RigReaction reaction in _reactions.Drain(_battle.Events))
        {
            Rig(reaction.Warrior)?.React(reaction);
        }

        if (_battle.IsFinished && !_reported)
        {
            _reported = true;
            GD.Print($"Fight ended: {_battle.Result!.Outcome} ({_battle.Result.ElapsedSeconds:F1} s)");
            Finished?.Invoke(_battle.Result);
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

            // Position, scale and draw order come from depth: the warrior really walks on the arena
            // plane, the camera still looks from the side.
            ScenePoint spot = _choreography.PositionFor(snapshot);
            float scale = _choreography.ScaleFor(snapshot);

            rig.Position = new Vector2(spot.X, spot.Y);

            // The facing is given by mirroring the root (the pose code always assumes facing right),
            // and the depth scale rides on the same Scale.
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

            // The starting position is given by the core; only the node is built here.
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
