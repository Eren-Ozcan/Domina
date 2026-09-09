using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Dojo;

/// <summary>A candidate in the market — with his stats and his price.</summary>
/// <remarks>
/// <para>
/// Buying a warrior should be a <b>choice</b>, not a button: candidates come with different stats, the
/// stats are <b>visible</b> before the purchase, and the price comes out of the stats themselves. With
/// a fixed-stat, fixed-price warrior there is no question of "whom shall I buy"; if you have the money
/// you buy, if not you do not.
/// <para>
/// A candidate does not join the roster until he is bought: the <see cref="Warrior"/> object is only
/// created at the moment of purchase. Having six candidates wandering the market carry permanent
/// identities would pollute the same identity space as the dead warriors.
/// </para>
/// </remarks>
/// <param name="Name">The candidate's name.</param>
/// <param name="Stats">The visible stats — nothing is hidden in the bargaining.</param>
/// <param name="Talent">
/// How quickly he benefits from training (1.0 = average) — it multiplies the gain of a training day
/// directly (<see cref="TrainingGround"/>).
/// </param>
/// <param name="Price">The gold asked.</param>
public sealed record RecruitOffer(string Name, WarriorStats Stats, double Talent, int Price);

/// <summary>The slave market's tunable numbers.</summary>
/// <remarks>
/// The numbers are <b>not locked</b>. The measurement's question is clear: buying a cheap raw candidate
/// and training him, and buying an expensive ready-made one, should be <b>rivals</b> — if one is always
/// right, the market is a button again.
/// </remarks>
public sealed record MarketTuning
{
    /// <summary>The number of candidates standing in the market at once.</summary>
    /// <remarks>
    /// <para>
    /// Ten candidates, a wide stall: buying should be <b>choosing</b>, not making do with what is there.
    /// The same scale as the reference game's stall.
    /// </para>
    /// <para>
    /// The number's <b>balance effect was measured and there is none</b>: over 400 dojos × 60 days, 4, 6,
    /// 8 and 10 candidates give the same band (ending treasury 1184-1301, death 9.7-10.0%). What binds
    /// the market is not the list length but the stat ceiling (<see cref="BestFollowCeiling"/>) and the
    /// treasury. So this number is not a balance lever but the <b>feel</b> the screen gives.
    /// </para>
    /// </remarks>
    public int Candidates { get; init; } = 10;

    /// <summary>How often the market refreshes, in days.</summary>
    /// <remarks>
    /// <para>
    /// The stall refreshes <b>every day</b>. Waiting already has a price: waiting one day eats a day
    /// (food, water and medicine are paid and no expedition goes out that day), so "something better
    /// will come tomorrow" is not a free postponement.
    /// </para>
    /// <para>
    /// The stall is still frozen <b>within</b> the day (<see cref="DojoState.Recruits"/>): the player can
    /// enter and leave the market whenever he likes during the day but cannot reroll the list by buying.
    /// A bought candidate also goes on the record, and the same man is not sold twice
    /// (<see cref="DojoState.HireRecruit"/>).
    /// </para>
    /// </remarks>
    public int RefreshDays { get; init; } = 1;

    /// <summary>How far a candidate's stats swing around the base.</summary>
    public double Spread { get; init; } = 0.35;

    /// <summary>
    /// How closely the market tracks the roster's level (0 = not at all, 1 = fully).
    /// </summary>
    /// <remarks>
    /// In the early game there are no master warriors in the market; as the roster develops so does the
    /// market. Without tracking, either everything would be buyable from the start (training would mean
    /// nothing) or the market would become entirely pointless in the late game.
    /// </remarks>
    public double RosterFollow { get; init; } = 0.7;

    /// <summary>
    /// The upper bound of the market's best candidate relative to the dojo's <b>best warrior</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Average tracking (<see cref="RosterFollow"/>) says where the market <i>sits</i> but not how high
    /// it can <i>climb</i>: when <see cref="Spread"/> hits from above, a single candidate can approach
    /// the roster's best. This ceiling cuts that off — a bought warrior can never exceed this share of
    /// the best one you have.
    /// </para>
    /// <para>
    /// The reference is <b>the best warrior, not the average</b>: tied to the average, the market could
    /// be exploited by buying two cheap recruits to drag the average down. The best warrior cannot be
    /// dragged down, he can only be lost by dying — and when he dies it is right for the ceiling to fall too.
    /// </para>
    /// <para>
    /// The rationale: a trained warrior should be the player's <b>work</b>; if the market can copy him,
    /// training means nothing. The market is a <b>replacement</b> tool, not a <b>progress</b> tool. With
    /// the stat ceiling hard, the only thing the market can sell at full strength is
    /// <see cref="RecruitOffer.Talent"/> — the road to progress runs through buying a raw candidate and
    /// training him.
    /// </para>
    /// </remarks>
    public double BestFollowCeiling { get; init; } = 0.75;

    /// <summary>The lower bound of talent.</summary>
    public double MinTalent { get; init; } = 0.6;

