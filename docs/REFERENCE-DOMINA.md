# The reference game: Domina (Dolphin Barn, 2017)

This file describes **not our game** but the one we took inspiration from: *Domina* — Dolphin
Barn Incorporated's Roman gladiator school management game (Steam, 3 April 2017; removed from
the store in 2021). The aim is to work out its play item by item: which system produces which
decision, which number depends on what, and what the player fills a day with.

> **Why it exists:** the "what we take from Domina / what we deliberately change" table in GDD
> §1 is four lines. Four lines are not a reference. Borrowing a mechanic without knowing the
> mechanic **next to it** means not knowing why the borrowed piece works either.

## Sources and confidence level

Every item carries a mark:

| Mark | Meaning |
|---|---|
| **[K]** | It appears the same way in more than one source; reliable. |
| **[T]** | A single source, usually a strategy guide — that is, a player's **strategy**, which may not be the game's rule. |
| **[?]** | The sources contradict each other or there is no number; to be verified by playing. |
| **[V]** | From a gameplay video; seen on screen or described by the player. |

The sources (all read on 2026-09-05):

1. `steamcommunity.com/sharedfiles/filedetails/?id=1587630000` — "Beating Pro-Gamer
   Difficulty" (a post-2018 version; the most detailed account of the systems).
2. `?id=1123035492` — "Complete guide to Domina".
3. `?id=970561085` — "10 tips for new players".
4. `?id=1948681614` — "Ways to Win the Game" (the MEAT strategy).
5. `?id=2336870776` — "Brief Guide on Winning the Game (inc DLC Beta)".
6. `?id=905549957` — "How to be a Pro Gamer".
7. `?id=2507877258` — "Get Those Last 6 Achievements" (the file/save structure and a content list).
8. `gameplay.tips/guides/1006-domina.html` and `/6176-domina.html` (compilations of the above).
9. Wikipedia — *Domina (video game)*; Steam discussion threads (stat explanations, Twitch).
10. **A video:** "Domina Beginners Guide To Starting Right PLUS Tips & Tricks (2018 Edition)" —
    15:48, 1280×720. The transcript and the screen frames were read. The items marked **[V]**
    come from there. Because it is the 2018 version it contradicts newer guides (source 1) in
    places; the contradictions are written down separately.

**An important warning:** most of the sources are **strategy guides**. A guide says "do this",
not "this is the rule"; and the game changed a great deal between 2017 and 2021 (the guides
themselves say "the old guides are no longer valid" — especially about the Faber and EXP
nerfs). So most of the numbers are **[T]**. They can only be settled by playing.

---

## 1. The frame

- The player is a woman who inherits her father's **ludus** (gladiator school) (*domina* = lady).
  The goal is to win back the school's reputation. **[K]**
- The game runs on **a one-year countdown** and that is **written on screen**: `Days Left` at the
  top right. In the video's first frame it is **364** — so the year is **365 days**. **[V]**
- There is a second counter at the top right: `Next Battle: n` — **how many days until the
  scheduled fight**. In the video it counts down from 3 and returns to 7 after the fight. So the
  game carries two separate calendars: the end of the year and **the next compulsory fight**. **[V]**
- The guides talk in terms of counting days down: "the mid game at 300 days left", "the late game
  at 50-60 days left", "the Haruspex at 6-10 days left". **[T]**
- Time **flows in real time and can be paused**; every guide begins with "pause as soon as you
  enter the game". **[K]**
- There are difficulty tiers; the highest is **Pro-Gamer**. **[K]**
- The loss condition is not a single "game over": losing your best gladiator effectively ends the
  game ("basically it is game over"), because there is no roster ready for the final. **[T]**

## 2. The day and the screen flow

- The ludus is visible on a single screen: the courtyard, the training equipment, the staff, the
  gate. Gladiators are **dragged** next to the equipment; the equipment can be moved too. **[K]**
- `TAB` opens the HUD: who is working, who is idle. **[T]**
- Fights are chosen from the map/desk; the market screen opens from an object on the desk. **[T]**
- The Jupiter cards sit on a separate desk, and a card is **dragged** onto a gladiator or a staff
  member. **[K]**

## 3. Resources and economy

**The top bar (HUD)** always shows four resources: `Coin`, `Water`, `Food`, `Wine`; on the right
`Next Battle` and `Days Left`. While the game runs there is a pause symbol at the top right, and
when paused **PAUSED** is written in the middle of the screen. **[V]**

**The starting state in the video (the 2018 version):** `Coin 1000 · Water 400 · Food 800 ·
Wine 80 · Next Battle 3 · Days Left 364`. **[V]**

> Note: this is not the only number comparable with our 600-gold start — in Domina food and water
> start **as stock** (800/400), whereas our store is empty. Domina's first day opens with the
> question "what will I not spend", not "what will I buy".

| Resource | What it is for | Note |
|---|---|---|
| **Coin (gold)** | Slaves, staff, research, equipment, healing, betting | The single currency **[K]** |
| **Food / Water** | The roster's daily consumption | Storage upgrades lower the consumption/cost **[K]** |
| **Wine** | Bribes for the Legate and the Magistrate; gladiator morale | In the market **1 is restocked a day, with a stock cap of 2** **[T]** |
| **Stone** | The Architect's construction of training equipment | The Architect gathers stone continuously **[K]** |

