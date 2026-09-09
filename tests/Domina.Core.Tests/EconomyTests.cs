using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The treasury, the store and the prices (GDD §11, Open Decision #5). Three rules are protected: the
/// treasury does not go negative, a repair is always cheaper than a new piece, and the price of a hungry
/// warrior is <b>time</b> — not death.
/// </summary>
public class EconomyTests
{
    private static DojoState Funded(int gold = 1000, EconomyTuning? economy = null)
    {
        DojoState state = new(economy: economy);
        state.Resources = new Resources(Gold: gold);
        return state;
    }

    [Fact]
    public void PieceCostsWhatItStops()
    {
        Quartermaster market = new(new EconomyTuning { ArmorGoldPerDurability = 2 });

        Assert.Equal(80, market.PiecePrice(ArmorPiece.Keikogi));
        Assert.Equal(360, market.PiecePrice(ArmorPiece.OYoroiCuirass));
        Assert.Equal(0, market.PiecePrice(ArmorPiece.Bare));
    }

    /// <summary>
    /// The rule itself: if a repair's price per point does not stay below a new piece's, there is no
    /// decision called repairing left, and everyone uses a piece until it breaks.
    /// </summary>
    [Fact]
    public void RepairingIsAlwaysCheaperThanReplacing()
    {
        EconomyTuning economy = new();
        Assert.True(economy.RepairGoldPerWear < economy.ArmorGoldPerDurability);

        Quartermaster market = new(economy);
        Warrior warrior = new(new WarriorId(1), "Kenji", WarriorStats.Recruit(), Weapon.Katana(), Armor.Medium())
        {
            ArmorWear = new ArmorWearSet().With(HitLocation.Torso, 500),
        };

        // Nothing above its pool is paid: a piece about to break is not repaired for more than a new one.
        Assert.True(market.RepairPrice(warrior, HitLocation.Torso) < market.PiecePrice(ArmorPiece.DoMaru));
    }

    [Fact]
    public void RepairClearsTheSlotAndTakesTheGold()
    {
        DojoState state = Funded();
        Warrior warrior = new(new WarriorId(1), "Kenji", WarriorStats.Recruit(), Weapon.Katana(), Armor.Medium())
        {
            ArmorWear = new ArmorWearSet().With(HitLocation.Torso, 40),
        };

        int price = state.Quartermaster.RepairPrice(warrior, HitLocation.Torso);
        Assert.True(price > 0);
        Assert.True(state.Quartermaster.Repair(state, warrior, HitLocation.Torso));

        Assert.Equal(0, warrior.ArmorWear.At(HitLocation.Torso));
        Assert.Equal(1000 - price, state.Resources.Gold);
        Assert.Equal(0, state.Quartermaster.RepairPrice(warrior, HitLocation.Torso));
    }

    [Fact]
    public void EmptyPurseBuysNothing()
    {
        DojoState state = Funded(gold: 5);
        Warrior warrior = new(new WarriorId(1), "Kenji", WarriorStats.Recruit(), Weapon.Katana(), Armor.Medium())
        {
            ArmorWear = new ArmorWearSet().With(HitLocation.Torso, 90),
        };

        Assert.False(state.Quartermaster.Repair(state, warrior, HitLocation.Torso));
        Assert.False(state.Quartermaster.Equip(state, warrior, HitLocation.Head, ArmorPiece.Kabuto));
        Assert.Null(state.Quartermaster.Hire(state, "Hana"));

        Assert.Equal(5, state.Resources.Gold);
        Assert.Equal(90, warrior.ArmorWear.At(HitLocation.Torso));
        Assert.Equal(ArmorPiece.Bare, warrior.Armor.Head);
        Assert.Empty(state.Roster.Entries);
    }

    /// <summary>Wear belongs to the piece: a new piece does not inherit the broken one's ledger.</summary>
    [Fact]
    public void NewPieceComesWithACleanLedger()
    {
        DojoState state = Funded();
        Warrior warrior = new(new WarriorId(1), "Kenji", WarriorStats.Recruit(), Weapon.Katana(), Armor.Light())
        {
            ArmorWear = new ArmorWearSet().With(HitLocation.Torso, 35),
        };

        Assert.True(state.Quartermaster.Equip(state, warrior, HitLocation.Torso, ArmorPiece.DoMaru));

        Assert.Equal(ArmorPiece.DoMaru, warrior.Armor.Torso);
        Assert.Equal(0, warrior.ArmorWear.At(HitLocation.Torso));
    }

    [Fact]
    public void RestockBuysOnlyWhatIsMissing()
    {
        EconomyTuning economy = new() { FoodPrice = 2, WaterPrice = 1, MedicinePrice = 10 };
        DojoState state = Funded(gold: 100, economy: economy);
        state.Resources = state.Resources with { Food = 3 };

        int spent = state.Quartermaster.Restock(state, new Resources(Food: 5, Water: 4, Medicine: 1));

        Assert.Equal((2 * 2) + (4 * 1) + 10, spent);
        Assert.Equal(5, state.Resources.Food);
        Assert.Equal(4, state.Resources.Water);
        Assert.Equal(1, state.Resources.Medicine);
        Assert.Equal(100 - spent, state.Resources.Gold);
    }

    [Fact]
    public void RestockStopsAtThePurseInsteadOfGoingNegative()
    {
        DojoState state = Funded(gold: 5, economy: new EconomyTuning { FoodPrice = 2 });

        state.Quartermaster.Restock(state, new Resources(Food: 10));

        Assert.Equal(2, state.Resources.Food);
        Assert.Equal(1, state.Resources.Gold);
        Assert.False(state.Resources.AnyNegative);
    }

    [Fact]
    public void RewardComesFromTheEncounterNotFromTheFight()
    {
        // The premium is off: this test holds <b>where</b> the reward comes from, not the premium laid
        // on weight (that stands separately below).
        Quartermaster market = new(
            new EconomyTuning { VictoryGoldPerEnemyHealth = 2, RiskPremium = 0 });
        BattleSetup setup = new(
            [new Warrior(new WarriorId(1), "Kenji", WarriorStats.Recruit(), Weapon.Katana())],
            [new Warrior(new WarriorId(101), "Oni", WarriorStats.Recruit() with { MaxHealth = 150 }, Weapon.Tetsubo())]);

        Assert.Equal(300, market.PromisedReward(setup));
        Assert.Equal(300, market.RewardFor(setup, BattleOutcome.PlayerVictory));

        // GDD §10: pulling out erases that expedition's reward; so does a rout.
        Assert.Equal(0, market.RewardFor(setup, BattleOutcome.PlayerWithdrawal));
        Assert.Equal(0, market.RewardFor(setup, BattleOutcome.PlayerWipe));
    }

    /// <summary>
    /// A heavier encounter pays <b>more</b> than its proportion.
    /// </summary>
    /// <remarks>
    /// With direct proportion the curve's top end was never worth taking: three strong enemies carry
    /// three times the health but more than three times the risk. The measurement is in GDD §11 — without
    /// the premium it falls below zero net per fight in the long run.
    /// </remarks>
    [Fact]
    public void AHeavierEncounterPaysMoreThanItsShare()
    {
        EconomyTuning economy = new()
        {
            VictoryGoldPerEnemyHealth = 1,
            RiskPremium = 0.25,
            RiskFreeEnemyHealth = 100,
        };
        Quartermaster market = new(economy);

        int ordinary = market.PromisedReward(Against(health: 100));
        int heavy = market.PromisedReward(Against(health: 300));

        Assert.Equal(100, ordinary);
        Assert.Equal(450, heavy);

        static BattleSetup Against(double health) => new(
            [new Warrior(new WarriorId(1), "Kenji", WarriorStats.Recruit(), Weapon.Katana())],
            [
                new Warrior(
                    new WarriorId(101),
                    "Oni",
                    WarriorStats.Recruit() with { MaxHealth = health },
                    Weapon.Tetsubo()),
            ]);
    }

    [Fact]
    public void EveryDayEatsFromThePurse()
    {
        EconomyTuning economy = new() { FoodPrice = 2, WaterPrice = 1 };
        DojoState state = Funded(gold: 100, economy: economy);
        state.Roster.Recruit("Kenji");
        state.Roster.Recruit("Hana");

        DayReport report = state.AdvanceDay();

        Assert.Equal(2 * (2 + 1), report.Upkeep.GoldSpent);
        Assert.Equal(94, state.Resources.Gold);
        Assert.True(report.Upkeep.Fed);
        Assert.Equal(0, state.Resources.Food);
    }

    [Fact]
    public void MedicineBuysADayOfHealing()
    {
        DojoState state = Funded();
        RosterEntry entry = state.Roster.Recruit("Kenji");
        entry.Injure(4);

        DayReport report = state.AdvanceDay();

        Assert.Contains(entry.Id, report.Upkeep.Medicated);
        Assert.Equal(1, report.Upkeep.Medicine);
        Assert.Equal(2, entry.RecoveryDaysRemaining);
    }

    /// <summary>
    /// The price of scarcity is time: a hungry warrior neither heals nor trains that day.
    /// Nobody dies — hunger is not an irreversible penalty.
    /// </summary>
    [Fact]
    public void HungerCostsTheDayNotTheWarrior()
    {
        DojoState state = Funded(gold: 0);
        RosterEntry wounded = state.Roster.Recruit("Kenji");
        RosterEntry student = state.Roster.Recruit("Hana");
        wounded.Injure(3);
        student.Train();

        DayReport report = state.AdvanceDay();

        Assert.False(report.Upkeep.Fed);
        Assert.Equal(3, wounded.RecoveryDaysRemaining);
        Assert.Equal(0, student.TrainingDays);
        Assert.Empty(report.Trained);
        Assert.True(wounded.Warrior.IsAlive);
        Assert.True(student.Warrior.IsAlive);
    }

    /// <summary>If the store is short, those in the infirmary eat first — leaving the wounded hungry would compound the scarcity.</summary>
    [Fact]
    public void TheInfirmaryEatsFirst()
    {
        // Food for one: one of the two warriors will go hungry.
        DojoState state = Funded(
            gold: 0,
            economy: new EconomyTuning { MedicinePerInfirmaryDay = 0, MedicineRecoveryDays = 0 });
        RosterEntry healthy = state.Roster.Recruit("Hana");
        RosterEntry wounded = state.Roster.Recruit("Kenji");
        wounded.Injure(3);
        state.Resources = new Resources(Food: 1, Water: 1);

        DayReport report = state.AdvanceDay();

        Assert.Contains(healthy.Id, report.Upkeep.Hungry);
        Assert.DoesNotContain(wounded.Id, report.Upkeep.Hungry);
        Assert.Equal(2, wounded.RecoveryDaysRemaining);
    }
}
