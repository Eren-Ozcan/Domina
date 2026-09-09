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
}

/// <summary>A node in the tree — its cost, its branch and what comes before it.</summary>
/// <param name="Id">The permanent identity; this is what goes into the save.</param>
/// <param name="Branch">The branch it belongs to.</param>
/// <param name="Name">Display name.</param>
/// <param name="Cost">The cost in gold.</param>
/// <param name="Requires">The node that must be bought first; <c>null</c> for a branch's first.</param>
public sealed record SchoolNode(
    SchoolNodeId Id,
    SchoolBranch Branch,
    string Name,
    int Cost,
    SchoolNodeId? Requires = null);

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
}

/// <summary>The school tree itself — the nodes and their order.</summary>
public static class SchoolTree
{
    /// <summary>All the nodes, branch by branch and from cheapest to most expensive.</summary>
    public static IReadOnlyList<SchoolNode> All { get; } =
    [
        new(SchoolNodeId.TrainingGround, SchoolBranch.Training, "Talimhane", 200),
        new(SchoolNodeId.FormsMaster, SchoolBranch.Training, "Kata master", 400, SchoolNodeId.TrainingGround),
        new(SchoolNodeId.InnerDojo, SchoolBranch.Training, "Inner dojo", 700, SchoolNodeId.FormsMaster),

        new(SchoolNodeId.Infirmary, SchoolBranch.Infirmary, "Revir", 200),
        new(SchoolNodeId.Herbalist, SchoolBranch.Infirmary, "Herbalist", 400, SchoolNodeId.Infirmary),
        new(SchoolNodeId.BoneSetter, SchoolBranch.Infirmary, "Bone setter", 700, SchoolNodeId.Herbalist),

        new(SchoolNodeId.Steward, SchoolBranch.Steward, "Kâhya", 200),
        new(SchoolNodeId.Patron, SchoolBranch.Steward, "Hami", 400, SchoolNodeId.Steward),
        new(SchoolNodeId.Broker, SchoolBranch.Steward, "Simsar", 700, SchoolNodeId.Patron),
    ];

    public static SchoolNode Find(SchoolNodeId id) => All.Single(n => n.Id == id);

    /// <summary>The nodes in one branch, in the order they must be bought.</summary>
    public static IEnumerable<SchoolNode> Of(SchoolBranch branch) =>
        All.Where(n => n.Branch == branch);
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

    public School(SchoolTuning? tuning = null) => Tuning = tuning ?? new SchoolTuning();

    public SchoolTuning Tuning { get; }

    /// <summary>The nodes bought.</summary>
    public IReadOnlyCollection<SchoolNodeId> Owned => _owned;

    public bool Has(SchoolNodeId id) => _owned.Contains(id);

    /// <summary>The nodes that can be bought today — affording them is a separate question.</summary>
    /// <remarks>
    /// The order within a branch is compulsory: the kata master does not come without the training
    /// ground. Without the order the tree would not be a tree but nine independent buttons.
    /// </remarks>
    public IEnumerable<SchoolNode> Available() =>
        SchoolTree.All.Where(n => !Has(n.Id) && (n.Requires is null || Has(n.Requires.Value)));

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
    internal void Restore(IEnumerable<SchoolNodeId> owned)
    {
        _owned.Clear();
        HashSet<SchoolNodeId> wanted = [.. owned];
        foreach (SchoolNode node in SchoolTree.All)
        {
            if (wanted.Contains(node.Id))
            {
                Add(node.Id);
            }
        }
    }

    /// <summary>The facilities bought, applied to the day-loop settings.</summary>
    public DojoTuning Apply(DojoTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);

        double rate = tuning.Training.GapClosedPerDay;
        if (Has(SchoolNodeId.TrainingGround))
        {
            rate *= Tuning.TrainingRateStep;
        }

        if (Has(SchoolNodeId.InnerDojo))
        {
            rate *= Tuning.TrainingRateStep;
        }

        double ceiling = tuning.Training.SkillCeiling;
        double pool = tuning.Training.PoolCeiling;
        if (Has(SchoolNodeId.FormsMaster))
        {
            ceiling += Tuning.CeilingBonus;
            pool += Tuning.PoolCeilingBonus;
        }

        return tuning with
        {
            Training = tuning.Training with
            {
                GapClosedPerDay = rate,
                SkillCeiling = ceiling,
                PoolCeiling = pool,
            },
            NaturalRecoveryPerDay = tuning.NaturalRecoveryPerDay
                + (Has(SchoolNodeId.Infirmary) ? Tuning.RecoveryBonus : 0),
            RecoveryFreeDamageShare = Math.Clamp(
                tuning.RecoveryFreeDamageShare
                    + (Has(SchoolNodeId.BoneSetter) ? Tuning.FreeDamageBonus : 0),
                0,
                1),
        };
    }

    /// <summary>The facilities bought, applied to the price and reward settings.</summary>
    /// <remarks>
    /// A discount is rounded <b>down</b> and stops at 1. Rounded to nearest, the discount would disappear
    /// entirely on cheap items (2 gold of food ×0.80 = 1.6, which rounds back to 2): the steward branch
    /// would never touch the store. The floor of 1 stops a discount from making an item
    /// free.
    /// </remarks>
    public EconomyTuning Apply(EconomyTuning economy)
    {
        ArgumentNullException.ThrowIfNull(economy);

        return economy with
        {
            MedicineRecoveryDays = economy.MedicineRecoveryDays
                + (Has(SchoolNodeId.Herbalist) ? Tuning.MedicineBonus : 0),
            FoodPrice = Priced(economy.FoodPrice, SchoolNodeId.Steward, Tuning.UpkeepPriceFactor),
            WaterPrice = Priced(economy.WaterPrice, SchoolNodeId.Steward, Tuning.UpkeepPriceFactor),
            MedicinePrice = Priced(economy.MedicinePrice, SchoolNodeId.Steward, Tuning.UpkeepPriceFactor),
            RecruitPrice = Priced(economy.RecruitPrice, SchoolNodeId.Broker, Tuning.RecruitPriceFactor),
            RepairGoldPerWear = Has(SchoolNodeId.Broker)
                ? economy.RepairGoldPerWear * Tuning.RepairPriceFactor
                : economy.RepairGoldPerWear,
            VictoryGoldPerEnemyHealth = Has(SchoolNodeId.Patron)
                ? economy.VictoryGoldPerEnemyHealth * Tuning.RewardFactor
                : economy.VictoryGoldPerEnemyHealth,
        };
    }

    private int Priced(int price, SchoolNodeId node, double factor) =>
        Has(node) ? Math.Max(1, (int)Math.Floor(price * factor)) : price;
}
