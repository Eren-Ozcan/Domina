using Domina.Core.Combat;

namespace Domina.Presentation;

/// <summary>
/// Projects the arena plane onto the screen.
/// </summary>
/// <remarks>
/// <para>
/// The core holds the warriors on a <b>plane</b> (X along the line, Y depth). The camera still looks
/// from the side; depth is turned into three things on screen: a vertical offset, a slight scale and
/// the draw order. This is 2D brawler staging itself — the depth is real but the image stays flat, so
/// a rig built from cut-out paper pieces does not break.
/// </para>
/// <para>
/// There are <b>no decisions</b> here: the answer to who stands where is in the core. This class only
/// converts units.
/// </para>
/// </remarks>
public sealed record ArenaLayout
{
    /// <summary>The arena's width along the line — must match the value in the core.</summary>
    public float Width { get; init; } = 1920f;

    /// <summary>The arena's depth — must match the value in the core.</summary>
    public float Depth { get; init; } = 420f;

    /// <summary>The ground line where depth is zero (the frontmost).</summary>
    public float FrontGroundY { get; init; } = 860f;

    /// <summary>The rearmost ground line. The difference between them is depth's vertical counterpart.</summary>
    public float BackGroundY { get; init; } = 600f;

    /// <summary>The scale of the rearmost warrior; the front one is 1.0.</summary>
    public float BackScale { get; init; } = 0.78f;

    /// <summary>The lean-back distance while gathering the sword — purely visual.</summary>
    public float WindupDrawBack { get; init; } = 16f;

    /// <summary>Converts an arena point to a screen point.</summary>
    public ScenePoint Project(ArenaPoint point) =>
        new((float)point.X, FrontGroundY - (DepthFraction(point.Y) * (FrontGroundY - BackGroundY)));

    /// <summary>The scale of a warrior at the given depth.</summary>
    public float ScaleAt(double depth) => 1f - (DepthFraction(depth) * (1f - BackScale));

    private float DepthFraction(double depth) =>
        Depth <= 0 ? 0 : Math.Clamp((float)(depth / Depth), 0f, 1f);
}
