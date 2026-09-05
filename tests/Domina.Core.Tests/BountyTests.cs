using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Kelle avı sözleşmeleri. Korunan dört karar: hedef isimli ve tek, sözleşmenin süresi
/// var ve süre dolunca düşer, üretim gün ile tohumun saf bir fonksiyonu, ve kabul edip
/// dönmemek kadroya onur olarak yazılır.
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

    /// <summary>Sözleşme birkaç gün açık kalır, sonra düşer — süresiz bir depo değil.</summary>
    [Fact]
    public void ContractsExpireAndLeaveDaysWithNoContract()
    {
        BountyBoard board = new(new BountyTuning { PostingDays = 4, OpenDays = 2 });
        EconomyTuning economy = new();

        Assert.NotNull(board.Posted(1, seed: 3, economy));
        Assert.NotNull(board.Posted(2, seed: 3, economy));
        Assert.Null(board.Posted(3, seed: 3, economy));
        Assert.Null(board.Posted(4, seed: 3, economy));

        // Yeni dönem, yeni sözleşme.
        Assert.NotNull(board.Posted(5, seed: 3, economy));
    }

    /// <summary>Aynı sözleşme açık kaldığı her gün aynı hedefi taşır.</summary>
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

    /// <summary>Hedef, aynı günün sıradan teklifinden belirgin şekilde güçlü.</summary>
    [Fact]
    public void TheTargetIsStrongerThanTheOrdinaryOfferOfTheSameDay()
    {
        DojoState state = Dojo();

        BountyContract? contract = state.Bounty;
        Assert.NotNull(contract);

        double ordinary = state.Offer.Enemies.Max(e => e.EffectiveStats.MaxHealth);
        Assert.True(
            contract.Target.EffectiveStats.MaxHealth > ordinary,
            $"hedef sıradan düşmandan güçlü değil: {contract.Target.EffectiveStats.MaxHealth:F0}"
                + $" / {ordinary:F0}");
    }

    /// <summary>Ödül, aynı canlı sıradan bir düşmanın ödediğinden yüksek.</summary>
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

    /// <summary>Kabul edip dönmemek kadronun tamamına onur olarak yazılır.</summary>
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

        // Son gün de dövüşmeden kapanınca söz kırılır.
        DayReport second = state.Decline();
        Assert.True(second.BountyBroken);
        Assert.Null(state.AcceptedBountyDay);

        Assert.All(
            state.Roster.Living,
            e => Assert.True(e.Warrior.Honor < before, $"{e.Warrior.Name} onur kaybetmedi"));
    }

    /// <summary>Kabul edilmemiş sözleşmenin süresi dolduğunda kimse cezalanmaz.</summary>
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

    /// <summary>Kelleyi getiren ekip söz verilen altını ve onuru alır.</summary>
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

        // Kasa doğrudan ölçülemez: aynı gün stok gideri ödeniyor ve aksilik altın
        // çalabiliyor. Ölçülen kalem sözleşmenin ödediği rakam.
        Assert.True(result.Claimed, "usta hedefi düşüremedi");
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

    /// <summary>Kellesi alınan sözleşme tahtadan iner — aynı hedef iki kez satılmaz.</summary>
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

        // Sözleşmenin süresi hâlâ dolmadı ama hedef ölü.
        Assert.True(contract.IsOpenOn(state.Day));
        Assert.Null(state.Bounty);
    }
}
