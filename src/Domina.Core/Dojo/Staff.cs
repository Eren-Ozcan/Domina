namespace Domina.Core.Dojo;

/// <summary>
/// The eleven professions a dojo can employ (docs/GDD.md §10).
/// </summary>
/// <remarks>
/// <para>
/// A role is bound to a <b>building</b>: one person per building, so the role is also the name of the
/// facility's post. What limits the payroll is not a slot count but the <b>daily wage</b> — cutting
/// staff in a crisis is the economy's gearbox, and an artificial "3 staff at a time" ceiling was
/// deliberately not taken.
/// </para>
/// <para>
/// Four of the roles carry a branch — they answer the dojo's four continuous expenses: training,
/// health, equipment, supply. The remaining seven are situational.
/// </para>
/// </remarks>
public enum StaffRole
{
    /// <summary>Drill master — the training branch's post, and the dojo's free starting staff member.</summary>
    DrillMaster,

    /// <summary>Kata master — raises the ceiling a warrior can be trained towards.</summary>
    KataMaster,

    /// <summary>Weapon master — permanent weapon mastery. <b>No effect in the code yet.</b></summary>
    WeaponMaster,

    /// <summary>Physician — the health branch's post: no medicine bill, and limbs he can still save.</summary>
    Physician,

    /// <summary>Smith — the equipment branch's post: cheaper repairs, and the gate to ō-yoroi.</summary>
    Smith,

    /// <summary>Steward — the supply branch's post: the expense side only, never the reward.</summary>
    Steward,

    /// <summary>Broker — changes what the market puts out, never its prices.</summary>
    Broker,

    /// <summary>Bard — daily morale. <b>No effect in the code yet</b> (morale is step 6).</summary>
    Bard,

    /// <summary>Monk — omamori slots and the funeral rite. <b>No effect in the code yet.</b></summary>
    Monk,

    /// <summary>Cook — does not produce, cuts consumption.</summary>
    Cook,

    /// <summary>Diviner — reads the enemy in an offer. <b>No effect in the code yet.</b></summary>
    Diviner,
}

/// <summary>The numbers of the staff economy.</summary>
/// <remarks>
/// They sit apart from <see cref="SchoolTuning"/> — the facility's numbers are what the <b>building</b>
/// does, these are what the <b>person</b> costs and what only a person can do. Balance numbers never go
/// into the save (GDD §2), so a wage change reaches an old save too.
/// </remarks>
public sealed record StaffTuning
{
    /// <summary>The daily wage of a role that carries a branch.</summary>
    /// <remarks>
    /// The four branch roles are the ones a dojo keeps for the whole season, so their wage is the real
    /// weight on the payroll; the situational seven are hired for a stretch and let go.
    /// </remarks>
    public int BranchWagePerDay { get; init; } = 6;

    /// <inheritdoc cref="BranchWagePerDay"/>
    public int SituationalWagePerDay { get; init; } = 4;

    /// <summary>
    /// The share of its own number a facility produces with nobody in it.
    /// </summary>
    /// <remarks>
    /// GDD §10: <b>the construction investment is never wasted.</b> An empty building still works, at
    /// half; staff bring it to full. What this share does <b>not</b> touch is a gate — no medicine bill,
    /// a limb saved, ō-yoroi — because those either exist or they do not, and half of a gate is nothing.
    /// </remarks>
    public double EmptyFacilityShare { get; init; } = 0.5;

    /// <summary>
    /// The chance the physician pulls a warrior back from a wound the fight counted as mortal.
    /// </summary>
    /// <remarks>
    /// This is the health branch's real answer to the measured <b>infirmary trap</b>. Rebinding the
    /// branch to limbs was tried first and measured as <b>nothing</b> — limbs are lost on 5% of
    /// warrior-fights, so a quarter of them is invisible in a season, and with the physician's wage on
    /// top the branch came out worse than an empty infirmary (79.2% of dojos closed against 75.5%).
    /// Death is the loss that actually drives a dojo under, so that is where the branch had to be bound.
    /// It is GDD §10's own wording for the post — "turns a mortal wound around".
    /// </remarks>
    public double MortalSaveChance { get; init; } = 0.25;

    /// <summary>The infirmary days a warrior pulled back from a mortal wound owes.</summary>
    /// <remarks>
    /// It has to be long enough that being saved is not free: the man is out of the season for a
    /// stretch, and the dojo that saved him still has to feed him meanwhile.
    /// </remarks>
    public int MortalWoundRecoveryDays { get; init; } = 12;

    /// <summary>The chance the physician's branch saves a limb the fight took.</summary>
    /// <remarks>
    /// This is the answer to the measured <b>infirmary trap</b>: the branch used to sell only time, and
    /// time is the one thing this economy has spare (collapse 9.8% → 14.5% when it was bought). Rebound
    /// to <b>lives</b>, it now buys back the one loss the player cannot train away. The die is rolled in
    /// the dojo, not in the fight: the resolver still says the limb came off, the dojo says whether the
    /// man kept it.
    /// </remarks>
    public double LimbSaveChance { get; init; } = 0.25;

    /// <summary>The multiplier the smith applies to a repair bill.</summary>
    public double SmithRepairFactor { get; init; } = 0.70;

    /// <summary>The multiplier the cook applies to the daily food need.</summary>
    public double CookFoodFactor { get; init; } = 0.75;

    /// <summary>How many extra candidates the broker puts in the stall.</summary>
    public int BrokerExtraCandidates { get; init; } = 2;

    /// <summary>The wage of a role.</summary>
    public int WageOf(StaffRole role) => Facilities.IsBranchRole(role)
        ? BranchWagePerDay
        : SituationalWagePerDay;
}

/// <summary>Who is employed today.</summary>
/// <remarks>
/// It holds state and it does not decide: hiring's gold, the daily wage and the effects belong to
/// <see cref="DojoState"/>. Only the <b>list of roles</b> goes into the save — a wage is a balance
/// number and comes from the code.
/// </remarks>
public sealed class Staff
{
    private readonly HashSet<StaffRole> _hired = [];

    /// <summary>The roles filled today.</summary>
    public IReadOnlyCollection<StaffRole> Hired => _hired;

    public bool Has(StaffRole role) => _hired.Contains(role);

    /// <summary>The day's payroll.</summary>
    public int DailyWage(StaffTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);

        int total = 0;
        foreach (StaffRole role in _hired)
        {
            total += tuning.WageOf(role);
        }

        return total;
    }

    /// <summary>Takes the post. <c>false</c> if it is already filled.</summary>
    internal bool Add(StaffRole role) => _hired.Add(role);

    /// <summary>Lets the person go. The building stays.</summary>
    internal bool Remove(StaffRole role) => _hired.Remove(role);

    /// <summary>Restores the posts coming from the save, dropping any whose building is not standing.</summary>
    internal void Restore(IEnumerable<StaffRole> roles, School school)
    {
        ArgumentNullException.ThrowIfNull(school);

        _hired.Clear();
        foreach (StaffRole role in roles)
        {
            if (school.HasPostFor(role))
            {
                _hired.Add(role);
            }
        }
    }
}
