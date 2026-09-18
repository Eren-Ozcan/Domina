using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The terms of going out, and the picking of the men who will walk them.
/// </summary>
/// <remarks>
/// <para>
/// The sheet is the whole moment of sending: what it pays and what it costs above, the men and the
/// road standing facing each other in the middle, and the roster to pick from underneath. The party
/// used to be ticked on the day's board and the terms read afterwards, which asked the player to
/// choose his men before anything had told him what the road was worth.
/// </para>
/// <para>
/// The reference game puts the same things on one page — the terms above, the two sides below, the
/// two answers at the foot (docs/REFERENCE-DOMINA-UI.md). What is taken from it is that <b>layout</b>
/// and not its widgets.
/// </para>
/// <para>
/// It <b>decides nothing</b>. Whether a party may go is
/// <see cref="OfferModel.Judge(DojoState, IReadOnlyList{WarriorId})"/>'s to say and the sheet only
/// prints that verdict; refusing leaves the day exactly as it was, and the ticked men are handed back
/// so the board remembers them.
/// </para>
/// <para>
/// The men are drawn, not listed. Each figure is the arena's own rig at his own build
/// (<see cref="WarriorLook"/>), so the party weighed here is the party watched fighting, and the
/// road's men are drawn the same way when the hut has read them.
/// </para>
/// </remarks>
public sealed partial class SortieScreen : CanvasLayer
{
    private static readonly Color PaidColor = UiKit.Good;
    private static readonly Color CostColor = UiKit.Brick;

    private readonly HashSet<WarriorId> _party = [];

    private VBoxContainer _bargain = null!;
    private HBoxContainer _figures = null!;
    private HBoxContainer _heading = null!;
    private Label _verdict = null!;
    private Button _send = null!;
    private int _limit = 1;

    /// <summary>The dojo the men are being taken out of.</summary>
    public required DojoState Dojo { get; init; }

    /// <summary>The promise being kept; <c>null</c> when it is the day's own offer.</summary>
    public BountyContract? Contract { get; init; }

    /// <summary>The men already ticked when the sheet opened.</summary>
    public IReadOnlyList<WarriorId> Chosen { get; init; } = [];

    /// <summary>They go. The ticked men are handed over.</summary>
    public Action<IReadOnlyList<WarriorId>>? Accepted { get; set; }

    /// <summary>Not today. The selection is handed back so the board keeps it.</summary>
    public Action<IReadOnlyList<WarriorId>>? Refused { get; set; }

    public override void _Ready()
    {
        _party.UnionWith(Chosen);

        OfferCard offer = OfferModel.Describe(Dojo);
        _limit = offer.RequiredPartySize ?? offer.MaxPartySize;

        SortieSheet sheet = Read();

        Control page = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        AddChild(page);
        page.AddChild(UiKit.Dim(0.76f));

        Button away = new();
        away.Pressed += Refuse;

        VBoxContainer paper = UiKit.Sheet(page, sheet.Heading, away, sheet.Line);

        // Everything above the answer scrolls as one column. The sheet grows as men are ticked — each
        // one puts another figure on it — and on a short window the two answers at the foot were the
        // first thing to fall off the paper, which is the one part that must always be reachable.
        ScrollContainer body = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        paper.AddChild(body);

        VBoxContainer read = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        read.AddThemeConstantOverride("separation", 14);
        body.AddChild(read);

        _bargain = new VBoxContainer();
        read.AddChild(_bargain);

        read.AddChild(UiKit.Rule());
        Facing(read, sheet);

        read.AddChild(UiKit.Rule());
        Picking(read);

        _verdict = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        paper.AddChild(_verdict);

        HBoxContainer foot = new() { Alignment = BoxContainer.AlignmentMode.End };
        foot.AddThemeConstantOverride("separation", 10);
        paper.AddChild(foot);

        Button refuse = new() { Text = "Not today" };
        refuse.Pressed += Refuse;
        foot.AddChild(UiKit.Cut(refuse));

        _send = new Button { Text = "Open the gate" };
        _send.Pressed += () => Accepted?.Invoke([.. _party]);
        foot.AddChild(UiKit.Act(_send));

        Reprint();
    }

    /// <summary>The terms as they stand with the men currently ticked.</summary>
    private SortieSheet Read() =>
        Contract is not null && OfferModel.DescribeBounty(Dojo) is BountyCard bounty
            ? SortieModel.Describe(Dojo, bounty, [.. _party])
            : SortieModel.Describe(Dojo, [.. _party]);

    private void Refuse() => Refused?.Invoke([.. _party]);

