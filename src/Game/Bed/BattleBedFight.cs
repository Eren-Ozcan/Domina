using Domina.Core.Combat;
using Domina.Core.Rng;

namespace Domina.Game.Bed;

/// <summary>
/// The real resolver on the bed: <see cref="Battle"/>, over its own kinematics or over the engine's space.
/// </summary>
/// <remarks>
/// <c>--fighter core</c> passes no space and the fight is the engine-free resolver's, exactly; <c>--fighter
/// engine</c> passes a <c>GodotArenaSpace</c> and the same resolver carries out its steps on the physics
/// server. Everything else about the two is the same code.
/// </remarks>
internal sealed class BattleBedFight(BattleSetup setup, IArenaSpace? space) : IBedFight
{
    private Battle? _battle;

    public bool Finished => _battle?.IsFinished ?? true;

    public void Reset(ulong seed) => _battle = new Battle(setup, new SeededRandom(seed), space);

    public void Step() => _battle!.Step();

    public FightRecord Record(ulong seed)
    {
        BattleResult result = _battle!.Result!;

        ulong digest = FightDigest.Mix(FightDigest.Mix(FightDigest.Empty, (long)result.Outcome), result.ElapsedSeconds);
        int attacks = 0;
        int hits = 0;
        foreach (WarriorBattleSummary s in result.Summaries)
        {
            digest = FightDigest.Mix(digest, s.Id.Value);
            digest = FightDigest.Mix(digest, (long)s.FinalState);
            digest = FightDigest.Mix(digest, s.HealthRemaining);
            digest = FightDigest.Mix(digest, s.AttacksMade);
            digest = FightDigest.Mix(digest, s.HitsLanded);
            digest = FightDigest.Mix(digest, s.DamageDealt);
            digest = FightDigest.Mix(digest, s.DamageTaken);
            digest = FightDigest.Mix(digest, (long)s.LostParts);
            attacks += s.AttacksMade;
            hits += s.HitsLanded;
        }

        int ticks = (int)Math.Round(result.ElapsedSeconds / TrivialFight.TickSeconds);
        return new FightRecord(seed, (int)result.Outcome, ticks, digest, attacks, hits);
    }
}
