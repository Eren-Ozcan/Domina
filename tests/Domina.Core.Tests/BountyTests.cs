using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Bounty contracts. Four decisions are protected: the target is named and single, the contract has a
/// deadline and falls off when it expires, the generation is a pure function of the day and the seed,
/// and accepting and not coming back is written to the roster as honour.
/// </summary>
public class BountyTests
{
    private static DojoState Dojo(ulong seed = 7, BountyTuning? bounties = null)
    {
        DojoState state = new(seed: seed, bounties: bounties);
        state.Resources = new Resources(Gold: 1000);
        return state;
    }

    [Fact]
    public void TheSameDayAndSeedAlwaysPostTheSameContract()
    {
        BountyBoard board = new();
        EconomyTuning economy = new();

        BountyContract? first = board.Posted(2, seed: 99, economy);
        BountyContract? again = board.Posted(2, seed: 99, economy);

        Assert.NotNull(first);
        Assert.NotNull(again);
        Assert.Equal(first.Target.Name, again.Target.Name);
        Assert.Equal(first.Reward, again.Reward);
        Assert.Equal(first.Deadline, again.Deadline);
    }

    /// <summary>A contract stays open for a few days, then falls off — it is not an open-ended warehouse.</summary>
    [Fact]
    public void ContractsExpireAndLeaveDaysWithNoContract()
    {
        BountyBoard board = new(new BountyTuning { PostingDays = 4, OpenDays = 2 });
        EconomyTuning economy = new();

        Assert.NotNull(board.Posted(1, seed: 3, economy));
        Assert.NotNull(board.Posted(2, seed: 3, economy));
        Assert.Null(board.Posted(3, seed: 3, economy));
        Assert.Null(board.Posted(4, seed: 3, economy));

        // A new period, a new contract.
        Assert.NotNull(board.Posted(5, seed: 3, economy));
    }

    /// <summary>The same contract carries the same target every day it stays open.</summary>
    [Fact]
    public void AnOpenContractKeepsItsTargetWhileItIsOpen()
    {
        BountyBoard board = new(new BountyTuning { PostingDays = 5, OpenDays = 3 });
        EconomyTuning economy = new();

        BountyContract? day1 = board.Posted(1, seed: 42, economy);
        BountyContract? day3 = board.Posted(3, seed: 42, economy);

        Assert.NotNull(day1);
        Assert.NotNull(day3);
        Assert.Equal(day1.Target.Name, day3.Target.Name);
        Assert.Equal(1, day1.DaysLeft(3));
    }

    /// <summary>The target is clearly stronger than the same day's ordinary offer.</summary>
    [Fact]
    public void TheTargetIsStrongerThanTheOrdinaryOfferOfTheSameDay()
    {
        DojoState state = Dojo();

        BountyContract? contract = state.Bounty;
        Assert.NotNull(contract);

        double ordinary = state.Offer.Enemies.Max(e => e.EffectiveStats.MaxHealth);
        Assert.True(
            contract.Target.EffectiveStats.MaxHealth > ordinary,
            $"the target is not stronger than an ordinary enemy: {contract.Target.EffectiveStats.MaxHealth:F0}"
                + $" / {ordinary:F0}");
    }

    /// <summary>The reward is higher than an ordinary enemy with the same health pays.</summary>
    [Fact]
    public void TheContractPaysMoreThanTheSameHealthWouldPayOnAPatrol()
    {
        DojoState state = Dojo();
        BountyContract? contract = state.Bounty;
        Assert.NotNull(contract);

        int ordinary = (int)Math.Round(
            contract.Target.EffectiveStats.MaxHealth * state.Economy.VictoryGoldPerEnemyHealth);

        Assert.True(contract.Reward > ordinary, $"{contract.Reward} / {ordinary}");
    }

    [Fact]
    public void AcceptingDoesNotSpendTheDay()
    {
        DojoState state = Dojo();
        int before = state.Day;

        Assert.NotNull(state.AcceptBounty());
        Assert.Equal(before, state.Day);
        Assert.Equal(before, state.AcceptedBountyDay);
    }

