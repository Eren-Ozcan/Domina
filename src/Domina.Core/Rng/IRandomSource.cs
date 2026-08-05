namespace Domina.Core.Rng;

/// <summary>
/// Çekirdeğin kullandığı TEK rastgelelik kaynağı.
/// </summary>
/// <remarks>
/// Kural: <c>Domina.Core</c> içinde <see cref="System.Random"/> doğrudan
/// kullanılmaz. Her rastgelelik bu arayüzden geçer ki dövüşler seed ile
/// tekrar üretilebilsin (bkz. CLAUDE.md → "Mimari kuralı").
/// </remarks>
public interface IRandomSource
{
    /// <summary>[0.0, 1.0) aralığında bir değer.</summary>
    double NextDouble();

    /// <summary>[0, exclusiveMax) aralığında bir tam sayı.</summary>
    int NextInt(int exclusiveMax);

    /// <summary><paramref name="probability"/> olasılıkla true (0.0 asla, 1.0 daima).</summary>
    bool Chance(double probability);
}
