namespace Domina.Presentation;

/// <summary>
/// Bir savaşçının tek karedeki duruşu: her kemiğin açısı, radyan cinsinden.
/// </summary>
/// <remarks>
/// <para>
/// Kilitlenmiş rig'in birebir karşılığı (bkz. <c>docs/PROGRESS.md</c> → "Kilitlenen rig").
/// <b>Yakın</b> taraf oyuncuya dönük olan, silahı tutan taraftır; <b>uzak</b> taraf
/// gövdenin arkasında kalır. Kopma noktaları omuz ve kalçadır, bu yüzden kol ve bacak
/// zincirleri ayrı ayrı adreslenir.
/// </para>
/// <para>
/// Alan eklemek ucuz değildir: yeni bir alan, tüm duruşların yeniden gözden geçirilmesi
/// demektir. Sanat geldiğinde bu tip aynı kalır, yalnızca kemiğe asılı çizim değişir.
/// </para>
/// </remarks>
public readonly record struct RigPose
{
    /// <summary>Tüm gövdenin devrilmesi — yalnızca ölümde kullanılır.</summary>
    public float RootRotation { get; init; }

    /// <summary>Kalçanın dayanma noktasından yatay kayması (topallamada dolar).</summary>
    public float HipOffsetX { get; init; }

    /// <summary>Kalçanın çökmesi; artı değer aşağı iner (topallamada dolar).</summary>
    public float HipOffsetY { get; init; }

    public float Torso { get; init; }

    public float Head { get; init; }

    public float NearShoulder { get; init; }

    public float NearElbow { get; init; }

    public float FarShoulder { get; init; }

    public float FarElbow { get; init; }

    public float NearHip { get; init; }

    public float NearKnee { get; init; }

    public float FarHip { get; init; }

    public float FarKnee { get; init; }

    public float Weapon { get; init; }

    /// <summary>Acı rengine karışma oranı (0-1) — vuruş yeme anında yükselir.</summary>
    public float HurtBlend { get; init; }

    /// <summary>Savaşçı sahnede görünüyor mu? Arenayı terk edince kapanır.</summary>
    public bool Visible { get; init; }
}
