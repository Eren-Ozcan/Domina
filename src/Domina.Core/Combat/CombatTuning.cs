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
    /// Açıklık doğduğunda hücuma kalkma olasılığı — <b>Saldırganlık 0 iken</b>.
    /// </summary>
    /// <remarks>
    /// Hücum kararı savaşçının kimliğinden çıkar: atılgan olan atılır, ölçülü olan
    /// mesafeyi yürüyerek kapatır. Saldırganlık zaten saldırı sıklığını belirliyor
    /// (<see cref="SpacingSecondsAtZeroAggression"/>); aynı stat'ın ikinci işi budur.
    /// Zar <b>açıklık başına bir kez</b> atılır (docs/GDD.md §4), o yüzden bu sayılar
    /// "saniyede bir denenen şans" değil, <b>gördüğü fırsatların kaçını kullandığı</b>.
    /// </remarks>
    public double ChargeChanceAtZeroAggression { get; init; } = 0.35;

    /// <inheritdoc cref="ChargeChanceAtZeroAggression"/>
    /// <remarks>
    /// <para>
    /// Fırsat değerlendirmesi <b>ne zaman</b> hücum edilebileceğini söyler; bu eğri
    /// <b>hangi savaşçının</b> o fırsatı kullandığını. Boşluk açıldığında ölçülü savaşçı
    /// çoğu zaman yürümeyi seçer, atılgan olan atlar.
    /// </para>
    /// <para>
    /// Ölçüldü (3v3): 0.35-1.00 dövüş başına 1.88 kalkış / <b>1.66 tamamlanmış hücum</b>
    /// veriyor — zar açıklık başına atıldığından bu, saniye başına atılan eski 0.12-0.45
    /// bandının ürettiği sıklığın (1.71 tamamlanmış) yerini tutar. Eğri tek başına sıklık
    /// düğmesidir: 0.12-0.45 aynı kuralla 0.78 kalkışa iner, 0.50-1.00 ise 2.15'e çıkar.
    /// </para>
    /// <para>
    /// Üst uç <b>1.00</b>: en atılgan savaşçı gördüğü her açıklığı kullanır. Bandın alt ucu
    /// da yükseldiği için Saldırganlık'ın ayırt etme gücü daraldı (eski oran 3.75 kat, yeni
    /// 2.86 kat) — bunun karşılığında hücum sıklığı savaşçının hızından bağımsızlaştı ve
    /// <c>Speed</c> ekseni ilk kez canlandı (3v3 zaferi Hız 0'da %83.8, Hız 100'de %87.1).
    /// </para>
    /// </remarks>
    public double ChargeChanceAtMaxAggression { get; init; } = 1.00;

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
    /// Hücumun <b>hedefinin</b> karşı vuruş yapma olasılığı. Yoldan geçilen diğer
    /// düşmanlar bedava vuruşlarını her zaman alır; bu sayı yalnızca cepheden gelene
    /// bakan savaşçı içindir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ayrımın sebebi zamanlamadır (docs/GDD.md §4): yanından koşarak geçen bir gövdeye
    /// vurmak kolaydır, üstüne gelen bir gövdeyi tam anında karşılamak zordur. Bu yüzden
    /// hedefin karşı vuruşu normal dövüş sekansındaki gibi kesin değil, <b>seyrek</b>.
    /// </para>
    /// <para>
    /// Ölçüm bu sayıya bir <b>taban</b> koydu, ve tabanı koyan şey hücum değil kaçış
    /// kuralı: hedefin topladığı karşı vuruşlar, sayıca azalan tarafın başlıca geliri
    /// ve sayı üstünlüğünün çığa dönmesini engelleyen şey. Altına inildiğinde
    /// docs/GDD.md §5'in "çekmek ölümü azaltır" vaadi <b>tersine dönüyor</b> (3v3,
    /// 20.000 dövüş: 0.25'te çeken %41.5, çekmeyen %40.0). 0.6 bu tabanın kendisi —
    /// zevkle değil kısıtla seçildi: çeken %39.6, çekmeyen %40.3.
    /// </para>
    /// <para>
    /// Kurguyu taşıyan şey oran değil, karşı vuruşun <b>sonucu</b>: tuttuğunda hücumun
    /// momentumu söner (bkz. <c>Combatant.ChargeMomentumBroken</c>). Nadirlik yerine
    /// ağırlık — ve yeni bir ayar sayısı doğurmadan.
    /// </para>
    /// </remarks>
    public double ChargeTargetCounterChance { get; init; } = 0.6;

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

    // ---- Zırh ağırlığı ----

    /// <summary>
    /// Cezaların tamamının uygulandığı toplam kuşam ağırlığı. Tam ō-yoroi budur;
    /// daha hafif kuşamlar cezayı oranla alır.
    /// </summary>
    /// <remarks>
    /// Zırhın dövüş içi bedeli yoktu ve ō-yoroi her eksende üstündü: zafer %68 → %96,
    /// ölüm %41.6 → %16.3, uzuv kaybı %8.6 → %0.4, karşılığında sıfır. Tek fren fiyattı,
    /// o da ekonomi sayıları gelene kadar yok. Ağırlık, §7'nin vaat ettiği "ağır
    /// göğüslük, çıplak kollar" kararının sahadaki karşılığıdır.
    /// </remarks>
    public double ArmorWeightAtFullPenalty { get; init; } = 16;

    /// <summary>Tam ağırlıkta saldırı döngüsünün uzama oranı.</summary>
    /// <remarks>
    /// <para>
    /// Ağırlığın <b>tek</b> hattı budur, ve öyle olması ölçümle geldi. Denenip düşen iki
    /// hat: stamina toparlanmasına yazılan ceza <b>hiç</b> ölçülmedi (%90 kesintide zafer
    /// %92.34 → %92.33), yürüme hızına yazılan ceza ise zaferi kıpırdatmadığı hâlde §5'in
    /// vaadini sildi — kuşanmış savaşçı arenayı terk edemeden yetişildiği için "Kaç" tuşu
    /// ölümü düşürmez oldu (çeken %46.35, çekilmeyen %46.44; ceza yokken %44.32'ye karşı
    /// %46.33). Sebep: dövüş hasar alışverişiyle bitiyor ve iki hat da o alışverişe
    /// dokunmuyordu. Kılıcın yavaşlaması doğrudan hasar çıktısına iner.
    /// </para>
    /// <para>
    /// 0.75 seçildi çünkü takasın döndüğü eşik orası (3v3, 20.000 dövüş,
    /// <c>losing:0.7</c>): dō-maru dövüşü kazanır (%71.8 zafer, %40.3 ölüm), ō-yoroi
    /// sakat dönmemeyi alır (uzuv kaybı %0.82'ye karşı %3.38). Üç kademe de bir şeyde
    /// en iyi olur. 0.60'ta ō-yoroi hâlâ her eksende önde (%76.3 / %37.0), 0.90'da
    /// ağır kuşam düpedüz kötü (%64.3 zafer).
    /// </para>
    /// </remarks>
    public double ArmorAttackSlowdownAtFullWeight { get; init; } = 0.75;

    // ---- Sersemletme ----

    /// <summary>
    /// Tek darbenin azami cana oranı bunu aşarsa sersemletme zarı atılır.
    /// </summary>
    /// <remarks>
    /// Uzuv kopma eşiğiyle (<see cref="GrievousSeverityThreshold"/>) aynı yerden başlar
    /// ama ayrı bir düğmedir: ikisi <b>aynı ağır darbenin</b> iki ayrı sonucudur ve
    /// künt/kesici takasının nereden döndüğü ancak ayrı ayrı taranarak bulunur.
    /// </remarks>
    /// <remarks>
    /// Tarandı (<c>blade</c>/<c>club</c>, 20.000 dövüş, <c>losing:0.7</c>): 0.20'de iki
    /// sınıf başa baş (kesici %92.06, künt %92.08 zafer). 0.30'da sersemletme neredeyse
    /// hiç ateşlenmiyor ve künt yine geriye düşüyor (%89.07'ye karşı %91.55) — kuralın
    /// çözdüğü sorun aynen geri geliyor. 0.10'da <b>kesici silah da</b> sersemletmeye
    /// başlıyor (savaşçı başına 0.26 yenen) ve iki taraf birden zayıflıyor.
    /// </remarks>
    public double StunSeverityThreshold { get; init; } = 0.20;

    /// <summary>Ağır darbede taban sersemletme şansı; silah, bölge ve zırh bunu ölçekler.</summary>
    /// <remarks>
    /// <para>
    /// 0.35, takasın <b>tam döndüğü</b> yer: künt silah kopma çarpanında kaybettiğini
    /// (0.15'e karşı 1.0) burada geri alır. Ölçüldü (aynı savaşçı, aynı düşman, yalnızca
    /// silah farklı — <c>blade</c>/<c>club</c>, 20.000 dövüş): kural yokken kesici %91.57,
    /// künt %88.68 zafer alıyordu; künt silah <b>her eksende</b> kötüydü. 0.35'te ikisi
    /// %92.06 / %92.08. 0.60'ta künt öne geçiyor (%93.83), 1.00'da düpedüz baskın (%95.67).
    /// </para>
    /// <para>
    /// Bedeli oyuncu da öder: 3v3'te Oni'nin tetsubo'su artık ısırıyor, oyuncu zaferi
    /// %69.31'den %65.20'ye iniyor. Mutlak denge Faz 9'un işi; buradaki sayı sınıflar
    /// arası <b>oranı</b> tutuyor.
    /// </para>
    /// </remarks>
    public double BaseStunChance { get; init; } = 0.35;

    /// <summary>Sersemleyen savaşçının donduğu süre.</summary>
    /// <remarks>
    /// Sersemletme <b>hamleyi değil savaşçıyı</b> durdurur: yürümez, vurmaz, kaçınmaz.
    /// Süre saldırı döngüsünden kısa tutulur — uzun süre, sersemleten tarafın bedava
    /// bir infaz penceresi kazanması demek olurdu.
    /// </remarks>
    /// <remarks>
    /// Ölçüm şaşırtıcı çıktı: 0.5 ile 0.9 arasında <b>hiçbir fark yok</b> (künt zaferi
    /// %92.06 / %92.08). Sebep, sersemlemenin bu bantta çoğunlukla savaşçının zaten
    /// beklemekte olduğu boşluğa denk gelmesi — kuralın ısıran tarafı kaybedilen hamle
    /// değil, <b>kapanan kaçınma</b>. Diş 1.0 saniyenin üstünde çıkıyor: 1.4'te künt
    /// %94.16'ya fırlıyor. 0.9, o eşiğin hemen altında ve ekranda okunacak kadar uzun
    /// olduğu için seçildi.
    /// </remarks>
    public double StunSeconds { get; init; } = 0.9;

    /// <summary>Kafaya inen darbenin sersemletme şansına uyguladığı çarpan.</summary>
    /// <remarks>
    /// Kabuto'nun dövüş içi karşılığı budur. Bölge ağırlıkları (§7) kafayı zaten nadir
    /// yapıyor; nadir olanın ağır sonucu olmazsa miğfer yalnızca bir hasar sayısıdır.
    /// </remarks>
    /// <remarks>
    /// Ölçüldü (3v3, 20.000 dövüş): çarpan 1.0'da savaşçı başına 0.35, 2.0'da 0.39,
    /// 3.0'da 0.42 sersemleme. Eksen çalışıyor ama yumuşak — kafa isabet ağırlığı 10
    /// olduğu için burada çok büyük bir sayı, nadir bir olayı büyütmekten öteye geçmez.
    /// </remarks>
    public double StunHeadMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Zırhın uzuv kopmaya karşı direncinin ne kadarı sersemletmeye de sayılır.
    /// </summary>
    /// <remarks>
    /// Plaka kesiği durdurduğu kadar darbeyi durdurmaz — künt kuvvet zırhın altından
    /// geçer. Ayrı bir <c>ArmorPiece</c> alanı yerine tek bir pay kullanılmasının sebebi,
    /// zırhın iki direnci arasındaki farkın <b>ölçülebilir tek sayı</b> kalması.
    /// </remarks>
    /// <remarks>
    /// Ölçüldü (3v3, tam kuşam, 20.000 dövüş): pay 0'da savaşçı başına 0.51, 0.6'da 0.33,
    /// 1.0'da 0.22 sersemleme. 0.6 seçildi çünkü 4-D'nin kademe takası ayakta kalıyor —
    /// dō-maru her payda daha az ölüm veriyor (%43.78'e karşı %44.15), ō-yoroi uzvu
    /// koruyor (%0.83'e karşı %3.43). Pay 1.0 zırhı künt silaha karşı fazla iyi yapardı
    /// ve künt sınıfın tek kazancını en pahalı kuşamın önünde silerdi.
    /// </remarks>
    public double ArmorStunResistanceShare { get; init; } = 0.6;

    // ---- Kılıç yakalama ----

    /// <summary>
    /// Yakalama aletiyle gelen vuruşu tutma taban şansı; silah, kavrayış ve isabet
    /// bunu ölçekler.
    /// </summary>
    /// <remarks>
    /// GDD §4 kalkanı reddederken "aynı mekanik ihtiyacı jitte/sai karşılar" diyordu;
    /// karşılığı kodda yoktu. Yakalama, savunmanın kaçınmadan sonraki <b>ikinci</b>
    /// eksenidir: kaçınma darbeyi ıskalatır ve orada biter, yakalama darbeyi durdurur
    /// <b>ve</b> saldıranı açıkta bırakır.
    /// </remarks>
    public double BaseCatchChance { get; init; } = 0.24;

    /// <summary>Yakalamanın stamina bedeli. Yalnızca tutan zar için ödenir.</summary>
    /// <remarks>
    /// Kaçınmadan (<see cref="DodgeStaminaCost"/>) pahalıdır: kaçınma savaşçıyı olduğu
    /// yerden çeker, yakalama karşıdakinin bütün ağırlığını tutar. Pahalı olmasaydı
    /// yakalama aleti bedava bir ikinci savunma katmanı olurdu.
    /// </remarks>
    public double CatchStaminaCost { get; init; } = 16;

    /// <summary>Silahı yakalanan saldıranın açıkta kaldığı süre.</summary>
    /// <remarks>
    /// Kural bu süreyle yaşar: yakalama yalnızca hasarı silseydi zayıf bir kaçınma
    /// olurdu. Asıl karşılık, saldıranın kilitlendiği ve <b>kaçınamadığı</b> penceredir —
    /// yakalayanın kendi düşük hasarı bu pencerede telafi edilir.
    /// </remarks>
    public double CatchBindSeconds { get; init; } = 0.6;

    /// <summary>Çift el silahla gelen vuruşun yakalanma şansına uygulanan çarpan.</summary>
    /// <remarks>
    /// Yakalamanın kendi cevabı budur — yoksa jitte her eşleşmede doğru seçim olurdu.
    /// Nodachi'nin kaldıracı tek elle tutulan bir çengeli söker; ağır silah seçen
    /// savaşçı bunun karşılığını burada alır.
    /// </remarks>
    public double CatchTwoHandedFactor { get; init; } = 0.75;

    /// <summary>İsabet 100 iken yakalama şansına eklenen oran.</summary>
    /// <remarks>
    /// Yakalama bir zamanlama işidir, bir refleks işi değil: kaçınma Kaçınma'ya bağlıyken
    /// yakalama <b>İsabet</b>'e bağlanır. İki savunma ekseni aynı stattan beslenseydi
    /// yakalama aleti yalnızca kaçınması yüksek savaşçının işine yarar, ekipman kararı
    /// stat kararının kopyası olurdu.
    /// </remarks>
    public double CatchAccuracyBonusAtMax { get; init; } = 0.5;

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

    /// <summary>Ağırlık <b>bacak başına</b>dır; iki bacak birlikte 25 eder.</summary>
    /// <inheritdoc cref="TorsoHitWeight"/>
    public double LegHitWeight { get; init; } = 12.5;

    /// <summary>Ağırlık <b>kol başına</b>dır; iki kol birlikte 20 eder.</summary>
    /// <inheritdoc cref="TorsoHitWeight"/>
    public double ArmHitWeight { get; init; } = 10;

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
