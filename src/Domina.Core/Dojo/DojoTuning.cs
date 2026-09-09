namespace Domina.Core.Dojo;

/// <summary>The day loop's tunable numbers.</summary>
/// <remarks>
/// None of the numbers here are <b>locked</b>. Honour decay depends on Open Decision #8, recovery speed
/// and resource consumption on Open Decision #5; both will be settled by measurement. The defaults are
/// there to keep the loop working, they make no balance claim.
/// </remarks>
public sealed record DojoTuning
{
    /// <summary>A training day's stat return.</summary>
    public TrainingTuning Training { get; init; } = new();

    /// <summary>The infirmary days burnt in one day.</summary>
    public int NaturalRecoveryPerDay { get; init; } = 1;

    /// <summary>
    /// The days a warrior who comes back having lost nearly all his health spends in bed.
    /// </summary>
    public int RecoveryDaysAtFullDamage { get; init; } = 6;

    /// <summary>The infirmary days each lost limb adds.</summary>
    /// <remarks>
    /// Limb loss already carries a permanent penalty (GDD §7); the days here are the treatment time laid
    /// <b>on top of</b> the loss, not the penalty itself.
    /// </remarks>
    public int RecoveryDaysPerLostLimb { get; init; } = 5;

    /// <summary>
    /// The damage share counted as free — a scratch below this eats no day.
    /// </summary>
    /// <remarks>
    /// Without a threshold every fight would mean a day in the infirmary and the day loop's real
    /// decision ("an expedition today, or training") would disappear on its own.
    /// </remarks>
    public double RecoveryFreeDamageShare { get; init; } = 0.25;

    /// <summary>
    /// Honour's daily drift toward neutral (<see cref="Model.HonorScale.Starting"/>).
    /// </summary>
    /// <remarks>
    /// GDD §6's rationale: a troll attack must not be a permanent penalty, only <b>sustained</b>
    /// dishonour should lead to seppuku. Decay is what makes that continuity compulsory.
    /// </remarks>
    public double HonorDecayPerDay { get; init; } = 0.5;
}
