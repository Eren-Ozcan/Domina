using System.Globalization;
using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Sim;

/// <summary>The run settings parsed from the command line.</summary>
/// <param name="PlayerArmor">
/// If given, it overrides the kit in the scenario; if <c>null</c> the scenario's own is used.
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

/// <summary>The parse result: settings, a help request, or an error.</summary>
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
        string armorLabel = "from the scenario";
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
        MarketTuning marketTuning = new();
        bool useBounties = false;
        EconomyTuning economy = new();
        TrainingTuning training = new();
        double? acceptRatio = null;
        bool useSchool = false;
        SchoolBranch? schoolOnly = null;
        bool usePaths = false;

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];

            if (arg is "-h" or "--help")
            {
                return ParsedArgs.Help();
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                return ParsedArgs.Fail($"Unexpected argument: {arg}");
            }

            if (i + 1 >= args.Count)
            {
                return ParsedArgs.Fail($"{arg} expects a value.");
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
                        return ParsedArgs.Fail($"--battles must be a positive integer: {value}");
                    }

                    break;

                case "--seed":
                    if (!ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out firstSeed))
                    {
                        return ParsedArgs.Fail($"--seed must be a non-negative integer: {value}");
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
                        return ParsedArgs.Fail($"--grievous must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { GrievousSeverityThreshold = grievous };
                    break;

                case "--sever":
                    if (!TryFraction(value, out double sever))
                    {
                        return ParsedArgs.Fail($"--sever must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { BaseDismembermentChance = sever };
                    break;

                case "--charge-chance":
                    if (!TryFraction(value, out double chargeChance))
                    {
                        return ParsedArgs.Fail($"--charge-chance must be between 0 and 1: {value}");
                    }

                    // Flattens the Aggression curve so the axis stays flat: pulling both ends to the
                    // same value fixes the probability independently of the warrior.
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
                        return ParsedArgs.Fail($"--speed must be between 0 and 100: {value}");
                    }

                    playerSpeed = speedValue;
                    break;

                case "--charge-chance-min":
                    if (!TryFraction(value, out double chanceMin))
                    {
                        return ParsedArgs.Fail($"--charge-chance-min must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { ChargeChanceAtZeroAggression = chanceMin };
                    break;

                case "--charge-chance-max":
                    if (!TryFraction(value, out double chanceMax))
                    {
                        return ParsedArgs.Fail($"--charge-chance-max must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { ChargeChanceAtMaxAggression = chanceMax };
                    break;

                case "--charge-windup":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double chargeWindup)
                        || chargeWindup < 0)
                    {
                        return ParsedArgs.Fail($"--charge-windup must be a non-negative number: {value}");
                    }

                    tuning = tuning with { ChargeWindupSeconds = chargeWindup };
                    break;

                case "--charge-speed":
                    if (!TryMultiplier(value, out double chargeSpeed))
                    {
                        return ParsedArgs.Fail($"--charge-speed must be 1 or more: {value}");
                    }

                    tuning = tuning with { ChargeSpeedMultiplier = chargeSpeed };
                    break;

                case "--charge-damage":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double chargeDamage)
                        || chargeDamage < 0)
                    {
                        return ParsedArgs.Fail($"--charge-damage must be a non-negative number: {value}");
                    }

                    tuning = tuning with { ChargeDamageAtFullSpeed = chargeDamage };
                    break;

                case "--charge-counter":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double chargeCounter)
                        || chargeCounter is < 0 or > 1)
                    {
                        return ParsedArgs.Fail($"--charge-counter must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { ChargeTargetCounterChance = chargeCounter };
                    break;

                case "--armor-attack-penalty":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double armorAttack)
                        || armorAttack < 0)
                    {
                        return ParsedArgs.Fail($"--armor-attack-penalty must be a non-negative number: {value}");
                    }

                    tuning = tuning with { ArmorAttackSlowdownAtFullWeight = armorAttack };
                    break;

                case "--stun-chance":
                    if (!TryFraction(value, out double stunChance))
                    {
                        return ParsedArgs.Fail($"--stun-chance must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { BaseStunChance = stunChance };
                    break;

                case "--stun-disarm":
                    if (!TryFraction(value, out double stunDisarm))
                    {
                        return ParsedArgs.Fail($"--stun-disarm must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { StunDisarmChance = stunDisarm };
                    break;

                case "--stun-threshold":
                    if (!TryFraction(value, out double stunThreshold))
                    {
                        return ParsedArgs.Fail($"--stun-threshold must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { StunSeverityThreshold = stunThreshold };
                    break;

                case "--stun-seconds":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double stunSeconds)
                        || stunSeconds < 0)
                    {
                        return ParsedArgs.Fail($"--stun-seconds must be a non-negative number: {value}");
                    }

                    tuning = tuning with { StunSeconds = stunSeconds };
                    break;

                case "--stun-head":
                    if (!TryMultiplier(value, out double stunHead))
                    {
                        return ParsedArgs.Fail($"--stun-head must be 1 or more: {value}");
                    }

                    tuning = tuning with { StunHeadMultiplier = stunHead };
                    break;

                case "--stun-armor-share":
                    if (!TryFraction(value, out double stunArmorShare))
                    {
                        return ParsedArgs.Fail($"--stun-armor-share must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { ArmorStunResistanceShare = stunArmorShare };
                    break;

                case "--catch-chance":
                    if (!TryFraction(value, out double catchChance))
                    {
                        return ParsedArgs.Fail($"--catch-chance must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { BaseCatchChance = catchChance };
                    break;

                case "--catch-bind":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double catchBind)
                        || catchBind < 0)
                    {
                        return ParsedArgs.Fail($"--catch-bind must be a non-negative number: {value}");
                    }

                    tuning = tuning with { CatchBindSeconds = catchBind };
                    break;

                case "--catch-two-handed":
                    if (!TryFraction(value, out double catchTwoHanded))
                    {
                        return ParsedArgs.Fail($"--catch-two-handed must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { CatchTwoHandedFactor = catchTwoHanded };
                    break;

                case "--catch-stamina":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double catchStamina)
                        || catchStamina < 0)
                    {
                        return ParsedArgs.Fail($"--catch-stamina must be a non-negative number: {value}");
                    }

                    tuning = tuning with { CatchStaminaCost = catchStamina };
                    break;

                case "--catch-accuracy":
                    if (!TryFraction(value, out double catchAccuracy))
                    {
                        return ParsedArgs.Fail($"--catch-accuracy must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { CatchAccuracyBonusAtMax = catchAccuracy };
                    break;

                case "--poison-damage":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double poisonDamage)
                        || poisonDamage < 0)
                    {
                        return ParsedArgs.Fail($"--poison-damage must be a non-negative number: {value}");
                    }

                    tuning = tuning with { PoisonDamagePerTick = poisonDamage };
                    break;

                case "--poison-seconds":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double poisonSeconds)
                        || poisonSeconds < 0)
                    {
                        return ParsedArgs.Fail($"--poison-seconds must be a non-negative number: {value}");
                    }

                    tuning = tuning with { PoisonSeconds = poisonSeconds };
                    break;

                case "--poison-tick":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double poisonTick)
                        || poisonTick <= 0)
                    {
                        return ParsedArgs.Fail($"--poison-tick must be a positive number: {value}");
                    }

                    tuning = tuning with { PoisonTickSeconds = poisonTick };
                    break;

                case "--poison-dose":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double poisonDose)
                        || poisonDose < 0)
                    {
                        return ParsedArgs.Fail($"--poison-dose must be a non-negative number: {value}");
                    }

                    tuning = tuning with { PoisonMaxDose = poisonDose };
                    break;

                case "--armor-durability":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double durability)
                        || durability < 0)
                    {
                        return ParsedArgs.Fail($"--armor-durability must be a non-negative number: {value}");
                    }

                    tuning = tuning with { ArmorDurabilityScale = durability };
                    break;

                case "--block-chance":
                    if (!TryFraction(value, out double blockChance))
                    {
                        return ParsedArgs.Fail($"--block-chance must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { MaxBlockChance = blockChance };
                    break;

                case "--block-seconds":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double blockSeconds)
                        || blockSeconds < 0)
                    {
                        return ParsedArgs.Fail($"--block-seconds must be a non-negative number: {value}");
                    }

                    tuning = tuning with { BlockSeconds = blockSeconds };
                    break;

                case "--block-reduction":
                    if (!TryFraction(value, out double blockReduction))
                    {
                        return ParsedArgs.Fail($"--block-reduction must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { BlockDamageReduction = blockReduction };
                    break;

                case "--target-wounded":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double targetWounded)
                        || targetWounded < 0)
                    {
                        return ParsedArgs.Fail($"--target-wounded must be a non-negative number: {value}");
                    }

                    tuning = tuning with { TargetWoundedWeight = targetWounded };
                    break;

                case "--target-exposed":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double targetExposed)
                        || targetExposed < 0)
                    {
                        return ParsedArgs.Fail($"--target-exposed must be a non-negative number: {value}");
                    }

                    tuning = tuning with { TargetExposedWeight = targetExposed };
                    break;

                case "--target-crowd":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double targetCrowd)
                        || targetCrowd < 0)
                    {
                        return ParsedArgs.Fail($"--target-crowd must be a non-negative number: {value}");
                    }

                    tuning = tuning with { TargetCrowdPenalty = targetCrowd };
                    break;

                case "--target-sticky":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double targetSticky)
                        || targetSticky < 0)
                    {
                        return ParsedArgs.Fail($"--target-sticky must be a non-negative number: {value}");
                    }

                    tuning = tuning with { TargetStickiness = targetSticky };
                    break;

                case "--disarm-chance":
                    if (!TryFraction(value, out double disarmChance))
                    {
                        return ParsedArgs.Fail($"--disarm-chance must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { BaseDisarmChance = disarmChance };
                    break;

                case "--disarm-catch":
                    if (!TryFraction(value, out double disarmCatch))
                    {
                        return ParsedArgs.Fail($"--disarm-catch must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { CatchDisarmChance = disarmCatch };
                    break;

                case "--disarm-armor-share":
                    if (!TryFraction(value, out double disarmArmorShare))
                    {
                        return ParsedArgs.Fail($"--disarm-armor-share must be between 0 and 1: {value}");
                    }

                    tuning = tuning with { ArmorHardnessShare = disarmArmorShare };
                    break;

                case "--drop-distance":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double dropDistance)
                        || dropDistance < 0)
                    {
                        return ParsedArgs.Fail($"--drop-distance must be a non-negative number: {value}");
                    }

                    tuning = tuning with { WeaponDropDistance = dropDistance };
                    break;

                case "--pickup-radius":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pickupRadius)
                        || pickupRadius < 0)
                    {
                        return ParsedArgs.Fail($"--pickup-radius must be a non-negative number: {value}");
                    }

                    tuning = tuning with { WeaponPickupRadius = pickupRadius };
                    break;

                case "--armor":
                    if (!TryParseArmor(value, out playerArmor))
                    {
                        return ParsedArgs.Fail(
                            $"Unknown kit: {value} (none | light | medium | heavy)");
                    }

                    armorLabel = playerArmor!.Name;
                    break;

                case "--mode":
                    if (!string.Equals(value, "battle", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "campaign", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--mode must be battle or campaign: {value}");
                    }

                    campaign = string.Equals(value, "campaign", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--days":
                    if (!TryCount(value, out days))
                    {
                        return ParsedArgs.Fail($"--days must be a positive integer: {value}");
                    }

                    break;

                case "--campaigns":
                    if (!TryCount(value, out campaigns))
                    {
                        return ParsedArgs.Fail($"--campaigns must be a positive integer: {value}");
                    }

                    break;

                case "--party":
                    if (!TryCount(value, out partySize) || partySize > 4)
                    {
                        return ParsedArgs.Fail($"--party must be between 1 and 4: {value}");
                    }

                    break;

                case "--roster":
                    if (!TryCount(value, out rosterTarget))
                    {
                        return ParsedArgs.Fail($"--roster must be a positive integer: {value}");
                    }

                    break;

                case "--gold":
                    if (!TryAmount(value, out startingGold))
                    {
                        return ParsedArgs.Fail($"--gold must be a non-negative integer: {value}");
                    }

                    break;

                case "--repair-at":
                    if (!TryFraction(value, out repairAt))
                    {
                        return ParsedArgs.Fail($"--repair-at must be between 0 and 1: {value}");
                    }

                    break;

                case "--reserve-days":
                    if (!TryAmount(value, out reserveDays))
                    {
                        return ParsedArgs.Fail($"--reserve-days must be a non-negative integer: {value}");
                    }

                    break;

                case "--offers":
                    if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--offers must be on or off: {value}");
                    }

                    useOffers = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--accept-up-to":
                    if (!Enum.TryParse(value, ignoreCase: true, out acceptUpTo))
                    {
                        return ParsedArgs.Fail(
                            $"--accept-up-to must be faint | rising | heavy | dire: {value}");
                    }

                    break;

                case "--cautious":
                    if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--cautious must be on or off: {value}");
                    }

                    cautiousWhenThin = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--market":
                    if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--market must be on or off: {value}");
                    }

                    useMarket = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--bounty":
                    if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--bounty must be on or off: {value}");
                    }

                    useBounties = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--market-refresh":
                    if (!TryCount(value, out int refreshDays))
                    {
                        return ParsedArgs.Fail(
                            $"--market-refresh must be a positive integer: {value}");
                    }

                    marketTuning = marketTuning with { RefreshDays = refreshDays };
                    break;

                case "--market-candidates":
                    if (!TryCount(value, out int candidates))
                    {
                        return ParsedArgs.Fail(
                            $"--market-candidates must be a positive integer: {value}");
                    }

                    marketTuning = marketTuning with { Candidates = candidates };
                    break;

                case "--market-ceiling":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ceiling)
                        || ceiling < 0)
                    {
                        return ParsedArgs.Fail(
                            $"--market-ceiling must be a non-negative number: {value}");
                    }

                    marketTuning = marketTuning with { BestFollowCeiling = ceiling };
                    break;

                case "--risk-premium":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double premium)
                        || premium < 0)
                    {
                        return ParsedArgs.Fail($"--risk-premium must be a non-negative number: {value}");
                    }

                    economy = economy with { RiskPremium = premium };
                    break;

                case "--risk-free-health":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double riskFree)
                        || riskFree <= 0)
                    {
                        return ParsedArgs.Fail($"--risk-free-health must be a positive number: {value}");
                    }

                    economy = economy with { RiskFreeEnemyHealth = riskFree };
                    break;

                case "--accept-ratio":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ratio)
                        || ratio <= 0)
                    {
                        return ParsedArgs.Fail($"--accept-ratio must be a positive number: {value}");
                    }

                    acceptRatio = ratio;
                    break;

                case "--school":
                    if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--school must be on or off: {value}");
                    }

                    useSchool = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--school-only":
                    if (string.Equals(value, "any", StringComparison.OrdinalIgnoreCase))
                    {
                        schoolOnly = null;
                        break;
                    }

                    if (!Enum.TryParse(value, ignoreCase: true, out SchoolBranch branch))
                    {
                        return ParsedArgs.Fail(
                            $"--school-only must be training | infirmary | steward | any: {value}");
                    }

                    schoolOnly = branch;
                    break;

                case "--paths":
                    if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        return ParsedArgs.Fail($"--paths must be on or off: {value}");
                    }

                    usePaths = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                    break;

                case "--path-days":
                    if (!TryCount(value, out int pathDays))
                    {
                        return ParsedArgs.Fail($"--path-days must be a positive integer: {value}");
                    }

                    training = training with { PathTrainingDays = pathDays };
                    break;

                case "--train-rate":
                    if (!TryFraction(value, out double trainRate))
                    {
                        return ParsedArgs.Fail($"--train-rate must be between 0 and 1: {value}");
                    }

                    training = training with { GapClosedPerDay = trainRate };
                    break;

                case "--fight-rate":
                    if (!TryFraction(value, out double fightRate))
                    {
                        return ParsedArgs.Fail($"--fight-rate must be between 0 and 1: {value}");
                    }

                    training = training with { FightGapClosed = fightRate };
                    break;

                case "--train-ceiling":
                    if (!double.TryParse(
                            value, NumberStyles.Float, CultureInfo.InvariantCulture, out double trainCeiling)
                        || trainCeiling <= 0)
                    {
                        return ParsedArgs.Fail($"--train-ceiling must be a positive number: {value}");
                    }

                    training = training with { SkillCeiling = trainCeiling };
                    break;

                case "--market-pick":
                    if (!Enum.TryParse(value, ignoreCase: true, out marketPick))
                    {
                        return ParsedArgs.Fail($"--market-pick must be value or best: {value}");
                    }

                    break;

                case "--event-chance":
                    if (!TryFraction(value, out double eventChance))
                    {
                        return ParsedArgs.Fail($"--event-chance must be between 0 and 1: {value}");
                    }

                    events = events with { ChancePerDay = eventChance };
                    break;

                case "--power-start":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double powerStart)
                        || powerStart <= 0)
                    {
                        return ParsedArgs.Fail($"--power-start must be a positive number: {value}");
                    }

                    encounters = encounters with { StartingPower = powerStart };
                    break;

                case "--power-per-day":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double perDay)
                        || perDay < 0)
                    {
                        return ParsedArgs.Fail($"--power-per-day must be a non-negative number: {value}");
                    }

                    encounters = encounters with { PowerPerDay = perDay };
                    break;

                case "--power-variance":
                    if (!TryFraction(value, out double variance))
                    {
                        return ParsedArgs.Fail($"--power-variance must be between 0 and 1: {value}");
                    }

                    encounters = encounters with { DailyVariance = variance };
                    break;

                case "--power-max":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double powerMax)
                        || powerMax <= 0)
                    {
                        return ParsedArgs.Fail($"--power-max must be a positive number: {value}");
                    }

                    encounters = encounters with { MaxPower = powerMax };
                    break;

                case "--duel-chance":
                    if (!TryFraction(value, out double duelChance))
                    {
                        return ParsedArgs.Fail($"--duel-chance must be between 0 and 1: {value}");
                    }

                    encounters = encounters with { DuelChance = duelChance };
                    break;

                case "--reward":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double reward)
                        || reward < 0)
                    {
                        return ParsedArgs.Fail($"--reward must be a non-negative number: {value}");
                    }

                    economy = economy with { VictoryGoldPerEnemyHealth = reward };
                    break;

                case "--armor-gold":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double armorGold)
                        || armorGold < 0)
                    {
                        return ParsedArgs.Fail($"--armor-gold must be a non-negative number: {value}");
                    }

                    economy = economy with { ArmorGoldPerDurability = armorGold };
                    break;

                case "--repair-gold":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double repairGold)
                        || repairGold < 0)
                    {
                        return ParsedArgs.Fail($"--repair-gold must be a non-negative number: {value}");
                    }

                    economy = economy with { RepairGoldPerWear = repairGold };
                    break;

                case "--food-price":
                    if (!TryAmount(value, out int foodPrice))
                    {
                        return ParsedArgs.Fail($"--food-price must be a non-negative integer: {value}");
                    }

                    economy = economy with { FoodPrice = foodPrice };
                    break;

                case "--water-price":
                    if (!TryAmount(value, out int waterPrice))
                    {
                        return ParsedArgs.Fail($"--water-price must be a non-negative integer: {value}");
                    }

                    economy = economy with { WaterPrice = waterPrice };
                    break;

                case "--medicine-price":
                    if (!TryAmount(value, out int medicinePrice))
                    {
                        return ParsedArgs.Fail($"--medicine-price must be a non-negative integer: {value}");
                    }

                    economy = economy with { MedicinePrice = medicinePrice };
                    break;

                case "--recruit-price":
                    if (!TryAmount(value, out int recruitPrice))
                    {
                        return ParsedArgs.Fail($"--recruit-price must be a non-negative integer: {value}");
                    }

                    economy = economy with { RecruitPrice = recruitPrice };
                    break;

                default:
                    return ParsedArgs.Fail($"Unknown option: {arg}");
            }
        }

        Scenario? scenario = Scenarios.Find(scenarioName);
        if (scenario is null)
        {
            string known = string.Join(", ", Scenarios.All.Select(s => s.Name));
            return ParsedArgs.Fail($"Unknown scenario: {scenarioName} (known: {known})");
        }

        if (!TryParsePolicy(policySpec, out IRetreatPolicy? policy, out string label))
        {
            return ParsedArgs.Fail(
                $"Unknown retreat policy: {policySpec} "
                + "(never | below:<0-1> | losing:<0-1> | at:<seconds>)");
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
                new DojoTuning { Training = training },
                tuning,
                policy,
                useOffers,
                encounters,
                acceptUpTo,
                cautiousWhenThin,
                events,
                useMarket,
                marketTuning,
                marketPick,
                useBounties,
                acceptRatio,
                useSchool,
                schoolOnly,
                usePaths)
            : null;

        return ParsedArgs.Ok(new SimOptions(
            scenario, battles, firstSeed, policy, label, csvPath, tuning, playerArmor, armorLabel,
            playerSpeed, campaignOptions));
    }

    /// <summary>
    /// Parses the kit to be forced onto the player's side.
    /// </summary>
    /// <remarks>
    /// Because armour is now slot by slot, the claim "good armour = less limb loss" can only be measured
    /// while the rest of the scenario is held fixed. This option is that measurement's tool.
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

    /// <summary>Multiplier axes: below 1 would turn a charge into a punishment, so there is nothing below that.</summary>
    private static bool TryMultiplier(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && value >= 1;

    private static bool TryFraction(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && value is >= 0 and <= 1;

    /// <summary>
    /// Parses the retreat policy.
    /// </summary>
    /// <remarks>
    /// In the game the retreat decision comes from the player's key; here a policy stands in for it.
    /// Measuring the difference in death and maiming between a player who "never pulls out" and one who
    /// "pulls out at 30% health" is the only way to balance the limb-loss mechanic — limb loss only
    /// happens in fights where you intervene in time.
    /// </remarks>
    private static bool TryParsePolicy(string spec, out IRetreatPolicy? policy, out string label)
    {
        if (string.Equals(spec, "never", StringComparison.OrdinalIgnoreCase))
        {
            policy = NeverRetreat.Instance;
            label = "never pull out";
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
            label = string.Create(CultureInfo.InvariantCulture, $"pull out at second {atSecond:0.##}");
            return true;
        }

        if (spec.StartsWith("losing:", StringComparison.OrdinalIgnoreCase)
            && TryFraction(spec["losing:".Length..], out double losingAt))
        {
            policy = new RetreatWhenLosing(losingAt);
            label = string.Create(
                CultureInfo.InvariantCulture,
                $"pull out when outnumbered and health falls below {losingAt * 100:0.#}%");
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
                    $"pull out when health falls below {fraction * 100:0.#}%");
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

        writer.WriteLine("Domina batch combat simulation — for balance measurement.");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  Domina.Sim [--scenario <ad>] [--battles <N>] [--seed <S>]");
        writer.WriteLine("             [--policy never|below:<share>|losing:<share>|at:<sec>]");
        writer.WriteLine("             [--out <file.csv>]");
        writer.WriteLine("             [--grievous <0-1>] [--sever <0-1>]");
        writer.WriteLine("             [--armor none|light|medium|heavy] [--speed <0-100>]");
        writer.WriteLine("             [--charge-chance <0-1>] [--charge-chance-min/-max <0-1>]");
        writer.WriteLine("             [--charge-speed <>=1>] [--charge-damage <>=0>]");
        writer.WriteLine("             [--charge-windup <sec>] [--charge-counter <0-1>]");
        writer.WriteLine("             [--armor-attack-penalty <>=0>]");
        writer.WriteLine("             [--stun-chance <0-1>] [--stun-threshold <0-1>]");
        writer.WriteLine("             [--stun-seconds <sec>] [--stun-head <>=1>]");
        writer.WriteLine("             [--stun-disarm <0-1>]");
        writer.WriteLine("             [--stun-armor-share <0-1>]");
        writer.WriteLine("             [--catch-chance <0-1>] [--catch-bind <sec>]");
        writer.WriteLine("             [--catch-two-handed <0-1>] [--catch-stamina <number>]");
        writer.WriteLine("             [--catch-accuracy <0-1>]");
        writer.WriteLine("             [--poison-damage <number>] [--poison-seconds <sec>]");
        writer.WriteLine("             [--poison-tick <sec>] [--poison-dose <number>]");
        writer.WriteLine("             [--armor-durability <multiplier>]");
        writer.WriteLine("             [--target-wounded <points>] [--target-exposed <points>]");
        writer.WriteLine("             [--target-crowd <points>] [--target-sticky <points>]");
        writer.WriteLine("             [--block-chance <0-1>] [--block-seconds <sec>]");
        writer.WriteLine("             [--block-reduction <0-1>]");
        writer.WriteLine("             [--disarm-chance <0-1>] [--disarm-catch <0-1>]");
        writer.WriteLine("             [--disarm-armor-share <0-1>] [--drop-distance <units>]");
        writer.WriteLine("             [--pickup-radius <units>]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine($"  --scenario  The matchup to run (default: {DefaultScenario})");
        writer.WriteLine($"  --battles   Number of fights (default: {DefaultBattles})");
        writer.WriteLine($"  --seed      The first seed; the rest increment by one (default: {DefaultSeed})");
        writer.WriteLine("  --policy    never | below:<share> | losing:<share> | at:<sec>");
        writer.WriteLine("              (default: never)");
        writer.WriteLine("              below   = pull out when health drops (a crude baseline)");
        writer.WriteLine("              losing  = pull out when outnumbered and health drops (player model)");
        writer.WriteLine("              at      = pull out at the given second, whatever is happening");
        writer.WriteLine("  --out       CSV file to write one line per fight to");
        writer.WriteLine("  --grievous  Heavy-blow threshold (blow/max health ratio)");
        writer.WriteLine("  --sever     Base dismemberment chance on a heavy blow");
        writer.WriteLine("  --armor     Overrides the player side's kit (isolates the armour axis)");
        writer.WriteLine("  --speed     Overrides the player side's Speed stat (isolates the speed axis)");
        writer.WriteLine("  --charge-chance    Fixes the charge probability (flattens the Aggression curve)");
        writer.WriteLine("  --charge-chance-min/-max  The two ends of the Aggression curve");
        writer.WriteLine("  --charge-windup    The windup before the run (0 = no windup)");
        writer.WriteLine("  --charge-speed     The speed multiplier during a charge");
        writer.WriteLine("  --charge-damage    Damage share added to the arrival blow at maximum speed");
        writer.WriteLine("  --charge-counter   The charge target's counter-hit probability");
        writer.WriteLine("  --armor-speed-penalty    Walking speed lost at full armour");
        writer.WriteLine("  --armor-attack-penalty   How much the attack cycle stretches at full armour");
        writer.WriteLine("  --stun-chance      Base stun chance on a heavy blow");
        writer.WriteLine("  --stun-disarm      The chance a stunned warrior lets go of his own weapon");
        writer.WriteLine("  --stun-threshold   The blow/max health ratio at which the stun die is rolled");
        writer.WriteLine("  --stun-seconds     How long a stunned warrior is frozen");
        writer.WriteLine("  --stun-head        The stun multiplier for a blow to the head");
        writer.WriteLine("  --stun-armor-share The share of armour's dismemberment resistance counted against stun");
        writer.WriteLine("  --catch-chance     Base chance of catching an incoming strike with a catching implement");
        writer.WriteLine("  --catch-bind       How long the attacker whose weapon is caught stays exposed");
        writer.WriteLine("  --catch-two-handed The multiplier applied to a two-handed weapon's catch chance");
        writer.WriteLine("  --catch-stamina    The stamina cost of a catch");
        writer.WriteLine("  --catch-accuracy   The share added to catch chance at Accuracy 100");
        writer.WriteLine("  --poison-damage    Damage poison deals in one tick (at dose 1)");
        writer.WriteLine("  --poison-seconds   The lifetime of one dose");
        writer.WriteLine("  --poison-tick      The interval at which poison deals damage");
        writer.WriteLine("  --poison-dose      The maximum dose that can accumulate on one warrior");
        writer.WriteLine("  --armor-durability The multiplier for armour durability pools (0 = no wear)");
        writer.WriteLine("  --disarm-chance    Base chance of the weapon falling on a strike landing on armour");
        writer.WriteLine("  --disarm-catch     The chance a caught weapon leaves the palm");
        writer.WriteLine("  --disarm-armor-share The hardness share of the struck piece's dismemberment resistance");
        writer.WriteLine("  --drop-distance    How far a dropped weapon is flung from the warrior");
        writer.WriteLine("  --pickup-radius    The distance at which a weapon on the ground can be picked up");
        writer.WriteLine();
        writer.WriteLine("Scenarios:");
        foreach (Scenario s in Scenarios.All)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {s.Name,-10} {s.Description}"));
        }
    }
}
