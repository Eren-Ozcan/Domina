using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Sim;

/// <summary>The summary of a single fight as it enters batch simulation.</summary>
/// <remarks>
/// Limb losses are counted <b>piece by piece</b>. A total rate hides whether slot-by-slot armour is
/// working: a kit with bare arms and a full set can give the same total, while the distribution of the
/// limbs lost is entirely different.
/// </remarks>
internal sealed record BattleRow(
    ulong Seed,
    BattleOutcome Outcome,
    double Seconds,
    int PlayerDeaths,
    int PlayerEscapes,
    int PlayerLimbLosses,
    int LostArms,
    int LostLegs,
    int LostEyes,
    int EnemyDeaths,
    int EnemyWeaponsDropped,
    int PlayerAttacks,
    int PlayerHits,
    double PlayerDamageDealt,
    double PlayerDamageTaken,
    int PlayerStunsTaken,
    int PlayerStunsInflicted,
    int PlayerCatchesMade,
    int PlayerBlocksMade,
    int PlayerTimesCaught,
    double PlayerArmorWear,
    int PlayerArmorDestroyed,
    int PlayerWarriorsLosingArmor,
    int PlayerWeaponsDropped,
    int PlayerDisarmsInflicted,
    int PlayerWeaponsPickedUp,
    int PlayerTimesPoisoned,
    int PlayerPoisonsInflicted,
    double PlayerPoisonDamageTaken,
    double PlayerPoisonDamageDealt,
    int PlayerPoisonDeaths,
    int PlayerChargesStarted,
    int PlayerChargesConnected,
    int PlayerChargeOpportunitiesTaken,
    int PlayerChargesBroken,
    double PlayerChargeStartSecondsSum,
    double PlayerLastChargeStartSeconds);

/// <summary>
/// Runs the fights in a seed range.
/// </summary>
/// <remarks>
/// <para>
/// All balance work rests on this: running tens of thousands of fights without opening the engine and
/// looking at the death/maiming/victory rates. It is only possible because the resolver is independent
/// of the engine (see CLAUDE.md → "Architecture rule").
/// </para>
/// <para>
/// The roster is built <b>once</b> and reused across all fights; this is safe because
/// <see cref="Battle"/> does not change the warriors' persistent state, and it avoids needless object
/// allocation over 10,000 fights.
/// </para>
/// </remarks>
internal sealed class BatchRunner
{
    private readonly BattleSetup _setup;

    /// <param name="playerArmor">
    /// If given, replaces the kit of everyone on the player side with it. For measuring the armour axis
    /// <b>on its own</b>: the rest of the scenario stays fixed and only the kit changes, so it becomes
    /// clear whether a difference in limb loss comes from armour or from stats.
    /// </param>
    /// <param name="playerSpeed">
    /// If given, replaces the <c>Speed</c> of everyone on the player side with it. Same rationale as with
    /// armour: because speed now also feeds the force of the arrival blow (docs/GDD.md §4), the claim
    /// "a fast warrior charges better" can only be measured while everything else is fixed.
    /// </param>
    public BatchRunner(
        Scenario scenario,
        IRetreatPolicy? retreatPolicy,
        CombatTuning? tuning = null,
        Armor? playerArmor = null,
        double? playerSpeed = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        BattleSetup built = scenario.Build();

        if (playerArmor is not null)
        {
            foreach (Warrior w in built.PlayerSide)
            {
                w.Armor = playerArmor;
            }
        }

        if (playerSpeed is double speed)
        {
            foreach (Warrior w in built.PlayerSide)
            {
                w.BaseStats = w.BaseStats with { Speed = speed };
            }
        }

        _setup = built with
        {
            RetreatPolicy = retreatPolicy,
            Tuning = tuning ?? built.Tuning,

            // The event stream is only for visualisation; collecting it here would mean hundreds of
            // needless allocations per fight.
            CollectEvents = false,
        };

        PlayerSideSize = _setup.PlayerSide.Count;
        EnemySideSize = _setup.EnemySide.Count;
    }

