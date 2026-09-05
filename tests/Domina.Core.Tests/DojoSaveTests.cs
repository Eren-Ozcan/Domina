using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Kayıt sistemi (GDD §2): versiyonlu, merge-on-load, hiçbir koşulda fırlatmayan.
/// Korunan karar şu: dosyaya yalnızca oyuncunun ürettiği şey yazılır — denge sayıları
/// yüklerken yeniden hesaplanır, yoksa eski kayıt yeni dengeyi geri getirirdi.
/// </summary>
public class DojoSaveTests
{
    private static DojoState Populated()
    {
        DojoState state = new()
        {
            Resources = new Resources(Gold: 120, Food: 8, Water: 6, Medicine: 2),
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
        Assert.Contains(result.Warnings, w => w.Contains("daha yeni", StringComparison.Ordinal));
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
                { "id": 1, "name": "Çakışan", "isAlive": true },
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
        Assert.Equal("İsimsiz 9", result.State!.Roster.Find(new WarriorId(9))!.Name);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void ADayCounterBelowOneIsPulledBack()
    {
        LoadResult result = DojoSaveFile.Restore(DojoSnapshot.Empty with { Day = 0 });

        Assert.Equal(1, result.State!.Day);
        Assert.Single(result.Warnings);
    }
}
