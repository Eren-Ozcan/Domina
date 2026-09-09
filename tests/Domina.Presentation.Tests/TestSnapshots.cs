using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// The snapshot builder.
/// </summary>
/// <remarks>
/// The presentation layer consumes not the fight but <b>the snapshot</b>; the tests can therefore give a
/// snapshot directly without building a fight. Producing a state (a warrior about to die, a warrior
/// locked into a strike) with a real fight meant hunting for a seed.
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
