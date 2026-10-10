using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Domina.Core.Combat;
using Domina.Game.Combat;
using Domina.Presentation;
using Domina.Sim;
using Godot;

namespace Domina.Game.Bed;

/// <summary>
/// The engine bed: fights run headless inside Godot, timed and fingerprinted.
/// </summary>
/// <remarks>
/// <para>
/// This is Phase A of docs/PLAN-ENGINE-RESOLUTION.md, the plan's gate. It answers two questions before
/// any of <c>Battle.cs</c> moves: can a headless Godot step fights fast enough to sweep (10,000 fights in
/// 10 s, the figure <c>ThroughputTests</c> pins on the core), and does the same seed give the same fight
/// twice on the same machine and build. Every fight's fingerprint goes to <c>--digest</c>; two runs are
/// the same only if the two files are.
/// </para>
/// <para>
/// <b>Two lanes.</b> The manual lane steps one fight at a time in the bed's own loop, all of it inside a
/// single engine frame — the shape an unwatched fight would take. The frame lane lets the engine's fixed
/// physics step drive the tick, one tick a physics frame, with many fights side by side, each in its own
/// <c>SubViewport</c> and so its own physics space — the shape a watched fight takes, run flat out
/// (Godot's <c>--fixed-fps 20</c> turns off its real-time sync).
/// </para>
/// <para>
/// <b>Three fighters.</b> <c>--fighter trivial</c> is the stand-in Phase A was read on.
/// <c>--fighter core</c> is the engine-free resolver, <see cref="Battle"/> over its own kinematics: the
/// calibration that says whether this runtime is as fast as the one the core's figures were taken on.
/// <c>--fighter engine</c> is the same resolver carrying out its steps on Godot's physics server
/// (<see cref="GodotArenaSpace"/>) — Phase B's first slice. The two resolver fighters fight the arena's
/// demo roster.
/// </para>
/// <para>
/// <b>The sim's measurement.</b> <c>--sim</c> hands the rest of the command line to <c>Domina.Sim</c>, and
/// the sim's duel bed or campaign asks the bed's resolver fighter for its fights (<see cref="RunSim"/>).
/// </para>
/// </remarks>
public sealed partial class EngineBed : Node
{
    private readonly List<Slot> _slots = [];
    private readonly List<GodotArenaSpace> _spaces = [];
    private BedOptions _options = BedOptions.Default;
    private TrivialKit? _kit;
    private FightCosts? _costs;
    private FightRecord[] _records = [];
    private int _nextFight;
    private int _recorded;
    private long _started;
    private bool _manualRan;
    private int _frames;
    private double _insideSeconds;
    private bool _over;

    public override void _Ready()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

        _options = BedOptions.Parse(OS.GetCmdlineUserArgs(), out string? error);
        if (error is not null)
        {
            GD.PrintErr($"engine bed: {error}");
            GD.Print(BedOptions.Usage);
            Stop(2);
            return;
        }

        Engine.PhysicsTicksPerSecond = (int)Math.Round(1.0 / TrivialFight.TickSeconds);

        GD.Print($"engine bed - {_options.Describe()}");
        GD.Print($"build: Godot {Engine.GetVersionInfo()["string"]}, .NET {System.Environment.Version}; optimised: "
            + $"Domina.Game {Optimised(typeof(EngineBed))}, Domina.Core {Optimised(typeof(Battle))}, "
            + $"Domina.Sim {Optimised(typeof(SimCli))}, GodotSharp {Optimised(typeof(Node))} ({typeof(Node).Assembly.Location})");

        if (_options.Sim is not null)
        {
            // The sim's measurement runs in the first frame, as the manual lane does.
            SetProcess(true);
            SetPhysicsProcess(false);
            return;
        }

        _records = new FightRecord[_options.Fights];

        _kit = _options.Fighter == BedFighter.Trivial ? new TrivialKit() : null;
        _costs = _options.Profile ? new FightCosts() : null;
        int slots = _options.Lane == BedLane.Frame ? Math.Min(_options.Slots, _options.Fights) : 1;
        for (int i = 0; i < slots; i++)
        {
            _slots.Add(NewSlot(i));
        }

        if (!SpacesAreSeparate())
        {
            GD.PrintErr("engine bed: two slots share a physics space; their fights would touch.");
            Stop(3);
            return;
        }

