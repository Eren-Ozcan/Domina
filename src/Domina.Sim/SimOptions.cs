using System.Globalization;
using Domina.Core.Combat;

namespace Domina.Sim;

/// <summary>Komut satırından çözülmüş çalıştırma ayarları.</summary>
internal sealed record SimOptions(
    Scenario Scenario,
    int Battles,
    ulong FirstSeed,
    IRetreatPolicy? RetreatPolicy,
    string PolicyLabel,
    string? CsvPath);

/// <summary>Ayrıştırma sonucu: ayarlar, yardım isteği veya hata.</summary>
internal sealed record ParsedArgs(SimOptions? Options, string? Error, bool HelpRequested)
{
    public static ParsedArgs Help() => new(null, null, HelpRequested: true);

    public static ParsedArgs Fail(string message) => new(null, message, HelpRequested: false);

    public static ParsedArgs Ok(SimOptions options) => new(options, null, HelpRequested: false);
}

internal static class SimArgs
{
    public const int DefaultBattles = 10_000;
    public const ulong DefaultSeed = 1;
    public const string DefaultScenario = "3v3";

    public static ParsedArgs Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string scenarioName = DefaultScenario;
        int battles = DefaultBattles;
        ulong firstSeed = DefaultSeed;
        string policySpec = "never";
        string? csvPath = null;

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];

            if (arg is "-h" or "--help")
            {
                return ParsedArgs.Help();
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                return ParsedArgs.Fail($"Beklenmeyen argüman: {arg}");
            }

            if (i + 1 >= args.Count)
            {
                return ParsedArgs.Fail($"{arg} bir değer bekliyor.");
            }

            string value = args[++i];

            switch (arg)
            {
                case "--scenario":
                    scenarioName = value;
                    break;

                case "--battles":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out battles)
                        || battles <= 0)
                    {
                        return ParsedArgs.Fail($"--battles pozitif bir tam sayı olmalı: {value}");
                    }

                    break;

                case "--seed":
                    if (!ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out firstSeed))
                    {
                        return ParsedArgs.Fail($"--seed negatif olmayan bir tam sayı olmalı: {value}");
                    }

                    break;

                case "--policy":
                    policySpec = value;
                    break;

                case "--out":
                    csvPath = value;
                    break;

                default:
                    return ParsedArgs.Fail($"Bilinmeyen seçenek: {arg}");
            }
        }

        Scenario? scenario = Scenarios.Find(scenarioName);
        if (scenario is null)
        {
            string known = string.Join(", ", Scenarios.All.Select(s => s.Name));
            return ParsedArgs.Fail($"Bilinmeyen senaryo: {scenarioName} (bilinenler: {known})");
        }

        if (!TryParsePolicy(policySpec, out IRetreatPolicy? policy, out string label))
        {
            return ParsedArgs.Fail($"Bilinmeyen kaçış politikası: {policySpec} (never | below:<0-1>)");
        }

        return ParsedArgs.Ok(new SimOptions(scenario, battles, firstSeed, policy, label, csvPath));
    }

    /// <summary>
    /// Kaçış politikasını çözer.
    /// </summary>
    /// <remarks>
    /// Oyunda kaçış kararı oyuncunun tuşundan gelir; burada onun yerine bir politika
    /// geçer. "Hiç çekilmeyen" ile "canı %30'a düşünce çeken" oyuncu arasındaki ölüm
    /// ve sakatlık farkını ölçmek, uzuv kaybı mekaniğinin dengesinin tek yoludur —
    /// uzuv kaybı yalnızca zamanında müdahale edilen dövüşlerde oluşur.
    /// </remarks>
    private static bool TryParsePolicy(string spec, out IRetreatPolicy? policy, out string label)
    {
        if (string.Equals(spec, "never", StringComparison.OrdinalIgnoreCase))
        {
            policy = NeverRetreat.Instance;
            label = "hiç çekilme";
            return true;
        }

        if (spec.StartsWith("below:", StringComparison.OrdinalIgnoreCase))
        {
            string number = spec["below:".Length..];
            if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double fraction)
                && fraction is >= 0 and <= 1)
            {
                policy = new RetreatBelowHealth(fraction);
                label = string.Create(
                    CultureInfo.InvariantCulture,
                    $"can %{fraction * 100:0.#} altına düşünce çek");
                return true;
            }
        }

        policy = null;
        label = string.Empty;
        return false;
    }

    public static void WriteUsage(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("Domina toplu dövüş simülasyonu — denge ölçümü için.");
        writer.WriteLine();
        writer.WriteLine("Kullanım:");
        writer.WriteLine("  Domina.Sim [--scenario <ad>] [--battles <N>] [--seed <S>]");
        writer.WriteLine("             [--policy never|below:<oran>] [--out <dosya.csv>]");
        writer.WriteLine();
        writer.WriteLine("Seçenekler:");
        writer.WriteLine($"  --scenario  Koşturulacak eşleşme (varsayılan: {DefaultScenario})");
        writer.WriteLine($"  --battles   Dövüş sayısı (varsayılan: {DefaultBattles})");
        writer.WriteLine($"  --seed      İlk seed; sonrakiler birer artar (varsayılan: {DefaultSeed})");
        writer.WriteLine("  --policy    Kaçış politikası: never | below:0.3 (varsayılan: never)");
        writer.WriteLine("  --out       Dövüş başına satır yazılacak CSV dosyası");
        writer.WriteLine();
        writer.WriteLine("Senaryolar:");
        foreach (Scenario s in Scenarios.All)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {s.Name,-10} {s.Description}"));
        }
    }
}
