using System.Text;
using Domina.Core.Campaign;

namespace Domina.Core.Dojo.Journal;

/// <summary>
/// Every move of a run, in the order it was made.
/// </summary>
/// <remarks>
/// <para>
/// This is the project's test data. The core is seeded and deterministic on purpose
/// (<c>CLAUDE.md</c> → "Architecture rule"), so <b>a seed plus this list is the whole run</b>: the
/// opening state comes from the seed, everything after it comes from the moves. A session that
/// misbehaved can therefore be handed to <see cref="MoveReplay"/> and walked again in a test, without
/// the engine and without a saved screenshot of the state.
/// </para>
/// <para>
/// Two things follow from that and they are the reason the file looks the way it does. First,
/// <b>a refused move is written down too</b>: "the player pressed and nothing happened" is exactly the
/// report a bug arrives as, and a journal that only kept successes would delete it. Second, each line
/// carries what the dojo looked like <b>after</b> the move
/// (<see cref="Move.After"/>) — a replay that drifts is then caught at the line where it drifted, not
/// twenty days later.
/// </para>
/// <para>
/// The format is JSONL: one move a line, appendable, readable in a terminal, and a truncated file (the
/// game was killed mid-write) still loads every complete line before the break.
/// </para>
/// </remarks>
public sealed class MoveJournal
{
    private readonly List<Move> _moves = [];

    /// <summary>Whether moves are being written down at all.</summary>
    /// <remarks>
    /// The batch runner turns it off: a hundred thousand measured campaigns do not need a hundred
    /// thousand journals, and the list would be the run's largest allocation by far.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>The moves so far, oldest first.</summary>
    public IReadOnlyList<Move> Moves => _moves;

    /// <summary>How many moves have been written down.</summary>
    public int Count => _moves.Count;

    /// <summary>The lines that could not be read when this journal was loaded.</summary>
    /// <remarks>Empty for a live run. A loaded file keeps its complaints here instead of throwing.</remarks>
    public IReadOnlyList<string> Warnings { get; private set; } = [];

    /// <summary>Writes the run's opening line: the seed and the tier every later move is read against.</summary>
    public Move? Start(ulong seed, DifficultyTier tier, int day = 1)
    {
        if (!Enabled)
        {
            return null;
        }

        Move move = new(
            day,
            MoveKind.Start,
            [MoveArg.Of("seed", seed), MoveArg.Of("difficulty", tier)],
            []);

        _moves.Add(move);
        return move;
    }

    /// <summary>Writes a move down, with the dojo's state as it stands at this moment.</summary>
    /// <param name="state">The dojo the move was made against.</param>
    /// <param name="kind">Which move it was.</param>
    /// <param name="ok">Did it take effect? A refusal is recorded, not dropped.</param>
    /// <param name="args">The move's own arguments — what a replay hands back to the same method.</param>
    public Move? Record(DojoState state, MoveKind kind, bool ok, params MoveArg[] args)
    {
        ArgumentNullException.ThrowIfNull(state);
        return RecordOn(state, state.Day, kind, ok, args);
    }

