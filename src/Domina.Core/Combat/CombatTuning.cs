namespace Domina.Core.Combat;

/// <summary>
/// Every tunable number of combat in one place.
/// </summary>
/// <remarks>
/// These values are <b>estimated starting points for balance</b>, not proven
/// values. They will be tuned in phase 9 with the <c>Domina.Sim</c> batch
/// simulation (see docs/GDD.md → Open Decision #8).
/// </remarks>
public sealed record CombatTuning
{
    /// <summary>Simulation step. 20 Hz, enough to tell attack windows apart.</summary>
    public double TickSeconds { get; init; } = 0.05;

    /// <summary>
    /// The tick ceiling that stops a fight nobody can finish. <b>Not a design rule.</b>
    /// </summary>
    /// <remarks>
    /// The old <c>MaxBattleSeconds</c> did two jobs at once: it declared a fight past 180 s a draw,
    /// and it kept <see cref="Battle.Run"/> — a loop with no other exit — from running forever. The
    /// design job was dropped (docs/ROADMAP.md, build order step 1); only the safety job remains.
    /// The value sits far above any real fight — the longest of the 24 measured scenarios,
    /// <c>jitte-armored</c>, peaked at 138.6 s over 20.000 fights — so reaching it means two sides
    /// that can neither close nor finish. That is an <b>anomaly to be looked at</b>, reported as
    /// <see cref="BattleOutcome.Stalled"/>, never a result to balance around.
    /// </remarks>
    public double StallGuardSeconds { get; init; } = 900;

    // ---- Arena and movement ----

    /// <summary>Width of the arena along the line.</summary>
    public double ArenaWidth { get; init; } = 1920;

    /// <summary>Depth of the arena. Encircling and flanking happen on this axis.</summary>
    public double ArenaDepth { get; init; } = 420;

    /// <summary>The teams' starting distance from the centre.</summary>
    public double StartOffsetX { get; init; } = 480;

    /// <summary>Starting depth spacing between warriors on the same team.</summary>
    public double StartSpacingY { get; init; } = 120;

    /// <summary>Walking speed when the Speed stat is 0 (units/second).</summary>
    /// <remarks>
    /// While speed was a single constant, chaser and fleer moved at the same rate; since
    /// net closing was zero, <b>escape always succeeded</b>. The ends were chosen so that
    /// 50 lands on the old constant (240), which preserves the existing balance baseline.
    /// </remarks>
    public double MoveSpeedAtZeroSpeed { get; init; } = 150;

    /// <inheritdoc cref="MoveSpeedAtZeroSpeed"/>
    public double MoveSpeedAtMaxSpeed { get; init; } = 330;

    /// <summary>Speed multiplier of the one fleeing — back turned, balance broken.</summary>
    /// <remarks>
    /// This is the single tuning knob of the chase. 0.85 is too harsh: the fleer can never
    /// open a gap and, after contact, pressing the key almost always cost at least one death
    /// (56% partial escape). At 0.92 pressing early still works and pressing late burns you.
    /// </remarks>
    public double RetreatSpeedMultiplier { get; init; } = 0.92;

    /// <summary>
    /// Warriors cannot get closer to each other than this — it prevents overlap.
    /// </summary>
    public double PersonalSpace { get; init; } = 74;

    /// <summary>How much further to close after entering reach (0-1).</summary>
    /// <remarks>
    /// 1.0 stops at exactly full reach; stopping at the edge of reach makes attacks miss
    /// because the target is moving too. Stepping slightly inside is more stable.
    /// </remarks>
    public double PreferredReachFraction { get; init; } = 0.85;

    /// <summary>What an attack from behind adds to hit chance.</summary>
    /// <remarks>
    /// This is the mechanical meaning of encircling: when you are surrounded, someone is
    /// necessarily behind you.
    /// </remarks>
    public double FlankHitBonus { get; init; } = 0.25;

    /// <summary>Damage multiplier of an attack from behind.</summary>
    public double FlankDamageMultiplier { get; init; } = 1.25;

    /// <summary>Distance required for a fleeing warrior to count as having left the arena.</summary>
    public double ExitMargin { get; init; } = 220;

    // ---- Charge ----

    /// <summary>
    /// Probability of launching a charge when an opening appears — <b>at Aggression 0</b>.
    /// </summary>
    /// <remarks>
    /// The charge decision comes out of the warrior's identity: the bold one throws himself
    /// forward, the measured one closes the distance walking. Aggression already sets attack
    /// frequency (<see cref="SpacingSecondsAtZeroAggression"/>); this is the same stat's
    /// second job. The die is rolled <b>once per opening</b> (docs/GDD.md §4), so these
    /// numbers are not "a chance tried each second" but <b>how many of the openings he sees
    /// he uses</b>.
    /// </remarks>
    public double ChargeChanceAtZeroAggression { get; init; } = 0.35;

