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

        _status = new Label { Position = new Vector2(24, 20) };
        _status.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_status);

        _notice = new Label { Position = new Vector2(24, 48) };
        _notice.AddThemeFontSizeOverride("font_size", 18);
        _notice.AddThemeColorOverride("font_color", ShutColor);
        AddChild(_notice);

        var player = Column(new Vector2(24, 60));
        var enemy = Column(new Vector2(1560, 60));

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

    private VBoxContainer Column(Vector2 position)
    {
        var column = new VBoxContainer { Position = position, CustomMinimumSize = new Vector2(330, 0) };
        column.AddThemeConstantOverride("separation", 12);
        AddChild(column);
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

            var fill = new StyleBoxFlat { BgColor = color };
            var background = new StyleBoxFlat { BgColor = new Color(0.16f, 0.15f, 0.15f) };

            bar.AddThemeStyleboxOverride("fill", fill);
            bar.AddThemeStyleboxOverride("background", background);

            return bar;
        }
    }
}
