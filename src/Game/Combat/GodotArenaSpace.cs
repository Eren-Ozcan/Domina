using System.Diagnostics;
using Domina.Core.Combat;
using Godot;

namespace Domina.Game.Combat;

/// <summary>How <see cref="GodotArenaSpace"/> carries out a step.</summary>
internal enum ArenaMotion
{
    /// <summary>A motion test for every step a man takes.</summary>
    Every,

    /// <summary>
    /// A motion test only for a step that can touch someone — a standing body within the step's length
    /// and two radii; any other step is placed without asking the server.
    /// </summary>
    Contact,
}

/// <summary>
/// The fight's ground on Godot's physics server: one body per man, and a step that can touch someone
/// is a motion test.
/// </summary>
/// <remarks>
/// <para>
/// The engine's side of <see cref="IArenaSpace"/> (docs/PLAN-ENGINE-RESOLUTION.md, Phase B). Where the
/// resolver's own kinematics let a man walk into another and then push him back out, here the motion
/// test stops him at the other's body and the rest of the step slides along it. A body is a circle
/// <see cref="CombatTuning.PersonalSpace"/> across, so two men touch where the old push put them.
/// </para>
/// <para>
/// It keeps the rules Phase A measured (docs/engine-bed/2026-10-phase-a/) and adds what the real fight
/// taught (docs/engine-bed/2026-10-phase-b-space/). The bodies are static bodies on the server, with no
/// nodes: a node moved for the picture would hand its place to the server at the frame's flush, behind
/// the fight's back. A man who means to stay costs nothing, and under <see cref="ArenaMotion.Contact"/>
/// neither does a step that cannot reach anyone. A body's place is handed to the server only when a
/// motion test is about to read it (<see cref="Hand"/>), because that hand-over is the dearest call the
/// space makes.
/// </para>
/// <para>
/// <b>Every fight gets a fresh physics space, and the engine never steps it.</b> The server's broadphase
/// is a tree built up move by move, and when a step touches two bodies at once the tree's shape decides
/// which one the test reports. A space carried from fight to fight made a seed's fight depend, now and
/// then, on the fights before it; a space the engine steps made it depend on whether the engine was
/// running. A new, inactive space for each fight makes the fight a function of its seed alone — the same
/// in a sweep, alone, or one tick a physics frame.
/// </para>
/// <para>
/// A fleer is placed where his step ends without a test: nobody stops him. The arena's edge is still a
/// clamp, not a wall; walls authored in the editor come with the arena scene (Phase F). One instance
/// serves fight after fight but never two at once; it holds server objects, so it is disposed.
/// </para>
/// </remarks>
internal sealed class GodotArenaSpace : IArenaSpace, IDisposable
{
    private const uint FighterLayer = 1;

    private readonly CombatTuning _tuning;
    private readonly ArenaMotion _motion;
    private readonly CircleShape2D _shape;
    private readonly PhysicsTestMotionParameters2D _test = new();
    private readonly PhysicsTestMotionResult2D _tested = new();
    private readonly List<Rid> _bodies = [];
    private Rid _space;
    private Vector2[] _where = [];
    private ArenaPoint[] _at = [];
    private bool[] _standing = [];
    private bool[] _stale = [];

    public GodotArenaSpace(CombatTuning tuning, ArenaMotion motion = ArenaMotion.Contact)
    {
        _tuning = tuning;
        _motion = motion;
        _shape = new CircleShape2D { Radius = (float)(tuning.PersonalSpace / 2) };
    }

    /// <summary>Motion tests asked of the server, over every fight this space has carried.</summary>
    public long MotionTests { get; private set; }

    /// <summary>Places handed to the server (<see cref="Hand"/>), over every fight this space has carried.</summary>
    public long Handovers { get; private set; }

    /// <summary>Where the time goes, when the bed asks; null otherwise.</summary>
    internal Bed.FightCosts? Costs { get; set; }

    public void Begin(int bodies)
    {
        // Out of the old space before it goes, so the server frees an empty one.
        foreach (Rid body in _bodies)
        {
            PhysicsServer2D.BodySetSpace(body, default);
        }

        if (_space.IsValid)
        {
            PhysicsServer2D.FreeRid(_space);
        }

        // Created inactive, and left so: the engine's step never touches it.
        _space = PhysicsServer2D.SpaceCreate();

        while (_bodies.Count < bodies)
        {
            Rid body = PhysicsServer2D.BodyCreate();
            PhysicsServer2D.BodySetMode(body, PhysicsServer2D.BodyMode.Static);
            PhysicsServer2D.BodyAddShape(body, _shape.GetRid());
            _bodies.Add(body);
        }

        if (_at.Length != bodies)
        {
            _at = new ArenaPoint[bodies];
            _where = new Vector2[bodies];
            _standing = new bool[bodies];
            _stale = new bool[bodies];
        }

        Array.Fill(_standing, false);
        Array.Fill(_stale, false);
    }

