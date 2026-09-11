using Domina.Core.Honor;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Dojo;

/// <summary>A warrior standing before the tribunal, waiting for the verdict.</summary>
/// <param name="Warrior">Who it is.</param>
/// <param name="Name">His name on the day he was summoned — the roster can be renamed under it.</param>
/// <param name="Honor">The honour that put him there.</param>
/// <param name="Willpower">What he has when nobody speaks for him (docs/GDD.md §3).</param>
/// <param name="OpenedDay">The day he was called.</param>
public sealed record Summons(
    WarriorId Warrior,
    string Name,
    double Honor,
    double Willpower,
    int OpenedDay)
{
    /// <summary>What the crowd has said so far.</summary>
    public CrowdVerdict Tally { get; init; } = CrowdVerdict.Silent;
}

/// <summary>How a summons ended.</summary>
/// <param name="Warrior">Whose it was.</param>
/// <param name="Name">His name.</param>
/// <param name="Outcome">Pardoned, or the sword.</param>
/// <param name="Verdict">The crowd's tally; silent if nobody spoke.</param>
/// <param name="DecidedByAudience">Was it decided by the artificial crowd — did no real vote come in?</param>
public sealed record TribunalVerdict(
    WarriorId Warrior,
    string Name,
    SeppukuOutcome Outcome,
    CrowdVerdict Verdict,
    bool DecidedByAudience);

/// <summary>
/// The dojo's own seppuku tribunal — the day-loop half of docs/GDD.md §6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists beside <see cref="SeppukuArbiter"/>.</b> The arbiter is the <b>live</b> half: it
/// runs on the wall clock, opens a 60-second vote, and never lets a vote and a fight compete for the
/// same chat command. That is the right shape while a stream is running and the wrong one for a dojo
/// whose unit of time is the day — a 15-minute pardon immunity means nothing to a calendar, and a
/// season played in an afternoon would resolve every vote in the same minute. So the clock and the
/// queue are re-stated here in days, and the parts that carry the <b>rule</b> rather than the timing —
/// the threshold, the crowd's tally, the artificial crowd's decision, the pardoned honour — are the
/// arbiter's own types, used unchanged. When chat arrives it drives this by casting votes into the
/// standing summons; it does not get a second rulebook.
/// </para>
/// <para>
/// <b>Without this class the honour system had no teeth.</b> Everything that pushes honour down — a
/// broken promise, a rout, a week the dojo sat out — was arithmetic on a number nothing ever read. The
/// missed-week penalty in particular was locked at a value chosen because "honour pushes toward the
/// seppuku threshold", and until now nothing happened at that threshold.
/// </para>
/// <para>
/// It holds no randomness of its own: the stream is handed in at the moment of the verdict and is a
/// function of the seed and the day, so reloading a save cannot reroll a verdict.
/// </para>
/// </remarks>
public sealed class Tribunal(HonorTuning? tuning = null, ISeppukuFallback? fallback = null)
{
    private readonly List<Summons> _queue = [];
    private readonly Dictionary<WarriorId, int> _immuneUntil = [];
    private readonly HashSet<string> _voters = new(StringComparer.OrdinalIgnoreCase);

    public HonorTuning Tuning { get; } = tuning ?? HonorTuning.Default;

    private readonly ISeppukuFallback _fallback = fallback ?? new HonorWeightedFallback(tuning);

    /// <summary>The man standing before the tribunal today; <c>null</c> if nobody is.</summary>
    public Summons? Standing { get; private set; }

    /// <summary>Those summoned and still waiting their turn.</summary>
    /// <remarks>
    /// One at a time, exactly as in the live arbiter: two open votes would make a chat command
    /// ambiguous, and on the dojo's side it would make the day's report unreadable.
    /// </remarks>
    public IReadOnlyList<Summons> Queue => _queue;

    /// <summary>The day a pardoned warrior may be summoned again.</summary>
    public IReadOnlyDictionary<WarriorId, int> Immunity => _immuneUntil;

    /// <summary>Is this warrior under a pardon's protection on the given day?</summary>
    public bool IsImmune(WarriorId warrior, int day) =>
        _immuneUntil.TryGetValue(warrior, out int until) && day < until;

