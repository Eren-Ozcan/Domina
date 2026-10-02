namespace Domina.Core.Combat;

/// <summary>
/// The ground the fight stands on: where a man ends up when he means to go somewhere.
/// </summary>
/// <remarks>
/// <para>
/// The resolver decides where each man <b>means</b> to go — toward his target, away from the man he
/// poisoned, to the blade on the ground, off the field. Whether he gets there is this door's answer:
/// whether another body stops him, whether the arena's edge holds him. That is the first piece of the
/// fight moved into the engine (docs/PLAN-ENGINE-RESOLUTION.md, Phase B): the decision stays on the
/// core's tick, the space it is carried out in can be Godot's.
/// </para>
/// <para>
/// Bodies are numbered in the battle's own order, the player's side first. The resolver keeps every
/// man's position as the space last returned it, and reads distances off those positions.
/// </para>
/// <para>
/// Today there are two: <see cref="KinematicArenaSpace"/>, the engine-free resolver's own kinematics
/// (the default), and the engine's, built on the physics server. The resolver is the same over both.
/// </para>
/// </remarks>
public interface IArenaSpace
{
    /// <summary>A fight begins with <paramref name="bodies"/> men; whatever stood in the space before is gone.</summary>
    void Begin(int bodies);

    /// <summary>Puts body <paramref name="body"/> at its place in the formation.</summary>
    /// <returns>Where it stands.</returns>
    ArenaPoint Enter(int body, ArenaPoint at);

    /// <summary>
    /// Body <paramref name="body"/> means to stand at <paramref name="intended"/> at the end of this tick.
    /// </summary>
    /// <param name="body">The man, in the battle's order.</param>
    /// <param name="intended">Where he means to be; his last position when he means to stay.</param>
    /// <param name="passing">
    /// He is leaving the field: nobody stops him and the arena's edge does not hold him.
    /// </param>
    /// <returns>Where he stands.</returns>
    ArenaPoint Settle(int body, ArenaPoint intended, bool passing);

    /// <summary>The man is off the field — dead, escaped or yielded — and stands in nobody's way.</summary>
    void Leave(int body);
}
