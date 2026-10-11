# Phase B's parity bed — the sim's measurement on the engine's ground, read 2026-10-02

Phase B's first slice (`../2026-10-phase-b-space/`) put the real fight's movement on Godot's physics server
and read one scenario. Phase B's acceptance (`docs/PLAN-ENGINE-RESOLUTION.md`, section 4) is wider: every
scenario of the pre-engine baseline (`../../baselines/2026-10-pre-engine/`, 34 duel scenarios under two
policies and the campaign beds over six seeds) re-read, every locked number inside its noise. This reading
runs that whole set inside the engine bed, with the sim itself asking the engine's fighter for its fights.

## Verdict

**The acceptance fails on the campaign, and the cause is not the engine but the collision rule it brings.**

- **Calibration holds.** The resolver over its own kinematics, run by the sim inside Godot's runtime, gives
  the baseline back line for line: **104 of 104 reports** identical but the wall clock.
- **Determinism holds.** The engine's ground run twice: **69 of 69 reports** identical (the duel bed and one
  campaign).
- **The duel bed holds — except where one side outnumbers the other.** 27 one-on-one scenarios are identical
  fight for fight (54 reports); the six 3v3 scenarios move by no more than |z| 1.0 on any line. **The ambush
  (1v3) is far outside:** the lone veteran dies in **28.12%** of fights instead of **33.23%** (z 11.1),
  escapes in 63.79% instead of 60.01%, wins 8.09% instead of 6.76%.
- **Every campaign figure is outside its noise, and all of them the same way: the season is harder.** All 36
  (six figures × two beds × three tiers) fall outside the recorded value's own band, at paired t of 2.2 to 36:

  | Bed | Tier | Last night won | Dojos closed | Deaths / warrior-fight | Net / fight | Bouts won |
  |---|---|---|---|---|---|---|
  | plain | apprentice | 36.37 → 32.27 | 18.12 → 19.77 | 2.80 → 3.18 | 60.48 → 57.13 | 2.92 → 2.68 |
  | plain | master | 23.98 → 20.53 | 25.88 → 28.18 | 3.97 → 4.33 | 48.07 → 44.53 | 2.21 → 1.99 |
  | plain | legend | 11.97 → 9.02 | 32.73 → 34.72 | 5.45 → 5.92 | 33.53 → 29.37 | 1.31 → 1.07 |
  | rich | apprentice | 40.52 → 31.82 | 16.82 → 21.13 | 2.70 → 3.47 | 52.13 → 45.62 | 3.06 → 2.63 |
  | rich | master | 29.08 → 20.33 | 24.02 → 27.63 | 3.60 → 4.53 | 41.90 → 34.73 | 2.44 → 1.90 |
  | rich | legend | 15.03 → 9.57 | 31.55 → 35.73 | 5.02 → 5.93 | 27.53 → 21.25 | 1.51 → 1.07 |

  Six-seed means, the baseline first. The full comparison, with each figure's paired difference, its band and
  its t, is `compare.txt`.

## The cause: the rule, shown without the engine

Two readings take Godot out of the question.

**The engine agrees with exact geometry.** A throwaway diagnostic (not kept) ran the ambush on the bed with
every step answered twice from the same state — once by the physics server, once by an exact
double-precision solver of the same rule. The two agree on **99.93%** of 1.39 million steps and part on the
rest by 3 units on average.

**The rule alone reproduces the engine's numbers.** The engine's rule was written engine-free — a man
out of anyone he already overlaps, then a swept circle that stops at the first body it meets, and the rest of
the step slid along that body once — what `GodotArenaSpace` asks of the server, in plain C#. The sim run over
it (`raw/slide/`) lands where the engine lands. (That copy was a throwaway project at the time of the
reading; once #25 closed it became the core's default ground, `SlideArenaSpace`, which gives the same reports
line for line.)

| | Kinematics (baseline) | The rule, engine-free | The engine |
|---|---|---|---|
| ambush, never — victory / deaths | 6.76 / 33.23 | 8.49 / 27.82 | 8.09 / 28.12 |
| ambush, losing:0.7 — victory / deaths | 0.54 / 6.26 | 0.81 / 4.66 | 0.83 / 4.58 |
| 3v3, never — victory / deaths | 66.73 / 50.24 | 66.67 / 50.19 | 66.65 / 50.18 |
| 3v3-armored, never — victory / deaths | 41.52 / 67.80 | 41.02 / 68.09 | 41.09 / 68.04 |
| rich master, six seeds — last night won | 29.08 | 20.20 | 20.33 |
| rich master, six seeds — deaths / warrior-fight | 3.60 | 4.53 | 4.53 |
| rich master, six seeds — net / fight | 41.90 | 34.65 | 34.73 |

On the rich master bed the engine and the engine-free rule differ by +0.13 points of last night (sd 0.22 over
the six seeds) — nothing. **The move into the engine costs nothing measurable; the change of rule costs the
season.**

