using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// Okul ekranı: üç kol, dokuz tesis, peşin bedel (GDD §10).
/// </summary>
/// <remarks>
/// <para>
/// Hangi düğümün neden kapalı olduğuna <see cref="SchoolModel"/> karar verir (motorsuz,
/// testli); alım <see cref="DojoState.BuySchoolNode"/> üzerinden geçer — parayı kasadan
/// düşen ve bonusu ayarlara işleyen taraf orası.
/// </para>
/// <para>
/// Kilitli düğüm <b>gizlenmiyor</b>: okul uzun vadeli yatırım, oyuncu neye para
/// biriktirdiğini görmeden biriktiremez.
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

    /// <summary>Ekranı kurar ve ağacı basar.</summary>
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

        _buyButton = new Button { Text = "Satın al" };
        _buyButton.Pressed += Buy;
        panel.AddChild(_buyButton);

        _notice = new Label();
        _notice.AddThemeColorOverride("font_color", WarningColor);
        panel.AddChild(_notice);

        return panel;
    }

    /// <summary>Ağacı ve seçili düğümün ayrıntısını yeniden basar.</summary>
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
            $"Gün {_dojo.Day}  ·  Kasa {summary.Gold} altın" +
            $"  ·  Tesis {summary.Owned}/{summary.Total}" +
            $"  ·  Bugün alınabilir {summary.Affordable}" +
            (summary.NextCost is int next ? $"  ·  Sıradaki bedel {next} altın" : "  ·  Ağaç tamam");

        ShowDetail(columns.SelectMany(c => c.Nodes).Single(n => n.Id == _selected));
    }

    private void ShowDetail(SchoolNodeRow node)
    {
        _detail.Text = string.Join(
            '\n',
            $"{node.Name}  —  {node.Cost} altın  ·  {BranchName(node.Branch)} kolu, {node.Tier}. kademe",
            Effect(node.Id),
            StateText(node));

        _buyButton.Disabled = node.State != SchoolNodeState.Affordable;
        _buyButton.Text = node.State == SchoolNodeState.Owned
            ? "Alındı"
            : $"Satın al ({node.Cost} altın)";
        _notice.Text = node.State == SchoolNodeState.TooExpensive
            ? $"Kasa yetmiyor: {node.GoldShort} altın eksik."
            : string.Empty;
    }

    private void Buy()
    {
        if (_selected is SchoolNodeId id && !_dojo.BuySchoolNode(id))
        {
            _notice.Text = "Bu tesis şimdi alınamaz.";
            return;
        }

        Persist();
        Refresh();
    }

    private static string NodeText(SchoolNodeRow node) => node.State switch
    {
        SchoolNodeState.Owned => $"{node.Name}  —  alındı",
        SchoolNodeState.Locked => $"{node.Name}  —  kilitli",
        SchoolNodeState.TooExpensive => $"{node.Name}  —  {node.Cost} altın (yetmiyor)",
        _ => $"{node.Name}  —  {node.Cost} altın",
    };

    private static string StateText(SchoolNodeRow node) => node.State switch
    {
        SchoolNodeState.Owned => "Bu tesis alındı; bonusu her gün işliyor.",
        SchoolNodeState.Locked => $"Önce {Required(node)} alınmalı.",
        SchoolNodeState.TooExpensive => "Sırası geldi; kasa yetmiyor.",
        _ => "Bugün alınabilir. Tesis peşindir ve geri satılmaz.",
    };

    private static string Required(SchoolNodeRow node) =>
        node.Requires is SchoolNodeId id ? SchoolTree.Find(id).Name : "önceki kademe";

    /// <summary>Düğümün ne sattığı — bir cümleyle.</summary>
    /// <remarks>
    /// Metin ekranda duruyor, sayı <see cref="SchoolTuning"/>'de: bonusların büyüklüğü
    /// denge sayısıdır ve ölçümle değişir, cümle değişmez.
    /// </remarks>
    private static string Effect(SchoolNodeId id) => id switch
    {
        SchoolNodeId.TrainingGround => "Antrenman günü daha çok kazandırır.",
        SchoolNodeId.FormsMaster => "Savaşçının yaklaşabildiği stat tavanı yükselir.",
        SchoolNodeId.InnerDojo => "Antrenman ikinci kez hızlanır.",
        SchoolNodeId.Infirmary => "Doğal iyileşme günde bir gün daha erir.",
        SchoolNodeId.Herbalist => "İlaç bir gün daha eritir.",
        SchoolNodeId.BoneSetter => "Sıyrık sayılan hasar payı büyür — daha az revir günü.",
        SchoolNodeId.Steward => "Günlük yiyecek, su ve ilaç ucuzlar.",
        SchoolNodeId.Patron => "Zafer daha çok öder.",
        _ => "Savaşçı alımı ve zırh onarımı ucuzlar.",
    };

    private static string BranchName(SchoolBranch branch) => branch switch
    {
        SchoolBranch.Training => "Talimhane",
        SchoolBranch.Infirmary => "Revir",
        _ => "Kâhya",
    };

    private static Color StateColor(SchoolNodeState state) => state switch
    {
        SchoolNodeState.Owned => GoodColor,
        SchoolNodeState.Affordable => InkColor,
        SchoolNodeState.TooExpensive => PendingColor,
        _ => MutedColor,
    };
}
