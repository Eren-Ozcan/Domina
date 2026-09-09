using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The limb-loss outcome tree (GDD §7). What decides is whether the blow is <b>lethal</b>:
/// <code>
/// heavy blow + dismemberment die held
///   ├─ health > 0  → the limb goes, the fight continues
///   └─ health ≤ 0  → if the key was pressed he lives with the limb lost, otherwise he dies
/// </code>
/// This tree is the mechanic that defines the game's identity — "pressing the key saves a life but is
/// not free".
/// </summary>
public class DismembermentTests
{
    /// <summary>
    /// A setting that lands every blow on a leg.
    /// </summary>
    /// <remarks>
    /// Tests exercising the outcome tree need the blow to land on a <b>severable</b> region; a blow to
    /// the torso has nothing to take off. The region distribution is the subject of separate
    /// tests.
    /// </remarks>
    private static CombatTuning AlwaysLimb { get; } = TestBuilders.PointBlank with
    {
        TorsoHitWeight = 0,
        HeadHitWeight = 0,
        ArmHitWeight = 0,
        LegHitWeight = 100,
    };

    /// <summary>A setup that passes the heavy-blow threshold in one strike without killing.</summary>
    /// <summary>
    /// A light, fast weapon: it <b>draws first blood</b> without passing the heavy-blow threshold.
    /// </summary>
    /// <remarks>
    /// Because the "pull out" key is closed before the first hit (GDD §5), a test measuring the
    /// intervention branch has to start the fight first. The victim's own strike is the cheapest way:
    /// it unlocks the key without risking anyone's life.
    /// </remarks>
    private static Weapon Quick { get; } =
        new("Test-Tantō", WeaponClass.Cutting, 12, TwoHanded: false, AttackSeconds: 0.4);

    /// <summary>
    /// Starts the fight, then presses the "pull out" key.
    /// </summary>
    /// <remarks>
    /// The key is closed until the first hit (GDD §5): there is no such thing as fleeing before contact.
    /// That is not the subject of the tests measuring the intervention branch, it is their precondition.
    /// </remarks>
    private static bool PressAfterFirstBlood(Battle battle)
    {
        while (!battle.ContactMade && battle.Step())
        {
        }

        return battle.CommandRetreat();
    }

    private static BattleSetup Executioner(
        double defenderHealth = 300,
        Armor? armor = null,
        double victimAggression = 0,
        Weapon? victimWeapon = null) => new(
        [
            TestBuilders.Warrior(
                1,
                "Kurban",
                health: defenderHealth,
                aggression: victimAggression,
                weapon: victimWeapon,
                armor: armor),
        ],
        [TestBuilders.Warrior(101, "Cellat", aggression: 100, weapon: TestBuilders.Executioner())])
        {
            Tuning = AlwaysLimb,
        };

    /// <summary>
    /// A heavy blow that does not kill costs a limb without any key being pressed, and the warrior stays
    /// on the field.
    /// </summary>
    /// <remarks>
    /// The reason this branch exists: while dismemberment only happened when <c>PlayerIntervened</c> was
    /// set, and only the "Flee" key that ended the expedition set that flag, <b>winning while losing a
    /// limb was impossible</b> (20,000 fights, victory + limb loss: 0 times).
    /// </remarks>
    [Fact]
    public void ANonLethalGrievousBlowCostsALimbWithNoButtonPressed()
    {
        var battle = new Battle(Executioner(), new FixedRandom(0.0));
        battle.Run();

        // The first severing must happen while the warrior is still standing: health 300, one blow ~90.
        WarriorDismembered first = battle.Events.OfType<WarriorDismembered>().First();
        AttackLanded blow = battle.Events
            .OfType<AttackLanded>()
            .First(e => e.Defender == new WarriorId(1));

        Assert.Equal(first.Warrior, blow.Defender);
        Assert.True(blow.DefenderHealthRemaining > 0);

        // And the fight must have continued after the severing.
        Assert.Contains(
            battle.Events.OfType<AttackLanded>(),
            e => e.AtSeconds > first.AtSeconds);
    }

