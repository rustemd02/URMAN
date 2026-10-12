using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>One frozen localized-text request with a reviewed, reversible draft apply.</summary>
public partial class StudioTextAiPanel : AcceptDialog
{
    private const string ConsentPath = ".urman-studio/codex-pilot.json";
    private readonly StudioRoot _studio;
    private readonly string _consentPath;
    private readonly Label _target = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly TextEdit _prompt = new()
    {
        CustomMinimumSize = new Vector2(0, 90),
        WrapMode = TextEdit.LineWrappingMode.Boundary,
        TabInputMode = false,
        PlaceholderText = "Например: сделай реплику теплее, сохранив её смысл"
    };
    private readonly CheckButton _globalInstructions = new() { Text = "Использовать мои глобальные инструкции Codex" };
    private readonly TextEdit _before = ReadOnlyText();
    private readonly TextEdit _after = ReadOnlyText();
    private readonly Button _ask;
    private readonly Button _stop;
    private readonly Button _apply;
    private string? _entityId;
    private EntityTextRequest? _request;
    private EntityTextProposal? _proposal;
    private CancellationTokenSource? _cancel;
    private double _freshnessTimer;
    private bool _busy;
    private bool _stale;
    private string? _consentWarning;

    internal bool IsActionBusy => _busy || _studio.IsWorkspaceActionBusy;
    internal bool HasActiveOperation => _busy || _cancel is not null;

    public StudioTextAiPanel(StudioRoot studio)
    {
        _studio = studio;
        GuiEmbedSubwindows = true;
        Title = "Предложить вариант текста";
        OkButtonText = "Закрыть";
        MinSize = new Vector2I(850, 580);
        WrapControls = true;

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 480), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(body);
        body.AddChild(_target);
        body.AddChild(_status);
        body.AddChild(new Label { Text = "Что изменить", ThemeTypeVariation = "MutedLabel" });
        body.AddChild(_prompt);

        _consentPath = Path.Combine(studio.Workspace.Root, ConsentPath.Replace('/', Path.DirectorySeparatorChar));
        LoadGlobalInstructionConsent(_consentPath);
        _globalInstructions.TooltipText = "Только ваши инструкции из ~/.codex/AGENTS.md. Команды, инструменты и запись проекта остаются запрещены.";
        _globalInstructions.Toggled += allowed => SaveGlobalInstructionConsent(_consentPath, allowed);
        body.AddChild(_globalInstructions);

        var actions = new HBoxContainer();
        body.AddChild(actions);
        _ask = StudioRoot.Button(actions, "Предложить вариант", () => _ = AskAsync());
        _stop = StudioRoot.Button(actions, "Остановить", StopCurrentOperation);
        _apply = StudioRoot.Button(actions, "Применить в черновик", ApplyProposal);

        var compare = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddChild(compare);
        var beforeColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        beforeColumn.AddChild(new Label { Text = "Текущий текст", ThemeTypeVariation = "HeaderLabel" });
        beforeColumn.AddChild(_before);
        compare.AddChild(beforeColumn);
        var afterColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        afterColumn.AddChild(new Label { Text = "Предложение", ThemeTypeVariation = "HeaderLabel" });
        afterColumn.AddChild(_after);
        compare.AddChild(afterColumn);
        body.AddChild(new Label
        {
            Text = "Применение изменит один текст в черновике Studio. Другие переводы и поля сохраняются; отмена доступна через Undo.",
            ThemeTypeVariation = "MutedLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        _prompt.TextChanged += InvalidateProposalForPromptChange;
        VisibilityChanged += HandleVisibilityChanged;
        TreeExiting += HandleTreeExiting;
        UpdateControls();
    }

    public void Open(string textId)
    {
        if (HasActiveOperation) return;
        LoadGlobalInstructionConsent(_consentPath);
        _entityId = textId;
        _request = null;
        _proposal = null;
        _stale = false;
        _before.Text = string.Empty;
        _after.Text = string.Empty;
        _prompt.Text = string.Empty;
        _status.Text = ConsentStatus("Опишите нужный вариант. Перед передачей текста будет создан точный снимок одного поля.");
        try
        {
            var summary = _studio.Catalog.Describe(textId);
            var field = _studio.Workspace.Get(textId) is JsonObject entity
                && TryGetRussianText(entity, out _, out _, out _, out var resolvedField)
                ? resolvedField == EntityTextProposalProtocol.RussianTranslationField ? "отдельный русский перевод" : "основной русский текст"
                : "русский текст";
            _target.Text = $"Объект: {summary.Name} · {field}";
            _target.TooltipText = textId;
        }
        catch (Exception)
        {
            _target.Text = "Объект текста";
            _target.TooltipText = textId;
        }
        UpdateControls();
        PopupCenteredClamped(new Vector2I(980, 700), 0.9f);
        _prompt.GrabFocus();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _busy || _request is null || _proposal is null) return;
        _freshnessTimer += delta;
        if (_freshnessTimer < 1.5) return;
        _freshnessTimer = 0;
        RefreshRequestFreshness();
    }

