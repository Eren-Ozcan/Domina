namespace Domina.Core.Combat;

/// <summary>
/// Dövüşün tüm ayarlanabilir sayıları tek yerde.
/// </summary>
/// <remarks>
/// Bu değerler <b>denge için tahmini başlangıç noktalarıdır</b>, kanıtlanmış
/// değerler değil. Faz 9'da <c>Domina.Sim</c> toplu simülasyonuyla ayarlanacak
/// (bkz. docs/GDD.md → Açık Karar #8).
/// </remarks>
public sealed record CombatTuning
{
    /// <summary>Simülasyon adımı. 20 Hz, saldırı pencerelerini ayırt etmeye yeter.</summary>
    public double TickSeconds { get; init; } = 0.05;

    /// <summary>Dövüş bu süreyi aşarsa berabere sayılır.</summary>
    public double MaxBattleSeconds { get; init; } = 180;

    // ---- Arena ve hareket ----

    /// <summary>Arenanın hat boyunca genişliği.</summary>
    public double ArenaWidth { get; init; } = 1920;

    /// <summary>Arenanın derinliği. Kuşatma ve çevirme bu eksende olur.</summary>
    public double ArenaDepth { get; init; } = 420;

    /// <summary>Takımların başlangıçta merkeze uzaklığı.</summary>
    public double StartOffsetX { get; init; } = 480;

    /// <summary>Aynı takımdaki savaşçıların başlangıçtaki derinlik aralığı.</summary>
    public double StartSpacingY { get; init; } = 120;

    /// <summary>Hız statı 0 iken yürüme hızı (birim/saniye).</summary>
    /// <remarks>
    /// Hız tek bir sabitken kovalayan ile kaçan aynı hızda gidiyordu; net kapanma sıfır
    /// olduğu için <b>kaçış her zaman başarılıydı</b>. Uçlar 50'de eski sabite (240)
    /// denk gelecek şekilde seçildi, böylece mevcut denge tabanı korunuyor.
    /// </remarks>
    public double MoveSpeedAtZeroSpeed { get; init; } = 150;

    /// <inheritdoc cref="MoveSpeedAtZeroSpeed"/>
    public double MoveSpeedAtMaxSpeed { get; init; } = 330;

    /// <summary>Kaçanın hız çarpanı — sırtı dönük, dengesi bozuk.</summary>
    /// <remarks>
    /// Kovalamacanın tek ayar düğmesi bu. 0.85 çok sert: kaçan hiç arayı açamıyor ve
    /// temastan sonra basılan tuş neredeyse her zaman en az bir ölüye mal oluyordu
    /// (%56 kısmi kaçış). 0.92'de erken basmak hâlâ işe yarıyor, geç basmak yakıyor.
    /// </remarks>
    public double RetreatSpeedMultiplier { get; init; } = 0.92;

    /// <summary>
    /// Savaşçılar birbirine bundan daha fazla sokulamaz — üst üste binmeyi engeller.
    /// </summary>
    public double PersonalSpace { get; init; } = 74;

    /// <summary>Menzile girdikten sonra ne kadar daha yaklaşılacağı (0-1).</summary>
    /// <remarks>
    /// 1.0 tam menzilde durur; menzil sınırında durmak vuruşları ıskalatır çünkü hedef
    /// de hareket ediyor. Biraz içeri girmek daha kararlı.
    /// </remarks>
    public double PreferredReachFraction { get; init; } = 0.85;

    /// <summary>Arkadan gelen saldırının isabet şansına eklediği.</summary>
    /// <remarks>
    /// Kuşatmanın mekanik karşılığı bu: çevrildiğinde birileri mutlaka arkanda kalır.
    /// </remarks>
    public double FlankHitBonus { get; init; } = 0.25;

    /// <summary>Arkadan gelen saldırının hasar çarpanı.</summary>
    public double FlankDamageMultiplier { get; init; } = 1.25;

    /// <summary>Kaçan savaşçının arenayı terk etmiş sayılması için gereken mesafe.</summary>
    public double ExitMargin { get; init; } = 220;

    // ---- Hücum ----

