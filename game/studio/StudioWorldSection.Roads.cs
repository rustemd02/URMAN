using System.Text;
using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;
using Urman.Godot;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

public sealed partial class StudioWorldSection
{
    private const string RoadCodexConsentPath = ".urman-studio/codex-pilot.json";
    private const string OpenPartRoadFile = "game/content/world/act1_open_part.world.v1.json";
    private const string FarBankRoadFile = "game/content/world/act1_far_bank.world.v1.json";
    private const string RoadSmoothOwnerPrefix = "urman.studio:" + RoadSegmentSmoothSource.Operation + "/";

    private sealed record RoadEditorInputs(
        AuthoredSourceSnapshot OpenPart,
        AuthoredSourceSnapshot FarBank,
        IReadOnlyList<RoadSourceRecord> OpenPartRoads,
        IReadOnlyList<RoadSourceRecord> FarBankRoads,
        IReadOnlyList<RoadSourceRecord> FixedRoads)
    {
        public AuthoredSourceSnapshot Source(string path) => path switch
        {
            OpenPartRoadFile => OpenPart,
            FarBankRoadFile => FarBank,
            _ => throw new InvalidOperationException("Неизвестный источник дорожек.")
        };

        public IReadOnlyList<RoadSourceRecord> Roads(string path) => path switch
        {
            OpenPartRoadFile => OpenPartRoads,
            FarBankRoadFile => FarBankRoads,
            _ => throw new InvalidOperationException("Неизвестный источник дорожек.")
        };

        public IReadOnlyList<RoadSourceRecord> OtherRoads(string path) => path switch
        {
            OpenPartRoadFile => FarBankRoads,
            FarBankRoadFile => OpenPartRoads,
            _ => throw new InvalidOperationException("Неизвестный источник дорожек.")
        };
    }

    private sealed record RoadEditorPreview(
        string SourcePath,
        string RoadId,
        int StartIndex,
        int EndIndex,
        double Strength,
        double MaxDeviation,
        RoadSegmentSmoothProposal? Proposal,
        RoadEditorInputs Inputs,
        string CandidateText,
        string CandidateHash,
        RoadSourceRecord BeforeRoad,
        RoadSourceRecord CandidateRoad);

    private AcceptDialog? _roadEditorDialog;
    private bool _roadAiBusy;

    internal bool IsRoadAiBusy => _roadAiBusy;

    private void OpenRoadEditor()
    {
        if (_roadEditorDialog is { } existing && GodotObject.IsInstanceValid(existing))
        {
            existing.PopupCentered(new Vector2I(820, 640));
            return;
        }
        if (!WorldReady || _demo is null || studio.IsWorkspaceActionBusy || studio.IsAiActionBusy)
        {
            studio.ShowBanner("Для сглаживания дорожки сначала загрузите карту и завершите текущее действие Studio.", error: false);
            return;
        }

        RoadEditorInputs captured;
        Act1ConnectedWorld? openedWorld;
        try
        {
            captured = CaptureRoadEditorInputs();
            openedWorld = CurrentRoadEditorWorld();
            if (openedWorld is null)
                throw new InvalidOperationException("Текущий мир деревни недоступен.");
        }
        catch (Exception error)
        {
            studio.ShowBanner("Редактор дорожек не открыт: " + error.Message, error: true);
            return;
        }

        var openedDemo = _demo;
        var openedView = _view;
        var openedSelection = studio.Selection;
        var dialog = new AcceptDialog
        {
            Title = "Сгладить участок дорожки",
            OkButtonText = "Закрыть",
            MinSize = new Vector2I(820, 620)
        };
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(800, 580),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        dialog.AddChild(scroll);
        var layout = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(layout);
        layout.AddChild(new Label
        {
            Text = "Выберите исходник, дорожку и диапазон точек (границы включительно; не менее пяти точек). Предпросмотр показывает только линию и контрольные точки: рельеф и столкновения до применения не меняются.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(760, 54)
        });

        var sourceChoice = new OptionButton();
        sourceChoice.AddItem("Открытая часть деревни");
        sourceChoice.AddItem("Заречье — дальний берег");
        layout.AddChild(Row("Источник:", sourceChoice));

        var roadChoice = new OptionButton();
        layout.AddChild(Row("Дорожка:", roadChoice));
        var countLabel = new Label { ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        layout.AddChild(countLabel);

        var startIndex = new SpinBox { MinValue = 0, MaxValue = 0, Step = 1, Value = 0, Prefix = "с точки " };
        var endIndex = new SpinBox { MinValue = 4, MaxValue = 4, Step = 1, Value = 4, Prefix = "по точку " };
        layout.AddChild(Row("Диапазон:", startIndex, endIndex));
        var strength = new SpinBox { MinValue = .05, MaxValue = 1, Step = .05, Value = .6, Prefix = "сила ", Suffix = "" };
        var deviation = new SpinBox { MinValue = .05, MaxValue = 1, Step = .05, Value = .35, Suffix = " м" };
        layout.AddChild(Row("Ручные параметры:", strength, new Label { Text = "макс. сдвиг точки", ThemeTypeVariation = "MutedLabel" }, deviation));

        var prompt = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 86),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            TabInputMode = false,
            PlaceholderText = "Например: сгладь неровный поворот, не смещая стыки с соседними дорогами"
        };
        layout.AddChild(new Label { Text = "Что изменить", ThemeTypeVariation = "MutedLabel" });
        layout.AddChild(prompt);
        var globalInstructions = new CheckButton { Text = "Использовать мои глобальные инструкции Codex" };
        globalInstructions.TooltipText = "При включении Codex также может прочитать только ~/.codex/AGENTS.md. Инструменты, MCP и запись проекта остаются отключены.";
        layout.AddChild(globalInstructions);

