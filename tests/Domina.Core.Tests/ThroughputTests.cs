using System.Diagnostics;
using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Phase 1's acceptance criterion: <b>10,000 fights must run in under 10 seconds</b>.
/// </summary>
/// <remarks>
/// This is not a micro-optimisation test but the architecture's health check. Balance work runs on the
/// loop "change a number, run tens of thousands of fights, look at the rate"; if that loop takes
/// minutes, nobody does balance in practice. If an engine dependency or a heavy per-fight allocation
/// leaks into the core, it shows here first.
/// </remarks>
[Collection(ThroughputGroup.Name)]
public class ThroughputTests
{
    private const int _battles = 10_000;
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(10);

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
        ])
    {
        // Batch simulation's real condition: the event stream is not collected.
        CollectEvents = false,
    };

    [Fact]
    public void TenThousandBattlesRunWithinTheBudget()
    {
        BattleSetup setup = ThreeVsThree();

        // The roster is reused; Battle does not change the warriors' persistent state.
        long started = Stopwatch.GetTimestamp();
        int finished = 0;

        for (int i = 0; i < _battles; i++)
        {
            BattleResult result = new Battle(setup, new SeededRandom((ulong)i + 1)).Run();
            if (result.Outcome != BattleOutcome.Stalled)
            {
                finished++;
            }
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

        Assert.True(
            elapsed < _budget,
            $"{_battles} fights took {elapsed.TotalSeconds:F2} s; the budget is {_budget.TotalSeconds:F0} s.");

        // Most of the fights must actually resolve — if they all ended on the time limit, being "fast"
        // would prove nothing.
        Assert.True(finished > _battles * 0.9, $"Only {finished} of {_battles} fights resolved.");
    }

    [Fact]
    public void PerBattleAllocationStaysSmallWithoutEvents()
    {
        BattleSetup setup = ThreeVsThree();

        // Warm-up: so the JIT and the first allocations do not enter the measurement.
        for (int i = 0; i < 50; i++)
        {
            _ = new Battle(setup, new SeededRandom((ulong)i)).Run();
        }

        // Not the whole process but this thread: because the tests run in parallel, a
        // GC.GetTotalAllocatedBytes measurement would be polluted by other tests' allocations.
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 200; i++)
        {
            _ = new Battle(setup, new SeededRandom((ulong)i)).Run();
        }

        long perBattle = (GC.GetAllocatedBytesForCurrentThread() - before) / 200;

        // A few KB per fight (the warrior states + the summary) is expected. Much above that means the
        // event stream or some other list is leaking.
        Assert.True(perBattle < 16 * 1024, $"{perBattle} bytes were allocated per fight.");
    }
}

/// <summary>Runs the timing tests on their own.</summary>
/// <remarks>
/// The budget is measured by wall clock: when another test class runs at the same time and keeps the
/// core busy, the measurement measures the machine's current load rather than the combat resolver's
/// speed. As the number of classes grows this is inevitable — which is why the measurement runs alone.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ThroughputGroup
{
    public const string Name = "throughput";
}
