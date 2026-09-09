using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Sim;

/// <summary>A dojo played out day by day.</summary>
/// <remarks>
/// <para>
/// The economy numbers (Open Decision #5) cannot be locked in by looking at a single fight: armour's
/// price accumulates <b>across expeditions</b>, an infirmary day eats <b>time</b> rather than income,
/// and the new warrior hired to replace a dead one also comes out of the treasury. So the unit of
/// measurement is not the fight but the <b>expedition series</b>: the same roster meets the same enemy
/// for days on end and the treasury's curve is examined.
/// </para>
/// <para>
/// A fixed <b>policy</b> plays in the player's place (repair, replace, hire, go on an expedition). The
/// policy does not have to be smart; it has to be <b>the same</b> — two price settings can only be
/// compared under the same behaviour.
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
    MarketPick Pick = MarketPick.Value,
    bool UseBounties = false,
    double? AcceptRatio = null,
    bool UseSchool = false,
    SchoolBranch? SchoolOnly = null,
    bool UsePaths = false)
{
    public const int DefaultDays = 60;
    public const int DefaultCampaigns = 200;
    public const int DefaultPartySize = 3;
    public const int DefaultRosterTarget = 4;
    public const int DefaultStartingGold = 600;

    /// <summary>How many days' worth of food money is held back from being spent on steel.</summary>
    public const int DefaultReserveDays = 10;

    /// <summary>
    /// The wear share after which a repair is made.
    /// </summary>
    /// <remarks>
    /// This is the policy's only real decision: repairing early spends the money early, repairing late
    /// lets the piece break in the middle of a fight. Halfway is the neutral starting point from which
    /// both ends can be measured.
    /// </remarks>
    public const double DefaultRepairAtWearShare = 0.5;
}

/// <summary>The policy for picking a candidate from the market.</summary>
/// <remarks>
/// The two extremes are kept apart on purpose: "buy the cheap raw candidate" and "buy the best you can
/// afford" are two strategies the design wants to be rivals. Measurement can only say which one wins if
/// the two are run separately.
/// </remarks>
internal enum MarketPick
{
    /// <summary>The most stat per gold — it leans to the cheap and raw side.</summary>
    Value,

    /// <summary>The highest-stat candidate you can afford — the expensive, ready-made side.</summary>
    Best,

    /// <summary>The most talented candidate you can afford — the side that invests in promise.</summary>
    /// <remarks>
    /// Measurement <b>required</b> a third extreme: the real basis of the "buy the cheap raw candidate
    /// and train him" strategy is not cheapness but <see cref="RecruitOffer.Talent"/>. A policy that
    /// looks at stat per gold never reads talent, so even after training was written it did not
    /// represent that strategy.
    /// </remarks>
    Talent,
}

