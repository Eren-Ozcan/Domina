using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Honor;

/// <summary>
/// Onur puanının nasıl değiştiğini hesaplar.
/// </summary>
/// <remarks>
/// Durumsuzdur — savaşçının onurunu kendisi saklamaz, yalnızca yeni değeri döner.
/// Kalıcı hali meta katman tutar.
/// </remarks>
public sealed class HonorEngine(HonorTuning? tuning = null)
{
    private readonly HonorTuning _tuning = tuning ?? HonorTuning.Default;

    public HonorTuning Tuning => _tuning;

    /// <summary>
    /// Dövüş performansının onura etkisi.
    /// </summary>
    /// <remarks>
    /// "Onurlu dövüş" burada şu demek: saldırdı, isabet ettirdi, kaçmadı.
    /// Kaçarak hayatta kalmak akıllıcadır ama onur kazandırmaz — oyuncunun
    /// "çekeyim mi" kararını gerçek bir ikilem yapan da budur.
    /// </remarks>
    public double PerformanceDelta(WarriorBattleSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (summary.AttacksMade == 0)
        {
            // Hiç saldırmadan biten dövüş seyirlik değildir.
            return -_tuning.PerformanceHonorSwing * 0.5;
        }

        // İsabet oranı (0-1) nötr noktası 0.5 kabul edilerek -1..+1 aralığına taşınır.
        double aggressionScore = (summary.Accuracy - 0.5) * 2;

        // Kaçarak biten dövüş onur getirmez.
        double escapePenalty = summary.Escaped ? _tuning.EscapePerformancePenalty : 0;

        double score = Math.Clamp(aggressionScore + escapePenalty, -1, 1);
        return score * _tuning.PerformanceHonorSwing;
    }

    /// <summary>
    /// Çekilmenin düz onur bedeli.
    /// </summary>
    /// <remarks>
    /// <see cref="PerformanceDelta"/> ile toplanır, onun yerine geçmez: biri "nasıl
    /// dövüştü", bu "çekildi mi". Savaş başlamadan çekilmek artık mümkün olmadığı için
    /// (bkz. docs/GDD.md §5) bu ceza her zaman <b>başlamış</b> bir dövüşü terk etmenin
    /// bedelidir.
    /// </remarks>
    public double RetreatDelta(WarriorBattleSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return summary.Escaped ? -_tuning.RetreatHonorPenalty : 0;
    }

    /// <summary>Aktif dövüşe verilen chat tepkisinin onura etkisi.</summary>
    public double LiveVoteDelta(CrowdVerdict verdict)
    {
        if (!verdict.HasVotes)
        {
            return 0;
        }

        // Oran 0.5 nötr; -1..+1 aralığına taşınır.
        return (verdict.BushiRatio - 0.5) * 2 * _tuning.LiveVoteHonorSwing;
    }

    /// <summary>
    /// Dövüş dışındaki bir savaşçıya hedefli komutun etkisi. Kasıtlı olarak küçüktür.
    /// </summary>
    public double TargetedVoteDelta(bool isBushi) =>
        (isBushi ? 1 : -1) * _tuning.TargetedVoteHonorSwing;

    /// <summary>Zamanla nötre doğru toparlanma.</summary>
    public double ApplyDecay(double honor, TimeSpan elapsed)
    {
        const double neutral = (HonorScale.Min + HonorScale.Max) / 2;

        double step = _tuning.DecayPerHourTowardNeutral * elapsed.TotalHours;
        double distance = neutral - honor;

        if (Math.Abs(distance) <= step)
        {
            return neutral;
        }

        return HonorScale.Clamp(honor + (Math.Sign(distance) * step));
    }

    public static double Apply(double honor, double delta) => HonorScale.Clamp(honor + delta);
}
