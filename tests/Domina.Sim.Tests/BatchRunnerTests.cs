using Domina.Core.Combat;
using Domina.Sim;

namespace Domina.Sim.Tests;

/// <summary>
/// Toplu simülasyonun sayımı. Bu sayılar denge kararlarının tek dayanağı — yanlış
/// toplanan bir oran, sessizce yanlış bir denge ayarına yol açar. Bu yüzden
/// toplamlar dövüş başına satırlarla karşılaştırılarak doğrulanır.
/// </summary>
public class BatchRunnerTests
{
    private static Scenario Scenario(string name = "3v3") =>
        Scenarios.Find(name) ?? throw new InvalidOperationException($"Senaryo yok: {name}");

    [Fact]
    public void EveryScenarioIsRunnable()
    {
        foreach (Scenario scenario in Scenarios.All)
        {
            BatchReport report = new BatchRunner(scenario, NeverRetreat.Instance).Run(1, 20);

            Assert.Equal(20, report.Battles);
            Assert.Equal(20, report.Victories + report.Withdrawals + report.Wipes + report.TimeLimits);
        }
    }

    [Fact]
    public void ScenarioLookupIsCaseInsensitiveAndRejectsUnknownNames()
    {
        Assert.NotNull(Scenarios.Find("3V3"));
        Assert.NotNull(Scenarios.Find("AMBUSH"));
        Assert.Null(Scenarios.Find("yok-böyle-bir-şey"));
    }

    [Fact]
    public void TheSameSeedRangeProducesTheSameNumbers()
    {
        // Bu bozulursa "sayıyı değiştirdim, oran değişti" cümlesi anlamını yitirir.
        BatchReport a = new BatchRunner(Scenario(), NeverRetreat.Instance).Run(1, 200);
        BatchReport b = new BatchRunner(Scenario(), NeverRetreat.Instance).Run(1, 200);

        Assert.Equal(a.Victories, b.Victories);
        Assert.Equal(a.PlayerDeaths, b.PlayerDeaths);
        Assert.Equal(a.PlayerLimbLosses, b.PlayerLimbLosses);
        Assert.Equal(a.TotalSeconds, b.TotalSeconds, precision: 9);
    }

    [Fact]
    public void SeedsAdvanceOneByOneFromTheFirst()
    {
        var rows = new List<BattleRow>();
        new BatchRunner(Scenario(), NeverRetreat.Instance).Run(50, 5, rows.Add);

        Assert.Equal([50ul, 51ul, 52ul, 53ul, 54ul], rows.Select(r => r.Seed));
    }

    [Fact]
    public void TotalsAgreeWithThePerBattleRows()
    {
        var rows = new List<BattleRow>();
        BatchReport report = new BatchRunner(Scenario(), new RetreatBelowHealth(0.3)).Run(1, 300, rows.Add);

        Assert.Equal(300, rows.Count);
        Assert.Equal(rows.Sum(r => r.PlayerDeaths), report.PlayerDeaths);
        Assert.Equal(rows.Sum(r => r.PlayerEscapes), report.PlayerEscapes);
        Assert.Equal(rows.Sum(r => r.PlayerLimbLosses), report.PlayerLimbLosses);
        Assert.Equal(rows.Sum(r => r.EnemyDeaths), report.EnemyDeaths);
        Assert.Equal(rows.Count(r => r.Outcome == BattleOutcome.PlayerVictory), report.Victories);
    }

    [Fact]
    public void RatesUseTheRightDenominator()
    {
        // Oranların paydası dövüş sayısı değil, sahaya çıkan savaşçı sayısıdır:
        // 3v3'te bir dövüşte üç savaşçı ölebilir.
        BatchReport report = new BatchRunner(Scenario(), NeverRetreat.Instance).Run(1, 100);

        Assert.Equal(300, report.PlayerAppearances);
        Assert.Equal(300, report.EnemyAppearances);
        Assert.Equal((double)report.PlayerDeaths / 300, report.PlayerDeathRate, precision: 9);
        Assert.Equal((double)report.Victories / 100, report.VictoryRate, precision: 9);
        Assert.InRange(report.PlayerDeathRate, 0, 1);
        Assert.InRange(report.PlayerAccuracy, 0, 1);
    }

