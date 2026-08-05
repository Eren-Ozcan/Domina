using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>Testlerde savaşçı üretmek için kısa yollar.</summary>
internal static class TestBuilders
{
    public static Warrior Warrior(
        int id,
        string? name = null,
        double health = 100,
        double aggression = 40,
        double defense = 0,
        double evasion = 0,
        double strength = 40,
        double accuracy = 60,
        double stamina = 100,
        Weapon? weapon = null,
        Armor? armor = null) =>
        new(
            new WarriorId(id),
            name ?? $"Savaşçı{id}",
            new WarriorStats(health, aggression, defense, evasion, strength, accuracy, stamina),
            weapon,
            armor);

    /// <summary>Ağır darbe eşiğini tek vuruşta aşan silah.</summary>
    public static Weapon Executioner() =>
        new("Test-Nodachi", WeaponClass.Cutting, 80, TwoHanded: false, AttackSeconds: 1.0);
}

/// <summary>
/// Sabit değer döndüren sahte rastgelelik kaynağı.
/// </summary>
/// <remarks>
/// <c>0.0</c> = her zar tutar (isabet eder, kaçınır, uzuv kopar).
/// <c>0.999</c> = hiçbiri tutmaz. Kaçınma gibi istenmeyen dalları kapatmak için
/// ilgili statı 0 vermek yeterlidir — <see cref="Chance"/> olasılık 0 iken
/// değere bakmadan false döner.
/// </remarks>
internal sealed class FixedRandom(double value) : IRandomSource
{
    public double NextDouble() => value;

    public int NextInt(int exclusiveMax) => (int)(value * exclusiveMax);

    public bool Chance(double probability)
    {
        if (probability <= 0.0)
        {
            return false;
        }

        return probability >= 1.0 || value < probability;
    }
}
