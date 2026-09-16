using System.Globalization;
using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Godot;

namespace Domina.Game;

/// <summary>
/// The terms kept: three slots, what each of them is holding, and an overwrite shaped unlike a load.
/// </summary>
/// <remarks>
/// <para>
/// A term writes itself into its slot at every dawn, so there is no save button here and nothing to
/// confirm about keeping one. What the sheet is for is the two things that need a choice: which kept
/// term to go back into, and — when all three slots are full — which one a new term is written over
/// (design canvas → 6c).
/// </para>
/// <para>
/// Loading is paper and writing over is cut, because one of them can be undone by loading the other
/// and the other cannot be undone at all.
/// </para>
/// </remarks>
public sealed partial class SavesScreen : CanvasLayer
{
    /// <summary>Why the sheet was opened: to go back into a term, or to make room for a new one.</summary>
    public required bool ChoosingRoom { get; init; }

    /// <summary>A kept term was chosen to be played.</summary>
    public Action<int>? Loaded { get; set; }

    /// <summary>A slot was freed for a new term.</summary>
    public Action<int>? Freed { get; set; }

    /// <summary>Back to the title.</summary>
    public Action? Closed { get; set; }

    private VBoxContainer _rows = null!;
    private Control _page = null!;

    public override void _Ready()
    {
        _page = new Control { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        AddChild(_page);
        _page.AddChild(new ColorRect { Color = UiKit.Night, AnchorRight = 1, AnchorBottom = 1 });

        Button back = new();

        if (Closed is Action closed)
        {
            back.Pressed += closed;
        }

        back.Text = "back to the title ✕";

        VBoxContainer sheet = UiKit.Sheet(
            _page,
            "Terms kept",
            back,
            ChoosingRoom
                ? "Every slot holds a term. A new one has to be written over one of them."
                : "A term writes itself into its slot at every dawn.");

        _rows = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 12);
        sheet.AddChild(_rows);

        Build();
    }

    /// <summary>Prints the three slots as they stand.</summary>
    private void Build()
    {
        foreach (Node child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        for (int slot = 1; slot <= SaveSlot.Slots; slot++)
        {
            _rows.AddChild(SaveSlot.Exists(slot) ? Kept(slot) : Empty(slot));
        }
    }

    /// <summary>A slot with a term in it.</summary>
    private Control Kept(int slot)
    {
        LoadResult read = SaveSlot.Peek(slot);

        PanelContainer card = new();
        card.AddThemeStyleboxOverride("panel", UiKit.PaperStyle(UiKit.Raised, shadow: 6));

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 18);
        UiKit.Padded(card, 16, 14).AddChild(row);

        VBoxContainer said = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        said.AddThemeConstantOverride("separation", 5);
        row.AddChild(said);

        if (read.State is not DojoState kept)
        {
            said.AddChild(UiKit.OnPaper($"Slot {Figure(slot)}", UiKit.Ink, UiKit.HeadSize + 4, display: true));
            said.AddChild(UiKit.Body(
                "The file is there and cannot be read. Writing a new term over it is the only thing "
                + "left to do with it.",
                UiKit.Brick,
                UiKit.NoteSize));
            row.AddChild(Over(slot, "the file is unreadable"));
            return card;
        }

        said.AddChild(UiKit.OnPaper(kept.Name, UiKit.Ink, UiKit.HeadSize + 4, display: true));

        HBoxContainer figures = new();
        figures.AddThemeConstantOverride("separation", 26);
        said.AddChild(figures);

        figures.AddChild(UiKit.OnPaper(
            $"Day {Figure(kept.Day)} of {Figure(kept.Season.Tuning.Days)}",
            UiKit.Ink,
            UiKit.HeadSize,
            display: true));
        figures.AddChild(UiKit.Body(
            $"{Figure(kept.Roster.Living.Count())} on the roster",
            UiKit.Ink,
            UiKit.BodySize,
            wrap: false));
        figures.AddChild(UiKit.Body($"{Figure(kept.Resources.Gold)} koku", UiKit.Ink, UiKit.BodySize, wrap: false));
        figures.AddChild(UiKit.Body(
            $"{Figure(kept.Season.HeadsTaken)} of {Figure(kept.Season.Tuning.BountyGate)} heads",
            UiKit.Ink,
            UiKit.BodySize,
            wrap: false));

        said.AddChild(UiKit.Note($"written at dawn on day {Figure(kept.Day)} · slot {Figure(slot)}"));

        if (ChoosingRoom)
        {
            row.AddChild(Over(slot, Cost(kept)));
            return card;
        }

        Button load = new() { Text = "Continue this term" };
        int chosen = slot;
        load.Pressed += () => Loaded?.Invoke(chosen);
        row.AddChild(UiKit.Act(load));
        return card;
    }

