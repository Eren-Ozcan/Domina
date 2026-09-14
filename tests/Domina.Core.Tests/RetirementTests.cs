using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Retirement, and the post a retired man can hold (docs/GDD.md §10). The decisions protected here:
/// retiring is what a career ends in and not a way out of a bad week, a master of the house eats
/// nothing and draws no wage, he is better than a hire in the three trades he was trained for, and the
/// three roles that need an outsider stay barred — that bar is what keeps wages a real pressure.
/// </summary>
public class RetirementTests
{
    private static DojoState Dojo() => new(
        events: new EventTuning { ChancePerDay = 0 },
        school: new SchoolTuning { BuildDaysFactor = 0 })
    {
        Purse = new Resources(Gold: 5000, Food: 200, Water: 200),
    };

    private static RosterEntry Veteran(DojoState dojo, string name = "Kenji")
    {
        RosterEntry entry = dojo.Roster.Recruit(name);
        for (int fight = 0; fight < dojo.Tuning.VictoriesForRetirement; fight++)
        {
            dojo.Roster.Credit(entry.Id);
        }

        return entry;
    }

    /// <summary>A man with nothing behind him cannot walk into a post to dodge the week.</summary>
    [Fact]
    public void ARawWarriorCannotRetire()
    {
        DojoState dojo = Dojo();
        RosterEntry raw = dojo.Roster.Recruit("Kenji");

        Assert.False(dojo.CanRetire(raw));
        Assert.False(dojo.Retire(raw.Id));
        Assert.True(raw.IsFitForCampaign);
    }

    /// <summary>A career's worth of victories opens the door; so does a lost limb.</summary>
    [Fact]
    public void VictoriesOrALostLimbOpenTheDoor()
    {
        DojoState dojo = Dojo();

        Assert.True(dojo.CanRetire(Veteran(dojo)));

        RosterEntry maimed = dojo.Roster.Recruit("Goro");
        maimed.Warrior.AddDisability(BodyPart.SwordArm);

        Assert.True(dojo.CanRetire(maimed));
    }

    /// <summary>He leaves the field, stops eating, and is not on the roster the day's bill counts.</summary>
    [Fact]
    public void ARetiredManIsNoLongerAMouthOrASword()
    {
        DojoState dojo = Dojo();
        RosterEntry veteran = Veteran(dojo);
        dojo.Roster.Recruit("Goro");

        int fedBefore = dojo.Roster.Living.Count();

        Assert.True(dojo.Retire(veteran.Id));

        Assert.False(veteran.IsFitForCampaign);
        Assert.Equal(fedBefore - 1, dojo.Roster.Living.Count());
        Assert.Contains(veteran, dojo.Roster.Masters);
        Assert.True(veteran.Warrior.IsAlive);
    }

    /// <summary>He holds a post for nothing, and is worth more than a hire in it.</summary>
    [Fact]
    public void AMasterHoldsHisPostForNoWage()
    {
        DojoState dojo = Dojo();
        RosterEntry veteran = Veteran(dojo);
        dojo.BuySchoolNode(SchoolNodeId.TrainingGround);
        dojo.Retire(veteran.Id);

        Assert.True(dojo.Appoint(veteran.Id, StaffRole.DrillMaster));
        Assert.True(dojo.Staff.HeldByMaster(StaffRole.DrillMaster));
        Assert.Equal(0, dojo.Staff.DailyWage(dojo.StaffTuning));

        double master = dojo.School.Efficiency(SchoolNodeId.TrainingGround, dojo.Staff, dojo.StaffTuning);

        Assert.True(master > 1);
    }

    /// <summary>The three trades that need an outsider refuse him.</summary>
    [Fact]
    public void ThePhysicianTheCookAndTheDivinerRefuseAMaster()
    {
        DojoState dojo = Dojo();
        RosterEntry veteran = Veteran(dojo);
        dojo.BuySchoolNode(SchoolNodeId.Infirmary);
        dojo.BuySchoolNode(SchoolNodeId.Kitchen);
        dojo.BuySchoolNode(SchoolNodeId.DivinerHut);
        dojo.Retire(veteran.Id);

        Assert.False(dojo.Appoint(veteran.Id, StaffRole.Physician));
        Assert.False(dojo.Appoint(veteran.Id, StaffRole.Cook));
        Assert.False(dojo.Appoint(veteran.Id, StaffRole.Diviner));

        // And an outsider still can, which is the whole point of the bar.
        Assert.True(dojo.Hire(StaffRole.Physician));
    }

    /// <summary>A man still on the field cannot be appointed to anything.</summary>
    [Fact]
    public void OnlyARetiredManMayHoldAPost()
    {
        DojoState dojo = Dojo();
        RosterEntry veteran = Veteran(dojo);
        dojo.BuySchoolNode(SchoolNodeId.TrainingGround);

        Assert.False(dojo.Appoint(veteran.Id, StaffRole.DrillMaster));
    }

    /// <summary>The retirement and the post both survive a save, and a post with no master does not.</summary>
    [Fact]
    public void TheMasterAndHisPostSurviveTheSave()
    {
        DojoState dojo = Dojo();
        RosterEntry veteran = Veteran(dojo);
        dojo.BuySchoolNode(SchoolNodeId.TrainingGround);
        dojo.Retire(veteran.Id);
        dojo.Appoint(veteran.Id, StaffRole.DrillMaster);

        LoadResult loaded = DojoSaveFile.Load(DojoSaveFile.Write(dojo));

        Assert.True(loaded.Succeeded);

        DojoState after = loaded.State!;

        Assert.True(after.Roster.Find(veteran.Id)!.Retired);
        Assert.True(after.Staff.HeldByMaster(StaffRole.DrillMaster));
        Assert.Equal(0, after.Staff.DailyWage(after.StaffTuning));

        // A file that names a master who is not retired loses the post rather than the dojo.
        DojoSnapshot tampered = DojoSaveFile.Capture(dojo) with
        {
            Staff = [new PostSnapshot(StaffRole.DrillMaster, 9_999)],
        };

        Assert.False(DojoSaveFile.Restore(tampered).State!.Staff.Has(StaffRole.DrillMaster));
    }
}
