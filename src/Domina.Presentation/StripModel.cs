using System.Globalization;
using Domina.Core.Dojo;

namespace Domina.Presentation;

/// <summary>One store as the strip prints it.</summary>
/// <param name="Figure">The stock itself — the thing read at a glance.</param>
/// <param name="Name">What it is, and how long it lasts: <c>"rice · 13 days"</c>.</param>
/// <param name="Days">How many days the stock covers at the present rate; <c>null</c> when nothing draws on it.</param>
/// <param name="Pressing">Whether the days left are few enough that the figure is a problem today.</param>
public readonly record struct StripStore(string Figure, string Name, int? Days, bool Pressing);

/// <summary>The strip along the top of the world, as words.</summary>
/// <param name="Day">The day — <c>"Day 23"</c>.</param>
/// <param name="Term">The term it sits in — <c>"of 60"</c>.</param>
/// <param name="Stores">The stores, in the order the strip prints them.</param>
public readonly record struct StripLine(string Day, string Term, IReadOnlyList<StripStore> Stores);

/// <summary>
/// Works out what the one piece of always-on interface says. It produces text, it does not draw.
/// </summary>
/// <remarks>
/// <para>
/// The strip is the only thing on screen at every moment of a term, so it is also the only place a
/// store can be read without walking anywhere. That makes the days-left figure beside each store the
/// most load-bearing number in the game: a stock on its own ("3 medicine") says nothing until it is
/// set against the men who want it, and the reference game's worst habit is printing the stock and
/// never the rate (docs/REFERENCE-DOMINA.md).
/// </para>
/// <para>
/// The arithmetic lives here rather than in the screen for the usual reason: it is the kind of thing
/// that has to be the same on the strip, on the ledger and in a balance run, and none of those three
/// may open the engine to find out (CLAUDE.md → architecture rule).
/// </para>
/// </remarks>
public static class StripModel
{
    /// <summary>A store with this many days left or fewer is pressing today.</summary>
    public const int Pressing = 2;

    /// <summary>Reads the strip off the dojo as it now stands.</summary>
    public static StripLine Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        Resources purse = dojo.Resources;
        Resources draw = dojo.DailyDraw();

        return new StripLine(
            Day: $"Day {dojo.Day.ToString(CultureInfo.InvariantCulture)}",
            Term: $"of {dojo.Season.Tuning.Days.ToString(CultureInfo.InvariantCulture)}",
            Stores:
            [
                Store(purse.Gold, "koku", draw.Gold),
                Store(purse.Food, "rice", draw.Food),
                Store(purse.Water, "water", draw.Water),
                Store(purse.Medicine, "medicine", draw.Medicine),
            ]);
    }

    /// <summary>One store: the stock, its name, and what the name says about how long it lasts.</summary>
    private static StripStore Store(int stock, string name, int draw)
    {
        if (draw <= 0)
        {
            // Nothing draws on it today. A rate invented for a store nothing is spending would be a
            // lie the player would plan around — the gold line does this whenever there is no payroll.
            return new StripStore(
                stock.ToString(CultureInfo.InvariantCulture),
                name,
                Days: null,
                Pressing: false);
        }

        int days = stock / draw;

        return new StripStore(
            stock.ToString(CultureInfo.InvariantCulture),
            $"{name} · {days.ToString(CultureInfo.InvariantCulture)} days",
            days,
            days <= Pressing);
    }
}
