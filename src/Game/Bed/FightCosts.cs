using System.Diagnostics;

namespace Domina.Game.Bed;

/// <summary>
/// The stand-in fight's time, split by the engine pieces it calls (<c>--profile</c>).
/// </summary>
/// <remarks>
/// The clip's figure includes the blow its key calls, and with it that blow's blade query: under
/// <see cref="BedClip.Mixer"/> the blade's time is counted twice, once on its own and once inside the clip.
/// </remarks>
internal sealed class FightCosts
{
    public Cost Reset;
    public Cost Move;
    public Cost Blade;
    public Cost Clip;

    public static void Add(ref Cost cost, long started)
    {
        cost.Ticks += Stopwatch.GetTimestamp() - started;
        cost.Calls++;
    }

    internal struct Cost
    {
        public long Ticks;
        public long Calls;

        public readonly double Seconds => (double)Ticks / Stopwatch.Frequency;

        public readonly string Describe(string name, double wallSeconds) => Calls == 0
            ? $"{name,-6} -"
            : $"{name,-6} {Seconds,7:F3} s {100 * Seconds / wallSeconds,5:F1}%  {Calls,10} calls  "
                + $"{1e6 * Seconds / Calls,6:F2} us/call";
    }
}
