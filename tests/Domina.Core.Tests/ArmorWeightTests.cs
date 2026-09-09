using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Armour's weight is what makes a kit a <b>decision</b>. While it was free, ō-yoroi was superior on
/// every axis and its only brake was a price that did not yet exist. These tests tie down weight's two
/// lines: the sword lands late, the warrior walks slowly.
/// </summary>
public class ArmorWeightTests
{
    /// <summary>The setting where weight touches nothing — the comparison baseline.</summary>
    private static readonly CombatTuning Weightless = TestBuilders.PointBlank with
    {
        ArmorAttackSlowdownAtFullWeight = 0,
    };

    /// <summary>A kit's weight is the sum of its pieces; an empty slot carries no weight.</summary>
    [Fact]
    public void ArmorWeighsTheSumOfItsPieces()
    {
        Assert.Equal(0, Armor.None().Weight);
        Assert.Equal(ArmorPiece.Keikogi.Weight, Armor.Light().Weight);
        Assert.True(Armor.Heavy().Weight > Armor.Medium().Weight);
        Assert.Equal(
            ArmorPiece.Kabuto.Weight
            + ArmorPiece.OYoroiCuirass.Weight
            + (ArmorPiece.HeavyKote.Weight * 2)
            + (ArmorPiece.HeavySuneate.Weight * 2),
            Armor.Heavy().Weight);
    }

    /// <summary>
    /// A heavily armoured warrior starts fewer attacks in the same time — that is weight's biting line.
    /// The speed and stamina penalties did not budge victory in measurement; because a fight ends through
    /// the damage exchange, the price has to land there.
    /// </summary>
    [Fact]
    public void HeavyArmorSlowsTheSword()
    {
        int bare = AttacksIn(TestBuilders.PointBlank, Armor.None());
        int heavy = AttacksIn(TestBuilders.PointBlank, Armor.Heavy());

        Assert.True(heavy < bare, $"Heavy armour did not slow the sword ({heavy} >= {bare}).");
    }

    /// <summary>The reason for the slowdown is weight: with the penalty zeroed the difference closes.</summary>
    [Fact]
    public void WithoutThePenaltyArmorDoesNotSlowTheSword()
    {
        Assert.Equal(AttacksIn(Weightless, Armor.None()), AttacksIn(Weightless, Armor.Heavy()));
    }

    /// <summary>
    /// Weight does not touch walking. It was tried and undone: a penalty written onto walking speed did
    /// not budge victory at all but erased §5's promise — the armoured warrior was caught before he could
    /// leave the arena, so the "Flee" key stopped reducing death (pulling 46.35%, not pulling 46.44%;
    /// against 44.32% vs 46.33% with no penalty).
    /// </summary>
    [Fact]
    public void ArmorDoesNotSlowTheWalk()
    {
        Assert.Equal(DistanceWalked(Armor.None()), DistanceWalked(Armor.Heavy()), 6);
    }

    private static int AttacksIn(CombatTuning tuning, Armor armor)
    {
        var battle = new Battle(
            new BattleSetup(
                [TestBuilders.Warrior(1, health: 4000, armor: armor, evasion: 0)],
                [TestBuilders.Warrior(101, health: 4000, aggression: 0, evasion: 0)])
            {
                Tuning = tuning,
            },
            new SeededRandom(11));

        for (int i = 0; i < 400 && battle.Step(); i++)
        {
            // A fixed number of ticks: the comparison is made against time.
        }

        return battle.Events.OfType<AttackStarted>().Count(e => e.Attacker == new WarriorId(1));
    }

    private static double DistanceWalked(Armor armor)
    {
        var battle = new Battle(
            new BattleSetup(
                [TestBuilders.Warrior(1, armor: armor)],
                [TestBuilders.Warrior(101, speed: 0)]),
            new SeededRandom(11));

        double start = battle.SnapshotOf(new WarriorId(1)).Position.X;
        for (int i = 0; i < 40 && battle.Step(); i++)
        {
            // The closing phase.
        }

        return Math.Abs(battle.SnapshotOf(new WarriorId(1)).Position.X - start);
    }
}