    /// <inheritdoc cref="ChargeChanceAtZeroAggression"/>
    /// <remarks>
    /// <para>
    /// Opportunity evaluation says <b>when</b> a charge is possible; this curve says
    /// <b>which warrior</b> uses that opportunity. When a gap opens, the measured warrior
    /// mostly chooses to walk, the bold one leaps.
    /// </para>
    /// <para>
    /// Measured (3v3): 0.35-1.00 gives 1.88 launches / <b>1.66 completed charges</b> per
    /// fight — since the die is rolled per opening, this stands in for the frequency the old
    /// per-second band of 0.12-0.45 produced (1.71 completed). The curve alone is the
    /// frequency knob: under the same rule 0.12-0.45 drops to 0.78 launches, and 0.50-1.00
    /// rises to 2.15.
    /// </para>
    /// <para>
    /// Upper end <b>1.00</b>: the boldest warrior uses every opening he sees. Because the
    /// bottom of the band rose too, Aggression's discriminating power narrowed (old ratio
    /// 3.75x, new 2.86x) — in exchange, charge frequency became independent of the warrior's
    /// speed and the <c>Speed</c> axis came alive for the first time (3v3 victory 83.8% at
    /// Speed 0, 87.1% at Speed 100).
    /// </para>
    /// </remarks>
    public double ChargeChanceAtMaxAggression { get; init; } = 1.00;

    /// <summary>
    /// The windup spent in place before the run starts. The warrior does not move during it
    /// and <b>the first hit he takes breaks the charge</b> (docs/GDD.md §4). His defence
    /// continues at its normal rate — a blow he can dodge does not take the move away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This duration is <b>what determines the distance a charge needs</b>: while winding up,
    /// the enemy keeps walking, and reaching you takes
    /// <c>(distance − enemy reach) ÷ enemy speed</c>. If the windup is longer than that, the
    /// charge breaks before it even launches.
    /// </para>
    /// <para>
    /// Measured: from 320 units Tengu arrives in 0.73 s, Kappa in 0.88 s, Oni in 0.87 s.
    /// 0.75 s was chosen for that reason — <b>only a fast enemy</b> can break a charge that
    /// launched at the threshold, and the speed stat does its first real job as "the thing
    /// that breaks a charge".
    /// </para>
    /// </remarks>
    public double ChargeWindupSeconds { get; init; } = 0.75;

    // Note: the charge has NO "minimum distance" setting and must not have one. The warrior
    // does not look at a fixed threshold, he looks at the opening: "can nobody hit me right
    // now, and do I have time to finish my windup?" The distance needed derives from that —
    //     enemy reach + enemy speed × ChargeWindupSeconds
    // — that is, it comes out differently for each enemy. Measurement confirmed this: the old
    // constant, hand-locked to 320, sat right in the middle of the 287-327 band this formula
    // produces for the current roster. For the same reason there is no separate crowd
    // restriction either: if three enemies can reach you, there is no opening.

    /// <summary>Speed multiplier during a charge.</summary>
    /// <remarks>
    /// <b>Measured: this axis barely touches balance</b> (3v3 victory 86.5% at 1.0, 85.4% at
    /// 1.6, 83.7% at 3.0 — high speed is slightly unfavourable, because arriving earlier means
    /// entering the enemy line earlier). So it is not a balance knob but a <b>presentation
    /// knob</b>: 1.6 so that a charge looks like a charge on screen.
    /// </remarks>
    public double ChargeSpeedMultiplier { get; init; } = 1.6;

    /// <summary>
    /// The damage share added to the arrival blow of a warrior running at the arena's maximum
    /// walking speed. The actual multiplier is
    /// <c>1 + (arrival speed ÷ MoveSpeedAtMaxSpeed) × this number</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Momentum is speed.</b> The multiplier is not fixed, it comes out of the warrior's
    /// real speed at the moment of arrival — meaning both <see cref="ChargeSpeedMultiplier"/>
    /// and the warrior's <c>Speed</c> stat feed into damage. A heavy Oni's charge cannot be
    /// as hard as a Tengu's.
    /// </para>
    /// <para>
    /// This turns the speed axis, which measured as <b>inert</b>, into a live one and gives
    /// the <c>Speed</c> stat a second job in the dojo: until then it only set base walking
    /// speed in the core. The pattern comes from Mount &amp; Blade's couched lance — there too
    /// damage depends on the horse's speed (see docs/DESIGN-REFERENCES.md §3).
    /// </para>
    /// <para>
    /// Because limb-loss risk comes from the damage/maxHP ratio (docs/GDD.md §7), the charge's
    /// maiming probability falls out of this <b>on its own</b>; there is no separate
    /// dismemberment multiplier.
    /// </para>
    /// </remarks>
    public double ChargeDamageAtFullSpeed { get; init; } = 0.43;

