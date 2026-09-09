using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Campaign;

/// <summary>The bounty contracts' tunable numbers.</summary>
/// <remarks>
/// The numbers are <b>not locked</b>. The measurement's question is clear: a contract must not be a
/// more expensive copy of the daily offer — both its risk and its reward have to be clearly different,
/// so that the question "do I take the cheap job today, or stay fresh for the big one in three days"
/// can arise.
/// </remarks>
public sealed record BountyTuning
{
    /// <summary>How often a new contract is posted, in days.</summary>
    /// <remarks>
    /// If a new contract were posted every day, a target you did not like could be swapped by waiting a
    /// day and the contract would be a second copy of the daily offer.
    /// </remarks>
    public int PostingDays { get; init; } = 4;

    /// <summary>How many days the contract stays open from the day it was posted.</summary>
    /// <remarks>
    /// The deadline gives the campaign the one thing it has not had until now: a <b>plannable future</b>.
    /// An open-ended contract would be a warehouse rather than a decision — the player would keep it on
    /// hold until his roster was perfect.
    /// </remarks>
    public int OpenDays { get; init; } = 3;

    /// <summary>The target's strength relative to the same day's ordinary offer.</summary>
    public double PowerMultiplier { get; init; } = 1.8;

    /// <summary>The reward's multiple relative to an ordinary enemy with the same health.</summary>
    /// <remarks>
    /// It has to be greater than one: because the target is single and strong, the party cannot go in
    /// with a numbers advantage, so the same health means more risk. Left at 1, the contract would
    /// mathematically always be a bad deal.
    /// </remarks>
    public double RewardMultiplier { get; init; } = 1.6;

    /// <summary>The honour the party earns for completing the contract.</summary>
    public double HonorReward { get; init; } = 6;

    /// <summary>The honour price of a contract accepted and left to expire.</summary>
    /// <remarks>
    /// Accepting is a <b>promise</b>. Without a price every contract would be accepted and then, if the
    /// right day never came, quietly forgotten — both the deadline and the decision would lose their
    /// meaning.
    public double BrokenHonorPenalty { get; init; } = 10;

    /// <summary>The target's epithet — the name itself comes from the bestiary, the epithet from here.</summary>
    /// <remarks>
    /// Temporary: per GDD §8 the target's name will come from chat while streaming (phase 5) and the
    /// viewer who named him will support the enemy side throughout the fight. The pool will then be read
    /// from the viewer list rather than from here; the contract itself will not change.
    /// </remarks>
    public IReadOnlyList<string> Epithets { get; init; } =
    [
        "Rib-Breaker", "Mouth of the Mist", "Nine Wounds", "The Red Marsh", "Bone-Gatherer",
        "Night-Walker", "Temple-Burner", "Two-Faced", "Silent Step", "Blind Fury",
    ];

    /// <summary>The party that issued the contract — why the target is wanted.</summary>
    /// <remarks>
    /// The patron is not only text: it is the entry point of GDD §6's honour axis into the contract. For
    /// now it does not change the reward, it carries tone.
    /// </remarks>
    public IReadOnlyList<string> Patrons { get; init; } =
    [
        "a village headman", "a temple priest", "the merchants' guild", "the regional lord", "a widowed farmer",
    ];
}

/// <summary>A posted bounty contract.</summary>
/// <remarks>
/// <para>
/// It differs from the daily offer (<see cref="EncounterOffer"/>) in three ways: the target is
/// <b>named</b>, the contract has a <b>deadline</b>, and there is a separate <b>price</b> for accepting
/// and not coming back. Together the three turn the decision from "shall I go in today" into "which day
/// shall I go in".
/// </para>
/// <para>
/// A contract produces its own <see cref="EncounterOffer"/>, because the expedition layer only knows
/// offers. Opening a separate expedition route would mean two different doors into a fight.
/// </para>
/// </remarks>
/// <param name="PostedDay">The day the contract was posted.</param>
/// <param name="Deadline">The last valid day — this day included.</param>
/// <param name="Target">The target; single and strong.</param>
/// <param name="Threat">The band that can be read before going in.</param>
/// <param name="Reward">The gold promised.</param>
/// <param name="Patron">The party that issued the contract.</param>
/// <param name="HonorReward">The honour the party earns on completion.</param>
/// <param name="BrokenHonorPenalty">The honour the roster loses if it is accepted and expires.</param>
public sealed record BountyContract(
    int PostedDay,
    int Deadline,
    Warrior Target,
    ThreatBand Threat,
    int Reward,
    string Patron,
    double HonorReward,
    double BrokenHonorPenalty)
{
    /// <summary>Is the contract still open on this day?</summary>
    public bool IsOpenOn(int day) => day >= PostedDay && day <= Deadline;

    /// <summary>How many days are left, the last day included.</summary>
    public int DaysLeft(int day) => Math.Max(0, Deadline - day + 1);

    /// <summary>The contract's fight — the form the expedition layer knows.</summary>
    /// <remarks>
    /// Because the target is single, no party size is imposed: how many you take is the contract's real
    /// decision. Sending one man and keeping the roster at home is valid, and so is piling all four in —
    /// one takes the risk, the other leaves the dojo undefended that day.
    /// </remarks>
    public EncounterOffer AsOffer(int day) =>
        new(day, [Target], Threat, $"{Target.Name} — wanted by {Patron}");
}

