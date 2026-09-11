namespace Domina.Core.Dojo;

/// <summary>The school's three branches.</summary>
/// <remarks>
/// The three branches look at three different bottlenecks: the training ground speeds up
/// <b>progress</b>, the infirmary <b>time</b>, the steward <b>the treasury</b>. There is never enough
/// money for all three at once — the branch itself is a decision, and the order is a second one.
/// </remarks>
public enum SchoolBranch
{
    /// <summary>Training ground — the speed and ceiling of training.</summary>
    Training,

    /// <summary>Infirmary — the days a wound eats.</summary>
    Infirmary,

    /// <summary>Steward — prices and rewards.</summary>
    Steward,

    /// <summary>Forge — what the kit costs and what may be worn.</summary>
    Equipment,

    /// <summary>The situational buildings: the kitchen, the shrine, the bard's hall, the diviner's hut.</summary>
    Support,

    /// <summary>The class halls — a class becomes trainable once its hall stands.</summary>
    Class,

    /// <summary>Quarters — how many men the dojo can house at all.</summary>
    Quarters,
}

/// <summary>A facility/staff member that can be bought at the school.</summary>
public enum SchoolNodeId
{
    /// <summary>Training ground: a training day earns more.</summary>
    TrainingGround,

    /// <summary>Kata master: the ceiling a warrior can approach rises.</summary>
    FormsMaster,

    /// <summary>Inner dojo: training speeds up a second time.</summary>
    InnerDojo,

    /// <summary>Infirmary: natural recovery burns two days a day.</summary>
    Infirmary,

    /// <summary>Herbalist: medicine burns one more day.</summary>
    Herbalist,

    /// <summary>Bone setter: the share of damage counted as a scratch grows.</summary>
    BoneSetter,

    /// <summary>Steward: the daily stock gets cheaper.</summary>
    Steward,

    /// <summary>Patron: victory pays more.</summary>
    Patron,

    /// <summary>Broker: hiring warriors and repairs get cheaper.</summary>
    Broker,

    /// <summary>Forge: the smith's post, and where a repair bill is cut.</summary>
    Forge,

    /// <summary>Kitchen: the cook's post — the day's food need falls.</summary>
    Kitchen,

    /// <summary>Shrine: the monk's post. It has no number yet — omamori are not written.</summary>
    Shrine,

    /// <summary>Bard's hall: the bard's post. It has no number yet — morale is step 6.</summary>
    BardHall,

    /// <summary>Diviner's hut: the diviner's post. It has no number yet — the offer screen does not read it.</summary>
    DivinerHut,

    /// <summary>Torite hall: the catching class becomes trainable.</summary>
    ToriteHall,

    /// <summary>Poison garden: the poison class becomes trainable.</summary>
    PoisonGarden,

    /// <summary>Archery range: the range class becomes trainable.</summary>
    ArcheryRange,

    /// <summary>Barracks: the dojo can house more men than the master left it room for.</summary>
    Barracks,

    /// <summary>Long house: the roster's ceiling rises a second time.</summary>
    LongHouse,
}

/// <summary>A node in the tree — its cost, its branch and what comes before it.</summary>
/// <param name="Id">The permanent identity; this is what goes into the save.</param>
/// <param name="Branch">The branch it belongs to.</param>
/// <param name="Name">Display name.</param>
/// <param name="Cost">The cost in gold.</param>
/// <param name="Requires">The node that must be bought first; <c>null</c> for a branch's first.</param>
/// <param name="BuildDays">
/// The days the building takes to go up. Gold is paid on the order and the days cannot be bought off
/// (GDD §10) — that is what makes the tree a <b>season-long</b> decision instead of a shopping list.
/// </param>
/// <param name="Role">
/// The post inside the building, if it has one. A building with a post works at
/// <see cref="StaffTuning.EmptyFacilityShare"/> while it stands empty; an upgrade tier carries no post
/// of its own, because one building holds one person.
/// </param>
/// <param name="Unlocks">The class this hall makes trainable, if it is a class hall.</param>
public sealed record SchoolNode(
    SchoolNodeId Id,
    SchoolBranch Branch,
    string Name,
    int Cost,
    SchoolNodeId? Requires = null,
    int BuildDays = 6,
    StaffRole? Role = null,
    Model.WarriorClass? Unlocks = null);

