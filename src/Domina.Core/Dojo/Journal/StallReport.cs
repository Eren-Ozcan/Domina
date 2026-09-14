using Domina.Core.Combat;

namespace Domina.Core.Dojo.Journal;

/// <summary>
/// A fight that hit the stall guard, written out blow by blow.
/// </summary>
/// <remarks>
/// <para>
/// The guard is documented as <b>an anomaly, not a result</b> (docs/GDD.md §7): a fight that reaches
/// it is a bug to be reproduced from its seed, never a share to balance around. The fault line says it
/// happened; this says <b>what happened</b> — every strike, block, catch, poison tick and limb, from
/// the event stream the resolver already produces.
/// </para>
/// <para>
/// The fight is <b>fought again</b> to get it. The stalled fight itself was almost certainly resolved
/// with the event stream switched off (the arena is the only side that turns it on, and a measurement
/// run never does), and the core is deterministic, so re-running the same setup on the same seed gives
/// the same fight — this time with the stream collected. It costs one extra fight, on the day a fight
/// hung, which is the one day nobody minds paying for it.
/// </para>
/// <para>
/// It runs only when a folder was handed in (<see cref="DojoState.DiagnosticsFolder"/>) and only when
/// the seed was recorded; without either there is nothing honest to write.
/// </para>
/// </remarks>
public static class StallReport
{
    /// <summary>Writes the stalled fight's stream beside the run, and gives back the file's path.</summary>
    /// <param name="state">The dojo the fight belongs to.</param>
    /// <param name="setup">The fight as it was set up; it is re-run from this.</param>
    /// <param name="seed">The seed it was fought on; without it nothing can be re-run.</param>
    /// <param name="label">What the fight was — an expedition, a hunt, a bout of the last night.</param>
    /// <returns>The path written, or <c>null</c> if nothing was.</returns>
    public static string? Write(DojoState state, BattleSetup setup, ulong? seed, string label)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(setup);

        if (string.IsNullOrWhiteSpace(state.DiagnosticsFolder) || seed is not ulong stream)
        {
            return null;
        }

        try
        {
            // The same setup and the same seed give the same fight; the one thing changed is that the
            // stream is kept this time.
            Battle again = new(setup with { CollectEvents = true }, new Rng.SeededRandom(stream));
            again.Run();

            string path = Path.Combine(
                state.DiagnosticsFolder,
                $"stall-day{state.Day}-{label}-{stream}.jsonl");

            BattleLog.Save(path, again.Events);
            return path;
        }
        catch (Exception broken) when (broken is IOException or UnauthorizedAccessException)
        {
            // A diagnostic that cannot be written must not take the run down with it: the fault line
            // is already filed, and it is the part that matters.
            return null;
        }
    }
}
