using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The ground the fight stands on (<see cref="IArenaSpace"/>, docs/PLAN-ENGINE-RESOLUTION.md Phase B).
/// </summary>
/// <remarks>
/// That the kinematic space is the resolver's old movement exactly was shown on the whole baseline
/// (docs/engine-bed/2026-10-phase-b-space/). Pinned here is the other half of the door: the resolver
/// obeys the space it is handed — it stands men where the space says, begins it afresh for every fight,
/// enters the men in the battle's order, and takes a man out of it once, when he leaves the field — so an
/// engine's space behind the door is consulted the way the kinematic one is. And the default ground's own
/// rule (<see cref="SlideArenaSpace"/>, Open Decision #25): a step stops at the man it meets, keeps its
/// sideways part, is not held by a man it walks away from or by one who has left, and holds no fleer.
/// </remarks>
public class ArenaSpaceTests
{
    private static readonly WarriorId[] _order = [new(1), new(2), new(3), new(101), new(102), new(103)];

    private static BattleSetup ThreeVsThree() => new(
        [
            TestBuilders.Warrior(1, evasion: 30, defense: 20, armor: Armor.Light()),
            TestBuilders.Warrior(2, evasion: 20, defense: 30, weapon: Weapon.Nodachi(), armor: Armor.Medium()),
            TestBuilders.Warrior(3, evasion: 40, defense: 10, weapon: Weapon.Yari()),
        ],
        [
            TestBuilders.Warrior(101, "Oni", health: 140, aggression: 55, defense: 25, weapon: Weapon.Tetsubo()),
            TestBuilders.Warrior(102, "Kappa", health: 70, aggression: 65, evasion: 35),
            TestBuilders.Warrior(103, "Tengu", health: 80, aggression: 70, evasion: 50),
        ]);

    [Theory]
    [InlineData(ArenaGround.Slide)]
    [InlineData(ArenaGround.Kinematic)]
    public void OneSpaceServesFightAfterFight(ArenaGround ground)
    {
        // The first fight leaves men dead in the space; the second must not find them standing there.
        IArenaSpace space = ground == ArenaGround.Slide
            ? new SlideArenaSpace(CombatTuning.Default)
            : new KinematicArenaSpace(CombatTuning.Default);
        BattleSetup setup = ThreeVsThree() with { Tuning = CombatTuning.Default with { Ground = ground } };
        BattleResult first = new Battle(setup, new SeededRandom(5), space).Run();
        Assert.Contains(first.Summaries, s => s.FinalState is CombatState.Dead);

        BattleResult reused = new Battle(setup, new SeededRandom(17), space).Run();
        BattleResult fresh = new Battle(setup, new SeededRandom(17)).Run();

        Assert.Equal(fresh.Outcome, reused.Outcome);
        Assert.Equal(fresh.ElapsedSeconds, reused.ElapsedSeconds, precision: 9);
        Assert.Equal(fresh.Summaries, reused.Summaries);
    }

    [Fact]
    public void TheResolverStandsMenWhereTheSpaceSays()
    {
        // A space that lets nobody take a step: the two lines stay where the formation put them, nobody
        // comes into reach, and the fight runs to the guard without a blow.
        BattleResult result = new Battle(
            ThreeVsThree() with { CollectEvents = false },
            new SeededRandom(17),
            new FrozenSpace()).Run();

        Assert.Equal(BattleOutcome.Stalled, result.Outcome);
        Assert.All(result.Summaries, s => Assert.Equal(0, s.HitsLanded));
    }

    [Fact]
    public void TheMenEnterInTheBattlesOrderAndLeaveOnce()
    {
        var space = new RecordingSpace(new KinematicArenaSpace(CombatTuning.Default));
        BattleResult result = new Battle(ThreeVsThree(), new SeededRandom(17), space).Run();

        Assert.Equal([6], space.Begun);
        Assert.Equal([0, 1, 2, 3, 4, 5], space.Entered);

        // Someone fell before the end; the last man down can fall on the final tick and never be taken
        // out, because the space hears of a departure at the start of the next tick's movement.
        Assert.NotEmpty(space.Left);
        Assert.Equal(space.Left.Distinct().Count(), space.Left.Count);

        foreach (int body in space.Left)
        {
            Assert.True(result.SummaryFor(_order[body]).FinalState
                is CombatState.Dead or CombatState.Escaped or CombatState.Yielded);

            int leftAt = space.Calls.IndexOf(("leave", body));
            Assert.DoesNotContain(space.Calls.Skip(leftAt), call => call == ("settle", body));
        }
    }

    /// <summary>The ground a fight is handed no space for is the one its tuning names, the slide by default.</summary>
    [Fact]
    public void TheTuningNamesTheGroundAndTheSlideIsTheDefault()
    {
        Assert.Equal(ArenaGround.Slide, CombatTuning.Default.Ground);

        // Seeds where the two rules part: an unhanded fight follows its tuning's rule, not the other.
        BattleSetup setup = ThreeVsThree() with { CollectEvents = false };
        BattleSetup old = setup with { Tuning = CombatTuning.Default with { Ground = ArenaGround.Kinematic } };
        int parted = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            BattleResult byDefault = new Battle(setup, new SeededRandom(seed)).Run();
            BattleResult slid = new Battle(setup, new SeededRandom(seed), new SlideArenaSpace(setup.Tuning)).Run();
            BattleResult byOld = new Battle(old, new SeededRandom(seed)).Run();
            BattleResult pushed = new Battle(old, new SeededRandom(seed), new KinematicArenaSpace(old.Tuning)).Run();

            Assert.Equal(slid.Summaries, byDefault.Summaries);
            Assert.Equal(pushed.Summaries, byOld.Summaries);
            parted += slid.Summaries.SequenceEqual(pushed.Summaries) ? 0 : 1;
        }

