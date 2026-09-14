using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domina.Core.Combat;

/// <summary>
/// A fight's event stream written out blow by blow, as text.
/// </summary>
/// <remarks>
/// <para>
/// The resolver already produces the stream — it is the whole architecture rule (<c>CLAUDE.md</c>):
/// the fight knows nothing about animation, it emits events and something else draws them. The same
/// stream is the finest record of a fight that exists, so this writes it down: who struck whom at what
/// second, what got through, which limb came off, which piece of armour broke, who panicked and who
/// went down.
/// </para>
/// <para>
/// It is <b>not</b> written on every fight. The stream is only collected when
/// <see cref="BattleSetup.CollectEvents"/> is on (the arena turns it on because it has to draw the
/// fight; a measurement run leaves it off so that a batch does not allocate a million events). This is
/// the writer for when it is on and the fight is worth keeping — a bug report, a balance argument, a
/// test fixture.
/// </para>
/// <para>
/// One event a line, like the move journal, and for the same reason: a file cut short by a crash still
/// reads up to the break, and a fight can be skimmed in a terminal.
/// </para>
/// </remarks>
public static class BattleLog
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The stream as the text of a <c>.jsonl</c> file, one event a line.</summary>
    /// <remarks>
    /// Each line names its own event type first, so a reader does not have to guess the shape from the
    /// fields: <c>{"event":"AttackLanded","AtSeconds":3.4,...}</c>.
    /// </remarks>
    public static string ToJsonl(IEnumerable<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        StringBuilder text = new();
        foreach (BattleEvent happening in events)
        {
            text.Append(Line(happening)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>One event as its line.</summary>
    public static string Line(BattleEvent happening)
    {
        ArgumentNullException.ThrowIfNull(happening);

        // Serialised against its own runtime type: the stream is a family of records, and serialising
        // it as the base type would write nothing but the timestamp.
        string body = JsonSerializer.Serialize(happening, happening.GetType(), _options);
        string name = happening.GetType().Name;

        return body.Length > 2
            ? $"{{\"event\":\"{name}\",{body[1..]}"
            : $"{{\"event\":\"{name}\"}}";
    }

    /// <summary>Writes the stream to disk, creating the folder if it is not there.</summary>
    /// <remarks>
    /// Plain file IO, like the move journal's: the core does not reach for the engine, so the caller
    /// hands in a path rather than a Godot <c>user://</c> address.
    /// </remarks>
    public static void Save(string path, IEnumerable<BattleEvent> events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(events);

        string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(path, ToJsonl(events), Encoding.UTF8);
    }
}
