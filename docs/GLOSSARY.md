# Terms

The Japanese terms used in the project and the core's mechanical vocabulary. The aim is to
see, in one place, what a term is and **what job it does in the game** while reading the GDD
or the code. The numbers here are a summary; `docs/GDD.md` is what binds.

---

## Weapons

### One-handed

| Term | What it is | Its job in the game |
|---|---|---|
| **katana** | The classic samurai sword, cutting | The balanced option. In measurement its "best place" is against an **armoured enemy** |
| **wakizashi** | A short sword, the katana's companion | Faster, less damage |
| **tantō** | A dagger | It has a poisoned form: 7 damage / 0.85 s |
| **kama** | A sickle; short-hafted, cutting | A short cutting weapon of farming-tool origin |
| **ono** | An axe | A heavy one-handed cutting weapon |
| **tekagi** | A claw/hook worn on the fingers | A short-reach clawing weapon |
| **jitte** | **A forked iron rod, not sharp.** An Edo-period law-enforcement weapon: it holds the sword with its fork without breaking it | The main implement of sword catching (grip 1.0). 14 damage / 1.00 s, blunt class |
| **sai** | A three-pronged iron baton, the jitte's relative (a prong on both sides) | It catches more (grip 1.25; 3.71 catches per fight against the jitte's 2.75). 14 damage / 1.05 s, blunt |

### Two-handed

| Term | What it is | Its job in the game |
|---|---|---|
| **nodachi** | A huge long sword | Heavy, slow, high damage. Hard to catch (leverage ×0.75) |
| **naginata** | A polearm with a curved blade at the end | Melee with reach |
| **kanabō** | A studded/knobbed iron club | Blunt |
| **tetsubo** | The heavy version of the kanabō, an iron mace | The heavy end of the blunt class; the weapon that carries stunning |
| **bō / jō** | A long staff (~180 cm) / a short staff (~125 cm) | Non-lethal blunt |
| **yari** | A straight-tipped spear | Piercing (armour piercing 0.5 / 0.15) |

### Thrown

| Term | What it is | Its job in the game |
|---|---|---|
| **shuriken** | A throwing star/blade | 12 damage, 4 ammo; the poisoned form is 12 damage / 2 ammo |
| **kunai** | A throwing knife (of digging-tool origin) | A short-range projectile |
| **yumi** | The Japanese longbow (asymmetric; the lower part is short) | It entered in the decision pass as a **two-handed ranged class** |
| **fukiya** | A blowgun, it fires poisoned needles | A poison-carrying projectile |

---

## Armour