    public int PlayerSideSize { get; }

    public int EnemySideSize { get; }

    /// <summary>
    /// Runs <paramref name="battles"/> fights starting from <paramref name="firstSeed"/>.
    /// </summary>
    /// <param name="onRow">Called when each fight ends (to stream into CSV).</param>
    public BatchReport Run(ulong firstSeed, int battles, Action<BattleRow>? onRow = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(battles);

        var report = new BatchReport(PlayerSideSize, EnemySideSize);

        for (int i = 0; i < battles; i++)
        {
            ulong seed = firstSeed + (ulong)i;
            BattleRow row = RunOne(seed);

            report.Add(row);
            onRow?.Invoke(row);
        }

        return report;
    }

    private BattleRow RunOne(ulong seed)
    {
        BattleResult result = new Battle(_setup, new SeededRandom(seed)).Run();

        int playerDeaths = 0;
        int playerEscapes = 0;
        int playerLimbLosses = 0;
        int lostArms = 0;
        int lostLegs = 0;
        int lostEyes = 0;
        int enemyDeaths = 0;
        int enemyWeaponsDropped = 0;
        int playerAttacks = 0;
        int playerHits = 0;
        double damageDealt = 0;
        double damageTaken = 0;
        int stunsTaken = 0;
        int stunsInflicted = 0;
        int catchesMade = 0;
        int blocksMade = 0;
        int timesCaught = 0;
        double armorWear = 0;
        int armorDestroyed = 0;
        int warriorsLosingArmor = 0;
        int weaponsDropped = 0;
        int disarmsInflicted = 0;
        int weaponsPickedUp = 0;
        int timesPoisoned = 0;
        int poisonsInflicted = 0;
        double poisonDamageTaken = 0;
        double poisonDamageDealt = 0;
        int poisonDeaths = 0;
        int chargesStarted = 0;
        int chargesConnected = 0;
        int chargeOpportunities = 0;
        int chargesBroken = 0;
        double chargeStartSum = 0;
        double lastChargeStart = 0;

        foreach (WarriorBattleSummary s in result.Summaries)
        {
            if (s.Team != Battle.PlayerTeam)
            {
                if (s.Died)
                {
                    enemyDeaths++;
                }

                enemyWeaponsDropped += s.TimesDisarmed;

                continue;
            }

            if (s.Died)
            {
                playerDeaths++;

                if (s.DeathCause == DeathCause.Poison)
                {
                    poisonDeaths++;
                }
            }

            if (s.Escaped)
            {
                playerEscapes++;
            }

            if (s.LostLimb)
            {
                playerLimbLosses++;

                // The pieces are counted one by one: a warrior can lose more than one limb in the same
                // fight, and a "came back maimed" rate hides that.
                foreach (BodyPart part in s.LostParts.Parts())
                {
                    if (part.IsArm())
                    {
                        lostArms++;
                    }
                    else if (part.IsLeg())
                    {
                        lostLegs++;
                    }
                    else if (part == BodyPart.Eye)
                    {
                        lostEyes++;
                    }
                }
            }

            stunsTaken += s.TimesStunned;
            stunsInflicted += s.StunsInflicted;
            catchesMade += s.CatchesMade;
            blocksMade += s.BlocksPerformed;
            timesCaught += s.TimesCaught;
            armorWear += s.ArmorWear.Total;

            int destroyed = s.DestroyedArmor.Count();
            armorDestroyed += destroyed;

            if (destroyed > 0)
            {
                warriorsLosingArmor++;
            }

            weaponsDropped += s.TimesDisarmed;
            disarmsInflicted += s.DisarmsInflicted;
            weaponsPickedUp += s.WeaponsPickedUp;
            timesPoisoned += s.TimesPoisoned;
            poisonsInflicted += s.PoisonsInflicted;
            poisonDamageTaken += s.PoisonDamageTaken;
            poisonDamageDealt += s.PoisonDamageDealt;
            chargesStarted += s.ChargesStarted;
            chargesConnected += s.ChargesConnected;
            chargeOpportunities += s.ChargeOpportunitiesTaken;
            chargesBroken += s.ChargesBroken;
            chargeStartSum += s.ChargeStartSecondsSum;
            lastChargeStart = Math.Max(lastChargeStart, s.LastChargeStartSeconds);
            playerAttacks += s.AttacksMade;
            playerHits += s.HitsLanded;
            damageDealt += s.DamageDealt;
            damageTaken += s.DamageTaken;
        }

        return new BattleRow(
            seed,
            result.Outcome,
            result.ElapsedSeconds,
            playerDeaths,
            playerEscapes,
            playerLimbLosses,
            lostArms,
            lostLegs,
            lostEyes,
            enemyDeaths,
            enemyWeaponsDropped,
            playerAttacks,
            playerHits,
            damageDealt,
            damageTaken,
            stunsTaken,
            stunsInflicted,
            catchesMade,
            blocksMade,
            timesCaught,
            armorWear,
            armorDestroyed,
            warriorsLosingArmor,
            weaponsDropped,
            disarmsInflicted,
            weaponsPickedUp,
            timesPoisoned,
            poisonsInflicted,
            poisonDamageTaken,
            poisonDamageDealt,
            poisonDeaths,
            chargesStarted,
            chargesConnected,
            chargeOpportunities,
            chargesBroken,
            chargeStartSum,
            lastChargeStart);
    }
}

