using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The cut-paper ground the fight is watched on.
/// </summary>
/// <remarks>
/// <para>
/// The arena was a single line across the screen with two figures on it, and a fight read as two
/// stickmen in a void: nothing said where the men were standing, and the ground the choreography
/// already works in — a near edge and a far one, with depth between them — was invisible.
/// </para>
/// <para>
/// It is the yard's own set (<see cref="YardArt"/>): flat polygons, listed back to front, in the same
/// night palette. Nothing here is lit, animated or interactive, and <b>nothing here is read by the
/// simulation</b> — the ground is drawn from <see cref="ArenaLayout"/>, which is the same record the
/// choreography stands the men on, so the paper and the men cannot drift apart.
/// </para>
/// <para>
/// It is deliberately plain. A drawn arena is a stand-in until real art arrives, and what has to
/// survive into that art is the staging — where the far edge is, where the near edge is, and that the
/// men fight on the band between them.
/// </para>
/// </remarks>
public static class ArenaArt
{
    /// <summary>The whole set for one fight, back to front.</summary>
    /// <param name="layout">The ground the choreography stands the men on.</param>
    /// <param name="height">The window's height, so the near apron reaches the bottom of it.</param>
    public static IReadOnlyList<(Color Fill, Vector2[] Points)> Ground(ArenaLayout layout, float height = 1080f)
    {
        ArgumentNullException.ThrowIfNull(layout);

        float width = layout.Width;
        float back = layout.BackGroundY;
        float front = layout.FrontGroundY;

        List<(Color, Vector2[])> shapes =
        [
            // The night behind everything, and the hills standing in it. The ridge is one broken line,
            // the same device the town's roofs behind the yard wall are drawn with.
            (Hex(0x0B0907), Rect(0, 0, width, back)),
            (Hex(0x15161D), Ridge(width, back, reach: 150, step: 320, seed: 3)),
            (Hex(0x1B1D26), Ridge(width, back, reach: 84, step: 210, seed: 7)),

            // The ground: the far band the men walk in, and the near apron under the front edge, a
            // shade darker so the near edge of the field is a line the eye can find.
            (Hex(0x2A241C), Rect(0, back, width, front - back)),
            (Hex(0x1E1914), Rect(0, front, width, Mathf.Max(height - front, 120f))),
        ];

        // The rope along the far edge: posts at an even pace with two lines slung between them. It is
        // what says the field is enclosed without drawing a wall the fight would have to respect.
        for (float x = 60; x < width; x += 240)
        {
            shapes.Add((Hex(0x241E18), Rect(x, back - 54, 10, 58)));
        }

        shapes.Add((Hex(0x322A21), Rect(0, back - 46, width, 5)));
        shapes.Add((Hex(0x2B241C), Rect(0, back - 24, width, 4)));

        // Two torches, one at each end of the near edge, out of the men's way. The flame is the only
        // warm thing on the field, which is what makes the two ends of it readable at a glance.
        foreach (float x in (float[])[96f, width - 132f])
        {
            shapes.Add((Hex(0x241E18), Rect(x, front - 232, 12, 240)));
            shapes.Add((Hex(0x7A3527), Points(x - 5, front - 236, x + 17, front - 236, x + 6, front - 274)));
            shapes.Add((Hex(0xB98F4A), Points(x, front - 242, x + 12, front - 242, x + 6, front - 266)));
        }

        // Scuffs in the dirt where the fighting happens — flat lozenges, no two the same length, so the
        // band between the edges does not read as a painted floor.
        float[] scuffs = [0.18f, 0.31f, 0.44f, 0.56f, 0.68f, 0.81f];

        for (int i = 0; i < scuffs.Length; i++)
        {
            float x = width * scuffs[i];
            float y = back + ((front - back) * (0.3f + (0.1f * (i % 4))));
            float length = 70 + (i % 3 * 26);

            shapes.Add((Hex(0x241E18), Points(x, y, x + length, y - 5, x + length + 8, y + 4, x + 8, y + 9)));
        }

        return shapes;
    }

    /// <summary>A ridge line across the back: a zigzag closed off along the bottom.</summary>
    /// <remarks>
    /// The peaks are stepped through a fixed sequence rather than drawn from a generator: the ground is
    /// scenery, and scenery that changes between two runs of the same seed would make a recorded fight
    /// impossible to compare with itself.
    /// </remarks>
    private static Vector2[] Ridge(float width, float baseline, float reach, float step, int seed)
    {
        int peaks = (int)(width / step) + 2;
        List<Vector2> points = [new(0, baseline)];

        for (int i = 0; i < peaks; i++)
        {
            // A fixed, repeating wobble — enough that the ridge is not a row of identical teeth.
            float wobble = ((i * seed) % 5) / 4f;
            points.Add(new Vector2(i * step, baseline - (reach * (0.45f + (wobble * 0.55f)))));
            points.Add(new Vector2((i * step) + (step / 2), baseline - (reach * (0.2f + (wobble * 0.3f)))));
        }

        points.Add(new Vector2(width, baseline));

        return [.. points];
    }

    private static Vector2[] Rect(float x, float y, float width, float height) =>
        [new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)];

    private static Vector2[] Points(params float[] pairs)
    {
        Vector2[] points = new Vector2[pairs.Length / 2];

        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector2(pairs[i * 2], pairs[(i * 2) + 1]);
        }

        return points;
    }

    private static Color Hex(uint rgb) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
}
