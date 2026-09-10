using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Target selection is a decision, not an ordering: at every decision step the warrior weighs the
/// enemies by distance, wounds, exposed regions and how his teammates have piled up. These tests tie
/// down the direction of the weights and the rule's two brakes (the opportunity window, stickiness).
/// </summary>
public class TargetSelectionTests
{
    private static CombatTuning Quiet { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        BaseDisarmChance = 0,
        CatchDisarmChance = 0,
        StallGuardSeconds = 6,
    };

    /// <summary>Who the first attack goes to — the observable form of the choice.</summary>
    private static WarriorId FirstTargetOf(Battle battle, WarriorId attacker)
    {
        battle.Run();

        return battle.Events.OfType<AttackStarted>().First(a => a.Attacker == attacker).Defender;
    }

    /// <summary>As the wound difference grows the team turns on the wounded one.</summary>
    /// <remarks>
    /// The rule cannot be tested directly: the core does not expose health (the fight's only intervention
    /// point is <c>CommandRetreat</c>), so there is no setup that says "wound this enemy". What is tested
    /// is therefore the outcome: with stickiness off and the wound weight dominant, both warriors must be
    /// fighting the <b>same</b> — the most wounded — enemy at the end of the fight.
    /// </remarks>
    [Fact]
    public void TheWoundedOneDrawsTheTeam()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, "Biri", aggression: 100, weapon: Weapon.Katana()),
                TestBuilders.Warrior(2, "Other", aggression: 100, weapon: Weapon.Katana()),
            ],
            [
                TestBuilders.Warrior(101, "Healthy", health: 4000),

                // The same blow opens a much larger ratio on this one: the wound weight looks at the
                // ratio, not at absolute damage.
                TestBuilders.Warrior(102, "Frail", health: 400),
            ])
        {
            Tuning = Quiet with
            {
                StallGuardSeconds = 12,
                TargetStickiness = 0,
                TargetCrowdPenalty = 0,
                TargetWoundedWeight = 10_000,

                // The opportunity window is taken out of the equation here: it has its own test
                // (ADistantWoundedEnemyIsNotWorthTheWalk), and left open both warriors would only see
                // the enemy in front of them.
                TargetOpportunityRange = 10_000,
            },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        CombatantSnapshot weakest = battle.Snapshots()
            .Where(s => s.Team != 0)
            .OrderBy(s => s.Health / s.MaxHealth)
            .First();

        WarriorId[] late = [.. battle.Events.OfType<AttackStarted>()
            .Where(a => a.Attacker == new WarriorId(1) || a.Attacker == new WarriorId(2))
            .TakeLast(4)
            .Select(a => a.Defender)];

        Assert.NotEmpty(late);
        Assert.All(late, t => Assert.Equal(weakest.Id, t));
    }

    /// <summary>The opportunity window: a distant wounded enemy cannot beat the healthy one beside you.</summary>
    /// <remarks>
    /// Without the window the warrior would leave the enemy in front of him and cross the arena, taking
    /// free hits along the way. Measured: an unbounded wound weight turned the rule into a flat
    /// difficulty increase (docs/GDD.md §4).
    /// </remarks>
    [Fact]
    public void ADistantWoundedEnemyIsNotWorthTheWalk()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Chooser", aggression: 100, weapon: Weapon.Katana())],
            [
                TestBuilders.Warrior(101, "Near", health: 100),
                TestBuilders.Warrior(102, "Uzak", health: 100),
            ])
        {
            Tuning = Quiet with { StartOffsetX = 30 },
        };

        var battle = new Battle(setup, new FixedRandom(0.999));
        battle.Step();
        PushAway(battle, new WarriorId(102), by: 600);

        Assert.Equal(new WarriorId(101), FirstTargetOf(battle, new WarriorId(1)));
    }

    /// <summary>A region whose armour has broken draws the target.</summary>
    /// <remarks>
    /// Armour wear's in-combat meaning closes here: an enemy whose piece has broken not only takes more
    /// damage, he also draws <b>more attention</b>.
    /// </remarks>
    [Fact]
    public void ABareSpotDrawsTheBlade()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, "Biri", aggression: 100, weapon: Weapon.Katana()),
                TestBuilders.Warrior(2, "Other", aggression: 100, weapon: Weapon.Katana()),
            ],
            [
                // The bare side has no piece to break (its pool is zero): so that which warrior the
                // breaking happened to is known from the test's setup.
                TestBuilders.Warrior(101, "Bare", health: 4000, armor: Armor.None()),
                TestBuilders.Warrior(102, "Armoured", health: 4000, armor: Armor.Light()),
            ])
        {
            Tuning = Quiet with
            {
                StallGuardSeconds = 12,

                // Fragile armour: so the moment of breaking happens inside the test.
                ArmorDurabilityScale = 0.02,
                TargetStickiness = 0,
                TargetCrowdPenalty = 0,
                TargetWoundedWeight = 0,
                TargetExposedWeight = 10_000,
                TargetOpportunityRange = 10_000,
            },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        CombatantSnapshot bare = battle.Snapshots()
            .Where(s => s.Team != 0)
            .OrderByDescending(s => s.DestroyedArmor.Count())
            .First();

        Assert.NotEqual(HitLocationSet.None, bare.DestroyedArmor);

        WarriorId[] late = [.. battle.Events.OfType<AttackStarted>()
            .Where(a => a.Attacker == new WarriorId(1) || a.Attacker == new WarriorId(2))
            .TakeLast(4)
            .Select(a => a.Defender)];

        Assert.All(late, t => Assert.Equal(bare.Id, t));
    }

    /// <summary>Stickiness: while nothing changes, the target does not change.</summary>
    /// <remarks>
    /// Without stickiness a warrior caught between two enemies would change direction at every decision
    /// step and reach neither — the price of switching targets is the road wasted.
    /// </remarks>
    [Fact]
    public void AWarriorDoesNotThrashBetweenEqualEnemies()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Chooser", aggression: 100, weapon: Weapon.Katana())],
            [
                TestBuilders.Warrior(101, "Twin-1", health: 4000),
                TestBuilders.Warrior(102, "Twin-2", health: 4000),
            ])
        {
            Tuning = Quiet with { StallGuardSeconds = 12 },
        };

        var battle = new Battle(setup, new FixedRandom(0.999));
        battle.Run();

        WarriorId[] targets = [.. battle.Events.OfType<AttackStarted>()
            .Where(a => a.Attacker == new WarriorId(1))
            .Select(a => a.Defender)];

        Assert.NotEmpty(targets);
        Assert.All(targets, t => Assert.Equal(targets[0], t));
    }

    /// <summary>The crowd penalty: the team does not pile onto the same enemy.</summary>
    [Fact]
    public void ATeamSpreadsInsteadOfPiling()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, "Biri", aggression: 100, weapon: Weapon.Katana()),
                TestBuilders.Warrior(2, "Other", aggression: 100, weapon: Weapon.Katana()),
            ],
            [
                TestBuilders.Warrior(101, "Enemy-1", health: 4000),
                TestBuilders.Warrior(102, "Enemy-2", health: 4000),
            ])
        {
            Tuning = Quiet with { StallGuardSeconds = 12, TargetCrowdPenalty = 1000 },
        };

        var battle = new Battle(setup, new FixedRandom(0.999));
        battle.Run();

        WarriorId? first = battle.Events.OfType<AttackStarted>()
            .Where(a => a.Attacker == new WarriorId(1))
            .Select(a => (WarriorId?)a.Defender)
            .FirstOrDefault();

        WarriorId? second = battle.Events.OfType<AttackStarted>()
            .Where(a => a.Attacker == new WarriorId(2))
            .Select(a => (WarriorId?)a.Defender)
            .FirstOrDefault();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }

    /// <summary>A dead target is dropped — the rule's oldest form still holds.</summary>
    [Fact]
    public void ADeadEnemyIsDropped()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Chooser", aggression: 100, weapon: TestBuilders.Executioner())],
            [
                TestBuilders.Warrior(101, "Fragile", health: 30),
                TestBuilders.Warrior(102, "Sturdy", health: 4000),
            ])
        {
            Tuning = Quiet with { StallGuardSeconds = 12 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        Assert.Contains(battle.Events.OfType<WarriorDied>(), d => d.Warrior == new WarriorId(101));
        Assert.Contains(
            battle.Events.OfType<AttackStarted>(),
            a => a.Attacker == new WarriorId(1) && a.Defender == new WarriorId(102));
    }

    /// <summary>The only way to move a target far away: keep the fight going long enough to push him away.</summary>
    private static void PushAway(Battle battle, WarriorId id, double by)
    {
        for (int i = 0; i < 200 && !battle.IsFinished; i++)
        {
            CombatantSnapshot s = battle.SnapshotOf(id);

            if (s.Position.X >= by)
            {
                return;
            }

            battle.Step();
        }
    }
}
