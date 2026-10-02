# Phase B, first slice — the real fight's steps on Godot's physics server, read 2026-10-02

Phase A (`../2026-10-phase-a/`) passed on a stand-in fight with a forecast: the real 3v3 would land at
about 9 s for 10,000 fights, at the gate's line. This slice puts the forecast to the test with the real
resolver instead of a stand-in. Phase B's first piece — "positions, distance, reach … become nodes and
queries; the decision logic still runs on the core's own tick" (`docs/PLAN-ENGINE-RESOLUTION.md`) —
starts with movement: where a man ends up when he means to go somewhere.

## Verdict

**The real fight runs on Godot's physics server at 3.8-3.9 s for 10,000 fights, its fingerprints are
a function of the seed alone, and its numbers sit inside the noise of the core's.**

- **Throughput:** 3.85 / 3.78 / 3.90 s for 10,000 fights of the arena's demo 3v3 (the core resolver:
  1.6-1.7 s on the same runs). The forecast was ~9 s; the room under the 10 s gate is now 2.5×.
- **Determinism:** two runs identical 10,000/10,000; the engine's own physics loop (256 fights side by
  side) identical to the bed's loop 10,000/10,000; seeds 5001-10000 run alone identical to the same
  seeds inside the full sweep 5,000/5,000.
- **First parity reading** (20,000 fights, seeds 1-20000): victory 68.69% → 68.62%, withdrawal
  9.36% → 9.57%, wipe 21.95% → 21.80%, average fight 19.61 → 19.63 s, hit rate 61.6% → 61.6%. The
  standard error of a 68% share over 20,000 fights is ±0.33 points.

It is one scenario. The parity bed proper — every scenario in the baseline, every locked number inside
its noise — needs the sim's scenarios on the bed, which is the next slice's work.

## What was built

- **`IArenaSpace`** (`src/Domina.Core/Combat/`) — the door. `Battle.Move` decides where each man
  *means* to go; the space says where he ends up. `Begin(bodies)`, `Enter(body, at)`,
  `Settle(body, intended, passing)`, `Leave(body)`. The core stays engine-free.
- **`KinematicArenaSpace`** — the resolver's own `Separate` and `Clamp`, moved out of `Battle` without
  a change. **Proven to change nothing:** the whole pre-engine baseline (`../../baselines/2026-10-pre-engine/`,
  68 duel and 36 campaign reports) re-run on the seam build is identical line for line except the wall
  clock, and the suite is green (884 tests, 3 of them new in `ArenaSpaceTests`).
- **`GodotArenaSpace`** (`src/Game/Combat/`) — the engine's side: one static body per man on the
  physics server, a circle `PersonalSpace` across; a step that can touch someone is a motion test
  (`PhysicsServer2D.BodyTestMotion`), and the rest of the step slides along the body it met.
- **The bed** grew a third fighter: `--fighter engine` is `Battle` over `GodotArenaSpace`, `--fighter
  core` is `Battle` over its own kinematics, and both run in both lanes (`IBedFight`).

```bash
docs/engine-bed/2026-10-phase-b-space/run-phase-b-space.sh \
  tools/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64_console.exe <out-folder>
```

## The reading (`raw/`)

Godot 4.7-stable (editor binary, headless; Debug GodotSharp), .NET 10.0.10, i7-11800H. Other processes
held 15-23% of the CPU — quieter than Phase A's reading (40-50%).

| Run | 10,000 fights | Fingerprints |
|---|---|---|
| `core-1..3` — the resolver's own kinematics | 1.69 / 1.60 / 1.68 s | identical ×3 |
| `engine-1..3` — on the physics server | **3.85 / 3.78 / 3.90 s** | 1 vs 2: **10000/10000** |
| `engine-frame-256` — the engine's loop steps it | 8.54 s | **10000/10000 vs manual** |
| `engine-second-half` — seeds 5001-10000 alone | (5,000 fights) | **5000/5000 vs the sweep** |
| `engine-every` — a motion test for every step | 8.88 s | **10000/10000 vs contact** |
| `engine-profile` | 4.56 s | identical |