    /// <summary>Accepting and not coming back is written as honour to the whole roster.</summary>
    [Fact]
    public void BreakingThePromiseCostsTheWholeRosterHonor()
    {
        DojoState state = Dojo(bounties: new BountyTuning { PostingDays = 6, OpenDays = 2 });
        state.Roster.Recruit("Kenji", WarriorStats.Recruit());
        state.Roster.Recruit("Hana", WarriorStats.Recruit());

        BountyContract? contract = state.AcceptBounty();
        Assert.NotNull(contract);

        double before = state.Roster.Living.Min(e => e.Warrior.Honor);

        DayReport first = state.Decline();
        Assert.False(first.BountyBroken);

        // When the last day too closes without a fight, the promise is broken.
        DayReport second = state.Decline();
        Assert.True(second.BountyBroken);
        Assert.Null(state.AcceptedBountyDay);

        Assert.All(
            state.Roster.Living,
            e => Assert.True(e.Warrior.Honor < before, $"{e.Warrior.Name} onur kaybetmedi"));
    }

    /// <summary>When an unaccepted contract expires, nobody is punished.</summary>
    [Fact]
    public void AnUnacceptedContractExpiresQuietly()
    {
        DojoState state = Dojo(bounties: new BountyTuning { PostingDays = 6, OpenDays = 1 });
        state.Roster.Recruit("Kenji", WarriorStats.Recruit());

        double before = state.Roster.Living.Single().Warrior.Honor;
        DayReport report = state.Decline();

        Assert.False(report.BountyBroken);
        Assert.Equal(before, state.Roster.Living.Single().Warrior.Honor);
    }

    /// <summary>The party that brings in the head takes the gold and the honour promised.</summary>
    [Fact]
    public void ClaimingTheHeadPaysThePromisedGoldAndHonor()
    {
        DojoState state = Dojo(seed: 11);
        RosterEntry hero = state.Roster.Recruit(
            "Usta",
            WarriorStats.Recruit() with
            {
                MaxHealth = 4000,
                Strength = 95,
                Accuracy = 95,
                Defense = 90,
                MaxStamina = 400,
            });

        BountyContract? contract = state.AcceptBounty();
        Assert.NotNull(contract);

        double honorBefore = hero.Warrior.Honor;

        BountyResult result = new Expedition()
            .SendToBounty(state, contract, [hero], new SeededRandom(5));

        // The treasury cannot be measured directly: the stock cost is paid the same day and a mishap can
        // steal gold. The item measured is the figure the contract paid.
        Assert.True(result.Claimed, "the master could not fell the target");
        Assert.Equal(contract.Reward, result.Reward);
        Assert.True(hero.Warrior.Honor > honorBefore);
        Assert.Null(state.AcceptedBountyDay);
    }

    [Fact]
    public void AClosedContractCannotBeEntered()
    {
        DojoState state = Dojo(bounties: new BountyTuning { PostingDays = 6, OpenDays = 1 });
        RosterEntry hero = state.Roster.Recruit("Kenji", WarriorStats.Recruit());

        BountyContract? contract = state.Bounty;
        Assert.NotNull(contract);

        state.Decline();

        Assert.Throws<InvalidOperationException>(
            () => new Expedition().SendToBounty(state, contract, [hero], new SeededRandom(1)));
    }

    /// <summary>A contract whose head is taken comes off the board — the same target is not sold twice.</summary>
    [Fact]
    public void AClaimedContractLeavesTheBoardForTheRestOfItsDays()
    {
        DojoState state = Dojo(seed: 11, bounties: new BountyTuning { PostingDays = 6, OpenDays = 4 });
        RosterEntry hero = state.Roster.Recruit(
            "Usta",
            WarriorStats.Recruit() with
            {
                MaxHealth = 4000,
                Strength = 95,
                Accuracy = 95,
                Defense = 90,
                MaxStamina = 400,
            });

        BountyContract? contract = state.Bounty;
        Assert.NotNull(contract);

        BountyResult result = new Expedition()
            .SendToBounty(state, contract, [hero], new SeededRandom(5));
        Assert.True(result.Claimed);

        // The contract has not expired yet but the target is dead.
        Assert.True(contract.IsOpenOn(state.Day));
        Assert.Null(state.Bounty);
    }
}
