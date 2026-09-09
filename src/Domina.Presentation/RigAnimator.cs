using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>
/// A warrior's animation state: state + reactions → pose.
/// </summary>
/// <remarks>
/// <para>
/// Poses are generated <b>procedurally</b> (state + phase → bone angle). When real art arrives an
/// AnimationPlayer can take its place; the caller does not change, because the only interface is
/// <see cref="Advance"/>.
/// </para>
/// <para>
/// The class's memory is only <b>visual</b> memory: timers and lost limbs. It cannot affect the course
/// of the fight — the arrow points one way (see CLAUDE.md → "Architecture rule").
/// </para>
/// </remarks>
public sealed class RigAnimator
{
    // Reaction timers fall from 1 to 0; the larger the number, the shorter the reaction.
    private const double _flinchDecayPerSecond = 4.0;
    private const double _dodgeDecayPerSecond = 3.4;
    private const double _overswingDecayPerSecond = 2.6;
    private const double _opportunityDecayPerSecond = 2.4;
    private const double _stumbleDecayPerSecond = 1.8;
    private const double _catchDecayPerSecond = 2.8;
    private const double _deathFallPerSecond = 3.2;

    private readonly HashSet<BodyPart> _lost = [];

    private double _clock;
    private double _flinch;
    private double _dodge;
    private double _overswing;
    private double _opportunity;
    private double _stumble;
    private double _catch;
    private double _deathLean;

    /// <summary>Lost limbs are permanent — they are not put back.</summary>
    public bool HasLost(BodyPart part) => _lost.Contains(part);

    /// <summary>
    /// Processes a one-off reaction.
    /// </summary>
    /// <returns>
    /// The limb if this reaction took one off <b>for the first time</b>, otherwise <c>null</c>.
    /// The caller uses this to detach the node from the rig; if the same limb comes again it returns
    /// <c>null</c>, because it is no longer in the scene.
    /// </returns>
    public BodyPart? React(in RigReaction reaction)
    {
        switch (reaction.Kind)
        {
            case RigReactionKind.Flinch:
                _flinch = 1;
                break;

            case RigReactionKind.Dodge:
                _dodge = 1;
                break;

            case RigReactionKind.Overswing:
                _overswing = 1;
                break;

            case RigReactionKind.OpportunitySwing:
                _opportunity = 1;
                break;

            case RigReactionKind.Throw:
                // A throw is a swing too; the arm's return uses the same curve.
                _overswing = 1;
                break;

            case RigReactionKind.Catch:
                _catch = 1;
                break;

            case RigReactionKind.Stumble:
                _stumble = 1;
                break;

            case RigReactionKind.PoisonThroe:
                // In the procedural pose it borrows the stumble curve: in both cases nobody struck, the
                // warrior bends under his own weight. This is the place that will split when real art
                // arrives (see docs/ROADMAP.md 2.2).
                _stumble = 1;
                break;

            case RigReactionKind.WeaponLost:
                // It borrows the stumble curve: in both cases the warrior loses his balance for a
                // moment. It will split when real art arrives (docs/ROADMAP.md 2.2).
                _stumble = 1;
                break;

            case RigReactionKind.ArmorShattered:
                // It borrows the stumble curve: a warrior whose plate breaks is thrown for a moment.
                // It will split when real art arrives (docs/ROADMAP.md 2.2).
                _stumble = 1;
                break;

            case RigReactionKind.Block:
                // A shake, not a knockback: it borrows the flinch curve, but the stance itself comes
                // from Guard(), so the warrior stays in place.
                _flinch = 1;
                break;

            case RigReactionKind.Dismember:
                if (reaction.Part is BodyPart part && _lost.Add(part))
                {
                    return part;
                }

                break;

            default:
                break;
        }

        return null;
    }

    /// <summary>Advances the timers and produces this frame's pose.</summary>
    /// <param name="state">The state reported by the core.</param>
    /// <param name="stateProgress">How far the state has progressed (0-1).</param>
    /// <param name="delta">The time elapsed.</param>
    public RigPose Advance(CombatState state, double stateProgress, double delta)
    {
        _clock += delta;
        _flinch = Decay(_flinch, delta, _flinchDecayPerSecond);
        _dodge = Decay(_dodge, delta, _dodgeDecayPerSecond);
        _overswing = Decay(_overswing, delta, _overswingDecayPerSecond);
        _opportunity = Decay(_opportunity, delta, _opportunityDecayPerSecond);
        _stumble = Decay(_stumble, delta, _stumbleDecayPerSecond);
        _catch = Decay(_catch, delta, _catchDecayPerSecond);

        if (state == CombatState.Dead)
        {
            _deathLean = Math.Min(1, _deathLean + (delta * _deathFallPerSecond));
        }

        return Compose(state, stateProgress);
    }

