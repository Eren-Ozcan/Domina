using Domina.Core.Combat;
using Domina.Core.Honor;
using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>Writes the fight's result onto the roster.</summary>
/// <remarks>
/// <para>
/// The core <b>does not touch</b> the persistent state: it says what happened, not what will happen.
/// Death, limb loss, broken armour and accumulated wear are <b>reports</b> in the fight summary;
/// turning them into something irreversible is this class's job. The reason for the separation is the
/// architecture rule — batch simulation runs the same roster tens of thousands of times and the
/// warrior's persistent state must not be corrupted in any of them.
/// </para>
/// <para>
/// Only the <b>dojo side's</b> summaries are processed. The yokai carry their own identities and those
/// identities can collide with the roster's; without the team filter, an arm lost by an enemy could be
/// written onto a warrior in the dojo.
/// </para>
/// </remarks>
public sealed class BattleAftermath(HonorEngine? honor = null)
{
    private readonly HonorEngine _honor = honor ?? new HonorEngine();

    /// <summary>Applies the fight result to the roster and returns what changed.</summary>
    public AftermathReport Apply(DojoState state, BattleResult result)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);

        List<WarriorAftermath> lines = [];
        foreach (WarriorBattleSummary summary in result.Summaries)
        {
            if (summary.Team != Battle.PlayerTeam)
            {
                continue;
            }

            RosterEntry? entry = state.Roster.Find(summary.Id);
            if (entry is null || !entry.Warrior.IsAlive)
            {
                continue;
            }

            lines.Add(ApplyTo(state, entry, summary));
        }

        return new AftermathReport(result.Outcome, lines);
    }

    private WarriorAftermath ApplyTo(DojoState state, RosterEntry entry, WarriorBattleSummary summary)
    {
        Warrior warrior = entry.Warrior;

        List<BodyPart> lost = [];
        foreach (BodyPart part in summary.LostParts.Parts())
        {
            if (warrior.AddDisability(part))
            {
                lost.Add(part);
            }
        }

        WearArmor(warrior, summary);

        List<HitLocation> shattered = [];
        foreach (HitLocation slot in summary.DestroyedArmor.Slots())
        {
            shattered.Add(slot);
            StripSlot(warrior, slot);
        }

        if (summary.Died)
        {
            state.Roster.Kill(warrior.Id);
            return new WarriorAftermath(warrior.Id, Died: true, lost, shattered, RecoveryDays: 0, HonorDelta: 0);
        }

        double honorDelta = _honor.PerformanceDelta(summary) + _honor.RetreatDelta(summary);
        warrior.Honor = HonorScale.Clamp(warrior.Honor + honorDelta);

        int days = RecoveryDays(state.Tuning, warrior, summary, lost.Count);
        entry.Injure(days);

        return new WarriorAftermath(warrior.Id, Died: false, lost, shattered, days, honorDelta);
    }

    /// <summary>
    /// Adds the damage absorbed in the fight to the warrior's permanent wear ledger.
    /// </summary>
    private static void WearArmor(Warrior warrior, WarriorBattleSummary summary)
    {
        ArmorWearSet total = warrior.ArmorWear;
        foreach (HitLocation slot in ArmorSlots.All)
        {
            double added = summary.ArmorWear.At(slot);
            if (added > 0)
            {
                total = total.With(slot, total.At(slot) + added);
            }
        }

        warrior.ArmorWear = total;
    }

    /// <summary>
    /// Removes a broken piece from the kit and resets that slot's wear.
    /// </summary>
    /// <remarks>
    /// The reset is required: wear belongs to <b>the piece</b>, not to the slot. If the counter stayed,
    /// the brand-new piece fitted in its place would inherit the broken piece's ledger and break on the
    /// first blow.
    /// </remarks>
    private static void StripSlot(Warrior warrior, HitLocation slot)
    {
        warrior.Armor = warrior.Armor.With(slot, ArmorPiece.Bare);
        warrior.ArmorWear = warrior.ArmorWear.With(slot, 0);
    }

    /// <summary>How many days the warrior cannot go on an expedition.</summary>
    /// <remarks>
    /// It comes from two items: the share of the damage taken and the number of limbs lost. The numbers
    /// are <b>not locked</b> — GDD §7 only says "according to the severity of the wound", the duration
    /// will be measured in the economy pass (Open Decision #5).
    /// </remarks>
    private static int RecoveryDays(
        DojoTuning tuning,
        Warrior warrior,
        WarriorBattleSummary summary,
        int lostLimbs)
    {
        double maxHealth = warrior.EffectiveStats.MaxHealth;
        double lostShare = maxHealth <= 0
            ? 0
            : Math.Clamp(1 - (summary.HealthRemaining / maxHealth), 0, 1);

        double free = Math.Clamp(tuning.RecoveryFreeDamageShare, 0, 0.99);
        double paid = Math.Max(0, lostShare - free) / (1 - free);

        int fromWounds = (int)Math.Ceiling(paid * tuning.RecoveryDaysAtFullDamage);
        return fromWounds + (lostLimbs * tuning.RecoveryDaysPerLostLimb);
    }
}

/// <summary>A fight as written onto the roster.</summary>
/// <param name="Outcome">The fight's result.</param>
/// <param name="Warriors">The books of every warrior on the dojo side.</param>
public sealed record AftermathReport(BattleOutcome Outcome, IReadOnlyList<WarriorAftermath> Warriors)
{
    public IEnumerable<WarriorAftermath> Dead => Warriors.Where(w => w.Died);

    /// <summary>The warriors put in the infirmary.</summary>
    public IEnumerable<WarriorAftermath> Wounded => Warriors.Where(w => !w.Died && w.RecoveryDays > 0);
}

/// <param name="Id">The warrior.</param>
/// <param name="Died">Did he fail to come out of the fight alive — there is no way back.</param>
/// <param name="LostParts">The limbs permanently lost in this fight.</param>
/// <param name="ShatteredArmor">The armour slots that broke and were removed from the kit.</param>
/// <param name="RecoveryDays">How many days he cannot go on an expedition.</param>
/// <param name="HonorDelta">The fight's effect on honour.</param>
public sealed record WarriorAftermath(
    WarriorId Id,
    bool Died,
    IReadOnlyList<BodyPart> LostParts,
    IReadOnlyList<HitLocation> ShatteredArmor,
    int RecoveryDays,
    double HonorDelta);
