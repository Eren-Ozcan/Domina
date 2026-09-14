using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The timed offer queue (docs/GDD.md §10): several jobs stand at once, each expiring on its own day.
/// What is protected here is that the board stays a <b>pure function of the seed</b> apart from the one
/// thing that is written down — which postings were taken — so reloading a save can neither reroll the
/// board nor hand back a job the dojo has already done.
/// </summary>
public class OfferBoardTests
{
    private static DojoState Dojo(ulong seed = 7)
    {
        DojoState state = new(seed: seed)
        {
            Purse = new Resources(Gold: 900, Food: 60, Water: 60, Medicine: 6),
        };

        state.Roster.Recruit("Kenji", weapon: Weapon.Katana(), armor: Armor.Medium());
        state.Roster.Recruit("Hana", weapon: Weapon.Yari(), armor: Armor.Light());
        state.Roster.Recruit("Sora", weapon: Weapon.Katana(), armor: Armor.Light());
        return state;
    }

    /// <summary>On day one there is one posting — a board cannot show days that never happened.</summary>
    [Fact]
    public void TheBoardStartsWithTodaysPostingAlone()
    {
        DojoState dojo = Dojo();

        Assert.Single(dojo.Board);
        Assert.Equal(1, dojo.Board[0].Day);
    }

    /// <summary>It fills to the board's life and stops there.</summary>
    [Fact]
    public void TheBoardFillsToItsLifeAndNoFurther()
    {
        DojoState dojo = Dojo();
        int life = dojo.Encounters.Tuning.OfferLifeDays;

        for (int i = 0; i < life + 3; i++)
        {
            dojo.Decline();
        }

        Assert.Equal(life, dojo.Board.Count);

        // Newest first, and the oldest is exactly as old as the life allows.
        Assert.Equal(dojo.Day, dojo.Board[0].Day);
        Assert.Equal(dojo.Day - life + 1, dojo.Board[^1].Day);
    }

    /// <summary>A posting expires on its own day and is gone the morning after.</summary>
    [Fact]
    public void APostingRunsOutOnItsLastDay()
    {
        DojoState dojo = Dojo();
        EncounterOffer first = dojo.Board[0];
        int expiry = dojo.ExpiryOf(first);

        while (dojo.Day < expiry)
        {
            dojo.Decline();
            Assert.True(dojo.IsOnTheBoard(first));
        }

        dojo.Decline();

        Assert.False(dojo.IsOnTheBoard(first));
        Assert.DoesNotContain(dojo.Board, o => o.Day == first.Day);
    }

    /// <summary>There is one of each job: taking it strikes it off for good.</summary>
    [Fact]
    public void ATakenPostingDoesNotComeBackTomorrow()
    {
        DojoState dojo = Dojo();
        EncounterOffer taken = dojo.Board[0];
        List<RosterEntry> party = [.. dojo.Roster.Living.Take(taken.RequiredPartySize ?? 2)];

        new Expedition().Send(dojo, taken, party, new SeededRandom(4));

        Assert.False(dojo.IsOnTheBoard(taken));
        Assert.Contains(taken.Day, dojo.TakenOffers);
        Assert.DoesNotContain(dojo.Board, o => o.Day == taken.Day);
    }

    /// <summary>A job that is still standing can be taken days after it was posted.</summary>
    [Fact]
    public void YesterdaysPostingCanStillBeTaken()
    {
        DojoState dojo = Dojo();
        EncounterOffer yesterday = dojo.Board[0];

        dojo.Decline();

        List<RosterEntry> party = [.. dojo.Roster.Living.Take(yesterday.RequiredPartySize ?? 2)];
        Assert.Null(Expedition.Refuse(dojo, yesterday, party));

        ExpeditionResult result = new Expedition().Send(dojo, yesterday, party, new SeededRandom(4));
        Assert.Equal(2, result.Day.Day);
    }

    /// <summary>A posting that has run out is refused, and says which refusal it is.</summary>
    [Fact]
    public void AnExpiredPostingIsRefused()
    {
        DojoState dojo = Dojo();
        EncounterOffer first = dojo.Board[0];

        for (int i = 0; i <= dojo.Encounters.Tuning.OfferLifeDays; i++)
        {
            dojo.Decline();
        }

        List<RosterEntry> party = [.. dojo.Roster.Living.Take(2)];
        Assert.Equal(ExpeditionRefusal.StaleOffer, Expedition.Refuse(dojo, first, party));
    }

