using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Arena artık bir <b>düzlem</b>: savaşçılar yürür, silahın menzili vardır, ve
/// çevrilmek gerçek bir tehlikedir. Bu testler uzamın vaat ettiği şeyleri bağlar.
/// </summary>
public class MovementTests
{
    private static BattleSetup Approach(Weapon? playerWeapon = null) => new(
        [TestBuilders.Warrior(1, health: 400, weapon: playerWeapon)],
        [TestBuilders.Warrior(101, health: 400)]);

    private static void StepMany(Battle battle, int steps)
    {
        for (int i = 0; i < steps && battle.Step(); i++)
        {
            // Step() bitiş koşulunu kendi kontrol eder.
        }
    }

    /// <summary>Karşılıklı duran iki savaşçı birbirine yürür.</summary>
    [Fact]
    public void WarriorsWalkTowardEachOther()
    {
        var battle = new Battle(Approach(), new SeededRandom(7));

        double startGap = Gap(battle);
        StepMany(battle, 10);

        Assert.True(Gap(battle) < startGap, "Savaşçılar yaklaşmadı.");
    }

    /// <summary>
    /// Menzil dışındayken saldırı başlamaz; savaşçı önce yaklaşmak zorundadır.
    /// </summary>
    [Fact]
    public void NoOneSwingsFromOutOfReach()
    {
        var battle = new Battle(Approach(), new SeededRandom(7));

        // İlk tick'te taraflar 960 birim uzakta — hiçbir silah oraya erişemez.
        battle.Step();

        Assert.DoesNotContain(battle.Events, e => e is AttackStarted);
        Assert.Equal(CombatState.Idle, battle.SnapshotOf(new WarriorId(1)).State);
    }

    /// <summary>
    /// Uzun silah daha uzakta durur. Menzil olmasaydı naginata ile tantō arasındaki
    /// fark yalnızca hasar ve hız olurdu.
    /// </summary>
    [Fact]
    public void LongerWeaponsStopFartherAway()
    {
        var withSpear = new Battle(Approach(Weapon.Yari()), new SeededRandom(3));
        var withBlade = new Battle(Approach(Weapon.Katana()), new SeededRandom(3));

        StepUntilSwinging(withSpear);
        StepUntilSwinging(withBlade);

        Assert.True(
            Gap(withSpear) > Gap(withBlade),
            "Mızraklı savaşçı kılıçlıdan daha uzaktan vurmalı.");
    }

    /// <summary>Savaşçılar üst üste binmez.</summary>
    [Fact]
    public void WarriorsKeepTheirPersonalSpace()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400),
                TestBuilders.Warrior(2, health: 400),
                TestBuilders.Warrior(3, health: 400),
            ],
            [TestBuilders.Warrior(101, health: 400)]);

        var battle = new Battle(setup, new SeededRandom(11));
        StepMany(battle, 120);

        IReadOnlyList<CombatantSnapshot> snapshots = battle.Snapshots();
        for (int i = 0; i < snapshots.Count; i++)
        {
            for (int j = i + 1; j < snapshots.Count; j++)
            {
                if (!snapshots[i].IsActive || !snapshots[j].IsActive)
                {
                    continue;
                }

                double gap = snapshots[i].Position.DistanceTo(snapshots[j].Position);
                Assert.True(gap > 1, $"İki savaşçı üst üste bindi: {gap:F1}");
            }
        }
    }

    /// <summary>
    /// Kaçan savaşçı <b>menzilindeki her düşmandan</b> bedava vuruş yer. Kuşatmanın
    /// bedeli budur: çevrildiysen çekilmek üç darbe demektir.
    /// </summary>
    [Fact]
    public void BeingSurroundedMakesRetreatCostMore()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 900)],
            [
                TestBuilders.Warrior(101, health: 400),
                TestBuilders.Warrior(102, health: 400),
                TestBuilders.Warrior(103, health: 400),
            ])
        {
            // Üçü de kaçanın menzilinde: kuşatılmış hâl.
            Tuning = TestBuilders.PointBlank with { StartSpacingY = 25 },
        };

        var battle = new Battle(setup, new SeededRandom(5));

        // Tuş ilk isabete kadar kapalı (GDD §5); kuşatmanın bedeli ondan sonra ölçülür.
        while (!battle.ContactMade && battle.Step())
        {
        }

        battle.CommandRetreat();
        StepMany(battle, 10);

        int swings = battle.Events.OfType<OpportunityAttack>().Count();
        Assert.True(swings > 1, $"Çevrilmiş savaşçı tek bedava vuruşla kurtuldu: {swings}");
    }

    /// <summary>
    /// Hedef hamle sırasında menzilden çıkarsa kılıç boşluğa iner — vuruşa
    /// kilitlenmenin bedeli.
    /// </summary>
    [Fact]
    public void ASwingAtAFleeingTargetCanWhiff()
    {
        // Kaçan hedefi kovalayan uzun bir dövüşte er ya da geç ıska olur.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 900, aggression: 100)],
            [TestBuilders.Warrior(101, health: 900, aggression: 100)])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new SeededRandom(2));
        StepMany(battle, 20);

        while (!battle.ContactMade && battle.Step())
        {
        }

        battle.CommandRetreat();
        StepMany(battle, 60);

        Assert.Contains(battle.Events, e => e is AttackMissed or OpportunityAttack);
    }

    private static void StepUntilSwinging(Battle battle)
    {
        for (int i = 0; i < 400; i++)
        {
            if (battle.SnapshotOf(new WarriorId(1)).State == CombatState.AttackWindup)
            {
                return;
            }

            if (!battle.Step())
            {
                return;
            }
        }
    }

    private static double Gap(Battle battle) =>
        battle.SnapshotOf(new WarriorId(1)).Position
            .DistanceTo(battle.SnapshotOf(new WarriorId(101)).Position);
}
