using System.Globalization;

namespace Domina.Presentation;

/// <summary>The arena's command-line arguments.</summary>
/// <param name="Seed">The seed of the fight to watch.</param>
/// <param name="SpeedMultiplier">Playback speed; 1 = real time.</param>
public readonly record struct ArenaArguments(long? Seed, double? SpeedMultiplier)
{
    /// <summary>
    /// Reads arguments in the form <c>-- --seed 52 --speed 4</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What determinism buys in practice: when batch simulation reports an interesting fight ("the
    /// warrior loses an arm on seed 52"), that fight can be watched in the arena exactly as it was. This
    /// is the main route for debugging.
    /// </para>
    /// <para>
    /// Unrecognised arguments are silently skipped: Godot can put its own arguments in the same array,
    /// and the arena does not have to know what they are.
    /// </para>
    /// </remarks>
    public static ArenaArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        long? seed = null;
        double? speed = null;

        for (int i = 0; i < args.Count - 1; i++)
        {
            switch (args[i])
            {
                case "--seed" when long.TryParse(
                    args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedSeed):
                    seed = parsedSeed;
                    break;

                case "--speed" when double.TryParse(
                    args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed)
                    && parsedSpeed > 0:
                    speed = parsedSpeed;
                    break;

                default:
                    break;
            }
        }

        return new ArenaArguments(seed, speed);
    }
}
