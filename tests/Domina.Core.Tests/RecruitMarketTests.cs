using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The warrior market. Four decisions are protected: candidates come with different stats and the stats
/// are visible before the purchase, the price comes out of the stats themselves, the market tracks the
/// roster's level, and the list <b>freezes</b> within the day — or the player rerolls it by buying.
/// </summary>
public class RecruitMarketTests
{
    private static DojoState Funded(int gold = 5000, ulong seed = 12, MarketTuning? market = null)
    {
        DojoState state = new(seed: seed, market: market);
        state.Resources = new Resources(Gold: gold);
        return state;
    }

    private static double Score(WarriorStats stats) =>
        stats.MaxHealth
        + stats.Strength
        + stats.Accuracy
        + stats.Defense
        + stats.Evasion
        + stats.Speed
        + stats.Aggression;

    [Fact]
    public void CandidatesDifferFromEachOther()
    {
        RecruitMarket market = new();
        IReadOnlyList<RecruitOffer> stock =
            market.Stock(new SeededRandom(7), WarriorStats.Recruit(), basePrice: 150);

        Assert.Equal(new MarketTuning().Candidates, stock.Count);
        Assert.True(stock.Select(o => o.Stats.MaxHealth).Distinct().Count() > 1);
        Assert.True(stock.Select(o => o.Price).Distinct().Count() > 1);
        Assert.All(stock, o => Assert.InRange(o.Talent, 0.6, 1.4));
    }

    [Fact]
    public void TheSamePeriodAlwaysShowsTheSameStock()
    {
        DojoState state = Funded();

        List<string> first = [.. state.Recruits.Select(o => $"{o.Name}:{o.Price}")];
        List<string> again = [.. state.Recruits.Select(o => $"{o.Name}:{o.Price}")];

        Assert.Equal(first, again);
    }

    /// <summary>The market refreshes every few days; refreshed every day, the choice would be postponed.</summary>
    [Fact]
    public void StockStandsForItsPeriodThenTurnsOver()
    {
        DojoState state = Funded(market: new MarketTuning { RefreshDays = 2 });
        static string Key(IReadOnlyList<RecruitOffer> stock) =>
            string.Join('|', stock.Select(o => $"{o.Name}:{o.Price}"));

        string day1 = Key(state.Recruits);
        state.AdvanceDay();
        string day2 = Key(state.Recruits);
        state.AdvanceDay();
        string day3 = Key(state.Recruits);

        Assert.Equal(day1, day2);
        Assert.NotEqual(day2, day3);
    }

    /// <summary>With the default setting the stall refreshes every day.</summary>
    [Fact]
    public void TheStallTurnsOverEveryDay()
    {
        DojoState state = Funded();
        static string Key(IReadOnlyList<RecruitOffer> stock) =>
            string.Join('|', stock.Select(o => $"{o.Name}:{o.Price}"));

        string day1 = Key(state.Recruits);
        state.AdvanceDay();

        Assert.Equal(1, new MarketTuning().RefreshDays);
        Assert.NotEqual(day1, Key(state.Recruits));
    }

    /// <summary>
    /// Buying does not eat the day: the market is open all day and, as long as the treasury allows, more
    /// than one warrior can be bought. Otherwise two dead could not be replaced by two warriors the same day.
    /// </summary>
    [Fact]
    public void BuyingDoesNotSpendTheDayAndMoreThanOneCanBeHired()
    {
        DojoState state = Funded();
        int day = state.Day;

        Assert.NotNull(state.HireRecruit(0));
        Assert.NotNull(state.HireRecruit(1));

        Assert.Equal(day, state.Day);
        Assert.Equal(2, state.Roster.Living.Count());
    }

    /// <summary>
    /// Because the stall is frozen within the day, the same candidate could be sold twice: one person
    /// would turn into the whole roster. A bought candidate goes on the record.
    /// </summary>
    [Fact]
    public void TheSameCandidateCannotBeBoughtTwiceInADay()
    {
        DojoState state = Funded();

        Assert.NotNull(state.HireRecruit(0));
        Assert.Null(state.HireRecruit(0));
        Assert.Single(state.Roster.Living);
        Assert.Equal([0], state.HiredToday);
    }

