using Domina.Core.Combat;
using Domina.Core.Dojo.Journal;
using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>The only door between the treasury and the store: it quotes prices and does the shopping.</summary>
/// <remarks>
/// <para>
/// Price calculation and <b>buying</b> are kept apart: asking a price does not change state, so the
/// interface can ask "what does this repair cost" without touching the treasury. Shopping that cannot
/// be afforded is <b>not done</b> — the treasury does not go negative, there is no such item as debt.
/// </para>
/// <para>
/// All the prices come from <see cref="EconomyTuning"/>; there is not a single constant here.
/// </para>
/// </remarks>
public sealed class Quartermaster(EconomyTuning? economy = null)
{
    public EconomyTuning Economy { get; } = economy ?? new EconomyTuning();

    /// <summary>The price of an armour piece from scratch.</summary>
    public int PiecePrice(ArmorPiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        return (int)Math.Ceiling(piece.Durability * Economy.ArmorGoldPerDurability);
    }

    /// <summary>The price of erasing the wear in one slot.</summary>
    /// <remarks>
    /// An unworn or empty slot is free; nothing above the piece's pool is paid — a piece about to break
    /// is not repaired for more than a new one costs.
    /// </remarks>
    public int RepairPrice(Warrior warrior, HitLocation slot)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        ArmorPiece piece = warrior.Armor.At(slot);
        if (!piece.IsWorn || piece.Durability <= 0)
        {
            return 0;
        }