| Term | What it is | Its job in the game |
|---|---|---|
| **keikogi** | Training clothing, cloth — not counted as armour | The lightest kit. Durability 1, ~7 fights, 40 gold |
| **dō** | A cuirass (chest armour) | Armour's core piece, the torso slot |
| **dō-maru** | Light body armour that wraps the body (foot soldier's armour) | The middle tier: durability 7 |
| **ō-yoroi** | "Great armour" — the mounted samurai's heavy box armour | The most expensive and most protective: durability 16, ~15 fights, 570 gold |
| **kabuto** | A helmet | The head slot (it comes with the ō-yoroi kit) |
| **kote** | An armoured sleeve | The arm slot; heavy kote come in the ō-yoroi kit |
| **suneate** | Shin armour, a greave | The leg slot; heavy suneate come in the ō-yoroi kit |
| **ō-sode** | The ō-yoroi's wide shoulder plate | In the decision pass it became **the piece that takes the shield's place**: because a hand shield was rejected, the shoulder slot carries the block's equipment side |
| **tate** | **Not a hand-carried shield** — a fixed wooden screen planted on the ground | That is why it is not equipment: in the decision pass it was made a **field feature** |

---

## Mechanical terms

### Catching — "chance" and "bind"

Catching is the **second defensive axis**, tried before evasion. A die is rolled every time an
enemy attempts a strike in melee.

**Chance** = how often it holds.

```
chance = 0.24 × grip × catchability × leverage   (+ the Accuracy share)
```

- **grip** — the defender's implement: jitte 1.0, sai 1.25, **everything else 0**
- **catchability** — the attacker's weapon: cutting 1.0, piercing 0.7, blunt 0.25, fists 0
- **leverage** — ×0.75 if the attacker's weapon is two-handed
- **the Accuracy share** — +0.5 at Accuracy 100 (tied to Accuracy, not to Evasion)

Example: a warrior with a sai against an enemy with a katana → 0.24 × 1.25 × 1.0 = **30%**.
The same warrior against an enemy with a two-handed nodachi → ×0.75, so **22.5%**.

**Bind** = what you gain when it holds. Two things happen at once:

1. The blow is **erased** (no damage)
2. The attacker is exposed for **0.6 seconds** — a bound warrior **does not walk, strike or evade**

0.6 s looks short, but a typical strike takes 0.85-1.05 s: the bind effectively eats the
enemy's next strike. Evasion makes the blow miss and ends there; catching erases the blow **and
gives free time**. Its price is **16 stamina** — a warrior who catches constantly tires.

How the measurement reads: the jitte/sai does not win more than the katana; what it wins is
**not coming home maimed** (because the enemy strikes less, limb loss falls).

### Stunning

A heavy blow rolls **two separate dice**: dismemberment and stun. Which one holds is set by the
weapon's class — that is the trade:

| Class | Dismemberment multiplier | Stun multiplier |
|---|---|---|
| Cutting (katana, nodachi) | 1.0 | 0.25 |
| Piercing (yari) | 0.5 | 0.15 |
| Blunt (tetsubo, kanabō) | 0.15 | **1.0** |

A stunned warrior **freezes for 0.9 seconds**: he does not walk, does not strike and **cannot
evade**. The real teeth are the evasion closing. The die is only rolled when the blow passes
**20%** of maximum health; the base chance is 0.35, ×2.0 for a blow to the head. A warrior
pulling out is not stunned, and a stunned one is not stunned again (the duration is not
refreshed).

This rule is the blunt class's **return**: a blunt weapon loses to a cutting one on
dismemberment (0.15 against 1.0), and what it gains is freezing.

### Weapon drops

A weapon **does not break, it falls** — it lies at a point in the arena and returns to its owner
when the fight ends. The die is rolled on **the attacker's** weapon: an edge that bites into
plate twists and the weapon leaves the striker's hand.

| Trigger | Base chance |
|---|---|
| A strike landing on armour | 0.05 |
| A caught weapon levered out of the hook | 0.05 |

| Class | Tendency to leave the hand | Rationale |
|---|---|---|
| Cutting | 1.0 | An edge that bites into plate twists |
| Piercing | 0.6 | The tip slides, the haft stays in the palm |
| Blunt | 0.2 | A rebounding club does not leave the palm |
| Fists | 0 | There is nothing to drop |

The weapon is flung **250 units behind the other man** — in measurement what carried the price
was not the distance but the **direction**: if it falls behind him or to the side, the rule is
free and even helpful. **Anyone** empty-handed can pick it up (the one who dropped it, a
teammate, an enemy); a warrior with a weapon in hand neither picks up nor searches, and does not
pick up a weapon he cannot use. An unarmed warrior fights with his fists (8 damage, reach 100).
A projectile knocks nobody's weapon out.

The rule's price varies with the shape of the fight: 7.3% of dropped weapons are picked up in
1v1, 40.4% in 3v3.

> **The third trigger (decided 2026-09-07, not yet measured):** a stun also drops the weapon.
> When a stun holds, a separate die is rolled and for the first time it is **the defender** who
> drops his weapon; the chance is set by the stunned warrior's own weapon class (the tendency
> table above works in the second direction too — a blunt weapon is hard to drop). The base
> chance will be looked for where it does not make the blunt class dominant.

### The stats

Eight numbers (`WarriorStats`), plus Honour which stands apart. The recruit base values are in
brackets.

| Stat | Code name | What it sets |
|---|---|---|
| **Health** | `MaxHealth` (100) | Maximum health. The dismemberment and stun thresholds are read from it too: the die is rolled when the blow passes 20% of maximum health — a warrior with high health is also resistant to being maimed |
| **Aggression** | `Aggression` (40) | How many of the charge opportunities he sees he uses. A probability of 0.35 at 0, 1.00 at 100 |
| **Defence** | `Defense` (35) | Reduces the damage taken **and gives the block chance** (`Defence ÷ 100 × 0.45`; a warrior with Defence 0 never blocks) |
| **Evasion** | `Evasion` (35) | The attempt to make a blow miss; it spends stamina |
| **Strength** | `Strength` (40) | The strike's damage |
| **Accuracy** | `Accuracy` (55) | The chance to hit **and catching** (catching is tied to Accuracy, not to Evasion — so that two defensive axes do not feed off the same stat) |
| **Stamina** | `MaxStamina` (100) | Running, evading, attacking and catching (16) spend it; as it falls, damage and accuracy drop |
| **Speed** | `Speed` (50) | Walking/running speed. Added late: while speed was fixed, chaser and fleer moved at the same rate and **escape always succeeded** |

**Honour** (`Honor`, 0-100, starting at 50) is not a combat stat: it is tied to the reward
multiplier, the seppuku threshold and the chat vote (see `docs/GDD.md` §6).

