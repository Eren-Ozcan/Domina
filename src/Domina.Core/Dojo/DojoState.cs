using Domina.Core.Campaign;
using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>The dojo's whole persistent state — this is what the save file is about.</summary>
/// <remarks>
/// <para>
/// It contains nothing engine-dependent and is <b>deterministic</b>: the same seed and the same day
/// give the same result. The two items that need randomness (the encounter offer and the day's event)
/// are pure functions of the day and <see cref="Seed"/> — no stream state is carried, neither is
/// written to the save, and neither can be changed by reloading the save.
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
        SchoolTuning? school = null,
        StaffTuning? staff = null,
        SeasonTuning? season = null,
        Honor.HonorTuning? honor = null,
        DifficultyTier difficulty = DifficultyTier.Master)
    {
        _baseTuning = tuning ?? new DojoTuning();
        _baseEconomy = economy ?? new EconomyTuning();
        School = new School(school);
        StaffTuning = staff ?? new StaffTuning();
        Encounters = new EncounterGenerator(encounters);
        Events = new DayEventTable(events);
        Market = new RecruitMarket(market);
        Bounties = new BountyBoard(bounties, encounters);
        Season = new Season(season);
        Tribunal = new Tribunal(honor);
        Difficulty = difficulty;
        Seed = seed;
        Tuning = _baseTuning;
        Quartermaster = new Quartermaster(_baseEconomy);
        ApplySchool();
    }

    /// <summary>
    /// The day loop's settings — <b>with the school applied</b>.
    /// </summary>
    /// <remarks>
    /// Every read goes through here; the raw settings are not handed out. Otherwise one place would
    /// read the number with the facilities and another without, and the bonus would quietly half-work.
    /// </remarks>
    public DojoTuning Tuning { get; private set; }

    /// <summary>Prices and shopping. The economy numbers are read from here.</summary>
    public Quartermaster Quartermaster { get; private set; }

    /// <summary>The dojo's facilities — the investment that does not die (GDD §10).</summary>
    public School School { get; }

    /// <summary>Who is on the payroll. A building is bought once; a person is paid every day.</summary>
    public Staff Staff { get; } = new();

    /// <summary>The wages and the numbers only a person produces.</summary>
    public StaffTuning StaffTuning { get; }

    public EconomyTuning Economy => Quartermaster.Economy;

    /// <summary>The wheel that produces the day's offer.</summary>
    public EncounterGenerator Encounters { get; }

    /// <summary>The table that draws the day's mishap (GDD §11: random events).</summary>
    public DayEventTable Events { get; }

    /// <summary>The warrior market.</summary>
    public RecruitMarket Market { get; }

    /// <summary>The board the bounty contracts are posted on.</summary>
    public BountyBoard Bounties { get; }

    /// <summary>The season's clock and its books — the countdown, the weekly tick and the gate.</summary>
    public Season Season { get; }

    /// <summary>The tribunal a dishonoured warrior is called before (docs/GDD.md §6).</summary>
    public Tribunal Tribunal { get; }

    /// <summary>
    /// The tier this run is played at (docs/GDD.md §10).
    /// </summary>
    /// <remarks>
    /// The tier itself is <b>the player's decision</b>, so it goes into the save the way the seed does;
    /// what it multiplies stays in the code, so a patch that retunes the balance reaches an old save.
    /// The numbers are already folded into this dojo's tuning — this field is what the screen reads and
    /// what the save writes down.
    /// </remarks>
    public DifficultyTier Difficulty { get; private set; }

    /// <summary>The day an accepted contract was posted, if there is one; otherwise <c>null</c>.</summary>
    /// <remarks>
    /// The contract itself is not stored — it is recomputed from the day and the seed. The only thing
    /// that must be stored is <b>whether a promise was given</b>, and that is a single number.
    /// </remarks>
    public int? AcceptedBountyDay { get; private set; }

    /// <summary>The day the contract whose head was taken was posted; otherwise <c>null</c>.</summary>
    /// <remarks>
    /// Because the board is pure, a contract is regenerated every day until it expires. Without this
    /// record the same target would appear posted again the next day and the same head would be sold
    /// twice — measured, 12.7 bounties per dojo in 60 days.
    /// </remarks>
    public int? ClaimedBountyDay { get; private set; }

    /// <summary>
    /// The expedition's seed. It lives in the save; offers are recomputed from it and the day.
    /// </summary>
    public ulong Seed { get; private set; }

    public Roster Roster { get; } = new();

    public Resources Resources { get; set; }

    /// <summary>Which day it is. The game starts on day 1.</summary>
    public int Day { get; private set; } = 1;

    /// <summary>
    /// Closes a day and moves to the next.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Entering an encounter also takes <b>a full day</b> (GDD §10): the expedition layer makes this
    /// call when the fight ends, and a day spent in the dojo makes the same call. It is not called
    /// twice per day — this is the item that closes the "enter, look, run" loop.
    /// </para>
    /// <para>
    /// The dead are not touched: both honour and the infirmary work for the living.
    /// </para>
    /// </remarks>
    public DayReport AdvanceDay()
    {
        // The event is processed <b>before</b> upkeep: spoiled provisions should make that day's
        // shopping more expensive, stolen gold should strain the bill due that day. Processed after,
        // the mishap would be postponed to the next day and the pressure on the buffer would arrive a day late.
        DayEvent? happening = Events.Roll(this, Day);
        ApplyEvent(happening);

        UpkeepReport upkeep = PayUpkeep(happening);

        // The site burns its day whatever else happens: building time is the one cost in this economy
        // that gold cannot touch (GDD §10).
        IReadOnlyList<SchoolNodeId> opened = School.AdvanceConstruction();
        if (opened.Count > 0)
        {
            ApplySchool();
        }

        List<WarriorId> recovered = [];
        List<WarriorId> trained = [];

        foreach (RosterEntry entry in Roster.Living)
        {
            bool fed = !upkeep.Hungry.Contains(entry.Id);

            if (entry.Activity == DojoActivity.Training && fed)
            {
                entry.TrainingDays++;

                // Training writes the <b>raw</b> stat, not the effective one: a disability's multiplier
                // is permanent and is not taken back by training (GDD §7). A warrior who loses an arm
                // recovers by working, but he does not get the arm back.
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
            SettleMorale(entry, fed);
        }

        // The promise is weighed at the end of the day: if the last day too closed without a fight, the contract is broken.
        bool broken = BreakBountyIfExpired();

        // The tribunal sits after the day's honour has moved and before the season is weighed: a verdict
        // can empty the roster, and the season has to close on the same day if it does.
        TribunalVerdict? verdict = HoldTribunal();

        // The season's tick is weighed last, after the day's own books: a warrior who died today does not
        // pay for the week he did not see, and a roster emptied today closes the dojo on the same day.
        Season.RecordStanding(Roster.FitForCampaign.Any());
        SeasonDayClose season = Season.Close(Day, Roster.Living.Any());
        if (season.HonorPenalty > 0)
        {
            // The penalty is written to the <b>whole</b> roster, like a broken promise: the week with no
            // fight filed is the dojo's week, not one warrior's. Written to a single man, the player would
            // dump it on someone he had already written off and hiding would cost nothing.
            foreach (RosterEntry entry in Roster.Living)
            {
                entry.Warrior.Honor = HonorScale.Clamp(entry.Warrior.Honor - season.HonorPenalty);
            }
        }

        int closed = Day;
        Day++;
        _offer = null;
        _recruits = null;
        _hiredToday.Clear();
        _bounty = null;
        _bountyRead = false;
        return new DayReport(
            closed,
            recovered,
            trained,
            upkeep,
            happening,
            broken,
            opened,
            season.MissedWeek,
            season.Phase,
            season.HonorPenalty,
            verdict);
    }

    /// <summary>
    /// Closes the day's food/water/medicine bill: what is missing is bought from the market, the rest
    /// is eaten from the store.
    /// </summary>
    /// <remarks>
    /// If the store is not enough, <b>those in the infirmary are fed first</b>. The order cannot be
    /// arbitrary: a warrior left hungry neither heals nor trains that day, and leaving the wounded
    /// hungry would turn scarcity into a punishment with no way back. The price of scarcity is
    /// <b>time</b>, not death.
    /// </para>
    /// <para>
    /// The treasury does not go negative: an item that cannot be paid for is not bought, it is reported as missing.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Grows the per-head need by the event's multiplier.
    /// </summary>
    /// <remarks>
    /// It is rounded up: there is no such thing as half a measure of rice, and the mishap's price must
    /// not be lost in the rounding.
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

        // The cook does not produce, he cuts consumption — and he cuts it off the <b>kitchen's</b> total,
        // not off each man's bowl. Per head the saving would vanish in the rounding (one measure of rice
        // ×0.75 is still one measure), which is the same trap the steward's price floor guards against.
        double cookFactor = School.Has(SchoolNodeId.Kitchen)
            ? 1 - ((1 - StaffTuning.CookFoodFactor)
                   * School.Efficiency(SchoolNodeId.Kitchen, Staff, StaffTuning))
            : 1;
        int waterPer = Scaled(Economy.WaterPerWarriorPerDay, happening?.WaterFactor ?? 1);
        bool medicineWorks = happening?.MedicineWorks ?? true;

        Resources need = new(
            Gold: 0,
            Food: (int)Math.Ceiling(living.Count * foodPer * cookFactor),
            Water: living.Count * waterPer,
            Medicine: medicineWorks ? wounded * Economy.MedicinePerInfirmaryDay : 0);

        int spent = Quartermaster.Restock(this, need);

        // Wages are paid before the stores are eaten but after the shopping: the payroll is the day's
        // first bill and the one the player can cut. A dojo that cannot meet it loses the people, never
        // the buildings — that is the gearbox GDD §10 asks for.
        int payroll = Staff.DailyWage(StaffTuning);
        int paid = Math.Min(payroll, Resources.Gold);
        List<StaffRole> left = [];
        if (payroll > 0)
        {
            Resources = Resources with { Gold = Resources.Gold - paid };
            if (paid < payroll)
            {
                left = [.. Staff.Hired];
                foreach (StaffRole role in left)
                {
                    Staff.Remove(role);
                }

                ApplySchool();
            }
        }

        int food = Math.Min(Resources.Food, need.Food);
        int water = Math.Min(Resources.Water, need.Water);
        int medicine = Math.Min(Resources.Medicine, need.Medicine);

        Resources = Resources with
        {
            Food = Resources.Food - food,
            Water = Resources.Water - water,
            Medicine = Resources.Medicine - medicine,
        };

        // A fed mouth is measured against the ration the kitchen actually served: with a cook the day's
        // whole need is smaller, so the same store feeds the same men.
        double servedPer = Math.Max(foodPer * cookFactor, 0);
        int fedMouths = servedPer <= 0 ? living.Count : (int)Math.Floor(food / servedPer);
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

        return new UpkeepReport(spent, food, water, medicine, hungry, medicated, paid, left);
    }

    /// <summary>
    /// Today's encounter offer (GDD §10: one offer a day, take it or leave it).
    /// </summary>
    /// <remarks>
    /// Every read returns the same offer and nothing is stored: the generation is a pure function of
    /// the day and the seed. That is also why loading the save to change an offer you did not like
    /// does not work.
    /// </remarks>
    public EncounterOffer Offer => _offer ??= Encounters.Offer(Day, Seed);

    /// <summary>
    /// The candidates standing in the market today.
    /// </summary>
    /// <remarks>
    /// It is <b>frozen</b> within the day. Because the market's level tracks the roster's average,
    /// without freezing, buying one candidate would instantly change the remaining ones: the player
    /// would take a cheap one and reroll the list to get the candidate he wanted.
    /// </remarks>
    public IReadOnlyList<RecruitOffer> Recruits => _recruits ??= Market.Stock(
        Day,
        Seed,
        Market.AnchorFor(Roster),
        Economy.RecruitPrice,
        BrokerReach(StaffTuning.BrokerExtraCandidates),
        Market.Tuning.ClassedChance
            + (BrokerReach(StaffTuning.BrokerExtraCandidates) > 0
                ? Market.Tuning.ClassedChance * School.Efficiency(SchoolNodeId.Broker, Staff, StaffTuning)
                : 0));

    /// <summary>
    /// What the broker adds to the stall — candidates, never prices (GDD §10).
    /// </summary>
    /// <remarks>
    /// He is deliberately kept off the price side: the steward owns the expense column, and a broker
    /// who also discounted would make one building answer two questions. What he changes is <b>what is
    /// standing there</b> — a longer list, and a better chance of a man who already carries a class.
    /// </remarks>
    private int BrokerReach(int full) =>
        (int)Math.Round(full * School.Efficiency(SchoolNodeId.Broker, Staff, StaffTuning));

    /// <summary>The indices of the candidates bought from the stall today.</summary>
    /// <remarks>
    /// It empties when the day closes — tomorrow other candidates stand at the stall
    /// (<see cref="MarketTuning.RefreshDays"/>) and the old mark would block the wrong man.
    /// </remarks>
    public IReadOnlyCollection<int> HiredToday => _hiredToday;

    /// <summary>
    /// Buys the candidate at the stall.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Buying does not eat the day:</b> the market is open all day, and as long as the treasury and
    /// the stall allow it more than one warrior can be bought. What eats the day is going on an
    /// expedition or spending the day in the dojo — with a separate cap on the number of purchases, it
    /// would be impossible to replace two dead warriors on the same day.
    /// </para>
    /// <para>
    /// A bought candidate <b>goes on the record</b> and comes off the stall. Because the stall is frozen
    /// within the day (<see cref="Recruits"/>), without this record the same candidate could be sold
    /// endlessly: one person would turn into the whole roster. It cannot be left to the screen's mark —
    /// reloading the save and buying the same man again is the same door.
    /// </para>
    /// </remarks>
    /// <param name="index">The candidate's index within <see cref="Recruits"/>.</param>
    /// <param name="weapon">The weapon to equip; the default if not given.</param>
    /// <param name="armor">The armour to equip; the default if not given.</param>
    /// <returns>The roster entry if he was hired; <c>null</c> if he is not at the stall, was already bought, or there is not enough gold.</returns>
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

    /// <summary>The contract posted on the board today; <c>null</c> if there is none.</summary>
    /// <remarks>
    /// Like the offer it is fixed within the day and not stored: the same day and the same seed always
    /// give the same contract. A contract <b>does not replace the daily offer</b>, it stands beside it —
    /// the day still takes one job, and which job you do is the decision.
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

    /// <summary>Accepts today's contract — a promise is given.</summary>
    /// <remarks>
    /// Accepting does <b>not</b> eat the day: a contract can be accepted and another job done the same
    /// day. What it does eat is time — if you do not come back by the last day the roster loses honour
    /// (<see cref="BountyContract.BrokenHonorPenalty"/>).
    /// </remarks>
    /// <returns>The contract if it could be accepted, <c>null</c> if not.</returns>
    public BountyContract? AcceptBounty()
    {
        if (Bounty is not BountyContract open || AcceptedBountyDay is not null)
        {
            return null;
        }

        AcceptedBountyDay = open.PostedDay;
        return open;
    }

    /// <summary>The head was taken: the promise closes and the contract comes off the board.</summary>
    internal void CloseBounty(int postedDay)
    {
        Season.RecordHead();
        AcceptedBountyDay = null;
        ClaimedBountyDay = postedDay;
        _bounty = null;
        _bountyRead = false;
    }

    /// <summary>Restores the promise coming from the save.</summary>
    /// <remarks>
    /// The contract itself is not written to the save, it is recomputed from the day and the seed; the
    /// only thing written is whether a promise was given. Otherwise the player could escape his promise
    /// by reloading the save.
    /// </remarks>
    internal void RestoreBounty(int? acceptedDay, int? claimedDay)
    {
        AcceptedBountyDay = acceptedDay;
        ClaimedBountyDay = claimedDay;
        _bounty = null;
        _bountyRead = false;
    }

    /// <summary>
    /// Declines the offer: the day passes in the dojo.
    /// </summary>
    /// <remarks>
    /// There is <b>no</b> counterpart for accepting here: setting up the fight is the expedition layer's
    /// job (<see cref="Expedition"/>), and closing the day is again <see cref="AdvanceDay"/>. Merged
    /// into a single call, the core would be coupled to the combat resolver.
    /// </remarks>
    public DayReport Decline() => AdvanceDay();

    /// <summary>
    /// Buys a facility from the school.
    /// </summary>
    /// <remarks>
    /// A facility's price is <b>up front</b> and it is not sold back: the school is a permanent
    /// investment, and if it could be undone the player would rearrange the tree before every
    /// expedition. A node whose turn has not come or that cannot be paid for is not bought; the treasury does not go negative.
    /// </remarks>
    /// <returns><c>true</c> if it was bought.</returns>
    public bool BuySchoolNode(SchoolNodeId id)
    {
        SchoolNode node = SchoolTree.Find(id);
        if (School.Has(id) || School.IsBuilding(id) || node.Cost > Resources.Gold || !School.Begin(id))
        {
            return false;
        }

        Resources = Resources with { Gold = Resources.Gold - node.Cost };
        ApplySchool();
        return true;
    }

    /// <summary>
    /// Holds the day's tribunal: the standing man is answered, and the next dishonoured man is called.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stream is built from the seed and the day rather than carried: a verdict must be the same
    /// verdict when the save is opened again, and a carried stream would let a reload reroll it — the
    /// same door the offer and the market close.
    /// </para>
    /// <para>
    /// A warrior condemned here dies <b>through the roster</b>, like every other death, so permadeath,
    /// the name going back into the pool and the closing screen's count all keep working.
    /// </para>
    /// </remarks>
    private TribunalVerdict? HoldTribunal()
    {
        // A man who fell in today's fight is off the books before the tribunal sits: otherwise the day's
        // report would read a verdict over a corpse.
        foreach (RosterEntry entry in Roster.Entries)
        {
            if (!entry.Warrior.IsAlive || entry.Released)
            {
                Tribunal.Forget(entry.Id);
            }
        }

        TribunalVerdict? verdict = Tribunal.Close(Day, new Rng.SeededRandom(TribunalSeed()));

        if (verdict is not null)
        {
            if (verdict.Outcome == Honor.SeppukuOutcome.Seppuku)
            {
                Roster.Kill(verdict.Warrior);
                Tribunal.Forget(verdict.Warrior);
            }
            else if (Roster.Find(verdict.Warrior) is RosterEntry pardoned)
            {
                // A pardon is not an acquittal: he gets up in debt, a little above the threshold.
                pardoned.Warrior.Honor = HonorScale.Clamp(Tribunal.Tuning.PardonedHonor);
            }
        }

        foreach (RosterEntry entry in Roster.Living)
        {
            Tribunal.Summon(entry.Warrior, Day);
        }

        // The man called today is answered tomorrow: the day in between is the crowd's.
        Tribunal.CallNext(Day);

        return verdict;
    }

    /// <summary>The tribunal's stream for today — a function of the seed and the day, never carried.</summary>
    private ulong TribunalSeed()
    {
        ulong x = Seed ^ ((ulong)Day * 0xD1B54A32D192ED03);
        x ^= x >> 29;
        x *= 0x94D049BB133111EB;
        x ^= x >> 32;
        return x;
    }

    /// <summary>
    /// Files a fight for the season: the week's tick is answered and the books are written.
    /// </summary>
    /// <remarks>
    /// It is the <b>expedition layer</b> that calls this, not the resolver: what answers the compulsory
    /// week is a fight the dojo actually took the field for, and a fight simulated for measurement with
    /// no dojo behind it must not write a season it is not part of.
    /// </remarks>
    internal void RecordFight(bool victory, int dead) => Season.RecordFight(Day, victory, dead);

    /// <summary>
    /// Ends the warrior's term — he walks out free and is counted on the closing screen.
    /// </summary>
    /// <remarks>
    /// The one release the day loop refuses is a man in the infirmary: sending a wounded man out of the
    /// gate to save his upkeep is exactly the move the fiction cannot carry, and it is the same door the
    /// hungry-day rule closes. He can be released the day he is on his feet again.
    /// </remarks>
    /// <returns><c>true</c> if he walked out.</returns>
    public bool Release(WarriorId id)
    {
        RosterEntry? entry = Roster.Find(id);
        if (entry is null || entry.RecoveryDaysRemaining > 0 || !Roster.Release(id))
        {
            return false;
        }

        // A man who has walked out of the gate is not tried the next morning.
        Tribunal.Forget(id);
        return true;
    }

    /// <summary>The closing screen's figures — what the season did (docs/GDD.md §10).</summary>
    /// <remarks>
    /// The freed are counted from <b>both</b> ends: those released while the season ran, and everyone
    /// still standing when it ended, whose term the season outlived. A run that closes early frees the
    /// second group too — the men are out of the dojo either way, and the ending that pretended
    /// otherwise would be punishing the player for the day the run stopped.
    /// </remarks>
    public SeasonSummary Summarise() => new(
        Season.Phase,
        Math.Min(Day, Season.Tuning.Days),
        Season.Battles,
        Season.Victories,
        Season.HeadsTaken,
        Season.MissedWeeks,
        [.. Roster.Entries.Where(e => !e.Warrior.IsAlive).Select(e => e.Name)],
        [.. Roster.Released.Concat(Roster.Living).Select(e => e.Name)]);

    /// <summary>
    /// How many men the dojo can house — the master's six plus whatever the quarters branch added.
    /// </summary>
    /// <remarks>
    /// The ceiling counts the <b>living</b>: the dead keep their record and the released have walked
    /// out, and neither of them sleeps here.
    /// </remarks>
    public int Capacity => Tuning.RosterCapacity;

    /// <summary>Is there a bed free today?</summary>
    public bool HasRoomForAnother => Roster.Living.Count() < Capacity;

    /// <summary>The day a feast was last called; <c>null</c> if the dojo has never held one.</summary>
    public int? LastFeastDay { get; private set; }

    /// <summary>Can a feast be called today — is there sake, and has the cooldown passed?</summary>
    public bool CanFeast =>
        Roster.Living.Any()
        && Resources.Sake >= FeastSake
        && (LastFeastDay is not int last || Day - last >= Tuning.Morale.FeastCooldownDays);

    /// <summary>The sake a feast would drink today.</summary>
    public int FeastSake => Roster.Living.Count() * Math.Max(0, Tuning.Morale.SakePerWarrior);

    /// <summary>
    /// Calls a feast: the sake goes, the whole roster's spirits come up.
    /// </summary>
    /// <remarks>
    /// This is Open Decision #14's whole point (closed 2026-09-10). Sake is not eaten day by day like
    /// food — it sits in the store until the player decides the roster needs a night, which makes it
    /// the one lever he has over morale that is not "win a fight". The cooldown is what keeps it a
    /// decision instead of a standing order.
    /// </remarks>
    /// <returns><c>true</c> if the feast was held.</returns>
    public bool Feast()
    {
        if (!CanFeast)
        {
            return false;
        }

        Resources = Resources with { Sake = Resources.Sake - FeastSake };
        LastFeastDay = Day;

        foreach (RosterEntry entry in Roster.Living)
        {
            MoraleLedger.Raise(entry.Warrior, Tuning.Morale.FeastGain);
        }

        return true;
    }

    /// <summary>Buys sake from the market — the day's bill never buys it by itself.</summary>
    /// <returns>How many measures were actually bought.</returns>
    public int BuySake(int measures)
    {
        if (measures <= 0 || Economy.SakePrice <= 0)
        {
            return 0;
        }

        int affordable = Math.Min(measures, Resources.Gold / Economy.SakePrice);
        if (affordable <= 0)
        {
            return 0;
        }

        Resources = Resources with
        {
            Gold = Resources.Gold - (affordable * Economy.SakePrice),
            Sake = Resources.Sake + affordable,
        };

        return affordable;
    }

    /// <summary>
    /// The day's quiet movement of morale: the bard, the rest, and the hungry.
    /// </summary>
    /// <remarks>
    /// A hungry day is the only one of the three that Will brakes — the others are gains, and a
    /// stubborn man is not more cheerful, only harder to break (docs/GDD.md §3).
    /// </remarks>
    private void SettleMorale(RosterEntry entry, bool fed)
    {
        MoraleTuning morale = Tuning.Morale;

        // The day's own pull first: a victory's high and a defeat's hole both fade, so neither becomes a
        // permanent stat (the same shape as honour's decay).
        MoraleLedger.Drift(entry.Warrior, morale);

        if (!fed)
        {
            MoraleLedger.Lower(entry.Warrior, morale.HungerLoss, morale);
            return;
        }

        // A quiet day settles a man back toward the middle; it never lifts him above it. What is above
        // has to be bought — a victory, a feast, or the bard.
        MoraleLedger.Settle(entry.Warrior, morale.RestGain);

        if (School.Has(SchoolNodeId.BardHall))
        {
            MoraleLedger.Raise(
                entry.Warrior,
                morale.BardGain * School.Efficiency(SchoolNodeId.BardHall, Staff, StaffTuning));
        }
    }

    /// <summary>
    /// Fills a post. The building must be standing; the wage starts the next time the day closes.
    /// </summary>
    /// <remarks>
    /// Hiring costs no gold up front — the price of staff is the <b>wage</b>, and a joining fee would
    /// only blunt the one gear the economy has: letting someone go on a bad day (GDD §10).
    /// </remarks>
    /// <returns><c>true</c> if the post was taken.</returns>
    public bool Hire(StaffRole role)
    {
        if (!School.HasPostFor(role) || !Staff.Add(role))
        {
            return false;
        }

        ApplySchool();
        return true;
    }

    /// <summary>Lets the person go. The building stays and drops to half efficiency.</summary>
    public bool Dismiss(StaffRole role)
    {
        if (!Staff.Remove(role))
        {
            return false;
        }

        ApplySchool();
        return true;
    }

    /// <summary>
    /// Trains a warrior into a class — the hall must be standing and his body must allow it.
    /// </summary>
    /// <remarks>
    /// The facility is the price (GDD §10): the hall is what was bought, so the training itself costs
    /// no further gold. A class is <b>not</b> final the way a path is — losing an arm closes some of
    /// them and reopens the choice, which is why this refuses only a warrior who already has a class he
    /// can still practise.
    /// </remarks>
    /// <returns><c>true</c> if he could be trained into it.</returns>
    public bool TrainClass(WarriorId id, WarriorClass klass)
    {
        RosterEntry? entry = Roster.Find(id);
        if (entry is null
            || klass == WarriorClass.None
            || !entry.Warrior.IsAlive
            || !School.UnlockedClasses().Contains(klass)
            || !ClassAptitude.IsPossibleFor(klass, entry.Warrior.Disabilities))
        {
            return false;
        }

        // A class he can still practise is not re-chosen; one his body has closed is.
        if (entry.Warrior.Class != WarriorClass.None
            && ClassAptitude.IsPossibleFor(entry.Warrior.Class, entry.Warrior.Disabilities))
        {
            return false;
        }

        entry.Warrior.Class = klass;
        return true;
    }

    /// <summary>
    /// Chooses the warrior's path — once, with no way back.
    /// </summary>
    /// <remarks>
    /// What unlocks it is training days (<see cref="TrainingTuning.PathTrainingDays"/>): a path should
    /// be something <b>earned by working</b>, not something bought.
    /// </remarks>
    /// <returns><c>true</c> if it could be chosen.</returns>
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

    /// <summary>Restores the facilities coming from the save.</summary>
    /// <summary>Restores the "candidates bought today" mark coming from the save.</summary>
    /// <remarks>
    /// The candidates themselves are not written to the save (they are regenerated from the day and the
    /// seed), the only thing written is <b>which indices</b> were bought. Without it the player could
    /// buy the same candidate over and over by reloading the save.
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

    /// <summary>Restores the posts coming from the save; a post with no building is dropped.</summary>
    internal void RestoreStaff(IEnumerable<StaffRole> roles)
    {
        Staff.Restore(roles, School);
        ApplySchool();
    }

    /// <summary>Restores the day of the last feast, so a reload cannot buy a second night in a row.</summary>
    internal void RestoreFeast(int? day) => LastFeastDay = day;

    internal void RestoreSchool(
        IEnumerable<SchoolNodeId> owned,
        IEnumerable<Save.BuildSiteSnapshot>? sites = null)
    {
        School.Restore(
            owned,
            (sites ?? []).Select(s => new KeyValuePair<SchoolNodeId, int>(s.Id, s.DaysLeft)));
        ApplySchool();
    }

    /// <summary>Applies the facilities to the settings — after a purchase and after loading a save.</summary>
    private void ApplySchool()
    {
        Tuning = School.Apply(_baseTuning, Staff, StaffTuning);
        Quartermaster = new Quartermaster(School.Apply(_baseEconomy, Staff, StaffTuning));
    }

    /// <summary>Restores the season's books coming from the save.</summary>
    /// <remarks>
    /// The clock goes into the file because it is state the <b>player produced</b> — which weeks he
    /// answered, which heads he brought in. Without it a reload would hand back a fresh week and the
    /// compulsory fight would cost nothing.
    /// </remarks>
    internal void RestoreSeason(Save.SeasonSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Season.Restore(
            snapshot.Phase,
            snapshot.LastFightDay,
            snapshot.MissedWeeks,
            snapshot.HeadsTaken,
            snapshot.Battles,
            snapshot.Victories,
            snapshot.Dead,
            snapshot.FinalRound,
            snapshot.MissedStreak);
    }

    /// <summary>Restores the tribunal's books coming from the save.</summary>
    internal void RestoreTribunal(Save.TribunalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Tribunal.Restore(
            snapshot.Standing is Save.SummonsSnapshot standing ? Rebuild(standing) : null,
            (snapshot.Queue ?? []).Select(Rebuild),
            (snapshot.Immunity ?? []).Select(
                i => new KeyValuePair<WarriorId, int>(new WarriorId(i.Warrior), i.UntilDay)));
    }

    private static Summons Rebuild(Save.SummonsSnapshot snapshot) => new(
        new WarriorId(snapshot.Warrior),
        string.IsNullOrWhiteSpace(snapshot.Name) ? $"Warrior {snapshot.Warrior}" : snapshot.Name,
        HonorScale.Clamp(snapshot.Honor),
        Math.Clamp(snapshot.Willpower, 0, 100),
        Math.Max(1, snapshot.OpenedDay));

    /// <summary>Restores the tier coming from the save.</summary>
    internal void RestoreDifficulty(DifficultyTier tier) => Difficulty = tier;

    /// <summary>Restores the day counter coming from the save.</summary>
    internal void RestoreDay(int day)
    {
        Day = Math.Max(1, day);
        _offer = null;
        _recruits = null;
        _hiredToday.Clear();
        _bounty = null;
        _bountyRead = false;
    }

    /// <summary>Restores the expedition seed coming from the save.</summary>
    internal void RestoreSeed(ulong seed)
    {
        Seed = seed;
        _offer = null;
        _recruits = null;
        _bounty = null;
        _bountyRead = false;
    }

    /// <summary>
    /// Deducts the price of a contract that was accepted and whose last day has passed.
    /// </summary>
    /// <remarks>
    /// The penalty is written to <b>the whole roster</b>, not to the one who would have gone on the
    /// expedition: the dojo gave the promise, not a warrior. Written to one person, the player would
    /// dump the penalty on a warrior he had already written off and the promise would cost nothing —
    /// the price of pulling out is written to the whole team for the same reason (GDD §5).
    /// </remarks>
    private bool BreakBountyIfExpired()
    {
        if (AcceptedBountyDay is not int accepted)
        {
            return false;
        }

        // The measure is not today's state but <b>tomorrow's</b>: if the last day too closed without a
        // fight, the promise is broken. Looking at today, the penalty would land a day late and on the
        // evening of the last day the player would still count as "my promise stands".
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

    /// <summary>Pulls honour one day's worth toward neutral; it does not cross the threshold to the other side.</summary>
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

/// <summary>The summary of the day that closed — it feeds the interface's "what happened today" screen.</summary>
/// <param name="Day">The day that closed (the new day is one more than this).</param>
/// <param name="Recovered">The warriors who left the infirmary that day.</param>
/// <param name="Trained">The warriors who spent that day on the training ground.</param>
/// <param name="Upkeep">The day's food/water/medicine bill.</param>
/// <param name="Event">That day's mishap; <c>null</c> if a quiet day passed.</param>
/// <param name="Opened">The buildings that finished going up today.</param>
/// <param name="MissedWeek">
/// The compulsory-fight tick landed on a week with no fight filed, and the roster paid for it in honour.
/// </param>
/// <param name="Phase">Where the run stands after the day closed.</param>
/// <param name="Tribunal">The verdict the tribunal gave today; <c>null</c> if it gave none.</param>
/// <param name="HonorLost">
/// What the missed week cost every living warrior. It is <c>0</c> for the first missed week of a
/// streak — that one is free (<see cref="SeasonTuning.GraceWeeks"/>).
/// </param>
public sealed record DayReport(
    int Day,
    IReadOnlyList<WarriorId> Recovered,
    IReadOnlyList<WarriorId> Trained,
    UpkeepReport Upkeep,
    DayEvent? Event = null,
    bool BountyBroken = false,
    IReadOnlyList<SchoolNodeId>? Opened = null,
    bool MissedWeek = false,
    SeasonPhase Phase = SeasonPhase.Running,
    double HonorLost = 0,
    TribunalVerdict? Tribunal = null);

/// <summary>One day's store and treasury movement.</summary>
/// <param name="GoldSpent">The gold paid that day for stock bought from the market.</param>
/// <param name="Food">The food eaten.</param>
/// <param name="Water">The water drunk.</param>
/// <param name="Medicine">The medicine used.</param>
/// <param name="Hungry">The warriors who got no share — they do not heal or train that day.</param>
/// <param name="Medicated">The warriors given medicine — they burn extra infirmary days that day.</param>
/// <param name="Wages">The wages actually paid that day.</param>
/// <param name="Walked">
/// The staff who left because the payroll could not be met. The buildings stay; the people do not.
/// </param>
public sealed record UpkeepReport(
    int GoldSpent,
    int Food,
    int Water,
    int Medicine,
    IReadOnlySet<WarriorId> Hungry,
    IReadOnlySet<WarriorId> Medicated,
    int Wages = 0,
    IReadOnlyList<StaffRole>? Walked = null)
{
    /// <summary><c>true</c> if the whole roster was fed.</summary>
    public bool Fed => Hungry.Count == 0;
}
