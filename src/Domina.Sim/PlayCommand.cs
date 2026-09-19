using System.Globalization;
using System.Text;
using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Honor;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Sim;

/// <summary>
/// Plays one season a day at a time, from a script.
/// </summary>
/// <remarks>
/// <para>
/// The batch runner answers "what do ten thousand campaigns do" under a <b>fixed policy</b>, which is
/// the one thing it cannot question: if the policy is starving itself, every number it prints measures
/// the policy rather than the game. This plays the same core with the decisions left <b>open</b>, so a
/// deciding player — a person at a terminal, or an agent — can be put in the seat instead.
/// </para>
/// <para>
/// It is <b>stateless on purpose</b>. The script is the whole run: every invocation rebuilds the dojo
/// from the seed and replays the file from its first line, then prints where the season stands. A
/// caller therefore plays by appending one line and running again, and never has to hold a live
/// process open. The same property makes a run reproducible — the script and the seed are the save.
/// </para>
/// </remarks>
internal static class PlayCommand
{
    /// <summary>The flag that hands the run to this instead of to a measurement.</summary>
    public const string Flag = "--play";

    /// <summary>The script asked for something this build cannot do.</summary>
    public const int ExitRefused = 2;

    /// <summary>Is this a play invocation?</summary>
    public static bool Wanted(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (int i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], Flag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Replays the script and prints the season's standing.</summary>
    public static int Run(TextWriter output, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(args);

        string? path = null;
        ulong seed = 20260914;
        int days = 180;
        int gold = 600;

        for (int i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case Flag when i + 1 < args.Count:
                    path = args[++i];
                    break;

                case "--seed" when i + 1 < args.Count:
                    seed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;

                case "--days" when i + 1 < args.Count:
                    days = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;

                case "--gold" when i + 1 < args.Count:
                    gold = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;

                default:
                    break;
            }
        }

        if (path is null)
        {
            output.WriteLine($"{Flag} needs a script file.");
            return ExitRefused;
        }

        // A script that does not exist yet is an empty one: the first turn of a run is "show me the
        // opening position", and asking the caller to create a blank file first would buy nothing.
        string[] lines = File.Exists(path) ? File.ReadAllLines(path) : [];

        Session session = new(seed, days, gold);
        List<string> notes = [];

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string? refusal = session.Do(line);
            if (refusal is not null)
            {
                notes.Add($"line {i + 1} ({line}): {refusal}");
            }

            if (session.Over)
            {
                // The rest of the script is not played, and it used to go unmentioned: a player whose
                // dojo died on line 40 of 90 saw a standing that looked like the end of his season
                // rather than the middle of it.
                int left = lines.Length - i - 1;
                if (left > 0)
                {
                    notes.Add(
                        $"the season ended on line {i + 1} — {left} further "
                        + $"{(left == 1 ? "line was" : "lines were")} not played");
                }

                break;
            }
        }

