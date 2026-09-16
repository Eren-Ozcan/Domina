using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The last night: one bout at a time, the party chosen again for each (docs/GDD.md §10).
/// </summary>
/// <remarks>
/// <para>
/// It is deliberately <b>not</b> the day screen with a different label. There is no offer to decline,
/// no contract, no market and no day to close — the only decision the night has is <b>who goes out
/// next</b>, and the screen is that decision and its price: every man is listed with what is left of
/// him, because nothing heals between the bouts.
/// </para>
/// <para>
/// Like the day screen it hands the fight to <see cref="Watcher"/> and lets <see cref="FinalNight"/>
/// close the books. The accounting is not repeated here.
/// </para>
/// </remarks>
public sealed partial class FinalNightScreen : DojoScreen
{
    /// <inheritdoc/>
    /// <remarks>The last night is the ground itself; there is nowhere to go back to until it is decided.</remarks>
    protected override bool TakesTheStage => true;

    private readonly HashSet<WarriorId> _party = [];
    private readonly FinalNight _night = new();

    private DojoState _dojo = null!;
    private Label _boutLabel = null!;
    private VBoxContainer _partyList = null!;
    private Label _verdictLabel = null!;
    private Button _sendButton = null!;
    private Label _log = null!;
    private Label _headline = null!;

    /// <summary>The side that lets the bout be watched; <c>null</c> resolves it in the background.</summary>
    public Func<PendingBattle, bool>? Watcher { get; set; }

    /// <summary>The report to print when coming back from the arena.</summary>
    public string? Report { get; set; }

    /// <summary>Called when the night ends, whichever way it ends.</summary>
    public Action? Ended { get; set; }

    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

        VBoxContainer page = BuildPage(
            "the last night",
            "Bouts one after another, and the only decision left is who goes out for each. "
            + "Nothing heals in between and there is no way back to the dojo.");

        // The night is framed as the last thing the term does, so it is announced rather than listed:
        // an eyebrow, one line the size of a headline, and then the business (design canvas → 6g).
        page.AddChild(UiKit.SectionLabel("The final night · the last seat", onNight: true));

        _headline = UiKit.OnNight(string.Empty, UiKit.PaperInk, UiKit.DisplaySize, display: true);
        page.AddChild(_headline);

        page.AddChild(UiKit.Body(
            "The winner's school keeps the province's first seat for the next term. Nothing comes "
            + "after this fight.",
            UiKit.NightMuted,
            UiKit.NoteSize + 1));

        VBoxContainer bout = UiKit.Section(page, "The bout in front of you");
        _boutLabel = UiKit.Body();
        bout.AddChild(_boutLabel);

