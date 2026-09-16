using Godot;

namespace Domina.Game;

/// <summary>
/// They come back through the gate: what the work paid, what it cost, and who did not come back.
/// </summary>
/// <remarks>
/// <para>
/// The report used to be a paragraph printed at the top of the day's screen, where it sat behind
/// whatever the player did next and was gone by the following morning. It is a sheet now, opened over
/// the yard at the moment the party walks back into it, and it closes onto the ground like every other
/// sheet (design canvas → 5d).
/// </para>
/// <para>
/// It states rather than asks: the fight is over and nothing on this sheet can be decided. The one act
/// on it is the way back in.
/// </para>
/// </remarks>
public sealed partial class AftermathScreen : CanvasLayer
{
    /// <summary>The headline — what the expedition came to and what it cost, in one line.</summary>
    public required string Headline { get; init; }

    /// <summary>The report itself, a line at a time, as the day's books were closed.</summary>
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>Back into the yard.</summary>
    public Action? Closed { get; set; }

    public override void _Ready()
    {
        Control page = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        AddChild(page);
        page.AddChild(UiKit.Dim(0.74f));

        Button back = new();

        if (Closed is Action closed)
        {
            back.Pressed += closed;
        }

        VBoxContainer sheet = UiKit.Sheet(
            page,
            Headline,
            back,
            "They came through the gate at dusk. The books are already closed.");

        VBoxContainer told = UiKit.Section(sheet, "What the day came to", fill: true);

        foreach (string line in Lines)
        {
            if (line.Length > 0)
            {
                told.AddChild(UiKit.Body(line));
            }
        }

        HBoxContainer foot = new() { Alignment = BoxContainer.AlignmentMode.End };
        sheet.AddChild(foot);

        Button into = new() { Text = "Into the yard" };

        if (Closed is Action away)
        {
            into.Pressed += away;
        }

        foot.AddChild(UiKit.Act(into));
    }
}
