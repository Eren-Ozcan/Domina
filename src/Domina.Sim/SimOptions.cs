using System.Globalization;
using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Sim;

/// <summary>Komut satırından çözülmüş çalıştırma ayarları.</summary>
/// <param name="PlayerArmor">
/// Verilmişse senaryodaki kuşamı ezer; <c>null</c> ise senaryonunki kullanılır.
/// </param>
internal sealed record SimOptions(
    Scenario Scenario,
    int Battles,
    ulong FirstSeed,
    IRetreatPolicy? RetreatPolicy,
    string PolicyLabel,
    string? CsvPath,
    CombatTuning Tuning,
    Armor? PlayerArmor,
    string ArmorLabel,
    double? PlayerSpeed = null);

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
        CombatTuning tuning = CombatTuning.Default;
        Armor? playerArmor = null;
        string armorLabel = "senaryodaki";
        double? playerSpeed = null;

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

                case "--grievous":
                    if (!TryFraction(value, out double grievous))
                    {
                        return ParsedArgs.Fail($"--grievous 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { GrievousSeverityThreshold = grievous };
                    break;

                case "--sever":
                    if (!TryFraction(value, out double sever))
                    {
                        return ParsedArgs.Fail($"--sever 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { BaseDismembermentChance = sever };
                    break;

                case "--charge-chance":
                    if (!TryFraction(value, out double chargeChance))
                    {
                        return ParsedArgs.Fail($"--charge-chance 0-1 arasında olmalı: {value}");
                    }

                    // Eksen düz kalsın diye Saldırganlık eğrisini bastırır: iki uç da aynı
                    // değere çekilince olasılık savaşçıdan bağımsız sabitlenir.
                    tuning = tuning with
                    {
                        ChargeChanceAtZeroAggression = chargeChance,
                        ChargeChanceAtMaxAggression = chargeChance,
                    };
                    break;

                case "--speed":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double speedValue)
                        || speedValue is < 0 or > 100)
                    {
                        return ParsedArgs.Fail($"--speed 0-100 arasında olmalı: {value}");
                    }

                    playerSpeed = speedValue;
                    break;

                case "--charge-chance-min":
                    if (!TryFraction(value, out double chanceMin))
                    {
                        return ParsedArgs.Fail($"--charge-chance-min 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { ChargeChanceAtZeroAggression = chanceMin };
                    break;

                case "--charge-chance-max":
                    if (!TryFraction(value, out double chanceMax))
                    {
                        return ParsedArgs.Fail($"--charge-chance-max 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { ChargeChanceAtMaxAggression = chanceMax };
                    break;

                case "--charge-windup":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double chargeWindup)
                        || chargeWindup < 0)
                    {
                        return ParsedArgs.Fail($"--charge-windup negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { ChargeWindupSeconds = chargeWindup };
                    break;

                case "--charge-speed":
                    if (!TryMultiplier(value, out double chargeSpeed))
                    {
                        return ParsedArgs.Fail($"--charge-speed 1 veya üstü olmalı: {value}");
                    }

                    tuning = tuning with { ChargeSpeedMultiplier = chargeSpeed };
                    break;

                case "--charge-damage":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double chargeDamage)
                        || chargeDamage < 0)
                    {
                        return ParsedArgs.Fail($"--charge-damage negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { ChargeDamageAtFullSpeed = chargeDamage };
                    break;

                case "--armor":
                    if (!TryParseArmor(value, out playerArmor))
                    {
                        return ParsedArgs.Fail(
                            $"Bilinmeyen kuşam: {value} (none | light | medium | heavy)");
                    }

                    armorLabel = playerArmor!.Name;
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
            return ParsedArgs.Fail(
                $"Bilinmeyen kaçış politikası: {policySpec} "
                + "(never | below:<0-1> | losing:<0-1> | at:<saniye>)");
        }

        return ParsedArgs.Ok(new SimOptions(
            scenario, battles, firstSeed, policy, label, csvPath, tuning, playerArmor, armorLabel,
            playerSpeed));
    }

    /// <summary>
    /// Oyuncu tarafına zorlanacak kuşamı çözer.
    /// </summary>
    /// <remarks>
    /// Zırh artık yuva yuva olduğu için "iyi zırh = düşük uzuv kaybı" iddiası ancak
    /// senaryonun geri kalanı sabitken ölçülebilir. Bu seçenek o ölçümün aracıdır.
    /// </remarks>
    private static bool TryParseArmor(string spec, out Armor? armor)
    {
        armor = spec.ToLowerInvariant() switch
        {
            "none" => Armor.None(),
            "light" => Armor.Light(),
            "medium" => Armor.Medium(),
            "heavy" => Armor.Heavy(),
            _ => null,
        };

        return armor is not null;
    }

    /// <summary>Çarpan eksenleri: 1'in altı hücumu cezaya çevirirdi, oradan aşağısı yok.</summary>
    private static bool TryMultiplier(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && value >= 1;

    private static bool TryFraction(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && value is >= 0 and <= 1;

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

        if (spec.StartsWith("at:", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(
                spec["at:".Length..],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double atSecond)
            && atSecond >= 0)
        {
            policy = new RetreatAtSecond(atSecond);
            label = string.Create(CultureInfo.InvariantCulture, $"{atSecond:0.##}. saniyede çek");
            return true;
        }

        if (spec.StartsWith("losing:", StringComparison.OrdinalIgnoreCase)
            && TryFraction(spec["losing:".Length..], out double losingAt))
        {
            policy = new RetreatWhenLosing(losingAt);
            label = string.Create(
                CultureInfo.InvariantCulture,
                $"sayıca gerideyken canı %{losingAt * 100:0.#} altına düşünce çek");
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
        writer.WriteLine("             [--policy never|below:<oran>|losing:<oran>|at:<sn>]");
        writer.WriteLine("             [--out <dosya.csv>]");
        writer.WriteLine("             [--grievous <0-1>] [--sever <0-1>]");
        writer.WriteLine("             [--armor none|light|medium|heavy] [--speed <0-100>]");
        writer.WriteLine("             [--charge-chance <0-1>] [--charge-chance-min/-max <0-1>]");
        writer.WriteLine("             [--charge-speed <>=1>] [--charge-damage <>=0>]");
        writer.WriteLine("             [--charge-windup <sn>]");
        writer.WriteLine();
        writer.WriteLine("Seçenekler:");
        writer.WriteLine($"  --scenario  Koşturulacak eşleşme (varsayılan: {DefaultScenario})");
        writer.WriteLine($"  --battles   Dövüş sayısı (varsayılan: {DefaultBattles})");
        writer.WriteLine($"  --seed      İlk seed; sonrakiler birer artar (varsayılan: {DefaultSeed})");
        writer.WriteLine("  --policy    never | below:<oran> | losing:<oran> | at:<sn>");
        writer.WriteLine("              (varsayılan: never)");
        writer.WriteLine("              below   = canı düşen olunca çek (kaba taban)");
        writer.WriteLine("              losing  = sayıca gerideyken canı düşünce çek (oyuncu modeli)");
        writer.WriteLine("              at      = verilen saniyede, olan bitene bakmadan çek");
        writer.WriteLine("  --out       Dövüş başına satır yazılacak CSV dosyası");
        writer.WriteLine("  --grievous  Ağır darbe eşiği (darbe/azami can oranı)");
        writer.WriteLine("  --sever     Ağır darbede taban uzuv kopma şansı");
        writer.WriteLine("  --armor     Oyuncu tarafının kuşamını ezer (zırh eksenini izole eder)");
        writer.WriteLine("  --speed     Oyuncu tarafının Hız stat'ını ezer (hız eksenini izole eder)");
        writer.WriteLine("  --charge-chance    Hücum olasılığını sabitler (Saldırganlık eğrisini bastırır)");
        writer.WriteLine("  --charge-chance-min/-max  Saldırganlık eğrisinin iki ucu");
        writer.WriteLine("  --charge-windup    Koşu öncesi birikme süresi (0 = birikme yok)");
        writer.WriteLine("  --charge-speed     Hücum sırasındaki hız çarpanı");
        writer.WriteLine("  --charge-damage    Azami hızda varış vuruşuna eklenen hasar oranı");
        writer.WriteLine();
        writer.WriteLine("Senaryolar:");
        foreach (Scenario s in Scenarios.All)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {s.Name,-10} {s.Description}"));
        }
    }
}