/// <summary>A batch's totals and the rates derived from them.</summary>
internal sealed class BatchReport(int playerSideSize, int enemySideSize)
{
    public int Battles { get; private set; }

    public int Victories { get; private set; }

    /// <summary>The party pulled out alive — the expedition was spent, the warriors are still standing.</summary>
    public int Withdrawals { get; private set; }

    /// <summary>The party was wiped out — nobody escaped.</summary>
    public int Wipes { get; private set; }

    public int TimeLimits { get; private set; }

    public int PlayerDeaths { get; private set; }

    public int PlayerEscapes { get; private set; }

    public int PlayerLimbLosses { get; private set; }

    public int LostArms { get; private set; }

    public int LostLegs { get; private set; }

    public int LostEyes { get; private set; }

    public int EnemyDeaths { get; private set; }

    /// <summary>The number of times enemies dropped their weapon (as events).</summary>
    /// <remarks>
    /// This is armour's never-counted gain and it shows up in <b>no warrior's counter</b>: nobody
    /// disarmed the enemy who struck plate and lost his weapon. Kept together with the rest, the claim
    /// "armour takes the enemy's weapon out of his hand" could not be measured.
    /// </remarks>
    public int EnemyWeaponsDropped { get; private set; }

    public int PlayerAttacks { get; private set; }

    public int PlayerHits { get; private set; }

    public double TotalSeconds { get; private set; }

    public double PlayerDamageDealt { get; private set; }

    public double PlayerDamageTaken { get; private set; }

    /// <summary>The number of stuns the player's warriors took.</summary>
    /// <remarks>
    /// The blunt weapon's return only shows here: a cutting weapon produces limb loss, a blunt one
    /// produces this number. If the two do not stand side by side in the same measurement, the point
    /// where the trade turns cannot be found (docs/GDD.md Open Decision #4-B).
    /// </remarks>
    public int PlayerStunsTaken { get; private set; }

    /// <summary>The number of stuns the player's warriors inflicted on the enemy.</summary>
    public int PlayerStunsInflicted { get; private set; }

    /// <summary>The number of enemy strikes the player's warriors caught.</summary>
    /// <remarks>
    /// The jitte/sai's return only shows here: the catching implement loses on damage and takes its gain
    /// back in this number and in the window the bound enemy stays exposed
    /// (docs/GDD.md Open Decision #4-B).
    /// </remarks>
    public int PlayerCatchesMade { get; private set; }

    public int PlayerBlocksMade { get; private set; }

