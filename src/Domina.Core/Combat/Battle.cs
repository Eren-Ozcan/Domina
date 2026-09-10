using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Combat;

/// <summary>
/// Deterministic simulation of a single fight.
/// </summary>
/// <remarks>
/// <para>
/// It advances step by step (<see cref="Step"/>). The Godot layer steps it in sync with
/// animation; batch simulation runs it to the end with <see cref="Run"/>.
/// Same seed + same input = same result.
/// </para>
/// <para>
/// <b>It does NOT change the warriors' persistent state.</b> Death and maiming outcomes are
/// reported inside <see cref="BattleResult"/>; writing them into persistent state is the meta
/// layer's job. Otherwise tens of thousands of fights could not be simulated with the same warrior object.
/// </para>
/// </remarks>
public sealed class Battle
{
    private readonly List<Combatant> _combatants = [];
    private readonly List<BattleEvent> _events = [];
    private readonly BattleSetup _setup;
    private readonly CombatTuning _tuning;
    private readonly IRandomSource _rng;
    /// <summary>
    /// The limbs lost in this fight, per warrior.
    /// </summary>
    /// <remarks>
    /// A <b>set</b> is kept per warrior, not a single part: a surrounded warrior takes an
    /// opportunity attack from every enemy in reach while fleeing (§5), so he can lose more
    /// than one limb in a single fight. With a single part kept, every new loss erased the
    /// previous one, and because <see cref="AlreadyLost"/> only looked at the last one the
    /// same limb could come off over and over (measured: 22 severings on one warrior,
    /// arm/leg in turn).
    /// </remarks>
    private readonly Dictionary<WarriorId, BodyPartSet> _lostParts = [];

    /// <summary>
    /// The armour pieces destroyed during the fight — per warrior.
    /// </summary>
    /// <remarks>
    /// It travels the same route as limb loss: the core does not change persistent state, it
    /// writes what happened into the fight summary; striking the armour off the books is the dojo layer's job.
    /// </remarks>
    private readonly Dictionary<WarriorId, HitLocationSet> _destroyedArmor = [];

    /// <summary>
    /// Projectiles in the air. They stay in the order they were thrown and resolve in order.
    /// </summary>
    /// <remarks>
    /// The ordering is required for determinism: if two projectiles arrive on the same tick,
    /// which one resolves first must be fixed, or the same seed does not give the same fight.
    /// </remarks>
    private readonly List<Projectile> _projectiles = [];

    /// <summary>
    /// Weapons that fell out of a hand and lie in the arena.
    /// </summary>
    /// <remarks>
    /// A dropped weapon does not leave the fight, it only leaves <b>its owner</b>; anyone left
    /// unarmed can walk to it (see <see cref="GroundWeapon"/>).
    /// </remarks>
    private readonly List<GroundWeapon> _dropped = [];

    /// <summary>
    /// Has the first hit landed? This is the threshold that unlocks the flee key.
    /// </summary>
    /// <remarks>
    /// The threshold is a <b>hit</b>, not a move: the fight does not count as begun because a
    /// sword went up, it counts as begun because blood was drawn. Through missed moves the team
    /// can still be pulled out cheaply — that is what armour buys (see docs/GDD.md §5).
    /// </remarks>
    private bool _contactMade;

    /// <summary>Consecutive refused "pull out" presses before contact.</summary>
    private int _refusedRetreatPresses;

    public Battle(BattleSetup setup, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(rng);

        if (setup.PlayerSide.Count == 0 || setup.EnemySide.Count == 0)
        {
            throw new ArgumentException("Both sides must have at least one warrior.", nameof(setup));
        }

        _setup = setup;
        _tuning = setup.Tuning;
        _rng = rng;

        foreach (Warrior w in setup.PlayerSide)
        {
            _combatants.Add(new Combatant(w, PlayerTeam));
        }

        foreach (Warrior w in setup.EnemySide)
        {
            _combatants.Add(new Combatant(w, EnemyTeam));
        }

        PlaceCombatants();

        foreach (Combatant c in _combatants)
        {
            c.BeginState(CombatState.Idle, SpacingSeconds(c));
        }

        Emit(new BattleStarted(0));
    }

    public const int PlayerTeam = 0;
    public const int EnemyTeam = 1;

    public double ElapsedSeconds { get; private set; }

    public bool IsFinished { get; private set; }

    /// <summary>Filled in when the fight ends.</summary>
    public BattleResult? Result { get; private set; }

    /// <summary>
    /// The event stream produced. Empty if <see cref="BattleSetup.CollectEvents"/> is off.
    /// </summary>
    public IReadOnlyList<BattleEvent> Events => _events;

    /// <summary>
    /// True if the first hit has landed — that is, <see cref="CommandRetreat"/> is now accepted.
    /// </summary>
    /// <remarks>
    /// The interface draws the key inactive before that. It is a <b>fight-level</b> flag, not a
    /// per-warrior one: it does not matter which side struck, first blood starts the fight.
    /// </remarks>
    public bool ContactMade => _contactMade;

    /// <summary>How many times in a row the key was pressed before contact.</summary>
    public int RefusedRetreatPresses => _refusedRetreatPresses;

    /// <summary>
    /// The warriors' current state. The Godot layer prints it to the HUD (health/stamina bars,
    /// which warrior the "pull out" key is active for).
    /// </summary>
    public IReadOnlyList<CombatantSnapshot> Snapshots() => _combatants.ConvertAll(Snapshot);

    /// <summary>The current state of a single warrior.</summary>
    public CombatantSnapshot SnapshotOf(WarriorId id)
    {
        Combatant c = _combatants.Find(x => x.Id == id)
                      ?? throw new ArgumentException($"No such warrior in the fight: {id}", nameof(id));

        return Snapshot(c);
    }

    private static CombatantSnapshot Snapshot(Combatant c) => new(
        c.Id,
        c.Team,
        c.State,
        Math.Max(0, c.Health),
        c.Stamina,
        c.Warrior.EffectiveStats.MaxHealth,
        c.Warrior.EffectiveStats.MaxStamina,
        c.RetreatRequested,
        c.StateProgress,
        c.IsCancellable,
        c.Target is { IsActive: true } t ? t.Id : null,
        c.Position,
        c.Facing,
        c.SpeedThisTick,
        c.IsPoisoned,
        c.Disarmed,
        c.DestroyedArmor);

    /// <summary>
    /// The player's "pull out" key — it pulls <b>the whole team</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single warrior cannot be pulled out separately (see docs/GDD.md §5). Were it
    /// per-warrior, the right play would be "pull the moment someone is wounded, continue with
    /// the rest" — a small, losslessly repeatable optimisation. A team-level command makes the
    /// decision rare and heavy.
    /// </para>
    /// <para>
    /// The command is resolved <b>separately</b> for each warrior: the one with his sword in the air
    /// is buffered, the one who is idle starts fleeing immediately. So one key press can take effect at three different moments.
    /// </para>
    /// </remarks>
    /// <returns>True if at least one warrior accepted the command.</returns>
    public bool CommandRetreat()
    {
        if (!_contactMade)
        {
            // The fight has not begun: no pulling out before anyone is touched (§5).
            _refusedRetreatPresses++;
            Emit(new RetreatRefused(ElapsedSeconds, _refusedRetreatPresses));
            return false;
        }

        bool accepted = false;

        foreach (Combatant c in _combatants)
        {
            if (c.Team == PlayerTeam)
            {
                accepted |= CommandRetreat(c);
            }
        }

        return accepted;
    }

    private bool CommandRetreat(Combatant c)
    {
        if (!c.IsActive || c.State == CombatState.Retreating || c.RetreatRequested)
        {
            return false;
        }

        c.RetreatRequested = true;
        Emit(new RetreatCommanded(ElapsedSeconds, c.Id));

        if (c.IsCancellable)
        {
            BeginRetreat(c);
        }
        else
        {
            // Sword in the air — he cannot flee before the current strike finishes.
            Emit(new RetreatBuffered(ElapsedSeconds, c.Id));
        }

        return true;
    }

    /// <summary>Advances one step.</summary>
    /// <returns>True if the fight continues, false if it has ended.</returns>
    public bool Step()
    {
        if (IsFinished)
        {
            return false;
        }

        ElapsedSeconds += _tuning.TickSeconds;

        Move();

        // Projectiles advance before the warriors: after movement, before moves.
        // A projectile in the air finds where it arrives this tick from the target's NEW position.
        AdvanceProjectiles();

        if (Finish())
        {
            return false;
        }

        foreach (Combatant c in _combatants)
        {
            if (!c.IsActive)
            {
                continue;
            }

            TickPoison(c);

            if (!c.IsActive)
            {
                // If poison finished him, this warrior's turn closes here; the remaining moves
                // would be the moves of a dead warrior.
                if (Finish())
                {
                    return false;
                }

                continue;
            }

            RegenerateStamina(c);
            ConsultRetreatPolicy(c);
            AdvanceState(c);

            if (Finish())
            {
                return false;
            }
        }

        if (ElapsedSeconds >= _tuning.StallGuardSeconds)
        {
            // The guard, not a rule: nobody could close or finish. See CombatTuning.StallGuardSeconds.
            Complete(BattleOutcome.Stalled);
            return false;
        }

        return true;
    }

    /// <summary>Runs the fight to the end.</summary>
    public BattleResult Run()
    {
        while (Step())
        {
            // Step() checks the end condition itself.
        }

        return Result!;
    }

    // -------------------------------------------------------------- movement

    /// <summary>
    /// Starting formation: the two sides facing each other, each spread out in depth.
    /// </summary>
    private void PlaceCombatants()
    {
        double centerX = _tuning.ArenaWidth / 2;
        double centerY = _tuning.ArenaDepth / 2;

        int player = 0;
        int enemy = 0;

        foreach (Combatant c in _combatants)
        {
            bool isPlayer = c.Team == PlayerTeam;
            int index = isPlayer ? player++ : enemy++;
            int count = isPlayer ? _setup.PlayerSide.Count : _setup.EnemySide.Count;

            // Centres the roster in depth: 3 people spread over the range -1, 0, +1.
            double lane = index - ((count - 1) / 2.0);

            c.Position = new ArenaPoint(
                centerX + (isPlayer ? -_tuning.StartOffsetX : _tuning.StartOffsetX),
                centerY + (lane * _tuning.StartSpacingY));

            c.Facing = isPlayer ? 1 : -1;
        }
    }

