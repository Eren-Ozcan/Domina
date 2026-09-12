using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;

namespace Domina.Core.Tests;

/// <summary>
/// The difficulty tiers (docs/GDD.md §10). The decision protected: Master <b>is</b> the measured game —
/// the other two are multipliers laid over it, never a second set of tuning — and a tier changes how
/// often the player is in trouble, not what trouble means.
/// </summary>
public class DifficultyTests
{
    /// <summary>Master leaves every measured number exactly where the docs say it is.</summary>
    [Fact]
    public void MasterChangesNothingAtAll()
    {
        EncounterTuning curve = new();
        EconomyTuning economy = new();

        Assert.Same(curve, Difficulty.Master.Apply(curve));
        Assert.Same(economy, Difficulty.Master.Apply(economy));
    }

    /// <summary>The two tiers pull the enemy and the purse in opposite directions.</summary>
    [Fact]
    public void TheTiersMoveTheEnemyAndTheMoneyTogether()
    {
        EncounterTuning curve = new();
        EconomyTuning economy = new();

        EncounterTuning soft = Difficulty.Apprentice.Apply(curve);
        EncounterTuning hard = Difficulty.Legend.Apply(curve);

        Assert.True(soft.StartingPower < curve.StartingPower);
        Assert.True(hard.StartingPower > curve.StartingPower);

        Assert.True(
            Difficulty.Apprentice.Apply(economy).VictoryGoldPerEnemyHealth
            > economy.VictoryGoldPerEnemyHealth);
        Assert.True(
            Difficulty.Legend.Apply(economy).VictoryGoldPerEnemyHealth
            < economy.VictoryGoldPerEnemyHealth);
    }

    /// <summary>The ceiling moves with the curve, or the tier stops existing in the late season.</summary>
    [Fact]
    public void TheCurvesCeilingMovesWithIt()
    {
        EncounterTuning curve = new();

        Assert.True(Difficulty.Legend.Apply(curve).MaxPower > curve.MaxPower);
        Assert.True(Difficulty.Apprentice.Apply(curve).MaxPower < curve.MaxPower);
    }

    /// <summary>A tier never touches what a wound or a death costs.</summary>
    [Fact]
    public void ATierDoesNotChangeWhatTroubleMeans()
    {
        EconomyTuning economy = new();
        EconomyTuning soft = Difficulty.Apprentice.Apply(economy);

        Assert.Equal(economy.RecruitPrice, soft.RecruitPrice);
        Assert.Equal(economy.FoodPrice, soft.FoodPrice);
        Assert.Equal(economy.MedicinePrice, soft.MedicinePrice);
    }

    /// <summary>The chosen tier survives the save — and is re-derived from the code, not the file.</summary>
    [Fact]
    public void TheTierSurvivesASaveAndLoad()
    {
        DojoState before = NewGame.Create(seed: 12, tier: DifficultyTier.Legend);
        DojoState after = DojoSaveFile.Load(DojoSaveFile.Write(before)).State!;

        Assert.Equal(DifficultyTier.Legend, after.Difficulty);
        Assert.Equal(
            before.Encounters.Tuning.MaxPower,
            after.Encounters.Tuning.MaxPower,
            9);
        Assert.Equal(
            before.Economy.VictoryGoldPerEnemyHealth,
            after.Economy.VictoryGoldPerEnemyHealth,
            9);
    }
}