/// <summary>The school tree's catalogue and numbers.</summary>
/// <remarks>
/// <para>
/// GDD §10's decision: <b>the real long-term investment is in the school</b>, not in the warrior. The
/// reason is permadeath — if a dead warrior also took a large investment with him, the player would
/// avoid sending him into the field. The school does not die; the roster melts, the school stays.
/// </para>
/// <para>
/// The costs <b>rise</b> within a branch (200 / 400 / 700): the first node is reachable early, the
/// third is the business of a dojo that has survived. The numbers are <b>not locked</b>; the
/// measurement's question is which branch pays for itself.
/// </para>
/// </remarks>
public sealed record SchoolTuning
{
    /// <summary>
    /// What every building's construction time is multiplied by.
    /// </summary>
    /// <remarks>
    /// The days themselves live on the node, because they belong to the building; this is the knob that
    /// lets the <b>whole</b> calendar be swept at once — and set to 0 it puts the tree back the way it
    /// was before build time existed, which is how a measurement can separate "the facility is weak"
    /// from "the facility arrived too late".
    /// </remarks>
    public double BuildDaysFactor { get; init; } = 1.0;

    /// <summary>The training-speed multiplier of the training ground and the inner dojo.</summary>
    public double TrainingRateStep { get; init; } = 1.30;

    /// <summary>What the kata master adds to the percentage stat ceiling.</summary>
    public double CeilingBonus { get; init; } = 4;

    /// <summary>What the kata master adds to the health/stamina ceiling.</summary>
    public double PoolCeilingBonus { get; init; } = 20;

    /// <summary>The extra infirmary day the infirmary burns per day.</summary>
    public int RecoveryBonus { get; init; } = 1;

    /// <summary>The infirmary day the herbalist adds to medicine.</summary>
    public int MedicineBonus { get; init; } = 1;

    /// <summary>What the bone setter adds to the scratch share.</summary>
    public double FreeDamageBonus { get; init; } = 0.10;

    /// <summary>The multiplier the steward applies to daily stock prices.</summary>
    public double UpkeepPriceFactor { get; init; } = 0.80;

    /// <summary>The multiplier the patron applies to the victory reward.</summary>
    public double RewardFactor { get; init; } = 1.15;

    /// <summary>The multiplier the broker applies to hiring a warrior.</summary>
    public double RecruitPriceFactor { get; init; } = 0.75;

    /// <summary>The multiplier the broker applies to repairs.</summary>
    public double RepairPriceFactor { get; init; } = 0.80;

    /// <summary>
    /// The men each tier of the quarters branch adds to the roster's ceiling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dojo starts with room for six — docs/STORY.md's "starting roster ceiling", the room the dead
    /// master left. It is a <b>starting</b> ceiling and not a life sentence: the season's work is
    /// growing, and the last night's measurement said plainly what growth is worth (a developed dojo
    /// with eight men wins the night 38.5% of the time, with six 13.5%).
    /// </para>
    /// <para>
    /// Depth had been <b>free</b> until this branch existed, which is why the night read as either a
    /// formality or a wall depending on how many men the measurement happened to hand the dojo. Now it
    /// is bought like everything else: gold up front, days to build, and every extra man is another
    /// mouth on the daily bill for the rest of the season.
    /// </para>
    /// </remarks>
    public int HousingPerTier { get; init; } = 2;
}

