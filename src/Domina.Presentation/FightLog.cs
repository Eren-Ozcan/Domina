using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>How loudly a line of the fight's report reads.</summary>
public enum FightVoice
{
    /// <summary>The field itself: a weapon dropped, a man picking one up, an arrow gone wide.</summary>
    Quiet,

    /// <summary>Something that changed the fight: a kill, a man yielding, the fight ending.</summary>
    Plain,

    /// <summary>Something that happened to one of ours: a wound, a limb, a death.</summary>
    Ours,
}

/// <summary>One line of the stream the fight emits.</summary>
/// <param name="Text">The sentence, as it is printed.</param>
/// <param name="Voice">How it is printed.</param>
public readonly record struct FightLine(string Text, FightVoice Voice);

/// <summary>
/// Turns the fight's event stream into the sentences the screen prints under "what is happening".
/// </summary>
/// <remarks>
/// <para>
/// The simulation emits events, not prose — <see cref="BattleLog"/> writes the same stream as JSON for
/// a bug report. What the player reads beside the fight is this: a handful of the events, said in
/// English, with the names of the men in them.
/// </para>
/// <para>
/// Not every event is told. A fight of two minutes emits hundreds, and a report that prints all of them
/// is a report nobody reads — the ones kept are the ones that change what the player is looking at:
/// a landing blow, a limb, a death, a man yielding or pulling out, a weapon lost or found.
/// </para>
/// <para>
/// It takes the names rather than the roster so that the enemy side, which has no roster, can be told
/// by whatever the fight calls it (CLAUDE.md → architecture rule: no engine, no dojo).
/// </para>
/// </remarks>
public static class FightLog
{
    /// <summary>Says what one event is, or nothing at all when it is not worth a line.</summary>
    /// <param name="happening">The event.</param>
    /// <param name="names">What each man is called.</param>
    /// <param name="ours">Which men are the dojo's.</param>
    public static FightLine? Tell(
        BattleEvent happening,
        IReadOnlyDictionary<WarriorId, string> names,
        IReadOnlySet<WarriorId> ours)
    {
        ArgumentNullException.ThrowIfNull(happening);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(ours);

        string Name(WarriorId id) => names.TryGetValue(id, out string? found) ? found : "a man";
        FightVoice Voice(WarriorId hurt) => ours.Contains(hurt) ? FightVoice.Ours : FightVoice.Plain;

        return happening switch
        {
            WarriorDied died => new FightLine(
                died.Cause == DeathCause.GrievousBlow
                    ? $"{Name(died.Warrior)} is cut down where he stands."
                    : $"{Name(died.Warrior)} is killed.",
                Voice(died.Warrior)),

            WarriorDismembered lost => new FightLine(
                $"{Name(lost.Warrior)} loses {Part(lost.Part)}.",
                Voice(lost.Warrior)),

            WarriorYielded yielded => new FightLine(
                $"{Name(yielded.Warrior)} throws his weapon down and yields.",
                Voice(yielded.Warrior)),

            RetreatStarted leaving => new FightLine(
                $"{Name(leaving.Warrior)} is coming off the field.",
                Voice(leaving.Warrior)),

            RetreatBuffered held => new FightLine(
                $"{Name(held.Warrior)} is mid-strike; he leaves when it finishes.",
                Voice(held.Warrior)),

            WarriorStunned stunned => new FightLine(
                $"{Name(stunned.Attacker)} knocks {Name(stunned.Defender)} off his feet.",
                Voice(stunned.Defender)),

            WeaponDropped dropped => new FightLine(
                dropped.Disarmer is WarriorId taker
                    ? $"{Name(taker)} strikes the {dropped.Weapon} out of {Name(dropped.Warrior)}'s hands."
                    : $"{Name(dropped.Warrior)} loses his {dropped.Weapon}.",
                Voice(dropped.Warrior)),

            WeaponPickedUp found => new FightLine(
                $"{Name(found.Warrior)} takes up a {found.Weapon} from the ground.",
                FightVoice.Quiet),

            ArmorDestroyed broken => new FightLine(
                $"{Name(broken.Warrior)}'s {broken.Piece} gives way.",
                Voice(broken.Warrior)),

            AttackLanded landed when landed.DefenderHealthRemaining <= 0 => null,

            AttackLanded landed when ours.Contains(landed.Defender) => new FightLine(
                $"{Name(landed.Defender)} takes a blow and does not step back.",
                FightVoice.Ours),

            ProjectileHit hit when ours.Contains(hit.Defender) => new FightLine(
                $"{Name(hit.Defender)} is hit at distance.",
                FightVoice.Ours),

            BattleEnded ended => new FightLine(Ending(ended.Outcome), FightVoice.Plain),

            _ => null,
        };
    }

    /// <summary>The end of it, in the words the aftermath will use again.</summary>
    private static string Ending(BattleOutcome outcome) => outcome switch
    {
        BattleOutcome.PlayerVictory => "The field is clear. It is yours.",
        BattleOutcome.PlayerWipe => "Nobody of yours is left standing.",
        BattleOutcome.PlayerWithdrawal => "Your men are off the field.",
        BattleOutcome.Stalled => "The fight could not resolve itself; it is in the journal.",
        _ => "It is over.",
    };

    /// <summary>A limb, named the way the aftermath names it.</summary>
    private static string Part(BodyPart part) => part switch
    {
        BodyPart.SwordArm => "his sword arm",
        BodyPart.OffArm => "his off arm",
        BodyPart.RightLeg => "his right leg",
        BodyPart.LeftLeg => "his left leg",
        BodyPart.Eye => "an eye",
        _ => "a limb",
    };
}
