using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Zehir, hasar azaltımının etrafından dolaşan tek yoldur: doz kana girer, zırh onu
/// okuyamaz. Bu testler kuralın iki ucunu bağlar — doz zamanla işler, zırh ve Savunma
/// onu azaltmaz — ve zehrin <b>karışmaması</b> gereken yerleri (uzuv kopma, sersemletme,
/// temiz silah) kapalı tutar.
/// </summary>
public class PoisonTests
{
    /// <summary>Zehrin izole edildiği ayar: kopma ve sersemletme dalları kapalı.</summary>
    /// <remarks>
    /// Üçü de aynı vuruştan çıkar; açık bırakılsalardı test zehrin değil sonuç ağacının
    /// davranışını ölçerdi.
    /// </remarks>
    private static CombatTuning PoisonOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        MaxBattleSeconds = 12,
    };

    /// <summary>Zehirli alet: çeliği hafif, karşılığı doz.</summary>
    private static Weapon Fang { get; } =
        new("Test-Zehirli", WeaponClass.Cutting, 5, TwoHanded: false, AttackSeconds: 1.0)
        {
            Poison = 1.0,
        };

    /// <summary>Aynı aletin temiz hâli — kontrol tarafı.</summary>
    private static Weapon CleanFang { get; } = Fang with { Name = "Test-Temiz", Poison = 0 };

    private static BattleSetup Bout(
        Weapon attackerWeapon,
        Armor? defenderArmor = null,
        double defenderDefense = 0,
        CombatTuning? tuning = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Zehirlenen",
                health: 400,
                aggression: 0,
                defense: defenderDefense,
                weapon: Weapon.Fists(),
                armor: defenderArmor),
        ],
        [TestBuilders.Warrior(101, "Zehirleyen", aggression: 100, weapon: attackerWeapon)])
    {
        Tuning = tuning ?? PoisonOnly,
    };

    /// <summary>Zehirli vuruş doz bırakır ve doz zamanla can yer.</summary>
    [Fact]
    public void APoisonedBlowLeavesADoseThatKeepsWorking()
    {
        var battle = new Battle(Bout(Fang), new FixedRandom(0.0));
        battle.Run();

        WarriorPoisoned poisoned = battle.Events.OfType<WarriorPoisoned>().First();
        Assert.Equal(new WarriorId(1), poisoned.Defender);
        Assert.Equal(new WarriorId(101), poisoned.Attacker);
        Assert.Equal(PoisonOnly.PoisonSeconds, poisoned.Seconds);

        // Hasar vuruşun anında değil, sonrasında gelir.
        List<PoisonTicked> ticks = [.. battle.Events.OfType<PoisonTicked>()];
        Assert.NotEmpty(ticks);
        Assert.All(ticks, t => Assert.True(t.AtSeconds > poisoned.AtSeconds));
    }

    /// <summary>
    /// Zehir hasarı zırhtan da Savunma statından da geçmez.
    /// </summary>
    /// <remarks>
    /// Kuralın tamamı bunun üstüne kurulu. Zırh dozu azaltsaydı zehir yalnızca "biraz
    /// daha hasar" olurdu ve zehirli silahın düşük çeliği hiçbir şeyin bedeli olmazdı.
    /// </remarks>
    [Fact]
    public void PoisonIgnoresArmorAndDefense()
    {
        double bare = FirstTickDamage(Bout(Fang));
        double armored = FirstTickDamage(
            Bout(Fang, defenderArmor: Armor.Heavy(), defenderDefense: 100));

        Assert.Equal(bare, armored);

        static double FirstTickDamage(BattleSetup setup)
        {
            var battle = new Battle(setup, new FixedRandom(0.0));
            battle.Run();
            return battle.Events.OfType<PoisonTicked>().First().Damage;
        }
    }

    /// <summary>Doz birikir — ama tavanı aşmaz.</summary>
    [Fact]
    public void DosesStackUpToTheCap()
    {
        CombatTuning tuning = PoisonOnly with { PoisonMaxDose = 2.0 };
        var battle = new Battle(Bout(Fang, tuning: tuning), new FixedRandom(0.0));
        battle.Run();

        List<WarriorPoisoned> doses = [.. battle.Events.OfType<WarriorPoisoned>()];

        Assert.True(doses.Count >= 3, $"Yeterli zehirli vuruş düşmedi ({doses.Count}).");
        Assert.Equal(1.0, doses[0].Dose);
        Assert.Equal(2.0, doses[1].Dose);
        Assert.All(doses, d => Assert.True(d.Dose <= tuning.PoisonMaxDose));
    }

    /// <summary>Süre dolunca zehir kendiliğinden biter.</summary>
    [Fact]
    public void PoisonExpiresOnItsOwn()
    {
        // Tek bir zehirli vuruş: saldıran bir daha vuramayacak kadar yavaş.
        Weapon slowFang = Fang with { AttackSeconds = 30 };
        var battle = new Battle(
            Bout(slowFang, tuning: PoisonOnly with { MaxBattleSeconds = 20 }),
            new FixedRandom(0.0));
        battle.Run();

        WarriorPoisoned poisoned = Assert.Single(battle.Events.OfType<WarriorPoisoned>());
        double lastTick = battle.Events.OfType<PoisonTicked>().Max(t => t.AtSeconds);

        Assert.True(
            lastTick <= poisoned.AtSeconds + PoisonOnly.PoisonSeconds,
            $"Zehir ömrünü aştı ({lastTick:F2} > {poisoned.AtSeconds + PoisonOnly.PoisonSeconds:F2}).");
    }

    /// <summary>Temiz silah doz bırakmaz — kontrol tarafı.</summary>
    [Fact]
    public void ACleanWeaponNeverPoisons()
    {
        var battle = new Battle(Bout(CleanFang), new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<AttackLanded>());
        Assert.Empty(battle.Events.OfType<WarriorPoisoned>());
        Assert.Empty(battle.Events.OfType<PoisonTicked>());
    }

    /// <summary>
    /// Zehir uzuv koparmaz ve sersemletmez: ikisi de <b>darbenin</b> sonucudur.
    /// </summary>
    /// <remarks>
    /// Eşikler erişilemeyecek kadar yükseğe çekilir, yani vuruşun kendisi hiçbir zar
    /// attırmaz; geriye yalnızca zehir kalır. Zehir bu dalları da açsaydı zehirli silah
    /// hem oyunun imza mekaniğinden pay alır hem künt sınıfın işini yapardı.
    /// </remarks>
    [Fact]
    public void PoisonNeitherSeversNorStuns()
    {
        CombatTuning tuning = PoisonOnly with
        {
            BaseDismembermentChance = 1.0,
            BaseStunChance = 1.0,
            GrievousSeverityThreshold = 5.0,
            StunSeverityThreshold = 5.0,
        };

        var battle = new Battle(Bout(Fang, tuning: tuning), new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<PoisonTicked>());
        Assert.Empty(battle.Events.OfType<WarriorDismembered>());
        Assert.Empty(battle.Events.OfType<WarriorStunned>());
    }

    /// <summary>Zehrin indirdiği ölüm ayrı bir sebep taşır: kimse vurmamıştır.</summary>
    [Fact]
    public void PoisonKillsUnderItsOwnCause()
    {
        // Tek vuruş, sonra saldıran susar; canı bitiren şey yalnızca doz olabilir.
        Weapon slowFang = Fang with { AttackSeconds = 30 };
        BattleSetup setup = Bout(slowFang, tuning: PoisonOnly with { MaxBattleSeconds = 30 }) with
        {
            PlayerSide =
            [
                TestBuilders.Warrior(1, "Zehirlenen", health: 12, aggression: 0, weapon: Weapon.Fists()),
            ],
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        WarriorDied died = battle.Events.OfType<WarriorDied>().First(e => e.Warrior == new WarriorId(1));
        Assert.Equal(DeathCause.Poison, died.Cause);
        Assert.Equal(DeathCause.Poison, battle.Result!.SummaryFor(new WarriorId(1)).DeathCause);
    }

    /// <summary>
    /// Çekilen savaşçının zehri durmaz — tuş bir panzehir değildir.
    /// </summary>
    /// <remarks>
    /// Sersemletme ve yakalama çekilene işlemez, çünkü ikisi de kaçış vaadinin üstüne
    /// <b>yeni bir zar</b> koyar. Zehir yeni bir zar değil, çoktan ödenmiş bir bedelin
    /// devamıdır; durdurulsaydı kaçış komutu aynı zamanda bir tedavi olurdu.
    /// </remarks>
    [Fact]
    public void PoisonKeepsWorkingOnARetreatingWarrior()
    {
        var battle = new Battle(Bout(Fang), new FixedRandom(0.0));

        while (!battle.ContactMade && battle.Step())
        {
        }

        Assert.True(battle.CommandRetreat());
        double commandedAt = battle.ElapsedSeconds;

        battle.Run();

        Assert.Contains(
            battle.Events.OfType<PoisonTicked>(),
            t => t.Warrior == new WarriorId(1) && t.AtSeconds > commandedAt);
    }

    /// <summary>Zehir mermide de taşınır — kural yakın dövüşe ait değil, namluya ait.</summary>
    [Fact]
    public void ThrownWeaponsCarryPoison()
    {
        BattleSetup setup = new(
            [
                TestBuilders.Warrior(1, "Hedef", health: 400, aggression: 0, weapon: Weapon.Fists()),
            ],
            [
                TestBuilders.Warrior(
                    101,
                    "Atıcı",
                    aggression: 100,
                    weapon: CleanFang,
                    thrown: ThrownWeapon.PoisonedShuriken()),
            ])
        {
            Tuning = PoisonOnly with { StartOffsetX = 400 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        ProjectileHit hit = battle.Events.OfType<ProjectileHit>().First();
        Assert.Contains(
            battle.Events.OfType<WarriorPoisoned>(),
            p => p.Defender == new WarriorId(1) && p.AtSeconds == hit.AtSeconds);
    }
}
