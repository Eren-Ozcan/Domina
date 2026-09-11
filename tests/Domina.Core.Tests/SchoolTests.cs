using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The school tree and the warrior's path (GDD §10 "Skill tree depth"). The decisions protected: the
/// order within a branch is compulsory, a facility is paid up front and not sold back, the bonuses apply
/// to <b>all</b> reads, and a warrior's path is not bought — it is unlocked with training days.
/// </summary>
public class SchoolTests
{
    private static DojoState Rich(int gold = 5000) =>
        // Mishaps off and construction instant: what is measured here is the tree itself — neither the
        // day's luck nor the calendar. Build time has its own tests below.
        new(
            events: new EventTuning { ChancePerDay = 0 },
            school: new SchoolTuning { BuildDaysFactor = 0 })
        {
            Resources = new Resources(Gold: gold),
        };

    /// <summary>Puts the warrior on drill for the given number of days and closes the days.</summary>
    private static void Drills(DojoState state, RosterEntry entry, int days)
    {
        for (int day = 0; day < days; day++)
        {
            entry.Train(Drill.Strikes);
            state.AdvanceDay();
        }
    }

    [Fact]
    public void ABranchMustBeBoughtInOrder()
    {
        DojoState state = Rich();

        Assert.False(state.BuySchoolNode(SchoolNodeId.FormsMaster));
        Assert.True(state.BuySchoolNode(SchoolNodeId.TrainingGround));
        Assert.True(state.BuySchoolNode(SchoolNodeId.FormsMaster));
    }

    [Fact]
    public void AFacilityIsPaidForOnceAndUpFront()
    {
        DojoState state = Rich(gold: 200);

        Assert.True(state.BuySchoolNode(SchoolNodeId.TrainingGround));
        Assert.Equal(0, state.Resources.Gold);
        Assert.False(state.BuySchoolNode(SchoolNodeId.TrainingGround));
    }

    /// <summary>The treasury does not go negative: a facility that cannot be afforded is not bought, and the tree does not change.</summary>
    [Fact]
    public void AFacilityBeyondThePurseChangesNothing()
    {
        DojoState state = Rich(gold: 199);

        Assert.False(state.BuySchoolNode(SchoolNodeId.TrainingGround));
        Assert.Equal(199, state.Resources.Gold);
        Assert.Empty(state.School.Owned);
    }

    [Fact]
    public void TheTrainingBranchSpeedsUpTrainingAndLiftsTheCeiling()
    {
        DojoState state = Rich();
        double rate = state.Tuning.Training.GapClosedPerDay;
        double ceiling = state.Tuning.Training.SkillCeiling;

        state.BuySchoolNode(SchoolNodeId.TrainingGround);
        Assert.True(state.Tuning.Training.GapClosedPerDay > rate);

        state.BuySchoolNode(SchoolNodeId.FormsMaster);
        Assert.True(state.Tuning.Training.SkillCeiling > ceiling);

        double twoTiers = state.Tuning.Training.GapClosedPerDay;
        state.BuySchoolNode(SchoolNodeId.InnerDojo);
        Assert.True(state.Tuning.Training.GapClosedPerDay > twoTiers);
    }

    [Fact]
    public void TheInfirmaryBranchBuysBackDays()
    {
        DojoState state = Rich();
        int natural = state.Tuning.NaturalRecoveryPerDay;
        int medicine = state.Economy.MedicineRecoveryDays;
        double free = state.Tuning.RecoveryFreeDamageShare;

        state.BuySchoolNode(SchoolNodeId.Infirmary);
        state.BuySchoolNode(SchoolNodeId.Herbalist);
        state.BuySchoolNode(SchoolNodeId.BoneSetter);

        Assert.True(state.Tuning.NaturalRecoveryPerDay > natural);
        Assert.True(state.Economy.MedicineRecoveryDays > medicine);
        Assert.True(state.Tuning.RecoveryFreeDamageShare > free);
    }

