using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Campaign;

/// <summary>Zorluk eğrisinin ayarlanabilir sayıları.</summary>
/// <remarks>
/// Sayılar <b>kilitli değil</b>: GDD §10 yalnızca "zorluk tek eğri üzerinde artar, boss
/// yapısı kurulmuyor" diyor. Eğrinin dikliği ancak sefer dizisi ölçümüyle kapanır — ekonomi
/// turunun ortaya çıkardığı sınır burada geçerli: savaşçı-dövüş başına ölüm %20'yi aşınca
/// hiçbir fiyat dojo'yu ayakta tutmuyor (GDD §11).
/// </remarks>
public sealed record EncounterTuning
{
    /// <summary>1. günün gücü.</summary>
    public double StartingPower { get; init; } = 0.9;

    /// <summary>Her günün eklediği güç.</summary>
    public double PowerPerDay { get; init; } = 0.02;

    /// <summary>Eğrinin tavanı — sonsuza kadar sertleşmez.</summary>
    public double MaxPower { get; init; } = 3.0;

    /// <summary>
    /// Günün gücüne binen dalgalanma payı.
    /// </summary>
    /// <remarks>
    /// Eğri düz bir çizgi olsaydı her gün aynı teklif gelirdi ve "al ya da bırak" kararı
    /// kendiliğinden ortadan kalkardı: bırakmanın anlamı, yarının farklı olabilmesi.
    /// </remarks>
    public double DailyVariance { get; init; } = 0.25;

    /// <summary>Kadronun büyümeye başladığı güç.</summary>
    public double SecondEnemyAtPower { get; init; } = 1.1;

    /// <summary>Üçüncü düşmanın eklendiği güç.</summary>
    public double ThirdEnemyAtPower { get; init; } = 1.6;

    /// <summary>Tek savaşçı dayatan düello teklifinin çıkma olasılığı.</summary>
    /// <remarks>
    /// GDD §10: bazı karşılaşmalar tam bir sayı dayatır. Düello o kuralın en ucuz
    /// gösterimi — kadro değil, <b>bir</b> savaşçı seçtiriyor.
    /// </remarks>
    public double DuelChance { get; init; } = 0.12;

    public double HeavyThreshold { get; init; } = 1.5;

    public double DireThreshold { get; init; } = 2.2;

    public double RisingThreshold { get; init; } = 1.1;
}

/// <summary>Günün teklifini üretir.</summary>
/// <remarks>
/// <para>
/// Üretim <b>saf</b>: aynı seed ve aynı gün daima aynı teklifi verir. Böylece teklif
/// kayıtta saklanmak zorunda kalmaz — dosyada gün ve seed durur, teklif yüklerken yeniden
/// hesaplanır. Kaydı yükleyip teklifi beğenmeyince yeniden yüklemek de bir şey değiştirmez.
/// </para>
/// <para>
/// Motor bilmez, rastgeleliği <see cref="IRandomSource"/>'tan alır (CLAUDE.md → mimari kuralı).
/// </para>
/// </remarks>
public sealed class EncounterGenerator(EncounterTuning? tuning = null)
{
    /// <summary>Düşman kimlikleri kadroyla çakışmasın diye buradan başlar.</summary>
    /// <remarks>
    /// <see cref="Dojo.BattleAftermath"/> takım filtresiyle zaten korunuyor ama kimliklerin
    /// ayrı bir bantta durması, günlüğe bakan insanın hangi tarafı okuduğunu da ayırır.
    /// </remarks>
    public const int FirstEnemyId = 100_000;

    public EncounterTuning Tuning { get; } = tuning ?? new EncounterTuning();

