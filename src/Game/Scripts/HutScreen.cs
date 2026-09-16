using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The hut at dusk: who is lying inside it, and who has been called to stand in front of it.
/// </summary>
/// <remarks>
/// <para>
/// When a man is called the whole yard is standing in it, so the screen takes the stage rather than
/// opening as a sheet: there is no going about the yard's business while a man waits on the mat
/// (design canvas → 6f).
/// </para>
/// <para>
/// The ends are printed, not pressed. GDD §6 gives the verdict to the crowd — with the artificial
/// crowd standing in when nobody is watching — so what this screen owes the player is what each end
/// would cost him, and the one act on it is to send the yard to bed.
/// </para>
/// </remarks>
public sealed partial class HutScreen : DojoScreen
{
    /// <inheritdoc/>
    protected override bool TakesTheStage => true;

    private DojoState _dojo = null!;
    private VBoxContainer _page = null!;

    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

        _page = BuildPage(
            "the hut",
            "The lit window and the man on the mat. Nothing is named out here — the hut is what it is.");

        Refresh();
    }

    public override void Refresh()
    {
        // The head and the rule the page was built with stay; everything under them is reprinted.
        while (_page.GetChildCount() > 2)
        {
            Node last = _page.GetChild(_page.GetChildCount() - 1);
            _page.RemoveChild(last);
            last.QueueFree();
        }

        HutSheet sheet = TribunalModel.Describe(_dojo);

        if (sheet.Called is string called)
        {
            Called(called, sheet);
        }
        else
        {
            VBoxContainer quiet = UiKit.Section(_page, "Nobody is called tonight");
            quiet.AddChild(UiKit.Body(
                "A man stands here when his honour has fallen far enough that the yard will not let it "
                + "pass. Until then the hut is the infirmary and nothing else."));
        }

        Abed(sheet);
    }

    /// <summary>The man standing, his record, what the crowd has said, and the two ends.</summary>
    private void Called(string name, HutSheet sheet)
    {
        VBoxContainer called = new();
        called.AddThemeConstantOverride("separation", 6);
        _page.AddChild(called);

        called.AddChild(UiKit.SectionLabel("Called at dusk, in front of everyone", onNight: true));
        called.AddChild(UiKit.OnNight(name, UiKit.PaperInk, UiKit.DisplaySize, display: true));
        called.AddChild(UiKit.Body(sheet.Record, UiKit.NightMuted, UiKit.NoteSize + 2));

        PanelContainer said = new();
        said.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(new Color(UiKit.Night, 0.86f)));
        _page.AddChild(said);
        UiKit.Padded(said, 18, 16).AddChild(UiKit.Body(sheet.Tally, UiKit.PaperInk, UiKit.NoteSize + 2));

        HBoxContainer ends = new();
        ends.AddThemeConstantOverride("separation", 12);
        _page.AddChild(ends);

        foreach (TribunalEnd end in sheet.Ends)
        {
            ends.AddChild(End(end));
        }

        _page.AddChild(UiKit.Body(
            "The verdict is not yours to give. The crowd speaks, and where it is silent the yard "
            + "decides for itself at dawn.",
            UiKit.NightMuted,
            UiKit.NoteSize));
    }

    /// <summary>One end: paper when the man lives, cut out of the night when he does not.</summary>
    private static Control End(TribunalEnd end)
    {
        PanelContainer card = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride(
            "panel",
            end.Irreversible
                ? UiKit.FlatStyle(UiKit.Night, UiKit.Brick, 3)
                : UiKit.PaperStyle(UiKit.Surface, shadow: 12));

        VBoxContainer said = UiKit.Padded(card, 20, 18);
        said.AddThemeConstantOverride("separation", 6);

        said.AddChild(end.Irreversible
            ? UiKit.OnNight(end.Name, UiKit.BrickLit, UiKit.HeadSize + 6, display: true)
            : UiKit.OnPaper(end.Name, UiKit.Ink, UiKit.HeadSize + 6, display: true));

        said.AddChild(UiKit.Body(
            end.What,
            end.Irreversible ? UiKit.PaperInk : UiKit.Ink,
            UiKit.BodySize));

        said.AddChild(UiKit.Body(
            end.Cost,
            end.Irreversible ? UiKit.NightMuted : UiKit.Muted,
            UiKit.NoteSize));

        return card;
    }

    /// <summary>Who is lying in the hut, and when each of them is up.</summary>
    private void Abed(HutSheet sheet)
    {
        VBoxContainer inside = UiKit.Section(_page, "On the mats inside", fill: true);

        if (sheet.Abed.Count == 0)
        {
            inside.AddChild(UiKit.Body("Nobody. Every man in the yard is on his feet."));
            return;
        }

        foreach (AbedLine line in sheet.Abed)
        {
            HBoxContainer row = new();
            row.AddThemeConstantOverride("separation", 12);

            Label named = UiKit.OnPaper(line.Name, UiKit.Ink, UiKit.BodySize + 1, display: true);
            named.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(named);
            row.AddChild(UiKit.Note(line.Line, wrap: false));
            inside.AddChild(row);
        }
    }
}
