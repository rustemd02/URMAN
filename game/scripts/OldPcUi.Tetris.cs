using Godot;

namespace Urman.Godot;

// The tetris window (§7): the game owns its own control, and the desktop only
// hands it the stored record and keeps the one number it returns. The game
// never sees the runtime bridge, so it cannot reach knowledge at all.
public partial class OldPcUi
{
    private OldPcTetris _tetris = null!;

    public OldPcTetris Tetris => _tetris;

    // The desktop state is replaced on every restore, so the window re-reads the
    // only field the game shares with it instead of keeping a stale record.
    private void RestoreTetrisRecord() => _tetris?.SetRecord(_desktop.TetrisHigh);

    private void BuildTetris()
    {
        var body = CreateDesktopWindow("tetris", "Тетрис", out _);
        _tetris = new OldPcTetris
        {
            Name = "Game",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(420, 540)
        };
        _tetris.SetRecord(_desktop.TetrisHigh);
        _tetris.RecordChanged += record =>
        {
            _desktop.TetrisHigh = record;
            MarkDesktopChanged();
        };
        body.AddChild(_tetris);
        var row = new HBoxContainer { Name = "Keys" };
        body.AddChild(row);
        foreach (var (label, action) in new (string, Action)[]
                 {
                     ("←", () => _tetris.Move(-1)), ("→", () => _tetris.Move(1)), ("↓", () => _tetris.SoftDrop()),
                     ("↻", () => _tetris.Rotate()), ("⤓", () => _tetris.HardDrop()), ("Пауза [P]", () => _tetris.TogglePause())
                 })
            DesktopButton(row, "Key_" + label, label, action);
    }
}
