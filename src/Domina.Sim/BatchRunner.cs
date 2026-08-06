using Domina.Core.Combat;
using Domina.Core.Rng;

namespace Domina.Sim;

/// <summary>Tek bir dövüşün toplu simülasyona giren özeti.</summary>
internal sealed record BattleRow(
    ulong Seed,
    BattleOutcome Outcome,
    double Seconds,
    int PlayerDeaths,
    int PlayerEscapes,
    int PlayerLimbLosses,
    int EnemyDeaths,
    int PlayerAttacks,
    int PlayerHits,
    double PlayerDamageDealt,
    double PlayerDamageTaken);

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

    public BatchRunner(Scenario scenario, IRetreatPolicy? retreatPolicy)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        _setup = scenario.Build() with
        {
            RetreatPolicy = retreatPolicy,

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
        int enemyDeaths = 0;
        int playerAttacks = 0;
        int playerHits = 0;
        double damageDealt = 0;
        double damageTaken = 0;

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
            }

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
            enemyDeaths,
            playerAttacks,
            playerHits,
            damageDealt,
            damageTaken);
    }
}

/// <summary>Bir partinin toplam sayıları ve türetilmiş oranları.</summary>
internal sealed class BatchReport(int playerSideSize, int enemySideSize)
{
    public int Battles { get; private set; }

    public int Victories { get; private set; }

    public int Defeats { get; private set; }

    public int TimeLimits { get; private set; }

    public int PlayerDeaths { get; private set; }

    public int PlayerEscapes { get; private set; }

    public int PlayerLimbLosses { get; private set; }

    public int EnemyDeaths { get; private set; }

    public int PlayerAttacks { get; private set; }

    public int PlayerHits { get; private set; }

    public double TotalSeconds { get; private set; }

    public double PlayerDamageDealt { get; private set; }

    public double PlayerDamageTaken { get; private set; }

    /// <summary>Partide sahaya çıkan toplam oyuncu savaşçısı — oranların paydası.</summary>
    public int PlayerAppearances => Battles * playerSideSize;

    public int EnemyAppearances => Battles * enemySideSize;

    public double VictoryRate => Rate(Victories, Battles);

    public double DefeatRate => Rate(Defeats, Battles);

    public double TimeLimitRate => Rate(TimeLimits, Battles);

    /// <summary>Sahaya çıkan bir oyuncu savaşçısının ölme oranı.</summary>
    public double PlayerDeathRate => Rate(PlayerDeaths, PlayerAppearances);

    public double PlayerEscapeRate => Rate(PlayerEscapes, PlayerAppearances);

    /// <summary>Denge çalışmasının en kritik sayısı: kalıcı sakatlık üretme hızı.</summary>
    public double PlayerLimbLossRate => Rate(PlayerLimbLosses, PlayerAppearances);

    public double EnemyDeathRate => Rate(EnemyDeaths, EnemyAppearances);

    public double PlayerAccuracy => Rate(PlayerHits, PlayerAttacks);

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
            case BattleOutcome.PlayerDefeat:
                Defeats++;
                break;
            case BattleOutcome.TimeLimit:
            default:
                TimeLimits++;
                break;
        }

        PlayerDeaths += row.PlayerDeaths;
        PlayerEscapes += row.PlayerEscapes;
        PlayerLimbLosses += row.PlayerLimbLosses;
        EnemyDeaths += row.EnemyDeaths;
        PlayerAttacks += row.PlayerAttacks;
        PlayerHits += row.PlayerHits;
        PlayerDamageDealt += row.PlayerDamageDealt;
        PlayerDamageTaken += row.PlayerDamageTaken;
    }

    private static double Rate(int part, int total) => total == 0 ? 0 : (double)part / total;
}
