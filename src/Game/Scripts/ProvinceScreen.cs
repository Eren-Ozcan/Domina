using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The province board: twelve villages, who each one pays, and when he moves next (docs/GDD.md §10).
/// </summary>
/// <remarks>
/// <para>
/// It is a <b>picture, not a screen you act on</b> — Open Decision #2's footnote. There is no travel,
/// no routing and no fight started from here: what changes a village's hand is a contract finished for
/// it, which is taken on the day screen like any other work. So this screen has no buttons at all.
/// </para>
/// <para>
/// What it must never print is the rival's own counter. His bound is the season's one hidden pressure,
/// and a bar for it would turn the thing the player is supposed to read off the province into
/// arithmetic.
/// </para>
/// </remarks>
public sealed partial class ProvinceScreen : DojoScreen
{
    private DojoState _dojo = null!;
    private HFlowContainer _summaryRow = null!;
    private Label _summary = null!;
    private VBoxContainer _tiles = null!;

    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

        VBoxContainer page = BuildPage(
            "Province",
            "Twelve villages and who they pay. Nothing on this board can be pressed — a village "
            + "changes hands on the work you take on the day screen, and this is where you read the "
            + "result of it.");

        VBoxContainer standing = UiKit.Section(page, "Where the province stands");
        _summaryRow = UiKit.ChipRow();
        standing.AddChild(_summaryRow);

        _summary = UiKit.Note();
        standing.AddChild(_summary);

        VBoxContainer villages = UiKit.Section(page, "The villages", fill: true);
        villages.AddChild(UiKit.Note("Green is yours, red is his, and amber is a village leaning away from you."));

        ScrollContainer scroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        villages.AddChild(scroll);

        _tiles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _tiles.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_tiles);

        Refresh();
    }

    public override void Refresh()
    {
        Clear(_tiles);

        ProvinceBoard board = ProvinceModel.Describe(_dojo);

        Clear(_summaryRow);
        _summaryRow.AddChild(UiKit.Chip($"{board.Yours}", "yours", board.Yours > 0 ? UiKit.Good : UiKit.Warning));
        _summaryRow.AddChild(UiKit.Chip($"{board.His}", "his", board.His > 0 ? UiKit.Warning : UiKit.Ink));
        _summaryRow.AddChild(UiKit.Chip($"{board.Free}", "neither"));
        _summaryRow.AddChild(UiKit.Chip(
            board.UnderRaid ? "now" : $"{board.DaysToMove}",
            board.UnderRaid ? "he is at the gate" : "days to his next move",
            board.UnderRaid ? UiKit.Warning : UiKit.Pending));

        _summary.Text = board.UnderRaid
            ? "He is standing in your yard. Answer him, or the village he came for is his."
            : "He takes one village at a time, and the board says which one he is walking toward.";
        _summary.AddThemeColorOverride("font_color", board.UnderRaid ? WarningColor : UiKit.Muted);

        foreach (SettlementTile tile in board.Settlements)
        {
            PanelContainer panel = new();
            panel.AddThemeStyleboxOverride(
                "panel",
                UiKit.PanelStyle(UiKit.Raised, radius: 3, border: tile.Pressed ? UiKit.Warning : null));

            VBoxContainer box = UiKit.Padded(panel, 10, 6);
            box.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            box.AddChild(UiKit.Body(ProvinceModel.Line(tile), Tint(tile)));

            if (tile.Pressed)
            {
                box.AddChild(UiKit.Note("This is the one he moves on next.", UiKit.Warning));
            }

            _tiles.AddChild(panel);
        }
    }

    /// <summary>Whose the village is, read at a glance.</summary>
    private static Color Tint(SettlementTile tile) => tile.Held switch
    {
        Allegiance.Yours => tile.Warning > 0 ? PendingColor : GoodColor,
        Allegiance.His => WarningColor,
        _ => tile.Warning > 0 ? PendingColor : InkColor,
    };
}
