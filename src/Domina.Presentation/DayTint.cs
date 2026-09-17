namespace Domina.Presentation;

/// <summary>
/// The wash laid over the world at a given hour of the day: a colour and how much of it.
/// </summary>
/// <param name="Red">The wash's red channel, 0-1.</param>
/// <param name="Green">The wash's green channel, 0-1.</param>
/// <param name="Blue">The wash's blue channel, 0-1.</param>
/// <param name="Strength">How much of the wash is laid down, 0-1.</param>
public readonly record struct Wash(float Red, float Green, float Blue, float Strength);

/// <summary>
/// The day-cycle tint: which wash the hour puts over the yard.
/// </summary>
/// <remarks>
/// <para>
/// The day is a real-time clock running under everything (<see cref="DayClock.Progress"/>), and until
/// now nothing on screen said so except a bar in the strip. The tint is the world's own way of saying
/// it: the yard warms at dawn, loses its colour at noon, goes to rust at dusk and to indigo at night.
/// </para>
/// <para>
/// It lives here rather than in the engine for the usual reason — a colour picked in a
/// <c>_Process</c> loop cannot be tested, and the one thing this must never do is go dark enough to
/// cost legibility. <see cref="MaxStrength"/> is the ceiling that guarantees it, and the tests hold it.
/// </para>
/// <para>
/// It is a <b>wash over paper</b>, not a light: the interface's own ink is never tinted by it, so
/// nothing the player has to read depends on the hour (design canvas → 6d, "blood is not an interface
/// colour" — and neither is the weather).
/// </para>
/// </remarks>
public static class DayTint
{
    /// <summary>The most of itself the wash is ever allowed to lay down.</summary>
    /// <remarks>
    /// The ceiling is the promise: at its heaviest the wash is a seventh of the picture, which reads as
    /// an hour and never as a filter over the page. It was set by looking: at a quarter the yard's lit
    /// night turned sepia at dawn, which is a different painting rather than the same one at a
    /// different hour.
    /// </remarks>
    public const float MaxStrength = 0.15f;

    /// <summary>The wash for an hour of the day, given as the day's progress from 0 to 1.</summary>
    public static Wash At(double progress)
    {
        double hour = progress <= 0 ? 0 : progress >= 1 ? 1 : progress;

        // Four hours the day is read by, each with the wash it is recognised by. Between two of them
        // the wash is mixed, so the change is continuous and the player never sees it step.
        (double At, Wash Wash)[] hours =
        [
            (0.00, new Wash(0.98f, 0.82f, 0.58f, 0.11f)),   // first light, low and yellow
            (0.30, new Wash(1.00f, 0.98f, 0.94f, 0.03f)),   // the flat hours, almost nothing
            (0.72, new Wash(0.94f, 0.58f, 0.34f, 0.10f)),   // the sun going down, rust
            (1.00, new Wash(0.42f, 0.48f, 0.78f, MaxStrength)), // night, indigo
        ];

        for (int i = 0; i < hours.Length - 1; i++)
        {
            (double from, Wash a) = hours[i];
            (double to, Wash b) = hours[i + 1];

            if (hour > to)
            {
                continue;
            }

            double span = to - from;
            float t = span <= 0 ? 0 : (float)((hour - from) / span);
            return Mix(a, b, t);
        }

        return hours[^1].Wash;
    }

    /// <summary>One wash bled into the next.</summary>
    private static Wash Mix(Wash a, Wash b, float t) => new(
        a.Red + ((b.Red - a.Red) * t),
        a.Green + ((b.Green - a.Green) * t),
        a.Blue + ((b.Blue - a.Blue) * t),
        a.Strength + ((b.Strength - a.Strength) * t));
}