    /// <summary>
    /// The same, for the one move whose day is not the dojo's current day.
    /// </summary>
    /// <remarks>
    /// Closing the day moves the calendar on, so by the time the move is written down the dojo already
    /// stands on the morning after. The move belongs to the day the player spent, which is why that day
    /// is passed in — the state observed alongside it is still the state that followed the move.
    /// </remarks>
    public Move? RecordOn(DojoState state, int day, MoveKind kind, bool ok, params MoveArg[] args)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!Enabled)
        {
            return null;
        }

        Move move = new(day, kind, args ?? [], Observe(state, ok));
        _moves.Add(move);
        return move;
    }

    /// <summary>
    /// The same, with the move's line-by-line detail: who took the wound, what the bill was made of.
    /// </summary>
    /// <remarks>
    /// The rows are <b>observations</b> and no replay reads them (<see cref="Move.Detail"/>). They are
    /// written because the questions asked of a finished run are detailed ones — which limb, whose
    /// gold, what the day taught him — and a journal of totals answers none of them.
    /// </remarks>
    public Move? RecordDetailed(
        DojoState state,
        int day,
        MoveKind kind,
        bool ok,
        IReadOnlyList<MoveRow> detail,
        params MoveArg[] args)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!Enabled)
        {
            return null;
        }

        Move move = new(day, kind, args ?? [], Observe(state, ok))
        {
            Detail = detail ?? [],
        };

        _moves.Add(move);
        return move;
    }

    /// <summary>Takes a loaded move as it stands, without observing anything.</summary>
    internal void Append(Move move)
    {
        ArgumentNullException.ThrowIfNull(move);
        _moves.Add(move);
    }

    /// <summary>Forgets everything — a new run in the same process starts with an empty journal.</summary>
    public void Clear()
    {
        _moves.Clear();
        Warnings = [];
    }

    /// <summary>
    /// What the dojo looked like the moment after a move.
    /// </summary>
    /// <remarks>
    /// Kept deliberately small and cheap: the figures that move on nearly every decision, and that a
    /// diverging replay shows up in first. Anything derived from them (what the roster is worth, how
    /// the season is going) is recomputed by whoever reads the journal.
    /// </remarks>
    public static IReadOnlyList<MoveArg> Observe(DojoState state, bool ok)
    {
        ArgumentNullException.ThrowIfNull(state);

        return
        [
            MoveArg.Of(Move.OkKey, ok),
            MoveArg.Of("gold", state.Resources.Gold),
            MoveArg.Of("food", state.Resources.Food),
            MoveArg.Of("sake", state.Resources.Sake),
            MoveArg.Of("living", state.Roster.Living.Count()),
            MoveArg.Of("injured", state.Roster.Living.Count(e => e.RecoveryDaysRemaining > 0)),
        ];
    }

    /// <summary>The whole journal as the text of a <c>.jsonl</c> file.</summary>
    public string ToJsonl()
    {
        StringBuilder text = new();
        foreach (Move move in _moves)
        {
            text.Append(move.ToJson()).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Writes the journal to disk, creating the folder if it is not there.</summary>
    /// <remarks>
    /// Plain file IO and nothing else: the core must not reach for the engine, so the game hands it a
    /// path the engine resolved rather than a Godot <c>user://</c> address.
    /// </remarks>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(path, ToJsonl(), Encoding.UTF8);
    }

    /// <summary>Reads a journal back from the text of a file.</summary>
    /// <remarks>
    /// It never throws. A line that cannot be read is skipped and its complaint is kept in
    /// <see cref="Warnings"/> — a file cut short by a crash is precisely the file worth replaying.
    /// </remarks>
    public static MoveJournal Parse(string jsonl)
    {
        MoveJournal journal = new();
        List<string> warnings = [];

        if (!string.IsNullOrWhiteSpace(jsonl))
        {
            string[] lines = jsonl.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                Move? move = Move.FromJson(line, out string? problem);
                if (move is null)
                {
                    warnings.Add($"Line {i + 1} was skipped: {problem ?? "it could not be read"}.");
                    continue;
                }

                journal.Append(move);
            }
        }

        journal.Warnings = warnings;
        return journal;
    }

    /// <summary>Reads a journal from disk; a missing file gives an empty journal with one warning.</summary>
    public static MoveJournal Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            MoveJournal empty = new();
            empty.Warnings = [$"The journal at {path} could not be read: {problem.Message}"];
            return empty;
        }
    }

    /// <summary>
    /// The faults filed during the run — what broke, in the order it broke.
    /// </summary>
    /// <remarks>
    /// The first thing to look at in a journal that came from a bug report: each fault carries where
    /// it happened and what it said, and the moves around it say what the run was doing at the time.
    /// </remarks>
    public IEnumerable<Move> Faults => _moves.Where(m => m.Kind == MoveKind.Fault);

    /// <summary>The seed the run opened with, read off the <see cref="MoveKind.Start"/> line.</summary>
    public ulong? Seed => _moves.FirstOrDefault(m => m.Kind == MoveKind.Start)?.Seed("seed");

    /// <summary>The tier the run was played at, read off the same line.</summary>
    public DifficultyTier? Difficulty => _moves
        .FirstOrDefault(m => m.Kind == MoveKind.Start)?
        .Choice<DifficultyTier>("difficulty");
}
