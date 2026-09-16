using System.Globalization;
using Domina.Core.Dojo;

namespace Domina.Presentation;

/// <summary>One of the four things taking a man in decides.</summary>
/// <param name="Label">The card's own small label — "he eats from tonight".</param>
/// <param name="Figure">The figure it turns on, read at a glance.</param>
/// <param name="Line">What that figure means for the term.</param>
/// <param name="Grave">Whether it is a cost rather than a gain.</param>
public readonly record struct GateCard(string Label, string Figure, string Line, bool Grave);

/// <summary>The man in the gateway, as the sheet reads him.</summary>
/// <param name="Name">What he is called — the viewer's own name, put on a man.</param>
/// <param name="Line">His grade and what he is carrying.</param>
/// <param name="Cards">What taking him in costs, and buys.</param>
/// <param name="CanFeed">Whether the store can carry another mouth at all.</param>
public readonly record struct GateSheet(string Name, string Line, IReadOnlyList<GateCard> Cards, bool CanFeed);

/// <summary>
/// A man standing in the gateway: what taking him in costs, and what it buys.
/// </summary>
/// <remarks>
/// <para>
/// The decision is a store decision before it is a fighting one — a fourth body is a fourth mouth, and
/// the design's sheet leads with the days the rice loses rather than with his grade (design canvas →
/// 9b). So the first card is the store, counted before and after him.
/// </para>
/// <para>
/// Nothing here knows the man came from the chat. The gate hands over a name and a drawn warrior; what
/// the sheet says about him would be the same if he had walked up the road.
/// </para>
/// </remarks>
public static class GateModel
{
    /// <summary>Reads the gateway against the dojo as it stands today.</summary>
    /// <param name="dojo">The dojo he would be joining.</param>
    /// <param name="name">His name.</param>
    /// <param name="grade">His grade, as the market rates him.</param>
    /// <param name="carrying">What he has on him.</param>
    public static GateSheet Describe(DojoState dojo, string name, int grade, string carrying)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        Resources purse = dojo.Resources;
        Resources draw = dojo.DailyDraw();

        int mouths = Math.Max(1, dojo.Roster.Living.Count());
        int ricePer = Math.Max(1, draw.Food / mouths);
        int waterPer = Math.Max(1, draw.Water / mouths);

        int riceNow = draw.Food <= 0 ? 0 : purse.Food / draw.Food;
        int riceThen = purse.Food / Math.Max(1, draw.Food + ricePer);
        int waterThen = purse.Water / Math.Max(1, draw.Water + waterPer);

        List<GateCard> cards =
        [
            new GateCard(
                "he eats from tonight",
                $"rice {Figure(riceNow)} → {Figure(riceThen)} days",
                $"He draws about {Figure(ricePer)} rice and {Figure(waterPer)} water a day, and the "
                + $"water then holds {Figure(waterThen)} days.",
                Grave: riceThen <= StripModel.Pressing),

            new GateCard(
                "he is worth, today",
                Worth(grade),
                $"{Ordinal(grade)} grade, carrying {carrying}. What he is worth in a fight is what he "
                + "is worth today, not what he might become.",
                Grave: false),

            new GateCard(
                "in six weeks, if he lives",
                dojo.Staff.Has(StaffRole.DrillMaster) ? "a full step or two" : "half of that",
                dojo.Staff.Has(StaffRole.DrillMaster)
                    ? "The drill master is on the post, so what he learns at it counts for more."
                    : "There is no drill master on the post, so the yard trains at the bare rate.",
                Grave: !dojo.Staff.Has(StaffRole.DrillMaster)),

            new GateCard(
                "and if he dies",
                "his name goes on the stone",
                "He dies like anyone else in the yard, and the name he was given dies with him.",
                Grave: true),
        ];

        return new GateSheet(
            name,
            $"{Ordinal(grade)} grade · {carrying}",
            cards,
            purse.Food > draw.Food);
    }

    /// <summary>What a grade is worth in a fight, said as a body rather than as a number.</summary>
    private static string Worth(int grade) => grade switch
    {
        <= 3 => "a body in the line",
        <= 7 => "a flank, for a few exchanges",
        <= 12 => "a man who can be sent out alone",
        _ => "a blade the province has heard of",
    };

    private static string Ordinal(int grade) => grade switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{Figure(grade)}th",
    };

    private static string Figure(int value) => value.ToString(CultureInfo.InvariantCulture);
}
