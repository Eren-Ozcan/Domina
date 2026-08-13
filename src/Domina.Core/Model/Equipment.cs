namespace Domina.Core.Model;

/// <summary>Silahın yaralanma karakteri.</summary>
public enum WeaponClass
{
    /// <summary>Kesici (katana, nagi). Uzuv kopmasına yol açar.</summary>
    Cutting,

    /// <summary>Künt (tetsubo, sopa). Uzuv kopma riski düşük, sersemletme yüksek.</summary>
    Blunt,

    /// <summary>Delici (yari, mızrak). Menzil avantajlı, uzuv kopması orta.</summary>
    Piercing,
}

/// <summary>Bir savaşçının kuşandığı silah.</summary>
/// <param name="Name">Görünen ad.</param>
/// <param name="Class">Yaralanma karakteri.</param>
/// <param name="Damage">Taban hasar.</param>
/// <param name="TwoHanded">İki el gerektiriyor mu (kolunu kaybeden kullanamaz).</param>
/// <param name="AttackSeconds">Bir saldırı döngüsünün toplam süresi.</param>
public sealed record Weapon(
    string Name,
    WeaponClass Class,
    double Damage,
    bool TwoHanded,
    double AttackSeconds)
{
    /// <summary>Uzuv kopma riskine uygulanan çarpan.</summary>
    public double DismembermentFactor => Class switch
    {
        WeaponClass.Cutting => 1.0,
        WeaponClass.Piercing => 0.5,
        WeaponClass.Blunt => 0.15,
        _ => 1.0,
    };

    /// <summary>
    /// Vuruşun eriştiği mesafe (arena birimi). Savaşçı boyu 256 birimdir.
    /// </summary>
    /// <remarks>
    /// Uzun silah uzaktan vurur ama yavaştır; kısa silah yaklaşmak zorundadır. Menzil
    /// olmasaydı silahlar yalnızca hasar ve hızla ayrışırdı — naginata ile tantō
    /// arasındaki asıl fark bu.
    /// </remarks>
    public double Reach => Class switch
    {
        _ when TwoHanded => 150,
        WeaponClass.Piercing => 130,
        _ => 100,
    };

    public static Weapon Katana() => new("Katana", WeaponClass.Cutting, 22, false, 1.10);

    public static Weapon Nodachi() => new("Nodachi", WeaponClass.Cutting, 34, true, 1.60);

    public static Weapon Yari() => new("Yari", WeaponClass.Piercing, 25, true, 1.35);

    public static Weapon Tetsubo() => new("Tetsubo", WeaponClass.Blunt, 30, true, 1.55);

    /// <summary>Silahsız/uzuv kaybı sonrası düşülen taban.</summary>
    public static Weapon Fists() => new("Yumruk", WeaponClass.Blunt, 8, false, 0.80);
}

/// <summary>Zırh. Hasarı azaltır ve uzuv kopma riskini düşürür.</summary>
/// <param name="Name">Görünen ad.</param>
/// <param name="DamageReduction">Her isabette düşülen sabit hasar.</param>
/// <param name="DismembermentResistance">
/// Uzuv kopma riskini azaltan oran (0 = korumasız, 1 = tam bağışık).
/// Ekipmana yatırımı anlamlı kılan ana kalem.
/// </param>
public sealed record Armor(string Name, double DamageReduction, double DismembermentResistance)
{
    public static Armor None() => new("Yok", 0, 0);

    public static Armor Light() => new("Hafif keikogi", 3, 0.20);

    public static Armor Medium() => new("Dō-maru", 7, 0.40);

    public static Armor Heavy() => new("Ō-yoroi", 12, 0.60);
}
