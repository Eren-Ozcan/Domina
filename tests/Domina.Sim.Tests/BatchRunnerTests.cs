using Domina.Core.Combat;
using Domina.Sim;

namespace Domina.Sim.Tests;

/// <summary>
/// Batch simulation's counting. These numbers are the only basis for balance decisions — a rate summed
/// wrongly leads silently to a wrong balance setting. That is why the totals are verified against the
/// per-fight rows.
/// </summary>
public class BatchRunnerTests
{
    private static Scenario Scenario(string name = "3v3") =>
        Scenarios.Find(name) ?? throw new InvalidOperationException($"No such scenario: {name}");

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
        Assert.Null(Scenarios.Find("no-such-thing"));
    }

    [Fact]
    public void TheSameSeedRangeProducesTheSameNumbers()
    {
        // If this breaks, the sentence "I changed the number and the rate changed" loses its meaning.
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
        // The denominator of the rates is not the number of fights but the number of warriors who took
        // the field: in 3v3 three warriors can die in one fight.
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
    /// The key turns death into limb loss (GDD §7): a player who pulls out brings back fewer dead and
    /// more maimed. This is the most important difference the tool has to measure.
    /// </summary>
    /// <remarks>
    /// Limb loss does <b>not</b> happen only in fights with intervention — a heavy blow that does not kill
    /// severs without the key too (GDD §7). So the test ties down not the total number of maimed but the
    /// <b>difference in deaths</b>: in measurement the number of maimed comes out almost the same for the
    /// player who pulls out and the one who does not, while death falls measurably.
    /// </remarks>
    [Fact]
    public void InterventionTradesDeathsForLostLimbs()
    {
        // The sample is deliberately large: since the charge stopped closing defence (GDD §4) the
        // difference in deaths between pulling out and not narrowed — 40.2% against 38.6% over 10,000
        // fights — and it drowns in noise on a small sample.
        BatchReport reckless = new BatchRunner(Scenario(), NeverRetreat.Instance).Run(1, 3000);
        BatchReport careful = new BatchRunner(Scenario(), new RetreatBelowHealth(0.3)).Run(1, 3000);

        Assert.Equal(0, reckless.PlayerEscapes);
        Assert.True(careful.PlayerEscapes > 0);

        Assert.True(careful.PlayerDeaths < reckless.PlayerDeaths);
        Assert.True(careful.PlayerLimbLosses > 0);
    }

    /// <summary>
    /// The price of escape is a ladder (GDD §5) and the moment you press slides you one way down it: the
    /// later you press, the more dead and the fewer who get out alive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The key is <b>not a trade</b>: "it turns death into limb loss" is only one branch of the tree.
    /// This test ties down the ladder's order — if the order breaks, §5's promise is broken.
    /// Not the absolute numbers but the <b>ordering</b> is tied down; the numbers are phase 9's job.
    /// </para>
    /// <para>
    /// The ladder's top rung is no longer "press before contact": the key is closed until the first hit.
    /// Even the earliest press is not free, and this test ties that down too — a pre-contact press used
    /// to give 0% limb loss, meaning the rung did not exist.
    /// </para>
    /// </remarks>
    [Fact]
    public void PressingLaterCostsMore()
    {
        BatchReport asSoonAsItOpens = new BatchRunner(Scenario(), new RetreatAtSecond(0)).Run(1, 400);
        BatchReport afterAWhile = new BatchRunner(Scenario(), new RetreatAtSecond(2)).Run(1, 400);
        BatchReport whenLosing = new BatchRunner(Scenario(), new RetreatWhenLosing(0.7)).Run(1, 400);

        // Even pressing the moment the key unlocks brings maiming: there is no escape without contact.
        Assert.True(asSoonAsItOpens.PlayerLimbLosses > 0);

        // As the ladder goes down, the dead rise and the survivors fall.
        Assert.True(asSoonAsItOpens.PlayerDeaths <= afterAWhile.PlayerDeaths);
        Assert.True(afterAWhile.PlayerDeaths < whenLosing.PlayerDeaths);

        Assert.True(asSoonAsItOpens.PlayerEscapes >= afterAWhile.PlayerEscapes);
        Assert.True(afterAWhile.PlayerEscapes > whenLosing.PlayerEscapes);
    }
}
