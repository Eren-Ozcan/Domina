using Domina.Core.Combat;

namespace Domina.Presentation;

/// <summary>
/// Arena düzlemini ekrana yansıtır.
/// </summary>
/// <remarks>
/// <para>
/// Çekirdek savaşçıları bir <b>düzlemde</b> tutar (X hat boyunca, Y derinlik).
/// Kamera hâlâ yandan bakar; derinlik ekranda üç şeye çevrilir: dikey kayma, hafif
/// ölçek ve çizim sırası. Bu, 2D brawler sahnelemesinin ta kendisidir — derinlik
/// gerçek ama görüntü düz kalır, yani kesilmiş kâğıt parçalarından kurulu rig bozulmaz.
/// </para>
/// <para>
/// Burada <b>karar yok</b>: kim nerede durur sorusunun cevabı çekirdekte. Bu sınıf
/// yalnızca birim çevirir.
/// </para>
/// </remarks>
public sealed record ArenaLayout
{
    /// <summary>Arenanın hat boyunca genişliği — çekirdekteki değerle aynı olmalı.</summary>
    public float Width { get; init; } = 1920f;

    /// <summary>Arenanın derinliği — çekirdekteki değerle aynı olmalı.</summary>
    public float Depth { get; init; } = 420f;

    /// <summary>Derinliğin sıfır olduğu (en öndeki) zemin çizgisi.</summary>
    public float FrontGroundY { get; init; } = 860f;

    /// <summary>En arkadaki zemin çizgisi. Aradaki fark derinliğin dikey karşılığıdır.</summary>
    public float BackGroundY { get; init; } = 600f;

    /// <summary>En arkadaki savaşçının ölçeği; öndeki 1.0'dır.</summary>
    public float BackScale { get; init; } = 0.78f;

    /// <summary>Kılıcı toplarken geri yaslanma mesafesi — saf görsel.</summary>
    public float WindupDrawBack { get; init; } = 16f;

    /// <summary>Arena noktasını ekran noktasına çevirir.</summary>
    public ScenePoint Project(ArenaPoint point) =>
        new((float)point.X, FrontGroundY - (DepthFraction(point.Y) * (FrontGroundY - BackGroundY)));

    /// <summary>Verilen derinlikteki savaşçının ölçeği.</summary>
    public float ScaleAt(double depth) => 1f - (DepthFraction(depth) * (1f - BackScale));

    private float DepthFraction(double depth) =>
        Depth <= 0 ? 0 : Math.Clamp((float)(depth / Depth), 0f, 1f);
}
