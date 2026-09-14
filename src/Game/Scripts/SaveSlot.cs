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

    /// <summary>
    /// The run's move journal — every decision the player made, in order.
    /// </summary>
    /// <remarks>
    /// It sits beside the save rather than inside it, because the two answer different questions. The
    /// save is <b>where the run stands</b>; the journal is <b>how it got there</b>, and a seed plus the
    /// journal is enough to walk the whole run again in a test without opening the engine
    /// (<see cref="Domina.Core.Dojo.Journal.MoveReplay"/>).
    /// </remarks>
    public const string JournalPath = "user://moves.jsonl";

    /// <summary>
    /// How many of the live journal's moves are already in the file.
    /// </summary>
    /// <remarks>
    /// The file is <b>appended to</b>, never rewritten. A loaded save comes back with an empty journal
    /// in memory while the file still holds every earlier day, so rewriting it from memory would throw
    /// the run's history away on the first autosave after a load.
    /// </remarks>
    private static int _writtenMoves;

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

        AppendMoves(dojo);

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

        // And so does the journal: a new run opens its own, and moves from a dead season above a new
        // Start line would make the file unreplayable.
        if (dir.FileExists(JournalPath))
        {
            dir.Remove(JournalPath);
        }

        _writtenMoves = 0;
    }

    /// <summary>
    /// Adds the moves made since the last write to the journal file.
    /// </summary>
    /// <remarks>
    /// A journal that cannot be written does <b>not</b> fail the save: the save is the player's run and
    /// the journal is our test data, and losing the second must never cost him the first.
    /// </remarks>
    private static void AppendMoves(DojoState dojo)
    {
        if (dojo.Journal.Count <= _writtenMoves)
        {
            return;
        }

        string lines = string.Concat(
            dojo.Journal.Moves.Skip(_writtenMoves).Select(m => m.ToJson() + "\n"));

        bool existing = FileAccess.FileExists(JournalPath);
        using FileAccess? file = FileAccess.Open(
            JournalPath,
            existing ? FileAccess.ModeFlags.ReadWrite : FileAccess.ModeFlags.Write);

        if (file is null)
        {
            GD.PushWarning($"The move journal could not be written: {FileAccess.GetOpenError()}");
            return;
        }

        if (existing)
        {
            file.SeekEnd();
        }

        file.StoreString(lines);
        _writtenMoves = dojo.Journal.Count;
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
