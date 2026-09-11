using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Honor;

public enum SeppukuOutcome
{
    /// <summary>Chat pardoned him — the warrior lives on.</summary>
    Pardoned,

    /// <summary>Found dishonourable — permanent death.</summary>
    Seppuku,
}

/// <summary>An open seppuku vote.</summary>
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

    /// <summary>How many distinct users voted.</summary>
    public int VoterCount => _voters.Count;

    /// <summary>One vote per user — spam must not decide the outcome.</summary>
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

/// <summary>A vote's result.</summary>
public sealed record SeppukuResolution(
    WarriorId WarriorId,
    string WarriorName,
    SeppukuOutcome Outcome,
    CrowdVerdict Verdict,
    bool DecidedByAudience);

/// <summary>
/// The artificial crowd that decides when no votes come in.
/// </summary>
/// <remarks>
/// It will be extended in phase 6 (AI Crowd). Even this simple form lets the core work completely in
/// single-player mode: the decision is still made if there is no real chat <b>or</b> if there is real
/// chat but nobody voted.
/// </remarks>
public interface ISeppukuFallback
{
    /// <param name="honor">The warrior's honour when the vote opened.</param>
    /// <param name="willpower">
    /// His Will (docs/GDD.md §3) — the stat that decides whether he stands his own ground when nobody
    /// speaks for him.
    /// </param>
    /// <param name="rng">The seeded stream.</param>
    bool ShouldPardon(double honor, double willpower, IRandomSource rng);
}

/// <summary>The lower the honour, the smaller the chance of a pardon.</summary>
public sealed class HonorWeightedFallback(HonorTuning? tuning = null) : ISeppukuFallback
{
    private readonly HonorTuning _tuning = tuning ?? HonorTuning.Default;

    public bool ShouldPardon(double honor, double willpower, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        // 50% at the threshold, 5% at zero honour.
        double t = Math.Clamp(honor / Math.Max(1, _tuning.SeppukuThreshold), 0, 1);
        double chance = 0.05 + (0.45 * t);

        // Will is what a man has when nobody speaks for him: an empty chat is exactly the case the
        // stat was added for (docs/GDD.md §3). It shifts the die, it never decides it — at Will 100 the
        // pardon chance grows by WillPardonBonus, at 0 it shrinks by the same share, and at the middle
        // the old number is reproduced exactly, so the honour measurements still stand.
        double shift = ((Math.Clamp(willpower, 0, 100) - 50) / 50) * _tuning.WillPardonBonus;

        return rng.Chance(Math.Clamp(chance + shift, 0, 1));
    }
}

/// <summary>
/// Queues the seppuku votes and resolves them.
/// </summary>
/// <remarks>
/// <para>
/// Why a queue: <c>!bushi</c>/<c>!ronin</c> are used both as a reaction to a live fight and as a vote.
/// If a fight and a vote were open at the same time, it would be unclear which one a command written in
/// chat counted for. So a vote waits until the fight ends and <b>two votes are never open at once</b>
/// (see docs/GDD.md §6).
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
    /// The record of the warrior in the open vote. It is stored separately because it has been taken out
    /// of the queue — with zero votes the AI decision looks at this honour value.
    /// </summary>
    private PendingEntry? _activeEntry;

    public SeppukuArbiter(IRandomSource rng, HonorTuning? tuning = null, ISeppukuFallback? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(rng);

        _rng = rng;
        _tuning = tuning ?? HonorTuning.Default;
        _fallback = fallback ?? new HonorWeightedFallback(_tuning);
    }

    /// <summary>The meta layer updates this when a fight starts and ends.</summary>
    public bool BattleInProgress { get; set; }

    public SeppukuVote? ActiveVote { get; private set; }

    public int PendingCount => _queue.Count;

    /// <summary>
    /// Evaluates the warrior's honour; if it is below the threshold he is queued.
    /// </summary>
    /// <returns>True if he was queued in this call.</returns>
    public bool Consider(Warrior warrior, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        if (!warrior.IsAlive || warrior.Honor >= _tuning.SeppukuThreshold)
        {
            return false;
        }

        // Pardon immunity: during this period no new vote opens even if he falls below the threshold.
        if (_immuneUntil.TryGetValue(warrior.Id, out DateTimeOffset until) && now < until)
        {
            return false;
        }

        if (ActiveVote?.WarriorId == warrior.Id || _queue.Exists(e => e.Id == warrior.Id))
        {
            return false;
        }

        _queue.Add(new PendingEntry(
            warrior.Id, warrior.Name, warrior.Honor, warrior.EffectiveStats.Willpower));
        return true;
    }

    /// <summary>
    /// Advances time: resolves a vote whose duration is up and opens a new one if appropriate.
    /// </summary>
    /// <returns>The result if a vote was resolved in this call, otherwise <c>null</c>.</returns>
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

        // No vote opens while a fight is running — the rule that avoids command ambiguity.
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

    /// <summary>Casts a vote in the open vote.</summary>
    /// <returns>True if the vote was counted; false if there is no vote or the user has already voted.</returns>
    public bool CastVote(string user, bool isBushi)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        return ActiveVote?.TryCast(user, isBushi) ?? false;
    }

    /// <summary>Closes the vote without waiting for its duration (for tests and fast-forwarding).</summary>
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
        double will = entry?.Willpower ?? 50;

        // Even a single vote is a real vote; the AI only decides at zero votes.
        bool pardon = decidedByAudience
            ? _fallback.ShouldPardon(honor, will, _rng)
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

    /// <summary>The value honour is pulled to after a pardon.</summary>
    public double PardonedHonor => _tuning.PardonedHonor;

    private sealed record PendingEntry(WarriorId Id, string Name, double Honor, double Willpower);
}
