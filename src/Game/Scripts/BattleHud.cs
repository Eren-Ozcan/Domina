using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// Dövüş arayüzü: can/stamina barları ve <b>pes etme tuşu</b>.
/// </summary>
/// <remarks>
/// Ne yazacağına <see cref="HudModel"/> karar verir (motorsuz, testli); buradaki iş
/// düğümleri kurup metni basmak. Pes etmenin neden tek tuş olduğu ve tuşun neden
/// kilitli savaşçı sayısını gösterdiği oraya yazılı.
/// </remarks>
public sealed partial class BattleHud : CanvasLayer
{
    private static readonly Color HealthColor = new(0.72f, 0.25f, 0.25f);
    private static readonly Color StaminaColor = new(0.78f, 0.70f, 0.32f);
    private static readonly Color LockedColor = new(0.85f, 0.55f, 0.20f);

    private readonly Dictionary<WarriorId, WarriorPanel> _panels = [];
    private Label _status = null!;
    private Button _retreat = null!;
    private long _seed;

    /// <summary>Arayüzü kurar.</summary>
    /// <param name="battle">Gösterilecek dövüş.</param>
    /// <param name="setup">İsimlerin okunacağı kadro.</param>
    /// <param name="seed">Başlıkta gösterilen seed — bir dövüşü tekrar açmayı sağlar.</param>
    /// <param name="onRetreat">"Çek" tuşuna basıldığında çağrılır — tüm ekip çekilir.</param>
    public void Build(Battle battle, BattleSetup setup, long seed, Action onRetreat)
    {
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(onRetreat);

        _seed = seed;

        // İsim kadrodan gelir; dövüş sonucu daha yokken de gösterilebilmeli.
        Dictionary<WarriorId, string> names = [];
        foreach (Warrior warrior in setup.PlayerSide.Concat(setup.EnemySide))
        {
            names[warrior.Id] = warrior.Name;
        }

        _status = new Label { Position = new Vector2(24, 20) };
        _status.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_status);

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

    /// <summary>Her karede çağrılır.</summary>
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

        RetreatPrompt prompt = HudModel.DescribeRetreat(snapshots);
        _retreat.Text = prompt.Text;
        _retreat.Disabled = !prompt.Enabled;

        if (prompt.Locked)
        {
            _retreat.AddThemeColorOverride("font_color", LockedColor);
        }
        else
        {
            _retreat.RemoveThemeColorOverride("font_color");
        }

        _status.Text = HudModel.DescribeStatus(_seed, battle.ElapsedSeconds, battle.Result?.Outcome);
    }

    private VBoxContainer Column(Vector2 position)
    {
        var column = new VBoxContainer { Position = position, CustomMinimumSize = new Vector2(330, 0) };
        column.AddThemeConstantOverride("separation", 12);
        AddChild(column);
        return column;
    }

    /// <summary>Tek bir savaşçının arayüz satırı.</summary>
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
