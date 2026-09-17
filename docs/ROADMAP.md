# Development Roadmap

> For the design decisions see `GDD.md`. This file describes **how it gets built**.
> The effort estimates are **relative** (S/M/L/XL), not a calendar.

> ⚠️ **2026-09-07 — a decision pass changed this plan.** The decision pass and the
> profession pass over `COMPARISON-DOMINA.md` brought in new systems and invalidated a few
> locked rules. The phase boundaries are gathered in the "New work from the decision pass"
> section below; distributing them into the phases will be done once the open
> **9.1 core decision** is made (that decision sets the architecture directly).

## New work from the decision pass (2026-09-07)

**To be decided first:** GDD "Open Decisions" #13 — is resolution inside the engine or
outside it? This decision sets the shape of phase 1 and of the balance setup; everything
under it depends on it.

**Changes that reopen existing phases**

| Change | Affected | Note |
|---|---|---|
| **Discrete day → pausable real time** | Phase 1, Phase 3 | The day's close, event triggering, `AdvanceDay` and the chat vote all assume a discrete day. The core stays on a fixed tick; what changes is the layer that consumes it. **The single largest risk item** |
| ~~**`BattleOutcome.TimeLimit` goes away**~~ | Phase 1 | **Done (2026-09-10).** A fight lasts until someone falls; what is left is a stall guard, and reaching it is an anomaly |
| ~~**Fights grant stats**~~ | Phase 1, Phase 3 | **Done (2026-09-10).** Training stops being the only route of growth; the "going on an expedition is pure loss" problem closes |
| ~~**A stun drops the weapon**~~ | Phase 1 | **Done (2026-09-10).** A third trigger; for the first time the defender loses his weapon |
| **The block splits into stat/equipment** | Phase 1, Phase 2 | The frequency comes from Defence, the effect from the ō-sode |
| **Reward band 0.75-1.25** | Phase 5, Phase 9 | Surrender survival uses the same ratio |
| **All the balance numbers are invalid** | Phase 9 → every phase | Measurement will no longer be one pass but done **as each system enters the code** |

**New systems** (to be distributed into the phases)

- **The class system** — ~~the class list~~, ~~the limb-class fitness matrix~~, ~~the `class × implement` product~~ (all three landed at build-order step 4, 2026-09-10); what is left is **unlocking with a facility**, which belongs to step 5
- ~~**Morale + Will**~~ — landed at build-order step 6 (2026-09-10): the 9th stat, the morale band, the two-way bond, and both the seppuku and panic checks
- **Staff and facilities** — ~~11 professions, a daily wage, four upgrade branches, half efficiency for an empty facility, build time~~ (landed at build-order step 5, 2026-09-10); what is left is **placing retired warriors**, which waits on retirement itself, and the four posts whose systems are not written (weapon master, bard, monk, diviner)
- **NPC relationships** — three parties, five tiers, the table of actions that raise and lower them, the effects of a tier
- **Omamori** — the charm definitions, warrior and staff slots, carrying/selling, temple supply
- ~~**The season skeleton**~~ — landed at build-order step 7 (2026-09-10): the 180-day countdown, the weekly tick, the 3-head gate, the five bouts and the closing screen's figures; the last night's own numbers were measured the same day (a developed dojo with eight men wins it 38.5%, with six 13.5%, an undeveloped one 0% — a school and depth, not one of the two), and the screens followed: the season's line on the day screen, `FinalNightScreen` (one bout at a time) and `SeasonEndScreen` (the dead and the freed)
- **The story cast** — fixed-name targets, a horde that grows on defeat, the night raid
- **Field features** — fog, mud, a narrow bridge, night, tate
- **The chat bot and the pool window** — the write direction, 1-hour freshness, `!no` session lifetime
- **The crowd indicator** — a collective indicator instead of fake chat in single player
- **Screens** — the market, facilities/staff, the offer queue, the final tournament (phase 3 counted as "done"; the dojo layer grew)

## Build order after the decision pass (2026-09-08)

The screens are not the gap. The dojo loop is already closed end to end — `TitleScreen` →
`DojoHub` (day / roster / market / school) → `BattleArena` → the report → the save; the
models behind them (`MarketModel`, `SchoolModel`, `OfferModel`, `RosterModel`) are
engine-free and tested. What is missing is that **none of the decision pass's systems are in
the code yet**: there is no class, staff, facility, morale or season type, and
`BattleOutcome.TimeLimit` is still there (**step 1 closed it on 2026-09-10**).

The order below is a dependency chain — each step is its own commit, and each is measured
before the next one starts (20.000 fights, `losing:0.7`, against a control that differs by
one thing only; both the number and the sweeps that found nothing are written down).

