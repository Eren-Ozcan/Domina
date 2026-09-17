using System.Globalization;
using Godot;

namespace Domina.Game;

/// <summary>
/// The settings sheet: what the build can be told to do, and what it says about what it cannot.
/// </summary>
/// <remarks>
/// <para>
/// Every line says what it costs, the way the rest of the game does — a setting that only names itself
/// makes the player press it to find out (design canvas → 6d).
/// </para>
/// <para>
/// The sheet is honest about the two panels the build has nothing behind: the sound and the crowd in
/// the chat are named and then plainly said to be unwired, rather than given switches that move and do
/// nothing.
/// </para>
/// </remarks>
public sealed partial class SettingsScreen : CanvasLayer
{
    /// <summary>Closed, back to whatever it opened over.</summary>
    public Action? Closed { get; set; }

    /// <summary>Called whenever a setting changes, so the yard can take up its new state at once.</summary>
    public Action? Changed { get; set; }

    public override void _Ready()
    {
        Control page = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        AddChild(page);
        page.AddChild(UiKit.Dim(0.72f));

        Button back = new();

        if (Closed is Action closed)
        {
            back.Pressed += closed;
        }

        VBoxContainer sheet = UiKit.Sheet(
            page,
            "Settings",
            back,
            "They are written down as they are changed; there is nothing to confirm.");

        HBoxContainer columns = new();
        columns.AddThemeConstantOverride("separation", 12);
        sheet.AddChild(columns);

        BuildPicture(columns);
        BuildReading(columns);
        BuildHands(columns);
        BuildUnwired(columns);
    }

    /// <summary>The window itself.</summary>
    private void BuildPicture(Control parent)
    {
        VBoxContainer panel = UiKit.Section(parent, "Picture");

        panel.AddChild(Toggle(
            "The window",
            () => GameSettings.Borderless ? "borderless, the whole screen" : "a window",
            () => GameSettings.Borderless = !GameSettings.Borderless));

        panel.AddChild(Toggle(
            "The paper",
            () => GameSettings.PaperGrain ? "grained, and the hour washed over it" : "flat",
            () => GameSettings.PaperGrain = !GameSettings.PaperGrain,
            "The grain, the inked edges and the colour the hour puts over the yard. Taking it off "
            + "changes nothing the game does — only what it looks like."));

        panel.AddChild(UiKit.Note(
            "The yard is drawn at 1920 × 1080 and the engine stretches it; nothing is cut off at "
            + "another size."));
    }

    /// <summary>Reading it — and being able to.</summary>
    private void BuildReading(Control parent)
    {
        VBoxContainer panel = UiKit.Section(parent, "Reading it");

        panel.AddChild(Toggle(
            "Text size",
            () => GameSettings.LargeText ? "one step up" : "as drawn",
            () => GameSettings.LargeText = !GameSettings.LargeText,
            "Every sheet holds at either size: the whole stage is scaled, not the type inside it."));

        panel.AddChild(Toggle(
            "Name the destinations",
            () => GameSettings.NameDestinations ? "always" : "under the cursor",
            () => GameSettings.NameDestinations = !GameSettings.NameDestinations,
            "Every object keeps its chalk name showing, for a player who would rather read the yard "
            + "than learn it."));

        panel.AddChild(Toggle(
            "Reduced motion",
            () => GameSettings.ReducedMotion ? "on" : "off",
            () => GameSettings.ReducedMotion = !GameSettings.ReducedMotion,
            "No flicker and nothing that moves on its own. The clock still runs and the day still ends."));

        panel.AddChild(Toggle(
            "The day log",
            () => $"{GameSettings.LogHold.ToString("0", CultureInfo.InvariantCulture)}s a line",
            () => GameSettings.LogHold = GameSettings.LogHold >= 8 ? 4 : GameSettings.LogHold + 2,
            "How long a line of the fight's report holds before the next takes its place."));
    }

    /// <summary>The hands: what the keys do. There is no key that opens a menu, because there is no menu.</summary>
    private static void BuildHands(Control parent)
    {
        VBoxContainer panel = UiKit.Section(parent, "Hands");

        panel.AddChild(Reading("Walk up to a thing", "click it"));
        panel.AddChild(Reading("Close what is open", "Esc, or the corner of the sheet"));
        panel.AddChild(Reading("Stop the clock", "Space"));
        panel.AddChild(Reading("Set the clock's speed", "the strip, on the right"));
        panel.AddChild(UiKit.Note("There is no key that opens a menu, because there is no menu."));
    }

    /// <summary>What the build has nothing behind, said plainly rather than switched.</summary>
    private static void BuildUnwired(Control parent)
    {
        VBoxContainer panel = UiKit.Section(parent, "Not yet wired");

        panel.AddChild(Reading("Sound", "there is none yet"));
        panel.AddChild(Reading("Twitch · Kick", "the chat layer is not written"));
        panel.AddChild(UiKit.Note(
            "When the crowd is connected these become the panel the design draws: who is connected, "
            + "whether viewers may enter the roster, and whether a viewer's man may die in it. Until "
            + "then they are not offered as switches, because a switch that moves and changes nothing "
            + "is worse than an empty shelf."));
    }

    /// <summary>A setting: what it is, what it is set to, and what setting it does.</summary>
    private Control Toggle(string what, Func<string> reading, Action press, string? clause = null)
    {
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 3);

        Button button = new() { Text = reading(), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        button.Pressed += () =>
        {
            press();
            button.Text = reading();
            GameSettings.Save();
            GameSettings.Apply(GetTree().Root);
            Changed?.Invoke();
        };

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 12);

        Label label = UiKit.Body(what, UiKit.Ink, UiKit.BodySize, wrap: false);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);
        row.AddChild(UiKit.WayOut(button));
        column.AddChild(row);

        if (clause is not null)
        {
            column.AddChild(UiKit.Note(clause));
        }

        return column;
    }

    /// <summary>A line that states a fact rather than offering a choice.</summary>
    private static Control Reading(string what, string is_)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 12);

        Label label = UiKit.Body(what, UiKit.Ink, UiKit.BodySize, wrap: false);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);

        PanelContainer chip = new();
        chip.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Pressed));
        UiKit.Padded(chip, 13, 6).AddChild(UiKit.Body(is_, UiKit.Ink, UiKit.NoteSize, wrap: false));
        row.AddChild(chip);
        return row;
    }
}
