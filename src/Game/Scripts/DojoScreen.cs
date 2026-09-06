using Domina.Core.Dojo;
using Godot;

namespace Domina.Game;

/// <summary>
/// Dojo ekranlarının ortak iskeleti: zemin, kenar boşluğu ve en üstte gezinme çubuğu.
/// </summary>
/// <remarks>
/// <para>
/// Ekranların birbirine benzemesi bir tercih değil, gereklilik: oyuncu gün içinde
/// dört ekran arasında gidip geliyor ve her ekranda özetin yeri, listenin yeri ve
/// tuşların yeri aynı kalmalı.
/// </para>
/// <para>
/// Gezinme çubuğu ekranın <b>içine</b> giriyor, üstüne binen ayrı bir katmana değil:
/// ayrı katmanda dursaydı her ekranın en üst satırını örterdi.
/// </para>
/// </remarks>
public abstract partial class DojoScreen : CanvasLayer
{
    /// <summary>Sıradan metin.</summary>
    protected static readonly Color InkColor = new(0.82f, 0.82f, 0.78f);

    /// <summary>Sönük metin — kapalı seçenek, geçmiş kayıt.</summary>
    protected static readonly Color MutedColor = new(0.45f, 0.45f, 0.48f);

    /// <summary>Olumlu: alınabilir, gönderilebilir, kazanılmış.</summary>
    protected static readonly Color GoodColor = new(0.55f, 0.75f, 0.45f);

    /// <summary>Bekleyen: sırası gelmiş ama parası yetmeyen, süresi daralan.</summary>
    protected static readonly Color PendingColor = new(0.78f, 0.70f, 0.32f);

    /// <summary>Uyarı: reddedilen komut, ölüm, kırılan söz.</summary>
    protected static readonly Color WarningColor = new(0.80f, 0.35f, 0.35f);

    /// <summary>
    /// Ekranın tepesine konacak gezinme çubuğu; tek başına açılan sahnede <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Build"/> çağrılmadan <b>önce</b> verilmeli. Ekranlar tek başına da
    /// açılabildiği için (her birinin kendi sahnesi var) çubuk zorunlu değil.
    /// </remarks>
    public Control? Chrome { get; set; }

    /// <summary>
    /// Ekran dojo'yu değiştirdiğinde çağrılır; kaydı yazan taraf bunu dinler.
    /// </summary>
    /// <remarks>
    /// Ekran <b>kaydı kendi yazmıyor</b>: dosya yolunu ve yuvayı bilen tek yer hub.
    /// Her ekran kendi yazsaydı, kaydın ne zaman yazıldığı dört dosyaya dağılırdı.
    /// </remarks>
    public Action? Changed { get; set; }

    /// <summary>Ekranı kurar ve içeriği basar.</summary>
    /// <param name="dojo">Gösterilecek dojo — ekran bunu okur ve komutları buna verir.</param>
    public abstract void Build(DojoState dojo);

    /// <summary>Zemini, kenar boşluğunu ve gezinme çubuğunu kurar; içerik sütununu döndürür.</summary>
    protected VBoxContainer BuildPage()
    {
        ColorRect backdrop = new()
        {
            Color = new Color(0.09f, 0.09f, 0.11f),
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        AddChild(backdrop);

        MarginContainer margin = new() { AnchorRight = 1, AnchorBottom = 1 };
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        AddChild(margin);

        VBoxContainer page = new();
        page.AddThemeConstantOverride("separation", 12);
        margin.AddChild(page);

        if (Chrome is not null)
        {
            page.AddChild(Chrome);
        }

        return page;
    }

    /// <summary>Dojo değişti; kaydı yazması için hub'a haber verir.</summary>
    protected void Persist() => Changed?.Invoke();

    /// <summary>Düğümün bütün çocuklarını siler — yeniden basmanın ilk adımı.</summary>
    protected static void Clear(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
