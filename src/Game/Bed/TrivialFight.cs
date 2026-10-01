using System.Diagnostics;
using Domina.Core.Combat;
using Domina.Core.Rng;
using Godot;

namespace Domina.Game.Bed;

/// <summary>
/// The harness's stand-in fight: three against three, built from Godot nodes, resolved on the core's tick.
/// </summary>
/// <remarks>
/// <para>
/// The plan's Phase A asks for the harness to be measured against a trivial fight <b>before</b>
/// <c>Battle.cs</c> is touched (docs/PLAN-ENGINE-RESOLUTION.md, 3.1). Trivial in its rules, not in its
/// shape: it is sized like <c>ThroughputTests</c>' 3v3 (six men, about twenty seconds a fight) and it
/// uses every engine piece the real fight would — bodies on the physics server, movement by motion
/// test (<c>MoveAndCollide</c>), the blade as a shape query, the blow on a clip's method key, the seeded
/// stream for the rolls. What the bed reads off it is what those pieces cost and whether they repeat.
/// </para>
/// <para>
/// A moved body's transform is pushed to the physics server by hand. Godot sends a node's new transform
/// to the server through a notification the scene tree flushes once a frame, and the manual lane runs a
/// whole fight inside one frame — without the push every query would see the bodies where they stood
/// when the frame began.
/// </para>
/// </remarks>
internal sealed class TrivialFight
{
    public static readonly double TickSeconds = CombatTuning.Default.TickSeconds;

    private const float Speed = 90f;
    private const double HitChance = 0.6;
    private const int StallTicks = 2400;

    private static readonly double[] _health = [100, 100, 100, 140, 70, 80];

    private readonly TrivialKit _kit;
    private readonly BedClip _clip;
    private readonly BedBlade _blade;
    private readonly TrivialFighter[] _fighters;
    private readonly PhysicsDirectSpaceState2D _space;
    private SeededRandom _random = new(0);
    private int _tick;

    public TrivialFight(Node parent, TrivialKit kit, BedOptions options)
    {
        _kit = kit;
        _clip = options.Clip;
        _blade = options.Blade;
        Root = new Node2D { Name = "TrivialFight" };
        parent.AddChild(Root);

        _fighters = new TrivialFighter[_health.Length];
        for (int i = 0; i < _fighters.Length; i++)
        {
            int team = i < 3 ? 0 : 1;
            var start = new Vector2(team == 0 ? -220f : 220f, -70f + (70f * (i % 3)));
            _fighters[i] = new TrivialFighter(this, Root, kit, options.Body, i, team, _health[i], start);
        }

        _space = PhysicsServer2D.SpaceGetDirectState(Root.GetWorld2D().Space);
    }

    public Node2D Root { get; }

    public bool Finished { get; private set; }

    /// <summary>0 or 1 for the side left standing, -1 for a fight stopped on the time limit.</summary>
    public int Winner { get; private set; }

    public int Ticks => _tick;

    public ulong Digest { get; private set; }

    public int Blows { get; private set; }

    public int Hits { get; private set; }

    /// <summary>Where the time goes, when the bed asks (<c>--profile</c>); null otherwise.</summary>
    public FightCosts? Costs { get; set; }

    /// <summary>Puts every man back on his mark and starts the fight of <paramref name="seed"/>.</summary>
    public void Reset(ulong seed)
    {
        long started = Costs is null ? 0 : Stopwatch.GetTimestamp();
        ResetCore(seed);
        if (Costs is not null)
        {
            FightCosts.Add(ref Costs.Reset, started);
        }
    }

    private void ResetCore(ulong seed)
    {
        _random = new SeededRandom(seed);
        _tick = 0;
        Finished = false;
        Winner = -1;
        Digest = FightDigest.Empty;
        Blows = 0;
        Hits = 0;

        foreach (TrivialFighter f in _fighters)
        {
            f.Health = f.MaxHealth;
            f.Alive = true;
            f.Target = null;
            f.SwingTicks = -1;
            f.Landed = false;
            f.Player.Stop();
            f.Blade.Rotation = TrivialKit.RestAngle;
            f.Body.CollisionLayer = f.Layer;
            f.Body.GlobalPosition = f.Start;
            Place(f);
        }
    }

    /// <summary>One tick of the fight: every man still standing acts once, in a fixed order.</summary>
    public void Step()
    {
        if (Finished)
        {
            return;
        }

        _tick++;
        foreach (TrivialFighter f in _fighters)
        {
            if (f.Alive)
            {
                Act(f);
            }
        }

        bool sideZero = false;
        bool sideOne = false;
        foreach (TrivialFighter f in _fighters)
        {
            sideZero |= f.Alive && f.Team == 0;
            sideOne |= f.Alive && f.Team == 1;
        }

        if (!sideZero || !sideOne)
        {
            Finish(sideZero ? 0 : sideOne ? 1 : -1);
        }
        else if (_tick >= StallTicks)
        {
            Finish(-1);
        }
    }

