using Domina.Core.Model;
using Godot;

namespace Domina.Game;

/// <summary>
/// The paper theatre: the pigments, the two faces, and the few pieces of paper every screen is cut from.
/// </summary>
/// <remarks>
/// <para>
/// The screens were grey boxes on a grey ground, which is what a management game looks like when
/// nobody has decided what it is made of. The design canvas decides: the world is a lit night, and
/// everything the player reads is a piece of paper laid over it — a sheet for a decision, a slip for
/// a single fact, a card for a thing on a sheet. Paper takes square corners and a hard cast shadow,
/// never a rounded one; ink is printed on paper, and the palest paper colour is the only text allowed
/// on the night.
/// </para>
/// <para>
/// Three rules hold the whole set together, and every part here exists to keep one of them:
/// the yard is never replaced, only dimmed (<see cref="Sheet"/>, <see cref="Slip"/>); a sheet has
/// exactly one dominant act and it is indigo (<see cref="Act"/>); and an act that cannot be taken back
/// is not indigo but cut out of the night with a brick edge (<see cref="Cut"/>).
/// </para>
/// <para>
/// It is engine-side only. Nothing here decides what is shown or whether a command is allowed — that
/// stays in <c>Domina.Presentation</c> (CLAUDE.md → architecture rule).
/// </para>
/// </remarks>
public static class UiKit
{
    // ── The night ────────────────────────────────────────────────────────────────────────────────

    /// <summary>The ground behind everything: the yard at night.</summary>
    public static readonly Color Ground = Hex(0x14110D);

    /// <summary>A deeper night — the title, and the ground a cut act is cut out of.</summary>
    public static readonly Color Night = Hex(0x0D0B09);

    /// <summary>Text on the night: the strip's figures, a chalked name, a notice that changes the season.</summary>
    public static readonly Color PaperInk = Hex(0xF0E7D2);

    /// <summary>Secondary text on the night — a unit, a note, the clause under a name.</summary>
    public static readonly Color NightMuted = Hex(0xA89C8A);

    /// <summary>The hairline that divides the strip, and any rule drawn on the night.</summary>
    public static readonly Color NightEdge = Hex(0x2E2720);

    // ── The paper ────────────────────────────────────────────────────────────────────────────────

    /// <summary>A sheet: the paper a decision is printed on.</summary>
    public static readonly Color Surface = Hex(0xE9DFC9);

    /// <summary>A card on a sheet — a row in a list, a contract on the board.</summary>
    public static readonly Color Raised = Hex(0xDCD0B6);

    /// <summary>Paper pressed flat: a refused act, an inset block, a quotation of an order.</summary>
    public static readonly Color Pressed = Hex(0xCFC1A4);

    /// <summary>The hairline between two pieces of paper.</summary>
    public static readonly Color Edge = Hex(0xC2B191);

    /// <summary>Ink on paper.</summary>
    public static readonly Color Ink = Hex(0x191512);

    /// <summary>Second ink: a unit, a note, a past record.</summary>
    public static readonly Color Muted = Hex(0x4F473D);

    // ── The four spent colours ───────────────────────────────────────────────────────────────────

    /// <summary>The eyebrow over a plate, and the one accent the night is allowed.</summary>
    public static readonly Color Heading = Hex(0xA97F3F);

    /// <summary>The one act a sheet exists for. Nothing else is indigo (GDD §12).</summary>
    public static readonly Color Indigo = Hex(0x2F4468);

    /// <summary>Text on indigo, when it is a clause under the act rather than the act itself.</summary>
    public static readonly Color IndigoInk = Hex(0xC3CEE4);

    /// <summary>The edge of a thing that cannot be undone, and a refusal printed on paper.</summary>
    public static readonly Color Brick = Hex(0x7A3527);

    /// <summary>The same, lit, for text on the night: the term lost, a cut act's own word.</summary>
    public static readonly Color BrickLit = Hex(0xD98A76);

    /// <summary>Positive: buyable, sendable, won.</summary>
    public static readonly Color Good = Hex(0x59613F);

    /// <summary>Pending: its turn has come but it cannot be afforded, its deadline is closing in.</summary>
    public static readonly Color Pending = Hex(0xA97F3F);

    /// <summary>Warning: a refused command, a death, a broken promise. On paper this is the brick.</summary>
    public static readonly Color Warning = Brick;

    /// <summary>Blood, and a limb coming away. Nowhere else (GDD §12).</summary>
    /// <remarks>
    /// <see cref="Warning"/> is the interface's red — a refusal, an empty chest, an act that cannot be
    /// taken back — and it is the brick of a roof tile, not of a wound. Vermilion is the scene's red
    /// and it is not an interface colour at all: if it is on screen, something has been cut.
    /// </remarks>
    public static readonly Color Vermilion = Hex(0xBE3A22);

    // ── The type scale ───────────────────────────────────────────────────────────────────────────

    /// <summary>A plate's own headline — the one line that names what the screen is about.</summary>
    public const int DisplaySize = 44;

    /// <summary>The screen's own title, and a chalked name in the yard.</summary>
    public const int TitleSize = 28;

    /// <summary>A sheet's head, and the name on a card that is the card's subject.</summary>
    public const int HeadSize = 22;

    /// <summary>A figure meant to be read at a glance — a purse, a fee, a count.</summary>
    public const int FigureSize = 22;

    /// <summary>A section heading: letterspaced, uppercase, and never loud.</summary>
    public const int SectionSize = 17;

    /// <summary>Body text.</summary>
    public const int BodySize = 17;

    /// <summary>A note under a line of body text.</summary>
    public const int NoteSize = 16;

    // ── The two faces ────────────────────────────────────────────────────────────────────────────

    /// <summary>The display face: names, figures, acts, anything the eye lands on first.</summary>
    /// <remarks>
    /// A mincho with brush-cut serifs. It carries the period without a single decorative frame, which
    /// is what lets every panel in the game be a plain rectangle of paper.
    /// </remarks>
    public static Font? Display { get; } = LoadFace("ShipporiMinchoB1-Regular");

