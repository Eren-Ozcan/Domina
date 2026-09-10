using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// The event stream that tells what is happening during a fight.
/// </summary>
/// <remarks>
/// <para>
/// <b>The crux of this design:</b> the combat resolver knows nothing about animation — it only
/// produces these events. The Godot layer takes the events and plays them back. If the separation
/// breaks and the resolver becomes coupled to animation, batch simulation without opening the engine
/// becomes impossible and balance work dies
/// (see CLAUDE.md → "Architecture rule").
/// </para>
/// </remarks>
public abstract record BattleEvent(double AtSeconds);

public sealed record BattleStarted(double AtSeconds) : BattleEvent(AtSeconds);

public sealed record AttackStarted(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

public sealed record AttackMissed(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

/// <summary>The evasion succeeded — no damage, but stamina is gone.</summary>
public sealed record AttackDodged(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

public sealed record AttackLanded(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double Damage,
    double DefenderHealthRemaining) : BattleEvent(AtSeconds);

/// <summary>
/// The blow met a block stance: most of the damage was erased and no limb came off — but the blow landed.
/// </summary>
/// <remarks>
/// It is a separate event from evasion and catching because the three tell different things on screen:
/// an evading warrior pulls away, a catching one binds, a blocking one <b>is shaken in place</b>.
/// <paramref name="Damage"/> is the damage left after the block — it is not zero, and the difference
/// exists so the presentation layer can show the block as "it worked, but not for free".
/// </remarks>
/// <summary>The warrior went into a block stance — he is not striking during it.</summary>
/// <remarks>
/// The stance itself has to be visible on screen: the player must be able to see why his warrior is not
/// striking. <see cref="AttackBlocked"/> is the moment the stance <b>worked</b>; this event is the
/// moment the stance was <b>taken</b>.
/// </remarks>
public sealed record BlockRaised(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

public sealed record AttackBlocked(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double Damage) : BattleEvent(AtSeconds);

/// <summary>
/// The defender caught the incoming weapon: no damage, and the attacker stays bound for
/// <paramref name="BindSeconds"/>.
/// </summary>
/// <remarks>
/// It is a separate event from evasion because what it says on screen is separate: in an evasion the
/// defender pulls away, in a catch the two warriors <b>lock together</b> for a moment. The presentation
/// layer should play this as a single stance involving both sides.
/// </remarks>
public sealed record AttackCaught(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double BindSeconds) : BattleEvent(AtSeconds);

/// <summary>
/// The weapon fell out of the hand: the warrior spends the rest of the fight with his <b>fists</b>.
/// </summary>
/// <remarks>
/// <para>
/// It can come from two places: the rebound of a strike landing on armour breaks the grip, or a caught
/// weapon is levered out of the palm by the hook. <paramref name="Disarmer"/> is the warrior who took
/// the weapon in the second case; for a warrior who struck plate and lost his own weapon it is
/// <c>null</c> — nobody disarmed him.
/// </para>
/// <para>
/// The loss belongs to <b>the fight</b> and is not permanent: the weapon stays on the ground and
/// returns to the warrior when the fight ends. Dropping was chosen over breaking so that equipment's
/// price does not open a separate inventory and repair ledger — the price is the rest of the fight.
/// </para>
/// </remarks>
public sealed record WeaponDropped(
    double AtSeconds,
    WarriorId Warrior,
    string Weapon,
    WarriorId? Disarmer) : BattleEvent(AtSeconds);

/// <summary>
/// An armour piece broke: that region is <b>bare</b> for the rest of the fight, and the piece is gone
/// permanently.
/// </summary>
/// <remarks>
/// This is where it differs from the weapon: a dropped weapon comes back at the end of the fight, a
/// broken piece does not. Armour is the game's consumable — the kit that absorbs the most runs out the
/// fastest. The core does not apply the permanent outcome; as with limb loss it produces the event and
/// the fight summary, and striking the armour off the books is the dojo layer's job.
/// </remarks>
public sealed record ArmorDestroyed(
    double AtSeconds,
    WarriorId Warrior,
    HitLocation Slot,
    string Piece) : BattleEvent(AtSeconds);

/// <summary>
/// The warrior picked a weapon up from the ground.
/// </summary>
/// <remarks>
/// Only an <b>empty-handed</b> warrior picks one up — a warrior with a weapon neither picks up nor
/// searches; he does not take a single step toward the blade on the ground. Without this limit everyone
/// would constantly collect better weapons and the fight would turn into a looting round.
/// </remarks>
public sealed record WeaponPickedUp(double AtSeconds, WarriorId Warrior, string Weapon)
    : BattleEvent(AtSeconds);

/// <summary>A heavy blow stunned the warrior: he freezes for <paramref name="Seconds"/>.</summary>
public sealed record WarriorStunned(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double Seconds) : BattleEvent(AtSeconds);

/// <summary>
/// The strike carried poison: a dose entered the defender's blood.
/// </summary>
/// <remarks>
/// The damage itself is a separate event (<see cref="PoisonTicked"/>). The two stand apart because what
/// they say on screen is different too: poisoning happens <b>once</b> and reveals the weapon, while the
/// damage repeats over time.
/// </remarks>
/// <param name="Dose">The total dose in the defender's blood after the strike.</param>
/// <param name="Seconds">The dose's refreshed lifetime.</param>
public sealed record WarriorPoisoned(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double Dose,
    double Seconds) : BattleEvent(AtSeconds);

/// <summary>
/// Poison worked once more. Nobody struck; the damage goes through neither armour nor defence.
/// </summary>
public sealed record PoisonTicked(
    double AtSeconds,
    WarriorId Warrior,
    double Damage,
    double HealthRemaining) : BattleEvent(AtSeconds);

/// <summary>
/// A heavy blow landed and, because the warrior was pulling out, he <b>lived but lost a limb</b>.
/// Had the player not intervened in time, this event would have been <see cref="WarriorDied"/>.
/// </summary>
public sealed record WarriorDismembered(double AtSeconds, WarriorId Warrior, BodyPart Part)
    : BattleEvent(AtSeconds);

/// <summary>
/// The key was pressed before contact and the command was refused (see docs/GDD.md §5).
/// </summary>
/// <remarks>
/// <para>
/// Escape only unlocks after the first hit: no pulling out before anyone is touched. A refused press
/// still produces an event, because the interface has something to say — if the rule is being seen for
/// the first time it should be taught, if it is being repeated insistently it should be answered.
/// </para>
/// <para>
/// <paramref name="ConsecutivePresses"/> is the number of consecutive refused presses in this fight; it
/// loses its meaning at the first accepted command. The core does not produce the text — it gives the
/// number, and the presentation layer decides what to write.
/// </para>
/// </remarks>
public sealed record RetreatRefused(double AtSeconds, int ConsecutivePresses) : BattleEvent(AtSeconds);

/// <summary>The player pressed the "pull out" key. The escape may not have started yet (see buffering).</summary>
public sealed record RetreatCommanded(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>
/// The command was buffered: the warrior was locked into an attack strike, and the escape will start
/// when his current move finishes (see docs/GDD.md §5).
/// </summary>
public sealed record RetreatBuffered(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>The escape started. From this moment the warrior cannot evade or block.</summary>
public sealed record RetreatStarted(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>
/// The projectile took off. The visualisation drives the flight from this event.
/// </summary>
/// <remarks>
/// The projectile is <b>not resolved instantly</b>: it stays in the air for the flight time and, on
/// arrival, results in <see cref="ProjectileHit"/> or <see cref="ProjectileMissed"/>. Resolved
/// instantly, the flight on screen and the moment of damage would not match.
/// </remarks>
/// <param name="FlightSeconds">The time left until arrival — the visualisation takes its speed from this.</param>
public sealed record ProjectileLaunched(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    string Weapon,
    ArenaPoint From,
    ArenaPoint To,
    double FlightSeconds) : BattleEvent(AtSeconds);

/// <summary>The projectile reached the target.</summary>
public sealed record ProjectileHit(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double Damage,
    double DefenderHealthRemaining) : BattleEvent(AtSeconds);

/// <summary>
/// The projectile was wasted — it missed, or the target left the field before it arrived.
/// </summary>
public sealed record ProjectileMissed(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

/// <summary>The warrior launched a charge: he is gathering force in place, not moving.</summary>
public sealed record ChargeStarted(double AtSeconds, WarriorId Warrior, WarriorId Target)
    : BattleEvent(AtSeconds);

/// <summary>The windup finished, the run started.</summary>
/// <remarks>
/// It stands apart for the visualisation: the windup and the run are two different moments of the same
/// move and cannot look the same on screen.
/// </remarks>
public sealed record ChargeLaunched(double AtSeconds, WarriorId Warrior, WarriorId Target)
    : BattleEvent(AtSeconds);

/// <summary>
/// The windup was scattered by a hit — the run never started, the bonus was not collected.
/// </summary>
public sealed record ChargeBroken(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>
/// The charge reached the target; the strike that follows carries a damage multiplier.
/// </summary>
public sealed record ChargeConnected(double AtSeconds, WarriorId Warrior, WarriorId Target)
    : BattleEvent(AtSeconds);

/// <summary>
/// The charge was wasted: the target died, fled, left the field, or the time ran out.
/// </summary>
/// <remarks>
/// This is the on-screen counterpart of the charge's commitment — the running warrior is left exposed
/// without reaching anyone.
/// </remarks>
public sealed record ChargeMissed(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>The free hit that comes from behind at fleeing prey.</summary>
public sealed record OpportunityAttack(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

/// <summary>
/// The accidental wound taken while leaving the arena — the only wound nobody struck.
/// </summary>
/// <remarks>
/// The visualisation should play this as a <b>stumble</b> rather than a strike; there is nobody
/// striking.
/// </remarks>
public sealed record EscapeMishap(
    double AtSeconds,
    WarriorId Warrior,
    double Damage,
    double HealthRemaining) : BattleEvent(AtSeconds);

/// <summary>The warrior left the arena alive.</summary>
public sealed record WarriorEscaped(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

public sealed record WarriorDied(double AtSeconds, WarriorId Warrior, DeathCause Cause)
    : BattleEvent(AtSeconds);

public sealed record BattleEnded(double AtSeconds, BattleOutcome Outcome) : BattleEvent(AtSeconds);

public enum DeathCause
{
    /// <summary>Health hit zero.</summary>
    Wounds,

    /// <summary>A heavy blow landed and nobody pulled him out — death by dismemberment.</summary>
    GrievousBlow,

    /// <summary>
    /// Poison finished him. There are seconds between the strike and the death; <b>nobody</b> landed the
    /// killing blow.
    /// <remarks>
    /// It stands as a separate cause because both the honour calculation and what the screen has to say
    /// are separate: a warrior felled by poison dies not on the battlefield but after it.
    /// </remarks>
    Poison,
}

public enum BattleOutcome
{
    /// <summary>The dojo side is still standing.</summary>
    PlayerVictory,

    /// <summary>
    /// The dojo side left the field <b>alive</b> — at least one warrior got away.
    /// </summary>
    /// <remarks>
    /// It is kept apart from a rout. For the game both mean "the fight was not won", but their prices are
    /// opposites: pulling out spends the expedition and the reward, a rout spends the warriors. Put in one
    /// box, no balance question can be answered — you get a wrong reading like "a player who flees is
    /// losing".
    /// </remarks>
    PlayerWithdrawal,

    /// <summary>Nobody was left on the dojo side and nobody escaped — the team was wiped out.</summary>
    PlayerWipe,

    /// <summary>
    /// The stall guard fired — <b>a bug, not a result</b>.
    /// </summary>
    /// <remarks>
    /// Neither side could close or finish within <see cref="CombatTuning.StallGuardSeconds"/>, which
    /// sits far above any real fight. A fight that ends this way is to be reproduced from its seed and
    /// looked at; it is not a draw, and no balance number may be read off it.
    /// </remarks>
    Stalled,
}
