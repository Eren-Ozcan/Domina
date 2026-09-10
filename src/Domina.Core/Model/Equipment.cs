namespace Domina.Core.Model;

/// <summary>The wounding character of a weapon.</summary>
public enum WeaponClass
{
    /// <summary>Cutting (katana, naginata). Causes limbs to come off.</summary>
    Cutting,

    /// <summary>Blunt (tetsubo, club). Low dismemberment risk, high stun.</summary>
    Blunt,

    /// <summary>Piercing (yari, spear). Reach advantage, medium dismemberment.</summary>
    Piercing,
}

/// <summary>The weapon a warrior carries.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Class">Wounding character.</param>
/// <param name="Damage">Base damage.</param>
/// <param name="TwoHanded">Does it need two hands (a warrior who lost an arm cannot use it).</param>
/// <param name="AttackSeconds">The total duration of one attack cycle.</param>
public sealed record Weapon(
    string Name,
    WeaponClass Class,
    double Damage,
    bool TwoHanded,
    double AttackSeconds)
{
    /// <summary>
    /// Does this weapon offer a target the other man can grip?
    /// </summary>
    /// <remarks>
    /// It is off only for fists: there is no blade, no haft to catch. The class's own difficulty
    /// is in <see cref="CatchFactor"/>; this flag answers the question "is there anything to hold
    /// at all".
    /// </remarks>
    public bool Catchable { get; init; } = true;

    /// <summary>
    /// The skill of catching the enemy's weapon with this one. 0 = cannot catch.
    /// </summary>
    /// <remarks>
    /// This is the jitte's and sai's reason to exist. They fill the mechanical gap GDD §4 left when it
    /// said "no shields": a hand-carried shield was not common in Japanese warfare, but there was an
    /// implement that <b>stopped</b> the incoming sword. The sai's three prongs grip better than the
    /// jitte's single hook.
    /// </remarks>
    public double CatchSkill { get; init; }

    /// <summary>Can it catch?</summary>
    public bool CanCatch => CatchSkill > 0;

    /// <summary>
    /// This weapon's <b>catchability</b> — how well a blade that enters the hook is held.
    /// </summary>
    /// <remarks>
    /// A cutting weapon seats in the hook; a piercing tip slides; a blunt club has no sharp edge to
    /// grip. In the catch die this is read from the attacker's weapon — multiplied by the defender's
    /// <see cref="CatchSkill"/>.
    /// </remarks>
    public double CatchFactor => !Catchable ? 0 : Class switch
    {
        WeaponClass.Cutting => 1.0,
        WeaponClass.Piercing => 0.7,
        WeaponClass.Blunt => 0.25,
        _ => 1.0,
    };

    /// <summary>
    /// The strength of the poison on the blade. 0 = a clean weapon.
    /// </summary>
    /// <remarks>
    /// Poison is armour's <b>answer</b>: the dose that poisons the blood is not read off the plate, it
    /// enters with every strike that scratches skin and works independently of both armour's damage
    /// reduction and the defence stat. The weapon's own damage pays for it — a poisoned knife falls
    /// short in an open fight and collects its return over time.
    /// </remarks>
    public double Poison { get; init; }

    /// <summary>Is the weapon poisoned?</summary>
    public bool IsPoisoned => Poison > 0;

    /// <summary>
    /// The weapon's <b>tendency to leave the hand</b> — the share of the grip that breaks on a hard
    /// contact. 0 = it does not fall.
    /// </summary>
    /// <remarks>
    /// This is armour's second answer against cutting weapons: an edge that bites into plate twists and
    /// takes the weapon out of the palm; a piercing tip slides, a blunt club rebounds but stays in the
    /// hand. The class's own value is in <see cref="DisarmFactor"/>; this field lets individual weapons
    /// depart from their class — for now none of them does, because the rule was first measured on the
    /// class axis.
    /// </remarks>
    public double? DisarmFactorOverride { get; init; }

    /// <inheritdoc cref="DisarmFactorOverride"/>
    public double DisarmFactor => DisarmFactorOverride ?? (!Catchable ? 0 : Class switch
    {
        WeaponClass.Cutting => 1.0,
        WeaponClass.Piercing => 0.6,
        WeaponClass.Blunt => 0.2,
        _ => 1.0,
    });

    /// <summary>
    /// The weapon's <b>block quality</b> — how much of the blow a warrior in the stance meets.
    /// </summary>
    /// <remarks>
    /// This is the block's second axis: the decision to take the stance comes out of the warrior's
    /// Defence stat, how much the stance holds comes out of <b>the weapon in his hand</b>. A long haft
    /// held in two hands meets the blow with its shaft; a one-handed short edge only changes its
    /// direction. A fist meets nothing — a warrior who drops his weapon loses his block too.
    /// </remarks>
    public double? BlockFactorOverride { get; init; }

    /// <inheritdoc cref="BlockFactorOverride"/>
    public double BlockFactor => BlockFactorOverride ?? (TwoHanded ? 1.0 : Class switch
    {
        WeaponClass.Blunt => 0.85,
        WeaponClass.Cutting => 0.80,
        WeaponClass.Piercing => 0.70,
        _ => 0.80,
    });

    /// <summary>The multiplier applied to dismemberment risk.</summary>
    public double DismembermentFactor => Class switch
    {
        WeaponClass.Cutting => 1.0,
        WeaponClass.Piercing => 0.5,
        WeaponClass.Blunt => 0.15,
        _ => 1.0,
    };

    /// <summary>
    /// The multiplier applied to stun risk.
    /// </summary>
    /// <remarks>
    /// This is the blunt class's return. With a dismemberment multiplier of 0.15, the blunt weapon was
    /// deprived of everything that makes limb loss the game's signature mechanic and got nothing in
    /// return (docs/GDD.md §7 recorded this as a "difference from the code"). The trade is real now:
    /// the blade takes limbs, the blunt weapon lands the heavy blow that freezes the warrior.
    /// </remarks>
    public double StunFactor => Class switch
    {
        WeaponClass.Blunt => 1.0,
        WeaponClass.Cutting => 0.25,
        WeaponClass.Piercing => 0.15,
        _ => 0.25,
    };

    /// <summary>
    /// The distance a strike reaches (arena units). A warrior is 256 units tall.
    /// </summary>
    /// <remarks>
    /// A long weapon strikes from afar but is slow; a short weapon has to close. Without reach, weapons
    /// would differ only in damage and speed — this is the real difference between a naginata and a
    /// tantō.
    /// </remarks>
    public double Reach => Class switch
    {
        _ when TwoHanded => 150,
        WeaponClass.Piercing => 130,
        _ => 100,
    };

    public static Weapon Katana() => new("Katana", WeaponClass.Cutting, 22, false, 1.10);

    public static Weapon Nodachi() => new("Nodachi", WeaponClass.Cutting, 34, true, 1.60);

    public static Weapon Yari() => new("Yari", WeaponClass.Piercing, 25, true, 1.35);

    public static Weapon Tetsubo() => new("Tetsubo", WeaponClass.Blunt, 30, true, 1.55);

    /// <summary>
    /// A single-hooked holding implement: low damage, its return is stopping the incoming sword.
    /// </summary>
    /// <remarks>
    /// Next to the katana (22/1.10) it stands at 14/1.00 — that is the trade: catching has to pay
    /// for the damage lost. Whether it pays is measured (the <c>katana</c>/<c>jitte</c>
    /// scenarios).
    /// </remarks>
    public static Weapon Jitte() => new("Jitte", WeaponClass.Blunt, 14, false, 1.00)
    {
        CatchSkill = 1.0,
    };

    /// <summary>
    /// A three-pronged holding implement. It carries the same damage as the jitte, strikes slower, grips better.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is not sharp — that is why it sits in the blunt class; the sai is not a cutting implement but
    /// a holding and thrusting one.
    /// </para>
    /// <para>
    /// It started at 13/1.05 and was outright bad (61.92% victory, control 73.09%): the extra grip did
    /// not pay for the damage lost. At 14/1.05 all three sit within half a point (72.73%). The
    /// difference from the jitte is not damage but <b>volume</b>: the sai catches 3.71 times per fight,
    /// the jitte 2.75 — meaning the sai should have more work to do against a crowd. That encirclement
    /// measurement has not been made yet.
    /// </para>
    /// </remarks>
    public static Weapon Sai() => new("Sai", WeaponClass.Blunt, 14, false, 1.05)
    {
        CatchSkill = 1.25,
    };

    /// <summary>
    /// A short knife. The control for poison: the same knife, a clean blade.
    /// </summary>
    /// <remarks>
    /// The poison measurement can only be made with a single difference. The only difference from
    /// <see cref="PoisonedTanto"/> is the dose on the blade; damage, speed and class are the same.
    /// </remarks>
    public static Weapon Tanto() => new("Tantō", WeaponClass.Cutting, 13, false, 0.85);

    /// <summary>
    /// A short knife with a poisoned blade — the only weapon that stands up to armour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As steel it is almost harmless (7, against a clean tantō's 13): most of its output comes from
    /// the dose, and armour cannot read a dose. That is the trade — poison takes no limbs, does not
    /// stun and does not kill at once; in exchange it is paid out <b>over time</b>.
    /// </para>
    /// <para>
    /// At damage 13 the measurement gave the wrong answer: poison did not get through armour, it only
    /// rescued a weak knife (74.00% in an open fight, 55.99% against an armoured enemy — katana 73.09%
    /// / 68.62%). With the knife lowered to 7 and the dose raised, 60% of the output moved to poison
    /// and the claim held: 72.19% / <b>77.19%</b>. Wearing ō-yoroi against a poisoner is now a loss —
    /// plate does not stop the dose, and its weight delays the strike.
    /// </para>
    /// </remarks>
    public static Weapon PoisonedTanto() =>
        Tanto() with { Name = "Zehirli tantō", Damage = 7, Poison = 1.0 };

    /// <summary>The fallback for being unarmed or after losing a limb.</summary>
    public static Weapon Fists() => new("Yumruk", WeaponClass.Blunt, 8, false, 0.80)
    {
        Catchable = false,
        BlockFactorOverride = 0.30,
    };
}

