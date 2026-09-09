using Domina.Core.Combat;

namespace Domina.Presentation;

/// <summary>
/// Computes the warriors' place in the scene from the snapshots.
/// </summary>
/// <remarks>
/// <para>
/// <b>Position is no longer produced here, it is read from the core.</b> The warriors really walk on
/// the arena plane; this class only projects that plane onto the screen and adds purely visual
/// decoration on top (such as leaning back while gathering the sword).
/// </para>
/// <para>
/// There used to be a fake space here: the move distance, the escape distance and the death position
/// were all guessed — because there was no position in the core. Once space entered the core, all that
/// maths became unnecessary. A dead warrior staying where he fell is no longer something to remember
/// either: the core does not move the dead.
/// </para>
/// </remarks>
public sealed class ArenaChoreography(ArenaLayout layout)
{
    public ArenaLayout Layout { get; } = layout;

    /// <summary>The warrior's screen position this frame.</summary>
    public ScenePoint PositionFor(in CombatantSnapshot snapshot)
    {
        ScenePoint ground = Layout.Project(snapshot.Position);

        if (snapshot.State is not (CombatState.AttackWindup or CombatState.ThrowWindup))
        {
            return ground;
        }

        // A slight lean back while gathering the sword — purely visual, with no counterpart in the core.
        float drawBack = Layout.WindupDrawBack * Curves.Smooth((float)snapshot.StateProgress);
        return ground with { X = ground.X - (snapshot.Facing * drawBack) };
    }

    /// <summary>Which way the warrior faces: +1 right, -1 left.</summary>
    public static float FacingOf(in CombatantSnapshot snapshot) => snapshot.Facing;

    /// <summary>
    /// The scale by depth — a distant warrior looks smaller.
    /// </summary>
    public float ScaleFor(in CombatantSnapshot snapshot) => Layout.ScaleAt(snapshot.Position.Y);

    /// <summary>
    /// The draw order: the one standing deep stays behind. The Godot layer turns this into <c>ZIndex</c>.
    /// </summary>
    /// <remarks>
    /// As Y grows the warrior <b>moves away</b>, so the order carries the opposite sign.
    /// </remarks>
    public static int DrawOrderFor(in CombatantSnapshot snapshot) => -(int)snapshot.Position.Y;
}
