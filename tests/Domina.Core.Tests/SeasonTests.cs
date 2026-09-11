using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The season skeleton (docs/GDD.md §10). The decisions protected: the season is a fixed 180-day
/// countdown, a week with no fight filed costs the roster honour and not gold, three heads open the
/// last night and nothing else does, and the run ends — in triumph, in defeat, or with the gate shut.
/// </summary>
public class SeasonTests
{
    private static DojoState Quiet(SeasonTuning? season = null) =>
        new(events: new EventTuning { ChancePerDay = 0 }, season: season)
        {
            Resources = new Resources(Gold: 5000, Food: 900, Water: 900, Medicine: 60),
        };

    private static DojoState WithRoster(SeasonTuning? season = null, int men = 2)
    {
        DojoState state = Quiet(season);
        for (int i = 0; i < men; i++)
        {
            state.Roster.Recruit($"Kenji {i}");
        }

        return state;
    }

    private static void RunTo(DojoState state, int day)
    {
        while (state.Day < day)
        {
            state.AdvanceDay();
        }
    }

    /// <summary>The first quiet week is free; the second is paid for, by everyone.</summary>
    /// <remarks>
    /// The penalty is the rule that closes the endless-training exploit, and it is honour rather than
    /// gold because a rich dojo would simply buy the safe loop. It lands on the whole roster for the
    /// same reason a broken promise does — the week is the dojo's, not one warrior's.
    /// </remarks>
    [Fact]
    public void TheSecondQuietWeekInARowCostsTheWholeRosterHonour()
    {
        DojoState state = WithRoster();

        DayReport first = null!;
        for (int i = 0; i < 7; i++)
        {
            first = state.AdvanceDay();
        }

        Assert.True(first.MissedWeek);
        Assert.Equal(0, first.HonorLost);

        double before = state.Roster.Living.First().Warrior.Honor;
        DayReport second = null!;
        for (int i = 0; i < 7; i++)
        {
            second = state.AdvanceDay();
        }

        Assert.True(second.MissedWeek);
        Assert.True(second.HonorLost > 0);
        Assert.Equal(2, state.Season.MissedWeeks);
        Assert.All(state.Roster.Living, e => Assert.True(e.Warrior.Honor < before));
    }

    /// <summary>A roster lying in the infirmary is not hiding, and is not charged for the quiet week.</summary>
    /// <remarks>
    /// Measured: without this test of who was standing, the ordinary dojo paid for 12.3 of its 13.4
    /// quiet weeks — its quiet weeks <b>are</b> the weeks its party is in bed. The penalty is aimed at
    /// the dojo that chooses not to take the field, not at the one that cannot.
    /// </remarks>
    [Fact]
    public void AWeekWithNobodyFitToSendIsNotCharged()
    {
        DojoState state = WithRoster();
        foreach (RosterEntry entry in state.Roster.Living)
        {
            entry.Injure(400);
        }

        DayReport report = null!;
        for (int i = 0; i < 14; i++)
        {
            report = state.AdvanceDay();
        }

        Assert.True(report.MissedWeek);
        Assert.Equal(0, report.HonorLost);
        Assert.Equal(0, state.Season.MissedStreak);
    }

    /// <summary>A fight filed inside the week answers the tick.</summary>
    [Fact]
    public void AFightFiledInsideTheWeekAnswersTheTick()
    {
        DojoState state = WithRoster();
        RunTo(state, 5);

        state.Season.RecordFight(state.Day, victory: true);
        RunTo(state, 8);

        Assert.Equal(0, state.Season.MissedWeeks);
    }

    /// <summary>A fight is filed by taking the field, and it is filed on the day the field was taken.</summary>
    /// <remarks>
    /// The expedition layer files it, not the resolver: a fight simulated for measurement with no dojo
    /// behind it must not answer a week it was never part of.
    /// </remarks>
    [Fact]
    public void AnExpeditionFilesTheDayItTookTheField()
    {
        DojoState state = WithRoster();
        RunTo(state, 4);

        List<RosterEntry> party = [.. state.Roster.FitForCampaign];
        new Expedition().Send(state, state.Offer, party, new SeededRandom(7));

        Assert.Equal(4, state.Season.LastFightDay);
        Assert.Equal(1, state.Season.Battles);
        Assert.Equal(5, state.Day);
    }

    /// <summary>The weekly counter is one clock: the fight's week and the rival's move share it.</summary>
    [Fact]
    public void TheTickCounterCountsDownToTheSameDayAllWeek()
    {
        DojoState state = WithRoster();

        Assert.Equal(6, state.Season.DaysToTick(state.Day));
        RunTo(state, 7);
        Assert.Equal(7, state.Season.DaysToTick(state.Day));
        Assert.Equal(174, state.Season.DaysLeft(state.Day));
    }

    /// <summary>Three heads open the gate; two leave it shut.</summary>
    [Fact]
    public void TheGateOpensOnTheThirdHeadAndNotBefore()
    {
        Season season = new();

        season.RecordHead();
        season.RecordHead();
        Assert.False(season.GateOpen);

        season.RecordHead();
        Assert.True(season.GateOpen);
    }

