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

    /// <summary>Weapon master — permanent mastery of the weapon a warrior carries (docs/GDD.md §10).</summary>
    WeaponMaster,

    /// <summary>Physician — the health branch's post: no medicine bill, and limbs he can still save.</summary>
    Physician,

    /// <summary>Smith — the equipment branch's post: cheaper repairs, and the gate to ō-yoroi.</summary>
    Smith,

    /// <summary>Steward — the supply branch's post: the expense side only, never the reward.</summary>
    Steward,

    /// <summary>Broker — changes what the market puts out, never its prices.</summary>
    Broker,

    /// <summary>Bard — the day's morale gain in his hall.</summary>
    Bard,

    /// <summary>Monk — the omamori slots, the funeral rite, and the temple's regard.</summary>
    Monk,

    /// <summary>Cook — does not produce, cuts consumption.</summary>
    Cook,

    /// <summary>Diviner — reads the enemy in the day's offer.</summary>
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

    /// <summary>How many charms a warrior may wear with the shrine standing and nobody in it.</summary>
    /// <remarks>
    /// The shrine is a <b>building</b>, so the half-efficiency rule would say "half a slot" — and half
    /// a slot is nothing. The rule is written in whole slots instead: the empty shrine keeps one open,
    /// the monk opens the second. That is GDD §10's own wording for the post, "opens omamori slots",
    /// and it is the one place in the staff economy where the person is worth a discrete thing rather
    /// than a share.
    /// </remarks>
    public int OmamoriSlotsEmptyShrine { get; init; } = 1;

    /// <summary>How many charms a warrior may wear with a monk in the shrine.</summary>
    public int OmamoriSlotsWithMonk { get; init; } = 2;

    /// <summary>The share of a charm's price the temple gives back for it.</summary>
    /// <remarks>
    /// Below 1, or a charm would be a savings account the player empties whenever a bill falls due.
    /// What it must stay is a <b>decision that can be undone at a cost</b> — that is what makes fitting
    /// one to a man who may die this week a real risk rather than a formality.
    /// </remarks>
    public double OmamoriResaleShare { get; init; } = 0.5;

    /// <summary>
    /// What a <b>retired warrior</b> is worth in a post he is good at, against a hire.
    /// </summary>
    /// <remarks>
    /// Above 1 because GDD §10 says so outright — "better than hired" — and because the whole point of
    /// the long game is that the man who survived it is worth more than the man you can buy. He also
    /// costs no wage, which is the larger half of the gift.
    /// </remarks>
    public double MasterEfficiency { get; init; } = 1.15;

    /// <summary>What he is worth in a post he is only passable at.</summary>
    public double MasterMediumEfficiency { get; init; } = 1.0;

    /// <summary>And in one he half understands.</summary>
    public double MasterWeakEfficiency { get; init; } = 0.5;

    /// <summary>What the monk in the shrine is worth to the temple's regard, every week.</summary>
    /// <remarks>
    /// GDD §10's third line for the post — "raises the temple relationship" — and the only standing in
    /// the game that is bought with a wage rather than with work. It is small and weekly rather than
    /// daily, so it can hold a tier against neglect but cannot climb one on its own.
    /// </remarks>
    public double MonkTempleRegardPerWeek { get; init; } = 3;

    /// <summary>The share of a comrade's death the funeral rite takes off the survivors.</summary>
    /// <remarks>
    /// This is the monk's second job (GDD §10) and the only one that touches the fight's aftermath. It
    /// is a share of the blow rather than a flat number, so it keeps its meaning on the night a party
    /// loses three men — which is the night a dojo actually spirals.
    /// </remarks>
    public double FuneralRelief { get; init; } = 0.5;

    /// <summary>What a master of the house is worth in this post; 0 if he is barred from it.</summary>
    /// <remarks>
    /// The tiers are GDD §10's own table, and the <b>bar</b> is an economic decision rather than a
    /// fictional one: if free retirees could fill every building, no building would ever stand empty,
    /// wages would leave the economy and the "cut the staff, keep the building" gear would spin free.
    /// The three barred roles are the ones that need an outsider — a physician, a cook, a diviner.
    /// </remarks>
    public double MasterWorth(StaffRole role) => role switch
    {
        StaffRole.DrillMaster or StaffRole.KataMaster or StaffRole.WeaponMaster => MasterEfficiency,
        StaffRole.Steward or StaffRole.Broker or StaffRole.Monk => MasterMediumEfficiency,
        StaffRole.Smith or StaffRole.Bard => MasterWeakEfficiency,
        _ => 0,
    };

    /// <summary>Can a retired warrior hold this post at all?</summary>
    public bool MasterMayHold(StaffRole role) => MasterWorth(role) > 0;

    /// <summary>The charm slots a dojo with this shrine and this staff opens.</summary>
    public int OmamoriSlots(bool shrine, bool monk) => !shrine
        ? 0
        : monk ? OmamoriSlotsWithMonk : OmamoriSlotsEmptyShrine;

    /// <summary>The wage of a role.</summary>
    public int WageOf(StaffRole role) => Facilities.IsBranchRole(role)
        ? BranchWagePerDay
        : SituationalWagePerDay;
}

