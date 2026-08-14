using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Kaçışı bedelsiz olmaktan çıkaran üç mekanik: <b>hız</b> (yetişen düşman),
/// <b>fırlatma</b> (arkadan gelen mermi) ve <b>kaçış zarı</b> (kimsenin vurmadığı yara).
/// </summary>
/// <remarks>
/// Üçü birden eklendi çünkü hiçbiri tek başına yetmiyordu: hız tek sabitken kovalayan
/// kaçana yetişemiyor, yakın dövüş arenanın uzak yarısına ulaşamıyor, ve temastan önce
/// basılan tuş %100 temiz çıkış veriyordu (ölçüldü, 20.000 dövüş).
/// </remarks>
public class RangedAndFlightTests
{
    /// <summary>Zar hiç tutmayan kaynak: kaza yarası gibi şansa bağlı dalları kapatır.</summary>
    private static CombatTuning NoMishap { get; } =
        TestBuilders.PointBlank with { EscapeMishapChance = 0 };

    [Fact]
    public void AFasterWarriorCatchesUpWithASlowerOne()
    {
        // Aynı kadro, tek fark hız. Yavaş kovalayan yetişemez, hızlı olan yetişir.
        Assert.False(CaughtUp(hunterSpeed: 5));
        Assert.True(CaughtUp(hunterSpeed: 100));
    }

