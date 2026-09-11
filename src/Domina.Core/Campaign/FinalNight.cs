using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Campaign;

/// <summary>The last night: five bouts, one after another, with no healing in between.</summary>
/// <remarks>
/// <para>
/// The night is <b>not a day</b>. Every bout writes its wounds and its dead to the roster and nothing
/// else happens: no day closes, no infirmary day burns, no upkeep is paid, no reward is collected.
/// That is the whole of the design (docs/GDD.md §10): a school that spent 180 days perfecting four
/// warriors has nobody left to send out for the fourth bout, so what the night tests is
/// <b>roster depth</b> rather than a champion.
/// </para>
/// <para>
/// It runs the same three steps as <see cref="Expedition"/> — setup, fight, accounting — and for the
/// same reason keeps them apart: the arena steps a watched fight itself, so a bout resolved in the
/// background and a bout watched on screen have to come out of the same inputs and leave the same
/// books.
/// </para>
/// </remarks>
public sealed class FinalNight(BattleAftermath? aftermath = null)
{
    private readonly BattleAftermath _aftermath = aftermath ?? new BattleAftermath();

    /// <summary>The last night's enemy ids start here, clear of both the roster and the offers.</summary>
    public const int FirstEnemyId = 900_000;

    /// <summary>Who can still be sent out tonight — hurt is allowed, broken is not.</summary>
    public static Func<RosterEntry, bool> CanAnswerTheBell(DojoState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        int ceiling = state.Season.Tuning.NightMaxWoundDays;
        return entry => entry.Warrior.IsAlive
            && !entry.Released
            && entry.RecoveryDaysRemaining <= ceiling;
    }

    /// <summary>Can this party take the field for this bout — and if not, why?</summary>
    public static FinalRefusal? Refuse(DojoState state, IReadOnlyList<RosterEntry> party)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(party);

        if (state.Season.Phase != SeasonPhase.FinalNight)
        {
            return FinalRefusal.NightNotOpen;
        }

        if (party.Count == 0)
        {
            return FinalRefusal.EmptyParty;
        }

        if (party.Count > EncounterOffer.MaxPartySize)
        {
            return FinalRefusal.WrongPartySize;
        }

        foreach (RosterEntry entry in party)
        {
            if (state.Roster.Find(entry.Id) is null)
            {
                return FinalRefusal.NotInRoster;
            }

            // The night does not ask for a whole man, it asks for one who can stand: a wound is carried
            // onto the field as health (SeasonTuning.NightWoundHealthPerDay), and only a man past the
            // ceiling is out. Refusing everyone with an infirmary day — the first version of this rule —
            // emptied the field entirely, because a season's roster is never unmarked.
            if (!entry.Warrior.IsAlive
                || entry.Released
                || entry.RecoveryDaysRemaining > state.Season.Tuning.NightMaxWoundDays)
            {
                return FinalRefusal.Unfit;
            }
        }

