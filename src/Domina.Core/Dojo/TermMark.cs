namespace Domina.Core.Dojo;

/// <summary>The kinds of day a term is later read back by.</summary>
/// <remarks>
/// These are the days that decide a term without announcing themselves. A player who loses a term on
/// day 41 lost it somewhere around day 22, and nothing in the run remembered where — the day's report
/// is printed once and gone, and the move journal records what the player <b>did</b> rather than what
/// the term did to him. This is that second list, and it is deliberately short: only the days that
/// cost something the term could not get back.
/// </remarks>
public enum TermMarkKind
{
    /// <summary>A week closed with nothing filed at the clerk's office.</summary>
    MissedWeek,

    /// <summary>The store could not feed everybody, and somebody went without.</summary>
    WentHungry,

    /// <summary>The payroll could not be met and staff walked out.</summary>
    StaffWalked,

    /// <summary>The rival stood in the yard and nobody answered him.</summary>
    Sacked,

    /// <summary>A bounty was accepted and then not brought in.</summary>
    BountyBroken,

    /// <summary>The rival took a village.</summary>
    VillageLost,

    /// <summary>A man was called to stand, and the verdict went against him.</summary>
    ManCondemned,

    /// <summary>A man was called to stand, and the crowd called him bushi.</summary>
    ManPardoned,
}

/// <summary>One day the term turned on.</summary>
/// <param name="Day">The day it happened.</param>
/// <param name="Kind">What kind of day it was.</param>
/// <param name="Subject">Whom or what it was about — a man's name, a village's; empty when it is neither.</param>
/// <param name="Count">How much of it there was — men left hungry, staff gone. Zero when it does not count.</param>
public readonly record struct TermMark(int Day, TermMarkKind Kind, string Subject = "", int Count = 0);
