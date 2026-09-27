using Godot;

namespace Urman.Godot;

/// <summary>
/// The old PC's tetris (03_oldpc_full_system.md §7): a plain easter egg. It
/// owns a 10x20 well, seven pieces, levels and a pause, keeps no reference to
/// the runtime bridge and reports exactly one number - the record - through a
/// callback. Nothing here can reach knowledge, the journal, hints or the search,
/// and the middle of a game is never part of a save.
/// </summary>
public partial class OldPcTetris : Control
{
    public const int Columns = 10;
    public const int Rows = 20;
    public const int MaximumRecord = 999999;

    private static readonly Vector2I[][] Pieces =
    [
        [new(0, 1), new(1, 1), new(2, 1), new(3, 1)],                                  // I
        [new(0, 0), new(0, 1), new(1, 1), new(2, 1)],                                  // J
        [new(2, 0), new(0, 1), new(1, 1), new(2, 1)],                                  // L
        [new(1, 0), new(2, 0), new(1, 1), new(2, 1)],                                  // O
        [new(1, 0), new(0, 1), new(1, 1), new(2, 1)],                                  // T
        [new(1, 0), new(2, 0), new(0, 1), new(1, 1)],                                  // S
        [new(0, 0), new(1, 0), new(1, 1), new(2, 1)]                                   // Z
    ];

    private static readonly Color Empty = new("11161d");
    private static readonly Color Grid = new("2b3a49");
    private static readonly Color Ink = new("d7e3d5");
    private static readonly Color[] Tints =
    [
        new("7fc4c9"), new("8fb3d9"), new("d9b183"), new("e0d78f"),
        new("b79ada"), new("96c98d"), new("d99a9a")
    ];

    private readonly int[,] _well = new int[Columns, Rows];
    private readonly RandomNumberGenerator _random = new();
    private Vector2I[] _cells = [];
    private int _piece;
    private Vector2I _origin;
    private float _dropTimer;
    private bool _paused;
    private bool _over;