    /// <summary>
    /// Advances everyone by one tick: closing on the target, fleeing, and the push that
    /// prevents overlap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A warrior locked into an attack does not walk.</b> That is the price of committing to a
    /// strike: once you have started the move you are nailed in place even if the target flees.
    /// </para>
    /// <para>
    /// There is no physics engine, we have our own kinematics — Godot's collision resolution
    /// changes from version to version and would break the "same seed = same fight" guarantee.
    /// </para>
    /// </remarks>
    private void Move()
    {
        foreach (Combatant c in _combatants)
        {
            ArenaPoint before = c.Position;

            if (!c.IsActive)
            {
                c.SpeedThisTick = 0;
                continue;
            }

            double step = StepFor(c);

            if (c.State is CombatState.Retreating)
            {
                // The destination is beyond the exit threshold too, so the warrior does not stop on it.
                double exitX = c.Team == PlayerTeam
                    ? -(_tuning.ExitMargin * 2)
                    : _tuning.ArenaWidth + (_tuning.ExitMargin * 2);

                c.Position = c.Position.MovedToward(new ArenaPoint(exitX, c.Position.Y), step);
            }
            else if (c.Unarmed && !c.RetreatRequested && NearestDropped(c) is GroundWeapon dropped)
            {
                // An empty-handed warrior's first job is to find a weapon: instead of fighting with
                // his fists he walks to the blade on the ground. While walking he is not defenceless,
                // but he does not strike either — this walk is where the price is paid.
                if (c.State is CombatState.Idle)
                {
                    c.Position = c.Position.MovedToward(dropped.Position, step);
                }

                TryPickUp(c);
            }
            else if (FindTarget(c) is Combatant target && CanAdvanceOn(c, target))
            {
                FaceToward(c, target);

                double reach = c.Weapon.Reach * _tuning.PreferredReachFraction;
                double gap = c.Position.DistanceTo(target.Position);

                if (gap > reach)
                {
                    c.Position = c.Position.MovedToward(target.Position, Math.Min(step, gap - reach));
                }
            }

            Separate(c);
            Clamp(c);
            c.SpeedThisTick = before.DistanceTo(c.Position) / _tuning.TickSeconds;
        }
    }

    /// <summary>
    /// The accidental wound taken while leaving the arena.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fleeing is never entirely clean: a twisted ankle, a branch taken in the dark, a wound
    /// bleeding on the way back. It <b>never kills</b> anyone — it does not push health below 1.
    /// Its purpose is not death but removing the case of "I got out without paying anything".
    /// </para>
    /// <para>
    /// This is the only non-spatial source of injury: on screen it is not a strike but a stumble.
    /// The other two sources (the enemy catching up, a throw from behind) are spatial.
    /// </para>
    /// </remarks>
    private void RollEscapeMishap(Combatant c)
    {
        if (!_rng.Chance(_tuning.EscapeMishapChance))
        {
            return;
        }

        double damage = Lerp(
            _tuning.EscapeMishapMinDamage,
            _tuning.EscapeMishapMaxDamage,
            _rng.NextDouble());

        damage = Math.Min(damage, Math.Max(0, c.Health - 1));
        if (damage <= 0)
        {
            return;
        }

        c.Health -= damage;
        c.DamageTaken += damage;
        Emit(new EscapeMishap(ElapsedSeconds, c.Id, damage, c.Health));
    }

    /// <summary>
    /// Can this warrior advance toward his target this tick?
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule is normally "if you are locked into a move you cannot walk" — the price of
    /// committing to a strike. <b>Against a fleeing target this rule is suspended:</b> the chaser
    /// keeps running while he swings his sword.
    /// </para>
    /// <para>
    /// The reason was measured. The rule was written for the case of two warriors standing and
    /// trading blows; in a chase, the hunter catches up and starts a move, freezes for the whole
    /// move, the fleer leaves reach in the meantime, and the sword landed on empty air <b>every
    /// single time</b>. So the enemy who caught up could never land a hit, and GDD §5's promise
    /// that "he stays defenceless throughout the escape" had no equivalent.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <para>
    /// Only during the <b>move</b>; not during recovery. With both open, the chaser never stops,
    /// his net speed stays above the fleer's the whole time and escape collapsed (measured: when
    /// outnumbered, 76% of the team that pulled out was wiped out entirely, instead of 30%).
    /// Stopping during recovery lets the chaser breathe and gives the fleer a chance to open the
    /// gap — the pursuit becomes a chase, not an execution.
    /// </para>
    /// </remarks>
    private static bool CanAdvanceOn(Combatant c, Combatant target) =>
        c.State is CombatState.Idle or CombatState.Charging
        || (target.State is CombatState.Retreating && c.State is CombatState.AttackWindup);

