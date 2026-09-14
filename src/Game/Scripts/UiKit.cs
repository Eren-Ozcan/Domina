using Godot;

namespace Domina.Game;

/// <summary>
/// The screens' shared look: the palette, the type sizes and the few boxes every screen is built out of.
/// </summary>
/// <remarks>
/// <para>
/// The dojo screens were written as plain columns of labels, which reads as one grey paragraph the
/// moment a screen carries more than a few lines. Everything readable about them — the type scale, the
/// panel, the section heading, the chip — lives here rather than in the screens, so a screen cannot
/// invent its own spacing and the four screens the player moves between during a day keep looking like
/// one game.
/// </para>
/// <para>
/// It is engine-side only. Nothing here decides what is shown or whether a command is allowed — that
/// stays in <c>Domina.Presentation</c> (CLAUDE.md → architecture rule).
/// </para>
/// </remarks>
public static class UiKit
{
    /// <summary>The page behind everything.</summary>
    public static readonly Color Ground = new(0.068f, 0.070f, 0.082f);

    /// <summary>A panel raised off the ground.</summary>
    public static readonly Color Surface = new(0.108f, 0.111f, 0.128f);

    /// <summary>A panel raised off a panel — a card on a board, a row in a list.</summary>
    public static readonly Color Raised = new(0.146f, 0.150f, 0.170f);

    /// <summary>The hairline between a panel and the ground.</summary>
    public static readonly Color Edge = new(0.22f, 0.225f, 0.255f);

    /// <summary>Ordinary text.</summary>
    public static readonly Color Ink = new(0.88f, 0.877f, 0.845f);

    /// <summary>Secondary text — a unit, a note, a past record.</summary>
    public static readonly Color Muted = new(0.50f, 0.505f, 0.535f);

    /// <summary>A heading's text, and the one accent the screens are allowed.</summary>
    public static readonly Color Heading = new(0.72f, 0.62f, 0.40f);

    /// <summary>Positive: buyable, sendable, won.</summary>
    public static readonly Color Good = new(0.49f, 0.66f, 0.47f);

    /// <summary>Pending: its turn has come but it cannot be afforded, its deadline is closing in.</summary>
    public static readonly Color Pending = new(0.80f, 0.68f, 0.36f);

    /// <summary>Warning: a refused command, a death, a broken promise.</summary>
    public static readonly Color Warning = new(0.76f, 0.40f, 0.38f);

    /// <summary>The screen's own title.</summary>
    public const int TitleSize = 26;

    /// <summary>A section heading.</summary>
    public const int SectionSize = 14;

    /// <summary>A figure meant to be read at a glance — a purse, a fee.</summary>
    public const int FigureSize = 20;

    /// <summary>Body text.</summary>
    public const int BodySize = 16;

    /// <summary>A note under a line of body text.</summary>
    public const int NoteSize = 13;

    /// <summary>
    /// The theme every screen hangs on its page, so the defaults are readable without a per-label override.
    /// </summary>
    /// <remarks>
    /// Built once and shared: a theme built per screen would be rebuilt on every screen change, and
    /// Godot compares theme resources by reference when it decides what to re-draw.
    /// </remarks>
    public static Theme Theme { get; } = BuildTheme();

    /// <summary>
    /// The screen's own name, with one line under it saying what the screen is for.
    /// </summary>
    /// <remarks>
    /// Every screen carries one. The navigation bar says which tab is lit, but a tab is four
    /// characters wide and the screens are dense; the player arriving on a screen has to be told, on
    /// the screen, what he is looking at and what he can do with it.
    /// </remarks>
    public static Control PageHeader(string title, string purpose)
    {
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 2);

        Label name = new() { Text = title };
        name.AddThemeFontSizeOverride("font_size", TitleSize);
        name.AddThemeColorOverride("font_color", Ink);
        column.AddChild(name);

        Label line = new() { Text = purpose, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        line.AddThemeFontSizeOverride("font_size", NoteSize);
        line.AddThemeColorOverride("font_color", Muted);
        column.AddChild(line);

        return column;
    }

    /// <summary>A row of chips, spaced and wrapping when the window is narrow.</summary>
    public static HFlowContainer ChipRow()
    {
        HFlowContainer row = new();
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        return row;
    }

    /// <summary>A note: small, dim, and never the thing being read first.</summary>
    public static Label Note(string text = "", Color? color = null, bool wrap = true) =>
        Body(text, color ?? Muted, NoteSize, wrap);

    /// <summary>
    /// A row in a list: a panel the player can pick, marked when it is the one selected.
    /// </summary>
    /// <remarks>
    /// A list of toggle buttons all wearing the same grey is the thing that made the market and the
    /// school unreadable — nothing said which row the detail panel below was describing. The selected
    /// row is given the heading's colour as an edge, which is the one border on screen.
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

