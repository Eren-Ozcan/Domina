namespace Domina.Core.Rng;

/// <summary>
/// Seed'li, deterministik rastgelelik kaynağı (xoshiro256** + splitmix64 tohumlama).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Random"/> KASITLI olarak kullanılmıyor: algoritması .NET
/// sürümleri arasında değişebilir, dolayısıyla "aynı seed = aynı dövüş" garantisi
/// motor/çalışma zamanı yükseltmelerinde bozulur. Buradaki algoritma sabittir, yani
/// bir seed bugün ne üretiyorsa yıllar sonra da aynısını üretir.
/// </para>
/// <para>Thread-safe DEĞİLDİR. Her dövüş kendi örneğini kullanır.</para>
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

        // splitmix64 ile durumu doldur — kötü/az bitli seed'lerin (0, 1, 2...)
        // ilk üretimleri bozmasını engeller.
        ulong x = seed;
        _s0 = SplitMix64(ref x);
        _s1 = SplitMix64(ref x);
        _s2 = SplitMix64(ref x);
        _s3 = SplitMix64(ref x);
    }

    /// <summary>Bu akışı üreten seed. Log/replay için saklanır.</summary>
    public ulong Seed { get; }

    public double NextDouble()
    {
        // Üst 53 bit → [0,1) aralığında çift duyarlıklı değer.
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
