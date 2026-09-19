using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>A place in the yard the player can walk to.</summary>
/// <remarks>
/// There is no list of screens any more — there is a yard with things standing in it, and each of
/// these is one of those things. Nothing is in this enum that the player cannot see from where he
/// stands (design canvas → 7c, "there is one hub, and it is a place").
/// </remarks>
public enum YardPlace
{
    /// <summary>The board by the gate: the day's work, and who walks it.</summary>
    Board,

    /// <summary>The rack against the wall: what a man carries.</summary>
    Rack,

    /// <summary>The post in the middle of the ground: a day spent not earning.</summary>
    Post,

    /// <summary>The cart at the wall: what can be bought while it is here.</summary>
    Cart,

    /// <summary>The men themselves, standing where they stand.</summary>
    Men,

    /// <summary>The gate: out of the yard, into the province.</summary>
    Gate,

    /// <summary>The hut with the lit window: the infirmary, and the tribunal when one is called.</summary>
    Hut,
}

/// <summary>
/// One thing standing in the yard: a few flat shapes, a name it writes in chalk, and one clause.
/// </summary>
/// <remarks>
/// <para>
/// The object is its own destination. Under the cursor it lifts one step in value — the paper
/// brightens, the steel catches the light — and the name is chalked on the ground beside its own post.
/// Nothing floats, nothing is framed, and the cursor gets no card of its own (design canvas → 7b, 8a).
/// </para>
/// <para>
/// Every figure here is a flat cut-paper stand-in for real art. They are drawn to be read at a glance
/// for staging and for what they are, not to be shipped.
/// </para>
/// </remarks>
public sealed partial class YardObject : Control
{
    private bool _lit;
    private bool _alwaysNamed;

    /// <summary>The shapes the thing is cut from, in the object's own coordinates.</summary>
    public required IReadOnlyList<(Color Fill, Vector2[] Points)> Shapes { get; init; }

    /// <summary>What the thing calls itself — "the rack", "the board". Lower case: it is a thing, not a screen.</summary>
    public required string ChalkName { get; init; }

    /// <summary>The single thing it does. One clause, never two.</summary>
    public required string Clause { get; init; }

    /// <summary>Where it goes.</summary>
    public required YardPlace Place { get; init; }

    /// <summary>Called when the player walks to it.</summary>
    public Action<YardPlace>? Walked { get; set; }

    /// <summary>Whether the chalk name is showing — under the cursor, or kept on from settings.</summary>
    public bool Named => _lit || AlwaysNamed;

    /// <summary>
    /// Settings → <i>name the destinations</i>: every chalk name showing at all times, for players who
    /// would rather read the yard than learn it.
    /// </summary>
    public bool AlwaysNamed
    {
        get => _alwaysNamed;
        set
        {
            _alwaysNamed = value;
            UpdateLabel();
        }
    }

    /// <summary>The chalk beside it; the yard owns it and this only shows and hides it.</summary>
    public Control? Label { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += () => Light(true);
        MouseExited += () => Light(false);
        GuiInput += OnInput;
        UpdateLabel();
    }

    public override void _Draw()
    {
        foreach ((Color fill, Vector2[] points) in Shapes)
        {
            DrawColoredPolygon(points, _lit ? fill.Lightened(0.16f) : fill);
        }
    }

    private void OnInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Walked?.Invoke(Place);
            AcceptEvent();
        }
    }

    private void Light(bool lit)
    {
        _lit = lit;
        UpdateLabel();
        QueueRedraw();
    }

    private void UpdateLabel()
    {
        if (Label is not null && IsInstanceValid(Label))
        {
            Label.Visible = Named;
        }
    }
}

/// <summary>
/// The dojo yard: the hub, and the only screen that is not opened over something else.
/// </summary>
/// <remarks>
/// <para>
/// Everything the player can do during a day is a thing standing here. A sheet opens <b>over</b> the
/// yard and closes back onto it; the only two places that take the whole stage are the two the player
/// walks to — out through the gate, and onto the ground (design canvas → 7a, 7c).
/// </para>
/// <para>
/// The yard is drawn in flat bands and flat shapes at the design's own 1920×1080, which is the
/// viewport the project is set to; the engine's canvas stretch does the rest. The art is a placeholder
/// in the same sense the canvas's own figures are: the staging is the part meant to survive.
/// </para>
/// </remarks>
public sealed partial class YardScreen : CanvasLayer
{
    /// <summary>How wide the yard is drawn. The viewport's own width — nothing here scales itself.</summary>
    private const float Width = 1920f;