        button.AddThemeColorOverride("font_color", color ?? Ink);
        button.AddThemeColorOverride("font_hover_color", color ?? Ink);
        button.AddThemeColorOverride("font_pressed_color", color ?? Ink);

        if (selected)
        {
            button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(0.20f, 0.19f, 0.17f), Heading));
            button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.24f, 0.22f, 0.19f), Heading));
            button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.20f, 0.19f, 0.17f), Heading));
        }

        return button;
    }

    /// <summary>Marks a navigation tab as the screen being shown.</summary>
    /// <remarks>
    /// The bar used to mark the open tab by disabling it, which paints it in the disabled colour —
    /// the one tab the player is on was the dimmest thing in the bar, saying "unavailable" where it
    /// meant "you are here". It is lit instead, and left clickable so nothing about it reads as refused.
    /// </remarks>
    public static Button Tab(Button button, bool current)
    {
        ArgumentNullException.ThrowIfNull(button);

        // An unlit tab is stripped back to a word on the bar: six filled boxes in a row are six things
        // competing with the screen under them, and only one of them is telling the player anything.
        if (!current)
        {
            button.AddThemeStyleboxOverride("normal", TabStyle(new Color(0, 0, 0, 0), null));
            button.AddThemeStyleboxOverride("hover", TabStyle(Raised, null));
            button.AddThemeStyleboxOverride("pressed", TabStyle(Raised, null));
            button.AddThemeColorOverride("font_color", Muted);
            return button;
        }

        StyleBoxFlat lit = TabStyle(Raised, Heading);
        button.AddThemeStyleboxOverride("normal", lit);
        button.AddThemeStyleboxOverride("hover", TabStyle(new Color(0.19f, 0.19f, 0.21f), Heading));
        button.AddThemeStyleboxOverride("pressed", lit);
        button.AddThemeStyleboxOverride("disabled", lit);
        button.AddThemeColorOverride("font_color", Heading);
        button.AddThemeColorOverride("font_hover_color", Heading);
        button.AddThemeColorOverride("font_disabled_color", Heading);
        return button;
    }

    /// <summary>A tab's ground: no edge at all, and a bar under the one that is open.</summary>
    private static StyleBoxFlat TabStyle(Color background, Color? underline)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            BorderColor = underline ?? Edge,
        };

        style.SetBorderWidthAll(0);
        style.BorderWidthBottom = underline is null ? 0 : 2;
        style.ContentMarginLeft = 14;
        style.ContentMarginRight = 14;
        style.ContentMarginTop = 7;
        style.ContentMarginBottom = 7;
        return style;
    }

    /// <summary>A section: a titled panel with a column inside it. Returns the column to fill.</summary>
    /// <param name="title">
    /// The heading, printed small and in the heading colour; <c>null</c> for a panel with no heading.
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
        panel.AddThemeStyleboxOverride("panel", PanelStyle(Surface));
        parent.AddChild(panel);

        VBoxContainer column = Padded(panel, 14, 12);
        column.AddThemeConstantOverride("separation", 8);
        column.SizeFlagsVertical = fill ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin;

        if (title is not null)
        {
            column.AddChild(SectionLabel(title));
        }

        return column;
    }

    /// <summary>A heading for a section: a small accent tick, then the words, never loud.</summary>
    /// <remarks>
    /// The tick is what separates one section from the next when a screen carries five of them. It is
    /// the only place the accent colour is spent on decoration, and it is two pixels wide.
    /// </remarks>
    public static Control SectionLabel(string text)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 7);

        ColorRect tick = new()
        {
            Color = Heading,
            CustomMinimumSize = new Vector2(2, SectionSize - 2),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        row.AddChild(tick);

        Label label = new() { Text = text.ToUpperInvariant() };
        label.AddThemeFontSizeOverride("font_size", SectionSize);
        label.AddThemeColorOverride("font_color", Heading);
        row.AddChild(label);

        return row;
    }

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
        return label;
    }

    /// <summary>
    /// A chip: one figure with its name and emblem under it, on its own small panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The purse used to be a single run of "gold · food · water · medicine" text, which is the one
    /// thing on the day screen a player reads at a glance and the one thing that run of text makes
    /// slowest to read. A chip gives each figure its own box, and the number is set larger than its name.
    /// </para>
    /// <para>
    /// The figure itself stays in the ordinary ink whatever state the chip is in, and the state is
    /// carried by a bar along the bottom instead. A row of eight chips each painting its own number
    /// green, amber or red is the thing that made the day screen read as confetti: with the numbers
    /// steady the row scans as one instrument, and the bar still says which figure wants attention.
    /// </para>
    /// </remarks>
    public static Control Chip(string figure, string name, Color? color = null, Mark mark = Mark.None)
    {
        PanelContainer panel = new() { CustomMinimumSize = new Vector2(84, 0) };
        panel.AddThemeStyleboxOverride("panel", PanelStyle(Raised, radius: 3));

        VBoxContainer column = Padded(panel, 10, 7);
        column.AddThemeConstantOverride("separation", 2);
        column.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        Label value = new() { Text = figure, HorizontalAlignment = HorizontalAlignment.Center };
        value.AddThemeFontSizeOverride("font_size", FigureSize);
        value.AddThemeColorOverride("font_color", Ink);
        column.AddChild(value);

        HBoxContainer caption = new() { Alignment = BoxContainer.AlignmentMode.Center };
        caption.AddThemeConstantOverride("separation", 5);

        if (mark != Mark.None)
        {
            caption.AddChild(Emblem(mark, Muted, NoteSize + 1));
        }

        Label name_ = new() { Text = name.ToUpperInvariant() };
        name_.AddThemeFontSizeOverride("font_size", NoteSize - 1);
        name_.AddThemeColorOverride("font_color", Muted);
        caption.AddChild(name_);
        column.AddChild(caption);

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
    /// A small drawn emblem: a coin, a grain, a drop.
    /// </summary>
    /// <remarks>
    /// The marks are drawn out of circles and polygons rather than written as text. Godot's built-in
    /// font carries the Latin alphabet and little else, so a "◆" or a "✚" would come out as an empty
    /// box on a machine whose fallback font happens to lack it — and the alternative, shipping an icon
    /// font, is a dependency for twelve shapes worth a dozen lines each.
    /// </remarks>
    public static Control Emblem(Mark mark, Color? color = null, int size = 12) =>
        new MarkIcon { Mark = mark, Tint = color ?? Muted, CustomMinimumSize = new Vector2(size, size) };

    /// <summary>A horizontal rule — the cheapest way to end a block without adding another panel.</summary>
    public static Control Rule()
    {
        ColorRect rule = new()
        {
            Color = Edge,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        return rule;
    }

    /// <summary>A panel's ground: a flat box with a hairline edge.</summary>
    public static StyleBoxFlat PanelStyle(Color background, int radius = 4, Color? border = null)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            BorderColor = border ?? Edge,
        };

        style.SetBorderWidthAll(border is null ? 1 : 1);
        return style;
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

    /// <summary>Marks a button as the screen's main command: filled, rather than outlined.</summary>
    public static Button Primary(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);

        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(0.22f, 0.26f, 0.20f)));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.27f, 0.33f, 0.24f)));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.18f, 0.22f, 0.17f)));
        button.AddThemeColorOverride("font_color", Good);
        return button;
    }

    private static StyleBoxFlat ButtonStyle(Color background, Color? border = null)
    {
        StyleBoxFlat style = PanelStyle(background, radius: 3, border: border ?? Edge);
        style.ContentMarginLeft = 14;
        style.ContentMarginRight = 14;
        style.ContentMarginTop = 7;
        style.ContentMarginBottom = 7;
        return style;
    }

    private static Theme BuildTheme()
    {
        Theme theme = new() { DefaultFontSize = BodySize };

        theme.SetColor("font_color", "Label", Ink);

        theme.SetFontSize("font_size", "Button", BodySize);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_hover_color", "Button", new Color(1, 1, 0.95f));
        theme.SetColor("font_pressed_color", "Button", Heading);
        theme.SetColor("font_disabled_color", "Button", Muted);
        theme.SetStylebox("normal", "Button", ButtonStyle(Raised));
        theme.SetStylebox("hover", "Button", ButtonStyle(new Color(0.21f, 0.21f, 0.25f)));
        theme.SetStylebox("pressed", "Button", ButtonStyle(new Color(0.14f, 0.14f, 0.17f)));
        theme.SetStylebox("disabled", "Button", ButtonStyle(new Color(0.11f, 0.11f, 0.13f)));
        theme.SetStylebox("focus", "Button", PanelStyle(new Color(0, 0, 0, 0), radius: 3, border: Heading));

        theme.SetStylebox("panel", "PanelContainer", PanelStyle(Surface));
        theme.SetStylebox("background", "ProgressBar", PanelStyle(new Color(0.10f, 0.10f, 0.12f), radius: 2));
        theme.SetStylebox("fill", "ProgressBar", PanelStyle(Heading, radius: 2, border: Heading));

        theme.SetFontSize("font_size", "CheckBox", BodySize);
        theme.SetColor("font_color", "CheckBox", Ink);

        return theme;
    }
}
