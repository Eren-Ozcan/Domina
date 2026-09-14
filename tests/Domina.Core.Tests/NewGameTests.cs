using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;

namespace Domina.Core.Tests;

/// <summary>
/// A new game's starting state. The decision protected: the start is built in the core and carries the
/// same numbers as the measured setup (GDD §11) — the dojo being played and the dojo whose balance is
/// measured must not drift apart.
/// </summary>
public class NewGameTests
{
    [Fact]
    public void AStartingDojoCarriesTheMeasuredPurseAndRoster()
    {
        DojoState dojo = NewGame.Create(seed: 7);

        Assert.Equal(NewGame.StartingGold, dojo.Resources.Gold);
        Assert.InRange(dojo.Roster.Living.Count(), NewGame.FewestWarriors, NewGame.MostWarriors);
        Assert.Equal(1, dojo.Day);
        Assert.All(dojo.Roster.Living, entry => Assert.Equal(0, entry.RecoveryDaysRemaining));
    }

    [Fact]
    public void TheSameSeedGivesTheSameDojo()
    {
        DojoState first = NewGame.Create(seed: 99);
        DojoState second = NewGame.Create(seed: 99);

        Assert.Equal(
            first.Roster.Living.Select(e => e.Warrior.Name),
            second.Roster.Living.Select(e => e.Warrior.Name));
        Assert.Equal(
            first.Roster.Living.Select(e => e.Warrior.BaseStats),
            second.Roster.Living.Select(e => e.Warrior.BaseStats));
    }

    [Fact]
    public void DifferentSeedsGiveDifferentDojos()
    {
        DojoState first = NewGame.Create(seed: 1);
        DojoState second = NewGame.Create(seed: 2);

        Assert.NotEqual(
            first.Roster.Living.Select(e => e.Warrior.BaseStats),
            second.Roster.Living.Select(e => e.Warrior.BaseStats));
    }

    /// <summary>
    /// The starting roster must not be a copy of the first day's stall — that is what the separate stream is for.
    /// </summary>
    [Fact]
    public void AStartingRosterIsNotTheFirstDayMarket()
    {
        DojoState dojo = NewGame.Create(seed: 4);

        IEnumerable<string> roster = dojo.Roster.Living.Select(e => e.Warrior.Name);
        IEnumerable<string> stock = dojo.Recruits.Select(o => o.Name);

        Assert.NotEqual(roster.Take(NewGame.FewestWarriors), stock.Take(NewGame.FewestWarriors));
    }

    /// <summary>
    /// How many men the master left is drawn, not fixed — no two seasons open the same way (STORY.md).
    /// </summary>
    /// <remarks>
    /// The size is drawn <b>before</b> the men are, so the same seed still opens the same dojo; what the
    /// draw takes away is the fixed cushion every run used to start with.
    /// </remarks>
    [Fact]
    public void TheMasterLeavesADifferentNumberOfMenFromSeasonToSeason()
    {
        HashSet<int> sizes = [];
        for (ulong seed = 1; seed <= 40; seed++)
        {
            int men = NewGame.Create(seed).Roster.Living.Count();
            Assert.InRange(men, NewGame.FewestWarriors, NewGame.MostWarriors);
            sizes.Add(men);
        }

        Assert.True(sizes.Count > 1, "every seed left the same number of men behind");
        Assert.Equal(
            NewGame.Create(seed: 5).Roster.Living.Count(),
            NewGame.Create(seed: 5).Roster.Living.Count());
    }

    /// <summary>A new game must be writable and readable back: the first day is saved too.</summary>
    [Fact]
    public void ANewGameSurvivesARoundTrip()
    {
        DojoState before = NewGame.Create(seed: 31);
        LoadResult result = DojoSaveFile.Load(DojoSaveFile.Write(before));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Warnings);

        DojoState after = result.State!;
        Assert.Equal(before.Seed, after.Seed);
        Assert.Equal(before.Resources, after.Resources);
        Assert.Equal(
            before.Roster.Living.Select(e => e.Warrior.Name),
            after.Roster.Living.Select(e => e.Warrior.Name));

        // Because the seed is saved, the same day must bring back the same offer and the same stall.
        Assert.Equal(before.Offer.Sighting, after.Offer.Sighting);
        Assert.Equal(before.Recruits.Select(o => o.Name), after.Recruits.Select(o => o.Name));
    }

    /// <summary>
    /// GDD §11: the store does not open empty. The stock is counted in <b>days</b> against the roster
    /// the master actually left, so a dojo of three and a dojo of five open with the same time.
    /// </summary>
    [Fact]
    public void TheStoreOpensWithThreeDaysOfEating()
    {
        for (ulong seed = 1; seed <= 12; seed++)
        {
            DojoState dojo = NewGame.Create(seed);
            int mouths = dojo.Roster.Living.Count();

            Assert.Equal(NewGame.StartingStoreDays * mouths, dojo.Resources.Food);
            Assert.Equal(NewGame.StartingStoreDays * mouths, dojo.Resources.Water);
            Assert.Equal(NewGame.StartingGold, dojo.Resources.Gold);
        }
    }
}