/// <summary>
/// A thrown weapon — carried in a slot separate from the melee weapon.
/// </summary>
/// <remarks>
/// <para>
/// GDD §4's third proficiency line (one-handed / two-handed / <b>throwing</b>) finds its equivalent
/// here. It was kept dormant because the core had neither space nor projectiles (Open Decision
/// #4-C); space arrived, and projectiles came with it.
/// </para>
/// <para>
/// The ranged attack's real job is <b>turning distance into a threat</b>: a warrior is defenceless
/// both while closing and while fleeing. Without throwing, the far half of the arena was a safe
/// zone — nobody could touch a fleeing warrior.
/// </para>
/// </remarks>
/// <param name="Name">Display name.</param>
/// <param name="Damage">Base damage. Markedly lower than melee weapons.</param>
/// <param name="Range">Maximum throwing distance (arena units). A warrior is 256 units tall.</param>
/// <param name="Speed">The projectile's speed (units/second); flight time is computed from distance.</param>
/// <param name="Ammo">How many times it can be thrown in one fight. When it runs out only melee is left.</param>
/// <param name="ThrowSeconds">The total duration of one throwing cycle.</param>
/// <param name="Class">Wounding character — it sets the dismemberment risk.</param>
public sealed record ThrownWeapon(
    string Name,
    WeaponClass Class,
    double Damage,
    double Range,
    double Speed,
    int Ammo,
    double ThrowSeconds)
{
    /// <inheritdoc cref="Weapon.Poison"/>
    public double Poison { get; init; }

    /// <inheritdoc cref="Weapon.IsPoisoned"/>
    public bool IsPoisoned => Poison > 0;

    /// <inheritdoc cref="Weapon.DismembermentFactor"/>
    public double DismembermentFactor => Class switch
    {
        WeaponClass.Cutting => 1.0,
        WeaponClass.Piercing => 0.5,
        WeaponClass.Blunt => 0.15,
        _ => 1.0,
    };

    /// <inheritdoc cref="Weapon.StunFactor"/>
    public double StunFactor => Class switch
    {
        WeaponClass.Blunt => 1.0,
        WeaponClass.Cutting => 0.25,
        WeaponClass.Piercing => 0.15,
        _ => 0.25,
    };

    /// <summary>Fast, light, many of them.</summary>
    public static ThrownWeapon Shuriken() =>
        new("Shuriken", WeaponClass.Cutting, 12, 700, 1400, 4, 0.7);

    /// <summary>
    /// A shuriken with a poisoned point: few of them, the same damage, it leaves a dose behind.
    /// </summary>
    /// <remarks>
    /// Poison's ranged form. A projectile <b>cannot be caught</b> (see <see cref="Weapon.CatchSkill"/>),
    /// so the poisoned point is the catching implement's answer too; in exchange the ammo halves.
    /// </remarks>
    public static ThrownWeapon PoisonedShuriken() =>
        Shuriken() with { Name = "Zehirli shuriken", Ammo = 2, Poison = 1.0 };

    /// <summary>Slow and heavy; few of them but serious damage.</summary>
    public static ThrownWeapon ThrowingSpear() =>
        new("Throwing spear", WeaponClass.Piercing, 26, 520, 900, 2, 1.1);
}

