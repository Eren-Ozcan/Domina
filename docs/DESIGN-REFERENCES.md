# Design Grounds

This file holds the **externally verifiable** grounds for the decisions in `docs/GDD.md`:
which decision leans on which established design practice, where we depart from that
practice, and why.

**Why a separate file:** the GDD states the decision, this states why the decision is
*defensible*. Mixing the two makes the GDD unreadable.

**Warning — the sources do not carry equal weight.** There are three kinds of source below:
(1) designers' own talks and writing, (2) non-peer-reviewed but established industry writing,
(3) community wikis and forum measurements. The third shows what a game *does*, not *why* it
does it — we do not take numbers from there, only read the pattern.

---

## 1. The charge's trigger: an assessment of the opportunity, not a fixed threshold

**Our rule (GDD §4):** the warrior does not look at a fixed distance; for every enemy he
computes "how long until he can hit me" and considers a charge if there is a gap big enough
to finish his windup.

**The ground — utility-based AI.** The core of the approach Dave Mark presented at the GDC AI
Summit is exactly this: the agent looks not at fixed thresholds or trees but at **options
scored against the current situation**, and picks the best. The method's real value for a
designer is that the rule can be expressed in natural language — like "if you are under fire,
look for cover first". Ours reads the same way: *"if nobody can hit me and I have time to
gather, try the charge."*

**Where we depart:** IAUS produces a continuous score; ours is a **yes/no fitness gate** plus a
die scaled by Aggression. So it is not the whole utility system, only the "query the
situation" principle. There is no reason to move to scoring: the charge has no rival, the only
question is whether it happens.

**What it does not verify:** this source does not give the threshold's *number*. Our number
comes from measurement (the GDD §4 table) and derives from a formula anyway.

