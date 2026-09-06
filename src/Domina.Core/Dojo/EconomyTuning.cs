namespace Domina.Core.Dojo;

/// <summary>Kasanın ayarlanabilir sayıları — fiyatlar, günlük tüketim, ödül.</summary>
/// <remarks>
/// <para>
/// Buradaki sayılar Açık Karar #5'in konusudur. GDD §11 yalnızca <b>kalemleri</b>
/// kilitler (gelir: dövüş ödülü; gider: ekipman, yiyecek/su, ilaç, savaşçı alımı);
/// büyüklükleri ölçümle kapanır. Bu yüzden hepsi tek yerde durur ve
/// <c>Domina.Sim</c> bunları dövüş dövüş oynatarak ölçebilir.
/// </para>
/// <para>
/// Tek para birimi <b>altın</b>dır. Yiyecek, su ve ilaç ambarda sayılabilir stok
/// olarak durur ama piyasadan altınla alınır: ekonominin tek kıt kaynağı bölünmesin,
/// "bugün ne alayım" kararı tek bir sayıya baksın.
/// </para>
/// </remarks>
public sealed record EconomyTuning
{
    /// <summary>Bir savaşçının günlük yiyeceği.</summary>
    public int FoodPerWarriorPerDay { get; init; } = 1;

    /// <summary>Bir savaşçının günlük suyu.</summary>
    public int WaterPerWarriorPerDay { get; init; } = 1;

    /// <summary>Revirdeki bir savaşçının günlük ilacı.</summary>
    public int MedicinePerInfirmaryDay { get; init; } = 1;

    /// <summary>
    /// İlacın o gün fazladan erittiği revir günü.
    /// </summary>
    /// <remarks>
    /// İlaç doğal iyileşmenin <b>üstüne</b> gelir (GDD §7): ilaçsız gün de bir gün
    /// eritir, ilaçlı gün iki. Yoksa ilaç zorunlu bir vergi olurdu, karar değil.
    /// </remarks>
    public int MedicineRecoveryDays { get; init; } = 1;

    public int FoodPrice { get; init; } = 2;

    public int WaterPrice { get; init; } = 1;

    public int MedicinePrice { get; init; } = 12;

    /// <summary>Yeni savaşçının alım bedeli.</summary>
    public int RecruitPrice { get; init; } = 150;

    /// <summary>
    /// Zırh parçasının fiyatı, dayanıklılık puanı başına.
    /// </summary>
    /// <remarks>
    /// Fiyat <see cref="Model.ArmorPiece.Durability"/> ile ölçeklenir: parça ne kadar
    /// hasar durduruyorsa o kadar eder. Koruma oranı ayrıca çarpan değildir — iki sayı
    /// zaten aynı yönde büyüyor, ikisini de çarpmak pahalı ucu iki kez cezalandırırdı.
    /// </remarks>
    public double ArmorGoldPerDurability { get; init; } = 1.5;

    /// <summary>
    /// Onarımın fiyatı, silinen yıpranma puanı başına.
    /// </summary>
    /// <remarks>
    /// <see cref="ArmorGoldPerDurability"/>'nin altında olmak <b>zorunda</b>: eşit ya da
    /// üstünde olsaydı onarım hiçbir zaman mantıklı olmaz, herkes parçayı dağılana kadar
    /// kullanıp yenisini alırdı. Aradaki fark onarımın kâr payıdır; farkın büyüklüğü
    /// "erken onar mı, sonuna kadar kullan mı" kararının tamamıdır.
    /// </remarks>
    public double RepairGoldPerWear { get; init; } = 0.9;

    /// <summary>Zaferin ödülü, düşmanın toplam canı başına altın.</summary>
    /// <remarks>
    /// Ödül karşılaşmanın <b>kendisinden</b> çıkar, dövüşün nasıl geçtiğinden değil:
    /// aynı düşman aynı parayı öder. Zorluk eğrisi (GDD §10) böylece geliri de taşır —
    /// ayrı bir ödül tablosu tutmaya gerek kalmaz.
    /// </remarks>
    public double VictoryGoldPerEnemyHealth { get; init; } = 0.45;

    /// <summary>
    /// Riskin ödüle bindirdiği prim — 0 ise ödül düşman canıyla düz orantılıdır.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Düz orantı bir yerde bozuluyor: üç güçlü düşman, bir zayıf düşmanın üç katı kadar
    /// <b>can</b> taşır ama üç katı kadar <b>risk</b> taşımaz — daha fazlasını taşır
    /// (odaklanan üç düşman aynı anda vurur, kaçış zorlaşır, ölüm ihtimali doğrusal
    /// büyümez). Ödül canla düz orantılı kaldığı sürece eğrinin üst ucu <b>hiçbir zaman</b>
    /// alınmaya değmez ve oyuncu, dojo'su büyüse bile teklifleri geri çevirmeye devam eder.
    /// </para>
    /// <para>
    /// Prim <see cref="RiskFreeEnemyHealth"/>'in üstünde başlar: sıradan bir karşılaşma
    /// eskisi kadar öder, ağırlaşan karşılaşma orantısından fazlasını.
    /// </para>
    /// </remarks>
    public double RiskPremium { get; init; } = 0.25;

    /// <summary>Primin başladığı düşman canı — bunun altı sıradan iş sayılır.</summary>
    public double RiskFreeEnemyHealth { get; init; } = 100;

    /// <summary>Çekilen ya da bozguna uğrayan seferin ödülü.</summary>
    /// <remarks>Sıfır — GDD §10: pes etmek o seferin ödülünü siler.</remarks>
    public int LostBattleGold { get; init; }
}
