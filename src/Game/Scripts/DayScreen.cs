using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The day's screen: the offer, the contract, party selection and the buttons that close the day (GDD §10).
/// </summary>
/// <remarks>
/// <para>
/// What is shown, and whether the expedition is refused, is decided by <see cref="OfferModel"/>
/// (engine-free, tested). The side that sets up the fight and closes the day is
/// <see cref="Expedition"/> — this screen does not open a second door.
/// </para>
/// <para>
/// The enemy roster is <b>not</b> on screen: before going in, only the threat band and a rough
/// description are readable. The model never carries the roster, so it cannot be printed here by accident.
/// </para>
/// <para>
/// The fight is <b>watched in the arena</b>: the screen sets the fight up with <c>Expedition.Prepare</c>
/// and hands it to <see cref="Watcher"/>; the arena steps it in real time, and the raw result of the
/// finished fight is written to the roster with <c>Expedition.Settle</c>. The accounting lives in one
/// place — a watched fight leaves the same books as one resolved in batch simulation.
/// </para>
/// <para>
/// If no <see cref="Watcher"/> is given (the screen was opened on its own) the fight is resolved in the
/// background. The same two calls, only with no arena in between.
/// </para>
/// </remarks>
public sealed partial class DayScreen : DojoScreen
{
    private readonly HashSet<WarriorId> _party = [];
    private readonly Expedition _expedition = new();

    private DojoState _dojo = null!;
    private Label _offerLabel = null!;
    private Label _bountyLabel = null!;
    private VBoxContainer _partyList = null!;
    private Label _verdictLabel = null!;
    private Button _sendButton = null!;
    private Button _bountyButton = null!;
    private Button _acceptButton = null!;
    private Button _restButton = null!;
    private Label _log = null!;

    /// <summary>
    /// The side that will let the prepared fight be watched; <c>null</c> means the fight is resolved in the background.
    /// </summary>
    /// <remarks>
    /// This screen does not play the fight: the arena is a separate scene and the day screen closes and
    /// gives way to it. The side taking over returns <c>true</c>; if it does not, the screen resolves the
    /// fight itself, so the screen also works on its own.
    /// </remarks>
    public Func<PendingBattle, bool>? Watcher { get; set; }

    /// <summary>The report to print when coming back from the arena; written once when the screen is built.</summary>
    public string? Report { get; set; }

    /// <summary>Builds the screen and prints the day.</summary>
    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

        VBoxContainer page = BuildPage();

