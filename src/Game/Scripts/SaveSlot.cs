using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Domina.Game;

/// <summary>The terms kept on disk, one file each.</summary>
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
    /// <summary>How many terms may be kept at once (design canvas -> 6c).</summary>
    public const int Slots = 3;

    /// <summary>
    /// The slot being played, and the one every write lands in.
    /// </summary>
    /// <remarks>
    /// It is set when a term is loaded or opened and stays put for the term. Nothing asks the player
    /// which slot to save into: a term writes itself into its own slot at every dawn, and the only
    /// place a slot is chosen is when a term is opened or written over.
    /// </remarks>
    public static int Current { get; set; } = 1;

    /// <summary>The save's path for a slot — in the user's data folder.</summary>
    public static string PathOf(int slot) => $"user://dojo{Clamp(slot)}.json";

    /// <summary>The slot being played.</summary>
    public static string Path => PathOf(Current);

    private static string TempPath => $"{Path}.new";

    /// <summary>
    /// Yesterday's copy. It is <b>not</b> an undo door (Open Decision #15).
    /// </summary>
    /// <remarks>
    /// It is rolled every time the save is written and read only when the current file cannot be read
    /// at all. Nothing in the game offers it: with permadeath, a "go back to the previous day" button
    /// would empty every other rule of its cost, so the door is not built rather than built and hidden.
    /// </remarks>
    private static string BackupPath => $"{Path}.bak";

    /// <summary>
    /// The run's move journal — every decision the player made, in order.
    /// </summary>
    /// <remarks>
    /// It sits beside the save rather than inside it, because the two answer different questions. The
    /// save is <b>where the run stands</b>; the journal is <b>how it got there</b>, and a seed plus the
    /// journal is enough to walk the whole run again in a test without opening the engine
    /// (<see cref="Domina.Core.Dojo.Journal.MoveReplay"/>).
    /// </remarks>
    public static string JournalPath => $"user://moves{Current}.jsonl";

    /// <summary>
    /// How many of the live journal's moves are already in the file.
    /// </summary>
    /// <remarks>
    /// The file is <b>appended to</b>, never rewritten. A loaded save comes back with an empty journal
    /// in memory while the file still holds every earlier day, so rewriting it from memory would throw
    /// the run's history away on the first autosave after a load.
    /// </remarks>
    private static int _writtenMoves;

    /// <summary>Is there a term kept in the slot being played?</summary>
    public static bool Exists() => FileAccess.FileExists(Path);

    /// <summary>Is there a term kept in this slot?</summary>
    public static bool Exists(int slot) => FileAccess.FileExists(PathOf(slot));

    /// <summary>The first slot with nothing in it, or <c>null</c> when all three are kept.</summary>
    /// <remarks>
    /// A new term writes itself into an empty slot without asking. It is only when every slot holds a
    /// term that the player is made to choose one to write over, and that choice is a cut act.
    /// </remarks>
    public static int? FirstEmpty()
    {
        for (int slot = 1; slot <= Slots; slot++)
        {
            if (!Exists(slot))
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>Reads a slot without making it the one being played, for a list of kept terms.</summary>
    public static LoadResult Peek(int slot)
    {
        if (!Exists(slot))
        {
            return LoadResult.Failed("The slot is empty.");
        }

        using FileAccess? file = FileAccess.Open(PathOf(slot), FileAccess.ModeFlags.Read);

        return file is null
            ? LoadResult.Failed($"The term could not be opened: {FileAccess.GetOpenError()}")
            : DojoSaveFile.Load(file.GetAsText());
    }

    /// <summary>Takes a term out of its slot for good.</summary>
    public static void Delete(int slot)
    {
        int was = Current;
        Current = Clamp(slot);
        Delete();
        Current = was;
    }

    private static int Clamp(int slot) => Math.Clamp(slot, 1, Slots);

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

    /// <summary>Deletes the term in the slot being played, with its backup and its journal.</summary>
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
