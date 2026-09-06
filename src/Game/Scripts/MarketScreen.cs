using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// Pazar ekranı: adaylar, statları, yetenek bandı ve fiyatı (GDD §10).
/// </summary>
/// <remarks>
/// <para>
/// Ne yazacağına ve satırların sırasına <see cref="MarketModel"/> karar verir (motorsuz,
/// testli); buradaki iş düğümleri kurup metni basmak. Alım <see cref="Quartermaster"/>
/// üzerinden geçer — fiyatı kasadan düşen ve adayı kadroya yazan taraf orası.
/// </para>
/// <para>
/// Adayın statları <b>kadronun en iyisiyle</b> yan yana basılıyor: pazarın asıl sorusu
/// "bu aday iyi mi" değil, "elimdekinden iyi mi". Tek başına duran sayı bu soruyu
/// cevaplamaz.
/// </para>
/// </remarks>
public sealed partial class MarketScreen : DojoScreen
{
    private DojoState _dojo = null!;
    private VBoxContainer _list = null!;
    private Label _summary = null!;
    private Label _detail = null!;
    private Button _buyButton = null!;
    private Label _notice = null!;
    private int? _selected;

    /// <summary>Ekranı kurar ve tezgâhı basar.</summary>
    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

        VBoxContainer page = BuildPage();

        _summary = new Label();
        page.AddChild(_summary);

        HSplitContainer split = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        page.AddChild(split);

        ScrollContainer scroll = new() { CustomMinimumSize = new Vector2(320, 0) };
        split.AddChild(scroll);

        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_list);

        split.AddChild(BuildDetailPanel());

        Refresh();
    }

    private Control BuildDetailPanel()
    {
        VBoxContainer panel = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(560, 0),
        };
        panel.AddThemeConstantOverride("separation", 10);

        _detail = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.AddChild(_detail);

        _buyButton = new Button { Text = "Satın al" };
        _buyButton.Pressed += Buy;
        panel.AddChild(_buyButton);

        _notice = new Label();
        _notice.AddThemeColorOverride("font_color", WarningColor);
        panel.AddChild(_notice);

        return panel;
    }

    /// <summary>Tezgâhı ve seçili adayın ayrıntısını yeniden basar.</summary>
    public void Refresh()
    {
        Clear(_list);

        IReadOnlyList<MarketRow> rows = MarketModel.Describe(_dojo);
        if (_selected is null && rows.Count > 0)
        {
            _selected = rows[0].Index;
        }

        foreach (MarketRow row in rows)
        {
            Button button = new()
            {
                Text = RowText(row),
                Alignment = HorizontalAlignment.Left,
                ToggleMode = true,
                ButtonPressed = row.Index == _selected,
            };
            button.AddThemeColorOverride("font_color", RowColor(row));

            int index = row.Index;
            button.Pressed += () =>
            {
                _selected = index;
                Refresh();
            };

            _list.AddChild(button);
        }

        MarketSummary summary = MarketModel.Summarize(_dojo);
        _summary.Text =
            $"Gün {_dojo.Day}  ·  Kasa {summary.Gold} altın  ·  Aday {summary.Candidates}" +
            $"  ·  Alınabilir {summary.Affordable}  ·  Bugün alınan {summary.Bought}" +
            $"  ·  Tezgâh {summary.DaysToRefresh} gün sonra yenilenir";

        ShowDetail(rows.FirstOrDefault(r => r.Index == _selected));
    }

    private void ShowDetail(MarketRow row)
    {
        if (row.Name is null)
        {
            _detail.Text = "Bugün tezgâhta kimse yok.";
            _buyButton.Disabled = true;
            _notice.Text = string.Empty;
            return;
        }

        WarriorStats stats = row.Stats;
        WarriorStats? best = BestLivingStats();

        _detail.Text = string.Join(
            '\n',
            $"{row.Name}  —  {row.Price} altın",
            $"Yetenek: {BandName(row.Band)}  ·  antrenmanın kazancını bu çarpar",
            row.BetterInRoster == 0
                ? "Kadroda bundan iyisi yok."
                : $"Kadroda bundan iyi {row.BetterInRoster} savaşçı var.",
            string.Empty,
            best is null ? "Kadro boş — kıyas yok." : "Sol sütun aday, sağ sütun kadronun en iyisi:",
            $"Can       {Pair(stats.MaxHealth, best?.MaxHealth)}",
            $"Saldırganlık {Pair(stats.Aggression, best?.Aggression)}",
            $"Savunma   {Pair(stats.Defense, best?.Defense)}",
            $"Kaçınma   {Pair(stats.Evasion, best?.Evasion)}",
            $"Güç       {Pair(stats.Strength, best?.Strength)}",
            $"İsabet    {Pair(stats.Accuracy, best?.Accuracy)}",
            $"Stamina   {Pair(stats.MaxStamina, best?.MaxStamina)}",
            $"Hız       {Pair(stats.Speed, best?.Speed)}");

        _buyButton.Disabled = row.Bought || !row.Affordable;
        _buyButton.Text = row.Bought ? "Alındı" : $"Satın al ({row.Price} altın)";
        _notice.Text = row.Bought || row.Affordable
            ? string.Empty
            : $"Kasa yetmiyor: {row.Price - _dojo.Resources.Gold} altın eksik.";
    }

    /// <summary>
    /// Seçili adayı satın alır.
    /// </summary>
    /// <remarks>
    /// Alım <see cref="DojoState.HireRecruit"/> üzerinden geçiyor: alınan adayı kaydeden
    /// ve aynı adamın iki kez satılmasını engelleyen taraf çekirdek. Ekranın kendi
    /// işareti olsaydı kaydı yükleyip aynı adayı yeniden almak açık kalırdı.
    /// </remarks>
    private void Buy()
    {
        if (_selected is not int index)
        {
            return;
        }

        if (_dojo.HireRecruit(index) is null)
        {
            _notice.Text = "Bu aday şimdi alınamaz.";
            return;
        }

        Persist();
        Refresh();
    }

    private WarriorStats? BestLivingStats()
    {
        RosterEntry? best = _dojo.Roster.Living
            .OrderByDescending(e => MarketModel.Score(e.Warrior.BaseStats))
            .FirstOrDefault();

        return best?.Warrior.BaseStats;
    }

    private static string RowText(MarketRow row) => row.Bought
        ? $"{row.Name}  —  alındı"
        : $"{row.Name}  —  {row.Price} altın  ·  {BandName(row.Band)}";

    private static Color RowColor(MarketRow row)
    {
        if (row.Bought)
        {
            return GoodColor;
        }

        return row.Affordable ? InkColor : MutedColor;
    }

    /// <summary>Adayın statı, kadronun en iyisi yanında.</summary>
    private static string Pair(double candidate, double? best) =>
        best is null ? $"{candidate:0}" : $"{candidate:0}   ({best:0})";

    private static string BandName(TalentBand band) => band switch
    {
        TalentBand.Dull => "kütük",
        TalentBand.Fair => "sıradan",
        TalentBand.Promising => "umut verici",
        _ => "nadir",
    };
}