| # | Step | Why here |
|---|---|---|
| 0 | **Open Decision #13** (leaning: (a) with real time in Godot) | Everything below is written into the engine-free core |
| ~~1~~ | ~~Remove `BattleOutcome.TimeLimit`~~ | **Done (2026-09-10)** — measured before and after, nothing moved. The record is in `docs/PROGRESS.md` |
| ~~2~~ | ~~A stun drops the weapon~~ | **Done (2026-09-10)** — locked at 0.15 after a 0-0.45 sweep; the rule and the measurement are in GDD §7 |
| ~~3~~ | ~~Fights grant stats~~ | **Done (2026-09-10)** — `Dojo/CombatSchooling.cs`, rate locked at 0.08; the rule and the measurement are in GDD §11 |
| ~~4~~ | ~~The class layer (`class × implement`)~~ | **Done (2026-09-10)** — three classes (torite / dokushi / kyūdō), catching zeroed without a class, poison and range scaled; the numbers and the measurement are in GDD §4 |
| ~~5~~ | ~~Facilities + staff~~ | **Done (2026-09-10)** — the school tree became the facility tree (build time, posts, half efficiency), the eleven roles draw a daily wage, class halls unlock the classes, and the infirmary trap is closed by binding the branch to **death** rather than to time; GDD §10 and `docs/PROGRESS.md` carry the numbers |
| ~~6~~ | ~~Morale + Will~~ | **Done (2026-09-10)** — the ninth stat, the morale band (×0.94/×1.03), the panic check (0.10), the meditation drill and sake/feasts; #14 closed with it. GDD §3 and `docs/PROGRESS.md` carry the numbers |
| ~~7~~ | ~~The season skeleton~~ | **Done (2026-09-10)** — the 180-day clock, the weekly tick, the 3-head gate and the five bouts of the last night are in `Campaign/Season.cs` and `Campaign/FinalNight.cs`. The missed-week honour penalty was locked at **5** and the rule was changed by the measurement: a week is only charged when the dojo **had men standing** (GDD §10). The five bouts were then measured too (2026-09-10): powers **2.2-3.2** against **2/3/3/4/1** men, and a wound carried onto the field as health instead of barring the man. The night asks for a school **and** depth — a developed dojo with eight men wins it 38.5% of the time, with six 13.5%, and a dojo that built nothing 0% |
| 7b | **Step 7's audit** — done 2026-09-10 | The gaps step 7 left: the seppuku tribunal wired into the day loop (`Dojo/Tribunal.cs` — until then honour was written and never read), the difficulty tiers (`Campaign/Difficulty.cs`), a starting roster drawn 3-5 instead of a fixed 4, releasing a man from the roster screen, the missed week and the verdict printed in the day's report, a save test for a night in progress, and the last night recalibrated against a dojo that actually builds its school. Then the roster ceiling was closed too: the story's six is a **starting** ceiling, and the **quarters** branch (barracks/long house, +2 beds each) buys the rest — measured, beds from day one are worth 30-35% of nights won against 10-11% for beds bought late, because a bed bought in the fifth month holds a raw recruit. **Closed 2026-09-12:** the rival's settlement map (`Dojo/Province.cs`) — and it turned out to be the biggest number measured on this project: the hiding dojo closes 7.5% of the time without the map and **72.8%** with it, while a dojo that answers the map pays about one point for it |
| ~~8~~ | ~~Discrete day → pausable real time~~ | **Done (2026-09-13)** — and it was the cheapest of the eight, because the core was not touched: `DayClock` / `DayInterrupt` / `DayLog` in the presentation layer, the ticking in `DojoHub._Process`, and a speed bar with ‖/1×/2×/4× plus the space bar. `AdvanceDay()` is still the atomic unit, so every measured dojo number stands. The stall guard's dojo answer landed with it. The record is in `docs/PROGRESS.md` |

**Step 1 in detail** (done 2026-09-10 — `MaxBattleSeconds` became `StallGuardSeconds`, 900 s,
and `BattleOutcome.TimeLimit` became `BattleOutcome.Stalled`). `MaxBattleSeconds` did two jobs at
once, and only one of them was removed. The **design** job — "a fight past 180 s is a draw" — goes. The
**safety** job cannot: a fight in which neither side can finish the other (both withdrawing,
the range never closing, no poison running) would otherwise loop forever, and `Battle.Run()`
is a `while` with no other exit. So:

1. **Measure first.** Run every scenario as it stands and write down the time-limit rate per
   scenario. Without that number a later shift in the victory rates cannot be told apart from
   the rule's removal.
2. **Remove the outcome, keep a guard.** `BattleOutcome.TimeLimit` and its event go; a hard
   tick ceiling stays, raised far above any real fight, and hitting it is **not** a result —
   it is an anomaly the sim counts and reports. A fight that reaches it is a bug to be looked
   at, not a draw to be balanced around.
3. **Re-run and compare** the same scenarios against the step 1 numbers.

**Step 4 in detail** (done 2026-09-10). The class list was settled before any code: **torite**
(catching), **dokushi** (poison), **kyūdō** (range), plus the classless warrior — and no blunt class,
because stunning stays open to everyone. Catching is the one mechanic a classless warrior cannot open
at all; poison and range are scaled rather than zeroed, so nothing already measured in §7 was thrown
away. The numbers (catch floor 0.10, classless dose 0.6, classless throw 0.85), the sweeps behind
them and the finding that the catching implement is now dominated are in GDD §4 and
`docs/PROGRESS.md`.

⏳ **Left open by this step:** ~~how a class is bought~~ and ~~the market's rare ready-classed
candidate~~ — both landed at step 5. ~~The **yumi** is not written, so the kyūdō class carries a
placeholder number until it is.~~ **Written 2026-09-13** (`ThrownWeapon.Yumi()`): range 1200, damage
26, 10 arrows, a 1.5 s draw, and the only thrown implement gated on the class (`UntrainedShare` 0.45).
Measured over 20.000 fights a cell, the class is worth +1.06 points on a shuriken and **+5.04 on the
yumi** — which is where §4 always said the range class's payoff belonged. ⏳ **What it leaves open:**
nothing in the dojo sells a throwing implement at all, so the bow has no price and a kyūdō cannot buy
his own weapon in a running season. A shop gap, listed with the Phase 4 work below.

