using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>
/// Arenanın geçici kadrosu.
/// </summary>
/// <remarks>
/// Faz 3'te gerçek roster meta katmandan gelecek; şimdilik görselleştirmeyi
/// çalıştırmaya yetecek kadarı burada duruyor. Motor katmanında değil burada olması,
/// aynı kadronun motor açmadan da (test, toplu simülasyon) kurulabilmesi içindir —
/// "aynı seed hem arenada hem <c>Domina.Sim</c>'de aynı dövüşü verir" iddiası ancak
/// iki taraf aynı kadroyu kurabildiğinde doğrulanabilir.
/// </remarks>
public static class DemoRoster
{
    /// <summary>Arenada oynanan dövüşün girdileri.</summary>
    /// <remarks>Oyuncu tuşuyla oynanıyor: politika yok.</remarks>
    public static BattleSetup Setup() => new(
        [
            new Warrior(new WarriorId(1), "Acemi", WarriorStats.Recruit(), Weapon.Katana(), Armor.Light()),
            new Warrior(
                new WarriorId(2),
                "Kıdemli",
                WarriorStats.Recruit() with { Strength = 55, Accuracy = 62, Defense = 45 },
                Weapon.Nodachi(),
                Armor.Medium()),
            new Warrior(
                new WarriorId(3),
                "Mızrakçı",
                WarriorStats.Recruit() with { Evasion = 50, Aggression = 50 },
                Weapon.Yari(),
                Armor.Light()),
        ],
        [
            Yokai(101, "Oni", 150, 55, 30, 15, 60, Weapon.Tetsubo()),
            Yokai(102, "Kappa", 85, 65, 15, 35, 35, Weapon.Katana()),
            Yokai(103, "Tengu", 90, 70, 10, 50, 40, Weapon.Katana()),
        ])
    {
        RetreatPolicy = null,
    };

    private static Warrior Yokai(
        int id,
        string name,
        double health,
        double aggression,
        double defense,
        double evasion,
        double strength,
        Weapon weapon) =>
        new(
            new WarriorId(id),
            name,
            new WarriorStats(health, aggression, defense, evasion, strength, Accuracy: 58, MaxStamina: 100),
            weapon);
}
