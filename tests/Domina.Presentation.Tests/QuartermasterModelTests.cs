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