**Step 5 in detail** (done 2026-09-10). The school tree **became** the facility tree rather than
gaining a second system beside it: the tiers stayed as upgrades, every node gained a construction
time, and the nodes GDD names a profession for gained a post. An empty building works at half; a
gate (no medicine bill, a limb kept, a life pulled back) needs the person outright. The
**infirmary trap** was fixed only on the second attempt — rebinding the branch to limbs measured as
nothing, so it was bound to death instead ("turns a mortal wound around", 0.25). The wage turned out
to move occupancy, not survival, which is the gearbox the design asked for.

**Step 6 in detail** (done 2026-09-10). Will is the only stat that does no damage — it decides how
long a man stays. Morale is the fast counter beside it, and the pair is bound both ways: Will brakes
morale's falls, morale bends the checks that rest on Will. Two things were learned by measuring rather
than by design: a flat daily morale gain pins every roster at 100 (so a quiet day now only **settles** a
warrior toward the middle, and highs drift back), and the panic rule applies to the adversaries too, so
it *raises* the player's victory rate. ⚠️ It also showed that a fight the enemy fled pays the same
reward as one where he fell — an economy leak for step 7.

⏳ ~~**Left open by step 6:** morale on the screens.~~ **Closed 2026-09-12** — the roster prints the
dojo's spirits and each man's, and the feast has its button.
 **Closed 2026-09-12:** a feast **never** touches
honour (GDD §3 — morale is the inside counter, honour the outside one), and the fled-enemy reward
leak went with the rule that an adversary no longer flees at all — he stands on the road and
**yields** in a match (GDD §5). It cost the player nine points of victory on `3v3` (69.64% → 60.63%)
— and that cost was **absorbed the same day** by the difficulty curve being re-derived for a 180-day
season: on the corrected slope the documented policy loses 4.1% of its warriors per fight and closes
25% of the time, against 10.4% and 52.5% before. The fight bed is harder; the season is not.

⏳ ~~**Left open by step 5**~~ — **all closed 2026-09-12**: retirement and the master of the house who
holds a post for no wage; the ō-yoroi gate and the sword forge (the equipment branch's two upper
tiers); the classed candidate measured against a policy that trains its own (0.05 stands); and the
school screen's payroll column. **Closed
2026-09-12:** the four inert posts — the bard landed with morale at step 6, and the weapon master, the
monk and the diviner with weapon mastery, the omamori and the reading of the day's offer (GDD §10).
Measured with them: mastery is worth +2.58 points of victory at full but the hall that teaches it
arrives too late in a season to move anything, the charms are worth buying (dojos closed
89.8% → 86.8%), and the funeral rite measured as nothing.

⏳ ~~**Left open by this step:** what the dojo layer does if a fight ever hits the guard.~~
**Closed 2026-09-13 with step 8.** The day does **not** run underneath a fight (the arena holds the
clock), so the answer needed no second clock: a fight that hits the guard closes as an **unresolved
encounter** — no reward, no honour, no morale swing, while the wounds, the wear, the lesson and the day
stand and the week's fight still counts as filed. The guard is an anomaly, not a result, so nothing
about it is balanced around; the sim keeps counting it exactly as before (GDD §7, §10).

**Step 8 in detail** (done 2026-09-13). Three shapes were on the table — a sub-day tick in the core, a
continuous clock with day-granular bookkeeping, or the core left alone with the pacing in the engine.
The third was taken because of Open Decision **#13**: a core-side clock is a write-off if resolution
ever moves into Godot, and a core-side tick would void every dojo number already measured (training
rate, wages, recovery, the week's tick, the difficulty rungs) for something the player cannot see. What
was written: `DayClock` (real seconds in, day rollovers out; speeds ‖/1×/2×/4×; a 3-rollover per-frame
cap; **named holds**, which are the game saying "not now" and are kept apart from the player's own
pause), `DayInterrupt` (which closed days stop the clock — what has to be **answered**, never a
building finished), `DayLog` (a closed day as text, now that the log is the only place a rival's move or
a verdict is seen), the ticking in `DojoHub._Process`, and the clock bar in the navigation chrome.
`SecondsPerDay = 60` is **not** a measured number and is flagged as such in the code: nothing in the sim
has an opinion about how long a quiet day should feel. Three holds are wired: the arena, a party being
picked (released in `DayScreen._ExitTree` so it cannot outlive its screen) and the window losing focus.

⏳ ~~**Left open by this step:** the **timed offer queue** (GDD §10).~~ **Closed 2026-09-13** — the
board stands for **2 days** (`EncounterTuning.OfferLifeDays`), yesterday's posting beside today's, each
printing its last day; only the taken postings go into the save, so the board stays a pure function of
the seed. It cost the curve: a standing job is an older, weaker and therefore cheaper job that eats a
whole day, which took the last night from 17.1% to 8.9% until `PowerPerDay` was re-derived from 0.011
to **0.010**. Still open with it: an unclaimed posting should probably grow **more** attractive as it
ages, or the queue stays filler rather than a decision (GDD §10).

**Decisions that do not wait on code:** none left of that sitting — #16 the opponent pool and #17 the
rival school closed on 2026-09-10, #15 the save backup on 2026-09-12, and **#3 adversary behaviour on
2026-09-13** (`Combat/TargetProfile.cs`: per-kind multipliers over the target-selection weights,
measured at zero against the season and kept as character). **Decisions that wait on measurement:** #18 the round's numbers (with steps
2-7), the rest of #5 the economy (staff wages, the daily upkeep — not before step 5), #8 the
honour thresholds (playtest, so after step 7). #14 is decided with step 6, #17 stays parked,
and #7 the game's name blocks nothing.

