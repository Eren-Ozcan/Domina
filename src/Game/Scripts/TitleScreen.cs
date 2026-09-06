using Godot;

namespace Domina.Game;

/// <summary>Oyunun ilk ekranı: yeni oyun mu, kaldığı yerden mi.</summary>
/// <remarks>
/// <para>
/// Ekran <b>dojo almaz</b>: dojo'nun nereden geleceğine burada karar verilir, o yüzden
/// diğer ekranlarla aynı iskeleti paylaşmıyor.
/// </para>
/// <para>
/// "Yeni oyun" kayıt varken <b>onay ister</b>: tek yuva var ve permadeath'li bir sefer
/// yanlış tuşla silinmemeli.
/// </para>
/// </remarks>
public sealed partial class TitleScreen : CanvasLayer
{
    private Label _note = null!;
    private Button _newGame = null!;
    private bool _confirming;

    /// <summary>Kaydı yükle.</summary>
    public Action? Continued { get; set; }

    /// <summary>Yeni bir sefer başlat — varsa eski kaydın üstüne.</summary>
    public Action? Started { get; set; }

    /// <summary>Ekranın altında duracak uyarı; yükleme eksik yaptıysa yazılır.</summary>
    public string? Warning { get; set; }

    public override void _Ready()
    {
        ColorRect backdrop = new()
        {
            Color = new Color(0.09f, 0.09f, 0.11f),
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        AddChild(backdrop);

        CenterContainer center = new() { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);

        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 16);
        center.AddChild(column);

        Label title = new()
        {
            Text = "DOMINA",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 48);
        column.AddChild(title);

        bool saved = SaveSlot.Exists();

        Button resume = new()
        {
            Text = "Kaldığın yerden devam et",
            Disabled = !saved,
        };
        resume.Pressed += () => Continued?.Invoke();
        column.AddChild(resume);

        _newGame = new Button { Text = saved ? "Yeni oyun (kaydın silinir)" : "Yeni oyun" };
        _newGame.Pressed += () => StartPressed(saved);
        column.AddChild(_newGame);

        _note = new Label
        {
            Text = Warning ?? (saved ? string.Empty : "Kayıtlı sefer yok."),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(420, 0),
        };
        _note.AddThemeColorOverride("font_color", new Color(0.78f, 0.70f, 0.32f));
        column.AddChild(_note);
    }

    private void StartPressed(bool saved)
    {
        if (saved && !_confirming)
        {
            _confirming = true;
            _newGame.Text = "Eminsen bir daha bas";
            _note.Text = "Yeni oyun kayıtlı seferi siler; permadeath'te geri dönüşü yok.";
            return;
        }

        Started?.Invoke();
    }
}
