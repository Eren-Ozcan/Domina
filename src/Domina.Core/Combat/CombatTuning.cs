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
    /// Mesafe uygunken hücuma kalkma olasılığı — <b>Saldırganlık 0 iken</b>.
    /// </summary>
    /// <remarks>
    /// Hücum kararı savaşçının kimliğinden çıkar: atılgan olan atılır, ölçülü olan
    /// mesafeyi yürüyerek kapatır. Saldırganlık zaten saldırı sıklığını belirliyor
    /// (<see cref="SpacingSecondsAtZeroAggression"/>); aynı stat'ın ikinci işi budur.
    /// </remarks>
    public double ChargeChanceAtZeroAggression { get; init; } = 0.12;

    /// <inheritdoc cref="ChargeChanceAtZeroAggression"/>
    /// <remarks>
    /// <para>
    /// Fırsat değerlendirmesi <b>ne zaman</b> hücum edilebileceğini söyler; bu eğri
    /// <b>hangi savaşçının</b> o fırsatı kullandığını. Boşluk açıldığında ölçülü savaşçı
    /// çoğu zaman yürümeyi seçer, atılgan olan atlar.
    /// </para>
    /// <para>
    /// Ölçüldü (3v3): 0.12-0.45 dövüş başına 2.32 kalkış / <b>1.71 tamamlanmış hücum</b>
    /// veriyor. Eğri tek başına sıklık düğmesidir — 0.30-1.00'de 2.80 kalkışa çıkıyor ama
    /// hücumun 3v3 zaferine katkısı +%9'a fırlıyor; 0.06-0.25'te 1.00'e iniyor.
    /// </para>
    /// </remarks>
    public double ChargeChanceAtMaxAggression { get; init; } = 0.45;

    /// <summary>
    /// Koşu başlamadan önce yerinde geçirilen birikme süresi. Savaşçı bu sürede yerinden
    /// kıpırdamaz ve <b>yediği ilk isabetle hücum dağılır</b> (docs/GDD.md §4).
    /// Savunması normal oranıyla sürer — kaçınabildiği darbe hamlesini götürmez.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bu süre, hücum için <b>gereken mesafeyi belirleyen şeydir</b>:
    /// birikirken düşman yürümeye devam eder, ve sana yetişmesi
    /// <c>(mesafe − düşmanın menzili) ÷ düşmanın hızı</c> kadar sürer. Süre bundan
    /// uzunsa hücum daha kalkmadan dağılır.
    /// </para>
    /// <para>
    /// Ölçüldü: 320 birimden Tengu 0.73 sn'de, Kappa 0.88 sn'de, Oni 0.87 sn'de yetişiyor.
    /// 0.75 sn bu yüzden seçildi — <b>yalnızca hızlı düşman</b> eşikten kalkan bir hücumu
    /// bozabilir, ve hız stat'ı ilk kez "hücumu bozan şey" olarak iş görür.
    /// </para>
    /// </remarks>
    public double ChargeWindupSeconds { get; init; } = 0.75;

    // Not: hücumun bir "en az mesafe" ayarı YOKTUR ve olmamalıdır. Savaşçı sabit bir eşiğe
    // bakmaz, boşluğa bakar: "şu an kimse bana vuramıyor ve birikmemi tamamlayacak kadar
    // vaktim var mı?" Gereken mesafe bundan türer —
    //     düşmanın menzili + düşmanın hızı × ChargeWindupSeconds
    // — yani her düşman için ayrı çıkar. Ölçüm bunu doğruladı: elle 320'ye kilitlenmiş olan
    // eski sabit, bu formülün mevcut kadro için ürettiği 287-327 bandının tam ortasıydı.
    // Aynı sebeple ayrı bir kalabalık kısıntısı da yok: üç düşman yetişiyorsa boşluk yoktur.

    /// <summary>Hücum sırasındaki hız çarpanı.</summary>
    /// <remarks>
    /// <b>Ölçüldü: bu eksen dengeye neredeyse hiç dokunmuyor</b> (3v3 zaferi 1.0'da
    /// %86.5, 1.6'da %85.4, 3.0'da %83.7 — yüksek hız hafifçe aleyhte, çünkü daha erken
    /// varmak düşman hattına daha erken girmek demek). Bu yüzden bir denge düğmesi değil
    /// <b>sunum düğmesidir</b>: hücum ekranda hücum gibi görünsün diye 1.6.
    /// </remarks>
    public double ChargeSpeedMultiplier { get; init; } = 1.6;

    /// <summary>
    /// Arenanın azami yürüme hızında koşan bir savaşçının varış vuruşuna eklenen hasar
    /// oranı. Gerçek çarpan <c>1 + (varış hızı ÷ MoveSpeedAtMaxSpeed) × bu sayı</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Momentum hızdır.</b> Çarpan sabit değil, savaşçının varış anındaki gerçek
    /// hızından çıkar — yani hem <see cref="ChargeSpeedMultiplier"/> hem de savaşçının
    /// <c>Speed</c> stat'ı hasara işler. Ağır Oni'nin hücumu, Tengu'nunki kadar sert
    /// olamaz.
    /// </para>
    /// <para>
    /// Bu, ölçümde <b>atıl</b> çıkmış olan hız eksenini canlıya çevirir ve <c>Speed</c>
    /// stat'ına dojo'da ikinci bir iş verir: o güne kadar çekirdekte yalnızca temel yürüme
    /// hızını belirliyordu. Kalıp Mount &amp; Blade'in couched lance'ından geliyor — orada
    /// da hasar atın hızına bağlıdır (bkz. docs/DESIGN-REFERENCES.md §3).
    /// </para>
    /// <para>
    /// Uzuv kaybı riski hasar/maxHP oranından geldiği için (docs/GDD.md §7) hücumun
    /// sakatlama olasılığı buradan <b>kendiliğinden</b> çıkar; ayrı bir kopma çarpanı yok.
    /// </para>
    /// </remarks>
    public double ChargeDamageAtFullSpeed { get; init; } = 0.43;

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
