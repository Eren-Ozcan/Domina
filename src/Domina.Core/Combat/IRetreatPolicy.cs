using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// The thing that makes the retreat decision.
/// </summary>
/// <remarks>
/// This policy does not exist in the game — the decision comes from the player's key
/// (<see cref="Battle.CommandRetreat"/>). The policy is for standing in for the player <b>in batch
/// simulation</b>: it makes it possible to measure the difference in death rate between "a player who
/// never pulls out" and "a player who pulls out at 30% health".
/// </remarks>
public interface IRetreatPolicy
{
    bool ShouldRetreat(in RetreatContext context);
}

/// <summary>The snapshot the policy sees when deciding.</summary>
/// <param name="WarriorId">The warrior being decided on.</param>
/// <param name="HealthFraction">The ratio of health left to maximum health (0-1).</param>
/// <param name="StaminaFraction">The ratio of stamina left to maximum stamina (0-1).</param>
/// <param name="ElapsedSeconds">The time since the fight began.</param>
/// <param name="AlliesStanding">The warriors still standing on the same side (himself included).</param>
/// <param name="EnemiesStanding">The warriors still standing on the other side.</param>
public readonly record struct RetreatContext(
    WarriorId WarriorId,
    double HealthFraction,
    double StaminaFraction,
    double ElapsedSeconds,
    int AlliesStanding,
    int EnemiesStanding);

/// <summary>A player who never pulls out — the baseline for carelessness.</summary>
public sealed class NeverRetreat : IRetreatPolicy
{
    public static NeverRetreat Instance { get; } = new();

    public bool ShouldRetreat(in RetreatContext context) => false;
}

/// <summary>A player who pulls out when health falls to a given share.</summary>
/// <remarks>
/// A crude model: because the command is <b>team-level</b>, when one warrior's health drops the whole
/// party leaves the field. In 3v3 that means the overwhelming majority of fights are abandoned — not
/// how a real player plays. It is used as a measurement baseline; as a player model
/// <see cref="RetreatWhenLosing"/> is more realistic.
/// </remarks>
public sealed class RetreatBelowHealth(double healthFraction) : IRetreatPolicy
{
    public double HealthFraction { get; } = healthFraction;

    public bool ShouldRetreat(in RetreatContext context) =>
        context.HealthFraction <= HealthFraction;
}

/// <summary>
/// A player who pulls out at a given second, whatever is happening.
/// </summary>
/// <remarks>
/// Policies that watch health cannot measure the <b>free</b> end of escape: if health has dropped a
/// wound has already been taken, so "everyone withdrew unwounded" never occurs by construction. Yet a
/// real player can see the matchup and press in the very first second. This policy is for measuring
/// that end.
/// </remarks>
public sealed class RetreatAtSecond(double seconds) : IRetreatPolicy
{
    public double Seconds { get; } = seconds;

    public bool ShouldRetreat(in RetreatContext context) => context.ElapsedSeconds >= Seconds;
}

/// <summary>
/// A player who pulls out when the fight <b>turns</b>: he withdraws when outnumbered and his health is
/// also below the threshold.
/// </summary>
/// <remarks>
/// A real player does not cancel an expedition over a single wound; he looks at how the fight is going.
/// A policy that watches health alone makes more than 80% of 3v3 fights be abandoned and renders the
/// measurement meaningless — the victory rate comes out low, but the cause is not balance, it is the
/// policy itself.
/// </remarks>
public sealed class RetreatWhenLosing(double healthFraction) : IRetreatPolicy
{
    public double HealthFraction { get; } = healthFraction;

    public bool ShouldRetreat(in RetreatContext context) =>
        context.AlliesStanding < context.EnemiesStanding
        && context.HealthFraction <= HealthFraction;
}
