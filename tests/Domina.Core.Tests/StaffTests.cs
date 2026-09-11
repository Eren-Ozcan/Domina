using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Facilities and staff (docs/GDD.md §10). The decisions protected here: a building takes time and
/// gold cannot shorten it, an empty building still works at half, a post cannot be filled before its
/// building stands, and a payroll the treasury cannot meet costs the people — never the buildings.
/// </summary>
public class StaffTests
{
    /// <summary>Mishaps off: what is measured is the rule, not the day's luck.</summary>
    private static DojoState Rich(int gold = 5000, double buildFactor = 1.0) =>
        new(
            events: new EventTuning { ChancePerDay = 0 },
            school: new SchoolTuning { BuildDaysFactor = buildFactor })
        {
            Resources = new Resources(Gold: gold, Food: 500, Water: 500, Medicine: 50),
        };

    [Fact]
    public void AFacilityTakesItsDaysAndGoldCannotShortenThem()
    {
        DojoState state = Rich();
        int days = SchoolTree.Find(SchoolNodeId.TrainingGround).BuildDays;

        Assert.True(state.BuySchoolNode(SchoolNodeId.TrainingGround));
        Assert.False(state.School.Has(SchoolNodeId.TrainingGround));

        for (int day = 1; day < days; day++)
        {
            state.AdvanceDay();
            Assert.False(state.School.Has(SchoolNodeId.TrainingGround));
        }

        DayReport report = state.AdvanceDay();

        Assert.True(state.School.Has(SchoolNodeId.TrainingGround));
        Assert.Equal([SchoolNodeId.TrainingGround], report.Opened);
    }

    /// <summary>The same building cannot be ordered twice while it is going up.</summary>
    [Fact]
    public void ABuildingUnderConstructionCannotBeOrderedAgain()
    {
        DojoState state = Rich();

        Assert.True(state.BuySchoolNode(SchoolNodeId.Infirmary));
        int gold = state.Resources.Gold;

        Assert.False(state.BuySchoolNode(SchoolNodeId.Infirmary));
        Assert.Equal(gold, state.Resources.Gold);
    }

    /// <summary>The next tier waits for the building below it to open, not merely to be paid for.</summary>
    [Fact]
    public void ATierWaitsForTheBuildingBelowItToOpen()
    {
        DojoState state = Rich();

        Assert.True(state.BuySchoolNode(SchoolNodeId.TrainingGround));
        Assert.False(state.BuySchoolNode(SchoolNodeId.FormsMaster));

        Finish(state);

        Assert.True(state.BuySchoolNode(SchoolNodeId.FormsMaster));
    }

    /// <summary>
    /// An empty building works at half; a person brings it to full.
    /// </summary>
    /// <remarks>
    /// GDD §10's promise that the construction investment is never wasted, and the reason the wage is a
    /// decision rather than a tax: half of the bonus is already in the player's hands.
    /// </remarks>
    [Fact]
    public void AnEmptyBuildingWorksAtHalfAndAPersonBringsItToFull()
    {
        DojoState state = Rich(buildFactor: 0);
        double bare = state.Tuning.Training.GapClosedPerDay;

        Assert.True(state.BuySchoolNode(SchoolNodeId.TrainingGround));
        double empty = state.Tuning.Training.GapClosedPerDay;

        Assert.True(state.Hire(StaffRole.DrillMaster));
        double staffed = state.Tuning.Training.GapClosedPerDay;

        Assert.True(empty > bare, $"The empty building gave nothing ({empty} <= {bare}).");
        Assert.True(staffed > empty, $"The person added nothing ({staffed} <= {empty}).");

        // Half of the bonus, not half of the rate.
        double bonus = staffed - bare;
        Assert.Equal(bare + (bonus * state.StaffTuning.EmptyFacilityShare), empty, 9);
    }

    [Fact]
    public void APostCannotBeFilledBeforeItsBuildingStands()
    {
        DojoState state = Rich();

        Assert.False(state.Hire(StaffRole.DrillMaster));

        Assert.True(state.BuySchoolNode(SchoolNodeId.TrainingGround));
        Assert.False(state.Hire(StaffRole.DrillMaster));

        Finish(state);

        Assert.True(state.Hire(StaffRole.DrillMaster));
        Assert.False(state.Hire(StaffRole.DrillMaster));
    }

