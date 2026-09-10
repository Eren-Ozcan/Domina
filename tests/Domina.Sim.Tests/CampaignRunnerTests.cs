using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Sim;

namespace Domina.Sim.Tests;

/// <summary>
/// The expedition series — the economy measurement's run tool. Two rules are protected: the measurement
/// is <b>deterministic</b> (the same seed gives the same result, or two price settings cannot be
/// compared) and the economy numbers are sweepable from the command line.
/// </summary>
public class CampaignRunnerTests
{
    private static CampaignOptions Options(int campaigns = 3, int days = 20) => new(
        Scenarios.Find("patrol")!,
        days,
        campaigns,
        PartySize: 3,
        RosterTarget: 4,
        StartingGold: 600,
        RepairAtWearShare: 0.5,
        ReserveDays: 10,
        new EconomyTuning(),
        new DojoTuning(),
        CombatTuning.Default,
        NeverRetreat.Instance);

    [Fact]
    public void SameSeedGivesTheSameCampaign()
    {
        CampaignOptions options = Options();

        CampaignReport first = new CampaignRunner(options).Run(firstSeed: 11);
        CampaignReport second = new CampaignRunner(options).Run(firstSeed: 11);

        Assert.Equal(first.AverageBattles, second.AverageBattles);
        Assert.Equal(first.NetGoldPerBattle, second.NetGoldPerBattle);
        Assert.Equal(first.AverageDeaths, second.AverageDeaths);
        Assert.Equal(first.AverageEndingGold, second.AverageEndingGold);
    }

    [Fact]
    public void DaysAreSpentEitherFightingOrWaiting()
    {
        CampaignReport report = new CampaignRunner(Options()).Run(firstSeed: 3);

        Assert.Equal(3, report.Campaigns);
        Assert.True(report.AverageBattles > 0);
        Assert.InRange(report.IdleDayShare, 0, 1);
        Assert.InRange(report.VictoryRate, 0, 1);
    }

    /// <summary>
    /// Training must be measurable: the same dojo, the only difference the rate.
    /// </summary>
    /// <remarks>
    /// The question the measurement has to answer is "does training work" — a run with the rate zeroed is
    /// that question's control group, so both must stay runnable.
    /// </remarks>
    [Fact]
    public void TrainingShowsUpAsStatGrowth()
    {
        // The fight's own lesson is switched off on both sides: since fights grant stats too, a control
        // that only zeroed the drill would still grow and training's share could not be read off it.
        CampaignOptions idle = Options() with
        {
            Dojo = new DojoTuning
            {
                Training = new TrainingTuning { GapClosedPerDay = 0, FightGapClosed = 0 },
            },
        };
        CampaignOptions drilled = Options() with
        {
            Dojo = new DojoTuning
            {
                Training = new TrainingTuning { GapClosedPerDay = 0.1, FightGapClosed = 0 },
            },
        };

        CampaignReport without = new CampaignRunner(idle).Run(firstSeed: 7);
        CampaignReport with = new CampaignRunner(drilled).Run(firstSeed: 7);

        Assert.True(without.AverageTrainingDays > 0);
        Assert.True(without.AverageScoreGain <= 0);
        Assert.True(with.AverageScoreGain > without.AverageScoreGain);
    }

    /// <summary>A school branch must be runnable on its own — that is the measurement's real question.</summary>
    /// <remarks>
    /// Whether a branch pays for itself can only be seen when it is run alone; with all of them bought,
    /// the money leaving the treasury hides which branch it helped.
    /// </remarks>
    [Fact]
    public void ASingleSchoolBranchCanBeMeasuredOnItsOwn()
    {
        CampaignOptions options = Options(days: 60) with
        {
            StartingGold = 3000,
            UseSchool = true,
            SchoolOnly = SchoolBranch.Steward,
        };

        CampaignReport report = new CampaignRunner(options).Run(firstSeed: 5);

        Assert.True(report.AverageSchoolNodes > 0);
        Assert.True(report.AverageSchoolGold > 0);
    }

    [Fact]
    public void WithoutTheSchoolNothingIsBought()
    {
        CampaignReport report = new CampaignRunner(Options() with { StartingGold = 3000 }).Run(firstSeed: 5);

        Assert.Equal(0, report.AverageSchoolNodes);
        Assert.Equal(0, report.AverageSchoolGold);
        Assert.Equal(0, report.AveragePaths);
    }