    /// <summary>How tall.</summary>
    private const float Height = 1080f;

    /// <summary>How many notices may lie at the edge of the yard at once.</summary>
    private const int Notices = 3;

    private readonly List<YardObject> _objects = [];

    private VBoxContainer? _notices;
    private VBoxContainer? _spoken;
    private YardMen? _men;

    /// <summary>Called when the player walks to one of the things in the yard.</summary>
    public Action<YardPlace>? Walked { get; set; }

    private bool _alwaysNamed;

    /// <summary>Settings → <i>name the destinations</i>, passed on to every object.</summary>
    public bool AlwaysNamed
    {
        get => _alwaysNamed;
        set
        {
            _alwaysNamed = value;

            foreach (YardObject thing in _objects)
            {
                thing.AlwaysNamed = value;
            }
        }
    }

    /// <summary>Draws the yard and stands everything in it.</summary>
    public void Build()
    {
        Control page = new() { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(page);

        Band(page, 0, 470, Hex(0x2A2B3A));      // the sky
        Moon(page, new Vector2(1578, 136), 58);
        Roofs(page);
        Band(page, 424, 118, Hex(0x1B1713));    // the wall
        Band(page, 542, 538, Hex(0x3A3229));    // the ground
        Band(page, 820, 260, Hex(0x40372C));    // the ground nearest the eye

        Hall(page);
        Brazier(page);

        Stand(page, YardArt.Hut(), new Vector2(44, 352), new Vector2(300, 300),
            YardPlace.Hut, "the hut", "the men on the mats, and whoever is called to stand",
            new Vector2(20, 312));

        // Each name is chalked beside its own post, and the offsets are chosen so that two things
        // standing near each other do not write over one another's names.
        Stand(page, YardArt.Board(), new Vector2(706, 596), new Vector2(216, 268),
            YardPlace.Board, "the board", "take the day's work, and choose who walks it",
            new Vector2(4, 286));

        Stand(page, YardArt.Rack(), new Vector2(1128, 520), new Vector2(258, 290),
            YardPlace.Rack, "the rack", "change what a man carries",
            new Vector2(-14, 308));

        Stand(page, YardArt.Post(), new Vector2(378, 470), new Vector2(176, 300),
            YardPlace.Post, "the post", "give a day to the drill instead of to the road",
            new Vector2(-6, 318));

        Stand(page, YardArt.Cart(), new Vector2(1424, 618), new Vector2(320, 246),
            YardPlace.Cart, "the cart", "buy a man, while the cart is still here",
            new Vector2(-24, 264));

        // The men are the one destination that is not a thing built in the yard: it is the ground they
        // stand on, and the men standing on it are the real figures (see YardMen below).
        Stand(page, YardArt.Ground(), new Vector2(180, 690), new Vector2(600, 240),
            YardPlace.Men, "the men", "read one of them, and see what he has become",
            new Vector2(214, 250));

        Stand(page, YardArt.Gate(), new Vector2(1612, 432), new Vector2(200, 260),
            YardPlace.Gate, "the gate", "walk out, and see how far the province reaches",
            new Vector2(-92, 300));

        // The men stand over the yard's own drawing rather than in it: they are nodes that move, and the
        // ground they are on is a control the cursor can take. A Node2D takes no mouse input, so the
        // destination underneath keeps the click even with a figure drawn on top of it.
        YardMen men = new() { Position = new Vector2(232, 700) };
        AddChild(men);
        _men = men;

        VBoxContainer notices = new() { Position = new Vector2(96, Height - 300) };
        notices.AddThemeConstantOverride("separation", 10);
        notices.MouseFilter = Control.MouseFilterEnum.Ignore;
        page.AddChild(notices);
        _notices = notices;
    }

    /// <summary>
    /// Sets a notice down at the edge of the yard.
    /// </summary>
    /// <remarks>
    /// Three at most are on screen and the oldest goes first. A notice never blocks the yard and never
    /// needs dismissing — it is a slip of paper laid on the ground, and the ledger keeps them all
    /// (design canvas → 7b, "nothing pops; the world reports").
    /// </remarks>
    /// <param name="ofTheSeason">
    /// Whether the thing reported changes the season rather than the day. Those are printed on ink
    /// rather than on paper, so the two are told apart at the edge of the eye without reading either.
    /// </param>
    public void Post(string title, string line, string hour, bool ofTheSeason = false)
    {
        if (_notices is not VBoxContainer column || !IsInstanceValid(column))
        {
            return;
        }

        column.AddChild(UiKit.Notice(title, line, hour, ofTheSeason ? UiKit.Heading : null, ofTheSeason));

        while (column.GetChildCount() > Notices)
        {
            Node oldest = column.GetChild(0);
            column.RemoveChild(oldest);
            oldest.QueueFree();
        }
    }

    /// <summary>
    /// The first term's opening: three things in the yard speak once, in order, and never again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The board, the rack and the post — the three that decide whether a first term survives its own
    /// first week. The rest of the yard (the cart, the hut, the hall, the gate) is never introduced; it
    /// is found (design canvas → 8b).
    /// </para>
    /// <para>
    /// The clock does not run while they speak: the hub holds it, and lets it go when the player has
    /// said he knows the yard or has heard all three.
    /// </para>
    /// </remarks>
    public void Introduce(Action done)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (_spoken is not null || _objects.Count == 0)
        {
            return;
        }

        (YardPlace Place, string Says)[] three =
        [
            (YardPlace.Board,
                "Work is posted here each morning. Nothing else in this yard earns you anything."),
            (YardPlace.Rack,
                "What a man carries is most of what he is worth on a road."),
            (YardPlace.Post,
                "A day given to the post is a day not earning. It is also how a man stops dying."),
        ];

        int at = 0;
        VBoxContainer said = new()
        {
            Position = new Vector2(96, Height - 220),
            CustomMinimumSize = new Vector2(720, 0),
        };
        said.AddThemeConstantOverride("separation", 7);
        GetChild(0).AddChild(said);
        _spoken = said;

        void Show()
        {
            foreach (Node child in said.GetChildren())
            {
                said.RemoveChild(child);
                child.QueueFree();
            }

            if (at >= three.Length)
            {
                said.QueueFree();
                _spoken = null;
                done();
                return;
            }

            (YardPlace place, string says) = three[at];
            YardObject? thing = _objects.Find(one => one.Place == place);

            PanelContainer panel = new();
            panel.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(new Color(UiKit.Ground, 0.88f)));
            said.AddChild(panel);

            VBoxContainer column = UiKit.Padded(panel, 22, 18);
            column.AddThemeConstantOverride("separation", 7);

            HBoxContainer head = new();
            head.AddThemeConstantOverride("separation", 12);
            column.AddChild(head);

            Label counted = UiKit.OnNight(
                at switch { 0 => "一", 1 => "二", _ => "三" },
                UiKit.PaperInk,
                UiKit.HeadSize,
                display: true);
            head.AddChild(counted);
            head.AddChild(UiKit.OnNight(thing?.ChalkName ?? "the yard", UiKit.PaperInk, UiKit.TitleSize, display: true));

            column.AddChild(UiKit.Body(says, UiKit.PaperInk, UiKit.NoteSize + 2));

            HBoxContainer acts = new();
            acts.AddThemeConstantOverride("separation", 14);
            column.AddChild(acts);

            Button on = new() { Text = "Go on" };
            on.Pressed += () =>
            {
                at++;
                Show();
            };
            acts.AddChild(UiKit.Act(on));

            Button known = new() { Text = "I know the yard" };
            known.Pressed += () =>
            {
                at = three.Length;
                Show();
            };
            acts.AddChild(UiKit.WayOut(known));

            acts.AddChild(UiKit.Body(
                $"{(at + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)} of "
                + $"{three.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} — either way "
                + "this never happens again in this term.",
                UiKit.NightMuted,
                UiKit.NoteSize));

            if (thing is not null)
            {
                thing.AlwaysNamed = true;
            }
        }

