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
    /// <summary>The way back — the title screen.</summary>
    public Action? Closed { get; set; }

    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        SeasonEndCard card = SeasonModel.Close(dojo);
        VBoxContainer page = BuildPage(
            "The season is over",
            "How the run ended, what it cost, and the names on both sides of that.");

        VBoxContainer verdict = UiKit.Section(page, "The verdict");

        Label headline = UiKit.Body(
            card.Headline,
            card.Phase == SeasonPhase.Triumph ? GoodColor : WarningColor,
            UiKit.FigureSize);
        verdict.AddChild(headline);

        HFlowContainer figures = UiKit.ChipRow();
        verdict.AddChild(figures);

        figures.AddChild(UiKit.Chip($"{card.Days}", "days played", mark: Mark.Sun));
        figures.AddChild(UiKit.Chip($"{card.Victories}/{card.Battles}", "fights won", mark: Mark.Blade));
        figures.AddChild(UiKit.Chip($"{card.Heads}", "heads brought in", mark: Mark.Grave));
        figures.AddChild(UiKit.Chip(
            $"{card.MissedWeeks}",
            "weeks with nothing filed",
            card.MissedWeeks > 0 ? UiKit.Warning : UiKit.Ink));

        // The map is the half of the season a fight record cannot show: a run can be lost on the
        // province without a single bout going badly.
        figures.AddChild(UiKit.Chip($"{card.SettlementsHeld}", "villages yours", UiKit.Good, Mark.Banner));
        figures.AddChild(UiKit.Chip($"{card.SettlementsHis}", "villages his", UiKit.Warning, Mark.Banner));
        figures.AddChild(UiKit.Chip(
            $"{card.Sacks}",
            "times he stood unanswered",
            card.Sacks > 0 ? UiKit.Warning : UiKit.Ink));

        HBoxContainer columns = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12);
        page.AddChild(columns);

        Column(columns, $"Buried — {card.Dead.Count}", card.Dead, WarningColor);
        Column(columns, $"Walked out free — {card.Freed.Count}", card.Freed, GoodColor);

        Button back = UiKit.Primary(new Button
        {
            Text = "Back to the title",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        });
        back.Pressed += () => Closed?.Invoke();
        page.AddChild(back);
    }

    private static void Column(Control parent, string title, IReadOnlyList<string> names, Color color)
    {
        VBoxContainer column = UiKit.Section(parent, title, fill: true);

        ScrollContainer scroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        column.AddChild(scroll);

        VBoxContainer list = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(list);

        if (names.Count == 0)
        {
            list.AddChild(UiKit.Body("nobody", MutedColor));
            return;
        }

        foreach (string name in names)
        {
            list.AddChild(UiKit.Body(name, color));
        }
    }
}
