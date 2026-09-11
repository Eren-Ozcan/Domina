using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>
/// What moves a warrior's morale, and how much (docs/GDD.md §3).
/// </summary>
/// <remarks>
/// <para>
/// Morale is the dojo's <b>fast</b> counter — it answers the week, where training answers the season.
/// The sources are the ones the design named: a victory and a rest day lift it, the bard's hall keeps
/// it up, a feast throws it up; a defeat, a fallen comrade and a hungry day pull it down.
/// </para>
/// <para>
/// The single rule that ties it to the ninth stat: <b>Will brakes the falls and not the rises</b>. A
/// stubborn man is not more cheerful after a victory, he is harder to break after a defeat — and if
/// Will lifted the gains too it would quietly become a ninth combat stat by another road.
/// </para>
/// </remarks>
public sealed record MoraleTuning
{
    /// <summary>
    /// What a quiet, fed day returns — <b>up to the middle of the scale and no further</b>.
    /// </summary>
    /// <remarks>
    /// Measured the other way round first, and it broke the system: an unconditional daily gain sent
    /// every roster to 100 within a month, after which the whole lower half of the band was unreachable
    /// and sweeping it changed nothing at all. A quiet day <b>settles</b> a warrior; it does not elate
    /// him. Everything above the middle has to be bought — a victory, a feast, the bard's hall.
    /// </remarks>
    public double RestGain { get; init; } = 2;

    /// <summary>
    /// How far morale slides back toward the middle every day, from either side.
    /// </summary>
    /// <remarks>
    /// The same shape as honour's decay (<c>DojoTuning.HonorDecayPerDay</c>) and for the same reason: a
    /// high that never fades is not a condition, it is a stat. It is what makes a feast a thing you
    /// spend <b>before</b> a hard week rather than once at the start of the season.
    /// </remarks>
    public double DriftPerDay { get; init; } = 1;

    /// <summary>What a won fight returns to everyone who came off the field.</summary>
    public double VictoryGain { get; init; } = 8;

    /// <summary>What a lost fight costs them.</summary>
    public double DefeatLoss { get; init; } = 10;

    /// <summary>What breaking and running costs the man who broke, on top of the defeat.</summary>
    /// <remarks>
    /// It has to bite, or panic would be free: a warrior who runs comes home worse than one who stood,
    /// which is what makes low morale a spiral the player has to answer rather than watch.
    /// </remarks>
    public double PanicLoss { get; init; } = 6;

    /// <summary>What a comrade's death costs every survivor of that fight.</summary>
    public double ComradeLoss { get; init; } = 6;

    /// <summary>What a day without food costs.</summary>
    public double HungerLoss { get; init; } = 5;

    /// <summary>What the bard's hall returns every day.</summary>
    /// <remarks>
    /// This is the number that takes the bard out of the inert list (build-order step 5): his hall is
    /// the only building whose whole output is morale, and it is scaled by the half-efficiency rule
    /// like any other.
    /// </remarks>
    public double BardGain { get; init; } = 2;

    /// <summary>What a feast returns to the whole roster.</summary>
    public double FeastGain { get; init; } = 15;

    /// <summary>The sake a feast drinks, per living warrior.</summary>
    public int SakePerWarrior { get; init; } = 1;

    /// <summary>How many days must pass between one feast and the next.</summary>
    /// <remarks>
    /// Without a cooldown a rich dojo would hold a feast every day and morale would stop being a
    /// resource at all — it would become a line item, bought once and never thought about again.
    /// </remarks>
    public int FeastCooldownDays { get; init; } = 7;

    /// <summary>How much of a fall Will can absorb at Will 100.</summary>
    /// <remarks>
    /// Below 1 deliberately: nerve should slow the collapse, never stop it. At Will 50 — where every
    /// measurement before this system was taken — the brake is exactly half of this.
    /// </remarks>
    public double WillBrake { get; init; } = 0.60;
}

/// <summary>Applies morale's movements. Pure, stateless, no die of its own.</summary>
public static class MoraleLedger
{
    /// <summary>Pulls morale one day's worth toward the middle, without crossing it.</summary>
    public static void Drift(Warrior warrior, MoraleTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        double step = (tuning ?? new MoraleTuning()).DriftPerDay;
        if (step <= 0)
        {
            return;
        }

        double distance = MoraleScale.Starting - warrior.Morale;
        warrior.Morale = Math.Abs(distance) <= step
            ? MoraleScale.Starting
            : MoraleScale.Clamp(warrior.Morale + (Math.Sign(distance) * step));
    }

    /// <summary>
    /// A quiet day's recovery: it moves the warrior toward the middle and never past it.
    /// </summary>
    public static void Settle(Warrior warrior, double amount)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        if (amount <= 0 || warrior.Morale >= MoraleScale.Starting)
        {
            return;
        }

        warrior.Morale = Math.Min(MoraleScale.Starting, warrior.Morale + amount);
    }

    /// <summary>Raises a warrior's morale. Will does <b>not</b> touch a rise.</summary>
    public static void Raise(Warrior warrior, double amount)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        if (amount <= 0)
        {
            return;
        }

        warrior.Morale = MoraleScale.Clamp(warrior.Morale + amount);
    }

    /// <summary>Lowers a warrior's morale, with his Will taking part of the blow.</summary>
    public static void Lower(Warrior warrior, double amount, MoraleTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        if (amount <= 0)
        {
            return;
        }

        warrior.Morale = MoraleScale.Clamp(warrior.Morale - Braked(warrior, amount, tuning));
    }

    /// <summary>What a fall of this size actually costs <b>this</b> warrior.</summary>
    public static double Braked(Warrior warrior, double amount, MoraleTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        MoraleTuning t = tuning ?? new MoraleTuning();
        double will = Math.Clamp(warrior.EffectiveStats.Willpower, 0, 100) / 100;

        return amount * (1 - (will * t.WillBrake));
    }
}
