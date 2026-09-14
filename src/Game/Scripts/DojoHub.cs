using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>The screens the hub can open.</summary>
public enum DojoTab
{
    /// <summary>The day's offer, the contract and the party.</summary>
    Day,

    /// <summary>Kadro.</summary>
    Roster,

    /// <summary>The slave market.</summary>
    Market,

    /// <summary>The school tree.</summary>
    School,

    /// <summary>The quartermaster's counter — armour, repairs, the forge and the throwing slot.</summary>
    Armoury,

    /// <summary>The province board — a picture of the season, with nothing to press.</summary>
    Province,
}

/// <summary>
/// The root that navigates the dojo's four screens over a single <see cref="DojoState"/>.
/// </summary>
/// <remarks>
/// <para>
/// The dojo is built <b>once</b> and all four screens see the same object: a warrior bought at the
/// market must appear on the roster screen, and a facility bought at the school in the day's books,
/// immediately. If every screen carried its own copy, the day loop would drift apart in four places.
/// </para>
/// <para>
/// When the screen changes the new one is built from scratch and the old one deleted: because every
/// screen reads on opening (a screen hidden and shown again would print the old day), this is the
/// cheapest correct behaviour.
/// </para>
/// <para>
/// When an expedition goes out, <see cref="BattleArena"/> takes the screens' place: the fight is
/// watched, when it ends the expedition layer closes the books and the day screen comes back with the
/// report. The arena knows nothing of the dojo — it takes a prepared fight and gives a raw result.
/// veriyor.
/// </para>
/// <para>
/// The dojo comes from <see cref="TitleScreen"/>: it is either loaded from <see cref="SaveSlot"/> or
/// <see cref="NewGame.Create"/> builds a new expedition. None of the screens knows which; they all
/// take a ready dojo.
/// </para>
/// <para>
/// <b>Only the hub writes the save</b>: the screens say they changed the dojo with
/// <see cref="DojoScreen.Changed"/>, and the only place that knows the file is here. Writing happens
/// on every change — a 60-day expedition is not played in one session and the game can be closed in
/// the middle of a day.
/// </para>
/// </remarks>
public sealed partial class DojoHub : Node
{
    /// <summary>The clock's hold while a fight is being watched.</summary>
    private const string ArenaHold = "arena";

    /// <summary>The clock's hold while the window is not the one being looked at.</summary>
    private const string FocusHold = "focus";

    /// <summary>The speed control, in the order it is printed.</summary>
    private static readonly (string Text, ClockSpeed Speed)[] Speeds =
    [
        ("||", ClockSpeed.Paused),
        ("1x", ClockSpeed.Normal),
        ("2x", ClockSpeed.Fast),
        ("4x", ClockSpeed.Fastest),
    ];

    private DojoState? _dojo;
    private TitleScreen? _title;
    private DojoScreen? _screen;
    private Node2D? _arena;
    private CanvasLayer? _arenaChrome;
    private DojoTab _tab = DojoTab.Day;
    private string? _report;

    /// <summary>
    /// The season's clock. It belongs to the hub because the hub is the only node that outlives a
    /// screen change: a clock owned by a screen would restart every time the player looked at the
    /// market (build step 8).
    /// </summary>
    private readonly DayClock _clock = new();

    private Label? _clockLabel;
    private ProgressBar? _clockBar;

    public override void _Ready()
    {
        ShowTitle(null);
    }

