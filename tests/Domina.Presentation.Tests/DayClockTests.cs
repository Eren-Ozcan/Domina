using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;

namespace Domina.Presentation.Tests;

/// <summary>
/// The clock of build step 8: real seconds in, day rollovers out. What is protected here is that the
/// pacing layer stays a pacing layer — it never decides what a day contains, only when one turns.
/// </summary>
public class DayClockTests
{
    private static DayClock Running(double secondsPerDay = 10)
    {
        DayClock clock = new() { SecondsPerDay = secondsPerDay };
        clock.Set(ClockSpeed.Normal);
        return clock;
    }

    /// <summary>A new clock is stopped: a season does not start running before the player looks at it.</summary>
    [Fact]
    public void TheClockStartsStopped()
    {
        DayClock clock = new();

        Assert.Equal(ClockSpeed.Paused, clock.Speed);
        Assert.False(clock.IsRunning);
        Assert.Equal(0, clock.Advance(1000));
    }

    [Fact]
    public void ADayTurnsWhenItsSecondsAreSpent()
    {
        DayClock clock = Running(secondsPerDay: 10);

        Assert.Equal(0, clock.Advance(9));
        Assert.Equal(0.9, clock.Progress, 3);
        Assert.Equal(1, clock.Advance(1.5));
        Assert.Equal(0.05, clock.Progress, 3);
    }

    /// <summary>The speed control buys days, not different days.</summary>
    [Theory]
    [InlineData(ClockSpeed.Normal, 1)]
    [InlineData(ClockSpeed.Fast, 2)]
    [InlineData(ClockSpeed.Fastest, 3)]
    public void SpeedBuysDaysPerTenSeconds(ClockSpeed speed, int expected)
    {
        DayClock clock = new() { SecondsPerDay = 10 };
        clock.Set(speed);

        // 4× would buy four days out of ten seconds; the per-frame cap hands back three.
        Assert.Equal(expected, clock.Advance(10));
    }

    /// <summary>
    /// A frame the machine slept through does not close a week: the cap stops it and the leftover is
    /// dropped rather than banked into the frames after it.
    /// </summary>
    [Fact]
    public void ALongFrameCannotSkipAWeek()
    {
        DayClock clock = Running(secondsPerDay: 10);

        Assert.Equal(DayClock.MaxRolloversPerAdvance, clock.Advance(300));
        Assert.Equal(0, clock.Progress, 3);
        Assert.Equal(0, clock.Advance(1));
    }

    /// <summary>A hold is the game's, a pause is the player's — and the two do not overwrite each other.</summary>
    [Fact]
    public void AHoldStopsTheClockWithoutTakingTheChosenSpeed()
    {
        DayClock clock = new() { SecondsPerDay = 10 };
        clock.Set(ClockSpeed.Fast);
        clock.Hold("arena");

        Assert.True(clock.IsHeld);
        Assert.False(clock.IsRunning);
        Assert.Equal(ClockSpeed.Fast, clock.Speed);
        Assert.Equal(0, clock.Advance(100));

        clock.Release("arena");

        Assert.True(clock.IsRunning);
        Assert.Equal(2, clock.Advance(10));
    }

    /// <summary>Holds are named, so releasing one does not release another's.</summary>
    [Fact]
    public void EveryHoldHasToBeReleased()
    {
        DayClock clock = Running();
        clock.Hold("arena");
        clock.Hold("party");
        clock.Hold("party");

        clock.Release("party");

        Assert.True(clock.IsHeld);
        Assert.True(clock.IsHeldBy("arena"));

        clock.Release("arena");

        Assert.False(clock.IsHeld);
    }

    /// <summary>The pause key gives back the speed it took.</summary>
    [Fact]
    public void ResumingComesBackAtTheSpeedThePlayerChose()
    {
        DayClock clock = new();
        clock.Set(ClockSpeed.Fastest);
        clock.Toggle();

        Assert.Equal(ClockSpeed.Paused, clock.Speed);

        clock.Toggle();

        Assert.Equal(ClockSpeed.Fastest, clock.Speed);
    }

    /// <summary>
    /// When the dojo moves the day itself — an expedition eats one — the clock starts the morning at
    /// the morning, not at whatever fraction was left on it.
    /// </summary>
    [Fact]
    public void RestartingDropsTheUnfinishedDay()
    {
        DayClock clock = Running(secondsPerDay: 10);
        clock.Advance(7);

        clock.Restart();

        Assert.Equal(0, clock.Progress, 3);
    }
}

/// <summary>
/// Which closed days stop the clock. The rule is narrow on purpose: stopping every morning would be
/// the button-driven day again, stopping for nothing would lose a rival's move in the log.
/// </summary>
public class DayInterruptTests
{
    private static DayReport Quiet(
        DayEvent? happening = null,
        bool missedWeek = false,
        SeasonPhase phase = SeasonPhase.Running,
        IReadOnlySet<WarriorId>? hungry = null,
        IReadOnlyList<SchoolNodeId>? opened = null) =>
        new(
            Day: 4,
            Recovered: [],
            Trained: [],
            Upkeep: new UpkeepReport(
                GoldSpent: 10,
                Food: 3,
                Water: 3,
                Medicine: 0,
                Hungry: hungry ?? new HashSet<WarriorId>(),
                Medicated: new HashSet<WarriorId>()),
            Event: happening,
            Opened: opened,
            MissedWeek: missedWeek,
            Phase: phase);

    [Fact]
    public void AnOrdinaryDayDoesNotStopTheClock()
    {
        Assert.False(DayInterrupt.Demands(Quiet()));
    }

    /// <summary>A building finished is news, not a decision: it is read at whatever speed he is running.</summary>
    [Fact]
    public void FinishedWorkIsNotAnInterruption()
    {
        Assert.False(DayInterrupt.Demands(Quiet(opened: [SchoolNodeId.Infirmary])));
    }

    [Fact]
    public void AHungryRosterStopsTheClock()
    {
        Assert.True(DayInterrupt.Demands(Quiet(hungry: new HashSet<WarriorId> { new(1) })));
    }

    [Fact]
    public void AMissedWeekStopsTheClock()
    {
        Assert.True(DayInterrupt.Demands(Quiet(missedWeek: true)));
    }

    /// <summary>The last night cannot be run past at 4×.</summary>
    [Fact]
    public void TheSeasonChangingGearStopsTheClock()
    {
        Assert.True(DayInterrupt.Demands(Quiet(phase: SeasonPhase.FinalNight)));
    }
}
