using Domina.Core.Dojo;

namespace Domina.Presentation;

/// <summary>A school node's state today.</summary>
public enum SchoolNodeState
{
    /// <summary>Bought; its bonus is in effect.</summary>
    Owned,

    /// <summary>Paid for and going up — the days left are on the row.</summary>
    /// <remarks>
    /// A building under construction is its own state, not a dimmed "affordable": the gold has already
    /// gone, nothing more can be spent on it, and what the player is waiting for is the calendar.
    /// </remarks>
    Building,

    /// <summary>Its turn has come and there is enough money.</summary>
    Affordable,

    /// <summary>Its turn has come but the treasury is short.</summary>
    TooExpensive,

    /// <summary>The node before it has not been bought.</summary>
    Locked,
}

/// <summary>A single row in the tree.</summary>
/// <param name="Id">The node's identity; the screen's command returns it.</param>
/// <param name="Branch">The branch it belongs to.</param>
/// <param name="Name">Display name.</param>
/// <param name="Cost">The cost in gold.</param>
/// <param name="Tier">The tier within the branch (1 starts at the base).</param>
/// <param name="State">Its state today.</param>
/// <param name="Requires">The node that must be bought first; <c>null</c> for a branch's first.</param>
/// <param name="GoldShort">
/// The gold missing on a node that is unlocked but unaffordable; 0 in the other states.
/// </param>
/// <param name="DaysLeft">The construction days left on a building that is going up; 0 otherwise.</param>
/// <param name="Role">The post the building carries, if it has one.</param>
/// <param name="Staffed">Is that post filled today — a standing building with an empty post works at half.</param>
public readonly record struct SchoolNodeRow(
    SchoolNodeId Id,
    SchoolBranch Branch,
    string Name,
    int Cost,
    int Tier,
    SchoolNodeState State,
    SchoolNodeId? Requires,
    int GoldShort,
    int DaysLeft = 0,
    StaffRole? Role = null,
    bool Staffed = false);

/// <summary>One branch — its nodes in the order they are bought.</summary>
/// <param name="Branch">Kolun kendisi.</param>
/// <param name="Nodes">The nodes in tier order.</param>
/// <param name="Owned">The number of nodes bought from this branch.</param>
public readonly record struct SchoolBranchColumn(
    SchoolBranch Branch,
    IReadOnlyList<SchoolNodeRow> Nodes,
    int Owned);

/// <summary>The numbers standing at the top of the school.</summary>
/// <param name="Gold">The gold in the treasury.</param>
/// <param name="Owned">The number of nodes bought.</param>
/// <param name="Total">The total nodes in the tree.</param>
/// <param name="Affordable">The number of nodes that can be bought today.</param>
/// <param name="NextCost">
/// The cost of the cheapest node that can be bought today; <c>null</c> if no unlocked node is left.
/// </param>
/// <param name="Building">How many buildings are going up today.</param>
/// <param name="Staffed">How many posts are filled.</param>
/// <param name="DailyWage">What those posts cost the treasury every day.</param>
public readonly record struct SchoolSummary(
    int Gold,
    int Owned,
    int Total,
    int Affordable,
    int? NextCost,
    int Building = 0,
    int Staffed = 0,
    int DailyWage = 0);

/// <summary>
/// The model the school screen reads. It computes why a node is closed; it does not draw.
/// </summary>
/// <remarks>
/// A closed node has <b>two separate reasons</b> — its turn not having come and not being affordable —
/// and the screen cannot show both with the same dimmed button: one opens by waiting, the other by
/// earning. <see cref="School.Available"/> only knows the order, not the treasury; the distinction is
/// made here.
/// </remarks>
public static class SchoolModel
{
    /// <summary>The tree, branch by branch and cheapest to most expensive within a branch.</summary>
    /// <remarks>
    /// The order is <b>the catalogue's</b> order and does not change with the dojo's state: the shape of
    /// the tree must stay fixed in the player's head. If a bought node moved to the top of the list, or
    /// locked ones were hidden, the player could not see what he was saving toward.
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
                rows.Add(Describe(node, ++tier, dojo.School, dojo.Resources.Gold, dojo.Staff));
            }

            columns.Add(new SchoolBranchColumn(
                branch,
                rows,
                rows.Count(r => r.State == SchoolNodeState.Owned)));
        }

        return columns;
    }

    /// <summary>A single node's row.</summary>
    /// <param name="node">The node in the catalogue.</param>
    /// <param name="tier">The tier within the branch (starting at 1).</param>
    /// <param name="school">Dojo'nun okulu.</param>
    /// <param name="gold">The gold in the treasury.</param>
    public static SchoolNodeRow Describe(
        SchoolNode node,
        int tier,
        School school,
        int gold,
        Staff? staff = null)
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
            GoldShort: state == SchoolNodeState.TooExpensive ? node.Cost - gold : 0,
            DaysLeft: school.UnderConstruction.TryGetValue(node.Id, out int days) ? days : 0,
            Role: node.Role,
            Staffed: node.Role is StaffRole role && staff?.Has(role) == true);
    }

    /// <summary>The numbers at the top of the school.</summary>
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
            NextCost: open.Count == 0 ? null : open.Min(n => n.Cost),
            Building: dojo.School.UnderConstruction.Count,
            Staffed: dojo.Staff.Hired.Count,
            DailyWage: dojo.Staff.DailyWage(dojo.StaffTuning));
    }

    private static SchoolNodeState StateOf(SchoolNode node, School school, int gold)
    {
        if (school.Has(node.Id))
        {
            return SchoolNodeState.Owned;
        }

        if (school.IsBuilding(node.Id))
        {
            return SchoolNodeState.Building;
        }

        if (node.Requires is SchoolNodeId required && !school.Has(required))
        {
            return SchoolNodeState.Locked;
        }

        return node.Cost <= gold ? SchoolNodeState.Affordable : SchoolNodeState.TooExpensive;
    }
}