    /// <summary>A slot with nothing in it.</summary>
    private Control Empty(int slot)
    {
        PanelContainer card = new();
        card.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Pressed, UiKit.Edge, 2));

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 14);
        UiKit.Padded(card, 16, 20).AddChild(row);

        Label named = UiKit.OnPaper($"Slot {Figure(slot)}, empty", UiKit.Muted, UiKit.HeadSize + 2, display: true);
        named.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(named);
        row.AddChild(UiKit.Note("a new term writes itself here without asking", wrap: false));

        if (ChoosingRoom)
        {
            Button take = new() { Text = "Open the term here" };
            int chosen = slot;
            take.Pressed += () => Freed?.Invoke(chosen);
            row.AddChild(UiKit.Act(take));
        }

        return card;
    }

    /// <summary>The cut act: writing a new term over a kept one.</summary>
    private Control Over(int slot, string cost)
    {
        Button over = new() { Text = "Write over it" };
        int chosen = slot;
        over.Pressed += () => Confirm(chosen, cost);
        return UiKit.Cut(over);
    }

    /// <summary>
    /// The confirm: what is lost, in the terms of the thing lost, and never "are you sure".
    /// </summary>
    private void Confirm(int slot, string cost)
    {
        Control veil = new() { AnchorRight = 1, AnchorBottom = 1 };
        veil.AddChild(UiKit.Dim(0.6f));
        _page.AddChild(veil);

        CenterContainer centre = new() { AnchorRight = 1, AnchorBottom = 1 };
        veil.AddChild(centre);

        PanelContainer sheet = new() { CustomMinimumSize = new Vector2(980, 0) };
        sheet.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Ink));
        centre.AddChild(sheet);

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 18);
        UiKit.Padded(sheet, 22, 20).AddChild(row);

        VBoxContainer said = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        said.AddThemeConstantOverride("separation", 4);
        said.AddChild(UiKit.OnNight(
            $"Writing over slot {Figure(slot)}",
            UiKit.PaperInk,
            UiKit.HeadSize + 2,
            display: true));
        said.AddChild(UiKit.Body(cost, UiKit.NightMuted, UiKit.NoteSize + 1));
        row.AddChild(said);

        Button over = new() { Text = "Write over it" };
        over.Pressed += () =>
        {
            SaveSlot.Delete(slot);
            veil.QueueFree();
            Freed?.Invoke(slot);
        };
        row.AddChild(UiKit.Cut(over));

        Button keep = new() { Text = "Keep it" };
        keep.Pressed += () => veil.QueueFree();
        row.AddChild(UiKit.WayOut(keep));
    }

    /// <summary>What a kept term is worth, said in what it is made of.</summary>
    private static string Cost(DojoState kept) =>
        $"{Figure(kept.Day)} days, {Figure(kept.Roster.Living.Count())} men and the seed are gone for "
        + "good. It cannot be got back.";

    private static string Figure(int value) => value.ToString(CultureInfo.InvariantCulture);
}
