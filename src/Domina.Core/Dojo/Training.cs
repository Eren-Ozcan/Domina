using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>Bir antrenman gününün konusu.</summary>
/// <remarks>
/// <para>
/// Dört talim sekiz statı <b>tam olarak</b> kaplar: hiçbir stat antrenmanın dışında
/// kalmaz, hiçbiri iki talimden birden beslenmez. Gün tek iş yediği için (GDD §10) talim
/// seçmek gerçek bir karardır — kadroyu neye göre şekillendirdiğin buradan çıkar.
/// </para>
/// <para>
/// Her talimin bir <b>birincil</b> bir de <b>ikincil</b> statı vardır; ikincil yarım pay
/// alır (<see cref="TrainingTuning.SecondaryShare"/>). Tek stat çalıştırılsaydı savaşçı
/// sekiz gün sekiz ayrı talimle düz bir profile doğru itilirdi; ikincil pay talimlere
/// şekil verir — vuruş talimi gören savaşçı hem isabetli hem atak olur.
/// </para>
/// </remarks>
public enum Drill
{
    /// <summary>Vuruş talimi — İsabet, ikincil Saldırganlık.</summary>
    Strikes,

    /// <summary>Siper talimi — Savunma, ikincil Güç.</summary>
    Guard,

    /// <summary>Ayak talimi — Kaçınma, ikincil Hız.</summary>
    Footwork,

    /// <summary>Kondisyon — Can, ikincil Stamina.</summary>
    Conditioning,
}

/// <summary>Antrenmanın ayarlanabilir sayıları.</summary>
/// <remarks>
/// <para>
/// Kazanç <b>mutlak</b> değil, kalan boşluğun payıdır: bir gün savaşçıyı tavana kalan
/// mesafenin <see cref="GapClosedPerDay"/> kadarını kapatacak şekilde ilerletir. Mutlak
/// artış sabit kalsaydı ya erken oyun anlamsız yavaş olurdu ya da geç oyunda her savaşçı
/// tavana yapışırdı; oran, azalan getiriyi kuralın içine koyar ve tavanı <b>aşılamaz</b>
/// değil <b>yaklaşılabilir</b> yapar.
/// </para>
/// <para>
/// Sayılar <b>kilitli değil</b>. Ölçümün sorusu belli (GDD §11): pazar tavanı yerine
/// koymayı acemi bandına kilitledi, ilerleme yolu artık yalnızca antrenman — "ucuz ham
/// adayı al, eğit" ile "parası yeten en iyisini al" bu sayıların altında <b>rakip</b>
/// olmak zorunda.
/// </para>
/// </remarks>
public sealed record TrainingTuning
{
    /// <summary>Bir antrenman gününün tavana kalan mesafeden kapattığı pay.</summary>
    /// <remarks>
    /// <b>0.04'te kilitlendi</b> (400 dojo × 60 gün, `patrol`): bu oranda iyi işleyen bir
    /// dojo'nun en iyi savaşçısı 60 günde ~465 skora çıkar, yani pazar tavanının ısırmaya
    /// başladığı ~473 sınırına dayanır. Aranan ilişki tam olarak budur — pazar bir yere
    /// kadar yerine koyar, ötesi yalnızca antrenmanla gelir. 0.02'de tavan hiç konuşmaz
    /// (antrenman süs kalır), 0.08'de dojo antrenmanla kurtulur (kasa 147'den 941'e çıkar,
    /// kapanan dojo %18.8'den %3.0'a düşer). Ayrıntı: docs/GDD.md §11.
    /// </remarks>
    public double GapClosedPerDay { get; init; } = 0.04;

    /// <summary>İkincil statın birincile göre aldığı pay.</summary>
    public double SecondaryShare { get; init; } = 0.5;

    /// <summary>Yüzdelik statların (İsabet, Savunma, ...) yaklaşabildiği tavan.</summary>
    /// <remarks>
    /// 100 değil: statın kendi ölçeğinin ucuna dayanan bir savaşçı dövüşün bütün
    /// zarlarını tek yönde çevirir. Tavan ölçeğin altında durur ki antrenman kadroyu
    /// güçlendirsin, dövüşü çözmesin.
    /// </remarks>
    public double SkillCeiling { get; init; } = 90;

    /// <summary>Savaşçının yolunu seçebilmesi için gereken antrenman günü.</summary>
    /// <remarks>
    /// Seçim <b>ücretsiz</b> ama bedava değil: bedeli, o güne kadar harcanan antrenman
    /// günleri. Gün şartı olmasaydı yol alım anında seçilirdi ve pazardan alınan savaşçı
    /// hazır uzmanlaşmış gelirdi — okulun yerine pazar yetiştirmiş olurdu.
    /// </remarks>
    public int PathTrainingDays { get; init; } = 20;

