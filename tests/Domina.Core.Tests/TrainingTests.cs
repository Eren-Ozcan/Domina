using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Training (GDD §11). The decisions protected: the gain is a share of the gap left (diminishing
/// returns), the ceiling is not passed, talent multiplies the gain directly, a hungry warrior does not
/// advance, and training writes the <b>raw</b> stat — a disability's multiplier is not undone by training.
/// </summary>
public class TrainingTests
{
    private static readonly TrainingTuning _fast = new() { GapClosedPerDay = 0.10 };

    [Fact]
    public void ADrillRaisesItsPrimaryStatMoreThanItsSecondary()
    {
        WarriorStats before = WarriorStats.Recruit();

        WarriorStats after = TrainingGround.After(before, Drill.Strikes, talent: 1, _fast);

        Assert.True(after.Accuracy > before.Accuracy);
        Assert.True(after.Aggression > before.Aggression);
        Assert.True(after.Accuracy - before.Accuracy > after.Aggression - before.Aggression);
    }

    [Fact]
    public void ADrillLeavesTheStatsItDoesNotTeachAlone()
    {
        WarriorStats before = WarriorStats.Recruit();

        WarriorStats after = TrainingGround.After(before, Drill.Footwork, talent: 1, _fast);

        Assert.Equal(before.Accuracy, after.Accuracy);
        Assert.Equal(before.Defense, after.Defense);
        Assert.Equal(before.MaxHealth, after.MaxHealth);
        Assert.True(after.Evasion > before.Evasion);
        Assert.True(after.Speed > before.Speed);
    }

    /// <summary>The four drills cover the eight stats exactly — none is left outside training.</summary>
    [Fact]
    public void TheFourDrillsCoverEveryStat()
    {
        WarriorStats before = WarriorStats.Recruit();
        WarriorStats after = before;

        foreach (Drill drill in Enum.GetValues<Drill>())
        {
            after = TrainingGround.After(after, drill, talent: 1, _fast);
        }

        Assert.True(after.MaxHealth > before.MaxHealth);
        Assert.True(after.Aggression > before.Aggression);
        Assert.True(after.Defense > before.Defense);
        Assert.True(after.Evasion > before.Evasion);
        Assert.True(after.Strength > before.Strength);
        Assert.True(after.Accuracy > before.Accuracy);
        Assert.True(after.MaxStamina > before.MaxStamina);
        Assert.True(after.Speed > before.Speed);
    }

    [Fact]
    public void GainShrinksAsTheStatApproachesItsCeiling()
    {
        WarriorStats early = WarriorStats.Recruit();
        WarriorStats late = early with { Accuracy = 85 };

        double earlyGain = TrainingGround.After(early, Drill.Strikes, 1, _fast).Accuracy - early.Accuracy;
        double lateGain = TrainingGround.After(late, Drill.Strikes, 1, _fast).Accuracy - late.Accuracy;

        Assert.True(earlyGain > lateGain);
    }

    [Fact]
    public void TrainingNeverPushesAStatPastItsCeiling()
    {
        TrainingTuning tuning = new() { GapClosedPerDay = 1.0, SkillCeiling = 90 };
        WarriorStats stats = WarriorStats.Recruit();

        for (int day = 0; day < 50; day++)
        {
            stats = TrainingGround.After(stats, Drill.Strikes, talent: 1.4, tuning);
        }

        Assert.True(stats.Accuracy <= 90);
        Assert.True(stats.Aggression <= 90);
    }

    [Fact]
    public void TalentScalesTheDaysWorth()
    {
        WarriorStats before = WarriorStats.Recruit();

        double slow = TrainingGround.After(before, Drill.Guard, talent: 0.6, _fast).Defense;
        double average = TrainingGround.After(before, Drill.Guard, talent: 1.0, _fast).Defense;
        double quick = TrainingGround.After(before, Drill.Guard, talent: 1.4, _fast).Defense;

        Assert.True(slow < average);
        Assert.True(average < quick);
    }

    [Fact]
    public void WeakestPicksTheDrillWithTheWidestGap()
    {
        WarriorStats stats = WarriorStats.Recruit() with { Evasion = 5 };

        Assert.Equal(Drill.Footwork, TrainingGround.Weakest(stats));
    }

    [Fact]
    public void ATrainingDayWritesTheWarriorsStats()
    {
        DojoState state = new(new DojoTuning { Training = _fast });
        state.Resources = new Resources(Gold: 500);
        RosterEntry entry = state.Roster.Recruit("Kenji");
        double before = entry.Warrior.BaseStats.Accuracy;

        entry.Train(Drill.Strikes);
        state.AdvanceDay();

        Assert.True(entry.Warrior.BaseStats.Accuracy > before);
    }

    /// <summary>A hungry warrior neither heals nor advances that day — the price of scarcity is time.</summary>
    [Fact]
    public void AHungryWarriorGainsNothing()
    {
        DojoState state = new(new DojoTuning { Training = _fast });
        state.Resources = Resources.Empty;
        RosterEntry entry = state.Roster.Recruit("Kenji");
        WarriorStats before = entry.Warrior.BaseStats;

        entry.Train(Drill.Strikes);
        DayReport report = state.AdvanceDay();

        Assert.False(report.Upkeep.Fed);
        Assert.Equal(before, entry.Warrior.BaseStats);
        Assert.Equal(0, entry.TrainingDays);
    }

    [Fact]
    public void AWoundedWarriorCannotTrain()
    {
        DojoState state = new(new DojoTuning { Training = _fast });
        state.Resources = new Resources(Gold: 500);
        RosterEntry entry = state.Roster.Recruit("Kenji");
        entry.Injure(3);
        WarriorStats before = entry.Warrior.BaseStats;

        Assert.False(entry.Train(Drill.Strikes));
        state.AdvanceDay();

        Assert.Equal(before, entry.Warrior.BaseStats);
    }

    /// <summary>
    /// Training writes the raw stat; the disability's multiplier keeps being applied on top — a maimed
    /// warrior who works recovers, he does not get the lost arm back (GDD §7).
    /// </summary>
    [Fact]
    public void TrainingWritesBaseStatsAndLeavesTheDisabilityPenaltyStanding()
    {
        DojoState state = new(new DojoTuning { Training = _fast });
        state.Resources = new Resources(Gold: 500);
        RosterEntry entry = state.Roster.Recruit("Kenji");
        entry.Warrior.AddDisability(BodyPart.SwordArm);

        entry.Train(Drill.Guard);
        state.AdvanceDay();

        Warrior warrior = entry.Warrior;
        Assert.True(warrior.EffectiveStats.Strength < warrior.BaseStats.Strength);
    }

    [Fact]
    public void TheChosenDrillSurvivesTheInfirmary()
    {
        DojoState state = new(new DojoTuning { Training = _fast });
        state.Resources = new Resources(Gold: 500);
        RosterEntry entry = state.Roster.Recruit("Kenji");

        entry.Train(Drill.Conditioning);
        entry.Injure(1);
        state.AdvanceDay();

        Assert.Equal(Drill.Conditioning, entry.Drill);
        Assert.True(entry.Train());
        Assert.Equal(Drill.Conditioning, entry.Drill);
    }
}
