using Domina.Core.Honor;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Seppuku kuyruğu (GDD §6). Kuyruğun sebebi: <c>!bushi</c>/<c>!ronin</c> hem aktif
/// dövüşe tepki hem oylama oyudur. Aynı anda hem dövüş hem oylama açık olsaydı
/// chat'in yazdığı komutun hangisine sayıldığı belirsiz kalırdı — bu yüzden oylama
/// dövüş bitene kadar bekler ve asla iki oylama birden açılmaz.
/// </summary>
public class SeppukuTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 8, 5, 20, 0, 0, TimeSpan.Zero);
    private static readonly HonorTuning _tuning = HonorTuning.Default;

    private static Warrior Disgraced(int id, double honor = 5)
    {
        Warrior w = TestBuilders.Warrior(id);
        w.Honor = honor;
        return w;
    }

    private static SeppukuArbiter Arbiter(double roll = 0.5) =>
        new(new FixedRandom(roll), _tuning);

    /// <summary>Oylamayı açar ve döner (dövüş bitmiş kabul edilir).</summary>
    private static SeppukuVote OpenVote(SeppukuArbiter arbiter, Warrior warrior)
    {
        Assert.True(arbiter.Consider(warrior, _t0));
        Assert.Null(arbiter.Tick(_t0));
        return arbiter.ActiveVote!;
    }

    // ------------------------------------------------------------------ eşik

    [Fact]
    public void OnlyWarriorsBelowTheThresholdAreQueued()
    {
        var arbiter = Arbiter();

        Assert.False(arbiter.Consider(Disgraced(1, _tuning.SeppukuThreshold), _t0));
        Assert.False(arbiter.Consider(Disgraced(2, _tuning.SeppukuThreshold + 1), _t0));
        Assert.True(arbiter.Consider(Disgraced(3, _tuning.SeppukuThreshold - 0.1), _t0));
        Assert.Equal(1, arbiter.PendingCount);
    }

    [Fact]
    public void TheDeadAreNotJudgedAgain()
    {
        Warrior warrior = Disgraced(1);
        warrior.Kill();

        Assert.False(Arbiter().Consider(warrior, _t0));
    }

    [Fact]
    public void AWarriorIsQueuedOnlyOnce()
    {
        var arbiter = Arbiter();
        Warrior warrior = Disgraced(1);

        Assert.True(arbiter.Consider(warrior, _t0));
        Assert.False(arbiter.Consider(warrior, _t0));
        Assert.Equal(1, arbiter.PendingCount);
    }

    // ---------------------------------------------------------------- kuyruk

    [Fact]
    public void NoVoteOpensWhileABattleIsRunning()
    {
        var arbiter = Arbiter();
        arbiter.BattleInProgress = true;
        arbiter.Consider(Disgraced(1), _t0);

        Assert.Null(arbiter.Tick(_t0));
        Assert.Null(arbiter.ActiveVote);
        Assert.Equal(1, arbiter.PendingCount);

        arbiter.BattleInProgress = false;
        arbiter.Tick(_t0);

        Assert.NotNull(arbiter.ActiveVote);
        Assert.Equal(0, arbiter.PendingCount);
    }

    [Fact]
    public void OnlyOneVoteIsOpenAtATime()
    {
        var arbiter = Arbiter();
        arbiter.Consider(Disgraced(1), _t0);
        arbiter.Consider(Disgraced(2), _t0);

        arbiter.Tick(_t0);

        Assert.NotNull(arbiter.ActiveVote);
        Assert.Equal(new WarriorId(1), arbiter.ActiveVote!.WarriorId);
        Assert.Equal(1, arbiter.PendingCount);

        // Açık oylama varken sıradaki beklemeye devam eder.
        arbiter.Tick(_t0 + TimeSpan.FromSeconds(1));
        Assert.Equal(new WarriorId(1), arbiter.ActiveVote!.WarriorId);
        Assert.Equal(1, arbiter.PendingCount);
    }

    [Fact]
    public void TheQueueDrainsInOrder()
    {
        var arbiter = Arbiter();
        arbiter.Consider(Disgraced(1), _t0);
        arbiter.Consider(Disgraced(2), _t0);

        arbiter.Tick(_t0);
        arbiter.CastVote("izleyici", isBushi: true);

        SeppukuResolution? first = arbiter.Tick(_t0 + _tuning.VoteWindow);
        Assert.NotNull(first);
        Assert.Equal(new WarriorId(1), first!.WarriorId);

        arbiter.Tick(_t0 + _tuning.VoteWindow);
        Assert.Equal(new WarriorId(2), arbiter.ActiveVote!.WarriorId);
    }

    // ------------------------------------------------------------------- oy

    [Fact]
    public void EachUserVotesOnce()
    {
        var arbiter = Arbiter();
        OpenVote(arbiter, Disgraced(1));

        Assert.True(arbiter.CastVote("kaguya", isBushi: true));
        Assert.False(arbiter.CastVote("kaguya", isBushi: false));

        // Büyük harf farkıyla ikinci hesap taklidi de sayılmaz.
        Assert.False(arbiter.CastVote("KAGUYA", isBushi: false));
        Assert.Equal(1, arbiter.ActiveVote!.VoterCount);
    }

    [Fact]
    public void VotesOutsideAnOpenBallotAreIgnored()
    {
        var arbiter = Arbiter();

        Assert.False(arbiter.CastVote("kimse", isBushi: true));
    }

    [Fact]
    public void TheVoteStaysOpenUntilItsWindowCloses()
    {
        var arbiter = Arbiter();
        SeppukuVote vote = OpenVote(arbiter, Disgraced(1));

        Assert.Equal(_t0 + _tuning.VoteWindow, vote.ClosesAt);
        Assert.Null(arbiter.Tick(_t0 + _tuning.VoteWindow - TimeSpan.FromSeconds(1)));
        Assert.NotNull(arbiter.Tick(_t0 + _tuning.VoteWindow));
    }

    // --------------------------------------------------------------- sonuç

    [Fact]
    public void TheMajorityDecides()
    {
        var arbiter = Arbiter();
        OpenVote(arbiter, Disgraced(1));

        arbiter.CastVote("a", isBushi: true);
        arbiter.CastVote("b", isBushi: true);
        arbiter.CastVote("c", isBushi: false);

        SeppukuResolution resolution = arbiter.ForceResolve(_t0)!;

        Assert.Equal(SeppukuOutcome.Pardoned, resolution.Outcome);
        Assert.False(resolution.DecidedByAudience);
        Assert.Equal(new CrowdVerdict(2, 1), resolution.Verdict);
    }

    [Fact]
    public void ATieIsNotMercy()
    {
        var arbiter = Arbiter();
        OpenVote(arbiter, Disgraced(1));

        arbiter.CastVote("a", isBushi: true);
        arbiter.CastVote("b", isBushi: false);

        Assert.Equal(SeppukuOutcome.Seppuku, arbiter.ForceResolve(_t0)!.Outcome);
    }

    [Fact]
    public void ASingleVoteCountsAsARealVerdict()
    {
        // Asgari katılım eşiği yok: tek oy bile AI kararını devre dışı bırakır.
        var arbiter = Arbiter(roll: 0.0);
        OpenVote(arbiter, Disgraced(1));
        arbiter.CastVote("tek", isBushi: false);

        SeppukuResolution resolution = arbiter.ForceResolve(_t0)!;

        Assert.Equal(SeppukuOutcome.Seppuku, resolution.Outcome);
        Assert.False(resolution.DecidedByAudience);
    }

    [Fact]
    public void SilenceHandsTheDecisionToTheArtificialAudience()
    {
        var arbiter = Arbiter(roll: 0.0);
        OpenVote(arbiter, Disgraced(1));

        SeppukuResolution resolution = arbiter.ForceResolve(_t0)!;

        Assert.True(resolution.DecidedByAudience);
        Assert.False(resolution.Verdict.HasVotes);
    }

    /// <summary>
    /// Sıfır oyda AI, <b>oylamaya giren savaşçının</b> onuruna bakmalıdır. Oylama
    /// açılırken savaşçı kuyruktan çıkarıldığı için bu değer bir kez kaybolmuştu;
    /// test o hatanın geri gelmemesi için var.
    /// </summary>
    [Fact]
    public void TheArtificialAudienceJudgesTheRightWarriorsHonor()
    {
        // Eşiğe yakın onur (11.9) → af şansı ~%50; sıfıra yakın onur → ~%5.
        var lucky = Arbiter(roll: 0.30);
        OpenVote(lucky, Disgraced(1, honor: 11.9));
        Assert.Equal(SeppukuOutcome.Pardoned, lucky.ForceResolve(_t0)!.Outcome);

        var doomed = Arbiter(roll: 0.30);
        OpenVote(doomed, Disgraced(2, honor: 0));
        Assert.Equal(SeppukuOutcome.Seppuku, doomed.ForceResolve(_t0)!.Outcome);
    }

    // ------------------------------------------------------------ af cooldown

    [Fact]
    public void APardonBuysImmunityForFifteenMinutes()
    {
        var arbiter = Arbiter();
        Warrior warrior = Disgraced(1);

        OpenVote(arbiter, warrior);
        arbiter.CastVote("merhametli", isBushi: true);
        Assert.Equal(SeppukuOutcome.Pardoned, arbiter.ForceResolve(_t0)!.Outcome);

        // Onuru hâlâ eşiğin altında olsa bile yeni oylama açılmaz.
        Assert.False(arbiter.Consider(warrior, _t0 + _tuning.PardonImmunity - TimeSpan.FromMinutes(1)));
        Assert.True(arbiter.Consider(warrior, _t0 + _tuning.PardonImmunity));
    }

    [Fact]
    public void ACondemnedWarriorGetsNoImmunity()
    {
        var arbiter = Arbiter();
        Warrior warrior = Disgraced(1);

        OpenVote(arbiter, warrior);
        arbiter.CastVote("acımasız", isBushi: false);
        Assert.Equal(SeppukuOutcome.Seppuku, arbiter.ForceResolve(_t0)!.Outcome);

        // Kalıcı ölümü işlemek meta katmanın işi; hakem savaşçıyı öldürmez.
        Assert.True(warrior.IsAlive);
        Assert.True(arbiter.Consider(warrior, _t0 + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void PardonedHonorLandsAboveTheThreshold()
    {
        // Affedilen savaşçı doğrudan yeni bir oylamaya düşmemeli.
        Assert.True(Arbiter().PardonedHonor > _tuning.SeppukuThreshold);
    }

    [Fact]
    public void ForceResolveDoesNothingWithoutAnOpenVote()
    {
        Assert.Null(Arbiter().ForceResolve(_t0));
    }

    [Fact]
    public void TheFallbackGetsHarsherAsHonorDrops()
    {
        var fallback = new HonorWeightedFallback(_tuning);
        var rng = new SeededRandom(4242);

        int pardonsAtThreshold = 0;
        int pardonsAtZero = 0;

        for (int i = 0; i < 2000; i++)
        {
            if (fallback.ShouldPardon(_tuning.SeppukuThreshold, rng))
            {
                pardonsAtThreshold++;
            }

            if (fallback.ShouldPardon(0, rng))
            {
                pardonsAtZero++;
            }
        }

        Assert.True(pardonsAtThreshold > pardonsAtZero * 3);
    }

    [Fact]
    public void ArbiterIsDeterministicForAGivenSeed()
    {
        static SeppukuOutcome Resolve(ulong seed)
        {
            var arbiter = new SeppukuArbiter(new SeededRandom(seed), _tuning);
            arbiter.Consider(Disgraced(1, honor: 6), _t0);
            arbiter.Tick(_t0);
            return arbiter.ForceResolve(_t0)!.Outcome;
        }

        Assert.Equal(Resolve(99), Resolve(99));
    }
}
