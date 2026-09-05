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
        ], contactMade: true);

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
        ], contactMade: true);

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
        ], contactMade: true);

        Assert.Equal("EKİBİ ÇEK (1)", prompt.Text);
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
        Assert.Equal("EKİP ÇEKİLİYOR", prompt.Text);
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
    /// Silahsızlık da durumun üstüne yazılır ve zehirle birlikte okunabilir.
    /// </summary>
    /// <remarks>
    /// İkisi bir aradayken oyuncunun kararı belirir: zehirlenmiş <b>ve</b> silahsız
    /// savaşçı, tuşa basılmadığında ölen savaşçıdır.
    /// </remarks>
    [Fact]
    public void ABrokenWeaponIsWrittenOnTopOfTheState()
    {
        Assert.Equal(
            "saldırıyor · silahsız",
            HudModel.DescribeState(
                TestSnapshots.Of(1, state: CombatState.AttackWindup, disarmed: true)));

        Assert.Equal(
            "bekliyor · zehirli · silahsız",
            HudModel.DescribeState(
                TestSnapshots.Of(1, state: CombatState.Idle, poisoned: true, disarmed: true)));
    }

    /// <summary>
    /// Zehir durumun yerine geçmez, üstüne yazılır.
    /// </summary>
    /// <remarks>
    /// Zehirlenmiş savaşçı yürür, vurur ve çekilir; paneli "zehirlendi" deyip sussaydı
    /// oyuncu savaşçının ne yaptığını göremezdi. Görmesi gereken şey ikisi birden:
    /// hâlâ dövüşüyor <b>ve</b> canı gidiyor.
    /// </remarks>
    [Fact]
    public void PoisonIsWrittenOnTopOfTheState()
    {
        Assert.Equal(
            "saldırıyor · zehirli",
            HudModel.DescribeState(
                TestSnapshots.Of(1, state: CombatState.AttackWindup, poisoned: true)));

        Assert.Equal(
            "çekilecek · vuruş bitince · zehirli",
            HudModel.DescribeState(
                TestSnapshots.Of(
                    1,
                    state: CombatState.AttackWindup,
                    retreatRequested: true,
                    canCancel: false,
                    poisoned: true)));
    }

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

    /// <summary>
    /// Savaş başlamadan çekilmek yok (GDD §5): tuş ilk isabete kadar pasif.
    /// </summary>
    /// <remarks>
    /// Tuş gizlenmiyor, pasif duruyor. Gizlenseydi kuralın varlığı hiç öğrenilmezdi;
    /// oyuncu tuşun neden yokluğunu değil, ne zaman geleceğini merak etmeli.
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

        // Basılabilir kalır ama komutu reddedilir: basış kuralı öğreten metni doğurur.
        Assert.True(prompt.Shut);
        Assert.Equal("SAVAŞ BAŞLAMADI", prompt.Text);
    }

    /// <summary>Kural oyun başında öğretilir.</summary>
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
    /// Öğrettikten sonra susar. Bildiğini tekrarlamak bilgi değil gürültüdür.
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
    /// Israrla basana cevap verilir — kaç kez öğretildiğinden bağımsız olarak.
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
