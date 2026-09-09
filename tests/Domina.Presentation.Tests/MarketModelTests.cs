using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// The market screen's model. Three decisions are protected: the order is by price and does not shift
/// as the treasury changes, where the candidate stands relative to the roster is written in the row, and
/// talent is read as a <b>band</b> rather than a number.
/// </summary>
public class MarketModelTests
{
    private static DojoState Funded(int gold, ulong seed = 12, MarketTuning? market = null)
    {
        DojoState state = new(seed: seed, market: market);
        state.Resources = new Resources(Gold: gold);
        return state;
    }

    [Fact]
    public void RowsComeCheapestFirst()
    {
        DojoState dojo = Funded(gold: 5000);

        IReadOnlyList<MarketRow> rows = MarketModel.Describe(dojo);

        Assert.Equal(dojo.Recruits.Count, rows.Count);
        Assert.Equal(rows.Select(r => r.Price).Order(), rows.Select(r => r.Price));
    }

    /// <summary>The stall is not rearranged to fit the player's pocket.</summary>
    [Fact]
    public void SpendingGoldDoesNotReorderTheStall()
    {
        DojoState rich = Funded(gold: 5000);
        DojoState poor = Funded(gold: 0);

        Assert.Equal(
            MarketModel.Describe(rich).Select(r => r.Index),
            MarketModel.Describe(poor).Select(r => r.Index));
    }

    /// <summary>Even if the row order shifts, a purchase uses the stock index.</summary>
    [Fact]
    public void TheIndexPointsBackAtTheStock()
    {
        DojoState dojo = Funded(gold: 5000);

        foreach (MarketRow row in MarketModel.Describe(dojo))
        {
            RecruitOffer offer = dojo.Recruits[row.Index];
            Assert.Equal(offer.Name, row.Name);
            Assert.Equal(offer.Price, row.Price);
        }
    }

    [Fact]
    public void AnUnaffordableCandidateIsMarkedWithWhatIsMissing()
    {
        DojoState dojo = Funded(gold: 0);

        IReadOnlyList<MarketRow> rows = MarketModel.Describe(dojo);

        Assert.All(rows, row => Assert.False(row.Affordable));
        Assert.Equal(0, MarketModel.Summarize(dojo).Affordable);
    }

    /// <summary>The market's real question: is this man better than what I have?</summary>
    [Fact]
    public void ARowCountsHowManyLivingWarriorsAreBetter()
    {
        DojoState dojo = Funded(gold: 5000);
        RecruitOffer offer = dojo.Recruits[0];

        RosterEntry weak = dojo.Roster.Recruit("Weak");
        weak.Warrior.BaseStats = offer.Stats with { Strength = offer.Stats.Strength - 20 };

        RosterEntry strong = dojo.Roster.Recruit("Strong");
        strong.Warrior.BaseStats = offer.Stats with { Strength = offer.Stats.Strength + 20 };

        MarketRow row = MarketModel.Describe(dojo).Single(r => r.Index == 0);

        Assert.Equal(1, row.BetterInRoster);
    }

    /// <summary>A dead warrior does not enter the comparison — he is no longer on the roster.</summary>
    [Fact]
    public void TheDeadDoNotCountInTheComparison()
    {
        DojoState dojo = Funded(gold: 5000);
        RecruitOffer offer = dojo.Recruits[0];

        RosterEntry fallen = dojo.Roster.Recruit("Merhum");
        fallen.Warrior.BaseStats = offer.Stats with { Strength = offer.Stats.Strength + 40 };
        dojo.Roster.Kill(fallen.Id);

        Assert.Equal(0, MarketModel.Describe(dojo).Single(r => r.Index == 0).BetterInRoster);
        Assert.Equal(0, MarketModel.Summarize(dojo).BestLivingScore);
    }

    /// <summary>A bought candidate stays at the stall but is not sold; the counter does not count him either.</summary>
    [Fact]
    public void ABoughtCandidateIsMarkedAndDropsOutOfTheAffordableCount()
    {
        DojoState dojo = Funded(gold: 5000);
        int affordable = MarketModel.Summarize(dojo).Affordable;

        Assert.NotNull(dojo.HireRecruit(0));

        MarketRow row = MarketModel.Describe(dojo).Single(r => r.Index == 0);
        MarketSummary summary = MarketModel.Summarize(dojo);

        Assert.True(row.Bought);
        Assert.Equal(1, summary.Bought);
        Assert.Equal(affordable - 1, summary.Affordable);
    }

    /// <summary>With the default setting the stall refreshes every day.</summary>
    [Fact]
    public void TheDefaultStallStandsForASingleDay()
    {
        Assert.Equal(1, MarketModel.DaysToRefresh(Funded(gold: 0)));
    }

    [Fact]
    public void TalentIsReadAsABandNotANumber()
    {
        Assert.Equal(TalentBand.Dull, Band(0.60));
        Assert.Equal(TalentBand.Fair, Band(1.00));
        Assert.Equal(TalentBand.Promising, Band(1.20));
        Assert.Equal(TalentBand.Rare, Band(1.40));
    }

    /// <summary>
    /// The market standing still must be written on screen; if it is not, the player postpones the
    /// decision thinking "it changes tomorrow" and finds the same stall two days running.
    /// </summary>
    [Fact]
    public void TheScreenKnowsHowManyDaysTheStallStands()
    {
        DojoState dojo = Funded(gold: 5000, market: new MarketTuning { RefreshDays = 3 });

        Assert.Equal(3, MarketModel.DaysToRefresh(dojo));
        dojo.Decline();
        Assert.Equal(2, MarketModel.DaysToRefresh(dojo));
        dojo.Decline();
        Assert.Equal(1, MarketModel.DaysToRefresh(dojo));
        dojo.Decline();
        Assert.Equal(3, MarketModel.DaysToRefresh(dojo));
    }

    /// <summary>The score must be the same formula as the market's ceiling calculation.</summary>
    [Fact]
    public void ScoreLeavesStaminaOutJustLikeTheCeilingDoes()
    {
        WarriorStats stats = WarriorStats.Recruit();

        Assert.Equal(
            MarketModel.Score(stats),
            MarketModel.Score(stats with { MaxStamina = stats.MaxStamina + 50 }));
    }

    private static TalentBand Band(double talent) =>
        MarketModel.Describe(
            new RecruitOffer("Aday", WarriorStats.Recruit(), talent, Price: 100),
            index: 0,
            gold: 0,
            livingScores: []).Band;
}