        Assert.True(parted > 0, "no seed parted the two rules; the test reads nothing");
    }

    /// <summary>A man walking straight at another stops at his edge rather than passing into him.</summary>
    [Fact]
    public void AStepIntoAManStopsAtHisEdge()
    {
        var space = Ground(out double size);
        space.Enter(0, new ArenaPoint(100, 100));
        space.Enter(1, new ArenaPoint(200, 100));

        ArenaPoint at = space.Settle(0, new ArenaPoint(180, 100), passing: false);

        Assert.Equal(200 - size, at.X, precision: 9);
        Assert.Equal(100, at.Y, precision: 9);
    }

    /// <summary>
    /// A step that meets a man at a slant keeps its sideways part: he slides along the other's edge and
    /// gets further than a stop, but never into him.
    /// </summary>
    [Fact]
    public void AStepAtASlantSlidesAlongTheManItMeets()
    {
        var space = Ground(out double size);
        var other = new ArenaPoint(200, 100);
        space.Enter(0, new ArenaPoint(100, 90));
        space.Enter(1, other);

        ArenaPoint at = space.Settle(0, new ArenaPoint(160, 70), passing: false);

        Assert.True(at.DistanceTo(other) >= size - 1e-9);
        Assert.True(at.X > 200 - size - 30, "the step stopped dead instead of sliding");
        Assert.True(at.Y < 90, "the slide did not keep the step's sideways part");
    }

    /// <summary>A man already overlapping another is put out of him first, and walking away is not blocked.</summary>
    [Fact]
    public void AnOverlappingManIsPutOutAndWalksAwayFreely()
    {
        var space = Ground(out double size);
        space.Enter(0, new ArenaPoint(100, 100));
        space.Enter(1, new ArenaPoint(110, 100));

        ArenaPoint at = space.Settle(0, new ArenaPoint(80, 100), passing: false);

        Assert.Equal(110 - size - 20, at.X, precision: 9);
    }

    /// <summary>A fleer passes through anyone and past the edge; everyone else is held by it.</summary>
    [Fact]
    public void AFleerPassesAndTheEdgeHoldsTheRest()
    {
        var space = Ground(out _);
        space.Enter(0, new ArenaPoint(100, 100));
        space.Enter(1, new ArenaPoint(130, 100));
        space.Enter(2, new ArenaPoint(5, 400));

        Assert.Equal(new ArenaPoint(160, 100), space.Settle(0, new ArenaPoint(160, 100), passing: true));
        Assert.Equal(new ArenaPoint(-30, 400), space.Settle(2, new ArenaPoint(-30, 400), passing: true));

        space.Leave(2);
        space.Enter(2, new ArenaPoint(5, 400));
        Assert.Equal(new ArenaPoint(0, 400), space.Settle(2, new ArenaPoint(-30, 400), passing: false));
    }

    /// <summary>A man who has left the field stands in nobody's way.</summary>
    [Fact]
    public void AManWhoLeftStopsNobody()
    {
        var space = Ground(out _);
        space.Enter(0, new ArenaPoint(100, 100));
        space.Enter(1, new ArenaPoint(200, 100));
        space.Leave(1);

        Assert.Equal(new ArenaPoint(200, 100), space.Settle(0, new ArenaPoint(200, 100), passing: false));
    }

    private static SlideArenaSpace Ground(out double size)
    {
        size = CombatTuning.Default.PersonalSpace;
        var space = new SlideArenaSpace(CombatTuning.Default);
        space.Begin(3);
        return space;
    }

    /// <summary>Lets nobody move: every step ends where it began.</summary>
    private sealed class FrozenSpace : IArenaSpace
    {
        private ArenaPoint[] _at = [];

        public void Begin(int bodies) => _at = new ArenaPoint[bodies];

        public ArenaPoint Enter(int body, ArenaPoint at) => _at[body] = at;

        public ArenaPoint Settle(int body, ArenaPoint intended, bool passing) => _at[body];

        public void Leave(int body)
        {
        }
    }

    /// <summary>Passes everything to another space and writes down what it was asked.</summary>
    private sealed class RecordingSpace(IArenaSpace inner) : IArenaSpace
    {
        public List<int> Begun { get; } = [];

        public List<int> Entered { get; } = [];

        public List<int> Left { get; } = [];

        public List<(string Kind, int Body)> Calls { get; } = [];

        public void Begin(int bodies)
        {
            Begun.Add(bodies);
            inner.Begin(bodies);
        }

        public ArenaPoint Enter(int body, ArenaPoint at)
        {
            Entered.Add(body);
            return inner.Enter(body, at);
        }

        public ArenaPoint Settle(int body, ArenaPoint intended, bool passing)
        {
            Calls.Add(("settle", body));
            return inner.Settle(body, intended, passing);
        }

        public void Leave(int body)
        {
            Left.Add(body);
            Calls.Add(("leave", body));
            inner.Leave(body);
        }
    }
}