    /// <summary>The number of times the player's warriors had their weapon caught.</summary>
    public int PlayerTimesCaught { get; private set; }

    /// <summary>The total damage the player's kits absorbed.</summary>
    /// <remarks>
    /// <b>How many fights a kit lasts</b> comes only from here: the damage absorbed per fight, divided by
    /// the piece's durability, gives the piece's lifetime.
    /// </remarks>
    public double PlayerArmorWear { get; private set; }

    /// <summary>The number of player armour pieces broken — a <b>permanent</b> loss.</summary>
    /// <remarks>
    /// Armour's real price only shows here: even a won fight can take a piece off the kit
    /// (docs/GDD.md §7).
    /// </remarks>
    public int PlayerArmorDestroyed { get; private set; }

    /// <summary>The number of player warriors who lost at least one armour piece.</summary>
    public int PlayerWarriorsLosingArmor { get; private set; }

    /// <summary>The number of times the player's warriors dropped their weapon (as events).</summary>
    /// <remarks>
    /// The rule's price only shows here: a drop lands in neither the damage nor the limb-loss counter,
    /// but the warrior is left with his fists until he walks to his weapon
    /// (docs/GDD.md §7).
    /// </remarks>
    public int PlayerWeaponsDropped { get; private set; }

    /// <summary>The number of enemy weapons the player's warriors knocked out.</summary>
    public int PlayerDisarmsInflicted { get; private set; }

    /// <summary>The number of weapons the player's warriors picked up from the ground.</summary>
    /// <remarks>
    /// This is the number that says whether the price is settled — a drop is not a permanent loss but a
    /// <b>walk</b>. If the two do not stand side by side, how much the rule really bites cannot be
    /// known.
    /// </remarks>
    public int PlayerWeaponsPickedUp { get; private set; }

    /// <summary>The number of poisoned strikes the player's warriors took.</summary>
    public int PlayerTimesPoisoned { get; private set; }

    /// <summary>The number of poisoned strikes the player's warriors inflicted on the enemy.</summary>
    public int PlayerPoisonsInflicted { get; private set; }

    /// <summary>The total damage the player's warriors took from poison.</summary>
    /// <remarks>
    /// Poison's return only shows here: a poisoned weapon loses damage in an open fight and takes its
    /// gain back in this damage, which armour cannot reduce
    /// (docs/GDD.md Open Decision #4-B).
    /// </remarks>
    public double PlayerPoisonDamageTaken { get; private set; }

    /// <summary>The total damage the player's warriors dealt with poison.</summary>
    public double PlayerPoisonDamageDealt { get; private set; }

    /// <summary>The number of player warriors who died of poison.</summary>
    /// <remarks>
    /// This is the only number that says whether death came <b>on the field</b> or after it; poison's
    /// claim that it "kills differently" can only be confirmed from here.
    /// </remarks>
    public int PlayerPoisonDeaths { get; private set; }

    public int PlayerChargesStarted { get; private set; }

    public int PlayerChargesConnected { get; private set; }

    public int PlayerChargeOpportunitiesTaken { get; private set; }

    public int PlayerChargesBroken { get; private set; }

    public double PlayerChargeStartSecondsSum { get; private set; }

    /// <summary>The latest charge launch in the whole run.</summary>
    public double LatestChargeStart { get; private set; }

    /// <summary>The total player warriors who took the field in the batch — the denominator of the rates.</summary>
    public int PlayerAppearances => Battles * playerSideSize;

    public int EnemyAppearances => Battles * enemySideSize;

    public double VictoryRate => Rate(Victories, Battles);

    public double WithdrawalRate => Rate(Withdrawals, Battles);

    public double WipeRate => Rate(Wipes, Battles);

    public double TimeLimitRate => Rate(TimeLimits, Battles);

    /// <summary>The rate at which a player warrior who takes the field dies.</summary>
    public double PlayerDeathRate => Rate(PlayerDeaths, PlayerAppearances);

    public double PlayerEscapeRate => Rate(PlayerEscapes, PlayerAppearances);