/// <summary>The school tree itself — the nodes and their order.</summary>
public static class SchoolTree
{
    /// <summary>All the nodes, branch by branch and from cheapest to most expensive.</summary>
    public static IReadOnlyList<SchoolNode> All { get; } =
    [
        new(SchoolNodeId.TrainingGround, SchoolBranch.Training, "Training ground", 200, null, 6, StaffRole.DrillMaster),
        new(SchoolNodeId.FormsMaster, SchoolBranch.Training, "Kata hall", 400, SchoolNodeId.TrainingGround, 10, StaffRole.KataMaster),
        new(SchoolNodeId.InnerDojo, SchoolBranch.Training, "Inner dojo", 700, SchoolNodeId.FormsMaster, 14, StaffRole.WeaponMaster),

        new(SchoolNodeId.Infirmary, SchoolBranch.Infirmary, "Infirmary", 200, null, 6, StaffRole.Physician),
        new(SchoolNodeId.Herbalist, SchoolBranch.Infirmary, "Herbalist", 400, SchoolNodeId.Infirmary, 10),
        new(SchoolNodeId.BoneSetter, SchoolBranch.Infirmary, "Bone setter", 700, SchoolNodeId.Herbalist, 14),

        new(SchoolNodeId.Steward, SchoolBranch.Steward, "Steward", 200, null, 6, StaffRole.Steward),
        new(SchoolNodeId.Patron, SchoolBranch.Steward, "Patron", 400, SchoolNodeId.Steward, 10),
        new(SchoolNodeId.Broker, SchoolBranch.Steward, "Broker", 700, SchoolNodeId.Patron, 14, StaffRole.Broker),

        new(SchoolNodeId.Forge, SchoolBranch.Equipment, "Forge", 250, null, 8, StaffRole.Smith),

        new(SchoolNodeId.Kitchen, SchoolBranch.Support, "Kitchen", 150, null, 5, StaffRole.Cook),
        new(SchoolNodeId.Shrine, SchoolBranch.Support, "Shrine", 150, null, 5, StaffRole.Monk),
        new(SchoolNodeId.BardHall, SchoolBranch.Support, "Bard's hall", 150, null, 5, StaffRole.Bard),
        new(SchoolNodeId.DivinerHut, SchoolBranch.Support, "Diviner's hut", 150, null, 5, StaffRole.Diviner),

        new(SchoolNodeId.Barracks, SchoolBranch.Quarters, "Barracks", 300, null, 8),
        new(SchoolNodeId.LongHouse, SchoolBranch.Quarters, "Long house", 600, SchoolNodeId.Barracks, 12),

        new(SchoolNodeId.ToriteHall, SchoolBranch.Class, "Torite hall", 450, null, 12, null, Model.WarriorClass.Torite),
        new(SchoolNodeId.PoisonGarden, SchoolBranch.Class, "Poison garden", 450, null, 12, null, Model.WarriorClass.Dokushi),
        new(SchoolNodeId.ArcheryRange, SchoolBranch.Class, "Archery range", 450, null, 12, null, Model.WarriorClass.Kyudo),
    ];

    public static SchoolNode Find(SchoolNodeId id) => All.Single(n => n.Id == id);

    /// <summary>The nodes in one branch, in the order they must be bought.</summary>
    public static IEnumerable<SchoolNode> Of(SchoolBranch branch) =>
        All.Where(n => n.Branch == branch);

    /// <summary>The building that carries this post.</summary>
    public static SchoolNode Of(StaffRole role) => All.Single(n => n.Role == role);
}

/// <summary>What is a building, what is a post, and which of the two a number belongs to.</summary>
public static class Facilities
{
    /// <summary>
    /// The four posts that carry a branch — training, health, equipment, supply.
    /// </summary>
    /// <remarks>
    /// The split is not decoration: these four answer the dojo's four <b>continuous</b> expenses, so
    /// they are the posts a dojo keeps all season and the real weight on the payroll. The other seven
    /// are hired for a stretch and let go.
    /// </remarks>
    public static bool IsBranchRole(StaffRole role) =>
        role is StaffRole.DrillMaster or StaffRole.Physician or StaffRole.Smith or StaffRole.Steward;

