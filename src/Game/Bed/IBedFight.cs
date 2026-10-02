namespace Domina.Game.Bed;

/// <summary>A fight the bed can step: reset on a seed, ticked to its end, written down.</summary>
/// <remarks>
/// Both lanes drive a fight through this and nothing else — the manual lane ticks it to the end in one
/// go, the frame lane once a physics frame — so a fight cannot behave differently in the two lanes
/// through any door but the engine's.
/// </remarks>
internal interface IBedFight
{
    bool Finished { get; }

    /// <summary>Starts the fight of <paramref name="seed"/>, the previous one forgotten.</summary>
    void Reset(ulong seed);

    /// <summary>One tick.</summary>
    void Step();

    /// <summary>The finished fight, fingerprinted.</summary>
    FightRecord Record(ulong seed);
}

/// <summary>A finished fight as the bed writes it down.</summary>
/// <param name="Outcome">The stand-in's winning side (-1 stalled), or the resolver's <c>BattleOutcome</c>.</param>
/// <param name="Blows">Blows struck (the stand-in) or attacks made (the resolver).</param>
internal readonly record struct FightRecord(ulong Seed, int Outcome, int Ticks, ulong Digest, int Blows, int Hits);
