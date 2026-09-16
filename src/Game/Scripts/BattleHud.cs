using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The fight, read across a room: the two sides, the stream the simulation emits, and one order.
/// </summary>
/// <remarks>
/// <para>
/// The ground is a place the player has walked to, so there is no sheet and no yard behind it — only
/// the strip along the top and, at the foot, the two things a fight is watched with: what is happening,
/// and the single order that may be given about it (design canvas → 5a).
/// </para>
/// <para>
/// Health is printed as pips rather than as a bar, because a fight is read from the far side of a room
/// and a bar at 22% and a bar at 34% are the same picture. Ten pips are a number the eye can count.
/// </para>
/// <para>
/// What the retreat key says is decided by <see cref="HudModel"/> (engine-free, tested) and what the
/// stream says by <see cref="FightLog"/>; the job here is building the nodes and printing the text.
/// </para>
/// </remarks>
public sealed partial class BattleHud : CanvasLayer
{
    /// <summary>How many pips a man's health is counted in.</summary>
    private const int Pips = 10;

    /// <summary>How many lines of the stream stand at the foot of the screen.</summary>
    private const int Told = 5;

    private readonly Dictionary<WarriorId, WarriorPanel> _panels = [];
    private readonly HashSet<WarriorId> _ours = [];
    private readonly Dictionary<WarriorId, string> _names = [];

    private VBoxContainer _stream = null!;
    private Label _place = null!;
    private Label _notice = null!;
    private Label _order = null!;
    private Button _retreat = null!;
    private Control? _page;
    private Control? _confirm;
    private long _seed;
    private int _eventsTold;

    /// <summary>The last refused-press count processed — so the same press is not answered twice.</summary>
    private int _refusalsSeen;

    /// <summary>
    /// How many times the text that teaches the rule has been shown so far.
    /// </summary>
    /// <remarks>
    /// Its persistent form is the save's job; here it is kept for the session. When the save layer
    /// arrives this field should be filled from there, or the rule is explained again at every launch.
    /// </remarks>
    private int _teachingShown;

    /// <summary>Builds the interface.</summary>
    /// <param name="battle">The fight to show.</param>
    /// <param name="setup">The roster the names are read from.</param>
    /// <param name="seed">The seed shown in the strip — it makes reopening a fight possible.</param>
    /// <param name="onRetreat">Called when the "pull out" key is pressed — the whole party pulls out.</param>
    public void Build(Battle battle, BattleSetup setup, long seed, Action onRetreat)
    {
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(onRetreat);

        _seed = seed;

        // The name comes from the roster; it must be showable before there is any fight result.
        foreach (Warrior warrior in setup.PlayerSide.Concat(setup.EnemySide))
        {
            _names[warrior.Id] = warrior.Name;
        }

        foreach (Warrior warrior in setup.PlayerSide)
        {
            _ours.Add(warrior.Id);
        }

        Control page = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        page.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(page);

        page.AddChild(BuildStrip());
        BuildSides(page, battle);
        BuildStream(page);
        BuildOrder(page, onRetreat);
    }