/// <summary>The contract board: it produces which contract is posted on which day.</summary>
/// <remarks>
/// <para>
/// <b>Pure</b> like the offer and the market: the same seed and the same day always give the same
/// contract. The contract is not written to the save, it is recomputed from the day and the seed —
/// loading the save to change a contract you did not like does not work.
/// </para>
/// <para>
/// The board works with <b>period</b> numbers: a contract is posted every
/// <see cref="BountyTuning.PostingDays"/> days and stays open for
/// <see cref="BountyTuning.OpenDays"/> days. Because the two are separate numbers there are days
/// with no contract — if a contract were posted every day, the ordinary offer would become pointless.
/// </para>
/// </remarks>
public sealed class BountyBoard(BountyTuning? tuning = null, EncounterTuning? encounters = null)
{
    /// <summary>Target ids live in their own band above the enemy band.</summary>
    public const int FirstTargetId = 200_000;

    public BountyTuning Tuning { get; } = tuning ?? new BountyTuning();

    private EncounterTuning Encounters { get; } = encounters ?? new EncounterTuning();

    /// <summary>The contract posted on the given day; <c>null</c> if there is none.</summary>
    public BountyContract? Posted(int day, ulong seed, EconomyTuning economy)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);
        ArgumentNullException.ThrowIfNull(economy);

        int posting = Math.Max(1, Tuning.PostingDays);
        int period = (day - 1) / posting;
        int postedDay = (period * posting) + 1;
        int deadline = postedDay + Math.Max(0, Tuning.OpenDays - 1);

        if (day > deadline)
        {
            return null;
        }

        return Build(postedDay, deadline, new SeededRandom(Mix(seed, period)), economy);
    }

    /// <summary>A contract whose stream is supplied from outside — for measurement and tests.</summary>
    public BountyContract Build(
        int postedDay,
        int deadline,
        IRandomSource random,
        EconomyTuning economy)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(economy);

        // The target's strength comes out of the curve of the day the contract was posted: a contract is
        // not something outside the calendar, it is a harder point on the same curve.
        double power = new EncounterGenerator(Encounters).PowerFor(postedDay, random)
            * Math.Max(1, Tuning.PowerMultiplier);

        YokaiKind kind = Pick(power, random);
        Warrior target = kind.Spawn(new WarriorId(FirstTargetId + postedDay), power);

        string epithet = Tuning.Epithets.Count == 0
            ? "Nameless"
            : Tuning.Epithets[random.NextInt(Tuning.Epithets.Count)];
        string patron = Tuning.Patrons.Count == 0
            ? "bilinmeyen bir taraf"
            : Tuning.Patrons[random.NextInt(Tuning.Patrons.Count)];

        target.Name = $"{target.Name} — {epithet}";

        int reward = (int)Math.Round(
            target.EffectiveStats.MaxHealth
            * economy.VictoryGoldPerEnemyHealth
            * Math.Max(0, Tuning.RewardMultiplier));

        return new BountyContract(
            postedDay,
            deadline,
            target,
            Band(power),
            reward,
            patron,
            Tuning.HonorReward,
            Tuning.BrokenHonorPenalty);
    }

    private ThreatBand Band(double power) => power switch
    {
        var p when p >= Encounters.DireThreshold => ThreatBand.Dire,
        var p when p >= Encounters.HeavyThreshold => ThreatBand.Heavy,
        var p when p >= Encounters.RisingThreshold => ThreatBand.Rising,
        _ => ThreatBand.Faint,
    };

    private static YokaiKind Pick(double power, IRandomSource random)
    {
        List<YokaiKind> pool = [.. Bestiary.AvailableAt(power)];
        if (pool.Count == 0)
        {
            return Bestiary.Oni;
        }

        // The contract target is picked from the <b>top</b> end of the curve: the point of a bounty hunt
        // is meeting something you would not run into on an ordinary patrol that day.
        double highest = pool.Max(k => k.MinPower);
        List<YokaiKind> top = [.. pool.Where(k => k.MinPower >= highest)];

        return top[random.NextInt(top.Count)];
    }

    /// <summary>Mixes the seed with the period — with a different salt from the offer, event and market streams.</summary>
    private static ulong Mix(ulong seed, int period)
    {
        ulong x = seed ^ ((ulong)period * 0xD6E8FEB86659FD93) ^ 0x27D4EB2F165667C5;
        x ^= x >> 32;
        x *= 0x9E3779B97F4A7C15;
        x ^= x >> 29;
        return x;
    }
}
