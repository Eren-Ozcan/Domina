# Prompt — readability and safe-area pass (paste into the Claude Design canvas)

Paste the whole thing. This is a correction pass over the finished 26-artboard set, not a redesign.

---

## 0. What this turn is for

The set is complete and the visual language is settled — dark woodblock × paper theatre, the living
yard as the hub. **Do not restyle anything, do not redraw compositions, do not move panels.** This
turn fixes measured defects only. The measurements below come from rendering the file at 1:1 and
walking the DOM of all 26 artboards (3,547 elements, 31,789 characters of text), so every number is
a real reading off this file, not an impression.

Work in the order below. If the quota runs out, stop at a whole item and say which is next.

## 1. The type is sitting on its floor — raise the ramp

Measured: **80% of all text in the set is 14–15px**. Only 6.7% is 18px or larger. The brief's "no
smaller than 14px" was a floor; the set treated it as the default.

The set must be readable at 1920×1080 from across a room — that was a stated requirement and it is
not met today. Raise the ramp, keeping the existing hierarchy intact:

- body / row text: 14 → **16px**
- secondary and caption text: 15 → **17px** (never let a caption end up larger than its body)
- the numbers in the persistent top strip (store, day, what is closing in): **18px minimum**, and
  the store figures themselves may go to 20px
- headings, titles and display sizes stay as they are — do not scale the whole ramp, only lift the
  bottom two steps so the gap between body and heading narrows
- nothing in the set below 16px afterwards, captions included. Today the floor is 12px (four
  elements on **7a Overlay anatomy**).

Reflow whatever this breaks rather than shrinking text back down. If a panel cannot hold its content
at 16px, cut the content, not the size.

## 2. Nothing sits in the safe area

Measured: on **20 of 26 artboards** the outermost text sits **12px from the top edge and 26px from
the sides**. The persistent strip lives in exactly that 12px band. On a television, in Big Picture,
or on any overscanning panel, the strip is the first thing cropped — and it is the one layer that
must be true everywhere.

- Give every artboard a **title-safe margin of 96px left/right and 54px top/bottom** (5%). No text,
  no number, no icon, no interactive element crosses it.
- Move the persistent strip inside that margin; it may still read as pinned to the top edge, but its
  content starts at 54px.
- Backgrounds, the yard itself, paper edges and bleed art may continue to the artboard edge — this
  rule is about anything the player has to read or hit.

## 3. Four contrast failures (WCAG AA, 4.5:1 for text below 24px)

Measured against their own backgrounds:

| Artboard | Element | Ratio | Size |
|---|---|---|---|
| 6a Title | `v0.4 · a sample build` | 3.39:1 | 14px |
| 6a Title | `seed 41-882-06` | 3.39:1 | 14px |
| 2a Plate | `EDO PERIOD · 60-DAY TERM` | 3.29:1 | 14px |
| 2b Day | `The dojo at Ashigara · second term` | 4.04:1 | 14px |

Fix each to **4.5:1 or better** by lifting the ink, not by brightening the paper.

Then do a pass of your own: much of the set's text sits on SVG paper shapes, which an automated walk
cannot sample, so the four above are a floor and not a ceiling. Check every piece of text that sits
on a drawn sheet, on the yard at night, and on the fight's ground, and state which ones you changed.

## 4. The ink has drifted — collapse the text palette

Measured: **17 distinct text colours** in use. Five of them are near-identical creams and greys and
are doing the same job:

`rgb(138,127,111)` · `rgb(125,115,101)` · `rgb(168,156,138)` · `rgb(207,193,164)` · `rgb(233,223,201)`

Collapse the whole set to the ramp already defined on the system plate — four or five inks at most
(primary ink, secondary ink, muted, ochre accent, blood) — and apply it across all 26 artboards.
Update the plate to show the final ramp with its contrast ratios written beside each step.

## 5. The yard does not name anything

Measured: the **Yard artboard carries 13 text nodes in total** — the persistent strip and almost
nothing else. That is the point of the design, and it is also its one real usability risk: a player
who does not recognise an object has no way to learn what it is.

Add three things without breaking the diegetic rule:

