using Domina.Core.Dojo;

namespace Domina.Presentation.Tests;

/// <summary>
/// The men on the training ground.
/// </summary>
/// <remarks>
/// What is tested is what the yard is <b>for</b>: that a player standing in it can tell one drill from
/// another without opening a sheet. So the assertions are about the poses differing and about nothing
/// leaving the rig broken — never about a particular angle, which is a drawing decision and is meant
/// to be changed without a test standing in the way.
/// </remarks>
public class DrillAnimatorTests
{
    private static readonly Drill[] _drills =
    [
        Drill.Strikes, Drill.Guard, Drill.Footwork, Drill.Conditioning, Drill.Meditation,
    ];

    /// <summary>
    /// The whole claim of the ground: five drills, five things to look at. If two of them posed the
    /// same, the yard would be saying the men are working and nothing more.
    /// </summary>
    [Fact]
    public void NoTwoDrillsLookTheSame()
    {
        foreach (double clock in (double[])[0.0, 0.4, 1.1, 2.7])
        {
            HashSet<RigPose> poses = [.. _drills.Select(drill => DrillAnimator.Drilling(drill, clock))];

            Assert.Equal(_drills.Length, poses.Count);
        }
    }

    [Fact]
    public void AManOnADrillDoesNotLookLikeAManWithNothingToDo()
    {
        RigPose waiting = DrillAnimator.Pose(DojoActivity.Resting, Drill.Strikes, 1.3);

        foreach (Drill drill in _drills)
        {
            Assert.NotEqual(waiting, DrillAnimator.Drilling(drill, 1.3));
        }
    }

    /// <summary>
    /// A man in the infirmary is on the ground where the player can see him, and what he must not look
    /// like is a man working: the yard is where the player counts who is available today.
    /// </summary>
    [Fact]
    public void AManInTheInfirmaryIsNotDrawnDrilling()
    {
        RigPose hurt = DrillAnimator.Pose(DojoActivity.Recovering, Drill.Conditioning, 0.8);

        Assert.NotEqual(DrillAnimator.Drilling(Drill.Conditioning, 0.8), hurt);
        Assert.NotEqual(DrillAnimator.Pose(DojoActivity.Resting, Drill.Conditioning, 0.8), hurt);
    }

    /// <summary>The drill is only read on a training day — a resting man's drill is tomorrow's plan.</summary>
    [Fact]
    public void TheDrillIsOnlyReadOnATrainingDay()
    {
        RigPose one = DrillAnimator.Pose(DojoActivity.Resting, Drill.Strikes, 2.2);
        RigPose two = DrillAnimator.Pose(DojoActivity.Resting, Drill.Meditation, 2.2);

        Assert.Equal(one, two);
    }

    /// <summary>
    /// Meditation is the drill with no motion in it (docs/GDD.md §10: the day the sword is not
    /// touched), and the four that move have to move.
    /// </summary>
    [Fact]
    public void TheFourWorkingDrillsMoveAndTheFifthBarelyDoes()
    {
        foreach (Drill drill in (Drill[])[Drill.Strikes, Drill.Guard, Drill.Footwork, Drill.Conditioning])
        {
            Assert.NotEqual(DrillAnimator.Drilling(drill, 0.0), DrillAnimator.Drilling(drill, 0.55));
        }

        RigPose sitting = DrillAnimator.Drilling(Drill.Meditation, 0.0);
        RigPose later = DrillAnimator.Drilling(Drill.Meditation, 0.55);

        Assert.Equal(sitting.HipOffsetY, later.HipOffsetY);
        Assert.Equal(sitting.NearHip, later.NearHip);
    }

    /// <summary>
    /// Every pose runs through the rig unchecked, so a number that is not a number would be applied to
    /// a bone and the figure would vanish from the yard with nothing said.
    /// </summary>
    [Fact]
    public void NoDrillEverProducesABrokenAngle()
    {
        foreach (Drill drill in _drills)
        {
            for (double clock = 0; clock < 6; clock += 0.05)
            {
                RigPose pose = DrillAnimator.Drilling(drill, clock);

                Assert.True(pose.Visible);

                foreach (float angle in Angles(pose))
                {
                    Assert.True(float.IsFinite(angle));
                    Assert.InRange(angle, -8f, 8f);
                }

                Assert.True(float.IsFinite(pose.HipOffsetX));
                Assert.True(float.IsFinite(pose.HipOffsetY));
            }
        }
    }

    /// <summary>
    /// A drill's cycle repeats: the ground must not drift out of its own rhythm over a day. The
    /// comparison is to three places, because a cycle is found by dividing a growing clock and the
    /// last bits of the division are not the same two cycles apart.
    /// </summary>
    [Fact]
    public void TheCycleComesBackToWhereItStarted()
    {
        Assert.Equal(
            DrillAnimator.Drilling(Drill.Strikes, 0.25).NearShoulder,
            DrillAnimator.Drilling(Drill.Strikes, 1.8 + 0.25).NearShoulder,
            precision: 3);

        Assert.Equal(
            DrillAnimator.Drilling(Drill.Guard, 0.25).NearElbow,
            DrillAnimator.Drilling(Drill.Guard, 2.0 + 0.25).NearElbow,
            precision: 3);
    }

    private static IEnumerable<float> Angles(RigPose pose) =>
    [
        pose.RootRotation, pose.Torso, pose.Head,
        pose.NearShoulder, pose.NearElbow, pose.FarShoulder, pose.FarElbow,
        pose.NearHip, pose.NearKnee, pose.FarHip, pose.FarKnee, pose.Weapon,
    ];
}
