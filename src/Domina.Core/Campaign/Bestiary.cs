using Domina.Core.Model;

namespace Domina.Core.Campaign;

/// <summary>A scalable template for a yokai kind.</summary>
/// <remarks>
/// <para>
/// There are only <b>numbers</b> here: each yokai's own combat behaviour (Open Decision #3) has not
/// been written. GDD §4's note is this: the behavioural difference will not be a separate code path but
/// the target-selection weights tuned per kind. When that tuning arrives a field is added to this
/// template — encounter generation does not change.
/// </para>
/// <para>
/// The template scales with <b>power</b>: the same kappa is a kappa on day 1 and on day 40, but further
/// along the curve it is tougher and harder. Difficulty rising along a single curve (GDD §10) requires
/// this — keeping a separate "strong kappa" kind would be writing the same curve twice.
/// yerde tarif etmek olurdu.
/// </para>
/// </remarks>
/// <param name="Name">Display name.</param>
/// <param name="Base">The stats at power 1.0.</param>
/// <param name="Weapon">The weapon it carries.</param>
/// <param name="Weight">Its weight for being drawn from the pool.</param>
/// <param name="MinPower">The power at which this kind first appears on the curve.</param>
public sealed record YokaiKind(
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

/// <summary>The yokai pool encounters are drawn from.</summary>
/// <remarks>
/// The list is <b>incomplete</b>: of the GDD's bestiary candidates only those whose numbers are settled
/// are here. Nue and the boss candidates (Gashadokuro, Shuten-dōji, Yamata-no-Orochi) are missing —
/// GDD §10 builds no boss structure, they will be strong enemies at the curve's top end.
/// </remarks>
public static class Bestiary
{
    /// <summary>Small, agile, in packs.</summary>
    public static YokaiKind Kappa { get; } = new(
        "Kappa",
        new WarriorStats(MaxHealth: 70, Aggression: 58, Defense: 14, Evasion: 30, Strength: 30, Accuracy: 52, MaxStamina: 100, Speed: 55),
        Weapon.Katana(),
        Weight: 3);

    /// <summary>Fast, high evasion, a short knife.</summary>
    public static YokaiKind Kitsune { get; } = new(
        "Kitsune",
        new WarriorStats(MaxHealth: 62, Aggression: 62, Defense: 12, Evasion: 42, Strength: 28, Accuracy: 58, MaxStamina: 100, Speed: 72),
        Weapon.Tanto(),
        Weight: 2);

    /// <summary>Fast, hit-and-run; ranged.</summary>
    public static YokaiKind Tengu { get; } = new(
        "Tengu",
        new WarriorStats(MaxHealth: 75, Aggression: 68, Defense: 12, Evasion: 45, Strength: 34, Accuracy: 60, MaxStamina: 100, Speed: 80),
        Weapon.Katana(),
        Weight: 2,
        MinPower: 1.2);

    /// <summary>Heavy, high damage, slow.</summary>
    public static YokaiKind Oni { get; } = new(
        "Oni",
        new WarriorStats(MaxHealth: 130, Aggression: 55, Defense: 28, Evasion: 14, Strength: 52, Accuracy: 55, MaxStamina: 100, Speed: 28),
        Weapon.Tetsubo(),
        Weight: 2,
        MinPower: 1.5);

    /// <summary>Long-hafted; its reach helps in a crowd.</summary>
    public static YokaiKind Jorogumo { get; } = new(
        "Jorōgumo",
        new WarriorStats(MaxHealth: 95, Aggression: 60, Defense: 20, Evasion: 30, Strength: 40, Accuracy: 58, MaxStamina: 100, Speed: 48),
        Weapon.Yari(),
        Weight: 1,
        MinPower: 1.8);

    public static IReadOnlyList<YokaiKind> All { get; } = [Kappa, Kitsune, Tengu, Oni, Jorogumo];

    /// <summary>The kinds that can take the field at the given power.</summary>
    public static IEnumerable<YokaiKind> AvailableAt(double power) =>
        All.Where(k => power >= k.MinPower);
}
