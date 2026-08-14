using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// Havada olan bir mermi.
/// </summary>
/// <remarks>
/// <para>
/// Mermi atıldığı anda çözülmez; <see cref="SecondsToImpact"/> sıfırlanana kadar uçar.
/// Bunun bedeli biraz durum tutmak, karşılığı ise şu: hedef uçuş sırasında kaçabilir,
/// ölebilir ya da arenayı terk edebilir. "Attı = vurdu" olsaydı mesafenin bir anlamı
/// kalmazdı — fırlatmanın var oluş sebebi tam olarak mesafeyi bir tehdide çevirmek.
/// </para>
/// <para>
/// <see cref="Origin"/> yalnızca görselleştirme içindir; çözümleme yalnızca zamana bakar.
/// </para>
/// </remarks>
internal sealed class Projectile(
    Combatant attacker,
    Combatant target,
    ThrownWeapon weapon,
    ArenaPoint origin,
    double secondsToImpact)
{
    public Combatant Attacker { get; } = attacker;

    public Combatant Target { get; } = target;

    public ThrownWeapon Weapon { get; } = weapon;

    public ArenaPoint Origin { get; } = origin;

    public double SecondsToImpact { get; set; } = secondsToImpact;
}