/// <summary>Plays one dojo day by day.</summary>
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

        // The roster is cloned from the scenario's own roster: while measuring the economy, stepping
        // outside the roster combat balance was measured on would make the two measurements incomparable.
        IReadOnlyList<Warrior> template = _options.Scenario.Build().PlayerSide;
        for (int i = 0; i < _options.RosterTarget; i++)
        {
            Enlist(state, template, i);
        }

        CampaignRow row = new();
        row.StartScore = BestScore(state);
        int hired = 0;

        for (int day = 0; day < _options.Days; day++)
        {
            row.GoldSpentOnGear += Maintain(state, template);

            if (_options.UseSchool)
            {
                row.GoldSpentOnSchool += BuildSchool(state, row);
            }

            if (_options.UsePaths)
            {
                row.Paths += ChoosePaths(state);
            }

            int purse = state.Resources.Gold;
            if (Hire(state, template, ref hired))
            {
                row.Hires++;
                row.GoldSpentOnHires += purse - state.Resources.Gold;
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
        row.EndScore = BestScore(state);
        row.SchoolNodes = state.School.Owned.Count;
        row.TrainingDays = state.Roster.Living.Sum(e => e.TrainingDays);
        return row;
    }

    /// <summary>Fixed-scenario mode: fight if the roster is enough, train if it is not.</summary>
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
    /// Offer mode: the day's offer arrives, it is entered if the party is enough, otherwise the day passes in the dojo.
    /// </summary>
    /// <remarks>
    /// The policy's right to filter offers is unlocked with <see cref="CampaignOptions.AcceptUpTo"/>.
    /// The default accepts every offer: if we want to measure the steepness of the curve, the policy
    /// must not run away from the curve — a dojo that says "I saw Dire, I did not go in" never measures
    /// the hard end of the curve. When the filtering <b>itself</b> is what we want to measure (does
    /// GDD §10's "take it or leave it" decision really save lives), the band is lowered.
    /// </remarks>
    private DayReport TakeOfferOrRest(DojoState state, ulong seed, CampaignRow row)
    {
        if (_options.UseBounties && TakeBounty(state, seed, row) is DayReport hunted)
        {
            return hunted;
        }

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

    /// <summary>
    /// Goes bounty hunting if there is an open contract and the roster is enough; <c>null</c> if it does not go.
    /// </summary>
    /// <remarks>
    /// The policy is deliberately <b>simple</b>: if the contract's band is within the acceptance limit
    /// and a full party can be sent, it goes in. What we want to measure is not how well the player
    /// chooses but what the contract <b>itself</b> adds to the economy.
    /// </remarks>
    private DayReport? TakeBounty(DojoState state, ulong seed, CampaignRow row)
    {
        if (state.Bounty is not BountyContract contract || contract.Threat > _options.AcceptUpTo)
        {
            return null;
        }

        List<RosterEntry> party = [.. state.Roster.FitForCampaign.Take(_options.PartySize)];
        if (party.Count != _options.PartySize)
        {
            return null;
        }

        state.AcceptBounty();

        BountyResult result = new Expedition().SendToBounty(
            state,
            contract,
            party,
            new SeededRandom(seed),
            _options.Tuning,
            _options.RetreatPolicy);

        row.Battles++;
        row.Bounties++;
        row.GoldEarned += result.Reward;
        if (result.Claimed)
        {
            row.Victories++;
            row.BountiesClaimed++;
        }

        row.Deaths += result.Aftermath.Dead.Count();
        row.RecoveryDays += result.Aftermath.Warriors.Sum(w => w.RecoveryDays);
        row.ArmorPiecesLost += result.Aftermath.Warriors.Sum(w => w.ShatteredArmor.Count);
        row.WarriorBattles += party.Count;
        row.PowerSum += contract.Target.EffectiveStats.MaxHealth;

        return result.Day;
    }

    /// <summary>Is the offer too heavy for the roster?</summary>
    /// <remarks>
    /// There are two filtering modes. <b>Band</b> mode is a fixed threshold (GDD §10's threat mark);
    /// <b>ratio</b> mode compares the offer with the roster's own strength. Keeping them separate is the
    /// measurement's real question: on a curve that grows as the days pass, a fixed band sooner or later
    /// declines every offer and the dojo goes bankrupt from idleness — whether that is the fault of the
    /// curve or of the fixed policy can only be seen with a policy that adapts.
    /// </remarks>
    private bool Declines(DojoState state, EncounterOffer offer)
    {
        if (_options.AcceptRatio is double ratio)
        {
            double party = state.Roster.FitForCampaign
                .Take(_options.PartySize)
                .Sum(e => Score(e.Warrior.EffectiveStats));
            double enemy = offer.Enemies.Sum(e => Score(e.EffectiveStats));

            return enemy > 0 && party < enemy * ratio;
        }

        if (offer.Threat > _options.AcceptUpTo)
        {
            return true;
        }

        // Entering a heavy offer while the roster is thin thins the roster further.
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

        // The drill policy is deliberately fixed: the weakest stat is worked. What we want to measure is
        // not how well the player chooses but what training <b>itself</b> adds.
        foreach (RosterEntry entry in state.Roster.FitForCampaign)
        {
            entry.Train(TrainingGround.Weakest(entry.Warrior.BaseStats, state.Tuning.Training));
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
    /// Keeps the kit standing: repairs a slot past the threshold, fits a new piece to a broken slot.
    /// </summary>
    /// <remarks>
    /// The order matters — repair first, replacement second. The other way round, a policy short of
    /// money would buy the expensive piece instead of the cheap repair and the price measurement would
    /// measure the policy's mistake.
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
            $"Reserve {++hired}",
            proto.BaseStats,
            proto.Weapon,
            proto.Armor);

        return entry is not null;
    }

    /// <summary>
    /// Buys the candidate from the market who gives the <b>most stat per gold</b>.
    /// </summary>
    /// <remarks>
    /// The policy does not have to be smart here either, it has to be <b>consistent</b>: what we want to
    /// measure is the question "cheap raw candidate or expensive ready-made one" itself, not the
    /// policy's intelligence. Choosing by value naturally probes both ends.
    /// </remarks>
    private static bool HireFromMarket(DojoState state, Warrior proto, int reserve, MarketPick pick)
    {
        int best = -1;
        double bestValue = 0;

        for (int index = 0; index < state.Recruits.Count; index++)
        {
            RecruitOffer offer = state.Recruits[index];
            if (!Affordable(state, offer.Price, reserve) || state.HiredToday.Contains(index))
            {
                continue;
            }

            double value = pick switch
            {
                MarketPick.Best => Score(offer.Stats),
                MarketPick.Talent => offer.Talent,
                _ => Score(offer.Stats) / Math.Max(1, offer.Price),
            };

            if (best < 0 || value > bestValue)
            {
                best = index;
                bestValue = value;
            }
        }

        // The purchase goes through DojoState.HireRecruit: if the measurement does not go through the
        // door the player plays, what it measures is not the game (the same candidate cannot be bought twice).
        return best >= 0 && state.HireRecruit(best, proto.Weapon, proto.Armor) is not null;
    }

    /// <summary>
    /// Buys facilities from the school as long as it can afford them.
    /// </summary>
    /// <remarks>
    /// The policy is simple and <b>the same</b>: the cheapest of the open nodes is bought. When
    /// <see cref="CampaignOptions.SchoolOnly"/> is given, only that branch is bought — that is the
    /// measurement's real question, because whether a branch pays for itself can only be seen when it is
    /// run on its own.
    /// </remarks>
    private int BuildSchool(DojoState state, CampaignRow row)
    {
        int before = state.Resources.Gold;

        // The school's buffer is <b>thicker</b> than the daily buffer: enough to buy a warrior on top of
        // the food money. A facility is optional, the store and replacing a dead warrior are not — with a
        // thin buffer the measurement was measuring the policy going hungry rather than the tree (hungry
        // days per dojo went from 47% to 67%).
        int reserve = Reserve(state) + _options.Economy.RecruitPrice;

        while (true)
        {
            List<SchoolNode> open = [.. state.School.Available()];
            SchoolNode? wanted = open
                .Where(n => _options.SchoolOnly is not SchoolBranch only || n.Branch == only)
                .Where(n => Affordable(state, n.Cost, reserve))
                .OrderBy(n => n.Cost)
                .FirstOrDefault();

            if (wanted is null || !state.BuySchoolNode(wanted.Id))
            {
                break;
            }

            row.SchoolNodes++;
        }

        return before - state.Resources.Gold;
    }

    /// <summary>
    /// Makes an unlocked warrior choose the path he is <b>already strong in</b>.
    /// </summary>
    /// <remarks>
    /// Building on strength is the player's natural move: closing the weak side is training's job, while
    /// the path sharpens who the warrior is.
    /// </remarks>
    private static int ChoosePaths(DojoState state)
    {
        int chosen = 0;
        foreach (RosterEntry entry in state.Roster.Living)
        {
            if (entry.Warrior.Path != WarriorPath.None)
            {
                continue;
            }

            WarriorStats stats = entry.Warrior.BaseStats;
            WarriorPath path = stats.Accuracy >= stats.Defense && stats.Accuracy >= stats.Evasion
                ? WarriorPath.Blade
                : stats.Defense >= stats.Evasion
                    ? WarriorPath.Stone
                    : WarriorPath.Shadow;

            if (state.ChoosePath(entry.Id, path))
            {
                chosen++;
            }
        }

        return chosen;
    }

    /// <summary>The stat score of the roster's best warrior — the market ceiling tracks this too.</summary>
    /// <remarks>
    /// Not the average but the <b>best</b>: what training produces is not the roster's evenly spread
    /// average but the warrior the player invested in. Looking at the average, the recruit hired to
    /// replace a dead veteran would hide training's gain.
    /// </remarks>
    private static double BestScore(DojoState state)
    {
        List<RosterEntry> living = [.. state.Roster.Living];
        return living.Count == 0 ? 0 : living.Max(e => Score(e.Warrior.BaseStats));
    }

    private static double Score(WarriorStats stats) =>
        stats.MaxHealth
        + stats.Strength
        + stats.Accuracy
        + stats.Defense
        + stats.Evasion
        + stats.Speed
        + stats.Aggression;

    /// <summary>Clones the roster from the scenario's roster — with its weapon and kit.</summary>
    private static void Enlist(DojoState state, IReadOnlyList<Warrior> template, int index)
    {
        Warrior proto = template[index % template.Count];
        state.Roster.Recruit($"Warrior {index + 1}", proto.BaseStats, proto.Weapon, proto.Armor);
    }

    /// <summary>
    /// The gold that is not to be spent: a few days' food for the roster.
    /// </summary>
    /// <remarks>
    /// A policy that put its last coin into steel would measure not the economy but the policy's
    /// stupidity: a hungry warrior does not heal, a roster that does not heal cannot go on an expedition,
    /// and the dojo locks up from starvation with repaired armour. A real player does not empty the store either.
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

/// <summary>The life of a single dojo.</summary>
internal sealed class CampaignRow
{
    public int DaysSurvived { get; set; }

    public int Battles { get; set; }

    public int Victories { get; set; }

    public int IdleDays { get; set; }

    /// <summary>The number of offers found too heavy and declined.</summary>
    public int DeclinedOffers { get; set; }

    /// <summary>The number of mishaps suffered.</summary>
    public int Mishaps { get; set; }

    /// <summary>The gold the mishaps took straight out of the treasury.</summary>
    public int MishapGold { get; set; }

    public int HungryDays { get; set; }

    public int Deaths { get; set; }

    public int Hires { get; set; }

    /// <summary>The number of bounty hunts entered.</summary>
    public int Bounties { get; set; }

    /// <summary>The number of contracts whose head was taken.</summary>
    public int BountiesClaimed { get; set; }

    public int RecoveryDays { get; set; }

    public int ArmorPiecesLost { get; set; }

    /// <summary>The number of warrior-fights — the denominator of the death rate.</summary>
    public int WarriorBattles { get; set; }

    /// <summary>The total enemy health met — the steepness of the curve is read from this.</summary>
    public double PowerSum { get; set; }

    public int GoldEarned { get; set; }

    public int GoldSpentOnGear { get; set; }

    public int GoldSpentOnUpkeep { get; set; }

    public int EndingGold { get; set; }

    public int SurvivingWarriors { get; set; }

    /// <summary>The stat score of the roster's best warrior on the first day.</summary>
    public double StartScore { get; set; }

    /// <summary>The same score on the last day — the difference is training's product.</summary>
    public double EndScore { get; set; }

    /// <summary>The living roster's total training days.</summary>
    public int TrainingDays { get; set; }

    /// <summary>The number of school facilities bought.</summary>
    public int SchoolNodes { get; set; }

    /// <summary>The gold that went to the school.</summary>
    public int GoldSpentOnSchool { get; set; }

    /// <summary>The gold that went to warriors hired to replace the dead.</summary>
    /// <remarks>
    /// A separate item: because the economy's binding constraint is the roster (GDD §11), the
    /// replacement cost must not be lost inside kit or store spending. It also enters the net figure —
    /// until it did, "profit per fight" looked positive while the treasury emptied.
    /// </remarks>
    public int GoldSpentOnHires { get; set; }

    /// <summary>The number of warriors who chose a path.</summary>
    public int Paths { get; set; }

    /// <summary>Nobody is left on the roster — the dojo has closed.</summary>
    public bool Collapsed { get; set; }
}

/// <summary>The sum of many dojo lifetimes.</summary>
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

    /// <summary>The share of days an offer was declined (GDD §10: take it or leave it).</summary>
    public double DeclinedShare => Share(r => r.DeclinedOffers);

    /// <summary>The share of days a mishap occurred.</summary>
    public double MishapDayShare => Share(r => r.Mishaps);

    /// <summary>The daily gold the mishaps took directly.</summary>
    public double MishapGoldPerDay => Share(r => r.MishapGold);

    public double HungryDayShare => Share(r => r.HungryDays);

    public double CollapseRate => Campaigns == 0 ? 0 : (double)_rows.Count(r => r.Collapsed) / Campaigns;

    public double AverageDeaths => Average(r => r.Deaths);

    /// <summary>The days the dojo stayed standing — the planned number of days for those that did not close.</summary>
    public double AverageDaysSurvived => Average(r => r.DaysSurvived);

    /// <summary>The day by which half the dojos closed; the planned day if none closed.</summary>
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

    /// <summary>Bounty hunts entered, per dojo.</summary>
    public double AverageBounties => Average(r => r.Bounties);

    /// <summary>How many of the bounty hunts entered had their head taken.</summary>
    public double BountyClaimRate
    {
        get
        {
            double attempts = _rows.Sum(r => r.Bounties);
            return attempts <= 0 ? 0 : _rows.Sum(r => r.BountiesClaimed) / attempts;
        }
    }

    public double AverageEndingGold => Average(r => r.EndingGold);

    /// <summary>The best warrior's stat score in the dojos still standing — training's product.</summary>
    /// <remarks>
    /// Closed dojos are left out: a dojo whose roster is dead has a score of zero and, mixed into the
    /// average, the measurement would be measuring survival rather than training.
    /// </remarks>
    public double AverageBestScore => Standing(r => r.EndScore);

    /// <summary>The points the same score gained from the first day to today.</summary>
    public double AverageScoreGain => Standing(r => r.EndScore - r.StartScore);

    /// <summary>Training days per dojo (the living roster's total).</summary>
    public double AverageTrainingDays => Standing(r => r.TrainingDays);

    /// <summary>The gold spent on replacements, per fight.</summary>
    public double HireGoldPerBattle => PerBattle(r => r.GoldSpentOnHires);

    /// <summary>School facilities bought, per dojo.</summary>
    public double AverageSchoolNodes => Standing(r => r.SchoolNodes);

    /// <summary>The gold that went to the school (per dojo).</summary>
    public double AverageSchoolGold => Standing(r => r.GoldSpentOnSchool);

    /// <summary>Warriors who chose a path (per dojo).</summary>
    public double AveragePaths => Standing(r => r.Paths);

    public double AverageArmorPiecesLost => Average(r => r.ArmorPiecesLost);

    public double RecoveryDaysPerBattle => PerBattle(r => r.RecoveryDays);

    /// <summary>Deaths per warrior-fight — the economy's binding constraint (GDD §11).</summary>
    public double DeathPerWarriorBattle
    {
        get
        {
            int appearances = _rows.Sum(r => r.WarriorBattles);
            return appearances == 0 ? 0 : (double)_rows.Sum(r => r.Deaths) / appearances;
        }
    }

    /// <summary>Enemy health per encounter — the curve's average height.</summary>
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

    /// <summary>Net profit per fight — the economy's one-sentence answer.</summary>
    public double NetGoldPerBattle
    {
        get
        {
            int battles = _rows.Sum(r => r.Battles);
            if (battles == 0)
            {
                return 0;
            }

            int net = _rows.Sum(
                r => r.GoldEarned
                    - r.GoldSpentOnGear
                    - r.GoldSpentOnUpkeep
                    - r.GoldSpentOnSchool
                    - r.GoldSpentOnHires);
            return (double)net / battles;
        }
    }

    /// <summary>The share of dojos whose treasury grew — those that kept their starting capital.</summary>
    public double SolventRate(int startingGold) => Campaigns == 0
        ? 0
        : (double)_rows.Count(r => !r.Collapsed && r.EndingGold >= startingGold) / Campaigns;

    /// <summary>An average over the dojos still standing only.</summary>
    private double Standing(Func<CampaignRow, double> pick)
    {
        List<CampaignRow> standing = [.. _rows.Where(r => !r.Collapsed)];
        return standing.Count == 0 ? 0 : standing.Sum(pick) / standing.Count;
    }

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
