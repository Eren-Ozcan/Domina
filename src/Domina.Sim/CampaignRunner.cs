using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Sim;

/// <summary>Bir dojo'nun gün gün oynatılmış hâli.</summary>
/// <remarks>
/// <para>
/// Ekonomi sayıları (Açık Karar #5) tek bir dövüşe bakarak kilitlenemez: zırhın bedeli
/// <b>seferler boyunca</b> birikir, revir günü geliri değil <b>zamanı</b> yer, ölen
/// savaşçının yerine alınan yeni savaşçı da kasadan çıkar. Bu yüzden ölçüm birimi dövüş
/// değil, <b>sefer dizisi</b>dir: aynı kadro aynı düşmanla günlerce karşılaşır ve kasanın
/// eğrisine bakılır.
/// </para>
/// <para>
/// Oyuncu yerine sabit bir <b>politika</b> oynar (onar, yenile, adam al, sefere çık).
/// Politika akıllı olmak zorunda değil; <b>aynı</b> olmak zorunda — iki fiyat ayarı ancak
/// aynı davranışın altında karşılaştırılabilir.
/// </para>
/// </remarks>
internal sealed record CampaignOptions(
    Scenario Scenario,
    int Days,
    int Campaigns,
    int PartySize,
    int RosterTarget,
    int StartingGold,
    double RepairAtWearShare,
    int ReserveDays,
    EconomyTuning Economy,
    DojoTuning Dojo,
    CombatTuning Tuning,
    IRetreatPolicy? RetreatPolicy,
    bool UseOffers = false,
    EncounterTuning? Encounters = null,
    ThreatBand AcceptUpTo = ThreatBand.Dire,
    bool CautiousWhenThin = false,
    EventTuning? Events = null,
    bool UseMarket = false,
    MarketTuning? Market = null,
    MarketPick Pick = MarketPick.Value)
{
    public const int DefaultDays = 60;
    public const int DefaultCampaigns = 200;
    public const int DefaultPartySize = 3;
    public const int DefaultRosterTarget = 4;
    public const int DefaultStartingGold = 600;

    /// <summary>Kaç günlük yiyecek parası çeliğe yatırılmadan bekletilir.</summary>
    public const int DefaultReserveDays = 10;

    /// <summary>
    /// Hangi yıpranma payından sonra onarım yapılır.
    /// </summary>
    /// <remarks>
    /// Politikanın tek gerçek kararı budur: erken onarmak parayı erken harcar, geç
    /// onarmak parçayı dövüşün ortasında dağıtır. Yarı yol, iki ucun da ölçülebileceği
    /// nötr başlangıçtır.
    /// </remarks>
    public const double DefaultRepairAtWearShare = 0.5;
}

/// <summary>Pazardan aday seçme politikası.</summary>
/// <remarks>
/// İki uç kasten ayrı tutulur: "ucuz ham adayı al" ile "parası yeten en iyisini al"
/// tasarımın rakip olmasını istediği iki strateji. Ölçüm ancak ikisi ayrı koşturulursa
/// hangisinin kazandığını söyleyebilir.
/// </remarks>
internal enum MarketPick
{
    /// <summary>Altın başına en çok stat — ucuz ve ham tarafa kayar.</summary>
    Value,

    /// <summary>Parası yeten en yüksek statlı aday — pahalı ve hazır taraf.</summary>
    Best,
}

/// <summary>Bir dojo'yu gün gün oynatır.</summary>
internal sealed class CampaignRunner(CampaignOptions options)
{
    private readonly CampaignOptions _options = options
        ?? throw new ArgumentNullException(nameof(options));

    public CampaignReport Run(ulong firstSeed)
    {
        CampaignReport report = new(_options.Days);

        for (int i = 0; i < _options.Campaigns; i++)
        {
            report.Add(RunOne(firstSeed + ((ulong)i * 1_000_003)));
        }

        return report;
    }

