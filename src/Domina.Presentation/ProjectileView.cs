using Domina.Core.Combat;

namespace Domina.Presentation;

/// <summary>
/// Havadaki bir merminin ekrandaki hâli.
/// </summary>
/// <remarks>
/// <para>
/// Mermi savaşçı değildir: rig'i, duruşu, tepkisi yoktur. Tek ihtiyacı iki uç nokta ve
/// bir ilerleme oranı. Bu yüzden <see cref="RigAnimator"/> yerine ayrı bir sahne nesnesi
/// olarak taşınır.
/// </para>
/// <para>
/// Çekirdek merminin nerede olduğunu <b>tutmaz</b> — yalnızca ne zaman varacağını bilir
/// (bkz. <c>Projectile</c>). Aradaki konum saf görselleştirmedir; simülasyona geri
/// beslenmez.
/// </para>
/// </remarks>
public sealed class ProjectileView(ProjectileLaunched launched)
{
    private readonly double _flightSeconds = Math.Max(0.0001, launched.FlightSeconds);

    public string Weapon { get; } = launched.Weapon;

    public ArenaPoint From { get; } = launched.From;

    public ArenaPoint To { get; } = launched.To;

    /// <summary>Havalanmasından beri geçen süre.</summary>
    public double Elapsed { get; private set; }

    /// <summary>Uçuşun tamamlanma oranı (0-1).</summary>
    public double Progress => Math.Clamp(Elapsed / _flightSeconds, 0, 1);

    /// <summary>Varmış mı? Varan mermi sahneden kaldırılır.</summary>
    public bool HasLanded => Progress >= 1;

    /// <summary>Merminin arena düzlemindeki anlık yeri.</summary>
    public ArenaPoint Position
    {
        get
        {
            double t = Progress;
            return new ArenaPoint(
                From.X + ((To.X - From.X) * t),
                From.Y + ((To.Y - From.Y) * t));
        }
    }

    public void Advance(double delta) => Elapsed += delta;
}

/// <summary>
/// Sahnedeki mermileri olay akışından sürer.
/// </summary>
/// <remarks>
/// <see cref="ReactionReader"/> ile aynı desen: olaylar tek seferliktir, okunan yer
/// sayaçla takip edilir. Ayrı tutulmalarının sebebi ömürlerinin farklı olması —
/// tepki bir kare sürer, mermi uçuşu boyunca yaşar.
/// </remarks>
public sealed class ProjectileTracker
{
    private readonly List<ProjectileView> _inFlight = [];

    /// <summary>Şimdiye kadar okunan olay sayısı.</summary>
    public int Consumed { get; private set; }

    /// <summary>Halen havada olan mermiler.</summary>
    public IReadOnlyList<ProjectileView> InFlight => _inFlight;

    /// <summary>Yeni atışları alır, havadakileri ilerletir, varanları düşürür.</summary>
    public void Advance(IReadOnlyList<BattleEvent> events, double delta)
    {
        ArgumentNullException.ThrowIfNull(events);

        for (; Consumed < events.Count; Consumed++)
        {
            if (events[Consumed] is ProjectileLaunched launched)
            {
                _inFlight.Add(new ProjectileView(launched));
            }
        }

        for (int i = 0; i < _inFlight.Count; i++)
        {
            _inFlight[i].Advance(delta);

            if (_inFlight[i].HasLanded)
            {
                _inFlight.RemoveAt(i--);
            }
        }
    }
}
