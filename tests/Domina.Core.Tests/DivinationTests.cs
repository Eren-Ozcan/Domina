using Domina.Core.Campaign;
using Domina.Core.Dojo;

namespace Domina.Core.Tests;

/// <summary>
/// Reading the day's offer — the diviner's system (docs/GDD.md §10). The decisions protected here: the
/// hut alone says who is out there, the diviner in it says what they are worth, and the reading never
/// lies.
/// </summary>
public class DivinationTests
{
    private static DojoState Dojo(bool hut, bool diviner)
    {
        DojoState state = new(
            events: new EventTuning { ChancePerDay = 0 },
            school: new SchoolTuning { BuildDaysFactor = 0 })
        {
            Resources = new Resources(Gold: 2000, Food: 200, Water: 200),
        };

        if (hut)
        {
            state.BuySchoolNode(SchoolNodeId.DivinerHut);
        }

        if (diviner)
        {
            state.Hire(StaffRole.Diviner);
        }

        return state;
    }

    /// <summary>Without the hut the offer stays what it always was: a band and a sighting line.</summary>
    [Fact]
    public void WithoutTheHutTheOfferCannotBeRead()
    {
        DojoState state = Dojo(hut: false, diviner: false);

        Assert.Equal(ReadingDepth.None, state.ReadingDepth);
        Assert.False(state.Reading.Any);
    }

    /// <summary>An empty hut names the enemy and his weapon, and stops there.</summary>
    [Fact]
    public void AnEmptyHutNamesTheEnemyWithoutHisNumbers()
    {
        DojoState state = Dojo(hut: true, diviner: false);
        OfferReading reading = state.Reading;

        Assert.Equal(ReadingDepth.Partial, state.ReadingDepth);
        Assert.Equal(state.Offer.Enemies.Count, reading.Enemies.Count);
        Assert.All(reading.Enemies, line =>
        {
            Assert.False(string.IsNullOrWhiteSpace(line.Name));
            Assert.False(string.IsNullOrWhiteSpace(line.Weapon));
            Assert.Null(line.Stats);
        });
    }

    /// <summary>The diviner reads the numbers too, and reads them right.</summary>
    [Fact]
    public void TheDivinerReadsTheNumbersAndTheyMatchTheEnemy()
    {
        DojoState state = Dojo(hut: true, diviner: true);
        OfferReading reading = state.Reading;

        Assert.Equal(ReadingDepth.Full, state.ReadingDepth);

        for (int i = 0; i < reading.Enemies.Count; i++)
        {
            Assert.Equal(state.Offer.Enemies[i].Name, reading.Enemies[i].Name);
            Assert.Equal(state.Offer.Enemies[i].Weapon.Name, reading.Enemies[i].Weapon);
            Assert.Equal(state.Offer.Enemies[i].EffectiveStats, reading.Enemies[i].Stats);
        }
    }
}
