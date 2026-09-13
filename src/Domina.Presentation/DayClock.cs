using Domina.Core.Campaign;
using Domina.Core.Dojo;

namespace Domina.Presentation;

/// <summary>The speeds the dojo's clock runs at.</summary>
public enum ClockSpeed
{
    /// <summary>Stopped by the player. Nothing moves until he starts it again.</summary>
    Paused,

    /// <summary>The season's own pace.</summary>
    Normal,

    /// <summary>Twice the season's pace.</summary>
    Fast,

    /// <summary>Four times the season's pace — the speed a quiet week is spent at.</summary>
    Fastest,
}

/// <summary>
/// The clock that turns real seconds into the dojo's days.
/// </summary>
/// <remarks>
/// <para>
/// Build step 8 (docs/ROADMAP.md): time flows and can be stopped, and the day is no longer a button.
/// The <b>core is untouched</b> by it — <see cref="DojoState.AdvanceDay"/> is still the atomic unit and
/// every number measured against it still holds. What this class owns is only the pacing: how much
/// real time a day costs, what the player's speed control does to it, and when the flow has to stop.
/// It lives in the presentation layer for the same reason the other models do — the rule is testable
/// without an engine, and the engine only feeds it a frame delta.
/// </para>
/// <para>
/// <b>Holding is not pausing.</b> A hold is the game saying "not now" (the arena is open, a party is
/// being picked); a pause is the player saying it. They are kept apart so that releasing a hold gives
/// the player back the speed he had chosen, and so that a hold cannot be cleared by the pause button.
/// </para>
/// <para>
/// <b>Determinism is not at stake here.</b> The clock decides <i>when</i> a day closes, never what
/// happens in it: the day itself is resolved by the core from the season's seed, so the same save
/// gives the same season whether it was played at 1× or skipped through at 4×.
/// </para>
/// </remarks>
public sealed class DayClock
{
    /// <summary>
    /// How many day rollovers a single <see cref="Advance"/> may hand back.
    /// </summary>
    /// <remarks>
    /// The same guard the arena puts on its own accumulator: after a long frame (a load, a window
    /// dragged, a machine asleep) the leftover time must not close a week in one frame and skip the
    /// player past the interruptions that week held. The excess is dropped, not banked.
    /// </remarks>
    public const int MaxRolloversPerAdvance = 3;

    private readonly HashSet<string> _holds = [];
    private double _elapsed;
    private ClockSpeed _resume = ClockSpeed.Normal;

    /// <summary>How much real time one day costs at <see cref="ClockSpeed.Normal"/>.</summary>
    /// <remarks>
    /// <b>Not a measured number</b> — it cannot be: nothing in the sim has an opinion about how long a
    /// quiet day should feel. 60 s puts a 180-day season at three hours at 1× and about three quarters
    /// of an hour at 4×, before a single fight is watched. It is a playtest number and it is expected
    /// to move.
    /// </remarks>
    public double SecondsPerDay { get; init; } = 60;

    /// <summary>The speed the player has chosen.</summary>
    public ClockSpeed Speed { get; private set; } = ClockSpeed.Paused;

    /// <summary>Is something in the game holding the clock — the arena, a decision being made?</summary>
    public bool IsHeld => _holds.Count > 0;

    /// <summary>Is time actually moving?</summary>
    public bool IsRunning => Speed != ClockSpeed.Paused && !IsHeld;

    /// <summary>How far into the current day the clock stands, 0 to 1.</summary>
    public double Progress => SecondsPerDay <= 0 ? 0 : Math.Clamp(_elapsed / SecondsPerDay, 0, 1);

    /// <summary>What a speed does to the flow of time.</summary>
    public static double Multiplier(ClockSpeed speed) => speed switch
    {
        ClockSpeed.Normal => 1,
        ClockSpeed.Fast => 2,
        ClockSpeed.Fastest => 4,
        _ => 0,
    };

