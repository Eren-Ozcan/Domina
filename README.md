# Domina *(working title)*

A samurai-themed **dojo management + fully automatic combat** game.
Godot 4 · Steam · Twitch/Kick chat integration.

You are the sensei of a dojo. You train warriors and send them on expeditions that
advance phase by phase. You cannot intervene in a fight — the single exception is
the decision to **pull** a warrior out of the arena. Death is permanent; those who
come back from the brink can lose limbs and keep fighting maimed.

If you are streaming, chat decides your warriors' names, judges fights as
**Bushi/Ronin** (which affects the bounty economy), and gets a say in the
**seppuku** vote for a warrior who falls dishonourably. If you are not streaming,
an AI crowd runs the same systems — single-player mode is mechanically equivalent.

## Documents

| File | Content |
|---|---|
| [`docs/GDD.md`](docs/GDD.md) | Design decisions + open decisions table |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Phase 0-9 development plan |
| [`docs/PROGRESS.md`](docs/PROGRESS.md) | Snapshot of where things stand |
| [`CLAUDE.md`](CLAUDE.md) | Repo rules, architecture constraint, store-asset policy |

## Status

**Phase 2 — the visualisation backbone is done.** Given a seed, a fight can be
watched on screen from start to finish: limbs come off, warriors get pulled out,
the surrender key works. The art is **deliberately temporary** (stickman) — no real
art is started before the visual style is decided.

## Development

The Godot executable is not kept in the repo; it is downloaded into `tools/`
(`tools/` is gitignored).

```
src/     simulation core, presentation logic, chat adapters, batch simulation, Godot project
tests/   tests (run without opening the engine)
docs/    design and plan
tools/   Godot binary (gitignored)
```

The only project that depends on Godot is `src/Game`. Decisions about how a fight
looks on screen (who stands where, which event produces which reaction, what a key
says) live engine-free in `src/Domina.Presentation` and their tests run without
opening the engine; `src/Game` only applies the result to nodes.

### Batch simulation

The balance-work tool: it runs tens of thousands of fights without opening Godot and
reports death, maiming and win rates.

```bash
dotnet run --project src/Domina.Sim -c Release -- --help
dotnet run --project src/Domina.Sim -c Release -- --scenario 3v3 --battles 10000 --policy below:0.3 --out result.csv
```

`--policy` stands in for the player's "pull" key; the difference between `never` and
`below:0.3` is the only way to measure the balance of the limb-loss mechanic (limb
loss only happens in fights where you intervene in time).

### Running the game

```bash
dotnet build src/Game/Domina.Game.csproj
tools/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64.exe --path src/Game -- --seed 81
```

`--speed 4` speeds the fight up (useful while looking at balance), and
`--headless --quit-after 900` gives the result on a single line. Without `--seed` the
default fight opens. When batch simulation reports an interesting fight ("the warrior
loses an arm on seed 52"), that exact fight can be watched here — this is what
determinism buys in practice.
