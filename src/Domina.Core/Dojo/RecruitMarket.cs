using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Dojo;

/// <summary>Pazardaki bir aday — statlarıyla ve fiyatıyla.</summary>
/// <remarks>
/// <para>
/// Savaşçı almak bir <b>seçim</b> olmalı, bir düğme değil: adaylar farklı statlarla gelir,
/// statlar alım öncesi <b>görünür</b> ve fiyat statın kendisinden çıkar. Sabit statlı sabit
/// fiyatlı savaşçıda "kimi alayım" diye bir soru yoktur; para varsa alınır, yoksa alınmaz.
/// </para>
/// <para>
/// Aday satın alınana kadar kadroya girmez: <see cref="Warrior"/> nesnesi ancak alım anında
/// üretilir. Pazarda gezinen altı adayın kalıcı kimlik taşıması, ölen savaşçılarla aynı
/// kimlik uzayını kirletirdi.
/// </para>
/// </remarks>
/// <param name="Name">Adayın adı.</param>
/// <param name="Stats">Görünen statlar — pazarlıkta gizli bir şey yok.</param>
/// <param name="Talent">
/// Antrenmandan ne kadar hızlı faydalanacağı (1.0 = ortalama) — bir antrenman gününün
/// kazancını doğrudan çarpar (<see cref="TrainingGround"/>).
/// </param>
/// <param name="Price">İstenen altın.</param>
public sealed record RecruitOffer(string Name, WarriorStats Stats, double Talent, int Price);

/// <summary>Köle pazarının ayarlanabilir sayıları.</summary>
/// <remarks>
/// Sayılar <b>kilitli değil</b>. Ölçümün sorusu belli: ucuz ham adayı alıp eğitmek ile
/// pahalı hazır adayı almak <b>rakip</b> olmalı — biri her zaman doğruysa pazar yine bir
/// düğmedir.
/// </remarks>
public sealed record MarketTuning
{
    /// <summary>Aynı anda pazarda duran aday sayısı.</summary>
    public int Candidates { get; init; } = 3;

    /// <summary>Pazarın kaç günde bir yenilendiği.</summary>
    /// <remarks>
    /// Her gün yenilenseydi beğenilmeyen kadro bir gün beklenerek düzeltilirdi ve seçim
    /// kararı "yarın daha iyisi gelir" diye ertelenirdi. Birkaç günlük durgunluk, eldeki
    /// adayı gerçek bir seçenek yapar.
    /// </remarks>
    public int RefreshDays { get; init; } = 2;

    /// <summary>Adayın statlarının taban etrafındaki oynama payı.</summary>
    public double Spread { get; init; } = 0.35;

    /// <summary>
    /// Pazarın kadronun seviyesini ne kadar takip ettiği (0 = hiç, 1 = tamamen).
    /// </summary>
    /// <remarks>
    /// Erken oyunda pazarda usta savaşçı bulunmaz; kadro geliştikçe pazar da gelişir.
    /// Takip olmasaydı ya baştan her şey satın alınabilir olurdu (antrenmanın anlamı
    /// kalmaz), ya da geç oyunda pazar tamamen anlamsızlaşırdı.
    /// </remarks>
    public double RosterFollow { get; init; } = 0.7;

