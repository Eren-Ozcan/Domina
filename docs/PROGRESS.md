# Status Log

Last updated: 2026-09-04 (Phase 3 started — roster, day cycle, save system and post-fight accounting)

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