    /// <summary>
    /// The posts that have no number in the code yet, because the system they belong to is not written.
    /// </summary>
    /// <remarks>
    /// They can still be hired and they still draw a wage: hiding them would have made the payroll read
    /// as complete when it is not. The weapon master waits on weapon mastery, the bard on morale
    /// (build-order step 6), the monk on omamori, the diviner on the offer screen.
    /// </remarks>
    public static bool IsInert(StaffRole role) =>
        role is StaffRole.WeaponMaster or StaffRole.Bard or StaffRole.Monk or StaffRole.Diviner;
}

/// <summary>The facilities the dojo owns.</summary>
/// <remarks>
/// It holds state, it does not decide: the side that deducts the money from the treasury and applies
/// the effects to the settings is <see cref="DojoState"/>. The reason for the separation is the save —
/// only <b>which nodes were bought</b> is written to the file; the size of the bonuses (a balance
/// number) is not, or an old save would bring back the old balance (GDD §2).
/// </remarks>
public sealed class School
{
    private readonly HashSet<SchoolNodeId> _owned = [];
    private readonly Dictionary<SchoolNodeId, int> _sites = [];

    public School(SchoolTuning? tuning = null) => Tuning = tuning ?? new SchoolTuning();

    public SchoolTuning Tuning { get; }

    /// <summary>The buildings still going up, and the days each has left.</summary>
    /// <remarks>
    /// Gold leaves the treasury on the order, the building arrives later, and no amount of gold
    /// shortens the wait (GDD §10). It is what stops the tree from being a shopping list: a facility
    /// ordered on day 150 of a 180-day season may never open.
    /// </remarks>
    public IReadOnlyDictionary<SchoolNodeId, int> UnderConstruction => _sites;

    /// <summary>Is this building ordered but not yet standing?</summary>
    public bool IsBuilding(SchoolNodeId id) => _sites.ContainsKey(id);

    /// <summary>The nodes bought.</summary>
    public IReadOnlyCollection<SchoolNodeId> Owned => _owned;

    public bool Has(SchoolNodeId id) => _owned.Contains(id);

    /// <summary>The nodes that can be bought today — affording them is a separate question.</summary>
    /// <remarks>
    /// The order within a branch is compulsory: the kata master does not come without the training
    /// ground. Without the order the tree would not be a tree but nine independent buttons.
    /// </remarks>
    public IEnumerable<SchoolNode> Available() =>
        SchoolTree.All.Where(n =>
            !Has(n.Id)
            && !IsBuilding(n.Id)
            && (n.Requires is null || Has(n.Requires.Value)));

    /// <summary>Does a building with this post stand today?</summary>
    /// <remarks>
    /// A post cannot be filled before its building opens — that is the whole reason a facility is
    /// bought before a person is hired, and why cutting the person leaves the building behind.
    /// </remarks>
    public bool HasPostFor(StaffRole role) => Has(SchoolTree.Of(role).Id);

    /// <summary>The class halls that stand — the classes that can be trained today.</summary>
    public IEnumerable<Model.WarriorClass> UnlockedClasses() =>
        SchoolTree.All
            .Where(n => n.Unlocks is not null && Has(n.Id))
            .Select(n => n.Unlocks!.Value);

    /// <summary>Puts a building on the site. <c>false</c> if it is standing, ordered, or out of order.</summary>
    internal bool Begin(SchoolNodeId id)
    {
        SchoolNode node = SchoolTree.Find(id);
        if (Has(id) || IsBuilding(id) || (node.Requires is not null && !Has(node.Requires.Value)))
        {
            return false;
        }

        int days = (int)Math.Ceiling(node.BuildDays * Tuning.BuildDaysFactor);
        if (days <= 0)
        {
            return Add(id);
        }

        _sites[id] = days;
        return true;
    }

