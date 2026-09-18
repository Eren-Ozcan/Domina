using Domina.Core.Campaign;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// Opening a school: the name, the instructor, the province and the seed.
/// </summary>
/// <remarks>
/// <para>
/// Every choice on this sheet has its consequence written beside it, and none of them is called easy or
/// hard: a province is quiet, as it is written, or a bad year, and the sheet says in the term's own
/// units what each one does (design canvas → 6b).
/// </para>
/// </remarks>
public sealed partial class NewTermScreen : CanvasLayer
{
    private ulong _seed = unchecked((ulong)Random.Shared.NextInt64());
    private DifficultyTier _tier = DifficultyTier.Master;
    private LineEdit _name = null!;
    private LineEdit _instructor = null!;
    private VBoxContainer _provinces = null!;
    private Label _seedLabel = null!;

    /// <summary>The term is opened: the two names the player wrote, the province and the seed.</summary>
    public Action<string, string, DifficultyTier, ulong>? Opened { get; set; }

    /// <summary>Back to the title, with nothing written.</summary>
    public Action? Closed { get; set; }

    public override void _Ready()
    {
        Control page = new() { AnchorRight = 1, AnchorBottom = 1, Theme = UiKit.Theme };
        AddChild(page);

        page.AddChild(new ColorRect { Color = UiKit.Night, AnchorRight = 1, AnchorBottom = 1 });

        Button back = new();

        if (Closed is Action closed)
        {
            back.Pressed += closed;
        }

        back.Text = "back to the title ✕";

        VBoxContainer sheet = UiKit.Sheet(
            page,
            "Open a school",
            back,
            "A term runs its whole length and cannot be restarted near the end of it.");

        VBoxContainer left = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        left.AddThemeConstantOverride("separation", 14);
        sheet.AddChild(left);

        _provinces = UiKit.Section(left, "How hard the province is");
        BuildProvinces();

        BuildNames(left);

        Button open = new() { Text = "Open the gates on day 1" };
        open.Pressed += () => Opened?.Invoke(DojoName, Instructor, _tier, _seed);
        sheet.AddChild(Foot(UiKit.Act(open), "one term, one save, and the ledger starts empty"));
    }

    /// <summary>The three provinces as three cards; the one chosen is printed on ink.</summary>
    private void BuildProvinces()
    {
        foreach (Node child in _provinces.GetChildren())
        {
            if (child is not Control head || head.GetIndex() > 0)
            {
                _provinces.RemoveChild(child);
                child.QueueFree();
            }
        }

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 8);
        _provinces.AddChild(row);

