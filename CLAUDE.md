# Domina (working title — the final name is not decided yet)

A samurai-themed dojo management + fully automatic combat game. Twitch/Kick chat
integration, aimed at Steam, Godot 4.

> ⚠️ This project is **unrelated to the finished trivia game Domina** that previously
> lived in the `C:\Projects\Domina` folder. "Domina" here is only the working folder name.

## Language rule

Everything written in this repo is in **English** — file names, code, comments, docs,
commit messages **and the game's own UI**. There is no exception: the screen texts, the
day log, the item names, the bounty epithets and the patron types are all English too.
The repo is public and its GitHub description is English, and the game is aimed at Steam.

Localisation (a TR/EN layer) is phase 8 work in `docs/ROADMAP.md`; until then the strings
are hardcoded English, and a new string is written in English.

## Design and plan — the single source of truth

Read before starting any work on this project:

- **`docs/GDD.md`** — locked design decisions. The "Open Decisions" table at the end
  shows what is still undecided.
- **`docs/ROADMAP.md`** — phase 0-9 development plan, acceptance criteria, risks.
- **`docs/REFERENCE-DOMINA.md`** — a system-by-system breakdown of the game this one is
  inspired by (Domina, Dolphin Barn); every item is marked by how trustworthy its source
  is. Look here first before borrowing a mechanic.
- **`docs/REFERENCE-DOMINA-UI.md`** — the screen and interface breakdown of the same
  game: which information sits where, what each control looks like, what not to copy.
- **`docs/COMPARISON-DOMINA.md`** — an item-by-item comparison with the reference game:
  what we did the same, what we changed on purpose, what we never did (⚪ marks an open gap).
- **`docs/DESIGN-REFERENCES.md`** — the externally verifiable grounds for the decisions
  (established design practice, source links) and the places where the sources proved us wrong.
- **`docs/GLOSSARY.md`** — Japanese weapon/armour terms and the core's mechanical
  vocabulary (bind chance, lock, etc.); if a term is unfamiliar, look here first.

Do not reopen design decisions for debate — the user settled them item by item over a
long session. If a change is needed, update the GDD first.

## Architecture rule (critical, must not be broken)

The simulation core **must not depend on the engine**. The combat resolver knows nothing
about animation — it only produces an **event stream**; the visualisation consumes it.
Randomness must be **seeded and deterministic**.

Rationale: combat is fully automatic and chat affects the outcome; balance can only be
done by simulating tens of thousands of fights without opening the engine. If this
separation breaks, balance work becomes impossible. Details: `docs/ROADMAP.md` →
"Core Principle".

## Engine binary

The Godot executable does not go into the repo, it is kept under `tools/` (the same
arrangement as the other projects). `tools/` is gitignored.

## Store / marketing assets

Marketing assets such as the store listing, feature graphic, icon and screenshots are
**never committed to this public repo**. They are kept in two places:

1. A local, gitignored copy: `docs/store-assets-originals/`
2. A private backup repo: `C:\Projects\pictures\<project-folder>\` (the local clone of the
   private `Eren-Ozcan/pictures` repo) — the files are copied there, then committed and
   pushed in that repo.

## Studio-wide information

For topics that are not specific to this game (Google account, Play Console, Steam
developer account, the state of yilkgames.com) the single source of truth is
`C:\Projects\pictures\STUDIO.md`. It is not repeated here.
