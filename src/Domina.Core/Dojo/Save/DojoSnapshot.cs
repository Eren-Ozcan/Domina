using Domina.Core.Model;

namespace Domina.Core.Dojo.Save;

/// <summary>Kayıt dosyasının kök nesnesi.</summary>
/// <remarks>
/// <para>
/// Kayıt <b>ayrı bir tip ailesi</b>dir, canlı model değil. Sebep GDD §2'nin kuralı:
/// kayıt versiyonlu ve ileri sürümde yüklenebilir olmalı. Canlı model doğrudan
/// serileştirilseydi her denge alanı (menzil, uzuv kopma çarpanı, blok kalitesi)
/// dosyaya yazılır ve <b>eski dosya yeni dengeyi ezerdi</b> — oyuncu bir sonraki
/// yamada düzeltilen sayıyı kaydından geri getirirdi.
/// </para>
/// <para>
/// Bu yüzden dosyaya yalnızca <b>oyuncunun ürettiği</b> şey yazılır: kim, hangi adla,
/// hangi statlarla, ne kuşanmış, ne kaybetmiş. Türetilen her şey yüklerken yeniden
/// hesaplanır.
/// </para>
/// </remarks>
/// <param name="Version">Dosya biçiminin sürümü. Bkz. <see cref="DojoSnapshot.CurrentVersion"/>.</param>
/// <param name="Day">Kaçıncı gün.</param>
/// <param name="Resources">Kasa ve ambar.</param>
/// <param name="Warriors">Kadro — ölüler dahil.</param>
/// <param name="Seed">
/// Seferin tohumu. Teklifler bundan ve günden yeniden hesaplandığı için tekliflerin
/// kendisi dosyaya yazılmaz — eski kayıt yeni bestiary'yi geri getirmesin diye.
/// </param>
public sealed record DojoSnapshot(
    int Version,
    int Day,
    Resources Resources,
    IReadOnlyList<WarriorSnapshot> Warriors,
    ulong Seed = 1)
{
    /// <summary>
    /// Yazılan dosyaların sürümü. Biçim <b>bozucu</b> şekilde değiştiğinde artar;
    /// alan eklemek bozucu değildir — eksik alan varsayılanıyla yüklenir.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>Yeni oyunun boş dojo'su.</summary>
    public static DojoSnapshot Empty { get; } = new(CurrentVersion, 1, Resources.Empty, []);
}

/// <param name="Id">Kalıcı kimlik. Eşleştirme her yerde bunun üzerinden yapılır.</param>
/// <param name="Name">Görünen ad — canlılar arasında eşsizdir.</param>
/// <param name="Stats">Sakatlık uygulanmamış ham statlar.</param>
/// <param name="Honor">0-100.</param>
/// <param name="IsAlive">Permadeath: <c>false</c> ise savaşçı bir daha dövüşmez.</param>
/// <param name="Disabilities">Kalıcı uzuv kayıpları.</param>
/// <param name="ArmorWear">Kuşamın bugüne kadar emdiği hasar — yuva yuva.</param>
/// <param name="RecoveryDaysRemaining">Kalan revir günü.</param>
/// <param name="TrainingDays">Tamamlanmış antrenman günü.</param>
/// <param name="Talent">Antrenmandan faydalanma payı; oyuncunun ürettiği bir değer olduğu için kayda girer.</param>
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
    double Talent = 1.0);

/// <summary>Silahın <b>tanımlayıcı</b> alanları. Türetilen sayılar yüklerken hesaplanır.</summary>
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

/// <summary>Kuşam — altı yuva ayrı ayrı yazılır (GDD §7 "Zırh yuva yuvadır").</summary>
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