    /// <summary>
    /// The nearest weapon this warrior can pick up; <c>null</c> if there is none.
    /// </summary>
    /// <remarks>
    /// <b>A warrior with a weapon in hand does not search.</b> The caller already checks for being
    /// unarmed; the only filter here is that a warrior who has lost an arm cannot pick up a two-handed
    /// weapon — if he could, the weapon he picked up would turn into a fist in his hand and the walk would be wasted.
    /// </remarks>
    private GroundWeapon? NearestDropped(Combatant c)
    {
        GroundWeapon? best = null;
        double bestDistance = double.MaxValue;

        foreach (GroundWeapon g in _dropped)
        {
            if (!CanWield(c, g.Weapon))
            {
                continue;
            }

            double distance = c.Position.SquaredDistanceTo(g.Position);
            if (distance < bestDistance)
            {
                best = g;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Can this warrior wield this weapon?</summary>
    private static bool CanWield(Combatant c, Weapon weapon) =>
        !weapon.TwoHanded || !c.Warrior.Disabilities.Any(d => d.BlocksTwoHandedWeapons);

    /// <summary>
    /// Where the dropped weapon is flung: <b>behind</b> the other man.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The direction was decided by measurement and it carries the rule's cost <b>on its own</b>.
    /// If the weapon falls behind its owner, the walk to pick it up pulls the warrior back out of
    /// the fight and, in front of a slow enemy, disarming becomes a free breather — measured, the
    /// player's victory <b>rose</b> as the distance grew (94.30% → 94.55%; 94.25% without the
    /// rule). Flinging it sideways comes to the same thing (94.4%): a slow enemy cannot punish the
    /// warrior who leaves the line.
    /// </para>
    /// <para>
    /// When it falls behind the other man the cost is real: going to the weapon means going
    /// through the enemy, and personal space does not allow that. In a duel the weapon is
    /// effectively gone (7% pickup), in a crowd it can be recovered because the target is split
    /// (41%) — this is exactly where the rule bites.
    /// </para>
    /// </remarks>
    private ArenaPoint DropPoint(Combatant owner, Combatant past) =>
        past.Position.MovedAwayFrom(owner.Position, _tuning.WeaponDropDistance);

    /// <summary>Pulls a point inside the arena bounds — a weapon does not fall off the field.</summary>
    private ArenaPoint Clamped(ArenaPoint point) => new(
        Math.Clamp(point.X, 0, _tuning.ArenaWidth),
        Math.Clamp(point.Y, 0, _tuning.ArenaDepth));

    /// <summary>Picks up the weapon at his feet.</summary>
    /// <remarks>
    /// The moment of picking up has no duration of its own: the price is already paid — the warrior
    /// <b>walked</b> to the weapon and for that whole span he had nothing but his fists. Adding a
    /// separate window for bending down would charge the same price twice.
    /// </remarks>
    private void TryPickUp(Combatant c)
    {
        for (int i = 0; i < _dropped.Count; i++)
        {
            GroundWeapon g = _dropped[i];

            if (!CanWield(c, g.Weapon)
                || c.Position.DistanceTo(g.Position) > _tuning.WeaponPickupRadius)
            {
                continue;
            }

            _dropped.RemoveAt(i);

            c.HeldWeapon = g.Weapon;
            c.Disarmed = false;
            c.WeaponsPickedUp++;

            Emit(new WeaponPickedUp(ElapsedSeconds, c.Id, g.Weapon.Name));
            return;
        }
    }

    /// <summary>
    /// The distance a warrior covers this tick.
    /// </summary>
    /// <remarks>
    /// Speed differs from warrior to warrior; that is what decides the outcome of a chase. The fleer
    /// also slows down: someone running with his back turned cannot go faster than the man facing him.
    /// </remarks>
    private double StepFor(Combatant c)
    {
        double speed = BaseMoveSpeed(c);

        if (c.State is CombatState.Retreating)
        {
            speed *= _tuning.RetreatSpeedMultiplier;
        }
        else if (c.State is CombatState.Charging)
        {
            speed *= _tuning.ChargeSpeedMultiplier;
        }

        return speed * _tuning.TickSeconds;
    }

    private static double Lerp(double from, double to, double t) => from + ((to - from) * t);

    /// <summary>Pushes overlapping warriors apart. The fleer is not pushed — his path must not be blocked.</summary>
    private void Separate(Combatant c)
    {
        if (c.State is CombatState.Retreating)
        {
            return;
        }

        foreach (Combatant other in _combatants)
        {
            if (ReferenceEquals(other, c) || !other.IsActive)
            {
                continue;
            }

            double gap = c.Position.DistanceTo(other.Position);
            if (gap >= _tuning.PersonalSpace)
            {
                continue;
            }

            // A fixed direction is needed to separate two warriors who land exactly on top of each
            // other; otherwise the direction vector is zero and both lock up.
            c.Position = gap <= double.Epsilon
                ? new ArenaPoint(c.Position.X - (_tuning.PersonalSpace / 2), c.Position.Y)
                : c.Position.MovedAwayFrom(other.Position, _tuning.PersonalSpace - gap);
        }
    }

    /// <summary>Depth cannot spill outside the arena; along the line only the fleer leaves.</summary>
    private void Clamp(Combatant c)
    {
        if (c.State is CombatState.Retreating)
        {
            return;
        }

        double y = Math.Clamp(c.Position.Y, 0, _tuning.ArenaDepth);
        double x = Math.Clamp(c.Position.X, 0, _tuning.ArenaWidth);
        c.Position = new ArenaPoint(x, y);
    }

    private static void FaceToward(Combatant c, Combatant target)
    {
        double dx = target.Position.X - c.Position.X;
        if (Math.Abs(dx) > double.Epsilon)
        {
            c.Facing = dx >= 0 ? 1 : -1;
        }
    }

    /// <summary>Is the attacker behind the defender?</summary>
    /// <remarks>
    /// The mechanical meaning of encircling: when you are surrounded someone is necessarily
    /// behind you, and his strike is both more accurate and heavier.
    /// </remarks>
    private static bool IsFlanking(Combatant attacker, Combatant defender)
    {
        double dx = attacker.Position.X - defender.Position.X;
        return Math.Abs(dx) > double.Epsilon && Math.Sign(dx) != defender.Facing;
    }

    /// <summary>Is the warrior close enough to strike his target?</summary>
    private static bool InReach(Combatant attacker, Combatant target) =>
        attacker.Position.DistanceTo(target.Position) <= attacker.Weapon.Reach;

    /// <summary>
    /// Has the fleeing warrior really left the arena? The edge of the frame is not enough — he
    /// has to go a little further to disappear from the screen.
    /// </summary>
    private bool HasLeftArena(Combatant c) =>
        c.Team == PlayerTeam
            ? c.Position.X <= -_tuning.ExitMargin
            : c.Position.X >= _tuning.ArenaWidth + _tuning.ExitMargin;

    // ------------------------------------------------------------------ state

    private void AdvanceState(Combatant c)
    {
        c.StateTimer -= _tuning.TickSeconds;
        if (c.StateTimer > 0)
        {
            return;
        }

        switch (c.State)
        {
            case CombatState.Idle:
                if (c.RetreatRequested)
                {
                    BeginRetreat(c);
                    return;
                }

                StartAttack(c);
                break;

            case CombatState.AttackWindup:
                ResolveWindupEnd(c);
                break;

            case CombatState.ThrowWindup:
                ReleaseThrow(c);
                break;

            case CombatState.AttackRecovery:
            case CombatState.ThrowRecovery:
                // Recovery is over — a buffered flee command is processed now.
                if (c.RetreatRequested)
                {
                    BeginRetreat(c);
                    return;
                }

                c.BeginState(CombatState.Idle, SpacingSeconds(c));
                break;

            case CombatState.Blocking:
                // The stance is over: the warrior returns to the decision loop and chooses again —
                // holding the block is one option, but the die is rolled anew every time.
                if (c.RetreatRequested)
                {
                    BeginRetreat(c);
                    return;
                }

                StartAttack(c);
                break;

            case CombatState.ChargeWindup:
                LaunchCharge(c);
                break;

            case CombatState.Charging:
                AdvanceCharge(c);
                break;

            case CombatState.Stunned:
            case CombatState.WeaponBound:
                // The stun or the bind is over: the warrior returns to the normal decision loop.
                // A flee command given in the meantime is not swallowed, it is processed here.
                if (c.RetreatRequested)
                {
                    BeginRetreat(c);
                    return;
                }

                c.BeginState(CombatState.Idle, SpacingSeconds(c));
                break;

            case CombatState.Retreating:
                // The escape now ends by distance, not by a timer: he really has to leave the
                // arena.
                if (HasLeftArena(c))
                {
                    RollEscapeMishap(c);
                    c.BeginState(CombatState.Escaped, 0);
                    Emit(new WarriorEscaped(ElapsedSeconds, c.Id));
                }
                else
                {
                    c.BeginState(CombatState.Retreating, _tuning.TickSeconds);
                }

                break;

            case CombatState.Escaped:
            case CombatState.Dead:
            default:
                break;
        }
    }

    private void StartAttack(Combatant attacker)
    {
        // The decision step: the target is weighed again here. An enemy who is wounded, whose
        // armour has broken, or on whom teammates have piled up is only noticed here.
        Combatant? target = ChooseTarget(attacker);

        if (target is null)
        {
            Wait(attacker);
            return;
        }

        // The opening is refreshed at every decision step — even while in reach. There is no
        // opening while in reach, so when he leaves the fight and distance opens again, that
        // counts as a new opening and the warrior makes his decision anew.
        bool hadOpening = attacker.SawChargeOpening;
        attacker.SawChargeOpening = HasRoomToGather(attacker);
        bool openingIsNew = attacker.SawChargeOpening && !hadOpening;

        // If out of reach he keeps closing — but if he has something to throw, he throws it.
        // Throwing's real job is filling this gap: closing and fleeing are no longer free.
        if (!InReach(attacker, target))
        {
            if (InThrowRange(attacker, target))
            {
                BeginThrow(attacker, target);
            }
            else if (ShouldCharge(attacker, target, openingIsNew))
            {
                BeginCharge(attacker, target);
            }
            else
            {
                Wait(attacker);
            }

            return;
        }

        // The third option of a warrior in reach: waiting instead of striking. The decision comes
        // out of the Defence stat — the way the charge comes out of Aggression (docs/GDD.md §5).
        if (ShouldBlock(attacker))
        {
            attacker.JustBlocked = true;
            attacker.BeginState(CombatState.Blocking, _tuning.BlockSeconds);
            Emit(new BlockRaised(ElapsedSeconds, attacker.Id));
            return;
        }

        attacker.JustBlocked = false;

        attacker.BeginState(
            CombatState.AttackWindup,
            AttackCycleSeconds(attacker) * _tuning.WindupFraction);

        Emit(new AttackStarted(ElapsedSeconds, attacker.Id, target.Id));
    }

    /// <summary>Does the warrior go into a block stance at this decision step?</summary>
    /// <remarks>
    /// <para>
    /// The condition for taking the stance is not a threat but a <b>move that is read</b>: an
    /// enemy being in reach is not enough, that enemy's sword must be gathered. Had the condition
    /// been proximity alone, the stance would be taken blindly — measured, it met only 0.20 blows
    /// per warrior; the remaining stances ate the attack cycle without stopping anything. In that
    /// form the block was the Defence stat's punishment, not its return.
    /// </para>
    /// <para>
    /// The condition is read not from his own reach but from <b>the larger of the two</b>: the
    /// warrior with a short knife standing against a long-hafted enemy is under threat too, and he
    /// is the one who most needs to block. A running charge counts as gathering — the arrival blow is a strike too.
    /// </para>
    /// <para>
    /// A warrior who wants to pull out does not block: the key is an order to leave, and taking
    /// the stance would delay it (docs/GDD.md §5 interruption table).
    /// </para>
    /// </remarks>
    private bool ShouldBlock(Combatant c)
    {
        if (c.RetreatRequested || c.JustBlocked)
        {
            return false;
        }

        bool swingIncoming = false;

        foreach (Combatant enemy in _combatants)
        {
            if (enemy.Team == c.Team || !enemy.IsActive || enemy.State is CombatState.Retreating)
            {
                continue;
            }

            // What is read is the gathering window: the sword went back and has not come down yet.
            if (enemy.State is not (CombatState.AttackWindup or CombatState.Charging))
            {
                continue;
            }

            if (c.Position.DistanceTo(enemy.Position) <= Math.Max(c.Weapon.Reach, enemy.Weapon.Reach))
            {
                swingIncoming = true;
                break;
            }
        }

        return swingIncoming && _rng.Chance(BlockChanceFor(c));
    }

    /// <summary>This warrior's probability of taking a block stance — it comes out of his identity.</summary>
    private double BlockChanceFor(Combatant c) =>
        Math.Clamp(c.Warrior.EffectiveStats.Defense / 100.0, 0, 1) * _tuning.MaxBlockChance;

    private void ResolveWindupEnd(Combatant attacker)
    {
        Combatant? target = FindTarget(attacker);
        if (target is not null)
        {
            attacker.Stamina = Math.Max(0, attacker.Stamina - _tuning.AttackStaminaCost);
            attacker.AttacksMade++;

            // If the target left reach during the move, the sword lands on empty air. The price of
            // committing to a strike: while the target flees you are nailed in place.
            if (InReach(attacker, target))
            {
                ResolveStrike(attacker, target);
            }
            else
            {
                Emit(new AttackMissed(ElapsedSeconds, attacker.Id, target.Id));
            }
        }

        if (attacker.State != CombatState.Dead)
        {
            attacker.BeginState(
                CombatState.AttackRecovery,
                AttackCycleSeconds(attacker) * (1 - _tuning.WindupFraction));
        }
    }

    // ---------------------------------------------------------------- charge

    /// <summary>
    /// Does the warrior launch a charge at this decision moment?
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only if the opening has <b>just appeared</b> and the die holds (docs/GDD.md §4). A warrior
    /// under a flee command does not charge — we do not want him caught between two commitments.
    /// </para>
    /// <para>
    /// <b>The die is rolled per opening, not per second.</b> If it were rolled again at every
    /// decision step for as long as the opening lasted, charge frequency would depend on how long
    /// the warrior loitered in the opening; and loitering time is the speed at which he closes
    /// distance. Measured: such a warrior charged <b>less often</b> the faster he was (2.20 per
    /// fight at Speed 0, 1.20 at Speed 100) and that fall exactly cancelled the damage increase
    /// tied to speed — the <c>Speed</c> axis measured as inert. Deciding once when he sees the
    /// opportunity is both more believable and independent of speed.
    /// </para>
    /// <para>
    /// <b>A fleeing target is not charged</b>, and if the target starts fleeing during the charge
    /// the move is wasted. Measured: otherwise the charge is not a move but a <b>chasing tool</b>,
    /// the 1.6x speed disables escape's only tuning knob (<c>RetreatSpeedMultiplier</c>) and
    /// docs/GDD.md §5's ladder <b>inverted</b> — pressing early became more lethal than pressing
    /// late.
    /// </para>
    /// <para>
    /// <b>Throwing takes priority:</b> a warrior with a projectile throws it, he does not charge.
    /// Closing distance is not the ranged warrior's problem to solve; the charge is the answer
    /// given to distance by the one who cannot hit from afar.
    /// </para>
    /// </remarks>
    private bool ShouldCharge(Combatant c, Combatant target, bool openingIsNew) =>
        !c.RetreatRequested
        && target.State is not CombatState.Retreating
        && openingIsNew
        && _rng.Chance(ChargeChanceFor(c));

    /// <summary>
    /// Is there enough room to finish the windup?
    /// </summary>
    /// <remarks>
    /// <para>
    /// The charge's trigger is <b>not a fixed distance threshold but an assessment of the
    /// opportunity</b> (docs/GDD.md §4): "nobody can hit me right now and I have time to finish my
    /// windup." What is asked of every active enemy is how long it would take him to become able
    /// to strike: <c>(distance − his reach) ÷ his speed</c>. If anyone can do that in less than the
    /// windup, there is no opening.
    /// </para>
    /// <para>
    /// The distance needed is thus <b>derived</b>: <c>reach + speed × windup</c>. No hand-picked
    /// threshold is needed, and no separate crowd restriction either — if three enemies can reach
    /// you there is no opening anyway. Measured: the old hand-locked 320 was the middle of the
    /// 287-327 band this formula produces for the current roster.
    /// </para>
    /// <para>
    /// <b>A deliberate blind spot:</b> the calculation only sees threats that come on foot. An
    /// enemy with a projectile can break the windup regardless of distance, and we do not want the
    /// warrior to know that in advance — this is what makes the ranged enemy the charge's natural counter.
    /// </para>
    /// </remarks>
    private bool HasRoomToGather(Combatant c)
    {
        foreach (Combatant enemy in _combatants)
        {
            // Someone fleeing is not a threat: he is going to the exit, not to you.
            if (enemy.Team == c.Team || !enemy.IsActive || enemy.State is CombatState.Retreating)
            {
                continue;
            }

            double gap = c.Position.DistanceTo(enemy.Position) - enemy.Weapon.Reach;

            if (gap <= 0 || gap / BaseMoveSpeed(enemy) <= _tuning.ChargeWindupSeconds)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>This warrior's travel per second without state multipliers.</summary>
    private double BaseMoveSpeed(Combatant c) => Lerp(
        _tuning.MoveSpeedAtZeroSpeed,
        _tuning.MoveSpeedAtMaxSpeed,
        Math.Clamp(c.Warrior.EffectiveStats.Speed / 100.0, 0, 1));

    /// <summary>The attack cycle stretched by the weight of the armour.</summary>
    private double AttackCycleSeconds(Combatant c) => c.Weapon.AttackSeconds
        * (1 + (ArmorLoad(c) * _tuning.ArmorAttackSlowdownAtFullWeight));

    /// <summary>The armour's ratio to full weight (0-1). All penalties are read from this.</summary>
    private double ArmorLoad(Combatant c) => _tuning.ArmorWeightAtFullPenalty <= 0
        ? 0
        : Math.Clamp(c.ArmorWeight / _tuning.ArmorWeightAtFullPenalty, 0, 1);

    /// <summary>This warrior's probability of launching a charge — it comes out of his identity.</summary>
    private double ChargeChanceFor(Combatant c) => Lerp(
        _tuning.ChargeChanceAtZeroAggression,
        _tuning.ChargeChanceAtMaxAggression,
        Math.Clamp(c.Warrior.EffectiveStats.Aggression / 100.0, 0, 1));

    private void BeginCharge(Combatant c, Combatant target)
    {
        c.ClearCharge();

        // The opening is spent. If there is still a gap when the charge ends, that is a new
        // opening and the warrior decides again — a warrior who cuts down his target and finds a
        // gap in front of him being able to launch at the next one depends on this.
        c.SawChargeOpening = false;

        FaceToward(c, target);
        c.ChargeTarget = target.Id;
        c.ChargesStarted++;
        c.ChargeStartSecondsSum += ElapsedSeconds;
        c.LastChargeStartSeconds = ElapsedSeconds;

        if (_tuning.ChargeWindupSeconds > 0)
        {
            c.BeginState(CombatState.ChargeWindup, _tuning.ChargeWindupSeconds);
            Emit(new ChargeStarted(ElapsedSeconds, c.Id, target.Id));
            return;
        }

        c.BeginState(CombatState.Charging, _tuning.TickSeconds);
        Emit(new ChargeStarted(ElapsedSeconds, c.Id, target.Id));
        Emit(new ChargeLaunched(ElapsedSeconds, c.Id, target.Id));
    }

    /// <summary>
    /// The windup is full: the run starts. If the target was lost in the meantime the move is wasted.
    /// </summary>
    private void LaunchCharge(Combatant c)
    {
        Combatant? target = ChargeTargetOf(c);

        if (target is null || !target.IsActive || target.State is CombatState.Retreating)
        {
            EndCharge(c, connected: false);
            return;
        }

        FaceToward(c, target);
        c.BeginState(CombatState.Charging, _tuning.TickSeconds);
        Emit(new ChargeLaunched(ElapsedSeconds, c.Id, target.Id));
    }

    /// <summary>
    /// A heavy blow scatters the windup: the run never starts, the damage multiplier is not earned.
    /// </summary>
    /// <remarks>
    /// <b>No threshold: every blow that lands scatters it.</b> The heavy-blow threshold
    /// (<see cref="CombatTuning.GrievousSeverityThreshold"/>) was tried first and measured at
    /// <b>0.0%</b> — blows landing on a fresh warrior almost never reach that threshold, so the
    /// rule was written down and never fired. With the hit criterion, 23.6% of windups scatter.
    /// The warrior cannot evade during it anyway, so "was he hit, then it scattered" reads as a
    /// single rule and spawns no new balance number.
    /// </remarks>
    private void BreakChargeWindup(Combatant c)
    {
        c.ClearCharge();
        c.ChargesBroken++;
        Emit(new ChargeBroken(ElapsedSeconds, c.Id));

        if (c.RetreatRequested)
        {
            BeginRetreat(c);
            return;
        }

        c.BeginState(CombatState.Idle, SpacingSeconds(c));
    }

    /// <summary>
    /// Advances a charge by one tick: the free hits taken along the way, arrival, and missing.
    /// </summary>
    /// <remarks>
    /// Like pulling out, it is rebuilt tick by tick; what decides when the charge ends is not
    /// duration but <b>distance</b>.
    /// </remarks>
    private void AdvanceCharge(Combatant c)
    {
        RollChargeOpportunities(c);

        if (!c.IsActive || c.State != CombatState.Charging)
        {
            return;
        }

        Combatant? target = ChargeTargetOf(c);

        if (target is null || !target.IsActive || target.State is CombatState.Retreating)
        {
            EndCharge(c, connected: false);
            return;
        }

        if (InReach(c, target))
        {
            c.ChargeSeconds = 0;
            c.ChargeOpportunists.Clear();
            c.ChargeBonusPending = !c.ChargeMomentumBroken;

            // The strike resolves on later ticks, by which time the warrior is no longer running.
            // That is why momentum is captured at the moment of impact.
            c.ChargeImpactSpeed = BaseMoveSpeed(c) * _tuning.ChargeSpeedMultiplier;
            c.ChargesConnected++;
            Emit(new ChargeConnected(ElapsedSeconds, c.Id, target.Id));

            c.BeginState(
                CombatState.AttackWindup,
                AttackCycleSeconds(c) * _tuning.WindupFraction);

            Emit(new AttackStarted(ElapsedSeconds, c.Id, target.Id));
            return;
        }

        c.ChargeSeconds += _tuning.TickSeconds;

        if (c.ChargeSeconds >= _tuning.ChargeMaxSeconds)
        {
            EndCharge(c, connected: false);
            return;
        }

        c.BeginState(CombatState.Charging, _tuning.TickSeconds);
    }

    /// <summary>
    /// The free hit of the enemies the charging warrior runs past.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same mechanic as the escape window (see <see cref="BeginRetreat"/>): a warrior who
    /// drops his defence owes a hit to everyone whose reach he enters. <b>Once</b> per enemy per
    /// charge — otherwise the enemy run past would strike on every tick.
    /// </para>
    /// <para>
    /// <b>The target is separate.</b> An enemy passed on the way always gets his hit; the charge's
    /// target can only answer if <see cref="CombatTuning.ChargeTargetCounterChance"/> holds.
    /// Hitting a body going past you is easy, meeting a body coming at you at exactly the right
    /// moment is hard (docs/GDD.md §4). The die is rolled once per charge: a target who misses it
    /// finds no second chance in the same run.
    /// </para>
    /// </remarks>
    private void RollChargeOpportunities(Combatant c)
    {
        foreach (Combatant hunter in _combatants)
        {
            if (hunter.Team == c.Team
                || !hunter.IsActive
                || !InReach(hunter, c)
                || !c.ChargeOpportunists.Add(hunter.Id))
            {
                continue;
            }

            // Meeting it head-on is hard: the target's counter-hit is rare, the passers-by's is not.
            if (hunter.Id == c.ChargeTarget && !_rng.Chance(_tuning.ChargeTargetCounterChance))
            {
                continue;
            }

            Emit(new OpportunityAttack(ElapsedSeconds, hunter.Id, c.Id));
            hunter.AttacksMade++;
            c.ChargeOpportunitiesTaken++;
            ResolveStrike(hunter, c);

            if (!c.IsActive)
            {
                return;
            }
        }
    }

    /// <summary>The charge ended without reaching the target — the bonus is not spent, the warrior is left exposed.</summary>
    /// <summary>The target the charge committed to — returned even if dead, so the caller can report the miss.</summary>
    private Combatant? ChargeTargetOf(Combatant c)
    {
        if (c.ChargeTarget is not WarriorId id)
        {
            return null;
        }

        foreach (Combatant other in _combatants)
        {
            if (other.Id == id)
            {
                return other;
            }
        }

        return null;
    }

    private void EndCharge(Combatant c, bool connected)
    {
        c.ClearCharge();

        if (!connected)
        {
            Emit(new ChargeMissed(ElapsedSeconds, c.Id));
        }

        if (c.RetreatRequested)
        {
            BeginRetreat(c);
            return;
        }

        c.BeginState(CombatState.Idle, SpacingSeconds(c));
    }

    /// <summary>
    /// The escape begins; <b>every enemy in reach</b> earns a free hit.
    /// </summary>
    /// <remarks>
    /// It existed so that "I pressed the key = I am safe" would not hold; with space it gained its
    /// real meaning: <b>if you are surrounded, the price of fleeing is three free hits.</b> Pulling
    /// out before being encircled is now a real decision.
    /// </remarks>
    private void BeginRetreat(Combatant c)
    {
        if (c.State is CombatState.Charging or CombatState.ChargeWindup)
        {
            // The move was cut short: the bonus is not spent, the hits taken do not come back.
            c.ClearCharge();
            Emit(new ChargeMissed(ElapsedSeconds, c.Id));
        }

        c.ClearCharge();
        c.BeginState(CombatState.Retreating, _tuning.TickSeconds);
        Emit(new RetreatStarted(ElapsedSeconds, c.Id));

        foreach (Combatant hunter in _combatants)
        {
            if (hunter.Team == c.Team || !hunter.IsActive || !InReach(hunter, c))
            {
                continue;
            }

            Emit(new OpportunityAttack(ElapsedSeconds, hunter.Id, c.Id));
            hunter.AttacksMade++;
            c.ChargeOpportunitiesTaken++;
            ResolveStrike(hunter, c);

            if (!c.IsActive)
            {
                return;
            }
        }
    }

    /// <summary>There is nobody to strike — he waits a short while and looks again.</summary>
    private static void Wait(Combatant c) => c.BeginState(CombatState.Idle, 0.2);

    // -------------------------------------------------------------- throwing

    /// <summary>Is the target outside melee reach but inside throwing range?</summary>
    private static bool InThrowRange(Combatant attacker, Combatant target) =>
        attacker.CanThrow
        && attacker.Warrior.UsableThrown is ThrownWeapon t
        && attacker.Position.DistanceTo(target.Position) <= t.Range;

    private void BeginThrow(Combatant attacker, Combatant target)
    {
        ThrownWeapon thrown = attacker.Warrior.UsableThrown!;

        FaceToward(attacker, target);
        attacker.BeginState(CombatState.ThrowWindup, thrown.ThrowSeconds * _tuning.WindupFraction);
        Emit(new AttackStarted(ElapsedSeconds, attacker.Id, target.Id));
    }

    /// <summary>The move is over: the projectile takes off. From here on it is the flight's job.</summary>
    private void ReleaseThrow(Combatant attacker)
    {
        ThrownWeapon? thrown = attacker.Warrior.UsableThrown;
        Combatant? target = FindTarget(attacker);

        if (thrown is not null && target is not null && attacker.CanThrow)
        {
            attacker.ThrowsLeft--;
            attacker.AttacksMade++;

            double distance = attacker.Position.DistanceTo(target.Position);
            double flight = Math.Max(_tuning.TickSeconds, distance / thrown.Speed);

            _projectiles.Add(new Projectile(attacker, target, thrown, attacker.Position, flight));
            Emit(new ProjectileLaunched(
                ElapsedSeconds,
                attacker.Id,
                target.Id,
                thrown.Name,
                attacker.Position,
                target.Position,
                flight));
        }

        if (attacker.State != CombatState.Dead)
        {
            attacker.BeginState(
                CombatState.ThrowRecovery,
                (thrown?.ThrowSeconds ?? _tuning.TickSeconds) * (1 - _tuning.WindupFraction));
        }
    }

    /// <summary>
    /// Advances the projectiles in the air by one tick and resolves the ones that arrive.
    /// </summary>
    /// <remarks>
    /// The list order is preserved: if two projectiles arrive on the same tick, which is handled
    /// first depends on the order they were thrown. Were it random, the same seed would not give the same fight.
    /// </remarks>
    private void AdvanceProjectiles()
    {
        if (_projectiles.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _projectiles.Count; i++)
        {
            Projectile p = _projectiles[i];
            p.SecondsToImpact -= _tuning.TickSeconds;

            if (p.SecondsToImpact > 0)
            {
                continue;
            }

            ResolveProjectile(p);
            _projectiles.RemoveAt(i--);
        }
    }

    /// <summary>
    /// The outcome of an arriving projectile.
    /// </summary>
    /// <remarks>
    /// During the flight the target may have died, fled or left range — the projectile is then
    /// wasted. There is no evasion: a warrior who cannot see what is coming cannot slip it, his
    /// defence is only his armour and the distance.
    /// </remarks>
    private void ResolveProjectile(Projectile p)
    {
        Combatant attacker = p.Attacker;
        Combatant target = p.Target;

        if (!target.IsActive
            || attacker.Position.DistanceTo(target.Position) > p.Weapon.Range)
        {
            Emit(new ProjectileMissed(ElapsedSeconds, attacker.Id, target.Id));
            return;
        }

        WarriorStats atkStats = attacker.Warrior.EffectiveStats;
        double reachedFraction =
            Math.Clamp(attacker.Position.DistanceTo(target.Position) / p.Weapon.Range, 0, 1);
        double hitChance = (_tuning.BaseThrowHitChance
                            + (atkStats.Accuracy * _tuning.AccuracyHitBonus))
                           * (1 - (reachedFraction * _tuning.ThrowFalloffAtMaxRange));

        if (!target.CanDefend)
        {
            hitChance += _tuning.RetreatingHitBonus;
        }

        if (!_rng.Chance(Math.Clamp(hitChance, 0.05, 0.95)))
        {
            Emit(new ProjectileMissed(ElapsedSeconds, attacker.Id, target.Id));
            return;
        }

        // Strength does not enter into throwing: what drives the javelin is the throw itself, not the arm.
        ApplyBlow(
            attacker,
            target,
            p.Weapon.Damage,
            p.Weapon.DismembermentFactor,
            p.Weapon.StunFactor,
            p.Weapon.Poison,
            BlowSource.Projectile);
    }

    // ------------------------------------------------------------- resolution

    private void ResolveStrike(Combatant attacker, Combatant defender)
    {
        if (!defender.IsActive)
        {
            return;
        }

        WarriorStats atkStats = attacker.Warrior.EffectiveStats;
        WarriorStats defStats = defender.Warrior.EffectiveStats;
        double staminaFactor = StaminaFactor(attacker);

        // The charge bonus is spent even on a miss: momentum is used once. It is never applied to
        // a target who has started fleeing — momentum comes from crashing into an enemy who stands
        // his ground, not into one who turns his back. Measured: with the bonus applying to fleers
        // too, the charge made the key pressed at first contact more lethal than pressing late and
        // inverted GDD §5's ladder.
        double impactSpeed = attacker.ChargeImpactSpeed;
        double chargeMultiplier =
            attacker.ConsumeChargeBonus() && defender.State is not CombatState.Retreating
                ? 1 + (impactSpeed / _tuning.MoveSpeedAtMaxSpeed * _tuning.ChargeDamageAtFullSpeed)
                : 1.0;

        // 1) Hit
        bool flanking = IsFlanking(attacker, defender);
        double hitChance = (_tuning.BaseHitChance + (atkStats.Accuracy * _tuning.AccuracyHitBonus))
                           * staminaFactor;

        if (!defender.CanDefend)
        {
            hitChance += _tuning.RetreatingHitBonus;
        }

        if (flanking)
        {
            hitChance += _tuning.FlankHitBonus;
        }

        if (!_rng.Chance(Math.Clamp(hitChance, 0.05, 0.98)))
        {
            Emit(new AttackMissed(ElapsedSeconds, attacker.Id, defender.Id));
            return;
        }

        // 2) Catching — tried BEFORE evasion. Catching is committed defence: the warrior goes into
        // the incoming weapon. Had it come after evasion the rule would never bite; evasion would
        // already have held and the jitte would stay "the last resort of the one who cannot evade" —
        // whereas its real return is locking the attacker down.
        if (TryCatch(attacker, defender, flanking))
        {
            return;
        }

        // 3) Block — a warrior in the stance does not evade, he meets the blow. It comes BEFORE
        // evasion because a block is not a die but a commitment: the warrior has already chosen his
        // move. Placed after, evasion would already have erased some of the blows the block holds
        // and the stance's measured value would fall below its own cost.
        bool blocking = !flanking && defender.State is CombatState.Blocking;

        // 4) Evasion — no evading while pulling out, and no evading a strike from behind
        if (!blocking && !flanking && defender.CanDefend && defender.Stamina >= _tuning.DodgeStaminaCost)
        {
            double evasionChance = defStats.Evasion / 100.0 * _tuning.MaxEvasionChance;
            if (_rng.Chance(evasionChance))
            {
                defender.Stamina -= _tuning.DodgeStaminaCost;
                defender.DodgesPerformed++;
                Emit(new AttackDodged(ElapsedSeconds, attacker.Id, defender.Id));
                return;
            }
        }

        // 5) Damage — the blow first lands on a region (armour and dismemberment are read from it)
        Weapon weapon = attacker.Weapon;
        double raw = weapon.Damage
                     * (1 + (atkStats.Strength / 100.0 * _tuning.StrengthDamageBonusAtMax))
                     * staminaFactor
                     * (flanking ? _tuning.FlankDamageMultiplier : 1.0)
                     * chargeMultiplier;

        // How much the stance holds is read from the weapon in the warrior's hand: a long haft meets
        // the blow with its shaft, a fist meets almost nothing.
        double dismemberment = weapon.DismembermentFactor;
        double stun = weapon.StunFactor;

        if (blocking)
        {
            double quality = _tuning.BlockDamageReduction * defender.Weapon.BlockFactor;

            raw *= 1 - Math.Clamp(quality, 0, 1);
            dismemberment *= _tuning.BlockDismembermentShare;
            stun *= _tuning.BlockStunShare;

            defender.Stamina = Math.Max(0, defender.Stamina - _tuning.BlockStaminaCost);
            defender.BlocksPerformed++;
        }

        ApplyBlow(
            attacker,
            defender,
            raw,
            dismemberment,
            stun,
            weapon.Poison,
            BlowSource.Melee,
            blocking);
    }

    /// <summary>
    /// Rolls the catch die; if it holds, it erases the strike and binds the attacker.
    /// </summary>
    /// <returns>True if the strike was caught — the caller stops there.</returns>
    /// <remarks>
    /// <para>
    /// This is the gap GDD §4 left when it rejected the shield: a hand-carried shield was not common
    /// in Japanese warfare, but there was an implement that <b>stopped</b> the incoming sword. The die
    /// feeds on three things — the grip of the defender's implement (<c>CatchSkill</c>), how catchable
    /// the attacker's weapon is (<c>CatchFactor</c>) and the defender's <b>Accuracy</b>. Evasion hangs
    /// on Evasion while catching hangs on Accuracy deliberately: if both defensive axes fed off the
    /// same stat, the equipment decision would be a copy of the stat decision.
    /// </para>
    /// <para>
    /// There are three protective rules. <b>Only melee is caught</b> — a projectile in the air does
    /// not seat in the hook. <b>A strike from behind is not caught</b>: a weapon you cannot see cannot
    /// be held, and if the rule worked here too the price of being encircled (§5) would be erased.
    /// <b>A warrior pulling out does not catch</b> — someone running with his back turned does not go
    /// into the other man's weapon, and as with stun no new die is placed on top of the escape promise.
    /// </para>
    /// </remarks>
    private bool TryCatch(Combatant attacker, Combatant defender, bool flanking)
    {
        if (flanking || !defender.CanDefend || defender.RetreatRequested)
        {
            return false;
        }

        Weapon catcher = defender.Weapon;
        if (!catcher.CanCatch || defender.Stamina < _tuning.CatchStaminaCost)
        {
            return false;
        }

        Weapon caught = attacker.Weapon;
        double accuracyBonus =
            defender.Warrior.EffectiveStats.Accuracy / 100.0 * _tuning.CatchAccuracyBonusAtMax;

        double chance = _tuning.BaseCatchChance
                        * catcher.CatchSkill
                        * caught.CatchFactor
                        * (caught.TwoHanded ? _tuning.CatchTwoHandedFactor : 1.0)
                        * (1 + accuracyBonus);

        if (!_rng.Chance(Math.Clamp(chance, 0, 1)))
        {
            return false;
        }

        defender.Stamina -= _tuning.CatchStaminaCost;
        defender.CatchesMade++;
        attacker.TimesCaught++;

        // The warrior who is caught loses his charge too — a bound arm carries no momentum.
        attacker.ClearCharge();

        // The hook does not only hold: it levers the weapon out of the palm. Disarming does not stack
        // ON TOP OF the bind, it takes its PLACE — with the weapon gone the bind is released too, and
        // the attacker is free but unarmed. Stacked, the catching implement would take both the window
        // and the weapon on a single die, and what it loses in damage would be more than repaid.

        double disarmChance = _tuning.CatchDisarmChance * caught.DisarmFactor;
        if (disarmChance > 0 && _rng.Chance(Math.Clamp(disarmChance, 0, 1)))
        {
            DropWeapon(attacker, defender, past: defender);
            return true;
        }

        attacker.BeginState(CombatState.WeaponBound, _tuning.CatchBindSeconds);
        Emit(new AttackCaught(
            ElapsedSeconds, attacker.Id, defender.Id, _tuning.CatchBindSeconds));

        return true;
    }

    /// <summary>The source of the blow — it only decides which event is emitted.</summary>
    private enum BlowSource
    {
        Melee,
        Projectile,
    }

    /// <summary>
    /// Applies the outcome of a blow that has landed: region, armour, damage, dismemberment tree.
    /// </summary>
    /// <remarks>
    /// Melee and throwing go through the <b>same</b> path. Written separately, reading armour by
    /// region or the heavy-blow tree would need maintenance in two places and one of them would
    /// quietly fall behind.
    /// </remarks>
    private void ApplyBlow(
        Combatant attacker,
        Combatant defender,
        double rawDamage,
        double dismembermentFactor,
        double stunFactor,
        double poison,
        BlowSource source,
        bool blocked = false)
    {
        WarriorStats defStats = defender.Warrior.EffectiveStats;
        HitLocation location = RollHitLocation();
        ArmorPiece struckPiece = defender.ArmorAt(location);

        double afterDefense =
            rawDamage * (1 - (defStats.Defense / 100.0 * _tuning.MaxDefenseReduction));
        double damage = Math.Max(
            _tuning.MinimumDamage,
            afterDefense - struckPiece.DamageReduction);

        // A piece wears as much as it stops. No die: every point it absorbs comes off its pool, and
        // when the pool runs out the piece breaks and is gone PERMANENTLY (docs/GDD.md §7).
        WearArmor(defender, location, struckPiece, afterDefense - damage);

        defender.Health -= damage;
        defender.TimesHit++;
        defender.DamageTaken += damage;
        attacker.HitsLanded++;
        attacker.DamageDealt += damage;

        double remaining = Math.Max(0, defender.Health);

        // A blocked blow is a separate event, not a "hit": the presentation layer has to tell a
        // shaken stance from a bloody hit. The counters still rise — the blow landed.
        Emit(blocked
            ? new AttackBlocked(ElapsedSeconds, attacker.Id, defender.Id, damage)
            : source == BlowSource.Melee
                ? new AttackLanded(ElapsedSeconds, attacker.Id, defender.Id, damage, remaining)
                : new ProjectileHit(ElapsedSeconds, attacker.Id, defender.Id, damage, remaining));

        // The first hit unlocks the flee key; whichever side strikes.
        _contactMade = true;

        // A hit scatters a gathering charge (docs/GDD.md §4). It is checked before the dismemberment
        // die: even if the blow kills, the scattering has already happened, and the death state
        // overrides the Idle set here.
        if (defender.State is CombatState.ChargeWindup)
        {
            BreakChargeWindup(defender);
        }

        // A hit does not scatter a charge already running — but the rare counter-hit of the target
        // meeting it head-on KILLS THE MOMENTUM: the arrival blow drops to an ordinary strike.
        // A passer-by's blow cannot do this; both the difficulty and the value are in meeting it in time.
        if (defender.State is CombatState.Charging && defender.ChargeTarget == attacker.Id)
        {
            defender.ChargeMomentumBroken = true;
        }

        // Heavy blow → dismemberment die (low health is NOT A PRECONDITION)
        bool grievous =
            TryGrievousBlow(defender, damage, defStats, location, struckPiece, dismembermentFactor);

        if (!grievous && defender.Health <= 0)
        {
            Die(defender, DeathCause.Wounds);
        }

        // Poison does not go through a die: if the blade scratched skin, the dose is in. Poison is not
        // applied to a dying warrior — poison's currency is time, and the dead have none.
        if (defender.IsActive && defender.Health > 0 && poison > 0)
        {
            ApplyPoison(attacker, defender, poison);
        }

        // The stun die comes last: a warrior who is not on his feet has nothing left to freeze.
        // It is rolled AFTER the dismemberment die so that the two outcomes of the same heavy blow
        // resolve in a fixed order — with the same seed, the fight stays the same.
        if (defender.IsActive && defender.Health > 0)
        {
            TryStun(attacker, defender, damage, defStats, location, struckPiece, stunFactor);
        }

        // The disarm die comes last and only in melee: what breaks the grip is the weapon striking
        // plate and bouncing back. A projectile has already left the hand.
        if (source == BlowSource.Melee)
        {
            TryDisarmOnArmor(attacker, defender, struckPiece);
        }
    }

    /// <summary>
    /// Wears the struck piece; if its pool is spent, breaks the piece.
    /// </summary>
    /// <remarks>
    /// The difference from the weapon is deliberate: a dropped weapon comes back at the end of the
    /// fight, a broken armour piece does not. Armour is the game's consumable — and the armour that
    /// absorbs the most is the one that runs out the fastest.
    /// </remarks>
    private void WearArmor(
        Combatant defender,
        HitLocation location,
        ArmorPiece struckPiece,
        double absorbed)
    {
        if (!defender.WearArmor(location, absorbed, _tuning.ArmorDurabilityScale))
        {
            return;
        }

        _destroyedArmor[defender.Id] = _destroyedArmor.GetValueOrDefault(defender.Id)
                                       | location.AsFlag();

        Emit(new ArmorDestroyed(ElapsedSeconds, defender.Id, location, struckPiece.Name));
    }

    /// <summary>
    /// Rolls the die for the attacker's own weapon being knocked out of his hand on a strike that lands on armour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is armour's second answer. A strike landing on a bare region <b>never</b> disarms:
    /// hardness is read from the struck piece's dismemberment resistance
    /// (<see cref="CombatTuning.ArmorHardnessShare"/>), and a bare region's resistance is zero. The
    /// rule thus limits itself — a warrior fighting an unarmoured enemy never loses his weapon.
    /// </para>
    /// <para>
    /// The die is rolled on the <b>attacker's</b> weapon, not the defender's: what breaks the grip is
    /// the blow bouncing off plate. The blunt class's third gain is here — a club that rebounds stays
    /// in the palm.
    /// </para>
    /// </remarks>
    private void TryDisarmOnArmor(Combatant attacker, Combatant defender, ArmorPiece struckPiece)
    {
        if (!attacker.IsActive || attacker.Disarmed)
        {
            return;
        }

        double hardness = struckPiece.DismembermentResistance * _tuning.ArmorHardnessShare;
        double chance = _tuning.BaseDisarmChance * attacker.Weapon.DisarmFactor * hardness;

        if (chance <= 0 || !_rng.Chance(Math.Clamp(chance, 0, 1)))
        {
            return;
        }

        DropWeapon(attacker, disarmer: null, past: defender);
    }

    /// <summary>
    /// Drops the weapon out of the hand: the warrior spends the rest of the fight with his fists.
    /// </summary>
    /// <remarks>
    /// Falling back on fists is not only a loss of damage — reach drops from 150/130 to 100, the
    /// attack cycle shortens, a fist cannot be caught. The size of the loss therefore differs from
    /// weapon to weapon: a warrior who drops his nodachi loses his distance too. The loss is not
    /// permanent: the weapon lies on the ground and he can walk to it (see <see cref="TryPickUp"/>).
    /// </remarks>
    /// <param name="past">
    /// The warrior the weapon is flung past — it falls <b>behind him</b>.
    /// </param>
    /// <remarks>
    /// The importance of the direction came out of measurement: if the weapon falls behind its owner,
    /// the walk to pick it up pulls the warrior <b>back</b> out of the fight and, in front of a slow
    /// enemy, disarming becomes not a cost but a free breather (measured: the player's victory
    /// <b>rose</b> as the distance grew). When it falls behind the other man the cost is real: going
    /// to his weapon means going through the enemy.
    /// </remarks>
    private void DropWeapon(Combatant owner, Combatant? disarmer, Combatant past)
    {
        Weapon lost = owner.Weapon;
        string name = lost.Name;

        // The weapon is not destroyed, it falls to the ground: anyone left unarmed — the disarmer, a
        // teammate, an enemy — can walk to it. That is what choosing dropping over breaking buys.
        _dropped.Add(new GroundWeapon(lost, Clamped(DropPoint(owner, past))));

        owner.HeldWeapon = null;
        owner.Disarmed = true;
        owner.TimesDisarmed++;

        if (disarmer is not null)
        {
            disarmer.DisarmsInflicted++;
        }

        Emit(new WeaponDropped(ElapsedSeconds, owner.Id, name, disarmer?.Id));
    }

    /// <summary>
    /// Applies the dose of a poisoned strike to the defender.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No die is rolled: the poison is on the blade, and if the strike scratched skin the dose is in.
    /// Armour does nothing here either — the whole rule is built on that
    /// (<see cref="CombatTuning.PoisonDamagePerTick"/>).
    /// </para>
    /// <para>
    /// The dose <b>accumulates</b> (capped by <see cref="CombatTuning.PoisonMaxDose"/>), while the
    /// timer is restarted on every strike. Were the timer to accumulate, a poisoned weapon would
    /// produce an endlessly extending damage tail in a single fight; were the dose not to accumulate,
    /// the poison of every strike after the first would be worth nothing.
    /// </para>
    /// </remarks>
    private void ApplyPoison(Combatant attacker, Combatant defender, double potency)
    {
        bool wasClean = !defender.IsPoisoned;

        defender.PoisonDose = Math.Min(defender.PoisonDose + potency, _tuning.PoisonMaxDose);
        defender.PoisonSecondsLeft = _tuning.PoisonSeconds;
        defender.PoisonSource = attacker;

        // The clock is only started when the first dose enters clean blood: reset on every strike, a
        // fast poisoned weapon would cancel its own poison by endlessly postponing the damage.
        if (wasClean)
        {
            defender.PoisonTickTimer = _tuning.PoisonTickSeconds;
        }

        defender.TimesPoisoned++;
        attacker.PoisonsInflicted++;

        Emit(new WarriorPoisoned(
            ElapsedSeconds,
            attacker.Id,
            defender.Id,
            defender.PoisonDose,
            defender.PoisonSecondsLeft));
    }

    /// <summary>
    /// Advances poison's clock and applies the damage when its turn comes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Poison damage goes through <b>neither armour nor the Defence stat</b>: the dose is in the
    /// blood, there is no plate in between. The poisoned weapon's low damage in an open fight is the price.
    /// </para>
    /// <para>
    /// Poison neither takes limbs nor stuns — both are outcomes of a <b>blow</b>, and here nobody is
    /// striking. It can kill; the cause of death is kept separate (<see cref="DeathCause.Poison"/>)
    /// because no strike on the field brought the warrior down.
    /// </para>
    /// <para>
    /// <b>The poison of a warrior pulling out does not stop.</b> Stun and catching do not apply to a
    /// fleeing warrior so that no new die is placed on top of the escape promise; poison is not a new
    /// die but the continuation of a price already paid — the key is not an antidote.
    /// </para>
    /// </remarks>
    private void TickPoison(Combatant c)
    {
        if (!c.IsPoisoned)
        {
            return;
        }

        c.PoisonSecondsLeft -= _tuning.TickSeconds;
        c.PoisonTickTimer -= _tuning.TickSeconds;

        if (c.PoisonTickTimer > 0)
        {
            if (c.PoisonSecondsLeft <= 0)
            {
                c.ClearPoison();
            }

            return;
        }

        double damage = _tuning.PoisonDamagePerTick * c.PoisonDose;

        c.Health -= damage;
        c.PoisonDamageTaken += damage;
        c.DamageTaken += damage;

        if (c.PoisonSource is Combatant source)
        {
            source.PoisonDamageDealt += damage;
            source.DamageDealt += damage;
        }

        c.PoisonTickTimer += _tuning.PoisonTickSeconds;

        Emit(new PoisonTicked(ElapsedSeconds, c.Id, damage, Math.Max(0, c.Health)));

        if (c.PoisonSecondsLeft <= 0)
        {
            c.ClearPoison();
        }

        if (c.Health <= 0)
        {
            Die(c, DeathCause.Poison);
        }
    }

    /// <summary>
    /// Rolls the stun die and, if it holds, freezes the warrior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule is the blunt weapon's reason to exist: a blade takes limbs, a blunt weapon lands the
    /// blow that freezes the warrior (docs/GDD.md §7). As with dismemberment, resistance is read from
    /// the armour of the region the blow <b>landed on</b> — but not all of it, only a share
    /// (<see cref="CombatTuning.ArmorStunResistanceShare"/>): plate does not stop blunt force the way
    /// it stops a cut.
    /// </para>
    /// <para>
    /// <b>A warrior pulling out is not stunned.</b> Since a stun would freeze him, an enemy with a
    /// blunt weapon could cancel the player's only intervention (docs/GDD.md §5) with a single die;
    /// the "Flee" key's promise to reduce death cannot bear that. Because a stunned warrior is already
    /// defenceless there is no stun stacking either — the duration is not refreshed.
    /// </para>
    /// </remarks>
    private void TryStun(
        Combatant attacker,
        Combatant defender,
        double damage,
        WarriorStats defStats,
        HitLocation location,
        ArmorPiece struckPiece,
        double stunFactor)
    {
        if (defender.State is CombatState.Retreating or CombatState.Stunned
            || defender.RetreatRequested)
        {
            return;
        }

        if (damage / defStats.MaxHealth < _tuning.StunSeverityThreshold)
        {
            return;
        }

        double resistance = struckPiece.DismembermentResistance * _tuning.ArmorStunResistanceShare;
        double chance = _tuning.BaseStunChance
                        * stunFactor
                        * (location == HitLocation.Head ? _tuning.StunHeadMultiplier : 1.0)
                        * (1 - resistance);

        if (!_rng.Chance(Math.Clamp(chance, 0, 1)))
        {
            return;
        }

        // A stunned warrior loses his charge too: a run is cut short, a windup scatters. The counters
        // are not kept here rather than in BreakChargeWindup — that is the rule about a hit scattering
        // a charge, this is the rule about a blow freezing the warrior.
        defender.ClearCharge();

        defender.TimesStunned++;
        attacker.StunsInflicted++;
        defender.BeginState(CombatState.Stunned, _tuning.StunSeconds);
        Emit(new WarriorStunned(ElapsedSeconds, attacker.Id, defender.Id, _tuning.StunSeconds));
    }

    /// <summary>
    /// Resolves the outcome of a heavy blow.
    /// </summary>
    /// <returns>True if a heavy blow was triggered (death or limb loss).</returns>
    /// <remarks>
    /// Resistance is read from the armour of the region the blow <b>landed on</b> — not of the limb
    /// that comes off. The die asks "did this strike cut through"; a kote covering the arm cannot stop
    /// a blow landing on the torso. A torso hit still costing a limb is a separate rule
    /// (see <see cref="SeverablePart"/>).
    /// </remarks>
    private bool TryGrievousBlow(
        Combatant defender,
        double damage,
        WarriorStats defStats,
        HitLocation location,
        ArmorPiece struckPiece,
        double dismembermentFactor)
    {
        double severity = damage / defStats.MaxHealth;
        if (severity < _tuning.GrievousSeverityThreshold)
        {
            return false;
        }

        double chance = _tuning.BaseDismembermentChance
                        * dismembermentFactor
                        * (1 - struckPiece.DismembermentResistance);

        if (!_rng.Chance(chance))
        {
            return false;
        }

        // The outcome tree (docs/GDD.md §7): what decides is whether the blow was lethal.
        bool severed = TrySever(defender, location);

        // A heavy blow that does not kill: the limb goes, the warrior stays on the field and keeps
        // fighting. No key is needed — which makes WINNING while losing a limb possible.
        if (defender.Health > 0)
        {
            return true;
        }

        // A lethal blow: pressing the key turns death into limb loss. If there is no limb left to
        // take there is nothing to turn either — the warrior dies.
        if (defender.PlayerIntervened && severed)
        {
            defender.Health = _tuning.SurvivalHealthAfterIntervention;
            return true;
        }

        Die(defender, DeathCause.GrievousBlow);
        return true;
    }

    /// <summary>Takes a limb if there is one that can be taken.</summary>
    /// <returns>True if a limb came off.</returns>
    private bool TrySever(Combatant defender, HitLocation location)
    {
        if (SeverablePart(defender, location) is not BodyPart part)
        {
            return false;
        }

        defender.LostLimb = true;
        _lostParts[defender.Id] = _lostParts.GetValueOrDefault(defender.Id) | part.AsFlag();
        Emit(new WarriorDismembered(ElapsedSeconds, defender.Id, part));
        return true;
    }

    /// <summary>
    /// Which limb the warrior brought back from death lost. <c>null</c> if he has no limb left to
    /// lose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Even if the blow landed on the torso, <b>a limb goes</b>: the region concerns only damage and
    /// armour, not the outcome tree. Otherwise the "pull out" key would be <b>free</b> on torso blows
    /// — you come back from death and lose nothing. GDD §7's promise is the opposite: "pressing the
    /// key saves a life but is not free".
    /// </para>
    /// <para>
    /// Measured: with torso hits made not to take limbs, player victory rose from 36% to 53%, that is,
    /// intervention became almost riskless.
    /// </para>
    /// <para>
    /// The choice among the remaining limbs is made with the region weights themselves — torso
    /// excluded. The same limb does not come off twice.
    /// </para>
    /// </remarks>
    private BodyPart? SeverablePart(Combatant defender, HitLocation location)
    {
        BodyPart? direct = location switch
        {
            HitLocation.SwordArm => BodyPart.SwordArm,
            HitLocation.OffArm => BodyPart.OffArm,
            HitLocation.RightLeg => BodyPart.RightLeg,
            HitLocation.LeftLeg => BodyPart.LeftLeg,
            HitLocation.Head => BodyPart.Eye,
            _ => null,
        };

        if (direct is BodyPart hit && !AlreadyLost(defender, hit))
        {
            return hit;
        }

        // It landed on the torso, or that limb is already gone: a weighted choice among the rest.
        Span<double> weights =
        [
            Weight(BodyPart.SwordArm, _tuning.ArmHitWeight),
            Weight(BodyPart.OffArm, _tuning.ArmHitWeight),
            Weight(BodyPart.RightLeg, _tuning.LegHitWeight),
            Weight(BodyPart.LeftLeg, _tuning.LegHitWeight),
            Weight(BodyPart.Eye, _tuning.HeadHitWeight),
        ];

        double total = 0;
        foreach (double w in weights)
        {
            total += w;
        }

        if (total <= 0)
        {
            return null;
        }

        double roll = _rng.NextDouble() * total;
        double running = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            running += weights[i];
            if (roll < running)
            {
                return (BodyPart)i;
            }
        }

        return BodyPart.Eye;

        double Weight(BodyPart part, double weight) => AlreadyLost(defender, part) ? 0 : weight;
    }

    private bool AlreadyLost(Combatant defender, BodyPart part) =>
        defender.Warrior.HasDisability(part)
        || _lostParts.GetValueOrDefault(defender.Id).Has(part);

    /// <summary>Where the blow lands — a weighted die, a single RNG call.</summary>
    private HitLocation RollHitLocation()
    {
        double torso = _tuning.TorsoHitWeight;
        double rightLeg = torso + _tuning.LegHitWeight;
        double leftLeg = rightLeg + _tuning.LegHitWeight;
        double swordArm = leftLeg + _tuning.ArmHitWeight;
        double offArm = swordArm + _tuning.ArmHitWeight;
        double total = offArm + _tuning.HeadHitWeight;

        double roll = _rng.NextDouble() * total;
        if (roll < torso)
        {
            return HitLocation.Torso;
        }

        if (roll < rightLeg)
        {
            return HitLocation.RightLeg;
        }

        if (roll < leftLeg)
        {
            return HitLocation.LeftLeg;
        }

        if (roll < swordArm)
        {
            return HitLocation.SwordArm;
        }

        return roll < offArm ? HitLocation.OffArm : HitLocation.Head;
    }

    private void Die(Combatant c, DeathCause cause)
    {
        c.BeginState(CombatState.Dead, 0);
        c.Health = 0;
        c.DeathCause = cause;
        Emit(new WarriorDied(ElapsedSeconds, c.Id, cause));
    }

    // ------------------------------------------------------------------ helpers

    private void RegenerateStamina(Combatant c)
    {
        double max = c.Warrior.EffectiveStats.MaxStamina;
        c.Stamina = Math.Min(max, c.Stamina + (_tuning.StaminaRegenPerSecond * _tuning.TickSeconds));
    }

    private void ConsultRetreatPolicy(Combatant c)
    {
        if (_setup.RetreatPolicy is null || c.RetreatRequested || c.Team != PlayerTeam)
        {
            return;
        }

        if (!_contactMade)
        {
            // The policy imitates the player's key; if the key is off before contact, the policy is
            // silent too. Otherwise the refused-press counter would inflate in simulation.
            return;
        }

        WarriorStats stats = c.Warrior.EffectiveStats;
        var context = new RetreatContext(
            c.Id,
            c.Health / stats.MaxHealth,
            c.Stamina / stats.MaxStamina,
            ElapsedSeconds,
            CountActive(PlayerTeam),
            CountActive(EnemyTeam));

        if (_setup.RetreatPolicy.ShouldRetreat(in context))
        {
            // The policy looks at a single warrior but the command covers the whole team — the same
            // rule as the player's key (see docs/GDD.md §5).
            CommandRetreat();
        }
    }

    private double StaminaFactor(Combatant c)
    {
        double fraction = c.Stamina / c.Warrior.EffectiveStats.MaxStamina;
        return fraction < _tuning.LowStaminaThreshold ? _tuning.LowStaminaPenalty : 1.0;
    }

    private double SpacingSeconds(Combatant c)
    {
        double t = Math.Clamp(c.Warrior.EffectiveStats.Aggression / 100.0, 0, 1);
        return _tuning.SpacingSecondsAtZeroAggression
               + ((_tuning.SpacingSecondsAtMaxAggression - _tuning.SpacingSecondsAtZeroAggression) * t);
    }

    /// <remarks>
    /// A plain loop instead of LINQ: these two helpers are called for every warrior on every tick, and
    /// the variables the lambdas captured allocated hundreds of thousands of bytes per fight. Batch
    /// simulation runs tens of thousands of fights; the hot loop must not allocate
    /// (see <c>ThroughputTests</c>).
    /// </remarks>
    /// <summary>
    /// The attacker's target: the one he has if it is still standing, otherwise the <b>nearest</b> enemy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The target is <b>sticky</b>: once chosen it is kept until the enemy dies or flees. Returning to
    /// the nearest on every tick would make warriors oscillate between two enemies and never land a
    /// hit.
    /// </para>
    /// <para>
    /// Before space existed the target was chosen at random — with no such thing as distance, there
    /// was no other meaningful rule. Now the rule comes out of space as it does in Domina: the nearest
    /// enemy the sword can reach.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The warrior's current target; if he has none, makes him choose again.
    /// </summary>
    /// <remarks>
    /// The target is chosen at <b>decision steps</b> (<see cref="ChooseTarget"/>), not on every tick.
    /// The movement loop reads here too and takes the choice as it is: were the scoring to run per
    /// tick, the choice would flicker every frame and the cost would grow with the square of the
    /// warrior count — measured, 10,000 fights went to twice the budget.
    /// </remarks>
    private Combatant? FindTarget(Combatant attacker) =>
        attacker.Target is { IsActive: true } current ? current : ChooseTarget(attacker);

    /// <inheritdoc cref="TargetScore"/>
    private Combatant? ChooseTarget(Combatant attacker)
    {
        Combatant? best = null;
        double bestScore = double.NegativeInfinity;

        foreach (Combatant c in _combatants)
        {
            if (c.Team == attacker.Team || !c.IsActive)
            {
                continue;
            }

            double score = TargetScore(attacker, c);

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        attacker.Target = best;
        return best;
    }

    /// <summary>
    /// How attractive an enemy is to this warrior. The highest is chosen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule replaces "pick the nearest and stay loyal to him until he dies". The old form was not
    /// a decision: a wounded enemy went unnoticed, a region exposed by broken armour went unnoticed,
    /// and three warriors did not know whether they had piled onto the same target.
    /// </para>
    /// <para>
    /// No die — the scoring is <b>deterministic</b>. The randomness is not in the decision itself but
    /// in the warrior's identity: two warriors standing on the same field choose different targets with
    /// different stats. With a die, the choice would jitter every tick and measurement would drown in noise.
    /// </para>
    /// <para>
    /// <b>Stickiness is required:</b> the current target gets extra points. Without it, a warrior
    /// caught between two enemies would change direction at every decision step and reach neither —
    /// the price of switching targets is that the road already walked is wasted.
    /// </para>
    /// </remarks>
    private double TargetScore(Combatant attacker, Combatant enemy)
    {
        WarriorStats stats = enemy.Warrior.EffectiveStats;

        // 1) Distance — on its own, the old rule itself. An enemy within reach takes no penalty;
        // the penalty is paid only for the road to be walked.
        double gap = Math.Max(0, attacker.Position.DistanceTo(enemy.Position) - attacker.Weapon.Reach);
        double score = -gap * _tuning.TargetDistanceWeight;

        // An opportunity is only an opportunity as far as it can be reached: a wound and an exposed
        // region count while they are in front of the warrior, not at the far end of the arena.
        // Without the bound, measurement turned the rule into an outright difficulty increase — the
        // warrior left the healthy enemy beside him and walked to the distant wounded one, taking free hits along the way.
        double opportunity = _tuning.TargetOpportunityRange <= 0
            ? 0
            : 1 - Math.Clamp(gap / _tuning.TargetOpportunityRange, 0, 1);

        // 2) Wound — an enemy close to being finished is finished first. A numbers advantage settles
        // the fight itself: felling one enemy is better than wounding three.
        double missing = stats.MaxHealth <= 0 ? 0 : 1 - Math.Clamp(enemy.Health / stats.MaxHealth, 0, 1);
        score += missing * opportunity * _tuning.TargetWoundedWeight;

        // 3) Exposed region — an enemy whose armour has broken is softer and the warrior sees it.
        // Armour wear's in-combat meaning closes here (docs/GDD.md §7).
        score += enemy.DestroyedArmor.Count() / 6.0 * opportunity * _tuning.TargetExposedWeight;

        // 4) Crowd — piling onto the same target is the natural result of the first three items, but
        // left unbounded, the team chases one enemy while the other two strike for free.
        int engaged = 0;

        foreach (Combatant mate in _combatants)
        {
            if (mate.Team == attacker.Team && mate.Id != attacker.Id && mate.Target == enemy)
            {
                engaged++;
            }
        }

        score -= engaged * _tuning.TargetCrowdPenalty;

        // 5) Stickiness — the price of changing direction.
        if (attacker.Target == enemy)
        {
            score += _tuning.TargetStickiness;
        }

        return score;
    }

    /// <summary>A random one of the standing warriors on the other side; <c>null</c> if there is none.</summary>
    /// <remarks>
    /// It makes two passes and allocates no array — because it is called in the hot loop
    /// (see <c>ThroughputTests.PerBattleAllocationStaysSmallWithoutEvents</c>).
    /// </remarks>
    private Combatant? RandomEnemy(int team)
    {
        int count = 0;
        foreach (Combatant c in _combatants)
        {
            if (c.Team != team && c.IsActive)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        int wanted = _rng.NextInt(count);
        foreach (Combatant c in _combatants)
        {
            if (c.Team != team && c.IsActive && wanted-- == 0)
            {
                return c;
            }
        }

        return null;
    }

    /// <inheritdoc cref="FindTarget"/>
    private int CountActive(int team)
    {
        int count = 0;
        foreach (Combatant c in _combatants)
        {
            if (c.Team == team && c.IsActive)
            {
                count++;
            }
        }

        return count;
    }

    private void Emit(BattleEvent e)
    {
        if (_setup.CollectEvents)
        {
            _events.Add(e);
        }
    }

    private bool Finish()
    {
        if (CountActive(PlayerTeam) == 0)
        {
            // Withdrawing and being wiped out are not the same: one spends the expedition, the other the roster.
            bool anyoneEscaped = _combatants.Exists(
                c => c.Team == PlayerTeam && c.State == CombatState.Escaped);

            Complete(anyoneEscaped ? BattleOutcome.PlayerWithdrawal : BattleOutcome.PlayerWipe);
            return true;
        }

        if (CountActive(EnemyTeam) == 0)
        {
            Complete(BattleOutcome.PlayerVictory);
            return true;
        }

        return false;
    }

    private void Complete(BattleOutcome outcome)
    {
        IsFinished = true;

        // Projectiles still in the air when the fight ends count as misses. Had they vanished silently
        // they would hang on screen in the visualisation: the launch event exists, the outcome does not.
        foreach (Projectile p in _projectiles)
        {
            Emit(new ProjectileMissed(ElapsedSeconds, p.Attacker.Id, p.Target.Id));
        }

        _projectiles.Clear();

        Emit(new BattleEnded(ElapsedSeconds, outcome));

        var summaries = _combatants.ConvertAll(c => new WarriorBattleSummary(
            c.Id,
            c.Warrior.Name,
            c.Team,
            c.State,
            Math.Max(0, c.Health),
            c.AttacksMade,
            c.HitsLanded,
            c.TimesHit,
            c.DodgesPerformed,
            c.DamageDealt,
            c.DamageTaken,
            c.LostLimb)
        {
            LostParts = _lostParts.GetValueOrDefault(c.Id),
            DestroyedArmor = _destroyedArmor.GetValueOrDefault(c.Id),
            ArmorWear = c.ArmorWear,
            BlocksPerformed = c.BlocksPerformed,
            TimesStunned = c.TimesStunned,
            StunsInflicted = c.StunsInflicted,
            DeathCause = c.DeathCause,
            TimesPoisoned = c.TimesPoisoned,
            PoisonsInflicted = c.PoisonsInflicted,
            PoisonDamageTaken = c.PoisonDamageTaken,
            PoisonDamageDealt = c.PoisonDamageDealt,
            CatchesMade = c.CatchesMade,
            TimesCaught = c.TimesCaught,
            Disarmed = c.Disarmed,
            TimesDisarmed = c.TimesDisarmed,
            DisarmsInflicted = c.DisarmsInflicted,
            WeaponsPickedUp = c.WeaponsPickedUp,
            ChargesStarted = c.ChargesStarted,
            ChargesConnected = c.ChargesConnected,
            ChargeOpportunitiesTaken = c.ChargeOpportunitiesTaken,
            ChargesBroken = c.ChargesBroken,
            ChargeStartSecondsSum = c.ChargeStartSecondsSum,
            LastChargeStartSeconds = c.LastChargeStartSeconds,
        });

        Result = new BattleResult(outcome, ElapsedSeconds, summaries);
    }
}
