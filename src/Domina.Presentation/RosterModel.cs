using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>Roster ekranındaki tek satır — bir savaşçının o günkü hâli.</summary>
/// <param name="Id">Savaşçının kimliği; ekran komutları bunu geri verir.</param>
/// <param name="Name">Görünen ad.</param>
/// <param name="IsAlive">Ölüler kadroda kalır (permadeath kalıcı, kayıt kalıcı).</param>
/// <param name="Status">Satırın durum rozeti.</param>
/// <param name="RecoveryDaysRemaining">Revirde kalan gün; sıfırsa sefere hazır.</param>
/// <param name="Activity">Bugünkü uğraş.</param>
/// <param name="Drill">Seçili talim — revirdeyken de saklanır.</param>
/// <param name="Path">Seçilmiş yol; <see cref="WarriorPath.None"/> ise henüz yok.</param>
/// <param name="PathUnlocked">Yol seçimi açılmış mı?</param>
/// <param name="TrainingDaysToPath">Yol kilidine kalan antrenman günü; açıksa 0.</param>
/// <param name="TrainingDays">Tamamlanmış antrenman günü.</param>
/// <param name="Honor">Onur (0-100).</param>
/// <param name="BaseStats">Ham statlar — antrenmanın yazdığı sayı.</param>
/// <param name="EffectiveStats">Yol ve sakatlıktan sonra dövüşün okuduğu sayı.</param>
/// <param name="Lost">Kaybedilmiş uzuvlar.</param>
/// <param name="WeaponName">Kullanabildiği silah — uzvunu kaybettiyse yumruk okunur.</param>
/// <param name="ArmorName">Kuşamın adı.</param>
/// <param name="ArmorWear">Kuşamdaki toplam yıpranma.</param>
/// <param name="IsFitForCampaign">Bugün sefere gönderilebilir mi?</param>
public readonly record struct RosterRow(
    WarriorId Id,
    string Name,
    bool IsAlive,
    RosterStatus Status,
    int RecoveryDaysRemaining,
    DojoActivity Activity,
    Drill Drill,
    WarriorPath Path,
    bool PathUnlocked,
    int TrainingDaysToPath,
    int TrainingDays,
    double Honor,
    WarriorStats BaseStats,
    WarriorStats EffectiveStats,
    BodyPartSet Lost,
    string WeaponName,
    string ArmorName,
    double ArmorWear,
    bool IsFitForCampaign);

/// <summary>Satırın rozeti — sıralamayı da bu belirler.</summary>
public enum RosterStatus
{
    /// <summary>Sefere hazır.</summary>
    Ready,

    /// <summary>Antrenmanda; sefere yine de gidebilir.</summary>
    Training,

    /// <summary>Revirde; sefere çıkamaz.</summary>
    Recovering,

    /// <summary>Ölü. Kayıt kadroda durur.</summary>
    Fallen,
}

/// <summary>Kadronun tepesinde duran sayılar.</summary>
/// <param name="Living">Canlı savaşçı sayısı.</param>
/// <param name="Fit">Bugün sefere gidebilecekler.</param>
/// <param name="Recovering">Revirdekiler.</param>
/// <param name="Fallen">Ölüler.</param>
/// <param name="PartyCapacity">Bir sefere çıkabilecek azami savaşçı (GDD §1).</param>
public readonly record struct RosterSummary(
    int Living,
    int Fit,
    int Recovering,
    int Fallen,
    int PartyCapacity);

/// <summary>Ad değiştirme denemesinin sonucu.</summary>
public enum RenameVerdict
{
    /// <summary>Kabul edilir.</summary>
    Ok,

    /// <summary>Boş ad.</summary>
    Empty,

    /// <summary>Ad başka bir <b>canlıda</b>; ölülerin adı havuza dönmüştür.</summary>
    Taken,

    /// <summary>Ad zaten bu savaşçının; değişiklik yok.</summary>
    Unchanged,
}

