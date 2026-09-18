using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The scene root that opens the terms sheet on its own.
/// </summary>
/// <remarks>
/// The sheet is only reachable in the game at the moment of sending a party, which is several
/// decisions into a day; this scene stands it up against <see cref="DemoRoster.Dojo"/> so the layout
/// can be looked at without playing to it. Nothing is sent from here — both answers only print what
/// they were given, and the save is never touched (the same arrangement as <see cref="RosterDemo"/>).
/// </remarks>
public sealed partial class SortieDemo : Node
{
    public override void _Ready()
    {
        DojoState dojo = DemoRoster.Dojo();

        // Two men are ticked to open with, so the preview shows the sheet in the state that has to be
        // looked at — the figures standing against the road — rather than an empty column.
        List<WarriorId> ticked =
        [
            .. OfferModel.Candidates(dojo).Where(candidate => candidate.Fit).Take(2).Select(c => c.Id),
        ];

        SortieScreen terms = new() { Dojo = dojo, Chosen = ticked };
        terms.Accepted = chosen => GD.Print($"The gate opens for {chosen.Count}.");
        terms.Refused = _ => GD.Print("Not today.");
        AddChild(terms);
    }
}
