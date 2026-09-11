using Domina.Core.Dojo;

namespace Domina.Presentation.Tests;

/// <summary>
/// The school screen's model. Three decisions are protected: the tree's shape does not change with the
/// dojo's state, "its turn has not come" and "I cannot afford it" are two separate states, and a locked
/// node is not hidden — the player must see what he is saving for.
/// </summary>
public class SchoolModelTests
{
    private static DojoState Funded(int gold)
    {
        // Construction instant: these tests are about how the screen reads the tree, and the
        // "going up" state has its own test.
        DojoState state = new(school: new SchoolTuning { BuildDaysFactor = 0 });
        state.Resources = new Resources(Gold: gold);
        return state;
    }

    /// <summary>A building that has been paid for but is not standing reads as its own state.</summary>
    [Fact]
    public void ABuildingGoingUpIsNeitherOwnedNorForSale()
    {
        DojoState dojo = new(school: new SchoolTuning()) { Resources = new Resources(Gold: 5000) };

        Assert.True(dojo.BuySchoolNode(SchoolNodeId.TrainingGround));

        SchoolNodeRow row = Row(dojo, SchoolNodeId.TrainingGround);
        Assert.Equal(SchoolNodeState.Building, row.State);
        Assert.Equal(SchoolTree.Find(SchoolNodeId.TrainingGround).BuildDays, row.DaysLeft);
        Assert.Equal(1, SchoolModel.Summarize(dojo).Building);
    }

    /// <summary>The screen must be able to say that a standing building has nobody in it.</summary>
    [Fact]
    public void ARowSaysWhetherItsPostIsFilled()
    {
        DojoState dojo = Funded(gold: 5000);
        Assert.True(dojo.BuySchoolNode(SchoolNodeId.TrainingGround));

        Assert.False(Row(dojo, SchoolNodeId.TrainingGround).Staffed);

        Assert.True(dojo.Hire(StaffRole.DrillMaster));

        Assert.True(Row(dojo, SchoolNodeId.TrainingGround).Staffed);
        Assert.Equal(1, SchoolModel.Summarize(dojo).Staffed);
        Assert.Equal(dojo.StaffTuning.WageOf(StaffRole.DrillMaster), SchoolModel.Summarize(dojo).DailyWage);
    }

    [Fact]
    public void EveryNodeIsListedWhateverTheDojoOwns()
    {
        DojoState dojo = Funded(gold: 0);

        IReadOnlyList<SchoolBranchColumn> columns = SchoolModel.Describe(dojo);

        Assert.Equal(Enum.GetValues<SchoolBranch>().Length, columns.Count);
        Assert.Equal(SchoolTree.All.Count, columns.Sum(c => c.Nodes.Count));
    }

    /// <summary>The order is the catalogue's order; a bought node is not moved to the top of the list.</summary>
    [Fact]
    public void BuyingDoesNotReshuffleTheTree()
    {
        DojoState dojo = Funded(gold: 5000);
        List<SchoolNodeId> before = [.. SchoolModel.Describe(dojo).SelectMany(c => c.Nodes).Select(n => n.Id)];

        Assert.True(dojo.BuySchoolNode(SchoolNodeId.Infirmary));

        Assert.Equal(before, SchoolModel.Describe(dojo).SelectMany(c => c.Nodes).Select(n => n.Id));
    }

    [Fact]
    public void WaitingForGoldAndWaitingForTheTierAreDifferentStates()
    {
        DojoState dojo = Funded(gold: 0);

        SchoolNodeRow first = Row(dojo, SchoolNodeId.TrainingGround);
        SchoolNodeRow second = Row(dojo, SchoolNodeId.FormsMaster);

        Assert.Equal(SchoolNodeState.TooExpensive, first.State);
        Assert.Equal(200, first.GoldShort);
        Assert.Equal(SchoolNodeState.Locked, second.State);
        Assert.Equal(0, second.GoldShort);
        Assert.Equal(SchoolNodeId.TrainingGround, second.Requires);
    }

    [Fact]
    public void APaidNodeReadsAsOwnedAndOpensTheNextTier()
    {
        DojoState dojo = Funded(gold: 200);

        Assert.True(dojo.BuySchoolNode(SchoolNodeId.TrainingGround));

        Assert.Equal(SchoolNodeState.Owned, Row(dojo, SchoolNodeId.TrainingGround).State);
        Assert.Equal(SchoolNodeState.TooExpensive, Row(dojo, SchoolNodeId.FormsMaster).State);

        dojo.Resources = dojo.Resources with { Gold = 400 };
        Assert.Equal(SchoolNodeState.Affordable, Row(dojo, SchoolNodeId.FormsMaster).State);
    }

    [Fact]
    public void TiersAreNumberedFromTheFootOfTheBranch()
    {
        DojoState dojo = Funded(gold: 0);

        Assert.Equal(1, Row(dojo, SchoolNodeId.Steward).Tier);
        Assert.Equal(2, Row(dojo, SchoolNodeId.Patron).Tier);
        Assert.Equal(3, Row(dojo, SchoolNodeId.Broker).Tier);
    }

    [Fact]
    public void TheSummaryCountsWhatTodayCanBuy()
    {
        DojoState dojo = Funded(gold: 200);

        SchoolSummary summary = SchoolModel.Summarize(dojo);

        Assert.Equal(0, summary.Owned);
        Assert.Equal(SchoolTree.All.Count, summary.Total);

        // Every branch foot at or under today's purse — the support buildings are the cheap ones.
        Assert.Equal(
            SchoolTree.All.Count(n => n.Requires is null && n.Cost <= 200),
            summary.Affordable);
        Assert.Equal(SchoolTree.All.Where(n => n.Requires is null).Min(n => n.Cost), summary.NextCost);
    }

    /// <summary>When the tree is finished there is no such thing as "the next cost".</summary>
    [Fact]
    public void AFinishedTreeHasNoNextCost()
    {
        DojoState dojo = Funded(gold: 10_000);
        foreach (SchoolNode node in SchoolTree.All)
        {
            Assert.True(dojo.BuySchoolNode(node.Id));
        }

        SchoolSummary summary = SchoolModel.Summarize(dojo);

        Assert.Equal(SchoolTree.All.Count, summary.Owned);
        Assert.Equal(0, summary.Affordable);
        Assert.Null(summary.NextCost);
    }

    private static SchoolNodeRow Row(DojoState dojo, SchoolNodeId id) =>
        SchoolModel.Describe(dojo).SelectMany(c => c.Nodes).Single(n => n.Id == id);
}
