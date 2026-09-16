using Domina.Chat;
using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// Someone is standing in the gateway, asking to be let in.
/// </summary>
/// <remarks>
/// <para>
/// A viewer who asks is given a man rather than a badge: the game draws a warrior off the term's seed
/// and the viewer's own name, and stands him in the gate. The sheet leads with the store, because a
/// fourth body is a fourth mouth and that is the part of the decision the player will regret first
/// (design canvas → 9b).
/// </para>
/// <para>
/// He is taken in for nothing — he is not a purchase and there is no haggling. What he costs is the
/// rice, and what he buys is a body that can hold a flank.
/// </para>
/// </remarks>
public sealed partial class GateScreen : CanvasLayer
{
    /// <summary>The arrival being decided.</summary>
    public required GateArrival Arrival { get; init; }

    /// <summary>The dojo he would be joining.</summary>
    public required DojoState Dojo { get; init; }

    /// <summary>He was let in.</summary>
    public Action? LetIn { get; set; }

    /// <summary>He was sent away.</summary>
    public Action? SentAway { get; set; }

    public override void _Ready()
    {
        Control page = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        AddChild(page);
        page.AddChild(UiKit.Dim(0.72f));

        GateSheet sheet = GateModel.Describe(
            Dojo,
            Arrival.Man.Name,
            Grade(),
            Arrival.Man.Class == Domina.Core.Model.WarriorClass.None
                ? "a robe and whatever he walked in with"
                : Arrival.Man.Class.ToString().ToLowerInvariant());

        Button away = new();
        away.Pressed += () => SentAway?.Invoke();
        away.Text = "send him away ✕";

        VBoxContainer paper = UiKit.Sheet(
            page,
            "Someone at the gate",
            away,
            "He asks for nothing but the food. What taking him in costs, and what it buys.");

        HBoxContainer columns = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        paper.AddChild(columns);

        Standing(columns, sheet);
        Costs(columns, sheet);

        HBoxContainer foot = new() { Alignment = BoxContainer.AlignmentMode.End };
        foot.AddThemeConstantOverride("separation", 10);
        paper.AddChild(foot);

        Button take = new() { Text = "Let him in" };
        take.Pressed += () => LetIn?.Invoke();

        foot.AddChild(sheet.CanFeed
            ? UiKit.Act(take)
            : UiKit.Refused(take, "The store cannot feed the men already in the yard."));
    }

    /// <summary>The man himself, on ink: he is standing in the dark of the gateway.</summary>
    private void Standing(Control parent, GateSheet sheet)
    {
        PanelContainer panel = new() { CustomMinimumSize = new Vector2(452, 0) };
        panel.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Ink));
        parent.AddChild(panel);

        VBoxContainer said = UiKit.Padded(panel, 26, 24);
        said.AddThemeConstantOverride("separation", 14);

        said.AddChild(UiKit.Body("standing in the gateway, asking", UiKit.NightMuted, UiKit.NoteSize));
        said.AddChild(UiKit.OnNight(sheet.Name, UiKit.PaperInk, UiKit.DisplaySize - 6, display: true));
        said.AddChild(UiKit.Body(sheet.Line, UiKit.NightMuted, UiKit.NoteSize + 1));

        // The figure in the gateway, cut out of the same paper the yard's men are.
        Control man = new() { CustomMinimumSize = new Vector2(120, 216) };
        man.Draw += () =>
        {
            foreach ((Color fill, Vector2[] points) in YardArt.Man())
            {
                man.DrawColoredPolygon(points, fill);
            }
        };
        said.AddChild(man);

        PanelContainer from = new();
        from.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(new Color(0.169f, 0.141f, 0.110f)));
        said.AddChild(from);

        VBoxContainer told = UiKit.Padded(from, 15, 13);
        told.AddThemeConstantOverride("separation", 4);
        told.AddChild(UiKit.Body("FROM THE CHAT", UiKit.NightMuted, UiKit.NoteSize));
        told.AddChild(UiKit.Body(
            $"A viewer named {Arrival.Viewer} asked to be let in. The game gave the name to a man and "
            + "put him in the gateway; it did not put a badge on him.",
            UiKit.PaperInk,
            UiKit.NoteSize + 1));
    }

    /// <summary>The four things taking him in decides.</summary>
    private static void Costs(Control parent, GateSheet sheet)
    {
        VBoxContainer column = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 12);
        parent.AddChild(column);

        column.AddChild(UiKit.SectionLabel("What taking him in costs, and buys"));

        foreach (GateCard card in sheet.Cards)
        {
            PanelContainer panel = new();
            panel.AddThemeStyleboxOverride("panel", UiKit.PaperStyle(UiKit.Raised, shadow: 5));
            column.AddChild(panel);

            VBoxContainer said = UiKit.Padded(panel, 15, 13);
            said.AddThemeConstantOverride("separation", 4);
            said.AddChild(UiKit.Body(
                string.Join(" ", card.Label.ToUpperInvariant().ToCharArray()),
                UiKit.Muted,
                UiKit.NoteSize));
            said.AddChild(UiKit.OnPaper(card.Figure, UiKit.Ink, UiKit.HeadSize + 4, display: true));
            said.AddChild(UiKit.Body(card.Line, card.Grave ? UiKit.Brick : UiKit.Muted, UiKit.NoteSize));
        }
    }

    /// <summary>His grade, as the market rates the man it drew.</summary>
    private int Grade() => Math.Max(1, (int)Math.Round(Arrival.Man.Talent * 20));
}