    /// <summary>Tomorrow's stall carries other candidates; yesterday's mark must not block tomorrow.</summary>
    [Fact]
    public void TheMarkOnBoughtCandidatesFallsWithTheDay()
    {
        DojoState state = Funded();

        Assert.NotNull(state.HireRecruit(0));
        state.AdvanceDay();

        Assert.Empty(state.HiredToday);
        Assert.NotNull(state.HireRecruit(0));
    }

    [Fact]
    public void AnIndexOutsideTheStallBuysNobody()
    {
        DojoState state = Funded();

        Assert.Null(state.HireRecruit(-1));
        Assert.Null(state.HireRecruit(state.Recruits.Count));
        Assert.Empty(state.Roster.Entries);
    }

    [Fact]
    public void AnEmptyPurseBuysNobodyAndLeavesNoMark()
    {
        DojoState state = Funded(gold: 0);

        Assert.Null(state.HireRecruit(0));
        Assert.Empty(state.HiredToday);
    }

    /// <summary>
    /// The list must freeze within the day: because the market tracks the roster's average, without
    /// freezing, buying one candidate would instantly change the rest and the list could be rerolled as
    /// often as you liked.
    /// </summary>
    [Fact]
    public void BuyingDoesNotReshuffleTheRestOfTheStock()
    {
        DojoState state = Funded();
        List<string> before = [.. state.Recruits.Select(o => $"{o.Name}:{o.Price}")];

        Assert.NotNull(Quartermaster.Hire(state, state.Recruits[0]));

        List<string> after = [.. state.Recruits.Select(o => $"{o.Name}:{o.Price}")];
        Assert.Equal(before, after);
    }

    /// <summary>
    /// The price tracks the stats. A one-by-one comparison is not enough — talent enters the price too,
    /// so a candidate with good stats but low talent can be cheap. What matters is the tendency.
    /// </summary>
    [Fact]
    public void BetterCandidatesCostMoreOnAverage()
    {
        RecruitMarket market = new();
        WarriorStats anchor = WarriorStats.Recruit();

        List<RecruitOffer> draws =
        [
            .. Enumerable.Range(1, 60)
                .SelectMany(seed => market.Stock(new SeededRandom((ulong)seed), anchor, basePrice: 150)),
        ];

        List<RecruitOffer> ranked = [.. draws.OrderBy(Score)];
        double cheapHalf = ranked.Take(ranked.Count / 2).Average(o => o.Price);
        double dearHalf = ranked.Skip(ranked.Count / 2).Average(o => o.Price);

        Assert.True(dearHalf > cheapHalf, $"the price does not track the stats: {cheapHalf:F0} / {dearHalf:F0}");
    }

    /// <summary>In the early game there is no master in the market; as the roster develops so does the market.</summary>
    [Fact]
    public void TheMarketFollowsTheRoster()
    {
        RecruitMarket market = new();

        DojoState green = Funded();
        green.Roster.Recruit("Acemi", WarriorStats.Recruit());

        DojoState veteran = Funded();
        veteran.Roster.Recruit(
            "Usta",
            WarriorStats.Recruit() with { MaxHealth = 200, Strength = 80, Accuracy = 85 });

        MarketAnchor greenAnchor = market.AnchorFor(green.Roster);
        MarketAnchor veteranAnchor = market.AnchorFor(veteran.Roster);

        Assert.True(veteranAnchor.Stats.MaxHealth > greenAnchor.Stats.MaxHealth);
        Assert.True(veteranAnchor.Stats.Strength > greenAnchor.Stats.Strength);

        // Even if the whole roster dies the market falls to recruit level, not to zero.
        DojoState empty = Funded();
        Assert.Equal(WarriorStats.Recruit(), market.AnchorFor(empty.Roster).Stats);
    }

