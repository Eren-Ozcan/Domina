using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Domina.Core.Dojo.Journal;

/// <summary>What kind of value an argument carries, so that the line reads the way a human writes it.</summary>
public enum MoveArgType
{
    /// <summary>Written quoted: a name, an enum, a free string.</summary>
    Text,

    /// <summary>Written bare: a count, a price, a seed.</summary>
    Number,

    /// <summary>Written bare as <c>true</c>/<c>false</c>.</summary>
    Flag,

    /// <summary>Written as <c>null</c> — "the caller passed nothing", which is not the same as "zero".</summary>
    Nothing,
}

/// <summary>One named value on a move's line.</summary>
/// <param name="Name">The key as it appears in the file.</param>
/// <param name="Value">The value in its written form; <see cref="MoveArgType.Nothing"/> ignores it.</param>
/// <param name="Type">How the value is written and read back.</param>
public sealed record MoveArg(string Name, string Value, MoveArgType Type = MoveArgType.Text)
{
    /// <summary>A text argument; <c>null</c> is written as <c>null</c> rather than as an empty string.</summary>
    public static MoveArg Of(string name, string? value) => value is null
        ? new MoveArg(name, string.Empty, MoveArgType.Nothing)
        : new MoveArg(name, value);

    public static MoveArg Of(string name, int value) =>
        new(name, value.ToString(CultureInfo.InvariantCulture), MoveArgType.Number);

    public static MoveArg Of(string name, int? value) => value is int number
        ? Of(name, number)
        : new MoveArg(name, string.Empty, MoveArgType.Nothing);

    public static MoveArg Of(string name, ulong value) =>
        new(name, value.ToString(CultureInfo.InvariantCulture), MoveArgType.Number);

    public static MoveArg Of(string name, ulong? value) => value is ulong number
        ? Of(name, number)
        : new MoveArg(name, string.Empty, MoveArgType.Nothing);

    /// <summary>A measured number. Rounded to three places: the file is read by people.</summary>
    public static MoveArg Of(string name, double value) => new(
        name,
        Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture),
        MoveArgType.Number);

    public static MoveArg Of(string name, bool value) =>
        new(name, value ? "true" : "false", MoveArgType.Flag);

    /// <summary>An enum argument, written by its name so the line stays readable after a renumbering.</summary>
    public static MoveArg Of<TEnum>(string name, TEnum value)
        where TEnum : struct, Enum => new(name, value.ToString() ?? string.Empty);

    public static MoveArg Of<TEnum>(string name, TEnum? value)
        where TEnum : struct, Enum => value is TEnum set
        ? Of(name, set)
        : new MoveArg(name, string.Empty, MoveArgType.Nothing);

    /// <summary>Is there a value at all?</summary>
    public bool HasValue => Type != MoveArgType.Nothing;
}

/// <summary>
/// One row of a move's detail — a warrior's books from a fight, an item on the day's bill.
/// </summary>
/// <remarks>
/// A row is written as its own object inside the move's <c>detail</c> array, so the move stays one
/// line and the line still carries everything: who took what, where it landed, what it cost.
/// </remarks>
/// <param name="Args">The row's named values, in the order they should be read.</param>
public sealed record MoveRow(IReadOnlyList<MoveArg> Args)
{
    public MoveRow(params MoveArg[] args)
        : this((IReadOnlyList<MoveArg>)args)
    {
    }

    /// <summary>A value on the row, or <c>null</c> if it is not there.</summary>
    public string? Text(string name) =>
        Args.FirstOrDefault(a => a.Name == name && a.HasValue)?.Value;
}