    /// <summary>Burns a day on every site and returns the buildings that opened today.</summary>
    internal IReadOnlyList<SchoolNodeId> AdvanceConstruction()
    {
        if (_sites.Count == 0)
        {
            return [];
        }

        List<SchoolNodeId> opened = [];
        foreach (SchoolNodeId id in _sites.Keys.ToList())
        {
            int left = _sites[id] - 1;
            if (left > 0)
            {
                _sites[id] = left;
                continue;
            }

            _sites.Remove(id);
            if (Add(id))
            {
                opened.Add(id);
            }
        }

        return opened;
    }

    /// <summary>Takes ownership of the node. <c>false</c> if its turn has not come or it is already bought.</summary>
    internal bool Add(SchoolNodeId id)
    {
        SchoolNode node = SchoolTree.Find(id);
        if (Has(id) || (node.Requires is not null && !Has(node.Requires.Value)))
        {
            return false;
        }

        _owned.Add(id);
        return true;
    }

    /// <summary>Restores the facilities coming from the save.</summary>
    /// <remarks>
    /// The order check works here too: a corrupted save cannot say "there is a kata master but no
    /// training ground". The nodes are tried in catalogue order and one that does not hold is silently dropped.
    /// </remarks>
    internal void Restore(IEnumerable<SchoolNodeId> owned, IEnumerable<KeyValuePair<SchoolNodeId, int>>? sites = null)
    {
        _owned.Clear();
        _sites.Clear();
        HashSet<SchoolNodeId> wanted = [.. owned];
        foreach (SchoolNode node in SchoolTree.All)
        {
            if (wanted.Contains(node.Id))
            {
                Add(node.Id);
            }
        }

        // A site whose building already stands, or whose turn has not come, is dropped in silence —
        // the same rule the owned list follows: a corrupted save may not open a door the tree closes.
        foreach ((SchoolNodeId id, int days) in sites ?? [])
        {
            SchoolNode node = SchoolTree.Find(id);
            if (Has(id) || (node.Requires is not null && !Has(node.Requires.Value)))
            {
                continue;
            }

            _sites[id] = Math.Clamp(days, 1, Math.Max(1, (int)Math.Ceiling(node.BuildDays * Tuning.BuildDaysFactor)));
        }
    }

    /// <summary>
    /// The share of its own number a building produces today: full with its post filled, half while it
    /// stands empty, and nothing at all if it is not built.
    /// </summary>
    /// <remarks>
    /// The rule of GDD §10 — <b>the construction investment is never wasted</b>. It is applied to the
    /// building's <b>bonus</b>, not to the number it modifies: half of a ×1.30 is ×1.15, not ×0.65. An
    /// upgrade tier has no post of its own and always works in full; what it is upgrading already
    /// carries the post.
    /// </remarks>
    public double Efficiency(SchoolNodeId id, Staff? staff, StaffTuning? tuning = null)
    {
        if (!Has(id))
        {
            return 0;
        }

        SchoolNode node = SchoolTree.Find(id);
        if (node.Role is not StaffRole role)
        {
            return 1;
        }

        return staff?.Has(role) == true
            ? 1
            : (tuning ?? new StaffTuning()).EmptyFacilityShare;
    }

