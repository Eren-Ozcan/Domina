using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The three mechanics that stop escape from being free: <b>speed</b> (the enemy catching up),
/// <b>throwing</b> (a projectile from behind) and the <b>escape die</b> (the wound nobody struck).
/// </summary>
/// <remarks>
/// All three were added because none was enough on its own: with speed a single constant the chaser
/// could not catch the fleer, melee could not reach the far half of the arena, and the key pressed
/// before contact gave a 100% clean exit (measured, 20,000 fights).
/// </remarks>
public class RangedAndFlightTests
{
    /// <summary>A source where no die holds: it closes the chance-based branches like the accidental wound.</summary>
    private static CombatTuning NoMishap { get; } =
        TestBuilders.PointBlank with { EscapeMishapChance = 0 };

    /// <summary>A light, fast weapon: it draws first blood without killing.</summary>
    /// <remarks>
    /// The "pull out" key is closed until the first hit (GDD §5). Tests measuring the escape mechanics
    /// have to start the fight first; the fleeing warrior's own strike is the cheapest way — it unlocks
    /// the key without risking anyone's life.
    /// </remarks>
    private static Weapon Quick { get; } =
        new("Test-Tantō", WeaponClass.Cutting, 12, TwoHanded: false, AttackSeconds: 0.4);

    /// <summary>A weapon that does no damage: it starts the fight and wounds nobody.</summary>
    private static Weapon Harmless { get; } =
        new("Test-Sopa", WeaponClass.Blunt, 0, TwoHanded: false, AttackSeconds: 0.4);

    /// <summary>The along-the-line distance between two warriors.</summary>
    private static double Gap(Battle battle) => Math.Abs(
        battle.SnapshotOf(new WarriorId(1)).Position.X
        - battle.SnapshotOf(new WarriorId(101)).Position.X);

    /// <summary>Steps to the first hit, then presses the key and returns the moment the escape started.</summary>
    private static double PressAfterFirstBlood(Battle battle)
    {
        while (!battle.ContactMade && battle.Step())
        {
        }

        battle.CommandRetreat();

        for (int i = 0; i < 400 && !battle.Events.Any(e => e is RetreatStarted); i++)
        {
            if (!battle.Step())
            {
                break;
            }
        }

        return battle.Events.OfType<RetreatStarted>().First().AtSeconds;
    }

    [Fact]
    public void AFasterWarriorCatchesUpWithASlowerOne()
    {
        // The same roster, the only difference is speed. A slow chaser cannot catch up, a fast one can.
        Assert.False(CaughtUp(hunterSpeed: 5));
        Assert.True(CaughtUp(hunterSpeed: 100));
    }

