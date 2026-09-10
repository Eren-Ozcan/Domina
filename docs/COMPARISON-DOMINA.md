# An item-by-item comparison with Domina

This file answers three questions: **what we did the same**, **what we deliberately did
differently**, and **what we never did at all** — and which of the last are decisions and which
are gaps.

The other side: *Domina* (Dolphin Barn, 2017). The system breakdown is in
`REFERENCE-DOMINA.md`, the screen breakdown in `REFERENCE-DOMINA-UI.md`. Our side:
`docs/GDD.md` + the code as it stands today (2026-09-05).

Every row carries a **status** mark:

| Mark | Meaning |
|---|---|
| ✅ | We have it and it works (in code, tested) |
| 🟡 | Decided, no code or half of it |
| 🔵 | **Deliberately not taken** — the rationale is written down |
| ⚪ | **A gap** — neither a decision nor code; unconsidered or in limbo |

---

## 1. The frame and time

| Topic | Domina | Ours | Status |
|---|---|---|---|
| Time flow | **Real time**, pausable; the day flows on its own | A **discrete day**; nothing happens until the player makes a decision | 🔵 |
| Calendar | A **365-day** countdown, `Days Left` on screen | There is a day counter, but **no upper bound** — the campaign does not end | ⚪ |
| A second clock | `Next Battle: n` — the days until the compulsory fight | None; the day's offer is regenerated every day and missing it costs nothing | ⚪ |
| The ending | A **final championship** at the end of the year, 15 opponents | None | ⚪ |
| Entry condition for the final | At least **3 regional champions** on the map must be beaten | None (there are bounty contracts but they are not a gate) | ⚪ |
| Difficulty tiers | Chosen from the menu (Pro-Gamer at the top) | None | ⚪ |
| Losing | When your champion dies the game is effectively over (not an official game over) | The dojo can close (1.2% in measurement) but there is **no narrative ending** | ⚪ |

> **The biggest gap is here.** All of Domina's economic pressure comes from the goal "in 365 days
> you will field an army of 15": every coin is an investment in that day. Our campaign has **no
> end**, so there is no compass for the question "shall I spend today or save for later". Our
> measurements run over 60 days, but what happens on day 60 is undefined. It does not even stand
> in the GDD as an Open Decision.

## 2. The roster and the warrior

| Topic | Domina | Ours | Status |
|---|---|---|---|
| Roster size | Up to **28** with building upgrades; crowding lowers morale | No limit; measurement targets 4 | ⚪ |
| Who goes on the expedition | The fight contract decides (1v1, 2/3, 15 men) | **At most 4**; an offer sometimes imposes an exact number | 🔵 |
| Stats | HP, Strength, Weapon, Defense, Agility, Meditate + derived Aggro/Turtle/Evasive/Stamina | **8 stats:** MaxHealth, Aggression, Defense, Evasion, Strength, Accuracy, MaxStamina, Speed | ✅ |
| What Strength does | **Strength = health + damage + resistance** (a single slider) | The three are **separate stats** (MaxHealth / Strength / Defense) | 🔵 |
| Behavioural tendency | Aggro / Turtle / Evasive as **open numbers**, by class | We have `Aggression`; the target-selection weights are in §4 — **there are no per-kind profiles yet** | 🟡 |
| Class | 8 classes (Murmillo, Thraex, Retiarius, Scissor, Velite, Sagittarius, Charioteer, Behemoth) | **No classes** (GDD §4) — identity comes from the weapon and the path | 🔵 |
| "Path" / specialisation | The class choice cannot be undone | **WarriorPath:** None / Blade / Stone / Shadow, not undoable | ✅ |
| Talent difference | The slave's starting stats | The `Talent` multiplier — how much he benefits from training | ✅ |
| Names | From chat (Twitch) or from a pool | A local pool for now, chat in phase 5 | 🟡 |
| Death | Permanent | Permanent | ✅ |
| Permanent impediment | There is an "impediment"; a maimed warrior can become useless | **Limb loss**, with sided penalties (the sword arm ×0.65, a leg ×0.55 evasion) — the warrior lives on | ✅ 🔵 |
| Morale | A Temperament bar; it **affects the stats**; raised with coin/wine/the bath/the bard | We have **honour** but **no morale** — there is no resource for a warrior's mood | 🔵 |
| Freeing | He asks for freedom after ~10 victories, and escapes if refused; a freed man returns for the final | None | ⚪ |
| Killing/selling | The `Put to Death` and `Sell` buttons | None — the only way to be rid of a warrior is death or seppuku | ⚪ |

## 3. Training

| Topic | Domina | Ours | Status |
|---|---|---|---|
| How | A **slider** per stat, running all day; `Level` + `Points` | A daily choice of **one drill**: `Strikes / Guard / Footwork / Conditioning` | 🔵 |
| The speed lever | The palus, stones, the coal pit, the bath + Doctore/Bard/Educator researches | The school nodes: the Training ground, the Inner dojo, the Kata master | ✅ |
| The ceiling | A stat ceiling raised by the Doctore researches | `FormsMaster` raises the ceiling | ✅ |
| Learning from fights | **The dominant route** — one fight equals 10 days of training; the victory screen says `+13 Agility` | **None.** A fight grants no stats; progress comes only from training days | ⚪ |
| A harmless training match | **Exhibition** — yielding allowed, no death; the main EXP source | None. Every fight is lethal | ⚪ |
| Automatic training | The `Enable Automatic Gladiator Training` checkbox | The drill stays selected and is applied when the day closes | ✅ |

> **The second big gap.** In Domina a warrior grows **by fighting**; training is its slow backup.
> In ours it is the exact opposite: a fight only wears him down. That makes the two games'
> tempos entirely different — theirs is "take the risk, get stronger", ours is "take the risk,
> get worn". Our side's rationale is not written down; the measurement finding that **the roster
> is the binding resource** (GDD §11) is probably a consequence of it.

## 4. The economy

| Topic | Domina | Ours |
|---|---|---|
| Money | One: coin | One: **gold** ✅ |
| Stock resources | Food, Water, **Wine**, Stone | Food, Water, **Medicine** ✅ 🔵 |
| The start | 1000 gold · 800 food · 400 water · 80 wine | **600 gold**, an empty store, 4 warriors 🔵 |
| Warrior price | Variable, from the Magistrate; they also arrive as rewards | **150 gold**, 10 candidates in the market, the price by stats+talent ✅ |
| Staff cost | A hiring cost of **13-100 gold** + daily food/water | No staff 🔵 |
| Armour/repair | A stepped price curve; the Faber upgrades for free | **1.50** per durability, repair **0.90/wear** ✅ |
| Daily consumption | Food/water per warrior + per staff member | 1 food + 1 water per warrior; 12 medicine for someone in the infirmary ✅ |
| Fight reward | Gold + food + water + wine + **slaves** | **Gold only** (enemy health × 0.45 + a risk premium) 🔵 |
| A "good fight" reward | **Crowd Favour** as a separate item (e.g. 73 gold) | None — the reward depends only on the outcome ⚪ |
| Gambling | **Betting** on pit fights (150 gold) | None ⚪ |
| Theft | The Agent steals weapons/armour | None 🔵 |
| Bargaining | The Emptor's discount, stacking with the Faber's | The `Broker` and `Steward` nodes ✅ |
| Scarcity | Drought/flood hit the supply; the well/store are insurance | Random events: theft, spoilage, the well, mouldy medicine, illness ✅ |
| Event frequency | Unknown | **15% a day**, five kinds, all of them subtract ✅ |

## 5. Combat

| Topic | Domina | Ours | Status |
|---|---|---|---|
| Resolution | Real time, inside the engine | An **engine-independent core**, seeded and deterministic | 🔵 |
| Intervention | With `Mind Control` researched, **one warrior is driven by hand**; the crowd dislikes it | None — combat is fully automatic | 🔵 |
| The single intervention | Surrendering with a mashed QTE | A **single "Flee" key** decision, conditionally unlocked, with an honour price | 🔵 |
| Yield threshold | `Automatic Yield`: automatic yielding **below 10% health** | The withdrawal decision is the player's; there is no automatic threshold | 🔵 |
| The right to yield | **`Surrender Allowed: Yes/No`** on the contract | Withdrawing is **always** available | ⚪ |
| Time limit | A countdown timer on screen (~3 min) | `BattleOutcome.TimeLimit` exists ✅ | ✅ |
| Block | Yes (a shield, `Shield Control`) | `CombatState.Blocking`, a decision derived from the Defence stat ✅ | ✅ |
| Charge | Not visible | The charge: distance 320, probability 0.40, windup 0.75 s ✅ | ✅ |
| Weapon drops | The `Disarming Weapon` skill | A 0.05 base on a strike landing on armour, the weapon is flung to the ground and can be picked up ✅ | ✅ |
| Sword catching | None | The **jitte/sai** bind ✅ | ✅ (extra on our side) |
| Poison | None | With dose/tick/lifetime rules ✅ | ✅ (extra on our side) |
| Stunning | None | The blunt weapons' return ✅ | ✅ (extra on our side) |
| Projectiles | The `Throw Weapons` skill, the Sagittarius class | `ThrownWeapon` in a separate slot, with the flight time modelled ✅ | ✅ |
| Armour | Slot by slot, weight burns stamina; `A/D/kg` on screen | **Six slots**, weight stretches the attack cycle, wear accumulates and a piece **breaks** ✅ | ✅ |
| Dismemberment | The `Dismemberment` skill — mostly death gore | **Living on maimed** — at the centre of the system 🔵 | ✅ |
| Obstacles on the field | `Obstacles: Lions / Tigers / It's a mystery` — written on the contract | None ⚪ | ⚪ |
| The opponent | Human gladiators + beasts | **Human** — a rival school's men (collector, cutthroat, duelist, kabukimono, senior student) | ✅ |

