using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// Pose generation. All these numbers will change when art arrives; what is tested is <b>the rules, not
/// the numbers</b>: a severed limb does not come back, a body does not twitch, a one-legged warrior
/// limps while fleeing too, and a missed strike looks different from a landed one.
/// </summary>
public class RigAnimatorTests
{
    private const double _frame = 1.0 / 60;

    private static RigPose Step(RigAnimator animator, CombatState state, double phase = 0, int frames = 1)
    {
        RigPose pose = default;

        for (int i = 0; i < frames; i++)
        {
            pose = animator.Advance(state, phase, _frame);
        }

        return pose;
    }

    [Fact]
    public void ALimbIsSeveredOnlyOnce()
    {
        var animator = new RigAnimator();
        var loss = new RigReaction(new WarriorId(1), RigReactionKind.Dismember, BodyPart.SwordArm);

        Assert.Equal(BodyPart.SwordArm, animator.React(loss));
        Assert.True(animator.HasLost(BodyPart.SwordArm));

        // If it comes a second time the node is no longer in the scene: it cannot be severed again.
        Assert.Null(animator.React(loss));
    }

    [Fact]
    public void TheEscapedAreNotDrawn()
    {
        var animator = new RigAnimator();

        Assert.True(Step(animator, CombatState.Idle).Visible);
        Assert.False(Step(animator, CombatState.Escaped).Visible);
    }

    [Fact]
    public void TheDeadCollapseAndSettle()
    {
        var animator = new RigAnimator();

        RigPose early = Step(animator, CombatState.Dead, frames: 3);
        RigPose late = Step(animator, CombatState.Dead, frames: 60);
        RigPose settled = Step(animator, CombatState.Dead, frames: 120);

        Assert.True(late.RootRotation > early.RootRotation);
        Assert.Equal(late.RootRotation, settled.RootRotation, 3);
        Assert.True(settled.RootRotation > 1.4f);
    }

    /// <summary>A body does not twitch: a half-finished shake ends with death.</summary>
    [Fact]
    public void TheCorpseDoesNotFlinch()
    {
        var animator = new RigAnimator();
        animator.React(new RigReaction(new WarriorId(1), RigReactionKind.Flinch));

        Assert.Equal(0f, Step(animator, CombatState.Dead).HurtBlend);
    }

    [Fact]
    public void AFlinchFadesInsteadOfSticking()
    {
        var animator = new RigAnimator();
        animator.React(new RigReaction(new WarriorId(1), RigReactionKind.Flinch));

        Assert.True(Step(animator, CombatState.Idle).HurtBlend > 0);

        // A second later no trace must be left, or the warrior stays permanently red.
        Assert.Equal(0f, Step(animator, CombatState.Idle, frames: 60).HurtBlend);
    }

    /// <summary>If a miss and a hit look the same, the player reads the fight from the health bar alone.</summary>
    [Fact]
    public void AMissLooksDifferentFromALandedSwing()
    {
        var missed = new RigAnimator();
        missed.React(new RigReaction(new WarriorId(1), RigReactionKind.Overswing));

        RigPose overswung = Step(missed, CombatState.AttackRecovery, phase: 0.2);
        RigPose clean = Step(new RigAnimator(), CombatState.AttackRecovery, phase: 0.2);

        Assert.True(overswung.Weapon < clean.Weapon);
        Assert.True(overswung.Torso > clean.Torso);
    }

    /// <summary>
    /// An opportunity attack has no state in the core — it resolves instantly behind fleeing prey. If the
    /// idle warrior does not play the swing, who the free hit came from is never visible on screen.
    /// </summary>
    [Fact]
    public void TheOpportunitySwingInterruptsTheIdlePose()
    {
        var hunter = new RigAnimator();
        hunter.React(new RigReaction(new WarriorId(1), RigReactionKind.OpportunitySwing));

        RigPose swinging = Step(hunter, CombatState.Idle);
        RigPose waiting = Step(new RigAnimator(), CombatState.Idle);

        Assert.True(swinging.NearShoulder > waiting.NearShoulder + 1f);

        // After roughly half a second he must return to waiting.
        RigPose after = Step(hunter, CombatState.Idle, frames: 60);
        Assert.Equal(waiting.NearShoulder, after.NearShoulder, 1);
    }

