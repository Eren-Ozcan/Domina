using Domina.Core.Dojo;
using Domina.Presentation;
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
    // The palette lives in UiKit, which is what the screens are drawn out of; these stay so that the
    // screens written before it keep reading the same names for the same four colours.

    /// <summary>Ordinary text.</summary>
    protected static readonly Color InkColor = UiKit.Ink;

    /// <summary>Dimmed text — a disabled option, a past record.</summary>
    protected static readonly Color MutedColor = UiKit.Muted;

    /// <summary>Positive: buyable, sendable, won.</summary>
    protected static readonly Color GoodColor = UiKit.Good;

    /// <summary>Pending: its turn has come but it cannot be afforded, its deadline is closing in.</summary>
    protected static readonly Color PendingColor = UiKit.Pending;

    /// <summary>Warning: a refused command, a death, a broken promise.</summary>
    protected static readonly Color WarningColor = UiKit.Warning;

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

    /// <summary>
    /// The dojo's clock, so the screen can stop it while a decision is being made; <c>null</c> in a
    /// scene opened on its own.
    /// </summary>
    /// <remarks>
    /// The screen never turns the day itself — the hub owns the clock and the core owns the day. What a
    /// screen is allowed to do is <b>hold</b> it: the moment the player is halfway through choosing a
    /// party is not the moment to let the morning arrive underneath him (build step 8).
    /// </remarks>
    public DayClock? Clock { get; set; }

    /// <summary>
    /// Reprints what the screen shows.
    /// </summary>
    /// <remarks>
    /// With the clock running the day can turn while the player is standing on any of the screens, so
    /// the hub calls this on every rollover. A screen with nothing to reprint leaves it empty.
    /// </remarks>
    public virtual void Refresh()
    {
    }

    /// <summary>Builds the screen and prints the content.</summary>
    /// <param name="dojo">The dojo to show — the screen reads it and gives its commands to it.</param>
    public abstract void Build(DojoState dojo);

    /// <summary>Builds the ground, the margin and the navigation bar; returns the content column.</summary>
    protected VBoxContainer BuildPage()
    {
        ColorRect backdrop = new()
        {
            Color = UiKit.Ground,
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        AddChild(backdrop);

        MarginContainer margin = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(margin);

        VBoxContainer page = new();
        page.AddThemeConstantOverride("separation", 10);
        margin.AddChild(page);

        if (Chrome is not null)
        {
            page.AddChild(Chrome);
        }

        return page;
    }

    /// <summary>
    /// The same page, with the screen's name and the one line saying what it is for at the top of it.
    /// </summary>
    /// <param name="title">The screen's name — the same word the navigation tab carries.</param>
    /// <param name="purpose">
    /// One line: what the screen shows and what the player can do here. It is not flavour text; the
    /// screens are dense, and a player who arrives on one has to be told which decision it is holding.
    /// </param>
    protected VBoxContainer BuildPage(string title, string purpose)
    {
        VBoxContainer page = BuildPage();
        page.AddChild(UiKit.PageHeader(title, purpose));
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
    /// <summary>
    /// Wraps a button's action so that a screen which throws is written down rather than swallowed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Godot catches an exception thrown inside a signal handler, writes it into its own log and
    /// carries on. That is the right thing for the window and the wrong thing for the run: the move
    /// journal — the file a bug report is made of — would show the moves before the press and then
    /// nothing, as though the player had simply stopped playing. The fault belongs in the same file and
    /// in the same order as the moves that led to it.
    /// </para>
    /// <para>
    /// The screen is written and reprinted afterwards either way. A half-applied action leaves the
    /// interface describing a dojo that no longer exists, and that is how one bug becomes three.
    /// </para>
    /// </remarks>
    /// <param name="dojo">The dojo the fault is filed against.</param>
    /// <param name="act">What the button does.</param>
    /// <param name="where">The name it is filed under; the screen's own type by default.</param>
    protected Action Guarded(DojoState dojo, Action act, string? where = null)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        ArgumentNullException.ThrowIfNull(act);

        return () =>
        {
            try
            {
                act();
            }
            catch (Exception broken)
            {
                dojo.RecordFault(where ?? GetType().Name, broken);
                GD.PushError($"{GetType().Name}: {broken}");

                Persist();
                Refresh();
            }
        };
    }

}
