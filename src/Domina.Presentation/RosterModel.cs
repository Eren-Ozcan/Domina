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
/// <param name="WeaponSkill">
/// What he has learned of the weapon in his hand, 0-1 (docs/GDD.md §10). It belongs to the pairing of
/// the man and that weapon, so the screen prints it beside the weapon's name and not beside his stats.
/// </param>
/// <param name="Morale">His own condition today, 0-100.</param>
/// <param name="Charms">The temple charms he is wearing.</param>
/// <param name="CharmSlots">How many he may wear today — the shrine opens them, the monk the second.</param>
/// <param name="IsFitForCampaign">Can he be sent on an expedition today?</param>
/// <param name="CanBeReleased">
/// Can his term be ended today? A dead man, a man already gone and a man in the infirmary cannot —
/// letting a wounded man out of the gate to save his upkeep is the door the hungry-day rule closes.
/// </param>
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
    bool IsFitForCampaign,
    bool CanBeReleased = false,
    double WeaponSkill = 0,
    double Morale = MoraleScale.Starting,
    IReadOnlyList<OmamoriKind>? Charms = null,
    int CharmSlots = 0);

/// <summary>The row's badge — it also sets the ordering.</summary>
public enum RosterStatus
{
    /// <summary>Ready for an expedition.</summary>
    Ready,

    /// <summary>Antrenmanda; sefere yine de gidebilir.</summary>
    Training,

    /// <summary>In the infirmary; cannot go on an expedition.</summary>
    Recovering,

    /// <summary>His term ended and he walked out free. The record stays, like a dead man's.</summary>
    Freed,

    /// <summary>Dead. The record stays on the roster.</summary>
    Fallen,
}

/// <summary>The numbers standing at the top of the roster.</summary>
/// <param name="Living">The number of living warriors.</param>
/// <param name="Fit">Those who can go on an expedition today.</param>
/// <param name="Recovering">Revirdekiler.</param>
/// <param name="Fallen">The dead.</param>
/// <param name="PartyCapacity">The maximum warriors who can go on one expedition (GDD §1).</param>
/// <param name="Freed">The men whose term ended — they walked out and are on the closing screen.</param>
/// <param name="Beds">How many men the dojo can house at all — the quarters branch raises it.</param>
/// <param name="Morale">The roster's average condition today, 0-100 (docs/GDD.md §3).</param>
/// <param name="Sake">The measures in the store — a feast drinks one per living man.</param>
/// <param name="FeastSake">What a feast would drink today.</param>
/// <param name="CanFeast">Is there sake, and has the cooldown passed?</param>
/// <param name="DaysToFeast">The days left on the cooldown; 0 when it is clear.</param>
public readonly record struct RosterSummary(
    int Living,
    int Fit,
    int Recovering,
    int Fallen,
    int PartyCapacity,
    int Freed = 0,
    int Beds = 0,
    double Morale = MoraleScale.Starting,
    int Sake = 0,
    int FeastSake = 0,
    bool CanFeast = false,
    int DaysToFeast = 0);

/// <summary>Where a warrior's condition sits on the scale — the word the screen prints.</summary>
/// <remarks>
/// A band rather than a number on the row itself: morale is a condition, not a stat, and printing
/// "63" beside eight real stats would invite the player to train it. The number is still there for
/// anyone who wants it, in the detail panel.
/// </remarks>
public enum MoraleBandName
{
    /// <summary>Broken — the bottom of the scale, where the panic check bites hardest.</summary>
    Broken,

    /// <summary>Low.</summary>
    Low,

    /// <summary>Steady — the middle, where the multiplier is exactly 1.</summary>
    Steady,

    /// <summary>Good.</summary>
    Good,

    /// <summary>High — bought with a victory, a feast or the bard, and it drifts back down.</summary>
    High,
}

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
            .Select(entry => Describe(entry, dojo.Tuning, dojo.OmamoriSlots))
            .OrderBy(row => (int)row.Status)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>A single warrior's row.</summary>
    public static RosterRow Describe(RosterEntry entry, DojoTuning tuning, int charmSlots = 0)
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
            IsFitForCampaign: entry.IsFitForCampaign,
            CanBeReleased: warrior.IsAlive && !entry.Released && entry.RecoveryDaysRemaining == 0,
            WeaponSkill: warrior.WeaponSkill,
            Morale: warrior.Morale,
            Charms: warrior.Charms,
            CharmSlots: charmSlots);
    }

    /// <summary>The numbers at the top of the roster.</summary>
    public static RosterSummary Summarize(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        int living = 0;
        int fit = 0;
        int recovering = 0;
        int fallen = 0;
        int freed = 0;

        foreach (RosterEntry entry in dojo.Roster.Entries)
        {
            if (!entry.Warrior.IsAlive)
            {
                fallen++;
                continue;
            }

            // A released man is on neither side of the ledger: he is not a loss and he is not a mouth.
            if (entry.Released)
            {
                freed++;
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

        return new RosterSummary(
            living,
            fit,
            recovering,
            fallen,
            PartyCapacity,
            freed,
            dojo.Capacity,
            living == 0 ? MoraleScale.Starting : dojo.Roster.Living.Average(e => e.Warrior.Morale),
            dojo.Resources.Sake,
            dojo.FeastSake,
            dojo.CanFeast,
            dojo.LastFeastDay is int last
                ? Math.Max(0, dojo.Tuning.Morale.FeastCooldownDays - (dojo.Day - last))
                : 0);
    }

    /// <summary>The band a morale value falls in.</summary>
    public static MoraleBandName Band(double morale) => morale switch
    {
        < 20 => MoraleBandName.Broken,
        < 40 => MoraleBandName.Low,
        < 60 => MoraleBandName.Steady,
        < 80 => MoraleBandName.Good,
        _ => MoraleBandName.High,
    };

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

        if (entry.Released)
        {
            return RosterStatus.Freed;
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
