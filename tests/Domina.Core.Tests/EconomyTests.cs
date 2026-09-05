using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Kasa, ambar ve fiyatlar (GDD §11, Açık Karar #5). Korunan üç kural: kasa eksiye
/// düşmez, onarım her zaman yenisinden ucuzdur, ve aç kalan savaşçının bedeli
/// <b>zaman</b>dır — ölüm değil.
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
    /// Kuralın kendisi: onarımın puan başına fiyatı yeninin altında kalmazsa onarım
    /// diye bir karar kalmaz, herkes parçayı dağılana kadar kullanır.
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

        // Havuzundan fazlası ödenmez: dağılmak üzere olan parça yenisinden pahalıya onarılmaz.
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

    /// <summary>Yıpranma parçaya aittir: yeni parça dağılanın defterini devralmaz.</summary>
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
        Quartermaster market = new(new EconomyTuning { VictoryGoldPerEnemyHealth = 2 });
        BattleSetup setup = new(
            [new Warrior(new WarriorId(1), "Kenji", WarriorStats.Recruit(), Weapon.Katana())],
            [new Warrior(new WarriorId(101), "Oni", WarriorStats.Recruit() with { MaxHealth = 150 }, Weapon.Tetsubo())]);

        Assert.Equal(300, market.PromisedReward(setup));
        Assert.Equal(300, market.RewardFor(setup, BattleOutcome.PlayerVictory));

        // GDD §10: çekilmek o seferin ödülünü siler; bozgun da öyle.
        Assert.Equal(0, market.RewardFor(setup, BattleOutcome.PlayerWithdrawal));
        Assert.Equal(0, market.RewardFor(setup, BattleOutcome.PlayerWipe));
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
    /// Kıtlığın bedeli zaman: aç savaşçı o gün ne iyileşir ne antrenman yapar.
    /// Kimse ölmez — açlık geri dönüşsüz bir ceza değildir.
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

    /// <summary>Ambar yetmezse revirdeki önce doyar — yaralıyı aç bırakmak kıtlığı katmerlerdi.</summary>
    [Fact]
    public void TheInfirmaryEatsFirst()
    {
        // Tek kişilik yiyecek: iki savaşçıdan biri aç kalacak.
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