    /// <summary>
    /// Pazardaki en iyi adayın, dojonun <b>en iyi savaşçısına</b> göre üst sınırı.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ortalama takibi (<see cref="RosterFollow"/>) pazarın nereye <i>oturduğunu</i>
    /// söyler ama nereye kadar <i>çıkabileceğini</i> söylemez: <see cref="Spread"/>
    /// üstten vurduğunda tek bir aday kadronun en iyisine yaklaşabilir. Bu tavan onu
    /// keser — satın alınan savaşçı elindeki en iyinin bu oranını asla geçemez.
    /// </para>
    /// <para>
    /// Referans <b>ortalama değil en iyi savaşçıdır</b>: ortalamaya bağlansaydı iki ucuz
    /// acemi alıp ortalamayı düşürerek pazar sömürülebilirdi. En iyi savaşçı
    /// düşürülemez, yalnızca ölerek kaybedilir — ölünce tavanın da düşmesi doğrudur.
    /// </para>
    /// <para>
    /// Gerekçe: yetiştirilen savaşçı oyuncunun <b>eseri</b> olmalı; pazar onu
    /// kopyalayabiliyorsa antrenmanın anlamı kalmaz. Pazar <b>yerine koyma</b> aracıdır,
    /// <b>ilerleme</b> aracı değil. Stat tavanı sertken pazarın tam güçle satabildiği tek
    /// şey <see cref="RecruitOffer.Talent"/> olarak kalır — ilerleme yolu ham adayı alıp
    /// eğitmekten geçer.
    /// </para>
    /// </remarks>
    public double BestFollowCeiling { get; init; } = 0.75;

    /// <summary>Yeteneğin alt ve üst sınırı.</summary>
    public double MinTalent { get; init; } = 0.6;

    /// <summary>Yeteneğin üst sınırı.</summary>
    public double MaxTalent { get; init; } = 1.4;

    /// <summary>Yeteneğin fiyata etkisi — 1.0 yetenek fiyatı değiştirmez.</summary>
    public double TalentPriceWeight { get; init; } = 0.5;

    /// <summary>Pazarın kullandığı isim havuzu.</summary>
    /// <remarks>
    /// Geçici: GDD §8'e göre isimler yayın açıkken chat'ten gelecek (Faz 5). Havuz o zaman
    /// buradan değil, izleyici listesinden okunacak; pazarın kendisi değişmeyecek.
    /// </remarks>
    public IReadOnlyList<string> Names { get; init; } =
    [
        "Kenji", "Hana", "Takeshi", "Ayame", "Ren", "Kaede", "Jiro", "Sora",
        "Michi", "Haruki", "Yuki", "Daichi", "Nozomi", "Kaito", "Rin", "Sato",
    ];
}

/// <summary>Pazarın etrafında üretileceği taban ve aşamayacağı tavan.</summary>
/// <remarks>
/// İkisi ayrı sorulara cevap verir: <paramref name="Stats"/> adayların <b>nereye
/// oturduğunu</b>, <paramref name="CeilingScore"/> ise <b>nereye kadar çıkabildiğini</b>
/// söyler. Taban kadronun ortalamasını, tavan kadronun en iyisini izler.
/// </remarks>
/// <param name="Stats">Adayların etrafında oynatılacağı taban statlar.</param>
/// <param name="CeilingScore">
/// Bir adayın toplam stat skorunun üst sınırı; sınırsız için sonsuz.
/// </param>
public sealed record MarketAnchor(WarriorStats Stats, double CeilingScore)
{
    /// <summary>Tavansız taban — ölçüm ve test için.</summary>
    public static MarketAnchor Uncapped(WarriorStats stats) =>
        new(stats, double.PositiveInfinity);
}

/// <summary>Günün köle pazarını üretir.</summary>
/// <remarks>
/// Teklif ve olay gibi <b>saf</b>: aynı tohum, aynı dönem ve aynı kadro seviyesi daima aynı
/// adayları verir. Pazar <see cref="MarketTuning.RefreshDays"/> günde bir yenilenir, yani
/// gün numarası değil <b>dönem</b> numarası karıştırılır.
/// </remarks>
public sealed class RecruitMarket(MarketTuning? tuning = null)
{
    public MarketTuning Tuning { get; } = tuning ?? new MarketTuning();

    /// <summary>Verilen gündeki adaylar.</summary>
    /// <param name="anchor">
    /// Pazarın etrafında üretileceği taban ve aşamayacağı tavan — bkz.
    /// <see cref="AnchorFor(Roster)"/>.
    /// </param>
    public IReadOnlyList<RecruitOffer> Stock(int day, ulong seed, MarketAnchor anchor, int basePrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);

