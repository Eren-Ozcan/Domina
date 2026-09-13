using Domina.Core.Dojo;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The province — twelve settlements and the rival's one stored number (docs/GDD.md §10, Open
/// Decision #17). The decisions protected here: a settlement needs three uncontested moves to fall,
/// the player's are pressed before the free ones, an answer is worth one move and no more, the
/// settlements never pay gold, and a spent bound is what opens the raid.
/// </summary>
public class ProvinceTests
{
    private static Province Empty() => new(new ProvinceTuning { Deniability = 99 });

    /// <summary>Pressed, pressed again, taken — never sooner.</summary>
    [Fact]
    public void ASettlementFallsOnTheThirdUncontestedMove()
    {
        Province province = Empty();
        Settlement target = province.Target!;

        Assert.Equal(ProvinceMoveKind.Pressed, province.Advance(7)!.Value.Kind);
        Assert.Equal(ProvinceMoveKind.Pressed, province.Advance(14)!.Value.Kind);
        Assert.Equal(Allegiance.None, target.Held);

        ProvinceMove taken = province.Advance(21)!.Value;

        Assert.Equal(ProvinceMoveKind.Taken, taken.Kind);
        Assert.Equal(target.Index, taken.Settlement);
        Assert.Equal(Allegiance.His, target.Held);
    }

    /// <summary>A day that is not his move day is not his.</summary>
    [Fact]
    public void HeMovesOnlyOnHisOwnDay()
    {
        Province province = Empty();

        Assert.Null(province.Advance(6));
        Assert.NotNull(province.Advance(7));
    }

    /// <summary>He takes back what was taken from him before he takes what is free.</summary>
    [Fact]
    public void ThePlayersSettlementsArePressedFirst()
    {
        Province province = Empty();
        Settlement mine = province.Settlements[5];
        province.Place(mine.Index, Allegiance.Yours);

        ProvinceMove move = province.Advance(7)!.Value;

        Assert.Equal(mine.Index, move.Settlement);
    }

    /// <summary>An answer eases the pressed settlement and puts his clock back — once a cycle.</summary>
    [Fact]
    public void AnAnswerIsWorthOneMoveAndNoMore()
    {
        Province province = Empty();
        province.Advance(7);

        Settlement pressed = province.Target!;
        Assert.Equal(1, pressed.Warning);

        int moveDay = province.NextMoveDay;

        Assert.True(province.Answer(8));
        Assert.Equal(0, pressed.Warning);
        Assert.Equal(moveDay + province.Tuning.PushBackDays, province.NextMoveDay);

        // The second win of the same week changes nothing: the tempo runs both ways, not one.
        Assert.False(province.Answer(9));
        Assert.Equal(moveDay + province.Tuning.PushBackDays, province.NextMoveDay);
    }

    /// <summary>Two contracts win a settlement that pays nobody.</summary>
    [Fact]
    public void TwoContractsWinAFreeSettlement()
    {
        Province province = Empty();
        Settlement village = province.Settlements[3];

        Assert.Null(province.FileContract(village.Index, 4));
        Assert.NotNull(province.FileContract(village.Index, 8));
        Assert.Equal(Allegiance.Yours, village.Held);
    }

    /// <summary>One of his has to be pushed off his books first, and then costs three.</summary>
    [Fact]
    public void OneOfHisMustBeEasedBeforeItCanBeWon()
    {
        Province province = Empty();
        Settlement village = province.Settlements[2];
        province.Place(village.Index, Allegiance.His, warning: 1);

        // The first contract only breaks his grip.
        Assert.Null(province.FileContract(village.Index, 3));
        Assert.Equal(0, village.Warning);

        Assert.Null(province.FileContract(village.Index, 5));
        Assert.Null(province.FileContract(village.Index, 7));
        Assert.NotNull(province.FileContract(village.Index, 9));
        Assert.Equal(Allegiance.Yours, village.Held);
    }

    /// <summary>What a held settlement is worth is the rival's trade, never its own money.</summary>
    [Fact]
    public void HeldSettlementsSweetenTheWorkAndPayNothingThemselves()
    {
        Province province = Empty();
        int bare = province.Sweeten(100);

        province.Place(0, Allegiance.Yours);
        province.Place(1, Allegiance.Yours);

        Assert.Equal(100, bare);
        Assert.True(province.Sweeten(100) > bare);
        Assert.Equal(0, province.Sweeten(0));
    }

    /// <summary>The bound is the only gate the raid has — and it closes again behind him.</summary>
    [Fact]
    public void ASpentBoundBringsHimToTheGateOnce()
    {
        Province province = new(new ProvinceTuning { Deniability = 1 });

        Assert.True(province.Answer(1));
        Assert.Equal(0, province.Deniability);

        Assert.Equal(ProvinceMoveKind.Raid, province.Advance(province.NextMoveDay)!.Value.Kind);
        Assert.True(province.RaidPending);

        province.RaidSettled();

        Assert.False(province.RaidPending);
        Assert.True(province.Deniability > 0);
    }

    /// <summary>The same seed deals the same province — the map is part of the run.</summary>
    [Fact]
    public void TheSameSeedDealsTheSameMap()
    {
        Province first = Empty();
        Province second = Empty();

        first.Deal(new SeededRandom(99));
        second.Deal(new SeededRandom(99));

        Assert.Equal(
            first.Settlements.Select(s => s.Held),
            second.Settlements.Select(s => s.Held));
        Assert.Equal(6, first.HisHoldings);
    }

    /// <summary>A contract answers the work already begun before it answers new pressure.</summary>
    /// <remarks>
    /// The rule the measurement forced: drawn at random per contract, two contracts landing on the same
    /// village in a season was a coincidence and the map was unwinnable.
    /// </remarks>
    [Fact]
    public void AContractAnswersTheVillageAlreadyBegun()
    {
        Province province = Empty();
        province.Advance(7);

        Settlement pressed = province.Target!;
        Settlement begun = province.Settlements.First(s => s.Index != pressed.Index);

        province.FileContract(begun.Index, 8);

        Assert.Equal(1, begun.Contracts);
        Assert.Equal(begun.Index, province.ContractTarget!.Index);
    }

    /// <summary>With nothing begun, it answers the village he is pressing.</summary>
    [Fact]
    public void WithNothingBegunItAnswersThePressedVillage()
    {
        Province province = Empty();
        province.Advance(7);

        Assert.Equal(province.Target!.Index, province.ContractTarget!.Index);
    }
}