1. **A hover state that names the thing**, drawn in the world's language rather than as a tooltip
   chip: the object's name and, in one short line, the single thing it does ("The rack · change what
   a man carries"). Show it on the Yard artboard for one object and add it to the destination states
   plate (7b) for all five states.
2. **A first-term orientation**: the first time the player stands in the yard, the destinations that
   matter today make themselves known once, in sequence, and never again. One artboard state is
   enough to fix the idea.
3. **An accessibility toggle, "name the destinations"**, which leaves the names on permanently for
   players who want them.

## 6. Settings has no accessibility section at all

Measured: the strings `reduced motion`, `text size` and `colour-safe` appear **zero times** in the
file. Add an accessibility panel to **6d Settings**, with at least:

- text size (the 16px base, plus a larger step that the layouts survive)
- reduced motion (the yard's life, smoke, crowd movement)
- colour-safe check, stated so it cannot break the blood rule: red stays reserved for blood in every
  mode, so the mode adjusts everything else around it
- "name the destinations" from §5
- subtitles / speed of the day log

Say in the panel what each one costs or changes, the way the rest of the set states consequences.

## 7. Five targets are too small

Measured: button heights run 28–75px, median 41px — good. Five elements fall below 32px:
two on **4c Market**, two on **4e Shrine**, one on **4g Roster**. Raise those to **32px minimum**,
matching the spacing of their neighbours.

## 8. One unrendered template token is on screen

**3a Yard**, top strip, right side: the literal text `{{ clockNote }}` is rendered where 3b and the
4x boards read "the yard waits". It is the only such token in the file. Replace it with the real
line.

## 9. The set is not one save file — two timelines are interleaved

Measured by reading every board's strip and copy:

- **day 23 / 412 koku / seven men / Ayame alive**: 2b, 3a, 3b, 4a–4g, 5a, 5b, 5c, 6e, "Yard — named"
- **day 25–26 / 686 koku / five men / Ayame dead**: 5d, 6f — and, the problem, **6a Title** and
  **6c Save slots**, which are the two screens that claim to describe the save the player is about
  to enter. The title screen offers to continue a save no hub screen shows.
- specimen material carrying the wrong day: **2a Plate** ("240 koku · day 25", "back the evening of
  day 25") and **7a** ("Day 25" on the "whole stage" example, beside a "Day 23" sheet example).

Decide it this way and apply it everywhere: **the set's present is day 23 of 60, seven men, 412
koku, Ayame alive.** 5d Aftermath and 6f Tribunal may stay later in the season — they are the
consequence of the contract the set is about — but they must be the *same* later moment and must say
so. 6a and 6c must advertise the day-23 save. 2a and 7a specimens move to day 23.

Related contradiction to settle in the same pass: the men in the dojo. **2b** says "four in the
dojo", **6a** says "five men standing", **4f and 6e** say seven. Pick seven and fix the other two.

Two more copy contradictions:

- **5b**: "4 medicine, 6 days abed. The chest holds three." — the cost and the store disagree.
- **4b Training**: the assignment list shows 5 men while its own pager and 4f say 7.

## 10. Overlaps and collisions

- **5c Province** — the "The Ii school at Mishima" label box is overlapped by the "THE BOUNTY GATE"
  panel; one line of it is unreadable. Blocker.
- **6g Final night** — the "Let him walk out" button's lower edge sits on the Hattori Sada unit card
  below it; the trailing "31st" sits on that card's bottom edge.
- **4b Training** — "WHAT SADA CAN STILL LEARN" wraps to two lines and collides with the ‹1st of 7›
  pager to its right.
- **4g Roster** — the bottom panel's two-line heading and the ochre "the better of the two, and not
  enough alone" crowd into each other.
- **2a Plate** — the "End his term" demo button wraps to three lines in a box sized for one.
- **2b Day** — the "End Genji's term" button's second line overruns the box's bottom edge.
- **6f Tribunal** — the grey helper text beside "Send the yard to bed" runs over the crowd
  silhouettes to its right.

## 11. Text drawn over artwork, in the world's own greys

- **7b Notices and states**, the "NAMING A THING" example: "the rack" and its subtitle "one blade
  wanting a hand" are drawn straight over the rack's posts, grey on dark. It is the least legible
  text in the set — and it is the board that defines how naming works, so it has to be exemplary.
- **"First term"** (the day-1 onboarding board): the two upper hotspot descriptions sit grey-on-navy
  over the sky and the dojo roof.

Give the name its own ground — a darkened wash behind it, a cut-paper slip behind the words, or the
ink lifted — without turning it into a floating tooltip chip, which the board's own thesis rejects.

## 12. The confirm anatomy is missing its failure case

The confirm family is currently split across three boards: **7a** holds the plain confirm and the
irreversible "Cut" family, **2a** and **3b** hold the blocked confirm with the live counter ("2 of 3
chosen"), **5b** and **4c** hold the costly confirm, **6c** holds the write-over.

- Add the **error / failure state**, which does not exist anywhere in the file: an act the player
  committed to that did not happen (the smith refused, the man would not go, the connection to the
  crowd dropped mid-fight). Say what failed, what it cost, and what the player may do now.
- Bring the **counter variant** onto 7a as well, so that board reads as the complete anatomy of a
  decision rather than as a subset.

## 13. 7c Flow does not cover the set

- Its headline says "THE WHOLE SET · TWENTY-TWO ARTBOARDS"; the file holds **28** boards with a
  `data-screen-label`. Fix the count and keep it fixed.
- **6e Pause** has no node — it appears only as the words "Stopped: 6e" inside the yard node.
- **2a, 2b, 7a, 7b, 7c** are relegated to footer prose; put every board in the map, marking the
  plates as plates.
- Add a **legend and a per-node marker** for modal / opens-over-the-yard / full-screen. Today the
  distinction is implied by column grouping and wording only.

## 14. Armour tier fails at the size the game actually uses

2a claims every tier is tellable in flat black — true at portrait size, false on the unit card. On
**5a Fight**, Sada (partial lamellar) and Ren (robe) are near-identical narrow glyphs separated only
by a faint shoulder bump; only the horned ō-yoroi reads reliably.

Redraw the **card-size glyphs** so robe / leather / partial lamellar / ō-yoroi separate at that size
— exaggerate the silhouette cue the tier owns (drape, fitted edge, standing plates, horns) rather
than adding detail. Then show all four at card size on 2a beside the portrait-size row, so the plate
proves the claim at both sizes.

## 15. The persistent strip is not persistent

Its composition changes across the destinations: **4a** and **4e** omit water; **4e** shows sake,
the others do not. The one layer that must be true everywhere has to carry the same fields in the
same order on every board.

## 16. Whitespace that reads as unfinished

Large blank cream bands inside otherwise finished sheets: **4a** (~90px between the "WHAT HE
CARRIES" heading and the first slot row, plus a second band above the bottom row), **3b**, **5d**,
**6b**, **6h**, **4e**, and an L-shaped void on **4d** where the left column ends well above the
right. Close them by reflowing, not by adding filler.

Also **7a**: the grey/blue blocks inside "A slip" / "A sheet" / "The whole stage" are intentional
diagram blocks, but nothing says so — add a hatch or a "(dimmed ground)" caption so they stop
reading as missing content.

## 17. Two smaller notes

- **3a Yard** stages three figures for a roster of seven, and the lower fifth is bare ground.
- **5c Province** fills the rival school's marker with brick `#7a3527`, the pigment 2a reserves for
  warnings and refusals. Blood red itself is clean — vermilion appears only on 5a's pool and on the
  two swatches — so keep it that way and give the rival a pigment of its own.

## 18. Deliver

Do the blockers first: 8 (the token), 9 (one save file), 10's first bullet (the 5c overlap), 12 (the
missing error state). Then the mechanical passes 1, 2, 3, 4, 7, 15, which touch every artboard. Then
the rest of 10 and 11, then 5, 6, 13, 14, 16, 17. At the end, state:

- which artboards changed, and what broke and had to reflow
- the final type ramp and the final ink ramp, with contrast ratios
- anything you could not fix without restyling, listed rather than silently skipped
