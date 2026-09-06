using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;

namespace Domina.Core.Tests;

/// <summary>
/// Yeni oyunun başlangıç durumu. Korunan karar: başlangıç çekirdekte kurulur ve
/// ölçülen kurulumla aynı sayıları taşır (GDD §11) — oynanan dojo ile dengesi ölçülen
/// dojo ayrışmamalı.
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
    /// Başlangıç kadrosu, ilk günün tezgâhının kopyası olmamalı — ayrı akış bunun için.
    /// </summary>
    [Fact]
    public void AStartingRosterIsNotTheFirstDayMarket()
    {
        DojoState dojo = NewGame.Create(seed: 4);

        IEnumerable<string> roster = dojo.Roster.Living.Select(e => e.Warrior.Name);
        IEnumerable<string> stock = dojo.Recruits.Select(o => o.Name);

        Assert.NotEqual(roster.Take(NewGame.StartingWarriors), stock.Take(NewGame.StartingWarriors));
    }

    /// <summary>Yeni oyun yazılıp geri okunabilmeli: ilk gün de kaydedilir.</summary>
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

        // Tohum kaydedildiği için aynı gün aynı teklifi ve aynı tezgâhı geri getirmeli.
        Assert.Equal(before.Offer.Sighting, after.Offer.Sighting);
        Assert.Equal(before.Recruits.Select(o => o.Name), after.Recruits.Select(o => o.Name));
    }
}
