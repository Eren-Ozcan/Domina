using Domina.Core.Dojo;

namespace Domina.Presentation;

/// <summary>
/// What a man looks like on the training ground: drill + clock → pose.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of <see cref="RigAnimator"/> for the yard. It is a second class rather than a
/// state on the first because the two read different things: the fight's animator is driven by
/// <see cref="Domina.Core.Combat.CombatState"/> and carries reaction timers and lost limbs, and a man
/// at the post has none of that — he has a drill and a clock, and that is the whole input.
/// </para>
/// <para>
/// It produces the same <see cref="RigPose"/>, so the yard drives the same rig the arena does and a
/// man drilling in the yard is the same body that walks out of the gate. The poses are deliberately
/// plain: a run on the spot, a press on the ground, a swing, a shuffle, a man sitting still. They say
/// <b>which drill</b> from across the yard and nothing more (docs/GDD.md §10 lists the five).
/// </para>
/// <para>
/// Engine-free and stateless but for the clock, like everything else in this assembly: the phase is
/// passed in, so two men on the same drill can be given different phases and the ground does not read
/// as one man copied five times.
/// </para>
/// </remarks>
public static class DrillAnimator
{
    /// <summary>The pose of a man at this moment of his day.</summary>
    /// <param name="activity">What he is doing today — only a training day has a drill in it.</param>
    /// <param name="drill">The drill he is on.</param>
    /// <param name="clock">His own clock, in seconds. Two men are offset so the ground is not a chorus.</param>
    public static RigPose Pose(DojoActivity activity, Drill drill, double clock) => activity switch
    {
        DojoActivity.Training => Drilling(drill, clock),
        DojoActivity.Recovering => Hurt(clock),
        _ => Waiting(clock),
    };

    /// <summary>The pose of a man on the given drill.</summary>
    public static RigPose Drilling(Drill drill, double clock) => drill switch
    {
        Drill.Strikes => Strikes(clock),
        Drill.Guard => Presses(clock),
        Drill.Footwork => Footwork(clock),
        Drill.Conditioning => Running(clock),
        Drill.Meditation => Sitting(clock),
        _ => Waiting(clock),
    };

    /// <summary>Standing in the yard with nothing to do — the same slow bob the fight waits with.</summary>
    private static RigPose Waiting(double clock)
    {
        float bob = MathF.Sin((float)clock * 1.6f) * 0.03f;

        return new RigPose
        {
            Visible = true,
            Torso = -0.04f + bob,
            Head = 0.03f - bob,
            NearShoulder = 2.85f + bob,
            NearElbow = -0.25f,
            FarShoulder = 3.00f - bob,
            FarElbow = -0.20f,
            NearHip = 0.06f,
            NearKnee = 0.04f,
            FarHip = -0.06f,
            FarKnee = 0.04f,
            Weapon = -0.25f,
        };
    }

    /// <summary>
    /// A man out of the infirmary's bed for the afternoon: standing, favouring one side.
    /// </summary>
    /// <remarks>
    /// He is on the ground and not in the hut because the yard is where the player looks for his men,
    /// and a bed he cannot see tells him nothing. What the pose has to say is <b>not working</b>: the
    /// weight is off one leg and the arms hang, which is the opposite of every drill beside it.
    /// </remarks>
    private static RigPose Hurt(double clock)
    {
        float sway = MathF.Sin((float)clock * 1.1f) * 0.04f;

        return new RigPose
        {
            Visible = true,
            Torso = 0.18f + sway,
            Head = 0.24f,
            NearShoulder = 3.05f,
            NearElbow = -0.10f,
            FarShoulder = 3.10f,
            FarElbow = -0.08f,
            HipOffsetY = 8f,
            NearHip = 0.20f,
            NearKnee = 0.26f,
            FarHip = -0.08f,
            FarKnee = 0.06f,
            Weapon = -0.10f,
        };
    }

    /// <summary>Strike drill: the sword is raised and cut down, over and over, at nothing.</summary>
    private static RigPose Strikes(double clock)
    {
        // A slow gather and a fast cut: the asymmetry is what makes it read as a strike rather than as
        // an arm waving. Two thirds of the cycle is the lift.
        float cycle = Wrap(clock, 1.8f);
        bool lifting = cycle < 0.66f;
        float t = lifting
            ? Curves.Smooth(cycle / 0.66f)
            : 1f - Curves.Smooth((cycle - 0.66f) / 0.34f);

        return new RigPose
        {
            Visible = true,
            Torso = -0.08f - (0.18f * t),
            Head = 0.06f * t,
            NearShoulder = 2.15f + (1.45f * t),
            NearElbow = -0.70f - (0.55f * t),
            FarShoulder = 2.45f + (1.15f * t),
            FarElbow = -0.55f - (0.40f * t),
            NearHip = -0.14f,
            NearKnee = 0.10f,
            FarHip = 0.16f,
            FarKnee = 0.12f,
            Weapon = -0.35f - (0.55f * t),
        };
    }

