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
        Assert.Equal(NewGame.StartingWarriors, dojo.Roster.Living.Count());
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

        Assert.NotEqual(roster.Take(NewGame.StartingWarriors), stock.Take(NewGame.StartingWarriors));
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
}
