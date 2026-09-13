using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Honor;

namespace Domina.Presentation;

/// <summary>
/// A closed day as the player reads it.
/// </summary>
/// <remarks>
/// <para>
/// It became a model of its own with build step 8. While the day was a button, the day screen could
/// print the two or three lines it cared about and the player had pressed for the rest; with the clock
/// running, the log is the <b>only</b> place a rival's move, a verdict or a payroll that emptied is
/// ever seen. So every field of <see cref="DayReport"/> that carries news is written here, in one
/// place, and both the day screen and the hub's own ticking read the same text.
/// </para>
/// <para>
/// Silence is the rule: a day that did nothing produces its one line about supplies and stops. A log
/// that reports every drill would bury the line that matters on the day it matters.
/// </para>
/// </remarks>
public static class DayLog
{
    /// <summary>The day as one block of text, one item a line.</summary>
    public static string Line(DayReport report) => string.Join('\n', Lines(report));

    /// <summary>The day's news, one item at a time.</summary>
    public static IReadOnlyList<string> Lines(DayReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> lines =
        [
            $"Day {report.Day} closed. {report.Upkeep.GoldSpent} gold paid for supplies.",
        ];

        if (report.Event is DayEvent happening)
        {
            lines.Add($"Setback: {happening.Description}");
        }

        if (report.Upkeep.Hungry.Count > 0)
        {
            lines.Add($"{report.Upkeep.Hungry.Count} warriors went hungry — they did not advance that day.");
        }

        if (report.Upkeep.Walked is { Count: > 0 } walked)
        {
            lines.Add("The payroll could not be met: "
                + string.Join(", ", walked.Select(SchoolModel.RoleName))
                + " walked out.");
        }

        if (report.Recovered.Count > 0)
        {
            lines.Add($"{report.Recovered.Count} warriors left the infirmary.");
        }

        if (report.Opened is { Count: > 0 } opened)
        {
            lines.Add("Work finished: " + string.Join(", ", opened.Select(id => SchoolTree.Find(id).Name)) + ".");
        }

        if (report.MissedWeek)
        {
            lines.Add($"The week passed with no work filed: the roster lost {report.HonorLost:0} honour.");
        }

        if (report.BountyBroken)
        {
            lines.Add("The promise was broken: the roster lost honour.");
        }

        if (report.RivalMove is ProvinceMove move)
        {
            lines.Add(MoveText(move));
        }

        if (report.Sacked is SackReport sack)
        {
            lines.Add($"The raid was not answered: {sack.Gold} gold and {sack.Food} food were taken.");
        }

        if (report.Tribunal is TribunalVerdict verdict)
        {
            lines.Add(verdict.Outcome == SeppukuOutcome.Seppuku
                ? $"The tribunal found against {verdict.Name}. He took his own life."
                : $"The tribunal pardoned {verdict.Name}.");
        }

        return lines;
    }

    private static string MoveText(ProvinceMove move) => move.Kind switch
    {
        ProvinceMoveKind.Taken => "Kurogane took a settlement.",
        ProvinceMoveKind.Raid => "Kurogane's men came to the gate.",
        _ => $"Kurogane pressed a settlement — its warning stands at {move.Warning}.",
    };
}