    private static double Decay(double value, double delta, double rate) =>
        Math.Max(0, value - (delta * rate));

    // ---------------------------------------------------------------- composition

    private RigPose Compose(CombatState state, double phase)
    {
        if (state == CombatState.Escaped)
        {
            return default;
        }

        if (state == CombatState.Dead)
        {
            return Death();
        }

        RigPose pose = state switch
        {
            // A throw is a gathering and a swing too; in the procedural pose it uses the same curves as
            // melee. This is the place that will split when real art arrives.
            // A charge's windup is a gathering too — until it earns its own pose
            // (see docs/ROADMAP.md 2.2) it borrows the attack windup's curves.
            CombatState.AttackWindup or CombatState.ThrowWindup or CombatState.ChargeWindup =>
                Windup(phase),
            CombatState.AttackRecovery or CombatState.ThrowRecovery =>
                Swing(Curves.Smooth(Math.Min(1, phase * 2.6))),
            CombatState.Blocking => Guard(),
            CombatState.Stunned => Stagger(),
            CombatState.WeaponBound => Bound(),
            CombatState.Retreating => Retreat(),
            CombatState.Charging => Charge(),
            _ => Idle(),
        };

        // An opportunity attack has no state in the core: it resolves instantly behind fleeing prey.
        // Temporarily taking over the pose of a waiting warrior is the only sign that shows who the
        // free hit came from.
        if (_opportunity > 0 && state == CombatState.Idle)
        {
            pose = Swing(Curves.Smooth(1 - _opportunity));
        }

        if (_overswing > 0
            && (state is CombatState.AttackRecovery or CombatState.ThrowRecovery
                || _opportunity > 0))
        {
            pose = Overswung(pose);
        }

        pose = Injuries(pose, state);
        return Reactions(pose);
    }

    private RigPose Idle()
    {
        float bob = MathF.Sin((float)_clock * 3f) * 0.04f;

        // The weapon arm is ready, the off arm slightly open for balance.
        return new RigPose
        {
            Visible = true,
            Torso = -0.06f + bob,
            Head = 0.04f - bob,
            NearShoulder = 2.15f + bob,
            NearElbow = -0.75f,
            FarShoulder = 2.55f - bob,
            FarElbow = -0.45f,
            NearHip = 0.10f + bob,
            NearKnee = 0.06f,
            FarHip = -0.10f - bob,
            FarKnee = 0.06f,
            Weapon = -0.35f,
        };
    }

    /// <summary>A stun: the warrior is on his feet but defenceless — weapon low, body open.</summary>
    /// <remarks>
    /// The pose has one job: to say <b>he can be hit right now</b>. If it is not distinguishable at a
    /// glance from a waiting warrior, the player cannot see the window the blunt weapon bought, and the
    /// stun becomes a rule that lives only in the numbers table.
    /// </remarks>
    private RigPose Stagger()
    {
        // A slow, irregular sway: deliberately apart from a waiting warrior's rhythmic bobbing.
        float sway = MathF.Sin((float)_clock * 1.7f) * 0.10f;

        return new RigPose
        {
            Visible = true,
            Torso = 0.26f + sway,
            Head = 0.34f + (sway * 0.5f),
            NearShoulder = 1.25f + sway,
            NearElbow = -0.20f,
            FarShoulder = 1.05f - sway,
            FarElbow = -0.15f,
            NearHip = 0.24f + sway,
            NearKnee = 0.28f,
            FarHip = -0.22f,
            FarKnee = 0.20f,
            Weapon = 0.35f,
        };
    }

    /// <summary>
    /// His weapon was caught: the warrior is locked forward, his arm held up.
    /// </summary>
    /// <remarks>
    /// It has to be distinguishable on screen from a stun (<see cref="Stagger"/>): a stunned warrior
    /// sinks and sways, a warrior whose weapon is caught stands <b>taut</b> — arm pinned up, body
    /// pulled forward. If the two looked the same, the player could not tell the window the jitte opened
    /// from the one the blunt weapon opened.
    /// </remarks>
    private RigPose Bound()
    {
        // A tremor: an arm trying to break free and failing. Deliberately fast and small next to the
        // stun's slow sway.
        float strain = MathF.Sin((float)_clock * 9f) * 0.035f;

        return new RigPose
        {
            Visible = true,
            Torso = -0.22f + strain,
            Head = 0.16f - strain,
            NearShoulder = 3.05f + strain,
            NearElbow = -1.05f - strain,
            FarShoulder = 2.30f,
            FarElbow = -0.60f,
            NearHip = -0.20f,
            NearKnee = 0.14f,
            FarHip = 0.18f,
            FarKnee = 0.22f,
            Weapon = -0.95f + strain,
        };
    }

