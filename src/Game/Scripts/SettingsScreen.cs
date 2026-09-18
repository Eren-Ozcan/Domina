using System.Globalization;
using Godot;

namespace Domina.Game;

/// <summary>
/// The settings sheet: what the build can be told to do, and what it says about what it cannot.
/// </summary>
/// <remarks>
/// <para>
/// <b>A row is a label and its reading; the sentence about it waits to be asked for.</b> The clause
/// each setting used to print under itself now goes to the chalk line at the foot of the sheet, where
/// the hand or the keyboard focus fetches it (<see cref="UiKit.Explains{T}"/>, and
/// docs/DESIGN-REFERENCES.md → "How much a line of interface may say"). What a setting is set to is
/// still on the control, because that is what the player came to read.
/// </para>
/// <para>
/// The sheet is still honest about the two panels the build has nothing behind: sound and the crowd in
/// the chat are named and said to be unwired, rather than given switches that move and do nothing.
/// </para>
/// </remarks>
public sealed partial class SettingsScreen : CanvasLayer
{
    /// <summary>Closed, back to whatever it opened over.</summary>
    public Action? Closed { get; set; }

    /// <summary>Called whenever a setting changes, so the yard can take up its new state at once.</summary>
    public Action? Changed { get; set; }

    private Label _line = null!;

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

        _line = UiKit.ChalkLine(sheet);

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
            () => GameSettings.Borderless ? "the whole screen" : "a window",
            () => GameSettings.Borderless = !GameSettings.Borderless,
            "Borderless over the whole screen, or a window that can be dragged and resized."));

        panel.AddChild(Toggle(
            "The paper",
            () => GameSettings.PaperGrain ? "grained" : "flat",
            () => GameSettings.PaperGrain = !GameSettings.PaperGrain,
            "The grain and the colour the hour puts over the yard. Looks only."));
    }

    /// <summary>Reading it — and being able to.</summary>
    private void BuildReading(Control parent)
    {
        VBoxContainer panel = UiKit.Section(parent, "Reading it");

        panel.AddChild(Toggle(
            "Text size",
            () => GameSettings.LargeText ? "one step up" : "as drawn",
            () => GameSettings.LargeText = !GameSettings.LargeText,
            "The whole stage is scaled, so every sheet still holds at the larger size."));

        panel.AddChild(Toggle(
            "Name the destinations",
            () => GameSettings.NameDestinations ? "always" : "under the cursor",
            () => GameSettings.NameDestinations = !GameSettings.NameDestinations,
            "Whether the yard's chalk names stay up, or wait for the cursor."));

        panel.AddChild(Toggle(
            "Reduced motion",
            () => GameSettings.ReducedMotion ? "on" : "off",
            () => GameSettings.ReducedMotion = !GameSettings.ReducedMotion,
            "No flicker and nothing moving on its own. The clock still runs."));

        panel.AddChild(Toggle(
            "The day log",
            () => $"{GameSettings.LogHold.ToString("0", CultureInfo.InvariantCulture)}s a line",
            () => GameSettings.LogHold = GameSettings.LogHold >= 8 ? 4 : GameSettings.LogHold + 2,
            "How long a line of the fight's report holds before the next one."));
    }

    /// <summary>The hands: what the keys do.</summary>
    private static void BuildHands(Control parent)
    {
        VBoxContainer panel = UiKit.Section(parent, "Hands");

        panel.AddChild(Reading("Walk up to a thing", "click it"));
        panel.AddChild(Reading("Close what is open", "Esc, or the corner of the sheet"));
        panel.AddChild(Reading("Stop the world", "Space, or Esc in the yard"));
        panel.AddChild(Reading("Set the clock's speed", "the strip, on the right"));
    }

    /// <summary>What the build has nothing behind, named rather than switched.</summary>
    private static void BuildUnwired(Control parent)
    {
        VBoxContainer panel = UiKit.Section(parent, "Not yet wired");

        panel.AddChild(Reading("Sound", "none yet"));
        panel.AddChild(Reading("Twitch · Kick", "not connected"));
    }

    /// <summary>
    /// A setting: what it is and what it is set to. The sentence about it is fetched, not printed.
    /// </summary>
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
            // The label is wired as well as the control, so the clause is there for a hand that is
            // reading the row rather than already reaching for the switch.
            label.MouseFilter = Control.MouseFilterEnum.Stop;
            UiKit.Explains(button, _line, clause);
            UiKit.Explains(label, _line, clause);
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
