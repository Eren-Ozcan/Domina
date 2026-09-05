using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Dövüşün kadroya yazılması. Korunan karar: çekirdek kalıcı hale dokunmaz, yalnızca
/// rapor eder — ölümü, uzuv kaybını, dağılan zırhı ve biriken yıpranmayı geri dönüşsüz
/// hale çeviren tek yer burasıdır. Toplu simülasyonun aynı kadroyu on binlerce kez
/// koşturabilmesi buna bağlı.
/// </summary>
public class DojoAftermathTests
{
    private static readonly BattleAftermath _aftermath = new();

    private static WarriorBattleSummary Summary(
        WarriorId id,
        double healthRemaining = 100,
        CombatState finalState = CombatState.Idle,
        int team = Battle.PlayerTeam) =>
        new(
            id,
            "Test",
            team,
            finalState,
            healthRemaining,
            AttacksMade: 10,
            HitsLanded: 5,
            TimesHit: 0,
            DodgesPerformed: 0,
            DamageDealt: 0,
            DamageTaken: 0,
            LostLimb: false);

    private static BattleResult Result(
        params WarriorBattleSummary[] summaries) =>
        new(BattleOutcome.PlayerVictory, ElapsedSeconds: 12, summaries);

    [Fact]
    public void DeathIsWrittenIntoTheRosterAndCannotBeUndone()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji");

        AftermathReport report = _aftermath.Apply(
            state,
            Result(Summary(entry.Id, healthRemaining: 0, finalState: CombatState.Dead)));

