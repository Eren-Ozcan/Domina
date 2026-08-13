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
    /// Uzuv kaybı yalnızca <b>zamanında müdahale edilen</b> dövüşlerde oluşur
    /// (GDD §7). Hiç çekilmeyen bir oyuncu sakat savaşçı üretmez, ölü üretir —
    /// aracın ölçmesi gereken en önemli fark budur.
    /// </summary>
    [Fact]
    public void InterventionTradesDeathsForLostLimbs()
    {
        BatchReport reckless = new BatchRunner(Scenario(), NeverRetreat.Instance).Run(1, 500);
        BatchReport careful = new BatchRunner(Scenario(), new RetreatBelowHealth(0.3)).Run(1, 500);

        Assert.Equal(0, reckless.PlayerLimbLosses);
        Assert.Equal(0, reckless.PlayerEscapes);
        Assert.True(careful.PlayerEscapes > 0);
        Assert.True(careful.PlayerLimbLosses > 0);
    }
}