    /// <summary>A warrior who loses a limb can win the fight — a maimed champion comes home.</summary>
    /// <remarks>
    /// In the old outcome tree this was impossible: severing required the "Flee" key, and the key ended
    /// the expedition. 20,000 fights were measured, victory + limb loss never appeared.
    /// </remarks>
    [Fact]
    public void AWarriorCanWinTheBattleAfterLosingALimb()
    {
        // The victim strikes hard (finishes in 2 blows), the executioner strikes often but light:
        // because the threshold was lowered, those light blows cost a limb too.
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kurban", health: 400, aggression: 100, weapon: TestBuilders.Executioner())],
            [
                TestBuilders.Warrior(
                    101,
                    "Cellat",
                    health: 100,
                    aggression: 100,
                    weapon: new Weapon("Testere", WeaponClass.Cutting, 30, false, 0.4)),
            ])
        {
            Tuning = AlwaysLimb with { GrievousSeverityThreshold = 0.05 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WarriorBattleSummary survivor = result.SummaryFor(new WarriorId(1));

        Assert.Equal(BattleOutcome.PlayerVictory, result.Outcome);
        Assert.True(survivor.LostLimb);
        Assert.False(survivor.Died);
    }

    [Fact]
    public void ALethalGrievousBlowWithoutInterventionKills()
    {
        // Health 60: one blow both passes the threshold and ends the health, and no key was pressed.
        var battle = new Battle(Executioner(defenderHealth: 60), new FixedRandom(0.0));
        BattleResult result = battle.Run();

        WarriorBattleSummary victim = result.SummaryFor(new WarriorId(1));

        Assert.True(victim.Died);
        Assert.Equal(
            DeathCause.GrievousBlow,
            battle.Events.OfType<WarriorDied>().Single().Cause);
    }

    /// <summary>
    /// The key's job: turning a lethal blow into limb loss.
    /// </summary>
    /// <remarks>
    /// Survival is <b>not immortality</b> — the saved warrior is left with 1 health and spends the rest
    /// of the escape one blow away from dying. This test keeps the executioner slow to isolate that
    /// branch: he must not catch up, so a single blow's outcome can be measured.
    /// </remarks>
    [Fact]
    public void InterventionTurnsALethalBlowIntoALimbLoss()
    {
        BattleSetup setup = Executioner(defenderHealth: 60, victimAggression: 100, victimWeapon: Quick) with
        {
            EnemySide =
            [
                TestBuilders.Warrior(
                    101, "Cellat", aggression: 100, speed: 1, weapon: TestBuilders.Executioner()),
            ],
        };

        var battle = new Battle(setup, new FixedRandom(0.0));

        // Pressing the key counts as "intervening in time" — even if the escape has not started yet.
        // The key only unlocks once the fight has begun (§5), so first blood has to be drawn first.
        Assert.True(PressAfterFirstBlood(battle));

        WarriorBattleSummary victim = battle.Run().SummaryFor(new WarriorId(1));

        Assert.False(victim.Died);
        Assert.True(victim.LostLimb);
        Assert.Contains(battle.Events, e => e is WarriorDismembered);
        Assert.DoesNotContain(battle.Events, e => e is WarriorDied);
    }

    /// <summary>
    /// The key cannot save a warrior with no limb left to take — there is no price to convert.
    /// </summary>
    [Fact]
    public void InterventionCannotSaveAWarriorWithNoLimbsLeftToLose()
    {
        // The victim fights just enough to unlock the key: he draws first blood with a fast, light weapon,
        // then presses. Otherwise he would die without the key ever unlocking and the branch would go unmeasured.
        Warrior victim = TestBuilders.Warrior(
            1, "Kurban", health: 60, aggression: 100, weapon: Quick);
        victim.AddDisability(BodyPart.SwordArm);
        victim.AddDisability(BodyPart.OffArm);
        victim.AddDisability(BodyPart.RightLeg);
        victim.AddDisability(BodyPart.LeftLeg);
        victim.AddDisability(BodyPart.Eye);

        var setup = new BattleSetup(
            [victim],
            [TestBuilders.Warrior(101, "Cellat", aggression: 100, weapon: TestBuilders.Executioner())])
        {
            Tuning = AlwaysLimb,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        Assert.True(PressAfterFirstBlood(battle));

        Assert.True(battle.Run().SummaryFor(new WarriorId(1)).Died);
        Assert.DoesNotContain(battle.Events, e => e is WarriorDismembered);
    }

    [Fact]
    public void LightBlowsNeverTriggerTheGrievousTree()
    {
        // Fists: the damage/max health ratio stays well below the threshold (0.28).
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 300, aggression: 0)],
            [TestBuilders.Warrior(101, aggression: 100, weapon: Weapon.Fists())])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        PressAfterFirstBlood(battle);
        battle.Run();

