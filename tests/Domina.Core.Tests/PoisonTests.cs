using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Poison is the only route around damage reduction: the dose enters the blood and armour cannot read
/// it. These tests tie down both ends of the rule — the dose works over time, armour and Defence do not
/// reduce it — and keep the places poison must <b>not touch</b> (dismemberment, stun, a clean weapon)
/// closed.
/// </summary>
public class PoisonTests
{
    /// <summary>The setting that isolates poison: the dismemberment and stun branches are off.</summary>
    /// <remarks>
    /// All three come out of the same strike; left open, the test would measure the outcome tree's
    /// behaviour rather than poison's.
    /// </remarks>
    private static CombatTuning PoisonOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        StallGuardSeconds = 12,
    };

    /// <summary>The poisoned implement: light on steel, its return is the dose.</summary>
    private static Weapon Fang { get; } =
        new("Test-Zehirli", WeaponClass.Cutting, 5, TwoHanded: false, AttackSeconds: 1.0)
        {
            Poison = 1.0,
        };

    /// <summary>The clean version of the same implement — the control side.</summary>
    private static Weapon CleanFang { get; } = Fang with { Name = "Test-Temiz", Poison = 0 };

    private static BattleSetup Bout(
        Weapon attackerWeapon,
        Armor? defenderArmor = null,
        double defenderDefense = 0,
        CombatTuning? tuning = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Zehirlenen",
                health: 400,
                aggression: 0,
                defense: defenderDefense,
                weapon: Weapon.Fists(),
                armor: defenderArmor),
        ],
        [TestBuilders.Warrior(101, "Zehirleyen", aggression: 100, weapon: attackerWeapon)])
        {
            Tuning = tuning ?? PoisonOnly,
        };

    /// <summary>A poisoned strike leaves a dose and the dose eats health over time.</summary>
    [Fact]
    public void APoisonedBlowLeavesADoseThatKeepsWorking()
    {
        var battle = new Battle(Bout(Fang), new FixedRandom(0.0));
        battle.Run();

        WarriorPoisoned poisoned = battle.Events.OfType<WarriorPoisoned>().First();
        Assert.Equal(new WarriorId(1), poisoned.Defender);
        Assert.Equal(new WarriorId(101), poisoned.Attacker);
        Assert.Equal(PoisonOnly.PoisonSeconds, poisoned.Seconds);

        // The damage comes not at the moment of the strike but afterwards.
        List<PoisonTicked> ticks = [.. battle.Events.OfType<PoisonTicked>()];
        Assert.NotEmpty(ticks);
        Assert.All(ticks, t => Assert.True(t.AtSeconds > poisoned.AtSeconds));
    }

    /// <summary>
    /// Poison damage goes through neither armour nor the Defence stat.
    /// </summary>
    /// <remarks>
    /// The whole rule is built on this. If armour reduced the dose, poison would be only "a bit more
    /// damage" and the poisoned weapon's low steel would be the price of nothing.
    /// </remarks>
    [Fact]
    public void PoisonIgnoresArmorAndDefense()
    {
        double bare = FirstTickDamage(Bout(Fang));
        double armored = FirstTickDamage(
            Bout(Fang, defenderArmor: Armor.Heavy(), defenderDefense: 100));

        Assert.Equal(bare, armored);

        static double FirstTickDamage(BattleSetup setup)
        {
            var battle = new Battle(setup, new FixedRandom(0.0));
            battle.Run();
            return battle.Events.OfType<PoisonTicked>().First().Damage;
        }
    }

    /// <summary>The dose accumulates — but does not pass the cap.</summary>
    [Fact]
    public void DosesStackUpToTheCap()
    {
        CombatTuning tuning = PoisonOnly with { PoisonMaxDose = 2.0 };
        var battle = new Battle(Bout(Fang, tuning: tuning), new FixedRandom(0.0));
        battle.Run();

        List<WarriorPoisoned> doses = [.. battle.Events.OfType<WarriorPoisoned>()];

        Assert.True(doses.Count >= 3, $"Not enough poisoned strikes landed ({doses.Count}).");
        Assert.Equal(1.0, doses[0].Dose);
        Assert.Equal(2.0, doses[1].Dose);
        Assert.All(doses, d => Assert.True(d.Dose <= tuning.PoisonMaxDose));
    }

    /// <summary>When the time is up the poison ends on its own.</summary>
    [Fact]
    public void PoisonExpiresOnItsOwn()
    {
        // A single poisoned strike: the attacker is too slow to strike again.
        Weapon slowFang = Fang with { AttackSeconds = 30 };
        var battle = new Battle(
            Bout(slowFang, tuning: PoisonOnly with { StallGuardSeconds = 20 }),
            new FixedRandom(0.0));
        battle.Run();

        WarriorPoisoned poisoned = Assert.Single(battle.Events.OfType<WarriorPoisoned>());
        double lastTick = battle.Events.OfType<PoisonTicked>().Max(t => t.AtSeconds);

        Assert.True(
            lastTick <= poisoned.AtSeconds + PoisonOnly.PoisonSeconds,
            $"The poison outlived its duration ({lastTick:F2} > {poisoned.AtSeconds + PoisonOnly.PoisonSeconds:F2}).");
    }

    /// <summary>A clean weapon leaves no dose — the control side.</summary>
    [Fact]
    public void ACleanWeaponNeverPoisons()
    {
        var battle = new Battle(Bout(CleanFang), new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<AttackLanded>());
        Assert.Empty(battle.Events.OfType<WarriorPoisoned>());
        Assert.Empty(battle.Events.OfType<PoisonTicked>());
    }

    /// <summary>
    /// Poison neither severs a limb nor stuns: both are the result of a <b>blow</b>.
    /// </summary>
    /// <remarks>
    /// The thresholds are pulled unreachably high, so the strike itself rolls no dice; only poison is
    /// left. If poison opened those branches too, the poisoned weapon would take a share of the game's
    /// signature mechanic and do the blunt class's job as well.
    /// </remarks>
    [Fact]
    public void PoisonNeitherSeversNorStuns()
    {
        CombatTuning tuning = PoisonOnly with
        {
            BaseDismembermentChance = 1.0,
            BaseStunChance = 1.0,
            GrievousSeverityThreshold = 5.0,
            StunSeverityThreshold = 5.0,
        };

        var battle = new Battle(Bout(Fang, tuning: tuning), new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<PoisonTicked>());
        Assert.Empty(battle.Events.OfType<WarriorDismembered>());
        Assert.Empty(battle.Events.OfType<WarriorStunned>());
    }

    /// <summary>A death brought by poison carries a separate cause: nobody struck.</summary>
    [Fact]
    public void PoisonKillsUnderItsOwnCause()
    {
        // One strike, then the attacker falls silent; the only thing that can end the health is the dose.
        Weapon slowFang = Fang with { AttackSeconds = 30 };
        BattleSetup setup = Bout(slowFang, tuning: PoisonOnly with { StallGuardSeconds = 30 }) with
        {
            PlayerSide =
            [
                TestBuilders.Warrior(1, "Zehirlenen", health: 12, aggression: 0, weapon: Weapon.Fists()),
            ],
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        WarriorDied died = battle.Events.OfType<WarriorDied>().First(e => e.Warrior == new WarriorId(1));
        Assert.Equal(DeathCause.Poison, died.Cause);
        Assert.Equal(DeathCause.Poison, battle.Result!.SummaryFor(new WarriorId(1)).DeathCause);
    }

    /// <summary>
    /// The poison of a warrior pulling out does not stop — the key is not an antidote.
    /// </summary>
    /// <remarks>
    /// Stun and catching do not apply to a fleeing warrior, because both put <b>a new die</b> on top of
    /// the escape promise. Poison is not a new die but the continuation of a price already paid; stopped,
    /// the flee command would also be a cure.
    /// </remarks>
    [Fact]
    public void PoisonKeepsWorkingOnARetreatingWarrior()
    {
        var battle = new Battle(Bout(Fang), new FixedRandom(0.0));

        while (!battle.ContactMade && battle.Step())
        {
        }

        Assert.True(battle.CommandRetreat());
        double commandedAt = battle.ElapsedSeconds;

        battle.Run();

        Assert.Contains(
            battle.Events.OfType<PoisonTicked>(),
            t => t.Warrior == new WarriorId(1) && t.AtSeconds > commandedAt);
    }

    /// <summary>Poison is carried on a projectile too — the rule belongs to the blade, not to melee.</summary>
    [Fact]
    public void ThrownWeaponsCarryPoison()
    {
        BattleSetup setup = new(
            [
                TestBuilders.Warrior(1, "Hedef", health: 400, aggression: 0, weapon: Weapon.Fists()),
            ],
            [
                TestBuilders.Warrior(
                    101,
                    "Thrower",
                    aggression: 100,
                    weapon: CleanFang,
                    thrown: ThrownWeapon.PoisonedShuriken()),
            ])
        {
            Tuning = PoisonOnly with { StartOffsetX = 400 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        ProjectileHit hit = battle.Events.OfType<ProjectileHit>().First();
        Assert.Contains(
            battle.Events.OfType<WarriorPoisoned>(),
            p => p.Defender == new WarriorId(1) && p.AtSeconds == hit.AtSeconds);
    }
}
