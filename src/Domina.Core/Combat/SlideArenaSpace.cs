namespace Domina.Core.Combat;

/// <summary>
/// The fight's ground: a body stops where it meets another and keeps only the sideways part of its step.
/// </summary>
/// <remarks>
/// <para>
/// The rule real bodies follow, and the one Godot's physics server carries out (Open Decision #25). A man
/// who means to step somewhere is first put out of anyone he already overlaps, then walks the step as a
/// circle <see cref="CombatTuning.PersonalSpace"/> across until he meets the first body in his way; what is
/// left of the step slides along that body, once, and stops at the next one it meets. The arena's depth and
/// width hold everyone but a fleer, who passes through.
/// </para>
/// <para>
/// It replaced <see cref="KinematicArenaSpace"/>'s push-out as the default on 2026-10-02. The old rule let a
/// man walk his whole step and then pushed him out of whoever he had walked into, so men flowed round each
/// other; under this one they queue, and the side with more men loses part of its numbers. The engine's
/// space (<c>GodotArenaSpace</c>) gives the same numbers on every bed of the parity record
/// (docs/engine-bed/2026-10-phase-b-parity/), so the core measures what the engine plays.
/// </para>
/// </remarks>
public sealed class SlideArenaSpace : IArenaSpace
{
    /// <summary>
    /// A motion this close to running along another's edge is not running into him: a slide starts at the
    /// point of contact, and without the margin its own rounding would stop it.
    /// </summary>
    private const double Tangent = 1e-6;

    private readonly CombatTuning _tuning;
    private ArenaPoint[] _at = [];
    private bool[] _standing = [];

    public SlideArenaSpace(CombatTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        _tuning = tuning;
    }

    public void Begin(int bodies)
    {
        if (_at.Length != bodies)
        {
            _at = new ArenaPoint[bodies];
            _standing = new bool[bodies];
        }

        Array.Fill(_standing, false);
    }

    public ArenaPoint Enter(int body, ArenaPoint at)
    {
        _at[body] = at;
        _standing[body] = true;
        return at;
    }

    public ArenaPoint Settle(int body, ArenaPoint intended, bool passing)
    {
        // The fleer is neither stopped nor held: his path must not be blocked, and only he leaves the arena.
        if (passing || intended == _at[body])
        {
            _at[body] = intended;
            return intended;
        }

        double moveX = intended.X - _at[body].X;
        double moveY = intended.Y - _at[body].Y;
        ArenaPoint from = OutOfEveryone(body);

        (double share, double normalX, double normalY) = Sweep(body, from, moveX, moveY);
        double x = from.X + (moveX * share);
        double y = from.Y + (moveY * share);

        if (share < 1)
        {
            // What is left of the step, less the part that runs into the body met.
            double restX = moveX * (1 - share);
            double restY = moveY * (1 - share);
            double into = (restX * normalX) + (restY * normalY);
            restX -= into * normalX;
            restY -= into * normalY;

            if ((restX * restX) + (restY * restY) > 1e-6)
            {
                (double slid, _, _) = Sweep(body, new ArenaPoint(x, y), restX, restY);
                x += restX * slid;
                y += restY * slid;
            }
        }

        _at[body] = new ArenaPoint(Math.Clamp(x, 0, _tuning.ArenaWidth), Math.Clamp(y, 0, _tuning.ArenaDepth));
        return _at[body];
    }

    public void Leave(int body) => _standing[body] = false;

    /// <summary>Where the man stands once he is out of everyone he overlaps, one after another in the battle's order.</summary>
    /// <remarks>
    /// Men overlap only where a rule put them so — a fleer passing through, the formation, the arena's edge —
    /// and a body pushed out of them before it moves is what the physics server does too.
    /// </remarks>
    private ArenaPoint OutOfEveryone(int body)
    {
        ArenaPoint at = _at[body];
        for (int other = 0; other < _at.Length; other++)
        {
            if (other == body || !_standing[other])
            {
                continue;
            }

            double gap = at.DistanceTo(_at[other]);
            if (gap >= _tuning.PersonalSpace)
            {
                continue;
            }

            // Two men on the very same spot have no line between them; a fixed direction parts them.
            at = gap <= double.Epsilon
                ? new ArenaPoint(at.X - (_tuning.PersonalSpace / 2), at.Y)
                : at.MovedAwayFrom(_at[other], _tuning.PersonalSpace - gap);
        }

        return at;
    }

    /// <summary>How much of the motion the body travels before it meets someone, and that body's normal.</summary>
    /// <returns>The share travelled (1 if nobody is met) and the unit normal at the point of contact.</returns>
    private (double Share, double NormalX, double NormalY) Sweep(int body, ArenaPoint from, double moveX, double moveY)
    {
        double size = _tuning.PersonalSpace;
        double length2 = (moveX * moveX) + (moveY * moveY);
        double margin = Tangent * Math.Sqrt(length2) * size;
        double best = 1;
        double normalX = 0;
        double normalY = 0;

        for (int other = 0; other < _at.Length; other++)
        {
            if (other == body || !_standing[other])
            {
                continue;
            }

            double apartX = from.X - _at[other].X;
            double apartY = from.Y - _at[other].Y;
            double closing = (apartX * moveX) + (apartY * moveY);

            // Moving away from him, or along his edge: nothing to meet.
            if (closing >= -margin)
            {
                continue;
            }

            double overlap = (apartX * apartX) + (apartY * apartY) - (size * size);
            double share;
            if (overlap <= 0)
            {
                share = 0;
            }
            else
            {
                double disc = (closing * closing) - (length2 * overlap);
                if (disc < 0)
                {
                    continue;
                }

                share = (-closing - Math.Sqrt(disc)) / length2;
                if (share < 0 || share > best)
                {
                    continue;
                }
            }

            best = share;
            double touchX = from.X + (moveX * share) - _at[other].X;
            double touchY = from.Y + (moveY * share) - _at[other].Y;
            double reach = Math.Sqrt((touchX * touchX) + (touchY * touchY));
            (normalX, normalY) = reach > 0 ? (touchX / reach, touchY / reach) : (0, 0);
        }

        return (best, normalX, normalY);
    }
}
