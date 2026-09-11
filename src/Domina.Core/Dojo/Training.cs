using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>The subject of a training day.</summary>
/// <remarks>
/// <para>
/// The four drills cover the eight stats <b>exactly</b>: no stat falls outside training, and none is
/// fed by two drills. Because a day eats a single job (GDD §10), choosing a drill is a real decision —
/// what you shape the roster around comes out of it.
/// </para>
/// <para>
/// Every drill has a <b>primary</b> and a <b>secondary</b> stat; the secondary takes half the share
/// (<see cref="TrainingTuning.SecondaryShare"/>). Training a single stat would push a warrior toward a
/// flat profile over eight days of eight separate drills; the secondary share gives the drills shape —
/// a warrior drilled on striking becomes both accurate and aggressive.
/// </para>
/// </remarks>
public enum Drill
{
    /// <summary>Strike drill — Accuracy, secondary Aggression.</summary>
    Strikes,

    /// <summary>Guard drill — Defence, secondary Strength.</summary>
    Guard,

    /// <summary>Footwork drill — Evasion, secondary Speed.</summary>
    Footwork,

    /// <summary>Kondisyon — Can, ikincil Stamina.</summary>
    Conditioning,

    /// <summary>
    /// Meditation — Will, and nothing else.
    /// </summary>
    /// <remarks>
    /// The only drill with no secondary stat, deliberately: sitting still is the day the warrior does
    /// not touch a sword, and that is exactly its price. It is the counterpart of the reference game's
    /// Meditate, and the only way Will is trained (docs/GDD.md §3).
    /// </remarks>
    Meditation,
}

/// <summary>Training's tunable numbers.</summary>
/// <remarks>
/// <para>
/// The gain is not <b>absolute</b> but a share of the gap left: a day advances the warrior by
/// <see cref="GapClosedPerDay"/> of the distance remaining to the ceiling. With a fixed absolute
/// increase, either the early game would be pointlessly slow or every warrior would stick to the
/// ceiling in the late game; a share puts diminishing returns inside the rule and makes the ceiling
/// something to be <b>approached</b>, not <b>passed</b>.
/// </para>
/// <para>
/// The numbers are <b>not locked</b>. The measurement's question is clear (GDD §11): the market ceiling
/// locked replacement into the recruit band, so the road to progress is now training alone — "buy the
/// cheap raw candidate and train him" and "buy the best you can afford" have to be <b>rivals</b>
/// olmak zorunda.
/// </para>
/// </remarks>
public sealed record TrainingTuning
{
    /// <summary>The share of the remaining distance to the ceiling a training day closes.</summary>
    /// <remarks>
    /// <b>Locked at 0.04</b> (400 dojos × 60 days, `patrol`): at this rate a well-running dojo's best
    /// warrior reaches a score of ~465 in 60 days, that is, right up against the ~473 limit where the
    /// market ceiling starts to bite. That is exactly the relationship wanted — the market replaces up to
    /// a point, and beyond it only training. At 0.02 the ceiling never speaks (training stays decorative),
    /// at 0.08 the dojo rescues itself with training (the treasury rises from 147 to 941, and closed
    /// dojos fall from 18.8% to 3.0%). Details: docs/GDD.md §11.
    /// </remarks>
    public double GapClosedPerDay { get; init; } = 0.04;

    /// <summary>
    /// The share of the remaining distance a <b>fight</b> closes — the other road of growth.
    /// </summary>
    /// <remarks>
    /// Deliberately larger than <see cref="GapClosedPerDay"/>: both spend the same day, but only one of
    /// them can cost a limb or the man himself, so the risky road has to pay better or nobody would take
    /// it (docs/COMPARISON-DOMINA.md, section 3). The ceilings and the secondary share are the same as
    /// training's — the two roads meet the same wall, so a dojo that does not build cannot grow a
    /// warrior past it however hard it fights.
    /// </remarks>
    public double FightGapClosed { get; init; } = 0.08;

    /// <summary>The share the secondary stat takes relative to the primary.</summary>
    public double SecondaryShare { get; init; } = 0.5;

    /// <summary>The ceiling the percentage stats (Accuracy, Defence, ...) can approach.</summary>
    /// <remarks>
    /// Not 100: a warrior pressed against the very end of a stat's own scale turns all of the fight's
    /// dice one way. The ceiling stays below the scale so that training strengthens the roster without
    /// resolving the fight.
    /// </remarks>
    public double SkillCeiling { get; init; } = 90;

    /// <summary>The training days needed before a warrior can choose his path.</summary>
    /// <remarks>
    /// The choice is <b>free of charge</b> but not free: its price is the training days spent up to that
    /// point. Without the day requirement the path would be chosen at the moment of purchase and a
    /// warrior bought from the market would arrive ready-specialised — the market would have trained him instead of the school.
    /// </remarks>
    public int PathTrainingDays { get; init; } = 20;

