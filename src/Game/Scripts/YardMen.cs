using Domina.Core.Dojo;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The men themselves, standing on the ground of the yard and working at what they were set to.
/// </summary>
/// <remarks>
/// <para>
/// The yard had a cut-paper figure standing in it that meant "the roster is over here". This is the
/// roster itself: one <see cref="WarriorRig"/> a man, wearing what the player bought him, drilling at
/// whatever he was put on this morning. A player who set three men on conditioning and one on the
/// post can see that from where he stands, without opening a sheet — which is what the hub being a
/// <b>place</b> was for (design canvas → 7c).
/// </para>
/// <para>
/// The poses come from <see cref="DrillAnimator"/> in the engine-free assembly, the same way the
/// fight's come from <see cref="RigAnimator"/>. Nothing here decides what a drill looks like; the job
/// left in the engine is standing the figures on the ground and handing each one the clock.
/// </para>
/// <para>
/// It is rebuilt when the roster changes, not every frame: the figures are nodes, and a man who has
/// not changed his drill since yesterday keeps the body he was built with.
/// </para>
/// </remarks>
public sealed partial class YardMen : Node2D
{
    /// <summary>How many figures the ground has room for before they stand on one another.</summary>
    /// <remarks>
    /// The roster's own ceiling is the quarters branch's bed count; this is the <b>ground's</b>. Past
    /// it the men would be drawn in a crowd nobody could read, so the overflow is left to the roster
    /// sheet, which is the screen for reading a whole roster anyway.
    /// </remarks>
    private const int Room = 8;

    /// <summary>How wide the trodden ground is.</summary>
    private const float Width = 450f;

    /// <summary>Where the near row's feet are, and how far back the far row stands.</summary>
    private const float NearRow = 132f;
    private const float FarRow = 58f;

    /// <summary>
    /// The colour the men are drawn in, at night, on the yard's own ground.
    /// </summary>
    /// <remarks>
    /// It is not the arena's indigo: nothing here is a side, so nothing needs the tint that says which
    /// side a figure is on. What it has to be is <b>legible against the dark ground</b> and no brighter
    /// than the board's paper, which is the one thing in the yard that is allowed to be the brightest.
    /// </remarks>
    private static readonly Color Ink = new(0.58f, 0.54f, 0.47f);

    private readonly List<Figure> _figures = [];

    /// <summary>A man on the ground: his body, what he is at, and his own clock.</summary>
    private sealed record Figure(WarriorRig Rig, DojoActivity Activity, Drill Drill, double Offset)
    {
        public double Clock { get; set; }
    }

    /// <summary>
    /// Stands the roster on the ground.
    /// </summary>
    /// <remarks>
    /// The dead, the freed and the masters of the house are not here: the first two are no longer in
    /// the dojo and the third does not train. What is left is the men a day can be spent on, which is
    /// the same set the roster sheet calls living.
    /// </remarks>
    public void Stand(IReadOnlyList<RosterRow> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        Clear();

        List<RosterRow> standing = [];

        foreach (RosterRow row in roster)
        {
            if (row.IsAlive && row.Status is RosterStatus.Ready or RosterStatus.Training or RosterStatus.Recovering)
            {
                standing.Add(row);
            }

            if (standing.Count == Room)
            {
                break;
            }
        }

        for (int i = 0; i < standing.Count; i++)
        {
            Place(standing[i], i, standing.Count);
        }
    }

    /// <summary>Takes every figure off the ground.</summary>
    public void Clear()
    {
        foreach (Figure figure in _figures)
        {
            RemoveChild(figure.Rig);
            figure.Rig.QueueFree();
        }

        _figures.Clear();
    }

    public override void _Process(double delta)
    {
        foreach (Figure figure in _figures)
        {
            figure.Clock += delta;
            figure.Rig.Pose(DrillAnimator.Pose(figure.Activity, figure.Drill, figure.Clock + figure.Offset));
        }
    }

    /// <summary>Builds one man and stands him in his place on the ground.</summary>
    /// <remarks>
    /// The two rows are what keeps eight men from reading as a line of fence posts, and the near row is
    /// drawn larger and later so the ground has a front and a back. Which row a man is in is his index
    /// and not a die: the yard must not reshuffle itself every time a sheet closes over it.
    /// </remarks>
    private void Place(RosterRow row, int index, int count)
    {
        bool near = index % 2 == 0;

        WarriorRig rig = new();
        AddChild(rig);

        // The men are spread across the whole ground by their place in the roster and put in the near
        // or the far row by turns. Spreading them by row instead would stand a dojo of two men in one
        // corner of a ground built for eight.
        float x = Width * ((index + 0.5f) / Math.Max(1, count));

        // He faces the middle of the ground, so the two ends of the line are not all looking off the
        // same edge of the yard.
        float facing = x > Width / 2 ? -1f : 1f;
        rig.Build(row.Id, row.Name, Ink, facing, row.Kit);
        rig.Position = new Vector2(x, near ? NearRow : FarRow);

        // The far row stands further off, so it is drawn smaller; the rig's own scale carries the
        // facing, which is why the fit multiplies rather than replaces it.
        float scale = near ? 0.52f : 0.42f;
        rig.Scale = new Vector2(scale * facing, scale);
        rig.ZIndex = near ? 1 : 0;

        // A man in the infirmary is drawn faded rather than removed: he is in the dojo and the player
        // is paying to feed him, which the ground should not hide.
        rig.Modulate = row.Status == RosterStatus.Recovering
            ? new Color(1f, 1f, 1f, 0.55f)
            : Colors.White;

        // Each man is given his own place in the cycle, or eight men press to the same beat and the
        // ground reads as one drill performed by a chorus.
        _figures.Add(new Figure(rig, ActivityOf(row), row.Drill, index * 0.47));
    }

    /// <summary>What he is at today, as the ground draws it.</summary>
    /// <remarks>
    /// The core's occupation is read rather than the row's badge: a man may be set to train and be in
    /// the infirmary at the same time, and the infirmary is what he is actually doing today.
    /// </remarks>
    private static DojoActivity ActivityOf(RosterRow row) => row.Status switch
    {
        RosterStatus.Recovering => DojoActivity.Recovering,
        RosterStatus.Training => DojoActivity.Training,
        _ => row.Activity,
    };
}
