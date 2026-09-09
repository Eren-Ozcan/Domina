# The reference game: Domina — a screen and interface breakdown

`REFERENCE-DOMINA.md` describes the **systems**; this file describes the **screens**: which
information sits where, what each control looks like, and which box the player looks at to make
a decision.

Source: 84 frames (1280×720) from the video "Domina Beginners Guide To Starting Right PLUS Tips
& Tricks (2018 Edition)". Everything is written **as far as it could be seen**; what could not
be read is marked "unclear". The game is the 2018 version.

> **Why it exists:** we have four screens (day, roster, market, school) and all of them are a
> flat `VBoxContainer` list. The reference's interface carries the established solutions of a
> **twenty-year-old genre**; before deciding what to take from it we need to see what it is.

---

## 0. The permanent top bar

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Coin: 935 ●  Water: 400 ◆  Food: 800 ◆  Wine: 80 ◆     Next Battle: 3     │
│                                                         Days Left: 364    │
│                                                                     [II]  │
└──────────────────────────────────────────────────────────────────────────┘
```

- On the left, four resources: **a label + a number + a small coloured icon**. No bars, no
  percentages, always whole numbers. No panel is laid over this bar.
- On the right, two lines: `Next Battle` (the days left until the compulsory fight) and
  `Days Left` (the end of the year).
- At the far right a small symbol shows whether the game is running; when paused, a large
  **PAUSED** appears in the middle of the screen **and the whole world goes into a bluish-grey
  filter** — so "paused" is carried not by a single word but by the screen's colour.

**What is missing:** none of the resources has a **trend**. It says "800 food" but not how much
melts a day or how much comes in; to see that you have to open the Architect panel. Scarcity is
the game's main pressure and that information is not in the HUD.

## 1. The main screen: the courtyard

```
┌──────────────────────────────────────────────────────────────────────────┐
│ [the top bar]                                                             │
│  ▯▯▯▯▯▯▯ a portico / building facade — the staff walk here                │
│                                                                           │
│            an open sand courtyard — the gladiators drill here            │
│            (a thin green bar under the unit + a name label)              │
│            text above him: "Granius is Strength Training"                │
│                                                                           │
│  ─── railing ───                                                          │
│  NPCs (the Legate in a red robe, a priestess in white) and object props:  │
│  an anvil, a paperwork desk, a well, a "Map of Games" sign                │
└──────────────────────────────────────────────────────────────────────────┘
```

- The screen is **not a menu but a diorama**: a fixed camera, a living courtyard. All the panels
  open **on top** of it; none is full screen, and the world behind keeps being visible and
  playing.
- **The world itself is the menu:** you click a figure to open a staff member or an NPC, and the
  desk to open the market. There is **no** separate navigation bar.
- A unit's state is told with a single combined "unit card": **a portrait + a name + a coloured
  health bar**. The same card appears in the courtyard, in the combat HUD and in the battle
  contract, identically.
- The slaves are **naked and in green shorts**; the gladiators are armoured. Who is what is clear
  without reading a label.
- What a unit is doing is told with **plain text written above him** ("X is Strength Training").
  No icon, no progress bar.

## 2. The panels' shared skeleton

All the staff panels use **the same** template:

```
┌──────────────────────────────────────────┐
│ <Name> Info                               │
│  [Button]        [Button]                 │
│  [Button]        [Button]                 │
│  [Button]        [Button]                 │
│                                            │
│ [Fire Employee]              [ Close ]     │
└──────────────────────────────────────────┘
```

- A maroon, speckled parchment ground; the title at the top left; **a grid of plain text buttons
  in two (sometimes three) columns**; **Fire Employee** at the bottom left, **Close** at the
  bottom right (with a red focus frame).
- **No icons.** The Architect panel, the Bard panel and the priest panel — without reading the
  title they cannot be told apart. Consistency was won, **recognisability lost**.
- While a research is running **the whole grid dims** — not item by item, the panel locks as a
  whole.

### On hover: information in two parts

- **Above** the button, a cost badge: something like `-30 gold · -30 water · -15 stone`.
- **Below** the button, a tooltip box: a one-sentence explanation.
- **No collision avoidance:** the badge cuts across the text of the button above, and the tooltip
  covers the buttons below. This is the whole game's most frequently repeated interface flaw.

## 3. The gladiator panel (the densest screen)

```
┌────────────────────────────────────────────────────────────────────────┐
│ Gladiator Info                                                          │
│ Vettius of Melitensium      Weight: 91kg    ┌─PRIMARY──────┐           │
│ Temperament  -[===|===]+    Total: 124kg    │ Basic Pugio  │  ┌──────┐ │
│              "Satisfied"                     │ A:+7 D:+7 1kg│  │ full │ │
│ Health [======green======] [Heal]           │ [==bar==]    │  │ body │ │
│        145/145                               └──────────────┘  │portrait│
│                                              ┌─SECONDARY────┐  │      │ │
│ Training Balance   Level  Points             │ (if any)     │  │THRAEX│ │
│  Agility  ─●────    13     62                └──────────────┘  └──────┘ │
│  Weapon   ─●────    14     34                                            │
│  Defense  ─●────    13     49                                            │
│  Strength ─●────    10   145HP MAX                                       │
│  Meditate ●─────   100     [Train]                                       │
│  Aggro:79  Turtle:21  Evasive:56  Stamina:50                            │
│  Victories: 3, Losses: 1                                                 │
│ [Reward Wine 1▲▼][Reward Coin 1▲▼][Award Private Room (disabled)]      │
│              [Put to Death][Grant Freedom][Sell] [<][>] [Close]         │
└────────────────────────────────────────────────────────────────────────┘
```

There are **25-30 separate numbers** on screen at once. Three things to learn:

1. **Training is shown in two layers:** for every stat there is a **slider** (how much of his
   time it eats), a **Level** and **Points**. So the player sees "what am I training" and "how
   far have I come" on the same line.
2. **The derived numbers are exposed:** Aggro / Turtle / Evasive / Stamina are written as plain
   numbers. The behaviour is not hidden.
3. **There are `<` `>` arrows** — the roster can be browsed without closing the panel. The roster
   screen and the warrior screen are **the same screen**.

**The flaws:** `Put to Death` — an irreversible action — looks **exactly the same** as `Sell` and
`Close`; there is no red "danger" colour anywhere. `Award Private Room` is disabled but **it does
not say why** (it needs the Architect's private-room building). There is **one** `Train` button
for five sliders, and which one it trains is unclear.

## 4. The Doctore: the skill tree

The one "full width" panel. ~30 nodes, two clusters, with **vertical connecting lines**
(prerequisites) between them. At the bottom left `Fire Employee`, on the right an
**`Enable Automatic Gladiator Training` checkbox** and `Close`.

- An ordinary node is ~20-70 gold; **a node that unlocks a class is 400-500 gold and 16-17
  turns**. But **the button's size and font are the same** — the information "this is a big
  decision" lives only in the number.
- Bought / buyable / locked nodes look **almost equally dim**. The tree does not answer "where
  did I get to" at a glance.
- When the tooltip box is large it completely covers the nodes behind it.

## 5. The market

```
┌───────────────────────────────────┐
│ City Market                        │
│  [the cost preview strip]          │
│ ┌───────┐ ┌───────┐ ┌───────┐      │
│ │x10 🍎 │ │ x0 🍯 │ │ x5 ◆ │      │
│ │Buy Food│ │Buy Wine│ │Buy Water│  │
│ │Buy All │ │Buy All │ │Buy All │   │
│ └───────┘ └───────┘ └───────┘      │
│              Sell                   │
│ [Sell Food][Sell Wine][Sell Water]  │  (all three disabled)
│ [Attend Pit Fight] [Hire Employees] │
│                          [ Close ]  │
└───────────────────────────────────┘
```

- The market is also a **junction**: the pit fight and hiring staff open from here.
- The stock is a small **badge** above the item (`x10`). The price is **not written** on the
  button — it appears in the preview strip above.
- The buy cells have icons, the sell cells do not; the two rows do not resemble each other.

## 6. The Legate / Magistrate panel

```
┌───────────────────────────────────────┐
│ Legate Germanicus Terentius            │
│ Temperament  -[====|===]+  "Satisfied" │
│ Bribery: [2 ▲▼]        [ Send Wine ]   │
│ [ Suggest Gladiator Patronage ]        │
│ [ Purchase Gladiators ]                │
│ [ Arrange Exhibition Match ]  (disabled)│
│ [ Sell Secret <Magistrate> ]  (disabled)│
│ [ Blackmail <Legate Secret> ] (disabled)│
│                              [ Close ] │
└───────────────────────────────────────┘
```

- The title is **the person's name, not the role's** — where the staff panels say "X Info", the
  NPC panel becomes personal.
- **The Temperament bar is the same widget as in the gladiator panel.** So "morale" and
  "relationship" are shown as a single concept in the game. This is the interface's most elegant
  decision.
- The counter + button pair (`Bribery: 2 ▲▼` + `Send Wine`) is the game's most understandable
  control.
- Three options are disabled and **none of them says why**.

## 7. The battle contract

```
┌──────────────────────────────────────────────────────────────────────┐
│ Arena Battle   Host: The Emperor        Game Type: Championship       │
│ Victory Reward: 131● 166🍎 155◆ 22🍯 2 Slaves   "A battle to the      │
│ Participation Cost: 11● 2🍎 1◆ 6 days            death against Ancus" │
│ Surrender Allowed: No                   Obstacles: Tigers             │
│                                                                        │
│ Pick Your Gladiators                    Opponent Gladiators           │
│ (Mind Control Not Researched)           ┌──────────────┐              │
│ ┌────────────────────┐        vs        │ portrait AI  │              │
│ │  (the pick box)     │                  │ Ancus...     │              │
│ └────────────────────┘                  │ [health bar] │              │
│ [Pick Gladiators]  Selected/MAX: 0/3                                  │
│                        [Reject Terms]        [Accept Terms]           │
└──────────────────────────────────────────────────────────────────────┘
```

- Symmetrical: **your cost** on the left, **the opponent and the terms** on the right, "vs" in
  the middle.
- The game's **most icon-heavy** screen: the reward and cost lines are icon+number, because those
  lines repeat often.
- The live `Selected/MAX: 0/3` counter and the `Accept Terms` that stays disabled until someone
  is selected — the game's cleanest gating logic.
- The `(Mind Control Not Researched)` parenthesis: the lock is **in front of the decision**, but
  where it unlocks is not written.

## 8. The battle screen

```
┌──────────────────────────────────────────────────────────────┐
│                            [Tullus 141/141][Vettius 24/145]   │
│                                              2:39             │
│         dust + blood particles; the fighters inside the cloud │
│                                                                │
│   the bottom 40% of the screen: the crowd                     │
│                                        [Ancus 106/550]        │
└──────────────────────────────────────────────────────────────┘
```

- Your fighters at the top (a portrait + a health bar + **a number**), with a **countdown timer**
  below; the opponent at the bottom right.
- There is **no** ability key, cooldown or command bar. It is watched.
- **Readability is poor:** the fighters get lost inside the dust effect; who is winning can only
  be told from the bars in the corner. The arena gives "atmosphere", not "information".

## 9. The victory screen and the cards

- A **VICTORY** header; below it, for each fighter an **"AI Training MAX"** card and the training
  earned from the fight (`Agility +13 · Weapon +12 · Strength +8 · Defense +4`).
- The rewards are listed as icon+number; **slaves as portraits**, cards as cards, and a separate
  item: **`73` Crowd Favour**.
- **The card hand** sits at the bottom of the screen: cream/parchment cards with gilded corners —
  the only visual language **deliberately separated** from the maroon panels of the rest of the
  game. There is **no cost** on a card (these are not bought, they are given).
- A card is **dragged** and dropped onto the small portrait slots at the top left (Doctore,
  Educator, gladiator). Below are **SORT** and **DISCARD**.
- The flaw: the drag targets are very small and sit on top of the moving crowd.

## 10. The ceremonial screens

- `PREPARE FOR BATTLE` and `SELECT GLADIATOR CLASS` — the **only** place a large, ornate font is
  used. Everywhere else uses the same small pixel font. The display font is spent **sparingly**;
  that is why it works.
- The patron notification has no button: only text and "Press any key".

---

## Conclusions for our four screens

The reference's decisions that work:

1. **One unit card, the same everywhere.** A portrait + a name + a health bar; in the courtyard,
   in the fight, in the contract. In our game a warrior is written as three different row formats
   on three screens — worth unifying.
2. **The same widget for two concepts.** The Temperament bar shows both a gladiator's morale and
   an NPC relationship. In our game, honour and the (future) chat relationship could share the
   same visual language.
3. **The decision's cost next to the decision.** The upgrade screen shows the price of the
   upgrade **and** of the downgrade at once; the contract screen puts the reward and the day cost
   side by side. Our day screen writes the reward but not **the cost**.
4. **A live counter + a disabled confirm button** (`Selected/MAX: 2/3`, a disabled
   `Accept Terms`). We already have this (`PartyVerdict`), but we do not show the counter on
   screen.
5. **Browsing the roster from inside the panel** (`<` `>`). In our game you have to go back to
   the list for each warrior.

What will not be copied:

1. **Icon-less panels that all look the same.** Our four screens already resemble each other; the
   reference shows where that ends.
2. **A dangerous action looking ordinary.** `Put to Death` and `Close` are the same button. If
   decisions like seppuku and manumission arrive in our game, they must be set apart.
3. **An option disabled without a reason.** A disabled button should say why it is disabled — our
   `RefusalText` does this correctly and should be kept.
4. **Overlapping tooltips and cost badges.**
5. **A resource indicator with no trend.** Our day screen today also writes only the stock; a
   "daily consumption" line should be added.
6. **A fight view that gives no information.** Our arena is watched too; instead of a fight lost
   under dust, who is winning must stay readable.

---

*The source frames: `scratchpad/domina-ref/frames` and `.../ui` (session-scoped; they do not go
into the repo).*
