using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The surrender mechanic (GDD §5). All the rules here serve the same purpose: pressing the "pull out"
/// key must <b>not be a free rescue</b>. The command does not take effect at once, there is no defence
/// during the escape, and the opponent earns a free hit. Without those three, the right play would be
/// "pull every warrior at the first wound".
/// </summary>
public class RetreatTests
{
    private static readonly WarriorId _fighter = new(1);
    private static readonly WarriorId _enemy = new(101);

    private static BattleSetup Duel(double playerHealth = 400, double playerEvasion = 0) => new(
        [TestBuilders.Warrior(1, health: playerHealth, evasion: playerEvasion)],
        [TestBuilders.Warrior(101, health: 400)])
    {
        Tuning = TestBuilders.PointBlank,
    };

    /// <summary>Steps until a given state is entered.</summary>
    private static bool StepUntil(Battle battle, Func<Battle, bool> predicate, int maxSteps = 400)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            if (predicate(battle))
            {
                return true;
            }

            if (!battle.Step())
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Steps until the moment the key unlocks: escape is only possible after the first hit (GDD §5).
    /// </summary>
    /// <remarks>
    /// Most of the tests here measure what happens after the moment the command is given; waiting for
    /// the fight to start is not their subject but their precondition.
    /// </remarks>
    private static void OpenTheButton(Battle battle) =>
        Assert.True(StepUntil(battle, b => b.ContactMade), "The first hit did not land.");

    /// <summary>
    /// No pulling out before the fight starts: the key is closed until someone is touched (GDD §5).
    /// </summary>
    /// <remarks>
    /// The threshold is a <b>hit</b>, not a move. This removes entirely the branch of pre-contact escape
    /// that used to measure a 35% completely unharmed return — a new
    /// kural eklemeden.
    /// </remarks>
    [Fact]
    public void TheButtonIsShutUntilTheFirstBloodIsDrawn()
    {
        var battle = new Battle(Duel(), new SeededRandom(31));

        Assert.False(battle.ContactMade);
        Assert.False(battle.CommandRetreat());
        Assert.False(battle.SnapshotOf(_fighter).RetreatRequested);
        Assert.DoesNotContain(battle.Events, e => e is RetreatCommanded);

        OpenTheButton(battle);
        Assert.True(battle.CommandRetreat());
    }

    /// <summary>
    /// A refused press is not silently swallowed: it is counted and produces an event.
    /// </summary>
    /// <remarks>
    /// The number is for the presentation layer — to teach the rule to someone seeing it for the first
    /// time, and to answer someone who keeps pressing. The core does not produce the text.
    /// </remarks>
    [Fact]
    public void RefusedPressesAreCountedNotSwallowed()
    {
        var battle = new Battle(Duel(), new SeededRandom(32));

        battle.CommandRetreat();
        battle.CommandRetreat();
        battle.CommandRetreat();

        Assert.Equal(3, battle.RefusedRetreatPresses);
        Assert.Equal(
            [1, 2, 3],
            battle.Events.OfType<RetreatRefused>().Select(e => e.ConsecutivePresses));
    }

    [Fact]
    public void CommandIsAcceptedImmediatelyWhenTheWarriorIsIdle()
    {
        var battle = new Battle(Duel(), new SeededRandom(1));

        Assert.True(StepUntil(
            battle,
            b => b.ContactMade && b.SnapshotOf(_fighter).State == CombatState.Idle));

        Assert.True(battle.CommandRetreat());
        Assert.Equal(CombatState.Retreating, battle.SnapshotOf(_fighter).State);

        Assert.Contains(battle.Events, e => e is RetreatCommanded);
        Assert.Contains(battle.Events, e => e is RetreatStarted);
        Assert.DoesNotContain(battle.Events, e => e is RetreatBuffered);
    }

