namespace Domina.Core.Model;

/// <summary>The temple charms a warrior can carry (docs/GDD.md §10).</summary>
/// <remarks>
/// <para>
/// The omamori is the counterpart of the reference game's Jupiter cards, and the decision round kept
/// the two properties that make it a decision rather than a bonus: it is <b>transferable</b> — it moves
/// between warriors and can be sold back — and it is <b>supplied by the temple</b>, so it costs gold
/// and a standing shrine.
/// </para>
/// <para>
/// Every charm adds <b>points</b> to a single stat, never a multiplier. Morale and mastery are already
/// multipliers, and a third one stacked on top of them would compound past the point where any of the
/// three could be measured on its own. Points also keep the charm honest at both ends of a season: the
/// same charm is a real gift to a recruit and a rounding error on a trained veteran, which is exactly
/// the shape the design wants for a thing that can be moved from one man to another.
/// </para>
/// </remarks>
public enum OmamoriKind
{
    /// <summary>Steady hand — Accuracy.</summary>
    SteadyHand,

    /// <summary>Iron gate — Defence.</summary>
    IronGate,

    /// <summary>Long breath — Stamina.</summary>
    LongBreath,

    /// <summary>Quiet mind — Will.</summary>
    QuietMind,

    /// <summary>Swift foot — Evasion.</summary>
    SwiftFoot,
}

/// <summary>What a charm is, what it gives and what the temple asks for it.</summary>
/// <param name="Kind">The permanent identity; this is what goes into the save.</param>
/// <param name="Name">Display name.</param>
/// <param name="Price">What the temple asks for one.</param>
/// <param name="Bonus">
/// The points this charm adds to its own stat. It is <b>per charm</b>, and that is a measured
/// decision rather than a stylistic one — see <see cref="Omamori"/>.
/// </param>
public sealed record OmamoriCharm(OmamoriKind Kind, string Name, int Price, double Bonus)
{
    /// <summary>The stats with this charm's blessing added.</summary>
    public WarriorStats Apply(WarriorStats stats) => Kind switch
    {
        OmamoriKind.SteadyHand => stats with { Accuracy = stats.Accuracy + Bonus },
        OmamoriKind.IronGate => stats with { Defense = stats.Defense + Bonus },
        OmamoriKind.LongBreath => stats with { MaxStamina = stats.MaxStamina + Bonus },
        OmamoriKind.QuietMind => stats with { Willpower = stats.Willpower + Bonus },
        OmamoriKind.SwiftFoot => stats with { Evasion = stats.Evasion + Bonus },
        _ => stats,
    };
}

/// <summary>The temple's catalogue and the size of a blessing.</summary>
/// <remarks>
/// <para>
/// The catalogue is not a power ladder: the charms are five answers to five different weaknesses, and
/// what should decide which one is bought is the warrior it is going on — the whole point of a charm
/// that can be moved. That intent needs a <b>fair exchange rate</b>, not one price: with every charm at
/// the same price the strongest one is simply the right answer for every man, and the shelf stops being
/// a choice. So each charm is priced where it stops being a bad buy without becoming a landslide.
/// </para>
/// <para>
/// <b>The blessing is per charm, and the sizes were measured (2026-09-12).</b> One size for all five
/// made the equal prices a lie: at +6 points each, a season in which every man wore the defence charm
/// won the last night 32.8% of the time against the accuracy charm's 14.1%, while the will and
/// stamina charms landed on the charmless dojo's own figures — 9.9% and 9.6% against 9.8%. The five
/// stats are simply not worth the same point for point.
/// </para>
/// <para>
/// ⚠️ <b>Three of the five cannot be brought to the rung with points at all.</b> Accuracy saturates
/// (8.8 → 10.2 → 9.9 → 9.8% of nights won at +6 / +12 / +18 / +30: the hit chance runs out of room),
/// and will and stamina sit on or below the charmless figures at any size (+20 will 7.4%, +40 will
/// 7.5%, +40 stamina 6.7%). Will is clamped to 0-100 wherever it is read and training already carries
/// a man to 90, so most of a large blessing is thrown away, and a fight ends long before a stamina
/// pool of that size binds. Will and stamina were given jobs that are not a stat on 2026-09-12 (will
/// buys the drill's rate, the pool was made to bind) and stopped being worse than wearing nothing —
/// but at 120 gold none of the three paid for itself, because the gold that goes to the stall is gold
/// the school does not get.
/// </para>
/// <para>
/// <b>The prices were then measured one charm at a time (2026-09-13).</b> Six seeds × 1600 dojos ×
/// 180 days on the bed in <c>docs/PROGRESS.md</c>, each seed compared against its <b>own</b> charmless
/// control, because a single seed's reading of the last night swings about ±1.3 points — wider than
/// the binomial error the earlier rounds assumed. The rung is read in <b>nights won</b>: a charm turns
/// gold into victories, and the net it costs per fight is the price of that, not a fault. Against a
/// charmless dojo's 13.7% of last nights, the paired gains are iron gate <b>+3.4</b> at 120 gold,
/// swift foot <b>+3.8</b> at 80, steady hand <b>+3.7</b> at 40, long breath <b>+3.7</b> at 40 and
/// quiet mind <b>+2.8</b> at 30. Those are the locked prices.
/// </para>
/// <para>
/// One price for all five was re-tested and fails in both directions: at 120 gold steady hand is
/// <b>worse than wearing nothing</b> (−2.1 nights, −5.8 net) while quiet mind and long breath are
/// noise, and at 40 gold iron gate is a landslide (<b>+10.5</b> nights — 13.7% to 24.2%, with the
/// dojo closing 4.9 points less often), which would collapse the shelf into a single right answer.
/// </para>
/// </remarks>
public static class Omamori
{
    /// <summary>The five charms: the kind, the name, the price, and the points it adds.</summary>
    public static IReadOnlyList<OmamoriCharm> All { get; } =
    [
        new(OmamoriKind.SteadyHand, "Steady hand", 40, 12),
        new(OmamoriKind.IronGate, "Iron gate", 120, 4),
        new(OmamoriKind.LongBreath, "Long breath", 40, 15),
        new(OmamoriKind.QuietMind, "Quiet mind", 30, 6),
        new(OmamoriKind.SwiftFoot, "Swift foot", 80, 8),
    ];

    public static OmamoriCharm Find(OmamoriKind kind) => All.Single(c => c.Kind == kind);

    /// <summary>The stats with every charm the warrior carries applied.</summary>
    public static WarriorStats Apply(WarriorStats stats, IReadOnlyList<OmamoriKind> worn)
    {
        ArgumentNullException.ThrowIfNull(worn);

        for (int i = 0; i < worn.Count; i++)
        {
            stats = Find(worn[i]).Apply(stats);
        }

        return stats;
    }
}