    /// <summary>
    /// The last day with the gate open hands the run to the last night; with the gate shut it closes the dojo.
    /// </summary>
    /// <remarks>
    /// The gate is read on the last day and nowhere else: three heads brought in on day 12 are worth
    /// exactly what three brought in on day 179 are — what it asks is whether the season did the work.
    /// </remarks>
    [Fact]
    public void TheLastDayReadsTheGate()
    {
        SeasonTuning tuning = new() { Days = 9 };

        DojoState shut = WithRoster(tuning);
        RunTo(shut, 9);
        shut.AdvanceDay();
        Assert.Equal(SeasonPhase.Closed, shut.Season.Phase);

        DojoState open = WithRoster(tuning);
        open.Season.RecordHead();
        open.Season.RecordHead();
        open.Season.RecordHead();
        RunTo(open, 9);
        open.AdvanceDay();
        Assert.Equal(SeasonPhase.FinalNight, open.Season.Phase);
    }

    /// <summary>A roster that runs out closes the dojo the same day, whatever day it is.</summary>
    [Fact]
    public void AnEmptyRosterClosesTheDojoOnTheSpot()
    {
        DojoState state = WithRoster(men: 1);
        state.Roster.Kill(state.Roster.Living.First().Id);

        state.AdvanceDay();

        Assert.Equal(SeasonPhase.Closed, state.Season.Phase);
        Assert.True(state.Season.IsOver);
    }

    /// <summary>A closed season stops counting: no further day moves it.</summary>
    [Fact]
    public void AClosedSeasonDoesNotKeepPayingForWeeks()
    {
        DojoState state = WithRoster(men: 1);
        state.Roster.Kill(state.Roster.Living.First().Id);
        state.AdvanceDay();

        int missed = state.Season.MissedWeeks;
        RunTo(state, state.Day + 14);

        Assert.Equal(missed, state.Season.MissedWeeks);
        Assert.Equal(SeasonPhase.Closed, state.Season.Phase);
    }

    /// <summary>A released man walks out alive: he is off the roster and on the closing screen.</summary>
    /// <remarks>
    /// The freed are the season's other score — the dojo is a sentence being served, so the men who
    /// leave it count on the opposite side from the dead.
    /// </remarks>
    [Fact]
    public void AReleasedManLeavesTheRosterAliveAndIsCountedFreed()
    {
        DojoState state = WithRoster(men: 2);
        RosterEntry leaving = state.Roster.Living.First();

        Assert.True(state.Release(leaving.Id));

        Assert.DoesNotContain(state.Roster.Living, e => e.Id == leaving.Id);
        Assert.True(leaving.Warrior.IsAlive);
        Assert.Contains(leaving.Name, state.Summarise().Freed);
        Assert.False(state.Release(leaving.Id));
    }

    /// <summary>A wounded man is not released — that door would buy the upkeep rule off.</summary>
    [Fact]
    public void AManInTheInfirmaryCannotBeReleased()
    {
        DojoState state = WithRoster(men: 2);
        RosterEntry hurt = state.Roster.Living.First();
        hurt.Injure(4);

        Assert.False(state.Release(hurt.Id));
        Assert.Contains(state.Roster.Living, e => e.Id == hurt.Id);
    }

    /// <summary>Everyone still standing at the end walked out too; the dead are counted apart.</summary>
    [Fact]
    public void TheClosingScreenCountsTheDeadAndTheFreedApart()
    {
        DojoState state = WithRoster(men: 3);
        RosterEntry fallen = state.Roster.Living.First();
        state.Roster.Kill(fallen.Id);

        SeasonSummary summary = state.Summarise();

        Assert.Equal([fallen.Name], summary.Dead);
        Assert.Equal(2, summary.Freed.Count);
    }

    /// <summary>The season's books survive a reload — the week cannot be laundered by loading a save.</summary>
    [Fact]
    public void TheSeasonSurvivesASaveAndLoad()
    {
        DojoState before = WithRoster();
        RunTo(before, 5);
        before.Season.RecordFight(before.Day, victory: true, dead: 1);
        before.Season.RecordHead();
        RosterEntry freed = before.Roster.Living.First();
        before.Release(freed.Id);
        RunTo(before, 9);

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.Equal(before.Season.LastFightDay, after.Season.LastFightDay);
        Assert.Equal(before.Season.MissedWeeks, after.Season.MissedWeeks);
        Assert.Equal(before.Season.HeadsTaken, after.Season.HeadsTaken);
        Assert.Equal(before.Season.Battles, after.Season.Battles);
        Assert.Equal(before.Season.Dead, after.Season.Dead);
        Assert.Contains(after.Roster.Released, e => e.Name == freed.Name);
        Assert.DoesNotContain(after.Roster.Living, e => e.Name == freed.Name);
    }

    /// <summary>An old save with no season block loads into a season that has not started.</summary>
    /// <remarks>
    /// Merge-on-load's rule (GDD §2): a missing field is a default, not a failure.
    /// </remarks>
    [Fact]
    public void ASaveWithNoSeasonBlockLoadsCleanly()
    {
        DojoState before = WithRoster();
        DojoSnapshot stripped = DojoSaveFile.Capture(before) with { Season = null };

        DojoState after = DojoSaveFile.Restore(stripped).State!;

        Assert.Equal(SeasonPhase.Running, after.Season.Phase);
        Assert.Equal(0, after.Season.HeadsTaken);
    }
}
