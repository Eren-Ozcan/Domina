using System.Globalization;

namespace Domina.Presentation;

/// <summary>Arenanın komut satırı argümanları.</summary>
/// <param name="Seed">İzlenecek dövüşün seed'i.</param>
/// <param name="SpeedMultiplier">Oynatma hızı; 1 = gerçek zaman.</param>
public readonly record struct ArenaArguments(long? Seed, double? SpeedMultiplier)
{
    /// <summary>
    /// <c>-- --seed 52 --speed 4</c> biçimindeki argümanları okur.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Determinizmin pratik karşılığı: toplu simülasyon ilginç bir dövüş bildirdiğinde
    /// ("52 numaralı seed'de savaşçı kolunu kaybediyor") o dövüş arenada birebir
    /// izlenebilir. Hata ayıklamanın ana yolu bu.
    /// </para>
    /// <para>
    /// Tanınmayan argümanlar sessizce atlanır: Godot kendi argümanlarını da aynı
    /// diziye koyabilir, arena onların ne olduğunu bilmek zorunda değil.
    /// </para>
    /// </remarks>
    public static ArenaArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        long? seed = null;
        double? speed = null;

        for (int i = 0; i < args.Count - 1; i++)
        {
            switch (args[i])
            {
                case "--seed" when long.TryParse(
                    args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedSeed):
                    seed = parsedSeed;
                    break;

                case "--speed" when double.TryParse(
                    args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSpeed)
                    && parsedSpeed > 0:
                    speed = parsedSpeed;
                    break;

                default:
                    break;
            }
        }

        return new ArenaArguments(seed, speed);
    }
}