---

## Core Principle: The Core First, The Engine Second

The most critical architectural decision: **the simulation core must not depend on Godot
at all.**

```
src/
  Core/          → a pure C# class library (NO Godot reference)
                   stats, the combat resolver, honour, economy, injury, RNG, the save model
  Chat/          → pure C# (NO Godot reference)
                   Twitch/Kick adapters, command parsing, the vote engine
  Game/          → the Godot 4 project — it uses Core and Chat via ProjectReference
                   scenes, animation, UI, sound, Steam
tests/
  Core.Tests/    → xUnit — runs without opening Godot
  Chat.Tests/    → xUnit — with a fake chat stream
```

**Why:** Combat is fully automatic and chat affects the outcome. A system like that can
only be balanced by **simulating thousands of fights in seconds without opening the
engine**. With a core tied to the UI, balance work becomes impossible.

**Deterministic RNG (set up in phase 1, cannot be added later):** every fight starts with
a `seed`. The same seed + the same inputs = the same result. That gives three things:
1. Bugs can be reproduced ("the warrior died wrongly on this seed")
2. Balance testing is automated (10,000 fights in batch simulation)
3. A fight replay feature comes free later

---

## Phase 0 — Scaffolding · S

**Goal:** an empty but solid foundation.

- [x] `git init`, `.gitignore` (Godot + .NET + `docs/store-assets-originals/`)
- [x] The Godot 4.x project + C# (.NET) set up and verified to work
- [x] The `src/` + `tests/` folder structure above, the `.csproj` references
- [x] xUnit set up, `dotnet test` working
- [x] `dotnet format` / analyzer rules
- [x] README + `docs/` (the GDD and ROADMAP already exist)
- [x] GitHub Actions: build + test on push

**Acceptance:** `dotnet test` is green, an empty Godot scene opens, CI passes.

---

## Phase 1 — The Simulation Core (No Visuals) · L

**Goal:** a full fight can be simulated in the console without opening Godot.

### 1.1 The data model
- [x] `Warrior`: HP, Aggression, Defence, Evasion, Strength, Stamina, Honour
- [x] `Injury` / `Disability`: arm, leg, eye — permanent stat modifiers
- [x] `Weapon`: the cutting/blunt distinction, damage, the number of hands (one/two)
- [x] `Armor`: a defence value, a reducer of limb-loss risk
- [x] Identity: every warrior has a **unique ID**; the name is a separate field
      (the same name can be reused at different times — GDD §6)

### 1.2 Deterministic RNG
- [x] The `IRandomSource` interface + a seeded implementation
- [x] **Rule:** `System.Random` is not used directly inside Core, always this interface
- [x] A fake (scripted) RNG implementation for tests

### 1.3 The combat resolver
- [x] Strike resolution: Aggression → the evasion die → Defence → damage (GDD §4)
- [x] Stamina consumption and the low-stamina penalties
- [x] Limb-loss risk: the blow/maxHP ratio × the weapon type × armour (GDD §7)
- [x] The outcome tree: light / heavy+surrender / heavy+no intervention (death)
- [x] **The charge:** the trigger is not a fixed distance threshold but an **assessment of
      the opportunity** — "nobody can hit me and I have time to finish my windup"; the
      distance needed derives from the enemy's reach and speed. Using an opportunity when it
      appears depends on a die **scaled by Aggression**. It **gathers in place and scatters
      on the first hit taken**, defence continues at its normal rate while running and
      opportunity attacks are taken along the way,
      and there is a damage multiplier on arrival; a fleeing target is not charged and the
      "pull out" command interrupts it (GDD §4). Six numbers, measured and locked on 2026-09-02
- [x] 1v1, 2vX, 3vX multi-warrior support
- [x] The fight produces an **event stream** — the visualisation will consume it
      (`AttackLanded`, `AttackDodged`, `WarriorDismembered`, `WarriorDied`, `RetreatStarted`...)

> **Critical:** the resolver knows nothing about animation. It only produces events.
> The visualisation in phase 2 plays those events back. If this separation breaks, balance
> testing dies.

### 1.4 The surrender logic
- [x] The interruptible/uninterruptible state machine (GDD §5)
- [x] Command buffering (holding it while locked into an attack)
- [x] The escape window: evasion/block disabled, an opportunity attack for the opponent

### 1.5 The honour engine
- [x] A 0-100 scale, decay, computed from fight performance
- [x] `bushiRatio` → `rewardMultiplier` clamp(0.5, 1.5)
- [x] Detecting the seppuku threshold + a **queue** (holding it while a fight is live)
- [x] The 15-minute pardon cooldown

