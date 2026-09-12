using Domina.Core.Dojo;

namespace Domina.Core.Campaign;

/// <summary>The three difficulty tiers (docs/GDD.md §10).</summary>
public enum DifficultyTier
{
    /// <summary>A softer road: the enemy is lighter and the work pays better.</summary>
    Apprentice,

    /// <summary>The balance's own tier — every number in the docs was measured here.</summary>
    Master,

    /// <summary>A harder road: the enemy is heavier and the same work pays less.</summary>
    Legend,
}

/// <summary>
/// What a tier does to the numbers.
/// </summary>
/// <remarks>
/// <para>
/// GDD §10's rule, kept exactly: <b>"Master" is the base of the balance measurement; the others are
/// derived by multipliers, not by a separate measurement run.</b> So there is no second set of tuning
/// here and no per-tier table — a tier is two multipliers laid over the measured numbers, and Master's
/// are both 1, which means every figure written down in the docs is still literally the figure the game
/// runs at Master.
/// </para>
/// <para>
/// The two axes are chosen to be the two the player actually feels: <b>how hard the enemy hits</b> and
/// <b>how far the money goes</b>. They pull in the same direction on purpose — an easier tier that only
/// weakened the enemy would make gold pile up and turn the dojo's decisions into formalities, which is
/// the part of the game the difficulty is not supposed to remove.
/// </para>
/// <para>
/// It is deliberately <b>not</b> applied to death, dismemberment or the seppuku threshold. A tier
/// should change how often the player is in trouble, not what trouble means: a run where limbs come off
/// less easily is a different game rather than an easier one.
/// </para>
/// </remarks>
/// <param name="Tier">Which tier this is.</param>
/// <param name="EnemyPower">The multiplier laid over the difficulty curve.</param>
/// <param name="Reward">The multiplier laid over what a fight pays.</param>
public sealed record Difficulty(DifficultyTier Tier, double EnemyPower, double Reward)
{
    /// <summary>The softer road.</summary>
    public static Difficulty Apprentice { get; } = new(DifficultyTier.Apprentice, 0.85, 1.15);

    /// <summary>The measured tier — both multipliers are 1.</summary>
    public static Difficulty Master { get; } = new(DifficultyTier.Master, 1, 1);

    /// <summary>The harder road.</summary>
    public static Difficulty Legend { get; } = new(DifficultyTier.Legend, 1.15, 0.85);

    /// <summary>The tier's numbers.</summary>
    public static Difficulty Of(DifficultyTier tier) => tier switch
    {
        DifficultyTier.Apprentice => Apprentice,
        DifficultyTier.Legend => Legend,
        _ => Master,
    };

    /// <summary>Lays the tier over the difficulty curve.</summary>
    /// <remarks>
    /// The ceiling moves with the curve. Left where it was, a harder tier would only reach the ceiling
    /// sooner and then flatten into the same late game — the tier would quietly stop existing exactly
    /// where the season gets hard.
    /// </remarks>
    public EncounterTuning Apply(EncounterTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);

        if (EnemyPower == 1)
        {
            return tuning;
        }

        return tuning with
        {
            StartingPower = tuning.StartingPower * EnemyPower,
            PowerPerDay = tuning.PowerPerDay * EnemyPower,
            MaxPower = tuning.MaxPower * EnemyPower,
        };
    }

    /// <summary>Lays the tier over what the work pays.</summary>
    /// <remarks>
    /// Only the <b>income</b> side is touched, never the prices. Moving both would be the same knob
    /// twice, and moving the prices instead would change what a suit of armour is worth relative to a
    /// warrior — a balance decision the tier has no business making.
    /// </remarks>
    public EconomyTuning Apply(EconomyTuning economy)
    {
        ArgumentNullException.ThrowIfNull(economy);

        if (Reward == 1)
        {
            return economy;
        }

        return economy with
        {
            VictoryGoldPerEnemyHealth = economy.VictoryGoldPerEnemyHealth * Reward,
        };
    }
}
