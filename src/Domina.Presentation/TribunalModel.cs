using System.Globalization;
using Domina.Core.Dojo;
using Domina.Core.Honor;

namespace Domina.Presentation;

/// <summary>One of the two ends a summons can have, as the hut prints it.</summary>
/// <param name="Name">What it is called — never "option A".</param>
/// <param name="What">What happens to the man.</param>
/// <param name="Cost">What it does to the dojo, in the dojo's own terms.</param>
/// <param name="Irreversible">Whether it is the end that cannot be undone.</param>
public readonly record struct TribunalEnd(string Name, string What, string Cost, bool Irreversible);

/// <summary>A man lying in the hut, and how long he is there for.</summary>
/// <param name="Name">His name.</param>
/// <param name="Line">What is wrong with him and when he is up.</param>
public readonly record struct AbedLine(string Name, string Line);

/// <summary>What the hut is holding tonight.</summary>
/// <param name="Called">The man standing, if one is; <c>null</c> when nobody is called.</param>
/// <param name="Record">His line: his grade, his honour, how long he has been here.</param>
/// <param name="Tally">What the crowd has said so far, in words.</param>
/// <param name="Ends">The two ends, with what each costs.</param>
/// <param name="Abed">Who is lying in the hut.</param>
public readonly record struct HutSheet(
    string? Called,
    string Record,
    string Tally,
    IReadOnlyList<TribunalEnd> Ends,
    IReadOnlyList<AbedLine> Abed);

/// <summary>
/// The hut at dusk: who is called, what the yard is waiting to hear, and who is lying inside it.
/// </summary>
/// <remarks>
/// <para>
/// The design draws the tribunal as three ends the player presses (design canvas → 6f). The model does
/// not work that way and the difference is deliberate: GDD §6 gives the verdict to the crowd, with an
/// artificial crowd standing in when nobody is watching. So the ends are printed with what each would
/// cost — which is the part of the design that carries the weight — and the player's own act is to
/// send the yard to bed.
/// </para>
/// <para>
/// The two ends are named from the tuning rather than written out, so that a change to the pardoned
/// honour or the immunity does not leave the hut describing a rule the core no longer follows.
/// </para>
/// </remarks>
public static class TribunalModel
{
    /// <summary>Reads the hut off the dojo as it now stands.</summary>
    public static HutSheet Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        Summons? standing = dojo.Tribunal.Standing;
        HonorTuning tuning = dojo.Tribunal.Tuning;

        List<AbedLine> abed = [];

        foreach (RosterEntry entry in dojo.Roster.Living)
        {
            if (entry.RecoveryDaysRemaining > 0)
            {
                abed.Add(new AbedLine(
                    entry.Name,
                    $"up in {entry.RecoveryDaysRemaining.ToString(CultureInfo.InvariantCulture)} days"
                    + (entry.Warrior.Disabilities.Count > 0 ? " · he will not come back whole" : string.Empty)));
            }
        }

        if (standing is not Summons called)
        {
            return new HutSheet(null, string.Empty, string.Empty, [], abed);
        }

        return new HutSheet(
            called.Name,
            Record(called, dojo.Day),
            Tally(called.Tally),
            Ends(tuning),
            abed);
    }

    /// <summary>The line under his name: what he is worth and how far he has fallen.</summary>
    private static string Record(Summons called, int day)
    {
        int days = Math.Max(0, day - called.OpenedDay);

        return $"honour {called.Honor.ToString("0", CultureInfo.InvariantCulture)} of 100 · "
            + $"will {called.Willpower.ToString("0", CultureInfo.InvariantCulture)} · "
            + (days == 0
                ? "called at dusk tonight"
                : $"standing since day {called.OpenedDay.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>What the crowd has said, said back in words rather than as a ratio.</summary>
    private static string Tally(CrowdVerdict verdict)
    {
        if (!verdict.HasVotes)
        {
            return "The crowd has not spoken. If it stays silent the yard decides for itself.";
        }

        return verdict.FavorsMercy
            ? $"The crowd is calling him bushi — {verdict.Bushi.ToString(CultureInfo.InvariantCulture)} "
                + $"to {verdict.Ronin.ToString(CultureInfo.InvariantCulture)}."
            : $"The crowd is calling him ronin — {verdict.Ronin.ToString(CultureInfo.InvariantCulture)} "
                + $"to {verdict.Bushi.ToString(CultureInfo.InvariantCulture)}.";
    }

    /// <summary>The two ends, each with what it costs.</summary>
    private static IReadOnlyList<TribunalEnd> Ends(HonorTuning tuning) =>
    [
        new TribunalEnd(
            "He is called bushi",
            "He keeps his name and stays in the yard. The store feeds him tomorrow as it did today.",
            $"His honour is set back to {tuning.PardonedHonor.ToString("0", CultureInfo.InvariantCulture)}, "
                + $"and he cannot be called again for "
                + $"{tuning.PardonImmunityDays.ToString(CultureInfo.InvariantCulture)} days.",
            Irreversible: false),

        new TribunalEnd(
            "He is granted the blade",
            "He dies on the mat tonight, with the yard watching and his name intact.",
            "There is no undoing it and no second asking. It is the only vermilion the game shows.",
            Irreversible: true),
    ];
}
