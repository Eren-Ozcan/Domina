using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Sim;

namespace Domina.Sim.Tests;

/// <summary>Command-line parsing and the output format.</summary>
public class SimCliTests
{
    private static SimOptions Parse(params string[] args)
    {
        ParsedArgs parsed = SimArgs.Parse(args);
        Assert.Null(parsed.Error);
        Assert.NotNull(parsed.Options);
        return parsed.Options!;
    }

    [Fact]
    public void DefaultsMatchThePhaseOneAcceptanceCriterion()
    {
        SimOptions options = Parse();

        // The acceptance criterion says "10,000 fights"; run with no arguments the tool must make
        // exactly that measurement.
        Assert.Equal(10_000, options.Battles);
        Assert.Equal("3v3", options.Scenario.Name);
        Assert.Equal(1ul, options.FirstSeed);
        Assert.Null(options.CsvPath);
        Assert.IsType<NeverRetreat>(options.RetreatPolicy);
    }

    /// <summary>
    /// A long horizon can only be measured with a policy that <b>adapts</b>.
    /// </summary>
    /// <remarks>
    /// A fixed threat band sooner or later declines every offer on a curve that grows as the days pass;
    /// what is measured then is the policy's bankruptcy, not the economy's.
    /// </remarks>
    [Fact]
    public void TheAcceptanceRatioAndTheRiskPremiumAreSweepable()
    {
        SimOptions options = Parse(
            "--mode", "campaign",
            "--accept-ratio", "1.5",
            "--risk-premium", "0.4",
            "--risk-free-health", "120");

        Assert.Equal(1.5, options.Campaign!.AcceptRatio!.Value, precision: 9);
        Assert.Equal(0.4, options.Campaign.Economy.RiskPremium, precision: 9);
        Assert.Equal(120, options.Campaign.Economy.RiskFreeEnemyHealth, precision: 9);
    }

    [Fact]
    public void TheSchoolAndThePathsAreSweepable()
    {
        SimOptions options = Parse(
            "--mode", "campaign",
            "--school", "on",
            "--school-only", "infirmary",
            "--paths", "on",
            "--path-days", "12");

        Assert.True(options.Campaign!.UseSchool);
        Assert.Equal(SchoolBranch.Infirmary, options.Campaign.SchoolOnly);
        Assert.True(options.Campaign.UsePaths);
        Assert.Equal(12, options.Campaign.Dojo.Training.PathTrainingDays);
    }

    /// <summary>The training rate must be sweepable — a number is only locked by sweeping it.</summary>
    [Fact]
    public void TrainingNumbersAreSweepable()
    {
        SimOptions options = Parse(
            "--mode", "campaign", "--train-rate", "0.04", "--train-ceiling", "80");

        Assert.Equal(0.04, options.Campaign!.Dojo.Training.GapClosedPerDay, precision: 9);
        Assert.Equal(80, options.Campaign.Dojo.Training.SkillCeiling, precision: 9);
    }

    [Fact]
    public void OptionsAreParsed()
    {
        SimOptions options = Parse("--scenario", "duel", "--battles", "42", "--seed", "7", "--out", "x.csv");

        Assert.Equal("duel", options.Scenario.Name);
        Assert.Equal(42, options.Battles);
        Assert.Equal(7ul, options.FirstSeed);
        Assert.Equal("x.csv", options.CsvPath);
    }

    /// <summary>
    /// Poison's four knobs must be sweepable from the command line.
    /// </summary>
    /// <remarks>
    /// Numbers are only locked by sweeping (see docs/PROGRESS.md); a rule with no sweep knob is a rule
    /// that cannot be measured.
    /// </remarks>
    [Fact]
    public void ThePoisonKnobsCanBeSwept()
    {
        SimOptions options = Parse(
            "--poison-damage", "2.5",
            "--poison-seconds", "6",
            "--poison-tick", "1",
            "--poison-dose", "3");

        Assert.Equal(2.5, options.Tuning.PoisonDamagePerTick, precision: 9);
        Assert.Equal(6, options.Tuning.PoisonSeconds, precision: 9);
        Assert.Equal(1, options.Tuning.PoisonTickSeconds, precision: 9);
        Assert.Equal(3, options.Tuning.PoisonMaxDose, precision: 9);
    }

