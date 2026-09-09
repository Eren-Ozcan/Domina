using Godot;

namespace Domina.Game;

/// <summary>The game's first screen: a new game, or carry on from where you left off.</summary>
/// <remarks>
/// <para>
/// The screen <b>takes no dojo</b>: where the dojo comes from is decided here, which is why it does not
/// share the same skeleton as the other screens.
/// </para>
/// <para>
/// "New game" <b>asks for confirmation</b> when a save exists: there is a single slot and a permadeath run
/// must not be erased by the wrong key.
/// </para>
/// </remarks>
public sealed partial class TitleScreen : CanvasLayer
{
    private Label _note = null!;
    private Button _newGame = null!;
    private bool _confirming;

    /// <summary>Load the save.</summary>
    public Action? Continued { get; set; }

    /// <summary>Start a new expedition — over the old save if there is one.</summary>
    public Action? Started { get; set; }

    /// <summary>The warning shown at the bottom of the screen; written if the load was incomplete.</summary>
    public string? Warning { get; set; }

    public override void _Ready()
    {
        ColorRect backdrop = new()
        {
            Color = new Color(0.09f, 0.09f, 0.11f),
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        AddChild(backdrop);

        CenterContainer center = new() { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);

        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 16);
        center.AddChild(column);

        Label title = new()
        {
            Text = "DOMINA",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 48);
        column.AddChild(title);

        bool saved = SaveSlot.Exists();

        Button resume = new()
        {
            Text = "Continue where you left off",
            Disabled = !saved,
        };
        resume.Pressed += () => Continued?.Invoke();
        column.AddChild(resume);

        _newGame = new Button { Text = saved ? "New game (your save is erased)" : "New game" };
        _newGame.Pressed += () => StartPressed(saved);
        column.AddChild(_newGame);

        _note = new Label
        {
            Text = Warning ?? (saved ? string.Empty : "No saved expedition."),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(420, 0),
        };
        _note.AddThemeColorOverride("font_color", new Color(0.78f, 0.70f, 0.32f));
        column.AddChild(_note);
    }

    private void StartPressed(bool saved)
    {
        if (saved && !_confirming)
        {
            _confirming = true;
            _newGame.Text = "Press again if you are sure";
            _note.Text = "A new game erases the saved expedition; with permadeath there is no way back.";
            return;
        }

        Started?.Invoke();
    }
}
