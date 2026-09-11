using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The class half of the <c>class × implement</c> product (docs/GDD.md §4). Catching's own end of it
/// is tied down in <see cref="WeaponCatchTests"/>; what is kept here is the rest of the rule — the
/// dose the classless still carries, the throwing hand the range class buys, and the limb-class
/// fitness matrix a maimed warrior chooses from.
/// </summary>
public class ClassLayerTests
{
    /// <summary>The setting that isolates the dose: the dismemberment and stun branches are off.</summary>
    private static CombatTuning PoisonOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        StallGuardSeconds = 12,
    };

    /// <summary>A light blade whose whole return is the dose on it.</summary>
    private static Weapon Fang { get; } =
        new("Test-Poisoned", WeaponClass.Cutting, 5, TwoHanded: false, AttackSeconds: 1.0)
        {
            Poison = 1.0,
        };

    /// <summary>
    /// A warrior of no poison class still poisons — with a smaller dose.
    /// </summary>
    /// <remarks>
    /// This is the deliberate difference from catching (decided 2026-09-10): poison is on the blade, so
    /// it is scaled rather than zeroed. Zeroed, a poisoned tantō would be dead equipment until a
    /// facility stood, and the poison numbers already locked in GDD §7 would have gone with it.
    /// </remarks>
    [Fact]
    public void AWarriorOfNoPoisonClassCarriesASmallerDose()
    {
        double trained = FirstDose(WarriorClass.Dokushi);
        double untrained = FirstDose(WarriorClass.None);

        Assert.True(untrained > 0, "The classless warrior's blade left no dose at all.");
        Assert.Equal(trained * PoisonOnly.UnclassedPoisonFactor, untrained, 6);

        static double FirstDose(WarriorClass klass)
        {
            BattleSetup setup = new(
                [TestBuilders.Warrior(1, "Target", health: 400, aggression: 0, weapon: Weapon.Fists())],
                [
                    TestBuilders.Warrior(
                        101, "Poisoner", aggression: 100, weapon: Fang, klass: klass),
                ])
            {
                Tuning = PoisonOnly,
            };

            var battle = new Battle(setup, new FixedRandom(0.0));
            battle.Run();

            return battle.Events.OfType<WarriorPoisoned>().First().Dose;
        }
    }

    /// <summary>
    /// The range class throws with a fuller hand than everyone else.
    /// </summary>
    /// <remarks>
    /// The throwing slot stays open to every warrior (docs/COMPARISON-DOMINA.md §5) — what the class
    /// buys today is the hit chance, and later the yumi. The <b>order</b> is tested, not the number.
    /// </remarks>
    [Fact]
    public void TheRangeClassThrowsWithAFullerHand()
    {
        int trained = HitsFor(WarriorClass.Kyudo);
        int untrained = HitsFor(WarriorClass.None);

        Assert.True(untrained > 0, "The warrior with no class hit nothing at range.");
        Assert.True(
            trained > untrained,
            $"The range class did not throw better ({trained} <= {untrained}).");

        static int HitsFor(WarriorClass klass)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 120; seed++)
            {
                BattleSetup setup = new(
                    [
                        TestBuilders.Warrior(
                            1,
                            "Thrower",
                            aggression: 100,
                            accuracy: 40,
                            weapon: Weapon.Fists(),
                            thrown: ThrownWeapon.Shuriken(),
                            klass: klass),
                    ],
                    [TestBuilders.Warrior(101, "Target", health: 4000, aggression: 0)])
                {
                    Tuning = PoisonOnly with { StartOffsetX = 400, StallGuardSeconds = 8 },
                };

                var battle = new Battle(setup, new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<ProjectileHit>().Count();
            }

            return total;
        }
    }

    /// <summary>
    /// A lost arm closes the classes that need a hand; the dose survives every loss.
    /// </summary>
    /// <remarks>
    /// The fitness matrix is what turns limb loss from a leak into a second career: the maimed warrior
    /// does not keep a class he can no longer practise, he chooses again among what is left.
    /// </remarks>
    [Fact]
    public void AnArmLossNarrowsTheClassChoiceWithoutEmptyingIt()
    {
        Warrior warrior = TestBuilders.Warrior(1, "Maimed");
        warrior.AddDisability(BodyPart.SwordArm);

        IReadOnlyList<WarriorClass> open = ClassAptitude.ChoicesFor(warrior.Disabilities);

        Assert.Equal([WarriorClass.Dokushi], open);
        Assert.False(ClassAptitude.IsPossibleFor(WarriorClass.Torite, warrior.Disabilities));
        Assert.False(ClassAptitude.IsPossibleFor(WarriorClass.Kyudo, warrior.Disabilities));
    }

    /// <summary>An unhurt warrior may take up any of the three.</summary>
    [Fact]
    public void AnUnhurtWarriorMayTakeUpAnyClass()
    {
        Warrior warrior = TestBuilders.Warrior(1, "Whole");

        Assert.Equal(
            [WarriorClass.Torite, WarriorClass.Dokushi, WarriorClass.Kyudo],
            ClassAptitude.ChoicesFor(warrior.Disabilities));
    }

    /// <summary>A leg is not a hand: it takes no class away.</summary>
    [Fact]
    public void ALegLossTakesNoClassAway()
    {
        Warrior warrior = TestBuilders.Warrior(1, "Limping");
        warrior.AddDisability(BodyPart.RightLeg);

        Assert.Equal(3, ClassAptitude.ChoicesFor(warrior.Disabilities).Count);
    }
}
