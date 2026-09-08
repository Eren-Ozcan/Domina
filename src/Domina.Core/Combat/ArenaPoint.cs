namespace Domina.Core.Combat;

/// <summary>
/// A point on the arena plane. <b>X</b> along the line (left to right), <b>Y</b> depth.
/// </summary>
/// <remarks>
/// <para>
/// Because the core has to be engine-free, Godot's <c>Vector2</c> cannot be used. The unit is arbitrary
/// but matches the scene's scale: the arena is 1920 units wide, a warrior
/// 256 birim boyunda.
/// </para>
/// <para>
/// Depth is shown on screen as a vertical offset + a slight scale + the draw order (brawler staging).
/// So the plane is real while the camera still looks from the side.
/// </para>
/// </remarks>
public readonly record struct ArenaPoint(double X, double Y)
{
    public static ArenaPoint Zero => new(0, 0);

    public double DistanceTo(ArenaPoint other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>For comparing distances without taking a square root.</summary>
    public double SquaredDistanceTo(ArenaPoint other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>Advances at most <paramref name="distance"/> units toward the target.</summary>
    public ArenaPoint MovedToward(ArenaPoint target, double distance)
    {
        double dx = target.X - X;
        double dy = target.Y - Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length <= double.Epsilon || distance <= 0)
        {
            return this;
        }

        if (distance >= length)
        {
            return target;
        }

        double scale = distance / length;
        return new ArenaPoint(X + (dx * scale), Y + (dy * scale));
    }

    /// <summary>Advances away from the target.</summary>
    /// <remarks>
    /// The distance is <b>always</b> what was asked for: it is not bounded by the gap between them. If
    /// the direction were computed without being reduced to a unit vector, a step taken near the source
    /// would shorten and the request "this far away" would be silently clipped.
    /// </remarks>
    public ArenaPoint MovedAwayFrom(ArenaPoint source, double distance)
    {
        double dx = X - source.X;
        double dy = Y - source.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length <= double.Epsilon || distance <= 0)
        {
            return this;
        }

        double scale = distance / length;
        return new ArenaPoint(X + (dx * scale), Y + (dy * scale));
    }
}