    [Fact]
    public void OutcomeRatesAddUpToOne()
    {
        BatchReport report = new BatchRunner(Scenario(), NeverRetreat.Instance).Run(1, 100);

        Assert.Equal(1.0, report.VictoryRate + report.WithdrawalRate + report.WipeRate + report.TimeLimitRate, precision: 9);
    }

    [Fact]
    public void AnEmptyReportDividesByNothing()
    {
        var report = new BatchReport(playerSideSize: 3, enemySideSize: 3);

        Assert.Equal(0, report.PlayerDeathRate, precision: 9);
        Assert.Equal(0, report.VictoryRate, precision: 9);
        Assert.Equal(0, report.AverageSeconds, precision: 9);
        Assert.Equal(0, report.PlayerAccuracy, precision: 9);
    }

    [Fact]
    public void RunningZeroBattlesIsRejected()
    {
        var runner = new BatchRunner(Scenario(), NeverRetreat.Instance);

        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Run(1, 0));
    }

    /// <summary>
    /// Tuş ölümü uzuv kaybına çevirir (GDD §7): çeken oyuncu daha az ölü, daha çok
    /// sakat getirir. Aracın ölçmesi gereken en önemli fark budur.
    /// </summary>
    /// <remarks>
    /// Uzuv kaybı <b>yalnızca</b> müdahale edilen dövüşlerde oluşmaz — öldürmeyen ağır
    /// darbe tuşsuz da koparır (GDD §7). Bu yüzden test toplam sakat sayısını değil
    /// <b>ölüm farkını</b> bağlar: ölçümde çeken ve çekmeyen oyuncunun sakat sayısı
    /// neredeyse aynı çıkıyor (%28.20'ye karşı %28.13), ölüm ise %39'dan %31'e düşüyor.
    /// </remarks>
    [Fact]
    public void InterventionTradesDeathsForLostLimbs()
    {
        BatchReport reckless = new BatchRunner(Scenario(), NeverRetreat.Instance).Run(1, 500);
        BatchReport careful = new BatchRunner(Scenario(), new RetreatBelowHealth(0.3)).Run(1, 500);

        Assert.Equal(0, reckless.PlayerEscapes);
        Assert.True(careful.PlayerEscapes > 0);

        Assert.True(careful.PlayerDeaths < reckless.PlayerDeaths);
        Assert.True(careful.PlayerLimbLosses > 0);
    }

    /// <summary>
    /// Kaçışın bedeli bir merdivendir (GDD §5) ve basma anı seni tek yönlü aşağı
    /// kaydırır: ne kadar geç basarsan o kadar çok ölü, o kadar az sağ çıkan.
    /// </summary>
    /// <remarks>
    /// Tuş bir <b>takas değil</b>: "ölümü uzuv kaybına çevirir" ağacın yalnızca bir dalı.
    /// Bu test merdivenin sırasını bağlar — sıra bozulursa §5'in vaadi bozulmuş demektir.
    /// Mutlak sayılar değil <b>sıralama</b> bağlanır; sayılar Faz 9'un işi.
    /// </remarks>
    [Fact]
    public void PressingLaterCostsMore()
    {
        BatchReport beforeContact = new BatchRunner(Scenario(), new RetreatAtSecond(0)).Run(1, 400);
        BatchReport afterContact = new BatchRunner(Scenario(), new RetreatAtSecond(2)).Run(1, 400);
        BatchReport whenLosing = new BatchRunner(Scenario(), new RetreatWhenLosing(0.7)).Run(1, 400);

        // Temastan önce basmak temiz: mesafe koruyor, kimse sakat kalmıyor.
        Assert.Equal(0, beforeContact.PlayerLimbLosses);

        // Temastan sonra basmak sakatlık getirir; ilk basamakla ikincinin farkı bu.
        Assert.True(afterContact.PlayerLimbLosses > 0);

        // Merdiven aşağı indikçe ölü artar, sağ çıkan azalır. Ölçümde ilk iki basamak
        // ölüm bakımından eşit (ikisi de sıfır) — bu yüzden sıkı sıralama yalnızca
        // son basamakta aranır.
        Assert.True(beforeContact.PlayerDeaths <= afterContact.PlayerDeaths);
        Assert.True(afterContact.PlayerDeaths < whenLosing.PlayerDeaths);

        Assert.True(beforeContact.PlayerEscapes >= afterContact.PlayerEscapes);
        Assert.True(afterContact.PlayerEscapes > whenLosing.PlayerEscapes);
    }
}
