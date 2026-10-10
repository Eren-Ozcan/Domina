# Baseline before the engine move — 2026-10-01

Every locked number this project has, read on the **engine-free resolver** (`Combat/Battle.cs`) on
the day Open Decision #24 closed, so that the fight moved into Godot
(`docs/PLAN-ENGINE-RESOLUTION.md`) has one written-down reading to be held against. The parity bed of
that plan's section 3.3 reads against **this folder**, not against figures scattered through
`docs/PROGRESS.md`.

## What was measured, on what

- **Rules:** the build of 2026-10-01 — `01105ee` plus the `IFightRunner` seam and
  `ChargeChanceAtMaxAggression` 0.50 (Open Decision #24). The difficulty table is the one tuned under
  the old ceiling and kept on purpose (the user's call, `docs/PROGRESS.md` 2026-10-01).
- **Duel bed:** every scenario `Domina.Sim --help` lists (34), 20,000 fights each (seeds 1..20000),
  under `--policy never` (the §11 protocol) and `--policy losing:0.7` (the build-order protocol).
- **Campaign beds:** 1600 dojos × 180 days, six seeds (`--seed` 1, 100001, 200001, 300001, 400001,
  500001), all three difficulty tiers, on two beds:
  - **plain** — the documented bed of 2026-09-12;
  - **rich** — the plain bed plus `--charms on --thrown-fit everyone --retire maimed --class-fit
    torite --train-classes on` (the bed of 2026-09-15 that #24 was measured on).
- **Raw reports:** `duel/<scenario>-<policy>.txt`, `campaign/<bed>-<tier>-s<seed>.txt` — the sim's
  own output, unedited. The tables below are generated from them by `summarise.py`.

## Re-running it

```bash
dotnet build src/Domina.Sim -c Release -o <dir>
docs/baselines/2026-10-pre-engine/run-baseline.sh <dir>/Domina.Sim.exe <out-folder>   # PARALLEL=12 by default
python docs/baselines/2026-10-pre-engine/summarise.py <out-folder>
```

Since Open Decision #25 closed (2026-10-02) the default ground is the slide; this folder reads back with
`EXTRA='--ground kinematic'` before the script (104 of 104 reports, checked that day), and the slide's own
reading is `../2026-10-slide/`.

The resolver is deterministic, so on this build every report reproduces line for line except its
wall clock. The whole set took 166 s with twelve runs in parallel on a 16-thread machine.

## Throughput (the number the engine bed has to answer)

Taken alone on the same machine, nothing else running:

| What | Wall clock |
|---|---|
| `ThroughputTests.TenThousandBattlesRunWithinTheBudget` (budget 10 s) | **2 s** |
| `--scenario 3v3 --battles 20000` | **4.23 s** (4,732 fights/s) |
| Rich bed, master, one seed (1600 dojos × 180 days) | **24.9 s** |

### Duel bed — 20,000 fights a scenario, seed 1..20000

| Scenario | Victory (never) | Deaths (never) | Charges/fight | Victory (losing:0.7) | Deaths (losing:0.7) | Escapes (losing:0.7) |
|---|---|---|---|---|---|---|
| 3v3 | 66.73 | 50.24 | 1.25 | 61.96 | 44.27 | 12.74 |
| 3v3-armored | 41.52 | 67.80 | 1.25 | 39.80 | 56.57 | 19.62 |
| 3v3-jitte | 70.22 | 46.38 | 1.25 | 66.36 | 41.34 | 11.23 |
| 3v3-poison | 62.86 | 53.67 | 1.25 | 58.20 | 48.04 | 12.41 |
| 3v3-torite-katana | 67.02 | 49.74 | 1.25 | 62.72 | 44.02 | 12.35 |
| ambush | 6.76 | 33.23 | 0.42 | 0.54 | 6.26 | 93.19 |
| blade | 93.00 | 2.31 | 0.44 | 93.00 | 2.31 | 4.70 |
| blade-armored | 80.81 | 7.50 | 0.44 | 80.81 | 7.50 | 11.69 |
| club | 92.01 | 2.93 | 0.44 | 92.01 | 2.93 | 5.07 |
| club-armored | 78.11 | 7.94 | 0.44 | 78.11 | 7.94 | 13.96 |
| duel | 61.19 | 36.96 | 0.41 | 61.19 | 36.96 | 1.85 |
| jitte | 82.92 | 6.18 | 0.44 | 82.92 | 6.18 | 10.90 |
| jitte-armored | 43.25 | 20.98 | 0.44 | 43.25 | 20.98 | 35.77 |
| jitte-heavy | 40.37 | 51.17 | 0.44 | 40.37 | 51.17 | 8.46 |
| jitte-unclassed | 72.95 | 11.18 | 0.44 | 72.95 | 11.18 | 15.87 |
| katana | 72.48 | 11.45 | 0.44 | 72.48 | 11.45 | 16.07 |
| katana-armored | 25.06 | 31.50 | 0.45 | 25.06 | 31.50 | 43.44 |
| katana-heavy | 24.30 | 68.16 | 0.44 | 24.30 | 68.16 | 7.54 |
| patrol | 98.43 | 6.28 | 1.26 | 96.15 | 5.98 | 2.95 |
| poison | 89.89 | 3.37 | 0.44 | 89.89 | 3.37 | 6.74 |
| poison-armored | 82.24 | 7.32 | 0.44 | 82.24 | 7.32 | 10.44 |
| poison-unclassed | 79.78 | 8.21 | 0.44 | 79.78 | 8.21 | 12.01 |
| sai | 84.13 | 6.04 | 0.44 | 84.13 | 6.04 | 9.83 |
| spear-armored | 45.05 | 21.09 | 0.44 | 45.05 | 21.09 | 33.86 |
| tanto | 26.31 | 38.57 | 0.44 | 26.31 | 38.57 | 35.12 |
| tanto-armored | 2.46 | 41.28 | 0.46 | 2.46 | 41.28 | 56.27 |
| thrown | 74.72 | 9.85 | 0.00 | 74.72 | 9.85 | 15.43 |
| thrown-kyudo | 75.78 | 9.30 | 0.00 | 75.78 | 9.30 | 14.91 |
| torite-katana | 73.61 | 11.09 | 0.44 | 73.61 | 11.09 | 15.30 |
| torite-katana-armored | 25.96 | 30.88 | 0.45 | 25.96 | 30.88 | 43.16 |
| torite-katana-heavy | 25.49 | 66.73 | 0.44 | 25.49 | 66.73 | 7.78 |
| veteran | 80.55 | 10.92 | 0.44 | 80.55 | 10.92 | 8.53 |
| yumi | 82.03 | 6.58 | 0.00 | 82.03 | 6.58 | 11.39 |
| yumi-unclassed | 77.02 | 8.74 | 0.00 | 77.02 | 8.74 | 14.24 |

### Campaign beds — 1600 dojos × 180 days, six seeds, mean (min-max)

| Bed | Tier | Last night won | Played the night | Dojos closed | Deaths / warrior-fight | Net / fight | Bouts won |
|---|---|---|---|---|---|---|---|
| plain | apprentice | 36.37% (34.7-38.2) | 80.85% (80.2-81.3) | 18.12% (17.8-18.5) | 2.80% (2.7-2.9) | 60.48 (59.6-61.5) | 2.92 (2.9-3.0) |
| plain | master | 23.98% (23.3-25.3) | 72.87% (71.4-75.7) | 25.88% (23.1-27.3) | 3.97% (3.8-4.1) | 48.07 (46.9-49.1) | 2.21 (2.2-2.2) |
| plain | legend | 11.97% (10.3-12.7) | 65.55% (64.5-67.3) | 32.73% (30.9-33.8) | 5.45% (5.4-5.6) | 33.53 (31.8-34.4) | 1.31 (1.2-1.4) |
| rich | apprentice | 40.52% (38.2-42.3) | 82.08% (81.6-82.9) | 16.82% (16.1-17.3) | 2.70% (2.6-2.8) | 52.13 (51.2-53.1) | 3.06 (3.0-3.2) |
| rich | master | 29.08% (27.1-30.4) | 74.77% (73.6-77.1) | 24.02% (21.5-25.1) | 3.60% (3.5-3.7) | 41.90 (41.2-42.8) | 2.44 (2.3-2.6) |
| rich | legend | 15.03% (13.9-15.8) | 66.98% (65.9-68.2) | 31.55% (30.6-32.7) | 5.02% (5.0-5.1) | 27.53 (26.8-28.2) | 1.51 (1.5-1.6) |
