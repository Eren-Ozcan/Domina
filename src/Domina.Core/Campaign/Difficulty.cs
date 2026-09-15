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
/// <param name="RivalShare">
/// The share of the province the rival already holds on day 1. It is the tier's third dial and the
/// cheapest one: the map is the season's slowest pressure, so how far behind the player opens says
/// more about the run than either multiplier does.
/// </param>
public sealed record Difficulty(
    DifficultyTier Tier,
    double EnemyPower,
    double Reward,
    double RivalShare = 0.5)
{
    /// <summary>The softer road.</summary>
    /// <remarks>
    /// Re-measured on the 0.0072 curve (2026-09-12, 4000 dojos x 180 days). The first numbers —
    /// ±0.15 on both axes and a third of the map — were read off a curve calibrated for a 60-day
    /// season, and on the real one they were not a softer road but a different game: the tier won
    /// the last night 22.6% of the time against Master's 8.8%.
    /// <para>
    /// <b>Confirmed on the current curve (2026-09-13, six seeds x 1600 dojos):</b> 22.8% of last
    /// nights won against Master's 13.7%, 21.2% of dojos closed against 28.3%, net +41.3 against
    /// +31.4. A softer road on the same map, not a different game — the multipliers stand.
    /// </para>
    /// <para>
    /// <b>Swept a third time (2026-09-15, three seeds x 1600 dojos on the rich bed) after the curve
    /// went back to 0.011 and the spoils round changed what a fight pays:</b> 29.1% of last nights won
    /// against Master's 20.2%, 20.4% of dojos closed against 28.4%, net +46.5 against +37.0, deaths
    /// 2.9% against 3.8% per warrior-fight. The gap narrowed with the richer season (1.44x Master's
    /// night, against 1.66x on the previous curve) but every column still separates cleanly, so the
    /// multipliers were not touched.
    /// </para>
    /// </remarks>
    public static Difficulty Apprentice { get; } = new(DifficultyTier.Apprentice, 0.93, 1.07, 0.42);

    /// <summary>The measured tier — both multipliers are 1.</summary>
    public static Difficulty Master { get; } = new(DifficultyTier.Master, 1, 1, 0.5);

    /// <summary>The harder road.</summary>
    /// <remarks>
    /// The same re-measurement, and this is the end that was broken: at ±0.15 the net per fight went
    /// negative (-2.9 gold), the best man ended the season at 1.6% mastery and the last night was won
    /// 0.2% of the time — 44x less often than at Master. That is the season's second half removed,
    /// not a harder road. At 1.07 / 0.93 / 0.58 the night is won 2.8% of the time on a positive net,
    /// which is a tier the player can still play.
    /// <para>
    /// <b>Re-measured on the current curve (2026-09-13, six seeds x 1600 dojos), and this end came
    /// up:</b> the binding stamina pool (GDD §5) pays the roster that trained, which lifts the hard
    /// tier's floor — 5.2% of last nights won, 35.5% of dojos closed, net <b>+19.0</b> a fight and a
    /// best man who ends the season at 10.3% mastery. A road that can be walked to the end, which is
    /// what this tier failed to be before. The multipliers were not touched.
    /// </para>
    /// <para>
    /// <b>The same third sweep (2026-09-15):</b> 9.2% of last nights won, 36.6% of dojos closed, net
    /// <b>+22.4</b> a fight, deaths 5.2% per warrior-fight and a best man at 15.0% mastery. The floor
    /// rose again with the season's new income, and the tier still sits at less than half Master's
    /// night on nearly a third more closures. Unchanged.
    /// </para>
    /// </remarks>
    public static Difficulty Legend { get; } = new(DifficultyTier.Legend, 1.07, 0.93, 0.58);

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
