namespace Domina.Core.Honor;

/// <summary>The tally of chat's reaction to a fight.</summary>
/// <param name="Bushi">Those who say "he fought like a real warrior".</param>
/// <param name="Ronin">"Onursuz" diyenler.</param>
/// <remarks>
/// <para>
/// A <b>ratio</b> is used, not the raw count. Three ronin in a chat of 5 and three ronin in a chat of
/// 5000 are not the same thing; a ratio keeps small and large streams fair to each other.
/// </para>
/// <para>
/// The multiplier having a lower and an upper bound also stops spam from pulling the result to the extremes
/// (bkz. docs/GDD.md §6).
/// </para>
/// </remarks>
public readonly record struct CrowdVerdict(int Bushi, int Ronin)
{
    public static CrowdVerdict Silent => new(0, 0);

    public int Total => Bushi + Ronin;

    /// <summary>Even a single vote counts as a real vote — there is no minimum turnout threshold.</summary>
    public bool HasVotes => Total > 0;

    /// <summary>With no votes it is neutral (0.5).</summary>
    public double BushiRatio => Total == 0 ? 0.5 : (double)Bushi / Total;

    /// <summary>Is the majority of chat in favour of pardoning?</summary>
    public bool FavorsMercy => Bushi > Ronin;

    /// <summary>The gold multiplier to apply to the fight reward.</summary>
    public double RewardMultiplier(HonorTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);

        double span = tuning.MaxRewardMultiplier - tuning.MinRewardMultiplier;
        return Math.Clamp(
            tuning.MinRewardMultiplier + (BushiRatio * span),
            tuning.MinRewardMultiplier,
            tuning.MaxRewardMultiplier);
    }

    public CrowdVerdict WithBushi(int count = 1) => this with { Bushi = Bushi + count };

    public CrowdVerdict WithRonin(int count = 1) => this with { Ronin = Ronin + count };
}
