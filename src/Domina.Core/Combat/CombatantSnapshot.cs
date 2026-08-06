using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// Bir savaşçının dövüş sırasındaki anlık hali — görselleştirme ve HUD için.
/// </summary>
/// <remarks>
/// <para>
/// Salt okunur bir kopyadır; buradan dövüşe müdahale edilemez. Dövüşün tek
/// müdahale noktası <see cref="Battle.CommandRetreat"/>'tir.
/// </para>
/// <para>
/// <see cref="StateProgress"/> ve <see cref="CanCancel"/> görselleştirme içindir:
/// animasyon durumun neresinde olunduğuna göre sürülür ve "çek" tuşu, komutun anında
/// mı işleyeceğini yoksa buffer'lanacağını mı oyuncuya önceden gösterir.
/// </para>
/// </remarks>
/// <param name="StateProgress">Mevcut durumun tamamlanma oranı (0-1).</param>
/// <param name="CanCancel">Kaçış komutu şu an anında işler mi, yoksa buffer'lanır mı.</param>
public readonly record struct CombatantSnapshot(
    WarriorId Id,
    int Team,
    CombatState State,
    double Health,
    double Stamina,
    double MaxHealth,
    double MaxStamina,
    bool RetreatRequested,
    double StateProgress,
    bool CanCancel)
{
    public double HealthFraction => MaxHealth <= 0 ? 0 : Health / MaxHealth;

    public double StaminaFraction => MaxStamina <= 0 ? 0 : Stamina / MaxStamina;

    public bool IsActive => State is not (CombatState.Dead or CombatState.Escaped);
}