    /// <summary>The player picks a speed. Pausing remembers what he was running at.</summary>
    public void Set(ClockSpeed speed)
    {
        if (speed != ClockSpeed.Paused)
        {
            _resume = speed;
        }

        Speed = speed;
    }

    /// <summary>Stops the clock on the player's own key.</summary>
    public void Pause() => Speed = ClockSpeed.Paused;

    /// <summary>Starts it again at the speed he last ran at.</summary>
    public void Resume() => Speed = _resume;

    /// <summary>The pause key: stopped becomes running, running becomes stopped.</summary>
    public void Toggle()
    {
        if (Speed == ClockSpeed.Paused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    /// <summary>
    /// The game holds the clock for as long as <paramref name="reason"/> stands.
    /// </summary>
    /// <remarks>
    /// Holds are counted by name rather than by depth, so a screen that holds twice and releases once
    /// does not leave the clock frozen for the rest of the season.
    /// </remarks>
    public void Hold(string reason) => _holds.Add(reason);

    /// <summary>Lets one reason go. The clock runs again when the last of them is gone.</summary>
    public void Release(string reason) => _holds.Remove(reason);

    /// <summary>Is this particular reason holding the clock?</summary>
    public bool IsHeldBy(string reason) => _holds.Contains(reason);

    /// <summary>
    /// Feeds the clock a frame and says how many days have turned.
    /// </summary>
    /// <returns>
    /// The number of day rollovers the caller owes the core, capped at
    /// <see cref="MaxRolloversPerAdvance"/>.
    /// </returns>
    public int Advance(double delta)
    {
        if (!IsRunning || delta <= 0 || SecondsPerDay <= 0)
        {
            return 0;
        }

        _elapsed += delta * Multiplier(Speed);

        int rollovers = 0;
        while (_elapsed >= SecondsPerDay && rollovers < MaxRolloversPerAdvance)
        {
            _elapsed -= SecondsPerDay;
            rollovers++;
        }

        if (rollovers == MaxRolloversPerAdvance)
        {
            // Whatever is left over after the cap is thrown away rather than carried into the next
            // frame: a banked backlog would keep firing days for seconds after the machine caught up.
            _elapsed = 0;
        }

        return rollovers;
    }

    /// <summary>
    /// Puts the clock back to the start of a day without turning one.
    /// </summary>
    /// <remarks>
    /// Used when the <b>dojo</b> moved the day rather than the clock — an expedition eats a day, the
    /// player skips one. The morning would otherwise begin with whatever fraction was left over.
    /// </remarks>
    public void Restart() => _elapsed = 0;
}

/// <summary>
/// What in a closed day is worth stopping the clock for.
/// </summary>
/// <remarks>
/// <para>
/// GDD §10: events land at the turn of the day, they do not pop up inside the flow. That settles
/// <i>when</i> the player is told; this settles <i>whether the flow stops while he reads it</i>. The
/// rule is deliberately narrow — a clock that stops every morning is the button-driven day with extra
/// steps, and a clock that never stops loses a rival's move in the log.
/// </para>
/// <para>
/// So: it stops for what the player would have to <b>answer</b> — a happening, a verdict, a move on the
/// map, a raid he did not answer, a payroll that emptied, men who went hungry, a promise broken, the
/// season changing gear. It does not stop for a building finished, a man out of the infirmary or a
/// drill done; those are read in the log at whatever speed he is running.
/// </para>
/// </remarks>
public static class DayInterrupt
{
    /// <summary>Does this day's close stop the clock?</summary>
    public static bool Demands(DayReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return report.Event is not null
            || report.Tribunal is not null
            || report.RivalMove is not null
            || report.Sacked is not null
            || report.MissedWeek
            || report.BountyBroken
            || report.Phase != SeasonPhase.Running
            || !report.Upkeep.Fed
            || (report.Upkeep.Walked?.Count ?? 0) > 0;
    }
}
