using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>A warrior's one-off visual reaction.</summary>
public enum RigReactionKind
{
    /// <summary>He took a hit.</summary>
    Flinch,

    /// <summary>He evaded — the price is stamina, so it must be visible on screen too.</summary>
    Dodge,

    /// <summary>He swung into empty air; the sword passed without finding the target.</summary>
    Overswing,

    /// <summary>He landed a free hit behind a fleeing warrior.</summary>
    OpportunitySwing,

    /// <summary>He caught the incoming weapon — the jitte/sai's one-off move.</summary>
    /// <remarks>
    /// It is kept apart from <see cref="Dodge"/>: in an evasion the defender pulls sideways, in a catch
    /// he goes <b>forward</b>. Tied to the same reaction, what the jitte does could not be told apart
    /// from an evasion on screen.
    /// </remarks>
    Catch,

    /// <summary>He lost a limb — permanent.</summary>
    Dismember,

    /// <summary>He threw a projectile.</summary>
    Throw,

    /// <summary>
    /// He stumbled while fleeing — the wound nobody struck.
    /// </summary>
    /// <remarks>
    /// It is kept apart from <see cref="Flinch"/>: there is nobody striking, so there is no blow
    /// direction on screen either. A stumble has to read on its own.
    /// </remarks>
    Stumble,

    /// <summary>
    /// Poison worked — the second wound nobody struck.
    /// </summary>
    /// <remarks>
    /// It is kept apart from <see cref="Flinch"/>: there is neither a blow nor a blow direction. It
    /// borrows <see cref="Stumble"/>'s curves but stays its own kind — a stumble happens while fleeing,
    /// poison in the middle of the fight.
    /// </remarks>
    PoisonThroe,

    /// <summary>
    /// His weapon fell out of his hand — the warrior looks for a moment at his empty hand.
    /// </summary>
    /// <remarks>
    /// It stands as its own kind because what it says on screen is not damage but <b>loss</b>: the weapon
    /// falls to the ground and the warrior is left with his fists until he walks to it. The continuous
    /// state is read from the snapshot (<c>CombatantSnapshot.Disarmed</c>); the reaction here is only for
    /// the moment it drops.
    /// </remarks>
    WeaponLost,

    /// <summary>
    /// An armour piece broke — a plate is gone from the warrior.
    /// </summary>
    /// <remarks>
    /// It stands apart from a weapon loss because its permanence is different: a dropped weapon can be
    /// picked up, a broken piece is gone after the fight too. The continuous state is in the snapshot
    /// (<c>CombatantSnapshot.DestroyedArmor</c>); the reaction here is for the moment it breaks.
    /// </remarks>
    ArmorShattered,

    /// <summary>
    /// He met the blow with his stance: shaken in place, his weapon still in front of him.
    /// </summary>
    /// <remarks>
    /// Apart from <see cref="Flinch"/>: a blocking warrior is not thrown back, he <b>resists</b>. Apart
    /// from <see cref="Dodge"/> too: an evading warrior pulls sideways, a blocking one does not move.
    /// from <see cref="Dodge"/> too: an evading warrior pulls sideways, a blocking one does not
    /// move. Tied to the same reaction, what the Defence stat does would not be visible on screen.
    /// </remarks>
    Block,
}

/// <param name="Part">Filled only for <see cref="RigReactionKind.Dismember"/>.</param>
public readonly record struct RigReaction(WarriorId Warrior, RigReactionKind Kind, BodyPart? Part = null);

/// <summary>
/// Translates the event stream into visual reactions.
/// </summary>
/// <remarks>
/// <para>
/// The visuals are driven by two channels: the <b>continuous</b> state from the snapshots (pose,
/// position, health), the <b>instant</b> reactions from here. The separation must hold — events are
/// one-off and cannot be replayed; that is why the read position is tracked with a counter and the list
/// only grows.
/// </para>
/// <para>
/// <b>Missed and evaded strikes have counterparts too.</b> When only hits were wired up, the three
/// attack outcomes (hit, miss, evasion) looked identical on screen: the sword came down the same way,
/// and either someone was shaken or nothing happened. Evasion is a core mechanic that spends
/// stamina; without a counterpart on screen the player cannot see where the stamina went.
/// </para>
/// <para>
/// Events with no counterpart are silently skipped — the core does not have to wait for the
/// visualisation to add a new event.
/// </para>
/// </remarks>
public sealed class ReactionReader
{
    private readonly List<RigReaction> _buffer = [];

