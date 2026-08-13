using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Uzuv kaybı sonuç ağacı (GDD §7): ağır darbe geldiğinde oyuncu müdahale ettiyse
/// savaşçı <b>uzvunu kaybederek yaşar</b>, etmediyse ölür. Bu ağaç oyunun kimliğini
/// belirleyen mekanik — "tuşa basmak hayat kurtarır ama bedelsiz değildir".
/// </summary>
public class DismembermentTests
{
    /// <summary>
    /// Her darbeyi bacağa indiren ayar.
    /// </summary>
    /// <remarks>
    /// Sonuç ağacını sınayan testler darbenin <b>koparılabilir</b> bir bölgeye inmesini
    /// ister; gövdeye inen darbenin koparacak bir şeyi yoktur. Bölge dağılımı ayrı
    /// testlerin konusu.
    /// </remarks>
    private static CombatTuning AlwaysLimb { get; } = TestBuilders.PointBlank with
    {
        TorsoHitWeight = 0,
        HeadHitWeight = 0,
        ArmHitWeight = 0,
        LegHitWeight = 100,
    };

    /// <summary>Tek vuruşta ağır darbe eşiğini aşan ama öldürmeyen kurulum.</summary>
    private static BattleSetup Executioner(double defenderHealth = 300, Armor? armor = null) => new(
        [TestBuilders.Warrior(1, "Kurban", health: defenderHealth, aggression: 0, armor: armor)],
        [TestBuilders.Warrior(101, "Cellat", aggression: 100, weapon: TestBuilders.Executioner())])
    {
        Tuning = AlwaysLimb,
    };

    [Fact]
    public void GrievousBlowWithoutInterventionKills()
    {
        var battle = new Battle(Executioner(), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WarriorBattleSummary victim = result.SummaryFor(new WarriorId(1));

        Assert.True(victim.Died);
        Assert.False(victim.LostLimb);
        Assert.Null(victim.LostPart);

        // Canı bitmeden, doğrudan ağır darbeyle ölmüş olmalı.
        WarriorDied death = battle.Events.OfType<WarriorDied>().Single();
        Assert.Equal(DeathCause.GrievousBlow, death.Cause);
    }

    [Fact]
    public void GrievousBlowAfterInterventionCostsALimbButNotLife()
    {
        var battle = new Battle(Executioner(), new FixedRandom(0.0));

        // Tuşa basmak "zamanında müdahale" sayılır — kaçış henüz başlamamış olsa bile.
        Assert.True(battle.CommandRetreat());

        BattleResult result = battle.Run();
        WarriorBattleSummary victim = result.SummaryFor(new WarriorId(1));

        Assert.False(victim.Died);
        Assert.True(victim.LostLimb);
        Assert.NotNull(victim.LostPart);
        Assert.Contains(battle.Events, e => e is WarriorDismembered);
        Assert.DoesNotContain(battle.Events, e => e is WarriorDied);
    }

    [Fact]
    public void DismembermentDoesNotSaveAWarriorWhoseHealthIsGone()
    {
        // Canı 100: aynı darbe hem eşiği aşar hem canı bitirir. Uzvunu kaybeder
        // ama yine de ölür — müdahale ölümsüzlük değildir.
        var battle = new Battle(Executioner(defenderHealth: 100), new FixedRandom(0.0));
        battle.CommandRetreat();

        WarriorBattleSummary victim = battle.Run().SummaryFor(new WarriorId(1));

        Assert.True(victim.Died);
        Assert.True(victim.LostLimb);
        Assert.Contains(battle.Events, e => e is WarriorDismembered);
        Assert.Equal(DeathCause.Wounds, battle.Events.OfType<WarriorDied>().Single().Cause);
    }

    [Fact]
    public void LightBlowsNeverTriggerTheGrievousTree()
    {
        // Yumruk: hasar/azami can oranı eşiğin (0.28) çok altında kalır.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 300, aggression: 0)],
            [TestBuilders.Warrior(101, aggression: 100, weapon: Weapon.Fists())])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.CommandRetreat();
        battle.Run();

        Assert.Contains(battle.Events, e => e is AttackLanded);
        Assert.DoesNotContain(battle.Events, e => e is WarriorDismembered);
    }

