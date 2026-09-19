using Domina.Core.Model;
using Domina.Sim;

namespace Domina.Sim.Tests;

/// <summary>The implement sweep — Open Decision #19's knobs.</summary>
public class ImplementBenchTests : IDisposable
{
    public void Dispose()
    {
        ImplementBench.Reset();
        GC.SuppressFinalize(this);
    }

    private static SimOptions Parse(params string[] args)
    {
        ParsedArgs parsed = SimArgs.Parse(args);
        Assert.Null(parsed.Error);
        Assert.NotNull(parsed.Options);
        return parsed.Options!;
    }

    [Fact]
    public void WithNothingOnTheCommandLineTheImplementsAreTheCatalogue()
    {
        ImplementBench.Apply(Parse().Implements);

        Assert.Equal(Weapon.Jitte().Damage, ImplementBench.Jitte().Damage);
        Assert.Equal(Weapon.Sai().AttackSeconds, ImplementBench.Sai().AttackSeconds);
        Assert.Equal(Weapon.PoisonedTanto().Poison, ImplementBench.PoisonedTanto().Poison);
    }

    [Fact]
    public void TheJitteAndTheSaiTakeTheSweptDamage()
    {
        ImplementBench.Apply(Parse("--implement-damage", "18").Implements);

        Assert.Equal(18, ImplementBench.Jitte().Damage);
        Assert.Equal(18, ImplementBench.Sai().Damage);
    }

    /// <summary>
    /// The sai is the heavier grip, and the sweep must not flatten that.
    /// </summary>
    /// <remarks>
    /// The two implements differ in grip, not in speed: the sai's cycle is the jitte's plus 0.05. A
    /// sweep that set both to one number would be measuring a weapon the game does not have.
    /// </remarks>
    [Fact]
    public void TheSaiKeepsItsExtraWeightWhenTheCycleIsSwept()
    {
        double gap = Weapon.Sai().AttackSeconds - Weapon.Jitte().AttackSeconds;

        ImplementBench.Apply(Parse("--implement-cycle", "0.90").Implements);

        Assert.Equal(0.90, ImplementBench.Jitte().AttackSeconds);
        Assert.Equal(0.90 + gap, ImplementBench.Sai().AttackSeconds, 6);
    }

    [Fact]
    public void ThePoisonedKnifeTakesItsBladeAndItsDose()
    {
        ImplementBench.Apply(Parse("--blade-damage", "11", "--dose", "1.5").Implements);

        Weapon knife = ImplementBench.PoisonedTanto();
        Assert.Equal(11, knife.Damage);
        Assert.Equal(1.5, knife.Poison);
        Assert.Equal(Weapon.PoisonedTanto().AttackSeconds, knife.AttackSeconds);
    }

    [Fact]
    public void ANumberThatIsNotOneIsRefused()
    {
        Assert.NotNull(SimArgs.Parse(["--implement-damage", "-1"]).Error);
        Assert.NotNull(SimArgs.Parse(["--implement-cycle", "0"]).Error);
        Assert.NotNull(SimArgs.Parse(["--dose", "nope"]).Error);
    }
}
