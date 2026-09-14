using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;

namespace Domina.Presentation.Tests;

/// <summary>
/// The roster screen's model. Three decisions are protected: the ordering follows the question "whom can
/// I send today", a dead warrior does not drop off the list, and a name clash is visible <b>before</b>
/// anything throws.
/// </summary>
public class RosterModelTests
{
    [Fact]
    public void ReadyWarriorsComeBeforeWoundedAndDead()
    {
        DojoState dojo = new();
        RosterEntry ready = dojo.Roster.Recruit("Zenji");
        RosterEntry wounded = dojo.Roster.Recruit("Aiko");
        RosterEntry dead = dojo.Roster.Recruit("Botan");

        wounded.Injure(3);
        dojo.Roster.Kill(dead.Id);

        IReadOnlyList<RosterRow> rows = RosterModel.Describe(dojo);

        Assert.Equal([ready.Id, wounded.Id, dead.Id], rows.Select(r => r.Id));
        Assert.Equal(RosterStatus.Ready, rows[0].Status);
        Assert.Equal(RosterStatus.Recovering, rows[1].Status);
        Assert.Equal(RosterStatus.Fallen, rows[2].Status);
    }

    [Fact]
    public void EqualStatusFallsBackToName()
    {
        DojoState dojo = new();
        dojo.Roster.Recruit("Zenji");
        dojo.Roster.Recruit("Aiko");

        IReadOnlyList<RosterRow> rows = RosterModel.Describe(dojo);

        Assert.Equal(["Aiko", "Zenji"], rows.Select(r => r.Name));
    }

    [Fact]
    public void ATrainingWarriorIsStillFitForCampaign()
    {
        DojoState dojo = new();
        RosterEntry entry = dojo.Roster.Recruit("Kenji");
        entry.Train(Drill.Guard);

        RosterRow row = RosterModel.Describe(dojo).Single();

        Assert.Equal(RosterStatus.Training, row.Status);
        Assert.Equal(Drill.Guard, row.Drill);
        Assert.True(row.IsFitForCampaign);
    }

    [Fact]
    public void TheRowSeparatesRawStatsFromWhatCombatReads()
    {
        DojoState dojo = new();
        RosterEntry entry = dojo.Roster.Recruit("Kenji");
        entry.Warrior.Path = WarriorPath.Blade;

        RosterRow row = RosterModel.Describe(dojo).Single();

        Assert.Equal(entry.Warrior.BaseStats, row.BaseStats);
        Assert.True(row.EffectiveStats.Accuracy > row.BaseStats.Accuracy);
    }

    [Fact]
    public void ALostLimbShowsOnTheRow()
    {
        DojoState dojo = new();
        RosterEntry entry = dojo.Roster.Recruit("Kenji");
        entry.Warrior.AddDisability(BodyPart.SwordArm);

        RosterRow row = RosterModel.Describe(dojo).Single();

        Assert.Equal(BodyPartSet.SwordArm, row.Lost);
    }

    [Fact]
    public void PathUnlockCountsDownWithTrainingDays()
    {
        DojoTuning tuning = new() { Training = new TrainingTuning { PathTrainingDays = 3 } };
        DojoState dojo = new(tuning) { Purse = new Resources(Gold: 500) };
        RosterEntry entry = dojo.Roster.Recruit("Kenji");

        RosterRow fresh = RosterModel.Describe(entry, dojo.Tuning);
        Assert.False(fresh.PathUnlocked);
        Assert.Equal(3, fresh.TrainingDaysToPath);

        for (int i = 0; i < 3; i++)
        {
            entry.Train();
            dojo.AdvanceDay();
        }

        RosterRow trained = RosterModel.Describe(entry, dojo.Tuning);
        Assert.True(trained.PathUnlocked);
        Assert.Equal(0, trained.TrainingDaysToPath);
    }

    [Fact]
    public void SummaryCountsTheLivingSeparatelyFromTheFit()
    {
        DojoState dojo = new();
        dojo.Roster.Recruit("Kenji");
        RosterEntry wounded = dojo.Roster.Recruit("Aiko");
        RosterEntry dead = dojo.Roster.Recruit("Botan");

        wounded.Injure(2);
        dojo.Roster.Kill(dead.Id);

        RosterSummary summary = RosterModel.Summarize(dojo);

        Assert.Equal(2, summary.Living);
        Assert.Equal(1, summary.Fit);
        Assert.Equal(1, summary.Recovering);
        Assert.Equal(1, summary.Fallen);
        Assert.Equal(4, summary.PartyCapacity);
    }

