using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>Hub'ın açabildiği ekranlar.</summary>
public enum DojoTab
{
    /// <summary>Günün teklifi, sözleşme ve ekip.</summary>
    Day,

    /// <summary>Kadro.</summary>
    Roster,

    /// <summary>Köle pazarı.</summary>
    Market,

    /// <summary>Okul ağacı.</summary>
    School,
}

/// <summary>
/// Dojo'nun dört ekranını tek bir <see cref="DojoState"/> üzerinde gezdiren kök.
/// </summary>
/// <remarks>
/// <para>
/// Dojo <b>bir kez</b> kuruluyor ve dört ekran aynı nesneyi görüyor: pazardan alınan
/// savaşçı kadro ekranında, okuldan alınan tesis günün hesabında anında görünmeli.
/// Her ekran kendi kopyasını taşısaydı gün döngüsü dört ayrı yerde ayrışırdı.
/// </para>
/// <para>
/// Ekran değişince yenisi baştan kuruluyor, eskisi siliniyor: ekranların hepsi
/// açılışta okuduğu için (gizlenip geri gösterilen ekran eski günü basardı) en ucuz
/// doğru davranış bu.
/// </para>
/// <para>
/// Sefere çıkıldığında ekranların yerini <see cref="BattleArena"/> alıyor: dövüş
/// izleniyor, bitince hesabı sefer katmanı kapatıyor ve gün ekranı bilançoyla geri
/// geliyor. Arenanın dojo'dan haberi yok — kurulmuş bir dövüş alıyor, ham sonuç
/// veriyor.
/// </para>
/// <para>
/// Dojo <see cref="TitleScreen"/>'den geliyor: ya <see cref="SaveSlot"/>'tan yüklenir ya
/// da <see cref="NewGame.Create"/> yeni bir sefer kurar. Ekranların hiçbiri bunu
/// bilmiyor, hepsi hazır bir dojo alıyor.
/// </para>
/// <para>
/// Kaydı <b>yalnızca hub yazıyor</b>: ekranlar dojo'yu değiştirdiklerini
/// <see cref="DojoScreen.Changed"/> ile söyler, dosyayı bilen tek yer burasıdır. Yazma
/// her değişiklikte olur — 60 günlük bir sefer tek oturumda oynanmıyor ve oyun bir
/// gün ortasında da kapanabilir.
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
    /// Oyun kapanırken son hâli yazar.
    /// </summary>
    /// <remarks>
    /// Her değişiklikte zaten yazılıyor; bu, arada kalan bir şey varsa diye son turdur.
    /// Kapanışa güvenilemez (çökme, güç kesintisi) — bu yüzden tek yazma noktası değil.
    /// </remarks>
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            Save();
        }
    }

    /// <summary>Başlangıç ekranını gösterir; sefer buradan başlar ya da yüklenir.</summary>
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

    /// <summary>Kayıtlı seferi yükler; yüklenemezse başlangıç ekranında kalınır, sebebi yazılır.</summary>
    private void Continue()
    {
        LoadResult result = SaveSlot.Load();
        if (result.State is not DojoState dojo)
        {
            ShowTitle(string.Join('\n', result.Warnings));
            return;
        }

        // Merge-on-load sessiz kalmasın: eksik yüklenen bir kayıt gün ekranının
        // bilançosunda yazar, oyuncu neyi kaybettiğini görür (GDD §2).
        _report = result.Warnings.Count == 0 ? null : string.Join('\n', result.Warnings);
        Play(dojo);
    }

    /// <summary>Yeni sefer kurar ve ilk günü hemen yazar.</summary>
    /// <remarks>
    /// Tohum rastgele: aynı tohum aynı teklifleri verir, o yüzden her sefer kendi
    /// tohumunu alır. Tohumun kendisi kayda giriyor (GDD §2), yani sefer yeniden
    /// yüklendiğinde aynı günler geri gelir.
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
    /// Kurulmuş dövüşü arenada oynatır.
    /// </summary>
    /// <remarks>
    /// Ekranlar kapanıyor: arena tam ekran bir sahne, üstüne dojo arayüzü binmemeli.
    /// Dövüş bitince hesabı <see cref="PendingBattle.Settle"/> kapatıyor (sefer katmanı),
    /// arena yalnızca ham sonucu veriyor — muhasebe iki yere bölünmüyor.
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

        // Sonuç geldiğinde hesap hemen kapanıyor ama ekran değişmiyor: oyuncu dövüşün
        // son karesini görmeden dojo'ya fırlatılmamalı, dönüşü kendi tuşuyla yapıyor.
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

    /// <summary>Dövüş bitince beliren tek tuş: dojo'ya dön.</summary>
    private void ShowReturnButton()
    {
        CanvasLayer chrome = new();
        MarginContainer margin = new() { AnchorRight = 1, AnchorBottom = 1 };
        margin.AddThemeConstantOverride("margin_bottom", 32);
        chrome.AddChild(margin);

        Button back = new()
        {
            Text = "Dojo'ya dön",
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
        DojoTab.Roster => "Kadro",
        DojoTab.Market => "Pazar",
        DojoTab.School => "Okul",
        _ => "Gün",
    };
}
