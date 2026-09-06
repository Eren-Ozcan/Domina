using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// Kadro ekranı: savaşçılar, statlar, yaralar, onur, isim düzenleme (GDD §6, §8).
/// </summary>
/// <remarks>
/// <para>
/// Ne yazacağına ve satırların hangi sırada geleceğine <see cref="RosterModel"/> karar
/// verir (motorsuz, testli); buradaki iş düğümleri kurup metni basmak. Ekran
/// <see cref="Roster"/>'ı doğrudan yazmaz — talim, yol ve ad değişikliği
/// <see cref="DojoState"/> üzerinden geçer, kurallar orada.
/// </para>
/// <para>
/// Ad değiştirme tuşu <see cref="RosterModel.JudgeRename"/> ile kapatılır: çakışan adda
/// <see cref="Roster.Rename"/> fırlatır, oyuncu bunu istisnadan değil sönük tuştan
/// öğrenmeli.
/// </para>
/// </remarks>
public sealed partial class RosterScreen : CanvasLayer
{
    private static readonly Color ReadyColor = new(0.82f, 0.82f, 0.78f);
    private static readonly Color TrainingColor = new(0.78f, 0.70f, 0.32f);
    private static readonly Color RecoveringColor = new(0.85f, 0.55f, 0.20f);
    private static readonly Color FallenColor = new(0.45f, 0.45f, 0.48f);
    private static readonly Color WarningColor = new(0.80f, 0.35f, 0.35f);

    private DojoState _dojo = null!;
    private VBoxContainer _list = null!;
    private Label _summary = null!;
    private Label _detail = null!;
    private LineEdit _nameEdit = null!;
    private Button _renameButton = null!;
    private Label _renameNotice = null!;
    private OptionButton _drillPicker = null!;
    private HBoxContainer _pathRow = null!;
    private WarriorId? _selected;

