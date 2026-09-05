using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Silahın elden düşmesi zırhın ikinci cevabıdır: plaka darbeyi durdurmakla kalmaz,
/// vuranın kavrayışını da bozar. Bu testler kuralın iki kaynağını (zırha vurmak,
/// yakalanmak), üç sınırını (çıplak et düşürmez, mermi düşürmez, yumruk düşmez) ve
/// düşen silahın <b>yerden alınmasını</b> bağlar.
/// </summary>
public class DisarmTests
{
    /// <summary>Düşürmenin izole edildiği ayar: kopma ve sersemletme dalları kapalı.</summary>
    /// <remarks>
    /// Üçü de aynı vuruştan çıkar. Açık bırakılsalardı savunan daha ilk darbede düşer,
    /// düşürme zarına sıra hiç gelmezdi.
    /// </remarks>
    private static CombatTuning DisarmOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDisarmChance = 1.0,
        CatchDisarmChance = 0,
        BaseDismembermentChance = 0,
        BaseStunChance = 0,
        MaxBattleSeconds = 12,
    };

    /// <summary>Yerden almanın kapalı olduğu ayar — düşmenin kendi sonucunu yalıtır.</summary>
    private static CombatTuning NoPickup { get; } = DisarmOnly with { WeaponPickupRadius = 0 };

    /// <summary>Düşürme zarının hiç tutmadığı ayar — kontrol tarafı.</summary>
    private static CombatTuning NoDisarm { get; } = DisarmOnly with { BaseDisarmChance = 0 };

    /// <summary>Kesici, tek el, hafif: düşünce kaybedilen şey görünür olsun diye.</summary>
    private static Weapon Blade { get; } =
        new("Test-Katana", WeaponClass.Cutting, 20, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>Aynı silahın künt hâli — sınıf ekseni yalıtılsın diye tek fark sınıf.</summary>
    private static Weapon Club { get; } =
        new("Test-Tetsubo", WeaponClass.Blunt, 20, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>Sert plaka: düşürme zarı vurulan parçanın direncinden beslenir.</summary>
    private static Armor Plate { get; } =
        Armor.Uniform("Test-Plaka", new ArmorPiece("Test-Parça", 0, DismembermentResistance: 1.0, Weight: 0));

    private static BattleSetup Bout(
        Weapon attackerWeapon,
        Armor? defenderArmor = null,
        CombatTuning? tuning = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Kuşanan",
                health: 4000,
                aggression: 0,
                weapon: Weapon.Fists(),
                armor: defenderArmor ?? Plate),
        ],
        [TestBuilders.Warrior(101, "Vuran", health: 4000, aggression: 100, weapon: attackerWeapon)])
    {
        Tuning = tuning ?? NoPickup,
    };

    /// <summary>Zırha inen vuruş saldıranın silahını elinden düşürür.</summary>
    [Fact]
    public void AnArmoredBlowKnocksTheWeaponLoose()
    {
        var battle = new Battle(Bout(Blade), new FixedRandom(0.0));
        battle.Run();

        WeaponDropped dropped = battle.Events.OfType<WeaponDropped>().First();

        // Düşüren kimse yok: kavrayışı bozan şey plakadan dönen darbedir.
        Assert.Equal(new WarriorId(101), dropped.Warrior);
        Assert.Null(dropped.Disarmer);
        Assert.Equal(Blade.Name, dropped.Weapon);
    }

    /// <summary>Çıplak bölgeye inen vuruş hiç düşürmez — kural kendi kendini sınırlar.</summary>
    /// <remarks>
    /// Sertlik vurulan parçanın kopma direncinden okunur ve çıplak bölgenin direnci
    /// sıfırdır. Bu sınır olmasaydı zırhsız düşmanla dövüşen savaşçı da silahını
    /// elinden kaçırır, kural zırhın cevabı olmaktan çıkardı.
    /// </remarks>
    [Fact]
    public void ABlowOnBareFleshNeverDisarms()
    {
        var battle = new Battle(Bout(Blade, Armor.None()), new FixedRandom(0.0));
        battle.Run();

        Assert.Empty(battle.Events.OfType<WeaponDropped>());
    }

    /// <summary>Künt silahın elden çıkma eğilimi kesicinin beşte biridir.</summary>
    /// <remarks>
    /// Zar sınıftan besleniyor; test sayıyı değil <b>sırayı</b> bağlar: aynı plakaya
    /// aynı zarla vuran künt silah, kesici düşerken avuçta kalmalı.
    /// </remarks>
    [Fact]
    public void ABluntWeaponKeepsTheGripWhereABladeLosesIt()
    {
        // 0.3: kesicinin şansının (1.0 × 1.0 × 1.0) altında, küntünkinin (0.2) üstünde.
        var blade = new Battle(Bout(Blade), new FixedRandom(0.3));
        blade.Run();

        var club = new Battle(Bout(Club), new FixedRandom(0.3));
        club.Run();

        Assert.NotEmpty(blade.Events.OfType<WeaponDropped>());
        Assert.Empty(club.Events.OfType<WeaponDropped>());
    }

    /// <summary>Silahını düşüren savaşçı dövüşü yumrukla sürdürür.</summary>
    [Fact]
    public void ADisarmedWarriorKeepsFightingWithFists()
    {
        var battle = new Battle(Bout(Blade), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WeaponDropped dropped = battle.Events.OfType<WeaponDropped>().First();
        List<AttackLanded> after =
        [
            .. battle.Events.OfType<AttackLanded>()
                .Where(a => a.Attacker == dropped.Warrior && a.AtSeconds > dropped.AtSeconds),
        ];

        Assert.NotEmpty(after);

        // Düşmeden sonraki vuruşlar yumruğun hasarını taşır: kesicininkinden düşük.
        double beforeDamage = battle.Events.OfType<AttackLanded>()
            .First(a => a.Attacker == dropped.Warrior).Damage;
        Assert.All(after, a => Assert.True(a.Damage < beforeDamage));

        WarriorBattleSummary attacker = result.Summaries.First(s => s.Id == new WarriorId(101));
        Assert.True(attacker.Disarmed);
        Assert.Equal(1, attacker.TimesDisarmed);
    }

    /// <summary>Silah bir kez düşer; yumruğun düşecek bir şeyi yoktur.</summary>
    [Fact]
    public void FistsCannotBeDropped()
    {
        var battle = new Battle(Bout(Blade), new FixedRandom(0.0));
        battle.Run();

        Assert.Single(battle.Events.OfType<WeaponDropped>());
    }

    /// <summary>Düşen silah yok olmaz: sahibi ona yürüyüp geri alabilir.</summary>
    /// <remarks>
    /// Kırılma yerine düşme seçilmesinin bütün karşılığı budur. Bedel kalıcı bir kayıp
    /// değil, silaha kadar yürünen ve yumrukla geçen süredir.
    /// </remarks>
    [Fact]
    public void TheOwnerCanWalkBackToItsWeapon()
    {
        // İki ayar: karşıdaki savaşçı silahlıdır (düşen kılıcı o almasın) ve silah tam
        // dibine düşer. Silah normalde karşıdakinin arkasına savrulur ve teke tekte
        // oraya varılamaz (bkz. Battle.DropPoint); ölçülen şey burada geometri değil,
        // sahibinin silahını geri alabilmesi.
        BattleSetup setup = new(
            [
                TestBuilders.Warrior(1, "Kuşanan", health: 4000, aggression: 0, weapon: Club, armor: Plate),
            ],
            [TestBuilders.Warrior(101, "Vuran", health: 4000, aggression: 100, weapon: Blade)])
        {
            Tuning = DisarmOnly with
            {
                MaxBattleSeconds = 30,
                WeaponDropDistance = 0,
                WeaponPickupRadius = 120,
            },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WeaponPickedUp picked = battle.Events.OfType<WeaponPickedUp>().First();

        Assert.Equal(new WarriorId(101), picked.Warrior);
        Assert.Equal(Blade.Name, picked.Weapon);

        WarriorBattleSummary attacker = result.Summaries.First(s => s.Id == new WarriorId(101));
        Assert.True(attacker.WeaponsPickedUp > 0);
    }

    /// <summary>Yerdeki silahı eli boş olan <b>herkes</b> alabilir — düşman da.</summary>
    /// <remarks>
    /// Silahın kime ait olduğu sorulmaz: arenada duran bir namludur. Kural bu yüzden
    /// tek yönlü değil — düşürdüğün silah düşmanın eline geçebilir.
    /// </remarks>
    [Fact]
    public void AnyEmptyHandedWarriorCanTakeIt()
    {
        var battle = new Battle(
            Bout(Blade, tuning: DisarmOnly with { MaxBattleSeconds = 30 }),
            new FixedRandom(0.0));
        battle.Run();

        // Kuşanan taraf yumrukla dövüşüyor: eli boş sayılır ve düşen kılıca yürür.
        Assert.Contains(
            battle.Events.OfType<WeaponPickedUp>(),
            p => p.Warrior == new WarriorId(1));
    }

    /// <summary>Elinde silah olan ne alır ne arar.</summary>
    /// <remarks>
    /// Bu sınır olmasaydı savaşçılar sürekli daha iyi silah toplar, dövüş bir yağma
    /// turuna dönerdi.
    /// </remarks>
    [Fact]
    public void AnArmedWarriorNeverPicksAnythingUp()
    {
        BattleSetup setup = new(
            [
                TestBuilders.Warrior(1, "Kuşanan", health: 4000, aggression: 0, weapon: Club, armor: Plate),
            ],
            [TestBuilders.Warrior(101, "Vuran", health: 4000, aggression: 100, weapon: Blade)])
        {
            Tuning = DisarmOnly with { MaxBattleSeconds = 30 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<WeaponDropped>());
        Assert.DoesNotContain(
            battle.Events.OfType<WeaponPickedUp>(),
            p => p.Warrior == new WarriorId(1));
    }

    /// <summary>Yakalanan silah avuçtan sökülebilir — ve kilidin yerine geçer.</summary>
    /// <remarks>
    /// Düşürme kilidin üstüne binseydi yakalama aleti tek zarda hem açık pencereyi hem
    /// silahı alırdı; hasarda kaybettiğinin karşılığı fazlasıyla ödenirdi.
    /// </remarks>
    [Fact]
    public void ACaughtWeaponCanBeTornLooseInsteadOfBound()
    {
        CombatTuning catching = NoPickup with
        {
            BaseDisarmChance = 0,
            BaseCatchChance = 1.0,
            CatchDisarmChance = 1.0,
        };

        BattleSetup setup = new(
            [
                TestBuilders.Warrior(
                    1,
                    "Yakalayan",
                    health: 4000,
                    aggression: 0,
                    weapon: new Weapon("Test-Jitte", WeaponClass.Blunt, 4, false, 1.0)
                    {
                        CatchSkill = 1.0,
                    }),
            ],
            [TestBuilders.Warrior(101, "Saldıran", health: 4000, aggression: 100, weapon: Blade)])
        {
            Tuning = catching,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        WeaponDropped dropped = battle.Events.OfType<WeaponDropped>().First();

        // Burada düşüren belli: çengeli tutan savaşçı.
        Assert.Equal(new WarriorId(101), dropped.Warrior);
        Assert.Equal(new WarriorId(1), dropped.Disarmer);

        // Elden çıkan silahla birlikte kenetlenme çözülür: o anda kilit olayı çıkmaz.
        Assert.DoesNotContain(
            battle.Events.OfType<AttackCaught>(),
            c => Math.Abs(c.AtSeconds - dropped.AtSeconds) < 1e-9);
    }

    /// <summary>Mermi kimsenin silahını düşürmez.</summary>
    /// <remarks>
    /// Kavrayışı bozan şey silahın plakaya çarpıp geri tepmesidir; fırlatılan silah
    /// zaten elden çıkmıştır.
    /// </remarks>
    [Fact]
    public void ProjectilesDisarmNobody()
    {
        BattleSetup setup = new(
            [
                TestBuilders.Warrior(1, "Kuşanan", health: 4000, aggression: 0, armor: Plate),
            ],
            [
                TestBuilders.Warrior(
                    101,
                    "Atan",
                    health: 4000,
                    aggression: 100,
                    weapon: Weapon.Fists(),
                    thrown: ThrownWeapon.Shuriken()),
            ])
        {
            // Taraflar uzakta başlar: yakınken savaşçı yumruğu seçer, mermi hiç havalanmaz.
            Tuning = NoPickup with { StartOffsetX = 400 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        Assert.NotEmpty(battle.Events.OfType<ProjectileHit>());
        Assert.Empty(battle.Events.OfType<WeaponDropped>());
    }

    /// <summary>Düşürme kalıcı hale dokunmaz — dövüş savaşçının silahını almaz.</summary>
    /// <remarks>
    /// Toplu simülasyon aynı kadroyu on binlerce kez koşturur; dövüş kalıcı hali
    /// değiştirseydi ikinci dövüş birincinin kalıntısıyla başlardı.
    /// </remarks>
    [Fact]
    public void DisarmingDoesNotTouchThePermanentWeapon()
    {
        BattleSetup setup = Bout(Blade);
        Warrior attacker = setup.EnemySide[0];

        new Battle(setup, new FixedRandom(0.0)).Run();

        Assert.Equal(Blade.Name, attacker.Weapon.Name);

        var second = new Battle(setup, new FixedRandom(0.0));
        second.Run();

        Assert.NotEmpty(second.Events.OfType<WeaponDropped>());
    }

    /// <summary>Kural kapalıyken hiçbir silah düşmez — kontrol tarafı ayakta.</summary>
    [Fact]
    public void TheRuleCanBeTurnedOff()
    {
        var battle = new Battle(Bout(Blade, tuning: NoDisarm), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        Assert.Empty(battle.Events.OfType<WeaponDropped>());
        Assert.All(result.Summaries, s => Assert.False(s.Disarmed));
    }
}
