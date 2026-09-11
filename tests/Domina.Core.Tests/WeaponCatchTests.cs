using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Sword catching fills the gap GDD §4 left when it rejected the shield: instead of a hand-carried
/// shield, an implement that <b>stops</b> the incoming weapon. These tests tie down both ends of the
/// rule — a caught strike does no damage, the caught warrior is left exposed — and keep the three
/// places it must not bite (from behind, projectiles, fleeing) closed.
/// </summary>
public class WeaponCatchTests
{
    /// <summary>The catch die always holds; what is measured is not the number but the rule.</summary>
    /// <remarks>
    /// The dismemberment and stun branches are switched off: all three dice are rolled from the same
    /// strike, and left open the test would measure the outcome tree's behaviour rather than catching's.
    /// </remarks>
    private static CombatTuning CatchOnly { get; } = TestBuilders.PointBlank with
    {
        BaseCatchChance = 1.0,
        BaseDismembermentChance = 0,
        BaseStunChance = 0,

        // The weapon falling out of the hand comes out of the same catch and takes the bind's PLACE; left
        // open, the tests in this file would never see the bind. Disarming itself
        // DisarmTests'in konusu.
        CatchDisarmChance = 0,
    };

    /// <summary>The setting where the catch die never holds — the control side.</summary>
    private static CombatTuning NoCatch { get; } = CatchOnly with { BaseCatchChance = 0 };

    /// <summary>The catching implement: it holds the incoming cutting weapon.</summary>
    private static Weapon Hook { get; } =
        new("Test-Jitte", WeaponClass.Blunt, 4, TwoHanded: false, AttackSeconds: 1.0)
        {
            CatchSkill = 1.0,
        };

