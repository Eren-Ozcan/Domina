using Godot;

namespace Domina.Game;

/// <summary>The emblems the screens are allowed to draw.</summary>
/// <remarks>
/// The list is deliberately short. An emblem is here to tell two chips apart at a glance in a row of
/// eight, not to illustrate anything: whenever a new one would only repeat what the caption beside it
/// already says, it is not added.
/// </remarks>
public enum Mark
{
    /// <summary>No emblem — the caption stands on its own.</summary>
    None,

    /// <summary>Gold: a coin.</summary>
    Coin,

    /// <summary>Food: a grain.</summary>
    Grain,

    /// <summary>Water: a drop.</summary>
    Drop,

    /// <summary>Medicine: a cross.</summary>
    Cross,

    /// <summary>Sake: a cup.</summary>
    Cup,

    /// <summary>Spirits, and anything else about how the men feel: a flame.</summary>
    Flame,

    /// <summary>A warrior, and a count of them: a head and shoulders.</summary>
    Person,

    /// <summary>The dead: a mound.</summary>
    Grave,

    /// <summary>A weapon, a fight, a bounty: a blade.</summary>
    Blade,

    /// <summary>Armour, the quartermaster, anything defended: a shield.</summary>
    Shield,

    /// <summary>The school and what is learned there: a scroll.</summary>
    Scroll,

    /// <summary>The province, a posting, a patron's house: a pennant.</summary>
    Banner,

    /// <summary>The day and the season's clock: a sun.</summary>
    Sun,
}

/// <summary>
/// Draws one <see cref="Mark"/> inside its own box, in a single colour.
/// </summary>
/// <remarks>
/// Everything is drawn against a 1×1 box and multiplied up by whatever size the control was given, so
/// one emblem sits correctly beside a 13px caption and beside a 20px figure without a second set of
/// numbers. The control never picks its own colour: the caller passes the ink it wants, because an
/// emblem that argued with the text beside it would be the exact noise these were added to remove.
/// </remarks>
public sealed partial class MarkIcon : Control
{
    /// <summary>Which emblem to draw.</summary>
    public Mark Mark { get; init; }

    /// <summary>The one colour it is drawn in.</summary>
    public Color Tint { get; init; } = Colors.White;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public override void _Draw()
    {
        float s = Mathf.Min(Size.X, Size.Y);
        if (s <= 0)
        {
            return;
        }

        Vector2 origin = new((Size.X - s) / 2f, (Size.Y - s) / 2f);
        Vector2 At(float x, float y) => origin + new Vector2(x * s, y * s);

        switch (Mark)
        {
            case Mark.Coin:
                // A ring and a pip rather than a disc with a hole punched in it: the draw calls paint
                // over each other, they do not cut, so a "hole" would only be a second coloured dot.
                DrawArc(At(0.5f, 0.5f), s * 0.34f, 0, Mathf.Tau, 24, Tint, s * 0.16f);
                DrawCircle(At(0.5f, 0.5f), s * 0.09f, Tint);
                break;

            case Mark.Grain:
                DrawColoredPolygon([At(0.5f, 0.02f), At(0.74f, 0.48f), At(0.5f, 0.94f), At(0.26f, 0.48f)], Tint);
                DrawLine(At(0.5f, 0.16f), At(0.5f, 0.80f), new Color(0, 0, 0, 0.45f), s * 0.10f);
                break;

            case Mark.Drop:
                DrawCircle(At(0.5f, 0.64f), s * 0.33f, Tint);
                DrawColoredPolygon([At(0.5f, 0.02f), At(0.78f, 0.66f), At(0.22f, 0.66f)], Tint);
                break;

            case Mark.Cross:
                DrawRect(new Rect2(At(0.38f, 0.06f), new Vector2(s * 0.24f, s * 0.88f)), Tint);
                DrawRect(new Rect2(At(0.06f, 0.38f), new Vector2(s * 0.88f, s * 0.24f)), Tint);
                break;

            case Mark.Cup:
                DrawColoredPolygon([At(0.12f, 0.28f), At(0.88f, 0.28f), At(0.64f, 0.86f), At(0.36f, 0.86f)], Tint);
                break;

            case Mark.Flame:
                DrawColoredPolygon(
                    [At(0.5f, 0f), At(0.88f, 0.50f), At(0.74f, 0.92f), At(0.26f, 0.92f), At(0.12f, 0.50f)],
                    Tint);
                DrawColoredPolygon(
                    [At(0.5f, 0.44f), At(0.68f, 0.70f), At(0.5f, 0.90f), At(0.32f, 0.70f)],
                    new Color(0, 0, 0, 0.45f));
                break;

            case Mark.Person:
                DrawCircle(At(0.5f, 0.26f), s * 0.22f, Tint);
                DrawColoredPolygon([At(0.14f, 0.98f), At(0.28f, 0.56f), At(0.72f, 0.56f), At(0.86f, 0.98f)], Tint);
                break;

            case Mark.Grave:
                DrawRect(new Rect2(At(0.10f, 0.84f), new Vector2(s * 0.80f, s * 0.14f)), Tint);
                DrawColoredPolygon([At(0.26f, 0.84f), At(0.34f, 0.24f), At(0.66f, 0.24f), At(0.74f, 0.84f)], Tint);
                break;

            case Mark.Blade:
                DrawColoredPolygon(
                    [At(0.80f, 0.04f), At(0.90f, 0.16f), At(0.34f, 0.78f), At(0.22f, 0.82f), At(0.26f, 0.68f)],
                    Tint);
                DrawLine(At(0.20f, 0.80f), At(0.06f, 0.96f), Tint, s * 0.14f);
                break;

            case Mark.Shield:
                DrawColoredPolygon(
                    [At(0.5f, 0.02f), At(0.92f, 0.20f), At(0.80f, 0.74f), At(0.5f, 0.98f), At(0.20f, 0.74f), At(0.08f, 0.20f)],
                    Tint);
                break;

            case Mark.Scroll:
                DrawRect(new Rect2(At(0.20f, 0.04f), new Vector2(s * 0.60f, s * 0.92f)), Tint);
                DrawRect(new Rect2(At(0.32f, 0.28f), new Vector2(s * 0.36f, s * 0.08f)), new Color(0, 0, 0, 0.5f));
                DrawRect(new Rect2(At(0.32f, 0.48f), new Vector2(s * 0.36f, s * 0.08f)), new Color(0, 0, 0, 0.5f));
                DrawRect(new Rect2(At(0.32f, 0.68f), new Vector2(s * 0.36f, s * 0.08f)), new Color(0, 0, 0, 0.5f));
                break;

            case Mark.Banner:
                DrawRect(new Rect2(At(0.14f, 0.02f), new Vector2(s * 0.14f, s * 0.96f)), Tint);
                DrawColoredPolygon([At(0.28f, 0.06f), At(0.94f, 0.06f), At(0.72f, 0.34f), At(0.94f, 0.62f), At(0.28f, 0.62f)], Tint);
                break;

            case Mark.Sun:
                DrawArc(At(0.5f, 0.5f), s * 0.34f, 0, Mathf.Tau, 24, Tint, s * 0.14f);
                DrawLine(At(0.5f, 0f), At(0.5f, 0.12f), Tint, s * 0.12f);
                DrawLine(At(0.5f, 0.88f), At(0.5f, 1f), Tint, s * 0.12f);
                DrawLine(At(0f, 0.5f), At(0.12f, 0.5f), Tint, s * 0.12f);
                DrawLine(At(0.88f, 0.5f), At(1f, 0.5f), Tint, s * 0.12f);
                break;

            default:
                break;
        }
    }
}
