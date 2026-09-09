using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// Translating the event stream into visual reactions.
/// </summary>
/// <remarks>
/// The real issue here is <b>being able to tell an attack's three outcomes apart</b>: a hit, a miss and
/// an evasion. When only hits are wired up, all three look the same on screen and the player has to
/// read the fight from the health bar alone.
/// </remarks>
public class ReactionReaderTests
{
    private static readonly WarriorId _attacker = new(1);
    private static readonly WarriorId _defender = new(101);

    private static List<BattleEvent> Stream(params BattleEvent[] events) => [.. events];

    [Fact]
    public void ALandedAttackShakesTheDefender()
    {
        var reader = new ReactionReader();
        var events = Stream(new AttackLanded(1, _attacker, _defender, 12, 88));

        RigReaction reaction = Assert.Single(reader.Drain(events));

        Assert.Equal(_defender, reaction.Warrior);
        Assert.Equal(RigReactionKind.Flinch, reaction.Kind);
    }

    [Fact]
    public void AMissedAttackSwingsPastTheTarget()
    {
        var reader = new ReactionReader();
        var events = Stream(new AttackMissed(1, _attacker, _defender));

        RigReaction reaction = Assert.Single(reader.Drain(events));

        Assert.Equal(_attacker, reaction.Warrior);
        Assert.Equal(RigReactionKind.Overswing, reaction.Kind);
    }

    /// <summary>
    /// Poison's damage is visible on screen, the moment of poisoning is not shown separately.
    /// </summary>
    /// <remarks>
    /// The moment of poisoning's counterpart is the strike itself, and there is already a flinch there; a
    /// second reaction would play the same moment twice. What has to be seen is the damage that comes
    /// <b>afterwards</b> with nobody striking.
    /// </remarks>
    [Fact]
    public void PoisonShowsWhenItWorksNotWhenItLands()
    {
        var reader = new ReactionReader();
        var events = Stream(
            new WarriorPoisoned(1, _attacker, _defender, 1.0, 6.0),
            new PoisonTicked(2, _defender, 2.5, 80));

        RigReaction reaction = Assert.Single(reader.Drain(events));

        Assert.Equal(_defender, reaction.Warrior);
        Assert.Equal(RigReactionKind.PoisonThroe, reaction.Kind);
    }

    /// <summary>Only the warrior whose weapon is gone produces the broken-weapon reaction.</summary>
    /// <remarks>
    /// The breaker's counterpart (if there is one) is already on screen: the catch move played a moment
    /// earlier. A second reaction would tell the same moment twice.
    /// </remarks>
    [Fact]
    public void ABrokenWeaponMovesOnlyItsOwner()
    {
        var reader = new ReactionReader();
        var events = Stream(new WeaponDropped(1, _attacker, "Katana", _defender));

        RigReaction reaction = Assert.Single(reader.Drain(events));

        Assert.Equal(_attacker, reaction.Warrior);
        Assert.Equal(RigReactionKind.WeaponLost, reaction.Kind);
    }

    /// <summary>Evasion costs stamina; where that cost goes must be visible on screen.</summary>
    [Fact]
    public void ADodgeMovesBothSides()
    {
        var reader = new ReactionReader();
        var events = Stream(new AttackDodged(1, _attacker, _defender));

        IReadOnlyList<RigReaction> reactions = reader.Drain(events);

        Assert.Equal(2, reactions.Count);
        Assert.Contains(reactions, r => r.Warrior == _attacker && r.Kind == RigReactionKind.Overswing);
        Assert.Contains(reactions, r => r.Warrior == _defender && r.Kind == RigReactionKind.Dodge);
    }

    /// <summary>
    /// An opportunity attack is the price of fleeing. Without a mark of its own it reads as "I pressed
    /// the key and then my health went".
    /// </summary>
    [Fact]
    public void TheOpportunityAttackIsShownOnTheHunter()
    {
        var reader = new ReactionReader();
        var events = Stream(
            new OpportunityAttack(1, _attacker, _defender),
            new AttackLanded(1, _attacker, _defender, 20, 80));

        IReadOnlyList<RigReaction> reactions = reader.Drain(events);

        Assert.Equal(2, reactions.Count);

        // The order matters: first the free hit is swung, then the fleer is shaken.
        Assert.Equal(RigReactionKind.OpportunitySwing, reactions[0].Kind);
        Assert.Equal(_attacker, reactions[0].Warrior);
        Assert.Equal(RigReactionKind.Flinch, reactions[1].Kind);
        Assert.Equal(_defender, reactions[1].Warrior);
    }

    [Fact]
    public void DismembermentCarriesTheLostPart()
    {
        var reader = new ReactionReader();
        var events = Stream(new WarriorDismembered(1, _defender, BodyPart.RightLeg));

        RigReaction reaction = Assert.Single(reader.Drain(events));

        Assert.Equal(RigReactionKind.Dismember, reaction.Kind);
        Assert.Equal(BodyPart.RightLeg, reaction.Part);
    }

    /// <summary>
    /// Events are one-off: if the same event is read a second time the shake restarts every frame and
    /// the warrior never settles.
    /// </summary>
    [Fact]
    public void EachEventIsReadExactlyOnce()
    {
        var reader = new ReactionReader();
        var events = Stream(new AttackLanded(1, _attacker, _defender, 12, 88));

        Assert.Single(reader.Drain(events));
        Assert.Empty(reader.Drain(events));
        Assert.Equal(1, reader.Consumed);

        events.Add(new AttackMissed(2, _attacker, _defender));

        Assert.Single(reader.Drain(events));
        Assert.Equal(2, reader.Consumed);
    }

    /// <summary>
    /// Death and leaving the arena are not one-off reactions but persistent states; both are driven by
    /// looking at the state (the collapse, the body's place, hiding).
    /// </summary>
    [Fact]
    public void EventsWithoutAOneShotReactionAreSkipped()
    {
        var reader = new ReactionReader();
        var events = Stream(
            new BattleStarted(0),
            new AttackStarted(1, _attacker, _defender),
            new RetreatCommanded(2, _attacker),
            new RetreatBuffered(2, _attacker),
            new RetreatStarted(3, _attacker),
            new WarriorEscaped(4, _attacker),
            new WarriorDied(5, _defender, DeathCause.Wounds),
            new BattleEnded(5, BattleOutcome.PlayerVictory));

        Assert.Empty(reader.Drain(events));
        Assert.Equal(events.Count, reader.Consumed);
    }
}