/// <summary>
/// Armour wear slot by slot — the total damage each piece of a kit has absorbed.
/// </summary>
/// <remarks>
/// A value type instead of a dictionary: the fight summary is produced per warrior and batch
/// simulation does this tens of thousands of times; allocating a six-element dictionary every time
/// would slow the measurement itself down.
/// </remarks>
public readonly record struct ArmorWearSet(
    double Head = 0,
    double Torso = 0,
    double SwordArm = 0,
    double OffArm = 0,
    double RightLeg = 0,
    double LeftLeg = 0)
{
    public double At(HitLocation location) => location switch
    {
        HitLocation.Head => Head,
        HitLocation.Torso => Torso,
        HitLocation.SwordArm => SwordArm,
        HitLocation.OffArm => OffArm,
        HitLocation.RightLeg => RightLeg,
        HitLocation.LeftLeg => LeftLeg,
        _ => 0,
    };

    /// <summary>Adds wear to the given slot.</summary>
    public ArmorWearSet With(HitLocation location, double amount) => location switch
    {
        HitLocation.Head => this with { Head = amount },
        HitLocation.Torso => this with { Torso = amount },
        HitLocation.SwordArm => this with { SwordArm = amount },
        HitLocation.OffArm => this with { OffArm = amount },
        HitLocation.RightLeg => this with { RightLeg = amount },
        HitLocation.LeftLeg => this with { LeftLeg = amount },
        _ => this,
    };

    /// <summary>The sum of all slots.</summary>
    public double Total => Head + Torso + SwordArm + OffArm + RightLeg + LeftLeg;
}

