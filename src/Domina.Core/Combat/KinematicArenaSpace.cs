namespace Domina.Core.Combat;

/// <summary>
/// The engine-free resolver's own kinematics: overlapping men are pushed apart, the arena's depth and
/// width hold everyone but a fleer.
/// </summary>
/// <remarks>
/// This is the code that lived in <see cref="Battle"/> as <c>Separate</c> and <c>Clamp</c> until the
/// <see cref="IArenaSpace"/> door was cut, moved without a change: a fight over it is the fight it was.
/// </remarks>
public sealed class KinematicArenaSpace : IArenaSpace
{
    private readonly CombatTuning _tuning;
    private ArenaPoint[] _at = [];
    private bool[] _standing = [];

    public KinematicArenaSpace(CombatTuning tuning)
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
        _at[body] = intended;

        // The fleer is neither pushed nor held: his path must not be blocked, and only he leaves the arena.
        if (!passing)
        {
            Separate(body);
            Clamp(body);
        }

        return _at[body];
    }

    public void Leave(int body) => _standing[body] = false;

    /// <summary>Pushes the man out of everyone he overlaps, one after another in the battle's order.</summary>
    private void Separate(int body)
    {
        for (int other = 0; other < _at.Length; other++)
        {
            if (other == body || !_standing[other])
            {
                continue;
            }

            double gap = _at[body].DistanceTo(_at[other]);
            if (gap >= _tuning.PersonalSpace)
            {
                continue;
            }

            // A fixed direction is needed to separate two warriors who land exactly on top of each
            // other; otherwise the direction vector is zero and both lock up.
            _at[body] = gap <= double.Epsilon
                ? new ArenaPoint(_at[body].X - (_tuning.PersonalSpace / 2), _at[body].Y)
                : _at[body].MovedAwayFrom(_at[other], _tuning.PersonalSpace - gap);
        }
    }

    /// <summary>Depth cannot spill outside the arena, nor can the line.</summary>
    private void Clamp(int body)
    {
        double y = Math.Clamp(_at[body].Y, 0, _tuning.ArenaDepth);
        double x = Math.Clamp(_at[body].X, 0, _tuning.ArenaWidth);
        _at[body] = new ArenaPoint(x, y);
    }
}
