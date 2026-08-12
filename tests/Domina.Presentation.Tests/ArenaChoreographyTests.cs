using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation.Tests;

/// <summary>
/// Savaşçıların sahnedeki yeri. Buradaki kuralların ortak amacı, <b>ekranda görünenin
/// çözümlemede olanla aynı şeyi anlatması</b>: hamle çekirdeğin seçtiği hedefe gider,
/// kaçan gerçekten kadrajı terk eder, ölen düştüğü yerde kalır.
/// </summary>
public class ArenaChoreographyTests
{
    private static readonly ArenaLayout _layout = new();

    /// <summary>Üç oyuncu, üç düşman — arenadaki kadronun aynısı.</summary>
    private static ArenaChoreography Arena()
    {
        var choreography = new ArenaChoreography(_layout);

        for (int i = 0; i < 3; i++)
        {
            choreography.Place(new WarriorId(1 + i), Battle.PlayerTeam, i);
            choreography.Place(new WarriorId(101 + i), Battle.EnemyTeam, i);
        }

        return choreography;
    }

    private static List<CombatantSnapshot> Line(params CombatantSnapshot[] snapshots) => [.. snapshots];

    [Fact]
    public void TheFrontRankStandsClosestToTheEnemy()
    {
        ScenePoint front = _layout.HomeFor(Battle.PlayerTeam, 0);
        ScenePoint back = _layout.HomeFor(Battle.PlayerTeam, 2);

        // Oyuncu tarafı merkezin solunda: öndeki savaşçı merkeze daha yakın.
        Assert.True(front.X > back.X);
        Assert.True(front.X < _layout.CenterX);

        ScenePoint enemyFront = _layout.HomeFor(Battle.EnemyTeam, 0);
        Assert.True(enemyFront.X > _layout.CenterX);
        Assert.Equal(_layout.GroundY, front.Y);
    }

    [Fact]
    public void TheLungeStopsAWeaponLengthShortOfTheTarget()
    {
        ArenaChoreography arena = Arena();

        // Vuruşun indiği an: toparlanmanın %35'i.
        CombatantSnapshot attacker = TestSnapshots.Of(1, state: CombatState.AttackRecovery, progress: 0.35);
        List<CombatantSnapshot> line = Line(attacker, TestSnapshots.Of(101, Battle.EnemyTeam));

        ScenePoint spot = arena.PositionFor(attacker, line);

        Assert.Equal(arena.HomeOf(new WarriorId(101)).X - _layout.MeleeRange, spot.X, 3);
    }

    /// <summary>
    /// Hamle sabit mesafe olsaydı arkadaki savaşçılar boşluğa kılıç sallardı: çekirdek
    /// hepsini aynı ön sıradaki düşmana yönlendiriyor.
    /// </summary>
    [Fact]
    public void TheBackRankLungesFartherToReachTheSameTarget()
    {
        ArenaChoreography arena = Arena();

        CombatantSnapshot front = TestSnapshots.Of(1, state: CombatState.AttackRecovery, progress: 0.35);
        CombatantSnapshot back = TestSnapshots.Of(3, state: CombatState.AttackRecovery, progress: 0.35);
        List<CombatantSnapshot> line = Line(front, back, TestSnapshots.Of(101, Battle.EnemyTeam));

        ScenePoint frontSpot = arena.PositionFor(front, line);
        ScenePoint backSpot = arena.PositionFor(back, line);

        Assert.Equal(frontSpot.X, backSpot.X, 3);
    }

    [Fact]
    public void TheLungeRetractsWhenTheRecoveryEnds()
    {
        ArenaChoreography arena = Arena();

        CombatantSnapshot attacker = TestSnapshots.Of(1, state: CombatState.AttackRecovery, progress: 1);
        ScenePoint spot = arena.PositionFor(attacker, Line(attacker, TestSnapshots.Of(101, Battle.EnemyTeam)));

        Assert.Equal(arena.HomeOf(new WarriorId(1)).X, spot.X, 3);
    }

