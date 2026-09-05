using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// Anlık görüntü kurucusu.
/// </summary>
/// <remarks>
/// Sunum katmanı dövüşü değil <b>anlık görüntüyü</b> tüketiyor; testler de dövüş
/// kurmadan doğrudan anlık görüntü verebiliyor. Bir durumu (ölmek üzere olan savaşçı,
/// vuruşa kilitli savaşçı) gerçek dövüşle üretmek seed aramak demekti.
/// </remarks>
internal static class TestSnapshots
{
    public static CombatantSnapshot Of(
        int id,
        int team = Battle.PlayerTeam,
        CombatState state = CombatState.Idle,
        double progress = 0,
        bool retreatRequested = false,
        bool canCancel = true,
        double health = 100,
        int? targetId = null,
        ArenaPoint position = default,
        int facing = 1,
        double speed = 0,
        bool poisoned = false,
        bool disarmed = false) =>
        new(
            new WarriorId(id),
            team,
            state,
            health,
            Stamina: 100,
            MaxHealth: 100,
            MaxStamina: 100,
            retreatRequested,
            progress,
            canCancel,
            targetId is int t ? new WarriorId(t) : null,
            position,
            facing,
            speed,
            poisoned,
            disarmed);
}
