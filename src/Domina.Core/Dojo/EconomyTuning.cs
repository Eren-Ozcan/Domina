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

    /// <summary>
    /// The multiplier over what the temple asks for a charm.
    /// </summary>
    /// <remarks>
    /// The catalogue carries the price and this carries the balance, because the charm is the one
    /// piece of kit that competes directly with the <b>school</b>: 120 gold (40 for the two charms
    /// whose stat cannot convert more) against a 200-gold building that compounds for the rest of the
    /// season. That trade is what the number has to get right.
    /// </remarks>
    public double CharmPriceFactor { get; init; } = 1.0;

    /// <summary>What reforging a weapon costs, per point of the damage it already deals.</summary>
    /// <remarks>
    /// Priced off the weapon rather than flat, so the forge is worth most to the warrior carrying the
    /// heaviest thing in the dojo — and so reforging a tantō is never the cheap way to a better blade.
    /// </remarks>
    public double ForgeGoldPerDamage { get; init; } = 12;

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
    /// The price of a thrown implement, per point of the damage its full quiver can deliver.
    /// </summary>
    /// <remarks>
    /// The stall prices a throwing implement by what it can actually throw — damage times ammunition —
    /// because that is the axis the four of them differ on: a shuriken is a handful of small cuts and
    /// the yumi is ten arrows worth more than two stars each. Range and draw time are deliberately not
    /// in the price: they are what the <b>class</b> is for, and paying for them at the stall would sell
    /// the kyudo's payoff to a dojo that never built the hall.
    ///
    /// <b>Locked at 1.4 on 2026-09-13</b> — 68 gold for a handful of stars, 364 for a bow. Four seeds
    /// × 1600 dojos × 180 days, every system on, against a control that fills no throwing slot at all:
    /// the last night is won 17.3% (nobody throws) / 22.0 (1.1) / 20.5 (1.4) / 18.4 (1.65) / 17.0
    /// (2.2), and the net per fight 28.2 / 31.7 / 30.1 / 28.2 / 25.8. <b>The trade turns at about
    /// 1.65</b>, where the slot pays for itself and no more, and at 2.2 filling it is worse than
    /// leaving it empty. 1.4 is one rung below the turn on purpose: at 1.1 the slot is so cheap that
    /// buying it is not a decision, and at the turn the stall is decoration. At 1.4 it costs a dojo
    /// about 536 gold a season, which puts it beside the temple's charms (693) as a real competitor
    /// for the school's money.
    /// </remarks>
    public double ThrownGoldPerDamage { get; init; } = 1.4;

    /// <summary>What a full dose of poison adds to a thrown implement's price, as a share.</summary>
    /// <remarks>
    /// A poisoned star carries half the ammunition of a clean one, so priced on the quiver alone it
    /// would be the cheapest thing on the stall while being the thing the plate cannot read. The
    /// premium is applied as a share of the implement's own price, so it stays proportional to what is
    /// being poisoned.
    ///
    /// At exactly 1.0 the two stars cost the same to the gold — half the quiver, twice the price — and
    /// a stall where the poisoned star is never the dearer of the two is a stall with no decision on
    /// it. 1.5 puts it a quarter above the clean star it is made from.
    /// </remarks>
    public double ThrownPoisonPremium { get; init; } = 1.5;

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

    /// <summary>The food a beaten band leaves behind, per enemy put down.</summary>
    /// <remarks>
    /// <para>
    /// Men who came to the field carried what they meant to eat on it. Paying the spoils in <b>stores</b>
    /// rather than in coin aims at a different channel from the reward: gold answers the treasury, and
    /// the store answers the <b>calendar</b>. A dojo that cannot buy food does not heal, and a roster
    /// that does not heal cannot take the field — which is the loop a purse alone cannot open.
    /// </para>
    /// <para>
    /// <b>4 per enemy</b>, chosen on 2026-09-15 as the last step that is free. Swept 0-8 against the
    /// documented policy (10.000 dojos x 180 days, <c>--accept-ratio 2.0</c>): the treasury's gain
    /// saturates here (net per fight 38.8 -> 45.2, and 45.4 at 5 and 6), while what the dojo does with
    /// the food keeps paying — training days 65.2 -> 73.9 and the best man 256 -> 273. Above it the
    /// closure rate starts to move for real (20.7% at 5, 22.0% at 8, against 18.5% with none), and the
    /// extra hunger relief buys no more nights won.
    /// </para>
    /// <para>
    /// <b>The closure cost at 4 is not real.</b> Three seeds put it at +0.7, -0.2 and +0.5 points —
    /// astride zero — while the training gain, the best man's score and the night repeat in all three.
    /// That replication is what chose the number, not the single-seed table.
    /// </para>
    /// </remarks>
    public double VictoryFoodPerEnemy { get; init; } = 4;

    /// <summary>The water a beaten band leaves behind, per enemy put down.</summary>
    /// <remarks>
    /// A quarter of the food. Water on its own moves almost nothing (net 37.5 -> 38.8, closures 18.3%
    /// -> 18.5%), so the ratio is not carrying a measurement — it is carrying the prices: water costs 1
    /// and food 2, so food is the scarce good and the spoils pay mostly in the scarce good. The
    /// reference pays 75 food against 12 water on one fight, a steeper version of the same asymmetry.
    /// </remarks>
    public double VictoryWaterPerEnemy { get; init; } = 1;
}