### 1.6 The batch simulation tool
- [x] CLI: run N fights over a seed range, write the death/maiming/victory rates to CSV
- [x] This tool is the foundation of all balance work — **if it is not built in phase 1, phase 9 hurts**
- [x] `--policy` for the escape policy that stands in for the player's "pull out" key
      (required, because limb loss only happens in fights with intervention)

**Acceptance:** ✅ *(2026-08-06)*
- [x] A 3v3 fight is simulated from start to finish with `dotnet test`
- [x] The same seed gives the same result 100 times (the determinism test)
- [x] 10,000 fights run in < 10 seconds — measured: **~1 s** (Release), ~2 s (Debug)
- [x] The unit tests for limb loss, surrender, honour and the seppuku queue are green (112 tests)

**Risk:** the balance numbers will not hold in this phase — that is normal. The goal is a
**working and measurable** system, not a balanced one.

---

## Phase 2 — Combat Visualisation · M

**Goal:** phase 1's event stream turns into a fight that can be watched on screen.

### 2.1 The style-independent backbone — ✅ *(2026-08-12)*
- [x] The warrior scene structure, **modular limbs** — a 15-part cutout rig, with the
      severing points at the shoulder and the hip (GDD §2, §7)
- [x] The animation state machine: wait → close → attack → react → repeat
- [x] Wiring phase 1's events to the animation (event → visual reaction) — **the attack's
      three outcomes are distinguishable**: a hit shakes, a miss swings through, an evasion
      pulls aside; an opportunity attack has its own strike
- [x] Matching the cancel windows to the animation timing
- [x] **Limb severing:** detaching the limb from the rig, blood VFX, a permanent model change
- [ ] The charge pose: a forward-leaning run — the mirror of fleeing, "charging" in the HUD.
      **The windup is a separate moment**: a gathering pose in place, and a shake-and-release
      when it scatters (the `ChargeStarted` / `ChargeLaunched` / `ChargeBroken` events stand
      apart for this)
- [x] Maimed animation sets: a limp (while fleeing too), a one-handed stance
- [x] The death animation (a collapse — gore is meaningless with temporary art, it comes with
      the art); the body stays where it fell
- [x] The **surrender key** UI — **a single key, it pulls the whole party** (GDD §5); the key
      shows in advance how many warriors will be affected and how many are locked into a
      strike, and after it is pressed which ones are still waiting is readable on the panel
- [x] Watching the fight batch simulation reported exactly as it was with `-- --seed N`
      (it can be sped up with `--speed N`)
- [x] The presentation logic was separated from the engine (`Domina.Presentation`) and covered
      by **57 tests** — phase 2 is now verified like the other phases in the repo

> **Skeleton2D + Bone2D were not used in the rig.** A Bone2D skeleton is for mesh
> *deformation*; what we need is not to deform a limb but to **sever** it. In a plain
> `Node2D` hierarchy, severing is detaching a node from its chain — exactly what GDD §2
> describes, and with fewer parts. Skeleton2D can be added later if IK is needed.

> **The presentation logic is not inside Godot.** Who stands where, which event produces
> which reaction, what angle the bones take, what the key says — all of it lives in
> `Domina.Presentation`, independent of the engine. The rationale is the same as for the
> core: a decision that cannot be tested without opening the engine never gets tested.
> `src/Game` now only builds the nodes and applies the angles it receives.

> **2026-08-13:** **space** was added to the core (the arena is a plane, the warriors walk,
> there is weapon reach and encirclement). The fake position maths in the presentation layer
> was deleted; depth is shown on screen with brawler staging. Details: `docs/PROGRESS.md`.
> Art production **is affected**: a real walk cycle is needed, scripted moves are not.

### 2.2 Art and polish — ⬜ can start
- [x] The visual style decision — **made (2026-08-13):** dark Edo woodblock × layered paper
      theatre. The full rule is in GDD §12
- [x] A full-screen texture overlay (paper grain + ink bleed) and the day-cycle tint —
      **done (2026-09-17):** `PaperOverlay.cs` on two layers. The grain and the inked edge sit over
      everything; the hour's wash (`DayTint` in `Domina.Presentation`, capped at 0.15 and tested for
      it) sits over the yard and **under** every sheet, so no sheet the player reads is ever tinted by
      the time of day. It is one switch on the settings sheet, and the yard moved to layer −1 to make
      room for the wash above it. `CanvasModulate` was not used: it tints a whole canvas including the
      interface printed on it, which is the one thing the design forbids
- [ ] Hanging the real art assets on the bones
- [ ] Cut-surface assets: the shoulder stump, the hip stump, the severed limb's cut end
- [ ] The camera, the arena scene, hit effects, sound
- [ ] Death/finisher (gore) animations
- [ ] The visual counterpart of the armour tiers (4 tiers × 6 slots: head, torso, two arms,
      two legs — GDD §7)

> **The asset production rule:** assets are drawn flat and clean (no gradients, no baked
> light, a transparent ground, the same side projection, an overlap margin at the ends); the
> woodblock texture comes from the screen overlay. If light is baked into a part, it becomes
> wrong when the limb rotates and every part has to be lit separately — not affordable in
> one-person production.

