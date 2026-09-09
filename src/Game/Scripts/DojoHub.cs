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
    private DojoState? _dojo;
    private TitleScreen? _title;
    private DojoScreen? _screen;
    private Node2D? _arena;
    private CanvasLayer? _arenaChrome;
    private DojoTab _tab = DojoTab.Day;
    private string? _report;

    public override void _Ready()
    {
        ShowTitle(null);
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
        _tab = DojoTab.Day;

        if (_title is not null)
        {
            RemoveChild(_title);
            _title.QueueFree();
            _title = null;
        }

        Show(_tab);
    }

    /// <summary>Dojo'nun o anki hâlini yuvaya yazar.</summary>
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

        DojoScreen screen = tab switch
        {
            DojoTab.Roster => new RosterScreen(),
            DojoTab.Market => new MarketScreen(),
            DojoTab.School => new SchoolScreen(),
            _ => new DayScreen(),
        };

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
            _report = bout.Settle(result);
            Save();
            ShowReturnButton();
        };

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

    private Control BuildNav()
    {
        HBoxContainer nav = new();
        nav.AddThemeConstantOverride("separation", 8);

        foreach (DojoTab tab in Enum.GetValues<DojoTab>())
        {
            Button button = new()
            {
                Text = TabName(tab),
                ToggleMode = true,
                ButtonPressed = tab == _tab,
                Disabled = tab == _tab,
            };

            DojoTab target = tab;
            button.Pressed += () => Show(target);
            nav.AddChild(button);
        }

        return nav;
    }

    private static string TabName(DojoTab tab) => tab switch
    {
        DojoTab.Roster => "Roster",
        DojoTab.Market => "Market",
        DojoTab.School => "School",
        _ => "Day",
    };
}
