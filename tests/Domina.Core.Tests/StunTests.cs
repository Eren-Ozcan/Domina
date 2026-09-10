using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The stun is the blunt weapon's reason to exist (GDD §7). A cutting weapon takes limbs
/// (<c>DismembermentFactor</c> 1.0), a blunt one does not (0.15) — in exchange it lands the blow that
/// freezes the warrior. These tests tie down both ends of the trade: a blunt strike freezes, and a
/// frozen warrior neither strikes nor evades.
/// </summary>
public class StunTests
{
    /// <summary>The setting that closes the dismemberment branch — so the stun is measured on its own.</summary>
    /// <remarks>
    /// The two dice are rolled from the same heavy blow; with dismemberment left open the test would
    /// measure a setup in which the stunned warrior also loses a limb, and which rule did what could not
    /// be told apart.
    /// </remarks>
    private static CombatTuning StunOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,

        // The threshold is lowered so the victim can take more than one heavy blow in a single fight:
        // because the hardness/health ratio is fixed, a blow that passes the threshold is also one that
        // kills the victim in a few strikes. What is tested is not the threshold's number but the rule.
        StunSeverityThreshold = 0.05,
        TorsoHitWeight = 100,
        HeadHitWeight = 0,
        ArmHitWeight = 0,
        LegHitWeight = 0,
    };

    /// <summary>A blunt weapon that passes the heavy-blow threshold in one strike.</summary>
    private static Weapon Club { get; } =
        new("Test-Kanabō", WeaponClass.Blunt, 40, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>
    /// The harmless weapon in the victim's hand: so the fight stays one-way.
    /// </summary>
    /// <remarks>
    /// If the victim answers even with his fists, a blow arriving with a charge stuns the striker and the
    /// tests cannot tell who was stunned. What is measured is not an exchange but
    /// the result of a single blow.
    /// </remarks>
    private static Weapon Harmless { get; } =
        new("Test-Sopa", WeaponClass.Cutting, 0, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>The same harmless weapon of the blunt class — it isolates the grip axis.</summary>
    private static Weapon HarmlessClub { get; } =
        new("Test-Sopa (kütük)", WeaponClass.Blunt, 0, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>A cutting weapon of the same hardness — it isolates the class difference.</summary>
    private static Weapon Blade { get; } =
        new("Test-Katana", WeaponClass.Cutting, 40, TwoHanded: false, AttackSeconds: 1.0);

    private static BattleSetup Beating(
        Weapon attackerWeapon,
        Armor? victimArmor = null,
        double victimAggression = 0,
        double victimEvasion = 0,
        CombatTuning? tuning = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Kurban",
                health: 400,
                aggression: victimAggression,
                evasion: victimEvasion,
                weapon: Harmless,
                armor: victimArmor),
        ],
        [TestBuilders.Warrior(101, "Striker", aggression: 100, weapon: attackerWeapon)])
        {
            Tuning = tuning ?? StunOnly,
        };

    /// <summary>A blunt weapon's heavy blow freezes the warrior.</summary>
    [Fact]
    public void ABluntGrievousBlowStunsTheDefender()
    {
        var battle = new Battle(Beating(Club), new FixedRandom(0.0));
        battle.Run();

        WarriorStunned stun = Assert.IsType<WarriorStunned>(
            battle.Events.OfType<WarriorStunned>().FirstOrDefault());

        Assert.Equal(new WarriorId(1), stun.Defender);
        Assert.Equal(new WarriorId(101), stun.Attacker);
        Assert.Equal(StunOnly.StunSeconds, stun.Seconds);
    }

    /// <summary>
    /// The trade itself: a cutting weapon of the same hardness stuns far more rarely.
    /// </summary>
    /// <remarks>
    /// Not the number but the <b>order</b> is tested. The blunt class loses to the cutting one on the
    /// dismemberment multiplier; if it does not win on this axis, a blunt weapon is bad on every axis and
    /// nobody carries one.
    [Fact]
    public void BluntStunsFarMoreOftenThanCutting()
    {
        Assert.True(Club.StunFactor > Blade.StunFactor);
        Assert.True(Club.DismembermentFactor < Blade.DismembermentFactor);

        int blunt = StunsIn(Club);
        int cutting = StunsIn(Blade);

        Assert.True(blunt > cutting, $"Blunt did not come ahead on stunning ({blunt} <= {cutting}).");

        static int StunsIn(Weapon weapon)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(Beating(weapon), new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<WarriorStunned>().Count();
            }

            return total;
        }
    }

    /// <summary>A stunned warrior starts no attack at all during that window.</summary>
    [Fact]
    public void AStunnedWarriorStopsSwinging()
    {
        var battle = new Battle(
            Beating(Club, victimAggression: 100, tuning: StunOnly with { StallGuardSeconds = 6 }),
            new FixedRandom(0.0));

        double stunnedAt = double.NaN;
        int attacksAtStun = 0;

        while (battle.Step())
        {
            if (double.IsNaN(stunnedAt)
                && battle.Events.OfType<WarriorStunned>().FirstOrDefault() is WarriorStunned s)
            {
                stunnedAt = s.AtSeconds;
                attacksAtStun = AttacksBy(battle, new WarriorId(1));
            }

            if (!double.IsNaN(stunnedAt) && battle.ElapsedSeconds >= stunnedAt + StunOnly.StunSeconds)
            {
                break;
            }
        }

        Assert.False(double.IsNaN(stunnedAt), "The victim was never stunned.");
        Assert.Equal(attacksAtStun, AttacksBy(battle, new WarriorId(1)));

        static int AttacksBy(Battle battle, WarriorId id) =>
            battle.Events.OfType<AttackStarted>().Count(e => e.Attacker == id);
    }

    /// <summary>A stunned warrior cannot evade — that is the real price of freezing.</summary>
    [Fact]
    public void AStunnedWarriorCannotDodge()
    {
        // Set so the evasion die holds every time: high Evasion + FixedRandom(0).
        var setup = Beating(Club, victimEvasion: 100);
        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        WarriorStunned stun = battle.Events.OfType<WarriorStunned>().First();

        Assert.DoesNotContain(
            battle.Events.OfType<AttackDodged>(),
            e => e.Defender == new WarriorId(1)
                 && e.AtSeconds > stun.AtSeconds
                 && e.AtSeconds <= stun.AtSeconds + StunOnly.StunSeconds);
    }

    /// <summary>
    /// A warrior pulling out is not stunned: an enemy with a blunt weapon would cancel the player's only
    /// (GDD §5) tek zarla iptal edemez.
    /// </summary>
    [Fact]
    public void RetreatIsNeverCancelledByAStun()
    {
        var battle = new Battle(Beating(Club), new FixedRandom(0.0));

        while (!battle.ContactMade && battle.Step())
        {
        }

        Assert.True(battle.CommandRetreat());

        double commandedAt = battle.ElapsedSeconds;
        battle.Run();

        // No stun AFTER the command. The blow in the same tick landed before the command —
        // it was the blow that unlocked the key.
        Assert.DoesNotContain(
            battle.Events.OfType<WarriorStunned>(),
            e => e.Defender == new WarriorId(1) && e.AtSeconds > commandedAt);

        Assert.Contains(battle.Events.OfType<RetreatStarted>(), e => e.Warrior == new WarriorId(1));
    }

    /// <summary>
    /// A stun <b>does not swallow</b> the flee command, it delays it: when the duration ends the buffered
    /// command is processed and the warrior starts pulling out.
    /// </summary>
    [Fact]
    public void AStunDelaysTheRetreatCommandButDoesNotEatIt()
    {
        var battle = new Battle(Beating(Club), new FixedRandom(0.0));

        // Wait for him to be stunned first, then press the key.
        while (!battle.Events.OfType<WarriorStunned>().Any() && battle.Step())
        {
        }

        Assert.True(battle.CommandRetreat());
        Assert.Contains(battle.Events.OfType<RetreatBuffered>(), e => e.Warrior == new WarriorId(1));

        battle.Run();

        Assert.Contains(battle.Events.OfType<RetreatStarted>(), e => e.Warrior == new WarriorId(1));
    }

    /// <summary>Armour damps the stun too — but less than it stops a cut.</summary>
    /// <remarks>
    /// That is the share's (<see cref="CombatTuning.ArmorStunResistanceShare"/>) only job: blunt force
    /// goes through underneath the plate, so armour's dismemberment resistance does not count one to one
    /// against stunning.
    /// </remarks>
    [Fact]
    public void ArmorDampensStunButLessThanItDampensSevering()
    {
        int bare = StunsWith(Armor.None());
        int plated = StunsWith(Armor.Heavy());

        Assert.True(plated < bare, $"Armour did not reduce stunning ({plated} >= {bare}).");
        Assert.True(plated > 0, "Armour cut stunning off entirely — the share must have slipped to 1.");

        static int StunsWith(Armor armor)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(Beating(Club, victimArmor: armor), new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<WarriorStunned>().Count();
            }

            return total;
        }
    }

    /// <summary>A blow to the head stuns more often — the kabuto's in-combat meaning.</summary>
    [Fact]
    public void AHeadBlowStunsMoreOftenThanATorsoBlow()
    {
        int torso = StunsWithHits(StunOnly);
        int head = StunsWithHits(StunOnly with
        {
            TorsoHitWeight = 0,
            HeadHitWeight = 100,
        });

        Assert.True(head > torso, $"The head blow did not come ahead ({head} <= {torso}).");

        static int StunsWithHits(CombatTuning tuning)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(Beating(Club, tuning: tuning), new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<WarriorStunned>().Count();
            }

            return total;
        }
    }

    /// <summary>
    /// The third trigger of dropping a weapon: a stunned warrior lets go of his own.
    /// </summary>
    /// <remarks>
    /// The other two triggers spend the <b>striker's</b> weapon (a blow bouncing off armour, a blade
    /// caught in a hook). This is the first in which the man who was hit is the one left empty-handed,
    /// so the event's owner is the defender and its disarmer is the striker.
    /// </remarks>
    [Fact]
    public void AStunnedWarriorLetsGoOfHisWeapon()
    {
        var battle = new Battle(Beating(Club), new FixedRandom(0.0));
        battle.Run();

        WeaponDropped dropped = Assert.IsType<WeaponDropped>(
            battle.Events.OfType<WeaponDropped>().FirstOrDefault());

        Assert.Equal(new WarriorId(1), dropped.Warrior);
        Assert.Equal(new WarriorId(101), dropped.Disarmer);

        WarriorStunned stun = battle.Events.OfType<WarriorStunned>().First();
        Assert.True(dropped.AtSeconds >= stun.AtSeconds);
    }

    /// <summary>
    /// A blunt weapon stays in the stunned hand where an edge does not.
    /// </summary>
    /// <remarks>
    /// Not the number but the <b>order</b> is tested, as with the stun itself: the die is read from the
    /// weapon of the man who was stunned, so the class that rebounds keeps its grip. This is the blunt
    /// class's third gain — it wins the trade both as the striker and as the man struck.
    /// </remarks>
    [Fact]
    public void ABluntWeaponStaysInTheStunnedHand()
    {
        int cutting = DropsWhenVictimCarries(Harmless);
        int blunt = DropsWhenVictimCarries(HarmlessClub);

        Assert.True(cutting > 0, "The rule never fired for the cutting victim.");
        Assert.True(blunt < cutting, $"Blunt did not hold on to its weapon ({blunt} >= {cutting}).");

        static int DropsWhenVictimCarries(Weapon victimWeapon)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var setup = new BattleSetup(
                    [TestBuilders.Warrior(1, "Kurban", health: 400, aggression: 0, weapon: victimWeapon)],
                    [TestBuilders.Warrior(101, "Striker", aggression: 100, weapon: Club)])
                {
                    Tuning = StunOnly,
                };

                var battle = new Battle(setup, new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<WeaponDropped>().Count(e => e.Warrior == new WarriorId(1));
            }

            return total;
        }
    }

    /// <summary>A warrior already empty-handed cannot drop a weapon a second time.</summary>
    [Fact]
    public void AWarriorWithNoWeaponDropsNothingWhenStunnedAgain()
    {
        var battle = new Battle(Beating(Club), new FixedRandom(0.0));
        battle.Run();

        Assert.True(battle.Events.OfType<WarriorStunned>().Count() > 1);
        Assert.Single(battle.Events.OfType<WeaponDropped>(), e => e.Warrior == new WarriorId(1));
    }

    /// <summary>A light blow below the threshold does not stun — the stun is a heavy-blow branch.</summary>
    [Fact]
    public void ALightBlowNeverStuns()
    {
        var pinprick = new Weapon("Test-Tantō", WeaponClass.Blunt, 6, TwoHanded: false, AttackSeconds: 0.4);
        var battle = new Battle(Beating(pinprick), new FixedRandom(0.0));
        battle.Run();

        Assert.Contains(battle.Events.OfType<AttackLanded>(), e => e.Defender == new WarriorId(1));
        Assert.Empty(battle.Events.OfType<WarriorStunned>());
    }
}
