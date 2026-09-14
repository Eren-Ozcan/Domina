# Plan — moving resolution into the engine (Open Decision #13, option (c))

> **Status: shelved. This is not a decision and nothing here is scheduled.**
>
> The rule in force is still the one in `CLAUDE.md` and `docs/ROADMAP.md` → "Core Principle":
> the simulation core does not depend on Godot, randomness is seeded, the resolver produces an
> event stream and the visualisation consumes it. This document exists so that if option (c) is
> ever taken, it is taken with the work already counted — not estimated in the moment it is wanted.
>
> GDD Open Decision #13 names the right moment to reconsider (c) as *after build-order step 8*,
> as a **rewrite decision rather than an architecture decision**. Step 8 closed on 2026-09-13, so
> the moment is here; the answer is still open.

---

## 1. What option (c) actually means

Not "use Godot for combat" — the game already does that, in `Game/Scripts/BattleHud.cs` and the
arena scene. Option (c) means the **resolver** moves: the thing that decides who swings, who is
hit, who dies. Today that is `Domina.Core/Combat/Battle.cs` — a fixed-tick loop
(`CombatTuning.TickSeconds`) with no engine reference and no wall clock.

Under (c) the decision would be taken by Godot's own frame loop and physics: positions, distances
and reach become node transforms and collision queries, and timing becomes `delta`.

**The thing (c) buys:** the fight stops being simulated twice. Today the core computes a position
and the HUD draws something that agrees with it; under (c) the drawn thing *is* the position, so
the picture can never disagree with the fight, and animation events (a blade landing on the frame
the sprite connects) come for free instead of being reconciled.

**The thing (c) costs:** the sim. `Domina.Sim` runs tens of thousands of campaigns headless. That
is not a convenience — **every balance number this project has** was read off that rig, and the
GDD records them with the sweeps that produced them. A resolver inside Godot cannot be swept that
way without shipping a headless Godot harness, which is item 3 below and is the whole risk.

---

## 2. Inventory — what would move, what would not

| Area | Files | Lines | Under (c) |
|---|---|---|---|
| The resolver | `Combat/Battle.cs` | 2,634 | **Rewritten** against Godot nodes and `delta` |
| Fight state | `Combat/Combatant.cs`, `CombatantSnapshot.cs` | 670 | Mostly survives; position/velocity move to the node |
| Space | `Combat/ArenaPoint.cs`, `Projectile.cs`, `GroundWeapon.cs` | 138 | **Deleted** — Godot owns space |
| Numbers | `Combat/CombatTuning.cs` | 902 | Survives unchanged; it is data |
| Events | `Combat/BattleEvent.cs`, `BattleLog.cs` | 442 | Survives; the consumer changes |
| Setup / policy | `BattleSetup.cs`, `IRetreatPolicy.cs`, `TargetProfile.cs` | 399 | Survives |
| The dojo layer | `Domina.Core/Dojo/**`, `Campaign/**` | — | **Untouched.** Day, season, economy, journal stay engine-free |
| Tests | 26 test files construct `new Battle(...)` | — | Rewritten or retired with the resolver |

So (c) is a rewrite of roughly **2,600 lines of the densest code in the repo** plus the 26 test
files that pin its behaviour — not a port. The dojo layer, which is the larger half of the core, is
not affected either way; #13 is a decision about the *fight*, not about the game.

---

## 3. The rig, which comes first and is non-negotiable

Without this, (c) is a coin toss. Nothing else in this plan may start until it exists.

**3.1 A headless Godot harness.** Godot runs with `--headless`; the question is whether the
resolver can be stepped deterministically inside it at a rate that makes sweeps possible. The
number to beat is the one `ThroughputTests` already pins: **10,000 fights in 10 s optimised**
(~1.7 s today, so there is room, but not 60× of it). Build the harness against a trivial fight
first and measure fights/second before touching `Battle.cs`.

**3.2 Determinism under a frame loop.** The core is deterministic because the tick is fixed and
the RNG is seeded. Godot gives fixed-step physics (`_PhysicsProcess`), which is the only usable
basis — but float order, collision order and node order are all part of the result. Acceptance:
**the same seed produces the same winner, the same elapsed seconds and the same event stream over
10,000 fights, on two different machines.** If that cannot be shown, (c) stops here permanently.

