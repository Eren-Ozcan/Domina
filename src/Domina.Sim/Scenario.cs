using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Sim;

/// <summary>A named matchup run in batch simulation.</summary>
/// <param name="Name">The name given on the command line.</param>
/// <param name="Description">The description shown in the list.</param>
/// <param name="Build">The factory that builds the matchup's roster.</param>
internal sealed record Scenario(string Name, string Description, Func<BattleSetup> Build);

/// <summary>
/// The standard matchups balance is examined on.
/// </summary>
/// <remarks>
/// Balance work proceeds with the question "what is the death rate in this scenario"; the scenarios
/// stay fixed in code so that two different measurements compare the same roster. The numbers are
/// estimated starting points (see <see cref="CombatTuning"/>).
/// </remarks>
internal static class Scenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("duel", "recruit vs kappa (1v1)", Duel),
        new("3v3", "dojo team vs enemy team (3v3)", ThreeVsThree),
        new("veteran", "equipped veteran vs oni (1v1)", Veteran),
        new("ambush", "a veteran is ambushed (1v3)", Ambush),
        new("blade", "blade master vs oni (1v1) — the cutting end of the blunt/cutting trade", Blade),
        new("club", "blunt master vs oni (1v1) — the same fight, only the weapon class differs", Club),
        new("katana", "one-handed master vs oni (1v1) — the control for sword catching", KatanaControl),
        new("jitte", "the same fight, only the weapon is a jitte — the catching end", JitteCatch),
        new("sai", "the same fight with a sai — better grip, lower damage", SaiCatch),
        new("jitte-heavy", "jitte vs an oni carrying a two-handed nodachi — catching's answer", JitteVsTwoHanded),
        new("katana-heavy", "the control for jitte-heavy: the same enemy, with a katana", KatanaVsTwoHanded),
        new("3v3-jitte", "3v3, the recruit carries a jitte instead of a katana — the bind's team value", ThreeVsThreeJitte),
        new("tanto", "short-knife master vs oni (1v1) — the control for poison", TantoControl),
        new("poison", "the same fight, the knife's blade poisoned — the poison end", PoisonedTanto),
        new("tanto-armored", "short knife vs armoured oni — the control for the armour wall", TantoVsArmored),
        new("poison-armored", "poisoned knife vs armoured oni — does poison get through the wall", PoisonedVsArmored),
        new("katana-armored", "katana vs armoured oni — the real alternative to poison", KatanaVsArmored),
        new("3v3-poison", "3v3, the tengu throws poisoned shuriken — when poison turns on the player", ThreeVsThreePoison),
        new("blade-armored", "blade master vs armoured oni — disarming's cutting end", BladeVsArmored),
        new("club-armored", "the same fight with a blunt weapon — steel striking plate", ClubVsArmored),
        new("spear-armored", "the same fight with a piercing weapon — where the third class sits", SpearVsArmored),
        new("jitte-armored", "jitte vs armoured oni — the catching implement's wall", JitteVsArmored),
        new("3v3-armored", "3v3, every enemy in full armour — disarming's team price", ThreeVsThreeArmored),
        new("patrol", "the daily patrol (3v3) — the ordinary encounter the economy is measured on", Patrol),
    ];

    public static Scenario? Find(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    private static BattleSetup Duel() => new(
        [
            new Warrior(new WarriorId(1), "Recruit", WarriorStats.Recruit(), Weapon.Katana()),
        ],
        [
            Enemy(101, "Collector", health: 85, aggression: 60, defense: 15, evasion: 30, strength: 35, speed: 55),
        ]);

    private static BattleSetup ThreeVsThree() => ThreeVsThreeWith(Weapon.Katana());

    /// <summary>
    /// The <b>ordinary</b> encounter the economy is measured on.
    /// </summary>
    /// <remarks>
    /// The other scenarios are balance probes: they are deliberately built heavy so the edge of a rule
    /// can be seen, and their deaths per warrior-fight sit in the 38-49% band. Such a fight cannot be
    /// fought <b>every day</b> — a roster cannot bear a funeral a day, and the price measured with that
    /// roster actually measures the price of warriors, not of armour or medicine. That is why the patrol
    /// stands apart: the same dojo roster against a weakened trio of enemies. The economy numbers (Open
    /// Decision #5) are measured on top of it.
    /// </remarks>
    private static BattleSetup Patrol() => new(
        [
            new Warrior(new WarriorId(1), "Recruit", WarriorStats.Recruit(), Weapon.Katana(), Armor.Light()),
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
            Enemy(101, "Collector", health: 90, aggression: 58, defense: 18, evasion: 28, strength: 36, speed: 52),
            Enemy(102, "Collector", health: 90, aggression: 58, defense: 18, evasion: 28, strength: 36, speed: 52),
            Enemy(103, "Cutthroat", health: 78, aggression: 62, defense: 14, evasion: 42, strength: 33, speed: 72, weapon: Weapon.Tanto()),
        ]);

    /// <summary>
    /// Where the bind duration (<c>CatchBindSeconds</c>) is really measured.
    /// </summary>
    /// <remarks>
    /// In 1v1 the bind does almost nothing: the window it opens falls into the gap the warrior was
    /// waiting in anyway, in his own attack cycle. The bind's promise is a <b>team</b> promise — the
    /// window the catcher opens is used not by the catcher but by <b>the men beside him</b>. To see that,
    /// the measurement is made with a party of three; the control is <c>3v3</c>, the only difference
    /// being the recruit's weapon.
    /// </remarks>
    private static BattleSetup ThreeVsThreeJitte() => ThreeVsThreeWith(Weapon.Jitte());

    /// <summary>
    /// The measurement where poison turns on <b>the player's side</b>: the tengu throws poisoned shuriken.
    /// </summary>
    /// <remarks>
    /// Poison keeps working on a warrior who is pulled out too — escape is not an antidote. Whether the
    /// rule breaks §5's ladder can only be seen here: the control is <c>3v3</c>, the only difference
    /// being the dose on the tengu's projectile.
    /// </remarks>
    private static BattleSetup ThreeVsThreePoison()
    {
        BattleSetup control = ThreeVsThree();
        control.EnemySide[2].Thrown = ThrownWeapon.PoisonedShuriken();

        return control;
    }

    private static BattleSetup ThreeVsThreeWith(Weapon recruitWeapon) => new(
        [
            new Warrior(new WarriorId(1), "Recruit", WarriorStats.Recruit(), recruitWeapon, Armor.Light()),
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
            Enemy(101, "Kabukimono", health: 150, aggression: 55, defense: 30, evasion: 15, strength: 60, speed: 25, weapon: Weapon.Tetsubo()),
            Enemy(102, "Collector", health: 85, aggression: 65, defense: 15, evasion: 35, strength: 35, speed: 55),
            Enemy(
                103,
                "Duelist",
                health: 90,
                aggression: 70,
                defense: 10,
                evasion: 50,
                strength: 40,
                speed: 85,
                thrown: ThrownWeapon.Shuriken()),
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
            Enemy(101, "Kabukimono", health: 150, aggression: 55, defense: 30, evasion: 15, strength: 60, speed: 25, weapon: Weapon.Tetsubo()),
        ]);

    /// <summary>
    /// The two ends of the blunt/cutting trade. The <b>only</b> difference between <see cref="Blade"/>
    /// and <see cref="Club"/> is the warrior's weapon — stats, kit and enemy are the same.
    /// </summary>
    /// <remarks>
    /// This is Open Decision #4-B's measurable question: does the blunt weapon collect through stun what
    /// it loses on the dismemberment multiplier? Because the nodachi (cutting 34/1.60) and the tetsubo
    /// (blunt 30/1.55) are the game's real two-handed choice, the comparison is made with those two
    /// rather than with an artificial laboratory weapon.
    /// </remarks>
    private static BattleSetup Blade() => Trade(Weapon.Nodachi());

    /// <inheritdoc cref="Blade"/>
    private static BattleSetup Club() => Trade(Weapon.Tetsubo());

    /// <summary>
    /// Sword catching's measurable question: does the catching implement pay for the damage it loses?
    /// </summary>
    /// <remarks>
    /// <para>
    /// The control is the katana (22/1.10), the experiments the jitte (14/1.00) and the sai (13/1.05).
    /// All three are <b>one-handed</b>: the number of hands was held fixed so the comparison isolates
    /// <b>catching</b> rather than the weapon class. It is built from the same body as <see cref="Trade"/>,
    /// so stats, kit and enemy are the same too.
    /// </para>
    /// <para>
    /// The enemy's weapon matters: the Kabukimono here carries the default katana. Carrying a tetsubo, its
    /// catchability would drop to 0.25 and the measurement would answer "does catching work against a
    /// blunt weapon" rather than "does catching work".
    /// </para>
    /// </remarks>
    private static BattleSetup KatanaControl() => Trade(Weapon.Katana());

    /// <inheritdoc cref="KatanaControl"/>
    private static BattleSetup JitteCatch() => Trade(Weapon.Jitte());

    /// <inheritdoc cref="KatanaControl"/>
    private static BattleSetup SaiCatch() => Trade(Weapon.Sai());

    /// <summary>
    /// The pair where catching's own answer is measured: the enemy carries a <b>two-handed</b> nodachi.
    /// </summary>
    /// <remarks>
    /// <c>CatchTwoHandedFactor</c> only shows up here. Left unmeasured, the jitte would be the right
    /// choice in every matchup and the leverage of an enemy who chose a heavy weapon would count for
    /// nothing. The control (<see cref="KatanaVsTwoHanded"/>) carries a katana against the same enemy —
    /// so that the difference comes only from catching.
    /// </remarks>
    private static BattleSetup JitteVsTwoHanded() => Trade(Weapon.Jitte(), Weapon.Nodachi());

    /// <inheritdoc cref="JitteVsTwoHanded"/>
    private static BattleSetup KatanaVsTwoHanded() => Trade(Weapon.Katana(), Weapon.Nodachi());

    /// <summary>
    /// Poison's measurable question: does the dose pay for the damage the weapon loses?
    /// </summary>
    /// <remarks>
    /// The control is a clean tantō (13/0.85), the experiment the same knife poisoned. The only
    /// difference is the dose on the blade — damage, speed, class and enemy are the same; it is built
    /// from the <see cref="Trade"/> body.
    private static BattleSetup TantoControl() => Trade(Weapon.Tanto());

    /// <inheritdoc cref="TantoControl"/>
    private static BattleSetup PoisonedTanto() => Trade(Weapon.PoisonedTanto());

    /// <summary>
    /// The pair where poison's real claim is measured: the enemy wears <b>full armour</b>.
    /// </summary>
    /// <remarks>
    /// Poison is the only route around armour's damage reduction. The short knife's 13 damage almost
    /// entirely melts in front of ō-yoroi; the dose does not melt. Left unmeasured, poison would be only
    /// "a bit more damage" and its return against armour would never show.
    /// </remarks>
    private static BattleSetup TantoVsArmored() => Trade(Weapon.Tanto(), enemyArmor: Armor.Heavy());

    /// <inheritdoc cref="TantoVsArmored"/>
    private static BattleSetup PoisonedVsArmored() =>
        Trade(Weapon.PoisonedTanto(), enemyArmor: Armor.Heavy());

    /// <summary>
    /// The <b>real</b> alternative against an armoured enemy: an ordinary sword.
    /// </summary>
    /// <remarks>
    /// The clean tantō is a weapon that already loses in front of armour; measuring poison only
    /// against it would give the misleading answer "poison works". Whether the poisoned knife really
    /// has a place shows only when it is compared with the normal option in the player's hand.
    /// </remarks>
    private static BattleSetup KatanaVsArmored() =>
        Trade(Weapon.Katana(), enemyArmor: Armor.Heavy());

    /// <summary>
    /// Disarming's measurable question: what does steel striking plate pay?
    /// </summary>
    /// <remarks>
    /// All three are <b>two-handed</b> and against an armoured enemy: nodachi (cutting, drop tendency
    /// 1.0), tetsubo (blunt, 0.2) and yari (piercing, 0.6). Because the rule only works on a strike
    /// landing on armour, the enemy has to wear full armour — with an unarmoured oni the measurement
    /// would be the same as the <c>blade</c>/<c>club</c> pair.
    /// </remarks>
    private static BattleSetup BladeVsArmored() =>
        Trade(Weapon.Nodachi(), enemyArmor: Armor.Heavy());

    /// <inheritdoc cref="BladeVsArmored"/>
    private static BattleSetup ClubVsArmored() =>
        Trade(Weapon.Tetsubo(), enemyArmor: Armor.Heavy());

    /// <inheritdoc cref="BladeVsArmored"/>
    private static BattleSetup SpearVsArmored() =>
        Trade(Weapon.Yari(), enemyArmor: Armor.Heavy());

    /// <summary>
    /// The catching implement's own wall: its damage melts in front of armour.
    /// </summary>
    /// <remarks>
    /// The disarm rule gives the jitte a clear advantage in front of a sword-carrying enemy; that must
    /// have a price, or the catching implement is the right choice in every matchup. The control is
    /// <see cref="KatanaVsArmored"/> — the same enemy, the same stats, the only difference the weapon.
    /// </remarks>
    private static BattleSetup JitteVsArmored() =>
        Trade(Weapon.Jitte(), enemyArmor: Armor.Heavy());

    /// <summary>
    /// Disarming's <b>team</b> price: the control is <c>3v3</c>, the only difference the enemy's armour.
    /// </summary>
    /// <remarks>
    /// In 1v1 a dropped weapon is one warrior's problem; in a crowd it shows which end of the roster
    /// rots — the senior carrying a blade, or the spearman.
    /// </remarks>
    private static BattleSetup ThreeVsThreeArmored()
    {
        BattleSetup control = ThreeVsThree();
        foreach (Warrior enemy in control.EnemySide)
        {
            enemy.Armor = Armor.Heavy();
        }

        return control;
    }

    private static BattleSetup Trade(
        Weapon weapon,
        Weapon? enemyWeapon = null,
        Armor? enemyArmor = null) => new(
        [
            new Warrior(
                new WarriorId(1),
                "Master",
                WarriorStats.Recruit() with
                {
                    MaxHealth = 130,
                    Aggression = 60,
                    Defense = 45,
                    Evasion = 40,
                    Strength = 60,
                    Accuracy = 68,
                },
                weapon,
                Armor.Medium()),
        ],
        [
            Enemy(
                101,
                "Kabukimono",
                health: 150,
                aggression: 55,
                defense: 30,
                evasion: 15,
                strength: 60,
                speed: 25,
                weapon: enemyWeapon,
                armor: enemyArmor),
        ]);

    private static BattleSetup Ambush()
    {
        BattleSetup veteran = Veteran();
        return veteran with
        {
            EnemySide =
            [
                Enemy(101, "Collector", health: 85, aggression: 65, defense: 15, evasion: 35, strength: 35, speed: 55),
                Enemy(102, "Collector", health: 85, aggression: 65, defense: 15, evasion: 35, strength: 35, speed: 55),
                Enemy(
                103,
                "Duelist",
                health: 90,
                aggression: 70,
                defense: 10,
                evasion: 50,
                strength: 40,
                speed: 85,
                thrown: ThrownWeapon.Shuriken()),
            ],
        };
    }

    /// <param name="speed">
    /// The enemies differ in speed on purpose: the Kabukimono is heavy and slow, the Duelist fast. This is what
    /// sets the price of fleeing — pulling out from a slow enemy before contact is close to free, while
    /// a fast one comes after you and catches up.
    /// </param>
    private static Warrior Enemy(
        int id,
        string name,
        double health,
        double aggression,
        double defense,
        double evasion,
        double strength,
        double accuracy = 58,
        double speed = 50,
        Weapon? weapon = null,
        ThrownWeapon? thrown = null,
        Armor? armor = null) =>
        new(
            new WarriorId(id),
            name,
            new WarriorStats(
                health, aggression, defense, evasion, strength, accuracy, MaxStamina: 100, Speed: speed),
            weapon ?? Weapon.Katana(),
            armor: armor,
            thrown: thrown);
}
