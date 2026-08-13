using Domina.Core.Combat;

namespace Domina.Presentation.Tests;

/// <summary>
/// Koreografi artık konum <b>üretmiyor</b>, çekirdekteki arena düzlemini ekrana
/// yansıtıyor. Bu testler o yansıtmayı ve üstüne eklenen saf görsel süslemeyi sınar.
/// </summary>
public class ArenaChoreographyTests
{
    private readonly ArenaLayout _layout = new();

    private ArenaChoreography Arena() => new(_layout);

    [Fact]
    public void ThePositionComesStraightFromTheCore()
    {
        ArenaChoreography arena = Arena();
        CombatantSnapshot warrior = TestSnapshots.Of(1, position: new ArenaPoint(640, 0));

        Assert.Equal(640f, arena.PositionFor(warrior).X, 3);
        Assert.Equal(_layout.FrontGroundY, arena.PositionFor(warrior).Y, 3);
    }

    /// <summary>
    /// Derinlik yandan bakan kamerada dikey kaymaya çevrilir: arkadaki savaşçı ekranda
    /// yukarıda durur. Bu olmadan iki savaşçı üst üste çizilir ve kim önde belli olmaz.
    /// </summary>
    [Fact]
    public void DepthLiftsTheWarriorUpTheScreen()
    {
        ArenaChoreography arena = Arena();

        ScenePoint front = arena.PositionFor(TestSnapshots.Of(1, position: new ArenaPoint(640, 0)));
        ScenePoint back = arena.PositionFor(TestSnapshots.Of(2, position: new ArenaPoint(640, _layout.Depth)));

        Assert.Equal(front.X, back.X, 3);
        Assert.True(back.Y < front.Y, "Derinlikteki savaşçı ekranda yukarıda durmalı.");
        Assert.Equal(_layout.BackGroundY, back.Y, 3);
    }

    /// <summary>Uzaktaki savaşçı küçülür; öndeki tam boydadır.</summary>
    [Fact]
    public void DepthShrinksTheWarrior()
    {
        ArenaChoreography arena = Arena();

        Assert.Equal(1f, arena.ScaleFor(TestSnapshots.Of(1, position: new ArenaPoint(0, 0))), 3);
        Assert.Equal(
            _layout.BackScale,
            arena.ScaleFor(TestSnapshots.Of(2, position: new ArenaPoint(0, _layout.Depth))),
            3);
    }

    /// <summary>Derindeki savaşçı öndekinin arkasına çizilir.</summary>
    [Fact]
    public void TheNearerWarriorDrawsInFront()
    {
        CombatantSnapshot near = TestSnapshots.Of(1, position: new ArenaPoint(0, 20));
        CombatantSnapshot far = TestSnapshots.Of(2, position: new ArenaPoint(0, 300));

        Assert.True(ArenaChoreography.DrawOrderFor(near) > ArenaChoreography.DrawOrderFor(far));
    }

    /// <summary>
    /// Kılıcı toplarken hafif geri yaslanma — çekirdekte karşılığı olmayan tek konum
    /// süslemesi. Yön savaşçının baktığı yöne bağlı.
    /// </summary>
    [Fact]
    public void TheWindupLeansBackFromTheFacing()
    {
        ArenaChoreography arena = Arena();
        var spot = new ArenaPoint(640, 0);

        ScenePoint idle = arena.PositionFor(TestSnapshots.Of(1, position: spot));
        ScenePoint winding = arena.PositionFor(TestSnapshots.Of(
            1,
            state: CombatState.AttackWindup,
            progress: 1,
            position: spot,
            facing: 1));

        Assert.True(winding.X < idle.X, "Sağa bakan savaşçı geri (sola) yaslanmalı.");

        ScenePoint mirrored = arena.PositionFor(TestSnapshots.Of(
            1,
            state: CombatState.AttackWindup,
            progress: 1,
            position: spot,
            facing: -1));

        Assert.True(mirrored.X > idle.X, "Sola bakan savaşçı ters yöne yaslanmalı.");
    }

    /// <summary>
    /// Ölen savaşçı düştüğü yerde kalır. Eskiden bunun için koreografinin hafızası
    /// gerekiyordu; artık çekirdek ölüyü hareket ettirmediği için bedava geliyor.
    /// </summary>
    [Fact]
    public void TheDeadStayWhereTheyFell()
    {
        ArenaChoreography arena = Arena();
        var spot = new ArenaPoint(812, 140);

        ScenePoint alive = arena.PositionFor(TestSnapshots.Of(1, position: spot));
        ScenePoint corpse = arena.PositionFor(
            TestSnapshots.Of(1, state: CombatState.Dead, health: 0, position: spot));

        Assert.Equal(alive.X, corpse.X, 3);
        Assert.Equal(alive.Y, corpse.Y, 3);
    }
}
