namespace Domina.Core.Dojo;

/// <summary>Okulun üç kolu.</summary>
/// <remarks>
/// Üç kol üç ayrı darboğaza bakar: talimhane <b>ilerlemeyi</b>, revir <b>zamanı</b>,
/// kâhya <b>kasayı</b> hızlandırır. Aynı anda hepsine para yetmez — kolun kendisi bir
/// karardır, sıra ise ikinci karar.
/// </remarks>
public enum SchoolBranch
{
    /// <summary>Talimhane — antrenmanın hızı ve tavanı.</summary>
    Training,

    /// <summary>Revir — yaranın yediği gün.</summary>
    Infirmary,

    /// <summary>Kâhya — fiyatlar ve ödül.</summary>
    Steward,
}

/// <summary>Okulda satın alınabilecek bir tesis/kadro.</summary>
public enum SchoolNodeId
{
    /// <summary>Talimhane: antrenman günü daha çok kazandırır.</summary>
    TrainingGround,

    /// <summary>Kata ustası: savaşçının yaklaşabildiği tavan yükselir.</summary>
    FormsMaster,

    /// <summary>İç dojo: antrenman ikinci kez hızlanır.</summary>
    InnerDojo,

    /// <summary>Revir: doğal iyileşme günde iki gün erir.</summary>
    Infirmary,

    /// <summary>Otacı: ilaç bir gün daha eritir.</summary>
    Herbalist,

    /// <summary>Kırıkçı: sıyrık sayılan hasar payı büyür.</summary>
    BoneSetter,

    /// <summary>Kâhya: günlük stok ucuzlar.</summary>
    Steward,

    /// <summary>Hami: zafer daha çok öder.</summary>
    Patron,

    /// <summary>Simsar: savaşçı alımı ve onarım ucuzlar.</summary>
    Broker,
}

/// <summary>Ağaçtaki bir düğüm — bedeli, kolu ve kendinden önce geleni.</summary>
/// <param name="Id">Kalıcı kimlik; kayda bu yazılır.</param>
/// <param name="Branch">Bağlı olduğu kol.</param>
/// <param name="Name">Görünen ad.</param>
/// <param name="Cost">Altın bedeli.</param>
/// <param name="Requires">Önce alınması gereken düğüm; kolun ilkinde <c>null</c>.</param>
public sealed record SchoolNode(
    SchoolNodeId Id,
    SchoolBranch Branch,
    string Name,
    int Cost,
    SchoolNodeId? Requires = null);

/// <summary>Okul ağacının kataloğu ve sayıları.</summary>
/// <remarks>
/// <para>
/// GDD §10'un kararı: <b>asıl uzun vadeli yatırım okulda</b> olacak, savaşçıda değil.
/// Sebep permadeath — ölen savaşçı koca bir yatırımı da götürseydi oyuncu savaşçısını
/// sahaya sürmekten kaçınırdı. Okul ölmez; kadro erir, okul kalır.
/// </para>
/// <para>
/// Bedeller kol içinde <b>artar</b> (200 / 400 / 700): ilk düğüm erken oyunda erişilir,
/// üçüncüsü ancak ayakta kalmış bir dojo'nun işidir. Sayılar <b>kilitli değil</b>;
/// ölçümün sorusu hangi kolun kendi bedelini ödediği.
/// </para>
/// </remarks>
public sealed record SchoolTuning
{
    /// <summary>Talimhanenin ve iç dojonun antrenman hızı çarpanı.</summary>
    public double TrainingRateStep { get; init; } = 1.30;

    /// <summary>Kata ustasının yüzdelik stat tavanına eklediği.</summary>
    public double CeilingBonus { get; init; } = 4;

    /// <summary>Kata ustasının can/stamina tavanına eklediği.</summary>
    public double PoolCeilingBonus { get; init; } = 20;

    /// <summary>Revirin günde erittiği fazladan revir günü.</summary>
    public int RecoveryBonus { get; init; } = 1;

    /// <summary>Otacının ilaca eklediği revir günü.</summary>
    public int MedicineBonus { get; init; } = 1;

    /// <summary>Kırıkçının sıyrık payına eklediği.</summary>
    public double FreeDamageBonus { get; init; } = 0.10;