    /// <summary>
    /// The probability that the <b>target</b> of a charge lands a counter-hit. Other enemies
    /// passed on the way always get their free hits; this number is only for the warrior
    /// facing the charge head-on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reason for the distinction is timing (docs/GDD.md §4): hitting a body running past
    /// you is easy, meeting a body coming at you at exactly the right moment is hard. So the
    /// target's counter-hit is not certain the way it is in the normal combat sequence, it is
    /// <b>rare</b>.
    /// </para>
    /// <para>
    /// Measurement put a <b>floor</b> under this number, and what sets that floor is not the
    /// charge but the escape rule: the counter-hits the target collects are the main income of
    /// the outnumbered side and the thing that stops a numbers advantage from snowballing.
    /// Below it, docs/GDD.md §5's promise that "pulling out reduces death" <b>inverts</b>
    /// (3v3, 20,000 fights: at 0.25 pulling gives 41.5%, not pulling 40.0%). 0.6 is that floor
    /// itself — chosen by constraint, not by taste: pulling 39.6%, not pulling 40.3%.
    /// </para>
    /// <para>
    /// What carries the fiction is not the rate but the counter-hit's <b>consequence</b>: when
    /// it lands, the charge's momentum dies (see <c>Combatant.ChargeMomentumBroken</c>).
    /// Weight instead of rarity — and without spawning a new tuning number.
    /// </para>
    /// </remarks>
    public double ChargeTargetCounterChance { get; init; } = 0.6;

    /// <summary>
    /// If a charge takes this long, the target counts as unreached and the move is wasted.
    /// </summary>
    /// <remarks>
    /// Without a time limit the charge would turn into a permanent speed bonus that chases a
    /// fleeing target indefinitely, and it would wreck §5's escape balance.
    /// </remarks>
    public double ChargeMaxSeconds { get; init; } = 4.0;

    // ---- Attack rhythm ----

    /// <summary>Wait between attacks at Aggression 0.</summary>
    public double SpacingSecondsAtZeroAggression { get; init; } = 1.4;

    /// <summary>Wait between attacks at Aggression 100.</summary>
    public double SpacingSecondsAtMaxAggression { get; init; } = 0.30;

    /// <summary>The uninterruptible (windup) part of attack duration; the rest is recovery.</summary>
    public double WindupFraction { get; init; } = 0.6;

    // ---- Hit and evasion ----

    public double BaseHitChance { get; init; } = 0.55;
    public double AccuracyHitBonus { get; init; } = 0.004;

    /// <summary>Evasion chance at Evasion 100.</summary>
    public double MaxEvasionChance { get; init; } = 0.45;

    /// <summary>Vulnerability while pulling out: no evasion or block, plus a hit bonus against him.</summary>
    public double RetreatingHitBonus { get; init; } = 0.30;

    /// <summary>Base hit chance of a throw; lower than melee.</summary>
    /// <remarks>
    /// A ranged attack must not be free: the price of being able to hit from a distance is
    /// missing more often and doing less damage. Otherwise putting a shuriken in everyone's
    /// pocket would be the dominant strategy.
    /// </remarks>
    public double BaseThrowHitChance { get; init; } = 0.40;

    /// <summary>The fraction hit chance falls to at the very edge of range.</summary>
    /// <remarks>
    /// Throwing from point blank is almost as accurate as melee, throwing from the edge is a
    /// hopeful shot — this is what makes distance a two-way decision.
    /// </remarks>
    public double ThrowFalloffAtMaxRange { get; init; } = 0.55;

    // ---- Damage ----

    /// <summary>Multiplier applied to weapon damage at Strength 100.</summary>
    public double StrengthDamageBonusAtMax { get; init; } = 0.8;

    /// <summary>The fraction damage is reduced by at Defence 100.</summary>
    public double MaxDefenseReduction { get; init; } = 0.45;

    public double MinimumDamage { get; init; } = 1;

    // ---- Target selection ----

    /// <summary>Score penalty per arena unit that must be walked to the target.</summary>
    /// <remarks>
    /// Left on its own, the rule collapses to its old form: pick the nearest. The other
    /// weights are measured against it — 100 units of walking is worth one fully exposed body
    /// region.
    /// </remarks>
    public double TargetDistanceWeight { get; init; } = 1.0;

    /// <summary>
    /// The distance at which wound and exposure gains fall to zero (beyond reach, arena units).
    /// </summary>
    /// <remarks>
    /// An opportunity window: the warrior finishes the wounded man in front of him, he does not
    /// walk to the one at the far end of the arena. Left unbounded, measurement turned the rule
    /// into a difficulty increase.
    /// </remarks>
    public double TargetOpportunityRange { get; init; } = 200;