    [Fact]
    public void RenameIsJudgedBeforeItThrows()
    {
        DojoState dojo = new();
        RosterEntry kenji = dojo.Roster.Recruit("Kenji");
        dojo.Roster.Recruit("Aiko");

        Assert.Equal(RenameVerdict.Ok, RosterModel.JudgeRename(dojo.Roster, kenji.Id, "Hana"));
        Assert.Equal(RenameVerdict.Taken, RosterModel.JudgeRename(dojo.Roster, kenji.Id, "aiko"));
        Assert.Equal(RenameVerdict.Empty, RosterModel.JudgeRename(dojo.Roster, kenji.Id, "  "));
        Assert.Equal(RenameVerdict.Unchanged, RosterModel.JudgeRename(dojo.Roster, kenji.Id, "kenji"));
    }

    [Fact]
    public void ADeadWarriorsNameIsFreeAgain()
    {
        DojoState dojo = new();
        RosterEntry kenji = dojo.Roster.Recruit("Kenji");
        RosterEntry botan = dojo.Roster.Recruit("Botan");
        dojo.Roster.Kill(botan.Id);

        Assert.Equal(RenameVerdict.Ok, RosterModel.JudgeRename(dojo.Roster, kenji.Id, "Botan"));
    }

    /// <summary>The row carries what he knows of the weapon in his hand.</summary>
    [Fact]
    public void TheRowCarriesTheMasteryOfTheWeaponInHand()
    {
        DojoState dojo = new();
        RosterEntry entry = dojo.Roster.Recruit("Kenji");
        entry.Warrior.GainMastery(0.4);

        RosterRow row = RosterModel.Describe(dojo).Single(r => r.Id == entry.Id);

        Assert.Equal(0.4, row.WeaponSkill, 6);
    }

    /// <summary>And the charms he wears, against the slots the dojo has opened.</summary>
    [Fact]
    public void TheRowCarriesTheCharmsAndTheSlots()
    {
        DojoState dojo = new(school: new SchoolTuning { BuildDaysFactor = 0 })
        {
            Purse = new Resources(Gold: 2000),
        };

        RosterEntry entry = dojo.Roster.Recruit("Kenji");
        dojo.BuySchoolNode(SchoolNodeId.Shrine);
        dojo.BuyCharm(OmamoriKind.IronGate);
        dojo.FitCharm(entry.Id, OmamoriKind.IronGate);

        RosterRow row = RosterModel.Describe(dojo).Single(r => r.Id == entry.Id);

        Assert.Equal(dojo.OmamoriSlots, row.CharmSlots);
        Assert.Equal([OmamoriKind.IronGate], row.Charms!);
    }

    /// <summary>The summary carries the roster's condition and what a feast would cost today.</summary>
    [Fact]
    public void TheSummaryCarriesTheSpiritsAndTheFeast()
    {
        DojoState dojo = new() { Purse = new Resources(Gold: 100, Sake: 2) };
        RosterEntry first = dojo.Roster.Recruit("Kenji");
        RosterEntry second = dojo.Roster.Recruit("Goro");
        first.Warrior.Morale = 30;
        second.Warrior.Morale = 70;

        RosterSummary summary = RosterModel.Summarize(dojo);

        Assert.Equal(50, summary.Morale, 6);
        Assert.Equal(2, summary.FeastSake);
        Assert.True(summary.CanFeast);
        Assert.Equal(0, summary.DaysToFeast);
    }

    /// <summary>A feast just held reads as the cooldown, not as missing sake.</summary>
    [Fact]
    public void AFeastJustHeldReadsAsTheCooldown()
    {
        DojoState dojo = new() { Purse = new Resources(Gold: 100, Sake: 10) };
        dojo.Roster.Recruit("Kenji");

        Assert.True(dojo.Feast());

        RosterSummary summary = RosterModel.Summarize(dojo);

        Assert.False(summary.CanFeast);
        Assert.True(summary.DaysToFeast > 0);
    }

    /// <summary>The band is a word, and the middle of the scale is steady.</summary>
    [Fact]
    public void TheMiddleOfTheScaleIsSteady()
    {
        Assert.Equal(MoraleBandName.Steady, RosterModel.Band(MoraleScale.Starting));
        Assert.Equal(MoraleBandName.Broken, RosterModel.Band(0));
        Assert.Equal(MoraleBandName.High, RosterModel.Band(100));
    }
}
