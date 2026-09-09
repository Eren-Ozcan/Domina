namespace Domina.Presentation.Tests;

/// <summary>
/// The arena's command-line arguments. What determinism buys in practice: when batch simulation reports
/// an interesting fight ("the warrior loses an arm on seed 52"), that fight must be watchable in the
/// arena exactly as it was.
/// </summary>
public class ArenaArgumentsTests
{
    [Fact]
    public void TheSeedIsRead()
    {
        ArenaArguments arguments = ArenaArguments.Parse(["--seed", "52"]);

        Assert.Equal(52, arguments.Seed);
        Assert.Null(arguments.SpeedMultiplier);
    }

    [Fact]
    public void TheSpeedIsRead()
    {
        ArenaArguments arguments = ArenaArguments.Parse(["--seed", "81", "--speed", "4.5"]);

        Assert.Equal(81, arguments.Seed);
        Assert.Equal(4.5, arguments.SpeedMultiplier);
    }

    /// <summary>The decimal separator must not change with the system language.</summary>
    [Fact]
    public void TheSpeedIsReadTheSameWayEverywhere()
    {
        Assert.Equal(0.25, ArenaArguments.Parse(["--speed", "0.25"]).SpeedMultiplier);
        Assert.Null(ArenaArguments.Parse(["--speed", "0,25"]).SpeedMultiplier);
    }

    [Fact]
    public void NothingIsSetWithoutArguments()
    {
        ArenaArguments arguments = ArenaArguments.Parse([]);

        Assert.Null(arguments.Seed);
        Assert.Null(arguments.SpeedMultiplier);
    }

    /// <summary>Godot can put its own arguments in the same array.</summary>
    [Fact]
    public void UnknownArgumentsAreSkipped()
    {
        ArenaArguments arguments = ArenaArguments.Parse(["--verbose", "--seed", "7", "--headless"]);

        Assert.Equal(7, arguments.Seed);
    }

    [Fact]
    public void ABrokenValueIsIgnoredInsteadOfCrashingTheArena()
    {
        Assert.Null(ArenaArguments.Parse(["--seed", "abc"]).Seed);
        Assert.Null(ArenaArguments.Parse(["--seed"]).Seed);
        Assert.Null(ArenaArguments.Parse(["--speed", "0"]).SpeedMultiplier);
        Assert.Null(ArenaArguments.Parse(["--speed", "-2"]).SpeedMultiplier);
    }
}
