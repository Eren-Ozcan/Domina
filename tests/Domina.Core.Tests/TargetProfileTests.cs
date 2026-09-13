using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The enemy kinds' character (docs/GDD.md §4, Open Decision #3): the same target decision taken with
/// different appetites, never a second code path.
/// </summary>
public class TargetProfileTests
{
    private const ulong Seed = 4242;

    /// <summary>
    /// The profile must be a <b>multiplier</b> over the tuning, so that all-ones is the behaviour every
    /// figure measured before profiles existed was measured on.
    /// </summary>
    [Fact]
    public void TheDefaultProfileIsAllOnes()
    {
        TargetProfile d = TargetProfile.Default;

        Assert.Equal(1, d.Distance);
        Assert.Equal(1, d.Wounded);
        Assert.Equal(1, d.Exposed);
        Assert.Equal(1, d.Crowd);
        Assert.Equal(1, d.Stickiness);
    }

    /// <summary>
    /// A field of default profiles is the same fight as a field with the reading switched off — the
    /// regression guard for every number taken before this feature.
    /// </summary>
    [Fact]
    public void ADefaultFieldFightsExactlyAsItDidBeforeProfiles()
    {
        BattleResult withProfiles = Run(TargetProfile.Default, profilesRead: true);
        BattleResult without = Run(TargetProfile.Default, profilesRead: false);

        Assert.Equal(without.Outcome, withProfiles.Outcome);
        Assert.Equal(without.ElapsedSeconds, withProfiles.ElapsedSeconds);
    }

    /// <summary>
    /// A kind's appetite actually reaches the field: the same men, the same seed, and only the way the
    /// enemy weighs the field differs — the fight has to come out differently.
    /// </summary>
    [Fact]
    public void AnAppetiteChangesTheFight()
    {
        TargetProfile hungry = new(Wounded: 3, Exposed: 2, Stickiness: 0.2);
        int differences = 0;
        System.Text.StringBuilder log = new();

        for (ulong seed = 1; seed <= 12; seed++)
        {
            BattleResult plain = Run(TargetProfile.Default, profilesRead: true, seed);
            BattleResult greedy = Run(hungry, profilesRead: true, seed);

            log.Append(seed).Append(": ").Append(plain.ElapsedSeconds).Append(" vs ")
                .Append(greedy.ElapsedSeconds).Append(' ');

            if (plain.ElapsedSeconds != greedy.ElapsedSeconds || plain.Outcome != greedy.Outcome)
            {
                differences++;
            }
        }

        Assert.True(differences > 0, log.ToString());
    }

    /// <summary>
    /// The switch is a measuring switch: with it off, an appetite is not read at all.
    /// </summary>
    [Fact]
    public void TheSwitchSilencesAnAppetite()
    {
        TargetProfile hungry = new(Wounded: 3, Exposed: 2, Stickiness: 0.2);

        BattleResult silenced = Run(hungry, profilesRead: false);
        BattleResult plain = Run(TargetProfile.Default, profilesRead: false);

        Assert.Equal(plain.Outcome, silenced.Outcome);
        Assert.Equal(plain.ElapsedSeconds, silenced.ElapsedSeconds);
    }

    /// <summary>
    /// Every kind on the road carries a character, and the roster's own men carry none: a warrior the
    /// player hired is directed by the player, not by a temperament that reads the field against him.
    /// </summary>
    [Fact]
    public void TheKindsHaveAppetitesAndTheDojosMenDoNot()
    {
        foreach (EnemyKind kind in Adversaries.All)
        {
            Warrior spawned = kind.Spawn(new WarriorId(500), 1.0);

            Assert.NotEqual(TargetProfile.Default, spawned.Targeting);
        }

        Assert.Equal(TargetProfile.Default, TestBuilders.Warrior(1).Targeting);
    }

    /// <summary>Three of the dojo's men against three enemies who all share one profile.</summary>
    private static BattleResult Run(TargetProfile enemyProfile, bool profilesRead, ulong seed = Seed)
    {
        List<Warrior> players =
        [
            TestBuilders.Warrior(1, health: 120),
            TestBuilders.Warrior(2, health: 120),
            TestBuilders.Warrior(3, health: 120),
        ];

        List<Warrior> enemies = [];
        for (int i = 0; i < 3; i++)
        {
            Warrior enemy = TestBuilders.Warrior(101 + i, health: 110);
            enemy.Targeting = enemyProfile;
            enemies.Add(enemy);
        }

        BattleSetup setup = new(players, enemies)
        {
            Tuning = CombatTuning.Default with { TargetProfiles = profilesRead },
            CollectEvents = false,
        };

        return new Battle(setup, new SeededRandom(seed)).Run();
    }
}
