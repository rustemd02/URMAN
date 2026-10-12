using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Scenes;

namespace Urman.Studio.App;

/// <summary>
/// A cutscene as an action list and a timeline of the same actions (spec
/// CINE01–CINE05): selecting in one selects in the other; reordering changes
/// the play order; scrubbing shows the shot and caption of the moment in the
/// Studio's village without granting anything. Walks whose length depends on
/// the ground are drawn as estimates. Camera shots can be taken from the
/// Studio's current view.
/// </summary>
public partial class StudioCutscenePanel : VBoxContainer
{
    public const string ScenePath = "game/content/cutscenes/tamara_fence.cutscene.v1.json";
    private const float PixelsPerSecond = 46f;
    private readonly StudioRoot _studio;
    private readonly ItemList _list = new() { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(360, 0) };
    private readonly LineEdit _search = new() { PlaceholderText = "Найти действие по тексту или ID…", ClearButtonEnabled = true };
    private readonly Label _count = new() { ThemeTypeVariation = "MutedLabel" };
    private readonly Label _sourceStatus = new() { Visible = false, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Control _tracks = new() { CustomMinimumSize = new Vector2(0, 250), MouseFilter = MouseFilterEnum.Stop };
    private readonly HSlider _scrub = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, Step = .05 };
    private readonly Label _frame = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _moveEarlier;
    private readonly Button _moveLater;
    private readonly Button _insertPause;
    private readonly Button _addShot;
    private readonly Button _deleteAction;
    private readonly Button _showInWorld;
    private IReadOnlyList<TimelineClip> _clips = [];
    private IReadOnlyList<TimelineClip> _visibleClips = [];
    private int _selected = -1;
    private string? _selectedId;
    private bool _sourceReady;
    private ScrollContainer _scroll = null!;

