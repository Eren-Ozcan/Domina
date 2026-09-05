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
        "lost_arms",
        "lost_legs",
        "lost_eyes",
        "enemy_deaths",
        "enemy_weapons_dropped",
        "player_attacks",
        "player_hits",
        "player_damage_dealt",
        "player_damage_taken",
        "stuns_taken",
        "stuns_inflicted",
        "blocks_made",
        "armor_wear",
        "armor_destroyed",
        "weapon_dropped",
        "disarms_inflicted",
        "weapons_picked_up",
        "times_poisoned",
        "poisonings_inflicted",
        "poison_damage_taken",
        "poison_damage_dealt",
        "poison_deaths",
        "charges_started",
        "charges_connected",
        "charge_opportunities",
        "charges_broken");

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
            $"{row.Seed},{row.Outcome},{row.Seconds:F2},{row.PlayerDeaths},{row.PlayerEscapes},{row.PlayerLimbLosses},{row.LostArms},{row.LostLegs},{row.LostEyes},{row.EnemyDeaths},{row.EnemyWeaponsDropped},{row.PlayerAttacks},{row.PlayerHits},{row.PlayerDamageDealt:F1},{row.PlayerDamageTaken:F1},{row.PlayerStunsTaken},{row.PlayerStunsInflicted},{row.PlayerBlocksMade},{row.PlayerArmorWear:F1},{row.PlayerArmorDestroyed},{row.PlayerWeaponsDropped},{row.PlayerDisarmsInflicted},{row.PlayerWeaponsPickedUp},{row.PlayerTimesPoisoned},{row.PlayerPoisonsInflicted},{row.PlayerPoisonDamageTaken:F1},{row.PlayerPoisonDamageDealt:F1},{row.PlayerPoisonDeaths},{row.PlayerChargesStarted},{row.PlayerChargesConnected},{row.PlayerChargeOpportunitiesTaken},{row.PlayerChargesBroken}"));
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
        writer.WriteLine($"Kuşam           : {options.ArmorLabel}");
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
        WriteCount(writer, "    · kol", report.LostArms, report.LostArmRate);
        WriteCount(writer, "    · bacak", report.LostLegs, report.LostLegRate);
        WriteCount(writer, "    · göz", report.LostEyes, report.LostEyeRate);
        writer.WriteLine($"  İsabet oranı              %{report.PlayerAccuracy * 100:F1}");
        writer.WriteLine($"  Hasar verilen/alınan      {report.PlayerDamageDealt:F0} / {report.PlayerDamageTaken:F0}");
        writer.WriteLine($"  Sersemleme (savaşçı başına) yenen {report.StunsTakenPerWarrior:F2} / geçirilen {report.StunsInflictedPerWarrior:F2}");
        writer.WriteLine($"  Yakalama (savaşçı başına)  yapılan {report.CatchesPerWarrior:F2} / yenen {report.TimesCaughtPerWarrior:F2}");
        writer.WriteLine($"  Blok (savaşçı başına)      {report.BlocksPerWarrior:F2} karşılanan darbe");
        writer.WriteLine($"  Zırh yıpranması           {report.ArmorWearPerWarrior:F1} hasar (savaşçı-dövüş başına)");
        writer.WriteLine($"  Zırh kaybı                savaşçı %{report.ArmorLossRate * 100:F2} / parça (savaşçı başına) {report.ArmorPiecesLostPerWarrior:F2}");
        writer.WriteLine($"  Silah düşürme             kendi %{report.WeaponDropRate * 100:F2} (yerden alınan %{report.PickupRate * 100:F1}) / düşürdüğü (savaşçı başına) {report.DisarmsPerWarrior:F2}");
        writer.WriteLine($"  Zehirlenme (savaşçı başına) yenen {report.PoisoningsTakenPerWarrior:F2} / geçirilen {report.PoisoningsInflictedPerWarrior:F2}");
        writer.WriteLine($"  Zehir hasarı              verilen {report.PlayerPoisonDamageDealt:F0} (%{report.PoisonShareOfDamageDealt * 100:F1}) / alınan {report.PlayerPoisonDamageTaken:F0}");
        writer.WriteLine($"  Zehirden ölüm             {report.PlayerPoisonDeaths} (ölümlerin %{report.PoisonDeathShare * 100:F1}'i)");
        writer.WriteLine($"  Hücum (dövüş başına)      {report.ChargesPerBattle:F2}  varış %{report.ChargeConnectRate * 100:F1}  bedava vuruş/hücum {report.OpportunitiesPerCharge:F2}");
        writer.WriteLine($"    · birikmede dağılan     %{report.ChargeBreakRate * 100:F1}");
        writer.WriteLine(
            $"    · kalkış anı            ortalama {report.AverageChargeStart:F2} sn, en geç {report.LatestChargeStart:F2} sn");
        writer.WriteLine();

        writer.WriteLine($"Düşman tarafı ({report.EnemyAppearances} savaşçı-dövüş)");
        WriteCount(writer, "  Ölüm", report.EnemyDeaths, report.EnemyDeathRate);
        WriteCount(writer, "  Silahı düştü", report.EnemyWeaponsDropped, report.EnemyWeaponDropRate);
    }

    private static void WriteCount(TextWriter writer, string label, int count, double rate) =>
        writer.WriteLine($"{label,-25} {count,8}  %{rate * 100:F2}");
}

