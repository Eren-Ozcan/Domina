# The paper theatre — how the design canvas landed in the game

The interface was designed on a canvas of its own (boards 5a–10a; the local copy is
`design/paper-theatre/Main.dc.html`). This file records what that design decided, what
the build now does about it, and what is still a gap. The canvas is the picture; this is
the account of the build.

## What the design decides

- **The world is a lit night and everything readable is paper laid over it.** Square
  corners, a hard cast shadow down and to the right, ink printed on paper. No rounded
  cards, no soft glows.
- **There is one hub and it is a place.** The dojo yard. Every destination is a thing
  standing in it — the board, the rack, the post, the cart, the men, the gate — and a
  destination names itself in chalk under the cursor, with exactly one clause saying what
  it does. No navigation bar, no menu, no tooltip cards (7b, 8a).
- **Three overlay sizes, one rule: the ground stays visible.** A slip for one fact and
  one act, a sheet for the common case, and the whole stage for the two places the player
  walks to — the province and the ground (7a, 7c).
- **Four decision shapes** (7a): indigo for the one act a sheet exists for; outlined
  paper for the way out; paper pressed flat, in place, with the number that refuses it,
  for a blocked act; and a shape cut out of the night with a brick edge for an act that
  cannot be taken back. A cut shape never says *Close*, and *Close* is never cut.
- **Nothing pops; the world reports.** Notices are slips set down at the edge of the
  yard, three at most, oldest gone first, never dismissed (7b).
- **Blood is not an interface colour.** Brick is the interface's red; vermilion means
  something has been cut, and nothing legible depends on telling those two apart (6d).

## What the build does

| Design | In the build |
| --- | --- |
| The kit — pigments, type, paper, the four decision shapes | `src/Game/Scripts/UiKit.cs` |
| The yard, its objects, their chalk names, its notices, the first-term introduction (8a, 8b) | `src/Game/Scripts/Yard.cs`, `YardArt.cs` |
| The strip along the top (day, stores in days, the hour, the clock's speed) | `DojoHub.BuildStrip`, `StripModel` in `Domina.Presentation` |
| Sheets over the yard; the two screens that take the whole stage | `DojoScreen.BuildPage`, `TakesTheStage` |
| Title, the yard at night (6a) | `TitleScreen.cs` |
| Opening a school — province, the two names, seed, what the seed opens with (6b) | `NewTermScreen.cs`, `NewTermModel` |
| Settings (6d) | `SettingsScreen.cs`, `GameSettings.cs` |
| The paper itself: grain, inked edges, and the hour washed over the yard | `PaperOverlay.cs`, `DayTint` in `Domina.Presentation` |
| The world stopped, and the four acts about it (6e) | `PauseScreen.cs` |
| The fight: two sides in pips, the stream in English, the one order (5a) | `BattleHud.cs`, `FightLog` in `Domina.Presentation` |
| The pull-out, with its cost stated first (5b) | `BattleHud.Confirm` |
| The aftermath, opened over the yard (5d) | `AftermathScreen.cs` |
| The province as a drawn map, with the villages pinned on it (5c) | `ProvinceScreen.cs` |
| The hut: who is on the mats, and who is called to stand (6f) | `HutScreen.cs`, `TribunalModel` |
| The final night, announced rather than listed (6g) | `FinalNightScreen.cs` |
| The term counted, the letter, and what carries over (6h) | `SeasonEndScreen.cs` |
| The term lost, cut rather than printed, and where it turned (9a) | `SeasonEndScreen.cs`, `TurningPoints`, `TermMark` in the core |
| Someone at the gate, and what taking him in costs (9b) | `GateScreen.cs`, `GateModel`, `ViewerGate` in `Domina.Chat` |
| An order that came back undone (10a) | `DojoScreen.Returned`, used by the rack's refusals |
| Three terms kept, and an overwrite shaped unlike a load (6c) | `SavesScreen.cs`, `SaveSlot` (three slots) |

The two faces the canvas uses are bundled, subsetted to the glyphs the screens print:
`src/Game/Fonts/`, listed in `THIRD-PARTY-NOTICES.md`.

### Where the build says no to the canvas

- **The pull-out is the party's, not one man's.** Board 5b draws *Pull Ren off the
  field*. GDD §5 decides that surrender is a single key that pulls everyone, because a
  per-man version makes the right play "pull the wounded one and fight on with the rest".
  The confirm sheet is built, with the cost stated first; the order it gives is the
  party's.
- **The term is 180 days, not 60.** The canvas's eyebrow and its save cards say sixty;
  the season's own tuning says otherwise, and the screens read the tuning.
- **Settings offers no switch it cannot honour.** Sound and the crowd in the chat are
  named on the sheet and said plainly to be unwired, rather than given switches that move
  and change nothing.

## Gaps — what the canvas has and the build does not

- **The chat transport.** `ViewerGate` in `Domina.Chat` is the seam the design's crowd plugs into: a
  viewer asks, a man is drawn off the term's seed and the viewer's name, and the gateway sheet opens
  over the yard. Nothing calls `Ask` yet, because there is no Twitch or Kick connection in this build,
  so the gate stands empty and the crowd panels in 6b and 6d say so rather than offering switches.
- **Sound.** There is none, so the settings sheet names the panel and leaves it unwired.
- **Carrying a term into the next one (6h).** The closing sheet counts what a next term would inherit,
  but the core ends a term and frees the slot; nothing yet opens a second term with the first one's
  men. The panel says so plainly.
- **The three player-pressed ends of the tribunal (6f).** GDD §6 gives the verdict to the crowd, with
  an artificial crowd standing in when nobody is watching, so the hut prints what each end costs and
  the player's own act is to send the yard to bed.
- **The per-man pull-out (5b).** GDD §5 makes the order the party's; the confirm sheet states the cost
  first, as the design asks, but the order it gives pulls everybody.
- **The shrine, the desk and the ledger (4e, 4f).** The canvas's lower boards (2a–4g) were not in the
  file that was handed over, so those destinations are not in the yard — nothing in the yard leads
  nowhere.
