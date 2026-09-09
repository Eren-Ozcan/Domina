using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Domina.Game;

/// <summary>The save's single slot on disk.</summary>
/// <remarks>
/// <para>
/// The core produces and reads the save's <b>text</b> (<see cref="DojoSaveFile"/>); this class
/// only puts that text into a file. The separation is deliberate: the file path and the engine's file
/// access depend on Godot, the save's format does not.
/// </para>
/// <para>
/// Writing is <b>two-step</b>: first a temporary file, then a swap. Written over directly, a game
/// closing mid-write would leave the save half finished and erase the expedition — and because
/// autosave runs every day, that window would open often.
/// </para>
/// </remarks>
public static class SaveSlot
{
    /// <summary>The save's path — in the user's data folder.</summary>
    public const string Path = "user://dojo.json";

    private const string TempPath = "user://dojo.json.new";

    /// <summary>Is there a save to load?</summary>
    public static bool Exists() => FileAccess.FileExists(Path);

    /// <summary>Writes the dojo to disk; returns <c>false</c> if it cannot.</summary>
    public static bool Write(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        string json = DojoSaveFile.Write(dojo);

        using (FileAccess? file = FileAccess.Open(TempPath, FileAccess.ModeFlags.Write))
        {
            if (file is null)
            {
                GD.PushWarning($"The save could not be written: {FileAccess.GetOpenError()}");
                return false;
            }

            file.StoreString(json);
        }

        return Swap();
    }

    /// <summary>Reads the save. If the file is missing or unreadable, it returns a failed result.</summary>
    public static LoadResult Load()
    {
        if (!Exists())
        {
            return LoadResult.Failed("No save found.");
        }

        using FileAccess? file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        return file is null
            ? LoadResult.Failed($"The save could not be opened: {FileAccess.GetOpenError()}")
            : DojoSaveFile.Load(file.GetAsText());
    }

    /// <summary>Deletes the save — so a new game does not sit on top of the old one.</summary>
    public static void Delete()
    {
        using DirAccess? dir = DirAccess.Open("user://");
        if (dir is not null && dir.FileExists(Path))
        {
            dir.Remove(Path);
        }
    }

    /// <summary>Puts the temporary file in the save's place.</summary>
    private static bool Swap()
    {
        using DirAccess? dir = DirAccess.Open("user://");
        if (dir is null)
        {
            GD.PushWarning("The save folder could not be opened.");
            return false;
        }

        if (dir.FileExists(Path))
        {
            dir.Remove(Path);
        }

        Error error = dir.Rename(TempPath, Path);
        if (error != Error.Ok)
        {
            GD.PushWarning($"The save could not be swapped into place: {error}");
            return false;
        }

        return true;
    }
}
