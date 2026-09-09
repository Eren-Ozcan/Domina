using Domina.Core.Dojo;
using Godot;

namespace Domina.Game;

/// <summary>
/// The common skeleton of the dojo screens: the ground, the margin and the navigation bar at the top.
/// </summary>
/// <remarks>
/// <para>
/// The screens resembling each other is not a preference but a necessity: the player moves between four
/// screens during the day, and the place of the summary, the place of the list and the place of the
/// buttons must stay the same on every screen.
/// </para>
/// <para>
/// The navigation bar goes <b>inside</b> the screen, not on a separate layer laid over it: on a
/// separate layer it would cover the top row of every screen.
/// </para>
/// </remarks>
public abstract partial class DojoScreen : CanvasLayer
{
    /// <summary>Ordinary text.</summary>
    protected static readonly Color InkColor = new(0.82f, 0.82f, 0.78f);

    /// <summary>Dimmed text — a disabled option, a past record.</summary>
    protected static readonly Color MutedColor = new(0.45f, 0.45f, 0.48f);

    /// <summary>Positive: buyable, sendable, won.</summary>
    protected static readonly Color GoodColor = new(0.55f, 0.75f, 0.45f);

    /// <summary>Pending: its turn has come but it cannot be afforded, its deadline is closing in.</summary>
    protected static readonly Color PendingColor = new(0.78f, 0.70f, 0.32f);

    /// <summary>Warning: a refused command, a death, a broken promise.</summary>
    protected static readonly Color WarningColor = new(0.80f, 0.35f, 0.35f);

    /// <summary>
    /// The navigation bar to put at the top of the screen; <c>null</c> in a scene opened on its own.
    /// </summary>
    /// <remarks>
    /// It must be given <b>before</b> <see cref="Build"/> is called. Because the screens can also be
    /// can be opened on its own (each has its own scene), the bar is not compulsory.
    /// </remarks>
    public Control? Chrome { get; set; }

    /// <summary>
    /// Called when the screen changes the dojo; the side that writes the save listens to this.
    /// </summary>
    /// <remarks>
    /// The screen <b>does not write the save itself</b>: the only place that knows the file path and the
    /// slot is the hub. If every screen wrote its own, when the save is written would be spread over four files.
    /// </remarks>
    public Action? Changed { get; set; }

    /// <summary>Builds the screen and prints the content.</summary>
    /// <param name="dojo">The dojo to show — the screen reads it and gives its commands to it.</param>
    public abstract void Build(DojoState dojo);

    /// <summary>Builds the ground, the margin and the navigation bar; returns the content column.</summary>
    protected VBoxContainer BuildPage()
    {
        ColorRect backdrop = new()
        {
            Color = new Color(0.09f, 0.09f, 0.11f),
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        AddChild(backdrop);

        MarginContainer margin = new() { AnchorRight = 1, AnchorBottom = 1 };
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        AddChild(margin);

        VBoxContainer page = new();
        page.AddThemeConstantOverride("separation", 12);
        margin.AddChild(page);

        if (Chrome is not null)
        {
            page.AddChild(Chrome);
        }

        return page;
    }

    /// <summary>The dojo changed; it tells the hub to write the save.</summary>
    protected void Persist() => Changed?.Invoke();

    /// <summary>Deletes all the node's children — the first step of reprinting.</summary>
    protected static void Clear(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
