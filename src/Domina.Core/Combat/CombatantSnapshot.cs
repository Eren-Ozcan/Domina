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
/// <param name="Position">
/// Arena düzlemindeki yeri. Görselleştirme konumu <b>kendisi hesaplamaz</b>; savaşçılar
/// gerçekten yürüdüğü için tek doğruluk kaynağı çekirdektir.
/// </param>
/// <param name="Facing">Baktığı yön (+1 sağa, -1 sola).</param>
/// <param name="Speed">Bu tick'teki hızı — yürüme döngüsü buradan sürülür.</param>
/// <param name="TargetId">
/// Vurmaya çalıştığı düşman; hedefi yoksa <c>null</c>. Hedef seçimi rastgele olduğu için
/// görselleştirme bunu <b>kendi başına türetemez</b> — hamlenin nereye gideceği buradan
/// okunur (bkz. <c>ArenaChoreography.StrikePoint</c>).
/// </param>
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
    bool CanCancel,
    WarriorId? TargetId = null,
    ArenaPoint Position = default,
    int Facing = 1,
    double Speed = 0)
{
    public double HealthFraction => MaxHealth <= 0 ? 0 : Health / MaxHealth;

    public double StaminaFraction => MaxStamina <= 0 ? 0 : Stamina / MaxStamina;

    public bool IsActive => State is not (CombatState.Dead or CombatState.Escaped);
}
