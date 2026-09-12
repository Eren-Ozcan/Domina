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
        VBoxContainer page = BuildPage();

        Label headline = new()
        {
            Text = card.Headline,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        headline.AddThemeColorOverride(
            "font_color",
            card.Phase == SeasonPhase.Triumph ? GoodColor : WarningColor);
        page.AddChild(headline);

        page.AddChild(new Label
        {
            Text = string.Join(
                '\n',
                $"Days played: {card.Days}",
                $"Fights: {card.Battles}  ·  won {card.Victories}",
                $"Heads brought in: {card.Heads}",
                $"Weeks with no fight filed: {card.MissedWeeks}"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        HBoxContainer columns = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 32);
        page.AddChild(columns);

        columns.AddChild(Column($"Buried ({card.Dead.Count})", card.Dead, WarningColor));
        columns.AddChild(Column($"Walked out free ({card.Freed.Count})", card.Freed, GoodColor));

        Button back = new() { Text = "Back to the title" };
        back.Pressed += () => Closed?.Invoke();
        page.AddChild(back);
    }

    private static Control Column(string title, IReadOnlyList<string> names, Color color)
    {
        VBoxContainer column = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

        Label heading = new() { Text = title };
        heading.AddThemeColorOverride("font_color", color);
        column.AddChild(heading);

        ScrollContainer scroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        column.AddChild(scroll);

        VBoxContainer list = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(list);

        if (names.Count == 0)
        {
            Label none = new() { Text = "nobody" };
            none.AddThemeColorOverride("font_color", MutedColor);
            list.AddChild(none);
            return column;
        }

        foreach (string name in names)
        {
            list.AddChild(new Label { Text = name });
        }

        return column;
    }
}
