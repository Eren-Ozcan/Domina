using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// The strip's model. The decisions protected: a store says how long it lasts rather than how much of
/// it there is, a store nothing draws on is not given an invented rate, and the two days before a
/// store runs out are the ones the strip marks.
/// </summary>
public class StripModelTests
{
    private static DojoState Quiet(int men = 2, Resources? purse = null)
    {
        DojoState state = new(events: new EventTuning { ChancePerDay = 0 })
        {
            Purse = purse ?? new Resources(Gold: 400, Food: 200, Water: 200, Medicine: 20),
        };

        for (int i = 0; i < men; i++)
        {
            state.Roster.Recruit($"Kenji {i}", WarriorStats.Recruit());
        }

        return state;
    }

    [Fact]
    public void TheDayIsPrintedAgainstItsTerm()
    {
        StripLine line = StripModel.Describe(Quiet());

        Assert.Equal("Day 1", line.Day);
        Assert.Equal("of 180", line.Term);
    }

    [Fact]
    public void AStoreSaysHowManyDaysItLasts()
    {
        DojoState dojo = Quiet();
        Resources draw = dojo.DailyDraw();

        StripStore rice = StripModel.Describe(dojo).Stores[1];

        Assert.Equal(dojo.Resources.Food / draw.Food, rice.Days);
        Assert.Contains("days", rice.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void AStoreNothingDrawsOnIsGivenNoRate()
    {
        // Nobody is wounded, so the infirmary takes no medicine: the line must not invent a rate the
        // player would plan around.
        StripStore medicine = StripModel.Describe(Quiet()).Stores[3];

        Assert.Null(medicine.Days);
        Assert.Equal("medicine", medicine.Name);
        Assert.False(medicine.Pressing);
    }

    [Fact]
    public void AStoreWithTwoDaysLeftIsPressing()
    {
        DojoState dojo = Quiet(purse: new Resources(Gold: 400, Food: 4, Water: 200, Medicine: 20));
        Resources draw = dojo.DailyDraw();

        StripStore rice = StripModel.Describe(dojo).Stores[1];

        Assert.True(rice.Days <= StripModel.Pressing);
        Assert.True(rice.Pressing);

        // And the figure itself is the stock, not the days — the days are what the name says.
        Assert.Equal("4", rice.Figure);
        Assert.True(draw.Food > 0);
    }

    [Fact]
    public void AFullStoreIsNotPressing()
    {
        StripStore water = StripModel.Describe(Quiet()).Stores[2];

        Assert.False(water.Pressing);
    }
}