## 6. Fight types

| Domina | Ours | Status |
|---|---|---|
| A scheduled fight (arranged by the Legate/Magistrate, it eats days) | **The day's offer** — take it or leave it, entering eats a day | ✅ |
| A pit fight (the opponent is invisible, there is betting) | None | ⚪ |
| An exhibition (a deathless training match) | None | ⚪ |
| Regional champions (9 fixed opponents, a gate to the final) | **Bounty contracts** — timed, with an honour reward, and accepting does not eat a day | ✅ 🔵 |
| Chariot race / Beast mode / Gravitas | None | 🔵 |
| The final championship | None | ⚪ |

## 7. The management layer

| Topic | Domina | Ours | Status |
|---|---|---|---|
| Structure | **Staff** (14 roles) + a research tree for each | **A school tree**: 9 nodes, three branches (training / health / economy) | 🔵 |
| Slot limit | 3 (2018) or 6 staff at once; a fired staff member's bonus **goes** | Once a node is bought it is **permanent** — no undo, no slots | 🔵 |
| Cost | Staff 13-100 gold + **daily stock**; research gold + turns + stock | A node costs **gold only**; the order within a branch is compulsory | 🔵 |
| Buildings | The palus, the bath, the well, storage, the wine cellar, a private room, walls | Abstract nodes (Training ground, Infirmary...) — no physical buildings | 🔵 |
| Build time | An hourglass (turns) | Instant | 🔵 |
| NPC relationships | The Legate + the Magistrate: wine bribes, patronage, **selling secrets, blackmail** | None — nobody "arranges" the offers | ⚪ |
| Patronage | An NPC pays a warrior's **food/water until he dies** | None | ⚪ |
| The card system | Jupiter blessings: dragged onto a warrior/staff member, sold, held in hand | None | 🔵 |

## 8. Chat / the crowd

| Topic | Domina | Ours | Status |
|---|---|---|---|
| Connection | The stream name in the settings; `domina_bot` joins the chat | A platform-independent adapter (Twitch + Kick land on the same internal event) | 🟡 |
| Participation | A gladiator named after a viewer | **The same model**: everyone included, `!no` opts out | 🟡 |
| Voting | Votes on events | **The seppuku vote** (`SeppukuArbiter`), the honour commands | ✅ |
| Economic effect | A reward increase based on viewer reaction (a weak source) | **Honour → the reward multiplier** (GDD §6) | ✅ |
| Play without a stream | With the integration off the game is the same | **The AI crowd is fully equivalent** (GDD §9) | 🔵 |
| Crowd money | Crowd Favour — payment for the fight's *spectacle* | None; the crowd only speaks through honour | ⚪ |

## 9. What we have and Domina does not

These are not "missing", they are **our additions** — all of them in the code and tested:

1. **An engine-independent, deterministic core.** Balance is measured by simulating tens of
   thousands of fights (`Domina.Sim`). Domina has no such lever; its balance numbers are
   discussed through players' guesses.
2. **A measured economy.** Runs of 1000 dojos × 60 days; the rationale for the prices is
   measurement.
3. **Sword catching (jitte/sai), poison, stunning** — three separate weapon identities.
4. **Armour wear and pieces breaking in the middle of a fight.**
5. **Limb loss continuing with survival** and sided penalties (the sword arm / the off arm / a
   leg kept separate).
6. **The honour system** — 0-100, with decay, tied to the reward multiplier and the seppuku vote.
7. **Contract honour:** if a bounty is accepted and not returned from, the roster loses honour.
8. **The path choice** (Blade / Stone / Shadow) — an irreversible specialisation instead of a
   class.
9. **A versioned, merge-on-load save** and showing the warnings to the player.

## 10. A number-by-number summary

| Measure | Domina | Ours |
|---|---|---|
| Starting purse | 1000 | **600** |
| Starting roster | 3 | **4** |
| Starting store | 800 food / 400 water / 80 wine | **empty** |
| Campaign length | 365 days | **unlimited** (measurement over 60 days) |
| Expedition party | 1-15 by contract | **at most 4** |
| The market | 10 candidates? (unknown) — from the Magistrate | **10 candidates**, refreshed every day |
| Warrior price | variable | **150 gold** base |
| An ordinary research | ~20-70 gold + 6 turns | A school node: gold, no duration |
| Unlocking a class | **400-500 gold, 16-17 turns** | — (no classes) |
| Yield threshold | **10%** (20% with an upgrade) | the player's decision |
| Fight duration | a ~3-minute timer | `TimeLimit` exists |
| Event frequency | unknown | **15% a day** |

---

## 11. The work list that comes out of this

The following are proposals; none of them is a decision. Ordered **by impact**.

### A. The campaign has no end (the biggest gap)

All of Domina's tension comes from the 365-day countdown. We have a day counter but no goal.
Three options: (1) a fixed-length season + a closing fight, (2) open-ended but rising difficulty,
(3) turning the bounties into a gate — "three heads = the last contract". Our measurements
already run over 60 days; **what happens at the end of those 60 days must be defined**.

### B. Why does a fight teach nothing?

In the reference, what grows a warrior is fighting. In ours a fight only wears him down and
growth comes only from training days — that is, **going on an expedition is pure loss**, with no
reason beyond the reward. Is that a deliberate decision, or was it never discussed? There is no
rationale in the GDD. The measurement finding "the roster is the binding resource" may come
directly from here.

### C. There is no harmless fight

In Domina an exhibition is training, income and a door to "try the new man" at once. In ours
every fight is lethal, so there is **no safe way** to try a recruit. Training does not take its
place, because training is not a decision but a wait.

### D. There is no resource called morale

Honour is the warrior's **reputation**, not his mood. In Domina morale affects the stats directly
and "buying morale with money" produces a small but constant decision. In ours the only thing
that can be spent on a warrior is equipment.

### E. There is no way to be rid of a roster member

In Domina a surplus/maimed warrior is sold, freed or killed. In ours he only dies. A maimed
warrior stays on the roster forever and eats food — that is not a decision, it is a leak.

### F. A "good fight" is not rewarded

Crowd Favour separates winning from *fighting well*. In ours the reward depends only on the
outcome; the honour system partly takes its place but does not turn into **money**.

### G. The right to yield could be tied to the contract

`Surrender Allowed: No` is a one-line rule but it changes the character of a whole fight. Our
withdrawal key is always available; some contracts turning it off would add weight to the
bounties.

### H. What will not be taken (deliberately)

- Driving a warrior by hand (mind control) — GDD §1.
- Getting harder as you win + the lose-on-purpose exploit — it punishes the player's success.
- Staff + slot economy — the school tree took its place, with less micromanagement.
- The card/blessing system — a second layer of randomness; our randomness is already in the daily
  events and in the fight.

---

*The update rule: this file is updated together with `REFERENCE-DOMINA*.md` or the GDD whenever
they change. Every ⚪ row must turn either into a GDD decision or into a deliberate "we are not
taking this" line.*

---

> ✅ **Processed (2026-09-07).** The `# 12` and `# 13` sections below **have been written into
> `docs/GDD.md` and `docs/ROADMAP.md`.** From now on the GDD is the single source of truth; these
> two sections stand as a **historical record** — showing which decision was made on what grounds
> and what the reference game did. A new decision is written not here but in the GDD.

# 12. What changed — the line-by-line decision pass (2026-09-05)

This section holds the decisions made while the tables above were reviewed one by one. What is
above is **left as it stands** (a historical record); the current decision is what is written
here. In case of a conflict **this section governs**. When the pass is finished it will be
written into the GDD.

## Section 1 — The frame and time ✔ completed

| Row | Old state | New decision | Note |
|---|---|---|---|
| Time flow | A discrete day 🔵 | **Real time + pause** (the Domina model) | The core keeps advancing on a fixed tick and determinism is preserved; real time only means the clock driving the tick. The day-close/event code that assumes a discrete day will be rebuilt. A pause-or-window rule is needed for the chat vote. |
| Calendar | No upper bound ⚪ | **A fixed countdown**, with the days left on screen | The length will be decided in Section 10 (the measurements run over 60 days). |
| A second clock | None ⚪ | **A compulsory fight counter** (`Next fight: n days`) | Missing it has a price; it closes the exploit of endless safe training. The kind of penalty and the number n will be set separately. |
| The ending | None ⚪ | **A final tournament** — the last day, knockout, the whole roster | The season's goal is roster breadth and depth. The number of opponents will be set. |
| Entry condition for the final | None ⚪ | **A bounty gate** — the final cannot be entered until n heads are taken | It gives the existing bounty system a purpose. n (3 in Domina) will be set. |
| Difficulty tiers | None ⚪ | **A tier choice**: Apprentice / Master / Legend | "Master" is the balance measurement's baseline; the other tiers derive from it with multipliers, not from separate measurement runs. |
| Losing | No narrative ending ⚪ | **A clear ending: the dojo closes** | When the treasury and the roster are gone, a closing screen + a season summary (days, victories, the number of dead). With a countdown and compulsory fights, losing is a real possibility. |

