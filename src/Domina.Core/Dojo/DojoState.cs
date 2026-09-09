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

    public EconomyTuning Economy => Quartermaster.Economy;

    /// <summary>The wheel that produces the day's offer.</summary>
    public EncounterGenerator Encounters { get; }

    /// <summary>The table that draws the day's mishap (GDD §11: random events).</summary>
    public DayEventTable Events { get; }

    /// <summary>The warrior market.</summary>
    public RecruitMarket Market { get; }

    /// <summary>The board the bounty contracts are posted on.</summary>
    public BountyBoard Bounties { get; }

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
        }

        // The promise is weighed at the end of the day: if the last day too closed without a fight, the contract is broken.
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
    public IReadOnlyList<RecruitOffer> Recruits =>
        _recruits ??= Market.Stock(Day, Seed, Market.AnchorFor(Roster), Economy.RecruitPrice);

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
        if (School.Has(id) || node.Cost > Resources.Gold || !School.Add(id))
        {
            return false;
        }

        Resources = Resources with { Gold = Resources.Gold - node.Cost };
        ApplySchool();
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

    internal void RestoreSchool(IEnumerable<SchoolNodeId> owned)
    {
        School.Restore(owned);
        ApplySchool();
    }

    /// <summary>Applies the facilities to the settings — after a purchase and after loading a save.</summary>
    private void ApplySchool()
    {
        Tuning = School.Apply(_baseTuning);
        Quartermaster = new Quartermaster(School.Apply(_baseEconomy));
    }

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
public sealed record DayReport(
    int Day,
    IReadOnlyList<WarriorId> Recovered,
    IReadOnlyList<WarriorId> Trained,
    UpkeepReport Upkeep,
    DayEvent? Event = null,
    bool BountyBroken = false);

/// <summary>One day's store and treasury movement.</summary>
/// <param name="GoldSpent">The gold paid that day for stock bought from the market.</param>
/// <param name="Food">The food eaten.</param>
/// <param name="Water">The water drunk.</param>
/// <param name="Medicine">The medicine used.</param>
/// <param name="Hungry">The warriors who got no share — they do not heal or train that day.</param>
/// <param name="Medicated">The warriors given medicine — they burn extra infirmary days that day.</param>
public sealed record UpkeepReport(
    int GoldSpent,
    int Food,
    int Water,
    int Medicine,
    IReadOnlySet<WarriorId> Hungry,
    IReadOnlySet<WarriorId> Medicated)
{
    /// <summary><c>true</c> if the whole roster was fed.</summary>
    public bool Fed => Hungry.Count == 0;
}
