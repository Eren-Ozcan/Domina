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
    /// The way back to the yard; <c>null</c> in a scene opened on its own.
    /// </summary>
    /// <remarks>
    /// It must be given <b>before</b> <see cref="Build"/> is called. There is no navigation bar any
    /// more — a screen is a sheet opened over the yard, and the only way out of a sheet is back onto
    /// the ground it opened over (design canvas → 7a). A screen opened on its own as a scene has
    /// nowhere to go back to, so the way out is not compulsory.
    /// </remarks>
    public Action? Back { get; set; }

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

    /// <summary>
    /// Whether the screen takes the whole stage instead of opening as a sheet over the yard.
    /// </summary>
    /// <remarks>
    /// Two screens do, and only two: the province and the ground. They are places the player has
    /// walked to, so there is no yard behind them to dim — only the strip, and a way back. Everything
    /// else is a sheet, and a sheet never replaces the yard (design canvas → 7a).
    /// </remarks>
    protected virtual bool TakesTheStage => false;

    /// <summary>
    /// What the whole stage is painted with when the screen takes it.
    /// </summary>
    /// <remarks>
    /// The ground is the night; the province is a sheet of paper the size of the stage, because a map
    /// is a drawn thing and carries no blood (design canvas → 5c, 7c).
    /// </remarks>
    protected virtual Color StageGround => UiKit.Ground;

    /// <summary>The sheet's own head, in the object's words — "the board", "the rack".</summary>
    protected virtual string SheetTitle => string.Empty;

    /// <summary>The one line under the head: what is decided here, in the object's own terms.</summary>
    protected virtual string SheetLine => string.Empty;

    /// <summary>Opens the screen over the yard and returns the column its content goes in.</summary>
    protected VBoxContainer BuildPage() => BuildPage(SheetTitle, SheetLine);

    /// <summary>
    /// The same, with the head named here rather than by the screen's own properties.
    /// </summary>
    /// <param name="title">The sheet's own head — the same word the thing in the yard is called.</param>
    /// <param name="purpose">
    /// One line: what the sheet shows and what the player can do here. It is not flavour text; a sheet
    /// is dense, and a player who has just walked to it has to be told which decision it is holding.
    /// </param>
    protected VBoxContainer BuildPage(string title, string purpose)
    {
        Control page = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        AddChild(page);

        if (TakesTheStage)
        {
            // Nothing is behind a place you have walked to, so the ground itself is painted rather than
            // the yard dimmed — and the way back is the screen's own, not a sheet's corner.
            ColorRect ground = new() { Color = StageGround, AnchorRight = 1, AnchorBottom = 1 };
            page.AddChild(ground);

            MarginContainer margin = new() { AnchorRight = 1, AnchorBottom = 1 };
            margin.AddThemeConstantOverride("margin_left", 60);
            margin.AddThemeConstantOverride("margin_right", 60);
            margin.AddThemeConstantOverride("margin_top", 96);
            margin.AddThemeConstantOverride("margin_bottom", 48);
            page.AddChild(margin);

            VBoxContainer stage = new();
            stage.AddThemeConstantOverride("separation", 14);
            margin.AddChild(stage);

            HBoxContainer head = new();
            head.AddThemeConstantOverride("separation", 16);
            stage.AddChild(head);

            VBoxContainer named = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            named.AddThemeConstantOverride("separation", 3);
            bool onPaper = StageGround.Luminance > 0.4f;
            named.AddChild(UiKit.OnPaper(
                title,
                onPaper ? UiKit.Ink : UiKit.PaperInk,
                UiKit.TitleSize,
                display: true));
            named.AddChild(UiKit.Body(
                purpose,
                onPaper ? UiKit.Muted : UiKit.NightMuted,
                UiKit.NoteSize));
            head.AddChild(named);

            if (Back is Action away)
            {
                Button back = new() { Text = "back to the yard" };
                back.Pressed += away;
                head.AddChild(UiKit.WayOut(back));
            }

            stage.AddChild(UiKit.Rule(onNight: true));
            return stage;
        }

        page.AddChild(UiKit.Dim(0.72f));

        Button close = new();

        if (Back is Action back_)
        {
            close.Pressed += back_;
        }
        else
        {
            close.Disabled = true;
        }

        VBoxContainer sheet = UiKit.Sheet(page, title, close, purpose.Length > 0 ? purpose : null);

        // The body scrolls inside the sheet: the sheet's own size is decided by the yard around it, and
        // a screen that grows past it must not push its own act off the foot of the paper.
        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        sheet.AddChild(scroll);

        VBoxContainer body = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(body);
        return body;
    }

    /// <summary>
    /// An order that came back undone: what was asked for, what struck it, and what it cost.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fourth shape of the set is not a decision but the report of one (design canvas -> 7a, 10a).
    /// A command the core refused after it was given is exactly that: the player asked, the world said
    /// no, and the screen owes him the order as he gave it, the reason, and — the part a refusal line
    /// never says — what it did <b>not</b> cost, so that he can tell a setback from a disaster.
    /// </para>
    /// <para>
    /// It opens over the screen it happened on rather than over the yard: the sheet is still open, the
    /// order was given on it, and closing this puts him back where he was standing.
    /// </para>
    /// </remarks>
    /// <param name="ordered">The order as it was given — the man, the thing, the price.</param>
    /// <param name="struckBy">What struck it, in the world's own words rather than the interface's.</param>
    /// <param name="cost">What the refusal cost: the thing he still does not have.</param>
    /// <param name="notCost">What it did not cost — usually the money, and the day.</param>
    protected void Returned(string ordered, string struckBy, string cost, string notCost)
    {
        Control page = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        AddChild(page);
        page.AddChild(UiKit.Dim(0.5f));

        CenterContainer centre = new() { AnchorRight = 1, AnchorBottom = 1 };
        page.AddChild(centre);

        PanelContainer sheet = new() { CustomMinimumSize = new Vector2(1180, 0) };
        sheet.AddThemeStyleboxOverride("panel", UiKit.PaperStyle(UiKit.Surface, shadow: 12));
        centre.AddChild(sheet);

        VBoxContainer column = UiKit.Padded(sheet, 28, 24);
        column.AddThemeConstantOverride("separation", 16);

        column.AddChild(UiKit.Body("it came back undone, at the counter", UiKit.Muted, UiKit.NoteSize));
        column.AddChild(UiKit.OnPaper("The work was not taken", UiKit.Ink, UiKit.TitleSize + 4, display: true));

        UiKit.Returned(column, ordered, struckBy);

        HBoxContainer cards = new();
        cards.AddThemeConstantOverride("separation", 12);
        column.AddChild(cards);

        cards.AddChild(Told("WHAT IT COST", cost, UiKit.Brick));
        cards.AddChild(Told("WHAT IT DID NOT COST", notCost, UiKit.Muted));

        HBoxContainer acts = new() { Alignment = BoxContainer.AlignmentMode.End };
        acts.AddThemeConstantOverride("separation", 10);
        column.AddChild(acts);

        Button again = new() { Text = "Ask again" };
        again.Pressed += () => page.QueueFree();
        acts.AddChild(UiKit.Act(again));

        Button stand = new() { Text = "Let it stand" };
        stand.Pressed += () => page.QueueFree();
        acts.AddChild(UiKit.WayOut(stand));
    }

    /// <summary>One of the cards under a returned order.</summary>
    private static Control Told(string label, string what, Color colour)
    {
        PanelContainer card = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", UiKit.PaperStyle(UiKit.Raised, shadow: 5));

        VBoxContainer said = UiKit.Padded(card, 15, 13);
        said.AddThemeConstantOverride("separation", 4);
        said.AddChild(UiKit.Body(string.Join(" ", label.ToCharArray()), UiKit.Muted, UiKit.NoteSize));
        said.AddChild(UiKit.Body(what, colour, UiKit.BodySize));
        return card;
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
