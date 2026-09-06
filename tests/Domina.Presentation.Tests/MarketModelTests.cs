using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// Pazar ekranının modeli. Korunan üç karar: sıra fiyata göredir ve kasa değiştikçe
/// kaymaz, adayın kadroya göre nerede durduğu satırda yazar, ve yetenek sayı olarak
/// değil <b>bant</b> olarak okunur.
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

    /// <summary>Tezgâh oyuncunun cebine göre yeniden dizilmez.</summary>
    [Fact]
    public void SpendingGoldDoesNotReorderTheStall()
    {
        DojoState rich = Funded(gold: 5000);
        DojoState poor = Funded(gold: 0);

        Assert.Equal(
            MarketModel.Describe(rich).Select(r => r.Index),
            MarketModel.Describe(poor).Select(r => r.Index));
    }

    /// <summary>Satır sırası kaysa da satın alma stok sırasını gösterir.</summary>
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

    /// <summary>Pazarın asıl sorusu: bu adam elimdekinden iyi mi?</summary>
    [Fact]
    public void ARowCountsHowManyLivingWarriorsAreBetter()
    {
        DojoState dojo = Funded(gold: 5000);
        RecruitOffer offer = dojo.Recruits[0];

        RosterEntry weak = dojo.Roster.Recruit("Zayıf");
        weak.Warrior.BaseStats = offer.Stats with { Strength = offer.Stats.Strength - 20 };

        RosterEntry strong = dojo.Roster.Recruit("Güçlü");
        strong.Warrior.BaseStats = offer.Stats with { Strength = offer.Stats.Strength + 20 };

        MarketRow row = MarketModel.Describe(dojo).Single(r => r.Index == 0);

        Assert.Equal(1, row.BetterInRoster);
    }

    /// <summary>Ölü savaşçı kıyaslamaya girmez — kadroda olan artık o değil.</summary>
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

    /// <summary>Alınan aday tezgâhta durur ama satılmaz; sayaç da onu saymaz.</summary>
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

    /// <summary>Varsayılan ayarda tezgâh her gün yenilenir.</summary>
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
    /// Pazarın durgunluğu ekranda yazmalı; yazmazsa oyuncu kararı "yarın değişir" diye
    /// erteler ve iki gün aynı tezgâhı bulur.
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

    /// <summary>Skor, pazarın tavan hesabıyla aynı formül olmalı.</summary>
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
