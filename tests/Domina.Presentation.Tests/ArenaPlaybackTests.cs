using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Presentation.Tests;

/// <summary>
/// Phase 2's acceptance criterion: <b>given a seed, the fight can be watched from start to finish and
/// the events match exactly what is seen on screen</b> (a warrior who loses a limb is severed on screen too).
/// </summary>
/// <remarks>
/// The playback here is the engine-free twin of <c>BattleArena._Process</c>: it steps the fight in the
/// same order, turns events into reactions and computes the positions. That it can run without Godot is
/// the proof that the presentation logic is separated from the engine — if the separation breaks, this file
/// derlenmez.
/// </remarks>
public class ArenaPlaybackTests
{
    /// <summary>The threshold standing in for the player's key: it pulls the party when health falls below this share.</summary>
    private const double _pullOutBelow = 0.45;

    private sealed record Playback(
        BattleResult Result,
        IReadOnlyList<BattleEvent> Events,
        List<RigReaction> Reactions,
        Dictionary<WarriorId, ScenePoint> FinalPositions,
        Dictionary<WarriorId, CombatState> FinalStates);

    /// <summary>"Watches" a fight from start to finish and collects what happens on screen.</summary>
    private static Playback Watch(long seed, bool pullOut = true)
    {
        BattleSetup setup = DemoRoster.Setup();
        var battle = new Battle(setup, new SeededRandom((ulong)seed));
        var choreography = new ArenaChoreography(new ArenaLayout());
        var reader = new ReactionReader();

        List<RigReaction> reactions = [];
        Dictionary<WarriorId, ScenePoint> positions = [];
        Dictionary<WarriorId, CombatState> states = [];
        bool commanded = false;

        while (true)
        {
            bool running = battle.Step();

            reactions.AddRange(reader.Drain(battle.Events));

            IReadOnlyList<CombatantSnapshot> snapshots = battle.Snapshots();
            foreach (CombatantSnapshot snapshot in snapshots)
            {
                positions[snapshot.Id] = choreography.PositionFor(snapshot);
                states[snapshot.Id] = snapshot.State;

                // The player's key: the party pulls out when it is wounded. Limb loss only happens in
                // fights where you intervene in time (GDD §7).
                if (pullOut
                    && !commanded
                    && snapshot.Team == Battle.PlayerTeam
                    && snapshot.HealthFraction is > 0 and < _pullOutBelow)
                {
                    commanded = true;
                    battle.CommandRetreat();
                }
            }

            if (!running)
            {
                return new Playback(battle.Result!, battle.Events, reactions, positions, states);
            }
        }
    }

    /// <summary>
    /// Finds the first seed that produces the state being looked for (death, escape, limb loss).
    /// </summary>
    /// <remarks>
    /// The seed is not pinned: the balance numbers will be tuned in phase 9 and a pinned seed would
    /// quietly lose its meaning that day — the test would stay green while testing nothing.
    /// A seed found by searching is the same on every run, because the fight is deterministic.
    /// </remarks>
    private static Playback WatchUntil(string looking, Func<Playback, bool> until)
    {
        for (long seed = 1; seed <= 400; seed++)
        {
            Playback playback = Watch(seed);

            if (until(playback))
            {
                return playback;
            }
        }

        throw new InvalidOperationException(
            $"{looking} was not found within 400 seeds — have the balance numbers changed?");
    }

    /// <summary>
    /// The heart of the acceptance criterion: a warrior whose books say he lost a limb must be severed on
    /// screen too, and <b>the same limb</b>.
    /// </summary>
    [Fact]
    public void WhatTheSummaryReportsIsWhatTheScreenShows()
    {
        Playback playback = WatchUntil(
            "limb loss",
            p => p.Reactions.Exists(r => r.Kind == RigReactionKind.Dismember));

        foreach (WarriorBattleSummary summary in playback.Result.Summaries)
        {
            List<RigReaction> severed = playback.Reactions
                .FindAll(r => r.Warrior == summary.Id && r.Kind == RigReactionKind.Dismember);

            if (!summary.LostLimb)
            {
                Assert.Empty(severed);
                continue;
            }

            // A warrior can lose more than one limb in a single fight; the reactions must match the set
            // in the summary exactly.
            BodyPartSet reacted = severed.Aggregate(
                BodyPartSet.None,
                (set, r) => r.Part is BodyPart part ? set | part.AsFlag() : set);

            Assert.Equal(summary.LostParts, reacted);
            Assert.Equal(summary.LostParts.Parts().Count(), severed.Count);
        }
    }

