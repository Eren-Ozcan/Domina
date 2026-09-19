using Domina.Core.Model;
using Domina.Presentation;
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

    /// <summary>
    /// The men who walked out, as they stand now that the books are closed.
    /// </summary>
    /// <remarks>
    /// The report is written about the expedition; this is written about the <b>men</b>. A player who
    /// sent four out and lost one had to read that out of a paragraph, and the one thing he wanted to
    /// know — which of them it was — was a name in a sentence rather than a face with a body under it.
    /// Empty is allowed: the last night's bouts and a report the hub could not match to a party print
    /// the lines alone, as before.
    /// </remarks>
    public IReadOnlyList<RosterRow> Returned { get; init; } = [];

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

        if (Returned.Count > 0)
        {
            VBoxContainer men = UiKit.Section(sheet, "Who walked out, and what came back");
            HFlowContainer faces = new();
            faces.AddThemeConstantOverride("h_separation", 12);
            faces.AddThemeConstantOverride("v_separation", 10);
            men.AddChild(faces);

            foreach (RosterRow man in Returned)
            {
                Control card = UiKit.UnitCard(
                    man.Name,
                    Trade(man),
                    MoraleScale.Clamp(man.Morale) / MoraleScale.Max,
                    Came(man),
                    bar: Told(man),
                    ours: true,
                    nameColor: man.IsAlive ? null : Told(man),
                    lost: man.Lost,
                    alive: man.IsAlive,
                    kit: man.Kit);

                card.CustomMinimumSize = new Vector2(300, 0);
                faces.AddChild(card);
            }
        }

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

    /// <summary>What the fight did to this man, in the one line his card has room for.</summary>
    /// <remarks>
    /// The limbs come first when there are any: a man who came back walking and a man who came back
    /// without an arm are both "in the infirmary for six days", and only one of them is a season's
    /// worth of news.
    /// </remarks>
    private static string Came(RosterRow man)
    {
        if (!man.IsAlive)
        {
            return "Did not come back.";
        }

        string wound = Wound(man.Lost);

        if (man.RecoveryDaysRemaining > 0)
        {
            return wound.Length > 0
                ? $"{wound} — {man.RecoveryDaysRemaining} days in the hut"
                : $"{man.RecoveryDaysRemaining} days in the hut";
        }

        return wound.Length > 0 ? $"{wound} — on his feet" : "Walked back unhurt.";
    }

    /// <summary>What he no longer has. A man is not asked to carry a list on one line.</summary>
    private static string Wound(BodyPartSet lost)
    {
        if (lost == BodyPartSet.None)
        {
            return string.Empty;
        }

        List<string> gone = [];

        if (lost.HasFlag(BodyPartSet.SwordArm) || lost.HasFlag(BodyPartSet.OffArm))
        {
            gone.Add("an arm");
        }

        if (lost.HasFlag(BodyPartSet.RightLeg) || lost.HasFlag(BodyPartSet.LeftLeg))
        {
            gone.Add("a leg");
        }

        if (lost.HasFlag(BodyPartSet.Eye))
        {
            gone.Add("an eye");
        }

        return gone.Count == 0 ? string.Empty : $"Lost {string.Join(" and ", gone)}";
    }

    /// <summary>The colour of his bar — the one thing on the card read from across the sheet.</summary>
    private static Color Told(RosterRow man)
    {
        if (!man.IsAlive)
        {
            return UiKit.Vermilion;
        }

        return man.RecoveryDaysRemaining > 0 ? UiKit.Pending : UiKit.Good;
    }

    /// <summary>The word beside his name: his path, or what he is now instead.</summary>
    private static string Trade(RosterRow man) => man.IsAlive
        ? man.Path switch
        {
            WarriorPath.Blade => "blade",
            WarriorPath.Stone => "stone",
            WarriorPath.Shadow => "shadow",
            _ => string.Empty,
        }
        : "fallen";
}
