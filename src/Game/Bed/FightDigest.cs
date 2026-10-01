namespace Domina.Game.Bed;

/// <summary>
/// A 64-bit FNV-1a fingerprint, fed one value at a time.
/// </summary>
/// <remarks>
/// Determinism is read off it: two runs of the same fights are the same only if every fingerprint
/// matches. Doubles and floats go in by their bits, not their printed form, so a drift in the last
/// place of a position is a different fight.
/// </remarks>
internal static class FightDigest
{
    public const ulong Empty = 14695981039346656037UL;

    private const ulong Prime = 1099511628211UL;

    public static ulong Mix(ulong hash, ulong value)
    {
        for (int i = 0; i < 8; i++)
        {
            hash ^= (value >> (i * 8)) & 0xFF;
            hash *= Prime;
        }

        return hash;
    }

    public static ulong Mix(ulong hash, long value) => Mix(hash, unchecked((ulong)value));

    public static ulong Mix(ulong hash, double value) => Mix(hash, BitConverter.DoubleToInt64Bits(value));

    public static ulong Mix(ulong hash, float value) => Mix(hash, (long)BitConverter.SingleToInt32Bits(value));
}