    /// <summary>He is at the gate: a raid stands alone, with nothing else to read beside it.</summary>
    [Fact]
    public void ARaidIsTheWholeBoard()
    {
        DojoState dojo = new(
            events: new EventTuning { ChancePerDay = 0 },
            province: new ProvinceTuning { Deniability = 1, MoveEveryDays = 2 })
        {
            Purse = new Resources(Gold: 900, Food: 200, Water: 200),
        };

        dojo.Roster.Recruit("Kenji");

        // One answered move spends the bound; the move after it brings him to the gate.
        dojo.Province.Answer(dojo.Day);
        while (!dojo.UnderRaid)
        {
            dojo.AdvanceDay();
        }

        Assert.Single(dojo.Board);
        Assert.Equal(dojo.Day, dojo.ExpiryOf(dojo.Board[0]));
    }

    /// <summary>The same seed gives the same board: nothing about it is rolled twice.</summary>
    [Fact]
    public void TheBoardIsAPureFunctionOfTheSeed()
    {
        DojoState one = Dojo();
        DojoState two = Dojo();

        for (int i = 0; i < 4; i++)
        {
            one.Decline();
            two.Decline();
        }

        Assert.Equal(
            one.Board.Select(o => (o.Day, o.Sighting, o.Threat)),
            two.Board.Select(o => (o.Day, o.Sighting, o.Threat)));
    }

    /// <summary>
    /// A standing job pays less than it would have on the day it was posted.
    /// </summary>
    /// <remarks>
    /// The rule that keeps the queue from becoming a larder (docs/GDD.md §10): if waiting paid better,
    /// hoarding the postings until the roster is strong would be the correct play and the day a job
    /// arrives would stop being the day to answer it.
    /// </remarks>
    [Fact]
    public void AStandingPostingPaysLessThanAFreshOne()
    {
        DojoState dojo = Dojo();
        EncounterOffer posting = dojo.Board[0];
        int fresh = dojo.PromisedRewardFor(posting);

        dojo.Decline();

        int standing = dojo.PromisedRewardFor(posting);

        Assert.True(standing < fresh, $"a standing job paid {standing}, a fresh one {fresh}");
        Assert.Equal(1, dojo.FeeScaleOf(dojo.Board[0]));
    }

    /// <summary>What the board prints is what the treasury receives.</summary>
    [Fact]
    public void TheReducedFeeIsWhatTheFightActuallyPays()
    {
        DojoState dojo = Dojo();
        EncounterOffer posting = dojo.Board[0];

        dojo.Decline();

        int promised = dojo.PromisedRewardFor(posting);
        int purse = dojo.Resources.Gold;

        List<RosterEntry> party = [.. dojo.Roster.Living.Take(posting.RequiredPartySize ?? 2)];
        ExpeditionResult result = new Expedition().Send(dojo, posting, party, new SeededRandom(4));

        // Only a won fight pays; a lost one is its own measurement and says nothing about the fee.
        if (result.Battle.Outcome == BattleOutcome.PlayerVictory)
        {
            Assert.Equal(promised, result.Reward);
            Assert.Equal(purse + promised, dojo.Resources.Gold);
        }
    }

    /// <summary>The fee stops falling at the floor: a standing job is a fallback, not dead text.</summary>
    [Fact]
    public void TheFeeNeverFallsBelowTheFloor()
    {
        EncounterTuning tuning = new() { StaleFeePerDay = 0.5, StaleFeeFloor = 0.4 };
        EncounterGenerator generator = new(tuning);

        Assert.Equal(1, generator.FeeScale(postedDay: 10, today: 10));
        Assert.Equal(0.5, generator.FeeScale(postedDay: 10, today: 11));
        Assert.Equal(0.4, generator.FeeScale(postedDay: 10, today: 12));
        Assert.Equal(0.4, generator.FeeScale(postedDay: 10, today: 40));
    }

    /// <summary>
    /// The save carries the taken postings, so a reload cannot put a finished job back on the board.
    /// </summary>
    [Fact]
    public void TheSaveRemembersWhichPostingsWereTaken()
    {
        DojoState dojo = Dojo();
        dojo.Decline();

        // Today's posting: one taken on its last day is forgotten as the day closes (it could not be
        // on the board again anyway), so the boundary the save has to carry is a job still in its life.
        EncounterOffer taken = dojo.Board[0];
        List<RosterEntry> party = [.. dojo.Roster.Living.Take(taken.RequiredPartySize ?? 2)];
        new Expedition().Send(dojo, taken, party, new SeededRandom(9));

        DojoSnapshot snapshot = DojoSaveFile.Capture(dojo);
        DojoState loaded = DojoSaveFile.Restore(snapshot).State
            ?? throw new InvalidOperationException("the save did not load");

        Assert.Contains(taken.Day, loaded.TakenOffers);
        Assert.DoesNotContain(loaded.Board, o => o.Day == taken.Day);
    }
}
