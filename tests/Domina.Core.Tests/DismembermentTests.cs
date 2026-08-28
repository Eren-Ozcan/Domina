using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Uzuv kaybı sonuç ağacı (GDD §7). Belirleyici olan darbenin <b>öldürücü olup
/// olmadığı</b>:
/// <code>
/// ağır darbe + kopma zarı tuttu
///   ├─ can > 0  → uzuv gider, dövüş sürer
///   └─ can ≤ 0  → tuşa basılmışsa uzuvla yaşar, basılmamışsa ölür
/// </code>
/// Bu ağaç oyunun kimliğini belirleyen mekanik — "tuşa basmak hayat kurtarır ama
/// bedelsiz değildir".
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
    /// <summary>
    /// Hafif ve hızlı silah: ağır darbe eşiğini aşmadan <b>ilk kanı akıtır</b>.
    /// </summary>
    /// <remarks>
    /// "Çek" tuşu ilk isabetten önce kapalı olduğu için (GDD §5) müdahale dalını ölçen
    /// testin savaşı önce başlatması gerekir. Kurbanın kendi vuruşu bunun en ucuz yolu:
    /// kimsenin canını riske atmadan tuşu açar.
    /// </remarks>
    private static Weapon Quick { get; } =
        new("Test-Tantō", WeaponClass.Cutting, 12, TwoHanded: false, AttackSeconds: 0.4);

    /// <summary>
    /// Savaşı başlatır, sonra "çek" tuşuna basar.
    /// </summary>
    /// <remarks>
    /// Tuş ilk isabete kadar kapalıdır (GDD §5): temas öncesi kaçış diye bir şey yok.
    /// Müdahale dalını ölçen testlerin konusu bu değil, ön koşulu.
    /// </remarks>
    private static bool PressAfterFirstBlood(Battle battle)
    {
        while (!battle.ContactMade && battle.Step())
        {
        }

        return battle.CommandRetreat();
    }

    private static BattleSetup Executioner(
        double defenderHealth = 300,
        Armor? armor = null,
        double victimAggression = 0,
        Weapon? victimWeapon = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Kurban",
                health: defenderHealth,
                aggression: victimAggression,
                weapon: victimWeapon,
                armor: armor),
        ],
        [TestBuilders.Warrior(101, "Cellat", aggression: 100, weapon: TestBuilders.Executioner())])
    {
        Tuning = AlwaysLimb,
    };

    /// <summary>
    /// Öldürmeyen ağır darbe hiçbir tuşa basılmadan uzva mal olur ve savaşçı sahada
    /// kalır.
    /// </summary>
    /// <remarks>
    /// Bu dalın varlık sebebi: kopma yalnızca <c>PlayerIntervened</c> iken oluşurken,
    /// o bayrağı da yalnızca seferi bitiren "Kaç" tuşu açtığı için <b>uzuv kaybederek
    /// kazanmak imkânsızdı</b> (20.000 dövüş, zafer + uzuv kaybı: 0 kez).
    /// </remarks>
    [Fact]
    public void ANonLethalGrievousBlowCostsALimbWithNoButtonPressed()
    {
        var battle = new Battle(Executioner(), new FixedRandom(0.0));
        battle.Run();

        // İlk kopma, savaşçı hâlâ ayaktayken olmalı: canı 300, tek darbe ~90.
        WarriorDismembered first = battle.Events.OfType<WarriorDismembered>().First();
        AttackLanded blow = battle.Events
            .OfType<AttackLanded>()
            .First(e => e.Defender == new WarriorId(1));

        Assert.Equal(first.Warrior, blow.Defender);
        Assert.True(blow.DefenderHealthRemaining > 0);

        // Ve dövüş kopmadan sonra devam etmiş olmalı.
        Assert.Contains(
            battle.Events.OfType<AttackLanded>(),
            e => e.AtSeconds > first.AtSeconds);
    }

    /// <summary>Uzvunu kaybeden savaşçı dövüşü kazanabilir — eve sakat şampiyon gelir.</summary>
    /// <remarks>
    /// Eski sonuç ağacında bu imkânsızdı: kopma "Kaç" tuşunu gerektiriyordu, tuş da
    /// seferi bitiriyordu. 20.000 dövüş ölçüldü, zafer + uzuv kaybı hiç görülmedi.
    /// </remarks>
    [Fact]
    public void AWarriorCanWinTheBattleAfterLosingALimb()
    {
        // Kurban sert vuruyor (2 darbede bitirir), cellat sık ama hafif vuruyor:
        // eşik düşürüldüğü için o hafif darbeler de uzva mal olur.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kurban", health: 400, aggression: 100, weapon: TestBuilders.Executioner())],
            [
                TestBuilders.Warrior(
                    101,
                    "Cellat",
                    health: 100,
                    aggression: 100,
                    weapon: new Weapon("Testere", WeaponClass.Cutting, 30, false, 0.4)),
            ])
        {
            Tuning = AlwaysLimb with { GrievousSeverityThreshold = 0.05 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WarriorBattleSummary survivor = result.SummaryFor(new WarriorId(1));

        Assert.Equal(BattleOutcome.PlayerVictory, result.Outcome);
        Assert.True(survivor.LostLimb);
        Assert.False(survivor.Died);
    }

    [Fact]
    public void ALethalGrievousBlowWithoutInterventionKills()
    {
        // Canı 60: tek darbe hem eşiği aşar hem canı bitirir, tuşa da basılmamış.
        var battle = new Battle(Executioner(defenderHealth: 60), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WarriorBattleSummary victim = result.SummaryFor(new WarriorId(1));

        Assert.True(victim.Died);
        Assert.Equal(
            DeathCause.GrievousBlow,
            battle.Events.OfType<WarriorDied>().Single().Cause);
    }

    /// <summary>
    /// Tuşun işi: öldürücü darbeyi uzuv kaybına çevirmek.
    /// </summary>
    /// <remarks>
    /// Kurtuluş <b>ölümsüzlük değil</b> — kurtulan savaşçı 1 canla kalır ve kaçışın geri
    /// kalanını bir sonraki darbede ölecek durumda geçirir. Bu test o dalı izole etmek
    /// için celladı yavaş tutuyor: peşinden yetişemesin, tek darbenin sonucu ölçülebilsin.
    /// </remarks>
    [Fact]
    public void InterventionTurnsALethalBlowIntoALimbLoss()
    {
        BattleSetup setup = Executioner(defenderHealth: 60, victimAggression: 100, victimWeapon: Quick) with
        {
            EnemySide =
            [
                TestBuilders.Warrior(
                    101, "Cellat", aggression: 100, speed: 1, weapon: TestBuilders.Executioner()),
            ],
        };

        var battle = new Battle(setup, new FixedRandom(0.0));

        // Tuşa basmak "zamanında müdahale" sayılır — kaçış henüz başlamamış olsa bile.
        // Tuş ancak savaş başlayınca açılır (§5), o yüzden önce ilk kan akmalı.
        Assert.True(PressAfterFirstBlood(battle));

        WarriorBattleSummary victim = battle.Run().SummaryFor(new WarriorId(1));

        Assert.False(victim.Died);
        Assert.True(victim.LostLimb);
        Assert.Contains(battle.Events, e => e is WarriorDismembered);
        Assert.DoesNotContain(battle.Events, e => e is WarriorDied);
    }

    /// <summary>
    /// Koparacak uzvu kalmamış savaşçıyı tuş kurtaramaz — çevrilecek bir bedel yoktur.
    /// </summary>
    [Fact]
    public void InterventionCannotSaveAWarriorWithNoLimbsLeftToLose()
    {
        // Kurban tuşu açacak kadar dövüşür: hızlı ve hafif silahla ilk kanı akıtır,
        // sonra basar. Aksi hâlde tuş hiç açılmadan ölürdü ve dal ölçülmemiş olurdu.
        Warrior victim = TestBuilders.Warrior(
            1, "Kurban", health: 60, aggression: 100, weapon: Quick);
        victim.AddDisability(BodyPart.Arm);
        victim.AddDisability(BodyPart.Leg);
        victim.AddDisability(BodyPart.Eye);

        var setup = new BattleSetup(
            [victim],
            [TestBuilders.Warrior(101, "Cellat", aggression: 100, weapon: TestBuilders.Executioner())])
        {
            Tuning = AlwaysLimb,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        Assert.True(PressAfterFirstBlood(battle));

        Assert.True(battle.Run().SummaryFor(new WarriorId(1)).Died);
        Assert.DoesNotContain(battle.Events, e => e is WarriorDismembered);
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
        PressAfterFirstBlood(battle);
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
        PressAfterFirstBlood(battle);
        battle.Run();

        Assert.Contains(battle.Events, e => e is AttackLanded);
        Assert.DoesNotContain(battle.Events, e => e is WarriorDismembered);
    }

    [Fact]
    public void HeavyArmorPreventsDismembermentThatBareSkinAllows()
    {
        // Ekipmana yatırımı anlamlı kılan kalem: aynı darbe, aynı zar, farklı zırh.
        // Bacağa inen darbe — çıplak bacakta 0.35, ağır suneate altında 0.35 × 0.60 = 0.21.
        var unarmored = new Battle(Executioner(armor: Armor.None()), new FixedRandom(0.30));
        PressAfterFirstBlood(unarmored);
        unarmored.Run();

        var armored = new Battle(Executioner(armor: Armor.Heavy()), new FixedRandom(0.30));
        PressAfterFirstBlood(armored);
        armored.Run();

        Assert.Contains(unarmored.Events, e => e is WarriorDismembered);
        Assert.DoesNotContain(armored.Events, e => e is WarriorDismembered);
    }

    /// <summary>
    /// Direnç, darbenin indiği <b>bölgenin</b> parçasından okunur — takımın ortalamasından
    /// değil.
    /// </summary>
    /// <remarks>
    /// Yuva yuva zırhın bütün gerekçesi bu: hafif keikogi gövdeyi örter, kolu örtmez.
    /// Aynı kuşam, aynı zar, aynı silah — darbe kola inince kol kopar, gövdeye inince
    /// kopmaz. Direnç tek skaler kalsaydı ikisi de aynı sonucu verirdi ve "ucuz kuşam
    /// al, kollarını riske at" diye bir karar hiç var olmazdı.
    /// </remarks>
    [Fact]
    public void TheStruckRegionDecidesResistanceNotTheSuit()
    {
        // Zar 0.30 — keikogi'li gövde: 0.35 × 0.80 = 0.28 (kopmaz).
        //            Açık kol:         0.35 × 1.00 = 0.35 (kopar).
        var toTheArm = new Battle(AtRegion(HitLocation.Arm, Armor.Light()), new FixedRandom(0.30));
        PressAfterFirstBlood(toTheArm);
        toTheArm.Run();

        var toTheTorso = new Battle(AtRegion(HitLocation.Torso, Armor.Light()), new FixedRandom(0.30));
        PressAfterFirstBlood(toTheTorso);
        toTheTorso.Run();

        Assert.Contains(toTheArm.Events, e => e is WarriorDismembered);
        Assert.DoesNotContain(toTheTorso.Events, e => e is WarriorDismembered);
    }

    /// <summary>Kolu örten parça, kolu örtmeyen kuşamın izin verdiği kopmayı durdurur.</summary>
    [Fact]
    public void KoteProtectsTheArmThatAKeikogiLeavesBare()
    {
        var bareArms = new Battle(AtRegion(HitLocation.Arm, Armor.Light()), new FixedRandom(0.30));
        PressAfterFirstBlood(bareArms);
        bareArms.Run();

        // Dō-maru'nun kotesi: 0.35 × 0.70 = 0.245, zarın altında kalır.
        var withKote = new Battle(AtRegion(HitLocation.Arm, Armor.Medium()), new FixedRandom(0.30));
        PressAfterFirstBlood(withKote);
        withKote.Run();

        Assert.Contains(bareArms.Events, e => e is WarriorDismembered);
        Assert.DoesNotContain(withKote.Events, e => e is WarriorDismembered);
    }

    /// <summary>
    /// Bir savaşçı aynı uzvu iki kez kaybedemez — kaçış boyunca kaç darbe yerse yesin.
    /// </summary>
    /// <remarks>
    /// Kayıtlar savaşçı başına tek parça tutulurken her yeni kayıp öncekini siliyordu:
    /// kolunu kaybeden savaşçı bacağını kaybedince "kolu duruyor" sayılıyor, kol tekrar
    /// kopabiliyordu. Ölçüldü: tek savaşçıda 22 kopma, Kol/Bacak sırayla.
    /// </remarks>
    [Fact]
    public void ALimbCanOnlyBeLostOncePerWarrior()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kurban", health: 100_000, aggression: 0)],
            [
                TestBuilders.Warrior(101, "Cellat1", aggression: 100, weapon: TestBuilders.Executioner()),
                TestBuilders.Warrior(102, "Cellat2", aggression: 100, weapon: TestBuilders.Executioner()),
                TestBuilders.Warrior(103, "Cellat3", aggression: 100, weapon: TestBuilders.Executioner()),
            ])
        {
            // Can devasa, her darbe ağır, her zar kopmayı tutturuyor: kurban yalnızca
            // "kaybedecek uzvu kalmadı" kuralıyla durabilir.
            Tuning = TestBuilders.PointBlank with { GrievousSeverityThreshold = 0.0 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        PressAfterFirstBlood(battle);
        BattleResult result = battle.Run();

        BodyPart[] lost = [.. battle.Events.OfType<WarriorDismembered>().Select(e => e.Part)];

        Assert.NotEmpty(lost);
        Assert.Equal(lost.Length, lost.Distinct().Count());
        Assert.Equal(lost.Order(), result.SummaryFor(new WarriorId(1)).LostParts.Parts().Order());
    }

    /// <summary>Her darbeyi verilen bölgeye indiren kurulum.</summary>
    private static BattleSetup AtRegion(HitLocation location, Armor armor)
    {
        CombatTuning tuning = TestBuilders.PointBlank with
        {
            TorsoHitWeight = location == HitLocation.Torso ? 100 : 0,
            LegHitWeight = location == HitLocation.Leg ? 100 : 0,
            ArmHitWeight = location == HitLocation.Arm ? 100 : 0,
            HeadHitWeight = location == HitLocation.Head ? 100 : 0,
        };

        return Executioner(armor: armor) with { Tuning = tuning };
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
        PressAfterFirstBlood(battle);
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
        PressAfterFirstBlood(battle);
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
        PressAfterFirstBlood(battle);
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
