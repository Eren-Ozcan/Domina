using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>The day's offer — as the screen reads it.</summary>
/// <remarks>
/// The enemy roster is <b>not</b> here. The offer object carries it (so the fight is built with the
/// same roster) but the screen must not see it: per GDD §10 only the band and a rough description are
/// readable before going in. The model never carrying the roster also makes it impossible for the
/// screen to print it by accident.
/// </remarks>
/// <param name="Day">The day the offer is valid for.</param>
/// <param name="Threat">The readable threat band.</param>
/// <param name="Sighting">The rough description ("three kappa", say).</param>
/// <param name="RequiredPartySize">The party size imposed; <c>null</c> if there is none.</param>
/// <param name="MaxPartySize">The maximum warriors who can go on the expedition.</param>
/// <param name="PromisedReward">The gold paid if it is won — readable before going in.</param>
public readonly record struct OfferCard(
    int Day,
    ThreatBand Threat,
    string Sighting,
    int? RequiredPartySize,
    int MaxPartySize,
    int PromisedReward);

/// <summary>The contract on the board — as the screen reads it.</summary>
/// <param name="TargetName">The target's name; a contract is written against a single named creature.</param>
/// <param name="Patron">The party that issued the contract.</param>
/// <param name="Threat">The readable threat band.</param>
/// <param name="Reward">The gold promised.</param>
/// <param name="DaysLeft">The days left, the last day included.</param>
/// <param name="HonorReward">The honour the party earns if the head is brought in.</param>
/// <param name="BrokenHonorPenalty">The honour <b>the roster</b> loses if the promise is not kept.</param>
/// <param name="Accepted">Has the promise been given?</param>
public readonly record struct BountyCard(
    string TargetName,
    string Patron,
    ThreatBand Threat,
    int Reward,
    int DaysLeft,
    double HonorReward,
    double BrokenHonorPenalty,
    bool Accepted);

/// <summary>The row of a candidate who can be sent on the expedition.</summary>
/// <param name="Id">The warrior's identity; the screen's command returns it.</param>
/// <param name="Name">Display name.</param>
/// <param name="Fit">Can he be sent today?</param>
/// <param name="RecoveryDaysRemaining">The days left in the infirmary.</param>
/// <param name="Score">The total stat score — from the <b>effective</b> stats, disabilities included.</param>
public readonly record struct PartyCandidate(
    WarriorId Id,
    string Name,
    bool Fit,
    int RecoveryDaysRemaining,
    double Score);

/// <summary>The verdict on going on an expedition with the selected party.</summary>
/// <param name="Refusal">The reason for the refusal; <c>null</c> if it can be sent.</param>
/// <param name="Size">The number of warriors selected.</param>
public readonly record struct PartyVerdict(ExpeditionRefusal? Refusal, int Size)
{
    /// <summary>Can the party be sent today?</summary>
    public bool CanSend => Refusal is null;
}

/// <summary>
/// The model the day's offer screen reads. It decides what is shown and <b>knowing in advance that the
/// expedition will be refused</b>; it does not draw.
/// </summary>
/// <remarks>
/// <see cref="Expedition.Send"/> throws on an unfit party; the player should learn this from a dimmed
/// button, not from an exception. The verdict is read from <see cref="Expedition.Refuse"/> itself — if
/// the screen wrote a second set of rules the two sides would drift apart and the button would start
/// blocking an expedition that could be sent.
/// </remarks>
public static class OfferModel
{
    /// <summary>Today's offer.</summary>
    public static OfferCard Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        EncounterOffer offer = dojo.Offer;

        return new OfferCard(
            Day: offer.Day,
            Threat: offer.Threat,
            Sighting: offer.Sighting,
            RequiredPartySize: offer.RequiredPartySize,
            MaxPartySize: EncounterOffer.MaxPartySize,
            PromisedReward: dojo.Quartermaster.PromisedReward(new BattleSetup([], offer.Enemies)));
    }

    /// <summary>The card if there is a contract on the board today, otherwise <c>null</c>.</summary>
    public static BountyCard? DescribeBounty(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        if (dojo.Bounty is not BountyContract contract)
        {
            return null;
        }

        return new BountyCard(
            TargetName: contract.Target.Name,
            Patron: contract.Patron,
            Threat: contract.Threat,
            Reward: contract.Reward,
            DaysLeft: contract.DaysLeft(dojo.Day),
            HonorReward: contract.HonorReward,
            BrokenHonorPenalty: contract.BrokenHonorPenalty,
            Accepted: dojo.AcceptedBountyDay == contract.PostedDay);
    }

    /// <summary>Those who can be sent on the expedition first, then those in the infirmary.</summary>
    /// <remarks>
    /// The dead are never listed: they stay as records on the roster screen, they are not options here.
    /// Those in the infirmary are <b>visible but not selectable</b> — "there is nobody" and "everyone is
    /// in bed" must not be the same screen.
    /// </remarks>
    public static IReadOnlyList<PartyCandidate> Candidates(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        return dojo.Roster.Living
            .Select(entry => new PartyCandidate(
                entry.Id,
                entry.Name,
                entry.IsFitForCampaign,
                entry.RecoveryDaysRemaining,
                MarketModel.Score(entry.Warrior.EffectiveStats)))
            .OrderByDescending(c => c.Fit)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Can the selected party be sent against the day's offer?</summary>
    public static PartyVerdict Judge(DojoState dojo, IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        return Judge(dojo, dojo.Offer, party);
    }

    /// <summary>Can the selected party be sent against the contract?</summary>
    /// <remarks>
    /// A contract can be entered <b>without accepting it</b> (see
    /// <see cref="Expedition.SendToBounty"/>), so the verdict looks at the day rather than the promise:
    /// an expired contract cannot be sent against.
    /// </remarks>
    public static PartyVerdict JudgeBounty(
        DojoState dojo,
        BountyContract contract,
        IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(party);

        return contract.IsOpenOn(dojo.Day)
            ? Judge(dojo, contract.AsOffer(dojo.Day), party)
            : new PartyVerdict(ExpeditionRefusal.StaleOffer, party.Count);
    }

    /// <summary>Turns the selected ids into roster entries — the form the expedition layer wants.</summary>
    /// <remarks>An id not on the roster is not silently dropped, the verdict refuses it.</remarks>
    public static IReadOnlyList<RosterEntry> Party(DojoState dojo, IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        ArgumentNullException.ThrowIfNull(party);

        return [.. party.Select(dojo.Roster.Find).OfType<RosterEntry>()];
    }

    private static PartyVerdict Judge(
        DojoState dojo,
        EncounterOffer offer,
        IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(party);

        // If a missing id dropped off the list the party would look smaller and the verdict would write
        // the wrong reason; the count is read from the selection, and the roster entry as far as it can be found.
        List<RosterEntry> entries = [.. Party(dojo, party)];
        if (entries.Count != party.Count)
        {
            return new PartyVerdict(ExpeditionRefusal.NotInRoster, party.Count);
        }

        return new PartyVerdict(Expedition.Refuse(dojo, offer, entries), party.Count);
    }
}
