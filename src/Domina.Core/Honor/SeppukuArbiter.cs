using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Honor;

public enum SeppukuOutcome
{
    /// <summary>Chat affetti — savaşçı yaşamaya devam eder.</summary>
    Pardoned,

    /// <summary>Onursuz bulundu — kalıcı ölüm.</summary>
    Seppuku,
}

/// <summary>Açık bir seppuku oylaması.</summary>
public sealed class SeppukuVote
{
    private readonly HashSet<string> _voters = new(StringComparer.OrdinalIgnoreCase);

    internal SeppukuVote(WarriorId warriorId, string warriorName, DateTimeOffset opensAt, TimeSpan window)
    {
        WarriorId = warriorId;
        WarriorName = warriorName;
        OpenedAt = opensAt;
        ClosesAt = opensAt + window;
    }

    public WarriorId WarriorId { get; }

    public string WarriorName { get; }

    public DateTimeOffset OpenedAt { get; }

    public DateTimeOffset ClosesAt { get; }

    public CrowdVerdict Tally { get; private set; } = CrowdVerdict.Silent;

    /// <summary>Kaç farklı kullanıcı oy kullandı.</summary>
    public int VoterCount => _voters.Count;

    /// <summary>Kullanıcı başına tek oy — spam sonucu belirlemesin.</summary>
    internal bool TryCast(string user, bool isBushi)
    {
        if (!_voters.Add(user))
        {
            return false;
        }

        Tally = isBushi ? Tally.WithBushi() : Tally.WithRonin();
        return true;
    }
}

/// <summary>Bir oylamanın sonucu.</summary>
public sealed record SeppukuResolution(
    WarriorId WarriorId,
    string WarriorName,
    SeppukuOutcome Outcome,
    CrowdVerdict Verdict,
    bool DecidedByAudience);

/// <summary>
/// Hiç oy gelmediğinde kararı veren yapay seyirci.
/// </summary>
/// <remarks>
/// Faz 6'da (AI Seyirci) genişletilecek. Buradaki basit hâli bile çekirdeğin tek
/// oyunculu modda eksiksiz çalışmasını sağlar: gerçek chat yoksa <b>veya</b> gerçek
/// chat var ama kimse oy kullanmadıysa karar yine de verilir.
/// </remarks>
public interface ISeppukuFallback
{
    bool ShouldPardon(double honor, IRandomSource rng);
}

/// <summary>Onur ne kadar düşükse af ihtimali o kadar azalır.</summary>
public sealed class HonorWeightedFallback(HonorTuning? tuning = null) : ISeppukuFallback
{
    private readonly HonorTuning _tuning = tuning ?? HonorTuning.Default;

    public bool ShouldPardon(double honor, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        // Eşikte %50, sıfır onurda %5 af şansı.
        double t = Math.Clamp(honor / Math.Max(1, _tuning.SeppukuThreshold), 0, 1);
        return rng.Chance(0.05 + (0.45 * t));
    }
}

/// <summary>
/// Seppuku oylamalarını sıraya alır ve çözer.
/// </summary>
/// <remarks>
/// <para>
/// Neden kuyruk: <c>!bushi</c>/<c>!ronin</c> hem aktif dövüşe tepki hem de oylama oyu
/// olarak kullanılıyor. Aynı anda hem dövüş hem oylama açık olsaydı chat'in yazdığı
/// komut hangisine sayılacağı belirsiz kalırdı. Bu yüzden oylama dövüş bitene kadar
/// bekler ve <b>aynı anda asla iki oylama açılmaz</b> (bkz. docs/GDD.md §6).
/// </para>
/// </remarks>
public sealed class SeppukuArbiter
{
    private readonly HonorTuning _tuning;
    private readonly ISeppukuFallback _fallback;
    private readonly IRandomSource _rng;
    private readonly List<PendingEntry> _queue = [];
    private readonly Dictionary<WarriorId, DateTimeOffset> _immuneUntil = [];

    /// <summary>
    /// Açık oylamanın savaşçısına ait kayıt. Kuyruktan çıkarıldığı için ayrıca
    /// saklanır — sıfır oy durumunda AI kararı bu onur değerine bakar.
    /// </summary>
    private PendingEntry? _activeEntry;

