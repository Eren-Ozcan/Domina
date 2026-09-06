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
    private readonly DojoTuning _baseTuning;
    private readonly EconomyTuning _baseEconomy;
    private EncounterOffer? _offer;
    private IReadOnlyList<RecruitOffer>? _recruits;
    private BountyContract? _bounty;
    private bool _bountyRead;
    private readonly HashSet<int> _hiredToday = [];

    public DojoState(
        DojoTuning? tuning = null,
        EconomyTuning? economy = null,
        ulong seed = 1,
        EncounterTuning? encounters = null,
        EventTuning? events = null,
        MarketTuning? market = null,
        BountyTuning? bounties = null,
        SchoolTuning? school = null)
    {
        _baseTuning = tuning ?? new DojoTuning();
        _baseEconomy = economy ?? new EconomyTuning();
        School = new School(school);
        Encounters = new EncounterGenerator(encounters);
        Events = new DayEventTable(events);
        Market = new RecruitMarket(market);
        Bounties = new BountyBoard(bounties, encounters);
        Seed = seed;
        Tuning = _baseTuning;
        Quartermaster = new Quartermaster(_baseEconomy);
        ApplySchool();
    }

    /// <summary>
    /// Gün döngüsünün ayarları — <b>okul işlenmiş hâliyle</b>.
    /// </summary>
    /// <remarks>
    /// Okumaların hepsi buradan geçer; ham ayar dışarıya verilmez. Aksi hâlde bir yer
    /// tesisli, bir yer tesissiz sayı okur ve bonus sessizce yarım işlerdi.
    /// </remarks>
    public DojoTuning Tuning { get; private set; }

    /// <summary>Fiyatlar ve alışveriş. Ekonomi sayıları buradan okunur.</summary>
    public Quartermaster Quartermaster { get; private set; }

    /// <summary>Dojo'nun tesisleri — ölmeyen yatırım (GDD §10).</summary>
    public School School { get; }

    public EconomyTuning Economy => Quartermaster.Economy;

    /// <summary>Günün teklifini üreten çark.</summary>
    public EncounterGenerator Encounters { get; }

    /// <summary>Günün aksiliğini çeken tablo (GDD §11: rastgele olaylar).</summary>
    public DayEventTable Events { get; }

    /// <summary>Savaşçı pazarı.</summary>
    public RecruitMarket Market { get; }

    /// <summary>Kelle avı sözleşmelerini asan tahta.</summary>
    public BountyBoard Bounties { get; }

    /// <summary>Kabul edilmiş sözleşme varsa onun asıldığı gün; yoksa <c>null</c>.</summary>
    /// <remarks>
    /// Sözleşmenin kendisi saklanmaz — günden ve tohumdan yeniden hesaplanır. Saklanması
    /// gereken tek şey <b>söz verilip verilmediği</b>, o da tek bir sayı.
    /// </remarks>
    public int? AcceptedBountyDay { get; private set; }

    /// <summary>Kellesi alınmış sözleşmenin asıldığı gün; yoksa <c>null</c>.</summary>
    /// <remarks>
    /// Tahta saf olduğu için sözleşme, süresi dolana kadar her gün yeniden üretilir. Bu
    /// kayıt olmadan aynı hedef ertesi gün yeniden asılı görünür ve aynı kelle iki kez
    /// satılırdı — ölçüldü, dojo başına 60 günde 12.7 kelle avı çıktı.
    /// </remarks>
    public int? ClaimedBountyDay { get; private set; }

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

                // Antrenman <b>ham</b> statı yazar, etkin statı değil: sakatlığın çarpanı
                // kalıcıdır ve eğitimle geri alınmaz (GDD §7). Kolunu kaybeden savaşçı
                // çalışarak toparlanır, ama kaybettiği kolu geri kazanmaz.
                entry.Warrior.BaseStats = TrainingGround.After(
                    entry.Warrior.BaseStats,
                    entry.Drill,
                    entry.Warrior.Talent,
                    Tuning.Training);

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

        // Söz, günün sonunda tartılır: son gün de dövüşmeden kapandıysa sözleşme kırılmıştır.
        bool broken = BreakBountyIfExpired();

        int closed = Day;
        Day++;
        _offer = null;
        _recruits = null;
        _hiredToday.Clear();
        _bounty = null;
        _bountyRead = false;
        return new DayReport(closed, recovered, trained, upkeep, happening, broken);
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

    /// <summary>Bugün tezgâhtan alınmış adayların sıraları.</summary>
    /// <remarks>
    /// Gün kapanınca boşalır — yarın tezgâhta başka adaylar durur
    /// (<see cref="MarketTuning.RefreshDays"/>) ve eski işaret yanlış adamı kapatırdı.
    /// </remarks>
    public IReadOnlyCollection<int> HiredToday => _hiredToday;

    /// <summary>
    /// Tezgâhtaki adayı satın alır.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Alım günü yemez:</b> pazar gün boyu açıktır, kasa ve tezgâh el verdiği sürece
    /// birden fazla savaşçı alınabilir. Günü yiyen şey sefere çıkmak ya da günü dojo'da
    /// geçirmektir — alım sayısına ayrıca bir tavan konsaydı, aynı gün iki ölünün yerine
    /// iki savaşçı koymak imkânsızlaşırdı.
    /// </para>
    /// <para>
    /// Alınan aday <b>kayda geçer</b> ve tezgâhtan düşer. Tezgâh gün içinde donduğu için
    /// (<see cref="Recruits"/>) bu kayıt olmadan aynı aday sınırsız kez satılırdı: tek
    /// bir kişi kadronun tamamına dönüşürdü. Ekranın işaretine bırakılamaz — kaydı
    /// yükleyip aynı adamı yeniden almak da aynı kapıdır.
    /// </para>
    /// </remarks>
    /// <param name="index">Adayın <see cref="Recruits"/> içindeki sırası.</param>
    /// <param name="weapon">Kuşandırılacak silah; verilmezse varsayılan.</param>
    /// <param name="armor">Kuşandırılacak zırh; verilmezse varsayılan.</param>
    /// <returns>Alındıysa kadro kaydı; tezgâhta yoksa, alınmışsa ya da para yetmiyorsa <c>null</c>.</returns>
    public RosterEntry? HireRecruit(int index, Weapon? weapon = null, Armor? armor = null)
    {
        if (index < 0 || index >= Recruits.Count || _hiredToday.Contains(index))
        {
            return null;
        }

        RosterEntry? entry = Quartermaster.Hire(this, Recruits[index], weapon, armor);
        if (entry is not null)
        {
            _hiredToday.Add(index);
        }

        return entry;
    }

    /// <summary>Bugün tahtada asılı sözleşme; yoksa <c>null</c>.</summary>
    /// <remarks>
    /// Teklif gibi gün içinde sabittir ve saklanmaz: aynı gün ve aynı tohum daima aynı
    /// sözleşmeyi verir. Sözleşme <b>günlük teklifin yerine geçmez</b>, yanında durur —
    /// gün yine tek iş yer, hangi işi yapacağın karardır.
    /// </remarks>
    public BountyContract? Bounty
    {
        get
        {
            if (!_bountyRead)
            {
                BountyContract? posted = Bounties.Posted(Day, Seed, Economy);
                _bounty = posted?.PostedDay == ClaimedBountyDay ? null : posted;
                _bountyRead = true;
            }

            return _bounty;
        }
    }

    /// <summary>Bugünün sözleşmesini kabul eder — bir söz verilir.</summary>
    /// <remarks>
    /// Kabul etmek günü <b>yemez</b>: sözleşme kabul edilip aynı gün başka bir iş
    /// yapılabilir. Yediği şey süredir — son güne kadar dönülmezse kadro onur kaybeder
    /// (<see cref="BountyContract.BrokenHonorPenalty"/>).
    /// </remarks>
    /// <returns>Kabul edilebildiyse sözleşme, edilemediyse <c>null</c>.</returns>
    public BountyContract? AcceptBounty()
    {
        if (Bounty is not BountyContract open || AcceptedBountyDay is not null)
        {
            return null;
        }

        AcceptedBountyDay = open.PostedDay;
        return open;
    }

    /// <summary>Kelle alındı: söz kapanır ve sözleşme tahtadan iner.</summary>
    internal void CloseBounty(int postedDay)
    {
        AcceptedBountyDay = null;
        ClaimedBountyDay = postedDay;
        _bounty = null;
        _bountyRead = false;
    }

    /// <summary>Kayıttan gelen sözü yerine koyar.</summary>
    /// <remarks>
    /// Sözleşmenin kendisi kayda yazılmaz, günden ve tohumdan yeniden hesaplanır; yazılan
    /// tek şey söz verilip verilmediğidir. Aksi hâlde oyuncu kaydı yeniden yükleyerek
    /// verdiği sözden kurtulurdu.
    /// </remarks>
    internal void RestoreBounty(int? acceptedDay, int? claimedDay)
    {
        AcceptedBountyDay = acceptedDay;
        ClaimedBountyDay = claimedDay;
        _bounty = null;
        _bountyRead = false;
    }

    /// <summary>
    /// Teklifi geri çevirir: gün dojo'da geçer.
    /// </summary>
    /// <remarks>
    /// Kabul etmenin karşılığı burada <b>yok</b>: dövüşü kurmak sefer katmanının işi
    /// (<see cref="Expedition"/>), günü kapatmak yine <see cref="AdvanceDay"/>. İkisi
    /// tek çağrıda birleşseydi çekirdek dövüş çözümleyicisine bağlanırdı.
    /// </remarks>
    public DayReport Decline() => AdvanceDay();

    /// <summary>
    /// Okuldan bir tesis satın alır.
    /// </summary>
    /// <remarks>
    /// Tesisin bedeli <b>peşin</b>dir ve geri satılmaz: okul kalıcı bir yatırımdır, geri
    /// alınabilseydi oyuncu her sefer öncesi ağacı yeniden dizerdi. Sırası gelmemiş ya da
    /// parası yetmeyen düğüm alınmaz; kasa eksiye düşmez.
    /// </remarks>
    /// <returns>Alındıysa <c>true</c>.</returns>
    public bool BuySchoolNode(SchoolNodeId id)
    {
        SchoolNode node = SchoolTree.Find(id);
        if (School.Has(id) || node.Cost > Resources.Gold || !School.Add(id))
        {
            return false;
        }

        Resources = Resources with { Gold = Resources.Gold - node.Cost };
        ApplySchool();
        return true;
    }

    /// <summary>
    /// Savaşçının yolunu seçer — bir kez, geri dönüşsüz.
    /// </summary>
    /// <remarks>
    /// Kilidi açan şey antrenman günüdür (<see cref="TrainingTuning.PathTrainingDays"/>):
    /// yol, satın alınan değil <b>çalışılarak kazanılan</b> bir şey olmalı.
    /// </remarks>
    /// <returns>Seçilebildiyse <c>true</c>.</returns>
    public bool ChoosePath(WarriorId id, WarriorPath path)
    {
        RosterEntry? entry = Roster.Find(id);
        if (entry is null
            || path == WarriorPath.None
            || !entry.Warrior.IsAlive
            || entry.Warrior.Path != WarriorPath.None
            || entry.TrainingDays < Tuning.Training.PathTrainingDays)
        {
            return false;
        }

        entry.Warrior.Path = path;
        return true;
    }

    /// <summary>Kayıttan gelen tesisleri yerine koyar.</summary>
    /// <summary>Kayıttan gelen "bugün alınmış adaylar" işaretini yerine koyar.</summary>
    /// <remarks>
    /// Adayların kendisi kayda yazılmaz (günden ve tohumdan yeniden üretilir), yazılan
    /// tek şey <b>hangi sıraların</b> alındığı. Yazılmasaydı oyuncu kaydı yeniden
    /// yükleyerek aynı adayı tekrar tekrar satın alırdı.
    /// </remarks>
    internal void RestoreHiredToday(IEnumerable<int> indexes)
    {
        ArgumentNullException.ThrowIfNull(indexes);

        _hiredToday.Clear();
        foreach (int index in indexes)
        {
            _hiredToday.Add(index);
        }
    }

    internal void RestoreSchool(IEnumerable<SchoolNodeId> owned)
    {
        School.Restore(owned);
        ApplySchool();
    }

    /// <summary>Tesisleri ayarlara işler — satın alma ve kayıt yükleme sonrası.</summary>
    private void ApplySchool()
    {
        Tuning = School.Apply(_baseTuning);
        Quartermaster = new Quartermaster(School.Apply(_baseEconomy));
    }

    /// <summary>Kayıttan gelen gün sayacını yerine koyar.</summary>
    internal void RestoreDay(int day)
    {
        Day = Math.Max(1, day);
        _offer = null;
        _recruits = null;
        _hiredToday.Clear();
        _bounty = null;
        _bountyRead = false;
    }

    /// <summary>Kayıttan gelen sefer tohumunu yerine koyar.</summary>
    internal void RestoreSeed(ulong seed)
    {
        Seed = seed;
        _offer = null;
        _recruits = null;
        _bounty = null;
        _bountyRead = false;
    }

    /// <summary>
    /// Kabul edilip son günü geçen sözleşmenin bedelini keser.
    /// </summary>
    /// <remarks>
    /// Ceza <b>kadronun tamamına</b> yazılır, sefere gidecek olana değil: sözü dojo verdi,
    /// bir savaşçı değil. Tek kişiye yazılsaydı oyuncu cezayı zaten gözden çıkardığı bir
    /// savaşçının üstüne yıkar, söz de bedelsiz kalırdı — çekilmenin bedeli de aynı
    /// sebeple tüm ekibe yazılıyor (GDD §5).
    /// </remarks>
    private bool BreakBountyIfExpired()
    {
        if (AcceptedBountyDay is not int accepted)
        {
            return false;
        }

        // Ölçü bugünün değil <b>yarının</b> durumu: son gün de dövüşmeden kapandıysa söz
        // kırılmıştır. Bugüne bakılsaydı ceza bir gün geç düşer, oyuncu son günün
        // akşamında hâlâ "sözüm duruyor" sayılırdı.
        BountyContract? open = Bounty;
        if (open is not null && open.PostedDay == accepted && Day < open.Deadline)
        {
            return false;
        }

        double penalty = open?.PostedDay == accepted
            ? open.BrokenHonorPenalty
            : Bounties.Tuning.BrokenHonorPenalty;

        foreach (RosterEntry entry in Roster.Living)
        {
            entry.Warrior.Honor = HonorScale.Clamp(entry.Warrior.Honor - penalty);
        }

        AcceptedBountyDay = null;
        return true;
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
    DayEvent? Event = null,
    bool BountyBroken = false);

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
