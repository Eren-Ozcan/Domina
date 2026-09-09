using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>A warrior's temporary state during a fight.</summary>
public enum CombatState
{
    /// <summary>Taking distance / waiting for the next attack. Interruptible.</summary>
    Idle,

    /// <summary>Locked into an attack. <b>Not interruptible</b> — a flee command is buffered.</summary>
    AttackWindup,

    /// <summary>Recovery after an attack. Interruptible.</summary>
    AttackRecovery,

    /// <summary>Locked into a throwing move. <b>Not interruptible</b>, just like a melee strike.</summary>
    ThrowWindup,

    /// <summary>Recovery after a throw. Interruptible.</summary>
    ThrowRecovery,

    /// <summary>
    /// The windup before a charge: the warrior stands in place gathering force and <b>the first hit
    /// he takes scatters the charge</b>. His defence keeps working at its normal rate.
    /// </summary>
    /// <remarks>
    /// The charge's price is paid here — not by closing defence but by <b>leaving the commitment
    /// exposed</b>: the warrior does not move, and a single hit he takes spends the move. His right
    /// to evade is not taken away; the blow he cannot evade takes his charge.
    /// </remarks>
    ChargeWindup,

    /// <summary>
    /// Charging the target: accelerated, committed. <b>Not interruptible</b> — but his defence
    /// continues at its normal rate.
    /// </summary>
    Charging,

    /// <summary>
    /// In a block stance: his weapon is placed in front of the incoming blow, he is not striking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is a separate axis from evasion. Evasion makes the blow <b>miss</b> and leaves the warrior
    /// where he was; a block <b>meets</b> the blow — damage drops, no limb comes off, but the blow
    /// has landed. Its price is the attack cycle: time spent blocking is a strike not made.
    /// </para>
    /// <para>
    /// A blunt weapon goes through the block: the stun share works during a block too
    /// (<see cref="CombatTuning.BlockStunShare"/>). With no shield, this is the blunt class's fourth
    /// gain — the stance protects against steel, not against concussion.
    /// </para>
    /// <para>
    /// A flee command interrupts this state <b>immediately</b> (docs/GDD.md §5 interruption table).
    /// </para>
    /// </remarks>
    Blocking,

    /// <summary>
    /// Stunned by a heavy blow: he cannot walk, cannot strike, <b>cannot evade</b>.
    /// </summary>
    /// <remarks>
    /// The other half of the blunt weapon's trade (docs/GDD.md §7). It is not interruptible, but when
    /// its duration ends the warrior returns to the normal decision loop; a buffered flee command is
    /// processed there too — a stun does not <b>swallow</b> the command, it delays it.
    /// </remarks>
    Stunned,

    /// <summary>
    /// His weapon was caught: for as long as he is bound he cannot walk, strike or <b>evade</b>.
    /// </summary>
    /// <remarks>
    /// It is a separate state from a stun, because both its cause and its look are separate: a stunned
    /// warrior staggers under his own weight, a warrior whose weapon is caught stands <b>tied to the
    /// other man</b>. Merged into one state they could neither be told apart on screen nor could the
    /// jitte's return carry its own counter in measurement.
    /// </remarks>
    WeaponBound,

    /// <summary>Leaving the arena. Cannot evade, cannot block.</summary>
    Retreating,

    /// <summary>Left the arena alive.</summary>
    Escaped,

    /// <summary>Dead.</summary>
    Dead,
}

/// <summary>
/// The runtime state of a warrior taking part in a fight.
/// </summary>
/// <remarks>
/// <see cref="Warrior"/> holds the persistent state; this is only the temporary state of <b>this
/// fight</b>. When the fight ends, the persistent outcomes (death, maiming) are written to the warrior.
/// </remarks>
internal sealed class Combatant(Warrior warrior, int team)
{
    public Warrior Warrior { get; } = warrior;

    public int Team { get; } = team;

    public WarriorId Id => Warrior.Id;

    /// <summary>The armour pieces destroyed in this fight.</summary>
    /// <remarks>
    /// A destroyed piece leaves that region <b>bare</b>: damage reduction, dismemberment resistance
    /// and hardness all go at once — so a warrior whose armour is gone takes more damage, starts
    /// losing limbs, and no longer knocks the weapon out of the enemy he strikes.
    /// </remarks>
    public HitLocationSet DestroyedArmor { get; private set; }

