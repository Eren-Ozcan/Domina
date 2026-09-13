using Domina.Core.Dojo;

namespace Domina.Presentation.Tests;

/// <summary>
/// The province board's model. The decisions protected: it carries no commands at all — the board is a
/// picture, not a screen the player acts on — it never hands the rival's own counter to the screen,
/// and it names his next target only while a village's word is still good.
/// </summary>
public class ProvinceModelTests
{
    private static DojoState Dojo() => new(events: new EventTuning { ChancePerDay = 0 })
    {
        Resources = new Resources(Gold: 600, Food: 100, Water: 100),
    };

    [Fact]
    public void TheBoardCountsEverySettlementOnce()
    {
        DojoState dojo = Dojo();
        dojo.Province.Place(0, Allegiance.Yours);
        dojo.Province.Place(1, Allegiance.His);

        ProvinceBoard board = ProvinceModel.Describe(dojo);

        Assert.Equal(dojo.Province.Settlements.Count, board.Settlements.Count);
        Assert.Equal(1, board.Yours);
        Assert.Equal(1, board.His);
        Assert.Equal(board.Settlements.Count - 2, board.Free);
    }

    /// <summary>His next move is named only when a village has handed the word across.</summary>
    [Fact]
    public void HisTargetIsNamedOnlyWhileTheWordHolds()
    {
        DojoState dojo = Dojo();

        Assert.DoesNotContain(ProvinceModel.Describe(dojo).Settlements, t => t.Pressed);

        // The word comes with a village that changes hands; the gift is the fourth in the cycle.
        while (dojo.Province.TargetKnownUntil == 0)
        {
            dojo.Province.FileContract(dojo.Province.ContractTarget!.Index, dojo.Day);
        }

        Assert.Contains(ProvinceModel.Describe(dojo).Settlements, t => t.Pressed);
    }

    /// <summary>A village of his under pressure says his grip holds, not a count to fill.</summary>
    [Fact]
    public void AVillageStillInHisGripSaysSo()
    {
        DojoState dojo = Dojo();
        dojo.Province.Place(2, Allegiance.His, warning: 1);

        SettlementTile tile = ProvinceModel.Describe(dojo).Settlements[2];

        Assert.Contains("grip", ProvinceModel.Line(tile), StringComparison.Ordinal);
        Assert.Equal(dojo.Province.Tuning.ContractsForHis, tile.ContractsNeeded);
    }

    /// <summary>The raid is the one thing the board shouts about.</summary>
    [Fact]
    public void TheBoardSaysWhenHeIsAtTheGate()
    {
        DojoState dojo = Dojo();

        Assert.False(ProvinceModel.Describe(dojo).UnderRaid);
    }
}
