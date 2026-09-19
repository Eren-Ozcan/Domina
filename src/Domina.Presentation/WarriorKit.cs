using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>The silhouette of the thing in a man's hand.</summary>
/// <remarks>
/// It is a <b>shape</b> and not a weapon: the drawing side needs to know how long the thing is and
/// what its head looks like, not what it does to a torso. Two weapons that fight differently may share
/// a shape (the jitte and the sai are both a short hooked bar), and one that fights the same may not.
/// <see cref="WeaponShape.Blade"/> is first on purpose, so a rig built with no kit at all draws the
/// plain sword it drew before kits existed.
/// </remarks>
public enum WeaponShape
{
    /// <summary>A one-handed sword — the katana.</summary>
    Blade,

    /// <summary>A knife — the tantō.</summary>
    ShortBlade,

    /// <summary>A sword too long for one hand — the nodachi.</summary>
    LongBlade,

    /// <summary>A haft with a head on it — the yari.</summary>
    Spear,

    /// <summary>A haft with a weight on it — the tetsubo.</summary>
    Club,

    /// <summary>A short bar with a hook off the guard — the jitte and the sai.</summary>
    Hook,

    /// <summary>Nothing in the hand.</summary>
    Unarmed,
}

/// <summary>How much is worn on one region of the body.</summary>
/// <remarks>
/// Three steps rather than the piece itself: the drawing has to be readable at the size of a list
/// chip, where the difference between a kote and a heavy kote is a pixel. What must read across a
/// yard is bare, cloth, plate — and the fact that the plate is the smith's.
/// </remarks>
public enum PlateWeight
{
    /// <summary>Nothing on this region.</summary>
    Bare,

    /// <summary>Cloth — the keikogi.</summary>
    Cloth,

    /// <summary>Lacquered plate a stall sells.</summary>
    Plate,

    /// <summary>The plate only a smith of one's own can fit.</summary>
    Heavy,
}

/// <summary>
/// What a man is wearing and carrying, as the drawing needs it.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of <see cref="WarriorLook"/>: the look is the man and never changes, the kit is
/// what he happens to have on today and changes the morning the player buys him a cuirass. Both are
/// read by the rig before it hangs anything on a bone, and neither touches the part list — a plate is
/// a drawing on the torso bone, not a bone of its own (docs/ROADMAP.md, phase 2).
/// </para>
/// <para>
/// <b>Every field's zero is the old bare figure</b>, so a screen that knows nothing of gear — a
/// candidate at the stall, a name at the gate — passes nothing and gets what it always got.
/// </para>
/// </remarks>
/// <param name="Weapon">The shape in his hand.</param>
/// <param name="TwoHanded">Does he hold it with both hands? The off arm is drawn onto the haft.</param>
/// <param name="Head">What is on his head.</param>
/// <param name="Torso">What is on his body.</param>
/// <param name="SwordArm">What is on the arm that carries the weapon.</param>
/// <param name="OffArm">What is on the other arm.</param>
/// <param name="RightLeg">What is on the near leg.</param>
/// <param name="LeftLeg">What is on the far leg.</param>
public readonly record struct WarriorKit(
    WeaponShape Weapon,
    bool TwoHanded,
    PlateWeight Head,
    PlateWeight Torso,
    PlateWeight SwordArm,
    PlateWeight OffArm,
    PlateWeight RightLeg,
    PlateWeight LeftLeg)
{
    /// <summary>A bare man with a plain sword — what the rig drew before it read a kit.</summary>
    public static WarriorKit Default => default;

    /// <summary>Is anything worn at all? A bare man is drawn with no plates over him.</summary>
    public bool AnyArmour =>
        Head != PlateWeight.Bare || Torso != PlateWeight.Bare
        || SwordArm != PlateWeight.Bare || OffArm != PlateWeight.Bare
        || RightLeg != PlateWeight.Bare || LeftLeg != PlateWeight.Bare;

    /// <summary>What this man is wearing and carrying.</summary>
    /// <remarks>
    /// The weapon read is <see cref="Warrior.UsableWeapon"/> and not the one in his record: a man who
    /// has lost the arm for a two-handed weapon fights with his fists, and the figure must show the
    /// empty hands the core already gave him.
    /// </remarks>
    public static WarriorKit Of(Warrior warrior)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        Weapon weapon = warrior.UsableWeapon;
        Armor armour = warrior.Armor;

        return new WarriorKit(
            Weapon: ShapeOf(weapon),
            TwoHanded: weapon.TwoHanded,
            Head: WeightOf(armour.Head),
            Torso: WeightOf(armour.Torso),
            SwordArm: WeightOf(armour.SwordArm),
            OffArm: WeightOf(armour.OffArm),
            RightLeg: WeightOf(armour.RightLeg),
            LeftLeg: WeightOf(armour.LeftLeg));
    }

    /// <summary>
    /// The shape of one weapon.
    /// </summary>
    /// <remarks>
    /// The name is asked first and the class second. The name is what tells a yari from a tetsubo —
    /// both are two-handed and neither is cutting — and the class is what catches a weapon the
    /// catalogue grows later, or a forged copy whose name carries a smith's prefix.
    /// </remarks>
    public static WeaponShape ShapeOf(Weapon? weapon)
    {
        if (weapon is null)
        {
            return WeaponShape.Unarmed;
        }

        string name = weapon.Name;

        if (Names(name, "Fists"))
        {
            return WeaponShape.Unarmed;
        }

        if (Names(name, "Nodachi"))
        {
            return WeaponShape.LongBlade;
        }

        if (Names(name, "Yari"))
        {
            return WeaponShape.Spear;
        }

        if (Names(name, "Tetsubo"))
        {
            return WeaponShape.Club;
        }

        if (Names(name, "Jitte") || Names(name, "Sai"))
        {
            return WeaponShape.Hook;
        }

        if (Names(name, "Tantō") || Names(name, "Tanto"))
        {
            return WeaponShape.ShortBlade;
        }

        if (Names(name, "Katana"))
        {
            return WeaponShape.Blade;
        }

        // Nothing the catalogue lists by name. The class and the grip are what is left, and they are
        // enough to keep a new weapon from being drawn as a katana by default.
        return weapon.Class switch
        {
            WeaponClass.Piercing => WeaponShape.Spear,
            WeaponClass.Blunt => weapon.CanCatch ? WeaponShape.Hook : WeaponShape.Club,
            _ => weapon.TwoHanded ? WeaponShape.LongBlade : WeaponShape.Blade,
        };
    }

    /// <summary>Which of the three steps a piece is drawn at.</summary>
    /// <remarks>
    /// The smith's gate decides the heaviest step rather than the reduction number: it is the
    /// distinction the player paid the plate works for, and it must be the one he can see.
    /// </remarks>
    private static PlateWeight WeightOf(ArmorPiece? piece)
    {
        if (piece is null || !piece.IsWorn)
        {
            return PlateWeight.Bare;
        }

        if (piece.NeedsSmith)
        {
            return PlateWeight.Heavy;
        }

        return piece.DamageReduction >= 7 ? PlateWeight.Plate : PlateWeight.Cloth;
    }

    /// <summary>The name a weapon carries, whatever a forge wrote in front of it.</summary>
    private static bool Names(string name, string what) =>
        name.Contains(what, StringComparison.OrdinalIgnoreCase);
}
