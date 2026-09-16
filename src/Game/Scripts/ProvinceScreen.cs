using System.Globalization;
using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The province, walked out to: a drawn sheet with the twelve villages pinned on it.
/// </summary>
/// <remarks>
/// <para>
/// The board was a list of tiles, which is the one thing a map is not. It is a sheet of paper the size
/// of the stage now — coast, river and roads drawn on it, the dojo in the middle of it, the villages
/// where they stand, and the two panels the design puts at its feet: what the bounty gate is waiting
/// for, and what a day on the road costs (design canvas → 5c).
/// </para>
/// <para>
/// It is a <b>picture, not a screen the player acts on</b> (GDD open decision #2): nothing on it can
/// be pressed, no journey is planned from it, and a village changes hands on the work taken at the
/// board. What it adds over the list is where those villages are in relation to each other and to the
/// man taking them.
/// </para>
/// <para>
/// The map carries no blood: it is drawn, and the ground is the only place that is not.
/// </para>
/// </remarks>
public sealed partial class ProvinceScreen : DojoScreen
{
    /// <inheritdoc/>
    /// <remarks>
    /// The map is somewhere the player has walked to, out through the gate, so there is no yard behind
    /// it to dim — only the strip, and a way back (design canvas → 7a, 7c).
    /// </remarks>
    protected override bool TakesTheStage => true;

    /// <inheritdoc/>
    protected override Color StageGround => UiKit.Pressed;

    /// <summary>Where each of the twelve villages sits on the sheet, in the design's own coordinates.</summary>
    /// <remarks>
    /// The places are fixed rather than drawn from the seed: a province whose map moved between terms
    /// would teach the player nothing, and the villages' names are fixed in the core for the same
    /// reason. They are laid out around the dojo so that the coast road, the river and the pass each
    /// carry a few of them.
    /// </remarks>
    private static readonly Vector2[] Places =
    [
        new(420, 10), new(800, 10), new(1180, 10),
        new(20, 150), new(20, 370), new(20, 560),
        new(1580, 150), new(1580, 370), new(1580, 560),
        new(420, 620), new(800, 620), new(1180, 620),
    ];

    /// <summary>How wide a card on the map is allowed to be.</summary>
    private const int CardWidth = 250;

    private DojoState _dojo = null!;
    private Control _sheet = null!;

    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

        VBoxContainer page = BuildPage(
            "the province",
            "Twelve villages, the man taking them, and what a day on the road costs. Nothing here "
            + "can be pressed — a village changes hands on the work you take at the board.");

        _sheet = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        page.AddChild(_sheet);

