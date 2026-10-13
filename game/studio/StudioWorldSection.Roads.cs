using System.Text;
using Godot;
using Urman.Experiments.AgentBAct1;
using Urman.Godot;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

public sealed partial class StudioWorldSection
{
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
        RoadEditorInputs Inputs,
        string CandidateText,
        string CandidateHash,
        RoadSourceRecord BeforeRoad,
        RoadSourceRecord CandidateRoad);

    private AcceptDialog? _roadEditorDialog;

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
            MinSize = new Vector2I(820, 640)
        };
        var layout = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        dialog.AddChild(layout);
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
        layout.AddChild(Row("Сглаживание:", strength, new Label { Text = "макс. сдвиг точки", ThemeTypeVariation = "MutedLabel" }, deviation));

        var previewButton = new Button { Text = "Показать предпросмотр", ThemeTypeVariation = "PrimaryButton" };
        var applyButton = new Button { Text = "Применить — один шаг Undo", Disabled = true };
        var actionRow = new HBoxContainer();
        actionRow.AddChild(previewButton);
        actionRow.AddChild(applyButton);
        layout.AddChild(actionRow);
        var status = new Label
        {
            Text = "Применение изменит только выбранный файл дорожек. Отмена — обычная команда Undo Studio.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(760, 56)
        };
        layout.AddChild(status);

        RoadEditorPreview? preview = null;
        Node3D? overlay = null;
        var closed = false;
        void ClearOverlay()
        {
            if (overlay is not null && GodotObject.IsInstanceValid(overlay)) overlay.QueueFree();
            overlay = null;
            RequestViewportRender();
        }

        void ClearPreview(string? message = null)
        {
            preview = null;
            applyButton.Disabled = true;
            ClearOverlay();
            if (message is not null) status.Text = message;
        }

        void CloseEditor()
        {
            if (closed) return;
            closed = true;
            ClearOverlay();
            if (openedDemo is not null && GodotObject.IsInstanceValid(openedDemo))
                openedDemo.TreeExiting -= CloseEditor;
            if (openedView is not null && GodotObject.IsInstanceValid(openedView))
                openedView.TreeExiting -= CloseEditor;
            if (_roadEditorDialog == dialog) _roadEditorDialog = null;
            if (GodotObject.IsInstanceValid(dialog) && dialog.IsInsideTree()) dialog.QueueFree();
        }

        void EnsureEditorCurrent()
        {
            if (closed || _shuttingDown || !WorldReady || !ReferenceEquals(_demo, openedDemo)
                || !ReferenceEquals(CurrentRoadEditorWorld(), openedWorld)
                || !ReferenceEquals(_view, openedView) || studio.Selection != openedSelection)
                throw new InvalidOperationException("Мир или выделение изменились. Откройте редактор дорожек заново.");
            if (studio.IsWorkspaceActionBusy || studio.IsAiActionBusy)
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

        void RefreshRoadChoice()
        {
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
                return;
            }
            roadChoice.Select(0);
            RefreshPointRange();
        }

        void RefreshPointRange()
        {
            ClearPreview();
            if (roadChoice.Selected < 0 || roadChoice.Selected >= roadChoice.ItemCount) return;
            var road = captured.Roads(SelectedSourcePath())[roadChoice.Selected];
            var maxStart = Math.Max(0, road.Points.Count - 5);
            endIndex.MaxValue = road.Points.Count - 1;
            endIndex.MinValue = Math.Min(4, road.Points.Count - 1);
            startIndex.MaxValue = maxStart;
            startIndex.Value = Math.Clamp(startIndex.Value, 0, maxStart);
            endIndex.Value = Math.Clamp(endIndex.Value, endIndex.MinValue, endIndex.MaxValue);
            previewButton.Disabled = road.Points.Count < 5;
            countLabel.Text = $"{road.Points.Count} контрольных точек · ширина {road.Width:0.##} м · диапазон точек включительный";
        }

        sourceChoice.ItemSelected += _ => RefreshRoadChoice();
        roadChoice.ItemSelected += _ => RefreshPointRange();
        startIndex.ValueChanged += value =>
        {
            endIndex.MinValue = Math.Min(endIndex.MaxValue, value + 4);
            ClearPreview("Диапазон изменился. Покажите предпросмотр заново.");
        };
        endIndex.ValueChanged += _ => ClearPreview("Диапазон изменился. Покажите предпросмотр заново.");
        strength.ValueChanged += _ => ClearPreview("Сила сглаживания изменилась. Покажите предпросмотр заново.");
        deviation.ValueChanged += _ => ClearPreview("Максимальный сдвиг изменился. Покажите предпросмотр заново.");

        previewButton.Pressed += () =>
        {
            try
            {
                EnsureEditorCurrent();
                var current = CaptureRoadEditorInputs();
                EnsureInputsMatch(captured, current);
                var path = SelectedSourcePath();
                var roadId = SelectedRoadId();
                var beforeRoad = captured.Roads(path).Single(road => road.Id == roadId);
                var start = (int)Math.Round(startIndex.Value);
                var end = (int)Math.Round(endIndex.Value);
                var additionalRoads = captured.OtherRoads(path).Concat(captured.FixedRoads).ToArray();
                var candidate = RoadSegmentSmoothSource.Smooth(path, captured.Source(path).Text, roadId,
                    start, end, strength.Value, deviation.Value, additionalRoads);
                if (string.Equals(candidate, captured.Source(path).Text, StringComparison.Ordinal))
                {
                    ClearPreview("В выбранном диапазоне с этими параметрами нет допустимого перемещения точек.");
                    return;
                }
                var candidateRoad = RoadSegmentSmoothSource.Read(path, candidate).Single(road => road.Id == roadId);
                var bytes = new UTF8Encoding(false, true).GetBytes(candidate);
                var next = new RoadEditorPreview(path, roadId, start, end, strength.Value, deviation.Value,
                    captured, candidate, AtomicFile.Sha256(bytes), beforeRoad, candidateRoad);
                ClearOverlay();
                overlay = CreateRoadSmoothOverlay(next);
                _viewport.AddChild(overlay);
                preview = next;
                applyButton.Disabled = false;
                status.Text = $"Предпросмотр готов: {beforeRoad.Street}, точки {start}–{end} включительно. Бирюзовая линия и крестики показывают кандидат; рельеф и столкновения ещё не менялись.";
                RequestViewportRender();
            }
            catch (Exception error)
            {
                ClearPreview("Предпросмотр недоступен: " + error.Message);
            }
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
                    || (int)Math.Round(endIndex.Value) != selectedPreview.EndIndex
                    || strength.Value != selectedPreview.Strength || deviation.Value != selectedPreview.MaxDeviation)
                    throw new InvalidOperationException("Выбранные источник, диапазон или параметры отличаются от предпросмотра.");
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