        int period = (day - 1) / Math.Max(1, Tuning.RefreshDays);
        return Stock(new SeededRandom(Mix(seed, period)), anchor, basePrice);
    }

    /// <summary>Akışı dışarıdan verilen pazar — ölçüm ve test için.</summary>
    public IReadOnlyList<RecruitOffer> Stock(IRandomSource random, MarketAnchor anchor, int basePrice)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(anchor);

        List<RecruitOffer> stock = [];
        for (int i = 0; i < Tuning.Candidates; i++)
        {
            stock.Add(Draw(random, anchor, basePrice));
        }

        return stock;
    }

    /// <summary>Tavansız pazar — ölçüm ve test için.</summary>
    public IReadOnlyList<RecruitOffer> Stock(IRandomSource random, WarriorStats anchor, int basePrice) =>
        Stock(random, MarketAnchor.Uncapped(anchor), basePrice);

    /// <summary>Kadroya bakarak pazarın tabanını ve tavanını çıkarır.</summary>
    /// <remarks>
    /// <para>
    /// <b>Taban</b> kadronun ortalaması ile acemi seviyesi arasındadır
    /// (<see cref="MarketTuning.RosterFollow"/>). Kadro boşken (herkes öldüyse) taban
    /// acemi statlarıdır — yoksa dojo çöktükten sonra pazar da çöker ve toparlanmanın
    /// yolu kalmazdı.
    /// </para>
    /// <para>
    /// <b>Tavan</b> ise ortalamayı değil kadronun <b>en iyi savaşçısını</b> izler
    /// (<see cref="MarketTuning.BestFollowCeiling"/>): satın alınan hiçbir savaşçı elde
    /// yetiştirilmiş en iyiyi geçemesin. Boş kadroda tavan acemi skorudur, yani ilk
    /// alımlar da acemi bandında kalır.
    /// </para>
    /// </remarks>
    public MarketAnchor AnchorFor(Roster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        List<Warrior> living = [.. roster.Living.Select(e => e.Warrior)];
        WarriorStats recruit = WarriorStats.Recruit();
        if (living.Count == 0)
        {
            return new MarketAnchor(recruit, Score(recruit));
        }

        WarriorStats average = new(
            living.Average(w => w.BaseStats.MaxHealth),
            living.Average(w => w.BaseStats.Aggression),
            living.Average(w => w.BaseStats.Defense),
            living.Average(w => w.BaseStats.Evasion),
            living.Average(w => w.BaseStats.Strength),
            living.Average(w => w.BaseStats.Accuracy),
            living.Average(w => w.BaseStats.MaxStamina),
            living.Average(w => w.BaseStats.Speed));

        double follow = Math.Clamp(Tuning.RosterFollow, 0, 1);
        double best = living.Max(w => Score(w.BaseStats));

        // Tavan acemi seviyesinin altına hiçbir zaman inmez: aksi halde tavan daha ilk
        // günden ısırır ve pazar acemi kadroya acemiden zayıf adam satar — yerine koyma
        // yolu kapanır, dojo toparlanamaz (ölçüldü: 400 dojonun tamamı kasayı sıfırladı).
        // Tavan ancak en iyi savaşçı acemiyi belirgin şekilde geçtiğinde devreye girer.
        double ceiling = Math.Max(
            Score(recruit),
            best * Math.Max(0, Tuning.BestFollowCeiling));

        return new MarketAnchor(Blend(recruit, average, follow), ceiling);
    }

    private RecruitOffer Draw(IRandomSource random, MarketAnchor anchor, int basePrice)
    {
        double talent = Tuning.MinTalent
            + (random.NextDouble() * Math.Max(0, Tuning.MaxTalent - Tuning.MinTalent));

        WarriorStats around = anchor.Stats;
        WarriorStats stats = Capped(
            new WarriorStats(
                Roll(random, around.MaxHealth, cap: false),
                Roll(random, around.Aggression),
                Roll(random, around.Defense),
                Roll(random, around.Evasion),
                Roll(random, around.Strength),
                Roll(random, around.Accuracy),
                Roll(random, around.MaxStamina, cap: false),
                Roll(random, around.Speed)),
            anchor.CeilingScore);

        string name = Tuning.Names.Count == 0
            ? "Adsız"
            : Tuning.Names[random.NextInt(Tuning.Names.Count)];

        return new RecruitOffer(name, stats, talent, Price(stats, talent, around, basePrice));
    }

    /// <summary>Tavanı aşan adayı statlarını oranlayarak aşağı çeker.</summary>
    /// <remarks>
    /// Tek tek kırpmak yerine <b>hepsi aynı oranla</b> ölçeklenir: kırpma, tavana dayanan
    /// her adayı aynı düz profile çevirirdi ve "kimi alayım" sorusu geri kaybolurdu.
    /// Ölçekleme adayın şeklini korur, yalnızca ağırlığını düşürür.
    /// </remarks>
    private static WarriorStats Capped(WarriorStats stats, double ceilingScore)
    {
        double score = Score(stats);
        if (double.IsInfinity(ceilingScore) || ceilingScore <= 0 || score <= ceilingScore)
        {
            return stats;
        }

        double scale = ceilingScore / score;
        return new WarriorStats(
            Math.Max(1, stats.MaxHealth * scale),
            Math.Max(1, stats.Aggression * scale),
            Math.Max(1, stats.Defense * scale),
            Math.Max(1, stats.Evasion * scale),
            Math.Max(1, stats.Strength * scale),
            Math.Max(1, stats.Accuracy * scale),
            Math.Max(1, stats.MaxStamina * scale),
            Math.Max(1, stats.Speed * scale));
    }

    /// <summary>Tek bir statı taban etrafında oynatır.</summary>
    private double Roll(IRandomSource random, double around, bool cap = true)
    {
        double swing = ((random.NextDouble() * 2) - 1) * Tuning.Spread;
        double value = around * (1 + swing);

        return cap ? Math.Clamp(value, 1, 95) : Math.Max(1, value);
    }

    /// <summary>
    /// Fiyat adayın <b>tabana göre</b> ne kadar iyi olduğundan çıkar.
    /// </summary>
    /// <remarks>
    /// Sabit fiyat, iyi adayı bedava; kötü adayı ise soygun yapardı. Yetenek de fiyata
    /// girer ama statlardan daha az ağırlıkla: yetenek bir <b>vaat</b>, stat ise elde
    /// olan.
    /// </remarks>
    private int Price(WarriorStats stats, double talent, WarriorStats anchor, int basePrice)
    {
        double ratio = Score(anchor) <= 0 ? 1 : Score(stats) / Score(anchor);
        double talentRatio = 1 + ((talent - 1) * Tuning.TalentPriceWeight);

        return Math.Max(1, (int)Math.Round(basePrice * ratio * talentRatio));
    }

    private static double Score(WarriorStats stats) =>
        stats.MaxHealth
        + stats.Strength
        + stats.Accuracy
        + stats.Defense
        + stats.Evasion
        + stats.Speed
        + stats.Aggression;

    private static WarriorStats Blend(WarriorStats a, WarriorStats b, double towardsB) => new(
        Lerp(a.MaxHealth, b.MaxHealth, towardsB),
        Lerp(a.Aggression, b.Aggression, towardsB),
        Lerp(a.Defense, b.Defense, towardsB),
        Lerp(a.Evasion, b.Evasion, towardsB),
        Lerp(a.Strength, b.Strength, towardsB),
        Lerp(a.Accuracy, b.Accuracy, towardsB),
        Lerp(a.MaxStamina, b.MaxStamina, towardsB),
        Lerp(a.Speed, b.Speed, towardsB));

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    /// <summary>Tohumu <b>dönemle</b> karıştırır — teklif ve olay akışlarından ayrı tuzla.</summary>
    private static ulong Mix(ulong seed, int period)
    {
        ulong x = seed ^ ((ulong)period * 0xC2B2AE3D27D4EB4F) ^ 0x5DEECE66D;
        x ^= x >> 30;
        x *= 0xBF58476D1CE4E5B9;
        x ^= x >> 27;
        return x;
    }
}