### The new work Section 1 brings
- The move to real time: the existing discrete-day architecture (the day's close, event
  triggering, the chat vote) has to be redesigned — it will enter the ROADMAP as a risk.
- The final tournament, the compulsory fight counter, the tier multipliers, the closing screen:
  all four are new systems.
- Open numbers: the season length, the compulsory fight interval and its penalty, the number of
  heads gating the final, the number of opponents in the tournament.

## Section 2 — The roster and the warrior ✔ completed

| Row | Old state | New decision | Note |
|---|---|---|---|
| Roster size | No limit ⚪ | **A tiered cap**, unlocked with dojo upgrades (starting at ~6) | Growing the roster becomes an investment decision; accumulating depth for the final tournament gains meaning. Every tier brings gold and a daily stock load. |
| Who goes on the expedition | At most 4 🔵 | **The same: at most 4** | The fight scene stays readable; the balance measurement was made on this assumption. In the final tournament depth is useful as a reserve. |
| Stats | 8 stats ✅ | **A 9th stat: Will** is added | Seppuku resistance, the panic threshold, honour gain. The counterpart of Domina's Meditate. |
| What Strength does | The three are separate 🔵 | **The same: the three stay separate** (MaxHealth / Strength / Defense) | So that the training choice stays meaningful: a warrior who is durable but not a striker is possible. |
| Behavioural tendency | A single `Aggression` 🟡 | **Three tendencies, as open numbers**: Aggressive / Defensive / Evasive | Visible on screen for both warriors and adversaries. Because combat is fully automatic, the player being able to read the fight in advance is critical. |
| Class | No classes 🔵 | **A class system is added** — unlocked, assigned, not undoable | It exists alongside the path: **Path = stat tendency, Class = role** (weapon + behaviour). The GDD §4 decision "no classes" is void. |
| Path / specialisation | 3 paths, not undoable ✅ | **The same** — but on being maimed the **class** is chosen again, never the Path | The path is earned with 20 training days; maiming must not erase that work. A man who loses an arm does not lose his agility — what changes is how he fights. |
| **Class change on being maimed** (a new row) | — | **Limb loss reopens the class choice**; the lost limb makes some classes impossible and the player picks among the rest | Limb loss stops being a "leak" and turns into a **second career**. Not fate, but a narrowed choice. |
| Talent difference | The Talent multiplier ✅ | **Talent + different starting stats together** | Two axes in the market: the one who is strong now, or the one who will be strong later. With a countdown running, the question "do I have time to train him" is born. |
| Names | Pool → chat in phase 5 🟡 | **The same plan** | Play without a stream stays fully equivalent. |
| Death | Permanent ✅ | **Permanent + an inheritance** | **Only two things** carry over: (1) **his equipment** — his weapon and armour return to the dojo store, **but only if the fight is won**: someone has to survive to carry the body. On a one-man expedition a dead warrior's equipment stays on the field too. Going out in numbers gains a measurable return; 1v1 contracts carry equipment risk. (2) **His honour and title** — written on the dojo wall, a small permanent honour increase, visible in the season summary. What does not carry over: the **class** (450 gold) and the **path** (20 training days) — both are lost entirely, so death stays heavy. The rationale: in this pass the warrior became expensive (class + path + morale); pure permadeath would have made the player avoid taking the field — GDD §10's own warning was now working against us. |
| Permanent impediment | Limb loss, sided penalties ✅🔵 | **The raw penalties stay exactly as they are** (the sword arm ×0.65, a leg ×0.55); the compensation is the **class change** | The penalty is not softened — the loss stays real, but there is a way out. |
| Morale | None 🔵 | **A morale resource is added**; tied two ways to Will | High Will → morale falls slowly, he is not easily broken after a defeat. Low morale → the checks that lean on Will (the seppuku risk, the panic threshold) shift against him. Morale is short-term mood, Will is long-term endurance. |
| Freeing | None ⚪ | **Retirement: he becomes a master** | A warrior with many victories or heavily maimed leaves the field and becomes an instructor: a permanent bonus to training speed, no more daily food load, never takes the field again. Transformation instead of loss. |
| Killing / selling | None ⚪ | **Only honourable exits**: seppuku, retirement, seeing him off | A student is not property — he is not sold or killed. The leak "a maimed warrior eats food forever" is closed by retirement. |

### The new work Section 2 brings
- **The class system**: the class list, the unlock cost, the limb-class fitness matrix, its
  interaction with the Path.
- **The Will stat** and **the morale resource**: two new systems, tied to each other; they enter
  the seppuku and panic checks.
- **Showing the three behavioural tendencies** on screen + the per-kind adversary profiles.
- **The inheritance**: what carries over from a dead warrior and how it is shown.
- **The retirement / seeing-off** flow and the roster cap upgrade node.
- GDD §4's "no classes" rationale and §10's "the warrior side is kept shallow" rationale are
  **now void** — both have to be rewritten.

## Section 3 — Training ✔ completed

| Row | Old state | New decision | Note |
|---|---|---|---|
| How training works | One drill a day 🔵 | **The same: a single drill choice** (Strikes / Guard / Footwork / Conditioning) | A slider is a setting, a choice is a decision. In the real-time flow it continues as a "drill block". |
| The speed lever | Abstract school nodes ✅ | **Physical facilities** — buildings and equipment visible in the dojo | Like the training post, stones and the bath; the dojo growing is seen on screen. The abstract node logic (the order within a branch, paying up front, no selling back) is preserved; what changes is the presentation and the build cost. |
| The ceiling | `FormsMaster` raises it ✅ | **The same: a ceiling raised by a facility** | The stat gain from fights hits the same ceiling — without investing in the dojo a warrior cannot grow past a point. The two systems lock each other. |
| Learning from fights | None ⚪ | **A fight grows him** (the Domina model) — a fight grants stats faster than training | The tempo reversed: it is now "take the risk, get stronger". The compulsory fight counter becomes an opportunity, not a punishment. The victory screen shows the stat gain. **The measurement finding "the roster is the binding resource" is invalidated by this change — it has to be measured again.** |
| A harmless training match | None ⚪ | **An in-dojo sparring match** — your own warriors against each other | No income, no death; there are wounds, stat gains and a morale effect. The same resolver is used and no new opponent content is needed. The safe way to try a recruit and a maimed warrior's new class. |
| Automatic training | The drill stays selected ✅ | **The same** | Natural in a real-time flow: the dojo works while the player is busy with something else. No separate "automatic mode" checkbox is needed. |

### The new work Section 3 brings
- **Stat gain from fights**: the gain formula, its display on the victory screen, its interaction
  with the facility ceiling. The existing balance measurements are void with this change — they
  have to be rerun.
- **The in-dojo sparring match**: a matchmaking screen, a deathless mode, morale and wound
  outcomes.
- **Physical facilities**: the visual counterpart of the nine school nodes; the dojo screen
  becomes a growing place.

## Section 4 — The economy ✔ completed

