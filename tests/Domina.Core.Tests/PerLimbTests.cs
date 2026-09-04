using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// Uzuvlar tek tek durur: kılıç kolu, boştaki kol, iki bacak. Bu testler ayrımın iki
/// vaadini bağlar — zırh her uzva ayrı kuşanılır, ve hangi kolu kaybettiğin bir şey
/// ifade eder.
/// </summary>
public class PerLimbTests
{
    /// <summary>Kılıç kolunun kaybı boştaki kolun kaybından ağırdır.</summary>
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

    /// <summary>İki bacağın kaybı tek bacağınkinden ağırdır — çarpanlar birleşir.</summary>
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
    /// Zırh her uzva ayrı kuşanılır: bir kolu kollukla örtmek diğerini örtmez.
    /// "Ağır göğüslük, çıplak kollar" kararının yarısı da böyle mümkün olur (§7).
    /// </summary>
    [Fact]
    public void EachLimbCarriesItsOwnPiece()
    {
        var mixed = new Armor(
            "Yarım kuşam",
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

    /// <summary>Uzuv kümesi her uzvu ayrı taşır; aynı uzuv iki kez kaybedilmez.</summary>
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
