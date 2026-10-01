using Domina.Core.Rng;

namespace Domina.Core.Combat;

/// <summary>
/// The thing that fights a fight: a setup and a random stream in, the result out.
/// </summary>
/// <remarks>
/// <para>
/// This is the door the fight is being moved through (docs/PLAN-ENGINE-RESOLUTION.md, Open Decision #13).
/// The campaign, the stall report and the sim ask for a fight here instead of building a
/// <see cref="Battle"/> themselves, so the resolver behind the door can change without any of them
/// changing. Today there is one resolver, <see cref="CoreFightRunner"/>; the engine's fight will be the
/// second, and the bed runs the two side by side on the same seeds.
/// </para>
/// <para>
/// The call is <b>synchronous</b>: it returns when the fight is over. That is what the dojo layer and
/// the sim are written against, and an engine fight behind it is the runner's problem to make
/// synchronous (a worker thread waiting on the engine's lane), not theirs.
/// </para>
/// <para>
/// A watched fight does not come through here. The arena steps a live <see cref="Battle"/> because the
/// player's key has to land inside it while it runs; this door is for the fights nobody watches.
/// </para>
/// </remarks>
public interface IFightRunner
{
    /// <summary>Fights the fight to its end.</summary>
    /// <remarks>
    /// The event stream comes back on <see cref="BattleResult.Events"/> when
    /// <see cref="BattleSetup.CollectEvents"/> asks for it.
    /// </remarks>
    BattleResult Fight(BattleSetup setup, IRandomSource random);
}

/// <summary>
/// The engine-free resolver, <see cref="Battle"/>, behind the <see cref="IFightRunner"/> door.
/// </summary>
/// <remarks>
/// It holds no state, so one instance serves every caller; it is the default wherever a runner is
/// optional, which is why passing nothing behaves exactly as the code did before the door existed.
/// </remarks>
public sealed class CoreFightRunner : IFightRunner
{
    public static CoreFightRunner Instance { get; } = new();

    private CoreFightRunner()
    {
    }

    public BattleResult Fight(BattleSetup setup, IRandomSource random) => new Battle(setup, random).Run();
}