/// <summary>
/// One move in the journal: what the player did on which day, and what the dojo looked like straight
/// afterwards.
/// </summary>
/// <remarks>
/// <para>
/// The two argument lists are kept apart on purpose. <see cref="Args"/> is <b>the move itself</b> —
/// everything a replay has to hand back to the same method to make the same thing happen.
/// <see cref="After"/> is <b>what was observed</b> once the move had been made: it is never fed back
/// into anything, it is what a replay is checked against. A run that diverges is found at the first
/// line where the observed gold stops matching, rather than at the end where only the outcome differs.
/// </para>
/// <para>
/// One line of the file is one move:
/// </para>
/// <code>
/// {"day":3,"move":"HireRecruit","index":1,"weapon":"Katana","armor":"Light keikogi","after":{"ok":true,"gold":412,"food":18,"living":4}}
/// </code>
/// </remarks>
/// <param name="Day">The day the move was made on.</param>
/// <param name="Kind">Which move it was.</param>
/// <param name="Args">The move's own arguments, in the order the method takes them.</param>
/// <param name="After">What the dojo looked like immediately after the move.</param>
public sealed record Move(
    int Day,
    MoveKind Kind,
    IReadOnlyList<MoveArg> Args,
    IReadOnlyList<MoveArg> After)
{
    /// <summary>The key an unrecognised move's written name is kept under.</summary>
    public const string UnknownNameArg = "name";

    /// <summary>The key <see cref="After"/> is written under.</summary>
    public const string AfterKey = "after";

    /// <summary>The key <see cref="Detail"/> is written under.</summary>
    public const string DetailKey = "detail";

    /// <summary>
    /// The move's line-by-line detail: a row per warrior for a fight, a row per item for a bill.
    /// </summary>
    /// <remarks>
    /// Detail is <b>observation, never input</b>, like <see cref="After"/>: a replay reads none of it.
    /// It is here because the questions asked of a run afterwards are detailed ones — where the gold
    /// came from and where it went, who took the wound and which limb it cost, what the fight taught —
    /// and a journal that only kept totals cannot answer any of them.
    /// </remarks>
    public IReadOnlyList<MoveRow> Detail { get; init; } = [];

    /// <summary>The key that says whether the move actually took effect.</summary>
    public const string OkKey = "ok";

    /// <summary>The move's name as it is written to the file.</summary>
    public string Name => Kind == MoveKind.Unknown
        ? Text(UnknownNameArg) ?? nameof(MoveKind.Unknown)
        : Kind.ToString();

    /// <summary>Did the move take effect? A refused move is written down too — a refusal is data.</summary>
    public bool Ok => Flag(OkKey, After) ?? true;

    /// <summary>An argument's written value, or <c>null</c> if it is not on the line.</summary>
    public string? Text(string name, IReadOnlyList<MoveArg>? within = null) =>
        (within ?? Args).FirstOrDefault(a => a.Name == name && a.HasValue)?.Value;

    /// <summary>An argument read as a whole number.</summary>
    public int? Number(string name, IReadOnlyList<MoveArg>? within = null) =>
        int.TryParse(Text(name, within), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : null;

    /// <summary>An argument read as a seed.</summary>
    public ulong? Seed(string name, IReadOnlyList<MoveArg>? within = null) =>
        ulong.TryParse(Text(name, within), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value)
            ? value
            : null;

    /// <summary>An argument read as a flag.</summary>
    public bool? Flag(string name, IReadOnlyList<MoveArg>? within = null) =>
        bool.TryParse(Text(name, within), out bool value) ? value : null;

    /// <summary>An argument read back into its enum.</summary>
    public TEnum? Choice<TEnum>(string name, IReadOnlyList<MoveArg>? within = null)
        where TEnum : struct, Enum =>
        Enum.TryParse(Text(name, within), ignoreCase: true, out TEnum value) ? value : null;

    /// <summary>The list of whole numbers an argument holds — a party, for instance, is written as <c>1|4|7</c>.</summary>
    public IReadOnlyList<int> Numbers(string name)
    {
        string? written = Text(name);
        if (string.IsNullOrEmpty(written))
        {
            return [];
        }

        List<int> values = [];
        foreach (string part in written.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    /// <summary>Writes a list of whole numbers as one readable argument.</summary>
    public static MoveArg List(string name, IEnumerable<int> values) => MoveArg.Of(
        name,
        string.Join('|', values.Select(v => v.ToString(CultureInfo.InvariantCulture))));

    /// <summary>The move as the single line it occupies in the file.</summary>
    public string ToJson()
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("day", Day);
            writer.WriteString("move", Name);

            foreach (MoveArg arg in Args)
            {
                if (arg.Name is "day" or "move" or AfterKey or DetailKey
                    || (Kind == MoveKind.Unknown && arg.Name == UnknownNameArg))
                {
                    continue;
                }

                Write(writer, arg);
            }

            if (Detail.Count > 0)
            {
                writer.WriteStartArray(DetailKey);
                foreach (MoveRow row in Detail)
                {
                    writer.WriteStartObject();
                    foreach (MoveArg arg in row.Args)
                    {
                        Write(writer, arg);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (After.Count > 0)
            {
                writer.WriteStartObject(AfterKey);
                foreach (MoveArg arg in After)
                {
                    Write(writer, arg);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Reads one line back into a move.
    /// </summary>
    /// <remarks>
    /// It follows the save's rule (GDD §2): <b>it never throws</b>. A line that cannot be read returns
    /// <c>null</c> and says why, and the rest of the file still loads — one broken line must not cost
    /// the whole journal.
    /// </remarks>
    /// <param name="line">The line to read.</param>
    /// <param name="problem">Why it could not be read; <c>null</c> if it could.</param>
    public static Move? FromJson(string line, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                problem = "the line is not an object";
                return null;
            }

            int day = root.TryGetProperty("day", out JsonElement dayValue) && dayValue.TryGetInt32(out int number)
                ? number
                : 0;

            string written = root.TryGetProperty("move", out JsonElement moveValue)
                ? moveValue.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrEmpty(written))
            {
                problem = "the line names no move";
                return null;
            }

            bool known = Enum.TryParse(written, ignoreCase: false, out MoveKind kind)
                && kind != MoveKind.Unknown;

            List<MoveArg> args = [];
            List<MoveArg> after = [];
            List<MoveRow> detail = [];

            if (!known)
            {
                args.Add(MoveArg.Of(UnknownNameArg, written));
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.Name is "day" or "move")
                {
                    continue;
                }

                if (property.Name == DetailKey && property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement row in property.Value.EnumerateArray())
                    {
                        if (row.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        detail.Add(new MoveRow([.. row.EnumerateObject().Select(Read)]));
                    }

                    continue;
                }

                if (property.Name == AfterKey && property.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty observed in property.Value.EnumerateObject())
                    {
                        after.Add(Read(observed));
                    }

                    continue;
                }

                args.Add(Read(property));
            }

            return new Move(day, known ? kind : MoveKind.Unknown, args, after) { Detail = detail };
        }
        catch (JsonException broken)
        {
            problem = broken.Message;
            return null;
        }
    }

    private static void Write(Utf8JsonWriter writer, MoveArg arg)
    {
        switch (arg.Type)
        {
            case MoveArgType.Number:
            case MoveArgType.Flag:
                writer.WritePropertyName(arg.Name);
                writer.WriteRawValue(arg.Value, skipInputValidation: false);
                break;
            case MoveArgType.Nothing:
                writer.WriteNull(arg.Name);
                break;
            default:
                writer.WriteString(arg.Name, arg.Value);
                break;
        }
    }

    private static MoveArg Read(JsonProperty property) => property.Value.ValueKind switch
    {
        JsonValueKind.Number => new MoveArg(property.Name, property.Value.GetRawText(), MoveArgType.Number),
        JsonValueKind.True or JsonValueKind.False =>
            new MoveArg(property.Name, property.Value.GetRawText(), MoveArgType.Flag),
        JsonValueKind.Null => new MoveArg(property.Name, string.Empty, MoveArgType.Nothing),
        _ => new MoveArg(property.Name, property.Value.ToString()),
    };
}
