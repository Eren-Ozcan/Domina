using System.Collections.ObjectModel;

namespace Domina.Core.Model;

/// <summary>
/// Dojo'daki bir savaşçının kalıcı hali.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Id"/> kalıcı ve benzersizdir; <see cref="Name"/> değildir.
/// Bir isim aynı anda yalnızca <b>bir canlı</b> savaşçıya ait olabilir, ama X ölünce
/// havuza döner ve ileride yeni bir X gelebilir (bkz. docs/GDD.md §6). Bu yüzden
/// eşleştirme her yerde Id üzerinden yapılır, isim üzerinden değil.
/// </para>
/// </remarks>
public sealed class Warrior
{
    private readonly List<Disability> _disabilities = [];

    public Warrior(
        WarriorId id,
        string name,
        WarriorStats baseStats,
        Weapon? weapon = null,
        Armor? armor = null,
        ThrownWeapon? thrown = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
        BaseStats = baseStats;
        Weapon = weapon ?? Weapon.Katana();
        Armor = armor ?? Armor.None();
        Thrown = thrown;
        Honor = HonorScale.Starting;
    }

    public WarriorId Id { get; }

    /// <summary>Chat'ten çekilmiş veya üretilmiş ad. Oyuncu her zaman değiştirebilir.</summary>
    public string Name { get; set; }

    /// <summary>Sakatlık uygulanmamış ham statlar.</summary>
    public WarriorStats BaseStats { get; set; }

    public Weapon Weapon { get; set; }

    public Armor Armor { get; set; }

    /// <summary>Fırlatma yuvası. <c>null</c> ise savaşçı menzilli saldıramaz.</summary>
    public ThrownWeapon? Thrown { get; set; }

    /// <summary>
    /// Fırlatma, kol kaybından sonra da mümkündür — tek elle atılır.
    /// </summary>
    /// <remarks>
    /// İki elli yakın dövüş silahını kullanamayan savaşçının elinde kalan tek gerçek
    /// tehdit budur (bkz. <see cref="UsableWeapon"/>).
    /// </remarks>
    public ThrownWeapon? UsableThrown => Thrown;

    /// <summary>0-100. Ekonomiyi ve seppuku eşiğini besler (bkz. docs/GDD.md §6).</summary>
    public double Honor { get; set; }

    public bool IsAlive { get; private set; } = true;

    /// <summary>Kalıcı sakatlıklar. Geri alınamaz.</summary>
    public IReadOnlyList<Disability> Disabilities => new ReadOnlyCollection<Disability>(_disabilities);

    /// <summary>Sakatlıkların uygulanmış hali — dövüşün kullandığı statlar.</summary>
    public WarriorStats EffectiveStats
    {
        get
        {
            WarriorStats s = BaseStats;
            foreach (Disability d in _disabilities)
            {
                s = s with
                {
                    Strength = s.Strength * d.StrengthMultiplier,
                    Evasion = s.Evasion * d.EvasionMultiplier,
                    Accuracy = s.Accuracy * d.AccuracyMultiplier,
                    Speed = s.Speed * d.SpeedMultiplier,
                };
            }

            return s;
        }
    }

    /// <summary>
    /// Kolunu kaybeden savaşçı iki elli silahını kullanamaz; eline ne verirsen ver
    /// pratikte yumrukla dövüşür. Ekipman ekranının bunu göstermesi gerekir.
    /// </summary>
    public Weapon UsableWeapon =>
        Weapon.TwoHanded && _disabilities.Exists(d => d.BlocksTwoHandedWeapons)
            ? Weapon.Fists()
            : Weapon;

    public bool HasDisability(BodyPart part) => _disabilities.Exists(d => d.Part == part);

    /// <summary>Kalıcı sakatlık ekler. Aynı uzuv iki kez kaybedilemez.</summary>
    public bool AddDisability(BodyPart part)
    {
        if (HasDisability(part))
        {
            return false;
        }

        _disabilities.Add(new Disability(part));
        return true;
    }

    /// <summary>Kalıcı ölüm. Geri dönüşü yoktur.</summary>
    public void Kill() => IsAlive = false;
}

/// <summary>Savaşçının kalıcı, benzersiz kimliği.</summary>
public readonly record struct WarriorId(int Value)
{
    public override string ToString() => $"W{Value}";
}

/// <summary>Dövüşü besleyen statlar.</summary>
/// <param name="MaxHealth">Azami can.</param>
/// <param name="Aggression">Saldırı sıklığı/agresiflik (0-100).</param>
/// <param name="Defense">Alınan hasarı azaltır (0-100).</param>
/// <param name="Evasion">Kaçınma denemesi şansı, stamina harcar (0-100).</param>
/// <param name="Strength">Hasar çarpanını besler (0-100).</param>
/// <param name="Accuracy">İsabet şansı (0-100).</param>
/// <param name="MaxStamina">Azami stamina.</param>
/// <param name="Speed">
/// Yürüme hızı (0-100). Yaklaşmayı, kuşatmayı ve <b>kaçabilmeyi</b> belirler.
/// </param>
/// <remarks>
/// <see cref="Speed"/> geç eklendi ve varsayılanı 50'dir: hız tek bir sabitken kovalayan
/// ile kaçan aynı hızda gidiyordu, yani <b>kaçış her zaman başarılıydı</b>. Sırtını
/// dönüp koşan savaşçıya kimse yetişemiyordu ve temastan önce basılan "Kaç" tuşu
/// %100 bedelsiz çıkış veriyordu (ölçüldü, 20.000 dövüş).
/// </remarks>
public readonly record struct WarriorStats(
    double MaxHealth,
    double Aggression,
    double Defense,
    double Evasion,
    double Strength,
    double Accuracy,
    double MaxStamina,
    double Speed = 50)
{
    /// <summary>Yeni bir acemi için taban.</summary>
    public static WarriorStats Recruit() => new(
        MaxHealth: 100,
        Aggression: 40,
        Defense: 35,
        Evasion: 35,
        Strength: 40,
        Accuracy: 55,
        MaxStamina: 100,
        Speed: 50);
}

/// <summary>Onur ölçeğinin sabitleri.</summary>
public static class HonorScale
{
    public const double Min = 0;
    public const double Max = 100;

    /// <summary>Yeni savaşçı nötr başlar.</summary>
    public const double Starting = 50;

    public static double Clamp(double value) => Math.Clamp(value, Min, Max);
}
