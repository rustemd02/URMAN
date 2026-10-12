using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>
/// Edits the shop's actual item catalogue. SKUs stay fixed because they form
/// persistent runtime item IDs; only fields read by ShopCatalog are editable.
/// </summary>
public sealed class StudioItemsSection : IStudioSection
{
    private readonly StudioRoot _studio;

    public StudioItemsSection(StudioRoot studio) => _studio = studio;

    internal const string SourcePath = "game/content/urman.shop.v1.json";
    internal const string TransactionOwner = "urman.studio:shop-catalog";
    private const int MaximumSourceBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private sealed record Product(string Sku, string Title, string Unit, string Description, IReadOnlyList<string> Uses);
    private sealed record Draft(string Title, string Unit, string Description, IReadOnlyList<string> Uses);
    private sealed record SourceSnapshot(string Hash, JsonObject Root, IReadOnlyList<Product> Products);

    private Control? _view;
    private ItemList _list = null!;
    private LineEdit _search = null!;
    private Label _count = null!;
    private Label _sku = null!;
    private LineEdit _title = null!;
    private LineEdit _unit = null!;
    private TextEdit _description = null!;
    private TextEdit _uses = null!;
    private Label _status = null!;
    private Button _apply = null!;
    private Button _reload = null!;
    private string? _selectedSku;
    private SourceSnapshot? _snapshot;
    private readonly Dictionary<string, Draft> _drafts = new(StringComparer.Ordinal);
    private bool _loadingForm;
    private bool _applying;
    private bool _externalConflict;
    private string? _loadError;

    public string Key => "items";
    public string Title => "Предметы";
    public Control View => _view ??= Build();

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
        left.AddChild(new Label { Text = "Товары в магазине", ThemeTypeVariation = "HeaderLabel" });
        _search = new LineEdit { PlaceholderText = "Найти по названию, описанию или SKU…", ClearButtonEnabled = true };
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
        form.AddChild(new Label { Text = "Карточка товара", ThemeTypeVariation = "HeaderLabel" });
        _sku = new Label { ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.Arbitrary };
        form.AddChild(_sku);
        _title = Field(form, "Название в игре", new LineEdit());
        _unit = Field(form, "Как продаётся", new LineEdit());
        _description = Field(form, "Описание и подсказка игроку", new TextEdit
        {
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            CustomMinimumSize = new Vector2(0, 130)
        });
        _uses = Field(form, "Где предмет можно применить (по одному ID в строке)", new TextEdit
        {
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            CustomMinimumSize = new Vector2(0, 82)
        });
        _title.TextChanged += _ => RecordDraft();
        _unit.TextChanged += _ => RecordDraft();
        _description.TextChanged += RecordDraft;
        _uses.TextChanged += RecordDraft;

