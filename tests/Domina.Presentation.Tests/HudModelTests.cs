using Domina.Core.Combat;

namespace Domina.Presentation.Tests;

/// <summary>
/// The interface texts. A single key pulls the whole party (GDD §5); the key's only job is to say in
/// advance <b>how many warriors the command will take effect on immediately</b>.
/// </summary>
public class HudModelTests
{
    [Fact]
    public void TheButtonCountsWhoTheCommandWillReach()
    {
        RetreatPrompt prompt = HudModel.DescribeRetreat(
        [
            TestSnapshots.Of(1),
            TestSnapshots.Of(2),
            TestSnapshots.Of(101, Battle.EnemyTeam),
        ], contactMade: true);

        Assert.True(prompt.Enabled);
        Assert.False(prompt.Locked);
        Assert.Equal("PULL THE PARTY (2)", prompt.Text);
    }

    /// <summary>
    /// A locked warrior's escape starts when his strike finishes. The player must see this before
    /// pressing, or the delay feels like a bug.
    /// </summary>
    [Fact]
    public void TheButtonWarnsBeforeTheDelayHappens()
    {
        RetreatPrompt prompt = HudModel.DescribeRetreat(
        [
            TestSnapshots.Of(1),
            TestSnapshots.Of(2, state: CombatState.AttackWindup, canCancel: false),
            TestSnapshots.Of(3, state: CombatState.AttackWindup, canCancel: false),
        ], contactMade: true);

        Assert.True(prompt.Locked);
        Assert.Equal("PULL THE PARTY (3) · 2 locked", prompt.Text);
    }

    [Fact]
    public void TheDeadAndTheEscapedAreNotCounted()
    {
        RetreatPrompt prompt = HudModel.DescribeRetreat(
        [
            TestSnapshots.Of(1),
            TestSnapshots.Of(2, state: CombatState.Dead, health: 0),
            TestSnapshots.Of(3, state: CombatState.Escaped),
        ], contactMade: true);

        Assert.Equal("PULL THE PARTY (1)", prompt.Text);
    }

    [Fact]
    public void TheButtonReportsTheTeamIsAlreadyLeaving()
    {
        RetreatPrompt prompt = HudModel.DescribeRetreat(
        [
            TestSnapshots.Of(1, state: CombatState.Retreating),
            TestSnapshots.Of(2, retreatRequested: true, state: CombatState.AttackWindup, canCancel: false),
        ], contactMade: true);

        Assert.False(prompt.Enabled);
        Assert.Equal("PARTY PULLING OUT", prompt.Text);
    }

    [Fact]
    public void TheButtonGoesQuietWhenTheBattleIsOver()
    {
        RetreatPrompt prompt = HudModel.DescribeRetreat(
        [
            TestSnapshots.Of(1, state: CombatState.Dead, health: 0),
            TestSnapshots.Of(101, Battle.EnemyTeam),
        ], contactMade: true);

        Assert.False(prompt.Enabled);
        Assert.Equal("—", prompt.Text);
    }

    /// <summary>
    /// A buffered command must be visible on the panel: if the panel says "attacking" during the delay,
    /// the key reads as swallowed.
    /// </summary>
    [Fact]
    public void TheBufferedCommandIsVisibleOnThePanel()
    {
        string label = HudModel.DescribeState(
            TestSnapshots.Of(1, state: CombatState.AttackWindup, retreatRequested: true, canCancel: false));

        Assert.Equal("pulling out · when the strike ends", label);
    }

    [Fact]
    public void TheRunningWarriorIsNotReportedAsWaiting()
    {
        Assert.Equal(
            "pulling out",
            HudModel.DescribeState(TestSnapshots.Of(1, state: CombatState.Retreating, retreatRequested: true)));
    }

    [Theory]
    [InlineData(CombatState.Idle, "waiting")]
    [InlineData(CombatState.AttackWindup, "attacking")]
    [InlineData(CombatState.AttackRecovery, "recovering")]
    [InlineData(CombatState.Escaped, "escaped")]
    [InlineData(CombatState.Dead, "dead")]
    public void EveryStateHasALabel(CombatState state, string expected) =>
        Assert.Equal(expected, HudModel.DescribeState(TestSnapshots.Of(1, state: state)));