    /// <summary>
    /// Turns real time into the dojo's days.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole of build step 8 lands here: the frame's delta goes into <see cref="DayClock"/>, and
    /// every day it hands back is closed by the core's own <see cref="DojoState.Decline"/>. Nothing
    /// about a day changes — what changes is that the player no longer presses a button for it.
    /// </para>
    /// <para>
    /// The flow stops for what the player has to answer (<see cref="DayInterrupt"/>) and the rest of
    /// the rollovers in that frame are dropped: a happening must not be run past by the two days that
    /// were queued behind it. The save is written on every day that turns, because a season is now
    /// spent by sitting still and the game can be closed at any second of it.
    /// </para>
    /// </remarks>
    public override void _Process(double delta)
    {
        UpdateClockBar();

        // Neither the last night nor the closing screen has a day to spend: the season's clock stops
        // being a clock the moment the run leaves its running phase.
        if (_dojo is not DojoState dojo || _arena is not null || dojo.Season.Phase != SeasonPhase.Running)
        {
            return;
        }

        int rollovers = _clock.Advance(delta);
        if (rollovers == 0)
        {
            return;
        }

        List<string> log = [];
        bool phaseChanged = false;

        for (int i = 0; i < rollovers; i++)
        {
            DayReport report;
            try
            {
                report = dojo.Decline();
            }
            catch (Exception broken)
            {
                // A day that throws would otherwise throw again on the next frame, for ever. The clock
                // stops, the fault is written into the journal beside the moves that led to it, and the
                // file is flushed at once — the next thing to happen may be the process dying.
                dojo.RecordFault("day", broken);
                Save();
                _clock.Pause();
                log.Add("The day could not be closed; the clock stopped. It is in the journal.");
                break;
            }

            log.Add(DayLog.Line(report));

            if (report.Phase != SeasonPhase.Running)
            {
                phaseChanged = true;
            }

            if (DayInterrupt.Demands(report))
            {
                // Said out loud: the player has to know the flow stopped on purpose rather than
                // wonder whether he paused it himself somewhere.
                _clock.Pause();
                log.Add("The clock stopped here.");
                break;
            }
        }

        Save();

        if (phaseChanged)
        {
            // The last night and the closing screen take the hub over; the days that turned into them
            // are printed on the screen that opens.
            _report = string.Join(System.Environment.NewLine, log);
            Show(_tab);
            return;
        }

        _screen?.Refresh();
        (_screen as DayScreen)?.Note(string.Join(System.Environment.NewLine, log));
    }

