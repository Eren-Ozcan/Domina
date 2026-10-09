using System.Diagnostics;
using Domina.Core.Combat;

namespace Domina.Sim;

/// <summary>The batch simulation tool's entry point.</summary>
internal static class SimCli
{
    public const int ExitOk = 0;
    public const int ExitUsage = 2;
    public const int ExitIoError = 3;

    /// <param name="fighter">
    /// What fights the measurement's fights; the engine-free resolver if left out. The engine bed passes
    /// its own, so that the same measurement runs inside Godot over the engine's ground
    /// (docs/PLAN-ENGINE-RESOLUTION.md, Phase B).
    /// </param>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, IFightRunner? fighter = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        // The replay and the played season ask the door for the default resolver and are not handed this
        // one, so asking for them over another resolver would quietly run the old one.
        if (fighter is not null and not CoreFightRunner && (ReplayCommand.Wanted(args) || PlayCommand.Wanted(args)))
        {
            error.WriteLine("the replay and the played season run on the engine-free resolver only");
            return ExitUsage;
        }

        // The replay takes a path and nothing else, so it is answered before the measurement's own
        // options are parsed: the two commands share no settings (see ReplayCommand).
        if (ReplayCommand.Wanted(args))
        {
            return ReplayCommand.Run(args, output, error);
        }

        // The played season is answered here for the same reason: it takes a script and a seed, and
        // none of the measurement's thousand switches mean anything to it.
        if (PlayCommand.Wanted(args))
        {
            return PlayCommand.Run(output, args);
        }

        ParsedArgs parsed = SimArgs.Parse(args);

        if (parsed.HelpRequested)
        {
            SimArgs.WriteUsage(output);
            output.WriteLine();
            ReplayCommand.WriteUsage(output);
            return ExitOk;
        }

        if (parsed.Options is null)
        {
            error.WriteLine(parsed.Error);
            error.WriteLine();
            SimArgs.WriteUsage(error);
            return ExitUsage;
        }

        SimOptions options = parsed.Options;

        // The implements the run was asked to measure, before the first weapon is built.
        ImplementBench.Apply(options.Implements);

        if (options.Campaign is CampaignOptions campaign)
        {
            long began = Stopwatch.GetTimestamp();
            CampaignOptions fought = fighter is null ? campaign : campaign with { Fighter = fighter };
            CampaignReport campaignReport = new CampaignRunner(fought).Run(options.FirstSeed);
            CampaignSummaryReport.Write(
                output, campaign, campaignReport, Stopwatch.GetElapsedTime(began));
            return ExitOk;
        }

        var runner = new BatchRunner(
            options.Scenario,
            options.RetreatPolicy,
            options.Tuning,
            options.PlayerArmor,
            options.PlayerSpeed,
            options.EffectiveMoraleBand,
            options.PlayerMorale,
            options.PlayerMastery,
            options.EffectiveMasteryBand,
            fighter);

        try
        {
            BatchReport report = Execute(runner, options, out TimeSpan wallClock);
            SummaryReport.Write(output, options, report, wallClock);

            if (options.CsvPath is not null)
            {
                output.WriteLine();
                output.WriteLine($"CSV written: {Path.GetFullPath(options.CsvPath)}");
            }

            return ExitOk;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error.WriteLine($"CSV could not be written: {ex.Message}");
            return ExitIoError;
        }
    }

    private static BatchReport Execute(BatchRunner runner, SimOptions options, out TimeSpan wallClock)
    {
        if (options.CsvPath is null)
        {
            long start = Stopwatch.GetTimestamp();
            BatchReport report = runner.Run(options.FirstSeed, options.Battles);
            wallClock = Stopwatch.GetElapsedTime(start);
            return report;
        }

        using var file = new StreamWriter(options.CsvPath, append: false);
        var csv = new CsvReport(file);
        csv.WriteHeader();

        long started = Stopwatch.GetTimestamp();
        BatchReport withCsv = runner.Run(options.FirstSeed, options.Battles, csv.WriteRow);
        wallClock = Stopwatch.GetElapsedTime(started);
        return withCsv;
    }
}
