using Domina.Core.Dojo.Journal;

namespace Domina.Sim;

/// <summary>
/// Walks a move journal and says whether the run still comes out the same.
/// </summary>
/// <remarks>
/// <para>
/// This is the journal's consumer. The game writes <c>moves.jsonl</c> beside its save; this reads it,
/// rebuilds the dojo from the seed on the opening line and makes every move again, comparing the dojo
/// against what was observed at the time. The core is engine-free, so a session a player complains
/// about can be walked here, in a terminal, without Godot.
/// </para>
/// <para>
/// It is <b>also the balance tool's other half</b>. The batch runner answers "what do ten thousand
/// campaigns do"; this answers "does the run I already have still do what it did" — which is the
/// question a retuning has to be held against, and the one a measurement cannot ask.
/// </para>
/// <para>
/// It lives outside the argument parser on purpose: the parser builds a measurement's options, and a
/// replay takes one path and nothing else. The two share no settings.
/// </para>
/// </remarks>
internal static class ReplayCommand
{
    /// <summary>The flag that hands the run to this instead of to a measurement.</summary>
    public const string Flag = "--replay";

    /// <summary>The run diverged: the journal and the replay stopped agreeing.</summary>
    public const int ExitDiverged = 1;

    /// <summary>Is this a replay invocation?</summary>
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

    /// <summary>Walks the journal and writes the report.</summary>
    /// <returns>
    /// <see cref="SimCli.ExitOk"/> if every move came out the same, <see cref="ExitDiverged"/> if one
    /// did not, <see cref="SimCli.ExitUsage"/> if no file was named and
    /// <see cref="SimCli.ExitIoError"/> if it could not be read.
    /// </returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (Path(args) is not string path)
        {
            error.WriteLine($"{Flag} needs the path of a journal file.");
            error.WriteLine();
            WriteUsage(error);
            return SimCli.ExitUsage;
        }

        MoveJournal journal = MoveJournal.Load(path);

        if (journal.Count == 0)
        {
            error.WriteLine($"No moves could be read from {System.IO.Path.GetFullPath(path)}.");
            foreach (string complaint in journal.Warnings)
            {
                error.WriteLine($"  {complaint}");
            }

            return SimCli.ExitIoError;
        }

        if (journal.Seed is null)
        {
            error.WriteLine(
                "The journal has no Start line, so the run's seed is unknown and it cannot be replayed.");
            return SimCli.ExitIoError;
        }

        foreach (string complaint in journal.Warnings)
        {
            output.WriteLine($"Warning: {complaint}");
        }

        ReplayReport report = MoveReplay.Run(journal);
        bool verbose = args.Contains("--verbose");

        Header(output, path, journal, report);

        // The faults the run filed at the time are printed whatever happens: they are the reason a
        // journal usually arrives, and they are not the replay's own findings.
        foreach (Move fault in journal.Faults)
        {
            output.WriteLine(
                $"  fault  day {fault.Day}  {fault.Text("where")}: {fault.Text("what")}");
        }

        foreach (ReplayNote skipped in report.Skipped)
        {
            output.WriteLine($"  skipped  {skipped}");
        }

        if (verbose)
        {
            output.WriteLine();
            output.WriteLine(report.Describe());
        }

        if (report.FirstDivergence is not ReplayNote drift)
        {
            output.WriteLine();
            output.WriteLine("The run came out the same, move for move.");
            return SimCli.ExitOk;
        }

        output.WriteLine();
        output.WriteLine($"The run diverged at {drift}");
        output.WriteLine(
            "Everything before that line matched, so that move is where the change bit.");

        return ExitDiverged;
    }

    /// <summary>What the replay found, in four lines.</summary>
    private static void Header(
        TextWriter output,
        string path,
        MoveJournal journal,
        ReplayReport report)
    {
        int faults = journal.Faults.Count();

        output.WriteLine($"Journal: {System.IO.Path.GetFullPath(path)}");
        output.WriteLine(
            $"Seed {journal.Seed}, {journal.Difficulty?.ToString() ?? "unstated tier"}, "
            + $"{journal.Count} moves, {journal.Moves[^1].Day - journal.Moves[0].Day + 1} days.");
        output.WriteLine(
            $"Replayed to day {report.Dojo.Day}: {report.Dojo.Resources.Gold} gold, "
            + $"{report.Dojo.Roster.Living.Count()} standing.");
        output.WriteLine(
            $"{report.Notes.Count(n => n.Kind == ReplayNoteKind.Matched)} matched, "
            + $"{report.Skipped.Count()} skipped, {faults} fault{(faults == 1 ? string.Empty : "s")} filed.");
    }

    /// <summary>The path that follows the flag; <c>null</c> if none does.</summary>
    private static string? Path(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], Flag, StringComparison.Ordinal)
                && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    public static void WriteUsage(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("Replaying a run:");
        writer.WriteLine("  Domina.Sim --replay <moves.jsonl> [--verbose]");
        writer.WriteLine();
        writer.WriteLine("  Rebuilds the dojo from the journal's seed and makes every move again,");
        writer.WriteLine("  comparing it line by line. Exit 0 if the run came out the same, 1 if it");
        writer.WriteLine("  diverged — and the line it diverged at is where the change bit.");
        writer.WriteLine("  The game writes its journal beside the save, as user://moves.jsonl.");
    }
}
