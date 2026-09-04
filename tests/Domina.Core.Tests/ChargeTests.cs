using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Hücum (GDD §4). Kuralın tamamı tek bir takasa dayanır: mesafeyi hızla kapatırsın,
/// karşılığında hamleni açıkta bırakırsın — birikirken kıpırdayamazsın ve yediğin tek
/// isabet hücumu götürür. Buradaki testler takasın iki ucunu da bağlar; ödül olmadan
/// hücum bir intihar, bedel olmadan bedava bir hız bonusudur.
/// </summary>
public class ChargeTests
{
    private static readonly WarriorId _fighter = new(1);
    private static readonly WarriorId _enemy = new(101);

    /// <summary>Taraflar uzakta başlar; zar her zaman hücumu seçer.</summary>
    private static CombatTuning AlwaysCharges { get; } = CombatTuning.Default with
    {
        ChargeChanceAtZeroAggression = 1.0,
        ChargeChanceAtMaxAggression = 1.0,
    };

    private static CombatTuning NeverCharges { get; } = CombatTuning.Default with
    {
        ChargeChanceAtZeroAggression = 0.0,
        ChargeChanceAtMaxAggression = 0.0,
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
    /// <b>Hücum savunmayı kapatmaz.</b> Koşan savaşçı normal oranıyla kaçınmayı sürdürür;
    /// savunmasızlık kaçışa özgüdür (docs/GDD.md §4-§5).
    /// </summary>
    [Fact]
    public void TheChargingWarriorStillDefends()
    {
        // Kaçınması yüksek bir savaşçı hücum ederken de kaçınabilmeli.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 400, evasion: 100)],
            [TestBuilders.Warrior(101, health: 400, thrown: ThrownWeapon.Shuriken())])
        {
            Tuning = AlwaysCharges with { ChargeWindupSeconds = 1.5 },
        };

        var battle = new Battle(setup, new SeededRandom(3));
        StepUntil(battle, b => b.IsFinished, maxSteps: 4000);

        Assert.Contains(battle.Events.OfType<AttackDodged>(), e => e.Defender == _fighter);
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
            [TestBuilders.Warrior(101, health: 400, thrown: ThrownWeapon.Shuriken())])
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

    // ---- Birikme (GDD §4) ----

    /// <summary>
    /// Hücum önce yerinde birikir, sonra koşar. Birikirken savaşçı <b>yerinden
    /// kıpırdamaz</b> — bedelin ödendiği pencere burasıdır.
    /// </summary>
    [Fact]
    public void TheChargeGathersBeforeItRuns()
    {
        var battle = new Battle(Duel(AlwaysCharges), new SeededRandom(7));

        Assert.True(StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.ChargeWindup));
        Assert.Empty(battle.Events.OfType<ChargeLaunched>());

        ArenaPoint gathering = battle.SnapshotOf(_fighter).Position;

        Assert.True(StepUntil(battle, b => b.Events.OfType<ChargeLaunched>().Any()));

