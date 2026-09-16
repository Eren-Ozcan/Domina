using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>Why the counter will not sell this.</summary>
public enum CounterRefusal
{
    /// <summary>It will.</summary>
    None,

    /// <summary>The treasury cannot cover it.</summary>
    TooExpensive,

    /// <summary>He is already carrying it.</summary>
    AlreadyCarried,

    /// <summary>It is not bought off a stall: it needs the plate works and a smith of one's own.</summary>
    NeedsSmith,

    /// <summary>The sword forge and its smith; or the blade has already been reforged once.</summary>
    NeedsForge,

    /// <summary>There is nothing to do — an unworn slot has no wear to erase.</summary>
    Nothing,
}

/// <summary>One piece the stall offers for one region.</summary>
/// <param name="Slot">The region it is worn on.</param>
/// <param name="Piece">The piece itself — the command hands it straight back to the quartermaster.</param>
/// <param name="Price">What it costs new.</param>
/// <param name="Refusal">Why it cannot be bought today, or <see cref="CounterRefusal.None"/>.</param>
public readonly record struct ArmorOffer(
    HitLocation Slot,
    ArmorPiece Piece,
    int Price,
    CounterRefusal Refusal)
{
    public bool CanBuy => Refusal == CounterRefusal.None;
}

/// <summary>One region of the kit as the counter reads it.</summary>
/// <param name="Slot">The region.</param>
/// <param name="Worn">What he is wearing there — <see cref="ArmorPiece.Bare"/> if nothing.</param>
/// <param name="Wear">The damage that piece has absorbed.</param>
/// <param name="RepairPrice">What erasing that wear costs; 0 if there is nothing to erase.</param>
/// <param name="RepairRefusal">Why the repair cannot be done today.</param>
/// <param name="Offers">What the stall would fit in its place.</param>
public readonly record struct ArmorSlotRow(
    HitLocation Slot,
    ArmorPiece Worn,
    double Wear,
    int RepairPrice,
    CounterRefusal RepairRefusal,
    IReadOnlyList<ArmorOffer> Offers)
{
    public bool CanRepair => RepairRefusal == CounterRefusal.None;

    /// <summary>How far through its life the piece is, 0-1 — the bar the screen draws.</summary>
    public double WornShare => Worn.Durability <= 0 ? 0 : Math.Clamp(Wear / Worn.Durability, 0, 1);
}

/// <summary>One implement on the throwing stall.</summary>
/// <param name="Thrown">The implement — handed straight back to the quartermaster.</param>
/// <param name="Price">What it costs.</param>
/// <param name="Refusal">Why it cannot be bought today.</param>
/// <param name="ClassGated">
/// Is this the implement a hand has to be taught (<see cref="ThrownWeapon.UntrainedShare"/> below 1)?
/// The screen says so, because a bow bought for a classless warrior is gold spent on a penalty.
/// </param>
public readonly record struct ThrownOffer(
    ThrownWeapon Thrown,
    int Price,
    CounterRefusal Refusal,
    bool ClassGated)
{
    public bool CanBuy => Refusal == CounterRefusal.None;
}

/// <summary>One melee weapon on the rack.</summary>
/// <param name="Weapon">The weapon — handed straight back to the quartermaster.</param>
/// <param name="Price">What the rack asks.</param>
/// <param name="Refusal">Why it cannot be bought today.</param>
/// <param name="Class">
/// The class whose implement this is, or <c>null</c> for a weapon anybody carries. The rack sells it
/// either way; the row says so, because it is the difference between a jitte bought for a torite and
/// the same jitte bought for a man who was never taught to hold one (docs/GDD.md §4).
/// </param>
/// <param name="Trained">Is the man it is laid out for of that class?</param>
public readonly record struct WeaponOffer(
    Weapon Weapon,
    int Price,
    CounterRefusal Refusal,
    WarriorClass? Class,
    bool Trained)
{
    public bool CanBuy => Refusal == CounterRefusal.None;

    /// <summary>Is this an implement the hand has to be taught, in a hand that was not?</summary>
    public bool Wasted => Class is not null && !Trained;
}

