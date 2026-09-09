using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Campaign;

/// <summary>The difficulty curve's tunable numbers.</summary>
/// <remarks>
/// The numbers are <b>not locked</b>: GDD §10 only says "difficulty rises along a single curve, no boss
/// structure is built". The curve's steepness can only be settled by an expedition-series measurement —
/// the limit the economy pass revealed holds here: once deaths per warrior-fight pass 20%, no price
/// keeps a dojo standing (GDD §11).
/// </remarks>
public sealed record EncounterTuning
{
    /// <summary>Day 1's power.</summary>
    public double StartingPower { get; init; } = 0.9;

    /// <summary>The power each day adds.</summary>
    public double PowerPerDay { get; init; } = 0.02;

    /// <summary>The curve's ceiling — it does not harden forever.</summary>
    /// <remarks>
    /// <b>Locked at 2.2</b> (400 dojos × 180 days, measurement GDD §11). The ceiling has to stop where a
    /// fully grown dojo can still make a profit: the dojo's own growth has a limit (the stat ceiling, a
    /// roster of four, the armour tiers) but the curve had none, and at 3.0 it fell below zero net per
    /// fight in the long run (−0.1), offer refusals rose to 62% and closed dojos to 11.2%. At 2.2 the net
    /// is 25.6, refusals 53.7% and closures 2.5%. A lower ceiling (1.4) kills the decision: the dojo
    /// accepts 96% of the offers. The ceiling is the same as <see cref="DireThreshold"/>: <b>Dire is not
    /// the curve's destination but the fluctuation at its top.</b> It does not affect the first 60 days
    /// at all — the curve does not touch the ceiling until day 66, so the numbers locked earlier stay in
    /// place.
    /// </remarks>
    public double MaxPower { get; init; } = 2.2;

    /// <summary>
    /// The fluctuation share laid on top of the day's power.
    /// </summary>
    /// <remarks>
    /// If the curve were a straight line the same offer would arrive every day and the "take it or leave
    /// it" decision would disappear on its own: the point of leaving it is that tomorrow can be different.
    /// </remarks>
    public double DailyVariance { get; init; } = 0.25;

    /// <summary>The power at which the enemy party starts to grow.</summary>
    public double SecondEnemyAtPower { get; init; } = 1.1;

    /// <summary>The power at which a third enemy is added.</summary>
    public double ThirdEnemyAtPower { get; init; } = 1.6;

    /// <summary>The chance of a duel offer that imposes a single warrior.</summary>
    /// <remarks>
    /// GDD §10: some encounters impose an exact number. The duel is that rule's cheapest showing — it
    /// makes you choose <b>one</b> warrior, not a party.
    /// </remarks>
    public double DuelChance { get; init; } = 0.12;

    public double HeavyThreshold { get; init; } = 1.5;

    public double DireThreshold { get; init; } = 2.2;

    public double RisingThreshold { get; init; } = 1.1;
}

/// <summary>Produces the day's offer.</summary>
/// <remarks>
/// <para>
/// The generation is <b>pure</b>: the same seed and the same day always give the same offer. So the
/// offer does not have to be stored in the save — the file holds the day and the seed, and the offer is
/// recomputed on load. Reloading the save because you did not like the offer changes nothing either.
/// </para>
/// <para>
/// It knows nothing of the engine and takes its randomness from <see cref="IRandomSource"/> (CLAUDE.md → architecture rule).
/// </para>
/// </remarks>
public sealed class EncounterGenerator(EncounterTuning? tuning = null)
{
    /// <summary>Enemy ids start here so they do not collide with the roster.</summary>
    /// <remarks>
    /// <see cref="Dojo.BattleAftermath"/> is already protected by the team filter, but keeping the ids
    /// in a separate band also tells a person reading the log which side they are looking at.
    /// </remarks>
    public const int FirstEnemyId = 100_000;

    public EncounterTuning Tuning { get; } = tuning ?? new EncounterTuning();

