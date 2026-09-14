using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// The season's screens' model. The decisions protected: the banner never threatens a dojo that has
/// nobody to send, the night's screen reads the same refusal the night itself does, and the closing
/// screen counts the dead and the freed apart.
/// </summary>
public class SeasonModelTests
{
    private static DojoState Quiet(SeasonTuning? season = null, int men = 2)
    {
        DojoState state = new(events: new EventTuning { ChancePerDay = 0 }, season: season)
        {
            Purse = new Resources(Gold: 4000, Food: 600, Water: 600, Medicine: 40),
        };

        for (int i = 0; i < men; i++)
        {
            state.Roster.Recruit($"Kenji {i}", WarriorStats.Recruit());
        }

        return state;
    }

    private static DojoState AtTheNight(SeasonTuning tuning, int men = 3)
    {
        DojoState state = Quiet(tuning, men);
        for (int i = 0; i < tuning.BountyGate; i++)
        {
            state.Season.RecordHead();
        }

        while (state.Day <= tuning.Days)
        {
            state.AdvanceDay();
        }

        return state;
    }

    [Fact]
    public void TheBannerCarriesTheCountdownTheTickAndTheGate()
    {
        DojoState dojo = Quiet();

        SeasonBanner banner = SeasonModel.Describe(dojo);

        Assert.Equal(1, banner.Day);
        Assert.Equal(180, banner.Days);
        Assert.Equal(180, banner.DaysLeft);
        Assert.Equal(6, banner.DaysToTick);
        Assert.Equal(3, banner.Gate);
        Assert.False(banner.GateOpen);
        Assert.Equal(SeasonPhase.Running, banner.Phase);
    }

    /// <summary>A dojo with nobody standing is not warned about a week it cannot be charged for.</summary>
    /// <remarks>
    /// The rule and the screen have to agree: the penalty skips a roster that is in the infirmary, so a
    /// banner that still said "no fight filed" would be threatening the player with a price he is not
    /// going to pay.
    /// </remarks>
    [Fact]
    public void TheBannerOnlyWarnsADojoThatCouldTakeTheField()
    {
        DojoState dojo = Quiet();
        Assert.True(SeasonModel.Describe(dojo).AtRisk);

        foreach (RosterEntry entry in dojo.Roster.Living)
        {
            entry.Injure(20);
        }

        SeasonBanner banner = SeasonModel.Describe(dojo);
        Assert.False(banner.AtRisk);
        Assert.Contains("nobody fit to send", SeasonModel.Line(banner), StringComparison.Ordinal);
    }

    [Fact]
    public void AFiledFightShowsOnTheBanner()
    {
        DojoState dojo = Quiet();
        dojo.Season.RecordFight(dojo.Day, victory: true);

        SeasonBanner banner = SeasonModel.Describe(dojo);

        Assert.True(banner.FiledThisWeek);
        Assert.Contains("filed", SeasonModel.Line(banner), StringComparison.Ordinal);
    }

    /// <summary>There is no night card while the season is still being played.</summary>
    [Fact]
    public void TheNightIsNotDescribedBeforeItOpens()
    {
        Assert.Null(SeasonModel.DescribeNight(Quiet()));
    }

    /// <summary>The card says which bout it is and what is standing there — never the stats.</summary>
    [Fact]
    public void TheNightCardCountsTheBoutsAndNamesWhatStandsThere()
    {
        SeasonTuning tuning = new() { Days = 3 };
        DojoState dojo = AtTheNight(tuning);

        FinalNightCard card = SeasonModel.DescribeNight(dojo)!.Value;

        Assert.Equal(1, card.Round);
        Assert.Equal(tuning.FinalRounds, card.Rounds);
        Assert.False(card.Last);
        Assert.Contains(Adversaries.SeniorStudent.Name, card.Sighting, StringComparison.Ordinal);
        Assert.DoesNotContain("health", card.Sighting, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A hurt man is offered with what is left of him; a broken one is listed and refused.</summary>
    [Fact]
    public void TheCandidatesCarryTheWoundTheNightWillNotHeal()
    {
        SeasonTuning tuning = new() { Days = 3 };
        DojoState dojo = AtTheNight(tuning, men: 3);
        List<RosterEntry> roster = [.. dojo.Roster.Living];

        roster[0].Injure(4);
        roster[1].Injure(tuning.NightMaxWoundDays + 1);

        IReadOnlyList<NightCandidate> candidates = SeasonModel.Candidates(dojo);

        NightCandidate hurt = candidates.Single(c => c.Id == roster[0].Id);
        NightCandidate broken = candidates.Single(c => c.Id == roster[1].Id);
        NightCandidate whole = candidates.Single(c => c.Id == roster[2].Id);

        Assert.True(hurt.CanStand);
        Assert.Equal(1 - (4 * tuning.NightWoundHealthPerDay), hurt.HealthShare, 9);
        Assert.False(broken.CanStand);
        Assert.Equal(1, whole.HealthShare);

        // Those who can stand are listed first — the screen must not open on the men it cannot send.
        Assert.True(candidates[0].CanStand);
        Assert.False(candidates[^1].CanStand);
    }

    /// <summary>The screen's verdict is the night's own refusal, not a second set of rules.</summary>
    [Fact]
    public void TheVerdictComesFromTheNightItself()
    {
        SeasonTuning tuning = new() { Days = 3 };
        DojoState dojo = AtTheNight(tuning, men: 2);
        List<RosterEntry> roster = [.. dojo.Roster.Living];
        roster[1].Injure(tuning.NightMaxWoundDays + 1);

        Assert.True(SeasonModel.Judge(dojo, [roster[0].Id]).CanSend);
        Assert.Equal(
            FinalRefusal.Unfit,
            SeasonModel.Judge(dojo, [roster[1].Id]).Refusal);
        Assert.Equal(FinalRefusal.EmptyParty, SeasonModel.Judge(dojo, []).Refusal);
    }

    /// <summary>The closing screen counts the dead and the freed on opposite sides.</summary>
    [Fact]
    public void TheClosingScreenSeparatesTheDeadFromTheFreed()
    {
        DojoState dojo = Quiet(new SeasonTuning { Days = 4 }, men: 3);
        List<RosterEntry> roster = [.. dojo.Roster.Living];
        dojo.Roster.Kill(roster[0].Id);
        dojo.Release(roster[1].Id);

        SeasonEndCard card = SeasonModel.Close(dojo);

        Assert.Equal([roster[0].Name], card.Dead);
        Assert.Contains(roster[1].Name, card.Freed);
        Assert.Contains(roster[2].Name, card.Freed);
    }

    /// <summary>The headline says which ending this is — a shut gate is not the same as an empty dojo.</summary>
    [Fact]
    public void TheHeadlineNamesTheEnding()
    {
        SeasonTuning tuning = new() { Days = 2 };

        DojoState shut = Quiet(tuning);
        while (shut.Day <= tuning.Days)
        {
            shut.AdvanceDay();
        }

        Assert.Equal(SeasonPhase.Closed, SeasonModel.Close(shut).Phase);
        Assert.Contains("heads", SeasonModel.Close(shut).Headline, StringComparison.Ordinal);

        DojoState empty = Quiet(tuning, men: 1);
        empty.Roster.Kill(empty.Roster.Living.Single().Id);
        empty.AdvanceDay();

        Assert.Contains("nobody left", SeasonModel.Close(empty).Headline, StringComparison.Ordinal);
    }
}