    /// <summary>The display face, one weight up, for a head that has to hold a whole sheet.</summary>
    public static Font? DisplayStrong { get; } = LoadFace("ShipporiMinchoB1-SemiBold");

    /// <summary>The body face: the clause under a name, a note, the day's log.</summary>
    public static Font? BodyFace { get; } = LoadFace("ZenKakuGothicNew-Regular");

    /// <summary>
    /// The theme every screen hangs on its page, so the defaults are readable without a per-label override.
    /// </summary>
    /// <remarks>
    /// Built once and shared: a theme built per screen would be rebuilt on every screen change, and
    /// Godot compares theme resources by reference when it decides what to re-draw.
    /// </remarks>
    public static Theme Theme { get; } = BuildTheme();

    /// <summary>
    /// The strip along the top of the world: the day, the stores, what is coming, the hour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one piece of interface that is always on screen, and the only one that is not paper: it is
    /// printed straight onto the night over the top of the yard, so the yard is never pushed down by it.
    /// </para>
    /// <para>
    /// On the ground and on the map it carries less — the day, the place and the hour — because the
    /// stores are a thing you read at home. The caller decides that by what it puts in
    /// <paramref name="figures"/>.
    /// </para>
    /// </remarks>
    /// <param name="day">The day and its term — <c>"Day 23"</c>, <c>"of 60"</c>.</param>
    /// <param name="figures">Each store as a figure and its name — <c>("96", "rice · 13 days")</c>.</param>
    /// <param name="coming">What is due, and when — <c>("the summons", "in 4 days")</c>. Empty for none.</param>
    public static Control Strip(
        (string Figure, string Name) day,
        IReadOnlyList<(string Figure, string Name, Color? Colour)> figures,
        (string Name, string When)? coming = null,
        Control? hour = null)
    {
        ArgumentNullException.ThrowIfNull(figures);

        PanelContainer band = new();
        band.AddThemeStyleboxOverride("panel", FlatStyle(new Color(Ground, 0.82f)));

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 26);
        Padded(band, 40, 14).AddChild(row);

        row.AddChild(Figure(day.Figure, day.Name, TitleSize));
        row.AddChild(StripRule());

        HBoxContainer stores = new();
        stores.AddThemeConstantOverride("separation", 22);
        row.AddChild(stores);

        foreach ((string figure, string name, Color? colour) in figures)
        {
            stores.AddChild(Figure(figure, name, FigureSize, colour));
        }

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        if (coming is (string what, string when))
        {
            HBoxContainer due = new();
            due.AddThemeConstantOverride("separation", 10);
            due.AddChild(OnNight(what, NightMuted, NoteSize + 2));
            due.AddChild(OnNight(when, PaperInk, FigureSize, display: true));
            row.AddChild(due);
        }

        if (hour is not null)
        {
            row.AddChild(StripRule());
            row.AddChild(hour);
        }

