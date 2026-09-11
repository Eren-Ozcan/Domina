using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The roster ceiling and the quarters branch (docs/GDD.md §10, docs/STORY.md). The decision
/// protected: six is where the roster <b>starts</b>, not where it ends — depth is bought like every
/// other lasting thing, with gold up front, days to build and a mouth to feed for the rest of the
/// season.
/// </summary>
public class QuartersTests
{
    private static DojoState Stocked(SchoolTuning? school = null) =>
        new(school: school ?? new SchoolTuning { BuildDaysFactor = 0 })
        {
            Resources = new Resources(Gold: 20000, Food: 400, Water: 400),
        };

    private static void Fill(DojoState dojo, int men)
    {
        for (int i = 0; i < men; i++)
        {
            dojo.Roster.Recruit($"Kenji {i}", WarriorStats.Recruit());
        }
    }

    /// <summary>The dojo opens with the room the dead master left it.</summary>
    [Fact]
    public void ADojoStartsWithTheMastersSixBeds()
    {
        Assert.Equal(6, Stocked().Capacity);
    }

    /// <summary>A man with nowhere to sleep is not bought, whatever the purse says.</summary>
    [Fact]
    public void AFullDojoCannotHire()
    {
        DojoState dojo = Stocked();
        Fill(dojo, dojo.Capacity);

        Assert.False(dojo.HasRoomForAnother);
        Assert.Null(dojo.HireRecruit(0));
        Assert.Null(dojo.Quartermaster.Hire(dojo, "Latecomer"));
        Assert.Equal(dojo.Capacity, dojo.Roster.Living.Count());
    }

    /// <summary>Each tier of the quarters raises the ceiling, and the bed is not a half-measure.</summary>
    /// <remarks>
    /// The half-efficiency rule of an empty building does not reach here: a bed either exists or it
    /// does not, and half a bed houses nobody.
    /// </remarks>
    [Fact]
    public void EachTierOfTheQuartersRaisesTheCeiling()
    {
        DojoState dojo = Stocked();
        int start = dojo.Capacity;

        Assert.True(dojo.BuySchoolNode(SchoolNodeId.Barracks));
        dojo.AdvanceDay();
        Assert.Equal(start + dojo.School.Tuning.HousingPerTier, dojo.Capacity);

        Assert.True(dojo.BuySchoolNode(SchoolNodeId.LongHouse));
        dojo.AdvanceDay();
        Assert.Equal(start + (2 * dojo.School.Tuning.HousingPerTier), dojo.Capacity);
    }

    /// <summary>The beds arrive when the building does, not when it is paid for.</summary>
    [Fact]
    public void TheBedsWaitForTheBuilding()
    {
        DojoState dojo = Stocked(new SchoolTuning());
        int start = dojo.Capacity;

        Assert.True(dojo.BuySchoolNode(SchoolNodeId.Barracks));
        Assert.Equal(start, dojo.Capacity);

        while (dojo.School.IsBuilding(SchoolNodeId.Barracks))
        {
            dojo.AdvanceDay();
        }

        Assert.Equal(start + dojo.School.Tuning.HousingPerTier, dojo.Capacity);
    }

    /// <summary>A bought bed makes room for a man the day before could not take.</summary>
    [Fact]
    public void ABoughtBedOpensTheStallAgain()
    {
        DojoState dojo = Stocked();
        Fill(dojo, dojo.Capacity);
        Assert.Null(dojo.Quartermaster.Hire(dojo, "Latecomer"));

        dojo.BuySchoolNode(SchoolNodeId.Barracks);
        dojo.AdvanceDay();

        Assert.NotNull(dojo.Quartermaster.Hire(dojo, "Latecomer"));
    }

    /// <summary>The dead and the released do not occupy a bed.</summary>
    [Fact]
    public void OnlyTheLivingTakeUpRoom()
    {
        DojoState dojo = Stocked();
        Fill(dojo, dojo.Capacity);

        dojo.Roster.Kill(dojo.Roster.Living.First().Id);
        Assert.True(dojo.HasRoomForAnother);

        Assert.NotNull(dojo.Quartermaster.Hire(dojo, "Replacement"));
        Assert.False(dojo.HasRoomForAnother);

        dojo.Release(dojo.Roster.Living.First().Id);
        Assert.True(dojo.HasRoomForAnother);
    }

    /// <summary>The quarters survive the save like every other building.</summary>
    [Fact]
    public void TheBedsSurviveASaveAndLoad()
    {
        DojoState before = Stocked();
        before.BuySchoolNode(SchoolNodeId.Barracks);
        before.AdvanceDay();

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.Equal(before.Capacity, after.Capacity);
    }
}