    /// <summary>
    /// Writes the final state as the game closes.
    /// </summary>
    /// <remarks>
    /// It is already written on every change; this is a last pass in case anything was left over.
    /// The close cannot be relied on (a crash, a power cut) — which is why it is not the only write point.
    /// </remarks>
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            Save();
        }

        // A window the player has clicked away from does not spend his season. It is a hold rather
        // than a pause, so he comes back to the speed he left running (build step 8).
        if (what == NotificationApplicationFocusOut)
        {
            _clock.Hold(FocusHold);
        }

        if (what == NotificationApplicationFocusIn)
        {
            _clock.Release(FocusHold);
        }
    }

    /// <summary>Shows the title screen; an expedition starts or is loaded from here.</summary>
    private void ShowTitle(string? warning)
    {
        CloseArena();
        CloseScreen();

        TitleScreen title = new() { Warning = warning };
        title.Continued += Continue;
        title.Started += Start;

        _title = title;
        AddChild(title);
    }

    /// <summary>Loads the saved expedition; if it cannot be loaded the title screen stays and the reason is written.</summary>
    private void Continue()
    {
        LoadResult result = SaveSlot.Load();
        if (result.State is not DojoState dojo)
        {
            ShowTitle(string.Join('\n', result.Warnings));
            return;
        }

        // Merge-on-load must not stay silent: a save that loaded incompletely says so in the day
        // screen's report, and the player sees what he lost (GDD §2).
        _report = result.Warnings.Count == 0 ? null : string.Join('\n', result.Warnings);

        // And it goes into the journal as a fault: what a save could not carry is the kind of thing
        // that shows up as a strange run three days later, and by then nothing remembers why.
        if (result.Warnings.Count > 0)
        {
            dojo.RecordFault("save", "the save loaded incompletely", result.Warnings);
        }

        Play(dojo);
    }

    /// <summary>Sets up a new expedition and writes the first day immediately.</summary>
    /// <remarks>
    /// The seed is random: the same seed gives the same offers, so every expedition takes its own seed.
    /// The seed itself goes into the save (GDD §2), so when the expedition is loaded again the same days
    /// come back.
    /// </remarks>
    private void Start()
    {
        SaveSlot.Delete();
        _report = null;
        Play(NewGame.Create(unchecked((ulong)Random.Shared.NextInt64())));
        Save();
    }

    private void Play(DojoState dojo)
    {
        _dojo = dojo;

        // Where a fault may leave a file of its own. The core does not know Godot's user:// and must
        // not learn it, so the path is resolved here and handed in: a fight that hits the stall guard
        // writes its blow-by-blow stream next to the save and names the file on its fault line.
        dojo.DiagnosticsFolder = ProjectSettings.GlobalizePath("user://diagnostics");
        _tab = DojoTab.Day;

        if (_title is not null)
        {
            RemoveChild(_title);
            _title.QueueFree();
            _title = null;
        }

        Show(_tab);
    }

    /// <summary>Writes the dojo as it now stands into the slot.</summary>
    private void Save()
    {
        if (_dojo is DojoState dojo)
        {
            SaveSlot.Write(dojo);
        }
    }

    private void Show(DojoTab tab)
    {
        if (_dojo is not DojoState dojo)
        {
            return;
        }

        _tab = tab;
        CloseArena();
        CloseScreen();

        // The season decides which screen this is. Once the run is over the four tabs are meaningless —
        // there is no day to close, no market to buy from — so the hub stops offering them rather than
        // leaving the player to work out that the buttons do nothing.
        if (dojo.Season.Phase == SeasonPhase.FinalNight)
        {
            ShowNight(dojo);
            return;
        }

        if (dojo.Season.IsOver)
        {
            ShowEnd(dojo);
            return;
        }

        DojoScreen screen = tab switch
        {
            DojoTab.Roster => new RosterScreen(),
            DojoTab.Market => new MarketScreen(),
            DojoTab.School => new SchoolScreen(),
            DojoTab.Armoury => new ArmouryScreen(),
            DojoTab.Province => new ProvinceScreen(),
            _ => new DayScreen(),
        };

        screen.Clock = _clock;

        if (screen is DayScreen day)
        {
            day.Watcher = Fight;
            day.Report = _report;
            _report = null;
        }

        screen.Chrome = BuildNav();
        screen.Changed = Save;
        _screen = screen;
        AddChild(screen);
        screen.Build(dojo);
    }

    /// <summary>The last night takes the hub over: one bout at a time until the run ends.</summary>
    private void ShowNight(DojoState dojo)
    {
        FinalNightScreen night = new()
        {
            Watcher = Fight,
            Report = _report,

            // The night has no navigation: there is nowhere else to go until it is decided.
            Changed = Save,
            Ended = () => Show(_tab),
        };

        _report = null;
        _screen = night;
        AddChild(night);
        night.Build(dojo);
    }

    /// <summary>The closing screen. The only way on from here is a new season.</summary>
    private void ShowEnd(DojoState dojo)
    {
        SeasonEndScreen end = new()
        {
            Closed = () =>
            {
                // The finished run is not carried back into the title screen's "continue": the save is
                // cleared here, so the next start is a new season rather than a dead one reopened.
                SaveSlot.Delete();
                _dojo = null;
                ShowTitle(null);
            },
        };

        _screen = end;
        AddChild(end);
        end.Build(dojo);
    }

    /// <summary>The pause key. Speed is chosen with the bar's own buttons; this is the one shortcut.</summary>
    /// <remarks>
    /// It is bound to the space bar because that is what a pausable-real-time game is expected to be
    /// paused with, and because the clock has to be stoppable from any of the screens without going
    /// back to the bar (docs/REFERENCE-DOMINA.md: every guide for the reference game begins with
    /// "pause as soon as you can").
    /// </remarks>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_dojo is null || _arena is not null)
        {
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
        {
            _clock.Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    private void CloseScreen()
    {
        if (_screen is not null)
        {
            RemoveChild(_screen);
            _screen.QueueFree();
            _screen = null;
        }
    }

    /// <summary>
    /// Plays the prepared fight in the arena.
    /// </summary>
    /// <remarks>
    /// The screens close: the arena is a full-screen scene and the dojo interface must not sit on top of
    /// it. When the fight ends the books are closed by <see cref="PendingBattle.Settle"/> (the expedition
    /// layer) and the arena only gives the raw result — the accounting is not split in two.
    /// </remarks>
    private bool Fight(PendingBattle bout)
    {
        ArgumentNullException.ThrowIfNull(bout);

        CloseScreen();

        BattleArena arena = new()
        {
            Bout = bout.Setup,
            Seed = unchecked((long)bout.Seed),
        };

        // When the result arrives the books close at once but the screen does not change: the player
        // must not be thrown back into the dojo before seeing the fight's last frame, he returns with his own key.
        arena.Finished += result =>
        {
            try
            {
                _report = bout.Settle(result);
            }
            catch (Exception broken) when (_dojo is DojoState hurt)
            {
                // The fight is over and its result is in hand; what failed is the accounting. The run
                // is left standing and the failure is filed with the fight's own seed beside it.
                hurt.RecordFault("expedition", broken);
                _report = "The expedition's books could not be closed. It is in the journal.";
            }

            Save();
            ShowReturnButton();
        };

        // The dojo's day does not run underneath a fight: the expedition already paid for it, and a
        // player watching the arena cannot answer anything the morning would bring (build step 8).
        _clock.Hold(ArenaHold);

        _arena = arena;
        AddChild(arena);
        return true;
    }

    /// <summary>The single button that appears when the fight ends: back to the dojo.</summary>
    private void ShowReturnButton()
    {
        CanvasLayer chrome = new();
        MarginContainer margin = new() { AnchorRight = 1, AnchorBottom = 1 };
        margin.AddThemeConstantOverride("margin_bottom", 32);
        chrome.AddChild(margin);

        Button back = new()
        {
            Text = "Return to the dojo",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
        };
        back.Pressed += () => Show(DojoTab.Day);
        margin.AddChild(back);

        _arenaChrome = chrome;
        AddChild(chrome);
    }

    private void CloseArena()
    {
        _clock.Release(ArenaHold);
        _clock.Restart();

        if (_arenaChrome is not null)
        {
            RemoveChild(_arenaChrome);
            _arenaChrome.QueueFree();
            _arenaChrome = null;
        }

        if (_arena is not null)
        {
            RemoveChild(_arena);
            _arena.QueueFree();
            _arena = null;
        }
    }

    /// <summary>
    /// The navigation bar, with the season's clock on the end of it.
    /// </summary>
    /// <remarks>
    /// The clock sits in the chrome rather than on a layer of its own for the reason the tabs do
    /// (<see cref="DojoScreen"/>): a bar laid over the screens would cover the top row of every one of
    /// them. It is rebuilt with the bar on every screen change, so the labels are re-found each time
    /// rather than kept across a screen that has been freed.
    /// </remarks>
    private Control BuildNav()
    {
        // The bar is a panel of its own rather than two loose rows: it is the only thing on screen that
        // does not change when the screen does, so it has to read as the frame around them.
        PanelContainer frame = new();
        frame.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(UiKit.Surface));

        VBoxContainer column = UiKit.Padded(frame, 12, 8);
        column.AddThemeConstantOverride("separation", 8);

        HBoxContainer nav = new();
        nav.AddThemeConstantOverride("separation", 6);
        column.AddChild(nav);

        foreach (DojoTab tab in Enum.GetValues<DojoTab>())
        {
            bool current = tab == _tab;
            Button button = UiKit.Tab(
                new Button
                {
                    Text = TabName(tab),
                    ToggleMode = true,
                    ButtonPressed = current,
                },
                current);

            // The open tab is lit rather than disabled, so it has to refuse its own press by hand:
            // showing the screen again would rebuild it and throw away whatever row was selected on it.
            DojoTab target = tab;
            button.Pressed += () =>
            {
                if (target != _tab)
                {
                    Show(target);
                }
            };

            nav.AddChild(button);
        }

        column.AddChild(UiKit.Rule());
        column.AddChild(BuildClockBar());
        return frame;
    }

    /// <summary>The clock's own row: what day it is, how far into it, and the speed control.</summary>
    private Control BuildClockBar()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 8);

        row.AddChild(UiKit.Emblem(Mark.Sun, UiKit.Heading, UiKit.BodySize - 3));

        _clockLabel = new Label();
        _clockLabel.AddThemeFontSizeOverride("font_size", UiKit.BodySize);
        _clockLabel.AddThemeColorOverride("font_color", UiKit.Ink);
        row.AddChild(_clockLabel);

        _clockBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.001,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(200, 10),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(_clockBar);

        foreach ((string text, ClockSpeed speed) in Speeds)
        {
            Button button = new() { Text = text };
            ClockSpeed chosen = speed;
            button.Pressed += () => _clock.Set(chosen);
            row.AddChild(button);
        }

        UpdateClockBar();
        return row;
    }

    /// <summary>Reprints the clock's row. Called every frame, so it touches nothing it need not.</summary>
    private void UpdateClockBar()
    {
        if (_clockLabel is null || !IsInstanceValid(_clockLabel) || _dojo is not DojoState dojo)
        {
            return;
        }

        int left = Math.Max(0, dojo.Season.Tuning.Days - dojo.Day + 1);
        string state = _clock.IsHeld
            ? "held"
            : _clock.Speed switch
            {
                ClockSpeed.Normal => "1x",
                ClockSpeed.Fast => "2x",
                ClockSpeed.Fastest => "4x",
                _ => "paused",
            };

        _clockLabel.Text = $"Day {dojo.Day}  ·  {left} days left  ·  {state}";

        if (_clockBar is not null && IsInstanceValid(_clockBar))
        {
            _clockBar.Value = _clock.Progress;
        }
    }

    private static string TabName(DojoTab tab) => tab switch
    {
        DojoTab.Roster => "Roster",
        DojoTab.Market => "Market",
        DojoTab.School => "School",
        DojoTab.Armoury => "Armoury",
        DojoTab.Province => "Province",
        _ => "Day",
    };
}
