using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The market screen: the candidates, their stats, the talent band and the price (GDD §10).
/// </summary>
/// <remarks>
/// <para>
/// What it says and the order of the rows are decided by <see cref="MarketModel"/> (engine-free,
/// tested); the job here is building the nodes and printing the text. A purchase goes through
/// <see cref="Quartermaster"/> — that is the side that deducts the price and writes the candidate onto the roster.
/// </para>
/// <para>
/// The candidate's stats are printed side by side with <b>the roster's best</b>: the market's real
/// question is not "is this candidate good" but "is he better than what I have". A number standing
/// alone does not answer that.
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

    /// <summary>Builds the screen and prints the stall.</summary>
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

        _buyButton = new Button { Text = "Buy" };
        _buyButton.Pressed += Buy;
        panel.AddChild(_buyButton);

        _notice = new Label();
        _notice.AddThemeColorOverride("font_color", WarningColor);
        panel.AddChild(_notice);

        return panel;
    }

    /// <summary>Reprints the stall and the selected candidate's detail.</summary>
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
            $"Day {_dojo.Day}  ·  Purse {summary.Gold} gold  ·  Candidates {summary.Candidates}" +
            $"  ·  Affordable {summary.Affordable}  ·  Bought today {summary.Bought}" +
            $"  ·  Stall refreshes in {summary.DaysToRefresh} days";

        ShowDetail(rows.FirstOrDefault(r => r.Index == _selected));
    }

    private void ShowDetail(MarketRow row)
    {
        if (row.Name is null)
        {
            _detail.Text = "Nobody at the stall today.";
            _buyButton.Disabled = true;
            _notice.Text = string.Empty;
            return;
        }

        WarriorStats stats = row.Stats;
        WarriorStats? best = BestLivingStats();

        _detail.Text = string.Join(
            '\n',
            $"{row.Name}  —  {row.Price} gold",
            $"Talent: {BandName(row.Band)}  ·  this multiplies what training gains",
            row.BetterInRoster == 0
                ? "Nobody on the roster is better."
                : $"{row.BetterInRoster} warriors on the roster are better.",
            string.Empty,
            best is null ? "The roster is empty — nothing to compare." : "Left column the candidate, right column the roster's best:",
            $"Health      {Pair(stats.MaxHealth, best?.MaxHealth)}",
            $"Aggression  {Pair(stats.Aggression, best?.Aggression)}",
            $"Defence     {Pair(stats.Defense, best?.Defense)}",
            $"Evasion     {Pair(stats.Evasion, best?.Evasion)}",
            $"Strength    {Pair(stats.Strength, best?.Strength)}",
            $"Accuracy    {Pair(stats.Accuracy, best?.Accuracy)}",
            $"Stamina     {Pair(stats.MaxStamina, best?.MaxStamina)}",
            $"Speed       {Pair(stats.Speed, best?.Speed)}");

        _buyButton.Disabled = row.Bought || !row.Affordable;
        _buyButton.Text = row.Bought ? "Bought" : $"Buy ({row.Price} gold)";
        _notice.Text = row.Bought || row.Affordable
            ? string.Empty
            : $"The purse is short: {row.Price - _dojo.Resources.Gold} gold missing.";
    }

    /// <summary>
    /// Buys the selected candidate.
    /// </summary>
    /// <remarks>
    /// The purchase goes through <see cref="DojoState.HireRecruit"/>: the side that records the bought candidate
    /// and the side that stops the same man being sold twice is the core. With the screen keeping its
    /// own mark, reloading the save and buying the same candidate again would stay open.
    /// </remarks>
    private void Buy()
    {
        if (_selected is not int index)
        {
            return;
        }

        if (_dojo.HireRecruit(index) is null)
        {
            _notice.Text = "This candidate cannot be bought now.";
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
        ? $"{row.Name}  —  bought"
        : $"{row.Name}  —  {row.Price} gold  ·  {BandName(row.Band)}";

    private static Color RowColor(MarketRow row)
    {
        if (row.Bought)
        {
            return GoodColor;
        }

        return row.Affordable ? InkColor : MutedColor;
    }

    /// <summary>The candidate's stat, next to the roster's best.</summary>
    private static string Pair(double candidate, double? best) =>
        best is null ? $"{candidate:0}" : $"{candidate:0}   ({best:0})";

    private static string BandName(TalentBand band) => band switch
    {
        TalentBand.Dull => "dull",
        TalentBand.Fair => "ordinary",
        TalentBand.Promising => "promising",
        _ => "rare",
    };
}