    private CampaignRow RunOne(ulong seed)
    {
        DojoState state = new(
            _options.Dojo,
            _options.Economy,
            seed,
            _options.Encounters,
            _options.Events,
            _options.Market);
        state.Resources = new Resources(Gold: _options.StartingGold);

        // Kadro senaryonun kendi kadrosundan çoğaltılır: ekonomiyi ölçerken dövüş
        // dengesinin ölçüldüğü kadronun dışına çıkmak, iki ölçümü kıyaslanamaz yapardı.
        IReadOnlyList<Warrior> template = _options.Scenario.Build().PlayerSide;
        for (int i = 0; i < _options.RosterTarget; i++)
        {
            Enlist(state, template, i);
        }

        CampaignRow row = new();
        int hired = 0;

        for (int day = 0; day < _options.Days; day++)
        {
            row.GoldSpentOnGear += Maintain(state, template);

            if (Hire(state, template, ref hired))
            {
                row.Hires++;
            }

            DayReport closed = _options.UseOffers
                ? TakeOfferOrRest(state, seed + (ulong)day, row)
                : FightScenarioOrRest(state, seed + (ulong)day, row);

            if (closed.Event is DayEvent mishap)
            {
                row.Mishaps++;
                row.MishapGold += mishap.Gold;
            }
            row.GoldSpentOnUpkeep += closed.Upkeep.GoldSpent;
            if (!closed.Upkeep.Fed)
            {
                row.HungryDays++;
            }

            if (!state.Roster.Living.Any())
            {
                row.Collapsed = true;
                row.DaysSurvived = day + 1;
                return row;
            }
        }

        row.DaysSurvived = _options.Days;
        row.EndingGold = state.Resources.Gold;
        row.SurvivingWarriors = state.Roster.Living.Count();
        return row;
    }

    /// <summary>Sabit senaryo kipi: kadro yeterse dövüş, yetmezse antrenman.</summary>
    private DayReport FightScenarioOrRest(DojoState state, ulong seed, CampaignRow row)
    {
        List<RosterEntry> party = [.. state.Roster.FitForCampaign.Take(_options.PartySize)];
        if (party.Count != _options.PartySize)
        {
            return Rest(state, row);
        }

        Fight(state, party, seed, row);
        return state.AdvanceDay();
    }

    /// <summary>
    /// Teklif kipi: günün teklifi gelir, ekip yetiyorsa girilir, yetmiyorsa gün dojo'da geçer.
    /// </summary>
    /// <remarks>
    /// Politikanın teklifi eleme hakkı <see cref="CampaignOptions.AcceptUpTo"/> ile açılır.
    /// Varsayılan her teklifi kabul eder: eğrinin dikliğini ölçmek istiyorsak politikanın
    /// eğriden kaçmaması gerekir — "Dire gördüm, girmedim" diyen bir dojo eğrinin sert ucunu
    /// hiç ölçmez. Elemenin <b>kendisi</b> ölçülmek istendiğinde (GDD §10'un "al ya da bırak"
    /// kararı gerçekten hayat kurtarıyor mu) bant düşürülür.
    /// </remarks>
    private DayReport TakeOfferOrRest(DojoState state, ulong seed, CampaignRow row)
    {
        EncounterOffer offer = state.Offer;
        if (Declines(state, offer))
        {
            return Rest(state, row, declined: true);
        }

        int wanted = offer.RequiredPartySize ?? _options.PartySize;

        List<RosterEntry> party = [.. state.Roster.FitForCampaign.Take(wanted)];
        if (party.Count != wanted || Expedition.Refuse(state, offer, party) is not null)
        {
            return Rest(state, row);
        }

        ExpeditionResult result = new Expedition().Send(
            state,
            offer,
            party,
            new SeededRandom(seed),
            _options.Tuning,
            _options.RetreatPolicy);

        row.Battles++;
        row.GoldEarned += result.Reward;
        if (result.Battle.Outcome == BattleOutcome.PlayerVictory)
        {
            row.Victories++;
        }

        row.Deaths += result.Aftermath.Dead.Count();
        row.RecoveryDays += result.Aftermath.Warriors.Sum(w => w.RecoveryDays);
        row.ArmorPiecesLost += result.Aftermath.Warriors.Sum(w => w.ShatteredArmor.Count);
        row.WarriorBattles += party.Count;
        row.PowerSum += offer.EnemyHealth;

        return result.Day;
    }

    /// <summary>Teklif kadroya göre fazla ağır mı?</summary>
    private bool Declines(DojoState state, EncounterOffer offer)
    {
        if (offer.Threat > _options.AcceptUpTo)
        {
            return true;
        }

        // Kadro eksikken ağır teklife girmek, eksik kadroyu daha da eksiltir.
        return _options.CautiousWhenThin
            && offer.Threat >= ThreatBand.Heavy
            && state.Roster.Living.Count() < _options.RosterTarget;
    }

