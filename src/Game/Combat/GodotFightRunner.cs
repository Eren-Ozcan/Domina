using Domina.Core.Combat;
using Domina.Core.Rng;

namespace Domina.Game.Combat;

/// <summary>
/// The resolver over the engine's ground behind the <see cref="IFightRunner"/> door: <see cref="Battle"/>
/// with its steps carried out on Godot's physics server.
/// </summary>
/// <remarks>
/// <para>
/// The second runner the door was cut for (docs/PLAN-ENGINE-RESOLUTION.md, Phase B). Whoever asks for a
/// fight at the door — the sim's duel bed, a campaign — gets the same <see cref="Battle"/> as from
/// <see cref="CoreFightRunner"/>, only standing on <see cref="GodotArenaSpace"/> instead of its own
/// kinematics. One space carries every fight, and every fight gets a fresh physics space inside it, so a
/// fight is a function of its seed alone.
/// </para>
/// <para>
/// It holds server objects, so it is disposed, and it answers one caller at a time on the thread that
/// owns the physics server.
/// </para>
/// </remarks>
internal sealed class GodotFightRunner(ArenaMotion motion = ArenaMotion.Contact) : IFightRunner, IDisposable
{
    private readonly GodotArenaSpace _space = new(CombatTuning.Default, motion);

    /// <summary>Fights fought.</summary>
    public long Fights { get; private set; }

    /// <summary>Motion tests asked of the server over every fight.</summary>
    public long MotionTests => _space.MotionTests;

    /// <summary>Places handed to the server over every fight.</summary>
    public long Handovers => _space.Handovers;

    public BattleResult Fight(BattleSetup setup, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(setup);

        _space.Fit(setup.Tuning);
        Fights++;
        return new Battle(setup, random, _space).Run();
    }

    public void Dispose() => _space.Dispose();
}
