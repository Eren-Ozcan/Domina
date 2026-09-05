using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Dojo;

/// <summary>Günün başına gelebilecek aksilikler.</summary>
/// <remarks>
/// <para>
/// GDD §11'in son kalemi: rastgele olaylar kaynak eksiltebilir, bu da <b>tampon tutma</b>
/// baskısı yaratır. Baskının işe yaraması için olayların ambara değil <b>kasaya</b> ve
/// <b>takvime</b> vurması gerekiyor: günlük alışveriş ambarı tam ihtiyaç kadar doldurduğu
/// için (bkz. <see cref="Quartermaster.Restock"/>) çalınan üç ölçek pirincin karşılığı
/// zaten sıfırdır. Bu yüzden olaylar ya altını götürür, ya o günün ihtiyacını büyütür,
/// ya da bir savaşçının gününü alır.
/// </para>
/// <para>
/// Hepsi <b>eksiltir</b>. Bağış, hazine, iyi haber yok — GDD §11 olayları tampon baskısı
/// olarak tarif ediyor; çift yönlü bir olay tablosu baskıyı ortadan kaldırırdı.
/// </para>
/// </remarks>
public enum DayEventKind
{
    /// <summary>Kasadan para gitti.</summary>
    Theft,

    /// <summary>Erzak bozuldu: o günün yiyeceği pahalandı.</summary>
    Spoilage,

    /// <summary>Kuyu bulandı: o günün suyu pahalandı.</summary>
    FoulWell,

    /// <summary>İlaç işe yaramadı: o gün revirdekiler ilaçsız kaldı.</summary>
    SpoiledMedicine,

    /// <summary>Bir savaşçı hastalandı: dövüşmeden revire düştü.</summary>
    Illness,
}

/// <summary>O gün ne olduğu.</summary>
/// <param name="Kind">Olayın türü.</param>
/// <param name="Description">Günlükte görünen cümle.</param>
/// <param name="Gold">Kasadan giden altın.</param>
/// <param name="Target">Olay bir savaşçıya değdiyse o savaşçı.</param>
/// <param name="RecoveryDays">Hastalığın yatırdığı gün.</param>
/// <param name="FoodFactor">O günün yiyecek ihtiyacının çarpanı (1 = normal).</param>
/// <param name="WaterFactor">O günün su ihtiyacının çarpanı (1 = normal).</param>
public sealed record DayEvent(
    DayEventKind Kind,
    string Description,
    int Gold = 0,
    WarriorId? Target = null,
    int RecoveryDays = 0,
    double FoodFactor = 1,
    double WaterFactor = 1)
{
    /// <summary>O gün ilaç kullanılabiliyor mu?</summary>
    public bool MedicineWorks => Kind != DayEventKind.SpoiledMedicine;
}

/// <summary>Rastgele olayların ayarlanabilir sayıları.</summary>
/// <remarks>
/// Sayılar <b>kilitli değil</b>: GDD §11 yalnızca "rastgele olaylar kaynak eksiltebilir"
/// diyor, sıklık ve şiddet ölçümle kapanacak. Ölçümün sorusu belli — olaylar tamponu
/// zorlamalı ama tek başına dojo kapatmamalı.
/// </remarks>
public sealed record EventTuning
{
    /// <summary>Bir günde olay çıkma olasılığı.</summary>
    public double ChancePerDay { get; init; } = 0.15;

    /// <summary>Hırsızlığın kasadan alabileceği <b>en büyük</b> pay.</summary>
    /// <remarks>
    /// Gerçek pay her seferinde sıfır ile bu sayı arasında çekilir. Sabit oran, aksiliği
    /// hesaplanabilir bir vergiye çevirirdi: oyuncu kayıp miktarını baştan bilirse tampon
    /// tutmak bir karar değil, bir aritmetik olurdu.
    /// </remarks>
    public double MaxTheftShare { get; init; } = 0.12;

    /// <summary>Bozulan erzağın o günün faturasına ekleyebileceği <b>en büyük</b> pay.</summary>
    /// <remarks>1.0 = fatura en kötü ihtimalle iki katına çıkar.</remarks>
    public double MaxSpoilageShare { get; init; } = 1.0;

    /// <summary>Bulanan kuyunun o günün su faturasına ekleyebileceği <b>en büyük</b> pay.</summary>
    public double MaxFoulWellShare { get; init; } = 1.0;

    /// <summary>Hastalığın yatırdığı <b>en çok</b> gün; gerçek süre 1 ile bu sayı arasında.</summary>
    public int MaxIllnessDays { get; init; } = 3;

    /// <summary>Olay türlerinin çekiliş ağırlıkları.</summary>
    /// <remarks>
    /// Hırsızlık en ağır kalem: doğrudan tampona vuran tek olay o. Diğerleri günü ya da
    /// bir savaşçıyı pahalılaştırır — tamponu değil, planı bozar.
    /// </remarks>
    public IReadOnlyList<(DayEventKind Kind, double Weight)> Weights { get; init; } =
    [
        (DayEventKind.Theft, 3),
        (DayEventKind.Spoilage, 2),
        (DayEventKind.FoulWell, 2),
        (DayEventKind.SpoiledMedicine, 1.5),
        (DayEventKind.Illness, 2),
    ];
}