    [Fact]
    public void ADodgeLeansAwayFromTheBlade()
    {
        var animator = new RigAnimator();
        animator.React(new RigReaction(new WarriorId(1), RigReactionKind.Dodge));

        RigPose dodging = Step(animator, CombatState.Idle);
        RigPose waiting = Step(new RigAnimator(), CombatState.Idle);

        Assert.True(dodging.RootRotation < waiting.RootRotation);
        Assert.True(dodging.Torso < waiting.Torso);
    }

    [Fact]
    public void TheOneArmedFightWithWhatIsLeft()
    {
        var animator = new RigAnimator();
        animator.React(new RigReaction(new WarriorId(1), RigReactionKind.Dismember, BodyPart.SwordArm));

        RigPose maimed = Step(animator, CombatState.Idle);
        RigPose whole = Step(new RigAnimator(), CombatState.Idle);

        // The body turns toward the sound side and the remaining arm comes forward.
        Assert.True(maimed.Torso > whole.Torso);
        Assert.True(maimed.FarShoulder < whole.FarShoulder);
    }

    /// <summary>
    /// When the flee pose ignored the disability, a warrior who had lost a leg ran out of the arena on
    /// <b>two legs</b>: because the severed leg's node was not in the scene it looked like one leg on
    /// screen, but the hip hung at the last limp value.
    /// </summary>
    [Fact]
    public void TheOneLeggedLimpWhileFleeingToo()
    {
        var animator = new RigAnimator();
        animator.React(new RigReaction(new WarriorId(1), RigReactionKind.Dismember, BodyPart.RightLeg));

        RigPose fleeing = Step(animator, CombatState.Retreating, frames: 12);

        // The severed leg is not driven, the remaining leg carries, the hip sinks.
        Assert.Equal(0f, fleeing.NearHip);
        Assert.Equal(0f, fleeing.NearKnee);
        Assert.True(fleeing.HipOffsetY > 0);
        Assert.True(fleeing.HipOffsetX > 0);

        RigPose whole = Step(new RigAnimator(), CombatState.Retreating, frames: 12);
        Assert.Equal(0f, whole.HipOffsetY);
        Assert.NotEqual(0f, whole.NearHip);
    }

    [Fact]
    public void TheOneLeggedStandUnevenlyWhileWaiting()
    {
        var animator = new RigAnimator();
        animator.React(new RigReaction(new WarriorId(1), RigReactionKind.Dismember, BodyPart.RightLeg));

        RigPose waiting = Step(animator, CombatState.Idle, frames: 12);

        Assert.Equal(0f, waiting.NearHip);
        Assert.True(waiting.Torso > Step(new RigAnimator(), CombatState.Idle, frames: 12).Torso);
    }

    /// <summary>
    /// The uninterruptible window's pose must be distinct: the player's moment of "I can no longer pull
    /// okuyabilmesinin tek yolu bu.
    /// </summary>
    [Fact]
    public void TheWindupRaisesTheBladeAsItLocks()
    {
        var animator = new RigAnimator();

        RigPose start = Step(animator, CombatState.AttackWindup, phase: 0);
        RigPose locked = Step(animator, CombatState.AttackWindup, phase: 1);

        Assert.True(locked.NearShoulder > start.NearShoulder);
        Assert.True(locked.Weapon < start.Weapon);
    }

    [Fact]
    public void TheSwingTravelsThroughTheRecovery()
    {
        var animator = new RigAnimator();

        RigPose raised = Step(animator, CombatState.AttackRecovery, phase: 0);
        RigPose finished = Step(animator, CombatState.AttackRecovery, phase: 1);

        Assert.True(finished.NearShoulder < raised.NearShoulder);
        Assert.True(finished.Weapon > raised.Weapon);
    }

    /// <summary>While fleeing, the back is turned and the body leans forward — no evasion or block.</summary>
    [Fact]
    public void TheFleeingTurnTheirBack()
    {
        RigPose fleeing = Step(new RigAnimator(), CombatState.Retreating, frames: 5);
        RigPose waiting = Step(new RigAnimator(), CombatState.Idle, frames: 5);

        Assert.True(fleeing.Torso > waiting.Torso);
        Assert.True(fleeing.Head < waiting.Head);
    }
}