    /// <summary>Yakalanan silah: tek el kesici.</summary>
    private static Weapon Blade { get; } =
        new("Test-Katana", WeaponClass.Cutting, 30, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>The two-handed version of the same weapon — it isolates the answer of leverage.</summary>
    private static Weapon HeavyBlade { get; } =
        new("Test-Nodachi", WeaponClass.Cutting, 30, TwoHanded: true, AttackSeconds: 1.0);

    private static BattleSetup Bout(
        Weapon defenderWeapon,
        Weapon attackerWeapon,
        double defenderAccuracy = 60,
        double defenderEvasion = 0,
        double defenderStamina = 100,
        CombatTuning? tuning = null,
        WarriorClass klass = WarriorClass.Torite) => new(
        [
            TestBuilders.Warrior(
                1,
                "Yakalayan",
                health: 400,
                aggression: 0,
                evasion: defenderEvasion,
                accuracy: defenderAccuracy,
                stamina: defenderStamina,
                weapon: defenderWeapon,
                klass: klass),
        ],
        [TestBuilders.Warrior(101, "Vuran", aggression: 100, weapon: attackerWeapon)])
        {
            Tuning = tuning ?? CatchOnly,
        };

    /// <summary>A caught strike does no damage at all.</summary>
    [Fact]
    public void ACaughtAttackLandsNoDamage()
    {
        // Plenty of stamina is given: the cost itself is the subject of a separate test
        // (<see cref="CatchingRequiresStamina"/>). The default 100 stamina is only enough for a few
        // catches, after which strikes start getting through — what is measured here is not the resource
        // but the caught strike doing no damage.
        var battle = new Battle(Bout(Hook, Blade, defenderStamina: 10000), new FixedRandom(0.0));
        battle.Run();

        AttackCaught caught = Assert.IsType<AttackCaught>(
            battle.Events.OfType<AttackCaught>().FirstOrDefault());

        Assert.Equal(new WarriorId(1), caught.Defender);
        Assert.Equal(new WarriorId(101), caught.Attacker);
        Assert.Equal(CatchOnly.CatchBindSeconds, caught.BindSeconds);

        // The catcher's health was not touched at all: catching does not reduce damage the way evasion
        // does, it erases the strike entirely.
        Assert.DoesNotContain(
            battle.Events.OfType<AttackLanded>(),
            e => e.Defender == new WarriorId(1));
    }

    /// <summary>A warrior whose weapon is caught starts no new attack during that window.</summary>
    [Fact]
    public void ABoundAttackerStopsSwinging()
    {
        var battle = new Battle(
            Bout(Hook, Blade, tuning: CatchOnly with { StallGuardSeconds = 6 }),
            new FixedRandom(0.0));

        double caughtAt = double.NaN;
        int attacksAtCatch = 0;

        while (battle.Step())
        {
            if (double.IsNaN(caughtAt)
                && battle.Events.OfType<AttackCaught>().FirstOrDefault() is AttackCaught c)
            {
                caughtAt = c.AtSeconds;
                attacksAtCatch = AttacksBy(battle, new WarriorId(101));
            }

            if (!double.IsNaN(caughtAt)
                && battle.ElapsedSeconds >= caughtAt + CatchOnly.CatchBindSeconds)
            {
                break;
            }
        }

        Assert.False(double.IsNaN(caughtAt), "No strike was caught.");
        Assert.Equal(attacksAtCatch, AttacksBy(battle, new WarriorId(101)));

        static int AttacksBy(Battle battle, WarriorId id) =>
            battle.Events.OfType<AttackStarted>().Count(e => e.Attacker == id);
    }

    /// <summary>
    /// Catching's real return: a bound warrior cannot evade.
    /// </summary>
    /// <remarks>
    /// If the rule only erased damage, catching would be an expensive evasion. Without the window it
    /// opens, the jitte's low damage is made up for nowhere.
    /// </remarks>
    [Fact]
    public void ABoundWarriorCannotDodge()
    {
        // The catcher strikes too; the bound warrior's evasion die will be tried.
        BattleSetup setup = Bout(Hook, Blade) with
        {
            PlayerSide =
            [
                TestBuilders.Warrior(
                    1,
                    "Yakalayan",
                    health: 400,
                    aggression: 100,
                    accuracy: 60,
                    weapon: Hook,
                    klass: WarriorClass.Torite),
            ],
            EnemySide =
            [
                TestBuilders.Warrior(101, "Vuran", aggression: 100, evasion: 100, weapon: Blade),
            ],
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        AttackCaught caught = battle.Events.OfType<AttackCaught>().First();

        Assert.DoesNotContain(
            battle.Events.OfType<AttackDodged>(),
            e => e.Defender == new WarriorId(101)
                 && e.AtSeconds > caught.AtSeconds
                 && e.AtSeconds <= caught.AtSeconds + CatchOnly.CatchBindSeconds);
    }

    /// <summary>
    /// A warrior with no class catches nothing — even with the hook in his hand.
    /// </summary>
    /// <remarks>
    /// The hard zero of the <c>class × implement</c> product (docs/GDD.md §4). It is what keeps the
    /// class from being a decoration on top of the equipment: catching is an active skill, so handing a
    /// jitte to a recruit buys nothing at all.
    /// </remarks>
    [Fact]
    public void AWarriorWithNoClassNeverCatches()
    {
        var battle = new Battle(
            Bout(Hook, Blade, klass: WarriorClass.None), new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<AttackCaught>());
    }

    /// <summary>
    /// A catching warrior holding the wrong implement still catches — badly.
    /// </summary>
    /// <remarks>
    /// The soft end of the same product: the identity belongs to the warrior, so a torite who is
    /// disarmed weakens instead of becoming someone else. What is tested is the <b>order</b>, not the
    /// number — the hook must stay clearly ahead of the bare sword, or the equipment decision dies.
    /// </remarks>
    [Fact]
    public void AWrongImplementCatchesWeaklyButNotNever()
    {
        CombatTuning tuning = CatchOnly with { BaseCatchChance = 0.5 };

        int withHook = CatchesWith(Hook, tuning);
        int withBlade = CatchesWith(Blade, tuning);

        Assert.True(withBlade > 0, "The catching warrior caught nothing with the wrong implement.");
        Assert.True(
            withBlade < withHook,
            $"The wrong implement was not weaker ({withBlade} >= {withHook}).");

        static int CatchesWith(Weapon weapon, CombatTuning tuning)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(Bout(weapon, Blade, tuning: tuning), new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<AttackCaught>().Count();
            }

            return total;
        }
    }

    /// <summary>
    /// A two-handed weapon is harder to catch — catching's own answer.
    /// </summary>
    /// <remarks>
    /// Not the number but the <b>order</b> is tested. Without the return for leverage, the jitte would be
    /// the right choice in every matchup and choosing a heavy weapon would become a free loss.
    /// </remarks>
    [Fact]
    public void TwoHandedWeaponsAreHarderToCatch()
    {
        CombatTuning tuning = CatchOnly with { BaseCatchChance = 0.5, CatchTwoHandedFactor = 0.5 };

        int oneHanded = CatchesAgainst(Blade, tuning);
        int twoHanded = CatchesAgainst(HeavyBlade, tuning);

        Assert.True(
            twoHanded < oneHanded,
            $"The two-handed weapon was not harder to catch ({twoHanded} >= {oneHanded}).");

        static int CatchesAgainst(Weapon weapon, CombatTuning tuning)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(Bout(Hook, weapon, tuning: tuning), new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<AttackCaught>().Count();
            }

            return total;
        }
    }

    /// <summary>Fists are not caught: there is nothing to hold.</summary>
    [Fact]
    public void FistsCannotBeCaught()
    {
        Assert.Equal(0, Weapon.Fists().CatchFactor);

        var battle = new Battle(Bout(Hook, Weapon.Fists()), new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<AttackCaught>());
    }

