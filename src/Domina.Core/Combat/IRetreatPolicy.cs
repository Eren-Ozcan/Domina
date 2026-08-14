using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// Kaçış kararını veren şey.
/// </summary>
/// <remarks>
/// Oyunda bu politika yoktur — karar oyuncunun tuşundan gelir
/// (<see cref="Battle.CommandRetreat"/>). Politika, <b>toplu simülasyonda</b>
/// oyuncunun yerine geçmek içindir: "hiç çekilmeyen oyuncu" ile "canı %30'a
/// düşünce çeken oyuncu" arasındaki ölüm oranı farkını ölçmeyi sağlar.
/// </remarks>
public interface IRetreatPolicy
{
    bool ShouldRetreat(in RetreatContext context);
}

/// <summary>Politikanın karar verirken gördüğü anlık durum.</summary>
/// <param name="WarriorId">Karar verilen savaşçı.</param>
/// <param name="HealthFraction">Kalan canın azami cana oranı (0-1).</param>
/// <param name="StaminaFraction">Kalan staminanın azami staminaya oranı (0-1).</param>
/// <param name="ElapsedSeconds">Dövüşün başından beri geçen süre.</param>
/// <param name="AlliesStanding">Aynı taraftaki ayakta kalan savaşçı sayısı (kendisi dahil).</param>
/// <param name="EnemiesStanding">Karşı taraftaki ayakta kalan savaşçı sayısı.</param>
public readonly record struct RetreatContext(
    WarriorId WarriorId,
    double HealthFraction,
    double StaminaFraction,
    double ElapsedSeconds,
    int AlliesStanding,
    int EnemiesStanding);

/// <summary>Hiç çekilmeyen oyuncu — dikkatsizlik durumunun tabanı.</summary>
public sealed class NeverRetreat : IRetreatPolicy
{
    public static NeverRetreat Instance { get; } = new();

    public bool ShouldRetreat(in RetreatContext context) => false;
}

/// <summary>Can belirli bir orana düşünce çeken oyuncu.</summary>
/// <remarks>
/// Kaba bir model: komut <b>ekip bazlı</b> olduğu için tek bir savaşçının canı düşünce
/// tüm ekip sahayı terk eder. 3v3'te bu, dövüşlerin ezici çoğunluğunun terk edilmesi
/// demek — gerçek bir oyuncunun oynayışı değil. Ölçüm tabanı olarak kullanılır, oyuncu
/// modeli olarak <see cref="RetreatWhenLosing"/> daha gerçekçidir.
/// </remarks>
public sealed class RetreatBelowHealth(double healthFraction) : IRetreatPolicy
{
    public double HealthFraction { get; } = healthFraction;

    public bool ShouldRetreat(in RetreatContext context) =>
        context.HealthFraction <= HealthFraction;
}

/// <summary>
/// Belirli bir saniyede, olan bitene bakmadan çeken oyuncu.
/// </summary>
/// <remarks>
/// Cana bakan politikalar kaçışın <b>bedelsiz</b> ucunu ölçemez: can düşmüşse zaten yara
/// alınmıştır, yani "kimse yara almadan çekildi" durumu kurgu gereği hiç oluşmaz. Oysa
/// gerçek oyuncu eşleşmeyi görüp daha ilk saniyede basabilir. Bu politika o ucu ölçmek
/// içindir.
/// </remarks>
public sealed class RetreatAtSecond(double seconds) : IRetreatPolicy
{
    public double Seconds { get; } = seconds;

    public bool ShouldRetreat(in RetreatContext context) => context.ElapsedSeconds >= Seconds;
}

/// <summary>
/// Dövüş <b>döndüğünde</b> çeken oyuncu: sayıca geride kalmış ve canı da eşiğin
/// altındaysa çekilir.
/// </summary>
/// <remarks>
/// Gerçek oyuncu tek bir yaraya bakıp seferi iptal etmez; dövüşün gidişatına bakar.
/// Yalnızca cana bakan politika 3v3'te dövüşlerin %80'inden fazlasını terk ettiriyor
/// ve ölçümü anlamsızlaştırıyor — zafer oranı düşük çıkıyor ama sebebi denge değil,
/// politikanın kendisi.
/// </remarks>
public sealed class RetreatWhenLosing(double healthFraction) : IRetreatPolicy
{
    public double HealthFraction { get; } = healthFraction;

    public bool ShouldRetreat(in RetreatContext context) =>
        context.AlliesStanding < context.EnemiesStanding
        && context.HealthFraction <= HealthFraction;
}
