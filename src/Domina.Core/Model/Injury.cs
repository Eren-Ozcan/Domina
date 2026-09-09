using System.Numerics;

namespace Domina.Core.Model;

/// <summary>The limbs that can be lost permanently.</summary>
/// <remarks>
/// The limbs stand <b>one by one</b>: right arm, left arm, right leg, left leg. A single "arm" record
/// represented both the armour and the loss twice over — yet when one arm goes the other is still
/// there, and if armour is slot by slot (§7) arm pieces must be slot by slot too.
/// </remarks>
public enum BodyPart
{
    /// <summary>The sword arm — the loss that most affects strength and the use of two-handed weapons.</summary>
    SwordArm,

    /// <summary>The off arm. Losing it still ends two-handed weapons, and lowers strength a little.</summary>
    OffArm,

    /// <summary>The right leg — mobility.</summary>
    RightLeg,

    /// <summary>Sol bacak — hareket kabiliyeti.</summary>
    LeftLeg,

    /// <summary>Depth perception — it affects accuracy.</summary>
    Eye,
}

/// <summary>
/// The set of limbs lost.
/// </summary>
/// <remarks>
/// A <b>set</b>, not a list: the same limb cannot be lost twice, and the type should carry that rule.
/// A set also has value equality — required because the fight summaries are records: with a list, two
/// identical runs would count as different because of a reference difference and the determinism tests
/// would become meaningless. If the <b>order</b> of the losses is needed,
/// <c>WarriorDismembered</c> in the event stream is already ordered.
/// </remarks>
[Flags]
public enum BodyPartSet
{
    None = 0,
    SwordArm = 1 << 0,
    OffArm = 1 << 1,
    RightLeg = 1 << 2,
    LeftLeg = 1 << 3,
    Eye = 1 << 4,
}

public static class BodyPartSetExtensions
{
    public static BodyPartSet AsFlag(this BodyPart part) => part switch
    {
        BodyPart.SwordArm => BodyPartSet.SwordArm,
        BodyPart.OffArm => BodyPartSet.OffArm,
        BodyPart.RightLeg => BodyPartSet.RightLeg,
        BodyPart.LeftLeg => BodyPartSet.LeftLeg,
        BodyPart.Eye => BodyPartSet.Eye,
        _ => BodyPartSet.None,
    };

    /// <summary>Uzuv bir kol mu?</summary>
    public static bool IsArm(this BodyPart part) =>
        part is BodyPart.SwordArm or BodyPart.OffArm;

    /// <summary>Is the limb a leg?</summary>
    public static bool IsLeg(this BodyPart part) =>
        part is BodyPart.RightLeg or BodyPart.LeftLeg;

    public static bool Has(this BodyPartSet set, BodyPart part) => (set & part.AsFlag()) != 0;

    /// <summary>The limbs in the set, in <see cref="BodyPart"/> order.</summary>
    public static IEnumerable<BodyPart> Parts(this BodyPartSet set)
    {
        foreach (BodyPart part in Enum.GetValues<BodyPart>())
        {
            if (set.Has(part))
            {
                yield return part;
            }
        }
    }
}

/// <summary>The set of armour slots — it carries which pieces broke.</summary>
/// <remarks>
/// The counterpart of <see cref="BodyPartSet"/> but separate: a lost limb and a broken armour piece
/// are not the same thing (a cuirass can break, a torso does not come off) and the two are counted separately.
/// </remarks>
public enum HitLocationSet
{
    None = 0,
    Head = 1 << 0,
    Torso = 1 << 1,
    SwordArm = 1 << 2,
    OffArm = 1 << 3,
    RightLeg = 1 << 4,
    LeftLeg = 1 << 5,
}

public static class HitLocationSetExtensions
{
    public static HitLocationSet AsFlag(this HitLocation location) => location switch
    {
        HitLocation.Head => HitLocationSet.Head,
        HitLocation.Torso => HitLocationSet.Torso,
        HitLocation.SwordArm => HitLocationSet.SwordArm,
        HitLocation.OffArm => HitLocationSet.OffArm,
        HitLocation.RightLeg => HitLocationSet.RightLeg,
        HitLocation.LeftLeg => HitLocationSet.LeftLeg,
        _ => HitLocationSet.None,
    };

    public static bool Has(this HitLocationSet set, HitLocation location) =>
        (set & location.AsFlag()) != 0;

    /// <summary>The slots in the set, in <see cref="HitLocation"/> order.</summary>
    public static IEnumerable<HitLocation> Slots(this HitLocationSet set)
    {
        foreach (HitLocation location in Enum.GetValues<HitLocation>())
        {
            if (set.Has(location))
            {
                yield return location;
            }
        }
    }

    /// <summary>The number of slots in the set.</summary>
    /// <remarks>
    /// It is done by bit counting, not through <see cref="Slots"/>: target selection reads this once per
    /// decision step, and an iterator with <c>Enum.GetValues</c> meant kilobytes of allocation per fight
    /// (<c>ThroughputTests</c> caught it).
    /// </remarks>
    public static int Count(this HitLocationSet set) => BitOperations.PopCount((uint)set);
}

/// <summary>The region a blow lands on.</summary>
/// <remarks>
/// <para>
/// It is separate from <see cref="BodyPart"/>: not every region is a limb that can be lost. A heavy
/// blow to the torso can kill the warrior but has nothing to take off.
/// </para>
/// <para>
/// The regions are not equally likely (see <c>CombatTuning</c>). Were they equal, torso armour would
/// become worthless and armour investment would turn into the flat optimisation "spread it evenly".
/// </para>
/// </remarks>
public enum HitLocation
{
    Head,
    Torso,
    SwordArm,
    OffArm,
    RightLeg,
    LeftLeg,
}

/// <summary>
/// A warrior's permanent disability. It remains after coming back from death and cannot be undone.
/// </summary>
/// <remarks>
/// In Domina, dismemberment was only the visual effect of the moment of death; here it is the mechanic
/// of <b>surviving and living on maimed</b> (see docs/GDD.md §7).
/// </remarks>
public sealed record Disability(BodyPart Part)
{
    /// <summary>
    /// The multiplier applied to attack strength.
    /// </summary>
    /// <remarks>
    /// The sword arm and the off arm are not the same thing: the first is the strike itself, the second
    /// is balance. Both end two-handed weapons (see
    /// <see cref="BlocksTwoHandedWeapons"/>), but for a warrior fighting one-handed the loss of the off
    /// arm is a bearable one.
    /// </remarks>
    public double StrengthMultiplier => Part switch
    {
        BodyPart.SwordArm => 0.65,
        BodyPart.OffArm => 0.85,
        _ => 1.0,
    };

    /// <summary>The multiplier applied to evasion.</summary>
    public double EvasionMultiplier => Part.IsLeg() ? 0.55 : 1.0;

    /// <summary>
    /// The multiplier applied to walking speed.
    /// </summary>
    /// <remarks>
    /// A warrior who loses a leg loses not only evasion but <b>the ability to flee</b>: a man who limps
    /// cannot get away from a chaser. This is limb loss's heaviest secondary price.
    /// </remarks>
    public double SpeedMultiplier => Part.IsLeg() ? 0.60 : 1.0;

    /// <summary>The multiplier applied to hit chance.</summary>
    public double AccuracyMultiplier => Part == BodyPart.Eye ? 0.75 : 1.0;

    /// <summary>Whichever arm goes, a two-handed weapon cannot be used.</summary>
    public bool BlocksTwoHandedWeapons => Part.IsArm();
}
