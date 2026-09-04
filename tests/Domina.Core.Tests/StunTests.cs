using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Sersemletme, künt silahın var oluş sebebidir (GDD §7). Kesici uzuv koparır
/// (<c>DismembermentFactor</c> 1.0), künt koparmaz (0.15) — karşılığında savaşçıyı
/// donduran darbeyi indirir. Bu testler takasın iki ucunu da bağlar: künt vuruş
/// dondurur, donan savaşçı ne vurur ne kaçınır.
/// </summary>
public class StunTests
{
    /// <summary>Uzuv kopma dalını kapatan ayar — sersemletme tek başına ölçülsün.</summary>
    /// <remarks>
    /// İki zar aynı ağır darbeden atılıyor; kopma açık kalsaydı test, sersemleyen
    /// savaşçının uzvunu da kaybettiği bir kurulumu ölçerdi ve hangi kuralın ne yaptığı
    /// ayrışmazdı.
    /// </remarks>
    private static CombatTuning StunOnly { get; } = TestBuilders.PointBlank with
    {
        BaseDismembermentChance = 0,

        // Eşik düşürülür ki kurban tek dövüşte birden fazla ağır darbe yiyebilsin:
        // sertlik/can oranı sabit olduğu için eşiği aşan darbe aynı zamanda kurbanı
        // birkaç vuruşta öldüren darbedir. Sınanan şey eşiğin sayısı değil kural.
        StunSeverityThreshold = 0.05,
        TorsoHitWeight = 100,
        HeadHitWeight = 0,
        ArmHitWeight = 0,
        LegHitWeight = 0,
    };