    /// <summary>The same seed gives the same fight — including what is seen on screen.</summary>
    [Fact]
    public void TheSameSeedIsWatchedTheSameWayTwice()
    {
        Playback first = Watch(20260806);
        Playback second = Watch(20260806);

        Assert.Equal(first.Result.Outcome, second.Result.Outcome);
        Assert.Equal(first.Result.ElapsedSeconds, second.Result.ElapsedSeconds, 6);
        Assert.Equal(first.Reactions, second.Reactions);
        Assert.Equal(first.FinalPositions, second.FinalPositions);
    }

    /// <summary>A dead warrior stays where he fell; he is not teleported back to his line.</summary>
    [Fact]
    public void TheDeadRestWhereTheyFellInsideTheFrame()
    {
        var layout = new ArenaLayout();
        Playback playback = WatchUntil("dead", p => p.FinalStates.ContainsValue(CombatState.Dead));

        foreach ((WarriorId id, CombatState state) in playback.FinalStates)
        {
            if (state != CombatState.Dead)
            {
                continue;
            }

            // The body is neither flung onto the escape route nor dropped out of frame; its depth also
            // stays inside the arena.
            ScenePoint spot = playback.FinalPositions[id];
            Assert.InRange(spot.X, 0, layout.Width);
            Assert.InRange(spot.Y, layout.BackGroundY, layout.FrontGroundY);
        }
    }

    /// <summary>A warrior who leaves the arena alive really leaves the frame before he is hidden.</summary>
    [Fact]
    public void TheEscapedLeaveTheFrame()
    {
        var layout = new ArenaLayout();
        Playback playback = WatchUntil("fleer", p => p.FinalStates.ContainsValue(CombatState.Escaped));

        foreach ((WarriorId id, CombatState state) in playback.FinalStates)
        {
            if (state != CombatState.Escaped)
            {
                continue;
            }

            float x = playback.FinalPositions[id].X;
            Assert.True(x < 0 || x > layout.Width, $"{id} disappeared inside the frame: {x}");
        }
    }

    /// <summary>
    /// An opportunity attack is the price of every escape; a price with no counterpart on screen looks to
    /// the player like "I pressed the key and then my health went".
    /// </summary>
    [Fact]
    public void EveryOpportunityAttackReachesTheScreen()
    {
        Playback playback = WatchUntil("escape", p => p.Reactions.Exists(
            r => r.Kind == RigReactionKind.OpportunitySwing));

        int swings = playback.Reactions.Count(r => r.Kind == RigReactionKind.OpportunitySwing);
        int events = playback.Events.OfType<OpportunityAttack>().Count();

        // The number is tied to the event stream rather than to an upper bound: a free hit now comes not
        // only from an escape but from a charge too (GDD §4), so the assumption of one per warrior is not
        // correct. What is tied down is not the number itself but the event and the screen matching
        // exactly.
        Assert.True(events > 0);
        Assert.Equal(events, swings);
    }

    /// <summary>
    /// A limb comes off and reaches the screen without the key ever being pressed (GDD §7 — a heavy blow
    /// that does not kill needs no key).
    /// </summary>
    /// <remarks>
    /// Under the old rule this reaction <b>never</b> appeared in a fight without intervention; the test
    /// expected the opposite then. When the rule changed, this was tied here so that nothing was left
    /// verifying that severing reaches the visualisation.
    /// </remarks>
    [Fact]
    public void MaimingReachesTheScreenWithoutAnyButtonPress()
    {
        var seedsWithMaiming = 0;

        for (long seed = 1; seed <= 40; seed++)
        {
            Playback playback = Watch(seed, pullOut: false);

            List<RigReaction> severed =
                playback.Reactions.FindAll(r => r.Kind == RigReactionKind.Dismember);

            if (severed.Count == 0)
            {
                continue;
            }

            seedsWithMaiming++;

            // Every maiming reaction must match a loss in the summary — nobody gets a "phantom limb"
            // kaybetmemeli.
            foreach (RigReaction reaction in severed)
            {
                WarriorBattleSummary summary = playback.Result.SummaryFor(reaction.Warrior);

                Assert.True(summary.LostLimb);
                Assert.True(summary.LostParts.Has(Assert.NotNull(reaction.Part)));
            }
        }

        Assert.True(seedsWithMaiming > 0);
    }
}