- The market refreshes **every 2 days**. **[T]** (Another guide says wine arrives **every day**,
  one at a time — a contradiction **[?]**.)
- The guides' shared advice: keep the food/water stock at around 1000. **[T]**
- Income items: the rewards from scheduled fights, the **pit fight** reward + betting, the
  **exhibition** reward (100-200 coin/fight **[T]**), regional champion rewards, **crowd favour**
  (below), and selling surplus slaves/horses/chariots.
- Spending items: buying slaves, staff wages/hiring costs, research, equipment and repairs,
  healing, food/water, wine.
- **The staff eat resources too:** every staff member has a daily **food/water consumption** (see
  the table below) — so hiring staff is not only a **money** decision but a **store** decision.
  The Sacerdos has **no** consumption at all; that makes him especially valuable in the video's
  eyes. **[V]**
- **There are scarcity events:** drought and flood hit the food/water supply; that is why an
  Architect who sets up your own production is considered critical. **[V]**
- **Food can be sold:** the surplus is turned into money at the market, **7 food ≈ 1 gold** (the
  video, 2018). With the Agricola producing ~20 food a day that means ~3 gold a day — so surplus
  production is an income item, but a small one. **[V]**
- **The equipment price curve is stepped:** you upgrade the same piece many times, and while the
  price runs normally **one upgrade is suddenly expensive**, after which it is cheap again. The
  guide's exploit: have the **Faber do the expensive step for free**, then carry on with the
  cheap steps. **[T]**

## 4. The gladiator: stats

The values visible on screen (compiled from the Steam discussion) **[K]**:

- **HP / Vitality** — the health pool. ~120-160 is typical on starting slaves; 300-500 at the end,
  600+ if only strength is trained. **[T]**
- **Strength** — the damage base.
- **Weapon skill** — weapon proficiency.
- **Agility** — agility.
- **Defense** — defence.
- **Meditate / AI skill** — how well the gladiator manages himself **when the player does not
  intervene**. Automatic play rests on this. **[K]**
- **Morale / temperament** — morale; it **affects the stats**. **[K]**
- **Stamina** — every swing eats it; heavy two-handed weapons run him out of breath in 2-3 swings
  and the damage drops. **[K]**
- **Weight / Final weight** — the armour's weight; heavy armour slows him down and burns stamina. **[K]**
- **Aggressive / Defensive tendency (aggro / turtle) and Evasion** — **behavioural tendencies**,
  not skills:
  - high **aggro** → he walks at the enemy, starts the attack and keeps it up;
  - high **turtle** → he waits, wants the opponent to attack first, blocks;
  - high **evasion** → he rolls away and looks for an angle. **[K]**

### The gladiator panel — the full field list on screen **[V]**

Two gladiators were opened in the video; the panel shows:

- **Name and homeland:** "Vettius of Melitensium", "Granius of Helvetia"
- **Class:** THRAEX / MURMILLO (above the portrait)
- **Weight: 91kg · Total: 124kg** — body weight and the total **including the kit**
- **Temperament:** a slider, with a label ("Satisfied", "Neutral")
- **Health: 145/145** (a green bar) + a **Heal** button (disabled at full health)
- The **Training Balance** table — the columns are **Level** and **Points**:
  | Row | Example (Vettius) | Example (Granius) |
  |---|---|---|
  | Agility | 13 / 62 | 2 / 60 |
  | Weapon | 14 / 34 | 4 / 26 |
  | Defense | 13 / 49 | 5 / 36 |
  | Strength | 10 / **145HP MAX** | 5 / **158HP MAX** |
  | Meditate | **100** | 25 |
- **Aggro: 79 · Turtle: 21 · Evasive: 56 · Stamina: 50** (Granius: 69 / 34 / 63 / 50)
- **Victories: 3 · Losses: 1**
- Buttons: **Reward Wine [n]**, **Reward Coin [n]**, **Award Private Room** (disabled),
  **Put to Death**, **Grant Freedom**, **Sell**, forward/back arrows, **Close**

> Three things stand out. First, **Level and Points are separate** — so training is two-layered:
> the points that accumulate and the level that comes out of them. Second, **to the right of the
> Strength row it says not a point total but "145HP MAX"**: strength is directly the health
> ceiling. Third, **Aggro/Turtle/Evasive sit in the panel as numbers** (79/21/56) — the
> behavioural tendency is a number shown to the player **openly**, not a hidden personality.

The game's own tooltip text (the Strength row): *"Increase Hitpoints, Attack Damage, and Defense
Resiliance."* **[V]**

**The definitions in the video (2018)** — clearer than the guides' **[V]**:

| Stat | What it does |
|---|---|
| **Strength** | Three things on its own: **health**, **strike damage** and **resistance to damage**. That is why the "only pump strength" strategy works. |
| **Agility** | **Movement speed**. |
| **Weapon** | **Strike damage**. |
| **Defense** | The ability to **resist** an attack. |
| **Meditation** | The video's advice: **never train it** — it comes on its own from fights. |

> Note: Strength doing three jobs at once means that the trio we keep separate — `MaxHealth`,
> `Strength` and `Defense` — hangs on a **single slider** in Domina. That is also what makes the
> training decision shallow: the dominant strategy is "always strength".

