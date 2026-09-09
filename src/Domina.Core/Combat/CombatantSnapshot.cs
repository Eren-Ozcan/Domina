using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// A warrior's current state during a fight — for the visualisation and the HUD.
/// </summary>
/// <remarks>
/// <para>
/// It is a read-only copy; the fight cannot be interfered with from here. The fight's only
/// intervention point is <see cref="Battle.CommandRetreat"/>.
/// </para>
/// <para>
/// <see cref="StateProgress"/> and <see cref="CanCancel"/> are for the visualisation: the animation is
/// driven by where in the state we are, and the "pull out" key shows the player in advance whether the
/// command will take effect immediately or be buffered.
/// </para>
/// </remarks>
/// <param name="StateProgress">How far the current state has progressed (0-1).</param>
/// <param name="CanCancel">Whether a flee command takes effect immediately right now or is buffered.</param>
/// <param name="Position">
/// His place on the arena plane. The visualisation <b>does not compute</b> the position itself; because
/// the warriors really walk, the single source of truth is the core.
/// </param>
/// <param name="Facing">The direction he faces (+1 right, -1 left).</param>
/// <param name="Speed">His speed this tick — the walk cycle is driven from it.</param>
/// <param name="Poisoned">
/// Is there still poison in his blood? It is not a <b>state</b> — a poisoned warrior walks, strikes and
/// evades; that is why it cannot be represented inside <see cref="CombatState"/> and is carried as a
/// separate flag.
/// </param>
/// <param name="Disarmed">
/// Is his weapon gone? Like poison this is not a <b>state</b> but a mark laid on top of the state: an
/// unarmed warrior keeps fighting — only with his fists.
/// </param>
/// <param name="DestroyedArmor">
/// The armour slots that broke. The visualisation strips the kit from here — a region whose plate is
/// gone must look bare on screen too, or the warrior is fighting in armour he only thinks he has
/// gibi durur (docs/GDD.md §12).
/// </param>
/// <param name="TargetId">
/// The enemy he is trying to strike; <c>null</c> if he has no target. Because target selection is
/// random, the visualisation <b>cannot derive it on its own</b> — where the move goes is read from
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
    double Speed = 0,
    bool Poisoned = false,
    bool Disarmed = false,
    HitLocationSet DestroyedArmor = HitLocationSet.None)
{
    public double HealthFraction => MaxHealth <= 0 ? 0 : Health / MaxHealth;

    public double StaminaFraction => MaxStamina <= 0 ? 0 : Stamina / MaxStamina;

    public bool IsActive => State is not (CombatState.Dead or CombatState.Escaped);
}
