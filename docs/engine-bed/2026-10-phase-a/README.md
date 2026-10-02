# Phase A — the engine bed's gate, read 2026-10-02

Phase A of `docs/PLAN-ENGINE-RESOLUTION.md`: before any of `Combat/Battle.cs` moves, can a headless
Godot step fights **fast enough to sweep** and **the same way twice**? The plan's acceptance, as
section 0 amended it: 10,000 fights in 10 s (the figure `ThroughputTests` pins on the core), and the
same seed giving the same winner, the same elapsed time and the same event stream over 10,000 fights,
run twice on the same machine and build. The plan asks for the harness to be measured against a
**trivial fight** first; this folder is that measurement.

## Verdict

**The gate passes on the stand-in fight, with little room.**

- **Throughput:** 10,000 fights in **7.7 / 8.3 / 9.3 s** (three runs, median 8.3 s) against the 10 s
  budget — single process, the editor binary, on a machine that was not idle (below).
- **Determinism:** two runs, **10,000 of 10,000 fights identical**, fingerprint by fingerprint. And a
  stronger result than the plan asked for: the same 10,000 fights stepped by the **engine's own
  physics loop** (the frame lane, 256 fights side by side) are **identical to the bed's own loop**,
  fight for fight. The fight does not depend on who steps it.

The room is the caveat. The stand-in makes 297 motion tests a fight; the real 3v3 fight moves on
**285** fighter-ticks a fight (counted on the core, below). So the port is projected to land at about
**9 s** — at the line, not under it with space. Phase B therefore starts with an engine-call budget
(rule 6), and the levers that would buy room are listed with what each was measured to give.

## What the bed is

`src/Game/Bed/` — a scene in the game's own Godot project, run headless:

```bash
dotnet build src/Game/Domina.Game.csproj -c Debug -p:Optimize=true   # the editor binary loads Debug
tools/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64_console.exe --headless \
  --path src/Game res://Bed/engine_bed.tscn -- --fights 10000 --digest gate.txt
```

- **`TrivialFight`** — three against three, sized like `ThroughputTests`' 3v3 (six men, about 15 s
  a fight), built from the engine pieces the real fight would use: bodies on the physics server,
  movement by motion test (`MoveAndCollide`), the blade as a collision test, the blow on a strike
  clip's **method key**, the core's `SeededRandom` for the rolls, the core's tick
  (`CombatTuning.TickSeconds`, 0.05 s).
- **Two lanes.** `--lane manual` steps one fight at a time in the bed's own loop, all inside one
  engine frame — the unwatched fight. `--lane frame` lets the engine's fixed physics step drive the
  tick, one tick a physics frame, many fights side by side, each in its own `SubViewport` and so its
  own physics space — the watched fight's shape, run flat out with Godot's `--fixed-fps 20`.
- **`--fighter core`** runs the engine-free resolver through `CoreFightRunner` inside Godot's
  runtime — the calibration that says this runtime is as fast as the one the core's figures came from.
- **`--digest <file>`** writes every fight's fingerprint (FNV-1a over every blow, the winner, the
  tick count and every man's final health and position bits); two runs agree only if the files do.
- **`--profile`** splits the time by engine piece. The other switches are the variants below.

`run-phase-a.sh` takes the whole reading; `raw/` is its output, unedited.

```bash
docs/engine-bed/2026-10-phase-a/run-phase-a.sh \
  tools/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64_console.exe <out-folder>
```

## The reading

Godot 4.7-stable (the editor binary, headless), .NET 10.0.10, i7-11800H (8 cores / 16 threads).
Domina.Game and Domina.Core optimised; the GodotSharp the editor binary loads is its **Debug** build
(`GodotSharp/Api/Debug`). **The machine was not idle:** other processes held 40-50% of the CPU
throughout, and the baseline's figures were taken with nothing else running — the core calibration
read 1.77-1.99 s here against 1.59-1.69 s earlier the same day. Expect every figure below to come down
on a quiet machine; they are recorded as read.

| Run (`raw/`) | What changes | 10,000 fights | Fingerprints |
|---|---|---|---|
| `core-1..3` | the core resolver inside Godot (calibration) | 1.91 / 1.77 / 1.99 s | identical ×3 |
| `gate-1..3` | **the gate configuration** | **9.35 / 8.33 / 7.67 s** | 1 vs 2: **10000/10000** |
| `gate-profile` | the same, timed by piece | 7.92 s | identical |
| `frame-256` | the engine's physics loop steps it, 256 fights at once | 13.86 s | **10000/10000 vs manual** |
| `blade-query` | blade as a space query (`IntersectShape`) | 15.14 s | identical |
| `clip-mixer` | `AnimationPlayer` advanced every tick, its key calls the blow | 24.68 s | identical |
| `rebuild` | the fight's nodes built afresh for every fight | 25.86 s | identical |
| `character-manual` / `-frame` | kinematic bodies (`CharacterBody2D`), 1,000 fights | 1.04 / 4.04 s | **332/1000** between lanes |

The gate configuration's time, by piece (`gate-profile`):