    /// <summary>Score gain for an enemy whose health is fully gone (scales with the ratio).</summary>
    public double TargetWoundedWeight { get; init; } = 120;

    /// <summary>Score gain for an enemy whose armour is fully broken (scales with the exposed-region ratio).</summary>
    public double TargetExposedWeight { get; init; } = 90;

    /// <summary>Score penalty per teammate already locked onto the same target.</summary>
    public double TargetCrowdPenalty { get; init; } = 60;

    /// <summary>Score advantage of the current target — the cost of switching.</summary>
    public double TargetStickiness { get; init; } = 80;

    // ---- Block ----

    /// <summary>Probability that a warrior in reach goes into a block stance at Defence 100.</summary>
    /// <remarks>
    /// <para>
    /// It is built the same way as evasion (<see cref="MaxEvasionChance"/>) and <b>not</b> the
    /// same way as the charge (<see cref="ChargeChanceAtZeroAggression"/>): there is no base,
    /// so a warrior with Defence 0 never blocks. The rationale is twofold — the Defence stat is
    /// no longer only a passive damage reduction, it now buys a <b>move</b> visible on screen;
    /// and a baseless threshold lets tests that are not exercising the rule switch blocking off
    /// by zeroing the stat (unlike the charge).
    /// </para>
    /// </remarks>
    public double MaxBlockChance { get; init; } = 0.45;

    /// <summary>
    /// Duration of the block stance — the warrior does not strike during it.
    /// </summary>
    /// <remarks>
    /// This is the cost of blocking and it is deliberately <b>more expensive than evasion</b>:
    /// evasion erases a single blow and leaves the warrior in his attack cycle, a block buys a
    /// <b>span of time</b>. If the duration were short, a block would be a free second evasion;
    /// if it were long, a defensive warrior would never strike at all.
    /// </remarks>
    public double BlockSeconds { get; init; } = 0.8;

    /// <summary>The damage share erased from a blocked blow (scales with the weapon's block quality).</summary>
    public double BlockDamageReduction { get; init; } = 0.70;

    /// <summary>The share of dismemberment risk that remains on a blocked blow.</summary>
    /// <remarks>
    /// Zero: a blocked blow does not take a limb. This is where blocking parts ways with
    /// evasion — evasion is a die, a block is a <b>guarantee</b>: the warrior who takes the
    /// stance does not lose his arm to that blow. It is the only certain promise the Defence
    /// stat makes against the game's signature punishment.
    /// </remarks>
    public double BlockDismembermentShare { get; init; }

    /// <summary>The share of stun risk that remains on a blocked blow.</summary>
    /// <remarks>
    /// The blunt class's gain against blocking. The share is kept high because if a block cut
    /// off blunt weapons as completely as it cuts off blades, the blunt weapon's only answer
    /// would be erased in front of a defensive warrior. The stance protects against steel, not
    /// against concussion.
    /// </remarks>
    public double BlockStunShare { get; init; } = 0.75;

    /// <summary>The stamina cost of every blocked blow.</summary>
    public double BlockStaminaCost { get; init; } = 5;

    // ---- Stamina ----

    public double AttackStaminaCost { get; init; } = 6;
    public double DodgeStaminaCost { get; init; } = 12;
    public double StaminaRegenPerSecond { get; init; } = 4;

    /// <summary>Below this fraction of stamina, damage and hit chance drop.</summary>
    public double LowStaminaThreshold { get; init; } = 0.3;
    public double LowStaminaPenalty { get; init; } = 0.65;

    // ---- Armour weight ----

    /// <summary>
    /// The total armour weight at which the penalties apply in full. That is a complete
    /// ō-yoroi; lighter armour takes the penalty proportionally.
    /// </summary>
    /// <remarks>
    /// Armour had no in-combat cost and ō-yoroi was superior on every axis: victory 68% → 96%,
    /// death 41.6% → 16.3%, limb loss 8.6% → 0.4%, in exchange for nothing. The only brake was
    /// price, and that does not exist until the economy numbers arrive. Weight is the field
    /// equivalent of §7's promised "heavy cuirass, bare arms" decision.
    /// </remarks>
    public double ArmorWeightAtFullPenalty { get; init; } = 16;

