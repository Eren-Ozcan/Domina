using Domina.Core.Campaign;
using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>Dojo'nun bütün kalıcı hâli — kayıt dosyasının konusu budur.</summary>
/// <remarks>
/// <para>
/// Motora bağımlı hiçbir şey içermez ve <b>deterministik</b>tir: aynı tohum ve aynı gün
/// aynı sonucu verir. Rastgelelik gerektiren iki kalem (karşılaşma teklifi ve günün olayı)
/// gün ile <see cref="Seed"/>'in saf birer fonksiyonudur — akış durumu taşınmaz, ikisi de
/// kayda yazılmaz, ikisi de kaydı yeniden yükleyerek değiştirilemez.
/// </para>
/// </remarks>
public sealed class DojoState
{
    private EncounterOffer? _offer;
    private IReadOnlyList<RecruitOffer>? _recruits;

    public DojoState(
        DojoTuning? tuning = null,
        EconomyTuning? economy = null,
        ulong seed = 1,
        EncounterTuning? encounters = null,
        EventTuning? events = null,
        MarketTuning? market = null)
    {
        Tuning = tuning ?? new DojoTuning();
        Quartermaster = new Quartermaster(economy);
        Encounters = new EncounterGenerator(encounters);
        Events = new DayEventTable(events);
        Market = new RecruitMarket(market);
        Seed = seed;
    }

    public DojoTuning Tuning { get; }

    /// <summary>Fiyatlar ve alışveriş. Ekonomi sayıları buradan okunur.</summary>
    public Quartermaster Quartermaster { get; }

    public EconomyTuning Economy => Quartermaster.Economy;

    /// <summary>Günün teklifini üreten çark.</summary>
    public EncounterGenerator Encounters { get; }

    /// <summary>Günün aksiliğini çeken tablo (GDD §11: rastgele olaylar).</summary>
    public DayEventTable Events { get; }

    /// <summary>Savaşçı pazarı.</summary>
    public RecruitMarket Market { get; }

    /// <summary>
    /// Seferin tohumu. Kayıtta durur; teklifler bundan ve günden yeniden hesaplanır.
    /// </summary>
    public ulong Seed { get; private set; }

    public Roster Roster { get; } = new();

    public Resources Resources { get; set; }

    /// <summary>Kaçıncı gün. Oyun 1. günde başlar.</summary>
    public int Day { get; private set; } = 1;

    /// <summary>
    /// Bir günü kapatır ve ertesi güne geçer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Karşılaşmaya girmek de <b>tam bir gün</b> yer (GDD §10): sefer katmanı dövüş
    /// bitince bu çağrıyı yapar, dojo'da geçen gün de aynı çağrıyı yapar. Gün başına
    /// iki kez çağrılmaz — "gir–bak–kaç" döngüsünü kapatan kalem budur.
    /// </para>
    /// <para>
    /// Ölü savaşçılara dokunulmaz: onur da revir de canlılar için işler.
    /// </para>
    /// </remarks>
    public DayReport AdvanceDay()
    {
        // Olay upkeep'ten <b>önce</b> işlenir: bozulan erzak o günün alışverişini
        // pahalılaştırmalı, çalınan altın o gün ödenecek hesabı zorlamalı. Sonra
        // işlenseydi aksilik ertesi güne ötelenir ve tampon baskısı bir gün gecikirdi.
        DayEvent? happening = Events.Roll(this, Day);
        ApplyEvent(happening);

        UpkeepReport upkeep = PayUpkeep(happening);

        List<WarriorId> recovered = [];
        List<WarriorId> trained = [];

        foreach (RosterEntry entry in Roster.Living)
        {
            bool fed = !upkeep.Hungry.Contains(entry.Id);

            if (entry.Activity == DojoActivity.Training && fed)
            {
                entry.TrainingDays++;
                trained.Add(entry.Id);
            }

            if (entry.RecoveryDaysRemaining > 0 && fed)
            {
                int days = Tuning.NaturalRecoveryPerDay
                    + (upkeep.Medicated.Contains(entry.Id) ? Economy.MedicineRecoveryDays : 0);

                entry.RecoveryDaysRemaining = Math.Max(0, entry.RecoveryDaysRemaining - days);

                if (entry.RecoveryDaysRemaining == 0)
                {
                    entry.Activity = DojoActivity.Resting;
                    recovered.Add(entry.Id);
                }
            }

            entry.Warrior.Honor = DecayedHonor(entry.Warrior.Honor);
        }

        int closed = Day;
        Day++;
        _offer = null;
        _recruits = null;
        return new DayReport(closed, recovered, trained, upkeep, happening);
    }

