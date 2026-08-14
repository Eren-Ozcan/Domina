using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>Testlerde savaşçı üretmek için kısa yollar.</summary>
internal static class TestBuilders
{
    /// <summary>
    /// Taraflar birbirinin menzilinde başlar.
    /// </summary>
    /// <remarks>
    /// Uzam geldiğinden beri savaşçılar varsayılan arenada birbirine <b>yürüyor</b>.
    /// Sonuç ağacını, kaçışı ya da hasarı sınayan testler yürüyüşü değil çarpışmayı
    /// ölçüyor; yaklaşmayı beklemek testi hem yavaşlatır hem kırılganlaştırır.
    /// Yaklaşmanın kendisi <c>MovementTests</c>'in konusu.
    /// </remarks>
    /// <remarks>
    /// <para>
    /// <see cref="CombatTuning.BaseDismembermentChance"/> burada <b>sabitlenir</b>:
    /// sonuç ağacını sınayan testler belirli zar değerleriyle çalışıyor ve o değerler
    /// eşiğe göre seçildi. Denge sayısı devralınsaydı her denge ayarında bu testler
    /// kırılırdı — oysa sınadıkları şey denge değil, kural.
    /// </para>
    /// </remarks>
    public static CombatTuning PointBlank { get; } = CombatTuning.Default with
    {
        StartOffsetX = 30,
        BaseDismembermentChance = 0.35,
    };

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
        double speed = 50,
        Weapon? weapon = null,
        Armor? armor = null,
        ThrownWeapon? thrown = null) =>
        new(
            new WarriorId(id),
            name ?? $"Savaşçı{id}",
            new WarriorStats(health, aggression, defense, evasion, strength, accuracy, stamina, speed),
            weapon,
            armor,
            thrown);

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
