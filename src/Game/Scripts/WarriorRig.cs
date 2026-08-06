using Domina.Core.Combat;
using Domina.Core.Model;
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
/// Duruşlar burada <b>yordamsal</b> üretiliyor (durum + faz → kemik açısı). Gerçek
/// sanat geldiğinde bunun yerini AnimationPlayer alacak; çağıran taraf
/// (<see cref="BattleArena"/>) değişmez, çünkü tek arayüz <see cref="ApplyState"/>.
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

    private Node2D _hip = null!;
    private Node2D _torso = null!;
    private Node2D _head = null!;
    private Node2D _armFar = null!;
    private Node2D _armNear = null!;
    private Node2D _legFar = null!;
    private Node2D _legNear = null!;
    private Node2D _weapon = null!;
    private Polygon2D _headShape = null!;

    private float _facing = 1f;
    private double _clock;
    private double _flinch;
    private Color _tint = Colors.White;

    /// <summary>Kaybedilen uzuvlar — kalıcıdır, geri takılmaz.</summary>
    private readonly HashSet<BodyPart> _lost = [];

    /// <summary>Dövüş bittiğinde savaşçının son hali (yere düşme açısı için).</summary>
    private float _deathLean;

    public WarriorId WarriorId { get; private set; }

    public string WarriorName { get; private set; } = string.Empty;

    /// <summary>Rig'i kurar. Bir kez çağrılır.</summary>
    public void Build(Warrior warrior, Color tint, float facing)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        WarriorId = warrior.Id;
        WarriorName = warrior.Name;
        _tint = tint;
        _facing = facing;

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

        ApplyIdlePose(0);
    }

    /// <summary>
    /// Dövüşün o anki halini duruşa çevirir.
    /// </summary>
    /// <param name="state">Çekirdeğin bildirdiği durum.</param>
    /// <param name="phase">Durumun tamamlanma oranı (0-1).</param>
    /// <param name="delta">Geçen süre.</param>
    public void ApplyState(CombatState state, double phase, double delta)
    {
        _clock += delta;
        _flinch = Math.Max(0, _flinch - (delta * 4));

        switch (state)
        {
            case CombatState.AttackWindup:
                ApplyWindupPose(phase);
                break;
            case CombatState.AttackRecovery:
                ApplySwingPose(phase);
                break;
            case CombatState.Retreating:
                ApplyRetreatPose(phase);
                break;
            case CombatState.Dead:
                ApplyDeathPose(delta);
                return;
            case CombatState.Escaped:
                Visible = false;
                return;
            case CombatState.Idle:
            default:
                ApplyIdlePose(phase);
                break;
        }

        ApplyInjuryOverlay();
        ApplyFlinch();
    }

    /// <summary>Vuruş yeme tepkisi — tek seferlik, duruşun üstüne biner.</summary>
    public void Flinch() => _flinch = 1;

    /// <summary>
    /// Uzvu kalıcı olarak ayırır ve düşen parçayı sahneye bırakır.
    /// </summary>
    /// <remarks>
    /// Kopan uzuv gizlenmiyor, <b>ayrılıyor</b>: altındaki tüm zincir (ön kol, el,
    /// silah) onunla birlikte gidiyor. Kolunu kaybeden savaşçının silahını da
    /// kaybetmesi böylece görselde bedava geliyor — çekirdekteki
    /// <see cref="Warrior.UsableWeapon"/> kuralıyla aynı sonuç.
    /// </remarks>
    public void Dismember(BodyPart part)
    {
        if (!_lost.Add(part))
        {
            return;
        }

        Node2D? limb = part switch
        {
            BodyPart.Arm => _armNear,
            BodyPart.Leg => _legNear,
            _ => null,
        };

        if (limb is not null)
        {
            DropLimb(limb);
        }

        Splatter(part == BodyPart.Leg ? _hip : _torso);

        if (part == BodyPart.Eye)
        {
            _headShape.Color = new Color(0.75f, 0.15f, 0.15f);
        }
    }

    /// <summary>Ölüm anı — savaşçı yığılır.</summary>
    public void Die() => _deathLean = 0;

    // ------------------------------------------------------------------ duruşlar

    private void ApplyIdlePose(double phase)
    {
        float bob = Mathf.Sin((float)_clock * 3f) * 0.04f;

        _torso.Rotation = -0.06f + bob;
        _head.Rotation = 0.04f - bob;

        // Silah kolu hazır, boşta olan kol denge için hafif açık.
        Pose(_armNear, 2.15f + bob, -0.75f);
        Pose(_armFar, 2.55f - bob, -0.45f);

        Stand(bob);
        _weapon.Rotation = -0.35f;
    }

    private void ApplyWindupPose(double phase)
    {
        // Kesilemez pencere: kılıç geriye ve yukarı toplanır. Oyuncunun "artık
        // çekemem" anını görsel olarak okuyabilmesi için duruş belirgin olmalı.
        float t = Ease((float)phase);

        _torso.Rotation = -0.06f - (0.30f * t);
        _head.Rotation = 0.10f * t;

        Pose(_armNear, 2.15f + (1.35f * t), -0.75f - (0.55f * t));
        Pose(_armFar, 2.55f - (0.65f * t), -0.45f);

        Stand(0);
        _legNear.Rotation = -0.18f * t;
        _weapon.Rotation = -0.35f - (0.55f * t);
    }

    private void ApplySwingPose(double phase)
    {
        // Vuruş ilk anda iner, kalanı toparlanmadır (yeniden kesilebilir).
        float t = Ease(Math.Min(1f, (float)phase * 2.6f));

        _torso.Rotation = -0.36f + (0.62f * t);
        _head.Rotation = 0.10f - (0.22f * t);

        Pose(_armNear, 3.50f - (1.95f * t), -1.30f + (1.15f * t));
        Pose(_armFar, 1.90f + (0.75f * t), -0.45f);

        Stand(0);
        _legNear.Rotation = -0.18f + (0.34f * t);
        _weapon.Rotation = -0.90f + (0.80f * t);
    }

    private void ApplyRetreatPose(double phase)
    {
        // Savunmasızlık penceresi: sırtını dönmüş, koşuyor. Kaçınma/blok yok —
        // duruşun bunu ele vermesi lazım ki bedeli görsel olarak da anlaşılsın.
        float run = Mathf.Sin((float)_clock * 14f);

        _torso.Rotation = 0.28f;
        _head.Rotation = -0.35f;

        Pose(_armNear, 2.4f + (run * 0.7f), -0.9f);
        Pose(_armFar, 2.4f - (run * 0.7f), -0.9f);

        _legNear.Rotation = run * 0.75f;
        _legFar.Rotation = -run * 0.75f;
        Bend(_legNear, Math.Max(0, -run) * 0.9f);
        Bend(_legFar, Math.Max(0, run) * 0.9f);

        _weapon.Rotation = -0.1f;
    }

    private void ApplyDeathPose(double delta)
    {
        _deathLean = Mathf.Min(1f, _deathLean + ((float)delta * 3.2f));
        float t = Ease(_deathLean);

        Rotation = t * 1.45f;
        Position = new Vector2(Position.X, Position.Y);

        _torso.Rotation = 0.35f * t;
        _head.Rotation = 0.5f * t;
        Pose(_armNear, 2.2f, -0.2f);
        Pose(_armFar, 2.6f, -0.2f);
        _legNear.Rotation = 0.4f * t;
        _legFar.Rotation = -0.25f * t;
    }

    /// <summary>Ayakta durma — bacak kaybı varsa topallamaya döner.</summary>
    private void Stand(float bob)
    {
        if (_lost.Contains(BodyPart.Leg))
        {
            // Tek bacak: sağlam bacağa yaslanmış, gövde o tarafa yatık.
            float hop = Mathf.Abs(Mathf.Sin((float)_clock * 2.2f)) * 0.10f;
            _legFar.Rotation = -0.12f - hop;
            Bend(_legFar, 0.05f);
            _hip.Position = new Vector2(6, -HipHeight + 16 + (hop * 40));
            return;
        }

        _hip.Position = new Vector2(0, -HipHeight);
        _legNear.Rotation = 0.10f + bob;
        _legFar.Rotation = -0.10f - bob;
        Bend(_legNear, 0.06f);
        Bend(_legFar, 0.06f);
    }

    /// <summary>Sakatlıkların duruşa kalıcı etkisi.</summary>
    private void ApplyInjuryOverlay()
    {
        if (_lost.Contains(BodyPart.Arm))
        {
            // Kolunu kaybeden savaşçı kalan koluyla tek elli dövüşür: gövde
            // sağlam tarafa döner, kalan kol daha öne çıkar.
            _torso.Rotation += 0.12f;
            Pose(_armFar, _armFar.Rotation - 0.35f, -0.55f);
        }

        if (_lost.Contains(BodyPart.Leg))
        {
            _torso.Rotation += 0.16f;
        }
    }

    private void ApplyFlinch()
    {
        if (_flinch <= 0)
        {
            return;
        }

        float t = (float)_flinch;
        _torso.Rotation += 0.22f * t;
        _head.Rotation += 0.30f * t;
        Modulate = _tint.Lerp(new Color(1f, 0.55f, 0.55f), t * 0.7f);
    }

    // ------------------------------------------------------------- rig kurulumu

    private Node2D BuildArm(Node2D parent, Color color, int z)
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

    private Node2D BuildLeg(Node2D parent, Color color, int z)
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
    private static void Pose(Node2D limb, float upper, float lower)
    {
        limb.Rotation = upper;
        Bend(limb, lower);
    }

    private static void Bend(Node2D limb, float angle) =>
        limb.GetChild<Node2D>(1).Rotation = angle;

    private Color Shade(float amount) =>
        amount >= 0 ? _tint.Lerp(Colors.White, amount) : _tint.Lerp(Colors.Black, -amount);

    private static float Ease(float t) => t * t * (3f - (2f * t));

    // ------------------------------------------------------------- uzuv kopması

    /// <summary>Kopan uzvu rig'den ayırıp sahneye düşürür.</summary>
    private void DropLimb(Node2D limb)
    {
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
                DefaultColor = new Color(0.65f, 0.06f, 0.06f),
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