    /// <summary>
    /// Guard drill: presses on the ground, the arms carrying the whole body.
    /// </summary>
    /// <remarks>
    /// The rig has no fourth limb state and needs none: the root is laid down flat (the root rotation
    /// the death fall already uses), the hip is dropped to the ground and the arms are put under the
    /// shoulders. What moves is the whole body, up and down on the arms — which is the drill.
    /// </remarks>
    private static RigPose Presses(double clock)
    {
        // Down slowly, up sharply — a press that rose and fell at one rate reads as a hinge.
        float cycle = Wrap(clock, 2.0f);
        float t = cycle < 0.55f
            ? Curves.Smooth(cycle / 0.55f)
            : 1f - Curves.Smooth((cycle - 0.55f) / 0.45f);

        return new RigPose
        {
            Visible = true,

            // Face down along the ground, and held off it by the length of an arm: the body pivots on
            // the toes, so without the lift he would be lying on the ground he is pressing away from.
            RootRotation = -1.50f,
            RootOffsetY = -86f + (30f * t),
            HipOffsetY = 6f,
            Torso = 0.10f,
            Head = -0.42f,

            // The arms come out of a torso that is now horizontal, so they reach "down" to the ground
            // beside it; bending the elbow is what lowers the chest.
            NearShoulder = 1.45f,
            NearElbow = -0.10f - (0.95f * t),
            FarShoulder = 1.60f,
            FarElbow = -0.10f - (0.95f * t),
            NearHip = 0.04f,
            NearKnee = 0.02f,
            FarHip = -0.04f,
            FarKnee = 0.02f,

            // The weapon is set down beside him rather than left hanging: with the body flat, a blade
            // held at the waiting angle stands straight up out of his hand like a flagpole.
            Weapon = -1.55f,
        };
    }

    /// <summary>Footwork drill: short hops side to side, the body low and the guard up.</summary>
    private static RigPose Footwork(double clock)
    {
        float side = MathF.Sin((float)clock * 4.4f);
        float hop = MathF.Abs(MathF.Sin((float)clock * 8.8f));

        return new RigPose
        {
            Visible = true,
            RootRotation = side * 0.06f,
            HipOffsetX = side * 12f,
            HipOffsetY = 14f - (hop * 12f),
            Torso = -0.20f,
            Head = 0.10f,
            NearShoulder = 1.90f,
            NearElbow = -1.05f,
            FarShoulder = 2.15f,
            FarElbow = -0.95f,
            NearHip = 0.30f - (side * 0.24f),
            NearKnee = 0.34f - (hop * 0.20f),
            FarHip = -0.30f - (side * 0.24f),
            FarKnee = 0.36f - (hop * 0.20f),
            Weapon = -1.20f,
        };
    }

    /// <summary>Conditioning drill: running on the spot, knees high, arms swinging.</summary>
    private static RigPose Running(double clock)
    {
        float run = MathF.Sin((float)clock * 9f);
        float lift = MathF.Abs(MathF.Cos((float)clock * 9f));

        return new RigPose
        {
            Visible = true,
            Torso = -0.14f,
            Head = 0.08f,
            HipOffsetY = 6f - (lift * 5f),
            NearShoulder = 2.45f + (run * 0.55f),
            NearElbow = -1.15f,
            FarShoulder = 2.45f - (run * 0.55f),
            FarElbow = -1.15f,

            // The knee comes up rather than the stride going out: he is not covering ground. A negative
            // hip is the thigh swung forward, so the knee folds on that half of the cycle.
            NearHip = (run * 0.80f) - 0.10f,
            NearKnee = MathF.Max(0, -run) * 1.30f,
            FarHip = (-run * 0.80f) - 0.10f,
            FarKnee = MathF.Max(0, run) * 1.30f,
            Weapon = -0.30f,
        };
    }

    /// <summary>
    /// Meditation: sitting on his heels, hands on his thighs, and nothing moving but his breath.
    /// </summary>
    /// <remarks>
    /// The one drill with no motion in it, deliberately — the day the sword is not touched
    /// (<see cref="Drill.Meditation"/>) has to be the one figure on the ground that is still.
    /// </remarks>
    private static RigPose Sitting(double clock)
    {
        float breath = MathF.Sin((float)clock * 0.9f) * 0.02f;

        return new RigPose
        {
            Visible = true,

            // Seiza: the hip is dropped almost onto the heels, the thighs come forward off it (a
            // negative hip) and the shins fold back under them.
            HipOffsetY = 84f,
            Torso = 0.02f + breath,
            Head = -0.02f - breath,
            NearShoulder = 2.60f,
            NearElbow = -0.95f,
            FarShoulder = 2.70f,
            FarElbow = -0.95f,
            NearHip = -1.42f,
            NearKnee = 2.70f,
            FarHip = -1.36f,
            FarKnee = 2.66f,

            // The sword is across his knees, which is where it is when it is not being held.
            Weapon = -1.30f,
        };
    }

    /// <summary>Where in a cycle of this length the clock stands, 0-1.</summary>
    private static float Wrap(double clock, float seconds)
    {
        float phase = (float)(clock / seconds);
        return phase - MathF.Floor(phase);
    }
}
