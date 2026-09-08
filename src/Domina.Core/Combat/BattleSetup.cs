using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>A fight's inputs.</summary>
/// <param name="PlayerSide">The 1-3 warriors the dojo sent on the expedition.</param>
/// <param name="EnemySide">The yokai on the other side.</param>
public sealed record BattleSetup(
    IReadOnlyList<Warrior> PlayerSide,
    IReadOnlyList<Warrior> EnemySide)
{
    public CombatTuning Tuning { get; init; } = CombatTuning.Default;

    /// <summary>
    /// Should the event stream be collected? Needed for visualisation; switched off in batch simulation
    /// (tens of thousands of fights) it avoids needless allocation.
    /// </summary>
    public bool CollectEvents { get; init; } = true;

    /// <summary>
    /// The policy that makes the retreat decision. In the game the player's key
    /// (<see cref="Battle.CommandRetreat"/>) is used; in simulation a policy is supplied. <c>null</c>
    /// means nobody pulls out on his own.
    /// </summary>
    public IRetreatPolicy? RetreatPolicy { get; init; }
}

/// <summary>A fight's result.</summary>
public sealed record BattleResult(
    BattleOutcome Outcome,
    double ElapsedSeconds,
    IReadOnlyList<WarriorBattleSummary> Summaries)
{
    public WarriorBattleSummary SummaryFor(WarriorId id) =>
        Summaries.First(s => s.Id == id);
}

/// <summary>The books a single warrior came out of the fight with.</summary>
public sealed record WarriorBattleSummary(
    WarriorId Id,
    string Name,
    int Team,
    CombatState FinalState,
    double HealthRemaining,
    int AttacksMade,
    int HitsLanded,
    int TimesHit,
    int DodgesPerformed,
    double DamageDealt,
    double DamageTaken,
    bool LostLimb)
{
    /// <summary>
    /// The limbs lost in this fight. The meta layer turns them into permanent disabilities.
    /// </summary>
    /// <remarks>
    /// There can be more than one: a surrounded warrior takes an opportunity attack from every enemy in
    /// reach while fleeing (§5) and each can cost a separate limb.
    /// </remarks>
    public BodyPartSet LostParts { get; init; } = BodyPartSet.None;

    /// <summary>How many blows were met with a block.</summary>
    /// <remarks>
    /// It stands apart from the evasion counter: evasion erases the blow, a block <b>takes</b> it. The
    /// Defence stat's return can only be measured when the two are counted separately.
    /// </remarks>
    public int BlocksPerformed { get; init; }

    /// <summary>How many times he was stunned and frozen in this fight.</summary>
    /// <remarks>
    /// The blunt weapon's return can only be measured with this: a cutting weapon is rewarded with limb
    /// loss, a blunt one with this counter (docs/GDD.md §7).
    /// </remarks>
    public int TimesStunned { get; init; }

    /// <summary>How many enemies were stunned.</summary>
    public int StunsInflicted { get; init; }

    /// <summary>The cause of death if he died; null if he survived.</summary>
    /// <remarks>
    /// If death by poison and death by a blow were put in the same box, poison could not be measured:
    /// the poisoned weapon's claim is not "it kills more" but that it kills <b>differently</b>.
    /// </remarks>
    public DeathCause? DeathCause { get; init; }

    /// <summary>How many poisoned strikes were taken.</summary>
    /// <remarks>
    /// Poison's return is read from two numbers at once: this counter says how often the weapon
    /// <b>connects</b>, while <see cref="PoisonDamageTaken"/> says how much work the dose really did
    /// (docs/GDD.md §7).
    /// </remarks>
    public int TimesPoisoned { get; init; }

    /// <summary>How many enemies were poisoned.</summary>
    public int PoisonsInflicted { get; init; }

    /// <summary>The total damage taken from poison — the only damage armour never reduces.</summary>
    public double PoisonDamageTaken { get; init; }

    /// <summary>Zehirle verilen toplam hasar.</summary>
    public double PoisonDamageDealt { get; init; }

    /// <summary>How many incoming weapons were caught.</summary>
    /// <remarks>
    /// The jitte/sai's return can only be measured with this: the catching implement loses on damage and
    /// takes its gain back in this counter and in the free hits during the window the enemy stays bound
    /// (docs/GDD.md §7).
    /// </remarks>
    public int CatchesMade { get; init; }

    /// <summary>How many times his own weapon was caught and left him exposed.</summary>
    public int TimesCaught { get; init; }

    /// <summary>Did his weapon fall out of his hand in this fight?</summary>
    /// <remarks>
    /// Disarming's return shows up in neither damage nor limb loss: a warrior who loses his weapon
    /// finishes the fight with his fists — his reach, his damage and his right to catch all go at once
    /// (docs/GDD.md §7).
    /// </remarks>
    public bool Disarmed { get; init; }

    /// <summary>The damage the kit absorbed in this fight — the dojo adds it to the permanent wear.</summary>
    /// <remarks>
    /// Armour is not used up in a single fight; it wears across expeditions. How many fights a kit lasts
    /// is only known when this number is put next to the piece's durability.
    /// </remarks>
    public ArmorWearSet ArmorWear { get; init; }

    /// <summary>The armour pieces broken in this fight — a <b>permanent</b> loss.</summary>
    /// <remarks>
    /// It is reported the same way as limb loss (<see cref="LostParts"/>): the core does not touch the
    /// persistent state, it says what happened. Armour's real price only shows here — even a won fight
    /// can take a piece off the kit.
    /// </remarks>
    public HitLocationSet DestroyedArmor { get; init; } = HitLocationSet.None;

    /// <summary>How many times a weapon fell out of the hand in this fight.</summary>
    /// <remarks>
    /// <see cref="Disarmed"/> says the state at the <b>end</b> of the fight; only this counter says how
    /// many times the rule bit.
    /// </remarks>
    public int TimesDisarmed { get; init; }

    /// <summary>How many weapons were picked up from the ground.</summary>
    /// <remarks>
    /// It is read together with <see cref="Disarmed"/>: one says the price was incurred, this counter
    /// says whether it was settled.
    /// </remarks>
    public int WeaponsPickedUp { get; init; }

    /// <summary>How many enemy weapons were knocked out — the counter for armour and the catching implement.</summary>
    public int DisarmsInflicted { get; init; }

    /// <summary>How many charges were launched in this fight.</summary>
    /// <remarks>
    /// The charge's numbers can only be measured with these two counters: the threshold and the
    /// probability say how often a charge <b>starts</b>, while the arrival rate says whether what was
    /// started collected its return (docs/GDD.md Open Decision 11).
    /// </remarks>
    public int ChargesStarted { get; init; }

    /// <summary>How many of the charges launched reached the target.</summary>
    public int ChargesConnected { get; init; }

    /// <summary>The free hits taken during his charges — the charge's price promised in §4.</summary>
    public int ChargeOpportunitiesTaken { get; init; }

    /// <summary>The number of charges scattered by a hit during the windup.</summary>
    public int ChargesBroken { get; init; }

    /// <summary>The sum of the charge launch moments — it gives the average launch moment.</summary>
    public double ChargeStartSecondsSum { get; init; }

    /// <summary>The moment of the latest charge launched in this fight.</summary>
    public double LastChargeStartSeconds { get; init; }

    public bool Died => FinalState == CombatState.Dead;

    public bool Escaped => FinalState == CombatState.Escaped;

    /// <summary>How many of the attacks landed — the main input of the honour calculation.</summary>
    public double Accuracy => AttacksMade == 0 ? 0 : (double)HitsLanded / AttacksMade;
}
