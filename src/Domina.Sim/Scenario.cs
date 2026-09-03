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
        new("blade", "kesici usta vs oni (1v1) — künt/kesici takasının kesici ucu", Blade),
        new("club", "künt usta vs oni (1v1) — aynı dövüş, yalnızca silah sınıfı farklı", Club),
        new("katana", "tek el usta vs oni (1v1) — kılıç yakalamanın kontrolü", KatanaControl),
        new("jitte", "aynı dövüş, yalnızca silah jitte — yakalamanın ucu", JitteCatch),
        new("sai", "aynı dövüş, sai ile — daha iyi kavrayış, daha düşük hasar", SaiCatch),
        new("jitte-heavy", "jitte vs çift el nodachi taşıyan oni — yakalamanın cevabı", JitteVsTwoHanded),
        new("katana-heavy", "jitte-heavy'nin kontrolü: aynı düşman, katana ile", KatanaVsTwoHanded),
        new("3v3-jitte", "3v3, acemi katana yerine jitte taşıyor — kilidin takım değeri", ThreeVsThreeJitte),
        new("tanto", "kısa bıçaklı usta vs oni (1v1) — zehrin kontrolü", TantoControl),
        new("poison", "aynı dövüş, bıçağın namlusu zehirli — zehrin ucu", PoisonedTanto),
        new("tanto-armored", "kısa bıçak vs zırhlı oni — zırh duvarının kontrolü", TantoVsArmored),
        new("poison-armored", "zehirli bıçak vs zırhlı oni — zehir duvarı aşıyor mu", PoisonedVsArmored),
        new("katana-armored", "katana vs zırhlı oni — zehrin karşısındaki gerçek seçenek", KatanaVsArmored),
        new("3v3-poison", "3v3, tengu zehirli shuriken atıyor — zehir oyuncunun üstüne dönünce", ThreeVsThreePoison),
    ];

    public static Scenario? Find(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    private static BattleSetup Duel() => new(
        [
            new Warrior(new WarriorId(1), "Acemi", WarriorStats.Recruit(), Weapon.Katana()),
        ],
        [
            Yokai(101, "Kappa", health: 85, aggression: 60, defense: 15, evasion: 30, strength: 35, speed: 55),
        ]);

    private static BattleSetup ThreeVsThree() => ThreeVsThreeWith(Weapon.Katana());

    /// <summary>
    /// Kilit süresinin (<c>CatchBindSeconds</c>) asıl ölçüldüğü yer.
    /// </summary>
    /// <remarks>
    /// 1v1'de kilit neredeyse hiçbir şey yapmaz: açılan pencere savaşçının zaten kendi
    /// saldırı döngüsünde beklediği boşluğa denk gelir. Kilidin vaadi <b>takım</b>
    /// vaadidir — yakalayanın açtığı pencereyi yakalayan değil, <b>yanındakiler</b>
    /// kullanır. Ölçüm bunu görebilmek için üç kişilik kadroda yapılır; kontrol
    /// <c>3v3</c>, tek fark acemi'nin silahı.
    /// </remarks>
    private static BattleSetup ThreeVsThreeJitte() => ThreeVsThreeWith(Weapon.Jitte());

    /// <summary>
    /// Zehrin <b>oyuncu tarafına</b> döndüğü ölçüm: tengu zehirli shuriken atar.
    /// </summary>
    /// <remarks>
    /// Zehir çekilen savaşçıda da işlemeye devam eder — kaçış bir panzehir değildir.
    /// Kuralın §5'in merdivenini bozup bozmadığı ancak burada görünür: kontrol
    /// <c>3v3</c>, tek fark tengu'nun merisinin ucundaki doz.
    /// </remarks>
    private static BattleSetup ThreeVsThreePoison()
    {
        BattleSetup control = ThreeVsThree();
        control.EnemySide[2].Thrown = ThrownWeapon.PoisonedShuriken();

        return control;
    }

    private static BattleSetup ThreeVsThreeWith(Weapon recruitWeapon) => new(
        [
            new Warrior(new WarriorId(1), "Acemi", WarriorStats.Recruit(), recruitWeapon, Armor.Light()),
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
            Yokai(101, "Oni", health: 150, aggression: 55, defense: 30, evasion: 15, strength: 60, speed: 25, weapon: Weapon.Tetsubo()),
            Yokai(102, "Kappa", health: 85, aggression: 65, defense: 15, evasion: 35, strength: 35, speed: 55),
            Yokai(
                103,
                "Tengu",
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
            Yokai(101, "Oni", health: 150, aggression: 55, defense: 30, evasion: 15, strength: 60, speed: 25, weapon: Weapon.Tetsubo()),
        ]);

    /// <summary>
    /// Künt/kesici takasının iki ucu. <see cref="Blade"/> ile <see cref="Club"/>
    /// arasındaki <b>tek</b> fark savaşçının silahıdır — statlar, kuşam, düşman aynı.
    /// </summary>
    /// <remarks>
    /// Açık Karar #4-B'nin ölçülebilir sorusu bu: künt silah, kaybettiği uzuv kopma
    /// çarpanının karşılığını sersemletmeden alıyor mu? Nodachi (kesici 34/1.60) ile
    /// Tetsubo (künt 30/1.55) oyunun gerçek çift el seçimi olduğu için karşılaştırma
    /// yapay bir laboratuvar silahıyla değil bu ikisiyle yapılır.
    /// </remarks>
    private static BattleSetup Blade() => Trade(Weapon.Nodachi());

    /// <inheritdoc cref="Blade"/>
    private static BattleSetup Club() => Trade(Weapon.Tetsubo());

    /// <summary>
    /// Kılıç yakalamanın ölçülebilir sorusu: yakalama aleti, kaybettiği hasarı ödüyor mu?
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kontrol katana (22/1.10), deney jitte (14/1.00) ve sai (13/1.05). Üçü de <b>tek
    /// el</b>: karşılaştırma silah sınıfını değil <b>yakalamayı</b> yalıtsın diye el
    /// sayısı sabit tutuldu. <see cref="Trade"/> ile aynı gövdeden kurulur, yani statlar,
    /// kuşam ve düşman da aynıdır.
    /// </para>
    /// <para>
    /// Düşmanın silahı önemlidir: Oni burada varsayılan katana'yı taşır. Tetsubo taşısaydı
    /// yakalanabilirlik 0.25'e inerdi ve ölçüm "yakalama işe yarıyor mu" sorusunu değil
    /// "künt silaha karşı işe yarıyor mu" sorusunu cevaplardı.
    /// </para>
    /// </remarks>
    private static BattleSetup KatanaControl() => Trade(Weapon.Katana());

    /// <inheritdoc cref="KatanaControl"/>
    private static BattleSetup JitteCatch() => Trade(Weapon.Jitte());

    /// <inheritdoc cref="KatanaControl"/>
    private static BattleSetup SaiCatch() => Trade(Weapon.Sai());

    /// <summary>
    /// Yakalamanın kendi cevabının ölçüldüğü çift: düşman <b>çift el</b> nodachi taşır.
    /// </summary>
    /// <remarks>
    /// <c>CatchTwoHandedFactor</c> yalnızca burada görünür. Ölçülmeden bırakılsaydı jitte
    /// her eşleşmede doğru seçim olur, ağır silah seçen düşmanın kaldıracı hiçbir şeye
    /// karşılık gelmezdi. Kontrol (<see cref="KatanaVsTwoHanded"/>) aynı düşmanla katana
    /// taşır — fark yalnızca yakalamadan gelsin diye.
    /// </remarks>
    private static BattleSetup JitteVsTwoHanded() => Trade(Weapon.Jitte(), Weapon.Nodachi());

    /// <inheritdoc cref="JitteVsTwoHanded"/>
    private static BattleSetup KatanaVsTwoHanded() => Trade(Weapon.Katana(), Weapon.Nodachi());

    /// <summary>
    /// Zehrin ölçülebilir sorusu: doz, silahın kaybettiği hasarı ödüyor mu?
    /// </summary>
    /// <remarks>
    /// Kontrol temiz tantō (13/0.85), deney aynı bıçağın zehirli hâli. Tek fark namludaki
    /// doz — hasar, hız, sınıf ve düşman aynı; <see cref="Trade"/> gövdesinden kurulur.
    /// </remarks>
    private static BattleSetup TantoControl() => Trade(Weapon.Tanto());

    /// <inheritdoc cref="TantoControl"/>
    private static BattleSetup PoisonedTanto() => Trade(Weapon.PoisonedTanto());

    /// <summary>
    /// Zehrin asıl iddiasının ölçüldüğü çift: düşman <b>tam kuşam</b> taşır.
    /// </summary>
    /// <remarks>
    /// Zehir, zırhın hasar azaltımının etrafından dolaşan tek yoldur. Kısa bıçağın 13
    /// hasarı ō-yoroi'nin önünde neredeyse tamamen erir; doz erimez. Ölçülmeden bırakılsa
    /// zehir yalnızca "biraz daha hasar" olurdu ve zırhın önündeki karşılığı hiç görünmezdi.
    /// </remarks>
    private static BattleSetup TantoVsArmored() => Trade(Weapon.Tanto(), enemyArmor: Armor.Heavy());

    /// <inheritdoc cref="TantoVsArmored"/>
    private static BattleSetup PoisonedVsArmored() =>
        Trade(Weapon.PoisonedTanto(), enemyArmor: Armor.Heavy());

    /// <summary>
    /// Zırhlı düşmanın karşısındaki <b>gerçek</b> alternatif: sıradan bir kılıç.
    /// </summary>
    /// <remarks>
    /// Temiz tantō, zırhın önünde zaten kaybeden bir silah; zehri yalnızca ona karşı
    /// ölçmek "zehir işe yarıyor" diye yanıltıcı bir cevap verirdi. Zehirli bıçağın
    /// gerçekten bir yere oturup oturmadığı, oyuncunun elindeki normal seçenekle
    /// karşılaştırılınca görünür.
    /// </remarks>
    private static BattleSetup KatanaVsArmored() =>
        Trade(Weapon.Katana(), enemyArmor: Armor.Heavy());

    private static BattleSetup Trade(
        Weapon weapon,
        Weapon? enemyWeapon = null,
        Armor? enemyArmor = null) => new(
        [
            new Warrior(
                new WarriorId(1),
                "Usta",
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
            Yokai(
                101,
                "Oni",
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
                Yokai(101, "Kappa", health: 85, aggression: 65, defense: 15, evasion: 35, strength: 35, speed: 55),
                Yokai(102, "Kappa", health: 85, aggression: 65, defense: 15, evasion: 35, strength: 35, speed: 55),
                Yokai(
                103,
                "Tengu",
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
    /// Yokai'ler hızda kasıtlı olarak ayrışır: Oni ağır ve yavaş, Tengu hızlı. Kaçmanın
    /// bedelini belirleyen şey budur — yavaş düşmandan temastan önce çekilmek bedelsize
    /// yakındır, hızlı olan peşinden gelip yetişir.
    /// </param>
    private static Warrior Yokai(
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