        // Birikme boyunca hiç yol alınmadı; koşu ancak şimdi başlıyor.
        Assert.Equal(gathering.X, PositionAtLaunch(battle).X, precision: 6);
        Assert.Equal(CombatState.Charging, battle.SnapshotOf(_fighter).State);
    }

    private static ArenaPoint PositionAtLaunch(Battle battle) => battle.SnapshotOf(_fighter).Position;

    /// <summary>
    /// Birikirken yenen bir isabet hücumu dağıtır: koşu hiç başlamaz, hasar çarpanı
    /// kazanılmaz.
    /// </summary>
    /// <remarks>
    /// Ölçüm bu kuralı iki kez değiştirdi. Önce "ağır darbe dağıtır" denendi — 3v3'te
    /// birikmenin <b>%0.0</b>'ı dağılıyordu, çünkü taze savaşçıya inen darbeler ağır
    /// darbe eşiğine hemen hiç ulaşmıyor. İsabet ölçütüyle %23.6'sı dağılıyor.
    /// </remarks>
    [Fact]
    public void AHitWhileGatheringBreaksTheCharge()
    {
        // Menzil içindeyken bile hücuma kalkılsın ve birikme yeterince uzun sürsün ki
        // yanı başındaki düşman vurmaya fırsat bulsun.
        // Mermili düşman hücuma kalkmaz, atar (GDD §4) — biriken savaşçıyı vurabilecek
        // tek düşman odur, ve hücumun doğal karşıtı da tam olarak budur.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 400)],
            [TestBuilders.Warrior(101, health: 400, thrown: ThrownWeapon.Shuriken())])
        {
            // Birikme, ilk karar anındaki boşluğa sığacak kadar uzun tutuldu: düşman
            // menzile giremiyor, ama mermisi yetişiyor — kasıtlı kör nokta budur.
            Tuning = AlwaysCharges with { ChargeWindupSeconds = 1.5 },
        };

        var battle = new Battle(setup, new SeededRandom(11));

        Assert.True(StepUntil(battle, b => b.Events.OfType<ChargeBroken>().Any(e => e.Warrior == _fighter)));

        ChargeBroken broken = battle.Events.OfType<ChargeBroken>().First(e => e.Warrior == _fighter);

        // Dağılan birikme koşuya dönüşmedi ve varış bonusu kazanılmadı.
        Assert.DoesNotContain(
            battle.Events.OfType<ChargeLaunched>(),
            e => e.Warrior == _fighter && e.AtSeconds <= broken.AtSeconds);
        Assert.DoesNotContain(
            battle.Events.OfType<ChargeConnected>(),
            e => e.Warrior == _fighter && e.AtSeconds <= broken.AtSeconds);
    }

    /// <summary>"Çek" komutu birikmeyi de keser, tıpkı koşuyu kestiği gibi.</summary>
    [Fact]
    public void TheRetreatCommandCutsTheWindupToo()
    {
        var laggard = new WarriorId(2);
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400, speed: 100),
                TestBuilders.Warrior(2, health: 400, speed: 0),
            ],
            [TestBuilders.Warrior(101, health: 400, thrown: ThrownWeapon.Shuriken())])
        {
            Tuning = AlwaysCharges with { ChargeWindupSeconds = 1.5 },
        };

        var battle = new Battle(setup, new SeededRandom(9));

        Assert.True(StepUntil(
            battle,
            b => b.ContactMade && b.SnapshotOf(laggard).State == CombatState.ChargeWindup));

        Assert.True(battle.CommandRetreat());

        Assert.Equal(CombatState.Retreating, battle.SnapshotOf(laggard).State);
        Assert.Contains(battle.Events.OfType<ChargeMissed>(), e => e.Warrior == laggard);
    }

    // ---- Karar: statlar + zar (GDD §4) ----

    /// <summary>
    /// Hücum kararı savaşçının kimliğinden çıkar: atılgan olan daha sık hücuma kalkar.
    /// </summary>
    [Fact]
    public void AggressionDecidesHowOftenAWarriorCharges()
    {
        Assert.True(
            ChargeCount(aggression: 90) > ChargeCount(aggression: 10),
            "Saldırganlık hücum sıklığını değiştirmiyor.");
    }

    /// <summary>
    /// Bir savaşçının otuz dövüşte kaç kez hücuma kalktığı. Tek dövüş zarın insafındadır;
    /// eğilimi görmek için koşum tekrarlanır.
    /// </summary>
    private static int ChargeCount(double aggression)
    {
        int charges = 0;

        for (ulong seed = 1; seed <= 30; seed++)
        {
            // Devrilen her hedef bir karar anı açar — böylece tek dövüşte birden çok
            // hücum zarı atılır ve eğilim ölçülebilir hale gelir.
            BattleSetup setup = FlankedPair(aggression);

            var battle = new Battle(setup, new SeededRandom(seed));
            StepUntil(battle, b => b.IsFinished, maxSteps: 4000);

            charges += battle.Events.OfType<ChargeStarted>().Count(e => e.Warrior == _fighter);
        }

        return charges;
    }

    /// <summary>
    /// Menzilinde düşman varken hücuma kalkılmaz: birikmeyi tamamlayacak boşluk yoktur.
    /// </summary>
    /// <remarks>
    /// Hücumun tetiği sabit bir mesafe eşiği değil, bir <b>fırsat değerlendirmesi</b>dir:
    /// "şu an kimse bana vuramıyor ve birikmemi tamamlayacak vaktim var." Zar her zaman
    /// tutsa bile bu koşul sağlanmadan hücum başlamaz.
    /// </remarks>
    [Fact]
    public void NobodyChargesWithAnEnemyAlreadyInReach()
    {
        var battle = new Battle(Duel(AlwaysCharges), new SeededRandom(21));

        // Temas kurulana kadar koştur: artık iki taraf da birbirinin menzilinde.
        Assert.True(StepUntil(battle, b => b.ContactMade));

        int before = battle.Events.OfType<ChargeStarted>().Count();

        for (int i = 0; i < 400; i++)
        {
            battle.Step();
        }

        Assert.Equal(before, battle.Events.OfType<ChargeStarted>().Count());
    }

    /// <summary>
    /// Boşluğun ölçüsü <b>düşmanın hızıdır</b>: yavaş düşman birikmeye vakit bırakır,
    /// hızlı düşman aynı mesafeden bırakmaz.
    /// </summary>
    /// <remarks>
    /// Gereken mesafe elle seçilmiş bir sayı değil, <c>menzil + hız × birikme</c>
    /// formülünden türer — bu yüzden aynı mesafe bir düşman için yeterli, öbürü için
    /// yetersizdir. Uzun birikme farkı açılış mesafesinde görünür kılıyor.
    /// </remarks>
    [Fact]
    public void TheRoomNeededDependsOnHowFastTheEnemyIs()
    {
        Assert.True(ChargesAgainst(enemySpeed: 0), "Yavaş düşmana karşı hücum hiç kalkmadı.");
        Assert.False(ChargesAgainst(enemySpeed: 100), "Hızlı düşman birikmeye vakit bırakmamalıydı.");
    }

    private static bool ChargesAgainst(double enemySpeed)
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 400)],
            [TestBuilders.Warrior(101, health: 400, speed: enemySpeed)])
        {
            // Uzun birikme: açılış mesafesi yavaş düşman için yeterli, hızlı için değil.
            Tuning = AlwaysCharges with { ChargeWindupSeconds = 3.0 },
        };

        var battle = new Battle(setup, new SeededRandom(7));
        StepUntil(battle, b => b.IsFinished, maxSteps: 4000);

        return battle.Events.OfType<ChargeStarted>().Any(e => e.Warrior == _fighter);
    }

    /// <summary>
    /// İki cepheli 2v2: her savaşçı kendi karşısındakine tutuşur, biri hedefini devirince
    /// sıradaki düşman <b>öbür cephede ve menzilin çok dışında</b> kalır.
    /// </summary>
    /// <remarks>
    /// Yeniden tutuşmayı ancak bu biçim ölçebilir. Tek savaşçıya karşı kalabalık
    /// kurulduğunda düşmanların hepsi aynı noktada toplanıyor ve hedef devrildiğinde
    /// sıradaki zaten menzilin içinde oluyor — karar anı hiç gelmiyor.
    /// </remarks>
    private static BattleSetup FlankedPair(double aggression) => new(
        [
            TestBuilders.Warrior(1, health: 4000, aggression: aggression, strength: 100, accuracy: 100),
            TestBuilders.Warrior(2, health: 40_000),
        ],
        [
            TestBuilders.Warrior(101, health: 1),
            TestBuilders.Warrior(102, health: 40_000),
        ])
    {
        Tuning = CombatTuning.Default with { StartSpacingY = 900 },
    };

    /// <summary>
    /// Hedefini deviren savaşçının önünde boşluk açılır ve sıradakine hücum edebilir.
    /// Ölçüm bu kuralı gerektirdi: sabit eşikle 30.000 dövüşün hiçbirinde 1.75 sn'den
    /// sonra hücuma kalkılmıyordu, çünkü hatlar buluştuktan sonra mesafe bir daha hiç
    /// doğmuyor. Fırsat değerlendirmesi bunu kendiliğinden çözer.
    /// </summary>
    [Fact]
    public void KillingYourTargetOpensAChargeOnTheNextOne()
    {
        // Normal eşik ulaşılamaz; kalkan her hücum ancak yeniden tutuşma penceresinden
        // gelmiş olabilir.
        BattleSetup setup = FlankedPair(aggression: 100);

        // Ölçülen şey zarın tutup tutmadığı değil, fırsatın doğup doğmadığı.
        setup = setup with
        {
            Tuning = setup.Tuning with
            {
                ChargeChanceAtZeroAggression = 1.0,
                ChargeChanceAtMaxAggression = 1.0,
            },
        };

        var battle = new Battle(setup, new SeededRandom(13));

        Assert.True(StepUntil(battle, b => b.Events.OfType<WarriorDied>().Any(), maxSteps: 4000));

        double death = battle.Events.OfType<WarriorDied>().First().AtSeconds;

        // Hedefi devrildikten SONRA da hücuma kalkabilmeli: hücum açılış hamlesi değil.
        Assert.True(
            StepUntil(
                battle,
                b => b.Events.OfType<ChargeStarted>()
                    .Any(e => e.Warrior == _fighter && e.AtSeconds > death),
                maxSteps: 4000),
            "Hedefi devrilen savaşçı sıradakine hiç hücum etmedi.");
    }

    private static double Gap(Battle battle) =>
        battle.SnapshotOf(_fighter).Position.DistanceTo(battle.SnapshotOf(_enemy).Position);
}

