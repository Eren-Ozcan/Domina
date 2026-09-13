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
    private Label _summary = null!;
    private VBoxContainer _tiles = null!;

    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

        VBoxContainer page = BuildPage();

        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_summary);

        ScrollContainer scroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        page.AddChild(scroll);

        _tiles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _tiles.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_tiles);

        page.AddChild(new Label
        {
            Text = "A village changes hands on the work you file for it, not from this board.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        Refresh();
    }

    public override void Refresh()
    {
        Clear(_tiles);

        ProvinceBoard board = ProvinceModel.Describe(_dojo);

        _summary.Text = board.UnderRaid
            ? $"Yours {board.Yours}  ·  his {board.His}  ·  free {board.Free}  ·  he is at the gate"
            : $"Yours {board.Yours}  ·  his {board.His}  ·  free {board.Free}"
              + $"  ·  he moves in {board.DaysToMove} days";

        _summary.AddThemeColorOverride("font_color", board.UnderRaid ? WarningColor : InkColor);

        foreach (SettlementTile tile in board.Settlements)
        {
            Label line = new()
            {
                Text = tile.Pressed
                    ? $"{ProvinceModel.Line(tile)}  ←  his next move"
                    : ProvinceModel.Line(tile),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };

            line.AddThemeColorOverride("font_color", Tint(tile));
            _tiles.AddChild(line);
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