        return null;
    }

    /// <summary>The men the given bout puts on the field.</summary>
    /// <remarks>
    /// The first four bouts are Kurogane's seniors; the fifth is the man himself, alone. He stands
    /// there for the same reason the player does — the post is given to a person and not to a
    /// signboard, and a candidate who sends others in his place has conceded it.
    /// </remarks>
    /// <param name="tuning">The season's numbers; the round's power and count are read from it.</param>
    /// <param name="round">The bout, from 1.</param>
    public static IReadOnlyList<Warrior> Opponents(SeasonTuning tuning, int round)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(round, tuning.FinalRounds);

        double power = tuning.FinalRoundPowers[round - 1];
        int count = Math.Max(1, tuning.FinalRoundEnemies[round - 1]);
        bool last = round == tuning.FinalRounds;

        List<Warrior> enemies = [];
        for (int i = 0; i < count; i++)
        {
            EnemyKind kind = last ? Adversaries.KuroganeHead : Adversaries.SeniorStudent;
            enemies.Add(kind.Spawn(new WarriorId(FirstEnemyId + (round * 10) + i), power));
        }

        return enemies;
    }

    /// <summary>Sets the bout up but does not run it — the arena steps it itself.</summary>
    /// <exception cref="InvalidOperationException">If the party cannot take the field.</exception>
    public static BattleSetup Prepare(
        DojoState state,
        IReadOnlyList<RosterEntry> party,
        CombatTuning? tuning = null,
        IRetreatPolicy? retreat = null,
        bool collectEvents = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(party);

        if (Refuse(state, party) is FinalRefusal refusal)
        {
            throw new InvalidOperationException($"The party cannot take the field: {refusal}.");
        }

        return new BattleSetup(
            [.. party.Select(e => e.Warrior)],
            Opponents(state.Season.Tuning, state.Season.FinalRound))
        {
            Tuning = tuning ?? CombatTuning.Default,
            RetreatPolicy = retreat,
            CollectEvents = collectEvents,
            StartingHealthShare = WoundedShares(state, party),
        };
    }

    /// <summary>What each man of the party has left of himself when the bell goes.</summary>
    /// <remarks>
    /// The wounds are read from the infirmary days the roster is carrying — the only record of a wound
    /// the dojo keeps — and nothing is written back: the share is an input to this one bout.
    /// </remarks>
    public static IReadOnlyDictionary<WarriorId, double> WoundedShares(
        DojoState state,
        IReadOnlyList<RosterEntry> party)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(party);

        SeasonTuning tuning = state.Season.Tuning;
        Dictionary<WarriorId, double> shares = [];

        foreach (RosterEntry entry in party)
        {
            if (entry.RecoveryDaysRemaining <= 0)
            {
                continue;
            }

            double share = 1 - (entry.RecoveryDaysRemaining * tuning.NightWoundHealthPerDay);
            shares[entry.Id] = Math.Max(tuning.NightWoundHealthFloor, share);
        }

        return shares;
    }

    /// <summary>Runs the bout and writes its books.</summary>
    /// <exception cref="InvalidOperationException">If the party cannot take the field.</exception>
    public FinalRoundResult Fight(
        DojoState state,
        IReadOnlyList<RosterEntry> party,
        IRandomSource random,
        CombatTuning? tuning = null,
        IRetreatPolicy? retreat = null,
        bool collectEvents = false)
    {
        ArgumentNullException.ThrowIfNull(random);

        BattleSetup setup = Prepare(state, party, tuning, retreat, collectEvents);
        return Settle(state, setup, new Battle(setup, random).Run());
    }

    /// <summary>
    /// Closes the books of a finished bout: the wounds and the dead go onto the roster, the score goes
    /// onto the season, and <b>nothing else moves</b>.
    /// </summary>
    /// <remarks>
    /// There is deliberately no reward here. Gold on the last night would be gold with nothing left to
    /// spend it on, and paying for the bouts would quietly turn the night into another day of the
    /// economy — the night's only currency is the men still standing.
    /// </remarks>
    public FinalRoundResult Settle(DojoState state, BattleSetup setup, BattleResult battle)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(battle);

        int round = state.Season.FinalRound;
        AftermathReport aftermath = _aftermath.Apply(state, battle);

        bool won = battle.Outcome == BattleOutcome.PlayerVictory;
        SeasonPhase phase = state.Season.RecordFinalRound(won, aftermath.Dead.Count());

        // A night that was won on the field but leaves nobody able to walk out for the next bout is
        // still a night lost: the refusal above would only report it one call later, and an ending has
        // to be an ending the moment it is decided.
        if (phase == SeasonPhase.FinalNight && !state.Roster.Living.Any(CanAnswerTheBell(state)))
        {
            phase = state.Season.RecordFinalRound(false);
        }

        return new FinalRoundResult(round, battle, aftermath, won, phase);
    }
}

/// <summary>One bout of the last night as it comes back.</summary>
/// <param name="Round">The bout that was fought, from 1.</param>
/// <param name="Battle">The fight's raw result.</param>
/// <param name="Aftermath">What was written to the roster.</param>
/// <param name="Won">Was the bout won?</param>
/// <param name="Phase">Where the run stands after the bout.</param>
public sealed record FinalRoundResult(
    int Round,
    BattleResult Battle,
    AftermathReport Aftermath,
    bool Won,
    SeasonPhase Phase);

/// <summary>Why a party cannot take the field on the last night.</summary>
public enum FinalRefusal
{
    /// <summary>The season has not reached the last night, or the night is already over.</summary>
    NightNotOpen,

    /// <summary>Nobody was selected.</summary>
    EmptyParty,

    /// <summary>More than <see cref="EncounterOffer.MaxPartySize"/> men were selected.</summary>
    WrongPartySize,

    /// <summary>The warrior is not on this roster.</summary>
    NotInRoster,

    /// <summary>The warrior is wounded or dead — the night heals nobody.</summary>
    Unfit,
}