        VBoxContainer bell = UiKit.Section(page, "Who answers the bell", fill: true);

        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 150),
        };
        bell.AddChild(scroll);

        _partyList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _partyList.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_partyList);

        VBoxContainer orders = UiKit.Section(page, "Orders");

        _verdictLabel = UiKit.Body();
        orders.AddChild(_verdictLabel);

        _sendButton = UiKit.Act(new Button
        {
            Text = "Let them walk out",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        });
        _sendButton.Pressed += Guarded(_dojo, Send);
        orders.AddChild(_sendButton);

        _log = UiKit.Note(Report ?? string.Empty);
        orders.AddChild(_log);

        Refresh();
    }

    /// <summary>Reprints the bout, the men and the button.</summary>
    public override void Refresh()
    {
        if (SeasonModel.DescribeNight(_dojo) is not FinalNightCard card)
        {
            Ended?.Invoke();
            return;
        }

        _boutLabel.Text = string.Join(
            '\n',
            $"Bout {card.Round} of {card.Rounds}  ·  {card.Sighting}",
            card.Last
                ? "The man himself. Nothing is healed, nothing is held back."
                : "Nothing heals between the bouts. A party of at most "
                  + $"{card.MaxPartySize}; withdrawing loses the night.");
        _boutLabel.AddThemeColorOverride("font_color", card.Last ? WarningColor : InkColor);

        _headline.Text = card.Last
            ? $"{_dojo.Name} against the man himself"
            : $"{_dojo.Name} against {card.Sighting}";

        BuildPartyList();
        UpdateButton();
    }

    private void BuildPartyList()
    {
        Clear(_partyList);
        IReadOnlyList<NightCandidate> candidates = SeasonModel.Candidates(_dojo);

        // A man who can no longer stand must not stay selected: the button would be dimmed and the
        // screen would not say why.
        _party.IntersectWith(candidates.Where(c => c.CanStand).Select(c => c.Id));

        if (candidates.Count == 0)
        {
            _partyList.AddChild(UiKit.Body("There is nobody left to send.", UiKit.Warning));
            return;
        }

        foreach (NightCandidate candidate in candidates)
        {
            CheckBox box = new()
            {
                Text = candidate.CanStand
                    ? $"{candidate.Name}  —  power {candidate.Score:0}"
                      + (candidate.WoundDays > 0
                          ? $"  ·  hurt, {candidate.HealthShare * 100:0}% of him left"
                          : "  ·  whole")
                    : $"{candidate.Name}  —  cannot be carried to the field",
                Disabled = !candidate.CanStand,
                ButtonPressed = _party.Contains(candidate.Id),
            };
            box.AddThemeColorOverride(
                "font_color",
                !candidate.CanStand ? MutedColor : candidate.WoundDays > 0 ? PendingColor : InkColor);

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

                UpdateButton();
            };

            _partyList.AddChild(box);
        }
    }

    private void UpdateButton()
    {
        NightVerdict verdict = SeasonModel.Judge(_dojo, [.. _party]);

        _sendButton.Disabled = !verdict.CanSend;
        _verdictLabel.Text = verdict.Refusal is FinalRefusal refusal
            ? RefusalText(refusal)
            : $"{verdict.Size} take the field.";
        _verdictLabel.AddThemeColorOverride(
            "font_color",
            verdict.CanSend ? GoodColor : WarningColor);
    }

    private void Send()
    {
        List<WarriorId> chosen = [.. _party];
        if (!SeasonModel.Judge(_dojo, chosen).CanSend)
        {
            return;
        }

        DojoState dojo = _dojo;
        List<RosterEntry> party = [.. SeasonModel.Party(dojo, chosen)];
        int round = dojo.Season.FinalRound;
        BattleSetup setup = FinalNight.Prepare(dojo, party, collectEvents: true);

        ulong seed = BoutSeed(round);

        Fight(new PendingBattle(
            setup,
            seed,
            battle => Describe(_night.Settle(dojo, setup, battle, seed), dojo)));
    }

    private void Fight(PendingBattle bout)
    {
        if (Watcher?.Invoke(bout) == true)
        {
            return;
        }

        _log.Text = bout.Settle(new Battle(bout.Setup, new SeededRandom(bout.Seed)).Run());
        _party.Clear();
        Persist();
        Refresh();
    }

    private static string Describe(FinalRoundResult result, DojoState dojo)
    {
        List<string> lines =
        [
            result.Won
                ? $"Bout {result.Round} is won."
                : $"Bout {result.Round} is lost. The night is over.",
        ];

        foreach (WarriorAftermath warrior in result.Aftermath.Warriors)
        {
            string name = dojo.Roster.Find(warrior.Id)?.Name ?? "Warrior";
            if (warrior.Died)
            {
                lines.Add($"{name} fell.");
            }
            else if (warrior.RecoveryDays > 0)
            {
                lines.Add($"{name} is hurt and goes into the next bout with it.");
            }
        }

        if (result.Phase == SeasonPhase.Triumph)
        {
            lines.Add("Five bouts, one night. The licensing of the province is yours.");
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// The bout's stream — the seed, the day and the bout number.
    /// </summary>
    /// <remarks>
    /// The bout number goes in because the night burns no day: without it the five bouts would all be
    /// the same fight, and reloading between them would reroll nothing.
    /// </remarks>
    private ulong BoutSeed(int round) =>
        _dojo.Seed ^ ((ulong)(_dojo.Day + round) * 0x9E3779B97F4A7C15);

    private static string RefusalText(FinalRefusal refusal) => refusal switch
    {
        FinalRefusal.EmptyParty => "Nobody selected.",
        FinalRefusal.WrongPartySize => "Too many for one bout.",
        FinalRefusal.Unfit => "One of those selected cannot answer the bell.",
        FinalRefusal.NightNotOpen => "The night is not open.",
        _ => "One of those selected is not on the roster.",
    };
}