The engine fight's time by piece (`engine-profile`): the space's settle path 2.69 s, 59% —
3,228,635 settles at 0.83 µs on average, **76.4 motion tests and 74.2 hand-overs to the server a fight**.
Everything else is the resolver's own C#, which the core runs at ~0.17 ms a fight.

## How it got from 18 s to 3.9 s

The first engine space ran the same 10,000 fights in **18.3 s** (14.4-15.1 s on quieter re-runs). Each
step below was measured on the bed the same session; the code of the dropped ones is gone, the readings
are kept here.

| Change | 10,000 fights | Per settle | Fingerprints |
|---|---|---|---|
| Nodes walk: `MoveAndCollide` on a `StaticBody2D`, place pushed every step | 14.4-18.3 s | 3.57-3.70 µs | — |
| The server body walks: `BodyTestMotion` with parameters kept from call to call | 9.0-9.6 s | 2.07-2.23 µs | differ from nodes (cause not traced) |
| …and a step that cannot touch anyone is not tested | 7.0-7.6 s | 1.54-1.63 µs | identical to every-step |
| …and a place is handed to the server only before a motion test | 5.1-6.1 s | ~1.2 µs | *differ* — see below |
| …and every fight gets a fresh, inactive space | **3.8-3.9 s** | 0.83 µs | identical across runs, lanes and sweeps |

Two diagnostics behind the table:

- **The hand-over is the dearest call.** With the server push commented out (wrong, only to time it),
  a settle fell to 0.19 µs: about 1.4 µs of every settle was `BodySetState`, more than the motion test
  itself. Disposing the `Transform2D` Variant at once (it is heap-backed, with a finaliser) took 7.6 s to
  7.0 s; handing a place over only when a test is about to read it did the rest.
- **The server remembers the order it was told things in.** Its broadphase is a tree built move by
  move, and when a step touches two bodies at once the tree's shape decides which contact the test
  reports. With one space carried from fight to fight, the manual and frame lanes agreed on 9,996 of
  10,000 fights; with the engine kept from stepping that space, 9,998. The rest was the space's memory
  of earlier fights: a seed's fight depended, now and then, on which fights the same space had carried
  before it. A fresh space for every fight, created inactive so the engine never steps it, made the
  fight a function of its seed alone (the three identities above). It also made the contact pre-check
  fingerprint-identical to testing every step, which it was not under the lazy hand-over in a carried
  space.

## Rules this slice adds to Phase A's

1. **The bodies live on the server, with no nodes.** A node moved for the picture hands its place to the
   server at the frame's flush, behind the fight's back, and changes the broadphase's history. The
   picture is drawn on other nodes from what the space answers. (Refines Phase A rule 1.)
2. **Every fight gets a fresh physics space, created inactive and never stepped by the engine; the
   bodies are pooled.** That is what makes a seed's fight the same in a sweep, alone, or one tick a
   physics frame. (Refines Phase A rule 5: pool the bodies, renew the space.)
3. **A place reaches the server only when a motion test is about to read it.** The hand-over costs more
   than the test.
4. **A step that cannot touch anyone is not tested** — no standing body within the step's length and two
   radii. In a fresh space it is fingerprint-identical to testing every step, and it cuts the tests from
   343 to 76 a fight.
5. **Variants built for the server are disposed at once.** A `Transform2D` Variant is heap-backed and
   finalised otherwise.

## What is left of Phase B

- **The parity bed proper** — the sim's 34 scenarios on the bed under `--fighter core|engine`, every
  locked number inside its noise. The scenarios live in `Domina.Sim`; bringing the sim into the bed is the
  plan's Phase E work and Phase B's acceptance needs it first.
- **Projectiles and the weapon on the ground** through the space.
- **Reach** as collision tests rather than distances (Phase A rule 4: `Shape2D.Collide`, not a query).
- **The arena's edge** as walls rather than a clamp — with the arena scene (Phase F).
- **The game itself**: the watched arena and the unwatched fights still run the resolver's own
  kinematics; they move over when parity holds.
