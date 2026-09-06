using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// Günün teklifi ekranının modeli. Korunan üç karar: düşman kadrosu ekrana hiç
/// verilmez, ödül girmeden önce okunur, ve seferin reddedileceği <b>fırlamadan önce</b>
/// bilinir.
/// </summary>
public class OfferModelTests
{
    private static DojoState Stocked(ulong seed = 12)
    {
        DojoState state = new(seed: seed);
        state.Resources = new Resources(Gold: 2000, Food: 200, Water: 200, Medicine: 20);
        return state;
    }

    [Fact]
    public void TheCardCarriesTheBandAndTheSightingNotTheEnemies()
    {
        DojoState dojo = Stocked();

        OfferCard card = OfferModel.Describe(dojo);

        Assert.Equal(dojo.Day, card.Day);
        Assert.Equal(dojo.Offer.Threat, card.Threat);
        Assert.Equal(dojo.Offer.Sighting, card.Sighting);
        Assert.Equal(EncounterOffer.MaxPartySize, card.MaxPartySize);
    }

    /// <summary>Ödül girmeden önce okunabilir olmalı; yoksa "al ya da bırak" bir kumar olur.</summary>
    [Fact]
    public void TheRewardIsReadableBeforeEntering()
    {
        DojoState dojo = Stocked();

        Assert.True(OfferModel.Describe(dojo).PromisedReward > 0);
    }

    /// <summary>Hami ödülü büyütür — kart okulun işlediği sayıyı göstermeli.</summary>
    [Fact]
    public void TheCardReadsTheSchoolAdjustedReward()
    {
        DojoState dojo = Stocked();
        int before = OfferModel.Describe(dojo).PromisedReward;

        dojo.Resources = dojo.Resources with { Gold = 5000 };
        Assert.True(dojo.BuySchoolNode(SchoolNodeId.Steward));
        Assert.True(dojo.BuySchoolNode(SchoolNodeId.Patron));

        Assert.True(OfferModel.Describe(dojo).PromisedReward > before);
    }

    [Fact]
    public void TheWoundedAreListedButNotFit()
    {
        DojoState dojo = Stocked();
        RosterEntry ready = dojo.Roster.Recruit("Zenji");
        RosterEntry wounded = dojo.Roster.Recruit("Aiko");
        RosterEntry dead = dojo.Roster.Recruit("Botan");
        wounded.Injure(3);
        dojo.Roster.Kill(dead.Id);

        IReadOnlyList<PartyCandidate> candidates = OfferModel.Candidates(dojo);

        Assert.Equal([ready.Id, wounded.Id], candidates.Select(c => c.Id));
        Assert.True(candidates[0].Fit);
        Assert.False(candidates[1].Fit);
        Assert.Equal(3, candidates[1].RecoveryDaysRemaining);
    }

    [Fact]
    public void AnEmptyPartyIsRefused()
    {
        DojoState dojo = Stocked();
        dojo.Roster.Recruit("Zenji");

        PartyVerdict verdict = OfferModel.Judge(dojo, []);

        Assert.False(verdict.CanSend);
        Assert.Equal(ExpeditionRefusal.EmptyParty, verdict.Refusal);
    }

    [Fact]
    public void AWoundedWarriorClosesTheButton()
    {
        DojoState dojo = Stocked();
        RosterEntry wounded = dojo.Roster.Recruit("Aiko");
        wounded.Injure(3);

        Assert.Equal(ExpeditionRefusal.Unfit, OfferModel.Judge(dojo, [wounded.Id]).Refusal);
    }

    [Fact]
    public void AFifthWarriorDoesNotFitInTheParty()
    {
        DojoState dojo = Stocked();
        List<WarriorId> party =
            [.. Enumerable.Range(1, 5).Select(i => dojo.Roster.Recruit($"Savaşçı {i}").Id)];

        PartyVerdict verdict = OfferModel.Judge(dojo, party);

        Assert.Equal(ExpeditionRefusal.WrongPartySize, verdict.Refusal);
        Assert.Equal(5, verdict.Size);
    }

    /// <summary>Ölen savaşçının kimliği ekranda kalabilir; hüküm onu sessizce düşürmez.</summary>
    [Fact]
    public void AnIdThatIsNoLongerInTheRosterIsRefusedNotDropped()
    {
        DojoState dojo = Stocked();
        RosterEntry ready = dojo.Roster.Recruit("Zenji");

        PartyVerdict verdict = OfferModel.Judge(dojo, [ready.Id, new WarriorId(9999)]);

        Assert.Equal(ExpeditionRefusal.NotInRoster, verdict.Refusal);
        Assert.Equal(2, verdict.Size);
    }

    [Fact]
    public void AFitWarriorCanBeSent()
    {
        DojoState dojo = Stocked();
        RosterEntry ready = dojo.Roster.Recruit("Zenji");

        Assert.True(OfferModel.Judge(dojo, [ready.Id]).CanSend);
        Assert.Equal([ready], OfferModel.Party(dojo, [ready.Id]));
    }

    [Fact]
    public void ABoardWithNoContractShowsNoCard()
    {
        DojoState dojo = Stocked();

        Assert.Equal(dojo.Bounty is null, OfferModel.DescribeBounty(dojo) is null);
    }

    [Fact]
    public void AnAcceptedContractIsMarkedOnTheCard()
    {
        DojoState dojo = Stocked();
        BountyContract contract = OpenContract(dojo);

        Assert.False(OfferModel.DescribeBounty(dojo)!.Value.Accepted);
        Assert.NotNull(dojo.AcceptBounty());

        BountyCard card = OfferModel.DescribeBounty(dojo)!.Value;

        Assert.True(card.Accepted);
        Assert.Equal(contract.Target.Name, card.TargetName);
        Assert.Equal(contract.Reward, card.Reward);
        Assert.Equal(contract.DaysLeft(dojo.Day), card.DaysLeft);
    }

    /// <summary>Süresi geçmiş sözleşmeye ekip gönderilemez — söz verilmiş olsa bile.</summary>
    [Fact]
    public void AClosedContractRefusesTheParty()
    {
        DojoState dojo = Stocked();
        BountyContract contract = OpenContract(dojo);
        RosterEntry ready = dojo.Roster.Recruit("Zenji");

        Assert.True(OfferModel.JudgeBounty(dojo, contract, [ready.Id]).CanSend);

        while (contract.IsOpenOn(dojo.Day))
        {
            dojo.Decline();
        }

        Assert.Equal(
            ExpeditionRefusal.StaleOffer,
            OfferModel.JudgeBounty(dojo, contract, [ready.Id]).Refusal);
    }

    /// <summary>Tahtada sözleşmesiz günler var; ilk asılı olanı bulana kadar gün geçer.</summary>
    private static BountyContract OpenContract(DojoState dojo)
    {
        for (int guard = 0; guard < 30; guard++)
        {
            if (dojo.Bounty is BountyContract contract)
            {
                return contract;
            }

            dojo.Decline();
        }

        throw new InvalidOperationException("Tahtaya 30 günde sözleşme asılmadı.");
    }
}
