namespace Domina.Core.Combat;

/// <summary>
/// Arena düzlemindeki bir nokta. <b>X</b> hat boyunca (soldan sağa), <b>Y</b> derinlik.
/// </summary>
/// <remarks>
/// <para>
/// Çekirdek motorsuz olmak zorunda olduğu için Godot'un <c>Vector2</c>'si kullanılamaz.
/// Birim keyfîdir ama sahnenin ölçeğiyle aynıdır: arena 1920 birim geniş, savaşçı
/// 256 birim boyunda.
/// </para>
/// <para>
/// Derinlik ekranda dikey kayma + hafif ölçek + çizim sırası olarak gösterilir (brawler
/// sahnelemesi). Yani düzlem gerçek, kamera hâlâ yandan bakıyor.
/// </para>
/// </remarks>
public readonly record struct ArenaPoint(double X, double Y)
{
    public static ArenaPoint Zero => new(0, 0);

    public double DistanceTo(ArenaPoint other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>Karekök almadan mesafe karşılaştırmak için.</summary>
    public double SquaredDistanceTo(ArenaPoint other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>Hedefe doğru en fazla <paramref name="distance"/> birim ilerler.</summary>
    public ArenaPoint MovedToward(ArenaPoint target, double distance)
    {
        double dx = target.X - X;
        double dy = target.Y - Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length <= double.Epsilon || distance <= 0)
        {
            return this;
        }

        if (distance >= length)
        {
            return target;
        }

        double scale = distance / length;
        return new ArenaPoint(X + (dx * scale), Y + (dy * scale));
    }

    /// <summary>Hedeften uzağa doğru ilerler.</summary>
    /// <remarks>
    /// Mesafe <b>her zaman</b> istenen kadardır: aradaki uzaklıkla sınırlanmaz. Yön
    /// birim vektöre indirgenmeden hesaplansaydı, kaynağa yakınken atılan adım kısalır
    /// ve "şu kadar uzağa" isteği sessizce kırpılırdı.
    /// </remarks>
    public ArenaPoint MovedAwayFrom(ArenaPoint source, double distance)
    {
        double dx = X - source.X;
        double dy = Y - source.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length <= double.Epsilon || distance <= 0)
        {
            return this;
        }

        double scale = distance / length;
        return new ArenaPoint(X + (dx * scale), Y + (dy * scale));
    }
}
