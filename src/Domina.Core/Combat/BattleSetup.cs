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

    /// <summary>Bu dövüşte kaç kez sersemleyip donuldu.</summary>
    /// <remarks>
    /// Künt silahın karşılığı ancak bununla ölçülür: kesici uzuv kopmasıyla ödüllenir,
    /// künt bu sayaçla (docs/GDD.md §7).
    /// </remarks>
    public int TimesStunned { get; init; }

    /// <summary>Kaç düşman sersemletildi.</summary>
    public int StunsInflicted { get; init; }

    /// <summary>Öldüyse ölümün sebebi; hayatta kaldıysa null.</summary>
    /// <remarks>
    /// Zehirle ölüm ile darbeyle ölüm aynı kutuya konsaydı zehrin ölçümü yapılamazdı:
    /// zehirli silahın iddiası "daha çok öldürüyor" değil, <b>başka türlü</b> öldürüyor.
    /// </remarks>
    public DeathCause? DeathCause { get; init; }

    /// <summary>Kaç kez zehirli vuruş yendi.</summary>
    /// <remarks>
    /// Zehrin karşılığı iki sayıda birden okunur: bu sayaç silahın <b>temas</b> sıklığını,
    /// <see cref="PoisonDamageTaken"/> ise dozun gerçekten ne kadar iş yaptığını söyler
    /// (docs/GDD.md §7).
    /// </remarks>
    public int TimesPoisoned { get; init; }

    /// <summary>Kaç düşman zehirlendi.</summary>
    public int PoisonsInflicted { get; init; }

    /// <summary>Zehirden yenen toplam hasar — zırhın hiç azaltmadığı tek hasar.</summary>
    public double PoisonDamageTaken { get; init; }

    /// <summary>Zehirle verilen toplam hasar.</summary>
    public double PoisonDamageDealt { get; init; }

    /// <summary>Kaç kez gelen silah yakalandı.</summary>
    /// <remarks>
    /// Jitte/sai'nin karşılığı ancak bununla ölçülür: yakalama aleti hasarda kaybeder,
    /// kazandığını bu sayaçta ve düşmanın kilitli kaldığı süredeki bedava vuruşlarda
    /// geri alır (docs/GDD.md §7).
    /// </remarks>
    public int CatchesMade { get; init; }

    /// <summary>Kaç kez kendi silahı yakalanıp açıkta kalındı.</summary>
    public int TimesCaught { get; init; }

    /// <summary>Bu dövüşte kaç kez hücuma kalkıldı.</summary>
    /// <remarks>
    /// Hücumun sayıları ancak bu iki sayaçla ölçülebilir: eşik ve olasılık hücumun ne
    /// sıklıkla <b>başladığını</b>, varış oranı ise başlayanın karşılığının alınıp
    /// alınmadığını söyler (docs/GDD.md Açık Karar 11).
    /// </remarks>
    public int ChargesStarted { get; init; }

    /// <summary>Başlayan hücumların kaçı hedefe vardı.</summary>
    public int ChargesConnected { get; init; }

    /// <summary>Hücumları sırasında yediği bedava vuruş — hücumun §4'te vaat edilen bedeli.</summary>
    public int ChargeOpportunitiesTaken { get; init; }

    /// <summary>Birikme aşamasında isabet yiyip dağılan hücum sayısı.</summary>
    public int ChargesBroken { get; init; }

    /// <summary>Hücum kalkış anlarının toplamı — ortalama kalkış anını verir.</summary>
    public double ChargeStartSecondsSum { get; init; }

    /// <summary>Bu dövüşte en geç kalkılan hücumun anı.</summary>
    public double LastChargeStartSeconds { get; init; }

    public bool Died => FinalState == CombatState.Dead;

    public bool Escaped => FinalState == CombatState.Escaped;

    /// <summary>Saldırıların ne kadarı tuttu — onur hesabının ana girdisi.</summary>
    public double Accuracy => AttacksMade == 0 ? 0 : (double)HitsLanded / AttacksMade;
}
