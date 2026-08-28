using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Hücum (GDD §4). Kuralın tamamı tek bir takasa dayanır: mesafeyi hızla kapatırsın,
/// karşılığında savunmayı bırakırsın. Buradaki testler takasın iki ucunu da bağlar —
/// ödül olmadan hücum bir intihar, bedel olmadan bedava bir hız bonusudur.
/// </summary>
public class ChargeTests
{
    private static readonly WarriorId _fighter = new(1);
    private static readonly WarriorId _enemy = new(101);

    /// <summary>Taraflar uzakta başlar; zar her zaman hücumu seçer.</summary>
    private static CombatTuning AlwaysCharges { get; } = CombatTuning.Default with
    {
        ChargeChance = 1.0,
        ChargeMinDistance = 200,
    };

    private static CombatTuning NeverCharges { get; } = CombatTuning.Default with
    {
        ChargeChance = 0.0,
    };

    private static BattleSetup Duel(CombatTuning tuning, double playerHealth = 400) => new(
        [TestBuilders.Warrior(1, health: playerHealth)],
        [TestBuilders.Warrior(101, health: 400)])
    {
        Tuning = tuning,
    };

    private static bool StepUntil(Battle battle, Func<Battle, bool> predicate, int maxSteps = 2000)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            if (predicate(battle))
            {
                return true;
            }

