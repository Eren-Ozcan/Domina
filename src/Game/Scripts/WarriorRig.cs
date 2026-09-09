using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// A warrior's visual counterpart in the scene — for now a stickman of coloured bars.
/// </summary>
/// <remarks>
/// <para>
/// <b>The visuals are temporary, the skeleton is not.</b> The part list and joint points here are
/// phase 2's locked rig: the animations hang on this structure. When the art changes later, the drawing
/// hung on each bone changes and the hierarchy stays the same. If the part list changes, all the
/// animations are redone — which is why adding a new bone is not a cheap job.
/// </para>
/// <para>
/// The severing points are the <b>shoulder</b> and the <b>hip</b>: when an arm comes off the upper-arm
/// node is detached, when a leg comes off the thigh node is, and
/// everything below it goes with it. This is the equivalent of GDD §2's sentence "limb loss =
/// detaching a node at runtime"; no new art asset is needed.
/// </para>
/// <para>
/// <b>This class does not compute poses.</b> The angles are produced by <see cref="RigAnimator"/> (engine-free,
/// tested); the job left here is building the nodes and applying the angles that arrive.
/// </para>
/// </remarks>
public sealed partial class WarriorRig : Node2D
{
    // ---- Locked proportions (total height 256 px, the root at foot level) ----
    private const float HipHeight = 124f;
    private const float TorsoLength = 84f;
    private const float HeadRadius = 24f;
    private const float ShoulderDrop = 74f;
    private const float UpperArm = 48f;
    private const float Forearm = 46f;
    private const float Hand = 12f;
    private const float Thigh = 62f;
    private const float Shin = 62f;
    private const float Foot = 14f;
    private const float WeaponLength = 70f;

    private static readonly Color HurtColor = new(1f, 0.55f, 0.55f);
    private static readonly Color BloodColor = new(0.65f, 0.06f, 0.06f);

    private readonly RigAnimator _animator = new();

    private Node2D _hip = null!;
    private Node2D _torso = null!;
    private Node2D _head = null!;
    private Node2D _armFar = null!;
    private Node2D _legFar = null!;
    private Polygon2D _headShape = null!;

    // The severable chains: after a limb is detached these nodes no longer belong to the rig.
    // Clearing the reference is required — otherwise the pose is applied every frame to the limb on the
    // ground too, and the severed arm keeps fighting while lying at the bottom of the scene.
    private Node2D? _armNear;
    private Node2D? _legNear;
    private Node2D? _weapon;

    private Color _tint = Colors.White;

    public WarriorId WarriorId { get; private set; }

    public string WarriorName { get; private set; } = string.Empty;

    /// <summary>Builds the rig. Called once.</summary>
    public void Build(Warrior warrior, Color tint, float facing)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        WarriorId = warrior.Id;
        WarriorName = warrior.Name;
        _tint = tint;

        // The facing: we mirror the root. The pose code always assumes "facing right", which is how
        // every pose is written in one place for both sides.
        Scale = new Vector2(facing, 1);

        _hip = Joint(this, new Vector2(0, -HipHeight));

        _torso = Joint(_hip, Vector2.Zero);
        Limb(_torso, -TorsoLength, 11f, Shade(0.00f));

        _head = Joint(_torso, new Vector2(0, -TorsoLength));
        _headShape = Circle(_head, new Vector2(0, -HeadRadius), HeadRadius, Shade(0.12f));

        // The far side is drawn first; the z order is separate so the near side stays on top.
        _armFar = BuildArm(_torso, Shade(-0.22f), z: -1);
        _legFar = BuildLeg(_hip, Shade(-0.22f), z: -1);
        _legNear = BuildLeg(_hip, Shade(0.06f), z: 1);
        _armNear = BuildArm(_torso, Shade(0.06f), z: 2);

        Node2D hand = _armNear.GetChild<Node2D>(1).GetChild<Node2D>(1);
        _weapon = Joint(hand, new Vector2(0, Hand));
        Limb(_weapon, WeaponLength, 6f, new Color(0.85f, 0.85f, 0.90f));

