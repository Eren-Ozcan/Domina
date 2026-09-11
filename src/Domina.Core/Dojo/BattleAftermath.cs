using Domina.Core.Combat;
using Domina.Core.Honor;
using Domina.Core.Model;
using Domina.Core.Rng;

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
/// Only the <b>dojo side's</b> summaries are processed. The enemies carry their own identities and those
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

        // Counted before the roster is written, because a man who dies in this fight still has to weigh
        // on the men who saw it (docs/GDD.md §3).
        int fallen = result.Summaries.Count(s => s.Team == Battle.PlayerTeam && s.Died);
        bool won = result.Outcome == BattleOutcome.PlayerVictory;

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

        SettleMorale(state, result, won, fallen);

        return new AftermathReport(result.Outcome, lines);
    }

    /// <summary>
    /// Does the infirmary keep the limb the fight took?
    /// </summary>
    /// <remarks>
    /// <para>
    /// The die is rolled <b>in the dojo, not in the fight</b>: the resolver still says the limb came
    /// off, and the event stream a viewer watches does not change. What changes is what the dojo writes
    /// down afterwards — which is the only place a physician could plausibly act anyway.
    /// </para>
    /// <para>
    /// It is the answer to the measured <b>infirmary trap</b>: the branch used to sell time, and time
    /// is what this economy has spare. Bound to limbs it sells the one loss training cannot undo. The
    /// gate needs <b>both</b> the bone setter's room and a physician in it — a share of a saved limb is
    /// meaningless, so half-efficiency does not apply.
    /// </para>
    /// <para>
    /// The seed is built from the dojo's own seed, the day and the warrior, so the same season replays
    /// identically and reloading the save cannot re-roll a lost arm.
    /// </para>
    /// </remarks>
    private static bool SavedByThePhysician(DojoState state, RosterEntry entry, BodyPart part)
    {
        if (!state.School.Has(SchoolNodeId.BoneSetter) || !state.Staff.Has(StaffRole.Physician))
        {
            return false;
        }

        ulong seed = state.Seed
            + ((ulong)state.Day * 7919UL)
            + ((ulong)entry.Warrior.Id.Value * 104_729UL)
            + (ulong)(part + 1);

        return new SeededRandom(seed).Chance(state.StaffTuning.LimbSaveChance);
    }

    /// <summary>
    /// Writes the fight onto the morale of everyone who came back from it.
    /// </summary>
    /// <remarks>
    /// It runs after the roster is written, so it sees who actually lived — including a man the
    /// physician pulled back, who is a survivor and not a funeral. A warrior who broke and ran carries
    /// the defeat <b>and</b> his own panic; the rest carry only what the day did.
    /// </remarks>
    private static void SettleMorale(DojoState state, BattleResult result, bool won, int fallen)
    {
        MoraleTuning morale = state.Tuning.Morale;

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

            if (won)
            {
                MoraleLedger.Raise(entry.Warrior, morale.VictoryGain);
            }
            else
            {
                MoraleLedger.Lower(entry.Warrior, morale.DefeatLoss, morale);
            }

            if (summary.Panicked)
            {
                MoraleLedger.Lower(entry.Warrior, morale.PanicLoss, morale);
            }

            // A comrade's death weighs once per man lost: two funerals hurt twice as much as one.
            if (fallen > 0)
            {
                MoraleLedger.Lower(entry.Warrior, morale.ComradeLoss * fallen, morale);
            }
        }
    }

    /// <summary>
    /// Does the physician turn a mortal wound around?
    /// </summary>
    /// <remarks>
    /// The gate needs the infirmary <b>and</b> a physician in it: a building with nobody in it cannot
    /// half-save a life. Like the limb die it is rolled in the dojo and seeded from the season, so a
    /// reload cannot re-roll a death.
    /// </remarks>
    private static bool PulledBackFromDeath(DojoState state, RosterEntry entry)
    {
        if (!state.School.Has(SchoolNodeId.Infirmary) || !state.Staff.Has(StaffRole.Physician))
        {
            return false;
        }

        ulong seed = state.Seed
            + ((ulong)state.Day * 15_486_071UL)
            + ((ulong)entry.Warrior.Id.Value * 32_452_843UL);

        return new SeededRandom(seed).Chance(state.StaffTuning.MortalSaveChance);
    }

    private WarriorAftermath ApplyTo(DojoState state, RosterEntry entry, WarriorBattleSummary summary)
    {
        Warrior warrior = entry.Warrior;

        List<BodyPart> lost = [];
        List<BodyPart> saved = [];
        foreach (BodyPart part in summary.LostParts.Parts())
        {
            if (SavedByThePhysician(state, entry, part))
            {
                saved.Add(part);
                continue;
            }

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

        if (summary.Died && PulledBackFromDeath(state, entry))
        {
            // He does not come out of it whole: the wound is what a mortal wound is, only survived. The
            // fight's own lesson is skipped — a man carried off the field learns nothing that day.
            entry.Injure(state.StaffTuning.MortalWoundRecoveryDays);

            return new WarriorAftermath(
                warrior.Id,
                Died: false,
                lost,
                shattered,
                state.StaffTuning.MortalWoundRecoveryDays,
                HonorDelta: 0)
            {
                SavedParts = saved,
                PulledBack = true,
            };
        }

        if (summary.Died)
        {
            state.Roster.Kill(warrior.Id);

            // The dead learn nothing: the lesson is applied only to a warrior who came off the field.
            return new WarriorAftermath(warrior.Id, Died: true, lost, shattered, RecoveryDays: 0, HonorDelta: 0)
            {
                SavedParts = saved,
            };
        }

        // What the fight taught him. It is written before the infirmary days, because the lesson is the
        // fight's own return: a warrior who comes back wounded still comes back having learned.
        Drill? lesson = CombatSchooling.LessonOf(summary);
        if (lesson is not null)
        {
            // The raw stat is written, as in training: a disability's multiplier sits on top of it and
            // no amount of fighting takes an arm back (GDD §7).
            warrior.BaseStats = CombatSchooling.After(
                warrior.BaseStats,
                summary,
                warrior.Talent,
                state.Tuning.Training);

            entry.TrainingDays++;
        }

        double honorDelta = _honor.PerformanceDelta(summary) + _honor.RetreatDelta(summary);
        warrior.Honor = HonorScale.Clamp(warrior.Honor + honorDelta);

        int days = RecoveryDays(state.Tuning, warrior, summary, lost.Count);
        entry.Injure(days);

        return new WarriorAftermath(warrior.Id, Died: false, lost, shattered, days, honorDelta)
        {
            Lesson = lesson,
            SavedParts = saved,
        };
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
    double HonorDelta)
{
    /// <summary>
    /// What the fight taught him — <c>null</c> if it taught nothing, and always <c>null</c> for the dead.
    /// </summary>
    /// <remarks>
    /// The victory screen shows this: the day's return is not only gold, and a warrior who came back
    /// wounded still came back having learned something (docs/COMPARISON-DOMINA.md, section 3).
    /// </remarks>
    public Drill? Lesson { get; init; }

    /// <summary>
    /// The limbs the fight took and the infirmary kept. The day's report should say so out loud — it is
    /// the whole return of the health branch.
    /// </summary>
    public IReadOnlyList<BodyPart> SavedParts { get; init; } = [];

    /// <summary>
    /// Was he carried off the field as a dead man and kept alive by the physician? The day's report has
    /// to say so — it is the single most valuable thing the dojo's gold buys.
    /// </summary>
    public bool PulledBack { get; init; }
}