    /// <summary>
    /// The block stance: the warrior is crouched, his weapon horizontal in front of his body.
    /// </summary>
    /// <remarks>
    /// It has to be distinguishable from waiting (<see cref="Idle"/>) — in both the warrior is not
    /// striking, but one is watching for an opening and the other is closed up. No sway: a blocking
    /// warrior does not move, his only movement on screen is being shaken by the incoming blow.
    /// </remarks>
    private static RigPose Guard() => new()
    {
        Visible = true,
        Torso = -0.30f,
        Head = 0.10f,
        NearShoulder = 1.75f,
        NearElbow = -1.35f,
        FarShoulder = 2.05f,
        FarElbow = -1.15f,
        NearHip = 0.22f,
        NearKnee = 0.30f,
        FarHip = -0.26f,
        FarKnee = 0.34f,
        Weapon = -1.55f,
    };

    private static RigPose Windup(double phase)
    {
        // The uninterruptible window: the sword gathers back and up. The pose has to be distinct so the
        // player can visually read the moment of "I can no longer pull out".
        float t = Curves.Smooth(phase);

        return new RigPose
        {
            Visible = true,
            Torso = -0.06f - (0.30f * t),
            Head = 0.10f * t,
            NearShoulder = 2.15f + (1.35f * t),
            NearElbow = -0.75f - (0.55f * t),
            FarShoulder = 2.55f - (0.65f * t),
            FarElbow = -0.45f,
            NearHip = -0.18f * t,
            NearKnee = 0.06f,
            FarHip = -0.10f,
            FarKnee = 0.06f,
            Weapon = -0.35f - (0.55f * t),
        };
    }

    private static RigPose Swing(float t)
    {
        // The strike lands at the first moment, the rest is recovery (interruptible again).
        return new RigPose
        {
            Visible = true,
            Torso = -0.36f + (0.62f * t),
            Head = 0.10f - (0.22f * t),
            NearShoulder = 3.50f - (1.95f * t),
            NearElbow = -1.30f + (1.15f * t),
            FarShoulder = 1.90f + (0.75f * t),
            FarElbow = -0.45f,
            NearHip = -0.18f + (0.34f * t),
            NearKnee = 0.06f,
            FarHip = -0.10f,
            FarKnee = 0.06f,
            Weapon = -0.90f + (0.80f * t),
        };
    }

    /// <summary>An overswing: because the sword found no target, the warrior carries his own move.</summary>
    /// <remarks>
    /// Without an on-screen counterpart for a miss, a landed and a missed strike look the same and the
    /// player has to read the course of the fight from the health bar alone.
    /// </remarks>
    private RigPose Overswung(RigPose pose)
    {
        float t = (float)_overswing;

        return pose with
        {
            RootRotation = pose.RootRotation + (0.10f * t),
            Torso = pose.Torso + (0.20f * t),
            NearShoulder = pose.NearShoulder - (0.30f * t),
            Weapon = pose.Weapon - (0.45f * t),
        };
    }

    /// <summary>
    /// The charge pose: leaning forward, weapon back, a running cycle with longer strides than fleeing.
    /// </summary>
    /// <remarks>
    /// The mirror of fleeing: both are running, in both there is no defence, but one runs toward the
    /// target and the other away from it. What carries the difference is the direction of the body — back
    /// while fleeing, forward while charging (docs/GDD.md §4).
    /// </remarks>
    private RigPose Charge()
    {
        float run = MathF.Sin((float)_clock * 12f);

        return new RigPose
        {
            Visible = true,
            Torso = -0.34f,
            Head = 0.12f,
            NearShoulder = 1.75f + (run * 0.5f),
            NearElbow = -1.05f,
            FarShoulder = 2.9f - (run * 0.5f),
            FarElbow = -0.7f,
            NearHip = run * 0.95f,
            NearKnee = MathF.Max(0, -run) * 1.05f,
            FarHip = -run * 0.95f,
            FarKnee = MathF.Max(0, run) * 1.05f,
            Weapon = -0.6f,
        };
    }

    private RigPose Retreat()
    {
        // The defenceless window: back turned, running. No evasion or block — the pose has to give that
        // away so the price is understood visually too.
        float run = MathF.Sin((float)_clock * 14f);

        return new RigPose
        {
            Visible = true,
            Torso = 0.28f,
            Head = -0.35f,
            NearShoulder = 2.4f + (run * 0.7f),
            NearElbow = -0.9f,
            FarShoulder = 2.4f - (run * 0.7f),
            FarElbow = -0.9f,
            NearHip = run * 0.75f,
            NearKnee = MathF.Max(0, -run) * 0.9f,
            FarHip = -run * 0.75f,
            FarKnee = MathF.Max(0, run) * 0.9f,
            Weapon = -0.1f,
        };
    }