    /// <summary>The slots' remaining durability; built on the first wear.</summary>
    /// <remarks>
    /// The pool is built on top of <b>permanent</b> wear: the warrior enters the fight with the armour
    /// left over from past expeditions (<see cref="Model.Warrior.ArmorWear"/>).
    /// </remarks>
    private double[]? _durability;

    /// <summary>The damage the armour absorbed in this fight — slot by slot.</summary>
    /// <remarks>
    /// The dojo layer adds this to the warrior's permanent wear; the core does not touch persistent
    /// state.
    /// </remarks>
    public ArmorWearSet ArmorWear { get; private set; }

    /// <summary>The piece covering this region <b>right now</b>.</summary>
    /// <remarks>
    /// The whole fight reads this, not <c>Warrior.Armor.At</c> — just as with the weapon: a destroyed
    /// piece cannot be gone on screen and still there in the mechanics.
    /// </remarks>
    public ArmorPiece ArmorAt(HitLocation location) =>
        DestroyedArmor.Has(location) ? ArmorPiece.Bare : Warrior.Armor.At(location);

    /// <summary>The weight of the armour still on him — a destroyed piece no longer slows him.</summary>
    public double ArmorWeight
    {
        get
        {
            double total = 0;
            foreach (HitLocation location in Enum.GetValues<HitLocation>())
            {
                total += ArmorAt(location).Weight;
            }

            return total;
        }
    }

    /// <summary>
    /// Subtracts the damage a piece absorbed from its durability.
    /// </summary>
    /// <returns>True if the piece broke on this blow.</returns>
    /// <remarks>
    /// The pool is reduced by the <b>absorbed</b> damage, not the incoming damage: what wears a piece
    /// is the blow it stops (docs/GDD.md §7).
    /// </remarks>
    public bool WearArmor(HitLocation location, double absorbed, double scale)
    {
        ArmorPiece piece = ArmorAt(location);

        if (absorbed <= 0 || piece.Durability <= 0 || scale <= 0)
        {
            return false;
        }

        _durability ??= BuildDurability(scale);

        ArmorWear = ArmorWear.With(location, ArmorWear.At(location) + absorbed);

        int slot = (int)location;
        _durability[slot] -= absorbed;

        if (_durability[slot] > 0)
        {
            return false;
        }

        DestroyedArmor |= location.AsFlag();
        return true;
    }

    private double[] BuildDurability(double scale)
    {
        HitLocation[] slots = Enum.GetValues<HitLocation>();
        var pools = new double[slots.Length];

        foreach (HitLocation location in slots)
        {
            pools[(int)location] = (Warrior.Armor.At(location).Durability * scale)
                                   - Warrior.ArmorWear.At(location);
        }

        return pools;
    }

    /// <summary>Did his weapon fall out of his hand in this fight?</summary>
    /// <remarks>
    /// The loss belongs to <b>the fight</b>: <see cref="Model.Warrior"/> holds the persistent state and
    /// the fight does not touch it (batch simulation runs the same roster tens of thousands of times).
    /// A dropped weapon returns to the warrior when the fight ends; the price is the rest of the fight
    /// (<see cref="WeaponDropped"/>).
    /// </remarks>
    public bool Disarmed { get; set; }

    /// <summary>
    /// The weapon in his hand right now. Fists if it was dropped.
    /// </summary>
    /// <remarks>
    /// The whole fight reads this, not <c>Warrior.UsableWeapon</c>: reach, speed, damage,
    /// catchability — all change when the weapon leaves the hand. If even one place read the
    /// persistent weapon, a dropped weapon would be on the ground on screen and in the hand in the mechanics.
    /// </remarks>
    public Weapon Weapon => Disarmed ? _fists : HeldWeapon ?? Warrior.UsableWeapon;

    /// <summary>A single fists instance — the combat loop reads this many times per tick.</summary>
    /// <remarks>
    /// Producing a new record on every read meant close to a hundred kilobytes of allocation per
    /// fight (<c>ThroughputTests</c> caught it); since the weapon is an unchanging value, one instance
    /// is enough.
    /// </remarks>
    private static readonly Weapon _fists = Model.Weapon.Fists();

    /// <summary>
    /// A weapon picked up from the ground. <c>null</c> means the warrior carries his own.
    /// </summary>
    /// <remarks>
    /// A weapon picked up may well be <b>the enemy's</b>: nobody asks whose the blade lying in the
    /// arena is. It is not written to persistent state — when the fight ends everyone returns to his
    /// own kit.
    /// </remarks>
    public Weapon? HeldWeapon { get; set; }