**Acceptance:** given a seed, the fight can be watched from start to finish; the events and
what is seen on screen match exactly (a warrior who loses a limb is severed on screen too).
✅ *Verified for the backbone and now **tied down by tests**: `ArenaPlaybackTests` plays the
fight in the same order as the arena and checks that the limb loss in the books matches the
severing on screen — and the same limb at that. The comparison with the engine matches too:
seed 20260806 gives PlayerVictory / 15.2 s both in the arena and in the engine-free playback,
and seed 81 gives PlayerDefeat / 32.0 s in both.*

**Risk:** still the project's most expensive visual part, but **the 2D cutout decision brought
it from XL down to M** — the risk of 3D modular dismemberment is gone. With the backbone
finished, all the remaining risk is in 2.2: art production and the style decision.

**Precondition:** ~~the visual style decision~~ — **met (2026-08-13, GDD §12).**
Phase 2.2 is no longer waiting on anything.

---

## Phase 3 — The Dojo / Meta Layer · L

**Goal:** the game between the fights.

- [x] **The roster screen**: warriors, stats, wounds, honour, name editing
      — the decision lives in `Domina.Presentation/RosterModel.cs` (engine-free, tested): the
      badge, the ordering and the name-clash verdict are there;
      `Game/Scripts/RosterScreen.cs` only builds and prints the nodes. The order follows the
      question "whom can I send today": ready, training, in the infirmary, dead. A dead
      warrior does not drop off the list. The raw stat and the stat the fight reads are
      printed side by side (`40 → 46`), because training writes the raw one and the path and
      disabilities ride on top. The rename button is disabled in advance with
      `RosterModel.JudgeRename` — `Roster.Rename` throws on a clash, and the player should not
      learn that from an exception
- [x] **Name editing** (changing a name that came from chat or was generated — GDD §8)
      — `Roster.Rename`; name uniqueness is enforced only among the living
- [x] **Training grounds + training duration/effect** — `Dojo/Training.cs`: four drills cover
      the eight stats, the gain is a share of the gap left to the ceiling (diminishing returns
      inside the rule), `Warrior.Talent` multiplies the gain, a hungry warrior does not advance
      and what is written is the **raw** stat (the disability's multiplier stays on top). The
      rate is **locked at 0.04**: measured over 400 dojos × 60 days, a well-running dojo gets
      ahead of the band where the market ceiling bites within 60 days (GDD §11 "Training")
- [x] **The warrior skill tree (simple)** — `Model/WarriorPath`: three paths (Blade / Stone /
      Shadow), one choice, no going back; 20 training days unlock it and the multiplier enters
      `EffectiveStats` (the fight only reads the result). Measurement: deaths per warrior-fight
      6.3% → 6.0% (GDD §11)
- [x] **The school + instructor skill tree (deep)** — `Dojo/School.cs`: three branches × three
      tiers, the order within a branch compulsory, a facility paid up front and not sold back;
      the bonuses apply to **all** reads through `DojoState.Tuning` and `Economy`, and only the
      nodes bought go into the save. Measurement (400 dojos × 180 days): the training ground
      sells survival (death 9.95 → 6.36), the steward sells money (treasury 731 → 1718 but
      death 11.27), **the infirmary in this form is a trap** — an open item, GDD §11
- [x] **The infirmary/physician: recovery time, speeding it up with the medicine resource**
      — a day without medicine burns one infirmary day, a day with it two
      (`DojoState.AdvanceDay`); if the store falls short those in the infirmary eat first, and
      a hungry warrior does not heal that day
- [x] **Post-fight accounting** — `BattleAftermath`: death, limb loss, armour wear and
      breakage, infirmary days and honour are written to the roster here
- [x] **The economy: gold, food/water, medicine; buying and selling** — `EconomyTuning` +
      `Quartermaster` (price, repair, replacement, stock, hiring warriors, the expedition
      reward); the numbers were measured over 1000 dojos × 60 days with
      `Domina.Sim --mode campaign` and locked in GDD §11. **The long-horizon correction
      (2026-09-04):** the net figure was not counting replacements (18.5, not 31.3); the
      difficulty curve's ceiling went 3.0 → **2.2**, and a **risk premium** was added to the
      reward (0.25, past 100 health). Over 180 days the ending treasury went 75 → 2288, closed
      dojos 8.5% → 2.5%, and the early game does not move (GDD §11 "The long horizon")
- [x] **The economy: random events** — `Dojo/RandomEvents.cs`: 15% a day, five kinds (theft, provisions spoiling, the well going muddy, medicine going mouldy, illness); they all subtract, the effect hits the treasury and the calendar, measurement in GDD §11
- [x] The day loop — `DojoState.AdvanceDay()`: deterministic, containing no randomness;
      it burns infirmary days, pulls honour toward neutral and returns the closing day's summary
- [x] **The recruit flow — the warrior market** (`Dojo/RecruitMarket.cs`): candidates come with
      different stats, the stats are visible before the purchase, the price comes out of the
      stats, the market tracks the roster's level and refreshes every two days;
      `Warrior.Talent` is the second axis training reads. The name pool is still local — the
      chat connection is in phase 5
- [x] **The save system:** versioned, merge-on-load, try/catch (GDD §2)
      — `Domina.Core/Dojo/Save`; the save is a separate type family and the balance numbers do
      not go into the file (so an old save does not bring back the old balance)

**Acceptance:** a warrior can be hired and trained, wounded and healed, and when the game is
closed and reopened everything is in place.

> **Phase 3 closed (2026-09-04).** All the items are ticked.

> **Three orphan screens were written and the loop closed (2026-09-04).** The market, the
> school and the day's offer now exist as screens; all four (the roster included) are
> navigated over a single `DojoState` under `dojo.tscn`. The decisions live in the three
> models in `Domina.Presentation` (`MarketModel`, `SchoolModel`, `OfferModel`), and the
> commands go through the core's own doors (`DojoState.HireRecruit`, `BuySchoolNode`,
> `AcceptBounty`, `Decline`, `Expedition`). **When an expedition goes out the fight is watched
> in the arena:** `Expedition.Prepare` sets the fight up, the arena steps it, and
> `Expedition.Settle` closes the books — the accounting is in one place, and a watched fight
> leaves the same result as one resolved in batch simulation (`ExpeditionSettleTests`). The
> loop can be played by hand: the day opens, a warrior is bought at the market, an offer or a
> contract is chosen, the fight is watched, the roster melts, a facility is bought at the
> school, next day.