    /// <summary>
    /// The market tracks the roster's average but <b>cannot pass</b> its best: a trained warrior should
    /// stay the player's work, not something buyable.
    /// </summary>
    [Fact]
    public void NoCandidateOutgrowsTheBestWarriorInTheRoster()
    {
        RecruitMarket market = new();

        DojoState dojo = Funded();
        dojo.Roster.Recruit("Acemi", WarriorStats.Recruit());
        dojo.Roster.Recruit(
            "Usta",
            WarriorStats.Recruit() with { MaxHealth = 220, Strength = 90, Accuracy = 90 });

        MarketAnchor anchor = market.AnchorFor(dojo.Roster);
        double best = Score(
            WarriorStats.Recruit() with { MaxHealth = 220, Strength = 90, Accuracy = 90 });

        IEnumerable<RecruitOffer> offers = Enumerable
            .Range(1, 200)
            .SelectMany(seed => market.Stock(new SeededRandom((ulong)seed), anchor, basePrice: 150));

        foreach (RecruitOffer offer in offers)
        {
            Assert.True(
                Score(offer.Stats) <= best * new MarketTuning().BestFollowCeiling + 1e-6,
                $"the candidate passed the ceiling: {Score(offer.Stats):F1} / {best:F1}");
        }
    }

    /// <summary>The ceiling scales rather than clips — candidates at the ceiling are not copies of each other.</summary>
    [Fact]
    public void TheCeilingScalesTheCandidateInsteadOfFlatteningIt()
    {
        RecruitMarket market = new(new MarketTuning { BestFollowCeiling = 0.2 });

        DojoState dojo = Funded();
        dojo.Roster.Recruit("Usta", WarriorStats.Recruit() with { MaxHealth = 300, Strength = 95 });

        MarketAnchor anchor = market.AnchorFor(dojo.Roster);
        IReadOnlyList<RecruitOffer> stock = market.Stock(new SeededRandom(3), anchor, basePrice: 150);

        // They all hit the ceiling but their profiles are still different.
        Assert.True(stock.Select(o => Math.Round(o.Stats.Strength, 3)).Distinct().Count() > 1);
    }

    [Fact]
    public void HiringFromTheMarketTakesTheAskingPriceAndKeepsTheTalent()
    {
        DojoState state = Funded(gold: 5000);
        RecruitOffer pick = state.Recruits[0];

        RosterEntry? entry = Quartermaster.Hire(state, pick);

        Assert.NotNull(entry);
        Assert.Equal(5000 - pick.Price, state.Resources.Gold);
        Assert.Equal(pick.Stats, entry!.Warrior.BaseStats);
        Assert.Equal(pick.Talent, entry.Warrior.Talent);
    }

    [Fact]
    public void AnEmptyPurseHiresNobody()
    {
        DojoState state = Funded(gold: 0);
        RecruitOffer pick = state.Recruits[0];

        Assert.Null(Quartermaster.Hire(state, pick));
        Assert.Empty(state.Roster.Entries);
    }

    /// <summary>A name clash must not make a good candidate unbuyable (GDD §6).</summary>
    [Fact]
    public void ATakenNameDoesNotBlockThePurchase()
    {
        DojoState state = Funded();
        RecruitOffer pick = state.Recruits[0];
        state.Roster.Recruit(pick.Name);

        RosterEntry? entry = Quartermaster.Hire(state, pick);

        Assert.NotNull(entry);
        Assert.NotEqual(pick.Name, entry!.Name);
        Assert.StartsWith(pick.Name, entry.Name, StringComparison.Ordinal);
    }

    /// <summary>Talent goes into the save: it is part of what the player bought.</summary>
    [Fact]
    public void TalentSurvivesASaveRoundTrip()
    {
        DojoState state = Funded();
        RosterEntry entry = state.Roster.Recruit("Kenji", WarriorStats.Recruit(), talent: 1.35);

        Dojo.Save.LoadResult loaded = Dojo.Save.DojoSaveFile.Load(Dojo.Save.DojoSaveFile.Write(state));

        Assert.True(loaded.Succeeded);
        Assert.Equal(1.35, loaded.State!.Roster.Find(entry.Id)!.Warrior.Talent);
    }

    private static RecruitOffer Only(RecruitMarket market, WarriorStats anchor, ulong seed) =>
        market.Stock(new SeededRandom(seed), anchor, basePrice: 150)[0];

    private static double Score(RecruitOffer offer) =>
        offer.Stats.MaxHealth
        + offer.Stats.Strength
        + offer.Stats.Accuracy
        + offer.Stats.Defense
        + offer.Stats.Evasion
        + offer.Stats.Speed
        + offer.Stats.Aggression;
}
