using Domina.Core.Combat;

namespace Domina.Presentation;

/// <summary>
/// Havadaki bir merminin ekrandaki hâli.
/// </summary>
/// <remarks>
/// <para>
/// A projectile is not a warrior: it has no rig, no pose, no reactions. All it needs is two endpoints
/// and a progress ratio. That is why it is carried as a separate scene object rather than through
/// <see cref="RigAnimator"/>.
/// </para>
/// <para>
/// The core <b>does not hold</b> where the projectile is — it only knows when it will arrive (see
/// <c>Projectile</c>). The position in between is pure visualisation; it does not feed back into the
/// beslenmez.
/// </para>
/// </remarks>
public sealed class ProjectileView(ProjectileLaunched launched)
{
    private readonly double _flightSeconds = Math.Max(0.0001, launched.FlightSeconds);

    public string Weapon { get; } = launched.Weapon;

    public ArenaPoint From { get; } = launched.From;

    public ArenaPoint To { get; } = launched.To;

    /// <summary>The time since it took off.</summary>
    public double Elapsed { get; private set; }

    /// <summary>How far the flight has progressed (0-1).</summary>
    public double Progress => Math.Clamp(Elapsed / _flightSeconds, 0, 1);

    /// <summary>Has it arrived? An arrived projectile is removed from the scene.</summary>
    public bool HasLanded => Progress >= 1;

    /// <summary>The projectile's current place on the arena plane.</summary>
    public ArenaPoint Position
    {
        get
        {
            double t = Progress;
            return new ArenaPoint(
                From.X + ((To.X - From.X) * t),
                From.Y + ((To.Y - From.Y) * t));
        }
    }

    public void Advance(double delta) => Elapsed += delta;
}

/// <summary>
/// Drives the projectiles in the scene from the event stream.
/// </summary>
/// <remarks>
/// The same pattern as <see cref="ReactionReader"/>: events are one-off and the read position is
/// tracked with a counter. The reason they are kept apart is that their lifetimes differ — a reaction
/// lasts a frame, a projectile lives for the whole flight.
/// </remarks>
public sealed class ProjectileTracker
{
    private readonly List<ProjectileView> _inFlight = [];

    /// <summary>The number of events read so far.</summary>
    public int Consumed { get; private set; }

    /// <summary>Halen havada olan mermiler.</summary>
    public IReadOnlyList<ProjectileView> InFlight => _inFlight;

    /// <summary>Takes the new throws, advances those in the air, drops those that arrive.</summary>
    public void Advance(IReadOnlyList<BattleEvent> events, double delta)
    {
        ArgumentNullException.ThrowIfNull(events);

        for (; Consumed < events.Count; Consumed++)
        {
            if (events[Consumed] is ProjectileLaunched launched)
            {
                _inFlight.Add(new ProjectileView(launched));
            }
        }

        for (int i = 0; i < _inFlight.Count; i++)
        {
            _inFlight[i].Advance(delta);

            if (_inFlight[i].HasLanded)
            {
                _inFlight.RemoveAt(i--);
            }
        }
    }
}
