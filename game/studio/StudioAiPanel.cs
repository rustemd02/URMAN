using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Godot;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>Frozen house request → typed proposal → staged geometry → one reversible apply.</summary>
public partial class StudioAiPanel : AcceptDialog, IProgress<CodexProposalProgress>
{
    private const string RecipePath = "game/content/studio/hero_house.recipe.json";
    private const string GeneratorPath = "assets/source/blender/act1/urman_village_exterior_kit.py";
    private const int SelectedWindowScope = 0;
    private const int MainRoomWindowsScope = 1;
    private const int AllWindowsScope = 2;
    private readonly StudioRoot _studio;
    private readonly TextEdit _prompt = new() { CustomMinimumSize = new(560, 100), WrapMode = TextEdit.LineWrappingMode.Boundary, TabInputMode = false, PlaceholderText = "Например: сделай окна этого дома шире" };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private string? _openedSelection;
    private StudioWorldSection.SelectedPart? _openedPart;
    private Control? _previousFocus;
    private bool _focusCaptured;
    private readonly OptionButton _scope = new();
    private readonly CheckButton _globalInstructions = new() { Text = "Использовать мои глобальные инструкции Codex" };
    private readonly Button _ask;
    private readonly Button _apply;
    private readonly Button _copy;
    private readonly Button _paste;
    private readonly Button _saveRequest;
    private readonly Button _importFile;
    private readonly Button _beforeView;
    private readonly Button _afterView;
    private readonly SubViewport _preview = new() { Size = new(560, 260), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
    private readonly Node3D _previewModels = new() { Name = "HouseCompareModels" };
    private readonly Camera3D _previewCamera = new() { Position = new(11, 7, 13), Current = true };
    private Node3D? _beforeModel;
    private Node3D? _afterModel;
    private CancellationTokenSource? _cancel;
    private HouseWindowRequest? _request;
    private HouseWindowProposal? _proposal;
    private string? _stage;
    private string? _recipeText;
    private string? _snapshot;
    private JsonObject? _capturedWidths;
    private string? _consentIoWarning;
    private bool _busy;
    private int _providerWarningSeen;

    void IProgress<CodexProposalProgress>.Report(CodexProposalProgress progress)
    {
        if (progress.Stage == CodexProposalStage.ConfigurationWarning)
            Interlocked.Exchange(ref _providerWarningSeen, 1);
    }

    public static bool OwnsFile(string path) => path == RecipePath ||
        System.Text.RegularExpressions.Regex.IsMatch(path,
            @"\A(?:game/assets/models/studio/hero-house-[a-f0-9]{32}\.glb(?:\.manifest\.json)?|assets/source/blender/studio/hero-house-[a-f0-9]{32}\.blend)\z",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public StudioAiPanel(StudioRoot studio)
    {
        _studio = studio;
        Title = "Изменить окна выбранного дома";
        OkButtonText = "Закрыть";
        MinSize = new(600, 560);
        WrapControls = true;
        var body = new VBoxContainer();
        AddChild(body);
        body.AddChild(_status);
        body.AddChild(_prompt);
        _scope.AddItem("Только выбранное окно", SelectedWindowScope);
        _scope.AddItem("7 окон главных комнат", MainRoomWindowsScope);
        _scope.AddItem("Все 8 окон, включая окно в сенях", AllWindowsScope);
        _scope.TooltipText = "Охват изменения окон. Для выбранного окна выберите его в мире; при применении остальные проёмы сохранятся.";
        body.AddChild(_scope);
        _scope.ItemSelected += _ =>
        {
            SetBusy(_busy);
            UpdateScopeStatus(scopeChanged: _request is not null && _proposal is null);
        };
        body.AddChild(_globalInstructions);
        var permissionFile = Path.Combine(studio.Workspace.Root, ".urman-studio/codex-pilot.json");
        LoadGlobalInstructionConsent(permissionFile);
        _globalInstructions.TooltipText = "Только ваши инструкции из ~/.codex/AGENTS.md. Команды, инструменты и запись модели остаются запрещены.";
        _globalInstructions.Toggled += allowed => SaveGlobalInstructionConsent(permissionFile, allowed);
        var row = new HBoxContainer();
        body.AddChild(row);
        _ask = StudioRoot.Button(row, "Предложить изменение", () => _ = AskAsync());
        _ask.TooltipText = "Отправить запрос: ⌘/Ctrl+Enter";
        _prompt.GuiInput += input =>
        {
            if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter } key
                && (key.MetaPressed || key.CtrlPressed))
            {
                _prompt.AcceptEvent();
                if (!_ask.Disabled) _ = AskAsync();
            }
        };
        StudioRoot.Button(row, "Проверить Codex", () => _ = DoctorAsync());
        StudioRoot.Button(row, "Остановить", () => _cancel?.Cancel());
        _apply = StudioRoot.Button(row, "Применить", () => _ = ApplyAsync());
        _apply.Disabled = true;
        var handoff = new HBoxContainer();
        body.AddChild(handoff);
        _copy = StudioRoot.Button(handoff, "Скопировать запрос для агента", CopyRequest);
        _copy.TooltipText = "Вставьте запрос в свой Codex или Claude. Это ручной перенос: Studio не управляет внешней сессией.";
        _paste = StudioRoot.Button(handoff, "Вставить JSON-ответ", () => _ = RunProposalAsync(fromManual: true));
        _paste.TooltipText = "Скопируйте чистый JSON ответа. Studio проверит его и покажет модель до применения.";
        _paste.Disabled = true;
        var files = new HBoxContainer();
        body.AddChild(files);
        _saveRequest = StudioRoot.Button(files, "Сохранить запрос в файл", SaveRequest);
        _saveRequest.TooltipText = "Передайте текст своему агенту. Его внешней сессией Studio не управляет.";
        _importFile = StudioRoot.Button(files, "Открыть JSON-ответ…", OpenProposalFile);
        _importFile.TooltipText = "Проверить ответ по сохранённому запросу и показать модель до применения";
        _importFile.Disabled = true;
        var compare = new HBoxContainer();
        body.AddChild(compare);
        _beforeView = StudioRoot.Button(compare, "До", () => ShowPreview(after: false));
        _afterView = StudioRoot.Button(compare, "После", () => ShowPreview(after: true));
        _beforeView.Disabled = true;
        _afterView.Disabled = true;
        var viewport = new SubViewportContainer { Stretch = true, CustomMinimumSize = new(560, 260) };
        viewport.AddChild(_preview);
        body.AddChild(viewport);
        _preview.AddChild(_previewModels);
        _preview.AddChild(new DirectionalLight3D { RotationDegrees = new(-40, -25, 0), LightEnergy = 1.5f });
        _previewCamera.Ready += () => _previewCamera.LookAt(new(0, 1.5f, 0));
        _preview.AddChild(_previewCamera);
        VisibilityChanged += () =>
        {
            if (Visible) return;
            _cancel?.Cancel();
            if (!_focusCaptured) return;
            var previousFocus = _previousFocus;
            _previousFocus = null;
            _focusCaptured = false;
            Callable.From(() =>
            {
                if (Visible) return;
                if (GodotObject.IsInstanceValid(previousFocus) && previousFocus!.IsInsideTree()
                    && previousFocus.IsVisibleInTree() && previousFocus.FocusMode != Control.FocusModeEnum.None)
                    previousFocus.GrabFocus();
                else
                    ((StudioWorldSection)_studio.Section("world")).RestoreViewportFocus();
            }).CallDeferred();
        };
    }