/// <summary>The rack as it stands for one warrior.</summary>
/// <param name="Id">The warrior it is laid out for.</param>
/// <param name="Gold">The treasury, so the screen prints the number the prices were judged by.</param>
/// <param name="Carrying">The weapon in his hand today.</param>
/// <param name="Mastery">What he has learned on it — what changing weapon costs him.</param>
/// <param name="Offers">Everything the rack would sell him.</param>
public sealed record RackCard(
    WarriorId Id,
    int Gold,
    Weapon Carrying,
    double Mastery,
    IReadOnlyList<WeaponOffer> Offers);

/// <summary>The counter as it stands for one warrior.</summary>
/// <param name="Id">The warrior it is laid out for.</param>
/// <param name="Gold">The treasury, so the screen can print the same number the prices are judged by.</param>
/// <param name="Slots">The six regions.</param>
/// <param name="FullRepairPrice">What erasing all of his wear would cost.</param>
/// <param name="ForgePrice">What reforging his weapon would cost.</param>
/// <param name="ForgeRefusal">Why it cannot be reforged today.</param>
/// <param name="WeaponName">The blade in his hand, so the forge row can name what it would reforge.</param>
/// <param name="Carrying">What is in his throwing slot; <c>null</c> if the slot is empty.</param>
/// <param name="Thrown">The throwing stall.</param>
public sealed record CounterCard(
    WarriorId Id,
    int Gold,
    IReadOnlyList<ArmorSlotRow> Slots,
    int FullRepairPrice,
    int ForgePrice,
    CounterRefusal ForgeRefusal,
    string WeaponName,
    ThrownWeapon? Carrying,
    IReadOnlyList<ThrownOffer> Thrown)
{
    public bool CanForge => ForgeRefusal == CounterRefusal.None;
}

/// <summary>
/// The quartermaster's counter: what one warrior's kit costs to improve, mend or replace.
/// </summary>
/// <remarks>
/// <para>
/// The core has had the counter since the economy landed — <see cref="Quartermaster"/> fits pieces,
/// erases wear, reforges blades and now sells the throwing slot — but no screen ever called any of it,
/// so the only side shopping was the measuring policy. This model is the missing half: it says what
/// the counter would do and why it would refuse, and the screen only draws it.
/// </para>
/// <para>
/// It decides <b>nothing</b> the core does not. Every price comes from the quartermaster and every
/// gate is the core's own (the plate works and its smith, the sword forge, a blade already reforged),
/// so a row that says "buy" and a purchase that is refused can never disagree.
/// </para>
/// </remarks>
public static class QuartermasterModel
{
    /// <summary>Lays the counter out for one warrior.</summary>
    public static CounterCard? Describe(DojoState dojo, WarriorId id)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        RosterEntry? entry = dojo.Roster.Find(id);
        if (entry is null || !entry.Warrior.IsAlive)
        {
            return null;
        }

        Warrior warrior = entry.Warrior;
        Quartermaster shop = dojo.Quartermaster;
        int gold = dojo.Resources.Gold;
        bool smith = HasSmith(dojo, SchoolNodeId.PlateWorks);

        List<ArmorSlotRow> slots = [];
        foreach (HitLocation slot in ArmorSlots.All)
        {
            ArmorPiece worn = warrior.Armor.At(slot);
            int repair = shop.RepairPrice(warrior, slot);

            slots.Add(new ArmorSlotRow(
                slot,
                worn,
                warrior.ArmorWear.At(slot),
                repair,
                repair <= 0 ? CounterRefusal.Nothing
                    : repair > gold ? CounterRefusal.TooExpensive
                    : CounterRefusal.None,
                [.. ArmorPiece.For(slot).Select(piece => Offer(shop, slot, piece, worn, gold, smith))]));
        }