> **The save was wired into the game (2026-09-05).** The game opens with a title screen; the
> dojo is either loaded from the `user://dojo.json` slot or built with `NewGame.Create`, and it
> is written on every change. The second half of phase 3's acceptance criterion ("when the game
> is closed and reopened everything is in place") now holds on screen too — until that day it
> was only true in the core.

---

## Phase 4 — The Expedition and the Adversaries · L

**Goal:** expeditions that advance phase by phase, and human enemies.

- [x] **The daily encounter offer** — `Domina.Core/Campaign`: the offer is a pure function of
      the day and the seed (it is not stored in the save and cannot be changed by reloading),
      the threat band and the rough description are readable before going in, and the full
      roster is invisible
- [x] **The difficulty curve** — a single curve plus a daily fluctuation (`EncounterTuning`);
      the numbers are not locked, the measurement is in GDD §10
- [x] **An expedition eats a day** — `Expedition.Send` runs the fight, writes it to the roster,
      pays the reward and closes the day itself; `DojoState.Decline()` closes a day not entered
- [x] **Surrendering ends the expedition** (GDD §5, §10) — the reward for withdrawing is 0
      (`Quartermaster`), and Open Decision #9 had already fallen (in a one-fight expedition
      there is no previous room)
- [x] **Party selection: 1-4 warriors** — `EncounterOffer.Accepts`; a duel offer imposes exactly
      one warrior, and `Expedition.Refuse` declines an unfit party with its reason
