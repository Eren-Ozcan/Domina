using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The save system (GDD §2): versioned, merge-on-load, throwing under no circumstances.
/// The decision protected is this: only what the player produced is written to the file — the balance
/// numbers are recomputed on load, or an old save would bring back the old balance.
/// </summary>
public class DojoSaveTests
{
    private static DojoState Populated(bool instantBuild = false)
    {
        DojoState state = new(school: instantBuild ? new SchoolTuning { BuildDaysFactor = 0 } : null)
        {
            Purse = new Resources(Gold: 120, Food: 8, Water: 6, Medicine: 2),
        };

        RosterEntry kenji = state.Roster.Recruit(
            "Kenji",
            WarriorStats.Recruit() with { Strength = 61 },
            Weapon.PoisonedTanto(),
            Armor.Medium());
        kenji.Warrior.Thrown = ThrownWeapon.PoisonedShuriken();
        kenji.Warrior.Honor = 71;
        kenji.Warrior.AddDisability(BodyPart.LeftLeg);
        kenji.Warrior.ArmorWear = new ArmorWearSet(Torso: 42.5, Head: 3);
        kenji.Injure(4);

        RosterEntry hana = state.Roster.Recruit("Hana");
        hana.Train();
        state.AdvanceDay();

        RosterEntry gone = state.Roster.Recruit("Sora");
        state.Roster.Kill(gone.Id);

        return state;
    }