    /// <summary>Can ve staminanın yaklaşabildiği tavan.</summary>
    /// <remarks>
    /// Ayrı tutulur çünkü ölçeği ayrı: acemi 100 canla gelir, yüzdelik statları 35-55
    /// bandındadır. Aynı tavana bağlansalardı kondisyon talimi ilk günden ısırırdı.
    /// </remarks>
    public double PoolCeiling { get; init; } = 180;
}

/// <summary>Antrenman alanı — bir günün stat karşılığını hesaplar.</summary>
/// <remarks>
/// Saf ve durumsuzdur: aynı statlar, aynı talim ve aynı yetenek daima aynı sonucu verir.
/// Rastgelelik <b>kasten yok</b> — antrenman oyuncunun yatırımıdır, kumarı değil; zar
/// atsaydı "bugün eğitsem mi" kararı zarın arkasına saklanırdı.
/// </remarks>
public static class TrainingGround
{
    /// <summary>Bir günlük talimden sonraki statlar.</summary>
    /// <param name="stats">Sakatlık uygulanmamış ham statlar — antrenman bunları yazar.</param>
    /// <param name="drill">Günün talimi.</param>
    /// <param name="talent">
    /// Savaşçının <see cref="Warrior.Talent"/> payı; kazancı doğrudan çarpar.
    /// </param>
    /// <param name="tuning">Antrenman sayıları.</param>
    public static WarriorStats After(
        WarriorStats stats,
        Drill drill,
        double talent,
        TrainingTuning? tuning = null)
    {
        TrainingTuning t = tuning ?? new TrainingTuning();
        double primary = Math.Max(0, t.GapClosedPerDay * Math.Max(0, talent));
        double secondary = primary * Math.Max(0, t.SecondaryShare);

        return drill switch
        {
            Drill.Strikes => stats with
            {
                Accuracy = Grow(stats.Accuracy, t.SkillCeiling, primary),
                Aggression = Grow(stats.Aggression, t.SkillCeiling, secondary),
            },
            Drill.Guard => stats with
            {
                Defense = Grow(stats.Defense, t.SkillCeiling, primary),
                Strength = Grow(stats.Strength, t.SkillCeiling, secondary),
            },
            Drill.Footwork => stats with
            {
                Evasion = Grow(stats.Evasion, t.SkillCeiling, primary),
                Speed = Grow(stats.Speed, t.SkillCeiling, secondary),
            },
            Drill.Conditioning => stats with
            {
                MaxHealth = Grow(stats.MaxHealth, t.PoolCeiling, primary),
                MaxStamina = Grow(stats.MaxStamina, t.PoolCeiling, secondary),
            },
            _ => stats,
        };
    }

    /// <summary>
    /// Kadronun <b>en geri</b> statını çalıştıran talim.
    /// </summary>
    /// <remarks>
    /// Arayüz bunu bir öneri olarak kullanabilir, ölçüm ise politika olarak: seçim
    /// tavana kalan <b>oransal</b> mesafeye bakar, ham puana değil — yoksa ölçekleri
    /// farklı olduğu için kondisyon her gün kazanırdı.
    /// </remarks>
    public static Drill Weakest(WarriorStats stats, TrainingTuning? tuning = null)
    {
        TrainingTuning t = tuning ?? new TrainingTuning();

        Drill pick = Drill.Strikes;
        double widest = -1;

        foreach ((Drill drill, double value, double ceiling) in
            new[]
            {
                (Drill.Strikes, stats.Accuracy, t.SkillCeiling),
                (Drill.Guard, stats.Defense, t.SkillCeiling),
                (Drill.Footwork, stats.Evasion, t.SkillCeiling),
                (Drill.Conditioning, stats.MaxHealth, t.PoolCeiling),
            })
        {
            double gap = ceiling <= 0 ? 0 : Math.Max(0, (ceiling - value) / ceiling);
            if (gap > widest)
            {
                widest = gap;
                pick = drill;
            }
        }

        return pick;
    }

    /// <summary>Statı tavana kalan mesafenin bir payı kadar yaklaştırır.</summary>
    /// <remarks>
    /// Pay 1'i geçemez: yetenek çarpanı yüksek oranlarda payı 1'in üstüne çıkarabilir ve
    /// stat tek günde tavanı <b>aşardı</b> — tavanın yaklaşılan bir sınır olması kuralın
    /// kendisinde durmalı, ayarın küçük seçilmesine bırakılmamalı.
    /// </remarks>
    private static double Grow(double value, double ceiling, double share) =>
        value >= ceiling ? value : value + ((ceiling - value) * Math.Clamp(share, 0, 1));
}