    /// <summary>Balance work's most critical number: the rate at which permanent maiming is produced.</summary>
    public double PlayerLimbLossRate => Rate(PlayerLimbLosses, PlayerAppearances);

    public double LostArmRate => Rate(LostArms, PlayerAppearances);

    public double LostLegRate => Rate(LostLegs, PlayerAppearances);

    public double LostEyeRate => Rate(LostEyes, PlayerAppearances);

    public double EnemyDeathRate => Rate(EnemyDeaths, EnemyAppearances);

    /// <summary>The rate at which an enemy who takes the field drops his weapon.</summary>
    public double EnemyWeaponDropRate => Rate(EnemyWeaponsDropped, EnemyAppearances);

    public double PlayerAccuracy => Rate(PlayerHits, PlayerAttacks);

    /// <summary>Charges per fight — the joint output of the threshold and the probability.</summary>
    public double ChargesPerBattle => Battles == 0 ? 0 : (double)PlayerChargesStarted / Battles;

    /// <summary>The rate at which started charges reach their target.</summary>
    public double ChargeConnectRate => Rate(PlayerChargesConnected, PlayerChargesStarted);

    /// <summary>The average moment a charge launches — where in the fight charging happens.</summary>
    public double AverageChargeStart => PlayerChargesStarted == 0
        ? 0
        : PlayerChargeStartSecondsSum / PlayerChargesStarted;

    /// <summary>The share of charges scattered during the windup.</summary>
    public double ChargeBreakRate => Rate(PlayerChargesBroken, PlayerChargesStarted);

    /// <summary>Free hits taken per charge — the measure of the price §4 promised.</summary>
    public double OpportunitiesPerCharge => PlayerChargesStarted == 0
        ? 0
        : (double)PlayerChargeOpportunitiesTaken / PlayerChargesStarted;

    /// <summary>The stuns a player warrior who takes the field suffers per fight.</summary>
    public double StunsTakenPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerStunsTaken / PlayerAppearances;

    /// <summary>The stuns a player warrior who takes the field inflicts per fight.</summary>
    public double StunsInflictedPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerStunsInflicted / PlayerAppearances;

    /// <summary>The catches a player warrior who takes the field makes per fight.</summary>
    public double CatchesPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerCatchesMade / PlayerAppearances;

    /// <summary>Not the ratio of strikes caught to all strikes aimed at him — it is the number of times
    /// he is caught per fight; it shows whether catching runs both ways.</summary>
    public double TimesCaughtPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerTimesCaught / PlayerAppearances;

    /// <summary>The number of blows a player warrior meets with a block per fight.</summary>
    /// <remarks>
    /// A block's price is the strike not made; this counter does not say "is it working" on its own. It
    /// is read next to <see cref="ArmorWearPerWarrior"/> and the limb-loss rate: if blocking lowers
    /// damage and dismemberment without lowering victory, the stance pays its price.
    /// </remarks>
    public double BlocksPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerBlocksMade / PlayerAppearances;

    /// <summary>The damage a player warrior lets his kit absorb per fight.</summary>
    public double ArmorWearPerWarrior =>
        PlayerAppearances == 0 ? 0 : PlayerArmorWear / PlayerAppearances;

    /// <summary>The rate at which a player warrior who takes the field loses a piece from his kit.</summary>
    public double ArmorLossRate => Rate(PlayerWarriorsLosingArmor, PlayerAppearances);

    /// <summary>The number of pieces a player warrior who takes the field loses per fight.</summary>
    public double ArmorPiecesLostPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerArmorDestroyed / PlayerAppearances;

    /// <summary>The rate at which a player warrior who takes the field drops his weapon per fight.</summary>
    public double WeaponDropRate => Rate(PlayerWeaponsDropped, PlayerAppearances);

    /// <summary>The rate at which dropped weapons are picked up.</summary>
    public double PickupRate => Rate(PlayerWeaponsPickedUp, PlayerWeaponsDropped);

