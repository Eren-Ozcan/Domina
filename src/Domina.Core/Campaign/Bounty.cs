using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Campaign;

/// <summary>Kelle avı sözleşmelerinin ayarlanabilir sayıları.</summary>
/// <remarks>
/// Sayılar <b>kilitli değil</b>. Ölçümün sorusu belli: sözleşme, günlük teklifin daha
/// pahalı bir kopyası olmamalı — riski de ödülü de belirgin şekilde farklı olmalı ki
/// "bugün ucuz işe mi gideyim, üç gün sonraki büyük iş için taze mi kalayım" diye bir
/// soru doğsun.
/// </remarks>
public sealed record BountyTuning
{
    /// <summary>Kaç günde bir yeni sözleşme asılır.</summary>
    /// <remarks>
    /// Her gün yeni sözleşme asılsaydı beğenilmeyen hedef bir gün beklenerek değişirdi ve
    /// sözleşme günlük teklifin ikinci bir kopyası olurdu.
    /// </remarks>
    public int PostingDays { get; init; } = 4;

    /// <summary>Sözleşmenin asıldığı günden itibaren kaç gün açık kaldığı.</summary>
    /// <remarks>
    /// Süre, kampanyaya bugüne kadar olmayan tek şeyi verir: <b>planlanabilir gelecek</b>.
    /// Süresiz sözleşme bir karar değil bir depo olurdu — oyuncu onu kadro mükemmel olana
    /// kadar bekletirdi.
    /// </remarks>
    public int OpenDays { get; init; } = 3;

    /// <summary>Hedefin, aynı günün sıradan teklifine göre gücü.</summary>
    public double PowerMultiplier { get; init; } = 1.8;

    /// <summary>Ödülün, aynı canlı sıradan bir düşmana göre katı.</summary>
    /// <remarks>
    /// Birden büyük olmak zorunda: hedef tek ve güçlü olduğu için ekip sayıca üstün
    /// giremez, yani aynı can daha çok risk demek. Çarpan 1'de kalsaydı sözleşme
    /// matematiksel olarak her zaman kötü bir anlaşma olurdu.
    /// </remarks>
    public double RewardMultiplier { get; init; } = 1.6;

    /// <summary>Sözleşmeyi tamamlayan ekibin kazandığı onur.</summary>
    public double HonorReward { get; init; } = 6;

    /// <summary>Kabul edilip süresi dolan sözleşmenin onur bedeli.</summary>
    /// <remarks>
    /// Kabul etmek bir <b>söz</b>dür. Bedeli olmasaydı her sözleşme kabul edilir, sonra
    /// uygun gün gelmezse sessizce unutulurdu — süre de karar da anlamını kaybederdi.
    /// </remarks>
    public double BrokenHonorPenalty { get; init; } = 10;

    /// <summary>Hedefin lakabı — adın kendisi bestiary'den, lakap buradan gelir.</summary>
    /// <remarks>
    /// Geçici: GDD §8'e göre yayın açıkken hedef adı chat'ten gelecek (Faz 5) ve adını
    /// veren izleyici dövüş boyunca düşman tarafını tutacak. Havuz o zaman buradan değil
    /// izleyici listesinden okunacak; sözleşmenin kendisi değişmeyecek.
    /// </remarks>
    public IReadOnlyList<string> Epithets { get; init; } =
    [
        "Kaburga Kıran", "Sisin Ağzı", "Dokuz Yara", "Kızıl Bataklı", "Kemik Toplayan",
        "Gece Yürüyen", "Tapınak Yakan", "İki Yüzlü", "Sessiz Adım", "Kör Öfke",
    ];

    /// <summary>Sözleşmeyi veren taraf — hedefin niçin arandığı.</summary>
    /// <remarks>
    /// İşveren yalnızca metin değil: GDD §6'daki onur ekseninin sözleşmeye giriş
    /// noktasıdır. Şimdilik ödülü değiştirmez, ton taşır.
    /// </remarks>
    public IReadOnlyList<string> Patrons { get; init; } =
    [
        "köy muhtarı", "tapınak rahibi", "tüccar loncası", "bölge lordu", "dul bir çiftçi",
    ];
}