        var previewButton = new Button { Text = "Показать предпросмотр", ThemeTypeVariation = "PrimaryButton" };
        var askCodexButton = new Button { Text = "Спросить Codex" };
        var stopCodexButton = new Button { Text = "Остановить Codex", Disabled = true };
        var applyButton = new Button { Text = "Применить — один шаг Undo", Disabled = true };
        var actionRow = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actionRow.AddChild(askCodexButton);
        actionRow.AddChild(stopCodexButton);
        actionRow.AddChild(previewButton);
        actionRow.AddChild(applyButton);
        layout.AddChild(actionRow);
        var status = new Label
        {
            Text = "Запрос Codex передаёт всю выбранную дорожку, выбранный диапазон и геометрию других дорожных осей для сохранения стыков. Codex предлагает только параметры сглаживания; проект не меняется до отдельного «Применить».",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(760, 64)
        };
        layout.AddChild(status);

        RoadEditorPreview? preview = null;
        RoadSegmentSmoothRequest? acceptedRequest = null;
        RoadSegmentSmoothProposal? acceptedProposal = null;
        CancellationTokenSource? roadCancellation = null;
        ConfirmationDialog? pendingDisclosure = null;
        string? consentWarning = null;
        Node3D? overlay = null;
        var closed = false;
        var originalEditorExclusive = dialog.Exclusive;

