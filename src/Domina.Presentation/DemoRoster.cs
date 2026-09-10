using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>
/// The arena's temporary roster.
/// </summary>
/// <remarks>
/// In phase 3 the real roster will come from the meta layer; for now just enough to run the
/// visualisation stands here. It is here rather than in the engine layer so the same roster can be
/// built without opening the engine too (tests, batch simulation) — the claim "the same seed gives the
/// same fight both in the arena and in <c>Domina.Sim</c>" can only be verified when both sides can
/// build the same roster.
/// </remarks>
public static class DemoRoster
{
    /// <summary>The inputs of the fight played in the arena.</summary>
    /// <remarks>Played with the player's key: no policy.</remarks>
    public static BattleSetup Setup() => new(
        [
            new Warrior(new WarriorId(1), "Acemi", WarriorStats.Recruit(), Weapon.Katana(), Armor.Light()),
            new Warrior(
                new WarriorId(2),
                "Senior",
                WarriorStats.Recruit() with { Strength = 55, Accuracy = 62, Defense = 45 },
                Weapon.Nodachi(),
                Armor.Medium()),
            new Warrior(
                new WarriorId(3),
                "Spearman",
                WarriorStats.Recruit() with { Evasion = 50, Aggression = 50 },
                Weapon.Yari(),
                Armor.Light()),
        ],
        [
            Enemy(101, "Kabukimono", 150, 55, 30, 15, 60, Weapon.Tetsubo()),
            Enemy(102, "Collector", 85, 65, 15, 35, 35, Weapon.Katana()),
            Enemy(103, "Duelist", 90, 70, 10, 50, 40, Weapon.Katana()),
        ])
    {
        RetreatPolicy = null,
    };

    /// <summary>The temporary dojo that drives the roster screen.</summary>
    /// <remarks>
    /// It carries all four states the screen has to show at once: ready, training, in the infirmary and
    /// dead. The dojo of the game being played does not come from here — that is either loaded from a
    /// save or built with <c>NewGame.Create</c>; this is only for screens opened on their own.
    /// </remarks>
    public static DojoState Dojo()
    {
        DojoState dojo = new() { Resources = new Resources(Gold: 600, Food: 20, Water: 20, Medicine: 2) };

        dojo.Roster.Recruit("Acemi", weapon: Weapon.Katana(), armor: Armor.Light());

        RosterEntry senior = dojo.Roster.Recruit(
            "Senior",
            WarriorStats.Recruit() with { Strength = 55, Accuracy = 62, Defense = 45 },
            Weapon.Nodachi(),
            Armor.Medium());
        senior.Train(Drill.Guard);

        RosterEntry wounded = dojo.Roster.Recruit("Spearman", weapon: Weapon.Yari(), armor: Armor.Light());
        wounded.Warrior.AddDisability(BodyPart.OffArm);
        wounded.Injure(4);

        RosterEntry fallen = dojo.Roster.Recruit("Rahip");
        dojo.Roster.Kill(fallen.Id);

        return dojo;
    }

    private static Warrior Enemy(
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
