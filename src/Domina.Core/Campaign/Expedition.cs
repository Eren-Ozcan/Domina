using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Campaign;

/// <summary>The layer that accepts the offer and sets up the fight.</summary>
/// <remarks>
/// <para>
/// This is the only bridge between the dojo and the combat resolver. <see cref="DojoState"/> does not
/// set up the fight and <see cref="Battle"/> does not close the day — merging the two into one class
/// would break the separation that keeps the core runnable without the engine.
/// </para>
/// <para>
/// An expedition <b>eats a day</b> (GDD §10) and the day is eaten even if you flee. Closing the day is
/// not the caller's job: <see cref="Send"/> runs the fight, writes the result to the roster and closes
/// the day itself, because that is exactly the item that closes the "enter, look, run" loop.
/// </para>
/// </remarks>
public sealed class Expedition(BattleAftermath? aftermath = null)
{
    private readonly BattleAftermath _aftermath = aftermath ?? new BattleAftermath();

    /// <summary>Can the party be sent on the expedition — and if not, why?</summary>
    public static ExpeditionRefusal? Refuse(DojoState state, EncounterOffer offer, IReadOnlyList<RosterEntry> party)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(party);

        if (party.Count == 0)
        {
            return ExpeditionRefusal.EmptyParty;
        }

        if (offer.Day != state.Day)
        {
            return ExpeditionRefusal.StaleOffer;
        }

        if (!offer.Accepts(party.Count))
        {
            return ExpeditionRefusal.WrongPartySize;
        }

        foreach (RosterEntry entry in party)
        {
            if (state.Roster.Find(entry.Id) is null)
            {
                return ExpeditionRefusal.NotInRoster;
            }

            if (!entry.IsFitForCampaign)
            {
                return ExpeditionRefusal.Unfit;
            }
        }

