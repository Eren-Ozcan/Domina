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

    /// <summary>İsabet şansına uygulanan çarpan.</summary>
    public double AccuracyMultiplier => Part == BodyPart.Eye ? 0.75 : 1.0;

    /// <summary>Kol kaybı iki elli silah kullanımını imkânsız kılar.</summary>
    public bool BlocksTwoHandedWeapons => Part == BodyPart.Arm;
}
