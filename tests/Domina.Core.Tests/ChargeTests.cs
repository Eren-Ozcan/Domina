using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The charge (GDD §4). The whole rule rests on one trade: you close the distance with speed and in
/// exchange leave your move exposed — you cannot move while winding up and a single hit takes the
/// charge away. The tests here tie down both ends of the trade; without the reward a charge is
/// suicide, without the price it is a free speed bonus.
/// </summary>
public class ChargeTests
{
    private static readonly WarriorId _fighter = new(1);
    private static readonly WarriorId _enemy = new(101);

    /// <summary>The sides start far apart; the die always chooses to charge.</summary>
    private static CombatTuning AlwaysCharges { get; } = CombatTuning.Default with
    {
        ChargeChanceAtZeroAggression = 1.0,
        ChargeChanceAtMaxAggression = 1.0,
    };

    private static CombatTuning NeverCharges { get; } = CombatTuning.Default with
    {
        ChargeChanceAtZeroAggression = 0.0,
        ChargeChanceAtMaxAggression = 0.0,
    };

    private static BattleSetup Duel(CombatTuning tuning, double playerHealth = 400) => new(
        [TestBuilders.Warrior(1, health: playerHealth)],
        [TestBuilders.Warrior(101, health: 400)])
    {
        Tuning = tuning,
    };

    private static bool StepUntil(Battle battle, Func<Battle, bool> predicate, int maxSteps = 2000)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            if (predicate(battle))
            {
                return true;
            }