        Assert.Contains(battle.Events, e => e is AttackLanded);
        Assert.DoesNotContain(battle.Events, e => e is WarriorDismembered);
    }

    [Fact]
    public void BluntWeaponsRarelyDismember()
    {
        // Die 0.20: below the 0.35 threshold for a blade (it severs), above the
        // 0.35 × 0.15 = 0.0525 threshold for a blunt weapon (it does not).
        Weapon blunt = TestBuilders.Executioner() with { Class = WeaponClass.Blunt };

        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, health: 300, aggression: 0)],
            [TestBuilders.Warrior(101, aggression: 100, weapon: blunt)])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new FixedRandom(0.20));
        PressAfterFirstBlood(battle);
        battle.Run();

        Assert.Contains(battle.Events, e => e is AttackLanded);
        Assert.DoesNotContain(battle.Events, e => e is WarriorDismembered);
    }

    [Fact]
    public void HeavyArmorPreventsDismembermentThatBareSkinAllows()
    {
        // The item that makes investing in equipment worthwhile: the same blow, the same die, different armour.
        // A blow to the leg — 0.35 on a bare leg, 0.35 × 0.60 = 0.21 under heavy suneate.
        var unarmored = new Battle(Executioner(armor: Armor.None()), new FixedRandom(0.30));
        PressAfterFirstBlood(unarmored);
        unarmored.Run();

        var armored = new Battle(Executioner(armor: Armor.Heavy()), new FixedRandom(0.30));
        PressAfterFirstBlood(armored);
        armored.Run();

        Assert.Contains(unarmored.Events, e => e is WarriorDismembered);
        Assert.DoesNotContain(armored.Events, e => e is WarriorDismembered);
    }

    /// <summary>
    /// Resistance is read from the piece of the <b>region</b> the blow landed on — not from the kit's
    /// average.
    /// </summary>
    /// <remarks>
    /// This is the whole rationale for slot-by-slot armour: a light keikogi covers the torso, not the
    /// arm. The same kit, the same die, the same weapon — when the blow lands on the arm the arm comes
    /// off, when it lands on the torso it does not. Had resistance stayed a single scalar, both would
    /// give the same result and the decision "buy the cheap kit, risk your arms" would never exist.
    /// </remarks>
    [Fact]
    public void TheStruckRegionDecidesResistanceNotTheSuit()
    {
        // Die 0.30 — torso with keikogi: 0.35 × 0.80 = 0.28 (no severing).
        //            Bare arm:          0.35 × 1.00 = 0.35 (severs).
        var toTheArm = new Battle(AtRegion(HitLocation.SwordArm, Armor.Light()), new FixedRandom(0.30));
        PressAfterFirstBlood(toTheArm);
        toTheArm.Run();

        var toTheTorso = new Battle(AtRegion(HitLocation.Torso, Armor.Light()), new FixedRandom(0.30));
        PressAfterFirstBlood(toTheTorso);
        toTheTorso.Run();

        Assert.Contains(toTheArm.Events, e => e is WarriorDismembered);
        Assert.DoesNotContain(toTheTorso.Events, e => e is WarriorDismembered);
    }

    /// <summary>A piece covering the arm stops the severing a kit that leaves the arm bare allows.</summary>
    [Fact]
    public void KoteProtectsTheArmThatAKeikogiLeavesBare()
    {
        var bareArms = new Battle(AtRegion(HitLocation.SwordArm, Armor.Light()), new FixedRandom(0.30));
        PressAfterFirstBlood(bareArms);
        bareArms.Run();

        // The dō-maru's kote: 0.35 × 0.70 = 0.245, which stays below the die.
        var withKote = new Battle(AtRegion(HitLocation.SwordArm, Armor.Medium()), new FixedRandom(0.30));
        PressAfterFirstBlood(withKote);
        withKote.Run();

        Assert.Contains(bareArms.Events, e => e is WarriorDismembered);
        Assert.DoesNotContain(withKote.Events, e => e is WarriorDismembered);
    }

    /// <summary>
    /// A warrior cannot lose the same limb twice — however many blows he takes during the escape.
    /// </summary>
    /// <remarks>
    /// While the records kept a single part per warrior, every new loss erased the previous one: a
    /// warrior who had lost an arm counted as "his arm is still there" once he lost a leg, and the arm
    /// could come off again. Measured: 22 severings on one warrior, arm/leg in turn.
    /// </remarks>
    [Fact]
    public void ALimbCanOnlyBeLostOncePerWarrior()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kurban", health: 100_000, aggression: 0)],
            [
                TestBuilders.Warrior(101, "Cellat1", aggression: 100, weapon: TestBuilders.Executioner()),
                TestBuilders.Warrior(102, "Cellat2", aggression: 100, weapon: TestBuilders.Executioner()),
                TestBuilders.Warrior(103, "Cellat3", aggression: 100, weapon: TestBuilders.Executioner()),
            ])
        {
            // Health is enormous, every blow is heavy, every die holds the severing: the victim can only
            // be stopped by the rule "no limb left to lose".
            Tuning = TestBuilders.PointBlank with { GrievousSeverityThreshold = 0.0 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        PressAfterFirstBlood(battle);
        BattleResult result = battle.Run();

        BodyPart[] lost = [.. battle.Events.OfType<WarriorDismembered>().Select(e => e.Part)];

        Assert.NotEmpty(lost);
        Assert.Equal(lost.Length, lost.Distinct().Count());
        Assert.Equal(lost.Order(), result.SummaryFor(new WarriorId(1)).LostParts.Parts().Order());
    }

    /// <summary>A setup that lands every blow on the given region.</summary>
    private static BattleSetup AtRegion(HitLocation location, Armor armor)
    {
        CombatTuning tuning = TestBuilders.PointBlank with
        {
            TorsoHitWeight = location == HitLocation.Torso ? 100 : 0,
            LegHitWeight = location == HitLocation.RightLeg ? 100 : 0,
            ArmHitWeight = location == HitLocation.SwordArm ? 100 : 0,
            HeadHitWeight = location == HitLocation.Head ? 100 : 0,
        };

        return Executioner(armor: armor) with { Tuning = tuning };
    }

    [Fact]
    public void AlreadyLostPartsAreNotChosenAgain()
    {
        Warrior victim = TestBuilders.Warrior(1, health: 300, aggression: 0);
        Assert.True(victim.AddDisability(BodyPart.SwordArm));

        var setup = new BattleSetup(
            [victim],
            [TestBuilders.Warrior(101, aggression: 100, weapon: TestBuilders.Executioner())])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        PressAfterFirstBlood(battle);
        battle.Run();

        WarriorDismembered lost = battle.Events.OfType<WarriorDismembered>().First();
        Assert.NotEqual(BodyPart.SwordArm, lost.Part);
    }

    /// <summary>
    /// A heavy blow to the torso costs a limb too — intervention is never free.
    /// </summary>
    /// <remarks>
    /// With torso hits made not to sever, intervention became riskless and player victory rose from 36%
    /// to 53% (10,000 fights, 3v3). The region concerns damage and armour, not the outcome tree.
    /// </remarks>
    [Fact]
    public void BlowsToTheTorsoStillCostALimb()
    {
        var setup = new BattleSetup(
            [TestBuilders.Warrior(1, "Kurban", health: 300, aggression: 0)],
            [TestBuilders.Warrior(101, "Cellat", aggression: 100, weapon: TestBuilders.Executioner())])
        {
            // Every blow lands on the torso; the weights of the remaining limbs stay at their defaults,
            // because the limb to come off will be chosen among them.
            Tuning = TestBuilders.PointBlank with { TorsoHitWeight = 1000 },
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        PressAfterFirstBlood(battle);
        battle.Run();

        Assert.Contains(battle.Events, e => e is WarriorDismembered);
    }

    [Fact]
    public void BattleNeverMutatesThePermanentWarrior()
    {
        // Writing the persistent state is the meta layer's job. If this breaks, tens of thousands of
        // fights cannot be simulated with the same roster — the basic assumption of batch simulation.
        Warrior victim = TestBuilders.Warrior(1, health: 300, aggression: 0);

        var setup = new BattleSetup(
            [victim],
            [TestBuilders.Warrior(101, aggression: 100, weapon: TestBuilders.Executioner())])
        {
            Tuning = TestBuilders.PointBlank,
        };

        var battle = new Battle(setup, new FixedRandom(0.0));
        PressAfterFirstBlood(battle);
        battle.Run();

        Assert.True(victim.IsAlive);
        Assert.Empty(victim.Disabilities);
    }

    [Fact]
    public void ArmLossForcesFistsInsteadOfATwoHandedWeapon()
    {
        Warrior warrior = TestBuilders.Warrior(1, weapon: Weapon.Nodachi());
        Assert.Equal(Weapon.Nodachi(), warrior.UsableWeapon);

        warrior.AddDisability(BodyPart.SwordArm);

        Assert.Equal(Weapon.Fists(), warrior.UsableWeapon);
    }

    [Theory]
    [InlineData(BodyPart.SwordArm)]
    [InlineData(BodyPart.OffArm)]
    [InlineData(BodyPart.RightLeg)]
    [InlineData(BodyPart.Eye)]
    public void DisabilitiesArePermanentAndNotDuplicated(BodyPart part)
    {
        Warrior warrior = TestBuilders.Warrior(1);

        Assert.True(warrior.AddDisability(part));
        Assert.False(warrior.AddDisability(part));
        Assert.Single(warrior.Disabilities);
        Assert.True(warrior.HasDisability(part));
    }

    [Fact]
    public void EachLostPartWeakensItsOwnStat()
    {
        WarriorStats baseline = TestBuilders.Warrior(1, strength: 100, evasion: 100, accuracy: 100).BaseStats;

        Warrior armless = TestBuilders.Warrior(1, strength: 100, evasion: 100, accuracy: 100);
        armless.AddDisability(BodyPart.SwordArm);

        Warrior lame = TestBuilders.Warrior(2, strength: 100, evasion: 100, accuracy: 100);
        lame.AddDisability(BodyPart.RightLeg);

        Warrior halfBlind = TestBuilders.Warrior(3, strength: 100, evasion: 100, accuracy: 100);
        halfBlind.AddDisability(BodyPart.Eye);

        Assert.True(armless.EffectiveStats.Strength < baseline.Strength);
        Assert.Equal(baseline.Evasion, armless.EffectiveStats.Evasion);

        Assert.True(lame.EffectiveStats.Evasion < baseline.Evasion);
        Assert.True(halfBlind.EffectiveStats.Accuracy < baseline.Accuracy);
    }
}
