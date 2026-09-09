using System.Globalization;

namespace Domina.Sim;

/// <summary>
/// Writes one CSV line per fight.
/// </summary>
/// <remarks>
/// One line for each fight — not totals. This is what actually helps in balance work: the sentence
/// "12% deaths on average" hides whether the distribution piles up at the two ends or in the middle.
/// The raw lines can be turned into a table or a chart, and thanks to the seed column an interesting
/// fight can be watched again in the engine exactly as it was.
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
    /// Writes one line. Numbers are <b>always</b> formatted with the invariant culture; a comma decimal
    /// separator would break the CSV.
    /// </summary>
    public void WriteRow(BattleRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        writer.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{row.Seed},{row.Outcome},{row.Seconds:F2},{row.PlayerDeaths},{row.PlayerEscapes},{row.PlayerLimbLosses},{row.LostArms},{row.LostLegs},{row.LostEyes},{row.EnemyDeaths},{row.EnemyWeaponsDropped},{row.PlayerAttacks},{row.PlayerHits},{row.PlayerDamageDealt:F1},{row.PlayerDamageTaken:F1},{row.PlayerStunsTaken},{row.PlayerStunsInflicted},{row.PlayerBlocksMade},{row.PlayerArmorWear:F1},{row.PlayerArmorDestroyed},{row.PlayerWeaponsDropped},{row.PlayerDisarmsInflicted},{row.PlayerWeaponsPickedUp},{row.PlayerTimesPoisoned},{row.PlayerPoisonsInflicted},{row.PlayerPoisonDamageTaken:F1},{row.PlayerPoisonDamageDealt:F1},{row.PlayerPoisonDeaths},{row.PlayerChargesStarted},{row.PlayerChargesConnected},{row.PlayerChargeOpportunitiesTaken},{row.PlayerChargesBroken}"));
    }
}

/// <summary>Summarises a batch's result to the console.</summary>
internal static class SummaryReport
{
    public static void Write(TextWriter writer, SimOptions options, BatchReport report, TimeSpan wallClock)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(report);

        ulong lastSeed = options.FirstSeed + (ulong)(options.Battles - 1);
        double perSecond = wallClock.TotalSeconds <= 0 ? 0 : report.Battles / wallClock.TotalSeconds;

        writer.WriteLine($"Scenario        : {options.Scenario.Name} — {options.Scenario.Description}");
        writer.WriteLine($"Fights          : {report.Battles} (seed {options.FirstSeed}..{lastSeed})");
        writer.WriteLine($"Retreat policy  : {options.PolicyLabel}");
        writer.WriteLine($"Kit             : {options.ArmorLabel}");
        writer.WriteLine($"Wall clock      : {wallClock.TotalSeconds:F2} s ({perSecond:F0} fights/s)");
        writer.WriteLine($"Fight duration  : {report.AverageSeconds:F1} s on average (in game)");
        writer.WriteLine();

        writer.WriteLine("Outcome");
        WriteCount(writer, "  Victory", report.Victories, report.VictoryRate);
        WriteCount(writer, "  Withdrawal", report.Withdrawals, report.WithdrawalRate);
        WriteCount(writer, "  Rout", report.Wipes, report.WipeRate);
        WriteCount(writer, "  Time limit", report.TimeLimits, report.TimeLimitRate);
        writer.WriteLine();

        writer.WriteLine($"Player side ({report.PlayerAppearances} warrior-fights)");
        WriteCount(writer, "  Deaths", report.PlayerDeaths, report.PlayerDeathRate);
        WriteCount(writer, "  Escapes", report.PlayerEscapes, report.PlayerEscapeRate);
        WriteCount(writer, "  Limb losses", report.PlayerLimbLosses, report.PlayerLimbLossRate);
        WriteCount(writer, "    · arm", report.LostArms, report.LostArmRate);
        WriteCount(writer, "    · leg", report.LostLegs, report.LostLegRate);
        WriteCount(writer, "    · eye", report.LostEyes, report.LostEyeRate);
        writer.WriteLine($"  Hit rate                  {report.PlayerAccuracy * 100:F1}%");
        writer.WriteLine($"  Damage dealt/taken        {report.PlayerDamageDealt:F0} / {report.PlayerDamageTaken:F0}");
        writer.WriteLine($"  Stuns (per warrior)       taken {report.StunsTakenPerWarrior:F2} / inflicted {report.StunsInflictedPerWarrior:F2}");
        writer.WriteLine($"  Catches (per warrior)     made {report.CatchesPerWarrior:F2} / suffered {report.TimesCaughtPerWarrior:F2}");
        writer.WriteLine($"  Blocks (per warrior)      {report.BlocksPerWarrior:F2} blows met");
        writer.WriteLine($"  Armour wear               {report.ArmorWearPerWarrior:F1} damage (per warrior-fight)");
        writer.WriteLine($"  Armour loss               warrior {report.ArmorLossRate * 100:F2}% / pieces (per warrior) {report.ArmorPiecesLostPerWarrior:F2}");
        writer.WriteLine($"  Weapon drops              own {report.WeaponDropRate * 100:F2}% (picked up {report.PickupRate * 100:F1}%) / inflicted (per warrior) {report.DisarmsPerWarrior:F2}");
        writer.WriteLine($"  Poisonings (per warrior)  taken {report.PoisoningsTakenPerWarrior:F2} / inflicted {report.PoisoningsInflictedPerWarrior:F2}");
        writer.WriteLine($"  Poison damage             dealt {report.PlayerPoisonDamageDealt:F0} ({report.PoisonShareOfDamageDealt * 100:F1}%) / taken {report.PlayerPoisonDamageTaken:F0}");
        writer.WriteLine($"  Poison deaths             {report.PlayerPoisonDeaths} ({report.PoisonDeathShare * 100:F1}% of deaths)");
        writer.WriteLine($"  Charges (per fight)       {report.ChargesPerBattle:F2}  arrival {report.ChargeConnectRate * 100:F1}%  free hits/charge {report.OpportunitiesPerCharge:F2}");
        writer.WriteLine($"    · scattered in windup  {report.ChargeBreakRate * 100:F1}%");
        writer.WriteLine(
            $"    · launch moment        average {report.AverageChargeStart:F2} s, latest {report.LatestChargeStart:F2} s");
        writer.WriteLine();

