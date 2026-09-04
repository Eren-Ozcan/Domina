using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Sim;

/// <summary>Tek bir dövüşün toplu simülasyona giren özeti.</summary>
/// <remarks>
/// Uzuv kayıpları <b>parça parça</b> sayılır. Toplam oran, yuva yuva zırhın işe
/// yarayıp yaramadığını gizler: kolları açık bir kuşam ile tam takım aynı toplamı
/// verebilir, ama kaybedilen uzuvların dağılımı bambaşkadır.
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
    int PlayerAttacks,
    int PlayerHits,
    double PlayerDamageDealt,
    double PlayerDamageTaken,
    int PlayerStunsTaken,
    int PlayerStunsInflicted,
    int PlayerChargesStarted,
    int PlayerChargesConnected,
    int PlayerChargeOpportunitiesTaken,
    int PlayerChargesBroken,
    double PlayerChargeStartSecondsSum,
    double PlayerLastChargeStartSeconds);

/// <summary>
/// Bir seed aralığındaki dövüşleri koşturur.
/// </summary>
/// <remarks>
/// <para>
/// Denge çalışmasının tamamı buna dayanır: motor açmadan on binlerce dövüş koşup
/// ölüm/sakatlık/kazanma oranlarına bakmak. Bu ancak çözümleyici motordan bağımsız
/// olduğu için mümkün (bkz. CLAUDE.md → "Mimari kuralı").
/// </para>
/// <para>
/// Kadro <b>bir kez</b> kurulur ve tüm dövüşlerde tekrar kullanılır; <see cref="Battle"/>
/// savaşçıların kalıcı halini değiştirmediği için bu güvenlidir ve 10.000 dövüşte
/// gereksiz nesne ayırmayı önler.
/// </para>
/// </remarks>
internal sealed class BatchRunner
{
    private readonly BattleSetup _setup;

    /// <param name="playerArmor">
    /// Verilirse oyuncu tarafındaki herkesin kuşamını bununla değiştirir. Zırh eksenini
    /// <b>tek başına</b> ölçmek için: senaryonun geri kalanı sabit kalır, yalnızca kuşam
    /// değişir, böylece uzuv kaybı farkı zırhtan mı yoksa statlardan mı geliyor ayrışır.
    /// </param>
    /// <param name="playerSpeed">
    /// Verilirse oyuncu tarafındaki herkesin <c>Speed</c>'ini bununla değiştirir. Zırhla
    /// aynı gerekçe: hız artık varış vuruşunun sertliğine de işlediği için (docs/GDD.md §4)
    /// "hızlı savaşçı daha iyi hücum eder" iddiası ancak diğer her şey sabitken ölçülebilir.
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

            // Olay akışı yalnızca görselleştirme içindir; burada biriktirmek
            // dövüş başına yüzlerce gereksiz ayırma demek olurdu.
            CollectEvents = false,
        };

        PlayerSideSize = _setup.PlayerSide.Count;
        EnemySideSize = _setup.EnemySide.Count;
    }

    public int PlayerSideSize { get; }

    public int EnemySideSize { get; }

    /// <summary>
    /// <paramref name="firstSeed"/>'den başlayarak <paramref name="battles"/> dövüş koşturur.
    /// </summary>
    /// <param name="onRow">Her dövüş bitince çağrılır (CSV'ye akıtmak için).</param>
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
        int playerAttacks = 0;
        int playerHits = 0;
        double damageDealt = 0;
        double damageTaken = 0;
        int stunsTaken = 0;
        int stunsInflicted = 0;
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

                continue;
            }

            if (s.Died)
            {
                playerDeaths++;
            }

            if (s.Escaped)
            {
                playerEscapes++;
            }

            if (s.LostLimb)
            {
                playerLimbLosses++;

                // Parçalar tek tek sayılır: bir savaşçı aynı dövüşte birden fazla
                // uzvunu kaybedebilir, "sakat döndü" oranı bunu gizler.
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
            playerAttacks,
            playerHits,
            damageDealt,
            damageTaken,
            stunsTaken,
            stunsInflicted,
            chargesStarted,
            chargesConnected,
            chargeOpportunities,
            chargesBroken,
            chargeStartSum,
            lastChargeStart);
    }
}

/// <summary>Bir partinin toplam sayıları ve türetilmiş oranları.</summary>
internal sealed class BatchReport(int playerSideSize, int enemySideSize)
{
    public int Battles { get; private set; }

    public int Victories { get; private set; }

    /// <summary>Ekip sağ çekildi — sefer harcandı, savaşçılar duruyor.</summary>
    public int Withdrawals { get; private set; }

    /// <summary>Ekip kırıldı — kimse kaçamadı.</summary>
    public int Wipes { get; private set; }

    public int TimeLimits { get; private set; }

    public int PlayerDeaths { get; private set; }

    public int PlayerEscapes { get; private set; }