    /// <summary>
    /// Calls the warrior before the tribunal if his honour has fallen through the threshold.
    /// </summary>
    /// <returns><c>true</c> if he was summoned by this call.</returns>
    public bool Summon(Warrior warrior, int day)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        if (!warrior.IsAlive
            || warrior.Honor >= Tuning.SeppukuThreshold
            || IsImmune(warrior.Id, day)
            || Standing?.Warrior == warrior.Id
            || _queue.Exists(s => s.Warrior == warrior.Id))
        {
            return false;
        }

        _queue.Add(new Summons(
            warrior.Id,
            warrior.Name,
            warrior.Honor,
            warrior.EffectiveStats.Willpower,
            day));

        return true;
    }

    /// <summary>Chat speaks for or against the man standing. One voice per user.</summary>
    /// <returns><c>true</c> if the voice was counted.</returns>
    public bool Vote(string user, bool bushi)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        if (Standing is not Summons open || !_voters.Add(user))
        {
            return false;
        }

        Standing = open with { Tally = bushi ? open.Tally.WithBushi() : open.Tally.WithRonin() };
        return true;
    }

    /// <summary>Answers the man standing, if his day has passed.</summary>
    /// <remarks>
    /// <para>
    /// A summons stands for <b>a day</b>, so the man called at the close of one day is answered at the
    /// close of the next. The day in between is what makes the crowd's voice possible at all, and on a
    /// silent dojo it is what keeps the verdict from being an instant execution the player never saw
    /// coming.
    /// </para>
    /// <para>
    /// It only answers; calling the next man is <see cref="CallNext"/>, a separate step because the
    /// day's new summonses are written between the two. Merged, a man who fell into disgrace today
    /// would have to wait an extra day before he was even called.
    /// </para>
    /// </remarks>
    /// <param name="day">The day that is closing.</param>
    /// <param name="random">The stream the artificial crowd decides with; seeded by the caller.</param>
    /// <returns>The verdict if one was given today.</returns>
    public TribunalVerdict? Close(int day, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        TribunalVerdict? verdict = null;

        if (Standing is Summons open && day > open.OpenedDay)
        {
            bool spoken = open.Tally.HasVotes;
            bool pardon = spoken
                ? open.Tally.FavorsMercy
                : _fallback.ShouldPardon(open.Honor, open.Willpower, random);

            verdict = new TribunalVerdict(
                open.Warrior,
                open.Name,
                pardon ? SeppukuOutcome.Pardoned : SeppukuOutcome.Seppuku,
                open.Tally,
                !spoken);

            if (pardon)
            {
                _immuneUntil[open.Warrior] = day + Tuning.PardonImmunityDays;
            }

            Standing = null;
            _voters.Clear();
        }

        return verdict;
    }

    /// <summary>Calls the next man to stand, if the tribunal is free.</summary>
    /// <remarks>
    /// One at a time: while a man is standing the rest wait, so the day's report never has to carry two
    /// verdicts and a chat command is never ambiguous about whom it speaks for.
    /// </remarks>
    public Summons? CallNext(int day)
    {
        if (Standing is null && _queue.Count > 0)
        {
            Standing = _queue[0] with { OpenedDay = day };
            _queue.RemoveAt(0);
        }

        return Standing;
    }

    /// <summary>Takes the warrior off the tribunal's books — he died, or he walked out free.</summary>
    /// <remarks>
    /// A dead or released man cannot be tried. Without this a warrior who fell in the fight that closed
    /// the day would still be called the next morning, and the report would name a corpse.
    /// </remarks>
    public void Forget(WarriorId warrior)
    {
        _queue.RemoveAll(s => s.Warrior == warrior);

        if (Standing?.Warrior == warrior)
        {
            Standing = null;
            _voters.Clear();
        }
    }

    /// <summary>Restores the tribunal from a save.</summary>
    /// <remarks>
    /// The queue and the pardons go into the file for the reason every other promise does: reloading
    /// must not be a way out. The voices already cast are <b>not</b> saved — they belong to a live chat
    /// that is not there when the file is opened again.
    /// </remarks>
    internal void Restore(
        Summons? standing,
        IEnumerable<Summons> queue,
        IEnumerable<KeyValuePair<WarriorId, int>> immunity)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(immunity);

        Standing = standing;
        _voters.Clear();

        _queue.Clear();
        _queue.AddRange(queue);

        _immuneUntil.Clear();
        foreach ((WarriorId warrior, int until) in immunity)
        {
            _immuneUntil[warrior] = until;
        }
    }
}