        SetProcess(_options.Lane == BedLane.Manual);
        SetPhysicsProcess(_options.Lane == BedLane.Frame);
    }

    public override void _ExitTree()
    {
        _kit?.Dispose();
        foreach (GodotArenaSpace space in _spaces)
        {
            space.Dispose();
        }
    }

    /// <summary>The manual lane: the whole run inside the first engine frame.</summary>
    public override void _Process(double delta)
    {
        if (_manualRan || _over)
        {
            return;
        }

        _manualRan = true;
        _started = Stopwatch.GetTimestamp();

        if (_options.Sim is not null)
        {
            RunSim(_options.Sim);
            return;
        }

        Slot slot = _slots[0];
        for (int i = 0; i < _options.Fights; i++)
        {
            Begin(slot, i);
            while (!slot.Fight.Finished)
            {
                slot.Fight.Step();
            }

            Record(slot);
        }

        Report();
    }

    /// <summary>
    /// The frame lane: one tick of every running fight per physics frame.
    /// </summary>
    /// <remarks>
    /// A fight is reset in one physics callback and first stepped in the next, whether it is among the
    /// first or fills a slot another fight left — so every fight sees the same order of callbacks and
    /// server steps, and its result cannot depend on which slot it drew.
    /// </remarks>
    public override void _PhysicsProcess(double delta)
    {
        if (_over)
        {
            return;
        }

        long entered = Stopwatch.GetTimestamp();

        if (_started == 0)
        {
            _started = entered;
            foreach (Slot slot in _slots)
            {
                Begin(slot, _nextFight++);
            }

            return;
        }

        if (Math.Abs(delta - TrivialFight.TickSeconds) > 1e-9)
        {
            GD.PrintErr($"engine bed: the physics step is {delta} s, the fight's tick {TrivialFight.TickSeconds} s.");
            Stop(3);
            return;
        }

        _frames++;
        foreach (Slot slot in _slots)
        {
            if (slot.FightIndex < 0)
            {
                continue;
            }

            slot.Fight.Step();
            if (slot.Fight.Finished)
            {
                Record(slot);
                if (_nextFight < _options.Fights)
                {
                    Begin(slot, _nextFight++);
                }
            }
        }

        _insideSeconds += Stopwatch.GetElapsedTime(entered).TotalSeconds;

        // Real-time sync would show as frames that take a twentieth of a second however little is in them.
        double outside = Stopwatch.GetElapsedTime(_started).TotalSeconds - _insideSeconds;
        if (_frames == 100 && outside > 100 * TrivialFight.TickSeconds * 0.8)
        {
            GD.PrintErr("engine bed: the engine is waiting out real time between frames; pass --fixed-fps 20 before the --.");
            Stop(3);
            return;
        }

        if (_recorded == _options.Fights)
        {
            Report();
        }
    }

    private static string Optimised(Type type)
    {
        DebuggableAttribute? debuggable = type.Assembly.GetCustomAttribute<DebuggableAttribute>();
        return debuggable is { IsJITOptimizerDisabled: true } ? "NO" : "yes";
    }

    private Slot NewSlot(int index)
    {
        // A SubViewport carries its own World2D, and with it its own physics space: the fights in two
        // slots never see each other's bodies. Nothing is drawn — the bed is headless and the rendering
        // server is the dummy one — but the nodes are the same nodes a watched fight would draw.
        var viewport = new SubViewport
        {
            Name = $"Slot{index}",
            Disable3D = true,
            Size = new Vector2I(2, 2),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        AddChild(viewport);
        return new Slot(viewport, NewFight(viewport));
    }

    private IBedFight NewFight(SubViewport viewport)
    {
        if (_options.Fighter == BedFighter.Trivial)
        {
            return new TrivialFight(viewport, _kit!, _options) { Costs = _costs };
        }

        // The roster the arena opens on, without the event stream: batch simulation's real condition,
        // as in ThroughputTests.
        BattleSetup setup = DemoRoster.Setup() with { CollectEvents = false };
        if (_options.Fighter == BedFighter.Core)
        {
            return new BattleBedFight(setup, null);
        }

        var space = new GodotArenaSpace(setup.Tuning, _options.Motion) { Costs = _costs };
        _spaces.Add(space);
        return new BattleBedFight(setup, space);
    }

    private bool SpacesAreSeparate()
    {
        var spaces = new HashSet<Rid>();
        foreach (Slot slot in _slots)
        {
            if (!spaces.Add(slot.Viewport.World2D.Space))
            {
                return false;
            }
        }

        return true;
    }

    private void Begin(Slot slot, int fightIndex)
    {
        if (_options.Rebuild)
        {
            // Free, not QueueFree: a body queued for deletion stays in the space until the frame ends,
            // and the manual lane's next fight would collide with it.
            ((TrivialFight)slot.Fight).Root.Free();
            slot.Fight = NewFight(slot.Viewport);
        }

        slot.FightIndex = fightIndex;
        slot.Fight.Reset(SeedOf(fightIndex));
    }

    private void Record(Slot slot)
    {
        _records[slot.FightIndex] = slot.Fight.Record(SeedOf(slot.FightIndex));
        slot.FightIndex = -1;
        _recorded++;
    }

    private ulong SeedOf(int fightIndex) => _options.FirstSeed + (ulong)fightIndex;

    /// <summary>
    /// The sim's own measurement — a duel bed or a campaign — with its fights fought by the bed's fighter.
    /// </summary>
    /// <remarks>
    /// This is the parity bed of docs/PLAN-ENGINE-RESOLUTION.md: the same command line that wrote the
    /// pre-engine baseline, run once over the resolver's own kinematics inside this runtime (which has to
    /// give the baseline back line for line) and once over the engine's ground.
    /// </remarks>
    private void RunSim(IReadOnlyList<string> arguments)
    {
        using GodotFightRunner? engine = _options.Fighter == BedFighter.Engine ? new GodotFightRunner(_options.Motion) : null;
        IFightRunner fighter = engine is null ? CoreFightRunner.Instance : engine;
        var error = new StringWriter();
        int exit;

        if (_options.ReportPath is { } path)
        {
            using var report = new StreamWriter(path) { NewLine = "\n" };
            exit = SimCli.Run(arguments, report, error, fighter);
        }
        else
        {
            using var report = new StringWriter { NewLine = "\n" };
            exit = SimCli.Run(arguments, report, error, fighter);
            GD.Print(report.ToString().TrimEnd());
        }

        if (error.GetStringBuilder().Length > 0)
        {
            GD.PrintErr(error.ToString().TrimEnd());
        }

        double seconds = Stopwatch.GetElapsedTime(_started).TotalSeconds;
        if (engine is { Fights: > 0 })
        {
            GD.Print($"fights {engine.Fights}; motion tests {engine.MotionTests} ({(double)engine.MotionTests / engine.Fights:F1} a fight), "
                + $"places handed to the server {engine.Handovers} ({(double)engine.Handovers / engine.Fights:F1} a fight)");
        }

        GD.Print($"RESULT {_options.Describe()} exit={exit} seconds={seconds:F3}");
        Stop(exit);
    }

    private void Report()
    {
        double seconds = Stopwatch.GetElapsedTime(_started).TotalSeconds;
        int fights = _options.Fights;
        long ticks = 0;
        long blows = 0;
        long hits = 0;
        ulong total = FightDigest.Empty;
        var outcomes = new SortedDictionary<int, int>();

        foreach (FightRecord r in _records)
        {
            ticks += r.Ticks;
            blows += r.Blows;
            hits += r.Hits;
            total = FightDigest.Mix(total, r.Digest);
            outcomes[r.Outcome] = outcomes.GetValueOrDefault(r.Outcome) + 1;
        }

        GD.Print($"fights {fights} (seeds {SeedOf(0)}..{SeedOf(fights - 1)}) in {seconds:F3} s = "
            + $"{fights / seconds:F0} fights/s; {ticks} ticks, {ticks * TrivialFight.TickSeconds / fights:F2} s average fight");
        GD.Print("outcomes: " + string.Join(", ", outcomes.Select(o =>
            $"{OutcomeName(o.Key)} {o.Value} ({100.0 * o.Value / fights:F2}%)")));
        GD.Print($"blows {blows}, hits {hits} ({(blows == 0 ? 0 : 100.0 * hits / blows):F1}%)");
        if (_spaces.Count > 0)
        {
            long tests = _spaces.Sum(space => space.MotionTests);
            long handovers = _spaces.Sum(space => space.Handovers);
            GD.Print($"motion tests {tests} ({(double)tests / fights:F1} a fight), "
                + $"places handed to the server {handovers} ({(double)handovers / fights:F1} a fight)");
        }

        if (_options.Lane == BedLane.Frame)
        {
            GD.Print($"frame lane: {_frames} physics frames, {100.0 * (seconds - _insideSeconds) / seconds:F1}% "
                + "of the wall clock outside the bed's step (engine loop and physics server)");
        }

        if (_costs is not null)
        {
            GD.Print("profile (the clip's time includes the blow it calls):");
            GD.Print("  " + _costs.Reset.Describe("reset", seconds));
            GD.Print("  " + _costs.Move.Describe("move", seconds));
            GD.Print("  " + _costs.Blade.Describe("blade", seconds));
            GD.Print("  " + _costs.Clip.Describe("clip", seconds));
        }

        GD.Print($"digest {total:x16}");
        GD.Print($"RESULT {_options.Describe()} fights={fights} seconds={seconds:F3} "
            + $"fights_per_s={fights / seconds:F0} ticks={ticks} digest={total:x16}");

        if (_options.DigestPath is { } path)
        {
            var lines = new StringBuilder();
            foreach (FightRecord r in _records)
            {
                lines.Append(CultureInfo.InvariantCulture, $"{r.Seed} {r.Outcome} {r.Ticks} {r.Digest:x16}\n");
            }

            File.WriteAllText(path, lines.ToString());
        }

        Stop(0);
    }

    private string OutcomeName(int outcome) => _options.Fighter != BedFighter.Trivial
        ? ((BattleOutcome)outcome).ToString()
        : outcome switch
        {
            0 => "side 0",
            1 => "side 1",
            _ => "stalled",
        };

    private void Stop(int exitCode)
    {
        _over = true;
        SetProcess(false);
        SetPhysicsProcess(false);
        GetTree().Quit(exitCode);
    }

    private sealed class Slot(SubViewport viewport, IBedFight fight)
    {
        public SubViewport Viewport { get; } = viewport;

        public IBedFight Fight { get; set; } = fight;

        public int FightIndex { get; set; } = -1;
    }
}