    public SeppukuArbiter(IRandomSource rng, HonorTuning? tuning = null, ISeppukuFallback? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(rng);

        _rng = rng;
        _tuning = tuning ?? HonorTuning.Default;
        _fallback = fallback ?? new HonorWeightedFallback(_tuning);
    }

    /// <summary>Meta katman bunu dövüş başlarken/biterken günceller.</summary>
    public bool BattleInProgress { get; set; }

    public SeppukuVote? ActiveVote { get; private set; }

    public int PendingCount => _queue.Count;

    /// <summary>
    /// Savaşçının onurunu değerlendirir; eşiğin altındaysa kuyruğa alır.
    /// </summary>
    /// <returns>Bu çağrıda kuyruğa alındıysa true.</returns>
    public bool Consider(Warrior warrior, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        if (!warrior.IsAlive || warrior.Honor >= _tuning.SeppukuThreshold)
        {
            return false;
        }

        // Af bağışıklığı: bu sürede eşiğin altına inse bile yeni oylama açılmaz.
        if (_immuneUntil.TryGetValue(warrior.Id, out DateTimeOffset until) && now < until)
        {
            return false;
        }

        if (ActiveVote?.WarriorId == warrior.Id || _queue.Exists(e => e.Id == warrior.Id))
        {
            return false;
        }

        _queue.Add(new PendingEntry(warrior.Id, warrior.Name, warrior.Honor));
        return true;
    }

    /// <summary>
    /// Zamanı ilerletir: süresi dolan oylamayı çözer, uygunsa yeni oylama açar.
    /// </summary>
    /// <returns>Bu çağrıda bir oylama çözüldüyse sonucu, yoksa <c>null</c>.</returns>
    public SeppukuResolution? Tick(DateTimeOffset now)
    {
        if (ActiveVote is not null)
        {
            if (now < ActiveVote.ClosesAt)
            {
                return null;
            }

            return CloseActiveVote(now);
        }

        // Dövüş sürerken oylama açılmaz — komut belirsizliğini önleyen kural.
        if (BattleInProgress || _queue.Count == 0)
        {
            return null;
        }

        PendingEntry next = _queue[0];
        _queue.RemoveAt(0);
        _activeEntry = next;
        ActiveVote = new SeppukuVote(next.Id, next.Name, now, _tuning.VoteWindow);
        return null;
    }

    /// <summary>Açık oylamaya oy verir.</summary>
    /// <returns>Oy sayıldıysa true; oylama yoksa veya kullanıcı zaten oy verdiyse false.</returns>
    public bool CastVote(string user, bool isBushi)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        return ActiveVote?.TryCast(user, isBushi) ?? false;
    }

    /// <summary>Süresini beklemeden oylamayı kapatır (test ve hızlandırma için).</summary>
    public SeppukuResolution? ForceResolve(DateTimeOffset now) =>
        ActiveVote is null ? null : CloseActiveVote(now);

    private SeppukuResolution? CloseActiveVote(DateTimeOffset now)
    {
        SeppukuVote vote = ActiveVote!;
        PendingEntry? entry = _activeEntry;
        ActiveVote = null;
        _activeEntry = null;

        CrowdVerdict verdict = vote.Tally;
        bool decidedByAudience = !verdict.HasVotes;

        double honor = entry?.Honor ?? _tuning.SeppukuThreshold / 2;

        // Tek bir oy bile gerçek oydur; yalnızca sıfır oyda AI karar verir.
        bool pardon = decidedByAudience
            ? _fallback.ShouldPardon(honor, _rng)
            : verdict.FavorsMercy;

        if (pardon)
        {
            _immuneUntil[vote.WarriorId] = now + _tuning.PardonImmunity;
        }

        return new SeppukuResolution(
            vote.WarriorId,
            vote.WarriorName,
            pardon ? SeppukuOutcome.Pardoned : SeppukuOutcome.Seppuku,
            verdict,
            decidedByAudience);
    }

    /// <summary>Af sonrası onurun çekileceği değer.</summary>
    public double PardonedHonor => _tuning.PardonedHonor;

    private sealed record PendingEntry(WarriorId Id, string Name, double Honor);
}
