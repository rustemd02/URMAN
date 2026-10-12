using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Godot;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Play;
using Urman.Studio.Core.Storage;
using FileAccess = System.IO.FileAccess;

namespace Urman.Studio.App;

public partial class StudioAiPanel
{
    private const int FamilySelectedScope = 0;
    private const int FamilyGroupScope = 1;
    private const int FamilyAllScope = 2;
    private const string FamilyBaseModelPath = "res://assets/models/act1/urman_village_exterior_kit.glb";
    private const string FamilyBaseModelFile = "game/assets/models/act1/urman_village_exterior_kit.glb";
    private const string FamilyGeneratorPath = "assets/source/blender/act1/urman_village_exterior_kit.py";
    private const string FamilyBaselineBlendPath = "assets/source/blender/act1/urman_village_exterior_kit.blend";
    private const string FamilyCatalogPath = "game/content/world/catalog.v1.json";
    private const string FamilyPrivatePrefix = "res://assets/models/studio/house-variant-";
    private const string FamilyBlendPrefix = "assets/source/blender/studio/house-variant-";
    private const string FamilyModelFilePrefix = "game/assets/models/studio/house-variant-";
    private const int FamilyMaximumModelBytes = 256 * 1024 * 1024;
    private const int FamilyMaximumWorldSourceBytes = 8 * 1024 * 1024;
    private static readonly UTF8Encoding FamilyStrictUtf8 = new(false, true);
    private static readonly string[] FamilyWindowIds =
    ["Front0", "Front1", "Front2", "Rear0", "Rear1", "Left0", "Left1", "Right0", "SeniOuter0"];
    private static readonly IReadOnlyDictionary<string, decimal> FamilyBaseWidths = new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
        ["Front0"] = .96m, ["Front1"] = .96m, ["Front2"] = .96m,
        ["Rear0"] = .96m, ["Rear1"] = .96m,
        ["Left0"] = 1.0m, ["Left1"] = 1.0m, ["Right0"] = 1.0m,
        ["SeniOuter0"] = .80m
    };
    private static readonly IReadOnlyDictionary<string, (string CatalogId, string SourceNode)> FamilyComponents =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["A"] = ("urman.catalog:urman_village_exterior_kit/villageparcel-varianta-timbergable", "VillageParcel_VariantA_TimberGable"),
            ["B"] = ("urman.catalog:urman_village_exterior_kit/villageparcel-variantb-plasterannex", "VillageParcel_VariantB_PlasterAnnex"),
            ["C"] = ("urman.catalog:urman_village_exterior_kit/villageparcel-variantc-banyayard", "VillageParcel_VariantC_BanyaYard")
        };

    private sealed record FamilyComponentInfo(string Component, string CatalogId, string SourceNode, IReadOnlyList<double> Origin);
    private sealed record FamilyCandidate(string EntityId, string EntityKind, string RelativePath, FamilyComponentInfo Component, JsonObject Entity);
    private sealed record FamilyCurrentModel(HouseFamilyWindowModelProvenance Provenance,
        IReadOnlyDictionary<string, decimal> Widths, IReadOnlyList<string>? TargetEntityIds);
    private sealed record FamilyStageArtifact(
        string Component, string ModelId, string ModelPath, string ModelRelativePath,
        string BlendRelativePath, string ManifestRelativePath, string Directory,
        string RecipePath, string GlbPath, string BlendPath, string ManifestPath,
        string RecipeSha256, string GlbSha256, string BlendSha256, string ManifestSha256,
        IReadOnlyList<string> TargetEntityIds);

    private readonly OptionButton _familyTargetPicker = new()
    {
        Visible = false,
        CustomMinimumSize = new Vector2(250, 0),
        TooltipText = "Выберите дом для просмотра пары «до / после»."
    };
    private bool _familyTargetPickerConnected;
    private HouseFamilyWindowRequest? _familyRequest;
    private HouseFamilyWindowProposal? _familyProposal;
    private bool _familyMode;
    private bool _familyPreviewReady;
    private bool _familyRetryBuildAvailable;
    private string? _familyPreviewTargetId;
    private string? _familyDisclosedSnapshotHash;
    private int _familyFrozenScopeChoice = -1;
    private string[] _familyFrozenScopeSelection = [];
    private readonly Dictionary<string, FamilyStageArtifact> _familyBuilds = new(StringComparer.Ordinal);

    internal OptionButton CreateFamilyTargetPicker()
    {
        if (!_familyTargetPickerConnected)
        {
            _familyTargetPicker.ItemSelected += index =>
            {
                if (!_familyMode || _familyRequest is null || !_familyPreviewReady || IsActionBusy) return;
                if (_familyTargetPicker.GetItemMetadata((int)index).AsString() is { } id)
                    ShowFamilyTargetPreview(id);
            };
            _familyTargetPickerConnected = true;
        }
        return _familyTargetPicker;
    }

    internal bool TryEnableFamilyMode(string? selected)
    {
        if (string.IsNullOrWhiteSpace(selected) || !string.Equals(_studio.Selection, selected, StringComparison.Ordinal)
            || IsActionBusy || !TryGetFamilyCandidate(selected, out _))
            return false;

        ResetFamilyPreparedState();
        _familyMode = true;
        _openedSelection = selected;
        Title = "Изменить окна семейства домов";
        _prompt.PlaceholderText = "Например: сделай окна домов шире, сохранив различия между участками";
        _scope.Visible = true;
        ConfigureFamilyScope();
        _status.Text = FamilyReadyStatus("Выберите один дом, выделенную группу или все совместимые дома. Общий ассет каталога останется неизменным.");
        SetFamilyBusy(false);
        return true;
    }

    internal void ConfigureFamilyScope()
    {
        _scope.Clear();
        _scope.AddItem("Только выбранный дом", FamilySelectedScope);
        _scope.AddItem("Выбранная группа домов", FamilyGroupScope);
        _scope.AddItem("Все совместимые дома", FamilyAllScope);
        _scope.Select(FamilySelectedScope);
        _scope.TooltipText = "Охват фиксируется в запросе. Несовместимые и невыбранные объекты перечисляются как исключённые.";
    }

    internal bool IsCurrentFamilyMode() => _familyMode && Visible;

    internal void FamilyScopeChanged()
    {
        if (!_familyMode || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy) return;
        ResetFamilyPreparedState();
        _status.Text = FamilyReadyStatus("Охват изменён; прежний запрос и ответ отозваны. Откройте раскрытие, чтобы зафиксировать новый список целей и исключений.");
        SetFamilyBusy(false);
    }

    internal void FamilyPromptChanged()
    {
        if (!_familyMode || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy
            || _familyRequest is not { } request || string.Equals(_prompt.Text, request.Prompt, StringComparison.Ordinal))
            return;
        ResetFamilyPreparedState();
        _status.Text = FamilyReadyStatus("Описание изменилось; прежний запрос и ответ отозваны. Подготовьте новый снимок.");
        SetFamilyBusy(false);
    }

    internal void RefreshFamilyRequestFreshness()
    {
        if (!_familyMode || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy
            || _familyRequest is not { } request)
            return;
        try { EnsureFamilyRequestCurrent(request, allowUntargetedTargetFileChanges: false); }
        catch (Exception error)
        {
            ResetFamilyPreparedState();
            _status.Text = FamilyReadyStatus("Снимок семейства отозван: " + error.Message + " Откройте раскрытие, чтобы снять актуальный запрос.");
        }
    }

    internal void SetFamilyBusy(bool busy)
    {
        if (!_familyMode) return;
        var blocked = busy || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy;
        var selected = IsCurrentFamilyMode();
        _ask.Text = "Предложить окна семейству";
        _ask.TooltipText = "Сначала покажите полный снимок и подтвердите точные дома и исключения; файлы изменятся только после отдельного Apply.";
        _paste.TooltipText = "Проверить чистый JSON по текущему снимку; внешний агент остаётся под вашим управлением.";
        _importFile.TooltipText = "Открыть JSON-ответ и проверить его по замороженному запросу.";
        _apply.Text = "Применить ко всем выбранным";
        _apply.TooltipText = "Записать все варианты и источники одним обратимым файловым действием.";
        _doctor.Disabled = blocked;
        _scope.Visible = true;
        _scope.Disabled = blocked || _familyProposal is not null || !selected;
        _prompt.Editable = !blocked;
        _globalInstructions.Disabled = blocked;
        _ask.Disabled = blocked || !selected || _familyProposal is not null;
        _disclosure.Disabled = blocked || !selected;
        _copy.Disabled = blocked || !selected;
        _saveRequest.Disabled = blocked || !selected;
        var canPaste = selected && _familyRequest is not null && _familyProposal is null;
        _paste.Disabled = blocked || !canPaste;
        _importFile.Disabled = _paste.Disabled;
        var canApply = selected && _familyRequest is not null && _familyProposal is not null
            && _familyPreviewReady && _stage is not null && !_familyRetryBuildAvailable;
        _apply.Disabled = blocked || !canApply;
        _beforeView.Disabled = blocked || !_familyPreviewReady;
        _afterView.Disabled = blocked || !_familyPreviewReady;
        _retryBuild.Disabled = blocked || !_familyRetryBuildAvailable || _familyProposal is null || _familyRequest is null || !selected;
        _restorePreview.Disabled = true;
        _familyTargetPicker.Visible = selected && _familyPreviewReady && _familyRequest?.Targets.Count > 1;
        _familyTargetPicker.Disabled = blocked || !_familyPreviewReady;
    }

    internal void ClearFamilyState()
    {
        _cancel?.Cancel();
        if (_busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy) return;
        ResetFamilyPreparedState();
        _familyMode = false;
        _familyTargetPicker.Visible = false;
    }

    private void ResetFamilyPreparedState()
    {
        _familyRequest = null;
        _familyProposal = null;
        _familyFrozenScopeChoice = -1;
        _familyFrozenScopeSelection = [];
        _familyDisclosedSnapshotHash = null;
        _familyPreviewReady = false;
        _familyRetryBuildAvailable = false;
        _familyPreviewTargetId = null;
        _familyBuilds.Clear();
        if (_approvedScopeSnapshotHash?.StartsWith("family:", StringComparison.Ordinal) == true)
        {
            _approvedScopeSnapshotHash = null;
            _approvedScopeGlobalInstructions = null;
        }
        _previewArtifactHashes = null;
        _previewReady = false;
        ClearBuildStage();
        _familyTargetPicker.Clear();
        _familyTargetPicker.Visible = false;
        if (GodotObject.IsInstanceValid(this) && IsInsideTree() && _familyMode) SetFamilyBusy(false);
    }

    internal void OpenFamilyDisclosure()
    {
        if (!_familyMode || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy || !Visible) return;
        try
        {
            var request = PrepareFamilyRequest();
            var instructionPolicy = _globalInstructions.ButtonPressed ? "включены" : "выключены";
            var text = "Модель получает только замороженные сведения ниже и возвращает одну ширину для девяти окон каждого выбранного дома."
                + " Файлы проекта изменяет Studio отдельным подтверждённым действием после предпросмотра."
                + " Камера передаётся числовыми параметрами; снимок изображения не отправляется."
                + $"\nГлобальные инструкции Codex для прямого запуска сейчас {instructionPolicy}. При ручной передаче внешней сессией управляет пользователь.\n\n"
                + FamilyRequestDescription(request)
                + "\n\nПолный канонический JSON запроса:\n"
                + CodexProposalProvider.SerializeRequest(request);
            if (_familyProposal is { } proposal)
                text += "\n\nПринятый ответ:\n" + CodexProposalProvider.SerializeProposal(request, proposal);
            ShowDisclosure(text, rememberGlobalInstructions: _familyProposal is null);
            _familyDisclosedSnapshotHash = request.SnapshotHash;
        }
        catch (Exception error)
        {
            _status.Text = FamilyReadyStatus("Не удалось подготовить раскрытие запроса: " + error.Message);
        }
    }

    internal void CopyFamilyRequest()
    {
        if (!_familyMode || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy || !Visible) return;
        try
        {
            var request = PrepareFamilyRequest();
            RequireFamilyDisclosureAndConfirmation(request, "Studio скопирует точный запрос для ручной передачи агенту.", directProvider: false,
                confirmed =>
                {
                    DisplayServer.ClipboardSet(CodexProposalProvider.CreateClipboardPrompt(confirmed));
                    _status.Text = FamilyReadyStatus("Запрос скопирован. Внешней сессией управляет пользователь; проект пока не изменён.");
                });
        }
        catch (Exception error) { _status.Text = FamilyReadyStatus("Запрос не скопирован: " + error.Message); }
    }

    internal void SaveFamilyRequest()
    {
        if (!_familyMode || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy || !Visible) return;
        try
        {
            var request = PrepareFamilyRequest();
            RequireFamilyDisclosureAndConfirmation(request, "Studio сохранит точный запрос для ручной передачи агенту.", directProvider: false,
                confirmed =>
                {
                    var path = Urman.Studio.Core.Handoff.DeveloperContext.WriteHouseFamilyWindowRequest(_studio.Workspace.Root, confirmed);
                    _status.Text = FamilyReadyStatus("Запрос сохранён для ручной передачи. Проект ещё не изменён.");
                    OS.ShellShowInFileManager(path);
                });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _status.Text = FamilyReadyStatus("Запрос не сохранён: " + error.Message + " Существующий файл не перезаписывается.");
        }
    }

    internal Task AskFamilyAsync()
    {
        if (!_familyMode || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy || !Visible || _familyProposal is not null)
            return Task.CompletedTask;
        try
        {
            var request = PrepareFamilyRequest(checkProviderPolicy: true);
            RequireFamilyDisclosureAndConfirmation(request,
                "Studio отправит этот точный снимок в Codex. Изменение файлов проекта потребует отдельного предпросмотра и нажатия «Применить».",
                directProvider: true,
                confirmed => _ = RunFamilyProposalCoreAsync(fromManual: false, proposalFile: null, confirmed));
        }
        catch (Exception error) { _status.Text = FamilyReadyStatus("Запрос не отправлен: " + error.Message); }
        return Task.CompletedTask;
    }

    internal Task RunFamilyProposalAsync(bool fromManual, string? proposalFile = null)
    {
        if (!_familyMode || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy || !Visible)
            return Task.CompletedTask;
        if (!fromManual)
        {
            if (_familyRequest is not { } directRequest || _familyProposal is not null) return Task.CompletedTask;
            try
            {
                EnsureFamilyRequestCurrent(directRequest, checkProviderPolicy: true);
                var approval = FamilyApprovalKey(directRequest);
                if (_approvedScopeSnapshotHash != approval
                    || _approvedScopeGlobalInstructions != _globalInstructions.ButtonPressed)
                {
                    RequireFamilyDisclosureAndConfirmation(directRequest,
                        "Studio отправит этот снимок в Codex. Apply останется отдельным действием.", true,
                        confirmed => _ = RunFamilyProposalCoreAsync(false, null, confirmed));
                    return Task.CompletedTask;
                }
                _ = RunFamilyProposalCoreAsync(false, null, directRequest);
            }
            catch (Exception error) { _status.Text = FamilyReadyStatus("Запрос не отправлен: " + error.Message); }
            return Task.CompletedTask;
        }

        try
        {
            var request = PrepareFamilyRequest();
            if (_familyProposal is not null) throw new InvalidOperationException("Для этого снимка уже принят ответ. Сначала измените запрос или сбросьте его.");
            RequireFamilyDisclosureAndConfirmation(request, "Studio проверит JSON по замороженному списку домов.", directProvider: false,
                confirmed => _ = RunFamilyProposalCoreAsync(fromManual: true, proposalFile, confirmed));
        }
        catch (Exception error) { _status.Text = FamilyReadyStatus("Ответ не принят: " + error.Message); }
        return Task.CompletedTask;
    }

    internal void OpenFamilyProposalFile()
    {
        if (!_familyMode || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy
            || _familyRequest is null || _familyProposal is not null || !Visible) return;
        var picker = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "Открыть JSON-ответ семейству домов",
            UseNativeDialog = true,
            Filters = ["*.json ; Ответ семейства домов"]
        };
        var requestId = _familyRequest.RequestId;
        picker.FileSelected += path =>
        {
            CloseRootPopup(picker);
            if (IsActionBusy || !Visible || !_familyMode || _familyRequest?.RequestId != requestId)
            {
                _status.Text = FamilyReadyStatus("Ответ не открыт: снимок изменился или панель недоступна. Выберите файл для актуального запроса.");
                return;
            }
            _ = RunFamilyProposalAsync(fromManual: true, proposalFile: path);
        };
        picker.Canceled += () => CloseRootPopup(picker);
        ShowRootPopup(picker, () => picker.PopupCentered(new Vector2I(900, 600)));
    }

    internal async Task RetryFamilyBuildAsync()
    {
        if (!_familyMode || !Visible || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy
            || !_familyRetryBuildAvailable || _familyRequest is not { } request || _familyProposal is not { } proposal)
            return;
        SetBusy(true);
        var cancellation = new CancellationTokenSource();
        _cancel = cancellation;
        try
        {
            EnsureFamilyRequestCurrent(request);
            HouseFamilyWindowProtocol.ValidateProposal(request, proposal);
            _status.Text = "Повторяю локальную сборку семейств. Codex не вызывается; исходники ещё не меняются.";
            _familyRetryBuildAvailable = false;
            await BuildFamilyAndPreviewAsync(request, proposal, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            _familyPreviewReady = false;
            _familyRetryBuildAvailable = _familyProposal is not null && _stage is not null;
            if (GodotObject.IsInstanceValid(this) && Visible)
                _status.Text = FamilyReadyStatus("Сборка остановлена. Проверенное предложение осталось доступно для повтора; файлы проекта не изменены.");
        }
        catch (Exception error)
        {
            _familyPreviewReady = false;
            _familyRetryBuildAvailable = true;
            if (GodotObject.IsInstanceValid(this))
                _status.Text = FamilyReadyStatus("Повторная локальная сборка не удалась: " + error.Message + " Файлы проекта не изменены.");
        }
        finally
        {
            if (ReferenceEquals(_cancel, cancellation)) _cancel = null;
            cancellation.Dispose();
            if (GodotObject.IsInstanceValid(this))
            {
                if (!Visible)
                {
                    ResetFamilyPreparedState();
                    _familyMode = false;
                    _familyTargetPicker.Visible = false;
                }
                SetBusy(false);
            }
        }
    }

    internal async Task ApplyFamilyAsync()
    {
        if (!_familyMode || !Visible || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy
            || _familyRequest is not { } request || _familyProposal is not { } proposal
            || !_familyPreviewReady || _familyRetryBuildAvailable || _stage is null)
            return;

        SetBusy(true);
        var committed = false;
        try
        {
            HouseFamilyWindowProtocol.ValidateRequest(request);
            HouseFamilyWindowProtocol.ValidateProposal(request, proposal);
            EnsureFamilyRequestCurrent(request, allowUntargetedTargetFileChanges: true);
            ValidateFamilyStageArtifacts(request, proposal);

            var changedFiles = PrepareFamilySourceWrites(request, proposal);
            var writes = new List<StudioFileWrite>();
            foreach (var artifact in _familyBuilds.Values.OrderBy(item => item.Component, StringComparer.Ordinal))
            {
                var glb = ReadBoundedFamilyFile(artifact.GlbPath, FamilyMaximumModelBytes);
                var blend = ReadBoundedFamilyFile(artifact.BlendPath, FamilyMaximumModelBytes);
                var manifest = ReadBoundedFamilyFile(artifact.ManifestPath, 2 * 1024 * 1024);
                if (AtomicFile.Sha256(glb) != artifact.GlbSha256 || AtomicFile.Sha256(blend) != artifact.BlendSha256
                    || AtomicFile.Sha256(manifest) != artifact.ManifestSha256)
                    throw new InvalidDataException("Артефакт изменился непосредственно перед применением.");
                writes.Add(new(artifact.ModelRelativePath, null, glb));
                writes.Add(new(artifact.BlendRelativePath, null, blend));
                writes.Add(new(artifact.ManifestRelativePath, null, manifest));
            }
            writes.AddRange(changedFiles.Select(file => file.Write));
            var allowed = writes.Select(write => write.RelativePath).ToHashSet(StringComparer.Ordinal);
            var sourcePaths = changedFiles.Select(file => file.Path).ToArray();
            var studio = _studio;
            var world = (StudioWorldSection)studio.Section("world");
            var artifactSnapshot = new Dictionary<string, FamilyStageArtifact>(_familyBuilds, StringComparer.Ordinal);
            const string label = "изменить окна семейства домов через Codex";
            var owner = "house-family.windows.resize/" + request.RequestId;
            await studio.Session.ApplyFilesAsync(label, owner, writes, allowed,
                () =>
                {
                    foreach (var path in sourcePaths)
                    {
                        var file = studio.Workspace.File(path);
                        if (file.Dirty || file.IsNew)
                            throw new InvalidOperationException("Черновик исходника изменился во время применения: " + path);
                        file.PullFromDisk();
                    }
                    studio.Workspace.Reindex();
                    ValidatePromotedFamilyModels(studio, request, artifactSnapshot);
                },
                () => world.ReloadGeometryAsync());
            committed = true;

            var history = _studio.Session.History().FirstOrDefault();
            var expectedPaths = writes.Select(write => write.RelativePath).ToHashSet(StringComparer.Ordinal);
            if (history is null || history.Label != label || history.EntityChangeCount != 0
                || history.TransactionId is null || !new HashSet<string>(history.Paths, StringComparer.Ordinal).SetEquals(expectedPaths))
                throw new InvalidOperationException("Studio не подтвердила единственную транзакцию всех вариантов и исходников семейства.");

            var transactionId = history.TransactionId;
            ResetFamilyPreparedState();
            _status.Text = "Окна выбранных домов изменены одной транзакцией " + transactionId
                + ". ⌘/Ctrl+Z полностью отменит варианты и исходники; Redo повторит их без нового запроса.";
        }
        catch (Exception error)
        {
            if (committed && GodotObject.IsInstanceValid(this)) ResetFamilyPreparedState();
            _status.Text = committed
                ? "Изменение уже применено; Studio не будет повторять транзакцию автоматически. " + error.Message
                : "Семейство не применено: " + error.Message;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(this))
            {
                if (!Visible)
                {
                    ResetFamilyPreparedState();
                    _familyMode = false;
                    _familyTargetPicker.Visible = false;
                }
                SetBusy(false);
            }
        }
    }

    private HouseFamilyWindowRequest PrepareFamilyRequest(bool checkProviderPolicy = false)
    {
        if (!IsCurrentFamilyMode() || string.IsNullOrWhiteSpace(_prompt.Text))
            throw new InvalidOperationException("Опишите нужное изменение окон и оставьте семейный режим открытым.");
        if (_familyRequest is null)
        {
            _familyRequest = CaptureFamilyRequest();
            _familyProposal = null;
        }
        else
        {
            EnsureFamilyRequestCurrent(_familyRequest, checkProviderPolicy: checkProviderPolicy);
        }
        var request = _familyRequest ?? throw new InvalidOperationException("Снимок семейства не подготовлен.");
        HouseFamilyWindowProtocol.ValidateRequest(request);
        return request;
    }

    private HouseFamilyWindowRequest CaptureFamilyRequest()
    {
        var world = (StudioWorldSection)_studio.Section("world");
        if (!world.WorldReady)
            throw new InvalidOperationException("Сначала загрузите карту в разделе «Мир», чтобы зафиксировать её точку входа и камеру.");
        var worldFiles = _studio.Workspace.Files
            .Where(file => file.RelativePath.StartsWith("game/content/world/", StringComparison.Ordinal)
                && file.RelativePath.EndsWith(".world.v1.json", StringComparison.Ordinal))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
        if (worldFiles.Length == 0) throw new InvalidOperationException("В Workspace нет загруженных исходников мира.");

        var sourceSnapshots = new Dictionary<string, AuthoredSourceSnapshot>(StringComparer.Ordinal);
        var allWorldIds = new List<string>();
        var candidates = new List<FamilyCandidate>();
        foreach (var file in worldFiles)
        {
            if (file.Dirty || file.IsNew)
                throw new InvalidOperationException("Сначала сохраните или отмените черновик исходника мира: " + file.RelativePath);
            var source = file.CaptureSource();
            if (source.IsNew || source.Dirty || source.BaseSha256 is null
                || !string.Equals(source.Sha256, source.BaseSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Для семейного запроса разрешён только чистый сохранённый исходник: " + file.RelativePath);
            EnsureNoDuplicateJsonProperties(source.Text);
            sourceSnapshots.Add(file.RelativePath, source);
            foreach (var id in file.Keys())
            {
                allWorldIds.Add(id);
                if (file.Get(id) is JsonObject entity && TryCreateFamilyCandidate(id, file.RelativePath, entity, out var candidate))
                    candidates.Add(candidate);
            }
        }
        if (allWorldIds.Count != allWorldIds.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidOperationException("В исходниках мира есть повторяющиеся ID; точный список исключений нельзя безопасно зафиксировать.");

        var scopeChoice = _scope.Selected;
        string[] selectionIds;
        IReadOnlyList<FamilyCandidate> selectedCandidates;
        switch (scopeChoice)
        {
            case FamilySelectedScope:
                selectionIds = _studio.Selection is { } selectedId ? [selectedId] : [];
                if (selectionIds.Length == 0) throw new InvalidOperationException("Выберите один совместимый дом в мире.");
                selectedCandidates = candidates.Where(candidate => candidate.EntityId == selectionIds[0]).ToArray();
                break;
            case FamilyGroupScope:
                var group = ((StudioWorldSection)_studio.Section("world")).Group.ToArray();
                selectionIds = (group.Length > 0 ? group : _studio.Selection is { } one ? [one] : [])
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (selectionIds.Length == 0) throw new InvalidOperationException("Сначала выделите группу домов в мире.");
                var selectedSet = selectionIds.ToHashSet(StringComparer.Ordinal);
                selectedCandidates = candidates.Where(candidate => selectedSet.Contains(candidate.EntityId)).ToArray();
                break;
            case FamilyAllScope:
                selectionIds = [];
                selectedCandidates = candidates.OrderBy(candidate => candidate.EntityId, StringComparer.Ordinal).ToArray();
                break;
            default:
                throw new InvalidOperationException("Выберите поддерживаемый охват семейства.");
        }
        if (selectedCandidates.Count == 0)
            throw new InvalidOperationException("В выбранном охвате нет поддерживаемых домов A, B или C с точным исходником.");
        if (selectedCandidates.Count > HouseFamilyWindowProtocol.MaximumTargets)
            throw new InvalidOperationException("Охват превышает безопасный предел целей семейного запроса.");
        foreach (var candidate in selectedCandidates)
        {
            if (_studio.Workspace.IsAmbiguous(candidate.EntityId)
                || _studio.Workspace.Locate(candidate.EntityId) is not { } address
                || address.RelativePath != candidate.RelativePath || address.Key != candidate.EntityId)
                throw new InvalidOperationException("ID выбранного дома неоднозначен или переместился: " + candidate.EntityId);
        }

        var orderedCandidates = selectedCandidates.OrderBy(candidate => candidate.EntityId, StringComparer.Ordinal).ToArray();
        var excludedIds = allWorldIds.Where(id => !orderedCandidates.Any(candidate => candidate.EntityId == id))
            .Order(StringComparer.Ordinal).ToArray();
        if (excludedIds.Length > HouseFamilyWindowProtocol.MaximumExcludedTargets)
            throw new InvalidOperationException("Список явных исключений превышает предел протокола.");

        var worldStart = world.PlayStart() ?? throw new InvalidOperationException("Не удалось определить точку входа активного мира.");
        var context = CampaignWorldCatalog.Discover(_studio.Workspace).Resolve(
            PlayPreparation.Campaign, "res://scenes/act1_demo.tscn", worldStart.Zone, worldStart.Spawn, baseRevision: Revision());
        var dependencies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var dependency in context.AuthoredDependencies
            .Concat(worldFiles.Select(file => file.RelativePath))
            .Append(FamilyGeneratorPath).Append(FamilyBaselineBlendPath).Append(FamilyCatalogPath)
            .Append(FamilyBaseModelFile).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, dependency);
            var path = Path.Combine(_studio.Workspace.Root, dependency.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new InvalidOperationException("Нет зависимости активного мира: " + dependency);
            var diskHash = AtomicFile.Sha256OfFile(path);
            if (sourceSnapshots.TryGetValue(dependency, out var sourceSnapshot)
                && !string.Equals(sourceSnapshot.Sha256, diskHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Исходник мира изменился на диске после загрузки Workspace: " + dependency);
            dependencies[dependency] = diskHash;
        }

        var currentModelCache = new Dictionary<(string ModelPath, string SourceNode, string CatalogId), FamilyCurrentModel>();
        var targets = new List<HouseFamilyWindowTarget>(orderedCandidates.Length);
        foreach (var candidate in orderedCandidates)
        {
            var snapshot = sourceSnapshots[candidate.RelativePath];
            var currentEntity = _studio.Workspace.File(candidate.RelativePath).Get(candidate.EntityId) as JsonObject
                ?? throw new InvalidOperationException("Выбранный дом исчез из Workspace: " + candidate.EntityId);
            var modelPath = ReadCurrentModelPath(candidate, currentEntity);
            var cacheKey = (modelPath, candidate.Component.SourceNode, candidate.Component.CatalogId);
            if (!currentModelCache.TryGetValue(cacheKey, out var currentModel))
            {
                currentModel = InspectCurrentFamilyModel(modelPath, candidate.Component, dependencies);
                currentModelCache.Add(cacheKey, currentModel);
            }
            if (currentModel.TargetEntityIds is { } ownerIds)
            {
                if (!ownerIds.Contains(candidate.EntityId, StringComparer.Ordinal))
                    throw new InvalidOperationException("Закрытая семейная модель не принадлежит выбранному объекту: " + candidate.EntityId);
            }
            else if (currentModel.Provenance.ManifestPath is not null)
            {
                throw new InvalidOperationException("Старая индивидуальная модель без списка владельцев может использоваться только одним точным объектом.");
            }

            var sourceData = JsonSerializer.SerializeToElement(currentEntity);
            var houseSource = new HouseWindowSource(candidate.RelativePath, candidate.EntityId, snapshot.BaseSha256,
                snapshot.Sha256, sourceData);
            targets.Add(new HouseFamilyWindowTarget(candidate.EntityId, candidate.EntityKind, candidate.Component.Component,
                candidate.Component.CatalogId, houseSource, currentModel.Provenance, currentModel.Widths));
        }
        var camera = world.CaptureAuthoringCamera();
        var environment = new HouseAuthoringEnvironment(context.BaseRevision, context.Campaign.Id,
            context.Campaign.ExactVersion, context.World.WorldId, context.StateId, context.World.ScenePath,
            context.World.ZoneId, context.World.SpawnPointId, camera, dependencies);
        var scopeText = scopeChoice switch
        {
            FamilySelectedScope => $"selected authored house ({orderedCandidates.Length} target)",
            FamilyGroupScope => $"frozen selected group of authored houses ({orderedCandidates.Length} compatible targets)",
            _ => $"all compatible authored houses in the loaded world ({orderedCandidates.Length} targets)"
        };
        var request = new HouseFamilyWindowRequest(Guid.NewGuid().ToString("N"), string.Empty, _prompt.Text,
            scopeText, excludedIds, targets, environment);
        request = request with { SnapshotHash = HouseFamilyWindowProtocol.ComputeSnapshotHash(request) };
        HouseFamilyWindowProtocol.ValidateRequest(request);
        _familyFrozenScopeChoice = scopeChoice;
        _familyFrozenScopeSelection = selectionIds;
        _familyDisclosedSnapshotHash = null;
        _familyBuilds.Clear();
        return request;
    }

    private bool TryCreateFamilyCandidate(string id, string path, JsonObject entity, out FamilyCandidate candidate)
    {
        candidate = null!;
        if (entity["id"] is not JsonValue entityIdNode || entityIdNode.GetValue<string>() != id
            || entity["kind"] is not JsonValue kindNode || entity["params"] is not JsonObject parameters)
            return false;
        var kind = kindNode.GetValue<string>();
        if (kind is not ("prop" or "kit-placement") || (bool?)parameters["retired"] == true) return false;
        FamilyComponentInfo component;
        if (kind == "prop")
        {
            if (parameters["catalogId"] is not JsonValue catalogIdValue
                || !TryResolveFamilyComponent(catalogIdValue.GetValue<string>(), out component)) return false;
        }
        else
        {
            if (parameters["component"] is not JsonValue componentValue
                || (string?)parameters["assetSource"] != FamilyBaseModelPath) return false;
            var placementName = componentValue.GetValue<string>();
            var match = FamilyComponents.FirstOrDefault(pair => placementName == pair.Value.SourceNode + "_Dwelling");
            if (string.IsNullOrEmpty(match.Key) || !TryResolveFamilyComponent(match.Value.CatalogId, out component)) return false;
        }
        if (!_studio.Workspace.HasFile(path) || !_studio.Workspace.File(path).Contains(id)) return false;
        candidate = new FamilyCandidate(id, kind, path, component, entity);
        return true;
    }

    private bool TryGetFamilyCandidate(string id, out FamilyCandidate candidate)
    {
        candidate = null!;
        if (_studio.Workspace.IsAmbiguous(id) || _studio.Workspace.Locate(id) is not { } address
            || !address.RelativePath.StartsWith("game/content/world/", StringComparison.Ordinal)
            || !address.RelativePath.EndsWith(".world.v1.json", StringComparison.Ordinal)
            || _studio.Workspace.File(address.RelativePath).Get(address.Key) is not JsonObject entity)
            return false;
        return TryCreateFamilyCandidate(id, address.RelativePath, entity, out candidate);
    }

    private bool TryResolveFamilyComponent(string catalogId, out FamilyComponentInfo component)
    {
        component = null!;
        var match = FamilyComponents.FirstOrDefault(pair => pair.Value.CatalogId == catalogId);
        if (string.IsNullOrEmpty(match.Key)
            || _studio.Workspace.IsAmbiguous(catalogId)
            || _studio.Workspace.Locate(catalogId) is not { } address
            || address.RelativePath != FamilyCatalogPath
            || _studio.Workspace.Get(catalogId) is not JsonObject catalogEntry
            || (string?)catalogEntry["id"] != catalogId
            || catalogEntry["source"] is not JsonObject source
            || (string?)source["scene"] != FamilyBaseModelPath
            || (string?)source["node"] != match.Value.SourceNode
            || !TryReadOrigin(catalogEntry["origin"], out var origin))
            return false;
        component = new FamilyComponentInfo(match.Key, catalogId, match.Value.SourceNode, origin);
        return true;
    }

    private FamilyCurrentModel InspectCurrentFamilyModel(string modelPath, FamilyComponentInfo component,
        Dictionary<string, string> dependencies)
    {
        var modelRelativePath = FamilyProjectPath(modelPath);
        StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, modelRelativePath);
        var modelFullPath = Path.Combine(_studio.Workspace.Root, modelRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var modelBytes = ReadBoundedFamilyFile(modelFullPath, FamilyMaximumModelBytes);
        var modelHash = AtomicFile.Sha256(modelBytes);
        dependencies[modelRelativePath] = modelHash;
        if (modelPath == FamilyBaseModelPath)
        {
            _ = ReadRuntimePropTransform(modelBytes, modelFullPath, component.SourceNode);
            var manifestless = new HouseFamilyWindowModelProvenance(modelPath, modelHash, component.SourceNode,
                component.Origin.ToArray());
            return new FamilyCurrentModel(manifestless, FamilyBaseWidths, null);
        }

        var modelId = ParseFamilyPrivateId(modelPath);
        var manifestRelativePath = modelRelativePath + ".manifest.json";
        var blendRelativePath = FamilyBlendPrefix + modelId + ".blend";
        StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, manifestRelativePath);
        StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, blendRelativePath);
        var manifestFullPath = Path.Combine(_studio.Workspace.Root, manifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var manifestBytes = ReadBoundedFamilyFile(manifestFullPath, 2 * 1024 * 1024);
        var manifestHash = AtomicFile.Sha256(manifestBytes);
        dependencies[manifestRelativePath] = manifestHash;
        var manifest = ParseStrictFamilyJson(manifestBytes);
        ValidateFamilyCurrentManifest(manifest, modelBytes, component, dependencies);
        var blendFullPath = Path.Combine(_studio.Workspace.Root, blendRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var blendBytes = ReadBoundedFamilyFile(blendFullPath, FamilyMaximumModelBytes);
        var blendHash = AtomicFile.Sha256(blendBytes);
        if ((string?)manifest["outputs"]?["blend"]?["sha256"] != blendHash)
            throw new InvalidDataException("Сохранённый BLEND семейства не совпадает с его манифестом.");
        dependencies[blendRelativePath] = blendHash;
        var privateTransform = ReadRuntimePropTransform(modelBytes, modelFullPath, component.SourceNode);
        var canonicalBytes = ReadBoundedFamilyFile(Path.Combine(_studio.Workspace.Root, FamilyBaseModelFile.Replace('/', Path.DirectorySeparatorChar)), FamilyMaximumModelBytes);
        var canonicalTransform = ReadRuntimePropTransform(canonicalBytes,
            Path.Combine(_studio.Workspace.Root, FamilyBaseModelFile.Replace('/', Path.DirectorySeparatorChar)), component.SourceNode);
        if (!TransformNearlyEqual(privateTransform, canonicalTransform))
            throw new InvalidDataException("Приватная модель семейства сдвинула каталожный компонент относительно исходной позиции.");
        var widths = ReadFamilyWidths(manifest["windowWidths"]);
        var targetIds = ReadFamilyTargetEntityIds(manifest);
        return new FamilyCurrentModel(new HouseFamilyWindowModelProvenance(modelPath, modelHash,
            component.SourceNode, component.Origin.ToArray(), manifestRelativePath, manifestHash), widths, targetIds);
    }

    private static void ValidateFamilyCurrentManifest(JsonObject manifest, byte[] glbBytes,
        FamilyComponentInfo component, IReadOnlyDictionary<string, string> dependencies)
    {
        if ((string?)manifest["schema"] != "house-variant-build-manifest/v1"
            || (string?)manifest["component"] != component.Component
            || (string?)manifest["catalogId"] != component.CatalogId
            || (string?)manifest["sourceNode"] != component.SourceNode
            || (bool?)manifest["previewTransformPreserved"] != true
            || !ReadFiniteMatrix(manifest["previewTransform"], required: true)
            || !OriginMatches(manifest["catalogOrigin"], component.Origin)
            || (string?)manifest["outputs"]?["glb"]?["path"] != "house_variant.glb"
            || (string?)manifest["outputs"]?["glb"]?["sha256"] != AtomicFile.Sha256(glbBytes)
            || (string?)manifest["outputs"]?["blend"]?["path"] != "house_variant.blend"
            || !IsSha256((string?)manifest["outputs"]?["blend"]?["sha256"])
            || (string?)manifest["source"]?["generator"]?["path"] != FamilyGeneratorPath
            || (string?)manifest["source"]?["baselineBlend"]?["path"] != FamilyBaselineBlendPath
            || (string?)manifest["source"]?["catalog"]?["path"] != FamilyCatalogPath
            || !IsSha256((string?)manifest["source"]?["generator"]?["sha256"])
            || !IsSha256((string?)manifest["source"]?["baselineBlend"]?["sha256"])
            || !IsSha256((string?)manifest["source"]?["catalog"]?["sha256"])
            || string.IsNullOrWhiteSpace((string?)manifest["source"]?["recipe"]?["path"])
            || !IsSha256((string?)manifest["source"]?["recipe"]?["sha256"]))
            throw new InvalidDataException("Приватная модель, её манифест или происхождение каталога не совпадают.");
        foreach (var key in new[] { "generator", "baselineBlend", "catalog" })
        {
            var path = (string?)manifest["source"]?[key]?["path"];
            var hash = (string?)manifest["source"]?[key]?["sha256"];
            if (path is null || hash is null || !dependencies.TryGetValue(path, out var currentHash)
                || !string.Equals(hash, currentHash, StringComparison.Ordinal))
                throw new InvalidDataException("Приватная модель семейства собрана по другой или устаревшей зависимости.");
        }
        _ = ReadFamilyWidths(manifest["windowWidths"]);
        _ = ReadFamilyTargetEntityIds(manifest);
    }

    private static IReadOnlyList<string>? ReadFamilyTargetEntityIds(JsonObject manifest)
    {
        if (manifest["targetEntityIds"] is not JsonArray ids)
        {
            var singleOwner = (string?)manifest["entityId"];
            if (string.IsNullOrWhiteSpace(singleOwner))
                throw new InvalidDataException("Приватный манифест не указывает владельца модели.");
            return Array.AsReadOnly(new[] { singleOwner });
        }
        var values = ids.Select(node => node is JsonValue value && value.TryGetValue<string>(out var id) ? id : null).ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) || !values.SequenceEqual(values.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length || values.Length == 0
            || (string?)manifest["entityId"] != values[0])
            throw new InvalidDataException("Манифест семейства содержит некорректный список владельцев.");
        return Array.AsReadOnly(values.Select(value => value!).ToArray());
    }

    private void EnsureFamilyRequestCurrent(HouseFamilyWindowRequest request, bool checkProviderPolicy = false,
        bool allowUntargetedTargetFileChanges = false)
    {
        if (!IsCurrentFamilyMode() || !ReferenceEquals(_familyRequest, request)
            || !string.Equals(_prompt.Text, request.Prompt, StringComparison.Ordinal)
            || _scope.Selected != _familyFrozenScopeChoice)
            throw new InvalidOperationException("Описание, охват или режим семейства изменились. Подготовьте новый снимок.");
        if (_familyFrozenScopeChoice is FamilySelectedScope or FamilyGroupScope)
        {
            var currentScope = _familyFrozenScopeChoice == FamilyGroupScope
                ? (((StudioWorldSection)_studio.Section("world")).Group.Count > 0
                    ? ((StudioWorldSection)_studio.Section("world")).Group.ToArray()
                    : _studio.Selection is { } one ? [one] : [])
                : _studio.Selection is { } selected ? [selected] : [];
            if (!currentScope.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .SequenceEqual(_familyFrozenScopeSelection, StringComparer.Ordinal))
                throw new InvalidOperationException("Выделенный дом или группа изменились после снимка.");
        }
        HouseFamilyWindowProtocol.ValidateRequest(request);
        var targetPaths = request.Targets.Select(target => target.Source.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var (relativePath, expectedHash) in request.Environment.DependencyHashes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, relativePath);
            var fullPath = Path.Combine(_studio.Workspace.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath)) throw new InvalidOperationException("Зависимость запроса исчезла: " + relativePath);
            var currentHash = AtomicFile.Sha256OfFile(fullPath);
            if (targetPaths.Contains(relativePath))
            {
                var file = _studio.Workspace.HasFile(relativePath) ? _studio.Workspace.File(relativePath) : null;
                if (file is null || file.Dirty || file.IsNew)
                    throw new InvalidOperationException("Целевой исходник отсутствует в чистом Workspace: " + relativePath);
                if (currentHash != expectedHash && !allowUntargetedTargetFileChanges)
                    throw new InvalidOperationException("Исходник целевого дома изменился после снимка: " + relativePath);
            }
            else if (currentHash != expectedHash)
                throw new InvalidOperationException("Зависимость изменилась после снимка: " + relativePath);
        }
        EnsureFamilyTargetsUnchanged(request, allowUntargetedTargetFileChanges);
        EnsureFamilyEnvironmentCurrent(request);
        if (checkProviderPolicy && _familyProposal is null && _disclosedGlobalInstructions is bool disclosed
            && disclosed != _globalInstructions.ButtonPressed)
            throw new InvalidOperationException("Политика глобальных инструкций Codex изменилась. Откройте раскрытие ещё раз.");
    }

    private void EnsureFamilyEnvironmentCurrent(HouseFamilyWindowRequest request)
    {
        var world = (StudioWorldSection)_studio.Section("world");
        if (!world.WorldReady || world.PlayStart() is not { } start)
            throw new InvalidOperationException("Загруженная карта или её точка входа изменилась после семейного снимка.");
        var current = CampaignWorldCatalog.Discover(_studio.Workspace).Resolve(
            PlayPreparation.Campaign, "res://scenes/act1_demo.tscn", start.Zone, start.Spawn, baseRevision: Revision());
        var frozen = request.Environment;
        if (!string.Equals(current.BaseRevision, frozen.BaseRevision, StringComparison.Ordinal)
            || !string.Equals(current.Campaign.Id, frozen.CampaignId, StringComparison.Ordinal)
            || !string.Equals(current.Campaign.ExactVersion, frozen.CampaignVersion, StringComparison.Ordinal)
            || !string.Equals(current.World.WorldId, frozen.WorldId, StringComparison.Ordinal)
            || !string.Equals(current.StateId, frozen.StateId, StringComparison.Ordinal)
            || !string.Equals(current.World.ScenePath, frozen.ScenePath, StringComparison.Ordinal)
            || !string.Equals(current.World.ZoneId, frozen.ZoneId, StringComparison.Ordinal)
            || !string.Equals(current.World.SpawnPointId, frozen.SpawnPointId, StringComparison.Ordinal))
            throw new InvalidOperationException("Кампания, мир, состояние или точка входа изменились после снимка.");
        var camera = world.CaptureAuthoringCamera();
        if (!JsonNode.DeepEquals(JsonNode.Parse(camera.GetRawText()), JsonNode.Parse(frozen.Camera.GetRawText())))
            throw new InvalidOperationException("Камера мира изменилась после снимка семейства.");
    }

    private void EnsureFamilyTargetsUnchanged(HouseFamilyWindowRequest request, bool allowUntargetedTargetFileChanges)
    {
        foreach (var group in request.Targets.GroupBy(target => target.Source.Path, StringComparer.Ordinal))
        {
            var relativePath = group.Key;
            var file = _studio.Workspace.HasFile(relativePath) ? _studio.Workspace.File(relativePath) : null;
            if (file is null || file.Dirty || file.IsNew)
                throw new InvalidOperationException("Целевой файл отсутствует в чистом Workspace: " + relativePath);
            StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, relativePath);
            var fullPath = Path.Combine(_studio.Workspace.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var bytes = ReadBoundedFamilyFile(fullPath, FamilyMaximumWorldSourceBytes);
            var text = FamilyStrictUtf8.GetString(bytes);
            EnsureNoDuplicateJsonProperties(text);
            var scratch = AuthoredFile.CreateNew(_studio.Workspace.Root, relativePath, text);
            foreach (var target in group)
            {
                if (scratch.Keys().Count(key => key == target.EntityId) != 1
                    || !JsonNode.DeepEquals(scratch.Get(target.EntityId), JsonNode.Parse(target.Source.Data.GetRawText())))
                    throw new InvalidOperationException("Целевой объект изменился после снимка: " + target.EntityId);
            }
            var expectedHash = request.Environment.DependencyHashes[relativePath];
            if (!allowUntargetedTargetFileChanges && AtomicFile.Sha256(bytes) != expectedHash)
                throw new InvalidOperationException("Файл целевого дома изменился после снимка: " + relativePath);
        }
    }

    private void RequireFamilyDisclosureAndConfirmation(HouseFamilyWindowRequest request, string actionText,
        bool directProvider, Action<HouseFamilyWindowRequest> dispatch)
    {
        var disclosureCurrent = _familyDisclosedSnapshotHash == request.SnapshotHash
            && _disclosedGlobalInstructions == _globalInstructions.ButtonPressed;
        if (!disclosureCurrent)
        {
            OpenFamilyDisclosure();
            _status.Text = FamilyReadyStatus("Сначала проверьте полный список целей, исключений и JSON. После закрытия раскрытия нажмите действие ещё раз для отдельного подтверждения.");
            return;
        }
        var directInstructions = _globalInstructions.ButtonPressed;
        var dialogText = actionText + "\n\n"
            + $"Зафиксировано целей: {request.Targets.Count}; явных исключений: {request.ExcludedEntityIds.Count}.\n"
            + $"Snapshot: {request.SnapshotHash}.\nПолитика глобальных инструкций Codex: {(directInstructions ? "включена" : "выключена")}.\n"
            + (directProvider
                ? "Прямой запрос уйдёт в Codex. Сначала Studio проверит ответ и соберёт отдельные варианты; применение останется отдельным действием."
                : "Это ручная передача. Studio не управляет внешней сессией; проект пока не меняется.");
        var approvalKey = FamilyApprovalKey(request);
        RequireScopeConfirmationCore(approvalKey, dialogText, directProvider, directInstructions,
            confirmedInstructions => DispatchFamilyScopeApproval(request, directProvider, confirmedInstructions, dispatch),
            dismissed: () =>
            {
                if (!ReferenceEquals(_familyRequest, request)) return;
                if (_familyProposal is null) _status.Text = FamilyReadyStatus("Подтверждение отменено; проект не изменён.");
            });
    }

    private void DispatchFamilyScopeApproval(HouseFamilyWindowRequest request, bool directProvider,
        bool confirmedGlobalInstructions, Action<HouseFamilyWindowRequest> dispatch)
    {
        if (!GodotObject.IsInstanceValid(this) || !Visible || !_familyMode || !ReferenceEquals(_familyRequest, request)) return;
        try
        {
            EnsureFamilyRequestCurrent(request, checkProviderPolicy: directProvider);
            if (directProvider && _globalInstructions.ButtonPressed != confirmedGlobalInstructions)
                throw new InvalidOperationException("Согласие на глобальные инструкции Codex изменилось во время подтверждения.");
            var key = FamilyApprovalKey(request);
            _approvedScopeSnapshotHash = key;
            _approvedScopeGlobalInstructions = directProvider ? confirmedGlobalInstructions : null;
            dispatch(request);
        }
        catch (Exception error) { _status.Text = FamilyReadyStatus("Запрос не передан: " + error.Message); }
    }

    private static string FamilyApprovalKey(HouseFamilyWindowRequest request) => "family:" + request.SnapshotHash;

    private async Task RunFamilyProposalCoreAsync(bool fromManual, string? proposalFile, HouseFamilyWindowRequest request)
    {
        if (!_familyMode || !Visible || _busy || _evidenceBusy || _scopeConfirmationBusy || _studio.IsWorkspaceActionBusy
            || !ReferenceEquals(_familyRequest, request) || !fromManual && _familyProposal is not null)
            return;
        HouseFamilyWindowProposal? imported = null;
        if (fromManual)
        {
            try
            {
                EnsureFamilyRequestCurrent(request);
                var json = proposalFile is null ? DisplayServer.ClipboardGet() : ReadFamilyProposalFile(proposalFile);
                imported = CodexProposalProvider.ParseProposal(json, request);
                HouseFamilyWindowProtocol.ValidateProposal(request, imported);
            }
            catch (Exception error)
            {
                _status.Text = FamilyReadyStatus("Ответ не принят: " + error.Message + " Нужен чистый JSON по этому точному запросу.");
                return;
            }
        }
        SetBusy(true);
        Interlocked.Exchange(ref _providerWarningSeen, 0);
        var cancellation = new CancellationTokenSource();
        _cancel = cancellation;
        string? providerDirectory = null;
        try
        {
            EnsureFamilyRequestCurrent(request, checkProviderPolicy: !fromManual);
            if (!fromManual && (_approvedScopeSnapshotHash != FamilyApprovalKey(request)
                || _approvedScopeGlobalInstructions != _globalInstructions.ButtonPressed))
                throw new InvalidOperationException("Прямой запрос не подтверждён для текущего снимка и политики Codex.");
            _familyPreviewReady = false;
            _familyRetryBuildAvailable = false;
            _familyBuilds.Clear();
            ClearBuildStage();
            Urman.Studio.Core.Handoff.DeveloperContext.WriteHouseFamilyWindowJob(_studio.Workspace.Root, request);
            var proposal = imported;
            if (proposal is null)
            {
                providerDirectory = Directory.CreateTempSubdirectory("urman-family-request-" + request.RequestId + "-").FullName;
                File.WriteAllText(Path.Combine(providerDirectory, "request.json"),
                    HouseFamilyWindowProtocol.SerializeRequest(request), FamilyStrictUtf8);
                _status.Text = "Codex готовит одно предложение для зафиксированного семейства…";
                proposal = await new CodexProposalProvider(allowGlobalInstructions: _globalInstructions.ButtonPressed)
                    .GenerateFamilyAsync(request, providerDirectory, cancellation.Token, this);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            if (!GodotObject.IsInstanceValid(this) || !Visible || !_familyMode || !ReferenceEquals(_familyRequest, request))
                throw new OperationCanceledException(cancellation.Token);
            EnsureFamilyRequestCurrent(request, checkProviderPolicy: !fromManual);
            HouseFamilyWindowProtocol.ValidateProposal(request, proposal);
            Urman.Studio.Core.Handoff.DeveloperContext.WriteHouseFamilyWindowJob(_studio.Workspace.Root, request, proposal);
            _familyProposal = proposal;
            _status.Text = "Ответ проверен. Собираю не более трёх вариантов A/B/C и проверяю каждый перед показом…";
            await BuildFamilyAndPreviewAsync(request, proposal, cancellation.Token);
            if (Volatile.Read(ref _providerWarningSeen) != 0)
                _status.Text += "\nCodex сообщил предупреждение; MCP и доступные инструменты отключены.";
            if (fromManual) _status.Text += "\nОтвет передан вручную; внешней сессией управлял пользователь.";
            if (_consentIoWarning is not null) _status.Text += "\n" + _consentIoWarning;
        }
        catch (OperationCanceledException)
        {
            _familyPreviewReady = false;
            _familyRetryBuildAvailable = _familyProposal is not null && _stage is not null;
            if (GodotObject.IsInstanceValid(this) && Visible)
                _status.Text = FamilyReadyStatus("Действие остановлено. Если ответ уже принят, его локальную сборку можно повторить; файлы проекта не изменены.");
        }
        catch (Exception error)
        {
            _familyPreviewReady = false;
            if (_familyProposal is not null && _stage is not null) _familyRetryBuildAvailable = true;
            if (GodotObject.IsInstanceValid(this))
                _status.Text = FamilyReadyStatus("Предложение не показано: " + error.Message + " Файлы проекта не изменены.");
        }
        finally
        {
            if (ReferenceEquals(_cancel, cancellation)) _cancel = null;
            cancellation.Dispose();
            if (providerDirectory is not null)
            {
                try { Directory.Delete(providerDirectory, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            if (GodotObject.IsInstanceValid(this))
            {
                if (!Visible)
                {
                    ResetFamilyPreparedState();
                    _familyMode = false;
                    _familyTargetPicker.Visible = false;
                }
                SetBusy(false);
            }
        }
    }

    private async Task BuildFamilyAndPreviewAsync(HouseFamilyWindowRequest request,
        HouseFamilyWindowProposal proposal, CancellationToken cancellation)
    {
        HouseFamilyWindowProtocol.ValidateProposal(request, proposal);
        EnsureFamilyRequestCurrent(request);
        ClearBuildStage();
        _familyBuilds.Clear();
        _stage = Directory.CreateTempSubdirectory("urman-family-" + request.RequestId + "-").FullName;
        foreach (var family in request.Targets.GroupBy(target => target.Component, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            cancellation.ThrowIfCancellationRequested();
            EnsureFamilyRequestCurrent(request);
            var ids = family.Select(target => target.EntityId).Order(StringComparer.Ordinal).ToArray();
            var representative = ids[0];
            var artifact = await BuildFamilyComponentAsync(request, proposal, family.Key, ids, representative, cancellation);
            _familyBuilds.Add(family.Key, artifact);
        }
        cancellation.ThrowIfCancellationRequested();
        EnsureFamilyRequestCurrent(request);
        if (!GodotObject.IsInstanceValid(this) || !Visible || !_familyMode) throw new OperationCanceledException(cancellation);
        PopulateFamilyTargetPicker(request);
        _familyPreviewReady = true;
        _familyRetryBuildAvailable = false;
        if (request.Targets.Count > 0) ShowFamilyTargetPreview(request.Targets[0].EntityId);
        if (!_familyPreviewReady || _familyPreviewTargetId != request.Targets[0].EntityId)
            throw new InvalidOperationException("Первую пару моделей не удалось загрузить для просмотра.");
        _status.Text = $"Одна ширина {proposal.WindowWidth:0.00} м для девяти окон · {request.Targets.Count} точных целей · {request.ExcludedEntityIds.Count} исключений. Проверьте пары «до / после» в списке; проект ещё не изменён.";
        SetBusy(false);
    }

    private async Task<FamilyStageArtifact> BuildFamilyComponentAsync(HouseFamilyWindowRequest request,
        HouseFamilyWindowProposal proposal, string component, IReadOnlyList<string> targetIds,
        string representative, CancellationToken cancellation)
    {
        var componentInfo = request.Targets.First(target => target.Component == component);
        var modelId = Guid.NewGuid().ToString("N");
        var modelRelative = FamilyModelFilePrefix + modelId + ".glb";
        var blendRelative = FamilyBlendPrefix + modelId + ".blend";
        var manifestRelative = modelRelative + ".manifest.json";
        foreach (var relative in new[] { modelRelative, blendRelative, manifestRelative })
        {
            StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, relative);
            var destination = Path.Combine(_studio.Workspace.Root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(destination) || Directory.Exists(destination))
                throw new IOException("Отказано перезаписывать существующий приватный вариант: " + relative);
        }

        var stage = Path.Combine(_stage ?? throw new InvalidOperationException("Временная папка сборки отсутствует."), component + "-" + modelId);
        Directory.CreateDirectory(stage);
        var recipePath = Path.Combine(stage, "recipe.json");
        var widths = new JsonObject();
        foreach (var id in FamilyWindowIds) widths[id] = proposal.WindowWidth;
        var recipe = new JsonObject
        {
            ["schema"] = "house-variant-recipe/v1",
            ["entityId"] = representative,
            ["component"] = component,
            ["windowWidths"] = widths
        };
        var recipeBytes = FamilyStrictUtf8.GetBytes(recipe.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.WriteAllBytes(recipePath, recipeBytes);
        EnsureFamilyRequestCurrent(request);
        _status.Text = $"Собираю вариант {component} для {targetIds.Count} выбранных домов…";
        await GenerateFamilyAsync(recipePath, stage, cancellation);
        cancellation.ThrowIfCancellationRequested();
        EnsureFamilyRequestCurrent(request);
        var glbPath = Path.Combine(stage, "house_variant.glb");
        var blendPath = Path.Combine(stage, "house_variant.blend");
        var manifestPath = Path.Combine(stage, "manifest.json");
        var glbBytes = ReadBoundedFamilyFile(glbPath, FamilyMaximumModelBytes);
        var blendBytes = ReadBoundedFamilyFile(blendPath, FamilyMaximumModelBytes);
        var originalManifestBytes = ReadBoundedFamilyFile(manifestPath, 2 * 1024 * 1024);
        var manifest = ParseStrictFamilyJson(originalManifestBytes);
        if ((string?)manifest["entityId"] != representative)
            throw new InvalidDataException("Сборка использовала другой представительный ID семейства.");
        manifest["targetEntityIds"] = new JsonArray(targetIds.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        var manifestBytes = FamilyStrictUtf8.GetBytes(manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.WriteAllBytes(manifestPath, manifestBytes);
        var artifact = new FamilyStageArtifact(component, modelId, FamilyPrivatePrefix + modelId + ".glb",
            modelRelative, blendRelative, manifestRelative, stage, recipePath, glbPath, blendPath, manifestPath,
            AtomicFile.Sha256(recipeBytes), AtomicFile.Sha256(glbBytes), AtomicFile.Sha256(blendBytes), AtomicFile.Sha256(manifestBytes),
            Array.AsReadOnly(targetIds.ToArray()));
        ValidateFamilyStageArtifact(request, proposal, artifact);
        _ = componentInfo;
        return artifact;
    }

    private async Task GenerateFamilyAsync(string recipePath, string outputDirectory, CancellationToken cancellation)
    {
        var blender = OperatingSystem.IsWindows()
            ? Path.Combine(StudioRoot.ToolsRoot, ".tools", "blender", "windows", "blender-4.5.12-windows-x64", "blender.exe")
            : Path.Combine(StudioRoot.ToolsRoot, ".tools", "blender", "Blender.app", "Contents", "MacOS", "Blender");
        if (!File.Exists(blender)) throw new InvalidOperationException("Нет закреплённого Blender. Запрос сохранён; модель проекта не изменена.");
        var start = new ProcessStartInfo(blender)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _studio.Workspace.Root
        };
        foreach (var argument in new[] { "--background", "--factory-startup", "--python", FamilyGeneratorPath, "--",
            "--root", _studio.Workspace.Root, "--component-only", "house-variant", "--recipe", recipePath,
            "--output-dir", outputDirectory }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Blender не запустился.");
        var output = process.StandardOutput.ReadToEndAsync(cancellation);
        var errors = process.StandardError.ReadToEndAsync(cancellation);
        try { await process.WaitForExitAsync(cancellation); }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        var stdout = await output;
        var stderr = await errors;
        if (process.ExitCode != 0)
            throw new IOException("Blender не завершил сборку семейного варианта: " + LastProcessLines(stdout, stderr));
    }

    private void PopulateFamilyTargetPicker(HouseFamilyWindowRequest request)
    {
        _familyTargetPicker.Clear();
        foreach (var target in request.Targets)
        {
            var name = _studio.Catalog.NameOf(target.EntityId);
            _familyTargetPicker.AddItem($"{target.Component} · {name} · {target.EntityId}");
            _familyTargetPicker.SetItemMetadata(_familyTargetPicker.ItemCount - 1, target.EntityId);
        }
        _familyTargetPicker.Visible = request.Targets.Count > 1;
        if (request.Targets.Count > 0) _familyTargetPicker.Select(0);
    }

    private void ShowFamilyTargetPreview(string targetId)
    {
        if (_familyRequest is not { } request || _familyProposal is null
            || !request.Targets.Any(target => target.EntityId == targetId)) return;
        try
        {
            EnsureFamilyRequestCurrent(request);
            var target = request.Targets.Single(item => item.EntityId == targetId);
            var artifact = _familyBuilds[target.Component];
            var stageBytes = ReadBoundedFamilyFile(artifact.GlbPath, FamilyMaximumModelBytes);
            if (AtomicFile.Sha256(stageBytes) != artifact.GlbSha256)
                throw new InvalidDataException("Вариант семейства изменился после сборки.");
            var sourceBytes = ReadCurrentModelBytes(target.CurrentModel.ModelPath, request.Environment.DependencyHashes);
            ClearPreviewModels();
            _beforeModel = LoadFamilyModelPreview(target, sourceBytes, target.CurrentModel.ModelPath);
            _previewModels.AddChild(_beforeModel);
            _afterModel = LoadFamilyModelPreview(target, stageBytes, artifact.ModelPath);
            _previewModels.AddChild(_afterModel);
            FramePreviewModels(_beforeModel, _afterModel);
            _familyPreviewTargetId = targetId;
            _beforeView.Disabled = false;
            _afterView.Disabled = false;
            _previewCamera.Current = true;
            ShowPreview(after: true);
            SetFamilyBusy(false);
        }
        catch (Exception error)
        {
            ClearPreviewModels();
            _familyPreviewReady = false;
            _familyRetryBuildAvailable = _familyProposal is not null;
            _status.Text = FamilyReadyStatus("Пара для выбранного дома не загружена: " + error.Message + " Проект не изменён.");
            SetFamilyBusy(false);
        }
    }

    private Node3D LoadFamilyModelPreview(HouseFamilyWindowTarget target, byte[] bytes, string modelPath)
    {
        var component = FamilyComponents[target.Component];
        var expectedName = target.EntityKind == "kit-placement" ? component.SourceNode + "_Dwelling" : component.SourceNode;
        var projectPath = FamilyProjectPath(modelPath);
        var basePath = Path.GetDirectoryName(Path.Combine(_studio.Workspace.Root, projectPath.Replace('/', Path.DirectorySeparatorChar))) ?? _studio.Workspace.Root;
        using var document = new GltfDocument();
        using var state = new GltfState();
        if (document.AppendFromBuffer(bytes, basePath, state) != Error.Ok)
            throw new InvalidDataException("Модель семейства не прошла импорт предпросмотра.");
        if (document.GenerateScene(state) is not Node3D scene)
            throw new InvalidDataException("Модель семейства не имеет 3D-корня.");
        try
        {
            Node3D selected;
            Transform3D selectedTransform;
            if (target.EntityKind == "prop")
            {
                selectedTransform = FindRuntimePropTransform(scene, component.SourceNode, out selected);
            }
            else
            {
                var matches = scene.Name.ToString() == expectedName ? [scene]
                    : scene.FindChildren(expectedName, nameof(Node3D), true, false).OfType<Node3D>().ToArray();
                if (matches.Length != 1 || matches[0] != scene && matches[0].GetParent()?.Name.ToString() != component.SourceNode)
                    throw new InvalidDataException("В модели не найден единственный каталожный жилой компонент для этого участка.");
                selected = matches[0];
                selectedTransform = new Transform3D(ComposeImportedBasis(selected), Vector3.Zero);
            }

            if (selected != scene)
            {
                var parent = selected.GetParent() ?? throw new InvalidDataException("У выбранного компонента нет исходного родителя.");
                parent.RemoveChild(selected);
                scene.Free();
            }
            ClearSceneOwnership(selected);
            selected.Transform = selectedTransform;
            var wrapper = new Node3D { Name = "FamilyPreview_" + target.Component };
            if (target.EntityKind == "prop")
            {
                var scale = ReadScalarScale(target.Source.Data);
                var origin = new Vector3((float)target.CurrentModel.CatalogOrigin[0],
                    (float)target.CurrentModel.CatalogOrigin[1], (float)target.CurrentModel.CatalogOrigin[2]);
                wrapper.Scale = Vector3.One * scale;
                wrapper.Position = -origin * scale;
            }
            else
            {
                wrapper.Scale = ReadVectorScale(target.Source.Data);
            }
            wrapper.AddChild(selected);
            return wrapper;
        }
        catch
        {
            if (GodotObject.IsInstanceValid(scene)) scene.Free();
            throw;
        }
    }

    private static Transform3D FindRuntimePropTransform(Node3D scene, string sourceNode, out Node3D selected)
    {
        var top = scene.GetChildCount() == 1 && scene.GetChild(0) is Node3D only && only.GetChildCount() > 1 ? only : scene;
        if (scene.Name.ToString() == sourceNode)
        {
            selected = scene;
            return scene.Transform;
        }
        if (top.Name.ToString() == sourceNode)
        {
            selected = top;
            return top.Transform;
        }
        var matches = top.GetChildren().OfType<Node3D>().Where(child => child.Name.ToString() == sourceNode).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("В модели не найден единственный компонент каталога " + sourceNode + ".");
        selected = matches[0];
        return top == scene ? selected.Transform : top.Transform * selected.Transform;
    }

    private byte[] ReadCurrentModelBytes(string modelPath, IReadOnlyDictionary<string, string> dependencies)
    {
        var relative = FamilyProjectPath(modelPath);
        if (!dependencies.TryGetValue(relative, out var expectedHash))
            throw new InvalidDataException("Модель не входит в замороженный набор зависимостей: " + relative);
        StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, relative);
        var full = Path.Combine(_studio.Workspace.Root, relative.Replace('/', Path.DirectorySeparatorChar));
        var bytes = ReadBoundedFamilyFile(full, FamilyMaximumModelBytes);
        if (AtomicFile.Sha256(bytes) != expectedHash)
            throw new InvalidOperationException("Текущая модель изменилась после снимка: " + relative);
        return bytes;
    }

    private void ValidateFamilyStageArtifacts(HouseFamilyWindowRequest request, HouseFamilyWindowProposal proposal)
    {
        if (_familyBuilds.Count != request.Targets.Select(target => target.Component).Distinct(StringComparer.Ordinal).Count())
            throw new InvalidDataException("Не собран вариант для каждой выбранной семьи A/B/C.");
        foreach (var artifact in _familyBuilds.Values)
            ValidateFamilyStageArtifact(request, proposal, artifact);
    }

    private void ValidateFamilyStageArtifact(HouseFamilyWindowRequest request, HouseFamilyWindowProposal proposal,
        FamilyStageArtifact artifact)
    {
        if (_stage is null) throw new InvalidDataException("Папка временной сборки удалена.");
        var root = Path.GetFullPath(_stage) + Path.DirectorySeparatorChar;
        var stageDirectory = Path.GetFullPath(artifact.Directory) + Path.DirectorySeparatorChar;
        if (!stageDirectory.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Временная сборка вышла за свою изолированную папку.");
        foreach (var path in new[] { artifact.RecipePath, artifact.GlbPath, artifact.BlendPath, artifact.ManifestPath })
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Временный артефакт сборки стал ссылкой.");
        var recipeBytes = ReadBoundedFamilyFile(artifact.RecipePath, 64 * 1024);
        var glbBytes = ReadBoundedFamilyFile(artifact.GlbPath, FamilyMaximumModelBytes);
        var blendBytes = ReadBoundedFamilyFile(artifact.BlendPath, FamilyMaximumModelBytes);
        var manifestBytes = ReadBoundedFamilyFile(artifact.ManifestPath, 2 * 1024 * 1024);
        if (AtomicFile.Sha256(recipeBytes) != artifact.RecipeSha256 || AtomicFile.Sha256(glbBytes) != artifact.GlbSha256
            || AtomicFile.Sha256(blendBytes) != artifact.BlendSha256 || AtomicFile.Sha256(manifestBytes) != artifact.ManifestSha256)
            throw new InvalidDataException("Артефакт сборки изменился после предпросмотра.");
        EnsureNoDuplicateJsonProperties(FamilyStrictUtf8.GetString(recipeBytes));
        var recipe = ParseStrictFamilyJson(recipeBytes);
        var targets = request.Targets.Where(target => target.Component == artifact.Component)
            .Select(target => target.EntityId).Order(StringComparer.Ordinal).ToArray();
        if (!targets.SequenceEqual(artifact.TargetEntityIds, StringComparer.Ordinal)
            || !targets.SequenceEqual(artifact.TargetEntityIds.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || (string?)recipe["schema"] != "house-variant-recipe/v1"
            || (string?)recipe["entityId"] != targets[0]
            || (string?)recipe["component"] != artifact.Component
            || !FamilyRecipeWidthsMatch(recipe["windowWidths"], proposal.WindowWidth))
            throw new InvalidDataException("Рецепт сборки не соответствует замороженной семье или общей ширине.");
        var manifest = ParseStrictFamilyJson(manifestBytes);
        var firstTarget = request.Targets.First(target => target.Component == artifact.Component);
        var component = FamilyComponents[artifact.Component];
        if ((string?)manifest["schema"] != "house-variant-build-manifest/v1"
            || (string?)manifest["entityId"] != targets[0]
            || (string?)manifest["component"] != artifact.Component
            || (string?)manifest["catalogId"] != firstTarget.CatalogId
            || (string?)manifest["sourceNode"] != component.SourceNode
            || !OriginMatches(manifest["catalogOrigin"], firstTarget.CurrentModel.CatalogOrigin)
            || (bool?)manifest["previewTransformPreserved"] != true
            || !ReadFiniteMatrix(manifest["previewTransform"], required: true)
            || !FamilyTargetIdsMatch(manifest["targetEntityIds"], targets)
            || !FamilyWidthsMatch(manifest["windowWidths"], proposal.WindowWidth))
            throw new InvalidDataException("Манифест сборки не соответствует выбранной семье, исходной позиции или ширине.");
        var dependencies = request.Environment.DependencyHashes;
        ValidateStageSource(manifest["source"]?["generator"], FamilyGeneratorPath, dependencies);
        ValidateStageSource(manifest["source"]?["baselineBlend"], FamilyBaselineBlendPath, dependencies);
        ValidateStageSource(manifest["source"]?["catalog"], FamilyCatalogPath, dependencies);
        if ((string?)manifest["source"]?["recipe"]?["path"] != Path.GetFullPath(artifact.RecipePath)
            || (string?)manifest["source"]?["recipe"]?["sha256"] != artifact.RecipeSha256
            || (string?)manifest["outputs"]?["glb"]?["path"] != "house_variant.glb"
            || (string?)manifest["outputs"]?["glb"]?["sha256"] != artifact.GlbSha256
            || (string?)manifest["outputs"]?["blend"]?["path"] != "house_variant.blend"
            || (string?)manifest["outputs"]?["blend"]?["sha256"] != artifact.BlendSha256)
            throw new InvalidDataException("Манифест сборки не содержит хэши фактических рецепта и выходных файлов.");
        var baseBytes = ReadCurrentModelBytes(FamilyBaseModelPath, dependencies);
        var stagedTransform = ReadRuntimePropTransform(glbBytes, artifact.GlbPath, component.SourceNode);
        var baseTransform = ReadRuntimePropTransform(baseBytes,
            Path.Combine(_studio.Workspace.Root, FamilyBaseModelFile.Replace('/', Path.DirectorySeparatorChar)), component.SourceNode);
        if (!TransformNearlyEqual(stagedTransform, baseTransform))
            throw new InvalidDataException("Сборка сдвинула положение каталожного участка перед предпросмотром.");
    }

    private static void ValidateStageSource(JsonNode? node, string expectedPath, IReadOnlyDictionary<string, string> dependencies)
    {
        if ((string?)node?["path"] != expectedPath || !dependencies.TryGetValue(expectedPath, out var expectedHash)
            || (string?)node?["sha256"] != expectedHash)
            throw new InvalidDataException("Сборка использовала другую или изменённую зависимость: " + expectedPath);
    }

    private static bool FamilyRecipeWidthsMatch(JsonNode? node, decimal width)
    {
        if (node is not JsonObject values || values.Count != FamilyWindowIds.Length) return false;
        return FamilyWindowIds.All(id => (decimal?)values[id] == width)
            && values.Select(pair => pair.Key).Order(StringComparer.Ordinal).SequenceEqual(FamilyWindowIds.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private static bool FamilyWidthsMatch(JsonNode? node, decimal width) => FamilyRecipeWidthsMatch(node, width);

    private static bool FamilyTargetIdsMatch(JsonNode? node, IReadOnlyList<string> expected) =>
        node is JsonArray values && values.Count == expected.Count
        && values.Select(value => value is JsonValue item && item.TryGetValue<string>(out var id) ? id : null)
            .SequenceEqual(expected, StringComparer.Ordinal);

    private sealed record PreparedFamilySource(string Path, StudioFileWrite Write);

    private IReadOnlyList<PreparedFamilySource> PrepareFamilySourceWrites(HouseFamilyWindowRequest request,
        HouseFamilyWindowProposal proposal)
    {
        var writes = new List<PreparedFamilySource>();
        foreach (var group in request.Targets.GroupBy(target => target.Source.Path, StringComparer.Ordinal))
        {
            var relativePath = group.Key;
            var registered = _studio.Workspace.File(relativePath);
            if (registered.Dirty || registered.IsNew)
                throw new InvalidOperationException("Сначала сохраните или отмените локальный черновик: " + relativePath);
            StudioFileTransaction.EnsureNoLinks(_studio.Workspace.Root, relativePath);
            var fullPath = Path.Combine(_studio.Workspace.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var bytes = ReadBoundedFamilyFile(fullPath, FamilyMaximumWorldSourceBytes);
            var text = FamilyStrictUtf8.GetString(bytes);
            EnsureNoDuplicateJsonProperties(text);
            var currentSha = AtomicFile.Sha256(bytes);
            var scratch = AuthoredFile.CreateNew(_studio.Workspace.Root, relativePath, text);
            foreach (var target in group)
            {
                if (scratch.Keys().Count(id => id == target.EntityId) != 1)
                    throw new InvalidOperationException("В актуальном исходнике ID цели отсутствует или повторяется: " + target.EntityId);
                var entity = scratch.Get(target.EntityId) as JsonObject
                    ?? throw new InvalidOperationException("Точная цель отсутствует в актуальном исходнике: " + target.EntityId);
                if (!JsonNode.DeepEquals(entity, JsonNode.Parse(target.Source.Data.GetRawText())))
                    throw new InvalidOperationException("Целевая запись изменилась после снимка: " + target.EntityId);
                var modelPath = _familyBuilds[target.Component].ModelPath;
                if (entity["params"] is not JsonObject parameters)
                    throw new InvalidDataException("У цели отсутствует params: " + target.EntityId);
                parameters["modelPath"] = modelPath;
                scratch.Put(target.EntityId, entity);
            }
            var outputText = scratch.Text;
            var outputBytes = FamilyStrictUtf8.GetBytes(outputText);
            if (AtomicFile.Sha256(outputBytes) == currentSha)
                throw new InvalidDataException("Приватный вариант семейства не изменяет целевой исходник.");
            writes.Add(new(relativePath, new StudioFileWrite(relativePath, currentSha, outputBytes)));
        }
        return writes;
    }

    private static void ValidatePromotedFamilyModels(StudioRoot studio, HouseFamilyWindowRequest request,
        IReadOnlyDictionary<string, FamilyStageArtifact> artifacts)
    {
        bool? applied = null;
        foreach (var target in request.Targets)
        {
            var entity = studio.Workspace.File(target.Source.Path).Get(target.EntityId) as JsonObject
                ?? throw new InvalidOperationException("Цель исчезла при импорте семейства: " + target.EntityId);
            var parameters = entity["params"] as JsonObject
                ?? throw new InvalidDataException("У цели пропал params после применения: " + target.EntityId);
            if (target.EntityKind == "kit-placement" && (string?)parameters["assetSource"] != FamilyBaseModelPath)
                throw new InvalidOperationException("Базовый assetSource постановки изменился: " + target.EntityId);
            var currentPath = parameters["modelPath"] is null
                ? target.EntityKind == "kit-placement" ? (string?)parameters["assetSource"] : FamilyBaseModelPath
                : (string?)parameters["modelPath"];
            var expectedNewPath = artifacts[target.Component].ModelPath;
            var isNew = string.Equals(currentPath, expectedNewPath, StringComparison.Ordinal);
            if (!isNew && !string.Equals(currentPath, target.CurrentModel.ModelPath, StringComparison.Ordinal))
                throw new InvalidOperationException("Применённая цель указывает на неожиданный вариант: " + target.EntityId);
            if (applied is { } previous && previous != isNew)
                throw new InvalidOperationException("Файловая транзакция оставила смешанные версии целевых домов.");
            applied = isNew;
        }
        if (applied != true) return; // ApplyFilesAsync also invokes the barrier after restoring the previous side.

        foreach (var artifact in artifacts.Values.OrderBy(item => item.Component, StringComparer.Ordinal))
        {
            var fullPath = Path.Combine(studio.Workspace.Root, artifact.ModelRelativePath.Replace('/', Path.DirectorySeparatorChar));
            StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, artifact.ModelRelativePath);
            var bytes = ReadBoundedFamilyFile(fullPath, FamilyMaximumModelBytes);
            if (AtomicFile.Sha256(bytes) != artifact.GlbSha256)
                throw new InvalidDataException("Промотированная модель не совпадает с проверенным предпросмотром.");
            using var document = new GltfDocument();
            using var state = new GltfState();
            if (document.AppendFromFile(fullPath, state) != Error.Ok)
                throw new IOException("Приватная GLB-модель семейства не прошла импорт перед обновлением мира: " + artifact.ModelRelativePath);
        }
    }

    private string ReadFamilyProposalFile(string path)
    {
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("Ответ не должен быть символической ссылкой.");
        return FamilyStrictUtf8.GetString(ReadBoundedFamilyFile(path, HouseFamilyWindowProtocol.MaximumProposalBytes));
    }

    private static string FamilyRequestDescription(HouseFamilyWindowRequest request)
    {
        var targets = string.Join("\n", request.Targets.Select(target =>
            $"{target.EntityId} · {target.EntityKind} · {target.Component} · {target.CatalogId}"));
        var excluded = request.ExcludedEntityIds.Count == 0 ? "нет" : string.Join("\n", request.ExcludedEntityIds);
        return $"Охват: {request.ScopeDescription}\nЦели ({request.Targets.Count}):\n{targets}\n\nИсключения ({request.ExcludedEntityIds.Count}):\n{excluded}";
    }

    private static string FamilyReadyStatus(string? detail = null) =>
        (detail is null ? string.Empty : detail + "\n")
        + "Можно подготовить точный снимок домов A/B/C. Общий каталожный GLB не меняется; каждое применение создаёт приватные варианты и одну обратимую транзакцию.";

    private static string ReadCurrentModelPath(FamilyCandidate candidate, JsonObject entity)
    {
        if (entity["params"] is not JsonObject parameters) throw new InvalidDataException("У цели отсутствует params.");
        if (candidate.EntityKind == "kit-placement" && (string?)parameters["assetSource"] != FamilyBaseModelPath)
            throw new InvalidDataException("Для kit-placement разрешён только исходный каталожный assetSource; приватная модель хранится отдельно в modelPath.");
        var path = parameters["modelPath"] is null
            ? candidate.EntityKind == "kit-placement" ? (string?)parameters["assetSource"] : FamilyBaseModelPath
            : (string?)parameters["modelPath"];
        if (path != FamilyBaseModelPath && !IsFamilyPrivatePath(path))
            throw new InvalidDataException("Цель использует модель вне утверждённого каталожного GLB или приватного house-variant.");
        return path!;
    }

    private static string FamilyProjectPath(string modelPath)
    {
        if (modelPath == FamilyBaseModelPath) return FamilyBaseModelFile;
        if (!IsFamilyPrivatePath(modelPath)) throw new InvalidDataException("Недопустимый путь модели семейства.");
        return "game/" + modelPath["res://".Length..];
    }

    private static bool IsFamilyPrivatePath(string? path) => path is not null
        && Regex.IsMatch(path, @"\Ares://assets/models/studio/house-variant-[0-9a-f]{32}\.glb\z", RegexOptions.CultureInvariant);

    private static string ParseFamilyPrivateId(string path)
    {
        if (!IsFamilyPrivatePath(path)) throw new InvalidDataException("Приватная модель не имеет канонического UUID.");
        return path.Substring(FamilyPrivatePrefix.Length, 32);
    }

    private static bool TryReadOrigin(JsonNode? node, out IReadOnlyList<double> origin)
    {
        origin = [];
        if (node is not JsonArray values || values.Count != 3) return false;
        var parsed = new double[3];
        for (var index = 0; index < parsed.Length; index++)
        {
            if (!TryReadFiniteNumber(values[index], out parsed[index])) return false;
        }
        origin = Array.AsReadOnly(parsed);
        return true;
    }

    private static bool OriginMatches(JsonNode? node, IReadOnlyList<double> expected) =>
        TryReadOrigin(node, out var origin) && origin.Count == expected.Count
        && origin.Zip(expected).All(pair => Math.Abs(pair.First - pair.Second) <= .000001);

    private static bool ReadFiniteMatrix(JsonNode? node, bool required)
    {
        if (node is not JsonArray rows || rows.Count != 4) return !required;
        foreach (var row in rows)
        {
            if (row is not JsonArray columns || columns.Count != 4) return false;
            foreach (var value in columns)
                if (!TryReadFiniteNumber(value, out _)) return false;
        }
        return true;
    }

    private static bool TryReadFiniteNumber(JsonNode? node, out double result)
    {
        result = 0;
        if (node is not JsonValue value) return false;
        if (value.TryGetValue<double>(out result)) return double.IsFinite(result);
        if (value.TryGetValue<decimal>(out var decimalValue))
        {
            result = (double)decimalValue;
            return double.IsFinite(result);
        }
        return false;
    }

    private static IReadOnlyDictionary<string, decimal> ReadFamilyWidths(JsonNode? node)
    {
        if (node is not JsonObject widths || widths.Count != FamilyWindowIds.Length
            || !widths.Select(pair => pair.Key).Order(StringComparer.Ordinal).SequenceEqual(FamilyWindowIds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Манифест не содержит точную карту девяти окон.");
        var result = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var id in FamilyWindowIds)
        {
            if (widths[id] is not JsonValue value || !value.TryGetValue<decimal>(out var width)
                || width < HouseFamilyWindowProtocol.MinimumWidthMeters || width > HouseFamilyWindowProtocol.MaximumWidthMeters)
                throw new InvalidDataException("Манифест содержит некорректную ширину окна: " + id);
            result.Add(id, width);
        }
        return result;
    }

    private static bool IsSha256(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string LastProcessLines(string stdout, string stderr)
    {
        var lines = (stdout + "\n" + stderr).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new string(string.Join("\n", lines.TakeLast(6)).Take(1500).ToArray());
    }

    private static byte[] ReadBoundedFamilyFile(string path, int maximumBytes)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 0 || info.Length > maximumBytes)
            throw new InvalidDataException("Файл отсутствует, пуст или превышает безопасный предел: " + path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length != info.Length) throw new IOException("Файл изменился во время чтения: " + path);
        var bytes = new byte[checked((int)input.Length)];
        var offset = 0;
        while (offset < bytes.Length)
        {
            var count = input.Read(bytes, offset, bytes.Length - offset);
            if (count == 0) throw new EndOfStreamException("Файл изменился во время чтения: " + path);
            offset += count;
        }
        if (input.ReadByte() != -1) throw new IOException("Файл вырос во время чтения: " + path);
        return bytes;
    }

    private static JsonObject ParseStrictFamilyJson(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 64 });
        EnsureNoDuplicateJsonProperties(document.RootElement);
        return JsonNode.Parse(bytes)?.AsObject() ?? throw new InvalidDataException("Ожидался JSON-объект.");
    }

    private static void EnsureNoDuplicateJsonProperties(string text)
    {
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 64 });
        EnsureNoDuplicateJsonProperties(document.RootElement);
    }

    private static void EnsureNoDuplicateJsonProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw new InvalidDataException("JSON содержит повторяющееся поле: " + property.Name);
                EnsureNoDuplicateJsonProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) EnsureNoDuplicateJsonProperties(item);
        }
    }

    private static Transform3D ReadRuntimePropTransform(byte[] bytes, string basePath, string sourceNode)
    {
        using var document = new GltfDocument();
        using var state = new GltfState();
        if (document.AppendFromBuffer(bytes, Path.GetDirectoryName(basePath) ?? string.Empty, state) != Error.Ok
            || document.GenerateScene(state) is not Node3D scene)
            throw new InvalidDataException("Каталожная GLB-модель не импортируется для проверки позиции.");
        try
        {
            return FindRuntimePropTransform(scene, sourceNode, out _);
        }
        finally { scene.Free(); }
    }

    private static bool TransformNearlyEqual(Transform3D first, Transform3D second)
    {
        static bool Near(float a, float b) => float.IsFinite(a) && float.IsFinite(b) && Mathf.Abs(a - b) <= .001f;
        return Near(first.Basis.X.X, second.Basis.X.X) && Near(first.Basis.X.Y, second.Basis.X.Y) && Near(first.Basis.X.Z, second.Basis.X.Z)
            && Near(first.Basis.Y.X, second.Basis.Y.X) && Near(first.Basis.Y.Y, second.Basis.Y.Y) && Near(first.Basis.Y.Z, second.Basis.Y.Z)
            && Near(first.Basis.Z.X, second.Basis.Z.X) && Near(first.Basis.Z.Y, second.Basis.Z.Y) && Near(first.Basis.Z.Z, second.Basis.Z.Z)
            && Near(first.Origin.X, second.Origin.X) && Near(first.Origin.Y, second.Origin.Y) && Near(first.Origin.Z, second.Origin.Z);
    }

    private static float ReadScalarScale(JsonElement source)
    {
        using var document = JsonDocument.Parse(source.GetRawText());
        var parameters = document.RootElement.GetProperty("params");
        if (!parameters.TryGetProperty("scale", out var scale)) return 1f;
        if (scale.ValueKind != JsonValueKind.Number || !scale.TryGetSingle(out var value) || !float.IsFinite(value) || value <= 0f || value > 100f)
            throw new InvalidDataException("Масштаб пропа должен быть одним конечным положительным числом.");
        return value;
    }

    private static Vector3 ReadVectorScale(JsonElement source)
    {
        using var document = JsonDocument.Parse(source.GetRawText());
        var parameters = document.RootElement.GetProperty("params");
        if (!parameters.TryGetProperty("scale", out var scale)) return Vector3.One;
        if (scale.ValueKind != JsonValueKind.Array || scale.GetArrayLength() != 3)
            throw new InvalidDataException("Масштаб kit-placement должен содержать три числа.");
        var values = scale.EnumerateArray().Select(value => value.TryGetSingle(out var number) && float.IsFinite(number) && number > 0f && number <= 100f
            ? number : throw new InvalidDataException("Масштаб kit-placement содержит недопустимое число.")).ToArray();
        return new Vector3(values[0], values[1], values[2]);
    }
}
