using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>Yeteneğin okunabilir bandı.</summary>
/// <remarks>
/// Yetenek ekranda <b>sayı olarak</b> gösterilmez. Stat elde olandır ve tam sayısıyla
/// yazılır; yetenek ise bir <b>vaat</b> — "1.23" yazmak onu ölçülmüş bir stat gibi
/// gösterir ve pazarı hesap tablosuna çevirir. Bant, kararı verecek kadar bilgi verir.
/// </remarks>
public enum TalentBand
{
    /// <summary>Ortalamanın belirgin altında.</summary>
    Dull,

    /// <summary>Ortalama civarı.</summary>
    Fair,

    /// <summary>Ortalamanın üstünde.</summary>
    Promising,

    /// <summary>Pazarın üst ucu.</summary>
    Rare,
}

/// <summary>Pazardaki tek satır — bir aday ve bugünkü hükmü.</summary>
/// <param name="Index">
/// Adayın <see cref="DojoState.Recruits"/> içindeki yeri; ekran satın alırken bunu geri
/// verir. İsim eşsiz değil (aynı isim iki adayda çıkabilir), o yüzden kimlik sıradır.
/// </param>
/// <param name="Name">Adayın adı.</param>
/// <param name="Stats">Görünen statlar — pazarlıkta gizli bir şey yok.</param>
/// <param name="Band">Yeteneğin bandı.</param>
/// <param name="Price">İstenen altın.</param>
/// <param name="Affordable">Kasadaki altın yetiyor mu?</param>
/// <param name="Bought">Bugün alındı mı? Alınan aday tezgâhta durur ama satılmaz.</param>
/// <param name="Score">Toplam stat skoru — satırların kıyaslandığı tek sayı.</param>
/// <param name="BetterInRoster">
/// Kadroda bu adaydan iyi <b>kaç canlı</b> savaşçı var. Sıfırsa aday bugün elindeki
/// herkesten iyi.
/// </param>
public readonly record struct MarketRow(
    int Index,
    string Name,
    WarriorStats Stats,
    TalentBand Band,
    int Price,
    bool Affordable,
    bool Bought,
    double Score,
    int BetterInRoster);

/// <summary>Pazarın tepesinde duran sayılar.</summary>
/// <param name="Gold">Kasadaki altın.</param>
/// <param name="Candidates">Bugün pazarda duran aday sayısı.</param>
/// <param name="Affordable">Bugün alınabilecek aday sayısı — alınmış olanlar sayılmaz.</param>
/// <param name="Bought">Bugün alınmış aday sayısı.</param>
/// <param name="DaysToRefresh">
/// Kaç gün sonra pazar yenilenir; bugün yenilendiyse tam bir dönem.
/// </param>
/// <param name="BestLivingScore">Kadrodaki en iyi canlının skoru; kadro boşsa 0.</param>
public readonly record struct MarketSummary(
    int Gold,
    int Candidates,
    int Affordable,
    int Bought,
    int DaysToRefresh,
    double BestLivingScore);

/// <summary>
/// Pazar ekranının okuduğu model. Kıyaslamayı ve hükmü hesaplar, çizim yapmaz.
/// </summary>
/// <remarks>
/// Ekran <see cref="DojoState.Recruits"/>'i doğrudan okusaydı iki iş sızardı: adayın
/// <b>kadroya göre</b> nerede durduğu ve yeteneğin nasıl okunacağı. Birincisi pazarın
/// asıl sorusu ("bu adam elimdekinden iyi mi"), ikincisi ise bilerek bulanık tutulan
/// tek kalem.
/// </remarks>
public static class MarketModel
{
    /// <summary>Bugünkü pazar, ucuzdan pahalıya.</summary>
    /// <remarks>
    /// Sıra <b>fiyata</b> göredir, kasaya göre değil: alınabilirlik altın harcandıkça
    /// değişir, sıra da her alımda kayardı. Pazar tezgâhı oyuncunun cebine göre yeniden
    /// dizilmemeli — hangi adayın nerede durduğu gün boyu sabit kalsın.
    /// </remarks>
    public static IReadOnlyList<MarketRow> Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        IReadOnlyList<RecruitOffer> stock = dojo.Recruits;
        List<double> living = [.. dojo.Roster.Living.Select(e => Score(e.Warrior.BaseStats))];

