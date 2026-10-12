using Godot;
using System.Text;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

public partial class StudioAiPanel
{
    private FenceAlignmentRequest? _fenceRequest;
    private FenceAlignmentProposal? _fenceProposal;
    private bool _fencePreviewActive;
    private bool _fencePreviewRestoreFailed;
    private bool _fenceMode;

    internal bool HasTransientFencePreview
    {
        get
        {
            if (_fencePreviewActive || _fencePreviewRestoreFailed) return true;
            try { return ((StudioWorldSection)_studio.Section("world")).HasTransientFencePreview; }
            catch (Exception) { return false; }
        }
    }

    private static string FenceManualReadyStatus(string? detail = null) =>
        (detail is null ? string.Empty : detail + "\n")
        + "Можно подготовить запрос забора. Проверенное изменение сначала показывается во временном предпросмотре, затем его можно применить одной обратимой транзакцией.";

    private bool IsCurrentFenceMode()
    {
        if (!_fenceMode || _studio.Selection is not null) return false;
        try
        {
            return ((StudioWorldSection)_studio.Section("world")).HasFenceSelection;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private FenceAlignmentRequest PrepareFenceRequest(bool checkProviderPolicy = false)
    {
        if (!Visible || !IsCurrentFenceMode())
            throw new InvalidOperationException("Выбор забора изменился или больше не доступен. Выберите пролёт снова.");
        if (string.IsNullOrWhiteSpace(_prompt.Text))
            throw new InvalidOperationException("Опишите нужное изменение забора.");

        var world = (StudioWorldSection)_studio.Section("world");
        var request = _fenceRequest;
        try
        {
            if (request is null)
            {
                request = world.CaptureFenceRequest(_prompt.Text, Revision());
                FenceAlignmentProtocol.ValidateRequest(request);
                _fenceRequest = request;
            }

            EnsureFenceRequestCurrentForPanel(request, checkProviderPolicy);
            SetBusy(_busy);
            return request;
        }
        catch
        {
            if (request is not null && ReferenceEquals(_fenceRequest, request))
                InvalidatePreparedRequest();
            throw;
        }
    }

    private void EnsureFenceRequestCurrentForPanel(FenceAlignmentRequest request, bool checkProviderPolicy = false)
    {
        if (!_fenceMode || !Visible || _studio.Selection is not null
            || !string.Equals(_prompt.Text, request.Prompt, StringComparison.Ordinal)
            || !IsCurrentFenceMode())
            throw new InvalidOperationException("Описание, выбранный пролёт или состояние панели изменились.");

        FenceAlignmentProtocol.ValidateRequest(request);
        ((StudioWorldSection)_studio.Section("world")).EnsureFenceRequestCurrent(request, Revision());
        if (checkProviderPolicy && _fenceProposal is null && _disclosedGlobalInstructions is bool disclosed
            && disclosed != _globalInstructions.ButtonPressed)
            throw new InvalidOperationException("Политика глобальных инструкций Codex изменилась. Откройте «Что уйдёт агенту…» ещё раз.");
    }

    private void RefreshFenceRequestFreshness()
    {
        if (_busy || _studio.IsWorkspaceActionBusy) return;
        if (_fenceRequest is not { } request) return;
        if (_fencePreviewRestoreFailed) return;
        if (_fencePreviewActive)
        {
            try
            {
                var world = (StudioWorldSection)_studio.Section("world");
                if (world.HasTransientFencePreview)
                {
                    EnsureFenceRequestCurrentForPanel(request);
                    return;
                }
            }
            catch (Exception error)
            {
                if (!InvalidatePreparedRequest()) return;
                _status.Text = FenceManualReadyStatus("Предпросмотр снят: снимок или выбранный пролёт изменился. "
                    + error.Message + " Подготовьте новый запрос.");
                return;
            }

            if (!InvalidatePreparedRequest()) return;
            _status.Text = FenceManualReadyStatus("Временный предпросмотр был снят после изменения исходника. Ответ отозван.");
            return;
        }
        try
        {
            EnsureFenceRequestCurrentForPanel(request);
        }
        catch (Exception error)
        {
            if (!InvalidatePreparedRequest()) return;
            _status.Text = FenceManualReadyStatus("Подготовленный запрос забора отозван: " + error.Message
                + " Откройте раскрытие, чтобы снять новый снимок.");
        }
    }

    private bool TryClearFencePreviewBeforeForget()
    {
        if (_fenceProposal is null && !_fencePreviewActive && !_fencePreviewRestoreFailed
            && !HasTransientFencePreview)
            return true;

        try
        {
            var world = (StudioWorldSection)_studio.Section("world");
            world.ClearFenceAiPreview();
            if (world.HasTransientFencePreview)
                throw new InvalidOperationException("Временный предпросмотр всё ещё удерживает сцену.");
            _fencePreviewActive = false;
            _fencePreviewRestoreFailed = false;
            _fenceProposal = null;
            return true;
        }
        catch (Exception error)
        {
            _fencePreviewActive = true;
            _fencePreviewRestoreFailed = true;
            _status.Text = "Действие остановлено: не удалось снять временный предпросмотр забора. Исправьте исходник или состояние сцены и повторите остановку. "
                + error.Message;
            UpdateBusyState();
            if (GodotObject.IsInstanceValid(_studio)) _studio.RefreshStatus();
            return false;
        }
    }

    private void OpenFenceDisclosure()
    {
        if (IsActionBusy || !Visible) return;
        try
        {
            var request = PrepareFenceRequest();
            var instructions = _globalInstructions.ButtonPressed ? "включены" : "выключены";
            var disclosureText = "Снимок привязан к выбранному участку и пролёту. При прямом запуске Studio передаёт его в Codex; при ручной передаче внешней сессией управляет пользователь.\n"
                + "Ответ проверяется по снимку и показывается как временный предпросмотр в мире. Для непустого проверенного изменения доступно отдельное обратимое применение.\n"
                + $"Политика глобальных инструкций для прямого запуска сейчас: {instructions}; подтверждение повторно покажет это значение.\n\n"
                + FenceAlignmentProtocol.Describe(request)
                + "\n\nПолный канонический JSON запроса:\n"
                + FenceAlignmentProtocol.SerializeRequest(request);

            ShowDisclosure(disclosureText, rememberGlobalInstructions: true);
        }
        catch (Exception error)
        {
            _status.Text = FenceManualReadyStatus("Не удалось подготовить раскрытие запроса забора: " + error.Message);
        }
    }

    private void RequireFenceScopeConfirmation(FenceAlignmentRequest request, string actionText, bool directProvider,
        Action<FenceAlignmentRequest> dispatch)
    {
        var globalInstructions = _globalInstructions.ButtonPressed;
        var description = FenceAlignmentProtocol.Describe(request);
        var dialogText = actionText + "\n\n" + description
            + $"\nГлобальные инструкции Codex {(directProvider ? "для прямого запроса" : "сейчас")} {(globalInstructions ? "включены" : "выключены")}."
            + (directProvider
                ? "\nStudio примет только ответ по этому запросу и покажет временный предпросмотр в мире; Apply потребуется нажать отдельно."
                : "\nОтвет проверяется по этому запросу и показывается как временный предпросмотр; внешней сессией Studio не управляет.");
        RequireScopeConfirmationCore(FenceApprovalKey(request), dialogText, directProvider,
            globalInstructions,
            confirmedGlobalInstructions => DispatchFenceScopeApproval(request, directProvider, confirmedGlobalInstructions, dispatch),
            dismissed: () =>
            {
                if (!ReferenceEquals(_fenceRequest, request)) return;
                if (!InvalidatePreparedRequest()) return;
                _status.Text = FenceManualReadyStatus("Подтверждение отменено; запрос забора отозван.");
            });
    }

    private static string FenceApprovalKey(FenceAlignmentRequest request) => "fence:" + request.SnapshotHash;

    private void DispatchFenceScopeApproval(FenceAlignmentRequest request, bool directProvider,
        bool globalInstructions, Action<FenceAlignmentRequest> dispatch)
    {
        if (!GodotObject.IsInstanceValid(this)) return;
        if (!Visible || IsActionBusy || !ReferenceEquals(_fenceRequest, request)
            || !string.Equals(_fenceRequest?.SnapshotHash, request.SnapshotHash, StringComparison.Ordinal)
            || directProvider && _globalInstructions.ButtonPressed != globalInstructions)
        {
            _status.Text = FenceManualReadyStatus("Запрос не передан: снимок изменился, политика Codex изменилась или панель закрыта.");
            return;
        }

        try
        {
            EnsureFenceRequestCurrentForPanel(request);
            if (directProvider && _globalInstructions.ButtonPressed != globalInstructions)
                throw new InvalidOperationException("Согласие на глобальные инструкции Codex изменилось во время подтверждения.");
            var key = FenceApprovalKey(request);
            var sameApproval = string.Equals(_approvedScopeSnapshotHash, key, StringComparison.Ordinal);
            _approvedScopeSnapshotHash = key;
            if (directProvider) _approvedScopeGlobalInstructions = globalInstructions;
            else if (!sameApproval) _approvedScopeGlobalInstructions = null;
            dispatch(request);
        }
        catch (Exception error)
        {
            _status.Text = FenceManualReadyStatus("Запрос не передан: " + error.Message);
        }
    }

    private void CopyFenceRequest()
    {
        if (IsActionBusy || !Visible) return;
        try
        {
            var request = PrepareFenceRequest();
            RequireFenceScopeConfirmation(request, "Studio скопирует этот снимок для ручной передачи агенту.", directProvider: false,
                confirmedRequest =>
                {
                    DisplayServer.ClipboardSet(FenceAlignmentProtocol.CreateClipboardPrompt(confirmedRequest));
                    _status.Text = FenceManualReadyStatus("Запрос скопирован для ручной передачи; Studio не управляет внешней сессией.");
                });
        }
        catch (Exception error)
        {
            _status.Text = FenceManualReadyStatus("Запрос не скопирован: " + error.Message);
        }
    }

    private void SaveFenceRequest()
    {
        if (IsActionBusy || !Visible) return;
        try
        {
            var request = PrepareFenceRequest();
            RequireFenceScopeConfirmation(request, "Studio сохранит этот снимок локально для ручной передачи агенту.", directProvider: false,
                confirmedRequest =>
                {
                    var path = Urman.Studio.Core.Handoff.DeveloperContext.WriteFenceAlignmentRequest(
                        _studio.Workspace.Root, confirmedRequest);
                    _status.Text = FenceManualReadyStatus("Запрос забора сохранён для ручной передачи; проект не изменён.");
                    OS.ShellShowInFileManager(path);
                });
        }
        catch (Exception error)
        {
            _status.Text = FenceManualReadyStatus("Запрос не сохранён: " + error.Message
                + " Существующий файл не перезаписывается.");
        }
    }

    private Task AskFenceAsync()
    {
        if (IsActionBusy || !Visible || !_fenceMode || _fenceProposal is not null) return Task.CompletedTask;
        try
        {
            var request = PrepareFenceRequest(checkProviderPolicy: true);
            RequireFenceScopeConfirmation(request,
                "Studio отправит этот запрос в Codex и покажет только временный предпросмотр забора.",
                directProvider: true,
                confirmedRequest => _ = RunFenceProposalAsync(fromManual: false, preparedRequest: confirmedRequest));
        }
        catch (Exception error) { _status.Text = FenceManualReadyStatus("Запрос не отправлен: " + error.Message); }
        return Task.CompletedTask;
    }

    private void OpenFenceProposalFile()
    {
        if (IsActionBusy || !Visible || !_fenceMode || _fenceProposal is not null) return;
        try
        {
            var request = PrepareFenceRequest();
            var requestId = request.RequestId;
            var picker = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem,
                Title = "Открыть JSON-ответ о выпрямлении забора", UseNativeDialog = true,
                Filters = ["*.json ; Ответ о выпрямлении забора"]
            };
            picker.FileSelected += path =>
            {
                CloseRootPopup(picker);
                if (IsActionBusy || !Visible || !_fenceMode || _fenceRequest?.RequestId != requestId
                    || !ReferenceEquals(_fenceRequest, request))
                {
                    _status.Text = FenceManualReadyStatus("Ответ не открыт: запрос изменился или панель недоступна.");
                    return;
                }
                string proposalJson;
                try
                {
                    EnsureFenceRequestCurrentForPanel(request);
                    proposalJson = ReadFenceProposalFile(path);
                }
                catch (Exception error)
                {
                    _status.Text = FenceManualReadyStatus("Ответ не открыт: " + error.Message);
                    return;
                }
                RequireFenceScopeConfirmation(request,
                    "Studio проверит выбранный JSON-ответ и покажет его как временный предпросмотр.",
                    directProvider: false,
                    confirmedRequest => _ = RunFenceProposalAsync(fromManual: true, manualProposalJson: proposalJson,
                        preparedRequest: confirmedRequest));
            };
            picker.Canceled += () => CloseRootPopup(picker);
            ShowRootPopup(picker, () => picker.PopupCentered(new Vector2I(900, 600)));
        }
        catch (Exception error) { _status.Text = FenceManualReadyStatus("Файл ответа не открыт: " + error.Message); }
    }

    private Task RunFenceManualClipboardAsync()
    {
        if (IsActionBusy || !Visible || !_fenceMode || _fenceProposal is not null) return Task.CompletedTask;
        try
        {
            var request = PrepareFenceRequest();
            var proposalJson = DisplayServer.ClipboardGet();
            RequireFenceScopeConfirmation(request,
                "Studio проверит вставленный JSON-ответ и покажет его как временный предпросмотр.",
                directProvider: false,
                confirmedRequest => _ = RunFenceProposalAsync(fromManual: true, manualProposalJson: proposalJson,
                    preparedRequest: confirmedRequest));
        }
        catch (Exception error) { _status.Text = FenceManualReadyStatus("Ответ не принят: " + error.Message); }
        return Task.CompletedTask;
    }

    private static string ReadFenceProposalFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        const int maximumBytes = 8 * 1024;
        if (stream.Length > maximumBytes)
            throw new InvalidDataException("Ответ о выпрямлении забора превышает ограничение 8 КиБ.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
            throw new InvalidDataException("Файл ответа изменился во время чтения и превышает ограничение 8 КиБ.");
        return new UTF8Encoding(false, true).GetString(bytes);
    }

    private async Task RunFenceProposalAsync(bool fromManual, string? proposalFile = null,
        FenceAlignmentRequest? preparedRequest = null, string? manualProposalJson = null)
    {
        if (IsActionBusy || !Visible || !_fenceMode || _fenceProposal is not null) return;
        if (!fromManual && (preparedRequest is null || !ReferenceEquals(_fenceRequest, preparedRequest))) return;

        FenceAlignmentProposal? imported = null;
        FenceAlignmentRequest? request = null;
        if (fromManual)
        {
            try
            {
                request = preparedRequest ?? _fenceRequest
                    ?? throw new InvalidOperationException("Сначала подготовьте запрос забора.");
                if (!ReferenceEquals(_fenceRequest, request))
                    throw new InvalidOperationException("Снимок запроса заменён.");
                EnsureFenceRequestCurrentForPanel(request);
                var json = manualProposalJson ?? (proposalFile is null ? DisplayServer.ClipboardGet() : ReadFenceProposalFile(proposalFile));
                imported = FenceAlignmentProtocol.ParseProposal(json, request);
                FenceAlignmentProtocol.ValidateProposal(request, imported);
            }
            catch (Exception error)
            {
                _status.Text = FenceManualReadyStatus("Ответ не принят: " + error.Message
                    + " Нужен чистый JSON v3 именно для подготовленного запроса.");
                return;
            }
        }

        SetBusy(true);
        Interlocked.Exchange(ref _providerWarningSeen, 0);
        _cancel = new CancellationTokenSource();
        string? workingSnapshotDirectory = null;
        var previewAttempted = false;
        try
        {
            request ??= preparedRequest ?? throw new InvalidOperationException("Запрос забора ещё не подготовлен.");
            EnsureFenceRequestCurrentForPanel(request, checkProviderPolicy: !fromManual);
            if (!ReferenceEquals(_fenceRequest, request))
                throw new InvalidOperationException("Снимок запроса заменён до отправки.");
            if (!fromManual && (_approvedScopeGlobalInstructions != _globalInstructions.ButtonPressed
                || !string.Equals(_approvedScopeSnapshotHash, FenceApprovalKey(request), StringComparison.Ordinal)))
                throw new InvalidOperationException("Прямой запрос не подтверждён для текущего снимка и согласия.");

            workingSnapshotDirectory = Directory.CreateTempSubdirectory("urman-fence-" + request.RequestId + "-").FullName;
            File.WriteAllText(Path.Combine(workingSnapshotDirectory, "request.json"),
                FenceAlignmentProtocol.SerializeRequest(request), new UTF8Encoding(false));
            _status.Text = fromManual ? "Проверяю ответ по точному запросу…" : "Codex готовит предложение для временного предпросмотра…";
            var acceptedProposal = imported ?? await new CodexProposalProvider(allowGlobalInstructions: _globalInstructions.ButtonPressed)
                .RunAsync(request, workingSnapshotDirectory, _cancel.Token, this);
            _cancel.Token.ThrowIfCancellationRequested();
            if (!Visible || !_fenceMode || !ReferenceEquals(_fenceRequest, request))
                throw new InvalidOperationException("Панель или запрос изменились до показа ответа.");
            EnsureFenceRequestCurrentForPanel(request, checkProviderPolicy: !fromManual);
            FenceAlignmentProtocol.ValidateProposal(request, acceptedProposal);
            if (!fromManual && _approvedScopeGlobalInstructions != _globalInstructions.ButtonPressed)
                throw new InvalidOperationException("Согласие на глобальные инструкции изменилось до ответа.");

            Urman.Studio.Core.Handoff.DeveloperContext.WriteFenceAlignmentJob(
                _studio.Workspace.Root, request, acceptedProposal);
            _fenceProposal = acceptedProposal;
            previewAttempted = true;
            ((StudioWorldSection)_studio.Section("world")).PreviewFenceProposal(request, acceptedProposal, Revision());
            _fencePreviewActive = ((StudioWorldSection)_studio.Section("world")).HasTransientFencePreview;
            _fencePreviewRestoreFailed = false;
            _status.Text = _fencePreviewActive
                ? "Предложение показано во временном предпросмотре в мире. Исходник участков ещё не изменён; проверьте пролёт и нажмите «Применить» отдельно."
                : "Предложение принято, но оно не меняет выбранный пролёт; временный предпросмотр не создан и применять нечего.";
            if (fromManual) _status.Text += "\nОтвет перенесён вручную; Studio не управляет внешней сессией.";
            if (Volatile.Read(ref _providerWarningSeen) != 0)
                _status.Text += "\nCodex сообщил предупреждение. MCP и доступные инструменты отключены.";
            if (_consentIoWarning is not null) _status.Text += "\n" + _consentIoWarning;
        }
        catch (OperationCanceledException)
        {
            if (previewAttempted && !TryClearFencePreviewBeforeForget()) return;
            if (InvalidatePreparedRequest())
                _status.Text = FenceManualReadyStatus("Остановлено; временный предпросмотр снят, исходник участков не изменён.");
        }
        catch (Exception error)
        {
            if (previewAttempted && !TryClearFencePreviewBeforeForget()) return;
            if (!InvalidatePreparedRequest()) return;
            _status.Text = FenceManualReadyStatus("Предложение не показано: " + error.Message
                + " Исходник участков не изменён.");
        }
        finally
        {
            _cancel.Dispose();
            _cancel = null;
            if (workingSnapshotDirectory is not null)
            {
                try { Directory.Delete(workingSnapshotDirectory, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            SetBusy(false);
        }
    }

    private async Task ApplyFenceAsync()
    {
        if (!_fenceMode || !Visible || _busy || _evidenceBusy || _scopeConfirmationBusy
            || _studio.IsWorkspaceActionBusy || _fencePreviewRestoreFailed
            || _fenceRequest is not { } request || _fenceProposal is not { PostIndices.Count: > 0 } proposal
            || !_fencePreviewActive || !HasTransientFencePreview || !IsCurrentFenceMode())
            return;

        var world = (StudioWorldSection)_studio.Section("world");
        StudioWorldSection.FenceApplyPlan? plan = null;
        var committed = false;
        SetBusy(true);
        try
        {
            plan = world.BeginFenceApply(request, proposal, Revision());
            const string label = "выпрямить выбранный пролёт забора";
            var write = plan.Write;
            await _studio.Session.ApplyFilesAsync(label,
                "urman.studio:" + FenceAlignmentProposal.Operation + "/" + request.RequestId,
                [write], new HashSet<string>([write.RelativePath], StringComparer.Ordinal),
                static () => { }, () => world.RefreshFenceApplyAfterPromotionAsync(plan));

            var history = _studio.Session.History().FirstOrDefault();
            if (history is null || history.Label != label || history.EntityChangeCount != 0
                || history.TransactionId is null || history.Paths.Count != 1
                || history.Paths[0] != write.RelativePath)
                throw new InvalidOperationException("Studio не подтвердила единственную файловую транзакцию забора.");
            if (!world.FinishFenceApply(plan, applied: true))
                throw new InvalidOperationException("Сохранённый файл и игровой вид не удалось подтвердить как одну версию.");
            committed = true;
            _fencePreviewActive = false;
            _fencePreviewRestoreFailed = false;
            if (!InvalidatePreparedRequest())
            {
                _status.Text = "Забор применён одной транзакцией " + history.TransactionId
                    + "; безопасно очистить снимок панели не удалось. Не запускайте повторное применение.";
                return;
            }
            _status.Text = "Выбранный пролёт выпрямлен. ⌘/Ctrl+Z отменит единственную транзакцию "
                + history.TransactionId + "; повторить можно через Redo без нового запроса.";
        }
        catch (Exception error)
        {
            if (plan is not null && !committed)
            {
                if (!world.FinishFenceApply(plan, applied: false))
                {
                    _fencePreviewActive = true;
                    _fencePreviewRestoreFailed = true;
                    _status.Text = "Транзакция не подтверждена; исходник или игровой вид остаётся заблокирован до восстановления безопасного состояния. "
                        + error.Message;
                    return;
                }
                _fencePreviewActive = false;
                _fencePreviewRestoreFailed = false;
                if (!InvalidatePreparedRequest()) return;
                _status.Text = "Изменение не применено; исходник и игровой вид подтверждённо восстановлены. " + error.Message;
                return;
            }
            _status.Text = committed
                ? "Изменение уже применено; Studio не будет повторять транзакцию автоматически. " + error.Message
                : "Предложение не применено: " + error.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }
}
