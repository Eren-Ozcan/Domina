# The played seasons

Every season a person or an agent has played by hand through `Domina.Sim --play`, kept as the file
that produced it. The harness is stateless — it rebuilds the dojo from the seed and replays the
script from line 1 — so **the script plus the seed is the save**, and a season here can be re-run
verbatim after a balance change to ask what the same player, making the same decisions, would have
got on the new numbers.

To replay one, from the repo root:

```
dotnet run --project src/Domina.Sim -- --play docs/played-seasons/<round>/<file> --seed <seed>
```

The reports these seasons produced are in `PROGRESS.md`, one entry per round.

## The pull-out order

A move can carry `pull:0.5`, and it means one exact thing: **pull out when we are losing** — the party
is outnumbered on the field *and* under that share of health. Both conditions, not either. It is the
core's `RetreatWhenLosing`, the batch bed's stand-in for a player, and it is deliberately not a plain
health threshold: a player does not call off an expedition over one wound, and an order watching
health alone abandons more than 80% of 3v3 fights.

The consequence is worth writing down, because five played seasons reported the order as broken when
it was not: **send four men at one enemy and the order cannot fire**, because you are never
outnumbered until three of them are down. A lone man can never trip it at all. If the fight has to be
breakable off, that is what the party size is for — or the fight is one to fight to the end.

This is the harness only. The game's own key is a key: it is pressed, and the party leaves.

## Round 1 — 2026-09-14, three seasons

| File | Seed | Play |
|---|---|---|
| `round-1-4xxx/novice.txt` | 4303 | first-time player |
| `round-1-4xxx/blade.txt` | 4101 | blade-first |
| `round-1-4xxx/steward.txt` | 4202 | steward-first |

⚠️ These predate the 2026-09-17 roster change. A warrior's number is now his for the season; it used
to be his place in an alphabetical list and was handed out again on every hire and death. A script
from this round that contains `drill` / `class` / `path` / `fit` / `retire` / `release` therefore no
longer points at the same men, and does **not** replay to the season its player saw.

## Round 2 — 2026-09-15, four seasons at Master

| File | Seed | Play | Outcome |
|---|---|---|---|
| `round-2-5xxx/s5101.txt` | 5101 | balanced | closed day 118 |
| `round-2-5xxx/s5202.txt` | 5202 | aggressive | closed day 50 |
| `round-2-5xxx/s5303.txt` | 5303 | school-first | closed day 75 |
| `round-2-5xxx/s5404.txt` | 5404 | cautious | reached day 180, gate shut |

Same warning as round 1: written before the numbering change.

## Round 3 — 2026-09-17, three seasons at Master

| File | Seed | Play | Outcome |
|---|---|---|---|
| `round-3-6xxx/hawk.txt` | 6101 | aggressive | closed day 96 |
| `round-3-6xxx/mason.txt` | 6202 | school-first | closed day 156 |
| `round-3-6xxx/novice.txt` | 6303 | first-time player | closed day 129 |

Verified to replay to those days on 2026-09-18.

## Round 4 — 2026-09-17, six seasons

The first round in which the last night was reached at all: 2 of 6.

| File | Seed | Outcome |
|---|---|---|
| `round-4-7xxx/s7101.txt` | 7101 | closed day 154, 0 men |
| `round-4-7xxx/s7202.txt` | 7202 | closed day 138, 0 men |
| `round-4-7xxx/s7303.txt` | 7303 | reached the last night, lost bout 1 |
| `round-4-7xxx/s7404.txt` | 7404 | reached the last night, lost bout 2 |
| `round-4-7xxx/s7505.txt` | 7505 | closed day 154 |
| `round-4-7xxx/s7606.txt` | 7606 | day 180 alive, never opened the night |

7101 and 7202 contain warrior-indexed moves written before the numbering change and end 69 and 39
days earlier on a replay; the other four land where their players left them.

## Round 5 — 2026-09-17, six seasons

4 of 6 reached the last night and all six lost it; nobody reached bout 3.

| File | Seed | Outcome |
|---|---|---|
| `round-5-8xxx/s8101.txt` | 8101 | day 181 Fallen, won bout 1, lost bout 2 |
| `round-5-8xxx/s8202.txt` | 8202 | day 181 Fallen, won bout 1, lost bout 2 |
| `round-5-8xxx/s8303.txt` | 8303 | dead day 110 |
| `round-5-8xxx/s8404.txt` | 8404 | day 181 Fallen, lost bout 1 |
| `round-5-8xxx/s8505.txt` | 8505 | day 181 Fallen, lost bout 1 |
| `round-5-8xxx/s8606.txt` | 8606 | dead day 56 |

Two notes on this round. Lines 143-145 of `s8202.txt` (`hire 8`, `drill 12 Guard`, `day`) were
written by another player's helper script over a shared path and are not that player's decisions; he
was told and left them in. And the harness was fixed while the round was still running, so the
screen these six read was not one screen — later rounds are frozen for their duration.
