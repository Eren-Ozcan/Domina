using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Honor;

/// <summary>
/// Computes how the honour score changes.
/// </summary>
/// <remarks>
/// It is stateless — it does not store the warrior's honour itself, it only returns the new value.
/// The persistent state is held by the meta layer.
/// </remarks>
public sealed class HonorEngine(HonorTuning? tuning = null)
{
    private readonly HonorTuning _tuning = tuning ?? HonorTuning.Default;

    public HonorTuning Tuning => _tuning;

    /// <summary>
    /// The effect of fight performance on honour.
    /// </summary>
    /// <remarks>
    /// "An honourable fight" here means: he attacked, he landed hits, he did not flee.
    /// Surviving by fleeing is sensible but earns no honour — and that is what makes the player's
    /// decision "shall I pull out" a real dilemma.
    /// </remarks>
    public double PerformanceDelta(WarriorBattleSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (summary.AttacksMade == 0)
        {
            // A fight that ends without a single attack is not worth watching.
            return -_tuning.PerformanceHonorSwing * 0.5;
        }

        // The hit rate (0-1) is moved to the -1..+1 range with 0.5 taken as the neutral point.
        double aggressionScore = (summary.Accuracy - 0.5) * 2;

        // A fight that ends in flight brings no honour.
        double escapePenalty = summary.Escaped ? _tuning.EscapePerformancePenalty : 0;

        double score = Math.Clamp(aggressionScore + escapePenalty, -1, 1);
        return score * _tuning.PerformanceHonorSwing;
    }

    /// <summary>
    /// The flat honour price of pulling out.
    /// </summary>
    /// <remarks>
    /// It is added to <see cref="PerformanceDelta"/>, it does not replace it: one is "how did he fight",
    /// this is "did he pull out". Because pulling out before the fight begins is no longer possible
    /// (see docs/GDD.md §5), this penalty is always the price of leaving a fight that has <b>begun</b>
    /// bedelidir.
    /// </remarks>
    public double RetreatDelta(WarriorBattleSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return summary.Escaped ? -_tuning.RetreatHonorPenalty : 0;
    }

    /// <summary>The effect on honour of a chat reaction to a live fight.</summary>
    public double LiveVoteDelta(CrowdVerdict verdict)
    {
        if (!verdict.HasVotes)
        {
            return 0;
        }

        // The ratio 0.5 is neutral; it is moved to the -1..+1 range.
        return (verdict.BushiRatio - 0.5) * 2 * _tuning.LiveVoteHonorSwing;
    }

    /// <summary>
    /// The effect of a command targeting a warrior outside a fight. Deliberately small.
    /// </summary>
    public double TargetedVoteDelta(bool isBushi) =>
        (isBushi ? 1 : -1) * _tuning.TargetedVoteHonorSwing;

    /// <summary>Recovery toward neutral over time.</summary>
    public double ApplyDecay(double honor, TimeSpan elapsed)
    {
        const double neutral = (HonorScale.Min + HonorScale.Max) / 2;

        double step = _tuning.DecayPerHourTowardNeutral * elapsed.TotalHours;
        double distance = neutral - honor;

        if (Math.Abs(distance) <= step)
        {
            return neutral;
        }

        return HonorScale.Clamp(honor + (Math.Sign(distance) * step));
    }

    public static double Apply(double honor, double delta) => HonorScale.Clamp(honor + delta);
}
