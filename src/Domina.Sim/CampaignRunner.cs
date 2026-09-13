using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Honor;
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
    bool UsePaths = false,
    SchoolTuning? School = null,
    StaffTuning? Staff = null,
    bool UseStaff = false,
    MoraleBand MoraleBand = default,
    SeasonTuning? Season = null,
    bool Hide = false,
    int FinalRest = 0,
    MasteryBand MasteryBand = default,
    bool UseCharms = false,
    ProvinceTuning? Province = null,
    bool MeetRaids = true,
    RetirementPolicy Retirement = RetirementPolicy.None,
    bool UseSmithUpgrades = false,
    StandingTuning? Standing = null)
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
            _options.Market,
            school: _options.School,
            staff: _options.Staff,

            // The season's length is the run's length, whatever the season's own default says: a tick
            // calendar that outlives the measurement would put the last night beyond the last day and
            // the night would never be measured at all.
            season: (_options.Season ?? new SeasonTuning()) with { Days = _options.Days },
            province: _options.Province,
            standing: _options.Standing);
        state.Resources = new Resources(Gold: _options.StartingGold);

        // The map is dealt off the run's own seed, like the game's first day does it: what he already
        // holds is half of the run-to-run variety, and a measurement that opened every season on an
        // empty province would be measuring a start the game never gives.
        state.Province.Deal(new SeededRandom(seed ^ 0x3C3C_C3C3_5A5A_A5A5), 0.5);

        // The roster is cloned from the scenario's own roster: while measuring the economy, stepping
        // outside the roster combat balance was measured on would make the two measurements incomparable.
        IReadOnlyList<Warrior> template = _options.Scenario.Build().PlayerSide;

        // The starting roster cannot exceed the beds: the quarters are the ceiling, and a measurement
        // that started over it would be measuring a dojo the game cannot produce.
        int start = Math.Min(_options.RosterTarget, state.Capacity);
        for (int i = 0; i < start; i++)
        {
            Enlist(state, template, i);
        }

        CampaignRow row = new();
        row.StartScore = BestScore(state);
        int hired = 0;

        for (int day = 0; day < _options.Days; day++)
        {
            row.GoldSpentOnGear += Maintain(state, template);

            // Posts are filled <b>before</b> the next building is ordered: a person is a running cost and
            // a building is a one-off, so a policy that spent every spare coin on walls first would never
            // be able to pay anybody, and the measurement would read as "nobody hires staff".
            if (_options.UseStaff)
            {
                FillPosts(state);
            }

            if (_options.UseSchool)
            {
                row.GoldSpentOnSchool += BuildSchool(state, row);
            }

            row.StaffDays += state.Staff.Hired.Count;
            row.SettlementDays += state.Province.YourHoldings;

            if (_options.UsePaths)
            {
                row.Paths += ChoosePaths(state);
            }

            if (_options.UseCharms)
            {
                row.GoldSpentOnCharms += FitCharms(state);
            }

            if (_options.Retirement != RetirementPolicy.None)
            {
                row.Retirements += RetireVeterans(state);
            }

            int purse = state.Resources.Gold;
            if (Hire(state, template, ref hired))
            {
                row.Hires++;
                row.GoldSpentOnHires += purse - state.Resources.Gold;
            }

            // The hiding dojo is the exploit made into a policy: it never files a fight and lives off
            // the training ground. It is the only policy the missed-week penalty can be measured on —
            // a dojo that fights every week never pays it, so a sweep run against it moves nothing.
            // The last stretch before the night is spent healing. A player plans for a fixed date; a
            // policy that fights up to the last day arrives at the gate with its party in the infirmary,
            // and what that measures is the calendar, not the night.
            bool resting = _options.FinalRest > 0 && _options.Days - day <= _options.FinalRest;

            DayReport closed = MeetTheRaid(state, seed + (ulong)day, row) is DayReport met
                ? met
                : _options.Hide || resting
                ? Rest(state, row, declined: true)
                : _options.UseOffers
                    ? TakeOfferOrRest(state, seed + (ulong)day, row)
                    : FightScenarioOrRest(state, seed + (ulong)day, row);

            if (closed.Event is DayEvent mishap)
            {
                row.Mishaps++;
                row.MishapGold += mishap.Gold;
            }
            if (closed.MissedWeek)
            {
                row.MissedWeeks++;
            }

            if (closed.RivalMove is ProvinceMove move && move.Kind == ProvinceMoveKind.Raid)
            {
                row.Raids++;
            }

            if (closed.Sacked is not null)
            {
                row.Sacks++;
            }

            if (closed.HonorLost > 0)
            {
                row.ChargedWeeks++;
            }

            row.LongestMissedStreak = Math.Max(row.LongestMissedStreak, state.Season.MissedStreak);

            // The day the first man crosses the seppuku threshold is what the honour penalty is really
            // measured by: the number itself says nothing until it says how long hiding can be kept up.
            if (row.DayAtSeppukuRisk == 0
                && state.Roster.Living.Any(e => e.Warrior.Honor < HonorTuning.Default.SeppukuThreshold))
            {
                row.DayAtSeppukuRisk = day + 1;
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

        FightTheNight(state, seed, row);

        row.DaysSurvived = _options.Days;
        row.EndingHonor = state.Roster.Living.Any()
            ? state.Roster.Living.Average(e => e.Warrior.Honor)
            : 0;
        row.HeadsTaken = state.Season.HeadsTaken;
        row.GateOpen = state.Season.GateOpen;
        row.EndingGold = state.Resources.Gold;
        row.BestMastery = state.Roster.Living.Any()
            ? state.Roster.Living.Max(e => e.Warrior.WeaponSkill)
            : 0;
        row.SurvivingWarriors = state.Roster.Living.Count();
        row.EndScore = BestScore(state);
        row.SchoolNodes = state.School.Owned.Count;
        row.Capacity = state.Capacity;
        row.Staff = state.Staff.Hired.Count;
        row.TrainingDays = state.Roster.Living.Sum(e => e.TrainingDays);
        return row;
    }

    /// <summary>
    /// Plays the last night if the season reached it — five bouts, best party first, no day in between.
    /// </summary>
    /// <remarks>
    /// The policy is deliberately the simplest one that is not stupid: <b>the strongest men still on
    /// their feet</b>, up to the party limit, bout after bout. It is not how a player would play the
    /// night — he would hold a fresh man back for Kurogane — but it is the same policy every time,
    /// which is what makes two sets of bout numbers comparable. What it measures is whether the night
    /// is survivable by a season's worth of roster at all.
    /// </remarks>
    private void FightTheNight(DojoState state, ulong seed, CampaignRow row)
    {
        if (state.Season.Phase != SeasonPhase.FinalNight)
        {
            return;
        }

        row.ReachedTheNight = true;
        FinalNight night = new();

        while (state.Season.Phase == SeasonPhase.FinalNight)
        {
            int round = state.Season.FinalRound;
            List<RosterEntry> party =
            [
                .. state.Roster.Living
                    .Where(FinalNight.CanAnswerTheBell(state))
                    .OrderByDescending(e => Score(e.Warrior.EffectiveStats))
                    .Take(EncounterOffer.MaxPartySize),
            ];

            if (party.Count == 0)
            {
                // Nobody can stand up: the night is over without a bout being fought.
                row.NightPartySize += 0;
                break;
            }

            row.NightPartySize += party.Count;

            FinalRoundResult result = night.Fight(
                state,
                party,
                new SeededRandom(seed + 7_777_777 + (ulong)round),
                _options.Tuning,

                // No retreat policy on the night: withdrawing from a bout <b>is</b> losing it, and the
                // run ends with it. A policy that pulls the party out at 70% health was throwing the
                // whole night away in the first bout — no player would, so neither does the measurement.
                retreat: null);

            row.NightRoundsFought++;
            row.NightDeaths += result.Aftermath.Dead.Count();

            if (result.Won)
            {
                row.NightRoundsWon++;
            }
            else
            {
                row.NightLostOnRound = round;
            }
        }

        row.Triumph = state.Season.Phase == SeasonPhase.Triumph;
    }

    /// <summary>
    /// Meets him in the yard when he is at the gate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It runs <b>before</b> everything else the day would do, hiding included: a raid is not an offer
    /// that can be declined, and a player who is sitting out the season still picks up a sword when the
    /// gate goes. Everyone fit goes out, up to the party ceiling — the dojo is defending its own ground,
    /// so nobody is kept back.
    /// </para>
    /// <para>
    /// Switchable, because the question needs both halves: what a raid costs when it is ignored is the
    /// sack, and what it costs when it is met can only be read off a policy that meets it.
    /// </para>
    /// </remarks>
    private DayReport? MeetTheRaid(DojoState state, ulong seed, CampaignRow row)
    {
        if (!_options.MeetRaids || !state.UnderRaid)
        {
            return null;
        }

        List<RosterEntry> party = [.. state.Roster.FitForCampaign.Take(EncounterOffer.MaxPartySize)];
        if (party.Count == 0)
        {
            return null;
        }

        EncounterOffer raid = state.Offer;
        if (Expedition.Refuse(state, raid, party) is not null)
        {
            return null;
        }

        ExpeditionResult result = new Expedition().Send(
            state,
            raid,
            party,

            // The raid is fought out: there is no pulling back from a fight in your own yard, and a
            // policy that withdrew would be measuring the key rather than the raid.
            new SeededRandom(seed),
            _options.Tuning,
            retreat: null);

        row.Battles++;
        row.RaidsMet++;
        row.GoldEarned += result.Reward;
        if (result.Battle.Outcome == BattleOutcome.PlayerVictory)
        {
            row.Victories++;
            row.RaidsWon++;
        }

        row.Deaths += result.Aftermath.Dead.Count();
        row.RecoveryDays += result.Aftermath.Warriors.Sum(w => w.RecoveryDays);
        row.ArmorPiecesLost += result.Aftermath.Warriors.Sum(w => w.ShatteredArmor.Count);
        row.WarriorBattles += party.Count;
        row.PowerSum += raid.EnemyHealth;

        return result.Day;
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
    /// <summary>
    /// Puts the dojo's own forge to work: full plate where the gate is open, and a reforged blade.
    /// </summary>
    /// <remarks>
    /// Greedy, like the rest of the policy — upgrade whatever is affordable the day it becomes so.
    /// What is being measured is whether the branch pays for itself at all, which is the question it
    /// has failed twice (GDD §10, "the forge pays for nothing").
    /// </remarks>
    private static void Upgrade(DojoState state, Warrior warrior, int reserve)
    {
        foreach ((HitLocation slot, ArmorPiece plate) in ((HitLocation, ArmorPiece)[])
        [
            (HitLocation.Torso, ArmorPiece.OYoroiCuirass),
            (HitLocation.Head, ArmorPiece.Kabuto),
            (HitLocation.SwordArm, ArmorPiece.HeavyKote),
            (HitLocation.OffArm, ArmorPiece.HeavyKote),
            (HitLocation.RightLeg, ArmorPiece.HeavySuneate),
            (HitLocation.LeftLeg, ArmorPiece.HeavySuneate),
        ])
        {
            if (warrior.Armor.At(slot) != plate
                && Affordable(state, state.Quartermaster.PiecePrice(plate), reserve))
            {
                state.Quartermaster.Equip(state, warrior, slot, plate);
            }
        }

        if (Affordable(state, state.Quartermaster.ForgePrice(warrior), reserve))
        {
            state.Quartermaster.Forge(state, warrior);
        }
    }

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

            // The equipment branch's two upper tiers only show up in a measurement if the policy uses
            // them: the template kit is what the scenario carries, and nothing in it is ō-yoroi.
            if (_options.UseSmithUpgrades)
            {
                Upgrade(state, warrior, reserve);
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
            return HireFromMarket(state, proto, reserve, _options.Pick, Band);
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

        if (entry is not null)
        {
            entry.Warrior.MoraleBand = Band;
        }

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
    private static bool HireFromMarket(
        DojoState state,
        Warrior proto,
        int reserve,
        MarketPick pick,
        MoraleBand band)
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
        if (best < 0)
        {
            return false;
        }

        RosterEntry? bought = state.HireRecruit(best, proto.Weapon, proto.Armor);
        if (bought is not null)
        {
            bought.Warrior.MoraleBand = band;
        }

        return bought is not null;
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
    /// Fills every open post the payroll can carry, and lets people go when it cannot.
    /// </summary>
    /// <remarks>
    /// The policy is deliberately blunt — hire whatever building stands, and dismiss when the treasury
    /// falls under the reserve. What is being measured is not a clever payroll but whether a wage-bearing
    /// staff economy is survivable at all, and a policy that hired selectively would answer a question
    /// about the policy rather than about the numbers. Posts with no effect in the code yet are skipped:
    /// paying for them would be measuring a wage against nothing.
    /// </remarks>
    private void FillPosts(DojoState state)
    {
        int reserve = Reserve(state) + _options.Economy.RecruitPrice;

        if (state.Resources.Gold < reserve)
        {
            foreach (StaffRole role in state.Staff.Hired.ToList())
            {
                // A master of the house is not let go on a bad day: he costs nothing, and cutting him
                // would be cutting the one thing the long game pays out.
                if (!state.Staff.HeldByMaster(role))
                {
                    state.Dismiss(role);
                }
            }

            return;
        }

        // A post is only taken when the purse could carry it for a stretch: hiring on the exact day the
        // treasury crosses the line would mean hiring and firing the same person every other day, and
        // what that measures is the policy's twitch, not the wage.
        const int WageRunwayDays = 10;

        foreach (SchoolNode node in SchoolTree.All)
        {
            if (node.Role is not StaffRole role
                || Facilities.IsInert(role)
                || state.Staff.Has(role)
                || !state.School.Has(node.Id))
            {
                continue;
            }

            int runway = state.StaffTuning.WageOf(role) * WageRunwayDays;
            if (state.Resources.Gold < reserve + runway)
            {
                continue;
            }

            state.Hire(role);
        }
    }

    /// <summary>
    /// Retires the men who have earned it, and puts them in whatever post they may hold.
    /// </summary>
    /// <remarks>
    /// Deliberately blunt again, and deliberately <b>greedy</b>: the moment a man is eligible he comes
    /// off the field. That is the policy the rule has to survive — if retiring the best sword the day
    /// it qualifies were the winning move, the gate would be in the wrong place.
    /// </remarks>
    private int RetireVeterans(DojoState state)
    {
        int retired = 0;

        foreach (RosterEntry entry in state.Roster.Living.ToList())
        {
            // A dojo that retires its way down to nobody has no season left; the roster comes first.
            if (state.Roster.Living.Count() <= _options.PartySize)
            {
                break;
            }

            // The maimed-only policy is the one a player would actually run: a man the field has
            // already taken an arm from is worth more in a post than in a party, while retiring a whole
            // veteran is giving away the best sword in the dojo.
            if (_options.Retirement == RetirementPolicy.Maimed
                && entry.Warrior.Disabilities.Count == 0)
            {
                continue;
            }

            if (!state.CanRetire(entry) || !state.Retire(entry.Id))
            {
                continue;
            }

            retired++;
            Appoint(state, entry);
        }

        return retired;
    }

    /// <summary>Puts a new master into the best post standing empty that he may hold.</summary>
    private static void Appoint(DojoState state, RosterEntry master)
    {
        foreach (SchoolNode node in SchoolTree.All)
        {
            if (node.Role is not StaffRole role
                || state.Staff.Has(role)
                || !state.School.Has(node.Id)
                || !state.StaffTuning.MasterMayHold(role))
            {
                continue;
            }

            if (state.Appoint(master.Id, role))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Buys charms from the temple and hangs them on whoever has a slot free.
    /// </summary>
    /// <remarks>
    /// A blunt policy on purpose, like <see cref="FillPosts"/>: fill every open slot with the first
    /// charm in the catalogue that is affordable. The question the measurement asks is whether the
    /// shrine branch pays for itself at all, and a policy that matched a charm to a warrior's weakest
    /// stat would be answering a question about the policy instead.
    /// </remarks>
    private int FitCharms(DojoState state)
    {
        if (state.OmamoriSlots <= 0)
        {
            return 0;
        }

        int before = state.Resources.Gold;
        int reserve = Reserve(state) + _options.Economy.RecruitPrice;

        foreach (RosterEntry entry in state.Roster.Living)
        {
            while (entry.Warrior.Charms.Count < state.OmamoriSlots)
            {
                OmamoriKind? held = state.CharmStore.FirstOrDefault(pair => pair.Value > 0).Key;
                if (state.CharmStore.Count == 0)
                {
                    OmamoriCharm wanted = Omamori.All[0];
                    if (!Affordable(state, state.PriceOf(wanted.Kind), reserve) || !state.BuyCharm(wanted.Kind))
                    {
                        return before - state.Resources.Gold;
                    }

                    held = wanted.Kind;
                }

                if (held is not OmamoriKind kind || !state.FitCharm(entry.Id, kind))
                {
                    return before - state.Resources.Gold;
                }
            }
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
    /// <summary>The band the run measures with — an unset record field is zeroes, not the design's band.</summary>
    private MoraleBand Band =>
        _options.MoraleBand == default ? MoraleBand.Default : _options.MoraleBand;

    /// <inheritdoc cref="Band"/>
    private MasteryBand Mastery =>
        _options.MasteryBand == default ? MasteryBand.Default : _options.MasteryBand;

    private void Enlist(DojoState state, IReadOnlyList<Warrior> template, int index)
    {
        Warrior proto = template[index % template.Count];
        RosterEntry entry =
            state.Roster.Recruit($"Warrior {index + 1}", proto.BaseStats, proto.Weapon, proto.Armor);
        entry.Warrior.MoraleBand = Band;
        entry.Warrior.MasteryBand = Mastery;
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

/// <summary>Which men the policy takes off the field.</summary>
internal enum RetirementPolicy
{
    /// <summary>Nobody retires — the control.</summary>
    None,

    /// <summary>Everyone the gate lets through, the day it lets him through.</summary>
    Everyone,

    /// <summary>Only the men the field has already maimed.</summary>
    Maimed,
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

    /// <summary>Posts filled at the end of the season.</summary>
    public int Staff { get; set; }

    /// <summary>Post-days worked over the season — the honest measure of how full the payroll was.</summary>
    public int StaffDays { get; set; }

    /// <summary>The gold that went to the school.</summary>
    public int GoldSpentOnSchool { get; set; }

    /// <summary>The men who left the field for good.</summary>
    public int Retirements { get; set; }

    /// <summary>The best mastery on the roster when the season ended (0-1).</summary>
    /// <remarks>
    /// Without it a sweep of the mastery rates reads as "nothing moved" and cannot tell apart the two
    /// reasons for that: the building never bought, or the bonus too small to matter.
    /// </remarks>
    public double BestMastery { get; set; }

    /// <summary>Settlement-days held over the season — what the map was actually worth to the dojo.</summary>
    /// <remarks>
    /// Counted per day rather than at the end, for the same reason post-days are: a map won in the last
    /// week and a map held all season pay completely differently, and an end-of-season count cannot
    /// tell the two apart.
    /// </remarks>
    public int SettlementDays { get; set; }

    /// <summary>The raids he brought to the gate.</summary>
    public int Raids { get; set; }

    /// <summary>The raids the dojo went out and met.</summary>
    public int RaidsMet { get; set; }

    /// <summary>The raids it won.</summary>
    public int RaidsWon { get; set; }

    /// <summary>The raids nobody answered — the store emptied and the name lost.</summary>
    public int Sacks { get; set; }

    /// <summary>The gold that went to the temple's charms.</summary>
    /// <remarks>
    /// Kept apart from the school's gold although both are optional: a building is bought once and a
    /// charm can be sold back, so lumping them together would hide the one spending line in this
    /// economy that is partly reversible.
    /// </remarks>
    public int GoldSpentOnCharms { get; set; }

    /// <summary>The gold that went to warriors hired to replace the dead.</summary>
    /// <remarks>
    /// A separate item: because the economy's binding constraint is the roster (GDD §11), the
    /// replacement cost must not be lost inside kit or store spending. It also enters the net figure —
    /// until it did, "profit per fight" looked positive while the treasury emptied.
    /// </remarks>
    public int GoldSpentOnHires { get; set; }

    /// <summary>The number of warriors who chose a path.</summary>
    public int Paths { get; set; }

    /// <summary>The weeks that closed with no fight filed.</summary>
    public int MissedWeeks { get; set; }

    /// <summary>The season ended with the gate open and the last night was played.</summary>
    public bool ReachedTheNight { get; set; }

    /// <summary>The bouts of the last night that were actually fought.</summary>
    public int NightRoundsFought { get; set; }

    /// <summary>The bouts won.</summary>
    public int NightRoundsWon { get; set; }

    /// <summary>The bout the night was lost on; <c>0</c> if it was not lost on the field.</summary>
    public int NightLostOnRound { get; set; }

    /// <summary>The men sent out across the whole night — the depth the night actually asked for.</summary>
    public int NightPartySize { get; set; }

    /// <summary>The warriors the last night buried.</summary>
    public int NightDeaths { get; set; }

    /// <summary>All five bouts won.</summary>
    public bool Triumph { get; set; }

    /// <summary>The missed weeks that were actually paid for — the free first week is not among them.</summary>
    public int ChargedWeeks { get; set; }

    /// <summary>The longest run of weeks missed back to back.</summary>
    public int LongestMissedStreak { get; set; }

    /// <summary>The first day a living warrior stood below the seppuku threshold; <c>0</c> if none did.</summary>
    public int DayAtSeppukuRisk { get; set; }

    /// <summary>The living roster's average honour on the last day.</summary>
    public double EndingHonor { get; set; }

    /// <summary>The beds the dojo ended the season with.</summary>
    public int Capacity { get; set; }

    /// <summary>The heads brought in — the last night's gate counts these.</summary>
    public int HeadsTaken { get; set; }

    /// <summary>Were the three heads in by the end of the season?</summary>
    public bool GateOpen { get; set; }

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

    /// <summary>Weeks with no fight filed, per dojo.</summary>
    public double AverageMissedWeeks => Average(r => r.MissedWeeks);

    /// <summary>Settlements held, averaged over every day of the season.</summary>
    public double AverageSettlements => Campaigns == 0
        ? 0
        : _rows.Sum(r => (double)r.SettlementDays / Math.Max(1, r.DaysSurvived)) / Campaigns;

    /// <summary>Raids per dojo.</summary>
    public double AverageRaids => Average(r => r.Raids);

    /// <summary>Raids nobody answered, per dojo.</summary>
    public double AverageSacks => Average(r => r.Sacks);

    /// <summary>Retirements per dojo.</summary>
    public double AverageRetirements => Average(r => r.Retirements);

    /// <summary>The best mastery reached, averaged over the dojos.</summary>
    public double AverageBestMastery =>
        Campaigns == 0 ? 0 : _rows.Sum(r => r.BestMastery) / Campaigns;

    /// <summary>Raids met in the yard, per dojo.</summary>
    public double AverageRaidsMet => Average(r => r.RaidsMet);

    /// <summary>The share of met raids that were won.</summary>
    public double RaidWinRate
    {
        get
        {
            int met = _rows.Sum(r => r.RaidsMet);
            return met == 0 ? 0 : (double)_rows.Sum(r => r.RaidsWon) / met;
        }
    }

    /// <summary>The missed weeks that were paid for, per dojo.</summary>
    public double AverageChargedWeeks => Average(r => r.ChargedWeeks);

    /// <summary>The longest run of missed weeks, per dojo.</summary>
    public double AverageLongestMissedStreak => Average(r => r.LongestMissedStreak);

    /// <summary>The living roster's average honour on the last day, in the dojos still standing.</summary>
    public double AverageEndingHonor => Standing(r => r.EndingHonor);

    /// <summary>The share of dojos in which somebody crossed the seppuku threshold.</summary>
    public double SeppukuRiskRate =>
        Campaigns == 0 ? 0 : (double)_rows.Count(r => r.DayAtSeppukuRisk > 0) / Campaigns;

    /// <summary>The day the threshold was first crossed, among the dojos where it was.</summary>
    public double AverageDayAtSeppukuRisk
    {
        get
        {
            List<int> days = [.. _rows.Where(r => r.DayAtSeppukuRisk > 0).Select(r => r.DayAtSeppukuRisk)];
            return days.Count == 0 ? 0 : days.Average();
        }
    }

    /// <summary>The share of dojos that played the last night.</summary>
    public double NightRate => Campaigns == 0 ? 0 : (double)_rows.Count(r => r.ReachedTheNight) / Campaigns;

    /// <summary>The share of dojos that won all five bouts.</summary>
    public double TriumphRate => Campaigns == 0 ? 0 : (double)_rows.Count(r => r.Triumph) / Campaigns;

    /// <summary>Bouts won, among the dojos that played the night.</summary>
    public double AverageNightRoundsWon => AtTheNight(r => r.NightRoundsWon);

    /// <summary>Men sent out across the night, among the dojos that played it.</summary>
    public double AverageNightPartySize => AtTheNight(r => r.NightPartySize);

    /// <summary>The warriors the night buried, among the dojos that played it.</summary>
    public double AverageNightDeaths => AtTheNight(r => r.NightDeaths);

    /// <summary>How often each bout was survived, among the dojos that reached that bout.</summary>
    public double RoundSurvivalRate(int round)
    {
        List<CampaignRow> reached =
            [.. _rows.Where(r => r.ReachedTheNight && r.NightRoundsWon >= round - 1
                                 && (r.NightLostOnRound == 0 || r.NightLostOnRound >= round))];

        if (reached.Count == 0)
        {
            return 0;
        }

        return (double)reached.Count(r => r.NightRoundsWon >= round) / reached.Count;
    }

    private double AtTheNight(Func<CampaignRow, double> pick)
    {
        List<CampaignRow> night = [.. _rows.Where(r => r.ReachedTheNight)];
        return night.Count == 0 ? 0 : night.Average(pick);
    }

    /// <summary>The roster ceiling reached, per surviving dojo.</summary>
    public double AverageCapacity => Standing(r => r.Capacity);

    /// <summary>Heads brought in, per dojo.</summary>
    public double AverageHeads => Average(r => r.HeadsTaken);

    /// <summary>The share of dojos that reached the last night's gate.</summary>
    public double GateRate => Campaigns == 0 ? 0 : (double)_rows.Count(r => r.GateOpen) / Campaigns;

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

    /// <summary>Posts filled at the end of the season, per dojo.</summary>
    public double AverageStaff => Standing(r => r.Staff);

    /// <summary>Post-days worked per dojo — a post filled all season counts as the season's length.</summary>
    public double AverageStaffDays => Standing(r => r.StaffDays);

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
                    - r.GoldSpentOnCharms
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