    /// <summary>Is he empty-handed — that is, will he walk to a weapon on the ground?</summary>
    /// <remarks>
    /// The measure is the weapon <b>being fists</b>, not "did he drop it": a warrior who cannot use his
    /// two-handed weapon because he lost an arm is empty-handed too and can pick up a one-handed weapon
    /// from the ground. A warrior with a weapon in hand neither picks up nor searches (docs/GDD.md §7).
    /// </remarks>
    public bool Unarmed => Weapon == _fists;

    public double Health { get; set; } = warrior.EffectiveStats.MaxHealth;

    public double Stamina { get; set; } = warrior.EffectiveStats.MaxStamina;

    public CombatState State { get; set; } = CombatState.Idle;

    /// <summary>The time left until the current state ends.</summary>
    public double StateTimer { get; set; }

    /// <summary>
    /// The enemy he is currently trying to strike. Kept until that enemy dies or flees
    /// (see <c>Battle.FindTarget</c>).
    /// </summary>
    public Combatant? Target { get; set; }

    /// <summary>His place on the arena plane.</summary>
    public ArenaPoint Position { get; set; }

    /// <summary>
    /// The direction he faces: +1 right, -1 left. An attack from behind is decided from this.
    /// </summary>
    public int Facing { get; set; } = 1;

    /// <summary>How far he travelled this tick — the visualisation drives the walk cycle from it.</summary>
    public double SpeedThisTick { get; set; }

    /// <summary>The total duration set when the current state was entered.</summary>
    /// <remarks>
    /// The visualisation drives the animation by where in the state we are; the time left is not enough
    /// for that on its own, the total duration is needed too
    /// (see <see cref="CombatantSnapshot.StateProgress"/>).
    /// </remarks>
    public double StateDuration { get; private set; }

    /// <summary>Enters a new state and sets the timers along with it.</summary>
    public void BeginState(CombatState state, double duration)
    {
        State = state;
        StateTimer = duration;
        StateDuration = duration;
    }

    /// <summary>How far the state has progressed (0-1).</summary>
    public double StateProgress =>
        StateDuration <= 0 ? 1 : Math.Clamp(1 - (StateTimer / StateDuration), 0, 1);

    /// <summary>Did the player say "pull out"? It may be buffered.</summary>
    public bool RetreatRequested { get; set; }

    /// <summary>
    /// The condition that allows surviving a heavy blow: did the player intervene? It counts if the
    /// command was given (even if the escape has not started yet) — pressing the key means
    /// "intervening in time" (see docs/GDD.md §7).
    /// </summary>
    public bool PlayerIntervened => RetreatRequested || State == CombatState.Retreating;

    /// <summary>The cause of death, if he died.</summary>
    /// <remarks>
    /// The event stream already carries the cause, but batch simulation does not collect events
    /// (<c>BattleSetup.CollectEvents</c>). Death by poison can only be counted here.
    /// </remarks>
    public DeathCause? DeathCause { get; set; }

    /// <summary>Is he still taking part in the fight?</summary>
    public bool IsActive => State is not (CombatState.Dead or CombatState.Escaped);

    /// <summary>
    /// Can an evasion/block die be rolled?
    /// </summary>
    /// <remarks>
    /// <b>A charge does not close defence.</b> A warrior who is running or gathering force evades at
    /// his normal rate (docs/GDD.md §4). One who turns his back and flees does not — defencelessness belongs to escape.
    /// </remarks>
    public bool CanDefend => State is not (
        CombatState.Retreating or CombatState.Stunned or CombatState.WeaponBound);

    /// <summary>Can a flee command be processed immediately in this state?</summary>
    /// <remarks>
    /// <b>A charge is interrupted by a flee command.</b> The charge is committed against his own
    /// decisions — the warrior cannot give it up and choose another move — but the player's "pull out"
    /// command is a separate axis. Were it not interruptible, a warrior running at the moment of the
    /// command would have to finish the charge, reach the enemy line and start fleeing from there;
    /// measured, this made <b>pressing at first contact more lethal than pressing late</b> and inverted
    /// docs/GDD.md §5's ladder. Even when interrupted, the charge's price is paid: the free hits taken
    /// along the way do not come back and the damage multiplier is not spent.
    /// </remarks>
    public bool IsCancellable =>
        State is CombatState.Idle
            or CombatState.AttackRecovery
            or CombatState.ThrowRecovery
            or CombatState.Charging
            or CombatState.ChargeWindup
            or CombatState.Blocking;