        _offerLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_offerLabel);

        _bountyLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_bountyLabel);

        _acceptButton = new Button { Text = "Accept the contract" };
        _acceptButton.Pressed += AcceptBounty;
        page.AddChild(_acceptButton);

        page.AddChild(new Label { Text = "Who goes on the expedition?" });

        ScrollContainer scroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        page.AddChild(scroll);

        _partyList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_partyList);

        _verdictLabel = new Label();
        page.AddChild(_verdictLabel);

        HBoxContainer buttons = new();
        buttons.AddThemeConstantOverride("separation", 12);
        page.AddChild(buttons);

        _sendButton = new Button { Text = "Take the offer" };
        _sendButton.Pressed += SendToOffer;
        buttons.AddChild(_sendButton);

        _bountyButton = new Button { Text = "Take the bounty" };
        _bountyButton.Pressed += SendToBounty;
        buttons.AddChild(_bountyButton);

        _restButton = new Button { Text = "Spend the day in the dojo" };
        _restButton.Pressed += Rest;
        buttons.AddChild(_restButton);

        _log = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _log.Text = Report ?? string.Empty;
        page.AddChild(_log);

        Refresh();
    }

    /// <summary>Reprints the day, the party and the buttons.</summary>
    public void Refresh()
    {
        OfferCard offer = OfferModel.Describe(_dojo);
        Resources purse = _dojo.Resources;

        _offerLabel.Text = string.Join(
            '\n',
            $"Day {_dojo.Day}  ·  Purse {purse.Gold} gold  ·  Food {purse.Food}" +
            $"  ·  Water {purse.Water}  ·  Medicine {purse.Medicine}",
            $"Offer: {offer.Sighting}",
            $"Threat: {ThreatName(offer.Threat)}  ·  Promised reward {offer.PromisedReward} gold",
            offer.RequiredPartySize is int size
                ? $"This job wants exactly {size}."
                : $"Party of at most {offer.MaxPartySize}.");

        BuildPartyList();
        ShowBounty(OfferModel.DescribeBounty(_dojo));
        UpdateButtons();
    }

    private void BuildPartyList()
    {
        Clear(_partyList);
        IReadOnlyList<PartyCandidate> candidates = OfferModel.Candidates(_dojo);

        // Those who dropped off the roster must not stay selected: if a dead or wounded warrior stays in
        // the selection, the verdict says "not on the roster" and the button looks disabled for no reason.
        _party.IntersectWith(candidates.Where(c => c.Fit).Select(c => c.Id));

        if (candidates.Count == 0)
        {
            _partyList.AddChild(new Label { Text = "Nobody left on the roster." });
            return;
        }

        foreach (PartyCandidate candidate in candidates)
        {
            CheckBox box = new()
            {
                Text = candidate.Fit
                    ? $"{candidate.Name}  —  power {candidate.Score:0}"
                    : $"{candidate.Name}  —  infirmary {candidate.RecoveryDaysRemaining} days",
                Disabled = !candidate.Fit,
                ButtonPressed = _party.Contains(candidate.Id),
            };
            box.AddThemeColorOverride("font_color", candidate.Fit ? InkColor : MutedColor);

            WarriorId id = candidate.Id;
            box.Toggled += pressed =>
            {
                if (pressed)
                {
                    _party.Add(id);
                }
                else
                {
                    _party.Remove(id);
                }

                UpdateButtons();
            };

            _partyList.AddChild(box);
        }
    }

    private void ShowBounty(BountyCard? card)
    {
        if (card is not BountyCard bounty)
        {
            _bountyLabel.Text = "No contract on the board today.";
            _bountyLabel.AddThemeColorOverride("font_color", MutedColor);
            return;
        }

        _bountyLabel.Text = string.Join(
            '\n',
            $"Contract: {bounty.TargetName}  ·  {bounty.Patron}",
            $"Threat: {ThreatName(bounty.Threat)}  ·  Reward {bounty.Reward} gold" +
            $"  ·  Time {bounty.DaysLeft} days",
            bounty.Accepted
                ? $"The promise is given. If it is not kept the roster loses {bounty.BrokenHonorPenalty:0} honour."
                : $"Accepting costs no day, it buys time. The party that brings the head gains {bounty.HonorReward:0} honour.");
        _bountyLabel.AddThemeColorOverride(
            "font_color",
            bounty.Accepted ? PendingColor : InkColor);
    }

    private void UpdateButtons()
    {
        List<WarriorId> party = [.. _party];
        PartyVerdict offer = OfferModel.Judge(_dojo, party);
        BountyContract? contract = _dojo.Bounty;
        PartyVerdict bounty = contract is null
            ? new PartyVerdict(ExpeditionRefusal.StaleOffer, party.Count)
            : OfferModel.JudgeBounty(_dojo, contract, party);

        _sendButton.Disabled = !offer.CanSend;
        _bountyButton.Disabled = !bounty.CanSend;
        _bountyButton.Visible = contract is not null;
        _acceptButton.Visible = contract is not null;
        _acceptButton.Disabled = contract is null || _dojo.AcceptedBountyDay is not null;
        _restButton.Disabled = false;

        _verdictLabel.Text = offer.Refusal is ExpeditionRefusal refusal
            ? RefusalText(refusal)
            : $"Party ready: {party.Count}.";
        _verdictLabel.AddThemeColorOverride(
            "font_color",
            offer.CanSend ? GoodColor : WarningColor);
    }

    private void SendToOffer()
    {
        List<WarriorId> chosen = [.. _party];
        if (!OfferModel.Judge(_dojo, chosen).CanSend)
        {
            return;
        }

        DojoState dojo = _dojo;
        EncounterOffer offer = dojo.Offer;
        List<RosterEntry> party = [.. OfferModel.Party(dojo, chosen)];
        BattleSetup setup = Expedition.Prepare(dojo, offer, party, collectEvents: true);

        Fight(new PendingBattle(
            setup,
            BattleSeed(),
            battle => Log(new Expedition().Settle(dojo, setup, battle), dojo)));
    }

    private void SendToBounty()
    {
        if (_dojo.Bounty is not BountyContract contract)
        {
            return;
        }

        List<WarriorId> chosen = [.. _party];
        if (!OfferModel.JudgeBounty(_dojo, contract, chosen).CanSend)
        {
            return;
        }

        DojoState dojo = _dojo;
        List<RosterEntry> party = [.. OfferModel.Party(dojo, chosen)];
        BattleSetup setup = Expedition.PrepareBounty(dojo, contract, party, collectEvents: true);

        Fight(new PendingBattle(
            setup,
            BattleSeed(),
            battle => Log(
                new Expedition().SettleBounty(dojo, contract, party, setup, battle),
                contract,
                dojo)));
    }

    /// <summary>
    /// Lets the fight be watched; if there is nobody to watch it, resolves it here.
    /// </summary>
    /// <remarks>
    /// In a watched fight this screen closes and the report is printed on the screen returning from the
    /// arena; that is why the callback that closes the books <b>does not touch this node</b>, it only
    /// produces text.
    /// </remarks>
    private void Fight(PendingBattle bout)
    {
        if (Watcher?.Invoke(bout) == true)
        {
            return;
        }

        _log.Text = bout.Settle(new Battle(bout.Setup, new SeededRandom(bout.Seed)).Run());
        AfterDay();
    }

    private static string Log(ExpeditionResult result, DojoState dojo) => string.Join(
        '\n',
        $"{OutcomeText(result.Aftermath.Outcome)}  ·  {result.Reward} gold went into the purse.",
        AftermathText(result.Aftermath, dojo),
        DayText(result.Day));

    private static string Log(BountyResult result, BountyContract contract, DojoState dojo) =>
        string.Join(
            '\n',
            $"{OutcomeText(result.Aftermath.Outcome)}  ·  {result.Reward} gold went into the purse." +
            (result.Claimed ? $"  The head was taken: {contract.Target.Name}." : "  The head was not taken."),
            AftermathText(result.Aftermath, dojo),
            DayText(result.Day));

    private void AcceptBounty()
    {
        if (_dojo.AcceptBounty() is BountyContract contract)
        {
            _log.Text = $"The promise is given: {contract.Target.Name}, last day {contract.Deadline}.";
            Persist();
        }

        Refresh();
    }

    private void Rest()
    {
        DayReport report = _dojo.Decline();
        _log.Text = $"The day passed in the dojo.\n{DayText(report)}";
        AfterDay();
    }

    private void AfterDay()
    {
        _party.Clear();
        Persist();
        Refresh();
    }

    /// <summary>
    /// The expedition's stream — derived from the day and the seed.
    /// </summary>
    /// <remarks>
    /// So that the same save gives the same fight on the same day, it is the <b>day</b> that is mixed in
    /// rather than the clock: a random seed would open the door to "load the save and reroll the
    /// fight".
    /// </remarks>
    private ulong BattleSeed() => _dojo.Seed ^ ((ulong)_dojo.Day * 0x9E3779B97F4A7C15);

    private static string DayText(DayReport report)
    {
        List<string> lines =
        [
            $"Day {report.Day} closed. {report.Upkeep.GoldSpent} gold paid for supplies.",
        ];

        if (report.Event is DayEvent happening)
        {
            lines.Add($"Setback: {happening.Description}");
        }

        if (report.Upkeep.Hungry.Count > 0)
        {
            lines.Add($"{report.Upkeep.Hungry.Count} warriors went hungry — they did not advance that day.");
        }

        if (report.Recovered.Count > 0)
        {
            lines.Add($"{report.Recovered.Count} warriors left the infirmary.");
        }

        if (report.BountyBroken)
        {
            lines.Add("The promise was broken: the roster lost honour.");
        }

        return string.Join('\n', lines);
    }

    private static string AftermathText(AftermathReport aftermath, DojoState dojo)
    {
        List<string> lines = [];

        foreach (WarriorAftermath warrior in aftermath.Warriors)
        {
            string name = dojo.Roster.Find(warrior.Id)?.Name ?? "Warrior";
            if (warrior.Died)
            {
                lines.Add($"{name} died.");
                continue;
            }

            if (warrior.LostParts.Count > 0)
            {
                lines.Add($"{name} came back permanently maimed.");
            }

            if (warrior.RecoveryDays > 0)
            {
                lines.Add($"{name} will spend {warrior.RecoveryDays} days in the infirmary.");
            }
        }

        return lines.Count == 0 ? "The party came back without a scratch." : string.Join('\n', lines);
    }

    private static string OutcomeText(BattleOutcome outcome) => outcome switch
    {
        BattleOutcome.PlayerVictory => "Victory.",
        BattleOutcome.PlayerWithdrawal => "The party left the field.",
        BattleOutcome.PlayerWipe => "The party was wiped out.",
        _ => "Time ran out; nobody finished it.",
    };

    private static string RefusalText(ExpeditionRefusal refusal) => refusal switch
    {
        ExpeditionRefusal.EmptyParty => "Nobody selected.",
        ExpeditionRefusal.StaleOffer => "This is not today's offer.",
        ExpeditionRefusal.WrongPartySize => "The party size does not fit this job.",
        ExpeditionRefusal.Unfit => "One of those selected is not fit for an expedition.",
        _ => "One of those selected is not on the roster.",
    };

    private static string ThreatName(ThreatBand threat) => threat switch
    {
        ThreatBand.Faint => "patrol work",
        ThreatBand.Rising => "an ordinary day",
        ThreatBand.Heavy => "the roster should prepare",
        _ => "a high risk of death",
    };
}

/// <summary>A fight that has been set up but not yet run.</summary>
/// <remarks>
/// The three pieces have to travel together: the fight's inputs, its seed and the call that <b>closes
/// the books</b>. Given separately, the arena could write a finished fight to the wrong expedition.
/// </remarks>
/// <param name="Setup">The fight's inputs — <c>Expedition.Prepare</c> built them.</param>
/// <param name="Seed">The fight's seed; the same day gives the same fight.</param>
/// <param name="Settle">
/// Closes the books of the finished fight and returns the report to print on screen.
/// </param>
public sealed record PendingBattle(BattleSetup Setup, ulong Seed, Func<BattleResult, string> Settle);
