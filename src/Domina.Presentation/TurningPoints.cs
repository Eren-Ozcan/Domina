using System.Globalization;
using Domina.Core.Dojo;

namespace Domina.Presentation;

/// <summary>One day the term turned on, as the closing sheet prints it.</summary>
/// <param name="Day">The day — <c>"day 22"</c>.</param>
/// <param name="Line">What happened, and what it cost.</param>
/// <param name="Grave">Whether it is one of the days that cost the term rather than a day that hurt.</param>
public readonly record struct TurningPoint(string Day, string Line, bool Grave);

/// <summary>
/// Where a term turned: the handful of days a closing sheet can point at.
/// </summary>
/// <remarks>
/// <para>
/// A term lost on day 41 was lost somewhere around day 22, and a closing screen that cannot say where
/// teaches the player nothing about the next one (design canvas → 9a, "where it turned"). The core
/// keeps the days as facts — <see cref="DojoState.Marks"/> — and the wording is here.
/// </para>
/// <para>
/// Only a few are printed. The list is the term's whole misfortune and a player reading twenty lines
/// reads none of them, so the gravest kinds are kept first and the rest fall off the bottom.
/// </para>
/// </remarks>
public static class TurningPoints
{
    /// <summary>How many days the sheet prints.</summary>
    public const int Most = 5;

    /// <summary>Reads the term's marks back as lines, gravest first, earliest first within a kind.</summary>
    public static IReadOnlyList<TurningPoint> Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        return [.. dojo.Marks
            .Select(Tell)
            .Where(point => point is not null)
            .Select(point => point!.Value)
            .OrderByDescending(point => point.Grave)
            .Take(Most)];
    }

    /// <summary>The one line that says what a day cost.</summary>
    private static TurningPoint? Tell(TermMark mark)
    {
        string day = $"day {mark.Day.ToString(CultureInfo.InvariantCulture)}";

        return mark.Kind switch
        {
            TermMarkKind.MissedWeek => new TurningPoint(
                day,
                "A week closed with nothing filed at the clerk's office. Standing is spent by sitting "
                + "still, and nothing else has to go wrong for it to fall.",
                Grave: false),

            TermMarkKind.WentHungry => new TurningPoint(
                day,
                $"The store could not feed {Men(mark.Count)}. A hungry man neither heals nor trains, "
                + "so the day is spent twice.",
                Grave: true),

            TermMarkKind.StaffWalked => new TurningPoint(
                day,
                $"The payroll could not be met and {Left(mark.Count)} walked out. The buildings stay; "
                + "the people who make them worth anything do not.",
                Grave: true),

            TermMarkKind.Sacked => new TurningPoint(
                day,
                "The rival stood in the yard and nobody answered him. He took what he came for and the "
                + "province watched it happen.",
                Grave: true),

            TermMarkKind.BountyBroken => new TurningPoint(
                day,
                "A bounty was accepted and not brought in. The office files a broken promise under the "
                + "school's own name.",
                Grave: true),

            TermMarkKind.VillageLost => new TurningPoint(
                day,
                $"{mark.Subject} went over to him. A village is not a fight you can ask for back.",
                Grave: false),

            TermMarkKind.ManCondemned => new TurningPoint(
                day,
                $"{mark.Subject} was granted the blade, with the yard watching.",
                Grave: true),

            TermMarkKind.ManPardoned => new TurningPoint(
                day,
                $"{mark.Subject} was called to stand and the crowd called him bushi.",
                Grave: false),

            _ => null,
        };
    }

    private static string Men(int many) =>
        many == 1
            ? "one man"
            : $"{many.ToString(CultureInfo.InvariantCulture)} men";

    private static string Left(int many) =>
        many == 1
            ? "one of the staff"
            : $"{many.ToString(CultureInfo.InvariantCulture)} of the staff";
}
