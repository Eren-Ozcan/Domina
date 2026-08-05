namespace Domina.Core.Combat;

/// <summary>
/// Dövüşün tüm ayarlanabilir sayıları tek yerde.
/// </summary>
/// <remarks>
/// Bu değerler <b>denge için tahmini başlangıç noktalarıdır</b>, kanıtlanmış
/// değerler değil. Faz 9'da <c>Domina.Sim</c> toplu simülasyonuyla ayarlanacak
/// (bkz. docs/GDD.md → Açık Karar #8).
/// </remarks>
public sealed record CombatTuning
{
    /// <summary>Simülasyon adımı. 20 Hz, saldırı pencerelerini ayırt etmeye yeter.</summary>
    public double TickSeconds { get; init; } = 0.05;

    /// <summary>Dövüş bu süreyi aşarsa berabere sayılır.</summary>
    public double MaxBattleSeconds { get; init; } = 180;

    // ---- Saldırı ritmi ----

    /// <summary>Saldırganlık 0 iken saldırılar arası bekleme.</summary>
    public double SpacingSecondsAtZeroAggression { get; init; } = 1.4;

    /// <summary>Saldırganlık 100 iken saldırılar arası bekleme.</summary>
    public double SpacingSecondsAtMaxAggression { get; init; } = 0.30;

    /// <summary>Saldırı süresinin kesilemez (windup) kısmı; kalanı toparlanmadır.</summary>
    public double WindupFraction { get; init; } = 0.6;

    // ---- İsabet ve kaçınma ----

    public double BaseHitChance { get; init; } = 0.55;
    public double AccuracyHitBonus { get; init; } = 0.004;

    /// <summary>Kaçınma 100 iken kaçınma şansı.</summary>
    public double MaxEvasionChance { get; init; } = 0.45;

    /// <summary>Çekilirken savunmasızlık: kaçınma/blok yok, üstüne isabet bonusu.</summary>
    public double RetreatingHitBonus { get; init; } = 0.30;

    // ---- Hasar ----

    /// <summary>Güç 100 iken silah hasarına uygulanan çarpan.</summary>
    public double StrengthDamageBonusAtMax { get; init; } = 0.8;

    /// <summary>Savunma 100 iken hasarın azaldığı oran.</summary>
    public double MaxDefenseReduction { get; init; } = 0.45;

    public double MinimumDamage { get; init; } = 1;

    // ---- Stamina ----

    public double AttackStaminaCost { get; init; } = 6;
    public double DodgeStaminaCost { get; init; } = 12;
    public double StaminaRegenPerSecond { get; init; } = 4;

    /// <summary>Bu oranın altında stamina kalınca hasar ve isabet düşer.</summary>
    public double LowStaminaThreshold { get; init; } = 0.3;
    public double LowStaminaPenalty { get; init; } = 0.65;

    // ---- Uzuv kaybı ----

    /// <summary>
    /// Tek darbenin azami cana oranı bunu aşarsa "ağır darbe" sayılır ve uzuv
    /// kopma zarı atılır. Düşük can ÖN KOŞUL DEĞİL — ilk darbede de olabilir
    /// (bkz. docs/GDD.md §7).
    /// </summary>
    public double GrievousSeverityThreshold { get; init; } = 0.28;

    /// <summary>Ağır darbede taban uzuv kopma şansı; silah ve zırh bunu ölçekler.</summary>
    public double BaseDismembermentChance { get; init; } = 0.35;

    // ---- Çekilme ----

    /// <summary>Kaçış komutundan arenadan çıkışa kadar geçen savunmasız süre.</summary>
    public double RetreatSeconds { get; init; } = 1.2;

    public static CombatTuning Default { get; } = new();
}
