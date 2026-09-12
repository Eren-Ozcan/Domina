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
public sealed record OmamoriCharm(OmamoriKind Kind, string Name, int Price)
{
    /// <summary>The stats with this charm's blessing added.</summary>
    public WarriorStats Apply(WarriorStats stats, double bonus, double poolBonus) => Kind switch
    {
        OmamoriKind.SteadyHand => stats with { Accuracy = stats.Accuracy + bonus },
        OmamoriKind.IronGate => stats with { Defense = stats.Defense + bonus },
        OmamoriKind.LongBreath => stats with { MaxStamina = stats.MaxStamina + poolBonus },
        OmamoriKind.QuietMind => stats with { Willpower = stats.Willpower + bonus },
        OmamoriKind.SwiftFoot => stats with { Evasion = stats.Evasion + bonus },
        _ => stats,
    };
}

/// <summary>The temple's catalogue and the size of a blessing.</summary>
/// <remarks>
/// The prices are equal across the five on purpose: the charms are not a power ladder, they are five
/// answers to five different weaknesses, and a price difference would quietly rank them. What decides
/// which one is bought is the warrior it is going on — the whole point of a charm that can be moved.
/// </remarks>
public static class Omamori
{
    /// <summary>The points a charm adds to a percentage stat.</summary>
    /// <remarks>
    /// Not locked until it is swept. It is deliberately of the same order as a handful of training days
    /// so that a charm is worth buying early and never worth hoarding for the last night.
    /// </remarks>
    public const double StatBonus = 6;

    /// <summary>The points a charm adds to a pool stat (stamina), on its own larger scale.</summary>
    public const double PoolBonus = 15;

    public static IReadOnlyList<OmamoriCharm> All { get; } =
    [
        new(OmamoriKind.SteadyHand, "Steady hand", 120),
        new(OmamoriKind.IronGate, "Iron gate", 120),
        new(OmamoriKind.LongBreath, "Long breath", 120),
        new(OmamoriKind.QuietMind, "Quiet mind", 120),
        new(OmamoriKind.SwiftFoot, "Swift foot", 120),
    ];

    public static OmamoriCharm Find(OmamoriKind kind) => All.Single(c => c.Kind == kind);

    /// <summary>The stats with every charm the warrior carries applied.</summary>
    public static WarriorStats Apply(WarriorStats stats, IReadOnlyList<OmamoriKind> worn)
    {
        ArgumentNullException.ThrowIfNull(worn);

        for (int i = 0; i < worn.Count; i++)
        {
            stats = Find(worn[i]).Apply(stats, StatBonus, PoolBonus);
        }

        return stats;
    }
}
