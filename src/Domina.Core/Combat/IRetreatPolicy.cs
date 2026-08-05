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
public sealed class RetreatBelowHealth(double healthFraction) : IRetreatPolicy
{
    public double HealthFraction { get; } = healthFraction;

    public bool ShouldRetreat(in RetreatContext context) =>
        context.HealthFraction <= HealthFraction;
}