/// <summary>
/// Roster ekranının okuduğu model. Sayıyı ve rozeti hesaplar, çizim yapmaz.
/// </summary>
/// <remarks>
/// Ekran <see cref="Roster"/>'ı doğrudan okusaydı iki iş sızardı: hangi savaşçının
/// önce geleceği ve ad değiştirmenin <b>reddedileceğini önceden bilmek</b>. İkincisi
/// önemli: <see cref="Roster.Rename"/> çakışan adda fırlatır, ekran ise tuşu daha
/// basılmadan kapatabilmeli.
/// </remarks>
public static class RosterModel
{
    /// <summary>Bir sefere çıkabilecek azami savaşçı (GDD §1 — üst sınır 4).</summary>
    public const int PartyCapacity = 4;

    /// <summary>Kadroyu ekran sırasına dizer: önce hazır olan, en sonda ölüler.</summary>
    /// <remarks>
    /// Sıra rozete göredir, isim ikincil anahtardır: oyuncu ekranı "bugün kimi
    /// gönderebilirim" sorusuyla açar, cevabın listenin altında aranması gerekmesin.
    /// </remarks>
    public static IReadOnlyList<RosterRow> Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        return dojo.Roster.Entries
            .Select(entry => Describe(entry, dojo.Tuning))
            .OrderBy(row => (int)row.Status)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Tek savaşçının satırı.</summary>
    public static RosterRow Describe(RosterEntry entry, DojoTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(tuning);

        Warrior warrior = entry.Warrior;
        int pathDays = tuning.Training.PathTrainingDays;
        bool unlocked = warrior.IsAlive && entry.TrainingDays >= pathDays;

        return new RosterRow(
            Id: entry.Id,
            Name: entry.Name,
            IsAlive: warrior.IsAlive,
            Status: StatusOf(entry),
            RecoveryDaysRemaining: entry.RecoveryDaysRemaining,
            Activity: entry.Activity,
            Drill: entry.Drill,
            Path: warrior.Path,
            PathUnlocked: unlocked,
            TrainingDaysToPath: Math.Max(0, pathDays - entry.TrainingDays),
            TrainingDays: entry.TrainingDays,
            Honor: warrior.Honor,
            BaseStats: warrior.BaseStats,
            EffectiveStats: warrior.EffectiveStats,
            Lost: LostParts(warrior),
            WeaponName: warrior.UsableWeapon.Name,
            ArmorName: warrior.Armor.Name,
            ArmorWear: warrior.ArmorWear.Total,
            IsFitForCampaign: entry.IsFitForCampaign);
    }

    /// <summary>Kadronun tepesindeki sayılar.</summary>
    public static RosterSummary Summarize(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        int living = 0;
        int fit = 0;
        int recovering = 0;
        int fallen = 0;

        foreach (RosterEntry entry in dojo.Roster.Entries)
        {
            if (!entry.Warrior.IsAlive)
            {
                fallen++;
                continue;
            }

            living++;

            if (entry.IsFitForCampaign)
            {
                fit++;
            }
            else
            {
                recovering++;
            }
        }

        return new RosterSummary(living, fit, recovering, fallen, PartyCapacity);
    }

    /// <summary>
    /// Ad değişikliği kabul edilir mi? Ekran bunu <b>yazarken</b> sorar,
    /// <see cref="Roster.Rename"/> fırlatmadan önce.
    /// </summary>
    public static RenameVerdict JudgeRename(Roster roster, WarriorId id, string? newName)
    {
        ArgumentNullException.ThrowIfNull(roster);

        if (string.IsNullOrWhiteSpace(newName))
        {
            return RenameVerdict.Empty;
        }

        RosterEntry? entry = roster.Find(id);
        if (entry is not null && string.Equals(entry.Name, newName, StringComparison.OrdinalIgnoreCase))
        {
            return RenameVerdict.Unchanged;
        }

        return roster.IsNameTaken(newName) ? RenameVerdict.Taken : RenameVerdict.Ok;
    }

    private static RosterStatus StatusOf(RosterEntry entry)
    {
        if (!entry.Warrior.IsAlive)
        {
            return RosterStatus.Fallen;
        }

        if (entry.RecoveryDaysRemaining > 0)
        {
            return RosterStatus.Recovering;
        }

        return entry.Activity == DojoActivity.Training
            ? RosterStatus.Training
            : RosterStatus.Ready;
    }

    private static BodyPartSet LostParts(Warrior warrior)
    {
        BodyPartSet lost = BodyPartSet.None;
        foreach (Disability disability in warrior.Disabilities)
        {
            lost |= disability.Part.AsFlag();
        }

        return lost;
    }
}