/// <summary>Asılmış bir kelle avı sözleşmesi.</summary>
/// <remarks>
/// <para>
/// Günlük tekliften (<see cref="EncounterOffer"/>) üç şeyle ayrılır: hedef <b>isimlidir</b>,
/// sözleşmenin bir <b>süresi</b> vardır, ve kabul edip dönmemenin ayrı bir <b>bedeli</b>
/// vardır. Üçü birlikte kararı "bugün gireyim mi" olmaktan çıkarıp "hangi gün gireyim"e
/// çevirir.
/// </para>
/// <para>
/// Sözleşme kendi <see cref="EncounterOffer"/>'ını üretir, çünkü sefer katmanı yalnızca
/// teklif tanır. Ayrı bir sefer yolu açmak, dövüşe giden iki farklı kapı demek olurdu.
/// </para>
/// </remarks>
/// <param name="PostedDay">Sözleşmenin asıldığı gün.</param>
/// <param name="Deadline">Son geçerli gün — bu gün dahil.</param>
/// <param name="Target">Hedef; tek ve güçlü.</param>
/// <param name="Threat">Girmeden önce okunabilen bant.</param>
/// <param name="Reward">Söz verilen altın.</param>
/// <param name="Patron">Sözleşmeyi veren taraf.</param>
/// <param name="HonorReward">Tamamlanınca ekibin kazandığı onur.</param>
/// <param name="BrokenHonorPenalty">Kabul edilip süresi dolarsa kadronun kaybettiği onur.</param>
public sealed record BountyContract(
    int PostedDay,
    int Deadline,
    Warrior Target,
    ThreatBand Threat,
    int Reward,
    string Patron,
    double HonorReward,
    double BrokenHonorPenalty)
{
    /// <summary>Sözleşme bu gün hâlâ açık mı?</summary>
    public bool IsOpenOn(int day) => day >= PostedDay && day <= Deadline;

    /// <summary>Son gün dahil kaç gün kaldı.</summary>
    public int DaysLeft(int day) => Math.Max(0, Deadline - day + 1);

    /// <summary>Sözleşmenin dövüşü — sefer katmanının tanıdığı biçim.</summary>
    /// <remarks>
    /// Hedef tek olduğu için ekip büyüklüğü dayatılmaz: kaç kişiyle gideceğin
    /// sözleşmenin asıl kararıdır. Tek kişi gönderip kadroyu evde tutmak da, dördünü
    /// birden yığmak da geçerli — biri riski, diğeri o gün dojo'yu savunmasız bırakır.
    /// </remarks>
    public EncounterOffer AsOffer(int day) =>
        new(day, [Target], Threat, $"{Target.Name} — {Patron} arıyor");
}

/// <summary>Sözleşme tahtası: hangi gün hangi sözleşmenin asılı olduğunu üretir.</summary>
/// <remarks>
/// <para>
/// Teklif ve pazar gibi <b>saf</b>: aynı tohum ve aynı gün daima aynı sözleşmeyi verir.
/// Sözleşme kayda yazılmaz, günden ve tohumdan yeniden hesaplanır — kaydı yükleyip
/// beğenilmeyen sözleşmeyi değiştirmek işe yaramaz.
/// </para>
/// <para>
/// Tahta <b>dönem</b> numarasıyla çalışır: sözleşme
/// <see cref="BountyTuning.PostingDays"/> günde bir asılır ve
/// <see cref="BountyTuning.OpenDays"/> gün açık kalır. İkisi ayrı sayı olduğu için
/// sözleşmesiz günler vardır — sözleşme her gün asılı olsaydı sıradan teklif
/// anlamsızlaşırdı.
/// </para>
/// </remarks>
public sealed class BountyBoard(BountyTuning? tuning = null, EncounterTuning? encounters = null)
{
    /// <summary>Hedef kimlikleri düşman bandının üstünde ayrı bir bantta durur.</summary>
    public const int FirstTargetId = 200_000;