    [Fact]
    public void CommandIsBufferedWhileTheSwordIsInTheAir()
    {
        var battle = new Battle(Duel(), new SeededRandom(2));

        Assert.True(StepUntil(
            battle,
            b => b.ContactMade && b.SnapshotOf(_fighter).State == CombatState.AttackWindup));

        Assert.True(battle.CommandRetreat());

        // The command was accepted, but the escape does not start before the strike finishes.
        Assert.Contains(battle.Events, e => e is RetreatBuffered);
        Assert.DoesNotContain(battle.Events, e => e is RetreatStarted);
        Assert.Equal(CombatState.AttackWindup, battle.SnapshotOf(_fighter).State);
        Assert.True(battle.SnapshotOf(_fighter).RetreatRequested);
    }

    [Fact]
    public void ABufferedCommandRunsAsSoonAsTheAttackFinishes()
    {
        var battle = new Battle(Duel(), new SeededRandom(3));

        StepUntil(
            battle,
            b => b.ContactMade && b.SnapshotOf(_fighter).State == CombatState.AttackWindup);
        battle.CommandRetreat();

        Assert.True(StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.Retreating));
        Assert.Contains(battle.Events, e => e is RetreatStarted);

        // The buffered command lets the strike finish first: the escape starts after the attack.
        double buffered = battle.Events.OfType<RetreatBuffered>().First().AtSeconds;
        double started = battle.Events.OfType<RetreatStarted>().First().AtSeconds;
        Assert.True(started > buffered, "A buffered escape must not start immediately.");
    }

    [Fact]
    public void RetreatingCostsAFreeSwingToTheOpponent()
    {
        var battle = new Battle(Duel(), new SeededRandom(4));
        OpenTheButton(battle);
        battle.CommandRetreat();

        Assert.True(StepUntil(battle, b => b.Events.Any(e => e is OpportunityAttack)));

        OpportunityAttack free = battle.Events.OfType<OpportunityAttack>().First();
        Assert.Equal(_enemy, free.Attacker);
        Assert.Equal(_fighter, free.Defender);
    }

    [Fact]
    public void ARetreatingWarriorCannotDodge()
    {
        // Evasion 100 and the die at 0.40: normally he would evade (chance 0.45), while pulling out he cannot.
        var defending = new Battle(Duel(playerEvasion: 100), new FixedRandom(0.40));
        StepUntil(defending, b => b.Events.Any(e => e is AttackDodged));

        var fleeing = new Battle(Duel(playerEvasion: 100), new FixedRandom(0.40));
        OpenTheButton(fleeing);
        fleeing.CommandRetreat();
        StepUntil(fleeing, b => b.Events.Any(e => e is RetreatStarted));

        fleeing.Run();

        // The comparison looks at what happens AFTER the escape started; before that it is a normal fight.
        // Both are seen within the same tick: the standing warrior evades the move, and after he starts
        // fleeing he takes the free hit without evading.
        int left = fleeing.Events.ToList().FindIndex(e => e is RetreatStarted);
        List<BattleEvent> afterLeaving = fleeing.Events.Skip(left).ToList();

        Assert.Contains(defending.Events, e => e is AttackDodged);
        Assert.Contains(fleeing.Events, e => e is AttackDodged { Defender.Value: 1 });
        Assert.DoesNotContain(afterLeaving, e => e is AttackDodged { Defender.Value: 1 });
        Assert.Contains(afterLeaving, e => e is AttackLanded { Defender.Value: 1 });
    }

    [Fact]
    public void EscapingLeavesTheArenaAliveButLosesTheBattle()
    {
        var battle = new Battle(Duel(), new SeededRandom(5));
        OpenTheButton(battle);
        battle.CommandRetreat();

        BattleResult result = battle.Run();
        WarriorBattleSummary summary = result.SummaryFor(_fighter);

        Assert.True(summary.Escaped);
        Assert.False(summary.Died);
        Assert.True(summary.HealthRemaining > 0);

        // Surviving is not victory, but it is not a rout either: the fight was not won and the warrior is
        // alive. The two are separate outcomes.
        Assert.Equal(BattleOutcome.PlayerWithdrawal, result.Outcome);
        Assert.Contains(battle.Events, e => e is WarriorEscaped);
    }

    [Fact]
    public void RepeatingTheCommandChangesNothing()
    {
        var battle = new Battle(Duel(), new SeededRandom(6));
        OpenTheButton(battle);

        Assert.True(battle.CommandRetreat());
        Assert.False(battle.CommandRetreat());
    }

    [Fact]
    public void AnEmptyArenaRejectsFurtherCommands()
    {
        var battle = new Battle(Duel(), new SeededRandom(7));
        OpenTheButton(battle);
        battle.CommandRetreat();
        battle.Run();

        Assert.False(battle.CommandRetreat());
    }

    [Fact]
    public void UnknownWarriorsAreRejectedRatherThanThrowing() =>
        Assert.Throws<ArgumentException>(() =>
            new Battle(Duel(), new SeededRandom(8)).SnapshotOf(new WarriorId(999)));

    /// <summary>
    /// GDD §5's main rule: the command covers <b>the whole party</b>. Were it per-warrior, the right play
    /// would be "pull the wounded one, continue with the rest" — a losslessly repeatable optimisation.
    /// This test keeps that door closed.
    /// </summary>
    [Fact]
    public void TheWholePartyRetreatsTogether()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400),
                TestBuilders.Warrior(2, health: 400),
                TestBuilders.Warrior(3, health: 400),
            ],
            [TestBuilders.Warrior(101, health: 400)])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new SeededRandom(9));
        OpenTheButton(battle);
        Assert.True(battle.CommandRetreat());

        for (int id = 1; id <= 3; id++)
        {
            Assert.True(battle.SnapshotOf(new WarriorId(id)).RetreatRequested);
        }

        BattleResult result = battle.Run();

        // Nobody is left behind; nobody remains on the field for the dojo, but nobody died either.
        Assert.Equal(3, result.Summaries.Count(s => s.Team == Battle.PlayerTeam && s.Escaped));
        Assert.Equal(BattleOutcome.PlayerWithdrawal, result.Outcome);
    }

    /// <summary>
    /// A single key can take effect at three different moments: the command of a warrior with his sword
    /// in the air is buffered, an idle one flees at once. The team command does not remove that
    /// subtlety.
    /// </summary>
    [Fact]
    public void OnePressResolvesPerWarriorTiming()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400, aggression: 100),
                TestBuilders.Warrior(2, health: 400, aggression: 0),
            ],
            [TestBuilders.Warrior(101, health: 400)])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new SeededRandom(21));

        // Advance until one is locked into a strike; the other will still be waiting.
        StepUntil(
            battle,
            b => b.ContactMade
                 && b.SnapshotOf(new WarriorId(1)).State == CombatState.AttackWindup
                 && b.SnapshotOf(new WarriorId(2)).CanCancel);
        Assert.True(battle.SnapshotOf(new WarriorId(2)).CanCancel);

        battle.CommandRetreat();

        Assert.Contains(battle.Events, e => e is RetreatBuffered { Warrior.Value: 1 });
        Assert.Contains(battle.Events, e => e is RetreatStarted { Warrior.Value: 2 });
        Assert.Equal(CombatState.AttackWindup, battle.SnapshotOf(new WarriorId(1)).State);
        Assert.Equal(CombatState.Retreating, battle.SnapshotOf(new WarriorId(2)).State);
    }

    /// <summary>
    /// Pressing the key saves <b>everyone</b> with maiming instead of death — not a single warrior. That
    /// is why everyone must pay the price too (see <see cref="HonorTests"/> — a fleeing warrior earns no
    /// honour).
    /// </summary>
    /// <remarks>
    /// The protection is <b>not immortality</b>: an enemy who catches the fleeing warrior can still fell
    /// him before he leaves the field. That is why the test does not say "nobody died" but "every lethal
    /// blow was turned into limb loss" — that is the real rule.
    /// </remarks>
    [Fact]
    public void InterventionProtectsEveryWarriorNotJustOne()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 300, aggression: 0),
                TestBuilders.Warrior(2, health: 300, aggression: 0),
            ],
            [TestBuilders.Warrior(101, aggression: 100, weapon: TestBuilders.Executioner())])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        OpenTheButton(battle);
        battle.CommandRetreat();
        BattleResult result = battle.Run();

        // Every warrior who was hit must have lost a limb; nobody should have "just died".
        foreach (WarriorBattleSummary s in result.Summaries.Where(s => s.Team == Battle.PlayerTeam))
        {
            if (s.TimesHit > 0)
            {
                Assert.True(s.LostLimb, s.Name);
            }
        }

        Assert.DoesNotContain(
            battle.Events.OfType<WarriorDied>(),
            e => e.Cause == DeathCause.GrievousBlow);
    }

    /// <summary>
    /// The policy looks at a single warrior's state but follows the same rule as the key: the command it
    /// triggers pulls the whole party. Otherwise batch simulation would measure a way of playing that is
    /// not possible in the game and the balance numbers would come out wrong.
    /// </summary>
    [Fact]
    public void ThePolicyPullsTheWholePartyAndNeverTheEnemy()
    {
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400),
                TestBuilders.Warrior(2, health: 400),
            ],
            [TestBuilders.Warrior(101, health: 400)])
        {
            // Not so that only the first warrior's health falls below the threshold —
            // the threshold is 1.0, so it triggers on the first step.
            RetreatPolicy = new RetreatBelowHealth(1.0),
        };

        var battle = new Battle(setup, new SeededRandom(10));

        // The policy goes through the same door as the key: it stays silent until the first hit. The
        // silence is tested not at the end of the tick contact lands on but on every tick BEFORE it:
        // within the same tick the strike resolves first and the next warrior's policy is read after —
        // so on the contact tick the command may already have been accepted.
        while (!battle.ContactMade)
        {
            Assert.False(battle.SnapshotOf(new WarriorId(1)).RetreatRequested);
            Assert.True(battle.Step(), "The fight ended without any contact.");
        }

        Assert.True(StepUntil(battle, b => b.SnapshotOf(new WarriorId(1)).RetreatRequested));

        Assert.True(battle.SnapshotOf(new WarriorId(1)).RetreatRequested);
        Assert.True(battle.SnapshotOf(new WarriorId(2)).RetreatRequested);
        Assert.False(battle.SnapshotOf(_enemy).RetreatRequested);
    }

    /// <summary>
    /// The visualisation's contract: <see cref="CombatantSnapshot.CanCancel"/> says whether the "pull
    /// out" key will take effect at once or be buffered. The player has to see this before pressing, or
    /// the buffering comes as a surprise.
    /// </summary>
    [Fact]
    public void TheSnapshotTellsWhetherACommandWouldBeBuffered()
    {
        var battle = new Battle(Duel(), new SeededRandom(12));

        Assert.True(battle.SnapshotOf(_fighter).CanCancel);

        StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.AttackWindup);
        Assert.False(battle.SnapshotOf(_fighter).CanCancel);

        StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.AttackRecovery);
        Assert.True(battle.SnapshotOf(_fighter).CanCancel);
    }

    /// <summary>
    /// The animation is driven by where in the state we are; the ratio must advance from 0 to 1 through
    /// the state, or the strike animation does not stay in sync with the resolution.
    /// </summary>
    [Fact]
    public void StateProgressAdvancesFromStartToEnd()
    {
        var battle = new Battle(Duel(), new SeededRandom(13));
        StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.AttackWindup);

        double first = battle.SnapshotOf(_fighter).StateProgress;
        double last = first;

        while (battle.SnapshotOf(_fighter).State == CombatState.AttackWindup)
        {
            double now = battle.SnapshotOf(_fighter).StateProgress;
            Assert.InRange(now, 0, 1);
            Assert.True(now >= last, "Durum ilerlemesi geriye gitmemeli.");
            last = now;

            if (!battle.Step())
            {
                break;
            }
        }

        Assert.True(last > first, "The progress must rise through the windup.");
    }

    [Fact]
    public void NeverRetreatLeavesEveryoneFighting()
    {
        var setup = Duel() with { RetreatPolicy = NeverRetreat.Instance };
        BattleResult result = new Battle(setup, new SeededRandom(11)).Run();

        Assert.DoesNotContain(result.Summaries, s => s.Escaped);
    }
}
