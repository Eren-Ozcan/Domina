using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Blok, savunmanın kaçınmadan ayrı ikinci eksenidir: kaçınma darbeyi ıskalatır ve orada
/// biter, blok darbeyi <b>karşılar</b> — hasar düşer, uzuv kopmaz, ama darbe gelmiştir ve
/// duruşta geçen süre vurulmayan vuruştur. Bu testler kararın kaynağını (Savunma statı),
/// duruşun bedelini (saldırı döngüsü) ve tuttuğu ile tutmadığı şeyi bağlar.
/// </summary>
public class BlockTests
{
    /// <summary>Bloğun izole edildiği ayar: kopma, sersemletme ve düşürme kapalı.</summary>
    private static CombatTuning BlockOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        BaseDisarmChance = 0,
        CatchDisarmChance = 0,
        MaxBattleSeconds = 20,
    };

    private static Weapon Blade { get; } =
        new("Test-Katana", WeaponClass.Cutting, 20, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>Blok kalitesi 1.0 olan silah — kaliteyi denklemden çıkarır.</summary>
    private static Weapon Guardpole { get; } =
        new("Test-Naginata", WeaponClass.Cutting, 20, TwoHanded: true, AttackSeconds: 1.0);

    /// <param name="defense">Savunanın Savunma statı — blok zarının tek kaynağı.</param>
    private static BattleSetup Bout(
        double defense,
        CombatTuning? tuning = null,
        Weapon? defenderWeapon = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Karşılayan",
                health: 4000,
                aggression: 0,
                defense: defense,
                weapon: defenderWeapon ?? Guardpole),
        ],
        [TestBuilders.Warrior(101, "Vuran", health: 4000, aggression: 100, weapon: Blade)])
    {
        Tuning = tuning ?? BlockOnly,
    };

    /// <summary>Savunma 0 olan savaşçı hiç bloklamaz.</summary>
    /// <remarks>
    /// Kaçınmayla aynı şekil: taban şans yok. Kural bu yüzden kendi kendini sınırlar —
    /// bloğu ölçmeyen testler statı sıfırlayarak duruşu kapatabilir.
    /// </remarks>
    [Fact]
    public void AWarriorWithNoDefenseNeverRaisesAGuard()
    {
        var battle = new Battle(Bout(defense: 0), new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<BlockRaised>());
        Assert.Empty(battle.Events.OfType<AttackBlocked>());
    }

    /// <summary>Duruş, gelen darbeyi karşılar: hasar düşer ve olay ayrı akar.</summary>
    [Fact]
    public void AGuardedBlowLandsSofterThanAnOpenOne()
    {
        var guarded = new Battle(Bout(defense: 100), new FixedRandom(0.0));
        guarded.Run();

        var open = new Battle(Bout(defense: 0), new FixedRandom(0.0));
        open.Run();

        AttackBlocked[] blocked = [.. guarded.Events.OfType<AttackBlocked>()];
        Assert.NotEmpty(blocked);

        double openDamage = open.Events.OfType<AttackLanded>()
            .First(a => a.Defender == new WarriorId(1)).Damage;

        // Karşılanan darbe silinmez, hafifler: BlockDamageReduction 0.70, silahın blok
        // kalitesi 1.0 — geriye açık darbenin onda üçü kalır.
        Assert.All(blocked, b => Assert.True(
            b.Damage < openDamage,
            $"Bloklanan darbe {b.Damage:F2}, açık darbe {openDamage:F2}."));
    }

    /// <summary>Bloklanan darbe uzuv koparmaz — Savunma statının verdiği tek kesin söz.</summary>
    [Fact]
    public void AGuardedBlowCannotTakeALimb()
    {
        CombatTuning alwaysSevers = BlockOnly with { BaseDismembermentChance = 1.0 };

        var battle = new Battle(Bout(defense: 100, alwaysSevers), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        Assert.NotEmpty(battle.Events.OfType<AttackBlocked>());

        // Kopma zarı her vuruşta tutuyor; bloklananların hiçbiri uzuv götüremez.
        var blockedAt = battle.Events.OfType<AttackBlocked>().Select(b => b.AtSeconds).ToHashSet();
        var severedAt = battle.Events.OfType<WarriorDismembered>()
            .Where(d => d.Warrior == new WarriorId(1))
            .Select(d => d.AtSeconds);

        Assert.All(severedAt, at => Assert.DoesNotContain(at, blockedAt));
        Assert.NotNull(result);
    }

    /// <summary>Künt silah bloğun içinden geçer: sarsıntı payı duruşa rağmen işler.</summary>
    /// <remarks>
    /// Kalkan yokken künt sınıfın dördüncü kazancı budur. Blok künte de tam işleseydi
    /// savunmacı savaşçının önünde künt silahın tek karşılığı silinirdi.
    /// </remarks>
    [Fact]
    public void AGuardStopsSteelButNotTheShock()
    {
        // Sersemletme eşiği sıfırlanır: sınanan şey ağır darbenin ne olduğu değil,
        // bloklanan darbenin sarsıntı payını taşımaya devam ettiği.
        CombatTuning stunOnly = BlockOnly with
        {
            BaseStunChance = 1.0,
            StunSeverityThreshold = 0,

            // Kısa sersemleme: uzun olsaydı savunan iki darbe arasında duruşa geçecek
            // fırsat bulamaz, test sersemletmeyi değil sersemletme kilidini ölçerdi.
            StunSeconds = 0.2,
            MaxBattleSeconds = 10,
        };

        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Karşılayan", health: 4000, aggression: 0, defense: 100)],
            [
                TestBuilders.Warrior(
                    101,
                    "Sopalı",
                    health: 4000,
                    aggression: 100,
                    weapon: new Weapon("Test-Tetsubo", WeaponClass.Blunt, 30, TwoHanded: true, AttackSeconds: 2.0)),
            ])
        {
            Tuning = stunOnly,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        AttackBlocked[] blocked = [.. battle.Events.OfType<AttackBlocked>()];
        Assert.NotEmpty(blocked);

        // Karşılanan darbelerden en az biri sersemletmiş olmalı: duruş çeliği durdurur,
        // sarsıntıyı durdurmaz.
        var stunnedAt = battle.Events.OfType<WarriorStunned>().Select(s => s.AtSeconds).ToHashSet();
        Assert.Contains(blocked, b => stunnedAt.Contains(b.AtSeconds));
    }

    /// <summary>Bloğun bedeli: duruşta geçen süre vurulmayan vuruştur.</summary>
    [Fact]
    public void AGuardIsPaidForWithSwings()
    {
        var guarded = new Battle(Bout(defense: 100), new FixedRandom(0.0));
        BattleResult guardedResult = guarded.Run();

        var open = new Battle(Bout(defense: 0), new FixedRandom(0.0));
        BattleResult openResult = open.Run();

        int guardedSwings = guardedResult.SummaryFor(new WarriorId(1)).AttacksMade;
        int openSwings = openResult.SummaryFor(new WarriorId(1)).AttacksMade;

        Assert.True(
            guardedSwings < openSwings,
            $"Bloklayan {guardedSwings}, hiç bloklamayan {openSwings} vuruş yaptı.");
    }

    /// <summary>Blok arkasına blok gelmez: duruş bir ritme bağlıdır.</summary>
    /// <remarks>
    /// Zar her karar adımında yeniden atılsaydı savunması yüksek savaşçı arka arkaya
    /// bloklayıp hiç vurmayabilirdi — dövüş kilitlenirdi.
    /// </remarks>
    [Fact]
    public void AGuardCannotFollowAGuard()
    {
        var battle = new Battle(Bout(defense: 100), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        // FixedRandom(0.0) her zarı tutturur: kural olmasaydı savaşçı ilk duruştan sonra
        // hiç vurmazdı.
        Assert.True(result.SummaryFor(new WarriorId(1)).AttacksMade > 0);
        Assert.NotEmpty(battle.Events.OfType<BlockRaised>());
    }

    /// <summary>Duruşun ne kadar tuttuğu elindeki silahtan okunur.</summary>
    /// <remarks>
    /// Silahını düşüren savaşçı bloğunu da kaybeder: yumruğun blok kalitesi 0.30.
    /// </remarks>
    [Fact]
    public void TheWeaponInHandDecidesHowMuchTheGuardHolds()
    {
        var withPole = new Battle(Bout(defense: 100), new FixedRandom(0.0));
        withPole.Run();

        var withFists = new Battle(
            Bout(defense: 100, defenderWeapon: Weapon.Fists()),
            new FixedRandom(0.0));
        withFists.Run();

        double poleDamage = withPole.Events.OfType<AttackBlocked>().First().Damage;
        double fistDamage = withFists.Events.OfType<AttackBlocked>().First().Damage;

        Assert.True(
            poleDamage < fistDamage,
            $"Sap {poleDamage:F2}, yumruk {fistDamage:F2} geçirdi — kalite ters çalışıyor.");
    }

    /// <summary>Arkadan gelen vuruş bloklanamaz.</summary>
    [Fact]
    public void AGuardFacesOnlyForward()
    {
        Assert.Equal(0.30, Weapon.Fists().BlockFactor);
        Assert.Equal(1.0, Guardpole.BlockFactor);

        // Yakalama aleti bloğa ayrıca kayırılmaz: tek elli künt bir alettir, kalitesi de
        // odur. Kayırıldığında ölçüm kilitli bir freni kırıyordu — ağır silah taşıyan
        // düşmanın önünde jitte yanlış seçim olmaktan çıkıyordu (docs/GDD.md §5).
        Assert.Equal(0.85, Weapon.Jitte().BlockFactor);
    }
}
