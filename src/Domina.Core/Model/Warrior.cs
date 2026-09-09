using System.Collections.ObjectModel;

namespace Domina.Core.Model;

/// <summary>
/// The persistent state of a warrior in the dojo.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Id"/> is permanent and unique; <see cref="Name"/> is not.
/// A name can belong to only <b>one living</b> warrior at a time, but when X dies it returns to the
/// pool and a new X can arrive later (see docs/GDD.md §6). That is why matching everywhere is done by
/// Id, not by name.
/// </para>
/// </remarks>
public sealed class Warrior
{
    private readonly List<Disability> _disabilities = [];

    public Warrior(
        WarriorId id,
        string name,
        WarriorStats baseStats,
        Weapon? weapon = null,
        Armor? armor = null,
        ThrownWeapon? thrown = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
        BaseStats = baseStats;
        Weapon = weapon ?? Weapon.Katana();
        Armor = armor ?? Armor.None();
        Thrown = thrown;
        Honor = HonorScale.Starting;
    }

    public WarriorId Id { get; }

    /// <summary>A name drawn from chat or generated. The player can always change it.</summary>
    public string Name { get; set; }

    /// <summary>The raw stats with no disability applied.</summary>
    public WarriorStats BaseStats { get; set; }

    public Weapon Weapon { get; set; }

    public Armor Armor { get; set; }

    /// <summary>The throwing slot. <c>null</c> means the warrior cannot attack at range.</summary>
    public ThrownWeapon? Thrown { get; set; }

    /// <summary>
    /// Throwing is possible after losing an arm too — it is thrown one-handed.
    /// </summary>
    /// <remarks>
    /// This is the only real threat left in the hand of a warrior who cannot use a two-handed melee
    /// weapon (see <see cref="UsableWeapon"/>).
    /// </remarks>
    public ThrownWeapon? UsableThrown => Thrown;

    /// <summary>0-100. It feeds the economy and the seppuku threshold (see docs/GDD.md §6).</summary>
    public double Honor { get; set; }

    /// <summary>
    /// How quickly he benefits from training (1.0 = average).
    /// </summary>
    /// <remarks>
    /// The unchanging share a warrior is born with. This is the second axis of the buying decision: two
    /// candidates arriving with the same stats do not develop at the same speed, so a cheap raw
    /// candidate can turn out better in the long run than an expensive ready-made one. <b>The fight does
    /// not read this</b> — only training does (<see cref="Dojo.TrainingGround"/>): it multiplies a day's gain.
    /// </remarks>
    public double Talent { get; set; } = 1.0;

    /// <summary>
    /// The path the warrior chose — chosen once, never taken back.
    /// </summary>
    /// <remarks>
    /// Progression on the warrior side is deliberately <b>shallow</b>: a single choice, three options
    /// (GDD §10 "Skill tree depth"). The real long-term investment is in the school; with a deep tree
    /// tied to the warrior, permadeath would turn into the loss of a large investment and the player
    /// would avoid sending his warrior into the field. The choice is unlocked in the dojo (training
    /// days); <b>the fight only reads the result</b> — the multipliers enter <see cref="EffectiveStats"/>.
    /// </remarks>
    public WarriorPath Path { get; set; } = WarriorPath.None;

    public bool IsAlive { get; private set; } = true;

    /// <summary>Permanent disabilities. They cannot be undone.</summary>
    public IReadOnlyList<Disability> Disabilities => new ReadOnlyCollection<Disability>(_disabilities);

    /// <summary>The stats with disabilities applied — the ones the fight uses.</summary>
    public WarriorStats EffectiveStats
    {
        get
        {
            // With no path chosen the calculation is skipped entirely: this is the fight's hot path
            // (millions of reads per warrior over tens of thousands of fights) and an empty multiplier
            // pass slowed it down measurably.
            WarriorStats s = Path == WarriorPath.None
                ? BaseStats
                : PathScale.Apply(BaseStats, Path);
            foreach (Disability d in _disabilities)
            {
                s = s with
                {
                    Strength = s.Strength * d.StrengthMultiplier,
                    Evasion = s.Evasion * d.EvasionMultiplier,
                    Accuracy = s.Accuracy * d.AccuracyMultiplier,
                    Speed = s.Speed * d.SpeedMultiplier,
                };
            }

            return s;
        }
    }

    /// <summary>
    /// A warrior who has lost an arm cannot use his two-handed weapon; whatever you put in his hand he
    /// fights with his fists in practice. The equipment screen has to show this.
    /// </summary>
    public Weapon UsableWeapon =>
        Weapon.TwoHanded && _disabilities.Exists(d => d.BlocksTwoHandedWeapons)
            ? Weapon.Fists()
            : Weapon;