    private static DayReport Rest(DojoState state, CampaignRow row, bool declined = false)
    {
        row.IdleDays++;
        if (declined)
        {
            row.DeclinedOffers++;
        }

        foreach (RosterEntry entry in state.Roster.FitForCampaign)
        {
            entry.Train();
        }

        return state.AdvanceDay();
    }

    private void Fight(DojoState state, List<RosterEntry> party, ulong seed, CampaignRow row)
    {
        BattleSetup template = _options.Scenario.Build();
        BattleSetup setup = new([.. party.Select(e => e.Warrior)], template.EnemySide)
        {
            Tuning = _options.Tuning,
            RetreatPolicy = _options.RetreatPolicy,
            CollectEvents = false,
        };

        BattleResult result = new Battle(setup, new SeededRandom(seed)).Run();
        AftermathReport aftermath = new BattleAftermath().Apply(state, result);

        int reward = state.Quartermaster.RewardFor(setup, result.Outcome);
        state.Resources = state.Resources with { Gold = state.Resources.Gold + reward };

        row.Battles++;
        row.GoldEarned += reward;
        if (result.Outcome == BattleOutcome.PlayerVictory)
        {
            row.Victories++;
        }

        row.Deaths += aftermath.Dead.Count();
        row.RecoveryDays += aftermath.Warriors.Sum(w => w.RecoveryDays);
        row.ArmorPiecesLost += aftermath.Warriors.Sum(w => w.ShatteredArmor.Count);
        row.WarriorBattles += party.Count;
        row.PowerSum += setup.EnemySide.Sum(e => e.EffectiveStats.MaxHealth);
    }

    /// <summary>
    /// Kuşamı ayakta tutar: eşiği geçen yuvayı onarır, dağılmış yuvaya yenisini takar.
    /// </summary>
    /// <remarks>
    /// Sıra önemli — önce onarım, sonra yenileme. Tersi olsaydı politika, parası kısıtlıyken
    /// ucuz onarım yerine pahalı parçayı alır ve fiyat ölçümü politikanın hatasını ölçerdi.
    /// </remarks>
    private int Maintain(DojoState state, IReadOnlyList<Warrior> template)
    {
        int before = state.Resources.Gold;
        int reserve = Reserve(state);

        foreach (RosterEntry entry in state.Roster.Living)
        {
            Warrior warrior = entry.Warrior;
            Armor kit = template[(warrior.Id.Value - 1) % template.Count].Armor;

            foreach (HitLocation slot in ArmorSlots.All)
            {
                ArmorPiece piece = warrior.Armor.At(slot);
                if (piece.IsWorn
                    && piece.Durability > 0
                    && warrior.ArmorWear.At(slot) >= piece.Durability * _options.RepairAtWearShare
                    && Affordable(state, state.Quartermaster.RepairPrice(warrior, slot), reserve))
                {
                    state.Quartermaster.Repair(state, warrior, slot);
                }
            }

            foreach (HitLocation slot in ArmorSlots.All)
            {
                ArmorPiece wanted = kit.At(slot);
                if (!warrior.Armor.At(slot).IsWorn
                    && wanted.IsWorn
                    && Affordable(state, state.Quartermaster.PiecePrice(wanted), reserve))
                {
                    state.Quartermaster.Equip(state, warrior, slot, wanted);
                }
            }
        }

        return before - state.Resources.Gold;
    }

    private bool Hire(DojoState state, IReadOnlyList<Warrior> template, ref int hired)
    {
        if (state.Roster.Living.Count() >= _options.RosterTarget)
        {
            return false;
        }

        int reserve = Reserve(state);
        Warrior proto = template[hired % template.Count];

        if (_options.UseMarket)
        {
            return HireFromMarket(state, proto, reserve, _options.Pick);
        }

        if (!Affordable(state, _options.Economy.RecruitPrice, reserve))
        {
            return false;
        }

        RosterEntry? entry = state.Quartermaster.Hire(
            state,
            $"Yedek {++hired}",
            proto.BaseStats,
            proto.Weapon,
            proto.Armor);

        return entry is not null;
    }

