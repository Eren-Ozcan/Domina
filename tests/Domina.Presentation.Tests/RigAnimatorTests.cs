using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// Duruş üretimi. Sanat geldiğinde bu sayıların hepsi değişecek; sınanan şey
/// <b>sayılar değil kurallar</b>: kopan uzuv geri gelmez, ceset seğirmez, tek bacaklı
/// savaşçı kaçarken de topallar, ıskalayan vuruş isabet edenden farklı görünür.
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
        var loss = new RigReaction(new WarriorId(1), RigReactionKind.Dismember, BodyPart.Arm);

        Assert.Equal(BodyPart.Arm, animator.React(loss));
        Assert.True(animator.HasLost(BodyPart.Arm));

        // İkinci kez gelirse düğüm zaten sahnede değil: yeniden koparılamaz.
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

    /// <summary>Ceset seğirmez: yarım kalmış sarsıntı ölümle birlikte biter.</summary>
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

        // Bir saniye sonra iz kalmamalı, yoksa savaşçı kalıcı olarak kırmızı kalır.
        Assert.Equal(0f, Step(animator, CombatState.Idle, frames: 60).HurtBlend);
    }

    /// <summary>Iska ile isabet aynı görünürse oyuncu dövüşü yalnızca can barından okur.</summary>
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
    /// Fırsat saldırısı çekirdekte bir duruma karşılık gelmez — kaçan avın arkasından
    /// anında çözülür. Boşta bekleyen savaşçı vuruşu oynatmazsa bedava vuruşun kimden
    /// geldiği ekranda hiç görünmez.
    /// </summary>
    [Fact]
    public void TheOpportunitySwingInterruptsTheIdlePose()
    {
        var hunter = new RigAnimator();
        hunter.React(new RigReaction(new WarriorId(1), RigReactionKind.OpportunitySwing));

        RigPose swinging = Step(hunter, CombatState.Idle);
        RigPose waiting = Step(new RigAnimator(), CombatState.Idle);

        Assert.True(swinging.NearShoulder > waiting.NearShoulder + 1f);

        // Yaklaşık yarım saniye sonra beklemeye dönmeli.
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
        animator.React(new RigReaction(new WarriorId(1), RigReactionKind.Dismember, BodyPart.Arm));

        RigPose maimed = Step(animator, CombatState.Idle);
        RigPose whole = Step(new RigAnimator(), CombatState.Idle);

        // Gövde sağlam tarafa döner, kalan kol öne çıkar.
        Assert.True(maimed.Torso > whole.Torso);
        Assert.True(maimed.FarShoulder < whole.FarShoulder);
    }

    /// <summary>
    /// Kaçış duruşu sakatlığı yok saydığında bacağını kaybetmiş savaşçı arenadan
    /// <b>iki bacakla</b> koşarak çıkıyordu: kopan bacağın düğümü sahnede olmadığı için
    /// ekranda tek bacak görünüyor, ama kalça son topallama değerinde asılı kalıyordu.
    /// </summary>
    [Fact]
    public void TheOneLeggedLimpWhileFleeingToo()
    {
        var animator = new RigAnimator();
        animator.React(new RigReaction(new WarriorId(1), RigReactionKind.Dismember, BodyPart.Leg));

        RigPose fleeing = Step(animator, CombatState.Retreating, frames: 12);

        // Kopan bacak sürülmez, kalan bacak taşır, kalça çöker.
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
        animator.React(new RigReaction(new WarriorId(1), RigReactionKind.Dismember, BodyPart.Leg));

        RigPose waiting = Step(animator, CombatState.Idle, frames: 12);

        Assert.Equal(0f, waiting.NearHip);
        Assert.True(waiting.Torso > Step(new RigAnimator(), CombatState.Idle, frames: 12).Torso);
    }

    /// <summary>
    /// Kesilemez pencerenin duruşu belirgin olmalı: oyuncunun "artık çekemem" anını
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

    /// <summary>Kaçarken sırt dönük ve gövde öne yatık — kaçınma/blok yok.</summary>
    [Fact]
    public void TheFleeingTurnTheirBack()
    {
        RigPose fleeing = Step(new RigAnimator(), CombatState.Retreating, frames: 5);
        RigPose waiting = Step(new RigAnimator(), CombatState.Idle, frames: 5);

        Assert.True(fleeing.Torso > waiting.Torso);
        Assert.True(fleeing.Head < waiting.Head);
    }
}