    [Fact]
    public void TheRetreatPolicyCanStandInForThePlayer()
    {
        SimOptions options = Parse("--policy", "below:0.35");

        var policy = Assert.IsType<RetreatBelowHealth>(options.RetreatPolicy);
        Assert.Equal(0.35, policy.HealthFraction, precision: 9);
        Assert.Contains("35", options.PolicyLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePolicyFractionIsReadInvariantly()
    {
        // A ratio is always read with a dot; a comma form is not accepted. Otherwise the same command
        // would produce different policies under different locale settings and two measurements could
        // not be compared.
        Assert.Equal(0.35, ((RetreatBelowHealth)Parse("--policy", "below:0.35").RetreatPolicy!).HealthFraction, precision: 9);
        Assert.NotNull(SimArgs.Parse(["--policy", "below:0,35"]).Error);
    }

    [Theory]
    [InlineData("--battles", "0")]
    [InlineData("--battles", "-5")]
    [InlineData("--battles", "abc")]
    [InlineData("--scenario", "missing")]
    [InlineData("--policy", "maybe")]
    [InlineData("--policy", "below:2")]
    [InlineData("--accept-ratio", "0")]
    [InlineData("--risk-premium", "-1")]
    [InlineData("--risk-free-health", "0")]
    [InlineData("--school", "belki")]
    [InlineData("--school-only", "mutfak")]
    [InlineData("--paths", "belki")]
    [InlineData("--path-days", "0")]
    [InlineData("--train-rate", "2")]
    [InlineData("--train-ceiling", "0")]
    [InlineData("--unknown", "1")]
    public void BadInputIsRejectedWithAMessage(string flag, string value)
    {
        ParsedArgs parsed = SimArgs.Parse([flag, value]);

        Assert.Null(parsed.Options);
        Assert.False(string.IsNullOrWhiteSpace(parsed.Error));
    }

    [Fact]
    public void AMissingValueIsAnError()
    {
        Assert.NotNull(SimArgs.Parse(["--battles"]).Error);
        Assert.NotNull(SimArgs.Parse(["tuhaf"]).Error);
    }

    [Fact]
    public void HelpIsRequestedNotAnError()
    {
        Assert.True(SimArgs.Parse(["--help"]).HelpRequested);
        Assert.True(SimArgs.Parse(["-h"]).HelpRequested);
        Assert.Null(SimArgs.Parse(["--help"]).Error);
    }

    [Fact]
    public void HelpListsEveryScenario()
    {
        var output = new StringWriter();
        int exit = SimCli.Run(["--help"], output, TextWriter.Null);

        Assert.Equal(SimCli.ExitOk, exit);
        foreach (Scenario scenario in Scenarios.All)
        {
            Assert.Contains(scenario.Name, output.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BadUsageExplainsItselfOnStandardError()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exit = SimCli.Run(["--scenario", "missing"], output, error);

        Assert.Equal(SimCli.ExitUsage, exit);
        Assert.Empty(output.ToString());
        Assert.Contains("missing", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ASmallRunPrintsASummary()
    {
        var output = new StringWriter();
        int exit = SimCli.Run(["--scenario", "duel", "--battles", "25"], output, TextWriter.Null);

        string text = output.ToString();

        Assert.Equal(SimCli.ExitOk, exit);
        Assert.Contains("duel", text, StringComparison.Ordinal);
        Assert.Contains("Victory", text, StringComparison.Ordinal);
        Assert.Contains("Limb losses", text, StringComparison.Ordinal);
        Assert.Contains("seed 1..25", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvGetsAHeaderAndOneRowPerBattle()
    {
        string path = Path.Combine(Path.GetTempPath(), $"domina-sim-{Guid.NewGuid():N}.csv");

        try
        {
            int exit = SimCli.Run(
                ["--scenario", "duel", "--battles", "10", "--seed", "1", "--out", path],
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(SimCli.ExitOk, exit);

            string[] lines = File.ReadAllLines(path);

            Assert.Equal(11, lines.Length);
            Assert.StartsWith("seed,outcome,seconds", lines[0], StringComparison.Ordinal);
            Assert.StartsWith("1,", lines[1], StringComparison.Ordinal);

            // The numbers must be written with a dot: a comma decimal separator would break the CSV.
            // The column count is read from the header; hard-coded it would break on every new column.
            int columns = lines[0].Count(c => c == ',');
            foreach (string line in lines.Skip(1))
            {
                Assert.Equal(columns, line.Count(c => c == ','));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