/// <summary>An armour piece covering a single region.</summary>
/// <param name="Name">Display name.</param>
/// <param name="DamageReduction">The flat damage subtracted on a hit landing on that region.</param>
/// <param name="DismembermentResistance">
/// The share by which the dismemberment risk of a heavy blow to that region is reduced
/// (0 = unprotected, 1 = fully immune).
/// </param>
/// <param name="Weight">
/// The piece's weight. The kit's total stretches the attack cycle
/// (see <c>CombatTuning.ArmorWeightAtFullPenalty</c>).
/// </param>
/// <param name="Durability">
/// <b>How much of a blow the piece can absorb</b>. The damage it absorbs comes off this pool; when the
/// pool runs out the piece breaks and is <b>gone permanently</b>.
/// </param>
/// <remarks>
/// <para>
/// Durability makes armour a <b>consumable</b>. Weight wrote armour's price on the field; durability
/// writes its price in the dojo: the best kit is the one that absorbs the most, and the one that
/// absorbs the most is the one that runs out the fastest. The difference from the weapon is
/// deliberate — a dropped weapon comes back at the end of the fight, a broken armour piece does not.
/// </para>
/// <para>
/// The pool is reduced by the <b>absorbed</b> damage, not by the incoming damage: what wears a piece is
/// the blow it stops. Reduced by incoming damage, thick plate would run out as fast as thin cloth and
/// the difference between the tiers would exist only on paper.
/// </para>
/// </remarks>
/// <remarks>
/// Weight is what makes armour a <b>decision</b>. While it was free, ō-yoroi was superior on every
/// axis — victory from 68% to 96%, death from 41.6% to 16.3%, limb loss from 8.6% to 0.4%, and nothing
/// paid in return; the only brake was price, and that does not exist until the economy numbers arrive.
/// Weight carries the price onto the field: a heavily armoured warrior closes slowly and his sword
/// lands late.
/// </remarks>
public sealed record ArmorPiece(
    string Name,
    double DamageReduction,
    double DismembermentResistance,
    double Weight,
    double Durability = 0)
{
    /// <summary>Is there really a piece in this slot?</summary>
    public bool IsWorn => DamageReduction > 0 || DismembermentResistance > 0;

    /// <summary>An uncovered region.</summary>
    public static ArmorPiece Bare { get; } = new("Bare", 0, 0, 0);

    public static ArmorPiece Keikogi { get; } = new("Keikogi", 4, 0.20, 1, Durability: 40);

    public static ArmorPiece DoMaru { get; } = new("Dō-maru cuirass", 9, 0.45, 4, Durability: 110);

    public static ArmorPiece OYoroiCuirass { get; } =
        new("Ō-yoroi cuirass", 14, 0.65, 7, Durability: 180);

    /// <summary>Arm armour — it covers <b>one</b> arm; two arms need two pieces.</summary>
    public static ArmorPiece Kote { get; } = new("Kote", 4, 0.30, 0.75, Durability: 45);

    /// <inheritdoc cref="Kote"/>
    public static ArmorPiece HeavyKote { get; } = new("Heavy kote", 6, 0.45, 1.5, Durability: 75);

    /// <summary>Shin armour — it covers <b>one</b> leg.</summary>
    public static ArmorPiece Suneate { get; } = new("Suneate", 4, 0.25, 0.75, Durability: 45);

    /// <inheritdoc cref="Suneate"/>
    public static ArmorPiece HeavySuneate { get; } = new("Heavy suneate", 6, 0.40, 1.5, Durability: 75);

    public static ArmorPiece Kabuto { get; } = new("Kabuto", 8, 0.55, 3, Durability: 90);
}

