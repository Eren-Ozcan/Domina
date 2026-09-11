using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Morale and Will (docs/GDD.md §3). The decisions protected: morale is a narrow band and never a
/// second class, Will brakes the falls and not the rises, a warrior can break on his own nerve, and a
/// feast is a decision with a price rather than a button.
/// </summary>
public class MoraleTests
{
    private static DojoState Quiet(int gold = 2000) =>
        new(events: new EventTuning { ChancePerDay = 0 })
        {
            Resources = new Resources(Gold: gold, Food: 200, Water: 200, Medicine: 20),
        };

    /// <summary>Morale bends the fighting stats and leaves the pools alone.</summary>
    /// <remarks>
    /// Health and stamina carry between fights; scaling them would take away wounds a warrior already
    /// had, which is a different rule wearing morale's coat.
    /// </remarks>
    [Fact]
    public void MoraleBendsWhatHeDoesTodayAndNotWhatHeCarries()
    {
        Warrior warrior = TestBuilders.Warrior(1, "Kenji");
        WarriorStats middle = warrior.EffectiveStats;

        warrior.Morale = MoraleScale.Max;
        WarriorStats high = warrior.EffectiveStats;

        warrior.Morale = MoraleScale.Min;
        WarriorStats low = warrior.EffectiveStats;

        Assert.True(high.Accuracy > middle.Accuracy);
        Assert.True(low.Accuracy < middle.Accuracy);
        Assert.Equal(middle.MaxHealth, low.MaxHealth);
        Assert.Equal(middle.MaxStamina, high.MaxStamina);
    }

    /// <summary>
    /// At the middle of the scale morale does nothing at all.
    /// </summary>
    /// <remarks>
    /// This is what keeps every number measured before morale existed valid: a warrior at 50 is the
    /// warrior those measurements were taken on.
    /// </remarks>
    [Fact]
    public void TheMiddleOfTheScaleChangesNothing()
    {
        Warrior warrior = TestBuilders.Warrior(1, "Kenji");

        Assert.Equal(warrior.BaseStats.Accuracy, warrior.EffectiveStats.Accuracy, 9);
        Assert.Equal(1, MoraleBand.Default.FactorFor(MoraleScale.Starting), 9);
    }

    /// <summary>Will slows a fall and does nothing at all to a rise.</summary>
    [Fact]
    public void WillBrakesTheFallsAndNotTheRises()
    {
        Warrior stubborn = TestBuilders.Warrior(1, "Kenji");
        stubborn.BaseStats = stubborn.BaseStats with { Willpower = 100 };

        Warrior brittle = TestBuilders.Warrior(2, "Hana");
        brittle.BaseStats = brittle.BaseStats with { Willpower = 0 };

        MoraleLedger.Lower(stubborn, 20);
        MoraleLedger.Lower(brittle, 20);

        Assert.True(stubborn.Morale > brittle.Morale);

        stubborn.Morale = MoraleScale.Starting;
        brittle.Morale = MoraleScale.Starting;
        MoraleLedger.Raise(stubborn, 10);
        MoraleLedger.Raise(brittle, 10);

        Assert.Equal(stubborn.Morale, brittle.Morale, 9);
    }

    /// <summary>A hungry day costs morale; a fed one returns a little.</summary>
    [Fact]
    public void AHungryDayCostsMoraleAndAFedDayReturnsSome()
    {
        DojoState fed = Quiet();
        RosterEntry wellFed = fed.Roster.Recruit("Kenji");
        wellFed.Warrior.Morale = 40;
        fed.AdvanceDay();

        DojoState starving = new(events: new EventTuning { ChancePerDay = 0 })
        {
            Resources = Resources.Empty,
        };
        RosterEntry hungry = starving.Roster.Recruit("Hana");
        hungry.Warrior.Morale = 40;
        starving.AdvanceDay();

        Assert.True(wellFed.Warrior.Morale > 40);
        Assert.True(wellFed.Warrior.Morale <= MoraleScale.Starting);
        Assert.True(hungry.Warrior.Morale < 40);
    }

    /// <summary>The bard's hall is the one building whose whole output is morale.</summary>
    [Fact]
    public void TheBardsHallLiftsTheRosterEveryDay()
    {
        double MoraleAfterADay(bool bard)
        {
            DojoState state = new(
                events: new EventTuning { ChancePerDay = 0 },
                school: new SchoolTuning { BuildDaysFactor = 0 })
            {
                Resources = new Resources(Gold: 2000, Food: 50, Water: 50),
            };

            RosterEntry entry = state.Roster.Recruit("Kenji");
            entry.Warrior.Morale = 40;

            if (bard)
            {
                state.BuySchoolNode(SchoolNodeId.BardHall);
                state.Hire(StaffRole.Bard);
            }

            state.AdvanceDay();
            return entry.Warrior.Morale;
        }

        Assert.True(MoraleAfterADay(bard: true) > MoraleAfterADay(bard: false));
    }