        session.Write(output, notes);
        return 0;
    }

    /// <summary>One season, mid-play.</summary>
    private sealed class Session
    {
        private readonly DojoState _state;
        private readonly int _days;
        private readonly ulong _seed;
        private readonly List<string> _log = [];

        /// <summary>Every death of the season, dated — the printed log only reaches ten days back.</summary>
        private readonly List<string> _deaths = [];

        /// <summary>The season-long number of every warrior the dojo has had. See <see cref="NumberOf"/>.</summary>
        private readonly Dictionary<WarriorId, int> _numbers = [];

        private int _nextNumber;

        /// <summary>
        /// The day the last line of the log belongs to.
        /// </summary>
        /// <remarks>
        /// The toll cannot read the day off the dojo: a fight closes the day it was fought on, so by
        /// the time the dead are counted the dojo has already turned over to the next one. The roll
        /// used to be written with that later day and a player read his own log as two different
        /// deaths — the line said day 17, the roll said day 18.
        /// </remarks>
        private int _loggedDay;

        public Session(ulong seed, int days, int gold)
        {
            _seed = seed;
            _days = days;

            _state = new DojoState(
                new DojoTuning(),
                new EconomyTuning(),
                seed,
                new EncounterTuning(),
                new EventTuning(),
                new MarketTuning(),
                school: new SchoolTuning(),
                staff: new StaffTuning(),
                season: new SeasonTuning() with { Days = days },
                province: new ProvinceTuning(),
                standing: new StandingTuning(),
                honor: new HonorTuning());

            _state.Journal.Enabled = false;
            _state.SetPurse(new Resources(Gold: gold));
            _state.Province.Deal(new SeededRandom(seed ^ 0x3C3C_C3C3_5A5A_A5A5), 0.5);

            // The opening roster is the scenario's, exactly as the measurement builds it: a deciding
            // player and the batch policy have to start on the same bed or their seasons cannot be
            // compared, which is the whole reason this command exists.
            Scenario scenario = Scenarios.Find("3v3") ?? Scenarios.All[0];
            IReadOnlyList<Warrior> template = scenario.Build().PlayerSide;
            int start = Math.Min(4, _state.Capacity);
            for (int i = 0; i < start; i++)
            {
                Warrior proto = template[i % template.Count];
                _state.Roster.Recruit($"Warrior {i + 1}", proto.BaseStats, proto.Weapon, proto.Armor);
            }
        }

        /// <summary>Is the season finished — the last night played out, or the dojo gone?</summary>
        public bool Over =>
            _state.Season.Phase is SeasonPhase.Triumph or SeasonPhase.Fallen or SeasonPhase.Closed
            || !_state.Roster.Living.Any();

        /// <summary>Makes one move. Returns null if it was made, or why it was not.</summary>
        public string? Do(string line)
        {
            // Who was standing before the move, so that what the move cost can be said out loud. A
            // played season that reports a won fight and quietly returns two men fewer is unreadable:
            // the player cannot tell a victory from a disaster, and every judgement he makes after it
            // is made blind. The core knows; it is the telling that has to be built.
            Dictionary<WarriorId, (string Name, int Infirmary, bool Alive)> before =
                _state.Roster.Entries.ToDictionary(
                    e => e.Warrior.Id,
                    e => (e.Warrior.Name, e.RecoveryDaysRemaining, e.Warrior.IsAlive));

            string? refusal = Act(line);
            Toll(before);
            return refusal;
        }

        /// <summary>
        /// Something the move did that is neither a refusal nor a day's event.
        /// </summary>
        /// <remarks>
        /// A refusal means nothing happened; these are moves that happened <b>partly</b>, which is the
        /// harder thing to notice and the one the player pays for later.
        /// </remarks>
        private void Note(string said) => _log.Add($"day {_state.Day} — {said}");

        /// <summary>Says what the move just made cost the roster.</summary>
        private void Toll(Dictionary<WarriorId, (string Name, int Infirmary, bool Alive)> before)
        {
            if (_log.Count == 0)
            {
                return;
            }

            List<string> dead = [];
            List<string> hurt = [];

            foreach (RosterEntry entry in _state.Roster.Entries)
            {
                if (!before.TryGetValue(entry.Warrior.Id, out (string Name, int Infirmary, bool Alive) was)
                    || !was.Alive)
                {
                    // A man who was already dead before this move did not die in it. Without this the
                    // toll reads the whole graveyard out again on every single day.
                    continue;
                }

                if (!entry.Warrior.IsAlive)
                {
                    dead.Add(was.Name);
                }
                else if (entry.RecoveryDaysRemaining > was.Infirmary)
                {
                    // "left", because this is what remains after the day that has just been paid for —
                    // not the length that was rolled. Without the word the two numbers read as a
                    // contradiction in the same line.
                    hurt.Add($"{was.Name} {entry.RecoveryDaysRemaining}d left");
                }
            }

            if (dead.Count == 0 && hurt.Count == 0)
            {
                return;
            }

            StringBuilder toll = new(_log[^1]);
            if (dead.Count > 0)
            {
                toll.Append(CultureInfo.InvariantCulture, $" | DEAD: {string.Join(", ", dead)}");

                // The printed log is a window on the last ten days, so a death older than that is
                // unreadable by the time its consequences are felt. The roll is kept for the whole
                // season: who fell, on which day, doing what.
                foreach (string name in dead)
                {
                    _deaths.Add($"day {_loggedDay}: {name}");
                }
            }

            if (hurt.Count > 0)
            {
                toll.Append(CultureInfo.InvariantCulture, $" | wounded: {string.Join(", ", hurt)}");
            }

            _log[^1] = toll.ToString();
        }

        private string? Act(string line)
        {
            string[] word = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string verb = word[0].ToLowerInvariant();

            // A finished season used to swallow every further move without a word, so a script that
            // ran past the end read as if it were still being played. It is refused out loud instead.
            if (Over)
            {
                return $"the season is over ({_state.Season.Phase}) — no move is taken";
            }

            switch (verb)
            {
                case "day":
                    return Day();

                case "expedition":
                    return Expedition(word);

                case "accept":
                    return _state.AcceptBounty() is not null ? null : "no bounty could be accepted";

                case "bounty":
                    return Bounty(word);

                case "night":
                    return Night(word);

                case "restock":
                    return Restock(word);

                case "hire":
                    return Hire(word);

                case "drill":
                    return Drill(word);

                case "build":
                    return Build(word);

                case "staff":
                    return Staff(word);

                case "sake":
                    return BuySake(word);

                case "feast":
                    return Feast();

                case "gift":
                    return Gift(word);

                case "release":
                    return Warrior(word, id => _state.Release(id));

                case "retire":
                    return Warrior(
                        word,
                        entry => _state.CanRetire(entry)
                            ? null
                            : $"the retirement was refused — {entry.Name} has {entry.Victories} "
                                + $"victory(ies) and the house asks for {_state.Tuning.VictoriesForRetirement}"
                                + " (a crippling wound opens it early)",
                        id => _state.Retire(id));

                case "dismiss":
                    return DismissStaff(word);

                case "charm":
                    return Charm(word);

                case "fit":
                    return Fit(word);

                case "class":
                    return Class(word);

                case "path":
                    return Path(word);

                default:
                    return $"there is no move called {verb}";
            }
        }

        private string? Day()
        {
            DayReport report = _state.AdvanceDay();
            Record(report, string.Empty);
            return null;
        }

        /// <summary>
        /// The order the player would have given with his hand on the key: pull everybody out.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The game's fight is watched and the pull-out is a key (GDD §5: the order is the party's, not
        /// one man's). A script has no hand on that key, so a played season could only ever fight every
        /// fight to the last man — which is how two played seasons ended, with a whole party dying in
        /// one engagement that a watching player would have broken off.
        /// </para>
        /// <para>
        /// So the script states the order in advance: <c>pull:0.5</c> on the move means "pull out when
        /// the party is under half health", whatever the count on the field. It costs what the key
        /// costs, in honour and in the fee.
        /// </para>
        /// <para>
        /// It is the core's <see cref="RetreatBelowHealth"/> and <b>not</b> the batch bed's
        /// <see cref="RetreatWhenLosing"/>, which also wants the party outnumbered. That second
        /// condition is right for a measurement bed — watching health alone abandons most 3v3 fights
        /// and the victory rate then measures the policy rather than the balance — but wrong for a
        /// hand-played season: a party of four against one enemy cannot trip it until three of them
        /// are down, and a lone man never can, so the order a player wrote never fired. The bed keeps
        /// both conditions; the played season takes the order at its word.
        /// </para>
        /// </remarks>
        private static RetreatBelowHealth? PullOut(string[] word)
        {
            foreach (string token in word)
            {
                if (!token.StartsWith("pull:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (double.TryParse(
                        token["pull:".Length..],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double share)
                    && share is > 0 and <= 1)
                {
                    return new RetreatBelowHealth(share);
                }
            }

            return null;
        }

        private string? Expedition(string[] word)
        {
            IReadOnlyList<EncounterOffer> board = _state.Board;
            if (board.Count == 0)
            {
                return "the board is empty today";
            }

            int pick = word.Length > 1 ? Number(word[1]) : 0;
            if (pick < 0 || pick >= board.Count)
            {
                return $"the board has no offer {pick}";
            }

            EncounterOffer offer = board[pick];
            List<RosterEntry> party = Party(word, 2, [.. _state.Roster.FitForCampaign], out string? cannot);
            if (cannot is not null)
            {
                return cannot;
            }

            if (Domina.Core.Campaign.Expedition.Refuse(_state, offer, party) is ExpeditionRefusal no)
            {
                // A bare "WrongPartySize" leaves the player guessing, and the guess is hard: a job's
                // party rule can change overnight when the slot is replaced, so the rule he read is
                // not always the rule his queued line meets. The refusal says the number.
                string why = no == ExpeditionRefusal.WrongPartySize
                    ? offer.RequiredPartySize is int need
                        ? $"WrongPartySize — this job takes exactly {need}, {party.Count} were sent"
                        : $"WrongPartySize — this job takes 1-{EncounterOffer.MaxPartySize}, {party.Count} were sent"
                    : no.ToString();
                return $"the expedition was refused: {why}";
            }

            RetreatBelowHealth? pull = PullOut(word);
            ExpeditionResult result = new Domina.Core.Campaign.Expedition().Send(
                _state,
                offer,
                party,
                _seed + (ulong)_state.Day,
                new CombatTuning(),
                pull);

            string told = result.Battle.Outcome switch
            {
                BattleOutcome.PlayerVictory => $"won the fight, {result.Reward} gold",
                BattleOutcome.PlayerWithdrawal => $"pulled out, {result.Reward} gold",
                _ => "lost the fight",
            };
            Record(result.Day, $"expedition {pick}: {told} (the fight closed the day)");
            return null;
        }

        /// <summary>Answers the bell for one bout of the last night.</summary>
        /// <remarks>
        /// The night is fought a bout at a time rather than in one call, because choosing who answers
        /// each bell <b>is</b> the night: the party that wins the first bout is the party that has to
        /// stand up for the second, carrying its wounds. A command that settled all five at once would
        /// take the only decision the night offers away from the player.
        /// </remarks>
        private string? Night(string[] word)
        {
            if (_state.Season.Phase != SeasonPhase.FinalNight)
            {
                // The night opens when the last day closes, not on it — a player who read the standing
                // on day 180 and typed `night` was told only that the season was "Running", and missed
                // the climax he had played 180 days for.
                return $"the last night is not open (the season is {_state.Season.Phase}) — "
                    + $"it opens when day {_days} closes, so day {_state.Day} still has to be played out"
                    + (_state.Season.GateOpen
                        ? " (the gate is open, so there will be a night)"
                        : $" — and the gate is shut: {Math.Max(0, 3 - _state.Season.HeadsTaken)} more head(s) or there is no night at all");
            }

            List<RosterEntry> party = Party(
                word,
                1,
                [.. _state.Roster.Living.Where(FinalNight.CanAnswerTheBell(_state))],
                out string? cannot);
            if (cannot is not null)
            {
                return cannot;
            }

            if (FinalNight.Refuse(_state, party) is FinalRefusal no)
            {
                return $"the bell was not answered: {no}";
            }

            int round = _state.Season.FinalRound;
            FinalRoundResult result = new FinalNight().Fight(
                _state,
                party,
                new SeededRandom(_seed + 7_777_777 + (ulong)round),
                new CombatTuning(),
                PullOut(word));

            _log.Add(
                $"the last night, bout {round}: {(result.Won ? "won" : "LOST")} "
                + $"with {party.Count} men — {_state.Season.Phase}");
            return null;
        }

        private string? Bounty(string[] word)
        {
            if (_state.Bounty is not BountyContract contract)
            {
                // Three different situations used to print the same sentence, and a player could not
                // tell them apart: nothing posted, a contract posted but never accepted, and a
                // contract accepted and then let expire while the days were spent elsewhere.
                return _state.AcceptedBountyDay is int accepted
                    ? $"the contract you accepted on day {accepted} is no longer on the board — "
                        + "it ran out while the days went elsewhere"
                    : "no contract is on the board today";
            }

            // The hunt used to send the top four and nothing else could be asked of it, so a failed
            // hunt could take the whole roster in one line — and it did, in two played seasons. The
            // party is sized the way an expedition's is.
            List<RosterEntry> party = Party(word, 1, [.. _state.Roster.FitForCampaign], out string? cannot);
            if (cannot is not null)
            {
                return cannot;
            }

            if (party.Count == 0)
            {
                return "nobody is fit to hunt";
            }

            // The expedition asks the core whether the party may go and prints the refusal; the hunt
            // asked nothing and handed an oversized party straight to the core, which throws rather
            // than refuses. A played season died on that throw — the whole run, not the move.
            if (!contract.AsOffer(_state.Day).Accepts(party.Count))
            {
                return $"the hunt was refused: WrongPartySize — a contract takes "
                    + $"1-{EncounterOffer.MaxPartySize} men, {party.Count} were sent";
            }

            BountyResult result = new Domina.Core.Campaign.Expedition().SendToBounty(
                _state,
                contract,
                party,
                new SeededRandom(_seed + 5_000_011 + (ulong)_state.Day),
                new CombatTuning(),
                PullOut(word));

            Record(result.Day, $"bounty: {(result.Claimed ? "head taken" : "the hunt failed")} (the hunt closed the day)");
            return null;
        }

        /// <summary>Calls a feast, or says which of its three conditions is not met.</summary>
        /// <remarks>
        /// "the feast was refused" was the one refusal in a played season that gave no reason at all,
        /// and the reason is never guessable: sake is bought, not drawn, and the cooldown is invisible.
        /// </remarks>
        /// <summary>Buys sake by the measure.</summary>
        /// <remarks>
        /// The core has had <see cref="DojoState.BuySake"/> since the feast landed, and the harness
        /// had the drinking half and not the buying one — exactly the shape of the missing
        /// <c>dismiss</c>. A played season met "the feast was refused — it drinks 10 sake (4 in the
        /// store)" and had no move that could ever reach ten: sake is not a restock argument, and it
        /// was not even printed in the purse.
        /// </remarks>
        private string? BuySake(string[] word)
        {
            if (word.Length < 2)
            {
                return $"sake needs a number of measures ({_state.Economy.SakePrice} gold each)";
            }

            int measures = Number(word[1]);
            if (measures <= 0)
            {
                return "sake needs a number of measures above zero";
            }

            int bought = _state.BuySake(measures);
            if (bought <= 0)
            {
                return $"no sake was bought — a measure costs {_state.Economy.SakePrice} gold "
                    + $"and {_state.Resources.Gold} is in the chest";
            }

            Note($"bought {bought} measure(s) of sake for {bought * _state.Economy.SakePrice} gold"
                + (bought < measures ? $" — {measures} were asked for and the purse reached {bought}" : string.Empty)
                + $"; the store holds {_state.Resources.Sake} and a feast drinks {_state.FeastSake}");
            return null;
        }

        private string? Feast()
        {
            if (!_state.Roster.Living.Any())
            {
                return "the feast was refused — there is nobody in the yard to hold it for";
            }

            if (_state.Resources.Sake < _state.FeastSake)
            {
                return $"the feast was refused — it drinks {_state.FeastSake} sake "
                    + $"({_state.Resources.Sake} in the store)";
            }

            if (!_state.CanFeast)
            {
                return $"the feast was refused — the last one was on day {_state.LastFeastDay}, "
                    + $"and they stand {_state.Tuning.Morale.FeastCooldownDays} days apart";
            }

            return _state.Feast() ? null : "the feast was refused";
        }

        private string? Restock(string[] word)
        {
            if (word.Length < 4)
            {
                return "restock needs food, water and medicine";
            }

            Resources want = new(Food: Number(word[1]), Water: Number(word[2]), Medicine: Number(word[3]));
            Resources before = _state.Resources;
            int spent = _state.Quartermaster.Restock(_state, want);
            Resources after = _state.Resources;

            if (spent <= 0)
            {
                return "nothing was bought - restock tops the store UP TO the levels given, "
                    + "and it already holds that much";
            }

            // The purse buys food first, then water, then medicine, and a thin purse simply stops
            // part-way. A player who asked for water and got none was never told which line the gold
            // ran out on — it showed up days later as a store that flatlined.
            List<string> short_ = [];
            if (after.Food < want.Food)
            {
                short_.Add($"food {after.Food} of {want.Food}");
            }

            if (after.Water < want.Water)
            {
                short_.Add($"water {after.Water} of {want.Water}");
            }

            if (after.Medicine < want.Medicine)
            {
                short_.Add($"medicine {after.Medicine} of {want.Medicine}");
            }

            if (short_.Count > 0)
            {
                Note($"restock spent {spent} gold and came up short: {string.Join(", ", short_)} "
                    + $"({after.Gold} gold left; it buys food, then water, then medicine)");
            }

            return null;
        }

        private string? Hire(string[] word)
        {
            if (word.Length < 2)
            {
                return "hire needs a candidate";
            }

            int pick = Number(word[1]);

            // "the candidate was not taken on" is true and useless: the three things that refuse a
            // hire are a full roster, an empty purse and an index that is not on the stall, and the
            // player cannot tell them apart by looking at the standing. The refusal names the one.
            if (pick < 0 || pick >= _state.Recruits.Count)
            {
                return $"the stall has no candidate {pick}";
            }

            RecruitOffer candidate = _state.Recruits[pick];

            if (_state.HiredToday.Contains(pick))
            {
                return $"the candidate was not taken on — {candidate.Name} was already taken on today "
                    + "(the stall regenerates tomorrow; another candidate may still be hired now)";
            }

            if (_state.Roster.Living.Count() >= _state.Capacity)
            {
                return $"the candidate was not taken on — the roster is full "
                    + $"({_state.Capacity} beds; build quarters or release a man)";
            }

            if (_state.Resources.Gold < candidate.Price)
            {
                return $"the candidate was not taken on — he costs {candidate.Price} gold and "
                    + $"{_state.Resources.Gold} is in the chest";
            }

            return _state.HireRecruit(pick) is not null ? null : "the candidate was not taken on";
        }

        private string? Drill(string[] word)
        {
            if (word.Length < 3)
            {
                return "drill needs a warrior and a drill";
            }

            if (!Enum.TryParse(word[2], ignoreCase: true, out Drill drill))
            {
                // Every other refusal that takes a word out of an enum prints the whole set; this one
                // did not, and a player guessed eighteen words across two turns to find the five.
                return $"there is no drill called {word[2]} — they are "
                    + $"{string.Join(", ", Enum.GetNames<Drill>())}";
            }

            return Warrior(word, id => _state.SetDrill(id, drill));
        }

        private string? Build(string[] word)
        {
            if (word.Length < 2 || !Enum.TryParse(word[1], ignoreCase: true, out SchoolNodeId node))
            {
                return $"there is no facility called {(word.Length > 1 ? word[1] : "?")}";
            }

            // "the order was refused" was the one refusal left in the harness that named no rule at
            // all: a player with 1,273 gold ordered a building whose prerequisite was not standing,
            // was told nothing, and never found out why. A building is left off "can be ordered"
            // exactly when one of these three is true, so the refusal says which.
            if (_state.School.Has(node))
            {
                return $"the order was refused — the {node} is already standing";
            }

            if (_state.School.IsBuilding(node))
            {
                return $"the order was refused — the {node} is already on the site";
            }

            SchoolNode plan = SchoolTree.Find(node);
            if (plan.Requires is SchoolNodeId needs && !_state.School.Has(needs))
            {
                return $"the order was refused — the {node} is built onto the {needs}, "
                    + "and that is not standing";
            }

            int price = _state.School.PriceOf(plan);
            if (price > _state.Resources.Gold)
            {
                return $"the order was refused — the {node} costs {price} gold and "
                    + $"{_state.Resources.Gold} is in the chest";
            }

            return _state.BuySchoolNode(node) ? null : "the order was refused";
        }

        private string? Staff(string[] word)
        {
            if (word.Length < 2 || !Enum.TryParse(word[1], ignoreCase: true, out StaffRole role))
            {
                return $"there is no post called {(word.Length > 1 ? word[1] : "?")} — "
                    + $"they are {string.Join(", ", Enum.GetNames<StaffRole>())}";
            }

            // "the post was not filled" was refused silently for a whole season in one played run:
            // the post belongs to a building, and a player who has not built it is told nothing about
            // which one. The wage is said out loud in the same breath, because hiring is free at the
            // counter and then takes gold out of every day that follows.
            if (!_state.School.HasPostFor(role))
            {
                SchoolNode post = SchoolTree.Of(role);
                return $"the post was not filled — a {role} works out of the {post.Id}, "
                    + $"and it is not standing ({post.Cost}g/{post.BuildDays}d)";
            }

            if (_state.Staff.Has(role))
            {
                return $"the post was not filled — a {role} already holds it "
                    + $"(dismiss {role} lets him go)";
            }

            if (!_state.Hire(role))
            {
                return "the post was not filled";
            }

            Note($"{role} took the post at {_state.StaffTuning.WageOf(role)} gold a day — "
                + $"the wage runs until he is dismissed (dismiss {role}), and the payroll is now "
                + $"{_state.Staff.DailyWage(_state.StaffTuning)} gold a day");
            return null;
        }

        /// <summary>Lets a member of staff go.</summary>
        /// <remarks>
        /// The core has always had <see cref="DojoState.Dismiss"/> — the wage is the one gear the
        /// economy has, and letting someone go on a bad day is what it is for (GDD §10). The harness
        /// had the hiring half and not this one, so a played season signed eight wages it could never
        /// unsign: two dojos died at 40-44 gold a day with a full purse and no men.
        /// </remarks>
        private string? DismissStaff(string[] word)
        {
            if (word.Length < 2 || !Enum.TryParse(word[1], ignoreCase: true, out StaffRole role))
            {
                return $"there is no post called {(word.Length > 1 ? word[1] : "?")} — "
                    + $"they are {string.Join(", ", Enum.GetNames<StaffRole>())}";
            }

            if (!_state.Staff.Has(role))
            {
                return $"nobody was let go — no {role} is on the payroll "
                    + $"({(_state.Staff.Hired.Count == 0 ? "nobody is" : string.Join(", ", _state.Staff.Hired) + " are")})";
            }

            if (!_state.Dismiss(role))
            {
                return "nobody was let go";
            }

            Note($"{role} was let go — the building stays and drops to half its work, and the payroll "
                + $"falls to {_state.Staff.DailyWage(_state.StaffTuning)} gold a day");
            return null;
        }

        private string? Gift(string[] word)
        {
            // No screen in the standing ever printed a patron's name, so the verb was unusable: a
            // wrong guess said only that the guess was wrong. The refusal lists the whole set.
            if (word.Length < 2 || !Enum.TryParse(word[1], ignoreCase: true, out Patron patron))
            {
                return $"there is no patron called {(word.Length > 1 ? word[1] : "?")} — "
                    + $"they are {string.Join(", ", Enum.GetNames<Patron>())}";
            }

            int price = _state.Standing.Tuning.GiftPrice;

            return _state.SendGift(patron)
                ? null
                : $"the gift was refused — it costs {price} gold and {_state.Resources.Gold} is in the chest";
        }

        private string? Charm(string[] word)
        {
            if (word.Length < 2 || !Enum.TryParse(word[1], ignoreCase: true, out OmamoriKind kind))
            {
                return $"there is no charm called {(word.Length > 1 ? word[1] : "?")} — "
                    + $"they are {string.Join(", ", Enum.GetNames<OmamoriKind>())}";
            }

            if (!_state.School.Has(SchoolNodeId.Shrine))
            {
                return "the charm was not bought — the omamori are the temple's supply and there is no shrine";
            }

            return _state.BuyCharm(kind) ? null : "the charm was not bought — the gold was not there";
        }

        private string? Fit(string[] word)
        {
            if (word.Length < 3 || !Enum.TryParse(word[2], ignoreCase: true, out OmamoriKind kind))
            {
                return "fit needs a warrior and a charm — they are "
                    + $"{string.Join(", ", Enum.GetNames<OmamoriKind>())}";
            }

            return Warrior(
                word,
                entry =>
                {
                    if (_state.CharmStore.GetValueOrDefault(kind) <= 0)
                    {
                        return $"the charm was not fitted — no {kind} is in the store "
                            + $"(charm {kind} buys one for {_state.PriceOf(kind)} gold)";
                    }

                    if (entry.Warrior.Charms.Count >= _state.OmamoriSlots)
                    {
                        return $"the charm was not fitted — {entry.Name} already wears "
                            + $"{string.Join(", ", entry.Warrior.Charms)} and a man has "
                            + $"{_state.OmamoriSlots} slot(s)"
                            + (_state.Staff.Has(StaffRole.Monk) ? string.Empty : "; a monk in the shrine opens the second");
                    }

                    return null;
                },
                id => _state.FitCharm(id, kind));
        }

        private string? Class(string[] word)
        {
            if (word.Length < 3 || !Enum.TryParse(word[2], ignoreCase: true, out WarriorClass klass))
            {
                return "class needs a warrior and a class — they are "
                    + $"{string.Join(", ", Enum.GetNames<WarriorClass>().Where(n => n != nameof(WarriorClass.None)))}";
            }

            return Warrior(
                word,
                entry =>
                {
                    if (!_state.School.UnlockedClasses().Contains(klass))
                    {
                        SchoolNode hall = SchoolTree.All.Single(n => n.Unlocks == klass);
                        return $"the class was refused — {klass} is taught in the {hall.Id}, "
                            + $"and it is not standing ({hall.Cost}g/{hall.BuildDays}d)";
                    }

                    if (!ClassAptitude.IsPossibleFor(klass, entry.Warrior.Disabilities))
                    {
                        return $"the class was refused — {entry.Name} cannot practise {klass} with "
                            + $"the wounds he carries (lost: {string.Join(", ", entry.Warrior.Disabilities.Select(d => d.Part))})";
                    }

                    if (entry.Warrior.Class != WarriorClass.None
                        && ClassAptitude.IsPossibleFor(entry.Warrior.Class, entry.Warrior.Disabilities))
                    {
                        return $"the class was refused — {entry.Name} already practises "
                            + $"{entry.Warrior.Class}, and a class is only re-chosen when a wound closes it";
                    }

                    return null;
                },
                id => _state.TrainClass(id, klass));
        }

        private string? Path(string[] word)
        {
            if (word.Length < 3 || !Enum.TryParse(word[2], ignoreCase: true, out WarriorPath path))
            {
                return "path needs a warrior and a path — they are "
                    + $"{string.Join(", ", Enum.GetNames<WarriorPath>().Where(n => n != nameof(WarriorPath.None)))}";
            }

            return Warrior(
                word,
                entry =>
                {
                    if (entry.Warrior.Path != WarriorPath.None)
                    {
                        return $"the path was refused — {entry.Name} walks the "
                            + $"{entry.Warrior.Path} path already, and a path is chosen once with no way back";
                    }

                    int needed = _state.Tuning.Training.PathTrainingDays;
                    if (entry.TrainingDays < needed)
                    {
                        return $"the path was refused — a path is earned on the training ground: "
                            + $"{entry.Name} has {entry.TrainingDays} training day(s) of the {needed} it asks for";
                    }

                    return null;
                },
                id => _state.ChoosePath(id, path));
        }

        private string? Warrior(string[] word, Func<WarriorId, bool> act) =>
            Warrior(word, _ => null, act);

        /// <summary>Makes a move on one named warrior, saying why the move is not open to him.</summary>
        /// <remarks>
        /// The bare "the move was refused" was the harness's worst line: every rule in the core that
        /// can turn a move down printed the same six words, and a player could spend four turns
        /// guessing at tenure, training days, a hall he had not built and a slot that was full. The
        /// check runs before the core is asked, and it names the one thing that is in the way.
        /// </remarks>
        private string? Warrior(string[] word, Func<RosterEntry, string?> why, Func<WarriorId, bool> act)
        {
            if (word.Length < 2)
            {
                return "the move names no warrior";
            }

            int number = Number(word[1]);
            RosterEntry? entry = Living().Find(e => NumberOf(e) == number);
            if (entry is null)
            {
                return $"there is no warrior {number} on the roster — a number is given once and "
                    + "kept for the season, so a dead man's number is never handed to anybody else";
            }

            if (why(entry) is string no)
            {
                return no;
            }

            return act(entry.Warrior.Id) ? null : "the move was refused";
        }

        /// <summary>The number a warrior keeps for the whole season.</summary>
        /// <remarks>
        /// The roster was printed in alphabetical order and numbered by its position in that list, so
        /// every hire and every death renumbered the men under it: a player who read "6" in the
        /// morning and wrote <c>drill 6</c> an hour later drilled a different man, and two veterans
        /// were put on the wrong exercise that way without anybody noticing. The number is handed out
        /// once, on the day the man first appears, and is never handed out again.
        /// </remarks>
        private int NumberOf(RosterEntry entry)
        {
            if (!_numbers.TryGetValue(entry.Warrior.Id, out int number))
            {
                number = _nextNumber++;
                _numbers[entry.Warrior.Id] = number;
            }

            return number;
        }

        private List<RosterEntry> Living()
        {
            // The numbers follow the order the men joined in, not the order they are printed in, so
            // they are handed out over the whole roster — the dead included — before anybody is listed.
            foreach (RosterEntry entry in _state.Roster.Entries)
            {
                NumberOf(entry);
            }

            return [.. _state.Roster.Living.OrderBy(NumberOf)];
        }

        /// <summary>Who goes out on a fight move.</summary>
        /// <remarks>
        /// The party used to be taken off the top of the roster by quality and there was no way to
        /// ask for anybody else, so the bottom of the bench never fought, never gained and was still
        /// raw on the night it was needed — three played seasons reported the same trap and none of
        /// them had a move that reached it. <c>men:0,3,5</c> names the party by the numbers the
        /// roster prints; a bare number still means "the best n of them".
        /// </remarks>
        private List<RosterEntry> Party(string[] word, int from, List<RosterEntry> pool, out string? refusal)
        {
            refusal = null;

            if (Array.Find(word, w => w.StartsWith("men:", StringComparison.OrdinalIgnoreCase)) is string named)
            {
                List<RosterEntry> chosen = [];
                foreach (string part in named[4..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    int number = Number(part.Trim());
                    RosterEntry? man = pool.Find(e => NumberOf(e) == number);
                    if (man is null)
                    {
                        refusal = Living().Exists(e => NumberOf(e) == number)
                            ? $"warrior {number} cannot go today — he is on the roster but not fit for the field"
                            : $"there is no warrior {number} on the roster";
                        return [];
                    }

                    if (!chosen.Contains(man))
                    {
                        chosen.Add(man);
                    }
                }

                if (chosen.Count == 0)
                {
                    refusal = "men: named nobody — it takes the roster's numbers, as in men:0,3,5";
                }

                return chosen;
            }

            int want = EncounterOffer.MaxPartySize;
            for (int i = from; i < word.Length; i++)
            {
                if (int.TryParse(word[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int size))
                {
                    want = size;
                    break;
                }
            }

            return [.. pool.OrderByDescending(Score).Take(Math.Clamp(want, 1, EncounterOffer.MaxPartySize))];
        }

        private static int Number(string word) =>
            int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : -1;

        private static double Score(RosterEntry entry)
        {
            WarriorStats s = entry.Warrior.EffectiveStats;
            return s.MaxHealth + s.Strength + s.Accuracy + s.Defense + s.Evasion + s.Aggression;
        }

        private void Record(DayReport report, string what)
        {
            StringBuilder line = new();
            line.Append(CultureInfo.InvariantCulture, $"day {report.Day}");

            if (what.Length > 0)
            {
                line.Append(" — ").Append(what);
            }

            if (report.Event is DayEvent happening)
            {
                line.Append(CultureInfo.InvariantCulture, $" | mishap: {happening.Description} ({happening.Gold} gold)");
            }

            // The rival's own move is what a sack comes out of, and the harness used to print the
            // theft without ever printing the raid — so the loss read as causeless. The game's day log
            // (Domina.Presentation/DayLog.cs) has always said both; this says the same thing.
            if (report.RivalMove is ProvinceMove move)
            {
                line.Append(CultureInfo.InvariantCulture, $" | the rival moved: {move.Kind}");

                // A move name is not a consequence. A raid in particular reads as flavour until the
                // day it costs the treasury, so the one move that has a price says its price here.
                if (move.Kind == ProvinceMoveKind.Raid)
                {
                    line.Append(" — he stands on the board until he is fought; a day that closes with him unanswered is a sacking");
                }
            }

            if (report.Sacked is SackReport sack)
            {
                line.Append(
                    CultureInfo.InvariantCulture,
                    $" | SACKED (the raid was not answered): -{sack.Gold} gold, -{sack.Food} food");
            }

            if (report.MissedWeek)
            {
                line.Append(" | a week closed with no fight filed");
            }

            // The payroll failing empties every post in one day. The core reports who walked; the
            // harness printed nothing, so six hires vanished from the standing with no line to read it
            // from — the buildings stay, and it looks like nothing happened until the work slows.
            if (report.Upkeep.Walked is { Count: > 0 } walked)
            {
                line.Append(CultureInfo.InvariantCulture,
                    $" | the payroll could not be met and they walked: {string.Join(", ", walked)}");
            }

            if (report.Tribunal is TribunalVerdict verdict)
            {
                // The tribunal is the one way a man dies on a day nobody fought, so the line has to
                // name him: without it a warrior simply disappears from the roster between two checks
                // and the log gives the player nothing to read it from.
                line.Append(CultureInfo.InvariantCulture, $" | the tribunal sat: {verdict.Name} — {verdict.Outcome}");
            }

            if (report.Opened is { Count: > 0 } opened)
            {
                line.Append(CultureInfo.InvariantCulture, $" | opened: {string.Join(", ", opened)}");
            }

            _log.Add(line.ToString());
            _loggedDay = report.Day;
        }

        /// <summary>Prints where the season stands, and what can be done about it.</summary>
        public void Write(TextWriter output, List<string> notes)
        {
            Resources purse = _state.Resources;

            output.WriteLine($"DAY {_state.Day} of {_days}   phase {_state.Season.Phase}");
            output.WriteLine(
                $"PURSE  {purse.Gold} gold | food {purse.Food} | water {purse.Water} | "
                + $"medicine {purse.Medicine} | sake {purse.Sake}"
                + $"   (food {_state.Economy.FoodPrice}g, water {_state.Economy.WaterPrice}g, "
                + $"medicine {_state.Economy.MedicinePrice}g, sake {_state.Economy.SakePrice}g)");

            Resources draw = _state.DailyDraw();
            int payroll = _state.Staff.DailyWage(_state.StaffTuning);
            output.WriteLine(
                $"DRAW   today takes food {draw.Food}, water {draw.Water}, medicine {draw.Medicine}, gold {draw.Gold}"
                + (payroll > 0 ? $" (of which {payroll} is the staff payroll)" : string.Empty));
            // The gate used to be a bare fraction, and a player could reach day 180 alive without ever
            // being told that the heads are what the season is for: surviving to the end with a shut
            // gate closes the season with no last night at all.
            int daysLeft = Math.Max(0, _days - _state.Day);
            // "heads 18/3" reads as an overflow rather than as a requirement long since met, so once
            // the gate is open the fraction goes and the count stands on its own.
            string gate = _state.Season.GateOpen
                ? "gate open — the last night will be fought"
                : $"gate shut — {3 - _state.Season.HeadsTaken} more head(s) or there is no last night, "
                    + $"{daysLeft} days left";
            string heads = _state.Season.GateOpen
                ? $"heads {_state.Season.HeadsTaken} (3 were needed)"
                : $"heads {_state.Season.HeadsTaken}/3";
            output.WriteLine(
                $"SEASON {heads}, {gate}, "
                + $"quiet weeks in a row {_state.Season.MissedStreak}");

            // The one line every played season asked for and none of them got. From day 9 the header
            // said "the last night will be fought" and nothing else for 171 days, so four players
            // built one good party of four and met a chain of five. Wounds carry between the bells
            // and the first loss ends the season, which is what actually decided four of six runs.
            if (_state.Season.GateOpen)
            {
                output.WriteLine(
                    $"  the night is {_state.Season.Tuning.FinalRounds} bouts, answered one at a time; "
                    + "wounds carry from bell to bell and nobody heals between them, and the FIRST bout "
                    + "lost ends the season where it stands — the bouts left are not fought. A party is "
                    + $"1-{EncounterOffer.MaxPartySize} men, so the night asks for "
                    + $"{_state.Season.Tuning.FinalRounds} parties, not one.");
            }

            output.WriteLine();
            // The number is the man's for the season (see NumberOf) — it is not his place in this list.
            output.WriteLine("ROSTER (no. name  health  infirmary  drill  morale  honor  class  charms)");
            double threshold = _state.Tribunal.Tuning.SeppukuThreshold;
            output.WriteLine(
                // This line used to say "winning fights lifts honour; sitting out lets it fall", and
                // both halves were wrong. A fight's honour is HonorEngine.PerformanceDelta — the
                // man's own hit rate against a neutral 0.5 — so a man can win and lose honour, and a
                // played season watched exactly that happen and could not explain it. The pardon is
                // the crowd's, never the player's: there is no move that grants one.
                $"  a man whose honour falls under {threshold:F0} is called before the tribunal, and the "
                + "verdict is seppuku unless the crowd pardons him — the house has no move that does. "
                + "A fight moves honour by how well HE fought, not by whether the party won: a man who "
                + "misses more than he lands loses honour in a victory. A week the dojo takes no fight "
                + "costs the whole roster honour.");
            List<RosterEntry> living = Living();
            foreach (RosterEntry e in living)
            {
                WarriorStats s = e.Warrior.EffectiveStats;
                string charms = e.Warrior.Charms.Count == 0
                    ? _state.OmamoriSlots == 0
                        ? "no charm (no slot until a shrine stands)"
                        : $"no charm ({_state.OmamoriSlots} slot(s))"
                    : $"{string.Join("+", e.Warrior.Charms)} ({e.Warrior.Charms.Count} of {_state.OmamoriSlots})";

                // The one death that arrives with no warning at all: three played seasons lost a man
                // to the tribunal and not one of them had been told the threshold existed.
                string tribunal = _state.Tribunal.Standing?.Warrior == e.Warrior.Id
                    ? "  <- STANDS BEFORE THE TRIBUNAL, the verdict comes when this day closes"
                    : _state.Tribunal.Queue.Any(q => q.Warrior == e.Warrior.Id)
                        ? "  <- SUMMONED by the tribunal"
                        : e.Warrior.Honor < threshold
                            ? "  <- HONOUR BELOW THE THRESHOLD, the tribunal will call him"
                            : string.Empty;

                output.WriteLine(
                    $"  {NumberOf(e),2}  {e.Warrior.Name,-12} hp {s.MaxHealth,3:F0}  "
                    + $"{(e.RecoveryDaysRemaining > 0 ? $"infirmary {e.RecoveryDaysRemaining}d" : "fit        ")}  "
                    + $"{e.Drill,-12} morale {e.Warrior.Morale,5:F0}  honor {e.Warrior.Honor,5:F0}  "
                    + $"{e.Warrior.Class}  str {s.Strength:F0} acc {s.Accuracy:F0} def {s.Defense:F0} eva {s.Evasion:F0}  "
                    + $"{charms}{tribunal}");
            }

            if (living.Count == 0)
            {
                output.WriteLine("  (nobody is left standing)");
            }

            output.WriteLine($"  beds {_state.Capacity}, fit for the field {_state.Roster.FitForCampaign.Count()}");

            // The dead stay in the roster, and a closed dojo showed an empty table with no way to ask
            // who had been in it. The roll is the post-mortem, and it is dated: the log window only
            // reaches ten days back, which is how a collapse became invisible to a player who
            // scripted a long block of days.
            if (_deaths.Count > 0)
            {
                output.WriteLine($"  fallen {_deaths.Count}: {string.Join(" · ", _deaths.TakeLast(10))}"
                    + (_deaths.Count > 10 ? " (last ten)" : string.Empty));
            }

            output.WriteLine();
            output.WriteLine("BOARD (index  enemies  promised  expires  party)");
            output.WriteLine(
                "  the threat word in brackets is the day's band, read off the calendar and not off "
                + "your roster: it says nothing about how your men compare, and the health behind "
                + "the same word grows all season. How many of them there are is what costs lives.");

            // A raid stands on the board like any other job, and a player who does not know that reads
            // it as one and lets it expire — then the day closes with a sacking whose cause was never
            // on the screen. The game's own board says he is at the gate (OfferModel.IsRaid); this
            // said nothing, so it says it here, with the price of leaving it standing.
            if (_state.UnderRaid)
            {
                output.WriteLine(
                    "  HE IS AT THE GATE — the job below is Kurogane's raid and it stands alone. "
                    + "Fighting it answers him; letting the day close unanswered is a sacking.");
            }
            IReadOnlyList<EncounterOffer> board = _state.Board;
            for (int i = 0; i < board.Count; i++)
            {
                EncounterOffer o = board[i];
                string party = o.RequiredPartySize is int need
                    ? $"send exactly {need}"
                    : $"send 1-{EncounterOffer.MaxPartySize}";
                Resources spoils = _state.PromisedSpoilsFor(o);
                string stores = spoils.Food > 0 || spoils.Water > 0
                    ? $" + {spoils.Food} food, {spoils.Water} water"
                    : string.Empty;
                // The threat word is the day's band and nothing else: it is read off the calendar
                // curve, so the same word covers a lone man worth 150 gold and three men who take a
                // funeral home, and the health behind it doubles over a season while the word does
                // not move. Three played seasons worked that out by burying people. The line now
                // prints the count first, gives the health a man at a time, and says what the word is.
                double each = o.Enemies.Count > 0 ? o.EnemyHealth / o.Enemies.Count : o.EnemyHealth;
                output.WriteLine(
                    $"  {i}  {o.Enemies.Count} enem{(o.Enemies.Count == 1 ? "y" : "ies")}, "
                    + $"health {o.EnemyHealth:F0} ({each:F0} each)  [{o.Threat}]  "
                    + $"{_state.PromisedRewardFor(o)} gold{stores}  expires day {_state.ExpiryOf(o)}  "
                    + $"{party}  — {o.Sighting}");
            }

            if (board.Count == 0)
            {
                output.WriteLine("  (nothing on the board)");
            }

            output.WriteLine();
            output.WriteLine("BOUNTY");
            if (_state.Bounty is BountyContract contract)
            {
                // The board prints "send 1-4" on every job and the contract printed nothing, so a
                // player had no way to know the hunt takes a party of the same shape — one sent six
                // men at it and the run died on an unhandled WrongPartySize.
                output.WriteLine(
                    $"  {contract.Target.Name} — {contract.Reward} gold, "
                    + $"{contract.DaysLeft(_state.Day)} days left, "
                    + $"send 1-{EncounterOffer.MaxPartySize}, "
                    + $"{(_state.AcceptedBountyDay is null ? "not accepted (accept)" : "accepted (bounty sends the party)")}");
            }
            else
            {
                output.WriteLine("  (no contract posted today)");
            }

            output.WriteLine();
            output.WriteLine("RECRUITS (index  name  price  talent)");
            IReadOnlyList<RecruitOffer> recruits = _state.Recruits;
            for (int i = 0; i < recruits.Count; i++)
            {
                RecruitOffer r = recruits[i];
                output.WriteLine(
                    $"  {i}  {r.Name,-12} {r.Price,4} gold  talent {r.Talent:F2}  {r.Class}  "
                    + $"hp {r.Stats.MaxHealth:F0} str {r.Stats.Strength:F0} acc {r.Stats.Accuracy:F0}");
            }

            output.WriteLine();
            output.WriteLine("SCHOOL");
            output.WriteLine(
                $"  standing: {(_state.School.Owned.Count == 0 ? "nothing" : string.Join(", ", _state.School.Owned))}");
            if (_state.School.UnderConstruction.Count > 0)
            {
                output.WriteLine(
                    "  going up: "
                    + string.Join(", ", _state.School.UnderConstruction.Select(s => $"{s.Key} ({s.Value}d)")));
            }

            output.WriteLine(
                "  can be ordered: "
                + string.Join(
                    ", ",
                    _state.School.Available().Select(n => $"{n.Id} {n.Cost}g/{n.BuildDays}d")));

            // A post is free to fill and then takes its wage out of every day that follows, with no
            // line anywhere that added the wages up. Two played seasons died of a payroll their
            // players had read as a one-off: the standing now prices each post and the whole bill.
            output.WriteLine(
                "  staff: "
                + (_state.Staff.Hired.Count == 0
                    ? "nobody"
                    : string.Join(
                        ", ",
                        _state.Staff.Hired.Select(r => $"{r} {_state.StaffTuning.WageOf(r)}g/day"))
                      + $" — payroll {_state.Staff.DailyWage(_state.StaffTuning)} gold a day "
                      + "(dismiss <post> lets one go)"));

            output.WriteLine();
            output.WriteLine("LOG (last 10 days)");
            foreach (string line in _log.TakeLast(10))
            {
                output.WriteLine($"  {line}");
            }

            if (notes.Count > 0)
            {
                output.WriteLine();
                output.WriteLine("REFUSED");
                foreach (string note in notes)
                {
                    output.WriteLine($"  {note}");
                }
            }

            if (Over)
            {
                output.WriteLine();
                output.WriteLine($"THE SEASON IS OVER — {_state.Season.Phase}");
            }
        }
    }
}
