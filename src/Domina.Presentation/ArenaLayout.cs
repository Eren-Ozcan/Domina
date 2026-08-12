using Domina.Core.Combat;

namespace Domina.Presentation;

/// <summary>
/// Arenanın ölçüleri: kim nerede durur, vuruş nereye iner, kaçan nereye gider.
/// </summary>
/// <remarks>
/// Sayılar tek yerde durur ki hem sahne kurulumu hem hareket matematiği aynı
/// düzeni görsün. Kadro sırası anlamlıdır: <b>0 numaralı savaşçı en öndedir</b> ve
/// çekirdeğin hedef seçimi de (<c>Battle.FindTarget</c>) listedeki ilk ayakta olanı
/// seçer — ekrandaki ön sıra ile çözümlemedeki hedef bu sayede aynı savaşçıdır.
/// </remarks>
public sealed record ArenaLayout
{
    /// <summary>Kadrajın genişliği. Kaçışın nerede biteceğini belirler.</summary>
    public float Width { get; init; } = 1920f;

    /// <summary>Zemin çizgisi — savaşçıların kökü bu yükseklikte durur.</summary>
    public float GroundY { get; init; } = 720f;

    /// <summary>İki tarafın merkeze olan uzaklığı.</summary>
    public float SideOffset { get; init; } = 150f;

    /// <summary>Aynı taraftaki savaşçılar arası mesafe.</summary>
    public float LaneGap { get; init; } = 140f;

    /// <summary>Vuruş anında hedefe bırakılan mesafe — silah boyu kadar.</summary>
    public float MeleeRange { get; init; } = 110f;

    /// <summary>Kılıcı toplarken geri yaslanma mesafesi.</summary>
    public float WindupDrawBack { get; init; } = 16f;

    /// <summary>
    /// Kaçan savaşçının kadraj kenarını geçtikten sonra gideceği fazladan mesafe.
    /// </summary>
    /// <remarks>
    /// Kaçış mesafesi sabit olduğunda savaşçı arenanın <b>ortasında</b> yok oluyordu:
    /// <see cref="CombatState.Escaped"/> gelir gelmez düğüm gizleniyor, ama savaşçı
    /// hâlâ kadrajın içinde duruyordu. Mesafe artık kadrajdan hesaplanıyor.
    /// </remarks>
    public float ExitMargin { get; init; } = 240f;

    public float CenterX => Width / 2f;

    /// <summary>Savaşçının hattındaki yeri.</summary>
    /// <param name="team">Takım (bkz. <see cref="Battle.PlayerTeam"/>).</param>
    /// <param name="index">Kadro sırası; 0 en öndeki savaşçıdır.</param>
    public ScenePoint HomeFor(int team, int index)
    {
        float direction = team == Battle.PlayerTeam ? -1f : 1f;
        return new ScenePoint(CenterX + (direction * (SideOffset + (index * LaneGap))), GroundY);
    }

    /// <summary>Savaşçının baktığı yön: oyuncu tarafı sağa, düşman tarafı sola bakar.</summary>
    public static float FacingFor(int team) => team == Battle.PlayerTeam ? 1f : -1f;

    /// <summary>Savaşçının kadrajı terk etmesi için gereken mesafe.</summary>
    public float ExitDistanceFrom(ScenePoint home, int team) =>
        team == Battle.PlayerTeam
            ? home.X + ExitMargin
            : (Width - home.X) + ExitMargin;
}