        Show();
    }

    /// <summary>
    /// Stands the roster on the ground of the yard.
    /// </summary>
    /// <remarks>
    /// Called whenever the roster can have changed — a day closed, a man bought, a sheet shut — and not
    /// per frame: the figures are nodes, and only the pose moves between two calls of this.
    /// </remarks>
    public void StandMen(IReadOnlyList<RosterRow> roster)
    {
        if (_men is YardMen men && IsInstanceValid(men))
        {
            men.Stand(roster);
        }
    }

    /// <summary>Takes every notice off the ground — a new day, or a term that has ended.</summary>
    public void ClearNotices()
    {
        if (_notices is not VBoxContainer column || !IsInstanceValid(column))
        {
            return;
        }

        foreach (Node notice in column.GetChildren())
        {
            column.RemoveChild(notice);
            notice.QueueFree();
        }
    }

    /// <summary>A flat band across the whole width — the sky, the wall, the ground.</summary>
    private static void Band(Control page, float top, float height, Color colour)
    {
        page.AddChild(new ColorRect
        {
            Color = colour,
            Position = new Vector2(0, top),
            Size = new Vector2(Width, height),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    private static void Moon(Control page, Vector2 centre, float radius)
    {
        Control moon = new()
        {
            Position = centre - new Vector2(radius, radius),
            Size = new Vector2(radius * 2, radius * 2),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        moon.Draw += () => moon.DrawCircle(new Vector2(radius, radius), radius, new Color(Hex(0xD9CFB4), 0.85f));
        page.AddChild(moon);
    }

    /// <summary>The roofs of the town behind the wall: one broken line across the sky.</summary>
    private static void Roofs(Control page) =>
        Cut(page, YardArt.Roofs(), new Vector2(0, 232), new Vector2(Width, 240));

    /// <summary>The hall at the back of the yard. Not a destination — the yard needs a back wall.</summary>
    private static void Hall(Control page) =>
        Cut(page, YardArt.Hall(), new Vector2(812, 300), new Vector2(300, 242));

    /// <summary>The one warm thing in the yard, and the only light that is not the moon.</summary>
    private static void Brazier(Control page)
    {
        Control fire = new()
        {
            Position = new Vector2(372, 386),
            Size = new Vector2(26, 40),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        fire.Draw += () =>
        {
            fire.DrawCircle(new Vector2(13, 20), 34, new Color(Hex(0xB98F4A), 0.16f));
            fire.DrawRect(new Rect2(0, 0, 26, 40), Hex(0xB98F4A));
        };

        page.AddChild(fire);
    }

    /// <summary>Draws a set of shapes at a place in the yard, with nothing to press.</summary>
    private static void Cut(Control page, IReadOnlyList<(Color Fill, Vector2[] Points)> shapes, Vector2 at, Vector2 size)
    {
        Control piece = new() { Position = at, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
        piece.Draw += () =>
        {
            foreach ((Color fill, Vector2[] points) in shapes)
            {
                piece.DrawColoredPolygon(points, fill);
            }
        };

        page.AddChild(piece);
    }

    /// <summary>Stands a destination in the yard, with its chalk name on the ground beside it.</summary>
    private void Stand(
        Control page,
        IReadOnlyList<(Color Fill, Vector2[] Points)> shapes,
        Vector2 at,
        Vector2 size,
        YardPlace place,
        string name,
        string clause,
        Vector2 chalkAt)
    {
        Control chalk = UiKit.Chalk(name, clause);
        chalk.Position = at + chalkAt;
        chalk.Visible = false;
        chalk.MouseFilter = Control.MouseFilterEnum.Ignore;
        page.AddChild(chalk);

        YardObject thing = new()
        {
            Shapes = shapes,
            ChalkName = name,
            Clause = clause,
            Place = place,
            Position = at,
            Size = size,
            Label = chalk,
            AlwaysNamed = AlwaysNamed,
            Walked = walked => Walked?.Invoke(walked),
        };

        page.AddChild(thing);
        _objects.Add(thing);
    }

    private static Color Hex(uint rgb) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
}
