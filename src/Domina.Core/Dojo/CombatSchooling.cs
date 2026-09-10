using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>
/// What a fight teaches the warrior who came out of it.
/// </summary>
/// <remarks>
/// <para>
/// Until this rule existed the only road to growth was training, so going on an expedition was pure
/// loss: the warrior spent a day, took wounds and wore his armour out, and came back exactly the man
/// he left as. The decision round reversed the tempo — <b>a fight grows him, and faster than a drill
/// does</b> (docs/COMPARISON-DOMINA.md, section 3). The compulsory fight of the season stops being a
/// punishment and becomes an opportunity.
/// </para>
/// <para>
/// The lesson is not chosen by the player but <b>by the fight</b>: a warrior who spent the fight
/// swinging learns to strike, one who spent it blocking learns his guard. The shape is the drill's own
/// shape — one primary and one secondary stat — so the two roads of growth are directly comparable and
/// a single number (<see cref="TrainingTuning.FightGapClosed"/> against
/// <see cref="TrainingTuning.GapClosedPerDay"/>) says how much faster the risky road is.
/// </para>
/// <para>
/// Pure and stateless, like <see cref="TrainingGround"/>: no die is rolled here either. The fight's own
/// randomness is already in the counters the lesson is read from.
/// </para>
/// </remarks>
public static class CombatSchooling
{
    /// <summary>
    /// The drill the fight amounted to — <c>null</c> if it taught nothing.
    /// </summary>
    /// <remarks>
    /// The four counters are read as they are, not scaled: every one of them counts <b>events</b> (a
    /// swing, a block, a dodge, a blow taken), so they are already on the same scale. A warrior who did
    /// none of the four — he was pulled out before contact, or never reached the enemy — learns nothing:
    /// the rule pays for what the fight actually put him through, not for having been on the field.
    /// </remarks>
    public static Drill? LessonOf(WarriorBattleSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        // The order is the tie-break, and it is deliberate: with everything equal the fight is read as a
        // striking fight, because the swing is the one act a warrior always chooses himself.
        (Drill Drill, int Count)[] axes =
        [
            (Drill.Strikes, summary.AttacksMade),
            (Drill.Guard, summary.BlocksPerformed),
            (Drill.Footwork, summary.DodgesPerformed),
            (Drill.Conditioning, summary.TimesHit),
        ];

        Drill pick = Drill.Strikes;
        int best = 0;

        foreach ((Drill drill, int count) in axes)
        {
            if (count > best)
            {
                best = count;
                pick = drill;
            }
        }

        return best > 0 ? pick : null;
    }

    /// <summary>The stats after the fight's lesson; unchanged if it taught nothing.</summary>
    /// <param name="stats">The raw stats with no disability applied — the lesson writes these.</param>
    /// <param name="summary">The warrior's books from the fight.</param>
    /// <param name="talent">The warrior's talent share; it multiplies the gain, as in training.</param>
    /// <param name="tuning">The growth numbers, shared with training.</param>
    public static WarriorStats After(
        WarriorStats stats,
        WarriorBattleSummary summary,
        double talent,
        TrainingTuning? tuning = null)
    {
        TrainingTuning t = tuning ?? new TrainingTuning();

        return LessonOf(summary) is not Drill lesson
            ? stats
            : TrainingGround.After(
                stats,
                lesson,
                talent,
                t with { GapClosedPerDay = t.FightGapClosed });
    }
}
