using Godot;

namespace Domina.Game;

/// <summary>
/// The few settings the game can actually honour, kept beside the save.
/// </summary>
/// <remarks>
/// <para>
/// The design's settings sheet has four panels (design canvas → 6d). Two of them — sound, and the
/// crowd in the chat — have nothing behind them in this build, and a switch that moves without changing
/// anything is worse than an empty shelf: it teaches the player that the sheet lies. What is here is
/// what the build does: the window, the frame cap, how large the interface is set, whether the yard
/// names its own destinations, and how long a line of the day's report holds.
/// </para>
/// <para>
/// They are written into <c>user://settings.cfg</c> rather than into the term's save: they belong to
/// the person playing, not to the term being played, and a new term must not put the text size back.
/// </para>
/// </remarks>
public static class GameSettings
{
    /// <summary>Where the settings are kept.</summary>
    private const string Path = "user://settings.cfg";

    private const string Section = "domina";

    /// <summary>The interface at its designed size.</summary>
    public const float BaseScale = 1f;

    /// <summary>One step up, for a player who cannot comfortably read the base.</summary>
    public const float LargeScale = 1.12f;

    /// <summary>Whether the interface is set one step larger than it is drawn.</summary>
    public static bool LargeText { get; set; }

    /// <summary>
    /// Whether every object in the yard keeps its chalk name showing, not only under the cursor.
    /// </summary>
    public static bool NameDestinations { get; set; }

    /// <summary>
    /// No smoke, no flicker, nothing that moves on its own. The clock still runs and the day still ends.
    /// </summary>
    public static bool ReducedMotion { get; set; }

    /// <summary>How long a line of the day's report holds before the next takes its place, in seconds.</summary>
    public static double LogHold { get; set; } = 6;

    /// <summary>Whether the window is borderless and fills the screen.</summary>
    public static bool Borderless { get; set; }

    /// <summary>Reads the settings back, or leaves the defaults standing when there is no file yet.</summary>
    public static void Load()
    {
        ConfigFile file = new();

        if (file.Load(Path) != Error.Ok)
        {
            return;
        }

        LargeText = (bool)file.GetValue(Section, "large_text", LargeText);
        NameDestinations = (bool)file.GetValue(Section, "name_destinations", NameDestinations);
        ReducedMotion = (bool)file.GetValue(Section, "reduced_motion", ReducedMotion);
        LogHold = (double)file.GetValue(Section, "log_hold", LogHold);
        Borderless = (bool)file.GetValue(Section, "borderless", Borderless);
    }

    /// <summary>Writes them down. Called whenever one of them is changed, because there is no OK button.</summary>
    public static void Save()
    {
        ConfigFile file = new();
        file.SetValue(Section, "large_text", LargeText);
        file.SetValue(Section, "name_destinations", NameDestinations);
        file.SetValue(Section, "reduced_motion", ReducedMotion);
        file.SetValue(Section, "log_hold", LogHold);
        file.SetValue(Section, "borderless", Borderless);
        file.Save(Path);
    }

    /// <summary>
    /// Puts the settings into effect on the window the game is running in.
    /// </summary>
    /// <remarks>
    /// The text size is the viewport's own scale rather than a font size passed down through every
    /// label: the sheets are laid out against the design's 1920×1080 and a larger face inside the same
    /// box would push an act off the foot of the paper, while scaling the whole stage keeps every sheet
    /// holding at either size — which is what the design promises.
    /// </remarks>
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.ContentScaleFactor = LargeText ? LargeScale : BaseScale;
        DisplayServer.WindowSetMode(
            Borderless ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }
}