        return band;
    }

    /// <summary>
    /// A sheet: the common case. Paper laid over the yard, with the yard still visible around it.
    /// </summary>
    /// <remarks>
    /// The head names the sheet and offers the way back, and that is the only way out a sheet has —
    /// there is no navigation bar, because every sheet closes onto the ground it opened over. The
    /// column returned is the body; the caller fills it and puts its one act along the foot.
    /// </remarks>
    /// <param name="parent">The layer the sheet opens over. It is dimmed by <see cref="Dim"/>.</param>
    /// <param name="title">The sheet's own head, in the object's own words — "the rack", "the board".</param>
    /// <param name="back">The way back, wired by the caller. Its text is set here.</param>
    public static VBoxContainer Sheet(Control parent, string title, Button back, string? line = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(back);

        MarginContainer margin = new();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 60);
        margin.AddThemeConstantOverride("margin_right", 60);
        margin.AddThemeConstantOverride("margin_top", 96);
        margin.AddThemeConstantOverride("margin_bottom", 60);
        parent.AddChild(margin);

        PanelContainer paper = new();
        paper.AddThemeStyleboxOverride("panel", PaperStyle(Surface, shadow: 10));
        margin.AddChild(paper);

        VBoxContainer column = Padded(paper, 28, 24);
        column.AddThemeConstantOverride("separation", 16);

        HBoxContainer head = new();
        head.AddThemeConstantOverride("separation", 16);
        column.AddChild(head);

        VBoxContainer named = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        named.AddThemeConstantOverride("separation", 2);
        named.AddChild(OnPaper(title, Ink, HeadSize + 4, display: true));

        if (line is not null)
        {
            named.AddChild(Note(line));
        }

        head.AddChild(named);

        back.Text = "back to the yard ✕";
        head.AddChild(WayOut(back));

        column.AddChild(Rule());
        return column;
    }

    /// <summary>
    /// A slip: one fact and one act, on a piece of paper smaller than the thing behind it.
    /// </summary>
    /// <remarks>
    /// The yard barely dims for a slip. It is what the well, a runner's message and a single choice
    /// about a single man are worth — anything that needs a second column is a <see cref="Sheet"/>.
    /// </remarks>
    public static VBoxContainer Slip(Control parent, string where, string what)
    {
        ArgumentNullException.ThrowIfNull(parent);

        CenterContainer centre = new();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(centre);

        PanelContainer paper = new() { CustomMinimumSize = new Vector2(440, 0) };
        paper.AddThemeStyleboxOverride("panel", PaperStyle(Surface, shadow: 8));
        centre.AddChild(paper);

        VBoxContainer column = Padded(paper, 18, 16);
        column.AddThemeConstantOverride("separation", 6);
        column.AddChild(Note(where));
        column.AddChild(OnPaper(what, Ink, HeadSize, display: true));
        return column;
    }

    /// <summary>The dimming the yard takes while something is open over it.</summary>
    /// <param name="weight">
    /// How much of the ground is left: a slip takes about a third, a sheet nearly three quarters. The
    /// ground is never taken to black — a screen the yard cannot be seen through is a screen that has
    /// replaced the yard, and only the ground and the map may do that.
    /// </param>
    public static Control Dim(float weight)
    {
        ColorRect veil = new() { Color = new Color(Ground, weight), MouseFilter = Control.MouseFilterEnum.Stop };
        veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return veil;
    }

    /// <summary>
    /// A notice: a slip of paper set down at the edge of the yard, reporting something that happened.
    /// </summary>
    /// <remarks>
    /// Nothing pops. A notice never blocks the yard, never needs dismissing and never carries a
    /// button — the ledger keeps them all, and three at most are on screen, oldest gone first. The bar
    /// down its left says what kind of thing it is: indigo for the day's own business, brick for a
    /// store running out, ochre for something that changes the season.
    /// </remarks>
    /// <param name="ofTheSeason">
    /// A notice that changes the season is printed on ink instead of paper, so the two are told apart
    /// at the edge of the eye without reading either.
    /// </param>
    public static Control Notice(string title, string line, string hour, Color? bar = null, bool ofTheSeason = false)
    {
        PanelContainer panel = new() { CustomMinimumSize = new Vector2(520, 0) };
        panel.AddThemeStyleboxOverride(
            "panel",
            ofTheSeason ? PaperStyle(Ink, shadow: 5) : PaperStyle(Raised, shadow: 5));

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 12);
        Padded(panel, 14, 12).AddChild(row);

        row.AddChild(new ColorRect
        {
            Color = bar ?? (ofTheSeason ? Heading : Indigo),
            CustomMinimumSize = new Vector2(5, 0),
        });

        VBoxContainer column = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(ofTheSeason
            ? OnNight(title, PaperInk, HeadSize - 3, display: true)
            : OnPaper(title, Ink, HeadSize - 3, display: true));
        column.AddChild(Body(line, ofTheSeason ? NightMuted : Muted, NoteSize));
        row.AddChild(column);

        row.AddChild(Body(hour, ofTheSeason ? NightMuted : Muted, NoteSize, wrap: false));
        return panel;
    }

    /// <summary>
    /// The act the sheet exists for: indigo ground, one per sheet, with the cost of it underneath.
    /// </summary>
    /// <remarks>
    /// Indigo is spent on nothing else in the game, so the player never has to look for the thing the
    /// screen is for. A sheet with two indigo acts is a sheet that has not decided what it is.
    /// </remarks>
    /// <param name="clause">
    /// What taking the act costs or does, in the terms of the thing spent — "18 rice, 12 water on
    /// setting out". Never a restatement of the act's own word.
    /// </param>
    public static Button Act(Button button, string clause = "")
    {
        ArgumentNullException.ThrowIfNull(button);

        Dress(button, Indigo, PaperInk, border: null);

        if (clause.Length > 0)
        {
            button.TooltipText = clause;
        }

        return button;
    }

    /// <summary>The way out: outlined paper, never filled, never coloured.</summary>
    public static Button WayOut(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        Dress(button, Raised, Ink, border: Ink);
        return button;
    }

    /// <summary>
    /// An act that is refused: pressed flat into the paper, still where the act will be, saying the
    /// number that refuses it.
    /// </summary>
    /// <remarks>
    /// A blocked act is never hidden and never brick. Brick is for what cannot be taken back; a thing
    /// the player cannot afford yet is not dangerous, it is simply not ready, and hiding it teaches
    /// the player nothing about what to save for.
    /// </remarks>
    public static Button Refused(Button button, string because)
    {
        ArgumentNullException.ThrowIfNull(button);

        Dress(button, Pressed, Muted, border: null);
        button.Disabled = true;
        button.AddThemeColorOverride("font_disabled_color", Muted);
        button.TooltipText = because;
        return button;
    }

    /// <summary>
    /// An act that cannot be taken back: cut out of the night, edged in brick, its corners cut off.
    /// </summary>
    /// <remarks>
    /// The shape itself is the warning, so the words never have to be "are you sure" — they name what
    /// is lost in the terms of the thing lost. A cut shape never carries the word <i>Close</i>, and
    /// <i>Close</i> is never cut.
    /// </remarks>
    public static Button Cut(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);

        Dress(button, Night, BrickLit, border: Brick, borderWidth: 2);
        return button;
    }

    /// <summary>
    /// The count beside a blocked act — <c>2 of 3 chosen</c> — that moves as the player chooses.
    /// </summary>
    /// <remarks>
    /// The refusal text already says why a command cannot be given, but it says it in a sentence the
    /// player has to read. The count says the same thing in two characters, and it is the one piece of
    /// the reference game's gating that we had in the model and never put on screen.
    /// </remarks>
    public static Control Counter(string label, int count, int limit, Color? color = null)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 7);
        row.AddChild(OnPaper(
            count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            color ?? (count == limit ? Ink : Muted),
            FigureSize,
            display: true));
        row.AddChild(Note($"of {limit.ToString(System.Globalization.CultureInfo.InvariantCulture)} {label}", wrap: false));
        return row;
    }

    /// <summary>
    /// The block a set of terms is read off: a label in the left column, its figure in the right.
    /// </summary>
    /// <remarks>
    /// The reference game's contract sheet puts what a job pays and what it costs on two lines with
    /// their labels aligned, and that alignment is the whole reason the pair can be compared at a
    /// glance. A run of sentences cannot be compared, which is what our board printed before.
    /// </remarks>
    public static GridContainer Terms(Control parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        GridContainer grid = new() { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 3);
        parent.AddChild(grid);
        return grid;
    }

    /// <summary>One line of a <see cref="Terms"/> block: what it is, and the figure it stands at.</summary>
    /// <param name="grid">The block the line is added to.</param>
    /// <param name="label">What the figure is — "reward", "setting out".</param>
    /// <param name="value">The figure itself, already worded.</param>
    /// <param name="mark">The emblem printed before the figure; <see cref="Mark.None"/> for none.</param>
    /// <param name="color">The figure's ink; the ordinary ink when it is not given.</param>
    public static void Term(GridContainer grid, string label, string value, Mark mark = Mark.None, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(grid);

        grid.AddChild(Body(label, Muted, NoteSize, wrap: false));

        HBoxContainer figure = new();
        figure.AddThemeConstantOverride("separation", 5);
        if (mark != Mark.None)
        {
            figure.AddChild(new CenterContainer { CustomMinimumSize = new Vector2(12, 0) });
            figure.GetChild<CenterContainer>(0).AddChild(Emblem(mark, color ?? Muted));
        }

        figure.AddChild(Body(value, color, BodySize, wrap: false));
        grid.AddChild(figure);
    }

    /// <summary>
    /// The fourth shape: not a decision but the report of one — an order that came back undone.
    /// </summary>
    /// <remarks>
    /// Paper ground, a brick rule along the top, the order quoted as it was given and then struck. It
    /// is drawn in full on board 10a: what was attempted, what went wrong, what it cost and what it
    /// did not, and what may be done about it now.
    /// </remarks>
    public static VBoxContainer Returned(Control parent, string ordered, string struckBy)
    {
        ArgumentNullException.ThrowIfNull(parent);

        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 10);
        parent.AddChild(column);

        column.AddChild(new ColorRect { Color = Brick, CustomMinimumSize = new Vector2(0, 3) });

        PanelContainer quoted = new();
        quoted.AddThemeStyleboxOverride("panel", FlatStyle(Pressed));
        column.AddChild(quoted);

        VBoxContainer inside = Padded(quoted, 14, 12);
        inside.AddThemeConstantOverride("separation", 4);
        inside.AddChild(Letterspaced("what was ordered", Muted, NoteSize));
        inside.AddChild(OnPaper(ordered, Brick, HeadSize - 2, display: true));
        inside.AddChild(Body(struckBy, Muted, NoteSize));
        return column;
    }

    /// <summary>
    /// A name chalked beside the thing it names, with the one clause the thing is for.
    /// </summary>
    /// <remarks>
    /// The yard has no tooltips and no floating cards: an object under the cursor lifts a step out of
    /// the dark and writes its own name on the ground beside its post. A name may add exactly one
    /// clause, and it is the single thing the object does — if a destination needs a second line to
    /// explain itself, the destination is wrong.
    /// </remarks>
    public static Control Chalk(string name, string clause)
    {
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 4);

        // Chalk is written across the ground, never down it: the label is laid out at the object's own
        // position with no container to give it a width, and a wrapping label with no width comes out
        // as one letter a line.
        column.AddChild(OnNight(name, PaperInk, TitleSize, display: true));
        column.AddChild(OnNight(clause, NightMuted, NoteSize + 2, display: false));

        column.MouseFilter = Control.MouseFilterEnum.Ignore;
        return column;
    }

    /// <summary>
    /// The screen's own name, with one line under it saying what the screen is for.
    /// </summary>
    public static Control PageHeader(string title, string purpose)
    {
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 3);
        column.AddChild(OnPaper(title, Ink, TitleSize, display: true));
        column.AddChild(Note(purpose));
        return column;
    }

    /// <summary>A section: a titled panel with a column inside it. Returns the column to fill.</summary>
    /// <param name="title">
    /// The heading, letterspaced and in the second ink; <c>null</c> for a panel with no heading.
    /// </param>
    /// <param name="fill">
    /// Whether the panel should take the height left over in its parent — a section holding a list
    /// that scrolls needs it; one holding three lines of text must not have it, or it stretches.
    /// </param>
    /// <param name="ratio">
    /// How much of a row of sections this one takes. Two sections side by side split the row evenly
    /// at the default; a section holding a wide table and sitting beside a list of short lines is
    /// given more, so the table is not scrolled sideways while the list sits half empty.
    /// </param>
    public static VBoxContainer Section(Control parent, string? title, bool fill = false, float ratio = 1f)
    {
        ArgumentNullException.ThrowIfNull(parent);

        PanelContainer panel = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = fill ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
            SizeFlagsStretchRatio = ratio,
        };
        panel.AddThemeStyleboxOverride("panel", PaperStyle(Raised, shadow: 5));
        parent.AddChild(panel);

        VBoxContainer column = Padded(panel, 16, 14);
        column.AddThemeConstantOverride("separation", 9);
        column.SizeFlagsVertical = fill ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin;

        if (title is not null)
        {
            column.AddChild(SectionLabel(title));
        }

        return column;
    }

    /// <summary>A heading for a section: letterspaced small caps, and no rule, no tick, no colour.</summary>
    /// <param name="onNight">
    /// Whether the heading sits on the night rather than on paper. On the night it takes the ochre,
    /// which is the only accent the night is allowed; on paper it takes the second ink, because ochre
    /// on paper is a stain rather than an accent.
    /// </param>
    public static Control SectionLabel(string text, bool onNight = false) =>
        Letterspaced(text, onNight ? Heading : Muted, SectionSize, display: true);

    /// <summary>A line of body text, wrapping unless it is told not to.</summary>
    /// <param name="wrap">
    /// <c>false</c> for a label whose parent gives it no width to wrap inside. A wrapping label in a
    /// <see cref="GridContainer"/> cell asks for the width of its longest word and the grid gives it
    /// that, so "Aggression" comes out as ten lines of one letter — which is what the market's
    /// comparison table did.
    /// </param>
    public static Label Body(string text = "", Color? color = null, int size = BodySize, bool wrap = true)
    {
        Label label = new()
        {
            Text = text,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Ink);

        if (BodyFace is Font face)
        {
            label.AddThemeFontOverride("font", face);
        }

        return label;
    }

    /// <summary>A note: small, in the second ink, and never the thing being read first.</summary>
    public static Label Note(string text = "", Color? color = null, bool wrap = true) =>
        Body(text, color ?? Muted, NoteSize, wrap);

    /// <summary>A line set in the display face on paper.</summary>
    public static Label OnPaper(string text, Color? color = null, int size = BodySize, bool display = false)
    {
        Label label = Body(text, color ?? Ink, size, wrap: false);

        if (display && Display is Font face)
        {
            label.AddThemeFontOverride("font", size >= TitleSize && DisplayStrong is Font strong ? strong : face);
        }

        return label;
    }

    /// <summary>The same, on the night.</summary>
    public static Label OnNight(string text, Color? color = null, int size = BodySize, bool display = false) =>
        OnPaper(text, color ?? PaperInk, size, display);

    /// <summary>A figure with its name beside it, the way the strip prints a store.</summary>
    public static Control Figure(string figure, string name, int size = FigureSize, Color? nameColour = null)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 7);
        row.AddChild(OnNight(figure, PaperInk, size, display: true));
        row.AddChild(OnNight(name, nameColour ?? NightMuted, NoteSize + 2));
        return row;
    }

    /// <summary>A row of chips, spaced and wrapping when the window is narrow.</summary>
    public static HFlowContainer ChipRow()
    {
        HFlowContainer row = new();
        row.AddThemeConstantOverride("h_separation", 8);
        row.AddThemeConstantOverride("v_separation", 8);
        return row;
    }

    /// <summary>
    /// A chip: one figure with its name and emblem under it, on its own small card.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The purse used to be a single run of "gold · food · water · medicine" text, which is the one
    /// thing a player reads at a glance and the one thing that run of text makes slowest to read. A
    /// chip gives each figure its own card, and the number is set larger than its name.
    /// </para>
    /// <para>
    /// The figure itself stays in ink whatever state the chip is in, and the state is carried by a bar
    /// along the bottom instead. A row of eight chips each painting its own number green, amber or red
    /// is the thing that made the day screen read as confetti.
    /// </para>
    /// </remarks>
    /// <param name="trend">
    /// What the figure is doing — <c>"−7 / day"</c>, <c>"13 days left"</c>. A stock with no trend is the
    /// reference game's own worst habit: it writes "800 food" and never what melts a day. Left empty
    /// for a figure that does not move on its own, like a headcount.
    /// </param>
    public static Control Chip(
        string figure,
        string name,
        Color? color = null,
        Mark mark = Mark.None,
        string trend = "")
    {
        PanelContainer panel = new() { CustomMinimumSize = new Vector2(92, 0) };
        panel.AddThemeStyleboxOverride("panel", PaperStyle(Raised, shadow: 4));

        VBoxContainer column = Padded(panel, 12, 9);
        column.AddThemeConstantOverride("separation", 2);
        column.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        Label value = OnPaper(figure, Ink, FigureSize, display: true);
        value.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(value);

        HBoxContainer caption = new() { Alignment = BoxContainer.AlignmentMode.Center };
        caption.AddThemeConstantOverride("separation", 5);

        if (mark != Mark.None)
        {
            caption.AddChild(Emblem(mark, Muted, NoteSize));
        }

        Label named = Body(name.ToUpperInvariant(), Muted, NoteSize - 2, wrap: false);
        caption.AddChild(named);
        column.AddChild(caption);

        if (trend.Length > 0)
        {
            Label moving = Body(trend, color ?? Muted, NoteSize - 1);
            moving.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(moving);
        }

        if (color is Color state && state != Ink)
        {
            column.AddChild(new ColorRect
            {
                Color = state,
                CustomMinimumSize = new Vector2(0, 3),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            });
        }

        return panel;
    }

    /// <summary>
    /// One man, written the same way on every screen: a portrait, his name and trade, a bar, one note.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A warrior was printed as three different row formats — a toggle button on the roster, a
    /// checkbox line on the day screen, a name and a number in the arena's HUD — so the player had to
    /// learn him three times. The reference game's one good structural decision is that its unit card
    /// is identical in the courtyard, the contract and the fight; this is that card.
    /// </para>
    /// <para>
    /// <paramref name="fraction"/> is deliberately unnamed: in the arena it is health, on the roster it
    /// is composure, on a patron it is regard. They are one concept in the fiction — how much of him is
    /// left to spend — and giving them one widget is what makes the screens read as one game.
    /// </para>
    /// </remarks>
    /// <param name="ours">
    /// Whether the man belongs to the dojo. <see cref="Indigo"/> down his edge is the only thing that
    /// separates your side from theirs in the arena, where both are dark paper figures.
    /// </param>
    /// <param name="lost">What he has already lost, so the head in the card carries his blinded eye.</param>
    /// <param name="alive">Is he alive? A dead man's head is printed in ash.</param>
    public static Control UnitCard(
        string name,
        string trade,
        double fraction,
        string note,
        Color? bar = null,
        bool ours = false,
        bool selected = false,
        Color? nameColor = null,
        BodyPartSet lost = default,
        bool alive = true)
    {
        PanelContainer panel = new();
        panel.AddThemeStyleboxOverride(
            "panel",
            PaperStyle(selected ? Surface : Raised, shadow: selected ? 6 : 4, border: selected ? Ink : null));

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 12);
        Padded(panel, 14, 11).AddChild(row);

        // The square was a hole in the card until the rig could draw a head into it. It is his own head:
        // the look is keyed on the name (see WarriorLook), so the man on this line is the man the arena
        // will fight with, and a list of six is six faces rather than six empty frames.
        PanelContainer portrait = new() { CustomMinimumSize = new Vector2(44, 44) };
        portrait.AddThemeStyleboxOverride("panel", FlatStyle(Pressed, border: ours ? Indigo : null, borderWidth: ours ? 3 : 0));
        portrait.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(portrait);

        if (name.Length > 0)
        {
            WarriorPortrait head = new(PortraitCrop.Head, new Vector2(44, 44), framed: false);
            head.Print(default, name, lost, alive);
            portrait.AddChild(head);
        }

        VBoxContainer column = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        row.AddChild(column);

        HBoxContainer heading = new();
        heading.AddThemeConstantOverride("separation", 8);
        column.AddChild(heading);

        Label named = OnPaper(name, nameColor ?? Ink, HeadSize - 3, display: true);
        named.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        named.ClipText = true;
        heading.AddChild(named);

        if (trade.Length > 0)
        {
            heading.AddChild(Note(trade, wrap: false));
        }

        column.AddChild(Bar(fraction, bar ?? Good, height: 6));

        if (note.Length > 0)
        {
            column.AddChild(Note(note, wrap: false));
        }

        return panel;
    }

    /// <summary>The same card, as something the player can pick.</summary>
    /// <remarks>
    /// The card is laid over a toggle button rather than drawn inside one: a Godot
    /// <see cref="Button"/> takes a single line of text, and the card is four rows. The children are
    /// told to ignore the mouse so the whole card stays one hit target instead of eating its own clicks.
    /// </remarks>
    public static Button UnitButton(
        string name,
        string trade,
        double fraction,
        string note,
        Color? bar = null,
        bool ours = false,
        bool selected = false,
        Color? nameColor = null,
        BodyPartSet lost = default,
        bool alive = true)
    {
        Button button = new()
        {
            ToggleMode = true,
            ButtonPressed = selected,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        StyleBoxEmpty empty = new();
        button.AddThemeStyleboxOverride("normal", empty);
        button.AddThemeStyleboxOverride("hover", empty);
        button.AddThemeStyleboxOverride("pressed", empty);
        button.AddThemeStyleboxOverride("focus", empty);

        Control card = UnitCard(name, trade, fraction, note, bar, ours, selected, nameColor, lost, alive);
        card.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(card);
        button.CustomMinimumSize = new Vector2(0, 70);

        return button;
    }

    /// <summary>
    /// A row in a list: a card the player can pick, marked when it is the one the detail is describing.
    /// </summary>
    /// <remarks>
    /// A list of toggle buttons all wearing the same grey is the thing that made the market and the
    /// school unreadable — nothing said which row the detail panel below was describing. The picked
    /// row is lifted onto the brighter paper and given the one ink edge on the sheet.
    /// </remarks>
    public static Button ListRow(string text, bool selected, Color? color = null)
    {
        Button button = new()
        {
            Text = text,
            Alignment = HorizontalAlignment.Left,
            ToggleMode = true,
            ButtonPressed = selected,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        Dress(
            button,
            selected ? Surface : Raised,
            color ?? Ink,
            border: selected ? Ink : null,
            shadow: selected ? 5 : 3);

        return button;
    }

    /// <summary>Marks a navigation tab as the screen being shown.</summary>
    /// <remarks>
    /// The yard replaced the tab bar — a destination is an object you walk to, not a word in a strip —
    /// but a sheet that browses a set of its own (the plates, the settings pages) still needs to say
    /// which of them is open. It is a name with a rule under it, and nothing else.
    /// </remarks>
    public static Button Tab(Button button, bool current)
    {
        ArgumentNullException.ThrowIfNull(button);

        StyleBoxFlat quiet = FlatStyle(new Color(0, 0, 0, 0));
        quiet.ContentMarginLeft = quiet.ContentMarginRight = 14;
        quiet.ContentMarginTop = quiet.ContentMarginBottom = 8;

        if (!current)
        {
            button.AddThemeStyleboxOverride("normal", quiet);
            button.AddThemeStyleboxOverride("hover", quiet);
            button.AddThemeStyleboxOverride("pressed", quiet);
            button.AddThemeColorOverride("font_color", Muted);
            return button;
        }

        StyleBoxFlat lit = FlatStyle(new Color(0, 0, 0, 0), border: Ink, borderWidth: 0);
        lit.BorderWidthBottom = 2;
        lit.ContentMarginLeft = lit.ContentMarginRight = 14;
        lit.ContentMarginTop = lit.ContentMarginBottom = 8;

        button.AddThemeStyleboxOverride("normal", lit);
        button.AddThemeStyleboxOverride("hover", lit);
        button.AddThemeStyleboxOverride("pressed", lit);
        button.AddThemeStyleboxOverride("disabled", lit);
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Ink);
        button.AddThemeColorOverride("font_disabled_color", Ink);
        return button;
    }

    /// <summary>
    /// A labelled bar with its own reading beside the label: health, composure, a patron's regard.
    /// </summary>
    /// <remarks>
    /// The reference game shows a gladiator's mood and a magistrate's goodwill with the same widget,
    /// which is the cheapest way to tell the player those are one idea. Ours does the same, and the
    /// reading to the right is always words or a figure — never a percentage of something unnamed.
    /// </remarks>
    public static Control Gauge(string label, string reading, double fraction, Color? fill = null)
    {
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 5);

        HBoxContainer line = new();
        line.AddThemeConstantOverride("separation", 8);
        column.AddChild(line);

        Label named = Note(label, wrap: false);
        named.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        line.AddChild(named);

        Label read = OnPaper(reading, Ink, BodySize, display: true);
        read.HorizontalAlignment = HorizontalAlignment.Right;
        line.AddChild(read);

        column.AddChild(Bar(fraction, fill ?? Indigo, height: 10));
        return column;
    }

    /// <summary>A bare bar: a trough pressed into the paper with a filled run in it.</summary>
    /// <remarks>
    /// Godot's <c>ProgressBar</c> would do this, and it brings a minimum height, a centred percentage
    /// label to switch off and a value range to keep in step with whatever is being shown. Two
    /// rectangles are cheaper to read in the code and to lay out at six pixels tall.
    /// </remarks>
    public static Control Bar(double fraction, Color fill, int height = 8)
    {
        PanelContainer trough = new() { CustomMinimumSize = new Vector2(0, height) };
        trough.AddThemeStyleboxOverride("panel", FlatStyle(Pressed));

        // A run is laid out by ratio rather than by pixels: the card is stretched by its container and
        // a pixel width measured now would be wrong by the time the screen is drawn.
        HBoxContainer split = new();
        trough.AddChild(split);

        float part = Math.Clamp((float)fraction, 0f, 1f);
        if (part > 0f)
        {
            split.AddChild(new ColorRect
            {
                Color = fill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = part,
            });
        }

        if (part < 1f)
        {
            split.AddChild(new Control
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 1f - part,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }

        return trough;
    }

    /// <summary>
    /// The two arrows that walk the roster from inside a detail panel, with the place in the list between them.
    /// </summary>
    /// <remarks>
    /// Going back to the list to read the next man is the thing that makes comparing two of them
    /// tedious, and the reference game solved it with a pair of arrows twenty years ago. The buttons are
    /// the caller's so it can wire and disable them; this only dresses them and sets them in a row.
    /// </remarks>
    public static Control Browse(Button previous, Button next, string position)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 8);

        previous.Text = "‹";
        next.Text = "›";
        row.AddChild(WayOut(previous));
        row.AddChild(Note(position, wrap: false));
        row.AddChild(WayOut(next));
        return row;
    }

    /// <summary>Marks a button as the one that cannot be taken back. The same shape as <see cref="Cut"/>.</summary>
    /// <remarks>
    /// In the reference game <c>Put to Death</c> is the same grey box as <c>Close</c> and sits beside
    /// it. Seppuku, sending a man off and abandoning the term are ours, and they are cut out of the
    /// night with a brick edge so the hand slows down before the click, not after it.
    /// </remarks>
    public static Button Danger(Button button) => Cut(button);

    /// <summary>Marks a button as the screen's main command. The same shape as <see cref="Act"/>.</summary>
    public static Button Primary(Button button) => Act(button);

    /// <summary>
    /// A small drawn emblem: a coin, a grain, a drop.
    /// </summary>
    /// <remarks>
    /// The marks are drawn out of circles and polygons rather than written as text. The bundled faces
    /// are subsetted to what the screens print, and an emblem drawn as a character would be one more
    /// glyph to keep in that set on every machine — a dozen lines of geometry each is cheaper and
    /// cannot come out as an empty box.
    /// </remarks>
    public static Control Emblem(Mark mark, Color? color = null, int size = 12) =>
        new MarkIcon { Mark = mark, Tint = color ?? Muted, CustomMinimumSize = new Vector2(size, size) };

    /// <summary>
    /// The chalk line at the foot of a sheet: one clause about whatever the hand is over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the yard's chalk name brought onto paper. The yard already answers "what is this" under
    /// the cursor and never in a floating card (design canvas -> 7b, 8a), and a sheet answers it the
    /// same way: the clause is printed in <b>one fixed place</b>, at the foot of the paper, rather
    /// than over the control the player is reaching for.
    /// </para>
    /// <para>
    /// The line keeps its height whether or not it is saying anything, so a sheet does not jump as the
    /// hand crosses it. Nothing that has to be read to act belongs here — the label and the reading on
    /// the control itself carry that, and this line only says what the setting is for.
    /// </para>
    /// </remarks>
    public static Label ChalkLine(Control column)
    {
        ArgumentNullException.ThrowIfNull(column);

        column.AddChild(Rule());

        Label line = Note(string.Empty, Muted, wrap: false);
        line.CustomMinimumSize = new Vector2(0, NoteSize + 6);
        line.VerticalAlignment = VerticalAlignment.Center;
        column.AddChild(line);
        return line;
    }

    /// <summary>
    /// Gives a control the clause it prints on the sheet's chalk line while it is under the hand.
    /// </summary>
    /// <remarks>
    /// <b>The focus is wired as well as the cursor.</b> A clause that only the mouse can reach is a
    /// clause a player on a pad never sees, and the game is aimed at a machine that is often played
    /// with one; the same clause is therefore printed when the control takes keyboard focus
    /// (WCAG 2.1 SC 1.4.13 asks for the same thing of anything shown on hover).
    /// </remarks>
    public static T Explains<T>(T control, Label line, string clause)
        where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(line);

        control.MouseEntered += () => line.Text = clause;
        control.FocusEntered += () => line.Text = clause;
        control.MouseExited += () => Erase(line, clause);
        control.FocusExited += () => Erase(line, clause);
        return control;
    }

    /// <summary>Clears the line, but only if it is still saying what this control put there.</summary>
    private static void Erase(Label line, string clause)
    {
        if (line.Text == clause)
        {
            line.Text = string.Empty;
        }
    }

    /// <summary>A horizontal rule — the cheapest way to end a block without adding another panel.</summary>
    public static Control Rule(bool onNight = false) =>
        new ColorRect
        {
            Color = onNight ? NightEdge : Edge,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

    /// <summary>
    /// A piece of paper: square corners, and a hard shadow cast down and to the right.
    /// </summary>
    /// <remarks>
    /// The shadow is offset rather than centred and takes no blur at all. A soft glow under a panel is
    /// how a web page floats a card; a sheet of paper on a table casts an edge, and the whole look of
    /// the game rests on the difference.
    /// </remarks>
    public static StyleBoxFlat PaperStyle(Color background, int shadow = 6, Color? border = null)
    {
        StyleBoxFlat style = FlatStyle(background, border, border is null ? 0 : 1);
        style.ShadowColor = new Color(0.04f, 0.03f, 0.02f, 0.45f);
        style.ShadowSize = shadow;
        style.ShadowOffset = new Vector2(shadow, shadow);
        return style;
    }

    /// <summary>A flat box with no shadow: a trough, a bar, the strip's ground.</summary>
    public static StyleBoxFlat FlatStyle(Color background, Color? border = null, int borderWidth = 1)
    {
        StyleBoxFlat style = new() { BgColor = background, BorderColor = border ?? Edge };
        style.SetCornerRadiusAll(0);
        style.SetBorderWidthAll(border is null ? 0 : borderWidth);
        return style;
    }

    /// <summary>Kept for the screens that still ask for a panel by its old name.</summary>
    public static StyleBoxFlat PanelStyle(Color background, int radius = 0, Color? border = null)
    {
        _ = radius;
        return PaperStyle(background, shadow: 5, border);
    }

    /// <summary>Wraps a margin around a child column and returns the column.</summary>
    public static VBoxContainer Padded(Control parent, int sides, int ends)
    {
        ArgumentNullException.ThrowIfNull(parent);

        MarginContainer margin = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_left", sides);
        margin.AddThemeConstantOverride("margin_right", sides);
        margin.AddThemeConstantOverride("margin_top", ends);
        margin.AddThemeConstantOverride("margin_bottom", ends);
        parent.AddChild(margin);

        VBoxContainer column = new();
        margin.AddChild(column);
        return column;
    }

    /// <summary>A letterspaced line in small caps — a section's name, a label over a quoted order.</summary>
    private static Label Letterspaced(string text, Color colour, int size, bool display = false)
    {
        // Godot's label has no letter-spacing, and the design's small caps are spaced wide enough that
        // the absence shows. A space between the characters is the same thing done by hand, and it
        // costs nothing — these lines are two or three words long and never wrap.
        string spaced = string.Join(" ", text.ToUpperInvariant().ToCharArray());
        return OnPaper(spaced, colour, size, display);
    }

    /// <summary>Paints a button: a ground, a word, and an edge when it is the way out.</summary>
    private static void Dress(Button button, Color ground, Color word, Color? border, int borderWidth = 1, int shadow = 4)
    {
        StyleBoxFlat normal = FlatStyle(ground, border, borderWidth);
        normal.ShadowColor = new Color(0.04f, 0.03f, 0.02f, 0.45f);
        normal.ShadowSize = shadow;
        normal.ShadowOffset = new Vector2(shadow, shadow);
        normal.ContentMarginLeft = normal.ContentMarginRight = 18;
        normal.ContentMarginTop = normal.ContentMarginBottom = 11;

        StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = ground.Lightened(0.08f);

        // Pressed means pressed: the paper goes down onto the table and the shadow it was casting goes
        // with it, which is the whole of the click's feedback.
        StyleBoxFlat down = (StyleBoxFlat)normal.Duplicate();
        down.BgColor = ground.Darkened(0.08f);
        down.ShadowSize = 0;

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", down);
        button.AddThemeStyleboxOverride("disabled", normal);
        button.AddThemeStyleboxOverride("focus", FlatStyle(new Color(0, 0, 0, 0), border ?? word, borderWidth));
        button.AddThemeColorOverride("font_color", word);
        button.AddThemeColorOverride("font_hover_color", word);
        button.AddThemeColorOverride("font_pressed_color", word);
        button.AddThemeColorOverride("font_disabled_color", word);

        if (Display is Font face)
        {
            button.AddThemeFontOverride("font", face);
        }

        button.AddThemeFontSizeOverride("font_size", HeadSize - 2);
    }

    /// <summary>A hairline standing up between two runs of the strip.</summary>
    private static Control StripRule() =>
        new ColorRect { Color = NightEdge, CustomMinimumSize = new Vector2(1, 0) };

    /// <summary>
    /// Loads one of the bundled faces, or gives back nothing and lets the engine's own font stand in.
    /// </summary>
    /// <remarks>
    /// The faces are imported resources, and a headless or half-imported project has none of them. A
    /// missing face must not take the screen down with it: every label asks for its face and carries on
    /// without one, which is also what keeps the presentation tests free of the engine.
    /// </remarks>
    private static Font? LoadFace(string name)
    {
        string path = $"res://Fonts/{name}.ttf";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Font>(path) : null;
    }

    private static Color Hex(uint rgb) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

    private static Theme BuildTheme()
    {
        Theme theme = new() { DefaultFontSize = BodySize };

        if (BodyFace is Font body)
        {
            theme.DefaultFont = body;
        }

        theme.SetColor("font_color", "Label", Ink);

        theme.SetFontSize("font_size", "Button", HeadSize - 2);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_hover_color", "Button", Ink);
        theme.SetColor("font_pressed_color", "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", Muted);

        if (Display is Font display)
        {
            theme.SetFont("font", "Button", display);
        }

        StyleBoxFlat rest = PaperStyle(Raised, shadow: 4);
        rest.ContentMarginLeft = rest.ContentMarginRight = 18;
        rest.ContentMarginTop = rest.ContentMarginBottom = 11;

        StyleBoxFlat over = (StyleBoxFlat)rest.Duplicate();
        over.BgColor = Surface;

        StyleBoxFlat down = (StyleBoxFlat)rest.Duplicate();
        down.BgColor = Pressed;
        down.ShadowSize = 0;

        StyleBoxFlat off = (StyleBoxFlat)rest.Duplicate();
        off.BgColor = Pressed;
        off.ShadowSize = 0;

        theme.SetStylebox("normal", "Button", rest);
        theme.SetStylebox("hover", "Button", over);
        theme.SetStylebox("pressed", "Button", down);
        theme.SetStylebox("disabled", "Button", off);
        theme.SetStylebox("focus", "Button", FlatStyle(new Color(0, 0, 0, 0), Ink));

        theme.SetStylebox("panel", "PanelContainer", PaperStyle(Surface));
        theme.SetStylebox("background", "ProgressBar", FlatStyle(Pressed));
        theme.SetStylebox("fill", "ProgressBar", FlatStyle(Indigo));

        theme.SetFontSize("font_size", "CheckBox", BodySize);
        theme.SetColor("font_color", "CheckBox", Ink);

        // A field is a line ruled on the paper, not a slab of the engine's default grey: the one place
        // the player writes on a sheet has to be made of the same stuff as the sheet.
        StyleBoxFlat field = FlatStyle(Pressed, Edge);
        field.ContentMarginLeft = field.ContentMarginRight = 12;
        field.ContentMarginTop = field.ContentMarginBottom = 8;
        theme.SetStylebox("normal", "LineEdit", field);
        theme.SetStylebox("focus", "LineEdit", FlatStyle(Surface, Ink));
        theme.SetStylebox("read_only", "LineEdit", FlatStyle(Pressed, Edge));
        theme.SetColor("font_color", "LineEdit", Ink);
        theme.SetColor("font_placeholder_color", "LineEdit", Muted);
        theme.SetColor("font_uneditable_color", "LineEdit", Muted);
        theme.SetColor("caret_color", "LineEdit", Ink);
        theme.SetColor("selection_color", "LineEdit", new Color(Indigo, 0.30f));
        theme.SetFontSize("font_size", "LineEdit", BodySize);

        return theme;
    }
}