    /// <summary>
    /// Hücumun düşünülebilmesi için hedefe olması gereken en az mesafe.
    /// </summary>
    /// <remarks>
    /// Bunun altında hücum yok: zaten menzile yürünüyor, koşmanın kazandıracağı bir şey
    /// olmadığı gibi savunmayı bırakmanın da karşılığı olmazdı (docs/GDD.md §4).
    /// </remarks>
    public double ChargeMinDistance { get; init; } = 320;

    /// <summary>Mesafe uygunken her <c>Idle</c> kararında hücuma kalkma olasılığı.</summary>
    public double ChargeChance { get; init; } = 0.35;

    /// <summary>Hücum sırasındaki hız çarpanı.</summary>
    public double ChargeSpeedMultiplier { get; init; } = 1.6;

    /// <summary>
    /// Hücumla varılan ilk vuruşun hasar çarpanı — momentumun karşılığı.
    /// </summary>
    /// <remarks>
    /// Uzuv kaybı riski hasar/maxHP oranından geldiği için (docs/GDD.md §7) hücumun
    /// sakatlama olasılığını artırması buradan <b>kendiliğinden</b> çıkar; ayrı bir
    /// kopma çarpanı yoktur.
    /// </remarks>
    public double ChargeDamageMultiplier { get; init; } = 1.5;

    /// <summary>
    /// Hücum bu kadar sürerse hedefe varılamamış sayılır ve hamle boşa gider.
    /// </summary>
    /// <remarks>
    /// Zaman sınırı olmasaydı hücum, kaçan bir hedefi süresiz kovalayan kalıcı bir hız
    /// bonusuna dönerdi ve §5'in kaçış dengesini yıkardı.
    /// </remarks>
    public double ChargeMaxSeconds { get; init; } = 4.0;

    // ---- Saldırı ritmi ----

    /// <summary>Saldırganlık 0 iken saldırılar arası bekleme.</summary>
    public double SpacingSecondsAtZeroAggression { get; init; } = 1.4;

    /// <summary>Saldırganlık 100 iken saldırılar arası bekleme.</summary>
    public double SpacingSecondsAtMaxAggression { get; init; } = 0.30;

    /// <summary>Saldırı süresinin kesilemez (windup) kısmı; kalanı toparlanmadır.</summary>
    public double WindupFraction { get; init; } = 0.6;

    // ---- İsabet ve kaçınma ----

    public double BaseHitChance { get; init; } = 0.55;
    public double AccuracyHitBonus { get; init; } = 0.004;

    /// <summary>Kaçınma 100 iken kaçınma şansı.</summary>
    public double MaxEvasionChance { get; init; } = 0.45;

    /// <summary>Çekilirken savunmasızlık: kaçınma/blok yok, üstüne isabet bonusu.</summary>
    public double RetreatingHitBonus { get; init; } = 0.30;

    /// <summary>Fırlatmanın taban isabet şansı; yakın dövüşten düşüktür.</summary>
    /// <remarks>
    /// Menzilli saldırı bedava olmamalı: uzaktan vurabilmenin bedeli, daha sık ıskalamak
    /// ve daha az hasar. Yoksa herkesin cebine shuriken koymak baskın strateji olurdu.
    /// </remarks>
    public double BaseThrowHitChance { get; init; } = 0.40;

    /// <summary>Menzilin tam ucunda isabetin düştüğü oran.</summary>
    /// <remarks>
    /// Menzilin dibinden atmak neredeyse yakın dövüş kadar isabetli, ucundan atmak
    /// umut atışıdır — mesafenin iki yönlü bir karar olmasını sağlayan şey bu.
    /// </remarks>
    public double ThrowFalloffAtMaxRange { get; init; } = 0.55;

    // ---- Hasar ----

    /// <summary>Güç 100 iken silah hasarına uygulanan çarpan.</summary>
    public double StrengthDamageBonusAtMax { get; init; } = 0.8;

    /// <summary>Savunma 100 iken hasarın azaldığı oran.</summary>
    public double MaxDefenseReduction { get; init; } = 0.45;

    public double MinimumDamage { get; init; } = 1;

    // ---- Stamina ----

    public double AttackStaminaCost { get; init; } = 6;
    public double DodgeStaminaCost { get; init; } = 12;
    public double StaminaRegenPerSecond { get; init; } = 4;

    /// <summary>Bu oranın altında stamina kalınca hasar ve isabet düşer.</summary>
    public double LowStaminaThreshold { get; init; } = 0.3;
    public double LowStaminaPenalty { get; init; } = 0.65;

