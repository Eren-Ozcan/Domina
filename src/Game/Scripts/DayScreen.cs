using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Honor;
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
    /// <summary>The clock's hold while a party is being picked.</summary>
    private const string PartyHold = "party";

    private readonly HashSet<WarriorId> _party = [];
    private readonly Expedition _expedition = new();

    private DojoState _dojo = null!;
    private Label _seasonLabel = null!;
    private Label _offerLabel = null!;
    private Label _readingLabel = null!;
    private VBoxContainer _patronRows = null!;
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

        // The season's line stands above everything: the countdown, the rival's next move, this week's
        // compulsory fight and the head gate are the four things every other decision on this screen is
        // made against (docs/GDD.md §10).
        _seasonLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_seasonLabel);

        _offerLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_offerLabel);

        // The diviner's reading sits directly under the offer it reads, and hides itself when the dojo
        // has no hut: an empty panel would advertise the information it is withholding.
        _readingLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        page.AddChild(_readingLabel);

        // The three parties sit on the day screen because that is where their work arrives: a contract
        // is taken here, and what the tiers are worth is read against the offer standing beside them.
        _patronRows = new VBoxContainer();
        _patronRows.AddThemeConstantOverride("separation", 4);
        page.AddChild(_patronRows);

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

        // With the clock running this is no longer how a day is spent — it is how a day is skipped.
        // The dojo works through the day either way; this only refuses to wait for it.
        _restButton = new Button { Text = "Skip to tomorrow" };
        _restButton.Pressed += Rest;
        buttons.AddChild(_restButton);

        _log = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _log.Text = Report ?? string.Empty;
        page.AddChild(_log);

        Refresh();
    }

    /// <summary>Reprints the day, the party and the buttons.</summary>
    public override void Refresh()
    {
        OfferCard offer = OfferModel.Describe(_dojo);
        Resources purse = _dojo.Resources;

        SeasonBanner banner = SeasonModel.Describe(_dojo);
        _seasonLabel.Text = SeasonModel.Line(banner);
        _seasonLabel.AddThemeColorOverride(
            "font_color",
            banner.AtRisk ? PendingColor : banner.GateOpen ? GoodColor : InkColor);

        _offerLabel.Text = string.Join(
            '\n',
            $"Day {_dojo.Day}  ·  Purse {purse.Gold} gold  ·  Food {purse.Food}" +
            $"  ·  Water {purse.Water}  ·  Medicine {purse.Medicine}  ·  Sake {purse.Sake}" +
            $"  ·  Spirits {RosterModel.Band(RosterModel.Summarize(_dojo).Morale).ToString().ToLowerInvariant()}",
            $"Offer: {offer.Sighting}",
            $"Threat: {ThreatName(offer.Threat)}  ·  Promised reward {offer.PromisedReward} gold",
            offer.RequiredPartySize is int size
                ? $"This job wants exactly {size}."
                : $"Party of at most {offer.MaxPartySize}.");

        ShowReading(OfferModel.ReadOffer(_dojo));
        BuildPatronRows();

        BuildPartyList();
        ShowBounty(OfferModel.DescribeBounty(_dojo));
        UpdateButtons();
    }

    /// <summary>Prints what the diviner's hut could read off today's offer.</summary>
    /// <remarks>
    /// The label is emptied and hidden when there is no hut: a dojo that has not bought the reading
    /// must not see an empty panel where the numbers would be, or the screen would be advertising what
    /// it is withholding.
    /// </remarks>
    private void ShowReading(IReadOnlyList<EnemyLine> lines)
    {
        _readingLabel.Visible = lines.Count > 0;
        if (lines.Count == 0)
        {
            _readingLabel.Text = string.Empty;
            return;
        }

        List<string> rows = ["The hut reads the road:"];
        foreach (EnemyLine line in lines)
        {
            rows.Add(line.Stats is null
                ? $"  · {line.Name} — {line.Weapon}"
                : $"  · {line.Name} — {line.Weapon} — {line.Stats}");
        }

        _readingLabel.Text = string.Join('\n', rows);
    }

    /// <summary>The three parties, what they are worth today, and the gift button.</summary>
    /// <remarks>
    /// A gift is the only thing on this screen that buys a relationship with gold rather than with
    /// work, and each one is worth less than the last — so the button says the price and the panel
    /// says the tier, and the diminishing return is left for the player to notice.
    /// </remarks>
    private void BuildPatronRows()
    {
        Clear(_patronRows);

        foreach (PatronCard card in OfferModel.Patrons(_dojo))
        {
            HBoxContainer line = new();
            line.AddThemeConstantOverride("separation", 6);

            Label name = new()
            {
                Text = $"{card.Name} — {OfferModel.TierName(card.Tier)} · {card.Effect}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            name.AddThemeColorOverride("font_color", TierColor(card.Tier));
            line.AddChild(name);

            Patron patron = card.Patron;
            Button gift = new() { Text = $"Send a gift ({card.GiftPrice})", Disabled = !card.CanGift };
            gift.Pressed += () =>
            {
                if (_dojo.SendGift(patron))
                {
                    Persist();
                }

                Refresh();
            };

            line.AddChild(gift);
            _patronRows.AddChild(line);
        }
    }

    private static Color TierColor(StandingTier tier) => tier switch
    {
        StandingTier.Hostile => WarningColor,
        StandingTier.Cold => PendingColor,
        StandingTier.Pleased or StandingTier.Loyal => GoodColor,
        _ => InkColor,
    };

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

                // A man picked is a decision half made: the clock waits rather than letting the
                // morning arrive on top of it. It runs again the moment the selection is empty
                // (build step 8).
                if (_party.Count > 0)
                {
                    Clock?.Hold(PartyHold);
                }
                else
                {
                    Clock?.Release(PartyHold);
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

        // The clock did not turn this day, the button did: the next morning starts at the morning
        // rather than at whatever fraction of the skipped day was left on it.
        Clock?.Restart();
        AfterDay();
    }

    /// <summary>
    /// Lets the clock go as the screen is taken down.
    /// </summary>
    /// <remarks>
    /// The party hold outlives this node otherwise: the screen is freed on a tab change and when the
    /// arena opens, and a hold nobody owns any more would stop the season for the rest of the run.
    /// </remarks>
    public override void _ExitTree() => Clock?.Release(PartyHold);

    /// <summary>Prints a day the clock closed while the player was standing on this screen.</summary>
    /// <remarks>
    /// The hub is the side that turns those days — this screen only shows what they said. An empty
    /// text is ignored so that a quiet frame does not wipe the report the player is reading.
    /// </remarks>
    public void Note(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            _log.Text = text;
        }
    }

    private void AfterDay()
    {
        _party.Clear();
        Clock?.Release(PartyHold);
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

    /// <summary>
    /// The day's own news.
    /// </summary>
    /// <remarks>
    /// The text itself lives in <see cref="DayLog"/>: with the clock running, days also turn while the
    /// player is standing on another screen, and the hub prints those. One formatter, so a day reads
    /// the same wherever it was spent.
    /// </remarks>
    private static string DayText(DayReport report) => DayLog.Line(report);

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
        _ => "The fight stalled and was broken off.",
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
