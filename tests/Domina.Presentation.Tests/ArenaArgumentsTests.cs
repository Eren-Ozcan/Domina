namespace Domina.Presentation.Tests;

/// <summary>
/// Arenanın komut satırı argümanları. Determinizmin pratik karşılığı: toplu simülasyon
/// ilginç bir dövüş bildirdiğinde ("52 numaralı seed'de savaşçı kolunu kaybediyor")
/// o dövüş arenada birebir izlenebilmeli.
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

    /// <summary>Ondalık ayırıcı sistem diline göre değişmemeli.</summary>
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

    /// <summary>Godot kendi argümanlarını da aynı diziye koyabilir.</summary>
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
