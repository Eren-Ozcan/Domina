using System.Globalization;

namespace Domina.Sim;

/// <summary>
/// Dövüş başına CSV satırı yazar.
/// </summary>
/// <remarks>
/// Her dövüş için bir satır — toplamlar değil. Denge çalışmasında asıl işe yarayan
/// budur: "ortalama %12 ölüm" cümlesi, dağılımın iki uçta mı ortada mı toplandığını
/// gizler. Ham satırlar tabloya/grafiğe dökülebilir, seed sütunu sayesinde ilginç
/// bir dövüş motorda birebir tekrar izlenebilir.
/// </remarks>
internal sealed class CsvReport(TextWriter writer)
{
    private static readonly string _headerLine = string.Join(
        ',',
        "seed",
        "outcome",
        "seconds",
        "player_deaths",
        "player_escapes",
        "player_limb_losses",
        "enemy_deaths",
        "player_attacks",
        "player_hits",
        "player_damage_dealt",
        "player_damage_taken");

    public void WriteHeader() => writer.WriteLine(_headerLine);

    /// <summary>
    /// Bir satır yazar. Sayılar <b>daima</b> invariant kültürle biçimlenir; virgüllü
    /// ondalık ayırıcı CSV'yi bozardı.
    /// </summary>
    public void WriteRow(BattleRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        writer.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{row.Seed},{row.Outcome},{row.Seconds:F2},{row.PlayerDeaths},{row.PlayerEscapes},{row.PlayerLimbLosses},{row.EnemyDeaths},{row.PlayerAttacks},{row.PlayerHits},{row.PlayerDamageDealt:F1},{row.PlayerDamageTaken:F1}"));
    }
}

/// <summary>Parti sonucunu konsola özetler.</summary>
internal static class SummaryReport
{
    public static void Write(TextWriter writer, SimOptions options, BatchReport report, TimeSpan wallClock)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(report);

        ulong lastSeed = options.FirstSeed + (ulong)(options.Battles - 1);
        double perSecond = wallClock.TotalSeconds <= 0 ? 0 : report.Battles / wallClock.TotalSeconds;

        writer.WriteLine($"Senaryo         : {options.Scenario.Name} — {options.Scenario.Description}");
        writer.WriteLine($"Dövüş           : {report.Battles} (seed {options.FirstSeed}..{lastSeed})");
        writer.WriteLine($"Kaçış politikası: {options.PolicyLabel}");
        writer.WriteLine($"Koşma süresi    : {wallClock.TotalSeconds:F2} sn ({perSecond:F0} dövüş/sn)");
        writer.WriteLine($"Dövüş süresi    : ortalama {report.AverageSeconds:F1} sn (oyun içi)");
        writer.WriteLine();

        writer.WriteLine("Sonuç");
        WriteCount(writer, "  Zafer", report.Victories, report.VictoryRate);
        WriteCount(writer, "  Çekilme", report.Withdrawals, report.WithdrawalRate);
        WriteCount(writer, "  Bozgun", report.Wipes, report.WipeRate);
        WriteCount(writer, "  Süre doldu", report.TimeLimits, report.TimeLimitRate);
        writer.WriteLine();

        writer.WriteLine($"Oyuncu tarafı ({report.PlayerAppearances} savaşçı-dövüş)");
        WriteCount(writer, "  Ölüm", report.PlayerDeaths, report.PlayerDeathRate);
        WriteCount(writer, "  Kaçış", report.PlayerEscapes, report.PlayerEscapeRate);
        WriteCount(writer, "  Uzuv kaybı", report.PlayerLimbLosses, report.PlayerLimbLossRate);
        writer.WriteLine($"  İsabet oranı              %{report.PlayerAccuracy * 100:F1}");
        writer.WriteLine($"  Hasar verilen/alınan      {report.PlayerDamageDealt:F0} / {report.PlayerDamageTaken:F0}");
        writer.WriteLine();

        writer.WriteLine($"Düşman tarafı ({report.EnemyAppearances} savaşçı-dövüş)");
        WriteCount(writer, "  Ölüm", report.EnemyDeaths, report.EnemyDeathRate);
    }

    private static void WriteCount(TextWriter writer, string label, int count, double rate) =>
        writer.WriteLine($"{label,-25} {count,8}  %{rate * 100:F2}");
}