    /// <summary>
    /// Reprints everything a tick changes: the terms, the figures, the count and the verdict.
    /// </summary>
    /// <remarks>
    /// The roster list itself is <b>not</b> rebuilt. It is built once and left alone, because rebuilding
    /// it would throw the scroll back to the top on every tick, and a player picking his fourth man
    /// would lose the place he was reading.
    /// </remarks>
    private void Reprint()
    {
        SortieSheet sheet = Read();

        Clear(_bargain);
        Bargain(_bargain, sheet);

        Clear(_figures);

        if (sheet.Party.Count == 0)
        {
            _figures.AddChild(UiKit.Body("Nobody is ticked yet.", UiKit.Muted, wrap: false));
        }

        foreach (RosterRow man in sheet.Party)
        {
            _figures.AddChild(Ours(man));
        }

        Clear(_heading);
        _heading.AddChild(new Label
        {
            Text = "Who goes on the expedition?",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });
        _heading.AddChild(UiKit.Counter("chosen", _party.Count, _limit));

        Judge();
    }

    /// <summary>Prints the verdict and dresses the one act beside it.</summary>
    /// <remarks>
    /// A blocked act is never hidden and never brick: it stays where the act will be, pressed into the
    /// paper, with the line beside it saying what refuses it (design canvas → 7a).
    /// </remarks>
    private void Judge()
    {
        List<WarriorId> party = [.. _party];
        PartyVerdict verdict = Contract is BountyContract contract
            ? OfferModel.JudgeBounty(Dojo, contract, party)
            : OfferModel.Judge(Dojo, party);

        if (verdict.Refusal is ExpeditionRefusal refusal)
        {
            _verdict.Text = RefusalText(refusal);
            _verdict.AddThemeColorOverride("font_color", UiKit.Warning);
            UiKit.Refused(_send, RefusalText(refusal));
            return;
        }

        _verdict.Text = party.Count >= _limit
            ? "The seats are full."
            : $"{party.Count} of {_limit} seats taken.";
        _verdict.AddThemeColorOverride("font_color", UiKit.Muted);
        UiKit.Act(_send, $"{party.Count} go out today");
        _send.Disabled = false;
    }

    /// <summary>The bargain: what winning is worth on the left, what it costs on the right.</summary>
    private static void Bargain(Control parent, SortieSheet sheet)
    {
        HBoxContainer columns = new();
        columns.AddThemeConstantOverride("separation", 34);
        parent.AddChild(columns);

        VBoxContainer paid = UiKit.Section(columns, "What it pays", fill: true);
        GridContainer pays = UiKit.Terms(paid);

        foreach (SortieTerm term in sheet.Pays)
        {
            UiKit.Term(pays, term.Label, term.Value, Mark.Coin, term.Grave ? CostColor : PaidColor);
        }

        VBoxContainer owed = UiKit.Section(columns, "What it costs", fill: true);
        GridContainer costs = UiKit.Terms(owed);

        foreach (SortieTerm term in sheet.Costs)
        {
            UiKit.Term(costs, term.Label, term.Value, Mark.None, term.Grave ? CostColor : null);
        }
    }

