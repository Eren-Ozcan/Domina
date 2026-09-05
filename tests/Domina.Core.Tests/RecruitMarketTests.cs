using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Savaşçı pazarı. Korunan dört karar: adaylar farklı statlarla gelir ve statlar alım
/// öncesi görünür, fiyat statın kendisinden çıkar, pazar kadronun seviyesini takip eder,
/// ve liste gün içinde <b>donar</b> — yoksa oyuncu alım yaparak listeyi yeniden çevirir.
/// </summary>
public class RecruitMarketTests
{
    private static DojoState Funded(int gold = 5000, ulong seed = 12, MarketTuning? market = null)
    {
        DojoState state = new(seed: seed, market: market);
        state.Resources = new Resources(Gold: gold);
        return state;
    }

    [Fact]
    public void CandidatesDifferFromEachOther()
    {
        RecruitMarket market = new();
        IReadOnlyList<RecruitOffer> stock =
            market.Stock(new SeededRandom(7), WarriorStats.Recruit(), basePrice: 150);

        Assert.Equal(3, stock.Count);
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

    /// <summary>Pazar birkaç günde bir yenilenir; her gün yenilenseydi seçim ertelenirdi.</summary>
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

    /// <summary>
    /// Liste gün içinde donmalı: pazar kadronun ortalamasını takip ettiği için, donmasaydı
    /// bir aday almak kalan adayları anında değiştirir ve liste istenildiği kadar
    /// çevrilebilirdi.
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
    /// Fiyat statı takip eder. Tek tek karşılaştırma yetmez — yetenek de fiyata giriyor,
    /// yani statı iyi ama yeteneği düşük bir aday ucuz olabilir. Bakılması gereken eğilim.
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

        Assert.True(dearHalf > cheapHalf, $"fiyat statı takip etmiyor: {cheapHalf:F0} / {dearHalf:F0}");
    }

    /// <summary>Erken oyunda pazarda usta bulunmaz; kadro geliştikçe pazar da gelişir.</summary>
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

        WarriorStats greenAnchor = market.AnchorFor(green.Roster);
        WarriorStats veteranAnchor = market.AnchorFor(veteran.Roster);

        Assert.True(veteranAnchor.MaxHealth > greenAnchor.MaxHealth);
        Assert.True(veteranAnchor.Strength > greenAnchor.Strength);

        // Kadro tamamen ölse bile pazar acemi seviyesine düşer, sıfıra değil.
        DojoState empty = Funded();
        Assert.Equal(WarriorStats.Recruit(), market.AnchorFor(empty.Roster));
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

    /// <summary>Ad çakışması iyi bir adayı satın alınamaz yapmamalı (GDD §6).</summary>
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

    /// <summary>Yetenek kayda girer: oyuncunun satın aldığı şeyin bir parçası.</summary>
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