    [Fact]
    public void BluntWeaponsRarelyDismember()
    {
        // Zar 0.20: kesici için 0.35 eşiğinin altında (kopar), künt için
        // 0.35 × 0.15 = 0.0525 eşiğinin üstünde (kopmaz).
        Weapon blunt = TestBuilders.Executioner() with { Class = WeaponClass.Blunt };

        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 300, aggression: 0)],
            [TestBuilders.Warrior(101, aggression: 100, weapon: blunt)])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new FixedRandom(0.20));
        battle.CommandRetreat();
        battle.Run();

        Assert.Contains(battle.Events, e => e is AttackLanded);
        Assert.DoesNotContain(battle.Events, e => e is WarriorDismembered);
    }

    [Fact]
    public void HeavyArmorPreventsDismembermentThatLightArmorAllows()
    {
        // Ekipmana yatırımı anlamlı kılan kalem: aynı darbe, aynı zar, farklı zırh.
        var unarmored = new Battle(Executioner(armor: Armor.None()), new FixedRandom(0.20));
        unarmored.CommandRetreat();
        unarmored.Run();

        var armored = new Battle(Executioner(armor: Armor.Heavy()), new FixedRandom(0.20));
        armored.CommandRetreat();
        armored.Run();

        Assert.Contains(unarmored.Events, e => e is WarriorDismembered);
        Assert.DoesNotContain(armored.Events, e => e is WarriorDismembered);
    }

    [Fact]
    public void AlreadyLostPartsAreNotChosenAgain()
    {
        Warrior victim = TestBuilders.Warrior(1, health: 300, aggression: 0);
        Assert.True(victim.AddDisability(BodyPart.Arm));

        var setup = new BattleSetup(
            [victim],
            [TestBuilders.Warrior(101, aggression: 100, weapon: TestBuilders.Executioner())])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.CommandRetreat();
        battle.Run();

        WarriorDismembered lost = battle.Events.OfType<WarriorDismembered>().First();
        Assert.NotEqual(BodyPart.Arm, lost.Part);
    }

    /// <summary>
    /// Gövdeye inen ağır darbe de bir uzva mal olur — müdahale asla bedava değildir.
    /// </summary>
    /// <remarks>
    /// Gövde vuruşları koparmasın denince müdahale risksizleşiyor ve oyuncu zaferi
    /// %36'dan %53'e çıkıyordu (10.000 dövüş, 3v3). Bölge hasarı ve zırhı ilgilendirir,
    /// sonuç ağacını değil.
    /// </remarks>
    [Fact]
    public void BlowsToTheTorsoStillCostALimb()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kurban", health: 300, aggression: 0)],
            [TestBuilders.Warrior(101, "Cellat", aggression: 100, weapon: TestBuilders.Executioner())])
        {
            // Her darbe gövdeye iniyor; kalan uzuvların ağırlıkları varsayılan kalır,
            // çünkü kopacak uzuv onların arasından seçilecek.
            Tuning = TestBuilders.PointBlank with { TorsoHitWeight = 1000 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.CommandRetreat();
        battle.Run();

        Assert.Contains(battle.Events, e => e is WarriorDismembered);
    }

    [Fact]
    public void BattleNeverMutatesThePermanentWarrior()
    {
        // Kalıcı hali işlemek meta katmanın işi. Bu bozulursa aynı kadroyla
        // on binlerce dövüş simüle edilemez — toplu simülasyonun temel varsayımı.
        Warrior victim = TestBuilders.Warrior(1, health: 300, aggression: 0);

        var setup = new BattleSetup(
            [victim],
            [TestBuilders.Warrior(101, aggression: 100, weapon: TestBuilders.Executioner())])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.CommandRetreat();
        battle.Run();

        Assert.True(victim.IsAlive);
        Assert.Empty(victim.Disabilities);
    }

    [Fact]
    public void ArmLossForcesFistsInsteadOfATwoHandedWeapon()
    {
        Warrior warrior = TestBuilders.Warrior(1, weapon: Weapon.Nodachi());
        Assert.Equal(Weapon.Nodachi(), warrior.UsableWeapon);

        warrior.AddDisability(BodyPart.Arm);

        Assert.Equal(Weapon.Fists(), warrior.UsableWeapon);
    }

    [Theory]
    [InlineData(BodyPart.Arm)]
    [InlineData(BodyPart.Leg)]
    [InlineData(BodyPart.Eye)]
    public void DisabilitiesArePermanentAndNotDuplicated(BodyPart part)
    {
        Warrior warrior = TestBuilders.Warrior(1);

        Assert.True(warrior.AddDisability(part));
        Assert.False(warrior.AddDisability(part));
        Assert.Single(warrior.Disabilities);
        Assert.True(warrior.HasDisability(part));
    }

    [Fact]
    public void EachLostPartWeakensItsOwnStat()
    {
        WarriorStats baseline = TestBuilders.Warrior(1, strength: 100, evasion: 100, accuracy: 100).BaseStats;

        Warrior armless = TestBuilders.Warrior(1, strength: 100, evasion: 100, accuracy: 100);
        armless.AddDisability(BodyPart.Arm);

        Warrior lame = TestBuilders.Warrior(2, strength: 100, evasion: 100, accuracy: 100);
        lame.AddDisability(BodyPart.Leg);

        Warrior halfBlind = TestBuilders.Warrior(3, strength: 100, evasion: 100, accuracy: 100);
        halfBlind.AddDisability(BodyPart.Eye);

        Assert.True(armless.EffectiveStats.Strength < baseline.Strength);
        Assert.Equal(baseline.Evasion, armless.EffectiveStats.Evasion);

        Assert.True(lame.EffectiveStats.Evasion < baseline.Evasion);
        Assert.True(halfBlind.EffectiveStats.Accuracy < baseline.Accuracy);
    }
}
