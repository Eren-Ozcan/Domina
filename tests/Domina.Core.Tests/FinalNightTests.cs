using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The last night (docs/GDD.md §10). The decisions protected: three heads and nothing else open it,
/// five bouts run one after another with no healing in between, the fifth is Kurogane himself, and a
/// bout lost ends the run for good.
/// </summary>
public class FinalNightTests
{
    /// <summary>A night whose bouts a single warrior can win — the subject under test is the structure.</summary>
    private static SeasonTuning Easy(int days = 1) => new()
    {
        Days = days,
        FinalRoundPowers = [0.2, 0.2, 0.2, 0.2, 0.2],
        FinalRoundEnemies = [1, 1, 1, 1, 1],
    };

    private static WarriorStats Master() => WarriorStats.Recruit() with
    {
        MaxHealth = 4000,
        Strength = 95,
        Accuracy = 95,
        Defense = 90,
        Evasion = 60,
        MaxStamina = 400,
    };

    /// <summary>A dojo standing on the threshold of the night, with the gate already open.</summary>
    private static DojoState AtTheGate(SeasonTuning tuning, int men = 1)
    {
        DojoState state = new(events: new EventTuning { ChancePerDay = 0 }, season: tuning)
        {
            Resources = new Resources(Gold: 5000, Food: 500, Water: 500, Medicine: 40),
        };

        for (int i = 0; i < men; i++)
        {
            state.Roster.Recruit($"Master {i}", Master());
        }

        for (int i = 0; i < tuning.BountyGate; i++)
        {
            state.Season.RecordHead();
        }

        while (state.Day <= tuning.Days)
        {
            state.AdvanceDay();
        }

        return state;
    }

    /// <summary>Before the night opens nobody takes the field, however fit the roster is.</summary>
    [Fact]
    public void TheNightRefusesAPartyWhileTheSeasonIsStillRunning()
    {
        DojoState state = new(season: Easy(days: 40))
        {
            Resources = new Resources(Gold: 1000),
        };
        RosterEntry master = state.Roster.Recruit("Master", Master());

        Assert.Equal(FinalRefusal.NightNotOpen, FinalNight.Refuse(state, [master]));
    }

    /// <summary>A bout is not a day: nothing heals, no upkeep is paid, the countdown does not move.</summary>
    /// <remarks>
    /// This is the whole of the night's design. If a day closed between the bouts the wounded would come
    /// back and the night would stop testing roster depth.
    /// </remarks>
    [Fact]
    public void ABoutBurnsNoDay()
    {
        DojoState state = AtTheGate(Easy());
        int day = state.Day;
        int food = state.Resources.Food;

        FinalRoundResult result = new FinalNight().Fight(
            state,
            [.. state.Roster.FitForCampaign],
            new SeededRandom(4));

        Assert.True(result.Won);
        Assert.Equal(day, state.Day);
        Assert.Equal(food, state.Resources.Food);
        Assert.Equal(2, state.Season.FinalRound);
    }

    /// <summary>Five bouts won end the season in triumph.</summary>
    [Fact]
    public void FiveWonBoutsEndTheSeasonInTriumph()
    {
        SeasonTuning tuning = Easy();
        DojoState state = AtTheGate(tuning);
        FinalNight night = new();

        for (int round = 1; round <= tuning.FinalRounds; round++)
        {
            FinalRoundResult result = night.Fight(
                state,
                [.. state.Roster.FitForCampaign],
                new SeededRandom((ulong)round));

            Assert.True(result.Won, $"bout {round} was lost");
            Assert.Equal(round, result.Round);
        }

        Assert.Equal(SeasonPhase.Triumph, state.Season.Phase);
        Assert.Equal(tuning.FinalRounds, state.Season.Victories);
    }

    /// <summary>A bout lost ends the run — there is no second attempt at the night.</summary>
    [Fact]
    public void ALostBoutEndsTheRun()
    {
        SeasonTuning tuning = Easy() with { FinalRoundPowers = [40, 40, 40, 40, 40] };
        DojoState state = new(events: new EventTuning { ChancePerDay = 0 }, season: tuning)
        {
            Resources = new Resources(Gold: 2000, Food: 100, Water: 100),
        };
        state.Roster.Recruit("Kenji", WarriorStats.Recruit());
        for (int i = 0; i < tuning.BountyGate; i++)
        {
            state.Season.RecordHead();
        }

        while (state.Day <= tuning.Days)
        {
            state.AdvanceDay();
        }

        RosterEntry doomed = state.Roster.Living.Single();
        FinalRoundResult result = new FinalNight().Fight(state, [doomed], new SeededRandom(3));

        Assert.False(result.Won);
        Assert.Equal(SeasonPhase.Fallen, state.Season.Phase);
        Assert.True(state.Season.IsOver);
        Assert.Equal(
            FinalRefusal.NightNotOpen,
            FinalNight.Refuse(state, [.. state.Roster.Entries]));
    }

    /// <summary>A hurt man still answers the bell; a broken one cannot be carried to the field.</summary>
    /// <remarks>
    /// The first version of this rule refused anybody carrying an infirmary day, and the measurement
    /// killed it: a season's roster is never unmarked, so 75% of the dojos that reached the night could
    /// field nobody at all. A wound is carried as health instead.
    /// </remarks>
    [Fact]
    public void AHurtManFightsOnAndABrokenOneCannot()
    {
        SeasonTuning tuning = Easy();
        DojoState state = AtTheGate(tuning, men: 2);
        List<RosterEntry> roster = [.. state.Roster.Living];

        roster[0].Injure(tuning.NightMaxWoundDays - 1);
        roster[1].Injure(tuning.NightMaxWoundDays + 1);

        Assert.Null(FinalNight.Refuse(state, [roster[0]]));
        Assert.Equal(FinalRefusal.Unfit, FinalNight.Refuse(state, [roster[1]]));
    }

