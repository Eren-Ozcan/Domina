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
/// Antrenmandan ne kadar hızlı faydalanacağı (1.0 = ortalama).
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
    /// Pazarın etrafında üretileceği seviye — kadronun ortalaması.
    /// </param>
    public IReadOnlyList<RecruitOffer> Stock(int day, ulong seed, WarriorStats anchor, int basePrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);

        int period = (day - 1) / Math.Max(1, Tuning.RefreshDays);
        return Stock(new SeededRandom(Mix(seed, period)), anchor, basePrice);
    }

    /// <summary>Akışı dışarıdan verilen pazar — ölçüm ve test için.</summary>
    public IReadOnlyList<RecruitOffer> Stock(IRandomSource random, WarriorStats anchor, int basePrice)
    {
        ArgumentNullException.ThrowIfNull(random);

        List<RecruitOffer> stock = [];
        for (int i = 0; i < Tuning.Candidates; i++)
        {
            stock.Add(Draw(random, anchor, basePrice));
        }

        return stock;
    }

    /// <summary>
    /// Pazarın etrafında üretileceği seviye: kadronun ortalaması ile acemi tabanı arasında.
    /// </summary>
    /// <remarks>
    /// Kadro boşken (herkes öldüyse) taban acemi statlarıdır — yoksa dojo çöktükten sonra
    /// pazar da çöker ve toparlanmanın yolu kalmazdı.
    /// </remarks>
    public WarriorStats AnchorFor(Roster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        List<Warrior> living = [.. roster.Living.Select(e => e.Warrior)];
        WarriorStats recruit = WarriorStats.Recruit();
        if (living.Count == 0)
        {
            return recruit;
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
        return Blend(recruit, average, follow);
    }

    private RecruitOffer Draw(IRandomSource random, WarriorStats anchor, int basePrice)
    {
        double talent = Tuning.MinTalent
            + (random.NextDouble() * Math.Max(0, Tuning.MaxTalent - Tuning.MinTalent));

        WarriorStats stats = new(
            Roll(random, anchor.MaxHealth, cap: false),
            Roll(random, anchor.Aggression),
            Roll(random, anchor.Defense),
            Roll(random, anchor.Evasion),
            Roll(random, anchor.Strength),
            Roll(random, anchor.Accuracy),
            Roll(random, anchor.MaxStamina, cap: false),
            Roll(random, anchor.Speed));

        string name = Tuning.Names.Count == 0
            ? "Adsız"
            : Tuning.Names[random.NextInt(Tuning.Names.Count)];

        return new RecruitOffer(name, stats, talent, Price(stats, talent, anchor, basePrice));
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
