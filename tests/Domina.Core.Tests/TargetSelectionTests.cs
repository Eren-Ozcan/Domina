using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Hedef seçimi bir karardır, bir sıralama değil: savaşçı her karar adımında düşmanları
/// mesafe, yara, açık bölge ve takım arkadaşlarının yığılması üzerinden tartar. Bu testler
/// ağırlıkların yönünü ve kuralın iki frenini (fırsat penceresi, yapışkanlık) bağlar.
/// </summary>
public class TargetSelectionTests
{
    private static CombatTuning Quiet { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        BaseDisarmChance = 0,
        CatchDisarmChance = 0,
        MaxBattleSeconds = 6,
    };

    /// <summary>İlk saldırının kime yöneldiği — seçimin gözlemlenebilir hâli.</summary>
    private static WarriorId FirstTargetOf(Battle battle, WarriorId attacker)
    {
        battle.Run();

        return battle.Events.OfType<AttackStarted>().First(a => a.Attacker == attacker).Defender;
    }

    /// <summary>Yara farkı büyüdükçe takım yaralının üstüne döner.</summary>
    /// <remarks>
    /// Kural doğrudan sınanamaz: çekirdek canı dışarıya açmıyor (dövüşe tek müdahale
    /// noktası <c>CommandRetreat</c>), yani "şu düşmanı yarala" diye bir kurulum yok.
    /// Sınanan şey bu yüzden sonuç: yapışkanlık kapalı ve yara ağırlığı baskınken iki
    /// savaşçı da dövüşün sonunda <b>aynı</b> — en yaralı — düşmanı dövüyor olmalı.
    /// </remarks>
    [Fact]
    public void TheWoundedOneDrawsTheTeam()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, "Biri", aggression: 100, weapon: Weapon.Katana()),
                TestBuilders.Warrior(2, "Öbürü", aggression: 100, weapon: Weapon.Katana()),
            ],
            [
                TestBuilders.Warrior(101, "Sağlam", health: 4000),

                // Aynı darbe bunda çok daha büyük bir oran açar: yara ağırlığı orana
                // bakar, mutlak hasara değil.
                TestBuilders.Warrior(102, "Cılız", health: 400),
            ])
        {
            Tuning = Quiet with
            {
                MaxBattleSeconds = 12,
                TargetStickiness = 0,
                TargetCrowdPenalty = 0,
                TargetWoundedWeight = 10_000,

                // Fırsat penceresi burada denklemden çıkarılır: kendi testi var
                // (ADistantWoundedEnemyIsNotWorthTheWalk), ve açık bırakılırsa iki
                // savaşçı da yalnızca önündeki düşmanı görür.
                TargetOpportunityRange = 10_000,
            },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        CombatantSnapshot weakest = battle.Snapshots()
            .Where(s => s.Team != 0)
            .OrderBy(s => s.Health / s.MaxHealth)
            .First();

        WarriorId[] late = [.. battle.Events.OfType<AttackStarted>()
            .Where(a => a.Attacker == new WarriorId(1) || a.Attacker == new WarriorId(2))
            .TakeLast(4)
            .Select(a => a.Defender)];

        Assert.NotEmpty(late);
        Assert.All(late, t => Assert.Equal(weakest.Id, t));
    }

    /// <summary>Fırsat penceresi: uzaktaki yaralı, yanındaki sağlamı geçemez.</summary>
    /// <remarks>
    /// Pencere olmasaydı savaşçı önündeki düşmanı bırakıp arenayı kat eder ve yol boyunca
    /// bedava vuruş yerdi. Ölçüldü: sınırsız yara ağırlığı kuralı düz bir zorluk artışına
    /// çeviriyordu (docs/GDD.md §4).
    /// </remarks>
    [Fact]
    public void ADistantWoundedEnemyIsNotWorthTheWalk()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Seçen", aggression: 100, weapon: Weapon.Katana())],
            [
                TestBuilders.Warrior(101, "Yakın", health: 100),
                TestBuilders.Warrior(102, "Uzak", health: 100),
            ])
        {
            Tuning = Quiet with { StartOffsetX = 30 },
        };

        var battle = new Battle(setup, new FixedRandom(0.999));
        battle.Step();
        PushAway(battle, new WarriorId(102), by: 600);

        Assert.Equal(new WarriorId(101), FirstTargetOf(battle, new WarriorId(1)));
    }

    /// <summary>Kuşamı dağılmış bölge hedefi çeker.</summary>
    /// <remarks>
    /// Zırh yıpranmasının dövüş içi karşılığı burada kapanır: parçası dağılan düşman
    /// yalnızca daha çok hasar almaz, aynı zamanda <b>daha çok dikkat</b> çeker.
    /// </remarks>
    [Fact]
    public void ABareSpotDrawsTheBlade()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, "Biri", aggression: 100, weapon: Weapon.Katana()),
                TestBuilders.Warrior(2, "Öbürü", aggression: 100, weapon: Weapon.Katana()),
            ],
            [
                // Çıplak tarafın dağılacak parçası yoktur (havuzu sıfır): dağılmanın
                // hangi savaşçıda gerçekleştiği testin kurulumundan bilinsin diye.
                TestBuilders.Warrior(101, "Çıplak", health: 4000, armor: Armor.None()),
                TestBuilders.Warrior(102, "Kuşanan", health: 4000, armor: Armor.Light()),
            ])
        {
            Tuning = Quiet with
            {
                MaxBattleSeconds = 12,

                // Kırılgan kuşam: dağılma anı testin içinde gerçekleşsin diye.
                ArmorDurabilityScale = 0.02,
                TargetStickiness = 0,
                TargetCrowdPenalty = 0,
                TargetWoundedWeight = 0,
                TargetExposedWeight = 10_000,
                TargetOpportunityRange = 10_000,
            },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        CombatantSnapshot bare = battle.Snapshots()
            .Where(s => s.Team != 0)
            .OrderByDescending(s => s.DestroyedArmor.Count())
            .First();

        Assert.NotEqual(HitLocationSet.None, bare.DestroyedArmor);

        WarriorId[] late = [.. battle.Events.OfType<AttackStarted>()
            .Where(a => a.Attacker == new WarriorId(1) || a.Attacker == new WarriorId(2))
            .TakeLast(4)
            .Select(a => a.Defender)];

        Assert.All(late, t => Assert.Equal(bare.Id, t));
    }

    /// <summary>Yapışkanlık: hiçbir şey değişmezken hedef değişmez.</summary>
    /// <remarks>
    /// Yapışkanlık olmasaydı iki düşman arasında kalan savaşçı her karar adımında yön
    /// değiştirir, hiçbirine varamazdı — hedef değiştirmenin bedeli boşa giden yoldur.
    /// </remarks>
    [Fact]
    public void AWarriorDoesNotThrashBetweenEqualEnemies()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Seçen", aggression: 100, weapon: Weapon.Katana())],
            [
                TestBuilders.Warrior(101, "Eş-1", health: 4000),
                TestBuilders.Warrior(102, "Eş-2", health: 4000),
            ])
        {
            Tuning = Quiet with { MaxBattleSeconds = 12 },
        };

        var battle = new Battle(setup, new FixedRandom(0.999));
        battle.Run();

        WarriorId[] targets = [.. battle.Events.OfType<AttackStarted>()
            .Where(a => a.Attacker == new WarriorId(1))
            .Select(a => a.Defender)];

        Assert.NotEmpty(targets);
        Assert.All(targets, t => Assert.Equal(targets[0], t));
    }

    /// <summary>Kalabalık cezası: takım aynı düşmanın üstüne yığılmaz.</summary>
    [Fact]
    public void ATeamSpreadsInsteadOfPiling()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, "Biri", aggression: 100, weapon: Weapon.Katana()),
                TestBuilders.Warrior(2, "Öbürü", aggression: 100, weapon: Weapon.Katana()),
            ],
            [
                TestBuilders.Warrior(101, "Düşman-1", health: 4000),
                TestBuilders.Warrior(102, "Düşman-2", health: 4000),
            ])
        {
            Tuning = Quiet with { MaxBattleSeconds = 12, TargetCrowdPenalty = 1000 },
        };

        var battle = new Battle(setup, new FixedRandom(0.999));
        battle.Run();

        WarriorId? first = battle.Events.OfType<AttackStarted>()
            .Where(a => a.Attacker == new WarriorId(1))
            .Select(a => (WarriorId?)a.Defender)
            .FirstOrDefault();

        WarriorId? second = battle.Events.OfType<AttackStarted>()
            .Where(a => a.Attacker == new WarriorId(2))
            .Select(a => (WarriorId?)a.Defender)
            .FirstOrDefault();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }

    /// <summary>Ölen hedef bırakılır — kuralın en eski hâli hâlâ geçerli.</summary>
    [Fact]
    public void ADeadEnemyIsDropped()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Seçen", aggression: 100, weapon: TestBuilders.Executioner())],
            [
                TestBuilders.Warrior(101, "Kırılgan", health: 30),
                TestBuilders.Warrior(102, "Dayanıklı", health: 4000),
            ])
        {
            Tuning = Quiet with { MaxBattleSeconds = 12 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        Assert.Contains(battle.Events.OfType<WarriorDied>(), d => d.Warrior == new WarriorId(101));
        Assert.Contains(
            battle.Events.OfType<AttackStarted>(),
            a => a.Attacker == new WarriorId(1) && a.Defender == new WarriorId(102));
    }

    /// <summary>Hedefi uzağa taşımanın tek yolu: dövüşü onu uzaklaştıracak kadar sürdürmek.</summary>
    private static void PushAway(Battle battle, WarriorId id, double by)
    {
        for (int i = 0; i < 200 && !battle.IsFinished; i++)
        {
            CombatantSnapshot s = battle.SnapshotOf(id);

            if (s.Position.X >= by)
            {
                return;
            }

            battle.Step();
        }
    }
}
