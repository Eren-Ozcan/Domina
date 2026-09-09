using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The scene root that opens the roster screen on its own.
/// </summary>
/// <remarks>
/// The screen takes a <see cref="DojoState"/>; this scene builds it with
/// <see cref="DemoRoster.Dojo"/>. The game being played does not go through here
/// (<see cref="DojoHub"/> loads the save); this
/// scene stands here to open the screen on its own, without touching the save.
/// </remarks>
public sealed partial class RosterDemo : Node
{
    public override void _Ready()
    {
        RosterScreen screen = new();
        AddChild(screen);
        screen.Build(DemoRoster.Dojo());
    }
}
