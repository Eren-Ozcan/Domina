using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;

namespace Domina.Core.Tests;

/// <summary>
/// The province inside the day loop (docs/GDD.md §10). The decisions protected here: he moves on the
/// dojo's own weekly clock, a raid nobody answers is paid for out of the store and the school's name,
/// and the whole map goes into the save because it is state the season produced.
/// </summary>
public class ProvinceDayTests
{
    private static DojoState Stocked(ProvinceTuning? province = null)
    {
        DojoState state = new(
            events: new EventTuning { ChancePerDay = 0 },
            province: province)
        {
            Purse = new Resources(Gold: 900, Food: 200, Water: 200),
        };

        state.Roster.Recruit("Kenji");
        return state;
    }

    /// <summary>The week the dojo has to answer and the week he moves are the same week.</summary>
    [Fact]
    public void HeMovesOnTheWeeklyTick()
    {
        DojoState state = Stocked();

        for (int day = 1; day < state.Province.Tuning.MoveEveryDays; day++)
        {
            Assert.Null(state.AdvanceDay().RivalMove);
        }

        DayReport tick = state.AdvanceDay();

        Assert.NotNull(tick.RivalMove);
        Assert.Equal(ProvinceMoveKind.Pressed, tick.RivalMove!.Value.Kind);
    }

    /// <summary>A raid left standing empties the store and costs the whole roster its name.</summary>
    [Fact]
    public void ARaidNobodyAnsweredSacksTheDojo()
    {
        DojoState state = Stocked(new ProvinceTuning { Deniability = 1, MoveEveryDays = 2 });
        state.Roster.Living.First().Warrior.Honor = 60;

        // One won fight spends the bound; the move after it brings him to the gate.
        state.Province.Answer(state.Day);

        DayReport announced = state.AdvanceDay();
        while (announced.RivalMove?.Kind != ProvinceMoveKind.Raid)
        {
            announced = state.AdvanceDay();
        }

        Assert.True(state.UnderRaid);
        Assert.Null(announced.Sacked);

        int gold = state.Resources.Gold;
        double honor = state.Roster.Living.First().Warrior.Honor;

        DayReport sacked = state.AdvanceDay();
        while (sacked.Sacked is null)
        {
            sacked = state.AdvanceDay();
        }

        Assert.True(sacked.Sacked!.Value.Gold > 0);
        Assert.True(state.Resources.Gold < gold);
        Assert.True(state.Roster.Living.First().Warrior.Honor < honor);
        Assert.False(state.UnderRaid);
    }

    /// <summary>The map is state the season produced, so it survives a save and a load.</summary>
    [Fact]
    public void TheMapSurvivesTheSave()
    {
        DojoState state = Stocked();
        state.Province.Place(3, Allegiance.Yours);
        state.Province.Place(4, Allegiance.His, warning: 1, contracts: 2);
        state.AdvanceDay();

        LoadResult loaded = DojoSaveFile.Load(DojoSaveFile.Write(state));

        Assert.True(loaded.Succeeded);

        Province restored = loaded.State!.Province;

        Assert.Equal(Allegiance.Yours, restored.Settlements[3].Held);
        Assert.Equal(Allegiance.His, restored.Settlements[4].Held);
        Assert.Equal(1, restored.Settlements[4].Warning);
        Assert.Equal(2, restored.Settlements[4].Contracts);
        Assert.Equal(state.Province.NextMoveDay, restored.NextMoveDay);
        Assert.Equal(state.Province.Deniability, restored.Deniability);
    }
}