            if (!battle.Step())
            {
                return predicate(battle);
            }
        }

        return false;
    }

    /// <summary>If the distance is right the charge starts and the warrior really speeds up.</summary>
    [Fact]
    public void ChargingClosesTheGapFasterThanWalking()
    {
        var charging = new Battle(Duel(AlwaysCharges), new SeededRandom(7));
        var walking = new Battle(Duel(NeverCharges), new SeededRandom(7));

        Assert.True(StepUntil(charging, b => b.Events.OfType<ChargeStarted>().Any()));

        // After the same number of ticks the charging one must have covered more ground.
        for (int i = 0; i < 20; i++)
        {
            charging.Step();
            walking.Step();
        }

        double chargedGap = Gap(charging);
        double walkedGap = Gap(walking);

        Assert.True(
            chargedGap < walkedGap,
            $"The charge is not faster than walking: {chargedGap:F1} >= {walkedGap:F1}");
    }

    /// <summary>
    /// <b>A charge does not close defence.</b> A running warrior keeps evading at his normal rate;
    /// defencelessness belongs to fleeing (docs/GDD.md §4-§5).
    /// </summary>
    [Fact]
    public void TheChargingWarriorStillDefends()
    {
        // A warrior with high evasion must be able to evade while charging too.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 400, evasion: 100)],
            [TestBuilders.Warrior(101, health: 400, thrown: ThrownWeapon.Shuriken())])
        {
            Tuning = AlwaysCharges with { ChargeWindupSeconds = 1.5 },
        };

        var battle = new Battle(setup, new SeededRandom(3));
        StepUntil(battle, b => b.IsFinished, maxSteps: 4000);

        Assert.Contains(battle.Events.OfType<AttackDodged>(), e => e.Defender == _fighter);
    }

    /// <summary>
    /// The first strike on arrival carries momentum: with the same seed, raising only the multiplier
    /// raises the damage.
    /// </summary>
    [Fact]
    public void ArrivingWithMomentumHitsHarder()
    {
        double plain = FirstBlowAfterCharge(bonusAtFullSpeed: 0.0);
        double heavy = FirstBlowAfterCharge(bonusAtFullSpeed: 1.0);

        Assert.True(heavy > plain, $"The charge bonus does not reach the damage: {heavy:F2} <= {plain:F2}");
    }

    /// <summary>
    /// <b>Momentum is speed:</b> with the same bonus setting, a fast warrior's arrival blow is harder
    /// than a slow one's. A heavy Oni's charge cannot be as hard as a Tengu's.
    /// </summary>
    /// <remarks>
    /// The multiplier comes out of the real speed at the moment of arrival; that is why both the
    /// <c>Speed</c> stat and <see cref="CombatTuning.ChargeSpeedMultiplier"/> feed into damage. The
    /// latter is what keeps alive the speed axis that measured as inert.
    /// </remarks>
    [Fact]
    public void AFasterWarriorChargesHarder()
    {
        double slow = FirstBlowAfterCharge(bonusAtFullSpeed: 1.0, speed: 0);
        double fast = FirstBlowAfterCharge(bonusAtFullSpeed: 1.0, speed: 100);

        Assert.True(fast > slow, $"Speed does not reach the arrival blow: {fast:F2} <= {slow:F2}");
    }

    /// <summary>
    /// <b>Meeting it head-on kills the charge.</b> When the target's counter-hit holds, the arrival
    /// blow is still made but does not earn the momentum multiplier (docs/GDD.md §4).
    /// </summary>
    /// <remarks>
    /// The counter-hit's value is not in its frequency but in its consequence: three times out of four
    /// the target cannot answer, and when he does he ends the move. This is what gives a rare answer
    /// weight without spawning a new tuning number.
    /// </remarks>
    [Fact]
    public void AParriedChargeArrivesWithoutMomentum()
    {
        double untouched = FirstBlowAfterCharge(bonusAtFullSpeed: 1.0, targetCounter: 0);
        double parried = FirstBlowAfterCharge(bonusAtFullSpeed: 1.0, targetCounter: 1);

        Assert.True(
            parried < untouched,
            $"The counter-hit does not kill the momentum: {parried:F2} >= {untouched:F2}");
    }

    /// <summary>
    /// The charge's target is different from an enemy passed on the way: his free hit is <b>not
    /// certain</b>, it depends on a die.
    /// </summary>
    [Fact]
    public void TheChargedTargetOnlySometimesCountersBack()
    {
        var battle = new Battle(
            Duel(AlwaysCharges with { ChargeTargetCounterChance = 0 }),
            new SeededRandom(11));

        StepUntil(battle, b => b.Events.OfType<ChargeConnected>().Any());

        Assert.DoesNotContain(
            battle.Events.OfType<OpportunityAttack>(),
            e => e.Defender == _fighter);
    }

    private static double FirstBlowAfterCharge(
        double bonusAtFullSpeed,
        double speed = 50,
        double targetCounter = 0)
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 400, speed: speed)],
            [TestBuilders.Warrior(101, health: 400)])
        {
            // What is measured is the multiplier, not the counter-hit die: if the target's answer holds,
            // the momentum dies and the arrival blow never earns its bonus (docs/GDD.md §4).
            Tuning = AlwaysCharges with
            {
                ChargeDamageAtFullSpeed = bonusAtFullSpeed,
                ChargeTargetCounterChance = targetCounter,
            },
        };

        var battle = new Battle(setup, new SeededRandom(11));

        Assert.True(StepUntil(battle, b => b.Events.OfType<ChargeConnected>().Any()));

        ChargeConnected arrival = battle.Events.OfType<ChargeConnected>().First();

        // What is wanted is not the first strike after arrival but the first strike by the arriving
        // warrior: the gap between them can be filled by the other side's blows.
        Assert.True(StepUntil(
            battle,
            b => b.Events.OfType<AttackLanded>()
                .Any(e => e.AtSeconds >= arrival.AtSeconds && e.Attacker == arrival.Warrior)));

        return battle.Events.OfType<AttackLanded>()
            .First(e => e.AtSeconds >= arrival.AtSeconds && e.Attacker == arrival.Warrior)
            .Damage;
    }

    /// <summary>If the target cannot be reached the move is wasted — the time limit ends the charge.</summary>
    [Fact]
    public void AChargeThatNeverArrivesIsWasted()
    {
        var battle = new Battle(
            Duel(AlwaysCharges with { ChargeMaxSeconds = 0.2 }),
            new SeededRandom(5));

        Assert.True(StepUntil(battle, b => b.Events.OfType<ChargeMissed>().Any()));
        Assert.DoesNotContain(battle.Events.OfType<ChargeConnected>(), _ => true);
    }

    /// <summary>
    /// The "pull out" command interrupts the charge. The charge is committed against its own decisions,
    /// but the player's command is a separate axis — were it not interruptible, a warrior running at the
    /// moment of the command would have to reach the enemy line and GDD §5's ladder would invert.
    /// </summary>
    [Fact]
    public void TheRetreatCommandCutsTheChargeShort()
    {
        // Because the key is closed until first contact (GDD §5) a warrior who is still running is
        // needed: the fast warrior opens contact, and the heavy one is caught charging from behind.
        var laggard = new WarriorId(2);
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400, speed: 100),
                TestBuilders.Warrior(2, health: 400, speed: 0),
            ],
            [TestBuilders.Warrior(101, health: 400, thrown: ThrownWeapon.Shuriken())])
        {
            Tuning = AlwaysCharges,
        };

        var battle = new Battle(setup, new SeededRandom(9));

        Assert.True(StepUntil(
            battle,
            b => b.ContactMade && b.SnapshotOf(laggard).State == CombatState.Charging));

        Assert.True(battle.CommandRetreat());

        Assert.Equal(CombatState.Retreating, battle.SnapshotOf(laggard).State);
        Assert.Contains(battle.Events.OfType<ChargeMissed>(), e => e.Warrior == laggard);
        Assert.DoesNotContain(battle.Events.OfType<ChargeConnected>(), e => e.Warrior == laggard);
    }

    /// <summary>
    /// A fleeing target is not charged. Measured: if it is, the 1.6x speed disables escape's only tuning
    /// knob and the chase balance (GDD §5) collapses.
    /// </summary>
    [Fact]
    public void NobodyChargesAFleeingTarget()
    {
        var battle = new Battle(Duel(AlwaysCharges), new SeededRandom(4));

        Assert.True(StepUntil(battle, b => b.ContactMade));
        Assert.True(battle.CommandRetreat());
        Assert.True(StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.Retreating));

        int before = battle.Events.OfType<ChargeStarted>().Count(e => e.Warrior == _enemy);

        StepUntil(battle, b => b.IsFinished);

        int after = battle.Events.OfType<ChargeStarted>().Count(e => e.Warrior == _enemy);
        Assert.Equal(before, after);
    }

    // ---- Birikme (GDD §4) ----

    /// <summary>
    /// A charge first gathers in place, then runs. While gathering the warrior <b>does not move</b> —
    /// this is the window where the price is paid.
    /// </summary>
    [Fact]
    public void TheChargeGathersBeforeItRuns()
    {
        var battle = new Battle(Duel(AlwaysCharges), new SeededRandom(7));

        Assert.True(StepUntil(battle, b => b.SnapshotOf(_fighter).State == CombatState.ChargeWindup));
        Assert.Empty(battle.Events.OfType<ChargeLaunched>());

        ArenaPoint gathering = battle.SnapshotOf(_fighter).Position;

        Assert.True(StepUntil(battle, b => b.Events.OfType<ChargeLaunched>().Any()));

        // No ground was covered during the windup; the run only starts now.
        Assert.Equal(gathering.X, PositionAtLaunch(battle).X, precision: 6);
        Assert.Equal(CombatState.Charging, battle.SnapshotOf(_fighter).State);
    }

    private static ArenaPoint PositionAtLaunch(Battle battle) => battle.SnapshotOf(_fighter).Position;

    /// <summary>
    /// A hit taken during the windup scatters the charge: the run never starts and the damage multiplier
    /// is not earned.
    /// </summary>
    /// <remarks>
    /// Measurement changed this rule twice. First "a heavy blow scatters it" was tried — in 3v3
    /// <b>0.0%</b> of windups scattered, because blows landing on a fresh warrior almost never reach the
    /// heavy-blow threshold. With the hit criterion, 23.6% scatter.
    /// </remarks>
    [Fact]
    public void AHitWhileGatheringBreaksTheCharge()
    {
        // Charging should happen even while in reach, and the windup should last long enough for the
        // enemy right beside him to find a chance to strike.
        // An enemy with a projectile does not charge, he throws (GDD §4) — he is the only enemy who can
        // hit a gathering warrior, and that is exactly the charge's natural counter.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 400)],
            [TestBuilders.Warrior(101, health: 400, thrown: ThrownWeapon.Shuriken())])
        {
            // The windup is kept long enough to fit the gap at the first decision moment: the enemy
            // cannot get into reach, but his projectile can — that is the deliberate blind spot.
            Tuning = AlwaysCharges with { ChargeWindupSeconds = 1.5 },
        };

        var battle = new Battle(setup, new SeededRandom(11));

        Assert.True(StepUntil(battle, b => b.Events.OfType<ChargeBroken>().Any(e => e.Warrior == _fighter)));

        ChargeBroken broken = battle.Events.OfType<ChargeBroken>().First(e => e.Warrior == _fighter);

        // The scattered windup did not turn into a run and the arrival bonus was not earned.
        Assert.DoesNotContain(
            battle.Events.OfType<ChargeLaunched>(),
            e => e.Warrior == _fighter && e.AtSeconds <= broken.AtSeconds);
        Assert.DoesNotContain(
            battle.Events.OfType<ChargeConnected>(),
            e => e.Warrior == _fighter && e.AtSeconds <= broken.AtSeconds);
    }

    /// <summary>The "pull out" command interrupts the windup too, just as it interrupts the run.</summary>
    [Fact]
    public void TheRetreatCommandCutsTheWindupToo()
    {
        var laggard = new WarriorId(2);
        var setup = new BattleSetup(
            [
                TestBuilders.Warrior(1, health: 400, speed: 100),
                TestBuilders.Warrior(2, health: 400, speed: 0),
            ],
            [TestBuilders.Warrior(101, health: 400, thrown: ThrownWeapon.Shuriken())])
        {
            Tuning = AlwaysCharges with { ChargeWindupSeconds = 1.5 },
        };

        var battle = new Battle(setup, new SeededRandom(9));

        Assert.True(StepUntil(
            battle,
            b => b.ContactMade && b.SnapshotOf(laggard).State == CombatState.ChargeWindup));

        Assert.True(battle.CommandRetreat());

        Assert.Equal(CombatState.Retreating, battle.SnapshotOf(laggard).State);
        Assert.Contains(battle.Events.OfType<ChargeMissed>(), e => e.Warrior == laggard);
    }

    // ---- Karar: statlar + zar (GDD §4) ----

    /// <summary>
    /// The charge decision comes out of the warrior's identity: the bold one charges more often.
    /// </summary>
    [Fact]
    public void AggressionDecidesHowOftenAWarriorCharges()
    {
        Assert.True(
            ChargeCount(aggression: 90) > ChargeCount(aggression: 10),
            "Aggression does not change charge frequency.");
    }

    /// <summary>
    /// How many times a warrior launches a charge over thirty fights. A single fight is at the mercy of
    /// the die; to see the tendency the run is repeated.
    /// </summary>
    private static int ChargeCount(double aggression)
    {
        int charges = 0;

        for (ulong seed = 1; seed <= 30; seed++)
        {
            // Every target felled opens a decision moment — so more than one charge die is rolled in a
            // single fight and the tendency becomes measurable.
            BattleSetup setup = FlankedPair(aggression);

            var battle = new Battle(setup, new SeededRandom(seed));
            StepUntil(battle, b => b.IsFinished, maxSteps: 4000);

            charges += battle.Events.OfType<ChargeStarted>().Count(e => e.Warrior == _fighter);
        }

        return charges;
    }

    /// <summary>
    /// No charge is launched while an enemy is in reach: there is no gap to finish the windup in.
    /// </summary>
    /// <remarks>
    /// The charge's trigger is not a fixed distance threshold but an <b>assessment of the opportunity</b>:
    /// "nobody can hit me right now and I have time to finish my windup." Even if the die always holds,
    /// no charge starts until that condition is met.
    /// </remarks>
    [Fact]
    public void NobodyChargesWithAnEnemyAlreadyInReach()
    {
        var battle = new Battle(Duel(AlwaysCharges), new SeededRandom(21));

        // Run until contact is made: now both sides are within each other's reach.
        Assert.True(StepUntil(battle, b => b.ContactMade));

        int before = battle.Events.OfType<ChargeStarted>().Count();

        for (int i = 0; i < 400; i++)
        {
            battle.Step();
        }

        Assert.Equal(before, battle.Events.OfType<ChargeStarted>().Count());
    }

    /// <summary>
    /// The measure of the gap is <b>the enemy's speed</b>: a slow enemy leaves time for the windup, a
    /// fast one does not from the same distance.
    /// </summary>
    /// <remarks>
    /// The distance needed is not a hand-picked number, it derives from <c>reach + speed × windup</c> —
    /// which is why the same distance is enough for one enemy and not for another. A long windup makes
    /// the difference visible at the opening distance.
    /// </remarks>
    [Fact]
    public void TheRoomNeededDependsOnHowFastTheEnemyIs()
    {
        Assert.True(ChargesAgainst(enemySpeed: 0), "No charge was launched against the slow enemy.");
        Assert.False(ChargesAgainst(enemySpeed: 100), "The fast enemy should not have left time for the windup.");
    }

    private static bool ChargesAgainst(double enemySpeed)
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 400)],
            [TestBuilders.Warrior(101, health: 400, speed: enemySpeed)])
        {
            // A long windup: the opening distance is enough for the slow enemy, not for the fast one.
            Tuning = AlwaysCharges with { ChargeWindupSeconds = 3.0 },
        };

        var battle = new Battle(setup, new SeededRandom(7));
        StepUntil(battle, b => b.IsFinished, maxSteps: 4000);

        return battle.Events.OfType<ChargeStarted>().Any(e => e.Warrior == _fighter);
    }

    /// <summary>
    /// A two-front 2v2: each warrior engages the one opposite him, and when one fells his target the next
    /// enemy is <b>on the other front and well out of reach</b>.
    /// </summary>
    /// <remarks>
    /// Only this shape can measure re-engagement. Set up as a crowd against a single warrior, all the
    /// enemies gather at the same point and when the target is felled the next one is already in reach —
    /// the decision moment never comes.
    /// </remarks>
    private static BattleSetup FlankedPair(double aggression) => new(
        [
            TestBuilders.Warrior(1, health: 4000, aggression: aggression, strength: 100, accuracy: 100),
            TestBuilders.Warrior(2, health: 40_000),
        ],
        [
            TestBuilders.Warrior(101, health: 1),
            TestBuilders.Warrior(102, health: 40_000),
        ])
    {
        Tuning = CombatTuning.Default with { StartSpacingY = 900 },
    };

    /// <summary>
    /// A gap opens in front of a warrior who fells his target and he can charge the next one.
    /// Measurement required this rule: with a fixed threshold, in none of 30,000 fights was a charge
    /// launched after 1.75 s, because once the lines meet the distance never appears again. Assessing the
    /// opportunity solves this by itself.
    /// </summary>
    [Fact]
    public void KillingYourTargetOpensAChargeOnTheNextOne()
    {
        // The normal threshold is unreachable; any charge launched can only have come through the
        // re-engagement window.
        BattleSetup setup = FlankedPair(aggression: 100);

        // What is measured is not whether the die held but whether the opportunity appeared.
        setup = setup with
        {
            Tuning = setup.Tuning with
            {
                ChargeChanceAtZeroAggression = 1.0,
                ChargeChanceAtMaxAggression = 1.0,
            },
        };

        var battle = new Battle(setup, new SeededRandom(13));

        Assert.True(StepUntil(battle, b => b.Events.OfType<WarriorDied>().Any(), maxSteps: 4000));

        double death = battle.Events.OfType<WarriorDied>().First().AtSeconds;

        // He must be able to charge AFTER his target is felled too: the charge is not an opening move.
        Assert.True(
            StepUntil(
                battle,
                b => b.Events.OfType<ChargeStarted>()
                    .Any(e => e.Warrior == _fighter && e.AtSeconds > death),
                maxSteps: 4000),
            "The warrior whose target was felled never charged the next one.");
    }

    /// <summary>
    /// The charge die is rolled <b>once per opening</b>, not again at every decision step for as long as
    /// the opening lasts.
    /// </summary>
    /// <remarks>
    /// This is the rule's balance reason (docs/GDD.md §4): with a die rolled per step, charge frequency
    /// depended on how long the warrior loitered in the opening — that is, inversely on his own speed.
    /// Because a faster warrior charged harder but less often, the <c>Speed</c> axis measured as inert.
    /// </remarks>
    [Fact]
    public void TheChargeIsJudgedOncePerOpening()
    {
        // A value that collides with no other probability: the die counter counts only this.
        const double chargeChance = 0.371;

        BattleSetup setup = Duel(CombatTuning.Default with
        {
            ChargeChanceAtZeroAggression = chargeChance,
            ChargeChanceAtMaxAggression = chargeChance,
            StartSpacingY = 900,
        });

        var rng = new ChargeRollCounter(chargeChance);
        var battle = new Battle(setup, rng);

        // Run until the opening closes: as the sides approach, at some point the gap ends.
        // The 900 units in between would have been enough for dozens of rolls if a die were rolled per step.
        StepUntil(battle, _ => false, maxSteps: 200);

        // There are two warriors on the field and both see the same opening: one die per man.
        Assert.Equal(2, rng.ChargeRolls);
    }

    /// <summary>
    /// A source that counts the rolls made against a given probability and always refuses that die.
    /// The other probabilities go to the real seeded source so the fight runs normally.
    /// </summary>
    private sealed class ChargeRollCounter(double counted) : IRandomSource
    {
        private readonly SeededRandom _inner = new(11);

        public int ChargeRolls { get; private set; }

        public double NextDouble() => _inner.NextDouble();

        public int NextInt(int exclusiveMax) => _inner.NextInt(exclusiveMax);

        public bool Chance(double probability)
        {
            if (probability == counted)
            {
                ChargeRolls++;
                return false;
            }

            return _inner.Chance(probability);
        }
    }

    private static double Gap(Battle battle) =>
        battle.SnapshotOf(_fighter).Position.DistanceTo(battle.SnapshotOf(_enemy).Position);
}

