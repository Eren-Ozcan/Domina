namespace Domina.Core.Model;

/// <summary>Kalıcı olarak kaybedilebilecek uzuvlar.</summary>
public enum BodyPart
{
    /// <summary>Kılıç tutan el — saldırı gücünü ve iki elli silah kullanımını etkiler.</summary>
    Arm,

    /// <summary>Hareket kabiliyeti — kaçınmayı etkiler.</summary>
    Leg,

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
    Arm = 1 << 0,
    Leg = 1 << 1,
    Eye = 1 << 2,
}

public static class BodyPartSetExtensions
{
    public static BodyPartSet AsFlag(this BodyPart part) => part switch
    {
        BodyPart.Arm => BodyPartSet.Arm,
        BodyPart.Leg => BodyPartSet.Leg,
        BodyPart.Eye => BodyPartSet.Eye,
        _ => BodyPartSet.None,
    };

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
    Arm,
    Leg,
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
    /// <summary>Saldırı gücüne uygulanan çarpan.</summary>
    public double StrengthMultiplier => Part == BodyPart.Arm ? 0.65 : 1.0;

    /// <summary>Kaçınmaya uygulanan çarpan.</summary>
    public double EvasionMultiplier => Part == BodyPart.Leg ? 0.55 : 1.0;

    /// <summary>
    /// Yürüme hızına uygulanan çarpan.
    /// </summary>
    /// <remarks>
    /// Bacağını kaybeden savaşçı yalnızca kaçınmayı değil <b>kaçabilmeyi</b> de kaybeder:
    /// topallayan biri kovalayandan uzaklaşamaz. Uzuv kaybının en ağır ikincil bedeli bu.
    /// </remarks>
    public double SpeedMultiplier => Part == BodyPart.Leg ? 0.60 : 1.0;

    /// <summary>İsabet şansına uygulanan çarpan.</summary>
    public double AccuracyMultiplier => Part == BodyPart.Eye ? 0.75 : 1.0;

    /// <summary>Kol kaybı iki elli silah kullanımını imkânsız kılar.</summary>
    public bool BlocksTwoHandedWeapons => Part == BodyPart.Arm;
}