    /// <summary>Kâhyanın günlük stok fiyatlarına uyguladığı çarpan.</summary>
    public double UpkeepPriceFactor { get; init; } = 0.80;

    /// <summary>Haminin zafer ödülüne uyguladığı çarpan.</summary>
    public double RewardFactor { get; init; } = 1.15;

    /// <summary>Simsarın savaşçı alımına uyguladığı çarpan.</summary>
    public double RecruitPriceFactor { get; init; } = 0.75;

    /// <summary>Simsarın onarıma uyguladığı çarpan.</summary>
    public double RepairPriceFactor { get; init; } = 0.80;
}

/// <summary>Okul ağacının kendisi — düğümler ve sırası.</summary>
public static class SchoolTree
{
    /// <summary>Bütün düğümler, kol kol ve ucuzdan pahalıya.</summary>
    public static IReadOnlyList<SchoolNode> All { get; } =
    [
        new(SchoolNodeId.TrainingGround, SchoolBranch.Training, "Talimhane", 200),
        new(SchoolNodeId.FormsMaster, SchoolBranch.Training, "Kata ustası", 400, SchoolNodeId.TrainingGround),
        new(SchoolNodeId.InnerDojo, SchoolBranch.Training, "İç dojo", 700, SchoolNodeId.FormsMaster),

        new(SchoolNodeId.Infirmary, SchoolBranch.Infirmary, "Revir", 200),
        new(SchoolNodeId.Herbalist, SchoolBranch.Infirmary, "Otacı", 400, SchoolNodeId.Infirmary),
        new(SchoolNodeId.BoneSetter, SchoolBranch.Infirmary, "Kırıkçı", 700, SchoolNodeId.Herbalist),

        new(SchoolNodeId.Steward, SchoolBranch.Steward, "Kâhya", 200),
        new(SchoolNodeId.Patron, SchoolBranch.Steward, "Hami", 400, SchoolNodeId.Steward),
        new(SchoolNodeId.Broker, SchoolBranch.Steward, "Simsar", 700, SchoolNodeId.Patron),
    ];

    public static SchoolNode Find(SchoolNodeId id) => All.Single(n => n.Id == id);

    /// <summary>Bir koldaki düğümler, alınması gereken sırayla.</summary>
    public static IEnumerable<SchoolNode> Of(SchoolBranch branch) =>
        All.Where(n => n.Branch == branch);
}

/// <summary>Dojo'nun sahip olduğu tesisler.</summary>
/// <remarks>
/// Durum tutar, karar vermez: parayı kasadan düşen ve etkileri ayarlara işleyen taraf
/// <see cref="DojoState"/>. Ayrılmasının sebebi kayıt — dosyaya yalnızca <b>hangi
/// düğümlerin alındığı</b> yazılır; bonusların büyüklüğü (denge sayısı) yazılmaz, yoksa
/// eski kayıt yeni dengeyi geri getirirdi (GDD §2).
/// </remarks>
public sealed class School
{
    private readonly HashSet<SchoolNodeId> _owned = [];

    public School(SchoolTuning? tuning = null) => Tuning = tuning ?? new SchoolTuning();

    public SchoolTuning Tuning { get; }

    /// <summary>Alınmış düğümler.</summary>
    public IReadOnlyCollection<SchoolNodeId> Owned => _owned;

    public bool Has(SchoolNodeId id) => _owned.Contains(id);

    /// <summary>Bugün satın alınabilecek düğümler — parası ayrı bir soru.</summary>
    /// <remarks>
    /// Kol içinde sıra zorunludur: kata ustası talimhanesiz gelmez. Sıra olmasaydı ağaç
    /// bir ağaç değil, dokuz bağımsız düğmeden ibaret olurdu.
    /// </remarks>
    public IEnumerable<SchoolNode> Available() =>
        SchoolTree.All.Where(n => !Has(n.Id) && (n.Requires is null || Has(n.Requires.Value)));

    /// <summary>Düğümü sahiplenir. Sırası gelmediyse ya da zaten alındıysa <c>false</c>.</summary>
    internal bool Add(SchoolNodeId id)
    {
        SchoolNode node = SchoolTree.Find(id);
        if (Has(id) || (node.Requires is not null && !Has(node.Requires.Value)))
        {
            return false;
        }

        _owned.Add(id);
        return true;
    }

