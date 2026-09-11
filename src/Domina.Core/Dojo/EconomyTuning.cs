namespace Domina.Core.Dojo;

/// <summary>The treasury's tunable numbers — prices, daily consumption, reward.</summary>
/// <remarks>
/// <para>
/// The numbers here are the subject of Open Decision #5. GDD §11 only locks the <b>items</b> (income:
/// the fight reward; spending: equipment, food/water, medicine, hiring warriors); their sizes are
/// settled by measurement. That is why they all live in one place and <c>Domina.Sim</c> can measure
/// them by playing them out fight by fight.
/// </para>
/// <para>
/// The single currency is <b>gold</b>. Food, water and medicine stand in the store as countable stock
/// but are bought from the market with gold: the economy's only scarce resource must not be split, and
/// the decision "what shall I buy today" should look at a single number.
/// </para>
/// </remarks>
public sealed record EconomyTuning
{
    /// <summary>One warrior's daily food.</summary>
    public int FoodPerWarriorPerDay { get; init; } = 1;

    /// <summary>One warrior's daily water.</summary>
    public int WaterPerWarriorPerDay { get; init; } = 1;

    /// <summary>The daily medicine for a warrior in the infirmary.</summary>
    public int MedicinePerInfirmaryDay { get; init; } = 1;

    /// <summary>
    /// The extra infirmary day the medicine burns that day.
    /// </summary>
    /// <remarks>
    /// Medicine comes <b>on top of</b> natural recovery (GDD §7): a day without medicine burns a day
    /// too, a day with it burns two. Otherwise medicine would be a compulsory tax, not a decision.
    /// </remarks>
    public int MedicineRecoveryDays { get; init; } = 1;

    public int FoodPrice { get; init; } = 2;

    public int WaterPrice { get; init; } = 1;

    public int MedicinePrice { get; init; } = 12;

    /// <summary>
    /// What one measure of sake costs (Open Decision #14).
    /// </summary>
    /// <remarks>
    /// It is priced above food and below medicine deliberately: a feast should be an expense a healthy
    /// dojo notices and a struggling one cannot justify. Sake is <b>never</b> restocked automatically —
    /// the day's bill does not buy it, the player does.
    /// </remarks>
    public int SakePrice { get; init; } = 6;

    /// <summary>The purchase price of a new warrior.</summary>
    public int RecruitPrice { get; init; } = 150;

    /// <summary>
    /// The price of an armour piece, per durability point.
    /// </summary>
    /// <remarks>
    /// The price scales with <see cref="Model.ArmorPiece.Durability"/>: a piece is worth as much damage
    /// as it stops. The protection ratio is not a separate multiplier — the two numbers already grow in
    /// the same direction, and multiplying both would punish the expensive end twice.
    /// </remarks>
    public double ArmorGoldPerDurability { get; init; } = 1.5;

    /// <summary>
    /// The price of a repair, per wear point erased.
    /// </summary>
    /// <remarks>
    /// It <b>must</b> be below <see cref="ArmorGoldPerDurability"/>: equal or above, repairing would
    /// never make sense and everyone would use a piece until it broke and buy a new one. The difference
    /// is repairing's margin; the size of that difference is the whole of the decision "repair early or
    /// use it to the end".
    /// </remarks>
    public double RepairGoldPerWear { get; init; } = 0.9;

    /// <summary>The victory reward, in gold per point of the enemy's total health.</summary>
    /// <remarks>
    /// The reward comes out of the encounter <b>itself</b>, not out of how the fight went: the same
    /// enemy pays the same money. The difficulty curve (GDD §10) thus carries income too — there is no
    /// need to keep a separate reward table.
    /// </remarks>
    public double VictoryGoldPerEnemyHealth { get; init; } = 0.45;

    /// <summary>
    /// The premium risk adds to the reward — at 0 the reward is directly proportional to enemy health.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Direct proportion breaks down in one place: three strong enemies carry three times the
    /// <b>health</b> of one weak enemy but not three times the <b>risk</b> — they carry more (three
    /// enemies focused on you strike at once, fleeing gets harder, and the chance of death does not grow
    /// linearly). As long as the reward stays proportional to health, the curve's top end is
    /// <b>never</b> worth taking and the player keeps declining offers even as his dojo grows.
    /// </para>
    /// <para>
    /// The premium starts above <see cref="RiskFreeEnemyHealth"/>: an ordinary encounter pays as much as
    /// before, a heavier one more than its proportion.
    /// </para>
    /// </remarks>
    public double RiskPremium { get; init; } = 0.25;

    /// <summary>The enemy health at which the premium starts — below this counts as ordinary work.</summary>
    public double RiskFreeEnemyHealth { get; init; } = 100;

    /// <summary>The reward of an expedition withdrawn from or routed.</summary>
    /// <remarks>Zero — GDD §10: surrendering erases that expedition's reward.</remarks>
    public int LostBattleGold { get; init; }
}