/// <summary>Günün olayını çeker.</summary>
/// <remarks>
/// Karşılaşma teklifi gibi <b>saf</b>: aynı gün ve aynı tohum daima aynı olayı verir.
/// Böylece olay da kayda yazılmaz ve kaydı yeniden yükleyerek başa gelen aksilik
/// değiştirilemez.
/// </remarks>
public sealed class DayEventTable(EventTuning? tuning = null)
{
    public EventTuning Tuning { get; } = tuning ?? new EventTuning();

    /// <summary>O günün olayı; olay yoksa <c>null</c>.</summary>
    public DayEvent? Roll(DojoState state, int day)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Roll(state, new SeededRandom(Mix(state.Seed, day)));
    }

    /// <summary>Akışı dışarıdan verilen çekiliş — ölçüm ve test için.</summary>
    public DayEvent? Roll(DojoState state, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);

        if (!random.Chance(Tuning.ChancePerDay))
        {
            return null;
        }

        return Build(state, Pick(random), random);
    }

    private DayEvent Build(DojoState state, DayEventKind kind, IRandomSource random) => kind switch
    {
        DayEventKind.Theft => Theft(state, random),
        DayEventKind.Spoilage => Spoilage(random),
        DayEventKind.FoulWell => FoulWell(random),
        DayEventKind.SpoiledMedicine => new DayEvent(kind, "İlaç küflenmiş; revir bugün boş elle çalıştı."),
        DayEventKind.Illness => Illness(state, random),
        _ => new DayEvent(kind, "Sıradan bir aksilik."),
    };

    private DayEvent Theft(DojoState state, IRandomSource random)
    {
        int purse = Math.Max(0, state.Resources.Gold);
        int taken = Math.Min(purse, (int)Math.Round(purse * random.NextDouble() * Tuning.MaxTheftShare));

        // Kasada para varken hırsız eli boş dönmez: yuvarlama sıfıra düşse de bir altın gider.
        if (taken == 0 && purse > 0)
        {
            taken = 1;
        }

        return new DayEvent(
            DayEventKind.Theft,
            taken > 0 ? $"Kasadan {taken} altın çalındı." : "Hırsız girdi ama kasada bir şey yoktu.",
            Gold: taken);
    }

    private DayEvent Spoilage(IRandomSource random)
    {
        double factor = 1 + (random.NextDouble() * Tuning.MaxSpoilageShare);
        return new DayEvent(
            DayEventKind.Spoilage,
            "Ambardaki erzağın bir kısmı bozuldu.",
            FoodFactor: factor);
    }

    private DayEvent FoulWell(IRandomSource random)
    {
        double factor = 1 + (random.NextDouble() * Tuning.MaxFoulWellShare);
        return new DayEvent(
            DayEventKind.FoulWell,
            "Kuyu bulandı, su uzaktan taşındı.",
            WaterFactor: factor);
    }

    /// <summary>
    /// Hastalık <b>sağlam</b> savaşçıya değer.
    /// </summary>
    /// <remarks>
    /// Zaten revirde yatan birini hastalandırmak görünmez bir olay olurdu: revir günü
    /// sayacı uzun olanı ezmiyor (bkz. <see cref="RosterEntry.Injure"/>), yani olay çoğu
    /// zaman hiçbir şey yapmazdı.
    /// </remarks>
    private DayEvent Illness(DojoState state, IRandomSource random)
    {
        RosterEntry? victim = PickWarrior(state, random, e => e.IsFitForCampaign);
        if (victim is null)
        {
            return new DayEvent(DayEventKind.Illness, "Dojo'da hastalık dolaştı ama kimseyi yatıramadı.");
        }

        int days = 1 + random.NextInt(Math.Max(1, Tuning.MaxIllnessDays));
        return new DayEvent(
            DayEventKind.Illness,
            $"{victim.Name} hastalandı; {days} gün revirde.",
            Target: victim.Id,
            RecoveryDays: days);
    }

    private static RosterEntry? PickWarrior(
        DojoState state,
        IRandomSource random,
        Func<RosterEntry, bool> fits)
    {
        List<RosterEntry> pool = [.. state.Roster.Living.Where(fits)];
        return pool.Count == 0 ? null : pool[random.NextInt(pool.Count)];
    }

    private DayEventKind Pick(IRandomSource random)
    {
        double total = Tuning.Weights.Sum(w => w.Weight);
        if (total <= 0)
        {
            return DayEventKind.Theft;
        }

        double roll = random.NextDouble() * total;
        foreach ((DayEventKind kind, double weight) in Tuning.Weights)
        {
            roll -= weight;
            if (roll <= 0)
            {
                return kind;
            }
        }

        return Tuning.Weights[^1].Kind;
    }

    /// <summary>
    /// Tohumu günle karıştırır — teklif akışından <b>ayrı</b> bir tuzla.
    /// </summary>
    /// <remarks>
    /// Aynı tuz kullanılsaydı olay ile teklif birbirine kilitlenirdi: ağır teklifin geldiği
    /// gün daima hırsızlık da olurdu ve iki sistem tek bir sisteme dönüşürdü.
    /// </remarks>
    private static ulong Mix(ulong seed, int day)
    {
        ulong x = seed ^ ((ulong)day * 0xD1B54A32D192ED03) ^ 0xA5A5A5A5A5A5A5A5;
        x ^= x >> 31;
        x *= 0x9FB21C651E98DF25;
        x ^= x >> 29;
        return x;
    }
}