> [Architecture Tricks: Managing Behaviors in Time, Space, and Depth (GDC 2013)](https://www.gdcvault.com/play/1018040/Architecture-Tricks-Managing-Behaviors-in) ·
> [IAUS — Intrinsic Algorithm](https://www.gameai.com/iaus.php) ·
> [Utility system (overview)](https://en.wikipedia.org/wiki/Utility_system)

---

## 2. The windup duration: is 0.75 s readable

**Our rule:** a charge gathers in place for 0.75 s; the warrior does not move during it and
the move scatters on the first hit he takes (his defence continues at its normal rate — see §3).

**The ground — human reaction time.** Reaction to a simple visual stimulus is in the
**200-300 ms** band; 12-18 frames at 60 fps. Common design guides suggest thinking in terms of
a margin of about **0.25 s** in practice. The shared conclusion of the telegraphing literature:
the time between the signal and the blow should be long enough for the player to perceive and
answer, short enough not to make combat sluggish — and **there is no single right number**, it
depends on how long the expected answer takes.

**What it means for us:** 0.75 s is **~3 times** the reaction floor. So the windup is a
comfortable window in which a viewer can read "he is gathering, he will run". Our player does
not have to answer with reflexes (combat is fully automatic), so the lower bound is not tight
for us — what is needed is *readability*, and 0.75 s is well above it.

**The honest limit:** this does not prove that 0.75 is the **right** number; it only shows it
is *not too short to read*. The number itself came from measurement (the string of the
scatter-rate curve).

> [Reaction Time and Game Design](https://www.retrogamedeconstructionzone.com/2020/05/reaction-time-and-game-design.html) ·
> [How to Design Enemy Attack Telegraphs](https://bugnet.io/blog/how-to-design-enemy-attack-telegraphs) ·
> [Keys to Combat Design: Anatomy of an Attack](https://gdkeys.com/keys-to-combat-design-1-anatomy-of-an-attack/)

---

## 3. Commitment: speed is earned, and lost when you slow down

**The ground — Mount & Blade, the couched lance.** Couching the lance requires being **above a
certain horse speed** (in Bannerlord the threshold is a speed value of ~44); while the speed is
insufficient the lance stays up, and when it is reached the lance **visibly** drops under the
arm. The commitment breaks after a single strike, or **when you slow down enough**. **Damage
depends on speed** — a faster horse hits harder. A couched hit also **ignores** normal
directional blocking.

**What it verifies:**
- The charge having a **visible preparation state** (our windup; their lance dropping)
- The commitment breaking **under conditions**

**Where we deliberately depart:** in M&B a couched hit disables defence (directional blocking).
We had something similar for a while — a charging warrior could not evade — and it was
**removed**. Measurement showed the reason: defencelessness produced a penalty asymmetric in
*who was charging* (GDD §4, "Why the charge does not close defence"). What lets M&B carry it is
that the strike is a single one under the player's control; in our game the charge is automatic
and both sides do it.

**Applied — and it held in two steps.** M&B's "damage depends on speed" pattern was taken: the
arrival blow's multiplier is now `1 + (arrival speed ÷ maximum walking speed) × share`. **The
first measurement did not confirm the balance expectation:** the `Speed` axis was still inert
(3v3 victory 83.9% at Speed 0, 84.7% at Speed 100). The reason was not the pattern but a second
coupling on our side — because the charge die was rolled per decision step, a fast warrior
passed through the opportunity window quickly and charged **less often** (2.20 → 1.20). Our own
sampling was eating the increase the pattern gave.

Once the die was rolled once per opportunity, frequency came loose from speed and the pattern
worked as expected: victory **83.6%** at Speed 0 and **87.0%** at Speed 100 (GDD §4, "One die
per opportunity").

The lesson: a source verifies a **pattern**, not what that pattern will do in your system. In
M&B speed is the charge's only variable; in ours there was a second channel tied to speed and
it had the opposite sign. Measuring a pattern and getting a flat result does not refute it —
look for the counter-channel in your own system first.

> [Couched lance damage (Mount & Blade Wiki)](https://mountandblade.fandom.com/wiki/Couched_lance_damage) ·
> [Bannerlord couch lance guide](https://gamerempire.net/mount-blade-2-bannerlord-how-to-couch-lance/)

---

## 4. "An opportunity attack along the way" — D&D narrowed this rule twice

**Our current rule (GDD §4):** every enemy whose reach the charging warrior passes through gets
one free hit on him. Measured: **0.35** hits per charge — almost none, and what does fire is a
by-product of the difference in weapon reach.

**The ground — D&D's attack of opportunity.** The rule has been tried at the table for fifty
years and reached its present form by being **narrowed and narrowed**:

| Edition | Trigger | Count limit |
|---|---|---|
| 3.x | Movement/action in a threatened area — a broad trigger | **Can be raised** with Combat Reflexes |
| 5e | Only if you **leave his reach** | **One** reaction per turn |

In 5e circling an enemy is free; you only provoke when you leave his reach. And it can be
avoided entirely with the **Disengage** action. Two justifications stand out: making the
reaction a **decision** (the player stays engaged with the fight even out of turn) and keeping
combat fast.

**What it means for us — it confirms the direction the user proposed.** Everyone you pass
turning to hit you is 3.x's abandoned broad trigger. Established practice says:

1. The trigger should be **"he left your reach while engaged with you", not "he went past you"**.
2. There should be a **hard cap per enemy** (in our case already once per charge).
3. There should be a way to avoid it (in our case: the opportunity rule already blocks a charge
   in a crowd).

This is the same pattern as the escape window in GDD §5 — "when you enter a commitment you owe
a debt to everyone in reach". So it is not a new concept but the existing one extended to the
charge.

> [Opportunity Attacks in D&D 5e (Arcane Eye)](https://arcaneeye.com/mechanic-overview/opportunity-attack-5e/) ·
> [Why Opportunity Attacks Matter in 5e](https://screenrant.com/dnd-5e-attack-opportunity-rules-good/)

---

## 5. The angle on arrival — Total War contradicts us

**The proposal discussed:** the charge's target (the one met head-on) answers at a **low** rate,
while the warriors passed on the flank hit at a **high** rate, because they see the charger's
flank.

**The source says the opposite.** In the Total War series the answer to a charge is tied
directly to the **front**: a spear unit with the *charge defence* attribute **completely
cancels** the enemy's charge bonus **if it is braced and charged from the front**. A charge
from the flank or the rear does **not** cancel the bonus — the unit takes full damage.

So in the industry's most-tested charge model, **meeting head-on is the charger's worst angle**,
not his best. The rationale is intuitive: an enemy who is looking at you is the one who is
**ready** for you.

**But it has a condition, and that is the real lesson.** In TW not every unit does this; **the
right weapon + the right stance** are required. A spear stops a frontal charge, a sword cannot.

**The conclusion for our system:** make the **weapon**, not the angle, the criterion. We already
have a reach difference (katana 100, two-handed weapons 150). The natural rule:

> If the charge's target carries a weapon with **longer reach than the charger's**, he earns a
> meeting blow on arrival. A target with a short weapon cannot stop the momentum.

That does three things at once: it preserves the user's objection ("not everyone should turn and
hit" — only the target, and only if the condition holds), it takes TW's verified frontal rule,
and it **spawns no new number** — it uses the existing reach values. It also gives
`Weapon.Reach` a second design job: a long weapon is no longer only "strikes first" but "meets a
charge".

**Decided (2026-09-02) — and both sources were partly right.** The user's angle model was taken:
the target can answer but **not certainly**, it depends on a die (0.6); an enemy passed on the
way takes his hit for certain. The TW rule making weapon reach the criterion was **not taken** —
instead TW's real idea, *bracing cancels the charge bonus*, was carried over as it stands: when
the target's answer holds, **the momentum dies** and the arrival blow does not earn the damage
multiplier. That gives the rare answer weight and again **spawns no new number**.

The measurement also gave a surprise: this rate carries more than the charge's price. The
answers the target collects are **the main income of the outnumbered side**; when it is turned
down, the crowded side's advantage compounds and §5's escape promise collapses. 0.6 was chosen
because it is the lowest value that keeps all the locked promises standing (the table: GDD §4,
"What the target's answer carries").

**An honest note:** TW's 13 s charge bonus duration and 20% bracing bonus cannot be carried over
to us — those are unit-scale battles lasting minutes; our fights are 14 s and the bonus rides on
a single blow. We take the pattern, not the number. The sources are a community wiki and forum
measurements, not an official design document.

> [Charge Bonus (Total War: Warhammer Wiki)](https://totalwarwarhammer.fandom.com/wiki/Charge_Bonus) ·
> [Charge Defence vs. Large](https://totalwarwarhammer.fandom.com/wiki/Charge_Defence_vs._Large)

---

## 6. Is the charge a "decision" — and whose

**The measured reality (current):** the charge is **no longer** always the right move. In a duel
it is neutral (66.7% → 66.0%), for an equipped veteran it is **harmful** (98.3% → 96.1%), and in
3v3 it is a measured gain (81.6% → 84.2%). Before the defencelessness rule was removed it helped
in every scenario except `veteran`.

**The ground — Sid Meier, "interesting decisions".** The criterion is this: if the player always
picks the same option, or if the choice is random, there is **no interesting decision** there.
The kinds of decision Meier lists: personalisation, a **trade-off**, and the tension between the
short and the long term.

**Applying that criterion to us needs a correction.** Our combat is fully automatic; the player
does not decide to charge. So the criterion has to be applied **not to the moment of the fight
but to the dojo layer**: the charge should be the field counterpart of the decisions the player
makes *in preparation* (Aggression training, `Speed`, weapon reach, armour). For the decision to
be interesting there has to be a trade-off on the preparation axis.

**The current state:** Aggression sets the charge frequency and the move has a **real price** —
in two scenarios its contribution is zero or negative. The trade-off criterion is met. Two axes
are still missing: `Speed` is inert (see §3) and weapon reach only plays a role in a charge by
accident (see §5). If both are wired up, the charge becomes the place where all three
preparation axes are read at once.

> [GDC 2012: Sid Meier on interesting decisions](https://www.gamedeveloper.com/design/gdc-2012-sid-meier-on-how-to-see-games-as-sets-of-interesting-decisions) ·
> [Interesting Decisions (GDC Vault)](https://www.gdcvault.com/play/1015756/interesting)

---

## Where each number comes from

Commercial games do not publish their balance constants. So **we take no numbers from the
sources**; the sources give the pattern and the rationale, the numbers come out of the
`Domina.Sim` measurement.

| Number | Its source |
|---|---|
| Windup 0.75 s | **Measurement** (the string of the scatter curve). The sources only confirm it is *not too short to read*: the reaction floor is 200-300 ms |
| The Aggression curve 0.12-0.45 | **Measurement** — 1.71 completed charges per fight |
| The damage multiplier 1.5 | **Measurement** — the 1.25-1.5 band minimises player deaths |
| The speed multiplier 1.6 | **A presentation decision** — no balance effect in measurement |
| The 4.0 s time limit | **A safety valve** — it never fills in measurement |
| Opportunity attack: once per enemy | **The pattern is verified** — D&D 5e allows one reaction per turn |
| The distance needed | **Derived:** `enemy reach + speed × windup` |

---

## What gets added to this file

When a new design decision enters the GDD, if it has a counterpart tried outside, a section is
opened here: **what we did, what the source says, where we departed and why.** The places where
a source refuted us (like §5) are **not deleted** — that record is the valuable part.
