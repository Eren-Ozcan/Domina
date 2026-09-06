using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// Kadro ekranını tek başına açan sahne kökü.
/// </summary>
/// <remarks>
/// Ekran bir <see cref="DojoState"/> alır; bu sahne onu <see cref="DemoRoster.Dojo"/>
/// ile kuruyor. Oynanan oyun buradan geçmez (<see cref="DojoHub"/> kaydı yükler); bu
/// sahne ekranı tek başına, kayda dokunmadan açmak için duruyor.
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