    /// <summary>
    /// Pazardan <b>en çok stat/altın</b> veren adayı alır.
    /// </summary>
    /// <remarks>
    /// Politikanın burada da akıllı olması gerekmiyor, <b>tutarlı</b> olması gerekiyor:
    /// ölçülmek istenen şey "ucuz ham aday mı, pahalı hazır aday mı" sorusunun kendisi,
    /// politikanın zekâsı değil. Değer başına seçim, iki ucu da doğal olarak yoklar.
    /// </remarks>
    private static bool HireFromMarket(DojoState state, Warrior proto, int reserve, MarketPick pick)
    {
        RecruitOffer? best = null;
        double bestValue = 0;

        foreach (RecruitOffer offer in state.Recruits)
        {
            if (!Affordable(state, offer.Price, reserve))
            {
                continue;
            }

            double value = pick == MarketPick.Best
                ? Score(offer.Stats)
                : Score(offer.Stats) / Math.Max(1, offer.Price);

            if (best is null || value > bestValue)
            {
                best = offer;
                bestValue = value;
            }
        }

        return best is not null
            && Quartermaster.Hire(state, best, proto.Weapon, proto.Armor) is not null;
    }

    private static double Score(WarriorStats stats) =>
        stats.MaxHealth
        + stats.Strength
        + stats.Accuracy
        + stats.Defense
        + stats.Evasion
        + stats.Speed
        + stats.Aggression;

    /// <summary>Kadroyu senaryonun kadrosundan çoğaltır — silahı ve kuşamıyla.</summary>
    private static void Enlist(DojoState state, IReadOnlyList<Warrior> template, int index)
    {
        Warrior proto = template[index % template.Count];
        state.Roster.Recruit($"Savaşçı {index + 1}", proto.BaseStats, proto.Weapon, proto.Armor);
    }

    /// <summary>
    /// Elden çıkarılmayacak altın: kadronun birkaç günlük yiyeceği.
    /// </summary>
    /// <remarks>
    /// Politikanın son kuruşunu çeliğe yatırması ekonomiyi değil, politikanın aptallığını
    /// ölçerdi: aç kalan savaşçı iyileşmez, iyileşmeyen kadro sefere çıkamaz, ve dojo
    /// onarılmış zırhla açlıktan kilitlenir. Gerçek oyuncu da ambarı boşaltmaz.
    /// </remarks>
    private int Reserve(DojoState state)
    {
        int mouths = Math.Max(1, state.Roster.Living.Count());
        int perDay = (mouths * _options.Economy.FoodPerWarriorPerDay * _options.Economy.FoodPrice)
            + (mouths * _options.Economy.WaterPerWarriorPerDay * _options.Economy.WaterPrice);

        return perDay * _options.ReserveDays;
    }

    private static bool Affordable(DojoState state, int price, int reserve) =>
        price > 0 && state.Resources.Gold - price >= reserve;
}

/// <summary>Tek bir dojo'nun ömrü.</summary>
internal sealed class CampaignRow
{
    public int DaysSurvived { get; set; }

    public int Battles { get; set; }

    public int Victories { get; set; }

    public int IdleDays { get; set; }

    /// <summary>Ağır bulunup geri çevrilen teklif sayısı.</summary>
    public int DeclinedOffers { get; set; }

    /// <summary>Başa gelen aksilik sayısı.</summary>
    public int Mishaps { get; set; }

    /// <summary>Aksiliklerin kasadan doğrudan aldığı altın.</summary>
    public int MishapGold { get; set; }

    public int HungryDays { get; set; }

    public int Deaths { get; set; }

    public int Hires { get; set; }

    public int RecoveryDays { get; set; }

    public int ArmorPiecesLost { get; set; }

    /// <summary>Savaşçı-dövüş sayısı — ölüm oranının paydası.</summary>
    public int WarriorBattles { get; set; }

    /// <summary>Karşılaşılan düşman canının toplamı — eğrinin dikliği buradan okunur.</summary>
    public double PowerSum { get; set; }

    public int GoldEarned { get; set; }

    public int GoldSpentOnGear { get; set; }

    public int GoldSpentOnUpkeep { get; set; }

    public int EndingGold { get; set; }

    public int SurvivingWarriors { get; set; }

    /// <summary>Kadroda kimse kalmadı — dojo kapandı.</summary>
    public bool Collapsed { get; set; }
}

/// <summary>Bir sürü dojo ömrünün toplamı.</summary>
internal sealed class CampaignReport(int days)
{
    private readonly List<CampaignRow> _rows = [];

    public int PlannedDays { get; } = days;

