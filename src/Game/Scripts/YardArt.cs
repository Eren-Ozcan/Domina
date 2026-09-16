using Godot;

namespace Domina.Game;

/// <summary>
/// The cut-paper shapes the yard is built from.
/// </summary>
/// <remarks>
/// <para>
/// Every figure, building and prop is a flat stand-in for real art, traced from the design canvas's
/// own boards (8a, 8b) so that the staging — what stands where, and how big it reads from the far side
/// of the yard — is the thing that survives into the real art rather than being re-invented with it.
/// </para>
/// <para>
/// Each shape is a polygon in its object's own coordinates, listed back to front. A rectangle is a
/// polygon like anything else: the engine draws one primitive, and the code that stands the objects in
/// the yard does not have to know which of them happened to be square.
/// </para>
/// </remarks>
public static class YardArt
{
    /// <summary>The roofs of the town behind the wall: one broken line across the sky.</summary>
    public static IReadOnlyList<(Color Fill, Vector2[] Points)> Roofs() =>
    [
        (Hex(0x1D1E28), Points(
            0, 240, 0, 150, 180, 74, 330, 128, 520, 52, 700, 140, 880, 96, 1060, 168,
            1240, 110, 1450, 164, 1650, 104, 1830, 158, 1920, 126, 1920, 240)),
    ];

    /// <summary>The hall at the back of the yard, with its doorway standing open on the dark.</summary>
    public static IReadOnlyList<(Color Fill, Vector2[] Points)> Hall() =>
    [
        (Hex(0x100D0A), Points(0, 242, 0, 74, 300, 74, 300, 242, 216, 242, 216, 132, 84, 132, 84, 242)),
        (Hex(0x211B15), Points(-6, 74, 306, 74, 276, 42, 24, 42)),
        (Hex(0x0A0806), Rect(84, 132, 132, 110)),
    ];

    /// <summary>The hut, and the one lit window in it.</summary>
    public static IReadOnlyList<(Color Fill, Vector2[] Points)> Hut() =>
    [
        (Hex(0x191512), Points(12, 120, 150, 44, 288, 120, 288, 284, 12, 284)),
        (Hex(0xB98F4A), Rect(72, 164, 72, 66)),
        (Hex(0x191512), Rect(104, 164, 8, 66)),
    ];

    /// <summary>
    /// The board: two posts, a crossbeam, and the day's paper nailed to it.
    /// </summary>
    /// <remarks>
    /// The paper is the brightest thing in the yard at night, which is the whole of the board's claim
    /// on the player's attention — no dot, no count, no badge (design canvas → 7b, "something new").
    /// </remarks>
    public static IReadOnlyList<(Color Fill, Vector2[] Points)> Board() =>
    [
        (Hex(0x241E18), Rect(34, 96, 16, 172)),
        (Hex(0x241E18), Rect(166, 96, 16, 172)),
        (Hex(0x2B241C), Points(18, 74, 198, 74, 216, 96, 0, 96)),
        (Hex(0xE9DFC9), Rect(62, 108, 92, 124)),
    ];

    /// <summary>The rack: a roof, two rails, five blades and the butts of five hafts under them.</summary>
    public static IReadOnlyList<(Color Fill, Vector2[] Points)> Rack()
    {
        List<(Color, Vector2[])> shapes =
        [
            (Hex(0x3D332A), Points(0, 96, 258, 54, 258, 84, 0, 126)),
            (Hex(0x3D332A), Rect(6, 120, 16, 150)),
            (Hex(0x3D332A), Rect(236, 78, 16, 192)),
            (Hex(0x463B2F), Rect(22, 168, 214, 12)),
            (Hex(0x463B2F), Rect(22, 220, 214, 12)),
        ];

        // The steel is what brightens under the cursor, so each blade is its own shape rather than one
        // wide bar with gaps painted into it.
        (int X, int Y, int Height)[] blades =
        [
            (46, 120, 106), (76, 126, 100), (106, 118, 108), (146, 124, 102), (186, 116, 110),
        ];

        foreach ((int x, int y, int height) in blades)
        {
            shapes.Add((Hex(0xB9B6B0), Rect(x, y, 9, height)));
        }

        foreach ((int x, int _, int _) in blades)
        {
            shapes.Add((Hex(0x5A4A38), Rect(x - 2, 226, 13, 26)));
        }

        return shapes;
    }