    private void UpdateScopeStatus(bool scopeChanged = false)
    {
        if (_openedSelection != HeroHouseRecipe.EntityId) return;
        var summary = _scope.Selected switch
        {
            SelectedWindowScope => $"{_openedPart?.Label ?? "Выбранное окно"} · будет изменено только это окно. Остальные проёмы, высоты и двери сохранятся.",
            MainRoomWindowsScope => "Эталонный дом · изменятся ровно 7 окон главных комнат. Окно в сенях и ширина по умолчанию сохранятся.",
            AllWindowsScope => "Эталонный дом · изменятся все 8 окон, включая окно в сенях.",
            _ => "Выберите охват изменения окон."
        };
        summary += scopeChanged
            ? "\nОхват изменился после подготовки запроса. Скопируйте новый запрос перед вставкой JSON-ответа."
            : "\nCodex предложит ширину, вы увидите новую модель перед применением. ⌘/Ctrl+Z полностью отменяет применение.";
        if (_consentIoWarning is not null) summary += "\n" + _consentIoWarning;
        _status.Text = summary;
    }

    private (string? PartId, IReadOnlyList<string>? PartIds) SelectedScope() => _scope.Selected switch
    {
        SelectedWindowScope when _openedPart is { PartId: var partId }
            && HeroHouseRecipe.WindowIds.Contains(partId, StringComparer.Ordinal) => (partId, null),
        MainRoomWindowsScope => (null, HouseWindowProposalValidator.MainRoomWindowIds),
        AllWindowsScope => (null, null),
        _ => throw new InvalidOperationException("Выберите поддерживаемый охват окон.")
    };

