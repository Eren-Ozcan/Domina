using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The limbs stand one by one: the sword arm, the off arm, the two legs. These tests tie down the
/// distinction's two promises — armour is equipped per limb, and which arm you lose matters
/// ifade eder.
/// </summary>
public class PerLimbTests
{
    /// <summary>Losing the sword arm is heavier than losing the off arm.</summary>
    [Fact]
    public void LosingTheSwordArmCostsMoreThanLosingTheOther()
    {
        Warrior sword = TestBuilders.Warrior(1, strength: 100);
        sword.AddDisability(BodyPart.SwordArm);

        Warrior off = TestBuilders.Warrior(2, strength: 100);
        off.AddDisability(BodyPart.OffArm);

        Assert.True(sword.EffectiveStats.Strength < off.EffectiveStats.Strength);
        Assert.True(off.EffectiveStats.Strength < TestBuilders.Warrior(3, strength: 100).BaseStats.Strength);
    }

    /// <summary>Hangi kol giderse gitsin iki elli silah biter.</summary>
    [Theory]
    [InlineData(BodyPart.SwordArm)]
    [InlineData(BodyPart.OffArm)]
    public void EitherArmEndsTwoHandedWeapons(BodyPart arm)
    {
        Warrior warrior = TestBuilders.Warrior(1, weapon: Weapon.Nodachi());
        warrior.AddDisability(arm);

        Assert.Equal(Weapon.Fists(), warrior.UsableWeapon);
    }

    /// <summary>Losing both legs is heavier than losing one — the multipliers combine.</summary>
    [Fact]
    public void LosingBothLegsCompounds()
    {
        Warrior one = TestBuilders.Warrior(1, speed: 100);
        one.AddDisability(BodyPart.RightLeg);

        Warrior both = TestBuilders.Warrior(2, speed: 100);
        both.AddDisability(BodyPart.RightLeg);
        both.AddDisability(BodyPart.LeftLeg);

        Assert.True(both.EffectiveStats.Speed < one.EffectiveStats.Speed);
    }

    /// <summary>
    /// Armour is equipped per limb: covering one arm with a kote does not cover the other.
    /// Half of the "heavy cuirass, bare arms" decision becomes possible this way too (§7).
    /// </summary>
    [Fact]
    public void EachLimbCarriesItsOwnPiece()
    {
        var mixed = new Armor(
            "Half kit",
            Head: ArmorPiece.Bare,
            Torso: ArmorPiece.OYoroiCuirass,
            SwordArm: ArmorPiece.HeavyKote,
            OffArm: ArmorPiece.Bare,
            RightLeg: ArmorPiece.Suneate,
            LeftLeg: ArmorPiece.Bare);

        Assert.Equal(ArmorPiece.HeavyKote, mixed.At(HitLocation.SwordArm));
        Assert.Equal(ArmorPiece.Bare, mixed.At(HitLocation.OffArm));
        Assert.Equal(ArmorPiece.Suneate, mixed.At(HitLocation.RightLeg));
        Assert.Equal(ArmorPiece.Bare, mixed.At(HitLocation.LeftLeg));
        Assert.True(mixed.Weight < Armor.Heavy().Weight);
    }

    /// <summary>The limb set carries every limb separately; the same limb is not lost twice.</summary>
    [Fact]
    public void TheSetKeepsEveryLimbApart()
    {
        BodyPartSet set = BodyPartSet.SwordArm | BodyPartSet.LeftLeg;

        Assert.True(set.Has(BodyPart.SwordArm));
        Assert.False(set.Has(BodyPart.OffArm));
        Assert.True(set.Has(BodyPart.LeftLeg));
        Assert.False(set.Has(BodyPart.RightLeg));
        Assert.Equal([BodyPart.SwordArm, BodyPart.LeftLeg], set.Parts());
    }
}
