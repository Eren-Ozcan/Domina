using System.Globalization;
using Domina.Core.Dojo;
using Godot;

namespace Domina.Game;

/// <summary>
/// The world stopped: the yard holds its breath, and four things may be done about it.
/// </summary>
/// <remarks>
/// <para>
/// There is no word for it anywhere on the screen — no "paused", no "menu". The yard stays where it
/// was, the colour is drawn out of it and the clock's mark stands still; the only new thing is a small
/// stack of acts at the right (design canvas → 6e).
/// </para>
/// <para>
/// Abandoning the term is the one cut act on it, and it says what it costs in the term's own units:
/// the days that were played, the men in the yard and the seed they were drawn from.
/// </para>
/// </remarks>
public sealed partial class PauseScreen : CanvasLayer
{
    /// <summary>The dojo the stack is describing.</summary>
    public required DojoState Dojo { get; init; }

    /// <summary>Let the day run on.</summary>
    public Action? Resumed { get; set; }

    /// <summary>Write the term down where it stands.</summary>
    public Action? Wrote { get; set; }

    /// <summary>Open the settings sheet over this one.</summary>
    public Action? Settings { get; set; }

    /// <summary>Give the term up. It cannot be got back.</summary>
    public Action? Abandoned { get; set; }

    public override void _Ready()
    {
        Control page = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        AddChild(page);

        // The yard is not covered, only drained: what is behind this is a place, and the player is
        // still standing in it.
        ColorRect drawn = new() { Color = new Color(UiKit.Ground, 0.45f), AnchorRight = 1, AnchorBottom = 1 };
        page.AddChild(drawn);

        // Anywhere in the yard lets the day run on again — the same thing the space bar does.
        Button ground = new() { AnchorRight = 1, AnchorBottom = 1, Flat = true };
        ground.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        ground.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        ground.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        ground.Pressed += () => Resumed?.Invoke();
        page.AddChild(ground);

        VBoxContainer stack = new() { Position = new Vector2(1320, 300), CustomMinimumSize = new Vector2(440, 0) };
        stack.AddThemeConstantOverride("separation", 3);
        page.AddChild(stack);

        Button on = new() { Text = "Let the day run on" };
        on.Pressed += () => Resumed?.Invoke();
        stack.AddChild(Stacked(UiKit.Act(on), "Space, or click anywhere in the yard", UiKit.IndigoInk));

        Button write = new() { Text = "Write the term down" };
        write.Pressed += () => Wrote?.Invoke();
        stack.AddChild(Stacked(UiKit.WayOut(write), "one slot · it is written at every dawn as well", UiKit.Muted));

        Button settings = new() { Text = "Settings" };
        settings.Pressed += () => Settings?.Invoke();
        stack.AddChild(Stacked(UiKit.WayOut(settings), "the picture, the hands, and reading it", UiKit.Muted));

        Button give = new() { Text = "Abandon the term" };
        give.Pressed += () => Abandoned?.Invoke();
        stack.AddChild(Stacked(UiKit.Cut(give), Cost(), UiKit.NightMuted));
    }

    /// <summary>What giving the term up costs, counted in the things it is made of.</summary>
    private string Cost()
    {
        int men = Dojo.Roster.Living.Count();

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1}, {2} {3} and the seed are gone. The slot is freed.",
            Dojo.Day,
            Dojo.Day == 1 ? "day" : "days",
            men,
            men == 1 ? "man" : "men");
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
}