        form.AddChild(new Label
        {
            Text = "В игре используются название, единица, описание и список применений. SKU сохраняется: он задаёт постоянный ID предмета. В модели магазина нет цены, вместимости или флага квестового предмета.",
            ThemeTypeVariation = "MutedLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        form.AddChild(new Label
        {
            Text = "Изменения записываются одной отменяемой операцией и видны игре при следующем запуске.",
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

    private static T Field<T>(VBoxContainer parent, string label, T field) where T : Control
    {
        parent.AddChild(new Label { Text = label, ThemeTypeVariation = "MutedLabel" });
        field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
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
        var sku = id.StartsWith("shop/", StringComparison.Ordinal) ? id["shop/".Length..] : id;
        if (!_snapshot.Products.Any(product => product.Sku == sku)) return false;
        _search.Text = "";
        Select(sku);
        FillList();
        return true;
    }

    public void FillProperties(StudioProperties panel)
    {
        panel.Header("Товары магазина", "Каталог игры");
        if (_selectedSku is { } sku && ProductFor(sku) is { } product)
        {
            var draft = DraftFor(product);
            panel.Text("SKU: " + sku, muted: true);
            panel.Text("Игровой ID: shop/" + sku, muted: true);
            panel.Text("В игре: " + draft.Title + " — " + draft.Unit, muted: true);
            panel.Text(draft.Description, muted: true);
            panel.Text("Применение: " + (draft.Uses.Count == 0 ? "не указано" : string.Join(", ", draft.Uses)), muted: true);
        }
        else
        {
            panel.Text("Выберите товар слева, чтобы открыть его карточку.", muted: true);
        }

        panel.Text("Источник: " + SourcePath, muted: true);
        panel.Text("Правки SKU запрещены, чтобы не менять сохранённые ссылки и ID.", muted: true);
    }

    private void TryLoadInitialSource()
    {
        try
        {
            AcceptSnapshot(ReadSnapshot(), clearDrafts: true);
            _selectedSku ??= _snapshot?.Products.FirstOrDefault()?.Sku;
            _status.Text = "Каталог прочитан из исходника игры.";
        }
        catch (Exception error) when (IsSourceError(error))
        {
            _loadError = error.Message;
            _status.Text = "Каталог не открыт: " + error.Message;
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
                _loadError = null;
                return;
            }

            if (current.Hash == _snapshot.Hash) return;
            if (HasDirtyDrafts())
            {
                _externalConflict = true;
                _status.Text = "Файл каталога изменился. Ваши черновики сохранены; применить их нельзя, пока вы не перечитаете файл.";
                return;
            }

            AcceptSnapshot(current, clearDrafts: true);
            _loadError = null;
            _status.Text = "Каталог обновлён из изменившегося исходника.";
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
        if (_selectedSku is null || snapshot.Products.All(product => product.Sku != _selectedSku))
            _selectedSku = snapshot.Products.FirstOrDefault()?.Sku;
    }

    private SourceSnapshot ReadSnapshot()
    {
        var rootPath = Path.GetFullPath(_studio.Workspace.Root);
        StudioFileTransaction.EnsureNoLinks(rootPath, SourcePath);
        var path = Path.Combine(rootPath, SourcePath.Replace('/', Path.DirectorySeparatorChar));
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("Файл каталога магазина не найден.", path);
        if (info.Length > MaximumSourceBytes) throw new InvalidDataException("Файл каталога магазина превышает допустимый размер.");

        var bytes = File.ReadAllBytes(path);
        var sourceText = new UTF8Encoding(false, true).GetString(bytes);
        var root = JsonNode.Parse(sourceText) as JsonObject
            ?? throw new InvalidDataException("Каталог магазина должен быть JSON-объектом.");
        var products = ReadProducts(root);
        return new(AtomicFile.Sha256(bytes), root, products);
    }

    private static IReadOnlyList<Product> ReadProducts(JsonObject root)
    {
        if (root["items"] is not JsonArray items || items.Count == 0)
            throw new InvalidDataException("Каталог должен содержать непустой массив items.");

        var products = new List<Product>(items.Count);
        var skus = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in items)
        {
            if (value is not JsonObject item) throw new InvalidDataException("Каждый товар должен быть JSON-объектом.");
            var sku = RequiredString(item, "sku");
            if (string.IsNullOrWhiteSpace(sku) || !skus.Add(sku))
                throw new InvalidDataException("SKU товара пустой или повторяется.");
            var title = RequiredString(item, "title");
            var unit = RequiredString(item, "unit");
            var description = RequiredString(item, "description");
            if (item["uses"] is not JsonArray uses)
                throw new InvalidDataException($"У товара {sku} отсутствует массив uses.");
            var useIds = uses.Select(use => use is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
                    ? text
                    : throw new InvalidDataException($"У товара {sku} поле uses должно содержать строки."))
                .ToArray();
            if (useIds.Any(string.IsNullOrWhiteSpace) || useIds.Distinct(StringComparer.Ordinal).Count() != useIds.Length)
                throw new InvalidDataException($"У товара {sku} есть пустые или повторные ID применения.");
            products.Add(new(sku, title, unit, description, useIds));
        }

        return products;
    }

    private static string RequiredString(JsonObject item, string field) =>
        item[field] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : throw new InvalidDataException($"У товара отсутствует строковое поле {field}.");

    private Product? ProductFor(string sku) => _snapshot?.Products.FirstOrDefault(product => product.Sku == sku);

    private Draft DraftFor(Product product) => _drafts.GetValueOrDefault(product.Sku) ?? From(product);

    private static Draft From(Product product) => new(product.Title, product.Unit, product.Description, product.Uses.ToArray());

    private static bool Same(Draft left, Product right) => left.Title == right.Title
        && left.Unit == right.Unit
        && left.Description == right.Description
        && left.Uses.SequenceEqual(right.Uses, StringComparer.Ordinal);

    private bool HasDirtyDrafts() => _snapshot is not null && _drafts.Any(pair =>
        _snapshot.Products.FirstOrDefault(product => product.Sku == pair.Key) is not { } product || !Same(pair.Value, product));

    private int DirtyCount() => _snapshot is null ? 0 : _drafts.Count(pair =>
        _snapshot.Products.FirstOrDefault(product => product.Sku == pair.Key) is { } product && !Same(pair.Value, product));

    private void Select(string sku)
    {
        StoreDraftFromFields();
        if (_snapshot is null || _snapshot.Products.All(product => product.Sku != sku)) return;
        _selectedSku = sku;
        ShowSelected();
        UpdateState();
    }

    private void StoreDraftFromFields()
    {
        if (_loadingForm || _selectedSku is null || _snapshot is null || ProductFor(_selectedSku) is null
            || _title is null || _unit is null || _description is null || _uses is null) return;
        _drafts[_selectedSku] = new(_title.Text, _unit.Text, _description.Text, ParseUseIds(_uses.Text));
    }

    private static IReadOnlyList<string> ParseUseIds(string text) => text
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .ToArray();

    private void RecordDraft()
    {
        StoreDraftFromFields();
        UpdateState();
        _studio.RefreshStatus();
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
        var matches = _snapshot.Products.Select(product => (Product: product, Draft: DraftFor(product)))
            .Where(item => needle.Length == 0 || ($"{item.Draft.Title}\n{item.Draft.Unit}\n{item.Draft.Description}\n{item.Product.Sku}\n{string.Join("\n", item.Draft.Uses)}")
                .Contains(needle, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(item => item.Draft.Title, StringComparer.CurrentCulture)
            .ToArray();
        foreach (var (product, draft) in matches)
        {
            var dirty = _drafts.TryGetValue(product.Sku, out var staged) && !Same(staged, product);
            _list.AddItem((dirty ? "● " : "") + draft.Title + "  ·  " + draft.Unit);
            _list.SetItemMetadata(_list.ItemCount - 1, product.Sku);
            _list.SetItemTooltip(_list.ItemCount - 1, "shop/" + product.Sku + "\n" + draft.Description);
            if (product.Sku == _selectedSku) _list.Select(_list.ItemCount - 1);
        }

        _count.Text = $"Найдено: {matches.Length}" + (DirtyCount() == 0 ? "" : $" · черновиков: {DirtyCount()}");
    }

    private void ShowSelected()
    {
        if (_sku is null) return;
        if (_snapshot is null || _selectedSku is null || ProductFor(_selectedSku) is not { } product)
        {
            _sku.Text = "Товар не выбран";
            SetFormEditable(false);
            _loadingForm = true;
            _title.Text = "";
            _unit.Text = "";
            _description.Text = "";
            _uses.Text = "";
            _loadingForm = false;
            return;
        }

        var draft = DraftFor(product);
        _loadingForm = true;
        _sku.Text = $"SKU: {product.Sku}   ·   постоянный игровой ID: shop/{product.Sku}";
        _title.Text = draft.Title;
        _unit.Text = draft.Unit;
        _description.Text = draft.Description;
        _uses.Text = string.Join("\n", draft.Uses);
        SetFormEditable(true);
        _loadingForm = false;
    }

    private void SetFormEditable(bool enabled)
    {
        _title.Editable = enabled && !_busyForUi;
        _unit.Editable = enabled && !_busyForUi;
        _description.Editable = enabled && !_busyForUi;
        _uses.Editable = enabled && !_busyForUi;
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
        SetFormEditable(_snapshot is not null && _selectedSku is not null && !_externalConflict && _loadError is null);
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
            DialogText = "Несохранённые правки товаров будут удалены, а каталог перечитан с диска.",
            OkButtonText = "Отбросить и перечитать",
            CancelButtonText = "Оставить черновики",
            MinSize = new Vector2I(520, 0)
        };
        dialog.Confirmed += () =>
        {
            try
            {
                ReloadFromDisk();
            }
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
            _loadError = null;
            _status.Text = "Каталог перечитан. Несохранённые черновики сброшены.";
            FillList();
            ShowSelected();
        }
        catch (Exception error) when (IsSourceError(error))
        {
            _loadError = error.Message;
            _externalConflict = true;
            _status.Text = "Каталог не перечитан: " + error.Message;
        }

        UpdateState();
    }

    public void DiscardPendingChanges()
    {
        StoreDraftFromFields();
        _drafts.Clear();
        _externalConflict = false;
        ReloadFromDisk();
        _status.Text = "Несохранённые правки товаров отброшены.";
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
            _status.Text = "Дождитесь завершения текущего действия Studio перед сохранением товаров.";
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
            _status.Text = "Применяю каталог…";
            UpdateState();
            _studio.Session.ApplyFiles("изменить товары магазина", TransactionOwner, [write], allowedPaths,
                () => ReloadAfterTransaction(candidateHash, baselineHash));

            _drafts.Clear();
            _externalConflict = false;
            _loadError = null;
            _status.Text = "Каталог применён одной отменяемой операцией. Игра прочитает его при следующем запуске.";
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
        if (candidate["items"] is not JsonArray items) throw new InvalidDataException("В каталоге отсутствует массив items.");
        foreach (var pair in _drafts)
        {
            var original = _snapshot.Products.FirstOrDefault(product => product.Sku == pair.Key);
            if (original is null || Same(pair.Value, original)) continue;
            var matches = items.OfType<JsonObject>().Where(item => (string?)item["sku"] == pair.Key).ToArray();
            if (matches.Length != 1) throw new InvalidDataException($"SKU {pair.Key} отсутствует или повторяется; каталог не изменён.");
            if (string.IsNullOrWhiteSpace(pair.Value.Title) || string.IsNullOrWhiteSpace(pair.Value.Unit))
                throw new InvalidDataException($"Заполните название и единицу для товара {pair.Key}.");
            var uses = pair.Value.Uses;
            if (uses.Any(string.IsNullOrWhiteSpace) || uses.Distinct(StringComparer.Ordinal).Count() != uses.Count)
                throw new InvalidDataException($"У товара {pair.Key} есть пустые или повторные ID применения.");
            matches[0]["title"] = pair.Value.Title;
            matches[0]["unit"] = pair.Value.Unit;
            matches[0]["description"] = pair.Value.Description;
            matches[0]["uses"] = new JsonArray(uses.Select(use => (JsonNode?)JsonValue.Create(use)).ToArray());
        }

        return candidate;
    }

    private static void ValidateCandidate(byte[] bytes)
    {
        var text = new UTF8Encoding(false, true).GetString(bytes);
        var root = JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException("Изменённый каталог не является JSON-объектом.");
        _ = ReadProducts(root);
    }

    private void ReloadAfterTransaction(string candidateHash, string baselineHash)
    {
        var snapshot = ReadSnapshot();
        if (_applying && snapshot.Hash != candidateHash && snapshot.Hash != baselineHash)
            throw new InvalidDataException("Каталог после транзакции не совпадает с ожидаемым снимком.");

        if (_applying)
        {
            // Keep staged values until ApplyFiles has accepted the history command.
            AcceptSnapshot(snapshot, clearDrafts: false);
        }
        else if (HasDirtyDrafts())
        {
            // Undo/Redo can change the source while the author has another local draft.
            // Preserve that draft and require an explicit reload before applying it.
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

    private static bool IsSourceError(Exception error) => error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or FormatException;
}