    public int Score { get; private set; }
    public int Lines { get; private set; }
    public int LandedPieces { get; private set; }
    public int FilledCells
    {
        get
        {
            var filled = 0;
            for (var x = 0; x < Columns; x++)
            for (var y = 0; y < Rows; y++) filled += _well[x, y] == 0 ? 0 : 1;
            return filled;
        }
    }
    public int Level { get; private set; } = 1;
    public int Record { get; private set; }
    public bool Paused => _paused;
    public bool GameOver => _over;
    public Action<int>? RecordChanged { get; set; }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        _random.Randomize();
        StartGame();
    }

    public void SetRecord(int record)
    {
        Record = Math.Clamp(record, 0, MaximumRecord);
        QueueRedraw();
    }

    public void StartGame() => StartGame(seed: null);

    /// <summary>A fixed seed makes the piece sequence reproducible for the proof.</summary>
    public void StartGame(ulong? seed)
    {
        if (seed is { } fixedSeed) _random.Seed = fixedSeed;
        Array.Clear(_well);
        LandedPieces = 0;
        Score = 0;
        Lines = 0;
        Level = 1;
        _over = false;
        _paused = false;
        _dropTimer = 0;
        Spawn();
        QueueRedraw();
    }

    public void TogglePause()
    {
        if (_over) return;
        _paused = !_paused;
        QueueRedraw();
    }

    public void Pause()
    {
        if (!_over) _paused = true;
        QueueRedraw();
    }

    public void Resume()
    {
        if (!_over && _paused) _paused = false;
        QueueRedraw();
    }

    public void Move(int dx)
    {
        if (!Playable) return;
        if (Fits(_cells, _origin + new Vector2I(dx, 0))) _origin += new Vector2I(dx, 0);
        QueueRedraw();
    }

    public void Rotate()
    {
        if (!Playable) return;
        var rotated = _cells.Select(cell => new Vector2I(3 - cell.Y, cell.X)).ToArray();
        if (Fits(rotated, _origin)) _cells = rotated;
        QueueRedraw();
    }

    public void SoftDrop() => Step(manual: true);

    public void HardDrop()
    {
        if (!Playable) return;
        while (Fits(_cells, _origin + Vector2I.Down)) _origin += Vector2I.Down;
        Land();
    }

    public void Step(float delta) => Step(manual: false, delta);

    private void Step(bool manual, float delta = 0f)
    {
        if (!Playable) return;
        _dropTimer = 0;
        if (Fits(_cells, _origin + Vector2I.Down))
        {
            _origin += Vector2I.Down;
        }
        else if (manual)
        {
            Land();
            return;
        }
        else
        {
            Land();
            return;
        }
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || !Playable) return;
        _dropTimer += (float)delta;
        var interval = Mathf.Max(.08f, .8f - (Level - 1) * .07f);
        if (_dropTimer >= interval) Step(true, 0f);
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!IsVisibleInTree() || !HasFocus()) return;
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key) return;
        switch (key.Keycode)
        {
            case Key.Left: Move(-1); break;
            case Key.Right: Move(1); break;
            case Key.Down: SoftDrop(); break;
            case Key.Up or Key.X: Rotate(); break;
            case Key.Z: RotateBack(); break;
            case Key.Space: HardDrop(); break;
            case Key.P: TogglePause(); break;
            case Key.N when _over: StartGame(); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    public override void _Notification(int what)
    {
        // §7: losing the window or the application focus pauses the game instead
        // of letting pieces fall while nobody watches.
        if (what is (int)NotificationApplicationFocusOut or (int)NotificationWMWindowFocusOut) Pause();
    }

    private void RotateBack()
    {
        if (!Playable) return;
        var rotated = _cells.Select(cell => new Vector2I(cell.Y, 3 - cell.X)).ToArray();
        if (Fits(rotated, _origin)) _cells = rotated;
        QueueRedraw();
    }

    private bool Playable => !_paused && !_over && _cells.Length > 0;

    private void Spawn()
    {
        _piece = _random.RandiRange(0, Pieces.Length - 1);
        _cells = Pieces[_piece].ToArray();
        _origin = new Vector2I(3, 0);
        if (Fits(_cells, _origin)) return;
        _over = true;
        PushRecord();
    }

    private void Land()
    {
        LandedPieces++;
        foreach (var cell in _cells)
        {
            var at = _origin + cell;
            if (at.X is >= 0 and < Columns && at.Y is >= 0 and < Rows) _well[at.X, at.Y] = _piece + 1;
        }
        ClearLines();
        Spawn();
        QueueRedraw();
    }

    private void ClearLines()
    {
        var cleared = 0;
        for (var y = Rows - 1; y >= 0; y--)
        {
            var full = true;
            for (var x = 0; x < Columns && full; x++) full = _well[x, y] != 0;
            if (!full) continue;
            cleared++;
            for (var row = y; row > 0; row--)
            for (var x = 0; x < Columns; x++) _well[x, row] = _well[x, row - 1];
            for (var x = 0; x < Columns; x++) _well[x, 0] = 0;
            y++;
        }
        if (cleared == 0) return;
        Lines += cleared;
        Level = 1 + Lines / 10;
        Score = Math.Min(MaximumRecord, Score + (cleared switch { 1 => 40, 2 => 100, 3 => 300, _ => 1200 }) * Level);
        PushRecord();
    }

    private void PushRecord()
    {
        if (Score <= Record) return;
        Record = Math.Min(MaximumRecord, Score);
        RecordChanged?.Invoke(Record);
    }

    private bool Fits(Vector2I[] cells, Vector2I origin)
    {
        foreach (var cell in cells)
        {
            var at = origin + cell;
            if (at.X < 0 || at.X >= Columns || at.Y >= Rows) return false;
            if (at.Y >= 0 && _well[at.X, at.Y] != 0) return false;
        }
        return true;
    }

    public override void _Draw()
    {
        var size = Size;
        var cell = Mathf.Floor(Mathf.Min(size.X / (Columns + 4f), size.Y / Rows));
        if (cell < 4) return;
        var board = new Vector2((Columns + 4) * cell, Rows * cell);
        var origin = (size - board) * .5f;
        DrawRect(new Rect2(origin, new Vector2(Columns * cell, Rows * cell)), Empty);
        for (var x = 0; x <= Columns; x++)
            DrawLine(origin + new Vector2(x * cell, 0), origin + new Vector2(x * cell, Rows * cell), Grid, 1);
        for (var y = 0; y <= Rows; y++)
            DrawLine(origin + new Vector2(0, y * cell), origin + new Vector2(Columns * cell, y * cell), Grid, 1);
        void Block(int x, int y, Color color)
        {
            var rect = new Rect2(origin + new Vector2(x * cell, y * cell), new Vector2(cell - 1, cell - 1));
            DrawRect(rect, color);
            DrawRect(rect, Ink, false, 1f);
        }
        for (var x = 0; x < Columns; x++)
        for (var y = 0; y < Rows; y++)
            if (_well[x, y] != 0) Block(x, y, Tints[_well[x, y] - 1]);
        if (!_over)
        {
            foreach (var cellOffset in _cells)
            {
                var at = _origin + cellOffset;
                if (at.Y >= 0) Block(at.X, at.Y, Tints[_piece]);
            }
        }
        var hud = origin + new Vector2((Columns + 1) * cell, 0);
        DrawString(ThemeDB.FallbackFont, hud + new Vector2(0, cell * 2), $"Очки\n{Score}", HorizontalAlignment.Left, -1, (int)Mathf.Max(10, cell));
        DrawString(ThemeDB.FallbackFont, hud + new Vector2(0, cell * 6), $"Линии\n{Lines}", HorizontalAlignment.Left, -1, (int)Mathf.Max(10, cell));
        DrawString(ThemeDB.FallbackFont, hud + new Vector2(0, cell * 10), $"Уровень\n{Level}", HorizontalAlignment.Left, -1, (int)Mathf.Max(10, cell));
        DrawString(ThemeDB.FallbackFont, hud + new Vector2(0, cell * 14), $"Рекорд\n{Record}", HorizontalAlignment.Left, -1, (int)Mathf.Max(10, cell));
        if (_paused && !_over) DrawString(ThemeDB.FallbackFont, origin + new Vector2(cell, Rows * cell * .5f), "Пауза", HorizontalAlignment.Left, -1, (int)Mathf.Max(12, cell));
        if (_over) DrawString(ThemeDB.FallbackFont, origin + new Vector2(cell, Rows * cell * .5f), "Игра окончена · N — заново", HorizontalAlignment.Left, -1, (int)Mathf.Max(12, cell));
    }
}
