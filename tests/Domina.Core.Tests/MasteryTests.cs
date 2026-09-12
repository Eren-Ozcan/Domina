using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Weapon mastery — the weapon master's system (docs/GDD.md §10). The decisions protected here: no
/// mastery at all without his hall, mastery belongs to the <b>pairing</b> of a man and one weapon, and
/// what it buys in the fight is accuracy and nothing else.
/// </summary>
public class MasteryTests
{
    private static DojoState Dojo(bool innerDojo, bool master)
    {
        DojoState state = new(
            events: new EventTuning { ChancePerDay = 0 },
            school: new SchoolTuning { BuildDaysFactor = 0 })
        {
            Resources = new Resources(Gold: 5000, Food: 200, Water: 200),
        };

        if (innerDojo)
        {
            state.BuySchoolNode(SchoolNodeId.TrainingGround);
            state.BuySchoolNode(SchoolNodeId.FormsMaster);
            state.BuySchoolNode(SchoolNodeId.InnerDojo);
        }

        if (master)
        {
            state.Hire(StaffRole.WeaponMaster);
        }

        return state;
    }

    /// <summary>The whole system is the post's output: no hall, no mastery.</summary>
    [Fact]
    public void WithoutTheHallADrillDayTeachesNoMastery()
    {
        DojoState state = Dojo(innerDojo: false, master: false);
        RosterEntry entry = state.Roster.Recruit("Kenji");
        entry.Train();

        state.AdvanceDay();

        Assert.Equal(0, entry.Warrior.WeaponSkill);
    }

    /// <summary>An empty hall teaches at half, the master at full.</summary>
    [Fact]
    public void AnEmptyHallTeachesHalfOfWhatTheMasterDoes()
    {
        double SkillAfterADay(bool master)
        {
            DojoState state = Dojo(innerDojo: true, master);
            RosterEntry entry = state.Roster.Recruit("Kenji");
            entry.Train();

            state.AdvanceDay();
            return entry.Warrior.WeaponSkill;
        }

        double empty = SkillAfterADay(master: false);
        double staffed = SkillAfterADay(master: true);

        Assert.True(empty > 0);
        Assert.Equal(staffed / 2, empty, 6);
    }

    /// <summary>Meditation is the day the sword is not touched.</summary>
    [Fact]
    public void MeditationBuysNoMastery()
    {
        DojoState state = Dojo(innerDojo: true, master: true);
        RosterEntry entry = state.Roster.Recruit("Kenji");
        entry.Train(Drill.Meditation);

        state.AdvanceDay();

        Assert.Equal(0, entry.Warrior.WeaponSkill);
    }

    /// <summary>Changing weapon gives up what was learned; the old weapon keeps it.</summary>
    [Fact]
    public void MasteryBelongsToTheWeaponAndNotToTheMan()
    {
        Warrior warrior = new(new WarriorId(1), "Kenji", WarriorStats.Recruit(), Weapon.Katana());
        warrior.GainMastery(0.5);

        Assert.Equal(0.5, warrior.WeaponSkill, 6);

        warrior.Weapon = Weapon.Yari();
        Assert.Equal(0, warrior.WeaponSkill);

        warrior.Weapon = Weapon.Katana();
        Assert.Equal(0.5, warrior.WeaponSkill, 6);
    }

    /// <summary>Mastery lands on accuracy alone — every other stat is left where it was.</summary>
    [Fact]
    public void MasteryLiftsAccuracyAndNothingElse()
    {
        Warrior warrior = new(new WarriorId(1), "Kenji", WarriorStats.Recruit());
        WarriorStats before = warrior.EffectiveStats;

        warrior.GainMastery(1.0);
        WarriorStats after = warrior.EffectiveStats;

        Assert.Equal(before.Accuracy * MasteryBand.Default.FactorFor(1), after.Accuracy, 6);
        Assert.Equal(before.Strength, after.Strength);
        Assert.Equal(before.Defense, after.Defense);
        Assert.Equal(before.Speed, after.Speed);
    }

    /// <summary>A lost arm takes the two-handed weapon, and with it the mastery of that weapon.</summary>
    [Fact]
    public void ALostArmTakesTheMasteryOfATwoHandedWeaponWithIt()
    {
        Warrior warrior = new(new WarriorId(1), "Kenji", WarriorStats.Recruit(), Weapon.Nodachi());
        warrior.GainMastery(0.8);

        warrior.AddDisability(BodyPart.SwordArm);

        Assert.Equal(0, warrior.WeaponSkill);
        Assert.Equal(0.8, warrior.Mastery.Of("Nodachi"), 6);
    }

    /// <summary>Mastery is a share of the gap, so it approaches 1 and never reaches it.</summary>
    [Fact]
    public void MasteryApproachesFullnessAndNeverPassesIt()
    {
        Warrior warrior = new(new WarriorId(1), "Kenji", WarriorStats.Recruit());

        for (int day = 0; day < 500; day++)
        {
            warrior.GainMastery(0.05);
        }

        Assert.True(warrior.WeaponSkill < 1);
        Assert.True(warrior.WeaponSkill > 0.99);
    }
}
