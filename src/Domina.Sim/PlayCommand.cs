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
                    hurt.Add($"{was.Name} {entry.RecoveryDaysRemaining}d");
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

            switch (verb)
            {
                case "day":
                    return Day();

                case "expedition":
                    return Expedition(word);

                case "accept":
                    return _state.AcceptBounty() is not null ? null : "no bounty could be accepted";

                case "bounty":
                    return Bounty();

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

                case "feast":
                    return _state.Feast() ? null : "the feast was refused";

                case "gift":
                    return Gift(word);

                case "release":
                    return Warrior(word, id => _state.Release(id));

                case "retire":
                    return Warrior(word, id => _state.Retire(id));

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
            int want = word.Length > 2 ? Number(word[2]) : EncounterOffer.MaxPartySize;
            List<RosterEntry> party =
            [
                .. _state.Roster.FitForCampaign
                    .OrderByDescending(e => Score(e))
                    .Take(Math.Clamp(want, 1, EncounterOffer.MaxPartySize)),
            ];

            if (Domina.Core.Campaign.Expedition.Refuse(_state, offer, party) is ExpeditionRefusal no)
            {
                return $"the expedition was refused: {no}";
            }

            ExpeditionResult result = new Domina.Core.Campaign.Expedition().Send(
                _state,
                offer,
                party,
                _seed + (ulong)_state.Day,
                new CombatTuning());

            string told = result.Battle.Outcome == BattleOutcome.PlayerVictory
                ? $"won the fight, {result.Reward} gold"
                : "lost the fight";
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
                return $"the last night is not open (the season is {_state.Season.Phase})";
            }

            int want = word.Length > 1 ? Number(word[1]) : EncounterOffer.MaxPartySize;
            List<RosterEntry> party =
            [
                .. _state.Roster.Living
                    .Where(FinalNight.CanAnswerTheBell(_state))
                    .OrderByDescending(Score)
                    .Take(Math.Clamp(want, 1, EncounterOffer.MaxPartySize)),
            ];

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
                retreat: null);

            _log.Add(
                $"the last night, bout {round}: {(result.Won ? "won" : "LOST")} "
                + $"with {party.Count} men — {_state.Season.Phase}");
            return null;
        }

        private string? Bounty()
        {
            if (_state.Bounty is not BountyContract contract)
            {
                return "no bounty is accepted";
            }

            List<RosterEntry> party =
            [
                .. _state.Roster.FitForCampaign.OrderByDescending(Score).Take(EncounterOffer.MaxPartySize),
            ];

            if (party.Count == 0)
            {
                return "nobody is fit to hunt";
            }

            BountyResult result = new Domina.Core.Campaign.Expedition().SendToBounty(
                _state,
                contract,
                party,
                new SeededRandom(_seed + 5_000_011 + (ulong)_state.Day),
                new CombatTuning());

            Record(result.Day, $"bounty: {(result.Claimed ? "head taken" : "the hunt failed")} (the hunt closed the day)");
            return null;
        }

        private string? Restock(string[] word)
        {
            if (word.Length < 4)
            {
                return "restock needs food, water and medicine";
            }

            int spent = _state.Quartermaster.Restock(
                _state,
                new Resources(Food: Number(word[1]), Water: Number(word[2]), Medicine: Number(word[3])));

            return spent > 0
                ? null
                : "nothing was bought - restock tops the store UP TO the levels given, and it already holds that much";
        }

        private string? Hire(string[] word)
        {
            if (word.Length < 2)
            {
                return "hire needs a candidate";
            }

            return _state.HireRecruit(Number(word[1])) is not null ? null : "the candidate was not taken on";
        }

        private string? Drill(string[] word)
        {
            if (word.Length < 3)
            {
                return "drill needs a warrior and a drill";
            }

            if (!Enum.TryParse(word[2], ignoreCase: true, out Drill drill))
            {
                return $"there is no drill called {word[2]}";
            }

            return Warrior(word, id => _state.SetDrill(id, drill));
        }

        private string? Build(string[] word)
        {
            if (word.Length < 2 || !Enum.TryParse(word[1], ignoreCase: true, out SchoolNodeId node))
            {
                return $"there is no facility called {(word.Length > 1 ? word[1] : "?")}";
            }

            return _state.BuySchoolNode(node) ? null : "the order was refused";
        }

        private string? Staff(string[] word)
        {
            if (word.Length < 2 || !Enum.TryParse(word[1], ignoreCase: true, out StaffRole role))
            {
                return $"there is no post called {(word.Length > 1 ? word[1] : "?")}";
            }

            return _state.Hire(role) ? null : "the post was not filled";
        }

        private string? Gift(string[] word)
        {
            if (word.Length < 2 || !Enum.TryParse(word[1], ignoreCase: true, out Patron patron))
            {
                return $"there is no patron called {(word.Length > 1 ? word[1] : "?")}";
            }

            return _state.SendGift(patron) ? null : "the gift was refused";
        }

        private string? Charm(string[] word)
        {
            if (word.Length < 2 || !Enum.TryParse(word[1], ignoreCase: true, out OmamoriKind kind))
            {
                return $"there is no charm called {(word.Length > 1 ? word[1] : "?")}";
            }

            return _state.BuyCharm(kind) ? null : "the charm was not bought";
        }

        private string? Fit(string[] word)
        {
            if (word.Length < 3 || !Enum.TryParse(word[2], ignoreCase: true, out OmamoriKind kind))
            {
                return "fit needs a warrior and a charm";
            }

            return Warrior(word, id => _state.FitCharm(id, kind));
        }

        private string? Class(string[] word)
        {
            if (word.Length < 3 || !Enum.TryParse(word[2], ignoreCase: true, out WarriorClass klass))
            {
                return "class needs a warrior and a class";
            }

            return Warrior(word, id => _state.TrainClass(id, klass));
        }

        private string? Path(string[] word)
        {
            if (word.Length < 3 || !Enum.TryParse(word[2], ignoreCase: true, out WarriorPath path))
            {
                return "path needs a warrior and a path";
            }

            return Warrior(word, id => _state.ChoosePath(id, path));
        }

        private string? Warrior(string[] word, Func<WarriorId, bool> act)
        {
            if (word.Length < 2)
            {
                return "the move names no warrior";
            }

            List<RosterEntry> living = Living();
            int index = Number(word[1]);
            if (index < 0 || index >= living.Count)
            {
                return $"there is no warrior {index}";
            }

            return act(living[index].Warrior.Id) ? null : "the move was refused";
        }

        private List<RosterEntry> Living() =>
            [.. _state.Roster.Living.OrderBy(e => e.Warrior.Name, StringComparer.Ordinal)];

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

            if (report.Sacked is SackReport sack)
            {
                line.Append(CultureInfo.InvariantCulture, $" | SACKED: -{sack.Gold} gold, -{sack.Food} food");
            }

            if (report.MissedWeek)
            {
                line.Append(" | a week closed with no fight filed");
            }

            if (report.Tribunal is not null)
            {
                line.Append(" | the tribunal sat");
            }

            if (report.Opened is { Count: > 0 } opened)
            {
                line.Append(CultureInfo.InvariantCulture, $" | opened: {string.Join(", ", opened)}");
            }

            _log.Add(line.ToString());
        }

        /// <summary>Prints where the season stands, and what can be done about it.</summary>
        public void Write(TextWriter output, List<string> notes)
        {
            Resources purse = _state.Resources;

            output.WriteLine($"DAY {_state.Day} of {_days}   phase {_state.Season.Phase}");
            output.WriteLine(
                $"PURSE  {purse.Gold} gold | food {purse.Food} | water {purse.Water} | medicine {purse.Medicine}");

            Resources draw = _state.DailyDraw();
            output.WriteLine(
                $"DRAW   today takes food {draw.Food}, water {draw.Water}, medicine {draw.Medicine}, gold {draw.Gold}");
            output.WriteLine(
                $"SEASON heads {_state.Season.HeadsTaken}/3, gate {(_state.Season.GateOpen ? "open" : "shut")}, "
                + $"quiet weeks in a row {_state.Season.MissedStreak}");

            output.WriteLine();
            output.WriteLine("ROSTER (index name  health  infirmary  drill  morale  honor  class)");
            List<RosterEntry> living = Living();
            for (int i = 0; i < living.Count; i++)
            {
                RosterEntry e = living[i];
                WarriorStats s = e.Warrior.EffectiveStats;
                output.WriteLine(
                    $"  {i}  {e.Warrior.Name,-12} hp {s.MaxHealth,3:F0}  "
                    + $"{(e.RecoveryDaysRemaining > 0 ? $"infirmary {e.RecoveryDaysRemaining}d" : "fit        ")}  "
                    + $"{e.Drill,-12} morale {e.Warrior.Morale,5:F0}  honor {e.Warrior.Honor,5:F0}  "
                    + $"{e.Warrior.Class}  str {s.Strength:F0} acc {s.Accuracy:F0} def {s.Defense:F0} eva {s.Evasion:F0}");
            }

            output.WriteLine($"  beds {_state.Capacity}, fit for the field {_state.Roster.FitForCampaign.Count()}");

            output.WriteLine();
            output.WriteLine("BOARD (index  threat  enemies  promised  expires  party)");
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
                output.WriteLine(
                    $"  {i}  {o.Threat,-7} {o.Enemies.Count} men, health {o.EnemyHealth:F0}  "
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
                output.WriteLine(
                    $"  {contract.Target.Name} — {contract.Reward} gold, "
                    + $"{contract.DaysLeft(_state.Day)} days left, "
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

            output.WriteLine(
                $"  staff: {(_state.Staff.Hired.Count == 0 ? "nobody" : string.Join(", ", _state.Staff.Hired))}");

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
