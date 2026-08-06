using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Sim;

/// <summary>Toplu simülasyonda koşturulan adlandırılmış eşleşme.</summary>
/// <param name="Name">Komut satırında verilen ad.</param>
/// <param name="Description">Listede görünen açıklama.</param>
/// <param name="Build">Eşleşmenin kadrosunu kuran fabrika.</param>
internal sealed record Scenario(string Name, string Description, Func<BattleSetup> Build);

/// <summary>
/// Dengeye bakılacak standart eşleşmeler.
/// </summary>
/// <remarks>
/// Denge çalışması "şu senaryoda ölüm oranı ne" sorusuyla ilerler; senaryolar
/// kodda sabit durur ki iki farklı ölçüm aynı kadroyu karşılaştırsın. Sayılar
/// tahmini başlangıç noktalarıdır (bkz. <see cref="CombatTuning"/>).
/// </remarks>
internal static class Scenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("duel", "acemi vs kappa (1v1)", Duel),
        new("3v3", "dojo takımı vs yokai takımı (3v3)", ThreeVsThree),
        new("veteran", "donanımlı veteran vs oni (1v1)", Veteran),
        new("ambush", "veteran pusuya düşüyor (1v3)", Ambush),
    ];

    public static Scenario? Find(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    private static BattleSetup Duel() => new(
        [
            new Warrior(new WarriorId(1), "Acemi", WarriorStats.Recruit(), Weapon.Katana()),
        ],
        [
            Yokai(101, "Kappa", health: 85, aggression: 60, defense: 15, evasion: 30, strength: 35),
        ]);

    private static BattleSetup ThreeVsThree() => new(
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
            Yokai(101, "Oni", health: 150, aggression: 55, defense: 30, evasion: 15, strength: 60, weapon: Weapon.Tetsubo()),
            Yokai(102, "Kappa", health: 85, aggression: 65, defense: 15, evasion: 35, strength: 35),
            Yokai(103, "Tengu", health: 90, aggression: 70, defense: 10, evasion: 50, strength: 40),
        ]);

    private static BattleSetup Veteran() => new(
        [
            new Warrior(
                new WarriorId(1),
                "Veteran",
                WarriorStats.Recruit() with
                {
                    MaxHealth = 130,
                    Aggression = 60,
                    Defense = 55,
                    Evasion = 50,
                    Strength = 65,
                    Accuracy = 72,
                },
                Weapon.Nodachi(),
                Armor.Heavy()),
        ],
        [
            Yokai(101, "Oni", health: 150, aggression: 55, defense: 30, evasion: 15, strength: 60, weapon: Weapon.Tetsubo()),
        ]);

    private static BattleSetup Ambush()
    {
        BattleSetup veteran = Veteran();
        return veteran with
        {
            EnemySide =
            [
                Yokai(101, "Kappa", health: 85, aggression: 65, defense: 15, evasion: 35, strength: 35),
                Yokai(102, "Kappa", health: 85, aggression: 65, defense: 15, evasion: 35, strength: 35),
                Yokai(103, "Tengu", health: 90, aggression: 70, defense: 10, evasion: 50, strength: 40),
            ],
        };
    }

    private static Warrior Yokai(
        int id,
        string name,
        double health,
        double aggression,
        double defense,
        double evasion,
        double strength,
        double accuracy = 58,
        Weapon? weapon = null) =>
        new(
            new WarriorId(id),
            name,
            new WarriorStats(health, aggression, defense, evasion, strength, accuracy, MaxStamina: 100),
            weapon ?? Weapon.Katana());
}
