using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// Olay akışının görsel tepkilere çevrilmesi.
/// </summary>
/// <remarks>
/// Buradaki asıl mesele <b>saldırının üç sonucunun birbirinden ayırt edilebilmesi</b>:
/// isabet, ıska ve kaçınma. Yalnızca isabet bağlandığında üçü de ekranda aynı görünür,
/// oyuncu dövüşü yalnızca can barından okumak zorunda kalır.
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
    /// Zehrin hasarı ekranda görünür, zehirlenme anı ayrıca görünmez.
    /// </summary>
    /// <remarks>
    /// Zehirlenme anının karşılığı vuruşun kendisidir ve orada zaten bir irkilme var;
    /// ikinci bir tepki aynı anı iki kez oynatırdı. Görülmesi gereken şey, <b>sonradan</b>
    /// gelen ve vuranı olmayan hasar.
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

    /// <summary>Kaçınma stamina harcar; harcamanın nereye gittiği ekranda görünmeli.</summary>
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
    /// Fırsat saldırısı kaçışın bedelidir. Ayrı bir işareti olmazsa "tuşa bastım,
    /// sonra canım gitti" olarak okunur.
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

        // Sıra önemli: önce bedava vuruş savrulur, sonra kaçan sarsılır.
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
    /// Olaylar tek seferliktir: aynı olay ikinci kez okunursa sarsıntı her karede
    /// yeniden başlar ve savaşçı bir daha durulmaz.
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
    /// Ölüm ve arenadan çıkış tek seferlik tepki değil kalıcı haldir; ikisi de
    /// duruma bakılarak sürülür (yığılma, ceset yeri, gizlenme).
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