    /// <remarks>
    /// What is measured is the blow taken <b>after the escape started</b>. The first hit is the blow
    /// that unlocks the key and lands in both setups; what separates the chase is whether the gap
    /// closes.
    /// </remarks>
    private static bool CaughtUp(double hunterSpeed)
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Fleer", health: 900, aggression: 0, speed: 50)],
            [TestBuilders.Warrior(101, "Kovalayan", health: 400, aggression: 100, speed: hunterSpeed)])
        {
            Tuning = NoMishap,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        double left = PressAfterFirstBlood(battle);
        battle.Run();

        return battle.Events
            .OfType<AttackLanded>()
            .Any(e => e.Defender == new WarriorId(1) && e.AtSeconds > left);
    }

    /// <summary>A warrior who loses a leg loses not only evasion but the ability to flee.</summary>
    [Fact]
    public void LosingALegCostsSpeedToo()
    {
        Warrior lame = TestBuilders.Warrior(1, speed: 80);
        double before = lame.EffectiveStats.Speed;

        lame.AddDisability(BodyPart.RightLeg);

        Assert.True(lame.EffectiveStats.Speed < before);
    }

    /// <summary>Because the fleer runs with his back turned he is slower than the chaser.</summary>
    [Fact]
    public void RetreatingIsSlowerThanChasing()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Fleer", health: 900, aggression: 0, speed: 50)],
            [TestBuilders.Warrior(101, "Kovalayan", health: 400, aggression: 100, speed: 50)])
        {
            Tuning = NoMishap with { StartOffsetX = 60 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        PressAfterFirstBlood(battle);

        // What is measured is step speed: of two warriors with the same Speed value, the one running with
        // his back turned advances more slowly. Looking at the blows would be misleading — the chaser
        // stops while striking and can fall behind in net terms (see Battle.CanAdvanceOn).
        var compared = false;

        for (int i = 0; i < 200 && battle.Step(); i++)
        {
            CombatantSnapshot fleeing = battle.SnapshotOf(new WarriorId(1));
            CombatantSnapshot chasing = battle.SnapshotOf(new WarriorId(101));

            if (fleeing.State != CombatState.Retreating || chasing.Speed <= 0)
            {
                continue;
            }

            Assert.True(
                fleeing.Speed < chasing.Speed,
                $"The fleer did not slow down: {fleeing.Speed} vs {chasing.Speed}");

            compared = true;
            break;
        }

        Assert.True(compared, "No tick was found where the chase could be measured.");
    }

    // ------------------------------------------------------------------ throwing

    private static BattleSetup Thrower(double startOffset, ThrownWeapon? thrown = null) => new(
        [TestBuilders.Warrior(1, "Hedef", health: 400, aggression: 0, speed: 50)],
        [
            TestBuilders.Warrior(
                101,
                "Thrower",
                aggression: 100,
                accuracy: 100,
                speed: 1,
                thrown: thrown ?? ThrownWeapon.Shuriken()),
        ])
    {
        Tuning = NoMishap with { StartOffsetX = startOffset },
    };

    /// <summary>
    /// A projectile reaches a target outside melee reach — the far half of the arena is no longer a safe
    /// zone.
    /// </summary>
    [Fact]
    public void AThrownWeaponReachesATargetOutOfMeleeRange()
    {
        var battle = new Battle(Thrower(startOffset: 250), new FixedRandom(0.0));
        battle.Run();

        Assert.Contains(battle.Events, e => e is ProjectileLaunched);
        Assert.Contains(battle.Events, e => e is ProjectileHit);
    }

    /// <summary>A projectile is not resolved instantly: the time it spends in the air shows in the event stream.</summary>
    [Fact]
    public void AProjectileSpendsTimeInTheAir()
    {
        var battle = new Battle(Thrower(startOffset: 300), new FixedRandom(0.0));
        battle.Run();

        ProjectileLaunched launched = battle.Events.OfType<ProjectileLaunched>().First();
        ProjectileHit hit = battle.Events.OfType<ProjectileHit>().First();

        Assert.True(launched.FlightSeconds > 0);
        Assert.True(hit.AtSeconds > launched.AtSeconds);
    }

    /// <summary>The projectiles run out; when they do, the warrior is left with melee alone.</summary>
    /// <remarks>
    /// The target is fleeing: if he closed, he would enter melee reach and the thrower would switch to
    /// the sword before running out of ammo. The range is deliberately larger than the arena too — what
    /// is measured is the ammo running out, not the range falling short.
    /// </remarks>
    [Fact]
    public void AThrowerRunsOutOfAmmunition()
    {
        ThrownWeapon twoShots = ThrownWeapon.Shuriken() with { Ammo = 2, Range = 4000 };

        var battle = new Battle(Thrower(startOffset: 250, twoShots), new FixedRandom(0.0));

        // The first projectile finds the target and unlocks the key; the second comes from behind while he flees.
        PressAfterFirstBlood(battle);
        battle.Run();

        Assert.Equal(2, battle.Events.OfType<ProjectileLaunched>().Count());
    }

    /// <summary>A projectile cannot reach a target who leaves the field during the flight.</summary>
    [Fact]
    public void AProjectileMissesATargetThatLeftTheArena()
    {
        // From the edge of the range, with a slow projectile: the target flees while it is in the air.
        ThrownWeapon slow = ThrownWeapon.Shuriken() with { Speed = 60, Range = 900 };

        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Fleer", health: 400, aggression: 0, speed: 100)],
            [
                TestBuilders.Warrior(
                    101, "Thrower", aggression: 100, accuracy: 100, speed: 1, thrown: slow),
            ])
        {
            Tuning = NoMishap with { StartOffsetX = 420 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));

        // The first projectile finds the target — it is also what unlocks the key. What is measured is the one AFTER it.
        PressAfterFirstBlood(battle);
        battle.Run();

        Assert.Contains(battle.Events, e => e is ProjectileLaunched);
        Assert.Contains(battle.Events, e => e is ProjectileMissed);
    }

    // ---------------------------------------------------------------- escape die

    /// <summary>
    /// The escape die wounds but <b>does not kill</b>: it does not push health below 1.
    /// </summary>
    /// <remarks>
    /// Its purpose is not death but removing the case of "I got out without paying anything". If it could
    /// kill, it would look to the player like a death for no reason — there is nobody striking on screen.
    /// </remarks>
    [Fact]
    public void TheEscapeMishapWoundsButNeverKills()
    {
        var mishaps = 0;

        for (ulong seed = 1; seed <= 200; seed++)
        {
            // The contact that unlocks the key is harmless: the other side strikes with a zero-damage
            // weapon (MinimumDamage is 0 too). So that the only thing measured is the escape die — the
            // fight having started is a precondition, not the value being measured.
            var setup = new BattleSetup(
                [TestBuilders.Warrior(1, "Fleer", health: 4, aggression: 0)],
                [
                    TestBuilders.Warrior(
                        101, "Slow", health: 400, aggression: 100, speed: 1,
                        weapon: Harmless),
                ])
            {
                Tuning = TestBuilders.PointBlank with
                {
                    EscapeMishapChance = 1.0,
                    MinimumDamage = 0,
                },
            };

            var battle = new Battle(setup, new Domina.Core.Rng.SeededRandom(seed));
            while (!battle.ContactMade && battle.Step()) { }
            if (seed == 6) { foreach (var ev in battle.Events) Console.WriteLine($"DBG {ev.AtSeconds:F2} {ev}"); }
            PressAfterFirstBlood(battle);
            BattleResult result = battle.Run();

            WarriorBattleSummary summary = result.SummaryFor(new WarriorId(1));

            Assert.True(summary.Escaped);
            Assert.False(summary.Died);
            Assert.True(summary.HealthRemaining >= 1);

            mishaps += battle.Events.OfType<EscapeMishap>().Count();
        }

        Assert.Equal(200, mishaps);
    }

    /// <summary>With the die off the exit is spotless — the price really does come from the die.</summary>
    [Fact]
    public void WithoutTheMishapRollLeavingIsClean()
    {
        // Contact is made from a distance: pulling out in melee would mean a free hit and "a spotless
        // exit" could not be measured.
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(
                    1, "Fleer", health: 100, aggression: 100, accuracy: 100,
                    thrown: ThrownWeapon.Shuriken()),
            ],
            [TestBuilders.Warrior(101, "Slow", health: 400, aggression: 0, speed: 1)])
        {
            Tuning = NoMishap with { StartOffsetX = 400 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        PressAfterFirstBlood(battle);
        BattleResult result = battle.Run();

        Assert.DoesNotContain(battle.Events, e => e is EscapeMishap);
        Assert.Equal(100, result.SummaryFor(new WarriorId(1)).HealthRemaining);
    }
}