    /// <summary>A feast spends sake, lifts the roster, and cannot be held again the next day.</summary>
    [Fact]
    public void AFeastSpendsSakeAndThenWaits()
    {
        DojoState state = Quiet();
        RosterEntry kenji = state.Roster.Recruit("Kenji");
        RosterEntry hana = state.Roster.Recruit("Hana");
        kenji.Warrior.Morale = 40;
        hana.Warrior.Morale = 40;

        Assert.False(state.CanFeast);
        Assert.Equal(2, state.BuySake(2));
        Assert.True(state.CanFeast);
        Assert.True(state.Feast());

        Assert.Equal(0, state.Resources.Sake);
        Assert.True(kenji.Warrior.Morale > 40);
        Assert.True(hana.Warrior.Morale > 40);

        state.BuySake(2);
        Assert.False(state.CanFeast);
        Assert.False(state.Feast());
    }

    /// <summary>
    /// A quiet day never lifts a warrior above the middle, and a high fades on its own.
    /// </summary>
    /// <remarks>
    /// The first shape of this rule was an unconditional daily gain, and the measurement killed it: every
    /// roster sat at 100 within a month and the whole lower half of the band became unreachable. What is
    /// above the middle has to be bought, and it does not keep.
    /// </remarks>
    [Fact]
    public void QuietDaysSettleTowardTheMiddleFromEitherSide()
    {
        DojoState state = Quiet();
        RosterEntry low = state.Roster.Recruit("Kenji");
        RosterEntry high = state.Roster.Recruit("Hana");
        low.Warrior.Morale = 20;
        high.Warrior.Morale = 90;

        for (int day = 0; day < 40; day++)
        {
            state.AdvanceDay();
        }

        Assert.Equal(MoraleScale.Starting, low.Warrior.Morale, 6);
        Assert.Equal(MoraleScale.Starting, high.Warrior.Morale, 6);
    }

    /// <summary>Sake is never bought by the day's own bill — only by the player.</summary>
    [Fact]
    public void TheDaysBillNeverBuysSake()
    {
        DojoState state = Quiet();
        state.Roster.Recruit("Kenji");

        state.AdvanceDay();

        Assert.Equal(0, state.Resources.Sake);
    }

    /// <summary>
    /// A warrior in trouble can decide for himself that he has had enough.
    /// </summary>
    /// <remarks>
    /// The first decision to leave the field in this game that is not the player's. What is tested is
    /// the rule and its direction, not the number: a brittle man breaks more often than a stubborn one.
    /// </remarks>
    [Fact]
    public void ABrittleWarriorBreaksMoreOftenThanAStubbornOne()
    {
        int Panics(double will)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var battle = new Battle(Losing(will), new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<WarriorPanicked>().Count();
            }

            return total;
        }

        int brittle = Panics(0);
        int stubborn = Panics(100);

        Assert.True(brittle > 0, "Nobody broke even at Will 0.");
        Assert.True(brittle > stubborn, $"Will did not hold the line ({stubborn} >= {brittle}).");
    }

    /// <summary>Low morale bends the same check further against the warrior.</summary>
    [Fact]
    public void LowMoraleBreaksAWarriorSooner()
    {
        int Panics(double morale)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                BattleSetup setup = Losing(will: 50);
                setup.PlayerSide[0].Morale = morale;

                var battle = new Battle(setup, new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<WarriorPanicked>().Count();
            }

            return total;
        }

        Assert.True(Panics(MoraleScale.Min) > Panics(MoraleScale.Max));
    }

    /// <summary>A fight nobody on the dojo side is winning — the bed the panic check is measured on.</summary>
    private static BattleSetup Losing(double will) => new(
        [
            Nerved(
                TestBuilders.Warrior(1, "Kenji", health: 40, aggression: 40, weapon: Weapon.Katana()),
                will),
        ],
        [
            TestBuilders.Warrior(101, "Enemy", health: 400, aggression: 90, strength: 60),
            TestBuilders.Warrior(102, "Enemy 2", health: 400, aggression: 90, strength: 60),
        ])
    {
        Tuning = CombatTuning.Default with { StartOffsetX = 60 },
    };

    /// <summary>Gives the warrior the nerve the test is about.</summary>
    private static Warrior Nerved(Warrior warrior, double will)
    {
        warrior.BaseStats = warrior.BaseStats with { Willpower = will };
        return warrior;
    }
}
