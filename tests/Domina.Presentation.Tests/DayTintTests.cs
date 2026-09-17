using Domina.Presentation;

namespace Domina.Presentation.Tests;

/// <summary>
/// The day-cycle wash (ROADMAP phase 2.2): the hour is allowed to colour the world and is never
/// allowed to cost legibility.
/// </summary>
public class DayTintTests
{
    [Fact]
    public void TheWashIsNeverHeavierThanItsCeiling()
    {
        for (int step = 0; step <= 200; step++)
        {
            double progress = step / 200.0;

            Assert.InRange(DayTint.At(progress).Strength, 0f, DayTint.MaxStrength);
        }
    }

    [Fact]
    public void AnHourOutsideTheDayIsClampedRatherThanExtrapolated()
    {
        Assert.Equal(DayTint.At(0), DayTint.At(-4));
        Assert.Equal(DayTint.At(1), DayTint.At(9));
    }

    [Fact]
    public void TheFlatHoursLayDownAlmostNothing()
    {
        // Midday is when the player is reading the sheets, and the wash must be out of the way there.
        Assert.True(DayTint.At(0.30).Strength < 0.08f);
    }

    [Fact]
    public void TheDayWarmsAtItsEndsAndCoolsAtNight()
    {
        Wash dawn = DayTint.At(0.02);
        Wash dusk = DayTint.At(0.72);
        Wash night = DayTint.At(0.99);

        Assert.True(dawn.Red > dawn.Blue);
        Assert.True(dusk.Red > dusk.Blue);
        Assert.True(night.Blue > night.Red);
    }

    [Fact]
    public void TheWashMovesWithoutStepping()
    {
        Wash last = DayTint.At(0);

        for (int step = 1; step <= 400; step++)
        {
            Wash now = DayTint.At(step / 400.0);

            // No two neighbouring hours may differ by more than a hair, or the yard would blink as the
            // clock crosses one of the day's marks.
            Assert.True(Math.Abs(now.Red - last.Red) < 0.05f);
            Assert.True(Math.Abs(now.Green - last.Green) < 0.05f);
            Assert.True(Math.Abs(now.Blue - last.Blue) < 0.05f);
            Assert.True(Math.Abs(now.Strength - last.Strength) < 0.05f);

            last = now;
        }
    }
}
