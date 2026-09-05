using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Kılıç yakalama, GDD §4'ün kalkanı reddederken bıraktığı boşluğu doldurur: elde
/// taşınan kalkan yerine, gelen silahı <b>durduran</b> bir alet. Bu testler kuralın iki
/// ucunu da bağlar — yakalanan vuruş hasar vermez, yakalanan savaşçı açıkta kalır — ve
/// kuralın ısırmaması gereken üç yeri (arka, mermi, kaçış) kapalı tutar.
/// </summary>
public class WeaponCatchTests
{
    /// <summary>Yakalama zarı her zaman tutsun; ölçülen şey sayı değil kural.</summary>
    /// <remarks>
    /// Kopma ve sersemletme dalları kapatılır: üç zar da aynı vuruştan atılıyor ve açık
    /// bırakılsalardı test, yakalamanın değil sonuç ağacının davranışını ölçerdi.
    /// </remarks>
    private static CombatTuning CatchOnly { get; } = TestBuilders.PointBlank with
    {
        BaseCatchChance = 1.0,
        BaseDismembermentChance = 0,
        BaseStunChance = 0,

        // Silahın elden düşmesi de aynı yakalamadan çıkar ve kilidin YERİNE geçer; açık
        // bırakılsaydı bu dosyadaki testler kilidi hiç göremezdi. Düşürmenin kendisi
        // DisarmTests'in konusu.
        CatchDisarmChance = 0,
    };

    /// <summary>Yakalama zarının hiç tutmadığı ayar — kontrol tarafı.</summary>
    private static CombatTuning NoCatch { get; } = CatchOnly with { BaseCatchChance = 0 };

    /// <summary>Yakalayan alet: gelen kesici silahı tutar.</summary>
    private static Weapon Hook { get; } =
        new("Test-Jitte", WeaponClass.Blunt, 4, TwoHanded: false, AttackSeconds: 1.0)
        {
            CatchSkill = 1.0,
        };