        var consentPath = Path.Combine(studio.Workspace.Root,
            RoadCodexConsentPath.Replace('/', Path.DirectorySeparatorChar));
        void LoadGlobalInstructionConsent()
        {
            consentWarning = null;
            globalInstructions.SetPressedNoSignal(false);
            try
            {
                StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, RoadCodexConsentPath, allowStudioMetadata: true);
                if (!File.Exists(consentPath)) return;
                if (new FileInfo(consentPath).Length > 4 * 1024)
                    throw new InvalidDataException("Сохранённый выбор Codex слишком велик.");
                using var document = JsonDocument.Parse(File.ReadAllText(consentPath));
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("allowGlobalInstructions", out var allowed)
                    && allowed.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    globalInstructions.SetPressedNoSignal(allowed.GetBoolean());
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                consentWarning = "Согласие на глобальные инструкции не прочитано; оно выключено.";
                globalInstructions.SetPressedNoSignal(false);
            }
        }

        void SaveGlobalInstructionConsent(bool allowed)
        {
            try
            {
                StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, RoadCodexConsentPath, allowStudioMetadata: true);
                AtomicFile.WriteAllText(consentPath,
                    JsonSerializer.Serialize(new { allowGlobalInstructions = allowed }));
                consentWarning = null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                globalInstructions.SetPressedNoSignal(!allowed);
                consentWarning = "Не удалось сохранить выбор инструкций Codex; переключатель возвращён.";
                if (!closed) status.Text = consentWarning;
            }
        }

        LoadGlobalInstructionConsent();
        if (consentWarning is not null) status.Text += "\n" + consentWarning;
        globalInstructions.Toggled += SaveGlobalInstructionConsent;

        bool HasSelectedRoad() => roadChoice.Selected >= 0 && roadChoice.Selected < roadChoice.ItemCount;
        bool HasValidRange() => HasSelectedRoad()
            && roadChoice.Selected < captured.Roads(SelectedSourcePath()).Count
            && startIndex.Value >= 0 && endIndex.Value >= startIndex.Value + 4
            && endIndex.Value < captured.Roads(SelectedSourcePath())[roadChoice.Selected].Points.Count;

        void RefreshRoadControls()
        {
            if (closed || !GodotObject.IsInstanceValid(dialog)) return;
            var busy = _roadAiBusy || studio.IsWorkspaceActionBusy || studio.IsAiActionBusy;
            sourceChoice.Disabled = busy;
            roadChoice.Disabled = busy;
            startIndex.Editable = !busy;
            endIndex.Editable = !busy;
            strength.Editable = !busy;
            deviation.Editable = !busy;
            prompt.Editable = !busy;
            globalInstructions.Disabled = busy;
            askCodexButton.Disabled = busy || !HasSelectedRoad() || !HasValidRange() || string.IsNullOrWhiteSpace(prompt.Text);
            stopCodexButton.Disabled = roadCancellation is null;
            previewButton.Disabled = busy || !HasSelectedRoad() || !HasValidRange();
            applyButton.Disabled = busy || preview is null;
        }

        void WriteRoadJobArtifact(RoadSegmentSmoothRequest request, string fileName, string contents, int maximumBytes)
        {
            var relative = $".urman-studio/ai-jobs/{request.RequestId}/{fileName}";
            StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, relative, allowStudioMetadata: true);
            var bytes = new UTF8Encoding(false, true).GetBytes(contents);
            if (bytes.Length > maximumBytes)
                throw new InvalidDataException("Артефакт запроса дорожки превышает допустимый размер.");
            var path = Path.Combine(studio.Workspace.Root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > maximumBytes)
                    throw new IOException("Существующий артефакт запроса слишком велик; он оставлен без изменений.");
                var existing = File.ReadAllBytes(path);
                if (existing.AsSpan().SequenceEqual(bytes)) return;
                throw new IOException("В папке этого запроса уже находится другой артефакт; он оставлен без изменений.");
            }
            AtomicFile.WriteAllBytes(path, bytes, overwrite: false);
        }

        void ClearAcceptedProposal()
        {
            acceptedRequest = null;
            acceptedProposal = null;
        }

        void ClearOverlay()
        {
            if (overlay is not null && GodotObject.IsInstanceValid(overlay)) overlay.QueueFree();
            overlay = null;
            RequestViewportRender();
        }

        void ClearPreview(string? message = null)
        {
            preview = null;
            ClearOverlay();
            if (message is not null) status.Text = message;
            RefreshRoadControls();
        }

        void CloseEditor()
        {
            if (closed) return;
            closed = true;
            roadCancellation?.Cancel();
            if (pendingDisclosure is { } disclosure) DismissDisclosure(disclosure);
            ClearOverlay();
            if (openedDemo is not null && GodotObject.IsInstanceValid(openedDemo))
                openedDemo.TreeExiting -= CloseEditor;
            if (openedView is not null && GodotObject.IsInstanceValid(openedView))
                openedView.TreeExiting -= CloseEditor;
            if (_roadEditorDialog == dialog) _roadEditorDialog = null;
            if (GodotObject.IsInstanceValid(dialog) && dialog.IsInsideTree()) dialog.QueueFree();
        }

        void DismissDisclosure(ConfirmationDialog disclosure)
        {
            if (pendingDisclosure != disclosure) return;
            if (GodotObject.IsInstanceValid(disclosure) && disclosure.IsInsideTree())
            {
                if (disclosure.Visible) disclosure.Hide();
                disclosure.QueueFree();
            }
            pendingDisclosure = null;
            Callable.From(() =>
            {
                if (!closed && pendingDisclosure is null && GodotObject.IsInstanceValid(dialog)
                    && dialog.IsInsideTree() && dialog.Visible)
                    dialog.Exclusive = originalEditorExclusive;
            }).CallDeferred();
        }

        dialog.VisibilityChanged += () =>
        {
            if (!closed && pendingDisclosure is null && dialog.Visible)
                dialog.Exclusive = originalEditorExclusive;
        };

        void EnsureEditorCurrent(CancellationTokenSource? ownRoadOperation = null)
        {
            if (closed || _shuttingDown || !WorldReady || !ReferenceEquals(_demo, openedDemo)
                || !ReferenceEquals(CurrentRoadEditorWorld(), openedWorld)
                || !ReferenceEquals(_view, openedView) || studio.Selection != openedSelection)
                throw new InvalidOperationException("Мир или выделение изменились. Откройте редактор дорожек заново.");
            var ownRoadBusy = ownRoadOperation is not null && _roadAiBusy
                && ReferenceEquals(roadCancellation, ownRoadOperation);
            if (studio.IsWorkspaceActionBusy || studio.IsAiActionBusy && !ownRoadBusy)
                throw new InvalidOperationException("Дождитесь завершения текущего действия Studio.");
        }

        int SelectedSourceIndex() => sourceChoice.Selected == 1 ? 1 : 0;
        string SelectedSourcePath() => SelectedSourceIndex() == 1 ? FarBankRoadFile : OpenPartRoadFile;
        string SelectedRoadId()
        {
            var index = roadChoice.Selected;
            return index >= 0 && index < roadChoice.ItemCount
                ? roadChoice.GetItemMetadata(index).AsString()
                : throw new InvalidOperationException("Выберите дорожку.");
        }

        void EnsureRoadInputsCurrentOnDisk(RoadEditorInputs expected)
        {
            foreach (var path in new[] { OpenPartRoadFile, FarBankRoadFile })
            {
                StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, path);
                var file = studio.Workspace.File(path);
                if (file.IsNew || file.Dirty || file.ChangedOnDisk())
                    throw new InvalidOperationException("Исходник дорожек изменился на диске или имеет черновик: " + path);
                if (!SameRoadSource(expected.Source(path), file.CaptureSource()))
                    throw new InvalidOperationException("Снимок исходника дорожек больше не совпадает: " + path);
            }
            EnsureInputsMatch(expected, CaptureRoadEditorInputs());
        }

        void RefreshRoadChoice()
        {
            ClearAcceptedProposal();
            ClearPreview();
            roadChoice.Clear();
            var path = SelectedSourcePath();
            var roads = captured.Roads(path);
            for (var index = 0; index < roads.Count; index++)
            {
                var road = roads[index];
                roadChoice.AddItem($"{road.Street} — {road.Id}");
                roadChoice.SetItemMetadata(index, road.Id);
            }
            if (roadChoice.ItemCount == 0)
            {
                countLabel.Text = "В этом исходнике нет дорожек для редактирования.";
                previewButton.Disabled = true;
                startIndex.MaxValue = 0;
                endIndex.MaxValue = 0;
                endIndex.MinValue = 0;
                RefreshRoadControls();
                return;
            }
            roadChoice.Select(0);
            RefreshPointRange();
        }

        void RefreshPointRange()
        {
            ClearAcceptedProposal();
            ClearPreview();
            if (roadChoice.Selected < 0 || roadChoice.Selected >= roadChoice.ItemCount) return;
            var road = captured.Roads(SelectedSourcePath())[roadChoice.Selected];
            var maxStart = Math.Max(0, road.Points.Count - 5);
            endIndex.MaxValue = road.Points.Count - 1;
            endIndex.MinValue = Math.Min(4, road.Points.Count - 1);
            startIndex.MaxValue = maxStart;
            startIndex.Value = Math.Clamp(startIndex.Value, 0, maxStart);
            endIndex.Value = Math.Clamp(endIndex.Value, endIndex.MinValue, endIndex.MaxValue);
            countLabel.Text = $"{road.Points.Count} контрольных точек · ширина {road.Width:0.##} м · диапазон точек включительный";
            RefreshRoadControls();
        }

        sourceChoice.ItemSelected += _ => RefreshRoadChoice();
        roadChoice.ItemSelected += _ => RefreshPointRange();
        startIndex.ValueChanged += value =>
        {
            ClearAcceptedProposal();
            endIndex.MinValue = Math.Min(endIndex.MaxValue, value + 4);
            ClearPreview("Диапазон изменился. Покажите предпросмотр заново.");
        };
        endIndex.ValueChanged += _ =>
        {
            ClearAcceptedProposal();
            ClearPreview("Диапазон изменился. Покажите предпросмотр заново.");
        };
        strength.ValueChanged += _ =>
        {
            ClearAcceptedProposal();
            ClearPreview("Сила сглаживания изменилась. Покажите предпросмотр заново.");
        };
        deviation.ValueChanged += _ =>
        {
            ClearAcceptedProposal();
            ClearPreview("Максимальный сдвиг изменился. Покажите предпросмотр заново.");
        };
        prompt.TextChanged += () =>
        {
            ClearAcceptedProposal();
            ClearPreview("Запрос изменился. Старое предложение отозвано; отправьте новый запрос или используйте ручной предпросмотр.");
            RefreshRoadControls();
        };

        void ShowPreview(RoadSegmentSmoothProposal? proposed = null)
        {
            EnsureEditorCurrent(roadCancellation);
            var current = CaptureRoadEditorInputs();
            EnsureRoadInputsCurrentOnDisk(captured);
            EnsureInputsMatch(captured, current);

            var path = proposed?.SourcePath ?? SelectedSourcePath();
            var roadId = proposed?.RoadId ?? SelectedRoadId();
            var start = proposed?.StartIndex ?? (int)Math.Round(startIndex.Value);
            var end = proposed?.EndIndex ?? (int)Math.Round(endIndex.Value);
            var strengthValue = proposed?.Strength ?? strength.Value;
            var deviationValue = proposed?.MaxDeviation ?? deviation.Value;
            if (proposed is not null)
            {
                if (acceptedRequest is null || acceptedProposal != proposed)
                    throw new InvalidOperationException("Предложение Codex больше не соответствует текущему снимку.");
                RoadSegmentSmoothProtocol.ValidateProposal(acceptedRequest, proposed);
                if (path != SelectedSourcePath() || roadId != SelectedRoadId()
                    || start != (int)Math.Round(startIndex.Value) || end != (int)Math.Round(endIndex.Value))
                    throw new InvalidOperationException("Источник или диапазон изменился после ответа Codex.");
            }

            var source = captured.Source(path);
            var beforeRoad = captured.Roads(path).Single(road => road.Id == roadId);
            var additionalRoads = captured.OtherRoads(path).Concat(captured.FixedRoads).ToArray();
            var candidate = RoadSegmentSmoothSource.Smooth(path, source.Text, roadId,
                start, end, strengthValue, deviationValue, additionalRoads);
            if (string.Equals(candidate, source.Text, StringComparison.Ordinal))
            {
                ClearPreview("В выбранном диапазоне с этими параметрами нет допустимого перемещения точек.");
                return;
            }
            var candidateRoad = RoadSegmentSmoothSource.Read(path, candidate).Single(road => road.Id == roadId);
            var bytes = new UTF8Encoding(false, true).GetBytes(candidate);
            var next = new RoadEditorPreview(path, roadId, start, end, strengthValue, deviationValue,
                proposed, captured, candidate, AtomicFile.Sha256(bytes), beforeRoad, candidateRoad);
            ClearOverlay();
            overlay = CreateRoadSmoothOverlay(next);
            _viewport.AddChild(overlay);
            preview = next;
            status.Text = proposed is null
                ? $"Предпросмотр готов: {beforeRoad.Street}, точки {start}–{end} включительно. Бирюзовая линия и крестики показывают кандидат; рельеф и столкновения ещё не менялись."
                : $"Codex: {proposed.Summary}\nПредпросмотр для {beforeRoad.Street}, точки {start}–{end}: сила {strengthValue:0.###}, макс. сдвиг {deviationValue:0.###} м. Бирюзовая линия и крестики показывают детерминированный кандидат; проект не менялся. Проверьте его и нажмите «Применить» отдельно.";
            RefreshRoadControls();
            RequestViewportRender();
        }

        void EnsureRoadRequestCurrent(
            RoadSegmentSmoothRequest request,
            RoadEditorInputs requestInputs,
            bool allowGlobalInstructions,
            CancellationTokenSource? ownRoadOperation)
        {
            EnsureEditorCurrent(ownRoadOperation);
            EnsureRoadInputsCurrentOnDisk(requestInputs);
            if (!string.Equals(prompt.Text, request.Prompt, StringComparison.Ordinal)
                || globalInstructions.ButtonPressed != allowGlobalInstructions
                || SelectedSourcePath() != request.SourcePath
                || SelectedRoadId() != request.RoadId
                || (int)Math.Round(startIndex.Value) != request.StartIndex
                || (int)Math.Round(endIndex.Value) != request.EndIndex)
                throw new InvalidOperationException("Запрос, согласие или выбранный диапазон изменились после подтверждения.");
            var sourceBytes = new UTF8Encoding(false, true).GetBytes(requestInputs.Source(request.SourcePath).Text);
            if (!string.Equals(AtomicFile.Sha256(sourceBytes), request.SourceSha256, StringComparison.Ordinal)
                || !string.Equals(requestInputs.Source(request.SourcePath).BaseSha256, request.SourceSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("SHA исходника больше не совпадает со снимком Codex.");
        }

        async Task RunCodexAsync(RoadSegmentSmoothRequest request, RoadEditorInputs requestInputs, bool allowGlobalInstructions)
        {
            if (closed || roadCancellation is not null || _roadAiBusy) return;
            var cancellation = new CancellationTokenSource();
            roadCancellation = cancellation;
            _roadAiBusy = true;
            RefreshRoadControls();
            studio.RefreshStatus();
            DirectoryInfo? workingDirectory = null;
            try
            {
                EnsureRoadRequestCurrent(request, requestInputs, allowGlobalInstructions, cancellation);
                WriteRoadJobArtifact(request, "request.json",
                    RoadSegmentSmoothProtocol.SerializeRequest(request), RoadSegmentSmoothProtocol.MaximumSerializedRequestBytes);

                workingDirectory = Directory.CreateTempSubdirectory("urman-road-" + request.RequestId + "-");
                if (workingDirectory.EnumerateFileSystemInfos().Any())
                    throw new InvalidOperationException("Временная рабочая папка Codex должна быть пустой.");

                status.Text = "Запрос отправлен. Codex возвращает только ограниченные параметры; остановка доступна. Исходник не меняется.";
                var provider = new CodexProposalProvider(allowGlobalInstructions: allowGlobalInstructions);
                var proposal = await provider.GenerateRoadSegmentSmoothAsync(
                    request, workingDirectory.FullName, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                EnsureRoadRequestCurrent(request, requestInputs, allowGlobalInstructions, cancellation);
                RoadSegmentSmoothProtocol.ValidateProposal(request, proposal);
                WriteRoadJobArtifact(request, "proposal.json",
                    RoadSegmentSmoothProtocol.SerializeProposal(request, proposal), RoadSegmentSmoothProtocol.MaximumProposalBytes);

                acceptedRequest = request;
                acceptedProposal = proposal;
                ShowPreview(proposal);
                if (consentWarning is not null) status.Text += "\n" + consentWarning;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                if (!closed) status.Text = "Запрос остановлен. Исходник не менялся; можно попробовать снова.";
            }
            catch (Exception error)
            {
                ClearAcceptedProposal();
                if (!closed) ClearPreview("Codex не показан: " + error.Message + " Исходник не изменён.");
            }
            finally
            {
                if (workingDirectory is not null)
                {
                    try { workingDirectory.Delete(recursive: true); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        if (!closed) status.Text += " Не удалось удалить временную папку Codex: " + error.Message;
                    }
                }
                if (ReferenceEquals(roadCancellation, cancellation))
                {
                    roadCancellation = null;
                    _roadAiBusy = false;
                }
                cancellation.Dispose();
                if (GodotObject.IsInstanceValid(studio)) studio.RefreshStatus();
                RefreshRoadControls();
            }
        }

        void AskCodex()
        {
            if (closed || _roadAiBusy || roadCancellation is not null || pendingDisclosure is not null
                || studio.IsWorkspaceActionBusy || studio.IsAiActionBusy) return;
            try
            {
                EnsureEditorCurrent();
                if (string.IsNullOrWhiteSpace(prompt.Text))
                    throw new InvalidOperationException("Опишите, как изменить выбранный участок дороги.");
                EnsureRoadInputsCurrentOnDisk(captured);
                var requestInputs = CaptureRoadEditorInputs();
                EnsureInputsMatch(captured, requestInputs);
                var path = SelectedSourcePath();
                var roadId = SelectedRoadId();
                var start = (int)Math.Round(startIndex.Value);
                var end = (int)Math.Round(endIndex.Value);
                var contextRoads = captured.OtherRoads(path).Concat(captured.FixedRoads).ToArray();
                var request = RoadSegmentSmoothProtocol.CreateRequest(prompt.Text, path,
                    captured.Source(path).Text, roadId, start, end, contextRoads, Guid.NewGuid().ToString("N"));
                var allowGlobalInstructions = globalInstructions.ButtonPressed;
                var serializedRequest = RoadSegmentSmoothProtocol.SerializeRequest(request);

                var disclosure = new ConfirmationDialog
                {
                    Title = "Проверьте передачу запроса Codex",
                    OkButtonText = "Отправить этот запрос",
                    MinSize = new Vector2I(820, 620),
                    DialogText = ""
                };
                disclosure.GetLabel().Hide();
                disclosure.AddCancelButton("Назад");
                var disclosureBody = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                disclosure.AddChild(disclosureBody);
                disclosureBody.AddChild(new Label
                {
                    Text = "Передача включает всю выбранную дорогу и её точки, выбранный диапазон, остальные дорожные оси двух исходников для сохранения стыков и эту инструкцию.",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(740, 48)
                });
                disclosureBody.AddChild(new Label
                {
                    Text = $"Источник: {request.SourcePath}\nДорожка: {request.RoadId}; точки {request.StartIndex}–{request.EndIndex} включительно\nСнимок SHA-256: {request.SourceSha256}\nКонтекст: {request.ContextRoads.Count} дорожных осей (обе части деревни и фиксированные оси генератора)\nГлобальные инструкции: {(allowGlobalInstructions ? "разрешены — только ~/.codex/AGENTS.md" : "выключены")}\nCodex не получает инструменты, MCP или право записи. Будут предложены только сила и максимальный сдвиг точки.",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(740, 135)
                });
                disclosureBody.AddChild(new TextEdit
                {
                    Text = serializedRequest,
                    Editable = false,
                    WrapMode = TextEdit.LineWrappingMode.Boundary,
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(740, 300)
                });
                var resolved = false;
                disclosure.Confirmed += () =>
                {
                    if (resolved) return;
                    resolved = true;
                    DismissDisclosure(disclosure);
                    if (closed) return;
                    try
                    {
                        EnsureRoadRequestCurrent(request, requestInputs, allowGlobalInstructions, null);
                        if (globalInstructions.ButtonPressed != allowGlobalInstructions)
                            throw new InvalidOperationException("Согласие на глобальные инструкции изменилось после просмотра снимка.");
                        ClearAcceptedProposal();
                        ClearPreview();
                        _ = RunCodexAsync(request, requestInputs, allowGlobalInstructions);
                    }
                    catch (Exception error)
                    {
                        if (!closed) status.Text = "Запрос не отправлен: " + error.Message;
                    }
                };
                disclosure.Canceled += () =>
                {
                    resolved = true;
                    if (!closed) status.Text = "Запрос не отправлен. Проект не менялся.";
                    DismissDisclosure(disclosure);
                };
                disclosure.CloseRequested += () =>
                {
                    resolved = true;
                    DismissDisclosure(disclosure);
                };
                pendingDisclosure = disclosure;
                try
                {
                    dialog.Exclusive = false;
                    disclosure.Exclusive = true;
                    studio.AddChild(disclosure);
                    disclosure.PopupCenteredClamped(new Vector2I(820, 620), 0.9f);
                }
                catch
                {
                    DismissDisclosure(disclosure);
                    throw;
                }
            }
            catch (Exception error)
            {
                if (!closed) status.Text = "Запрос не подготовлен: " + error.Message;
            }
        }

        askCodexButton.Pressed += AskCodex;
        stopCodexButton.Pressed += () => roadCancellation?.Cancel();
        previewButton.Pressed += () =>
        {
            ClearAcceptedProposal();
            try { ShowPreview(); }
            catch (Exception error) { ClearPreview("Предпросмотр недоступен: " + error.Message); }
        };

        applyButton.Pressed += async () =>
        {
            if (preview is not { } selectedPreview) return;
            try
            {
                EnsureEditorCurrent();
                var current = CaptureRoadEditorInputs();
                EnsureInputsMatch(selectedPreview.Inputs, current);
                if (SelectedSourcePath() != selectedPreview.SourcePath
                    || SelectedRoadId() != selectedPreview.RoadId
                    || (int)Math.Round(startIndex.Value) != selectedPreview.StartIndex
                    || (int)Math.Round(endIndex.Value) != selectedPreview.EndIndex)
                    throw new InvalidOperationException("Выбранные источник, диапазон или параметры отличаются от предпросмотра.");
                if (selectedPreview.Proposal is { } aiProposal)
                {
                    if (acceptedRequest is null || acceptedProposal != aiProposal
                        || aiProposal.Strength != selectedPreview.Strength
                        || aiProposal.MaxDeviation != selectedPreview.MaxDeviation)
                        throw new InvalidOperationException("Предложение Codex больше не связано с предпросмотром.");
                    RoadSegmentSmoothProtocol.ValidateProposal(acceptedRequest, aiProposal);
                }
                else if (strength.Value != selectedPreview.Strength || deviation.Value != selectedPreview.MaxDeviation)
                {
                    throw new InvalidOperationException("Ручные параметры отличаются от предпросмотра.");
                }
                var source = selectedPreview.Inputs.Source(selectedPreview.SourcePath);
                var file = studio.Workspace.File(selectedPreview.SourcePath);
                EnsureCleanRoadSource(source);
                if (file.Dirty || file.IsNew || file.Text != source.Text)
                    throw new InvalidOperationException("В Studio появился черновик источника дорожек.");
                StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, selectedPreview.SourcePath);

                var candidateBytes = new UTF8Encoding(false, true).GetBytes(selectedPreview.CandidateText);
                if (AtomicFile.Sha256(candidateBytes) != selectedPreview.CandidateHash)
                    throw new InvalidOperationException("Предпросмотр изменился после подготовки.");
                var write = new StudioFileWrite(selectedPreview.SourcePath, source.BaseSha256!, candidateBytes);
                var allowedPaths = new HashSet<string>(StringComparer.Ordinal) { selectedPreview.SourcePath };
                var owner = RoadSmoothOwnerPrefix + Guid.NewGuid().ToString("N");
                applyButton.Disabled = true;
                previewButton.Disabled = true;
                status.Text = "Сохраняю один файл дорожек и перестраиваю сцену…";

                void ReconcilePromotedFile()
                {
                    StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, selectedPreview.SourcePath);
                    var fullPath = Path.Combine(studio.Workspace.Root,
                        selectedPreview.SourcePath.Replace('/', Path.DirectorySeparatorChar));
                    var diskHash = AtomicFile.Sha256OfFile(fullPath);
                    var candidateSide = string.Equals(diskHash, selectedPreview.CandidateHash, StringComparison.Ordinal);
                    if (!candidateSide && !string.Equals(diskHash, source.BaseSha256, StringComparison.Ordinal))
                        throw new InvalidOperationException("Источник дорожек изменился вне этой транзакции.");

                    if (candidateSide)
                    {
                        var otherPath = selectedPreview.SourcePath == OpenPartRoadFile ? FarBankRoadFile : OpenPartRoadFile;
                        var other = selectedPreview.Inputs.Source(otherPath);
                        var otherFile = studio.Workspace.File(otherPath);
                        StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, otherPath);
                        if (other.Dirty || other.IsNew || otherFile.Dirty || otherFile.IsNew || otherFile.ChangedOnDisk())
                            throw new InvalidOperationException("Второй источник дорожек изменился во время применения.");
                        var currentOther = otherFile.CaptureSource();
                        if (!SameRoadSource(other, currentOther))
                            throw new InvalidOperationException("Контекст другого участка изменился во время применения.");
                    }

                    if (file.Dirty || file.IsNew)
                        throw new InvalidOperationException("Черновик источника дорожек появился во время применения.");
                    if (file.ChangedOnDisk())
                    {
                        var pulled = file.PullFromDisk();
                        if (!pulled.Reloaded || pulled.Conflicts.Count != 0)
                            throw new InvalidOperationException("Не удалось синхронизировать сохранённый источник дорожек.");
                        studio.Workspace.Reindex();
                    }
                    var expected = candidateSide ? selectedPreview.CandidateText : source.Text;
                    if (file.Dirty || !string.Equals(file.Text, expected, StringComparison.Ordinal))
                        throw new InvalidOperationException("Черновик Studio не совпадает с сохранённой стороной транзакции.");
                }

                await studio.Session.ApplyFilesAsync(
                    "Сгладить дорожку: " + selectedPreview.BeforeRoad.Street,
                    owner,
                    [write],
                    allowedPaths,
                    ReconcilePromotedFile,
                    () => ReloadGeometryAsync());
                studio.ShowBanner("Участок дорожки применён одним шагом. Его можно отменить обычной командой Undo.", error: false);
                CloseEditor();
            }
            catch (Exception error)
            {
                if (!closed && GodotObject.IsInstanceValid(dialog))
                {
                    applyButton.Disabled = preview is null;
                    previewButton.Disabled = false;
                    status.Text = "Не применено: " + error.Message + " Предпросмотр снят; проверьте исходники и подготовьте его заново.";
                    ClearPreview(status.Text);
                }
                studio.ShowBanner("Дорожка не применена: " + error.Message, error: true);
            }
        };

        void CheckEditorContext()
        {
            if (closed) return;
            if (_shuttingDown || !ReferenceEquals(_demo, openedDemo) || !ReferenceEquals(_view, openedView)
                || !ReferenceEquals(CurrentRoadEditorWorld(), openedWorld) || studio.Selection != openedSelection)
            {
                CloseEditor();
                return;
            }
            if (studio.IsWorkspaceActionBusy || studio.IsAiActionBusy) return;
            try
            {
                EnsureInputsMatch(captured, CaptureRoadEditorInputs());
            }
            catch (Exception error)
            {
                ClearPreview("Исходники дорожек изменились. Редактор закрыт, чтобы не использовать устаревшее состояние: " + error.Message);
                CloseEditor();
            }
        }

        var timer = new global::Godot.Timer { WaitTime = .8, OneShot = false, Autostart = true };
        timer.Timeout += CheckEditorContext;
        dialog.AddChild(timer);
        dialog.Confirmed += CloseEditor;
        dialog.Canceled += CloseEditor;
        dialog.CloseRequested += CloseEditor;
        dialog.TreeExiting += () =>
        {
            if (!closed) CloseEditor();
        };
        openedDemo!.TreeExiting += CloseEditor;
        if (openedView is not null) openedView.TreeExiting += CloseEditor;
        _roadEditorDialog = dialog;
        studio.AddChild(dialog);
        RefreshRoadChoice();
        dialog.PopupCentered(new Vector2I(820, 640));
    }

    private RoadEditorInputs CaptureRoadEditorInputs()
    {
        if (!studio.Workspace.HasFile(OpenPartRoadFile) || !studio.Workspace.HasFile(FarBankRoadFile))
            throw new InvalidOperationException("Оба исходника дорожек должны быть подключены к открытому миру Studio.");
        var openPart = studio.Workspace.File(OpenPartRoadFile).CaptureSource();
        var farBank = studio.Workspace.File(FarBankRoadFile).CaptureSource();
        EnsureCleanRoadSource(openPart);
        EnsureCleanRoadSource(farBank);
        var openPartRoads = RoadSegmentSmoothSource.Read(OpenPartRoadFile, openPart.Text);
        var farBankRoads = RoadSegmentSmoothSource.Read(FarBankRoadFile, farBank.Text);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var road in openPartRoads.Concat(farBankRoads))
            if (!ids.Add(road.Id)) throw new InvalidDataException("ID дорожки повторяется между исходниками: " + road.Id);
        return new(openPart, farBank, openPartRoads, farBankRoads, CaptureFixedRoads());
    }

    private static IReadOnlyList<RoadSourceRecord> CaptureFixedRoads()
    {
        var axes = AgentBAct1HeightField.FixedRoadAxesForStudio;
        var result = new RoadSourceRecord[axes.Length];
        for (var index = 0; index < axes.Length; index++)
        {
            var axis = axes[index];
            result[index] = new RoadSourceRecord(
                "__studio_fixed_axis/" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
                "Ось улицы из генератора",
                axis.HalfWidth * 2,
                Array.AsReadOnly(axis.Points.Select(point => new RoadControlPoint(point.X, point.Z)).ToArray()));
        }
        return Array.AsReadOnly(result);
    }

    private static void EnsureCleanRoadSource(AuthoredSourceSnapshot source)
    {
        if (source.IsNew || source.Dirty || source.BaseText is null || source.BaseSha256 is null
            || !string.Equals(source.BaseSha256, source.Sha256, StringComparison.Ordinal)
            || !string.Equals(source.BaseText, source.Text, StringComparison.Ordinal))
            throw new InvalidOperationException("Сначала сохраните или отмените черновик: " + source.RelativePath);
    }

    private static bool SameRoadSource(AuthoredSourceSnapshot left, AuthoredSourceSnapshot right) =>
        left.RelativePath == right.RelativePath && left.BaseSha256 == right.BaseSha256
        && left.Sha256 == right.Sha256 && left.Text == right.Text && left.IsNew == right.IsNew && left.Dirty == right.Dirty;

    private static void EnsureInputsMatch(RoadEditorInputs expected, RoadEditorInputs current)
    {
        if (!SameRoadSource(expected.OpenPart, current.OpenPart) || !SameRoadSource(expected.FarBank, current.FarBank)
            || !RoadRecordsEqual(expected.FixedRoads, current.FixedRoads))
            throw new InvalidOperationException("Исходники или фиксированные оси дорожек изменились после снимка.");
    }

    private static bool RoadRecordsEqual(IReadOnlyList<RoadSourceRecord> left, IReadOnlyList<RoadSourceRecord> right)
    {
        if (left.Count != right.Count) return false;
        for (var roadIndex = 0; roadIndex < left.Count; roadIndex++)
        {
            var first = left[roadIndex];
            var second = right[roadIndex];
            if (first.Id != second.Id || first.Street != second.Street || first.Width != second.Width
                || first.Points.Count != second.Points.Count) return false;
            for (var pointIndex = 0; pointIndex < first.Points.Count; pointIndex++)
                if (first.Points[pointIndex] != second.Points[pointIndex]) return false;
        }
        return true;
    }

    private Act1ConnectedWorld? CurrentRoadEditorWorld()
    {
        if (_demo is null || !GodotObject.IsInstanceValid(_demo) || !_demo.IsInsideTree()) return null;
        return _demo.GetTree().GetNodesInGroup("act1_connected_world")
            .OfType<Act1ConnectedWorld>().FirstOrDefault(_demo.IsAncestorOf);
    }

    private Node3D CreateRoadSmoothOverlay(RoadEditorPreview preview)
    {
        var root = new Node3D { Name = "StudioRoadSmoothGhost" };
        var beforePoints = preview.BeforeRoad.Points.Skip(preview.StartIndex)
            .Take(preview.EndIndex - preview.StartIndex + 1).ToArray();
        var candidatePoints = preview.CandidateRoad.Points.Skip(preview.StartIndex)
            .Take(preview.EndIndex - preview.StartIndex + 1).ToArray();
        root.AddChild(CreateRoadPolyline("Исходный диапазон", beforePoints, new Color(.95f, .58f, .14f, .94f), false));
        root.AddChild(CreateRoadPolyline("Кандидат сглаживания", candidatePoints, new Color(.1f, .93f, .83f, 1f), false));
        root.AddChild(CreateRoadPolyline("Контрольные точки кандидата", candidatePoints, new Color(.12f, .94f, .78f, 1f), true));
        return root;
    }

    private static MeshInstance3D CreateRoadPolyline(
        string name, IReadOnlyList<RoadControlPoint> points, Color color, bool pointTicks)
    {
        var mesh = new ImmediateMesh();
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = color,
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        mesh.SurfaceBegin(pointTicks ? Mesh.PrimitiveType.Lines : Mesh.PrimitiveType.LineStrip, material);
        if (pointTicks)
        {
            foreach (var point in points)
            {
                var ground = AgentBAct1HeightField.CollisionGround((float)point.X, (float)point.Z) + .16f;
                var centre = new Vector3((float)point.X, ground, (float)point.Z);
                mesh.SurfaceAddVertex(centre + new Vector3(-.18f, 0, 0));
                mesh.SurfaceAddVertex(centre + new Vector3(.18f, 0, 0));
                mesh.SurfaceAddVertex(centre + new Vector3(0, 0, -.18f));
                mesh.SurfaceAddVertex(centre + new Vector3(0, 0, .18f));
                mesh.SurfaceAddVertex(centre);
                mesh.SurfaceAddVertex(centre + new Vector3(0, .38f, 0));
            }
        }
        else
        {
            foreach (var point in points)
                mesh.SurfaceAddVertex(new Vector3((float)point.X,
                    AgentBAct1HeightField.CollisionGround((float)point.X, (float)point.Z) + .18f,
                    (float)point.Z));
        }
        mesh.SurfaceEnd();
        return new MeshInstance3D { Name = name, Mesh = mesh };
    }

    private static Control Row(string label, params Control[] controls)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(170, 0), VerticalAlignment = VerticalAlignment.Center });
        foreach (var control in controls)
        {
            control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(control);
        }
        return row;
    }
}
