namespace Domina.Core.Rng;

/// <summary>
/// A seeded, deterministic randomness source (xoshiro256** + splitmix64 seeding).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Random"/> is DELIBERATELY not used: its algorithm can change between .NET
/// versions, so the guarantee "the same seed = the same fight" would break on engine/runtime upgrades.
/// The algorithm here is fixed, meaning whatever a seed produces today it will produce
/// years from now.
/// </para>
/// <para>It is NOT thread-safe. Every fight uses its own instance.</para>
/// </remarks>
public sealed class SeededRandom : IRandomSource
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public SeededRandom(ulong seed)
    {
        Seed = seed;

        // Fill the state with splitmix64 — it stops bad/low-bit seeds (0, 1, 2...) from spoiling the
        // first outputs.
        ulong x = seed;
        _s0 = SplitMix64(ref x);
        _s1 = SplitMix64(ref x);
        _s2 = SplitMix64(ref x);
        _s3 = SplitMix64(ref x);
    }

    /// <summary>The seed that produced this stream. Kept for logging/replay.</summary>
    public ulong Seed { get; }

    public double NextDouble()
    {
        // The top 53 bits → a double-precision value in the range [0,1).
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    public int NextInt(int exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);
        return (int)(NextDouble() * exclusiveMax);
    }

    public bool Chance(double probability)
    {
        if (probability <= 0.0)
        {
            return false;
        }

        if (probability >= 1.0)
        {
            return true;
        }

        return NextDouble() < probability;
    }

    private ulong NextUInt64()
    {
        ulong result = Rotl(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotl(_s3, 45);

        return result;
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        ulong z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
