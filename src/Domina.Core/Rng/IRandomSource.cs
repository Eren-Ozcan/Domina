namespace Domina.Core.Rng;

/// <summary>
/// The ONLY randomness source the core uses.
/// </summary>
/// <remarks>
/// The rule: <see cref="System.Random"/> is not used directly inside <c>Domina.Core</c>. All
/// randomness goes through this interface so that fights can be reproduced from a seed
/// (see CLAUDE.md → "Architecture rule").
/// </remarks>
public interface IRandomSource
{
    /// <summary>A value in the range [0.0, 1.0).</summary>
    double NextDouble();

    /// <summary>An integer in the range [0, exclusiveMax).</summary>
    int NextInt(int exclusiveMax);

    /// <summary>True with probability <paramref name="probability"/> (0.0 never, 1.0 always).</summary>
    bool Chance(double probability);
}
