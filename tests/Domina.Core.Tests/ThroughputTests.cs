using System.Diagnostics;
using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Faz 1'in kabul kriteri: <b>10.000 dövüş 10 saniyenin altında koşmalı</b>.
/// </summary>
/// <remarks>
/// Bu bir mikro-optimizasyon testi değil, mimarinin sağlık kontrolüdür. Denge
/// çalışması "bir sayıyı değiştir, on binlerce dövüş koştur, orana bak" döngüsüyle
/// yürür; bu döngü dakikalar sürerse pratikte kimse denge yapmaz. Çekirdeğe motor
/// bağımlılığı veya dövüş başına ağır bir ayırma sızarsa ilk burada görülür.
/// </remarks>
public class ThroughputTests
{
    private const int _battles = 10_000;
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(10);

    private static BattleSetup ThreeVsThree() => new(
        [
            TestBuilders.Warrior(1, evasion: 30, defense: 20, armor: Armor.Light()),
            TestBuilders.Warrior(2, evasion: 20, defense: 30, weapon: Weapon.Nodachi(), armor: Armor.Medium()),
            TestBuilders.Warrior(3, evasion: 40, defense: 10, weapon: Weapon.Yari()),
        ],
        [
            TestBuilders.Warrior(101, "Oni", health: 140, aggression: 55, defense: 25, weapon: Weapon.Tetsubo()),
            TestBuilders.Warrior(102, "Kappa", health: 70, aggression: 65, evasion: 35),
            TestBuilders.Warrior(103, "Tengu", health: 80, aggression: 70, evasion: 50),
        ])
    {
        // Toplu simülasyonun gerçek koşulu: olay akışı biriktirilmez.
        CollectEvents = false,
    };

    [Fact]
    public void TenThousandBattlesRunWithinTheBudget()
    {
        BattleSetup setup = ThreeVsThree();

        // Kadro tekrar kullanılıyor; Battle savaşçıların kalıcı halini değiştirmez.
        long started = Stopwatch.GetTimestamp();
        int finished = 0;

        for (int i = 0; i < _battles; i++)
        {
            BattleResult result = new Battle(setup, new SeededRandom((ulong)i + 1)).Run();
            if (result.Outcome != BattleOutcome.TimeLimit)
            {
                finished++;
            }
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

        Assert.True(
            elapsed < _budget,
            $"{_battles} dövüş {elapsed.TotalSeconds:F2} sn sürdü; bütçe {_budget.TotalSeconds:F0} sn.");

        // Dövüşlerin çoğu gerçekten sonuçlanmalı — hepsi süre dolarak bitseydi
        // "hızlı" olması hiçbir şey kanıtlamazdı.
        Assert.True(finished > _battles * 0.9, $"{_battles} dövüşün yalnızca {finished} tanesi sonuçlandı.");
    }

    [Fact]
    public void PerBattleAllocationStaysSmallWithoutEvents()
    {
        BattleSetup setup = ThreeVsThree();

        // Isınma: JIT ve ilk ayırmalar ölçüme karışmasın.
        for (int i = 0; i < 50; i++)
        {
            _ = new Battle(setup, new SeededRandom((ulong)i)).Run();
        }

        // Süreç geneli değil, bu iş parçacığı: testler paralel koştuğu için
        // GC.GetTotalAllocatedBytes ölçümü başka testlerin ayırmalarıyla kirlenirdi.
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 200; i++)
        {
            _ = new Battle(setup, new SeededRandom((ulong)i)).Run();
        }

        long perBattle = (GC.GetAllocatedBytesForCurrentThread() - before) / 200;

        // Dövüş başına birkaç KB (savaşçı durumları + özet) beklenir. Bunun çok
        // üstü, olay akışının veya başka bir listenin sızdığı anlamına gelir.
        Assert.True(perBattle < 16 * 1024, $"Dövüş başına {perBattle} bayt ayrıldı.");
    }
}
