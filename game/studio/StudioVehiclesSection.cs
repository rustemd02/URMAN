using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>Edits the movement values consumed by the authored vehicle physics.</summary>
public sealed class StudioVehiclesSection : IStudioSection
{
    internal const string SourcePath = "game/content/vehicles/act1_vehicles.v1.json";
    internal const string TransactionOwner = "urman.studio:vehicle-catalog";
    private const int MaximumSourceBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private sealed record Vehicle(
        string Id, string Kind, string DisplayName,
        float MaxForwardSpeed, float ReverseSpeed, float Acceleration,
        float BrakeDeceleration, float SteeringDegrees, bool UsesMechanics);

    private sealed record Draft(
        string MaxForwardSpeed, string ReverseSpeed, string Acceleration,
        string BrakeDeceleration, string SteeringDegrees);

    private sealed record SourceSnapshot(string Hash, JsonObject Root, IReadOnlyList<Vehicle> Vehicles);

    private readonly StudioRoot _studio;
    private readonly Dictionary<string, Draft> _drafts = new(StringComparer.Ordinal);
    private Control? _view;
    private ItemList _list = null!;
    private LineEdit _search = null!;
    private Label _count = null!;
    private Label _identity = null!;
    private Label _status = null!;
    private LineEdit _maxForwardSpeed = null!;
    private LineEdit _reverseSpeed = null!;
    private LineEdit _acceleration = null!;
    private LineEdit _brakeDeceleration = null!;
    private LineEdit _steeringDegrees = null!;
    private Button _apply = null!;
    private Button _reload = null!;
    private string? _selectedId;
    private SourceSnapshot? _snapshot;
    private bool _loadingForm;
    private bool _applying;
    private bool _externalConflict;
    private string? _loadError;

    public StudioVehiclesSection(StudioRoot studio) => _studio = studio;

    public string Key => "vehicles";
    public string Title => "Транспорт";
    public Control View => _view ??= Build();
    public StudioCheckpointCatalogSource CheckpointCatalogSource =>
        new(TransactionOwner, SourcePath, CaptureCheckpointImage, ValidateCandidate);

    public bool HasPendingChanges
    {
        get
        {
            StoreDraftFromFields();
            return DirtyCount() > 0;
        }
    }