    private bool RequestScopeMatchesCurrent(HouseWindowRequest request)
    {
        var current = SelectedScope();
        return request.PartId == current.PartId
            && (request.PartIds is null
                ? current.PartIds is null
                : current.PartIds is not null && request.PartIds.SequenceEqual(current.PartIds, StringComparer.Ordinal));
    }

    private void LoadGlobalInstructionConsent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("allowGlobalInstructions", out var allowed)
                    && allowed.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    _globalInstructions.ButtonPressed = allowed.GetBoolean();
            }
        }
        catch (IOException)
        {
            _consentIoWarning = "Не удалось прочитать локальный выбор инструкций Codex; он выключен.";
        }
        catch (UnauthorizedAccessException)
        {
            _consentIoWarning = "Нет доступа к локальному выбору инструкций Codex; он выключен.";
        }
        catch (JsonException)
        {
            // A malformed consent file remains opt-out by default.
        }
    }

    private void SaveGlobalInstructionConsent(string path, bool allowed)
    {
        try
        {
            AtomicFile.WriteAllText(path, new JsonObject { ["allowGlobalInstructions"] = allowed }.ToJsonString());
            _consentIoWarning = null;
        }
        catch (IOException)
        {
            _globalInstructions.SetPressedNoSignal(!allowed);
            _consentIoWarning = "Не удалось сохранить выбор инструкций Codex; переключатель возвращён.";
            _status.Text = _consentIoWarning;
        }
        catch (UnauthorizedAccessException)
        {
            _globalInstructions.SetPressedNoSignal(!allowed);
            _consentIoWarning = "Нет доступа для сохранения выбора инструкций Codex; переключатель возвращён.";
            _status.Text = _consentIoWarning;
        }
    }

    public void Open(string? selected)
    {
        if (Visible) { _prompt.GrabFocus(); return; }
        _previousFocus = _studio.GetViewport().GuiGetFocusOwner();
        _focusCaptured = true;
        ((StudioWorldSection)_studio.Section("world")).SuspendForAi();
        if (_busy)
        {
            PopupCenteredClamped(new(600, 560), 0.9f);
            _prompt.GrabFocus();
            return;
        }
        ClearProposal();
        _request = null;
        _openedSelection = selected;
        _openedPart = ((StudioWorldSection)_studio.Section("world")).SelectionPart;
        var windowSelected = _openedPart is { PartId: var partId }
            && HeroHouseRecipe.WindowIds.Contains(partId, StringComparer.Ordinal);
        _scope.Visible = selected == HeroHouseRecipe.EntityId;
        _scope.SetItemDisabled(SelectedWindowScope, !windowSelected);
        _scope.Select(windowSelected ? SelectedWindowScope : MainRoomWindowsScope);
        SetBusy(false);
        if (selected == HeroHouseRecipe.EntityId)
            UpdateScopeStatus();
        else
        {
            _status.Text = "Выберите эталонный дом Бабая в мире. Другие объекты будут подключены следующими этапами B20.";
            if (_consentIoWarning is not null) _status.Text += "\n" + _consentIoWarning;
        }
        ResetSize();
        PopupCenteredClamped(new(600, 560), 0.9f);
        _prompt.GrabFocus();
        // Wrapped labels have their final minimum height only after container layout.
        Callable.From(() =>
        {
            if (!Visible) return;
            ResetSize();
            PopupCenteredClamped(new(600, 560), 0.9f);
            _prompt.GrabFocus();
        }).CallDeferred();
    }

    private HouseWindowRequest Capture()
    {
        if (_openedSelection != HeroHouseRecipe.EntityId || _studio.Selection != _openedSelection) throw new InvalidOperationException("Выбор изменился: снова выберите эталонный дом.");
        if (_studio.Workspace.Locate(HeroHouseRecipe.EntityId) is null)
            throw new InvalidOperationException("Источник дома не найден или его ID повторяется. Исправьте идентичность перед запросом.");
        if (string.IsNullOrWhiteSpace(_prompt.Text)) throw new InvalidOperationException("Опишите изменение окон.");
        _recipeText = File.ReadAllText(Path.Combine(_studio.Workspace.Root, RecipePath));
        _ = HeroHouseRecipe.Parse(_recipeText);
        var recipe = JsonNode.Parse(_recipeText)!.AsObject();
        if (recipe["modelPath"] is null && ((decimal)recipe["windowClearWidth"]! != 1.06m
            || recipe["windowWidths"] is JsonObject existingWidths && existingWidths.Count > 0))
            throw new InvalidOperationException("Рецепт изменён без согласованной модели. Сначала восстановите принятый исходный дом.");
        _capturedWidths = new JsonObject();
        foreach (var id in HeroHouseRecipe.WindowIds)
            _capturedWidths[id] = (decimal?)recipe["windowWidths"]?[id]
                ?? (id == "SeniOuter0" && recipe["modelPath"] is null ? .80m : (decimal)recipe["windowClearWidth"]!);
        var (partId, partIds) = SelectedScope();
        _snapshot = Fingerprint(_recipeText);
        var currentWidth = partId is null ? (decimal)recipe["windowClearWidth"]! : (decimal)_capturedWidths[partId]!;
        return new HouseWindowRequest(Guid.NewGuid().ToString("N"), _snapshot, HeroHouseRecipe.EntityId,
            currentWidth, _prompt.Text, partId) { PartIds = partIds };
    }

    private string Fingerprint(string recipe)
    {
        var world = _studio.Workspace.Locate(HeroHouseRecipe.EntityId)!;
        var current = HeroHouseRecipe.Parse(recipe);
        var modelPath = current.ModelPath ?? "res://assets/models/act1/urman_village_exterior_kit.glb";
        var modelHash = AtomicFile.Sha256OfFile(Path.Combine(_studio.Workspace.Root, "game", modelPath["res://".Length..]));
        var source = File.ReadAllBytes(Path.Combine(_studio.Workspace.Root, GeneratorPath));
        return AtomicFile.Sha256(System.Text.Encoding.UTF8.GetBytes(recipe + "\n" +
            _studio.Workspace.File(world.RelativePath).Text + "\n" + AtomicFile.Sha256(source) + "\n" + modelHash + "\n" +
            AtomicFile.Sha256OfFile(Path.Combine(_studio.Workspace.Root, "assets/source/blender/act1/urman_village_exterior_kit.blend")) + "\n" + Revision()));
    }

    private string Revision()
    {
        var revision = Urman.Studio.Core.Handoff.DeveloperContext.ResolveRevision(_studio.Workspace.Root);
        if (revision == "неизвестна") throw new IOException("Нет полной Git-ревизии. Проверьте checkout перед запросом.");
        return revision;
    }

    private HouseWindowRequest PrepareManualRequest()
    {
        if (_proposal is null) _request = Capture();
        var request = _request ?? throw new InvalidOperationException("Запрос ещё не подготовлен.");
        HouseWindowProposalValidator.ValidateRequest(request);
        SetBusy(false);
        return request;
    }

    private void CopyRequest()
    {
        if (_busy) return;
        try
        {
            DisplayServer.ClipboardSet(CodexProposalProvider.CreateClipboardPrompt(PrepareManualRequest()));
            if (_proposal is null) _status.Text = "Вставьте запрос в свой агент, затем скопируйте JSON ответа и нажмите «Вставить JSON-ответ». Studio покажет модель до применения. Внешняя сессия работает отдельно.";
        }
        catch (Exception error) { _status.Text = error.Message; }
    }

    private void SaveRequest()
    {
        if (_busy) return;
        try
        {
            var request = PrepareManualRequest();
            var path = Urman.Studio.Core.Handoff.DeveloperContext.WriteHouseWindowRequest(_studio.Workspace.Root, request);
            var message = "Запрос сохранён. Передайте файл своему агенту, затем откройте его чистый JSON-ответ. Проект ещё не изменён.";
            _status.Text = _proposal is null ? message : _status.Text + "\n" + message;
            OS.ShellShowInFileManager(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            var message = "Запрос не сохранён: проверьте выбранный дом, описание и доступ к папке. Существующий файл не перезаписывается.";
            _status.Text = _proposal is null ? message : _status.Text + "\n" + message;
        }
    }

    private void OpenProposalFile()
    {
        if (_busy || _request is null || _proposal is not null || !Visible) return;
        var requestId = _request.RequestId;
        var picker = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem,
            Title = "Открыть JSON-ответ агента", UseNativeDialog = true, Filters = ["*.json ; Ответ агента"]
        };
        picker.FileSelected += path =>
        {
            picker.QueueFree();
            if (_busy || !Visible || _request?.RequestId != requestId)
            {
                _status.Text = "Ответ не открыт: запрос изменился или панель недоступна. Выберите файл для актуального запроса.";
                return;
            }
            _ = RunProposalAsync(fromManual: true, proposalFile: path);
        };
        picker.Canceled += picker.QueueFree;
        _studio.AddChild(picker);
        picker.PopupCentered(new Vector2I(900, 600));
    }

    private Task AskAsync() => RunProposalAsync(fromManual: false);

    private async Task RunProposalAsync(bool fromManual, string? proposalFile = null)
    {
        if (_busy || !Visible) return;
        HouseWindowProposal? imported = null;
        if (fromManual)
        {
            try
            {
                if (_request is null || _proposal is not null)
                    throw new InvalidOperationException("Сначала скопируйте новый запрос для агента.");
                if (_prompt.Text != _request.Prompt
                    || !RequestScopeMatchesCurrent(_request))
                    throw new InvalidOperationException("Описание или охват изменились. Скопируйте новый запрос.");
                if (_openedSelection != _studio.Selection
                    || _snapshot != Fingerprint(File.ReadAllText(Path.Combine(_studio.Workspace.Root, RecipePath))))
                    throw new InvalidOperationException("Выбор или исходник изменился. Скопируйте новый запрос.");
                imported = proposalFile is null
                    ? CodexProposalProvider.ParseProposal(DisplayServer.ClipboardGet(), _request)
                    : CodexProposalProvider.ParseProposalFile(proposalFile, _request);
            }
            catch (Exception error)
            {
                _status.Text = error is JsonException or CodexProposalProvider.CodexProposalException or IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException or NotSupportedException
                    ? "Ответ не принят. Нужен чистый JSON по схеме именно скопированного запроса. Проект не изменён."
                    : "Ответ не принят: " + error.Message;
                return;
            }
        }
        SetBusy(true);
        ClearProposal();
        Interlocked.Exchange(ref _providerWarningSeen, 0);
        _cancel = new();
        try
        {
            if (!fromManual) _request = Capture();
            var request = _request ?? throw new InvalidOperationException("Запрос ещё не подготовлен.");
            Urman.Studio.Core.Handoff.DeveloperContext.WriteHouseWindowJob(_studio.Workspace.Root, request);
            _stage = Directory.CreateTempSubdirectory("urman-house-" + request.RequestId + "-").FullName;
            File.WriteAllText(Path.Combine(_stage, "request.json"), JsonSerializer.Serialize(_request));
            _status.Text = fromManual ? "Проверяю перенесённое предложение…" : "Codex готовит предложение…";
            _proposal = imported ?? await new CodexProposalProvider(allowGlobalInstructions: _globalInstructions.ButtonPressed)
                .RunAsync(_request!, _stage, _cancel.Token, this);
            if (!Visible) throw new OperationCanceledException();
            File.WriteAllText(Path.Combine(_stage, "proposal.json"), JsonSerializer.Serialize(_proposal));
            Urman.Studio.Core.Handoff.DeveloperContext.WriteHouseWindowJob(_studio.Workspace.Root, request, _proposal);
            var recipe = JsonNode.Parse(_recipeText!)!.AsObject();
            if (_request.PartId is { } partId)
            {
                var widths = (JsonObject)_capturedWidths!.DeepClone();
                widths[partId] = _proposal.WindowWidth;
                recipe["windowWidths"] = widths;
            }
            else if (_request.PartIds is { } partIds)
            {
                var widths = (JsonObject)_capturedWidths!.DeepClone();
                foreach (var windowId in partIds) widths[windowId] = _proposal.WindowWidth;
                recipe["windowWidths"] = widths;
            }
            else
            {
                recipe["windowClearWidth"] = _proposal.WindowWidth;
                recipe.Remove("windowWidths");
            }
            recipe["modelPath"] = "res://assets/models/studio/hero-house-" + _request.RequestId + ".glb";
            File.WriteAllText(Path.Combine(_stage, "recipe.json"), recipe.ToJsonString(new() { WriteIndented = true }));
            _status.Text = "Создаю настоящий проём и согласованный интерьер…";
            await GenerateAsync(_stage, _cancel.Token);
            ValidateStage();
            ClearPreviewModels();
            _beforeModel = LoadCurrentHousePreview();
            _previewModels.AddChild(_beforeModel);
            _afterModel = LoadHousePreview(Path.Combine(_stage, "hero_house.glb"));
            _previewModels.AddChild(_afterModel);
            _beforeView.Disabled = false;
            _afterView.Disabled = false;
            ShowPreview(after: true);
            var scopeSummary = _request.PartId is not null ? "только окно " + _request.PartId
                : _request.PartIds is not null ? "7 окон главных комнат; окно в сенях и ширина по умолчанию сохранены"
                : "все 8 окон выбранного дома, включая окно в сенях";
            var widthSummary = _request.PartIds is not null
                ? $"Целевая ширина 7 окон главных комнат: {_proposal.WindowWidth:0.00} м."
                : $"Ширина: {_proposal.ExpectedWidth:0.00} → {_proposal.WindowWidth:0.00} м.";
            _status.Text = $"{_proposal.Summary}\n{widthSummary} {scopeSummary}. Ещё не применено.";
            if (Volatile.Read(ref _providerWarningSeen) != 0)
                _status.Text += "\nCodex сообщил предупреждение в сессии. Перед запросом проверено: MCP отключены, доступных MCP-инструментов нет.";
            if (_consentIoWarning is not null) _status.Text += "\n" + _consentIoWarning;
            if (fromManual) _status.Text += "\nОтвет перенесён вручную. Studio не управляет внешней сессией агента.";
            _apply.Disabled = false;
        }
        catch (OperationCanceledException) { ClearProposal(); _status.Text = "Остановлено. Проект не изменён."; }
        catch (Exception error) { ClearProposal(); _status.Text = "Не применено: " + error.Message; }
        finally { _cancel.Dispose(); _cancel = null; SetBusy(false); }
    }

    private async Task GenerateAsync(string stage, CancellationToken cancellation)
    {
        var blender = OperatingSystem.IsWindows()
            ? Path.Combine(_studio.Workspace.Root, ".tools", "blender", "windows", "blender-4.5.12-windows-x64", "blender.exe")
            : Path.Combine(_studio.Workspace.Root, ".tools/blender/Blender.app/Contents/MacOS/Blender");
        if (!File.Exists(blender)) throw new InvalidOperationException("Нет закреплённого Blender. Запрос и исходный дом сохранены.");
        var start = new ProcessStartInfo(blender) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = _studio.Workspace.Root };
        foreach (var argument in new[] { "--background", "--factory-startup", "--python", GeneratorPath, "--", "--root", _studio.Workspace.Root, "--component-only", "hero-house", "--recipe", Path.Combine(stage, "recipe.json"), "--output-dir", stage }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Blender не запустился.");
        var output = process.StandardOutput.ReadToEndAsync(cancellation);
        var errors = process.StandardError.ReadToEndAsync(cancellation);
        try { await process.WaitForExitAsync(cancellation); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        await output; await errors;
        if (process.ExitCode != 0) throw new IOException("Blender не завершил сборку; последняя принятая модель не изменена.");
    }

    private async Task DoctorAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var doctor = await new CodexProposalProvider().CheckAsync();
            var version = doctor.Version is { Length: > 0 } value ? $"Codex {value}" : "Codex";
            _status.Text = !doctor.ExecutableFound ? "Codex не найден. Установите приложение или CLI и войдите через ChatGPT."
                : doctor.LoginStatus switch
                {
                    CodexLoginStatus.LoggedIn => $"{version}: выполнен вход. Доступ к модели проверяется запросом; API-платежи не подключаются.",
                    CodexLoginStatus.NotLoggedIn => $"{version}: вход не выполнен. Войдите через ChatGPT в Codex.",
                    _ => $"{version}: статус входа неизвестен. Запрос проверит доступ к аккаунту и модели; API-платежи не подключаются."
                };
        }
        catch (Exception error) { _status.Text = "Codex не готов: " + error.Message; }
        finally { SetBusy(false); }
    }

    private async Task ApplyAsync()
    {
        if (_busy || _proposal is null || _request is null || _stage is null) return;
        SetBusy(true);
        try
        {
            if (_snapshot != Fingerprint(File.ReadAllText(Path.Combine(_studio.Workspace.Root, RecipePath))))
                throw new InvalidOperationException("Исходник изменился после запроса. Получите новое предложение.");
            ValidateStage();
            var model = "game/assets/models/studio/hero-house-" + _request.RequestId + ".glb";
            var blend = "assets/source/blender/studio/hero-house-" + _request.RequestId + ".blend";
            var manifest = model + ".manifest.json";
            var writes = new StudioFileWrite[] {
                new(RecipePath, AtomicFile.Sha256(System.Text.Encoding.UTF8.GetBytes(_recipeText!)), File.ReadAllBytes(Path.Combine(_stage, "recipe.json"))),
                new(model, null, File.ReadAllBytes(Path.Combine(_stage, "hero_house.glb"))),
                new(blend, null, File.ReadAllBytes(Path.Combine(_stage, "hero_house.blend"))),
                new(manifest, null, File.ReadAllBytes(Path.Combine(_stage, "manifest.json")))
            };
            await _studio.Session.ApplyFilesAsync("изменить окна дома через Codex", HeroHouseRecipe.EntityId, writes,
                new HashSet<string>(writes.Select(write => write.RelativePath), StringComparer.Ordinal), ImportAssets,
                () => ((StudioWorldSection)_studio.Section("world")).ReloadGeometryAsync());
            _status.Text = "Окна изменены. ⌘/Ctrl+Z вернёт исходные проёмы, рецепт и модель; повторить можно через Redo без Codex.";
            ClearProposal();
        }
        catch (Exception error)
        {
            _status.Text = "Не применено: " + error.Message;
        }
        finally { SetBusy(false); }
    }

    private void ImportAssets()
    {
        // The runtime uses the same native glTF parser; no partially imported cached resource is promoted.
        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(_studio.Workspace.Root, RecipePath)))!;
        if ((string?)recipe["modelPath"] is not { } modelPath) return;
        var fullPath = Path.Combine(_studio.Workspace.Root, "game", modelPath["res://".Length..]);
        if (new GltfDocument().AppendFromFile(fullPath, new GltfState()) != Error.Ok)
            throw new IOException("Импорт модели не прошёл; возвращаю предыдущую версию.");
    }

    private Node3D LoadCurrentHousePreview()
    {
        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(_studio.Workspace.Root, RecipePath)))?.AsObject()
            ?? throw new InvalidDataException("Рецепт эталонного дома не является JSON-объектом.");
        var modelPath = (string?)recipe["modelPath"];
        string fullPath;
        if (modelPath is null)
        {
            fullPath = Path.Combine(_studio.Workspace.Root, "game/assets/models/act1/urman_village_exterior_kit.glb");
        }
        else
        {
            const string prefix = "res://assets/models/studio/hero-house-";
            if (!modelPath.StartsWith(prefix, StringComparison.Ordinal)
                || !modelPath.EndsWith(".glb", StringComparison.Ordinal)
                || modelPath.AsSpan(prefix.Length).Contains('/')
                || modelPath.Contains("..", StringComparison.Ordinal)
                || modelPath.Contains('\\'))
                throw new InvalidDataException("В рецепте указан недопустимый путь текущей модели.");
            fullPath = Path.Combine(_studio.Workspace.Root, "game", modelPath["res://".Length..]);
        }

        return LoadHousePreview(fullPath);
    }

    private static Node3D LoadHousePreview(string path)
    {
        if (!File.Exists(path)) throw new IOException("Текущая или новая модель дома не найдена для предпросмотра.");
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromFile(path, state) != Error.Ok)
            throw new InvalidOperationException("Модель дома не прошла импорт предпросмотра.");
        if (document.GenerateScene(state) is not Node3D scene)
            throw new InvalidDataException("Модель дома не имеет 3D-корня.");

        var components = scene.Name == HeroHouseRecipe.Component
            ? new[] { scene }
            : scene.FindChildren(HeroHouseRecipe.Component, nameof(Node3D), true, false).OfType<Node3D>().ToArray();
        if (components.Length != 1)
        {
            scene.Free();
            throw new InvalidDataException("В модели не найден единственный компонент HeroHouse_TimberPlaster.");
        }

        var component = components[0];
        var importedBasis = ComposeImportedBasis(component);
        if (component != scene)
        {
            var parent = component.GetParent();
            if (parent is null)
            {
                scene.Free();
                throw new InvalidDataException("У компонента дома отсутствует импортированный родитель.");
            }
            parent.RemoveChild(component);
            scene.Free();
        }

        ClearSceneOwnership(component);
        component.Transform = new Transform3D(importedBasis, Vector3.Zero);
        component.Visible = false;
        return component;
    }

    private static Basis ComposeImportedBasis(Node3D component)
    {
        var basis = component.Transform.Basis;
        for (var ancestor = component.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (ancestor is Node3D node) basis = node.Transform.Basis * basis;
        }
        return basis;
    }

    private static void ClearSceneOwnership(Node node)
    {
        node.Owner = null;
        foreach (var child in node.GetChildren()) ClearSceneOwnership(child);
    }

    private void ShowPreview(bool after)
    {
        if (_beforeModel is null || _afterModel is null) return;
        _beforeModel.Visible = !after;
        _afterModel.Visible = after;
        _beforeView.Text = after ? "До" : "● До";
        _afterView.Text = after ? "● После" : "После";
        _previewCamera.Current = true;
    }

    private void ClearPreviewModels()
    {
        foreach (var child in _previewModels.GetChildren())
        {
            _previewModels.RemoveChild(child);
            child.QueueFree();
        }
        _beforeModel = null;
        _afterModel = null;
        _beforeView.Disabled = true;
        _afterView.Disabled = true;
        _beforeView.Text = "До";
        _afterView.Text = "После";
    }

    private void ValidateStage()
    {
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(_stage!, "manifest.json")))!;
        var allWindows = _request!.PartId is null && _request.PartIds is null;
        if (_request.PartIds is { } mainRoomPartIds
            && !mainRoomPartIds.SequenceEqual(HouseWindowProposalValidator.MainRoomWindowIds, StringComparer.Ordinal))
            throw new InvalidDataException("Запрос изменил канонический набор семи окон главных комнат.");
        if ((string?)manifest["schema"] != "hero-house-build-manifest/v1"
            || (string?)manifest["entityId"] != HeroHouseRecipe.EntityId
            || (string?)manifest["component"] != HeroHouseRecipe.Component
            || (string?)manifest["modelPath"] != "res://assets/models/studio/hero-house-" + _request!.RequestId + ".glb"
            || (decimal?)manifest["windowClearHeight"] != 1.40m
            || (decimal?)manifest["windowClearWidth"] != (allWindows ? _proposal!.WindowWidth : (decimal)JsonNode.Parse(_recipeText!)!["windowClearWidth"]!))
            throw new InvalidDataException("Сборка не соответствует принятому предложению.");
        if (manifest["windowWidths"] is not JsonObject widths || widths.Count != HeroHouseRecipe.WindowIds.Count)
            throw new InvalidDataException("Сборка не содержит точный охват восьми окон.");
        foreach (var id in HeroHouseRecipe.WindowIds)
        {
            var targeted = allWindows || _request.PartId == id
                || _request.PartIds?.Contains(id, StringComparer.Ordinal) == true;
            var expected = targeted
                ? _proposal!.WindowWidth : (decimal)_capturedWidths![id]!;
            if ((decimal?)widths[id] != expected)
                throw new InvalidDataException("Сборка изменила ширину вне принятого охвата: " + id);
        }
        foreach (var (key, name) in new[] { ("glb", "hero_house.glb"), ("blend", "hero_house.blend") })
            if ((string?)manifest["outputs"]?[key]?["sha256"] != AtomicFile.Sha256OfFile(Path.Combine(_stage!, name)))
                throw new InvalidDataException("Артефакт изменился после сборки: " + name);
        if ((string?)manifest["source"]?["recipe"]?["sha256"] != AtomicFile.Sha256OfFile(Path.Combine(_stage!, "recipe.json")))
            throw new InvalidDataException("Рецепт изменился после сборки.");
        foreach (var (key, path) in new[] { ("generator", GeneratorPath), ("baselineBlend", "assets/source/blender/act1/urman_village_exterior_kit.blend") })
            if ((string?)manifest["source"]?[key]?["sha256"] != AtomicFile.Sha256OfFile(Path.Combine(_studio.Workspace.Root, path)))
                throw new InvalidDataException("Исходник изменился после сборки: " + path);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _scope.Disabled = busy || _proposal is not null || _openedSelection != HeroHouseRecipe.EntityId;
        _ask.Disabled = busy || _openedSelection != HeroHouseRecipe.EntityId
            || _studio.Selection != _openedSelection;
        _copy.Disabled = _ask.Disabled;
        _saveRequest.Disabled = _ask.Disabled;
        _paste.Disabled = _ask.Disabled || _request is null || _proposal is not null;
        _importFile.Disabled = _paste.Disabled;
        _apply.Disabled = busy || _proposal is null || _request is null || _stage is null;
    }
    private void ClearProposal()
    {
        _proposal = null;
        _apply.Disabled = true;
        ClearPreviewModels();
        if (_stage is not null)
        {
            try { Directory.Delete(_stage, recursive: true); } catch (IOException) { }
            _stage = null;
        }
    }
}
