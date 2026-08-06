using Domina.Core.Combat;
using Domina.Core.Model;
using Godot;

namespace Domina.Game;

/// <summary>
/// Dövüş arayüzü: can/stamina barları ve <b>pes etme tuşları</b>.
/// </summary>
/// <remarks>
/// <para>
/// Pes etme, dövüşteki tek müdahale noktası (GDD §5) ve <b>tek tuştur</b>: komut
/// ekibin tamamını çeker, savaşçı seçilmez. Savaşçı bazlı olsaydı doğru oynanış
/// "yara alanı çek, kalanla devam et" olurdu; tek tuş kararı nadir ve ağır yapar.
/// </para>
/// <para>
/// Tuş, komutun <b>kaç savaşçıda anında işleyeceğini</b> gösterir: vuruşa kilitli
/// savaşçıların komutu buffer'lanır ve kaçış ancak vuruş bitince başlar. Oyuncu bunu
/// basmadan önce görebilmeli, yoksa gecikme hata gibi hissedilir.
/// </para>
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

        int standing = 0;
        int locked = 0;
        int leaving = 0;

        foreach (CombatantSnapshot snapshot in battle.Snapshots())
        {
            if (_panels.TryGetValue(snapshot.Id, out WarriorPanel? panel))
            {
                panel.Refresh(snapshot);
            }

            if (snapshot.Team != Battle.PlayerTeam || !snapshot.IsActive)
            {
                continue;
            }

            if (snapshot.RetreatRequested || snapshot.State == CombatState.Retreating)
            {
                leaving++;
                continue;
            }

            standing++;
            if (!snapshot.CanCancel)
            {
                locked++;
            }
        }

        RefreshRetreatButton(standing, locked, leaving);

        _status.Text = battle.IsFinished
            ? $"seed {_seed}  ·  {battle.ElapsedSeconds:F1} sn  ·  {Describe(battle.Result!.Outcome)}"
            : $"seed {_seed}  ·  {battle.ElapsedSeconds:F1} sn";
    }

    /// <summary>
    /// Tek tuşun hali. Komut ekibi kapsadığı için tuş <b>kaç savaşçının</b> etkileneceğini
    /// ve kaçının vuruşa kilitli olduğunu söyler — kilitli olanların kaçışı vuruş bitince
    /// başlar, bu gecikme sürpriz olmamalı.
    /// </summary>
    private void RefreshRetreatButton(int standing, int locked, int leaving)
    {
        _retreat.Disabled = standing == 0;

        if (standing == 0)
        {
            _retreat.Text = leaving > 0 ? "EKİP ÇEKİLİYOR" : "—";
            _retreat.RemoveThemeColorOverride("font_color");
            return;
        }

        _retreat.Text = locked == 0
            ? $"EKİBİ ÇEK ({standing})"
            : $"EKİBİ ÇEK ({standing}) · {locked} kilitli";

        if (locked > 0)
        {
            _retreat.AddThemeColorOverride("font_color", LockedColor);
        }
        else
        {
            _retreat.RemoveThemeColorOverride("font_color");
        }
    }

    private static string Describe(BattleOutcome outcome) => outcome switch
    {
        BattleOutcome.PlayerVictory => "ZAFER",
        BattleOutcome.PlayerDefeat => "YENİLGİ",
        _ => "SÜRE DOLDU",
    };

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

        public void Refresh(CombatantSnapshot snapshot)
        {
            _health.Value = snapshot.HealthFraction * 100;
            _stamina.Value = snapshot.StaminaFraction * 100;
            _name.Text = $"{_warriorName}  ·  {StateLabel(snapshot.State)}";
        }

        private static string StateLabel(CombatState state) => state switch
        {
            CombatState.Idle => "bekliyor",
            CombatState.AttackWindup => "saldırıyor",
            CombatState.AttackRecovery => "toparlanıyor",
            CombatState.Retreating => "çekiliyor",
            CombatState.Escaped => "kurtuldu",
            CombatState.Dead => "öldü",
            _ => string.Empty,
        };

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
