# Design Decisions (Decision Log)

> This file holds the **locked** design decisions. For the roadmap, see `ROADMAP.md`.
> For the externally verifiable grounds behind the decisions, see `DESIGN-REFERENCES.md`.
> Decisions still open are in the "Open Decisions" section at the bottom.

> ⚠️ **2026-09-07 — decision round processed.** The line-by-line decision round (`# 12`)
> and the profession round (`# 13`) carried out on `docs/COMPARISON-DOMINA.md` have been
> folded into this file. The round **overrode** several locked rules (the discrete-day
> model, "no classes", `BattleOutcome.TimeLimit`, the reward multiplier band, chat staying
> silent) and brought in new systems (classes, staff, morale + Will, NPC relations, omamori,
> the final tournament, the night raid). Wherever something changed, the old decision was
> **not deleted**: it stands struck through, together with its rationale.
>
> ⚠️ **All balance measurements are invalid.** The move to real time and stat gain from
> fighting removed the ground the current numbers rested on. The measurement texts are kept
> as a **historical record**; each system will be re-measured as it enters the code
> (decision round 9.2).

## 1. Concept

A samurai-themed **trainer/school management + fully automatic combat** game. The player runs
a dojo (school), trains samurai warriors and sends them on phase-by-phase expeditions.
There is almost no intervention in the fight — the real game is in the **development and
decision** layer.

When the streamer connects to Twitch/Kick, chat influences the warrior pool, the economy
and the life-or-death decisions about warriors. **The single-player mode without a stream
is fully equivalent** — everything chat does is simulated by an AI audience.

### Reference games and what we take from them
| Game | What we take |
|---|---|
| Domina | School/trainer management, automatic combat, the chat-integration philosophy, surrender, gore |
| Darkest Dungeon | Base + roster + 1-3 person expedition + permadeath |
| Battle Brothers | Permanent disability (living on after limb loss) |
| Hades | Phase/room-based expedition progression |

### Deliberate differences from Domina
- The opponent is **human**, as in Domina, but the fights are not arena bouts: each one belongs to
  a rival school's protection racket, so a fight has a **place in the province** (§10 settlements).
  **Closed 2026-09-10 (Open Decision #16) — there are no monsters in the enemy pool.** What used to
  carry this differentiator was "yokai instead of humans"; what carries it now is the rest of this
  list, above all the contract economy and the depth-testing final
- In Domina limb loss is only **death gore**; with us there is **surviving and staying
  permanently maimed**
- Surrender in Domina is a **mash QTE**; with us it is a **single-key decision**
- The Domina model for the chat name pool is **kept** (everyone is included, with `!no` to
  opt out), but the pool gained a **one-hour freshness window** (§8)
- Engine: Domina is C++/Allegro; we use Godot 4 + C#
- The crowd has no money: Domina's *Crowd Favour* (separate payment for how watchable the
  fight is) was **not taken** — the crowd speaks only through the honour multiplier (§6)
- Staff **was taken** but the slot economy was not: the ceiling is set not by slots but by
  the **daily wage** (§10)
- A student is not property: a warrior is **not sold, not killed**; leaving the roster is
  only honourable (seppuku, retirement, sending them on their way — §10)

---

## 2. Technical Decisions

| Topic | Decision |
|---|---|
| Engine | **Godot 4.x** |
| Language | **C#** (.NET) — the typed language closest to the user's TS background |
| Platform | **Steam** (Windows first) |
| Architecture | Core simulation is **pure C# without Godot** — engine-independent, unit testable |
| RNG | **Seeded and deterministic** — a fight can be replayed/debugged |
| Chat | A platform-independent **adapter layer**; Twitch and Kick land on the same internal events |
| Save | **Versioned save + merge-on-load + try/catch** (the pattern from the Domina project) |
| Visual technique | **Pure 2D, cutout/skeletal animation** (a flat `Node2D` hierarchy — **not** Skeleton2D/Bone2D) |

> **Why not Skeleton2D:** Bone2D is for mesh deformation; what we need is not to deform a
> limb but to **tear it off**. In a flat `Node2D` chain, severing = detaching a node from
> its chain, which is exactly what the rationale below describes. The weapon of a warrior
> who loses an arm goes with the chain.

### Why runtime-skeletal and not baked frames
Domina uses frame-based sprite sheets (every animation frame pre-baked). Because limb loss
is **permanent** for us, going frame-based would mean drawing the entire animation set
separately for every combination (no right arm, no left arm, one leg, no arm + no leg…) →
combinatorial explosion.

In skeleton-based cutout, losing a limb is **detaching a node at runtime**; no new art
asset is needed. Only a few special animations (one-handed attack, limping) are added.
This is not an aesthetic decision but a technical one **forced by the permanent-disability
mechanic**.

Reference games: Darkest Dungeon (exactly our structure), Rayman Origins/Legends (quality
ceiling), Ori, Hades, Guacamelee.

---

## 3. Theme

**Samurai, early Edo.** The player is the sensei of a dojo; the warriors fight the men of a
rival school — its collectors, its hired blades, street bravos, its own seniors. Honour sits at
the centre of both the economy and the life-or-death decision, but not as a written code: what a
masterless man owns is his **name**, and the honour system already models that.

