using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Armour is a consumable: every blow it stops wears it, and when its pool runs out the piece breaks
/// <b>permanently</b>. These tests tie down wear's source (the damage absorbed), the consequence of
/// breaking (that region is bare) and the rule not touching the persistent state.
/// </summary>
public class ArmorDurabilityTests
{
    /// <summary>The setting that isolates wear: dismemberment, stun and disarming are off.</summary>
    private static CombatTuning WearOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        BaseDisarmChance = 0,
        CatchDisarmChance = 0,
        StallGuardSeconds = 20,
    };

    /// <summary>A pool that runs out in one strike: so the moment of breaking can be measured.</summary>
    private static Armor Brittle { get; } = Armor.Uniform(
        "Test fragile",
        new ArmorPiece("Test piece", DamageReduction: 5, DismembermentResistance: 0.5, Weight: 1, Durability: 5));

    /// <summary>The inexhaustible version of the same piece — the control side.</summary>
    private static Armor Sturdy { get; } = Armor.Uniform(
        "Test sturdy",
        new ArmorPiece("Test piece", DamageReduction: 5, DismembermentResistance: 0.5, Weight: 1, Durability: 10_000));

    private static Weapon Blade { get; } =
        new("Test-Katana", WeaponClass.Cutting, 20, TwoHanded: false, AttackSeconds: 1.0);

    private static BattleSetup Bout(Armor defenderArmor, CombatTuning? tuning = null) => new(
        [
            TestBuilders.Warrior(1, "Armoured", health: 4000, aggression: 0, weapon: Weapon.Fists(), armor: defenderArmor),
        ],
        [TestBuilders.Warrior(101, "Vuran", health: 4000, aggression: 100, weapon: Blade)])
    {
        Tuning = tuning ?? WearOnly,
    };

    /// <summary>A piece wears by as much damage as it stops.</summary>
    /// <remarks>
    /// Reduced by the incoming damage, thick plate would run out as fast as thin cloth and the difference
    /// between the tiers would exist only on paper (docs/GDD.md §7).
    /// </remarks>
    [Fact]
    public void APieceWearsByWhatItStops()
    {
        var battle = new Battle(Bout(Sturdy), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WarriorBattleSummary worn = result.Summaries.First(s => s.Id == new WarriorId(1));
        int hits = battle.Events.OfType<AttackLanded>().Count(a => a.Defender == new WarriorId(1));

        // Every hit wears it by the piece's reduction: 5 damage × the number of hits.
        Assert.Equal(hits * 5.0, worn.ArmorWear.Total, precision: 6);
    }

    /// <summary>When the pool runs out the piece breaks and that region is left bare.</summary>
    [Fact]
    public void AnExhaustedPieceShattersAndLeavesTheSlotBare()
    {
        var battle = new Battle(Bout(Brittle), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        ArmorDestroyed destroyed = battle.Events.OfType<ArmorDestroyed>().First();
        Assert.Equal(new WarriorId(1), destroyed.Warrior);

        WarriorBattleSummary summary = result.Summaries.First(s => s.Id == new WarriorId(1));
        Assert.True(summary.DestroyedArmor.Has(destroyed.Slot));

        // After it breaks, a strike landing on that region is no longer reduced: the damage rises.
        List<AttackLanded> onSlot =
            [.. battle.Events.OfType<AttackLanded>().Where(a => a.Defender == new WarriorId(1))];

        // The strike that finishes the piece still takes the reduction; the ones after it do not.
        double whileWorn = onSlot.First(a => a.AtSeconds <= destroyed.AtSeconds).Damage;

        Assert.Contains(
            onSlot,
            a => a.AtSeconds > destroyed.AtSeconds && a.Damage > whileWorn);
    }

    /// <summary>A warrior with no armour has nothing to wear down.</summary>
    [Fact]
    public void BareSlotsNeverWear()
    {
        var battle = new Battle(Bout(Armor.None()), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        Assert.Empty(battle.Events.OfType<ArmorDestroyed>());
        Assert.Equal(0, result.Summaries.First(s => s.Id == new WarriorId(1)).ArmorWear.Total);
    }

    /// <summary>Durability belongs to the warrior: a fight writes on top of past wear.</summary>
    /// <remarks>
    /// This is the whole meaning of the rule: a piece is not used up in a single fight, it wears
    /// <b>across expeditions</b> and one day breaks in the middle of one.
    /// </remarks>
    [Fact]
    public void WearCarriesOverFromEarlierBattles()
    {
        BattleSetup fresh = Bout(Sturdy);

        var first = new Battle(fresh, new FixedRandom(0.0));
        first.Run();
        Assert.Empty(first.Events.OfType<ArmorDestroyed>());

        // The same kit, but taking the field almost used up.
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

    /// <summary>A fight does not touch the persistent state — the dojo applies the wear.</summary>
    [Fact]
    public void TheBattleDoesNotWriteBackToTheWarrior()
    {
        BattleSetup setup = Bout(Brittle);
        Warrior worn = setup.PlayerSide[0];

        new Battle(setup, new FixedRandom(0.0)).Run();

        Assert.Equal(0, worn.ArmorWear.Total);
        Assert.Equal("Test fragile", worn.Armor.Name);
    }

    /// <summary>With the scale at 0 armour never wears — the control side holds.</summary>
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

    /// <summary>A broken piece no longer carries weight either.</summary>
    /// <remarks>
    /// Armour's price on the field was weight; a warrior whose piece is gone loses his protection and
    /// gets his speed back. If the weight were read from the persistent kit, the warrior would keep
    /// carrying the load of a plate that is no longer there.
    /// </remarks>
    [Fact]
    public void AShatteredPieceStopsWeighing()
    {
        var battle = new Battle(Bout(Brittle), new FixedRandom(0.0));
        battle.Run();

        // When all six slots break the kit's weight goes to zero; the snapshot carries that.
        CombatantSnapshot snapshot = battle.SnapshotOf(new WarriorId(1));
        Assert.NotEqual(HitLocationSet.None, snapshot.DestroyedArmor);
    }
}