        return null;
    }

    /// <summary>
    /// Sends the party against the offer: runs the fight, writes the result to the roster, pays the
    /// reward and closes the day.
    /// </summary>
    /// <remarks>
    /// This is the route that resolves a fight <b>unwatched</b> (batch simulation, tests). The arena
    /// steps the fight itself; in that case it is set up with <see cref="Prepare"/> and closed with
    /// <see cref="Settle"/>. Both do the same three steps — setup, fight, accounting — and the
    /// accounting lives in a single place.
    /// </remarks>
    /// <exception cref="InvalidOperationException">If the party is not fit for the expedition.</exception>
    public ExpeditionResult Send(
        DojoState state,
        EncounterOffer offer,
        IReadOnlyList<RosterEntry> party,
        Rng.IRandomSource random,
        CombatTuning? tuning = null,
        IRetreatPolicy? retreat = null,
        bool collectEvents = false)
    {
        ArgumentNullException.ThrowIfNull(random);

        BattleSetup setup = Prepare(state, offer, party, tuning, retreat, collectEvents);
        return Settle(state, setup, new Battle(setup, random).Run());
    }

    /// <summary>
    /// Sets up the expedition's fight but <b>does not run it</b>.
    /// </summary>
    /// <remarks>
    /// Needed for the arena: because the player can intervene while watching the fight (the retreat
    /// command), the fight has to be stepped in real time rather than run in advance and replayed.
    /// Keeping the setup here guarantees that a watched fight and one resolved in the background come
    /// out of the <b>same</b> inputs.
    /// </remarks>
    /// <exception cref="InvalidOperationException">If the party is not fit for the expedition.</exception>
    public static BattleSetup Prepare(
        DojoState state,
        EncounterOffer offer,
        IReadOnlyList<RosterEntry> party,
        CombatTuning? tuning = null,
        IRetreatPolicy? retreat = null,
        bool collectEvents = false)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(party);

        if (Refuse(state, offer, party) is ExpeditionRefusal refusal)
        {
            throw new InvalidOperationException($"The party cannot be sent on the expedition: {refusal}.");
        }

        return new BattleSetup([.. party.Select(e => e.Warrior)], offer.Enemies)
        {
            Tuning = tuning ?? CombatTuning.Default,
            RetreatPolicy = retreat,
            CollectEvents = collectEvents,
        };
    }

    /// <summary>
    /// Closes the books of a finished fight: writes to the roster, pays the reward, closes the day.
    /// </summary>
    /// <remarks>
    /// <b>Where</b> the fight ran is of no concern here — it may have been watched in the arena or
    /// resolved inside <see cref="Send"/>. The accounting must live in one place: if the arena wrote its
    /// own books, a watched fight and a simulated one would leave different results and balance
    /// measurement would not be measuring what is on screen.
    /// </remarks>
    public ExpeditionResult Settle(DojoState state, BattleSetup setup, BattleResult battle)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(battle);

        AftermathReport aftermath = _aftermath.Apply(state, battle);

        int reward = state.Quartermaster.RewardFor(setup, battle.Outcome);
        state.Resources = state.Resources with { Gold = state.Resources.Gold + reward };

        DayReport day = state.AdvanceDay();
        return new ExpeditionResult(battle, aftermath, reward, day);
    }

    /// <summary>
    /// Sends the party against a bounty contract.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three items differ from an ordinary expedition: the reward is the figure the contract
    /// <b>promised</b> (not one derived from the fight's health), the party earns honour when the
    /// contract is completed, and the promise is broken if they do not come back. The fight itself goes
    /// the same route — the target is handed to the expedition layer as a single enemy, because opening
    /// a second door into a fight would split the resolver in two.
    /// </para>
    /// <para>
    /// An unaccepted contract can be entered too: seeing the job on the board and going the same day is
    /// legitimate. Accepting does not buy a day — it buys <b>time</b>.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// If the contract is not open today, or the party is not fit for the expedition.
    /// </exception>
    public BountyResult SendToBounty(
        DojoState state,
        BountyContract contract,
        IReadOnlyList<RosterEntry> party,
        Rng.IRandomSource random,
        CombatTuning? tuning = null,
        IRetreatPolicy? retreat = null,
        bool collectEvents = false)
    {
        ArgumentNullException.ThrowIfNull(random);

        BattleSetup setup = PrepareBounty(state, contract, party, tuning, retreat, collectEvents);
        return SettleBounty(state, contract, party, setup, new Battle(setup, random).Run());
    }

    /// <summary>Sets up the contract's fight but does not run it — see <see cref="Prepare"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// If the contract is not open today, or the party is not fit for the expedition.
    /// </exception>
    public static BattleSetup PrepareBounty(
        DojoState state,
        BountyContract contract,
        IReadOnlyList<RosterEntry> party,
        CombatTuning? tuning = null,
        IRetreatPolicy? retreat = null,
        bool collectEvents = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(contract);

        if (!contract.IsOpenOn(state.Day))
        {
            throw new InvalidOperationException("The contract is not open today.");
        }

        return Prepare(state, contract.AsOffer(state.Day), party, tuning, retreat, collectEvents);
    }

    /// <summary>Closes the books of a finished bounty hunt — see <see cref="Settle"/>.</summary>
    public BountyResult SettleBounty(
        DojoState state,
        BountyContract contract,
        IReadOnlyList<RosterEntry> party,
        BattleSetup setup,
        BattleResult battle)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(battle);

        AftermathReport aftermath = _aftermath.Apply(state, battle);

        bool claimed = battle.Outcome == BattleOutcome.PlayerVictory;
        int reward = claimed ? contract.Reward : state.Quartermaster.Economy.LostBattleGold;
        state.Resources = state.Resources with { Gold = state.Resources.Gold + reward };

        if (claimed)
        {
            // The honour is written to the party that <b>went</b> on the expedition, not to the whole
            // roster: they are the ones who brought the head. The penalty for a broken promise is written
            // to the whole roster, because the dojo gave the promise — the gain is personal, the debt shared.
            foreach (RosterEntry entry in party)
            {
                if (state.Roster.Find(entry.Id) is RosterEntry alive && alive.Warrior.IsAlive)
                {
                    alive.Warrior.Honor =
                        HonorScale.Clamp(alive.Warrior.Honor + contract.HonorReward);
                }
            }

            state.CloseBounty(contract.PostedDay);
        }

        DayReport day = state.AdvanceDay();
        return new BountyResult(contract, battle, aftermath, reward, claimed, day);
    }
}

/// <summary>A bounty hunt as it comes back to the dojo.</summary>
/// <param name="Contract">The contract entered.</param>
/// <param name="Battle">The fight's raw result.</param>
/// <param name="Aftermath">What was written to the roster.</param>
/// <param name="Reward">The gold that entered the treasury.</param>
/// <param name="Claimed">Was the head taken — was the contract completed.</param>
/// <param name="Day">The summary of the day the expedition ate.</param>
public sealed record BountyResult(
    BountyContract Contract,
    BattleResult Battle,
    AftermathReport Aftermath,
    int Reward,
    bool Claimed,
    DayReport Day);

/// <summary>An expedition as it comes back to the dojo.</summary>
/// <param name="Battle">The fight's raw result.</param>
/// <param name="Aftermath">What was written to the roster.</param>
/// <param name="Reward">The gold that entered the treasury (0 on a withdrawal or a rout).</param>
/// <param name="Day">The summary of the day the expedition ate.</param>
public sealed record ExpeditionResult(
    BattleResult Battle,
    AftermathReport Aftermath,
    int Reward,
    DayReport Day);

/// <summary>Seferin reddedilme sebebi.</summary>
public enum ExpeditionRefusal
{
    /// <summary>Nobody was selected.</summary>
    EmptyParty,

    /// <summary>The offer is not today's offer — an offer accepted yesterday cannot be entered.</summary>
    StaleOffer,

    /// <summary>The encounter imposes another number (a duel, say) or the upper limit was exceeded.</summary>
    WrongPartySize,

    /// <summary>The warrior is not on this roster.</summary>
    NotInRoster,

    /// <summary>The warrior is in the infirmary or dead.</summary>
    Unfit,
}
