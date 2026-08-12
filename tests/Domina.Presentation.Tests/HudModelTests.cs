using Domina.Core.Combat;

namespace Domina.Presentation.Tests;

/// <summary>
/// Arayüz metinleri. Tek tuş, ekibin tamamını çekiyor (GDD §5); tuşun tek işi
/// <b>komutun kaç savaşçıda anında işleyeceğini</b> önceden söylemek.
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
        ]);

        Assert.True(prompt.Enabled);
        Assert.False(prompt.Locked);
        Assert.Equal("EKİBİ ÇEK (2)", prompt.Text);
    }

    /// <summary>
    /// Kilitli savaşçının kaçışı vuruş bitince başlar. Oyuncu bunu basmadan önce
    /// görebilmeli, yoksa gecikme hata gibi hissedilir.
    /// </summary>
    [Fact]
    public void TheButtonWarnsBeforeTheDelayHappens()
    {
        RetreatPrompt prompt = HudModel.DescribeRetreat(
        [
            TestSnapshots.Of(1),
            TestSnapshots.Of(2, state: CombatState.AttackWindup, canCancel: false),
            TestSnapshots.Of(3, state: CombatState.AttackWindup, canCancel: false),
        ]);

        Assert.True(prompt.Locked);
        Assert.Equal("EKİBİ ÇEK (3) · 2 kilitli", prompt.Text);
    }

    [Fact]
    public void TheDeadAndTheEscapedAreNotCounted()
    {
        RetreatPrompt prompt = HudModel.DescribeRetreat(
        [
            TestSnapshots.Of(1),
            TestSnapshots.Of(2, state: CombatState.Dead, health: 0),
            TestSnapshots.Of(3, state: CombatState.Escaped),
        ]);

        Assert.Equal("EKİBİ ÇEK (1)", prompt.Text);
    }

    [Fact]
    public void TheButtonReportsTheTeamIsAlreadyLeaving()
    {
        RetreatPrompt prompt = HudModel.DescribeRetreat(
        [
            TestSnapshots.Of(1, state: CombatState.Retreating),
            TestSnapshots.Of(2, retreatRequested: true, state: CombatState.AttackWindup, canCancel: false),
        ]);

        Assert.False(prompt.Enabled);
        Assert.Equal("EKİP ÇEKİLİYOR", prompt.Text);
    }

    [Fact]
    public void TheButtonGoesQuietWhenTheBattleIsOver()
    {
        RetreatPrompt prompt = HudModel.DescribeRetreat(
        [
            TestSnapshots.Of(1, state: CombatState.Dead, health: 0),
            TestSnapshots.Of(101, Battle.EnemyTeam),
        ]);

        Assert.False(prompt.Enabled);
        Assert.Equal("—", prompt.Text);
    }

    /// <summary>
    /// Buffer'lanan komut panelde görünmeli: gecikme boyunca panel "saldırıyor" derse
    /// tuş yutulmuş gibi okunur.
    /// </summary>
    [Fact]
    public void TheBufferedCommandIsVisibleOnThePanel()
    {
        string label = HudModel.DescribeState(
            TestSnapshots.Of(1, state: CombatState.AttackWindup, retreatRequested: true, canCancel: false));

        Assert.Equal("çekilecek · vuruş bitince", label);
    }

    [Fact]
    public void TheRunningWarriorIsNotReportedAsWaiting()
    {
        Assert.Equal(
            "çekiliyor",
            HudModel.DescribeState(TestSnapshots.Of(1, state: CombatState.Retreating, retreatRequested: true)));
    }

    [Theory]
    [InlineData(CombatState.Idle, "bekliyor")]
    [InlineData(CombatState.AttackWindup, "saldırıyor")]
    [InlineData(CombatState.AttackRecovery, "toparlanıyor")]
    [InlineData(CombatState.Escaped, "kurtuldu")]
    [InlineData(CombatState.Dead, "öldü")]
    public void EveryStateHasALabel(CombatState state, string expected) =>
        Assert.Equal(expected, HudModel.DescribeState(TestSnapshots.Of(1, state: state)));

    /// <summary>
    /// Seed sürekli görünür durmalı: bir dövüşü tekrar açmanın ve toplu simülasyondaki
    /// karşılığını bulmanın tek yolu o.
    /// </summary>
    [Fact]
    public void TheStatusLineCarriesTheSeed()
    {
        Assert.Equal("seed 52  ·  15.2 sn", HudModel.DescribeStatus(52, 15.24, outcome: null));
        Assert.Equal(
            "seed 52  ·  15.2 sn  ·  ZAFER",
            HudModel.DescribeStatus(52, 15.24, BattleOutcome.PlayerVictory));
    }
}