    /// <summary>Verilen günün teklifi.</summary>
    public EncounterOffer Offer(int day, ulong campaignSeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);
        return Offer(day, new SeededRandom(Mix(campaignSeed, day)));
    }

    /// <summary>Akışı dışarıdan verilen teklif — ölçüm ve test için.</summary>
    public EncounterOffer Offer(int day, IRandomSource random)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);
        ArgumentNullException.ThrowIfNull(random);

        double power = PowerFor(day, random);

        bool duel = random.Chance(Tuning.DuelChance);
        int count = duel ? 1 : CountFor(power, random);

        List<Warrior> enemies = [];

        // Kalabalık gücü <b>bölmez</b>: üç düşman üç kat düşman demek. Bölseydi kalabalık
        // teklif, aynı tehdidi daha az canla taşırdı — yani aynı riski daha az ödülle
        // satardı, çünkü ödül düşman canına bağlı (GDD §11).
        double each = power;
        for (int i = 0; i < count; i++)
        {
            YokaiKind kind = Pick(power, random);
            enemies.Add(kind.Spawn(new WarriorId(FirstEnemyId + (day * 10) + i), each));
        }

        return new EncounterOffer(
            day,
            enemies,
            Band(power),
            Sighting(enemies, duel),
            duel ? 1 : null);
    }

    /// <summary>Günün ham gücü — eğri artı o güne düşen dalgalanma.</summary>
    public double PowerFor(int day, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        double curve = Tuning.StartingPower + ((day - 1) * Tuning.PowerPerDay);
        double swing = ((random.NextDouble() * 2) - 1) * Tuning.DailyVariance;

        return Math.Clamp(curve + swing, 0.3, Tuning.MaxPower);
    }

    private ThreatBand Band(double power) => power switch
    {
        var p when p >= Tuning.DireThreshold => ThreatBand.Dire,
        var p when p >= Tuning.HeavyThreshold => ThreatBand.Heavy,
        var p when p >= Tuning.RisingThreshold => ThreatBand.Rising,
        _ => ThreatBand.Faint,
    };

    private int CountFor(double power, IRandomSource random)
    {
        int most = 1;
        if (power >= Tuning.SecondEnemyAtPower)
        {
            most = 2;
        }

        if (power >= Tuning.ThirdEnemyAtPower)
        {
            most = 3;
        }

        // Kalabalık üst sınıra yapışmaz: aynı güç bazen tek ağır düşman, bazen üç zayıf
        // düşman demek. İki durum aynı dövüş değil — biri hedef seçimini, diğeri dayanmayı sınar.
        return most == 1 ? 1 : 1 + random.NextInt(most);
    }

    private static YokaiKind Pick(double power, IRandomSource random)
    {
        List<YokaiKind> pool = [.. Bestiary.AvailableAt(power)];
        if (pool.Count == 0)
        {
            return Bestiary.Kappa;
        }

        double total = pool.Sum(k => k.Weight);
        double roll = random.NextDouble() * total;

        foreach (YokaiKind kind in pool)
        {
            roll -= kind.Weight;
            if (roll <= 0)
            {
                return kind;
            }
        }

        return pool[^1];
    }

    /// <summary>Girmeden önce okunan kaba tanım — tür ve sayı, stat yok.</summary>
    private static string Sighting(IReadOnlyList<Warrior> enemies, bool duel)
    {
        string names = string.Join(
            " ve ",
            enemies.GroupBy(e => e.Name).Select(g => g.Count() == 1 ? g.Key : $"{g.Count()} {g.Key}"));

        return duel ? $"{names} düelloya çağırıyor" : names;
    }

    /// <summary>Seed ve günü tek bir akışa karıştırır.</summary>
    /// <remarks>
    /// Gün doğrudan seed'e eklenseydi, ardışık günlerin akışları birbirinin kaydırılmış
    /// hâli olurdu ve teklifler gözle görülür şekilde tekrarlardı.
    /// </remarks>
    private static ulong Mix(ulong seed, int day)
    {
        ulong x = seed ^ ((ulong)day * 0x9E3779B97F4A7C15);
        x ^= x >> 33;
        x *= 0xFF51AFD7ED558CCD;
        x ^= x >> 33;
        return x;
    }
}