    private static bool CaughtUp(double hunterSpeed)
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kaçan", health: 400, aggression: 0, speed: 50)],
            [TestBuilders.Warrior(101, "Kovalayan", aggression: 100, speed: hunterSpeed)])
        {
            // Menzil dışında başlarlar: yetişebilmek yalnızca hızla mümkün.
            Tuning = NoMishap with { StartOffsetX = 150 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.CommandRetreat();
        battle.Run();

        return battle.Events.OfType<AttackLanded>().Any(e => e.Defender == new WarriorId(1));
    }

    /// <summary>Bacağını kaybeden savaşçı yalnızca kaçınmayı değil kaçabilmeyi de kaybeder.</summary>
    [Fact]
    public void LosingALegCostsSpeedToo()
    {
        Warrior lame = TestBuilders.Warrior(1, speed: 80);
        double before = lame.EffectiveStats.Speed;

        lame.AddDisability(BodyPart.Leg);

        Assert.True(lame.EffectiveStats.Speed < before);
    }

    /// <summary>Kaçan sırtı dönük koştuğu için kovalayandan yavaştır.</summary>
    [Fact]
    public void RetreatingIsSlowerThanChasing()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kaçan", health: 400, aggression: 0, speed: 50)],
            [TestBuilders.Warrior(101, "Kovalayan", aggression: 100, speed: 50)])
        {
            Tuning = NoMishap with { StartOffsetX = 60 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.CommandRetreat();
        battle.Run();

        // Hızlar eşit ama kaçan yavaşlar; aradaki fark kovalayanı menzile sokar.
        Assert.Contains(battle.Events.OfType<AttackLanded>(), e => e.Defender == new WarriorId(1));
    }

    // ------------------------------------------------------------------ fırlatma

    private static BattleSetup Thrower(double startOffset, ThrownWeapon? thrown = null) => new(
        [TestBuilders.Warrior(1, "Hedef", health: 400, aggression: 0, speed: 50)],
        [
            TestBuilders.Warrior(
                101,
                "Atıcı",
                aggression: 100,
                accuracy: 100,
                speed: 1,
                thrown: thrown ?? ThrownWeapon.Shuriken()),
        ])
    {
        Tuning = NoMishap with { StartOffsetX = startOffset },
    };

    /// <summary>
    /// Yakın dövüş menzilinin dışındaki hedefe mermi ulaşır — arenanın uzak yarısı artık
    /// güvenli bölge değil.
    /// </summary>
    [Fact]
    public void AThrownWeaponReachesATargetOutOfMeleeRange()
    {
        var battle = new Battle(Thrower(startOffset: 250), new FixedRandom(0.0));
        battle.Run();

        Assert.Contains(battle.Events, e => e is ProjectileLaunched);
        Assert.Contains(battle.Events, e => e is ProjectileHit);
    }

    /// <summary>Mermi anında çözülmez: havada geçirdiği süre olay akışında görünür.</summary>
    [Fact]
    public void AProjectileSpendsTimeInTheAir()
    {
        var battle = new Battle(Thrower(startOffset: 300), new FixedRandom(0.0));
        battle.Run();

        ProjectileLaunched launched = battle.Events.OfType<ProjectileLaunched>().First();
        ProjectileHit hit = battle.Events.OfType<ProjectileHit>().First();

        Assert.True(launched.FlightSeconds > 0);
        Assert.True(hit.AtSeconds > launched.AtSeconds);
    }

    /// <summary>Mermi biter; bittiğinde savaşçının elinde yalnızca yakın dövüş kalır.</summary>
    /// <remarks>
    /// Hedef kaçıyor: yaklaşsaydı yakın dövüş menziline girer ve atıcı cephanesini
    /// bitirmeden kılıca geçerdi. Menzil de kasıtlı olarak arenadan büyük — ölçülen şey
    /// cephanenin bitmesi, menzilin yetmemesi değil.
    /// </remarks>
    [Fact]
    public void AThrowerRunsOutOfAmmunition()
    {
        ThrownWeapon twoShots = ThrownWeapon.Shuriken() with { Ammo = 2, Range = 4000 };

        var battle = new Battle(Thrower(startOffset: 250, twoShots), new FixedRandom(0.0));
        battle.CommandRetreat();
        battle.Run();

        Assert.Equal(2, battle.Events.OfType<ProjectileLaunched>().Count());
    }

    /// <summary>Uçuş sırasında sahayı terk eden hedefe mermi ulaşamaz.</summary>
    [Fact]
    public void AProjectileMissesATargetThatLeftTheArena()
    {
        // Menzilin ucundan, yavaş bir mermiyle: hedef kaçarken mermi havada kalır.
        ThrownWeapon slow = ThrownWeapon.Shuriken() with { Speed = 60, Range = 900 };

        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kaçan", health: 400, aggression: 0, speed: 100)],
            [
                TestBuilders.Warrior(
                    101, "Atıcı", aggression: 100, accuracy: 100, speed: 1, thrown: slow),
            ])
        {
            Tuning = NoMishap with { StartOffsetX = 420 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.CommandRetreat();
        battle.Run();

        Assert.Contains(battle.Events, e => e is ProjectileLaunched);
        Assert.Contains(battle.Events, e => e is ProjectileMissed);
    }

    // ---------------------------------------------------------------- kaçış zarı

    /// <summary>
    /// Kaçış zarı yaralar ama <b>öldürmez</b>: canı 1'in altına indirmez.
    /// </summary>
    /// <remarks>
    /// Amacı ölüm değil, "hiç bedel ödemeden çıktım" durumunu ortadan kaldırmak. Öldürebilseydi
    /// oyuncuya sebepsiz bir ölüm olarak görünürdü — ekranda vuran kimse yok.
    /// </remarks>
    [Fact]
    public void TheEscapeMishapWoundsButNeverKills()
    {
        var mishaps = 0;

        for (ulong seed = 1; seed <= 200; seed++)
        {
            var setup = new BattleSetup(
                [TestBuilders.Warrior(1, "Kaçan", health: 4, aggression: 0)],
                [TestBuilders.Warrior(101, "Yavaş", aggression: 0, speed: 1)])
            {
                Tuning = TestBuilders.PointBlank with
                {
                    StartOffsetX = 400,
                    EscapeMishapChance = 1.0,
                },
            };

            var battle = new Battle(setup, new Domina.Core.Rng.SeededRandom(seed));
            battle.CommandRetreat();
            BattleResult result = battle.Run();

            WarriorBattleSummary summary = result.SummaryFor(new WarriorId(1));

            Assert.True(summary.Escaped);
            Assert.False(summary.Died);
            Assert.True(summary.HealthRemaining >= 1);

            mishaps += battle.Events.OfType<EscapeMishap>().Count();
        }

        Assert.Equal(200, mishaps);
    }

    /// <summary>Zar kapalıyken çıkış tertemizdir — bedel gerçekten zardan geliyor.</summary>
    [Fact]
    public void WithoutTheMishapRollLeavingIsClean()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kaçan", health: 100, aggression: 0)],
            [TestBuilders.Warrior(101, "Yavaş", aggression: 0, speed: 1)])
        {
            Tuning = NoMishap with { StartOffsetX = 400 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.CommandRetreat();
        BattleResult result = battle.Run();

        Assert.DoesNotContain(battle.Events, e => e is EscapeMishap);
        Assert.Equal(100, result.SummaryFor(new WarriorId(1)).HealthRemaining);
    }
}
