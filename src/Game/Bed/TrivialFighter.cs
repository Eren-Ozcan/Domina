using Godot;

namespace Domina.Game.Bed;

/// <summary>One man in the stand-in fight: his nodes, and the little state the fight keeps on him.</summary>
internal sealed class TrivialFighter
{
    private readonly TrivialFight _fight;

    public TrivialFighter(TrivialFight fight, Node2D root, TrivialKit kit, BedBody bodyKind, int index, int team,
        double maxHealth, Vector2 start)
    {
        _fight = fight;
        Index = index;
        Team = team;
        MaxHealth = maxHealth;
        Start = start;
        Layer = team == 0 ? 1u : 2u;

        Body = bodyKind == BedBody.Static
            ? new StaticBody2D()
            : new CharacterBody2D { MotionMode = CharacterBody2D.MotionModeEnum.Floating };
        Body.Name = $"Fighter{index}";
        Body.CollisionLayer = Layer;
        Body.CollisionMask = 3;
        Body.Position = start;
        Body.AddChild(new CollisionShape2D { Shape = kit.BodyShape });

        Blade = new Node2D { Name = "Blade", Rotation = TrivialKit.RestAngle };
        Body.AddChild(Blade);

        Cue = new StrikeCue { Name = "Cue", Fighter = this };
        Body.AddChild(Cue);

        // Manual: nothing advances the clip but the fight's own tick. Immediate: the key's call happens
        // inside that advance, not at the end of the engine frame — a deferred blow would land after the
        // whole manual lane had run.
        Player = new AnimationPlayer
        {
            Name = "Player",
            CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual,
            CallbackModeMethod = AnimationMixer.AnimationCallbackModeMethod.Immediate,
        };
        Player.AddAnimationLibrary(string.Empty, kit.Library);
        Body.AddChild(Player);

        BladeQuery = new PhysicsShapeQueryParameters2D
        {
            Shape = kit.BladeShape,
            CollisionMask = team == 0 ? 2u : 1u,
            CollideWithBodies = true,
            CollideWithAreas = false,
        };

        root.AddChild(Body);
        Rid = Body.GetRid();
    }

    public int Index { get; }

    public int Team { get; }

    public double MaxHealth { get; }

    public Vector2 Start { get; }

    public uint Layer { get; }

    public PhysicsBody2D Body { get; }

    public Rid Rid { get; }

    public Node2D Blade { get; }

    public StrikeCue Cue { get; }

    public AnimationPlayer Player { get; }

    public PhysicsShapeQueryParameters2D BladeQuery { get; }

    public double Health { get; set; }

    public bool Alive { get; set; }

    /// <summary>Where the fight last put him. Kept on this side so reading it costs no engine call.</summary>
    public Vector2 Position { get; set; }

    public TrivialFighter? Target { get; set; }

    /// <summary>Ticks into the current swing, or -1 when he is not swinging.</summary>
    public int SwingTicks { get; set; } = -1;

    /// <summary>Has this swing's blow been struck? Only <see cref="BedClip.Keys"/> keeps it.</summary>
    public bool Landed { get; set; }

    /// <summary>The clip's key calls this, through <see cref="StrikeCue"/>.</summary>
    public void Land() => _fight.Land(this);
}
