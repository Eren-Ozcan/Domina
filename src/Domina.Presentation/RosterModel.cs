using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>A single row on the roster screen — a warrior's state that day.</summary>
/// <param name="Id">The warrior's identity; the screen's commands return it.</param>
/// <param name="Name">Display name.</param>
/// <param name="IsAlive">The dead stay on the roster (permadeath is permanent, the record is permanent).</param>
/// <param name="Status">The row's status badge.</param>
/// <param name="RecoveryDaysRemaining">The days left in the infirmary; zero means ready for an expedition.</param>
/// <param name="Activity">Today's occupation.</param>
/// <param name="Drill">The drill selected — kept while in the infirmary too.</param>
/// <param name="Path">The path chosen; <see cref="WarriorPath.None"/> means none yet.</param>
/// <param name="PathUnlocked">Has the path choice been unlocked?</param>
/// <param name="TrainingDaysToPath">The training days left to unlock the path; 0 if it is open.</param>
/// <param name="TrainingDays">The training days completed.</param>
/// <param name="Honor">Onur (0-100).</param>
/// <param name="BaseStats">The raw stats — the number training writes.</param>
/// <param name="EffectiveStats">The number the fight reads, after the path and disabilities.</param>
/// <param name="Lost">The limbs lost.</param>
/// <param name="WeaponName">The weapon he can use — fists if he has lost a limb.</param>
/// <param name="ArmorName">The kit's name.</param>
/// <param name="ArmorWear">The total wear on the kit.</param>
/// <param name="IsFitForCampaign">Can he be sent on an expedition today?</param>
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

/// <summary>The row's badge — it also sets the ordering.</summary>
public enum RosterStatus
{
    /// <summary>Ready for an expedition.</summary>
    Ready,

    /// <summary>Antrenmanda; sefere yine de gidebilir.</summary>
    Training,

    /// <summary>In the infirmary; cannot go on an expedition.</summary>
    Recovering,

    /// <summary>Dead. The record stays on the roster.</summary>
    Fallen,
}

/// <summary>The numbers standing at the top of the roster.</summary>
/// <param name="Living">The number of living warriors.</param>
/// <param name="Fit">Those who can go on an expedition today.</param>
/// <param name="Recovering">Revirdekiler.</param>
/// <param name="Fallen">The dead.</param>
/// <param name="PartyCapacity">The maximum warriors who can go on one expedition (GDD §1).</param>
public readonly record struct RosterSummary(
    int Living,
    int Fit,
    int Recovering,
    int Fallen,
    int PartyCapacity);

/// <summary>The result of a rename attempt.</summary>
public enum RenameVerdict
{
    /// <summary>Kabul edilir.</summary>
    Ok,

    /// <summary>An empty name.</summary>
    Empty,

    /// <summary>The name belongs to another <b>living</b> warrior; the dead have returned their names to the pool.</summary>
    Taken,

    /// <summary>The name is already this warrior's; no change.</summary>
    Unchanged,
}

/// <summary>
/// The model the roster screen reads. It computes the numbers and the badges; it does not draw.
/// </summary>
/// <remarks>
/// If the screen read <see cref="Roster"/> directly, two jobs would leak: which warrior comes first,
/// and <b>knowing in advance that a rename will be refused</b>. The second matters:
/// <see cref="Roster.Rename"/> throws on a clashing name, and the screen must be able to disable the
/// button before it is even pressed.
/// </remarks>
public static class RosterModel
{
    /// <summary>The maximum warriors who can go on one expedition (GDD §1 — upper limit 4).</summary>
    public const int PartyCapacity = 4;

    /// <summary>Orders the roster for the screen: the ready first, the dead last.</summary>
    /// <remarks>
    /// The order is by badge, with the name as the secondary key: the player opens the screen with the
    /// question "whom can I send today", and the answer should not have to be looked for at the bottom of the list.
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

    /// <summary>A single warrior's row.</summary>
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

    /// <summary>The numbers at the top of the roster.</summary>
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
    /// Is the rename accepted? The screen asks this <b>while typing</b>, before
    /// <see cref="Roster.Rename"/> throws.
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
