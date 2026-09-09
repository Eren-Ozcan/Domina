using Domina.Core.Combat;
using Domina.Core.Honor;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The honour engine (GDD §6). Two design decisions are protected here: chat's effect is computed on a
/// <b>ratio rather than a raw count</b> (so a small and a large stream weigh the same), and honour
/// returns to neutral over time (so a momentary troll wave does not turn into a permanent death
/// sentence).
/// </summary>
public class HonorTests
{
    private static readonly HonorEngine _engine = new();

    private static WarriorBattleSummary Summary(
        int attacks,
        int hits,
        CombatState finalState = CombatState.Idle) =>
        new(
            new WarriorId(1),
            "Test",
            Battle.PlayerTeam,
            finalState,
            HealthRemaining: 50,
            attacks,
            hits,
            TimesHit: 0,
            DodgesPerformed: 0,
            DamageDealt: 0,
            DamageTaken: 0,
            LostLimb: false);

    // ------------------------------------------------------------ performans

    [Fact]
    public void FlawlessAggressionEarnsTheFullSwing()
    {
        Assert.Equal(_engine.Tuning.PerformanceHonorSwing, _engine.PerformanceDelta(Summary(10, 10)), precision: 9);
    }

    [Fact]
    public void MissingEverythingCostsTheFullSwing()
    {
        Assert.Equal(-_engine.Tuning.PerformanceHonorSwing, _engine.PerformanceDelta(Summary(10, 0)), precision: 9);
    }

    [Fact]
    public void HalfAccuracyIsNeutral()
    {
        Assert.Equal(0, _engine.PerformanceDelta(Summary(10, 5)), precision: 9);
    }

    [Fact]
    public void ABattleWithoutASingleSwingIsPunished()
    {
        // A fight that ends without a single attack is not worth watching.
        Assert.True(_engine.PerformanceDelta(Summary(0, 0)) < 0);
    }

    [Fact]
    public void SurvivingByRunningEarnsLessThanStandingGround()
    {
        double stoodGround = _engine.PerformanceDelta(Summary(10, 10));
        double ranAway = _engine.PerformanceDelta(Summary(10, 10, CombatState.Escaped));

        // Fleeing is sensible but earns no honour — the "shall I pull out" dilemma is born here.
        Assert.True(ranAway < stoodGround);
    }

    // ---------------------------------------------------------- chat tepkisi

    [Fact]
    public void SilenceMovesNothing()
    {
        Assert.Equal(0, _engine.LiveVoteDelta(CrowdVerdict.Silent), precision: 9);
        Assert.False(CrowdVerdict.Silent.HasVotes);
    }

    [Fact]
    public void AUnanimousCrowdMovesHonorByTheFullSwing()
    {
        Assert.Equal(_engine.Tuning.LiveVoteHonorSwing, _engine.LiveVoteDelta(new CrowdVerdict(20, 0)), precision: 9);
        Assert.Equal(-_engine.Tuning.LiveVoteHonorSwing, _engine.LiveVoteDelta(new CrowdVerdict(0, 20)), precision: 9);
    }

    [Fact]
    public void ASplitCrowdCancelsOut()
    {
        Assert.Equal(0, _engine.LiveVoteDelta(new CrowdVerdict(15, 15)), precision: 9);
    }

    [Fact]
    public void TargetedVotesStayTinyComparedToLiveOnes()
    {
        // Otherwise a group could kill a warrior who never fought by spamming him.
        double targeted = Math.Abs(_engine.TargetedVoteDelta(isBushi: false));
        double live = Math.Abs(_engine.LiveVoteDelta(new CrowdVerdict(0, 20)));

        Assert.True(targeted * 10 < live);
        Assert.True(_engine.TargetedVoteDelta(isBushi: true) > 0);
    }

    // ------------------------------------------------------------ reward ratio

    [Fact]
    public void RewardMultiplierSpansTheConfiguredRange()
    {
        HonorTuning tuning = HonorTuning.Default;

        Assert.Equal(tuning.MaxRewardMultiplier, new CrowdVerdict(10, 0).RewardMultiplier(tuning), precision: 9);
        Assert.Equal(tuning.MinRewardMultiplier, new CrowdVerdict(0, 10).RewardMultiplier(tuning), precision: 9);
        Assert.Equal(1.0, new CrowdVerdict(5, 5).RewardMultiplier(tuning), precision: 9);
        Assert.Equal(1.0, CrowdVerdict.Silent.RewardMultiplier(tuning), precision: 9);
    }

    [Fact]
    public void SmallAndLargeChatsAreTreatedEqually()
    {
        HonorTuning tuning = HonorTuning.Default;

        // 3 ronin in a chat of 5 and 3000 ronin in a chat of 5000 are the same verdict.
        double small = new CrowdVerdict(2, 3).RewardMultiplier(tuning);
        double large = new CrowdVerdict(2000, 3000).RewardMultiplier(tuning);

        Assert.Equal(small, large, precision: 9);
        Assert.Equal(
            _engine.LiveVoteDelta(new CrowdVerdict(2, 3)),
            _engine.LiveVoteDelta(new CrowdVerdict(2000, 3000)),
            precision: 9);
    }

    // ------------------------------------------------------------------ decay

    [Fact]
    public void HonorDriftsTowardNeutralFromBothSides()
    {
        HonorTuning tuning = HonorTuning.Default;

        Assert.Equal(20 + tuning.DecayPerHourTowardNeutral, _engine.ApplyDecay(20, TimeSpan.FromHours(1)), precision: 9);
        Assert.Equal(80 - tuning.DecayPerHourTowardNeutral, _engine.ApplyDecay(80, TimeSpan.FromHours(1)), precision: 9);
    }

    [Fact]
    public void DecayNeverOvershootsNeutral()
    {
        Assert.Equal(50, _engine.ApplyDecay(48, TimeSpan.FromHours(1)), precision: 9);
        Assert.Equal(50, _engine.ApplyDecay(52, TimeSpan.FromHours(1)), precision: 9);
        Assert.Equal(50, _engine.ApplyDecay(50, TimeSpan.FromHours(10)), precision: 9);
    }

    [Fact]
    public void ASingleTrollWaveDoesNotSurviveARestPeriod()
    {
        // Only SUSTAINED dishonour should lead to seppuku.
        double honor = HonorEngine.Apply(HonorScale.Starting, _engine.LiveVoteDelta(new CrowdVerdict(0, 500)));
        Assert.True(honor < HonorScale.Starting);

        double recovered = _engine.ApplyDecay(honor, TimeSpan.FromHours(24));
        Assert.Equal(50, recovered, precision: 9);
    }

    // ------------------------------------------------------------------- scale

    [Fact]
    public void HonorStaysInsideTheScale()
    {
        Assert.Equal(HonorScale.Max, HonorEngine.Apply(HonorScale.Max, +50), precision: 9);
        Assert.Equal(HonorScale.Min, HonorEngine.Apply(HonorScale.Min, -50), precision: 9);
        Assert.Equal(
            HonorScale.Starting,
            new Warrior(new WarriorId(1), "Yeni", WarriorStats.Recruit()).Honor,
            precision: 9);
    }
}
