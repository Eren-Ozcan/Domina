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
    /// <inheritdoc/>
    protected override string SheetTitle => "the board";

    /// <inheritdoc/>
    protected override string SheetLine =>
        "Work is posted here each morning. Take what the day offers, choose who walks it, and nothing else in this yard earns you anything.";

    /// <summary>The clock's hold while a party is being picked.</summary>
    private const string PartyHold = "party";

    private readonly HashSet<WarriorId> _party = [];
    private readonly Expedition _expedition = new();

    private DojoState _dojo = null!;
    private Label _seasonLabel = null!;
    private HFlowContainer _storeRow = null!;
    private Label _offerLabel = null!;
    private VBoxContainer _offerTerms = null!;
    private VBoxContainer _bountyTerms = null!;
    private Label _readingLabel = null!;
    private VBoxContainer _patronRows = null!;
    private Label _bountyLabel = null!;
    private Label _partyLine = null!;
    private int _partyLimit;
    private Label _verdictLabel = null!;
    private Button _sendButton = null!;
    private SortieScreen? _terms;
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

        // The store gets chips rather than a line of text, and each chip carries what the day takes off
        // it. A stock with no trend is the thing the reference game's interface does worst: it writes
        // "800 food" and never how much melts a day, which is the pressure the whole season is built on.
        _storeRow = UiKit.ChipRow();
        page.AddChild(_storeRow);

        // The board is read the way the reference game's contract sheet is read: the terms of the work
        // stand in one column and our own side stands in the other, so what a job pays and who would
        // walk it are compared without scrolling between them (docs/REFERENCE-DOMINA-UI.md §7).
        HBoxContainer columns = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        page.AddChild(columns);

        VBoxContainer termsColumn = UiKit.Section(columns, "the work posted today", fill: true, ratio: 1.25f);

        // The terms are longer than the sheet on a short window — the parties were falling off the
        // bottom of it — so the column scrolls inside its own panel rather than pushing the sheet.
        ScrollContainer termsScroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        termsColumn.AddChild(termsScroll);

        VBoxContainer terms = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        terms.AddThemeConstantOverride("separation", 9);
        termsScroll.AddChild(terms);

        _offerLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        terms.AddChild(_offerLabel);

        // What a job pays was on the board from the start; what taking it costs was not. The reference
        // prints the reward and the participation cost as one aligned pair, and a reward with no cost
        // beside it is a number the player cannot weigh.
        _offerTerms = new VBoxContainer();
        terms.AddChild(_offerTerms);

        // The diviner's reading sits directly under the offer it reads, and hides itself when the dojo
        // has no hut: an empty panel would advertise the information it is withholding.
        _readingLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        terms.AddChild(_readingLabel);

        terms.AddChild(UiKit.Rule());
        terms.AddChild(UiKit.SectionLabel("the contract"));

        _bountyLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        terms.AddChild(_bountyLabel);

        _bountyTerms = new VBoxContainer();
        terms.AddChild(_bountyTerms);

        _acceptButton = new Button { Text = "Accept the contract" };
        _acceptButton.Pressed += Guarded(_dojo, AcceptBounty);
        terms.AddChild(UiKit.WayOut(_acceptButton));

        terms.AddChild(UiKit.Rule());
        terms.AddChild(UiKit.SectionLabel("the parties"));

        // The three parties sit on the day screen because that is where their work arrives: a contract
        // is taken here, and what the tiers are worth is read against the offer standing beside them.
        _patronRows = new VBoxContainer();
        _patronRows.AddThemeConstantOverride("separation", 4);
        terms.AddChild(_patronRows);

        VBoxContainer ours = UiKit.Section(columns, null, fill: true);

        // The men are no longer ticked here. Picking a party and reading what the road pays were two
        // screens apart, which asked the player to choose his men before anything had told him what the
        // job was worth; both now happen on the terms sheet (SortieScreen), which this button opens.
        _partyLine = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        ours.AddChild(_partyLine);

        _verdictLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        ours.AddChild(_verdictLabel);

        // The acts stand in a column under the party they act on rather than in a row across the foot
        // of the sheet: in a row the third of them was pushed off the paper's edge.
        VBoxContainer buttons = new();
        buttons.AddThemeConstantOverride("separation", 8);
        ours.AddChild(buttons);

        // The one act the sheet exists for takes the indigo, and it is the only indigo on it: sending
        // men out is what the board is for, and a second filled act would make the player choose twice
        // (design canvas → 7a).
        _sendButton = new Button { Text = "Send them out" };
        _sendButton.Pressed += Guarded(_dojo, SendToOffer);
        buttons.AddChild(UiKit.Act(_sendButton));

        _bountyButton = new Button { Text = "Take the bounty" };
        _bountyButton.Pressed += Guarded(_dojo, SendToBounty);
        buttons.AddChild(UiKit.WayOut(_bountyButton));

        // With the clock running this is no longer how a day is spent — it is how a day is skipped.
        // The dojo works through the day either way; this only refuses to wait for it.
        _restButton = new Button { Text = "Skip to tomorrow" };
        _restButton.Pressed += Guarded(_dojo, Rest);
        buttons.AddChild(UiKit.WayOut(_restButton));

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

        BuildStoreRow(purse);

        _offerLabel.Text = offer.Sighting;

        // A job that wants exactly three is counted against three; otherwise the limit is the ceiling.
        _partyLimit = offer.RequiredPartySize ?? offer.MaxPartySize;

        ShowOfferTerms(offer);

        ShowReading(OfferModel.ReadOffer(_dojo));
        BuildPatronRows();

        ShowBounty(OfferModel.DescribeBounty(_dojo));
        UpdateButtons();
    }

    /// <summary>The offer's terms: the threat, what it pays, what walking it costs, and how long it stands.</summary>
    /// <remarks>
    /// The cost is the day the expedition spends, priced in what that day draws off the store — the
    /// same arithmetic the morning charges (<see cref="DojoState.DailyDraw"/>), so the figure beside
    /// the reward cannot drift from what going out actually takes.
    /// </remarks>
    private void ShowOfferTerms(OfferCard offer)
    {
        Clear(_offerTerms);
        GridContainer grid = UiKit.Terms(_offerTerms);

        UiKit.Term(grid, "threat", ThreatName(offer.Threat), Mark.Blade, ThreatColor(offer.Threat));
        UiKit.Term(
            grid,
            "reward",
            offer.IsStanding
                ? $"{offer.PromisedReward} gold — {offer.FullReward} on the day it was posted"
                : $"{offer.PromisedReward} gold",
            Mark.Coin,
            offer.IsStanding ? PendingColor : GoodColor);

        Resources draw = _dojo.DailyDraw();
        UiKit.Term(grid, "setting out", $"one day · {DrawText(draw)}", Mark.Grain);

        UiKit.Term(
            grid,
            "party",
            offer.RequiredPartySize is int size ? $"exactly {size}" : $"at most {offer.MaxPartySize}",
            Mark.Person);

        UiKit.Term(
            grid,
            "stands",
            offer.LastDay ? "the last day it can be taken" : $"{offer.DaysLeft} more days",
            Mark.None,
            offer.LastDay ? WarningColor : MutedColor);
    }

    /// <summary>What one day takes off the store, as a single line.</summary>
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

    private static Color ThreatColor(ThreatBand threat) => threat switch
    {
        ThreatBand.Faint => GoodColor,
        ThreatBand.Rising => InkColor,
        ThreatBand.Heavy => PendingColor,
        _ => WarningColor,
    };

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
            gift.Pressed += Guarded(_dojo, () =>
            {
                if (_dojo.SendGift(patron))
                {
                    Persist();
                }

                Refresh();
            });

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

    /// <summary>The store, as one chip an item, with what a day takes off it under the figure.</summary>
    /// <remarks>
    /// The draw comes from <see cref="DojoState.DailyDraw"/> — the same arithmetic the morning charges —
    /// so the trend under a figure cannot drift from what the day actually takes. The state bar is lit
    /// off the days the stock has left rather than off the figure: "3 medicine" says nothing until it is
    /// set against two men in the infirmary.
    /// </remarks>
    private void BuildStoreRow(Resources purse)
    {
        Clear(_storeRow);
        Resources draw = _dojo.DailyDraw();

        _storeRow.AddChild(UiKit.Chip(
            $"{purse.Gold}",
            "gold",
            draw.Gold > 0 && purse.Gold < draw.Gold * 3 ? UiKit.Warning : null,
            Mark.Coin,
            draw.Gold > 0 ? $"−{draw.Gold} / day" : "no wages owed"));

        _storeRow.AddChild(StoreChip($"{purse.Food}", "food", purse.Food, draw.Food, Mark.Grain));
        _storeRow.AddChild(StoreChip($"{purse.Water}", "water", purse.Water, draw.Water, Mark.Drop));
        _storeRow.AddChild(StoreChip($"{purse.Medicine}", "medicine", purse.Medicine, draw.Medicine, Mark.Cross));

        // Sake has no daily drain by design (GDD §11): it sits until a feast is called, so its line says
        // what it is for rather than inventing a rate for it.
        _storeRow.AddChild(UiKit.Chip($"{purse.Sake}", "sake", null, Mark.Cup, "kept for a feast"));

        RosterSummary summary = RosterModel.Summarize(_dojo);
        _storeRow.AddChild(UiKit.Chip(
            $"{summary.Living}/{summary.Beds}",
            "on the mat",
            null,
            Mark.Shield,
            $"spirits {RosterModel.Band(summary.Morale).ToString().ToLowerInvariant()}"));
    }

    /// <summary>A stock chip whose state and trend are read off the days it has left.</summary>
    private static Control StoreChip(string figure, string name, int stock, int draw, Mark mark)
    {
        if (draw <= 0)
        {
            return UiKit.Chip(figure, name, null, mark, "nothing drawn");
        }

        int days = stock / draw;
        Color? state = days switch
        {
            <= 2 => UiKit.Warning,
            <= 6 => UiKit.Pending,
            _ => null,
        };

        return UiKit.Chip(figure, name, state, mark, $"−{draw} / day · {days}d");
    }

    private void ShowBounty(BountyCard? card)
    {
        Clear(_bountyTerms);

        if (card is not BountyCard bounty)
        {
            _bountyLabel.Text = "No contract on the board today.";
            _bountyLabel.AddThemeColorOverride("font_color", MutedColor);
            return;
        }

        _bountyLabel.Text = bounty.Accepted
            ? $"The promise is given: {bounty.TargetName}."
            : $"{bounty.TargetName} — {bounty.Patron}";
        _bountyLabel.AddThemeColorOverride("font_color", bounty.Accepted ? PendingColor : InkColor);

        GridContainer grid = UiKit.Terms(_bountyTerms);
        UiKit.Term(grid, "issued by", bounty.Patron, Mark.None, MutedColor);
        UiKit.Term(grid, "threat", ThreatName(bounty.Threat), Mark.Blade, ThreatColor(bounty.Threat));
        UiKit.Term(grid, "reward", $"{bounty.Reward} gold", Mark.Coin, GoodColor);
        UiKit.Term(
            grid,
            "the head is worth",
            $"{bounty.HonorReward:0} honour to the party that brings it",
            Mark.Person,
            GoodColor);
        UiKit.Term(
            grid,
            "time",
            bounty.DaysLeft <= 1 ? "the last day" : $"{bounty.DaysLeft} days",
            Mark.None,
            bounty.DaysLeft <= 1 ? WarningColor : MutedColor);
        UiKit.Term(
            grid,
            "if the promise breaks",
            bounty.Accepted
                ? $"the roster loses {bounty.BrokenHonorPenalty:0} honour"
                : $"once given, {bounty.BrokenHonorPenalty:0} honour off the roster",
            Mark.None,
            WarningColor);
    }

    /// <summary>
    /// Dresses the day's three acts.
    /// </summary>
    /// <remarks>
    /// The party's own verdict is not read here any more: the men are ticked on the terms sheet, which
    /// prints the refusal beside its own act. What this screen still has to say is whether there is
    /// anybody to send at all — an act opening a sheet with an empty roster behind it is a door onto
    /// nothing.
    /// </remarks>
    private void UpdateButtons()
    {
        int fit = OfferModel.Candidates(_dojo).Count(candidate => candidate.Fit);
        BountyContract? contract = _dojo.Bounty;

        _bountyButton.Visible = contract is not null;
        _acceptButton.Visible = contract is not null;
        _acceptButton.Disabled = contract is null || _dojo.AcceptedBountyDay is not null;
        _restButton.Disabled = false;

        _partyLine.Text = _party.Count > 0
            ? $"{_party.Count} of {_partyLimit} ticked — the men are chosen on the terms sheet."
            : $"Up to {_partyLimit} may walk this one. The men are chosen on the terms sheet.";
        _partyLine.AddThemeColorOverride("font_color", MutedColor);

        // A blocked act is never hidden and never brick: it stays where the act will be, pressed into
        // the paper, with the line beside it saying what refuses it (design canvas → 7a).
        if (fit == 0)
        {
            _verdictLabel.Text = "Nobody in the yard can walk out today.";
            _verdictLabel.AddThemeColorOverride("font_color", WarningColor);
            UiKit.Refused(_sendButton, "Nobody is fit for the road.");
            _bountyButton.Disabled = true;
            return;
        }

        _verdictLabel.Text = "The terms are read before the gate opens.";
        _verdictLabel.AddThemeColorOverride("font_color", MutedColor);
        UiKit.Act(_sendButton);
        _sendButton.Disabled = false;
        _bountyButton.Disabled = contract is null;
    }

    /// <summary>Opens the terms; the men are ticked there and the sending is what accepting does.</summary>
    private void SendToOffer() => ReadTerms(null, chosen =>
    {
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
            battle => Log(new Expedition().Settle(dojo, setup, battle), dojo),
            [.. party.Select(entry => entry.Id)]));
    });

    private void SendToBounty()
    {
        if (_dojo.Bounty is not BountyContract contract)
        {
            return;
        }

        ReadTerms(contract, chosen =>
        {
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
                    dojo),
                [.. party.Select(entry => entry.Id)]));
        });
    }

    /// <summary>
    /// Opens the terms over the day: the men are ticked there, and sending is what accepting them does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sheet is opened over this screen rather than handed to the hub, because refusing must leave
    /// the day exactly as it was — the ticked men included, which a screen the hub closed and rebuilt
    /// would lose.
    /// </para>
    /// <para>
    /// The clock is held for as long as the sheet stands: a party half chosen is a decision half made,
    /// and the morning must not arrive on top of it (build step 8).
    /// </para>
    /// </remarks>
    /// <param name="contract">The promise being kept, or <c>null</c> for the day's own offer.</param>
    /// <param name="send">What accepting the terms does, with the men ticked on the sheet.</param>
    private void ReadTerms(BountyContract? contract, Action<IReadOnlyList<WarriorId>> send)
    {
        if (_terms is not null)
        {
            return;
        }

        SortieScreen terms = new()
        {
            Dojo = _dojo,
            Contract = contract,
            Chosen = [.. _party],
            Layer = 4,
        };

        terms.Refused = Remember;
        terms.Accepted = chosen =>
        {
            Remember(chosen);
            send(chosen);
        };

        _terms = terms;
        Clock?.Hold(PartyHold);
        AddChild(terms);
    }

    /// <summary>Closes the sheet and keeps the men it was left holding.</summary>
    private void Remember(IReadOnlyList<WarriorId> chosen)
    {
        _party.Clear();
        _party.UnionWith(chosen);
        Clock?.Release(PartyHold);

        if (_terms is not null)
        {
            RemoveChild(_terms);
            _terms.QueueFree();
            _terms = null;
        }

        UpdateButtons();
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
/// <param name="Party">
/// The men who walked out of the gate, in the order they were ticked.
/// </param>
/// <remarks>
/// The party travels with the fight so the sheet the men walk back into can print <b>them</b> rather
/// than a paragraph about them: the ids are read against the roster after the books are closed, which
/// is what makes a man's line say what the fight did to him.
/// </remarks>
public sealed record PendingBattle(
    BattleSetup Setup,
    ulong Seed,
    Func<BattleResult, string> Settle,
    IReadOnlyList<WarriorId>? Party = null);
