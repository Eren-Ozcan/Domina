using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// A fight grows the warrior, and faster than a drill does (docs/COMPARISON-DOMINA.md, section 3).
/// These tests hold the two ends of that claim: the lesson is read from what the fight actually
/// consisted of, and the ceilings are the same ones training meets.
/// </summary>
public class CombatSchoolingTests
{
    private static WarriorBattleSummary Summary(
        int attacks = 0,
        int blocks = 0,
        int dodges = 0,
        int timesHit = 0) =>
        new(
            new WarriorId(1),
            "Test",
            Battle.PlayerTeam,
            CombatState.Idle,
            HealthRemaining: 100,
            AttacksMade: attacks,
            HitsLanded: 0,
            TimesHit: timesHit,
            DodgesPerformed: dodges,
            DamageDealt: 0,
            DamageTaken: 0,
            LostLimb: false)
        {
            BlocksPerformed = blocks,
        };

    [Theory]
    [InlineData(9, 2, 1, 0, Drill.Strikes)]
    [InlineData(2, 9, 1, 0, Drill.Guard)]
    [InlineData(2, 1, 9, 0, Drill.Footwork)]
    [InlineData(2, 1, 0, 9, Drill.Conditioning)]
    public void TheFightTeachesWhatItConsistedOf(
        int attacks,
        int blocks,
        int dodges,
        int timesHit,
        Drill expected) =>
        Assert.Equal(expected, CombatSchooling.LessonOf(Summary(attacks, blocks, dodges, timesHit)));

    /// <summary>
    /// A warrior who did nothing learns nothing — the rule pays for the fight, not for having stood in it.
    /// </summary>
    [Fact]
    public void AFightThatNeverTouchedHimTeachesNothing()
    {
        WarriorBattleSummary empty = Summary();
        WarriorStats stats = WarriorStats.Recruit();

        Assert.Null(CombatSchooling.LessonOf(empty));
        Assert.Equal(stats, CombatSchooling.After(stats, empty, talent: 1.0));
    }

    /// <summary>
    /// The claim itself: the risky road pays better than the safe one.
    /// </summary>
    /// <remarks>
    /// Both spend the same day, but only the fight can cost a limb or the man. The order is what is
    /// tested, not the size of the gap — the numbers are GDD §11's business.
    /// </remarks>
    [Fact]
    public void AFightTeachesFasterThanADrill()
    {
        WarriorStats stats = WarriorStats.Recruit();
        TrainingTuning tuning = new();

        double drilled = TrainingGround.After(stats, Drill.Strikes, talent: 1.0, tuning).Accuracy;
        double fought = CombatSchooling.After(stats, Summary(attacks: 10), talent: 1.0, tuning).Accuracy;

        Assert.True(tuning.FightGapClosed > tuning.GapClosedPerDay);
        Assert.True(fought > drilled, $"The fight did not teach faster ({fought} <= {drilled}).");
    }

    /// <summary>The two roads meet the same wall: fighting does not carry a stat past the ceiling.</summary>
    [Fact]
    public void FightingDoesNotCarryAStatPastTheTrainingCeiling()
    {
        TrainingTuning tuning = new() { FightGapClosed = 0.9 };
        WarriorStats stats = WarriorStats.Recruit() with { Accuracy = tuning.SkillCeiling - 0.5 };

        for (int fight = 0; fight < 50; fight++)
        {
            stats = CombatSchooling.After(stats, Summary(attacks: 10), talent: 1.4, tuning);
        }

        Assert.True(stats.Accuracy <= tuning.SkillCeiling);
    }

    /// <summary>Talent multiplies the fight's lesson as it multiplies a drill's.</summary>
    [Fact]
    public void TalentMultipliesWhatTheFightTeaches()
    {
        WarriorStats stats = WarriorStats.Recruit();

        double dull = CombatSchooling.After(stats, Summary(attacks: 10), talent: 0.6).Accuracy;
        double gifted = CombatSchooling.After(stats, Summary(attacks: 10), talent: 1.4).Accuracy;

        Assert.True(gifted > dull);
    }
}
