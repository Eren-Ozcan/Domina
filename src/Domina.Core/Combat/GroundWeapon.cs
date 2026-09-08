using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// A weapon that fell out of a hand and stayed in the arena.
/// </summary>
/// <remarks>
/// <para>
/// A dropped weapon is not destroyed: it becomes <b>a point in the arena</b> and anyone left unarmed —
/// the one who dropped it, a teammate or an enemy — can pick it up. This is the real return of choosing
/// dropping over breaking; a lost weapon is not a loss but something lying on the ground that has to be
/// walked to.
/// </para>
/// <para>
/// The list preserves the order of the drops: if two warriors reach the same weapon on the same tick,
/// which one takes it must be fixed, or the same seed does not give the same fight.
/// </para>
/// </remarks>
internal sealed class GroundWeapon(Weapon weapon, ArenaPoint position)
{
    public Weapon Weapon { get; } = weapon;

    /// <summary>Where it fell. The weapon lies there, it is not dragged.</summary>
    public ArenaPoint Position { get; } = position;
}