    private RigPose Death()
    {
        float t = Curves.Smooth(_deathLean);

        return new RigPose
        {
            Visible = true,
            RootRotation = t * 1.45f,
            Torso = 0.35f * t,
            Head = 0.5f * t,
            NearShoulder = 2.2f,
            NearElbow = -0.2f,
            FarShoulder = 2.6f,
            FarElbow = -0.2f,
            NearHip = 0.4f * t,
            FarHip = -0.25f * t,
            Weapon = -0.35f,
        };
    }

    /// <summary>The effect of permanent disabilities on the pose.</summary>
    private RigPose Injuries(RigPose pose, CombatState state)
    {
        if (_lost.Any(p => p.IsArm()))
        {
            // A warrior who has lost an arm fights one-handed with the arm he has left: the body turns
            // toward the sound side and the remaining arm comes further forward.
            pose = pose with
            {
                Torso = pose.Torso + 0.12f,
                FarShoulder = pose.FarShoulder - 0.35f,
                FarElbow = -0.55f,
            };
        }

        return _lost.Any(p => p.IsLeg()) ? OneLegged(pose, state) : pose;
    }

    /// <summary>
    /// One leg: leaning on the sound leg, the body tilted to that side.
    /// </summary>
    /// <remarks>
    /// The flee pose goes through here too. When it did not, a warrior who had lost a leg was running
    /// out of the arena on <b>two legs</b>: because the severed leg's node was not in the scene it
    /// looked like one leg on screen, but the hip hung at the last limp value and the sound leg played
    /// the normal running cycle.
    /// </remarks>
    private RigPose OneLegged(RigPose pose, CombatState state)
    {
        // There are two running states; both use the same hopping cycle.
        bool running = state is CombatState.Retreating or CombatState.Charging;
        float speed = running ? 5.4f : 2.2f;
        float reach = running ? 0.24f : 0.10f;
        float hop = MathF.Abs(MathF.Sin((float)_clock * speed)) * reach;

        return pose with
        {
            Torso = pose.Torso + 0.16f,
            HipOffsetX = 6f,
            HipOffsetY = 16f + (hop * 40f),
            NearHip = 0,
            NearKnee = 0,
            FarHip = -0.12f - hop,
            FarKnee = 0.05f + (hop * 1.6f),
        };
    }

    /// <summary>One-off reactions layered on top of the pose.</summary>
    private RigPose Reactions(RigPose pose)
    {
        if (_dodge > 0)
        {
            // Evasion costs stamina; where that cost goes has to be visible on screen.
            float t = Curves.Smooth(_dodge);
            pose = pose with
            {
                RootRotation = pose.RootRotation - (0.20f * t),
                Torso = pose.Torso - (0.28f * t),
                Head = pose.Head - (0.18f * t),
                FarHip = pose.FarHip - (0.22f * t),
            };
        }

        if (_flinch > 0)
        {
            float t = (float)_flinch;
            pose = pose with
            {
                Torso = pose.Torso + (0.22f * t),
                Head = pose.Head + (0.30f * t),
                HurtBlend = t * 0.7f,
            };
        }

        if (_catch > 0)
        {
            // Catching is the opposite of evasion: the defender goes not sideways but FORWARD, and the
            // off arm comes up to meet the incoming weapon.
            float t = Curves.Smooth(_catch);
            pose = pose with
            {
                RootRotation = pose.RootRotation + (0.12f * t),
                Torso = pose.Torso - (0.18f * t),
                FarShoulder = pose.FarShoulder + (0.85f * t),
                FarElbow = pose.FarElbow - (0.50f * t),
                NearShoulder = pose.NearShoulder + (0.30f * t),
            };
        }

        if (_stumble > 0)
        {
            // A stumble is not a shake: nobody struck, the warrior tripped over his own foot. So the
            // body goes not back but SIDEWAYS and the legs split.
            float t = Curves.Smooth(_stumble);
            pose = pose with
            {
                RootRotation = pose.RootRotation + (0.26f * t),
                Torso = pose.Torso + (0.14f * t),
                NearHip = pose.NearHip - (0.34f * t),
                NearKnee = pose.NearKnee + (0.40f * t),
                HurtBlend = Math.Max(pose.HurtBlend, t * 0.45f),
            };
        }

        return pose;
    }
}