    /// <summary>How much the attack cycle stretches at full weight.</summary>
    /// <remarks>
    /// <para>
    /// This is weight's <b>only</b> line of effect, and it being so came out of measurement.
    /// Two lines were tried and dropped: a penalty written onto stamina regeneration measured
    /// as <b>nothing</b> (at a 90% cut, victory 92.34% → 92.33%), and a penalty written onto
    /// walking speed did not budge victory yet erased §5's promise — the armoured warrior was
    /// caught before he could leave the arena, so the "Flee" key stopped reducing death
    /// (pulling 46.35%, not pulling 46.44%; against 44.32% vs 46.33% with no penalty). The
    /// reason: fights end through the damage exchange and neither line touched that exchange.
    /// The sword slowing down lands directly on damage output.
    /// </para>
    /// <para>
    /// 0.75 was chosen because that is the threshold where the trade turns (3v3, 20,000 fights,
    /// <c>losing:0.7</c>): dō-maru wins the fight (71.8% victory, 40.3% death), ō-yoroi buys not
    /// coming back maimed (limb loss 0.82% against 3.38%). All three tiers are best at
    /// something. At 0.60 ō-yoroi is still ahead on every axis (76.3% / 37.0%), at 0.90 heavy
    /// armour is outright bad (64.3% victory).
    /// </para>
    /// </remarks>
    public double ArmorAttackSlowdownAtFullWeight { get; init; } = 0.75;

    // ---- Stun ----

    /// <summary>
    /// If a single blow's ratio to maximum health exceeds this, the stun die is rolled.
    /// </summary>
    /// <remarks>
    /// It starts from the same place as the dismemberment threshold
    /// (<see cref="GrievousSeverityThreshold"/>) but it is a separate knob: the two are two
    /// distinct outcomes of <b>the same heavy blow</b>, and where the blunt/blade trade turns
    /// can only be found by sweeping them separately.
    /// </remarks>
    /// <remarks>
    /// Swept (<c>blade</c>/<c>club</c>, 20,000 fights, <c>losing:0.7</c>): at 0.20 the two
    /// classes are level (blade 92.06%, blunt 92.08% victory). At 0.30 stun almost never fires
    /// and blunt falls behind again (89.07% against 91.55%) — the problem the rule solved comes
    /// straight back. At 0.10 <b>blades stun too</b> (0.26 taken per warrior) and both sides
    /// weaken at once.
    /// </remarks>
    public double StunSeverityThreshold { get; init; } = 0.20;

    /// <summary>Base stun chance on a heavy blow; weapon, body region and armour scale it.</summary>
    /// <remarks>
    /// <para>
    /// 0.35 is where the trade turns <b>exactly</b>: what the blunt weapon loses on the
    /// dismemberment multiplier (0.15 against 1.0) it takes back here. Measured (same warrior,
    /// same enemy, only the weapon different — <c>blade</c>/<c>club</c>, 20,000 fights):
    /// without the rule, blades took 91.57% and blunt 88.68% victory; the blunt weapon was bad
    /// on <b>every axis</b>. At 0.35 the two sit at 92.06% / 92.08%. At 0.60 blunt goes ahead
    /// (93.83%), at 1.00 it is outright dominant (95.67%).
    /// </para>
    /// <para>
    /// The player pays for it too: in 3v3 the Oni's tetsubo now bites, and player victory drops
    /// from 69.31% to 65.20%. Absolute balance is phase 9's job; the number here holds the
    /// <b>ratio</b> between the classes.
    /// </para>
    /// </remarks>
    public double BaseStunChance { get; init; } = 0.35;

    /// <summary>How long a stunned warrior is frozen.</summary>
    /// <remarks>
    /// A stun stops <b>the warrior, not the move</b>: he does not walk, strike or evade. The
    /// duration is kept shorter than the attack cycle — a long one would hand the stunning side
    /// a free execution window.
    /// </remarks>
    /// <remarks>
    /// The measurement was surprising: between 0.5 and 0.9 there is <b>no difference at all</b>
    /// (blunt victory 92.06% / 92.08%). The reason is that in this band a stun mostly lands in
    /// a gap where the warrior was waiting anyway — the biting side of the rule is not the lost
    /// move but the <b>closed evasion</b>. The teeth appear above 1.0 seconds: at 1.4 blunt
    /// jumps to 94.16%. 0.9 was chosen because it sits just under that threshold and is long
    /// enough to read on screen.
    /// </remarks>
    public double StunSeconds { get; init; } = 0.9;

    /// <summary>The multiplier a blow to the head applies to stun chance.</summary>
    /// <remarks>
    /// This is the kabuto's in-combat meaning. The region weights (§7) already make the head
    /// rare; if the rare thing has no heavy consequence, the helmet is only a damage number.
    /// </remarks>
    /// <remarks>
    /// Measured (3v3, 20,000 fights): at multiplier 1.0 there are 0.35 stuns per warrior, at
    /// 2.0 there are 0.39, at 3.0 there are 0.42. The axis works but is soft — since the head
    /// hit weight is 10, a very large number here does no more than magnify a rare event.
    /// </remarks>
    public double StunHeadMultiplier { get; init; } = 2.0;

