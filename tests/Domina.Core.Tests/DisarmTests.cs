using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Dropping the weapon is armour's second answer: plate does not only stop the blow, it breaks the
/// striker's grip too. These tests tie down the rule's two sources (striking armour, being caught), its
/// three limits (bare flesh does not disarm, a projectile does not disarm, fists do not fall) and
/// <b>picking the dropped weapon up</b> from the ground.
/// </summary>
public class DisarmTests
{
    /// <summary>The setting that isolates disarming: the dismemberment and stun branches are off.</summary>
    /// <remarks>
    /// All three come out of the same strike. Left open, the defender would fall on the very first blow
    /// and the disarm die would never get its turn.
    /// </remarks>
    private static CombatTuning DisarmOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDisarmChance = 1.0,
        CatchDisarmChance = 0,
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        StallGuardSeconds = 12,
    };

    /// <summary>The setting with picking up disabled — it isolates the drop's own consequence.</summary>
    private static CombatTuning NoPickup { get; } = DisarmOnly with { WeaponPickupRadius = 0 };

    /// <summary>The setting where the disarm die never holds — the control side.</summary>
    private static CombatTuning NoDisarm { get; } = DisarmOnly with { BaseDisarmChance = 0 };

    /// <summary>Cutting, one-handed, light: so that what is lost when it drops is visible.</summary>
    private static Weapon Blade { get; } =
        new("Test-Katana", WeaponClass.Cutting, 20, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>The blunt version of the same weapon — the only difference is the class, so the class axis is isolated.</summary>
    private static Weapon Club { get; } =
        new("Test-Tetsubo", WeaponClass.Blunt, 20, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>Hard plate: the disarm die feeds on the struck piece's resistance.</summary>
    private static Armor Plate { get; } =
        Armor.Uniform("Test plate", new ArmorPiece("Test piece", 0, DismembermentResistance: 1.0, Weight: 0));

    private static BattleSetup Bout(
        Weapon attackerWeapon,
        Armor? defenderArmor = null,
        CombatTuning? tuning = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Armoured",
                health: 4000,
                aggression: 0,
                weapon: Weapon.Fists(),
                armor: defenderArmor ?? Plate),
        ],
        [TestBuilders.Warrior(101, "Vuran", health: 4000, aggression: 100, weapon: attackerWeapon)])
        {
            Tuning = tuning ?? NoPickup,
        };

    /// <summary>A strike landing on armour knocks the attacker's weapon out of his hand.</summary>
    [Fact]
    public void AnArmoredBlowKnocksTheWeaponLoose()
    {
        var battle = new Battle(Bout(Blade), new FixedRandom(0.0));
        battle.Run();

        WeaponDropped dropped = battle.Events.OfType<WeaponDropped>().First();

        // Nobody disarmed him: what breaks the grip is the blow rebounding off the plate.
        Assert.Equal(new WarriorId(101), dropped.Warrior);
        Assert.Null(dropped.Disarmer);
        Assert.Equal(Blade.Name, dropped.Weapon);
    }

    /// <summary>A strike landing on a bare region never disarms — the rule limits itself.</summary>
    /// <remarks>
    /// Hardness is read from the struck piece's dismemberment resistance, and a bare region's resistance
    /// is zero. Without this limit a warrior fighting an unarmoured enemy would lose his weapon too, and
    /// the rule would stop being armour's answer.
    /// </remarks>
    [Fact]
    public void ABlowOnBareFleshNeverDisarms()
    {
        var battle = new Battle(Bout(Blade, Armor.None()), new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<WeaponDropped>());
    }

    /// <summary>A blunt weapon's tendency to leave the hand is a fifth of a cutting weapon's.</summary>
    /// <remarks>
    /// The die feeds on the class; the test ties down not the number but the <b>order</b>: striking the
    /// same plate with the same die, a blunt weapon must stay in the palm while a cutting one falls.
    /// </remarks>
    [Fact]
    public void ABluntWeaponKeepsTheGripWhereABladeLosesIt()
    {
        // 0.3: below the cutting weapon's chance (1.0 × 1.0 × 1.0), above the blunt one's (0.2).
        var blade = new Battle(Bout(Blade), new FixedRandom(0.3));
        blade.Run();

        var club = new Battle(Bout(Club), new FixedRandom(0.3));
        club.Run();

        Assert.NotEmpty(blade.Events.OfType<WeaponDropped>());
        Assert.Empty(club.Events.OfType<WeaponDropped>());
    }

    /// <summary>A warrior who drops his weapon carries on with his fists.</summary>
    [Fact]
    public void ADisarmedWarriorKeepsFightingWithFists()
    {
        var battle = new Battle(Bout(Blade), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WeaponDropped dropped = battle.Events.OfType<WeaponDropped>().First();
        List<AttackLanded> after =
        [
            .. battle.Events.OfType<AttackLanded>()
                .Where(a => a.Attacker == dropped.Warrior && a.AtSeconds > dropped.AtSeconds),
        ];

        Assert.NotEmpty(after);

        // The strikes after the drop carry the fists' damage: lower than the cutting weapon's.
        double beforeDamage = battle.Events.OfType<AttackLanded>()
            .First(a => a.Attacker == dropped.Warrior).Damage;
        Assert.All(after, a => Assert.True(a.Damage < beforeDamage));

        WarriorBattleSummary attacker = result.Summaries.First(s => s.Id == new WarriorId(101));
        Assert.True(attacker.Disarmed);
        Assert.Equal(1, attacker.TimesDisarmed);
    }

    /// <summary>A weapon drops once; fists have nothing to drop.</summary>
    [Fact]
    public void FistsCannotBeDropped()
    {
        var battle = new Battle(Bout(Blade), new FixedRandom(0.0));
        battle.Run();

        Assert.Single(battle.Events.OfType<WeaponDropped>());
    }

    /// <summary>A dropped weapon is not destroyed: its owner can walk to it and take it back.</summary>
    /// <remarks>
    /// This is the whole return of choosing dropping over breaking. The price is not a permanent loss
    /// but the time spent walking to the weapon with only fists.
    /// </remarks>
    [Fact]
    public void TheOwnerCanWalkBackToItsWeapon()
    {
        // Two settings: the warrior opposite is armed (so he does not take the fallen sword) and the
        // weapon falls right at his feet. Normally the weapon is flung behind the other man and in a duel
        // it cannot be reached (see Battle.DropPoint); what is measured here is not the geometry but the
        // owner being able to take his weapon back.
        BattleSetup setup = new(
            [
                TestBuilders.Warrior(1, "Armoured", health: 4000, aggression: 0, weapon: Club, armor: Plate),
            ],
            [TestBuilders.Warrior(101, "Vuran", health: 4000, aggression: 100, weapon: Blade)])
        {
            Tuning = DisarmOnly with
            {
                StallGuardSeconds = 30,
                WeaponDropDistance = 0,
                WeaponPickupRadius = 120,
            },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WeaponPickedUp picked = battle.Events.OfType<WeaponPickedUp>().First();

        Assert.Equal(new WarriorId(101), picked.Warrior);
        Assert.Equal(Blade.Name, picked.Weapon);

        WarriorBattleSummary attacker = result.Summaries.First(s => s.Id == new WarriorId(101));
        Assert.True(attacker.WeaponsPickedUp > 0);
    }

    /// <summary><b>Anyone</b> empty-handed can pick a weapon off the ground — the enemy too.</summary>
    /// <remarks>
    /// Nobody asks whose the weapon is: it is a blade lying in the arena. So the rule is not one-way —
    /// the weapon you knock out can end up in the enemy's hand.
    /// </remarks>
    [Fact]
    public void AnyEmptyHandedWarriorCanTakeIt()
    {
        var battle = new Battle(
            Bout(Blade, tuning: DisarmOnly with { StallGuardSeconds = 30 }),
            new FixedRandom(0.0));
        battle.Run();

        // The armoured side is fighting with fists: he counts as empty-handed and walks to the fallen sword.
        Assert.Contains(
            battle.Events.OfType<WeaponPickedUp>(),
            p => p.Warrior == new WarriorId(1));
    }

    /// <summary>A warrior with a weapon in hand neither picks up nor searches.</summary>
    /// <remarks>
    /// Without this limit the warriors would constantly collect better weapons and the fight would turn
    /// into a looting round.
    /// </remarks>
    [Fact]
    public void AnArmedWarriorNeverPicksAnythingUp()
    {
        BattleSetup setup = new(
            [
                TestBuilders.Warrior(1, "Armoured", health: 4000, aggression: 0, weapon: Club, armor: Plate),
            ],
            [TestBuilders.Warrior(101, "Vuran", health: 4000, aggression: 100, weapon: Blade)])
        {
            Tuning = DisarmOnly with { StallGuardSeconds = 30 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<WeaponDropped>());
        Assert.DoesNotContain(
            battle.Events.OfType<WeaponPickedUp>(),
            p => p.Warrior == new WarriorId(1));
    }

    /// <summary>A caught weapon can be levered out of the palm — and that replaces the bind.</summary>
    /// <remarks>
    /// If disarming stacked on top of the bind, the catching implement would take both the open window
    /// and the weapon on a single die; what it loses in damage would be more than repaid.
    /// </remarks>
    [Fact]
    public void ACaughtWeaponCanBeTornLooseInsteadOfBound()
    {
        CombatTuning catching = NoPickup with
        {
            BaseDisarmChance = 0,
            BaseCatchChance = 1.0,
            CatchDisarmChance = 1.0,
        };

        BattleSetup setup = new(
            [
                TestBuilders.Warrior(
                    1,
                    "Yakalayan",
                    health: 4000,
                    aggression: 0,
                    weapon: new Weapon("Test-Jitte", WeaponClass.Blunt, 4, false, 1.0)
                    {
                        CatchSkill = 1.0,
                    }),
            ],
            [TestBuilders.Warrior(101, "Attacker", health: 4000, aggression: 100, weapon: Blade)])
        {
            Tuning = catching,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        WeaponDropped dropped = battle.Events.OfType<WeaponDropped>().First();

        // Here the disarmer is known: the warrior holding the hook.
        Assert.Equal(new WarriorId(101), dropped.Warrior);
        Assert.Equal(new WarriorId(1), dropped.Disarmer);

        // With the weapon gone the bind is released too: no bind event comes out at that moment.
        Assert.DoesNotContain(
            battle.Events.OfType<AttackCaught>(),
            c => Math.Abs(c.AtSeconds - dropped.AtSeconds) < 1e-9);
    }

    /// <summary>A projectile knocks nobody's weapon out.</summary>
    /// <remarks>
    /// What breaks the grip is the weapon striking plate and rebounding; a thrown weapon has already
    /// left the hand.
    /// </remarks>
    [Fact]
    public void ProjectilesDisarmNobody()
    {
        BattleSetup setup = new(
            [
                TestBuilders.Warrior(1, "Armoured", health: 4000, aggression: 0, armor: Plate),
            ],
            [
                TestBuilders.Warrior(
                    101,
                    "Atan",
                    health: 4000,
                    aggression: 100,
                    weapon: Weapon.Fists(),
                    thrown: ThrownWeapon.Shuriken()),
            ])
        {
            // The sides start far apart: up close the warrior chooses his fists and no projectile takes off.
            Tuning = NoPickup with { StartOffsetX = 400 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<ProjectileHit>());
        Assert.Empty(battle.Events.OfType<WeaponDropped>());
    }

    /// <summary>Disarming does not touch the persistent state — a fight does not take the warrior's weapon.</summary>
    /// <remarks>
    /// Batch simulation runs the same roster tens of thousands of times; if a fight changed the
    /// persistent state, the second fight would start with the first one's residue.
    /// </remarks>
    [Fact]
    public void DisarmingDoesNotTouchThePermanentWeapon()
    {
        BattleSetup setup = Bout(Blade);
        Warrior attacker = setup.EnemySide[0];

        new Battle(setup, new FixedRandom(0.0)).Run();

        Assert.Equal(Blade.Name, attacker.Weapon.Name);

        var second = new Battle(setup, new FixedRandom(0.0));
        second.Run();

        Assert.NotEmpty(second.Events.OfType<WeaponDropped>());
    }

    /// <summary>With the rule off no weapon drops — the control side holds.</summary>
    [Fact]
    public void TheRuleCanBeTurnedOff()
    {
        var battle = new Battle(Bout(Blade, tuning: NoDisarm), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        Assert.Empty(battle.Events.OfType<WeaponDropped>());
        Assert.All(result.Summaries, s => Assert.False(s.Disarmed));
    }
}
