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

    /// <summary>
    /// The whole body lifted off the ground it stands on; a positive value sinks it.
    /// </summary>
    /// <remarks>
    /// It is applied <b>before</b> <see cref="RootRotation"/> and is therefore straight up and down in
    /// the scene whatever the body is doing. The yard's press-ups are what it exists for: a body laid
    /// flat pivots on the feet and ends up lying <i>on</i> the ground, with the arms that are supposed
    /// to be holding it up reaching down through it. Every other pose leaves it at zero and is
    /// unchanged.
    /// </remarks>
    public float RootOffsetY { get; init; }

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