> The counterpart in the reference to our §4 decision that "the behavioural difference is not
> separate code but target-selection weights" is exactly this: in Domina the class difference is
> **not a separate combat system** but different tendency numbers in the same system.

## 5. Training

- Every gladiator has a **slider** per stat; the player divides the training time. **[K]**
- The **auto-train** checkbox is opened at the Doctore; when it is on, everyone works
  continuously. **[K]**
- A common opening: first **Meditate to 100** (so the AI is good), then entirely into weapon. **[T]**
  One guide says the exact opposite: "do not touch the sliders, do not max meditate, you are
  breaking the balance; run plenty of exhibitions instead" **[?]**.
- **Fighting teaches faster than training.** A repeated claim: the AI skill rises as much in 1
  fight as in 10 days of training. With the Doctore Emeritus's *Master Mimic* skill, **+100 AI**
  and **+10-30 weapon/strength** can be seen in a single fight. **[T]**
- The training equipment (built by the Architect): the **palus** (training post), the **coal pit**,
  **stones**, the **bath** (healing speed), the apothecary. A guide sets a target of "15 palus". **[T]**
- There are stat **ceilings** and they rise with the Doctore/Doctore Emeritus researches; training
  time can be cut by **up to 75%**. **[T]**
- **Class training can only be done on classless slaves**; once a class is chosen it cannot be
  changed. The video rules out the class researches (unlocking Murmillo/Retiarius) as "expensive
  and very long, do not take them": trained gladiators with a class already arrive through the
  **fight reward** and by **buying from the Legate**. **[V]**
- The video says training a slave from scratch takes **very long** — buying a ready gladiator is
  almost always faster. **[V]**

## 6. Classes

A class is **assigned later** (slave → gladiator) and it sets the equipment template. **[K]**

| Class | Character | Tendency profile |
|---|---|---|
| **Murmillo** | Sword + shield, offensive | high aggro / low turtle / medium evasion **[K]** |
| **Thraex** | Defensive | medium aggro / high turtle / low evasion **[K]** |
| **Retiarius** | Net + trident, at a distance, debuffs with the net | low aggro / medium turtle / high evasion **[K]** |
| **Scissor** | Two-handed, offensive | **[T]** |
| **Velite** | Long-reach melee | **[T]** |
| **Sagittarius** | An archer; support in multi-fights, weak in a duel | **[T]** |
| **Charioteer** | For chariot racing | **[T]** |
| **Behemoth** | A giant/monster; a separate enemy type and an achievement target | **[T]** |

- Assigning a class has a cost: the guide says "**do not give a class to a slave who will not
  fight**, or the agent/Faber starts upgrading his equipment and eats your real man's money". **[T]**

### Class selection is a separate screen **[V]**

The **"SELECT GLADIATOR CLASS"** screen opens from the gladiator panel: three portrait buttons —
`Murmillo`, `Thraex`, `Retiarius`. The game's only **icon-first** screen; everywhere else uses
text buttons.

### The map: "Map of Games" **[V]**

A map of Italy; most regions have a **padlock**, one has a **green tick**. Beside it a box that
states the goal in plain text:

> "You need to defeat at least **3 Regional Champions** to be considered for the Final
> Championship in Rome. **1 / 3** have been defeated."

So entering the final is a **precondition**: at least 3 regional champions. The reason the guides
say "finish the Big 3 early" is not strategy but a **gate**.

### The patron event — the exact text **[V]**

> "Magistrate Atilius Antonius has agreed to become a patron of your ludus! He has adopted
> **Tullus of Lechia**. The Magistrate will be responsible for this gladiator's **food and
> water** until the day that he dies on the field of battle."

A notification with no buttons, closed with "Press any key". Patronage is exactly this: **an NPC
pays a gladiator's food and water until he dies**.

## 7. Staff (employees)

Staff sit in **slots**; the number of slots is limited and can be expanded with money (1500 gold)
**[T]**. The critical rule: **if you fire someone, the bonuses he researched go too** — the
buildings the Architect built stay, but the others' passive bonuses fall away. **[K]**

**The real hiring list taken from the video (price / daily consumption / promise)** **[V]**:

| Staff member | Price | Daily consumption | What the screen says |
|---|---|---|---|
| **Agent** | 13 | 1 food, 1 water | "Dirty work, free pit fights" |
| **Bard** | 13 | 1 food, 1 water | "Morale..." (cut off) |
| **Agricultor** | 25 | 1 water | "4 Food/day" |
| **Educator** | 30 | 1 food, 2 water | "Morale, AI Proficiency" |
| **Medicus** | 34 | 1 food, 1 water | "Gladiator Healing" |
| **Emptor** | 45 | 2 food, 1 water | "Reduced costs on upgrades and resources." |
| **Architect** | 65 | 2 food, 1 water | "Ludus upgrades" |
| **Haruspex** | 72 | 1 food, 1 water | "Sacrifices to the Gods" |
| **Faber** | 75 | 1 water, 1 food | "Inexpensive upgrades and equipment repairs." |
| **Sacerdos** | 100 | **nothing** | "Healing, Training, and Morale Boost" |
| **Vintner** | 100 | 1 water, 1 food | "Wine, Magistrate Favour" |

> The prices are **very cheap** (13-100 gold against a starting purse of 1000). So the staff
> decision is not a **money** decision but a **slot** decision: only a limited number can be kept
> at once, and a fired staff member's researches go. Our school tree being bounded by price
> (GDD §10) departs from this **deliberately**.

