using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Kadro ve gün döngüsü (GDD §6, §10). Korunan üç karar: isim eşsizliği yalnızca
/// <b>canlılar</b> arasında geçerlidir, ölen savaşçı kadrodan silinmez, ve bir gün
/// dojo'da da sefere çıkıldığında da tek bir çağrıyla kapanır.
/// </summary>
public class DojoTests
{
    [Fact]
    public void RecruitAssignsUniqueIds()
    {
        Roster roster = new();

        RosterEntry first = roster.Recruit("Kenji");
        RosterEntry second = roster.Recruit("Hana");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, roster.Count);
    }

    [Fact]
    public void ANameCannotBelongToTwoLivingWarriors()
    {
        Roster roster = new();
        roster.Recruit("Kenji");

        Assert.Throws<InvalidOperationException>(() => roster.Recruit("kenji"));
    }

    [Fact]
    public void ADeadWarriorsNameReturnsToThePool()
    {
        Roster roster = new();
        RosterEntry first = roster.Recruit("Kenji");

        roster.Kill(first.Id);
        RosterEntry second = roster.Recruit("Kenji");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, roster.Count);
        Assert.Same(second, roster.FindLiving("Kenji"));
    }

    [Fact]
    public void KillingKeepsTheRecordInTheRoster()
    {
        Roster roster = new();
        RosterEntry entry = roster.Recruit("Kenji");

        roster.Kill(entry.Id);

        Assert.NotNull(roster.Find(entry.Id));
        Assert.Empty(roster.Living);
        Assert.False(entry.Warrior.IsAlive);
    }

    [Fact]
    public void RenameRejectsANameALivingWarriorHolds()
    {
        Roster roster = new();
        RosterEntry kenji = roster.Recruit("Kenji");
        roster.Recruit("Hana");

        Assert.Throws<InvalidOperationException>(() => roster.Rename(kenji.Id, "Hana"));
        Assert.Equal("Kenji", kenji.Name);
    }

    [Fact]
    public void RenameAllowsChangingOnlyTheCasingOfTheSameName()
    {
        Roster roster = new();
        RosterEntry kenji = roster.Recruit("kenji");

        roster.Rename(kenji.Id, "Kenji");

        Assert.Equal("Kenji", kenji.Name);
    }

    [Fact]
    public void AnInjuredWarriorCannotBeSentOrTrained()
    {
        Roster roster = new();
        RosterEntry entry = roster.Recruit("Kenji");

        entry.Injure(3);

        Assert.False(entry.IsFitForCampaign);
        Assert.False(entry.Train());
        Assert.Equal(DojoActivity.Recovering, entry.Activity);
    }

    [Fact]
    public void ALongerInjuryWinsOverAShorterOne()
    {
        Roster roster = new();
        RosterEntry entry = roster.Recruit("Kenji");

        entry.Injure(2);
        entry.Injure(5);
        entry.Injure(1);

        Assert.Equal(5, entry.RecoveryDaysRemaining);
    }

    [Fact]
    public void DaysBurnTheInfirmaryDownAndReportTheRelease()
    {
        // İlaçsız gün: ambar dolu ama eczane kapalı, iyileşme doğal hızda kalsın.
        DojoState state = new(economy: new EconomyTuning { MedicineRecoveryDays = 0 });
        state.Resources = new Resources(Gold: 500);
        RosterEntry entry = state.Roster.Recruit("Kenji");
        entry.Injure(2);

        DayReport first = state.AdvanceDay();
        Assert.Empty(first.Recovered);
        Assert.Equal(1, first.Day);
        Assert.Equal(2, state.Day);

        DayReport second = state.AdvanceDay();
        Assert.Equal([entry.Id], second.Recovered);
        Assert.True(entry.IsFitForCampaign);
        Assert.Equal(DojoActivity.Resting, entry.Activity);
    }

    [Fact]
    public void TrainingDaysAccumulateOnlyWhileTraining()
    {
        DojoState state = new();
        state.Resources = new Resources(Gold: 500);
        RosterEntry entry = state.Roster.Recruit("Kenji");

        state.AdvanceDay();
        Assert.Equal(0, entry.TrainingDays);

        entry.Train();
        DayReport report = state.AdvanceDay();

        Assert.Equal(1, entry.TrainingDays);
        Assert.Equal([entry.Id], report.Trained);
    }

    [Fact]
    public void HonorDriftsBackToNeutralFromBothSides()
    {
        DojoState state = new(new DojoTuning { HonorDecayPerDay = 2 });
        RosterEntry shamed = state.Roster.Recruit("Kenji");
        RosterEntry praised = state.Roster.Recruit("Hana");
        shamed.Warrior.Honor = 20;
        praised.Warrior.Honor = 90;

        state.AdvanceDay();

        Assert.Equal(22, shamed.Warrior.Honor, 6);
        Assert.Equal(88, praised.Warrior.Honor, 6);
    }

    [Fact]
    public void HonorSettlesOnNeutralInsteadOfOscillatingAroundIt()
    {
        DojoState state = new(new DojoTuning { HonorDecayPerDay = 3 });
        RosterEntry entry = state.Roster.Recruit("Kenji");
        entry.Warrior.Honor = 51;

        state.AdvanceDay();

        Assert.Equal(HonorScale.Starting, entry.Warrior.Honor, 6);
    }

    [Fact]
    public void TheDeadAreLeftOutOfTheDayCycle()
    {
        DojoState state = new(new DojoTuning { HonorDecayPerDay = 5 });
        RosterEntry entry = state.Roster.Recruit("Kenji");
        entry.Warrior.Honor = 10;
        state.Roster.Kill(entry.Id);

        state.AdvanceDay();

        Assert.Equal(10, entry.Warrior.Honor, 6);
    }

    [Fact]
    public void ResourcesAddSubtractAndAnswerWhetherACostIsAffordable()
    {
        Resources purse = new(Gold: 100, Food: 5, Water: 5, Medicine: 1);
        Resources cost = new(Gold: 40, Medicine: 2);

        Assert.False(purse.Covers(cost));
        Assert.True(purse.Covers(new Resources(Gold: 40, Medicine: 1)));
        Assert.Equal(new Resources(60, 5, 5, -1), purse - cost);
        Assert.True((purse - cost).AnyNegative);
        Assert.Equal(new Resources(60, 5, 5, 0), (purse - cost).ClampedToZero());
    }

    [Fact]
    public void ALoadedWarriorKeepsItsIdAndDoesNotCollideWithLaterRecruits()
    {
        Roster roster = new();
        Warrior loaded = new(new WarriorId(7), "Kenji", WarriorStats.Recruit());

        roster.Add(loaded);
        RosterEntry fresh = roster.Recruit("Hana");

        Assert.Equal(new WarriorId(7), roster.Find(new WarriorId(7))!.Id);
        Assert.Equal(new WarriorId(8), fresh.Id);
    }
}
