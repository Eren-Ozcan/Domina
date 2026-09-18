using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The closing screen: how the run ended and what it cost (docs/GDD.md §10).
/// </summary>
/// <remarks>
/// <para>
/// The two columns are the point of it. The dead are the price; <b>the men who walked out free</b> —
/// those released while the season ran and everyone still standing when it ended — are the score that
/// is not gold. The dojo is a sentence being served, so the ending is counted in people rather than in
/// the treasury.
/// </para>
/// <para>
/// There is one button and it goes back to the title: the run is over and nothing on this screen can
/// be acted on. A run that ended is not reopened — permadeath ends the season too.
/// </para>
/// </remarks>
public sealed partial class SeasonEndScreen : DojoScreen
{
    /// <inheritdoc/>
    /// <remarks>The book is closed; the yard it was kept in is not there to come back to.</remarks>
    protected override bool TakesTheStage => true;

    /// <summary>The way back — the title screen.</summary>
    public Action? Closed { get; set; }

    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        SeasonEndCard card = SeasonModel.Close(dojo);
        VBoxContainer page = BuildPage(
            "the term, counted",
            "How the term ended, what it cost, and the names on both sides of that.");

        Judgement(page, card, dojo);
        Letter(page, dojo);

        HBoxContainer columns = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        page.AddChild(columns);

        Turned(columns, dojo);
        Counted(columns, card, dojo);
        Gone(columns, card);
        Carried(columns, card, dojo);

        HBoxContainer foot = new() { Alignment = BoxContainer.AlignmentMode.End };
        foot.AddThemeConstantOverride("separation", 10);
        page.AddChild(foot);