    /// <summary>A projectile in the air is not caught — the rule belongs to melee alone.</summary>
    [Fact]
    public void ProjectilesCannotBeCaught()
    {
        BattleSetup setup = new(
            [
                TestBuilders.Warrior(
                    1, "Yakalayan", health: 400, aggression: 0, weapon: Hook, klass: WarriorClass.Torite),
            ],
            [
                TestBuilders.Warrior(
                    101,
                    "Thrower",
                    aggression: 100,
                    weapon: Blade,
                    thrown: ThrownWeapon.Shuriken()),
            ])
        {
            Tuning = CatchOnly with { StartOffsetX = 400 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<ProjectileHit>());
        Assert.DoesNotContain(
            battle.Events.OfType<AttackCaught>(),
            e => battle.Events.OfType<ProjectileHit>().Any(h => h.AtSeconds == e.AtSeconds));
    }

    /// <summary>Stamina yetmiyorsa yakalama denenmez.</summary>
    /// <remarks>
    /// The cost's counterpart in measurement was this: with the cost at zero, victory was 76.85%, at 16
    /// it was 72.63% — and the number of catches stays almost the same (2.72 against 2.75). So the cost
    /// bites not by making catches rarer but by <b>tiring</b> the warrior.
    /// </remarks>
    [Fact]
    public void CatchingRequiresStamina()
    {
        var battle = new Battle(
            Bout(Hook, Blade, defenderStamina: 0, tuning: CatchOnly with { StallGuardSeconds = 4 }),
            new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<AttackCaught>());
    }

    /// <summary>
    /// A warrior pulling out does not catch: no new die is placed on top of the escape promise.
    /// </summary>
    /// <remarks>
    /// The counterpart of the protective rule in stun (GDD §5). A warrior running with his back turned
    /// does not go into the other man's weapon; if the rule worked here too, fleeing would not be an exit
    /// but a new combat move.
    /// </remarks>
    [Fact]
    public void ARetreatingWarriorNeverCatches()
    {
        var battle = new Battle(Bout(Hook, Blade), new FixedRandom(0.0));

        while (!battle.ContactMade && battle.Step())
        {
        }

        Assert.True(battle.CommandRetreat());

        double commandedAt = battle.ElapsedSeconds;
        battle.Run();

        Assert.DoesNotContain(
            battle.Events.OfType<AttackCaught>(),
            e => e.Defender == new WarriorId(1) && e.AtSeconds > commandedAt);
    }

    /// <summary>
    /// Catching hangs on Accuracy, not on Evasion.
    /// </summary>
    /// <remarks>
    /// If both defensive axes fed off the same stat, the equipment decision would be a copy of the stat
    /// decision and the jitte would stay only "the second defence of a warrior with high evasion".
    /// </remarks>
    [Fact]
    public void CatchingScalesWithAccuracyNotEvasion()
    {
        CombatTuning tuning = CatchOnly with { BaseCatchChance = 0.4 };

        int lowAccuracy = CatchesWith(accuracy: 0, evasion: 0, tuning);
        int highAccuracy = CatchesWith(accuracy: 100, evasion: 0, tuning);

        Assert.True(
            highAccuracy > lowAccuracy,
            $"The accuracy axis did not work ({highAccuracy} <= {lowAccuracy}).");

        static int CatchesWith(double accuracy, double evasion, CombatTuning tuning)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(
                    Bout(Hook, Blade, defenderAccuracy: accuracy, defenderEvasion: evasion, tuning: tuning),
                    new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<AttackCaught>().Count();
            }

            return total;
        }
    }

    /// <summary>
    /// Catching is tried <b>before</b> evasion.
    /// </summary>
    /// <remarks>
    /// The order decides whether the rule bites: with evasion first, catching would almost never fire on
    /// a warrior with high evasion and the jitte would be "the last resort of the one who cannot evade" —
    /// whereas its real return is locking the attacker down.
    /// </remarks>
    [Fact]
    public void CatchIsTriedBeforeDodge()
    {
        // A setup where the evasion die also holds: with both open, catching must win. Plenty of
        // stamina, or an exhausted catch leaves the turn to evasion and the test measures the resource's
        // behaviour rather than the order's.
        var battle = new Battle(
            Bout(Hook, Blade, defenderEvasion: 100, defenderStamina: 10000),
            new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<AttackCaught>());
        Assert.DoesNotContain(
            battle.Events.OfType<AttackDodged>(),
            e => e.Defender == new WarriorId(1));
    }

    /// <summary>
    /// With catching off the same setup lets the strike through — the control side.
    /// </summary>
    /// <remarks>
    /// This is the only test that shows the rule really does something: with catching on there is no
    /// damage, with it off there is. Without the two side by side, the "no damage" result could just as
    /// well come from a coincidence in the setup.
    /// </remarks>
    [Fact]
    public void WithoutTheRuleTheSameBlowLands()
    {
        var battle = new Battle(Bout(Hook, Blade, tuning: NoCatch), new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<AttackCaught>());
        Assert.Contains(battle.Events.OfType<AttackLanded>(), e => e.Defender == new WarriorId(1));
    }
}