    public BountyTuning Tuning { get; } = tuning ?? new BountyTuning();

    private EncounterTuning Encounters { get; } = encounters ?? new EncounterTuning();

    /// <summary>Verilen gün asılı olan sözleşme; yoksa <c>null</c>.</summary>
    public BountyContract? Posted(int day, ulong seed, EconomyTuning economy)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(day);
        ArgumentNullException.ThrowIfNull(economy);

        int posting = Math.Max(1, Tuning.PostingDays);
        int period = (day - 1) / posting;
        int postedDay = (period * posting) + 1;
        int deadline = postedDay + Math.Max(0, Tuning.OpenDays - 1);

        if (day > deadline)
        {
            return null;
        }

        return Build(postedDay, deadline, new SeededRandom(Mix(seed, period)), economy);
    }

    /// <summary>Akışı dışarıdan verilen sözleşme — ölçüm ve test için.</summary>
    public BountyContract Build(
        int postedDay,
        int deadline,
        IRandomSource random,
        EconomyTuning economy)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(economy);

        // Hedefin gücü, sözleşmenin asıldığı günün eğrisinden çıkar: sözleşme takvimin
        // dışında bir şey değil, aynı eğrinin daha sert bir noktası.
        double power = new EncounterGenerator(Encounters).PowerFor(postedDay, random)
            * Math.Max(1, Tuning.PowerMultiplier);

        YokaiKind kind = Pick(power, random);
        Warrior target = kind.Spawn(new WarriorId(FirstTargetId + postedDay), power);

        string epithet = Tuning.Epithets.Count == 0
            ? "Adsız"
            : Tuning.Epithets[random.NextInt(Tuning.Epithets.Count)];
        string patron = Tuning.Patrons.Count == 0
            ? "bilinmeyen bir taraf"
            : Tuning.Patrons[random.NextInt(Tuning.Patrons.Count)];

        target.Name = $"{target.Name} — {epithet}";

        int reward = (int)Math.Round(
            target.EffectiveStats.MaxHealth
            * economy.VictoryGoldPerEnemyHealth
            * Math.Max(0, Tuning.RewardMultiplier));

        return new BountyContract(
            postedDay,
            deadline,
            target,
            Band(power),
            reward,
            patron,
            Tuning.HonorReward,
            Tuning.BrokenHonorPenalty);
    }

    private ThreatBand Band(double power) => power switch
    {
        var p when p >= Encounters.DireThreshold => ThreatBand.Dire,
        var p when p >= Encounters.HeavyThreshold => ThreatBand.Heavy,
        var p when p >= Encounters.RisingThreshold => ThreatBand.Rising,
        _ => ThreatBand.Faint,
    };

    private static YokaiKind Pick(double power, IRandomSource random)
    {
        List<YokaiKind> pool = [.. Bestiary.AvailableAt(power)];
        if (pool.Count == 0)
        {
            return Bestiary.Oni;
        }

        // Sözleşme hedefi eğrinin <b>üst</b> ucundan seçilir: kelle avının anlamı, o gün
        // sıradan devriyede karşına çıkmayacak bir şeyle karşılaşmak.
        double highest = pool.Max(k => k.MinPower);
        List<YokaiKind> top = [.. pool.Where(k => k.MinPower >= highest)];

        return top[random.NextInt(top.Count)];
    }

    /// <summary>Tohumu dönemle karıştırır — teklif, olay ve pazar akışlarından ayrı tuzla.</summary>
    private static ulong Mix(ulong seed, int period)
    {
        ulong x = seed ^ ((ulong)period * 0xD6E8FEB86659FD93) ^ 0x27D4EB2F165667C5;
        x ^= x >> 32;
        x *= 0x9E3779B97F4A7C15;
        x ^= x >> 29;
        return x;
    }
}
