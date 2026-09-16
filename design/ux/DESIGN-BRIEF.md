# Domina — UI/UX design brief

A self-contained prompt for a from-scratch interface design pass. Paste the whole thing.

This brief says **what the interface has to do** — the navigation model, what lives where, what the
player must be able to tell at a glance. It does not say how to draw it. Composition, layout,
colour, type and detail are the designer's call.

---

Design the complete interface for **Domina** (working title), a samurai dojo-management and
auto-battler PC game for Steam, built in Godot 4. Start from scratch: assume no existing screens.

## 1. The game, in one paragraph

You are the newly appointed instructor of a provincial dojo in Edo-period Japan, given a
**60-day term** to prove yourself. Each day the board offers one contract — a bounty, an escort, a
raid. You choose who goes, then the fight resolves **fully automatically** while you watch: you can
only pull a man out mid-fight, at a cost to your standing. Between fights you train, treat, arm and
feed a roster of 4–8 warriors out of a single shared store. Men take permanent wounds and lose
limbs. Twitch/Kick chat can join as warriors and vote, so the audience is a real system, not a
decoration. The season ends with a bounty gate and a final tournament.

The player's loop is: **read the day → judge the risk → commit men → watch → live with the result.**

## 2. The navigation model

The structural reference is the indie game **Domina** (Dolphin Barn), a gladiator-school manager.
Take its *shape*: a **diegetic hub**, not a menu system.

- **Home is a place, not a screen.** One view of the dojo, alive, with the men in it. The player
  comes back to it after every action; it is where the day is read.
- **The world is the navigation.** No tab bar, no main menu, no icon row. Every destination is an
  object or a person standing in that place: the contract, the armoury, training, the market, the
  wounded, the shrine, the roster/ledger. What each one is should be legible from what it is, not
  from a label pinned to it.
- **Destinations open over the hub, not instead of it.** The place stays present and stays alive
  behind whatever is open; closing returns to the place, never to a menu. The fight and the
  province map are the two exceptions that may take the whole screen.
- **One persistent non-diegetic layer** holds only what must be true everywhere: the store, the day
  of the season, what is closing in, and whether time is running. Everything else belongs to the
  world.
- **Pause is a state of the world**, readable without reading a word.
- **One unit card everywhere.** A man is presented the same way in the hub, in the contract, in the
  fight and in the aftermath — never three row formats for the same person.
- Browsing the roster happens **inside** the open panel (prev/next), not by going back to a list.
- A **live counter on a blocked confirm** ("2 of 3 chosen") — the player sees what is missing, not
  just that it is refused.
- The cost of a decision appears **next to the decision**, never on the following screen.

## 3. What the reference gets wrong — do not inherit it

1. **No primary decision.** Every screen must make it obvious which single action it exists for.
2. **Lists instead of judgements.** A roster tells you who is fit; it must also tell you whether
   these men beat what is waiting.
3. **Conditions written, not felt.** Not "mud, dawn fog" plus a rules paragraph — *which man* each
   condition hurts, and how much.
4. **Buttons with no consequence.** The real cost and the real return date belong on the button
   that spends them.
5. **A log eating a third of the screen** to report the past. Keep what changed; the rest behind a
   link.
6. **Raw numbers with no thresholds.** Gold, sake and a nearly-empty medicine chest must not look
   alike; only the urgent one raises its voice.
7. **Scattered pressure.** Clock, summons, bounty gate and standing read as one thing, not four.
8. **Dangerous actions dressed as ordinary ones.** Ending a man's term must not look like Close.
9. **A disabled option with no reason.** Always name who holds the key.
10. **A fight you cannot read.** Who is winning stays legible at a glance.

## 4. Visual direction (intent only — the execution is yours)

**Dark woodblock print × Japanese paper theater.** Edo-period ukiyo-e sensibility, executed to a
modern standard, desaturated so that one red reads across a room. Two type faces at most. No emoji.

Two things are fixed because the game's meaning depends on them:

- **Blood red is blood and nothing else.** Never a warning, never an accent, never a button.
- **Armour tier is readable from a figure's silhouette alone**, with all colour stripped: robe,
  leather, partial lamellar and full plate must still be tellable apart.

Everything else — surfaces, palette beyond that, marks, edges, iconography, figure treatment — is
open. The art dresses the **world**; it is not a metaphor for the interface. The game must not look
like a board game played on paper.

## 5. Screens to design

Each at **1920×1080**, on one canvas, as its own artboard.

1. **The hub, at rest.** What the player sees on opening the day: the place, the men, what each is
   doing, and every destination reachable from here. Show one destination in its attention state so
   the interaction reads.
2. **The hub with the day's contract open.** The same screen, one thing open over it — this fixes
   how everything opens. The contract must carry: reward, cost of setting out, return date, threat
   as something comparable rather than an adjective, the field conditions tied to the men they hurt,
   who goes (live counter, party-versus-threat read), one dominant action.
3. **Roster.** One man in depth with prev/next: stats, weapon proficiency, class, permanent wounds,
   honour, kit. Dense, comparable, scannable.
4. **Armoury.** Kit slot by slot, the swap's price beside the swap, armour weight and its penalty.
5. **Market.** Warriors and goods for sale, each with the daily drain it adds.
6. **Training.** The skill tree and the day's assignment; the vacant instructor's post and what it
   costs until filled.
7. **The fight.** Full screen: two sides, health legible at a glance, the event stream the
   simulation emits, the crowd/chat presence, and the single order the player may give — pull a man
   out.
8. **The aftermath.** What it cost: wounds, limbs, gold, standing, the head taken, who did not come
   back.
9. **The province map.** Settlements, the rival school, the patron, the bounty gate, the summons.

Plus a **design system plate**: the palette and its rules, the type ramp, the anatomy of an opened
panel over the world, controls in every state, the unit card, a destination's states (resting,
attention, locked), the four armour silhouettes, and the blood rule.

## 6. Rules the design must hold to

- The hub is never fully covered, except by the fight and the map.
- Every destination is a thing with a reason to be there — never a floating button, never an icon
  pinned to scenery, never a glow whose only job is "click me".
- Every screen states its one job in its largest element.
- The same unit card everywhere.
- One widget per concept: health, spirits and a patron's regard share a gauge.
- Every cost sits beside the thing that spends it; every date is a real date in the season.
- Every disabled control says why, and who holds the key.
- Every irreversible act is shaped unlike every reversible one.
- Numbers come with a threshold or a trend, never bare.
- Readable at 1920×1080 on a television across a room: body text no smaller than 14px, never grey
  on grey.
- All UI text is **English**. No localisation layer yet.
- Static mockups, not a clickable prototype, unless asked otherwise.

## 7. Deliver

The system plate first, then the hub with the contract open as the proof of the language, then the
rest. Say what you assumed. Where a number is unknown, show a realistic sample and mark it.
