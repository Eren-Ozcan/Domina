using Godot;

namespace Domina.Game.Bed;

/// <summary>
/// The resources every stand-in fighter shares: the body's shape, the blade's reach and the strike clip.
/// </summary>
/// <remarks>
/// <para>
/// The clip is the point of the kit. Its method key is <b>the frame the blow lands</b> (the plan's
/// Phase F): under <see cref="BedClip.Mixer"/> an <c>AnimationPlayer</c> runs the clip and the key calls
/// the blow; under <see cref="BedClip.Keys"/> the key's time is read off this same clip once and the
/// fight compares its tick count to it. One clip, two ways of reading it — so the bed can say what the
/// mixer costs without the two disagreeing about when the blade connects.
/// </para>
/// <para>
/// The key sits at 0.375 s, between two ticks (0.35 and 0.40). A key on a tick boundary would fire on
/// whichever side of it the summed tick lengths happen to round to; between two ticks there is no doubt.
/// </para>
/// </remarks>
internal sealed class TrivialKit : IDisposable
{
    public const string StrikeName = "strike";

    public const float BodyRadius = 14f;

    /// <summary>Centre to centre: a fighter strikes a man whose centre is this close.</summary>
    public const float Reach = 44f;

    public const float RestAngle = -1.2f;

    private const float StrikeAngle = 1.0f;
    private const double ClipSeconds = 1.0;
    private const double KeySeconds = 0.375;

    public TrivialKit()
    {
        BodyShape = new CircleShape2D { Radius = BodyRadius };

        // The blade query is a circle round the striker that touches a body exactly when the centres are
        // within reach.
        BladeShape = new CircleShape2D { Radius = Reach - BodyRadius };

        var clip = new Animation { Length = (float)ClipSeconds };

        // The picture: the blade swings out and back. Nothing in the fight reads it.
        int blade = clip.AddTrack(Animation.TrackType.Value);
        clip.TrackSetPath(blade, "Blade:rotation");
        clip.TrackInsertKey(blade, 0.0, RestAngle);
        clip.TrackInsertKey(blade, KeySeconds, StrikeAngle);
        clip.TrackInsertKey(blade, ClipSeconds, RestAngle);

        int cue = clip.AddTrack(Animation.TrackType.Method);
        clip.TrackSetPath(cue, "Cue");
        clip.TrackInsertKey(
            cue,
            KeySeconds,
            new Godot.Collections.Dictionary { ["method"] = nameof(StrikeCue.Land), ["args"] = new Godot.Collections.Array() });

        Library = new AnimationLibrary();
        Library.AddAnimation(StrikeName, clip);

        LandSeconds = clip.TrackGetKeyTime(cue, 0);
        ClipTicks = (int)Math.Round(ClipSeconds / TrivialFight.TickSeconds);
    }

    public CircleShape2D BodyShape { get; }

    public CircleShape2D BladeShape { get; }

    public AnimationLibrary Library { get; }

    /// <summary>The method key's time, read off the clip — not the constant it was written from.</summary>
    public double LandSeconds { get; }

    public int ClipTicks { get; }

    public void Dispose()
    {
        Library.Dispose();
        BladeShape.Dispose();
        BodyShape.Dispose();
    }
}
