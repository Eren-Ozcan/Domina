namespace Domina.Core.Model;

/// <summary>Kalıcı olarak kaybedilebilecek uzuvlar.</summary>
/// <remarks>
/// Uzuvlar <b>tek tek</b> durur: sağ kol, sol kol, sağ bacak, sol bacak. Tek bir "kol"
/// kaydı zırhı da kaybı da çift temsil ediyordu — oysa kolun biri gidince diğeri hâlâ
/// yerinde, ve zırh yuva yuvaysa (§7) kolluk da yuva yuva olmalı.
/// </remarks>
public enum BodyPart
{
    /// <summary>Kılıç tutan kol — gücü ve iki elli silah kullanımını en çok etkileyen kayıp.</summary>
    SwordArm,

    /// <summary>Boştaki kol. Kaybı iki elli silahı yine bitirir, gücü az düşürür.</summary>
    OffArm,

    /// <summary>Sağ bacak — hareket kabiliyeti.</summary>
    RightLeg,

    /// <summary>Sol bacak — hareket kabiliyeti.</summary>
    LeftLeg,

    /// <summary>Derinlik algısı — isabeti etkiler.</summary>
    Eye,
}

/// <summary>
/// Kaybedilmiş uzuvların kümesi.
/// </summary>
/// <remarks>
/// Liste değil <b>küme</b>: aynı uzuv iki kez kaybedilemez, tip bu kuralı taşısın.
/// Küme aynı zamanda değer eşitliğine sahiptir — dövüş özetleri record olduğu için
/// bu şart: liste tutulsaydı iki özdeş koşu referans farkı yüzünden farklı sayılır ve
/// determinizm testleri anlamsızlaşırdı. Kayıpların <b>sırası</b> gerekiyorsa olay
/// akışındaki <c>WarriorDismembered</c> zaten sıralı.
/// </remarks>
[Flags]
public enum BodyPartSet
{
    None = 0,
    SwordArm = 1 << 0,
    OffArm = 1 << 1,
    RightLeg = 1 << 2,
    LeftLeg = 1 << 3,
    Eye = 1 << 4,
}

public static class BodyPartSetExtensions
{
    public static BodyPartSet AsFlag(this BodyPart part) => part switch
    {
        BodyPart.SwordArm => BodyPartSet.SwordArm,
        BodyPart.OffArm => BodyPartSet.OffArm,
        BodyPart.RightLeg => BodyPartSet.RightLeg,
        BodyPart.LeftLeg => BodyPartSet.LeftLeg,
        BodyPart.Eye => BodyPartSet.Eye,
        _ => BodyPartSet.None,
    };

    /// <summary>Uzuv bir kol mu?</summary>
    public static bool IsArm(this BodyPart part) =>
        part is BodyPart.SwordArm or BodyPart.OffArm;

    /// <summary>Uzuv bir bacak mı?</summary>
    public static bool IsLeg(this BodyPart part) =>
        part is BodyPart.RightLeg or BodyPart.LeftLeg;

    public static bool Has(this BodyPartSet set, BodyPart part) => (set & part.AsFlag()) != 0;

    /// <summary>Kümedeki uzuvlar, <see cref="BodyPart"/> sırasıyla.</summary>
    public static IEnumerable<BodyPart> Parts(this BodyPartSet set)
    {
        foreach (BodyPart part in Enum.GetValues<BodyPart>())
        {
            if (set.Has(part))
            {
                yield return part;
            }
        }
    }
}

/// <summary>Darbenin indiği bölge.</summary>
/// <remarks>
/// <para>
/// <see cref="BodyPart"/>'tan ayrıdır: her bölge kaybedilebilir bir uzuv değildir.
/// Gövdeye inen ağır darbe savaşçıyı öldürebilir ama koparacak bir şeyi yoktur.
/// </para>
/// <para>
/// Bölgelerin olasılığı eşit değildir (bkz. <c>CombatTuning</c>). Eşit olsaydı gövde
/// zırhı değersizleşir, zırh yatırımı "hepsini eşit dağıt" gibi düz bir optimizasyona
/// dönerdi.
/// </para>
/// </remarks>
public enum HitLocation
{
    Head,
    Torso,
    SwordArm,
    OffArm,
    RightLeg,
    LeftLeg,
}

/// <summary>
/// Bir savaşçının kalıcı sakatlığı. Ölümden dönüldüğünde kalır ve geri alınamaz.
/// </summary>
/// <remarks>
/// Domina'da uzuv kopması yalnızca ölüm anının görsel efektiydi; burada
/// <b>hayatta kalıp sakat yaşamaya devam etme</b> mekaniğidir (bkz. docs/GDD.md §7).
/// </remarks>
public sealed record Disability(BodyPart Part)
{
    /// <summary>
    /// Saldırı gücüne uygulanan çarpan.
    /// </summary>
    /// <remarks>
    /// Kılıç tutan kol ile boştaki kol aynı şey değil: birincisi vuruşun kendisidir,
    /// ikincisi dengedir. İkisi de iki elli silahı bitirir (bkz.
    /// <see cref="BlocksTwoHandedWeapons"/>), ama tek elli dövüşen bir savaşçı için
    /// boştaki kolun kaybı taşınabilir bir kayıptır.
    /// </remarks>
    public double StrengthMultiplier => Part switch
    {
        BodyPart.SwordArm => 0.65,
        BodyPart.OffArm => 0.85,
        _ => 1.0,
    };

    /// <summary>Kaçınmaya uygulanan çarpan.</summary>
    public double EvasionMultiplier => Part.IsLeg() ? 0.55 : 1.0;

    /// <summary>
    /// Yürüme hızına uygulanan çarpan.
    /// </summary>
    /// <remarks>
    /// Bacağını kaybeden savaşçı yalnızca kaçınmayı değil <b>kaçabilmeyi</b> de kaybeder:
    /// topallayan biri kovalayandan uzaklaşamaz. Uzuv kaybının en ağır ikincil bedeli bu.
    /// </remarks>
    public double SpeedMultiplier => Part.IsLeg() ? 0.60 : 1.0;

    /// <summary>İsabet şansına uygulanan çarpan.</summary>
    public double AccuracyMultiplier => Part == BodyPart.Eye ? 0.75 : 1.0;

    /// <summary>Hangi kol giderse gitsin iki elli silah kullanılamaz.</summary>
    public bool BlocksTwoHandedWeapons => Part.IsArm();
}
