using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The sheet of paper everything else is printed on: grain, an ink-dark edge, and the hour's wash.
/// </summary>
/// <remarks>
/// <para>
/// GDD §12 decides the look — dark Edo woodblock over layered paper theatre — and the asset rule that
/// comes with it is that <b>nothing is drawn with its texture baked in</b>: a part grained in its own
/// file is wrong the moment it rotates, and every part would then have to be drawn twice. So the grain
/// is laid over the whole screen once, from here, and every sheet, every man and every chalk name gets
/// it for free (ROADMAP phase 2.2).
/// </para>
/// <para>
/// It is <b>two layers, not one</b>, and the split is the design's own rule:
/// </para>
/// <list type="bullet">
/// <item><description>the <b>wash</b> — the colour of the hour — sits on layer <see cref="WashLayer"/>,
/// over the yard and <b>under</b> every sheet. The world has weather; the page does not, and nothing
/// the player has to read is ever tinted by the time of day (design canvas → 6d);</description></item>
/// <item><description>the <b>grain and the bleed</b> sit on <see cref="PaperLayer"/>, over everything,
/// because they are the paper itself and the paper is what the sheets are printed on
/// too.</description></item>
/// </list>
/// <para>
/// The wash is the only part that moves, and it moves with the day rather than on its own, so
/// <see cref="GameSettings.ReducedMotion"/> does not have to switch it off. The whole thing is switched
/// off by <see cref="GameSettings.PaperGrain"/>, for a screen the grain does not suit.
/// </para>
/// </remarks>
public sealed partial class PaperOverlay : Node
{
    /// <summary>Over the yard (which is below it) and under the sheets (which are above it).</summary>
    private const int WashLayer = 0;

    /// <summary>Over everything the game draws, and under nothing.</summary>
    private const int PaperLayer = 128;

    /// <summary>The size of the noise field before it is tiled.</summary>
    private const int GrainTile = 256;

    /// <summary>How much of the grain reaches the picture.</summary>
    private const float GrainStrength = 0.18f;

    /// <summary>How dark the page goes at its very corner.</summary>
    private const float BleedStrength = 0.38f;

    private ColorRect _wash = null!;

    /// <summary>The day's progress, 0 to 1, as the hub reads it off the clock.</summary>
    public double Hour { get; set; }

    public override void _Ready()
    {
        CanvasLayer wash = new() { Layer = WashLayer };
        AddChild(wash);
        _wash = Full(new ColorRect());
        wash.AddChild(_wash);

        CanvasLayer paper = new() { Layer = PaperLayer };
        AddChild(paper);
        paper.AddChild(Grain());
        paper.AddChild(Bleed());

        Paint();
    }

    public override void _Process(double delta) => Paint();

    /// <summary>Lays the hour's wash down. Called every frame, so it touches one colour and nothing else.</summary>
    private void Paint()
    {
        if (!IsInstanceValid(_wash))
        {
            return;
        }

        Wash wash = DayTint.At(Hour);
        _wash.Color = new Color(wash.Red, wash.Green, wash.Blue, wash.Strength);
    }

    /// <summary>The noise field, tiled over the screen.</summary>
    /// <remarks>
    /// The grain is carried in the texture's <b>alpha</b> rather than laid on as a grey sheet: where
    /// the field is high the paper takes a dark speck, and everywhere else the texture is not there at
    /// all. A grey sheet would have to be blended, and any blend that can dim the whole picture is one
    /// bad number away from a black screen.
    /// </remarks>
    private static Control Grain()
    {
        FastNoiseLite noise = new()
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = 0.32f,
            FractalOctaves = 2,
        };

        Gradient speck = new()
        {
            Colors = [new Color(0.06f, 0.05f, 0.04f, 0f), new Color(0.06f, 0.05f, 0.04f, 1f)],
            Offsets = [0.40f, 1f],
        };

        NoiseTexture2D texture = new()
        {
            Noise = noise,
            ColorRamp = speck,
            Width = GrainTile,
            Height = GrainTile,
            Seamless = true,
            GenerateMipmaps = false,
        };

        TextureRect grain = Full(new TextureRect
        {
            Texture = texture,
            StretchMode = TextureRect.StretchModeEnum.Tile,
            TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
        });

        grain.Modulate = new Color(1, 1, 1, GrainStrength);
        return grain;
    }

    /// <summary>The ink gathering at the edge of the page.</summary>
    private static Control Bleed()
    {
        Gradient ramp = new()
        {
            Colors = [new Color(0, 0, 0, 0), new Color(0, 0, 0, BleedStrength)],
            Offsets = [0.58f, 1f],
        };

        GradientTexture2D texture = new()
        {
            Gradient = ramp,
            Width = 512,
            Height = 512,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };

        return Full(new TextureRect
        {
            Texture = texture,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        });
    }

    /// <summary>The whole screen, and no part of the mouse's business.</summary>
    private static T Full<T>(T control)
        where T : Control
    {
        control.AnchorRight = 1;
        control.AnchorBottom = 1;
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
        return control;
    }
}
