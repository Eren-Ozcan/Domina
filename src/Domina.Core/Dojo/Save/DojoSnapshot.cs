using Domina.Core.Campaign;
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
/// <param name="Resources">The treasury and the stores.</param>
/// <param name="Warriors">The roster — the dead included.</param>
/// <param name="Seed">
/// The expedition's seed. Because offers are recomputed from it and the day, the offers themselves are
/// not written to the file — so that an old save does not bring back the old adversary numbers.
/// </param>
/// <param name="School">
/// The school facilities bought. Only <b>which nodes</b> are written; the size of the bonuses is a
/// balance number and does not go into the file.
/// </param>
/// <param name="Difficulty">
/// The tier the run is played at. The tier is the player's decision, so it is written down; what it
/// multiplies is balance and stays in the code, so a retuned patch reaches an old save.
/// </param>
/// <param name="Charms">
/// The temple charms sitting in the store. A charm hanging on a warrior is written with that warrior,
/// not here — the two lists are the two places a charm can be, and merging them would lose which.
/// </param>
/// <param name="TakenOffers">
/// The posting days of the jobs already taken off the board. The jobs themselves are not written (they
/// are recomputed from the day and the seed); without this mark a job could be taken twice by reloading
/// the save, and one that was taken would stand on the board again.
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
    IReadOnlyList<int>? HiredRecruits = null,
    IReadOnlyList<PostSnapshot>? Staff = null,
    IReadOnlyList<BuildSiteSnapshot>? Sites = null,
    int? LastFeastDay = null,
    SeasonSnapshot? Season = null,
    TribunalSnapshot? Tribunal = null,
    DifficultyTier Difficulty = DifficultyTier.Master,
    IReadOnlyList<CharmStackSnapshot>? Charms = null,
    ProvinceSnapshot? Province = null,
    IReadOnlyList<StandingSnapshot>? Standing = null,
    IReadOnlyList<int>? TakenOffers = null,
    string? Name = null,
    IReadOnlyList<TermMark>? Marks = null,
    string? Instructor = null)
{
    /// <summary>
    /// The version of the files written. It rises when the format changes in a <b>breaking</b> way;
    /// adding a field is not breaking — a missing field loads with its default.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>A new game's empty dojo.</summary>
    public static DojoSnapshot Empty { get; } = new(CurrentVersion, 1, Resources.Empty, []);
}

/// <summary>
/// The season's clock and its books.
/// </summary>
/// <remarks>
/// Only what the <b>player produced</b> is here — which day he last filed a fight, how many weeks he
/// let pass, how many heads he brought in, where the last night stands. The season's <b>numbers</b>
/// (its length, the gate, what a missed week costs) are balance and stay in the code, so a patch that
/// retunes them reaches an old save too.
/// </remarks>
/// <param name="Phase">Where the run stands.</param>
/// <param name="LastFightDay">The day the dojo last filed a fight; <c>0</c> if it never has.</param>
/// <param name="MissedWeeks">The weeks that closed with no fight filed.</param>
/// <param name="HeadsTaken">The heads brought in — the gate counts these.</param>
/// <param name="Battles">Every fight the season filed.</param>
/// <param name="Victories">The fights won.</param>
/// <param name="Dead">The warriors the season buried.</param>
/// <param name="FinalRound">The bout of the last night that comes next.</param>
/// <param name="MissedStreak">The missed weeks standing in a row — the penalty is charged on this.</param>
public sealed record SeasonSnapshot(
    SeasonPhase Phase = SeasonPhase.Running,
    int LastFightDay = 0,
    int MissedWeeks = 0,
    int HeadsTaken = 0,
    int Battles = 0,
    int Victories = 0,
    int Dead = 0,
    int FinalRound = 1,
    int MissedStreak = 0)
{
    public static SeasonSnapshot From(Season season)
    {
        ArgumentNullException.ThrowIfNull(season);

        return new SeasonSnapshot(
            season.Phase,
            season.LastFightDay,
            season.MissedWeeks,
            season.HeadsTaken,
            season.Battles,
            season.Victories,
            season.Dead,
            season.FinalRound,
            season.MissedStreak);
    }
}

/// <summary>
/// The tribunal's books: who is standing, who is waiting and who a pardon still protects.
/// </summary>
/// <remarks>
/// It goes into the file for the reason a broken promise does — reloading must not be a way out of a
/// verdict. The <b>voices already cast</b> are deliberately not saved: they belong to a live chat that
/// is not there when the file is opened again, so the standing man faces the morning with a silent
/// crowd rather than with a tally nobody can add to.
/// </remarks>
/// <param name="Standing">The man before the tribunal; <c>null</c> if nobody is.</param>
/// <param name="Queue">Those summoned and waiting their turn.</param>
/// <param name="Immunity">A pardoned man and the day his protection runs out.</param>
public sealed record TribunalSnapshot(
    SummonsSnapshot? Standing = null,
    IReadOnlyList<SummonsSnapshot>? Queue = null,
    IReadOnlyList<ImmunitySnapshot>? Immunity = null);

/// <param name="Warrior">Whose summons it is.</param>
/// <param name="Name">His name on the day he was called.</param>
/// <param name="Honor">The honour that put him there — the verdict is weighed against this, not against today's.</param>
/// <param name="Willpower">What he has when nobody speaks for him.</param>
/// <param name="OpenedDay">The day he was called.</param>
public sealed record SummonsSnapshot(
    int Warrior,
    string Name,
    double Honor,
    double Willpower,
    int OpenedDay);

