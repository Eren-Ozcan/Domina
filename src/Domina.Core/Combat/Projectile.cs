using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// Havada olan bir mermi.
/// </summary>
/// <remarks>
/// <para>
/// A projectile is not resolved the moment it is thrown; it flies until <see cref="SecondsToImpact"/>
/// reaches zero. The price is holding a little state, the return is this: the target can flee, die or
/// leave the arena during the flight. Were it "thrown = hit", distance would mean nothing — and turning
/// distance into a threat is exactly why throwing exists.
/// </para>
/// <para>
/// <see cref="Origin"/> is for the visualisation only; the resolution looks at time alone.
/// </para>
/// </remarks>
internal sealed class Projectile(
    Combatant attacker,
    Combatant target,
    ThrownWeapon weapon,
    ArenaPoint origin,
    double secondsToImpact)
{
    public Combatant Attacker { get; } = attacker;

    public Combatant Target { get; } = target;

    public ThrownWeapon Weapon { get; } = weapon;

    public ArenaPoint Origin { get; } = origin;

    public double SecondsToImpact { get; set; } = secondsToImpact;
}
