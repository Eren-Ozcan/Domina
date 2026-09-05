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
    /// <summary>
    /// Bu silah, karşısındakinin kavradığı bir hedef sunuyor mu?
    /// </summary>
    /// <remarks>
    /// Yalnızca yumruk için kapalıdır: yakalanacak bir namlu, bir sap yoktur.
    /// Sınıfın kendi zorluğu <see cref="CatchFactor"/>'da; bu bayrak "ortada tutulacak
    /// bir şey var mı" sorusunun cevabı.
    /// </remarks>
    public bool Catchable { get; init; } = true;

    /// <summary>
    /// Bu silahla düşmanın silahını yakalama becerisi. 0 = yakalayamaz.
    /// </summary>
    /// <remarks>
    /// Jitte ve sai'nin var oluş sebebi budur. GDD §4 "kalkan yok" derken bıraktığı
    /// mekanik boşluğu bunlar doldurur: elde taşınan kalkan Japon savaşında yaygın
    /// değildi, ama gelen kılıcı <b>durduran</b> bir alet vardı. Sai'nin üç çatalı
    /// jitte'nin tek çengelinden daha iyi kavrar.
    /// </remarks>
    public double CatchSkill { get; init; }

    /// <summary>Yakalayabilir mi?</summary>
    public bool CanCatch => CatchSkill > 0;

    /// <summary>
    /// Bu silahın <b>yakalanabilirliği</b> — çengele giren namlunun ne kadar tutulduğu.
    /// </summary>
    /// <remarks>
    /// Kesici silah çengele oturur; delici uç kayar; künt sopanın kavranacak keskin
    /// hattı yoktur. Yakalama zarında bu, saldıranın silahından okunur — savunanın
    /// <see cref="CatchSkill"/>'i ile çarpılır.
    /// </remarks>
    public double CatchFactor => !Catchable ? 0 : Class switch
    {
        WeaponClass.Cutting => 1.0,
        WeaponClass.Piercing => 0.7,
        WeaponClass.Blunt => 0.25,
        _ => 1.0,
    };

    /// <summary>
    /// Namluya sürülmüş zehrin gücü. 0 = temiz silah.
    /// </summary>
    /// <remarks>
    /// Zehir, zırhın <b>cevabıdır</b>: kanı zehirleyen doz plakadan okunmaz, deriyi
    /// çizen her vuruşla girer ve zırhın hasar azaltımından da savunma statından da
    /// bağımsız işler. Silahın kendi hasarı bunun bedelini öder — zehirli bıçak açık
    /// dövüşte kısa kalır, karşılığını süreyle alır.
    /// </remarks>
    public double Poison { get; init; }

    /// <summary>Silah zehirli mi?</summary>
    public bool IsPoisoned => Poison > 0;

    /// <summary>
    /// Silahın <b>elden çıkma eğilimi</b> — sert bir temasta kavrayışın bozulma payı.
    /// 0 = düşmez.
    /// </summary>
    /// <remarks>
    /// Zırhın kesiciye karşı ikinci cevabı budur: plakaya saplanan ağız burkulur ve
    /// silahı avuçtan çıkarır; delici uç kayar, künt sopa geri teper ama elde kalır.
    /// Sınıfın kendi değeri <see cref="DisarmFactor"/>'de; bu alan tek tek silahların
    /// sınıflarından ayrılmasına izin verir — şimdilik hiçbiri ayrılmıyor, çünkü kural
    /// önce sınıf ekseninde ölçüldü.
    /// </remarks>
    public double? DisarmFactorOverride { get; init; }

    /// <inheritdoc cref="DisarmFactorOverride"/>
    public double DisarmFactor => DisarmFactorOverride ?? (!Catchable ? 0 : Class switch
    {
        WeaponClass.Cutting => 1.0,
        WeaponClass.Piercing => 0.6,
        WeaponClass.Blunt => 0.2,
        _ => 1.0,
    });

    /// <summary>
    /// Silahın <b>blok kalitesi</b> — duruşa geçen savaşçının darbeyi ne kadar karşıladığı.
    /// </summary>
    /// <remarks>
    /// Bloğun ikinci ekseni budur: duruşa geçme kararı savaşçının Savunma statından çıkar,
    /// duruşun ne kadar tuttuğu <b>elindeki silahtan</b>. Çift el tutulan uzun sap darbeyi
    /// gövdesiyle karşılar; tek elli kısa ağız yalnızca yönünü değiştirir. Yumruk hiçbir
    /// şey karşılamaz — silahını düşüren savaşçı bloğunu da kaybeder.
    /// </remarks>
    public double? BlockFactorOverride { get; init; }

    /// <inheritdoc cref="BlockFactorOverride"/>
    public double BlockFactor => BlockFactorOverride ?? (TwoHanded ? 1.0 : Class switch
    {
        WeaponClass.Blunt => 0.85,
        WeaponClass.Cutting => 0.80,
        WeaponClass.Piercing => 0.70,
        _ => 0.80,
    });

    /// <summary>Uzuv kopma riskine uygulanan çarpan.</summary>
    public double DismembermentFactor => Class switch
    {
        WeaponClass.Cutting => 1.0,
        WeaponClass.Piercing => 0.5,
        WeaponClass.Blunt => 0.15,
        _ => 1.0,
    };

    /// <summary>
    /// Sersemletme riskine uygulanan çarpan.
    /// </summary>
    /// <remarks>
    /// Künt sınıfın karşılığı budur. Kopma çarpanı 0.15 iken künt silah, uzuv kopmayı
    /// oyunun imza mekaniği yapan her şeyden mahrumdu ve karşılığında hiçbir şey
    /// almıyordu (docs/GDD.md §7 bunu "kod ile fark" olarak yazıyordu). Takas artık
    /// gerçek: kesici uzuv koparır, künt savaşçıyı donduran ağır darbeyi indirir.
    /// </remarks>
    public double StunFactor => Class switch
    {
        WeaponClass.Blunt => 1.0,
        WeaponClass.Cutting => 0.25,
        WeaponClass.Piercing => 0.15,
        _ => 0.25,
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

    /// <summary>
    /// Tek çengelli tutma aleti: hasarı düşük, karşılığı gelen kılıcı durdurmak.
    /// </summary>
    /// <remarks>
    /// Katana'nın (22/1.10) yanında 14/1.00 durur — takas budur: yakalama, kaybedilen
    /// hasarı ödemek zorunda. Ödeyip ödemediği ölçülür (<c>katana</c>/<c>jitte</c>
    /// senaryoları).
    /// </remarks>
    public static Weapon Jitte() => new("Jitte", WeaponClass.Blunt, 14, false, 1.00)
    {
        CatchSkill = 1.0,
    };

    /// <summary>
    /// Üç çatallı tutma aleti. Jitte ile aynı hasarı taşır, daha yavaş vurur, daha iyi kavrar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Keskin değildir — künt sınıfta durmasının sebebi budur; sai bir kesme aleti değil,
    /// bir tutma ve dürtme aletidir.
    /// </para>
    /// <para>
    /// 13/1.05 ile başladı ve düpedüz kötüydü (%61.92 zafer, kontrol %73.09): fazladan
    /// kavrayış, kaybedilen hasarı ödemiyordu. 14/1.05'te üçü de yarım puan içinde
    /// (%72.73). Jitte ile arasındaki fark hasar değil <b>hacim</b>: sai dövüş başına
    /// 3.71, jitte 2.75 yakalar — yani kalabalığa karşı sai'nin daha çok işi olmalıdır.
    /// Bu kuşatma ölçümü henüz yapılmadı.
    /// </para>
    /// </remarks>
    public static Weapon Sai() => new("Sai", WeaponClass.Blunt, 14, false, 1.05)
    {
        CatchSkill = 1.25,
    };

    /// <summary>
    /// Kısa bıçak. Zehrin kontrolü: aynı bıçak, temiz namlu.
    /// </summary>
    /// <remarks>
    /// Zehir ölçümü ancak tek farkla yapılabilir. <see cref="PoisonedTanto"/> ile
    /// arasındaki tek fark namluya sürülen doz; hasar, hız ve sınıf aynıdır.
    /// </remarks>
    public static Weapon Tanto() => new("Tantō", WeaponClass.Cutting, 13, false, 0.85);

    /// <summary>
    /// Namlusu zehirlenmiş kısa bıçak — zırhın önünde duran tek silah.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Çelik olarak neredeyse zararsızdır (7, temiz tantō 13 iken): çıktısının çoğu
    /// dozdan gelir, ve zırh dozu okuyamaz. Takas budur — zehir uzuv koparmaz, sersemletmez
    /// ve hemen öldürmez; karşılığında <b>zamanla</b> ödenir.
    /// </para>
    /// <para>
    /// Hasar 13 iken ölçüm yanlış cevap veriyordu: zehir zırhı aşmıyor, yalnızca zayıf bir
    /// bıçağı kurtarıyordu (açık dövüşte %74.00, zırhlı düşmana karşı %55.99 — katana
    /// %73.09 / %68.62). Bıçak 7'ye indirilip doz büyütülünce çıktının %60'ı zehre geçti
    /// ve iddia doğrulandı: %72.19 / <b>%77.19</b>. Zehirlinin karşısında ō-yoroi kuşanmak
    /// artık zarar — plaka dozu durdurmaz, ağırlığı ise vuruşu geciktirir.
    /// </para>
    /// </remarks>
    public static Weapon PoisonedTanto() =>
        Tanto() with { Name = "Zehirli tantō", Damage = 7, Poison = 1.0 };

    /// <summary>Silahsız/uzuv kaybı sonrası düşülen taban.</summary>
    public static Weapon Fists() => new("Yumruk", WeaponClass.Blunt, 8, false, 0.80)
    {
        Catchable = false,
        BlockFactorOverride = 0.30,
    };
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
    /// <inheritdoc cref="Weapon.Poison"/>
    public double Poison { get; init; }

    /// <inheritdoc cref="Weapon.IsPoisoned"/>
    public bool IsPoisoned => Poison > 0;

    /// <inheritdoc cref="Weapon.DismembermentFactor"/>
    public double DismembermentFactor => Class switch
    {
        WeaponClass.Cutting => 1.0,
        WeaponClass.Piercing => 0.5,
        WeaponClass.Blunt => 0.15,
        _ => 1.0,
    };

    /// <inheritdoc cref="Weapon.StunFactor"/>
    public double StunFactor => Class switch
    {
        WeaponClass.Blunt => 1.0,
        WeaponClass.Cutting => 0.25,
        WeaponClass.Piercing => 0.15,
        _ => 0.25,
    };

    /// <summary>Hızlı, hafif, çok sayıda.</summary>
    public static ThrownWeapon Shuriken() =>
        new("Shuriken", WeaponClass.Cutting, 12, 700, 1400, 4, 0.7);

    /// <summary>
    /// Ucu zehirlenmiş shuriken: az sayıda, hasarı aynı, arkasında doz bırakır.
    /// </summary>
    /// <remarks>
    /// Zehrin menzilli hâli. Mermi <b>yakalanamaz</b> (bkz. <see cref="Weapon.CatchSkill"/>),
    /// yani zehirli uç yakalama aletinin de cevabıdır; karşılığında cephane yarıya iner.
    /// </remarks>
    public static ThrownWeapon PoisonedShuriken() =>
        Shuriken() with { Name = "Zehirli shuriken", Ammo = 2, Poison = 1.0 };

    /// <summary>Yavaş ve ağır; az sayıda ama ciddi hasar.</summary>
    public static ThrownWeapon ThrowingSpear() =>
        new("Ucu sivri kargı", WeaponClass.Piercing, 26, 520, 900, 2, 1.1);
}