    public int PlayerLimbLosses { get; private set; }

    public int LostArms { get; private set; }

    public int LostLegs { get; private set; }

    public int LostEyes { get; private set; }

    public int EnemyDeaths { get; private set; }

    public int PlayerAttacks { get; private set; }

    public int PlayerHits { get; private set; }

    public double TotalSeconds { get; private set; }

    public double PlayerDamageDealt { get; private set; }

    public double PlayerDamageTaken { get; private set; }

    /// <summary>Oyuncu savaşçılarının yediği sersemletme sayısı.</summary>
    /// <remarks>
    /// Künt silahın karşılığı yalnızca burada görünür: kesici uzuv kaybı üretir, künt
    /// bu sayıyı üretir. İkisi aynı ölçümde yan yana durmazsa takasın döndüğü yer
    /// bulunamaz (docs/GDD.md Açık Karar #4-B).
    /// </remarks>
    public int PlayerStunsTaken { get; private set; }

    /// <summary>Oyuncu savaşçılarının düşmana geçirdiği sersemletme sayısı.</summary>
    public int PlayerStunsInflicted { get; private set; }

    public int PlayerChargesStarted { get; private set; }

    public int PlayerChargesConnected { get; private set; }

    public int PlayerChargeOpportunitiesTaken { get; private set; }

    public int PlayerChargesBroken { get; private set; }

    public double PlayerChargeStartSecondsSum { get; private set; }

    /// <summary>Bütün koşumdaki en geç hücum kalkışı.</summary>
    public double LatestChargeStart { get; private set; }

    /// <summary>Partide sahaya çıkan toplam oyuncu savaşçısı — oranların paydası.</summary>
    public int PlayerAppearances => Battles * playerSideSize;

    public int EnemyAppearances => Battles * enemySideSize;

    public double VictoryRate => Rate(Victories, Battles);

    public double WithdrawalRate => Rate(Withdrawals, Battles);

    public double WipeRate => Rate(Wipes, Battles);

    public double TimeLimitRate => Rate(TimeLimits, Battles);

    /// <summary>Sahaya çıkan bir oyuncu savaşçısının ölme oranı.</summary>
    public double PlayerDeathRate => Rate(PlayerDeaths, PlayerAppearances);

    public double PlayerEscapeRate => Rate(PlayerEscapes, PlayerAppearances);

    /// <summary>Denge çalışmasının en kritik sayısı: kalıcı sakatlık üretme hızı.</summary>
    public double PlayerLimbLossRate => Rate(PlayerLimbLosses, PlayerAppearances);

    public double LostArmRate => Rate(LostArms, PlayerAppearances);

    public double LostLegRate => Rate(LostLegs, PlayerAppearances);

    public double LostEyeRate => Rate(LostEyes, PlayerAppearances);

    public double EnemyDeathRate => Rate(EnemyDeaths, EnemyAppearances);

    public double PlayerAccuracy => Rate(PlayerHits, PlayerAttacks);

    /// <summary>Dövüş başına düşen hücum sayısı — eşik ve olasılığın birlikte çıktısı.</summary>
    public double ChargesPerBattle => Battles == 0 ? 0 : (double)PlayerChargesStarted / Battles;

    /// <summary>Başlayan hücumların hedefe varma oranı.</summary>
    public double ChargeConnectRate => Rate(PlayerChargesConnected, PlayerChargesStarted);

    /// <summary>Hücumun ortalama kalkış anı — dövüşün neresinde hücum ediliyor.</summary>
    public double AverageChargeStart => PlayerChargesStarted == 0
        ? 0
        : PlayerChargeStartSecondsSum / PlayerChargesStarted;

    /// <summary>Birikme aşamasında dağılan hücumların oranı.</summary>
    public double ChargeBreakRate => Rate(PlayerChargesBroken, PlayerChargesStarted);

    /// <summary>Hücum başına yenen bedava vuruş — §4'ün vaat ettiği bedelin ölçüsü.</summary>
    public double OpportunitiesPerCharge => PlayerChargesStarted == 0
        ? 0
        : (double)PlayerChargeOpportunitiesTaken / PlayerChargesStarted;

    /// <summary>Sahaya çıkan bir oyuncu savaşçısının dövüş başına yediği sersemletme.</summary>
    public double StunsTakenPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerStunsTaken / PlayerAppearances;

    /// <summary>Sahaya çıkan bir oyuncu savaşçısının dövüş başına geçirdiği sersemletme.</summary>
    public double StunsInflictedPerWarrior =>
        PlayerAppearances == 0 ? 0 : (double)PlayerStunsInflicted / PlayerAppearances;

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
        PlayerAttacks += row.PlayerAttacks;
        PlayerHits += row.PlayerHits;
        PlayerDamageDealt += row.PlayerDamageDealt;
        PlayerDamageTaken += row.PlayerDamageTaken;
        PlayerStunsTaken += row.PlayerStunsTaken;
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
