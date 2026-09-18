using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// One man's own face. What is tested is <b>the rules, not the dials</b>: the same man is drawn the
/// same way every time, two men are not drawn the same way, and nothing a dial produces can be put on
/// the rig as a broken number.
/// </summary>
public class WarriorLookTests
{
    private static readonly string[] _names =
    [
        "Takeda", "Sasaki", "Ito", "Hanzo", "Kuro", "Genji", "Musashi", "Rin",
        "Yoshi", "Kaede", "Toshiro", "Saburo", "Ichiro", "Hideo", "Ryu", "Nobu",
    ];

    [Fact]
    public void TheSameManIsDrawnTheSameWayTwice()
    {
        Assert.Equal(WarriorLook.Of(new WarriorId(4), "Takeda"), WarriorLook.Of(new WarriorId(4), "Takeda"));
    }

    /// <summary>
    /// The market's candidate and the man bought from it are the same figure. The look is keyed on the
    /// name for this reason: a candidate has no id until the purchase goes through.
    /// </summary>
    [Fact]
    public void TheManBoughtIsTheManWhoStoodAtTheStall()
    {
        WarriorLook atTheStall = WarriorLook.Of("Sasaki");
        WarriorLook inTheYard = WarriorLook.Of(new WarriorId(37), "Sasaki");

        Assert.Equal(atTheStall, inTheYard);
    }

    [Fact]
    public void TwoMenAreNotDrawnTheSameWay()
    {
        HashSet<WarriorLook> looks = [.. _names.Select(WarriorLook.Of)];

        // A collision is possible in principle; a roster's worth of names colliding is not, and that is
        // the case the screens care about.
        Assert.True(looks.Count >= _names.Length - 1, $"{_names.Length} men gave {looks.Count} faces.");
    }

    [Fact]
    public void ANamelessManStillHasAFace()
    {
        Assert.NotEqual(WarriorLook.Of(new WarriorId(1), null), WarriorLook.Of(new WarriorId(2), null));
    }

    [Fact]
    public void EveryDialLandsWhereTheRigCanDrawIt()
    {
        foreach (string name in _names)
        {
            WarriorLook look = WarriorLook.Of(name);

            Assert.InRange(look.Height, 0.9f, 1.1f);
            Assert.InRange(look.Girth, 0.8f, 1.3f);
            Assert.InRange(look.HeadSize, 0.9f, 1.1f);
            Assert.InRange(look.Shade, -0.15f, 0.15f);
            Assert.InRange(look.Accent, 0, WarriorLook.AccentCount - 1);
            Assert.True(Enum.IsDefined(look.Hair));
            Assert.True(Enum.IsDefined(look.Beard));
        }
    }

    /// <summary>The dials must not agree with each other — one hash driving them all gives one face.</summary>
    [Fact]
    public void TheDialsDoNotMoveTogether()
    {
        WarriorLook[] looks = [.. _names.Select(WarriorLook.Of)];

        Assert.True(looks.Select(l => l.Hair).Distinct().Count() > 1);
        Assert.True(looks.Select(l => l.Beard).Distinct().Count() > 1);
        Assert.True(looks.Select(l => l.Accent).Distinct().Count() > 2);
        Assert.Contains(looks, l => l.Headband);
        Assert.Contains(looks, l => !l.Headband);
    }
}
