using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Pes etme mekaniği (GDD §5). Buradaki kuralların tamamı aynı amaca hizmet eder:
/// "çek" tuşuna basmak <b>bedava kurtuluş olmasın</b>. Komut hemen işlemez, kaçış
/// süresince savunma yoktur ve rakip bedava vuruş kazanır. Bu üçü olmadan doğru
/// oynanış "her savaşçıyı ilk yara alınca çek" olurdu.
/// </summary>
public class RetreatTests
{
    private static readonly WarriorId _fighter = new(1);
    private static readonly WarriorId _enemy = new(101);

    private static BattleSetup Duel(double playerHealth = 400, double playerEvasion = 0) => new(
        [TestBuilders.Warrior(1, health: playerHealth, evasion: playerEvasion)],
        [TestBuilders.Warrior(101, health: 400)])
    {
        Tuning = TestBuilders.PointBlank,
    };

    /// <summary>Belirli bir duruma girene kadar adımlar.</summary>
    private static bool StepUntil(Battle battle, Func<Battle, bool> predicate, int maxSteps = 400)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            if (predicate(battle))
            {
                return true;
            }

            if (!battle.Step())
            {
                return false;
            }
        }

        return false;
    }

    [Fact]
    public void CommandIsAcceptedImmediatelyWhenTheWarriorIsIdle()
    {
        var battle = new Battle(Duel(), new SeededRandom(1));

        Assert.True(battle.CommandRetreat());
        Assert.Equal(CombatState.Retreating, battle.SnapshotOf(_fighter).State);

        Assert.Contains(battle.Events, e => e is RetreatCommanded);
        Assert.Contains(battle.Events, e => e is RetreatStarted);
        Assert.DoesNotContain(battle.Events, e => e is RetreatBuffered);
    }

    [Fact]
    public void CommandIsBufferedWhileTheSwordIsInTheAir()
    {
        var battle = new Battle(Duel(), new SeededRandom(2));

        Assert.True(StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.AttackWindup));

        Assert.True(battle.CommandRetreat());

        // Komut kabul edildi ama vuruş tamamlanmadan kaçış başlamaz.
        Assert.Contains(battle.Events, e => e is RetreatBuffered);
        Assert.DoesNotContain(battle.Events, e => e is RetreatStarted);
        Assert.Equal(CombatState.AttackWindup, battle.SnapshotOf(_fighter).State);
        Assert.True(battle.SnapshotOf(_fighter).RetreatRequested);
    }

    [Fact]
    public void ABufferedCommandRunsAsSoonAsTheAttackFinishes()
    {
        var battle = new Battle(Duel(), new SeededRandom(3));

        StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.AttackWindup);
        battle.CommandRetreat();

        Assert.True(StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.Retreating));
        Assert.Contains(battle.Events, e => e is RetreatStarted);

        // Buffer'lanan komut önce vuruşu tamamlatır: kaçış, saldırıdan sonra başlar.
        double buffered = battle.Events.OfType<RetreatBuffered>().First().AtSeconds;
        double started = battle.Events.OfType<RetreatStarted>().First().AtSeconds;
        Assert.True(started > buffered, "Buffer'lanan kaçış anında başlamamalı.");
    }

    [Fact]
    public void RetreatingCostsAFreeSwingToTheOpponent()
    {
        var battle = new Battle(Duel(), new SeededRandom(4));
        battle.CommandRetreat();

        OpportunityAttack free = battle.Events.OfType<OpportunityAttack>().First();
        Assert.Equal(_enemy, free.Attacker);
        Assert.Equal(_fighter, free.Defender);
    }

    [Fact]
    public void ARetreatingWarriorCannotDodge()
    {
        // Kaçınma 100 ve zar 0.40: normalde kaçınır (şans 0.45), çekilirken kaçamaz.
        var defending = new Battle(Duel(playerEvasion: 100), new FixedRandom(0.40));
        StepUntil(defending, b => b.Events.Any(e => e is AttackDodged or AttackLanded));

        var fleeing = new Battle(Duel(playerEvasion: 100), new FixedRandom(0.40));
        fleeing.CommandRetreat();

        Assert.Contains(defending.Events, e => e is AttackDodged);
        Assert.Contains(fleeing.Events, e => e is AttackLanded { Defender.Value: 1 });
        Assert.DoesNotContain(fleeing.Events, e => e is AttackDodged);
    }

    [Fact]
    public void EscapingLeavesTheArenaAliveButLosesTheBattle()
    {
        var battle = new Battle(Duel(), new SeededRandom(5));
        battle.CommandRetreat();

        BattleResult result = battle.Run();
        WarriorBattleSummary summary = result.SummaryFor(_fighter);

        Assert.True(summary.Escaped);
        Assert.False(summary.Died);
        Assert.True(summary.HealthRemaining > 0);

        // Sağ kalmak zafer değildir ama bozgun da değildir: dövüş kazanılmadı,
        // savaşçı hayatta. İkisi ayrı sonuçlar.
        Assert.Equal(BattleOutcome.PlayerWithdrawal, result.Outcome);
        Assert.Contains(battle.Events, e => e is WarriorEscaped);
    }

    [Fact]
    public void RepeatingTheCommandChangesNothing()
    {
        var battle = new Battle(Duel(), new SeededRandom(6));

        Assert.True(battle.CommandRetreat());
        Assert.False(battle.CommandRetreat());
    }

    [Fact]
    public void AnEmptyArenaRejectsFurtherCommands()
    {
        var battle = new Battle(Duel(), new SeededRandom(7));
        battle.CommandRetreat();
        battle.Run();

        Assert.False(battle.CommandRetreat());
    }

    [Fact]
    public void UnknownWarriorsAreRejectedRatherThanThrowing() =>
        Assert.Throws<ArgumentException>(() =>
            new Battle(Duel(), new SeededRandom(8)).SnapshotOf(new WarriorId(999)));

    /// <summary>
    /// GDD §5'in ana kuralı: komut <b>ekibin tamamını</b> kapsar. Savaşçı bazlı
    /// olsaydı doğru oynanış "yara alanı çek, kalanla devam et" olurdu — kayıpsız,
    /// sürekli tekrarlanan bir optimizasyon. Bu test o kapıyı kapalı tutar.
    /// </summary>
    [Fact]
    public void TheWholePartyRetreatsTogether()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400),
                TestBuilders.Warrior(2, health: 400),
                TestBuilders.Warrior(3, health: 400),
            ],
            [TestBuilders.Warrior(101, health: 400)])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new SeededRandom(9));
        Assert.True(battle.CommandRetreat());

        for (int id = 1; id <= 3; id++)
        {
            Assert.True(battle.SnapshotOf(new WarriorId(id)).RetreatRequested);
        }

        BattleResult result = battle.Run();

        // Kimse geride bırakılmaz; sahada dojo adına kimse kalmadı ama kimse de ölmedi.
        Assert.Equal(3, result.Summaries.Count(s => s.Team == Battle.PlayerTeam && s.Escaped));
        Assert.Equal(BattleOutcome.PlayerWithdrawal, result.Outcome);
    }

    /// <summary>
    /// Tek tuş, üç farklı anda devreye girebilir: kılıcı havada olan savaşçının
    /// komutu buffer'lanır, boşta olan hemen kaçar. Ekip komutu bu inceliği
    /// ortadan kaldırmaz.
    /// </summary>
    [Fact]
    public void OnePressResolvesPerWarriorTiming()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400, aggression: 100),
                TestBuilders.Warrior(2, health: 400, aggression: 0),
            ],
            [TestBuilders.Warrior(101, health: 400)])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new SeededRandom(21));

        // Biri vuruşa kilitlenene kadar ilerlet; diğeri hâlâ bekliyor olacak.
        StepUntil(battle, b => b.SnapshotOf(new WarriorId(1)).State == CombatState.AttackWindup);
        Assert.True(battle.SnapshotOf(new WarriorId(2)).CanCancel);

        battle.CommandRetreat();

        Assert.Contains(battle.Events, e => e is RetreatBuffered { Warrior.Value: 1 });
        Assert.Contains(battle.Events, e => e is RetreatStarted { Warrior.Value: 2 });
        Assert.Equal(CombatState.AttackWindup, battle.SnapshotOf(new WarriorId(1)).State);
        Assert.Equal(CombatState.Retreating, battle.SnapshotOf(new WarriorId(2)).State);
    }

    /// <summary>
    /// Ekip komutunun uzuv kaybı mekaniğine etkisi: tuşa basmak <b>herkesi</b>
    /// ölüm yerine sakatlıkla kurtarır. Bedeli de herkesin ödemesi bu yüzden şart
    /// (bkz. <see cref="HonorTests"/> — kaçan savaşçı onur kazanmaz).
    /// </summary>
    [Fact]
    public void InterventionProtectsEveryWarriorNotJustOne()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 300, aggression: 0),
                TestBuilders.Warrior(2, health: 300, aggression: 0),
            ],
            [TestBuilders.Warrior(101, aggression: 100, weapon: TestBuilders.Executioner())])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.CommandRetreat();
        BattleResult result = battle.Run();

        Assert.DoesNotContain(result.Summaries, s => s.Team == Battle.PlayerTeam && s.Died);
    }

    /// <summary>
    /// Politika tek bir savaşçının haline bakar ama tuşla aynı kuralı izler: tetiklenen
    /// komut ekibin tamamını çeker. Aksi hâlde toplu simülasyon oyunda mümkün olmayan
    /// bir oynanışı ölçer ve denge sayıları yanlış çıkardı.
    /// </summary>
    [Fact]
    public void ThePolicyPullsTheWholePartyAndNeverTheEnemy()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400),
                TestBuilders.Warrior(2, health: 400),
            ],
            [TestBuilders.Warrior(101, health: 400)])
        {
            // Yalnızca ilk savaşçının canı eşiğin altına inecek şekilde değil —
            // eşik 1.0, yani ilk adımda tetikler.
            RetreatPolicy = new RetreatBelowHealth(1.0),
        };

        var battle = new Battle(setup, new SeededRandom(10));
        battle.Step();

        Assert.True(battle.SnapshotOf(new WarriorId(1)).RetreatRequested);
        Assert.True(battle.SnapshotOf(new WarriorId(2)).RetreatRequested);
        Assert.False(battle.SnapshotOf(_enemy).RetreatRequested);
    }

    /// <summary>
    /// Görselleştirmenin sözleşmesi: <see cref="CombatantSnapshot.CanCancel"/>, "çek"
    /// tuşunun anında mı işleyeceğini yoksa buffer'lanacağını mı söyler. Oyuncunun
    /// tuşa basmadan önce bunu görebilmesi gerekiyor, yoksa buffer'lama sürpriz olur.
    /// </summary>
    [Fact]
    public void TheSnapshotTellsWhetherACommandWouldBeBuffered()
    {
        var battle = new Battle(Duel(), new SeededRandom(12));

        Assert.True(battle.SnapshotOf(_fighter).CanCancel);

        StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.AttackWindup);
        Assert.False(battle.SnapshotOf(_fighter).CanCancel);

        StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.AttackRecovery);
        Assert.True(battle.SnapshotOf(_fighter).CanCancel);
    }

    /// <summary>
    /// Animasyon, durumun neresinde olunduğuna göre sürülür; oran durum boyunca
    /// 0'dan 1'e ilerlemeli, yoksa vuruş animasyonu çözümlemeyle senkron tutmaz.
    /// </summary>
    [Fact]
    public void StateProgressAdvancesFromStartToEnd()
    {
        var battle = new Battle(Duel(), new SeededRandom(13));
        StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.AttackWindup);

        double first = battle.SnapshotOf(_fighter).StateProgress;
        double last = first;

        while (battle.SnapshotOf(_fighter).State == CombatState.AttackWindup)
        {
            double now = battle.SnapshotOf(_fighter).StateProgress;
            Assert.InRange(now, 0, 1);
            Assert.True(now >= last, "Durum ilerlemesi geriye gitmemeli.");
            last = now;

            if (!battle.Step())
            {
                break;
            }
        }

        Assert.True(last > first, "Windup boyunca ilerleme artmalı.");
    }

    [Fact]
    public void NeverRetreatLeavesEveryoneFighting()
    {
        var setup = Duel() with { RetreatPolicy = NeverRetreat.Instance };
        BattleResult result = new Battle(setup, new SeededRandom(11)).Run();

        Assert.DoesNotContain(result.Summaries, s => s.Escaped);
    }
}