    [Fact]
    public void ARoundTripKeepsEveryWarriorTheDayAndThePurse()
    {
        DojoState before = Populated();

        LoadResult result = DojoSaveFile.Load(DojoSaveFile.Write(before));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Warnings);
        DojoState after = result.State!;
        Assert.Equal(before.Day, after.Day);
        Assert.Equal(before.Resources, after.Resources);
        Assert.Equal(before.Roster.Count, after.Roster.Count);
    }

    [Fact]
    public void ARoundTripKeepsTheSchoolsName()
    {
        DojoState before = Populated();
        before.Name = "The Ashigara dojo";

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.Equal("The Ashigara dojo", after.Name);
    }

    [Fact]
    public void ARoundTripKeepsTheInstructorsName()
    {
        DojoState before = Populated();
        before.Instructor = "Master Ashigara";

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.Equal("Master Ashigara", after.Instructor);
    }

    [Fact]
    public void ATermWrittenBeforeInstructorsWereNamedIsKeptByThePost()
    {
        DojoSnapshot old = DojoSaveFile.Capture(Populated()) with { Instructor = null };

        DojoState after = DojoSaveFile.Restore(old).State!;

        Assert.False(string.IsNullOrWhiteSpace(after.Instructor));
    }

    [Fact]
    public void ATermWrittenBeforeSchoolsWereNamedKeepsTheNameItAlwaysHad()
    {
        // A save written before the field existed carries no name at all; loading one must not leave
        // the title screen printing an empty line where the term's name goes.
        DojoSnapshot old = DojoSaveFile.Capture(Populated()) with { Name = null };

        DojoState after = DojoSaveFile.Restore(old).State!;

        Assert.False(string.IsNullOrWhiteSpace(after.Name));
    }

    [Fact]
    public void ARoundTripKeepsWhatTheBattleReadsFromAWarrior()
    {
        DojoState before = Populated();
        RosterEntry source = before.Roster.FindLiving("Kenji")!;

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;
        RosterEntry loaded = after.Roster.Find(source.Id)!;

        Assert.Equal(source.Warrior.BaseStats, loaded.Warrior.BaseStats);
        Assert.Equal(source.Warrior.Honor, loaded.Warrior.Honor, 6);
        Assert.Equal(source.Warrior.Weapon, loaded.Warrior.Weapon);
        Assert.Equal(source.Warrior.Armor, loaded.Warrior.Armor);
        Assert.Equal(source.Warrior.Thrown, loaded.Warrior.Thrown);
        Assert.Equal(source.Warrior.ArmorWear, loaded.Warrior.ArmorWear);
        Assert.Equal(source.Warrior.EffectiveStats, loaded.Warrior.EffectiveStats);
        Assert.Equal(source.RecoveryDaysRemaining, loaded.RecoveryDaysRemaining);
        Assert.Equal(source.TrainingDays, loaded.TrainingDays);
    }

    /// <summary>
    /// The drill is the player's decision, not a derived value: if the save did not carry it, the game
    /// would return the warrior to the default drill at every launch.
    /// </summary>
    [Fact]
    public void ARoundTripKeepsTheChosenDrill()
    {
        DojoState before = Populated();
        RosterEntry source = before.Roster.FindLiving("Hana")!;
        Assert.True(source.Train(Drill.Footwork));

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.Equal(Drill.Footwork, after.Roster.Find(source.Id)!.Drill);
    }

    /// <summary>
    /// The school and the path go into the save: both are the player's irreversible decisions. The size
    /// of the bonuses does not — a balance number comes from the code, not from the file (GDD §2).
    /// </summary>
    [Fact]
    public void ARoundTripKeepsTheSchoolAndTheChosenPath()
    {
        DojoState before = Populated(instantBuild: true);
        before.SetPurse(before.Resources with { Gold = 5000 });
        before.BuySchoolNode(SchoolNodeId.TrainingGround);
        before.BuySchoolNode(SchoolNodeId.FormsMaster);

        RosterEntry source = before.Roster.FindLiving("Hana")!;
        for (int day = 0; day < before.Tuning.Training.PathTrainingDays; day++)
        {
            source.Train(Drill.Guard);
            before.AdvanceDay();
        }

        Assert.True(before.ChoosePath(source.Id, WarriorPath.Stone));

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.True(after.School.Has(SchoolNodeId.TrainingGround));
        Assert.True(after.School.Has(SchoolNodeId.FormsMaster));
        Assert.Equal(before.Tuning.Training.GapClosedPerDay, after.Tuning.Training.GapClosedPerDay, 9);
        Assert.Equal(WarriorPath.Stone, after.Roster.Find(source.Id)!.Warrior.Path);
    }

    /// <summary>
    /// A building that has been paid for but is not standing yet survives the save.
    /// </summary>
    /// <remarks>
    /// Its gold has already left the treasury, so losing the site on load would be losing a paid-for
    /// building; rounding its days up would be a way of shortening a wait gold cannot shorten.
    /// </remarks>
    [Fact]
    public void ARoundTripKeepsABuildingUnderConstruction()
    {
        DojoState before = Populated();
        before.SetPurse(before.Resources with { Gold = 5000 });
        Assert.True(before.BuySchoolNode(SchoolNodeId.Infirmary));
        before.AdvanceDay();

        int left = before.School.UnderConstruction[SchoolNodeId.Infirmary];

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.Equal(left, after.School.UnderConstruction[SchoolNodeId.Infirmary]);
        Assert.False(after.School.Has(SchoolNodeId.Infirmary));
    }

    /// <summary>
    /// The staff go into the save, and a post whose building is not standing is dropped.
    /// </summary>
    [Fact]
    public void ARoundTripKeepsTheStaffButNotAPostWithNoBuilding()
    {
        DojoState before = Populated(instantBuild: true);
        before.SetPurse(before.Resources with { Gold = 5000 });
        Assert.True(before.BuySchoolNode(SchoolNodeId.TrainingGround));
        Assert.True(before.Hire(StaffRole.DrillMaster));

        DojoSnapshot tampered = DojoSaveFile.Capture(before) with
        {
            Staff = [new PostSnapshot(StaffRole.DrillMaster), new PostSnapshot(StaffRole.Physician)],
        };

        DojoState after = DojoSaveFile.Restore(tampered).State!;

        Assert.True(after.Staff.Has(StaffRole.DrillMaster));
        Assert.False(after.Staff.Has(StaffRole.Physician));
    }

    /// <summary>
    /// The class goes into the save too: it was bought with a facility, and reloading must not hand
    /// the player a warrior who has forgotten what he can do.
    /// </summary>
    [Fact]
    public void ARoundTripKeepsTheTrainedClass()
    {
        DojoState before = Populated();
        RosterEntry source = before.Roster.FindLiving("Hana")!;
        source.Warrior.Class = WarriorClass.Torite;

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.Equal(WarriorClass.Torite, after.Roster.Find(source.Id)!.Warrior.Class);
    }

    /// <summary>A corrupted save cannot skip a branch's order: a master with no training ground is dropped.</summary>
    [Fact]
    public void ASaveCannotSkipAStepInASchoolBranch()
    {
        DojoSnapshot snapshot = DojoSnapshot.Empty with
        {
            School = [SchoolNodeId.FormsMaster],
        };

        DojoState state = DojoSaveFile.Restore(snapshot).State!;

        Assert.Empty(state.School.Owned);
    }

    [Fact]
    public void TheDeadStayDeadThroughASave()
    {
        DojoState before = Populated();
        WarriorId dead = before.Roster.Entries.Single(e => !e.Warrior.IsAlive).Id;

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.False(after.Roster.Find(dead)!.Warrior.IsAlive);
        Assert.Null(after.Roster.FindLiving("Sora"));
    }

    [Fact]
    public void AFreshRecruitAfterLoadingDoesNotStealAnExistingId()
    {
        DojoState before = Populated();
        int highest = before.Roster.Entries.Max(e => e.Id.Value);

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;
        RosterEntry recruit = after.Roster.Recruit("Yuki");

        Assert.Equal(highest + 1, recruit.Id.Value);
    }

    [Fact]
    public void GarbageIsReportedInsteadOfThrown()
    {
        LoadResult result = DojoSaveFile.Load("{ this is not json");

        Assert.False(result.Succeeded);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void AnEmptyFileIsReportedInsteadOfThrown()
    {
        Assert.False(DojoSaveFile.Load(null).Succeeded);
        Assert.False(DojoSaveFile.Load("   ").Succeeded);
    }

    [Fact]
    public void MissingFieldsLoadWithTheirDefaults()
    {
        const string Json = """
            {
              "version": 1,
              "day": 4,
              "warriors": [ { "id": 3, "name": "Kenji", "isAlive": true } ]
            }
            """;

        LoadResult result = DojoSaveFile.Load(Json);

        Assert.True(result.Succeeded);
        RosterEntry entry = result.State!.Roster.Find(new WarriorId(3))!;
        Assert.Equal(4, result.State.Day);
        Assert.Equal(Resources.Empty, result.State.Resources);
        Assert.Equal(Weapon.Katana(), entry.Warrior.Weapon);
        Assert.Equal(Armor.None(), entry.Warrior.Armor);
        Assert.Null(entry.Warrior.Thrown);
        Assert.Empty(entry.Warrior.Disabilities);
    }

    [Fact]
    public void UnknownFieldsAreIgnoredSoAnOlderBuildCanStillRead()
    {
        const string Json = """
            {
              "version": 1,
              "day": 2,
              "somethingFromTheFuture": { "nested": [1, 2, 3] },
              "warriors": [ { "id": 1, "name": "Kenji", "isAlive": true, "mood": "grim" } ]
            }
            """;

        LoadResult result = DojoSaveFile.Load(Json);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.State!.Roster.FindLiving("Kenji"));
    }

    [Fact]
    public void ANewerFileLoadsButSaysSo()
    {
        DojoSnapshot fromTheFuture = DojoSnapshot.Empty with { Version = DojoSnapshot.CurrentVersion + 1 };

        LoadResult result = DojoSaveFile.Restore(fromTheFuture);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Warnings, w => w.Contains("newer version", StringComparison.Ordinal));
    }

    [Fact]
    public void OneBrokenWarriorDoesNotTakeTheRosterWithIt()
    {
        const string Json = """
            {
              "version": 1,
              "day": 1,
              "warriors": [
                { "id": 1, "name": "Kenji", "isAlive": true },
                { "id": 1, "name": "Clashing", "isAlive": true },
                { "id": 2, "name": "Hana", "isAlive": true }
              ]
            }
            """;

        LoadResult result = DojoSaveFile.Load(Json);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.State!.Roster.Count);
        Assert.NotNull(result.State.Roster.FindLiving("Kenji"));
        Assert.NotNull(result.State.Roster.FindLiving("Hana"));
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void TwoLivingWarriorsCannotComeBackWithTheSameName()
    {
        const string Json = """
            {
              "version": 1,
              "day": 1,
              "warriors": [
                { "id": 1, "name": "Kenji", "isAlive": true },
                { "id": 2, "name": "kenji", "isAlive": true }
              ]
            }
            """;

        LoadResult result = DojoSaveFile.Load(Json);

        Assert.Equal(2, result.State!.Roster.Count);
        Assert.Equal(new WarriorId(1), result.State.Roster.FindLiving("Kenji")!.Id);
        Assert.NotNull(result.State.Roster.FindLiving("kenji (2)"));
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void ANamelessRecordIsGivenAName()
    {
        const string Json = """
            { "version": 1, "day": 1, "warriors": [ { "id": 9, "name": "", "isAlive": true } ] }
            """;

        LoadResult result = DojoSaveFile.Load(Json);

        Assert.True(result.Succeeded);
        Assert.Equal("Nameless 9", result.State!.Roster.Find(new WarriorId(9))!.Name);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void ADayCounterBelowOneIsPulledBack()
    {
        LoadResult result = DojoSaveFile.Restore(DojoSnapshot.Empty with { Day = 0 });

        Assert.Equal(1, result.State!.Day);
        Assert.Single(result.Warnings);
    }

    /// <summary>
    /// A candidate bought today goes into the save: without it the player could buy the same man over and
    /// over by reloading (the stall is frozen within the day).
    /// </summary>
    [Fact]
    public void ALoadedSaveRemembersWhichCandidatesWereAlreadyBought()
    {
        DojoState state = new() { Purse = new Resources(Gold: 5000) };
        Assert.NotNull(state.HireRecruit(1));

        LoadResult loaded = DojoSaveFile.Load(DojoSaveFile.Write(state));

        Assert.True(loaded.Succeeded);
        Assert.Equal([1], loaded.State!.HiredToday);
        Assert.Null(loaded.State.HireRecruit(1));
    }

    /// <summary>A corrupted save falls back to yesterday's, and says so.</summary>
    /// <remarks>
    /// Open Decision #15: the backup is for a broken file and not an undo door, so it fires only when
    /// the current save cannot be read at all — and when it fires the player is told.
    /// </remarks>
    [Fact]
    public void ACorruptedSaveFallsBackToYesterdayAndSaysSo()
    {
        DojoState dojo = new() { Purse = new Resources(Gold: 321) };
        dojo.Roster.Recruit("Kenji");

        string yesterday = DojoSaveFile.Write(dojo);

        LoadResult loaded = DojoSaveFile.LoadWithBackup("{ this is not a save", yesterday);

        Assert.True(loaded.Succeeded);
        Assert.Equal(321, loaded.State!.Resources.Gold);
        Assert.Contains(loaded.Warnings, w => w.Contains("day before", StringComparison.Ordinal));
    }

    /// <summary>A save that loads is never replaced by the backup, warnings or not.</summary>
    [Fact]
    public void AReadableSaveIsNeverReplacedByTheBackup()
    {
        DojoState today = new() { Purse = new Resources(Gold: 10) };
        DojoState yesterday = new() { Purse = new Resources(Gold: 999) };

        LoadResult loaded = DojoSaveFile.LoadWithBackup(
            DojoSaveFile.Write(today),
            DojoSaveFile.Write(yesterday));

        Assert.Equal(10, loaded.State!.Resources.Gold);
    }

    /// <summary>With both broken, the failure reported is the current save's.</summary>
    [Fact]
    public void TwoBrokenFilesReportTheCurrentOne()
    {
        LoadResult loaded = DojoSaveFile.LoadWithBackup("{ broken", "{ also broken");

        Assert.False(loaded.Succeeded);
    }
}
