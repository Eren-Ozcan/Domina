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

/// <summary>
/// Fırlatılan silah — yakın dövüş silahından ayrı bir yuvada taşınır.
/// </summary>
/// <remarks>
/// <para>
/// GDD §4'ün üçüncü yeterlilik hattı (tek el / çift el / <b>fırlatma</b>) burada
/// karşılığını buluyor. Uykuda tutulmasının sebebi çekirdekte uzam ve mermi olmamasıydı
/// (Açık Karar #4-C); uzam geldi, mermi de bununla geldi.
/// </para>
/// <para>
/// Menzilli saldırının asıl işi <b>mesafeyi bir tehdide çevirmek</b>: yaklaşırken ve
/// kaçarken savaşçı savunmasızdır. Fırlatma olmadan arenanın uzak yarısı güvenli
/// bölgeydi — kaçan savaşçıya kimse dokunamıyordu.
/// </para>
/// </remarks>
/// <param name="Name">Görünen ad.</param>
/// <param name="Damage">Taban hasar. Yakın dövüş silahlarından belirgin düşüktür.</param>
/// <param name="Range">Azami atış mesafesi (arena birimi). Savaşçı boyu 256 birimdir.</param>
/// <param name="Speed">Merminin hızı (birim/saniye); uçuş süresi mesafeden hesaplanır.</param>
/// <param name="Ammo">Bir dövüşte kaç kez atılabileceği. Bitince yalnızca yakın dövüş kalır.</param>
/// <param name="ThrowSeconds">Bir atış döngüsünün toplam süresi.</param>
/// <param name="Class">Yaralanma karakteri — uzuv kopma riskini belirler.</param>
public sealed record ThrownWeapon(
    string Name,
    WeaponClass Class,
    double Damage,
    double Range,
    double Speed,
    int Ammo,
    double ThrowSeconds)
{
    /// <inheritdoc cref="Weapon.DismembermentFactor"/>
    public double DismembermentFactor => Class switch
    {
        WeaponClass.Cutting => 1.0,
        WeaponClass.Piercing => 0.5,
        WeaponClass.Blunt => 0.15,
        _ => 1.0,
    };

    /// <summary>Hızlı, hafif, çok sayıda.</summary>
    public static ThrownWeapon Shuriken() =>
        new("Shuriken", WeaponClass.Cutting, 12, 700, 1400, 4, 0.7);

    /// <summary>Yavaş ve ağır; az sayıda ama ciddi hasar.</summary>
    public static ThrownWeapon ThrowingSpear() =>
        new("Ucu sivri kargı", WeaponClass.Piercing, 26, 520, 900, 2, 1.1);
}

/// <summary>Tek bir bölgeyi örten zırh parçası.</summary>
/// <param name="Name">Görünen ad.</param>
/// <param name="DamageReduction">O bölgeye inen isabette düşülen sabit hasar.</param>
/// <param name="DismembermentResistance">
/// O bölgeye inen ağır darbede uzuv kopma riskini azaltan oran
/// (0 = korumasız, 1 = tam bağışık).
/// </param>
/// <param name="Weight">
/// Parçanın ağırlığı. Takımın toplamı saldırı döngüsünü uzatır
/// (bkz. <c>CombatTuning.ArmorWeightAtFullPenalty</c>).
/// </param>
/// <remarks>
/// Ağırlık, zırhı bir <b>karar</b> yapan şeydir. Bedelsizken ō-yoroi her eksende
/// üstündü — zafer %68'den %96'ya, ölüm %41.6'dan %16.3'e, uzuv kaybı %8.6'dan %0.4'e
/// iniyor ve karşılığında hiçbir şey ödenmiyordu; tek fren fiyattı, o da ekonomi
/// sayıları gelene kadar yok. Ağırlık bedeli sahaya taşır: ağır kuşanan savaşçının
/// yaklaşır ve kılıcı geç iner.
/// </remarks>
public sealed record ArmorPiece(
    string Name,
    double DamageReduction,
    double DismembermentResistance,
    double Weight)
{
    /// <summary>Örtüsüz bölge.</summary>
    public static ArmorPiece Bare { get; } = new("Çıplak", 0, 0, 0);

    public static ArmorPiece Keikogi { get; } = new("Keikogi", 4, 0.20, 1);

    public static ArmorPiece DoMaru { get; } = new("Dō-maru gövdeliği", 9, 0.45, 4);

    public static ArmorPiece OYoroiCuirass { get; } = new("Ō-yoroi gövdeliği", 14, 0.65, 7);

    /// <summary>Kol zırhı — <b>tek</b> kolu örter; iki kol iki parça ister.</summary>
    public static ArmorPiece Kote { get; } = new("Kote", 4, 0.30, 0.75);

    /// <inheritdoc cref="Kote"/>
    public static ArmorPiece HeavyKote { get; } = new("Ağır kote", 6, 0.45, 1.5);

    /// <summary>Baldır zırhı — <b>tek</b> bacağı örter.</summary>
    public static ArmorPiece Suneate { get; } = new("Suneate", 4, 0.25, 0.75);

    /// <inheritdoc cref="Suneate"/>
    public static ArmorPiece HeavySuneate { get; } = new("Ağır suneate", 6, 0.40, 1.5);

    public static ArmorPiece Kabuto { get; } = new("Kabuto", 8, 0.55, 3);
}

