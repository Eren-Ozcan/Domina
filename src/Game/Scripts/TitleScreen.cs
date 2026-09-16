using System.Globalization;
using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Godot;

namespace Domina.Game;

/// <summary>The yard at night, before anyone is in it: the game's first screen.</summary>
/// <remarks>
/// <para>
/// The screen <b>takes no dojo</b>: where the dojo comes from is decided here, which is why it does not
/// share the same skeleton as the other screens.
/// </para>
/// <para>
/// It is the only screen in the set that is not a place in the world, and it is drawn as the yard all
/// the same — the same wall, the same board, the same brazier, with nobody standing in it. The term the
/// player left is named on the one indigo act, because "continue" without saying what is being
/// continued is the thing that makes a player open a save to find out (design canvas → 6a).
/// </para>
/// <para>
/// There is one slot. A new term therefore <b>writes over</b> the one that is kept, and that act is cut
/// rather than indigo: what it costs is a term, and a term cannot be got back.
/// </para>
/// </remarks>
public sealed partial class TitleScreen : CanvasLayer
{
    /// <summary>Load the save.</summary>
    public Action? Continued { get; set; }

    /// <summary>Open the new term's sheet, where the name, the province and the seed are chosen.</summary>
    public Action? Opening { get; set; }

    /// <summary>Open the settings sheet over the title.</summary>
    public Action? Settings { get; set; }

    /// <summary>Open the sheet of kept terms.</summary>
    public Action? Keeping { get; set; }

    /// <summary>The warning shown under the acts; written if the load was incomplete.</summary>
    public string? Warning { get; set; }

    public override void _Ready()
    {
        Control page = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        AddChild(page);

        BuildYardAtNight(page);

        MarginContainer margin = new() { AnchorRight = 1, AnchorBottom = 1 };
        margin.AddThemeConstantOverride("margin_left", 150);
        margin.AddThemeConstantOverride("margin_top", 250);
        margin.AddThemeConstantOverride("margin_right", 150);
        margin.AddThemeConstantOverride("margin_bottom", 150);
        page.AddChild(margin);

        VBoxContainer column = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(560, 0),
        };
        column.AddThemeConstantOverride("separation", 40);
        margin.AddChild(column);

