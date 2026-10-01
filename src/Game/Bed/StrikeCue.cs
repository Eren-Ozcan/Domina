using Godot;

namespace Domina.Game.Bed;

/// <summary>
/// The node a strike clip's method key calls: the frame the blade connects.
/// </summary>
/// <remarks>
/// A method track needs a node with a method on it; the fighter itself is plain C#, so this small node
/// stands on the body and passes the call on.
/// </remarks>
public sealed partial class StrikeCue : Node
{
    internal TrivialFighter? Fighter { get; set; }

    /// <summary>Called by the clip's method key. The name is the one written into the clip.</summary>
    public void Land() => Fighter?.Land();
}