        writer.WriteLine($"Enemy side ({report.EnemyAppearances} warrior-fights)");
        WriteCount(writer, "  Deaths", report.EnemyDeaths, report.EnemyDeathRate);
        WriteCount(writer, "  Weapon dropped", report.EnemyWeaponsDropped, report.EnemyWeaponDropRate);
    }

    private static void WriteCount(TextWriter writer, string label, int count, double rate) =>
        writer.WriteLine($"{label,-25} {count,8}  {rate * 100:F2}%");
}

/// <summary>Summarises an expedition series' result to the console.</summary>
/// <remarks>
/// It looks for the answer to one question: <b>does the encounter pay for itself.</b> If the net gold
/// per fight is positive the economy stands, if negative the dojo melts day by day. The lines beside it
/// (idle days, hungry days, closed dojos) say which road that number came by.
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

        writer.WriteLine($"Scenario        : {options.Scenario.Name} — {options.Scenario.Description}");
        writer.WriteLine(
            $"Expedition run  : {report.Campaigns} dojos × {options.Days} days, party of {options.PartySize}, roster of {options.RosterTarget}");
        writer.WriteLine($"Starting purse  : {options.StartingGold} gold");
        writer.WriteLine($"Wall clock      : {wallClock.TotalSeconds:F2} s");
        writer.WriteLine();

        writer.WriteLine("Prices");
        writer.WriteLine($"  Victory reward         {options.Economy.VictoryGoldPerEnemyHealth:F2} gold / enemy health");
        writer.WriteLine($"  Armour piece           {options.Economy.ArmorGoldPerDurability:F2} gold / durability");
        writer.WriteLine($"  Repair                 {options.Economy.RepairGoldPerWear:F2} gold / wear");
        writer.WriteLine(
            $"  Daily stock            food {options.Economy.FoodPrice} / water {options.Economy.WaterPrice} / medicine {options.Economy.MedicinePrice}");
        writer.WriteLine($"  Warrior purchase       {options.Economy.RecruitPrice}");
        writer.WriteLine();

        writer.WriteLine("Treasury (per fight)");
        writer.WriteLine($"  Income                 {report.GoldEarnedPerBattle:F1}");
        writer.WriteLine($"  Kit spending           {report.GearGoldPerBattle:F1}");
        writer.WriteLine($"  Replacements           {report.HireGoldPerBattle:F1}");
        writer.WriteLine($"  Net                    {report.NetGoldPerBattle:F1}");
        writer.WriteLine($"  Daily consumption      {report.UpkeepGoldPerDay:F1} gold/day");
        writer.WriteLine($"  Ending purse           {report.AverageEndingGold:F0} gold");
        writer.WriteLine($"  Kept their capital     {report.SolventRate(options.StartingGold) * 100:F1}%");
        writer.WriteLine();

        writer.WriteLine("Calendar");
        writer.WriteLine($"  Fights (per dojo)      {report.AverageBattles:F1}  victory {report.VictoryRate * 100:F1}%");
        writer.WriteLine($"  Idle days              {report.IdleDayShare * 100:F1}% (the roster was not enough for an expedition)");
        writer.WriteLine($"  Declined offers        {report.DeclinedShare * 100:F1}%");
        writer.WriteLine(
            $"  Mishaps                {report.MishapDayShare * 100:F1}% of days, {report.MishapGoldPerDay:F1} gold/day");
        writer.WriteLine($"  Hungry days            {report.HungryDayShare * 100:F1}%");
        writer.WriteLine($"  Infirmary days / fight {report.RecoveryDaysPerBattle:F2}");
        writer.WriteLine($"  Enemy health / fight   {report.EnemyHealthPerBattle:F0}");
        writer.WriteLine($"  Deaths / warrior-fight {report.DeathPerWarriorBattle * 100:F1}%");
        writer.WriteLine();

        writer.WriteLine("Roster");
        writer.WriteLine($"  Deaths (per dojo)      {report.AverageDeaths:F2}");
        writer.WriteLine($"  Warriors hired         {report.AverageHires:F2}");
        writer.WriteLine(
            $"  Bounty hunts           {report.AverageBounties:F2}"
            + $"  heads taken {report.BountyClaimRate * 100:F1}%");
        writer.WriteLine($"  Armour pieces broken   {report.AverageArmorPiecesLost:F2}");
        writer.WriteLine(
            $"  Training days          {report.AverageTrainingDays:F1}"
            + $"  best warrior {report.AverageBestScore:F0} score (+{report.AverageScoreGain:F0})");
        writer.WriteLine(
            $"  School facilities      {report.AverageSchoolNodes:F1}"
            + $"  {report.AverageSchoolGold:F0} gold, chose a path {report.AveragePaths:F2}");
        writer.WriteLine($"  Dojos closed           {report.CollapseRate * 100:F1}%");
        writer.WriteLine(
            $"  Days survived          average {report.AverageDaysSurvived:F0}, median {report.MedianDaysSurvived}");
    }
}
