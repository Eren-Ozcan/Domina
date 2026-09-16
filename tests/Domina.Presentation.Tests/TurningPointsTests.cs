using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// Where a term turned. The decisions protected: the days that cost the term are printed before the
/// days that only hurt, the sheet never prints more than it can be read at a glance, and a term that
/// went wrong nowhere says so rather than inventing a reason.
/// </summary>
public class TurningPointsTests
{
    private static DojoState Starving()
    {
        // A dojo with men, no store and no market money: the day's bill cannot be met, so somebody
        // goes hungry and the mark is written by the day loop itself.
        DojoState state = new(events: new EventTuning { ChancePerDay = 0 })
        {
            Purse = Resources.Empty,
        };

        for (int i = 0; i < 3; i++)
        {
            state.Roster.Recruit($"Kenji {i}", WarriorStats.Recruit());
        }

        return state;
    }

    [Fact]
    public void ATermThatWentWrongNowhereSaysNothing()
    {
        DojoState quiet = new(events: new EventTuning { ChancePerDay = 0 })
        {
            Purse = new Resources(Gold: 4000, Food: 600, Water: 600, Medicine: 40),
        };
        quiet.Roster.Recruit("Kenji", WarriorStats.Recruit());
        quiet.Decline();

        Assert.Empty(TurningPoints.Describe(quiet));
    }

    [Fact]
    public void TheDayTheStoreCouldNotFeedThemIsWrittenDown()
    {
        DojoState dojo = Starving();
        dojo.Decline();

        IReadOnlyList<TurningPoint> points = TurningPoints.Describe(dojo);

        Assert.NotEmpty(points);
        Assert.Contains(points, point => point.Line.Contains("could not feed", StringComparison.Ordinal));
        Assert.Contains(points, point => point.Grave);
    }

    [Fact]
    public void TheGravestDaysArePrintedFirst()
    {
        DojoState dojo = Starving();

        for (int i = 0; i < 20; i++)
        {
            dojo.Decline();
        }

        IReadOnlyList<TurningPoint> points = TurningPoints.Describe(dojo);

        Assert.True(points.Count <= TurningPoints.Most);
        Assert.True(points[0].Grave);
    }

    [Fact]
    public void ItReadsTheDaysTheCoreWroteDown()
    {
        DojoState dojo = Starving();
        dojo.Decline();

        Assert.Contains(dojo.Marks, mark => mark.Kind == TermMarkKind.WentHungry);
        Assert.Equal(1, dojo.Marks[0].Day);
    }
}