/// <summary>
/// Bir savaşçının kuşamı — her bölge için ayrı bir parça.
/// </summary>
/// <remarks>
/// <para>
/// Zırh tek bir skaler değil, <b>yuva yuva</b>dır: hasar azaltımı ve uzuv kopma
/// direnci darbenin indiği bölgenin parçasından okunur (<see cref="At"/>).
/// </para>
/// <para>
/// Sebep: isabet bölgeleri (bkz. <c>CombatTuning</c>) tam olarak bunun için var.
/// Direnç tek sayı olduğu sürece "iyi zırh" tek bir eksende ilerler ve ekipmanın
/// asıl ilginç kararı — <b>ağır göğüslük, çıplak kollar</b>: ucuz ve hızlı, ama eve
/// kolsuz dönme ihtimali yüksek — hiç var olmaz.
/// </para>
/// <para>
/// Yuvalar uzuv uzuvdur: kılıç kolu, boştaki kol, sağ bacak, sol bacak ayrı ayrı
/// kuşanılır. Tek bir "kol" yuvası hem iki kolluğu tek parça sayıyordu hem de kolunu
/// kaybetmiş savaşçının kalan kolunu temsil edemiyordu.
/// </para>
/// </remarks>
/// <param name="Name">Takımın görünen adı.</param>
public sealed record Armor(
    string Name,
    ArmorPiece Head,
    ArmorPiece Torso,
    ArmorPiece SwordArm,
    ArmorPiece OffArm,
    ArmorPiece RightLeg,
    ArmorPiece LeftLeg)
{
    /// <summary>Takımın toplam ağırlığı. Boş yuva ağırlık taşımaz.</summary>
    public double Weight =>
        Head.Weight + Torso.Weight + SwordArm.Weight + OffArm.Weight
        + RightLeg.Weight + LeftLeg.Weight;

    /// <summary>Verilen bölgeyi örten parça.</summary>
    public ArmorPiece At(HitLocation location) => location switch
    {
        HitLocation.Head => Head,
        HitLocation.Torso => Torso,
        HitLocation.SwordArm => SwordArm,
        HitLocation.OffArm => OffArm,
        HitLocation.RightLeg => RightLeg,
        HitLocation.LeftLeg => LeftLeg,
        _ => ArmorPiece.Bare,
    };

    /// <summary>Tüm bölgeleri aynı parçayla örten takım.</summary>
    public static Armor Uniform(string name, ArmorPiece piece) =>
        new(name, piece, piece, piece, piece, piece, piece);

    public static Armor None() => Uniform("Yok", ArmorPiece.Bare);

    /// <summary>Yalnızca gövdeyi örten kumaş. Kollar, bacaklar ve kafa açıkta.</summary>
    public static Armor Light() => new(
        "Hafif keikogi",
        Head: ArmorPiece.Bare,
        Torso: ArmorPiece.Keikogi,
        SwordArm: ArmorPiece.Bare,
        OffArm: ArmorPiece.Bare,
        RightLeg: ArmorPiece.Bare,
        LeftLeg: ArmorPiece.Bare);

    /// <summary>Gövde, iki kol ve iki bacak örtülü; kafa açık.</summary>
    public static Armor Medium() => new(
        "Dō-maru",
        Head: ArmorPiece.Bare,
        Torso: ArmorPiece.DoMaru,
        SwordArm: ArmorPiece.Kote,
        OffArm: ArmorPiece.Kote,
        RightLeg: ArmorPiece.Suneate,
        LeftLeg: ArmorPiece.Suneate);

    /// <summary>Tam takım.</summary>
    public static Armor Heavy() => new(
        "Ō-yoroi",
        Head: ArmorPiece.Kabuto,
        Torso: ArmorPiece.OYoroiCuirass,
        SwordArm: ArmorPiece.HeavyKote,
        OffArm: ArmorPiece.HeavyKote,
        RightLeg: ArmorPiece.HeavySuneate,
        LeftLeg: ArmorPiece.HeavySuneate);
}