        double wear = Math.Clamp(warrior.ArmorWear.At(slot), 0, piece.Durability);
        return (int)Math.Ceiling(wear * Economy.RepairGoldPerWear);
    }

    /// <summary>The price of making the whole kit as good as new.</summary>
    public int FullRepairPrice(Warrior warrior)
    {
        ArgumentNullException.ThrowIfNull(warrior);
        return ArmorSlots.All.Sum(slot => RepairPrice(warrior, slot));
    }

    /// <summary>Repairs one slot. If it cannot be afforded, nothing happens.</summary>
    public bool Repair(DojoState state, Warrior warrior, HitLocation slot)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(warrior);

        int price = RepairPrice(warrior, slot);
        if (price > state.Resources.Gold)
        {
            state.Record(
                MoveKind.Repair,
                false,
                MoveArg.Of("warrior", warrior.Id.Value),
                MoveArg.Of("slot", slot),
                MoveArg.Of("cost", price));
            return false;
        }

        state.Resources = state.Resources with { Gold = state.Resources.Gold - price };
        warrior.ArmorWear = warrior.ArmorWear.With(slot, 0);
        state.Record(
            MoveKind.Repair,
            true,
            MoveArg.Of("warrior", warrior.Id.Value),
            MoveArg.Of("slot", slot),
            MoveArg.Of("cost", price),
            MoveArg.Of("name", warrior.Name));
        return true;
    }

    /// <summary>
    /// Fits a new piece to the slot; the old one <b>is gone</b>, it is not sold back.
    /// </summary>
    /// <remarks>
    /// When a new piece is fitted, that slot's wear counter is reset: wear belongs to the piece, not to
    /// the slot (see <see cref="BattleAftermath"/>).
    /// </remarks>
    public bool Equip(DojoState state, Warrior warrior, HitLocation slot, ArmorPiece piece)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(warrior);
        ArgumentNullException.ThrowIfNull(piece);

        // The ō-yoroi gate (docs/GDD.md §10): full plate is not bought off a stall, it is fitted by a
        // smith of one's own in a plate works. Both are needed — the building is the equipment, the man
        // is the fitting — which is why it is a gate and not a share.
        if (piece.NeedsSmith
            && (!state.School.Has(SchoolNodeId.PlateWorks) || !state.Staff.Has(StaffRole.Smith)))
        {
            state.Record(
                MoveKind.EquipArmor,
                false,
                MoveArg.Of("warrior", warrior.Id.Value),
                MoveArg.Of("slot", slot),
                MoveArg.Of("piece", piece.Name),
                MoveArg.Of("refused", "the plate works or the smith is missing"));
            return false;
        }

        int price = PiecePrice(piece);
        if (price > state.Resources.Gold)
        {
            state.Record(
                MoveKind.EquipArmor,
                false,
                MoveArg.Of("warrior", warrior.Id.Value),
                MoveArg.Of("slot", slot),
                MoveArg.Of("piece", piece.Name),
                MoveArg.Of("cost", price));
            return false;
        }

        state.Resources = state.Resources with { Gold = state.Resources.Gold - price };
        warrior.Armor = warrior.Armor.With(slot, piece);
        warrior.ArmorWear = warrior.ArmorWear.With(slot, 0);
        state.Record(
            MoveKind.EquipArmor,
            true,
            MoveArg.Of("warrior", warrior.Id.Value),
            MoveArg.Of("slot", slot),
            MoveArg.Of("piece", piece.Name),
            MoveArg.Of("cost", price),
            MoveArg.Of("name", warrior.Name));
        return true;
    }

    /// <summary>The price of a thrown implement.</summary>
    /// <remarks>
    /// Priced on the quiver — damage times ammunition — with a share added for a dose of poison. See
    /// <see cref="EconomyTuning.ThrownGoldPerDamage"/> for why range is deliberately not in the price.
    /// </remarks>
    public int ThrownPrice(ThrownWeapon thrown)
    {
        ArgumentNullException.ThrowIfNull(thrown);

        double quiver = Math.Max(0, thrown.Damage) * Math.Max(0, thrown.Ammo);
        double poison = 1 + (Math.Max(0, thrown.Poison) * Math.Max(0, Economy.ThrownPoisonPremium));

        return (int)Math.Ceiling(quiver * Economy.ThrownGoldPerDamage * poison);
    }

    /// <summary>
    /// Puts a thrown implement in the warrior's throwing slot; what he carried is <b>gone</b>.
    /// </summary>
    /// <remarks>
    /// The same rule the armour counter works by: nothing is sold back, so swapping a yumi for a
    /// handful of stars is a decision and not a shuffle. The bow is sold to anybody — an untrained hand
    /// wastes it, which is the class's own business (docs/GDD.md §4).
    /// </remarks>
    /// <returns><c>true</c> if it was bought and carried.</returns>
    public bool EquipThrown(DojoState state, Warrior warrior, ThrownWeapon thrown)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(warrior);
        ArgumentNullException.ThrowIfNull(thrown);

        int price = ThrownPrice(thrown);
        if (price > state.Resources.Gold)
        {
            state.Record(
                MoveKind.EquipThrown,
                false,
                MoveArg.Of("warrior", warrior.Id.Value),
                MoveArg.Of("thrown", thrown.Name),
                MoveArg.Of("cost", price));
            return false;
        }

        state.Resources = state.Resources with { Gold = state.Resources.Gold - price };
        warrior.Thrown = thrown;
        state.Record(
            MoveKind.EquipThrown,
            true,
            MoveArg.Of("warrior", warrior.Id.Value),
            MoveArg.Of("thrown", thrown.Name),
            MoveArg.Of("cost", price),
            MoveArg.Of("name", warrior.Name));
        return true;
    }

    /// <summary>What reforging this warrior's weapon would cost.</summary>
    public int ForgePrice(Warrior warrior)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        return (int)Math.Ceiling(warrior.Weapon.Damage * Math.Max(0, Economy.ForgeGoldPerDamage));
    }

    /// <summary>
    /// Reforges the warrior's weapon into a better one of its own kind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The equipment branch's third tier, and the only thing in the game that improves a weapon rather
    /// than replacing it. It needs the sword forge <b>and</b> the smith, like the plate gate: a
    /// building with nobody in it forges nothing.
    /// </para>
    /// <para>
    /// The new weapon carries a new name, so the mastery the warrior built on the old one does not come
    /// with it (docs/GDD.md §10). That is deliberate and it is the real price: a veteran pays for the
    /// better blade with the years he spent learning the old one.
    /// </para>
    /// </remarks>
    /// <returns><c>true</c> if it was reforged.</returns>
    public bool Forge(DojoState state, Warrior warrior)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(warrior);

        if (!state.School.Has(SchoolNodeId.SwordForge)
            || !state.Staff.Has(StaffRole.Smith)
            || Weapon.IsForged(warrior.Weapon))
        {
            state.Record(
                MoveKind.ForgeWeapon,
                false,
                MoveArg.Of("warrior", warrior.Id.Value),
                MoveArg.Of("weapon", warrior.Weapon.Name));
            return false;
        }

        int price = ForgePrice(warrior);
        if (price > state.Resources.Gold)
        {
            state.Record(
                MoveKind.ForgeWeapon,
                false,
                MoveArg.Of("warrior", warrior.Id.Value),
                MoveArg.Of("weapon", warrior.Weapon.Name),
                MoveArg.Of("cost", price));
            return false;
        }

        state.Resources = state.Resources with { Gold = state.Resources.Gold - price };
        warrior.Weapon = Weapon.Forged(warrior.Weapon, state.School.Tuning.ForgedWeaponDamage);
        state.Record(
            MoveKind.ForgeWeapon,
            true,
            MoveArg.Of("warrior", warrior.Id.Value),
            MoveArg.Of("weapon", warrior.Weapon.Name),
            MoveArg.Of("cost", price),
            MoveArg.Of("name", warrior.Name));
        return true;
    }

    /// <summary>
    /// Fills the store up to the level wanted; only <b>what is missing</b> is bought.
    /// </summary>
    /// <returns>The gold spent.</returns>
    /// <param name="state">The dojo whose store is being filled.</param>
    /// <param name="target">The level the store should be brought up to.</param>
    /// <param name="journal">
    /// Whether this counts as a move of its own. The day's own closing shops through here
    /// (<see cref="DojoState.AdvanceDay"/>) and passes <c>false</c>: that shopping is part of the day
    /// and is written into the day's bill, and recording it twice would put a move inside a move and
    /// leave a replay shopping on its own.
    /// </param>
    public int Restock(DojoState state, Resources target, bool journal = true)
    {
        ArgumentNullException.ThrowIfNull(state);

        Resources have = state.Resources;
        int food = Math.Max(0, target.Food - have.Food);
        int water = Math.Max(0, target.Water - have.Water);
        int medicine = Math.Max(0, target.Medicine - have.Medicine);

        int spent = 0;
        (int Bought, int Spent) f = Afford(have.Gold, food, Economy.FoodPrice);
        spent += f.Spent;
        (int Bought, int Spent) w = Afford(have.Gold - spent, water, Economy.WaterPrice);
        spent += w.Spent;
        (int Bought, int Spent) m = Afford(have.Gold - spent, medicine, Economy.MedicinePrice);
        spent += m.Spent;

        state.Resources = have with
        {
            Gold = have.Gold - spent,
            Food = have.Food + f.Bought,
            Water = have.Water + w.Bought,
            Medicine = have.Medicine + m.Bought,
        };

        // The level wanted is the argument, because that is what was asked for; what actually arrived
        // is an observation, because the purse is what decides it.
        if (!journal)
        {
            return spent;
        }

        state.Record(
            MoveKind.Restock,
            spent > 0,
            MoveArg.Of("wantFood", target.Food),
            MoveArg.Of("wantWater", target.Water),
            MoveArg.Of("wantMedicine", target.Medicine),
            MoveArg.Of("boughtFood", f.Bought),
            MoveArg.Of("boughtWater", w.Bought),
            MoveArg.Of("boughtMedicine", m.Bought),
            MoveArg.Of("cost", spent));

        return spent;
    }

    /// <summary>Hires a new warrior onto the roster. If it cannot be afforded, nobody arrives.</summary>
    /// <remarks>
    /// A purchase at the base price with base stats. To pick from the market, use
    /// <see cref="Hire(DojoState, RecruitOffer, Weapon?, Armor?)"/> (the price is the candidate's own).
    /// </remarks>
    public RosterEntry? Hire(DojoState state, string name, WarriorStats? stats = null, Weapon? weapon = null, Armor? armor = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Economy.RecruitPrice > state.Resources.Gold || !state.HasRoomForAnother)
        {
            state.Record(MoveKind.Hire, false, MoveArg.Of("name", name));
            return null;
        }

        RosterEntry entry = state.Roster.Recruit(name, stats, weapon, armor);
        state.Resources = state.Resources with { Gold = state.Resources.Gold - Economy.RecruitPrice };
        state.Record(
            MoveKind.Hire,
            true,
            MoveArg.Of("name", name),
            MoveArg.Of("weapon", weapon?.Name),
            MoveArg.Of("armor", armor?.Name),
            MoveArg.Of("cost", Economy.RecruitPrice),
            MoveArg.Of("warrior", entry.Id.Value));
        return entry;
    }

    /// <summary>
    /// Buys a candidate from the market — with his own stats, his own talent and his own price.
    /// </summary>
    /// <remarks>
    /// A name already in use on the roster does <b>not</b> block the purchase: the same name cannot
    /// stand on two living warriors (GDD §6), so a distinguishing suffix is added. Otherwise a good
    /// candidate could not be bought because of the name the market drew.
    /// </remarks>
    public static RosterEntry? Hire(DojoState state, RecruitOffer offer, Weapon? weapon = null, Armor? armor = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(offer);

        // A man with nowhere to sleep is not bought: the quarters are the ceiling, and it is bought at
        // the school like everything else that lasts (docs/GDD.md §10).
        if (offer.Price > state.Resources.Gold || !state.HasRoomForAnother)
        {
            return null;
        }

        string name = offer.Name;
        for (int suffix = 2; state.Roster.IsNameTaken(name); suffix++)
        {
            name = $"{offer.Name} {suffix}";
        }

        RosterEntry entry = state.Roster.Recruit(name, offer.Stats, weapon, armor, offer.Talent);

        // The class comes with the man: that is what the higher price bought, and it is what makes the
        // stall a shortcut past the hall rather than a copy of it.
        entry.Warrior.Class = offer.Class;
        state.Resources = state.Resources with { Gold = state.Resources.Gold - offer.Price };
        return entry;
    }

    /// <summary>The reward the encounter promises — readable before going in.</summary>
    /// <remarks>
    /// It is computed from the enemy's <b>raw</b> health: the heavier the encounter, the more it pays.
    /// How the fight went does not change the reward; winning or not winning does. A heavier encounter
    /// pays <b>more</b> than its proportion — see
    /// <see cref="EconomyTuning.RiskPremium"/>.
    /// </remarks>
    public int PromisedReward(BattleSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        double health = setup.EnemySide.Sum(w => w.EffectiveStats.MaxHealth);
        return (int)Math.Round(health * Economy.VictoryGoldPerEnemyHealth * RiskFactor(health));
    }

    /// <summary>The premium multiplier laid on the encounter's weight.</summary>
    private double RiskFactor(double health)
    {
        double reference = Economy.RiskFreeEnemyHealth;
        if (Economy.RiskPremium <= 0 || reference <= 0 || health <= reference)
        {
            return 1;
        }

        return 1 + (Economy.RiskPremium * ((health / reference) - 1));
    }

    /// <summary>The gold the fight writes into the treasury.</summary>
    public int RewardFor(BattleSetup setup, BattleOutcome outcome) =>
        outcome == BattleOutcome.PlayerVictory ? PromisedReward(setup) : Economy.LostBattleGold;

    private static (int Bought, int Spent) Afford(int gold, int wanted, int price)
    {
        if (wanted <= 0 || price <= 0)
        {
            return (Math.Max(0, wanted), 0);
        }

        int bought = Math.Min(wanted, gold / price);
        return (bought, bought * price);
    }
}

/// <summary>Armour's six slots, in one place.</summary>
public static class ArmorSlots
{
    public static IReadOnlyList<HitLocation> All { get; } =
    [
        HitLocation.Head,
        HitLocation.Torso,
        HitLocation.SwordArm,
        HitLocation.OffArm,
        HitLocation.RightLeg,
        HitLocation.LeftLeg,
    ];
}
