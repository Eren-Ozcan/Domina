using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// End-to-end combat: the flow staying consistent from setup to finish. Phase 1's acceptance criterion
/// is "a 3v3 fight is simulated from start to finish" — the tests here are that sentence's counterpart.
/// The event stream and the result summary must match, because in phase 2 what appears on screen is the
/// event stream while what goes into the records is the summary; if the two drift apart, a warrior who
/// lost a limb looks intact on screen.
/// </summary>
public class BattleFlowTests
{
    private static BattleSetup ThreeVsThree() => new(
        [
            TestBuilders.Warrior(1, evasion: 30, defense: 20, armor: Armor.Light()),
            TestBuilders.Warrior(2, evasion: 20, defense: 30, weapon: Weapon.Nodachi(), armor: Armor.Medium()),
            TestBuilders.Warrior(3, evasion: 40, defense: 10, weapon: Weapon.Yari()),
        ],
        [
            TestBuilders.Warrior(101, "Oni", health: 140, aggression: 55, defense: 25, weapon: Weapon.Tetsubo()),
            TestBuilders.Warrior(102, "Kappa", health: 70, aggression: 65, evasion: 35),
            TestBuilders.Warrior(103, "Tengu", health: 80, aggression: 70, evasion: 50),
        ]);

    [Fact]
    public void AThreeOnThreeBattleRunsToACleanFinish()
    {
        var battle = new Battle(ThreeVsThree(), new SeededRandom(20260806));
        BattleResult result = battle.Run();

        Assert.True(battle.IsFinished);
        Assert.Same(result, battle.Result);
        Assert.Equal(6, result.Summaries.Count);
        Assert.True(result.ElapsedSeconds > 0);
        Assert.True(result.ElapsedSeconds <= CombatTuning.Default.StallGuardSeconds);

        // A finished fight does not advance.
        Assert.False(battle.Step());
    }

    [Fact]
    public void TheEventStreamOpensAndClosesExactlyOnce()
    {
        var battle = new Battle(ThreeVsThree(), new SeededRandom(11));
        BattleResult result = battle.Run();

        Assert.IsType<BattleStarted>(battle.Events[0]);
        Assert.IsType<BattleEnded>(battle.Events[^1]);
        Assert.Single(battle.Events.OfType<BattleStarted>());

        BattleEnded ended = Assert.Single(battle.Events.OfType<BattleEnded>());
        Assert.Equal(result.Outcome, ended.Outcome);
        Assert.Equal(result.ElapsedSeconds, ended.AtSeconds, precision: 9);
    }

    [Fact]
    public void EventsNeverGoBackInTime()
    {
        var battle = new Battle(ThreeVsThree(), new SeededRandom(12));
        battle.Run();

        double previous = -1;
        foreach (BattleEvent e in battle.Events)
        {
            Assert.True(e.AtSeconds >= previous, "The event stream must not go backwards in time.");
            previous = e.AtSeconds;
        }
    }

    [Theory]
    [InlineData(1ul)]
    [InlineData(77ul)]
    [InlineData(20260806ul)]
    [InlineData(4242424242ul)]
    public void TheEventStreamAgreesWithTheResultSummary(ulong seed)
    {
        var battle = new Battle(ThreeVsThree(), new SeededRandom(seed));
        BattleResult result = battle.Run();

        foreach (WarriorBattleSummary summary in result.Summaries)
        {
            // A blocked blow is a landed blow too: it flows as a separate event (it has to look separate
            // on screen) but counts as a hit in the counters. If the two are not added together, the
            // birbirini tutmaz.
            int landed = battle.Events.OfType<AttackLanded>().Count(e => e.Attacker == summary.Id)
                         + battle.Events.OfType<AttackBlocked>().Count(e => e.Attacker == summary.Id);
            int taken = battle.Events.OfType<AttackLanded>().Count(e => e.Defender == summary.Id)
                        + battle.Events.OfType<AttackBlocked>().Count(e => e.Defender == summary.Id);
            int dodges = battle.Events.OfType<AttackDodged>().Count(e => e.Defender == summary.Id);
            bool died = battle.Events.OfType<WarriorDied>().Any(e => e.Warrior == summary.Id);
            bool escaped = battle.Events.OfType<WarriorEscaped>().Any(e => e.Warrior == summary.Id);
            bool dismembered = battle.Events.OfType<WarriorDismembered>().Any(e => e.Warrior == summary.Id);

            Assert.Equal(landed, summary.HitsLanded);
            Assert.Equal(taken, summary.TimesHit);
            Assert.Equal(dodges, summary.DodgesPerformed);
            Assert.Equal(died, summary.Died);
            Assert.Equal(escaped, summary.Escaped);
            Assert.Equal(dismembered, summary.LostLimb);
            Assert.Equal(dismembered, summary.LostParts != BodyPartSet.None);

            // Hits are a subset of attacks.
            Assert.True(summary.HitsLanded <= summary.AttacksMade);
            Assert.InRange(summary.Accuracy, 0, 1);
        }
    }

