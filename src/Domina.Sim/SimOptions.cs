using System.Globalization;
using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
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
    double? PlayerSpeed = null,
    CampaignOptions? Campaign = null);

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
        bool campaign = false;
        int days = CampaignOptions.DefaultDays;
        int campaigns = CampaignOptions.DefaultCampaigns;
        int partySize = CampaignOptions.DefaultPartySize;
        int rosterTarget = CampaignOptions.DefaultRosterTarget;
        int startingGold = CampaignOptions.DefaultStartingGold;
        double repairAt = CampaignOptions.DefaultRepairAtWearShare;
        int reserveDays = CampaignOptions.DefaultReserveDays;
        bool useOffers = false;
        ThreatBand acceptUpTo = ThreatBand.Dire;
        bool cautiousWhenThin = false;
        EncounterTuning encounters = new();
        EventTuning events = new();
        bool useMarket = false;
        MarketPick marketPick = MarketPick.Value;
        EconomyTuning economy = new();

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

                case "--charge-counter":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double chargeCounter)
                        || chargeCounter is < 0 or > 1)
                    {
                        return ParsedArgs.Fail($"--charge-counter 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { ChargeTargetCounterChance = chargeCounter };
                    break;

                case "--armor-attack-penalty":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double armorAttack)
                        || armorAttack < 0)
                    {
                        return ParsedArgs.Fail($"--armor-attack-penalty negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { ArmorAttackSlowdownAtFullWeight = armorAttack };
                    break;

                case "--stun-chance":
                    if (!TryFraction(value, out double stunChance))
                    {
                        return ParsedArgs.Fail($"--stun-chance 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { BaseStunChance = stunChance };
                    break;

                case "--stun-threshold":
                    if (!TryFraction(value, out double stunThreshold))
                    {
                        return ParsedArgs.Fail($"--stun-threshold 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { StunSeverityThreshold = stunThreshold };
                    break;

                case "--stun-seconds":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double stunSeconds)
                        || stunSeconds < 0)
                    {
                        return ParsedArgs.Fail($"--stun-seconds negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { StunSeconds = stunSeconds };
                    break;

                case "--stun-head":
                    if (!TryMultiplier(value, out double stunHead))
                    {
                        return ParsedArgs.Fail($"--stun-head 1 veya üstü olmalı: {value}");
                    }

                    tuning = tuning with { StunHeadMultiplier = stunHead };
                    break;

                case "--stun-armor-share":
                    if (!TryFraction(value, out double stunArmorShare))
                    {
                        return ParsedArgs.Fail($"--stun-armor-share 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { ArmorStunResistanceShare = stunArmorShare };
                    break;

                case "--catch-chance":
                    if (!TryFraction(value, out double catchChance))
                    {
                        return ParsedArgs.Fail($"--catch-chance 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { BaseCatchChance = catchChance };
                    break;

                case "--catch-bind":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double catchBind)
                        || catchBind < 0)
                    {
                        return ParsedArgs.Fail($"--catch-bind negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { CatchBindSeconds = catchBind };
                    break;

                case "--catch-two-handed":
                    if (!TryFraction(value, out double catchTwoHanded))
                    {
                        return ParsedArgs.Fail($"--catch-two-handed 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { CatchTwoHandedFactor = catchTwoHanded };
                    break;

                case "--catch-stamina":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double catchStamina)
                        || catchStamina < 0)
                    {
                        return ParsedArgs.Fail($"--catch-stamina negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { CatchStaminaCost = catchStamina };
                    break;

                case "--catch-accuracy":
                    if (!TryFraction(value, out double catchAccuracy))
                    {
                        return ParsedArgs.Fail($"--catch-accuracy 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { CatchAccuracyBonusAtMax = catchAccuracy };
                    break;

                case "--poison-damage":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double poisonDamage)
                        || poisonDamage < 0)
                    {
                        return ParsedArgs.Fail($"--poison-damage negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { PoisonDamagePerTick = poisonDamage };
                    break;

                case "--poison-seconds":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double poisonSeconds)
                        || poisonSeconds < 0)
                    {
                        return ParsedArgs.Fail($"--poison-seconds negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { PoisonSeconds = poisonSeconds };
                    break;

                case "--poison-tick":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double poisonTick)
                        || poisonTick <= 0)
                    {
                        return ParsedArgs.Fail($"--poison-tick pozitif bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { PoisonTickSeconds = poisonTick };
                    break;

                case "--poison-dose":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double poisonDose)
                        || poisonDose < 0)
                    {
                        return ParsedArgs.Fail($"--poison-dose negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { PoisonMaxDose = poisonDose };
                    break;

                case "--armor-durability":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double durability)
                        || durability < 0)
                    {
                        return ParsedArgs.Fail($"--armor-durability negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { ArmorDurabilityScale = durability };
                    break;

                case "--block-chance":
                    if (!TryFraction(value, out double blockChance))
                    {
                        return ParsedArgs.Fail($"--block-chance 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { MaxBlockChance = blockChance };
                    break;

                case "--block-seconds":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double blockSeconds)
                        || blockSeconds < 0)
                    {
                        return ParsedArgs.Fail($"--block-seconds negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { BlockSeconds = blockSeconds };
                    break;

                case "--block-reduction":
                    if (!TryFraction(value, out double blockReduction))
                    {
                        return ParsedArgs.Fail($"--block-reduction 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { BlockDamageReduction = blockReduction };
                    break;

                case "--target-wounded":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double targetWounded)
                        || targetWounded < 0)
                    {
                        return ParsedArgs.Fail($"--target-wounded negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { TargetWoundedWeight = targetWounded };
                    break;

                case "--target-exposed":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double targetExposed)
                        || targetExposed < 0)
                    {
                        return ParsedArgs.Fail($"--target-exposed negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { TargetExposedWeight = targetExposed };
                    break;

                case "--target-crowd":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double targetCrowd)
                        || targetCrowd < 0)
                    {
                        return ParsedArgs.Fail($"--target-crowd negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { TargetCrowdPenalty = targetCrowd };
                    break;

                case "--target-sticky":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double targetSticky)
                        || targetSticky < 0)
                    {
                        return ParsedArgs.Fail($"--target-sticky negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { TargetStickiness = targetSticky };
                    break;

                case "--disarm-chance":
                    if (!TryFraction(value, out double disarmChance))
                    {
                        return ParsedArgs.Fail($"--disarm-chance 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { BaseDisarmChance = disarmChance };
                    break;

                case "--disarm-catch":
                    if (!TryFraction(value, out double disarmCatch))
                    {
                        return ParsedArgs.Fail($"--disarm-catch 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { CatchDisarmChance = disarmCatch };
                    break;

                case "--disarm-armor-share":
                    if (!TryFraction(value, out double disarmArmorShare))
                    {
                        return ParsedArgs.Fail($"--disarm-armor-share 0-1 arasında olmalı: {value}");
                    }

                    tuning = tuning with { ArmorHardnessShare = disarmArmorShare };
                    break;

                case "--drop-distance":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double dropDistance)
                        || dropDistance < 0)
                    {
                        return ParsedArgs.Fail($"--drop-distance negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { WeaponDropDistance = dropDistance };
                    break;

                case "--pickup-radius":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pickupRadius)
                        || pickupRadius < 0)
                    {
                        return ParsedArgs.Fail($"--pickup-radius negatif olmayan bir sayı olmalı: {value}");
                    }

                    tuning = tuning with { WeaponPickupRadius = pickupRadius };
                    break;

                case "--armor":
                    if (!TryParseArmor(value, out playerArmor))
                    {
                        return ParsedArgs.Fail(
                            $"Bilinmeyen kuşam: {value} (none | light | medium | heavy)");
                    }

                    armorLabel = playerArmor!.Name;
                    break;

                case "--mode":
                    if (!string.Equals(value, "battle", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "campaign", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--mode battle veya campaign olmali: {value}");
                    }

                    campaign = string.Equals(value, "campaign", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--days":
                    if (!TryCount(value, out days))
                    {
                        return ParsedArgs.Fail($"--days pozitif bir tam sayi olmali: {value}");
                    }

                    break;

                case "--campaigns":
                    if (!TryCount(value, out campaigns))
                    {
                        return ParsedArgs.Fail($"--campaigns pozitif bir tam sayi olmali: {value}");
                    }

                    break;

                case "--party":
                    if (!TryCount(value, out partySize) || partySize > 4)
                    {
                        return ParsedArgs.Fail($"--party 1-4 arasinda olmali: {value}");
                    }

                    break;

                case "--roster":
                    if (!TryCount(value, out rosterTarget))
                    {
                        return ParsedArgs.Fail($"--roster pozitif bir tam sayi olmali: {value}");
                    }

                    break;

                case "--gold":
                    if (!TryAmount(value, out startingGold))
                    {
                        return ParsedArgs.Fail($"--gold negatif olmayan bir tam sayi olmali: {value}");
                    }

                    break;

                case "--repair-at":
                    if (!TryFraction(value, out repairAt))
                    {
                        return ParsedArgs.Fail($"--repair-at 0-1 arasinda olmali: {value}");
                    }

                    break;

                case "--reserve-days":
                    if (!TryAmount(value, out reserveDays))
                    {
                        return ParsedArgs.Fail($"--reserve-days negatif olmayan bir tam sayi olmali: {value}");
                    }

                    break;

                case "--offers":
                    if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--offers on veya off olmali: {value}");
                    }

                    useOffers = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--accept-up-to":
                    if (!Enum.TryParse(value, ignoreCase: true, out acceptUpTo))
                    {
                        return ParsedArgs.Fail(
                            $"--accept-up-to faint | rising | heavy | dire olmali: {value}");
                    }

                    break;

                case "--cautious":
                    if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--cautious on veya off olmali: {value}");
                    }

                    cautiousWhenThin = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--market":
                    if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--market on veya off olmali: {value}");
                    }

                    useMarket = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--market-pick":
                    if (!Enum.TryParse(value, ignoreCase: true, out marketPick))
                    {
                        return ParsedArgs.Fail($"--market-pick value veya best olmali: {value}");
                    }

                    break;

                case "--event-chance":
                    if (!TryFraction(value, out double eventChance))
                    {
                        return ParsedArgs.Fail($"--event-chance 0-1 arasinda olmali: {value}");
                    }

                    events = events with { ChancePerDay = eventChance };
                    break;

                case "--power-start":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double powerStart)
                        || powerStart <= 0)
                    {
                        return ParsedArgs.Fail($"--power-start pozitif bir sayi olmali: {value}");
                    }

                    encounters = encounters with { StartingPower = powerStart };
                    break;

                case "--power-per-day":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double perDay)
                        || perDay < 0)
                    {
                        return ParsedArgs.Fail($"--power-per-day negatif olmayan bir sayi olmali: {value}");
                    }

                    encounters = encounters with { PowerPerDay = perDay };
                    break;

                case "--power-variance":
                    if (!TryFraction(value, out double variance))
                    {
                        return ParsedArgs.Fail($"--power-variance 0-1 arasinda olmali: {value}");
                    }

                    encounters = encounters with { DailyVariance = variance };
                    break;

                case "--power-max":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double powerMax)
                        || powerMax <= 0)
                    {
                        return ParsedArgs.Fail($"--power-max pozitif bir sayi olmali: {value}");
                    }

                    encounters = encounters with { MaxPower = powerMax };
                    break;

                case "--duel-chance":
                    if (!TryFraction(value, out double duelChance))
                    {
                        return ParsedArgs.Fail($"--duel-chance 0-1 arasinda olmali: {value}");
                    }

                    encounters = encounters with { DuelChance = duelChance };
                    break;

                case "--reward":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double reward)
                        || reward < 0)
                    {
                        return ParsedArgs.Fail($"--reward negatif olmayan bir sayi olmali: {value}");
                    }

                    economy = economy with { VictoryGoldPerEnemyHealth = reward };
                    break;

                case "--armor-gold":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double armorGold)
                        || armorGold < 0)
                    {
                        return ParsedArgs.Fail($"--armor-gold negatif olmayan bir sayi olmali: {value}");
                    }

                    economy = economy with { ArmorGoldPerDurability = armorGold };
                    break;

                case "--repair-gold":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double repairGold)
                        || repairGold < 0)
                    {
                        return ParsedArgs.Fail($"--repair-gold negatif olmayan bir sayi olmali: {value}");
                    }

                    economy = economy with { RepairGoldPerWear = repairGold };
                    break;

                case "--food-price":
                    if (!TryAmount(value, out int foodPrice))
                    {
                        return ParsedArgs.Fail($"--food-price negatif olmayan bir tam sayi olmali: {value}");
                    }

                    economy = economy with { FoodPrice = foodPrice };
                    break;

                case "--water-price":
                    if (!TryAmount(value, out int waterPrice))
                    {
                        return ParsedArgs.Fail($"--water-price negatif olmayan bir tam sayi olmali: {value}");
                    }

                    economy = economy with { WaterPrice = waterPrice };
                    break;

                case "--medicine-price":
                    if (!TryAmount(value, out int medicinePrice))
                    {
                        return ParsedArgs.Fail($"--medicine-price negatif olmayan bir tam sayi olmali: {value}");
                    }

                    economy = economy with { MedicinePrice = medicinePrice };
                    break;

                case "--recruit-price":
                    if (!TryAmount(value, out int recruitPrice))
                    {
                        return ParsedArgs.Fail($"--recruit-price negatif olmayan bir tam sayi olmali: {value}");
                    }

                    economy = economy with { RecruitPrice = recruitPrice };
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

        CampaignOptions? campaignOptions = campaign
            ? new CampaignOptions(
                scenario,
                days,
                campaigns,
                partySize,
                rosterTarget,
                startingGold,
                repairAt,
                reserveDays,
                economy,
                new DojoTuning(),
                tuning,
                policy,
                useOffers,
                encounters,
                acceptUpTo,
                cautiousWhenThin,
                events,
                useMarket,
                null,
                marketPick)
            : null;

        return ParsedArgs.Ok(new SimOptions(
            scenario, battles, firstSeed, policy, label, csvPath, tuning, playerArmor, armorLabel,
            playerSpeed, campaignOptions));
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

    private static bool TryCount(string value, out int count) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) && count > 0;

    private static bool TryAmount(string value, out int amount) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount) && amount >= 0;

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
        writer.WriteLine("             [--charge-windup <sn>] [--charge-counter <0-1>]");
        writer.WriteLine("             [--armor-attack-penalty <>=0>]");
        writer.WriteLine("             [--stun-chance <0-1>] [--stun-threshold <0-1>]");
        writer.WriteLine("             [--stun-seconds <sn>] [--stun-head <>=1>]");
        writer.WriteLine("             [--stun-armor-share <0-1>]");
        writer.WriteLine("             [--catch-chance <0-1>] [--catch-bind <sn>]");
        writer.WriteLine("             [--catch-two-handed <0-1>] [--catch-stamina <sayı>]");
        writer.WriteLine("             [--catch-accuracy <0-1>]");
        writer.WriteLine("             [--poison-damage <sayı>] [--poison-seconds <sn>]");
        writer.WriteLine("             [--poison-tick <sn>] [--poison-dose <sayı>]");
        writer.WriteLine("             [--armor-durability <çarpan>]");
        writer.WriteLine("             [--target-wounded <puan>] [--target-exposed <puan>]");
        writer.WriteLine("             [--target-crowd <puan>] [--target-sticky <puan>]");
        writer.WriteLine("             [--block-chance <0-1>] [--block-seconds <sn>]");
        writer.WriteLine("             [--block-reduction <0-1>]");
        writer.WriteLine("             [--disarm-chance <0-1>] [--disarm-catch <0-1>]");
        writer.WriteLine("             [--disarm-armor-share <0-1>] [--drop-distance <birim>]");
        writer.WriteLine("             [--pickup-radius <birim>]");
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
        writer.WriteLine("  --charge-counter   Hücumun hedefinin karşı vuruş olasılığı");
        writer.WriteLine("  --armor-speed-penalty    Tam kuşamda yürüme hızından düşen oran");
        writer.WriteLine("  --armor-attack-penalty   Tam kuşamda saldırı döngüsünün uzama oranı");
        writer.WriteLine("  --stun-chance      Ağır darbede taban sersemletme şansı");
        writer.WriteLine("  --stun-threshold   Sersemletme zarının atıldığı darbe/azami can oranı");
        writer.WriteLine("  --stun-seconds     Sersemleyen savaşçının donduğu süre");
        writer.WriteLine("  --stun-head        Kafaya inen darbenin sersemletme çarpanı");
        writer.WriteLine("  --stun-armor-share Zırhın kopma direncinin sersemletmeye sayılan payı");
        writer.WriteLine("  --catch-chance     Yakalama aletiyle gelen vuruşu tutma taban şansı");
        writer.WriteLine("  --catch-bind       Silahı yakalanan saldıranın açıkta kaldığı süre");
        writer.WriteLine("  --catch-two-handed Çift el silahın yakalanma şansına uygulanan çarpan");
        writer.WriteLine("  --catch-stamina    Yakalamanın stamina bedeli");
        writer.WriteLine("  --catch-accuracy   İsabet 100 iken yakalama şansına eklenen oran");
        writer.WriteLine("  --poison-damage    Zehrin bir tikte verdiği hasar (doz 1 iken)");
        writer.WriteLine("  --poison-seconds   Bir dozun ömrü");
        writer.WriteLine("  --poison-tick      Zehrin hasar verme aralığı");
        writer.WriteLine("  --poison-dose      Bir savaşçıda birikebilecek azami doz");
        writer.WriteLine("  --armor-durability Zırh dayanıklılık havuzlarının çarpanı (0 = yıpranmaz)");
        writer.WriteLine("  --disarm-chance    Zırha inen vuruşta silahın elden düşme taban şansı");
        writer.WriteLine("  --disarm-catch     Yakalanan silahın avuçtan çıkma şansı");
        writer.WriteLine("  --disarm-armor-share Vurulan parçanın kopma direncinin sertlik payı");
        writer.WriteLine("  --drop-distance    Düşen silahın savaşçıdan uzağa savrulma mesafesi");
        writer.WriteLine("  --pickup-radius    Yerdeki silahın alınabildiği mesafe");
        writer.WriteLine();
        writer.WriteLine("Senaryolar:");
        foreach (Scenario s in Scenarios.All)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {s.Name,-10} {s.Description}"));
        }
    }
}