        Button back = new() { Text = "Close the book here" };
        back.Pressed += () => Closed?.Invoke();
        foot.AddChild(UiKit.Act(back));
    }

    /// <summary>
    /// The one line the term is judged by, and what the province calls the dojo after it.
    /// </summary>
    /// <remarks>
    /// A term that ended badly is not printed on paper at all: it is cut out of the night with a brick
    /// edge, the same shape every act that cannot be taken back wears (design canvas -> 7c, 9a).
    /// </remarks>
    private static void Judgement(Control page, SeasonEndCard card, DojoState dojo)
    {
        bool won = card.Phase == SeasonPhase.Triumph;

        PanelContainer told = new();
        told.AddThemeStyleboxOverride(
            "panel",
            won ? UiKit.PaperStyle(UiKit.Surface, shadow: 12) : UiKit.FlatStyle(UiKit.Night, UiKit.Brick, 3));
        page.AddChild(told);

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 20);
        UiKit.Padded(told, 24, 20).AddChild(row);

        VBoxContainer said = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        said.AddThemeConstantOverride("separation", 4);
        said.AddChild(UiKit.Body(
            won
                ? "the last day of the term, and the book is closed on it"
                : "the book closed early",
            won ? UiKit.Muted : UiKit.NightMuted,
            UiKit.NoteSize));
        said.AddChild(won
            ? UiKit.OnPaper(card.Headline, UiKit.Ink, UiKit.TitleSize + 4, display: true)
            : UiKit.OnNight(card.Headline, UiKit.BrickLit, UiKit.TitleSize + 4, display: true));
        row.AddChild(said);

        VBoxContainer calls = new() { SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
        calls.AddThemeConstantOverride("separation", 2);
        calls.AddChild(UiKit.Body(
            "THE PROVINCE CALLS YOU",
            won ? UiKit.Muted : UiKit.NightMuted,
            UiKit.NoteSize,
            wrap: false));
        calls.AddChild(won
            ? UiKit.OnPaper(Called(dojo), UiKit.Ink, UiKit.HeadSize + 6, display: true)
            : UiKit.OnNight(Called(dojo), UiKit.PaperInk, UiKit.HeadSize + 6, display: true));
        row.AddChild(calls);
    }

    /// <summary>What the clerk's office thinks of the term, said as the letter that closes it.</summary>
    private static void Letter(Control page, DojoState dojo)
    {
        StandingTier tier = dojo.Standing.TierOf(Patron.Clerk);

        PanelContainer panel = new();
        panel.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Ink));
        page.AddChild(panel);

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 22);
        UiKit.Padded(panel, 24, 20).AddChild(row);

        PanelContainer seal = new() { CustomMinimumSize = new Vector2(64, 64) };
        seal.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Indigo));
        CenterContainer middle = new();
        seal.AddChild(middle);
        middle.AddChild(UiKit.OnNight("侍", UiKit.PaperInk, 30, display: true));
        row.AddChild(seal);

        VBoxContainer said = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        said.AddThemeConstantOverride("separation", 4);
        said.AddChild(UiKit.Body(
            "THE CLERK'S OFFICE, BY LETTER, ON THE LAST DAY",
            UiKit.NightMuted,
            UiKit.NoteSize));

        // The letter is addressed, because this is the one place in the term the province speaks to
        // the player rather than about the school (design canvas → 6b).
        said.AddChild(UiKit.Body(
            $"To {dojo.Instructor}, who keeps {dojo.Name}",
            UiKit.NightMuted,
            UiKit.BodySize));
        said.AddChild(UiKit.Body(Verdict(tier), UiKit.PaperInk, UiKit.HeadSize - 2));
        row.AddChild(said);
    }

    /// <summary>
    /// Where it turned: the days the term was decided on, printed before the figures.
    /// </summary>
    /// <remarks>
    /// The figures say what the term came to; these say where it went, which is the only part of a
    /// closing screen a player can do anything with next time (design canvas → 9a).
    /// </remarks>
    private static void Turned(Control parent, DojoState dojo)
    {
        VBoxContainer panel = UiKit.Section(parent, "Where it turned", fill: true);

        IReadOnlyList<TurningPoint> points = TurningPoints.Describe(dojo);

        if (points.Count == 0)
        {
            panel.AddChild(UiKit.Body(
                "Nothing was left unfiled, nobody went hungry and the rival never stood in the yard "
                + "unanswered. The term was decided in the fights."));
            return;
        }

        foreach (TurningPoint point in points)
        {
            HBoxContainer row = new();
            row.AddThemeConstantOverride("separation", 12);

            Label when = UiKit.OnPaper(
                point.Day,
                point.Grave ? UiKit.Brick : UiKit.Ink,
                UiKit.BodySize + 1,
                display: true);
            when.CustomMinimumSize = new Vector2(96, 0);
            row.AddChild(when);

            Label what = UiKit.Body(point.Line, point.Grave ? UiKit.Brick : UiKit.Ink, UiKit.NoteSize);
            what.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(what);

            panel.AddChild(row);
        }

        panel.AddChild(UiKit.Note("No single day loses a term. These are the ones that compounded."));
    }

    /// <summary>The term, counted: the figures the ledger would show.</summary>
    private static void Counted(Control parent, SeasonEndCard card, DojoState dojo)
    {
        VBoxContainer panel = UiKit.Section(parent, "The term, counted", fill: true);

        panel.AddChild(Reading("Days played", Figure(card.Days)));
        panel.AddChild(Reading("Fights", $"{Figure(card.Victories)} won of {Figure(card.Battles)}"));
        panel.AddChild(Reading(
            "Heads",
            $"{Figure(card.Heads)} of {Figure(dojo.Season.Tuning.BountyGate)} to the gate"));
        panel.AddChild(Reading("Weeks with nothing filed", Figure(card.MissedWeeks)));
        panel.AddChild(Reading("In the chest", $"{Figure(dojo.Resources.Gold)} koku"));
        panel.AddChild(Reading(
            "Villages",
            $"{Figure(card.SettlementsHeld)} yours · {Figure(card.SettlementsHis)} his"));
        panel.AddChild(Reading("Times he stood unanswered", Figure(card.Sacks)));
    }

    /// <summary>Who is not here for the next one, on ink because it is not a figure.</summary>
    private static void Gone(Control parent, SeasonEndCard card)
    {
        PanelContainer panel = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Ink));
        parent.AddChild(panel);

        VBoxContainer said = UiKit.Padded(panel, 18, 16);
        said.AddThemeConstantOverride("separation", 10);
        said.AddChild(UiKit.OnNight(
            string.Join(" ", "WHO IS NOT HERE FOR THE NEXT ON”".ToCharArray()),
            UiKit.NightMuted,
            UiKit.SectionSize,
            display: true));

        ScrollContainer scroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        said.AddChild(scroll);

        VBoxContainer names = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(names);

        if (card.Dead.Count == 0)
        {
            names.AddChild(UiKit.Body("Nobody. Every man who started the term finished it.", UiKit.PaperInk));
        }

        foreach (string name in card.Dead)
        {
            HBoxContainer row = new();
            row.AddThemeConstantOverride("separation", 12);

            Label named = UiKit.OnNight(name, UiKit.PaperInk, UiKit.BodySize + 1, display: true);
            named.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(named);
            row.AddChild(UiKit.Body("in the ground", UiKit.NightMuted, UiKit.NoteSize, wrap: false));
            names.AddChild(row);
        }

        said.AddChild(UiKit.Body(
            "Their names are cut into the stone at the shrine.",
            UiKit.NightMuted,
            UiKit.NoteSize));
    }

    /// <summary>
    /// What the term ends holding - which is where it stops, because there is no term after it.
    /// </summary>
    /// <remarks>
    /// The design's closing sheet offers a next term the men, the chest and the buildings (design
    /// canvas -> 6h). <b>The game is one term.</b> The campaign ends at the final tournament and
    /// nothing is played past it (docs/GDD.md §10, closed decision 2b), so the panel counts what the
    /// school ended with and says the book closes there. It is not a carry-over the build owes and
    /// has not paid: a second term is not a thing this game has.
    /// </remarks>
    private static void Carried(Control parent, SeasonEndCard card, DojoState dojo)
    {
        VBoxContainer panel = UiKit.Section(parent, "What the school ends holding", fill: true);

        panel.AddChild(Reading("Men still standing", Figure(card.Freed.Count)));
        panel.AddChild(Reading("The chest", $"{Figure(dojo.Resources.Gold)} koku"));
        panel.AddChild(Reading("The rack and the buildings", "kept until the book is closed"));
        panel.AddChild(UiKit.Rule());
        panel.AddChild(UiKit.Body(
            "The term is the whole game and this is the end of it — there is no term after this one "
            + "to carry the school into. Closing the book frees the slot, and a new school is opened "
            + "from the title with a seed of its own.",
            UiKit.Muted,
            UiKit.NoteSize));
    }

    /// <summary>A line with its figure set against the right edge.</summary>
    private static Control Reading(string what, string figure)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 12);

        Label label = UiKit.Body(what, UiKit.Ink, UiKit.BodySize, wrap: false);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);

        Label read = UiKit.OnPaper(figure, UiKit.Ink, UiKit.BodySize + 1, display: true);
        read.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(read);
        return row;
    }

    private static string Figure(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>What the province calls the dojo, off the clerk's own standing.</summary>
    private static string Called(DojoState dojo) => dojo.Standing.TierOf(Patron.Clerk) switch
    {
        StandingTier.Loyal => "Hatamoto",
        StandingTier.Pleased => "Bushi",
        StandingTier.Neutral => "A school of the province",
        StandingTier.Cold => "Barely a school",
        _ => "Ronin",
    };

    /// <summary>The letter itself, in the clerk's own register.</summary>
    private static string Verdict(StandingTier tier) => tier switch
    {
        StandingTier.Loyal =>
            "“The province asked and you answered, and it will say so where it matters. Keep the dojo.”",
        StandingTier.Pleased =>
            "“The work was done and the files are in order. The office will remember the school kindly.”",
        StandingTier.Neutral =>
            "“The term is closed. Nothing is owed either way, which is not the same as being thought well of.”",
        StandingTier.Cold =>
            "“Too much was left unanswered. The office has written it down, and it will be read at the next appointment.”",
        _ =>
            "“The province has no further use for the school, and the appointment will not be renewed.”",
    };
}