        foreach (ProvinceChoice choice in NewTermModel.Provinces)
        {
            bool chosen = choice.Tier == _tier;

            Button button = new()
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 150),
            };

            DifficultyTier tier = choice.Tier;
            button.Pressed += () =>
            {
                _tier = tier;
                BuildProvinces();
            };

            // The card is a button with the card laid over it, so the card brings its own margin: a
            // control anchored to the button's full rect would otherwise print flush against its edge.
            MarginContainer pad = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
            pad.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            pad.AddThemeConstantOverride("margin_left", 13);
            pad.AddThemeConstantOverride("margin_right", 13);
            pad.AddThemeConstantOverride("margin_top", 11);
            pad.AddThemeConstantOverride("margin_bottom", 11);
            button.AddChild(pad);

            VBoxContainer inside = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
            inside.AddThemeConstantOverride("separation", 3);
            pad.AddChild(inside);
            inside.AddChild(chosen
                ? UiKit.OnNight(choice.Name, UiKit.PaperInk, UiKit.HeadSize - 3, display: true)
                : UiKit.OnPaper(choice.Name, UiKit.Ink, UiKit.HeadSize - 3, display: true));
            inside.AddChild(UiKit.Body(choice.Cost, chosen ? UiKit.NightMuted : UiKit.Muted, UiKit.NoteSize));

            if (chosen)
            {
                button.AddThemeStyleboxOverride("normal", UiKit.FlatStyle(UiKit.Ink));
                button.AddThemeStyleboxOverride("hover", UiKit.FlatStyle(UiKit.Ink));
                button.AddThemeStyleboxOverride("pressed", UiKit.FlatStyle(UiKit.Ink));
            }
            else
            {
                button.AddThemeStyleboxOverride("normal", UiKit.FlatStyle(UiKit.Pressed));
                button.AddThemeStyleboxOverride("hover", UiKit.FlatStyle(UiKit.Surface));
                button.AddThemeStyleboxOverride("pressed", UiKit.FlatStyle(UiKit.Pressed));
            }

            row.AddChild(button);
        }
    }

    /// <summary>The names: what the school is called, and the seed it is drawn from.</summary>
    private void BuildNames(Control parent)
    {
        VBoxContainer section = UiKit.Section(parent, "Names");

        section.AddChild(Field("The dojo", "the province will call it this in every letter"));

        _name = new LineEdit
        {
            Text = "The Ashigara dojo",
            PlaceholderText = "the dojo",
        };
        _name.AddThemeFontSizeOverride("font_size", UiKit.HeadSize);
        _name.AddThemeColorOverride("font_color", UiKit.Ink);
        _name.AddThemeStyleboxOverride("normal", UiKit.FlatStyle(UiKit.Pressed));
        _name.AddThemeStyleboxOverride("focus", UiKit.FlatStyle(UiKit.Pressed, UiKit.Ink));
        section.AddChild(_name);

        section.AddChild(Field("The instructor", "the province writes to him by name when the term closes"));

        _instructor = new LineEdit
        {
            Text = "Master Ashigara",
            PlaceholderText = "the instructor",
        };
        _instructor.AddThemeFontSizeOverride("font_size", UiKit.HeadSize);
        _instructor.AddThemeColorOverride("font_color", UiKit.Ink);
        _instructor.AddThemeStyleboxOverride("normal", UiKit.FlatStyle(UiKit.Pressed));
        _instructor.AddThemeStyleboxOverride("focus", UiKit.FlatStyle(UiKit.Pressed, UiKit.Ink));
        section.AddChild(_instructor);

        section.AddChild(Field("The seed", "the same seed gives the same province and the same men"));

        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 12);
        section.AddChild(row);

        PanelContainer written = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        written.AddThemeStyleboxOverride("panel", UiKit.FlatStyle(UiKit.Pressed));
        _seedLabel = UiKit.OnPaper(NewTermModel.Spell(_seed), UiKit.Ink, UiKit.HeadSize, display: true);
        UiKit.Padded(written, 12, 10).AddChild(_seedLabel);
        row.AddChild(written);

        Button again = new() { Text = "Draw another" };
        again.Pressed += () =>
        {
            _seed = unchecked((ulong)Random.Shared.NextInt64());
            _seedLabel.Text = NewTermModel.Spell(_seed);
        };
        row.AddChild(UiKit.WayOut(again));
    }

    /// <summary>A field's own two lines: what it is, and what writing in it does.</summary>
    private static Control Field(string what, string does)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 12);

        Label label = UiKit.Body(what, UiKit.Muted, UiKit.BodySize, wrap: false);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);
        row.AddChild(UiKit.Note(does, wrap: false));
        return row;
    }

    /// <summary>The one act, along the foot, with what it costs under it.</summary>
    private static Control Foot(Button act, string clause)
    {
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 3);
        act.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        column.AddChild(act);

        Label note = UiKit.Note(clause, wrap: false);
        note.HorizontalAlignment = HorizontalAlignment.Right;
        column.AddChild(note);
        return column;
    }

    /// <summary>The seed the sheet is holding, for the hub to write into the term.</summary>
    public ulong Seed => _seed;

    /// <summary>The seed as it is printed, so a player can write it down.</summary>
    public string SpelledSeed => NewTermModel.Spell(_seed);

    /// <summary>What the school will be called; never empty.</summary>
    public string DojoName =>
        string.IsNullOrWhiteSpace(_name.Text)
            ? _name.PlaceholderText
            : _name.Text.Trim();

    /// <summary>What the instructor will be called; never empty.</summary>
    public string Instructor =>
        string.IsNullOrWhiteSpace(_instructor.Text)
            ? _instructor.PlaceholderText
            : _instructor.Text.Trim();

    /// <summary>Which province was chosen.</summary>
    public DifficultyTier Province => _tier;
}
