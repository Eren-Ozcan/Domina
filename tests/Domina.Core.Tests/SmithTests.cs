using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The equipment branch's two upper tiers (docs/GDD.md §10): the ō-yoroi gate and the sword forge.
/// The decisions protected here: full plate needs the plate works <b>and</b> a smith in it, a weapon
/// is reforged rather than replaced, and the mastery built on the old blade does not come with the
/// new one.
/// </summary>
public class SmithTests
{
    private static DojoState Dojo(bool plate = false, bool forge = false, bool smith = false)
    {
        DojoState state = new(
            events: new EventTuning { ChancePerDay = 0 },
            school: new SchoolTuning { BuildDaysFactor = 0 })
        {
            Resources = new Resources(Gold: 6000, Food: 200, Water: 200),
        };

        state.BuySchoolNode(SchoolNodeId.Forge);

        if (plate || forge)
        {
            state.BuySchoolNode(SchoolNodeId.PlateWorks);
        }

        if (forge)
        {
            state.BuySchoolNode(SchoolNodeId.SwordForge);
        }

        if (smith)
        {
            state.Hire(StaffRole.Smith);
        }

        return state;
    }

    /// <summary>A dojo with a forge and no plate works cannot put a man in full plate.</summary>
    [Fact]
    public void FullPlateNeedsThePlateWorksAndTheSmith()
    {
        DojoState bare = Dojo(smith: true);
        RosterEntry entry = bare.Roster.Recruit("Kenji");

        Assert.False(bare.Quartermaster.Equip(bare, entry.Warrior, HitLocation.Torso, ArmorPiece.OYoroiCuirass));

        DojoState built = Dojo(plate: true);
        RosterEntry second = built.Roster.Recruit("Kenji");

        // The building without the man forges nothing either.
        Assert.False(built.Quartermaster.Equip(built, second.Warrior, HitLocation.Torso, ArmorPiece.OYoroiCuirass));

        DojoState whole = Dojo(plate: true, smith: true);
        RosterEntry third = whole.Roster.Recruit("Kenji");

        Assert.True(whole.Quartermaster.Equip(whole, third.Warrior, HitLocation.Torso, ArmorPiece.OYoroiCuirass));
        Assert.Equal(ArmorPiece.OYoroiCuirass, third.Warrior.Armor.At(HitLocation.Torso));
    }

    /// <summary>The lighter kit is not gated — it was never the branch's business.</summary>
    [Fact]
    public void TheLighterKitNeedsNobody()
    {
        DojoState dojo = Dojo();
        RosterEntry entry = dojo.Roster.Recruit("Kenji");

        Assert.True(dojo.Quartermaster.Equip(dojo, entry.Warrior, HitLocation.Torso, ArmorPiece.DoMaru));
    }

    /// <summary>A weapon is made better, not swapped — and it takes the mastery with it.</summary>
    [Fact]
    public void ReforgingKeepsTheWeaponAndCostsTheMastery()
    {
        DojoState dojo = Dojo(forge: true, smith: true);
        RosterEntry entry = dojo.Roster.Recruit("Kenji");
        entry.Warrior.GainMastery(0.6);

        double before = entry.Warrior.Weapon.Damage;

        Assert.True(dojo.Quartermaster.Forge(dojo, entry.Warrior));

        Assert.True(entry.Warrior.Weapon.Damage > before);
        Assert.Equal(WeaponClass.Cutting, entry.Warrior.Weapon.Class);
        Assert.Equal(0, entry.Warrior.WeaponSkill);
        Assert.Equal(0.6, entry.Warrior.Mastery.Of("Katana"), 6);

        // And it is done once: a forged blade is not reforged into a better forged blade.
        Assert.False(dojo.Quartermaster.Forge(dojo, entry.Warrior));
    }

    /// <summary>Without the third tier, or without the smith, nothing is forged.</summary>
    [Fact]
    public void TheForgeNeedsItsBuildingAndItsMan()
    {
        DojoState noTier = Dojo(plate: true, smith: true);
        RosterEntry first = noTier.Roster.Recruit("Kenji");

        Assert.False(noTier.Quartermaster.Forge(noTier, first.Warrior));

        DojoState noSmith = Dojo(forge: true);
        RosterEntry second = noSmith.Roster.Recruit("Kenji");

        Assert.False(noSmith.Quartermaster.Forge(noSmith, second.Warrior));
    }

    /// <summary>The bill is read off the weapon, so the heaviest thing in the dojo costs most.</summary>
    [Fact]
    public void TheBillFollowsTheWeapon()
    {
        DojoState dojo = Dojo(forge: true, smith: true);
        RosterEntry knife = dojo.Roster.Recruit("Kenji", weapon: Weapon.Tanto());
        RosterEntry club = dojo.Roster.Recruit("Goro", weapon: Weapon.Tetsubo());

        Assert.True(
            dojo.Quartermaster.ForgePrice(club.Warrior) > dojo.Quartermaster.ForgePrice(knife.Warrior));
    }
}