    private Control Build()
    {
        var split = new HSplitContainer { SplitOffsets = [330] };
        var left = new VBoxContainer();
        left.AddChild(new Label { Text = "Транспортные средства", ThemeTypeVariation = "HeaderLabel" });
        _search = new LineEdit { PlaceholderText = "Найти по названию, виду или ID…", ClearButtonEnabled = true };
        _search.TextChanged += _ => FillList();
        left.AddChild(_search);
        _count = new Label { ThemeTypeVariation = "MutedLabel" };
        left.AddChild(_count);
        _list = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _list.ItemSelected += index => Select((string)_list.GetItemMetadata((int)index));
        left.AddChild(_list);
        split.AddChild(left);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(form);
        form.AddChild(new Label { Text = "Параметры движения", ThemeTypeVariation = "HeaderLabel" });
        _identity = new Label { ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        form.AddChild(_identity);
        _maxForwardSpeed = Field(form, "Предельная скорость вперёд (м/с)");
        _reverseSpeed = Field(form, "Предельная скорость назад (м/с)");
        _acceleration = Field(form, "Разгон (м/с²)");
        _brakeDeceleration = Field(form, "Замедление при торможении (м/с²)");
        _steeringDegrees = Field(form, "Максимальный угол руля (градусы)");
        _maxForwardSpeed.TextChanged += _ => RecordDraft();
        _reverseSpeed.TextChanged += _ => RecordDraft();
        _acceleration.TextChanged += _ => RecordDraft();
        _brakeDeceleration.TextChanged += _ => RecordDraft();
        _steeringDegrees.TextChanged += _ => RecordDraft();
        form.AddChild(new Label
        {
            Text = "У Нивы с подключённой моторной моделью разгон и торможение рассчитывает трансмиссия; соответствующие поля каталога показаны для справки и не меняют движение. Остальные отредактированные параметры используются физикой при следующем запуске игры.",
            ThemeTypeVariation = "MutedLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        var actions = new HBoxContainer();
        _apply = StudioRoot.Button(actions, "Применить изменения", () => SavePendingChanges());
        _apply.ThemeTypeVariation = "PrimaryButton";
        _reload = StudioRoot.Button(actions, "Перечитать файл", ConfirmReload);
        form.AddChild(actions);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        form.AddChild(_status);
        split.AddChild(scroll);

        TryLoadInitialSource();
        FillList();
        ShowSelected();
        UpdateState();
        return split;
    }

    private static LineEdit Field(VBoxContainer parent, string label)
    {
        parent.AddChild(new Label { Text = label, ThemeTypeVariation = "MutedLabel" });
        var field = new LineEdit
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            PlaceholderText = "Число; десятичный разделитель — точка"
        };
        parent.AddChild(field);
        return field;
    }

    public void Refresh()
    {
        if (_view is null) return;
        CheckForSourceChange();
        FillList();
        ShowSelected();
        UpdateState();
    }

    public bool Reveal(string id)
    {
        if (_snapshot is null) return false;
        var vehicleId = id.StartsWith("vehicle/", StringComparison.Ordinal) ? id["vehicle/".Length..] : id;
        if (_snapshot.Vehicles.All(vehicle => vehicle.Id != vehicleId)) return false;
        _search.Text = "";
        Select(vehicleId);
        FillList();
        return true;
    }

    public void FillProperties(StudioProperties panel)
    {
        panel.Header("Транспорт", "Физика Act I");
        if (_selectedId is { } id && VehicleFor(id) is { } vehicle)
        {
            var draft = DraftFor(vehicle);
            panel.Text("ID: " + vehicle.Id, muted: true);
            panel.Text("Вид: " + vehicle.Kind, muted: true);
            panel.Text(vehicle.DisplayName, muted: true);
            panel.Text($"Вперёд: {draft.MaxForwardSpeed} м/с · назад: {draft.ReverseSpeed} м/с", muted: true);
            panel.Text($"Разгон: {draft.Acceleration} м/с² · торможение: {draft.BrakeDeceleration} м/с²", muted: true);
            panel.Text("Источник: " + SourcePath, muted: true);
            if (vehicle.UsesMechanics)
                panel.Text("Угол руля действует в физике; разгон и торможение у этой Нивы берутся из моторной модели.", muted: true);
        }
        else
        {
            panel.Text("Выберите транспорт слева, чтобы увидеть его физические параметры.", muted: true);
        }
    }

    private void TryLoadInitialSource()
    {
        try
        {
            AcceptSnapshot(ReadSnapshot(), clearDrafts: true);
            _selectedId ??= _snapshot?.Vehicles.FirstOrDefault()?.Id;
            _status.Text = "Каталог транспорта прочитан из исходника игры.";
        }
        catch (Exception error) when (IsSourceError(error))
        {
            _loadError = error.Message;
            _status.Text = "Каталог транспорта не открыт: " + error.Message;
        }
    }

    private void CheckForSourceChange()
    {
        try
        {
            var current = ReadSnapshot();
            if (_snapshot is null)
            {
                AcceptSnapshot(current, clearDrafts: true);
                return;
            }

            if (current.Hash == _snapshot.Hash) return;
            if (HasDirtyDrafts())
            {
                _externalConflict = true;
                _status.Text = "Файл транспорта изменился. Ваши черновики сохранены; перечитайте файл перед применением.";
                return;
            }

            AcceptSnapshot(current, clearDrafts: true);
            _status.Text = "Каталог транспорта обновлён из изменившегося исходника.";
        }
        catch (Exception error) when (IsSourceError(error))
        {
            if (HasDirtyDrafts())
            {
                _externalConflict = true;
                _status.Text = "Не удалось перечитать файл; ваши черновики сохранены. " + error.Message;
            }
            else
            {
                _loadError = error.Message;
                _externalConflict = true;
                _status.Text = "Исходник недоступен или повреждён. Последний прочитанный снимок оставлен только для просмотра. " + error.Message;
            }
        }
    }

    private void AcceptSnapshot(SourceSnapshot snapshot, bool clearDrafts)
    {
        _snapshot = snapshot;
        _externalConflict = false;
        _loadError = null;
        if (clearDrafts) _drafts.Clear();
        if (_selectedId is null || snapshot.Vehicles.All(vehicle => vehicle.Id != _selectedId))
            _selectedId = snapshot.Vehicles.FirstOrDefault()?.Id;
    }

    private SourceSnapshot ReadSnapshot()
    {
        var rootPath = Path.GetFullPath(_studio.Workspace.Root);
        StudioFileTransaction.EnsureNoLinks(rootPath, SourcePath);
        var path = Path.Combine(rootPath, SourcePath.Replace('/', Path.DirectorySeparatorChar));
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("Файл транспорта не найден.", path);
        if (info.Length > MaximumSourceBytes) throw new InvalidDataException("Файл транспорта превышает допустимый размер.");

        var bytes = File.ReadAllBytes(path);
        var sourceText = new UTF8Encoding(false, true).GetString(bytes);
        var root = JsonNode.Parse(sourceText) as JsonObject
            ?? throw new InvalidDataException("Каталог транспорта должен быть JSON-объектом.");
        return new(AtomicFile.Sha256(bytes), root, ReadVehicles(root));
    }

    private static IReadOnlyList<Vehicle> ReadVehicles(JsonObject root)
    {
        if (root["vehicles"] is not JsonArray vehicles || vehicles.Count == 0)
            throw new InvalidDataException("Каталог должен содержать непустой массив vehicles.");

        var result = new List<Vehicle>(vehicles.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in vehicles)
        {
            if (value is not JsonObject item) throw new InvalidDataException("Каждое транспортное средство должно быть JSON-объектом.");
            var id = RequiredString(item, "id");
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                throw new InvalidDataException("ID транспорта пустой или повторяется.");
            var kind = RequiredString(item, "kind");
            if (kind is not ("Niva" or "Motorcycle" or "HorseCart"))
                throw new InvalidDataException($"Неизвестный вид транспорта: {kind}.");
            var displayName = RequiredString(item, "displayName");
            ValidateVector(item, "spawn");
            _ = Number(item, "yawDegrees");
            var maxForwardSpeed = PositiveNumber(item, "maxForwardSpeed");
            var reverseSpeed = PositiveNumber(item, "reverseSpeed");
            var acceleration = PositiveNumber(item, "acceleration");
            var brakeDeceleration = PositiveNumber(item, "brakeDeceleration");
            _ = PositiveNumber(item, "wheelBase");
            var steeringDegrees = NonNegativeNumber(item, "steeringDegrees");
            ValidatePositiveVector(item, "hullSize");
            ValidateVector(item, "hullCenter");
            ValidateVector(item, "seat");
            _ = PositiveNumber(item, "wheelRadius");
            if (item["hasRadio"] is not JsonValue radio || !radio.TryGetValue<bool>(out _))
                throw new InvalidDataException($"У транспорта {id} отсутствует логическое поле hasRadio.");

            var usesMechanics = kind == "Niva" && item["mechanics"] is JsonObject;
            result.Add(new(id, kind, displayName, maxForwardSpeed, reverseSpeed, acceleration,
                brakeDeceleration, steeringDegrees, usesMechanics));
        }

        return result;
    }

    private static string RequiredString(JsonObject item, string field) =>
        item[field] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : throw new InvalidDataException($"У транспорта отсутствует строковое поле {field}.");

    private static float Number(JsonObject item, string field)
    {
        if (item[field] is not JsonValue value
            || !float.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || !float.IsFinite(number))
            throw new InvalidDataException($"Поле {field} должно быть конечным числом.");
        return number;
    }

    private static float PositiveNumber(JsonObject item, string field)
    {
        var number = Number(item, field);
        return number > 0 ? number : throw new InvalidDataException($"Поле {field} должно быть больше нуля.");
    }

    private static float NonNegativeNumber(JsonObject item, string field)
    {
        var number = Number(item, field);
        return number >= 0 ? number : throw new InvalidDataException($"Поле {field} не может быть отрицательным.");
    }

    private static void ValidateVector(JsonObject item, string field)
    {
        if (item[field] is not JsonArray vector || vector.Count != 3)
            throw new InvalidDataException($"Поле {field} должно содержать три координаты.");
        foreach (var component in vector)
        {
            if (component is not JsonValue value
                || !float.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                || !float.IsFinite(number))
                throw new InvalidDataException($"Поле {field} содержит некорректную координату.");
        }
    }

    private static void ValidatePositiveVector(JsonObject item, string field)
    {
        ValidateVector(item, field);
        if (((JsonArray)item[field]!).Any(value =>
                !float.TryParse(value!.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number <= 0))
            throw new InvalidDataException($"Размеры {field} должны быть больше нуля.");
    }

    private Vehicle? VehicleFor(string id) => _snapshot?.Vehicles.FirstOrDefault(vehicle => vehicle.Id == id);

    private Draft DraftFor(Vehicle vehicle) => _drafts.GetValueOrDefault(vehicle.Id) ?? From(vehicle);

    private static Draft From(Vehicle vehicle) => new(
        Format(vehicle.MaxForwardSpeed), Format(vehicle.ReverseSpeed), Format(vehicle.Acceleration),
        Format(vehicle.BrakeDeceleration), Format(vehicle.SteeringDegrees));

    private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static bool Same(Draft draft, Vehicle vehicle) =>
        IsSamePositive(draft.MaxForwardSpeed, vehicle.MaxForwardSpeed)
        && IsSamePositive(draft.ReverseSpeed, vehicle.ReverseSpeed)
        && (vehicle.UsesMechanics || IsSamePositive(draft.Acceleration, vehicle.Acceleration)
            && IsSamePositive(draft.BrakeDeceleration, vehicle.BrakeDeceleration))
        && IsSameNonNegative(draft.SteeringDegrees, vehicle.SteeringDegrees);

    private static bool IsSamePositive(string text, float original) =>
        TryPositive(text, out var value) && value == original;

    private static bool IsSameNonNegative(string text, float original) =>
        TryNonNegative(text, out var value) && value == original;

    private static bool TryPositive(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && float.IsFinite(value) && value > 0;

    private static bool TryNonNegative(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && float.IsFinite(value) && value >= 0;

    private bool HasDirtyDrafts() => _snapshot is not null && _drafts.Any(pair =>
        _snapshot.Vehicles.FirstOrDefault(vehicle => vehicle.Id == pair.Key) is not { } vehicle || !Same(pair.Value, vehicle));

    private int DirtyCount() => _snapshot is null ? 0 : _drafts.Count(pair =>
        _snapshot.Vehicles.FirstOrDefault(vehicle => vehicle.Id == pair.Key) is { } vehicle && !Same(pair.Value, vehicle));

    private void Select(string id)
    {
        StoreDraftFromFields();
        if (_snapshot is null || _snapshot.Vehicles.All(vehicle => vehicle.Id != id)) return;
        _selectedId = id;
        ShowSelected();
        UpdateState();
    }

    private void StoreDraftFromFields()
    {
        if (_loadingForm || _selectedId is null || _snapshot is null || VehicleFor(_selectedId) is null
            || _maxForwardSpeed is null || _reverseSpeed is null || _acceleration is null
            || _brakeDeceleration is null || _steeringDegrees is null) return;
        _drafts[_selectedId] = new(_maxForwardSpeed.Text, _reverseSpeed.Text, _acceleration.Text,
            _brakeDeceleration.Text, _steeringDegrees.Text);
    }

    private void RecordDraft()
    {
        StoreDraftFromFields();
        UpdateState();
    }

    private void FillList()
    {
        if (_list is null) return;
        _list.Clear();
        if (_snapshot is null)
        {
            _count.Text = _loadError is null ? "Каталог недоступен" : "Каталог не прочитан";
            return;
        }

        var needle = _search.Text.Trim();
        var matches = _snapshot.Vehicles
            .Where(vehicle => needle.Length == 0 || ($"{vehicle.DisplayName}\n{vehicle.Kind}\n{vehicle.Id}")
                .Contains(needle, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(vehicle => vehicle.DisplayName, StringComparer.CurrentCulture)
            .ToArray();
        foreach (var vehicle in matches)
        {
            var dirty = _drafts.TryGetValue(vehicle.Id, out var staged) && !Same(staged, vehicle);
            _list.AddItem((dirty ? "● " : "") + vehicle.DisplayName + "  ·  " + vehicle.Kind);
            _list.SetItemMetadata(_list.ItemCount - 1, vehicle.Id);
            _list.SetItemTooltip(_list.ItemCount - 1, "vehicle/" + vehicle.Id + "\n" + vehicle.Kind);
            if (vehicle.Id == _selectedId) _list.Select(_list.ItemCount - 1);
        }

        var dirtyCount = DirtyCount();
        _count.Text = $"Найдено: {matches.Length}" + (dirtyCount == 0 ? "" : $" · черновиков: {dirtyCount}");
    }

    private void ShowSelected()
    {
        if (_identity is null) return;
        if (_snapshot is null || _selectedId is null || VehicleFor(_selectedId) is not { } vehicle)
        {
            _identity.Text = "Транспорт не выбран";
            SetFormEditable(false, usesMechanics: false);
            _loadingForm = true;
            _maxForwardSpeed.Text = _reverseSpeed.Text = _acceleration.Text = _brakeDeceleration.Text = _steeringDegrees.Text = "";
            _loadingForm = false;
            return;
        }

        var draft = DraftFor(vehicle);
        _loadingForm = true;
        _identity.Text = $"ID: {vehicle.Id}   ·   вид: {vehicle.Kind}   ·   {vehicle.DisplayName}";
        _maxForwardSpeed.Text = draft.MaxForwardSpeed;
        _reverseSpeed.Text = draft.ReverseSpeed;
        _acceleration.Text = draft.Acceleration;
        _brakeDeceleration.Text = draft.BrakeDeceleration;
        _steeringDegrees.Text = draft.SteeringDegrees;
        SetFormEditable(true, vehicle.UsesMechanics);
        _loadingForm = false;
    }

    private void SetFormEditable(bool enabled, bool usesMechanics)
    {
        _maxForwardSpeed.Editable = enabled && !_busyForUi;
        _reverseSpeed.Editable = enabled && !_busyForUi;
        _acceleration.Editable = enabled && !_busyForUi && !usesMechanics;
        _brakeDeceleration.Editable = enabled && !_busyForUi && !usesMechanics;
        _steeringDegrees.Editable = enabled && !_busyForUi;
    }

    private bool _busyForUi => _applying || _studio.Session.IsBusy
        || _studio.IsWorkspaceActionBusy || _studio.IsTextAiBusy;

    private void UpdateState()
    {
        if (_apply is null) return;
        var dirty = DirtyCount();
        _apply.Text = dirty == 0 ? "Применить изменения" : $"Применить изменения ({dirty})";
        _apply.Disabled = _busyForUi || dirty == 0 || _externalConflict || _loadError is not null;
        _reload.Disabled = _busyForUi;
        SetFormEditable(_snapshot is not null && _selectedId is not null && !_externalConflict && _loadError is null,
            VehicleFor(_selectedId ?? "")?.UsesMechanics ?? false);
        if (_externalConflict && HasDirtyDrafts())
            _status.AddThemeColorOverride("font_color", StudioTheme.Bad);
        else if (dirty > 0)
            _status.AddThemeColorOverride("font_color", StudioTheme.Warn);
        else
            _status.RemoveThemeColorOverride("font_color");
    }

    private void ConfirmReload()
    {
        if (!HasDirtyDrafts())
        {
            ReloadFromDisk();
            return;
        }

        var dialog = new ConfirmationDialog
        {
            Title = "Отбросить черновики?",
            DialogText = "Несохранённые параметры транспорта будут удалены, а каталог перечитан с диска.",
            OkButtonText = "Отбросить и перечитать",
            CancelButtonText = "Оставить черновики",
            MinSize = new Vector2I(520, 0)
        };
        dialog.Confirmed += () =>
        {
            try { ReloadFromDisk(); }
            finally { dialog.QueueFree(); }
        };
        dialog.Canceled += dialog.QueueFree;
        _studio.AddChild(dialog);
        dialog.PopupCentered();
    }

    private void ReloadFromDisk()
    {
        try
        {
            AcceptSnapshot(ReadSnapshot(), clearDrafts: true);
            _status.Text = "Каталог транспорта перечитан. Несохранённые черновики сброшены.";
            FillList();
            ShowSelected();
        }
        catch (Exception error) when (IsSourceError(error))
        {
            _loadError = error.Message;
            _externalConflict = true;
            _status.Text = "Каталог транспорта не перечитан: " + error.Message;
        }

        UpdateState();
    }

    public void DiscardPendingChanges()
    {
        StoreDraftFromFields();
        _drafts.Clear();
        _externalConflict = false;
        ReloadFromDisk();
        _status.Text = "Несохранённые параметры транспорта отброшены.";
        UpdateState();
    }

    public bool SavePendingChanges()
    {
        StoreDraftFromFields();
        if (DirtyCount() == 0) return true;
        if (_snapshot is null || _externalConflict || _loadError is not null)
        {
            _status.Text = "Черновики не сохранены. Перечитайте каталог и разрешите конфликт перед применением.";
            UpdateState();
            return false;
        }

        if (_busyForUi)
        {
            _status.Text = "Дождитесь завершения текущего действия Studio перед сохранением транспорта.";
            UpdateState();
            return false;
        }

        var saved = false;
        try
        {
            var disk = ReadSnapshot();
            if (disk.Hash != _snapshot.Hash)
            {
                _externalConflict = true;
                _status.Text = "Каталог изменился после загрузки. Черновики сохранены, но не записаны. Перечитайте файл и проверьте их заново.";
                UpdateState();
                return false;
            }

            var candidate = BuildCandidate();
            var bytes = Encoding.UTF8.GetBytes(candidate.ToJsonString(JsonOptions) + "\n");
            var candidateHash = AtomicFile.Sha256(bytes);
            var baselineHash = _snapshot.Hash;
            ValidateCandidate(bytes);
            var write = new StudioFileWrite(SourcePath, _snapshot.Hash, bytes);
            var allowedPaths = new HashSet<string>(StringComparer.Ordinal) { SourcePath };

            _applying = true;
            _status.Text = "Применяю параметры транспорта…";
            UpdateState();
            _studio.Session.ApplyFiles("изменить физику транспорта", TransactionOwner, [write], allowedPaths,
                () => ReloadAfterTransaction(candidateHash, baselineHash));

            _drafts.Clear();
            _externalConflict = false;
            _loadError = null;
            _status.Text = "Параметры транспорта записаны одной отменяемой операцией. Игра прочитает их при следующем запуске.";
            saved = true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or JsonException or ArgumentException)
        {
            _status.Text = "Изменения не применены: " + error.Message + " Черновики оставлены на месте.";
        }
        finally
        {
            _applying = false;
            Refresh();
            UpdateState();
        }

        return saved;
    }

    private JsonObject BuildCandidate()
    {
        if (_snapshot is null) throw new InvalidOperationException("Сначала перечитайте каталог.");
        var candidate = _snapshot.Root.DeepClone().AsObject();
        if (candidate["vehicles"] is not JsonArray vehicles) throw new InvalidDataException("В каталоге отсутствует массив vehicles.");
        foreach (var pair in _drafts)
        {
            var original = _snapshot.Vehicles.FirstOrDefault(vehicle => vehicle.Id == pair.Key);
            if (original is null || Same(pair.Value, original)) continue;
            var matches = vehicles.OfType<JsonObject>().Where(item => (string?)item["id"] == pair.Key).ToArray();
            if (matches.Length != 1) throw new InvalidDataException($"ID транспорта {pair.Key} отсутствует или повторяется; каталог не изменён.");
            var draft = pair.Value;
            matches[0]["maxForwardSpeed"] = ParsePositive(draft.MaxForwardSpeed, "скорость вперёд");
            matches[0]["reverseSpeed"] = ParsePositive(draft.ReverseSpeed, "скорость назад");
            matches[0]["steeringDegrees"] = ParseNonNegative(draft.SteeringDegrees, "угол руля");
            if (!original.UsesMechanics)
            {
                matches[0]["acceleration"] = ParsePositive(draft.Acceleration, "разгон");
                matches[0]["brakeDeceleration"] = ParsePositive(draft.BrakeDeceleration, "торможение");
            }
        }

        return candidate;
    }

    private StudioCheckpointCatalogImage CaptureCheckpointImage()
    {
        StoreDraftFromFields();
        if (_externalConflict || _loadError is not null)
            throw new InvalidOperationException("Сначала перечитайте каталог транспорта и разрешите конфликт.");

        var disk = ReadSnapshot();
        if (!HasDirtyDrafts()) return new(disk.Hash, null);
        if (_snapshot is null || disk.Hash != _snapshot.Hash)
        {
            _externalConflict = true;
            if (_status is not null) _status.Text = "Каталог транспорта изменился. Черновики сохранены, но не включены в снимок; перечитайте файл перед продолжением.";
            UpdateState();
            throw new InvalidOperationException("Каталог транспорта изменился после загрузки. Сначала разрешите конфликт.");
        }

        var bytes = Encoding.UTF8.GetBytes(BuildCandidate().ToJsonString(JsonOptions) + "\n");
        ValidateCandidate(bytes);
        return new(disk.Hash, bytes);
    }

    private static float ParsePositive(string text, string label) => TryPositive(text, out var value)
        ? value
        : throw new InvalidDataException($"Параметр «{label}» должен быть конечным числом больше нуля (десятичная точка — точка).");

    private static float ParseNonNegative(string text, string label) => TryNonNegative(text, out var value)
        ? value
        : throw new InvalidDataException($"Параметр «{label}» должен быть конечным числом не меньше нуля (десятичная точка — точка).");

    private static void ValidateCandidate(byte[] bytes)
    {
        var text = new UTF8Encoding(false, true).GetString(bytes);
        var root = JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException("Изменённый каталог не является JSON-объектом.");
        _ = ReadVehicles(root);
    }

    private void ReloadAfterTransaction(string candidateHash, string baselineHash)
    {
        var snapshot = ReadSnapshot();
        if (_applying && snapshot.Hash != candidateHash && snapshot.Hash != baselineHash)
            throw new InvalidDataException("Каталог после транзакции не совпадает с ожидаемым снимком.");

        if (_applying)
        {
            AcceptSnapshot(snapshot, clearDrafts: false);
        }
        else if (HasDirtyDrafts())
        {
            _externalConflict = true;
            _status.Text = "Каталог изменён отменой или повтором. Ваши черновики сохранены; перечитайте файл перед применением.";
        }
        else
        {
            AcceptSnapshot(snapshot, clearDrafts: true);
            _status.Text = "Каталог обновлён после отмены или повтора.";
        }

        FillList();
        ShowSelected();
        UpdateState();
    }

    private static bool IsSourceError(Exception error) => error is IOException or UnauthorizedAccessException
        or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or FormatException;
}
