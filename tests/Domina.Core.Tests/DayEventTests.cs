using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The day's mishaps (GDD §11: random events subtract resources, buffer-keeping pressure).
/// Three decisions are protected: an event is a <b>pure</b> function of the day and the seed, they all
/// <b>subtract</b>, and the effect hits the treasury or the calendar rather than the store — the daily
/// shopping already fills the store to exactly what is needed, so stolen provisions would count for nothing.
/// </summary>
public class DayEventTests
{
    /// <summary>A table that forces a single event kind — it isolates the effect.</summary>
    private static DayEventTable Always(DayEventKind kind, EventTuning? tuning = null) =>
        new((tuning ?? new EventTuning()) with { ChancePerDay = 1, Weights = [(kind, 1)] });

    private static DojoState Dojo(int gold = 1000, EventTuning? events = null)
    {
        DojoState state = new(seed: 5, events: events);
        state.Resources = new Resources(Gold: gold);
        return state;
    }

    [Fact]
    public void TheSameDayAndSeedAlwaysGiveTheSameDay()
    {
        DojoState state = Dojo();
        state.Roster.Recruit("Kenji", WarriorStats.Recruit(), Weapon.Katana(), Armor.Medium());

        List<string?> first = [.. Enumerable.Range(1, 40).Select(d => state.Events.Roll(state, d)?.Description)];
        List<string?> second = [.. Enumerable.Range(1, 40).Select(d => state.Events.Roll(state, d)?.Description)];

        Assert.Equal(first, second);
        Assert.Contains(first, e => e is not null);
        Assert.Contains(first, e => e is null);
    }

    /// <summary>The event stream must not be locked to the offer stream.</summary>
    [Fact]
    public void MishapsDoNotFollowTheEncounterStream()
    {
        DojoState state = Dojo();

        List<int> eventDays =
        [
            .. Enumerable.Range(1, 200).Where(d => state.Events.Roll(state, d) is not null),
        ];
        List<int> direDays =
        [
            .. Enumerable.Range(1, 200)
                .Where(d => state.Encounters.Offer(d, state.Seed).Threat >= Campaign.ThreatBand.Heavy),
        ];

        Assert.NotEmpty(eventDays);
        Assert.NotEmpty(direDays);

        // Were they locked, event days would be a subset of heavy-offer days.
        Assert.Contains(eventDays, d => !direDays.Contains(d));
    }

    [Fact]
    public void TheftTakesFromThePurseAndStopsAtZero()
    {
        DojoState rich = Dojo(gold: 1000, events: new EventTuning { ChancePerDay = 1, Weights = [(DayEventKind.Theft, 1)] });
        rich.Roster.Recruit("Kenji");

        DayReport day = rich.AdvanceDay();

        Assert.NotNull(day.Event);
        Assert.Equal(DayEventKind.Theft, day.Event!.Kind);
        Assert.InRange(day.Event.Gold, 1, 120);
        Assert.Equal(1000 - day.Event.Gold - day.Upkeep.GoldSpent, rich.Resources.Gold);

        DojoState broke = Dojo(gold: 0, events: new EventTuning { ChancePerDay = 1, Weights = [(DayEventKind.Theft, 1)] });
        broke.Roster.Recruit("Hana");
        broke.AdvanceDay();

        Assert.Equal(0, broke.Resources.Gold);
        Assert.False(broke.Resources.AnyNegative);
    }

    /// <summary>Spoiled provisions make that day's shopping more expensive — it is not postponed to the next day.</summary>
    [Fact]
    public void SpoiledStoresMakeTheDaysShoppingListDearer()
    {
        EventTuning always = new() { ChancePerDay = 1, Weights = [(DayEventKind.Spoilage, 1)] };
        EventTuning never = new() { ChancePerDay = 0 };

        DojoState spoiled = Dojo(events: always);
        DojoState calm = Dojo(events: never);
        foreach (DojoState state in (DojoState[])[spoiled, calm])
        {
            state.Roster.Recruit("Kenji");
            state.Roster.Recruit("Hana");
        }

        DayReport bad = spoiled.AdvanceDay();
        DayReport good = calm.AdvanceDay();

        Assert.Equal(DayEventKind.Spoilage, bad.Event!.Kind);
        Assert.InRange(bad.Event.FoodFactor, 1, 2);
        Assert.InRange(bad.Upkeep.Food, good.Upkeep.Food, good.Upkeep.Food * 2);
        Assert.True(bad.Upkeep.GoldSpent >= good.Upkeep.GoldSpent);
    }

