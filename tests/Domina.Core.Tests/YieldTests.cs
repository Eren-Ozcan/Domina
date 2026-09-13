using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// What an adversary does when his nerve goes (docs/GDD.md §5, 2026-09-12). The decisions protected
/// here: <b>he never leaves the field</b> — the rival's men do not run from the road, which is also
/// what closed the reward leak — and in a <b>match</b>, a bout fought before witnesses, he yields
/// instead: out of the fight, alive, and not struck again. The player's own key is untouched.
/// </summary>
public class YieldTests
{
    /// <summary>
    /// A bed where the enemy's nerve goes and the player's does not: the check is bound to the health
    /// share, and only the enemy is ever hurt enough to be put to it.
    /// </summary>
    private static BattleSetup Bed(bool match) => new(
        [TestBuilders.Warrior(1, health: 400, accuracy: 90, strength: 60)],
        [TestBuilders.Warrior(101, health: 40, aggression: 20)])
    {
        Tuning = TestBuilders.PointBlank with
        {
            BasePanicChance = 1,
            PanicHealthShare = 0.5,
            WillPanicResistance = 0,
        },
        Match = match,
    };

    private static BattleResult Run(BattleSetup setup) => new Battle(setup, new SeededRandom(7)).Run();

    /// <summary>Off a match his nerve buys him nothing: nobody escapes and nobody kneels.</summary>
    [Fact]
    public void AnAdversaryNeverLeavesTheField()
    {
        BattleResult result = Run(Bed(match: false));

        WarriorBattleSummary enemy = result.SummaryFor(new WarriorId(101));

        Assert.NotEqual(CombatState.Escaped, enemy.FinalState);
        Assert.NotEqual(CombatState.Yielded, enemy.FinalState);
        Assert.Equal(BattleOutcome.PlayerVictory, result.Outcome);
    }

    /// <summary>In a match the break has somewhere to go: he yields, and he lives.</summary>
    [Fact]
    public void InAMatchHeYieldsInsteadOfDying()
    {
        BattleResult result = Run(Bed(match: true));

        WarriorBattleSummary enemy = result.SummaryFor(new WarriorId(101));

        Assert.Equal(CombatState.Yielded, enemy.FinalState);
        Assert.False(enemy.Died);
        Assert.Equal(BattleOutcome.PlayerVictory, result.Outcome);
    }

    /// <summary>A man who has yielded is out of the fight — the bout ends with him.</summary>
    [Fact]
    public void TheBoutEndsWhenTheLastManYields()
    {
        BattleSetup setup = Bed(match: true) with { CollectEvents = true };
        Battle battle = new(setup, new SeededRandom(7));
        BattleResult result = battle.Run();

        Assert.Contains(battle.Events, e => e is WarriorYielded);
        Assert.Equal(BattleOutcome.PlayerVictory, result.Outcome);
    }

    /// <summary>
    /// In a match nobody runs: the player's man yields too, and that symmetry is the point.
    /// </summary>
    /// <remarks>
    /// Measured the other way first and it broke the last night: with the rival's men standing and the
    /// player's running, the rule fell on the player alone and hardest exactly where he is outnumbered
    /// — which is every bout of the night after the first.
    /// </remarks>
    [Fact]
    public void InAMatchTheDojosOwnManYieldsToo()
    {
        BattleSetup setup = new(
            [TestBuilders.Warrior(1, health: 30, aggression: 20)],
            [TestBuilders.Warrior(101, health: 400, accuracy: 90, strength: 60)])
        {
            Tuning = TestBuilders.PointBlank with
            {
                BasePanicChance = 1,
                PanicHealthShare = 1,
                WillPanicResistance = 0,
            },
            Match = true,
        };

        WarriorBattleSummary mine = Run(setup).SummaryFor(new WarriorId(1));

        Assert.Equal(CombatState.Yielded, mine.FinalState);
        Assert.False(mine.Died);
        Assert.True(mine.Panicked);
    }

    /// <summary>Off a match the player's man still runs — his key, and his nerve, are his own.</summary>
    [Fact]
    public void OnTheRoadAWarriorOfTheDojoStillRuns()
    {
        BattleSetup setup = new(
            [TestBuilders.Warrior(1, health: 30, aggression: 20)],
            [TestBuilders.Warrior(101, health: 400, accuracy: 90, strength: 60)])
        {
            Tuning = TestBuilders.PointBlank with
            {
                BasePanicChance = 1,
                PanicHealthShare = 1,
                WillPanicResistance = 0,
            },
            Match = false,
        };

        WarriorBattleSummary mine = Run(setup).SummaryFor(new WarriorId(1));

        Assert.NotEqual(CombatState.Yielded, mine.FinalState);
        Assert.True(mine.Panicked);
    }
}