    [Fact]
    public void TheWageLeavesTheTreasuryEveryDay()
    {
        DojoState state = Rich(buildFactor: 0);
        state.BuySchoolNode(SchoolNodeId.TrainingGround);
        state.Hire(StaffRole.DrillMaster);

        int before = state.Resources.Gold;
        DayReport report = state.AdvanceDay();

        Assert.Equal(state.StaffTuning.WageOf(StaffRole.DrillMaster), report.Upkeep.Wages);
        Assert.True(state.Resources.Gold <= before - report.Upkeep.Wages);
    }

    /// <summary>
    /// A payroll that cannot be met costs the people, never the buildings.
    /// </summary>
    /// <remarks>
    /// This is the gearbox of GDD §10: in a crisis the dojo shrinks to its walls and can climb back by
    /// hiring again. If the building went with the person, one bad week would undo a season's saving.
    /// </remarks>
    [Fact]
    public void AnUnpaidPayrollCostsThePeopleNotTheBuildings()
    {
        DojoState state = Rich(gold: 5000, buildFactor: 0);
        state.BuySchoolNode(SchoolNodeId.TrainingGround);
        state.Hire(StaffRole.DrillMaster);

        state.Resources = state.Resources with { Gold = 1 };
        DayReport report = state.AdvanceDay();

        Assert.Equal([StaffRole.DrillMaster], report.Upkeep.Walked);
        Assert.Empty(state.Staff.Hired);
        Assert.True(state.School.Has(SchoolNodeId.TrainingGround));
    }

    /// <summary>The cook does not produce, he cuts the day's need.</summary>
    [Fact]
    public void TheCookCutsTheDaysFood()
    {
        int Eaten(bool cook)
        {
            DojoState state = Rich(buildFactor: 0);
            state.Roster.Recruit("Kenji");
            state.Roster.Recruit("Hana");
            state.Roster.Recruit("Botan");
            state.Roster.Recruit("Aiko");

            if (cook)
            {
                state.BuySchoolNode(SchoolNodeId.Kitchen);
                state.Hire(StaffRole.Cook);
            }

            return state.AdvanceDay().Upkeep.Food;
        }

        Assert.True(Eaten(cook: true) < Eaten(cook: false));
    }

    /// <summary>
    /// The physician's post is a gate: with him in the infirmary the dojo stops buying medicine.
    /// </summary>
    [Fact]
    public void ThePhysicianEndsTheMedicineBill()
    {
        DojoState state = Rich(buildFactor: 0);
        state.BuySchoolNode(SchoolNodeId.Infirmary);

        int priced = state.Economy.MedicinePrice;
        Assert.True(priced > 0);

        Assert.True(state.Hire(StaffRole.Physician));
        Assert.Equal(0, state.Economy.MedicinePrice);

        // Letting him go brings the bill back — the building alone does not buy medicine.
        Assert.True(state.Dismiss(StaffRole.Physician));
        Assert.Equal(priced, state.Economy.MedicinePrice);
    }

    /// <summary>The smith's forge cuts a repair bill, and half of it while the forge stands empty.</summary>
    [Fact]
    public void TheForgeCutsRepairsAndHalfOfItWhileEmpty()
    {
        DojoState state = Rich(buildFactor: 0);
        double bare = state.Economy.RepairGoldPerWear;

        state.BuySchoolNode(SchoolNodeId.Forge);
        double empty = state.Economy.RepairGoldPerWear;

        state.Hire(StaffRole.Smith);
        double staffed = state.Economy.RepairGoldPerWear;

        Assert.True(empty < bare);
        Assert.True(staffed < empty);
    }

    /// <summary>The class hall is what makes a class trainable; the training itself costs no gold.</summary>
    [Fact]
    public void AClassHallIsWhatMakesTheClassTrainable()
    {
        DojoState state = Rich(buildFactor: 0);
        RosterEntry entry = state.Roster.Recruit("Kenji");

        Assert.False(state.TrainClass(entry.Id, WarriorClass.Torite));

        state.BuySchoolNode(SchoolNodeId.ToriteHall);
        int gold = state.Resources.Gold;

        Assert.True(state.TrainClass(entry.Id, WarriorClass.Torite));
        Assert.Equal(WarriorClass.Torite, entry.Warrior.Class);
        Assert.Equal(gold, state.Resources.Gold);

        // A class he can still practise is not swapped for another.
        state.BuySchoolNode(SchoolNodeId.PoisonGarden);
        Assert.False(state.TrainClass(entry.Id, WarriorClass.Dokushi));
    }

