using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// What a man is drawn wearing and carrying.
/// </summary>
/// <remarks>
/// The rule under test is that the figure cannot lie about him: the weapon drawn is the weapon the
/// core says he can use, the plate drawn is the plate he paid for, and a man with nothing on is drawn
/// with nothing on. The shapes themselves are a drawing decision and are not asserted.
/// </remarks>
public class WarriorKitTests
{
    private static Warrior Man(Weapon? weapon = null, Armor? armour = null) =>
        new(new WarriorId(7), "Takeda", WarriorStats.Recruit(), weapon, armour);

    [Fact]
    public void ABareManWithASwordIsWhatTheRigDrewBeforeKitsExisted()
    {
        WarriorKit kit = WarriorKit.Of(Man());

        Assert.Equal(WarriorKit.Default, kit);
        Assert.False(kit.AnyArmour);
    }

    [Theory]
    [InlineData("Katana", WeaponShape.Blade)]
    [InlineData("Nodachi", WeaponShape.LongBlade)]
    [InlineData("Yari", WeaponShape.Spear)]
    [InlineData("Tetsubo", WeaponShape.Club)]
    [InlineData("Jitte", WeaponShape.Hook)]
    [InlineData("Sai", WeaponShape.Hook)]
    [InlineData("Tanto", WeaponShape.ShortBlade)]
    [InlineData("Fists", WeaponShape.Unarmed)]
    public void EveryWeaponTheCatalogueSellsHasItsOwnSilhouette(string named, WeaponShape shape)
    {
        Weapon weapon = named switch
        {
            "Katana" => Weapon.Katana(),
            "Nodachi" => Weapon.Nodachi(),
            "Yari" => Weapon.Yari(),
            "Tetsubo" => Weapon.Tetsubo(),
            "Jitte" => Weapon.Jitte(),
            "Sai" => Weapon.Sai(),
            "Tanto" => Weapon.Tanto(),
            _ => Weapon.Fists(),
        };

        Assert.Equal(shape, WarriorKit.ShapeOf(weapon));
    }

    /// <summary>A smith's copy is the same weapon with a better edge, so it keeps its silhouette.</summary>
    [Fact]
    public void AForgedWeaponIsDrawnAsTheWeaponItWasForgedFrom()
    {
        Assert.Equal(
            WarriorKit.ShapeOf(Weapon.Yari()),
            WarriorKit.ShapeOf(Weapon.Forged(Weapon.Yari())));
    }

    /// <summary>
    /// The core takes a two-handed weapon off a man who lost an arm; the figure has to agree, or he is
    /// drawn swinging a nodachi with the arm that is on the ground behind him.
    /// </summary>
    [Fact]
    public void AManWhoCannotHoldHisWeaponIsDrawnWithEmptyHands()
    {
        Warrior man = Man(Weapon.Nodachi());
        man.AddDisability(BodyPart.SwordArm);

        Assert.Equal(WeaponShape.Unarmed, WarriorKit.Of(man).Weapon);
    }

    [Fact]
    public void ClothPlateAndTheSmithsPlateAreThreeDifferentDrawings()
    {
        WarriorKit cloth = WarriorKit.Of(Man(armour: Armor.Light()));
        WarriorKit plate = WarriorKit.Of(Man(armour: Armor.Medium()));
        WarriorKit smith = WarriorKit.Of(Man(armour: Armor.Heavy()));

        Assert.Equal(PlateWeight.Cloth, cloth.Torso);
        Assert.Equal(PlateWeight.Plate, plate.Torso);
        Assert.Equal(PlateWeight.Heavy, smith.Torso);
    }

    /// <summary>
    /// The kit is read slot by slot for the reason the armour is: a heavy cuirass over bare arms is
    /// the decision the equipment branch exists for, and the figure has to show both halves of it.
    /// </summary>
    [Fact]
    public void ACuirassOverBareArmsIsDrawnAsACuirassOverBareArms()
    {
        Armor half = Armor.None()
            .With(HitLocation.Torso, ArmorPiece.DoMaru);

        WarriorKit kit = WarriorKit.Of(Man(armour: half));

        Assert.Equal(PlateWeight.Plate, kit.Torso);
        Assert.Equal(PlateWeight.Bare, kit.SwordArm);
        Assert.Equal(PlateWeight.Bare, kit.OffArm);
        Assert.True(kit.AnyArmour);
    }

    [Fact]
    public void TheLeftAndRightOfAManAreNotTheSameSlot()
    {
        Armor odd = Armor.None()
            .With(HitLocation.RightLeg, ArmorPiece.Suneate);

        WarriorKit kit = WarriorKit.Of(Man(armour: odd));

        Assert.Equal(PlateWeight.Cloth, kit.RightLeg);
        Assert.Equal(PlateWeight.Bare, kit.LeftLeg);
    }
}
