using Domina.Core.Model;

namespace Domina.Core.Campaign;

/// <summary>Bir yokai türünün ölçeklenebilir kalıbı.</summary>
/// <remarks>
/// <para>
/// Burada yalnızca <b>sayılar</b> var: her yokai'nin kendi dövüş davranışı (Açık Karar #3)
/// yazılmadı. GDD §4'ün notu şu: davranış farkı ayrı bir kod yolu değil, hedef seçimi
/// ağırlıklarının türe göre ayarlanmış hâli olacak. O ayar geldiğinde bu kalıba bir alan
/// eklenir — encounter üretimi değişmez.
/// </para>
/// <para>
/// Kalıp <b>güç</b> ile ölçeklenir: aynı kappa 1. günde de 40. günde de kappa'dır, ama
/// eğrinin ilerisinde daha canlı ve daha serttir. Zorluğun tek eğri üzerinde artması
/// (GDD §10) bunu gerektiriyor — ayrı bir "güçlü kappa" türü tutmak, aynı eğriyi iki
/// yerde tarif etmek olurdu.
/// </para>
/// </remarks>
/// <param name="Name">Görünen ad.</param>
/// <param name="Base">Güç 1.0'daki statlar.</param>
/// <param name="Weapon">Taşıdığı silah.</param>
/// <param name="Weight">Havuzdan seçilme ağırlığı.</param>
/// <param name="MinPower">Bu türün eğride ilk göründüğü güç.</param>
public sealed record YokaiKind(
    string Name,
    WarriorStats Base,
    Weapon Weapon,
    double Weight = 1,
    double MinPower = 0)
{
    /// <summary>
    /// Verilen güçte bir örnek üretir.
    /// </summary>
    /// <remarks>
    /// Can ve hasar güçle <b>doğrudan</b>, isabet/savunma/kaçınma <b>karekökle</b> ölçeklenir.
    /// Sebep ölçümden geliyor: isabet ve kaçınma doğrusal büyütüldüğünde eğri bir yerde
    /// aniden duvara dönüşüyor — düşman ıskalanmaz olurken oyuncu ıskalar hâle geliyor ve
    /// zorluk artışı iki kere sayılıyor. Can ve hasar ise oyuncunun kendi ekipmanıyla
    /// karşılayabildiği eksen.
    /// </remarks>
    public Warrior Spawn(WarriorId id, double power)
    {
        double linear = Math.Max(0.1, power);
        double soft = Math.Sqrt(linear);

        WarriorStats stats = Base with
        {
            MaxHealth = Base.MaxHealth * linear,
            Strength = Base.Strength * linear,
            Accuracy = Cap(Base.Accuracy * soft),
            Defense = Cap(Base.Defense * soft),
            Evasion = Cap(Base.Evasion * soft),
        };

        return new Warrior(id, Name, stats, Weapon);
    }

    /// <summary>Statlar 0-100 ölçeğinde; eğri büyüdükçe taşmamalı.</summary>
    private static double Cap(double value) => Math.Clamp(value, 0, 95);
}

/// <summary>Karşılaşmaların çekildiği yokai havuzu.</summary>
/// <remarks>
/// Liste <b>eksik</b>: GDD'nin bestiary adaylarından yalnızca sayı tarafı belli olanlar
/// burada. Nue ve boss adayları (Gashadokuro, Shuten-dōji, Yamata-no-Orochi) yok — GDD §10
/// boss yapısı kurmuyor, onlar eğrinin üst ucundaki güçlü düşmanlar olacak.
/// </remarks>
public static class Bestiary
{
    /// <summary>Küçük, çevik, sürü halinde.</summary>
    public static YokaiKind Kappa { get; } = new(
        "Kappa",
        new WarriorStats(MaxHealth: 70, Aggression: 58, Defense: 14, Evasion: 30, Strength: 30, Accuracy: 52, MaxStamina: 100, Speed: 55),
        Weapon.Katana(),
        Weight: 3);

    /// <summary>Hızlı, yüksek kaçınma, kısa bıçak.</summary>
    public static YokaiKind Kitsune { get; } = new(
        "Kitsune",
        new WarriorStats(MaxHealth: 62, Aggression: 62, Defense: 12, Evasion: 42, Strength: 28, Accuracy: 58, MaxStamina: 100, Speed: 72),
        Weapon.Tanto(),
        Weight: 2);

    /// <summary>Hızlı, hit-and-run; menzilli.</summary>
    public static YokaiKind Tengu { get; } = new(
        "Tengu",
        new WarriorStats(MaxHealth: 75, Aggression: 68, Defense: 12, Evasion: 45, Strength: 34, Accuracy: 60, MaxStamina: 100, Speed: 80),
        Weapon.Katana(),
        Weight: 2,
        MinPower: 1.2);

    /// <summary>Ağır, yüksek hasar, yavaş.</summary>
    public static YokaiKind Oni { get; } = new(
        "Oni",
        new WarriorStats(MaxHealth: 130, Aggression: 55, Defense: 28, Evasion: 14, Strength: 52, Accuracy: 55, MaxStamina: 100, Speed: 28),
        Weapon.Tetsubo(),
        Weight: 2,
        MinPower: 1.5);

    /// <summary>Uzun saplı; menzili kalabalıkta işe yarar.</summary>
    public static YokaiKind Jorogumo { get; } = new(
        "Jorōgumo",
        new WarriorStats(MaxHealth: 95, Aggression: 60, Defense: 20, Evasion: 30, Strength: 40, Accuracy: 58, MaxStamina: 100, Speed: 48),
        Weapon.Yari(),
        Weight: 1,
        MinPower: 1.8);

    public static IReadOnlyList<YokaiKind> All { get; } = [Kappa, Kitsune, Tengu, Oni, Jorogumo];

    /// <summary>Verilen güçte sahaya çıkabilecek türler.</summary>
    public static IEnumerable<YokaiKind> AvailableAt(double power) =>
        All.Where(k => power >= k.MinPower);
}