    public ArenaPoint Enter(int body, ArenaPoint at)
    {
        Rid rid = _bodies[body];
        var place = new Vector2((float)at.X, (float)at.Y);

        // Placed before it joins the space, so the broadphase first sees it where it stands; joined in the
        // battle's order, so every fight's tree starts the same.
        SetTransform(rid, place);
        PhysicsServer2D.BodySetCollisionLayer(rid, FighterLayer);
        PhysicsServer2D.BodySetCollisionMask(rid, FighterLayer);
        PhysicsServer2D.BodySetSpace(rid, _space);

        _standing[body] = true;
        _where[body] = place;
        _at[body] = new ArenaPoint(place.X, place.Y);
        return _at[body];
    }

    public ArenaPoint Settle(int body, ArenaPoint intended, bool passing)
    {
        if (passing)
        {
            return Place(body, new Vector2((float)intended.X, (float)intended.Y));
        }

        if (intended == _at[body])
        {
            return _at[body];
        }

        long started = Costs is null ? 0 : Stopwatch.GetTimestamp();
        var target = new Vector2((float)intended.X, (float)intended.Y);
        Vector2 moved = _motion == ArenaMotion.Contact && ClearOfEveryone(body, (target - _where[body]).Length())
            ? target
            : Walk(body, target - _where[body]);

        var held = new Vector2(
            Math.Clamp(moved.X, 0f, (float)_tuning.ArenaWidth),
            Math.Clamp(moved.Y, 0f, (float)_tuning.ArenaDepth));
        ArenaPoint settled = Place(body, held);

        if (Costs is not null)
        {
            Bed.FightCosts.Add(ref Costs.Move, started);
        }

        return settled;
    }

    public void Leave(int body)
    {
        PhysicsServer2D.BodySetCollisionLayer(_bodies[body], 0);
        PhysicsServer2D.BodySetCollisionMask(_bodies[body], 0);
        _standing[body] = false;
    }

    public void Dispose()
    {
        foreach (Rid body in _bodies)
        {
            PhysicsServer2D.FreeRid(body);
        }

        if (_space.IsValid)
        {
            PhysicsServer2D.FreeRid(_space);
        }

        _bodies.Clear();
        _space = default;
        _tested.Dispose();
        _test.Dispose();
        _shape.Dispose();
    }

    private static void SetTransform(Rid body, Vector2 at)
    {
        // A Transform2D travels as a heap-backed Variant with a finaliser behind it; disposed here, the
        // finaliser never runs.
        using Variant transform = Variant.From(new Transform2D(0f, at));
        PhysicsServer2D.BodySetState(body, PhysicsServer2D.BodyState.Transform, transform);
    }

    /// <summary>The step as a motion test takes it, with what is left sliding along the body it met.</summary>
    private Vector2 Walk(int body, Vector2 motion)
    {
        Vector2 from = _where[body];
        bool bumped = TestMotion(body, from, motion);
        Vector2 at = from + _tested.GetTravel();

        if (bumped)
        {
            Vector2 rest = _tested.GetRemainder().Slide(_tested.GetCollisionNormal());
            if (rest.LengthSquared() > 1e-6f)
            {
                TestMotion(body, at, rest);
                at += _tested.GetTravel();
            }
        }

        return at;
    }

    private bool TestMotion(int body, Vector2 from, Vector2 motion)
    {
        Hand();
        MotionTests++;
        _test.From = new Transform2D(0f, from);
        _test.Motion = motion;
        return PhysicsServer2D.BodyTestMotion(_bodies[body], _test, _tested);
    }

    /// <summary>Can no standing body be touched by a step this long? Conservative by a unit.</summary>
    private bool ClearOfEveryone(int body, float step)
    {
        float clear = step + (float)_tuning.PersonalSpace + 1f;
        for (int other = 0; other < _where.Length; other++)
        {
            if (other != body && _standing[other] && (_where[other] - _where[body]).LengthSquared() < clear * clear)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Takes the body's new place; the server hears of it when a motion test is about to read it.</summary>
    private ArenaPoint Place(int body, Vector2 at)
    {
        _where[body] = at;
        _at[body] = new ArenaPoint(at.X, at.Y);
        _stale[body] = true;
        return _at[body];
    }

    /// <summary>Hands the server every place it has not heard of yet: a motion test reads them all.</summary>
    /// <remarks>
    /// Telling the server where a body stands is the dearest call the space makes — dearer than the
    /// motion test itself — and only a motion test ever reads it. So a body that moves ten ticks with
    /// nobody near is handed over once, when somebody near finally asks.
    /// </remarks>
    private void Hand()
    {
        for (int body = 0; body < _stale.Length; body++)
        {
            if (_stale[body])
            {
                _stale[body] = false;
                Handovers++;
                SetTransform(_bodies[body], _where[body]);
            }
        }
    }
}