    [Fact]
    public void SpoiledMedicineCostsTheInfirmaryItsDay()
    {
        DojoState state = Dojo(events: new EventTuning { ChancePerDay = 1, Weights = [(DayEventKind.SpoiledMedicine, 1)] });
        RosterEntry wounded = state.Roster.Recruit("Kenji");
        wounded.Injure(4);

        DayReport day = state.AdvanceDay();

        Assert.Equal(DayEventKind.SpoiledMedicine, day.Event!.Kind);
        Assert.Equal(0, day.Upkeep.Medicine);
        Assert.Empty(day.Upkeep.Medicated);

        // A day without medicine burns only the natural recovery.
        Assert.Equal(3, wounded.RecoveryDaysRemaining);
    }

    [Fact]
    public void IllnessPutsAHealthyWarriorInTheInfirmary()
    {
        DojoState state = Dojo(events: new EventTuning { ChancePerDay = 1, Weights = [(DayEventKind.Illness, 1)], MaxIllnessDays = 3 });
        RosterEntry entry = state.Roster.Recruit("Kenji");

        DayReport day = state.AdvanceDay();

        Assert.Equal(entry.Id, day.Event!.Target);
        Assert.InRange(day.Event.RecoveryDays, 1, 3);

        // The infirmary works the same day too: natural recovery burns one day, medicine one more.
        Assert.Contains(entry.Id, day.Upkeep.Medicated);
        // A short illness can pass the same day: natural recovery burns one day, medicine one more.
        int left = Math.Max(0, day.Event.RecoveryDays - 2);
        Assert.Equal(left, entry.RecoveryDaysRemaining);
        Assert.Equal(
            left > 0 ? DojoActivity.Recovering : DojoActivity.Resting,
            entry.Activity);
    }

    /// <summary>If there is no healthy warrior to put down, the illness falls on nothing — it does not crash.</summary>
    [Fact]
    public void MishapsWithNoTargetPassHarmlessly()
    {
        DojoState state = Dojo(events: new EventTuning { ChancePerDay = 1, Weights = [(DayEventKind.Illness, 1)] });
        RosterEntry wounded = state.Roster.Recruit("Kenji");
        wounded.Injure(5);

        DayReport day = state.AdvanceDay();

        Assert.Equal(DayEventKind.Illness, day.Event!.Kind);
        Assert.Null(day.Event.Target);
        Assert.Equal(0, day.Event.RecoveryDays);
    }

    /// <summary>They all subtract: no event puts money in the treasury or erases an infirmary day.</summary>
    [Fact]
    public void NoMishapEverHelps()
    {
        foreach (DayEventKind kind in Enum.GetValues<DayEventKind>())
        {
            DojoState state = Dojo();
            state.Roster.Recruit("Kenji", WarriorStats.Recruit(), Weapon.Katana(), Armor.Medium());

            DayEvent? happening = Always(kind).Roll(state, new SeededRandom(9));

            Assert.NotNull(happening);
            Assert.True(happening!.Gold >= 0);
            Assert.True(happening.RecoveryDays >= 0);
            Assert.True(happening.FoodFactor >= 1);
            Assert.True(happening.WaterFactor >= 1);
        }
    }

    [Fact]
    public void QuietDaysStayQuiet()
    {
        DojoState state = Dojo(events: new EventTuning { ChancePerDay = 0 });
        state.Roster.Recruit("Kenji");

        Assert.Null(state.AdvanceDay().Event);
    }

    /// <summary>
    /// The severity is not fixed: the same event does not take the same amount every time.
    /// </summary>
    /// <remarks>
    /// A fixed rate would turn the mishap into a calculable tax — if the player knows the loss up front,
    /// keeping a buffer is arithmetic rather than a decision.
    /// </remarks>
    [Fact]
    public void TheSameMishapDoesNotAlwaysCostTheSame()
    {
        DojoState state = Dojo(gold: 5000);
        DayEventTable thieves = Always(DayEventKind.Theft);

        List<int> takes =
        [
            .. Enumerable.Range(1, 25).Select(seed => thieves.Roll(state, new SeededRandom((ulong)seed))!.Gold),
        ];

        Assert.True(takes.Distinct().Count() > 5);
        Assert.All(takes, t => Assert.InRange(t, 1, (int)(5000 * 0.12) + 1));

        DayEventTable rot = Always(DayEventKind.Spoilage);
        List<double> factors =
        [
            .. Enumerable.Range(1, 25).Select(seed => rot.Roll(state, new SeededRandom((ulong)seed))!.FoodFactor),
        ];

        Assert.True(factors.Distinct().Count() > 5);
        Assert.All(factors, f => Assert.InRange(f, 1, 2));
    }
}