    /// <summary>
    /// Being unarmed is written on top of the state too, and reads together with poison.
    /// </summary>
    /// <remarks>
    /// With the two together the player's decision takes shape: a warrior who is poisoned <b>and</b>
    /// unarmed is the warrior who dies if the key is not pressed.
    /// </remarks>
    [Fact]
    public void ABrokenWeaponIsWrittenOnTopOfTheState()
    {
        Assert.Equal(
            "attacking · unarmed",
            HudModel.DescribeState(
                TestSnapshots.Of(1, state: CombatState.AttackWindup, disarmed: true)));

        Assert.Equal(
            "waiting · poisoned · unarmed",
            HudModel.DescribeState(
                TestSnapshots.Of(1, state: CombatState.Idle, poisoned: true, disarmed: true)));
    }

    /// <summary>
    /// Poison does not replace the state, it is written on top of it.
    /// </summary>
    /// <remarks>
    /// A poisoned warrior walks, strikes and pulls out; if his panel said "poisoned" and fell silent, the
    /// player could not see what the warrior was doing. What he needs to see is both at once:
    /// still fighting <b>and</b> losing health.
    /// </remarks>
    [Fact]
    public void PoisonIsWrittenOnTopOfTheState()
    {
        Assert.Equal(
            "attacking · poisoned",
            HudModel.DescribeState(
                TestSnapshots.Of(1, state: CombatState.AttackWindup, poisoned: true)));

        Assert.Equal(
            "pulling out · when the strike ends · poisoned",
            HudModel.DescribeState(
                TestSnapshots.Of(
                    1,
                    state: CombatState.AttackWindup,
                    retreatRequested: true,
                    canCancel: false,
                    poisoned: true)));
    }

    /// <summary>
    /// The seed must stay permanently visible: it is the only way to reopen a fight and find its
    /// counterpart in batch simulation.
    /// </summary>
    [Fact]
    public void TheStatusLineCarriesTheSeed()
    {
        Assert.Equal("seed 52  ·  15.2 s", HudModel.DescribeStatus(52, 15.24, outcome: null));
        Assert.Equal(
            "seed 52  ·  15.2 s  ·  VICTORY",
            HudModel.DescribeStatus(52, 15.24, BattleOutcome.PlayerVictory));
    }

    /// <summary>
    /// No pulling out before the fight starts (GDD §5): the key is inactive until the first hit.
    /// </summary>
    /// <remarks>
    /// The key is not hidden, it stands inactive. Hidden, the rule's existence would never be learnt;
    /// the player should wonder when the key arrives, not why it is absent.
    /// </remarks>
    [Fact]
    public void TheButtonStaysShutUntilTheFirstBloodIsDrawn()
    {
        RetreatPrompt prompt = HudModel.DescribeRetreat(
        [
            TestSnapshots.Of(1),
            TestSnapshots.Of(2),
            TestSnapshots.Of(101, Battle.EnemyTeam),
        ], contactMade: false);

        // It stays pressable but its command is refused: the press produces the text that teaches the rule.
        Assert.True(prompt.Shut);
        Assert.Equal("FIGHT NOT STARTED", prompt.Text);
    }

    /// <summary>The rule is taught at the start of the game.</summary>
    [Fact]
    public void TheRuleIsTaughtOnTheFirstPresses()
    {
        RetreatRefusalNotice notice = HudModel.DescribeRefusal(
            consecutivePresses: 1,
            teachingNoticesShown: 0);

        Assert.Equal(RetreatNoticeKind.Teaching, notice.Kind);
        Assert.NotEqual(string.Empty, notice.Text);
        Assert.Null(notice.AchievementId);
    }

    /// <summary>
    /// After teaching it, it falls silent. Repeating what he knows is noise, not information.
    /// </summary>
    [Fact]
    public void TheRuleStopsBeingRepeatedOnceItIsKnown()
    {
        RetreatRefusalNotice notice = HudModel.DescribeRefusal(
            consecutivePresses: 2,
            teachingNoticesShown: HudModel.TeachingNoticeLimit);

        Assert.Equal(RetreatNoticeKind.None, notice.Kind);
        Assert.Equal(string.Empty, notice.Text);
    }

    /// <summary>
    /// Someone who keeps pressing gets an answer — regardless of how many times it has been taught.
    /// </summary>
    [Fact]
    public void ThePersistentPresserGetsAnswered()
    {
        RetreatRefusalNotice notice = HudModel.DescribeRefusal(
            HudModel.TauntPressCount,
            teachingNoticesShown: HudModel.TeachingNoticeLimit);

        Assert.Equal(RetreatNoticeKind.Taunt, notice.Kind);
        Assert.Equal(HudModel.CowardAchievementId, notice.AchievementId);
    }
}