/// <summary>Who is in which post today.</summary>
/// <remarks>
/// <para>
/// It holds state and it does not decide: hiring's gold, the daily wage and the effects belong to
/// <see cref="DojoState"/>. What goes into the save is <b>which roles are filled and by whom</b> — a
/// wage is a balance number and comes from the code.
/// </para>
/// <para>
/// A post is held either by an outsider on a wage or by a <b>retired warrior</b> of the dojo's own,
/// who draws none (docs/GDD.md §10). That is the whole reason the post remembers a name rather than a
/// yes/no: the payroll and the efficiency both depend on which of the two is standing there.
/// </para>
/// </remarks>
public sealed class Staff
{
    private readonly Dictionary<StaffRole, Model.WarriorId?> _posts = [];

    /// <summary>The roles filled today.</summary>
    public IReadOnlyCollection<StaffRole> Hired => _posts.Keys;

    /// <summary>The posts, each with the retired warrior holding it or <c>null</c> for an outsider.</summary>
    public IReadOnlyDictionary<StaffRole, Model.WarriorId?> Posts => _posts;

    public bool Has(StaffRole role) => _posts.ContainsKey(role);

    /// <summary>The retired warrior in this post, if it is one of the dojo's own.</summary>
    public Model.WarriorId? MasterIn(StaffRole role) =>
        _posts.TryGetValue(role, out Model.WarriorId? master) ? master : null;

    /// <summary>Is this post held by a master of the house rather than by a hire?</summary>
    public bool HeldByMaster(StaffRole role) => MasterIn(role) is not null;

    /// <summary>The day's payroll — a retired man costs nothing.</summary>
    public int DailyWage(StaffTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);

        int total = 0;
        foreach ((StaffRole role, Model.WarriorId? master) in _posts)
        {
            if (master is null)
            {
                total += tuning.WageOf(role);
            }
        }

        return total;
    }

    /// <summary>Takes the post. <c>false</c> if it is already filled.</summary>
    internal bool Add(StaffRole role, Model.WarriorId? master = null) => _posts.TryAdd(role, master);

    /// <summary>Lets the person go, or sends the master back to his own room. The building stays.</summary>
    internal bool Remove(StaffRole role) => _posts.Remove(role);

    /// <summary>Restores the posts coming from the save, dropping any whose building is not standing.</summary>
    internal void Restore(IEnumerable<(StaffRole Role, Model.WarriorId? Master)> posts, School school)
    {
        ArgumentNullException.ThrowIfNull(school);

        _posts.Clear();
        foreach ((StaffRole role, Model.WarriorId? master) in posts)
        {
            if (school.HasPostFor(role))
            {
                _posts[role] = master;
            }
        }
    }
}