        return stock
            .Select((offer, index) => Describe(
                offer,
                index,
                dojo.Resources.Gold,
                living,
                dojo.HiredToday.Contains(index)))
            .OrderBy(row => row.Price)
            .ThenBy(row => row.Index)
            .ToList();
    }

    /// <summary>Tek adayın satırı.</summary>
    /// <param name="offer">Pazardaki aday.</param>
    /// <param name="index">Adayın stok içindeki yeri.</param>
    /// <param name="gold">Kasadaki altın.</param>
    /// <param name="livingScores">Kadrodaki canlıların skorları.</param>
    /// <param name="bought">Aday bugün alındı mı?</param>
    public static MarketRow Describe(
        RecruitOffer offer,
        int index,
        int gold,
        IReadOnlyCollection<double> livingScores,
        bool bought = false)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(livingScores);

        double score = Score(offer.Stats);

        return new MarketRow(
            Index: index,
            Name: offer.Name,
            Stats: offer.Stats,
            Band: BandOf(offer.Talent),
            Price: offer.Price,
            Affordable: offer.Price <= gold,
            Bought: bought,
            Score: score,
            BetterInRoster: livingScores.Count(s => s > score));
    }

    /// <summary>Pazarın tepesindeki sayılar.</summary>
    public static MarketSummary Summarize(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        IReadOnlyList<RecruitOffer> stock = dojo.Recruits;
        List<double> living = [.. dojo.Roster.Living.Select(e => Score(e.Warrior.BaseStats))];

        return new MarketSummary(
            Gold: dojo.Resources.Gold,
            Candidates: stock.Count,
            Affordable: stock
                .Where((o, i) => o.Price <= dojo.Resources.Gold && !dojo.HiredToday.Contains(i))
                .Count(),
            Bought: dojo.HiredToday.Count,
            DaysToRefresh: DaysToRefresh(dojo),
            BestLivingScore: living.Count == 0 ? 0 : living.Max());
    }

    /// <summary>Pazarın yenilenmesine kalan gün — bugün dahil değil.</summary>
    /// <remarks>
    /// Varsayılan ayarda tezgâh her gün yenilenir, yani bu sayı 1'dir; ölçüm ayarında
    /// (<see cref="MarketTuning.RefreshDays"/>) büyüyebilir. Ekranda yazması gerekir:
    /// tezgâhın kaç gün duracağını bilmeyen oyuncu, beğenmediği listeyi "yarın değişir"
    /// diye geçer ve durgun bir tezgâhta kararı boşuna erteler.
    /// </remarks>
    public static int DaysToRefresh(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        int period = Math.Max(1, dojo.Market.Tuning.RefreshDays);
        return period - ((dojo.Day - 1) % period);
    }

    /// <summary>Adayların kıyaslandığı toplam stat skoru.</summary>
    /// <remarks>
    /// Formül <see cref="RecruitMarket"/>'in tavan hesabıyla <b>aynı</b> olmalı: pazar
    /// bir adayı tavana takıldığı için kırpıyorsa, ekranda o adayın neden kadronun
    /// en iyisini geçemediği aynı sayıdan okunabilsin. Stamina dışarıda — tavan da onu
    /// saymıyor.
    /// </remarks>
    public static double Score(WarriorStats stats) =>
        stats.MaxHealth
        + stats.Strength
        + stats.Accuracy
        + stats.Defense
        + stats.Evasion
        + stats.Speed
        + stats.Aggression;

    private static TalentBand BandOf(double talent) => talent switch
    {
        < 0.85 => TalentBand.Dull,
        < 1.05 => TalentBand.Fair,
        < 1.25 => TalentBand.Promising,
        _ => TalentBand.Rare,
    };
}