    /// <summary>The blow of <paramref name="striker"/>'s swing lands — the frame the clip's key names.</summary>
    public void Land(TrivialFighter striker)
    {
        Blows++;
        TrivialFighter? target = striker.Target;
        ulong digest = FightDigest.Mix(FightDigest.Mix(Digest, _tick), striker.Index);

        if (target is not { Alive: true } || !BladeFinds(striker, target))
        {
            Digest = FightDigest.Mix(digest, -1L);
            return;
        }

        digest = FightDigest.Mix(digest, target.Index);
        if (!_random.Chance(HitChance))
        {
            Digest = FightDigest.Mix(digest, 0L);
            return;
        }

        int damage = 8 + _random.NextInt(14);
        Hits++;
        target.Health -= damage;
        Digest = FightDigest.Mix(digest, damage);

        if (target.Health <= 0)
        {
            target.Alive = false;
            target.Target = null;
            target.Body.CollisionLayer = 0;
            if (target.SwingTicks >= 0)
            {
                target.SwingTicks = -1;
                target.Player.Stop();
            }
        }
    }

    private void Act(TrivialFighter f)
    {
        if (f.SwingTicks >= 0)
        {
            f.SwingTicks++;
            if (_clip == BedClip.Mixer)
            {
                long started = Costs is null ? 0 : Stopwatch.GetTimestamp();
                f.Player.Advance(TickSeconds);
                if (Costs is not null)
                {
                    FightCosts.Add(ref Costs.Clip, started);
                }
            }
            else if (!f.Landed && f.SwingTicks * TickSeconds >= _kit.LandSeconds)
            {
                f.Landed = true;
                Land(f);
            }

            if (f.SwingTicks >= _kit.ClipTicks)
            {
                f.SwingTicks = -1;
                if (_clip == BedClip.Mixer)
                {
                    f.Player.Stop();
                }
            }

            return;
        }

        if (f.Target is not { Alive: true })
        {
            f.Target = Nearest(f);
            if (f.Target is null)
            {
                return;
            }
        }

        Vector2 toTarget = f.Target.Position - f.Position;
        if (toTarget.LengthSquared() > TrivialKit.Reach * TrivialKit.Reach)
        {
            Move(f, toTarget.Normalized() * (float)(Speed * TickSeconds));
            return;
        }

        f.SwingTicks = 0;
        f.Landed = false;
        if (_clip == BedClip.Mixer)
        {
            f.Player.Play(TrivialKit.StrikeName);
        }
    }

    private void Move(TrivialFighter f, Vector2 motion)
    {
        long started = Costs is null ? 0 : Stopwatch.GetTimestamp();
        MoveCore(f, motion);
        if (Costs is not null)
        {
            FightCosts.Add(ref Costs.Move, started);
        }
    }

    private static void MoveCore(TrivialFighter f, Vector2 motion)
    {
        using (KinematicCollision2D? bump = f.Body.MoveAndCollide(motion))
        {
            if (bump is not null)
            {
                Vector2 rest = bump.GetRemainder().Slide(bump.GetNormal());
                if (rest.LengthSquared() > 1e-6f)
                {
                    f.Body.MoveAndCollide(rest)?.Dispose();
                }
            }
        }

        Place(f);
    }

    /// <summary>Hands the body's transform to the physics server now, not at the frame's flush.</summary>
    private static void Place(TrivialFighter f)
    {
        Transform2D placed = f.Body.GlobalTransform;
        f.Position = placed.Origin;
        PhysicsServer2D.BodySetState(f.Rid, PhysicsServer2D.BodyState.Transform, placed);
    }

    private bool BladeFinds(TrivialFighter striker, TrivialFighter target)
    {
        long started = Costs is null ? 0 : Stopwatch.GetTimestamp();
        bool found = BladeFindsCore(striker, target);
        if (Costs is not null)
        {
            FightCosts.Add(ref Costs.Blade, started);
        }

        return found;
    }

    private bool BladeFindsCore(TrivialFighter striker, TrivialFighter target)
    {
        if (_blade == BedBlade.Shape)
        {
            return _kit.BladeShape.Collide(
                new Transform2D(0f, striker.Position), _kit.BodyShape, new Transform2D(0f, target.Position));
        }

        striker.BladeQuery.Transform = new Transform2D(0f, striker.Position);
        Godot.Collections.Array<Godot.Collections.Dictionary> found = _space.IntersectShape(striker.BladeQuery, 8);
        foreach (Godot.Collections.Dictionary hit in found)
        {
            if ((Rid)hit["rid"] == target.Rid)
            {
                return true;
            }
        }

        return false;
    }

    private TrivialFighter? Nearest(TrivialFighter f)
    {
        TrivialFighter? nearest = null;
        float best = float.MaxValue;
        foreach (TrivialFighter other in _fighters)
        {
            if (other.Team == f.Team || !other.Alive)
            {
                continue;
            }

            float distance = (other.Position - f.Position).LengthSquared();
            if (distance < best)
            {
                best = distance;
                nearest = other;
            }
        }

        return nearest;
    }

    private void Finish(int winner)
    {
        Finished = true;
        Winner = winner;

        ulong digest = FightDigest.Mix(FightDigest.Mix(Digest, winner), _tick);
        foreach (TrivialFighter f in _fighters)
        {
            digest = FightDigest.Mix(digest, f.Health);
            digest = FightDigest.Mix(digest, f.Position.X);
            digest = FightDigest.Mix(digest, f.Position.Y);
        }

        Digest = digest;
    }
}