| Staff member | What he does | Notes |
|---|---|---|
| **Doctore** | Manages training; auto-train; the skill tree (Humility, Deep Breathing, Blade Control, Net/Polearm Defense, Attack Vector/Rolling Attack, Interpretive Dance, Automatic Yield, class unlocks, Mind Control) | The game's **free** starting staff member **[K]** |
| **Doctore Emeritus** | The expensive upper version: training time **-75%**, more EXP earned from fights, higher stat ceilings, *Deeper Humility* (an automatic yield at 20% health), *Master Mimic* (learning from the opponent), *Infinity Weapon* (one strike hitting several enemies), a critical chance | The strategic centre of most guides; "buy him by 300 days left" **[T]** |
| **Medicus** | Automatic healing; *wash hands*, *antiseptics* | When the number of wounded is high **[K]** |
| **Faber** | **Automatically repairs and upgrades** equipment; blueprint researches lower the purchase price | The automatic-upgrade frequency was nerfed **[T]** |
| **Faber Emeritus** | The upper version; two Fabers can be run together | **[T]** |
| **Architect** | Gathers stone, builds the **palus/coal pit/bath/storage**, expands the courtyard and the staff area | Raises the roster capacity to **28** **[T]** |
| **Architect Emeritus** | The upper version; training-area and storage upgrades | **[T]** |
| **Agent (Sneaky)** | **Steals weapons/armour**, **arranges pit fights**, betting; if caught he is lost and has to be rehired | Once his reputation is "Dark Figure" he is almost never caught **[T]** |
| **Sacerdos (the priest)** | Prayers: *Prayer to Venus* (regeneration in the ludus and **in the middle of a fight**), *Prayer to Mars*, Neptune; passive stat increases | Survival in the early game **[T]** |
| **Bard** | Songs: healing, weapon, agility, **morale** | For the morale ceiling, the bard + the bath + the Educator together **[T]** |
| **Educator** | *Philosophy*, *Anatomy*, *Focus*, combat proficiency, morale | If he is fired his bonuses go — he is kept to the end **[T]** |
| **Emptor** | Bargaining: discounts on equipment and provisions (it **stacks** with the Faber's discount) | Hired for the last shopping round, then fired **[T]** |
| **Haruspex** | **Curses** the enemy: halving his health, stripping his Jupiter cards, lowering his combat proficiency | The curses **are spent on the next fight** — so 6-10 days before the final no fights are taken **[T]** |

### The details the video (2018) gives **[V]**

- **Only 3 staff members** can be kept at once. (A newer guide talks about 6 slots and a 1500-gold
  slot upgrade — a version difference **[?]**.)
- **The Architect** is, according to the video, the opening's most important staff member, because
  he is the only one who gives **food + water + wine production on his own** and **the buildings he
  puts up stay after he is gone**. Because drought/flood events hit the supply, your own production
  saves your life. He **cannot be rehired** after being fired (the video's claim **[?]**), so do
  not fire him before he has built everything.
- **The build order (the video):** first the **palus** (3 turns), then **stones**, then the
  **coal pit**, then the **bath**. The effects: the palus **shortens the training time**, the
  stones **raise strength**, the coal pit affects **agility** (the transcript is unclear **[?]**),
  and the bath **raises morale and reduces injuries**.
- **The Faber**: free repairs + free automatic upgrades. The real value is in the **blueprints**:
  **every piece except the helmet and the shield counts as "armor"** (the pauldron, the chestplate,
  the skirt, the greaves) — so **a single armor blueprint makes all four cheaper**. Plus a weapon
  blueprint, faster repairs and **net rebuilding**.
- **The Sacerdos**: his researches take **1 turn** (the fastest); he **turns water into wine**;
  he raises defence, strength and agility; he heals; and he occasionally upgrades a weapon.
- **The Agricola** (the "agri-tower" in the video): **+4 food** as soon as he is hired, another
  **+4 food** with every research — in the video it reaches **20 food a day**.
- **The Educator's** researches: **Focus** (shortens all training times, **4 turns**),
  **Psychology** (reduces the **shame** of losing/yielding), **Anatomy** (attack damage and the
  **critical chance**), maximum stamina and the **stamina regeneration rate**, faster healing, and
  **AI combat proficiency**.
- **The Medicus** and **the Agent** are, according to the video, "interesting but weak": most staff
  already give some base healing.

### The Doctore skill tree — the full node list on screen **[V]**

The panel is called **"Special Training Maneuvers"**; a 3-column grid with connecting lines between
the nodes (a prerequisite chain). All the nodes read on screen:

`Wolf Courage` · `Attack Shuffle` · `Berserk` · `Attack Vector` · `Throw Weapons` ·
`Weight Training` · `Critical Strike` · `Disarming Weapon` · `Mindfulness` ·
`Blade Control` · `Murmillo Training` · `Mind Control` · `Rolling Attack` ·
`Dismemberment` · `Automatic Yield` · `Disarming Shield` · `Wind Sprints` · `Low Stance` ·
`Defense Shuffle` · `Endurance Training` · `Evasive Roll` · `Grip Techniques` ·
`Interpretive Dance` · `Aimed Defense` · `Deep Breathing` · `Net Defense` · `Aimed Attack` ·
`Shield Control` · `Polearm Defense` · `Nimble Stance` · `Retiarius Training` ·
`Humility` (at the bottom, in the middle).

At the bottom of the panel: **Fire Employee**, the **Enable Automatic Gladiator Training** checkbox
and **Close**.

**The price/duration examples seen on screen** — every node wants both **gold** and **turns** (an
hourglass), and some also food/water:

| Node | Cost | Tooltip text |
|---|---|---|
| `Murmillo Training` | **400 gold · 16 turns** | "Unlock the Murmillo Class" |
| `Retiarius Training` | **500 gold · 17 turns** | (unlocks a class) |
| `Disarming Weapon` | **67 gold · 6 turns** | "Gladiator has higher chance of disarming opponent during a successful attack." |
| `Mind Control` | **31 gold · 6 turns · 10 food · 10 water** | "Allows you to directly control one gladiator on the field of battle." |

> The scale is this: an ordinary skill is **67 gold**, unlocking a class is **400 gold and 16
> turns**. That is why the guides say "do not take the class researches" — 400 gold is 40% of the
> starting purse. **Mind control costing 31 gold** is interesting too: the game's most
> controversial feature (playing by hand) is almost free, but the crowd does not like it — the
> cost is not in money but in **income**.

### The other staff's research lists (from the screen) **[V]**

- **Faber → researches (from the screen):** `Automatic Upgrade`, `Improved Furnace`,
  `Improved Anvil`, `Helmet Blueprints`, `Weapon Blueprints`, `Armor Blueprints`,
  `Shield Blueprints`, `Rebuild Nets`; below them **two checkboxes**: `Auto Repair` (ticked) and
  `Auto Upgrade`. The tooltip: *"Automatically repair damaged equipment, free of cost."* **[V]**
- **Architect → "Building Tasks":** `Palus`, `Baths`, `Grain Shelter`, `Water Well`,
  `Wall Reinforcement`, `Private Gladiator Quarters`, `Dig Hot Coal Pit`, `Wine Cellar`,
  `Apothecary`, `Gather Stones`. The coal pit's tooltip: *"Hot coals under a gladiator's feet will
  decrease agility training time."* — so it **shortens agility's training time**, it does not raise
  agility (the question in §19 is closed). The well's tooltip is even clearer: *"Building a well
  will make the ludus more resilient during droughts, and will produce water. (**+2 to 5
  Water/day**)"* — **30 gold · 30 water · 15 stone**. The building is openly sold as **insurance**
  against the scarcity event. **[V]**
- **Sacerdos → prayers:** `Prayer to Neptune`, `Juno`, `Apollo`, `Mars`, `Mercury`, `Venus`,
  `Vulcan`. Mercury's cost: **7 gold · 1 turn · 10 wine · 30 food · 30 water**, tooltip
  *"Occasional upgrade to gladiator weapon."* — so the prayers' price is the **store** more than
  the gold.
- **Bard → songs:** `Song of Venus`, `Juno`, `Minerva`, `Vesta`, `Diana`. Diana: **11 gold · 6
  turns · 10 wine · 20 water**, *"boost gladiator weapon training speed"*.
- **Educator:** `Teachings of Galen`, `Teachings of Dioscorides`, `Anatomy`, `Psychology`,
  `Philosophy`, `Focus`.
  Dioscorides: *"cleanliness can help their body heal more quickly after injury"*.

### The order the video gives **[V]**

The video's order: **Humility → Aimed Attack → Blade Control → Aimed Defense → Evasive Roll →
Shield Control**. After that it is a matter of preference; the video continues with **Disarm
Weapon** (an opponent left unarmed is effectively dead), **Weight Training** (to carry heavy
armour/weapons), **Attack Vector** (**attack speed**), **Polearm Defense**, **Deep Breathing**,
**Grip Techniques** (against having your weapon taken) and **Net Defense**.

Two important notes:

- **Automatic Yield and Berserk exclude each other** — if you take one you cannot take the other.
  So "yield when you are losing" and "go berserk when you are losing" are **two ends of the same
  decision**. **[V]**
- One skill (its name is unclear in the transcript, ~17 turns) raises **how many victories a
  gladiator will carry before asking for freedom**. **[V]**
- **Mind control** is a separate research; the video's author never used it. **[V]**

## 8. Equipment

- The slots: **weapon, shield, helmet, chest, shoulder (pauldron), waist/skirt, legs (greaves)**,
  plus the **net** for the Retiarius. **[K]** In the panel the slots are labelled `PRIMARY` /
  `SECONDARY`; two weapons can be used (two "Wooden Gladius" in the video). **[V]**
- **Every piece has three numbers:** attack, defence and **kilograms** — on screen in the form
  `A:+9 D:+9 2kg`. Below it a green bar: **durability**. **[V]**
- **The upgrade is comparative on screen:** hovering over a piece shows `DOWNGRADE <L-Click>` on
  the left and `UPGRADE <R-Click>` on the right, with **the price of both directions** visible.
  The example in the video: *Improved Leather Chestplate (D:+9, 3kg)* → the upgrade *Centurion's
  Mail (D:+11, **22kg**)* for **-23 gold**; the downgrade *Standard Leather Chest Plate (D:+8,
  4kg)* for **+7 gold** (the piece is sold). **[V]**

> The balance lever here does not exist in our game: **19 kilos for +2 defence**. Because weight
> means stamina and speed, "better armour" is not a flat improvement but an open trade. We have an
> `ArmorPiece.Weight` field, but the decision does not appear this nakedly on screen.
- Every piece has **upgrade steps**; the Faber upgrades automatically, the player skips ahead with
  money. Repair is a separate job. **[K]**
- The guides' "good value" set: the Gladius, the Elite Roman Centurion Shield, Death's Helmet, the
  Centurion Mail, the Centurion Leathers, the Onyx Greaves. **[T]**
- Price examples **[T]**: two gladius for 80 gold; a typical Murmillo set 412 gold; basic kit for
  13 men 6,400 gold; 13 Zweihanders 3,600 gold.
- **Weight is a real cost:** heavy armour slows you and burns stamina; the guides use **the enemy's
  armour as a weakness** — "a heavily armoured opponent is out of breath in 2-3 swings". **[K]**
- The video suggests spreading the equipment investment over **4-5 gladiators**: investing in one
  man means not finding a roster for multi-fighter battles, while spreading it over everyone melts
  the money. **[V]**
- A perverse incentive: good equipment **lowers EXP** — when a fight ends quickly, less is learnt.
  That is why gladiators in training are deliberately given weak kit. **[T]**

## 9. Fight types

| Type | How it comes | What it gives | Risk |
|---|---|---|---|
| **Scheduled / forced fight** | Arranged by the Legate and the Magistrate | Reward + reputation | Death **[K]** |
| **Pit fight** | Arranged by the Agent | Reward + **betting** (the guide: 150 gold on every fight) | The opponent's strength is **invisible** **[T]** |
| **Exhibition** | The Legate's/Magistrate's exhibition match | EXP + coin; **yielding allowed, no death** | The risk of a permanent impediment, closed by *Deeper Humility* **[T]** |
| **Regional champions ("the Big 3" + 9 fights)** | Fixed opponents on the map | Coin + **a blue Jupiter card** | Fixed and **the same in every game** **[K]** |
| **Chariot race / Beast mode / Gravitas** | Special events (they need a horse, a chariot, a lion) | Coin, achievements | **[T]** |
| **The final championship** | At the end of the year | The end of the game | **15 well-equipped gladiators**, all **100+** in every stat **[K]** |

- **The regional champions are fixed:** the same stats, the same fight, in every game. The player
  can memorise them and prepare. **[K]**
- There is one more fight after the final and it is **easier**; the gladiators who were freed come
  back for it. **[T]**

### The fight offer screen — "Arena Battle" **[V]**

A fight arrives as a **contract card**; the screen shows:

| Field | Examples from the video |
|---|---|
| **Host** | "The Emperor" |
| **Game Type** | "Championship" / "1 vs 1" |
| **Victory Reward** | 213 gold · 75 food · 12 water · 2 wine · **2 slaves** — or 131 gold · 166 food · 155 water · **2 slaves** |
| **Participation Cost** | "11 gold · 2 food · 1 water · **6 days**" — or "None" |
| **Surrender Allowed** | **Yes / No** |
| **Obstacles** | "It's a mystery." / "Lions" |
| **Pick Your Gladiators** | The "(Mind Control Not Researched)" warning; a portrait + an `AI` label in each slot; the counter **Selected/MAX: 2/3** or **0/1** |
| **Opponent Gladiators** | The opponent's portrait, his name ("Ancus the Animal", "Clodius", "Dirkus Digglerus"), an `AI` label |
| Buttons | **Pick Gladiators · Reject Terms · Accept Terms** (Accept is disabled until someone is selected) |

> Four things concern us directly:
> 1. **The participation cost eats days** ("6 days") — the reference's counterpart to our "a day
>    takes one job" rule; there the day is **the fight's price**, not a fixed rule.
> 2. **"Surrender Allowed: Yes/No" is a field on the contract** — so the right to surrender varies
>    per fight. In our game pulling out is always available; this could have been a lever that can
>    be switched off.
> 3. **The reward is not only gold:** food, water, wine and **slaves**. The reward grows the roster
>    directly.
> 4. **The obstacles are written on the contract** and even "It's a mystery" is an option — the
>    unknown is sold openly.

### The battle screen **[V]**

- A wide-angle view of the arena floor from above; the bottom third of the screen is **the crowd**.
- At the top right, the player's fighters: a portrait + a health bar, **as numbers** `141/141`,
  `24/145`.
- Below it a **countdown timer**: `2:49 → 2:29 → 2:09`. So the fight has a **time limit**.
- At the bottom right the opponent: `Ancus the Animal 470/550 → 325/550 → 106/550`.
- On hits, **red damage numbers** float up (`-13`, `-10`) with a blood effect.
- There is **no ability key, cooldown or command bar** — unless mind control has been researched
  the fight is watched entirely.

### The victory screen **[V]**

A large **VICTORY** header, with two cards below — for each fighter an **"AI Training MAX"** and
the training earned from the fight:

- Fighter 1: `Agility +13 · Weapon +12 · Strength +8 · Defense +4`
- Fighter 2: `Agility +10 · Weapon +11 · Strength +6 · Defense +1`

Below it, **Rewards**: `213 gold · 75 food · 2 wine · 12 water` + two slave portraits
(`Papirianus`, `Granius`) + a card (`2X Production`) + a `Weapon Master` card + a separate item:
**`73` — "Crowd Favour"**.

> Two conclusions: (1) **Fighting really does give training** and the amount is written on screen —
> the claim "fighting teaches faster than training" becomes visible here. (2) **Crowd favour is a
> separate reward line** (73 gold) standing next to the fight reward — so "a good fight" and
> "winning" are rewarded separately.

## 10. The rules of a fight

- The fight is **real time**. If you want, the player **drives a single gladiator directly**
  (attack, block, dodge); if not, they all fight with the AI. **[K]**
- **The crowd (crowd favour) is a system:** the longer the fight and the more that happens on the
  field, the more the crowd likes it and **the more money** it pays. The crowd **does not like** the
  player driving his gladiator by hand ("mind control"). **[T]**
- **Yielding (missio):** the **in-game tooltip** for the `Automatic Yield` skill:
  *"Gladiator will automatically yield and surrender if they are less than 10% HP."* — the base
  threshold is **10%**. The **20%** the guides mention must come from the Doctore Emeritus's
  *Deeper Humility* upgrade; the two numbers are two tiers of the same lever. **[V]**
- A gladiator who yields takes no **permanent impediment** and comes out of the lost match alive.
  **[T]** And yielding is **not possible in every fight**: the contract has a
  `Surrender Allowed: Yes/No` field. **[V]**
- **A permanent impediment** exists and can make a gladiator useless; maimed slaves are either
  freed or shifted to a class where the impediment does not matter (charioteer, sagittarius). **[T]**
- **Death is permanent.** A dead gladiator is gone; players cheat by quitting to the menu and
  reloading the save. **[T]**
- **A lion/beast** can enter the field; unblocked it does very high damage, and the gladiators make
  the mistake of ignoring it and attacking each other. **[T]**

## 11. Difficulty scaling

- The game **gets harder as you win**: consecutive (scheduled) fights won and the ludus's overall
  strength/equipment pull the difficulty up. It does **not** come back down when someone dies. **[T]**
- The play that produces: **losing on purpose**. Every guide says "send a naked slave, throw the
  match, please the crowd, protect your champion". **[K]**
- If the ludus is **crowded**, the opponents get weaker ("most pit fighters do not even have a
  weapon"). **[T]** — the second end of the same scaling: the player pulls the difficulty down by
  growing his roster.

> This is the reference's most contentious lever: the difficulty punishes the player's **success**
> and the optimal play becomes "lose on purpose". Our measurement's binding resource being the
> roster (GDD §11) comes from a similar place, but we have **no** punishment mechanic.

## 12. Jupiter blessings (the cards)

- A card is **dragged** onto a gladiator or a staff member; it **can be sold** (selling is advised
  in the early game if you need money). **[K]**
- The effects: damage, defence, automatic healing, **research cost/time discounts** (15% and 33%,
  say), Attack Stance, Riposte, dual-weapon mastery, AI proficiency...
- **Blue cards** come as championship rewards and are the most valuable. **[T]**
- **The cards seen on screen and their exact text** **[V]**:
  | Card | Text |
  |---|---|
  | `Recover Cards` | "Place this card on any entity to recover applied cards." |
  | `Weapon Master` | "All attacks do 35% more damage." |
  | `Rebuff Tolerance` | "Gladiator recovers faster after hitting opponents defense" |
  | `2X Production` (a blue frame) | "Employees who produce resources will generate 2X more [does not stack]" |
- The card screen has **SORT** and **DISCARD** buttons — so the cards are kept as a **hand/deck**. **[V]**
- Cards also **drop as fight rewards** (`Weapon Master` appeared on the victory screen). **[V]**
- The opponents can have cards too; the Haruspex strips them. **[T]**
- **Moving a card costs nothing:** a card can be taken off one gladiator and put on another, or even
  on a staff member, **freely**. The video's use: put the card that "lowers the research cost by
  10%" on the staff member currently **researching**, and move it to someone else when the job is
  done; put the healing card on whoever is wounded at the time. **[V]**
- If you play the tutorial **you cannot access the Jupiter cards** — which is why the guide advises
  skipping the tutorial. **[T]**

## 13. Patronage: wine and two NPCs

- The **Legate** and the **Magistrate** are the two NPCs who arrange fights. You get into their good
  books by sending them **wine**; a satisfied NPC arranges better and more profitable matches. **[K]**
- The bribe's price **doubles every time** and the **exact amount** has to be sent (64, 128,
  256...); if you send less they get angry. The guide says "send it 4 times". **[T]**
- They can also be asked to become a **patron** of a gladiator; when a patron's gladiator dies you
  have to ask again. **[T]**
- **The Legate panel (from the screen)** **[V]**: the title "Legate Germanicus Terentius"; a
  **Temperament** bar ("Neutral"); the **Bribery: [1 ▲▼]** counter + a **Send Wine** button; and the
  action list: **Suggest Gladiator Patronage**, **Purchase Gladiators**, **Arrange Exhibition
  Match** (disabled), **Sell Secret \<Magistrate\>** (disabled), **Blackmail \<Legate Secret\>**
  (disabled).

  > So wine is not the only lever: **selling secrets and blackmail** are a separate system and
  > arrive locked (they must be unlocked by the Agent's spying). And **arranging an exhibition
  > match** is also on this panel and disabled — it depends on the relationship level.

- **The video's opening trick:** at the start **sell/fire every gladiator you will not use** so only
  two main gladiators are left; then send wine until the Legate is satisfied and ask to **become a
  patron** — because only two men are left, the patronage **certainly** falls on one of them. Do the
  same with the Magistrate and the second gladiator gets a patron too. **A patroned gladiator's food
  and water are covered by the patron** — so this is a choice-narrowing trick done to save
  resources. **[V]**
- One guide argues the opposite: after the first bribe, do not care about the relationship, it
  cannot be kept satisfied anyway **[?]**.

## 14. Events

- There are random events and some give **large** gifts: a gladiator that can be bought or captured,
  the soldier the Legate gives during a Gallic raid (he can arrive with **100-150** in every stat),
  owning a lion. **[T]**
- Staff **can die**; NPCs can die (which is why wine is kept in hand). **[T]**
- There is also an event where a stray gladiator is found at the gate. **[T]**

## 15. Morale

- Morale **affects the stats**. The ways to raise it: **gifting coin or wine** (cheap), the bath, the
  Bard's songs, the Educator. **[K]**
- **Crowding lowers morale**: when the roster grows too large the gladiators get uncomfortable; the
  Doctore Emeritus's first skill softens this penalty (comfortable up to 18 men). **[T]**

## 16. Freedom

- After fighting enough (the guide: ~10 fights) gladiators **ask for freedom**; if it is not given
  they **try to escape**. **[T]**
- A freed fighter **comes back in the post-final fight**, so in the late game half the roster is
  freed on purpose. **[T]**
- They are also freed to make room when the roster hits its cap. **[T]**

## 17. Twitch integration

- You connect by entering the stream name in the settings; the game's bot (`domina_bot`) joins the
  chat, **asks for votes** and collects input. **[K]**
- **Naming gladiators** after viewers, **voting** on events and a **reward increase** based on
  viewer reactions are described. **[T]** — The source chain is weak here; no detailed command list
  could be found, and because the game was pulled from the store there is no official documentation
  either. **[?]**
- The integration is **optional**; with it off the play does not change. **[K]**

> This section is exactly the input for our phase 5. The decision in GDD §8 ("the name pool comes
> from chat, everyone is included, `!no` opts out") was made to preserve Domina's model; but the
> detail of Domina's **voting** side is still unknown.

---

## 18. Mapping onto our game

| In Domina | In ours | Status |
|---|---|---|
| A day that flows in real time, pausable | A discrete **day** step, one decision | A deliberate difference |
| Driving a gladiator directly (mind control) | **None** — combat is fully automatic | A deliberate difference (GDD §1) |
| Tendency stats (aggro/turtle/evasion) | Target-selection weights (§4) | **The same idea**; the per-kind adversary profiles will derive from it |
| Surrendering with a mashed QTE | A **single key** withdrawal decision | A deliberate difference |
| Yielding = escaping an impediment | In our game withdrawing erases the reward, and maiming is separate | Different; not measured |
| Crowd favour = money | **None** | An open question: does honour take its place? |
| Getting harder as you win + losing on purpose | **None** | Deliberately not taken |
| Staff + a research tree | The school/facility tree (§10) | We narrowed it: no staff, facilities yes |
| Fixed regional champions | Bounty contracts | A similar role, a different frame |
| A single final at the end of the year (15 opponents) | **None** | An open question: what is the end of the campaign? |
| Freeing / escaping | **None** | An open question: is the seppuku threshold its counterpart? |
| Death is permanent | The same | Shared |
| The name pool from chat | The same (phase 5) | Shared |

## 19. To be verified by playing

The video **closed** these questions in the first pass: the calendar is 365 days (`Days Left 364`
in the first frame), the full field list of the stat panel, the staff's daily food/water
consumption, what the coal pit does (it shortens **agility's training time**), the node names and
price scale of the Doctore tree, the fields of the fight contract, and that crowd favour is **a
separate reward line**.

What remains open:

1. How many jobs can be done in a day? A fight has a "6 days" participation cost, but how do
   shopping, research and training order themselves within the same day?
2. Is the yield threshold really 20%; and what happens to someone trying to yield in a fight with
   `Surrender Allowed: No`?
3. How does a permanent impediment appear on screen, and which stat does it lower by how much?
4. How is crowd favour computed — duration, the number of strikes, or deaths?
5. Is the difficulty scaling a visible number, or is it only felt?
6. The full list of the Jupiter cards and the size of their effects (we have 4 of them).
7. What chat can really vote on on the Twitch side — there is still not a single screenshot.
8. The economy's real curve: how much gold comes in and goes out in the first 10 days? (In the
   video the purse falls from 1000 to 680, then rises to 954 with a fight — a single sample.)
9. **How many staff slots are there?** The video says 3 (2018), a newer guide says 6 + a 1500-gold
   expansion.
10. **Is the Architect really unrehirable?**
11. How does the secret-selling / blackmail system unlock and what does it give?
12. What is the conversion between `Level` and `Points`? (E.g. Agility level 13 = 62 points.)
13. What is the difference between the class research (400 gold) and a class that arrives as a
    fight reward?

---

*This file will be updated by playing and by further video review; at every update the marks
(**[K]/[T]/[?]/[V]**) should be revisited.*
