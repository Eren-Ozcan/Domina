using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>
/// Bir savaşçının animasyon hali: durum + tepkiler → duruş.
/// </summary>
/// <remarks>
/// <para>
/// Duruşlar <b>yordamsal</b> üretilir (durum + faz → kemik açısı). Gerçek sanat
/// geldiğinde bunun yerini AnimationPlayer alabilir; çağıran taraf değişmez, çünkü
/// tek arayüz <see cref="Advance"/>.
/// </para>
/// <para>
/// Sınıfın hafızası yalnızca <b>görsel</b> hafızadır: sayaçlar ve kaybedilen uzuvlar.
/// Dövüşün gidişatını etkileyemez — ok tek yönlüdür (bkz. CLAUDE.md → "Mimari kuralı").
/// </para>
/// </remarks>
public sealed class RigAnimator
{
    // Tepki sayaçları 1'den 0'a iner; sayı büyüdükçe tepki kısalır.
    private const double _flinchDecayPerSecond = 4.0;
    private const double _dodgeDecayPerSecond = 3.4;
    private const double _overswingDecayPerSecond = 2.6;
    private const double _opportunityDecayPerSecond = 2.4;
    private const double _stumbleDecayPerSecond = 1.8;
    private const double _deathFallPerSecond = 3.2;

    private readonly HashSet<BodyPart> _lost = [];

    private double _clock;
    private double _flinch;
    private double _dodge;
    private double _overswing;
    private double _opportunity;
    private double _stumble;
    private double _deathLean;

    /// <summary>Kaybedilen uzuvlar kalıcıdır — geri takılmaz.</summary>
    public bool HasLost(BodyPart part) => _lost.Contains(part);

    /// <summary>
    /// Tek seferlik tepkiyi işler.
    /// </summary>
    /// <returns>
    /// Bu tepki bir uzvu <b>ilk kez</b> kopardıysa o uzuv, aksi hâlde <c>null</c>.
    /// Çağıran taraf düğümü rig'den ayırmak için bunu kullanır; ikinci kez aynı uzuv
    /// gelirse <c>null</c> döner, çünkü zaten sahnede değildir.
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
                // Fırlatma da bir savurmadır; kolun geri gelişi aynı eğriyi kullanır.
                _overswing = 1;
                break;

            case RigReactionKind.Stumble:
                _stumble = 1;
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

    /// <summary>Sayaçları ilerletir ve bu karenin duruşunu üretir.</summary>
    /// <param name="state">Çekirdeğin bildirdiği durum.</param>
    /// <param name="stateProgress">Durumun tamamlanma oranı (0-1).</param>
    /// <param name="delta">Geçen süre.</param>
    public RigPose Advance(CombatState state, double stateProgress, double delta)
    {
        _clock += delta;
        _flinch = Decay(_flinch, delta, _flinchDecayPerSecond);
        _dodge = Decay(_dodge, delta, _dodgeDecayPerSecond);
        _overswing = Decay(_overswing, delta, _overswingDecayPerSecond);
        _opportunity = Decay(_opportunity, delta, _opportunityDecayPerSecond);
        _stumble = Decay(_stumble, delta, _stumbleDecayPerSecond);

        if (state == CombatState.Dead)
        {
            _deathLean = Math.Min(1, _deathLean + (delta * _deathFallPerSecond));
        }

        return Compose(state, stateProgress);
    }

    private static double Decay(double value, double delta, double rate) =>
        Math.Max(0, value - (delta * rate));

