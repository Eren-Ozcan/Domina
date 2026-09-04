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

    /// <summary>Hafif ve hızlı silah: öldürmeden ilk kanı akıtır.</summary>
    /// <remarks>
    /// "Çek" tuşu ilk isabete kadar kapalı (GDD §5). Kaçışın mekaniğini ölçen testlerin
    /// önce savaşı başlatması gerekiyor; kaçacak savaşçının kendi vuruşu bunun en ucuz
    /// yolu — kimsenin canını riske atmadan tuşu açar.
    /// </remarks>
    private static Weapon Quick { get; } =
        new("Test-Tantō", WeaponClass.Cutting, 12, TwoHanded: false, AttackSeconds: 0.4);

    /// <summary>Hiç hasar vermeyen silah: savaşı başlatır, kimseyi yaralamaz.</summary>
    private static Weapon Harmless { get; } =
        new("Test-Sopa", WeaponClass.Blunt, 0, TwoHanded: false, AttackSeconds: 0.4);

    /// <summary>İki savaşçı arasındaki hat üstü mesafe.</summary>
    private static double Gap(Battle battle) => Math.Abs(
        battle.SnapshotOf(new WarriorId(1)).Position.X
        - battle.SnapshotOf(new WarriorId(101)).Position.X);

    /// <summary>İlk isabete kadar adımlar, sonra tuşa basar ve kaçışın başladığı anı döner.</summary>
    private static double PressAfterFirstBlood(Battle battle)
    {
        while (!battle.ContactMade && battle.Step())
        {
        }

        battle.CommandRetreat();

        for (int i = 0; i < 400 && !battle.Events.Any(e => e is RetreatStarted); i++)
        {
            if (!battle.Step())
            {
                break;
            }
        }

        return battle.Events.OfType<RetreatStarted>().First().AtSeconds;
    }

    [Fact]
    public void AFasterWarriorCatchesUpWithASlowerOne()
    {
        // Aynı kadro, tek fark hız. Yavaş kovalayan yetişemez, hızlı olan yetişir.
        Assert.False(CaughtUp(hunterSpeed: 5));
        Assert.True(CaughtUp(hunterSpeed: 100));
    }

    /// <remarks>
    /// Ölçülen şey <b>kaçış başladıktan sonra</b> yenen darbe. İlk isabet zaten tuşu açan
    /// darbedir ve iki kurulumda da düşer; kovalamacayı ayıran, aradaki mesafenin
    /// kapanıp kapanmadığıdır.
    /// </remarks>
    private static bool CaughtUp(double hunterSpeed)
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kaçan", health: 900, aggression: 0, speed: 50)],
            [TestBuilders.Warrior(101, "Kovalayan", health: 400, aggression: 100, speed: hunterSpeed)])
        {
            Tuning = NoMishap,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        double left = PressAfterFirstBlood(battle);
        battle.Run();

        return battle.Events
            .OfType<AttackLanded>()
            .Any(e => e.Defender == new WarriorId(1) && e.AtSeconds > left);
    }

    /// <summary>Bacağını kaybeden savaşçı yalnızca kaçınmayı değil kaçabilmeyi de kaybeder.</summary>
    [Fact]
    public void LosingALegCostsSpeedToo()
    {
        Warrior lame = TestBuilders.Warrior(1, speed: 80);
        double before = lame.EffectiveStats.Speed;

        lame.AddDisability(BodyPart.RightLeg);

        Assert.True(lame.EffectiveStats.Speed < before);
    }

    /// <summary>Kaçan sırtı dönük koştuğu için kovalayandan yavaştır.</summary>
    [Fact]
    public void RetreatingIsSlowerThanChasing()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kaçan", health: 900, aggression: 0, speed: 50)],
            [TestBuilders.Warrior(101, "Kovalayan", health: 400, aggression: 100, speed: 50)])
        {
            Tuning = NoMishap with { StartOffsetX = 60 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        PressAfterFirstBlood(battle);

        // Ölçülen şey adım hızı: aynı Speed değerine sahip iki savaşçıdan sırtı dönük
        // koşan daha yavaş ilerler. Darbeye bakmak yanıltıcı olurdu — kovalayan vuruş
        // yaptığı sürece duruyor ve net olarak geride kalabiliyor (bkz. Battle.CanAdvanceOn).
        var compared = false;

        for (int i = 0; i < 200 && battle.Step(); i++)
        {
            CombatantSnapshot fleeing = battle.SnapshotOf(new WarriorId(1));
            CombatantSnapshot chasing = battle.SnapshotOf(new WarriorId(101));

            if (fleeing.State != CombatState.Retreating || chasing.Speed <= 0)
            {
                continue;
            }

            Assert.True(
                fleeing.Speed < chasing.Speed,
                $"Kaçan yavaşlamadı: {fleeing.Speed} vs {chasing.Speed}");

            compared = true;
            break;
        }

        Assert.True(compared, "Kovalamacanın ölçülebildiği bir tick bulunamadı.");
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

        // İlk mermi hedefi bulur ve tuşu açar; ikincisi kaçarken arkadan gelir.
        PressAfterFirstBlood(battle);
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

        // İlk mermi hedefi bulur — tuşu açan da odur. Ölçülen, ondan SONRAKİ mermi.
        PressAfterFirstBlood(battle);
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
            // Tuşu açan temas zararsız: karşı taraf sıfır hasarlı bir silahla vuruyor
            // (MinimumDamage da 0). Ölçülen tek şey kaçış zarı olsun diye — savaşın
            // başlamış olması ön koşul, ölçülen değer değil.
            var setup = new BattleSetup(
                [TestBuilders.Warrior(1, "Kaçan", health: 4, aggression: 0)],
                [
                    TestBuilders.Warrior(
                        101, "Yavaş", health: 400, aggression: 100, speed: 1,
                        weapon: Harmless),
                ])
            {
                Tuning = TestBuilders.PointBlank with
                {
                    EscapeMishapChance = 1.0,
                    MinimumDamage = 0,
                },
            };

            var battle = new Battle(setup, new Domina.Core.Rng.SeededRandom(seed));
            while (!battle.ContactMade && battle.Step()) { }
            if (seed == 6) { foreach (var ev in battle.Events) Console.WriteLine($"DBG {ev.AtSeconds:F2} {ev}"); }
            PressAfterFirstBlood(battle);
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
        // Temas uzaktan kurulur: yakın dövüşte çekilmek bedava vuruş demek olurdu ve
        // "tertemiz çıkış" ölçülemezdi.
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(
                    1, "Kaçan", health: 100, aggression: 100, accuracy: 100,
                    thrown: ThrownWeapon.Shuriken()),
            ],
            [TestBuilders.Warrior(101, "Yavaş", health: 400, aggression: 0, speed: 1)])
        {
            Tuning = NoMishap with { StartOffsetX = 400 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        PressAfterFirstBlood(battle);
        BattleResult result = battle.Run();

        Assert.DoesNotContain(battle.Events, e => e is EscapeMishap);
        Assert.Equal(100, result.SummaryFor(new WarriorId(1)).HealthRemaining);
    }
}
