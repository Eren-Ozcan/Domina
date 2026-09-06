using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// Kadro ekranını tek başına açan sahne kökü.
/// </summary>
/// <remarks>
/// Ekran bir <see cref="DojoState"/> alır; kayıt katmanı gelene kadar onu
/// <see cref="DemoRoster.Dojo"/> kuruyor. Meta katman akışı bağlandığında bu sınıf
/// yerini gerçek geçişe bırakır — ekranın kendisi değişmez.
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
