# Plan — moving resolution into the engine (Open Decision #13, option (c))

> **Status: active since 2026-10-01.** Open Decision #13 closed on option (c), the user's call.
> The document was written on 2026-09-14 as a shelved plan so that the choice, if it came, would
> come with the work already counted; sections 1-7 are that plan, and section 0 below records what
> the decision changed in it. Where the two disagree, section 0 binds.
>
> While the plan runs, the rule in force is the transition one in `CLAUDE.md` and
> `docs/ROADMAP.md` → "Core Principle": the dojo layer stays engine-free, the contract is the
> event stream, randomness is seeded, and `Combat/Battle.cs` is the reference resolver until a
> later release deletes it.

---

## 0. What the decision changed (2026-10-01)

**Why (c), in the user's terms.** Four things are wanted from the engine, all four at once:
Godot physics and collision; the blow landing on the frame the animation connects; the arena,
the weapons' motion and the rig authored in the editor as content; and the fight and its picture
held in one structure. Section 6 below lists two of these as reasons that are not reasons; it is
kept as the record of the argument, and the call was made with it on the table.

**Determinism is narrowed (amends 3.2).** The acceptance is now *the same seed gives the same
winner, the same elapsed seconds and the same event stream over 10,000 fights, run twice on the
same machine and the same build*. Two machines are no longer required. What follows from it: a
`MoveJournal` replayed on another build may drift, so the journal's opening line carries the build
and the Godot version, and a replay on a different one warns instead of drifting silently.

**Measurement is kept, not mourned (amends section 5).** `Domina.Sim` runs inside a headless
Godot — the engine bed — with `--fighter core|engine` so that the two resolvers read the same
scenarios with the same seeds. Phase A is still the gate: if the harness cannot reach the
throughput or the determinism above, the work stops and comes back with the numbers.

**Step 0 comes before Phase A.** Open Decision #24 (the charge's season price) can only be
measured on the old resolver today, so it is answered there first. Then a baseline of every locked
number is taken on the old resolver and written down with its commands and seeds under
`docs/baselines/` — the parity bed of 3.3 is read against that, not against figures scattered
through `docs/PROGRESS.md`.

**A seam before the harness.** `IFightRunner` in the core (`BattleResult Fight(BattleSetup,
ulong seed)`) replaces the direct `new Battle(...).Run()` calls in the campaign, the stall report
and the sim, and the game gets an asynchronous lane for the fights nobody watches. Its first
implementation is today's resolver, so the seam changes nothing measurable; the engine fight is
then a second implementation behind the same door.

**Phase F is added: animation timing and editor content.** It comes after parity, never with it,
because it is a balance change. Each fighter gets an `AnimationPlayer` advanced by the fight's own
tick (callback mode manual, so the headless bed runs it too); a method-track key on the clip is the
frame the blow lands. The first clips are generated from today's `AttackSeconds` and windup so
nothing moves; clips authored in the editor afterwards are measured on the bed weapon by weapon
like any other number. The arena becomes a scene and a weapon's motion a resource; weapon and
armour **statistics** stay in `Domina.Core/Model`, because the market and the dojo layer read them.

**The inventory has grown since section 2 was counted.** On 2026-10-01: `Combat/` is 12 files and
5,502 lines, `Battle.cs` 2,773 of them, `Combatant.cs` 620, `CombatTuning.cs` 1,060; 47 of the 112
test files touch the combat namespace and about 30 construct a `Battle` directly; `Domina.Sim` is
7,629 lines. The tick is `CombatTuning.TickSeconds` = 0.05 s, so the engine bed starts at 20
physics ticks a second and any change to that is a measured change.

**Phase A, read 2026-10-02: the gate passes on the stand-in fight, with little room.** The record is
`docs/engine-bed/2026-10-phase-a/`; the bed is `src/Game/Bed/`. A trivial 3v3 built from Godot
bodies, motion tests, a collision-tested blade and a strike clip's method key ran 10,000 fights in
7.7 / 8.3 / 9.3 s on the editor binary (a loaded machine), and two runs were identical 10,000 of
10,000. The engine's own physics loop stepping the same fights gave the same 10,000 fingerprints as
the bed's loop — a watched fight and its replay on the bed are one fight. The room is thin: 91% of the
time is the motion test (2.4 µs), the stand-in makes 297 of them a fight and the core's real 3v3 moves
on 285 fighter-ticks a fight, so the port is forecast at about 9 s. Phase B inherits six measured
rules from the record — static bodies with the transform pushed to the server by hand, the bed
stepping fights itself, the blow's frame read off the clip rather than by running it, no
collection-returning engine call in the hot path, pooled fights, and a budget of about 330 motion
tests a fight — and counts its engine calls per fight on the bed as it goes.

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
the longest phase and be willing to stop at its end.* **Read 2026-10-02: passed on the stand-in, with
little room** (section 0; `docs/engine-bed/2026-10-phase-a/`).

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

**Phase F — animation timing and editor content** (added 2026-10-01, see section 0). After
parity, as a balance change. *Acceptance: generated clips read the same as the baseline; every
authored clip's timing carries its own bed measurement.*

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