    /// <summary>Losing an arm closes the hall he trained in and opens the choice again.</summary>
    [Fact]
    public void ALostArmReopensTheClassChoice()
    {
        DojoState state = Rich(buildFactor: 0);
        RosterEntry entry = state.Roster.Recruit("Kenji");

        state.BuySchoolNode(SchoolNodeId.ToriteHall);
        state.BuySchoolNode(SchoolNodeId.PoisonGarden);
        Assert.True(state.TrainClass(entry.Id, WarriorClass.Torite));

        entry.Warrior.AddDisability(BodyPart.SwordArm);

        Assert.False(state.TrainClass(entry.Id, WarriorClass.Torite));
        Assert.True(state.TrainClass(entry.Id, WarriorClass.Dokushi));
        Assert.Equal(WarriorClass.Dokushi, entry.Warrior.Class);
    }

    /// <summary>The broker widens the stall; he never touches its prices.</summary>
    [Fact]
    public void TheBrokerWidensTheStall()
    {
        DojoState state = Rich(gold: 20_000, buildFactor: 0);
        int plain = Rich(gold: 20_000, buildFactor: 0).Recruits.Count;

        state.BuySchoolNode(SchoolNodeId.Steward);
        state.BuySchoolNode(SchoolNodeId.Patron);
        state.BuySchoolNode(SchoolNodeId.Broker);
        state.Hire(StaffRole.Broker);

        Assert.Equal(plain + state.StaffTuning.BrokerExtraCandidates, state.Recruits.Count);
    }

    /// <summary>
    /// The physician turns a mortal wound around — the health branch's real return.
    /// </summary>
    /// <remarks>
    /// The gate needs the building <b>and</b> the person: an empty infirmary cannot half-save a life.
    /// The chance is forced to certainty here, because what is under test is the rule, not the number.
    /// </remarks>
    [Fact]
    public void ThePhysicianTurnsAMortalWoundAround()
    {
        DojoState state = new(
            events: new EventTuning { ChancePerDay = 0 },
            school: new SchoolTuning { BuildDaysFactor = 0 },
            staff: new StaffTuning { MortalSaveChance = 1.0 })
        {
            Resources = new Resources(Gold: 5000, Food: 100, Water: 100),
        };

        RosterEntry entry = state.Roster.Recruit("Kenji");
        state.BuySchoolNode(SchoolNodeId.Infirmary);
        state.Hire(StaffRole.Physician);

        AftermathReport report = new BattleAftermath().Apply(state, Fallen(entry));
        WarriorAftermath line = report.Warriors.Single();

        Assert.True(line.PulledBack);
        Assert.False(line.Died);
        Assert.True(entry.Warrior.IsAlive);
        Assert.Equal(state.StaffTuning.MortalWoundRecoveryDays, entry.RecoveryDaysRemaining);
    }

    /// <summary>With no physician in the building the same wound is what the fight said it was.</summary>
    [Fact]
    public void AnEmptyInfirmaryDoesNotSaveALife()
    {
        DojoState state = new(
            events: new EventTuning { ChancePerDay = 0 },
            school: new SchoolTuning { BuildDaysFactor = 0 },
            staff: new StaffTuning { MortalSaveChance = 1.0 })
        {
            Resources = new Resources(Gold: 5000, Food: 100, Water: 100),
        };

        RosterEntry entry = state.Roster.Recruit("Kenji");
        state.BuySchoolNode(SchoolNodeId.Infirmary);

        AftermathReport report = new BattleAftermath().Apply(state, Fallen(entry));

        Assert.True(report.Warriors.Single().Died);
        Assert.False(entry.Warrior.IsAlive);
    }

    /// <summary>A fight the warrior did not come out of.</summary>
    private static BattleResult Fallen(RosterEntry entry) => new(
        BattleOutcome.PlayerWipe,
        ElapsedSeconds: 30,
        [
            new WarriorBattleSummary(
                entry.Id,
                entry.Warrior.Name,
                Battle.PlayerTeam,
                CombatState.Dead,
                HealthRemaining: 0,
                AttacksMade: 6,
                HitsLanded: 2,
                TimesHit: 8,
                DodgesPerformed: 1,
                DamageDealt: 20,
                DamageTaken: 200,
                LostLimb: false),
        ]);

    /// <summary>Runs the days until every ordered building has opened.</summary>
    private static void Finish(DojoState state)
    {
        for (int guard = 0; guard < 60 && state.School.UnderConstruction.Count > 0; guard++)
        {
            state.AdvanceDay();
        }
    }
}
