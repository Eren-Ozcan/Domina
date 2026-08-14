using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>Bir dövüşün girdileri.</summary>
/// <param name="PlayerSide">Dojo'nun sefere gönderdiği 1-3 savaşçı.</param>
/// <param name="EnemySide">Karşıdaki yokai(ler).</param>
public sealed record BattleSetup(
    IReadOnlyList<Warrior> PlayerSide,
    IReadOnlyList<Warrior> EnemySide)
{
    public CombatTuning Tuning { get; init; } = CombatTuning.Default;

    /// <summary>
    /// Olay akışı biriktirilsin mi? Görselleştirme için gerekli; toplu simülasyonda
    /// (on binlerce dövüş) kapatılırsa gereksiz ayırma yapılmaz.
    /// </summary>
    public bool CollectEvents { get; init; } = true;

    /// <summary>
    /// Kaçış kararını veren politika. Oyunda oyuncunun tuşu
    /// (<see cref="Battle.CommandRetreat"/>) kullanılır; simülasyonda bir politika
    /// verilir. <c>null</c> ise kimse kendiliğinden çekilmez.
    /// </summary>
    public IRetreatPolicy? RetreatPolicy { get; init; }
}

/// <summary>Bir dövüşün sonucu.</summary>
public sealed record BattleResult(
    BattleOutcome Outcome,
    double ElapsedSeconds,
    IReadOnlyList<WarriorBattleSummary> Summaries)
{
    public WarriorBattleSummary SummaryFor(WarriorId id) =>
        Summaries.First(s => s.Id == id);
}

/// <summary>Tek bir savaşçının dövüşten çıkardığı bilanço.</summary>
public sealed record WarriorBattleSummary(
    WarriorId Id,
    string Name,
    int Team,
    CombatState FinalState,
    double HealthRemaining,
    int AttacksMade,
    int HitsLanded,
    int TimesHit,
    int DodgesPerformed,
    double DamageDealt,
    double DamageTaken,
    bool LostLimb)
{
    /// <summary>
    /// Bu dövüşte kaybedilen uzuvlar. Meta katman bunları kalıcı sakatlığa çevirir.
    /// </summary>
    /// <remarks>
    /// Birden fazla olabilir: kuşatılmış savaşçı kaçarken menzilindeki her düşmandan bir
    /// fırsat saldırısı yer (§5) ve her biri ayrı bir uzva mal olabilir.
    /// </remarks>
    public BodyPartSet LostParts { get; init; } = BodyPartSet.None;

    public bool Died => FinalState == CombatState.Dead;

    public bool Escaped => FinalState == CombatState.Escaped;

    /// <summary>Saldırıların ne kadarı tuttu — onur hesabının ana girdisi.</summary>
    public double Accuracy => AttacksMade == 0 ? 0 : (double)HitsLanded / AttacksMade;
}
