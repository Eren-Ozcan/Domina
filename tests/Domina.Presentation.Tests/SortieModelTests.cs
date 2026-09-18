using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// The sheet read before the gate opens. What is protected here is that it <b>states</b> and does not
/// decide: it carries only men the roster knows, it never prints an enemy the hut was not paid to
/// read, and the day it promises is the day the books will take.
/// </summary>
public class SortieModelTests
{
    private static DojoState Stocked(ulong seed = 12)
    {
        DojoState state = new(seed: seed);
        state.SetPurse(new Resources(Gold: 2000, Food: 200, Water: 200, Medicine: 20));
        return state;
    }

    private static List<WarriorId> TwoMen(DojoState dojo) =>
        [.. OfferModel.Candidates(dojo).Where(c => c.Fit).Take(2).Select(c => c.Id)];

    [Fact]
    public void TheSheetCarriesTheMenThatWereChosen()
    {
        DojoState dojo = Stocked();
        List<WarriorId> chosen = TwoMen(dojo);

        SortieSheet sheet = SortieModel.Describe(dojo, chosen);

        Assert.Equal(chosen, [.. sheet.Party.Select(row => row.Id)]);
    }

    /// <summary>A man who is not on the roster cannot be drawn on the sheet, whatever is passed in.</summary>
    [Fact]
    public void AManTheRosterDoesNotKnowIsNotPrinted()
    {
        DojoState dojo = Stocked();

        SortieSheet sheet = SortieModel.Describe(dojo, [new WarriorId(90210)]);

        Assert.Empty(sheet.Party);
    }

    /// <summary>
    /// Only the diviner's hut may see the road. With no hut the sheet says it is unread rather than
    /// printing an empty road, which would read as a road with nothing on it.
    /// </summary>
    [Fact]
    public void TheRoadIsUnreadUntilTheHutReadsIt()
    {
        DojoState dojo = Stocked();

        SortieSheet sheet = SortieModel.Describe(dojo, TwoMen(dojo));

        Assert.Equal(OfferModel.ReadOffer(dojo).Count > 0, sheet.Read);
        Assert.Equal(OfferModel.ReadOffer(dojo).Count, sheet.Enemies.Count);
    }

    [Fact]
    public void TheDayItPromisesIsTheDayTheBooksTake()
    {
        DojoState dojo = Stocked();
        Resources draw = dojo.DailyDraw();

        SortieSheet sheet = SortieModel.Describe(dojo, TwoMen(dojo));
        SortieTerm day = sheet.Costs.First(term => term.Label == "the day");

        Assert.Contains("one day", day.Value, StringComparison.Ordinal);

        if (draw.Food > 0)
        {
            Assert.Contains($"{draw.Food} food", day.Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheGoldOnTheSheetIsTheOffersOwn()
    {
        DojoState dojo = Stocked();

        SortieSheet sheet = SortieModel.Describe(dojo, TwoMen(dojo));

        Assert.Equal(
            $"{OfferModel.Describe(dojo).PromisedReward}",
            sheet.Pays.First(term => term.Label == "gold").Value);
    }

    /// <summary>The party line has to say how many are going; it is the number being accepted.</summary>
    [Fact]
    public void ThePartyLineCountsTheMenGoing()
    {
        DojoState dojo = Stocked();
        List<WarriorId> chosen = TwoMen(dojo);

        SortieSheet sheet = SortieModel.Describe(dojo, chosen);

        Assert.Contains(
            $"{chosen.Count} going",
            sheet.Costs.First(term => term.Label == "the party").Value,
            StringComparison.Ordinal);
    }

    /// <summary>A promise already given carries what breaking it costs — that is the whole of its risk.</summary>
    [Fact]
    public void ABountySheetStatesWhatBreakingThePromiseCosts()
    {
        DojoState dojo = Stocked();

        while (OfferModel.DescribeBounty(dojo) is null && dojo.Day < 40)
        {
            dojo.AdvanceDay();
        }

        if (OfferModel.DescribeBounty(dojo) is not BountyCard bounty)
        {
            return;
        }

        SortieSheet sheet = SortieModel.Describe(dojo, bounty, TwoMen(dojo));

        Assert.Contains("The head of", sheet.Heading, StringComparison.Ordinal);
        Assert.Contains(sheet.Costs, term => term.Label == "the promise" && term.Grave);
        Assert.Contains(sheet.Pays, term => term.Label == "honour");
    }
}