    /// <summary>Yakalanan silah: tek el kesici.</summary>
    private static Weapon Blade { get; } =
        new("Test-Katana", WeaponClass.Cutting, 30, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>Aynı silahın çift el hâli — kaldıracın cevabını izole eder.</summary>
    private static Weapon HeavyBlade { get; } =
        new("Test-Nodachi", WeaponClass.Cutting, 30, TwoHanded: true, AttackSeconds: 1.0);

    private static BattleSetup Bout(
        Weapon defenderWeapon,
        Weapon attackerWeapon,
        double defenderAccuracy = 60,
        double defenderEvasion = 0,
        double defenderStamina = 100,
        CombatTuning? tuning = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Yakalayan",
                health: 400,
                aggression: 0,
                evasion: defenderEvasion,
                accuracy: defenderAccuracy,
                stamina: defenderStamina,
                weapon: defenderWeapon),
        ],
        [TestBuilders.Warrior(101, "Vuran", aggression: 100, weapon: attackerWeapon)])
    {
        Tuning = tuning ?? CatchOnly,
    };

    /// <summary>Yakalanan vuruş hiç hasar vermez.</summary>
    [Fact]
    public void ACaughtAttackLandsNoDamage()
    {
        // Stamina bol verilir: bedelin kendisi ayrı bir testin konusu
        // (<see cref="CatchingRequiresStamina"/>). Varsayılan 100 stamina yalnızca
        // birkaç yakalamaya yeter ve sonrasında vuruşlar geçmeye başlar — burada
        // ölçülen şey kaynak değil, yakalanan vuruşun hasar vermemesi.
        var battle = new Battle(Bout(Hook, Blade, defenderStamina: 10000), new FixedRandom(0.0));
        battle.Run();

        AttackCaught caught = Assert.IsType<AttackCaught>(
            battle.Events.OfType<AttackCaught>().FirstOrDefault());

        Assert.Equal(new WarriorId(1), caught.Defender);
        Assert.Equal(new WarriorId(101), caught.Attacker);
        Assert.Equal(CatchOnly.CatchBindSeconds, caught.BindSeconds);

        // Yakalayanın canına hiç dokunulmadı: yakalama kaçınma gibi hasarı azaltmaz,
        // vuruşu tamamen siler.
        Assert.DoesNotContain(
            battle.Events.OfType<AttackLanded>(),
            e => e.Defender == new WarriorId(1));
    }

    /// <summary>Silahı yakalanan savaşçı o pencere boyunca yeni saldırı başlatmaz.</summary>
    [Fact]
    public void ABoundAttackerStopsSwinging()
    {
        var battle = new Battle(
            Bout(Hook, Blade, tuning: CatchOnly with { MaxBattleSeconds = 6 }),
            new FixedRandom(0.0));

        double caughtAt = double.NaN;
        int attacksAtCatch = 0;

        while (battle.Step())
        {
            if (double.IsNaN(caughtAt)
                && battle.Events.OfType<AttackCaught>().FirstOrDefault() is AttackCaught c)
            {
                caughtAt = c.AtSeconds;
                attacksAtCatch = AttacksBy(battle, new WarriorId(101));
            }

            if (!double.IsNaN(caughtAt)
                && battle.ElapsedSeconds >= caughtAt + CatchOnly.CatchBindSeconds)
            {
                break;
            }
        }

        Assert.False(double.IsNaN(caughtAt), "Hiçbir vuruş yakalanmadı.");
        Assert.Equal(attacksAtCatch, AttacksBy(battle, new WarriorId(101)));

        static int AttacksBy(Battle battle, WarriorId id) =>
            battle.Events.OfType<AttackStarted>().Count(e => e.Attacker == id);
    }

    /// <summary>
    /// Yakalamanın asıl karşılığı: kilitli savaşçı kaçınamaz.
    /// </summary>
    /// <remarks>
    /// Kural yalnızca hasarı silseydi yakalama pahalı bir kaçınma olurdu. Açılan pencere
    /// olmadan jitte'nin düşük hasarı hiçbir yerde telafi edilmez.
    /// </remarks>
    [Fact]
    public void ABoundWarriorCannotDodge()
    {
        // Yakalayan da vursun; kilitli savaşçının kaçınma zarı denenecek.
        BattleSetup setup = Bout(Hook, Blade) with
        {
            PlayerSide =
            [
                TestBuilders.Warrior(
                    1, "Yakalayan", health: 400, aggression: 100, accuracy: 60, weapon: Hook),
            ],
            EnemySide =
            [
                TestBuilders.Warrior(101, "Vuran", aggression: 100, evasion: 100, weapon: Blade),
            ],
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        AttackCaught caught = battle.Events.OfType<AttackCaught>().First();

        Assert.DoesNotContain(
            battle.Events.OfType<AttackDodged>(),
            e => e.Defender == new WarriorId(101)
                 && e.AtSeconds > caught.AtSeconds
                 && e.AtSeconds <= caught.AtSeconds + CatchOnly.CatchBindSeconds);
    }

    /// <summary>Yakalayacak aleti olmayan savaşçı hiç yakalamaz.</summary>
    [Fact]
    public void AnOrdinaryWeaponNeverCatches()
    {
        var battle = new Battle(Bout(Blade, Blade), new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<AttackCaught>());
    }

    /// <summary>
    /// Çift el silah daha zor yakalanır — yakalamanın kendi cevabı.
    /// </summary>
    /// <remarks>
    /// Sayı değil <b>sıra</b> sınanıyor. Kaldıracın karşılığı olmasaydı jitte her
    /// eşleşmede doğru seçim olur ve ağır silah seçmek bedelsiz bir kayba dönüşürdü.
    /// </remarks>
    [Fact]
    public void TwoHandedWeaponsAreHarderToCatch()
    {
        CombatTuning tuning = CatchOnly with { BaseCatchChance = 0.5, CatchTwoHandedFactor = 0.5 };

        int oneHanded = CatchesAgainst(Blade, tuning);
        int twoHanded = CatchesAgainst(HeavyBlade, tuning);

        Assert.True(
            twoHanded < oneHanded,
            $"Çift el silah daha zor yakalanmadı ({twoHanded} >= {oneHanded}).");

        static int CatchesAgainst(Weapon weapon, CombatTuning tuning)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(Bout(Hook, weapon, tuning: tuning), new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<AttackCaught>().Count();
            }

            return total;
        }
    }

    /// <summary>Yumruk yakalanmaz: ortada tutulacak bir şey yok.</summary>
    [Fact]
    public void FistsCannotBeCaught()
    {
        Assert.Equal(0, Weapon.Fists().CatchFactor);

        var battle = new Battle(Bout(Hook, Weapon.Fists()), new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<AttackCaught>());
    }

    /// <summary>Havada gelen mermi yakalanmaz — kural yalnızca yakın dövüşe aittir.</summary>
    [Fact]
    public void ProjectilesCannotBeCaught()
    {
        BattleSetup setup = new(
            [
                TestBuilders.Warrior(1, "Yakalayan", health: 400, aggression: 0, weapon: Hook),
            ],
            [
                TestBuilders.Warrior(
                    101,
                    "Atıcı",
                    aggression: 100,
                    weapon: Blade,
                    thrown: ThrownWeapon.Shuriken()),
            ])
        {
            Tuning = CatchOnly with { StartOffsetX = 400 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<ProjectileHit>());
        Assert.DoesNotContain(
            battle.Events.OfType<AttackCaught>(),
            e => battle.Events.OfType<ProjectileHit>().Any(h => h.AtSeconds == e.AtSeconds));
    }

    /// <summary>Stamina yetmiyorsa yakalama denenmez.</summary>
    /// <remarks>
    /// Bedelin ölçümdeki karşılığı buydu: bedel sıfırken zafer %76.85, 16'da %72.63 —
    /// üstelik yakalama sayısı neredeyse aynı kalıyor (2.72'ye karşı 2.75). Yani bedel
    /// yakalamayı seyrekleştirerek değil, savaşçıyı <b>yorarak</b> ısırıyor.
    /// </remarks>
    [Fact]
    public void CatchingRequiresStamina()
    {
        var battle = new Battle(
            Bout(Hook, Blade, defenderStamina: 0, tuning: CatchOnly with { MaxBattleSeconds = 4 }),
            new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<AttackCaught>());
    }

    /// <summary>
    /// Çekilen savaşçı yakalamaz: kaçış vaadinin üstüne yeni bir zar konmaz.
    /// </summary>
    /// <remarks>
    /// Sersemletmedeki koruma kuralının eşi (GDD §5). Sırtı dönük koşan savaşçı
    /// karşısındakinin silahına gitmez; kural burada da işleseydi kaçış bir çıkış değil
    /// yeni bir dövüş hamlesi olurdu.
    /// </remarks>
    [Fact]
    public void ARetreatingWarriorNeverCatches()
    {
        var battle = new Battle(Bout(Hook, Blade), new FixedRandom(0.0));

        while (!battle.ContactMade && battle.Step())
        {
        }

        Assert.True(battle.CommandRetreat());

        double commandedAt = battle.ElapsedSeconds;
        battle.Run();

        Assert.DoesNotContain(
            battle.Events.OfType<AttackCaught>(),
            e => e.Defender == new WarriorId(1) && e.AtSeconds > commandedAt);
    }

    /// <summary>
    /// Yakalama İsabet'e bağlıdır, Kaçınma'ya değil.
    /// </summary>
    /// <remarks>
    /// İki savunma ekseni aynı stattan beslenseydi ekipman kararı stat kararının
    /// kopyası olur, jitte yalnızca "kaçınması yüksek savaşçının ikinci savunması"
    /// olarak kalırdı.
    /// </remarks>
    [Fact]
    public void CatchingScalesWithAccuracyNotEvasion()
    {
        CombatTuning tuning = CatchOnly with { BaseCatchChance = 0.4 };

        int lowAccuracy = CatchesWith(accuracy: 0, evasion: 0, tuning);
        int highAccuracy = CatchesWith(accuracy: 100, evasion: 0, tuning);

        Assert.True(
            highAccuracy > lowAccuracy,
            $"İsabet ekseni çalışmadı ({highAccuracy} <= {lowAccuracy}).");

        static int CatchesWith(double accuracy, double evasion, CombatTuning tuning)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(
                    Bout(Hook, Blade, defenderAccuracy: accuracy, defenderEvasion: evasion, tuning: tuning),
                    new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<AttackCaught>().Count();
            }

            return total;
        }
    }

    /// <summary>
    /// Yakalama, kaçınmadan <b>önce</b> denenir.
    /// </summary>
    /// <remarks>
    /// Sıra kuralın ısırıp ısırmadığını belirler: kaçınma önce gelseydi yüksek kaçınmalı
    /// savaşçıda yakalama neredeyse hiç ateşlenmez, jitte "kaçınamayanın son çaresi"
    /// olurdu — oysa asıl karşılığı saldıranı kilitlemek.
    /// </remarks>
    [Fact]
    public void CatchIsTriedBeforeDodge()
    {
        // Kaçınma zarı da tutacak kurulum: her ikisi de açıkken yakalama kazanmalı.
        // Stamina bol, yoksa tükenen yakalama sırayı kaçınmaya bırakır ve test sıranın
        // değil kaynağın davranışını ölçer.
        var battle = new Battle(
            Bout(Hook, Blade, defenderEvasion: 100, defenderStamina: 10000),
            new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<AttackCaught>());
        Assert.DoesNotContain(
            battle.Events.OfType<AttackDodged>(),
            e => e.Defender == new WarriorId(1));
    }

    /// <summary>
    /// Yakalama kapalıyken aynı kurulum vuruşu geçirir — kontrol tarafı.
    /// </summary>
    /// <remarks>
    /// Kuralın gerçekten bir şey yaptığını gösteren tek test bu: yakalama açıkken hasar
    /// yok, kapalıyken var. İkisi yan yana durmazsa "hasar yok" sonucu kurulumun
    /// tesadüfünden de gelebilirdi.
    /// </remarks>
    [Fact]
    public void WithoutTheRuleTheSameBlowLands()
    {
        var battle = new Battle(Bout(Hook, Blade, tuning: NoCatch), new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<AttackCaught>());
        Assert.Contains(battle.Events.OfType<AttackLanded>(), e => e.Defender == new WarriorId(1));
    }
}