    /// <summary>Kayıttan gelen tesisleri yerine koyar.</summary>
    /// <remarks>
    /// Sıra kontrolü burada da işler: bozuk bir kayıt "kata ustası var, talimhane yok"
    /// diyemez. Düğümler katalog sırasıyla denenir, tutmayan sessizce düşer.
    /// </remarks>
    internal void Restore(IEnumerable<SchoolNodeId> owned)
    {
        _owned.Clear();
        HashSet<SchoolNodeId> wanted = [.. owned];
        foreach (SchoolNode node in SchoolTree.All)
        {
            if (wanted.Contains(node.Id))
            {
                Add(node.Id);
            }
        }
    }

    /// <summary>Alınmış tesislerin gün döngüsü ayarlarına işlenmiş hâli.</summary>
    public DojoTuning Apply(DojoTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);

        double rate = tuning.Training.GapClosedPerDay;
        if (Has(SchoolNodeId.TrainingGround))
        {
            rate *= Tuning.TrainingRateStep;
        }

        if (Has(SchoolNodeId.InnerDojo))
        {
            rate *= Tuning.TrainingRateStep;
        }

        double ceiling = tuning.Training.SkillCeiling;
        double pool = tuning.Training.PoolCeiling;
        if (Has(SchoolNodeId.FormsMaster))
        {
            ceiling += Tuning.CeilingBonus;
            pool += Tuning.PoolCeilingBonus;
        }

        return tuning with
        {
            Training = tuning.Training with
            {
                GapClosedPerDay = rate,
                SkillCeiling = ceiling,
                PoolCeiling = pool,
            },
            NaturalRecoveryPerDay = tuning.NaturalRecoveryPerDay
                + (Has(SchoolNodeId.Infirmary) ? Tuning.RecoveryBonus : 0),
            RecoveryFreeDamageShare = Math.Clamp(
                tuning.RecoveryFreeDamageShare
                    + (Has(SchoolNodeId.BoneSetter) ? Tuning.FreeDamageBonus : 0),
                0,
                1),
        };
    }

    /// <summary>Alınmış tesislerin fiyat ve ödül ayarlarına işlenmiş hâli.</summary>
    /// <remarks>
    /// İndirim <b>aşağı</b> yuvarlanır ve en az 1'de durur. Yakına yuvarlansaydı ucuz
    /// kalemlerde indirim tamamen kaybolurdu (2 altınlık yiyecek ×0.80 = 1.6, yuvarlanınca
    /// yine 2): kâhya kolu ambara hiç dokunmamış olurdu. Alt sınır 1, indirimin bir kalemi
    /// bedavaya çevirmesini engelliyor.
    /// </remarks>
    public EconomyTuning Apply(EconomyTuning economy)
    {
        ArgumentNullException.ThrowIfNull(economy);

        return economy with
        {
            MedicineRecoveryDays = economy.MedicineRecoveryDays
                + (Has(SchoolNodeId.Herbalist) ? Tuning.MedicineBonus : 0),
            FoodPrice = Priced(economy.FoodPrice, SchoolNodeId.Steward, Tuning.UpkeepPriceFactor),
            WaterPrice = Priced(economy.WaterPrice, SchoolNodeId.Steward, Tuning.UpkeepPriceFactor),
            MedicinePrice = Priced(economy.MedicinePrice, SchoolNodeId.Steward, Tuning.UpkeepPriceFactor),
            RecruitPrice = Priced(economy.RecruitPrice, SchoolNodeId.Broker, Tuning.RecruitPriceFactor),
            RepairGoldPerWear = Has(SchoolNodeId.Broker)
                ? economy.RepairGoldPerWear * Tuning.RepairPriceFactor
                : economy.RepairGoldPerWear,
            VictoryGoldPerEnemyHealth = Has(SchoolNodeId.Patron)
                ? economy.VictoryGoldPerEnemyHealth * Tuning.RewardFactor
                : economy.VictoryGoldPerEnemyHealth,
        };
    }

    private int Priced(int price, SchoolNodeId node, double factor) =>
        Has(node) ? Math.Max(1, (int)Math.Floor(price * factor)) : price;
}