    [Theory]
    [InlineData(1ul)]
    [InlineData(77ul)]
    [InlineData(20260806ul)]
    [InlineData(4242424242ul)]
    public void TheOutcomeMatchesWhoIsLeftStanding(ulong seed)
    {
        var battle = new Battle(ThreeVsThree(), new SeededRandom(seed));
        BattleResult result = battle.Run();

        bool playerStanding = result.Summaries.Any(s => s.Team == Battle.PlayerTeam && !s.Died && !s.Escaped);
        bool enemyStanding = result.Summaries.Any(s => s.Team == Battle.EnemyTeam && !s.Died && !s.Escaped);

        switch (result.Outcome)
        {
            case BattleOutcome.PlayerVictory:
                Assert.True(playerStanding);
                Assert.False(enemyStanding);
                break;
            case BattleOutcome.PlayerWithdrawal:
            case BattleOutcome.PlayerWipe:
                Assert.False(playerStanding);
                break;
            case BattleOutcome.Stalled:
            default:
                Assert.True(playerStanding && enemyStanding);
                break;
        }
    }

    [Fact]
    public void TheDeadHaveNoHealthLeftAndTheLivingDo()
    {
        BattleResult result = new Battle(ThreeVsThree(), new SeededRandom(13)).Run();

        foreach (WarriorBattleSummary summary in result.Summaries)
        {
            if (summary.Died)
            {
                Assert.Equal(0, summary.HealthRemaining, precision: 9);
            }
            else
            {
                Assert.True(summary.HealthRemaining > 0);
            }

            Assert.True(summary.HealthRemaining >= 0);
        }
    }

    [Fact]
    public void SnapshotsTrackTheBattleWhileItRuns()
    {
        var battle = new Battle(ThreeVsThree(), new SeededRandom(14));

        while (battle.Step())
        {
            foreach (CombatantSnapshot s in battle.Snapshots())
            {
                // The HUD prints these values straight onto the bar; negative health would break it.
                Assert.InRange(s.Health, 0, s.MaxHealth);
                Assert.InRange(s.Stamina, 0, s.MaxStamina);
            }
        }

        Assert.Equal(6, battle.Snapshots().Count);
    }

    [Fact]
    public void ADrawnOutStalemateIsBrokenOffByTheStallGuard()
    {
        // Two giants, with fists: neither can finish the other, so only the guard can end it. The
        // guard is not a draw — the fight is reported as an anomaly (CombatTuning.StallGuardSeconds).
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 100_000, weapon: Weapon.Fists())],
            [TestBuilders.Warrior(101, health: 100_000, weapon: Weapon.Fists())])
        {
            Tuning = TestBuilders.PointBlank,
        };

        BattleResult result = new Battle(setup, new SeededRandom(15)).Run();

        Assert.Equal(BattleOutcome.Stalled, result.Outcome);
        Assert.True(result.ElapsedSeconds >= CombatTuning.Default.StallGuardSeconds);
        Assert.DoesNotContain(result.Summaries, s => s.Died);
    }

    [Fact]
    public void AnUnevenFightStillResolves()
    {
        // 1v3: multi-warrior support must work when they pile up on one side too.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 200, defense: 40)],
            [
                TestBuilders.Warrior(101, health: 60),
                TestBuilders.Warrior(102, health: 60),
                TestBuilders.Warrior(103, health: 60),
            ])
        {
            Tuning = TestBuilders.PointBlank,
        };

        BattleResult result = new Battle(setup, new SeededRandom(16)).Run();

        Assert.Equal(4, result.Summaries.Count);
        Assert.True(result.Outcome
            is BattleOutcome.PlayerVictory
            or BattleOutcome.PlayerWithdrawal
            or BattleOutcome.PlayerWipe);
    }

    [Fact]
    public void ABattleNeedsFightersOnBothSides()
    {
        Warrior lonely = TestBuilders.Warrior(1);

        Assert.Throws<ArgumentException>(() =>
            new Battle(new BattleSetup([lonely], []), new SeededRandom(1)));

        Assert.Throws<ArgumentException>(() =>
            new Battle(new BattleSetup([], [lonely]), new SeededRandom(1)));
    }

    [Fact]
    public void EventCollectionCanBeTurnedOffWithoutChangingTheOutcome()
    {
        // Batch simulation does not collect events; the result must not be affected by that setting, or
        // the fight balance is examined on and the fight watched on screen would differ.
        var withEvents = new Battle(ThreeVsThree(), new SeededRandom(17));
        BattleResult a = withEvents.Run();

        var silent = new Battle(ThreeVsThree() with { CollectEvents = false }, new SeededRandom(17));
        BattleResult b = silent.Run();

        Assert.Empty(silent.Events);
        Assert.Equal(a.Outcome, b.Outcome);
        Assert.Equal(a.ElapsedSeconds, b.ElapsedSeconds, precision: 9);
        Assert.Equal(a.Summaries, b.Summaries);
    }
}
