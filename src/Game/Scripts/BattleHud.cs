using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The combat interface: health/stamina bars and the <b>surrender key</b>.
/// </summary>
/// <remarks>
/// What it says is decided by <see cref="HudModel"/> (engine-free, tested); the job here is building
/// the nodes and printing the text. Why surrendering is a single key and why the key shows the number
/// of locked warriors is written there.
/// </remarks>
public sealed partial class BattleHud : CanvasLayer
{
    private static readonly Color HealthColor = new(0.72f, 0.25f, 0.25f);
    private static readonly Color StaminaColor = new(0.78f, 0.70f, 0.32f);
    private static readonly Color LockedColor = new(0.85f, 0.55f, 0.20f);

    /// <summary>The colour of the key pressed before the fight starts: pressable but dim.</summary>
    private static readonly Color ShutColor = new(0.45f, 0.45f, 0.48f);

    private readonly Dictionary<WarriorId, WarriorPanel> _panels = [];
    private Label _status = null!;
    private Label _notice = null!;
    private Button _retreat = null!;
    private long _seed;

    /// <summary>The last refused-press count processed — so the same press is not answered twice.</summary>
    private int _refusalsSeen;

    /// <summary>
    /// How many times the text that teaches the rule has been shown so far.
    /// </summary>
    /// <remarks>
    /// Its persistent form is the save's job; here it is kept for the session. When the save layer
    /// arrives this field should be filled from there, or the rule is explained again at every launch.
    /// </remarks>
    private int _teachingShown;

    /// <summary>Builds the interface.</summary>
    /// <param name="battle">The fight to show.</param>
    /// <param name="setup">The roster the names are read from.</param>
    /// <param name="seed">The seed shown in the header — it makes reopening a fight possible.</param>
    /// <param name="onRetreat">Called when the "pull out" key is pressed — the whole party pulls out.</param>
    public void Build(Battle battle, BattleSetup setup, long seed, Action onRetreat)
    {
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(onRetreat);

        _seed = seed;

        // The name comes from the roster; it must be showable before there is any fight result.
        Dictionary<WarriorId, string> names = [];
        foreach (Warrior warrior in setup.PlayerSide.Concat(setup.EnemySide))
        {
            names[warrior.Id] = warrior.Name;
        }

        // The interface is anchored rather than placed at fixed coordinates: the enemy column used to
        // sit at x=1560, which puts it off the side of any window that is not the one it was written on.
        MarginContainer frame = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        frame.AddThemeConstantOverride("margin_left", 20);
        frame.AddThemeConstantOverride("margin_right", 20);
        frame.AddThemeConstantOverride("margin_top", 16);
        frame.AddThemeConstantOverride("margin_bottom", 16);
        frame.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(frame);

        // Everything that is only a container is transparent to the mouse: the interface covers the
        // window, and the fight is behind it.
        VBoxContainer page = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        page.AddThemeConstantOverride("separation", 10);
        frame.AddChild(page);

        page.AddChild(BuildStatusBar());

        // The two sides stand on the two edges of the window with the fight itself between them, so
        // whose bar is whose never has to be worked out from the names.
        HBoxContainer sides = new()
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        page.AddChild(sides);

        VBoxContainer player = Column(sides, "Your men", Control.SizeFlags.ShrinkBegin);

        Control gap = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        sides.AddChild(gap);

        VBoxContainer enemy = Column(sides, "Against you", Control.SizeFlags.ShrinkEnd);

        foreach (CombatantSnapshot snapshot in battle.Snapshots())
        {
            bool isPlayer = snapshot.Team == Battle.PlayerTeam;
            string name = names.GetValueOrDefault(snapshot.Id, snapshot.Id.ToString());

            var panel = new WarriorPanel(name);
            (isPlayer ? player : enemy).AddChild(panel.Root);
            _panels[snapshot.Id] = panel;
        }

        _retreat = new Button { CustomMinimumSize = new Vector2(300, 46) };
        _retreat.AddThemeFontSizeOverride("font_size", 20);
        _retreat.Pressed += () => onRetreat();
        player.AddChild(_retreat);
    }

    /// <summary>The bar across the top: how the fight stands, and whatever the interface last refused.</summary>
    private Control BuildStatusBar()
    {
        PanelContainer panel = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(UiKit.Surface));

        VBoxContainer column = UiKit.Padded(panel, 14, 8);
        column.AddThemeConstantOverride("separation", 2);
        column.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        _status = new Label();
        _status.AddThemeFontSizeOverride("font_size", UiKit.FigureSize);
        _status.AddThemeColorOverride("font_color", UiKit.Ink);
        column.AddChild(_status);