    private async Task AskAsync()
    {
        if (IsActionBusy || HasActiveOperation || !Visible || _entityId is null) return;

        EntityTextRequest request;
        try
        {
            request = CaptureRequest();
            EnsureRequestCurrent(request);
        }
        catch (Exception error) when (IsRecoverable(error))
        {
            _status.Text = "Запрос не отправлен: " + error.Message;
            return;
        }

        _request = request;
        _proposal = null;
        _stale = false;
        _before.Text = request.Source.CurrentText;
        _after.Text = string.Empty;
        var useGlobalInstructions = _globalInstructions.ButtonPressed;
        var cancellation = new CancellationTokenSource();
        _cancel = cancellation;
        SetBusy(true);
        DirectoryInfo? snapshotDirectory = null;
        try
        {
            snapshotDirectory = Directory.CreateTempSubdirectory("urman-text-" + request.RequestId + "-");
            if (snapshotDirectory.EnumerateFileSystemInfos().Any())
                throw new InvalidOperationException("Временная рабочая папка должна быть пустой.");

            var provider = new CodexProposalProvider(allowGlobalInstructions: useGlobalInstructions);
            _status.Text = ConsentStatus("Запрос отправлен. Ожидаю структурированное предложение.");
            var proposal = await provider.RunAsync(request, snapshotDirectory.FullName, cancellation.Token);
            if (!GodotObject.IsInstanceValid(this) || !Visible || !ReferenceEquals(_request, request)) return;
            if (_globalInstructions.ButtonPressed != useGlobalInstructions)
                throw new InvalidOperationException("Настройка глобальных инструкций изменилась во время запроса.");
            EnsureRequestCurrent(request);
            EntityTextProposalProtocol.ValidateProposal(request, proposal);
            _proposal = proposal;
            _stale = false;
            _after.Text = proposal.ProposedText;
            _status.Text = ConsentStatus("Предложение получено: " + proposal.Summary + " Проверьте текст до применения.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (GodotObject.IsInstanceValid(this) && Visible && ReferenceEquals(_request, request))
                _status.Text = ConsentStatus("Запрос остановлен. Проект не изменён.");
        }
        catch (Exception error) when (IsRecoverable(error))
        {
            if (GodotObject.IsInstanceValid(this) && Visible && ReferenceEquals(_request, request))
            {
                _proposal = null;
                _after.Text = string.Empty;
                _status.Text = ConsentStatus("Предложение не получено: " + error.Message);
            }
        }
        finally
        {
            if (snapshotDirectory is not null)
            {
                try { snapshotDirectory.Delete(recursive: true); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    if (GodotObject.IsInstanceValid(this) && Visible)
                        _status.Text += " Временную рабочую папку не удалось удалить: " + error.Message;
                }
            }
            if (ReferenceEquals(_cancel, cancellation)) _cancel = null;
            cancellation.Dispose();
            if (GodotObject.IsInstanceValid(this)) SetBusy(false);
        }
    }

    private EntityTextRequest CaptureRequest()
    {
        if (_entityId is not { } entityId || !_studio.CanOpenTextAi(entityId)
            || _studio.Workspace.Locate(entityId) is not { } address)
            throw new InvalidOperationException("Текстовый объект больше недоступен.");
        if (string.IsNullOrWhiteSpace(_prompt.Text))
            throw new InvalidOperationException("Опишите нужный вариант текста.");
        if (_prompt.Text.Length > EntityTextProposalProtocol.MaximumPromptCharacters)
            throw new InvalidOperationException("Описание превышает 8 000 знаков.");

        var entity = _studio.Workspace.Get(entityId) as JsonObject
            ?? throw new InvalidOperationException("Текстовый объект больше недоступен.");
        if (!TryGetRussianText(entity, out var defaultText, out var russianTranslation, out var currentText, out var field))
            throw new InvalidOperationException("У объекта нет поддерживаемого русского текста.");
        if (currentText.Length > EntityTextProposalProtocol.MaximumTextCharacters)
            throw new InvalidOperationException("Текущий текст превышает поддерживаемые 12 000 знаков.");

        var source = _studio.Workspace.File(address.RelativePath).CaptureSource();
        var unsigned = new EntityTextRequest(
            Guid.NewGuid().ToString("N"),
            string.Empty,
            entityId,
            field,
            _prompt.Text,
            new EntityTextSource(address.RelativePath, address.Key, source.BaseSha256, source.Sha256, defaultText, russianTranslation, currentText));
        return unsigned with { SnapshotHash = EntityTextProposalProtocol.ComputeSnapshotHash(unsigned) };
    }

    private void EnsureRequestCurrent(EntityTextRequest request)
    {
        EntityTextProposalProtocol.ValidateRequest(request);
        if (_studio.Workspace.Locate(request.EntityId) is not { } address
            || address.RelativePath != request.Source.Path
            || address.Key != request.Source.EntityKey
            || EntityCatalog.KindOf(request.EntityId, _studio.Workspace.Get(request.EntityId) as JsonObject) != "text")
            throw new InvalidOperationException("Текстовый объект или его исходный файл изменился.");

        var source = _studio.Workspace.File(request.Source.Path).CaptureSource();
        if (!string.Equals(source.Sha256, request.Source.CurrentHash, StringComparison.Ordinal)
            || !string.Equals(source.BaseSha256, request.Source.BaseHash, StringComparison.Ordinal)
            || _studio.Workspace.Get(request.EntityId) is not JsonObject entity
            || !TryGetRussianText(entity, out var defaultText, out var russianTranslation, out var currentText, out var field)
            || !string.Equals(field, request.Field, StringComparison.Ordinal)
            || !string.Equals(defaultText, request.Source.DefaultText, StringComparison.Ordinal)
            || !string.Equals(russianTranslation, request.Source.RussianTranslation, StringComparison.Ordinal)
            || !string.Equals(currentText, request.Source.CurrentText, StringComparison.Ordinal))
            throw new InvalidOperationException("Исходный текст изменился после снимка. Запросите новый вариант.");
    }

    private void RefreshRequestFreshness()
    {
        if (_request is not { } request || _proposal is null) return;
        try
        {
            EnsureRequestCurrent(request);
            if (_stale)
            {
                _stale = false;
                _status.Text = "Точный снимок снова совпадает с черновиком. Проверьте предложение перед применением.";
                UpdateControls();
            }
        }
        catch (Exception error) when (IsRecoverable(error))
        {
            if (!_stale)
            {
                _stale = true;
                _status.Text = "Предложение устарело: " + error.Message;
                UpdateControls();
            }
        }
    }

    private void ApplyProposal()
    {
        if (IsActionBusy || !Visible || _request is not { } request || _proposal is not { } proposal) return;
        try
        {
            EnsureRequestCurrent(request);
            EntityTextProposalProtocol.ValidateProposal(request, proposal);
            if (_studio.Workspace.Locate(request.EntityId) is not { } address
                || _studio.Workspace.Get(request.EntityId) is not JsonObject currentEntity)
                throw new InvalidOperationException("Текстовый объект больше недоступен.");

            var updated = (JsonObject)currentEntity.DeepClone();
            var tatarBefore = currentEntity["value"]?["translations"]?["tt"]?.DeepClone();
            updated["value"] = Localized.WithRussian(updated["value"], proposal.ProposedText);
            if (!TryGetBoundFieldText(updated["value"], proposal.Field, out var appliedText)
                || !string.Equals(appliedText, proposal.ProposedText, StringComparison.Ordinal)
                || !JsonNode.DeepEquals(tatarBefore, updated["value"]?["translations"]?["tt"]))
                throw new InvalidOperationException("Новый русский текст не совпал с выбранным полем или изменил татарский перевод.");
            _studio.Session.Set(address.RelativePath, address.Key, updated, "вариант текста");
            _request = null;
            _proposal = null;
            _stale = false;
            _status.Text = "Вариант внесён в черновик одним действием Undo. Файл не сохранён автоматически.";
            _studio.Refresh();
        }
        catch (Exception error) when (IsRecoverable(error))
        {
            _stale = true;
            _status.Text = "Не применено: " + error.Message;
        }
        UpdateControls();
    }

    private void StopCurrentOperation()
    {
        if (_cancel is null) return;
        _status.Text = "Останавливаю запрос…";
        _cancel.Cancel();
        UpdateControls();
    }

    private void InvalidateProposalForPromptChange()
    {
        if (_busy) return;
        if (_request is null)
        {
            UpdateControls();
            return;
        }
        _request = null;
        _proposal = null;
        _stale = false;
        _before.Text = string.Empty;
        _after.Text = string.Empty;
        _status.Text = "Описание изменилось. Предыдущее предложение отозвано.";
        UpdateControls();
    }

    private void HandleVisibilityChanged()
    {
        if (Visible) return;
        _cancel?.Cancel();
        _request = null;
        _proposal = null;
        _stale = false;
        _before.Text = string.Empty;
        _after.Text = string.Empty;
        if (GodotObject.IsInstanceValid(this)) UpdateControls();
    }

    private void HandleTreeExiting()
    {
        _cancel?.Cancel();
        _request = null;
        _proposal = null;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UpdateControls();
        if (GodotObject.IsInstanceValid(_studio)) _studio.RefreshStatus();
    }

    private void UpdateControls()
    {
        if (!GodotObject.IsInstanceValid(_ask)) return;
        var busy = IsActionBusy;
        _prompt.Editable = !busy;
        _globalInstructions.Disabled = busy;
        _ask.Disabled = busy || HasActiveOperation || _entityId is null || string.IsNullOrWhiteSpace(_prompt.Text)
            || _prompt.Text.Length > EntityTextProposalProtocol.MaximumPromptCharacters;
        _stop.Disabled = !_busy;
        _apply.Disabled = busy || _stale || _proposal is null || _request is null;
    }

    private void LoadGlobalInstructionConsent(string path)
    {
        _consentWarning = null;
        _globalInstructions.SetPressedNoSignal(false);
        try
        {
            if (!File.Exists(path)) return;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("allowGlobalInstructions", out var allowed)
                && allowed.ValueKind is JsonValueKind.True or JsonValueKind.False)
                _globalInstructions.SetPressedNoSignal(allowed.GetBoolean());
        }
        catch (IOException)
        {
            _consentWarning = "Не удалось прочитать локальный выбор инструкций Codex; он выключен.";
        }
        catch (UnauthorizedAccessException)
        {
            _consentWarning = "Нет доступа к локальному выбору инструкций Codex; он выключен.";
        }
        catch (JsonException)
        {
            _consentWarning = "Локальный выбор инструкций Codex повреждён; он выключен.";
        }
    }