**There are no monsters (2026-09-10, Open Decision #16).** The earlier yokai theme is dropped.
The reason is the story (`STORY.md`): every fight in a season belongs to the same protection
racket and the same 180-day clock, and a creature encounter belongs to neither — it would be
filler. Dropping it cost the design nothing, because an enemy is only a stat block: the numbers
carried over unchanged into `Campaign/Adversaries.cs`, only the identities were rewritten.

What a human enemy buys and a creature did not: permanent death, honour and the three-year
contract all weigh something when the other side is a person too.

**The school's licence is inherited, its standing is not (2026-09-10).** The dead master's licence
passes to the player with the school, so the dojo can take contract work from day 1 — the licence is
what makes the contract economy legal, and without it there would be no offer queue to open the game
with. What does **not** pass on is standing: a licence with no proven work behind it does not get a
school entered in the appointment contest (the 3-bounty gate, §10) and does not draw good men to the
gate (the market's `BestFollowCeiling`, §11). That split is the whole premise — you start legal and
unknown.

---

## 4. Warrior and Combat

### Stats (per warrior)

Nine stats. Recruit base values in parentheses (`WarriorStats.Recruit()`).

| Stat | Code name | What it determines |
|---|---|---|
| **Health** | `MaxHealth` (100) | Maximum health. The limb-loss and stun thresholds are also read from here (hit / max health > 0.20) |
| **Aggression** | `Aggression` (40) | How many of the charge opportunities it sees are used |
| **Defence** | `Defense` (35) | Reduces damage taken **and** gives the block frequency (§5) |
| **Evasion** | `Evasion` (35) | An attempt to make a blow miss; spends stamina |
| **Strength** | `Strength` (40) | The damage of a hit |
| **Accuracy** | `Accuracy` (55) | Chance to hit **and** blade catching (§7) |
| **Stamina** | `MaxStamina` (100) | Running, evading, attacking and catching consume it; as it drops, damage and accuracy fall |
| **Speed** | `Speed` (50) | Walking/running speed. While it was fixed, escape always succeeded |
| **Will** | `Willpower` (**new**) | Seppuku resistance, panic threshold, honour gain. It also slows how fast morale falls |

Besides these there are two more counters attached to a warrior, neither of which is a
combat stat:

- **Honour** (`Honor`) — 0-100, starting at 50. The warrior's **reputation** (§6)
- **Morale** — the warrior's **condition**. Low morale breaks stats; it is linked to Will
  in both directions (high Will → morale falls slowly; low morale → checks that rest on
  Will shift against the warrior). Raised by: victory, rest, the bard, a feast. Lowered by:
  defeat, a fallen comrade, going hungry. Numbers to be measured

> **Why honour and morale are separate:** honour is what the outside thinks about the
> warrior, morale is the warrior's own condition. Collapsing them into a single counter
> would load the reputation game chat plays and the upkeep game the dojo plays onto the
> same bar. They were merged at one point during the decision round, then unmerged.

### The arena is a plane — warriors really walk

The fight is not abstract but **spatial**: every warrior has a position on the arena plane
(X along the line, Y for depth). There is no physics engine — our own kinematics, a fixed
tick, full determinism.

| Rule | Consequence |
|---|---|
| **Weapon range** | A long weapon strikes from afar, a short weapon has to close in. Out of range, an attack **does not start** |
| **Target = nearest enemy** | The rule falls out of space, it does not need to be written separately. The target is sticky: it is kept until it dies or flees, otherwise the warrior oscillates between two enemies and never lands a hit |
| **Committing to a blow binds you in place** | If you have started a move you cannot walk; if the target leaves range the blade **comes down on empty air** |
| **Encirclement** | A blow from behind is more accurate, heavier, and **cannot be evaded** |
| **Personal space** | Warriors do not overlap |
| **Escape is real distance** | Not a counter: the one fleeing really has to leave the arena |

> **The real price of encirclement is paid in the escape:** when the pull-out command is
> given, **every enemy in range** gets a free hit. If you are surrounded, fleeing means not
> one blow but three. "Get out before you are surrounded" thereby becomes a genuine spatial
> decision.

> **Why not Godot physics:** collision resolution changes from version to version and from
> platform to platform; the "same seed = same fight" guarantee would die. Space lives in
> the core, in pure C# and deterministic.

> **Measured cost:** we went from 16,000 fights/s down to **8,700 fights/s**. The
> acceptance criterion is "10,000 fights < 10 s" — we are still eight times above it.

The camera still looks from the side; depth is shown on screen as vertical offset + a slight
scale + draw order (2D brawler staging). The plane is real, the image is flat — that is, the
rig built from cut paper pieces is not broken (§12).

### Charge — what distance buys

In an arena where everyone walks at a single fixed speed, distance means nothing. The charge
gives a warrior who has ended up far away a way to turn distance into an opportunity — the
price is leaving oneself defenceless.

| Rule | Decision |
|---|---|
| **Trigger: opportunity, not a threshold** | The warrior does not look at a fixed distance but at **space**: *"right now nobody can hit me, and do I have enough time to finish my wind-up?"* What is asked for each enemy is how long it would take them to become able to strike — `(distance − their range) ÷ their speed`. If anyone can do it in less than the wind-up time, there is no opportunity. **Throwing takes priority:** anyone with a projectile to throw throws instead of charging |
| **The decision is the warrior's own** | Whether an opportunity is used comes down to a die scaled by **Aggression**: the bold leap, the measured close the distance on foot. The die is rolled **once per opportunity** — not again at every decision step while the opportunity lasts |
| **Wind-up** | The run does not start immediately: the warrior first **stands still and gathers force**. During this time they do not move, cannot evade, cannot block, and **the first hit taken breaks the charge** — the run never starts, the damage multiplier is never earned. The real price of the charge is paid here |
| **Speed** | During the charge, movement speed increases by a **multiplier** (on top of the `Speed` stat, from the same place as `RetreatSpeedMultiplier`) |
| **Reward** | The first blow struck on arrival earns a **damage multiplier**, and that multiplier comes out of the **actual speed at the moment of arrival**: `1 + (arrival speed ÷ maximum walking speed) × ratio`. Momentum is speed — a heavy Oni's charge cannot be as hard as a Tengu's. Because limb-loss risk already comes from the damage/maxHP ratio (§7), the chance of being maimed rises **by itself** — no separate rule is written |
| **Price** | Commitment: while winding up the warrior does not budge, and **a single hit taken** wastes the move. Along the way, **every enemy whose range they pass through** gets one **opportunity attack** against them — the same mechanic as the escape window (§5) |
| **The target's answer is separate** | An enemy passed on the way gets their hit **for certain**; the **target** of the charge answers on a die. Meeting a body coming head-on at exactly the right moment is harder than hitting someone running past you. When the answer **lands, the charge's momentum dies**: the arrival blow is made but earns no damage multiplier. Not rarity but **weight** — and it spawns no new tuning number, it cancels an existing multiplier |
| **The defence stays open** | A charging warrior **keeps evading at their normal rate**. Defencelessness is specific to escaping (§5) — the price of the charge is not that the defence closes but that the move is exposed |
| **Whiffing** | If the target dies, leaves the field or **starts to flee**, the charge is wasted: the warrior drops to `Idle` and the reward is not taken. The charge keeps its own time limit — a charge that does not arrive within it is wasted the same way. (This is not the **fight's** time limit: `BattleOutcome.TimeLimit` was removed in the decision round, a fight lasts until someone falls) |
| **No charging a fleeing target** | A target that is fleeing does not start a charge and ends one already started. Measured: otherwise the speed-up disables the single tuning knob of escape (`RetreatSpeedMultiplier`, §5) |
| **The "pull out" command cuts the charge** | The charge is committed against the warrior's **own decisions** — they cannot abandon it and pick another move — but the player's command is a separate axis and cuts the charge instantly. The price has been paid: the free hits taken are not given back, the damage multiplier is not spent |

**Where commitment stands:** once a warrior has begun a charge they cannot change their mind
and pick another move; the charge either arrives or is wasted. The only thing that cuts it is
the player's "pull out" command. Measured — had the command not been able to cut it either,
a warrior already running at the moment the key was pressed had to finish the charge, reach
the enemy line and start fleeing from there; this made **pressing at first contact more
lethal than pressing late** and inverted §5's ladder (3v3, 2,000 fights: 18.9% dead at first
contact, 13.2% at the 2nd second). Once the command cut the charge, the ladder fell into
place: **1.0% → 13.2% → 36.6%**.

**Wind-up and the distance threshold are two ends of the same knob.** While a warrior winds
up, the enemy keeps walking; it takes them `(distance − enemy's range) ÷ enemy's speed` to
reach you. From 320 units a Tengu arrives in 0.73 s, a Kappa in 0.88 s, an Oni in 0.87 s. If
the wind-up is longer than these times the charge can break before it even gets going —
**the speed stat thereby does its first real work as "the thing that breaks a charge"**, and
because a throwing weapon can hit a winding-up warrior regardless of distance, a ranged enemy
become the natural counter to the charge.

### Why there is no fixed distance threshold

There used to be one — hand-locked at 320 — and it broke two things at once.

**The charge was trapped as an opening move.** The two lines start 960 units apart, walk
towards each other, and nothing ever puts distance between them again; warriors only close.
Measured: in none of 30,000 fights was a charge begun **after 1.75 s**.

**And the threshold was asking the wrong question.** What the warrior needs to know is not
"is my target more than 320 units away" but *"do I have time to finish my wind-up"*. That is
something computable:

> required distance = **enemy's range + enemy's speed × wind-up time**

With the current roster and a 0.75 s wind-up: **287** for Kappa, **296** for Oni, **327** for
Tengu. The hand-locked 320 was right in the middle of that band — that is, we had found the
right number by measurement but were holding it in the wrong place.

Once the rule was turned into an opportunity assessment, **three tuning numbers fell at
once**: the fixed threshold (derived), the crowd penalty (if three enemies can reach you
there is no opportunity anyway) and the re-ignition window (space opens by itself in front
of a warrior whose target has been felled). The latest charge start went from **1.75 s →
16.05 s**.

**A deliberate blind spot:** the calculation only sees threats arriving on foot. An enemy
with a projectile can hit regardless of distance and break the wind-up, and we do not want
the warrior to know that in advance — **this is what makes the ranged enemy the natural counter
to the charge.** A charging enemy also arrives faster than the calculation predicts: the
second thing that breaks a charge is another charge.

### Charge numbers — locked (2026-09-02)

10,000 fights in the 3v3 scenario, `--policy never`:

**Six** numbers in total — the distance threshold, the crowd multiplier and the re-ignition
threshold were deleted because they derive from the rule itself.

| Number | Value | Measurement |
|---|---|---|
| Probability, Aggression 0 | **0.35** | How many of the opportunities seen are used. A recruit (Aggr. 40) 0.61, a tengu (70) 0.81 |
| Probability, Aggression 100 | **1.00** | A single frequency knob. 0.35-1.00 → 1.88 starts / **1.63 completed charges**, i.e. it stands in for the frequency the old per-step die (0.12-0.45) produced. Under the same rule 0.12-0.45 → 0.78 starts, 0.50-1.00 → 2.15 |
| Wind-up | **0.75 s** | The break rate is 5.7% at 0.25 s, 13.0% at 0.5, **23.6% at 0.75**, and flattens out at 23-24% beyond that. 0.75 is the knee of the curve: past it the commitment lengthens but the risk does not grow |
| Speed multiplier | **1.6** | **Not a balance knob but a presentation knob**, and it stays that way: victory is 84.8% at 1.0, 84.5% at 1.6, 84.4% at 2.5. Being flat is expected since it works for both sides — the thing that needed to come alive was the `Speed` stat, not this multiplier |
| Damage ratio (at maximum speed) | **0.43** | Gives a Speed 50 warrior a ~1.50 multiplier at 1.6× speed. The 0.25-0.6 band is flat; 0.9 and above turn **against** the player (43.2% death), because the ratio works for both sides and variance favours the weaker one |
| The target's counter-blow | **0.6** | **The floor was set not by the charge but by §5.** The counters the target collects are the main income of the outnumbered side and the thing that keeps a numerical advantage from turning into an avalanche. Below 0.6, §5's promise that "pulling out reduces deaths" **inverts** (at 0.25: 41.5% for those who pull out, 40.0% for those who do not). At 0.6: 39.6% pulling out, 40.3% not |
| Time limit | **4.0 s** | Never runs out (distance closes in ~0.6 s). It stands as a safety valve against endless chasing |

With the locked tuning, charge off → on:

| Scenario | Victory | Charges/fight | Arrival | Broken | Latest start |
|---|---|---|---|---|---|
| duel | 66.4% → **63.4%** | 0.61 | 100% | 0% | 1.00 s |
| 3v3 | 81.8% → 83.2% | 1.89 | 86.7% | 13.3% | **13.80 s** |
| veteran | 98.4% → **94.6%** | 0.74 | 100% | 0% | 0.75 s |
| 1v3 | 46.1% → 55.9% | 0.74 | 100% | 0% | 0.75 s |

In single-enemy scenarios the charge is once again an **opening move**: with one person in
front of you the opportunity arises once, and that is at the start of the fight. This can now
be read off the rule itself rather than being a measurement artefact — in 3v3 the opportunity
is reborn as the lines break up, and the latest start is 13.80 s.

The charge's contribution is **negative** in two scenarios — these lines are the measurable
proof that the mechanic has a price. In a one-on-one fight the charge is now plainly a bad
idea: the person facing you is the one looking at you and nobody else, and they have the
highest chance of answering head-on.

### What the target's answer carries

What was expected when the rule was written was to tune the price of the charge. The
measurement showed something else: **the target's counter-blow is the mechanism that keeps a
numerical advantage from turning into an avalanche.** The main income of a warrior standing
against three is the counters collected from those coming at them; when that income is cut,
the crowd's advantage compounds and **§5's escape promise collapses** — the player who pulls
out loses more dead, because the comrades left on the field melt away faster.

| Target's counter-blow | 1v3 victory | Death when pulling out / not (3v3) |
|---|---|---|
| 1.0 (certain) | 63.8% | 38.1% / 40.2% |
| 0.7 | 59.0% | 39.1% / 40.3% |
| **0.6** | **55.9%** | **39.6% / 40.3%** |
| 0.5 | 52.6% | 40.1% / 40.1% |
| 0.35 | 47.7% | 41.0% / 40.2% ✗ |
| 0.25 | 44.7% | 41.5% / 40.0% ✗ |

0.6 was therefore chosen not out of taste but out of **constraint**: the lowest value that
keeps every locked promise standing. The forced-timing escape ladder (§5) holds at every
value — the only thing that breaks is the "pull out when health drops" behaviour, which is
what the player will actually do.

### One die per opportunity — the thing that brought `Speed` to life

The charge die used to be rolled **per decision step** (every 0.2 s). The invisible
consequence was this: the charge frequency depended on how long the warrior **lingered** in
the opportunity window — and the length of that lingering is the speed at which the distance
is closed. A fast warrior passed through the window quickly and therefore charged less often:
in 3v3, per fight, **2.20** at Speed 0 and **1.20** at Speed 100.

This exactly undid tying damage to speed (§4 "Reward"). The fast warrior hits harder but less
often, the two curves eat each other, and the `Speed` axis stayed **inert** in the
measurements: victory 83.9% at Speed 0, 84.7% at Speed 100 — noise.

Once the die was rolled once per opportunity, frequency came **completely** loose from speed
and the axis came alive:

| `Speed` | 0 | 25 | 50 | 75 | 100 |
|---|---|---|---|---|---|
| Victory (3v3) | 83.6% | 83.6% | 84.5% | 85.0% | **87.0%** |
| Charges/fight | 1.87 | 1.87 | 1.87 | 1.88 | 1.88 |

As a rule this is also the right one: you decide once when you see an opportunity, you do not
roll a die five times a second while it lasts. The price is that Aggression's power to
discriminate narrows — a band of 0.35-1.00 (2.86×) instead of 0.12-0.45 (3.75×), because the
same frequency had to be produced with fewer dice. `Speed` being a real knob in the dojo is
worth that trade.

**The break criterion changed with measurement.** "A heavy blow breaks it" (§7's threshold)
was tried first and came out at **0.0%**: the blows landing on a fresh warrior almost never
reach that threshold, so the rule was written but never fired. With the rule **every landing
hit breaks it**, 23.6%. Since the warrior cannot evade anyway, this reads as a single
sentence and spawns no new balance number.

**The size of the price varies with the crowd** and that is deliberate: 23.6% of wind-ups
break in 3v3, 31.6% in 1v3, and **0% in a duel** — a single enemy cannot reach you in 0.75 s.
What breaks a charge is a crowd.

### Why the charge does not close the defence

For a while it did: a charging warrior could not evade, could not block, and took an accuracy
bonus on top. Measurement showed that the rule worked **in an unexpected direction**.

In `Domina.Sim`'s 1v3 roster the lone veteran's victory jumped from 45.3% with the charge off
to 73.3% with it on. Our first reading was "the charge favours the outnumbered side"; it was
**wrong**. Even when the lone warrior's own charge was effectively zeroed, victory stayed at
68% — so the difference was not made by the player's charge. Runs where the only thing that
changed was the enemy's charge showed the cause:

| Enemy's charge | 1v3 victory | Enemy deaths |
|---|---|---|
| No charge at all | 45.3% | 70.0% |
| Wind-up 2.0 s (84.7% of charges break) | 54.9% | 72.3% |
| Wind-up 0.75 s | 68.1% | 80.9% |
| Wind-up 0 (charges always complete) | 79.0% | 90.6% |

The better the enemy's charge worked, the more the lone warrior won: three weak enemies were
running **defenceless** at a strong veteran and gifting them unevadable free damage.

**The rule was removed and the diagnosis confirmed.** Once the defence returned to its normal
rate, 1v3 victory fell from 73.3% to **64.6%** — nine points, from removing a single rule.
Putting the price in the closing of the defence produced a penalty that was asymmetric
depending on *who* was charging.

### Speed was tied to damage — and it did not bring the expected balance

While the damage multiplier was a fixed 1.5, the `ChargeSpeedMultiplier` axis was **inert**
in measurement. Tying momentum to real speed (Mount & Blade's couched-lance pattern) was set
up to fix that — **it did not.**

The mechanism works: a fast warrior's arrival blow is measurably harder (bound by a unit
test). But at the balance level the effect washes out, because **two curves move in opposite
directions.** The player side's `Speed` was swept (3v3, charge on minus charge off):

| Player side Speed | Charges/fight | Charge's contribution to victory |
|---|---|---|
| 0 | 2.19 | +3.4 points |
| 50 | 1.72 | +2.5 points |
| 100 | 1.20 | +2.6 points |

A fast warrior charges **harder but less often**: because they close fast, the opportunity
window stays open for less time. The two effects cancel out.

**The change still stands**, because it fixes the model rather than the number: momentum now
comes out of speed, an Oni's and a Tengu's charges are not equally hard, and one constant
replaced another — the tuning count stayed at six. But if we want `Speed` to be a **real
charge lever in the dojo**, the real obstacle is written here: the opportunity window works
against speed.

**The real gain:** the charge is no longer always the right move. Before the rule was
removed it favoured the player in every scenario except `veteran`; now it is **neutral** in a
duel (66.7% → 66.0%) and **harmful** for a well-equipped veteran (98.3% → 96.1%). To be able
to say a move has a price, sometimes it must not be made.

### Combat resolution (fully automatic, no manual aiming)
Every clash is resolved in order:
1. The attacker's **Aggression** determines the attack frequency
2. A die for the defender's **Evasion** → if they evade, **stamina is spent, no damage**
3. If they could not evade, **Defence** reduces the damage
4. The remaining damage comes off HP
5. The direction/angle of the blow is for visual variety, it **does not affect the mechanical
   outcome**

### Target selection (locked 2026-09-03)

"Who hits whom" is not an ordering but the **warrior's own decision**. The old rule was not a
decision: pick the nearest, stay loyal to them until they die. A wounded enemy went
unnoticed, a bare region where the kit had fallen apart went unnoticed, and three warriors
did not know whether they were all piling onto the same target.

At every **decision step** (when starting a new move, not on every tick) the warrior scores
the enemies and picks the highest.

| Item | Weight | What it says |
|---|---|---|
| Distance | −1 point / unit | The road to walk. An enemy within range takes no penalty |
| Wound | +120 × missing health ratio | Felling one enemy is better than wounding three |
| Bare region | +90 × ratio of destroyed pieces | The in-combat counterpart of armour wear (§7) |
| Crowd | −60 / teammate on the same target | While the team chases one enemy, the others should not get free hits |
| Stickiness | +80 (to the current target) | The price of changing direction: the ground already covered is wasted |
| **Opportunity window** | 200 units | The wound and bare-region gains fall to zero at this distance |

**No dice — the scoring is deterministic.** The randomness is in the warrior's identity, not
in the decision itself. Had a die been rolled, the choice would jitter at every step and the
measurement would drown in noise.

**Measured (3v3, 20,000 fights, `never`):** the old rule (distance only) gave 72.28% victory
/ 49.11% death; the new rule **72.17% / 50.30%**. Victory is the same, deaths go up — a team
that focuses kills more and the fight ends more sharply. Against a fully kitted enemy
(`3v3-armored`) the new rule is clearly better: 71.84% → **72.68%**.

> **The opportunity window was added afterwards.** An unbounded wound weight simply turned
> the rule into a difficulty increase: the warrior would leave the healthy enemy beside them
> and walk to a wounded one at the other end of the arena, taking free hits all the way
> (72.29% at weight 0, 71.77% at 120, 70.74% at 200 — monotonically down). Adding the window
> straightened the curve.

> **The bare-region weight is dormant for now.** Because at default durability no piece
> falls apart in a single fight (§7), where the rule bites is kit worn down over an
> expedition. Measured with worn kit, the effect is small and against the player (70.06% →
> 69.91%): the enemy sees the bare region too.

> **This is the input for the adversary behaviour decision (#3).** The differences between the
> kinds will not be separate behaviour code but these weights tuned per kind: the cutthroat that attacks
> the wounded, the kabukimono that ignores the crowd, the pursuer with high stickiness.

### ~~No classes~~ → Class system (decision round, 2026-09-07)

> **The old decision, now void:** *"A single character class in the base version: samurai.
> Warriors differ not by class but by weapon proficiency."* The decision round broke this —
> a class system is coming in and stands **side by side** with the Path.

A warrior differs across three layers, and the three are tied to different things:

| Layer | What it says | How it is acquired | Reversible |
|---|---|---|---|
| **Class** | What they **can do** — catching, poison, range | Trainable once the relevant **facility** is built in the dojo; rarely a ready-classed candidate appears in the market | No (limb loss reopens the choice) |
| **Path** | What they **lean towards** — pure stat tendency | A single choice after 20 training days | Never |
| **Mastery** | Which **weapon** they wield well | With a weapon-master staff member, and through use | Re-earned on a new weapon |

There are three **Paths** and they have not changed: Blade (Accuracy/Strength ×1.10), Rock
(Defence ×1.15, Health ×1.05), Shadow (Evasion ×1.15, Speed ×1.10). The multiplier enters
`EffectiveStats` and is applied **beneath** disability.

**Class is what unlocks the special mechanics** — and the mechanic works as a product of
`class × implement`:

```
chance = base × class multiplier × implement multiplier
```

| Case | Example (catching) |
|---|---|
| Right class + right implement | Catcher + sai → full strength |
| Right class + wrong weapon or empty-handed | Catcher + katana → **weak but not zero** (~10%) |
| Wrong class + right implement | Non-catcher + jitte → **zero** |

> **Why a product:** let identity belong to the warrior and efficiency to the equipment. A
> master who drops their weapon is weakened but does not become someone else entirely; and
> handing a recruit a jitte does not make a master either. The price: the two multipliers
> must be swept **together** — the old single-axis measurements (jitte 78.00% / katana
> 75.02% and the like) will be retaken under this rule.

**On being maimed the class is re-chosen, the Path never.** Limb loss makes some classes
impossible; the player chooses from those that remain. The Path is the labour of 20 training
days, and being maimed should not erase it — a man who loses his arm does not lose his
agility; what changes is what he can do.

### Field features (decision round, 2026-09-07)

The arena is not always the same empty plane. Field features enter the resolver **as
multipliers**, they do not open a separate rule set:

| Feature | Effect |
|---|---|
| **Fog** | Vision and target selection degrade, ranged weapons lose value |
| **Mud** | Speed and charge drop; heavy kit is punished twice |
| **Narrow bridge** | Encirclement becomes impossible — a numerical advantage melts away |
| **Night** | Accuracy drops, the surprise first blow gets heavier |
| **Tate (pavise)** | A fixed wooden shield planted in the ground: the warrior behind it is protected from ranged attack. There is **no** hand-carried shield; the tate is not equipment, it is part of the field |

The contract says which field it takes place on; the team and the equipment decision follow
from that.

### Weapon proficiency

Proficiency is kept **per grip, not per weapon name**. Three lines:

| Line | Scope | Status |
|---|---|---|
| **One-handed** | Katana, wakizashi, tantō, kama, ono, tekagi | Active |
| **Two-handed** | Nodachi, naginata, kanabō, bō/jō | Active |
| **Thrown** | Shuriken, kunai, yumi, fukiya | **Active** — the core gained projectiles on 2026-08-14 |

**Proficiency affects only accuracy and attack speed; it never affects raw damage.** If it
worked on damage too, it would collide with Strength and blow up the balance.

**Growth:** both use in combat and training in the dojo. Training is essential — it is where
a maimed warrior trains a new line from scratch; otherwise the right way to play would be
"if they're going to be maimed, let them die", and §7's "retire or keep them" question would
become a sham.

**Recruit penalty:** at proficiency 0 a weapon **can** be wielded, but accuracy is markedly
low. A "cannot wield below a threshold" rule locks up the roster — a new warrior could hold
nothing.

**No transfer between lines** — proficiency belongs to the grip. Transfer happens indirectly
through stats: Strength, Defence, Evasion, HP and Stamina are not tied to a line and remain
when the grip changes.

> **Why per grip — and why it is risky:** `Disability.BlocksTwoHandedWeapons` already exists.
> Which means **a two-handed master who loses an arm loses a lifetime's work.** That sharpness
> of risk is deliberate; but it is not ruinous: the warrior does not start from zero, they
> keep their stats and their experience — they come back as **a veteran with a recruit's
> accuracy**. The one-handed line must also remain trainable from scratch (the training rule
> above).
>
> Two-handed pays for its fragility with a **damage ceiling**: the Nodachi 34 / Katana 22 gap
> is preserved. The exact numbers are Phase 9's job.

---

## 5. Surrender ("Flee") — The Only Intervention in a Fight

- **One key**, an instant decision. **No** mash/QTE. **No** automatic threshold —
  if the player does not press, the warrior really dies.
- **No pulling out before the fight begins.** The key is **closed until the first hit**: the
  threshold is not a move but blood. Through missed moves the team can still get out cheaply
  — that is what armour buys.
- The command covers **the whole team**: once "flee" is said, all 1-3 warriors on the field
  pull out and all of them lose honour. A single warrior cannot pull out separately.

- The command **also ends the expedition**: everything after that room is cancelled, the team
  returns to the dojo and **that expedition's reward is not taken** (see §10).

> **Why team-based:** had it been per warrior, the right way to play would be "pull out the
> moment one takes a wound, carry on with the rest" — a small, lossless, endlessly repeatable
> optimisation. A team-based command makes the decision **rare and heavy**: saving one warrior
> means giving up the expedition and the whole team's honour. The price of the limb-loss
> mechanic is thereby tied to a real price.

### When the key opens

| Moment | Key |
|---|---|
| The fight has started, nobody has struck yet | **Closed** — it reads "FIGHT NOT BEGUN" on it |
| The first hit has landed (whichever side struck) | **Open**, and stays open until the fight ends |

Pressing a closed key is not silently swallowed; the core counts the press and produces an
event, and the interface writes the text:

- **At most 3 times at the start of the game** a short note comes up teaching the rule, then
  it goes quiet. Repeating the known is not information but noise.
- **On the 11th consecutive press in the same fight** a mocking answer comes back and the
  achievement **"Don't roll up your trousers before you see the stream"** unlocks.

> **Why the hit and not the move:** the key needs to open while the enemy drawing a bow is
> aiming from 30 m away; a reach threshold would work wrongly against a ranged enemy. The hit
> threshold says the same thing in melee and at range: *they touched you.*

> **Why closed rather than absent:** had the key been hidden, the existence of the rule would
> not be learned. The player should be wondering not about the key's absence but about when
> it will arrive.

### Animation cancelling (cancel window)
| Situation | Behaviour |
|---|---|
| Idle, approaching, post-attack recovery, block | The command is processed **instantly** |
| **Charge** (§4) | The command is processed **instantly** — the run is cut, the charge is wasted |
| A moment locked into an attack blow | The command is **buffered**, the escape starts when the current motion ends |
| Block stance | Goes to escape almost instantly |

### Block (locked 2026-09-03)

Block is now **a separate state** in the core (`CombatState.Blocking`) — it used to dissolve
into the Defence stat, and this table counted it as separate too. The gap is closed.

| Item | Rule |
|---|---|
| **Decision** | The warrior makes it: chance = `Defence ÷ 100 × 0.45`. No floor — a warrior with Defence 0 never blocks (the same shape as evasion, not as the charge) |
| **Condition** | Not proximity but a **read move**: an enemy within range must have their blade gathered (`AttackWindup` or a running charge). Range is read as the greater of the two — the short-bladed warrior facing a long haft is under threat too |
| **Duration** | 0.8 s, during which the warrior **does not strike**. That is the price of a block, and it is dearer than evasion: evasion erases a blow, a block buys time |
| **Rhythm** | A block does not follow a block. Had the die been rolled again at every step, a warrior with high defence would lock up the fight without ever striking |
| **Frequency** | **Read from the stat** (the decision row above). How often you block is determined by Defence |
| **What it holds** | **Read from the equipment.** 70% of the damage × the weapon's block quality (two-handed 1.0, blunt 0.85, cutting 0.80, piercing 0.70, **fist 0.30** — a warrior who drops their weapon loses their block too) × the shoulder piece's share. The ***ō-sode*** in the shoulder slot carries the armour side of the block: there is no shield in hand, the *tate* is a fixed pavise planted in the ground and enters as a **field feature** (decision round, Section 5) |
| **Limb** | A blocked blow **does not sever a limb**. The one firm promise the Defence stat gives against the signature penalty |
| **Concussion** | A blunt weapon's share works at 75% despite the stance. With no shield, this is the blunt class's fourth gain: the stance protects you from steel, not from concussion |
| **Flank/rear** | The encircled cannot block — a blow from behind does not see the stance |
| **Escape** | The command cuts the stance **instantly** (the table above) |

**Measured (3v3, 20,000 fights, `never`):** with the rule off, victory 71.21% / death 50.44%
/ limb loss 5.19%; with it on, **72.39% / 49.19% / 4.96%**. 0.20 blows per warrior are met.

> **The first version did not work.** When the stance was taken merely on "is there an enemy
> in range", it was taken blindly: still 0.20 blows met per warrior, but it **lowered**
> victory (71.21% → 70.50%), because stances taken for nothing ate into the attack cycle.
> Turning the condition into "read the incoming blow" flipped the rule's sign.

> **It has no brake of its own; its footing is the stat.** As `MaxBlockChance` grows the
> player gains monotonically (71.61% at 0.25, 72.39% at 0.45, 74.08% at 1.0) — the rule has no
> internal brake. The brake is provided by the Defence stat competing with other stats in the
> dojo. The number will be revisited in Phase 9.

> **Block was not favoured for catching implements.** When the jitte/sai were given the same
> block quality as a two-handed weapon, the measurement broke a locked brake: the jitte
> stopped being the wrong choice in front of an enemy carrying a heavy weapon (jitte-heavy
> 35.38% > katana-heavy 34.96%). The override was removed; both now carry the quality of a
> one-handed blunt implement (0.85).

### The price of escape is a ladder

The key is not a trade — there is no rule that says "give this much and get that". At the
moment it is pressed the outcome is **unknown**; the pull-out ends on one of the rungs
below. The moment you press slides you down this ladder in one direction only.

| Rung | What happens |
|---|---|
| **1** | ~~Everyone fled, nobody was hurt~~ — **no longer reachable**, see below |
| **2** | Everyone fled, there are wounded |
| **3** | Everyone fled, there are limb losses |
| **4** | Part of the team fled, the rest died — those who fled are intact |
| **5** | Part of the team fled, the rest died — those who fled have lost limbs |
| **6** | Nobody got away |

Measured (3v3, 20,000 fights, light kit, the contact rule in force). The columns represent
when the key was pressed:

| Rung | The moment the key opens | 2nd s | Health 50% | When outnumbered |
|---|---|---|---|---|
| 1 · all fled, unhurt | **0.0%** | 0.0% | 0.0% | 0.0% |
| 2 · all fled, wounded | 92.8% | 91.8% | 10.6% | — |
| 3 · all fled, limbs lost | 7.2% | 8.2% | 2.3% | — |
| 4 · partial escape, no limb loss | — | — | 71.1% | 11.1% |
| 5 · partial escape, limbs lost | — | — | 12.9% | 2.7% |
| 6 · nobody got away | — | — | 3.1% | **86.2%** |

> **The first rung was closed (2026-08-29).** Pressing before contact used to bring the team
> home spotless in **35%** of fights; the measurement gave **zero** limb losses and **zero**
> deaths over 20,000 fights. It had a structural branch: the escape die deals at most 12
> damage, against 100 maximum health its severity stays at 0.12 and never crosses the 0.20
> heavy-blow threshold. So the "no limb goes without a weapon touching it" rule made
> pre-contact escape free of charge.
>
> The fix was **not** to add a new die to being maimed, but to delay the key: there is no such
> thing as pulling out before anyone has touched you. No limb is drawn coming off for no
> reason on screen (there is only a `Stumble`, no severing animation) — but the rung is not
> free either: even when pressed the moment the key opens, **7.2%** of teams lose at least
> one limb.

### The three things that made escape no longer free

The top of the ladder gave, at one point, **100% unhurt exits**: fleeing carried no risk at
all. The causes were found one by one and all three fixed together.

| Why it was free | What was added |
|---|---|
| Speed was a single constant; pursuer and fugitive moved at the same speed and the net closing stayed zero | **Speed became a stat.** Oni slow (25), Kappa medium (55), Tengu fast (85). The one fleeing also slows down — they are running with their back turned |
| Melee could not reach the far half of the arena; beyond a certain distance the fugitive was untouchable | **Throwing woke up** (§4). A projectile spends time in the air; during the flight the target can flee, die or leave the field |
| Leaving the arena itself cost nothing | **The escape die.** A twisted ankle, a wound bleeding on the way back. It does not kill — it never takes health below 1 |

> **The chase has a rule of its own.** Normally a warrior locked into a move cannot walk.
> Against a fleeing target this is suspended: the pursuer runs while swinging their blade.
> Without this, the hunter would catch up, begin a move, freeze for the length of it, the
> fugitive would leave range and the blade would come down on empty air **every single time**
> — that is, an enemy who caught up could never strike. During recovery they do stop; otherwise
> the pursuer never slows down and escape collapses entirely (measured: 76% of teams pulling
> out while outnumbered were broken).

> **Pressing early is still the best option** but there is no longer such a thing as a clean
> exit: pressed the moment the key opens, **92.8%** of teams come home wounded and **7.2%**
> with a limb lost. The real price of fleeing is still **the expedition itself** — it is
> cancelled, the reward is not taken, the whole team loses honour.
>
> The price of pressing late is harsh: pulling out after being outnumbered costs the entire
> team in **86%** of fights. What the ladder says is clear — *if you are going to flee, flee
> early.*

### The honour price of fleeing

Pulling out lowers honour. From two separate items:

| Item | Where | Note |
|---|---|---|
| The penalty inside performance | `HonorTuning.EscapePerformancePenalty` | Blended with the hit rate: someone who fought well and then pulled out is still better than someone who fought badly |
| The key's flat price | `HonorTuning.RetreatHonorPenalty` | Independent of performance, applied on every pull-out |

The second item stands apart because on its own the penalty buried in performance let a
sufficiently accurate warrior **gain** honour despite fleeing. The price of pulling out must
be visible independently of how well the fight went.

> **The number has not been decided yet** (Open Decision #8). The value in the code is a
> placeholder; per the measure-before-deciding rule it will settle in Phase 9.

### The escape window (defencelessness)
From the moment the escape starts until leaving the arena:
- The warrior **cannot use Evasion or block**
- The opponent can strike with **increased accuracy**
- **Every enemy in range** gains an opportunity attack — that is, the price depends on how
  many enemies have surrounded you (§4)
- **A faster enemy gives chase** and keeps striking if they catch up
- **A ranged enemy shoots from behind**; if the projectile arrives before you leave the field
  it hits
- The escape ends not on a counter but on **distance**: the warrior really has to leave the
  arena, and stays defenceless for that whole time
- On leaving the arena an **accidental wound** die is rolled (it does not kill)

---

## 6. Honour System (Bushi / Ronin)

### The scale
A permanent 0-100 stat, starting at 50. Slow **decay** towards neutral over time (so a troll
raid is not a permanent penalty; only sustained dishonour should lead to seppuku).

### Chat commands
| Command | Effect |
|---|---|
| `!bushi` | Honour (+) to the warrior(s) in the active fight |
| `!ronin` | Honour (−) to the warrior(s) in the active fight |
| `!bushi-<name>` | Targeted, at a warrior outside the fight (+) — **small effect** |
| `!ronin-<name>` | Targeted (−) — **small effect** |

- The honour change coming from combat performance is **large**; targeted commands have a
  **small** effect (against griefing).
- ~~If the name is not found (dead/absent) it is **silently ignored** — no feedback to chat.~~
  **Void (2026-09-07):** the game now connects to chat through a **bot** and reports the
  command error (§8).
- A name can belong to **at most one living warrior** at a time → no ambiguity in name
  resolution. (X dies, then a new X can arrive.)

### Economic effect (reward multiplier)
```
bushiRatio = bushi / (bushi + ronin)          // neutral if there are no votes
rewardMultiplier = clamp(0.75 .. 1.25)        // 100% ronin = 0.75x, 100% bushi = 1.25x
```

> **The band was narrowed (decision round, 2026-09-07).** The old band was 0.5-1.5. 0.5 meant
> a silent or hostile chat could sink the economy single-handedly; 1.5 meant hype rendering
> balance measurements meaningless. **Chat should colour the reward, not determine it.** The
> new band will be measured in `Domina.Sim` — since the same ratio is also used for the chance
> of surviving a surrender, that will be re-measured too.
The **ratio** is used rather than the raw count → fairness between small and large chats.
The lower/upper bounds prevent spam from dragging it to the extremes.

The same ratio is also applied to the **chance of surviving** after a surrender (an
honourable withdrawal or a cowardly flight).

### Seppuku
If honour falls below the threshold the warrior **does not die immediately** → it goes to a
vote.

**Queue mechanics:**
1. A warrior who falls below the threshold enters a **queue**
2. If there is an active fight, they **wait until it ends**
3. When the fight ends a **60-second** voting window opens
4. There are **never two votes at once** — if more than one warrior falls below the threshold
   they are processed in order
5. During the voting window `!bushi` / `!ronin` count **only towards this vote**
6. **One vote per user** (spam protection)
7. Bushi majority → **pardon**, honour recovers to a little above the threshold
   Ronin majority → **seppuku** (permadeath)
8. **Even a single vote** is valid; there is no minimum turnout threshold
9. If **zero votes** come in, **the AI decides**

**Cooldown:** a pardoned warrior gains **15 minutes** of immunity. During that time no new
vote is triggered even if their honour falls below the threshold.

**Threshold — provisional number (2026-09-04):** **30** on a scale of 100; a pardon pulls
honour up to **45** (`HonorTuning.SeppukuThreshold`, `PardonedHonor`). The user's decision,
to be played with in playtest — §14 #8 stays open.

> The old 12 was stuck to the bottom of the scale: a warrior only got there through
> back-to-back disasters, so the vote almost never opened and chat's heaviest decision was
> practically absent from the game. 30 puts the warrior a **measurable** distance from neutral
> (50). A pardon at 45: above the threshold but below neutral — a pardoned warrior should not
> fall straight into a new vote, but a pardon is not absolution either; the warrior gets up in
> debt.

---

## 7. Wounds and Limb Loss

### Trigger
It can happen at **any moment** of the fight (including the first blow). Low HP is **not** a
precondition. If a single blow's **ratio to max HP** crosses the threshold, the risk of limb
loss arises.

**Factors affecting the risk:**
- The blow/maxHP ratio (a hard hit = high risk)
- Weapon type: cutting (sword/axe) → limb loss; blunt (mace) → stunning (see "Stunning"
  below)
- Armour/defence level **reduces** the risk (making investment in equipment meaningful)

### Stunning (locked 2026-09-02)

The same **heavy blow** rolls two separate dice: limb severing and **stunning**. Which die
lands depends on the weapon's class — that is the trade:

| Class | Limb-severing multiplier | Stunning multiplier |
|---|---|---|
| Cutting (katana, nodachi) | 1.0 | 0.25 |
| Piercing (yari) | 0.5 | 0.15 |
| Blunt (tetsubo, kanabō) | 0.15 | **1.0** |

**A stunned warrior freezes for 0.9 seconds:** they do not walk, do not strike and **cannot
evade**. The closing of evasion is the rule's real tooth; in measurement it bites harder than
the lost move.

| Number | Value |
|---|---|
| Threshold at which the die is rolled (blow/max health) | 0.20 |
| Base chance | 0.35 |
| Freeze duration | 0.9 s |
| Multiplier for a blow to the head | 2.0 |
| The counted share of the armour's severing resistance | 0.6 |

**Two protective rules:**

- **A warrior pulling out cannot be stunned.** Because stunning freezes them, an enemy with a
  blunt weapon could cancel the player's only intervention (§5) on a single die
- **A stunned warrior is not stunned again** — the duration is not refreshed, there is no
  lock-up. When it ends the warrior returns to their normal cycle; a "Flee" key pressed in the
  meantime is **not swallowed**, it is held and processed there

> **Why this rule was written:** the blunt weapon was losing to cutting on the severing
> multiplier (0.15 against 1.0) and getting nothing in return. Measured (same warrior, same
> enemy, only the weapon differing; 20,000 fights): without the rule, cutting takes 91.57% and
> blunt 88.68% of victories — blunt is worse on every axis. At a base chance of 0.35 the two
> are level at 92.06% / 92.08%. At 0.60 blunt pulls ahead, at 1.00 it becomes dominant.
>
> The player pays for it too: in 3v3 the Oni's tetsubo now bites and the player's victory
> falls from 69.31% to 65.20%. Absolute balance is Phase 9's job; the numbers here hold the
> **ratio** between classes.

### Blade catching — jitte and sai (locked 2026-09-03)

§4 rejects the hand-carried shield (the *tate* is a fixed pavise planted in the ground, and
an arm shield was not common in Japanese warfare) and said that the same mechanical need is
met by the **jitte/sai**. The rule is now in the core.

**Catching is the second defensive axis, tried before evasion.** Evasion makes the blow miss
and it ends there; catching **erases** the blow and leaves the attacker exposed for **0.6
seconds** — a locked warrior does not walk, does not strike, cannot evade.

The die is fed by three things:

| Multiplier | Where it is read from | Values |
|---|---|---|
| Grip (`CatchSkill`) | The defender's implement | jitte 1.0, sai 1.25, everything else 0 |
| Catchability (`CatchFactor`) | The attacker's weapon | cutting 1.0, piercing 0.7, blunt 0.25, fist 0 |
| Leverage | If the attacker's weapon is two-handed | ×0.75 |

| Number | Value |
|---|---|
| Base chance | 0.24 |
| Lock duration | 0.6 s |
| Stamina cost | 16 |
| Ratio added at Accuracy 100 | 0.5 |

**Catching is tied to Accuracy, not Evasion.** Had both defensive axes fed from the same stat,
the equipment decision would be a copy of the stat decision and the jitte would be merely "the
second defence of a high-evasion warrior".

**Three protective rules:**

- **Only melee is caught** — a projectile arriving through the air does not settle into the
  hook
- **A blow from behind is not caught**: a weapon you cannot see cannot be held, and if the
  rule worked here too the price of encirclement (§5) would be erased
- **A warrior pulling out does not catch** — the twin of the protection in stunning: no new
  die is placed on top of the escape promise

The two implements (14 damage / jitte 1.00 s, sai 1.05 s) are deliberately in the **blunt**
class: neither is sharp, both are for holding and jabbing. The difference between them is not
damage but **volume** — the sai catches 3.71 per fight, the jitte 2.75.

> **Measurement (1v1, 20,000 fights, `losing:0.7`; the control is a katana on the same body):**
>
> | Weapon | Victory | Limb loss | Catches/fight |
> |---|---|---|---|
> | Katana (control) | 73.09% | 0.90% | 0.00 |
> | Jitte | 72.63% | 0.52% | 2.75 |
> | Sai | 72.73% | 0.45% | 3.71 |
>
> The base chance turned here: at 0.15 the jitte gets 61.16%, at 0.20 68.53%, at 0.30 77.22%.
> At 0.24 the katana stays ahead on victory and the catching implements take **not coming home
> maimed** — every option is best at something.
>
> **A heavy weapon is the answer to catching.** When the enemy carries a two-handed nodachi the
> jitte wins 29.37% and the katana 34.78%, and the jitte also loses its limb protection (11.77%
> against 11.42%) — that is, carrying a jitte against a nodachi is simply the wrong choice. At a
> leverage multiplier of 0.5 this hole widened to 14 points (21.04%); 0.75 turns the trap into a
> preference.
>
> **The lock duration does almost nothing in measurement** — the same finding as with the stun
> duration. In 1v1, going from 0 s to 1.2 s only moves it from 72.44% to 73.72%. The window that
> opens coincides with the gap the warrior was already waiting through in their own attack cycle;
> where the rule bites is **the erased blow**. In 3v3 the team value did not separate out either,
> but for a different reason: there, a recruit carrying a jitte sees only ~0.57 **catchable**
> blows per fight (the ceiling was measured with `--catch-chance 1.0`). 0.6 s was chosen because
> it is long enough to read on screen and below the point where the trade turns.
>
> **Weapons being dropped changed this table too (2026-09-03).** The three numbers above were
> measured before that rule. With the rule, katana 75.02% / jitte 78.00% / sai 78.88%: in front
> of a sword-carrying enemy the catching implements are now ahead on victory as well, because
> they wrench the weapon they catch out of the palm. What the katana is "best at" moved — not
> victory but **an armoured enemy** (60.81% against the jitte's 34.14%). The leverage brake still
> stands: against a nodachi, jitte 35.22%, katana 37.84%.
>
> **The stamina cost bites not by making catching rarer but by tiring the warrior.** At a cost of
> 0 victory is 76.85%, at 16 it is 72.63% — but the number of catches is the same in both (2.72 /
> 2.75). At 8 it binds not at all (76.85%), at 30 it is ruinous (41.07%).

### Poison (locked 2026-09-03)

Poison is **the only way around damage reduction**. Plate stops a cut, stops a share of blunt
force (0.6 above) and stops poison not at all — a dose works on the blood, not on the armour.
The price of that is that the weapon's own steel is light.

**The rule.** Every hit landed by a poisoned weapon leaves a **dose** on the defender — no die
is rolled; if the edge grazed the skin the poison went in too. A dose eats health once per
second; the damage passes through neither the armour nor the Defence stat. Doses **stack** (up
to the cap), and the duration is reset with every new hit.

| Number | Value |
|---|---|
| Damage per tick (at dose 1) | 2.5 |
| Tick interval | 1.0 s |
| A dose's lifetime | 6.0 s |
| Maximum dose | 3.0 |
| Poisoned tantō | 7 damage / 0.85 s (clean tantō 13/0.85) |
| Poisoned shuriken | 12 damage, 2 rounds of ammunition (clean shuriken 12, 4 rounds) |

**Three limiting rules:**

- **Poison neither severs limbs nor stuns.** Both are consequences of *a blow*; in poisoning
  there is nobody striking. A poisoned weapon takes no share of the game's signature mechanic —
  that is half of the trade
- **Death by poison is a separate cause** (`DeathCause.Poison`): no blow on the field felled
  the warrior
- **The poison in a warrior pulling out does not stop.** Stunning and catching do not apply to
  someone pulling out so that no *new die* is placed on top of the escape promise; poison is not
  a new die but the continuation of a price already paid — the key is not an antidote

> **Measurement (1v1, 20,000 fights, `losing:0.7`):**
>
> | Weapon | Unarmoured oni | Oni in ō-yoroi |
> |---|---|---|
> | Katana (control) | 73.09% | 68.62% |
> | Clean tantō | 31.14% | 1.23% |
> | Poisoned tantō | 72.19% | **77.19%** |
>
> The poisoned blade is level with the katana in an open fight and pulls ahead in front of
> armour. **Wearing ō-yoroi against a poisoner is a loss:** the plate does not read the dose, and
> its weight slows the blow.
>
> **The first setup gave the wrong answer.** With the blade left at 13 damage, poison was merely
> rescuing a weak weapon (74.00% / 55.99%) — it never got past armour at all, because most of its
> output was still steel. Only once the blade was dropped to 7 and the dose raised, so that 60% of
> the output went to poison, was the claim confirmed.
>
> **The real knob is the dose cap:** at 1 the poisoned blade is plainly bad (16.71%), at 2 still
> behind (50.92%), at 3 level, at 5 dominant (82.25%). A dose's **lifetime**, meanwhile, does
> almost nothing past 6 seconds (3 s 52.90%, 6 s 72.19%, 9 s 73.11%): a fast weapon is
> continuously refreshing the duration anyway. The tick interval is not a neutral knob but
> directly the damage rate (94.93% at 0.5 s, 30.40% at 2 s); 1 s, divided into the dose's
> lifetime, makes poison **countable** — six hits.
>
> **Weapons being dropped changed this table (2026-09-03).** The numbers above were measured
> before that rule; with it, the poisoned blade also gets dropped when it strikes ō-yoroi (20.18%)
> and the table becomes: katana 75.02% / 60.81%, poisoned tantō 74.26% / **69.39%**. Poison's
> claim stands — it is still the only real answer in front of armour — but **"wearing heavy kit
> against a poisoner is a loss" is no longer true**: dropping gave armour back the share it had
> lost, and plate now earns 5 points in front of a poisoned blade (14 in front of a sword).
>
> **When the poison is turned on the player** (3v3, the tengu throwing poisoned shuriken; the
> control is the same roster): victory falls from 65.20% to 60.35%, escape from 8.10% to 6.86%,
> death rises from 45.45% to 50.44%, and 1.3% of deaths are directly from poison. §5's ladder
> stands — escape still works, it is just dearer.

### Dropping a weapon (locked 2026-09-03)

Poison went around armour; dropping writes armour's **answer**. Plate now not only lowers
damage, it also **breaks the grip** of whoever strikes it.

**The rule.** It comes from two places:

1. **A blow landing on armour** can knock the attacker's own weapon out of their hand. The die
   comes from the weapon's tendency to leave the hand and the hardness of **the piece struck**;
   hardness is read from that piece's severing resistance. A blow landing on a bare region
   **never** causes a drop — what breaks the grip is not flesh but the shock coming back off
   plate
2. **A caught weapon** can be wrenched out of the palm at the hook. This die does not stack **on
   top of** the lock, it **replaces** it: with the weapon leaving the hand the bind is also
   released, and the attacker comes free — but weaponless

The weapon **does not break, it falls**: it comes to rest at a point in the arena and returns
to its owner when the fight ends. The reason for this is deliberate — breakage would open an
inventory, repairs and a spare-weapon ledger; the game's question is not equipment upkeep but
how **that fight** is going.

**Picking up.** A dropped weapon can be picked up by **anyone with an empty hand**: the one who
dropped it, a teammate, an enemy. There are two limits:

- **Someone with a weapon in hand neither picks up nor looks.** They will not take a single step
  towards the blade on the ground; otherwise the fight would turn into a looting round
- **They do not pick up a weapon they cannot use**: a warrior who has lost an arm walks past a
  two-handed weapon on the ground

A weaponless warrior keeps fighting — with fists (8 damage, range 100). The size of the loss
varies from weapon to weapon: a warrior who drops their nodachi gives up 34 damage and 150 range
at once.

| Class | Tendency to leave the hand | Rationale |
|---|---|---|
| Cutting | 1.0 | An edge that lodges in plate twists |
| Piercing | 0.6 | The tip slides, the haft stays in the palm |
| Blunt | 0.2 | A staff bouncing back does not leave the palm |
| Fist | 0 | There is nothing to drop |

| Number | Value |
|---|---|
| Base chance on a blow landing on armour | 0.05 |
| Chance a caught weapon is dropped | 0.05 |
| Hardness share (from the piece's severing resistance) | 1.0 |
| Throw distance | 250 units, **behind the opponent** |
| Pick-up distance | 60 units |

**Three limiting rules:**

- **A projectile drops nobody's weapon** — what is thrown has already left the hand
- **Dropping does not touch the permanent state.** The core only produces the events
  (`WeaponDropped`, `WeaponPickedUp`); the warrior leaves the fight with their own kit
- **The die is rolled against the attacker's weapon**, not the defender's

> **Measurement (1v1, 20,000 fights, `losing:0.7`, the enemy in ō-yoroi):**
>
> | Weapon | Rule off | Rule on | Drops | Picked up |
> |---|---|---|---|---|
> | Nodachi (cutting) | 94.25% | 87.53% | 11.76% | 7.3% |
> | Tetsubo (blunt) | 90.55% | **89.20%** | 2.73% | 4.8% |
> | Yari (piercing) | 79.69% | 75.48% | 9.86% | 8.4% |
>
> **The trade turns in front of plate.** Without the rule, cutting is ahead even against an
> armoured enemy (94.25% against 90.55%); with the rule, blunt pulls ahead. This is the blunt
> class's third gain, and it appears exactly where armour is thickest.
>
> **The base chance was swept** (cutting / blunt victory): at 0.02 it is 91.50% / 89.77% — cutting
> is still ahead, the rule turns nothing. 0.05 is where the trade flips. At 0.08, 84.09% / 88.78%;
> at 0.20, 72.93% / 86.83%: a cutting weapon becomes uncarriable in front of an armoured enemy.
>
> **What carries the price is not distance but direction.** If the weapon falls behind its owner,
> the walk to pick it up pulls the warrior back out of the fight, and in front of a **slow** enemy
> a drop becomes a free breather — measured: as the throw distance grew the player's victory
> *rose* (94.30% → 94.55%, against 94.25% with the rule off). Being thrown sideways comes to the
> same thing (94.4%). When it falls behind the opponent the price becomes real, because going for
> the weapon means walking through the enemy. Distance itself is no longer a knob after that: 150,
> 250 and 400 units give the same result (87.43% / 87.45% / 87.48%).
>
> **Picking up does not work one-on-one, it works in a crowd:** in 1v1, 7.3% of dropped weapons
> are recovered; in 3v3, 40.4%. Because the targeting is split, in a crowd you can get to your
> weapon — that is, the rule's price varies with the shape of the fight rather than being a fixed
> penalty.
>
> **The drop chance on a catch was swept separately** (`jitte`/`sai`/`katana`, the enemy armed
> with a sword and unarmoured): at 0, 74.87% / 75.22% / 75.02% — a catching implement gains
> nothing from dropping weapons. At 0.05, 78.00% / 78.88% / 75.02%. **At 0.10 the brake breaks** —
> the jitte becomes the right choice against a nodachi too (38.27% against the katana's 37.84%)
> and `CatchTwoHandedFactor` becomes meaningless.
>
> **The rule does not make catching the superior weapon**, because two brakes still stand:
>
> | Matchup | Jitte | Katana |
> |---|---|---|
> | Sword-armed, unarmoured oni | **78.00%** | 75.02% |
> | Oni carrying a two-handed nodachi | 35.22% | **37.84%** |
> | Oni in ō-yoroi | 34.14% | **60.81%** |
>
> Every option is still best at something: the jitte against the sword, the katana against
> armour, the sai for limb protection (0.48%).
>
> **The team price** (3v3): when all the enemies wear full kit, victory falls from 67.32% to
> **65.35%** and 9.32% of the player's warriors drop their weapon during the fight. Without the
> rule, armouring the enemy **helped** the player (65.20% → 67.32%; heavy kit slows the blow); the
> rule inverts that. Because 40% of dropped weapons are recovered in a crowd, the wall is not as
> hard as it is one-on-one.

### Armour is slot by slot

Armour is not a single number; it consists of separate pieces for **head / body / sword arm /
off arm / right leg / left leg** — six slots. Damage reduction and limb-severing resistance are
read from the piece for **the region the blow lands on**.

| Set | Head | Body | Sword arm | Off arm | Right leg | Left leg |
|---|---|---|---|---|---|---|
| Light keikogi | — | keikogi | — | — | — | — |
| Dō-maru | — | dō | kote | kote | suneate | suneate |
| Ō-yoroi | kabuto | ō-yoroi cuirass | heavy kote | heavy kote | heavy suneate | heavy suneate |

> **Why limb by limb (2026-09-02):** a single "arm" slot counted the two vambraces as one piece
> and could not represent the remaining arm of a warrior who had lost one. Split apart, it becomes
> possible to armour a single arm and the loss itself becomes sided: the sword arm and the off arm
> are separate losses (see "Permanent penalties" below).

> **Why slot by slot:** as long as resistance stays a single scalar, "good armour" advances on one
> axis and the genuinely interesting equipment decision — **a heavy cuirass, bare arms**: cheap and
> fast, but with a high chance of coming home armless — never exists at all. This shows directly in
> measurement: because the dō-maru leaves the head open, eye loss only halves, unlike arm and leg
> loss.

### The weight of armour — locked (2026-09-02)

As long as armour has no in-combat price, kit is not a **decision** but a wallet check: in
measurement ō-yoroi raises victory from 69% to 92%, lowers death from 45.5% to 21% and limb loss
from 7.45% to 0.61%, and pays nothing in return. Its only brake was price, and that does not exist
until the economy numbers (Open Decision #5) arrive.

Weight carries this onto the field. Every piece has a weight; the set's total **lengthens the
attack cycle**.

| Piece | Weight | | Set | Total |
|---|---|---|---|---|
| Keikogi | 1 | | Unarmoured | 0 |
| Dō-maru cuirass | 4 | | Light keikogi | 1 |
| Ō-yoroi cuirass | 7 | | Dō-maru | 7 |
| Kote / heavy kote | 1.5 / 3 | | Ō-yoroi | 16 |
| Suneate / heavy suneate | 1.5 / 3 | | | |
| Kabuto | 3 | | | |

| Number | Value | What it does |
|---|---|---|
| `ArmorWeightAtFullPenalty` | 16 | The weight at which the full penalty applies — complete ō-yoroi |
| `ArmorAttackSlowdownAtFullWeight` | 0.75 | How much the attack cycle lengthens in full kit |

**0.75 is the threshold at which the trade turns.** All three tiers end up best at something: the
dō-maru wins the fight (71.8% victory, 40.3% death), ō-yoroi buys not coming home maimed (0.82%
limb loss against 3.38%), and light kit is cheap and weightless. At 0.60 ō-yoroi is still ahead on
every axis (76.3% victory, 37.0% death); at 0.90 heavy kit is plainly bad (64.3% victory).

### Armour wear and destruction (locked 2026-09-03)

Armour had two prices: cost and weight. This is the third — **armour is a consumable.**

**The rule.** A piece wears by as much damage as it stops: every point it absorbs comes off its own
durability pool. When the pool runs out the piece **falls apart on the spot** and that region is
bare for the rest of the fight; a destroyed piece is **gone permanently**. The difference from
weapons is deliberate — a dropped weapon comes back at the end of the fight, a shattered plate does
not.

**Wear belongs to the warrior, not to the fight.** A piece is not used up in a single fight; it
wears down over expeditions and one day falls apart in the middle of a fight nobody expected. The
core does not touch the permanent state: it writes the wear and the destroyed slots into the fight
summary (`ArmorDestroyed`), and the dojo keeps the ledger — the same path as with limb loss.

**The pool is reduced by the damage absorbed, not by the damage incoming.** What wears a piece is
the blow it stops. Had it come off the incoming damage, thin cloth and thick plate would be used up
at the same rate and the tier difference would exist only on paper.

| Piece | Reduction | Durability |
|---|---|---|
| Keikogi | 4 | 40 |
| Dō-maru cuirass | 9 | 110 |
| Ō-yoroi cuirass | 14 | 180 |
| Kote / heavy kote | 4 / 6 | 45 / 75 |
| Suneate / heavy suneate | 4 / 6 | 45 / 75 |
| Kabuto | 8 | 90 |

A destroyed piece **also leaves its weight behind**: a warrior whose protection is gone gets their
speed back. In the same way it leaves its hardness behind — a region left bare no longer knocks the
enemy's weapon out of their hand.

> **Measurement (3v3, 20,000 fights, `losing:0.7`):**
>
> | Kit | Wear per fight | The team's total durability | Lifetime |
> |---|---|---|---|
> | Light keikogi | 5.4 | 40 | ~7 fights |
> | Dō-maru | 20.2 | 290 | ~14 fights |
> | Ō-yoroi | 38.7 | 570 | ~15 fights |
>
> The numbers confirm the rule's real claim: **the kit that absorbs the most is the kit that wears
> the most.** Ō-yoroi absorbs seven times the damage of light kit and carries slightly more than
> seven times the durability in return — expensive kit buys protection, and not for free.
>
> **No piece falls apart in a single fight** (zero across 20,000 fights at default pools); the
> moment of destruction comes when you take the field in worn kit. That moment was measured by
> scaling the pools down: at 0.25, 0.12 pieces are destroyed per warrior (victory 69.92%, limb loss
> 0.94%); at 0.1, 0.89 pieces (victory 67.55%, limb loss **1.93%**). That is, going into a fight in
> worn armour is not just taking more damage but **doubling limb loss** — the price of not renewing
> the kit shows up in the roster.
>
> **The end of the chain:** an enemy whose armour has fallen apart no longer knocks anyone's weapon
> out of their hand. With all the enemies in full kit the rate at which the player's weapon is dropped
> is 1.81%; when they enter in worn kit it falls to 1.41% (victory 65.35% → 63.83%). As the armour
> goes, the fight becomes **deadlier but cleaner**: more cuts, fewer dropped weapons.

### Two lines that were tried and dropped

Weight could have bitten in three places; two fell in measurement.

- **Stamina recovery: measured zero.** Cutting regeneration by 90% in full kit moved 3v3 victory
  from 92.34% to 92.33%. Stamina is not binding in this fight anyway; the knob was deleted.
- **Walking speed: the gain could not be measured, and the price was §5's promise.** A speed penalty
  did not move victory at all but rendered the "Flee" key useless: because the escape ends on
  **distance** rather than a counter (§5), a kitted warrior is caught before they can leave the
  arena. The death difference the key buys fell from 2.0 points to **0.0 points** at a 25% penalty
  (46.35% pulling out, 46.44% not; without the penalty, 44.32% against 46.33%). That is why weight
  does not touch walking.

The one remaining line is attack speed, and the reason is the same in both cases: **the fight is
decided by the exchange of damage**, and any penalty that does not touch that exchange does not
measure.

### Hit region

Every hit lands on a region. The weights are deliberately unequal:

| Region | Weight |
|---|---|
| Body | 45 |
| Leg (12.5 each) | 25 |
| Arm (10 each) | 20 |
| Head | 10 |

> **Why the body dominates:** had the regions been equal, body armour would be just one of four
> pieces and would lose its value; armour investment would turn into a flat "spread it evenly"
> optimisation. With the body dominant, body armour becomes the main investment and limb armour a
> specialisation decision.

The region concerns **damage and armour**; not the outcome tree. A heavy blow to the body also costs
a limb — the severed limb is then chosen from among the remaining limbs with the same weights.

> **The measured reason:** when "a blow to the body should not sever" was tried, intervention became
> almost riskless and the player's victory went from 36% to 53% (10,000 fights, 3v3). Whereas §7's
> promise is "pressing the key saves a life but **is not free**". The region is not allowed to
> override the outcome tree.

### The outcome tree

When a heavy blow lands the severing die, what decides is **whether the blow is lethal**:

```
heavy blow + severing die landed
  ├─ health > 0  → the limb goes, the warrior stays on the field, THE FIGHT CONTINUES
  └─ health ≤ 0
       ├─ the player pressed "Flee" → they lose the limb but LIVE
       └─ did not press             → DEATH (gore/finisher animation)
```

| Case | Outcome |
|---|---|
| Light/medium blow | HP damage only, no permanent effect |
| Heavy blow, not lethal | **They lose the limb and keep fighting** — no key needed |
| Heavy blow, lethal + the player surrendered in time | **They live but lose the limb** — permanent stat penalty + visual change |
| Heavy blow, lethal + no intervention or too late | **Death** |

> **Why this was split in two.** Under the previous rule limb loss only occurred while
> `PlayerIntervened` was set; and that flag is only raised by the "Flee" key, which per §5 **ends
> the expedition**. The result: **winning while losing a limb was mathematically impossible.**
> 20,000 fights were measured, victory + limb loss: **0 times**. Every warrior who came home maimed
> was a monument to an abandoned expedition; a one-armed champion could never come home. This
> contradicts the "retire them or keep using them" decision below — that decision assumes the maimed
> veteran has won something.
>
> The weight of the key is preserved. But the key is **not a trade**: "turning death into limb loss"
> is only one branch of the tree. The key starts a **pull-out** whose outcome is unknown at the
> moment it is pressed; its price lands somewhere on the ladder in §5.

### How often it should happen

Limb loss is the game's signature mechanic; it should not be a rare surprise but **a regularly
experienced outcome**. The rate is **not a single number but a function of the kit** — a well
armoured warrior comes home intact, one in cheap kit does not.

Measured (3v3, 20,000 fights, `losing:0.7` player model, weight locked). This table is the balance
target:

| Kit | Exposed | Weight | Death | **Limb loss** | Victory |
|---|---|---|---|---|---|
| Unarmoured | all | 0 | 45.5% | **7.45%** | 69.1% |
| Light keikogi | arms, legs, head | 1 | 42.5% | **6.04%** | 71.3% |
| Dō-maru | head | 7 | 40.3% | **3.38%** | 71.8% |
| Ō-yoroi | — | 16 | 41.6% | **0.82%** | 71.5% |

A mixed mid-game roster (mostly in light/medium kit) thus produces roughly **5%** maimed — but that
is not a constant, it is the result of the player's equipment spending.

> **The key does not determine the rate; it determines deaths.** In the same kit, the limb loss of a
> player who never pulls out and one who pulls out early is almost identical (8.8% against 8.0%),
> while deaths fall from 48% to 29%. Before the rule changed the opposite held and the rate depended
> entirely on when the key was pressed — and back then victory + limb loss was impossible.

**16.5%** of won fights bring home a maimed warrior (light-kit measurement): a maimed roster is now
the record not only of abandoned expeditions but of **dearly bought victories** too.

### Permanent penalties
| Loss | Effect |
|---|---|
| Sword arm | Attack strength ×0.65, cannot use two-handed weapons, switches to the one-handed animation set |
| Off arm | Attack strength ×0.85, still cannot use two-handed weapons |
| Leg (each) | Evasion ×0.55, walking speed ×0.60, limping animation |
| Eye | Hit rate ×0.75 |

Losses compound: a warrior who loses both legs has their speed down to ×0.36. The reason the sword
arm and the off arm are separated is that the first is the blow itself and the second is balance —
both end two-handed weapons, but for someone fighting one-handed the loss of the off arm is a
bearable one.

A warrior does not become unusable — they stay in a middle state, "weakened but able to serve". It
leaves the player with the decision: **"retire them or keep using them"**.

### Visual implementation
A modular character: limbs as separate meshes/bones. At the moment of severing, a special
dismemberment animation + blood VFX. Afterwards it switches **permanently** to the missing-limb
model and the altered animation set.

### Recovery
- A wounded warrior cannot go on an expedition for X days (the length depends on the severity of the
  wound)
- Natural recovery is slow; an **infirmary/physician building** + a **medicine resource** speed it up
- Medicine is added to the economy as a new resource type

---

## 8. Chat Integration

### Participation (opt-out — the Domina model)
- **Everyone in chat is in the pool by default.** When a new recruit slot opens the system draws at
  random from users who have spoken in chat.
- A user who types **`!no`** leaves the pool (as in Domina) — consent is preserved
- **`!join`** is a priority signal ("move me up"), not a requirement
- If the pool is empty (single-player, or nobody has spoken) → it generates from the **Japanese name
  pool**
- A generated/drawn name is **always editable by the player**
- **A username filter is mandatory** — profane/inappropriate names are not taken into the pool
  (necessary for a game published on Steam)

> **Why opt-out and not opt-in:** the power of the mechanic is **surprise**. Opt-in kills the
> surprise and also leaves out the lurkers who are the majority of chat → in small streams the pool
> is permanently empty and the system always falls back.

### Bits / Kicks ("Summon a Hero")
- A chat member donates through the platform's native donation system (Twitch Bits / Kick Kicks)
- Their name enters the roster **guaranteed** (no waiting for a random draw)
- **The donation amount determines the starting training level**, and **there is an upper bound**
  (so a single large donor cannot create an unbalanced character)
- Permadeath and limb loss **apply exactly as usual** → not pay-to-win but **pay-for-head-start**

### Scope rule
All chat interaction affects **only that streamer's own session**. There is **no** shared/global
economy, channel-points system or real-money betting (compliance with Steam policy).

---

## 9. AI Audience (Single-Player Equivalence)

When there is no real chat **or** when a vote receives no votes at all, the AI steps in.

- The AI looks at the **same performance signals** real chat would look at: hit rate, aggression,
  damage taken, how "showy" the fight was
- It produces its own bushi/ronin ratio → the same reward multiplier formula runs
- In a seppuku vote it decides with a **weighted probability** based on the honour level (the lower
  the honour, the higher the chance of seppuku)

The result: single-player is **mechanically equivalent** to stream mode. No system stays shut off.

**But fake chat is not generated (2026-09-07).** In single-player, a fake conversation stream with
invented usernames is not shown; the crowd is read as a single **aggregate indicator** — a mood and
the multiplier in force (e.g. *displeased ×0.82*). Invented names cannot stand in for the real
viewer names the player knows; an honest indicator was preferred over an empty imitation. The
indicator stays readable in stream mode too, next to the real chat stream.

**The seppuku vote also opens when the fight ends in real time.** The time model moved to pausable
real time (§10) but the queue mechanics were kept: a warrior who falls below the threshold waits, a
60-second window opens when the fight ends, and there are never two votes at once. Stopping the
fight for a chat vote would break the rhythm of automatic combat and turn every seppuku candidate
into an interruption.

---

## 10. Expedition, Roster and Dojo

### Time: pausable real time (decision round, 2026-09-07)

> **The old model, now void:** the discrete day. The code that built day closing, event triggering
> and chat voting around the discrete day will be reworked.

- Time **flows and can be stopped** (the Domina model). The core still advances on a **fixed tick**
  and determinism is preserved — real time is only the clock driving the tick
- **A season is 180 days**, and the days remaining are shown on screen. A fixed countdown
- **Events land at the turn of the day:** they do not pop up in the middle of the flow. In the
  morning the player sees all the offers and events together (event probability 15% per day, to be
  re-measured)
- **One 7-day tick, two consequences (2026-09-10).** The compulsory-fight rule **stands** and the
  rival's move counter runs on the **same** tick — the move counter was briefly written as a
  replacement for it; that is now void, the two sit together on one clock:
  - **The honour penalty stays.** A week in which the dojo files no fight costs the **roster's
    honour** (not gold). The money penalty was rejected for the reason it always was: a rich player
    would simply **buy** the safe training loop and the endless-training exploit would stay open.
    Because honour also pushes towards the seppuku threshold (§6), the price of hiding accumulates
  - **The rival's move lands on the same day.** A `Next move: n days` counter stands on screen and the
    move takes a settlement one step further from you (see "The rival school and the settlements"
    below). Every settlement he holds on day 180 is men he brings to the final
  - **Why both:** the honour penalty is the rule that was **proven** to close the exploit and it bites
    immediately, on the dojo itself; the move counter bites slowly, on the map, and can be answered by
    a player who is willing to lose ground. One is the floor, the other is the pressure. The fiction
    carries both without strain — the week you file no work is the week the province reads your school
    as idle **and** the week he moves unopposed
  - **Unmeasured:** the honour number for a missed week (Open Decision #18) and whether the map
    pressure adds anything on top of the honour penalty
- **Difficulty tiers:** Apprentice / Master / Legend. "Master" is the base of the balance
  measurement; the others are derived by multipliers, not by a separate measurement run

### The end of the season: the bounty gate and the final tournament

- **Three bounties must be completed to enter the final** (the same number as Domina's "3 Regional
  Champion" gate). It stands on screen as `1/3`. The number is deliberately small: bounty hunting is
  hard as it is — a selective dojo was only entering 0.69 contracts in 60 days in measurement, and
  5 bounties would turn the gate into a wall
- **The fifth bout is the head of the rival school** (2026-09-10). The final is not an anonymous
  ladder: four of its seniors, then the man himself. Whether the night takes the form of the
  appointment contest or of his raid on the dojo, it ends with him
- **The final: 5 consecutive rounds, no recovery in between.** A new team can be formed for each
  round (the expedition limit is still 4) but the wounded and the exhausted accumulate — the final
  tests **roster depth**. Domina runs a single championship with 15 gladiators at the end of the
  year; 15 would not work for us, but a single fight would not carry the weight of 180 days either
- **Losing the final ends the game.** A definitive ending; the game does not carry the player to
  victory
- **Losing within the season is also possible:** when the treasury and the roster run out the dojo
  closes, and the closing screen gives the season summary (days, victories, the dead, and **the men who
  walked out free** — those released during the season plus everyone alive on day 180, whose term the
  season outlived)

### The dojo: facilities and staff

A **facility** = a physical building that is put up (gold up front, permanent, cannot be sold back,
and construction takes **time** — no speeding it up with gold). **Staff** = the person who runs that
building (a daily wage + per-head stores; you can cut them any day, the building stays).

- **No slots.** The natural ceiling is one person per building; the real constraint is the **daily
  wage**. Domina's "3/6 staff at a time" number is an artificial ceiling; with us the constraint is
  set by the economy itself, and **cutting staff in a crisis is the economy's gearbox**
- **An empty facility works at half efficiency** — the construction investment is never wasted;
  staff bring it to full efficiency. Some gates are still conditional on staff (a smith for
  ō-yoroi): those do not open at half efficiency, they either exist or they do not
- **Buildings are physical and fixed in place.** No free placement and no adjacency bonuses — no
  layout-planning game is being opened
- **Classes are unlocked by facilities:** once the relevant facility is built, that class becomes
  trainable (the counterpart of Domina's 400-500 gold, 16-17 turn class research)

#### Professions — 11 roles (profession round, 2026-09-07)

Three of the 14 roles in the draft were dropped: the **bonesetter** went into the physician's 3rd
tier, and the **fixer** (dirty work / theft / betting) and the **groom** (there is no horse-and-cart
activity) never made it in at all.

| Role | What it gives | Own branch |
|---|---|---|
| **Drill master** | Training speed multiplier. The dojo's **free starting staff member** | ✅ speed ×1.30 → two drills the same day → speed once more |
| **Kata master** | Raises the stat **ceiling** (+4 / +20 for health and stamina) **and multiplies the stats gained from fighting** | — |
| **Weapon master** | Gives warriors **permanent mastery per weapon**; the mastery stays with the warrior and does not go if the master is cut | — |
| **Physician** | Turns a mortal wound around **and** removes the medicine expense | ✅ mortal wound → medicine expense zero → limb loss ×0.75 |
| **Smith** | Makes repairs cheaper and faster; **is the requirement for ō-yoroi** | ✅ repairs ×0.7 → ō-yoroi unlocked → special weapons forged |
| **Steward** | **The expense side only**: stores ×0.80, warriors ×0.75, repairs ×0.80. **No** reward multiplier | ✅ purchases ×0.90 → ×0.80 → store +50% |
| **Broker** | **Does not touch prices** — changes *what the market puts out*: the number and quality of candidates, the frequency of classed candidates | — |
| **Bard** | Daily morale gain, slows morale decline; sings a **lament** for a fallen warrior | — |
| **Monk** | **The temple's hand inside the dojo**: opens omamori slots, conducts the funeral rite, raises the temple relationship. Gives **no** passive stats | — |
| **Cook** | **Does not produce, cuts consumption**: daily food consumption ×0.75 | — |
| **Diviner** | **No curses, but information**: reveals the enemy's stats, weapon and behavioural tendency in offers; partial reading in a blind fight | — |

The four roles that carry a branch correspond to the dojo's four continuous expenses:
**training, health, equipment, supply.** The remaining seven are situational staff.

> **Why there are no flat passive bonuses:** a bonus like "+2 stats to the whole roster" is the most
> dangerous thing in a staff economy — every dojo keeps it, and it stops being a decision. Domina's
> Sacerdos **consuming nothing** confirms this too: a bonus with no price is not a decision but a
> default.

#### A retired warrior can become staff

A retired warrior **draws no wage** — that is the reward of the long game. But they cannot do every
job:

| Tier | Roles |
|---|---|
| **Good** (better than hired) | Drill master, Kata master, Weapon master |
| **Medium** | Steward, Broker, Monk |
| **Weak** (half efficiency) | Smith, Bard |
| **Not at all** | **Physician, Cook, Diviner** — outside staff required |

> **The ban is an economic decision:** if free retirees could fill every facility, no facility would
> stand empty, staff wages would be pulled out of the economy and the "cut the staff, keep the
> building" gear would spin free. The three banned roles keep wage pressure permanently on.

### Leaving the roster — only honourable paths

- **Retirement:** a warrior with many victories, or one badly maimed, leaves the field and becomes a
  **master** — a permanent bonus to training speed, the daily food burden ends, they never take the
  field again
- **Seppuku** (§6) and **sending them on their way**
- **Release — walking a man out under his own name (2026-09-10).** A warrior joins on a **fixed-term
  service contract** (the story's *nenki hōkō*): the market price settles the claim standing over him,
  and what he gives back is the term. Releasing him early ends the term. **It costs no gold** — the
  price was already paid at the stall, and charging a second time would be the player paying himself.
  What it costs is the man: the roster slot, every training day invested in him, and — if he was the
  best in the dojo — the market ceiling that follows him (`BestFollowCeiling`, §11). Nothing comes back
  for it except the **roster's honour** (§6), which is the school's name. Keeping him is always the
  profitable move; that is the point of the item, and it is the only thing in the game that answers
  the rival's argument (`STORY.md`)
- **Nothing else is attached to it.** No leaving gift, no severance, no travel money: an optional gold
  line here would only be a second, smaller version of the same decision, and the decision is already
  made when the man walks out
- **Selling and killing are not included.** A student is not property. The "a maimed warrior eats
  food forever" leak is closed by retirement
- **What passes on from the dead:** their equipment — and only **if the fight is won** (someone has
  to survive to carry the body)

### NPC relations — three parties, five tiers

The **regional lord** (the owner of the offer queue), the **merchant guild** (market prices and
stock), the **temple** (omamori supply, the funeral rite).

> **The lord is away all season; his clerk's office issues the work (2026-09-10).** The story puts the
> lord in Edo for the whole 180 days, so nobody meets him until the appointment. The relationship is
> still **written to him** — the office files in his name and the file is what he reads on his return.
> What the player deals with day to day is the **deputy's clerk's office**, the administration running
> the province in his absence. That the deputy is the rival's patron does not make the office a
> faction to court: the clerk stamps licensed work, and the relationship number tracks the file, not
> the man. No fourth party is added and the rival's axis stays separate (below). A single number for each, five tiers:
Hostile / Cold / Neutral / Pleased / Loyal. Only that number is written to the save.

- **Raised by:** finishing a contract on time (large), a gift (small, with diminishing returns), a
  decision in an event in that party's favour, rising honour (all three at once, very small)
- **Lowered by:** losing or pulling out of a contract taken from them, never taking their offers
  (accumulates)
- **Not included:** selling secrets, blackmail, fixing a fight's outcome — the fairness of automatic
  combat must remain readable. **Patronage was also not taken**: an NPC does not cover a warrior's
  upkeep
- **Abandoning a contract eats from two places at once:** the roster's honour drops **and** the
  relationship tier drops. Taking a contract is giving your word

**Omamori** (a temple charm) is the counterpart of Domina's Jupiter cards: it is fitted to warriors
**and to staff**, can be removed and passed on, and can be sold. That transferability is known to
be open to exploitation (gathering them all on one warrior before an expedition) — to be measured in
the balance round.

### The rival school and the settlements (decision, 2026-09-10)

Open Decision #17 was closed by the story (`STORY.md`). The rival **Kurogane school** funds itself by
selling protection to the province's settlements and manufactures the trouble it then sells
protection from. The season is the tug-of-war over those settlements.

**Settlements — 12.** Each carries: who it pays (`His` / `None` / `Yours`), a warning level (0-2) and
the day of the last reprisal. They live in `Core/Dojo`, advance inside `AdvanceDay()` and go into the
snapshot. Engine-free and deterministic, as everything else in the core.

Twelve, not sixteen: 180 days ÷ 7 = **25 moves**. Against 12 settlements that is defensible-in-part,
which is the point; at 16 most of the map would never be touched.

**The starting state varies from run to run** (the only source of run variety — see "What was
deliberately left out" below): how many settlements the rival already holds, and the size of the
starting roster — **3-5 warriors**, never above the starting roster ceiling of 6. A set of numbers, not
a system.

> **The starting roster is a range, not a fixed 4 (2026-09-10).** The economy was measured on four
> warriors, so 4 stays the middle of the band and the band is deliberately narrow: at 3 the first week
> is tighter, at 5 the wage and food line starts higher. It cannot cross the roster ceiling, because a
> run that opens at the ceiling has no room to hire and the market would be dead on day 1.
>
> **A starting debt was rejected (2026-09-10).** It was on the list of run-variety numbers and is now
> struck: §11 has no debt item — the treasury cannot go negative — so a debt would need a whole
> mechanic (a creditor, a due date, what happens on default) for one number of variety. What the
> master left behind varies through the **starting capital and the roster**, not through a liability.

**What a settlement gives — never gold.**

The settlements have paid a heavy protection fee for years; they are drained, and a freed one does
not start paying the player instead. This is deliberate on two counts: it keeps the treasury tied to
contracts, so the measured economy is untouched, and it answers the "are you just the next racket"
question with the balance sheet rather than with dialogue — you protect people who cannot pay you.

1. **On liberation, one guaranteed thing — and that is all a settlement ever gives.** Whatever it
   still has: the last of the rice, a bundle of medicine, one free repair from its smith, a man who
   signs with no claim to settle, or **the name of the rival's next target** (which makes the move
   counter visible for one turn). Once. Never again.
2. **The real return is the offer queue.** Each held settlement widens it: more offers standing at
   once, better paid. This sells the player the one axis measurement showed to matter — the **right to
   be selective** (the dojo that refuses nothing loses its roster, the one that refuses everything
   heavy loses its treasury).

> **A weekly trickle of stores was proposed and dropped (2026-09-10).** At a 20% weekly chance per
> settlement it came to ~1.6 parcels a week at eight settlements — between 10% and 28% of an
> eight-man roster's weekly upkeep (3 gold per warrior per day). Three grounds for dropping it:
> stores are gold in another form, so it reintroduced through the back door the unmeasured income
> the "no gold from settlements" rule exists to keep out; it scaled with the number of settlements
> held, so it paid most in the late game, when the player needs it least; and it contradicted the
> fiction it was meant to serve — settlements drained by years of protection money do not have a
> parcel to send every week. The one-time gift already carries the feedback.

**How a settlement changes hands.**

The rival's move each 7 days picks a target — the player's settlements first, highest warning among
them:

```
move            -> that settlement's warning +1
move at warning 2 -> the settlement goes back to him, warning resets
```

So a settlement needs **three uncontested moves** to fall. With 25 moves against 12 settlements the
player cannot hold everything and must choose — which is the intended shape of the season.

The player's answer: take that settlement's contract, or hit the collectors on the road. Either gives
**warning −1** and pushes the next move back **2 days**. The tempo runs both ways.

Winning one: finish **2 contracts** for a `None` settlement and it becomes yours; a `His` settlement
must first have its warning brought to 0, then **3 contracts**.

**The rival needs one stored number, not a relationship.**

- **His strength is derived**, not stored: it is the count of settlements he holds, and it is what he
  brings to the final night.
- **One new stored value — deniability.** He killed the master once; openly destroying a second
  licensed school in an appointment year would end his candidacy, so how far he can go is bounded.
  Killing his men spends that bound down, and as it approaches zero the fourth step of the ladder (a
  direct attack on the dojo) opens.

**The three-party relationship system is not touched.** The lord, the guild and the temple keep their
five tiers; the rival is not a fourth party of the same kind, because there is no such thing as good
relations with him. The "relationships spoil one another" model that #17 previously rejected comes
back **only on his axis**: taking a settlement lowers what he holds and raises what the player holds,
and nothing propagates to the other three.

**Numbers to measure before locking:** 12 settlements; the 2/3-contract thresholds; the 2-day delay; and whether the map pressure adds anything on top of the
compulsory-fight honour penalty, which is the rule that actually closes the endless-training exploit.

**What was deliberately left out (2026-09-10):**

- **Passive income from settlements**, in gold or in stores. It would open a "grow rich without
  fighting" line and void the measured economy. A settlement gives one thing, on the day it comes
  over, and nothing after that.
- **Per-settlement traits** (a smith, a ferry, a gambling house). Rejected as unnecessary; the twelve
  are equal.
- **Doctrines for the rival** (pressure / terror / poaching the player's contracts). Rejected; he has
  one strategy.
- **A debt lever against the death spiral.** A roster broken on day 60 stays broken: the school is
  struck from the register and the run ends. No rescue loan.
- **A settlement board** as a screen the player acts on. If one is built it is a **picture only** — no
  travel, no routing, no fight started from it — and Open Decision #2 gets a footnote saying so.

### The expedition

- The base: the dojo — training grounds, infirmary, the trainer's (sensei's) skill tree
- **At most 4 warriors** are sent on an expedition; the number is the player's decision. Some
  encounters (duel, raid) **impose an exact number** — that encounter writes its own rule, but the
  upper bound is still 4
- **An expedition = one room, one fight** (direction set 2026-08-29). There is no run through
  consecutive rooms; one encounter is chosen in the daily cycle, the fight ends, the team returns to
  the dojo
- **Surrendering ends the expedition:** the fight is abandoned and that expedition's reward is not
  taken (§5). In a single-fight expedition these two are the same sentence — pulling out erases that
  fight's reward, nothing else
- Warriors left at the base are safe; **only the team on the expedition** is at risk of permadeath
- Permadeath: a fallen warrior is gone permanently

> **The consequence of a single-fight expedition:** because there is no chain, there is no
> room-by-room attrition. Difficulty has to come from the single encounter itself; it cannot be built
> out of health erosion. On the balance side this is good news — `Domina.Sim` already simulates a
> single fight, no chain modelling is needed.
>
### Fight types (decision round, 2026-09-07)

| Type | Rule |
|---|---|
| **Scheduled contract** | Taken from the timed offer queue; travel + the fight consume real time |
| **Blind fight** | The opponent is unseen, the reward high. You decide without knowing what equipment to take. **No betting** — uncertainty is a risk decision, not a shortcut to money |
| **Bounty hunt** | Fixed named story targets + ordinary generated bounties in between. Three of them open the gate to the final |
| **Drill match** | **Inside the dojo only.** No deathless exhibition fights against outsiders: inside the dojo is safe, every fight that goes outside is lethal |
| **Night raid** | The dojo itself is defended (below) |
| **Final tournament** | 5 rounds, no recovery, losing ends the game |

**No special events:** mini-games requiring a separate rule set, such as horse races or beast fights,
are not opened. Variety comes from contract types and field features; everything uses the same
resolver.

### The story cast and the night raid

- **The main story characters are the same in every game:** identity and stats are fixed, so they can
  be learned and prepared for (like Domina's fixed regional champions)
- **A defeated target does not get stronger, it grows.** If you lose an intermediate story fight the
  target's stats **do not change** — memorisation is preserved; instead men are added alongside them
  (defeat 1: +2 kappa, defeat 2: +4). A stat multiplier was rejected: a target that becomes
  unreachable closes off the counter-move, whereas a crowd can be answered with encirclement and
  field features
- **Defeat triggers a night raid.** Losing a story fight makes the dojo a target; the enemy you lost
  to can raid within a few days. If you are unprepared, the store is plundered and the wounded in the
  infirmary die. If you are prepared (a warrior on watch + a wall facility) the raid turns into a
  fight — but one that begins tired and half-equipped

### The price of entering an encounter (locked 2026-08-29)

- **Entering costs a day** — even if you flee. It requires no resource bookkeeping, works while the
  economy numbers are still open, and time is the scarcest resource anyway. This is the item that
  closes the "enter–look–flee" loop
- **The enemy roster is partly visible:** before entering, only a rough threat marker is readable
  (the enemy kind or a difficulty band). The full roster and stats are not visible — the choice is
  informed, the surprise does not die
- **One encounter offer per day:** take it or leave it. If it is not taken the day passes in the
  dojo. There is no list/map screen

> Entry costing a day, the honour price of fleeing (§5) and the key being closed until first contact
> together close the scouting loop: entering to look means risking a day and a fight.

### How the day's offer is generated (2026-09-04)

The offer is a **pure function of the day and the expedition's seed**: the same day and the same seed
always give the same offer. That is why the offer is not written to the save file — the day and the
seed are in the file, and the offer is recomputed on load. Nor can you change an offer you do not
like by reloading the save.

- **Power** is a single curve: on day 1 `StartingPower`, rising by `PowerPerDay` each day, with a
  fluctuation for that day on top. The fluctuation is essential — had the curve been a straight line,
  the same offer would come every day and the "leave it" decision would mean nothing; the meaning of
  leaving it is that tomorrow can be different.
- **Roster size** is unlocked by power (a second enemy, then a third) but does not stick to the upper
  bound: the same power sometimes means one heavy enemy and sometimes three. The two are not the same
  fight — one tests target selection, the other endurance.
- **A crowd does not divide the power:** three enemies are three times the enemy. Had it divided, a
  crowded offer would carry the same threat with less health, i.e. sell the same risk for less reward
  — reward is tied to enemy health (§11).
- **Enemy kinds take their place on the curve:** collector and cutthroat from the start, duelist after
  power 1.2, kabukimono after 1.5, senior student after 1.8.
- **Duel:** some offers impose exactly one warrior (§10's "the encounter writes its own rule" item).
  This is the most expensive item in measurement — with a single warrior there is nobody to share the
  damage.

Power scales health and damage **directly**; accuracy, defence and evasion by the **square root**.
With linear scaling the curve turns into a wall at some point: the enemy becomes unmissable while the
player starts missing, and the difficulty increase is counted twice.

**The numbers are not locked** (the steepness of the curve, the fluctuation margin, the duel rate).
The measurement below says what was measured, not which curve is right.

### "Take it or leave it" was measured (2026-09-04)

500 dojos × 120 days, with generated offers. The only difference is the policy's right to filter
offers:

| Policy | Dojos closed | Days survived (median) | Death / warrior-fight | Hungry days | Turned down |
|---|---|---|---|---|---|
| Takes every offer | 99.2% | 54 | 9.2% | 7.1% | 0% |
| Turns down heavy offers when short-handed | 0.4% | 120 (all) | 7.1% | 50.8% | 67.7% |

The two failures are each other's opposite: **the dojo that never refuses loses its roster, the dojo
that refuses everything heavy loses its treasury** (the latter ends with 0 in the treasury and spends
half its days hungry). The key itself is thereby proven — refusing is not an escape but a trade made
between a day and a warrior.

> The measurement's second result confirms the limit of the economy round (§11): once deaths per
> warrior-fight reach 9%, no price tuning keeps the dojo standing.

### The difficulty curve — no bosses (2026-08-29)

Because there is no expedition chain, no separate structure is **built** for a boss rhythm.
Encounters get harder **on a single curve** as the days pass; there is no separate boss encounter, no
boss calendar and no threat counter. The named men of the rival school (its
seniors) are for now merely strong enemies at the upper end of the curve. The boss idea can
be reopened later; right now it is not a rule.

### Skill tree depth
| Layer | Depth |
|---|---|
| **School + Trainer** | **Advanced/deep** — training speed, facility unlocks, economy bonuses, infirmary improvements |
| **Warrior** | **Simple** — basic stat/ability progression |

Rationale: let the real long-term investment be in the school → so that a warrior's death does not
feel like the loss of a huge investment, balancing the sting of permadeath.

**Written (2026-09-04):** `Domina.Core/Dojo/School.cs` and `Model/WarriorPath`.

**The school — three branches, three tiers per branch** (200 / 400 / 700 gold; order is compulsory
within a branch):

| Branch | 1st tier | 2nd tier | 3rd tier |
|---|---|---|---|
| **Drill hall** | Drill hall: training speed ×1.30 | Kata master: stat ceiling +4, health/stamina ceiling +20 | Inner dojo: speed ×1.30 once more |
| **Infirmary** | Infirmary: natural healing +1 day/day | Herbalist: medicine burns +1 day | Bonesetter: the share of damage counted as a graze +0.10 |
| **Steward** | Steward: daily stores ×0.80 | Patron: victory reward ×1.15 | Broker: warriors ×0.75, repairs ×0.80 |

- **A facility is paid for up front and cannot be sold back.** Had it been reversible, the player
  would rearrange the tree before every expedition; the school would then be not an investment but a
  settings panel.
- **There is an order within a branch.** Without the order the tree would not be a tree but nine
  independent buttons.
- **The bonuses apply to all readings:** `DojoState.Tuning` and `Economy` now return the
  **school-processed** form rather than the raw tuning. Code that read the raw value in half the
  places would silently apply half the bonus.
- **A discount rounds down and never goes below 1.** Had it rounded to nearest, 2 gold of food ×0.80
  = 1.6 → still 2, and the steward branch would never touch the store at all.
- **Only which nodes have been taken is written to the save**; the size of the bonuses is a balance
  number and does not go into the file (§2). A corrupt save cannot skip a branch's order.

**A warrior's path — one choice, three options.** Blade (Accuracy ×1.10, Strength ×1.10), Rock
(Defence ×1.15, Health ×1.05), Shadow (Evasion ×1.15, Speed ×1.10). It is chosen once and cannot be
undone; the lock is opened by **20 training days** — a path is not bought, it is earned by work. The
multiplier enters `EffectiveStats`, that is, **the fight only reads its result**; it is applied
**beneath** the disability multiplier (a path does not enlarge the penalty for a lost limb; the
penalty cuts the path).

The reason the warrior side is kept shallow is the same as the rationale above: a deep warrior tree
turns permadeath into the loss of a huge investment and the player would avoid putting their warrior
on the field. The measurement is in §11.

---

## 11. Economy

> ⚠️ **All the numbers in this section are invalid (2026-09-07).** The move to real time, stat gain
> from fighting, staff wages, classes and morale removed the ground the measurements below rested on.
> The texts stand as a **historical record** — they show which number came from which measurement and
> what was tried and dropped. Re-measurement will be done **as each system enters the code** (not as
> one big measurement round): for early feedback, at the price of repeating some sweeps.

### Economy decisions from the decision round (2026-09-07)

- **Start:** 600 gold + **three days of food and water** + 4 warriors. The store no longer starts
  completely empty: you plan your first expedition without going hungry, and supply pressure sets in
  from day 4. (Domina starts you with 1000 gold and a full store.) Opening the first day with a
  supply crisis did not make the game harder, it made it more **confusing**
- **The roster ceiling is tiered** (starting at ~6) and is raised by dojo upgrades; each tier brings
  a gold cost and a daily stores burden. The expedition limit is still 4
- **Staff expenses enter the economy:** a daily wage + food/water per head. The price of growing your
  management is linearly rising expenses
- **The market:** 10 candidates, refreshed daily, 150 gold base × talent, bitten by a ceiling that
  follows your best warrior. The main stock is **classless recruits**; now and then a **ready-classed**
  and markedly expensive candidate drops — a shortcut around the facility investment, but at a price.
  Its frequency must sit where it does not render the facility branch meaningless
- **Contracts:** instead of a single daily offer, a **timed offer queue** — several offers hang at
  once, each with an expiry, and an expedition consumes real time. **Blind fights** are in (the
  opponent hidden, the reward high); **betting is not**
- **Some contracts forbid pulling out** and pay more (Domina's `Surrender Allowed: No` line). The
  decision is made not in the fight but **when taking the contract** — this does not conflict with the
  sanctity of the surrender key; what closes the key is the player's own signature. If it appeared on
  every contract the rule would become less a choice than a tax; its frequency will be measured
- **No surrender threshold.** A Domina gladiator surrenders automatically when health drops below
  10%; with us **pulling out stays the player's decision**. An automatic threshold would take away
  the one intervention automatic combat leaves the player. The price is accepted knowingly: a
  forgotten warrior dies
- **Armour is a continuous expense item:** a worn piece is repaired with gold, a destroyed one does
  not come back (§7). The repair price is tied to the smith staff member

Domina's model is taken as the reference:
- Income: fight rewards (scaled by the chat honour multiplier), optional risky matches
- Expenses: equipment, resources (food/water), **medicine/treatment**, buying warriors
- Random events can drain resources → pressure to keep a buffer
- Optional risky matches **can be declined** (as in Domina, risk is optional)

### A single currency: gold (locked 2026-09-04)

Food, water and medicine sit in the store as countable stock but are bought from the market with
**gold**. The reason: if the economy's scarce resource is not divided, the "what do I spend on today"
decision looks at a single number. The treasury **cannot go negative** — there is no such item as
debt; a purchase you cannot afford is not made.

### Prices (locked 2026-09-04)

| Item | Number | What it scales with |
|---|---|---|
| Victory reward | **0.45 gold / enemy health** | The encounter itself — the same enemy pays the same money |
| Pull-out / rout reward | **0** | §10: surrendering erases that expedition's reward |
| Armour piece | **1.50 gold / durability point** | Keikogi 60, dō-maru 165, ō-yoroi cuirass 270 |
| Repair | **0.90 gold / wear point** | No more than the piece's pool is paid |
| Food / water | **2 / 1 gold**, one each per warrior per day | Roster size |
| Medicine | **12 gold**, one per warrior in the infirmary per day | Number of wounded |
| Buying a warrior | **150 gold** | — |
| Starting capital | **600 gold** | — |
| Starting roster | **3-5 warriors** (drawn per run, 4 is the measured middle), free | §10, run variety |

The starting roster is the same size as the measurement's roster (four warriors) and comes free: the
600 gold stands there for the first day's decisions, not for the roster itself. Had another number
been chosen, the dojo being played would no longer be the dojo whose economy was measured. The store
starts **empty** — the first day's food is bought by the day's closing.

**Repair is always cheaper than replacement** (0.90 < 1.50). Had it been equal or dearer there would
be no repair decision left: everyone would use a piece until it fell apart and buy a new one. The gap
between them is the whole of the "repair early or use it to the end" decision — in measurement the
two extremes come out almost level (over 60 days the early repairer finishes with 959 gold, the one
who uses it to the end with 1012), but the dojo that uses it to the end loses 3.36 pieces per fight
**in the middle of the fight**. When the gold is equal, choosing which side carries the risk is the
player's decision.

### Medicine is not a compulsory tax but an accelerator

A day without medicine still burns an infirmary day; a day with medicine burns two. Medicine is
therefore the biggest item in the economy: two thirds of the daily 34.5 gold consumption comes from
medicine, and its price directly sets the calendar — with free medicine the idle-day rate is 28.3%,
at 12 gold 33.8%, at 24 gold 59.1%. That is, the item that creates the "expedition today or
infirmary today" pressure is the price of medicine.

### The price of scarcity is time, not death

If the store is short, **those in the infirmary eat first**. A warrior left hungry neither heals nor
trains that day; nobody starves to death. The order could not be arbitrary: leaving the wounded
hungry would turn scarcity into a penalty with no recovery.

### The warrior market (2026-09-04)

Buying a warrior is a **choice**, not a button. With a fixed-stat, fixed-price warrior there is no
"who should I buy" question; if you have the money you buy, if not you do not. The model was taken
directly from Domina's slave market:

- **Candidates come with different stats, and the stats are visible before purchase.** There is
  nothing hidden in the bargain; the decision is made between the visible stats and the price.
- **The price comes out of the stats themselves** — how much better the candidate is than a base
  warrior sets the price. A fixed price would make a good candidate free and a bad one a robbery.
- **The market follows the roster's level** (`RosterFollow` 0.7): in the early game no master warrior
  goes on sale, and as the roster develops so does the market. Without the following, either
  everything could be bought from the start (training would mean nothing) or the market would become
  meaningless in the late game. Even if the roster dies entirely the market drops to **recruit level**,
  not to zero — otherwise a collapsed dojo would have no way back.
- **The market cannot exceed the best in your roster** (`BestFollowCeiling` 0.75). The following says
  where the market *sits*, but not how far up it can *go*: when the jitter struck from above, a single
  candidate could exceed the best in the roster. The ceiling cuts that off — a bought warrior cannot
  exceed 75% of the score of the best warrior trained in the dojo.

  The reference is **the best warrior, not the average**: had it been tied to the average, the market
  could be exploited by buying two cheap recruits to drag the average down. The best warrior cannot be
  dragged down, only lost by dying — and it is right that the ceiling drops when they die.

  Below the ceiling there is a **floor**: the ceiling never falls below the recruit score. Without this
  floor the ceiling bit from the very first day and the market was selling a recruit-level roster men
  weaker than recruits; measured, **all** 400 dojos zeroed their treasury and the idle-day rate rose to
  93.7%. That is, the ceiling only comes into play once the best warrior has clearly passed the recruit.

  A candidate hitting the ceiling is **not clipped but scaled**: all the stats shrink by the same
  coefficient. Clipping one by one would turn every candidate at the ceiling into the same flat profile
  and the "who should I buy" question would disappear; scaling preserves the candidate's shape and only
  lowers their weight.

  Rationale: a trained warrior should be the player's **work**. If the market can copy them, training
  means nothing. The market is a tool for **replacement**, not for **progression** — the route to
  progression is buying a raw candidate and training them, and the only thing the market can sell at
  full strength remains `Talent`.
- **Talent (`Talent`) is the second axis:** how fast a warrior benefits from training, an innate and
  unchanging share (0.6-1.4). Two candidates arriving with the same stats do not develop at the same
  rate. It enters the price but with less weight than the stats: talent is a **promise**, a stat is
  what you have. **The fight does not read it**, only training does — it directly multiplies a training
  day's gain (see "Training" below).
- **The list refreshes every day** (`RefreshDays` 1) and **freezes within the day**. Daily refresh is a
  **locked decision** (2026-09-04): waiting already has a price — waiting a day costs a day (the stores
  are paid, no expedition goes out that day) — so keeping the stall stagnant on top would be a second
  penalty. Had it not frozen within the day, then, because the market follows the roster average,
  buying one candidate would change the remaining ones and the list could be cycled as much as you
  liked.
- **Ten candidates stand at the stall** (`Candidates` 10, decided 2026-09-05). The same scale as the
  reference game's stall — the number does not come from a written source but was seen in the game
  itself; none of the written sources swept (Steam guides, gameskinny, gameplay.tips, namu.wiki,
  Grokipedia, three reviews, forums — 2026-09-04) mentions the stall size. **Measurement says the
  number is not a balance lever:** across 400 dojos × 60 days, 4 / 6 / 8 / 10 candidates give the same
  band (final treasury 1184 / 1258 / 1301 / 1285, death 10.0% / 9.8% / 9.7% / 9.7%). What binds the
  market is not the list length but the stat ceiling and the treasury; so this number is a **feel**
  decision, not a balance one: buying should be a matter of choosing, not of making do with what you
  have.
- **Buying does not cost a day and is not limited in number** (`DojoState.HireRecruit`): the market is
  open all day, and as long as the treasury and the stall allow, more than one warrior can be bought.
  What costs a day is going on an expedition or spending the day in the dojo. Had a separate ceiling
  been put on the number of purchases, two warriors could not be put in place of two dead on the same
  day — and that is exactly the market's job. The limit is **gold**, not a counter.
- **A bought candidate is removed from the stall and recorded.** Because the list freezes within the
  day, without this record the same candidate would be sold an unlimited number of times: a single
  person would become the entire roster. It cannot be left to the screen's marker — reloading the save
  and buying the same man again is the same door, so the purchased rows are written to the save file.

**Measurement — the price of a daily stall (400 dojos × 60 days, `patrol`, `--market-pick value`):**

| Stall | Final treasury | Preserved their capital | Warriors bought | Dojos closed |
|---|---|---|---|---|
| Every 2 days, 3 candidates (old) | 1254 | 51.7% | 7.98 | 17.5% |
| Every day, 3 candidates | 1288 | 54.5% | 7.87 | 14.5% |
| Every day, 4 candidates (new) | 1184 | 50.5% | 8.29 | 19.2% |
| Every day, 6 candidates | 1258 | 55.2% | 8.61 | 17.0% |

The stall's frequency and breadth are **not a balance lever**: at this sample size the difference
between the four rows stays within the noise. The reason is that what binds the market is not the list
but two other items — the stat ceiling (above) and the treasury. The measurement also shows a limit of
the measurement policy: the simulation buys at most one candidate per day, whereas the rule allows more
than one; the "two warriors in place of two dead on the same day" behaviour will only be seen by
playing.

**Measurement (400 dojos × 60 days, `patrol`):** two buying strategies were compared.

| Buying policy | Final treasury | Preserved their capital | Deaths (per dojo) | Dojos closed |
|---|---|---|---|---|
| Fixed price, fixed stats (old) | 815 | 60.8% | 6.21 | 1.5% |
| From the market, **most stats per gold** | 354 | 26.5% | 7.16 | 11.8% |
| From the market, **the best you can afford** | 705 | 52.2% | 5.93 | 4.8% |

Two things show. First, the old model was **subsidising replacement**: a veteran-quality warrior came
for 150 gold, and once the market pulls that to a realistic price the dojo struggles. Second and more
importantly, the "buy a cheap raw candidate" strategy was clearly losing that day — because a raw
candidate did not develop: training's effect on stats had not been written yet. That the two strategies
should be rivals is the design goal; the gap between them (354 against 705 gold) was the measure of the
hole the training system had to close. Training was written the same day and narrowed the hole — see
the "Training" heading below.

**The measure of the ceiling (same setup):** after the ceiling was added the same two strategies fall
to 147 (9.8% preserving their capital, 18.8% closing) and 204 gold (13.0%, 13.2%); deaths rise from
5.93 to 7.08. The mechanism is clear — because the replacement warrior is weaker, more of them die.
This is not a balance failure but the **price** of the ceiling: like yesterday's 354-705 gap, it too is
part of the shortfall training is meant to close.

The ratio was **locked at 0.75**. In the `patrol` roster, 0.75, 0.85 and 0.90 give the same result
because the recruit floor dominates (the best warrior's score is 387, the floor 355): the ceiling only
starts to speak once the best warrior's score passes ~473, and you only get there through training. So
what is measured today is the price not of the ratio but of **the ceiling itself**; the ratio is one of
the numbers that will need re-measuring once training is written.

### Bounty contracts (2026-09-04)

The daily offer (§10) is generic on its own: the same kind of job arrives each day, the type and band
change, and the decision is always "do I go in today". A **named target** changes that decision.

The contract does not **replace** the daily offer, it **stands beside** it. A day still costs one job;
which job you do is the decision.

- **The target is named and singular.** A kind from the adversary list (§10), an epithet on top
  ("Rib-Breaker") and a party who issues the contract ("a village headman"). Because there is a single enemy, team
  size is not imposed: how many you go with is the contract's real decision — sending one is choosing
  the risk, piling on all four is choosing to leave the dojo defenceless that day.
- **It has a duration** (`PostingDays` — posted every 4 days, `OpenDays` — stays open for 3). The
  duration gives the campaign the one thing it has lacked so far: **a plannable future**. "Let me not
  take the cheap job today and keep the roster fresh for the big job in two days." A contract without a
  duration would be a warehouse rather than a decision. Because the two numbers are separate there are
  **days with no contract**; had a contract been posted every day the ordinary offer would become
  meaningless.
- **Accepting is giving your word.** Accepting does not cost a day, it buys **time**: if you have not
  returned by the last day the **entire** roster loses honour (`BrokenHonorPenalty` 10). The penalty is
  written against the roster rather than the ones going on the expedition, because it was the dojo that
  gave its word — had it been written against one person, the player would dump the penalty on a warrior
  they had already written off. The price of fleeing is written against the whole team for the same
  reason (§5).
- **The team that brings the head back gains honour** (`HonorReward` 6). The gain to those who go, the
  debt to the roster: one is the return on labour, the other on the word given.
- **A contract whose head has been taken comes off the board.** Because the board is pure, a contract is
  regenerated every day until its duration expires; without this record the same target would appear
  posted again the next day and the same head would be sold twice (measured: 12.7 bounty hunts per dojo
  in 60 days).
- **Generation is pure**, like offers and the market: the same seed and the same day always give the
  same contract. The contract is not written to the save; the only things written are **whether the word
  was given** and **whether the head was taken**. Otherwise the player could escape their word by
  reloading the save.

**Measurement (400 dojos × 60 days, `patrol`, offer mode, market open):** a dojo entering every open
contract regardless of its band was compared with one entering none.

| Acceptance limit | Bounty hunts | Final treasury | Preserved their capital | Deaths | Dojos closed |
|---|---|---|---|---|---|
| `dire` (enter everything), no hunts | 0.00 | 4 | 0.0% | 8.37 | 75.5% |
| `dire`, hunts on | 7.44 | 1 | 0.0% | 9.62 | 80.8% |
| `rising` (selective), no hunts | 0.00 | 393 | 20.8% | 1.26 | 0.0% |
| `rising`, hunts on | 0.69 | 412 | 22.2% | 1.25 | 0.0% |

The shape is the shape we wanted: **the one who chooses wins, the one who jumps at every job sinks.**
The selective dojo profits from bounty hunting (393 → 412 gold, deaths unchanged); the dojo that enters
every contract raises deaths from 8.37 to 9.62. The contract became not an easy-money tap but an item
whose risk the player chooses.

The numbers are **not locked**. The real question the measurement raised is `PowerMultiplier` (1.8): with
the target this strong the band comes out `Heavy`/`Dire` most days and a selective dojo only enters
**0.69** contracts in 60 days — that is, the system works correctly but is almost never seen. Sweeping
power together with the reward multiplier was left to the measurement round after training's stat effect
is written.

### Training (2026-09-04)

The market ceiling (above) locked replacement into the recruit band: a bought warrior cannot exceed 75%
of the best one trained at home. That leaves progression in a single place — **training**. The rule is
this:

- **A day costs one job.** Training spends the same day as going on an expedition; its price is not gold
  but **time and opportunity**. No separate fee was set: an idle day already eats food and brings no
  reward.
- **Four drills, eight stats.** Strike (Accuracy + Aggression), Guard (Defence + Strength), Footwork
  (Evasion + Speed), Conditioning (Health + Stamina). Each drill has a primary and a secondary stat; the
  secondary takes **half a share**. Had a single stat been drilled, warriors would converge on the same
  flat profile in eight days; the secondary share gives the drills shape and makes "what do I drill
  today" a real question.
- **The gain is a share of the remaining gap**, not a fixed increment: one day closes `GapClosedPerDay`
  of the warrior's remaining distance to the ceiling. Diminishing returns are **inside** the rule; the
  ceiling is not exceeded, only approached. The ceiling for the percentage stats is **90**, for health
  and stamina **180** (their scales differ; had they been tied to the same ceiling, conditioning would
  bite from the first day).
- **Talent multiplies the gain directly** (`Talent`, 0.6-1.4). It was the only thing the market could
  sell at full strength; now it has a payoff.
- **No randomness.** Training is the player's investment, not their gamble; had a die been rolled, the
  decision would hide behind the die.
- **Raw stats are written, not effective stats.** The disability multiplier continues to be applied on
  top: a maimed warrior who trains recovers but does not get their arm back (§7).
- **A hungry warrior does not progress.** The price of scarcity is time (§11's upkeep rule) — and
  training eats from that same time.

**Measurement (400 dojos × 60 days, `patrol`, fixed scenario, market open):** three buying policies in
the same setup, with training off (rate 0) and on (0.04).

| Buying policy | Treasury without training | Treasury with training | Dojos closed (off → on) |
|---|---|---|---|
| Most stats per gold | 147 | 392 | 18.8% → 13.0% |
| The best you can afford | 204 | 435 | 13.2% → 6.5% |
| **The most talented** you can afford | 112 | 370 | 15.5% → 11.8% |

The third policy was **added** in this round: the real basis of the "buy a cheap raw candidate and train
them" strategy is not cheapness but `Talent`, and the policy looking at stats per gold did not read
talent at all — so it did not represent that strategy.

Two things show. First, training **narrowed the gap but did not close it**: buying a ready-made warrior
is still ahead (435 against 392), with the gap down from 57 gold to 43, proportionally from 39% to 11%.
Second and more importantly, the reason for the remaining gap is not the weakness of training but
**roster turnover**: in this setup there are ~7 deaths per dojo in 60 days and a trained warrior dies
before accumulating anything (only ~27 total training days per dojo). In a dojo played selectively (the
`rising` band, offer mode) the same number rises to 128 training days and the best warrior goes from 387
to a score of 465 in 60 days. **Training is a long-term investment; what makes it visible is surviving.**
This is the same constraint §11 already stated: the binding resource is not gold but the roster.

**The rate was locked at 0.04.** The rationale closes on itself: at this rate a well-running dojo brings
its best warrior to a score of ~465 in 60 days, i.e. right up against the ~473 limit at which the market
ceiling starts to bite — the market replaces up to a point, beyond that only training gets you there. At
0.02 the ceiling never speaks (training stays decorative); at 0.08 the dojo rescues itself through
training (treasury from 147 to 941, dojos closed from 18.8% to 3.0%) and the price of playing badly
disappears.

The **school tree** was layered on top of this rate (the same day, below): the drill-hall branch
multiplies the rate by ×1.30 and the inner dojo by ×1.30 again, and the kata master raises the ceiling.
0.04 is the base itself, not the rate of a facility-equipped dojo.

### The school tree and the warrior's path (2026-09-04)

The rule is in §10. The question here is a single one: **which branch pays for itself?** A branch is only
visible when it is run alone; when all of them are taken, the money leaving the treasury hides which
branch it served. That is why the measurement got its own switch (`--school-only`).

**Measurement (400 dojos × 180 days, `patrol`, fixed scenario, market open, path on):**

| School | Final treasury | Preserved their capital | Fights | Deaths | Dojos closed |
|---|---|---|---|---|---|
| none | 731 | 21.0% | 55.4 | 9.95 | 9.8% |
| **Drill hall** only | 1299 | 28.7% | 58.2 | **6.36** | 7.2% |
| **Infirmary** only | 86 | 3.2% | 36.5 | 9.03 | 14.5% |
| **Steward** only | **1718** | 33.8% | 60.8 | 11.27 | 9.8% |
| all (cheapest first) | 461 | 8.2% | 38.8 | 8.17 | 9.2% |

It says three things.

**The drill hall sells survival:** deaths fall from 9.95 to 6.36, dojos closed from 9.8% to 7.2%. It ends
up in the same place as the training round's finding — a trained warrior does not die, and a warrior who
does not die accumulates.

**The steward sells money, not safety:** the highest treasury (1718) but **deaths rise** (11.27). The
mechanism is clear — a cheaper dojo goes on more expeditions (60.8 fights) and pays the difference in
blood. That the two branches do not substitute for each other is exactly what is wanted.

**The infirmary in this form is a trap.** What it sells is **time**, but in this economy time is
plentiful (67% idle days) and what is scarce is gold and health. What is more, a dojo that heals fast goes
on expeditions more often, so the infirmary **increases exposure**: 786 gold is spent and dojos closed
rise from 9.8% to 14.5%. Nor is it the price — when reduced to 150/300/500 the table did not improve (188
gold, 15.5% closing). **It is a framing problem, not a number problem:** as long as the branch's output is
time, it sells nothing in a dojo where time is plentiful. It stands as an open item; a possible direction
is to tie the infirmary's output to **health** (a badly wounded warrior brought back from death) or to
**gold** (treatment standing in for medicine).

**Spreading out is worse than focusing:** the "all" row (461 gold) is below both rows that invest in a
single branch. This is a desired property — but in the form measured it is partly a product of the
policy: the sim policy takes the **cheapest** of the open nodes, i.e. it sprinkles money across three
branches.

**The warrior's path — small and in the right direction.** On the same bed, with the path off, 573 gold
and 6.3% deaths per warrior-fight; with the path on, 731 gold and 6.0% (1.05 warriors per dojo get to
choose a path). With the lock lowered to 10 days, 800 gold, 5.8% and 2.94 choices. **The lock was left at
20 days:** on a harsh bed most warriors die before they can see the path, while in a healthy (selective)
dojo 3.87 of four warriors see it in 60 days. That is the sentence we want — **the path is what surviving
buys you.**

None of the school's numbers are **locked**; the only thing locked is what the branches sell.

### The long horizon: the curve's ceiling and the risk premium (2026-09-04)

A side finding from the school round: a dojo that looked healthy over 60 days **went bankrupt over 180** —
treasury 0, 83% idle days, 83% of offers turned down. Digging it out produced two separate faults, one in
the measurement tool and one in the design.

**The measurement fault: the net calculation was not counting replacement.** `NetGoldPerBattle` deducted
kit and store expenses from income but did not count the warrior bought to replace the dead one — whereas
the binding constraint is the roster anyway (§11). Once the item was added the table changed: the old
economy measurement's "net 31.3 gold per fight" becomes **18.5 gold** once replacement is deducted too;
in offer mode it is **negative** even over 60 days (−0.9). That is, the daily-offer economy never paid for
itself; the 60-day window hid this because the dojo spent most days idle.

**The design fault: the curve's ceiling was far above the dojo's ceiling.** The dojo's growth has limits
(stat ceilings 90/180, a four-man roster, kit tiers); the curve's was 3.0, and every enemy came at that
power, up to three of them. The result: past a certain point no offer is worth taking and the dojo wastes
away from unemployment.

Two numbers were locked.

- **`MaxPower` 2.2** (formerly 3.0). The ceiling was placed at the same point as `DireThreshold`: **Dire
  is not the curve's destination but the fluctuation at its peak.** The first 60 days are unaffected — the
  curve does not touch the ceiling until day 66, so the numbers locked earlier stand.
- **A risk premium of 0.25, past 100 health** (`RiskPremium`, `RiskFreeEnemyHealth`). As long as the
  reward stays strictly proportional, the upper end of the curve is **never** worth taking: three strong
  enemies carry three times the *health* but more than three times the *risk* (all three strike at once,
  escape is harder). The premium leaves ordinary work as it is and pays more than proportionally for
  heavier work.

**Measurement (400 dojos, `patrol`, offer mode, market + school + path on, a policy that accepts offers up
to 1.5× its roster):**

| Setting | Days | Final treasury | Preserved their capital | Refusal rate | Hungry days | Dojos closed |
|---|---|---|---|---|---|---|
| old (ceiling 3.0, no premium) | 60 | 283 | 8.5% | 29.6% | 0.3% | 0.5% |
| **new (2.2 + premium)** | 60 | 326 | 11.0% | 29.3% | 0.3% | 0.8% |
| old (ceiling 3.0, no premium) | 180 | 75 | 3.5% | 64.7% | 30.1% | 8.5% |
| **new (2.2 + premium)** | 180 | **2288** | **40.0%** | 53.7% | 18.6% | **2.5%** |

The early game does not move, the long horizon straightens out: the number of fights rises from 57.7 to
79.5 and the refusal rate falls from 64.7% to 53.7% — that is, the point is not "easier" but **the offer
being worth taking again**.

Two notes:

- **A dojo that sticks to a fixed band still wastes away**, and that is correct. A policy that enters no
  offer above the `rising` band records −17.9 gold per fight over 180 days and spends 57% of its days
  hungry but **does not close** (1.0%): the price of refusing growth is not death but slow decay. The
  threat band is deliberately absolute — saying "hard for you" would hand the player's decision to the
  game (§10) — so a player who plays by the band has to raise that band **themselves**.
- **The fixed-scenario bed now reads richer** (1226 → 3146 gold over 60 days): `patrol` offers a heavy
  three-enemy encounter every day, and the premium lands exactly there. That bed measures an upper bound,
  not the game itself; the economy's real bed is offer mode.

### Random events (2026-09-04)

There is a **15%** chance per day of a mishap. There are five types. All of them subtract — no donations,
no treasure, no good news: §11 describes events as buffer pressure, and a two-way table would remove that
pressure.

| Event | Effect |
|---|---|
| Theft | **At most 12%** of the treasury goes; nothing is taken from an empty treasury |
| Spoiled provisions | That day's food costs **at most double** |
| A fouled well | That day's water costs **at most double** |
| Mouldy medicine | Those in the infirmary go without medicine that day — healing drops to its natural rate |
| Illness | A healthy warrior falls into the infirmary for **1-3 days** without fighting |

**The severity is not fixed; it is drawn each time between zero and the upper bound.** A fixed rate would
turn the mishap into a calculable tax: if the player knew the loss in advance, keeping a buffer would not
be a decision but arithmetic.

**Theft falling flat on an empty treasury is deliberate (decided 2026-09-04).** An empty treasury is not a
permanent state: the moment the player starts saving for armour, a repair or a new warrior, the treasury
fills — and theft bites at exactly that moment. That is, the event works as the price of the decision to
save. Producing an alternative for an empty treasury (reaching for the provisions instead) would break
that link and punish a sinking dojo a second time.

**The effect hits the treasury and the calendar, not the store.** Because the daily shopping fills the
store to exactly what is needed (no stock is accumulated), stolen provisions would amount to nothing;
that is why events either take gold, or enlarge that day's need, or take a warrior's day. A mishap is
processed **before upkeep**: spoiled provisions should make that day's shopping more expensive, not be
deferred to the next day.

Like an offer, an event is a **pure** function of the day and the seed (it is not written to the save and
cannot be changed by reloading) but it is generated with **a separate salt**: had the same salt been used,
the day a heavy offer arrived would always bring a theft too, and the two systems would collapse into one.

**Measurement** (400 dojos × 60 days, `patrol`): the mishap rate eats into the buffer but does not end the
game on its own.

| Daily event probability | Final treasury | Preserved their capital | Hungry days | Dojos closed |
|---|---|---|---|---|
| 0% | 945 | 66.2% | 4.8% | 1.2% |
| **15% (chosen)** | **815** | **60.8%** | **6.1%** | **1.5%** |
| 30% | 562 | 44.8% | 9.4% | 3.0% |

At 15%, about a seventh of the buffer goes to mishaps — the pressure is felt, but the item that determines
the treasury is still medicine and warriors. (The numbers belong to the randomised-severity version; in the
first measurement with a fixed rate the loss was larger: 766 gold, 58.5%.) The number is **not locked**;
the rate and the severity will be settled in Phase 9 together with the other numbers.

### Measurement (1000 dojos × 60 days, the `patrol` scenario)

The economy was measured in a separate scenario. The other scenarios are each a **balance probe** and are
deliberately set up harsh — death rates per warrior-fight in the 38-49% band. Such a fight cannot be
fought every day; a price measured with that roster would really be measuring the price of warriors.
`patrol` is the ordinary day's encounter: victory 98.6%, death per warrior-fight 7%.

| Measure | Result |
|---|---|
| Income / kit expense / net per fight | 114.5 / 31.1 / **31.3 gold** |
| *(2026-09-04 correction)* net, with replacement also deducted | **18.5 gold** — see "The long horizon" |
| Daily consumption | 34.5 gold |
| Fights in 60 days | 39.4 (per dojo) |
| Idle days (roster not enough for an expedition) | 33.8% |
| Hungry days | 4.4% |
| Deaths / warriors bought (per dojo) | 6.48 / 5.90 |
| Dojos preserving their capital | 66.4% |
| Dojos closed | 1.2% |

The reward curve is narrow: at 0.35 only 14.3% of dojos preserve their capital and 10.4% of days are spent
hungry; at 0.70 the treasury rises to 3719 gold and money stops being a constraint. 0.45 is the knee
between the two — the dojo stays standing but does not get rich, and a third of its days pass waiting on
the infirmary.

> **What the measurement really says:** the economy's binding constraint is not gold but the **roster**.
> Once encounter difficulty goes above 20% per warrior-fight, no price tuning keeps the dojo standing —
> income goes into replacing warriors and the roster starts to melt. That is why the difficulty curve (§10)
> is also the economy's curve.

---

## 12. Visual Style

**Dark Edo woodblock print × layered paper theatre.**
Ukiyo-e is the aesthetic reference; paper theatre is the production language.

### The split: clean assets, textured screen

Texture is **not baked into the assets, it is applied to the screen as a shader**.

| Layer | Content |
|---|---|
| Assets (limb drawings) | Flat colour, hard-edged two-tone shading, thick outline, **no gradients**, transparent background |
| Full-screen overlay (`CanvasLayer`) | Paper grain, slight ink bleed, edge vignette |
| Global colour (`CanvasModulate`) | Time of day (dawn/day/night) — **the blood/vermilion tint stays out of it** |

Rationale: the rig rotates 15 pieces at runtime. If texture and light are baked into a piece, the light
direction becomes wrong when the limb rotates and every piece has to be lit individually — unaffordable in
a one-person production. With the texture coming from the screen, the assets stay clean and the woodblock
feel comes from a single place and for free. The same layer also produces the day-cycle variants for free.

### Why paper theatre

It turns the rig's weakness into a style. When a flat limb rotates mechanically, on a painted figure it
reads as "why is the drawing turning like that"; in paper cutout it reads as "a paper piece is turning".
The same motion becomes acceptable.

### The palette — seven roles

| Role | Use |
|---|---|
| Paper / ground | Warm off-white — the scene's base value |
| Ink / shadow | Pitch dark — outline and shadow mass |
| Stone / metal | Neutral grey |
| Indigo | **The player's team** |
| Ochre | **Enemies** |
| Earth / wood | Structures and furniture |
| Vermilion | **Blood and critical emphasis only** — used nowhere else |

The value gap is between the figure and the ground: the scene is light paper, the figure a dark mass.
Because the distinction is one of **value** rather than colour, it survives being scaled down.

### Armour tiers are distinguished by silhouette, not by colour

Distinguishing by colour is not possible: the palette has to tell the two teams apart at the same time. The
difference comes from **volume and shape**, and the silhouette grows markedly with each tier:

| Tier | Silhouette |
|---|---|
| Bare | Narrow, soft, draping cloth |
| Leather | Fitted to the body, sharp-edged |
| Wooden boards hung on cord | Flat plates hanging from the shoulder — clumsy, irregular, standing off the body |
| A piece of iron | A single plate; part of the body is metal, the rest cloth |
| Half iron | Body and shoulders in metal, arms/legs open |
| Full iron | Armour covering everything; broad shoulders, a horned kabuto, the silhouette nearly doubled |

> **A note on tone:** this list looks less at the court samurai than at the world of the **ramshackle
> rōnin** (boards hung on cord, a single piece of iron). That is deliberate: the dojo starts poor, and as
> the equipment gets richer the silhouette gets heavier. Visual progression becomes the direct counterpart
> of the economy.
>
> **The tier list is an example, not locked.** What is locked is the rule: the material vocabulary will be
> shared across all slots (so a mixed set looks like character, not accident) and the silhouette will grow
> visibly at every tier.

### Armour is worn slot by slot

Armour is not a single piece; it is worn and upgraded **per slot**: helmet · body · shoulder (right/left) ·
arm (right/left) · skirt/hip · shin (right/left).

- A blow lands on a region (see §7 → Hit region); **the damage and the limb-severing risk are determined by
  that region's armour**, not by the warrior's average
- The side (right/left) determines **where you were struck**, not what the penalty is: whichever arm is
  severed, the stat penalty is the same
- With a limited budget the decision of **which limb to protect** arises; this ties directly to the weapon
  proficiency in §4 — protecting the arm of a two-handed master is protecting a lifetime's work

> Reference: in Domina armour works exactly like this — helmet, body, shoulder guard, skirt and greave are
> separate pieces, and "each piece of armour absorbs damage when the wearer is struck in that region". With
> us the **limb-severing risk** is read from that piece too.

The production cost is not additional: §12 already says "a base body + equipment pieces hung on the bones",
and slot-by-slot armour is the natural consequence of that.

**Production:** no new body is drawn per tier. A single **base body** + **equipment pieces** hung on the
same bones (kabuto, shoulder guard, cuirass, skirt, arm/shin protection). Thus a tier = a combination of
pieces + palette; colour and material become the cheap axis, and only adding a new **shape** is expensive.
The piece-by-piece wearing that Open Decision #4-D wants comes free too.

The number of silhouette classes is kept limited — at 128 px what is distinguished is shape, not colour.
Within the same silhouette there can be as many colour/material variants as you like.

### Joint and severing rules

- The **shoulder pivot** stays under the *sode* (shoulder guard) drawn on the body
- The **hip pivot** stays under the *kusazuri* / hakama skirt
- **Severing happens only at the shoulder and the hip.** The elbow, knee, wrist and ankle **move but do not
  come off**: in the core `BodyPart` recognises only `Arm / Leg / Eye`, and there is no mechanical difference
  between severing at the wrist and severing at the arm — both render that arm unusable
- The hand and the foot **do not have to be separate drawings**; `RigPose` gives them no separate angle, they
  inherit their parent's rotation. If they are merged into the forearm and the shin, four drawings per tier
  are saved
- Every limb piece carries an **overlap margin** at its upper end; it is hidden beneath the parent piece
- The cut surface: **a flat dark outline + a flat vermilion disc**. No bone/anatomy detail — it disappears at
  128 px. What carries the severing is not the stain itself but **the silhouette of the missing chain**

### Art reference

Ukiyo-e woodblock print (the Total War: Shogun 2 interface, Muramasa), Trek to Yomi (contrast), Okami (paper
texture). Sumi-e was **eliminated**: a grey figure over a grey wash, the warrior does not separate from the
ground and four armour tiers do not read in monochrome.

---

## Idea Book (not decisions)

This is for ideas that have not been decided but should not be forgotten. None of them is work that has
entered the plan; it should not be confused with the Open Decisions table.

### Prosthetics and gear for maimed warriors

Disability is currently one-way: the warrior weakens and there is no way to compensate (§7 "Permanent
penalties"). If compensation is ever wanted, its shape would be a **prosthetic slot**: pieces such as a
tekagi (a claw for the one-armed), a wooden leg, or a helmet with a scope for someone who has lost an eye
partly give back the disability's multiplier. The valuable part is that it turns §7's "retire them or keep
using them" decision into **"do I invest in them"**: the maimed veteran becomes a character money is spent
on. A richer version would also load each piece with its own price (a tekagi gives strength back but cannot
block, and so on).

Not written for now: the measurable half of 4-D (weight, slots) is locked, while this means a new equipment
slot and a new measurement round.

---

## Open Decisions

> **State after the decision round (2026-09-07).** Some rows of the table below were reopened in the round
> or added as new rows. The round's own record and rationale are in `docs/COMPARISON-DOMINA.md` → `# 12` and
> `# 13`; the text there is a **historical record**, and what binds is this file.

| # | Topic | Note |
|---|---|---|
| **13** | **Engine-free core — reopened** | Item 9.1 of the decision round. The rule is in force for now (the resolver knows no engine, is seeded, produces an event stream) but the user has not made the decision. Three options are on the table: (a) it continues as is, (b) the decision stays in the core but position/distance/animation timing are left entirely to Godot, (c) resolution is moved into the engine. **(c) conflicts with the "measure as each system enters" decision** — no sim means no measurement. **The biggest open item of this round**; it determines both the architecture and the ROADMAP | **Leaning after the 2026-09-08 round: (a), with one rider** — real time lives in Godot while the core stays on a fixed tick, so the engine carries the pausing, the speed and the flow of the day without the resolver moving. Three grounds were written down: (1) "measure everything first, then move to (c)" is not a saving — every system has to be written into the engine-free core to be measurable at all, so (c) would be a second implementation of the same rules; (2) once the sim is gone a ported number cannot be shown to still hold — real-time float timing, frame order and collision order drift, and the drift is invisible without a rig; (3) balance is not a single pass — every new enemy kind or weapon reopens the numbers, and without a sim retuning is playtest only, which a 180-day permadeath run makes slow and noisy. What (c) would save is **already paid for**: position, distance, projectile flight and the weapon on the ground are written and tested (`Combat/ArenaPoint.cs`, `Projectile.cs`, `GroundWeapon.cs`); the core work left (classes, staff, morale, season) is bookkeeping, which an engine barely helps with. **Not locked** — the user deliberately left it open. The right moment to reconsider (c) is **after the build plan's step 8**, once every system is written and measured and the game can be played end to end, as a rewrite decision rather than an architecture decision taken now.
| **14** | **Scarcity and a fourth resource** | Whether **sake** will be added to the stock resources and how scarcity will work — both to be tied to the morale system (§10 morale). Left at ⏳ in the decision round |
| **15** | **A save backup must not be an undo gate** | The versioned, merge-on-load save is kept, and an automatic backup rolled on top of it is coming. In a game with permadeath, the backup should **not** have an in-game "go back to the previous day" option; how it will be presented (or whether it will be presented at all) is to be decided |
| ~~16~~ | ~~The opponent pool~~ | **Closed (2026-09-10). The enemy is human; there are no monsters.** The story (`STORY.md`) makes every fight in a season part of one rival school's protection racket, and a creature encounter belongs neither to that racket nor to the 180-day clock — it would be filler. The change cost nothing mechanically: an enemy is only a stat block, so the five kinds kept their numbers and only their identities were rewritten (`Campaign/Bestiary.cs` → `Campaign/Adversaries.cs`, `YokaiKind` → `EnemyKind`; kappa → collector, kitsune → cutthroat, tengu → duelist, oni → kabukimono, jorōgumo → senior student). **Every measurement taken before the rename still holds.** §1 and §3 were rewritten with it |
| ~~17~~ | ~~Rival dojos and the village~~ | **Closed (2026-09-10)** — the full decision is in §10, "The rival school and the settlements". In short: 12 settlements with a three-state allegiance and a 0-2 warning level; the rival moves every 7 days on the **same tick** as the compulsory-fight rule, which stays (the move counter was briefly written as a replacement for it — void 2026-09-10); settlements never pay gold or stores on a schedule (one guaranteed item on the day it comes over, and a wider offer queue as the real return); the rival stores one number only (deniability) and his strength is derived from the settlements he holds; the opposite-pole model comes back **only on his axis** and the three-party system is untouched. Rejected with it: passive income, per-settlement traits, rival doctrines, a rescue loan. **The numbers are proposals awaiting measurement** |
| **18** | **Numbers that arrived with the round** | New numbers awaiting measurement: the weapon-drop chance from stunning, the `class × implement` multipliers, the weapon mastery bonus, the reward band 0.75-1.25, staff wages, facility construction times, class facility prices, the frequency of classed candidates in the market, the reward multiplier of a surrender-forbidden contract, the honour penalty for a missed mandatory fight |
| ~~1~~ | ~~Party size~~ | **Locked (2026-08-29).** The upper bound is **4**, the number is the player's decision; encounters such as duels/raids can impose an exact number (§10). The core already supports N warriors. **Follow-up work:** a four-warrior arena will cause camera and readability problems in 2.2 |
| ~~2~~ | ~~Expedition/map structure~~ | **Closed (2026-08-29).** An expedition is one room/one fight; **one encounter offer** per day, take it or leave it; no map screen. **No boss structure is being built** — difficulty rises on a single curve (§10) |
| 3 | Adversary behaviour | Which kinds, and each one's special combat behaviour. **The input is ready (2026-09-03):** the behavioural difference will not be separate code but the target-selection weights of §4 tuned per kind. **The numbers side is written (2026-09-04):** `Domina.Core/Campaign/Adversaries.cs` — collector, cutthroat, duelist, kabukimono and senior student are scaled on a single power curve and take their place on it (§10). The named adversaries of the story are absent: §10 builds no boss structure. **Renamed 2026-09-10 with #16** — the numbers did not move. The natural axis for the behaviour weights is now **discipline**: a street man picks the nearest target, a school man finishes the wounded and then turns to the most dangerous. **All that remains open is behaviour** — when that field is added to the mould, encounter generation does not change |
| 4-A | Equipment — melee weapons | **Locked.** They fit the existing `Weapon` model (a factory + a balance number): wakizashi, tantō, naginata, kanabō, kama, bō/jō, ono, tekagi. The grip lines are in §4 |
| 4-B | Equipment — those requiring new rules | **Stunning locked (2026-09-02)** — the rule and the numbers are in §7; the blunt class now has its payoff (where cutting was 91.57% and blunt 88.68%, both are now ~92%). **Blade catching locked (2026-09-03)** — the jitte/sai now fill the gap the shield left: base chance 0.24, lock 0.6 s, ×0.75 against a two-handed weapon; all three options are best at something (the katana at victory, 73.09%, the sai at limb protection, 0.45%). **Poison locked (2026-09-03)** — the dose goes around armour: 2.5 per tick, tick 1.0 s, lifetime 6.0 s, maximum dose 3.0; the poisoned tantō is level with the katana in an open fight (72.19% against 73.09%) and ahead in front of an armoured enemy (77.19% against 68.62%). Poison neither severs limbs nor stuns, and does not stop for someone pulling out. **Dropping a weapon locked (2026-09-03)** — armour's answer: base chance 0.05 on a blow landing on armour, 0.05 on a caught weapon, tendency to leave the hand 1.0 cutting / 0.6 piercing / 0.2 blunt. The weapon does not break, it is thrown 250 units behind the opponent; anyone with an empty hand (the one who dropped it, a teammate, an enemy) can pick it up, and anyone with a weapon in hand neither picks up nor looks. In front of an enemy in ō-yoroi the trade turns (nodachi 87.53%, tetsubo 89.20%); pick-up is 7.3% one-on-one and 40.4% in 3v3. **The item is closed** — the rule and numbers are in §7. **No shields:** a hand-carried shield was not common in Japanese warfare (the *tate* is a fixed pavise planted in the ground); the same mechanical need is met by the jitte/sai |
| 4-C | ~~Equipment — those requiring space/projectiles~~ | **Closed (2026-08-14).** The core gained projectiles: a `ThrownWeapon` is carried in a separate slot, the throw spends time in the air, and during the flight the target can flee/die/leave the field. The yumi and the fukiya come by the same route — only the range/speed/ammunition numbers differ. Makibishi is still open: that is a consumable, not a projectile |
| ~~4-D~~ | ~~Equipment — armour and the maimed warrior~~ | **Armour wear added (2026-09-03):** a piece wears by as much damage as it stops, falls apart on the spot when its pool runs out, and is **gone permanently**; wear accumulates on the warrior and is not used up in a single fight (ō-yoroi ~15 fights, keikogi ~7). The rule and numbers are in §7. **Locked (2026-09-02).** Armour comes in three tiers (keikogi / dō-maru / ō-yoroi) carried in **six slots**: head, body, sword arm, off arm, right leg, left leg. A kit has a **weight** and lengthens the attack cycle (`ArmorAttackSlowdownAtFullWeight` 0.75, full ō-yoroi = 16) — the table in §7. The maimed warrior's penalties were sided: sword arm ×0.65, off arm ×0.85, each leg ×0.55 evasion / ×0.60 speed. **Equipment specific to the maimed (prosthetics) was not written** — it stands in the "Idea Book" as an idea, not an open decision |
| ~~5~~ | ~~Economy numbers~~ | **Locked (2026-09-04).** A single currency, gold; prices and daily consumption are tabulated in §11. Victory reward 0.45 per enemy health, armour 1.50/durability, repair 0.90/wear, medicine 12, a warrior 150, starting capital 600. Measured over 1000 dojos × 60 days (the `patrol` scenario): net 31.3 gold per fight, 33.8% idle days, 1.2% of dojos closing. **The constraint the measurement revealed:** the binding resource is not gold but the roster — once encounter difficulty goes above 20% deaths per warrior-fight, no price keeps the dojo standing. **Random events were also added (2026-09-04):** 15% per day, five types, all subtractive; the measurement is in §11 (~a fifth of the buffer). **Long-horizon correction (2026-09-04):** the net calculation was not counting replacement — corrected, the net on the same bed is not 31.3 but **18.5**; the curve's ceiling was pulled to **2.2** and a **risk premium** (0.25, past 100 health) was added to the reward, measurement in §11 "The long horizon". **Still open:** optional risky matches (§11) |
| ~~6~~ | ~~Visual style~~ | **Locked 2026-08-13 — see §12** |
| 7 | The game's name | Not yet ("Domina" is only the folder name — not the final name) |
| ~~2b~~ | ~~The end of the campaign~~ | **Closed (2026-09-07).** A fixed 180-day countdown, a mandatory fight every 7 days (penalised in honour), a 3-bounty gate, a 5-round final tournament, losing the final = game over. The rule and rationale are in §10 |
| ~~2c~~ | ~~Are there classes~~ | **Closed (2026-09-07).** The class system is in and stands side by side with the Path; classes are unlocked by facilities, and the special mechanics work as a `class × implement` product (§4) |
| ~~12~~ | ~~Is block a separate state~~ | **Locked (2026-09-03).** A separate state: `CombatState.Blocking`. The decision comes from the Defence stat (`Defence ÷ 100 × 0.45`), the condition is reading the incoming blow, the duration is 0.8 s, and what it holds is 70% of the damage × the weapon's block quality. A blocked blow does not sever a limb, and 75% of blunt concussion gets through. Measured: victory 71.21% → 72.39%, limb loss 5.19% → 4.96%. The rule and numbers are in §5. **The rule has no brake of its own** — as `MaxBlockChance` grows the gain is one-way; the brake is the stat competing in the dojo, and the number is Phase 9's job |
| 8 | Honour threshold numbers | The decay rate, the effect coefficient of targeted commands — through playtest. **The honour price of fleeing is here too** (`RetreatHonorPenalty`, §5): the rule is locked, the number is not. **The seppuku threshold was set provisionally (2026-09-04):** **30** out of 100, a pardon at **45** (§6). The user's decision — not locked, to be played with in playtest |
| ~~9~~ | ~~Partial reward on escape~~ | **Dropped (2026-08-29).** If an expedition is a single fight there is no such thing as loot collected in earlier rooms; pulling out erases that fight's reward, and that is all (§10) |
| ~~10~~ | ~~The up-front price of an expedition~~ | **Closed (2026-08-29).** Entering costs **a day** (even if you flee), and the enemy roster is **partly** visible — only a threat marker (§10) |
| ~~11~~ | ~~Charge numbers~~ | **Locked (2026-09-02)** — the table is in §4. Distance 320, probability 0.40, wind-up 0.75 s, speed 1.6, damage 1.5. During the measurement a **wind-up phase was added**: the charge's price was only written down, and because the run took 0.6 s it was never measured. Also added during the measurement were the **re-ignition** and **stats + crowd** rules; the charge is no longer an opening move and the decision comes out of the warrior's identity. **Follow-up work:** in the adversary behaviour decision (#3), "who it charges" can be used as a character trait — an enemy that charges recklessly pays the price of defencelessness |