        return new CounterCard(
            id,
            gold,
            slots,
            shop.FullRepairPrice(warrior),
            shop.ForgePrice(warrior),
            ForgeRefusal(dojo, warrior, shop, gold),
            warrior.Weapon.Name,
            warrior.Thrown,
            [.. ThrownWeapon.Catalogue.Select(thrown => Offer(shop, thrown, warrior, gold))]);
    }

    /// <summary>Lays the rack out for one warrior.</summary>
    /// <remarks>
    /// It is its own card rather than another field on <see cref="CounterCard"/> because it is read on
    /// another screen: the rack hangs on the man's own page (the roster's detail column), beside the
    /// charms, where the weapon he carries is already printed. The armoury's counter is about his
    /// <b>kit</b> — six regions, their wear and the throwing slot — and arming him is a decision about
    /// the man (docs/GDD.md §10, Open Decision #21).
    /// </remarks>
    public static RackCard? Rack(DojoState dojo, WarriorId id)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        RosterEntry? entry = dojo.Roster.Find(id);
        if (entry is null || !entry.Warrior.IsAlive)
        {
            return null;
        }

        Warrior warrior = entry.Warrior;
        Quartermaster shop = dojo.Quartermaster;
        int gold = dojo.Resources.Gold;

        return new RackCard(
            id,
            gold,
            warrior.Weapon,
            warrior.WeaponSkill,
            [.. EquipmentCatalogue.Rack.Select(weapon => Offer(shop, weapon, warrior, gold))]);
    }

    private static WeaponOffer Offer(Quartermaster shop, Weapon weapon, Warrior warrior, int gold)
    {
        int price = shop.WeaponPrice(weapon);

        CounterRefusal refusal = warrior.Weapon.Name == weapon.Name ? CounterRefusal.AlreadyCarried
            : price > gold ? CounterRefusal.TooExpensive
            : CounterRefusal.None;

        WarriorClass? taught = ImplementOf(weapon);

        return new WeaponOffer(weapon, price, refusal, taught, taught is not null && warrior.Class == taught);
    }

    /// <summary>
    /// The class this weapon is the implement of, or <c>null</c> if any hand carries it as well as any other.
    /// </summary>
    /// <remarks>
    /// It reads the weapon's own fields rather than a table: the catch factor is what the torite is
    /// taught and the dose is what the dokushi is taught, so a weapon added later is classed by what it
    /// is and not by being remembered here.
    /// </remarks>
    private static WarriorClass? ImplementOf(Weapon weapon) =>
        weapon.CanCatch ? WarriorClass.Torite
        : weapon.IsPoisoned ? WarriorClass.Dokushi
        : null;

    private static ArmorOffer Offer(
        Quartermaster shop,
        HitLocation slot,
        ArmorPiece piece,
        ArmorPiece worn,
        int gold,
        bool smith)
    {
        int price = shop.PiecePrice(piece);

        CounterRefusal refusal = piece == worn ? CounterRefusal.AlreadyCarried
            : piece.NeedsSmith && !smith ? CounterRefusal.NeedsSmith
            : price > gold ? CounterRefusal.TooExpensive
            : CounterRefusal.None;

        return new ArmorOffer(slot, piece, price, refusal);
    }

    private static ThrownOffer Offer(Quartermaster shop, ThrownWeapon thrown, Warrior warrior, int gold)
    {
        int price = shop.ThrownPrice(thrown);

        CounterRefusal refusal = warrior.Thrown?.Name == thrown.Name ? CounterRefusal.AlreadyCarried
            : price > gold ? CounterRefusal.TooExpensive
            : CounterRefusal.None;

        return new ThrownOffer(thrown, price, refusal, thrown.UntrainedShare < 1);
    }

    private static CounterRefusal ForgeRefusal(
        DojoState dojo,
        Warrior warrior,
        Quartermaster shop,
        int gold)
    {
        if (!HasSmith(dojo, SchoolNodeId.SwordForge) || Weapon.IsForged(warrior.Weapon))
        {
            return CounterRefusal.NeedsForge;
        }

        return shop.ForgePrice(warrior) > gold ? CounterRefusal.TooExpensive : CounterRefusal.None;
    }

    /// <summary>
    /// A gate needs the building <b>and</b> the man in it.
    /// </summary>
    /// <remarks>
    /// Half-efficiency does not apply to either of the smith's two gates: a building with nobody in it
    /// fits half of no plate (docs/GDD.md §10).
    /// </remarks>
    private static bool HasSmith(DojoState dojo, SchoolNodeId node) =>
        dojo.School.Has(node) && dojo.Staff.Has(StaffRole.Smith);
}