/// <param name="Warrior">The pardoned man.</param>
/// <param name="UntilDay">The day he can be summoned again.</param>
public sealed record ImmunitySnapshot(int Warrior, int UntilDay);

/// <summary>A building still going up, and the days it has left.</summary>
/// <remarks>
/// The site goes into the save because its gold has already left the treasury: without it, reloading
/// would lose a paid-for building, and with the days rounded up it would become a way to shorten the
/// wait. The days are clamped to the building's own length on load, so an edited file cannot open a
/// hall tomorrow that takes twelve days.
/// </remarks>
public sealed record BuildSiteSnapshot(SchoolNodeId Id, int DaysLeft);

/// <summary>The province as the file keeps it (docs/GDD.md §10, Open Decision #17).</summary>
/// <remarks>
/// The settlements and the one number the rival stores go into the file because they are state the
/// <b>season produced</b>: which villages the player answered for, how far he has pressed the rest.
/// What a warning level is <b>worth</b> — how many moves it takes, how many contracts win a village —
/// stays in the code, so a retuned patch reaches an old save.
/// </remarks>
/// <param name="Settlements">The twelve, in index order.</param>
/// <param name="NextMoveDay">The day his next move falls on.</param>
/// <param name="Deniability">How far he can still go.</param>
/// <param name="RaidPending">Is he at the gate?</param>
/// <param name="TargetKnownUntil">Until which day a settlement's word revealed his next target.</param>
/// <param name="AnsweredForMoveDay">The move cycle the player has already answered.</param>
/// <param name="Sacks">How many times he was left standing in the yard — the closing screen reads it.</param>
public sealed record ProvinceSnapshot(
    IReadOnlyList<SettlementSnapshot> Settlements,
    int NextMoveDay,
    int Deniability,
    bool RaidPending = false,
    int TargetKnownUntil = 0,
    int AnsweredForMoveDay = 0,
    int Sacks = 0);

/// <summary>What one party thinks of the dojo, and what the season has asked of it.</summary>
/// <remarks>
/// The number goes into the file because it is what the season produced; what a <b>tier</b> is worth
/// stays in the code, so a retuned patch reaches an old save. The gifts are written with it, or
/// reloading would make every gift the first one again.
/// </remarks>
public sealed record StandingSnapshot(Patron Patron, double Value, int Gifts = 0, int LastFiled = 0);

/// <summary>One settlement's line in the file.</summary>
public sealed record SettlementSnapshot(
    int Index,
    Allegiance Held,
    int Warning = 0,
    int Contracts = 0,
    int ChangedDay = 0);

/// <summary>One filled post: the role, and the retired warrior holding it if it is one of the dojo's own.</summary>
/// <remarks>
/// The wage is not written — it is a balance number — but <b>who</b> stands in the post is, because a
/// master of the house costs nothing and is worth a different amount (docs/GDD.md §10).
/// </remarks>
public sealed record PostSnapshot(StaffRole Role, int? Master = null);

/// <summary>How many of one kind of charm are in the store.</summary>
/// <remarks>
/// A list of pairs rather than a dictionary: the kinds are an enum, and a dictionary keyed by an enum
/// serialises as its <b>names</b> — a renamed member would then silently empty a player's store.
/// </remarks>
public sealed record CharmStackSnapshot(OmamoriKind Kind, int Count);

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
/// <param name="Class">The class trained; it goes into the save because it was bought with a facility.</param>
/// <param name="Released">
/// His term ended and he walked out free. He is saved as a separate flag from death: both take him off
/// the roster, and the closing screen counts them on opposite sides.
/// </param>
/// <param name="Charms">The temple charms he is wearing (docs/GDD.md §10).</param>
/// <param name="Retired">He left the field for good and became a master of the house.</param>
/// <param name="Victories">The fights he came back from — the retirement gate reads it.</param>
/// <param name="Morale">
/// His condition today. It is saved because it is state the season produced, not a balance number —
/// what a point of morale is <b>worth</b> still comes from the code.
/// </param>
/// <param name="Mastery">
/// What he has learned of each weapon he has carried, weapon name to share (docs/GDD.md §10). It is
/// saved for the same reason morale is: the days that bought it are the player's, while what a point
/// of mastery is worth stays in the code.
/// </param>
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
    WarriorPath Path = WarriorPath.None,
    WarriorClass Class = WarriorClass.None,
    double Morale = MoraleScale.Starting,
    bool Released = false,
    IReadOnlyDictionary<string, double>? Mastery = null,
    IReadOnlyList<OmamoriKind>? Charms = null,
    bool Retired = false,
    int Victories = 0);

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
    double Poison = 0,
    double UntrainedShare = 1)
{
    public static ThrownWeaponSnapshot From(ThrownWeapon thrown) => new(
        thrown.Name,
        thrown.Class,
        thrown.Damage,
        thrown.Range,
        thrown.Speed,
        thrown.Ammo,
        thrown.ThrowSeconds,
        thrown.Poison,
        thrown.UntrainedShare);

    public ThrownWeapon ToThrownWeapon() =>
        new(Name, Class, Damage, Range, Speed, Ammo, ThrowSeconds)
        {
            Poison = Poison,
            UntrainedShare = UntrainedShare,
        };
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