    /// <summary>The ceiling health and stamina can approach.</summary>
    /// <remarks>
    /// It is kept separate because its scale is separate: a recruit arrives with 100 health while his
    /// percentage stats sit in the 35-55 band. Tied to the same ceiling, the conditioning drill would bite from the first day.
    /// </remarks>
    public double PoolCeiling { get; init; } = 180;
}

/// <summary>The training ground — it computes a day's stat return.</summary>
/// <remarks>
/// Pure and stateless: the same stats, the same drill and the same talent always give the same result.
/// Randomness is <b>deliberately absent</b> — training is the player's investment, not his gamble; with
/// a die, the decision "should I train today" would hide behind the die.
/// </remarks>
public static class TrainingGround
{
    /// <summary>The stats after one day of drill.</summary>
    /// <param name="stats">The raw stats with no disability applied — training writes these.</param>
    /// <param name="drill">The day's drill.</param>
    /// <param name="talent">
    /// The warrior's <see cref="Warrior.Talent"/> share; it multiplies the gain directly.
    /// </param>
    /// <param name="tuning">The training numbers.</param>
    public static WarriorStats After(
        WarriorStats stats,
        Drill drill,
        double talent,
        TrainingTuning? tuning = null)
    {
        TrainingTuning t = tuning ?? new TrainingTuning();
        double primary = Math.Max(0, t.GapClosedPerDay * Math.Max(0, talent));
        double secondary = primary * Math.Max(0, t.SecondaryShare);

        return drill switch
        {
            Drill.Strikes => stats with
            {
                Accuracy = Grow(stats.Accuracy, t.SkillCeiling, primary),
                Aggression = Grow(stats.Aggression, t.SkillCeiling, secondary),
            },
            Drill.Guard => stats with
            {
                Defense = Grow(stats.Defense, t.SkillCeiling, primary),
                Strength = Grow(stats.Strength, t.SkillCeiling, secondary),
            },
            Drill.Footwork => stats with
            {
                Evasion = Grow(stats.Evasion, t.SkillCeiling, primary),
                Speed = Grow(stats.Speed, t.SkillCeiling, secondary),
            },
            Drill.Conditioning => stats with
            {
                MaxHealth = Grow(stats.MaxHealth, t.PoolCeiling, primary),
                MaxStamina = Grow(stats.MaxStamina, t.PoolCeiling, secondary),
            },

            // The one drill with no secondary: a day spent sitting still is a day the sword is not
            // touched, and that is the whole price of Will.
            Drill.Meditation => stats with
            {
                Willpower = Grow(stats.Willpower, t.SkillCeiling, primary),
            },
            _ => stats,
        };
    }

    /// <summary>
    /// The drill that trains the roster's <b>weakest</b> stat.
    /// </summary>
    /// <remarks>
    /// The interface can use this as a suggestion and measurement as a policy: the choice looks at the
    /// <b>proportional</b> distance left to the ceiling, not at raw points — otherwise conditioning would
    /// win every day because its scale is different.
    /// </remarks>
    public static Drill Weakest(WarriorStats stats, TrainingTuning? tuning = null)
    {
        TrainingTuning t = tuning ?? new TrainingTuning();

        Drill pick = Drill.Strikes;
        double widest = -1;

        foreach ((Drill drill, double value, double ceiling) in
            new[]
            {
                (Drill.Strikes, stats.Accuracy, t.SkillCeiling),
                (Drill.Guard, stats.Defense, t.SkillCeiling),
                (Drill.Footwork, stats.Evasion, t.SkillCeiling),
                (Drill.Conditioning, stats.MaxHealth, t.PoolCeiling),
                (Drill.Meditation, stats.Willpower, t.SkillCeiling),
            })
        {
            double gap = ceiling <= 0 ? 0 : Math.Max(0, (ceiling - value) / ceiling);
            if (gap > widest)
            {
                widest = gap;
                pick = drill;
            }
        }

        return pick;
    }

    /// <summary>Moves a stat a share of the remaining distance closer to the ceiling.</summary>
    /// <remarks>
    /// The share cannot exceed 1: at high rates the talent multiplier could push it above 1 and a stat
    /// would <b>pass</b> the ceiling in a single day — the ceiling being a limit that is approached must
    /// live in the rule itself, not be left to the setting being chosen small.
    /// </remarks>
    private static double Grow(double value, double ceiling, double share) =>
        value >= ceiling ? value : value + ((ceiling - value) * Math.Clamp(share, 0, 1));
}
