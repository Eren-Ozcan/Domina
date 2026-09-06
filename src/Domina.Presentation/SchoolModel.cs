using Domina.Core.Dojo;

namespace Domina.Presentation;

/// <summary>Bir okul düğümünün bugünkü hâli.</summary>
public enum SchoolNodeState
{
    /// <summary>Alınmış; bonusu işliyor.</summary>
    Owned,

    /// <summary>Sırası geldi ve para yetiyor.</summary>
    Affordable,

    /// <summary>Sırası geldi ama kasa yetmiyor.</summary>
    TooExpensive,

    /// <summary>Önündeki düğüm alınmadı.</summary>
    Locked,
}

/// <summary>Ağaçtaki tek satır.</summary>
/// <param name="Id">Düğümün kimliği; ekran komutu bunu geri verir.</param>
/// <param name="Branch">Bağlı olduğu kol.</param>
/// <param name="Name">Görünen ad.</param>
/// <param name="Cost">Altın bedeli.</param>
/// <param name="Tier">Kol içindeki kademe (1 tabandan başlar).</param>
/// <param name="State">Bugünkü hâli.</param>
/// <param name="Requires">Önce alınması gereken düğüm; kolun ilkinde <c>null</c>.</param>
/// <param name="GoldShort">
/// Kilidi açık ama parası yetmeyen düğümde eksik altın; diğer hâllerde 0.
/// </param>
public readonly record struct SchoolNodeRow(
    SchoolNodeId Id,
    SchoolBranch Branch,
    string Name,
    int Cost,
    int Tier,
    SchoolNodeState State,
    SchoolNodeId? Requires,
    int GoldShort);

/// <summary>Bir kol — düğümleri alınma sırasıyla.</summary>
/// <param name="Branch">Kolun kendisi.</param>
/// <param name="Nodes">Kademe sırasıyla düğümler.</param>
/// <param name="Owned">Bu koldan alınmış düğüm sayısı.</param>
public readonly record struct SchoolBranchColumn(
    SchoolBranch Branch,
    IReadOnlyList<SchoolNodeRow> Nodes,
    int Owned);

/// <summary>Okulun tepesinde duran sayılar.</summary>
/// <param name="Gold">Kasadaki altın.</param>
/// <param name="Owned">Alınmış düğüm sayısı.</param>
/// <param name="Total">Ağaçtaki toplam düğüm.</param>
/// <param name="Affordable">Bugün satın alınabilecek düğüm sayısı.</param>
/// <param name="NextCost">
/// Bugün alınabilecek en ucuz düğümün bedeli; kilidi açık düğüm kalmadıysa <c>null</c>.
/// </param>
public readonly record struct SchoolSummary(
    int Gold,
    int Owned,
    int Total,
    int Affordable,
    int? NextCost);

/// <summary>
/// Okul ekranının okuduğu model. Hangi düğümün neden kapalı olduğunu hesaplar,
/// çizim yapmaz.
/// </summary>
/// <remarks>
/// Kapalı düğümün <b>iki ayrı sebebi</b> var — sırası gelmemiş olmak ve parasının
/// yetmemesi — ve ekran ikisini aynı sönük tuşla gösteremez: biri beklemekle,
/// diğeri kazanmakla açılır. <see cref="School.Available"/> yalnızca sırayı bilir,
/// kasayı bilmez; ayrımı burası yapar.
/// </remarks>
public static class SchoolModel
{
    /// <summary>Ağaç, kol kol ve kol içinde ucuzdan pahalıya.</summary>
    /// <remarks>
    /// Sıra <b>kataloğun</b> sırasıdır, dojo'nun durumuna göre değişmez: ağacın şekli
    /// oyuncunun kafasında sabit kalmalı. Alınan düğüm listenin başına taşınsaydı ya da
    /// kilitliler gizlenseydi, oyuncu neye doğru para biriktirdiğini göremezdi.
    /// </remarks>
    public static IReadOnlyList<SchoolBranchColumn> Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        List<SchoolBranchColumn> columns = [];
        foreach (SchoolBranch branch in Enum.GetValues<SchoolBranch>())
        {
            List<SchoolNodeRow> rows = [];
            int tier = 0;
            foreach (SchoolNode node in SchoolTree.Of(branch))
            {
                rows.Add(Describe(node, ++tier, dojo.School, dojo.Resources.Gold));
            }

            columns.Add(new SchoolBranchColumn(
                branch,
                rows,
                rows.Count(r => r.State == SchoolNodeState.Owned)));
        }

        return columns;
    }

    /// <summary>Tek düğümün satırı.</summary>
    /// <param name="node">Katalogdaki düğüm.</param>
    /// <param name="tier">Kol içindeki kademe (1'den başlar).</param>
    /// <param name="school">Dojo'nun okulu.</param>
    /// <param name="gold">Kasadaki altın.</param>
    public static SchoolNodeRow Describe(SchoolNode node, int tier, School school, int gold)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(school);

        SchoolNodeState state = StateOf(node, school, gold);

        return new SchoolNodeRow(
            Id: node.Id,
            Branch: node.Branch,
            Name: node.Name,
            Cost: node.Cost,
            Tier: tier,
            State: state,
            Requires: node.Requires,
            GoldShort: state == SchoolNodeState.TooExpensive ? node.Cost - gold : 0);
    }

    /// <summary>Okulun tepesindeki sayılar.</summary>
    public static SchoolSummary Summarize(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        List<SchoolNode> open = [.. dojo.School.Available()];
        int gold = dojo.Resources.Gold;

        return new SchoolSummary(
            Gold: gold,
            Owned: dojo.School.Owned.Count,
            Total: SchoolTree.All.Count,
            Affordable: open.Count(n => n.Cost <= gold),
            NextCost: open.Count == 0 ? null : open.Min(n => n.Cost));
    }

    private static SchoolNodeState StateOf(SchoolNode node, School school, int gold)
    {
        if (school.Has(node.Id))
        {
            return SchoolNodeState.Owned;
        }

        if (node.Requires is SchoolNodeId required && !school.Has(required))
        {
            return SchoolNodeState.Locked;
        }

        return node.Cost <= gold ? SchoolNodeState.Affordable : SchoolNodeState.TooExpensive;
    }
}
