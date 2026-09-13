using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Campaign;

/// <summary>A scalable template for a kind of enemy.</summary>
/// <remarks>
/// <para>
/// A kind is <b>numbers plus an appetite</b>. Its combat behaviour (Open Decision #3) is not a code
/// path of its own: it is <see cref="TargetProfile"/>, a set of multipliers over the target-selection
/// weights every warrior already runs, so a cutthroat and a duelist take the same decision with
/// different tastes. Encounter generation does not change, and a kind that names no profile fights
/// exactly as every kind did before profiles existed.
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
/// <param name="Targeting">
/// How a man of this kind reads the field. <c>null</c> means <see cref="TargetProfile.Default"/>.
/// </param>
public sealed record EnemyKind(
    string Name,
    WarriorStats Base,
    Weapon Weapon,
    double Weight = 1,
    double MinPower = 0,
    TargetProfile? Targeting = null)
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

        return new Warrior(id, Name, stats, Weapon) { Targeting = Targeting ?? TargetProfile.Default };
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
    /// <remarks>
    /// <b>He collects in numbers.</b> The crowd penalty is halved, so two collectors will stand on the
    /// same man rather than take one each — the whole threat of the kind is that it does not fight fair.
    /// </remarks>
    public static EnemyKind Collector { get; } = new(
        "Collector",
        new WarriorStats(MaxHealth: 70, Aggression: 58, Defense: 14, Evasion: 30, Strength: 30, Accuracy: 52, MaxStamina: 100, Speed: 55),
        Weapon.Katana(),
        Weight: 3,
        Targeting: new TargetProfile(Crowd: 0.5, Wounded: 1.2));

    /// <summary>A back-alley knife: fast, hard to hit, no armour worth the name.</summary>
    /// <remarks>
    /// <b>He finishes, he does not duel.</b> A wounded man and a bare region are worth far more to him
    /// than to anyone else, and he holds no loyalty to the fight he is in — the knife goes where the
    /// blood already is.
    /// </remarks>
    public static EnemyKind Cutthroat { get; } = new(
        "Cutthroat",
        new WarriorStats(MaxHealth: 62, Aggression: 62, Defense: 12, Evasion: 42, Strength: 28, Accuracy: 58, MaxStamina: 100, Speed: 72),
        Weapon.Tanto(),
        Weight: 2,
        Targeting: new TargetProfile(Wounded: 1.8, Exposed: 1.5, Stickiness: 0.6));

    /// <summary>A wandering swordsman on his own trial: fast, hit-and-run.</summary>
    /// <remarks>
    /// <b>He wants his own opponent.</b> He will not share a target and he will not leave the man he
    /// picked, and a wound in someone else's enemy is no argument to him — the trial is the point,
    /// not the kill.
    /// </remarks>
    public static EnemyKind Duelist { get; } = new(
        "Duelist",
        new WarriorStats(MaxHealth: 75, Aggression: 68, Defense: 12, Evasion: 45, Strength: 34, Accuracy: 60, MaxStamina: 100, Speed: 80),
        Weapon.Katana(),
        Weight: 2,
        MinPower: 1.2,
        Targeting: new TargetProfile(Wounded: 0.4, Crowd: 1.8, Stickiness: 1.6));

    /// <summary>A street bravo with an absurdly heavy weapon: high damage, slow.</summary>
    /// <remarks>
    /// <b>He swings at whoever is in front of him.</b> The road counts double to a man carrying a
    /// tetsubo at Speed 28 — walking the arena for an opportunity is how he spends a fight without
    /// landing a blow.
    /// </remarks>
    public static EnemyKind Kabukimono { get; } = new(
        "Kabukimono",
        new WarriorStats(MaxHealth: 130, Aggression: 55, Defense: 28, Evasion: 14, Strength: 52, Accuracy: 55, MaxStamina: 100, Speed: 28),
        Weapon.Tetsubo(),
        Weight: 2,
        MinPower: 1.5,
        Targeting: new TargetProfile(Distance: 2.0, Wounded: 0.6, Stickiness: 1.3));

    /// <summary>A rival school's senior: long-hafted, and his reach tells in a crowd.</summary>
    /// <remarks>
    /// <b>He is taught to pick.</b> The yari's reach makes the road cheap to him, so he takes the
    /// opening — the bare region, the man already bleeding — instead of the man nearest his feet.
    /// </remarks>
    public static EnemyKind SeniorStudent { get; } = new(
        "Senior Student",
        new WarriorStats(MaxHealth: 95, Aggression: 60, Defense: 20, Evasion: 30, Strength: 40, Accuracy: 58, MaxStamina: 100, Speed: 48),
        Weapon.Yari(),
        Weight: 1,
        MinPower: 1.8,
        Targeting: new TargetProfile(Distance: 0.8, Exposed: 1.4, Crowd: 0.8));

    /// <summary>The head of the Kurogane school — the fifth bout of the last night.</summary>
    /// <remarks>
    /// He is deliberately <b>outside</b> <see cref="All"/>: the pool is what the road offers, and he is
    /// met once, at the end, or not at all. His block is a senior's shape taken further on every axis
    /// the player can answer with his own kit — health, strength and accuracy — rather than a new
    /// mechanic, because GDD §10 builds no boss structure.
    /// </remarks>
    public static EnemyKind KuroganeHead { get; } = new(
        "Kurogane",
        new WarriorStats(MaxHealth: 120, Aggression: 66, Defense: 30, Evasion: 38, Strength: 46, Accuracy: 66, MaxStamina: 100, Speed: 62),
        Weapon.Katana(),
        Weight: 0,
        MinPower: 2.2,
        Targeting: new TargetProfile(Wounded: 1.5, Exposed: 1.6, Stickiness: 0.8));

    public static IReadOnlyList<EnemyKind> All { get; } = [Collector, Cutthroat, Duelist, Kabukimono, SeniorStudent];

    /// <summary>The kinds that can take the field at the given power.</summary>
    public static IEnumerable<EnemyKind> AvailableAt(double power) =>
        All.Where(k => power >= k.MinPower);
}
