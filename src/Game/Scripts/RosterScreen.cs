using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The roster screen: warriors, stats, wounds, honour, name editing (GDD §6, §8).
/// </summary>
/// <remarks>
/// <para>
/// What it says and the order the rows come in are decided by <see cref="RosterModel"/> (engine-free,
/// tested); the job here is building the nodes and printing the text. The screen does not write to
/// <see cref="Roster"/> directly — the drill, the path and a name change go through
/// <see cref="DojoState"/>, and the rules live there.
/// </para>
/// <para>
/// The rename button is disabled through <see cref="RosterModel.JudgeRename"/>: on a clashing name
/// <see cref="Roster.Rename"/> throws, and the player should learn that from a dimmed button rather
/// than from an exception.
/// </para>
/// </remarks>
public sealed partial class RosterScreen : DojoScreen
{
    private static readonly Color ReadyColor = InkColor;
    private static readonly Color TrainingColor = PendingColor;
    private static readonly Color RecoveringColor = new(0.85f, 0.55f, 0.20f);
    private static readonly Color FallenColor = MutedColor;

    private DojoState _dojo = null!;
    private VBoxContainer _list = null!;
    private Label _summary = null!;
    private Label _detail = null!;
    private LineEdit _nameEdit = null!;
    private Button _renameButton = null!;
    private Button _releaseButton = null!;
    private Label _releaseNotice = null!;
    private bool _releaseArmed;
    private Label _renameNotice = null!;
    private OptionButton _drillPicker = null!;
    private HBoxContainer _pathRow = null!;
    private Label _charmLabel = null!;
    private VBoxContainer _charmRows = null!;
    private WarriorId? _selected;

    /// <summary>Builds the screen and prints the roster.</summary>
    /// <param name="dojo">The dojo to show — the screen reads it and gives its commands to it.</param>
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
        // The detail column must not stretch forever on a wide screen: the rows should stay narrow
        // enough to read, and the space in between should go to the margin rather than to the list.
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
            PlaceholderText = "New name",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _nameEdit.TextChanged += _ => UpdateRenameControls();
        renameRow.AddChild(_nameEdit);

        _renameButton = new Button { Text = "Rename" };
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

        // The charms sit under the path because they are the other thing carried onto the field, and
        // unlike the path they can be moved from one man to another on any day (docs/GDD.md §10).
        _charmLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.AddChild(_charmLabel);

        _charmRows = new VBoxContainer();
        _charmRows.AddThemeConstantOverride("separation", 4);
        panel.AddChild(_charmRows);

        // Releasing a man is the one thing on this screen that cannot be undone and costs nothing to
        // press, so it asks twice — the same courtesy the rest of the dojo owes an irreversible move.
        _releaseButton = new Button { Text = "End his term" };
        _releaseButton.Pressed += Release;
        panel.AddChild(_releaseButton);

        _releaseNotice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.AddChild(_releaseNotice);

