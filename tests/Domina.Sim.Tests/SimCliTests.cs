using Domina.Core.Combat;
using Domina.Sim;

namespace Domina.Sim.Tests;

/// <summary>Komut satırı ayrıştırma ve çıktı biçimi.</summary>
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

        // Kabul kriteri "10.000 dövüş" diyor; araç argümansız çalıştırıldığında
        // tam olarak o ölçümü yapmalı.
        Assert.Equal(10_000, options.Battles);
        Assert.Equal("3v3", options.Scenario.Name);
        Assert.Equal(1ul, options.FirstSeed);
        Assert.Null(options.CsvPath);
        Assert.IsType<NeverRetreat>(options.RetreatPolicy);
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
        // Oran daima nokta ile okunur; virgüllü yazım kabul edilmez. Aksi hâlde aynı
        // komut farklı bölge ayarlarında farklı politika üretir ve iki ölçüm
        // karşılaştırılamaz hale gelirdi.
        Assert.Equal(0.35, ((RetreatBelowHealth)Parse("--policy", "below:0.35").RetreatPolicy!).HealthFraction, precision: 9);
        Assert.NotNull(SimArgs.Parse(["--policy", "below:0,35"]).Error);
    }

    [Theory]
    [InlineData("--battles", "0")]
    [InlineData("--battles", "-5")]
    [InlineData("--battles", "abc")]
    [InlineData("--scenario", "yok")]
    [InlineData("--policy", "maybe")]
    [InlineData("--policy", "below:2")]
    [InlineData("--bilinmeyen", "1")]
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

        int exit = SimCli.Run(["--scenario", "yok"], output, error);

        Assert.Equal(SimCli.ExitUsage, exit);
        Assert.Empty(output.ToString());
        Assert.Contains("yok", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ASmallRunPrintsASummary()
    {
        var output = new StringWriter();
        int exit = SimCli.Run(["--scenario", "duel", "--battles", "25"], output, TextWriter.Null);

        string text = output.ToString();

        Assert.Equal(SimCli.ExitOk, exit);
        Assert.Contains("duel", text, StringComparison.Ordinal);
        Assert.Contains("Zafer", text, StringComparison.Ordinal);
        Assert.Contains("Uzuv kaybı", text, StringComparison.Ordinal);
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

            // Sayılar nokta ile yazılmalı: virgül ondalık ayırıcı CSV'yi bozardı.
            // Sütun sayısı başlıktan okunur; sabit yazılsaydı her yeni sütunda kırılırdı.
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
