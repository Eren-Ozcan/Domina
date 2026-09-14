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
    /// <remarks>
    /// <b>Re-locked at 0.011 on 2026-09-12 (tenth round)</b>, when the stamina pool was made to bind
    /// (<see cref="Combat.CombatTuning.AttackStaminaCost"/>). At 0.0072 the same bed went from 27.7% of
    /// dojos closing to 13.2% and from 9.4% of last nights won to 43.7% — the trained roster's pool
    /// outlasts an adversary's flat 100. Swept 0.0072 / 0.010 / 0.011 / 0.012 / 0.013 / 0.016 (600 dojos
    /// × 180 days, every system on): closures 13.2 / 26.2 / 30.7 / 33.5 / 34.7 / 39.7% and deaths per
    /// warrior-fight 2.6 / 4.2 / 4.9 / 5.3 / 6.0 / 7.0%. 0.011 is the rung that puts deaths back on the
    /// locked 4.7% and leaves the season survivable.
    ///
    /// <b>Re-derived at 0.010 on 2026-09-13</b>, when the offer board became a two-day queue
    /// (<see cref="OfferLifeDays"/>). A standing job is an older, weaker and therefore cheaper job that
    /// eats a whole day, so the queue costs the dojo income rather than safety: on the old rung the
    /// same bed went from a 17.1% last night to 8.9% and from 28.1 net per fight to 21.9. Swept under
    /// the queue (three seeds × 1600 dojos × 180 days, every system on): at 0.010 / 0.0095 / 0.009 /
    /// 0.0085 the last night is won 16.4 / 17.8 / 19.8 / 20.5%, dojos close 25.6 / 24.1 / 21.7 / 19.7%,
    /// deaths per warrior-fight run 4.1 / 3.9 / 3.7 / 3.4% and the net per fight 27.8 / 28.7 / 29.5 /
    /// 30.2. Against the pre-queue bed (16.4 vs 17.1 last nights, 25.6 vs 26.8 closed, 4.1 vs 4.5%
    /// deaths, 27.8 vs 28.1 net) <b>0.010 is the rung that puts the
    /// season back where it was</b> before the queue — everything below it buys the player a road that
    /// is simply easier.
    /// </remarks>
    public double PowerPerDay { get; init; } = 0.010;

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

    /// <summary>
    /// How many days a posted job stays on the board, the day it was posted included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 1 is the old rule: one job a day, gone by the morning. Above 1 the board becomes a queue — the
    /// jobs of the last few days stand together, each with its own expiry, and the question stops being
    /// "shall I take today's" and becomes "which of these can I get to" (docs/GDD.md §10). It is the
    /// item the clock opened: with the day turning by itself, a job that lives exactly one day rotates
    /// out from under a player who is in the market.
    /// </para>
    /// <para>
    /// It is deliberately short. A long board is a shop: with eight jobs standing there is always a
    /// safe one, the risk decision disappears and the season becomes a picking exercise.
    /// </para>
    /// <para>
    /// <b>Locked at 2 on 2026-09-13, and it cost the curve.</b> A standing job is a job posted on an
    /// earlier day, so it carries that day's power: weaker, and — because the reward follows enemy
    /// health — cheaper, while eating a whole day just the same. Measured over three seeds × 1600
    /// dojos, adding the queue on top of everything else took the last night from 17.1% to 8.9% (life
    /// 2) and 6.1% (life 3) and the net per fight from 28.1 to 21.9 and 18.0; three policies (newest,
    /// best-paying, and one that waits for its wounded) all landed in the same place. The board is not
    /// more dangerous, it is <b>poorer</b>: it fills the season with cheap work on days the dojo would
    /// have trained. Life 2 is the shortest queue that is still a queue, and
    /// <see cref="PowerPerDay"/> was re-derived under it.
    /// </para>
    /// </remarks>
    public int OfferLifeDays { get; init; } = 2;

    /// <summary>
    /// The share of the fee a standing job loses for each day it has been left on the board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The queue must not become a larder.</b> Decided 2026-09-13, against the opposite proposal
    /// that had stood open in docs/GDD.md §10 — that the clerk should sweeten work nobody takes. He
    /// does not: a job sweetened by waiting makes "hoard the postings and come back when the roster is
    /// strong" the correct play, and the day the offer arrives stops being the day to answer it. The
    /// posting loses value instead, so <b>taking it the day it is posted is always the best price</b>
    /// and a standing job is a fallback for a day the dojo could not meet the fresh one, never a plan.
    /// </para>
    /// <para>
    /// The old job was already a slightly worse deal — it carries its posting day's power, so it is
    /// weaker and, the reward following enemy health, cheaper — but by <see cref="PowerPerDay"/> that
    /// is about 1% a day, far too small for a player to feel. This is the same pull written large
    /// enough to read on the card.
    /// </para>
    /// <para>
    /// 0 switches the rule off. The fee never falls below <see cref="StaleFeeFloor"/>: a job that paid
    /// nothing would not be a fallback, it would be a line of dead text on the board.
    /// </para>
    /// </remarks>
    public double StaleFeePerDay { get; init; } = 0.25;

    /// <summary>The least a standing job can pay, as a share of its posting-day fee.</summary>
    public double StaleFeeFloor { get; init; } = 0.4;
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

    /// <summary>
    /// The jobs standing on the board on this day, the newest first.
    /// </summary>
    /// <remarks>
    /// Every one of them is the posting day's own offer, so the board is still a pure function of the
    /// seed: nothing is stored and reloading cannot reroll it. An older job carries the power of the
    /// day it was posted, which is the queue's own small pull — a job left standing is a job that has
    /// gone stale rather than one that has grown, and <see cref="FeeScale"/> prices it that way.
    /// </remarks>
    public IReadOnlyList<EncounterOffer> Board(int day, ulong campaignSeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);

        int life = Math.Max(1, Tuning.OfferLifeDays);
        List<EncounterOffer> board = [];

        for (int posted = day; posted > day - life && posted >= 1; posted--)
        {
            board.Add(Offer(posted, campaignSeed));
        }

        return board;
    }

    /// <summary>The last day a job posted on <paramref name="postedDay"/> can be taken.</summary>
    public int ExpiryOf(int postedDay) => postedDay + Math.Max(1, Tuning.OfferLifeDays) - 1;

    /// <summary>
    /// The share of its posting-day fee a job posted on <paramref name="postedDay"/> pays today.
    /// </summary>
    /// <remarks>
    /// 1 on the day it is posted and falling from there (<see cref="EncounterTuning.StaleFeePerDay"/>).
    /// The whole rule lives in this one function so that the figure the board prints and the gold the
    /// treasury receives cannot drift apart.
    /// </remarks>
    public double FeeScale(int postedDay, int today)
    {
        int age = Math.Max(0, today - postedDay);
        double scale = 1 - (Math.Max(0, Tuning.StaleFeePerDay) * age);
        return Math.Clamp(scale, Math.Clamp(Tuning.StaleFeeFloor, 0, 1), 1);
    }

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
            EnemyKind kind = Pick(power, random);
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
    /// <summary>
    /// The raid he brings to the gate when his bound is spent (docs/GDD.md §10).
    /// </summary>
    /// <remarks>
    /// It is the ordinary curve with his holdings on top of it, not a new kind of fight: the design
    /// builds no boss structure, and a raid the player cannot read as "his men, more of them" would be
    /// a second difficulty system. The party size is deliberately free — the dojo is defending its own
    /// gate, so everyone who can stand may stand.
    /// </remarks>
    /// <param name="day">The day it falls on.</param>
    /// <param name="random">The dojo's own seeded source.</param>
    /// <param name="size">How many men he brings — the province's own dial.</param>
    public EncounterOffer Raid(int day, IRandomSource random, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);
        ArgumentNullException.ThrowIfNull(random);

        double power = Math.Min(Tuning.MaxPower, PowerFor(day, random));
        int count = Math.Max(1, size);

        List<Warrior> enemies = [];
        for (int i = 0; i < count; i++)
        {
            EnemyKind kind = Pick(power, random);
            enemies.Add(kind.Spawn(new WarriorId(FirstEnemyId + (day * 10) + i), power));
        }

        return new EncounterOffer(
            day,
            enemies,
            ThreatBand.Dire,
            $"{count} of Kurogane's men are at the gate",
            RequiredPartySize: null);
    }

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

    private static EnemyKind Pick(double power, IRandomSource random)
    {
        List<EnemyKind> pool = [.. Adversaries.AvailableAt(power)];
        if (pool.Count == 0)
        {
            return Adversaries.Collector;
        }

        double total = pool.Sum(k => k.Weight);
        double roll = random.NextDouble() * total;

        foreach (EnemyKind kind in pool)
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
            " and ",
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