        Assert.False(entry.Warrior.IsAlive);
        Assert.Single(report.Dead);
        Assert.Empty(state.Roster.Living);
    }

    [Fact]
    public void ADeadWarriorTakesNoRecoveryDaysAndNoHonor()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji");
        double honorBefore = entry.Warrior.Honor;

        WarriorAftermath line = _aftermath
            .Apply(state, Result(Summary(entry.Id, 0, CombatState.Dead)))
            .Warriors.Single();

        Assert.Equal(0, line.RecoveryDays);
        Assert.Equal(0, line.HonorDelta, 6);
        Assert.Equal(honorBefore, entry.Warrior.Honor, 6);
        Assert.Equal(0, entry.RecoveryDaysRemaining);
    }

    [Fact]
    public void ALostLimbBecomesAPermanentDisability()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji");

        WarriorBattleSummary summary = Summary(entry.Id, healthRemaining: 40) with
        {
            LostLimb = true,
            LostParts = BodyPartSet.SwordArm,
        };

        _aftermath.Apply(state, Result(summary));

        Assert.True(entry.Warrior.HasDisability(BodyPart.SwordArm));
        Assert.Single(entry.Warrior.Disabilities);
    }

    [Fact]
    public void TheSameLimbIsNotLostTwice()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji");
        WarriorBattleSummary summary = Summary(entry.Id, 40) with
        {
            LostLimb = true,
            LostParts = BodyPartSet.SwordArm,
        };

        _aftermath.Apply(state, Result(summary));
        AftermathReport second = _aftermath.Apply(state, Result(summary));

        Assert.Single(entry.Warrior.Disabilities);
        Assert.Empty(second.Warriors.Single().LostParts);
    }

    [Fact]
    public void ArmorWearAccumulatesAcrossBattles()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji", armor: Armor.Medium());
        WarriorBattleSummary summary = Summary(entry.Id, 80) with
        {
            ArmorWear = new ArmorWearSet(Torso: 12, SwordArm: 3),
        };

        _aftermath.Apply(state, Result(summary));
        _aftermath.Apply(state, Result(summary));

        Assert.Equal(24, entry.Warrior.ArmorWear.Torso, 6);
        Assert.Equal(6, entry.Warrior.ArmorWear.SwordArm, 6);
        Assert.Equal(0, entry.Warrior.ArmorWear.Head, 6);
    }

    [Fact]
    public void AShatteredPieceLeavesTheSetForGood()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji", armor: Armor.Medium());
        WarriorBattleSummary summary = Summary(entry.Id, 60) with
        {
            ArmorWear = new ArmorWearSet(Torso: 130),
            DestroyedArmor = HitLocationSet.Torso,
        };

        AftermathReport report = _aftermath.Apply(state, Result(summary));

        Assert.Equal(ArmorPiece.Bare, entry.Warrior.Armor.Torso);
        Assert.Equal([HitLocation.Torso], report.Warriors.Single().ShatteredArmor);
        Assert.NotEqual(ArmorPiece.Bare, entry.Warrior.Armor.SwordArm);
    }

    [Fact]
    public void AShatteredSlotStartsItsLedgerOverForTheNextPiece()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji", armor: Armor.Medium());

        _aftermath.Apply(
            state,
            Result(Summary(entry.Id, 60) with
            {
                ArmorWear = new ArmorWearSet(Torso: 130),
                DestroyedArmor = HitLocationSet.Torso,
            }));

        Assert.Equal(0, entry.Warrior.ArmorWear.Torso, 6);
    }

    [Fact]
    public void ScratchesCostNoDays()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji");

        _aftermath.Apply(state, Result(Summary(entry.Id, healthRemaining: 90)));

        Assert.Equal(0, entry.RecoveryDaysRemaining);
        Assert.True(entry.IsFitForCampaign);
    }

    [Fact]
    public void HeavyWoundsCostDaysAndTheWarriorCannotBeSent()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji");

        _aftermath.Apply(state, Result(Summary(entry.Id, healthRemaining: 5)));

        Assert.Equal(6, entry.RecoveryDaysRemaining);
        Assert.False(entry.IsFitForCampaign);
        Assert.Equal(DojoActivity.Recovering, entry.Activity);
    }

    [Fact]
    public void ALostLimbAddsItsOwnTreatmentDays()
    {
        DojoState state = new(new DojoTuning { RecoveryDaysPerLostLimb = 4 });
        RosterEntry entry = state.Roster.Recruit("Kenji");

        _aftermath.Apply(
            state,
            Result(Summary(entry.Id, healthRemaining: 100) with
            {
                LostLimb = true,
                LostParts = BodyPartSet.LeftLeg,
            }));

        Assert.Equal(4, entry.RecoveryDaysRemaining);
    }

    [Fact]
    public void HonorFollowsHowTheWarriorFought()
    {
        DojoState state = new();
        RosterEntry sharp = state.Roster.Recruit("Kenji");
        RosterEntry blunt = state.Roster.Recruit("Hana");
        sharp.Warrior.Honor = 50;
        blunt.Warrior.Honor = 50;

        _aftermath.Apply(
            state,
            Result(
                Summary(sharp.Id, 70) with { AttacksMade = 10, HitsLanded = 9 },
                Summary(blunt.Id, 70) with { AttacksMade = 10, HitsLanded = 1 }));

        Assert.True(sharp.Warrior.Honor > 50);
        Assert.True(blunt.Warrior.Honor < 50);
    }

    [Fact]
    public void RunningAwayCostsHonorOnTopOfTheFightItself()
    {
        DojoState state = new();
        RosterEntry stood = state.Roster.Recruit("Kenji");
        RosterEntry fled = state.Roster.Recruit("Hana");

        _aftermath.Apply(
            state,
            Result(
                Summary(stood.Id, 70),
                Summary(fled.Id, 70, CombatState.Escaped)));

        Assert.True(fled.Warrior.Honor < stood.Warrior.Honor);
    }

    [Fact]
    public void HonorNeverLeavesItsScale()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji");
        entry.Warrior.Honor = HonorScale.Max;

        _aftermath.Apply(state, Result(Summary(entry.Id, 70) with { AttacksMade = 10, HitsLanded = 10 }));

        Assert.Equal(HonorScale.Max, entry.Warrior.Honor, 6);
    }

    [Fact]
    public void TheEnemySideIsNeverWrittenIntoTheRoster()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit("Kenji");

        AftermathReport report = _aftermath.Apply(
            state,
            Result(Summary(entry.Id, 100, team: Battle.EnemyTeam) with
            {
                LostLimb = true,
                LostParts = BodyPartSet.SwordArm,
            }));

        Assert.Empty(report.Warriors);
        Assert.Empty(entry.Warrior.Disabilities);
        Assert.True(entry.Warrior.IsAlive);
    }

    [Fact]
    public void AWarriorWhoIsNotInTheRosterIsIgnored()
    {
        DojoState state = new();
        state.Roster.Recruit("Kenji");

        AftermathReport report = _aftermath.Apply(state, Result(Summary(new WarriorId(99), 10)));

        Assert.Empty(report.Warriors);
    }

    [Fact]
    public void ARealBattleLandsInTheRosterInOnePiece()
    {
        DojoState state = new();
        RosterEntry entry = state.Roster.Recruit(
            "Kenji",
            WarriorStats.Recruit() with { MaxHealth = 40 },
            armor: Armor.Light());

        Warrior yokai = TestBuilders.Warrior(500, "Oni", health: 400, strength: 90);
        BattleSetup setup = new([entry.Warrior], [yokai]) { Tuning = TestBuilders.PointBlank };
        BattleResult result = new Battle(setup, new SeededRandom(7)).Run();

        AftermathReport report = _aftermath.Apply(state, result);

        WarriorAftermath line = Assert.Single(report.Warriors);
        Assert.Equal(entry.Id, line.Id);
        Assert.Equal(line.Died, !entry.Warrior.IsAlive);
        Assert.True(entry.Warrior.ArmorWear.Total >= 0);
    }
}