    /// <summary>
    /// How much of armour's resistance to dismemberment also counts against stun.
    /// </summary>
    /// <remarks>
    /// Plate does not stop a blow the way it stops a cut — blunt force goes through underneath
    /// the armour. The reason a single share is used instead of a separate <c>ArmorPiece</c>
    /// field is to keep the difference between armour's two resistances a <b>single measurable
    /// number</b>.
    /// </remarks>
    /// <remarks>
    /// Measured (3v3, full armour, 20,000 fights): at share 0 there are 0.51 stuns per warrior,
    /// at 0.6 there are 0.33, at 1.0 there are 0.22. 0.6 was chosen because 4-D's tier trade
    /// survives — dō-maru gives fewer deaths at every share (43.78% against 44.15%), ō-yoroi
    /// protects the limb (0.83% against 3.43%). A share of 1.0 would make armour too good
    /// against blunt weapons and would erase the blunt class's only gain in front of the most
    /// expensive armour.
    /// </remarks>
    public double ArmorStunResistanceShare { get; init; } = 0.6;

    // ---- Sword catching ----

    /// <summary>
    /// Base chance of catching an incoming strike with a catching implement; weapon, grip and
    /// accuracy scale it.
    /// </summary>
    /// <remarks>
    /// While rejecting the shield, GDD §4 said "the jitte/sai fills the same mechanical need";
    /// there was no equivalent in the code. Catching is defence's <b>second</b> axis after
    /// evasion: evasion makes the blow miss and it ends there, catching stops the blow <b>and</b>
    /// leaves the attacker exposed.
    /// </remarks>
    public double BaseCatchChance { get; init; } = 0.24;

    /// <summary>The stamina cost of a catch. Paid only for the die that holds.</summary>
    /// <remarks>
    /// It is more expensive than evasion (<see cref="DodgeStaminaCost"/>): evasion pulls the
    /// warrior out of where he was, a catch holds the other man's entire weight. If it were not
    /// expensive, a catching implement would be a free second layer of defence.
    /// </remarks>
    public double CatchStaminaCost { get; init; } = 16;

    /// <summary>How long the attacker whose weapon is caught stays exposed.</summary>
    /// <remarks>
    /// The rule lives on this duration: if catching only erased damage it would be a weak
    /// evasion. The real return is the window in which the attacker is bound and <b>cannot
    /// evade</b> — the catcher's own low damage is made up for in that window.
    /// </remarks>
    public double CatchBindSeconds { get; init; } = 0.6;

    /// <summary>The multiplier applied to the catch chance of a strike from a two-handed weapon.</summary>
    /// <remarks>
    /// This is catching's own answer — otherwise the jitte would be the right choice in every
    /// matchup. A nodachi's leverage tears a one-handed hook off; the warrior who chooses a
    /// heavy weapon collects his return here.
    /// </remarks>
    public double CatchTwoHandedFactor { get; init; } = 0.75;

    /// <summary>The share added to catch chance at Accuracy 100.</summary>
    /// <remarks>
    /// Catching is a matter of timing, not of reflex: where evasion hangs on Evasion, catching
    /// hangs on <b>Accuracy</b>. If both defensive axes fed off the same stat, a catching
    /// implement would only help a warrior with high evasion, and the equipment decision would
    /// be a copy of the stat decision.
    /// </remarks>
    public double CatchAccuracyBonusAtMax { get; init; } = 0.5;

    // ---- Dropping the weapon ----

    /// <summary>
    /// Base chance of the weapon being knocked out of the hand on a strike that lands on armour;
    /// the weapon's tendency to slip and the hardness of the piece struck scale it.
    /// </summary>
    /// <remarks>
    /// This is armour's <b>second</b> answer. The first is reducing damage; but measurement
    /// showed that, together with poison, armour's sign can invert (docs/GDD.md §7). Disarming
    /// stops the plate from being only a damage number: the warrior who strikes steel spends
    /// <b>his weapon</b>. Not breakage but dropping: the weapon comes back when the fight ends,
    /// the cost is the rest of the fight.
    /// </remarks>
    public double BaseDisarmChance { get; init; } = 0.05;

    /// <summary>
    /// The chance of losing the weapon when it is caught; the weapon's tendency to slip scales it.
    /// </summary>
    /// <remarks>
    /// This is the jitte's historical job: the hook does not only hold, it levers the weapon out
    /// of the palm. The die is not multiplied by hardness on its own (a caught blade is already
    /// in the hook), so it is <b>stronger per event</b> than a strike on armour; in exchange,
    /// catching is rare.
    /// </remarks>
    /// <remarks>
    /// Swept (<c>jitte</c>/<c>sai</c>/<c>katana</c>, 20,000 fights, <c>losing:0.7</c>). With the
    /// rule at 0, jitte takes 74.87% and katana 75.02% — the catching implement pays in damage
    /// as before and gains nothing from disarming. At 0.05 jitte goes ahead with 78.00% / sai
    /// with 78.88%, but both brakes hold: it is still the wrong choice against an enemy carrying
    /// a nodachi (35.22% against katana's 37.84%) and outright bad against an armoured enemy
    /// (41.38% against 60.32%). At 0.10 the first brake breaks (jitte-heavy 38.27%, katana
    /// 37.84%): the catching implement becomes the answer to the heavy weapon too and
    /// <c>CatchTwoHandedFactor</c> becomes meaningless.
    /// </remarks>
    public double CatchDisarmChance { get; init; } = 0.05;

