using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Hit and run (docs/GDD.md §7): a poisoner who has already dosed his man steps out of the man's
/// reach and lets the dose do the rest of the work. It is off by default
/// (<see cref="CombatTuning.PoisonBackstepSeconds"/> 0), which is what every figure measured before
/// it stands on.
/// </summary>
public class BackstepTests
{
    /// <summary>
    /// The move belongs to the dose, not to the man: a warrior with a clean blade fights exactly the
    /// fight he fought before the state existed, whatever the knob says.
    /// </summary>
    [Fact]
    public void ACleanBladeNeverStepsBack()
    {
        BattleResult withMove = Run(Weapon.Tanto(), backstep: 1.5);
        BattleResult without = Run(Weapon.Tanto(), backstep: 0);

        Assert.Equal(without.Outcome, withMove.Outcome);
        Assert.Equal(without.ElapsedSeconds, withMove.ElapsedSeconds);
    }

    /// <summary>The knob at zero is the old fight, poisoned blade and all.</summary>
    [Fact]
    public void TheKnobAtZeroIsTheOldFight()
    {
        BattleResult poisoned = Run(Weapon.PoisonedTanto(), backstep: 0);
        BattleResult clean = Run(Weapon.PoisonedTanto(), backstep: 0);

        Assert.Equal(clean.Outcome, poisoned.Outcome);
        Assert.Equal(clean.ElapsedSeconds, poisoned.ElapsedSeconds);
    }

    /// <summary>
    /// With the knob open the poisoned fight comes out differently — the state reaches the field.
    /// </summary>
    [Fact]
    public void ThePoisonerStepsBackAndTheFightChanges()
    {
        int differences = 0;

        for (ulong seed = 1; seed <= 12; seed++)
        {
            BattleResult stepping = Run(Weapon.PoisonedTanto(), backstep: 1.5, seed);
            BattleResult standing = Run(Weapon.PoisonedTanto(), backstep: 0, seed);

            if (stepping.ElapsedSeconds != standing.ElapsedSeconds || stepping.Outcome != standing.Outcome)
            {
                differences++;
            }
        }

        Assert.True(differences > 0, "the poisoner never left reach");
    }

    /// <summary>
    /// He does not strike while he walks, so the step has to show as a <b>slower</b> striking rate.
    /// The count alone would not show it — the stepping fight also lasts longer, which is the whole
    /// point of the move — so what is measured is strikes per second.
    /// </summary>
    [Fact]
    public void TheStepIsPaidForInStrikesNotTaken()
    {
        double stepping = 0;
        double standing = 0;

        for (ulong seed = 1; seed <= 12; seed++)
        {
            stepping += StrikeRate(Run(Weapon.PoisonedTanto(), backstep: 1.5, seed));
            standing += StrikeRate(Run(Weapon.PoisonedTanto(), backstep: 0, seed));
        }

        Assert.True(stepping < standing, $"stepping {stepping:F2}, standing {standing:F2} strikes/s");
    }

    /// <summary>The poisoner's own striking rate over the fight he was in.</summary>
    private static double StrikeRate(BattleResult result)
    {
        WarriorBattleSummary poisoner = result.SummaryFor(new WarriorId(1));
        return result.ElapsedSeconds <= 0 ? 0 : poisoner.AttacksMade / result.ElapsedSeconds;
    }

    /// <summary>
    /// The step waits on the <b>dose cap</b>, not on the word "poisoned": if another strike would
    /// still add dose, standing there is the better move and he never walks. Raising the cap out of
    /// reach therefore has to reproduce the fight he fights with the move switched off entirely.
    /// </summary>
    [Fact]
    public void HeStaysInReachWhileAFreshDoseWouldStillLand()
    {
        for (ulong seed = 1; seed <= 12; seed++)
        {
            BattleResult unreachableCap = Run(Weapon.PoisonedTanto(), backstep: 1.0, seed, maxDose: 1000);
            BattleResult moveOff = Run(Weapon.PoisonedTanto(), backstep: 0, seed, maxDose: 1000);

            Assert.Equal(moveOff.Outcome, unreachableCap.Outcome);
            Assert.Equal(moveOff.ElapsedSeconds, unreachableCap.ElapsedSeconds);
        }
    }

    private const ulong Seed = 4242;

    /// <summary>
    /// One poisoner against one enemy who outlives a single dose: the step can only be read on a
    /// fight long enough for the dose to still be ticking when the man walks.
    /// </summary>
    private static BattleResult Run(
        Weapon weapon,
        double backstep,
        ulong seed = Seed,
        double? maxDose = null)
    {
        Warrior player = TestBuilders.Warrior(1, health: 120, weapon: weapon, klass: WarriorClass.Dokushi);
        Warrior enemy = TestBuilders.Warrior(101, health: 200, weapon: Weapon.Katana());

        CombatTuning tuning = CombatTuning.Default with { PoisonBackstepSeconds = backstep };
        if (maxDose is double cap)
        {
            tuning = tuning with { PoisonMaxDose = cap };
        }

        BattleSetup setup = new([player], [enemy])
        {
            Tuning = tuning,
            CollectEvents = false,
        };

        return new Battle(setup, new SeededRandom(seed)).Run();
    }
}