    public StudioCutscenePanel(StudioRoot studio)
    {
        _studio = studio;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        var bar = new HBoxContainer();
        bar.AddChild(new Label { Text = "Сцена «Забор Тамары: авария»", ThemeTypeVariation = "HeaderLabel", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _moveEarlier = StudioRoot.Button(bar, "▲", () => MoveSelected(-1));
        _moveEarlier.TooltipText = "Раньше в сцене (меняет порядок игры)";
        _moveLater = StudioRoot.Button(bar, "▼", () => MoveSelected(1));
        _moveLater.TooltipText = "Позже в сцене";
        _insertPause = StudioRoot.Button(bar, "+ Пауза", () => Insert("wait", new JsonObject { ["seconds"] = 1.0 }, "Пауза"));
        _addShot = StudioRoot.Button(bar, "+ Кадр из текущего вида", AddShotFromView);
        _deleteAction = StudioRoot.Button(bar, "Удалить действие", DeleteSelected);
        AddChild(bar);
        AddChild(_sourceStatus);
        AddChild(new Label { Text = "Постоянный итог аварии (забор сломан, квест начат, контрольная точка) фиксируется до начала сцены одной транзакцией, поэтому пропуск ничего не повторяет и не теряет. Перемотка здесь — только просмотр.", AutowrapMode = TextServer.AutowrapMode.WordSmart, ThemeTypeVariation = "MutedLabel" });
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        var left = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddChild(_search);
        left.AddChild(_count);
        left.AddChild(_list);
        row.AddChild(left);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var scroll = _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(_tracks);
        right.AddChild(scroll);
        var scrubRow = new HBoxContainer();
        scrubRow.AddChild(new Label { Text = "Время:" });
        scrubRow.AddChild(_scrub);
        right.AddChild(scrubRow);
        right.AddChild(_frame);
        _showInWorld = StudioRoot.Button(right, "Показать этот кадр в мире", ShowInWorld);
        row.AddChild(right);
        AddChild(row);
        _list.ItemSelected += index => SelectVisible((int)index);
        _search.TextChanged += _ => FillList();
        _tracks.Draw += DrawTracks;
        _tracks.GuiInput += OnTracksInput;
        _scrub.ValueChanged += _ => UpdateFrame();
        Refresh();
    }

    private JsonArray Actions()
    {
        var file = _studio.Workspace.File(ScenePath);
        var actions = new JsonArray();
        foreach (var id in file.Keys())
        {
            if (file.Get(id) is not JsonObject entity)
                throw new InvalidDataException($"Запись «{id}» должна быть объектом действия.");
            if (!string.Equals((string?)entity["id"], id, StringComparison.Ordinal))
                throw new InvalidDataException($"ID записи «{id}» не совпадает с ключом исходника.");
            if (entity["params"] is not JsonObject parameters || string.IsNullOrWhiteSpace((string?)parameters["action"]))
                throw new InvalidDataException($"У действия «{id}» отсутствует объект params или его поле action.");
            actions.Add(entity);
        }

        return actions;
    }

    private IEnumerable<string> Ids() => _studio.Workspace.File(ScenePath).Keys();

    private string Text(string key) => _studio.Catalog.ResolveText("urman.chapter1:text/" + key);

    public void Refresh()
    {
        if (!_studio.Workspace.HasFile(ScenePath))
        {
            ShowSourceError($"Исходник сцены не найден: {ScenePath}.");
            return;
        }

        try
        {
            var actions = Actions();
            _clips = CutsceneTimeline.Build(actions, Text);
            _sourceReady = true;
            _sourceStatus.Visible = false;
            FillList();
            _scrub.MaxValue = Math.Max(1, CutsceneTimeline.Length(_clips));
            _tracks.CustomMinimumSize = new Vector2((float)_scrub.MaxValue * PixelsPerSecond + 170, 250);
            _tracks.QueueRedraw();
            UpdateFrame();
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or NullReferenceException or KeyNotFoundException or FormatException or ArgumentException or IndexOutOfRangeException)
        {
            ShowSourceError($"Не удалось прочитать {ScenePath}: {error.Message}. Исправьте этот JSON-исходник и обновите Studio.");
        }
    }

    private void FillList()
    {
        var needle = _search.Text.Trim();
        _visibleClips = _clips.Where(clip => needle.Length == 0
            || $"{clip.Label}\n{clip.Id}\n{clip.Track}\n{clip.Action}".Contains(needle, StringComparison.OrdinalIgnoreCase)).ToArray();
        _list.Clear();
        for (var index = 0; index < _visibleClips.Count; index++)
        {
            var clip = _visibleClips[index];
            _list.AddItem(FormattableString.Invariant($"{clip.Start,5:0.0} с  {(clip.Estimated ? "⧗ " : "")}{clip.Label}"));
            _list.SetItemTooltip(index, clip.Id);
            if (clip.Id == _selectedId) _list.Select(index);
        }

        _count.Text = needle.Length == 0 ? $"Действий: {_clips.Count}" : $"Показано: {_visibleClips.Count} из {_clips.Count}";
        _selected = _selectedId is null ? -1 : FindClip(_selectedId);
        UpdateButtons();
    }

    private void SelectVisible(int index)
    {
        if (index < 0 || index >= _visibleClips.Count) return;
        SelectClip(_visibleClips[index].Id);
    }

    private void SelectClip(string id)
    {
        _selectedId = id;
        _selected = FindClip(id);
        if (_selected >= 0)
        {
            _scrub.SetValueNoSignal(_clips[_selected].Start + .01);
            _studio.Select(id);
        }

        _tracks.QueueRedraw();
        UpdateFrame();
        UpdateButtons();
    }

    private int FindClip(string id)
    {
        for (var index = 0; index < _clips.Count; index++)
        {
            if (_clips[index].Id == id) return index;
        }

        return -1;
    }

    private void UpdateButtons()
    {
        var selected = _sourceReady && _selected >= 0 && _selected < _clips.Count;
        _moveEarlier.Disabled = !selected || _selected == 0;
        _moveLater.Disabled = !selected || _selected >= _clips.Count - 1;
        _insertPause.Disabled = !_sourceReady;
        _addShot.Disabled = !_sourceReady;
        _deleteAction.Disabled = !selected;
        _showInWorld.Disabled = !_sourceReady;
        _search.Editable = _sourceReady;
    }

    private void ShowSourceError(string message)
    {
        _sourceReady = false;
        _clips = [];
        _visibleClips = [];
        _selected = -1;
        _list.Clear();
        _count.Text = "Список действий недоступен";
        _sourceStatus.Text = message;
        _sourceStatus.Visible = true;
        _frame.Text = message;
        _scrub.MaxValue = 1;
        _tracks.QueueRedraw();
        UpdateButtons();
    }

    public int Selected => _selected;
    public IReadOnlyList<TimelineClip> Clips => _clips;

    public void SelectForTest(int index)
    {
        if (index < 0 || index >= _clips.Count) return;
        var id = _clips[index].Id;
        var visibleIndex = _visibleClips.ToList().FindIndex(clip => clip.Id == id);
        if (visibleIndex >= 0) _list.Select(visibleIndex);
        SelectClip(id);
    }

    public void ScrubForTest(double seconds) => _scrub.Value = seconds;

    public string FrameText => _frame.Text;

    private void UpdateFrame()
    {
        if (_clips.Count == 0)
        {
            _frame.Text = _sourceReady ? "В сцене пока нет действий." : _sourceStatus.Text;
            _tracks.QueueRedraw();
            return;
        }

        try
        {
            var actions = Actions();
            var frame = CutsceneTimeline.At(actions, _clips, _scrub.Value, Text);
            var shot = frame.Shot is null ? "—" : (string?)actions[frame.Shot.Index]!["params"]!["tag"] ?? "";
            _frame.Text = $"{_scrub.Value:0.0} с · план «{shot}»" + (frame.LineText is null ? "" : $"\nСубтитр: {frame.LineText}");
            var playhead = 170 + (float)_scrub.Value * PixelsPerSecond;
            if (_scroll.Size.X > 0 && (playhead < _scroll.ScrollHorizontal + 170 || playhead > _scroll.ScrollHorizontal + _scroll.Size.X - 40))
            {
                _scroll.ScrollHorizontal = (int)Math.Max(0, playhead - _scroll.Size.X / 2);
            }

            _tracks.QueueRedraw();
        }
        catch (Exception error) when (IsSourceShapeError(error))
        {
            ShowSourceError($"Не удалось прочитать {ScenePath}: {error.Message}. Исправьте этот JSON-исходник и обновите Studio.");
        }
    }

    private static bool IsSourceShapeError(Exception error) =>
        error is InvalidDataException or InvalidOperationException or NullReferenceException or KeyNotFoundException
            or InvalidCastException or FormatException or ArgumentException or IndexOutOfRangeException;

    private void DrawTracks()
    {
        var names = CutsceneTimeline.Tracks.Values.Distinct().Append("События мира").Distinct().ToArray();
        var font = GetThemeDefaultFont();
        for (var row = 0; row < names.Length; row++)
        {
            var y = 24 + row * 34;
            _tracks.DrawRect(new Rect2(0, y, _tracks.Size.X, 32), row % 2 == 0 ? new Color("1c1f24") : new Color("20242a"));
            _tracks.DrawString(font, new Vector2(6, y + 21), names[row], HorizontalAlignment.Left, 160, 13, StudioTheme.Muted);
        }

        for (var second = 0; second <= (int)_scrub.MaxValue; second += 5)
        {
            var x = 170 + second * PixelsPerSecond;
            _tracks.DrawLine(new Vector2(x, 0), new Vector2(x, _tracks.Size.Y), new Color("323843"));
            _tracks.DrawString(font, new Vector2(x + 3, 16), $"{second} с", HorizontalAlignment.Left, 60, 12, StudioTheme.Muted);
        }

        foreach (var clip in _clips)
        {
            var row = Array.IndexOf(names, clip.Track);
            var y = 24 + row * 34 + 4;
            var width = clip.Action == "cut" ? 6f : Math.Max(6f, (float)clip.Duration * PixelsPerSecond);
            if (clip.Action == "cut")
            {
                var next = _clips.FirstOrDefault(other => other.Action == "cut" && other.Start > clip.Start)?.Start ?? _scrub.MaxValue;
                width = Math.Max(6f, (float)(next - clip.Start) * PixelsPerSecond);
            }

            var colour = clip.Action switch { "cut" => new Color("4b3f63"), "say" => new Color("314a5d"), "move" => new Color("3c5a44"), "wait" => new Color("343a44"), _ => new Color("55503a") };
            var rect = new Rect2(170 + (float)clip.Start * PixelsPerSecond, y, width, 24);
            _tracks.DrawRect(rect, clip.Index == _selected ? colour.Lightened(.35f) : colour);
            if (clip.Estimated) _tracks.DrawRect(rect, new Color(1, 1, 1, .5f), false, 1);
            _tracks.DrawString(font, rect.Position + new Vector2(4, 17), clip.Label, HorizontalAlignment.Left, Math.Max(0, width - 6), 11, StudioTheme.Text);
        }

        var playhead = 170 + (float)_scrub.Value * PixelsPerSecond;
        _tracks.DrawLine(new Vector2(playhead, 0), new Vector2(playhead, _tracks.Size.Y), StudioTheme.Warn, 2);
    }

    private void OnTracksInput(InputEvent @event)
    {
        if (!_sourceReady) return;
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        var time = (click.Position.X - 170) / PixelsPerSecond;
        var names = CutsceneTimeline.Tracks.Values.Distinct().Append("События мира").Distinct().ToArray();
        var row = (int)((click.Position.Y - 24) / 34);
        var hit = _clips.LastOrDefault(clip => row >= 0 && row < names.Length && clip.Track == names[row] && clip.Start <= time + .1);
        if (hit is not null)
        {
            var visibleIndex = _visibleClips.ToList().FindIndex(clip => clip.Id == hit.Id);
            if (visibleIndex >= 0) _list.Select(visibleIndex);
            SelectClip(hit.Id);
        }
        else
        {
            _scrub.Value = Math.Max(0, time);
        }
    }

    private void ShowInWorld()
    {
        if (!_sourceReady) return;
        try
        {
            var actions = Actions();
            var frame = CutsceneTimeline.At(actions, _clips, _scrub.Value, Text);
            if (frame.Shot is null) return;
            var p = actions[frame.Shot.Index]!["params"]!;
            var position = p["position"];
            if (position is JsonObject positionObject && positionObject["car"] is not null && positionObject["point"] is null)
            {
                _studio.RefreshStatus("Этот план привязан к машине: его точное место определяется в игре после аварии.");
                return;
            }

            if (position?["point"] is not JsonArray at || p["look"]?["point"] is not JsonArray look)
            {
                ShowSourceError($"У плана действия {frame.Shot.Id} должны быть числовые точки position.point и look.point в {ScenePath}. Исправьте исходник и обновите Studio.");
                return;
            }

            var world = (StudioWorldSection)_studio.Section("world");
            _studio.OpenSection("world");
            world.ShowShot(new Vector3((float)(double)at[0]!, (float)(double)at[1]!, (float)(double)at[2]!),
                new Vector3((float)(double)look[0]!, (float)(double)look[1]!, (float)(double)look[2]!), (float)(double)p["fov"]!);
        }
        catch (Exception error) when (IsSourceShapeError(error))
        {
            ShowSourceError($"Не удалось показать план из {ScenePath}: {error.Message}. Исправьте этот JSON-исходник и обновите Studio.");
        }
    }

    private void AddShotFromView()
    {
        if (!_sourceReady) return;
        var world = (StudioWorldSection)_studio.Section("world");
        if (world.CameraShot() is not { } shot)
        {
            _studio.RefreshStatus("Сначала откройте «Мир» и поставьте камеру так, как должен выглядеть кадр.");
            return;
        }

        Insert("cut", new JsonObject
        {
            ["tag"] = "shot-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(2)),
            ["position"] = new JsonObject { ["point"] = new JsonArray(Math.Round(shot.Position.X, 2), Math.Round(shot.Position.Y, 2), Math.Round(shot.Position.Z, 2)) },
            ["look"] = new JsonObject { ["point"] = new JsonArray(Math.Round(shot.Look.X, 2), Math.Round(shot.Look.Y, 2), Math.Round(shot.Look.Z, 2)) },
            ["fov"] = Math.Round(shot.Fov, 1), ["seconds"] = 2.0, ["handheld"] = false, ["pushIn"] = false
        }, "Новый план из вида Studio");
    }

    /// <summary>Insert an action after the selection; its ID is new, the rest keep theirs (one undo step).</summary>
    public string Insert(string action, JsonObject parameters, string label)
    {
        if (!_sourceReady) throw new InvalidOperationException($"Исходник сцены недоступен: {ScenePath}.");
        var ids = Ids().ToList();
        var at = _selected >= 0 ? _selected + 1 : ids.Count;
        var id = "urman.cutscene:tamara-fence/a-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4));
        parameters["action"] = action;
        var entity = new JsonObject { ["id"] = id, ["kind"] = "cutscene-action", ["name"] = label, ["params"] = parameters };
        _selectedId = id;
        Reorder(ids.Take(at).Append(id).Concat(ids.Skip(at)).ToList(), new Dictionary<string, JsonNode> { [id] = entity }, $"добавить в сцену: {label}");
        return id;
    }

    private void DeleteSelected()
    {
        if (!_sourceReady || _selected < 0 || _selected >= _clips.Count) return;
        var id = _clips[_selected].Id;
        _selectedId = _selected + 1 < _clips.Count ? _clips[_selected + 1].Id : _selected > 0 ? _clips[_selected - 1].Id : null;
        _studio.Session.Delete(ScenePath, id, "удалить действие сцены");
    }

    public void MoveSelected(int delta)
    {
        if (!_sourceReady) return;
        var ids = Ids().ToList();
        if (_selected < 0 || _selected + delta < 0 || _selected + delta >= ids.Count) return;
        (ids[_selected], ids[_selected + delta]) = (ids[_selected + delta], ids[_selected]);
        _selectedId = _clips[_selected].Id;
        Reorder(ids, [], "порядок действий сцены");
    }

    /// <summary>Change the play order (and add new actions) as one command; entities keep their IDs so merges match them.</summary>
    private void Reorder(List<string> order, Dictionary<string, JsonNode> added, string label)
    {
        using var _ = _studio.Session.Begin(label);
        foreach (var (id, entity) in added) _studio.Session.Set(ScenePath, id, entity.DeepClone(), label);
        _studio.Session.SetOrder(ScenePath, order, label);
    }
}
