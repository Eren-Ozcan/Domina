namespace Domina.Core.Honor;

/// <summary>The honour and seppuku system's tunable numbers.</summary>
/// <remarks>
/// The balance values will settle in phase 9 (see docs/GDD.md → Open Decision #8).
/// </remarks>
public sealed record HonorTuning
{
    /// <summary>The lower bound of the reward multiplier (100% ronin).</summary>
    public double MinRewardMultiplier { get; init; } = 0.5;

    /// <summary>The upper bound of the reward multiplier (100% bushi).</summary>
    public double MaxRewardMultiplier { get; init; } = 1.5;

    /// <summary>
    /// The effect on honour of a reaction to a live fight. Together with the change from performance it
    /// makes up the "large effect" side.
    /// </summary>
    public double LiveVoteHonorSwing { get; init; } = 12;

    /// <summary>
    /// The effect of a command targeting a warrior outside a fight (<c>!ronin-&lt;name&gt;</c>).
    /// Deliberately small: otherwise a group could spam a warrior who never fought and drag him to
    /// seppuku.
    /// </summary>
    public double TargetedVoteHonorSwing { get; init; } = 0.4;

    /// <summary>The maximum effect of fight performance on honour.</summary>
    public double PerformanceHonorSwing { get; init; } = 10;

    /// <summary>
    /// The penalty added to the performance score of a warrior who comes back by fleeing (on a -1..+1 scale).
    /// </summary>
    /// <remarks>
    /// This stays <b>inside</b> the performance component: a warrior who fights well and then pulls out
    /// is still better than one who fights badly. The key's own price is separate, see
    /// <see cref="RetreatHonorPenalty"/>.
    /// </remarks>
    public double EscapePerformancePenalty { get; init; } = -0.6;

    /// <summary>
    /// The flat honour price of fleeing. Independent of performance, applied on every withdrawal.
    /// </summary>
    /// <remarks>
    /// The number here is a <b>placeholder</b> — the honour price of fleeing has not been decided yet
    /// (see docs/GDD.md → Open Decision #8). The reason it is a separate knob: the penalty inside
    /// performance mixed with the hit rate, and a warrior accurate enough could gain honour even though
    /// he fled. The price of pulling out should be visible independently of how well he
    /// fought.
    /// </remarks>
    public double RetreatHonorPenalty { get; init; } = 8;

    /// <summary>
    /// How much honour recovers toward neutral (50) per hour. It stops a momentary troll attack from
    /// turning into a permanent death sentence; only <b>sustained</b> dishonour leads to seppuku.
    /// </summary>
    public double DecayPerHourTowardNeutral { get; init; } = 6;

    /// <summary>A warrior who falls below this value enters the seppuku vote.</summary>
    /// <remarks>
    /// <b>Temporary number (2026-09-04):</b> 30 on a scale of 100, standing until playtesting
    /// (GDD §14 #8 open). The old 12 was stuck to the bottom of the scale: honour only got there through
    /// back-to-back disasters, so the vote almost never opened — chat's heaviest decision was in practice
    /// not in the game.
    /// </remarks>
    public double SeppukuThreshold { get; init; } = 30;

    /// <summary>A pardoned warrior's honour is pulled to this value — a little above the threshold.</summary>
    /// <remarks>
    /// It moves with the threshold: a pardoned warrior must not fall straight into a new vote, but the
    /// pardon must not carry him to neutral (<see cref="HonorScale.Starting"/>) either — a pardon is not
    /// an acquittal, the warrior gets up in debt.
    /// </remarks>
    public double PardonedHonor { get; init; } = 45;

    /// <summary>Oylama penceresi.</summary>
    public TimeSpan VoteWindow { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The immunity period of a pardoned warrior before he can enter a new vote.
    /// During it the vote is not triggered even if his honour falls below the threshold.
    /// </summary>
    public TimeSpan PardonImmunity { get; init; } = TimeSpan.FromMinutes(15);

    public static HonorTuning Default { get; } = new();
}