| Row | Old state | New decision | Note |
|---|---|---|---|
| Money | One: gold ✅ | **The same: one currency** | Everything is measured on one axis. |
| Stock resources | Food / Water / Medicine ✅🔵 | **The same** ⏳ *not settled* | Because the morale system arrived, sake (Domina's wine) will be reconsidered as a fourth stock. It will be looked at once morale settles. |
| The start | 600 gold, an empty store 🔵 | **Start with a full store** (the Domina model) | 600 gold + food/water/medicine. The countdown already applies pressure; the opening does not also have to choke. The exact numbers will be measured again. |
| Warrior price / the market | 150 gold, 10 candidates ✅ | **The same** | The price formula will now reflect both axes at once (the stats + Talent), depending on the Section 2 decision. |
| Staff | None 🔵 | **A staff system is added** — every profession can be hired or filled with a retired warrior | A daily wage + stock, and you can cut it any day. A retired warrior takes no wage. **A retired warrior is weak in non-combat roles** (better than a hire at the drill/kata/weapon master, half efficiency at the physician/smith/bard). A draft of 14 professions was produced. |
| If a facility is empty | — (new) | **It works at half efficiency** | The build investment is never wasted; staff bring it to full efficiency. Some doors still require staff (a smith is required for ō-yoroi). |
| Armour / repair | A unit price + repair ✅ | **Tiered armour**: Leather → Lamellar → Ō-yoroi | The tier works on the balance **protection ↑ weight ↑** (weight burns stamina and stretches the attack cycle — the existing system). The upper tiers require **a forge + a smith**, or they are not found in the market. The wear/repair economy is preserved. |
| Daily consumption | A fixed amount per head ✅ | **The same** | This is the real price of raising the roster cap: linearly growing expenditure. |
| Fight reward | Gold only 🔵 | **Gold + stock** (the Domina model) | Food/water/medicine drop too. Store pressure becomes the second reason to go on an expedition; a hungry dojo is forced to take the field. |
| A "good fight" reward | None ⚪ | **Spectacle enters the honour multiplier** | No separate "crowd payment" item is added; a close fight raises honour quickly, and honour is already the reward multiplier — so it turns into money indirectly. The existing system deepens instead of a new one being built. |
| Gambling | None ⚪ | **The same: no betting** | Gambling offers a shortcut to a player who plays badly and breaks the balance measurement. |
| Theft | None 🔵 | **Mutual, but as an event** | We do not hire an agent to steal; but as honour rises the probability of a theft event rises ("your fame attracts thieves"). Honour now carries a cost too. |
| Bargaining / discounts | A permanent node discount ✅ | **The facility is permanent, the staff member is a multiplier** | A building is bought once and gives its small discount permanently (×0.90); with a staff member on it, the full discount (×0.75) but a daily wage runs. In a crisis you cut the staff and keep the building — for the first time the economy gets a **gear**. Because a retired warrior takes no wage, he becomes the long game's reward. |
| Scarcity | Five event kinds ✅ | ⏳ **not settled** | Seasonal scarcity (drought/winter) will be reconsidered later. |
| Event frequency | 15% a day, all subtract ✅ | **Let the events offer a choice** | An event becomes a choice rather than a notification: "hungry villagers at the gate — give (honour +) / refuse (the stock is kept)". It is wired directly into the chat vote. The frequency number will be measured again. |

### A terminology change
The term "school node" has been retired. From now on: a **Facility** = a physical building that is
put up (paid up front, permanent, not sold back) · **Staff** = the person who runs that building
(a daily wage, can be cut; hired or a retired warrior).

### The profession draft (14 roles)
Drill master · Kata master · Weapon master · Physician · Bone setter · Smith · Steward · Broker ·
Fixer · Bard · Monk · Cook · Groom · Diviner.
In the first three (drill / kata / weapon master) a retired warrior is **better** than a hire; in
the steward / broker / fixer / monk / groom roles he is **medium**; at the smith and the bard he
can work but is **weak** (half efficiency).

⏳ *not settled:* a retired warrior cannot be placed in the **physician, bone setter, cook or
diviner** roles at all — those four professions **require outside staff**. The smith and the bard
were kept out of this rule (a retired warrior can cover them, weakly). The decision is not
settled and will be revisited once the staff economy settles.

### The new work Section 4 brings
- **The staff system**: 14 professions, a daily wage, hiring/firing, placing retired warriors, an
  efficiency multiplier per role.
- **Tiered armour**: three tiers × six slots, the forge gate, the weight balance.
- **Events that offer a choice**: the option texts and outcomes per event, the chat vote
  connection.
- **Stock in the fight reward**: the reward formula is rebuilt.
- All the economy numbers (600 gold, 150 gold, the ×0.45 reward, the 15% events) **have to be
  measured again** — fights granting stats and real time changed those measurements'
  assumptions.

## Section 5 — Combat ✔ completed (except the Opponent row)

| Row | Old state | New decision | Note |
|---|---|---|---|
| Resolution | An engine-independent core 🔵 | **The same: an engine-free, seeded deterministic core** ⏳ *may be revisited* | Real time means running the core's fixed tick (`TickSeconds` 0.05) in the visualisation: pausing = not calling `Step()`, 2x = two steps per tick, smoothness = interpolation between ticks. A variable `dt` was rejected: determinism would hang on the frame rate and the sim and the game would give different results. Resolving in the engine does not give visual quality (the visualisation gives that), only unpredictable physics — in an automatic fight that chat influences, that reads as unfairness, and measurability is lost in exchange. **The user reserved the right to reopen this row later.** |
| Intervention | None ✅ | **The same: fully automatic** | No driving by hand and no in-fight orders. All the player's control is in the preparation (the party, the kit, the drill); his only decision in the fight is withdrawal. |
| The single intervention | A single "Flee" key ✅ | **The same** | One conditionally unlocked key, with an honour price. No QTE — it is a decision, not a test of dexterity. |
| Yield threshold | No automatic threshold ✅ | **The same: no automatic yield** | A warrior fights until he dies; the withdrawal decision is the player's. The Will stat was not tied to this row. |
| The right to yield | Withdrawal always available ⚪ | **The same: available in every contract** | A contract field like `Surrender Allowed` will not arrive. The rule is single and the price is always the same: honour. |
| Time limit | `BattleOutcome.TimeLimit` ✅ | **The time limit goes away** | No timer; a fight lasts until someone falls, withdraws or yields. What manages a long fight now is pausing/speeding up. |
| Block | A decision derived from the Defence stat ✅ | **It splits into two axes: stat + equipment** | The Defence stat gives the block's **frequency** (`MaxBlockChance` 0.45, no base — the locked number is kept); the weapon gives the block's **quality**; the armour gives **how much is absorbed** when the block does not hold. There is **no** hand shield — a samurai's two hands go to the weapon and defence is written into the armour. Instead the **ō-sode** enters the shoulder slot: it gives no block chance (a passive piece, not a move) but magnifies the effect, and its price is weight. It sits naturally on the tiered armour decision (Leather → Lamellar → Ō-yoroi). A real shield (**tate**) enters the field, not the arm: in the "screened position" field feature it protects against projectiles and is useless in melee. The price was accepted: a shield reads at a glance and an ō-sode does not — it will be covered by exaggerating the silhouette. |
| Charge | Distance 320 / 0.40 / 0.75 s ✅ | **The numbers stay, the charge becomes visible** | During the windup the warrior is marked on screen; the viewer sees the future in advance. A moment of tension for chat, no change for the resolver. |
| Weapon drops | A 0.05 base on a strike on armour, for everyone ✅ | **The base stays for everyone, specialisation magnifies it** | The class, the Path or the weapon type (jitte/sai) raises the chance; nobody is zeroed. Domina's model of "without the skill you can never disarm" was rejected. The measured finding is preserved: the rule's price is set by the **direction** of the drop (behind the opponent), not the distance. |
| Sword catching | The jitte/sai bind ✅ (extra on our side) | **The same, the numbers locked** | Two open measurements go to phase 9: the bind's value **to the team**, and what the sai's high catch volume is worth when outnumbered. With blocking added, the jitte's advantage had dropped from 2.98 to 0.42 points; that will be measured again too. |
| Poison | With dose/tick/lifetime rules ✅ (extra on our side) | **The same** | Open to everyone, no honour price, no contract ban. The measured finding is preserved: the real knob is the dose **cap**, not the lifetime. |
| Stunning | The blunt weapons' return ✅ (extra on our side) | **The same** | The blunt weapon's identity is going through the block (`BlockStunShare` 0.75). The ō-sode does not change this balance: armour absorbs damage, not concussion. |
| Projectiles | `ThrownWeapon` in a separate slot, for everyone ✅ | **Thrown weapons stay the same + the `yumi` (bow) becomes a class** | Shuriken/tantō stay open to everyone. The bow enters as a **two-handed** weapon tied to a class: superior at range, helpless up close. It raises the stakes of the off-hand slot decision (a bow = two hands), gives the ō-sode a real job against arrows, and fits the theme exactly (for a samurai the bow was the primary weapon). |
| Armour | Six slots, weight, wear, breakage ✅ | **The mechanics stay the same; `A / D / kg` comes to the screen** | The only thing taken from Domina is the display: the armour value / durability / weight are written openly on every piece, and the player makes the trade with his eyes open. The total weight's effect on the attack cycle is read on the same screen. |
| Dismemberment | Living on maimed 🔵 | **The same** | Severing is not a skill, it is open to everyone; not a death but a fate-changing event. A block zeroes it (`BlockDismembermentShare` 0) — the only certain promise the Defence stat makes. Section 2's limb-class fitness matrix hangs on this. |
| Obstacles on the field | None ⚪ | **Field features are added (not a live third party)** | Fog (accuracy/range drop), mud (speed/evasion drop, an extra penalty for heavy armour), a narrow bridge (a numbers advantage does not apply), night (projectiles are weak), a screened position (tate panels). They arrive written on the contract card; they enter the existing resolver as multipliers and need no new AI. |
| The opponent | Human — a rival school's men | **Settled 2026-09-10: human only, no monsters** | Open Decision #16 is closed. The story (`STORY.md`) makes every fight part of one rival school's protection racket and of the same 180-day clock; a creature encounter belongs to neither. The five kinds kept the yokai templates' numbers and changed only their identities, so no measurement was invalidated. This costs us the old "we fight monsters, Domina fights people" differentiator — what carries it now is the contract economy, the chat layer and the depth-testing final. |

### The new work Section 5 brings
- **The ō-sode and splitting the block axis**: scaling `BlockDamageReduction` from the armour
  piece, the shoulder slot's relation to blocking, the weight balance.
- **The `yumi` class**: a two-handed ranged weapon, flight/range behaviour, a penalty at close
  range, the arrow ammo economy.
- **Field features**: five field kinds, their display on the contract card, their entry into the
  resolver as multipliers.
- **Removing the time limit**: dismantling the `BattleOutcome.TimeLimit` route and reviewing the
  tests that rest on it.
- **The visible charge and the `A/D/kg` armour panel**: presentation work, it does not touch the
  core.
- Measurements carried over to phase 9: the bind's value in a team, the sai when outnumbered, the
  jitte/katana difference after blocking.

## Section 6 — Fight types ✔ completed

| Row | Old state | New decision | Note |
|---|---|---|---|
| Scheduled fight | "The day's offer" — take it or leave it ✅ | **A timed offer queue** | Instead of a single daily offer, several offers stand posted at once, each with its own expiry. Going on an expedition eats real time (travel + fight). The decision becomes not "which shall I take" but **"which can I get to"** — with the discrete-day model gone, the offer being daily had become meaningless. |
| Pit fight | None ⚪ | **A blind fight enters, betting does not** | A high-reward contract type in which the opponent is unknown: you decide without knowing what kit to take. Section 4's "no gambling" decision is preserved — uncertainty becomes a risk decision, not shortcut money. |
| Exhibition | None ⚪ | **Only the in-dojo sparring match** (accepted in Section 3) | A deathless show fight against outsiders **will not be opened**. The risk threshold stays clear: inside the dojo is safe, every fight that goes outside is lethal. Domina's exhibition was already the door to the "lose on purpose" exploit. |
| Regional champions | Bounty contracts ✅🔵 | **Fixed-name story targets** + the bounty frame | The main story characters are **the same in every game**: their identity and stats fixed, so they can be memorised and prepared for (as in Domina). Ordinary bounties keep being generated in between. The simple story to be written later will sit on this backbone. |
| A defeated target | — (new) | **It does not get stronger, it grows** | If you lose an intermediate story fight, the target's stats **do not change** (the memorisation is preserved); men are added beside him — a defeated senior gathers men (defeat 1: +2 collectors, defeat 2: +4). The difficulty rises but the counter-move stays open: the roster, the field and the kit chosen against a crowd. A stat multiplier was rejected because an uncapped multiplier makes the target unreachable and quietly ends the run. |
| The night raid | — (new) | **A defeat triggers it** | Losing a story fight makes the dojo a target: the enemy who beat you can raid at night within a few days. Unprepared, the store is looted and the wounded in the infirmary die; prepared (a warrior on watch + a wall facility), a fight starts — but the roster is tired and the kit is half. A defeat does not stay outside, it comes home. It fits the existing decisions: the wall/gate is a facility branch, the watchman is a staff role, and "post a watch / do not" is an event that offers a choice. |
| Chariot race / Beast mode / Gravitas | None 🔵 | **The same: no special events** | Mini-games that need their own rule sets will not be opened. Variety comes from the contract types and the field features; everything uses the same resolver. |
| The final championship | None ⚪ | **Yes — and losing ends the game** | The campaign's end is a single final fight. **Losing the final = game over**, definitively. This is where we depart from Domina: there, losing the final is the year ending (the loss condition is not official, "if you lose your best gladiator it is practically over" **[T]**), while for us it is the run ending. Losing has to be real — the game does not carry the player to a win. |

### The new work Section 6 brings
- **The offer queue**: several offers at once, an expiry per offer, the expedition's duration
  being deducted from real time.
- **The blind contract**: an offer type in which the opponent is hidden, and its reward multiplier.
- **Fixed story targets**: named adversary definitions (identity + fixed stats), their connection to
  the bounty system, story progress state.
- **Growth after a defeat**: building the horde beside a target according to the number of
  defeats.
- **The night raid**: the trigger rule, dojo defence (a watch staff member + a wall facility), the
  unprepared loss table (the store, the infirmary), the raid fight starting with a tired,
  half-equipped roster.
- **The final fight and game over**: the run's ending screen, terminating the save flow.

## Section 7 — The management layer ✔ completed

| Row | Old state | New decision | Note |
|---|---|---|---|
| Structure | A school tree: 9 nodes, three branches 🔵 | **A facility tree + staff; some professions get their own upgrade branch** | The three-branch × three-tier facility tree stays (with Section 4's terminology: a facility is the building, staff are the people who run it). Domina's "every staff member has his own research tree" model is **not** taken wholesale — 14 separate trees are not an economy but filling in a table. But part of it is taken: a few selected professions carry their own 2-3 tier upgrade. **Which professions will be decided in a separate pass, going through the 14 roles one by one.** |
| Slot limit | Once bought a node is permanent, no slots 🔵 | **There is no fixed slot count** | A facility is permanent and not sold back (the GDD decision is preserved). For staff, the natural cap is the number of facilities, one person per building; the real constraint is the **daily wage**. Domina's "3/6 staff at once" is an artificial cap; in ours the constraint is set by the economy itself, and cutting staff in a crisis is already the gear opened in Section 4. A cut staff member's bonus goes and his building stays — the same place as Domina's Architect rule. |
| Cost | A node costs gold only 🔵 | **A facility costs gold up front; staff cost a daily wage + stock per head** | Research/turn costs and a second resource (Domina's stone) do not enter — the single-currency decision is preserved. Staff consume food/water too: like the roster cap, growing the management has a linearly growing cost. |
| Buildings | Abstract nodes 🔵 | **Physical buildings, a fixed layout** | An unlocked facility becomes a visible building on the dojo screen. Free placement / adjacency bonuses are **not** included — we are not opening a layout game. The night raid decision had already made the wall and the gate physical; the rest of the facilities staying abstract would be inconsistent. Presentation work, it does not touch the resolver. |
| Build time | Instant 🔵 | **It takes time; staff are not made busy** | Domina's hourglass is taken: after the gold is paid, the facility comes up over a set time, and that time competes with the decision to go on an expedition. Domina's "the staff member doing that job cannot do anything else meanwhile" is **not** taken — for us staff exist to run a facility, and adding a busy state to track is not worth it. There is also no speeding it up with gold. |
| NPC relationships | None ⚪ | **We start with three NPCs; relationships yes, intrigue no** ⏳ *a rival dojo and the village later* | **The regional lord** (the owner of the offer queue), **the merchants' guild** (market prices, whether upper-tier armour is on sale, stock during a scarcity), **the temple** (the omamori supply, honour compensation through a funeral rite). For each a single number, five tiers: Hostile / Cold / Neutral / Pleased / Loyal; only that number goes into the save. **What raises it:** finishing a contract on time (large), a gift (small and with diminishing returns — loyalty is earned by work), a decision in that party's favour during an event, honour rising (all of them at once, very small). **What lowers it:** losing or withdrawing from a contract taken from them, never taking their offer until it expires (it accumulates), a decision against them. **Not taken:** selling secrets, blackmail, arranging a fight's outcome — the fairness of an automatic fight has to stay readable. A rival dojo and the village (and the opposite-pole model where relationships spoil each other) are out for now; they will be looked at once the economy and the staff settle. |
| Patronage | None ⚪ | **It does not enter** 🔵 | An NPC does not take on a warrior's costs. In Domina this was exploited by cutting the roster to two men and having both NPCs become patrons; in ours the daily consumption pressure is the economy's backbone (Section 4) and no exemption that dilutes that pressure will be opened. |
| The card system | None 🔵 | **It enters as omamori** | The Jupiter blessings are taken with a thematic adaptation: an **omamori (a temple charm)** is fitted to a warrior or a staff member, removed and given to someone else, and can be sold. The temple relationship sets its supply and strength — the charm system is the counterpart of the NPC layer, not a stray economy item. It is known that portability is open to exploitation (gathering them all on one warrior before an expedition); it will be measured in the balance pass. |

### The new work Section 7 brings
- **The profession pass**: deciding the 14 roles one by one — what each gives, whether it has its
  own upgrade branch, what a retired warrior's efficiency is. Section 4's open line about "a
  retired warrior cannot be placed at the physician/bone setter/cook/diviner" closes here too.
- **Build time**: a duration per facility, the rule of not working while it is going up, showing
  the remaining time on screen.
- **The physical dojo screen**: opening the facility buildings in a fixed layout, showing the
  wall/gate on this screen.
- **Staff stock consumption**: staff entering the daily consumption formula.
- **The NPC relationship system**: three parties, five tiers, the table of raising/lowering
  actions, the effect per tier (the offer queue's quality, market prices and stock, the omamori
  supply), writing it into the save.
- **Omamori**: the charm definitions, the warrior and staff slots, the carry/sell flow, the tie to
  the temple supply, the balance measurement.

## Section 8 — Chat / the crowd ✔ completed

| Row | Old state | New decision | Note |
|---|---|---|---|
| Connection | A platform-independent adapter, read-only 🟡 | **The adapter stays; the game writes to chat with a bot** | Domina's `domina_bot` is taken. The adapter layer (Twitch + Kick landing on the same internal event) is preserved exactly — the bot is its write direction, not a separate integration. What is written to chat: the opening and result of a seppuku vote, a warrior's death, a new warrior drawn from the pool, command feedback. The GDD §6 rule "if the name is not found it is silently ignored" falls with this decision; a command error is now said in chat. The write volume is deliberately limited (not every honour change is announced) — the bot must not drown the stream's chat. |
| Participation | An opt-out pool, `!no` / `!join`, no expiry 🟡 | **Opt-out stays; a 1-hour freshness window enters the pool** | The pool is no longer "everyone who spoke during the stream" but **everyone who spoke in the last hour**. The rationale: in a 6-hour stream, producing a warrior named after a viewer who wrote once at the start and left means a surprise the viewer never sees his own death in — the mechanic's power is in the person who is present. `!join` priority also lives for **1 hour**, then falls away. It is not a cost problem: chat is already read line by line (for the honour commands), and the pool is a set accumulating from that stream; Twitch's chatter-list API (lurkers included) is **not** taken — it needs moderator rights and has no equivalent on Kick, which would break platform independence. |
| Participation — `!no` | Indefinite 🟡 | **For the session** | `!no` is **not subject to the window** (someone who has opted out does not fall back into the pool when the window expires) but it is **not written to the save** either: when the game closes the list resets and the person writes `!no` again in the next stream. The rationale: a permanent blacklist means the game keeping usernames on disk; consent is taken again every stream and the save file stays clean. |
| Voting | A 60 s window after the fight ends, a queue 🔵 | **It stays exactly as it is** 🔵 | In Section 1 the time model moved to pausable real time; even so, a vote is not opened **during** a fight. The queue mechanic is preserved: a warrior who falls below the threshold waits, a 60-second window opens when the fight ends, and two votes are never open at once. Stopping the fight for a chat vote would break the automatic fight's own rhythm and turn every seppuku candidate into an interruption. During a fight chat only writes honour commands; the verdict comes after the fight. |
| Economic effect | A ratio-based multiplier, clamped 0.5–1.5 ✅ | **The formula stays, the band narrows to 0.75–1.25** | `bushiRatio = bushi/(bushi+ronin)` and using a ratio rather than raw counts (fairness between small and large chats) are preserved; so is applying the same ratio to the survival chance after surrendering. Only the ends change: 100% ronin gives 0.75, 100% bushi 1.25. The rationale: a 0.5 multiplier meant a silent or hostile chat could sink the economy on its own; 1.5 meant hype made the balance measurements meaningless. Chat should **colour** the reward, not determine it. The new band will be measured in `Domina.Sim`. |
| Play without a stream | The AI crowd is fully equivalent 🔵 | **Mechanical equivalence stays; no fake chat is generated** | The AI crowd looks at the same performance signals, produces its own bushi/ronin ratio, runs the same reward multiplier formula and makes an honour-weighted decision in the seppuku vote — no system stays switched off (GDD §9 is preserved). What changes is the **presentation**: in single player no fake chat stream with made-up usernames is shown, and the crowd is read as a single collective indicator (e.g. *displeased ×0.82*). Made-up names cannot take the place of the real viewer names the player knows; an honest indicator was preferred over an empty imitation. The bot also writes only in stream mode. |
| Crowd money | We have no Crowd Favour ⬜ | **It does not enter** | The crowd's only money channel stays the honour multiplier. A separate "spectacle" payment would push the player in a direction other than the one honour wants — toward showy but unnecessary risk; two separate crowd rewards would compete and muddy the reading of the fight. Show fights (an exhibition, a tournament) will bring their own reward items anyway; the ticket/prize money there is not the counterpart of this row. |

### The new work Section 8 brings
- **The chat bot**: a write direction on the adapter, the list of events to announce and a rate
  limit, command feedback, running only in stream mode. The "silently ignored" line in GDD §6 will
  be updated.
- **The pool window**: a timestamp on the set of speakers, the 1-hour drop rule, `!join` priority
  fading over the same period, `!no` being exempt from the window but limited to the session.
- **The reward band**: pulling the clamp to 0.75–1.25 and measuring it in `Domina.Sim` (because the
  surrender survival uses the same ratio, that is remeasured too).
- **The crowd indicator**: a collective crowd indicator instead of fake chat in single player (the
  mood + the multiplier in force), readable next to the real chat stream in stream mode too.

## Section 9 — What we have and Domina does not ✔ completed (except 9.1)

| Item | Old state | New decision | Note |
|---|---|---|---|
| 9.1 An engine-free deterministic core | An architecture rule, "critical not to break" in CLAUDE.md | ⏳ *not settled* | The item opened in Section 5's "Resolution" row stayed open. Three options are on the table: (a) the rule continues as it is, (b) the decision stays in the core but position/distance/animation timing are handed entirely to Godot, (c) resolution moves into the engine. Because 9.2 chose "measure as each system arrives", (c) conflicts with that decision — with no sim there is no measurement. The decision was not made in this pass. |
| 9.2 A measured economy | Runs of 1000 dojos × 60 days, the prices justified by measurement | **The method stays; measurement happens as each system enters the code** | The decision pass invalidated all the existing measurements (real time, stat gain from fights, staff, classes). Instead of one big measurement pass, **incremental** measurement was chosen: every system gets its own sweep as soon as it lands. The price was accepted knowingly — a later system breaks an earlier measurement and some sweeps are repeated; in exchange a wrong number does not stay buried for months. The numbers now to be treated as void: the training rate 0.04, the effects of the school branches, `MaxPower` 2.2, the risk premium 0.25, the market ceiling 0.75. |
| 9.3 Catching / poison / stunning | All three tied to **the weapon in hand** 🔵 | **Tied to both the class and the weapon — a product** | The class **opens** the mechanic, the weapon **multiplies** it: `chance = base × class × implement`. With the right implement, full strength (a catcher + a sai 30%, + a jitte 24%); with the wrong weapon or empty-handed, **weak but not zero** (10%); a warrior with no class catches **nothing** even holding a jitte (0%). The rationale: let the identity belong to the warrior and the efficiency to the equipment — a master who drops his weapon weakens but does not become someone else, and giving a recruit a jitte does not create a master. The price: the two multipliers have to be swept **together**; the old single-axis measurements (like jitte 78.00% / katana 75.02%) will be taken again under this rule. |
| 9.4 Armour wear | A piece wears by the damage it stops, and when it breaks it is gone permanently 🔵 | **Stays exactly as it is; repair yes, a broken piece does not come back** | A keikogi ~7 fights, an ō-yoroi ~15. A worn piece can be repaired with gold, but once its pool is spent and it has broken it is gone. Armour thus stays a continuous expense — it works in the same direction as Section 4's daily consumption pressure. Repair pricing and the smith staff member's effect on it will be seen in the profession pass. |
| 9.5 Limb loss continuing | The warrior lives on maimed, the penalties are sided 🔵 | **Stays exactly as it is** 🔵 | The sword arm ×0.65, the off arm ×0.85, a leg ×0.55 evasion / ×0.60 speed, an eye ×0.75 accuracy. A maimed warrior does not become unusable; the decision left to the player is **"retire him or keep using him"** and that decision is itself the mechanic's value. An honourable exit from the roster for a maimed warrior was **already closed in Section 2**: retirement (he becomes a master, a permanent bonus to training speed, the food load ends) and limb loss **reopening the class choice**. This row does not change those decisions, it confirms that the raw penalties are not softened. |
| 9.6 The honour system | 0-100, with decay, tied to the reward multiplier and to seppuku ✅ | **Stays per warrior; no dojo honour is added** | Honour stays each warrior's own stat (starting 50, threshold 30, pardon 45 — the numbers left to playtesting). A second layer of "dojo honour" derived from the roster's average was **not** opened: the NPC relationship already carries its own five-tier number (Section 7), and a third abstract reputation number would muddy both the screen and the model. |
| 9.7 Contract honour | A contract taken and not returned from lowers the roster's honour ✅ | **It stays and stacks with an NPC penalty** | Abandoning a contract bites in two places: the whole roster loses honour **and** the relationship tier with the party that issued it falls. The rationale: taking a contract is giving a promise, and not keeping it should leave a mark both in the warrior's record and in the working relationship. How often a selective dojo takes this double penalty will be measured — in Section 4's measurement only **0.69** contracts were entered in 60 days, so if the penalty is heavy the policy could lock up entirely. |
| 9.8 The path choice | Blade / Stone / Shadow, a single irreversible choice unlocked at 20 training days 🔵 | **It stands alongside the class; two separate layers** | The **class** says what he can do (catching, poison, range — 9.3), the **Path** says what he leans toward (pure stat tendencies: Blade Accuracy/Strength ×1.10, Stone Defence ×1.15 + Health ×1.05, Shadow Evasion ×1.15 + Speed ×1.10). Two warriors of the same class can take different paths; the roster thus differentiates on two axes. Branching the path within a class (paths specific to each class) was rejected — it would enlarge the balance surface for nothing. The path's 20-day lock and irreversibility are preserved. |
| 9.9 A versioned, merge-on-load save | A versioned, merging `Load` that never throws ✅ | **It stays; a rotating automatic backup is added on top** | The version + merge-on-load + showing the warnings to the player are preserved exactly (the condition for shipping updates in early access without breaking saves). On top of that come rotating backup files against corruption. ⏳ *An open detail:* in a game with permadeath the backup must not be **a door to undo** — that the backups are not offered in-game as a "go back to the previous day" option and stay only a corruption-recovery route will be decided separately. |

### The new work Section 9 brings
- **Closing 9.1**: the core/engine boundary is still open; without this decision the shape of
  phase 4 and beyond is undefined.
- **The incremental measurement setup**: a control-run pattern per system, a record of which number
  came from which measurement, marking the void numbers in the GDD.
- **Two-multiplier catching/poison/stunning**: the `class × implement` formula entering the core,
  zeroing for a warrior with no class, sweeping the two multipliers together.
- **Armour repair pricing**: tying the repair cost and the breakage limit to the economy (together
  with the profession pass).
- **The contract penalty's double effect**: measuring the honour loss + the relationship drop
  together, and whether a selective policy locks up.
- **The save backup**: a rotating backup file scheme and how (or whether) the backup is offered to
  the player.

## An additional decision — a stun drops the weapon (an addendum to Section 5, 2026-09-07)

An item opened after Section 5 closed. Until now the weapon-drop die was rolled in two places (0.05
on a strike landing on armour, 0.05 on a caught weapon) and the die was always rolled on **the
attacker's** weapon — an edge that bites into plate twists and the weapon leaves the striker's hand.

**The decision:** stunning becomes a **third trigger**. When a stun holds, another die is rolled and
**the stunned** warrior can drop his weapon.

**The chance is set by the stunned warrior's own weapon** — the existing "tendency to leave the
hand" table works in the second direction too:

| In the stunned warrior's hand | Tendency | Result |
|---|---|---|
| Cutting (katana, nodachi) | 1.0 | Drops easily |
| Piercing (yari) | 0.6 | — |
| Blunt (tetsubo, kanabō) | 0.2 | Hard to drop |
| Fists | 0 | There is nothing to drop |

**Why this direction:** carrying a heavy blunt weapon gains a defence of its own — even stunned, the
club stays in his palm. The same table working in both directions also needs no new set of numbers.

**What has to be measured:** this rule gives the blunt class a **third** gain (it produces the stun,
and it is also the most resistant to dropping). Blunt had already passed cutting in front of an
enemy in ō-yoroi (89.20% against 87.53%); the separate die's base chance will be looked for where it
does not make the blunt class dominant. And because for the first time it is **the defender** who
loses a weapon, the price of the "whoever is empty-handed picks it up" rule by fight shape (1v1
7.3%, 3v3 40.4%) has to be measured again.

## Section 10 — The number-by-number summary ✔ completed

Note: per 9.2's decision the numbers below are a record of the **shape**, not of measurement. The
decision pass invalidated all the existing balance; every number will be measured again when its
system enters the code.

| Row | Old state | New decision | Note |
|---|---|---|---|
| Starting purse and store | 600 gold, an **empty** store | **600 gold + 3 days of food and water** | Domina starts with a full store (800/400/80), while ours was completely empty. The middle was chosen: the player plans his first expedition without going hungry, and supply pressure bites from **day 4**. The rationale: opening the first day with a supply crisis made the game not harder but more **confusing**; scarcity should not be a punishment before it is taught. |
| Starting roster | 4 warriors | **4 warriors** (unchanged) | Domina starts with 3. Our expedition party is at most 4 anyway; starting the roster at a full party's size keeps the first expedition from depending on the market. |
| Campaign length | Unlimited (measurement over 60 days) | **A fixed 180-day countdown** | Section 1 said "a fixed countdown + the days left on screen" and left the number to here. 180 is the whole horizon the long-horizon pass measured: a selective dojo takes the treasury from 75 to 2288 and closures fall from 8.5% to 2.5%. Domina's 365 was halved — our decision density per day is higher. ⚠️ A measured risk: if the threat runs out in the late game the days fall into repetition; the difficulty curve's ceiling (`MaxPower` 2.2) will be revisited at this length. |
| Expedition party | At most 4 | **At most 4** (GDD #1, locked) | Domina sends 1-15 by contract. In ours the party limit also set the shape of the final (see below). |
| Compulsory fight | None | **Every 7 days; the penalty is honour** | The number for Section 1's "`Next fight: n days` counter" decision. A dojo that misses one loses **reputation, not money**: the roster's honour falls and the offer queue worsens. The rationale: with a money penalty a rich player would **buy** the safe training loop and the exploit would stay open. Because an honour penalty also pushes toward the seppuku threshold, the price of avoiding fights accumulates. |
| Entering the final | None | **3 heads** | The same number as Domina's gate ("3 Regional Champions", shown on screen as 1/3) but for a different reason: bounties are already hard for us — in measurement a selective dojo entered only **0.69** contracts in 60 days. 5 heads would turn the gate into a real blockage and the player would finish the season without ever seeing the final. A gate should give a **goal**, not be a wall. |
| The final tournament | None | **5 consecutive rounds, no healing in between** | Domina runs a single championship at the end of the year with **15 well-equipped gladiators** (all 100+ in every stat). 15 does not work for us: the expedition party is at most 4. But a single fight cannot carry the weight of 180 days either. Five rounds with no healing in between: a new party can be built for each round, and the wounded and the tired accumulate — so the final tests **roster depth**, doing with a party limit of 4 what Domina tests with 15 opponents. It matches Section 1's rationale ("the season's goal is roster breadth and depth") exactly. ⚠️ **It replaces Section 6's line "the campaign's end is a single final fight"** — the end is not one fight but five rounds; that line's ruling **"losing the final = game over"** holds exactly, and being knocked out of the tournament ends the run. |
| The market | 10 candidates, refreshed daily, a 150-gold base | **The same; a candidate with a class drops occasionally** | Because classes are unlocked with a facility (below), the market's main stock stays the **classless recruit**. On top of that a **ready, classed** and markedly expensive candidate appears rarely: a shortcut past the facility investment, but at a price. The frequency and the price multiplier will be measured — the shortcut must sit where it does not make the facility branch pointless. |
| Warrior price | A 150-gold base, multiplied by talent | **The shape stays** | Base × talent, bitten by the ceiling that tracks the best warrior (0.75). The numbers will be measured again per 9.2; the market ceiling will be swept again now that training, facilities and the risk premium exist. |
| Unlocking a class | Domina: 400-500 gold + 16-17 turns; we had none | **Unlocked with a facility: gold + build time** | Domina's research model is taken with a thematic adaptation. The class is the dojo's decision: once the relevant facility is built, that class can be trained. It sits directly on Section 7's facility tree and build-time rule; a dojo that is narrow at the start of the season widens over time. The gold and days per facility will be set by measurement. |
| A school/facility node | Gold, no duration | **Gold + build time** (Section 7) | Domina's hourglass had been taken; this row is that decision's number side. |
| Yield threshold | Domina: 10% (20% with an upgrade); ours the player's decision | **The player's decision stays** 🔵 | No automatic yield. Whether a warrior dies is the player's decision; the price of withdrawing already rises like a ladder (GDD §5). An automatic threshold would take away the **only** intervention the automatic fight leaves the player. The price was accepted knowingly: a forgotten warrior dies. |
| Fight duration | Domina ~3-minute timer; ours `TimeLimit` | **No time limit** (Section 5) | `BattleOutcome.TimeLimit` comes out of the code. |
| Event frequency | 15% a day | **The day stays the unit** | Time moved to real time, but the event die is rolled **at the turn of the day**; the player sees all the offers and events together in the morning. The rationale: events dropping suddenly within the flow raise the tempo but turn the pause-read-continue loop into constant interruption; reading them together daily is calmer both to write and to play. The 15% rate will be revisited by measurement. |

### The new work Section 10 brings
- **The 180-day season**: the countdown screen, the season-end conditions, remeasuring the
  difficulty ceiling at this length so the late-game threat does not run out.
- **The compulsory fight counter**: the 7-day rhythm, the honour penalty for missing it, measuring
  the penalty's cumulative effect with the seppuku threshold.
- **The final tournament**: 5 rounds, no healing between rounds, a party selection screen for each
  round, building the opponent rosters.
- **The head gate**: showing the 3/3 counter on screen, the final staying locked while the gate is
  closed.
- **Class facilities**: a facility for each class, the gold and build time; the frequency and price
  multiplier of the rare classed candidate in the market.
- **The starting package**: 3 days of food and water entering the save and the new-game flow.

## Section 11 — The work list that comes out of this ✔ completed

This section was a list of proposals, not decisions. By the end of the pass most of the items had
already been closed in other sections; here it is written where each item was tied down.

| Item | Outcome | Where it was decided |
|---|---|---|
| **A. The campaign has no end** | **Closed** | Section 1 (a fixed countdown, the final, the head gate) + Section 10 (180 days, 3 heads, a 5-round final). Of the three options proposed, (1) and (3) were combined: a fixed season **and** a head gate. |
| **B. A fight teaches nothing** | **Closed** | Sections 2/3: a fight now grants stats. GDD §10's "the warrior side is kept shallow" rationale is void. The item's diagnosis ("going on an expedition is pure loss") was correct. |
| **C. There is no harmless fight** | **Closed — deliberately closed** | Section 6: an exhibition enters only as an **in-dojo sparring match**; a deathless show fight against outsiders is not opened. The risk threshold stays clear: inside the dojo is safe, every fight that goes outside is lethal. |
| **D. There is no resource called morale** | **Closed — morale enters** | Section 2: a separate **morale resource** + a **9th stat, Will**, tied both ways (high Will → morale falls slowly; low morale → the seppuku risk and the panic threshold shift against him). At one point in this pass "let morale be folded into honour" was chosen and then undone: honour is **reputation**, morale is **mood** — putting the two on one counter would load chat's honour game and the dojo's maintenance game onto the same bar. Section 4's ⏳ line about **sake as a fourth resource** will be tied to this system. |
| **E. There is no way to be rid of a roster member** | **Closed** | Section 2: **retirement — he becomes a master** (he leaves the field, becomes an instructor, a permanent bonus to training speed, the daily food load ends) and **only honourable exits** (seppuku, retirement, seeing him off). Selling and killing **do not enter**: a student is not property. The "a maimed warrior eats food forever" leak is closed by retirement. |
| **F. A "good fight" is not rewarded** | **Closed — it does not enter** | Section 8: Crowd Favour was rejected. The crowd's only money channel stays the honour multiplier; a second spectacle payment would push the player in a direction other than the one honour wants. |
| **G. The right to yield could be tied to the contract** | **It enters** | Some contracts arrive with a `no withdrawal` condition and **pay more**. The decision is made not in the fight but **when the contract is taken** — it does not conflict with 10.3's "withdrawal is the player's decision", because what closes the key is the player's own signature. It adds weight to the bounties. The reward multiplier and which contract types can carry this condition will be measured; if it appears in every contract the rule turns from a choice into a tax. |
| **H. The "not taken" list** | **Partly void** | Still valid: driving a warrior by hand (mind control), getting harder as you win + the lose-on-purpose exploit, wine bribes / blackmail. **Two items are void:** "staff + slot economy will not be taken" — Section 7 took staff (with a wage constraint instead of slots); "the card/blessing system will not be taken" — Section 7 took it as **omamori**. These two lines will be corrected when the list is written into the GDD. |

### The new work Section 11 brings
- **The no-withdrawal contract**: showing the condition on the offer screen, the reward multiplier,
  how often it appears in which contract types, the withdrawal key being disabled in that fight.
- **Sake and morale**: tying the fourth resource Section 4 left open to the morale system.
- **Correcting the "not taken" list**: the staff and card system lines will be updated as they go
  into the GDD.

---

## The end of the decision pass

All the sections (1-11) were reviewed. The state coming out of the pass:

**The one big item left open:** 9.1 — the engine-independent deterministic core. The rule is in
force for now but the user has not decided; 9.2's "measure as each system arrives" decision requires
the sim to live, so the decision means solving these two items together.

**A separate pass left for later:** **the 14 professions one by one** — what each role gives,
whether it has its own upgrade branch, which profession a retired warrior can do (Section 4's ⏳ line
closes here too).

**The other ⏳ lines:** Section 4's stock resources (sake — now to be tied to morale) and scarcity;
~~Section 5's **Opponent** row~~ (**closed 2026-09-10** — human only) and ~~the rival dojo and
village NPCs~~ (**closed 2026-09-10** — 12 settlements, a 7-day move counter, no recurring income from
settlements; GDD §10 "The rival school and the settlements"); the save backup not turning into a
door to undo (9.9).

**The next job:** the decisions in this section **will be written into the GDD**. The locked rules
invalidated in this pass: the discrete-day model, "no classes", "the warrior side is kept shallow",
`BattleOutcome.TimeLimit`, "if the name is not found it is silently ignored", the 0.5-1.5 reward
multiplier band, and the staff/card lines of the "not taken" list. All the balance measurements are
void too — remeasurement will happen, per 9.2, as each system enters the code.

# 13. The profession pass — the 14 roles one by one (2026-09-07)

The separate pass Sections 7 and 4 left owing. The 14 roles in the draft were each decided;
**three fell off the list and one became a tier of another role**. That leaves **11 roles**.
Section 4's ⏳ line about "which role a retired warrior can be placed in" closes here too.

## The remaining roles

| Role | Its Domina counterpart | What it gives | Its own branch |
|---|---|---|---|
| **Drill master** | *Doctore* (the free starting staff member, auto-train, the game's largest skill tree) | A training speed multiplier. The dojo's **free starting staff member**. | ✅ 3 tiers: speed ×1.30 → two drills in the same day → speed ×1.30 once more |
| **Kata master** | *Doctore Emeritus* (training time −75%, stat ceilings, more EXP) | Raises the **stat ceiling** (+4 on the percentage stats, +20 on health/stamina) **and multiplies the stats gained from fights**. The late game's staff member; it rewards going on expeditions. | — |
| **Weapon master** | Not a separate staff member — the *Doctore*'s weapon nodes (*Blade Control*, *Attack Vector*) | Gives warriors **permanent mastery per weapon** (accuracy and strike speed with that weapon). The mastery lives **in the warrior**: if the master is cut, the mastery already earned does not go, but no new mastery is earned. | — |
| **Physician** | *Medicus* (34 gold; the video calls him "interesting but weak") | Turns a lethal wound around **and** removes the medicine expense. | ✅ 3 tiers: a lethal wound is turned → the medicine expense goes to zero → limb-loss risk ×0.75 |
| **Smith** | *Faber* (automatic repairs and upgrades, blueprint discounts) | Makes repairs cheaper and faster; he is **the condition for upper-tier armour such as the ō-yoroi** (without a smith it cannot be bought). | ✅ 3 tiers: repair ×0.7 → ō-yoroi unlocked → a special weapon is forged |
| **Steward** | *Emptor* ("cheaper upgrades and resources"; the guides hire him for the last shopping trip and fire him) | **The spending side only**: stock ×0.80, warriors ×0.75, repairs ×0.80. There is **no** reward multiplier. | ✅ 3 tiers: purchases ×0.90 → ×0.80 → store capacity +50% |
| **Broker** | Not counted (the *Emptor* bargains, the candidate comes from the *Magistrate*) | **He does not touch the price** — he changes what the market *puts out*: the number and quality of candidates rise and classed candidates appear more often. | — |
| **Bard** | *Bard* (13 gold, the cheapest staff member; morale through songs) | Gives a daily morale gain and slows the decline; he also **sings a lament for a dead warrior** — partly filling the hole a death opens in the roster's morale. | — |
| **Monk** | *Sacerdos* (100 gold, **consumes nothing**; prayers, passive stats) | **He is the temple's hand in the dojo**: he opens the omamori slots, compensates the honour lost to a death with a funeral rite, and slowly raises the temple relationship. He gives **no** passive stat bonus. | — |
| **Cook** | *Agricultor* (25 gold, produces 4 food a day; the opening's most important staff member per the video) | **He does not produce — he cuts consumption**: the roster's daily food consumption ×0.75. | — |
| **Diviner** | *Haruspex* (72 gold; curses the enemy, halves his health) | **No curses — information**: he reveals the enemy's stats, weapon and behavioural tendency in the offers, and gives a partial reading in a blind fight. | — |

## Those cut from the list

| Role | Outcome | Rationale |
|---|---|---|
| **Bone setter** | **Became the physician's 3rd tier** | Once the physician took on lethal wounds and the medicine expense, there was no separate job left for the bone setter. One role with a deep branch instead of two separate health staff. |
| **Fixer** | **Does not enter** | Domina's *Agent* carried dirty work, theft and betting; betting was rejected in Section 4 and stolen equipment was rejected here because it punctures the economy's backbone. Contracts already come from the NPC relationship (Section 7); a second queue source is not opened. The dirty-work axis was **never opened** in this pass. |
| **Groom** | **Does not enter** | His Domina counterpart was the horse/chariot events, and Section 6 rejected those. In a game with no horses there is no real job left for a groom; the expedition duration stays a property of the contract itself and is not negotiated with staff. |

## The roles with an upgrade branch

Section 7's rule ("14 separate trees are not an economy but filling in a table") was applied:
**four roles** carry their own 2-3 tier branch — **the drill master, the physician, the smith and
the steward.** The remaining seven roles have a single effect; their depth comes from the facility
tree and the NPC relationships.

How to read it: the four roles with a branch correspond to the dojo's four continuous expenses —
**training, health, equipment, supply.** The rest are situational staff: one opens a door (the monk,
the smith's upper tier), one gives information or morale on an axis (the diviner, the bard, the
broker), or one cuts a number (the cook).

## A retired warrior's efficiency (Section 4's ⏳ line closed)

A retired warrior **takes no wage** — that is the long game's reward. But he cannot do every job:

| Tier | Roles | Meaning |
|---|---|---|
| **Good** (better than a hire) | Drill master, Kata master, Weapon master | He teaches what he learnt on the field; the warrior you trained is most useful here |
| **Medium** | Steward, Broker, Monk | He manages; not as good as a hire but unpaid |
| **Weak** (half efficiency) | Smith, Bard | He does the craft by halves |
| **None** | **Physician, Cook, Diviner** | They need a lifetime of separate expertise — **outside staff are required** |

**The ban's rationale is an economic decision:** if a free retired warrior could fill every facility,
no facility would ever be empty and the staff wage would be pulled out of the economy. Section 4's
decision that "in a crisis you cut the staff and keep the building — for the first time the economy
gets a **gear**" would spin idle with no wage left to cut. The three banned roles keep the wage
pressure permanently open. Widening the ban entirely (only the three training roles) was rejected
too: retirement would then stay narrow enough to reopen the leak Section 2 closed.

## How the empty-facility rule lands in this pass

Section 4's rule that "if a facility is empty it works at **half efficiency**" is preserved; staff
bring it to full efficiency. The monk is the example:

- **A temple but no monk:** one omamori slot works; no funeral rite can be held and the temple
  relationship does not rise on its own.
- **With a monk:** all the slots open (warrior and staff), the funeral rite is held, and the
  relationship rises slowly.

Some doors are still a staff requirement (a smith for the ō-yoroi) — those do not open at half
efficiency, they are either there or not.

## The work this pass brings

- **Defining the 11 roles**: the effect per role, the daily wage, the daily stock consumption, the
  facility pairing.
- **The four upgrade branches**: the tier costs and effects for the drill master / physician /
  smith / steward.
- **Weapon mastery**: a permanent per-weapon mastery counter living in the warrior; outside the
  `class × implement` product, a flat bonus to be measured separately.
- **The diviner's enemy reading**: an enemy card on the offer screen, a partial reading in a blind
  fight.
- **The bard's lament**: reducing the roster's morale drop after a death; it ties into the morale
  system.
- **Placing retired warriors**: the efficiency tier per role, blocking placement in three roles,
  showing the reason in the interface.
- **Half efficiency for an empty facility**: a definition per facility of "what works with no
  staff", and marking the doors that require staff.