    [Fact]
    public void TheWindupLeansBackFromTheLine()
    {
        ArenaChoreography arena = Arena();
        ScenePoint home = arena.HomeOf(new WarriorId(1));

        CombatantSnapshot attacker = TestSnapshots.Of(1, state: CombatState.AttackWindup, progress: 1);
        ScenePoint spot = arena.PositionFor(attacker, Line(attacker));

        Assert.Equal(home.X - _layout.WindupDrawBack, spot.X, 3);
    }

    /// <summary>
    /// Kaçış sabit mesafe olduğunda savaşçı arenanın <b>ortasında</b> yok oluyordu:
    /// durum Escaped'e döner dönmez düğüm gizleniyor, ama savaşçı hâlâ kadrajın
    /// içinde duruyordu.
    /// </summary>
    [Fact]
    public void TheFleeingLeaveTheFrameBeforeTheyVanish()
    {
        ArenaChoreography arena = Arena();

        CombatantSnapshot player = TestSnapshots.Of(1, state: CombatState.Retreating, progress: 1);
        CombatantSnapshot enemy = TestSnapshots.Of(101, Battle.EnemyTeam, CombatState.Retreating, progress: 1);

        Assert.True(arena.PositionFor(player, Line(player)).X < 0);
        Assert.True(arena.PositionFor(enemy, Line(enemy)).X > _layout.Width);
    }

    [Fact]
    public void TheEscapedAreAlreadyOutsideTheFrame()
    {
        ArenaChoreography arena = Arena();

        CombatantSnapshot escaped = TestSnapshots.Of(1, state: CombatState.Escaped, progress: 1);

        Assert.True(arena.PositionFor(escaped, Line(escaped)).X < 0);
    }

    [Fact]
    public void TheFleeingStartFromTheirLine()
    {
        ArenaChoreography arena = Arena();

        CombatantSnapshot leaving = TestSnapshots.Of(1, state: CombatState.Retreating, progress: 0);

        Assert.Equal(arena.HomeOf(new WarriorId(1)).X, arena.PositionFor(leaving, Line(leaving)).X, 3);
    }

    /// <summary>
    /// Konum yalnızca duruma bakılarak hesaplansaydı hamlenin ortasında ölen savaşçı
    /// ölür ölmez hattındaki yerine ışınlanırdı.
    /// </summary>
    [Fact]
    public void TheDeadStayWhereTheyFell()
    {
        ArenaChoreography arena = Arena();
        var enemy = TestSnapshots.Of(101, Battle.EnemyTeam);

        CombatantSnapshot lunging = TestSnapshots.Of(1, state: CombatState.AttackRecovery, progress: 0.35);
        ScenePoint struckAt = arena.PositionFor(lunging, Line(lunging, enemy));

        Assert.True(struckAt.X > arena.HomeOf(new WarriorId(1)).X);

        CombatantSnapshot corpse = TestSnapshots.Of(1, state: CombatState.Dead, health: 0);

        Assert.Equal(struckAt.X, arena.PositionFor(corpse, Line(corpse, enemy)).X, 3);

        // Ceset sonraki karelerde de kımıldamamalı.
        Assert.Equal(struckAt.X, arena.PositionFor(corpse, Line(corpse, enemy)).X, 3);
    }

    [Fact]
    public void TheDeadWhoNeverMovedRestOnTheirLine()
    {
        ArenaChoreography arena = Arena();
        CombatantSnapshot corpse = TestSnapshots.Of(1, state: CombatState.Dead, health: 0);

        Assert.Equal(arena.HomeOf(new WarriorId(1)).X, arena.PositionFor(corpse, Line(corpse)).X, 3);
    }

    /// <summary>Hedef kalmadığında hamle boşa gitmez; savaşçı hattında kalır.</summary>
    [Fact]
    public void ThereIsNoLungeWithoutATarget()
    {
        ArenaChoreography arena = Arena();

        CombatantSnapshot attacker = TestSnapshots.Of(1, state: CombatState.AttackRecovery, progress: 0.35);
        CombatantSnapshot deadEnemy = TestSnapshots.Of(101, Battle.EnemyTeam, CombatState.Dead, health: 0);

        ScenePoint spot = arena.PositionFor(attacker, Line(attacker, deadEnemy));

        Assert.Equal(arena.HomeOf(new WarriorId(1)).X, spot.X, 3);
    }
}
