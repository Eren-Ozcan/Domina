using Domina.Chat;
using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>The screens the hub can open.</summary>
public enum DojoTab
{
    /// <summary>The day's offer, the contract and the party.</summary>
    Day,

    /// <summary>The roster.</summary>
    Roster,

    /// <summary>The slave market.</summary>
    Market,

    /// <summary>The school tree.</summary>
    School,

    /// <summary>The quartermaster's counter — armour, repairs, the forge and the throwing slot.</summary>
    Armoury,

    /// <summary>The province board — a picture of the season, with nothing to press.</summary>
    Province,

    /// <summary>The hut: the infirmary, and the tribunal on the night a man is called.</summary>
    Hut,
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

    /// <summary>The clock's hold while the yard is introducing itself on the first day of a term.</summary>
    private const string IntroductionHold = "introduction";

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
    private NewTermScreen? _opening;
    private SavesScreen? _saves;
    private GateScreen? _gate;

    /// <summary>
    /// The viewers asking to be let into the yard.
    /// </summary>
    /// <remarks>
    /// It lives for the term, like the clock: one man per viewer per term is the rule, and a gate
    /// rebuilt on a screen change would hand the same viewer a second man. Nothing calls
    /// <see cref="ViewerGate.Ask"/> yet — the chat transport is the part that is not written — so the
    /// queue stands empty until it is (design canvas → 9b).
    /// </remarks>
    private readonly ViewerGate _viewers = new();
    private PauseScreen? _stopped;
    private AftermathScreen? _aftermath;
    private SettingsScreen? _settings;
    private YardScreen? _yard;
    private CanvasLayer? _strip;
    private Node2D? _arena;
    private CanvasLayer? _arenaChrome;
    private DojoTab _tab = DojoTab.Day;
    private string? _report;

    /// <summary>
    /// The men the last fight went out with; the sheet they walk back into prints them.
    /// </summary>
    /// <remarks>
    /// It is kept beside the report and cleared with it: both belong to one expedition, and a party
    /// left over from yesterday's fight would be printed under today's report as though those were the
    /// men who had just come home.
    /// </remarks>
    private IReadOnlyList<WarriorId> _party = [];

    /// <summary>
    /// The season's clock. It belongs to the hub because the hub is the only node that outlives a
    /// screen change: a clock owned by a screen would restart every time the player looked at the
    /// market (build step 8).
    /// </summary>
    private readonly DayClock _clock = new();

    /// <summary>
    /// The paper the world is printed on: grain, inked edges and the hour's wash.
    /// </summary>
    /// <remarks>
    /// It belongs to the hub for the same reason the clock does — it outlives every screen change, and
    /// one rebuilt per screen would regenerate its noise field each time the player opened the market.
    /// </remarks>
    private PaperOverlay? _paper;

    private HBoxContainer? _stripRow;
    private Control? _hourRun;
    private Label? _hourLabel;

    public override void _Ready()
    {
        GameSettings.Load();
        GameSettings.Apply(GetTree().Root);
        ApplyPaper();
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
        UpdateHour();

        if (_paper is PaperOverlay paper && IsInstanceValid(paper))
        {
            // The wash is the world's way of saying the clock is running, so it reads the same
            // progress the strip's hour does rather than a second clock of its own.
            paper.Hour = _clock.Progress;
        }

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
            ShowYard();
            return;
        }

        _screen?.Refresh();
        RefreshStrip();
        RefreshYardMen();
        ShowGateIfAnyoneIsAsking();
        (_screen as DayScreen)?.Note(string.Join(System.Environment.NewLine, log));

        // The same lines are set down at the edge of the yard, because the player is usually standing
        // in it rather than on the board's sheet when a day turns (design canvas → 7b).
        if (_yard is YardScreen yard && IsInstanceValid(yard))
        {
            string hour = $"day {dojo.Day.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

            foreach (string told in log)
            {
                yard.Post("The day closed", told, hour);
            }
        }
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
        CloseStrip();
        CloseYard();
        CloseStopped();
        CloseSaves();

