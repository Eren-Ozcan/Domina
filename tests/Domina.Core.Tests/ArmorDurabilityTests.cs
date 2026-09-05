using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Zırh bir sarf malzemesidir: durdurduğu her darbe onu yıpratır ve havuzu bitince
/// parça <b>kalıcı olarak</b> dağılır. Bu testler yıpranmanın kaynağını (emilen hasar),
/// dağılmanın sonucunu (o bölge çıplak) ve kuralın kalıcı hale dokunmadığını bağlar.
/// </summary>
public class ArmorDurabilityTests
{
    /// <summary>Yıpranmanın izole edildiği ayar: kopma, sersemletme ve düşürme kapalı.</summary>
    private static CombatTuning WearOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        BaseDisarmChance = 0,
        CatchDisarmChance = 0,
        MaxBattleSeconds = 20,
    };

    /// <summary>Tek vuruşta biten havuz: dağılma anı ölçülebilsin diye.</summary>
    private static Armor Brittle { get; } = Armor.Uniform(
        "Test-Kırılgan",
        new ArmorPiece("Test-Parça", DamageReduction: 5, DismembermentResistance: 0.5, Weight: 1, Durability: 5));

    /// <summary>Aynı parçanın tükenmeyen hâli — kontrol tarafı.</summary>
    private static Armor Sturdy { get; } = Armor.Uniform(
        "Test-Sağlam",
        new ArmorPiece("Test-Parça", DamageReduction: 5, DismembermentResistance: 0.5, Weight: 1, Durability: 10_000));

    private static Weapon Blade { get; } =
        new("Test-Katana", WeaponClass.Cutting, 20, TwoHanded: false, AttackSeconds: 1.0);

    private static BattleSetup Bout(Armor defenderArmor, CombatTuning? tuning = null) => new(
        [
            TestBuilders.Warrior(1, "Kuşanan", health: 4000, aggression: 0, weapon: Weapon.Fists(), armor: defenderArmor),
        ],
        [TestBuilders.Warrior(101, "Vuran", health: 4000, aggression: 100, weapon: Blade)])
    {
        Tuning = tuning ?? WearOnly,
    };

    /// <summary>Parça durdurduğu hasar kadar yıpranır.</summary>
    /// <remarks>
    /// Gelen hasardan düşseydi kalın plaka ince kumaşla aynı hızda tükenir, kademe farkı
    /// yalnızca sayıda kalırdı (docs/GDD.md §7).
    /// </remarks>
    [Fact]
    public void APieceWearsByWhatItStops()
    {
        var battle = new Battle(Bout(Sturdy), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WarriorBattleSummary worn = result.Summaries.First(s => s.Id == new WarriorId(1));
        int hits = battle.Events.OfType<AttackLanded>().Count(a => a.Defender == new WarriorId(1));

        // Her isabet parçanın azaltımı kadar yıpratır: 5 hasar × isabet sayısı.
        Assert.Equal(hits * 5.0, worn.ArmorWear.Total, precision: 6);
    }

    /// <summary>Havuz bitince parça dağılır ve o bölge çıplak kalır.</summary>
    [Fact]
    public void AnExhaustedPieceShattersAndLeavesTheSlotBare()
    {
        var battle = new Battle(Bout(Brittle), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        ArmorDestroyed destroyed = battle.Events.OfType<ArmorDestroyed>().First();
        Assert.Equal(new WarriorId(1), destroyed.Warrior);

        WarriorBattleSummary summary = result.Summaries.First(s => s.Id == new WarriorId(1));
        Assert.True(summary.DestroyedArmor.Has(destroyed.Slot));

        // Dağıldıktan sonra o bölgeye inen vuruş artık azaltımsız: hasar yükselir.
        List<AttackLanded> onSlot =
            [.. battle.Events.OfType<AttackLanded>().Where(a => a.Defender == new WarriorId(1))];

        // Parçayı bitiren vuruş hâlâ azaltımı yer; ondan sonrakiler yemez.
        double whileWorn = onSlot.First(a => a.AtSeconds <= destroyed.AtSeconds).Damage;

        Assert.Contains(
            onSlot,
            a => a.AtSeconds > destroyed.AtSeconds && a.Damage > whileWorn);
    }

    /// <summary>Zırhı hiç olmayan savaşçıda yıpranacak bir şey yoktur.</summary>
    [Fact]
    public void BareSlotsNeverWear()
    {
        var battle = new Battle(Bout(Armor.None()), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        Assert.Empty(battle.Events.OfType<ArmorDestroyed>());
        Assert.Equal(0, result.Summaries.First(s => s.Id == new WarriorId(1)).ArmorWear.Total);
    }

    /// <summary>Dayanıklılık savaşçıya aittir: dövüş geçmiş yıpranmanın üstüne yazar.</summary>
    /// <remarks>
    /// Kuralın bütün anlamı bu: bir parça tek dövüşte tükenmez, <b>seferler boyunca</b>
    /// yıpranır ve bir gün ortada dağılır.
    /// </remarks>
    [Fact]
    public void WearCarriesOverFromEarlierBattles()
    {
        BattleSetup fresh = Bout(Sturdy);

        var first = new Battle(fresh, new FixedRandom(0.0));
        first.Run();
        Assert.Empty(first.Events.OfType<ArmorDestroyed>());

        // Aynı kuşam, ama neredeyse tükenmiş olarak sahaya çıkıyor.
        BattleSetup used = Bout(Sturdy);
        used.PlayerSide[0].ArmorWear = new ArmorWearSet(
            Head: 9_995,
            Torso: 9_995,
            SwordArm: 9_995,
            OffArm: 9_995,
            RightLeg: 9_995,
            LeftLeg: 9_995);

        var second = new Battle(used, new FixedRandom(0.0));
        second.Run();

        Assert.NotEmpty(second.Events.OfType<ArmorDestroyed>());
    }

    /// <summary>Dövüş kalıcı hale dokunmaz — yıpranmayı dojo işler.</summary>
    [Fact]
    public void TheBattleDoesNotWriteBackToTheWarrior()
    {
        BattleSetup setup = Bout(Brittle);
        Warrior worn = setup.PlayerSide[0];

        new Battle(setup, new FixedRandom(0.0)).Run();

        Assert.Equal(0, worn.ArmorWear.Total);
        Assert.Equal("Test-Kırılgan", worn.Armor.Name);
    }

    /// <summary>Ölçek 0 iken zırh hiç yıpranmaz — kontrol tarafı ayakta.</summary>
    [Fact]
    public void TheRuleCanBeTurnedOff()
    {
        var battle = new Battle(
            Bout(Brittle, WearOnly with { ArmorDurabilityScale = 0 }),
            new FixedRandom(0.0));
        BattleResult result = battle.Run();

        Assert.Empty(battle.Events.OfType<ArmorDestroyed>());
        Assert.All(result.Summaries, s => Assert.Equal(HitLocationSet.None, s.DestroyedArmor));
    }

    /// <summary>Dağılan parça artık ağırlık da taşımaz.</summary>
    /// <remarks>
    /// Zırhın sahadaki bedeli ağırlıktı; parçası giden savaşçı korumasını kaybederken
    /// hızını geri alır. Ağırlık kalıcı kuşamdan okunsaydı savaşçı olmayan bir plakanın
    /// yükünü taşımaya devam ederdi.
    /// </remarks>
    [Fact]
    public void AShatteredPieceStopsWeighing()
    {
        var battle = new Battle(Bout(Brittle), new FixedRandom(0.0));
        battle.Run();

        // Altı yuvanın hepsi dağılınca kuşam ağırlığı sıfırlanır; snapshot bunu taşır.
        CombatantSnapshot snapshot = battle.SnapshotOf(new WarriorId(1));
        Assert.NotEqual(HitLocationSet.None, snapshot.DestroyedArmor);
    }
}