    /// <summary>The more victory pays the fuller the treasury — the only axis the measurement holds.</summary>
    [Fact]
    public void ARicherRewardLeavesARicherDojo()
    {
        CampaignOptions lean = Options() with
        {
            Economy = new EconomyTuning { VictoryGoldPerEnemyHealth = 0.2 },
        };
        CampaignOptions fat = lean with
        {
            Economy = new EconomyTuning { VictoryGoldPerEnemyHealth = 1.2 },
        };

        double leanGold = new CampaignRunner(lean).Run(firstSeed: 5).NetGoldPerBattle;
        double fatGold = new CampaignRunner(fat).Run(firstSeed: 5).NetGoldPerBattle;

        Assert.True(fatGold > leanGold);
    }

    [Fact]
    public void EconomyKnobsComeFromTheCommandLine()
    {
        ParsedArgs parsed = SimArgs.Parse(
        [
            "--mode", "campaign",
            "--scenario", "patrol",
            "--days", "30",
            "--campaigns", "7",
            "--party", "2",
            "--roster", "5",
            "--gold", "900",
            "--reward", "0.6",
            "--armor-gold", "2",
            "--repair-gold", "1",
            "--medicine-price", "20",
            "--recruit-price", "90",
        ]);

        Assert.Null(parsed.Error);
        CampaignOptions campaign = parsed.Options!.Campaign!;

        Assert.Equal(30, campaign.Days);
        Assert.Equal(7, campaign.Campaigns);
        Assert.Equal(2, campaign.PartySize);
        Assert.Equal(5, campaign.RosterTarget);
        Assert.Equal(900, campaign.StartingGold);
        Assert.Equal(0.6, campaign.Economy.VictoryGoldPerEnemyHealth);
        Assert.Equal(2, campaign.Economy.ArmorGoldPerDurability);
        Assert.Equal(1, campaign.Economy.RepairGoldPerWear);
        Assert.Equal(20, campaign.Economy.MedicinePrice);
        Assert.Equal(90, campaign.Economy.RecruitPrice);
    }

    /// <summary>With no flag it is battle mode — the economy run does not open by accident.</summary>
    [Fact]
    public void BattleModeStaysTheDefault()
    {
        ParsedArgs parsed = SimArgs.Parse(["--scenario", "duel"]);

        Assert.Null(parsed.Error);
        Assert.Null(parsed.Options!.Campaign);
    }

    /// <summary>
    /// Offer mode: the enemy roster comes from the day's offer, not from the scenario.
    /// </summary>
    [Fact]
    public void OfferModeStillFightsAndStaysDeterministic()
    {
        CampaignOptions offers = Options() with { UseOffers = true };

        CampaignReport first = new CampaignRunner(offers).Run(firstSeed: 21);
        CampaignReport second = new CampaignRunner(offers).Run(firstSeed: 21);

        Assert.True(first.AverageBattles > 0);
        Assert.Equal(first.AverageBattles, second.AverageBattles);
        Assert.Equal(first.DeathPerWarriorBattle, second.DeathPerWarriorBattle);
        Assert.Equal(0, first.DeclinedShare);
    }

    /// <summary>
    /// The measurable counterpart of GDD §10's "take it or leave it" decision: a dojo that declines a
    /// heavy offer protects its roster and spends its day in exchange.
    /// </summary>
    [Fact]
    public void DecliningTradesDaysForWarriors()
    {
        CampaignOptions reckless = Options(campaigns: 40, days: 90) with { UseOffers = true };
        CampaignOptions careful = reckless with { AcceptUpTo = ThreatBand.Rising, CautiousWhenThin = true };

        CampaignReport hot = new CampaignRunner(reckless).Run(firstSeed: 8);
        CampaignReport cool = new CampaignRunner(careful).Run(firstSeed: 8);

        Assert.True(cool.DeclinedShare > 0);
        Assert.True(cool.CollapseRate < hot.CollapseRate);
        Assert.True(cool.IdleDayShare > hot.IdleDayShare);
    }

    [Fact]
    public void OfferKnobsComeFromTheCommandLine()
    {
        ParsedArgs parsed = SimArgs.Parse(
        [
            "--mode", "campaign",
            "--offers", "on",
            "--accept-up-to", "heavy",
            "--cautious", "on",
            "--power-start", "1.4",
            "--power-per-day", "0.05",
            "--power-variance", "0.1",
            "--duel-chance", "0",
        ]);

        Assert.Null(parsed.Error);
        CampaignOptions campaign = parsed.Options!.Campaign!;

        Assert.True(campaign.UseOffers);
        Assert.Equal(ThreatBand.Heavy, campaign.AcceptUpTo);
        Assert.True(campaign.CautiousWhenThin);
        Assert.Equal(1.4, campaign.Encounters!.StartingPower);
        Assert.Equal(0.05, campaign.Encounters.PowerPerDay);
        Assert.Equal(0.1, campaign.Encounters.DailyVariance);
        Assert.Equal(0, campaign.Encounters.DuelChance);
    }
}
