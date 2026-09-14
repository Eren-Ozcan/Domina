using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The three parties and their five tiers (docs/GDD.md §10). The decisions protected here: each tier
/// buys something on its own axis, a broken promise eats from two places at once, a gift is worth less
/// every time, a week with nothing filed costs, and the monk keeps the temple's regard up without any
/// work being taken from it.
/// </summary>
public class StandingTests
{
    private static DojoState Dojo() => new(
        events: new EventTuning { ChancePerDay = 0 },
        school: new SchoolTuning { BuildDaysFactor = 0 })
    {
        Purse = new Resources(Gold: 4000, Food: 200, Water: 200),
    };

    /// <summary>Everyone starts in the middle, and the middle buys nothing.</summary>
    [Fact]
    public void EveryPartyStartsNeutralAndNeutralIsWorthNothing()
    {
        DojoState dojo = Dojo();

        Assert.All(
            Enum.GetValues<Patron>(),
            patron => Assert.Equal(StandingTier.Neutral, dojo.Standing.TierOf(patron)));

        Assert.Equal(1, dojo.Standing.ClerkReward, 6);
        Assert.Equal(1, dojo.Standing.GuildPrice, 6);
        Assert.Equal(1, dojo.Standing.TempleCharmPrice, 6);
    }

    /// <summary>The guild's regard is read on the prices the dojo pays.</summary>
    [Fact]
    public void TheGuildsRegardIsReadOnThePrices()
    {
        DojoState dojo = Dojo();
        int before = dojo.Economy.RecruitPrice;

        for (int filed = 0; filed < 6; filed++)
        {
            dojo.Standing.Filed(Patron.Guild, dojo.Day);
        }

        // The prices are recomputed where the school is applied, so a purchase can never be priced
        // with one and not the other.
        Assert.True(dojo.SendGift(Patron.Guild));
        Assert.Equal(StandingTier.Loyal, dojo.Standing.TierOf(Patron.Guild));
        Assert.True(dojo.Economy.RecruitPrice < before);
    }

    /// <summary>A gift is worth less every time it is given.</summary>
    [Fact]
    public void EachGiftIsWorthLessThanTheLast()
    {
        Standing standing = new();

        double first = standing.Gift(Patron.Temple);
        double second = standing.Gift(Patron.Temple);
        double third = standing.Gift(Patron.Temple);

        Assert.True(second < first);
        Assert.True(third < second);
    }

    /// <summary>A week with nothing filed for a party costs it.</summary>
    [Fact]
    public void AWeekWithNothingFiledCosts()
    {
        Standing standing = new();
        double before = standing.Of(Patron.Clerk);

        standing.CloseWeek(day: 7);

        Assert.True(standing.Of(Patron.Clerk) < before);

        // A party the dojo has just worked for is not neglected.
        standing.Filed(Patron.Guild, day: 7);
        double guild = standing.Of(Patron.Guild);
        standing.CloseWeek(day: 8);

        Assert.Equal(guild, standing.Of(Patron.Guild), 6);
    }

    /// <summary>A promise broken costs the honour and the standing of the party it was given to.</summary>
    [Fact]
    public void ABrokenPromiseEatsFromTwoPlaces()
    {
        DojoState dojo = Dojo();
        RosterEntry entry = dojo.Roster.Recruit("Kenji");

        BountyContract contract = dojo.Bounty!;
        Assert.NotNull(dojo.AcceptBounty());

        double honour = entry.Warrior.Honor;
        double standing = dojo.Standing.Of(contract.Party);

        while (dojo.Day <= contract.Deadline)
        {
            dojo.AdvanceDay();
        }

        Assert.True(entry.Warrior.Honor < honour);
        Assert.True(dojo.Standing.Of(contract.Party) < standing);
    }

    /// <summary>The monk keeps the temple's regard up without any work being taken from it.</summary>
    [Fact]
    public void TheMonkKeepsTheTemplesRegard()
    {
        double RegardAfterAWeek(bool monk)
        {
            DojoState dojo = Dojo();
            dojo.Roster.Recruit("Kenji");

            if (monk)
            {
                dojo.BuySchoolNode(SchoolNodeId.Shrine);
                dojo.Hire(StaffRole.Monk);
            }

            for (int day = 0; day < dojo.Season.Tuning.CompulsoryFightDays; day++)
            {
                dojo.AdvanceDay();
            }

            return dojo.Standing.Of(Patron.Temple);
        }

        Assert.True(RegardAfterAWeek(monk: true) > RegardAfterAWeek(monk: false));
    }

    /// <summary>The three numbers and the gifts already given survive a save.</summary>
    [Fact]
    public void TheStandingsSurviveTheSave()
    {
        DojoState dojo = Dojo();
        dojo.Standing.Filed(Patron.Guild, dojo.Day);
        dojo.SendGift(Patron.Temple);

        LoadResult loaded = DojoSaveFile.Load(DojoSaveFile.Write(dojo));

        Assert.True(loaded.Succeeded);

        Standing after = loaded.State!.Standing;

        Assert.Equal(dojo.Standing.Of(Patron.Guild), after.Of(Patron.Guild), 6);
        Assert.Equal(dojo.Standing.Of(Patron.Temple), after.Of(Patron.Temple), 6);
        Assert.Equal(1, after.GiftsTo(Patron.Temple));
    }
}
