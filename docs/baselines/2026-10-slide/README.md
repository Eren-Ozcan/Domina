# Baseline on the slide ground — 2026-10-02

Every locked number re-read on the day Open Decision #25 closed on (a): real collision stays. A body now
stops where it meets another and keeps only the sideways part of its step (`Combat/SlideArenaSpace.cs`),
where until then a man walked his whole step and was pushed back out (`KinematicArenaSpace`). The rule is
the same on the engine and off it, so this folder is what both play. It is the reference the rest of the
engine move (`docs/PLAN-ENGINE-RESOLUTION.md`) reads against.

The rule moved every campaign figure, and the season was re-tuned on it the same day:
`EncounterTuning.PowerPerDay` **0.011 → 0.0092** (the user's call). `campaign/` is the season on the locked
number — the current one; `campaign-untuned/` is the same beds read before the re-tune, at 0.011, which is
the rule's own price and what the engine's parity reading was held against
(`docs/engine-bed/2026-10-phase-b-parity/compare-slide.txt`). The duel bed does not read the curve and is
the same under both.

## What was measured, on what

- **Rules:** the build of 2026-10-02 — the pre-engine baseline's rules (`../2026-10-pre-engine/`) with the
  default ground changed from the push-out to the slide and, for `campaign/`, `PowerPerDay` 0.0092.
- **Beds and protocol:** exactly the pre-engine baseline's — every scenario at 20,000 fights under
  `--policy never` and `--policy losing:0.7`; the plain and the rich campaign beds, 1600 dojos × 180 days,
  six seeds, three tiers.
- **Raw reports:** `duel/<scenario>-<policy>.txt`, `campaign/<bed>-<tier>-s<seed>.txt` and the same names in
  `campaign-untuned/`.

## Re-running it

```bash
dotnet build src/Domina.Sim -c Release -o <dir>
docs/baselines/2026-10-pre-engine/run-baseline.sh <dir>/Domina.Sim.exe <out-folder>
python docs/baselines/2026-10-pre-engine/summarise.py <out-folder>
```

The script and the summary are the pre-engine baseline's own. On today's build it gives `duel/` and
`campaign/` back; `EXTRA='--power-per-day 0.011'` gives `campaign-untuned/`, and `EXTRA='--ground kinematic
--power-per-day 0.011'` the pre-engine folder (its reports were checked line for line, 104 of 104, before the
re-tune).

## Against the pre-engine baseline

The duel bed moves only where one side outnumbers the other: every 1v1 scenario reads the same fight for
fight and the 3v3 scenarios stay inside noise, while the ambush (1v3) moves from 6.76% victory / 33.23%
deaths to **8.49% / 27.82%**. Every campaign figure moves, the same way — the season is harder
(`campaign-untuned/`):

| Bed | Tier | Last night won | Dojos closed | Deaths / warrior-fight | Net / fight |
|---|---|---|---|---|---|
| plain | apprentice | 36.37 → 31.87 | 18.12 → 20.23 | 2.80 → 3.17 | 60.48 → 56.83 |
| plain | master | 23.98 → 20.45 | 25.88 → 27.57 | 3.97 → 4.35 | 48.07 → 44.38 |
| plain | legend | 11.97 → 8.93 | 32.73 → 35.02 | 5.45 → 5.93 | 33.53 → 29.23 |
| rich | apprentice | 40.52 → 31.68 | 16.82 → 20.93 | 2.70 → 3.45 | 52.13 → 45.65 |
| rich | master | 29.08 → 20.20 | 24.02 → 27.87 | 3.60 → 4.53 | 41.90 → 34.65 |
| rich | legend | 15.03 → 9.48 | 31.55 → 35.30 | 5.02 → 5.95 | 27.53 → 21.10 |

Why, and the engine's reading against this folder: `docs/engine-bed/2026-10-phase-b-parity/`.

## The re-tune

The precedent is 2026-09-13's: when a rule moved the season, `PowerPerDay` brought the death rate back. Swept
on the rich master bed against the season #24 left (29.08% of last nights, 3.60% deaths per warrior-fight),
`PowerPerDay` 0.0085-0.011 × the night's power 0.85-1.0, three seeds and then six: 0.0092 gives both back at
the night's own power, and was locked over adding ×0.97 on the night (`docs/PROGRESS.md` → 2026-10-02 has
the sweep). Six seeds, before #25 → the slide untuned → the slide re-tuned:

| Bed | Tier | Last night won | Dojos closed | Deaths / warrior-fight | Net / fight |
|---|---|---|---|---|---|
| plain | apprentice | 36.37 → 31.87 → **37.18** | 18.12 → 20.23 → **14.50** | 2.80 → 3.17 → **2.60** | 60.48 → 56.83 → **57.52** |
| plain | master | 23.98 → 20.45 → **27.53** | 25.88 → 27.57 → **21.45** | 3.97 → 4.35 → **3.47** | 48.07 → 44.38 → **48.12** |
| plain | legend | 11.97 → 8.93 → **16.13** | 32.73 → 35.02 → **29.00** | 5.45 → 5.93 → **4.77** | 33.53 → 29.23 → **35.97** |
| rich | apprentice | 40.52 → 31.68 → **37.12** | 16.82 → 20.93 → **14.83** | 2.70 → 3.45 → **2.82** | 52.13 → 45.65 → **46.13** |
| rich | master | 29.08 → 20.20 → **28.00** | 24.02 → 27.87 → **22.10** | 3.60 → 4.53 → **3.65** | 41.90 → 34.65 → **38.65** |
| rich | legend | 15.03 → 9.48 → **16.58** | 31.55 → 35.30 → **29.30** | 5.02 → 5.95 → **4.83** | 27.53 → 21.10 → **27.68** |

The rich master bed — the one #24 was measured on — is back on its death rate and inside a point of its
night. What the number does not give back is the rich bed's net per fight (a weaker enemy pays less, the
reward being per enemy health) and the tiers' spread (apprentice 1.33× master's night against 1.39×,
legend's floor higher); the plain bed, which the collision cost less, comes out easier than before.

## Throughput

Taken alone, one after another: `--scenario 3v3 --battles 20000` 3.38 / 3.49 s on the slide against 3.68 /
3.70 s on the push-out; a rich master seed 18.4 s against 21.6 s. The slide costs nothing.

### Duel bed — 20,000 fights a scenario, seed 1..20000

| Scenario | Victory (never) | Deaths (never) | Charges/fight | Victory (losing:0.7) | Deaths (losing:0.7) | Escapes (losing:0.7) |
|---|---|---|---|---|---|---|
| 3v3 | 66.67 | 50.19 | 1.25 | 61.92 | 44.20 | 12.80 |
| 3v3-armored | 41.02 | 68.09 | 1.25 | 39.34 | 56.77 | 19.85 |
| 3v3-jitte | 70.33 | 46.31 | 1.25 | 66.44 | 41.26 | 11.27 |
| 3v3-poison | 62.83 | 53.65 | 1.25 | 58.10 | 47.98 | 12.52 |
| 3v3-torite-katana | 67.19 | 49.66 | 1.25 | 62.81 | 43.96 | 12.40 |
| ambush | 8.49 | 27.82 | 0.42 | 0.81 | 4.66 | 94.53 |
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
| patrol | 98.39 | 6.29 | 1.26 | 96.12 | 5.96 | 2.99 |
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
| plain | apprentice | 37.18% (35.5-38.8) | 84.48% (83.3-85.3) | 14.50% (13.2-15.8) | 2.60% (2.5-2.7) | 57.52 (56.8-58.3) | 2.95 (2.9-3.0) |
| plain | master | 27.53% (26.3-28.7) | 77.12% (75.4-78.8) | 21.45% (19.6-23.2) | 3.47% (3.4-3.6) | 48.12 (46.8-49.0) | 2.44 (2.4-2.6) |
| plain | legend | 16.13% (14.4-17.0) | 69.28% (67.4-70.6) | 29.00% (27.2-31.1) | 4.77% (4.7-5.0) | 35.97 (34.2-36.7) | 1.66 (1.5-1.7) |
| rich | apprentice | 37.12% (36.1-37.8) | 83.95% (82.9-85.2) | 14.83% (13.5-15.8) | 2.82% (2.8-2.9) | 46.13 (45.8-46.3) | 2.95 (2.9-3.0) |
| rich | master | 28.00% (25.7-29.3) | 76.83% (76.1-77.9) | 22.10% (21.1-22.9) | 3.65% (3.6-3.7) | 38.65 (37.5-39.5) | 2.42 (2.3-2.5) |
| rich | legend | 16.58% (14.9-18.1) | 68.97% (67.2-70.5) | 29.30% (27.9-30.6) | 4.83% (4.7-5.0) | 27.68 (26.3-29.1) | 1.67 (1.6-1.8) |