    /// <summary>
    /// The strip, as the ground carries it: where this is, and that there is no way back yet.
    /// </summary>
    /// <remarks>
    /// The stores are not on it. They are read at home, and a man watching his two remaining fighters
    /// cannot spend a koku from the field (design canvas → 7a).
    /// </remarks>
    private Control BuildStrip()
    {
        PanelContainer band = new() { AnchorRight = 1 };
        band.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(new Color(UiKit.Ground, 0.9f)));

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 26);
        UiKit.Padded(band, 96, 18).AddChild(row);

        _place = UiKit.OnNight("The field", UiKit.PaperInk, UiKit.FigureSize, display: true);
        row.AddChild(_place);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _notice = UiKit.OnNight(
            "back to the yard — not until the field is clear",
            new Color(0.435f, 0.396f, 0.341f),
            UiKit.NoteSize + 2);
        row.AddChild(_notice);

        return band;
    }

    /// <summary>The two sides, standing on the two edges of the window with the fight between them.</summary>
    private void BuildSides(Control page, Battle battle)
    {
        MarginContainer margin = new() { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 96);
        margin.AddThemeConstantOverride("margin_right", 96);
        margin.AddThemeConstantOverride("margin_top", 130);
        margin.AddThemeConstantOverride("margin_bottom", 380);
        page.AddChild(margin);

        HBoxContainer sides = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        sides.AddThemeConstantOverride("separation", 20);
        margin.AddChild(sides);

        VBoxContainer ours = Side(sides, "Yours", UiKit.IndigoInk);
        sides.AddChild(new Control
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        VBoxContainer theirs = Side(sides, "Theirs", UiKit.Heading);

        foreach (CombatantSnapshot snapshot in battle.Snapshots())
        {
            bool mine = snapshot.Team == Battle.PlayerTeam;
            string name = _names.GetValueOrDefault(snapshot.Id, snapshot.Id.ToString());

            WarriorPanel panel = new(name, mine);
            (mine ? ours : theirs).AddChild(panel.Root);
            _panels[snapshot.Id] = panel;
        }
    }

    /// <summary>One side's column, under a letterspaced heading saying whose it is.</summary>
    private static VBoxContainer Side(Control parent, string whose, Color colour)
    {
        VBoxContainer column = new()
        {
            CustomMinimumSize = new Vector2(620, 0),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        column.AddThemeConstantOverride("separation", 10);
        parent.AddChild(column);

        Label heading = UiKit.OnNight(
            string.Join(" ", whose.ToUpperInvariant().ToCharArray()),
            colour,
            UiKit.SectionSize,
            display: true);
        column.AddChild(heading);
        return column;
    }

    /// <summary>What is happening: the last few lines of the stream the simulation emits.</summary>
    private void BuildStream(Control page)
    {
        PanelContainer panel = new()
        {
            Position = new Vector2(96, 700),
            CustomMinimumSize = new Vector2(620, 0),
        };
        panel.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(new Color(UiKit.Ground, 0.88f)));
        page.AddChild(panel);

        VBoxContainer column = UiKit.Padded(panel, 18, 16);
        column.AddThemeConstantOverride("separation", 9);
        column.AddChild(UiKit.OnNight(
            string.Join(" ", "WHAT IS HAPPENING".ToCharArray()),
            UiKit.NightMuted,
            UiKit.SectionSize,
            display: true));

        _stream = new VBoxContainer();
        _stream.AddThemeConstantOverride("separation", 7);
        column.AddChild(_stream);
    }

    /// <summary>The only order that may be given, with what it costs beside it.</summary>
    private void BuildOrder(Control page, Action onRetreat)
    {
        PanelContainer panel = new()
        {
            Position = new Vector2(1120, 860),
            CustomMinimumSize = new Vector2(700, 0),
        };
        panel.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(new Color(UiKit.Ground, 0.88f)));
        page.AddChild(panel);

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 18);
        UiKit.Padded(panel, 18, 14).AddChild(row);

        VBoxContainer said = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        said.AddThemeConstantOverride("separation", 3);
        said.AddChild(UiKit.OnNight(
            string.Join(" ", "THE ONLY ORDER YOU MAY GIVE".ToCharArray()),
            UiKit.NightMuted,
            UiKit.NoteSize));
        said.AddChild(UiKit.Body(
            "They fight it themselves. You can take a man off the field, and nothing else.",
            UiKit.PaperInk,
            UiKit.NoteSize));

        _order = UiKit.Body(string.Empty, UiKit.NightMuted, UiKit.NoteSize);
        said.AddChild(_order);
        row.AddChild(said);

        _retreat = new Button();
        _retreat.Pressed += () => Confirm(onRetreat);
        row.AddChild(UiKit.Cut(_retreat));
        _page = page;
    }

    /// <summary>
    /// The cost of the order, stated before it is given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pulling out cannot be taken back, and the design's rule for an irreversible act is that the
    /// confirm says what is lost in the terms of the thing lost — never "are you sure" (design canvas →
    /// 5b, 7a). The three cards are the three things the order decides: the men come home, the work is
    /// given up, and the crowd watched it happen.
    /// </para>
    /// <para>
    /// The order is the party's, not one man's: the single key that pulls everybody is GDD §5, and the
    /// canvas's per-man version would make the right play "pull the wounded one, fight on with the
    /// rest" — which is the decision the design deliberately does not offer.
    /// </para>
    /// </remarks>
    private void Confirm(Action onRetreat)
    {
        if (_confirm is not null || _page is null)
        {
            return;
        }

        Control veil = new() { AnchorRight = 1, AnchorBottom = 1 };
        veil.AddChild(UiKit.Dim(0.72f));
        _page.AddChild(veil);
        _confirm = veil;

        CenterContainer centre = new() { AnchorRight = 1, AnchorBottom = 1 };
        veil.AddChild(centre);

        PanelContainer sheet = new() { CustomMinimumSize = new Vector2(1180, 0) };
        sheet.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Night, UiKit.Brick, 3));
        centre.AddChild(sheet);

        VBoxContainer column = UiKit.Padded(sheet, 28, 24);
        column.AddThemeConstantOverride("separation", 16);

        column.AddChild(UiKit.Body("an order that cannot be taken back", UiKit.NightMuted, UiKit.NoteSize));
        column.AddChild(UiKit.OnNight("Take them off the field", UiKit.PaperInk, UiKit.DisplaySize, display: true));

        HBoxContainer cards = new();
        cards.AddThemeConstantOverride("separation", 12);
        column.AddChild(cards);

        cards.AddChild(Consequence(
            "THEY LIVE",
            "and come home wounded",
            "Whoever is still standing walks off. The infirmary takes them, and the medicine chest pays for it."));

        cards.AddChild(Consequence(
            "THE WORK IS GIVEN UP",
            "the contract goes unpaid",
            "The day is spent, the fee is not earned, and the head you were sent for stays on its shoulders."));

        cards.AddChild(Consequence(
            "THE CROWD SEES IT",
            "and remembers it",
            "They came to watch a fight finish. A withdrawal is not a disgrace, but it is not what they paid for."));

        HBoxContainer acts = new() { Alignment = BoxContainer.AlignmentMode.End };
        acts.AddThemeConstantOverride("separation", 10);
        column.AddChild(acts);

        Button stand = new() { Text = "Let it stand" };
        stand.Pressed += CloseConfirm;
        acts.AddChild(UiKit.WayOut(stand));

        Button pull = new() { Text = "Take them off the field" };
        pull.Pressed += () =>
        {
            CloseConfirm();
            onRetreat();
        };
        acts.AddChild(UiKit.Cut(pull));
    }

    /// <summary>One of the three things the order decides.</summary>
    private static Control Consequence(string label, string what, string clause)
    {
        PanelContainer card = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Ink));

        VBoxContainer column = UiKit.Padded(card, 15, 13);
        column.AddThemeConstantOverride("separation", 3);
        column.AddChild(UiKit.Body(string.Join(" ", label.ToCharArray()), UiKit.NightMuted, UiKit.NoteSize));
        column.AddChild(UiKit.OnNight(what, UiKit.PaperInk, UiKit.HeadSize, display: true));
        column.AddChild(UiKit.Body(clause, UiKit.NightMuted, UiKit.NoteSize));
        return card;
    }

    private void CloseConfirm()
    {
        if (_confirm is not null)
        {
            _confirm.QueueFree();
            _confirm = null;
        }
    }

    /// <summary>Called every frame.</summary>
    public void Refresh(Battle battle)
    {
        ArgumentNullException.ThrowIfNull(battle);

        IReadOnlyList<CombatantSnapshot> snapshots = battle.Snapshots();

        foreach (CombatantSnapshot snapshot in snapshots)
        {
            if (_panels.TryGetValue(snapshot.Id, out WarriorPanel? panel))
            {
                panel.Refresh(snapshot);
            }
        }

        RetreatPrompt prompt = HudModel.DescribeRetreat(snapshots, battle.ContactMade);
        _retreat.Text = prompt.Text;
        _retreat.Disabled = !prompt.Enabled;

        _order.Text = prompt.Locked
            ? "some of them are mid-strike; they leave when it finishes"
            : prompt.Shut
                ? "nothing to pull out of yet — the sides have not met"
                : string.Empty;

        ShowRefusalIfAny(battle);
        TellWhatHappened(battle);

        _place.Text = HudModel.DescribeStatus(_seed, battle.ElapsedSeconds, battle.Result?.Outcome);

        if (battle.Result is not null)
        {
            _notice.Text = "the field is clear — the way back is at the foot of the screen";
            _notice.AddThemeColorOverride("font_color", UiKit.NightMuted);
        }
    }

    /// <summary>Adds whatever the fight has said since the last frame, and drops the oldest lines.</summary>
    private void TellWhatHappened(Battle battle)
    {
        IReadOnlyList<BattleEvent> events = battle.Events;

        for (; _eventsTold < events.Count; _eventsTold++)
        {
            if (FightLog.Tell(events[_eventsTold], _names, _ours) is not FightLine line)
            {
                continue;
            }

            Color colour = line.Voice switch
            {
                FightVoice.Ours => UiKit.BrickLit,
                FightVoice.Quiet => UiKit.NightMuted,
                _ => UiKit.PaperInk,
            };

            _stream.AddChild(UiKit.Body(line.Text, colour, UiKit.NoteSize));

            while (_stream.GetChildCount() > Told)
            {
                Node oldest = _stream.GetChild(0);
                _stream.RemoveChild(oldest);
                oldest.QueueFree();
            }
        }
    }

    /// <summary>
    /// Answers a key pressed before the fight starts.
    /// </summary>
    /// <remarks>
    /// The core does not produce the text: it gives the number of refused presses, and what is written
    /// is decided by <see cref="HudModel.DescribeRefusal"/>.
    /// </remarks>
    private void ShowRefusalIfAny(Battle battle)
    {
        if (battle.RefusedRetreatPresses <= _refusalsSeen)
        {
            return;
        }

        _refusalsSeen = battle.RefusedRetreatPresses;

        RetreatRefusalNotice notice = HudModel.DescribeRefusal(_refusalsSeen, _teachingShown);
        _order.Text = notice.Text;

        if (notice.Kind == RetreatNoticeKind.Teaching)
        {
            _teachingShown++;
        }
    }

    /// <summary>
    /// A single man on the field: his name, what is left of him, and what he is doing about it.
    /// </summary>
    /// <remarks>
    /// Indigo is yours and ochre is theirs, and the figure is printed beside the pips so a losing fight
    /// can be read without counting anything. The figure turns brick when a quarter of him is left —
    /// that is the interface's red, and the blood on the ground is the other one.
    /// </remarks>
    private sealed class WarriorPanel
    {
        private readonly Label _name;
        private readonly Label _figure;
        private readonly Label _state;
        private readonly HBoxContainer _pips;
        private readonly Color _colour;
        private readonly string _warriorName;

        public WarriorPanel(string name, bool ours)
        {
            _warriorName = name;
            _colour = ours ? new Color(0.290f, 0.388f, 0.565f) : UiKit.Heading;

            PanelContainer panel = new();
            panel.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(new Color(UiKit.Ground, 0.86f)));

            VBoxContainer column = UiKit.Padded(panel, 16, 12);
            column.AddThemeConstantOverride("separation", 5);

            HBoxContainer heading = new();
            heading.AddThemeConstantOverride("separation", 8);
            column.AddChild(heading);

            _name = UiKit.OnNight(name, UiKit.PaperInk, UiKit.HeadSize + 4, display: true);
            _name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _name.ClipText = true;
            heading.AddChild(_name);

            _figure = UiKit.OnNight(string.Empty, UiKit.PaperInk, UiKit.HeadSize + 2, display: true);
            _figure.HorizontalAlignment = HorizontalAlignment.Right;
            heading.AddChild(_figure);

            _pips = new HBoxContainer();
            _pips.AddThemeConstantOverride("separation", 3);
            column.AddChild(_pips);

            for (int i = 0; i < Pips; i++)
            {
                _pips.AddChild(new ColorRect
                {
                    Color = _colour,
                    CustomMinimumSize = new Vector2(0, 20),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                });
            }

            _state = UiKit.Body(string.Empty, UiKit.NightMuted, UiKit.NoteSize, wrap: false);
            column.AddChild(_state);

            Root = panel;
        }

        public Control Root { get; }

        public void Refresh(in CombatantSnapshot snapshot)
        {
            // Rounded up, so a man on his last sliver reads 1 and not 0: a living warrior printed as
            // dead is the one lie the interface must never tell.
            double health = Math.Max(snapshot.Health, 0);
            _figure.Text = $"{Math.Ceiling(health):0} / {snapshot.MaxHealth:0}";

            bool spent = snapshot.HealthFraction <= 0.25;
            _figure.AddThemeColorOverride("font_color", spent ? UiKit.BrickLit : UiKit.PaperInk);

            int lit = (int)Math.Ceiling(Math.Clamp(snapshot.HealthFraction, 0, 1) * Pips);

            for (int i = 0; i < _pips.GetChildCount(); i++)
            {
                if (_pips.GetChild(i) is ColorRect pip)
                {
                    pip.Color = i < lit
                        ? (spent ? UiKit.Brick : _colour)
                        : new Color(0.169f, 0.141f, 0.110f);
                }
            }

            _name.Text = _warriorName;
            _state.Text = HudModel.DescribeState(snapshot);
            _state.AddThemeColorOverride("font_color", spent ? UiKit.BrickLit : UiKit.NightMuted);
        }
    }
}
