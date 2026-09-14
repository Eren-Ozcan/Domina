using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// What the dojo does when a fight hits the stall guard. In the sim the guard is a counted anomaly and
/// nothing more; in the game the party still has to come home, so the encounter closes as
/// <b>unresolved</b>: no reward, no honour, no morale swing — but the wounds, the wear and the day
/// stand (docs/GDD.md §10, build step 8).
/// </summary>
public class StallGuardDojoTests
{
    /// <summary>Two men who cannot finish each other: only the guard can end this.</summary>
    private static (DojoState Dojo, BattleSetup Setup) Stalemate()
    {
        DojoState dojo = new(seed: 5)
        {
            Purse = new Resources(Gold: 500, Food: 50, Water: 50, Medicine: 5),
        };

        RosterEntry entry = dojo.Roster.Recruit(
            "Kenji",
            WarriorStats.Recruit() with { MaxHealth = 100_000 },
            Weapon.Fists());

        BattleSetup setup = new(
            [entry.Warrior],
            [TestBuilders.Warrior(101, health: 100_000, weapon: Weapon.Fists())])
        {
            Tuning = TestBuilders.PointBlank,
        };

        return (dojo, setup);
    }

    private static (DojoState Dojo, ExpeditionResult Result) Stalled()
    {
        (DojoState dojo, BattleSetup setup) = Stalemate();
        BattleResult battle = new Battle(setup, new SeededRandom(15)).Run();

        Assert.Equal(BattleOutcome.Stalled, battle.Outcome);
        return (dojo, new Expedition().Settle(dojo, setup, battle));
    }

    /// <summary>Nobody held the field, so nobody is paid for it.</summary>
    [Fact]
    public void AStalledFightPaysNothing()
    {
        (DojoState dojo, ExpeditionResult result) = Stalled();

        // The purse only ever moves by the day's own upkeep — the expedition itself added nothing.
        Assert.Equal(0, result.Reward);
        Assert.Equal(500 - result.Day.Upkeep.GoldSpent, dojo.Resources.Gold);
    }

    /// <summary>
    /// The guard is the resolver failing, not the warrior. Honour is what the province read of him, and
    /// the province saw no fight it could read — so the number does not move in either direction.
    /// </summary>
    [Fact]
    public void AStalledFightWritesNoHonor()
    {
        (DojoState dojo, ExpeditionResult result) = Stalled();

        Assert.All(result.Aftermath.Warriors, w => Assert.Equal(0, w.HonorDelta));
        Assert.Equal(HonorScale.Starting, dojo.Roster.Living.First().Warrior.Honor, 3);
    }

    /// <summary>Nobody won and nobody was beaten: the men come home no lower than they went out.</summary>
    [Fact]
    public void AStalledFightSwingsNoMorale()
    {
        (DojoState dojo, BattleSetup setup) = Stalemate();
        double before = dojo.Roster.Living.First().Warrior.Morale;

        BattleResult battle = new Battle(setup, new SeededRandom(15)).Run();
        new Expedition().Settle(dojo, setup, battle);

        Assert.Equal(before, dojo.Roster.Living.First().Warrior.Morale, 3);
    }

    /// <summary>
    /// It is not a free afternoon either: the day is eaten and the week's fight is filed. The party
    /// took the field — what failed there is the resolver's business, not the province's.
    /// </summary>
    [Fact]
    public void AStalledFightStillEatsTheDayAndFilesTheWeek()
    {
        (DojoState dojo, ExpeditionResult result) = Stalled();

        Assert.Equal(1, result.Day.Day);
        Assert.Equal(2, dojo.Day);
        Assert.False(result.Day.MissedWeek);
        Assert.Equal(1, dojo.Season.LastFightDay);
    }
}
