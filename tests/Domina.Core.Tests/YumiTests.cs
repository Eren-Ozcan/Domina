using Domina.Core.Dojo.Save;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The bow — the range class's own implement (docs/GDD.md §4). Everything else in the throwing slot
/// stays open to everyone; this is the one implement that has to be taught.
/// </summary>
public class YumiTests
{
    /// <summary>
    /// The bow is the shuriken's opposite on every axis, and the numbers say so rather than the prose.
    /// </summary>
    [Fact]
    public void TheBowIsTheShurikensOpposite()
    {
        ThrownWeapon yumi = ThrownWeapon.Yumi();
        ThrownWeapon stars = ThrownWeapon.Shuriken();

        Assert.True(yumi.Range > stars.Range, "The bow must outreach a thrown star.");
        Assert.True(yumi.Damage > stars.Damage, "An arrow must be worth more than a star.");
        Assert.True(yumi.Ammo > stars.Ammo, "A quiver holds more than a handful.");
        Assert.True(yumi.ThrowSeconds > stars.ThrowSeconds, "A draw must cost more than a flick.");
        Assert.True(yumi.Speed < stars.Speed, "The arrow flies slower than the star leaves the hand.");
    }

    /// <summary>
    /// The gate is on the implement, not in the tuning: the throwing slot every warrior carries must
    /// not become a class tax, so only the bow asks for training.
    /// </summary>
    [Fact]
    public void OnlyTheBowAsksForTraining()
    {
        Assert.Equal(0.45, ThrownWeapon.Yumi().UntrainedShare);
        Assert.Equal(1, ThrownWeapon.Shuriken().UntrainedShare);
        Assert.Equal(1, ThrownWeapon.PoisonedShuriken().UntrainedShare);
        Assert.Equal(1, ThrownWeapon.ThrowingSpear().UntrainedShare);
    }

    /// <summary>
    /// A saved bow has to load as a bow. Without the share on the record, a season reloaded from disk
    /// would hand every untrained warrior a fully trained hand.
    /// </summary>
    [Fact]
    public void TheGateSurvivesASave()
    {
        ThrownWeapon reloaded = ThrownWeaponSnapshot.From(ThrownWeapon.Yumi()).ToThrownWeapon();

        Assert.Equal(ThrownWeapon.Yumi().UntrainedShare, reloaded.UntrainedShare);
        Assert.Equal(ThrownWeapon.Yumi(), reloaded);
    }

    /// <summary>The bow is two-handed, so an arm ends it — the fitness matrix of §4 already says so.</summary>
    [Fact]
    public void AnArmEndsTheBow()
    {
        Assert.False(ClassAptitude.IsPossibleFor(WarriorClass.Kyudo, [new Disability(BodyPart.SwordArm)]));
        Assert.False(ClassAptitude.IsPossibleFor(WarriorClass.Kyudo, [new Disability(BodyPart.OffArm)]));
    }
}