        Refresh();
    }

    public override void Refresh()
    {
        Clear(_sheet);

        DrawSheet();

        ProvinceBoard board = ProvinceModel.Describe(_dojo);

        for (int i = 0; i < board.Settlements.Count && i < Places.Length; i++)
        {
            Pin(board.Settlements[i], Places[i]);
        }

        Dojo();
        Patrons();
        Gate(board);
        Distance();
    }

    /// <summary>The sheet itself: the coast, the river and the roads, drawn once.</summary>
    private void DrawSheet()
    {
        Control drawn = new()
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        drawn.Draw += () =>
        {
            Color sea = new(0.78f, 0.74f, 0.64f);
            Color land = new(0.847f, 0.796f, 0.690f);
            Color drawnIn = new(0.70f, 0.65f, 0.55f);

            drawn.DrawRect(new Rect2(0, 0, 1860, 900), land);

            // The coast along the top: the sea above one drawn line.
            drawn.DrawColoredPolygon(
                [new Vector2(0, 0), new Vector2(1860, 0), new Vector2(1860, 90), new Vector2(0, 130)],
                sea);

            // The river, and the roads out of the yard.
            drawn.DrawPolyline(
                [new Vector2(940, 860), new Vector2(900, 700), new Vector2(980, 500), new Vector2(930, 300),
                 new Vector2(1000, 110)],
                drawnIn,
                6f);

            foreach (Vector2[] road in Roads())
            {
                drawn.DrawPolyline(road, drawnIn, 3f);
            }
        };

        _sheet.AddChild(drawn);
    }

    /// <summary>The roads, as the sheet draws them — from the dojo outward.</summary>
    private static Vector2[][] Roads() =>
    [
        [new Vector2(860, 380), new Vector2(560, 300), new Vector2(240, 190)],
        [new Vector2(860, 380), new Vector2(1240, 300), new Vector2(1560, 200)],
        [new Vector2(860, 380), new Vector2(560, 560), new Vector2(240, 620)],
        [new Vector2(860, 380), new Vector2(1240, 560), new Vector2(1560, 620)],
        [new Vector2(860, 380), new Vector2(880, 620), new Vector2(900, 740)],
    ];

    /// <summary>One village, where it stands, with who it pays written under it.</summary>
    private void Pin(SettlementTile tile, Vector2 at)
    {
        VBoxContainer column = new() { Position = at };
        column.AddThemeConstantOverride("separation", 5);
        _sheet.AddChild(column);

        Color colour = Tint(tile);

        Control mark = new() { CustomMinimumSize = new Vector2(18, 18) };
        mark.Draw += () => mark.DrawColoredPolygon(
            [new Vector2(9, 0), new Vector2(18, 9), new Vector2(9, 18), new Vector2(0, 9)],
            colour);
        column.AddChild(mark);

        PanelContainer card = new() { CustomMinimumSize = new Vector2(CardWidth, 0) };
        card.AddThemeStyleboxOverride(
            "panel",
            UiKit.FlatStyle(tile.Pressed ? UiKit.Surface : UiKit.Raised, colour, tile.Pressed ? 2 : 1));
        column.AddChild(card);

        VBoxContainer said = UiKit.Padded(card, 11, 7);
        said.AddThemeConstantOverride("separation", 1);
        said.AddChild(UiKit.OnPaper(tile.Name, UiKit.Ink, UiKit.HeadSize - 5, display: true));
        said.AddChild(UiKit.Body(Held(tile), UiKit.Muted, UiKit.NoteSize - 1, wrap: false));

        if (tile.Pressed)
        {
            said.AddChild(UiKit.Note("this is the one he moves on next", UiKit.Brick, wrap: false));
        }
    }

    /// <summary>The dojo itself, in the middle of everything it can reach.</summary>
    private void Dojo()
    {
        VBoxContainer column = new() { Position = new Vector2(820, 350) };
        column.AddThemeConstantOverride("separation", 5);
        _sheet.AddChild(column);

        Control mark = new() { CustomMinimumSize = new Vector2(40, 26) };
        mark.Draw += () => mark.DrawColoredPolygon(
            [new Vector2(0, 26), new Vector2(20, 0), new Vector2(40, 26)],
            UiKit.Ink);
        column.AddChild(mark);

        PanelContainer card = new();
        card.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Ink));
        UiKit.Padded(card, 14, 8).AddChild(
            UiKit.OnNight($"{_dojo.Name} · yours", UiKit.PaperInk, UiKit.HeadSize - 3, display: true));
        column.AddChild(card);
    }

    /// <summary>The three parties the dojo answers to, each where its business is.</summary>
    private void Patrons()
    {
        (Patron Who, string What, Vector2 At)[] parties =
        [
            (Patron.Clerk, "the offer queue", new Vector2(1160, 430)),
            (Patron.Guild, "what the market asks", new Vector2(440, 430)),
            (Patron.Temple, "charms, and the rite", new Vector2(800, 190)),
        ];

        foreach ((Patron who, string what, Vector2 at) in parties)
        {
            StandingTier tier = _dojo.Standing.TierOf(who);
            double worth = _dojo.Standing.Of(who);

            PanelContainer card = new() { Position = at, CustomMinimumSize = new Vector2(CardWidth, 0) };
            card.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Raised, UiKit.Indigo));
            _sheet.AddChild(card);

            VBoxContainer said = UiKit.Padded(card, 11, 7);
            said.AddThemeConstantOverride("separation", 1);
            said.AddChild(UiKit.OnPaper(
                $"{who.ToString()} · {tier.ToString().ToLowerInvariant()}",
                UiKit.Ink,
                UiKit.HeadSize - 5,
                display: true));
            said.AddChild(UiKit.Body(what, UiKit.Muted, UiKit.NoteSize - 1, wrap: false));
            said.AddChild(UiKit.Body(
                $"standing {worth.ToString("0", CultureInfo.InvariantCulture)} of 100",
                UiKit.Muted,
                UiKit.NoteSize - 1,
                wrap: false));
        }
    }

    /// <summary>What the bounty gate is still waiting for, counted in heads.</summary>
    private void Gate(ProvinceBoard board)
    {
        int taken = _dojo.Season.HeadsTaken;
        int wanted = _dojo.Season.Tuning.BountyGate;

        PanelContainer panel = new() { Position = new Vector2(1180, 760) };
        panel.AddThemeStyleboxOverride("panel", UiKit.PaperStyle(UiKit.Surface, shadow: 10));
        _sheet.AddChild(panel);

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 20);
        UiKit.Padded(panel, 20, 16).AddChild(row);

        VBoxContainer said = new();
        said.AddThemeConstantOverride("separation", 3);
        said.AddChild(UiKit.SectionLabel("The bounty gate"));
        said.AddChild(UiKit.OnPaper(
            $"{taken.ToString(CultureInfo.InvariantCulture)} of "
            + $"{wanted.ToString(CultureInfo.InvariantCulture)} heads taken",
            UiKit.Ink,
            UiKit.HeadSize + 2,
            display: true));
        said.AddChild(UiKit.Note(
            taken >= wanted
                ? "The gate is open; the last night will be called when the term runs out."
                : "The term closes without a last night unless the rest are brought in.",
            wrap: false));
        row.AddChild(said);

        // The heads as squares: a filled one is taken, an outlined one is still on its owner.
        HBoxContainer heads = new() { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        heads.AddThemeConstantOverride("separation", 4);
        row.AddChild(heads);

        for (int i = 0; i < wanted; i++)
        {
            PanelContainer head = new() { CustomMinimumSize = new Vector2(22, 44) };
            head.AddThemeStyleboxOverride(
                "panel",
                i < taken ? UiKit.FlatStyle(UiKit.Ink) : UiKit.FlatStyle(UiKit.Pressed, UiKit.Ink, 2));
            heads.AddChild(head);
        }

        _ = board;
    }

    /// <summary>What a day on the road costs, in the stores it actually takes.</summary>
    private void Distance()
    {
        Resources draw = _dojo.DailyDraw();
        Resources purse = _dojo.Resources;

        PanelContainer panel = new() { Position = new Vector2(20, 760), CustomMinimumSize = new Vector2(430, 0) };
        panel.AddThemeStyleboxOverride("panel", UiKit.PaperStyle(UiKit.Surface, shadow: 10));
        _sheet.AddChild(panel);

        VBoxContainer said = UiKit.Padded(panel, 20, 16);
        said.AddThemeConstantOverride("separation", 8);
        said.AddChild(UiKit.SectionLabel("What distance costs"));
        said.AddChild(UiKit.Body(
            $"A day away is {draw.Food.ToString(CultureInfo.InvariantCulture)} rice and "
            + $"{draw.Water.ToString(CultureInfo.InvariantCulture)} water whether anything happens on "
            + "it or not, and the wages are owed either way."));

        said.AddChild(Reading("Rice at the present drain", Days(purse.Food, draw.Food)));
        said.AddChild(Reading("Water", Days(purse.Water, draw.Water)));
        said.AddChild(Reading(
            "The rival moves again",
            ProvinceModel.Describe(_dojo).UnderRaid
                ? "he is at the gate"
                : $"in {ProvinceModel.Describe(_dojo).DaysToMove.ToString(CultureInfo.InvariantCulture)} days"));
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

    private static string Days(int stock, int draw) =>
        draw <= 0
            ? "nothing drawn"
            : $"{(stock / draw).ToString(CultureInfo.InvariantCulture)} days";

    /// <summary>Who the village pays, in one short line that fits a card on a map.</summary>
    private static string Held(SettlementTile tile) => tile.Held switch
    {
        Allegiance.Yours => "speaks for you",
        Allegiance.His => $"pays him · {Count(tile)}",
        _ => $"pays nobody · {Count(tile)}",
    };

    /// <summary>How far the dojo has got with winning it over.</summary>
    private static string Count(SettlementTile tile) =>
        $"{tile.Contracts.ToString(CultureInfo.InvariantCulture)} of "
        + $"{tile.ContractsNeeded.ToString(CultureInfo.InvariantCulture)} contracts";

    /// <summary>Whose the village is, read at a glance.</summary>
    private static Color Tint(SettlementTile tile) => tile.Held switch
    {
        Allegiance.Yours => UiKit.Indigo,
        Allegiance.His => UiKit.Brick,
        _ => UiKit.Muted,
    };
}