/// <summary>
/// Yuva yuva zırh yıpranması — bir kuşamın her parçasının emdiği toplam hasar.
/// </summary>
/// <remarks>
/// Sözlük yerine değer türü: dövüş özeti savaşçı başına üretilir ve toplu simülasyon
/// bunu on binlerce kez yapar; her seferinde altı elemanlı bir sözlük ayırmak ölçümün
/// kendisini yavaşlatırdı.
/// </remarks>
public readonly record struct ArmorWearSet(
    double Head = 0,
    double Torso = 0,
    double SwordArm = 0,
    double OffArm = 0,
    double RightLeg = 0,
    double LeftLeg = 0)
{
    public double At(HitLocation location) => location switch
    {
        HitLocation.Head => Head,
        HitLocation.Torso => Torso,
        HitLocation.SwordArm => SwordArm,
        HitLocation.OffArm => OffArm,
        HitLocation.RightLeg => RightLeg,
        HitLocation.LeftLeg => LeftLeg,
        _ => 0,
    };

    /// <summary>Verilen yuvaya yıpranma ekler.</summary>
    public ArmorWearSet With(HitLocation location, double amount) => location switch
    {
        HitLocation.Head => this with { Head = amount },
        HitLocation.Torso => this with { Torso = amount },
        HitLocation.SwordArm => this with { SwordArm = amount },
        HitLocation.OffArm => this with { OffArm = amount },
        HitLocation.RightLeg => this with { RightLeg = amount },
        HitLocation.LeftLeg => this with { LeftLeg = amount },
        _ => this,
    };

    /// <summary>Bütün yuvaların toplamı.</summary>
    public double Total => Head + Torso + SwordArm + OffArm + RightLeg + LeftLeg;
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
/// <param name="Durability">
/// Parçanın <b>ne kadar darbe emebileceği</b>. Emdiği hasar bu havuzdan düşer; havuz
/// bitince parça dağılır ve <b>kalıcı olarak gider</b>.
/// </param>
/// <remarks>
/// <para>
/// Dayanıklılık, zırhı bir <b>sarf malzemesi</b> yapar. Ağırlık zırhın sahadaki bedelini
/// yazıyordu; dayanıklılık dojo'daki bedelini yazar: en iyi kuşam en çok emen kuşamdır,
/// en çok emen de en çabuk tükenendir. Silahtan farkı kasıtlı — düşen silah dövüş
/// sonunda geri gelir, dağılan zırh parçası gelmez.
/// </para>
/// <para>
/// Havuz <b>emilen</b> hasardan düşer, gelen hasardan değil: parçayı yıpratan şey
/// durdurduğu darbedir. Gelen hasardan düşseydi kalın plaka ince kumaşla aynı hızda
/// tükenir, kademe farkı yalnızca sayıda kalırdı.
/// </para>
/// </remarks>
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
    double Weight,
    double Durability = 0)
{
    /// <summary>Bu yuvada gerçekten bir parça var mı?</summary>
    public bool IsWorn => DamageReduction > 0 || DismembermentResistance > 0;

    /// <summary>Örtüsüz bölge.</summary>
    public static ArmorPiece Bare { get; } = new("Çıplak", 0, 0, 0);

    public static ArmorPiece Keikogi { get; } = new("Keikogi", 4, 0.20, 1, Durability: 40);

    public static ArmorPiece DoMaru { get; } = new("Dō-maru gövdeliği", 9, 0.45, 4, Durability: 110);

    public static ArmorPiece OYoroiCuirass { get; } =
        new("Ō-yoroi gövdeliği", 14, 0.65, 7, Durability: 180);

    /// <summary>Kol zırhı — <b>tek</b> kolu örter; iki kol iki parça ister.</summary>
    public static ArmorPiece Kote { get; } = new("Kote", 4, 0.30, 0.75, Durability: 45);

    /// <inheritdoc cref="Kote"/>
    public static ArmorPiece HeavyKote { get; } = new("Ağır kote", 6, 0.45, 1.5, Durability: 75);

    /// <summary>Baldır zırhı — <b>tek</b> bacağı örter.</summary>
    public static ArmorPiece Suneate { get; } = new("Suneate", 4, 0.25, 0.75, Durability: 45);

    /// <inheritdoc cref="Suneate"/>
    public static ArmorPiece HeavySuneate { get; } = new("Ağır suneate", 6, 0.40, 1.5, Durability: 75);

    public static ArmorPiece Kabuto { get; } = new("Kabuto", 8, 0.55, 3, Durability: 90);
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

    /// <summary>Verilen yuvaya başka bir parça takılmış hâli.</summary>
    public Armor With(HitLocation location, ArmorPiece piece) => location switch
    {
        HitLocation.Head => this with { Head = piece },
        HitLocation.Torso => this with { Torso = piece },
        HitLocation.SwordArm => this with { SwordArm = piece },
        HitLocation.OffArm => this with { OffArm = piece },
        HitLocation.RightLeg => this with { RightLeg = piece },
        HitLocation.LeftLeg => this with { LeftLeg = piece },
        _ => this,
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