    /// <summary>The facilities bought, applied to the day-loop settings.</summary>
    public DojoTuning Apply(DojoTuning tuning, Staff? staff = null, StaffTuning? staffTuning = null)
    {
        ArgumentNullException.ThrowIfNull(tuning);

        double rate = tuning.Training.GapClosedPerDay;
        rate *= Stepped(Tuning.TrainingRateStep, SchoolNodeId.TrainingGround);
        rate *= Stepped(Tuning.TrainingRateStep, SchoolNodeId.InnerDojo);

        double ceiling = tuning.Training.SkillCeiling
            + (Tuning.CeilingBonus * Share(SchoolNodeId.FormsMaster));
        double pool = tuning.Training.PoolCeiling
            + (Tuning.PoolCeilingBonus * Share(SchoolNodeId.FormsMaster));

        double Share(SchoolNodeId id) => Efficiency(id, staff, staffTuning);

        // A multiplier is scaled through its bonus: ×1.30 at half is ×1.15, never ×0.65.
        double Stepped(double step, SchoolNodeId id) => 1 + ((step - 1) * Share(id));

        return tuning with
        {
            Training = tuning.Training with
            {
                GapClosedPerDay = rate,
                SkillCeiling = ceiling,
                PoolCeiling = pool,
            },
            // The bed is the exception to the half-efficiency rule: recovery is counted in whole days,
            // and half a day cannot be spent. A built infirmary heals at its full rate whether or not a
            // physician stands over it; what the physician adds is the medicine bill and the limbs he
            // saves, and both are gates, not shares.
            NaturalRecoveryPerDay = tuning.NaturalRecoveryPerDay
                + (Has(SchoolNodeId.Infirmary) ? Tuning.RecoveryBonus : 0),
            RecoveryFreeDamageShare = Math.Clamp(
                tuning.RecoveryFreeDamageShare
                    + (Has(SchoolNodeId.BoneSetter) ? Tuning.FreeDamageBonus : 0),
                0,
                1),

            // A roof is the other exception to the half-efficiency rule: a bed either exists or it does
            // not, and half a bed houses nobody.
            RosterCapacity = tuning.RosterCapacity
                + (Has(SchoolNodeId.Barracks) ? Tuning.HousingPerTier : 0)
                + (Has(SchoolNodeId.LongHouse) ? Tuning.HousingPerTier : 0),
        };
    }

    /// <summary>The facilities bought, applied to the price and reward settings.</summary>
    /// <remarks>
    /// A discount is rounded <b>down</b> and stops at 1. Rounded to nearest, the discount would disappear
    /// entirely on cheap items (2 gold of food ×0.80 = 1.6, which rounds back to 2): the steward branch
    /// would never touch the store. The floor of 1 stops a discount from making an item
    /// free.
    /// </remarks>
    public EconomyTuning Apply(EconomyTuning economy, Staff? staff = null, StaffTuning? staffTuning = null)
    {
        ArgumentNullException.ThrowIfNull(economy);

        StaffTuning wages = staffTuning ?? new StaffTuning();

        double repair = economy.RepairGoldPerWear;
        if (Has(SchoolNodeId.Broker))
        {
            repair *= Factored(Tuning.RepairPriceFactor, SchoolNodeId.Broker);
        }

        if (Has(SchoolNodeId.Forge))
        {
            repair *= Factored(wages.SmithRepairFactor, SchoolNodeId.Forge);
        }

        // A discount is scaled through the share it takes off: ×0.80 at half is ×0.90.
        double Factored(double factor, SchoolNodeId id) =>
            1 - ((1 - factor) * Efficiency(id, staff, staffTuning));

        int Priced(int price, SchoolNodeId node, double factor) =>
            Has(node) ? Math.Max(1, (int)Math.Floor(price * Factored(factor, node))) : price;

        return economy with
        {
            MedicineRecoveryDays = economy.MedicineRecoveryDays
                + (Has(SchoolNodeId.Herbalist) ? Tuning.MedicineBonus : 0),
            FoodPrice = Priced(economy.FoodPrice, SchoolNodeId.Steward, Tuning.UpkeepPriceFactor),
            WaterPrice = Priced(economy.WaterPrice, SchoolNodeId.Steward, Tuning.UpkeepPriceFactor),
            RecruitPrice = Priced(economy.RecruitPrice, SchoolNodeId.Broker, Tuning.RecruitPriceFactor),
            RepairGoldPerWear = repair,

            // The physician's post is a gate, not a share: with him in the infirmary the dojo stops
            // buying medicine altogether, and half a gate would only be a cheaper bill.
            MedicinePrice = staff?.Has(StaffRole.Physician) == true
                ? 0
                : Priced(economy.MedicinePrice, SchoolNodeId.Steward, Tuning.UpkeepPriceFactor),
            VictoryGoldPerEnemyHealth = Has(SchoolNodeId.Patron)
                ? economy.VictoryGoldPerEnemyHealth * Tuning.RewardFactor
                : economy.VictoryGoldPerEnemyHealth,
        };
    }
}
