using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// Bir savaşçının dövüş sırasındaki anlık hali — görselleştirme ve HUD için.
/// </summary>
/// <remarks>
/// Salt okunur bir kopyadır; buradan dövüşe müdahale edilemez. Dövüşün tek
/// müdahale noktası <see cref="Battle.CommandRetreat"/>'tir.
/// </remarks>
public readonly record struct CombatantSnapshot(
    WarriorId Id,
    int Team,
    CombatState State,
    double Health,
    double Stamina,
    double MaxHealth,
    double MaxStamina,
    bool RetreatRequested)
{
    public double HealthFraction => MaxHealth <= 0 ? 0 : Health / MaxHealth;

    public double StaminaFraction => MaxStamina <= 0 ? 0 : Stamina / MaxStamina;

    public bool IsActive => State is not (CombatState.Dead or CombatState.Escaped);
}
