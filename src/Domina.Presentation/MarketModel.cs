using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>The readable band of talent.</summary>
/// <remarks>
/// Talent is not shown <b>as a number</b> on screen. A stat is what you have and is written with its
/// exact figure; talent is a <b>promise</b> — writing "1.23" makes it look like a measured stat and
/// turns the market into a spreadsheet. A band gives just enough information to decide.
/// </remarks>
public enum TalentBand
{
    /// <summary>Clearly below average.</summary>
    Dull,

    /// <summary>Around average.</summary>
    Fair,

    /// <summary>Above average.</summary>
    Promising,

    /// <summary>The market's top end.</summary>
    Rare,
}

/// <summary>A single row in the market — one candidate and today's verdict on him.</summary>
/// <param name="Index">
/// The candidate's place within <see cref="DojoState.Recruits"/>; the screen returns it when buying.
/// The name is not unique (the same name can appear on two candidates), so the identity is the index.
/// </param>
/// <param name="Name">The candidate's name.</param>
/// <param name="Stats">The visible stats — nothing is hidden in the bargaining.</param>
/// <param name="Band">The talent band.</param>
/// <param name="Price">The gold asked.</param>
/// <param name="Affordable">Is there enough gold in the treasury?</param>
/// <param name="Bought">Was he bought today? A bought candidate stays at the stall but is not sold.</param>
/// <param name="Score">The total stat score — the single number the rows are compared by.</param>
/// <param name="BetterInRoster">
/// <b>How many living</b> warriors on the roster are better than this candidate. If zero, he is
/// better than everyone you have today.
/// </param>
public readonly record struct MarketRow(
    int Index,
    string Name,
    WarriorStats Stats,
    TalentBand Band,
    int Price,
    bool Affordable,
    bool Bought,
    double Score,
    int BetterInRoster);

/// <summary>The numbers standing at the top of the market.</summary>
/// <param name="Gold">The gold in the treasury.</param>
/// <param name="Candidates">The number of candidates standing in the market today.</param>
/// <param name="Affordable">The number of candidates that can be bought today — those already bought do not count.</param>
/// <param name="Bought">The number of candidates bought today.</param>
/// <param name="DaysToRefresh">
/// In how many days the market refreshes; a full period if it refreshed today.
/// </param>
/// <param name="BestLivingScore">The score of the best living warrior on the roster; 0 if the roster is empty.</param>
public readonly record struct MarketSummary(
    int Gold,
    int Candidates,
    int Affordable,
    int Bought,
    int DaysToRefresh,
    double BestLivingScore);

/// <summary>
/// The model the market screen reads. It computes the comparison and the verdict; it does not draw.
/// </summary>
/// <remarks>
/// If the screen read <see cref="DojoState.Recruits"/> directly, two jobs would leak: where the
/// candidate stands <b>relative to the roster</b>, and how talent should be read. The first is the
/// market's real question ("is this man better than what I have"), the second is deliberately kept
/// vague.
/// </remarks>
public static class MarketModel
{
    /// <summary>Today's market, from cheapest to most expensive.</summary>
    /// <remarks>
    /// The order is by <b>price</b>, not by the treasury: affordability changes as gold is spent, and the
    /// order would shift with every purchase. The market stall must not be rearranged to fit the player's
    /// pocket — which candidate stands where should stay fixed all day.
    /// </remarks>
    public static IReadOnlyList<MarketRow> Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        IReadOnlyList<RecruitOffer> stock = dojo.Recruits;
        List<double> living = [.. dojo.Roster.Living.Select(e => Score(e.Warrior.BaseStats))];

        return stock
            .Select((offer, index) => Describe(
                offer,
                index,
                dojo.Resources.Gold,
                living,
                dojo.HiredToday.Contains(index)))
            .OrderBy(row => row.Price)
            .ThenBy(row => row.Index)
            .ToList();
    }

    /// <summary>A single candidate's row.</summary>
    /// <param name="offer">Pazardaki aday.</param>
    /// <param name="index">The candidate's place in the stock.</param>
    /// <param name="gold">The gold in the treasury.</param>
    /// <param name="livingScores">The scores of the living warriors on the roster.</param>
    /// <param name="bought">Was the candidate bought today?</param>
    public static MarketRow Describe(
        RecruitOffer offer,
        int index,
        int gold,
        IReadOnlyCollection<double> livingScores,
        bool bought = false)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(livingScores);

        double score = Score(offer.Stats);

        return new MarketRow(
            Index: index,
            Name: offer.Name,
            Stats: offer.Stats,
            Band: BandOf(offer.Talent),
            Price: offer.Price,
            Affordable: offer.Price <= gold,
            Bought: bought,
            Score: score,
            BetterInRoster: livingScores.Count(s => s > score));
    }

    /// <summary>The numbers at the top of the market.</summary>
    public static MarketSummary Summarize(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        IReadOnlyList<RecruitOffer> stock = dojo.Recruits;
        List<double> living = [.. dojo.Roster.Living.Select(e => Score(e.Warrior.BaseStats))];

        return new MarketSummary(
            Gold: dojo.Resources.Gold,
            Candidates: stock.Count,
            Affordable: stock
                .Where((o, i) => o.Price <= dojo.Resources.Gold && !dojo.HiredToday.Contains(i))
                .Count(),
            Bought: dojo.HiredToday.Count,
            DaysToRefresh: DaysToRefresh(dojo),
            BestLivingScore: living.Count == 0 ? 0 : living.Max());
    }

    /// <summary>The days left until the market refreshes — today not included.</summary>
    /// <remarks>
    /// With the default setting the stall refreshes every day, so this number is 1; in a measurement
    /// setting (<see cref="MarketTuning.RefreshDays"/>) it can grow. It has to be written on screen: a
    /// player who does not know how many days the stall will stand passes over a list he does not like
    /// thinking "it changes tomorrow" and postpones the decision for nothing at a stall that is not moving.
    /// </remarks>
    public static int DaysToRefresh(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        int period = Math.Max(1, dojo.Market.Tuning.RefreshDays);
        return period - ((dojo.Day - 1) % period);
    }

    /// <summary>The total stat score the candidates are compared by.</summary>
    /// <remarks>
    /// The formula must be <b>the same</b> as <see cref="RecruitMarket"/>'s ceiling calculation: if the
    /// market is clipping a candidate because he hit the ceiling, why he cannot pass the roster's best
    /// should be readable on screen from the same number. Stamina is left out — the ceiling does not
    /// count it either.
    /// </remarks>
    public static double Score(WarriorStats stats) =>
        stats.MaxHealth
        + stats.Strength
        + stats.Accuracy
        + stats.Defense
        + stats.Evasion
        + stats.Speed
        + stats.Aggression;

    private static TalentBand BandOf(double talent) => talent switch
    {
        < 0.85 => TalentBand.Dull,
        < 1.05 => TalentBand.Fair,
        < 1.25 => TalentBand.Promising,
        _ => TalentBand.Rare,
    };
}