    public bool HasDisability(BodyPart part) => _disabilities.Exists(d => d.Part == part);

    /// <summary>Adds a permanent disability. The same limb cannot be lost twice.</summary>
    public bool AddDisability(BodyPart part)
    {
        if (HasDisability(part))
        {
            return false;
        }

        _disabilities.Add(new Disability(part));
        return true;
    }

    /// <summary>
    /// The damage the kit has absorbed <b>to date</b> — slot by slot.
    /// </summary>
    /// <remarks>
    /// Durability belongs to <b>the warrior</b>, not to the fight: a piece is not used up in a single
    /// fight, it wears across expeditions and one day breaks in the middle of one. The fight reads this
    /// counter but does not write it (batch simulation runs the same roster tens of thousands of times);
    /// writing the wear a fight produced onto the warrior is the dojo layer's job.
    /// </remarks>
    public ArmorWearSet ArmorWear { get; set; }

    /// <summary>Permanent death. There is no way back.</summary>
    public void Kill() => IsAlive = false;
}

/// <summary>The paths a warrior can choose.</summary>
/// <remarks>
/// The three are not the same size but carry the same <b>weight</b>: every path raises two stats, one
/// clearly and one slightly. A single-stat path would reduce "which is better" to a single number; two
/// stats tie the path to the warrior's shape.
/// </remarks>
public enum WarriorPath
{
    /// <summary>Not chosen yet.</summary>
    None,

    /// <summary>The blade path — Accuracy and Strength.</summary>
    Blade,

    /// <summary>Kaya yolu — Savunma ve Can.</summary>
    Stone,

    /// <summary>The shadow path — Evasion and Speed.</summary>
    Shadow,
}

/// <summary>The multipliers a path applies to the stats.</summary>
/// <remarks>
/// They are applied <b>underneath</b> the disability multipliers: the path raises the raw stat, the
/// disability cuts it. Reversed, the path would also magnify the penalty of a lost limb.
/// </remarks>
public static class PathScale
{
    public static WarriorStats Apply(WarriorStats stats, WarriorPath path) => path switch
    {
        WarriorPath.Blade => stats with
        {
            Accuracy = stats.Accuracy * 1.10,
            Strength = stats.Strength * 1.10,
        },
        WarriorPath.Stone => stats with
        {
            Defense = stats.Defense * 1.15,
            MaxHealth = stats.MaxHealth * 1.05,
        },
        WarriorPath.Shadow => stats with
        {
            Evasion = stats.Evasion * 1.15,
            Speed = stats.Speed * 1.10,
        },
        _ => stats,
    };
}

/// <summary>The warrior's permanent, unique identity.</summary>
public readonly record struct WarriorId(int Value)
{
    public override string ToString() => $"W{Value}";
}

/// <summary>The stats that feed the fight.</summary>
/// <param name="MaxHealth">Azami can.</param>
/// <param name="Aggression">Attack frequency/aggressiveness (0-100).</param>
/// <param name="Defense">Reduces the damage taken (0-100).</param>
/// <param name="Evasion">The chance of an evasion attempt, spends stamina (0-100).</param>
/// <param name="Strength">Feeds the damage multiplier (0-100).</param>
/// <param name="Accuracy">Hit chance (0-100).</param>
/// <param name="MaxStamina">Azami stamina.</param>
/// <param name="Speed">
/// Walking speed (0-100). It sets closing, encircling and <b>being able to flee</b>.
/// </param>
/// <remarks>
/// <see cref="Speed"/> was added late and its default is 50: while speed was a single constant, chaser
/// and fleer moved at the same rate, so <b>escape always succeeded</b>. Nobody could catch a warrior
/// running with his back turned and the "Flee" key pressed before contact gave a 100% free exit
/// (measured, 20,000 fights).
/// </remarks>
public readonly record struct WarriorStats(
    double MaxHealth,
    double Aggression,
    double Defense,
    double Evasion,
    double Strength,
    double Accuracy,
    double MaxStamina,
    double Speed = 50)
{
    /// <summary>The base for a new recruit.</summary>
    public static WarriorStats Recruit() => new(
        MaxHealth: 100,
        Aggression: 40,
        Defense: 35,
        Evasion: 35,
        Strength: 40,
        Accuracy: 55,
        MaxStamina: 100,
        Speed: 50);
}

/// <summary>The honour scale's constants.</summary>
public static class HonorScale
{
    public const double Min = 0;
    public const double Max = 100;

    /// <summary>A new warrior starts neutral.</summary>
    public const double Starting = 50;

    public static double Clamp(double value) => Math.Clamp(value, Min, Max);
}