    private void SaveGlobalInstructionConsent(string path, bool allowed)
    {
        try
        {
            AtomicFile.WriteAllText(path, new JsonObject { ["allowGlobalInstructions"] = allowed }.ToJsonString());
            _consentWarning = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _globalInstructions.SetPressedNoSignal(!allowed);
            _consentWarning = "Не удалось сохранить выбор инструкций Codex; переключатель возвращён.";
            _status.Text = _consentWarning;
        }
    }

    internal static bool HasSupportedRussianText(JsonObject entity) =>
        TryGetRussianText(entity, out _, out _, out _, out _);

    private static bool TryGetRussianText(
        JsonObject entity,
        out string defaultText,
        out string? russianTranslation,
        out string russianText,
        out string field)
    {
        defaultText = string.Empty;
        russianTranslation = null;
        russianText = string.Empty;
        field = string.Empty;
        if (entity["value"] is not JsonObject localized
            || localized["default"] is not JsonValue defaultValue)
            return false;
        if (!defaultValue.TryGetValue<string>(out var parsedDefault) || parsedDefault is null)
            return false;
        defaultText = parsedDefault;

        if (localized["translations"] is { } translationsNode && translationsNode is not JsonObject)
            return false;
        if (localized["translations"] is JsonObject translations
            && translations.TryGetPropertyValue("ru", out var translationNode)
            && translationNode is not null)
        {
            if (translationNode is not JsonValue translationValue
                || !translationValue.TryGetValue<string>(out var parsedTranslation)
                || parsedTranslation is null)
                return false;
            russianTranslation = parsedTranslation;
        }

        try
        {
            russianText = Localized.Russian(localized);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        field = EntityTextProposalProtocol.ResolveField(defaultText, russianTranslation);
        return defaultText.Length <= EntityTextProposalProtocol.MaximumTextCharacters
            && (russianTranslation?.Length ?? 0) <= EntityTextProposalProtocol.MaximumTextCharacters
            && russianText.Length <= EntityTextProposalProtocol.MaximumTextCharacters
            && string.Equals(russianText, russianTranslation ?? defaultText, StringComparison.Ordinal);
    }

    private static bool TryGetBoundFieldText(JsonNode? localized, string field, out string text)
    {
        var node = field switch
        {
            EntityTextProposalProtocol.DefaultField => localized?["default"],
            EntityTextProposalProtocol.RussianTranslationField => localized?["translations"]?["ru"],
            _ => null
        };
        if (node is JsonValue value && value.TryGetValue<string>(out var parsedText) && parsedText is not null)
        {
            text = parsedText;
            return true;
        }
        text = string.Empty;
        return false;
    }

    private static TextEdit ReadOnlyText() => new()
    {
        Editable = false,
        WrapMode = TextEdit.LineWrappingMode.Boundary,
        CustomMinimumSize = new Vector2(0, 230),
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        SizeFlagsVertical = Control.SizeFlags.ExpandFill
    };

    private string ConsentStatus(string message) => _consentWarning is null ? message : message + "\n" + _consentWarning;

    private static bool IsRecoverable(Exception error) => error is not OutOfMemoryException and not StackOverflowException;
}
