using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>Shortcuts for producing warriors in the tests.</summary>
internal static class TestBuilders
{
    /// <summary>
    /// The sides start within each other's reach.
    /// </summary>
    /// <remarks>
    /// Since space arrived, the warriors <b>walk</b> toward each other in the default arena. Tests that
    /// exercise the outcome tree, escape or damage measure the collision, not the walk; waiting for them
    /// to close both slows the test down and makes it fragile.
    /// Closing itself is <c>MovementTests</c>'s subject.
    /// </remarks>
    /// <remarks>
    /// <para>
    /// <see cref="CombatTuning.BaseDismembermentChance"/> burada <b>sabitlenir</b>:
    /// tests exercising the outcome tree work with specific die values, and those values were chosen
    /// against the threshold. If the balance number were inherited, these tests would break on every
    /// balance change — whereas what they test is not balance but the rule.
    /// </para>
    /// </remarks>
    public static CombatTuning PointBlank { get; } = CombatTuning.Default with
    {
        StartOffsetX = 30,
        BaseDismembermentChance = 0.35,

        // Panic is off in the shared bed. It fires on exactly the warriors these tests hurt on purpose,
        // and left on it would sit inside every other rule's measurement; it has its own tests.
        BasePanicChance = 0,
    };

    public static Warrior Warrior(
        int id,
        string? name = null,
        double health = 100,
        double aggression = 40,
        double defense = 0,
        double evasion = 0,
        double strength = 40,
        double accuracy = 60,
        double stamina = 100,
        double speed = 50,
        Weapon? weapon = null,
        Armor? armor = null,
        ThrownWeapon? thrown = null,
        WarriorClass klass = WarriorClass.None) =>
        new(
            new WarriorId(id),
            name ?? $"Warrior{id}",
            new WarriorStats(health, aggression, defense, evasion, strength, accuracy, stamina, speed),
            weapon,
            armor,
            thrown)
        {
            Class = klass,
        };

    /// <summary>A weapon that passes the heavy-blow threshold in one strike.</summary>
    public static Weapon Executioner() =>
        new("Test-Nodachi", WeaponClass.Cutting, 80, TwoHanded: false, AttackSeconds: 1.0);
}

/// <summary>
/// A fake randomness source that returns a fixed value.
/// </summary>
/// <remarks>
/// <c>0.0</c> = every die holds (it hits, it evades, a limb comes off).
/// <c>0.999</c> = none of them holds. To close unwanted branches such as evasion it is enough to give
/// the relevant stat as 0 — <see cref="Chance"/> returns false without looking at the value when the
/// probability is 0.
/// </remarks>
internal sealed class FixedRandom(double value) : IRandomSource
{
    public double NextDouble() => value;

    public int NextInt(int exclusiveMax) => (int)(value * exclusiveMax);

    public bool Chance(double probability)
    {
        if (probability <= 0.0)
        {
            return false;
        }

        return probability >= 1.0 || value < probability;
    }
}