    // ------------------------------------------------------------------ derleme

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
            // Fırlatma da bir toplanma ve savurmadır; yordamsal duruşta yakın dövüşle
            // aynı eğrileri kullanır. Gerçek sanat geldiğinde ayrışacak yer burası.
            // Hücumun birikmesi de bir toplanmadır — kendi duruşunu kazanana kadar
            // (bkz. docs/ROADMAP.md 2.2) saldırı toplanmasının eğrilerini ödünç alır.
            CombatState.AttackWindup or CombatState.ThrowWindup or CombatState.ChargeWindup =>
                Windup(phase),
            CombatState.AttackRecovery or CombatState.ThrowRecovery =>
                Swing(Curves.Smooth(Math.Min(1, phase * 2.6))),
            CombatState.Retreating => Retreat(),
            CombatState.Charging => Charge(),
            _ => Idle(),
        };

        // Fırsat saldırısı çekirdekte bir duruma karşılık gelmez: kaçan avın arkasından
        // anında çözülür. Boşta bekleyen savaşçının duruşunu geçici olarak devralması,
        // bedava vuruşun kimden geldiğini gösteren tek işaret.
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

        // Silah kolu hazır, boşta olan kol denge için hafif açık.
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

    private static RigPose Windup(double phase)
    {
        // Kesilemez pencere: kılıç geriye ve yukarı toplanır. Oyuncunun "artık çekemem"
        // anını görsel olarak okuyabilmesi için duruş belirgin olmalı.
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
        // Vuruş ilk anda iner, kalanı toparlanmadır (yeniden kesilebilir).
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

    /// <summary>Boşa savurma: kılıç hedefi bulamadığı için savaşçı kendi hamlesini taşır.</summary>
    /// <remarks>
    /// Iskanın ekranda karşılığı olmadığında isabet eden ve etmeyen vuruş aynı görünür;
    /// oyuncu dövüşün gidişatını yalnızca can barından okumak zorunda kalır.
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
    /// Hücum duruşu: öne yatmış, silah geride, koşu döngüsü kaçıştan daha uzun adımlı.
    /// </summary>
    /// <remarks>
    /// Kaçışın aynası: ikisi de koşudur, ikisinde de savunma yoktur, ama biri hedefe
    /// diğeri hedeften kaçar. Farkı taşıyan şey gövdenin yönü — kaçarken geriye,
    /// hücumda öne yatar (docs/GDD.md §4).
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
        // Savunmasızlık penceresi: sırtını dönmüş, koşuyor. Kaçınma/blok yok — duruşun
        // bunu ele vermesi lazım ki bedeli görsel olarak da anlaşılsın.
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

    /// <summary>Kalıcı sakatlıkların duruşa etkisi.</summary>
    private RigPose Injuries(RigPose pose, CombatState state)
    {
        if (_lost.Contains(BodyPart.Arm))
        {
            // Kolunu kaybeden savaşçı kalan koluyla tek elli dövüşür: gövde sağlam
            // tarafa döner, kalan kol daha öne çıkar.
            pose = pose with
            {
                Torso = pose.Torso + 0.12f,
                FarShoulder = pose.FarShoulder - 0.35f,
                FarElbow = -0.55f,
            };
        }

        return _lost.Contains(BodyPart.Leg) ? OneLegged(pose, state) : pose;
    }

    /// <summary>
    /// Tek bacak: sağlam bacağa yaslanmış, gövde o tarafa yatık.
    /// </summary>
    /// <remarks>
    /// Kaçış duruşu da buradan geçer. Geçmediğinde bacağını kaybetmiş savaşçı
    /// arenadan <b>iki bacakla</b> koşarak çıkıyordu: kopan bacağın düğümü sahnede
    /// olmadığı için ekranda tek bacak görünüyor, ama kalça son topallama değerinde
    /// asılı kalıyor ve sağlam bacak normal koşu döngüsünü oynatıyordu.
    /// </remarks>
    private RigPose OneLegged(RigPose pose, CombatState state)
    {
        // Koşan iki durum var; ikisi de aynı sekme döngüsünü kullanır.
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

    /// <summary>Tek seferlik tepkilerin duruşun üstüne binmesi.</summary>
    private RigPose Reactions(RigPose pose)
    {
        if (_dodge > 0)
        {
            // Kaçınma stamina harcar; harcamanın nereye gittiği ekranda görünmeli.
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

        if (_stumble > 0)
        {
            // Sendeleme sarsılma değil: kimse vurmadı, savaşçı kendi ayağına takıldı.
            // Bu yüzden gövde geriye değil YANA gider ve bacak ayrışır.
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
