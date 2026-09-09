using Domina.Core.Combat;
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
            return false;
        }

        state.Resources = state.Resources with { Gold = state.Resources.Gold - price };
        warrior.ArmorWear = warrior.ArmorWear.With(slot, 0);
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

        int price = PiecePrice(piece);
        if (price > state.Resources.Gold)
        {
            return false;
        }

        state.Resources = state.Resources with { Gold = state.Resources.Gold - price };
        warrior.Armor = warrior.Armor.With(slot, piece);
        warrior.ArmorWear = warrior.ArmorWear.With(slot, 0);
        return true;
    }

    /// <summary>
    /// Fills the store up to the level wanted; only <b>what is missing</b> is bought.
    /// </summary>
    /// <returns>The gold spent.</returns>
    public int Restock(DojoState state, Resources target)
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

        if (Economy.RecruitPrice > state.Resources.Gold)
        {
            return null;
        }

        RosterEntry entry = state.Roster.Recruit(name, stats, weapon, armor);
        state.Resources = state.Resources with { Gold = state.Resources.Gold - Economy.RecruitPrice };
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

        if (offer.Price > state.Resources.Gold)
        {
            return null;
        }

        string name = offer.Name;
        for (int suffix = 2; state.Roster.IsNameTaken(name); suffix++)
        {
            name = $"{offer.Name} {suffix}";
        }

        RosterEntry entry = state.Roster.Recruit(name, offer.Stats, weapon, armor, offer.Talent);
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