    /// <summary>Ekranı kurar ve kadroyu basar.</summary>
    /// <param name="dojo">Gösterilecek dojo — ekran bunu okur ve komutları buna verir.</param>
    public void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

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
        // Ayrıntı sütunu geniş ekranda sonsuza uzamamalı: satırlar okunacak kadar dar
        // kalsın, aradaki boşluk listeye değil kenara gitsin.
        VBoxContainer panel = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(560, 0),
        };
        panel.AddThemeConstantOverride("separation", 10);

        _detail = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.AddChild(_detail);

        HBoxContainer renameRow = new();
        panel.AddChild(renameRow);

        _nameEdit = new LineEdit
        {
            PlaceholderText = "Yeni ad",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _nameEdit.TextChanged += _ => UpdateRenameControls();
        renameRow.AddChild(_nameEdit);

        _renameButton = new Button { Text = "Adını değiştir" };
        _renameButton.Pressed += ApplyRename;
        renameRow.AddChild(_renameButton);

        _renameNotice = new Label();
        panel.AddChild(_renameNotice);

        _drillPicker = new OptionButton();
        foreach (Drill drill in Enum.GetValues<Drill>())
        {
            _drillPicker.AddItem(DrillName(drill), (int)drill);
        }

        _drillPicker.ItemSelected += index => AssignDrill((Drill)_drillPicker.GetItemId((int)index));
        panel.AddChild(_drillPicker);

        _pathRow = new HBoxContainer();
        panel.AddChild(_pathRow);

        return panel;
    }

    /// <summary>Kadroyu ve seçili savaşçının ayrıntısını yeniden basar.</summary>
    public void Refresh()
    {
        foreach (Node child in _list.GetChildren())
        {
            child.QueueFree();
        }

        IReadOnlyList<RosterRow> rows = RosterModel.Describe(_dojo);
        _selected ??= rows.Count > 0 ? rows[0].Id : null;

        foreach (RosterRow row in rows)
        {
            Button button = new()
            {
                Text = RowText(row),
                Alignment = HorizontalAlignment.Left,
                ToggleMode = true,
                ButtonPressed = row.Id == _selected,
            };
            button.AddThemeColorOverride("font_color", StatusColor(row.Status));

            WarriorId id = row.Id;
            button.Pressed += () =>
            {
                _selected = id;
                Refresh();
            };

            _list.AddChild(button);
        }

        RosterSummary summary = RosterModel.Summarize(_dojo);
        _summary.Text =
            $"Gün {_dojo.Day}  ·  Kadro {summary.Living}  ·  Sefere hazır {summary.Fit}" +
            $"  ·  Revirde {summary.Recovering}  ·  Ölü {summary.Fallen}" +
            $"  ·  Sefer kadrosu en çok {summary.PartyCapacity}";

        ShowDetail(rows.FirstOrDefault(r => r.Id == _selected));
    }

    private void ShowDetail(RosterRow row)
    {
        if (row.Name is null)
        {
            _detail.Text = "Kadro boş.";
            _nameEdit.Editable = false;
            _drillPicker.Disabled = true;
            _renameButton.Disabled = true;
            _pathRow.Visible = false;
            return;
        }

        WarriorStats raw = row.BaseStats;
        WarriorStats live = row.EffectiveStats;

        _detail.Text = string.Join(
            '\n',
            $"{row.Name}  ({StatusName(row.Status)})",
            row.IsAlive
                ? $"Onur {row.Honor:0}  ·  Antrenman günü {row.TrainingDays}"
                : "Bu savaşçı öldü — kayıt kadroda kalır.",
            row.RecoveryDaysRemaining > 0
                ? $"Revirde: {row.RecoveryDaysRemaining} gün"
                : "Sefere hazır",
            string.Empty,
            $"Can       {Pair(raw.MaxHealth, live.MaxHealth)}",
            $"Saldırganlık {Pair(raw.Aggression, live.Aggression)}",
            $"Savunma   {Pair(raw.Defense, live.Defense)}",
            $"Kaçınma   {Pair(raw.Evasion, live.Evasion)}",
            $"Güç       {Pair(raw.Strength, live.Strength)}",
            $"İsabet    {Pair(raw.Accuracy, live.Accuracy)}",
            $"Stamina   {Pair(raw.MaxStamina, live.MaxStamina)}",
            $"Hız       {Pair(raw.Speed, live.Speed)}",
            string.Empty,
            $"Silah: {row.WeaponName}   Kuşam: {row.ArmorName} (yıpranma {row.ArmorWear:0.0})",
            $"Kalıcı sakatlık: {LostText(row.Lost)}",
            $"Yol: {PathName(row.Path)}");

        _nameEdit.Editable = row.IsAlive;
        _drillPicker.Disabled = !row.IsFitForCampaign;
        _drillPicker.Select(_drillPicker.GetItemIndex((int)row.Drill));

        BuildPathButtons(row);
        UpdateRenameControls();
    }

    private void BuildPathButtons(RosterRow row)
    {
        foreach (Node child in _pathRow.GetChildren())
        {
            child.QueueFree();
        }

        _pathRow.Visible = row.IsAlive;
        if (!row.IsAlive)
        {
            return;
        }

        if (row.Path != WarriorPath.None)
        {
            _pathRow.AddChild(new Label { Text = $"Yol seçildi: {PathName(row.Path)} — geri alınmaz." });
            return;
        }

        if (!row.PathUnlocked)
        {
            _pathRow.AddChild(new Label
            {
                Text = $"Yol seçimi {row.TrainingDaysToPath} antrenman günü sonra açılır.",
            });
            return;
        }

        foreach (WarriorPath path in new[] { WarriorPath.Blade, WarriorPath.Stone, WarriorPath.Shadow })
        {
            Button button = new() { Text = PathName(path) };
            WarriorId id = row.Id;
            WarriorPath chosen = path;
            button.Pressed += () =>
            {
                _dojo.ChoosePath(id, chosen);
                Refresh();
            };
            _pathRow.AddChild(button);
        }
    }

    private void UpdateRenameControls()
    {
        if (_selected is not WarriorId id)
        {
            return;
        }

        RenameVerdict verdict = RosterModel.JudgeRename(_dojo.Roster, id, _nameEdit.Text);
        bool alive = _dojo.Roster.Find(id)?.Warrior.IsAlive ?? false;

        _renameButton.Disabled = !alive || verdict != RenameVerdict.Ok;
        _renameNotice.Text = verdict switch
        {
            RenameVerdict.Taken => "Bu ad canlı bir savaşçıda.",
            RenameVerdict.Unchanged => "Ad zaten bu.",
            _ => string.Empty,
        };
        _renameNotice.AddThemeColorOverride("font_color", WarningColor);
    }

    private void ApplyRename()
    {
        if (_selected is not WarriorId id
            || RosterModel.JudgeRename(_dojo.Roster, id, _nameEdit.Text) != RenameVerdict.Ok)
        {
            return;
        }

        _dojo.Roster.Rename(id, _nameEdit.Text);
        _nameEdit.Text = string.Empty;
        Refresh();
    }

    private void AssignDrill(Drill drill)
    {
        if (_selected is WarriorId id)
        {
            _dojo.Roster.Find(id)?.Train(drill);
            Refresh();
        }
    }

    private static string RowText(RosterRow row) => row.Status switch
    {
        RosterStatus.Recovering => $"{row.Name}  —  revir {row.RecoveryDaysRemaining}g",
        RosterStatus.Fallen => $"{row.Name}  —  ölü",
        RosterStatus.Training => $"{row.Name}  —  {DrillName(row.Drill)}",
        _ => $"{row.Name}  —  hazır",
    };

    private static string Pair(double raw, double effective) =>
        Math.Abs(raw - effective) < 0.05
            ? $"{raw:0}"
            : $"{raw:0}  →  {effective:0}";

    private static string LostText(BodyPartSet lost)
    {
        if (lost == BodyPartSet.None)
        {
            return "yok";
        }

        List<string> parts = [];
        if (lost.HasFlag(BodyPartSet.SwordArm))
        {
            parts.Add("kılıç kolu");
        }

        if (lost.HasFlag(BodyPartSet.OffArm))
        {
            parts.Add("boştaki kol");
        }

        if (lost.HasFlag(BodyPartSet.RightLeg))
        {
            parts.Add("sağ bacak");
        }

        if (lost.HasFlag(BodyPartSet.LeftLeg))
        {
            parts.Add("sol bacak");
        }

        if (lost.HasFlag(BodyPartSet.Eye))
        {
            parts.Add("göz");
        }

        return string.Join(", ", parts);
    }

    private static Color StatusColor(RosterStatus status) => status switch
    {
        RosterStatus.Training => TrainingColor,
        RosterStatus.Recovering => RecoveringColor,
        RosterStatus.Fallen => FallenColor,
        _ => ReadyColor,
    };

    private static string StatusName(RosterStatus status) => status switch
    {
        RosterStatus.Training => "antrenmanda",
        RosterStatus.Recovering => "revirde",
        RosterStatus.Fallen => "ölü",
        _ => "hazır",
    };

    private static string DrillName(Drill drill) => drill switch
    {
        Drill.Guard => "Siper talimi",
        Drill.Footwork => "Ayak talimi",
        Drill.Conditioning => "Kondisyon",
        _ => "Vuruş talimi",
    };

    private static string PathName(WarriorPath path) => path switch
    {
        WarriorPath.Blade => "Kılıç",
        WarriorPath.Stone => "Kaya",
        WarriorPath.Shadow => "Gölge",
        _ => "seçilmedi",
    };
}