    /// <summary>What the wound costs him is health, and it is priced by the days he is carrying.</summary>
    [Fact]
    public void TheWoundIsCarriedOntoTheFieldAsHealth()
    {
        SeasonTuning tuning = Easy();
        DojoState state = AtTheGate(tuning, men: 2);
        List<RosterEntry> roster = [.. state.Roster.Living];
        roster[0].Injure(4);

        BattleSetup setup = FinalNight.Prepare(state, roster);

        Assert.NotNull(setup.StartingHealthShare);
        Assert.Equal(
            1 - (4 * tuning.NightWoundHealthPerDay),
            setup.StartingHealthShare[roster[0].Id],
            9);

        // The whole man is not in the list at all — an absent warrior starts with all of himself.
        Assert.DoesNotContain(roster[1].Id, setup.StartingHealthShare);
    }

    /// <summary>A won bout that leaves nobody able to stand is still a night lost.</summary>
    /// <remarks>
    /// The ending has to land the moment it is decided: reported one call later, the screen would offer
    /// a bout the player has no way of entering.
    /// </remarks>
    [Fact]
    public void ANightWithNobodyLeftToSendIsLostOnTheSpot()
    {
        DojoState state = AtTheGate(Easy(), men: 1);
        RosterEntry only = state.Roster.Living.Single();

        // The bout is stepped by hand so that the wound lands where a real one would: on the field,
        // before the books are closed. He wins it and is carried off, and nobody stands behind him.
        BattleSetup setup = FinalNight.Prepare(state, [only]);
        BattleResult battle = new Battle(setup, new SeededRandom(4)).Run();
        only.Injure(state.Season.Tuning.NightMaxWoundDays + 1);

        FinalRoundResult result = new FinalNight().Settle(state, setup, battle);

        Assert.True(result.Won);
        Assert.Equal(SeasonPhase.Fallen, result.Phase);
        Assert.Equal(SeasonPhase.Fallen, state.Season.Phase);
    }

    /// <summary>The fifth bout is the man himself, and he stands there alone.</summary>
    [Fact]
    public void TheFifthBoutIsKuroganeAlone()
    {
        SeasonTuning tuning = new();

        IReadOnlyList<Warrior> seniors = FinalNight.Opponents(tuning, 1);
        IReadOnlyList<Warrior> last = FinalNight.Opponents(tuning, tuning.FinalRounds);

        Assert.All(seniors, w => Assert.Equal(Adversaries.SeniorStudent.Name, w.Name));
        Assert.Equal(Adversaries.KuroganeHead.Name, Assert.Single(last).Name);
        Assert.True(
            last[0].EffectiveStats.MaxHealth > seniors[0].EffectiveStats.MaxHealth,
            "the head of the school should be heavier than his seniors");
    }

    /// <summary>He is met once, at the end: the road never offers him.</summary>
    [Fact]
    public void TheHeadOfTheSchoolIsNotInTheEncounterPool()
    {
        Assert.DoesNotContain(Adversaries.KuroganeHead, Adversaries.All);
        Assert.DoesNotContain(Adversaries.KuroganeHead, Adversaries.AvailableAt(3.0));
    }

    /// <summary>The night can be put down and picked up again — bout, wounds and all.</summary>
    /// <remarks>
    /// The night burns no day, so nothing else in the save moves while it is being fought: if the bout
    /// number or the wounds did not go into the file, closing the game between two bouts would hand the
    /// player a fresh night with a whole roster.
    /// </remarks>
    [Fact]
    public void ANightInProgressSurvivesASaveAndLoad()
    {
        SeasonTuning tuning = Easy();
        DojoState before = AtTheGate(tuning, men: 2);

        FinalRoundResult first = new FinalNight().Fight(
            before,
            [before.Roster.Living.First()],
            new SeededRandom(4));
        Assert.True(first.Won);

        RosterEntry hurt = before.Roster.Living.First();
        hurt.Injure(3);

        DojoState after = Dojo.Save.DojoSaveFile.Load(Dojo.Save.DojoSaveFile.Write(before)).State!;

        Assert.Equal(SeasonPhase.FinalNight, after.Season.Phase);
        Assert.Equal(before.Season.FinalRound, after.Season.FinalRound);
        Assert.Equal(before.Day, after.Day);

        RosterEntry carried = after.Roster.Living.Single(e => e.Name == hurt.Name);
        Assert.Equal(hurt.RecoveryDaysRemaining, carried.RecoveryDaysRemaining);

        // The wound is still priced the same way on the other side of the file.
        BattleSetup setup = FinalNight.Prepare(after, [carried]);
        Assert.Equal(
            1 - (hurt.RecoveryDaysRemaining * tuning.NightWoundHealthPerDay),
            setup.StartingHealthShare![carried.Id],
            9);
    }

    /// <summary>A head brought in is a step through the gate — the contract is what counts it.</summary>
    [Fact]
    public void AClaimedContractCountsTowardTheGate()
    {
        DojoState state = new(seed: 11)
        {
            Resources = new Resources(Gold: 1000),
        };
        RosterEntry master = state.Roster.Recruit("Master", Master());

        BountyContract? contract = state.AcceptBounty();
        Assert.NotNull(contract);

        BountyResult result = new Expedition()
            .SendToBounty(state, contract, [master], new SeededRandom(5));

        Assert.True(result.Claimed, "the master could not fell the target");
        Assert.Equal(1, state.Season.HeadsTaken);
    }
}