    public int Campaigns => _rows.Count;

    public void Add(CampaignRow row) => _rows.Add(row);

    public double AverageBattles => Average(r => r.Battles);

    public double VictoryRate => _rows.Sum(r => r.Battles) == 0
        ? 0
        : (double)_rows.Sum(r => r.Victories) / _rows.Sum(r => r.Battles);

    public double IdleDayShare => Share(r => r.IdleDays);

    /// <summary>Geri çevrilen teklifin gün payı (GDD §10: al ya da bırak).</summary>
    public double DeclinedShare => Share(r => r.DeclinedOffers);

    /// <summary>Aksilik çıkan günlerin payı.</summary>
    public double MishapDayShare => Share(r => r.Mishaps);

    /// <summary>Aksiliklerin doğrudan götürdüğü günlük altın.</summary>
    public double MishapGoldPerDay => Share(r => r.MishapGold);

    public double HungryDayShare => Share(r => r.HungryDays);

    public double CollapseRate => Campaigns == 0 ? 0 : (double)_rows.Count(r => r.Collapsed) / Campaigns;

    public double AverageDeaths => Average(r => r.Deaths);

    /// <summary>Dojo'nun ayakta kaldığı gün — kapanmayanlar için planlanan gün sayısı.</summary>
    public double AverageDaysSurvived => Average(r => r.DaysSurvived);

    /// <summary>Dojoların yarısının kapandığı gün; hiçbiri kapanmadıysa planlanan gün.</summary>
    public int MedianDaysSurvived
    {
        get
        {
            if (_rows.Count == 0)
            {
                return 0;
            }

            List<int> days = [.. _rows.Select(r => r.DaysSurvived).Order()];
            return days[days.Count / 2];
        }
    }

    public double AverageHires => Average(r => r.Hires);

    public double AverageEndingGold => Average(r => r.EndingGold);

    public double AverageArmorPiecesLost => Average(r => r.ArmorPiecesLost);

    public double RecoveryDaysPerBattle => PerBattle(r => r.RecoveryDays);

    /// <summary>Savaşçı-dövüş başına ölüm — ekonominin bağlayıcı kısıtı (GDD §11).</summary>
    public double DeathPerWarriorBattle
    {
        get
        {
            int appearances = _rows.Sum(r => r.WarriorBattles);
            return appearances == 0 ? 0 : (double)_rows.Sum(r => r.Deaths) / appearances;
        }
    }

    /// <summary>Karşılaşma başına düşman canı — eğrinin ortalama yüksekliği.</summary>
    public double EnemyHealthPerBattle
    {
        get
        {
            int battles = _rows.Sum(r => r.Battles);
            return battles == 0 ? 0 : _rows.Sum(r => r.PowerSum) / battles;
        }
    }

    public double GoldEarnedPerBattle => PerBattle(r => r.GoldEarned);

    public double GearGoldPerBattle => PerBattle(r => r.GoldSpentOnGear);

    public double UpkeepGoldPerDay => Share(r => r.GoldSpentOnUpkeep);

    /// <summary>Dövüş başına net kâr — ekonominin tek cümlelik cevabı.</summary>
    public double NetGoldPerBattle
    {
        get
        {
            int battles = _rows.Sum(r => r.Battles);
            if (battles == 0)
            {
                return 0;
            }

            int net = _rows.Sum(r => r.GoldEarned - r.GoldSpentOnGear - r.GoldSpentOnUpkeep);
            return (double)net / battles;
        }
    }

    /// <summary>Kasası artan dojo oranı — başlangıç sermayesini koruyanlar.</summary>
    public double SolventRate(int startingGold) => Campaigns == 0
        ? 0
        : (double)_rows.Count(r => !r.Collapsed && r.EndingGold >= startingGold) / Campaigns;

    private double Average(Func<CampaignRow, int> pick) =>
        Campaigns == 0 ? 0 : (double)_rows.Sum(pick) / Campaigns;

    private double Share(Func<CampaignRow, int> pick)
    {
        int days = _rows.Sum(r => r.DaysSurvived);
        return days == 0 ? 0 : (double)_rows.Sum(pick) / days;
    }

    private double PerBattle(Func<CampaignRow, int> pick)
    {
        int battles = _rows.Sum(r => r.Battles);
        return battles == 0 ? 0 : (double)_rows.Sum(pick) / battles;
    }
}
