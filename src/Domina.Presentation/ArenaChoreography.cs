using Domina.Core.Combat;

namespace Domina.Presentation;

/// <summary>
/// Savaşçıların sahnedeki yerini anlık görüntülerden hesaplar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Konum artık burada üretilmiyor, çekirdekten okunuyor.</b> Savaşçılar arena
/// düzleminde gerçekten yürüyor; bu sınıf yalnızca o düzlemi ekrana yansıtır ve
/// üstüne saf görsel süslemeler ekler (kılıcı toplarken geri yaslanma gibi).
/// </para>
/// <para>
/// Eskiden burada sahte bir uzam vardı: hamle mesafesi, kaçış mesafesi, ölüm konumu
/// hep tahmin ediliyordu — çünkü çekirdekte konum yoktu. Uzam çekirdeğe girince o
/// matematiğin tamamı gereksizleşti. Ölen savaşçının düştüğü yerde kalması da artık
/// hatırlanacak bir şey değil: çekirdek ölüyü hareket ettirmiyor.
/// </para>
/// </remarks>
public sealed class ArenaChoreography(ArenaLayout layout)
{
    public ArenaLayout Layout { get; } = layout;

    /// <summary>Savaşçının bu karedeki ekran konumu.</summary>
    public ScenePoint PositionFor(in CombatantSnapshot snapshot)
    {
        ScenePoint ground = Layout.Project(snapshot.Position);

        if (snapshot.State is not (CombatState.AttackWindup or CombatState.ThrowWindup))
        {
            return ground;
        }

        // Kılıcı toplarken hafif geri yaslanma — saf görsel, çekirdekte karşılığı yok.
        float drawBack = Layout.WindupDrawBack * Curves.Smooth((float)snapshot.StateProgress);
        return ground with { X = ground.X - (snapshot.Facing * drawBack) };
    }

    /// <summary>Savaşçının hangi tarafa baktığı: +1 sağa, -1 sola.</summary>
    public static float FacingOf(in CombatantSnapshot snapshot) => snapshot.Facing;

    /// <summary>
    /// Derinliğe göre ölçek — uzaktaki savaşçı küçük görünür.
    /// </summary>
    public float ScaleFor(in CombatantSnapshot snapshot) => Layout.ScaleAt(snapshot.Position.Y);

    /// <summary>
    /// Çizim sırası: derinde duran arkada kalır. Godot katmanı bunu <c>ZIndex</c> yapar.
    /// </summary>
    /// <remarks>
    /// Y büyüdükçe savaşçı <b>uzaklaşır</b>, yani sıra ters işaretlidir.
    /// </remarks>
    public static int DrawOrderFor(in CombatantSnapshot snapshot) => -(int)snapshot.Position.Y;
}