    /// <summary>
    /// Günün yiyecek/su/ilaç hesabını kapatır: eksik olan piyasadan alınır, kalan
    /// ambardan yenir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ambar yetmezse <b>revirdekiler önce</b> doyurulur. Sıra keyfî olamaz: aç kalan
    /// savaşçı o gün ne iyileşir ne antrenman yapar, ve yarası olanı aç bırakmak kıtlığı
    /// telafisi olmayan bir cezaya çevirirdi. Kıtlığın bedeli <b>zaman</b>dır, ölüm değil.
    /// </para>
    /// <para>
    /// Kasa eksiye düşmez: parası yetmeyen kalem alınmaz, eksik olarak raporlanır.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Kişi başı ihtiyacı olayın çarpanıyla büyütür.
    /// </summary>
    /// <remarks>
    /// Yukarı yuvarlanır: yarım ölçek pirinç diye bir şey yok, ve aksiliğin bedeli
    /// yuvarlamada kaybolmamalı.
    /// </remarks>
    private static int Scaled(int perWarrior, double factor) =>
        (int)Math.Ceiling(perWarrior * Math.Max(1, factor));

    private void ApplyEvent(DayEvent? happening)
    {
        if (happening is null)
        {
            return;
        }

        if (happening.Gold > 0)
        {
            Resources = Resources with { Gold = Math.Max(0, Resources.Gold - happening.Gold) };
        }

        if (happening.Target is not WarriorId target)
        {
            return;
        }

        RosterEntry? entry = Roster.Find(target);
        if (entry is null || !entry.Warrior.IsAlive)
        {
            return;
        }

        if (happening.RecoveryDays > 0)
        {
            entry.Injure(happening.RecoveryDays);
        }
    }

    private UpkeepReport PayUpkeep(DayEvent? happening)
    {
        List<RosterEntry> living = [.. Roster.Living];
        List<RosterEntry> queue =
        [
            .. living.Where(e => e.RecoveryDaysRemaining > 0),
            .. living.Where(e => e.RecoveryDaysRemaining == 0),
        ];

        int wounded = living.Count(e => e.RecoveryDaysRemaining > 0);
        int foodPer = Scaled(Economy.FoodPerWarriorPerDay, happening?.FoodFactor ?? 1);
        int waterPer = Scaled(Economy.WaterPerWarriorPerDay, happening?.WaterFactor ?? 1);
        bool medicineWorks = happening?.MedicineWorks ?? true;

        Resources need = new(
            Gold: 0,
            Food: living.Count * foodPer,
            Water: living.Count * waterPer,
            Medicine: medicineWorks ? wounded * Economy.MedicinePerInfirmaryDay : 0);

        int spent = Quartermaster.Restock(this, need);

        int food = Math.Min(Resources.Food, need.Food);
        int water = Math.Min(Resources.Water, need.Water);
        int medicine = Math.Min(Resources.Medicine, need.Medicine);

        Resources = Resources with
        {
            Food = Resources.Food - food,
            Water = Resources.Water - water,
            Medicine = Resources.Medicine - medicine,
        };

        int fedMouths = foodPer <= 0 ? living.Count : food / foodPer;
        int wateredMouths = waterPer <= 0 ? living.Count : water / waterPer;
        int served = Math.Min(fedMouths, wateredMouths);

        int dosed = !medicineWorks
            ? 0
            : Economy.MedicinePerInfirmaryDay <= 0
                ? wounded
                : medicine / Economy.MedicinePerInfirmaryDay;

        HashSet<WarriorId> hungry = [.. queue.Skip(served).Select(e => e.Id)];
        HashSet<WarriorId> medicated =
            [.. queue.Where(e => e.RecoveryDaysRemaining > 0).Take(dosed).Select(e => e.Id)];
        medicated.ExceptWith(hungry);

        return new UpkeepReport(spent, food, water, medicine, hungry, medicated);
    }