            if (!battle.Step())
            {
                return predicate(battle);
            }
        }

        return false;
    }

    /// <summary>Mesafe uygunsa hücum başlar ve savaşçı gerçekten hızlanır.</summary>
    [Fact]
    public void ChargingClosesTheGapFasterThanWalking()
    {
        var charging = new Battle(Duel(AlwaysCharges), new SeededRandom(7));
        var walking = new Battle(Duel(NeverCharges), new SeededRandom(7));

        Assert.True(StepUntil(charging, b => b.Events.OfType<ChargeStarted>().Any()));

        // Aynı sayıda tick sonra hücum eden daha çok yol almış olmalı.
        for (int i = 0; i < 20; i++)
        {
            charging.Step();
            walking.Step();
        }

        double chargedGap = Gap(charging);
        double walkedGap = Gap(walking);

        Assert.True(
            chargedGap < walkedGap,
            $"Hücum yürüyüşten hızlı değil: {chargedGap:F1} >= {walkedGap:F1}");
    }

    /// <summary>
    /// Hücumun bedeli: koşan savaşçı kaçınamaz, bloklayamaz — kaçış penceresiyle aynı
    /// savunmasızlık.
    /// </summary>
    [Fact]
    public void TheChargingWarriorCannotDefend()
    {
        // Kaçınması yüksek bir savaşçı bile hücum ederken kaçınamaz.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 400, evasion: 100)],
            [TestBuilders.Warrior(101, health: 400)])
        {
            Tuning = AlwaysCharges,
        };

        var battle = new Battle(setup, new SeededRandom(3));
        Assert.True(StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.Charging));

        Assert.DoesNotContain(
            battle.Events.OfType<AttackDodged>(),
            e => e.Defender == _fighter);
    }

    /// <summary>
    /// Varıştaki ilk vuruş momentum taşır: aynı seed'de yalnızca çarpanı büyütmek
    /// hasarı büyütür.
    /// </summary>
    [Fact]
    public void ArrivingWithMomentumHitsHarder()
    {
        double plain = FirstBlowAfterCharge(1.0);
        double heavy = FirstBlowAfterCharge(2.0);

        Assert.True(heavy > plain, $"Hücum bonusu hasara yansımıyor: {heavy:F2} <= {plain:F2}");
    }

    private static double FirstBlowAfterCharge(double multiplier)
    {
        var battle = new Battle(
            Duel(AlwaysCharges with { ChargeDamageMultiplier = multiplier }),
            new SeededRandom(11));

        Assert.True(StepUntil(battle, b => b.Events.OfType<ChargeConnected>().Any()));

        ChargeConnected arrival = battle.Events.OfType<ChargeConnected>().First();

        // Aranan şey varıştan sonraki ilk vuruş değil, varanın vurduğu ilk vuruş:
        // aradaki farkı karşı tarafın darbeleri doldurabilir.
        Assert.True(StepUntil(
            battle,
            b => b.Events.OfType<AttackLanded>()
                .Any(e => e.AtSeconds >= arrival.AtSeconds && e.Attacker == arrival.Warrior)));

        return battle.Events.OfType<AttackLanded>()
            .First(e => e.AtSeconds >= arrival.AtSeconds && e.Attacker == arrival.Warrior)
            .Damage;
    }

    /// <summary>Hedefe varılamazsa hamle boşa gider — süre sınırı hücumu bitirir.</summary>
    [Fact]
    public void AChargeThatNeverArrivesIsWasted()
    {
        var battle = new Battle(
            Duel(AlwaysCharges with { ChargeMaxSeconds = 0.2 }),
            new SeededRandom(5));

        Assert.True(StepUntil(battle, b => b.Events.OfType<ChargeMissed>().Any()));
        Assert.DoesNotContain(battle.Events.OfType<ChargeConnected>(), _ => true);
    }

    /// <summary>
    /// "Çek" komutu hücumu keser. Hücum kendi kararlarına karşı taahhütlüdür ama
    /// oyuncunun komutu ayrı bir eksendir — kesilemez olsaydı komut anında koşan
    /// savaşçı düşman hattına varmak zorunda kalır ve GDD §5'in merdiveni ters dönerdi.
    /// </summary>
    [Fact]
    public void TheRetreatCommandCutsTheChargeShort()
    {
        // Tuş ilk temasa kadar kapalı olduğu için (GDD §5) hâlâ koşmakta olan bir
        // savaşçıya ihtiyaç var: hızlı savaşçı teması açar, ağır olan arkadan hücumda
        // yakalanır.
        var laggard = new WarriorId(2);
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400, speed: 100),
                TestBuilders.Warrior(2, health: 400, speed: 0),
            ],
            [TestBuilders.Warrior(101, health: 400)])
        {
            Tuning = AlwaysCharges,
        };

        var battle = new Battle(setup, new SeededRandom(9));

        Assert.True(StepUntil(
            battle,
            b => b.ContactMade && b.SnapshotOf(laggard).State == CombatState.Charging));

        Assert.True(battle.CommandRetreat());

        Assert.Equal(CombatState.Retreating, battle.SnapshotOf(laggard).State);
        Assert.Contains(battle.Events.OfType<ChargeMissed>(), e => e.Warrior == laggard);
        Assert.DoesNotContain(battle.Events.OfType<ChargeConnected>(), e => e.Warrior == laggard);
    }

    /// <summary>
    /// Kaçan hedefe hücum edilmez. Ölçüldü: edilirse 1.6 kat hız kaçışın tek ayar
    /// düğmesini devre dışı bırakıyor ve kovalamaca dengesi (GDD §5) çöküyor.
    /// </summary>
    [Fact]
    public void NobodyChargesAFleeingTarget()
    {
        var battle = new Battle(Duel(AlwaysCharges), new SeededRandom(4));

        Assert.True(StepUntil(battle, b => b.ContactMade));
        Assert.True(battle.CommandRetreat());
        Assert.True(StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.Retreating));

        int before = battle.Events.OfType<ChargeStarted>().Count(e => e.Warrior == _enemy);

        StepUntil(battle, b => b.IsFinished);

        int after = battle.Events.OfType<ChargeStarted>().Count(e => e.Warrior == _enemy);
        Assert.Equal(before, after);
    }

    private static double Gap(Battle battle) =>
        battle.SnapshotOf(_fighter).Position.DistanceTo(battle.SnapshotOf(_enemy).Position);
}