    /// <summary>The enemy weapons a player warrior who takes the field knocks out per fight.</summary>
    public double DisarmsPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerDisarmsInflicted / PlayerAppearances;

    /// <summary>The poisoned strikes a player warrior who takes the field takes per fight.</summary>
    public double PoisoningsTakenPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerTimesPoisoned / PlayerAppearances;

    /// <summary>The poisoned strikes a player warrior who takes the field inflicts per fight.</summary>
    public double PoisoningsInflictedPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerPoisonsInflicted / PlayerAppearances;

    /// <summary>The share of deaths that come from poison.</summary>
    public double PoisonDeathShare => Rate(PlayerPoisonDeaths, PlayerDeaths);

    /// <summary>The share of damage dealt that comes from poison — how much work the dose really does.</summary>
    public double PoisonShareOfDamageDealt =>
        PlayerDamageDealt <= 0 ? 0 : PlayerPoisonDamageDealt / PlayerDamageDealt;

    public double AverageSeconds => Battles == 0 ? 0 : TotalSeconds / Battles;

    public void Add(BattleRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        Battles++;
        TotalSeconds += row.Seconds;

        switch (row.Outcome)
        {
            case BattleOutcome.PlayerVictory:
                Victories++;
                break;
            case BattleOutcome.PlayerWithdrawal:
                Withdrawals++;
                break;
            case BattleOutcome.PlayerWipe:
                Wipes++;
                break;
            case BattleOutcome.TimeLimit:
            default:
                TimeLimits++;
                break;
        }

        PlayerDeaths += row.PlayerDeaths;
        PlayerEscapes += row.PlayerEscapes;
        PlayerLimbLosses += row.PlayerLimbLosses;
        LostArms += row.LostArms;
        LostLegs += row.LostLegs;
        LostEyes += row.LostEyes;
        EnemyDeaths += row.EnemyDeaths;
        EnemyWeaponsDropped += row.EnemyWeaponsDropped;
        PlayerAttacks += row.PlayerAttacks;
        PlayerHits += row.PlayerHits;
        PlayerDamageDealt += row.PlayerDamageDealt;
        PlayerDamageTaken += row.PlayerDamageTaken;
        PlayerStunsTaken += row.PlayerStunsTaken;
        PlayerCatchesMade += row.PlayerCatchesMade;
        PlayerBlocksMade += row.PlayerBlocksMade;
        PlayerTimesCaught += row.PlayerTimesCaught;
        PlayerArmorWear += row.PlayerArmorWear;
        PlayerArmorDestroyed += row.PlayerArmorDestroyed;
        PlayerWarriorsLosingArmor += row.PlayerWarriorsLosingArmor;
        PlayerWeaponsDropped += row.PlayerWeaponsDropped;
        PlayerDisarmsInflicted += row.PlayerDisarmsInflicted;
        PlayerWeaponsPickedUp += row.PlayerWeaponsPickedUp;
        PlayerTimesPoisoned += row.PlayerTimesPoisoned;
        PlayerPoisonsInflicted += row.PlayerPoisonsInflicted;
        PlayerPoisonDamageTaken += row.PlayerPoisonDamageTaken;
        PlayerPoisonDamageDealt += row.PlayerPoisonDamageDealt;
        PlayerPoisonDeaths += row.PlayerPoisonDeaths;
        PlayerStunsInflicted += row.PlayerStunsInflicted;
        PlayerChargesStarted += row.PlayerChargesStarted;
        PlayerChargesConnected += row.PlayerChargesConnected;
        PlayerChargeOpportunitiesTaken += row.PlayerChargeOpportunitiesTaken;
        PlayerChargesBroken += row.PlayerChargesBroken;
        PlayerChargeStartSecondsSum += row.PlayerChargeStartSecondsSum;
        LatestChargeStart = Math.Max(LatestChargeStart, row.PlayerLastChargeStartSeconds);
    }

    private static double Rate(int part, int total) => total == 0 ? 0 : (double)part / total;
}
