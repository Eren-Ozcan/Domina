using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The arena is now a <b>plane</b>: the warriors walk, the weapon has reach, and being surrounded is a
/// real danger. These tests tie down what space promised.
/// </summary>
public class MovementTests
{
    private static BattleSetup Approach(Weapon? playerWeapon = null) => new(
        [TestBuilders.Warrior(1, health: 400, weapon: playerWeapon)],
        [TestBuilders.Warrior(101, health: 400)]);

    private static void StepMany(Battle battle, int steps)
    {
        for (int i = 0; i < steps && battle.Step(); i++)
        {
            // Step() checks the end condition itself.
        }
    }

    /// <summary>Two warriors standing opposite each other walk toward one another.</summary>
    [Fact]
    public void WarriorsWalkTowardEachOther()
    {
        var battle = new Battle(Approach(), new SeededRandom(7));

        double startGap = Gap(battle);
        StepMany(battle, 10);

        Assert.True(Gap(battle) < startGap, "The warriors did not close.");
    }

    /// <summary>
    /// No attack starts while out of reach; the warrior has to close first.
    /// </summary>
    [Fact]
    public void NoOneSwingsFromOutOfReach()
    {
        var battle = new Battle(Approach(), new SeededRandom(7));

        // On the first tick the sides are 960 units apart — no weapon can reach that far.
        battle.Step();

        Assert.DoesNotContain(battle.Events, e => e is AttackStarted);
        Assert.Equal(CombatState.Idle, battle.SnapshotOf(new WarriorId(1)).State);
    }

    /// <summary>
    /// A long weapon stands further away. Without reach, the difference between a naginata and a tantō
    /// would be damage and speed alone.
    /// </summary>
    [Fact]
    public void LongerWeaponsStopFartherAway()
    {
        var withSpear = new Battle(Approach(Weapon.Yari()), new SeededRandom(3));
        var withBlade = new Battle(Approach(Weapon.Katana()), new SeededRandom(3));

        StepUntilSwinging(withSpear);
        StepUntilSwinging(withBlade);

        Assert.True(
            Gap(withSpear) > Gap(withBlade),
            "The spear warrior must strike from further away than the sword one.");
    }

    /// <summary>Warriors do not overlap.</summary>
    [Fact]
    public void WarriorsKeepTheirPersonalSpace()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400),
                TestBuilders.Warrior(2, health: 400),
                TestBuilders.Warrior(3, health: 400),
            ],
            [TestBuilders.Warrior(101, health: 400)]);

        var battle = new Battle(setup, new SeededRandom(11));
        StepMany(battle, 120);

        IReadOnlyList<CombatantSnapshot> snapshots = battle.Snapshots();
        for (int i = 0; i < snapshots.Count; i++)
        {
            for (int j = i + 1; j < snapshots.Count; j++)
            {
                if (!snapshots[i].IsActive || !snapshots[j].IsActive)
                {
                    continue;
                }

                double gap = snapshots[i].Position.DistanceTo(snapshots[j].Position);
                Assert.True(gap > 1, $"Two warriors overlapped: {gap:F1}");
            }
        }
    }

    /// <summary>
    /// A fleeing warrior takes a free hit from <b>every enemy in his reach</b>. That is the price of
    /// being surrounded: if you are encircled, pulling out means three blows.
    /// </summary>
    [Fact]
    public void BeingSurroundedMakesRetreatCostMore()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 900)],
            [
                TestBuilders.Warrior(101, health: 400),
                TestBuilders.Warrior(102, health: 400),
                TestBuilders.Warrior(103, health: 400),
            ])
        {
            // All three are within the fleer's reach: the surrounded state.
            Tuning = TestBuilders.PointBlank with { StartSpacingY = 25 },
        };

        var battle = new Battle(setup, new SeededRandom(5));

        // The key is closed until the first hit (GDD §5); the price of encirclement is measured after that.
        while (!battle.ContactMade && battle.Step())
        {
        }

        battle.CommandRetreat();
        StepMany(battle, 10);

        int swings = battle.Events.OfType<OpportunityAttack>().Count();
        Assert.True(swings > 1, $"The surrounded warrior got away with a single free hit: {swings}");
    }

    /// <summary>
    /// If the target leaves reach during the move the sword lands on empty air — the price of committing
    /// kilitlenmenin bedeli.
    /// </summary>
    [Fact]
    public void ASwingAtAFleeingTargetCanWhiff()
    {
        // A chaser after a fleeing target misses sooner or later in a long fight.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 900, aggression: 100)],
            [TestBuilders.Warrior(101, health: 900, aggression: 100)])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new SeededRandom(2));
        StepMany(battle, 20);

        while (!battle.ContactMade && battle.Step())
        {
        }

        battle.CommandRetreat();
        StepMany(battle, 60);

        Assert.Contains(battle.Events, e => e is AttackMissed or OpportunityAttack);
    }

    private static void StepUntilSwinging(Battle battle)
    {
        for (int i = 0; i < 400; i++)
        {
            if (battle.SnapshotOf(new WarriorId(1)).State == CombatState.AttackWindup)
            {
                return;
            }

            if (!battle.Step())
            {
                return;
            }
        }
    }

    private static double Gap(Battle battle) =>
        battle.SnapshotOf(new WarriorId(1)).Position
            .DistanceTo(battle.SnapshotOf(new WarriorId(101)).Position);
}