    // ---- Charge ----

    /// <summary>
    /// The first strike reached by a charge has not resolved yet: the damage multiplier is spent on it.
    /// </summary>
    public bool ChargeBonusPending { get; set; }

    /// <summary>The charge's speed at the moment it reaches the target — the arrival blow's force comes from it.</summary>
    public double ChargeImpactSpeed { get; set; }

    /// <summary>
    /// The target's counter-hit held: the charge arrives but <b>having lost its momentum</b>.
    /// </summary>
    /// <remarks>
    /// The arrival blow is still made, the damage multiplier is not earned (docs/GDD.md §4). A separate
    /// flag is needed because the counter-hit happens on the way, while the multiplier is earned on arrival.
    /// </remarks>
    public bool ChargeMomentumBroken { get; set; }

    /// <summary>The time since the current charge started.</summary>
    public double ChargeSeconds { get; set; }

    /// <summary>
    /// Was there an opening suitable for a charge at the last decision step?
    /// </summary>
    /// <remarks>
    /// The charge decision is made <b>once, the moment the opening appears</b>, not again every 0.2
    /// seconds for as long as the opening lasts (docs/GDD.md §4). This flag catches that "moment it
    /// appeared". Otherwise charge frequency would depend on how long the warrior loitered in the
    /// opening — that is, <b>inversely on his own speed</b>.
    /// </remarks>
    public bool SawChargeOpening { get; set; }

    /// <summary>
    /// Which enemies used their free hit during this charge.
    /// </summary>
    /// <remarks>
    /// An opportunity attack happens <b>once per enemy</b> per charge: otherwise the enemy run past
    /// would strike on every tick and the charge would be an execution, not a move.
    /// </remarks>
    public HashSet<WarriorId> ChargeOpportunists { get; } = [];

    /// <summary>The target the charge launched at.</summary>
    /// <remarks>
    /// The charge is committed <b>to this target</b> (docs/GDD.md §4). It is kept separate from general
    /// target selection: if the target dies, the warrior cannot aim at the nearest one while running,
    /// and the charge is wasted. Otherwise the miss branch would never fire and the charge would be a free move.
    /// </remarks>
    public WarriorId? ChargeTarget { get; set; }

    /// <summary>Resets the charge counters.</summary>
    public void ClearCharge()
    {
        ChargeBonusPending = false;
        ChargeImpactSpeed = 0;
        ChargeMomentumBroken = false;
        ChargeSeconds = 0;
        ChargeTarget = null;
        ChargeOpportunists.Clear();
    }

    /// <summary>Reads and spends a pending charge bonus.</summary>
    public bool ConsumeChargeBonus()
    {
        if (!ChargeBonusPending)
        {
            return false;
        }

        ChargeBonusPending = false;
        return true;
    }

    // ---- Poison ----

    /// <summary>
    /// The strength of the poison in his blood. 0 = clean.
    /// </summary>
    /// <remarks>
    /// The dose <b>accumulates</b>, the timer is refreshed: the second strike does not erase the first
    /// strike's poison, it adds to it (capped by <c>CombatTuning.PoisonMaxDose</c>). With a single
    /// "is he poisoned" flag, a poisoned weapon striking repeatedly would count for nothing.
    /// </remarks>
    public double PoisonDose { get; set; }

    /// <summary>The time left until the poison runs out.</summary>
    public double PoisonSecondsLeft { get; set; }

    /// <summary>The time left until the next poison damage.</summary>
    /// <remarks>
    /// Poison runs on its own clock, not on the simulation step — otherwise the poison's strength would
    /// change whenever the tick resolution changed.
    /// </remarks>
    public double PoisonTickTimer { get; set; }

    /// <summary>Who gave the poison — the warrior the damage is credited to.</summary>
    /// <remarks>
    /// The last striker is kept. Because poison is spread over time, the owner of the damage is lost at
    /// the moment of the strike; unrecorded, a warrior who kills with poison appears in no counter.
    /// </remarks>
    public Combatant? PoisonSource { get; set; }

    /// <summary>Is he poisoned?</summary>
    public bool IsPoisoned => PoisonDose > 0 && PoisonSecondsLeft > 0;

    /// <summary>Clears the poison — dose, timer and source together.</summary>
    public void ClearPoison()
    {
        PoisonDose = 0;
        PoisonSecondsLeft = 0;
        PoisonTickTimer = 0;
        PoisonSource = null;
    }

