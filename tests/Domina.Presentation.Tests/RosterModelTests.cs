using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;

namespace Domina.Presentation.Tests;

/// <summary>
/// Roster ekranının modeli. Korunan üç karar: sıralama "bugün kimi gönderebilirim"
/// sorusuna göre, ölü savaşçı listeden düşmez, ve ad çakışması ekrana <b>fırlatmadan</b>
/// önce görünür.
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
        DojoState dojo = new(tuning) { Resources = new Resources(Gold: 500) };
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
}