        _notice = new Label();
        _notice.AddThemeFontSizeOverride("font_size", UiKit.BodySize);
        _notice.AddThemeColorOverride("font_color", ShutColor);
        column.AddChild(_notice);

        return panel;
    }

    /// <summary>Called every frame.</summary>
    public void Refresh(Battle battle)
    {
        ArgumentNullException.ThrowIfNull(battle);

        IReadOnlyList<CombatantSnapshot> snapshots = battle.Snapshots();

        foreach (CombatantSnapshot snapshot in snapshots)
        {
            if (_panels.TryGetValue(snapshot.Id, out WarriorPanel? panel))
            {
                panel.Refresh(snapshot);
            }
        }

        RetreatPrompt prompt = HudModel.DescribeRetreat(snapshots, battle.ContactMade);
        _retreat.Text = prompt.Text;
        _retreat.Disabled = !prompt.Enabled;

        if (prompt.Locked)
        {
            _retreat.AddThemeColorOverride("font_color", LockedColor);
        }
        else if (prompt.Shut)
        {
            _retreat.AddThemeColorOverride("font_color", ShutColor);
        }
        else
        {
            _retreat.RemoveThemeColorOverride("font_color");
        }

        ShowRefusalIfAny(battle);

        _status.Text = HudModel.DescribeStatus(_seed, battle.ElapsedSeconds, battle.Result?.Outcome);
    }

    /// <summary>
    /// Answers a key pressed before the fight starts.
    /// </summary>
    /// <remarks>
    /// The core does not produce the text: it gives the number of refused presses, and what is written
    /// is decided by <see cref="HudModel.DescribeRefusal"/>.
    /// </remarks>
    private void ShowRefusalIfAny(Battle battle)
    {
        if (battle.RefusedRetreatPresses <= _refusalsSeen)
        {
            return;
        }

        _refusalsSeen = battle.RefusedRetreatPresses;

        RetreatRefusalNotice notice = HudModel.DescribeRefusal(_refusalsSeen, _teachingShown);
        _notice.Text = notice.Text;

        if (notice.Kind == RetreatNoticeKind.Teaching)
        {
            _teachingShown++;
        }
    }

    /// <summary>One side's column of bars, under a heading saying whose side it is.</summary>
    private static VBoxContainer Column(Control parent, string title, Control.SizeFlags vertical)
    {
        PanelContainer panel = new()
        {
            SizeFlagsVertical = vertical,
            CustomMinimumSize = new Vector2(330, 0),
        };
        panel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(UiKit.Surface));
        parent.AddChild(panel);

        VBoxContainer column = UiKit.Padded(panel, 12, 10);
        column.AddThemeConstantOverride("separation", 12);
        column.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        column.AddChild(UiKit.SectionLabel(title));

        return column;
    }

    /// <summary>A single warrior's interface row.</summary>
    private sealed class WarriorPanel
    {
        private readonly Label _name;
        private readonly ProgressBar _health;
        private readonly ProgressBar _stamina;
        private readonly string _warriorName;

        public WarriorPanel(string name)
        {
            _warriorName = name;

            Root = new VBoxContainer();
            Root.AddThemeConstantOverride("separation", 2);

            _name = new Label { Text = name };
            _name.AddThemeFontSizeOverride("font_size", 17);
            _name.AddThemeColorOverride("font_color", UiKit.Ink);
            Root.AddChild(_name);

            _health = Bar(HealthColor, 14);
            _stamina = Bar(StaminaColor, 7);
            Root.AddChild(_health);
            Root.AddChild(_stamina);
        }

        public VBoxContainer Root { get; }

        public void Refresh(in CombatantSnapshot snapshot)
        {
            _health.Value = snapshot.HealthFraction * 100;
            _stamina.Value = snapshot.StaminaFraction * 100;
            _name.Text = $"{_warriorName}  ·  {HudModel.DescribeState(snapshot)}";
        }

        private static ProgressBar Bar(Color color, int height)
        {
            var bar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(300, height),
            };

            StyleBoxFlat fill = UiKit.PanelStyle(color, radius: 2, border: color);
            StyleBoxFlat background = UiKit.PanelStyle(UiKit.Raised, radius: 2);

            bar.AddThemeStyleboxOverride("fill", fill);
            bar.AddThemeStyleboxOverride("background", background);

            return bar;
        }
    }
}
