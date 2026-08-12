namespace Domina.Presentation;

/// <summary>Sahne düzlemindeki bir nokta. Godot'un <c>Vector2</c>'sinin motorsuz karşılığı.</summary>
/// <remarks>
/// Ayrı bir tip olmasının tek sebebi bu katmanın motoru referans almaması. Godot
/// katmanı okurken <c>new Vector2(p.X, p.Y)</c> yazar; başka bir dönüşüm yok.
/// </remarks>
public readonly record struct ScenePoint(float X, float Y)
{
    /// <summary>İki nokta arasında doğrusal geçiş.</summary>
    public static ScenePoint Lerp(ScenePoint from, ScenePoint to, float t) =>
        new(from.X + ((to.X - from.X) * t), from.Y + ((to.Y - from.Y) * t));
}

/// <summary>Animasyon eğrileri.</summary>
internal static class Curves
{
    /// <summary>Uçlarda yumuşayan 0-1 geçişi (smoothstep).</summary>
    public static float Smooth(float t)
    {
        float clamped = Math.Clamp(t, 0f, 1f);
        return clamped * clamped * (3f - (2f * clamped));
    }

    /// <inheritdoc cref="Smooth(float)"/>
    public static float Smooth(double t) => Smooth((float)t);
}