    /// <summary>The projectiles left in this fight. Persistent state is untouched, the counter is kept here.</summary>
    public int ThrowsLeft { get; set; } = warrior.UsableThrown?.Ammo ?? 0;

    /// <summary>Does he have a projectile to throw?</summary>
    public bool CanThrow => ThrowsLeft > 0 && Warrior.UsableThrown is not null;

    // ---- Performance counters (they feed the honour calculation and batch simulation) ----

    public int AttacksMade { get; set; }

    public int HitsLanded { get; set; }

    public int TimesHit { get; set; }

    public int DodgesPerformed { get; set; }

    /// <summary>How many blows he met with a block.</summary>
    public int BlocksPerformed { get; set; }

    /// <summary>
    /// The last decision step was a block stance — he cannot block again on the next step.
    /// </summary>
    /// <remarks>
    /// A block is a <b>stance</b>, not a shell. Were the die rolled again at every decision step, a
    /// warrior with high defence could block back to back and never strike: the fight would lock up, and
    /// the defence stat would be the best move without paying its price. The rule binds the stance to a
    /// <b>rhythm</b> — meet the blow, then answer it.
    /// </remarks>
    public bool JustBlocked { get; set; }

    public double DamageDealt { get; set; }

    public double DamageTaken { get; set; }

    public bool LostLimb { get; set; }

    /// <summary>How many times he was stunned in this fight.</summary>
    public int TimesStunned { get; set; }

    /// <summary>How many enemies he stunned — the counter where the blunt weapon's return is measured.</summary>
    public int StunsInflicted { get; set; }

    /// <summary>How many incoming weapons he caught — the counter for the jitte/sai's return.</summary>
    public int CatchesMade { get; set; }

    /// <summary>How many times his own weapon was caught and left him exposed.</summary>
    public int TimesCaught { get; set; }

    /// <summary>How many times his weapon fell out of his hand in this fight.</summary>
    /// <remarks>
    /// The <see cref="Disarmed"/> flag at the end of the fight is not enough: a warrior who drops his
    /// weapon and picks it up again looks armed there, yet he has paid the price. The rule can only be
    /// measured if the event is counted.
    /// </remarks>
    public int TimesDisarmed { get; set; }

    /// <summary>How many times he picked a weapon up from the ground.</summary>
    /// <remarks>
    /// This is the number that says how long the price of disarming really lasts: a fight spent unarmed,
    /// or a walk of a few seconds.
    /// </remarks>
    public int WeaponsPickedUp { get; set; }

    /// <summary>How many enemies' weapons he knocked out.</summary>
    /// <remarks>
    /// The catching implement's second return is read here — a knocked-out weapon shows up in neither
    /// damage nor limb loss.
    /// </remarks>
    public int DisarmsInflicted { get; set; }

    /// <summary>How many poisoned strikes he took.</summary>
    /// <remarks>
    /// Poison's return cannot be read from a single number: how often he was poisoned says how often the
    /// weapon <b>connects</b>, while the poison damage he took says how much work the dose did.
    /// </remarks>
    public int TimesPoisoned { get; set; }

    /// <summary>How many enemies he poisoned.</summary>
    public int PoisonsInflicted { get; set; }

    /// <summary>The total damage he took from poison.</summary>
    public double PoisonDamageTaken { get; set; }

    /// <summary>The total damage he dealt with his poison.</summary>
    public double PoisonDamageDealt { get; set; }

    /// <summary>How many times he launched a charge in this fight.</summary>
    public int ChargesStarted { get; set; }

    /// <summary>How many charges reached the target — how many of those started collected their return.</summary>
    public int ChargesConnected { get; set; }

    /// <summary>The number of free hits he took during his charges — the charge's paid price.</summary>
    public int ChargeOpportunitiesTaken { get; set; }

    /// <summary>The number of charges scattered during the windup.</summary>
    public int ChargesBroken { get; set; }

    /// <summary>The sum of the moments the charges launched at — for working out the average.</summary>
    public double ChargeStartSecondsSum { get; set; }

    /// <summary>The moment of the latest charge launched in this fight.</summary>
    /// <remarks>
    /// This is the number that says whether the charge is only an opening move: if the fight lasts
    /// 14 s and the latest launch is at 1 s, the distance threshold is never met in the rest of the
    /// fight.
    /// </remarks>
    public double LastChargeStartSeconds { get; set; }
}