    /// <summary>The post: a shelter over the drill ground, the post itself, and the beam across it.</summary>
    public static IReadOnlyList<(Color Fill, Vector2[] Points)> Post() =>
    [
        (Hex(0x211B15), Points(0, 96, 176, 96, 150, 54, 26, 54)),
        (Hex(0x241E18), Rect(14, 96, 12, 148)),
        (Hex(0x241E18), Rect(150, 96, 12, 148)),
        (Hex(0x3A3028), Rect(114, 168, 18, 132)),
        (Hex(0x4F4334), Rect(96, 148, 54, 22)),
    ];

    /// <summary>The cart: a roof over a crate, on two wheels, drawn up against the wall.</summary>
    public static IReadOnlyList<(Color Fill, Vector2[] Points)> Cart() =>
    [
        (Hex(0x191512), Points(18, 94, 302, 94, 320, 134, 0, 134)),
        (Hex(0x241E18), Rect(26, 134, 268, 62)),
        (Hex(0x2B241C), Circle(76, 206, 36)),
        (Hex(0x2B241C), Circle(246, 206, 36)),
    ];

    /// <summary>A man standing in the yard, with a blade at his side.</summary>
    public static IReadOnlyList<(Color Fill, Vector2[] Points)> Man() =>
    [
        (Hex(0x100D0A), Scaled(
            120f / 84f,
            216f / 150f,
            42, 8, 52, 15, 52, 24, 47, 28, 78, 38, 80, 58, 60, 62, 58, 104,
            62, 142, 22, 142, 26, 104, 24, 62, 4, 58, 6, 38, 37, 28, 32, 24, 32, 15)),
        (Hex(0x100D0A), Scaled(120f / 84f, 216f / 150f, 10, 72, 74, 72, 74, 79, 10, 79)),
    ];

    /// <summary>
    /// The gate: the way out of the yard, standing open on a road that goes dark at once.
    /// </summary>
    /// <remarks>
    /// It is drawn as a hole in the wall rather than as a door, because a closed door is the one thing
    /// in the set that means <i>barred</i> (design canvas → 7b, the fifth state).
    /// </remarks>
    public static IReadOnlyList<(Color Fill, Vector2[] Points)> Gate() =>
    [
        (Hex(0x211B15), Points(0, 84, 200, 84, 176, 40, 24, 40)),
        (Hex(0x241E18), Rect(10, 84, 22, 176)),
        (Hex(0x241E18), Rect(168, 84, 22, 176)),
        (Hex(0x0A0806), Rect(32, 108, 136, 152)),
    ];

    /// <summary>A rectangle, as the polygon it is.</summary>
    private static Vector2[] Rect(float x, float y, float width, float height) =>
        [new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)];

    /// <summary>A circle, cut into enough sides that the wheel of a cart does not read as a nut.</summary>
    private static Vector2[] Circle(float x, float y, float radius)
    {
        const int Sides = 24;
        Vector2[] points = new Vector2[Sides];

        for (int i = 0; i < Sides; i++)
        {
            float angle = Mathf.Tau * i / Sides;
            points[i] = new Vector2(x + (Mathf.Cos(angle) * radius), y + (Mathf.Sin(angle) * radius));
        }

        return points;
    }

    /// <summary>A polygon written out as x, y, x, y — the way the canvas writes one.</summary>
    private static Vector2[] Points(params float[] pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        Vector2[] points = new Vector2[pairs.Length / 2];

        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector2(pairs[i * 2], pairs[(i * 2) + 1]);
        }

        return points;
    }

    /// <summary>The same, stretched — the canvas draws its figures in a box of another size.</summary>
    private static Vector2[] Scaled(float x, float y, params float[] pairs)
    {
        Vector2[] points = Points(pairs);

        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector2(points[i].X * x, points[i].Y * y);
        }

        return points;
    }

    private static Color Hex(uint rgb) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
}
