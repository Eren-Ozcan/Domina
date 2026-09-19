using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>How much of the man a plate shows.</summary>
/// <remarks>
/// One plate class serves the whole game, and what changes from screen to screen is the crop, not the
/// drawing: his own page has room for the whole man, a line in a list has room for a head. Cropping
/// rather than shrinking is what keeps a 26-pixel chip readable — a whole figure at that size is a smudge.
/// </remarks>
public enum PortraitCrop
{
    /// <summary>Head to foot.</summary>
    Full,

    /// <summary>Head to hip — the man behind a counter.</summary>
    Bust,

    /// <summary>The head alone, for a chip beside a name in a list.</summary>
    Head,
}

/// <summary>
/// The man himself, printed on his own page: the arena's rig, standing still on a plate of paper.
/// </summary>
/// <remarks>
/// <para>
/// The reference game's gladiator panel carries a full-body portrait beside the numbers, and ours
/// printed the numbers with nothing to look at (docs/REFERENCE-DOMINA-UI.md §3). The figure here is
/// not a second drawing of the man: it is <see cref="WarriorRig"/>, the same body the arena fights
/// with, posed once through <see cref="WarriorRig.Stand"/>. When the art on the bones changes, the
/// portrait changes with the fight and cannot drift from it.
/// </para>
/// <para>
/// The man it prints is <b>this</b> man: the rig reads his <see cref="WarriorLook"/> off his id and
/// name, so his stature, his hair, his beard and his sash are the ones the arena gives him. A page of
/// six warriors is six different figures, and the one the player sent out is the one he watches fight.
/// </para>
/// <para>
/// It is a <b>portrait, not an animation</b>. Nothing here runs per frame: the rig is built, posed at
/// the clock's zero and left alone, so a sheet open on the yard costs no processing. What the man has
/// lost is already gone from the body — an arm off, a leg off, a bar over a blinded eye — because the
/// limbs line in the panel says the same thing in words and the two must agree.
/// </para>
/// </remarks>
public sealed partial class WarriorPortrait : PanelContainer
{
    /// <summary>The paper left under the feet and over the head, so the figure is not cropped.</summary>
    private const float Margin = 14f;

    private static readonly Color LivingTint = new(0.30f, 0.34f, 0.46f);
    private static readonly Color DeadTint = new(0.52f, 0.50f, 0.46f);

    private readonly PortraitCrop _crop;
    private readonly float _margin;
    private readonly Color _ink;
    private readonly Control _stage;
    private WarriorRig? _rig;

    /// <summary>
    /// Builds the plate. It is done in the constructor and not in <c>_Ready</c> because a page lays
    /// its panels out before it is in the tree, and the man is printed on the plate as it is built.
    /// </summary>
    /// <param name="crop">How much of him this plate has room for.</param>
    /// <param name="size">The plate's own size in pixels.</param>
    /// <param name="framed">Is the plate a sheet of paper, or does the figure sit bare on the page?</param>
    /// <param name="ink">
    /// The colour he is drawn in. The default is the ink a paper sheet takes; a plate laid on the
    /// night (the gateway, the hut) passes a pale one, or the man disappears into the dark.
    /// </param>
    public WarriorPortrait(
        PortraitCrop crop = PortraitCrop.Full,
        Vector2? size = null,
        bool framed = true,
        Color? ink = null)
    {
        _crop = crop;
        _margin = crop == PortraitCrop.Full ? Margin : 4f;
        _ink = ink ?? LivingTint;

        CustomMinimumSize = size ?? Default(crop);
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;

        if (framed)
        {
            AddThemeStyleboxOverride("panel", UiKit.PaperStyle(UiKit.Pressed, shadow: 0, border: UiKit.Edge));
        }

        // The figure is drawn in scene coordinates and the plate is a control, so a stage that clips
        // is what keeps a raised sword or a thrown limb inside the panel rather than over the sheet.
        _stage = new Control { ClipContents = true, SizeFlagsVertical = SizeFlags.ExpandFill };
        _stage.Resized += Place;
        AddChild(_stage);
    }

    /// <summary>Prints this man. Called every time the page changes the man it is showing.</summary>
    /// <param name="row">The row the page is printing — his losses and whether he is alive.</param>
    public void Print(RosterRow row) => Print(row.Id, row.Name ?? string.Empty, row.Lost, row.IsAlive, row.Kit);

    /// <summary>Prints a man a screen knows by hand — an arrival at the gate, a name on the market's board.</summary>
    /// <param name="id">His id; the look is derived from it, so it must be the id he will keep.</param>
    /// <param name="name">His name.</param>
    /// <param name="lost">What he has already lost.</param>
    /// <param name="alive">Is he alive? A dead man is printed in ash.</param>
    /// <param name="kit">
    /// What he wears and carries. A screen that knows only a name passes nothing and gets the bare
    /// figure — which is the honest drawing of a man nobody has equipped yet.
    /// </param>
    public void Print(
        WarriorId id,
        string name,
        BodyPartSet lost = default,
        bool alive = true,
        WarriorKit? kit = null)
    {
        Clear();

        _rig = new WarriorRig();
        _stage.AddChild(_rig);
        _rig.Build(id, name ?? string.Empty, alive ? _ink : DeadTint, facing: 1f, kit);
        _rig.Stand(lost);

        // A dead man is not drawn lying down: the page is a record, and the record is of the man who
        // stood there. He is printed in the ash the rest of the sheet greys the dead with instead.
        _rig.Modulate = alive ? Colors.White : new Color(1f, 1f, 1f, 0.55f);

        Place();
    }

    /// <summary>Empties the plate — the roster has nobody to show.</summary>
    public void Clear()
    {
        if (_rig is null)
        {
            return;
        }

        _stage.RemoveChild(_rig);
        _rig.QueueFree();
        _rig = null;
    }

    /// <summary>The plate size a crop is worth when the screen does not ask for one of its own.</summary>
    private static Vector2 Default(PortraitCrop crop) => crop switch
    {
        PortraitCrop.Head => new Vector2(34, 34),
        PortraitCrop.Bust => new Vector2(96, 104),
        _ => new Vector2(168, 232),
    };

    /// <summary>Stands the figure on the plate, scaled so the crop's band fills the paper.</summary>
    /// <remarks>
    /// The rig's root is at foot level, so a crop is not a second drawing: the band wanted is measured
    /// off <see cref="WarriorRig.StandingHeight"/> and <see cref="WarriorRig.HipLine"/>, the root is
    /// pushed below the plate by whatever is being cut off, and the stage's clipping does the cutting.
    /// </remarks>
    private void Place()
    {
        if (_rig is null)
        {
            return;
        }

        Vector2 size = _stage.Size;
        if (size.Y <= 0)
        {
            return;
        }

        float standing = _rig.StandingHeight;
        float hip = _rig.HipLine;

        // band: what must fit on the paper. below: what is cut off under it.
        (float band, float below) = _crop switch
        {
            PortraitCrop.Head => (standing - _rig.ShoulderLine, _rig.ShoulderLine),
            PortraitCrop.Bust => (standing - hip, hip),
            _ => (standing, 0f),
        };

        float scale = Mathf.Min((size.Y - (_margin * 2)) / Mathf.Max(band, 1f), 1.4f);
        _rig.Position = new Vector2(size.X / 2, size.Y - _margin + (below * scale));

        // The root's own scale carries the facing (-1 on the far side), so the fit multiplies it
        // rather than replacing it — assigning the scale outright would turn the man around.
        _rig.Scale = new Vector2(scale * Mathf.Sign(_rig.Scale.X), scale);
    }
}