    /// <summary>
    /// Bugünün karşılaşma teklifi (GDD §10: günde tek teklif, al ya da bırak).
    /// </summary>
    /// <remarks>
    /// Her okuyuşta aynı teklif döner ve saklanmaz: üretim gün ile tohumun saf bir
    /// fonksiyonu. Kaydı yükleyip beğenmediği teklifi yeniden yükleyerek değiştirmek de
    /// bu yüzden işe yaramaz.
    /// </remarks>
    public EncounterOffer Offer => _offer ??= Encounters.Offer(Day, Seed);

    /// <summary>
    /// Bugün pazarda duran adaylar.
    /// </summary>
    /// <remarks>
    /// Gün içinde <b>dondurulur</b>. Pazarın seviyesi kadronun ortalamasını takip ettiği
    /// için, dondurulmasaydı bir aday satın almak kalan adayları anında değiştirirdi:
    /// oyuncu ucuz birini alıp listeyi yeniden çevirerek istediği adayı elde ederdi.
    /// </remarks>
    public IReadOnlyList<RecruitOffer> Recruits =>
        _recruits ??= Market.Stock(Day, Seed, Market.AnchorFor(Roster), Economy.RecruitPrice);

    /// <summary>
    /// Teklifi geri çevirir: gün dojo'da geçer.
    /// </summary>
    /// <remarks>
    /// Kabul etmenin karşılığı burada <b>yok</b>: dövüşü kurmak sefer katmanının işi
    /// (<see cref="Expedition"/>), günü kapatmak yine <see cref="AdvanceDay"/>. İkisi
    /// tek çağrıda birleşseydi çekirdek dövüş çözümleyicisine bağlanırdı.
    /// </remarks>
    public DayReport Decline() => AdvanceDay();

    /// <summary>Kayıttan gelen gün sayacını yerine koyar.</summary>
    internal void RestoreDay(int day)
    {
        Day = Math.Max(1, day);
        _offer = null;
        _recruits = null;
    }

    /// <summary>Kayıttan gelen sefer tohumunu yerine koyar.</summary>
    internal void RestoreSeed(ulong seed)
    {
        Seed = seed;
        _offer = null;
        _recruits = null;
    }

    /// <summary>Onuru nötre doğru bir gün kadar çeker; eşiği geçip öbür tarafa sarkmaz.</summary>
    private double DecayedHonor(double honor)
    {
        double step = Tuning.HonorDecayPerDay;
        if (step <= 0)
        {
            return honor;
        }

        double distance = HonorScale.Starting - honor;
        if (Math.Abs(distance) <= step)
        {
            return HonorScale.Starting;
        }

        return HonorScale.Clamp(honor + Math.Sign(distance) * step);
    }
}

/// <summary>Kapanan günün özeti — arayüzün "bugün ne oldu" ekranını besler.</summary>
/// <param name="Day">Kapanan gün (yeni gün bunun bir fazlasıdır).</param>
/// <param name="Recovered">O gün revirden çıkan savaşçılar.</param>
/// <param name="Trained">O gün antrenman alanında geçiren savaşçılar.</param>
/// <param name="Upkeep">Günün yiyecek/su/ilaç hesabı.</param>
/// <param name="Event">O günün aksiliği; sakin gün geçtiyse <c>null</c>.</param>
public sealed record DayReport(
    int Day,
    IReadOnlyList<WarriorId> Recovered,
    IReadOnlyList<WarriorId> Trained,
    UpkeepReport Upkeep,
    DayEvent? Event = null);

/// <summary>Bir günün ambar ve kasa hareketi.</summary>
/// <param name="GoldSpent">O gün piyasadan alınan stok için ödenen altın.</param>
/// <param name="Food">Yenen yiyecek.</param>
/// <param name="Water">İçilen su.</param>
/// <param name="Medicine">Kullanılan ilaç.</param>
/// <param name="Hungry">Payına düşmeyen savaşçılar — o gün iyileşmez, antrenman yapmaz.</param>
/// <param name="Medicated">İlaç alan savaşçılar — o gün fazladan revir günü eritir.</param>
public sealed record UpkeepReport(
    int GoldSpent,
    int Food,
    int Water,
    int Medicine,
    IReadOnlySet<WarriorId> Hungry,
    IReadOnlySet<WarriorId> Medicated)
{
    /// <summary>Kadronun tamamı doyduysa <c>true</c>.</summary>
    public bool Fed => Hungry.Count == 0;
}
