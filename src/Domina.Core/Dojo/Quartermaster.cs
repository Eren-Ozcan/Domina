using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>Kasayla ambarın arasındaki tek kapı: fiyat sorar, alışverişi yapar.</summary>
/// <remarks>
/// <para>
/// Fiyat hesabı ile <b>satın alma</b> ayrı tutulur: fiyat sorusu durumu değiştirmez,
/// böylece arayüz "bu onarım kaç eder" diye kasaya dokunmadan sorabilir. Parası
/// yetmeyen alışveriş <b>yapılmaz</b> — kasa eksiye düşmez, borç diye bir kalem yok.
/// </para>
/// <para>
/// Fiyatların hepsi <see cref="EconomyTuning"/>'den gelir; burada tek bir sabit yoktur.
/// </para>
/// </remarks>
public sealed class Quartermaster(EconomyTuning? economy = null)
{
    public EconomyTuning Economy { get; } = economy ?? new EconomyTuning();

    /// <summary>Sıfırdan bir zırh parçasının fiyatı.</summary>
    public int PiecePrice(ArmorPiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        return (int)Math.Ceiling(piece.Durability * Economy.ArmorGoldPerDurability);
    }

    /// <summary>Bir yuvadaki yıpranmayı silmenin fiyatı.</summary>
    /// <remarks>
    /// Yıpranmamış ya da boş yuva bedavadır; parçanın havuzundan fazlası ödenmez —
    /// dağılmak üzere olan parça, yenisinden pahalıya onarılmaz.
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

    /// <summary>Kuşamın tamamını yeni gibi yapmanın fiyatı.</summary>
    public int FullRepairPrice(Warrior warrior)
    {
        ArgumentNullException.ThrowIfNull(warrior);
        return ArmorSlots.All.Sum(slot => RepairPrice(warrior, slot));
    }

    /// <summary>Bir yuvayı onarır. Parası yetmezse hiçbir şey olmaz.</summary>
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
    /// Yuvaya yeni bir parça takar; eskisi <b>gider</b>, geri satılmaz.
    /// </summary>
    /// <remarks>
    /// Yeni parça takıldığında o yuvanın yıpranma sayacı sıfırlanır: yıpranma parçaya
    /// aittir, yuvaya değil (bkz. <see cref="BattleAftermath"/>).
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
    /// Ambarı istenen seviyeye kadar doldurur; yalnızca <b>eksik olan</b> kadarı alınır.
    /// </summary>
    /// <returns>Harcanan altın.</returns>
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

    /// <summary>Kadroya yeni savaşçı alır. Parası yetmezse kimse gelmez.</summary>
    /// <remarks>
    /// Taban fiyattan, taban statlarla alım. Pazardan seçerek almak için
    /// <see cref="Hire(DojoState, RecruitOffer, Weapon?, Armor?)"/> kullanılır (fiyat adayın kendi fiyatıdır).
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
    /// Pazardaki bir adayı satın alır — kendi statları, kendi yeteneği ve kendi fiyatıyla.
    /// </summary>
    /// <remarks>
    /// Adın kadroda kullanılıyor olması alımı <b>engellemez</b>: aynı ad iki canlıda
    /// duramaz (GDD §6), o yüzden ada bir ayırt edici eklenir. Aksi hâlde pazarın çektiği
    /// isim yüzünden iyi bir aday satın alınamazdı.
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

    /// <summary>Karşılaşmanın söz verdiği ödül — girmeden önce okunabilir.</summary>
    /// <remarks>
    /// Düşmanın <b>ham</b> canından hesaplanır: karşılaşma ne kadar ağırsa o kadar öder.
    /// Dövüşün nasıl geçtiği ödülü değiştirmez; kazanmak ya da kazanmamak değiştirir.
    /// </remarks>
    public int PromisedReward(BattleSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        double health = setup.EnemySide.Sum(w => w.EffectiveStats.MaxHealth);
        return (int)Math.Round(health * Economy.VictoryGoldPerEnemyHealth);
    }

    /// <summary>Dövüşün kasaya yazdığı altın.</summary>
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

/// <summary>Zırhın altı yuvası, tek bir yerde.</summary>
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