        column.AddChild(BuildName());
        column.AddChild(BuildActs());
        BuildFooter(page);
    }

    /// <summary>The yard with nobody in it: the same bands and the same things, unlit and unnamed.</summary>
    private static void BuildYardAtNight(Control page)
    {
        Band(page, 0, 540, new Color(0.118f, 0.125f, 0.188f));
        Band(page, 470, 110, new Color(0.075f, 0.063f, 0.051f));
        Band(page, 580, 500, new Color(0.114f, 0.098f, 0.075f));
        Band(page, 860, 220, new Color(0.133f, 0.110f, 0.082f));

        Cut(page, YardArt.Roofs(), new Vector2(0, 300), new Vector2(1920, 240));
        Cut(page, YardArt.Hall(), new Vector2(812, 340), new Vector2(300, 242));
        Cut(page, YardArt.Rack(), new Vector2(1128, 560), new Vector2(258, 290));
        Cut(page, YardArt.Board(), new Vector2(706, 636), new Vector2(216, 268));

        Moon(page, new Vector2(1546, 162), 66);
        Fire(page, new Vector2(372, 420));
        Fire(page, new Vector2(1094, 436));
    }

    /// <summary>The game's own name, with the crest beside it and the term under it.</summary>
    private static Control BuildName()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 26);
        row.Alignment = BoxContainer.AlignmentMode.Begin;

        // The crest is a character in a box rather than a drawing: it is the one place in the game where
        // a Japanese glyph is the picture, and the bundled face carries it.
        PanelContainer crest = new() { CustomMinimumSize = new Vector2(116, 116) };
        crest.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Ink, UiKit.Heading, 2));
        CenterContainer middle = new();
        crest.AddChild(middle);
        middle.AddChild(UiKit.OnNight("侍", UiKit.PaperInk, 62, display: true));
        row.AddChild(crest);

        VBoxContainer named = new() { SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
        named.AddThemeConstantOverride("separation", 6);
        named.AddChild(UiKit.OnNight("Domina", UiKit.PaperInk, 96, display: true));
        named.AddChild(UiKit.SectionLabel("One term of a fighting school", onNight: true));
        row.AddChild(named);

        return row;
    }

    /// <summary>The four ways on, with what each of them means written beside it.</summary>
    private Control BuildActs()
    {
        VBoxContainer column = new() { CustomMinimumSize = new Vector2(470, 0) };
        column.AddThemeConstantOverride("separation", 3);

        // The one that is continued is the term last written to, which is the one the player was in.
        int kept = 0;

        for (int slot = 1; slot <= SaveSlot.Slots; slot++)
        {
            if (SaveSlot.Exists(slot))
            {
                kept = kept == 0 ? slot : kept;
            }
        }

        if (kept > 0 && SaveSlot.Peek(kept).State is DojoState dojo)
        {
            SaveSlot.Current = kept;

            Button resume = new() { Text = "Continue" };
            resume.Pressed += () => Continued?.Invoke();
            column.AddChild(Stacked(UiKit.Act(resume), Describe(dojo), UiKit.IndigoInk));

            Button terms = new() { Text = "Terms kept" };
            terms.Pressed += () => Keeping?.Invoke();
            column.AddChild(Stacked(UiKit.WayOut(terms), Kept(), UiKit.NightMuted));
        }

        Button fresh = new() { Text = "A new term" };
        fresh.Pressed += () => Opening?.Invoke();
        column.AddChild(Stacked(
            UiKit.Act(fresh),
            SaveSlot.FirstEmpty() is int free
                ? $"a dojo, a name, a seed — it writes itself into slot {free}"
                : "every slot is kept; one of them has to be written over",
            UiKit.IndigoInk));

        Button settings = new() { Text = "Settings" };
        settings.Pressed += () => Settings?.Invoke();
        column.AddChild(Stacked(UiKit.WayOut(settings), "the picture, the hands, and reading it", UiKit.NightMuted));

        Button leave = new() { Text = "Leave" };
        leave.Pressed += () => GetTree().Quit();
        column.AddChild(Stacked(UiKit.WayOut(leave), "the term is saved at each dawn", UiKit.NightMuted));

        if (Warning is string warning && warning.Length > 0)
        {
            column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
            column.AddChild(UiKit.Body(warning, UiKit.BrickLit, UiKit.NoteSize));
        }

        return column;
    }

    /// <summary>How many terms are kept, for the line under the sheet of them.</summary>
    private static string Kept()
    {
        int many = 0;

        for (int slot = 1; slot <= SaveSlot.Slots; slot++)
        {
            many += SaveSlot.Exists(slot) ? 1 : 0;
        }

        return many == 1
            ? "one kept · three slots"
            : $"{many.ToString(CultureInfo.InvariantCulture)} kept · {SaveSlot.Slots.ToString(CultureInfo.InvariantCulture)} slots";
    }

    /// <summary>The term in the slot, in one line: where it stands and what it is holding.</summary>
    private static string Describe(DojoState dojo)
    {
        int living = dojo.Roster.Living.Count();

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} · day {1} of {2} · {3} on the roster · {4} koku",
            dojo.Name,
            dojo.Day,
            dojo.Season.Tuning.Days,
            living,
            dojo.Resources.Gold);
    }

    /// <summary>An act with its consequence set under it rather than inside it.</summary>
    private static Control Stacked(Button act, string clause, Color colour)
    {
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 2);
        act.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        act.Alignment = HorizontalAlignment.Left;
        column.AddChild(act);
        column.AddChild(UiKit.Body(clause, colour, UiKit.NoteSize, wrap: false));
        return column;
    }

    /// <summary>The build and the seed, small, at the foot of the night.</summary>
    private void BuildFooter(Control page)
    {
        HBoxContainer row = new() { Position = new Vector2(150, 990) };
        row.AddThemeConstantOverride("separation", 30);
        row.AddChild(UiKit.Body("a sample build", UiKit.NightMuted, UiKit.NoteSize, wrap: false));
        row.AddChild(UiKit.Body(
            $"{SaveSlot.Slots.ToString(CultureInfo.InvariantCulture)} slots, each written at its own dawn",
            UiKit.NightMuted,
            UiKit.NoteSize,
            wrap: false));
        page.AddChild(row);
    }

    private static void Band(Control page, float top, float height, Color colour) =>
        page.AddChild(new ColorRect
        {
            Color = colour,
            Position = new Vector2(0, top),
            Size = new Vector2(1920, height),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

    private static void Cut(
        Control page,
        IReadOnlyList<(Color Fill, Vector2[] Points)> shapes,
        Vector2 at,
        Vector2 size)
    {
        Control piece = new() { Position = at, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
        piece.Draw += () =>
        {
            foreach ((Color fill, Vector2[] points) in shapes)
            {
                piece.DrawColoredPolygon(points, fill);
            }
        };

        page.AddChild(piece);
    }

    private static void Moon(Control page, Vector2 centre, float radius)
    {
        Control moon = new()
        {
            Position = centre - new Vector2(radius, radius),
            Size = new Vector2(radius * 2, radius * 2),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        moon.Draw += () => moon.DrawCircle(
            new Vector2(radius, radius),
            radius,
            new Color(0.851f, 0.812f, 0.706f, 0.7f));

        page.AddChild(moon);
    }

    private static void Fire(Control page, Vector2 at)
    {
        Control fire = new() { Position = at, Size = new Vector2(26, 40), MouseFilter = Control.MouseFilterEnum.Ignore };
        fire.Draw += () =>
        {
            fire.DrawCircle(new Vector2(13, 20), 38, new Color(0.659f, 0.506f, 0.247f, 0.14f));
            fire.DrawRect(new Rect2(0, 0, 26, 40), new Color(0.659f, 0.506f, 0.247f));
        };

        page.AddChild(fire);
    }
}
