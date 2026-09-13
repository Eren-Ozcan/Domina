# Status Log

Last updated: 2026-09-13 (the yumi written, the enemy kinds' manner closed #3, the stall priced charm by charm, the tiers re-swept, and the bed given a paired multi-seed protocol)

This file is the answer to "where did we leave off". The plan lives in `ROADMAP.md`, the design
decisions in `GDD.md`; here there is only a snapshot of **what has been done and what is next**.

---

## Summary

| Phase | Status |
| --- | --- |
| Phase 0 — Scaffolding | ✅ Done |
| Phase 1 — Simulation core | ✅ Done (acceptance criteria measured) |
| Phase 2.1 — Visualisation backbone | ✅ Done (acceptance criterion tied to a test) |
| Phase 2.2 — Art and polish | ⬜ Blocked on the visual style decision |
| Phase 3 — Dojo / meta layer | 🟨 Started (roster + day cycle + save + post-fight accounting; economy and training effect pending) |
| Phase 4+ | ⬜ Not started |

Verification: `dotnet build` → 0 errors / 0 warnings, `dotnet test` → 336/336 green.
The Godot project builds separately: `dotnet build src/Game/Domina.Game.csproj`.

> `dotnet format --verify-no-changes` is **not clean**: there are 6 IDE1006 (`_` prefix)
> warnings — `HudModel.cs` and `ArmorWeightTests.cs`. An old debt, not created in this
> round; the count did not change on 2026-09-03.

---

## Done

### Phase 0 — Scaffolding
- .NET 10 SDK, Godot 4.7 .NET build (not committed to the repo, kept under `tools/`)
- `Directory.Build.props`: `net8.0`, nullable on, `TreatWarningsAsErrors`
- Solution: `Domina.Core`, `Domina.Chat`, `Domina.Sim` + three test projects
- The Godot project (`src/Game`) both builds and runs headless; isolated from the root
  settings by its own `Directory.Build.props`
- GitHub Actions: build + test on push

### Phase 1.2 — Deterministic RNG
- `IRandomSource` + `SeededRandom` (xoshiro256\*\*)
- **`System.Random` was deliberately not used:** its algorithm can change between .NET
  versions, which would break the "same seed = same fight" guarantee
- `FixedRandom` (scripted RNG) for tests

### Phase 1.1 — Data model
`Warrior` (unique ID + separate name field), `WarriorStats`, `Injury`/`Disability`
(permanent stat modifiers keyed by `BodyPart`), `Weapon` (cutting/blunt, number of hands),
`Armor`.

### Phase 1.3–1.4 — Fight resolver
- Event stream (`BattleEvent` hierarchy) — the resolver knows nothing at all about animation
- `CombatTuning`: every balance number in a single file
- Step-by-step simulation, multi-warrior team support
- Withdrawal: command buffering, vulnerability window, opportunity attack for the opponent
- Grievous blow outcome tree (light / grievous+yield / grievous+no intervention)
- `CombatantSnapshot`: read-only outward view; the single point of intervention in a fight
  is `Battle.CommandRetreat`

### Phase 1.5 — Honour engine
`CrowdVerdict` (ratio-based reward multiplier), `HonorEngine`, `SeppukuArbiter`
(queue, 60 s voting window, one vote per user, pardon + 15 min immunity,
AI decision on zero votes).

> A bug found and fixed while writing this: when `SeppukuArbiter` opened the vote it
> removed the warrior from the queue, and then in the zero-vote case could not find the
> honour value the AI was supposed to look at. Fixed so that the active record is also
> kept. `SeppukuTests.TheArtificialAudienceJudgesTheRightWarriorsHonor` exists so that
> this bug does not come back.

### Phase 1.6 — Batch simulation tool
`Domina.Sim` is now a working CLI: it runs N fights across a seed range, writes one CSV
row per fight and summarises the rates.

```bash
dotnet run --project src/Domina.Sim -c Release -- --scenario 3v3 --battles 10000 --policy below:0.3 --out result.csv
```

- Scenarios are hard-coded (`duel`, `3v3`, `veteran`, `ambush`) — so that two measurements
  compare the same roster
- `--policy` stands in for the player's "pull out" button. **This is required because limb
  loss only occurs in fights where intervention came in time:** a batch run with `never`
  produces no crippled warriors at all, only dead ones
- The denominator of the rates is not the number of fights but the number of warriors who
  took the field (in a 3v3 three warriors can die in a single fight)

### Phase 1 tests
Grew from 6 tests to **112**:

| File | Coverage |
| --- | --- |
| `DeterminismTests` | same seed = same fight, same event stream |
| `DismembermentTests` | grievous blow outcome tree, weapon/armour effect, permanent disability |
| `RetreatTests` | command buffering, vulnerability window, opportunity attack |
| `HonorTests` | performance/chat/targeted vote effects, reward multiplier, decay |
| `SeppukuTests` | queue, one-vote rule, tie, pardon immunity, AI decision |
| `BattleFlowTests` | end-to-end 3v3, event stream ↔ summary consistency, the stall guard |
| `ThroughputTests` | 10,000-fight budget, per-fight allocation |
| `BatchRunnerTests` / `SimCliTests` | aggregation accuracy, argument parsing, CSV |

### Acceptance criterion measurement
"10,000 fights < 10 seconds" was **measured and passed**: ~1 s in Release
(≈10,000 fights/s), ~2 s in Debug.

> Problem found along the way: `Battle.FindTarget` and `CountActive` were building a LINQ
> lambda for every warrior on every tick — ~279 KB allocated per fight. Converted to plain
> loops; the results stayed bit-for-bit identical and the speed doubled (4,200 → 10,000
> fights/s). `ThroughputTests.PerBattleAllocationStaysSmallWithoutEvents` prevents this
> from coming back.

---

## Phase 2 — backbone (2026-08-06)

The fight can now be watched on screen. The art is **deliberately temporary**: every limb
is a flat-coloured bar, i.e. a stickman. So that we do not enter real art before the style
decision is made.

```bash
dotnet build src/Game/Domina.Game.csproj
tools/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64.exe --path src/Game -- --seed 81
```

### The locked rig (expensive to change)
15 parts, root at foot level, character 256 px:

```
Root → Hip → Torso → Head
                   → Arm_Upper → Arm_Fore → Arm_Hand → Weapon   (×2)
            → Leg_Thigh → Leg_Shin → Leg_Foot                   (×2)
```

The break points are the **shoulder** and the **hip**. When the art changes, the drawing
hung on each bone changes and the hierarchy stays the same — but if the part list or the
proportions change, every animation is redone.

> **Skeleton2D + Bone2D were not used** (the ROADMAP said so). Bone2D is for mesh
> deformation; what we need is not to deform a limb but to sever it. In a plain `Node2D`
> hierarchy severing = detaching a node from its chain, which is exactly what GDD §2
> describes. The weapon of a warrior who loses an arm goes with the chain too — the
> `UsableWeapon` rule in the core comes for free in the visuals.

### What works
- The fight is **stepped in real time**, it is not a replay — since the player can
  intervene while the fight runs, the decision has to land on the live simulation
- The visuals are driven from two channels: continuous state from snapshots, momentary
  reactions (shake, limb severing, death) from the event stream
- **A single "PULL" button withdraws the entire team.** The button shows how many
  warriors will be affected and how many are locked into a blow: *"PULL THE PARTY (3)
  · 3 locked"*. Withdrawal for the locked ones starts once the blow ends, and that delay
  must not be a surprise
- The lunge is not a fixed distance but reaches the target: because the core points
  everyone at the same front-row enemy, a fixed lunge had the warriors at the back
  swinging swords into empty space

### Design change: yielding became team-wide

GDD §5 first said "each warrior can be pulled out individually"; that was **changed**. Now
a single command pulls all 1-3 warriors on the field and all of them lose honour.

Rationale: had it been per warrior, correct play would be "pull the one who takes a wound
immediately, continue with the rest" — a small, lossless, endlessly repeated optimisation.
A team-wide command makes the decision rare and heavy.

The effect was measured (3v3, 10,000 fights, `--policy below:0.3`):

| | Per warrior | Team-wide |
| --- | --- | --- |
| Victory | 61.7% | **5.1%** |
| Death | 56.7% | 32.0% |
| Escape | 8.2% | 63.3% |
| Limb loss | 1.15% | 2.50% |

That is, the "pull whoever drops to 30% health" policy now means abandoning almost every
fight — exactly what was intended. Limb loss doubling is expected too: intervention saves
everyone with a crippling injury instead of death.

> This **changed the meaning** of the policy thresholds in `Domina.Sim`. When balance is
> examined in Phase 9, `below:0.3` no longer means "cautious player" but "player who runs
> at the first sign of trouble"; much lower thresholds will be needed for a meaningful
> comparison.

### Added to the core
`StateProgress` (0-1) and `CanCancel` were added to `CombatantSnapshot`; `Combatant` now
makes state transitions through `BeginState`. Matching the animation to the cancel window
was impossible without these. Behaviour did not change — everything stayed green,
determinism tests included.

### Verification
- The same seed (20260806) gives **15.2 s / PlayerVictory** both in the arena and in
  `Domina.Sim` → visualisation does not corrupt the simulation
- The fight the batch simulation reports as "limb loss at seed 52" also ends in limb loss
  in the arena; the severed arm falls to the ground together with its weapon

> Two bugs found and fixed during verification: the severed limb miscalculated the ground
> in scene coordinates and stayed hanging in the air; a dead warrior's button still read
> "PARTY PULLING OUT" (it read as if the command were being processed).

---

## Phase 2.1 — closing (2026-08-12)

The backbone was pulled from "works" to "done". Two jobs were done: **the presentation
logic was separated from the engine and tied to tests**, and then the gaps that surfaced
were closed.

### New layer: `Domina.Presentation`

A library with no dependency on Godot. It holds the decisions about how the fight looks on
screen; instead of Godot's `Vector2` it uses its own `ScenePoint`.

| Type | Its job |
| --- | --- |
| `ArenaLayout` / `ArenaChoreography` | who stands where, where the lunge goes, where the corpse stays |
| `ReactionReader` | event stream → one-shot visual reactions |
| `RigAnimator` / `RigPose` | state + reaction → 14 bone angles |
| `HudModel` | what the button and the panel say |
| `DemoRoster` / `ArenaArguments` | temporary roster, `--seed` / `--speed` |

`src/Game` is now thin: it builds the nodes, applies the incoming angle, detaches the
severed chain.

> **Why a separate project:** a decision that cannot be tested without opening the engine
> never gets tested. Phase 2 was the only untested phase; now `ArenaPlaybackTests` plays
> the fight in the same order as `BattleArena._Process` and checks that the limb loss in
> the tally holds over the **same limb** as the severing on screen — that is, Phase 2's
> acceptance criterion is now a test rather than a sentence. Being able to run without the
> engine is also live proof of the separation: if anything leaks into `src/Game`, these
> tests will not compile.

### Gaps that were closed

The shortcomings that became visible once the separation was clear:

- **The three outcomes of an attack all looked the same on screen.** Of 13 event types
  only 3 were wired to visuals; there was nothing for a miss or a dodge. Now a miss
  produces an overswing (`Overswing`) and a dodge produces a sidestep. Dodging is a core
  mechanic that spends stamina — if it has no counterpart on screen the player cannot see
  where the stamina is going.
- **The opportunity attack had no sign.** The cost of withdrawal could not be told apart
  from a normal blow ("I pressed the button, then my health went"). Since it corresponds
  to no state in the core — it resolves instantly behind the fleeing prey — a separate
  blow was added that temporarily takes over the pose of an idle warrior.
- **A dying warrior teleported back to the line.** Position was computed from state alone;
  someone who died mid-lunge snapped back. The choreography now remembers the frame before
  death.
- **A fleeing warrior vanished in the middle of the arena.** The escape distance was a
  fixed 460 px against a 1920 px frame. The distance is now computed from the frame.
- **A warrior who lost a leg did not limp while fleeing.** The escape pose did not pass
  through the disability layer: the hip stayed stuck at the last limp value while the
  intact leg played the normal running cycle.
- **A buffered command was not visible in the panel.** Before the button was pressed it
  said "3 locked", but after pressing it the panel still read "attacking" — as if the
  command had been swallowed.
- **A severed limb still received animation while lying on the ground.** The reference
  survived after the chain was detached and the pose was applied to the arm on the ground
  on every frame; that is why the falling rotation was never visible.
- **The pain flash multiplied the team colour a second time.** The limbs were already
  coloured; pushing the team colour into `Modulate` darkened the warrior instead of
  reddening them.

### Verification

- 57 new tests (173 total), `dotnet format` clean, the Godot layer builds separately
- Engine/engineless comparison matches exactly: seed 20260806 → **PlayerVictory / 15.2 s**,
  seed 81 → **PlayerDefeat / 32.0 s** — both in the arena and in engineless playback
- The tests do not pin a seed; they scan for the first seed that produces the situation
  they are after (death, escape, limb loss). When the balance numbers change in Phase 9 a
  fixed seed would silently lose its meaning: the test would stay green but would no
  longer be testing anything.

---

## Engine verification (2026-08-13)

After the space change the Godot layer was run:

```bash
dotnet build src/Game/Domina.Game.csproj                       # 0 errors / 0 warnings
tools/Godot_v4.7-stable_mono_win64/..._console.exe --headless --path src/Game -- --seed 81
```

The headless run completed without trouble and **the engine/engineless comparison held**:
seed 81 → **PlayerWipe / 18.8 s** both in the arena and in `Domina.Sim`. That is, while
space entered the core the architectural rule (visualisation does not affect the
simulation) was preserved.

> **Work not done — checking by eye.** The window was opened but the fight was not watched.
> Five things that will be on screen for the first time are waiting to be verified: real
> walking, depth reading as vertical offset + scale + draw order, no sword swinging from
> out of range, turning when the target changes, and a miss when the target moves away.
> To be looked at in the next session with `--seed 81 --speed 1`.

---

## Limb loss rate — decided (2026-08-14)

The pending question closed, but from an unexpected direction. The target had been set at
"5%"; the user said that number was arbitrary and that the rate should be **a function of
the kit** — a well-armoured warrior should come home intact. That turned out to be right,
and the mechanism was already partly in the code, tied to the wrong axis.

### Armour became slot by slot

`Armor` is no longer a single scalar but separate pieces (`ArmorPiece`) for
**head/torso/arm/leg**. Damage reduction and severing resistance are read from the region
the blow landed on. Hit locations already existed for exactly this — the comment in
`CombatTuning` stated the reason but there was nothing behind it.

For measurement `--armor none|light|medium|heavy` was added (it isolates the armour axis
while the rest of the scenario stays fixed) and the report now counts limbs **piece by
piece**.

### Two logic errors were found

**1. Winning while losing a limb was impossible.** Severing required `PlayerIntervened`,
that flag was only set by the "Flee" button, and per §5 the button ended the expedition.
20,000 fights, worst armour, **victory + limb loss: 0 times**. Every warrior who came back
crippled was a monument to an abandoned expedition; a one-armed champion could never come
home.

The outcome tree was split in two (GDD §7 updated): a **non-lethal** grievous blow severs
even without the button and the fight continues; on a **lethal** blow the old rule applies
— if the button was pressed the warrior lives with a lost limb, if not they die.

> **A description corrected while writing.** This was first written as "the button turns
> death into limb loss"; the user pointed out that this is only one branch of the tree.
> The button is **not a trade** — it starts a withdrawal whose outcome is unknown at the
> moment of pressing, and the cost falls onto a ladder: everyone escaped unwounded →
> everyone escaped wounded → escaped with limb loss → part of the team escaped → nobody
> escaped. The ladder was measured and written into GDD §5.
>
> For measurement `--policy at:<seconds>` was added: health-watching policies could not
> measure the costless end of the ladder **by construction** (if health has dropped, a
> wound has already been taken). With `at:0` it was seen that pressing before contact
> gives a **100% unwounded** exit.

### Escaping stopped being free (2026-08-14)

The user said "it should not be 100%". The reason was not one but three, and all three
were fixed at once.

| Why it was free | What was added |
|---|---|
| `MoveSpeed` was a single constant — pursuer and fleer at the same speed, net closing zero | `WarriorStats.Speed` (0-100). Oni 25, Kappa 55, Tengu 85. A fleeing warrior is additionally slowed by `RetreatSpeedMultiplier`. Losing a leg also lowers speed (`Disability.SpeedMultiplier`) |
| Melee did not reach the far half of the arena | `ThrownWeapon` + `Projectile`: the projectile spends time in the air and resolves on arrival. New events `ProjectileLaunched/Hit/Missed`; on the presentation side `ProjectileView`/`ProjectileTracker` |
| Leaving the arena itself was free | `EscapeMishapChance` (0.30): a mishap wound on the way out. It **never takes health below 1** — its purpose is not death, but removing the freeness |

**The real problem found while writing:** once speed was added the hunter caught up but
**could never land a blow**. The rule was "whoever locks into a lunge cannot walk"; the
hunter would catch up, swing, freeze for the duration of the lunge, the fleer would leave
range, and the sword came down into empty space every time. The rule was suspended for the
chase: a pursuer keeps running **during the lunge**. Not during recovery — with both open
the pursuer never stops and escaping collapsed entirely (76% of teams broke, instead of
30%). `RetreatSpeedMultiplier` was swept at 0.85/0.92/1.0, and **0.92** was chosen.

The new ladder (3v3, 20,000 fights, light kit):

| Rung | Before contact | 2nd s | Health 50% | When outnumbered |
|---|---|---|---|---|
| 1 · all escaped, unwounded | 35.0% | — | — | — |
| 2 · all escaped, wounded | 65.0% | 87.1% | 6.8% | — |
| 3 · all escaped, with limb loss | — | 7.4% | 2.3% | — |
| 4 · partial escape, no limbs lost | — | 4.9% | 71.8% | 29.6% |
| 5 · partial escape, with limb loss | — | 0.6% | 17.6% | 9.3% |
| 6 · nobody escaped | — | — | 1.5% | 61.1% |

> **The limb-loss band was not disturbed** — the rates set by armour stayed almost the same
> (unarmoured 8.6%, light 6.6%, medium 2.9%, heavy 0.4%). What changed is death and escape:
> escaping is now expensive, so death rose from 41.6% to 49.1% and escape fell from 8.7% to
> 4.1%.

In this pass GDD Open Decision **#4-C closed** (the throwing line is now "Active" in §4) and
the ladder plus three mechanics were written into §5. New test file `RangedAndFlightTests`.
190 tests total.

**2. The same limb could be severed more than once.** `_lostParts` held a single `BodyPart`
per warrior and every new loss erased the previous one; since `AlreadyLost` only looked at
the last one, a warrior who had lost an arm counted as "arm intact" once they lost a leg.
Measured: **22 severings** on a single warrior, `[Arm, Leg, Arm, Leg, ...]`. The record is
now a `BodyPartSet` — the type itself carries the no-duplicates guarantee, and since it has
value equality the summary records stay comparable in the determinism tests.

The same bug also swallowed the extra losses of a warrior who lost more than one limb: in an
unarmoured 3v3 there are **5830 severed limbs** against 5440 crippled warriors.

### The measured band (3v3, 20,000 fights, `losing:0.7`)

| Kit | Death | **Limb loss** | Victory |
|---|---|---|---|
| Unarmoured | 41.6% | **8.6%** | 68% |
| Light keikogi | 38.8% | **6.7%** | 70% |
| Dō-maru | 22.5% | **2.9%** | 89% |
| Ō-yoroi | 16.3% | **0.4%** | 96% |

`BaseDismembermentChance` **0.35 → 0.05**. The old value had been tuned for the tree in
which severing only fired inside the escape window; in the new tree the same number pushed
limb loss to 45%. 0.35/0.15/0.08/0.05/0.03 were swept — the knob does not move death and
victory rates appreciably, it only scales limb loss.

> **The button no longer determines the rate, it determines death.** In the same kit, limb
> loss for a player who never pulls out and one who pulls out early is almost the same
> (8.8% against 8.0%); death drops from 48% to 29%. **16.5%** of won fights bring home a
> crippled warrior.

Tests: `TestBuilders.PointBlank` now **pins** `BaseDismembermentChance` — tests that probe
the outcome tree test the rule, not the balance; if the balance number were inherited they
would break on every tuning change. 180 tests green in total.

### What is left of this

Of the previous session's four options, **C · blunt weapons should cause permanent injury**
is still open (the user said "next round"). GDD §7 promises "blunt → fracture/stun", the
core has nothing to match; since the tetsubo's severing multiplier is 0.15 it has almost no
permanent effect against a heavily armoured warrior. Options B/D are now unnecessary — they
had been put there to raise the rate.

---

## Stunning entered the core (2026-09-02)

Open Decision **#4-B's first item closed**: the stunning effect of blunt weapons is now in
the core. The "differs from the code" note in GDD §7 was deleted and replaced with the rule
and a table of numbers.

The rule: the same grievous blow rolls two dice — limb severing and stunning. A stunned
warrior freezes for 0.9 seconds; they do not walk, do not strike and **cannot dodge**.

### Measurement — where the trade turns

Two new scenarios were added (`blade` / `club`): same warrior, same enemy, **only the weapon
class differs** (Nodachi 34/1.60 against Tetsubo 30/1.55). Without a distinction this narrow,
it would not be separable whether the sentence "blunt weapons became useful" comes from the
stats or from the weapon.

| Base chance | Cutting victory | Blunt victory |
| --- | --- | --- |
| 0 (no rule) | 91.57% | 88.68% |
| 0.15 | 91.89% | 90.46% |
| **0.35** | **92.06%** | **92.08%** |
| 0.60 | 92.30% | 93.83% |
| 1.00 | 92.70% | 95.67% |
> Without the rule the blunt weapon was worse **on every axis**: it lost on the severing
> multiplier (0.15 against 1.0) and got nothing in return. 0.35 brings the two classes level;
> beyond that it tips in favour of blunt.

The threshold was swept too and left at 0.20: at 0.30 stunning almost never fires and blunt
falls behind again (89.07% against 91.55%) — that is, the problem comes straight back. At
0.10 **cutting weapons start stunning as well** and both sides weaken at once.

### An unexpected result on the duration

There is **no difference at all** between 0.5 and 0.9 seconds (92.06% / 92.08%). The reason:
in that band the stun mostly lands in a gap where the warrior was already waiting. What
bites about the rule is not the lost blow but **the closed dodge**. Teeth appear above 1.0
seconds — at 1.4 blunt jumps to 94.16%. 0.9 was chosen because it sits just below that
threshold and is long enough to read on screen.

### 4-D's tier trade survived

Armour damps stunning too, but not with the whole of its severing resistance — with a
**share of 0.6** (blunt force passes under the plate). Measured (3v3, 20,000 fights): at a
share of 0, 0.51 stuns per warrior; at 0.6, 0.33; at 1.0, 0.22. At 0.6 the tiers are still
each best at something: dō-maru has less death (43.78% against 44.15%), ō-yoroi less limb
loss (0.83% against 3.43%).

### The player pays the cost too

In a 3v3 the Oni's tetsubo now bites: player victory fell from 69.31% to **65.20%**, and
stuns taken per warrior rose to 0.39. Absolute balance is Phase 9's job; what is held in
this round is the **ratio** between classes.

### Two protective rules

- **A withdrawing warrior cannot be stunned** — otherwise an enemy with a blunt weapon would
  cancel §5's only intervention with a single die
- **A stunned warrior cannot be stunned again**, and the duration is not refreshed. When the
  duration ends, a buffered "Flee" command is processed: stunning does not **swallow** the
  command, it delays it

New test file: `StunTests` (9 tests). 233 tests green in total.

**Still open in 4-B:** poison, sword catching with jitte/sai, weapon breakage.

---

## Sword catching entered the core (2026-09-03)

Open Decision **#4-B's second item closed**: jitte and sai now fill the gap GDD §4 left when
it rejected shields. The rule and the table of numbers are in §7.

The rule: catching is the second line of defence, attempted **before dodging**. A dodge makes
the blow miss and it ends there; a catch erases the blow **and** binds the attacker for
0.6 s — a bound warrior does not walk, does not strike, cannot dodge. The die is fed by the
defender's Grip, the catchability of the attacker's weapon and the defender's **Accuracy**.

### Measurement — where the trade turns

Three new scenarios (`katana` / `jitte` / `sai`): same warrior, same enemy, all three
**one-handed**, only the weapon differs. The number of hands was held fixed so that what is
measured is catching and not the weapon class.

| Weapon | Victory | Limb loss | Catches/fight |
| --- | --- | --- | --- |
| Katana (control) | 73.09% | 0.90% | 0.00 |
| Jitte | 72.63% | 0.52% | 2.75 |
| Sai | 72.73% | 0.45% | 3.71 |

The base chance was swept: at 0.15 jitte gets 61.16%, at 0.20 68.53%, at 0.30 77.22%.
**0.24** is where the trade turns — the katana stays ahead on victory, the catching tools
buy not coming home crippled. 4-D's "every option is best at something" pattern holds.

### Heavy weapons are the answer to catching

Two more scenarios (`jitte-heavy` / `katana-heavy`): the enemy carries a two-handed nodachi.
Jitte wins 29.37%, katana 34.78%, and the jitte also loses its limb protection (11.77%
against 11.42) — against a nodachi the jitte is simply the wrong choice. At a leverage
multiplier of 0.5 the hole widened to 14 points (21.04%); **0.75** turns the trap into a
preference.

### The bind duration was again not measured — but for a different reason

The same finding as with the stun duration: in a 1v1, going from 0 s to 1.2 s only moves
72.44% → 73.72%. The window that opens lands in a gap where the warrior was already waiting;
where the rule bites is **the erased blow**.

`3v3-jitte` was added to measure the bind's team value, and there too it did not separate —
but for a different reason: a novice carrying a jitte sees only **~0.57 catchable blows** per
fight (the ceiling was measured with `--catch-chance 1.0`, 0.19/warrior). In a crowd the
targets split, the Tengu throws projectiles, and the Oni's tetsubo is hard to catch anyway.
**The team value of the bind is still an unmeasured question.** 0.6 s was chosen because it
is long enough to read on screen and below where the trade turns.

### The stamina cost bites from an unexpected direction

At a cost of 0 victory is 76.85%, at 16 it is 72.63% — but **the number of catches is the
same in both** (2.72 / 2.75). So the cost does not make catching rarer, it **tires** the
warrior: stamina feeds both attacking and dodging. At 8 it binds nothing at all, at 30 it is
ruinous (41.07%).

### The sai's first numbers were wrong

It started at 13/1.05 and was simply bad (61.92%): the extra grip did not pay for the lost
damage. At 14/1.05 all three sit within half a point. Its difference from the jitte is not
damage but **volume** — the sai catches more, so it should have more work to do against a
crowd. **This encirclement measurement was not done.**

New state: `CombatState.WeaponBound` — kept separate from stunning, because both its cause
and its look on screen are different (a stunned warrior collapses and sways, a bound one
stands taut). New test file: `WeaponCatchTests` (12 tests). 245 tests green in total.

**Still open in 4-B:** weapon breakage.

---

## Poison entered the core (2026-09-03)

Open Decision **#4-B's third item closed**: poison is now in the code. The rule and the table
of numbers are in GDD §7.

The rule: **every hit** from a poisoned weapon leaves a dose on the defender — no die, if the
blade broke the skin the poison went in too. A dose eats one health per second and this damage
passes through **neither armour nor the Defence stat**; poison is the only route that goes
around damage reduction. Doses stack (ceiling 3.0) and the duration is reset from scratch on
each new hit (6.0 s).

The locked numbers: 2.5 damage per tick, tick interval 1.0 s, dose lifetime 6.0 s, maximum
dose 3.0. Poisoned tantō 7/0.85 (clean tantō 13/0.85), poisoned shuriken 12 damage /
2 ammunition.

### The first setup gave the wrong answer

With the poisoned blade left at 13 damage, the measurement said "poison works" but did **not
confirm the claim**: 74.00% in an open fight, 55.99% against an oni wearing ō-yoroi (katana
73.09% / 68.62%). That is, poison was not beating armour, it was only rescuing a weak blade —
because most of the output was still steel, and armour reads steel.

Once the blade was reduced to 7 and the dose enlarged, 60% of the output moved to poison and
the claim was confirmed:

| Weapon | Unarmoured oni | Oni wearing ō-yoroi |
|---|---|---|
| Katana (control) | 73.09% | 68.62% |
| Clean tantō | 31.14% | 1.23% |
| Poisoned tantō | 72.19% | **77.19%** |

An unexpected result: **against a poisoner, wearing heavy armour is a liability.** The plate
does not stop the dose, while its weight slows the oni's blow — the sign of armour flips in
this matchup.

### The real knob is the dose ceiling, not the lifetime

Ceiling: at 1 the poisoned blade is simply bad (16.71%), at 2 still behind (50.92%), at 3 level
with the katana, at 5 dominant (82.25%).

The lifetime does almost nothing past 6 seconds (3 s 52.90%, 4.5 s 68.83%, 6 s 72.19%, 9 s
73.11%): a fast-hitting weapon already keeps refreshing the duration, and a long lifetime only
extends what comes after the **last** hit — which in most fights is a finished fight.

The tick interval is not a neutral knob but the damage rate directly (0.5 s 94.93%, 1 s 72.19%,
2 s 30.40%). 1 s was chosen because, divided into the dose's lifetime, poison becomes
**countable**: six hits.

### When poison turns on the player

`3v3-poison` was added (the tengu throws poisoned shuriken; the control uses the same roster):
victory falls from 65.20% to 60.35%, escape from 8.10% to 6.86%, death rises from 45.45% to
50.44% and 1.3% of deaths are directly from poison.

**A withdrawing warrior's poison does not stop** — this is deliberate: stunning and catching do
not apply to a withdrawing warrior so that *a new die* is not laid on top of the promise of
escape, but poison is not a new die, it is the continuation of a price already paid; the button
is not an antidote. The measurement says §5's ladder still stands: escape still works, it is
just more expensive.

### What poison does not take

Poison does not sever limbs and does not stun — both are the result of *a blow*, and with poison
nobody is striking. Death that comes from poison carries its own cause (`DeathCause.Poison`);
otherwise the claim "it kills a different way" would show up in no counter.

New test file: `PoisonTests` (9 tests). 257 tests green in total.

**Still open in 4-B:** weapon breakage.

> **Note:** `ThroughputTests` sits right at the edge of the budget (10 s) in Debug and can fail
> when the whole suite runs together. Unrelated to the change: measured three times in isolation,
> 7-9 s both before and after the change. 2 s in Release. Either the budget must be raised or the
> test must be tied to Release.

---

## Next up

**Phase 2.2 — art production.** The style decision is made, nothing is blocking it.

### Visual style — decided (2026-08-13)

**Dark Edo woodblock print × layered paper theatre.** The full rule is in GDD §12.

Process: the same scene was produced for five candidate styles (dojo, training ground, weapon
workshop, strategy room), then a character sheet with separated parts was requested in the
winning direction.

| Candidate | Result |
|---|---|
| Sumi-e | **Eliminated.** Grey figure over a grey wash — the warrior does not separate from the ground, and four armour tiers do not read in monochrome |
| Ukiyo-e | Works, but the background is as contrasty as the figure; in a fight scene it eats the warrior |
| Woodblock print + paper theatre | **Chosen** |
| Gothic painting / clean vector | Considered, passed over for identity or cost reasons |

The reason for the choice is not aesthetic: in paper theatre the value gap sits **between the
figure and the ground** (light paper / dark mass). In ukiyo-e that distinction comes from colour,
here from value — value survives being scaled down, colour does not. The 128 px test confirmed
this.

The style also translates the rig's weakness into its own language: a flat limb rotating
mechanically looks wrong on a painted figure and right on a paper cut-out.

> **Critical production rule — clean asset, textured screen.** Texture and light are not baked
> into the part; the woodblock feel comes from a full-screen `CanvasLayer` overlay and the day
> cycle from a `CanvasModulate` tint. Otherwise each of the 15 parts has to be lit separately.
> Blood/vermilion is kept out of the tint, or the accent dies.

### Target selection became random (2026-08-13)

The core now picks its target **at random** instead of the first standing enemy in the list; the
target is sticky until that enemy dies or flees. `CombatantSnapshot.TargetId` was added — the
choreography no longer derives the target itself, it reads it from the core (the old duplicated
rule was deleted).

| 3v3, 10,000 fights, `below:0.3` | Front rank | Random |
| --- | --- | --- |
| Victory | 5.1% | **36.1%** |
| Player death | 32.0% | 25.8% |
| Escape | 63.3% | 40.8% |
| Limb loss | 2.50% | **1.10%** |

> Under the old rule all three enemies piled onto the same warrior, that warrior dropped below
> the threshold quickly and the policy pulled the whole team out. Random targeting spreads the
> damage.
>
> **Note:** focused fire is mathematically the stronger AI (a dead warrior deals no damage), so
> this change weakened the enemy. **Limb loss halved** — the game's signature mechanic. In Phase 9
> difficulty must be recovered from enemy count/stats, not from the targeting rule.

Tests: one new test — two warriors in the same roster lunge at different targets; if the
choreography derived the target, both would run to the same point.

### The arena became a plane (2026-08-13) — the largest change

**Space** entered the core: every warrior has an `ArenaPoint` position, actually walks, weapons
have reach, and encirclement is possible. There is no physics engine — our own kinematics, fixed
tick, determinism intact.

What it brought:

- **Reach:** an attack does not start out of reach; a long weapon strikes from further away
- **Targeting comes out of space:** "nearest enemy". Random selection is no longer needed
- **Miss:** if the target leaves reach mid-lunge the sword comes down into empty space
- **Encirclement:** a blow from behind is more accurate, heavier, undodgeable
- **The cost of escape depends on distance:** while withdrawing, **every** enemy in reach gets a
  free blow; if you are surrounded, fleeing means three blows
- **Escape is no longer a counter but a distance:** the warrior really leaves the arena

> **The presentation layer shrank.** `ArenaChoreography` was imitating a fake space: lunge
> distance, escape distance, remembering where the dead fell. All of it was deleted; the class now
> only projects the arena plane onto the screen (depth = vertical offset + scale + draw order,
> brawler staging) and keeps one visual flourish: leaning back while picking up a sword. The dead
> staying where they fell came for free — the core does not move corpses.

Measurement (3v3, 10,000 fights, `below:0.3`): victory 67.8%, death 33.6%, escape 12.3%, limb loss
0.56%. Speed 16,000 → **8,700 fights/s** (criterion: 10,000 fights < 10 s, still eight times
above it). Fight duration 7.9 → 13.1 s (approach time added).

### Limb loss pulled to target (2026-08-13)

Over the course of the day it had eroded from 2.50% → 1.10% → 0.87% → 0.56%. The target: ~5% of a
reasonably playing player's warriors should come back crippled.

It was searched for by measuring. Two flags were added to `Domina.Sim`: **`--grievous`** (grievous
blow threshold) and **`--sever`** (severing chance) — Phase 9's balance work will be done with
these.

Two things were found:

1. **`--sever` does not behave as expected.** Raising it can *reduce* limb loss: enemies lose limbs
   too, weaken, and the fight ends early. Also, if the severing die does not hold, an unattended
   warrior does not die either — that is, this number scales death and crippling at the same time.
2. **The real determinant is not tuning but when the player pulls out.** With the same settings,
   moving the policy 30% → 50% → 70% moves limb loss to 1.3% → 5.6% → 7.7%.

The single change made: `GrievousSeverityThreshold` **0.28 → 0.20**. The threshold was pulled to
just below where weapon damages cluster — there is no difference at all between 0.24 and 0.28,
because no blow falls in that range.

| Policy | Victory | Death | Escape | Limb loss |
| --- | --- | --- | --- | --- |
| `below:0.2` | 59.8% | 54.5% | 3.8% | 0.88% |
| `below:0.3` | 53.7% | 55.0% | 6.3% | 1.27% |
| **`below:0.5`** | 11.6% | 43.2% | 47.7% | **5.57%** |
| `below:0.7` | 1.3% | 9.1% | 89.8% | 7.73% |

> A player who pulls out late brings home **corpses**, one who pulls out early brings home
> **cripples**. That is exactly what is intended: when the button is pressed determines what the
> roster looks like. This table was written into GDD §7 as a balance target.
>
> The victory rate swinging from 60% to 1% with the policy is a separate problem — **that is
> Phase 9's real job**, not limb loss.

> ⚠️ **This table was invalidated on 2026-08-14.** Once the outcome tree was split in two, limb
> loss became a function not of the pull-out policy but of **the kit**; the balance target in
> GDD §7 was replaced with the armour band. See "Limb loss rate — decided" above.

New test file: `MovementTests` (approach, reach, long-weapon distance, personal space, the cost of
a surrounded escape, misses). 175 tests green in total.

### Hit locations were added (2026-08-13)

Every hit now lands on a region: torso 45 / leg 25 / arm 20 / head 10 by weight
(`CombatTuning`). The purpose is for the **slot-by-slot armour** to come later to be meaningful —
if the regions were equal, torso armour would only be worth a quarter.

> **A design leak caught while writing.** In the first version a grievous blow to the torso did not
> sever a limb; the measurement raised player victory from 36% to **53%**. The reason: with 45%
> probability intervention had become **completely free** — you come back from death and lose
> nothing. GDD §7's promise is the opposite. The rule was fixed: the region concerns damage and
> armour, not the outcome tree; on a torso hit the severed limb is chosen from the remaining ones
> with the same weights. `DismembermentTests.BlowsToTheTorsoStillCostALimb` guards this.

After the fix the numbers returned to the random-targeting values: victory 35.9%, death 26.0%,
escape 40.9%, limb loss 1.17%. 175 tests green in total.

### First job

The asset production specification is settled; the next concrete step is the **full-screen texture
overlay + day tint**, then the cut-surface assets (shoulder stump, hip stump, the end of the severed
limb). On the concept sheets the cut surface will be simplified to a **flat vermilion disc + dark
contour** — bone detail is lost at 128 px, and what carries the severing is the silhouette of the
missing chain.

### Weapon proficiency — decided (2026-08-13)

Three questions were answered and **written into GDD §4**; they now live there, not here.

| Question | Decision |
|---|---|
| Number of lines | **Three** — one-handed / two-handed / thrown. The throwing line is dormant until projectiles/space enter the core (Open Decision #4-C) |
| Source of growth | **Use in fights + dojo training** |
| Novice penalty | At proficiency 0 the weapon **can be wielded**, accuracy is markedly lower |

In the same pass: GDD §2's Skeleton2D line was corrected, "differs from the code" notes were added
under §5 (block) and §7 (blunt stunning), and Open Decision #4 was split into four (A locked,
B/C/D open).

> A nuance added by the user went into the GDD: losing grip resets **only the line**. Stats are not
> tied to the line, so a crippled master does not start from zero — they come back as **an accurate
> novice**. The risk is sharp but not devastating.

On the code side there is **nothing at all** yet — proficiency comes with Phase 3 (the meta layer);
its counterpart in the core will be an accuracy and attack speed multiplier inside `CombatTuning`.

### Things to keep in mind
- The balance numbers are **deliberately raw**. The first measurement shows a player death rate of
  45-57% in a 3v3; that is Phase 9's job and will not be tuned now (see the ROADMAP risk).
- `Domina.Chat` is still an empty scaffold (Phase 5). Its test project is empty too — the
  "no test is available" warning from `dotnet test` comes from there, it is not an error.
- A fight does not change the warriors' permanent state; writing death/crippling into the permanent
  state is **the meta layer's** job and has not been written yet (Phase 3). The roster in the arena
  is temporary too — `DemoRoster` will hand over to the real roster in Phase 3.
- When the art arrives, the places that change are known: `WarriorRig.Limb` (the drawing hung on the
  bone) and the pose numbers in `RigAnimator`. `RigPose`'s fields and `BattleArena` do not change —
  even if an AnimationPlayer replaces the procedural pose, the interface stays the same.

---

## Losing the weapon from the hand entered the core (2026-09-03)

Open Decision **#4-B's last item closed; the item is now fully closed.** The rule and the table of
numbers are in GDD §7.

The rule comes from two places. **A blow landing on armour** breaks the attacker's grip: the die
comes from the weapon's tendency to leave the hand and the hardness of the piece that was struck,
and hardness is read from that piece's severing resistance — that is, **a blow to a bare region
never disarms**. **A caught weapon** can be wrenched out of the palm on the hook; this die does not
stack on top of the bind, it **replaces** it.

The weapon **does not break, it drops**. The first setup was breakage; it was turned into dropping
because it opened a maintenance ledger (inventory, repair, spare weapons). A dropped weapon stays in
the arena, returns to its owner when the fight ends, and **anyone with an empty hand** can pick it
up: the one who dropped it, a teammate, an enemy. Someone with a weapon in hand neither picks up nor
searches — they will not take a single step towards the blade on the ground; a warrior who has lost
an arm also passes over a two-handed weapon on the ground.

The locked numbers: base chance 0.05 on a blow to armour, 0.05 on a caught weapon, hardness share
1.0, throw distance 250 units, pickup distance 60 units; tendency to leave the hand cutting 1.0 /
piercing 0.6 / blunt 0.2 / fists 0.

### The trade turns in front of the plate

Three classes against an armoured enemy (20,000 fights, `losing:0.7`):

| Weapon | No rule | With rule | Disarms | Picked up |
| --- | --- | --- | --- | --- |
| Nodachi (cutting) | 94.25% | 87.53% | 11.76% | 7.3% |
| Tetsubo (blunt) | 90.55% | **89.20%** | 2.73% | 4.8% |
| Yari (piercing) | 79.69% | 75.48% | 9.86% | 8.4% |

This is how the blunt class loses the last place where cutting was superior in front of the plate.
The base chance was swept: at 0.02 nothing turns (91.50% / 89.77%), 0.05 is where the trade turns,
and past 0.08 a cutting weapon becomes uncarryable in front of an armoured enemy (72.93% at 0.20).
The hardness share turned out not to be an extra knob but a copy of the base chance (at 0.5, disarms
11.71% → 6.04%); it was left at 1.0.

### What carries the cost is not distance but direction

Where the weapon is thrown was measured three times, and the whole of the rule hangs on it:

- If it falls **behind its owner**, the walk to pick it up pulls the warrior back out of the fight
  and, in front of a slow enemy, disarming becomes **free**: as the distance grows the player's
  victory rises (94.30% → 94.55%; 94.25% with no rule). That is, the rule costs nothing at all
- Being thrown **sideways** comes to the same thing (94.4%): a slow enemy cannot punish a warrior
  who leaves the line
- When it falls **behind the opponent** the cost is real: going for the weapon means walking through
  the enemy, and personal space does not allow it. Only here does the trade turn

Once the direction is settled, distance stops being a knob: 150 / 250 / 400 units give 87.43% /
87.45% / 87.48%. 250 was chosen (close to a warrior's height, readable on screen).

### Picking up does not work in a duel, it works in a crowd

7.3% of dropped weapons are recovered in a 1v1, 40.4% in a 3v3. In a crowd the targets
split, so it becomes possible to go to the weapon — the cost of the rule is not a fixed
penalty but depends on the shape of the fight.

### The disarm chance on a catch: at 0.10 the brake breaks

At 0, a catching implement gains nothing from disarming (jitte 74.87%, katana 75.02%).
At 0.05 the jitte goes ahead of a sword-carrying enemy with 78.00% / sai 78.88%.
**At 0.10 the jitte becomes the right choice against a nodachi as well** (38.27% against
katana 37.84%) and `CatchTwoHandedFactor` becomes meaningless — hence 0.05.

The rule does not make catching a superior weapon, because both of its brakes still hold:
against a nodachi jitte 35.22% / katana 37.84, against an enemy wearing ō-yoroi jitte
34.14% / katana 60.81%. The "every option is best at something" pattern holds — only the
place where the katana is best has changed: not victory, but **the armoured enemy**.

### Armour is a real wall for the first time

In a 3v3, giving all the yokai full kit **worked in the player's favour** while there was no
rule (65.20% → 67.32%; heavy kit slows the blow). With the rule it comes down to 65.35% and
9.32% of the player's warriors drop their weapon during the fight.

### The rule broke two locked tables

Because disarming affects **everyone** who fights against armour, previously locked
measurements shifted — when the rule is switched off (`--disarm-chance 0 --disarm-catch 0`)
the old numbers come back exactly (3v3 65.20%), so the only thing that moved is this rule.

- **Poison:** the poisoned tantō also drops its weapon while striking ō-yoroi (20.18%). The
  new table is katana 75.02% / 60.81%, poisoned tantō 74.26% / **69.39%**. Poison's claim
  still stands, but **"against a poisoner, wearing heavy armour is a liability" is no longer
  true**
- **Catching:** katana 75.02% / jitte 78.00% / sai 78.88%. Catching implements are now ahead
  on victory too in front of a sword-carrying enemy

These corrections were written into the two relevant sections of GDD §7.

### New tools feeding the measurement

Five scenarios (`blade-armored`, `club-armored`, `spear-armored`, `jitte-armored`,
`3v3-armored`), five knobs (`--disarm-chance`, `--disarm-catch`, `--disarm-armor-share`,
`--drop-distance`, `--pickup-radius`) and three counters: the rate of dropping one's weapon,
the rate at which dropped weapons are picked up, and **the enemy's** disarm rate. The last
stands apart because nobody disarmed the enemy who hit a plate and lost their weapon — it is
the only event with no disarmer.

New state: `CombatState` did not change (being disarmed is not a state but a flag),
`CombatantSnapshot.Disarmed` (the HUD writes "· unarmed"), `RigReactionKind.WeaponLost`,
`GroundWeapon`. New test file: `DisarmTests` (12 tests). 271 tests green in total.

> **Two bugs that came up along the way:** `ArenaPoint.MovedAwayFrom` was clamping the
> requested distance by the distance to the source (the direction had not been reduced to a
> unit vector), which is why the throw distance appeared to do nothing in the first
> measurements. `Combatant.Weapon` was producing a new `Weapon.Fists()` on every read; that
> meant ~109 KB allocated per fight and `ThroughputTests` caught it — reduced to a single
> static instance.

**Nothing left open in #4-B.** A permanent cost for equipment (spare weapons, repair) is
deliberately **absent**: a dropped weapon comes back at the end of the fight.

---

## Armour wears down and falls apart (2026-09-03)

The last end of equipment closed: the weapon drops and is picked up, **armour wears down and
goes**.

The rule: a piece wears down by as much damage as it stops — every point it absorbs comes off
its own durability pool. When the pool runs out the piece falls apart in the middle of the
fight, that region is left bare and the piece is **permanently gone**. Wear belongs not to the
fight but to **the warrior**: it is not exhausted in a single fight, it accumulates across
expeditions (`Warrior.ArmorWear`), and the fight reads it but does not write it — writing it
into the permanent state is the dojo layer's job, the same route as limb loss.

Durabilities: keikogi 40, dō-maru 110, ō-yoroi torso 180, kote 45 / heavy kote 75, suneate
45 / heavy suneate 75, kabuto 90. The single balance knob is `ArmorDurabilityScale`.

### The kit that absorbs the most is the kit that wears the most

3v3, 20,000 fights, `losing:0.7`:

| Kit | Wear per fight | Team durability | Lifetime |
| --- | --- | --- | --- |
| Light keikogi | 5.4 | 40 | ~7 fights |
| Dō-maru | 20.2 | 290 | ~14 fights |
| Ō-yoroi | 38.7 | 570 | ~15 fights |

Ō-yoroi absorbs seven times the damage of light kit and carries a little more than seven times
the durability: expensive kit buys protection, it does not get it for free.

### The moment of falling apart: limb loss doubles

At default pools no piece falls apart in a single fight (zero in 20,000 fights) — falling apart
comes from **taking the field in worn kit**. It was measured by lowering the pool scale:

| Scale | Pieces destroyed (per warrior) | Victory | Limb loss |
| --- | --- | --- | --- |
| 1.0 | 0.00 | 70.27% | 0.83% |
| 0.25 | 0.12 | 69.92% | 0.94% |
| 0.1 | 0.89 | 67.55% | **1.93%** |

Entering a fight in worn armour is not only about taking more damage; the limb loss of a warrior
whose plate falls apart doubles. The cost of not renewing the kit shows up in the roster.

### The end of the chain: armour that is gone does not disarm either

A piece that falls apart leaves behind both its weight and its hardness. With the yokai in full
kit the rate at which the player's weapon is dropped is 1.81%, and with worn kit it is 1.41%
(victory 65.35% → 63.83%): as the armour goes, the fight becomes more lethal but cleaner — more
cuts, fewer weapons dropped from the hand.

### New state and tools

`ArmorPiece.Durability`, `ArmorWearSet` (a value type — the summary is produced per warrior, and
allocating a dictionary would slow the measurement down), `Warrior.ArmorWear`, `HitLocationSet`,
the `ArmorDestroyed` event, `CombatantSnapshot.DestroyedArmor` (the rig strips the kit from here),
`RigReactionKind.ArmorShattered`, the `--armor-durability` knob and two counters (wear per fight,
rate of pieces falling apart). New test file: `ArmorDurabilityTests` (7 tests).
278 tests green in total.


---

## Block became a separate state (2026-09-03)

Open Decision **#12 closed.** Block counted as a separate state in GDD §5 but in the core it
dissolved inside the Defence stat; the document carried this as "differs from the code (open)".
The difference is closed: `CombatState.Blocking` is a real move.

The rule and the table of numbers are in GDD §5. In brief: the decision comes out of the Defence
stat (no base — Defence 0 never blocks), the condition is reading the incoming blow, the stance
lasts 0.8 s and during it the warrior does not strike, 70% of the damage × the weapon's block
quality is erased, no limb is severed, and 75% of blunt concussion gets through.

### The first version ran the rule backwards

When the stance was taken merely on "is there an enemy in range" it was taken blindly: the warrior
still meets 0.20 blows each, but it **lowered victory** (71.21% → 70.50%). Every stance taken for
nothing ate into the attack cycle; block was not the payoff of the Defence stat but its penalty.
Once the condition was turned into "read the incoming blow" (`AttackWindup` or a running charge)
the sign flipped: **72.39% victory, death 50.44% → 49.19%, limb loss 5.19% → 4.96%.**

### Chained blocking was locking up the fight

When the die was rolled again at every decision step, a warrior with high defence would block over
and over and never strike — the 47 tests running with fixed dice caught it instantly. The rule: a
block does not follow a block. The stance was tied to a rhythm — meet it, then answer.

### The rule has no brake of its own

The bigger `MaxBlockChance` grows, the more the player wins one-directionally: 71.61% at 0.25,
72.39% at 0.45, 73.05% at 0.70, 74.08% at 1.0. There is no brake inside; the brake comes from the
Defence stat competing against the other stats in the dojo. The number was left to Phase 9.

### The catching implement was not favoured with block

Jitte/sai had been given as much block quality as a two-handed weapon; the measurement showed
this **broke a locked brake** — the jitte stopped being the wrong choice in front of an enemy
carrying a heavy weapon (jitte-heavy 35.38% > katana-heavy 34.96%). The override was removed.

---

## Target selection became a decision (2026-09-03)

`FindTarget` was a one-line sort: pick the nearest, stay loyal until they die. Now a warrior
scores the enemies at every decision step — distance, wounds, bare regions, the pile-up of
teammates, and the cost of changing direction. The weights are in `CombatTuning` and the scoring
is **deterministic** (no dice): the randomness is not in the decision itself but in the warrior's
identity.

The table is in GDD §4.

### Without an opportunity window the rule was a flat difficulty increase

An unbounded wounded weight walked a warrior past the intact enemy next to them all the way to
the wounded one at the far end of the arena, eating free blows along the way. One-directionally
down: 72.29% at weight 0, 72.28% at 40, 72.03% at 80, 71.77% at 120, 70.74% at 200. Once the
wounded and bare-region gains were tied to a window falling to zero at 200 units the curve
straightened out: **72.17%** (old rule 72.28%) and against a fully kitted enemy the new rule is
clearly better — `3v3-armored` 71.84% → **72.68%**.

The death rate still rises (49.11% → 50.30%): a focused team kills more and the fight ends more
sharply.

### The bare-region weight is dormant for now

At default durability no piece falls apart in a single fight, so where the weight bites is kit
worn down over an expedition. Measured with worn kit, the effect is small and against the player
(70.06% → 69.91%) — the enemy sees the bare region too.

### The measurement made us pay a performance debt

With the scoring running every tick, 10,000 fights went to twice the budget (19.86 s / 10 s) and
allocation per fight jumped to 350 KB. Two fixes: the target is only chosen at decision steps
(the walk cycle inherits the choice), and `HitLocationSet.Count()` now uses bit counting instead
of an iterator.

### The locked tables were preserved — but their margins thinned

| Comparison | Before | After |
|---|---|---|
| cutting / blunt (blade / club) | 92.52% / 92.51% | 92.25% / 92.90% |
| jitte / katana | 78.00% / 75.02% | 78.70% / 78.28% |
| sai | 78.88% | 80.55% |
| jitte-heavy / katana-heavy (brake) | 35.22% / 37.84% | 34.72% / 34.96% |
| jitte-armored / katana-armored (brake) | 41.38% / 60.32% | 35.44% / 60.77% |
| poisoned / katana, against an armoured enemy | 77.19% / 68.62% | 71.85% / 60.77% |

The directions hold: blunt is level with cutting, the jitte beats the katana, both brakes stand,
and poison is still the only right answer in front of armour. But **the jitte's margin fell from
2.98 points to 0.42, and the heavy-weapon brake from 2.62 points to 0.24** — block distributes
part of the catching implement's job to everyone. In an open fight the poisoned blade was 0.90
points behind the katana; now it is 2.04 points behind. These three numbers must be revisited in
Phase 9.

### New state and tools

`CombatState.Blocking`, the `BlockRaised` / `AttackBlocked` events, `Weapon.BlockFactor`,
`WarriorBattleSummary.BlocksPerformed`, `RigReactionKind.Block` + the `Guard()` pose,
six new tuning knobs (`MaxBlockChance`, `BlockSeconds`, `BlockDamageReduction`,
`BlockDismembermentShare`, `BlockStunShare`, `BlockStaminaCost`) and five target selection
weights (`TargetDistanceWeight`, `TargetWoundedWeight`, `TargetExposedWeight`,
`TargetCrowdPenalty`, `TargetStickiness`, `TargetOpportunityRange`). On the sim side
`--block-chance`, `--block-seconds`, `--block-reduction`, `--target-wounded`,
`--target-exposed`, `--target-crowd`, `--target-sticky` and a per-warrior block counter.

New test files: `BlockTests` (8 tests), `TargetSelectionTests` (6 tests).
292 tests green in total, 0 warnings.

---

## The dojo entered the core (2026-09-04)

Phase 3's first piece: **the roster, the day cycle and the save system**. All three live under
`Domina.Core/Dojo` and have no dependency on the engine — no interface, no Godot.

### The roster carries the state the fight does not know

`Warrior` stayed as the permanent state the fight reads; `RosterEntry` came alongside it and
carries the meta state: remaining infirmary days, that day's occupation, completed training days.
The reason for keeping them apart is the architectural rule — the fight resolver does not know
about the calendar, and the batch simulation runs the same warrior tens of thousands of times,
where there is no such thing as a "day".

`Roster` enforces two rules:

- **The dead are not deleted.** Permadeath is permanent, but the warrior's name, honour and
  disabilities stay on the record. Aliveness is read from `Warrior.IsAlive`.
- **Name uniqueness only among the living.** GDD §6's rule: when X dies the name returns to the
  pool and a new X may come along later. This is what lets the chat command (`!ronin-<name>`)
  resolve to a single target.

### The day cycle is deterministic

`DojoState.AdvanceDay()` closes a day: it processes training counters, erodes infirmary days,
and shifts honour one step towards neutral. There is **no** randomness — the same state with the
same calls gives the same result. Entering an encounter also takes a full day (GDD §10), so the
expedition layer will make the same call when the fight ends; it is not called twice per day.

Honour decay does **not** overshoot to the other side of the threshold: if the distance to
neutral is smaller than the step, the warrior settles exactly on 50. Otherwise honour would
oscillate around neutral.

The closed day returns a `DayReport` (who left the infirmary, who trained) — the input for the
"what happened today" screen.

### Save: the file does not carry the balance

The save is a separate family of types (`DojoSnapshot` and friends), not a serialised form of the
live model. The reason is a measurable danger: if the live model were written directly, every
balance field (reach, limb severing multiplier, block quality, armour durability) would enter the
file and **an old save would bring back the old balance** — the player would restore from their
save a number that was fixed in the next patch. Only what the player produced is written to the
file: who, under what name, with what stats, wearing what, having lost what.

All three of GDD §2's rules were tied to tests:

| Rule | Its counterpart |
| --- | --- |
| Versioned | `DojoSnapshot.CurrentVersion`; a file from a newer version loads but leaves a warning |
| Merge-on-load | Missing fields load with their defaults, unrecognised fields are ignored |
| try/catch | `Load` **never throws under any circumstances**; corrupt text returns a failed `LoadResult` |

A single corrupt warrior record does not take the rest of the roster with it: a clashing id is
skipped, a nameless record is given a name, a second living warrior with the same name is
renamed — all of it written to the warning list. The cost of merge-on-load is that the file
silently loads incomplete; the warnings are carried precisely to make that visible.

### Deliberately not written

- **Training does not touch stats.** It only counts days. Inventing a number before the effect is
  measured and locked would be a balance debt that is hard to remove later.
- ~~**No speeding up recovery with medicine.**~~ Written on 2026-09-04 — see below.
- **No encounter offer.** That is Phase 4's job; the day cycle stands ready to wait for it.

### The fight's tally is written into the roster

`BattleAftermath` is the **single place** that takes the fight's outcome and turns it into
permanent state. The core still does not touch the permanent state — death, limb loss, destroyed
armour and absorbed wear are each a report in the fight summary; they become irreversible here.
The separation is required by the architectural rule: the batch simulation runs the same roster
tens of thousands of times and in none of them may a warrior's permanent state be corrupted.

| Input | Effect on the roster |
| --- | --- |
| `Died` | `Roster.Kill` — permadeath; infirmary days and honour are not processed |
| `LostParts` | Permanent disability; the same limb is not lost a second time |
| `ArmorWear` | **Added** to the warrior's wear ledger (accumulates across expeditions) |
| `DestroyedArmor` | The piece leaves the kit and that slot's wear is **reset** |
| Remaining health + number of limbs | Infirmary days |
| Hit rate, escape | Honour via `HonorEngine` |

The counter of a destroyed slot had to be reset: wear belongs to **the piece**, not the slot. Had
the counter stayed, a brand-new piece fitted in its place would inherit the destroyed one's
ledger and fall apart on the first blow.

The team filter is a rule too: the yokai carry their own identities and those identities **can
clash** with those in the roster. Without the filter, an arm lost by an enemy could be written to
a warrior in the dojo.

The infirmary day numbers (`RecoveryDaysAtFullDamage` 6, `RecoveryDaysPerLostLimb` 5,
`RecoveryFreeDamageShare` 0.25) are **not locked** — GDD §7 only says "according to the weight of
the wound". Without a free damage share, every fight would mean a day in the infirmary and the day
cycle's real decision (expedition today, or training) would disappear by itself.

New test files: `DojoTests` (15 tests), `DojoSaveTests` (13 tests),
`DojoAftermathTests` (16 tests). 336 tests green in total, 0 warnings.


## 2026-09-04 — The economy numbers were measured and locked (Open Decision #5)

The treasury layer went in and the prices were closed by measurement. New files:
`Domina.Core/Dojo/EconomyTuning.cs` (every number in one place),
`Domina.Core/Dojo/Quartermaster.cs` (the single gate separating the price question from the
purchase), `Domina.Sim/CampaignRunner.cs` (the measurement tool that plays the dojo day by day).

### Why an expedition series was measured rather than a fight

The economy cannot be locked by looking at a single fight: the cost of armour accumulates across
expeditions, an infirmary day eats not income but **time**, and the replacement for a dead warrior
also comes out of the treasury. The unit of measurement therefore became not the fight but the
dojo's lifetime — `Domina.Sim --mode campaign` plays the same roster for days and looks at the
curve of the treasury. A fixed policy plays in the player's place (repair, renew, hire, go on
expedition); the policy does not need to be clever, it needs to be **the same**.

### The measurement was done in a separate scenario: `patrol`

All the existing scenarios are balance probes — deliberately set up heavy so that the edge of a
rule can be seen, with death rates per warrior-fight in the 38-49% band. Such a fight cannot be
had every day: the roster cannot bear a funeral a day, and a price measured with that roster
actually measures the price of a warrior, not the price of armour or medicine. `patrol` is the
ordinary day's encounter: victory 98.6%, death per warrior-fight 7%.

### The locked numbers (1000 dojos × 60 days)

| Item | Number |
| --- | --- |
| Victory reward | 0.45 gold / enemy health (withdrawal and rout: 0) |
| Armour piece | 1.50 gold / durability point |
| Repair | 0.90 gold / wear point |
| Food / water | 2 / 1 gold, 1 each per warrior per day |
| Medicine | 12 gold, 1 per day per warrior in the infirmary |
| Hiring a warrior / starting capital | 150 / 600 gold |

Result: net **31.3 gold** per fight, daily consumption 34.5, idle days 33.8%, hungry days 4.4%,
dojos that preserved their capital 66.4%, dojos that closed 1.2%.

### Three things the measurement revealed

1. **The binding constraint is not gold but the roster.** Once encounter difficulty goes above 20%
   death per warrior-fight, no price adjustment keeps the dojo standing — all the income goes into
   replacing warriors. The difficulty curve (GDD §10) is at the same time the economy's curve.
2. **The price of medicine sets the calendar.** Two thirds of daily consumption is medicine. With
   free medicine idle days are 28.3%, at 12 gold 33.8%, at 24 gold 59.1% — this is the item that
   creates the "expedition today, or the infirmary" pressure.
3. **The reward curve is narrow.** At 0.35, 14.3% of dojos preserve their capital; at 0.70 the
   treasury climbs to 3719 gold and money stops being a constraint. 0.45 is the knee between the
   two.

### Two rules that came out of the measurement

- **Repair is always cheaper than replacement** (0.90 < 1.50). Had it been equal or dearer, there
  would be no decision called repair. In the measurement the two extremes are almost level (an
  early repairer finishes with 959 gold, someone who uses a piece to the end with 1012) — but the
  one who uses it to the end loses the piece **in the middle of a fight**. With gold equal,
  choosing the risk is the player's decision.
- **The price of scarcity is time, not death.** If the store falls short, those in the infirmary
  eat first; a hungry warrior neither heals nor trains that day, but nobody starves to death. The
  order could not be arbitrary: leaving the wounded hungry would turn scarcity into a penalty with
  no recovery.

### Medicine now really heals

A day without medicine erodes one infirmary day, a day with medicine two. Medicine is therefore
not a compulsory tax but an accelerator — this closed Phase 3's "infirmary/physician" item.

Verification: build 0 errors / 0 warnings, `dotnet test -c Release` 353/353 green (+17:
`EconomyTests` 12, `CampaignRunnerTests` 5). `dotnet format` clean on the new files.


## 2026-09-04 (second round) — The day's encounter offer (entering Phase 4)

New folder `Domina.Core/Campaign`: `Bestiary` (scalable yokai templates),
`EncounterGenerator` + `EncounterTuning` (the day's offer and the difficulty curve),
`EncounterOffer` (threat band, rough description, imposed team size), `Expedition` (the single
bridge that turns the offer into a fight).

### The offer is not kept in the save

Generation is a **pure** function of the day and the expedition's seed. It gains two things: the
save file does not carry the bestiary (an old save cannot bring back the old balance, GDD §2) and
the player cannot change an offer they dislike by reloading the save. `DojoSnapshot` only gained a
`Seed` field — the format was not broken, missing fields load with their defaults.

### The bridge is in one place

`DojoState` does not set up the fight and `Battle` does not close the day. `Expedition.Send` does
all three in order: it runs the fight, writes the result into the roster (`BattleAftermath`), pays
the reward and **closes the day itself**. An expedition eats a day, even if you flee (GDD §10) —
this is the item that closes the "go in, look, run" loop. `Expedition.Refuse` turns the team back
with its reason: an empty team, yesterday's offer, the wrong team size (a duel wants exactly one
warrior), a warrior not in the roster, a warrior in the infirmary.

### Measurement: is "take it or leave it" really a decision

`Domina.Sim --mode campaign --offers on` now plays generated offers instead of a fixed scenario.
500 dojos × 120 days, the only difference being the policy's right to filter offers:

| Policy | Dojos closed | Days survived (median) | Death / warrior-fight | Hungry days | Refused |
|---|---|---|---|---|---|
| Takes every offer | 99.2% | 54 | 9.2% | 7.1% | 0% |
| Refuses heavy offers when the roster is short | 0.4% | 120 (all of them) | 7.1% | 50.8% | 67.7% |

The two failures are opposites of each other: the dojo that never refuses loses **its roster**, the
one that refuses everything heavy loses **its treasury**. Refusing is not an escape but a trade
between a day and a warrior.

### A rule corrected during the measurement

In the first version a crowded offer's power was **divided** among the enemies (`power / √count`).
The result: the same threat was carried with less health, and since the reward depends on enemy
health (§11) a crowded offer sold the same risk at half price — in the measurement 99% of dojos
were closing. A crowd no longer divides the power: three enemies are three times the enemy, three
times the reward.

### Deliberately not written

- **The curve's numbers were not locked.** The measurement says what was measured, not which curve
  is right; the curve will close in Phase 9's balance round.
- **No yokai behaviour.** The bestiary holds only numbers (behaviour is Open Decision #3's other,
  still-open half). When a field is added to the template, encounter generation will not change.
- **No random events** (GDD §11) — the economy's only remaining open item.

Verification: build 0 errors / 0 warnings, `dotnet test -c Release` 368/368 green (+15:
`EncounterTests` 12, `CampaignRunnerTests` +3). `dotnet format` clean on the new files.


## 2026-09-04 (third round) — Random events (GDD §11's last item)

`Domina.Core/Dojo/RandomEvents.cs`: a 15% chance of a mishap per day. Five kinds — theft, spoiled
provisions, the well going foul, medicine going mouldy, illness. All of them take away; there are
no donations or treasure, because §11 describes events as **pressure on the buffer** and a
two-way table would remove that pressure.

### The effect had to not land on the store

The first event that comes to mind is "provisions were stolen", but the daily shopping fills the
store to exactly what is needed (`Quartermaster.Restock`), so stock is nearly always zero — the
value of stolen provisions would be zero too. Events therefore hit one of three places: **the
treasury** (theft), **that day's bill** (spoiled provisions, a foul well, mouldy medicine) or
**the calendar** (illness).

The mishap is processed inside `AdvanceDay` **before** upkeep: spoiled provisions should make that
day's shopping more expensive, not be deferred to the next day.

### A separate salt

Like the offer, the event is a pure function of the day and the seed, but the mixing constant is
separate. Had the same salt been used, theft would always fall on the day a heavy offer arrived and
the two systems would collapse into one. A test holds this: event days are not a subset of heavy
offer days.

### Measurement (400 dojos × 60 days, `patrol`)

| Daily event probability | Final treasury | Preserved capital | Hungry days | Dojos closed |
|---|---|---|---|---|
| 0% | 945 | 66.2% | 4.8% | 1.2% |
| **15% (chosen)** | **766** | **58.5%** | **6.7%** | **2.2%** |
| 30% | 562 | 44.8% | 9.4% | 3.0% |

At 15% roughly a fifth of the buffer goes to mishaps. The pressure is felt but the item that
determines the treasury is still medicine and warriors — events do not end the game on their own.

One more thing was seen during the measurement: in offer mode (where the dojo already lives with an
empty treasury) mishaps have almost no effect — there is nothing to steal from an empty treasury.
That is, the event table punishes **the wealthy** dojo, not the one that is sinking. That is exactly
what pressure on the buffer means.

Verification: build 0 errors / 0 warnings, `dotnet test -c Release` 378/378 green
(+10: `DayEventTests`). `dotnet format` clean on the new file.


### Addendum (same day) — the severity became random, rust was dropped

Two corrections came in:

- **The rust event was removed.** Kit wear already comes from the fight; a second source was not
  needed.
- **The rates are not fixed.** The written numbers are now **upper bounds**: theft takes at most 12%
  of the treasury, spoilage and the foul well at most twice that day's bill, illness at most 3 days;
  the actual amount is drawn each time between zero and the upper bound. Rationale: a fixed rate
  turns a mishap into a calculable tax, and if the player knows the loss in advance, holding a buffer
  becomes arithmetic rather than a decision.

Measurement (400 dojos × 60 days, `patrol`, 15%): final treasury 815 gold, preserved capital 60.8%,
hungry days 6.1%, dojos closed 1.5% — as expected, slightly lighter than the fixed-rate version
(766 / 58.5%), because the loss now averages half the upper bound.

Test count 378 (`DayEventTests` 10 tests; the rust test was dropped, a test holding that the severity
is not fixed was added).


### Decision: theft should fall flat on an empty treasury (2026-09-04)

The measurement had noted "a dojo with an empty treasury is unaffected by theft"; it became clear
that this is not a shortcoming but **a decision**. Having an empty treasury is not a permanent state
— when the player saves up for armour, repairs or a new warrior the treasury fills and theft bites
at exactly that moment. The event thereby becomes the price of the decision to save up.

Two alternatives were considered and **rejected**:

- A thief going for the provisions when the treasury is empty (raising that day's bill): it would
  punish a sinking dojo a second time and would erase the event's meaning of "it hits what you have
  built up".
- Building a real stock buffer with bulk buying + store capacity: the idea stands, but it adds a new
  decision to the economy and therefore wants its own measurement round — it was not mixed into
  today's locked prices.


## 2026-09-04 (fourth round) — The warrior market

Domina's slave market was researched and its model taken over as-is (sources: Steam discussions and
player guides). The three rules there: candidates come with random stats and the stats are visible
before purchase, the quality of the market follows the player's current roster, and the growth rate
per level varies from warrior to warrior.

New file `Domina.Core/Dojo/RecruitMarket.cs`; `Warrior` gained a `Talent` field (it goes into the
save, the fight does not read it — training will).

### Three rules went into the code

- **The price comes out of the stats.** The candidate's score relative to a base warrior sets the
  price; talent enters the price too, but at half weight (talent is a promise, stats are what you
  have in hand).
- **The market follows the roster** (70%). If the roster dies out entirely the market falls to
  recruit level, not to zero — so that the recovery route of a collapsed dojo is not closed.
- **The list refreshes every two days and is frozen within a day.** Had it not been frozen, since the
  market follows the roster average, buying one candidate would change the remaining candidates: the
  player could buy a cheap one and reroll the list as often as they liked. A test holds this.

### The measurement revealed two things (400 dojos × 60 days, `patrol`)

| Hiring policy | Final treasury | Preserved capital | Deaths (per dojo) | Dojos closed |
|---|---|---|---|---|
| Fixed price, fixed stats (old) | 815 | 60.8% | 6.21 | 1.5% |
| Most stats per gold from the market | 354 | 26.5% | 7.16 | 11.8% |
| The best the money can buy from the market | 705 | 52.2% | 5.93 | 4.8% |

1. **The old model turned out to be subsidising replacement:** for 150 gold you got a warrior of
   veteran quality. Once the market pulls this to a real price the dojo struggles — this is not a
   balance break but a hidden subsidy becoming visible.
2. **The "buy cheap and raw, then train" strategy currently loses** because a raw candidate does not
   grow: training's stat effect has not been written. The two strategies being rivals is the design's
   goal; the 354 against 705 gold gap is the measure of the gap the training system has to close.
   That is the next job.

`Domina.Sim` gained two new knobs: `--market on|off` and `--market-pick value|best`.

Verification: build 0 errors / 0 warnings, `dotnet test -c Release` 388/388 green
(+10: `RecruitMarketTests`). `dotnet format` clean on the new files.


## 2026-09-04 (fifth round) — The market ceiling and bounties

### The market can no longer exceed the roster's best

The market followed the roster **average** but had no ceiling: when the variance hit from above, a
single candidate could exceed the best warrior in the dojo. If a trained warrior can be bought,
training has no meaning left.

`MarketAnchor` is a new type: the base (where it settles) and the ceiling (how high it can go) are
two separate questions. The ceiling follows **the best living warrior** (`BestFollowCeiling` 0.75),
not the average — had it been tied to the average, the market could be exploited by buying two cheap
recruits and lowering it. A candidate over the ceiling is **not clipped but scaled**: clipping turns
every candidate leaning on the ceiling into the same flat profile and would kill off the question
"which one should I buy".

**The measurement caught a flaw.** In the first version the ceiling was a flat `0.75 × best`; since a
recruit's score is 355, even in a recruit roster every candidate came out 25% weaker than a recruit.
**All** 400 dojos zeroed their treasury and idle days rose to 93.7%. A recruit floor was added to the
ceiling: `max(Score(recruit), best × ratio)`.

The cost of the ceiling (400 dojos × 60 days, `patrol`):

| Ceiling | Policy | Final treasury | Preserved capital | Deaths | Closed |
|---|---|---|---|---|---|
| none | value | 354 | 26.5% | 7.16 | 11.8% |
| none | best | 705 | 52.2% | 5.93 | 4.8% |
| 0.75 | value | 147 | 9.8% | 7.75 | 18.8% |
| 0.75 | best | 204 | 13.0% | 7.08 | 13.2% |
| 1.00 | best | 385 | 27.3% | 6.65 | 7.0% |

The ratio was locked at 0.75. On the `patrol` roster, 0.75, 0.85 and 0.90 give **the same** result:
the best warrior's score is 387 and the recruit floor is 355, so the ceiling only starts to speak
once the score passes ~473 — and you only get there through training. What was measured today is the
cost not of the ratio but **of the ceiling itself**.

### Bounty contracts

New file `Domina.Core/Campaign/Bounty.cs`. A timed contract with a named target, standing alongside
the daily offer. The detailed rationale is in GDD §11; the decisions the code holds:

- The target is **named and single**; the team size is not imposed, how many you send is a decision.
- A contract is posted every 4 days and stays open for 3 — **days with no contract** exist on purpose.
- Accepting does not eat a day, it buys **time**. If you cannot come back, the whole roster loses
  honour.
- The **team** that brings the head gains honour; the debt of a broken promise is written to **the
  roster**.
- A contract whose head has been taken comes off the board. Without this record the same target
  appeared posted again the next day — the measurement showed 12.7 bounties per dojo, the board was a
  money tap.
- Generation is pure; only "was a promise made" and "was the head taken" are written to the save.

The expedition layer goes down the same fight route via `Expedition.SendToBounty`: opening a second
door to the fight would split the resolver in two.

**Measurement (400 dojos × 60 days, `patrol`, offer mode, market on):**

| Acceptance limit | Bounties | Final treasury | Preserved capital | Deaths | Closed |
|---|---|---|---|---|---|
| `dire`, no bounties | 0.00 | 4 | 0.0% | 8.37 | 75.5% |
| `dire`, bounties on | 7.44 | 1 | 0.0% | 9.62 | 80.8% |
| `rising`, no bounties | 0.00 | 393 | 20.8% | 1.26 | 0.0% |
| `rising`, bounties on | 0.69 | 412 | 22.2% | 1.25 | 0.0% |

The shape is the shape we want: **whoever chooses wins, whoever jumps at every job sinks.** But the
numbers side stayed open: a `PowerMultiplier` of 1.8 pushes the target into the `Heavy`/`Dire` band
on most days, and a selective dojo enters only 0.69 contracts in 60 days. The system works correctly
but is almost invisible; sweeping power together with the reward multiplier was left to the training
round.

`Domina.Sim` gained three new knobs: `--market-ceiling <ratio>`, `--bounty on|off` and a "Bounties"
line in the report.

Verification: build 0 errors / 0 warnings, `dotnet test -c Release` 401/401 green
(+13: `BountyTests` 10, `RecruitMarketTests` +3). `dotnet format` clean on the new files.


## 2026-09-04 (sixth round) — Three orphaned screens

After the roster screen, the three remaining screens were written: **the market**, **the school** and
**the day's offer**. No new rules — all three put glass in front of rules that already stand in the
core.

### The decisions belong to the model, not the screen

The pattern from the roster screen was preserved: `Domina.Presentation` decides what will be written
and what will be disabled (engineless, tested), and the Godot side builds nodes and prints. Three new
models:

- **`MarketModel`** — the order is by **price**, not by the treasury: affordability changes as gold is
  spent, and if the stall were re-sorted on every purchase the player would lose track of where a
  candidate was. A row carries its position relative to the roster (`BetterInRoster`), because the
  market's question is not "is this candidate good" but "is he better than what I have". Talent is
  read as a **band, not a number** (`TalentBand`): stats are what you have, talent is a promise —
  writing "1.23" would present it as a measured stat. The score formula is exactly the same as the
  market's ceiling calculation, otherwise the screen could not explain why a candidate was scaled
  down.
- **`SchoolModel`** — a disabled node has **two separate reasons**: its turn has not come, and you
  cannot afford it. One is opened by waiting, the other by earning; they cannot be shown with the same
  greyed-out button. `School.Available()` only knows the order, not the treasury — the model makes the
  distinction, and the gold shortfall is written on the row too. A locked node is not hidden: the
  school is a long-term investment and the player cannot save up without seeing what they are saving
  for.
- **`OfferModel`** — it does **not carry** the enemy roster at all. The offer object holds it (so that
  the fight is set up with the same roster) but per GDD §10 only the band and the rough description
  are read before entering; since the model does not carry the roster, the screen cannot print it even
  by accident. The verdict on going on an expedition is read from `Expedition.Refuse`; a second rule
  set was not written — had it been, the two sides would drift apart and the button would start
  closing off an expedition that could be sent.

### Four screens, one dojo

`DojoScreen` is the common skeleton (background, margins, a navigation bar at the top); `DojoHub`
moves between the four over a single `DojoState`. Being one object is essential: a warrior bought at
the market must show up immediately on the roster screen, and a facility bought at the school in the
day's accounts. When the screen changes, the new one is built from scratch — a screen that is hidden
and shown again would print the old day.

All commands go through the core's own gates: `Quartermaster.Hire`, `DojoState.BuySchoolNode`,
`DojoState.AcceptBounty`, `Expedition.Send` / `SendToBounty`, `DojoState.Decline`. The expedition
flow is derived from the day and the seed; a random seed would open the door to "load the save and
reroll the fight".

**Two items left open:**

1. The fight is for now resolved **in the background** and its result written as a tally. Watching it
   in the arena will come with the expedition flow being connected to the arena (Phase 4); since the
   rules side goes down the same route, the result will not change.
2. Buying the same candidate twice within a day is for now prevented by **the screen**. Because the
   stall is frozen for the day (`DojoState.Recruits`) the core does not record this; if it needs to be
   recorded, its place is the core.

New scene `src/Game/dojo.tscn` (`main.tscn` is still the arena demo).

Verification: build 0 errors / 0 warnings, `dotnet test -c Release` 482/482 green
(+28: `MarketModelTests` 9, `SchoolModelTests` 7, `OfferModelTests` 12). `dotnet format` clean on the
new files.

## 2026-09-04 (seventh round) — The fight in the arena, the market every day

### The expedition was connected to the arena

The expedition layer ran the fight itself (`Expedition.Send`), which is why the day screen resolved
the fight in the background and printed a tally. The arena has to step the fight **in real time** —
the player can intervene with the "pull out" command — so setting up the fight, running it and
accounting for it must be callable separately. `Expedition` was split into three:

- `Prepare` / `PrepareBounty` — weighs the team, sets up the fight, **does not run it**. It does not
  touch the dojo (both the day and the treasury stay put).
- `Settle` / `SettleBounty` — closes the books on a finished fight: writes to the roster, pays the
  reward, processes honour and the promise if it is a bounty, and closes the day.
- `Send` / `SendToBounty` — puts the fight between the two; the batch simulation and the tests keep
  using this route.

The accounting staying in one place is essential: had the arena written its own accounting, the fight
that is watched and the fight `Domina.Sim` resolves would leave different results, and the balance
measurement would not have been measuring what is on screen. `ExpeditionSettleTests` holds this — same
seed, same team, same offer: two routes, one dojo (treasury, roster, infirmary days identical).

On the Godot side `BattleArena` can now take a fight from outside (`Bout`) and gives the raw result
when it ends (`Finished`); it does no setup and no accounting. `DayScreen` sets up the fight and hands
it over as `PendingBattle`, `DojoHub` closes the screens and opens the arena, and when the fight ends
a "Return to the dojo" button goes back to the day screen with the tally. If no `Watcher` is given
(the screen was opened on its own) the fight is resolved in the background — the same two calls, with
no arena in between.

### The market refreshes every day, the number of purchases is unrestricted

`RefreshDays` 2 → **1**, `Candidates` 3 → **10**. Rationale: waiting already has a cost — waiting a
day eats a day — so keeping the stall stagnant is a second penalty. **No** ceiling was put on the
number of purchases: the market is open all day and, as long as the treasury allows, more than one
warrior can be bought. The limit is gold, not a counter; otherwise two warriors could not be put in
the place of two dead ones on the same day.

The problem of the same candidate being sold twice was moved **into the core** (the temporary marker
that sat on the screen in the previous round was removed): `DojoState.HireRecruit(index)` records the
index that was bought, the marker drops when the day closes, and the bought indices are written **to
the save file** — otherwise loading the save and buying the same man again would stay open.
`Domina.Sim` now goes through this gate too: if the measurement does not go through the gate the
player plays, what it measures is not the game.

**Measurement (400 dojos × 60 days, `patrol`, `--market-pick value`):**

| Stall | Final treasury | Preserved capital | Death / warrior-fight | Dojos closed |
| --- | --- | --- | --- | --- |
| Every 2 days, 3 candidates (old) | 1254 | 51.7% | 9.6% | 17.5% |
| Every day, 3 candidates | 1288 | 54.5% | 9.5% | 14.5% |
| Every day, 4 candidates | 1184 | 50.5% | 10.0% | 19.2% |
| Every day, 6 candidates | 1258 | 55.2% | 9.8% | 17.0% |
| Every day, 8 candidates | 1301 | 56.0% | 9.7% | — |
| Every day, 10 candidates (new) | 1285 | 55.2% | 9.7% | — |

The differences between the rows are inside the noise for this sample: the stall's frequency and width
are **not a balance lever**; what binds the market is the stat ceiling and the treasury. The number of
candidates was therefore given as a feel decision rather than a balance one — ten candidates, the same
scale as the reference game's stall (2026-09-05). The measurement's own limit is visible too — the
simulation buys at most one candidate a day, whereas the rule allows more than one; "two warriors in
the place of two dead ones on the same day" will only be seen by playing.

`Domina.Sim` gained two new knobs: `--market-refresh <days>` and `--market-candidates <n>`.

### The school's ordering stands

The requirement of an order within a branch (the kata master does not come before the drill hall) was
discussed and **kept**; the screen keeps writing "the training ground first".

Verification: build 0 errors / 0 warnings, `dotnet test -c Release` 495/495 green
(+13: `ExpeditionSettleTests` 4, `RecruitMarketTests` +6, `DojoSaveTests` +1,
`MarketModelTests` +2). `dojo.tscn` opens without errors in headless Godot.

## 2026-09-05 — The save was wired into the game

The dojo is no longer built with `DemoRoster.Dojo()`: the game opens with a **title screen** and the
dojo is either loaded from the slot or built with `NewGame.Create(seed)`. This was the precondition for
playtesting — a 60-day dojo is not played in a single session.

**What was added:**

- `NewGame` (core): 600 gold, an empty store, four warriors. The roster is not written by hand, it is
  drawn from the market's own generator with **a separate seed** — had the same stream been used, the
  candidates standing at the stall on day one would be an exact copy of the roster. The starting setup
  lives in the core so that a measurement run and a played game start from the same setup.
- `SaveSlot` (Godot layer): `user://dojo.json`. Writing is two-step — a temporary file first, then a
  swap; since autosave runs on every change, the "the game closed while writing" window opens often and
  writing directly over the file would erase the expedition.
- `TitleScreen`: continue / new game. When a save exists, "new game" asks **twice** — there is a single
  slot and a permadeath expedition must not be erased with a wrong keypress.
- Autosave: the screens say they changed the dojo via `DojoScreen.Changed`, and the hub is the only
  place that writes. Write points: closing the day, post-fight accounting, accepting a contract, hiring
  a candidate, buying a facility, choosing a path, drills and a name change; plus one final pass on
  shutdown. Shutdown is not relied on **on its own** (crash, power cut).
- Load warnings do not stay silent: if merge-on-load could not rescue something, it is written in the
  day screen's tally (GDD §2).
- `project.godot` now opens with `dojo.tscn`, not the arena demo.

**Verification:** build 0 errors / 0 warnings; `dotnet test` 499/500 — the single red one is
`ThroughputTests.TenThousandBattlesRunWithinTheBudget` (10.11 s against a 10 s budget, measurement noise
in Debug, unrelated to the save work). New tests: `NewGameTests` 5 (starting numbers, seed determinism,
roster ≠ day one's stall, save round trip).

**Next up:** playing it yourself — is the infirmary branch a trap, is a seppuku threshold of 30 right,
does a bounty show up within 60 days, is a ten-candidate stall too crowded.

---

## 2026-09-13 (thirteenth round) — The bow is written, and the range class stops being a promise

**The yumi exists** (`ThrownWeapon.Yumi()`). It is built as the shuriken's opposite on every axis:
range **1200** against 700, damage **26** against 12, **10** arrows against a handful of 4 — and a draw
of **1.5 s** against 0.7, with a slower arrow. A bowman left alone settles the fight before it is
joined; a bowman closed on has spent it drawing.

**The class's depth moved onto the implement.** `UnclassedRangeFactor` (0.85) was always documented as
deliberately shallow — the throwing slot every warrior carries must not become a class tax — with the
note that the real payoff would arrive with the bow. So `ThrownWeapon.UntrainedShare` is new: 1 for
every other thrown thing, **0.45** for the yumi, multiplied into the shared factor. A bow in an
untrained hand is not a slightly worse bow.

| Fight (20.000 each, same master, only the slot and the class differ) | Victory |
|---|---|
| Shuriken, no class | 75.72% |
| Shuriken, kyūdō | 76.78% |
| Yumi, no class | 77.73% |
| Yumi, kyūdō | **82.77%** |

The class is worth **+1.06** points on a star and **+5.04** on the bow. For scale, the poison class is
worth ~11 points on its own blade, so the range class still sits below it — which is the right order
for a class whose implement also outranges everything on the field. The bow is worth **+2.01** to an
untrained hand as well, and that is intended: it is a real weapon anybody can loose badly.

⚠️ **A gap found while writing it: the thrown slot has no shop.** `Warrior.Thrown` is fought with,
saved and loaded, but nothing in the dojo — market, quartermaster, recruit screen — ever hands a
warrior one. So the yumi has no price and a kyūdō cannot buy his own weapon in a running season. It is
a shop gap, not a class gap; it is now a Phase 4 line in `ROADMAP.md`.

**Two Turkish strings were found and fixed** while working here — `"Zehirli shuriken"` in
`Equipment.cs` (a name the game would have shown a player) and a Turkish doc comment in `Injury.cs`.
The repo's rule is English everywhere, the game's own UI included.

**A save bug was closed before it could be shipped:** `ThrownWeaponSnapshot` did not carry the new
share, so a saved yumi would have loaded as a bow anyone could draw. A test pins it.

New scenarios: `yumi`, `yumi-unclassed`. New tests: `YumiTests` (4). 699 tests green under Release
(523 core + 123 presentation + 53 sim).

---

## 2026-09-13 (twelfth round) — The enemy kinds get a manner, and it costs the season nothing

**Open Decision #3 is closed.** The kinds had numbers and no behaviour; the decision had said since
2026-09-03 that the difference would be the target-selection weights tuned per kind rather than a code
path per kind, and that is what was built. `Combat/TargetProfile.cs` holds five **multipliers** over
the five terms every warrior already scores — the road to be walked, the wounded man, the bare region,
the teammate already on that target, the cost of turning away. Absolute numbers were rejected on
purpose: with multipliers a later round that retunes what a bare region is worth moves every kind with
it, and a profile stays a statement about character instead of a second balance table.

`TargetProfile.Default` is all ones, so a field without profiles is bit-for-bit the fight it was
before the type existed — a test pins that, because every combat figure in this file was taken on it.

| Kind | Profile | What it is |
|---|---|---|
| Collector | crowd x0.5, wound x1.2 | He collects in numbers |
| Cutthroat | wound x1.8, bare region x1.5, stickiness x0.6 | He finishes, he does not duel |
| Duelist | wound x0.4, crowd x1.8, stickiness x1.6 | He wants his own opponent |
| Kabukimono | distance x2.0, wound x0.6, stickiness x1.3 | Speed 28 under a tetsubo: the road is expensive |
| Senior student | distance x0.8, bare region x1.4, crowd x0.8 | The yari's reach buys him the choice |
| Kurogane | wound x1.5, bare region x1.6, stickiness x0.8 | He takes whatever is open |

**The dojo's own men carry no profile.** The player directs his side; a hired man choosing his opponent
by temperament would be reading the field against the player's plan.

**Measured against its own absence, and it is zero.** New switch `--enemy-profiles on|off`
(`CombatTuning.TargetProfiles`), six seeds x 1600 dojos x 180 days, each seed paired with its own
control:

| | profiles off | on | paired |
|---|---|---|---|
| Last night won | 13.67% | 13.73% | +0.07 ±0.84 |
| Dojos closed | 28.32% | 28.15% | −0.17 ±1.14 |
| Net per fight | 31.45 | 31.38 | −0.07 ±0.41 |
| Deaths / warrior-fight | 4.78% | 4.82% | +0.03 ±0.05 |
| Fight victory | 97.85% | 97.85% | +0.00 ±0.06 |

**Kept at zero on purpose.** A manner that also moved survival would be a difficulty change wearing a
personality, and the kinds are already separated by stats and weapons. What the round buys is the
lever: if a kind ever has to become a threat rather than a manner, the knob is written, measured and
switchable. The appetites do change the fights themselves — `AnAppetiteChangesTheFight` sweeps twelve
seeds and the same men come out differently — so the zero is an effect that cancels over a season, not
a feature that never fires.

**A note on why this was cheap to measure:** the decision was written into the core, so the whole
question was a sim flag and twelve runs. Had the behaviour been a code path per kind in the engine,
the same question would have needed a playtest per kind.

New files: `src/Domina.Core/Combat/TargetProfile.cs`, `tests/Domina.Core.Tests/TargetProfileTests.cs`
(5 tests). Changed: `Warrior.Targeting`, `Combatant.Targeting` (read once, like the stats),
`Battle.TargetScore` (every term through the attacker's profile), `EnemyKind.Targeting` + the six
kinds, `CombatTuning.TargetProfiles`, `--enemy-profiles` in the sim, GDD S4 (a new "the kinds'
appetites" block) and Open Decision #3.

695 tests green under Release (519 core + 123 presentation + 53 sim).

---

## 2026-09-13 (eleventh round) — The stall is priced charm by charm, and the bed learns its own error

**The round's first finding is about measurement, not about charms.** Halfway through, the same arm
was run on six different seeds and the charmless bed read **11.1 / 13.9 / 14.2 / 13.9 ...** per cent
of last nights won at 1600 dojos. That is a run-to-run spread of about **±1.3 points**, well over the
±0.8 binomial error the earlier rounds quoted — so a single-seed sweep cannot tell a two-point effect
from nothing, and a decision taken this morning on one seed had to be re-taken. **The protocol from
here on: several seeds, and every arm compared against its own seed's control**, reporting the paired
mean. The RNG is not at fault (xoshiro256** per campaign, stride 1,000,003); what is at fault is
reading one number as if it had no variance of its own.

**Open Decision 20 is closed, and larger than it was opened.** The question was what to do with the
two charms that had been given jobs but still did not pay for their 120 gold. Measured charm by charm
(six seeds x 1600 dojos x 180 days, each against its own charmless control, charmless bed **13.7%** of
last nights), the shelf turned out to be mispriced at both ends:

| Charm | 20g | 30g | 40g | 60g | 80g | 100g | 120g |
|---|---|---|---|---|---|---|---|
| Iron gate (defence) | | | **+10.5** | +7.9 | | | **+3.4** |
| Swift foot (evasion) | | | +7.6 | | **+3.8** | +2.6 | +2.1 |
| Steady hand (accuracy) | | | **+3.7** | +1.2 | | | **−2.1** |
| Long breath (stamina) | | | **+3.7** | | | | +1.1 |
| Quiet mind (will) | +3.1 | **+2.8** | +2.2 | | | | +0.6 |

(points of last nights won over the same seed's charmless control; ±0.5-1.7 across the six seeds)

**The rung is read in nights won, not in net.** This morning's working rule — "the charm should pay
its own gold back" — puts iron gate at 60 and makes it a landslide (+7.9). A charm's job is to turn
gold into victories; the net it costs per fight is the price of that, not a fault. At the rung
(about +3.5 points) the five land at **iron gate 120, swift foot 80, steady hand 40, long breath 40,
quiet mind 30**.

**One price for all five fails in both directions**, which is why §10's equal-price rule is now
superseded: at 120 gold steady hand is **worse than wearing nothing** (−2.1 nights, −5.8 net) while
quiet mind and long breath are noise, and at 40 gold iron gate takes the dojo from 13.7% of nights to
**24.2%** and closes 4.9 points fewer dojos — one right answer, and no shelf. The design's own intent
(five answers to five weaknesses, the man deciding which) needs prices proportional to strength.
Quiet mind reads the same at 20 and 30 inside the noise, so the dearer price was taken.

**The difficulty tiers were re-swept and the multipliers stand.** The tenth round left GDD S10's tier
table marked "partly void" because the curve moved under it when the stamina pool was made to bind.
Six seeds x 1600 dojos per tier:

| Tier | last night won | dojos closed | net / fight | deaths / warrior-fight | mastery, best man |
|---|---|---|---|---|---|
| Apprentice | 22.8% ±0.4 | 21.2% ±1.4 | +41.3 | 3.5% | 33.0% |
| Master | 13.7% ±1.3 | 28.3% ±1.3 | +31.4 | 4.8% | 21.3% |
| Legend | 5.2% ±0.5 | 35.5% ±1.0 | +19.0 | 6.5% | 10.3% |

The spread survived the new curve and **Legend's floor rose**: on the old curve the hard tier ran a
negative net and won 0.2% of nights, which was the season's second half removed; it is now a road
that can be walked to the end. Nothing was retuned — **0.93/1.07/0.42, 1/1/0.5, 1.07/0.93/0.58** stay
— and the "partly void" note is discharged in both places it appeared (S5's binding-pool paragraph
and S10's tier table).

Changed: `Omamori.All` (five prices), the catalogue's remarks,
`OmamoriTests.EachCharmCarriesItsOwnMeasuredBlessing` (price pinned per charm), GDD S10 (the omamori
section, the tier table, Open Decision 20) and S5's tier note. No new sim knobs; the sweeps used
`--charm-price`, `--charm-fit`, `--difficulty` and `--seed`.

⚠️ **Still owed:** the yumi is still unwritten, so the kyūdō class carries a placeholder number.

690 tests green under Release (514 core + 123 presentation + 53 sim). `ThroughputTests` is a
wall-clock budget and **fails under a Debug build** (10.000 fights take ~15 s against a 10 s budget);
it is a Release-only check, which is what the line above has always meant.

---

## 2026-09-12 (tenth round) — The measuring bed is written down, and it stops being the worst policy

**The campaign bed now has a name and a command line.** Two rounds in a row a figure was compared
against a number measured on a bed nobody had written down, and reconstructing it cost more than the
sweep. The bed every campaign figure in this file since the seventh round was taken on:

```
dotnet run --project src/Domina.Sim -c Release -- --mode campaign --days 180 --campaigns 1600   --accept-ratio 2.0 --offers on --market on --school on --paths on --staff on --bounty on   --province on --smith-upgrades on --standing on
```

Without `--accept-ratio 2.0` the same systems close 84-92% of dojos: the accept rule, not the
season's length, is what the old "the bed is brutal" figures were measuring. The bed's charmless
reading, 1600 dojos, Master: **27.7% closed, 9.4% won the night, net 24.4**.

**`--charm-fit` now defaults to `irongate` instead of `weakest`.** The old default was the worst
policy measured, so every campaign figure carried a pessimistic charm floor for no stated reason. A
bed should stand for an informed player, and the player finds the defence charm in an afternoon. The
default is a bed, not a balance claim — when a round moves the ladder, the default moves with it.
`--charm-fit weakest` still reproduces the old reading.

**The per-charm sizes did not flatten the ladder** — that is the round's real finding. On the bed
above (1600 dojos, against charms off at 9.4% of nights and net 24.4):

| Policy | Closed | Won the night | Net |
|---|---|---|---|
| charms off | 27.7% | 9.4% | 24.4 |
| **iron gate** (+4 defence) | 25.2% | **14.7%** | 22.9 |
| **swift foot** (+8 evasion) | **23.8%** | 13.0% | 23.0 |
| steady hand / `first` (+12 accuracy) | 25.4% | 8.8% | 21.4 |
| quiet mind (+6 will) | 28.7% | 5.9% | 18.4 |
| long breath (+15 stamina) | 29.1% | 5.6% | 18.2 |
| `weakest` | 27.9% | 6.1% | 18.4 |

At 1600 dojos a 10% share carries about ±0.75 points of standard error, so iron gate's +5.3 and
swift foot's +3.6 are real, steady hand's -0.6 is noise on a 120-gold bill, and the will and stamina
charms are **worse than wearing nothing** by 3.5 and 3.8 points. Sizing them up was the wrong lever
(the ninth round measured that and reverted it); sizing them honestly leaves them **net harmful**,
because the gold is spent and nothing comes back. Two of the five charms are a trap the stall still
sells at the same price as the two that work.

The fix is not a number, and the ninth round already said so: **willpower and stamina need a job that
is not a stat point** — the drill's mastery rate, the panic check inside the dojo, the recovery
between fights. Until that job exists, `quietmind` and `longbreath` are measuring instruments, not
buys, and GDD S10's equal-price rule stays open.

**`--accept-charms` stays `off`.** The blind accept rule is the seventh round's measured fix, not an
oversight; it is the comparison against pre-seventh-round numbers that is void, which the bed block
above now makes checkable instead of implicit.

**A stale claim was corrected**, not discovered: the ninth round wrote that CI does not build the
Godot layer and a step was owed. It does — `.github/workflows/ci.yml` has had a separate `game` job
since `27257f7` (2026-08-06). The `DayScreen.cs` break survived locally, not past CI.

**Then the two open design items were closed** — the user picked the jobs, the measurement picked the
numbers.

**Willpower buys the rate of every other drill** (`TrainingTuning.WillFocus`, locked **0.5**). The
factor is `1 + 0.5 × (will − 50) / 100`: a recruit's 35 learns at 0.93, a drilled 90 at 1.20, and the
weapon master's rate is scaled by the same number — one day, one rate. The will read is the
**effective** one, so the charm he wears and the spirits he is in both reach the training ground.
Swept 0 / 0.25 / 0.5 / 1.0 (800 dojos × 180 days): the best man ends at 298 / 300 / 307 / 338 and the
night is won 9.2 / 10.8 / 10.1 / 13.8%. 0.5 rather than 1.0 on purpose — at 1.0 meditation becomes the
answer to every question and Will is the stat every dojo trains first.

**The stamina pool was made to bind** (`AttackStaminaCost` 6 → **14**, `StaminaRegenPerSecond` 4 →
**1**). At the old numbers a 17-second fight drained about a point a second out of a pool of 100-180,
so `MaxStamina` changed nothing: on the 3v3 bed, cutting regeneration from 4 to 0.5 moved victory by
0.5 points. Swept at a regeneration of 1, the fight runs 16.9 / 17.8 / 20.1 s at an attack cost of
6 / 10 / 14, and only at 14 does the deeper pool decide fights. An earlier round's "stamina
regeneration measures zero" is void with it — the knob measured zero **because** the pool never bound.

**And that moved the season, so two difficulty numbers were re-locked in the same round.** The dojo
trains stamina to 180 while an adversary carries a flat 100, so a binding pool hands the trained roster
the long fight: the bed went from 27.7% closed / 9.4% of nights to **13.2% / 43.7%**. Each axis has its
own answer:

| | Was | Now | Measured against |
|---|---|---|---|
| `PowerPerDay` | 0.0072 | **0.011** | Deaths per warrior-fight back on 4.7% — swept 0.0072-0.016, deaths 2.6 / 4.2 / **4.9** / 5.3 / 6.0 / 7.0% and closures 13.2 / 26.2 / **30.7** / 33.5 / 34.7 / 39.7% |
| `FinalRoundPowers` | 1.8-2.8 | **2.34-3.64** (×1.30) | The night is five bouts with nothing healing between them, which is where a binding pool pays most — swept ×1.15 / ×1.30 / ×1.45, nights won 14.2 / **9.7** / 6.0%, closures and deaths unmoved |

Master's re-locked profile: **30.4% closed, 10.1% of nights won, net 30.1** (against 27.7 / 9.4 / 24.4).
Harsher on the dojos that fail, richer for the ones that do not — which is what a binding pool is.
**Apprentice and Legend were not re-swept**: their multipliers are unchanged and still sit over this
curve, but the spread is owed a measurement, and GDD S10's tier table says so now.

**The two dead charms are no longer worse than nothing** — and still do not clear their price. On the
re-locked bed (800 dojos, charmless 10.1% / 30.4% / 30.1): quiet mind **9.8% / 29.0% / 23.0**, long
breath **9.0% / 27.3% / 23.1**. They buy survival rather than nights. The remaining gap is the stall
and not the stat: a charm-buying run also trains less, because the gold spent at the temple is gold the
school does not get. That is GDD Open Decision **20**, deferred by the user on purpose.

New sim knobs: `--will-focus`, `--attack-stamina`, `--dodge-stamina`, `--block-stamina`,
`--stamina-regen`. New tests: `StaminaTests` 2 (the default numbers drain a pool; the deeper pool wins
the long fight — 220 health a side, because a pool binds by outlasting and a short fight says nothing),
`TrainingTests` +2 (will buys the rate; the charm he wears counts toward the day).

690 tests green under Release (514 core + 123 presentation + 53 sim); `Domina.slnx` and
`src/Game/Domina.Game.csproj` both build.

---

## 2026-09-12 (ninth round) — The blessing is per charm, and two of the five are the whole branch

The eighth round found the five charms were a ladder rather than five answers. `OmamoriCharm` now
carries **its own blessing size** (`Bonus`), the prices stay equal at 120, and the sizes were swept
one charm at a time on the standing bed (4000 dojos x 180 days, every man wearing the same charm,
full price). Charmless control: **28.0% closed, 9.8% night, 25.1 net**.

| Charm | Size | Closed | Won the night | Net |
|---|---|---|---|---|
| Iron gate (Defence) | +2 / +3 / **+4** / +6 | 26.9 / 26.2 / **24.1** / 22.3% | 10.3 / 12.7 / **15.5** / 21.7% | 21.1 / 22.4 / **23.5** / 25.9 |
| Swift foot (Evasion) | +6 / **+8** | 24.0 / **23.4%** | 13.1 / **15.2%** | 23.0 / **23.8** |
| Steady hand (Accuracy) | +6 / +12 / +18 / +30 | 27.5 / 27.0 / 25.4 / 25.1% | 8.8 / **10.2** / 9.9 / 9.8% | 20.8 / 22.0 / 22.4 / 22.7 |
| Quiet mind (Will) | +6 / +20 / +40 | 29.3 / 29.3 / 29.5% | 7.4 / 7.4 / 7.5% | 19.4 / 19.4 / 19.5 |
| Long breath (Stamina) | +15 / +40 | 29.8 / 29.8% | 6.7 / 6.7% | 18.7 / 18.7 |

**Locked: iron gate 4, swift foot 8, steady hand 12, quiet mind 6, long breath 15.** The first two
land on the same rung as each other and that rung is the one a 120-gold charm should sit on — bought,
the dojo closes 24.1% / 23.4% against 28.0% and wins the night 15.5% / 15.2% against 9.8%, for 1.3-1.6
gold a fight. **The price needed no change at all**; the eighth round's "net harm" was the blunt
policy buying the wrong charm.

⚠️ **The other three cannot be brought to that rung with points.** Accuracy saturates — +12, +18 and
+30 all sit at ~10% of nights won, because the hit chance runs out of room — and will and stamina are
**flat lines on or below the charmless dojo's own figures at any size**. The cause is that the points
stop converting: Will is **clamped to 0-100** wherever it is read (panic, morale, the tribunal) and
training already carries a man to 90, so most of a large blessing is thrown away, and a fight ends
long before a larger stamina pool binds. So those two keep their **small** sizes (+6, +15) instead of
being inflated along a flat curve — a "+40 stamina" on the temple's stall would read as the best buy
on the shelf while doing nothing. The honest reading, now in `Omamori`'s own remarks
and in GDD S10: **a stat point is the wrong currency for these three**; what they want is a job that
is not a stat — the drill's mastery rate, the panic check inside the dojo, the recovery between
fights. That is a design decision and it is left **open**.

The measuring policies were re-read on the new sizes: `weakest` 7.3% of nights (it spreads the gold
onto the two dead axes), `first` 10.2%, iron gate 15.5%, swift foot 15.2%. So the smart policy is
still the wrong instrument while three charms are inert — which is the same finding from the other
side.

**The temple's stall now prints the blessing** (`Iron gate · +4 defence · in store 2`): five
identical price tags over blessings that run from 4 to 40 points would hide the whole decision.

**And a build that had been broken on `main` since `afd0837` was fixed.** `DayScreen.cs` had a raw
newline inside a character literal (`string.Join('⏎', rows)`), so `src/Game` did not compile at all.
The root `dotnet build` never touches it — the Godot project is isolated by its own
`Directory.Build.props` — so nothing local caught it. **CI was not the gap**: `.github/workflows/ci.yml`
has had a separate `game` job building `src/Game/Domina.Game.csproj` since `27257f7` (2026-08-06),
and it would have failed on the first push. The gap is local: the break survived several commits
because nothing on this machine builds the Godot layer before a commit.

686 tests green under Release (510 core + 123 presentation + 53 sim), and
`dotnet build src/Game/Domina.Game.csproj` succeeds again.

## 2026-09-12 (eighth round) — The tiers are locked, and the five charms turn out to be a ladder

**The difficulty tiers are re-locked** on the numbers the seventh round proposed, confirmed on the
blind accept rule (4000 dojos x 180 days, every system on):

| | Apprentice **0.93 / 1.07 / 0.42** | Master 1 / 1 / 0.5 | Legend **1.07 / 0.93 / 0.58** |
|---|---|---|---|
| Dojos closed | 19.9% | 27.5% | 35.2% |
| Won the last night | 16.4% | 8.8% | 3.0% |
| Net per fight | +27.4 | +20.8 | +11.4 |
| Deaths / warrior-fight | 3.5% | 4.7% | 6.2% |
| Mastery, best man | 25.3% | 15.1% | 6.9% |

The old ±0.15 rungs are gone: they were read off the 60-day curve and on the real one Legend won the
night 0.2% of the time on a negative net. GDD S10's table carries the new numbers.

**The charm policy was written the way the design describes it** — `--charm-fit weakest` hangs the
charm that answers the man's **weakest stat**, comparing each stat against a fresh recruit's rather
than against the others (stamina is a pool on its own scale and would otherwise be the answer every
time). `--charm-fit first` keeps the blunt policy the branch was first measured with, and the five
charm names are accepted as well, which is what found the real problem.

**It measured worse than the blunt policy**, and at zero price — where the two policies spend the
same nothing and only the placement differs — it was still worse: night 11.7% against 14.1%. So the
loss is not the extra gold the smart policy spends; it is the placement itself. The five-charm rank,
every man wearing the same charm, **at zero price** (charms off: 28.0% closed, 9.8% night, 25.1 net):

| Charm | Closed | Won the night | Net | Deaths / warrior-fight |
|---|---|---|---|---|
| **Iron gate** (Defence) | **18.2%** | **32.8%** | 35.1 | 3.1% |
| Swift foot (Evasion) | 21.3% | 19.3% | 30.3 | 3.9% |
| Steady hand (Accuracy) | 25.2% | 14.1% | 28.5 | 4.1% |
| Quiet mind (Will) | 26.8% | 9.9% | 25.4 | 4.6% |
| Long breath (Stamina) | 27.0% | 9.6% | 25.1 | 4.6% |

**The charms are not five answers to five weaknesses, they are a ladder.** +6 defence is worth 3.3x
+6 accuracy, and the will and stamina charms are worth **nothing at all** — both sit on the charmless
dojo's own figures at any price. That is also why filling a man's weakest stat loses: it spreads the
gold onto the two dead axes.

And the branch is not underpriced either. A dojo that buys **only the iron gate at the full 120
gold** closes 22.3% against the charmless 28.0%, wins the night **21.7%** against 9.8% and still
nets 25.9 a fight. The seventh round's "+6 at 120 is net harm" holds only for a policy that buys the
wrong charm; the player will find the right one in an afternoon.

⏳ **Left open, and it is a GDD question rather than a number**: S10 sets one blessing size for all
five charms and equal prices, on the ground that the charms are not a power ladder. The measurement
says they are. The fix is a **per-charm blessing size** (equal prices kept, the design's own reason
intact), which is a core change to `Omamori` — not made blind in this round. Until it is made, the
charm numbers stay unlocked.

681 tests green under Release (505 core + 123 presentation + 53 sim). New sim knobs: `--charm-fit`,
`--accept-charms`; the report's school line now prints the charm gold beside the school gold.

## 2026-09-12 (seventh round) — The tier table re-measured, and a charm that was buying greed

Both halves of this round run on the same bed, written down here because the earlier rounds each
named a different one: **4000 dojos x 180 days**, the new curve, `--accept-ratio 2.0`, every system
on (`--offers --market --bounty --school --paths --staff --charms --province --smith-upgrades
--train-classes`). 4000 dojos puts the standard error on a last-night figure at ~0.5 points; the
400-dojo bed the earlier charm numbers were read on carried ~1.8, which is most of what they said.

**The difficulty tiers, re-derived on the new curve** (the fourth round invalidated the old table):

| | Apprentice | Master | Legend |
|---|---|---|---|
| Dojos closed | 12.5% | 27.5% | **40.5%** |
| Played the night | 85.8% | 69.8% | 53.7% |
| Won all five | 22.6% | 8.8% | **0.2%** |
| Net per fight | +31.7 | +20.8 | **-2.9** |
| Fights per dojo | 125.1 | 79.5 | 35.6 |
| Hungry days | 8.9% | 25.3% | 54.4% |
| Deaths / warrior-fight | 2.5% | 4.7% | 7.9% |
| Mastery, best man | 34.3% | 15.1% | 1.6% |

**The +-15% multipliers are not symmetric in what they produce.** On Legend the net per fight goes
negative, mastery falls to nothing and the night is won 0.2% of the time — 44x less often than at
Master. That is not the harder road GDD S10 asks for ("a tier changes how often the player is in
trouble, not what trouble means"); it is a different game with the season's whole second half
removed. The sweep of the rungs (same bed):

| Legend | Closed | Won the night | Net | Mastery |
|---|---|---|---|---|
| 1.15 / 0.85 / 0.67 (locked) | 40.5% | 0.2% | -2.9 | 1.6% |
| 1.10 / 0.90 / 0.60 | 38.2% | 1.4% | +6.5 | 4.3% |
| **1.07 / 0.93 / 0.58** | 35.4% | **2.8%** | +11.2 | 6.5% |
| 1.05 / 0.95 / 0.55 | 33.4% | 3.5% | +14.4 | 8.4% |

| Apprentice | Closed | Won the night | Net |
|---|---|---|---|
| 0.85 / 1.15 / 0.33 (locked) | 12.5% | 22.6% | +31.7 |
| **0.93 / 1.07 / 0.42** | 20.8% | 14.9% | +26.9 |
| 0.90 / 1.10 / 0.40 | 18.6% | 17.8% | +28.9 |

Proposed, **not locked in this round**: Legend 1.07 / 0.93 / 0.58 and Apprentice 0.93 / 1.07 / 0.42,
which puts the night's spread at 1.9x / 2.8÷ around Master instead of 2.6x / 44÷ and keeps all three
tiers on a positive net.

**The charm was net harm — and the measuring policy was half the reason.** On the school-first bed
the omamori cost the season 2.0 points of last-night wins and 4.7 gold a fight. The first sweep said
the blessing was not too small: raising it from +6 to +30 made every figure **worse** (night 7.8 ->
4.9%). The cause was the accept rule, not the charm: `Declines()` scored the party on
`EffectiveStats`, which the charms are inside, so a stronger blessing made the policy decline fewer
offers (46.3% -> 43.7%), fight more (79.5 -> 83.3 a dojo), lose more men (10.44 -> 11.35) and train
less (94 -> 62 days). **The charm was buying greed rather than safety.**

`Warrior.UnblessedStats` was added for it — the same stats with the temple's charms left out — and
the campaign policy now judges an offer with it (`--accept-charms on` restores the old reading; the
fight itself has always read, and still reads, the blessed stats). A charm is bought, moved between
men and sold back, so it has no business in the judgement of what the roster can take.

With the blind rule the blessing sweep turns the right way up and saturates, and the price is what
is left over (bonus / price, against **charms off: 28.0% closed, 9.8% night, 25.1 net**):

| | Closed | Won the night | Net |
|---|---|---|---|
| +6 at 120 (locked) | 27.5% | 8.8% | 20.8 |
| +12 at 120 | 27.0% | 10.2% | 22.0 |
| +20 at 120 | 26.0% | 9.9% | 22.5 |
| +30 at 120 | 25.1% | 9.8% | 22.7 |
| +6 at 60 | 27.2% | 9.7% | 23.2 |
| +6 at 30 | 25.4% | 11.8% | 25.4 |
| **+12 at 60** | **25.0%** | **11.8%** | **24.5** |
| +6 free | 25.2% | 14.1% | 28.5 |

So the blessing is worth roughly **30-60 gold at +6**, and at 120 gold no size of blessing reaches
parity on the night. The combination that pays for itself without being free money is **+12 at 60
gold**: it beats the charmless dojo on closure (25.0% against 28.0%) and on the night (11.8% against
9.8%) at the same net. Proposed, **not locked** — it is one bed and one policy, and the charm's
other half (a player who fits the charm to the man's weakest stat instead of buying the first in the
catalogue) has still never been measured.

680 tests green under Release (505 core + 123 presentation + 52 sim; +1 for the unblessed view).

## 2026-09-12 (sixth round) — The list of things that were never written

Eight items that had been carried as "open" for several rounds, in order.

**Morale reached the screens.** The roster prints the dojo's spirits beside its beds and each man's
beside his honour — as a **word** with the number after it, because a bare number next to eight real
stats invites the player to train a condition. The feast is the roster's lever, so it sits with the
summary and says which of the two refusals is in the way; the market sells the sake, since the day's
bill never buys it.

**Retirement (`Roster.Retire`, `DojoState.Appoint`).** Twelve victories or a lost limb, and a warrior
becomes a **master of the house**: no food, no wage, no field, and a post at GDD §10's own tier table.
The post now remembers **who** stands in it. ⚠️ Measured, and it is never the profitable move —
retiring everyone eligible takes closure from 25.0% to 36.5% and the night from 11.2% to 0.0%, and
even retiring only the maimed costs. Raising what a master is worth does not touch it (1.15 / 1.4 /
1.8 → 28.7 / 28.0 / 28.7%): the economy is roster-bound, a body beats a multiplier. It stays the
honourable exit it was written as, with the two levers that would change it named.

**The equipment branch got its two upper tiers** — plate works (450, the ō-yoroi gate: full plate needs
the building **and** the smith) and sword forge (700, a weapon reforged rather than replaced). The
reforged blade carries a **new name**, so the mastery built on the old one stays with the old one.
Measured: an equipment-only dojo that uses them closes **29.5%** against 61.8% and reaches the night
69.2% against 37.0% — the branch that "paid for nothing" at step 5 is now a commitment worth making,
and still a real cost against a school-first dojo (25.8 → 27.0% closed) because heavy is heavy.

**The three parties (`Dojo/Standing.cs`).** Clerk's office, guild, temple; five tiers; each tier on its
own axis (work pays ×0.06 a tier, prices ×0.05, charms ×0.10). A contract finished raises, a promise
broken eats from two places at once, a quiet week costs, a gift is worth less every time, and the monk
keeps the temple's regard up with a wage rather than with work. The whole system is worth **~4% of
income and no closure** — the size it was written for.

**#15 closed:** the backup is rolled on every write and read only when the current save cannot be read
at all. Never offered, never a button, says so when it fires, and deleted with the save.

**#8 measured and locked** — and a **bug** came out of it: `BattleAftermath` built its own honour
engine on the defaults, so a dojo's honour numbers reached the tribunal and never the fight. Every
earlier sweep of the retreat penalty had therefore measured nothing. One engine now, and with it:
seppuku **30** (20 / 30 / 40 → 22.0 / 26.2 / 36.5% closed), decay **0.5** (0 / 0.5 / 1.5 → 36.5 / 26.2
/ 19.5%), retreat penalty **8**, which only shows against a policy that actually flees (0 / 8 / 20 →
43.2 / 55.2 / 67.5% closed, first seppuku on day 34 / 9 / 5).

**`ThroughputTests` passes in Debug again — and it was a real cost, not a budget.** The fight read
`Warrior.EffectiveStats` several times per warrior per tick, and every read rebuilt the struct through
the path, morale, mastery, the charms and every disability. Nothing that feeds it can move while a
fight is running — the path is chosen in the dojo, morale settles at the day's close, mastery is
credited by the aftermath, and a limb comes off in the report rather than on the field — so the
combatant now reads it **once**. 10.000 fights in Debug: **10.66 s → ~8 s** under the 10 s budget, the
victory rates identical to the digit (3v3 60.63%), and Release runs at 7.344 fights a second.

**The classed candidate, measured against a dojo that trains its own** (step 5's open question). With
the halls in use — 3.3 men classed a season — the stall's shortcut still pays: chance 0 / 0.05 / 0.25
gives last nights won 9.8 / 11.8 / 12.2% and dojos closed 29.2 / 26.5 / 25.5%. It saturates fast, so
**0.05 stands**: worth taking when it appears, never worth waiting for.

## 2026-09-12 (fifth round) — One point of damage closes #19

The catching implements were priced wrong in a way the class layer made obvious: a torite holding a
**katana** beat the same torite holding a jitte everywhere, because a catch pays **per event** and the
0.10 unskilled-implement floor collected most of that value, while the jitte paid for its catch
frequency with every strike it landed.

The fix is one point of damage — **jitte and sai 14 → 15** — and the curve turned out to be steep
enough that it had to be found rather than reasoned (40.000 fights a row):

| Damage | Duel | Against a two-handed enemy | Against ō-yoroi |
|---|---|---|---|
| 14 (locked) | 71.42% | 29.19% | 26.92% |
| **15** | **77.07%** | **33.39%** | 38.31% |
| 16 | 81.83% | 40.59% | 50.01% |
| 18 | 87.01% | 47.88% | 69.53% |

The controls: a torite with a katana wins the duel **76.13%**, the two-handed fight **32.46%** and the
armoured fight **56.24%**. So at 15 the implement is level in a duel, **ahead where it is supposed to
be** — the heavy-weapon brake #19 reported as gone is back, 33.39% against 32.46% — and behind in
front of plate, which is where §7 already put the katana. The hard zero is untouched: a classless
jitte still wins 42.20% against a classed 77.07%.

Both halves of Open Decision #19 are closed by the same number, and the margin is defensible on this
bed: ±0.21 points of standard error at 40.000 fights against a gap of 0.9.

## 2026-09-12 (fourth round) — The curve was calibrated for a season a third as long

The "campaign bed is brutal" figure — 90%+ of dojos closing, which had been shrugged at for two
rounds as "the bed has always been pessimistic" — turned out to be one number written for a
**60-day** campaign and never revisited when the season became 180 days.

`EncounterTuning.PowerPerDay` was **0.02**. The curve starts at 0.9 and is capped at 2.2, so at that
slope it reaches its ceiling on about **day 65** and the remaining 115 days are fought at the top of
it. Re-derived as `(2.2 − 0.9) ÷ 180` = **0.0072**, the ceiling now arrives at the end of the season.
Same bed, same policy (400 dojos × 180 days, `--accept-ratio 2.0`), nothing else touched:

| | 0.02 (locked) | **0.0072** |
|---|---|---|
| Hungry days | 65.0% | **23.7%** |
| Net per fight | −12.1 gold | **+42.7** |
| Fights per dojo | 24.7 | **85.2** |
| Deaths per warrior-fight | 10.4% | **4.1%** |
| Dojos closed | 52.5% | **25.0%** |
| Won the last night | **0.0%** | **11.2%** |

The other half of the same finding: **the blunt policy was never the right instrument**. A dojo that
accepts every offer still closes 88% of the time on the new curve, while the documented policy — the
one that declines a fight its party is too weak for — closes 25%. Every closure figure in this file
should be read with the policy beside it.

**The last night was re-derived with it (powers 1.8 / 2.0 / 2.2 / 2.4 / 2.8).** Two rules had landed
since its calibration, both hitting it hardest: the adversaries stopped breaking and running, and a
match's panic became a **yield on both sides** — because with his men standing and the dojo's running,
the asymmetry fell on the player exactly where he is outnumbered, which is every bout after the
first. The outnumbered trigger was then dropped from matches entirely: the numbers of a bout are
agreed in front of witnesses, so walking into three men is not a line breaking. Even so the old
powers left the night won by 3.8%. Swept ×1.0 / 0.9 / 0.8 / 0.7 → 3.8 / 7.2 / 13.2 / 18.0%, and the
ladder settled on the ×0.8 rung rounded clean. **11.2%** now, which is where the quarters branch left
it (10-11%).

**And the weapon master's hall was repriced, which finally made mastery real.** It had been the
training branch's third tier (700 gold, 14 days) and measured as nothing there — a dojo that lives to
buy it has a month left to use it. It is now the **armoury**: a support building, 200 gold, 6 days.
On the new curve the best man ends the season at **24.8% mastery** instead of 0.0%, and switching
mastery off costs the run 2.7 points of last-night wins (11.2% → 8.5%) and 2.7 gold a fight.

⚠️ Invalidated by this round: every closure, net-per-fight and hungry-day figure measured before it,
and the difficulty-tier table. They were all read off the old slope.

## 2026-09-12 (third round) — The enemy stops running, and learns to yield

Two rules in one sitting, both the design's call.

**An adversary never leaves the field.** The rival's men are paid, pressed and watched by the school
that sent them, so the panic check no longer ends in flight for them — outside a match it is not even
rolled, because a die with no outcome still costs every fight the time to roll it. This closes the
**reward leak** step 6 found: a fled enemy used to pay exactly what a fallen one did, since the reward
comes from the encounter's enemy health.

**In a match he yields.** `BattleSetup.Match` marks a bout fought before witnesses under an agreed
form — the last night's five, in front of the lord — and there a broken adversary goes to
`CombatState.Yielded`: out of the bout, alive, not struck again. The player's side is untouched: his
key is still a team order, and a warrior of his who breaks runs rather than kneels.

**What it costs.** Panic used to fire on both sides, which is why it *raised* the player's victory
rate. Firing on his side alone is worse than switching it off:

| `3v3`, 20.000 fights | Both sides panic | Player side only |
|---|---|---|
| Victory | 69.64% | **60.63%** |
| Warrior deaths | 37.64% | **48.58%** |

Over a season (400 dojos × 180 days, map on): deaths per warrior-fight 6.3% → 10.2%, fights per dojo
35.1 → 26.6, dojos closed 90.2% → 92.8%. The other beds moved the same way — `duel` 60.59% → 58.15%,
`patrol` 96.96% → 95.64%.

Written down, **not patched**: the rule is what the design asked for and the numbers around it belong
to Phase 9. What this round does owe is a note that the campaign bed has been pessimistic all along —
a 90% closure rate was true before this rule and is not its doing.

The HUD names the new state, and a man who yielded is drawn where he stopped rather than removed from
the field: he is beaten, not gone.

## 2026-09-12 (second round) — The province, and the exploit it closes

`Dojo/Province.cs`: twelve settlements, a three-state allegiance, a 0-2 warning ladder, the rival's
weekly move on the compulsory fight's own clock, the contracts that win a village, the one number he
stores (deniability) and the raid it opens. It advances inside `AdvanceDay()`, it goes into the save,
and the move itself rolls no die — his target is the player's settlements first and the most pressed
among them — so a season replays identically.

Wiring: a won fight answers him (the pressed village eases, his next move is two days late, his bound
is spent a point) and it counts **once per move cycle**, or a dojo that fought three times in a week
would push his clock out of the season. A claimed bounty files a contract **for a settlement** — the
contract carries the village its road runs through — and two of them win a free village, three one of
his after his grip is broken. A village that comes over gives **one thing, once**: rice, medicine,
sake, or the name of his next target, good for one turn.

**The finding the round exists for.** GDD §10 asked whether the map adds any pressure on top of the
missed-week honour penalty. Against the hiding dojo — the endless-training exploit written as a
policy — 400 dojos × 180 days:

| | Map off | Map on |
|---|---|---|
| Dojos closed | **7.5%** | **72.8%** |
| Days survived | 176 (median 180) | 159 (median 165) |
| Training days | 48.3 | 24.0 |
| Raids / sacks per dojo | 0.00 | 4.44 / 4.43 |

Against an ordinary fighting policy the same map costs **one point** (89.0% → 90.2%). It presses the
player who ignores it and barely touches the one who answers.

**A rule the measurement changed.** The ladder first had a quiet step — he holds the whole province,
there is nothing left to press. That step made the map ignorable by exactly the player it was written
for: a dojo that never fights never kills his men, never spends his bound, and so was never raided.
Now, with nothing left to press, **the next thing to take is the school**. That single change is the
difference between 7.5% and 72.8%.

Swept: the warning ladder (1 → 97.2% closed, 2 → 91.5%, 3 → 89.2% — a cliff between 1 and 2, and 2 is
the first value past it), deniability (4 / 8 / 12 / 20 → 1.07 / 0.30 / 0.26 / 0.26 raids; above 8 the
bound stops binding), and the held-settlement reward share (0 / 0.03 / 0.06 / 0.10 → net −3.6 / −3.6 /
−3.1 / −2.6, linear). ⚠️ The **return** side of the map is effectively unmeasured: the measuring
policy holds 0.56 settlements on average because it files so few contracts, so the share has never
been read at a real holding, and the 2/3-contract thresholds and the 2-day delay could not move
anything either. That is the next policy to write, not the next number to guess.

A raid is the day's offer and cannot be declined like one: a day that closes with him still in the
yard is a **sack** — a third of the treasury, a third of the store, and 10 honour off every man,
against the missed week's 5.

New sim knobs: `--province on|off`, `--province-warning`, `--province-pushback`, `--deniability`,
`--settlement-reward`; the report grew a province line (settlements held, raids, sacks). The day
screen's season banner now prints the map and his next move, and never his bound — a visible bar
would turn the season's one hidden pressure into arithmetic.

637 tests green under Release (475 core + 114 presentation + 52 sim).

## 2026-09-12 — The last three posts: mastery, the omamori, the reading

Three posts had been drawing a wage against nothing since step 5. All three are written now, and
`Facilities.IsInert` lists nobody.

**The weapon master.** `Model/WeaponMastery.cs` — mastery is stored per **weapon name** on the
warrior, grown as a share of the gap to full (0.03 a drill day, 0.06 a fight), and read by the fight
as a multiplier on Accuracy alone. It is gated outright by the inner dojo rather than stepped: no
hall, no mastery; an empty hall teaches at half. Meditation buys none of it — the drill that does not
touch a sword must not teach the sword by the back door — and a lost arm takes the mastery of a
two-handed weapon with it, because what he cannot use he does not know.

The band was swept on `3v3`, 20.000 fights a point: victory 69.64% at mastery 0, **72.22%** at 1, and
the size of the bonus was linear (0.05 → 70.95, 0.10 → 72.22, 0.20 → 74.73, 0.30 → 77.39). Fourth
sweep in a row with **no knee**, so 0.10 is a budget. Across beds it lands where accuracy is scarcest
— `jitte-armored` +3.20 points, `duel` +2.50, `patrol` +0.56 — which is the lean the design wanted
from a bonus meant to carry a heavy weapon.

⚠️ **What the season said: the hall arrives too late to matter.** 400 dojos × 180 days with the
school and the payroll running, mastery on against mastery switched off entirely: **93.2% of dojos
closed either way**, identical deaths, identical best warrior. The inner dojo is the training
branch's third tier at 700 gold, and a dojo that lives to buy it has about a month left to use it.
The same family as the forge's finding — written down, **not repriced blind**.

**The monk.** `Model/Omamori.cs` — five charms at 120 gold, **+6 points on one stat** (+15 stamina),
added as points rather than as a share because morale and mastery are already multipliers and a third
would compound past measuring. Transferable, sellable back at half, and capped by **whole** slots: no
shrine, no charms; an empty shrine one slot, the monk the second (half a slot is nothing). A released
man leaves them in the store; a dead man's come home only if the field was won, the same rule his kit
follows. Measured on the support branch: deaths per dojo **9.26 → 6.84**, dojos closed
**89.8% → 86.8%**, days survived 71 → 75.

The **funeral rite** (half a comrade's death taken off the survivors) measured as **nothing**:
0 / 0.25 / 0.5 / 0.75 / 1 closed 89.8 / 89.8 / 89.8 / 89.2 / 89.5% of dojos. A quiet day already
pulls morale back toward the middle, so the relief lands on a hole that was closing by itself. Kept
at 0.5 as the post's colour, with the sweep on the record.

**The diviner.** `Campaign/Divination.cs` — the post sells information and never lies. An empty hut
names each enemy and his weapon; a diviner in it prints the stat block as well. `OfferModel.ReadOffer`
is the one place the screen may see the enemy roster, and the day screen **hides the panel entirely**
without a hut, so a dojo that has not paid is not shown what it is withholding.

Also decided, no code needed: **a feast never touches honour** (GDD §3). Morale is the counter inside
the dojo and honour the one outside it; sake drunk in your own yard is seen by nobody, and letting it
buy honour would give the feast two payoffs for one price.

New sim knobs: `--mastery`, `--mastery-accuracy`, `--mastery-rate`, `--mastery-fight`,
`--funeral-relief`, `--charms on|off` (the campaign policy buys charms and fits them, and the gold is
reported apart from the school's).

625 tests green (463 core + 112 presentation + 52 sim; 20 new) under Release. `ThroughputTests` still
misses its 10 s budget in **Debug** — it did before this round too (11.58 s on this machine against
12.82 s now); in Release the same test runs in ~2 s.

## 2026-09-10 — The enemy became human (Open Decisions #16 and #17)

The story pass (`STORY.md`) settled what the season is about, and it closed two decisions that had
been waiting on it.

**#16 — the enemy is human, and there are no monsters.** Every fight in a season now belongs to one
rival school's protection racket and to the same 180-day clock; a creature encounter belongs to
neither and would have been filler. The change was almost free, because an enemy is only a stat
block:

- `Campaign/Bestiary.cs` → `Campaign/Adversaries.cs`, `YokaiKind` → `EnemyKind`, `Bestiary` →
  `Adversaries`
- kappa → **Collector**, kitsune → **Cutthroat**, tengu → **Duelist**, oni → **Kabukimono**,
  jorōgumo → **Senior Student**
- **No number moved.** Stats, weapons, weights and `MinPower` are exactly what they were, so every
  measurement in this file taken before today still holds — the earlier entries name the old kinds
  and are left as they were written.
- Touched with it: `Bounty.cs`, `EncounterGenerator.cs`, `Battle.cs`, `BattleSetup.cs`,
  `BattleAftermath.cs`, `DemoRoster.cs`, `Scenario.cs`, `EncounterTests.cs`,
  `DojoAftermathTests.cs`, `project.godot`. A leftover Turkish comment line in the old bestiary went
  with it.
- 500 tests green (349 core + 99 presentation + 52 sim). `Domina.Chat.Tests` contains no tests —
  pre-existing, unrelated.

**#17 — the rival school and the settlements.** The full decision is in GDD §10. The shape: 12
settlements, a three-state allegiance and a 0-2 warning level; the rival moves every 7 days and that
counter **replaces** the old 7-day filing penalty; a settlement never pays on a schedule (one guaranteed
item on the day it comes over, nothing after, and a wider offer queue as the real return); the rival stores
one number only (deniability) and his strength is derived from what he holds.

Rejected in the same sitting: passive income from settlements, per-settlement traits, doctrines for
the rival, and a rescue loan against the death spiral. Run variety comes from a randomised starting
state only — there is no meta-progression.

**Nothing here is measured yet.** The numbers (12 settlements, 2/3-contract thresholds, the 2-day
delay) are proposals, and the claim that the move counter closes the
endless-training exploit is exactly that — a claim. Sim work before any of it is locked.

## 2026-09-10 — The roster ceiling: quarters, and what depth is actually worth

The night's measurement said depth wins it and the story says the dojo holds six. The two were only in
conflict while "six" was read as a life sentence: docs/STORY.md says the master left **3-5 men, never
above the dojo's starting roster ceiling of six** — a *starting* ceiling. Growing is the season's work,
so the ceiling became a thing you build.

**The quarters branch** (`SchoolBranch.Quarters`): barracks 300 gold / 8 days, long house 600 / 12,
each adding **2 beds** to the master's six. No post — a roof needs nobody to run it — and no
half-efficiency, because half a bed houses nobody. `DojoTuning.RosterCapacity` holds the base so the
sim can sweep the ceiling itself. Hiring is refused when the beds are full, at both doors
(`Quartermaster.Hire` and the market), and the dead and the released free their bed: the ceiling counts
the living.

**Then the measurement said something better than "beds win nights".** 300 dojos × 180 days, Master,
full school:

| | Won the night | Men sent across it | Hungry days |
|---|---|---|---|
| beds from day one: **8** | **29.7%** | 14.7 | 5.0% |
| beds from day one: **10** | **35.0%** | 15.6 | 2.8% |
| starts at six, buys the quarters (ends at 9 beds) | **10.7%** | 10.7 | 14.0% |
| quarters branch and nothing else (ends at 9.8 beds) | **0%** | 6.0 | 20.1% |

Two findings, and the second is the one worth keeping:

1. **Beds alone are not a strategy.** A dojo that builds nothing but quarters never gets past the
   fourth bout: it has men and no school to have made anything of them.
2. **A bed bought in the fifth month holds a raw recruit.** Instant construction barely moved the
   night (11.0% against 10.7%) and doubling build time only cost three points, so it is not the
   calendar — it is that the men who win the last night are the men who had a **season** to grow. Depth
   bought late converts gold into bodies, not into wins.

So the branch is priced as an early-game commitment and the decision it creates is a real one: beds
early, against school tiers early, out of the same purse. Buy it in month five and it is close to
wasted — which is the honest shape of this system rather than a number to be tuned away.

**Where that leaves the last night's calibration.** The figures locked earlier today (a "developed dojo
with eight men wins 38.5%") were measured when depth was free. With depth paid for, the same
policy wins **10-11%**, and a dojo that had its beds from the first day approaches **30%**. The sim's
night policy is still crude — strongest men standing, bout after bout, no fresh man held back for
Kurogane — so a player who invests early should land in that upper band. The bout numbers are left
where they are: the ending should be reachable by a season played well and not by a season played at
all.

⏳ **Still open:** the rival's settlement map. The weekly counter carries only the compulsory fight
until it exists.

445 core tests green (607 in total).

## 2026-09-10 — The gaps closed: the tribunal, the tiers, and a night measured against a real dojo

An audit of what step 7 had actually delivered turned up seven holes. All of them are closed here
except the rival's settlement map, which is a system of its own.

**1. Seppuku was wired to nothing.** `SeppukuArbiter` existed, was tested, and was called by no line
outside `Honor/`. So every rule that pushes honour down — a broken promise, a rout, the missed week
whose penalty had just been locked at 5 *because* "honour pushes toward the seppuku threshold" — was
arithmetic on a number nothing ever read. `Dojo/Tribunal.cs` closes it: a warrior below the threshold
is summoned at the close of the day, the crowd has a day to speak, and the next close answers him —
the sword, or a pardon that puts him back a little above the threshold with a fortnight's immunity.
One man stands at a time. The dead and the released come off the books before it sits.

The arbiter was **not** reused wholesale: it runs on a wall clock (a 60-second vote, fifteen minutes
of immunity), which is right for a live stream and meaningless to a calendar — a season played in an
afternoon would resolve every vote in the same minute. The clock and the queue are re-stated in days;
the parts that carry the *rule* — the threshold, the crowd's tally, the artificial crowd's decision,
the pardoned honour — are the arbiter's own types, unchanged. When chat arrives it votes into this
same summons rather than getting a second rulebook. The verdict's stream is derived from the seed and
the day, so a reload cannot reroll it, and the queue and the pardons go into the save; the voices
already cast do not.

**2. The banner promised a system that does not exist.** The day line said `Kurogane moves in 3 days`
while nothing in the code moves him. It now says `Week closes in 3 days`; the rival's half goes back
in with the settlement map.

**3. The starting roster is drawn, not fixed.** STORY.md gives the dead master 3-5 men and says no two
seasons open the same way; `NewGame` was handing out exactly four every time. The size is drawn before
the men are, so the same seed still opens the same dojo. Four — the measured `RosterTarget` — is the
middle of the range, so the economy that was measured is still the economy being played.

**4. Releasing a man was core-only.** The closing screen counted "the men who walked out free" and the
player had no way to free anybody. The roster screen now ends a term, with a confirmation, and refuses
a man in the infirmary — letting a wounded mouth out of the gate before a hungry day would buy the
upkeep rule off. Released men stay on the roster screen with their own badge, counted on neither side
of the ledger.

**5. The day's price was silent.** The missed week and the tribunal's verdict now print in the day's
report. A price the player is not told about is a price he cannot answer.

**6. A night in progress had no save test.** It has one: bout number, phase and carried wounds survive
the round trip, and the wound is priced the same on the other side of the file.

**7. Difficulty tiers did not exist.** `Campaign/Difficulty.cs`, exactly as GDD §10 fixes it — Master
**is** the measured game (both multipliers 1) and the other two are multipliers laid over it, never a
second table. Two axes, deliberately pulling together: enemy power ×0.85/×1.15 and what the work pays
×1.15/×0.85. Nothing touches death, dismemberment or the seppuku threshold — a tier changes how often
the player is in trouble, not what trouble means. The tier goes into the save (it is the player's
decision, like the seed); the multipliers stay in the code. Measured, 200 dojos × 180 days:

| Tier | Fights | Deaths / warrior-fight | Hungry days | Ending purse | Dojos closed | Won the night |
|---|---|---|---|---|---|---|
| Apprentice | 133 | 1.9% | 2.3% | 6377 | 1.0% | 85.5% |
| **Master** | 102 | 3.9% | 13.9% | 3231 | 8.5% | 51.5% |
| Legend | 61 | 6.4% | 35.8% | 524 | 17.0% | 14.0% |

**And the night itself was being measured against the wrong dojo.** Those tier runs were the first
time the last night had been played by a dojo that builds its school, fills its posts and chooses its
paths — and it won half of them at the numbers locked that morning, 78% with a deep roster. The
morning's calibration had been run with all of that switched off. So the bouts went back to the
design's own line and the calibration moved onto the crowd:

| The dojo that walks into the night (powers 2.2-3.2, crowd 2/3/3/4/1) | Won all five |
|---|---|
| school + staff + paths, **eight** men | **38.5%** |
| the same dojo, **six** men | **13.5%** |
| built none of it, six or eight men | **0%** |

> These rows were measured while **depth was free** — the roster ceiling did not exist yet. With the
> quarters branch paying for it, the same policy wins 10-11%; see the next entry.

The fourth bout — four seniors at once — is the gate (29% at six men, 51% at eight); one step further
(3/4/4/5) shuts it at 0.5%, a wall rather than an ending. Power is nearly inert up here: 2.2-3.2
against 2.6-3.6 moves the night three points, because an enemy's accuracy, defence and evasion are
capped at 95 and only health and strength keep scaling.

**8. The hungry days are an income problem, not a price problem.** The 17-49% figure flagged in the
last entry tracks one thing — how much work the dojo takes:

| Offer acceptance | Fights | Hungry days | Ending purse | Dojos closed |
|---|---|---|---|---|
| timid (1.0) | 63 | 41.5% | 132 | 10.0% |
| 1.6 | 88 | 22.8% | 1996 | 11.5% |
| 2.0 | 102 | 13.9% | 3231 | 8.5% |
| take everything (3.0) | 95 | 5.7% | 1487 | 3.0% |

No price was touched. A dojo starves by declining, not by shopping, and the greediest policy is also
the one least likely to close — idleness is the more dangerous of the two errors. What stays
structural is that **42-64% of days have no party to send**: the binding constraint is bodies
available per day, which is the roster-ceiling question the story raises and the code does not answer
yet.

⏳ **Left open:** the rival's settlement map (the counter is on screen, the move is not in the code),
and a roster ceiling — the story says six, the night's measurement says eight men is what wins it, and
those two have to be reconciled before either is locked.

438 core tests green (600 in total).

## 2026-09-10 — The season's screens

The core had the season; nothing on screen said so. Three pieces, all reading one engine-free model
(`Presentation/SeasonModel.cs`, tested):

- **The day screen's season line**, above everything else: `Day 12 of 180 · Kurogane moves in 3 days ·
  no fight filed — 3 days · Heads 1/3`. It is one counter for two things by design (the compulsory
  fight and the rival's move share a clock), and it **only warns a dojo that could take the field** —
  the penalty skips a roster that is in the infirmary, so a banner that still said "no fight filed"
  would be threatening the player with a price he is not going to pay. A bug fell out of writing it:
  `Season.FiledThisWeek` counted a dojo that had never filed a fight as filed, because day 0 sits
  inside the opening week's window.
- **`FinalNightScreen`** — deliberately not the day screen with a new label. There is no offer, no
  contract, no market and no day to close; the night's only decision is who goes out next, so the
  screen is that decision and its price: every man is listed with what is left of him (`hurt, 65% of
  him left`), a man past the wound ceiling is listed and not selectable, and the bout line says that
  withdrawing loses the night.
- **`SeasonEndScreen`** — two columns, the buried and **the men who walked out free**, with a headline
  that names which ending this is: a shut gate, an empty dojo, a lost bout, or the province's
  licensing. It clears the save on the way out; a finished run is not reopened.

The hub now routes on the season rather than on the tab: once the last night opens the four tabs are
gone, and once the run is over only the closing screen is left. 110 presentation tests green
(583 in total).

## 2026-09-10 — The last night, measured (and the rule that made it playable)

Step 7 left the five bouts written but unmeasured. The sim now plays the night — `--final-rest`
(the days before the night the dojo stops taking the field), `--final-powers`, `--final-enemies`,
`--night-wound-day`, `--night-wound-floor`, `--night-max-wound` — and reports it: who played it, bouts
won, men sent, dead, and the survival rate of each bout.

**The first measurement measured nothing.** 76% of dojos reached the night and **0.2 men** took the
field across all five bouts: almost every dojo could field nobody at all. Resting the party for the
last 3, 7 or 14 days did not move it, and neither did dropping the bouts to power 0.8 — below the
season's own first day. The cause was not the bouts. It was the night's fitness rule: it asked for
`IsFitForCampaign`, which means **zero infirmary days**, and a season's roster is never unmarked.

**So "no healing between the bouts" was given a body.** A wound is carried onto the field as
**health** rather than as a refusal: 5% of a man's health per infirmary day he is carrying, and past
10 days he cannot be carried to the field at all. That is the fiction (nobody withdraws from a
tournament for a cut) and it is the depth test the design asked for — every bout takes something off
the men who fought it. `BattleSetup.StartingHealthShare` was added for it, which is the first time a
fight starts with anyone less than whole.

| Knob | Swept | Result |
|---|---|---|
| Infirmary days allowed | 0 / 5 / 10 / 20 | won the night 0% / 5.0% / 6.5% / 6.5%. **0 is the old rule** — the reason the night was unplayable; past 10 nothing changes, because only a mortal wound carries more days |
| Health per wound day | 0 / 0.05 / 0.10 / 0.20 | 8.0% / 6.5% / 5.0% / 4.0%, men sent 7.2 → 6.1. Gentle and monotone: it prices the wound without deciding the night |
| Health floor | 0.2 vs 0.7 | identical to the figure. Under the locked pair (10 days × 0.05) the share stops at half health, so the floor never fires. Kept as a guard |

**Then the bouts themselves.** The written shape (powers 2.2/2.4/2.6/2.8/3.2 against 2/2/3/3/1) was
decided before it began: 28% of parties survived the **first** bout, 2% won the night. Swapping power
for bodies showed which axis carries the night — at the same powers, two men in the opening bout is
28% survival and one is 67%. The bouts were softened to 1.8-2.8 against 1/2/2/3/1.

> **Superseded the same day.** That calibration was run with the dojo's own systems switched off, and
> it was measuring the wrong dojo — see the next entry.

**The design's own claim, measured** (400 dojos × 180 days, party rested the last week, no retreat on
the night — pulling out of a bout is losing it):

| Roster carried into the night | Won all five | Men sent across it | Survived bouts 2 and 4 |
|---|---|---|---|
| six men | **5.5%** | 7.1 | 36% / 38% |
| eight men | **19.2%** | 10.3 | 58% / 49% |

Nothing else differed between those runs. **Depth is what wins the night.**

⏳ **Where these numbers are soft:** the sim's night policy is the strongest men still standing, bout
after bout. It does not train classes, does not kit a party for a final and does not hold a fresh man
back for Kurogane, so a player should beat these figures — they are the floor, not the expectation.
The season economy the night is reached through is also its own open question: in offer mode the dojo
spends 17-49% of its days hungry, which is what thins the roster before the night. That is an economy
finding, not a night finding, and it wants its own pass.

421 core tests green (573 in total).

## 2026-09-10 — Build order step 7: the season skeleton

The run now has an end. `Campaign/Season.cs` carries the clock and the books — the 180-day countdown,
the weekly compulsory-fight tick, the three-head gate, the score of the last night — and
`Campaign/FinalNight.cs` runs the five bouts. `DojoState` ticks the season when a day closes,
`Expedition` files the fight that answers the week, and a claimed contract counts a head. The whole of
it is bookkeeping over the day number: no engine, no randomness of its own.

**The measurement changed the rule, not just the number.** The design said "a week in which the dojo
files no fight costs the roster's honour". Measured — 200 hiding dojos (a policy that never takes the
field and lives on the training ground) against 200 ordinary ones, 180 days each:

| Missed-week penalty | Hider: ending honour / crossed the seppuku threshold | Ordinary dojo: ending honour / crossed |
|---|---|---|
| 0 | 50.0 / 0% | 50.3 / 8.5% |
| 4 | 41.0 / **0%** | 49.3 / 9.0% |
| **5** | **22.0 / 99% (day 91)** | **47.1 / 21%** |
| 6 | 11.4 / 100% (day 56) | 43.9 / 41.5% |
| 8 | 10.1 / 100% (day 35) | 38.6 / 59% |

Two things fell out of it:

1. **There is a hard floor at 4.** Honour decays 0.5 a day toward neutral, which is 3.5 a week, so any
   weekly penalty at or below 4 is **inert** — the hiding dojo settles at 41 and never reaches the
   threshold at all. The exploit-closing rule had a value range nobody had noticed was empty.
2. **The ordinary dojo misses half its weeks too.** A dojo that fights 40 times in 180 days still
   closes 13.4 of its 25 weeks with no fight filed — its party is in the infirmary. The first fix
   tried was a **grace week** (hiding is consecutive, bad luck is scattered) and the measurement
   killed it: the ordinary dojo's longest run of quiet weeks is **12.9**, one block at the end of the
   season, the same shape a hider's is. What separates them is not the pattern but **whether there was
   anyone to send**. With the week charged only when the dojo had men standing on 4 of its 7 days, the
   ordinary dojo's paid weeks fall 12.3 → 5.1 and the hider's stay at 21.8.

So the rule in the code is **"a week you chose to sit out costs honour"**: 5 per living warrior, one
free week at the start of a run, and nothing at all charged to a roster that was in bed. The grace
week is kept as a mercy, not as the thing that closes the exploit — it was credited with that for one
sweep and did not earn it.

**The last night.** Five bouts, no day between them: nothing heals, no upkeep is paid, no reward is
collected (gold on the last night is gold with nothing left to buy). A won bout that leaves nobody able
to stand ends the run on the spot. The bouts come at powers 2.2 / 2.4 / 2.6 / 2.8 / 3.2 against
2 / 2 / 3 / 3 / 1 men, the fifth being `Adversaries.KuroganeHead` — kept **out of** the encounter pool,
because he is met once, at the end, or not at all. ⏳ **These five numbers are not measured**: the
night's win rate needs a roster the campaign policy cannot yet build (it neither trains classes nor
kits a party for a final), so they are placeholders that the last night's own measurement pass will
revisit.

**Release.** The closing screen asks for "the men who walked out free", so releasing a warrior became a
thing the player can do: he leaves the roster alive, eats nothing, and is counted opposite the dead. A
man in the infirmary cannot be released — releasing a mouth before a hungry day and taking him back
after it would buy the upkeep rule off. The flag goes into the save beside death.

**The sim grew the tools this needed:** `--hide on|off` (the exploit as a policy), `--missed-week-honor`,
`--grace-weeks`, `--week-fit-days`, `--season-days`, and a season block in the campaign report (quiet
weeks, how many were paid for, the longest run, ending honour, the seppuku-threshold rate and the day it
was first crossed, heads and the gate).

573 tests green (420 core + 101 presentation + 52 sim).

## 2026-09-10 — Build order step 6: morale and Will

The ninth stat and the fast counter beside it. `WarriorStats.Willpower` (0-100, 50 by default),
`Warrior.Morale` with `MoraleScale`/`MoraleBand` in `Model/Warrior.cs`, the ledger and its numbers in
`Dojo/Morale.cs`, a **fifth drill** (`Drill.Meditation` — Will, and no secondary, because the price of
sitting still is the day the sword is not touched), and the first decision to leave the field that is
not the player's: `Battle.CheckPanic`.

**What Will does.** It does no damage. It sets how long a man stays: it resists the panic check, it
brakes morale's falls (never its rises), and when the chat says nothing it shifts the seppuku pardon
(`HonorTuning.WillPardonBonus` 0.20, reproducing the old number exactly at Will 50 so the honour
measurements still stand).

**The morale band, locked at ×0.94 / ×1.03** (`3v3`, 20.000 fights, `losing:0.7`):

| Morale | 0 | 25 | 50 | 75 | 100 |
|---|---|---|---|---|---|
| Victory (band 0.90/1.05) | 58.14% | 65.12% | 69.64% | 73.82% | 75.70% |

The first band tried, 0.90/1.05, swung the fight by **17.6 points** end to end — the multiplier lands
on six stats at once and compounds far harder than it reads, and at a floor of 0.70 the side collapses
outright (31.09%). Tightened to **0.94/1.03** the swing is 63.57% / 69.64% / 75.24%, about 12 points:
enough to tilt a close fight, not enough to decide one. Health and stamina are deliberately left out of
the multiplier — they are pools that carry between fights, and scaling them would take back wounds a
warrior already had.

**A rule that had to be rebuilt after measuring it.** Morale first rose by a flat amount every fed
day, and that sent every roster to 100 within a month: the whole lower half of the band became
unreachable and sweeping the floor changed *nothing* in a season (identical numbers at 1.00, 0.95, 0.90
and 0.80). It now works like honour: a daily **drift** back toward the middle from either side, and a
quiet day **settles** a warrior toward the middle but never above it. Everything above 50 has to be
bought — a victory, the bard's hall, or a feast.

**Panic, locked at 0.10 per check.** A warrior is put to the check while his health is under 30% or
his side is outnumbered, and the die is bent by Will (his nerve) and by morale (his condition) — GDD
§3's two-way bond seen from the field. He leaves **alone**; the player's key is still a team order, and
the event stream keeps them apart (`WarriorPanicked` against `RetreatCommanded`).

| Panic chance | 0 | 0.05 | 0.10 | 0.20 |
|---|---|---|---|---|
| `3v3` victory | 67.16% | 68.31% | 69.64% | 71.97% |
| `3v3` warrior deaths | 44.57% | 40.92% | 37.64% | 32.37% |
| Dojos closed (400 × 60 days) | 62.0% | 59.8% | 56.8% | 52.8% |

No knee, so the number is a budget again. **The finding worth keeping:** the rule cuts both ways — the
adversaries break too, and a rival who runs hands you the field. That is why panic *raises* the
player's victory rate instead of lowering it. ⚠️ It also exposes a leak: a fight the enemy fled pays
the **same** reward as a fight in which he was cut down, because the reward is computed from the
encounter's enemy health. Written down, not patched — it belongs with the season's economy (step 7).

**Sake, and Open Decision #14 closed.** `Resources.Sake` is the fourth stock and the only one the
day's own bill never buys: it sits in the store until the player calls a **feast**
(`DojoState.Feast()`), which drinks one measure per living warrior, lifts the whole roster by 15 and
then waits 7 days. A resource with a daily drain would only be a second food; one with no cooldown
would be a button pressed once at the start of the season.

Morale's other sources are the ones the design named: a victory +8, a defeat −10, breaking and running
another −6 on top, a comrade's death −6 **per man lost**, a hungry day −5, the bard's hall +2 a day
(which takes the bard off step 5's inert list — his hall is the only building whose whole output is
morale, and it is halved when the post is empty like any other).

The `Willpower` field went into the save with morale and the feast day; the class, path and morale all
travel with the warrior now.

550 tests green (397 core + 101 presentation + 52 sim; 11 new). New sim knobs: `--morale`,
`--morale-floor`, `--morale-ceiling`, `--panic-chance`, `--panic-health`, `--will-resist`,
`--morale-swing`, `--will-brake`. Three existing beds switched panic **off** deliberately
(`TestBuilders.PointBlank`, the charge tests, the retreat ladder in `BatchRunnerTests`): they measure
the player's key, and a warrior breaking on his own would sit inside every one of those measurements.

**Left open by this step:** the reward leak above; morale on the screens (nothing shows it yet);
whether a feast should also touch honour; and the fact that a fed, winning dojo sits at morale 50 for
most of a season — the band is priced at the ends, and the season only reaches those ends after a
defeat or a hungry week.

## 2026-09-10 — Build order step 5: facilities and staff

The school tree **became** the facility tree instead of standing beside one. Every node is now a
building with a construction time, and the nodes that GDD §10 names a profession for carry a
**post**: `Dojo/Staff.cs` (the eleven roles, the wages, `Facilities.IsBranchRole` / `IsInert`),
construction sites and per-building efficiency in `School.cs`, hiring, the payroll and
`TrainClass` in `DojoState`, and both the posts and the unfinished sites in the save.

**The three rules the step is built on**

1. **Gold is paid on the order, the building arrives later**, and no amount of gold shortens the
   wait — 6/10/14 days by tier, 12 for a class hall, 5-8 for the situational buildings.
   `SchoolTuning.BuildDaysFactor` scales all of them at once (0 = instant), which is what lets a
   measurement separate "the branch is weak" from "the branch arrived too late".
2. **An empty building works at half.** The share is applied to the building's **bonus**, never to
   the number it modifies: half of ×1.30 is ×1.15, not ×0.65.
3. **A gate is not halved.** No medicine bill, a limb kept, a life pulled back, ō-yoroi — these need
   the person outright, because half a gate is nothing.

**New buildings:** the forge (smith), the kitchen (cook), the shrine (monk), the bard's hall, the
diviner's hut, and the three class halls — torite, poison garden, archery range — which is how a
class is unlocked (the hall is the price; the training itself costs no further gold). Four of the
eleven posts have **no number in the code yet** (weapon master, bard, monk, diviner) because the
systems they belong to are not written; they can still be hired and still draw a wage, and
`Facilities.IsInert` names them so the sim does not pay for nothing.

**The infirmary trap — one failed fix, then the real one.** The branch was first rebound to
**limbs** (a physician + the bone setter's room save a quarter of the limbs a fight takes). Measured
over 400 dojos × 60 days it did **nothing at all**: a limb is lost on ~5% of warrior-fights, so a
quarter of them is invisible in a season, and with the physician's wage on top the branch came out
**worse than an empty infirmary** (79.2% of dojos closed against 75.5%). What actually drives a dojo
under is **death**, so the branch was bound there instead — GDD §10's own wording for the post,
"turns a mortal wound around":

| Mortal save | 0 | 0.15 | **0.25** | 0.40 |
|---|---|---|---|---|
| Dojos closed | 79.2% | 71.8% | **66.2%** | 60.0% |
| Days survived | 48 | 50 | 52 | 53 |

**0.25 locked.** At that value the health branch becomes the strongest single branch without
flattening the game (training-only 69.8%, steward-only 71.5%, control 79.2%); at 0.40 it is doing
too much of the player's work. The trap is closed: the branch that used to sell time now sells the
one loss training cannot undo. The limb save was **kept at 0.25 and is honestly marked as measuring
nothing** — it is the bone setter's tier and it costs nothing to leave in.

**The wage is a decision knob, not a survival knob** (1500 dojos, everything on):

| Branch wage | 0 | 6 | 16 |
|---|---|---|---|
| Dojos closed | 64.3% | 66.3% | 65.9% |
| Post-days per dojo | 44 | 37 | 19 |

Occupancy tracks the price cleanly and survival does not move outside noise (SE ±1.2 points). That
is exactly the gearbox GDD §10 asked for — the payroll is the thing the player cuts in a crisis, and
the policy that cuts it does not die of the cut. **6 gold a day for a branch post, 4 for a
situational one**, chosen as a budget rather than a threshold. The empty share (0-1) and the build
calendar (×0-×2) moved survival by nothing either; both stay where they were set (0.5, ×1), and what
build time really moves is occupancy (51 post-days at ×0 against 26 at ×2).

**Branch by branch** (400 dojos × 60 days, offers + market, staff on): control 79.2% closed,
training 69.8%, steward 71.5%, infirmary 66.2%, equipment 79.2% — **the forge pays for nothing** in
this bed, because repair money is a small line next to food and replacement. It is left in with the
finding written down rather than repriced blind; the ō-yoroi gate it carries is not written yet.

Also in this step: the broker widens the stall (+2 candidates, and a better chance of a
ready-classed candidate) and never touches a price; the cook cuts the **kitchen's** total food need,
not each man's bowl, because per head the saving vanishes in the rounding; the physician's post
zeroes the medicine bill outright.

539 tests green (386 core + 101 presentation + 52 sim; 19 new). New sim knobs: `--staff`,
`--branch-wage`, `--staff-wage`, `--empty-share`, `--build-days`, `--mortal-save`, `--limb-save`;
the campaign report now prints posts filled and post-days.

**Left open by this step:** the retired warrior as free staff (retirement itself is not written), the
ō-yoroi gate and the smith's forged weapons, the market's classed-candidate frequency measured
against a policy that actually trains classes (the campaign policy does not), and the four inert
posts. The screen work — a staff column on the school screen — has its model
(`SchoolNodeRow.Role`/`Staffed`, `SchoolSummary.DailyWage`) but no Godot scene yet.

## 2026-09-10 — Build order step 4: the class layer (`class × implement`)

`Model/WarriorClass.cs` (engine-free): three classes beside the path — **Torite** (catching),
**Dokushi** (poison), **Kyūdō** (range) — plus `None`, and `ClassAptitude`, which holds the shape of
the rule and the limb-class fitness matrix while `CombatTuning` holds the numbers so the sim can
sweep them. `Warrior.Class` is a settable property; it goes into the save (a class is bought with a
facility, so a reload must not erase it) and, unlike the path, it is **not** final — an arm closes
the torite and the kyūdō, and the maimed warrior chooses again among what is left.

**Where the product bites.** `chance = base × class × implement`:

| Mechanic | Class half | Implement half | With no class |
|---|---|---|---|
| Catching | torite only | `CatchSkill` (jitte 1.0, sai 1.25), else `UnskilledCatchImplementFactor` **0.10** | **zero** |
| Poison | dokushi 1.0, else `UnclassedPoisonFactor` **0.6** | the dose on the blade | a reduced dose |
| Range | kyūdō 1.0, else `UnclassedRangeFactor` **0.85** | the throwing slot | a shallower hand |

Catching is the system's **one hard zero** — an active skill, not a property of the hook. Poison and
range were deliberately left open and only scaled: zeroed, a poisoned tantō would be dead equipment
until a facility stood, and the poison numbers locked in GDD §7 would have gone with it. The sweep
priced that argument exactly — at share 0 the poisoned knife wins **6.18%**, where the *clean* tantō
it is built from wins 34.89%.

**The dose sweep** (`poison-unclassed`, 20.000 fights, `losing:0.7`; the trained control `poison` is
76.09%, a clean tantō 34.89%, a katana 78.17%):

| Classless share | 0 | 0.3 | **0.6** | 0.8 | 1.0 |
|---|---|---|---|---|---|
| Victory | 6.18% | 40.05% | **65.12%** | 71.93% | 76.09% |

The first sweep of the four with a real shape: concave, +25 points for the first 0.3, +6.8 and +4.2
for the two after. **0.6 locked** — it sits past the knee, keeps the untrained knife clearly better
than a zero dose and clearly worse than a sword (65.12% against 78.17%), and leaves the class worth
11 points. What the class buys is that the poisoned knife becomes a *choice* rather than a handicap.

**The throw sweep** (`thrown`, the same master with a shuriken slot; `thrown-kyudo` is the class end):

| Classless share | 0.5 | 0.7 | **0.85** | 1.0 |
|---|---|---|---|---|
| Victory | 79.59% | 81.34% | 82.37% | 83.52% |

Linear, no knee — a budget again. **0.85 locked as a placeholder**: the range class's real payoff is
the yumi, which is not written yet, so a deep penalty here would only tax the throwing slot every
warrior carries. Revisit when the bow lands.

**The catch floor sweep** (`torite-katana` — the same catching warrior holding a sword):

| Floor | 0 | 0.05 | **0.10** | 0.20 | 0.35 |
|---|---|---|---|---|---|
| Victory | 78.17% | 79.80% | 81.07% | 83.27% | 86.33% |

Linear again, 2.3 points per 0.1. **0.10 locked** — the number GDD §4 already named — because the
sweep gives no threshold to prefer over it.

**The finding this step really produced: the catching implement is now dominated.** With both arms
of the comparison carrying the same class, the torite fights better with a katana than with his own
implement in every measured matchup:

| Matchup | Torite + jitte | Torite + katana | Classless + katana |
|---|---|---|---|
| duel | 78.65% | **81.07%** | 78.17% |
| vs two-handed | 34.67% | **36.65%** | 34.30% |
| vs ō-yoroi | 35.59% | **63.63%** | 60.66% |
| 3v3 | 66.19% | **66.69%** | 67.16% |

The cause is not the floor: a catch is worth a great deal **per event** (it erases a tetsubo blow and
binds), so a rare catch collects most of the value, while the jitte pays 8 damage a strike for the
frequency. This is Open Decision **#19** widening — the heavy-weapon brake had already gone at step
2 — and the answer belongs to the implements' own prices (damage, speed, the disarm share), not to
the class layer. Left for Phase 9, written down rather than patched.

`jitte-unclassed` is the hard zero's price tag: the same jitte with no class wins **41.76%** against
the classed 78.65%. The old single-axis catch measurements are retired by that number — under the
product rule they were measuring two different warriors.

**Untouched, and shown to be untouched:** the blunt/cutting trade (`blade` 92.33% / `club` 93.38%,
armoured 88.38% / 91.09%) and the patrol (96.69%). Stunning has no class half, so it was re-measured
only as a control.

New scenarios: `jitte-unclassed`, `torite-katana`, `torite-katana-heavy`, `torite-katana-armored`,
`3v3-torite-katana`, `poison-unclassed`, `thrown`, `thrown-kyudo`. New sim knobs:
`--class-catch-floor`, `--class-poison-share`, `--class-range-share`. The catch and poison tests were
re-grounded (their defenders and poisoners now carry the class the rule requires), and
`AnOrdinaryWeaponNeverCatches` was replaced by the two ends of the product:
`AWarriorWithNoClassNeverCatches` and `AWrongImplementCatchesWeaklyButNotNever`.

520 tests green (369 core + 99 presentation + 52 sim; 8 new).

**Left open by this step:** how a class is bought (the facility, its gold and build time) and the
market's rare ready-classed candidate — both belong to step 5. The yumi, and with it the kyūdō
class's real content, is not written.

## 2026-09-10 — Build order step 3: a fight grows the warrior

`Dojo/CombatSchooling.cs` (engine-free, stateless, no die of its own — the fight's randomness is
already in the counters it reads). The largest of four counters — swings, blocks, dodges, blows
taken — names the drill the fight amounted to, and the gain runs through `TrainingGround` with
`TrainingTuning.FightGapClosed` in place of the daily rate, so the two roads of growth share the
secondary share, the ceilings and the talent multiplier. `BattleAftermath` writes the lesson for
every warrior who came off the field (the dead learn nothing, the wounded do) and counts the fight
as a day of schooling toward the path. `--fight-rate` was added to the sim.

**The sweep** (400 dojos × 60 days, two beds):

| Fight rate | `patrol`: best warrior | Deaths/dojo | Purse | Offers: best warrior |
|---|---|---|---|---|
| 0 | 423 (+36) | 5.74 | 3200 | 469 (+82) |
| 0.04 | 468 (+81) | 3.98 | 4005 | 508 (+121) |
| **0.08** | **493 (+106)** | **3.16** | **4454** | **529 (+142)** |
| 0.16 | 514 (+127) | 2.35 | 5019 | 553 (+166) |

Linear again, no knee — the third sweep in a row with none, which is itself worth noting: these
share-of-the-gap rules do not produce thresholds, so their numbers are always budgets. **0.08
locked** (twice the training rate): the design requires the risky road to pay better than the safe
one, which rules out ≤0.04, and at 0.16 the rule's own feedback — a dojo that grows faster loses
fewer men — has removed 60% of the base difficulty, the same failure the training lock rejected.

**The brake that was tested and held:** training does not become decorative. Best-warrior score with
training on against off is +37 / +37 / +32 / +30 across the four rates. A fight teaches only the
axis it consisted of, so drilling stays the only way to **shape** a warrior.

**A landmark moved, deliberately left alone.** The 0.04 training rate was locked against the ~473
score at which the market ceiling starts to bite; with fights teaching, the same dojo reaches 493
(529 in offer mode), so the market stops being a replacement route earlier in the season. The two
rates share one ceiling and only their ratio matters, so retuning waits for facilities (step 5),
where the ceiling becomes a purchase. Written into GDD §11 as a warning banner rather than a new
number.

**One existing sim test had to be re-grounded:** `TrainingShowsUpAsStatGrowth` used a zero training
rate as its control, which now still grows through fighting. Both sides of that test zero
`FightGapClosed` so the control measures training alone.

512 tests green (362 core + 99 presentation + 52 sim; 8 new).

## 2026-09-10 — Build order step 2: a stun drops the weapon

The third trigger of dropping a weapon, and the first in which the **defender** loses one: the
other two spend the striker's weapon. `CombatTuning.StunDisarmChance` is rolled right after the
stun holds, scaled by the **stunned warrior's own** `DisarmFactor` (cutting 1.0, piercing 0.6,
blunt 0.2), and the weapon falls behind the man who landed the blow — the disarm round's lesson
was that the direction, not the distance, carries the cost. `--stun-disarm` was added to the sim
so the axis could be swept on its own.

**The sweep** (0 / 0.05 / 0.10 / 0.15 / 0.30 / 0.45; 24 scenarios, 20.000 fights, `losing:0.7`):

| Scenario | off | 0.15 | 0.30 |
|---|---|---|---|
| `club` (blunt master) | 92.90% | **93.38%** | 94.08% |
| `blade` (cutting master) | 92.25% | 92.33% | 92.26% |
| `veteran` (nodachi vs tetsubō) | 78.42% | **75.80%** | 73.47% |
| `3v3` | 66.80% | 66.28% | 65.50% |
| `patrol` (the economy's bed) | 96.69% | 96.69% | 96.62% |
| `spear-armored` | 76.07% | 76.08% | 76.05% |

**There is no knee** — every line is linear in the chance, so the number is a budget rather than a
threshold. **0.15 locked.** There the rule is visible without being paid for twice: 5.06% of the
player's warrior-fights end up empty-handed (13% of them are picked up again, against 15-17% at
the higher settings), the blunt class gains its third win (+0.48 as the striker, and it holds its
own grip when struck), and the ordinary encounter does not feel it at all. The sharpest effect is
the one the rule is for — a heavy blade in front of a club loses 2.6 points.

**A finding that is not about this rule.** While looking for a brake to hang the number on, the
old one was tested and is gone. The catch round had locked "the catching implement is still the
wrong choice against an enemy carrying a nodachi"; with the stun-drop rule **switched off** and
100.000 fights, `jitte-heavy` takes 34.65% against `katana-heavy`'s 34.38% — it has crossed zero,
and `CatchTwoHandedFactor` no longer holds anything back. The margin had already been thinned to
0.24 points by the block rule, which is under the 20.000-fight bed's own standard error (±0.34
points at a 35% victory rate) — so it could not have been seen there at all. Written up as GDD
Open Decision **#19**, for Phase 9; nothing was re-locked on one measurement. The working lesson:
**a margin under ~0.5 points cannot be defended on a 20.000-fight bed.**

503 tests green (352 core + 99 presentation + 52 sim).

## 2026-09-10 — Build order step 1: the fight's time limit goes, a stall guard stays

`MaxBattleSeconds` (180 s) did two jobs at once. The **design** job — "a fight past 180 s is a
draw" — is gone. The **safety** job could not go: `Battle.Run()` is a `while` with no other exit,
and two sides that can neither close nor finish would loop forever. So the property became
`CombatTuning.StallGuardSeconds` (**900 s**) and `BattleOutcome.TimeLimit` became
`BattleOutcome.Stalled`, documented as **an anomaly, not a result** — a fight that reaches it is a
bug to reproduce from its seed, never a share to balance around. The sim counts it as
`BatchReport.Stalls` and shouts a line when it is above zero; `BatchReport.LongestSeconds` /
`LongestSeed` were added with it, so the tail can be watched instead of guessed at.

**Measured first, as the step required** — 24 scenarios × 20.000 fights, `--policy losing:0.7`:

| | Before (180 s draw) | After (900 s guard) |
|---|---|---|
| Fights ending on the limit | **0 of 480.000** (0.00% in every scenario) | 0 stalls |
| Victory rate, every scenario | duel 62.61 · patrol 96.69 · jitte-armored 35.44 · ambush 2.86 | **identical to the digit** |
| Mean duration | 10.8-45.3 s | identical |

**Why nothing moved:** the limit was never reached. The tail says how much room there was — over
20.000 fights the longest single fight per scenario was `jitte-armored` **138.6 s** (seed 13441),
then `tanto-armored` 93.5 s; p99.9 for the worst scenario was 102.6 s. So the old 180 s ceiling had
only ~1.3× headroom over the worst real fight — close enough that a slower future rule (heavier
armour, more binding) would have started clipping fights into draws without anyone noticing. The
new guard sits ~6.5× above that worst fight, which is the point: it must never be reachable by a
fight that is merely slow.

Costs nothing, changes nothing today, and removes a rule that would have started lying later.

⏳ **Left open:** what the **dojo layer** does if a fight ever hits the guard. In the sim it is a
counted anomaly; in the game it cannot simply hang. It is decided with build-order step 8 (real
time), since the answer depends on whether the day keeps running underneath.

500 tests green (349 core + 99 presentation + 52 sim; `Domina.Chat.Tests` still contains no tests).
