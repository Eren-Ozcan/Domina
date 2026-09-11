using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Core.Honor;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The dojo's seppuku tribunal (docs/GDD.md §6) — the half that gives the honour system teeth. The
/// decisions protected: only sustained dishonour reaches it, one man stands at a time, the crowd has a
/// day to speak, a pardon is a debt rather than an acquittal, and a reload is not a way out.
/// </summary>
public class TribunalTests
{
    private static DojoState Quiet(HonorTuning? honor = null, int men = 2)
    {
        DojoState state = new(events: new EventTuning { ChancePerDay = 0 }, honor: honor)
        {
            Resources = new Resources(Gold: 4000, Food: 600, Water: 600, Medicine: 40),
        };

        for (int i = 0; i < men; i++)
        {
            state.Roster.Recruit($"Kenji {i}", WarriorStats.Recruit());
        }

        return state;
    }

    private static RosterEntry Disgrace(DojoState state, int index = 0)
    {
        RosterEntry entry = state.Roster.Living.ElementAt(index);
        entry.Warrior.Honor = 5;
        return entry;
    }

    /// <summary>A warrior above the threshold is never called.</summary>
    [Fact]
    public void AnHonourableWarriorIsNeverSummoned()
    {
        DojoState state = Quiet();

        state.AdvanceDay();

        Assert.Null(state.Tribunal.Standing);
        Assert.Empty(state.Tribunal.Queue);
    }

    /// <summary>Falling through the threshold summons him — and the verdict lands the next day.</summary>
    /// <remarks>
    /// The day in between is the crowd's day. Without it a warrior would be summoned and executed in the
    /// same closing, and on a silent dojo the player would never see it coming.
    /// </remarks>
    [Fact]
    public void TheSummonsStandsForADayBeforeTheVerdict()
    {
        DojoState state = Quiet();
        RosterEntry doomed = Disgrace(state);

        DayReport first = state.AdvanceDay();
        Assert.Null(first.Tribunal);
        Assert.Equal(doomed.Id, state.Tribunal.Standing?.Warrior);

        DayReport second = state.AdvanceDay();
        Assert.NotNull(second.Tribunal);
        Assert.Equal(doomed.Id, second.Tribunal.Warrior);
    }

    /// <summary>The sword is real: a condemned warrior dies, permanently, through the roster.</summary>
    [Fact]
    public void ACondemnedWarriorDies()
    {
        DojoState state = Quiet(men: 2);
        RosterEntry doomed = Disgrace(state);

        state.AdvanceDay();
        DayReport report = state.AdvanceDay();
        while (report.Tribunal is null)
        {
            report = state.AdvanceDay();
        }

        if (report.Tribunal.Outcome == SeppukuOutcome.Seppuku)
        {
            Assert.False(doomed.Warrior.IsAlive);
            Assert.DoesNotContain(state.Roster.Living, e => e.Id == doomed.Id);
        }
        else
        {
            // A pardon is not an acquittal: he gets up a little above the threshold, in debt.
            Assert.True(doomed.Warrior.IsAlive);
            Assert.Equal(HonorTuning.Default.PardonedHonor, doomed.Warrior.Honor, 9);
        }
    }

    /// <summary>When the crowd speaks, the crowd decides — the artificial one does not get a turn.</summary>
    [Fact]
    public void AVoicedCrowdOverridesTheArtificialOne()
    {
        DojoState state = Quiet();
        RosterEntry doomed = Disgrace(state);

        state.AdvanceDay();
        Assert.True(state.Tribunal.Vote("viewer-1", bushi: true));
        Assert.False(state.Tribunal.Vote("viewer-1", bushi: false));

        DayReport report = state.AdvanceDay();

        Assert.NotNull(report.Tribunal);
        Assert.False(report.Tribunal.DecidedByAudience);
        Assert.Equal(SeppukuOutcome.Pardoned, report.Tribunal.Outcome);
        Assert.True(doomed.Warrior.IsAlive);
    }

    /// <summary>A pardoned man cannot be dragged back the next morning.</summary>
    [Fact]
    public void APardonBuysHimDays()
    {
        DojoState state = Quiet();
        RosterEntry doomed = Disgrace(state);

        state.AdvanceDay();
        state.Tribunal.Vote("viewer-1", bushi: true);
        state.AdvanceDay();

        // Straight back into disgrace — and the tribunal still will not take him.
        doomed.Warrior.Honor = 1;
        state.AdvanceDay();

        Assert.True(state.Tribunal.IsImmune(doomed.Id, state.Day));
        Assert.Null(state.Tribunal.Standing);
        Assert.True(doomed.Warrior.IsAlive);
    }

    /// <summary>Two dishonoured men are tried one at a time.</summary>
    [Fact]
    public void OnlyOneManStandsAtATime()
    {
        DojoState state = Quiet(men: 2);
        Disgrace(state, 0);
        Disgrace(state, 1);

        state.AdvanceDay();

        Assert.NotNull(state.Tribunal.Standing);
        Assert.Single(state.Tribunal.Queue);
    }

    /// <summary>A man who died in the day's fight is not tried the next morning.</summary>
    [Fact]
    public void TheDeadAreNotTried()
    {
        DojoState state = Quiet(men: 2);
        RosterEntry doomed = Disgrace(state);

        state.AdvanceDay();
        Assert.Equal(doomed.Id, state.Tribunal.Standing?.Warrior);

        state.Roster.Kill(doomed.Id);
        DayReport report = state.AdvanceDay();

        Assert.Null(report.Tribunal);
        Assert.Null(state.Tribunal.Standing);
    }

    /// <summary>A man released before the verdict is out of the tribunal's reach.</summary>
    [Fact]
    public void AReleasedManIsOutOfReach()
    {
        DojoState state = Quiet(men: 2);
        RosterEntry doomed = Disgrace(state);

        state.AdvanceDay();
        Assert.True(state.Release(doomed.Id));

        DayReport report = state.AdvanceDay();

        Assert.Null(report.Tribunal);
        Assert.True(doomed.Warrior.IsAlive);
    }

    /// <summary>The same seed and the same day give the same verdict — a reload is not a reroll.</summary>
    [Fact]
    public void TheVerdictSurvivesASaveAndCannotBeRerolled()
    {
        DojoState before = Quiet();
        Disgrace(before);
        before.AdvanceDay();

        DojoState reloaded = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.Equal(before.Tribunal.Standing?.Warrior, reloaded.Tribunal.Standing?.Warrior);
        Assert.Equal(before.Tribunal.Standing?.OpenedDay, reloaded.Tribunal.Standing?.OpenedDay);

        DayReport straight = before.AdvanceDay();
        DayReport afterReload = reloaded.AdvanceDay();

        Assert.NotNull(straight.Tribunal);
        Assert.NotNull(afterReload.Tribunal);
        Assert.Equal(straight.Tribunal.Outcome, afterReload.Tribunal.Outcome);
    }

    /// <summary>The queue and the pardons go into the save; the crowd's voices do not.</summary>
    [Fact]
    public void TheBooksAreSavedAndTheVoicesAreNot()
    {
        DojoState before = Quiet(men: 2);
        Disgrace(before, 0);
        Disgrace(before, 1);
        before.AdvanceDay();
        before.Tribunal.Vote("viewer-1", bushi: true);

        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.Single(after.Tribunal.Queue);
        Assert.False(after.Tribunal.Standing!.Tally.HasVotes);
    }
}