**What the rule changes.** The old kinematics let a man walk the whole of his step and then pushed him back
out of whoever he had walked into, along the line between the two — so a man behind another kept nearly all
of his step and flowed round him. A body stops where it meets the other and keeps only the sideways part of
what is left: men no longer pass round each other, they queue. Two equal sides hardly notice it (3v3, every
variant). The reading the three beds support — it is a reading, not a measurement of its own — is that **the
larger side loses part of its numbers**: in the ambush the three ambushers get in each other's way and the
lone man escapes more and dies less; in the campaign it is the dojo that is the larger side at the decisive
moments — the last night's first bout puts two of Kurogane's seniors against the party, and the party
survives it **46%** of the time instead of **59%** (rich master, seed 1), the province's raids are won 83.0%
instead of 87.7%.

## Throughput

Taken alone on the machine (`raw/solo/`), Godot 4.7-stable editor binary, optimised project assemblies:

| What | Core resolver | Engine's ground |
|---|---|---|
| `--scenario 3v3 --battles 20000` | 3.04 / 3.12 s | **6.76 / 6.92 s** (62.7 motion tests a fight) |
| Rich bed, master, seed 1 (1600 dojos × 180 days, 136,431 fights) | 15.4 s | **76.5 s** (139.6 motion tests, 103.6 hand-overs a fight) |

The campaign's fights touch far more than the duel bed's 3v3 — 140 motion tests a fight, the ambush 307 —
so a campaign seed costs five times the core's. Still inside Phase A's budget of ~330 tests a fight; the whole
baseline set (68 duel reports, 36 campaign seeds) ran on the engine in about 12 minutes with twelve runs side
by side, against about 2 for the core.

## What was built

- **`SimCli.Run(…, IFightRunner? fighter)`** — the sim's entry takes the fighter its measurement asks for its
  fights, and hands it to the duel bed (`BatchRunner`) and the campaign (`CampaignOptions.Fighter`). The
  replay and the played season refuse a fighter other than the core's: they are not handed it. `Domina.Sim`
  shows its internals to `Domina.Game`, and the game project references it — for the bed. Tests: four in
  `SimCliTests` (both measurements ask the fighter they are handed, the counted campaign is the plain one,
  the two commands refuse).
- **`GodotFightRunner`** (`src/Game/Combat/`) — `Battle` over `GodotArenaSpace` behind the `IFightRunner`
  door. One space carries every fight; `GodotArenaSpace.Fit(tuning)` sizes the bodies and the arena to each
  fight's tuning between fights, while the bodies still stand in the space `Begin` throws away.
- **The bed's `--sim`** — `-- --fighter core|engine [--motion every|contact] [--report <file>] --sim <the
  sim's arguments>`: everything after `--sim` is the sim's command line, and the report is the sim's own,
  written where `--report` says.

## Re-running it

```bash
docs/engine-bed/2026-10-phase-b-parity/run-phase-b-parity.sh \
  tools/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64_console.exe <out-folder>   # PARALLEL=12
python docs/engine-bed/2026-10-phase-b-parity/compare.py docs/baselines/2026-10-pre-engine <out-folder>

# the rule without the engine: the core's default ground since #25 closed
dotnet run -c Release --project src/Domina.Sim -- --scenario ambush --battles 20000 --seed 1
```

The baseline was written in the console's code page and the bed writes UTF-8, so `compare.py` reads both
with the em dash and the times sign as the code page wrote them and the other marks dropped.

`raw/` keeps the engine's reports (`engine/`, with each run's closing lines in `bed.txt`), the engine-free
rule's (`slide/`) and the solo timings (`solo/`). The core's reports inside the bed are not kept: they are
the baseline's, line for line, and `compare.txt` says so.

## What it left, and the reading after it

Phase B could not pass as it stood: the engine's ground was a different rule, and the rule moved every
campaign number. The user's call, the same day, was Open Decision #25's (a) — **real collision stays**. The
rule became the core's default ground (`SlideArenaSpace`), the whole baseline was re-read on it
(`../../baselines/2026-10-slide/`), and the engine's reports above were read again against that folder —
its duel bed and its `campaign-untuned/`, the season before the re-tune that followed (`compare-slide.txt`;
the engine's code had not changed, so its reports stand):

- **Duel bed:** 816 share lines, none beyond |z| 1.5 (the largest the ambush's victory, 8.49% → 8.09%); the
  1v1 scenarios identical fight for fight as before.
- **Campaign:** 31 of 36 figures inside the recorded value's own band; the five outside sit at 1.1 to 1.7
  times it, none of them a last night and the largest 0.53 points of a share (plain apprentice, played the
  night 78.78% → 79.32%) — where the old rule had put all 36 outside, last nights by 3 to 9 points. Read as
  the difference of two readings each carrying its own noise (the band × √2) rather than against one, one
  figure of the 36 is outside (plain apprentice, dojos closed −0.47 of ±0.40) — the one in twenty that
  chance alone gives.

**Phase B's movement passes on the slide ground.** What is left of Phase B — projectiles and the weapon on
the ground through the space, reach as collision tests, the arena's edge as walls — reads against
`../../baselines/2026-10-slide/`. The campaign's own numbers are re-decided on the slide in the re-tune round
#25 asked for.
