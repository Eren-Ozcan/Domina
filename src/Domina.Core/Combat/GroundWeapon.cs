using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// Elden düşüp arenada kalan bir silah.
/// </summary>
/// <remarks>
/// <para>
/// Düşen silah yok olmaz: <b>arenada bir nokta</b> olur ve silahsız kalan herkes —
/// düşüren, takım arkadaşı ya da düşman — onu alabilir. Kırılma yerine düşme
/// seçilmesinin asıl karşılığı bu; kaybedilen silah kayıp değil, yerde duran ve
/// uğruna yürünmesi gereken bir şey.
/// </para>
/// <para>
/// Liste düşme sırasını korur: aynı tick'te iki savaşçı aynı silaha varırsa hangisinin
/// aldığı sabit olmalı, yoksa aynı seed aynı dövüşü vermez.
/// </para>
/// </remarks>
internal sealed class GroundWeapon(Weapon weapon, ArenaPoint position)
{
    public Weapon Weapon { get; } = weapon;

    /// <summary>Düştüğü yer. Silah yerde durur, sürüklenmez.</summary>
    public ArenaPoint Position { get; } = position;
}
