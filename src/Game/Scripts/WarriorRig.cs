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

    /// <summary>
    /// The sashes a man may be wearing — <see cref="WarriorLook.Accent"/> indexes into this.
    /// </summary>
    /// <remarks>
    /// They are dyes a village could make, kept dull on purpose: the team tint is what says which side
    /// a figure is on, and a bright sash would read as a second team colour across a room.
    /// </remarks>
    private static readonly Color[] Accents =
    [
        new(0.48f, 0.16f, 0.14f),
        new(0.18f, 0.26f, 0.40f),
        new(0.30f, 0.34f, 0.20f),
        new(0.44f, 0.36f, 0.16f),
        new(0.34f, 0.22f, 0.34f),
        new(0.22f, 0.20f, 0.18f),
    ];

    // ---- The same proportions, put through this man's own build (see WarriorLook) ----
    private float _hipHeight = HipHeight;
    private float _torsoLength = TorsoLength;
    private float _headRadius = HeadRadius;
    private float _shoulderDrop = ShoulderDrop;
    private float _upperArm = UpperArm;
    private float _forearm = Forearm;
    private float _hand = Hand;
    private float _thigh = Thigh;
    private float _shin = Shin;
    private float _foot = Foot;
    private float _girth = 1f;

    private WarriorLook _look = WarriorLook.Of(default, string.Empty);

    private readonly RigAnimator _animator = new();

    private Node2D _hip = null!;
    private Node2D _torso = null!;
    private Node2D _head = null!;
    private Node2D? _armFar;
    private Node2D? _legFar;
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

    /// <summary>Builds the rig for a warrior of the core's. Called once.</summary>
    public void Build(Warrior warrior, Color tint, float facing)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        Build(warrior.Id, warrior.Name, tint, facing);
    }

    /// <summary>
    /// Builds the rig from what a screen already knows, with no core warrior behind it.
    /// </summary>
    /// <remarks>
    /// The rig reads nothing off a <see cref="Warrior"/> but his id and his name, and a sheet that
    /// wants his figure holds a presentation row rather than the core object. Taking the two values
    /// directly is what lets the man's own page print him without the screen reaching into the core.
    /// </remarks>
    public void Build(WarriorId id, string name, Color tint, float facing)
    {
        WarriorId = id;
        WarriorName = name;
        _look = WarriorLook.Of(id, name);

        // The man's own shade is a nudge on the team tint, not a colour of his own: the side must stay
        // readable across a room, and six men on one side still have to be told apart on a sheet.
        _tint = Shade(tint, _look.Shade);
        Stature();

        // The facing: we mirror the root. The pose code always assumes "facing right", which is how
        // every pose is written in one place for both sides.
        Scale = new Vector2(facing, 1);

        _hip = Joint(this, new Vector2(0, -_hipHeight));

        _torso = Joint(_hip, Vector2.Zero);
        Limb(_torso, -_torsoLength, 11f, Shade(0.00f));
        Sash();

        _head = Joint(_torso, new Vector2(0, -_torsoLength));
        _headShape = Circle(_head, new Vector2(0, -_headRadius), _headRadius, Shade(0.12f));
        Adorn();

        // The far side is drawn first; the z order is separate so the near side stays on top.
        _armFar = BuildArm(_torso, Shade(-0.22f), z: -1);
        _legFar = BuildLeg(_hip, Shade(-0.22f), z: -1);
        _legNear = BuildLeg(_hip, Shade(0.06f), z: 1);
        _armNear = BuildArm(_torso, Shade(0.06f), z: 2);

        Node2D hand = _armNear.GetChild<Node2D>(1).GetChild<Node2D>(1);
        _weapon = Joint(hand, new Vector2(0, _hand));
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

    /// <summary>
    /// Poses the rig as a portrait: standing still, with what he has already lost gone.
    /// </summary>
    /// <remarks>
    /// This is the fight's waiting pose read once, at the clock's zero, so the figure on a sheet is
    /// the same body the arena draws and not a second drawing of the same man. What a limb loss does
    /// here is <b>quiet</b>: the chain is removed, with no blood and nothing falling. The arena's
    /// <see cref="React"/> is the moment the limb comes off; a portrait is the day after.
    /// </remarks>
    /// <param name="lost">The limbs already gone. An eye is marked on the head, not detached.</param>
    public void Stand(BodyPartSet lost)
    {
        foreach (BodyPart part in lost.Parts())
        {
            Strip(part);
        }

        Apply(_animator.Advance(CombatState.Idle, 0, 0));
    }

    /// <summary>Removes a limb the man lost before this drawing — no blood, nothing falls.</summary>
    /// <remarks>
    /// The rig is built with a near and a far side, so the four limbs map onto the two sides: the
    /// sword arm and the right leg are the near ones, the off arm and the left leg the far ones.
    /// The weapon hangs off the near hand, so losing the sword arm takes the weapon with it — the
    /// same result as <see cref="Warrior.UsableWeapon"/> in the core.
    /// </remarks>
    private void Strip(BodyPart part)
    {
        switch (part)
        {
            case BodyPart.SwordArm:
                Discard(_armNear);
                _armNear = null;
                _weapon = null;
                break;
            case BodyPart.OffArm:
                Discard(_armFar);
                _armFar = null;
                break;
            case BodyPart.RightLeg:
                Discard(_legNear);
                _legNear = null;
                break;
            case BodyPart.LeftLeg:
                Discard(_legFar);
                _legFar = null;
                break;
            case BodyPart.Eye:
                Blinded();
                break;
            default:
                break;
        }
    }

    private static void Discard(Node2D? limb)
    {
        if (limb is null)
        {
            return;
        }

        limb.GetParent().RemoveChild(limb);
        limb.QueueFree();
    }

    /// <summary>The bar over the eye — the one loss that takes nothing off the body.</summary>
    private void Blinded()
    {
        var bar = new Line2D
        {
            Points = [new Vector2(-_headRadius, -_headRadius), new Vector2(_headRadius, -_headRadius)],
            Width = 7f,
            DefaultColor = Shade(-0.55f),
        };

        _head.AddChild(bar);
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
        _hip.Position = new Vector2(pose.HipOffsetX, -_hipHeight + pose.HipOffsetY);
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

    // ------------------------------------------------------------- this man's own face

    /// <summary>The height of the standing figure, root at foot level — his own, not the rig's.</summary>
    /// <remarks>
    /// A sheet that fits the figure to a plate needs the height of <b>this</b> man: the builds differ
    /// by a few per cent and a panel scaled to the locked 256 would crop the tall ones.
    /// </remarks>
    public float StandingHeight => _hipHeight + _torsoLength + (_headRadius * 2);

    /// <summary>How high his hip sits above his feet — where a bust crop cuts him off.</summary>
    public float HipLine => _hipHeight;

    /// <summary>How high his shoulders sit above his feet — where a head crop cuts him off.</summary>
    public float ShoulderLine => _hipHeight + _shoulderDrop;

    /// <summary>Puts the locked proportions through this man's build.</summary>
    /// <remarks>
    /// The stature multiplies the bones and the girth the drawing's width, so the skeleton is untouched
    /// and every pose still lands. The hip is the thigh plus the shin by construction; scaling both by
    /// the same number is what keeps his feet on the ground.
    /// </remarks>
    private void Stature()
    {
        float tall = _look.Height;

        _hipHeight = HipHeight * tall;
        _torsoLength = TorsoLength * tall;
        _shoulderDrop = ShoulderDrop * tall;
        _upperArm = UpperArm * tall;
        _forearm = Forearm * tall;
        _thigh = Thigh * tall;
        _shin = Shin * tall;
        _headRadius = HeadRadius * _look.HeadSize;
        _girth = _look.Girth;
    }

    /// <summary>The sash at his waist — the one piece of colour that is his and not his side's.</summary>
    private void Sash()
    {
        float half = 10f * _girth;
        var sash = new Line2D
        {
            Points = [new Vector2(-half, -8), new Vector2(half, -8)],
            Width = 7f,
            DefaultColor = Accents[_look.Accent % Accents.Length],
            ZIndex = 1,
        };

        _torso.AddChild(sash);
    }

    /// <summary>
    /// Hair, beard, band and scar — what tells one head from the next at a glance.
    /// </summary>
    /// <remarks>
    /// Everything is hung on the head joint, so it turns with the head and goes nowhere near the part
    /// list the animations run on. The figure always faces right in its own coordinates (the root
    /// carries the mirror), so +x is his face and -x is the back of his head.
    /// </remarks>
    private void Adorn()
    {
        float r = _headRadius;
        Color hair = Shade(-0.62f);

        switch (_look.Hair)
        {
            case HairStyle.Topknot:
                Stroke(new Vector2(-r * 0.15f, -r * 1.95f), new Vector2(-r * 1.05f, -r * 1.55f), 9f, hair, behind: true);
                Circle(_head, new Vector2(-r * 1.15f, -r * 1.45f), r * 0.22f, hair).ZIndex = -1;
                break;
            case HairStyle.Bun:
                Circle(_head, new Vector2(-r * 0.85f, -r * 1.75f), r * 0.42f, hair).ZIndex = -1;
                Stroke(new Vector2(-r * 0.2f, -r * 1.9f), new Vector2(-r * 0.75f, -r * 1.75f), 10f, hair, behind: true);
                break;
            case HairStyle.Loose:
                Stroke(new Vector2(-r * 0.35f, -r * 1.85f), new Vector2(-r * 0.9f, r * 0.55f), 13f, hair, behind: true);
                break;
            case HairStyle.Wild:
                Stroke(new Vector2(-r * 0.3f, -r * 1.8f), new Vector2(-r * 1.25f, -r * 2.15f), 7f, hair, behind: true);
                Stroke(new Vector2(0f, -r * 2f), new Vector2(r * 0.15f, -r * 2.75f), 7f, hair, behind: true);
                Stroke(new Vector2(-r * 0.5f, -r * 1.7f), new Vector2(-r * 1.15f, -r * 0.55f), 9f, hair, behind: true);
                break;
            case HairStyle.Shaved:
            default:
                break;
        }

        switch (_look.Beard)
        {
            case BeardStyle.Moustache:
                Stroke(new Vector2(r * 0.1f, -r * 0.72f), new Vector2(r * 0.8f, -r * 0.66f), 6f, hair);
                break;
            case BeardStyle.Stubble:
                Stroke(new Vector2(r * 0.15f, -r * 0.18f), new Vector2(r * 0.62f, -r * 0.42f), 9f, hair);
                break;
            case BeardStyle.Full:
                Stroke(new Vector2(-r * 0.25f, -r * 0.35f), new Vector2(r * 0.55f, -r * 0.2f), 13f, hair);
                Stroke(new Vector2(r * 0.1f, -r * 0.75f), new Vector2(r * 0.8f, -r * 0.68f), 6f, hair);
                break;
            case BeardStyle.None:
            default:
                break;
        }

        if (_look.Headband)
        {
            Color band = Accents[_look.Accent % Accents.Length];
            Stroke(new Vector2(-r * 0.95f, -r * 1.35f), new Vector2(r * 0.95f, -r * 1.3f), 8f, band);
            Stroke(new Vector2(-r * 0.9f, -r * 1.33f), new Vector2(-r * 1.75f, -r * 0.95f), 6f, band);
        }

        if (_look.Scar)
        {
            Stroke(new Vector2(r * 0.3f, -r * 1.45f), new Vector2(r * 0.72f, -r * 0.7f), 4f, Shade(0.40f));
        }
    }

    /// <summary>A mark drawn straight on the head, in the head's own coordinates.</summary>
    /// <param name="behind">
    /// Hair sits behind the head and everything worn on the face in front of it. Without the split the
    /// beard and the band are swallowed by the head's own circle and every man reads as bald.
    /// </param>
    private void Stroke(Vector2 from, Vector2 to, float width, Color color, bool behind = false)
    {
        var line = new Line2D
        {
            Points = [from, to],
            Width = width,
            DefaultColor = color,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            ZIndex = behind ? -1 : 1,
        };

        _head.AddChild(line);
    }

    // ------------------------------------------------------------- rig kurulumu

    private Node2D BuildArm(Node2D parent, Color color, int z)
    {
        Node2D upper = Joint(parent, new Vector2(0, -_shoulderDrop));
        upper.ZIndex = z;
        Limb(upper, _upperArm, 8f, color);

        Node2D fore = Joint(upper, new Vector2(0, _upperArm));
        Limb(fore, _forearm, 7f, color);

        Node2D hand = Joint(fore, new Vector2(0, _forearm));
        Limb(hand, _hand, 9f, color);

        return upper;
    }

    private Node2D BuildLeg(Node2D parent, Color color, int z)
    {
        Node2D thigh = Joint(parent, Vector2.Zero);
        thigh.ZIndex = z;
        Limb(thigh, _thigh, 10f, color);

        Node2D shin = Joint(thigh, new Vector2(0, _thigh));
        Limb(shin, _shin, 9f, color);

        Node2D foot = Joint(shin, new Vector2(0, _shin));
        Limb(foot, _foot, 8f, color, horizontal: true);

        return thigh;
    }

    private static Node2D Joint(Node2D parent, Vector2 offset)
    {
        var joint = new Node2D { Position = offset };
        parent.AddChild(joint);
        return joint;
    }

    /// <summary>The drawing hung on the bone. The only place that will change when art arrives.</summary>
    private void Limb(Node2D bone, float length, float width, Color color, bool horizontal = false)
    {
        var line = new Line2D
        {
            Points = [Vector2.Zero, horizontal ? new Vector2(length, 0) : new Vector2(0, length)],
            Width = width * _girth,
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

    private Color Shade(float amount) => Shade(_tint, amount);

    private static Color Shade(Color color, float amount) =>
        amount >= 0 ? color.Lerp(Colors.White, amount) : color.Lerp(Colors.Black, -amount);

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
