using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// Bir savaşçının sahnedeki görsel karşılığı — şimdilik renkli çubuklardan bir stickman.
/// </summary>
/// <remarks>
/// <para>
/// <b>Görsel geçici, iskelet değil.</b> Buradaki parça listesi ve eklem noktaları
/// Faz 2'nin kilitlenmiş rig'idir: animasyonlar bu yapıya bağlanır. Sanat sonradan
/// değiştiğinde her kemiğe asılı çizim değişir, hiyerarşi aynı kalır. Parça listesi
/// değişirse animasyonların tamamı yeniden yapılır — bu yüzden yeni kemik eklemek
/// ucuz bir iş değildir.
/// </para>
/// <para>
/// Kopma noktaları <b>omuz</b> ve <b>kalça</b>: <see cref="BodyPart.Arm"/> geldiğinde
/// üst kol düğümü, <see cref="BodyPart.Leg"/> geldiğinde uyluk düğümü ayrılır ve
/// altındaki her şey onunla birlikte gider. GDD §2'nin "uzuv kopması = çalışma anında
/// bir node'u ayırmak" cümlesinin karşılığı budur; yeni sanat varlığı gerekmez.
/// </para>
/// <para>
/// <b>Bu sınıf duruş hesaplamaz.</b> Açıları <see cref="RigAnimator"/> üretir (motorsuz,
/// testli); burada kalan iş düğümleri kurmak ve gelen açıları uygulamaktır.
/// </para>
/// </remarks>
public sealed partial class WarriorRig : Node2D
{
    // ---- Kilitlenmiş oranlar (toplam yükseklik 256 px, kök ayak hizasında) ----
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

    // Kopabilen zincirler: uzuv ayrıldıktan sonra bu düğümler artık rig'in değildir.
    // Referansın boşaltılması şart — aksi hâlde duruş her karede yerdeki uzva da
    // uygulanır ve kopan kol, sahnenin dibinde yatarken savaşmaya devam eder.
    private Node2D? _armNear;
    private Node2D? _legNear;
    private Node2D? _weapon;

    private Color _tint = Colors.White;

    public WarriorId WarriorId { get; private set; }

    public string WarriorName { get; private set; } = string.Empty;

    /// <summary>Rig'i kurar. Bir kez çağrılır.</summary>
    public void Build(Warrior warrior, Color tint, float facing)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        WarriorId = warrior.Id;
        WarriorName = warrior.Name;
        _tint = tint;

        // Yön: kökü aynalıyoruz. Duruş kodu daima "sağa bakıyor" varsayar; bu sayede
        // her poz iki taraf için de tek yerde yazılır.
        Scale = new Vector2(facing, 1);

        _hip = Joint(this, new Vector2(0, -HipHeight));

        _torso = Joint(_hip, Vector2.Zero);
        Limb(_torso, -TorsoLength, 11f, Shade(0.00f));

        _head = Joint(_torso, new Vector2(0, -TorsoLength));
        _headShape = Circle(_head, new Vector2(0, -HeadRadius), HeadRadius, Shade(0.12f));

        // Uzak taraf önce çizilir; yakın taraf üstte kalsın diye z sırası ayrı.
        _armFar = BuildArm(_torso, Shade(-0.22f), z: -1);
        _legFar = BuildLeg(_hip, Shade(-0.22f), z: -1);
        _legNear = BuildLeg(_hip, Shade(0.06f), z: 1);
        _armNear = BuildArm(_torso, Shade(0.06f), z: 2);

        Node2D hand = _armNear.GetChild<Node2D>(1).GetChild<Node2D>(1);
        _weapon = Joint(hand, new Vector2(0, Hand));
        Limb(_weapon, WeaponLength, 6f, new Color(0.85f, 0.85f, 0.90f));

        Apply(_animator.Advance(CombatState.Idle, 0, 0));
    }

    /// <summary>Tek seferlik görsel tepkiyi işler (vuruş, kaçınma, uzuv kaybı, ölüm).</summary>
    public void React(in RigReaction reaction)
    {
        if (_animator.React(reaction) is BodyPart severed)
        {
            Sever(severed);
        }
    }

    /// <summary>Dövüşün o anki halini duruşa çevirip düğümlere uygular.</summary>
    /// <param name="state">Çekirdeğin bildirdiği durum.</param>
    /// <param name="phase">Durumun tamamlanma oranı (0-1).</param>
    /// <param name="delta">Geçen süre.</param>
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

        // Renk zaten her uzva tek tek verilmiş durumda; buradaki modulate yalnızca acı
        // parlamasıdır. Takım rengiyle çarpmak uzuvları ikinci kez karartırdı.
        Modulate = pose.HurtBlend > 0 ? Colors.White.Lerp(HurtColor, pose.HurtBlend) : Colors.White;
    }

    /// <summary>
    /// Uzvu kalıcı olarak ayırır ve düşen parçayı sahneye bırakır.
    /// </summary>
    /// <remarks>
    /// Kopan uzuv gizlenmiyor, <b>ayrılıyor</b>: altındaki tüm zincir (ön kol, el,
    /// silah) onunla birlikte gidiyor. Kolunu kaybeden savaşçının silahını da
    /// kaybetmesi böylece görselde bedava geliyor — çekirdekteki
    /// <see cref="Warrior.UsableWeapon"/> kuralıyla aynı sonuç.
    /// </remarks>
    private void Sever(BodyPart part)
    {
        switch (part)
        {
            case BodyPart.Arm:
                DropLimb(_armNear);
                _armNear = null;
                _weapon = null;
                break;

            case BodyPart.Leg:
                DropLimb(_legNear);
                _legNear = null;
                break;

            case BodyPart.Eye:
            default:
                _headShape.Color = new Color(0.75f, 0.15f, 0.15f);
                break;
        }

        Splatter(part == BodyPart.Leg ? _hip : _torso);
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

    /// <summary>Kemiğe asılı çizim. Sanat geldiğinde değişecek tek yer burası.</summary>
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

    /// <summary>Üst ve alt eklemi birlikte kurar (omuz + dirsek, kalça + diz).</summary>
    /// <remarks>Zincir koptuysa düğüm artık rig'in değildir; sessizce atlanır.</remarks>
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

    // ------------------------------------------------------------- uzuv kopması

    /// <summary>Kopan uzvu rig'den ayırıp sahneye düşürür.</summary>
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

        // Zemin, savaşçının kendi kökünün bulunduğu yükseklik: uzuv oraya düşer.
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

/// <summary>Kopan uzvun yere düşüşü. Tamamen kozmetik — çekirdek bunu bilmez.</summary>
public sealed partial class FallingLimb : Node
{
    private float _velocity = -180f;
    private float _spin = 4.5f;

    /// <summary>Uzvun duracağı yükseklik — sahnenin zemin çizgisi.</summary>
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
