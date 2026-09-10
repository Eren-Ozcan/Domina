using Domina.Core.Model;

namespace Domina.Core.Campaign;

/// <summary>A scalable template for a kind of enemy.</summary>
/// <remarks>
/// <para>
/// There are only <b>numbers</b> here: each kind's own combat behaviour (Open Decision #3) has not
/// been written. GDD §4's note is this: the behavioural difference will not be a separate code path but
/// the target-selection weights tuned per kind. When that tuning arrives a field is added to this
/// template — encounter generation does not change.
/// </para>
/// <para>
/// The template scales with <b>power</b>: the same collector is a collector on day 1 and on day 40, but
/// further along the curve it is tougher and harder. Difficulty rising along a single curve (GDD §10)
/// requires this — keeping a separate "strong collector" kind would be describing the same curve twice.
/// </para>
/// </remarks>
/// <param name="Name">Display name.</param>
/// <param name="Base">The stats at power 1.0.</param>
/// <param name="Weapon">The weapon it carries.</param>
/// <param name="Weight">Its weight for being drawn from the pool.</param>
/// <param name="MinPower">The power at which this kind first appears on the curve.</param>
public sealed record EnemyKind(
    string Name,
    WarriorStats Base,
    Weapon Weapon,
    double Weight = 1,
    double MinPower = 0)
{
    /// <summary>
    /// Produces an instance at the given power.
    /// </summary>
    /// <remarks>
    /// Health and damage scale <b>directly</b> with power, accuracy/defence/evasion by its <b>square root</b>.
    /// The reason comes from measurement: when accuracy and evasion are grown linearly the curve turns
    /// into a wall at some point — the enemy becomes unmissable while the player starts missing, and the
    /// difficulty increase is counted twice. Health and damage, by contrast, are the axis the player can
    /// meet with his own equipment.
    /// </remarks>
    public Warrior Spawn(WarriorId id, double power)
    {
        double linear = Math.Max(0.1, power);
        double soft = Math.Sqrt(linear);

        WarriorStats stats = Base with
        {
            MaxHealth = Base.MaxHealth * linear,
            Strength = Base.Strength * linear,
            Accuracy = Cap(Base.Accuracy * soft),
            Defense = Cap(Base.Defense * soft),
            Evasion = Cap(Base.Evasion * soft),
        };

        return new Warrior(id, Name, stats, Weapon);
    }

    /// <summary>The stats are on a 0-100 scale; they must not overflow as the curve grows.</summary>
    private static double Cap(double value) => Math.Clamp(value, 0, 95);
}

/// <summary>The pool encounters are drawn from.</summary>
/// <remarks>
/// <para>
/// The enemy is <b>human</b> (Open Decision #16): the kinds below are the men a rival school puts on
/// the road, not creatures. The numbers are unchanged from the earlier bestiary — only the identities
/// were rewritten, so every measurement taken before this rename still holds.
/// </para>
/// <para>
/// The list is <b>incomplete</b>: only the kinds whose numbers are settled are here. The named
/// adversaries of the story (a rival school's head and its seniors) are absent — GDD §10 builds no
/// boss structure, they will be strong enemies at the curve's top end.
/// </para>
/// </remarks>
public static class Adversaries
{
    /// <summary>A rival school's fee collector: small, quick, and rarely alone.</summary>
    public static EnemyKind Collector { get; } = new(
        "Collector",
        new WarriorStats(MaxHealth: 70, Aggression: 58, Defense: 14, Evasion: 30, Strength: 30, Accuracy: 52, MaxStamina: 100, Speed: 55),
        Weapon.Katana(),
        Weight: 3);

    /// <summary>A back-alley knife: fast, hard to hit, no armour worth the name.</summary>
    public static EnemyKind Cutthroat { get; } = new(
        "Cutthroat",
        new WarriorStats(MaxHealth: 62, Aggression: 62, Defense: 12, Evasion: 42, Strength: 28, Accuracy: 58, MaxStamina: 100, Speed: 72),
        Weapon.Tanto(),
        Weight: 2);

    /// <summary>A wandering swordsman on his own trial: fast, hit-and-run.</summary>
    public static EnemyKind Duelist { get; } = new(
        "Duelist",
        new WarriorStats(MaxHealth: 75, Aggression: 68, Defense: 12, Evasion: 45, Strength: 34, Accuracy: 60, MaxStamina: 100, Speed: 80),
        Weapon.Katana(),
        Weight: 2,
        MinPower: 1.2);

    /// <summary>A street bravo with an absurdly heavy weapon: high damage, slow.</summary>
    public static EnemyKind Kabukimono { get; } = new(
        "Kabukimono",
        new WarriorStats(MaxHealth: 130, Aggression: 55, Defense: 28, Evasion: 14, Strength: 52, Accuracy: 55, MaxStamina: 100, Speed: 28),
        Weapon.Tetsubo(),
        Weight: 2,
        MinPower: 1.5);

    /// <summary>A rival school's senior: long-hafted, and his reach tells in a crowd.</summary>
    public static EnemyKind SeniorStudent { get; } = new(
        "Senior Student",
        new WarriorStats(MaxHealth: 95, Aggression: 60, Defense: 20, Evasion: 30, Strength: 40, Accuracy: 58, MaxStamina: 100, Speed: 48),
        Weapon.Yari(),
        Weight: 1,
        MinPower: 1.8);

    public static IReadOnlyList<EnemyKind> All { get; } = [Collector, Cutthroat, Duelist, Kabukimono, SeniorStudent];

    /// <summary>The kinds that can take the field at the given power.</summary>
    public static IEnumerable<EnemyKind> AvailableAt(double power) =>
        All.Where(k => power >= k.MinPower);
}