/// <summary>
/// A warrior's kit — a separate piece for every region.
/// </summary>
/// <remarks>
/// <para>
/// Armour is not a single scalar but <b>slot by slot</b>: damage reduction and dismemberment resistance
/// are read from the piece of the region the blow landed on (<see cref="At"/>).
/// </para>
/// <para>
/// The reason: hit regions (see <c>CombatTuning</c>) exist for exactly this. As long as resistance is a
/// single number, "good armour" advances on a single axis and equipment's really interesting decision —
/// <b>a heavy cuirass, bare arms</b>: cheap and fast, but with a high chance of coming home without an
/// arm — never exists at all.
/// </para>
/// <para>
/// The slots are limb by limb: sword arm, off arm, right leg and left leg are equipped separately. A
/// single "arm" slot both counted two arm pieces as one and could not represent the remaining arm of a
/// warrior who had lost one.
/// </para>
/// </remarks>
/// <param name="Name">The kit's display name.</param>
public sealed record Armor(
    string Name,
    ArmorPiece Head,
    ArmorPiece Torso,
    ArmorPiece SwordArm,
    ArmorPiece OffArm,
    ArmorPiece RightLeg,
    ArmorPiece LeftLeg)
{
    /// <summary>The kit's total weight. An empty slot carries no weight.</summary>
    public double Weight =>
        Head.Weight + Torso.Weight + SwordArm.Weight + OffArm.Weight
        + RightLeg.Weight + LeftLeg.Weight;

    /// <summary>The piece covering the given region.</summary>
    public ArmorPiece At(HitLocation location) => location switch
    {
        HitLocation.Head => Head,
        HitLocation.Torso => Torso,
        HitLocation.SwordArm => SwordArm,
        HitLocation.OffArm => OffArm,
        HitLocation.RightLeg => RightLeg,
        HitLocation.LeftLeg => LeftLeg,
        _ => ArmorPiece.Bare,
    };

    /// <summary>The same kit with another piece fitted to the given slot.</summary>
    public Armor With(HitLocation location, ArmorPiece piece) => location switch
    {
        HitLocation.Head => this with { Head = piece },
        HitLocation.Torso => this with { Torso = piece },
        HitLocation.SwordArm => this with { SwordArm = piece },
        HitLocation.OffArm => this with { OffArm = piece },
        HitLocation.RightLeg => this with { RightLeg = piece },
        HitLocation.LeftLeg => this with { LeftLeg = piece },
        _ => this,
    };

    /// <summary>A kit covering every region with the same piece.</summary>
    public static Armor Uniform(string name, ArmorPiece piece) =>
        new(name, piece, piece, piece, piece, piece, piece);

    public static Armor None() => Uniform("None", ArmorPiece.Bare);

    /// <summary>Cloth covering the torso only. Arms, legs and head are exposed.</summary>
    public static Armor Light() => new(
        "Hafif keikogi",
        Head: ArmorPiece.Bare,
        Torso: ArmorPiece.Keikogi,
        SwordArm: ArmorPiece.Bare,
        OffArm: ArmorPiece.Bare,
        RightLeg: ArmorPiece.Bare,
        LeftLeg: ArmorPiece.Bare);

    /// <summary>Torso, both arms and both legs covered; head exposed.</summary>
    public static Armor Medium() => new(
        "Dō-maru",
        Head: ArmorPiece.Bare,
        Torso: ArmorPiece.DoMaru,
        SwordArm: ArmorPiece.Kote,
        OffArm: ArmorPiece.Kote,
        RightLeg: ArmorPiece.Suneate,
        LeftLeg: ArmorPiece.Suneate);

    /// <summary>The full kit.</summary>
    public static Armor Heavy() => new(
        "Ō-yoroi",
        Head: ArmorPiece.Kabuto,
        Torso: ArmorPiece.OYoroiCuirass,
        SwordArm: ArmorPiece.HeavyKote,
        OffArm: ArmorPiece.HeavyKote,
        RightLeg: ArmorPiece.HeavySuneate,
        LeftLeg: ArmorPiece.HeavySuneate);
}