    /// <summary>Tek vuruşta ağır darbe eşiğini aşan künt silah.</summary>
    private static Weapon Club { get; } =
        new("Test-Kanabō", WeaponClass.Blunt, 40, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>
    /// Kurbanın elindeki zararsız silah: dövüş tek yönlü kalsın diye.
    /// </summary>
    /// <remarks>
    /// Kurban yumrukla bile karşılık verirse hücumla varan darbesi dövücüyü sersemletir
    /// ve testler kimin sersemlediğini ayırt edemez. Ölçülen şey karşılıklı dövüş değil,
    /// tek bir darbenin sonucu.
    /// </remarks>
    private static Weapon Harmless { get; } =
        new("Test-Sopa", WeaponClass.Cutting, 0, TwoHanded: false, AttackSeconds: 1.0);

    /// <summary>Aynı sertlikte kesici silah — sınıf farkını izole eder.</summary>
    private static Weapon Blade { get; } =
        new("Test-Katana", WeaponClass.Cutting, 40, TwoHanded: false, AttackSeconds: 1.0);

    private static BattleSetup Beating(
        Weapon attackerWeapon,
        Armor? victimArmor = null,
        double victimAggression = 0,
        double victimEvasion = 0,
        CombatTuning? tuning = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Kurban",
                health: 400,
                aggression: victimAggression,
                evasion: victimEvasion,
                weapon: Harmless,
                armor: victimArmor),
        ],
        [TestBuilders.Warrior(101, "Dövücü", aggression: 100, weapon: attackerWeapon)])
    {
        Tuning = tuning ?? StunOnly,
    };

    /// <summary>Künt silahın ağır darbesi savaşçıyı dondurur.</summary>
    [Fact]
    public void ABluntGrievousBlowStunsTheDefender()
    {
        var battle = new Battle(Beating(Club), new FixedRandom(0.0));
        battle.Run();

        WarriorStunned stun = Assert.IsType<WarriorStunned>(
            battle.Events.OfType<WarriorStunned>().FirstOrDefault());

        Assert.Equal(new WarriorId(1), stun.Defender);
        Assert.Equal(new WarriorId(101), stun.Attacker);
        Assert.Equal(StunOnly.StunSeconds, stun.Seconds);
    }

    /// <summary>
    /// Takasın kendisi: aynı sertlikteki kesici silah çok daha seyrek sersemletir.
    /// </summary>
    /// <remarks>
    /// Sayı değil <b>sıra</b> sınanıyor. Künt sınıf kopma çarpanında kesiciye kaybeder;
    /// bu eksende kazanmazsa künt silah her eksende kötüdür ve kimse kuşanmaz.
    /// </remarks>
    [Fact]
    public void BluntStunsFarMoreOftenThanCutting()
    {
        Assert.True(Club.StunFactor > Blade.StunFactor);
        Assert.True(Club.DismembermentFactor < Blade.DismembermentFactor);

        int blunt = StunsIn(Club);
        int cutting = StunsIn(Blade);

        Assert.True(blunt > cutting, $"Künt sersemletmede öne geçmedi ({blunt} <= {cutting}).");

        static int StunsIn(Weapon weapon)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(Beating(weapon), new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<WarriorStunned>().Count();
            }

            return total;
        }
    }

    /// <summary>Sersemleyen savaşçı o pencere boyunca hiç saldırı başlatmaz.</summary>
    [Fact]
    public void AStunnedWarriorStopsSwinging()
    {
        var battle = new Battle(
            Beating(Club, victimAggression: 100, tuning: StunOnly with { MaxBattleSeconds = 6 }),
            new FixedRandom(0.0));

        double stunnedAt = double.NaN;
        int attacksAtStun = 0;

        while (battle.Step())
        {
            if (double.IsNaN(stunnedAt)
                && battle.Events.OfType<WarriorStunned>().FirstOrDefault() is WarriorStunned s)
            {
                stunnedAt = s.AtSeconds;
                attacksAtStun = AttacksBy(battle, new WarriorId(1));
            }

            if (!double.IsNaN(stunnedAt) && battle.ElapsedSeconds >= stunnedAt + StunOnly.StunSeconds)
            {
                break;
            }
        }

        Assert.False(double.IsNaN(stunnedAt), "Kurban hiç sersemlemedi.");
        Assert.Equal(attacksAtStun, AttacksBy(battle, new WarriorId(1)));

        static int AttacksBy(Battle battle, WarriorId id) =>
            battle.Events.OfType<AttackStarted>().Count(e => e.Attacker == id);
    }

    /// <summary>Sersemleyen savaşçı kaçınamaz — donmanın asıl bedeli budur.</summary>
    [Fact]
    public void AStunnedWarriorCannotDodge()
    {
        // Kaçınma zarı her seferinde tutacak şekilde: yüksek Kaçınma + FixedRandom(0).
        var setup = Beating(Club, victimEvasion: 100);
        var battle = new Battle(setup, new FixedRandom(0.0));
        battle.Run();

        WarriorStunned stun = battle.Events.OfType<WarriorStunned>().First();

        Assert.DoesNotContain(
            battle.Events.OfType<AttackDodged>(),
            e => e.Defender == new WarriorId(1)
                 && e.AtSeconds > stun.AtSeconds
                 && e.AtSeconds <= stun.AtSeconds + StunOnly.StunSeconds);
    }

    /// <summary>
    /// Çekilen savaşçı sersemlemez: künt silahlı düşman oyuncunun tek müdahalesini
    /// (GDD §5) tek zarla iptal edemez.
    /// </summary>
    [Fact]
    public void RetreatIsNeverCancelledByAStun()
    {
        var battle = new Battle(Beating(Club), new FixedRandom(0.0));

        while (!battle.ContactMade && battle.Step())
        {
        }

        Assert.True(battle.CommandRetreat());

        double commandedAt = battle.ElapsedSeconds;
        battle.Run();

        // Komuttan SONRA sersemleme yok. Aynı tick'teki darbe komuttan önce düştü —
        // tuşu açan darbe zaten oydu.
        Assert.DoesNotContain(
            battle.Events.OfType<WarriorStunned>(),
            e => e.Defender == new WarriorId(1) && e.AtSeconds > commandedAt);

        Assert.Contains(battle.Events.OfType<RetreatStarted>(), e => e.Warrior == new WarriorId(1));
    }

    /// <summary>
    /// Sersemletme kaçış komutunu <b>yutmaz</b>, geciktirir: süre bitince buffer'lanmış
    /// komut işlenir ve savaşçı çekilmeye başlar.
    /// </summary>
    [Fact]
    public void AStunDelaysTheRetreatCommandButDoesNotEatIt()
    {
        var battle = new Battle(Beating(Club), new FixedRandom(0.0));

        // Önce sersemlemesini bekle, sonra tuşa bas.
        while (!battle.Events.OfType<WarriorStunned>().Any() && battle.Step())
        {
        }

        Assert.True(battle.CommandRetreat());
        Assert.Contains(battle.Events.OfType<RetreatBuffered>(), e => e.Warrior == new WarriorId(1));

        battle.Run();

        Assert.Contains(battle.Events.OfType<RetreatStarted>(), e => e.Warrior == new WarriorId(1));
    }

    /// <summary>Zırh sersemletmeyi de damperler — ama kesiği durdurduğundan azını.</summary>
    /// <remarks>
    /// Payın (<see cref="CombatTuning.ArmorStunResistanceShare"/>) tek işi bu: künt
    /// kuvvet plakanın altından geçer, o yüzden zırhın kopma direnci sersemletmeye
    /// birebir sayılmaz.
    /// </remarks>
    [Fact]
    public void ArmorDampensStunButLessThanItDampensSevering()
    {
        int bare = StunsWith(Armor.None());
        int plated = StunsWith(Armor.Heavy());

        Assert.True(plated < bare, $"Zırh sersemletmeyi azaltmadı ({plated} >= {bare}).");
        Assert.True(plated > 0, "Zırh sersemletmeyi tamamen kesti — pay 1'e kaymış olmalı.");

        static int StunsWith(Armor armor)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(Beating(Club, victimArmor: armor), new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<WarriorStunned>().Count();
            }

            return total;
        }
    }

    /// <summary>Kafaya inen darbe daha sık sersemletir — kabuto'nun dövüş içi karşılığı.</summary>
    [Fact]
    public void AHeadBlowStunsMoreOftenThanATorsoBlow()
    {
        int torso = StunsWithHits(StunOnly);
        int head = StunsWithHits(StunOnly with
        {
            TorsoHitWeight = 0,
            HeadHitWeight = 100,
        });

        Assert.True(head > torso, $"Kafa darbesi öne geçmedi ({head} <= {torso}).");

        static int StunsWithHits(CombatTuning tuning)
        {
            int total = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var battle = new Battle(Beating(Club, tuning: tuning), new SeededRandom(seed));
                battle.Run();
                total += battle.Events.OfType<WarriorStunned>().Count();
            }

            return total;
        }
    }

    /// <summary>Eşiğin altındaki hafif darbe sersemletmez — sersemletme ağır darbe dalıdır.</summary>
    [Fact]
    public void ALightBlowNeverStuns()
    {
        var pinprick = new Weapon("Test-Tantō", WeaponClass.Blunt, 6, TwoHanded: false, AttackSeconds: 0.4);
        var battle = new Battle(Beating(pinprick), new FixedRandom(0.0));
        battle.Run();

        Assert.Contains(battle.Events.OfType<AttackLanded>(), e => e.Defender == new WarriorId(1));
        Assert.Empty(battle.Events.OfType<WarriorStunned>());
    }
}
