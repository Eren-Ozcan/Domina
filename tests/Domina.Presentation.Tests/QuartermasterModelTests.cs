using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;

namespace Domina.Presentation.Tests;

/// <summary>
/// The quartermaster's counter as a screen reads it. What is protected here is that the model refuses
/// exactly what the core refuses: a row that offers to sell and a purchase that is turned away would
/// be a screen lying about the dojo's own rules.
/// </summary>
public class QuartermasterModelTests
{
    private static (DojoState Dojo, WarriorId Id) Dojo(int gold = 2000, Armor? armor = null)
    {
        DojoState state = new(seed: 11)
        {
            Purse = new Resources(Gold: gold, Food: 40, Water: 40, Medicine: 4),
        };

        RosterEntry entry = state.Roster.Recruit("Kenji", weapon: Weapon.Katana(), armor: armor ?? Armor.Light());
        return (state, entry.Id);
    }

    private static CounterCard Counter(DojoState dojo, WarriorId id) =>
        QuartermasterModel.Describe(dojo, id) ?? throw new InvalidOperationException("no counter");

    private static RackCard Rack(DojoState dojo, WarriorId id) =>
        QuartermasterModel.Rack(dojo, id) ?? throw new InvalidOperationException("no rack");

    [Fact]
    public void TheCounterCoversEveryRegionAndTheWholeThrowingStall()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        CounterCard card = Counter(dojo, id);

