using System.Globalization;

namespace Domina.Game.Bed;

/// <summary>Which fight the bed runs.</summary>
internal enum BedFighter
{
    /// <summary>The harness's own stand-in fight, built from Godot nodes (<see cref="TrivialFight"/>).</summary>
    Trivial,

    /// <summary>The engine-free resolver behind the door, run inside Godot's runtime as a calibration.</summary>
    Core,
}

/// <summary>Who steps the fight.</summary>
internal enum BedLane
{
    /// <summary>The bed's own loop: a fight is stepped tick after tick inside one engine frame.</summary>
    Manual,

    /// <summary>The engine's fixed physics step: one tick per physics frame, many fights side by side.</summary>
    Frame,
}

/// <summary>What a fighter's body is on the physics server.</summary>
internal enum BedBody
{
    /// <summary>A <c>StaticBody2D</c>: a moved transform reaches the server's broadphase at once.</summary>
    Static,

    /// <summary>A <c>CharacterBody2D</c>: kinematic, so the server applies a moved transform on its next step.</summary>
    Character,
}

/// <summary>How the frame the blow lands is found.</summary>
internal enum BedClip
{
    /// <summary>The fighter's <c>AnimationPlayer</c> is advanced every tick and its method key calls the blow.</summary>
    Mixer,

    /// <summary>The method key's time is read off the same clip once, and the tick count is compared to it.</summary>
    Keys,
}

/// <summary>How the blade finds the man it is swung at.</summary>
internal enum BedBlade
{
    /// <summary>A shape query against the fight's physics space (<c>IntersectShape</c>).</summary>
    Query,

    /// <summary>The blade's shape tested against the target's body shape alone (<c>Shape2D.Collide</c>).</summary>
    Shape,
}

/// <summary>The engine bed's command line (the arguments after Godot's <c>--</c>).</summary>
internal sealed record BedOptions(
    BedFighter Fighter,
    BedLane Lane,
    BedBody Body,
    BedClip Clip,
    BedBlade Blade,
    int Fights,
    ulong FirstSeed,
    int Slots,
    bool Rebuild,
    bool Profile,
    string? DigestPath)
{
    public const string Usage =
        "engine bed: -- [--fighter trivial|core] [--lane manual|frame] [--body static|character] "
        + "[--clip mixer|keys] [--blade query|shape] [--fights N] [--seed S] [--slots K] [--rebuild] [--profile] [--digest <file>]\n"
        + "  the frame lane needs Godot's own --fixed-fps 20 before the --, or it runs in real time";

    public static BedOptions Default { get; } = new(
        BedFighter.Trivial, BedLane.Manual, BedBody.Static, BedClip.Keys, BedBlade.Shape, 10_000, 1, 256, false, false, null);

    /// <summary>Reads the arguments; an unknown or malformed one is an error, never a silent default.</summary>
    public static BedOptions Parse(IReadOnlyList<string> arguments, out string? error)
    {
        BedOptions options = Default;
        error = null;

        for (int i = 0; i < arguments.Count; i++)
        {
            string name = arguments[i];
            string? value = i + 1 < arguments.Count ? arguments[i + 1] : null;
            bool takesValue = true;

            switch (name)
            {
                case "--fighter" when Enum.TryParse(value, true, out BedFighter fighter):
                    options = options with { Fighter = fighter };
                    break;
                case "--lane" when Enum.TryParse(value, true, out BedLane lane):
                    options = options with { Lane = lane };
                    break;
                case "--body" when Enum.TryParse(value, true, out BedBody body):
                    options = options with { Body = body };
                    break;
                case "--clip" when Enum.TryParse(value, true, out BedClip clip):
                    options = options with { Clip = clip };
                    break;
                case "--blade" when Enum.TryParse(value, true, out BedBlade blade):
                    options = options with { Blade = blade };
                    break;
                case "--fights" when int.TryParse(value, CultureInfo.InvariantCulture, out int fights) && fights > 0:
                    options = options with { Fights = fights };
                    break;
                case "--seed" when ulong.TryParse(value, CultureInfo.InvariantCulture, out ulong seed):
                    options = options with { FirstSeed = seed };
                    break;
                case "--slots" when int.TryParse(value, CultureInfo.InvariantCulture, out int slots) && slots > 0:
                    options = options with { Slots = slots };
                    break;
                case "--digest" when !string.IsNullOrEmpty(value):
                    options = options with { DigestPath = value };
                    break;
                case "--rebuild":
                    options = options with { Rebuild = true };
                    takesValue = false;
                    break;
                case "--profile":
                    options = options with { Profile = true };
                    takesValue = false;
                    break;
                default:
                    error = $"cannot read '{name}{(value is null ? string.Empty : " " + value)}'";
                    return options;
            }

            if (takesValue)
            {
                i++;
            }
        }

        // The core resolver runs its own loop; there is no engine step for it to be driven by.
        if (options.Fighter == BedFighter.Core && options.Lane == BedLane.Frame)
        {
            error = "the core fighter has no frame lane: it is stepped by Battle.Run";
        }

        return options;
    }

    public string Describe() => Fighter == BedFighter.Core
        ? "fighter=core lane=manual"
        : ($"fighter=trivial lane={Lane} body={Body} clip={Clip} blade={Blade} slots={(Lane == BedLane.Frame ? Slots : 1)} "
            + $"rebuild={(Rebuild ? "yes" : "no")}").ToLowerInvariant();
}
