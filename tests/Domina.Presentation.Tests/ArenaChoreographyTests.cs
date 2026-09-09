using Domina.Core.Combat;

namespace Domina.Presentation.Tests;

/// <summary>
/// The choreography no longer <b>produces</b> position, it projects the core's arena plane onto the
/// screen. These tests exercise that projection and the purely visual decoration added on top.
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
    /// With a camera looking from the side, depth is turned into a vertical offset: a warrior further
    /// back stands higher on screen. Without it two warriors are drawn on top of each other and who is in
    /// front is unclear.
    [Fact]
    public void DepthLiftsTheWarriorUpTheScreen()
    {
        ArenaChoreography arena = Arena();

        ScenePoint front = arena.PositionFor(TestSnapshots.Of(1, position: new ArenaPoint(640, 0)));
        ScenePoint back = arena.PositionFor(TestSnapshots.Of(2, position: new ArenaPoint(640, _layout.Depth)));

        Assert.Equal(front.X, back.X, 3);
        Assert.True(back.Y < front.Y, "The warrior in depth must stand higher on screen.");
        Assert.Equal(_layout.BackGroundY, back.Y, 3);
    }

    /// <summary>A distant warrior shrinks; the front one is at full size.</summary>
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

    /// <summary>A warrior in depth is drawn behind the one in front.</summary>
    [Fact]
    public void TheNearerWarriorDrawsInFront()
    {
        CombatantSnapshot near = TestSnapshots.Of(1, position: new ArenaPoint(0, 20));
        CombatantSnapshot far = TestSnapshots.Of(2, position: new ArenaPoint(0, 300));

        Assert.True(ArenaChoreography.DrawOrderFor(near) > ArenaChoreography.DrawOrderFor(far));
    }

    /// <summary>
    /// A slight lean back while gathering the sword — the only position decoration with no counterpart in
    /// the core. The direction depends on the way the warrior faces.
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

        Assert.True(winding.X < idle.X, "A warrior facing right must lean back (left).");

        ScenePoint mirrored = arena.PositionFor(TestSnapshots.Of(
            1,
            state: CombatState.AttackWindup,
            progress: 1,
            position: spot,
            facing: -1));

        Assert.True(mirrored.X > idle.X, "A warrior facing left must lean the opposite way.");
    }

    /// <summary>
    /// A dead warrior stays where he fell. This used to require memory in the choreography; now that the
    /// core does not move the dead, it comes free.
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
