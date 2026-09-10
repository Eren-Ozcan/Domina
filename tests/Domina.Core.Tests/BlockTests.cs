using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// A block is defence's second axis, separate from evasion: evasion makes the blow miss and ends
/// there, a block <b>meets</b> the blow — damage drops, no limb comes off, but the blow has landed, and
/// the time spent in the stance is a strike not made. These tests tie down the decision's source (the
/// Defence stat), the stance's price (the attack cycle) and what it holds and does not hold.
/// </summary>
public class BlockTests
{
    /// <summary>The setting that isolates the block: dismemberment, stun and disarming are off.</summary>
    private static CombatTuning BlockOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        BaseDisarmChance = 0,
        CatchDisarmChance = 0,
        StallGuardSeconds = 20,
    };

    private static Weapon Blade { get; } =
        new("Test-Katana", WeaponClass.Cutting, 20, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>A weapon with a block quality of 1.0 — it takes quality out of the equation.</summary>
    private static Weapon Guardpole { get; } =
        new("Test-Naginata", WeaponClass.Cutting, 20, TwoHanded: true, AttackSeconds: 1.0);

    /// <param name="defense">The defender's Defence stat — the block die's only source.</param>
    private static BattleSetup Bout(
        double defense,
        CombatTuning? tuning = null,
        Weapon? defenderWeapon = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Blocker",
                health: 4000,
                aggression: 0,
                defense: defense,
                weapon: defenderWeapon ?? Guardpole),
        ],
        [TestBuilders.Warrior(101, "Vuran", health: 4000, aggression: 100, weapon: Blade)])
        {
            Tuning = tuning ?? BlockOnly,
        };

    /// <summary>A warrior with Defence 0 never blocks.</summary>
    /// <remarks>
    /// The same shape as evasion: no base chance. That is why the rule limits itself — tests that do not
    /// measure blocking can switch the stance off by zeroing the stat.
    /// </remarks>
    [Fact]
    public void AWarriorWithNoDefenseNeverRaisesAGuard()
    {
        var battle = new Battle(Bout(defense: 0), new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<BlockRaised>());
        Assert.Empty(battle.Events.OfType<AttackBlocked>());
    }

    /// <summary>The stance meets the incoming blow: damage drops and the event flows separately.</summary>
    [Fact]
    public void AGuardedBlowLandsSofterThanAnOpenOne()
    {
        var guarded = new Battle(Bout(defense: 100), new FixedRandom(0.0));
        guarded.Run();

        var open = new Battle(Bout(defense: 0), new FixedRandom(0.0));
        open.Run();

        AttackBlocked[] blocked = [.. guarded.Events.OfType<AttackBlocked>()];
        Assert.NotEmpty(blocked);

        double openDamage = open.Events.OfType<AttackLanded>()
            .First(a => a.Defender == new WarriorId(1)).Damage;

        // A blow that is met is not erased, it is lightened: BlockDamageReduction 0.70 and the weapon's
        // block quality 1.0 — three tenths of the open blow are left.
        Assert.All(blocked, b => Assert.True(
            b.Damage < openDamage,
            $"The blocked blow was {b.Damage:F2}, the open blow {openDamage:F2}."));
    }

    /// <summary>A blocked blow takes no limb — the only certain promise the Defence stat makes.</summary>
    [Fact]
    public void AGuardedBlowCannotTakeALimb()
    {
        CombatTuning alwaysSevers = BlockOnly with { BaseDismembermentChance = 1.0 };

        var battle = new Battle(Bout(defense: 100, alwaysSevers), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        Assert.NotEmpty(battle.Events.OfType<AttackBlocked>());

        // The dismemberment die holds on every strike; none of the blocked ones may take a limb.
        var blockedAt = battle.Events.OfType<AttackBlocked>().Select(b => b.AtSeconds).ToHashSet();
        var severedAt = battle.Events.OfType<WarriorDismembered>()
            .Where(d => d.Warrior == new WarriorId(1))
            .Select(d => d.AtSeconds);

        Assert.All(severedAt, at => Assert.DoesNotContain(at, blockedAt));
        Assert.NotNull(result);
    }

    /// <summary>A blunt weapon goes through the block: the concussion share works despite the stance.</summary>
    /// <remarks>
    /// With no shield, this is the blunt class's fourth gain. If the block worked fully against blunt
    /// weapons too, their only answer would be erased in front of a defensive warrior.
    /// </remarks>
    [Fact]
    public void AGuardStopsSteelButNotTheShock()
    {
        // The stun threshold is zeroed: what is tested is not what a heavy blow is but that a blocked
        // blow keeps carrying its concussion share.
        CombatTuning stunOnly = BlockOnly with
        {
            BaseStunChance = 1.0,
            StunSeverityThreshold = 0,

            // A short stun: a long one would leave the defender no chance to take the stance between two
            // blows, and the test would measure the stun lock rather than the stun.
            StunSeconds = 0.2,
            StallGuardSeconds = 10,
        };

        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Blocker", health: 4000, aggression: 0, defense: 100)],
            [
                TestBuilders.Warrior(
                    101,
                    "Clubman",
                    health: 4000,
                    aggression: 100,
                    weapon: new Weapon("Test-Tetsubo", WeaponClass.Blunt, 30, TwoHanded: true, AttackSeconds: 2.0)),
            ])
        {
            Tuning = stunOnly,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        AttackBlocked[] blocked = [.. battle.Events.OfType<AttackBlocked>()];
        Assert.NotEmpty(blocked);

        // At least one of the blows met must have stunned: the stance stops steel, it does not stop
        // concussion.
        var stunnedAt = battle.Events.OfType<WarriorStunned>().Select(s => s.AtSeconds).ToHashSet();
        Assert.Contains(blocked, b => stunnedAt.Contains(b.AtSeconds));
    }

    /// <summary>The block's price: the time spent in the stance is a strike not made.</summary>
    [Fact]
    public void AGuardIsPaidForWithSwings()
    {
        var guarded = new Battle(Bout(defense: 100), new FixedRandom(0.0));
        BattleResult guardedResult = guarded.Run();

        var open = new Battle(Bout(defense: 0), new FixedRandom(0.0));
        BattleResult openResult = open.Run();

        int guardedSwings = guardedResult.SummaryFor(new WarriorId(1)).AttacksMade;
        int openSwings = openResult.SummaryFor(new WarriorId(1)).AttacksMade;

        Assert.True(
            guardedSwings < openSwings,
            $"The blocker made {guardedSwings} swings, the one who never blocked {openSwings}.");
    }

    /// <summary>No block comes right after a block: the stance is bound to a rhythm.</summary>
    /// <remarks>
    /// If the die were rolled again at every decision step, a warrior with high defence could block back
    /// to back and never strike — the fight would lock up.
    /// </remarks>
    [Fact]
    public void AGuardCannotFollowAGuard()
    {
        var battle = new Battle(Bout(defense: 100), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        // FixedRandom(0.0) makes every die hold: without the rule the warrior would never strike after
        // the first stance.
        Assert.True(result.SummaryFor(new WarriorId(1)).AttacksMade > 0);
        Assert.NotEmpty(battle.Events.OfType<BlockRaised>());
    }

    /// <summary>How much the stance holds is read from the weapon in his hand.</summary>
    /// <remarks>
    /// A warrior who drops his weapon loses his block too: the fists' block quality is 0.30.
    /// </remarks>
    [Fact]
    public void TheWeaponInHandDecidesHowMuchTheGuardHolds()
    {
        var withPole = new Battle(Bout(defense: 100), new FixedRandom(0.0));
        withPole.Run();

        var withFists = new Battle(
            Bout(defense: 100, defenderWeapon: Weapon.Fists()),
            new FixedRandom(0.0));
        withFists.Run();

        double poleDamage = withPole.Events.OfType<AttackBlocked>().First().Damage;
        double fistDamage = withFists.Events.OfType<AttackBlocked>().First().Damage;

        Assert.True(
            poleDamage < fistDamage,
            $"The haft let through {poleDamage:F2}, the fists {fistDamage:F2} — the quality works backwards.");
    }

    /// <summary>A strike from behind cannot be blocked.</summary>
    [Fact]
    public void AGuardFacesOnlyForward()
    {
        Assert.Equal(0.30, Weapon.Fists().BlockFactor);
        Assert.Equal(1.0, Guardpole.BlockFactor);

        // The catching implement gets no extra favour for blocking: it is a one-handed blunt implement
        // and its quality is that. When it was favoured, measurement broke a locked brake — the jitte
        // stopped being the wrong choice in front of an enemy carrying a heavy weapon (docs/GDD.md §5).
        Assert.Equal(0.85, Weapon.Jitte().BlockFactor);
    }
}
