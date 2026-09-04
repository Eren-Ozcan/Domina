using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Zırhın ağırlığı, kuşamı bir <b>karar</b> yapan şeydir. Bedelsizken ō-yoroi her
/// eksende üstündü ve tek freni henüz var olmayan fiyattı. Bu testler ağırlığın iki
/// hattını bağlar: kılıç geç iner, savaşçı yavaş yürür.
/// </summary>
public class ArmorWeightTests
{
    /// <summary>Ağırlığın hiçbir yere dokunmadığı ayar — karşılaştırma tabanı.</summary>
    private static readonly CombatTuning Weightless = TestBuilders.PointBlank with
    {
        ArmorAttackSlowdownAtFullWeight = 0,
    };

    /// <summary>Takımın ağırlığı parçalarının toplamıdır; boş yuva ağırlık taşımaz.</summary>
    [Fact]
    public void ArmorWeighsTheSumOfItsPieces()
    {
        Assert.Equal(0, Armor.None().Weight);
        Assert.Equal(ArmorPiece.Keikogi.Weight, Armor.Light().Weight);
        Assert.True(Armor.Heavy().Weight > Armor.Medium().Weight);
        Assert.Equal(
            ArmorPiece.Kabuto.Weight
            + ArmorPiece.OYoroiCuirass.Weight
            + (ArmorPiece.HeavyKote.Weight * 2)
            + (ArmorPiece.HeavySuneate.Weight * 2),
            Armor.Heavy().Weight);
    }

    /// <summary>
    /// Ağır kuşanan savaşçı aynı sürede daha az saldırı başlatır — ağırlığın ısıran
    /// hattı budur. Hız ve stamina cezaları ölçümde zaferi kıpırdatmadı; dövüş hasar
    /// alışverişiyle bittiği için bedelin oraya inmesi gerekiyor.
    /// </summary>
    [Fact]
    public void HeavyArmorSlowsTheSword()
    {
        int bare = AttacksIn(TestBuilders.PointBlank, Armor.None());
        int heavy = AttacksIn(TestBuilders.PointBlank, Armor.Heavy());

        Assert.True(heavy < bare, $"Ağır kuşam kılıcı yavaşlatmadı ({heavy} >= {bare}).");
    }

    /// <summary>Yavaşlamanın sebebi ağırlık: ceza sıfırlanınca fark kapanır.</summary>
    [Fact]
    public void WithoutThePenaltyArmorDoesNotSlowTheSword()
    {
        Assert.Equal(AttacksIn(Weightless, Armor.None()), AttacksIn(Weightless, Armor.Heavy()));
    }

    /// <summary>
    /// Ağırlık yürüyüşe dokunmaz. Denendi ve geri alındı: yürüme hızına yazılan ceza
    /// zaferi hiç kıpırdatmadı ama §5'in vaadini sildi — kuşanmış savaşçı arenayı terk
    /// edemeden yetişildiği için "Kaç" tuşu ölümü düşürmez oldu (çeken %46.35,
    /// çekilmeyen %46.44; ceza yokken %44.32'ye karşı %46.33).
    /// </summary>
    [Fact]
    public void ArmorDoesNotSlowTheWalk()
    {
        Assert.Equal(DistanceWalked(Armor.None()), DistanceWalked(Armor.Heavy()), 6);
    }

    private static int AttacksIn(CombatTuning tuning, Armor armor)
    {
        var battle = new Battle(
            new BattleSetup(
                [TestBuilders.Warrior(1, health: 4000, armor: armor, evasion: 0)],
                [TestBuilders.Warrior(101, health: 4000, aggression: 0, evasion: 0)])
            {
                Tuning = tuning,
            },
            new SeededRandom(11));

        for (int i = 0; i < 400 && battle.Step(); i++)
        {
            // Sabit sayıda tick: karşılaştırma süreye göre yapılır.
        }

        return battle.Events.OfType<AttackStarted>().Count(e => e.Attacker == new WarriorId(1));
    }

    private static double DistanceWalked(Armor armor)
    {
        var battle = new Battle(
            new BattleSetup(
                [TestBuilders.Warrior(1, armor: armor)],
                [TestBuilders.Warrior(101, speed: 0)]),
            new SeededRandom(11));

        double start = battle.SnapshotOf(new WarriorId(1)).Position.X;
        for (int i = 0; i < 40 && battle.Step(); i++)
        {
            // Yaklaşma aşaması.
        }

        return Math.Abs(battle.SnapshotOf(new WarriorId(1)).Position.X - start);
    }
}