    /// <summary>
    /// How much of the struck piece's dismemberment resistance is read as <b>hardness</b>.
    /// </summary>
    /// <remarks>
    /// Same rationale as the share used for stun (<see cref="ArmorStunResistanceShare"/>): if
    /// armour had a separate "hardness" field, every piece would need maintenance in two places.
    /// A strike landing on a bare region <b>never</b> disarms — what breaks the grip is plate,
    /// not flesh.
    /// </remarks>
    public double ArmorHardnessShare { get; init; } = 1.0;

    /// <summary>
    /// How far the dropped weapon is flung from the warrior.
    /// </summary>
    /// <remarks>
    /// This distance is the rule's real cost. If the weapon fell at his feet the warrior would
    /// pick it up on the next step and disarming would cost nothing; flung far, the cost turns
    /// into <b>walking</b> — for that whole span the warrior has nothing but his fists, and the
    /// direction he walks is away from the fight.
    /// </remarks>
    public double WeaponDropDistance { get; init; } = 220;

    /// <summary>The distance at which a weapon on the ground can be picked up.</summary>
    /// <remarks>
    /// An unarmed warrior <b>walks</b> to the weapon; this radius only answers the question "did
    /// he get there?". It is kept smaller than personal space (<see cref="PersonalSpace"/>) so
    /// that the moment of bending down is the same place the warrior is standing.
    /// </remarks>
    public double WeaponPickupRadius { get; init; } = 60;

    /// <summary>
    /// The scale of an armour piece's durability pool. 0 = armour never wears.
    /// </summary>
    /// <remarks>
    /// The pieces' own durability lives in <see cref="Model.ArmorPiece.Durability"/>; this
    /// multiplier scales all of them at once, so it becomes balance work's single knob.
    /// </remarks>
    public double ArmorDurabilityScale { get; init; } = 1.0;

    // ---- Poison ----

    /// <summary>
    /// Damage poison deals in one tick (at dose 1). Armour and Defence do <b>not</b> reduce it.
    /// </summary>
    /// <remarks>
    /// The whole rule hangs off this: poison is the only route around damage reduction. Plate
    /// stops a cut, stops a share of blunt force (<see cref="ArmorStunResistanceShare"/>), and
    /// does not stop poison at all — because poison works on blood, not on armour. The poisoned
    /// weapon's own low damage is the price of that.
    /// </remarks>
    /// <remarks>
    /// Measured (<c>poison</c>/<c>katana</c>, 20,000 fights, <c>losing:0.7</c>): at 2.5 the
    /// poisoned knife is level with the katana in an open fight (72.19% against 73.09%) but goes
    /// ahead against an armoured enemy (77.19% against 68.62%). At 1.2 poison only rescues a
    /// weak weapon and never gets through armour (74.00% / 55.99%); at 3.0 it is dominant on
    /// both axes (79.25% / 88.47%).
    /// </remarks>
    public double PoisonDamagePerTick { get; init; } = 2.5;

    /// <summary>The interval at which poison deals damage.</summary>
    /// <remarks>
    /// It is kept independent of the tick duration (<see cref="TickSeconds"/>): if poison were
    /// tied to the simulation step, the 20 Hz resolution would multiply its damage by twenty too.
    /// </remarks>
    /// <remarks>
    /// The interval is not a neutral knob, it is damage rate directly: the same dose gives 94.93%
    /// victory at 0.5 s, 72.19% at 1 s and 30.40% at 2 s. 1 s was chosen because it is a rhythm
    /// that can be read one tick at a time on screen, and because dividing the dose's lifetime
    /// (<see cref="PoisonSeconds"/>) by it makes poison something <b>countable</b>: six ticks.
    /// </remarks>
    public double PoisonTickSeconds { get; init; } = 1.0;

    /// <summary>The lifetime of one dose. Every new strike restarts the timer.</summary>
    /// <remarks>
    /// In measurement, anything past 6 seconds does almost nothing: 52.90% at 3 s, 68.83% at 4.5,
    /// 72.19% at 6, 73.11% at 9. The reason is that the poisoned weapon strikes fast and keeps
    /// refreshing the timer — a long lifetime only extends what happens after the <b>last</b>
    /// strike, and in most fights that is a fight already over.
    /// </remarks>
    public double PoisonSeconds { get; init; } = 6.0;

