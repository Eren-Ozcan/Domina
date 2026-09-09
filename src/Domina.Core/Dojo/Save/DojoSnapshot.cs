using Domina.Core.Model;

namespace Domina.Core.Dojo.Save;

/// <summary>The save file's root object.</summary>
/// <remarks>
/// <para>
/// The save is <b>a separate type family</b>, not the live model. The reason is GDD §2's rule: the save
/// must be versioned and loadable by a later build. Serialising the live model directly would write
/// every balance field (reach, dismemberment multiplier, block quality) into the file and <b>an old
/// file would override the new balance</b> — the player would bring back a number fixed in the next
/// patch out of his save.
/// </para>
/// <para>
/// So only what <b>the player produced</b> is written to the file: who, under what name, with what
/// stats, wearing what, having lost what. Everything derived is recomputed on load.
/// </para>
/// </remarks>
/// <param name="Version">The file format's version. See <see cref="DojoSnapshot.CurrentVersion"/>.</param>
/// <param name="Day">Which day it is.</param>
/// <param name="Resources">Kasa ve ambar.</param>
/// <param name="Warriors">The roster — the dead included.</param>
/// <param name="Seed">
/// The expedition's seed. Because offers are recomputed from it and the day, the offers themselves are
/// not written to the file — so that an old save does not bring back the old bestiary.
/// </param>
/// <param name="School">
/// The school facilities bought. Only <b>which nodes</b> are written; the size of the bonuses is a
/// balance number and does not go into the file.
/// </param>
/// <param name="HiredRecruits">
/// The indices of the candidates bought from the stall today. The candidates themselves are not written
/// (they are regenerated from the day and the seed); without this mark the same candidate could be
/// bought again by reloading the save.
/// </param>
public sealed record DojoSnapshot(
    int Version,
    int Day,
    Resources Resources,
    IReadOnlyList<WarriorSnapshot> Warriors,
    ulong Seed = 1,
    int? AcceptedBountyDay = null,
    int? ClaimedBountyDay = null,
    IReadOnlyList<SchoolNodeId>? School = null,
    IReadOnlyList<int>? HiredRecruits = null)
{
    /// <summary>
    /// The version of the files written. It rises when the format changes in a <b>breaking</b> way;
    /// adding a field is not breaking — a missing field loads with its default.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>A new game's empty dojo.</summary>
    public static DojoSnapshot Empty { get; } = new(CurrentVersion, 1, Resources.Empty, []);
}

/// <param name="Id">The permanent identity. Matching everywhere is done through it.</param>
/// <param name="Name">Display name — unique among the living.</param>
/// <param name="Stats">The raw stats with no disability applied.</param>
/// <param name="Honor">0-100.</param>
/// <param name="IsAlive">Permadeath: if <c>false</c> the warrior never fights again.</param>
/// <param name="Disabilities">Permanent limb losses.</param>
/// <param name="ArmorWear">The damage the kit has absorbed to date — slot by slot.</param>
/// <param name="RecoveryDaysRemaining">The infirmary days left.</param>
/// <param name="TrainingDays">The training days completed.</param>
/// <param name="Talent">His share of benefit from training; it goes into the save because it is a value the player produced.</param>
/// <param name="Drill">The drill selected — it goes into the save because it is the player's decision.</param>
/// <param name="Path">The path chosen; it goes into the save because it is a decision that cannot be undone.</param>
public sealed record WarriorSnapshot(
    int Id,
    string Name,
    WarriorStats Stats,
    double Honor,
    bool IsAlive,
    IReadOnlyList<BodyPart> Disabilities,
    ArmorWearSet ArmorWear,
    WeaponSnapshot Weapon,
    ArmorSnapshot Armor,
    ThrownWeaponSnapshot? Thrown,
    int RecoveryDaysRemaining,
    int TrainingDays,
    double Talent = 1.0,
    Drill Drill = Drill.Strikes,
    WarriorPath Path = WarriorPath.None);

/// <summary>The weapon's <b>identifying</b> fields. The derived numbers are computed on load.</summary>
public sealed record WeaponSnapshot(
    string Name,
    WeaponClass Class,
    double Damage,
    bool TwoHanded,
    double AttackSeconds,
    bool Catchable = true,
    double CatchSkill = 0,
    double Poison = 0,
    double? DisarmFactorOverride = null,
    double? BlockFactorOverride = null)
{
    public static WeaponSnapshot From(Weapon weapon) => new(
        weapon.Name,
        weapon.Class,
        weapon.Damage,
        weapon.TwoHanded,
        weapon.AttackSeconds,
        weapon.Catchable,
        weapon.CatchSkill,
        weapon.Poison,
        weapon.DisarmFactorOverride,
        weapon.BlockFactorOverride);

    public Weapon ToWeapon() => new(Name, Class, Damage, TwoHanded, AttackSeconds)
    {
        Catchable = Catchable,
        CatchSkill = CatchSkill,
        Poison = Poison,
        DisarmFactorOverride = DisarmFactorOverride,
        BlockFactorOverride = BlockFactorOverride,
    };
}

/// <inheritdoc cref="WeaponSnapshot"/>
public sealed record ThrownWeaponSnapshot(
    string Name,
    WeaponClass Class,
    double Damage,
    double Range,
    double Speed,
    int Ammo,
    double ThrowSeconds,
    double Poison = 0)
{
    public static ThrownWeaponSnapshot From(ThrownWeapon thrown) => new(
        thrown.Name,
        thrown.Class,
        thrown.Damage,
        thrown.Range,
        thrown.Speed,
        thrown.Ammo,
        thrown.ThrowSeconds,
        thrown.Poison);

    public ThrownWeapon ToThrownWeapon() =>
        new(Name, Class, Damage, Range, Speed, Ammo, ThrowSeconds) { Poison = Poison };
}

/// <summary>The kit — the six slots are written separately (GDD §7 "armour is slot by slot").</summary>
public sealed record ArmorSnapshot(
    string Name,
    ArmorPieceSnapshot Head,
    ArmorPieceSnapshot Torso,
    ArmorPieceSnapshot SwordArm,
    ArmorPieceSnapshot OffArm,
    ArmorPieceSnapshot RightLeg,
    ArmorPieceSnapshot LeftLeg)
{
    public static ArmorSnapshot From(Armor armor) => new(
        armor.Name,
        ArmorPieceSnapshot.From(armor.Head),
        ArmorPieceSnapshot.From(armor.Torso),
        ArmorPieceSnapshot.From(armor.SwordArm),
        ArmorPieceSnapshot.From(armor.OffArm),
        ArmorPieceSnapshot.From(armor.RightLeg),
        ArmorPieceSnapshot.From(armor.LeftLeg));

    public Armor ToArmor() => new(
        Name,
        Head.ToPiece(),
        Torso.ToPiece(),
        SwordArm.ToPiece(),
        OffArm.ToPiece(),
        RightLeg.ToPiece(),
        LeftLeg.ToPiece());
}

/// <inheritdoc cref="ArmorSnapshot"/>
public sealed record ArmorPieceSnapshot(
    string Name,
    double DamageReduction,
    double DismembermentResistance,
    double Weight,
    double Durability)
{
    public static ArmorPieceSnapshot From(ArmorPiece piece) =>
        new(piece.Name, piece.DamageReduction, piece.DismembermentResistance, piece.Weight, piece.Durability);

    public ArmorPiece ToPiece() =>
        new(Name, DamageReduction, DismembermentResistance, Weight, Durability);
}
