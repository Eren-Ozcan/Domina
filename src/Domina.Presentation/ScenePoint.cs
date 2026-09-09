namespace Domina.Presentation;

/// <summary>A point on the scene plane. The engine-free counterpart of Godot's <c>Vector2</c>.</summary>
/// <remarks>
/// The only reason it is a separate type is that this layer does not reference the engine. The Godot
/// layer writes <c>new Vector2(p.X, p.Y)</c> when reading it; there is no other conversion.
/// </remarks>
public readonly record struct ScenePoint(float X, float Y)
{
    /// <summary>A linear transition between two points.</summary>
    public static ScenePoint Lerp(ScenePoint from, ScenePoint to, float t) =>
        new(from.X + ((to.X - from.X) * t), from.Y + ((to.Y - from.Y) * t));
}

/// <summary>Animation curves.</summary>
internal static class Curves
{
    /// <summary>A 0-1 transition that eases at the ends (smoothstep).</summary>
    public static float Smooth(float t)
    {
        float clamped = Math.Clamp(t, 0f, 1f);
        return clamped * clamped * (3f - (2f * clamped));
    }

    /// <inheritdoc cref="Smooth(float)"/>
    public static float Smooth(double t) => Smooth((float)t);
}
