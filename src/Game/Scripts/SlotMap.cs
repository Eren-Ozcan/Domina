using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The six parts of a man, drawn as a figure: what is covered, and how far through its life the
/// piece on it is.
/// </summary>
/// <remarks>
/// <para>
/// The counter already lists the six regions as rows, and a list answers "what is on his head".
/// It does not answer "where is he bare", which is the question a player actually asks before
/// sending him out — the reference game's armour screen has the same hole, and its own gear cards
/// are read one at a time for the same reason (docs/REFERENCE-DOMINA-UI.md §3).
/// </para>
/// <para>
/// It draws and nothing else: the regions, the wear and the names come in from
/// <see cref="QuartermasterModel"/>, so the map cannot disagree with the rows beside it.
/// </para>
/// </remarks>
public sealed partial class SlotMap : Control
{
    private const int CellWidth = 58;
    private const int CellHeight = 34;
    private const int Gap = 6;

    private IReadOnlyList<ArmorSlotRow> _slots = [];

    /// <summary>The six regions, as the counter reads them.</summary>
    public IReadOnlyList<ArmorSlotRow> Slots
    {
        get => _slots;
        set
        {
            _slots = value ?? [];
            QueueRedraw();
        }
    }

    /// <inheritdoc/>
    public override void _Ready() =>
        CustomMinimumSize = new Vector2(
            (CellWidth * 3) + (Gap * 2),
            (CellHeight * 4) + (Gap * 3));

    /// <inheritdoc/>
    public override void _Draw()
    {
        foreach (ArmorSlotRow slot in _slots)
        {
            DrawPart(slot);
        }
    }

    /// <summary>Where on the figure a region sits: column, row, and how many columns wide it is.</summary>
    private static (int Column, int Row, int Width) Place(HitLocation slot) => slot switch
    {
        HitLocation.Head => (1, 0, 1),
        HitLocation.SwordArm => (0, 1, 1),
        HitLocation.Torso => (1, 1, 1),
        HitLocation.OffArm => (2, 1, 1),
        HitLocation.RightLeg => (0, 2, 1),
        _ => (2, 2, 1),
    };

    /// <summary>The short word printed in a part; the rows beside the map carry the full name.</summary>
    private static string PartName(HitLocation slot) => slot switch
    {
        HitLocation.Head => "head",
        HitLocation.Torso => "torso",
        HitLocation.SwordArm => "sword",
        HitLocation.OffArm => "off",
        HitLocation.RightLeg => "right",
        _ => "left",
    };

    private void DrawPart(ArmorSlotRow slot)
    {
        (int column, int row, int width) = Place(slot.Slot);
        Rect2 box = new(
            column * (CellWidth + Gap),
            row * (CellHeight + Gap),
            (width * CellWidth) + ((width - 1) * Gap),
            CellHeight);

        bool bare = slot.Worn.Durability <= 0;

        // Bare is paper pressed flat and nothing else — the same shape the rest of the game uses for
        // a thing that is not there. A covered part is filled by what is left of the piece.
        DrawRect(box, bare ? UiKit.Pressed : UiKit.Raised);

        if (!bare)
        {
            double left = 1 - slot.WornShare;
            Rect2 fill = new(box.Position, new Vector2(box.Size.X * (float)left, box.Size.Y));
            DrawRect(fill, Life(left));
        }

        DrawRect(box, bare ? UiKit.Warning : UiKit.Edge, filled: false, width: 1);

        if (UiKit.BodyFace is not Font face)
        {
            return;
        }

        DrawString(
            face,
            box.Position + new Vector2(6, (CellHeight / 2f) + 5),
            PartName(slot.Slot),
            HorizontalAlignment.Left,
            box.Size.X - 12,
            UiKit.NoteSize - 3,
            bare ? UiKit.Warning : UiKit.Ink);
    }

    /// <summary>The colour of what is left of a piece: fresh, worn, and about to go.</summary>
    private static Color Life(double left) => left switch
    {
        >= 0.66 => new Color(UiKit.Good, 0.55f),
        >= 0.33 => new Color(UiKit.Pending, 0.55f),
        _ => new Color(UiKit.Warning, 0.45f),
    };
}