- [ ] ~~The map/progress screen~~ — **dropped** (Open Decision #2 closed: there is no map screen)
- [x] **The throwing slot has no shop** — **closed 2026-09-13**, and it turned out to be the smaller
      half of the hole: `Quartermaster.Equip` / `Repair` / `Forge` were never called by a screen
      either, so nothing in the game could buy armour, mend it or reforge a blade. The counter is now
      its own screen (`Game/Scripts/ArmouryScreen.cs` over the tested `QuartermasterModel`), selling
      armour region by region, repairs, the forge and the throwing slot. The stall's price is locked
      at **1.4 gold per point of the quiver** (GDD §11); the measurement's surprise is that the slot
      itself, not the class, is what was missing — a roster that throws wins the last night 20.5% of
      the time against 17.3% for one that does not
- [x] **Adversary behaviour/AI profiles** — done 2026-09-13 (`Combat/TargetProfile.cs`). Not a combat
      pattern per kind but five multipliers over the shared target-selection weights, along the
      **discipline** axis the decision named: the collector gangs up, the cutthroat finishes the
      wounded, the duelist wants his own opponent, the kabukimono will not walk for an opportunity,
      the senior student is taught to pick. Measured against its own absence over six seeds × 1600
      dojos and it moves the season by nothing (last nights +0.07 ±0.84) — which is what a manner
      should cost; the lever is there if a kind must later become a threat
- [ ] ~~Boss encounters~~ — **not being built** (GDD §10: difficulty rises along a single curve)

### The adversaries (rewritten 2026-09-10 — Open Decision #16: the enemy is human)
| Kind | Role / character | First appears |
|---|---|---|
| **Collector** | A rival school's fee collector: small, quick, rarely alone | from the start |
| **Cutthroat** | A back-alley knife: fast, evasive, no armour worth the name | from the start |
| **Duelist** | A wandering swordsman on his own trial: hit-and-run | power 1.2 |
| **Kabukimono** | A street bravo with an absurdly heavy weapon: high damage, slow | power 1.5 |
| **Senior Student** | A rival school's senior: long-hafted, his reach tells in a crowd | power 1.8 |

The five carry the **same numbers** the yokai templates carried before the rename, so every
measurement taken before 2026-09-10 still stands. The old boss candidates are gone with the theme
and were never going to be built anyway (GDD §10 builds no boss structure); the story's named
adversaries — Kurogane's seniors and its head — sit at the top of the same single curve.

**Acceptance:** an expedition can be played from start to finish, and death/loss/reward are
reflected correctly in the dojo.

---

## Phase 5 — Chat Integration · L

**Goal:** Twitch and Kick connect and all the chat mechanics work.

### 5.1 The adapter layer (this first)
- [ ] The `IChatSource` interface — platform-independent
- [ ] Common internal events: `MessageReceived(user, text)`, `DonationReceived(user, amount)`
- [ ] **`FakeChatSource`** — a fake chat for tests and development (all the mechanics can be
      tested without a real connection; without it the chat features cannot be developed)
- [ ] Thread safety: chat arrives asynchronously and is passed to the game loop **through a queue**

### 5.2 The command engine
- [ ] The name pool: **everyone included by default**, `!no` → out, `!join` → priority
- [ ] A username filter (profanity/inappropriate)
- [ ] `!bushi` / `!ronin` → the live fight
- [ ] `!bushi-<name>` / `!ronin-<name>` → targeted, a small effect, **silent** if not found
- [ ] The seppuku vote: a 60 s window, one vote per user, a queue
- [ ] "Summon a Hero": a donation → a guaranteed roster entry, amount→level, **capped**

### 5.3 Twitch
- [ ] Chat: IRC-over-WebSocket (`wss://irc-ws.chat.twitch.tv`)
- [ ] **Spike:** **verify** that Cheers/Bits can be read from the PRIVMSG `bits` tag.
      If so, no separate OAuth/EventSub is needed for Bits — a large simplification.
      If not, the EventSub WebSocket + OAuth flow is required.
- [ ] Disconnection/reconnection, rate limits

### 5.4 Kick
- [ ] **Spike (priority):** verify how chat reading and **Kicks** (donation) events are received
      in Kick's current official API — the OAuth scopes, webhook or websocket, rate limits.
      → Kick's API is much newer than Twitch's; **there is uncertainty here, verify it early.**
- [ ] The adapter implementation
- [ ] Normalising Kicks into the same `DonationReceived` event

**Acceptance:** all the mechanics are tested with `FakeChatSource`; connected to a real Twitch
channel, `!join`, `!bushi`, the seppuku vote and the Bits flow work end to end.

**Risk:** Kick's API is the biggest unknown. Thanks to the adapter layer the game can ship with
Twitch even if Kick is late — **do not make Kick a release blocker.**

---

## Phase 6 — The AI Crowd · M

**Goal:** single-player mode, mechanically equivalent to streaming mode.

- [ ] Producing a bushi/ronin ratio from the fight's performance signals (GDD §9)
- [ ] A probabilistic, honour-weighted decision in the seppuku vote
- [ ] **The zero-vote fallback:** the AI decides even when there is real chat but no votes came in
- [ ] A Japanese name pool (it steps in when the `!join` pool is empty)

**Acceptance:** a full campaign can be played without chat; no system is "switched off".

---

## Phase 7 — Steam Integration · M

- [ ] **Spike:** choose the Steamworks route for Godot 4 + C#
      (GodotSteam GDExtension vs Steamworks.NET vs Facepunch.Steamworks) —
      verify the C# compatibility **before writing code**
- [ ] Steamworks initialisation, the Steam ID, the overlay
- [ ] Achievements (limb loss, seppuku, bosses, survival streaks)
- [ ] Cloud saves
- [ ] The content rating questionnaire (the gore/dismemberment declaration)
- [ ] The store page, capsule art, a trailer
- [ ] The build pipeline (Steam depot upload)

> **Note (the global CLAUDE.md rule):** store/marketing assets are **not committed to the public
> repo** — a local `docs/store-assets-originals/` (gitignored) + `pictures/<project>/` in the
> private `Eren-Ozcan/pictures` repo.

---

## Phase 8 — Polish · M

- [ ] Localisation infrastructure (TR/EN) — leave no hardcoded text
- [ ] Sound design, music
- [ ] Settings, key binding, accessibility (a gore filter included)
- [ ] Onboarding/tutorial — because it is an automatic combat game, "what am I controlling"
      has to be explained clearly
- [ ] The streamer mode settings screen: the channel name, switching commands on and off, the
      gore level

---

## Phase 9 — Balance and Release · L

- [ ] Balance passes with the batch simulation tool from phase 1
- [ ] Honour thresholds, the decay rate, the targeted-command coefficient (Open Decision #8)
- [ ] Griefing testing: simulate malicious chat scenarios
- [ ] A closed playtest → a streamer playtest (the chat mechanics can **only be tested in a real
      stream** — plan this early)
- [ ] Steam Next Fest / a demo
- [ ] The Early Access or full release decision
- [ ] **Repeating the name-clash check before release** (see the memory note)

---

## Critical Ordering Rules

1. **The phase 1 core before the visuals** — the other way round makes balance work impossible
2. **Deterministic RNG in phase 1** — it cannot be added later, it will have leaked everywhere
3. **`FakeChatSource` before the real APIs** — otherwise every test needs a live stream
4. **The Kick spike early** — the biggest technical unknown
5. **The visual style decision before phase 2** — the technical route (2D cutout) is settled, the style is not
6. **The core decision (#13) before everything (2026-09-07)** — will the engine-free core rule
   continue? If not, the "measure as each system arrives" setup cannot be applied and all of
   phase 1 is reshaped
7. **The move to real time before the new systems** — if staff, classes and morale are written
   on top of the discrete-day assumption, they get written twice

## The Dependency Chain

```
Phase 0 ─→ Phase 1 ─┬─→ Phase 2 (visuals)  ─┐
                    ├─→ Phase 3 (dojo)      ─┼─→ Phase 4 ─→ Phase 9
                    └─→ Phase 5 (chat) ─→ Phase 6┘
                                         Phase 7 ─┘
```
Phases 2, 3 and 5 are independent of each other — once phase 1 is done they can be worked in
any order.