| Piece | Calls | Per call | Share |
|---|---|---|---|
| move — `MoveAndCollide` + transform to the server | 2,970,583 | **2.43 µs** | 91.1% |
| blade — `Shape2D.Collide` | 627,429 | 0.21 µs | 1.7% |
| reset — every man back on his mark | 10,000 | 14.4 µs | 1.8% |
| everything else (the fight's own C#) | — | — | ~5% |

Read by hand the same session, not by the script:

- **Release GodotSharp** (a scratch copy of the editor with `Api/Release` copied over `Api/Debug`):
  6.98 / 7.42 / 7.33 s, fingerprints identical to the Debug API's.
- **No transform push** (rule 1's push commented out): the static body then behaves exactly like the
  kinematic one — 332/1000 identical between lanes, and the same fingerprints as the kinematic runs.
- **A C# pre-check before the motion test** (skip the test when no body is within reach of the step):
  7% faster, and **not** fingerprint-identical: placing the body at `from + motion` lands it a
  float-rounding away from where the motion test puts it (presumably in how the test composes its
  travel — not traced). Dropped.

## What the core's real fight costs in motion tests

Counted on the engine-free resolver, `DemoRoster` 3v3 (the calibration's roster), 2,000 fights:
389 ticks a fight, 1,731 fighter-ticks on the field, **285 fighter-ticks on which a man's position
changed**. The stand-in's 297 motion tests a fight are the same order — which is why its figure is a
fair forecast for the port: 285 × 2.43 µs ≈ 0.69 ms of motion tests, plus ~0.18 ms of the core's own
logic, ≈ 0.9 ms a fight, **≈ 9 s for 10,000**. Scenarios with thrown weapons add projectile flight
on top.

## The rules Phase B inherits

Each is a measured finding, not a preference.

> **Refined 2026-10-02** by Phase B's first slice (`../2026-10-phase-b-space/`): the bodies live on the
> server with no nodes (rule 1), and every fight gets a fresh, inactive physics space while the bodies
> are pooled (rule 5) — a space carried from fight to fight remembers the earlier fights in its
> broadphase. The stand-in here pooled its space; its lane identity held, but whether its seeds were
> pure was not tested. The real fight's forecast of rule 6 was beaten: 3.8-3.9 s, not ~9 s.

1. **Bodies are static on the server, and every move is pushed to it by hand.** Godot hands a moved
   node's transform to the physics server through a notification the scene tree flushes once a frame;
   a kinematic body (`CharacterBody2D`, `AnimatableBody2D`) waits for the next physics step besides.
   Either way a fight run inside one frame queries bodies where they stood when the frame began. A
   `StaticBody2D` moved and then pushed (`PhysicsServer2D.BodySetState`) is in the broadphase at
   once, and that is what makes the two lanes agree 10,000 of 10,000. Without the push, or with
   kinematic bodies, 332 of 1,000.
2. **The bed steps fights itself; the engine's loop is for the watched fight.** The frame lane gives
   the same fight bit for bit, so a watched fight and its replay on the bed are one fight — but it is
   no throughput lane (13.9 s here, 29-32 s in earlier runs of the same session; the engine's own
   per-frame work and 256 interleaved fights both cost).
3. **The blow's frame is read off the clip, not by running the clip.** Advancing an `AnimationPlayer`
   every tick costs 1.0 µs a fighter-tick — 12.4 s on 10,000 fights. Reading the method key's time off
   the same clip once and counting ticks gives the identical fight. The mixer runs only when someone
   watches. The key must call with `CallbackModeMethod = Immediate` (a deferred call lands after the
   frame, i.e. after the whole manual lane), and it is placed between two ticks (0.375 s) so the
   comparison has no boundary to round across — a key on a boundary was not tried.
4. **No call that returns a Godot collection in the hot path.** `IntersectShape` costs 8.6 µs a call
   (an `Array` of `Dictionary`), `Shape2D.Collide` 0.21 µs, and for "does my blade reach my target"
   they answer the same. A space query belongs where *anyone* in the arc matters, and then rarely.
5. **Fights are pooled and reset, never instantiated per fight.** Building six bodies, shapes, clips
   and players costs ~1.7 ms a fight — more than the whole fight. A reset is 14 µs.
6. **The budget is about 330 motion tests a fight.** 10 s for 10,000 fights is 1 ms a fight; the
   core's logic takes ~0.18 ms of it; at 2.4 µs a motion test the rest is ~330 tests. The real fight
   needs ~285. Phase B counts its engine calls per fight on the bed as it goes.

## If Phase B goes over

What would buy room, and what each was measured to give:

- **The Release GodotSharp** — ~7.3 s against ~8.3 s for the stand-in, same fingerprints. An
  exported build ships the Release one; the editor binary loads the Debug one.
- **An exported release build** — the native side without the editor's checks. Not measured: it needs
  the export templates, which are not in `tools/`.
- **Parallel processes for sweeps** — the baseline already runs twelve at once; the 10 s gate is the
  single-process inner loop, and sweeps never ran single-process.

What does not: a C# pre-check before the motion test (7%, and it changes the fight at the float
level); a frame lane (slower than the manual one).

## Not measured here

- The real resolver in the engine — that is Phase B and C.
- Two machines — not required since section 0 narrowed determinism to one machine and one build.
- Projectiles and the weapon on the ground as bodies — the stand-in has neither.