    // ---- Uzuv kaybı ----

    /// <summary>
    /// Tek darbenin azami cana oranı bunu aşarsa "ağır darbe" sayılır ve uzuv
    /// kopma zarı atılır. Düşük can ÖN KOŞUL DEĞİL — ilk darbede de olabilir
    /// (bkz. docs/GDD.md §7).
    /// </summary>
    /// <remarks>
    /// 0.28'den 0.20'ye indirildi: uzuv kaybı oyunun imza mekaniği ama ölçümde
    /// binde birkaça düşmüştü. Eşik silah hasarlarının kümelendiği yerin hemen
    /// altına çekildi — 0.24 ile 0.28 arasında hiçbir fark yok, çünkü aradaki
    /// aralığa düşen darbe yok.
    /// </remarks>
    public double GrievousSeverityThreshold { get; init; } = 0.20;

    /// <summary>Ağır darbede taban uzuv kopma şansı; silah ve zırh bunu ölçekler.</summary>
    /// <remarks>
    /// 0.35'ten 0.05'e indirildi. 0.35, kopmanın <b>yalnızca kaçış penceresinde</b>
    /// ateşlendiği eski sonuç ağacına göre ayarlanmıştı; ağaç ikiye ayrılıp öldürmeyen
    /// ağır darbe de koparmaya başlayınca (docs/GDD.md §7) aynı sayı uzuv kaybını
    /// %45'e çıkardı. Tarandı (3v3, 10.000 dövüş, <c>losing:0.7</c>) — ölüm ve zafer
    /// oranları bu knobla kayda değer biçimde oynamıyor, yalnızca uzuv kaybı ölçekleniyor.
    /// </remarks>
    public double BaseDismembermentChance { get; init; } = 0.05;

    /// <summary>
    /// Öldürücü darbeden "Kaç" tuşuyla kurtulan savaşçının kalan canı.
    /// </summary>
    /// <remarks>
    /// Tuş ölümü uzuv kaybına çevirir (docs/GDD.md §7) ama sağlık vermez: savaşçı
    /// kaçışın geri kalanını bir sonraki darbede ölecek durumda geçirir. Kurtuluş
    /// garanti değil, sadece bir şans.
    /// </remarks>
    public double SurvivalHealthAfterIntervention { get; init; } = 1;

    // ---- İsabet bölgesi ----

    /// <summary>
    /// Darbenin nereye ineceğinin ağırlıkları. Birbirine göre okunur, toplamları 1
    /// olmak zorunda değil.
    /// </summary>
    /// <remarks>
    /// Gövde kasıtlı olarak baskın: bölgeler eşit olsaydı gövde zırhı, dört zırh
    /// parçasından yalnızca biri olduğu için değersizleşirdi.
    /// </remarks>
    public double TorsoHitWeight { get; init; } = 45;

    /// <inheritdoc cref="TorsoHitWeight"/>
    public double LegHitWeight { get; init; } = 25;

    /// <inheritdoc cref="TorsoHitWeight"/>
    public double ArmHitWeight { get; init; } = 20;

    /// <inheritdoc cref="TorsoHitWeight"/>
    public double HeadHitWeight { get; init; } = 10;

    // ---- Çekilme ----

    /// <summary>Kaçış komutundan arenadan çıkışa kadar geçen savunmasız süre.</summary>
    public double RetreatSeconds { get; init; } = 1.2;

    /// <summary>
    /// Arenayı terk ederken kaza yarası alma şansı.
    /// </summary>
    /// <remarks>
    /// Kaçışın soyut bedeli: burkulan ayak, dönüş yolunda kanayan yara. Öldürmez
    /// (bkz. <c>Battle.RollEscapeMishap</c>), yalnızca "bedelsiz çıkış" diye bir şey
    /// kalmasın diye vardır — temastan önce basılan tuş bunsuz %100 temiz çıkış veriyordu.
    /// </remarks>
    public double EscapeMishapChance { get; init; } = 0.30;

    /// <inheritdoc cref="EscapeMishapChance"/>
    public double EscapeMishapMinDamage { get; init; } = 3;

    /// <inheritdoc cref="EscapeMishapChance"/>
    public double EscapeMishapMaxDamage { get; init; } = 12;

    public static CombatTuning Default { get; } = new();
}
