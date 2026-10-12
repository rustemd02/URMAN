using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Godot;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Play;
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
    private readonly Button _doctor;
    private readonly Button _stop;
    private readonly Button _apply;
    private readonly Button _copy;
    private readonly Button _paste;
    private readonly Button _disclosure;
    private readonly Button _saveRequest;
    private readonly Button _importFile;
    private readonly Button _beforeView;
    private readonly Button _afterView;
    private readonly Button _retryBuild;
    private readonly Button _restorePreview;
    private sealed record StageArtifacts(byte[] Recipe, byte[] Glb, byte[] Blend, byte[] Manifest);
    private sealed record StageArtifactHashes(string Recipe, string Glb, string Blend, string Manifest)
    {
        public static StageArtifactHashes From(StageArtifacts files) => new(
            AtomicFile.Sha256(files.Recipe), AtomicFile.Sha256(files.Glb),
            AtomicFile.Sha256(files.Blend), AtomicFile.Sha256(files.Manifest));
    }

    private readonly SubViewport _preview = new() { Size = new(560, 260), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible };
    private readonly Node3D _previewModels = new() { Name = "HouseCompareModels" };
    private readonly Camera3D _previewCamera = new() { Position = new(11, 7, 13), Current = true };
    private Node3D? _beforeModel;
    private Node3D? _afterModel;
    private CancellationTokenSource? _cancel;
    private HouseWindowRequest? _request;
    private HouseWindowProposal? _proposal;
    private AcceptDialog? _disclosureDialog;
    private ConfirmationDialog? _scopeConfirmationDialog;
    private Window? _rootPopup;
    private bool _rootPopupLeaseHeld;
    private bool _rootPopupOriginalExclusive;
    private int _rootPopupGeneration;
    private string? _stage;
    private StageArtifactHashes? _previewArtifactHashes;
    private string? _approvedScopeSnapshotHash;
    private bool? _approvedScopeGlobalInstructions;
    private string? _recipeText;
    private string? _sourceFingerprint;
    private bool? _disclosedGlobalInstructions;
    private JsonObject? _capturedWidths;
    private string? _consentIoWarning;
    private bool _busy;
    private bool _evidenceBusy;
    private bool _scopeConfirmationBusy;
    private bool _previewReady;
    private bool _retryBuildAvailable;
    private bool _previewFailureClearFailed;
    private int _providerWarningSeen;

    internal bool IsActionBusy => _busy || _evidenceBusy || _scopeConfirmationBusy
        || _fenceProposal is not null || HasTransientFencePreview || _studio.IsWorkspaceActionBusy;

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
        _prompt.TextChanged += () =>
        {
            if (IsActionBusy) return;
            if (_fenceRequest is { } fenceRequest && _prompt.Text != fenceRequest.Prompt)
            {
                if (!InvalidatePreparedRequest()) return;
                _status.Text = FenceManualReadyStatus("Описание изменилось; прежний запрос отозван.");
                SetBusy(false);
                return;
            }
            if (_request is not { } request || _prompt.Text == request.Prompt) return;
            if (!TryClearPreviewFailure(request))
            {
                ClearProposal();
                SetBusy(false);
                return;
            }
            ClearProposal();
            InvalidatePreparedRequest();
            _status.Text = "Описание изменилось; предыдущее предложение отозвано. Подготовьте новый запрос и получите новый ответ.";
            SetBusy(false);
        };
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
        _doctor = StudioRoot.Button(row, "Проверить Codex", () => _ = DoctorAsync());
        _stop = StudioRoot.Button(row, "Остановить", StopCurrentOperation);
        _apply = StudioRoot.Button(row, "Применить", () => _ = ApplyAsync());
        _apply.Disabled = true;
        var files = new HBoxContainer();
        body.AddChild(files);
        _disclosure = StudioRoot.Button(files, "Что уйдёт агенту…", OpenDisclosure);
        _disclosure.TooltipText = "Посмотреть состав сохранённого снимка и полный JSON запроса. Изображение камеры не отправляется.";
        var handoff = new HBoxContainer();
        body.AddChild(handoff);
        _copy = StudioRoot.Button(handoff, "Скопировать запрос для агента", CopyRequest);
        _copy.TooltipText = "Вставьте запрос в свой Codex или Claude. Это ручной перенос: Studio не управляет внешней сессией.";
        _paste = StudioRoot.Button(handoff, "Вставить JSON-ответ", () => _ = RunProposalAsync(fromManual: true));
        _paste.TooltipText = "Скопируйте чистый JSON ответа. Studio проверит его и покажет модель до применения.";
        _paste.Disabled = true;
        _saveRequest = StudioRoot.Button(files, "Сохранить запрос в файл", SaveRequest);
        _saveRequest.TooltipText = "Передайте текст своему агенту. Его внешней сессией Studio не управляет.";
        _importFile = StudioRoot.Button(files, "Открыть JSON-ответ…", OpenProposalFile);
        _importFile.TooltipText = "Проверить ответ по сохранённому запросу и показать модель до применения";
        _importFile.Disabled = true;
        var compare = new HBoxContainer();
        body.AddChild(compare);
        _beforeView = StudioRoot.Button(compare, "До", () => ShowPreview(after: false));
        _afterView = StudioRoot.Button(compare, "После", () => ShowPreview(after: true));
        _retryBuild = StudioRoot.Button(compare, "Повторить сборку", () => _ = RetryBuildAsync());
        _retryBuild.Disabled = true;
        _restorePreview = StudioRoot.Button(compare, "Восстановить…", OpenPreviewFailurePicker);
        _restorePreview.TooltipText = "Открыть только локальную запись о неудачном предпросмотре. Повторный запрос агенту и применение не выполняются.";
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
            if (Visible)
            {
                SetBusy(_busy);
                return;
            }
            DismissPendingScopeConfirmation();
            if (_disclosureDialog is { } disclosureDialog && GodotObject.IsInstanceValid(disclosureDialog))
            {
                _disclosureDialog = null;
                CloseRootPopup(disclosureDialog);
            }
            try { CloseActiveRootPopup(); }
            catch (Exception error)
            {
                try { if (GodotObject.IsInstanceValid(_studio)) _studio.ShowBanner("Не удалось закрыть диалог Studio: " + error.Message, error: true); }
                catch (Exception) { }
            }
            if (_fenceMode)
            {
                _cancel?.Cancel();
                if (InvalidatePreparedRequest()) _fenceMode = false;
            }
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
        TreeExiting += HandlePanelTreeExiting;
    }

    private void ShowRootPopup(Window popup, Action open)
    {
        if (_rootPopup is not null)
        {
            if (_rootPopup != popup && GodotObject.IsInstanceValid(popup)) popup.Free();
            throw new InvalidOperationException("Сначала закройте текущее окно Studio.");
        }

        if (!_rootPopupLeaseHeld)
        {
            _rootPopupOriginalExclusive = Exclusive;
            _rootPopupLeaseHeld = true;
        }
        _rootPopup = popup;
        _rootPopupGeneration++;
        popup.VisibilityChanged += () =>
        {
            if (!popup.Visible) ReleaseRootPopup(popup);
        };
        popup.TreeExiting += () => ReleaseRootPopup(popup);

        try
        {
            Exclusive = false;
            popup.Exclusive = true;
            _studio.AddChild(popup);
            open();
        }
        catch
        {
            CloseRootPopup(popup);
            throw;
        }
    }

    private void CloseRootPopup(Window popup)
    {
        if (GodotObject.IsInstanceValid(popup))
        {
            if (popup.Visible) popup.Hide();
            popup.QueueFree();
        }
        ReleaseRootPopup(popup);
    }

    private void CloseActiveRootPopup()
    {
        if (_rootPopup is { } popup) CloseRootPopup(popup);
    }

    private void ReleaseRootPopup(Window popup)
    {
        if (_rootPopup != popup) return;
        _rootPopup = null;
        var generation = ++_rootPopupGeneration;
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(this) || generation != _rootPopupGeneration
                || _rootPopup is not null || !_rootPopupLeaseHeld)
                return;

            _rootPopupLeaseHeld = false;
            if (IsInsideTree()) Exclusive = _rootPopupOriginalExclusive;
        }).CallDeferred();
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
            ? "\nОхват изменился после снимка. Откройте «Что уйдёт агенту…», чтобы обновить запрос перед передачей ответа."
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
        var worldSection = (StudioWorldSection)_studio.Section("world");
        var fenceSelected = worldSection.HasFenceSelection;
        worldSection.SuspendForAi();
        if (IsActionBusy)
        {
            PopupCenteredClamped(new(600, 560), 0.9f);
            _prompt.GrabFocus();
            return;
        }
        if (_previewFailureClearFailed && _request is { } retained && !TryClearPreviewFailure(retained))
        {
            PopupCenteredClamped(new(600, 560), 0.9f);
            _prompt.GrabFocus();
            return;
        }
        var staleReason = (string?)null;
        var currentPart = ((StudioWorldSection)_studio.Section("world")).SelectionPart;
        var preservePreparedState = _request is null && selected == HeroHouseRecipe.EntityId
            && _openedSelection == selected && _studio.Selection == selected
            && (_scope.Selected != SelectedWindowScope || _openedPart?.PartId == currentPart?.PartId);
        if (_request is { } existingRequest)
        {
            _openedPart = currentPart;
            try
            {
                if (!string.Equals(selected, _openedSelection, StringComparison.Ordinal))
                    throw new InvalidOperationException("Выбран другой объект.");
                EnsurePreparedRequestCurrent(existingRequest);
                preservePreparedState = true;
            }
            catch (Exception error)
            {
                staleReason = error.Message;
            }
        }

        if (!preservePreparedState)
        {
            ClearProposal();
            InvalidatePreparedRequest();
            _disclosedGlobalInstructions = null;
            _openedSelection = selected;
        }
        _fenceMode = fenceSelected;
        Title = _fenceMode ? "Предложить выпрямление забора" : "Изменить окна выбранного дома";
        _openedPart = currentPart;
        var windowSelected = _openedPart is { PartId: var partId }
            && HeroHouseRecipe.WindowIds.Contains(partId, StringComparer.Ordinal);
        _scope.Visible = !_fenceMode && selected == HeroHouseRecipe.EntityId;
        _scope.SetItemDisabled(SelectedWindowScope, !windowSelected);
        if (!preservePreparedState)
            _scope.Select(windowSelected ? SelectedWindowScope : MainRoomWindowsScope);
        SetBusy(false);
        if (!preservePreparedState && staleReason is not null)
        {
            _status.Text = "Предыдущий снимок снят: " + staleReason + " Подготовьте новый запрос.";
        }
        else if (!preservePreparedState && selected == HeroHouseRecipe.EntityId)
            UpdateScopeStatus();
        else if (!preservePreparedState && _fenceMode)
            _status.Text = FenceManualReadyStatus();
        else if (!preservePreparedState)
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
        var worldAddress = _studio.Workspace.Locate(HeroHouseRecipe.EntityId);
        if (worldAddress is null)
            throw new InvalidOperationException("Источник дома не найден или его ID повторяется. Исправьте идентичность перед запросом.");
        if (string.IsNullOrWhiteSpace(_prompt.Text)) throw new InvalidOperationException("Опишите изменение окон.");
        var worldSource = _studio.Workspace.File(worldAddress.RelativePath).CaptureSource();
        var worldEntity = _studio.Workspace.Get(HeroHouseRecipe.EntityId)
            ?? throw new InvalidOperationException("Источник выбранного дома больше не доступен.");
        var worldBaseHash = worldSource.BaseSha256;
        StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, RecipePath);
        var recipeFile = AuthoredFile.Load(_studio.Workspace.Root, RecipePath);
        var recipeSource = recipeFile.CaptureSource();
        var recipeBaseHash = recipeSource.BaseSha256
            ?? throw new InvalidDataException("У рецепта отсутствует сохранённая базовая ревизия.");
        _recipeText = recipeSource.Text;
        var parsedRecipe = HeroHouseRecipe.Parse(_recipeText);
        var recipe = JsonNode.Parse(_recipeText)!.AsObject();
        if (recipe["modelPath"] is null && ((decimal)recipe["windowClearWidth"]! != 1.06m
            || recipe["windowWidths"] is JsonObject existingWidths && existingWidths.Count > 0))
            throw new InvalidOperationException("Рецепт изменён без согласованной модели. Сначала восстановите принятый исходный дом.");
        _capturedWidths = new JsonObject();
        foreach (var id in HeroHouseRecipe.WindowIds)
            _capturedWidths[id] = (decimal?)recipe["windowWidths"]?[id]
                ?? (id == "SeniOuter0" && recipe["modelPath"] is null ? .80m : (decimal)recipe["windowClearWidth"]!);
        var (partId, partIds) = SelectedScope();
        var sourceFingerprint = Fingerprint(_recipeText);
        var currentWidth = partId is null ? (decimal)recipe["windowClearWidth"]! : (decimal)_capturedWidths[partId]!;
        var scopedIds = partId is not null
            ? new[] { partId }
            : partIds?.ToArray() ?? HeroHouseRecipe.WindowIds.ToArray();
        var currentWidths = scopedIds.ToDictionary(
            id => id, id => (decimal)_capturedWidths[id]!, StringComparer.Ordinal);
        var request = new HouseWindowRequest(Guid.NewGuid().ToString("N"), sourceFingerprint, HeroHouseRecipe.EntityId,
            currentWidth, _prompt.Text, partId)
        {
            PartIds = partIds,
            CurrentWidths = currentWidths,
            Sources = new[]
            {
                new HouseWindowSource(worldAddress.RelativePath, worldAddress.Key, worldBaseHash,
                    worldSource.Sha256, JsonSerializer.SerializeToElement(worldEntity)),
                new HouseWindowSource(RecipePath, "recipe", recipeBaseHash,
                    recipeSource.Sha256, JsonSerializer.SerializeToElement(JsonNode.Parse(_recipeText)!))
            }
        };
        var worldSection = (StudioWorldSection)_studio.Section("world");
        var playStart = worldSection.PlayStart()
            ?? throw new InvalidOperationException("Не удалось определить текущую точку входа выбранного мира.");
        var context = CampaignWorldCatalog.Discover(_studio.Workspace).Resolve(
            PlayPreparation.Campaign,
            "res://scenes/act1_demo.tscn",
            playStart.Zone,
            playStart.Spawn,
            baseRevision: Revision());
        var recipeModelPath = parsedRecipe.ModelPath ?? "res://assets/models/act1/urman_village_exterior_kit.glb";
        var modelDependencyPath = "game/" + recipeModelPath["res://".Length..];
        var dependencyHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var dependencyPaths = new[]
        {
            GeneratorPath,
            "assets/source/blender/act1/urman_village_exterior_kit.blend",
            modelDependencyPath
        }.Concat(context.AuthoredDependencies).Distinct(StringComparer.Ordinal);
        foreach (var dependencyPath in dependencyPaths)
        {
            StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, dependencyPath);
            dependencyHashes.Add(dependencyPath, AtomicFile.Sha256OfFile(Path.Combine(
                _studio.Workspace.Root, dependencyPath.Replace('/', Path.DirectorySeparatorChar))));
        }
        var environment = new HouseAuthoringEnvironment(
            context.BaseRevision,
            context.Campaign.Id,
            context.Campaign.ExactVersion,
            context.World.WorldId,
            context.StateId,
            context.World.ScenePath,
            context.World.ZoneId,
            context.World.SpawnPointId,
            worldSection.CaptureAuthoringCamera(),
            dependencyHashes);
        request = request with { Environment = environment };
        request = request with { SnapshotHash = AuthoringRequestV2.ComputeSnapshotHash(request) };
        _sourceFingerprint = sourceFingerprint;
        return request;
    }

    private string Fingerprint(string recipe)
    {
        var world = _studio.Workspace.Locate(HeroHouseRecipe.EntityId)!;
        var current = HeroHouseRecipe.Parse(recipe);
        var modelPath = current.ModelPath ?? "res://assets/models/act1/urman_village_exterior_kit.glb";
        var modelHash = AtomicFile.Sha256OfFile(Path.Combine(_studio.Workspace.Root, "game", modelPath["res://".Length..]));
        var source = File.ReadAllBytes(Path.Combine(_studio.Workspace.Root, GeneratorPath));
        return AtomicFile.Sha256(System.Text.Encoding.UTF8.GetBytes(recipe + "\n" +
            _studio.Workspace.File(world.RelativePath).CaptureSource().Text + "\n" + AtomicFile.Sha256(source) + "\n" + modelHash + "\n" +
            AtomicFile.Sha256OfFile(Path.Combine(_studio.Workspace.Root, "assets/source/blender/act1/urman_village_exterior_kit.blend")) + "\n" + Revision()));
    }

    private string Revision()
    {
        const string snapshotRevisionPrefix = "--urman-studio-snapshot-revision=";
        var snapshotRevisionArgument = OS.GetCmdlineUserArgs()
            .LastOrDefault(argument => argument.StartsWith(snapshotRevisionPrefix, StringComparison.Ordinal));
        if (snapshotRevisionArgument is not null)
        {
            var snapshotRevision = snapshotRevisionArgument[snapshotRevisionPrefix.Length..];
            if ((snapshotRevision.Length != 40 && snapshotRevision.Length != 64)
                || !snapshotRevision.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
                throw new IOException("Переданная ревизия снимка должна содержать 40 или 64 строчные шестнадцатеричные цифры.");

            // Это заявление оператора или проверившего provenance runner; Studio не аутентифицирует сам аргумент командной строки.
            var checkoutRoot = _studio.Workspace.Root;
            var checkoutRevision = Urman.Studio.Core.Handoff.DeveloperContext.ResolveRevision(checkoutRoot);
            if (checkoutRevision == "неизвестна"
                && (Directory.Exists(Path.Combine(checkoutRoot, ".git")) || File.Exists(Path.Combine(checkoutRoot, ".git"))))
                throw new IOException("Не удалось проверить Git HEAD рабочего проекта перед сравнением ревизии снимка.");
            if (checkoutRevision != "неизвестна" && checkoutRevision != snapshotRevision)
                throw new IOException("Переданная ревизия снимка не совпадает с Git HEAD рабочего проекта.");
            return snapshotRevision;
        }

        var revision = Urman.Studio.Core.Handoff.DeveloperContext.ResolveRevision(_studio.Workspace.Root);
        if (revision == "неизвестна") throw new IOException("Нет полной Git-ревизии. Проверьте checkout перед запросом.");
        return revision;
    }

    private void ValidateCapturedDependencies()
    {
        var dependencies = _request?.Environment?.DependencyHashes
            ?? throw new InvalidOperationException("В запросе отсутствует снимок зависимостей кампании.");
        foreach (var (relativePath, expectedHash) in dependencies)
        {
            StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, relativePath);
            var fullPath = Path.Combine(_studio.Workspace.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                throw new InvalidOperationException("Зависимость запроса больше недоступна: " + relativePath);
            if (!string.Equals(AtomicFile.Sha256OfFile(fullPath), expectedHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Зависимость запроса изменилась после снимка: " + relativePath);
        }
    }

    private HouseWindowRequest PrepareRequest(bool checkProviderPolicy = false)
    {
        if (_request is null)
            _request = Capture();
        else
            EnsurePreparedRequestCurrent(_request, checkProviderPolicy);
        var request = _request ?? throw new InvalidOperationException("Запрос ещё не подготовлен.");
        HouseWindowProposalValidator.ValidateRequest(request);
        SetBusy(_busy);
        return request;
    }

    private void EnsurePreparedRequestCurrent(HouseWindowRequest request, bool checkProviderPolicy = false)
    {
        if (_openedSelection != _studio.Selection
            || _prompt.Text != request.Prompt
            || !RequestScopeMatchesCurrent(request))
            throw new InvalidOperationException("Описание, охват или выбор изменились. Откройте «Что уйдёт агенту…», чтобы обновить снимок.");
        if (request.PartId is { } partId
            && ((StudioWorldSection)_studio.Section("world")).SelectionPart?.PartId != partId)
            throw new InvalidOperationException("Выбор окна изменился после снимка. Подготовьте новый запрос для текущего окна.");
        if (_sourceFingerprint is null
            || _sourceFingerprint != Fingerprint(File.ReadAllText(Path.Combine(_studio.Workspace.Root, RecipePath))))
            throw new InvalidOperationException("Исходник изменился после снимка. Откройте «Что уйдёт агенту…», чтобы получить актуальный запрос.");
        ValidateCapturedDependencies();
        if (checkProviderPolicy && _proposal is null && _disclosedGlobalInstructions is bool disclosed
            && disclosed != _globalInstructions.ButtonPressed)
            throw new InvalidOperationException("Политика глобальных инструкций Codex изменилась. Откройте «Что уйдёт агенту…» ещё раз.");
    }

    private void OpenDisclosure()
    {
        if (_fenceMode)
        {
            OpenFenceDisclosure();
            return;
        }
        if (IsActionBusy || _openedSelection != HeroHouseRecipe.EntityId || _studio.Selection != _openedSelection)
            return;
        try
        {
            var refreshed = false;
            if (_proposal is null)
            {
                if (_request is null)
                {
                    _request = Capture();
                }
                else
                {
                    try { EnsurePreparedRequestCurrent(_request); }
                    catch (InvalidOperationException)
                    {
                        InvalidatePreparedRequest();
                        _request = Capture();
                        refreshed = true;
                    }
                }
                SetBusy(_busy);
            }
            var request = _request ?? throw new InvalidOperationException("Сначала подготовьте запрос для выбранного дома.");
            HouseWindowProposalValidator.ValidateRequest(request);
            var globalInstructions = _globalInstructions.ButtonPressed ? "включены" : "выключены";
            var disclosureText = (refreshed ? "Снимок обновлён; прежний ответ больше не подходит.\n\n" : string.Empty)
                + "Модель получает данные ниже и предлагает JSON; файлы проекта меняет Studio только после нажатия «Применить».\n"
                + "Камера передаётся только числовыми параметрами (проекция, положение, поворот, поле зрения, размер и viewport); снимок или изображение не отправляется.\n"
                + $"Глобальные инструкции для прямого запуска из Studio сейчас {globalInstructions}. Их текст не входит в JSON или snapshot hash. При ручной передаче Studio не знает и не меняет политику инструкций внешнего агента.\n\n"
                + AuthoringRequestV2.Describe(request)
                + "\n\nПолный канонический JSON запроса:\n"
                + CodexProposalProvider.SerializeRequest(request);

            ShowDisclosure(disclosureText, rememberGlobalInstructions: _proposal is null);
            if (refreshed) _status.Text = "Снимок обновлён; прежний ответ больше не подходит.";
        }
        catch (Exception error)
        {
            _status.Text = "Не удалось подготовить раскрытие запроса: " + error.Message;
        }
    }

    private void ShowDisclosure(string disclosureText, bool rememberGlobalInstructions)
    {
        var dialog = new AcceptDialog
        {
            Title = "Что уйдёт агенту",
            OkButtonText = "Закрыть",
            MinSize = new Vector2I(700, 480)
        };
        var body = new VBoxContainer();
        dialog.AddChild(body);
        var contents = new TextEdit
        {
            Editable = false,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 420),
            Text = disclosureText
        };
        body.AddChild(contents);
        _disclosureDialog = dialog;
        if (rememberGlobalInstructions) _disclosedGlobalInstructions = _globalInstructions.ButtonPressed;
        dialog.Confirmed += () =>
        {
            if (_disclosureDialog == dialog) _disclosureDialog = null;
            CloseRootPopup(dialog);
        };
        dialog.VisibilityChanged += () =>
        {
            if (dialog.Visible) return;
            if (_disclosureDialog == dialog) _disclosureDialog = null;
            CloseRootPopup(dialog);
        };
        ShowRootPopup(dialog, () => dialog.PopupCenteredClamped(new Vector2I(1040, 720), 0.9f));
    }

    private bool InvalidatePreparedRequest()
    {
        if ((_fenceRequest is not null || _fenceProposal is not null || HasTransientFencePreview)
            && !TryClearFencePreviewBeforeForget())
            return false;
        _request = null;
        _fenceRequest = null;
        _previewArtifactHashes = null;
        _approvedScopeSnapshotHash = null;
        _approvedScopeGlobalInstructions = null;
        _sourceFingerprint = null;
        _disclosedGlobalInstructions = null;
        DismissPendingScopeConfirmation();
        if (_disclosureDialog is { } dialog && GodotObject.IsInstanceValid(dialog))
        {
            _disclosureDialog = null;
            CloseRootPopup(dialog);
        }
        return true;
    }

    private void RequireScopeConfirmation(HouseWindowRequest request, string actionText, bool directProvider,
        Action<HouseWindowRequest> dispatch)
    {
        var globalInstructions = _globalInstructions.ButtonPressed;
        RequireScopeConfirmationCore(request.SnapshotHash,
            ScopeConfirmationText(request, actionText, directProvider, globalInstructions), directProvider,
            globalInstructions,
            confirmedGlobalInstructions => DispatchScopeApproval(request, directProvider, confirmedGlobalInstructions, dispatch));
    }

    private void RequireScopeConfirmationCore(string snapshotHash, string dialogText, bool directProvider,
        bool globalInstructions, Action<bool> dispatch, Action? dismissed = null)
    {
        if (string.Equals(_approvedScopeSnapshotHash, snapshotHash, StringComparison.Ordinal)
            && (!directProvider || _approvedScopeGlobalInstructions == globalInstructions))
        {
            dispatch(globalInstructions);
            return;
        }
        if (_scopeConfirmationDialog is not null) return;

        var dialog = new ConfirmationDialog
        {
            Title = "Проверьте состав запроса",
            DialogText = dialogText,
            OkButtonText = "Продолжить",
            CancelButtonText = "Отмена",
            MinSize = new Vector2I(620, 0)
        };
        dialog.GetLabel().AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _scopeConfirmationDialog = dialog;
        _scopeConfirmationBusy = true;

        void Confirm()
        {
            if (_scopeConfirmationDialog != dialog) return;
            _scopeConfirmationDialog = null;
            _scopeConfirmationBusy = false;
            if (GodotObject.IsInstanceValid(dialog)) CloseRootPopup(dialog);
            if (!GodotObject.IsInstanceValid(this)) return;
            SetBusy(_busy);
            dispatch(globalInstructions);
        }

        void Dismiss()
        {
            if (_scopeConfirmationDialog != dialog) return;
            DismissScopeConfirmation(dialog);
            dismissed?.Invoke();
        }
        dialog.Confirmed += Confirm;
        dialog.Canceled += Dismiss;
        dialog.CloseRequested += Dismiss;
        dialog.TreeExiting += Dismiss;
        dialog.VisibilityChanged += () =>
        {
            if (dialog.Visible) return;
            Callable.From(() =>
            {
                if (_scopeConfirmationDialog == dialog && GodotObject.IsInstanceValid(dialog) && !dialog.Visible)
                    Dismiss();
            }).CallDeferred();
        };

        try
        {
            SetBusy(_busy);
            ShowRootPopup(dialog, () => dialog.PopupCentered(new Vector2I(620, 400)));
        }
        catch
        {
            DismissScopeConfirmation(dialog);
            throw;
        }
    }

    private void DispatchScopeApproval(HouseWindowRequest request, bool directProvider, bool globalInstructions,
        Action<HouseWindowRequest> dispatch)
    {
        if (!GodotObject.IsInstanceValid(this)) return;
        if (!Visible || IsActionBusy || !ReferenceEquals(_request, request)
            || !string.Equals(_request?.SnapshotHash, request.SnapshotHash, StringComparison.Ordinal))
        {
            _status.Text = "Запрос не передан: снимок изменился или панель закрыта. Подготовьте актуальный запрос.";
            return;
        }

        try
        {
            EnsurePreparedRequestCurrent(request, checkProviderPolicy: directProvider);
            if (directProvider && _globalInstructions.ButtonPressed != globalInstructions)
                throw new InvalidOperationException("Настройка глобальных инструкций Codex изменилась во время подтверждения.");
            var sameApproval = string.Equals(_approvedScopeSnapshotHash, request.SnapshotHash, StringComparison.Ordinal);
            _approvedScopeSnapshotHash = request.SnapshotHash;
            if (directProvider) _approvedScopeGlobalInstructions = globalInstructions;
            else if (!sameApproval) _approvedScopeGlobalInstructions = null;
            dispatch(request);
        }
        catch (Exception error)
        {
            _status.Text = "Запрос не передан: " + error.Message;
        }
    }

    private static string ScopeConfirmationText(HouseWindowRequest request, string actionText,
        bool directProvider, bool globalInstructions)
    {
        IReadOnlyList<string> targets = request.PartId is { } partId
            ? new[] { partId }
            : request.PartIds ?? HeroHouseRecipe.WindowIds;
        var excluded = HeroHouseRecipe.WindowIds.Where(id => !targets.Contains(id, StringComparer.Ordinal)).ToArray();
        var targetLabel = targets.Count switch
        {
            1 => "одно выбранное окно",
            7 => "ровно 7 окон главных комнат",
            8 => "все 8 окон, включая окно в сенях",
            _ => throw new InvalidOperationException("Снимок содержит неподдерживаемый охват окон.")
        };
        return actionText + "\n\n"
            + $"Охват: {targetLabel}.\nЦелевые окна ({targets.Count}): {string.Join(", ", targets.Select(WindowLabel))}.\n"
            + $"Исключённые окна ({excluded.Length}): {(excluded.Length == 0 ? "нет" : string.Join(", ", excluded.Select(WindowLabel)))}.\n"
            + "Выбранный эталонный дом: один экземпляр.\nПолитика: индивидуальный вариант; общий ассет не меняется."
            + (directProvider
                ? $"\nГлобальные инструкции Codex в прямой передаче: {(globalInstructions ? "включены" : "выключены")}."
                : "\nКопирование и сохранение готовят запрос для ручной передачи; внешней сессией агента Studio не управляет.");
    }

    private static string WindowLabel(string id) => id switch
    {
        "Front0" => "уличное окно 1",
        "Front1" => "уличное окно 2",
        "Front2" => "уличное окно 3",
        "Rear0" => "заднее окно 1",
        "Rear1" => "заднее окно 2",
        "Left0" => "левое окно",
        "Right0" => "правое окно",
        "SeniOuter0" => "окно в сенях",
        _ => throw new InvalidOperationException("Снимок содержит неизвестное окно.")
    };

    private void DismissPendingScopeConfirmation()
    {
        if (_scopeConfirmationDialog is { } dialog) DismissScopeConfirmation(dialog);
    }

    private void HandlePanelTreeExiting()
    {
        try { DismissPendingScopeConfirmation(); }
        catch (Exception) { }
        try { CloseActiveRootPopup(); }
        catch (Exception error)
        {
            try { if (GodotObject.IsInstanceValid(_studio)) _studio.ShowBanner("Не удалось закрыть диалог Studio при закрытии панели: " + error.Message, error: true); }
            catch (Exception) { }
        }

        if (!_fenceMode && _fenceRequest is null && _fenceProposal is null && !HasTransientFencePreview) return;
        _cancel?.Cancel();
        try
        {
            if (InvalidatePreparedRequest()) return;
            if (GodotObject.IsInstanceValid(_studio)) _studio.ShowBanner(_status.Text, error: true);
        }
        catch (Exception error)
        {
            try
            {
                if (GodotObject.IsInstanceValid(_studio))
                    _studio.ShowBanner("Не удалось безопасно снять предпросмотр забора при закрытии панели: " + error.Message, error: true);
            }
            catch (Exception) { }
        }
    }

    private void DismissScopeConfirmation(ConfirmationDialog dialog)
    {
        if (_scopeConfirmationDialog != dialog) return;
        _scopeConfirmationDialog = null;
        _scopeConfirmationBusy = false;
        if (GodotObject.IsInstanceValid(dialog)) CloseRootPopup(dialog);
        if (GodotObject.IsInstanceValid(this) && IsInsideTree()) SetBusy(_busy);
    }

    private void CopyRequest()
    {
        if (IsActionBusy || !Visible) return;
        if (_fenceMode)
        {
            CopyFenceRequest();
            return;
        }
        try
        {
            var request = PrepareRequest();
            RequireScopeConfirmation(request,
                "Studio скопирует этот запрос для ручной передачи агенту.", directProvider: false,
                confirmedRequest =>
                {
                    DisplayServer.ClipboardSet(CodexProposalProvider.CreateClipboardPrompt(confirmedRequest));
                    if (_proposal is null) _status.Text = "Вставьте запрос в свой агент, затем скопируйте JSON ответа и нажмите «Вставить JSON-ответ». Studio покажет модель до применения. Внешняя сессия работает отдельно.";
                });
        }
        catch (Exception error) { _status.Text = error.Message; }
    }

    private void SaveRequest()
    {
        if (IsActionBusy || !Visible) return;
        if (_fenceMode)
        {
            SaveFenceRequest();
            return;
        }
        try
        {
            var request = PrepareRequest();
            RequireScopeConfirmation(request,
                "Studio сохранит этот запрос локально для ручной передачи агенту.", directProvider: false,
                confirmedRequest =>
                {
                    var path = Urman.Studio.Core.Handoff.DeveloperContext.WriteHouseWindowRequest(_studio.Workspace.Root, confirmedRequest);
                    var message = "Запрос сохранён. Передайте файл своему агенту, затем откройте его чистый JSON-ответ. Проект ещё не изменён.";
                    _status.Text = _proposal is null ? message : _status.Text + "\n" + message;
                    OS.ShellShowInFileManager(path);
                });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            var message = "Запрос не сохранён: " + error.Message + " Существующий файл не перезаписывается; если снимок устарел, обновите его через «Что уйдёт агенту…».";
            _status.Text = _proposal is null ? message : _status.Text + "\n" + message;
        }
    }

    private void OpenProposalFile()
    {
        if (_fenceMode)
        {
            OpenFenceProposalFile();
            return;
        }
        if (IsActionBusy || _request is null || _proposal is not null || !Visible) return;
        var requestId = _request.RequestId;
        var picker = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem,
            Title = "Открыть JSON-ответ агента", UseNativeDialog = true, Filters = ["*.json ; Ответ агента"]
        };
        picker.FileSelected += path =>
        {
            CloseRootPopup(picker);
            if (IsActionBusy || !Visible || _request?.RequestId != requestId)
            {
                _status.Text = "Ответ не открыт: запрос изменился или панель недоступна. Выберите файл для актуального запроса.";
                return;
            }
            _ = RunProposalAsync(fromManual: true, proposalFile: path);
        };
        picker.Canceled += () => CloseRootPopup(picker);
        ShowRootPopup(picker, () => picker.PopupCentered(new Vector2I(900, 600)));
    }

    private void OpenPreviewFailurePicker()
    {
        if (IsActionBusy || !Visible || _openedSelection != HeroHouseRecipe.EntityId || _studio.Selection != _openedSelection
            || _request is not null || _proposal is not null || _previewFailureClearFailed)
            return;
        var jobsDirectory = Path.Combine(_studio.Workspace.Root, ".urman-studio", "ai-jobs");
        if (!Directory.Exists(jobsDirectory))
        {
            _status.Text = "Локальных записей о неудачном предпросмотре нет.";
            return;
        }

        var picker = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            CurrentDir = jobsDirectory,
            Title = "Восстановить локальный предпросмотр",
            UseNativeDialog = true,
            Filters = ["preview-failed.json ; Запись неудачного предпросмотра"]
        };
        picker.FileSelected += path =>
        {
            CloseRootPopup(picker);
            RestorePreviewFailure(path);
        };
        picker.Canceled += () => CloseRootPopup(picker);
        ShowRootPopup(picker, () => picker.PopupCentered(new Vector2I(900, 600)));
    }

    private void RestorePreviewFailure(string selectedPath)
    {
        if (IsActionBusy || !Visible || _openedSelection != HeroHouseRecipe.EntityId || _studio.Selection != _openedSelection
            || _request is not null || _proposal is not null || _previewFailureClearFailed)
            return;

        var previousPrompt = _prompt.Text;
        var previousScope = _scope.Selected;
        var previousPart = _openedPart;
        try
        {
            var requestId = PreviewFailureRequestId(selectedPath);
            var (request, proposal) = Urman.Studio.Core.Handoff.DeveloperContext.ReadHouseWindowPreviewFailure(
                _studio.Workspace.Root, requestId);
            HouseWindowProposalValidator.ValidateRequest(request);
            HouseWindowProposalValidator.Validate(request, proposal);
            var savedEnvironment = request.Environment
                ?? throw new InvalidDataException("В сохранённом запросе нет контекста кампании.");
            if (request.RequestId != requestId || request.EntityId != HeroHouseRecipe.EntityId || request.Sources is null)
                throw new InvalidDataException("Запись не содержит полный снимок поддерживаемого запроса дома.");

            _openedPart = ((StudioWorldSection)_studio.Section("world")).SelectionPart;
            if (request.PartId is { } partId)
            {
                if (_openedPart?.PartId != partId)
                    throw new InvalidOperationException("Для этого запроса выберите то же окно, которое было выбрано при сохранении.");
                _scope.Select(SelectedWindowScope);
            }
            else if (request.PartIds is not null)
            {
                _scope.Select(MainRoomWindowsScope);
            }
            else
            {
                _scope.Select(AllWindowsScope);
            }

            _prompt.Text = request.Prompt;
            var candidate = Capture();
            var candidateEnvironment = candidate.Environment
                ?? throw new InvalidOperationException("Не удалось проверить текущий контекст Studio.");
            candidate = candidate with
            {
                RequestId = request.RequestId,
                Environment = candidateEnvironment with { Camera = savedEnvironment.Camera }
            };
            if (!string.Equals(AuthoringRequestV2.ComputeSnapshotHash(candidate), request.SnapshotHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Текущие исходники, ширины или контекст проекта отличаются от сохранённого снимка.");

            ClearProposal();
            _request = request;
            _proposal = proposal;
            _disclosedGlobalInstructions = null;
            EnsurePreparedRequestCurrent(request);
            _previewReady = false;
            _retryBuildAvailable = true;
            SetBusy(false);
            _status.Text = "Восстановлены проверенный запрос и предложение для локальной сборки. Агент не запускался; проект не изменён. Нажмите «Повторить сборку», затем осмотрите модель перед отдельным применением.";
        }
        catch (Exception error)
        {
            _request = null;
            _proposal = null;
            _sourceFingerprint = null;
            _recipeText = null;
            _capturedWidths = null;
            _previewReady = false;
            _retryBuildAvailable = false;
            ClearBuildStage();
            _openedPart = previousPart;
            _scope.Select(previousScope);
            _prompt.Text = previousPrompt;
            SetBusy(false);
            _status.Text = "Восстановление отклонено: " + error.Message + " Сохранённая запись и файлы проекта не изменены.";
        }
    }

    private string PreviewFailureRequestId(string selectedPath)
    {
        var root = Path.GetFullPath(_studio.Workspace.Root);
        var path = Path.GetFullPath(selectedPath);
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        var segments = relative.Split('/');
        if (segments.Length != 4 || segments[0] != ".urman-studio" || segments[1] != "ai-jobs"
            || segments[3] != "preview-failed.json"
            || !Guid.TryParseExact(segments[2], "N", out var parsed)
            || !string.Equals(parsed.ToString("N"), segments[2], StringComparison.Ordinal))
            throw new InvalidOperationException("Выберите только preview-failed.json из .urman-studio/ai-jobs/{32-символьный ID}.");
        return segments[2];
    }

    private bool TryClearPreviewFailure(HouseWindowRequest request)
    {
        try
        {
            Urman.Studio.Core.Handoff.DeveloperContext.ClearHouseWindowPreviewFailure(_studio.Workspace.Root, request.RequestId);
            _previewFailureClearFailed = false;
            return true;
        }
        catch (Exception error)
        {
            _previewFailureClearFailed = true;
            _status.Text = "Действие остановлено: не удалось отозвать запись локального предпросмотра. Исправьте доступ к файлу и повторите действие. " + error.Message;
            SetBusy(_busy);
            return false;
        }
    }

    private bool TryMarkPreviewFailure(HouseWindowRequest request, HouseWindowProposal proposal, out string errorMessage)
    {
        try
        {
            Urman.Studio.Core.Handoff.DeveloperContext.MarkHouseWindowPreviewFailed(_studio.Workspace.Root, request, proposal);
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception error)
        {
            errorMessage = error.Message;
            return false;
        }
    }

    private void StopCurrentOperation()
    {
        if (_studio.IsWorkspaceActionBusy)
        {
            _status.Text = "Дождитесь завершения текущего действия Studio.";
            return;
        }
        if (_evidenceBusy)
        {
            _status.Text = "Снимок действия записывается. Дождитесь его завершения.";
            return;
        }
        if (_cancel is { } cancellation)
        {
            cancellation.Cancel();
            return;
        }
        if (_busy)
        {
            _status.Text = "Текущее действие нельзя безопасно прервать. Дождитесь результата; если применение завершится, его можно отменить через ⌘/Ctrl+Z.";
            return;
        }
        if (_fenceMode || _fenceProposal is not null || HasTransientFencePreview)
        {
            if (!InvalidatePreparedRequest()) return;
            SetBusy(false);
            _status.Text = FenceManualReadyStatus("Запрос забора отозван; временный предпросмотр снят, исходник участков не изменён.");
            return;
        }
        if (_request is not { } request) return;
        if (!TryClearPreviewFailure(request)) return;
        ClearProposal();
        InvalidatePreparedRequest();
        SetBusy(false);
        _status.Text = "Запрос и локальное предложение отозваны. Проект не изменён.";
    }

    private Task AskAsync()
    {
        if (_fenceMode) return AskFenceAsync();
        if (IsActionBusy || !Visible || _proposal is not null) return Task.CompletedTask;
        try
        {
            var request = PrepareRequest(checkProviderPolicy: true);
            RequireScopeConfirmation(request,
                "Studio отправит этот запрос в Codex автоматически. Изменение файлов проекта потребует отдельного предпросмотра и нажатия «Применить».",
                directProvider: true,
                confirmedRequest => _ = RunProposalAsync(fromManual: false, preparedRequest: confirmedRequest));
        }
        catch (Exception error) { _status.Text = error.Message; }
        return Task.CompletedTask;
    }

    private async Task RunProposalAsync(bool fromManual, string? proposalFile = null, HouseWindowRequest? preparedRequest = null)
    {
        if (_fenceMode)
        {
            if (fromManual && proposalFile is null)
                await RunFenceManualClipboardAsync();
            else
                await RunFenceProposalAsync(fromManual, proposalFile);
            return;
        }
        if (IsActionBusy || !Visible || (!fromManual && _proposal is not null)) return;
        if (!fromManual && (preparedRequest is null || !ReferenceEquals(_request, preparedRequest))) return;
        HouseWindowProposal? imported = null;
        if (fromManual)
        {
            try
            {
                if (_request is null || _proposal is not null)
                    throw new InvalidOperationException("Сначала скопируйте новый запрос для агента.");
                EnsurePreparedRequestCurrent(_request);
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
        var jobRequestAttempted = false;
        var proposalPersisted = false;
        var previewBuildAttempted = false;
        try
        {
            var request = fromManual
                ? _request ?? throw new InvalidOperationException("Запрос ещё не подготовлен.")
                : preparedRequest!;
            EnsurePreparedRequestCurrent(request, checkProviderPolicy: !fromManual);
            if (!ReferenceEquals(_request, request))
                throw new InvalidOperationException("Снимок запроса заменён до отправки.");
            jobRequestAttempted = true;
            Urman.Studio.Core.Handoff.DeveloperContext.WriteHouseWindowJob(_studio.Workspace.Root, request);
            _stage = Directory.CreateTempSubdirectory("urman-house-" + request.RequestId + "-").FullName;
            File.WriteAllText(Path.Combine(_stage, "request.json"), CodexProposalProvider.SerializeRequest(request));
            _status.Text = fromManual ? "Проверяю перенесённое предложение…" : "Codex готовит предложение…";
            var acceptedProposal = imported ?? await new CodexProposalProvider(allowGlobalInstructions: _globalInstructions.ButtonPressed)
                .RunAsync(request, _stage, _cancel.Token, this);
            _cancel.Token.ThrowIfCancellationRequested();
            EnsurePreparedRequestCurrent(request);
            HouseWindowProposalValidator.Validate(request, acceptedProposal);
            _proposal = acceptedProposal;
            Urman.Studio.Core.Handoff.DeveloperContext.WriteHouseWindowJob(_studio.Workspace.Root, request, acceptedProposal);
            proposalPersisted = true;
            previewBuildAttempted = true;
            _status.Text = await BuildAndPreviewAsync(false, _cancel.Token);
            if (Volatile.Read(ref _providerWarningSeen) != 0)
                _status.Text += "\nCodex сообщил предупреждение в сессии. Перед запросом проверено: MCP отключены, доступных MCP-инструментов нет.";
            if (_consentIoWarning is not null) _status.Text += "\n" + _consentIoWarning;
            if (fromManual) _status.Text += "\nОтвет перенесён вручную. Studio не управляет внешней сессией агента.";
            await RecordPreviewEvidenceAsync(request, acceptedProposal);
        }
        catch (OperationCanceledException)
        {
            ClearProposal();
            if (jobRequestAttempted) InvalidatePreparedRequest();
            _status.Text = "Остановлено. Проект не изменён.";
        }
        catch (Exception error)
        {
            if (previewBuildAttempted && proposalPersisted && CanRetainProposalForRetry()
                && _request is { } failedRequest && _proposal is { } failedProposal)
            {
                if (TryMarkPreviewFailure(failedRequest, failedProposal, out var receiptError))
                {
                    ClearBuildStage();
                    _retryBuildAvailable = true;
                    _status.Text = RetryBuildFailureMessage(error, retry: false);
                }
                else
                {
                    ClearProposal();
                    InvalidatePreparedRequest();
                    _status.Text = "Не удалось сохранить запись неудачного предпросмотра; восстановление и повтор этой попытки отключены. " + receiptError;
                }
            }
            else
            {
                ClearProposal();
                if (jobRequestAttempted) InvalidatePreparedRequest();
                _status.Text = "Не применено: " + error.Message;
            }
        }
        finally { _cancel.Dispose(); _cancel = null; SetBusy(false); }
    }

    private async Task RetryBuildAsync()
    {
        if (IsActionBusy || !_retryBuildAvailable || _previewReady || _request is null || _proposal is null || !Visible) return;
        var request = _request;
        var proposal = _proposal;
        if (!TryClearPreviewFailure(request)) return;
        SetBusy(true);
        _retryBuildAvailable = false;
        _cancel = new();
        try
        {
            EnsurePreparedRequestCurrent(request);
            HouseWindowProposalValidator.Validate(request, proposal);
            var summary = await BuildAndPreviewAsync(true, _cancel.Token);
            _status.Text = "Повторная сборка завершена.\n" + summary;
            await RecordPreviewEvidenceAsync(request, proposal);
        }
        catch (OperationCanceledException)
        {
            ClearProposal();
            InvalidatePreparedRequest();
            _status.Text = "Остановлено. Запрос и предложение сняты; проект не изменён.";
        }
        catch (Exception error)
        {
            if (CanRetainProposalForRetry() && _request is { } failedRequest && _proposal is { } failedProposal)
            {
                if (TryMarkPreviewFailure(failedRequest, failedProposal, out var receiptError))
                {
                    ClearBuildStage();
                    _retryBuildAvailable = true;
                    _status.Text = RetryBuildFailureMessage(error, retry: true);
                }
                else
                {
                    ClearProposal();
                    InvalidatePreparedRequest();
                    _status.Text = "Не удалось сохранить запись неудачного предпросмотра; восстановление и повтор этой попытки отключены. " + receiptError;
                }
            }
            else
            {
                ClearProposal();
                InvalidatePreparedRequest();
                _status.Text = "Снимок запроса устарел или предложение больше не проходит проверку. Получите новый запрос и ответ.";
            }
        }
        finally { _cancel.Dispose(); _cancel = null; SetBusy(false); }
    }

    private async Task<string> BuildAndPreviewAsync(bool freshStage, CancellationToken cancellation)
    {
        var request = _request ?? throw new InvalidOperationException("Запрос больше не подготовлен.");
        var proposal = _proposal ?? throw new InvalidOperationException("Предложение больше не доступно.");
        HouseWindowProposalValidator.Validate(request, proposal);
        EnsurePreparedRequestCurrent(request);
        cancellation.ThrowIfCancellationRequested();

        _previewReady = false;
        _retryBuildAvailable = false;
        if (freshStage)
        {
            ClearBuildStage();
            _stage = Directory.CreateTempSubdirectory("urman-house-" + request.RequestId + "-").FullName;
        }
        else
            ClearPreviewModels();
        var stage = _stage ?? throw new InvalidOperationException("Папка сборки отсутствует.");
        File.WriteAllText(Path.Combine(stage, "request.json"), CodexProposalProvider.SerializeRequest(request));
        File.WriteAllText(Path.Combine(stage, "proposal.json"), CodexProposalProvider.SerializeProposal(request, proposal));

        var recipe = JsonNode.Parse(_recipeText ?? throw new InvalidOperationException("Снимок рецепта больше не доступен."))!.AsObject();
        var capturedWidths = _capturedWidths ?? throw new InvalidOperationException("Снимок ширины окон больше не доступен.");
        if (request.PartId is { } partId)
        {
            var widths = (JsonObject)capturedWidths.DeepClone();
            widths[partId] = proposal.WindowWidth;
            recipe["windowWidths"] = widths;
        }
        else if (request.PartIds is { } partIds)
        {
            var widths = (JsonObject)capturedWidths.DeepClone();
            foreach (var windowId in partIds) widths[windowId] = proposal.WindowWidth;
            recipe["windowWidths"] = widths;
        }
        else
        {
            recipe["windowClearWidth"] = proposal.WindowWidth;
            recipe.Remove("windowWidths");
        }
        recipe["modelPath"] = "res://assets/models/studio/hero-house-" + request.RequestId + ".glb";
        File.WriteAllText(Path.Combine(stage, "recipe.json"), recipe.ToJsonString(new() { WriteIndented = true }));

        _status.Text = "Создаю настоящий проём и согласованный интерьер…";
        await GenerateAsync(stage, cancellation);
        cancellation.ThrowIfCancellationRequested();
        HouseWindowProposalValidator.Validate(request, proposal);
        EnsurePreparedRequestCurrent(request);
        var previewArtifacts = ReadStageArtifacts(stage);
        ValidateStage(previewArtifacts);

        ClearPreviewModels();
        _beforeModel = LoadCurrentHousePreview();
        _previewModels.AddChild(_beforeModel);
        _afterModel = LoadHousePreview(previewArtifacts.Glb, stage);
        _previewModels.AddChild(_afterModel);
        cancellation.ThrowIfCancellationRequested();
        HouseWindowProposalValidator.Validate(request, proposal);
        EnsurePreparedRequestCurrent(request);
        _beforeView.Disabled = false;
        _afterView.Disabled = false;
        ShowPreview(after: true);
        _previewArtifactHashes = StageArtifactHashes.From(previewArtifacts);
        _previewReady = true;
        SetBusy(_busy);

        var scopeSummary = request.PartId is not null ? "только окно " + request.PartId
            : request.PartIds is not null ? "7 окон главных комнат; окно в сенях и ширина по умолчанию сохранены"
            : "все 8 окон выбранного дома, включая окно в сенях";
        var widthSummary = request.PartId is null
            ? request.PartIds is not null
                ? $"Целевая ширина 7 окон главных комнат: {proposal.WindowWidth:0.00} м."
                : $"Целевая ширина всех 8 окон: {proposal.WindowWidth:0.00} м."
            : $"Ширина: {proposal.ExpectedWidth:0.00} → {proposal.WindowWidth:0.00} м.";
        return $"{proposal.Summary}\n{widthSummary} {scopeSummary}. Ещё не применено.";
    }

    private async Task RecordPreviewEvidenceAsync(HouseWindowRequest request, HouseWindowProposal proposal)
    {
        try
        {
            var identity = _studio.FreezeAiActionIdentity(request, proposal);
            await _studio.RecordAiActionEvidenceAsync(identity, StudioRoot.AiEvidenceAction.Preview, null,
                () => IsCurrentPreviewEvidence(request, proposal));
        }
        catch (Exception error)
        {
            _studio.ShowAiEvidenceBanner($"Предпросмотр завершён, но Studio receipt не подготовлен ({error.GetType().Name}).", error: true);
        }
    }

    private bool IsCurrentPreviewEvidence(HouseWindowRequest request, HouseWindowProposal proposal)
    {
        if (!IsInstanceValid(this) || !Visible || !IsInsideTree()
            || !ReferenceEquals(_request, request) || !ReferenceEquals(_proposal, proposal) || !_previewReady) return false;
        try
        {
            HouseWindowProposalValidator.Validate(request, proposal);
            EnsurePreparedRequestCurrent(request);
            return _stage is { } stage && _previewArtifactHashes is { } expected
                && expected == HashStageArtifacts(stage);
        }
        catch (Exception) { return false; }
    }

    private bool CanRetainProposalForRetry()
    {
        if (_cancel?.IsCancellationRequested == true || _previewReady || _request is not { } request || _proposal is not { } proposal
            || _recipeText is null || _capturedWidths is null)
            return false;
        try
        {
            HouseWindowProposalValidator.Validate(request, proposal);
            EnsurePreparedRequestCurrent(request);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string RetryBuildFailureMessage(Exception error, bool retry) =>
        $"{(retry ? "Повторная сборка" : "Сборка или предпросмотр")} не удались: {error.Message}\n"
        + "Проверенное предложение и запись неудачного предпросмотра сохранены. Можно повторить локальную сборку или восстановить её после перезапуска. Файлы проекта не изменены.";

    private void ClearBuildStage()
    {
        _previewReady = false;
        ClearPreviewModels();
        var stage = _stage;
        _stage = null;
        if (stage is null) return;
        try { Directory.Delete(stage, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private async Task GenerateAsync(string stage, CancellationToken cancellation)
    {
        var blender = OperatingSystem.IsWindows()
            ? Path.Combine(StudioRoot.ToolsRoot, ".tools", "blender", "windows", "blender-4.5.12-windows-x64", "blender.exe")
            : Path.Combine(StudioRoot.ToolsRoot, ".tools", "blender", "Blender.app", "Contents", "MacOS", "Blender");
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
        if (IsActionBusy) return;
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
        if (_fenceMode || _fenceProposal is not null || IsActionBusy || !_previewReady || _proposal is null || _request is null || _stage is null) return;
        if (!TryClearPreviewFailure(_request)) return;
        SetBusy(true);
        var mutationSucceeded = false;
        try
        {
            var request = _request;
            var proposal = _proposal;
            EnsurePreparedRequestCurrent(request);
            HouseWindowProposalValidator.Validate(request, proposal);
            StageArtifacts stageArtifacts;
            try
            {
                stageArtifacts = ReadStageArtifacts(_stage);
                if (_previewArtifactHashes is not { } previewHashes || previewHashes != StageArtifactHashes.From(stageArtifacts))
                    throw new InvalidDataException("Артефакты сборки изменились после предпросмотра. Повторите сборку перед применением.");
            }
            catch
            {
                _previewReady = false;
                _previewArtifactHashes = null;
                _retryBuildAvailable = true;
                throw;
            }
            ValidateStage(stageArtifacts);
            StudioRoot.AiActionIdentity? evidenceIdentity = null;
            string? evidenceIdentityError = null;
            try { evidenceIdentity = _studio.FreezeAiActionIdentity(request, proposal); }
            catch (Exception error) { evidenceIdentityError = error.GetType().Name; }
            var model = "game/assets/models/studio/hero-house-" + request.RequestId + ".glb";
            var blend = "assets/source/blender/studio/hero-house-" + request.RequestId + ".blend";
            var manifest = model + ".manifest.json";
            var writes = new StudioFileWrite[] {
                new(RecipePath, AtomicFile.Sha256(System.Text.Encoding.UTF8.GetBytes(_recipeText!)), stageArtifacts.Recipe),
                new(model, null, stageArtifacts.Glb),
                new(blend, null, stageArtifacts.Blend),
                new(manifest, null, stageArtifacts.Manifest)
            };
            try
            {
                await _studio.Session.ApplyFilesAsync("изменить окна дома через Codex", HeroHouseRecipe.EntityId, writes,
                    new HashSet<string>(writes.Select(write => write.RelativePath), StringComparer.Ordinal), ImportAssets,
                    () => ((StudioWorldSection)_studio.Section("world")).ReloadGeometryAsync());
            }
            catch (Exception error)
            {
                _status.Text = "Не применено: " + error.Message;
                return;
            }

            mutationSucceeded = true;
            _previewReady = false;
            _previewArtifactHashes = null;
            if (!GodotObject.IsInstanceValid(this))
            {
                if (evidenceIdentity is null)
                    _studio.ShowAiEvidenceBanner("Изменение применено, но frozen receipt identity не прочитан; Studio кадр не сохранён.", error: true);
                else
                {
                    var hiddenApplied = _studio.Session.History().FirstOrDefault();
                    if (_studio.IsPilotStep(hiddenApplied, request.RequestId, hiddenApplied?.TransactionId))
                        await _studio.RecordAiActionEvidenceAsync(evidenceIdentity, StudioRoot.AiEvidenceAction.Apply, hiddenApplied!.TransactionId,
                            () => _studio.IsPilotStep(_studio.Session.History().FirstOrDefault(), request.RequestId, hiddenApplied.TransactionId));
                    else
                        _studio.ShowAiEvidenceBanner("Изменение применено, но Studio не подтвердила транзакцию; Studio кадр не сохранён.", error: true);
                }
                return;
            }

            _status.Text = "Окна изменены. ⌘/Ctrl+Z вернёт исходные проёмы, рецепт и модель; повторить можно через Redo без Codex.";
            var applied = _studio.Session.History().FirstOrDefault();
            if (evidenceIdentity is null)
                _studio.ShowAiEvidenceBanner($"Изменение применено; frozen receipt identity не прочитан ({evidenceIdentityError ?? "unknown"}).", error: true);
            else if (_studio.IsPilotStep(applied, request.RequestId, applied?.TransactionId))
                await _studio.RecordAiActionEvidenceAsync(evidenceIdentity, StudioRoot.AiEvidenceAction.Apply, applied!.TransactionId,
                    () => _studio.IsPilotStep(_studio.Session.History().FirstOrDefault(), request.RequestId, applied.TransactionId));
            else
                _studio.ShowAiEvidenceBanner("Изменение применено, но Studio не подтвердила точную транзакцию; receipt не записан.", error: true);

            ClearProposal();
            InvalidatePreparedRequest();
        }
        catch (Exception error)
        {
            if (mutationSucceeded)
            {
                if (GodotObject.IsInstanceValid(this))
                    _status.Text = "Окна изменены; Studio завершила применение, но receipt или очистка панели не завершились: " + error.Message;
                _studio.ShowAiEvidenceBanner("Файлы уже изменены. Studio не будет повторять транзакцию автоматически.", error: true);
            }
            else if (GodotObject.IsInstanceValid(this))
                _status.Text = "Не применено: " + error.Message;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(this))
            {
                try { SetBusy(false); }
                catch (Exception) { }
            }
        }
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
        return LoadHousePreview(File.ReadAllBytes(path), Path.GetDirectoryName(path) ?? "");
    }

    private static Node3D LoadHousePreview(byte[] bytes, string basePath)
    {
        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromBuffer(bytes, basePath, state) != Error.Ok)
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
        _previewArtifactHashes = null;
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

    private static StageArtifacts ReadStageArtifacts(string stage) => new(
        File.ReadAllBytes(Path.Combine(stage, "recipe.json")),
        File.ReadAllBytes(Path.Combine(stage, "hero_house.glb")),
        File.ReadAllBytes(Path.Combine(stage, "hero_house.blend")),
        File.ReadAllBytes(Path.Combine(stage, "manifest.json")));

    private static StageArtifactHashes HashStageArtifacts(string stage) => new(
        AtomicFile.Sha256OfFile(Path.Combine(stage, "recipe.json")),
        AtomicFile.Sha256OfFile(Path.Combine(stage, "hero_house.glb")),
        AtomicFile.Sha256OfFile(Path.Combine(stage, "hero_house.blend")),
        AtomicFile.Sha256OfFile(Path.Combine(stage, "manifest.json")));

    private void ValidateStage(StageArtifacts files)
    {
        var manifest = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(files.Manifest))!;
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
        {
            var hash = key == "glb" ? AtomicFile.Sha256(files.Glb) : AtomicFile.Sha256(files.Blend);
            if ((string?)manifest["outputs"]?[key]?["sha256"] != hash)
                throw new InvalidDataException("Артефакт изменился после сборки: " + name);
        }
        if ((string?)manifest["source"]?["recipe"]?["sha256"] != AtomicFile.Sha256(files.Recipe))
            throw new InvalidDataException("Рецепт изменился после сборки.");
        foreach (var (key, path) in new[] { ("generator", GeneratorPath), ("baselineBlend", "assets/source/blender/act1/urman_village_exterior_kit.blend") })
            if ((string?)manifest["source"]?[key]?["sha256"] != AtomicFile.Sha256OfFile(Path.Combine(_studio.Workspace.Root, path)))
                throw new InvalidDataException("Исходник изменился после сборки: " + path);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UpdateBusyState();
        if (GodotObject.IsInstanceValid(_studio)) _studio.RefreshStatus();
    }

    internal void RefreshBusyState()
    {
        RefreshFenceRequestFreshness();
        UpdateBusyState();
    }

    internal void SetEvidenceBusy(bool busy)
    {
        _evidenceBusy = busy;
        UpdateBusyState();
    }

    private void UpdateBusyState()
    {
        var busy = IsActionBusy;
        _ask.Text = _fenceMode ? "Предложить выпрямление" : "Предложить изменение";
        _ask.TooltipText = _fenceMode
            ? "Отправить точный запрос по выбранному пролёту и показать только временный предпросмотр."
            : "Отправить запрос: ⌘/Ctrl+Enter";
        _paste.TooltipText = _fenceMode
            ? "Вставьте чистый JSON v3 для текущего запроса. Studio проверит его и покажет временный предпросмотр; применение отключено."
            : "Скопируйте чистый JSON ответа. Studio проверит его и покажет модель до применения.";
        _importFile.TooltipText = _fenceMode
            ? "Проверить ответ v3 по сохранённому запросу и показать временный предпросмотр в мире; применение отключено."
            : "Проверить ответ по сохранённому запросу и показать модель до применения.";
        _apply.TooltipText = _fenceMode
            ? "Применение предложений для заборов пока не подключено."
            : string.Empty;
        _prompt.Editable = !busy;
        var fenceSelected = IsCurrentFenceMode();
        _doctor.Disabled = busy || _fenceMode;
        _stop.Disabled = _studio.IsWorkspaceActionBusy || _evidenceBusy || _scopeConfirmationBusy;
        _globalInstructions.Disabled = busy || _previewFailureClearFailed;
        var houseSelected = _openedSelection == HeroHouseRecipe.EntityId && _studio.Selection == _openedSelection;
        _scope.Disabled = busy || _previewFailureClearFailed || _proposal is not null || _openedSelection != HeroHouseRecipe.EntityId;
        _ask.Disabled = busy || _previewFailureClearFailed || !(houseSelected || fenceSelected)
            || (_fenceMode ? _fenceProposal is not null : _proposal is not null);
        _copy.Disabled = busy || _previewFailureClearFailed || !(houseSelected || fenceSelected);
        _saveRequest.Disabled = busy || _previewFailureClearFailed || !(houseSelected || fenceSelected);
        _disclosure.Disabled = busy || _previewFailureClearFailed || !(houseSelected || fenceSelected);
        var housePasteReady = houseSelected && !_fenceMode && _request is not null && _proposal is null;
        var fencePasteReady = fenceSelected && _fenceMode && _fenceRequest is not null && _fenceProposal is null;
        _paste.Disabled = busy || _previewFailureClearFailed || !(housePasteReady || fencePasteReady);
        _importFile.Disabled = _paste.Disabled;
        _apply.Disabled = busy || _previewFailureClearFailed || _fenceMode || !houseSelected || !_previewReady
            || _proposal is null || _request is null || _stage is null;
        _beforeView.Disabled = busy || _fenceMode || !houseSelected || !_previewReady;
        _afterView.Disabled = busy || _fenceMode || !houseSelected || !_previewReady;
        _retryBuild.Disabled = busy || _fenceMode || _previewFailureClearFailed || !_retryBuildAvailable || _previewReady || _proposal is null
            || _request is null || !houseSelected || !Visible;
        _restorePreview.Disabled = busy || _fenceMode || _previewFailureClearFailed || !houseSelected || _request is not null || _proposal is not null;
    }
    private void ClearProposal()
    {
        _proposal = null;
        _retryBuildAvailable = false;
        ClearBuildStage();
        _apply.Disabled = true;
        _retryBuild.Disabled = true;
    }
}
