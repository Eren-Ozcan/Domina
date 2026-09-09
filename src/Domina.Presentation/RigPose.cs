namespace Domina.Presentation;

/// <summary>
/// A warrior's pose in a single frame: every bone's angle, in radians.
/// </summary>
/// <remarks>
/// <para>
/// The exact counterpart of the locked rig (see <c>docs/PROGRESS.md</c> → "The locked rig"). The
/// <b>near</b> side is the one facing the player, the one holding the weapon; the <b>far</b> side stays
/// behind the body. The severing points are the shoulder and the hip, which is why the arm and leg
/// chains are addressed separately.
/// </para>
/// <para>
/// Adding a field is not cheap: a new field means reviewing every pose. When art arrives this type
/// stays the same, only the drawing hung on the bone changes.
/// </para>
/// </remarks>
public readonly record struct RigPose
{
    /// <summary>The whole body toppling — used only in death.</summary>
    public float RootRotation { get; init; }

    /// <summary>The hip's horizontal shift from its resting point (filled while limping).</summary>
    public float HipOffsetX { get; init; }

    /// <summary>The hip sinking; a positive value goes down (filled while limping).</summary>
    public float HipOffsetY { get; init; }

    public float Torso { get; init; }

    public float Head { get; init; }

    public float NearShoulder { get; init; }

    public float NearElbow { get; init; }

    public float FarShoulder { get; init; }

    public float FarElbow { get; init; }

    public float NearHip { get; init; }

    public float NearKnee { get; init; }

    public float FarHip { get; init; }

    public float FarKnee { get; init; }

    public float Weapon { get; init; }

    /// <summary>The blend ratio toward the pain colour (0-1) — it rises at the moment of taking a hit.</summary>
    public float HurtBlend { get; init; }

    /// <summary>Is the warrior visible in the scene? It switches off when he leaves the arena.</summary>
    public bool Visible { get; init; }
}
