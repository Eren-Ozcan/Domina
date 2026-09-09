using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The school screen: three branches, nine facilities, paid up front (GDD §10).
/// </summary>
/// <remarks>
/// <para>
/// Which node is closed and why is decided by <see cref="SchoolModel"/> (engine-free,
/// tested); a purchase goes through <see cref="DojoState.BuySchoolNode"/> — that is the side that deducts
/// the price and applies the bonus to the settings.
/// </para>
/// <para>
/// A locked node is <b>not hidden</b>: the school is a long-term investment, and the player cannot
/// save toward something without seeing what he is saving for.
/// </para>
/// </remarks>
public sealed partial class SchoolScreen : DojoScreen
{
    private DojoState _dojo = null!;
    private HBoxContainer _columns = null!;
    private Label _summary = null!;
    private Label _detail = null!;
    private Button _buyButton = null!;
    private Label _notice = null!;
    private SchoolNodeId? _selected;

    /// <summary>Builds the screen and prints the tree.</summary>
    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

        VBoxContainer page = BuildPage();

        _summary = new Label();
        page.AddChild(_summary);

        _columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _columns.AddThemeConstantOverride("separation", 24);
        page.AddChild(_columns);

        page.AddChild(BuildDetailPanel());

        Refresh();
    }

    private Control BuildDetailPanel()
    {
        VBoxContainer panel = new() { CustomMinimumSize = new Vector2(0, 160) };
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

    /// <summary>Reprints the tree and the selected node's detail.</summary>
    public void Refresh()
    {
        Clear(_columns);

        IReadOnlyList<SchoolBranchColumn> columns = SchoolModel.Describe(_dojo);
        _selected ??= columns[0].Nodes[0].Id;

        foreach (SchoolBranchColumn column in columns)
        {
            VBoxContainer box = new() { CustomMinimumSize = new Vector2(280, 0) };
            box.AddThemeConstantOverride("separation", 8);

            box.AddChild(new Label
            {
                Text = $"{BranchName(column.Branch)}  ({column.Owned}/{column.Nodes.Count})",
            });

            foreach (SchoolNodeRow node in column.Nodes)
            {
                Button button = new()
                {
                    Text = NodeText(node),
                    Alignment = HorizontalAlignment.Left,
                    ToggleMode = true,
                    ButtonPressed = node.Id == _selected,
                };
                button.AddThemeColorOverride("font_color", StateColor(node.State));

                SchoolNodeId id = node.Id;
                button.Pressed += () =>
                {
                    _selected = id;
                    Refresh();
                };

                box.AddChild(button);
            }

            _columns.AddChild(box);
        }

        SchoolSummary summary = SchoolModel.Summarize(_dojo);
        _summary.Text =
            $"Day {_dojo.Day}  ·  Purse {summary.Gold} gold" +
            $"  ·  Facilities {summary.Owned}/{summary.Total}" +
            $"  ·  Affordable today {summary.Affordable}" +
            (summary.NextCost is int next ? $"  ·  Next cost {next} gold" : "  ·  The tree is complete");

        ShowDetail(columns.SelectMany(c => c.Nodes).Single(n => n.Id == _selected));
    }

    private void ShowDetail(SchoolNodeRow node)
    {
        _detail.Text = string.Join(
            '\n',
            $"{node.Name}  —  {node.Cost} gold  ·  {BranchName(node.Branch)} branch, tier {node.Tier}",
            Effect(node.Id),
            StateText(node));

        _buyButton.Disabled = node.State != SchoolNodeState.Affordable;
        _buyButton.Text = node.State == SchoolNodeState.Owned
            ? "Bought"
            : $"Buy ({node.Cost} gold)";
        _notice.Text = node.State == SchoolNodeState.TooExpensive
            ? $"The purse is short: {node.GoldShort} gold missing."
            : string.Empty;
    }

    private void Buy()
    {
        if (_selected is SchoolNodeId id && !_dojo.BuySchoolNode(id))
        {
            _notice.Text = "This facility cannot be bought now.";
            return;
        }

        Persist();
        Refresh();
    }

    private static string NodeText(SchoolNodeRow node) => node.State switch
    {
        SchoolNodeState.Owned => $"{node.Name}  —  bought",
        SchoolNodeState.Locked => $"{node.Name}  —  locked",
        SchoolNodeState.TooExpensive => $"{node.Name}  —  {node.Cost} gold (not enough)",
        _ => $"{node.Name}  —  {node.Cost} gold",
    };

    private static string StateText(SchoolNodeRow node) => node.State switch
    {
        SchoolNodeState.Owned => "This facility is bought; its bonus works every day.",
        SchoolNodeState.Locked => $"{Required(node)} must be bought first.",
        SchoolNodeState.TooExpensive => "Its turn has come; the purse is short.",
        _ => "Affordable today. A facility is paid up front and cannot be sold back.",
    };

    private static string Required(SchoolNodeRow node) =>
        node.Requires is SchoolNodeId id ? SchoolTree.Find(id).Name : "the previous tier";

    /// <summary>What the node sells — in one sentence.</summary>
    /// <remarks>
    /// The text stands on screen and the number lives in <see cref="SchoolTuning"/>: the size of the bonuses
    /// is a balance number and changes with measurement; the sentence does not.
    /// </remarks>
    private static string Effect(SchoolNodeId id) => id switch
    {
        SchoolNodeId.TrainingGround => "A training day gains more.",
        SchoolNodeId.FormsMaster => "The stat ceiling a warrior can approach rises.",
        SchoolNodeId.InnerDojo => "Training speeds up a second time.",
        SchoolNodeId.Infirmary => "Natural healing takes off one more day.",
        SchoolNodeId.Herbalist => "Medicine takes off one more day.",
        SchoolNodeId.BoneSetter => "More damage counts as a scratch — fewer infirmary days.",
        SchoolNodeId.Steward => "Daily food, water and medicine get cheaper.",
        SchoolNodeId.Patron => "Victory pays more.",
        _ => "Hiring warriors and repairing armour get cheaper.",
    };

    private static string BranchName(SchoolBranch branch) => branch switch
    {
        SchoolBranch.Training => "Training ground",
        SchoolBranch.Infirmary => "Infirmary",
        _ => "Steward",
    };

    private static Color StateColor(SchoolNodeState state) => state switch
    {
        SchoolNodeState.Owned => GoodColor,
        SchoolNodeState.Affordable => InkColor,
        SchoolNodeState.TooExpensive => PendingColor,
        _ => MutedColor,
    };
}