    /// <summary>The two sides, standing on the two edges of the sheet with the road between them.</summary>
    private void Facing(Control parent, SortieSheet sheet)
    {
        // The two columns take the height their cards need and no more: the roster underneath is what
        // the rest of the sheet belongs to, and an expanding pair of columns pushed it off the paper.
        HBoxContainer sides = new();
        sides.AddThemeConstantOverride("separation", 20);
        parent.AddChild(sides);

        VBoxContainer ours = UiKit.Section(sides, "The men going");
        _figures = new HBoxContainer();
        _figures.AddThemeConstantOverride("separation", 12);
        ours.AddChild(_figures);

        VBoxContainer between = new() { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        sides.AddChild(between);
        between.AddChild(UiKit.OnPaper("against", UiKit.Muted, UiKit.NoteSize, display: true));

        VBoxContainer theirs = UiKit.Section(sides, "What is on the road");

        HBoxContainer road = new();
        road.AddThemeConstantOverride("separation", 12);
        theirs.AddChild(road);

        if (!sheet.Read)
        {
            // An unread road is not an empty one. A blank column would say the road is clear; the card
            // marked unread says the dojo never paid to have it read (GDD §10 — only the band is free).
            road.AddChild(Unread());
            theirs.AddChild(UiKit.Body(
                "The hut has not read this road. The band above is all that can be known before the "
                + "gate opens.",
                UiKit.Muted));
            return;
        }

        foreach (EnemyLine line in sheet.Enemies)
        {
            road.AddChild(Theirs(line));
        }
    }

    /// <summary>The roster, ticked a man at a time. Built once; the ticking only changes the cards.</summary>
    private void Picking(Control parent)
    {
        VBoxContainer picked = UiKit.Section(parent, null, fill: true);

        _heading = new HBoxContainer();
        _heading.AddThemeConstantOverride("separation", 14);
        picked.AddChild(_heading);

        // No scroll of its own: the whole sheet scrolls, and a list that scrolled inside a scrolling
        // sheet would catch the wheel wherever the pointer happened to be.
        VBoxContainer list = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        picked.AddChild(list);

        IReadOnlyList<PartyCandidate> candidates = OfferModel.Candidates(Dojo);

        // A man who dropped off the roster must not stay ticked: the verdict would then refuse the
        // party over a man the player can no longer see.
        _party.IntersectWith(candidates.Where(candidate => candidate.Fit).Select(candidate => candidate.Id));

        if (candidates.Count == 0)
        {
            list.AddChild(UiKit.Body("Nobody left on the roster.", UiKit.Warning));
            return;
        }

        foreach (PartyCandidate candidate in candidates)
        {
            // The bar is the one gate this sheet cares about: a man is fit for the road or he is in the
            // infirmary. A fraction of his power would invite a comparison the resolver does not make.
            Button box = UiKit.UnitButton(
                candidate.Name,
                candidate.Fit ? $"power {candidate.Score:0}" : string.Empty,
                candidate.Fit ? 1 : 0,
                candidate.Fit ? "Fit for the road" : $"Infirmary — {candidate.RecoveryDaysRemaining} days",
                bar: candidate.Fit ? UiKit.Good : UiKit.Warning,
                ours: candidate.Fit,
                selected: _party.Contains(candidate.Id),
                nameColor: candidate.Fit ? null : UiKit.Muted);
            box.Disabled = !candidate.Fit;

            WarriorId id = candidate.Id;
            box.Toggled += pressed =>
            {
                if (pressed)
                {
                    _party.Add(id);
                }
                else
                {
                    _party.Remove(id);
                }

                Reprint();
            };

            list.AddChild(box);
        }
    }

    /// <summary>One of ours: his whole figure, his name, the spirits he walks out with, his blade.</summary>
    private static Control Ours(RosterRow man)
    {
        PanelContainer card = new();
        card.AddThemeStyleboxOverride("panel", UiKit.PaperStyle(UiKit.Raised, shadow: 4, border: UiKit.Indigo));

        VBoxContainer column = UiKit.Padded(card, 10, 10);
        column.AddThemeConstantOverride("separation", 5);

        // The whole man, not a bust: what is being weighed is whether to put a body on a road, and a
        // man missing a leg has to be visible as one.
        WarriorPortrait figure = new(PortraitCrop.Full, new Vector2(104, 150));
        figure.Print(man);
        column.AddChild(figure);

        column.AddChild(Named(man.Name));
        column.AddChild(UiKit.Bar(MoraleScale.Clamp(man.Morale) / MoraleScale.Max, UiKit.Indigo, height: 5));

        Label note = UiKit.Note(man.WeaponName, wrap: false);
        note.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(note);

        return card;
    }

    /// <summary>One of theirs, as the hut read him.</summary>
    private static Control Theirs(EnemyLine line)
    {
        PanelContainer card = new();
        card.AddThemeStyleboxOverride("panel", UiKit.PaperStyle(UiKit.Raised, shadow: 4, border: UiKit.Brick));

        VBoxContainer column = UiKit.Padded(card, 10, 10);
        column.AddThemeConstantOverride("separation", 5);

        WarriorPortrait figure = new(
            PortraitCrop.Full,
            new Vector2(104, 150),
            ink: new Color(0.46f, 0.24f, 0.20f));
        figure.Print(default, line.Name);
        column.AddChild(figure);

        column.AddChild(Named(line.Name));

        Label carried = UiKit.Note(line.Weapon, wrap: false);
        carried.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(carried);

        if (line.Stats is string stats)
        {
            Label numbers = UiKit.Note(stats);
            numbers.CustomMinimumSize = new Vector2(104, 0);
            column.AddChild(numbers);
        }

        return card;
    }

    /// <summary>The road nobody read: a card with a man-shaped hole where the figure would be.</summary>
    private static Control Unread()
    {
        PanelContainer card = new();
        card.AddThemeStyleboxOverride("panel", UiKit.PaperStyle(UiKit.Pressed, shadow: 0, border: UiKit.Edge));

        VBoxContainer column = UiKit.Padded(card, 10, 10);
        column.AddThemeConstantOverride("separation", 5);

        Label mark = UiKit.OnPaper("?", UiKit.Muted, UiKit.DisplaySize, display: true);
        mark.HorizontalAlignment = HorizontalAlignment.Center;
        mark.VerticalAlignment = VerticalAlignment.Center;
        mark.CustomMinimumSize = new Vector2(104, 150);
        column.AddChild(mark);

        Label named = UiKit.Note("unread", wrap: false);
        named.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(named);

        return card;
    }

    private static Label Named(string name)
    {
        Label named = UiKit.OnPaper(name, UiKit.Ink, UiKit.BodySize, display: true);
        named.HorizontalAlignment = HorizontalAlignment.Center;
        named.ClipText = true;
        named.CustomMinimumSize = new Vector2(104, 0);
        return named;
    }

    private static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string RefusalText(ExpeditionRefusal refusal) => refusal switch
    {
        ExpeditionRefusal.EmptyParty => "Nobody is ticked.",
        ExpeditionRefusal.StaleOffer => "This is not today's offer.",
        ExpeditionRefusal.WrongPartySize => "The party size does not fit this job.",
        ExpeditionRefusal.Unfit => "One of those ticked is not fit for an expedition.",
        _ => "One of those ticked is not on the roster.",
    };
}
