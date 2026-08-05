using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Determinizm, Faz 1'in pazarlık edilemez şartıdır: sonradan eklenemez, çünkü
/// rastgelelik her yere sızmış olur. Bu testler bozulursa denge çalışması ve
/// hata tekrar üretimi (replay) imkânsız hale gelir.
/// </summary>
public class DeterminismTests
{
    private static BattleSetup ThreeVsThree() => new(
        [
            TestBuilders.Warrior(1, evasion: 30, defense: 20),
            TestBuilders.Warrior(2, evasion: 20, defense: 30, weapon: Weapon.Nodachi()),
            TestBuilders.Warrior(3, evasion: 40, defense: 10, weapon: Weapon.Yari()),
        ],
        [
            TestBuilders.Warrior(101, "Oni", health: 140, aggression: 55, defense: 25, weapon: Weapon.Tetsubo()),
            TestBuilders.Warrior(102, "Kappa", health: 70, aggression: 65, evasion: 35),
            TestBuilders.Warrior(103, "Tengu", health: 80, aggression: 70, evasion: 50),
        ]);

    [Fact]
    public void SameSeedProducesIdenticalOutcome()
    {
        BattleResult First() => new Battle(ThreeVsThree(), new SeededRandom(20260805)).Run();

        BattleResult baseline = First();

        for (int i = 0; i < 100; i++)
        {
            BattleResult repeat = First();

            Assert.Equal(baseline.Outcome, repeat.Outcome);
            Assert.Equal(baseline.ElapsedSeconds, repeat.ElapsedSeconds, precision: 9);
            Assert.Equal(baseline.Summaries.Count, repeat.Summaries.Count);

            for (int s = 0; s < baseline.Summaries.Count; s++)
            {
                Assert.Equal(baseline.Summaries[s], repeat.Summaries[s]);
            }
        }
    }

    [Fact]
    public void SameSeedProducesIdenticalEventStream()
    {
        var a = new Battle(ThreeVsThree(), new SeededRandom(7)).Run();
        var b = new Battle(ThreeVsThree(), new SeededRandom(7)).Run();

        Assert.Equal(a.Outcome, b.Outcome);

        // Olay akışı görselleştirmeyi besliyor; birebir aynı olmalı ki bir hata
        // "şu seed'de şu an" diye tekrar üretilebilsin.
        var eventsA = new Battle(ThreeVsThree(), new SeededRandom(7));
        eventsA.Run();
        var eventsB = new Battle(ThreeVsThree(), new SeededRandom(7));
        eventsB.Run();

        Assert.Equal(eventsA.Events.Count, eventsB.Events.Count);
        for (int i = 0; i < eventsA.Events.Count; i++)
        {
            Assert.Equal(eventsA.Events[i], eventsB.Events[i]);
        }
    }

    [Fact]
    public void DifferentSeedsProduceDifferentBattles()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (ulong seed = 1; seed <= 40; seed++)
        {
            var battle = new Battle(ThreeVsThree(), new SeededRandom(seed));
            BattleResult result = battle.Run();
            seen.Add($"{result.Outcome}|{result.ElapsedSeconds:F2}|{battle.Events.Count}");
        }

        // Aynı kurulum farklı seed'lerde aynı dövüşü üretiyorsa rastgelelik akmıyordur.
        Assert.True(seen.Count > 1, "Farklı seed'ler ayırt edilebilir dövüşler üretmeli.");
    }

    [Fact]
    public void SeededRandomIsStableAcrossInstances()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);

        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(a.NextDouble(), b.NextDouble());
        }
    }

    [Fact]
    public void SeededRandomStaysInRange()
    {
        var rng = new SeededRandom(1);

        for (int i = 0; i < 10_000; i++)
        {
            double d = rng.NextDouble();
            Assert.InRange(d, 0.0, 0.9999999999);
            Assert.InRange(rng.NextInt(6), 0, 5);
        }
    }

    [Fact]
    public void ChanceHandlesCertainty()
    {
        var rng = new SeededRandom(3);

        Assert.False(rng.Chance(0));
        Assert.True(rng.Chance(1));
    }
}
