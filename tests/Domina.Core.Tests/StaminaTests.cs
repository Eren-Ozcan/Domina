using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The stamina pool, re-locked in the tenth round (docs/GDD.md §5). Until then an attack spent 6
/// against a regen of 4, so a fight ended long before the pool did and
/// <see cref="WarriorStats.MaxStamina"/> was a number that changed nothing — the conditioning drill and
/// the temple's long breath were both buying air. What is protected here is that the pool <b>binds</b>:
/// the bigger pool wins the long fight, and the default numbers are the ones that make it so.
/// </summary>
public class StaminaTests
{
    [Fact]
    public void TheDefaultNumbersDrainAPoolWithinAFight()
    {
        CombatTuning t = CombatTuning.Default;

        // A second of fighting has to cost more than it gives back, or the pool is decoration.
        Assert.True(t.AttackStaminaCost > t.StaminaRegenPerSecond);
    }

    [Fact]
    public void TheBiggerPoolWinsTheLongFight()
    {
        int deepWins = 0;

        for (int seed = 1; seed <= 200; seed++)
        {
            BattleSetup setup = new(
                [TestBuilders.Warrior(1, "Deep", health: 220, stamina: 180)],
                [TestBuilders.Warrior(2, "Shallow", health: 220, stamina: 60)]);

            BattleResult result = new Battle(setup, new SeededRandom((ulong)seed)).Run();

            if (result.Outcome == BattleOutcome.PlayerVictory)
            {
                deepWins++;
            }
        }

        // Two warriors identical in everything but the pool: without a binding pool this is a coin
        // toss, and the margin is the whole point of the tenth round's re-lock.
        Assert.True(deepWins > 120, $"the deeper pool won {deepWins} of 200");
    }
}
