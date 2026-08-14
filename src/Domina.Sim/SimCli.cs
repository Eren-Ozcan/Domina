using System.Diagnostics;

namespace Domina.Sim;

/// <summary>Toplu simülasyon aracının giriş noktası.</summary>
internal static class SimCli
{
    public const int ExitOk = 0;
    public const int ExitUsage = 2;
    public const int ExitIoError = 3;

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        ParsedArgs parsed = SimArgs.Parse(args);

        if (parsed.HelpRequested)
        {
            SimArgs.WriteUsage(output);
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
        var runner = new BatchRunner(
            options.Scenario, options.RetreatPolicy, options.Tuning, options.PlayerArmor);

        try
        {
            BatchReport report = Execute(runner, options, out TimeSpan wallClock);
            SummaryReport.Write(output, options, report, wallClock);

            if (options.CsvPath is not null)
            {
                output.WriteLine();
                output.WriteLine($"CSV yazıldı: {Path.GetFullPath(options.CsvPath)}");
            }

            return ExitOk;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error.WriteLine($"CSV yazılamadı: {ex.Message}");
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