    /// <summary>The upper bound of talent.</summary>
    public double MaxTalent { get; init; } = 1.4;

    /// <summary>Talent's effect on price — at 1.0 talent does not change the price.</summary>
    public double TalentPriceWeight { get; init; } = 0.5;

    /// <summary>The name pool the market uses.</summary>
    /// <remarks>
    /// Temporary: per GDD §8 the names will come from chat while streaming (phase 5). The pool will then
    /// be read from the viewer list rather than from here; the market itself will not change.
    /// </remarks>
    public IReadOnlyList<string> Names { get; init; } =
    [
        "Kenji", "Hana", "Takeshi", "Ayame", "Ren", "Kaede", "Jiro", "Sora",
        "Michi", "Haruki", "Yuki", "Daichi", "Nozomi", "Kaito", "Rin", "Sato",
    ];
}

/// <summary>The base the market is generated around and the ceiling it cannot pass.</summary>
/// <remarks>
/// The two answer different questions: <paramref name="Stats"/> says <b>where</b> the candidates sit,
/// <paramref name="CeilingScore"/> says <b>how high</b> they can climb. The base tracks the roster's
/// average, the ceiling the roster's best.
/// </remarks>
/// <param name="Stats">The base stats the candidates are swung around.</param>
/// <param name="CeilingScore">
/// The upper bound of a candidate's total stat score; infinity for no bound.
/// </param>
public sealed record MarketAnchor(WarriorStats Stats, double CeilingScore)
{
    /// <summary>An uncapped base — for measurement and tests.</summary>
    public static MarketAnchor Uncapped(WarriorStats stats) =>
        new(stats, double.PositiveInfinity);
}

/// <summary>Produces the day's slave market.</summary>
/// <remarks>
/// <b>Pure</b> like the offer and the event: the same seed, the same period and the same roster level
/// always give the same candidates. The market refreshes every <see cref="MarketTuning.RefreshDays"/>
/// days, so what is mixed in is not the day number but the <b>period</b> number.
/// </remarks>
public sealed class RecruitMarket(MarketTuning? tuning = null)
{
    public MarketTuning Tuning { get; } = tuning ?? new MarketTuning();

    /// <summary>The candidates on the given day.</summary>
    /// <param name="anchor">
    /// The base the market is generated around and the ceiling it cannot pass — see
    /// <see cref="AnchorFor(Roster)"/>.
    /// </param>
    public IReadOnlyList<RecruitOffer> Stock(int day, ulong seed, MarketAnchor anchor, int basePrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);