/// <summary>Sefer dizisinin sonucunu konsola özetler.</summary>
/// <remarks>
/// Tek soruya cevap arar: <b>karşılaşma kendi bedelini ödüyor mu.</b> Dövüş başına net
/// altın pozitifse ekonomi ayakta, negatifse dojo günden güne eriyor. Yanındaki satırlar
/// (boş gün, aç gün, kapanan dojo) bu sayının hangi yoldan çıktığını söyler.
/// </remarks>
internal static class CampaignSummaryReport
{
    public static void Write(
        TextWriter writer,
        CampaignOptions options,
        CampaignReport report,
        TimeSpan wallClock)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(report);

        writer.WriteLine($"Senaryo         : {options.Scenario.Name} — {options.Scenario.Description}");
        writer.WriteLine(
            $"Sefer dizisi    : {report.Campaigns} dojo × {options.Days} gün, {options.PartySize} kişilik ekip, {options.RosterTarget} kişilik kadro");
        writer.WriteLine($"Başlangıç kasası: {options.StartingGold} altın");
        writer.WriteLine($"Koşma süresi    : {wallClock.TotalSeconds:F2} sn");
        writer.WriteLine();

        writer.WriteLine("Fiyatlar");
        writer.WriteLine($"  Zafer ödülü            {options.Economy.VictoryGoldPerEnemyHealth:F2} altın / düşman canı");
        writer.WriteLine($"  Zırh parçası           {options.Economy.ArmorGoldPerDurability:F2} altın / dayanıklılık");
        writer.WriteLine($"  Onarım                 {options.Economy.RepairGoldPerWear:F2} altın / yıpranma");
        writer.WriteLine(
            $"  Günlük stok            yiyecek {options.Economy.FoodPrice} / su {options.Economy.WaterPrice} / ilaç {options.Economy.MedicinePrice}");
        writer.WriteLine($"  Savaşçı alımı          {options.Economy.RecruitPrice}");
        writer.WriteLine();

        writer.WriteLine("Kasa (dövüş başına)");
        writer.WriteLine($"  Gelir                  {report.GoldEarnedPerBattle:F1}");
        writer.WriteLine($"  Kuşam gideri           {report.GearGoldPerBattle:F1}");
        writer.WriteLine($"  Net                    {report.NetGoldPerBattle:F1}");
        writer.WriteLine($"  Günlük tüketim         {report.UpkeepGoldPerDay:F1} altın/gün");
        writer.WriteLine($"  Bitiş kasası           {report.AverageEndingGold:F0} altın");
        writer.WriteLine($"  Sermayesini koruyan    %{report.SolventRate(options.StartingGold) * 100:F1}");
        writer.WriteLine();

        writer.WriteLine("Takvim");
        writer.WriteLine($"  Dövüş (dojo başına)    {report.AverageBattles:F1}  zafer %{report.VictoryRate * 100:F1}");
        writer.WriteLine($"  Boş gün                %{report.IdleDayShare * 100:F1} (kadro sefere yetmedi)");
        writer.WriteLine($"  Geri çevrilen teklif   %{report.DeclinedShare * 100:F1}");
        writer.WriteLine(
            $"  Aksilik                %{report.MishapDayShare * 100:F1} gün, {report.MishapGoldPerDay:F1} altın/gün");
        writer.WriteLine($"  Aç gün                 %{report.HungryDayShare * 100:F1}");
        writer.WriteLine($"  Revir günü / dövüş     {report.RecoveryDaysPerBattle:F2}");
        writer.WriteLine($"  Düşman canı / dövüş    {report.EnemyHealthPerBattle:F0}");
        writer.WriteLine($"  Ölüm / savaşçı-dövüş   %{report.DeathPerWarriorBattle * 100:F1}");
        writer.WriteLine();

        writer.WriteLine("Kadro");
        writer.WriteLine($"  Ölüm (dojo başına)     {report.AverageDeaths:F2}");
        writer.WriteLine($"  Alınan savaşçı         {report.AverageHires:F2}");
        writer.WriteLine($"  Dağılan zırh parçası   {report.AverageArmorPiecesLost:F2}");
        writer.WriteLine($"  Kapanan dojo           %{report.CollapseRate * 100:F1}");
        writer.WriteLine(
            $"  Ayakta kalınan gün     ortalama {report.AverageDaysSurvived:F0}, ortanca {report.MedianDaysSurvived}");
    }
}
