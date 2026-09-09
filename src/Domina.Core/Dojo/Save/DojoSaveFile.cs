using System.Text.Json;
using System.Text.Json.Serialization;
using Domina.Core.Model;

namespace Domina.Core.Dojo.Save;

/// <summary>Writes and reads the dojo's save.</summary>
/// <remarks>
/// <para>
/// Three rules come from GDD §2: <b>versioned</b> (the file states its own format),
/// <b>merge-on-load</b> (a missing field is filled with its default, an unrecognised field is ignored)
/// and <b>try/catch</b> (a corrupted file does not crash the game, it carries what it can).
/// </para>
/// <para>
/// Loading therefore <b>never throws</b>: it returns a <see cref="LoadResult"/> and writes what it
/// could not rescue into the warning list. A single corrupted warrior record does not take the rest of
/// the roster with it — everyone else loads and that warrior is skipped.
/// </para>
/// </remarks>
public static class DojoSaveFile
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Produces a save object from the live state.</summary>
    public static DojoSnapshot Capture(DojoState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        List<WarriorSnapshot> warriors = [];
        foreach (RosterEntry entry in state.Roster.Entries)
        {
            Warrior w = entry.Warrior;
            warriors.Add(new WarriorSnapshot(
                w.Id.Value,
                w.Name,
                w.BaseStats,
                w.Honor,
                w.IsAlive,
                [.. w.Disabilities.Select(d => d.Part)],
                w.ArmorWear,
                WeaponSnapshot.From(w.Weapon),
                ArmorSnapshot.From(w.Armor),
                w.Thrown is null ? null : ThrownWeaponSnapshot.From(w.Thrown),
                entry.RecoveryDaysRemaining,
                entry.TrainingDays,
                w.Talent,
                entry.Drill,
                w.Path));
        }

        return new DojoSnapshot(
            DojoSnapshot.CurrentVersion,
            state.Day,
            state.Resources,
            warriors,
            state.Seed,
            state.AcceptedBountyDay,
            state.ClaimedBountyDay,
            [.. state.School.Owned],
            [.. state.HiredToday]);
    }

    public static string Write(DojoState state) =>
        JsonSerializer.Serialize(Capture(state), _options);

    /// <summary>
    /// Reads the save text. It <b>does not throw</b>: when it cannot read, it returns a failed result.
    /// </summary>
    public static LoadResult Load(string? json, DojoTuning? tuning = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return LoadResult.Failed("The save is empty.");
        }

        DojoSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<DojoSnapshot>(json, _options);
        }
        catch (JsonException e)
        {
            return LoadResult.Failed($"The save could not be read: {e.Message}");
        }

        return snapshot is null
            ? LoadResult.Failed("The save resolved to an empty object.")
            : Restore(snapshot, tuning);
    }

    /// <summary>Turns the save object into live state, writing what it could not rescue as warnings.</summary>
    public static LoadResult Restore(DojoSnapshot snapshot, DojoTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        List<string> warnings = [];
        if (snapshot.Version > DojoSnapshot.CurrentVersion)
        {
            warnings.Add(
                $"The save is from a newer version ({snapshot.Version} > {DojoSnapshot.CurrentVersion}); "
                + "unrecognised fields were ignored.");
        }

        DojoState state = new(tuning)
        {
            Resources = snapshot.Resources,
        };

        if (snapshot.Day < 1)
        {
            warnings.Add($"The day counter was invalid ({snapshot.Day}); it was pulled back to day 1.");
        }

        state.RestoreDay(Math.Max(1, snapshot.Day));
        state.RestoreSeed(snapshot.Seed);
        state.RestoreBounty(snapshot.AcceptedBountyDay, snapshot.ClaimedBountyDay);
        state.RestoreSchool(snapshot.School ?? []);
        state.RestoreHiredToday(snapshot.HiredRecruits ?? []);

        foreach (WarriorSnapshot record in snapshot.Warriors ?? [])
        {
            try
            {
                RestoreWarrior(state, record, warnings);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                warnings.Add($"A warrior record was skipped (Id {record.Id}): {e.Message}");
            }
        }

        return new LoadResult(state, warnings);
    }

    private static void RestoreWarrior(DojoState state, WarriorSnapshot record, List<string> warnings)
    {
        string name = record.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Nameless {record.Id}";
            warnings.Add($"Id {record.Id} had no name; it was given '{name}'.");
        }

        if (record.IsAlive && state.Roster.IsNameTaken(name))
        {
            string unique = $"{name} ({record.Id})";
            warnings.Add($"The name '{name}' appeared on two living warriors; the second became '{unique}'.");
            name = unique;
        }

        Warrior warrior = new(
            new WarriorId(record.Id),
            name,
            record.Stats,
            record.Weapon?.ToWeapon(),
            record.Armor?.ToArmor(),
            record.Thrown?.ToThrownWeapon())
        {
            Honor = HonorScale.Clamp(record.Honor),
            ArmorWear = record.ArmorWear,
            Talent = record.Talent <= 0 ? 1 : record.Talent,
            Path = record.Path,
        };

        foreach (BodyPart part in record.Disabilities ?? [])
        {
            warrior.AddDisability(part);
        }

        RosterEntry entry = state.Roster.Add(warrior);
        entry.Injure(Math.Max(0, record.RecoveryDaysRemaining));
        entry.TrainingDays = Math.Max(0, record.TrainingDays);
        entry.Drill = record.Drill;

        if (!record.IsAlive)
        {
            state.Roster.Kill(warrior.Id);
        }
    }
}

/// <summary>The result of a load attempt.</summary>
/// <remarks>
/// The warnings are carried so that they <b>do not stay silent</b>: the price of merge-on-load is the
/// file loading incompletely without a word. The interface must be able to show them to the player.
/// </remarks>
public sealed record LoadResult(DojoState? State, IReadOnlyList<string> Warnings)
{
    public bool Succeeded => State is not null;

    public static LoadResult Failed(string reason) => new(null, [reason]);
}
