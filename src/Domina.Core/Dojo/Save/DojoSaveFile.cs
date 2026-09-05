using System.Text.Json;
using System.Text.Json.Serialization;
using Domina.Core.Model;

namespace Domina.Core.Dojo.Save;

/// <summary>Dojo'nun kaydını yazar ve okur.</summary>
/// <remarks>
/// <para>
/// Üç kural GDD §2'den gelir: <b>versiyonlu</b> (dosya kendi biçimini söyler),
/// <b>merge-on-load</b> (eksik alan varsayılanla doldurulur, tanınmayan alan yok sayılır)
/// ve <b>try/catch</b> (bozuk dosya oyunu çökertmez, taşıyabildiğini taşır).
/// </para>
/// <para>
/// Yükleme bu yüzden hiçbir zaman <b>istisna fırlatmaz</b>: <see cref="LoadResult"/>
/// döner ve neyi kurtaramadığını uyarı listesinde yazar. Tek bir bozuk savaşçı kaydı
/// kadronun geri kalanını götürmez — kalan herkes yüklenir, o savaşçı atlanır.
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

    /// <summary>Yaşayan durumdan kayıt nesnesi çıkarır.</summary>
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
                w.Talent));
        }

        return new DojoSnapshot(
            DojoSnapshot.CurrentVersion, state.Day, state.Resources, warriors, state.Seed);
    }

    public static string Write(DojoState state) =>
        JsonSerializer.Serialize(Capture(state), _options);

    /// <summary>
    /// Kayıt metnini okur. <b>Fırlatmaz</b>: okuyamadığında başarısız bir sonuç döner.
    /// </summary>
    public static LoadResult Load(string? json, DojoTuning? tuning = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return LoadResult.Failed("Kayıt boş.");
        }

        DojoSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<DojoSnapshot>(json, _options);
        }
        catch (JsonException e)
        {
            return LoadResult.Failed($"Kayıt okunamadı: {e.Message}");
        }

        return snapshot is null
            ? LoadResult.Failed("Kayıt boş bir nesneye çözüldü.")
            : Restore(snapshot, tuning);
    }

    /// <summary>Kayıt nesnesini yaşayan duruma çevirir, kurtaramadığını uyarı olarak yazar.</summary>
    public static LoadResult Restore(DojoSnapshot snapshot, DojoTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        List<string> warnings = [];
        if (snapshot.Version > DojoSnapshot.CurrentVersion)
        {
            warnings.Add(
                $"Kayıt daha yeni bir sürümden ({snapshot.Version} > {DojoSnapshot.CurrentVersion}); "
                + "tanınmayan alanlar yok sayıldı.");
        }

        DojoState state = new(tuning)
        {
            Resources = snapshot.Resources,
        };

        if (snapshot.Day < 1)
        {
            warnings.Add($"Gün sayacı geçersizdi ({snapshot.Day}); 1. güne çekildi.");
        }

        state.RestoreDay(Math.Max(1, snapshot.Day));
        state.RestoreSeed(snapshot.Seed);

        foreach (WarriorSnapshot record in snapshot.Warriors ?? [])
        {
            try
            {
                RestoreWarrior(state, record, warnings);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                warnings.Add($"Savaşçı kaydı atlandı (Id {record.Id}): {e.Message}");
            }
        }

        return new LoadResult(state, warnings);
    }

    private static void RestoreWarrior(DojoState state, WarriorSnapshot record, List<string> warnings)
    {
        string name = record.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"İsimsiz {record.Id}";
            warnings.Add($"Id {record.Id} adsızdı; '{name}' verildi.");
        }

        if (record.IsAlive && state.Roster.IsNameTaken(name))
        {
            string unique = $"{name} ({record.Id})";
            warnings.Add($"'{name}' adı iki canlıda görünüyordu; ikincisi '{unique}' oldu.");
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
        };

        foreach (BodyPart part in record.Disabilities ?? [])
        {
            warrior.AddDisability(part);
        }

        RosterEntry entry = state.Roster.Add(warrior);
        entry.Injure(Math.Max(0, record.RecoveryDaysRemaining));
        entry.TrainingDays = Math.Max(0, record.TrainingDays);

        if (!record.IsAlive)
        {
            state.Roster.Kill(warrior.Id);
        }
    }
}

/// <summary>Bir yükleme denemesinin sonucu.</summary>
/// <remarks>
/// Uyarılar <b>sessiz kalmasın</b> diye taşınır: merge-on-load'ın bedeli, dosyanın
/// sessizce eksik yüklenmesidir. Arayüz bunları oyuncuya gösterebilmeli.
/// </remarks>
public sealed record LoadResult(DojoState? State, IReadOnlyList<string> Warnings)
{
    public bool Succeeded => State is not null;

    public static LoadResult Failed(string reason) => new(null, [reason]);
}
