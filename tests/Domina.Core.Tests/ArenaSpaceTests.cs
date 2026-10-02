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
/// engine's space behind the door is consulted the way the kinematic one is.
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

    [Fact]
    public void OneSpaceServesFightAfterFight()
    {
        // The first fight leaves men dead in the space; the second must not find them standing there.
        var space = new KinematicArenaSpace(CombatTuning.Default);
        BattleResult first = new Battle(ThreeVsThree(), new SeededRandom(5), space).Run();
        Assert.Contains(first.Summaries, s => s.FinalState is CombatState.Dead);

        BattleResult reused = new Battle(ThreeVsThree(), new SeededRandom(17), space).Run();
        BattleResult fresh = new Battle(ThreeVsThree(), new SeededRandom(17)).Run();

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
