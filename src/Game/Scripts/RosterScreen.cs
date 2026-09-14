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
    private Button _retireButton = null!;
    private Label _retireNotice = null!;
    private HBoxContainer _postRow = null!;
    private Button _previousButton = null!;
    private Button _nextButton = null!;
    private Label _placeLabel = null!;
    private bool _retireArmed;
    private Button _feastButton = null!;
    private Label _feastNotice = null!;
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

        // The list is on the left and the detail on the right, and comparing two men meant going back to
        // the list for each of them. The arrows walk the roster in place, in the order the list is in.
        _previousButton = new Button();
        _previousButton.Pressed += Guarded(_dojo, () => Step(-1));
        _nextButton = new Button();
        _nextButton.Pressed += Guarded(_dojo, () => Step(1));

        HBoxContainer browseRow = new();
        browseRow.AddThemeConstantOverride("separation", 10);
        _placeLabel = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        browseRow.AddChild(_placeLabel);
        browseRow.AddChild(UiKit.Browse(_previousButton, _nextButton, string.Empty));
        panel.AddChild(browseRow);

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
        _renameButton.Pressed += Guarded(_dojo, ApplyRename);
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

        // The feast is the roster's lever, not one warrior's, so it sits with the summary's business
        // rather than in a man's detail: one measure of sake per living head, and then a week's wait.
        _feastButton = new Button { Text = "Hold a feast" };
        _feastButton.Pressed += Guarded(_dojo, Feast);
        panel.AddChild(_feastButton);

        _feastNotice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.AddChild(_feastNotice);

        // The charms sit under the path because they are the other thing carried onto the field, and
        // unlike the path they can be moved from one man to another on any day (docs/GDD.md §10).
        _charmLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.AddChild(_charmLabel);

        _charmRows = new VBoxContainer();
        _charmRows.AddThemeConstantOverride("separation", 4);
        panel.AddChild(_charmRows);

        // Releasing a man is the one thing on this screen that cannot be undone and costs nothing to
        // press, so it asks twice — the same courtesy the rest of the dojo owes an irreversible move.
        _releaseButton = UiKit.Danger(new Button { Text = "End his term" });
        _releaseButton.Pressed += Guarded(_dojo, Release);
        panel.AddChild(_releaseButton);

        _releaseNotice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.AddChild(_releaseNotice);

        // Retirement is the other irreversible move, and it asks twice for the same reason releasing
        // does: what the dojo gets back is a man who eats nothing and can hold a post, and what it
        // loses is a sword it cannot have back.
        _retireButton = UiKit.Danger(new Button { Text = "Retire him" });
        _retireButton.Pressed += Guarded(_dojo, Retire);
        panel.AddChild(_retireButton);

        _retireNotice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.AddChild(_retireNotice);

        _postRow = new HBoxContainer();
        _postRow.AddThemeConstantOverride("separation", 6);
        panel.AddChild(_postRow);

        return panel;
    }

    /// <summary>Reprints the roster and the selected warrior's detail.</summary>
    public override void Refresh()
    {
        Clear(_list);

        IReadOnlyList<RosterRow> rows = RosterModel.Describe(_dojo);
        _selected ??= rows.Count > 0 ? rows[0].Id : null;

        foreach (RosterRow row in rows)
        {
            // The same card the party list and the arena HUD print. Outside a fight there is no current
            // health to read — a man is either on the mat or in the infirmary — so the bar carries his
            // spirits, which is the number that decides how he fights today.
            Button button = UiKit.UnitButton(
                row.Name,
                PathName(row.Path),
                MoraleScale.Clamp(row.Morale) / MoraleScale.Max,
                RowText(row),
                bar: StatusColor(row.Status),
                ours: row.IsAlive && row.Status != RosterStatus.Freed,
                selected: row.Id == _selected,
                nameColor: row.IsAlive ? null : StatusColor(row.Status));

            WarriorId id = row.Id;
            button.Pressed += Guarded(_dojo, () =>
            {
                // Moving to another man disarms the release: the confirmation belongs to the warrior it
                // was armed for, not to the button.
                _selected = id;
                _releaseArmed = false;
                Refresh();
            });

            _list.AddChild(button);
        }

        RosterSummary summary = RosterModel.Summarize(_dojo);
        _summary.Text =
            $"Day {_dojo.Day}  ·  Roster {summary.Living}/{summary.Beds}  ·  Ready {summary.Fit}" +
            $"  ·  Infirmary {summary.Recovering}  ·  Dead {summary.Fallen}" +
            (summary.Freed > 0 ? $"  ·  Walked out {summary.Freed}" : string.Empty) +
            $"  ·  Party of at most {summary.PartyCapacity}" +
            $"  ·  Spirits {BandName(RosterModel.Band(summary.Morale))} ({summary.Morale:0})";

        UpdateFeastControls(summary);

        int place = _selected is WarriorId shown ? IndexOf(rows, shown) : -1;
        _previousButton.Disabled = place <= 0;
        _nextButton.Disabled = place < 0 || place >= rows.Count - 1;
        _placeLabel.Text = place < 0 ? string.Empty : $"{place + 1} of {rows.Count}";

        ShowDetail(rows.FirstOrDefault(r => r.Id == _selected));
    }

    /// <summary>Moves the selection one place along the list the screen is showing.</summary>
    /// <remarks>
    /// It walks <see cref="RosterModel.Describe"/>'s order rather than the roster's own, so the arrows
    /// follow what the player can see: the dead sort to the bottom and the arrows reach them last.
    /// </remarks>
    private void Step(int by)
    {
        IReadOnlyList<RosterRow> rows = RosterModel.Describe(_dojo);
        if (rows.Count == 0 || _selected is not WarriorId id)
        {
            return;
        }

        int place = IndexOf(rows, id);
        if (place < 0)
        {
            return;
        }

        int moved = Math.Clamp(place + by, 0, rows.Count - 1);
        if (moved == place)
        {
            return;
        }

        // Moving off a man disarms both confirmations for the same reason picking another row does: the
        // second press belongs to the man it was armed for.
        _selected = rows[moved].Id;
        _releaseArmed = false;
        _retireArmed = false;
        Refresh();
    }

    private static int IndexOf(IReadOnlyList<RosterRow> rows, WarriorId id)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id == id)
            {
                return i;
            }
        }

        return -1;
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
                ? $"Honour {row.Honor:0}  ·  Spirits {BandName(RosterModel.Band(row.Morale))}"
                  + $" ({row.Morale:0})  ·  Training days {row.TrainingDays}"
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
        UpdateRetirementControls(row);
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
            off.Pressed += Guarded(_dojo, () =>
            {
                _dojo.UnfitCharm(row.Id, kind);
                Persist();
                Refresh();
            });

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
                // The blessing is on the row because the five are no longer the same size: the charms
                // cost the same and give 4 to 40 points, so a row without it would hide the whole
                // decision behind five identical price tags.
                Text = $"{charm.Name}  ·  +{charm.Bonus:0.#} {StatOf(charm.Kind)}  ·  in store {held}",
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
            buy.Pressed += Guarded(_dojo, () =>
            {
                _dojo.BuyCharm(kind);
                Persist();
                Refresh();
            });
            line.AddChild(buy);

            Button fit = new() { Text = "Fit", Disabled = !room || held == 0 };
            fit.Pressed += Guarded(_dojo, () =>
            {
                _dojo.FitCharm(row.Id, kind);
                Persist();
                Refresh();
            });
            line.AddChild(fit);

            Button sell = new() { Text = "Sell back", Disabled = held == 0 };
            sell.Pressed += Guarded(_dojo, () =>
            {
                _dojo.SellCharm(kind);
                Persist();
                Refresh();
            });
            line.AddChild(sell);

            _charmRows.AddChild(line);
        }
    }

    /// <summary>
    /// What the feast would cost today, and why it cannot be held if it cannot.
    /// </summary>
    /// <remarks>
    /// The two refusals are different and must read differently: no sake is something the player can
    /// go and buy, the cooldown is something only the calendar answers (docs/GDD.md §3).
    /// </remarks>
    private void UpdateFeastControls(RosterSummary summary)
    {
        _feastButton.Disabled = !summary.CanFeast;
        _feastButton.Text = $"Hold a feast ({summary.FeastSake} sake)";

        _feastNotice.Text = summary.CanFeast
            ? $"Sake in store: {summary.Sake}"
            : summary.DaysToFeast > 0
                ? $"The last feast was too recent — {summary.DaysToFeast} days."
                : $"Sake in store: {summary.Sake}; a feast wants {summary.FeastSake}.";

        _feastNotice.AddThemeColorOverride("font_color", summary.CanFeast ? InkColor : MutedColor);
    }

    private void Feast()
    {
        if (_dojo.Feast())
        {
            Persist();
        }

        Refresh();
    }

    private static string BandName(MoraleBandName band) => band switch
    {
        MoraleBandName.Broken => "broken",
        MoraleBandName.Low => "low",
        MoraleBandName.Good => "good",
        MoraleBandName.High => "high",
        _ => "steady",
    };

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
            button.Pressed += Guarded(_dojo, () =>
            {
                _dojo.ChoosePath(id, chosen);
                Persist();
                Refresh();
            });
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

    /// <summary>
    /// The retirement button, and the posts a master of the house can be put into.
    /// </summary>
    /// <remarks>
    /// The posts are listed only for a man who has already retired: the three trades that need an
    /// outsider (a physician, a cook, a diviner) are left out of the list entirely rather than shown
    /// disabled, because they are not something he is bad at — they are not his to take.
    /// </remarks>
    private void UpdateRetirementControls(RosterRow row)
    {
        Clear(_postRow);

        bool master = row.Status == RosterStatus.Master;
        _retireButton.Visible = row.IsAlive && row.Status != RosterStatus.Freed && !master;
        _retireButton.Disabled = !row.CanRetire;
        _retireButton.Text = _retireArmed
            ? $"Take {row.Name} off the field — for good"
            : "Retire him";

        _retireNotice.Text = master
            ? row.Post is StaffRole post
                ? $"A master of the house — {SchoolModel.RoleName(post)}, for no wage."
                : "A master of the house. He eats nothing and can hold a post."
            : row.CanRetire
                ? _retireArmed
                    ? "He never takes the field again. He draws no wage and eats nothing, "
                      + "and the posts he was trained for are his."
                    : $"{row.Victories} fights behind him."
                : $"{row.Victories} fights behind him — not a career yet.";

        _retireNotice.AddThemeColorOverride("font_color", master ? GoodColor : PendingColor);

        if (!master)
        {
            return;
        }

        foreach (PostRow open in SchoolModel.Posts(_dojo))
        {
            if (!open.Standing || !open.MastersMayHold || (open.Filled && open.Role != row.Post))
            {
                continue;
            }

            StaffRole role = open.Role;
            bool his = open.Role == row.Post;
            Button button = new() { Text = his ? $"Leave the {open.Building.ToLowerInvariant()}" : open.Name };
            button.Pressed += Guarded(_dojo, () =>
            {
                if (his)
                {
                    _dojo.Dismiss(role);
                }
                else
                {
                    _dojo.Appoint(row.Id, role);
                }

                Persist();
                Refresh();
            });

            _postRow.AddChild(button);
        }
    }

    /// <summary>Takes the selected warrior off the field — the second press is the one that does it.</summary>
    private void Retire()
    {
        if (_selected is not WarriorId id)
        {
            return;
        }

        if (!_retireArmed)
        {
            _retireArmed = true;
            Refresh();
            return;
        }

        _retireArmed = false;
        if (_dojo.Retire(id))
        {
            Persist();
        }

        Refresh();
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

    /// <summary>The line under a man's bar on his card.</summary>
    /// <remarks>
    /// It no longer repeats his name: the card prints that as its heading, and printing it twice was
    /// what made the list read as a wall once the rows grew past one line.
    /// </remarks>
    private static string RowText(RosterRow row) => row.Status switch
    {
        RosterStatus.Recovering => $"Infirmary — {row.RecoveryDaysRemaining}d  ·  spirits {row.Morale:0}",
        RosterStatus.Fallen => "Dead",
        RosterStatus.Freed => "Walked out free",
        RosterStatus.Master => $"Master of the house  ·  {row.Victories} won",
        RosterStatus.Training => $"{DrillName(row.Drill)}  ·  spirits {row.Morale:0}",
        _ => $"Ready  ·  spirits {row.Morale:0}",
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
        RosterStatus.Master => "master of the house",
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

    /// <summary>The stat a charm blesses, for the temple's stall.</summary>
    private static string StatOf(OmamoriKind kind) => kind switch
    {
        OmamoriKind.SteadyHand => "accuracy",
        OmamoriKind.IronGate => "defence",
        OmamoriKind.LongBreath => "stamina",
        OmamoriKind.QuietMind => "will",
        OmamoriKind.SwiftFoot => "evasion",
        _ => "—",
    };
}