        int period = (day - 1) / Math.Max(1, Tuning.RefreshDays);
        return Stock(new SeededRandom(Mix(seed, period)), anchor, basePrice);
    }

    /// <summary>A market whose stream is supplied from outside — for measurement and tests.</summary>
    public IReadOnlyList<RecruitOffer> Stock(IRandomSource random, MarketAnchor anchor, int basePrice)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(anchor);

        List<RecruitOffer> stock = [];
        for (int i = 0; i < Tuning.Candidates; i++)
        {
            stock.Add(Draw(random, anchor, basePrice));
        }

        return stock;
    }

    /// <summary>An uncapped market — for measurement and tests.</summary>
    public IReadOnlyList<RecruitOffer> Stock(IRandomSource random, WarriorStats anchor, int basePrice) =>
        Stock(random, MarketAnchor.Uncapped(anchor), basePrice);

    /// <summary>Derives the market's base and ceiling by looking at the roster.</summary>
    /// <remarks>
    /// <para>
    /// The <b>base</b> sits between the roster's average and the recruit level
    /// (<see cref="MarketTuning.RosterFollow"/>). With an empty roster (everyone dead) the base is the
    /// recruit stats — otherwise the market would collapse after the dojo did and there would be no way
    /// back.
    /// </para>
    /// <para>
    /// The <b>ceiling</b> tracks not the average but the roster's <b>best warrior</b>
    /// (<see cref="MarketTuning.BestFollowCeiling"/>): no bought warrior should surpass the best one
    /// trained by hand. With an empty roster the ceiling is the recruit score, so the first purchases
    /// stay in the recruit band too.
    /// </para>
    /// </remarks>
    public MarketAnchor AnchorFor(Roster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        List<Warrior> living = [.. roster.Living.Select(e => e.Warrior)];
        WarriorStats recruit = WarriorStats.Recruit();
        if (living.Count == 0)
        {
            return new MarketAnchor(recruit, Score(recruit));
        }

        WarriorStats average = new(
            living.Average(w => w.BaseStats.MaxHealth),
            living.Average(w => w.BaseStats.Aggression),
            living.Average(w => w.BaseStats.Defense),
            living.Average(w => w.BaseStats.Evasion),
            living.Average(w => w.BaseStats.Strength),
            living.Average(w => w.BaseStats.Accuracy),
            living.Average(w => w.BaseStats.MaxStamina),
            living.Average(w => w.BaseStats.Speed));

        double follow = Math.Clamp(Tuning.RosterFollow, 0, 1);
        double best = living.Max(w => Score(w.BaseStats));

        // The ceiling never falls below the recruit level: otherwise it would bite from the very first
        // day and the market would sell a recruit roster men weaker than a recruit — the replacement road
        // would close and the dojo could not recover (measured: all 400 dojos zeroed their treasury).
        // The ceiling only kicks in once the best warrior clearly passes a recruit.
        double ceiling = Math.Max(
            Score(recruit),
            best * Math.Max(0, Tuning.BestFollowCeiling));

        return new MarketAnchor(Blend(recruit, average, follow), ceiling);
    }

    private RecruitOffer Draw(IRandomSource random, MarketAnchor anchor, int basePrice)
    {
        double talent = Tuning.MinTalent
            + (random.NextDouble() * Math.Max(0, Tuning.MaxTalent - Tuning.MinTalent));

        WarriorStats around = anchor.Stats;
        WarriorStats stats = Capped(
            new WarriorStats(
                Roll(random, around.MaxHealth, cap: false),
                Roll(random, around.Aggression),
                Roll(random, around.Defense),
                Roll(random, around.Evasion),
                Roll(random, around.Strength),
                Roll(random, around.Accuracy),
                Roll(random, around.MaxStamina, cap: false),
                Roll(random, around.Speed)),
            anchor.CeilingScore);

        string name = Tuning.Names.Count == 0
            ? "Nameless"
            : Tuning.Names[random.NextInt(Tuning.Names.Count)];

        return new RecruitOffer(name, stats, talent, Price(stats, talent, around, basePrice));
    }

    /// <summary>Pulls a candidate over the ceiling down by scaling his stats.</summary>
    /// <remarks>
    /// Instead of clipping them one by one, they are all scaled by <b>the same ratio</b>: clipping would
    /// turn every candidate who hits the ceiling into the same flat profile and the question "whom shall
    /// I buy" would disappear again. Scaling preserves the candidate's shape and only lowers his weight.
    /// </remarks>
    private static WarriorStats Capped(WarriorStats stats, double ceilingScore)
    {
        double score = Score(stats);
        if (double.IsInfinity(ceilingScore) || ceilingScore <= 0 || score <= ceilingScore)
        {
            return stats;
        }

        double scale = ceilingScore / score;
        return new WarriorStats(
            Math.Max(1, stats.MaxHealth * scale),
            Math.Max(1, stats.Aggression * scale),
            Math.Max(1, stats.Defense * scale),
            Math.Max(1, stats.Evasion * scale),
            Math.Max(1, stats.Strength * scale),
            Math.Max(1, stats.Accuracy * scale),
            Math.Max(1, stats.MaxStamina * scale),
            Math.Max(1, stats.Speed * scale));
    }

    /// <summary>Swings a single stat around the base.</summary>
    private double Roll(IRandomSource random, double around, bool cap = true)
    {
        double swing = ((random.NextDouble() * 2) - 1) * Tuning.Spread;
        double value = around * (1 + swing);

        return cap ? Math.Clamp(value, 1, 95) : Math.Max(1, value);
    }

    /// <summary>
    /// The price comes out of how good the candidate is <b>relative to the base</b>.
    /// </summary>
    /// <remarks>
    /// A fixed price would make a good candidate free and a bad one a robbery. Talent enters the price
    /// too but with less weight than the stats: talent is a <b>promise</b>, a stat is what you have.
    /// olan.
    /// </remarks>
    private int Price(WarriorStats stats, double talent, WarriorStats anchor, int basePrice)
    {
        double ratio = Score(anchor) <= 0 ? 1 : Score(stats) / Score(anchor);
        double talentRatio = 1 + ((talent - 1) * Tuning.TalentPriceWeight);

        return Math.Max(1, (int)Math.Round(basePrice * ratio * talentRatio));
    }

    private static double Score(WarriorStats stats) =>
        stats.MaxHealth
        + stats.Strength
        + stats.Accuracy
        + stats.Defense
        + stats.Evasion
        + stats.Speed
        + stats.Aggression;

    private static WarriorStats Blend(WarriorStats a, WarriorStats b, double towardsB) => new(
        Lerp(a.MaxHealth, b.MaxHealth, towardsB),
        Lerp(a.Aggression, b.Aggression, towardsB),
        Lerp(a.Defense, b.Defense, towardsB),
        Lerp(a.Evasion, b.Evasion, towardsB),
        Lerp(a.Strength, b.Strength, towardsB),
        Lerp(a.Accuracy, b.Accuracy, towardsB),
        Lerp(a.MaxStamina, b.MaxStamina, towardsB),
        Lerp(a.Speed, b.Speed, towardsB));

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    /// <summary>Mixes the seed with the <b>period</b> — with a different salt from the offer and event streams.</summary>
    private static ulong Mix(ulong seed, int period)
    {
        ulong x = seed ^ ((ulong)period * 0xC2B2AE3D27D4EB4F) ^ 0x5DEECE66D;
        x ^= x >> 30;
        x *= 0xBF58476D1CE4E5B9;
        x ^= x >> 27;
        return x;
    }
}