**3.3 A parity bed.** Before the old resolver is deleted, both must run the same scenarios: the
24 sweep scenarios already on record in `docs/PROGRESS.md`, 20,000 fights each. The new resolver
does not have to agree fight-for-fight — it will not, and cannot — but **every locked number in
the GDD must be re-read on it and must land inside the noise of its recorded value** (which is
about ±1.3 points on a one-seed campaign reading, so paired means over six seeds, the protocol
locked 2026-09-13).

Any number that does not survive is a number the design has to re-decide, and that has to be
counted as part of the cost rather than discovered halfway.

---

## 4. Phases, if the rig passes

Each phase ends on a green suite and a committed measurement; none of them is worth starting
without the one above it.

**Phase A — the harness.** Headless Godot, a seeded stepper, fights/second measured, determinism
across two machines shown. *Acceptance: 3.1 and 3.2. This is the whole decision; expect it to be
the longest phase and be willing to stop at its end.*

**Phase B — space moves.** Positions, distance, reach, projectile flight and the weapon on the
ground become nodes and queries. The decision logic still runs on the core's own tick, driven by
the engine's fixed step. `ArenaPoint`, `Projectile`, `GroundWeapon` are deleted last, not first.
*Acceptance: the parity bed's 24 scenarios re-read, every locked number inside noise.*

**Phase C — the decision moves.** Target selection, charging, striking, catching, blocking,
evading, panic, disarming, poison, armour wear. This is where `Battle.cs` is actually rewritten and
where a subtle change is most likely to hide. *Acceptance: the parity bed again, plus the whole
GDD §5/§7 table re-read.*

**Phase D — the tests.** The 26 test files are rewritten against the new resolver, or retired with
a written reason. A rule with no test is a rule that will be broken silently. *Acceptance: coverage
of the §7 rules is at least what it is today, enumerated rule by rule.*

**Phase E — the sim.** `Domina.Sim` is repointed at the harness, or replaced. Every campaign
number in the GDD is re-measured, and the ones that moved are re-decided. *Acceptance: the
difficulty-tier table, the charm prices, the honour thresholds and the season closure figures all
carry a date after this phase.*

Phases B and C cannot be shipped separately to players: between them the fight is half in each
world. They are one release.

---

## 5. What would be lost, and what mitigates it

| Loss | Mitigation |
|---|---|
| Headless sweeps | Phase A's harness, or (c) does not happen |
| Every recorded balance number | Phase E re-measures; budget for the ones that move |
| `sim replay <path>` — walking a run from its journal without the engine | The dojo journal still replays the *dojo*; the fight inside it would need the harness |
| Test speed (the suite runs in seconds today) | Accept a slower suite, or keep the fight tests on a headless runner in CI only |
| The stall report, which is blow-by-blow from the event stream | Survives, if the event stream survives — keep `BattleEvent` as the contract in every phase |

The event stream is the one thing to defend at every step. It is what makes a fight inspectable,
replayable and reportable, and it is the contract the HUD, the journal and the stall report all
sit on.

---

## 6. When to take this, and when not

**Take it if** a fight's picture and a fight's truth have visibly diverged in playtest and the
reconciliation is getting worse, **and** Phase A passes on this machine. That is the failure (c)
genuinely fixes.

**Do not take it** for any of these, which look like reasons and are not:

- *"The engine would make the combat code simpler."* The dense part of `Battle.cs` is the rules —
  catching, disarming, poison, wear — and an engine does not carry rules.
- *"Position and distance are annoying to do by hand."* They are written, tested and paid for.
- *"We can measure everything first and move later."* GDD #13 already answered this: everything has
  to be written into the engine-free core to be measurable at all, so (c) is a **second**
  implementation of the same rules, not a saving.
- *"Balance is done."* Balance is never done. Every new enemy kind or weapon reopens it, and
  without a sim, retuning is playtest only — which a 180-day permadeath run makes slow and noisy.

**The point of no return** is the deletion of `Combat/Battle.cs`. Until that commit, the old
resolver is a working fallback and the sim still runs. Do not delete it in the same release that
ships the new one.

---

## 7. Honest summary

On today's evidence the leaning recorded in the GDD — **(a), with real time in Godot and the core
on a fixed tick** — is still the cheaper and better-supported answer, and build-order step 8 shipped
exactly that shape without touching the core or invalidating a single measured number. This plan
exists so that the alternative is a decision with a price on it rather than an open question, and
its first phase is deliberately the one that would kill it.
