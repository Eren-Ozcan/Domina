using Domina.Core.Model;

namespace Domina.Sim;

/// <summary>
/// The class implements as this run measures them — the jitte, the sai and the poisoned tantō with
/// their damage, their attack cycle and their dose put on the command line.
/// </summary>
/// <remarks>
/// <para>
/// Open Decision #19 asks what these three should be worth, and a question of that shape can only be
/// answered by sweeping the numbers. Every other axis in the harness already has a knob
/// (<see cref="Domina.Core.Combat.CombatTuning"/>); the implements did not, because their numbers sit
/// on the weapon itself and the weapon is built by a static factory.
/// </para>
/// <para>
/// The overrides are read once, before the first fight, and never change while a run is in flight —
/// the sweep is one value per process, so the run stays as deterministic as the seed makes it. A run
/// that sets nothing gets the catalogue's own numbers, which is what every measurement before this one
/// was made on.
/// </para>
/// </remarks>
internal static class ImplementBench
{
    /// <summary>Puts a parsed sweep on the bench; an unset field keeps the catalogue's number.</summary>
    internal static void Apply(ImplementSpec spec)
    {
        Damage = spec.Damage;
        Cycle = spec.Cycle;
        BladeDamage = spec.BladeDamage;
        Dose = spec.Dose;
    }

    /// <summary>What a jitte or a sai deals in a blow; <c>null</c> keeps the catalogue's 15.</summary>
    internal static double? Damage { get; set; }

    /// <summary>The seconds between their blows; <c>null</c> keeps the jitte's 1.00 and the sai's 1.05.</summary>
    internal static double? Cycle { get; set; }

    /// <summary>What the poisoned tantō's steel deals; <c>null</c> keeps its 7.</summary>
    internal static double? BladeDamage { get; set; }

    /// <summary>The strength of the dose on that blade; <c>null</c> keeps its 1.0.</summary>
    internal static double? Dose { get; set; }

    /// <summary>Clears every override — the catalogue's own numbers again.</summary>
    internal static void Reset()
    {
        Damage = null;
        Cycle = null;
        BladeDamage = null;
        Dose = null;
    }

    /// <summary>The single-hooked implement, as this run measures it.</summary>
    internal static Weapon Jitte() => Implement(Weapon.Jitte());

    /// <summary>The three-pronged implement, as this run measures it.</summary>
    /// <remarks>
    /// The sai's cycle is the jitte's plus 0.05 — the two differ in grip, not in speed, and a sweep
    /// that flattened them would be measuring a weapon the game does not have.
    /// </remarks>
    internal static Weapon Sai() => Weapon.Sai() with
    {
        Damage = Damage ?? Weapon.Sai().Damage,
        AttackSeconds = Cycle is double cycle
            ? cycle + (Weapon.Sai().AttackSeconds - Weapon.Jitte().AttackSeconds)
            : Weapon.Sai().AttackSeconds,
    };

    /// <summary>The poisoned knife, as this run measures it.</summary>
    internal static Weapon PoisonedTanto() => Weapon.PoisonedTanto() with
    {
        Damage = BladeDamage ?? Weapon.PoisonedTanto().Damage,
        Poison = Dose ?? Weapon.PoisonedTanto().Poison,
    };

    private static Weapon Implement(Weapon weapon) => weapon with
    {
        Damage = Damage ?? weapon.Damage,
        AttackSeconds = Cycle ?? weapon.AttackSeconds,
    };
}

/// <summary>The implement numbers a run was asked to measure, as the command line gave them.</summary>
/// <param name="Damage">What a jitte or a sai deals in a blow.</param>
/// <param name="Cycle">The seconds between a jitte's blows; the sai keeps its 0.05 of extra weight.</param>
/// <param name="BladeDamage">What the poisoned tantō's steel deals.</param>
/// <param name="Dose">The strength of the dose on that blade.</param>
internal readonly record struct ImplementSpec(
    double? Damage = null,
    double? Cycle = null,
    double? BladeDamage = null,
    double? Dose = null);