    /// <summary>The given day's offer.</summary>
    public EncounterOffer Offer(int day, ulong campaignSeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);
        return Offer(day, new SeededRandom(Mix(campaignSeed, day)));
    }

    /// <summary>An offer whose stream is supplied from outside — for measurement and tests.</summary>
    public EncounterOffer Offer(int day, IRandomSource random)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);
        ArgumentNullException.ThrowIfNull(random);

        double power = PowerFor(day, random);

        bool duel = random.Chance(Tuning.DuelChance);
        int count = duel ? 1 : CountFor(power, random);

        List<Warrior> enemies = [];

        // A crowd does <b>not</b> divide the power: three enemies mean three times the enemy. If it
        // divided, a crowd offer would carry the same threat with less health — that is, it would sell
        // the same risk for less reward, because the reward depends on enemy health (GDD §11).
        double each = power;
        for (int i = 0; i < count; i++)
        {
            YokaiKind kind = Pick(power, random);
            enemies.Add(kind.Spawn(new WarriorId(FirstEnemyId + (day * 10) + i), each));
        }

        return new EncounterOffer(
            day,
            enemies,
            Band(power),
            Sighting(enemies, duel),
            duel ? 1 : null);
    }

    /// <summary>The day's raw power — the curve plus that day's fluctuation.</summary>
    public double PowerFor(int day, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        double curve = Tuning.StartingPower + ((day - 1) * Tuning.PowerPerDay);
        double swing = ((random.NextDouble() * 2) - 1) * Tuning.DailyVariance;

        return Math.Clamp(curve + swing, 0.3, Tuning.MaxPower);
    }

    private ThreatBand Band(double power) => power switch
    {
        var p when p >= Tuning.DireThreshold => ThreatBand.Dire,
        var p when p >= Tuning.HeavyThreshold => ThreatBand.Heavy,
        var p when p >= Tuning.RisingThreshold => ThreatBand.Rising,
        _ => ThreatBand.Faint,
    };

    private int CountFor(double power, IRandomSource random)
    {
        int most = 1;
        if (power >= Tuning.SecondEnemyAtPower)
        {
            most = 2;
        }

        if (power >= Tuning.ThirdEnemyAtPower)
        {
            most = 3;
        }

        // A crowd does not stick to the upper bound: the same power sometimes means one heavy enemy and
        // sometimes three weak ones. The two are not the same fight — one tests target selection, the other endurance.
        return most == 1 ? 1 : 1 + random.NextInt(most);
    }

    private static YokaiKind Pick(double power, IRandomSource random)
    {
        List<YokaiKind> pool = [.. Bestiary.AvailableAt(power)];
        if (pool.Count == 0)
        {
            return Bestiary.Kappa;
        }

        double total = pool.Sum(k => k.Weight);
        double roll = random.NextDouble() * total;

        foreach (YokaiKind kind in pool)
        {
            roll -= kind.Weight;
            if (roll <= 0)
            {
                return kind;
            }
        }

        return pool[^1];
    }

    /// <summary>The rough description read before going in — kind and count, no stats.</summary>
    private static string Sighting(IReadOnlyList<Warrior> enemies, bool duel)
    {
        string names = string.Join(
            " ve ",
            enemies.GroupBy(e => e.Name).Select(g => g.Count() == 1 ? g.Key : $"{g.Count()} {g.Key}"));

        return duel ? $"{names} calls you to a duel" : names;
    }

    /// <summary>Mixes the seed and the day into a single stream.</summary>
    /// <remarks>
    /// If the day were added straight to the seed, consecutive days' streams would be shifted copies of
    /// each other and the offers would repeat visibly.
    /// </remarks>
    private static ulong Mix(ulong seed, int day)
    {
        ulong x = seed ^ ((ulong)day * 0x9E3779B97F4A7C15);
        x ^= x >> 33;
        x *= 0xFF51AFD7ED558CCD;
        x ^= x >> 33;
        return x;
    }
}