    [Fact]
    public void TheStewardBranchMovesPricesAndTheReward()
    {
        DojoState state = Rich();
        int food = state.Economy.FoodPrice;
        double reward = state.Economy.VictoryGoldPerEnemyHealth;
        int recruit = state.Economy.RecruitPrice;

        state.BuySchoolNode(SchoolNodeId.Steward);
        Assert.True(state.Economy.FoodPrice < food);

        state.BuySchoolNode(SchoolNodeId.Patron);
        Assert.True(state.Economy.VictoryGoldPerEnemyHealth > reward);

        state.BuySchoolNode(SchoolNodeId.Broker);
        Assert.True(state.Economy.RecruitPrice < recruit);
    }

    /// <summary>A discount cannot make an item free.</summary>
    [Fact]
    public void ADiscountNeverReachesZero()
    {
        DojoState state = new(economy: new EconomyTuning { WaterPrice = 1 })
        {
            Resources = new Resources(Gold: 5000),
        };

        state.BuySchoolNode(SchoolNodeId.Steward);

        Assert.Equal(1, state.Economy.WaterPrice);
    }

    [Fact]
    public void ATrainingDayIsWorthMoreWithTheTrainingGround()
    {
        DojoState plain = Rich();
        DojoState built = Rich();
        built.BuySchoolNode(SchoolNodeId.TrainingGround);

        double plainGain = Gain(plain);
        double builtGain = Gain(built);

        Assert.True(builtGain > plainGain);

        static double Gain(DojoState state)
        {
            RosterEntry entry = state.Roster.Recruit("Kenji");
            double before = entry.Warrior.BaseStats.Accuracy;
            entry.Train(Drill.Strikes);
            state.AdvanceDay();
            return entry.Warrior.BaseStats.Accuracy - before;
        }
    }

    [Fact]
    public void APathIsEarnedWithTrainingDaysNotGold()
    {
        DojoState state = Rich();
        RosterEntry entry = state.Roster.Recruit("Kenji");

        Assert.False(state.ChoosePath(entry.Id, WarriorPath.Blade));

        Drills(state, entry, state.Tuning.Training.PathTrainingDays);

        Assert.True(state.ChoosePath(entry.Id, WarriorPath.Blade));
        Assert.Equal(WarriorPath.Blade, entry.Warrior.Path);
    }

    [Fact]
    public void APathIsChosenOnceAndNeverSwapped()
    {
        DojoState state = Rich();
        RosterEntry entry = state.Roster.Recruit("Kenji");
        Drills(state, entry, state.Tuning.Training.PathTrainingDays);
        Assert.True(state.ChoosePath(entry.Id, WarriorPath.Stone));

        Assert.False(state.ChoosePath(entry.Id, WarriorPath.Shadow));
        Assert.Equal(WarriorPath.Stone, entry.Warrior.Path);
    }

    [Fact]
    public void ThePathShowsUpInTheStatsTheBattleReads()
    {
        Warrior warrior = new(new WarriorId(1), "Kenji", WarriorStats.Recruit());
        double accuracy = warrior.EffectiveStats.Accuracy;

        warrior.Path = WarriorPath.Blade;

        Assert.True(warrior.EffectiveStats.Accuracy > accuracy);
        Assert.Equal(WarriorStats.Recruit().Accuracy, warrior.BaseStats.Accuracy);
    }

    /// <summary>
    /// The path is applied <b>underneath</b> the disability: a lost limb's penalty cuts the path too, the
    /// path does not magnify the penalty.
    /// </summary>
    [Fact]
    public void ThePathIsCutByADisabilityRatherThanFeedingIt()
    {
        Warrior plain = new(new WarriorId(1), "Kenji", WarriorStats.Recruit());
        Warrior walker = new(new WarriorId(2), "Hana", WarriorStats.Recruit())
        {
            Path = WarriorPath.Shadow,
        };
        plain.AddDisability(BodyPart.LeftLeg);
        walker.AddDisability(BodyPart.LeftLeg);

        double plainLoss = plain.BaseStats.Evasion - plain.EffectiveStats.Evasion;
        double walkerLoss = PathScale.Apply(walker.BaseStats, WarriorPath.Shadow).Evasion
            - walker.EffectiveStats.Evasion;

        Assert.True(walker.EffectiveStats.Evasion > plain.EffectiveStats.Evasion);
        Assert.True(walkerLoss > plainLoss);
    }
}
