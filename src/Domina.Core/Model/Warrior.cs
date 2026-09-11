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
    /// 0-100 — the warrior's own condition today (docs/GDD.md §3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is deliberately <b>not</b> honour: honour is what the province thinks of him, morale is how he
    /// is. The two were merged once during the decision round and unmerged again, because a single bar
    /// would carry both the chat's reputation game and the dojo's upkeep game.
    /// </para>
    /// <para>
    /// It moves fast — a victory lifts it, a dead comrade or a hungry day drops it — and
    /// <see cref="WarriorStats.Willpower"/> is its brake. The fight reads it through
    /// <see cref="EffectiveStats"/>.
    /// </para>
    /// </remarks>
    public double Morale { get; set; } = MoraleScale.Starting;

    /// <summary>
    /// What morale is worth to this warrior's stats — the two ends of the multiplier.
    /// </summary>
    /// <remarks>
    /// It is a balance number, so it is carried on the warrior rather than in a static: the sim sweeps
    /// it by setting it on the roster it builds, and two runs in the same process cannot then read each
    /// other's band. It never goes into the save (docs/GDD.md §2).
    /// </remarks>
    public MoraleBand MoraleBand { get; set; } = MoraleBand.Default;

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

    /// <summary>
    /// The class the warrior was trained into — what he is able to <b>do</b>.
    /// </summary>
    /// <remarks>
    /// It is the class half of the <c>class × implement</c> product (docs/GDD.md §4): it opens
    /// catching outright, and scales poison and range. Unlike <see cref="Path"/> it is <b>not</b>
    /// final — losing a limb reopens the choice, because a lost arm closes some classes and the
    /// warrior picks again among the rest (<see cref="ClassAptitude.ChoicesFor"/>).
    /// </remarks>
    public WarriorClass Class { get; set; } = WarriorClass.None;

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

            // Morale sits between the path and disability: what he chose to become is beneath it, what
            // the field took from him is above it. A man in poor spirits is still the man he trained
            // into; he is only worse at being him today.
            s = MoraleScale.Apply(s, Morale, MoraleBand);
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
/// <param name="Willpower">
/// How much the warrior <b>endures</b> (0-100) — the ninth stat, and the only one that does no damage.
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
    double Speed = 50,
    double Willpower = 50)
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
        Speed: 50,
        Willpower: 50);
}

/// <summary>The honour scale's constants.</summary>
/// <summary>Morale's scale and what a day of it is worth to the fight.</summary>
/// <remarks>
/// The band is <b>narrow on purpose</b>. Morale is a condition, not a second class: it must be able to
/// tilt a close fight and never to decide one, or the dojo would be playing the mood bar instead of the
/// roster. The floor is further from the middle than the ceiling because the design has no rescue loan
/// (docs/GDD.md §10) — a bad week has to be felt.
/// </remarks>
public static class MoraleScale
{
    public const double Min = 0;

    public const double Max = 100;

    /// <summary>Where a warrior starts, and the point at which morale does nothing at all.</summary>
    public const double Starting = 50;

    public static double Clamp(double value) => Math.Clamp(value, Min, Max);

    /// <summary>The multiplier morale puts on the fighting stats.</summary>
    /// <remarks>
    /// Health and stamina are left alone: those are pools that carry over between fights, and scaling
    /// them would make a low-morale warrior lose the wounds he already had. Morale touches what he does
    /// <b>today</b> — his hand, his guard, his feet.
    /// </remarks>
    public static WarriorStats Apply(WarriorStats stats, double morale, MoraleBand? band = null)
    {
        MoraleBand b = band ?? MoraleBand.Default;
        double factor = b.FactorFor(morale);
        if (factor == 1)
        {
            return stats;
        }

        return stats with
        {
            Aggression = stats.Aggression * factor,
            Defense = stats.Defense * factor,
            Evasion = stats.Evasion * factor,
            Strength = stats.Strength * factor,
            Accuracy = stats.Accuracy * factor,
            Speed = stats.Speed * factor,
        };
    }
}

/// <summary>The two ends of morale's multiplier.</summary>
/// <param name="AtZero">The multiplier at morale 0.</param>
/// <param name="AtFull">The multiplier at morale 100.</param>
/// <remarks>
/// A balance number, so it lives in code and never in a save. It is interpolated linearly from
/// <see cref="MoraleScale.Starting"/>, which is where the multiplier is exactly 1 — a warrior at the
/// middle is the warrior every earlier measurement was taken on, so the band cannot silently move the
/// numbers already locked.
/// </remarks>
public readonly record struct MoraleBand(double AtZero, double AtFull)
{
    /// <summary>
    /// The locked band: ×0.94 at morale 0, ×1.03 at 100 (measured 2026-09-10).
    /// </summary>
    /// <remarks>
    /// The multiplier lands on six stats at once, so it compounds far harder than it reads. Measured on
    /// <c>3v3</c> over 20.000 fights, victory runs 63.6% at morale 0, 69.6% at the middle and 75.2% at
    /// 100 — an end-to-end swing of about 12 points, which tilts a close fight without deciding one. A
    /// floor of 0.90 was tried first and gave a 17.6-point swing (58.1% at morale 0), and 0.70 collapsed
    /// the side outright at 31.1%: below about 0.90 morale stops being a condition and becomes a second
    /// class.
    /// </remarks>
    public static MoraleBand Default { get; } = new(0.94, 1.03);

    public double FactorFor(double morale)
    {
        double m = MoraleScale.Clamp(morale);
        return m >= MoraleScale.Starting
            ? 1 + ((AtFull - 1) * ((m - MoraleScale.Starting) / (MoraleScale.Max - MoraleScale.Starting)))
            : AtZero + ((1 - AtZero) * (m / MoraleScale.Starting));
    }
}

public static class HonorScale
{
    public const double Min = 0;
    public const double Max = 100;

    /// <summary>A new warrior starts neutral.</summary>
    public const double Starting = 50;

    public static double Clamp(double value) => Math.Clamp(value, Min, Max);
}
