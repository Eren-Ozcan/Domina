namespace Domina.Core.Honor;

/// <summary>Onur ve seppuku sisteminin ayarlanabilir sayıları.</summary>
/// <remarks>
/// Denge değerleri Faz 9'da oturacak (bkz. docs/GDD.md → Açık Karar #8).
/// </remarks>
public sealed record HonorTuning
{
    /// <summary>Ödül çarpanının alt sınırı (%100 ronin).</summary>
    public double MinRewardMultiplier { get; init; } = 0.5;

    /// <summary>Ödül çarpanının üst sınırı (%100 bushi).</summary>
    public double MaxRewardMultiplier { get; init; } = 1.5;

    /// <summary>
    /// Aktif dövüşe verilen tepkinin onura etkisi. Performanstan gelen değişimle
    /// birlikte "büyük etki" tarafını oluşturur.
    /// </summary>
    public double LiveVoteHonorSwing { get; init; } = 12;

    /// <summary>
    /// Dövüş dışındaki savaşçıya hedefli komutun (<c>!ronin-&lt;isim&gt;</c>) etkisi.
    /// Kasıtlı olarak küçük: aksi hâlde bir grup, hiç dövüşmemiş bir savaşçıyı
    /// spam'leyerek seppuku'ya sürükleyebilirdi.
    /// </summary>
    public double TargetedVoteHonorSwing { get; init; } = 0.4;

    /// <summary>Dövüş performansının onura azami etkisi.</summary>
    public double PerformanceHonorSwing { get; init; } = 10;

    /// <summary>
    /// Kaçarak dönen savaşçının performans puanına eklenen ceza (-1..+1 ölçeğinde).
    /// </summary>
    /// <remarks>
    /// Bu, performans bileşeninin <b>içinde</b> kalır: iyi dövüşüp sonra çekilen savaşçı
    /// hâlâ kötü dövüşenden iyidir. Tuşun kendi bedeli ayrıdır, bkz.
    /// <see cref="RetreatHonorPenalty"/>.
    /// </remarks>
    public double EscapePerformancePenalty { get; init; } = -0.6;

    /// <summary>
    /// Kaçmanın düz onur bedeli. Performanstan bağımsız, her çekilişte uygulanır.
    /// </summary>
    /// <remarks>
    /// Buradaki sayı <b>yer tutucudur</b> — kaçmanın onur bedeli henüz kararlaştırılmadı
    /// (bkz. docs/GDD.md → Açık Karar #8). Ayrı bir knob olmasının sebebi:
    /// performans içindeki ceza isabet oranıyla karışıyor ve yeterince isabetli bir
    /// savaşçı kaçtığı hâlde onur kazanabiliyordu. Çekilmenin bedeli, ne kadar iyi
    /// dövüştüğünden bağımsız olarak görünmeli.
    /// </remarks>
    public double RetreatHonorPenalty { get; init; } = 8;

    /// <summary>
    /// Onurun saatte nötre (50) doğru toparlanma miktarı. Anlık bir troll saldırısının
    /// kalıcı ölüm cezasına dönüşmesini engeller; yalnızca <b>sürekli</b> onursuzluk
    /// seppuku'ya götürür.
    /// </summary>
    public double DecayPerHourTowardNeutral { get; init; } = 6;

    /// <summary>Bu değerin altına düşen savaşçı seppuku oylamasına girer.</summary>
    public double SeppukuThreshold { get; init; } = 12;

    /// <summary>Affedilen savaşçının onuru bu değere çekilir — eşiğin biraz üstü.</summary>
    public double PardonedHonor { get; init; } = 25;

    /// <summary>Oylama penceresi.</summary>
    public TimeSpan VoteWindow { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Affedilen savaşçının yeni oylamaya girmeden önceki bağışıklık süresi.
    /// Bu sürede onuru eşiğin altına inse bile oylama tetiklenmez.
    /// </summary>
    public TimeSpan PardonImmunity { get; init; } = TimeSpan.FromMinutes(15);

    public static HonorTuning Default { get; } = new();
}