    /// <summary>The number of events read so far.</summary>
    public int Consumed { get; private set; }

    /// <summary>
    /// Returns the reactions for the events produced since the last call.
    /// </summary>
    /// <remarks>
    /// The returned list is reused on the next call; the caller must not hold on to it.
    /// </remarks>
    public IReadOnlyList<RigReaction> Drain(IReadOnlyList<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _buffer.Clear();

        for (; Consumed < events.Count; Consumed++)
        {
            Translate(events[Consumed], _buffer);
        }

        return _buffer;
    }

    private static void Translate(BattleEvent battleEvent, List<RigReaction> into)
    {
        switch (battleEvent)
        {
            case AttackLanded landed:
                into.Add(new RigReaction(landed.Defender, RigReactionKind.Flinch));
                break;

            case AttackMissed missed:
                into.Add(new RigReaction(missed.Attacker, RigReactionKind.Overswing));
                break;

            case AttackCaught caught:
                // Only the catcher produces a reaction. The attacker's counterpart is not one-off: a
                // warrior whose weapon is caught enters CombatState.WeaponBound and his pose is driven
                // from there — adding a second reaction here would do the same job again, and only for a
                // moment at that.
                into.Add(new RigReaction(caught.Defender, RigReactionKind.Catch));
                break;

            case AttackDodged dodged:
                // Both at once: the attacker swings into empty air, the defender pulls aside.
                into.Add(new RigReaction(dodged.Attacker, RigReactionKind.Overswing));
                into.Add(new RigReaction(dodged.Defender, RigReactionKind.Dodge));
                break;

            case AttackBlocked blocked:
                // The stance held. There is no separate reaction for the attacker: the blow was not
                // wasted, it was met — a swing animation would say the wrong thing.
                into.Add(new RigReaction(blocked.Defender, RigReactionKind.Block));
                break;

            case OpportunityAttack opportunity:
                // The price of fleeing. It does not produce a strike outcome on its own — the hit/miss
                // event that follows completes it; the only job here is to show who the free hit came
                // from.
                into.Add(new RigReaction(opportunity.Attacker, RigReactionKind.OpportunitySwing));
                break;

            case WarriorDismembered lost:
                into.Add(new RigReaction(lost.Warrior, RigReactionKind.Dismember, lost.Part));
                break;

            case WeaponDropped dropped:
                // Only the one who lost the weapon produces a reaction. The disarmer's counterpart (if
                // there is one) is already on screen: the catch move played a moment earlier.
                into.Add(new RigReaction(dropped.Warrior, RigReactionKind.WeaponLost));
                break;

            case ArmorDestroyed destroyed:
                into.Add(new RigReaction(destroyed.Warrior, RigReactionKind.ArmorShattered));
                break;

            case WeaponPickedUp picked:
                // The moment of bending down to pick up. Not a separate reaction but the reverse of a
                // stumble — in the procedural pose both bend the body (see docs/ROADMAP.md 2.2).
                into.Add(new RigReaction(picked.Warrior, RigReactionKind.WeaponLost));
                break;

            case PoisonTicked poison:
                // The dose worked once more. The moment of poisoning itself (WarriorPoisoned) produces no
                // reaction: its counterpart on screen is the strike itself, and there is already a flinch
                // there.
                into.Add(new RigReaction(poison.Warrior, RigReactionKind.PoisonThroe));
                break;

            case ProjectileLaunched launched:
                // The projectile's flight is not a reaction but a separate scene object (see
                // ProjectileView). The only job here is to show the throwing warrior's move.
                into.Add(new RigReaction(launched.Attacker, RigReactionKind.Throw));
                break;

            case ProjectileHit hit:
                into.Add(new RigReaction(hit.Defender, RigReactionKind.Flinch));
                break;

            case EscapeMishap mishap:
                into.Add(new RigReaction(mishap.Warrior, RigReactionKind.Stumble));
                break;

            // Death and leaving the arena have NO counterpart here, because neither is one-off but a
            // persistent state: the state arrives as <see cref="CombatState.Dead"/> and the collapse is
            // driven from there, while where the body ends up is decided by
            // <see cref="ArenaChoreography"/>. Adding a reaction here would do the same job a second
            // time, and triggered only once at that.
            default:
                break;
        }
    }
}