        Apply(_animator.Advance(CombatState.Idle, 0, 0));
    }

    /// <summary>Processes a one-off visual reaction (a hit, an evasion, limb loss, death).</summary>
    public void React(in RigReaction reaction)
    {
        if (_animator.React(reaction) is BodyPart severed)
        {
            Sever(severed);
        }
    }

    /// <summary>Turns the fight's current state into a pose and applies it to the nodes.</summary>
    /// <param name="state">The state reported by the core.</param>
    /// <param name="phase">How far the state has progressed (0-1).</param>
    /// <param name="delta">The time elapsed.</param>
    public void Advance(CombatState state, double phase, double delta) =>
        Apply(_animator.Advance(state, phase, delta));

    private void Apply(in RigPose pose)
    {
        Visible = pose.Visible;

        if (!pose.Visible)
        {
            return;
        }

        Rotation = pose.RootRotation;
        _hip.Position = new Vector2(pose.HipOffsetX, -HipHeight + pose.HipOffsetY);
        _torso.Rotation = pose.Torso;
        _head.Rotation = pose.Head;

        Bend(_armNear, pose.NearShoulder, pose.NearElbow);
        Bend(_armFar, pose.FarShoulder, pose.FarElbow);
        Bend(_legNear, pose.NearHip, pose.NearKnee);
        Bend(_legFar, pose.FarHip, pose.FarKnee);

        if (_weapon is not null)
        {
            _weapon.Rotation = pose.Weapon;
        }

        // The colour is already given to each limb individually; the modulate here is only the pain
        // flash. Multiplying by the team colour would darken the limbs a second time.
        Modulate = pose.HurtBlend > 0 ? Colors.White.Lerp(HurtColor, pose.HurtBlend) : Colors.White;
    }

    /// <summary>
    /// Detaches the limb permanently and drops the fallen piece into the scene.
    /// </summary>
    /// <remarks>
    /// A severed limb is not hidden, it is <b>detached</b>: the whole chain below it (forearm, hand,
    /// weapon) goes with it. A warrior who loses an arm losing his weapon too therefore comes free in
    /// the visuals — the same result as the
    /// <see cref="Warrior.UsableWeapon"/> rule in the core.
    /// </remarks>
    private void Sever(BodyPart part)
    {
        if (part.IsArm())
        {
            DropLimb(_armNear);
            _armNear = null;
            _weapon = null;
        }
        else if (part.IsLeg())
        {
            DropLimb(_legNear);
            _legNear = null;
        }
        else
        {
            _headShape.Color = new Color(0.75f, 0.15f, 0.15f);
        }

        Splatter(part.IsLeg() ? _hip : _torso);
    }

    // ------------------------------------------------------------- rig kurulumu

    private static Node2D BuildArm(Node2D parent, Color color, int z)
    {
        Node2D upper = Joint(parent, new Vector2(0, -ShoulderDrop));
        upper.ZIndex = z;
        Limb(upper, UpperArm, 8f, color);

        Node2D fore = Joint(upper, new Vector2(0, UpperArm));
        Limb(fore, Forearm, 7f, color);

        Node2D hand = Joint(fore, new Vector2(0, Forearm));
        Limb(hand, Hand, 9f, color);

        return upper;
    }

    private static Node2D BuildLeg(Node2D parent, Color color, int z)
    {
        Node2D thigh = Joint(parent, Vector2.Zero);
        thigh.ZIndex = z;
        Limb(thigh, Thigh, 10f, color);

        Node2D shin = Joint(thigh, new Vector2(0, Thigh));
        Limb(shin, Shin, 9f, color);

        Node2D foot = Joint(shin, new Vector2(0, Shin));
        Limb(foot, Foot, 8f, color, horizontal: true);

        return thigh;
    }

    private static Node2D Joint(Node2D parent, Vector2 offset)
    {
        var joint = new Node2D { Position = offset };
        parent.AddChild(joint);
        return joint;
    }

    /// <summary>The drawing hung on the bone. The only place that will change when art arrives.</summary>
    private static void Limb(Node2D bone, float length, float width, Color color, bool horizontal = false)
    {
        var line = new Line2D
        {
            Points = [Vector2.Zero, horizontal ? new Vector2(length, 0) : new Vector2(0, length)],
            Width = width,
            DefaultColor = color,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };

        bone.AddChild(line);
    }

    private static Polygon2D Circle(Node2D parent, Vector2 center, float radius, Color color)
    {
        var points = new Vector2[20];
        for (int i = 0; i < points.Length; i++)
        {
            float a = Mathf.Tau * i / points.Length;
            points[i] = center + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }

        var polygon = new Polygon2D { Polygon = points, Color = color };
        parent.AddChild(polygon);
        return polygon;
    }

    /// <summary>Builds the upper and lower joint together (shoulder + elbow, hip + knee).</summary>
    /// <remarks>If the chain is severed the node no longer belongs to the rig; it is silently skipped.</remarks>
    private static void Bend(Node2D? limb, float upper, float lower)
    {
        if (limb is null)
        {
            return;
        }

        limb.Rotation = upper;
        limb.GetChild<Node2D>(1).Rotation = lower;
    }

    private Color Shade(float amount) =>
        amount >= 0 ? _tint.Lerp(Colors.White, amount) : _tint.Lerp(Colors.Black, -amount);

    // ------------------------------------------------------------- limb severing

    /// <summary>Detaches the severed limb from the rig and drops it into the scene.</summary>
    private void DropLimb(Node2D? limb)
    {
        if (limb is null)
        {
            return;
        }

        Vector2 worldPosition = limb.GlobalPosition;
        float worldRotation = limb.GlobalRotation;

        limb.GetParent().RemoveChild(limb);
        GetParent().AddChild(limb);

        limb.GlobalPosition = worldPosition;
        limb.GlobalRotation = worldRotation;
        limb.Scale = Scale;

        // The ground is the height of the warrior's own root: the limb falls there.
        limb.AddChild(new FallingLimb { GroundY = Position.Y });
    }

    private static void Splatter(Node2D at)
    {
        for (int i = 0; i < 7; i++)
        {
            float angle = Mathf.Tau * i / 7f;
            var drop = new Line2D
            {
                Points = [Vector2.Zero, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 22f],
                Width = 5f,
                DefaultColor = BloodColor,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round,
            };

            at.AddChild(drop);
        }
    }
}

/// <summary>A severed limb falling to the ground. Purely cosmetic — the core knows nothing of it.</summary>
public sealed partial class FallingLimb : Node
{
    private float _velocity = -180f;
    private float _spin = 4.5f;

    /// <summary>The height the limb comes to rest at — the scene's ground line.</summary>
    public float GroundY { get; init; }

    public override void _Process(double delta)
    {
        if (GetParent() is not Node2D limb)
        {
            return;
        }

        _velocity += (float)delta * 900f;
        limb.Position += new Vector2(-40f * (float)delta, _velocity * (float)delta);
        limb.Rotation += _spin * (float)delta;

        if (limb.Position.Y < GroundY)
        {
            return;
        }

        limb.Position = new Vector2(limb.Position.X, GroundY);
        _velocity = 0;
        _spin = 0;
        SetProcess(false);
    }
}
