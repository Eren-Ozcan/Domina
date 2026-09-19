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
    private WarriorKit _kit = WarriorKit.Default;

    private readonly RigAnimator _animator = new();

    /// <summary>
    /// The body under the root: it carries the topple and the lift, the root carries the facing.
    /// </summary>
    /// <remarks>
    /// They are two nodes because they are two different things. The facing is a mirror and belongs to
    /// the figure as it stands in the scene; the topple and the lift are the pose's, and change every
    /// frame. Put on one node, a lift given to a toppled body would be measured along the topple.
    /// </remarks>
    private Node2D _body = null!;

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

    /// <summary>The name the elbow and the knee are hung under, so <see cref="Bend"/> can find them.</summary>
    private const string LowerJoint = "lower";

    public WarriorId WarriorId { get; private set; }

    public string WarriorName { get; private set; } = string.Empty;

    /// <summary>Builds the rig for a warrior of the core's. Called once.</summary>
    /// <remarks>
    /// His kit is read off him here: a man who walks out of the gate in a cuirass is drawn in one, and
    /// the weapon in his hand is the one the core says he can use.
    /// </remarks>
    public void Build(Warrior warrior, Color tint, float facing)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        Build(warrior.Id, warrior.Name, tint, facing, WarriorKit.Of(warrior));
    }

    /// <summary>
    /// Builds the rig from what a screen already knows, with no core warrior behind it.
    /// </summary>
    /// <remarks>
    /// The rig reads nothing off a <see cref="Warrior"/> but his id and his name, and a sheet that
    /// wants his figure holds a presentation row rather than the core object. Taking the two values
    /// directly is what lets the man's own page print him without the screen reaching into the core.
    /// </remarks>
    /// <param name="id">His id.</param>
    /// <param name="name">His name — the look is keyed on it.</param>
    /// <param name="tint">The side's colour.</param>
    /// <param name="facing">1 to face right, -1 to face left.</param>
    /// <param name="kit">
    /// What he is wearing and carrying. Left out, he is drawn bare with a plain sword, which is what a
    /// screen holding nothing but a name (a candidate at the stall) can honestly say about him.
    /// </param>
    public void Build(WarriorId id, string name, Color tint, float facing, WarriorKit? kit = null)
    {
        WarriorId = id;
        WarriorName = name;
        _look = WarriorLook.Of(id, name);
        _kit = kit ?? WarriorKit.Default;

        // The man's own shade is a nudge on the team tint, not a colour of his own: the side must stay
        // readable across a room, and six men on one side still have to be told apart on a sheet.
        _tint = Shade(tint, _look.Shade);
        Stature();

        // The facing: we mirror the root. The pose code always assumes "facing right", which is how
        // every pose is written in one place for both sides.
        Scale = new Vector2(facing, 1);

        _body = Joint(this, Vector2.Zero);
        _hip = Joint(_body, new Vector2(0, -_hipHeight));

        _torso = Joint(_hip, Vector2.Zero);
        Limb(_torso, -_torsoLength, 11f, Shade(0.00f));
        Sash();
        Cuirass();

        _head = Joint(_torso, new Vector2(0, -_torsoLength));
        _headShape = Circle(_head, new Vector2(0, -_headRadius), _headRadius, Shade(0.12f));
        Adorn();
        Helm();

        // The far side is drawn first; the z order is separate so the near side stays on top.
        _armFar = BuildArm(_torso, Shade(-0.22f), z: -1, out _, _kit.OffArm);
        _legFar = BuildLeg(_hip, Shade(-0.22f), z: -1, _kit.LeftLeg);
        _legNear = BuildLeg(_hip, Shade(0.06f), z: 1, _kit.RightLeg);
        _armNear = BuildArm(_torso, Shade(0.06f), z: 2, out Node2D hand, _kit.SwordArm);

        _weapon = Arm(hand);

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

    /// <summary>
    /// Puts the body into a pose that did not come from the fight's animator.
    /// </summary>
    /// <remarks>
    /// The yard's drills are the one caller (<see cref="Domina.Presentation.DrillAnimator"/>): a man at
    /// the post is not in a <see cref="CombatState"/> and has no reaction timers, so he is posed
    /// directly. The rig stays the one body — the same bones, the same kit, the same lost limbs —
    /// which is the whole reason the drills are not a second figure drawn somewhere else.
    /// </remarks>
    public void Pose(in RigPose pose) => Apply(pose);

    private void Apply(in RigPose pose)
    {
        Visible = pose.Visible;

        if (!pose.Visible)
        {
            return;
        }

        _body.Rotation = pose.RootRotation;
        _body.Position = new Vector2(0, pose.RootOffsetY);
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

    // ------------------------------------------------------------- what he wears and carries

    /// <summary>
    /// The plate colour of one step of armour, on this man's own tint.
    /// </summary>
    /// <remarks>
    /// It is derived from the side's colour rather than given a palette of its own, for the reason the
    /// sash is kept dull: the tint is the only thing that says which side a figure is on, and a kit
    /// painted in lacquer black would read as a third team across a room. What the steps differ in is
    /// <b>value</b> — cloth lifts off the body, plate sinks below it, the smith's plate sinks further
    /// and takes a lit rim — which survives being made small and being made grey.
    /// </remarks>
    private Color Plate(PlateWeight weight) => weight switch
    {
        PlateWeight.Cloth => Shade(0.30f),
        PlateWeight.Plate => Shade(-0.42f),
        PlateWeight.Heavy => Shade(-0.60f),
        _ => _tint,
    };

    /// <summary>How much wider than the bone a plate of this step is drawn.</summary>
    private static float PlateWidth(PlateWeight weight) => weight switch
    {
        PlateWeight.Cloth => 1.55f,
        PlateWeight.Plate => 2.10f,
        PlateWeight.Heavy => 2.60f,
        _ => 0f,
    };

    /// <summary>The cuirass over the torso — and the shoulder guards the smith's plate comes with.</summary>
    private void Cuirass()
    {
        if (_kit.Torso == PlateWeight.Bare)
        {
            return;
        }

        float width = 11f * _girth * PlateWidth(_kit.Torso);

        // From the waist to just under the shoulders: a plate that reached the neck would swallow the
        // head's own circle at the size a list chip draws him.
        var plate = new Line2D
        {
            Points = [new Vector2(0, -6f), new Vector2(0, -_torsoLength * 0.86f)],
            Width = width,
            DefaultColor = Plate(_kit.Torso),
            BeginCapMode = Line2D.LineCapMode.Box,
            EndCapMode = Line2D.LineCapMode.Box,
        };

        _torso.AddChild(plate);

        if (_kit.Torso != PlateWeight.Heavy)
        {
            return;
        }

        // The sode: the two boards that hang off an ō-yoroi's shoulders and are most of its silhouette.
        foreach (float side in (float[])[-1f, 1f])
        {
            var sode = new Line2D
            {
                Points =
                [
                    new Vector2(side * width * 0.42f, -_shoulderDrop + 4f),
                    new Vector2(side * width * 0.82f, -_shoulderDrop + 30f),
                ],
                Width = 13f * _girth,
                DefaultColor = Plate(PlateWeight.Plate),
                BeginCapMode = Line2D.LineCapMode.Box,
                EndCapMode = Line2D.LineCapMode.Box,
                ZIndex = 3,
            };

            _torso.AddChild(sode);
        }
    }

    /// <summary>The kabuto: a bowl over the head, with the neck guard flaring behind it.</summary>
    private void Helm()
    {
        if (_kit.Head == PlateWeight.Bare)
        {
            return;
        }

        float r = _headRadius;
        Color steel = Plate(_kit.Head);

        var bowl = new Line2D
        {
            Points =
            [
                new Vector2(-r * 1.02f, -r * 1.15f),
                new Vector2(-r * 0.55f, -r * 1.95f),
                new Vector2(r * 0.45f, -r * 1.95f),
                new Vector2(r * 0.95f, -r * 1.25f),
            ],
            Width = r * 0.70f,
            DefaultColor = steel,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            JointMode = Line2D.LineJointMode.Round,
            ZIndex = 2,
        };

        _head.AddChild(bowl);

        // The shikoro, hung off the back of the bowl — the part that says helmet and not hat.
        var neck = new Line2D
        {
            Points = [new Vector2(-r * 1.05f, -r * 1.25f), new Vector2(-r * 1.55f, -r * 0.35f)],
            Width = r * 0.55f,
            DefaultColor = steel,
            BeginCapMode = Line2D.LineCapMode.Box,
            EndCapMode = Line2D.LineCapMode.Box,
            ZIndex = -1,
        };

        _head.AddChild(neck);
    }

    /// <summary>A sleeve or a greave laid over the bone under it.</summary>
    /// <remarks>
    /// It is hung on the same joint as the limb's own drawing and covers the upper segment only, so it
    /// bends with the arm and goes nowhere near the part list the animations run on. A piece drawn
    /// across a joint would tear open the first time the elbow bent.
    /// </remarks>
    private void Piece(Node2D bone, float length, float width, PlateWeight weight)
    {
        if (weight == PlateWeight.Bare)
        {
            return;
        }

        var piece = new Line2D
        {
            Points = [new Vector2(0, length * 0.16f), new Vector2(0, length * 0.92f)],
            Width = width * _girth * PlateWidth(weight),
            DefaultColor = Plate(weight),
            BeginCapMode = Line2D.LineCapMode.Box,
            EndCapMode = Line2D.LineCapMode.Box,
            ZIndex = 1,
        };

        bone.AddChild(piece);
    }

    /// <summary>
    /// Puts the thing he fights with in his hand.
    /// </summary>
    /// <remarks>
    /// The weapon is one node hung off the near hand however long it is, so the pose's single
    /// <see cref="RigPose.Weapon"/> angle still drives it and severing the sword arm still takes it
    /// with the chain. What the shape changes is the drawing, not the rig.
    /// </remarks>
    /// <returns>The weapon joint, or <c>null</c> when his hands are empty.</returns>
    private Node2D? Arm(Node2D hand)
    {
        if (_kit.Weapon == WeaponShape.Unarmed)
        {
            return null;
        }

        Node2D weapon = Joint(hand, new Vector2(0, _hand));
        Color steel = new(0.85f, 0.85f, 0.90f);
        Color wood = new(0.36f, 0.27f, 0.19f);

        switch (_kit.Weapon)
        {
            case WeaponShape.ShortBlade:
                Grip(weapon, 12f, wood);
                Limb(weapon, 34f, 5f, steel);
                break;

            case WeaponShape.LongBlade:
                Grip(weapon, 26f, wood);
                Tsuba(weapon);
                Limb(weapon, 112f, 7f, steel);
                break;

            case WeaponShape.Spear:
                // The haft is most of it and the head is the last handspan — the reach is the weapon.
                Limb(weapon, 108f, 6f, wood);
                Tip(weapon, 108f, 26f, steel, 9f);
                break;

            case WeaponShape.Club:
                Limb(weapon, 92f, 8f, wood);
                Tip(weapon, 62f, 34f, Shade(-0.30f), 17f);
                break;

            case WeaponShape.Hook:
                Grip(weapon, 14f, wood);
                Limb(weapon, 44f, 6f, steel);

                // The prong off the guard: the whole of why a jitte is carried.
                var prong = new Line2D
                {
                    Points = [new Vector2(0, 18f), new Vector2(11f, 30f)],
                    Width = 5f,
                    DefaultColor = steel,
                    BeginCapMode = Line2D.LineCapMode.Round,
                    EndCapMode = Line2D.LineCapMode.Round,
                };
                weapon.AddChild(prong);
                break;

            default:
                Grip(weapon, 18f, wood);
                Tsuba(weapon);
                Limb(weapon, WeaponLength, 6f, steel);
                break;
        }

        return weapon;
    }

    /// <summary>The bound handle at the weapon's root, drawn back over the hand.</summary>
    private static void Grip(Node2D weapon, float length, Color wood)
    {
        var grip = new Line2D
        {
            Points = [new Vector2(0, -length), Vector2.Zero],
            Width = 8f,
            DefaultColor = wood,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };

        weapon.AddChild(grip);
    }

    /// <summary>The tsuba: the disc between the grip and the blade.</summary>
    private static void Tsuba(Node2D weapon)
    {
        var guard = new Line2D
        {
            Points = [new Vector2(-8f, 0), new Vector2(8f, 0)],
            Width = 5f,
            DefaultColor = new Color(0.42f, 0.36f, 0.26f),
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };

        weapon.AddChild(guard);
    }

    /// <summary>The business end of a hafted weapon — a spear's point, a club's weight.</summary>
    private static void Tip(Node2D weapon, float tip, float length, Color colour, float width)
    {
        var head = new Line2D
        {
            Points = [new Vector2(0, tip - length), new Vector2(0, tip)],
            Width = width,
            DefaultColor = colour,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };

        weapon.AddChild(head);
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

    /// <remarks>
    /// The order the pieces are hung in is free: <see cref="Bend"/> finds the lower joint by the name
    /// <c>lower</c>, not by its place among the children, so a drawing added to the chain cannot
    /// silently stop the elbow bending.
    /// </remarks>
    /// <param name="hand">The hand at the end of the chain; the weapon hangs off it.</param>
    private Node2D BuildArm(Node2D parent, Color color, int z, out Node2D hand, PlateWeight guard = PlateWeight.Bare)
    {
        Node2D upper = Joint(parent, new Vector2(0, -_shoulderDrop));
        upper.ZIndex = z;
        Limb(upper, _upperArm, 8f, color);

        Node2D fore = Joint(upper, new Vector2(0, _upperArm), LowerJoint);
        Limb(fore, _forearm, 7f, color);

        hand = Joint(fore, new Vector2(0, _forearm));
        Limb(hand, _hand, 9f, color);

        Piece(upper, _upperArm, 8f, guard);

        // The kote is a sleeve down the forearm as much as the upper arm: drawn on the upper alone it
        // reads as a shoulder pad, which is the one Japanese piece it is not.
        Piece(fore, _forearm, 7f, guard);

        return upper;
    }

    private Node2D BuildLeg(Node2D parent, Color color, int z, PlateWeight greave = PlateWeight.Bare)
    {
        Node2D thigh = Joint(parent, Vector2.Zero);
        thigh.ZIndex = z;
        Limb(thigh, _thigh, 10f, color);

        Node2D shin = Joint(thigh, new Vector2(0, _thigh), LowerJoint);
        Limb(shin, _shin, 9f, color);

        Node2D foot = Joint(shin, new Vector2(0, _shin));
        Limb(foot, _foot, 8f, color, horizontal: true);

        // The suneate covers the shin and nothing above the knee, which is what it is named for.
        Piece(shin, _shin, 9f, greave);

        return thigh;
    }

    /// <param name="name">
    /// A name for the joint when something has to find it again later — <see cref="Bend"/> looks up
    /// <c>lower</c>. Naming it keeps the lookup off the child index, so a drawing can be hung anywhere
    /// in the chain without moving the elbow.
    /// </param>
    private static Node2D Joint(Node2D parent, Vector2 offset, string? name = null)
    {
        var joint = new Node2D { Position = offset };
        if (name is not null)
        {
            joint.Name = name;
        }

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
        if (limb.GetNodeOrNull<Node2D>(LowerJoint) is { } joint)
        {
            joint.Rotation = lower;
        }
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