        if (_opening is not null)
        {
            RemoveChild(_opening);
            _opening.QueueFree();
            _opening = null;
        }

        TitleScreen title = new() { Warning = warning };
        title.Continued = Continue;
        title.Opening = ShowNewTerm;
        title.Settings = ShowSettings;
        title.Keeping = () => ShowSaves(choosingRoom: false);

        _title = title;
        AddChild(title);
    }

    /// <summary>
    /// The sheet of kept terms: which one to go back into, or which one to write a new term over.
    /// </summary>
    private void ShowSaves(bool choosingRoom)
    {
        CloseTitle();
        CloseSaves();

        SavesScreen saves = new() { ChoosingRoom = choosingRoom };

        saves.Closed = () =>
        {
            CloseSaves();
            ShowTitle(null);
        };

        saves.Loaded = slot =>
        {
            SaveSlot.Current = slot;
            CloseSaves();
            Continue();
        };

        saves.Freed = slot =>
        {
            SaveSlot.Current = slot;
            CloseSaves();
            ShowNewTerm();
        };

        _saves = saves;
        AddChild(saves);
    }

    private void CloseSaves()
    {
        if (_saves is not null)
        {
            RemoveChild(_saves);
            _saves.QueueFree();
            _saves = null;
        }
    }

    private void CloseTitle()
    {
        if (_title is not null)
        {
            RemoveChild(_title);
            _title.QueueFree();
            _title = null;
        }
    }

    /// <summary>
    /// The sheet a term is opened on: a name, an instructor, a province and a seed.
    /// </summary>
    /// <remarks>
    /// It takes the title's place rather than opening over it, because the title is not a place in the
    /// world and there is no ground behind it to come back to (design canvas → 6a, 6b).
    /// </remarks>
    private void ShowNewTerm()
    {
        CloseTitle();

        // A new term takes an empty slot without asking. When all of them are kept, the player is sent
        // to choose which one is written over, and that choice is a cut act (design canvas -> 6c).
        if (SaveSlot.FirstEmpty() is int free)
        {
            SaveSlot.Current = free;
        }
        else if (_saves is null)
        {
            ShowSaves(choosingRoom: true);
            return;
        }

        NewTermScreen opening = new()
        {
            Closed = () => ShowTitle(null),
        };

        opening.Opened = (name, instructor, tier, seed) =>
        {
            RemoveChild(opening);
            opening.QueueFree();
            Start(name, instructor, tier, seed);
        };

        _title = null;
        _opening = opening;
        AddChild(opening);
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
    private void Start(string name, string instructor, DifficultyTier tier, ulong seed)
    {
        SaveSlot.Delete();
        _report = null;
        Play(NewGame.Create(seed, tier: tier, dojoName: name, instructor: instructor));
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

        _opening = null;
        BuildYard();
        BuildStrip();
        ShowYard();

        // A term opened on its first day introduces the three things it turns on, once. A term loaded
        // in the middle of itself does not — the player has been standing in this yard for weeks
        // (design canvas → 8b).
        if (dojo.Day == 1 && _yard is YardScreen yard)
        {
            _clock.Hold(IntroductionHold);
            yard.Introduce(() =>
            {
                yard.AlwaysNamed = GameSettings.NameDestinations;
                _clock.Release(IntroductionHold);
            });
        }
    }

    /// <summary>
    /// Stands the yard up. It is built once per term and outlives every sheet opened over it.
    /// </summary>
    /// <remarks>
    /// The yard is the hub and the hub is a place: rebuilding it when a sheet closes would throw away
    /// the one thing on screen the player is meant to be learning by looking at it (design canvas → 7c).
    /// </remarks>
    private void BuildYard()
    {
        CloseYard();

        // The yard is the ground and everything stands on it — including the hour's wash, which is
        // laid on layer 0 so the world takes the colour of the time of day and the sheets over it
        // never do (see PaperOverlay).
        YardScreen yard = new() { Layer = -1, Walked = Walk, AlwaysNamed = GameSettings.NameDestinations };
        _yard = yard;
        AddChild(yard);
        yard.Build();
        RefreshYardMen();
    }

    /// <summary>Walks to one of the things standing in the yard.</summary>
    private void Walk(YardPlace place) => Show(place switch
    {
        YardPlace.Rack => DojoTab.Armoury,
        YardPlace.Post => DojoTab.School,
        YardPlace.Cart => DojoTab.Market,
        YardPlace.Men => DojoTab.Roster,
        YardPlace.Gate => DojoTab.Province,
        YardPlace.Hut => DojoTab.Hut,
        _ => DojoTab.Day,
    });

    /// <summary>
    /// Closes whatever is open and leaves the player standing in the yard.
    /// </summary>
    /// <remarks>
    /// Every sheet ends here, and so does a fight: there is no screen in the game whose way out is
    /// another screen. The season is asked first, because a term that ended while a sheet was open has
    /// no yard left to come back to.
    /// </remarks>
    private void ShowYard()
    {
        CloseArena();
        CloseScreen();
        CloseStopped();
        CloseGate();

        if (_aftermath is not null)
        {
            RemoveChild(_aftermath);
            _aftermath.QueueFree();
            _aftermath = null;
        }

        if (_dojo is not DojoState dojo)
        {
            return;
        }

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

        if (_yard is null)
        {
            BuildYard();
        }

        RefreshStrip();
        RefreshYardMen();
        ShowGateIfAnyoneIsAsking();
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
            DojoTab.Hut => new HutScreen(),
            _ => new DayScreen(),
        };

        screen.Clock = _clock;

        if (screen is DayScreen day)
        {
            day.Watcher = Fight;
            day.Report = _report;
            _report = null;
        }

        screen.Back = ShowYard;
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
            Ended = ShowYard,
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

            // Stopping the world puts the four acts in the corner of the yard; letting it run on takes
            // them away again. There is no third state and no word for either (design canvas → 6e).
            if (_clock.Speed == ClockSpeed.Paused && _screen is null)
            {
                ShowStopped();
            }
            else
            {
                CloseStopped();
            }

            GetViewport().SetInputAsHandled();
        }

        // The way out of a sheet is always the same and it is always back to the ground it opened
        // over — the corner of the paper says so, and the key agrees with the corner. Standing in the
        // yard with nothing over it, the same key stops the world instead: it is the key a player
        // presses looking for the settings, and the four acts are where the settings are.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            if (_screen is not null)
            {
                ShowYard();
            }
            else if (_stopped is null)
            {
                _clock.Set(ClockSpeed.Paused);
                ShowStopped();
            }
            else
            {
                CloseStopped();
                _clock.Set(ClockSpeed.Normal);
            }

            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// The speed the strip was told to run at — and the world's four acts with it.
    /// </summary>
    /// <remarks>
    /// The bar's <c>||</c> and the space bar are the same act and must land in the same place: the
    /// button used to stop only the clock, so a player who never found the key could stop the world
    /// and still never see the four acts — which is the only way into the settings once a term is
    /// running (design canvas → 6e).
    /// </remarks>
    private void Choose(ClockSpeed speed)
    {
        _clock.Set(speed);

        if (speed == ClockSpeed.Paused && _screen is null)
        {
            ShowStopped();
        }
        else
        {
            CloseStopped();
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

        _party = bout.Party ?? [];

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

    /// <summary>
    /// The one act that appears when the fight ends: walk back through the gate.
    /// </summary>
    /// <remarks>
    /// The last frame of the fight is left standing behind it. A player whose man has just been killed
    /// is not thrown into the yard before he has seen it happen — he leaves the field himself.
    /// </remarks>
    private void ShowReturnButton()
    {
        CanvasLayer chrome = new() { Layer = 2 };
        MarginContainer margin = new() { AnchorRight = 1, AnchorBottom = 1 };
        margin.AddThemeConstantOverride("margin_bottom", 54);
        chrome.AddChild(margin);

        Button back = new()
        {
            Text = "Back through the gate",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
        };
        back.Pressed += ShowAftermath;
        margin.AddChild(UiKit.Act(back));

        _arenaChrome = chrome;
        AddChild(chrome);
    }

    /// <summary>
    /// The sheet the party walks back into the yard with: what the work paid and what it cost.
    /// </summary>
    /// <remarks>
    /// The report was a paragraph at the top of the day's screen, which a player reached by pressing
    /// something else first; it is the thing the fight was for, so it opens over the yard by itself
    /// (design canvas → 5d).
    /// </remarks>
    private void ShowAftermath()
    {
        CloseArena();
        CloseScreen();

        if (_report is not string told || told.Length == 0)
        {
            _party = [];
            ShowYard();
            return;
        }

        _report = null;
        IReadOnlyList<RosterRow> returned = Returned();
        _party = [];

        if (_yard is null)
        {
            BuildYard();
        }

        RefreshStrip();

        string[] lines = told.Split(
            System.Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        AftermathScreen aftermath = new()
        {
            Layer = 1,
            Headline = lines.Length > 0 ? lines[0] : "They came back.",
            Lines = lines.Length > 1 ? lines[1..] : [],
            Returned = returned,
        };

        aftermath.Closed = () =>
        {
            RemoveChild(aftermath);
            aftermath.QueueFree();
            _aftermath = null;
            ShowYard();
        };

        _aftermath = aftermath;
        AddChild(aftermath);
    }

    /// <summary>
    /// The party that went out, as the roster has them now the books are closed.
    /// </summary>
    /// <remarks>
    /// The rows are read <b>after</b> the accounting rather than kept from before it: what the sheet
    /// has to print is the wound, the days in the hut and the empty bed, and none of those exist until
    /// the expedition layer has written them. The order the men were ticked in is kept, so the sheet
    /// reads as the party the player put together.
    /// </remarks>
    private IReadOnlyList<RosterRow> Returned()
    {
        if (_dojo is not DojoState dojo || _party.Count == 0)
        {
            return [];
        }

        Dictionary<WarriorId, RosterRow> rows = [];

        foreach (RosterRow row in RosterModel.Describe(dojo))
        {
            rows[row.Id] = row;
        }

        List<RosterRow> party = [];

        foreach (WarriorId id in _party)
        {
            if (rows.TryGetValue(id, out RosterRow row))
            {
                party.Add(row);
            }
        }

        return party;
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
    /// The four acts that appear when the world stops.
    /// </summary>
    /// <remarks>
    /// They are put up rather than opened: the yard is not covered and not dimmed to a sheet's weight,
    /// because the player is still standing in it and is about to carry on standing in it.
    /// </remarks>
    private void ShowStopped()
    {
        if (_stopped is not null || _dojo is not DojoState dojo)
        {
            return;
        }

        PauseScreen stopped = new()
        {
            Dojo = dojo,
            Layer = 1,
            Resumed = () =>
            {
                CloseStopped();
                _clock.Set(ClockSpeed.Normal);
            },
            Wrote = () =>
            {
                Save();
                _yard?.Post("The term was written down", "It is kept in the one slot.", "now");
                CloseStopped();
                _clock.Set(ClockSpeed.Normal);
            },
            Settings = ShowSettings,
            Abandoned = () =>
            {
                // The one act on the stack that cannot be taken back, and the only place the term is
                // thrown away on purpose: the slot is freed and the title takes the yard's place.
                SaveSlot.Delete();
                _dojo = null;
                CloseStopped();
                ShowTitle(null);
            },
        };

        _stopped = stopped;
        AddChild(stopped);
    }

    private void CloseStopped()
    {
        if (_stopped is not null)
        {
            RemoveChild(_stopped);
            _stopped.QueueFree();
            _stopped = null;
        }
    }

    /// <summary>
    /// Opens the gateway sheet if somebody is standing in it and nothing else is.
    /// </summary>
    /// <remarks>
    /// It waits for the yard: a man asking to be let in while the player is halfway through choosing a
    /// party would take the decision out from under him, and the gateway is patient.
    /// </remarks>
    private void ShowGateIfAnyoneIsAsking()
    {
        if (_gate is not null || _screen is not null || _arena is not null
            || _dojo is not DojoState dojo
            || _viewers.Next() is not GateArrival arrival)
        {
            return;
        }

        GateScreen gate = new() { Arrival = arrival, Dojo = dojo, Layer = 1 };

        gate.LetIn = () =>
        {
            _viewers.Take();
            dojo.Roster.Recruit(arrival.Man.Name, arrival.Man.Stats);
            Save();
            CloseGate();
            _yard?.Post(
                "Someone was let in",
                $"{arrival.Man.Name} sleeps in the hut tonight, and eats from the store tomorrow.",
                "dusk");
        };

        gate.SentAway = () =>
        {
            _viewers.Take();
            CloseGate();
            _yard?.Post(
                "Someone was sent away",
                $"{arrival.Man.Name} walked back down the road. The store holds.",
                "dusk");
        };

        _gate = gate;
        AddChild(gate);
    }

    private void CloseGate()
    {
        if (_gate is not null)
        {
            RemoveChild(_gate);
            _gate.QueueFree();
            _gate = null;
        }
    }

    /// <summary>The settings sheet, opened over whatever is on screen and closing back onto it.</summary>
    private void ShowSettings()
    {
        if (_settings is not null)
        {
            return;
        }

        SettingsScreen settings = new() { Layer = 3 };
        settings.Closed = () =>
        {
            RemoveChild(settings);
            settings.QueueFree();
            _settings = null;
        };

        settings.Changed = () =>
        {
            if (_yard is YardScreen yard && IsInstanceValid(yard))
            {
                yard.AlwaysNamed = GameSettings.NameDestinations;
            }

            ApplyPaper();
        };

        _settings = settings;
        AddChild(settings);
    }

    /// <summary>
    /// The strip along the top of the world: the day, the stores, what is coming, and the hour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It belongs to the hub because it is the one piece of interface that outlives every screen, and
    /// it sits on a layer above them because it is printed onto the night over the top of the yard
    /// rather than pushed into a screen's own first row (design canvas → 7a, 8a).
    /// </para>
    /// <para>
    /// The speed control lives here with the hour: a pausable-real-time game is played with one hand on
    /// the clock, and a clock the player has to walk to is a clock he will forget to stop.
    /// </para>
    /// </remarks>
    private void BuildStrip()
    {
        CloseStrip();

        CanvasLayer layer = new() { Layer = 2 };
        Control page = new() { AnchorRight = 1, Theme = UiKit.Theme };
        layer.AddChild(page);

        PanelContainer band = new() { AnchorRight = 1 };
        band.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(new Color(UiKit.Ground, 0.82f)));
        page.AddChild(band);

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 26);
        UiKit.Padded(band, 96, 18).AddChild(row);

        _stripRow = row;
        _strip = layer;
        AddChild(layer);
        RefreshStrip();
    }

    /// <summary>
    /// Stands the roster on the ground of the yard again.
    /// </summary>
    /// <remarks>
    /// It is called wherever the strip is reprinted, and for the same reason: both say what the dojo
    /// is today, and a yard whose men were bought yesterday is as wrong as a strip whose day is.
    /// </remarks>
    private void RefreshYardMen()
    {
        if (_dojo is DojoState dojo && _yard is YardScreen yard && IsInstanceValid(yard))
        {
            yard.StandMen(RosterModel.Describe(dojo));
        }
    }

    /// <summary>Reprints the strip. The day, the stores and the hour are all read off the dojo.</summary>
    private void RefreshStrip()
    {
        if (_stripRow is not HBoxContainer row || !IsInstanceValid(row) || _dojo is not DojoState dojo)
        {
            return;
        }

        foreach (Node child in row.GetChildren())
        {
            row.RemoveChild(child);
            child.QueueFree();
        }

        StripLine line = StripModel.Describe(dojo);

        row.AddChild(UiKit.Figure(line.Day, line.Term, UiKit.TitleSize));
        row.AddChild(Divider());

        HBoxContainer stores = new();
        stores.AddThemeConstantOverride("separation", 22);
        row.AddChild(stores);

        foreach (StripStore store in line.Stores)
        {
            stores.AddChild(UiKit.Figure(
                store.Figure,
                store.Name,
                UiKit.FigureSize,
                store.Pressing ? UiKit.BrickLit : null));
        }

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        row.AddChild(Divider());
        row.AddChild(BuildHour());
    }

    /// <summary>The hour of the day, as a run along a trough, with the speed control beside it.</summary>
    private Control BuildHour()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 10);

        _hourLabel = UiKit.OnNight(string.Empty, UiKit.NightMuted, UiKit.NoteSize + 2);
        row.AddChild(_hourLabel);

        PanelContainer trough = new()
        {
            CustomMinimumSize = new Vector2(116, 10),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        trough.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.NightEdge));
        row.AddChild(trough);

        // The run is laid out by ratio inside the trough, so the hour keeps its place whatever the
        // window does to the strip's width.
        HBoxContainer split = new();
        trough.AddChild(split);

        ColorRect run = new() { Color = new Color(0.44f, 0.40f, 0.34f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        Control rest = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        split.AddChild(run);
        split.AddChild(rest);
        _hourRun = run;

        foreach ((string text, ClockSpeed speed) in Speeds)
        {
            Button button = new() { Text = text };
            button.AddThemeFontSizeOverride("font_size", UiKit.NoteSize);
            ClockSpeed chosen = speed;
            button.Pressed += () => Choose(chosen);
            row.AddChild(UiKit.WayOut(button));
        }

        UpdateHour();
        return row;
    }

    /// <summary>
    /// Hangs the paper over the world, or takes it away — whichever the settings ask for.
    /// </summary>
    /// <remarks>
    /// Called when the term opens and whenever the switch is moved, so the change lands on the screen
    /// the player is looking at rather than on the next one.
    /// </remarks>
    private void ApplyPaper()
    {
        if (GameSettings.PaperGrain)
        {
            if (_paper is null || !IsInstanceValid(_paper))
            {
                _paper = new PaperOverlay { Hour = _clock.Progress };
                AddChild(_paper);
            }

            return;
        }

        if (_paper is PaperOverlay paper && IsInstanceValid(paper))
        {
            RemoveChild(paper);
            paper.QueueFree();
        }

        _paper = null;
    }

    /// <summary>A hairline standing up between two runs of the strip.</summary>
    private static Control Divider() =>
        new ColorRect { Color = UiKit.NightEdge, CustomMinimumSize = new Vector2(1, 0) };

    /// <summary>Moves the hour along. Called every frame, so it touches nothing it need not.</summary>
    private void UpdateHour()
    {
        if (_hourLabel is null || !IsInstanceValid(_hourLabel))
        {
            return;
        }

        _hourLabel.Text = _clock.IsHeld
            ? "the yard waits for you"
            : _clock.Speed switch
            {
                ClockSpeed.Normal => "the hour turns",
                ClockSpeed.Fast => "the hour turns · 2x",
                ClockSpeed.Fastest => "the hour turns · 4x",
                _ => "the hour is held",
            };

        if (_hourRun is Control run && IsInstanceValid(run))
        {
            run.SizeFlagsStretchRatio = Math.Max(0.001f, (float)_clock.Progress);

            if (run.GetParent() is Control split && split.GetChildCount() > 1
                && split.GetChild(1) is Control left)
            {
                left.SizeFlagsStretchRatio = Math.Max(0.001f, 1f - (float)_clock.Progress);
            }
        }
    }

    /// <summary>Takes the strip down — the term is over, or the player is back at the title.</summary>
    private void CloseStrip()
    {
        _stripRow = null;
        _hourLabel = null;
        _hourRun = null;

        if (_strip is not null)
        {
            RemoveChild(_strip);
            _strip.QueueFree();
            _strip = null;
        }
    }

    /// <summary>Takes the yard down. Only the title screen and a finished term do this.</summary>
    private void CloseYard()
    {
        if (_yard is not null)
        {
            RemoveChild(_yard);
            _yard.QueueFree();
            _yard = null;
        }
    }
}
