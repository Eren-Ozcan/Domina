using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>One line of the terms: a label, what it says, and whether it is a cost.</summary>
/// <param name="Label">The term's own name — "gold", "the day", "the way back".</param>
/// <param name="Value">What it says today.</param>
/// <param name="Grave">A cost or a risk rather than a gain; the sheet prints it in the darker ink.</param>
public readonly record struct SortieTerm(string Label, string Value, bool Grave = false);

/// <summary>
/// The terms of going out, read once before the gate opens.
/// </summary>
/// <param name="Heading">What the job is.</param>
/// <param name="Line">The road, in a sentence.</param>
/// <param name="Pays">What winning it is worth.</param>
/// <param name="Costs">What taking it costs, and what it risks.</param>
/// <param name="Party">The men chosen, in the order they were picked off the roster.</param>
/// <param name="Enemies">
/// What the hut read off the road. Empty when the dojo has no diviner — which is not the same as an
/// empty road, and the sheet has to say so.
/// </param>
/// <param name="Read">Was the road read at all?</param>
/// <param name="Threat">The readable threat band, for the colour the sheet prints the road in.</param>
public readonly record struct SortieSheet(
    string Heading,
    string Line,
    IReadOnlyList<SortieTerm> Pays,
    IReadOnlyList<SortieTerm> Costs,
    IReadOnlyList<RosterRow> Party,
    IReadOnlyList<EnemyLine> Enemies,
    bool Read,
    ThreatBand Threat);

/// <summary>
/// The sheet the player reads before sending men out.
/// </summary>
/// <remarks>
/// <para>
/// The day's board already prints the offer's terms, but they are read there in the middle of every
/// other decision the morning carries, and the party is chosen in a list of names. The moment of
/// sending is the one place the whole bargain has to be in front of the player at once: what it pays,
/// what it costs, who is going and what is on the road — with the men and the road standing facing
/// each other rather than written in two paragraphs.
/// </para>
/// <para>
/// The reference game puts the same thing on one page and calls it the terms of the bout
/// (docs/REFERENCE-DOMINA-UI.md). What is taken from it is the <b>layout</b> — the bargain above, the
/// two sides facing below, the two answers at the foot — and not its widgets.
/// </para>
/// <para>
/// It decides nothing. Whether the party may go is
/// <see cref="OfferModel.Judge(DojoState, IReadOnlyList{WarriorId})"/>'s to say, and it has already
/// said so by the time this sheet opens; nothing here may refuse a party the expedition layer accepts.
/// </para>
/// </remarks>
public static class SortieModel
{
    /// <summary>The terms of today's offer, with these men going.</summary>
    public static SortieSheet Describe(DojoState dojo, IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        ArgumentNullException.ThrowIfNull(party);

        OfferCard offer = OfferModel.Describe(dojo);
        IReadOnlyList<EnemyLine> read = OfferModel.ReadOffer(dojo);

        List<SortieTerm> pays = [new SortieTerm("gold", $"{offer.PromisedReward}")];

        if (offer.IsStanding)
        {
            pays.Add(new SortieTerm(
                "standing job",
                $"posted {offer.AgeDays} days ago — it paid {offer.FullReward} on the day",
                Grave: true));
        }

        return new SortieSheet(
            Heading: offer.IsRaid ? "They are coming to the gate" : "The road out",
            Line: Road(offer.Sighting, offer.Threat),
            Pays: pays,
            Costs: Costs(dojo, party.Count, offer.RequiredPartySize, offer.MaxPartySize, offer.LastDay),
            Party: Men(dojo, party),
            Enemies: read,
            Read: read.Count > 0,
            Threat: offer.Threat);
    }

    /// <summary>The terms of a promise already given — the head the dojo is bound to bring in.</summary>
    public static SortieSheet Describe(DojoState dojo, BountyCard bounty, IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        ArgumentNullException.ThrowIfNull(party);

        OfferCard offer = OfferModel.Describe(dojo);

        List<SortieTerm> pays =
        [
            new SortieTerm("gold", $"{bounty.Reward}"),
            new SortieTerm("honour", $"+{bounty.HonorReward:0.0} to the man who takes the head"),
            new SortieTerm("the party", $"{bounty.Patron} is watching this one"),
        ];

        List<SortieTerm> costs =
        [
            .. Costs(dojo, party.Count, null, offer.MaxPartySize, bounty.DaysLeft <= 1),
            new SortieTerm(
                "the promise",
                $"broken, the roster loses {bounty.BrokenHonorPenalty:0.0} honour",
                Grave: true),
        ];

        return new SortieSheet(
            Heading: $"The head of {bounty.TargetName}",
            Line: Road($"{bounty.TargetName}, and whatever stands with him", bounty.Threat),
            Pays: pays,
            Costs: costs,
            Party: Men(dojo, party),
            Enemies: [],
            Read: false,
            Threat: bounty.Threat);
    }

    /// <summary>The words a threat band is read in. The band is never printed as a number.</summary>
    public static string ThreatName(ThreatBand threat) => threat switch
    {
        ThreatBand.Faint => "patrol work",
        ThreatBand.Rising => "an ordinary day",
        ThreatBand.Heavy => "the roster should prepare",
        _ => "men do not always come back from this one",
    };

    private static string Road(string sighting, ThreatBand threat) =>
        $"{sighting} — {ThreatName(threat)}.";

    /// <summary>
    /// What going out takes, whatever the job is.
    /// </summary>
    /// <remarks>
    /// The day's draw is read off <see cref="DojoState.DailyDraw"/>, the same arithmetic the morning
    /// charges, so the sheet cannot promise a cheaper day than the one the books will take.
    /// </remarks>
    private static List<SortieTerm> Costs(
        DojoState dojo,
        int going,
        int? required,
        int most,
        bool lastDay)
    {
        Resources draw = dojo.DailyDraw();

        return
        [
            new SortieTerm("the day", $"one day · {DrawText(draw)}", Grave: true),
            new SortieTerm(
                "the party",
                required is int size
                    ? $"{going} going — exactly {size} {(size == 1 ? "is" : "are")} asked for"
                    : $"{going} going, of at most {most}"),
            new SortieTerm(
                "the way back",
                "you may pull the party at any moment; a man mid-strike leaves when the strike finishes"),
            new SortieTerm(
                "the offer",
                lastDay ? "the last day it can be taken" : "it will still be there tomorrow",
                Grave: lastDay),
        ];
    }

    /// <summary>The chosen men as the roster describes them, so the sheet prints one man one way.</summary>
    private static List<RosterRow> Men(DojoState dojo, IReadOnlyList<WarriorId> party)
    {
        Dictionary<WarriorId, RosterRow> rows = RosterModel.Describe(dojo).ToDictionary(row => row.Id);

        return [.. party.Where(rows.ContainsKey).Select(id => rows[id])];
    }

    private static string DrawText(Resources draw)
    {
        List<string> parts = [];

        if (draw.Gold > 0)
        {
            parts.Add($"{draw.Gold} gold");
        }

        if (draw.Food > 0)
        {
            parts.Add($"{draw.Food} food");
        }

        if (draw.Water > 0)
        {
            parts.Add($"{draw.Water} water");
        }

        return parts.Count == 0 ? "the store is not drawn" : string.Join(", ", parts);
    }
}