    /// <summary>The maximum dose that can accumulate on one warrior.</summary>
    /// <remarks>
    /// Without a cap the poisoned weapon would be a self-feeding spiral: every strike grows the
    /// dose, the growing dose kills the enemy without slowing him, and the weapon's low damage
    /// would stop being the price of anything.
    /// </remarks>
    /// <remarks>
    /// This is the real balance knob: at 1 the poisoned knife is outright bad (16.71%), at 2 it is
    /// still behind (50.92%), at 3 it is level with the katana (72.19%), at 5 it is dominant
    /// (82.25%). The cap also writes the rule's <b>upper bound</b> — the fourth strike of a warrior
    /// carrying three strikes' worth of poison is thrown for damage, not for poison any more.
    /// </remarks>
    public double PoisonMaxDose { get; init; } = 3.0;

    // ---- Limb loss ----

    /// <summary>
    /// If a single blow's ratio to maximum health exceeds this, it counts as a "heavy blow" and
    /// the dismemberment die is rolled. Low health is NOT A PRECONDITION — it can happen on the
    /// first blow too (see docs/GDD.md §7).
    /// </summary>
    /// <remarks>
    /// Lowered from 0.28 to 0.20: limb loss is the game's signature mechanic but it had dropped to
    /// a few per thousand in measurement. The threshold was pulled just under where weapon damage
    /// clusters — there is no difference at all between 0.24 and 0.28, because no blow falls in the
    /// interval between them.
    /// </remarks>
    public double GrievousSeverityThreshold { get; init; } = 0.20;

    /// <summary>Base dismemberment chance on a heavy blow; weapon and armour scale it.</summary>
    /// <remarks>
    /// Lowered from 0.35 to 0.05. 0.35 was tuned against the old outcome tree, in which
    /// dismemberment fired <b>only in the escape window</b>; once the tree split in two and a heavy
    /// blow that does not kill also started taking limbs (docs/GDD.md §7), the same number pushed
    /// limb loss to 45%. Swept (3v3, 10,000 fights, <c>losing:0.7</c>) — death and victory rates do
    /// not move appreciably with this knob, only limb loss scales.
    /// </remarks>
    public double BaseDismembermentChance { get; init; } = 0.05;

    /// <summary>
    /// The health left to a warrior saved from a killing blow by the "Flee" key.
    /// </summary>
    /// <remarks>
    /// The key turns death into limb loss (docs/GDD.md §7) but it does not grant health: the warrior
    /// spends the rest of the escape one blow away from dying. Survival is not guaranteed, only a
    /// chance.
    /// </remarks>
    public double SurvivalHealthAfterIntervention { get; init; } = 1;

    // ---- Hit region ----

    /// <summary>
    /// The weights for where a blow lands. They are read relative to each other, they do not have
    /// to sum to 1.
    /// </summary>
    /// <remarks>
    /// The torso is deliberately dominant: if the regions were equal, torso armour would be
    /// worthless, since it is only one of four armour pieces.
    /// </remarks>
    public double TorsoHitWeight { get; init; } = 45;

    /// <summary>The weight is <b>per leg</b>; two legs together make 25.</summary>
    /// <inheritdoc cref="TorsoHitWeight"/>
    public double LegHitWeight { get; init; } = 12.5;

    /// <summary>The weight is <b>per arm</b>; two arms together make 20.</summary>
    /// <inheritdoc cref="TorsoHitWeight"/>
    public double ArmHitWeight { get; init; } = 10;

    /// <inheritdoc cref="TorsoHitWeight"/>
    public double HeadHitWeight { get; init; } = 10;

    // ---- Pulling out ----

    /// <summary>The defenceless span between the flee command and leaving the arena.</summary>
    public double RetreatSeconds { get; init; } = 1.2;

    /// <summary>
    /// The chance of taking an accidental wound while leaving the arena.
    /// </summary>
    /// <remarks>
    /// The abstract cost of escape: a twisted ankle, a wound bleeding on the way back. It does not
    /// kill (see <c>Battle.RollEscapeMishap</c>), it exists only so that there is no such thing as
    /// a "free exit" — without it, the key pressed before contact gave a 100% clean exit.
    /// </remarks>
    public double EscapeMishapChance { get; init; } = 0.30;

    /// <inheritdoc cref="EscapeMishapChance"/>
    public double EscapeMishapMinDamage { get; init; } = 3;

    /// <inheritdoc cref="EscapeMishapChance"/>
    public double EscapeMishapMaxDamage { get; init; } = 12;

    public static CombatTuning Default { get; } = new();
}
