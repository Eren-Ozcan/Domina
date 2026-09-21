using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// A class carries an appetite of its own (docs/GDD.md §4): the dokushi spreads the dose instead of
/// finishing the man the dose is already killing. It is the one exception to the rule that the dojo's
/// own men read the field alike.
/// </summary>
public class ClassProfileTests
{
    /// <summary>
    /// Only the poison class has one. The torite and the kyūdō keep the default until their own round
    /// measures one for them, and the classless man never had one.
    /// </summary>
    [Fact]
    public void OnlyTheDokushiCarriesAnAppetite()
    {
        Assert.Equal(TargetProfile.Default, ClassAptitude.Targeting(WarriorClass.None));
        Assert.Equal(TargetProfile.Default, ClassAptitude.Targeting(WarriorClass.Torite));
        Assert.Equal(TargetProfile.Default, ClassAptitude.Targeting(WarriorClass.Kyudo));

        TargetProfile poison = ClassAptitude.Targeting(WarriorClass.Dokushi);

        Assert.NotEqual(TargetProfile.Default, poison);
        Assert.True(poison.Wounded < 1, "the dying man is work already being done");
        Assert.True(poison.Crowd > 1, "he wants a body nobody else is on");
        Assert.True(poison.Stickiness < 1, "he holds his own fight loosely");
    }

    /// <summary>
    /// The appetite reaches the field: the same men, the same seed, and only whether the class is read
    /// differs — the fight has to come out differently somewhere across a sweep.
    /// </summary>
    [Fact]
    public void TheClassAppetiteChangesTheFight()
    {
        int differences = 0;

        for (ulong seed = 1; seed <= 12; seed++)
        {
            BattleResult read = Run(WarriorClass.Dokushi, classProfiles: true, seed);
            BattleResult silenced = Run(WarriorClass.Dokushi, classProfiles: false, seed);

            if (read.ElapsedSeconds != silenced.ElapsedSeconds || read.Outcome != silenced.Outcome)
            {
                differences++;
            }
        }

        Assert.True(differences > 0, "the dokushi's appetite never reached the field");
    }

    /// <summary>
    /// With the switch off a classed man reads the field exactly as the classless one beside him — the
    /// regression guard for every figure taken before class appetites existed.
    /// </summary>
    [Fact]
    public void TheSwitchOffFightsAsIfNoClassHadAnAppetite()
    {
        BattleResult classed = Run(WarriorClass.Dokushi, classProfiles: false);
        BattleResult classless = Run(WarriorClass.None, classProfiles: false);

        Assert.Equal(classless.Outcome, classed.Outcome);
        Assert.Equal(classless.ElapsedSeconds, classed.ElapsedSeconds);
    }

    /// <summary>
    /// A kind's own character speaks first. An adversary spawned with an appetite keeps it whatever
    /// class he carries, so the class fallback can never quietly overwrite Open Decision #3's six.
    /// </summary>
    [Fact]
    public void AKindsOwnAppetiteIsNotOverwrittenByHisClass()
    {
        TargetProfile kind = new(Wounded: 3, Exposed: 2, Stickiness: 0.2);

        BattleResult read = Run(WarriorClass.Dokushi, classProfiles: true, Seed, kind);
        BattleResult silenced = Run(WarriorClass.Dokushi, classProfiles: false, Seed, kind);

        Assert.Equal(silenced.Outcome, read.Outcome);
        Assert.Equal(silenced.ElapsedSeconds, read.ElapsedSeconds);
    }

    private const ulong Seed = 4242;

    /// <summary>
    /// Six of the dojo's men, all of one class, against six uneven enemies.
    /// </summary>
    /// <remarks>
    /// The field is six a side <b>on purpose</b>. Measured while this test was written: at three a
    /// side the same appetite moves nothing at all (0 of 12 seeds), because every man already has an
    /// opponent of his own and there is nothing to weigh; at six it moves 9 of 12. An appetite is a
    /// statement about whom to pick, so it can only speak on a field where picking is a real question.
    /// </remarks>
    private static BattleResult Run(
        WarriorClass klass,
        bool classProfiles,
        ulong seed = Seed,
        TargetProfile? explicitProfile = null)
    {
        List<Warrior> players = [];
        for (int i = 0; i < 6; i++)
        {
            Warrior man = TestBuilders.Warrior(1 + i, health: 120);
            man.Class = klass;

            if (explicitProfile is not null)
            {
                man.Targeting = explicitProfile;
            }

            players.Add(man);
        }

        // The enemies are deliberately uneven: a profile that weighs a wound differently can only
        // show on a field where one enemy is nearer death than another.
        List<Warrior> enemies = [];
        for (int i = 0; i < 6; i++)
        {
            enemies.Add(TestBuilders.Warrior(101 + i, health: 45 + (i * 20)));
        }

        BattleSetup setup = new(players, enemies)
        {
            Tuning = CombatTuning.Default with { ClassProfiles = classProfiles },
            CollectEvents = false,
        };

        return new Battle(setup, new SeededRandom(seed)).Run();
    }
}