Training covers these eight stats with **four drills**: Strike (Accuracy + Aggression), Guard
(Defence + Strength), Footwork (Evasion + Speed), Conditioning (Health + Stamina) — the second
stat takes half the share.

### Limb loss

The risk of limb loss arises when a single blow passes the threshold of its **ratio to maximum
health** (20%); low health is **not** a precondition, it can happen on the first blow. A cutting
weapon severs (multiplier 1.0), a blunt one stuns (0.15). A blocked blow does **not** sever.

The warrior **does not die, he is maimed** — he keeps fighting with permanent penalties:

| Loss | Effect |
|---|---|
| **The sword arm** | Attack strength ×0.65, cannot use a two-handed weapon, switches to the one-handed animation |
| **The off arm** | Attack strength ×0.85, still cannot use a two-handed weapon |
| **A leg** (each) | Evasion ×0.55, walking speed ×0.60 |
| **An eye** | Accuracy ×0.75 |

The losses combine (two legs → speed ×0.36). The reason the two arms are separated: the sword
arm is the strike itself, the off arm is balance — both end two-handed weapons, but for someone
fighting one-handed the loss of the off arm is bearable. The result is a decision left to the
player: **retire him or keep using him.** 16.5% of the fights won bring home a maimed warrior.

### Poison — what "dose" means

**Every hit** landed by a poisoned weapon leaves a **dose** on the defender; no die is rolled, if
the blade scratched skin the poison is in. The dose eats health once a second and that damage
goes through **neither armour nor the Defence stat** — that is poison's whole value: it does not
pierce the plate, it goes around it.

| Number | Value | What it means |
|---|---|---|
| Damage per tick | 2.5 | The health eaten per second at dose 1 |
| Tick interval | 1.0 s | How often it eats health |
| The dose's lifetime | 6.0 s | How long the poison takes to pass if he is not struck (restarted on every new strike) |
| Maximum dose | 3.0 | The cap on stacked poisoning — **this is the real knob**, not the lifetime |

Poison neither severs limbs nor stuns (both are the outcome of a *blow*; with poison there is
nobody striking — that is half the trade). Death by poison is a separate cause
(`DeathCause.Poison`). The poison of a warrior pulling out **does not stop**: the key is not an
antidote.

### The block

The block is a separate state in the core (`CombatState.Blocking`) — not a number melted into
the Defence stat.

| Item | Rule |
|---|---|
| **Chance** | `Defence ÷ 100 × 0.45`. No base: a warrior with Defence 0 never blocks |
| **Condition** | Not proximity but **a move that is read** — the enemy in reach must have his sword gathered. (In its first form the condition was only "is there an enemy in reach" and the rule *lowered* victory) |
| **Duration** | 0.8 s, during which the warrior **does not strike**. Evasion erases one blow, a block buys a **span of time** — that is what is expensive |
| **What it holds** | 70% of the damage × the weapon's block quality |
| **Rhythm** | No block comes right after a block |
| **Limbs** | A blocked blow does not sever a limb |
| **Concussion** | A blunt weapon's share works at 75% despite the stance — the blunt class's fourth gain |
| **Flank/rear** | A surrounded warrior cannot block |

**Block quality** (whatever is in hand): two-handed 1.0, blunt 0.85, cutting 0.80, piercing 0.70,
**fists 0.30** — a warrior who drops his weapon loses his block too. Giving the jitte/sai
two-handed quality was tried and it broke a locked brake (the jitte stopped being the wrong
choice in front of an enemy with a heavy weapon); both carry the one-handed blunt quality.

In the decision pass the block was **split in two**: how often blocking happens is read from the
stat, how much it cuts from the equipment (the shoulder piece, the **ō-sode**). There is no hand
shield.

### Other

| Term | What it is | Its job in the game |
|---|---|---|
| **omamori** | A cloth amulet/talisman bought at a temple | The counterpart of the reference game's "Jupiter cards": it is fitted to a warrior **or to staff**, can be removed and passed on, and sold; its supply is set by the temple relationship |
| **seppuku** | Honourable suicide | A warrior whose honour falls below the threshold (30) goes to a chat vote; a ronin majority → permanent death, a bushi majority → a pardon (honour 45) |
| **bushi** | A warrior/samurai | A chat command: honour (+) |
| **rōnin** | A masterless samurai | A chat command: honour (−) |
| **dojo** | A training place | The player's base; the facility tree and the staff live here |
| **sensei** | A teacher, an instructor | The staff on the training side |
| **yōkai** | The supernatural creatures of Japanese folklore | The enemy pool (the bestiary decision is still open) |
| **oni** | A demon, a horned giant | The heavy enemy archetype (it carries a tetsubo) |
| **tengu** | A winged mountain demon | The fast/ranged enemy archetype (it throws poisoned shuriken) |
