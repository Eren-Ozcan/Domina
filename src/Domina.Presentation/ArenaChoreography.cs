using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>
/// Savaşçıların sahnedeki yerini anlık görüntülerden hesaplar.
/// </summary>
/// <remarks>
/// <para>
/// Hamle sabit bir mesafe değil, <b>hedefe kadar</b>: çekirdek herkesi aynı ön
/// sıradaki düşmana yönelttiği için sabit hamle arkadaki savaşçılara boşluğa kılıç
/// sallatıyordu. Mesafeyi hedeften hesaplamak, çözümlemedeki hedef seçimiyle
/// ekrandaki hareketi aynı şeye bağlar.
/// </para>
/// <para>
/// Sınıfın hafızası var: <b>ölen savaşçı düştüğü yerde kalır</b>. Konum yalnızca
/// duruma bakılarak hesaplansaydı hamlenin ortasında ölen savaşçı ölür ölmez
/// hattındaki yerine ışınlanırdı.
/// </para>
/// </remarks>
public sealed class ArenaChoreography(ArenaLayout layout)
{
    private readonly Dictionary<WarriorId, ScenePoint> _homes = [];
    private readonly Dictionary<WarriorId, ScenePoint> _live = [];
    private readonly Dictionary<WarriorId, ScenePoint> _resting = [];

    /// <summary>Vuruşun hedefe ne kadar erken indiği (toparlanmanın oranı olarak).</summary>
    private const float _strikeAt = 0.35f;

    public ArenaLayout Layout { get; } = layout;

    /// <summary>Savaşçıyı hattına yerleştirir ve ev konumunu döndürür.</summary>
    /// <param name="index">Kadro sırası; 0 en öndeki savaşçıdır.</param>
    public ScenePoint Place(WarriorId id, int team, int index)
    {
        ScenePoint home = Layout.HomeFor(team, index);
        _homes[id] = home;
        _live[id] = home;
        return home;
    }

    /// <summary>Savaşçının hattındaki yeri.</summary>
    public ScenePoint HomeOf(WarriorId id) => _homes[id];

    /// <summary>Savaşçının bu karedeki yeri.</summary>
    /// <param name="snapshot">Konumu hesaplanacak savaşçı.</param>
    /// <param name="all">Aynı karenin tüm anlık görüntüleri — hedef bunlardan seçilir.</param>
    public ScenePoint PositionFor(in CombatantSnapshot snapshot, IReadOnlyList<CombatantSnapshot> all)
    {
        ArgumentNullException.ThrowIfNull(all);

        if (_resting.TryGetValue(snapshot.Id, out ScenePoint resting))
        {
            return resting;
        }

        ScenePoint home = _homes[snapshot.Id];

        if (snapshot.State == CombatState.Dead)
        {
            // Düştüğü yer: ölümden ÖNCEKİ karenin konumu. Ölüm anında durum artık
            // Dead olduğu için buradan yeniden hesaplanamaz, hatırlanması gerekir.
            ScenePoint spot = _live.GetValueOrDefault(snapshot.Id, home);
            _resting[snapshot.Id] = spot;
            return spot;
        }

        ScenePoint position = Compute(snapshot, all, home);
        _live[snapshot.Id] = position;
        return position;
    }

    private ScenePoint Compute(in CombatantSnapshot snapshot, IReadOnlyList<CombatantSnapshot> all, ScenePoint home)
    {
        float facing = ArenaLayout.FacingFor(snapshot.Team);
        float phase = (float)snapshot.StateProgress;

        switch (snapshot.State)
        {
            case CombatState.AttackWindup:
                // Kılıcı toplarken hafif geri yaslanma.
                return home with { X = home.X - (facing * Layout.WindupDrawBack * Curves.Smooth(phase)) };

            case CombatState.AttackRecovery:
                {
                    // Vuruş erken iner, kalanı geri çekilmedir — hamle de aynı ritmi izler.
                    float lunge = phase < _strikeAt
                        ? phase / _strikeAt
                        : 1f - ((phase - _strikeAt) / (1f - _strikeAt));

                    return ScenePoint.Lerp(home, StrikePoint(snapshot, all, home, facing), Curves.Smooth(lunge));
                }

            case CombatState.Retreating:
                return Fleeing(home, snapshot.Team, facing, Curves.Smooth(phase));

            case CombatState.Escaped:
                // Kadrajın dışı: savaşçı gizlenmeden önce ekranı gerçekten terk etmiş olmalı.
                return Fleeing(home, snapshot.Team, facing, 1f);

            case CombatState.Idle:
            case CombatState.Dead:
            default:
                return home;
        }
    }

    private ScenePoint Fleeing(ScenePoint home, int team, float facing, float progress) =>
        home with { X = home.X - (facing * Layout.ExitDistanceFrom(home, team) * progress) };

    /// <summary>
    /// Vuruşun ineceği nokta: çekirdeğin seçtiği hedefin bir silah boyu önü.
    /// </summary>
    /// <remarks>
    /// Hedef seçimi <c>Battle.FindTarget</c> ile aynı kuralı izler — karşı taraftaki
    /// <b>ilk ayakta</b> savaşçı. Anlık görüntüler kurulum sırasında geldiği için
    /// listedeki sıra o kuralla birebir aynı.
    /// </remarks>
    private ScenePoint StrikePoint(
        in CombatantSnapshot attacker,
        IReadOnlyList<CombatantSnapshot> all,
        ScenePoint home,
        float facing)
    {
        foreach (CombatantSnapshot other in all)
        {
            if (other.Team != attacker.Team && other.IsActive)
            {
                return new ScenePoint(_homes[other.Id].X - (facing * Layout.MeleeRange), home.Y);
            }
        }

        return home;
    }
}