        Assert.Equal(ArmorSlots.All.Count, card.Slots.Count);
        Assert.Equal(ThrownWeapon.Catalogue.Count, card.Thrown.Count);
        Assert.Null(card.Carrying);
    }

    /// <summary>A dead man has no kit to improve.</summary>
    [Fact]
    public void TheDeadHaveNoCounter()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        dojo.Roster.Kill(id);

        Assert.Null(QuartermasterModel.Describe(dojo, id));
    }

    /// <summary>An unworn slot has no wear to erase, and that is not the same as being too expensive.</summary>
    [Fact]
    public void AnUnwornSlotOffersNoRepair()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        ArmorSlotRow row = Counter(dojo, id).Slots.First(s => s.Slot == HitLocation.Torso);

        Assert.Equal(0, row.RepairPrice);
        Assert.Equal(CounterRefusal.Nothing, row.RepairRefusal);
        Assert.False(row.CanRepair);
    }

    /// <summary>A worn piece prices its own repair, and the bar the screen draws follows the wear.</summary>
    [Fact]
    public void AWornPieceIsPricedAndMeasured()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        Warrior warrior = dojo.Roster.Find(id)!.Warrior;
        warrior.ArmorWear = warrior.ArmorWear.With(HitLocation.Torso, 20);

        ArmorSlotRow row = Counter(dojo, id).Slots.First(s => s.Slot == HitLocation.Torso);

        Assert.True(row.RepairPrice > 0);
        Assert.True(row.CanRepair);
        Assert.Equal(20 / row.Worn.Durability, row.WornShare, 3);
    }

    /// <summary>What he is already wearing is not sold to him twice.</summary>
    [Fact]
    public void ThePieceHeIsWearingIsNotOnSale()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        ArmorSlotRow row = Counter(dojo, id).Slots.First(s => s.Slot == HitLocation.Torso);

        ArmorOffer worn = row.Offers.First(o => o.Piece == row.Worn);
        Assert.Equal(CounterRefusal.AlreadyCarried, worn.Refusal);
    }

    /// <summary>
    /// Plate stays on the list and is refused at the counter: the player should see what the plate
    /// works would open rather than wonder why the list is short.
    /// </summary>
    [Fact]
    public void PlateIsListedAndRefusedWithoutTheSmith()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        ArmorSlotRow row = Counter(dojo, id).Slots.First(s => s.Slot == HitLocation.Torso);

        ArmorOffer plate = row.Offers.First(o => o.Piece.NeedsSmith);
        Assert.Equal(CounterRefusal.NeedsSmith, plate.Refusal);
        Assert.False(plate.CanBuy);
    }

    /// <summary>An empty purse refuses everything, and says so as a price rather than as a gate.</summary>
    [Fact]
    public void AnEmptyPurseRefusesOnPrice()
    {
        (DojoState dojo, WarriorId id) = Dojo(gold: 0);
        CounterCard card = Counter(dojo, id);

        Assert.All(card.Thrown, t => Assert.Equal(CounterRefusal.TooExpensive, t.Refusal));
        Assert.Equal(CounterRefusal.NeedsForge, card.ForgeRefusal);
    }

    /// <summary>The bow is the one implement the stall marks as taught rather than picked up.</summary>
    [Fact]
    public void TheBowIsMarkedAsAnImplementForATrainedHand()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        IReadOnlyList<ThrownOffer> stall = Counter(dojo, id).Thrown;

        Assert.True(stall.Single(t => t.Thrown.Name == "Yumi").ClassGated);
        Assert.All(stall.Where(t => t.Thrown.Name != "Yumi"), t => Assert.False(t.ClassGated));
    }

    /// <summary>The stall prices the quiver: ten heavy arrows cost more than a handful of stars.</summary>
    [Fact]
    public void TheQuiverIsWhatIsPriced()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        IReadOnlyList<ThrownOffer> stall = Counter(dojo, id).Thrown;

        int yumi = stall.Single(t => t.Thrown.Name == "Yumi").Price;
        int shuriken = stall.Single(t => t.Thrown.Name == "Shuriken").Price;
        int poisoned = stall.Single(t => t.Thrown.Poison > 0).Price;

        Assert.True(yumi > shuriken);

        // Half the quiver, but the poison is paid for: it lands above the clean star it is made from.
        Assert.True(poisoned > shuriken);
    }

    /// <summary>What he carries is not sold to him again, and the counter says what it is.</summary>
    [Fact]
    public void TheImplementHeCarriesIsNotSoldTwice()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        Warrior warrior = dojo.Roster.Find(id)!.Warrior;

        Assert.True(dojo.Quartermaster.EquipThrown(dojo, warrior, ThrownWeapon.Yumi()));

        CounterCard card = Counter(dojo, id);
        Assert.Equal("Yumi", card.Carrying?.Name);
        Assert.Equal(
            CounterRefusal.AlreadyCarried,
            card.Thrown.Single(t => t.Thrown.Name == "Yumi").Refusal);
    }

    /// <summary>The rack sells every weapon a hand can hold, and never the bare fists.</summary>
    [Fact]
    public void TheRackSellsEveryWeaponButTheFists()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        RackCard rack = Rack(dojo, id);

        Assert.Equal(EquipmentCatalogue.Rack.Count, rack.Offers.Count);
        Assert.DoesNotContain(rack.Offers, o => o.Weapon.Name == Weapon.Fists().Name);
        Assert.Contains(rack.Offers, o => o.Weapon.Name == "Jitte");
        Assert.Contains(rack.Offers, o => o.Weapon.Name == "Poisoned tantō");
    }

    /// <summary>
    /// The two class implements are priced above the output they deal, and the katana above the knife.
    /// </summary>
    /// <remarks>
    /// This is the whole shape of <see cref="EconomyTuning.WeaponGoldPerDamageRate"/>: the rack reads
    /// the rate and then pays for what the rate cannot see — the grip and the dose.
    /// </remarks>
    [Fact]
    public void TheRackPricesTheGripAndTheDose()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        Quartermaster shop = dojo.Quartermaster;

        int jitte = shop.WeaponPrice(Weapon.Jitte());
        int sai = shop.WeaponPrice(Weapon.Sai());
        int tanto = shop.WeaponPrice(Weapon.Tanto());
        int poisoned = shop.WeaponPrice(Weapon.PoisonedTanto());

        // The sai deals less than the jitte every second and still costs more: the grip is the price.
        Assert.True(sai > jitte);

        // Half the steel of the clean knife, and dearer than it — the dose is paid for, not given away.
        Assert.True(poisoned > tanto);

        // Neither implement is the bargain of the season: both stand above the knife they outdamage.
        Assert.True(jitte > tanto);
    }

    /// <summary>The rack says which class an implement belongs to, and whether this hand was taught it.</summary>
    [Fact]
    public void TheRackSaysWhoWasTaughtToHoldIt()
    {
        (DojoState dojo, WarriorId id) = Dojo();

        WeaponOffer untaught = Rack(dojo, id).Offers.Single(o => o.Weapon.Name == "Jitte");
        Assert.Equal(WarriorClass.Torite, untaught.Class);
        Assert.False(untaught.Trained);
        Assert.True(untaught.Wasted);

        dojo.Roster.Find(id)!.Warrior.Class = WarriorClass.Torite;

        WeaponOffer taught = Rack(dojo, id).Offers.Single(o => o.Weapon.Name == "Jitte");
        Assert.True(taught.Trained);
        Assert.False(taught.Wasted);

        // The rack sells it either way — being untaught is a warning, not a refusal (docs/GDD.md §4).
        Assert.True(untaught.CanBuy);
    }

    /// <summary>The weapon in his hand is not sold to him again.</summary>
    [Fact]
    public void TheWeaponHeCarriesIsNotSoldTwice()
    {
        (DojoState dojo, WarriorId id) = Dojo();

        Assert.Equal(
            CounterRefusal.AlreadyCarried,
            Rack(dojo, id).Offers.Single(o => o.Weapon.Name == "Katana").Refusal);

        Assert.False(dojo.Quartermaster.EquipWeapon(dojo, dojo.Roster.Find(id)!.Warrior, Weapon.Katana()));
    }

    /// <summary>An empty purse refuses the rack, and the refusal is the one the core would give.</summary>
    [Fact]
    public void AnEmptyPurseRefusesTheRack()
    {
        (DojoState dojo, WarriorId id) = Dojo(gold: 10);

        WeaponOffer offer = Rack(dojo, id).Offers.Single(o => o.Weapon.Name == "Nodachi");
        Assert.Equal(CounterRefusal.TooExpensive, offer.Refusal);
        Assert.False(dojo.Quartermaster.EquipWeapon(dojo, dojo.Roster.Find(id)!.Warrior, Weapon.Nodachi()));
        Assert.Equal(10, dojo.Resources.Gold);
    }

    /// <summary>
    /// Arming him charges the printed price, changes the hand, and leaves the mastery behind.
    /// </summary>
    /// <remarks>
    /// The mastery is kept per weapon name, so what he built on the katana is still there when he buys
    /// a katana again — the change costs him the mastery <b>while he carries the other thing</b>.
    /// </remarks>
    [Fact]
    public void ArmingHimChargesThePrintedPriceAndCostsHimTheMastery()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        Warrior warrior = dojo.Roster.Find(id)!.Warrior;
        warrior.Mastery.Set("Katana", 0.8);
        warrior.Weapon = Weapon.Katana();
        Assert.Equal(0.8, warrior.WeaponSkill, 3);

        WeaponOffer offer = Rack(dojo, id).Offers.Single(o => o.Weapon.Name == "Jitte");
        int before = dojo.Resources.Gold;

        Assert.True(dojo.Quartermaster.EquipWeapon(dojo, warrior, offer.Weapon));
        Assert.Equal(before - offer.Price, dojo.Resources.Gold);
        Assert.Equal("Jitte", warrior.Weapon.Name);
        Assert.Equal(0, warrior.WeaponSkill, 3);

        Assert.True(dojo.Quartermaster.EquipWeapon(dojo, warrior, Weapon.Katana()));
        Assert.Equal(0.8, warrior.WeaponSkill, 3);
    }

    /// <summary>Buying deducts the price the counter printed — one number, not two.</summary>
    [Fact]
    public void TheCounterChargesWhatItPrinted()
    {
        (DojoState dojo, WarriorId id) = Dojo();
        ThrownOffer offer = Counter(dojo, id).Thrown.Single(t => t.Thrown.Name == "Yumi");
        int before = dojo.Resources.Gold;

        Assert.True(dojo.Quartermaster.EquipThrown(dojo, dojo.Roster.Find(id)!.Warrior, offer.Thrown));
        Assert.Equal(before - offer.Price, dojo.Resources.Gold);
    }
}