        return panel;
    }

    /// <summary>Reprints the roster and the selected warrior's detail.</summary>
    public void Refresh()
    {
        Clear(_list);

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
                // Moving to another man disarms the release: the confirmation belongs to the warrior it
                // was armed for, not to the button.
                _selected = id;
                _releaseArmed = false;
                Refresh();
            };

            _list.AddChild(button);
        }

        RosterSummary summary = RosterModel.Summarize(_dojo);
        _summary.Text =
            $"Day {_dojo.Day}  ·  Roster {summary.Living}/{summary.Beds}  ·  Ready {summary.Fit}" +
            $"  ·  Infirmary {summary.Recovering}  ·  Dead {summary.Fallen}" +
            (summary.Freed > 0 ? $"  ·  Walked out {summary.Freed}" : string.Empty) +
            $"  ·  Party of at most {summary.PartyCapacity}";

        ShowDetail(rows.FirstOrDefault(r => r.Id == _selected));
    }

    private void ShowDetail(RosterRow row)
    {
        if (row.Name is null)
        {
            _detail.Text = "The roster is empty.";
            _nameEdit.Editable = false;
            _drillPicker.Disabled = true;
            _renameButton.Disabled = true;
            _releaseButton.Visible = false;
            _releaseNotice.Text = string.Empty;
            _pathRow.Visible = false;
            return;
        }

        WarriorStats raw = row.BaseStats;
        WarriorStats live = row.EffectiveStats;

        _detail.Text = string.Join(
            '\n',
            $"{row.Name}  ({StatusName(row.Status)})",
            row.IsAlive
                ? $"Honour {row.Honor:0}  ·  Training days {row.TrainingDays}"
                : "This warrior died — the record stays on the roster.",
            row.RecoveryDaysRemaining > 0
                ? $"Infirmary: {row.RecoveryDaysRemaining} days"
                : "Ready",
            string.Empty,
            $"Health      {Pair(raw.MaxHealth, live.MaxHealth)}",
            $"Aggression  {Pair(raw.Aggression, live.Aggression)}",
            $"Defence     {Pair(raw.Defense, live.Defense)}",
            $"Evasion     {Pair(raw.Evasion, live.Evasion)}",
            $"Strength    {Pair(raw.Strength, live.Strength)}",
            $"Accuracy    {Pair(raw.Accuracy, live.Accuracy)}",
            $"Stamina     {Pair(raw.MaxStamina, live.MaxStamina)}",
            $"Speed       {Pair(raw.Speed, live.Speed)}",
            string.Empty,
            row.WeaponSkill > 0
                ? $"Weapon: {row.WeaponName} (mastery {row.WeaponSkill * 100:0}%)"
                  + $"   Armour: {row.ArmorName} (wear {row.ArmorWear:0.0})"
                : $"Weapon: {row.WeaponName}   Armour: {row.ArmorName} (wear {row.ArmorWear:0.0})",
            $"Limb loss: {LostText(row.Lost)}",
            $"Path: {PathName(row.Path)}");

        _nameEdit.Editable = row.IsAlive && row.Status != RosterStatus.Freed;
        _drillPicker.Disabled = !row.IsFitForCampaign;
        UpdateReleaseControls(row);
        _drillPicker.Select(_drillPicker.GetItemIndex((int)row.Drill));

        BuildPathButtons(row);
        BuildCharmRows(row);
        UpdateRenameControls();
    }

    /// <summary>
    /// The charms he wears, the slots he has left, and the temple's stall.
    /// </summary>
    /// <remarks>
    /// A dojo with no shrine sees one line saying so rather than an empty panel: the omamori is the
    /// temple's supply, and a screen that showed five buyable charms with nowhere to put them would be
    /// advertising a system the dojo has not bought.
    /// </remarks>
    private void BuildCharmRows(RosterRow row)
    {
        Clear(_charmRows);

        if (row.CharmSlots <= 0)
        {
            _charmLabel.Text = "Charms: the shrine is not standing.";
            return;
        }

        IReadOnlyList<OmamoriKind> worn = row.Charms ?? [];
        _charmLabel.Text = $"Charms {worn.Count}/{row.CharmSlots}";

        foreach (OmamoriKind charm in worn)
        {
            OmamoriKind kind = charm;
            Button off = new() { Text = $"Take off — {Omamori.Find(kind).Name}" };
            off.Pressed += () =>
            {
                _dojo.UnfitCharm(row.Id, kind);
                Persist();
                Refresh();
            };

            _charmRows.AddChild(off);
        }

        bool room = worn.Count < row.CharmSlots && row.IsAlive && row.Status != RosterStatus.Freed;

        foreach (OmamoriCharm charm in Omamori.All)
        {
            int held = _dojo.CharmStore.GetValueOrDefault(charm.Kind);
            HBoxContainer line = new();
            line.AddThemeConstantOverride("separation", 6);

            Label name = new()
            {
                Text = $"{charm.Name}  ·  in store {held}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            name.AddThemeColorOverride("font_color", held > 0 ? InkColor : MutedColor);
            line.AddChild(name);

            OmamoriKind kind = charm.Kind;

            Button buy = new()
            {
                Text = $"Buy ({charm.Price})",
                Disabled = _dojo.Resources.Gold < charm.Price,
            };
            buy.Pressed += () =>
            {
                _dojo.BuyCharm(kind);
                Persist();
                Refresh();
            };
            line.AddChild(buy);

            Button fit = new() { Text = "Fit", Disabled = !room || held == 0 };
            fit.Pressed += () =>
            {
                _dojo.FitCharm(row.Id, kind);
                Persist();
                Refresh();
            };
            line.AddChild(fit);

            Button sell = new() { Text = "Sell back", Disabled = held == 0 };
            sell.Pressed += () =>
            {
                _dojo.SellCharm(kind);
                Persist();
                Refresh();
            };
            line.AddChild(sell);

            _charmRows.AddChild(line);
        }
    }

    private void BuildPathButtons(RosterRow row)
    {
        Clear(_pathRow);

        _pathRow.Visible = row.IsAlive;
        if (!row.IsAlive)
        {
            return;
        }

        if (row.Path != WarriorPath.None)
        {
            _pathRow.AddChild(new Label { Text = $"Path chosen: {PathName(row.Path)} — it cannot be undone." });
            return;
        }

        if (!row.PathUnlocked)
        {
            _pathRow.AddChild(new Label
            {
                Text = $"The path opens after {row.TrainingDaysToPath} more training days.",
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
                Persist();
                Refresh();
            };
            _pathRow.AddChild(button);
        }
    }

    /// <summary>The release button and what it warns about.</summary>
    private void UpdateReleaseControls(RosterRow row)
    {
        _releaseButton.Visible = row.IsAlive && row.Status != RosterStatus.Freed;
        _releaseButton.Disabled = !row.CanBeReleased;
        _releaseButton.Text = _releaseArmed ? $"Let {row.Name} go — for good" : "End his term";

        _releaseNotice.Text = row.Status switch
        {
            RosterStatus.Freed => "His term is over. He walked out of the gate.",
            RosterStatus.Recovering => "A man in the infirmary is not sent out of the gate.",
            _ when _releaseArmed =>
                "He leaves the roster alive and does not come back. He is counted at the end of the season.",
            _ => string.Empty,
        };
        _releaseNotice.AddThemeColorOverride(
            "font_color",
            row.Status == RosterStatus.Freed ? MutedColor : PendingColor);
    }

    /// <summary>Ends the selected warrior's term — the second press is the one that does it.</summary>
    private void Release()
    {
        if (_selected is not WarriorId id)
        {
            return;
        }

        if (!_releaseArmed)
        {
            _releaseArmed = true;
            Refresh();
            return;
        }

        _releaseArmed = false;
        if (_dojo.Release(id))
        {
            Persist();
        }

        Refresh();
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
            RenameVerdict.Taken => "A living warrior has this name.",
            RenameVerdict.Unchanged => "The name is already this.",
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
        Persist();
        Refresh();
    }

    private void AssignDrill(Drill drill)
    {
        if (_selected is WarriorId id)
        {
            _dojo.Roster.Find(id)?.Train(drill);
            Persist();
            Refresh();
        }
    }

    private static string RowText(RosterRow row) => row.Status switch
    {
        RosterStatus.Recovering => $"{row.Name}  —  infirmary {row.RecoveryDaysRemaining}d",
        RosterStatus.Fallen => $"{row.Name}  —  dead",
        RosterStatus.Training => $"{row.Name}  —  {DrillName(row.Drill)}",
        _ => $"{row.Name}  —  ready",
    };

    private static string Pair(double raw, double effective) =>
        Math.Abs(raw - effective) < 0.05
            ? $"{raw:0}"
            : $"{raw:0}  →  {effective:0}";

    private static string LostText(BodyPartSet lost)
    {
        if (lost == BodyPartSet.None)
        {
            return "none";
        }

        List<string> parts = [];
        if (lost.HasFlag(BodyPartSet.SwordArm))
        {
            parts.Add("sword arm");
        }

        if (lost.HasFlag(BodyPartSet.OffArm))
        {
            parts.Add("off arm");
        }

        if (lost.HasFlag(BodyPartSet.RightLeg))
        {
            parts.Add("right leg");
        }

        if (lost.HasFlag(BodyPartSet.LeftLeg))
        {
            parts.Add("left leg");
        }

        if (lost.HasFlag(BodyPartSet.Eye))
        {
            parts.Add("eye");
        }

        return string.Join(", ", parts);
    }

    private static Color StatusColor(RosterStatus status) => status switch
    {
        RosterStatus.Training => TrainingColor,
        RosterStatus.Recovering => RecoveringColor,
        RosterStatus.Fallen => FallenColor,
        RosterStatus.Freed => MutedColor,
        _ => ReadyColor,
    };

    private static string StatusName(RosterStatus status) => status switch
    {
        RosterStatus.Training => "training",
        RosterStatus.Recovering => "infirmary",
        RosterStatus.Fallen => "dead",
        RosterStatus.Freed => "walked out free",
        _ => "ready",
    };

    private static string DrillName(Drill drill) => drill switch
    {
        Drill.Guard => "Guard drill",
        Drill.Footwork => "Footwork drill",
        Drill.Conditioning => "Conditioning",
        _ => "Striking drill",
    };

    private static string PathName(WarriorPath path) => path switch
    {
        WarriorPath.Blade => "Blade",
        WarriorPath.Stone => "Stone",
        WarriorPath.Shadow => "Shadow",
        _ => "not chosen",
    };
}
