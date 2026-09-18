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
| The man's own figure beside his numbers, and his path named under it | `WarriorPortrait.cs`, `WarriorRig.Stand` |
| One clause about whatever the hand is over, printed at the foot of a sheet | `UiKit.ChalkLine`, `UiKit.Explains` |

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
- **Nothing carries into a next term (6h), because there is no next term.** The canvas's
  closing sheet hands the men, the chest and the buildings on. The game is **one term**: the
  campaign ends at the final tournament and nothing is played past it (`docs/GDD.md` §10,
  closed decision 2b). The sheet therefore counts what the school ended holding and says the
  book closes there; a new school is opened from the title with a seed of its own.
- **A sheet answers "what is this for" on a chalk line, not in a card.** The canvas refuses
  floating tooltip cards (7b, 8a) and the build keeps that refusal, but it stops printing the
  clause under every row: it is fetched by hover **or** keyboard focus and printed in one fixed
  line at the foot of the paper. The grounds are in `docs/DESIGN-REFERENCES.md` §7.
- **Settings offers no switch it cannot honour.** Sound and the crowd in the chat are
  named on the sheet and said plainly to be unwired, rather than given switches that move
  and change nothing.

## The man's own face — where it appears

A warrior is drawn by one rig (`src/Game/Scripts/WarriorRig.cs`) and, until now, every warrior was
drawn by it identically: six men on the roster were six copies of one figure with different names
over them. `WarriorLook` (`src/Domina.Presentation/WarriorLook.cs`) is the small set of dials that
tells them apart — stature, build, head size, a shade on the team tint, hair, beard, a headband, a
scar, a sash colour.

- **It is derived, not stored.** The dials come from the man's **name**, so nothing is written to the
  save and the same man is the same figure in every screen, across a reload.
- **The name and not the id.** A market candidate is a `RecruitOffer` and has no id until he is
  bought; keying the look on the id would change his face at the moment of the purchase, which is the
  one moment the player is looking at him. A name belongs to one living warrior at a time (GDD §6),
  so the dojo cannot hold two men with one face. The cost is that renaming a man reprints him.
- **The skeleton is untouched.** The dials scale the bones and the drawing's width and hang marks on
  the head joint; the part list and the joints the animations run on are the same, so no animation is
  invalidated (ROADMAP, phase 2).

`WarriorPortrait` prints him at three crops — `Full`, `Bust`, `Head` — by pushing the rig's root below
the plate and letting the stage clip. Cropping rather than shrinking is what keeps a 30-pixel chip
readable. Where each one sits:

| Screen | Crop | Where |
| --- | --- | --- |
| Roster | Full | the plate beside his numbers, path under it |
| Roster, day screen (party) | Head | in the unit card's 44px square, which was an empty frame |
| Gate | Full | the man standing in the gateway, pale on the night |
| Market | Bust | beside the candidate's stats, the man being weighed |
| Armoury | Bust | at the head of the counter, so armour is fitted to a man |
| Hut | Head | on each tribunal card and each line on the mats |
| Battle HUD | Head | on each fighter's card, tinted to his side |
| Final night | Head | beside each tick in the party list |
| Season end | Head | over each name on the stone, printed in ash |
| Terms of going out | Full | each man going, and each of theirs the hut read |

The arena needs no wiring: it builds the same rig, so the man on the field is the man on the sheet.

### The ground they fight on

The arena drew a single line across the screen, so a fight read as two figures in a void.
`src/Game/Scripts/ArenaArt.cs` gives it the yard's own set — flat polygons in the night palette, listed
back to front: the night, two ridge lines, the far band the men walk in, a darker near apron, a roped
far edge, two torches at the near corners and a few scuffs in the dirt. It is drawn from
`ArenaLayout`, the same record the choreography stands the men on, so the paper and the men cannot
drift apart, and it is fixed rather than generated — scenery that changed between two runs of one seed
would make a recorded fight impossible to compare with itself. Like the figures, it is a stand-in:
what has to survive into real art is the staging, not the shapes.

### The terms of going out

`src/Game/Scripts/SortieScreen.cs` is the sheet held up at the moment of sending, with
`SortieModel` (`src/Domina.Presentation/SortieModel.cs`) behind it. The day's board already carried
the same figures, but they were read among every other decision the morning brings, and the party was
picked out of a list of names. The sheet puts the whole bargain on one page: **what it pays** and
**what it costs** above, **the men going** and **what is on the road** facing each other below, and
two answers at the foot — *Not today* and *Open the gate*.

The layout is the reference game's terms-of-the-bout page (REFERENCE-DOMINA-UI); its widgets are not.
Ours differs in three ways:

- **It decides nothing.** The party was judged by `OfferModel.Judge` before the sheet opened;
  refusing leaves the day exactly as it was, with the same men still ticked.
- **An unread road says so.** A dojo with no diviner sees one card marked *unread*, not an empty
  column — an empty column would read as a road with nothing on it (GDD §10: only the band is free).
- **The pull-out is a term.** Where the reference asks whether surrender is allowed, ours states the
  standing rule: the party may be pulled at any moment, and a man mid-strike leaves when the strike
  finishes.

The party is **ticked on this sheet**, not on the board behind it. Picking men and reading what the
road pays used to be two screens apart, which asked the player to choose his men before anything had
told him what the job was worth; the roster now sits under the two facing columns, each tick reprints
the terms, the count and the verdict, and the day's board keeps only the three acts (send, take the
bounty, skip) plus a line saying where the men are chosen. Refusing hands the ticked men back, so the
board remembers them, and the clock is held for as long as the sheet stands.

`sortie.tscn` (`SortieDemo`) stands the sheet up against the demo roster with two men already ticked,
so the layout can be looked at without playing a day to it — the same arrangement as `roster.tscn`.

### Gaps in the faces

- **The aftermath sheet** carries lines of text and no per-man structure, so the party that came back
  and the men who did not are named and not drawn.
- **The market's list rows** are plain rows; the face is in the detail panel only.
- **Gear is not on the figure.** What he carries is written beside him; the rig draws one blade and no
  armour, so two differently armoured men look the same below the neck.

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
- **A portrait on the man's own page.** The reference carries a full-body portrait and a class name on
  its gladiator panel, and ours prints the numbers with nothing to look at. The rig that draws a
  warrior in the yard exists; nothing poses it for the sheet yet.
- **The shrine, the desk and the ledger (4e, 4f).** The canvas's lower boards (2a–4g) were not in the
  file that was handed over, so those destinations are not in the yard — nothing in the yard leads
  nowhere.
