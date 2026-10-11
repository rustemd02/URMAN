using Godot;
using Urman.Studio.Core.AI;

namespace Urman.Studio.App;

public partial class StudioAiPanel
{
    private FenceAlignmentRequest? _fenceRequest;
    private bool _fenceMode;

    private static string FenceManualReadyStatus(string? detail = null) =>
        (detail is null ? string.Empty : detail + "\n")
        + "Ручной запрос готов. Предпросмотр и применение будут подключены на следующем этапе.";

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

    private FenceAlignmentRequest PrepareFenceRequest()
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

            EnsureFenceRequestCurrentForPanel(request);
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

    private void EnsureFenceRequestCurrentForPanel(FenceAlignmentRequest request)
    {
        if (!_fenceMode || !Visible || _studio.Selection is not null
            || !string.Equals(_prompt.Text, request.Prompt, StringComparison.Ordinal)
            || !IsCurrentFenceMode())
            throw new InvalidOperationException("Описание, выбранный пролёт или состояние панели изменились.");

        FenceAlignmentProtocol.ValidateRequest(request);
        ((StudioWorldSection)_studio.Section("world")).EnsureFenceRequestCurrent(request, Revision());
    }

    private void RefreshFenceRequestFreshness()
    {
        if (_fenceRequest is not { } request) return;
        try
        {
            EnsureFenceRequestCurrentForPanel(request);
        }
        catch (Exception error)
        {
            InvalidatePreparedRequest();
            _status.Text = FenceManualReadyStatus("Подготовленный запрос забора отозван: " + error.Message
                + " Откройте раскрытие, чтобы снять новый снимок.");
        }
    }

    private void OpenFenceDisclosure()
    {
        if (IsActionBusy || !Visible) return;
        try
        {
            var request = PrepareFenceRequest();
            var instructions = _globalInstructions.ButtonPressed ? "включены" : "выключены";
            var disclosureText = "Этот снимок предназначен для ручной передачи внешнему агенту. Studio не запускает его, "
                + "не принимает ответ, не показывает предпросмотр и не применяет изменение на этом этапе.\n"
                + $"Переключатель глобальных инструкций Codex сейчас {instructions}; ручная передача находится вне управления Studio.\n\n"
                + FenceAlignmentProtocol.Describe(request)
                + "\n\nПолный канонический JSON запроса:\n"
                + FenceAlignmentProtocol.SerializeRequest(request);

            ShowDisclosure(disclosureText, rememberGlobalInstructions: false);
        }
        catch (Exception error)
        {
            _status.Text = FenceManualReadyStatus("Не удалось подготовить раскрытие запроса забора: " + error.Message);
        }
    }

    private void RequireFenceScopeConfirmation(FenceAlignmentRequest request, string actionText,
        Action<FenceAlignmentRequest> dispatch)
    {
        var globalInstructions = _globalInstructions.ButtonPressed;
        var description = FenceAlignmentProtocol.Describe(request);
        var dialogText = actionText + "\n\n" + description
            + $"\nГлобальные инструкции Codex сейчас {(globalInstructions ? "включены" : "выключены")}; при ручной передаче Studio ими не управляет."
            + "\nОтвет агента здесь не принимается, предпросмотр и применение пока не подключены.";
        RequireScopeConfirmationCore(request.SnapshotHash, dialogText, directProvider: false,
            globalInstructions,
            _ => DispatchFenceScopeApproval(request, dispatch),
            dismissed: () =>
            {
                if (!ReferenceEquals(_fenceRequest, request)) return;
                InvalidatePreparedRequest();
                _status.Text = FenceManualReadyStatus("Подтверждение отменено; запрос забора отозван.");
            });
    }

    private void DispatchFenceScopeApproval(FenceAlignmentRequest request, Action<FenceAlignmentRequest> dispatch)
    {
        if (!GodotObject.IsInstanceValid(this)) return;
        if (!Visible || IsActionBusy || !ReferenceEquals(_fenceRequest, request)
            || !string.Equals(_fenceRequest?.SnapshotHash, request.SnapshotHash, StringComparison.Ordinal))
        {
            _status.Text = FenceManualReadyStatus("Запрос не передан: снимок изменился или панель закрыта.");
            return;
        }

        try
        {
            EnsureFenceRequestCurrentForPanel(request);
            var sameApproval = string.Equals(_approvedScopeSnapshotHash, request.SnapshotHash, StringComparison.Ordinal);
            _approvedScopeSnapshotHash = request.SnapshotHash;
            if (!sameApproval) _approvedScopeGlobalInstructions = null;
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
            RequireFenceScopeConfirmation(request, "Studio скопирует этот снимок для ручной передачи агенту.",
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
            RequireFenceScopeConfirmation(request, "Studio сохранит этот снимок локально для ручной передачи агенту.",
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
}
