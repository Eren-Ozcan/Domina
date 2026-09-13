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

    /// <summary>
    /// Yesterday's copy. It is <b>not</b> an undo door (Open Decision #15).
    /// </summary>
    /// <remarks>
    /// It is rolled every time the save is written and read only when the current file cannot be read
    /// at all. Nothing in the game offers it: with permadeath, a "go back to the previous day" button
    /// would empty every other rule of its cost, so the door is not built rather than built and hidden.
    /// </remarks>
    private const string BackupPath = "user://dojo.json.bak";

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

        Roll();
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
        if (file is null)
        {
            return LoadResult.Failed($"The save could not be opened: {FileAccess.GetOpenError()}");
        }

        return DojoSaveFile.LoadWithBackup(file.GetAsText(), ReadBackup());
    }

    /// <summary>Deletes the save — so a new game does not sit on top of the old one.</summary>
    public static void Delete()
    {
        using DirAccess? dir = DirAccess.Open("user://");
        if (dir is null)
        {
            return;
        }

        if (dir.FileExists(Path))
        {
            dir.Remove(Path);
        }

        // The backup goes with it: a new season must not be able to fall back into a dead one.
        if (dir.FileExists(BackupPath))
        {
            dir.Remove(BackupPath);
        }
    }

    /// <summary>Yesterday's text, or <c>null</c> if there is none.</summary>
    private static string? ReadBackup()
    {
        if (!FileAccess.FileExists(BackupPath))
        {
            return null;
        }

        using FileAccess? file = FileAccess.Open(BackupPath, FileAccess.ModeFlags.Read);
        return file?.GetAsText();
    }

    /// <summary>Moves the standing save into the backup's place before it is overwritten.</summary>
    private static void Roll()
    {
        if (!FileAccess.FileExists(Path))
        {
            return;
        }

        using DirAccess? dir = DirAccess.Open("user://");
        if (dir is null)
        {
            return;
        }

        if (dir.FileExists(BackupPath))
        {
            dir.Remove(BackupPath);
        }

        // A backup that could not be rolled is not worth failing the save over: the point of it is the
        // corrupted file that has not happened yet, and the save itself is the thing being protected.
        _ = dir.Copy(Path, BackupPath);
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
