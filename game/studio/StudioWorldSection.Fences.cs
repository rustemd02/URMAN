using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Experiments.AgentBAct1;
using Urman.Godot;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Play;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

public sealed partial class StudioWorldSection
{
    private const string OpenPartFenceSource = "res://content/world/act1_open_part.world.v1.json";
    private const string FarBankFenceSource = "res://content/world/act1_far_bank.world.v1.json";
    private const string OpenPartFenceFile = "game/content/world/act1_open_part.world.v1.json";
    private const string FarBankFenceFile = "game/content/world/act1_far_bank.world.v1.json";
    private static readonly string[] FenceBuildDependencyPaths =
    [
        "game/scripts/Act1ConnectedWorld.YardFences.cs",
        "game/scripts/Act1ConnectedWorld.Addresses.cs",
        "game/scripts/TimberHomeStyle.cs",
        "game/scripts/experiments/agent_b_act1/AgentBAct1Layout.cs",
        "game/scripts/experiments/agent_b_act1/AgentBAct1HeightField.cs",
        "game/content/urman.settlement.addresses.v1.json"
    ];

    private sealed record FencePick(
        CollisionShape3D Shape, string SourcePath, string RelativePath, string LotId, string EdgeRole,
        string RunId, int BayIndex, int BayCount, Vector2 RunStart, Vector2 RunEnd);

    private readonly record struct FencePreviewSources(string OpenPart, string FarBank);

    private FencePick? _fencePick;
    private MeshInstance3D? _fenceRangeMarker;
    private Act1ConnectedWorld? _fencePreviewWorld;
    private FencePreviewSources? _fencePreviewedSources;
    private Act1ConnectedWorld? _fencePreviewFailedWorld;
    private FencePreviewSources? _fencePreviewFailedSources;

    private void ClearFencePick()
    {
        _fencePick = null;
        if (_fenceRangeMarker is not null && GodotObject.IsInstanceValid(_fenceRangeMarker))
            _fenceRangeMarker.Visible = false;
    }

    private bool TryPickFence(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen).Normalized();
        var ray = PhysicsRayQueryParameters3D.Create(origin, origin + direction * 600f);
        var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (!hit.TryGetValue("collider", out var colliderValue)
            || colliderValue.AsGodotObject() is not CollisionObject3D collider
            || !hit.TryGetValue("shape", out var shapeValue))
            return false;

        var ownerId = collider.ShapeFindOwner(shapeValue.AsInt32());
        if (ownerId < 0 || collider.ShapeOwnerGetOwner(ownerId) is not CollisionShape3D shape
            || !shape.HasMeta("runId"))
            return false;

        if (_reloading || studio.IsAiActionBusy)
        {
            studio.ShowBanner("Дождитесь завершения текущего действия Studio, прежде чем выбирать забор.", error: false);
            return true;
        }

        _group.Clear();
        _pickedPart = null;
        ClearFencePick();
        var world = CurrentFenceWorld();
        if (world is null)
        {
            studio.Select(null);
            studio.ShowBanner("Предпросмотр заборов ещё не готов.", error: true);
            return true;
        }
        if (!TryGetPreviewSources(out var currentSources, out _, out var sourceError))
        {
            studio.Select(null);
            studio.ShowBanner("Забор не выбран: " + sourceError, error: true);
            return true;
        }
        if (!IsCurrentPreview(world, currentSources))
        {
            studio.Select(null);
            studio.ShowBanner("Забор не выбран: текущие исходники не совпадают с последним успешным предпросмотром.", error: true);
            return true;
        }

        if (!TryReadFencePick(world, collider, shape, out var picked, out var error))
        {
            studio.Select(null);
            studio.ShowBanner("Забор не выбран: " + error, error: true);
            return true;
        }

        _fencePick = picked;
        UpdateFenceRangeMarker(picked);
        studio.Select(null);
        return true;
    }

    private void SyncFencePreview()
    {
        var world = CurrentFenceWorld();
        if (world is null)
        {
            _fencePreviewWorld = null;
            _fencePreviewedSources = null;
            _fencePreviewFailedWorld = null;
            _fencePreviewFailedSources = null;
            ClearFenceSelectionAndRefresh();
            return;
        }

        if (!TryGetPreviewSources(out var sources, out var runtimeSources, out var sourceError))
        {
            var shouldReport = !ReferenceEquals(world, _fencePreviewFailedWorld)
                || _fencePreviewFailedSources != sources;
            _fencePreviewWorld = null;
            _fencePreviewedSources = null;
            _fencePreviewFailedWorld = world;
            _fencePreviewFailedSources = sources;
            ClearFenceSelectionAndRefresh();
            if (shouldReport) studio.ShowBanner("Заборы не обновлены: " + sourceError, error: true);
            return;
        }

        if (ReferenceEquals(world, _fencePreviewWorld) && _fencePreviewedSources == sources)
        {
            RefreshFenceSelection(world);
            return;
        }

        if (ReferenceEquals(world, _fencePreviewFailedWorld) && _fencePreviewFailedSources == sources)
        {
            ClearFenceSelectionAndRefresh();
            return;
        }

        try
        {
            world.RefreshYardFencesForStudio(runtimeSources);
            _fencePreviewWorld = world;
            _fencePreviewedSources = sources;
            _fencePreviewFailedWorld = null;
            _fencePreviewFailedSources = null;
            RefreshFenceSelection(world);
        }
        catch (Exception error)
        {
            // A failed rebuild can leave the old collision shapes visible; they are not editable against new source text.
            _fencePreviewWorld = null;
            _fencePreviewedSources = null;
            _fencePreviewFailedWorld = world;
            _fencePreviewFailedSources = sources;
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Заборы не обновлены: " + error.Message, error: true);
        }
    }

    private void RefreshFenceSelection(Act1ConnectedWorld world)
    {
        if (_fencePick is not { } previous) return;
        if (TryFindLiveFencePick(world, previous, out var current, out _))
        {
            _fencePick = current;
            UpdateFenceRangeMarker(current);
            return;
        }

        ClearFenceSelectionAndRefresh();
        studio.ShowBanner("Выбранный пролёт изменился в предпросмотре. Выберите его заново.", error: false);
    }

    private void ClearFenceSelectionAndRefresh()
    {
        var hadPick = _fencePick is not null;
        ClearFencePick();
        if (hadPick) studio.Select(null);
    }

    private Act1ConnectedWorld? CurrentFenceWorld()
    {
        if (_demo is null || !GodotObject.IsInstanceValid(_demo) || !_demo.IsInsideTree()) return null;
        return _demo.GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld;
    }

    private bool TryGetPreviewSources(
        out FencePreviewSources sources, out Dictionary<string, string> runtimeSources, out string error)
    {
        runtimeSources = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!studio.Workspace.HasFile(OpenPartFenceFile) || !studio.Workspace.HasFile(FarBankFenceFile))
        {
            sources = default;
            error = "исходник участка недоступен в Studio.";
            return false;
        }

        sources = new FencePreviewSources(studio.Workspace.File(OpenPartFenceFile).Text,
            studio.Workspace.File(FarBankFenceFile).Text);
        runtimeSources.Add(OpenPartFenceSource, sources.OpenPart);
        runtimeSources.Add(FarBankFenceSource, sources.FarBank);
        error = "";
        return true;
    }

    private bool IsCurrentPreview(Act1ConnectedWorld world, FencePreviewSources sources)
    {
        if (!ReferenceEquals(world, _fencePreviewWorld) || _fencePreviewedSources != sources) return false;
        try
        {
            return !studio.Workspace.File(OpenPartFenceFile).ChangedOnDisk()
                && !studio.Workspace.File(FarBankFenceFile).ChangedOnDisk();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private bool TryReadFencePick(
        Act1ConnectedWorld world, CollisionObject3D collider, CollisionShape3D shape,
        out FencePick pick, out string error)
    {
        pick = null!;
        if (!GodotObject.IsInstanceValid(shape) || !shape.IsInsideTree() || shape.GetParent() != collider
            || !IsUnderNode(shape, world))
        {
            error = "контакт больше не принадлежит текущей сцене.";
            return false;
        }

        if (!ReadStringMeta(shape, "sourcePath", out var sourcePath)
            || !ReadStringMeta(shape, "lotId", out var lotId)
            || !ReadStringMeta(shape, "edgeRole", out var edgeRole)
            || !ReadStringMeta(shape, "runId", out var runId)
            || !ReadStringMeta(shape, "bayId", out var bayId)
            || !shape.HasMeta("bayIndex") || !shape.HasMeta("bayCount")
            || !shape.HasMeta("runStartXZ") || !shape.HasMeta("runEndXZ"))
        {
            error = "в метаданных контакта не хватает источника, пролёта или границ.";
            return false;
        }

        var relativePath = FenceWorkspacePath(sourcePath);
        if (relativePath is null || !studio.Workspace.HasFile(relativePath))
        {
            error = "источник пролёта не входит в разрешённые файлы участков.";
            return false;
        }

        var bayIndex = shape.GetMeta("bayIndex").AsInt32();
        var bayCount = shape.GetMeta("bayCount").AsInt32();
        var runStart = shape.GetMeta("runStartXZ").AsVector2();
        var runEnd = shape.GetMeta("runEndXZ").AsVector2();
        if (edgeRole is not ("front" or "back" or "side-minus" or "side-plus")
            || bayCount < 1 || bayCount > 4096 || bayIndex < 0 || bayIndex >= bayCount
            || runStart.DistanceSquaredTo(runEnd) < .0001f
            || bayId != $"{runId}/bay-{bayIndex:D3}"
            || runId != FenceRunId(sourcePath, lotId, edgeRole, runStart, runEnd))
        {
            error = "локатор пролёта не совпадает с геометрией контакта.";
            return false;
        }

        var text = studio.Workspace.File(relativePath).Text;
        if (!TryFindUniqueLot(text, lotId, out _, out _, out error)) return false;
        pick = new FencePick(shape, sourcePath, relativePath, lotId, edgeRole, runId,
            bayIndex, bayCount, runStart, runEnd);
        return true;
    }

    private bool TryFindLiveFencePick(Act1ConnectedWorld world, FencePick wanted, out FencePick current, out string error)
    {
        if (GodotObject.IsInstanceValid(wanted.Shape)
            && wanted.Shape.GetParent() is CollisionObject3D wantedCollider
            && TryReadFencePick(world, wantedCollider, wanted.Shape, out current, out error)
            && SameFenceRun(wanted, current) && wanted.BayIndex == current.BayIndex)
            return true;

        foreach (var shape in world.FindChildren("*", nameof(CollisionShape3D), true, false).OfType<CollisionShape3D>())
        {
            if (!shape.HasMeta("runId") || shape.GetMeta("runId").AsString() != wanted.RunId
                || !shape.HasMeta("bayIndex") || shape.GetMeta("bayIndex").AsInt32() != wanted.BayIndex
                || shape.GetParent() is not CollisionObject3D collider)
                continue;
            if (TryReadFencePick(world, collider, shape, out current, out error) && SameFenceRun(wanted, current))
                return true;
        }

        current = null!;
        error = "текущая сцена больше не содержит этот участок, пролёт и секцию.";
        return false;
    }

    private static bool SameFenceRun(FencePick first, FencePick second) =>
        first.SourcePath == second.SourcePath && first.RelativePath == second.RelativePath
        && first.LotId == second.LotId && first.EdgeRole == second.EdgeRole && first.RunId == second.RunId
        && first.RunStart == second.RunStart && first.RunEnd == second.RunEnd;

    private static bool IsUnderNode(Node node, Node ancestor)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
            if (current == ancestor) return true;
        return false;
    }

    private static bool ReadStringMeta(Node node, string key, out string value)
    {
        value = node.HasMeta(key) ? node.GetMeta(key).AsString() : "";
        return value.Length > 0;
    }

    private static string? FenceWorkspacePath(string sourcePath) => sourcePath switch
    {
        OpenPartFenceSource => OpenPartFenceFile,
        FarBankFenceSource => FarBankFenceFile,
        _ => null
    };

    private static string FenceRunId(string sourcePath, string lotId, string edgeRole, Vector2 start, Vector2 end) =>
        FormattableString.Invariant($"{sourcePath}|{lotId}|{edgeRole}|{start.X:R},{start.Y:R}|{end.X:R},{end.Y:R}");

    private static bool TryFindUniqueLot(
        string sourceText, string lotId, out JsonObject root, out JsonObject lot, out string error)
    {
        root = null!;
        lot = null!;
        try
        {
            if (JsonNode.Parse(sourceText) is not JsonObject parsed || parsed["lots"] is not JsonArray lots)
            {
                error = "файл участков имеет неподдерживаемый формат.";
                return false;
            }

            var matches = lots.OfType<JsonObject>()
                .Where(item => string.Equals((string?)item["id"], lotId, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || (string?)matches[0]["kind"] == "keep")
            {
                error = "ID участка отсутствует, повторяется или не имеет редактируемого забора.";
                return false;
            }

            root = parsed;
            lot = matches[0];
            error = "";
            return true;
        }
        catch (JsonException errorValue)
        {
            error = "исходник участка нельзя прочитать: " + errorValue.Message;
            return false;
        }
    }

    private bool TryGetFreshFencePick(FencePick wanted, out FencePick current, out string error)
    {
        error = "";
        var world = CurrentFenceWorld();
        if (world is null)
        {
            current = null!;
            error = "предпросмотр заборов недоступен.";
            return false;
        }

        if (!TryGetPreviewSources(out var sources, out _, out error))
        {
            current = null!;
            return false;
        }
        if (!IsCurrentPreview(world, sources))
        {
            current = null!;
            error = "предпросмотр не соответствует текущим исходникам.";
            return false;
        }

        if (!TryFindLiveFencePick(world, wanted, out current, out error)) return false;
        try
        {
            if (!studio.Workspace.File(current.RelativePath).ChangedOnDisk()) return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = "не удалось проверить файл участка на диске.";
            current = null!;
            return false;
        }
        error = "файл участка изменился на диске; обновите сравнение перед правкой.";
        current = null!;
        return false;
    }

    internal bool HasFenceSelection
    {
        get
        {
            if (_fencePick is not { } selected) return false;
            try
            {
                return TryGetFreshFencePick(selected, out _, out _);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
                or InvalidDataException or InvalidOperationException or KeyNotFoundException or JsonException)
            {
                return false;
            }
        }
    }

    internal FenceAlignmentRequest CaptureFenceRequest(string prompt, string revision, string? requestId = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(revision);
        if (_fencePick is not { } selected)
            throw new InvalidOperationException("Сначала выберите свежий пролёт забора.");
        if (!TryGetFreshFencePick(selected, out var current, out var pickError))
            throw new InvalidOperationException("Выбранный пролёт забора устарел или недоступен: "
                + (pickError.Length == 0 ? "выберите забор заново." : pickError));
        if (current.BayCount < 3)
            throw new InvalidOperationException("Для запроса нужны три секции забора.");

        var sourceSnapshots = new Dictionary<string, AuthoredSourceSnapshot>(StringComparer.Ordinal);
        foreach (var sourcePath in new[] { OpenPartFenceFile, FarBankFenceFile })
        {
            if (!studio.Workspace.HasFile(sourcePath))
                throw new InvalidOperationException("Исходник забора больше не доступен в Studio: " + sourcePath);
            sourceSnapshots.Add(sourcePath, studio.Workspace.File(sourcePath).CaptureSource());
        }
        if (!sourceSnapshots.TryGetValue(current.RelativePath, out var targetSource))
            throw new InvalidOperationException("Источник выбранного пролёта не входит в снимок двух разрешённых файлов.");
        if (!TryFindUniqueLot(targetSource.Text, current.LotId, out _, out _, out var lotError))
            throw new InvalidDataException("Нельзя создать запрос для выбранного участка: " + lotError);
        var lotData = CaptureFenceLotData(targetSource.Text, current.LotId);

        if (_demo is null || !GodotObject.IsInstanceValid(_demo) || !_demo.IsInsideTree())
            throw new InvalidOperationException("Текущая демонстрационная сцена недоступна.");
        if (!string.Equals(_demo.SceneFilePath, "res://scenes/act1_demo.tscn", StringComparison.Ordinal))
            throw new InvalidOperationException("Fence authoring поддерживает только текущую сцену Act I.");
        var bridges = _demo.FindChildren("*", nameof(RuntimeBridge), true, false).OfType<RuntimeBridge>().ToArray();
        if (bridges.Length != 1 || !GodotObject.IsInstanceValid(bridges[0])
            || !bridges[0].IsInsideTree() || !IsUnderNode(bridges[0], _demo))
            throw new InvalidOperationException("В текущей демонстрационной сцене должен быть ровно один RuntimeBridge.");
        var bridge = bridges[0];
        if (!bridge.IsDebugSession || bridge.SessionIdentity is null
            || !string.Equals(bridge.CurrentZoneId, "village_day", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(bridge.CurrentSpawnPointId))
            throw new InvalidOperationException("Запрос забора доступен только в активной debug-сессии обычной деревни.");

        var context = CampaignWorldCatalog.Discover(studio.Workspace).Resolve(
            PlayPreparation.Campaign,
            _demo.SceneFilePath,
            bridge.CurrentZoneId,
            bridge.CurrentSpawnPointId,
            baseRevision: revision);
        if (context.World.Capability != WorldAuthoringCapability.AuthoredWorldPlots)
            throw new InvalidOperationException("Текущая сцена не подтверждена каталогом как потребитель авторских участков.");

        var dependencyHashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, snapshot) in sourceSnapshots)
            dependencyHashes.Add(path, snapshot.Sha256);
        foreach (var path in context.AuthoredDependencies.Concat(FenceBuildDependencyPaths)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (dependencyHashes.ContainsKey(path)) continue;
            dependencyHashes.Add(path, CaptureFenceDependencyHash(path));
        }

        var sourceHashes = sourceSnapshots.ToDictionary(
            pair => pair.Key,
            pair => new FenceSourceHashes(pair.Value.BaseSha256, pair.Value.Sha256),
            StringComparer.Ordinal);
        var start = Math.Clamp(current.BayIndex, 0, current.BayCount - 3);
        var target = new FenceAlignmentTarget(
            current.RelativePath,
            current.LotId,
            current.EdgeRole,
            current.RunId,
            start,
            current.BayCount,
            current.RunStart.X,
            current.RunStart.Y,
            current.RunEnd.X,
            current.RunEnd.Y);
        var environment = new HouseAuthoringEnvironment(
            context.BaseRevision,
            context.Campaign.Id,
            context.Campaign.ExactVersion,
            context.World.WorldId,
            context.StateId,
            context.World.ScenePath,
            bridge.CurrentZoneId,
            bridge.CurrentSpawnPointId,
            CaptureAuthoringCamera(),
            dependencyHashes);
        var request = new FenceAlignmentRequest(
            requestId ?? Guid.NewGuid().ToString("N"),
            string.Empty,
            prompt,
            target,
            lotData,
            sourceHashes,
            environment);
        request = request with { SnapshotHash = FenceAlignmentProtocol.ComputeSnapshotHash(request) };
        FenceAlignmentProtocol.ValidateRequest(request);
        return request;
    }

    internal void EnsureFenceRequestCurrent(FenceAlignmentRequest request, string revision)
    {
        ArgumentNullException.ThrowIfNull(request);
        FenceAlignmentProtocol.ValidateRequest(request);
        var current = CaptureFenceRequest(request.Prompt, revision, request.RequestId);
        if (!string.Equals(current.SnapshotHash, request.SnapshotHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Снимок забора изменился. Предложение больше нельзя применять; подготовьте новый запрос.");
    }

    private JsonElement CaptureFenceLotData(string sourceText, string lotId)
    {
        using var document = JsonDocument.Parse(sourceText, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 64
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("lots", out var lots)
            || lots.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Файл участка имеет неподдерживаемый формат.");

        var matches = lots.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String
                && string.Equals(id.GetString(), lotId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("ID выбранного участка отсутствует или повторяется.");
        return matches[0].Clone();
    }

    private string CaptureFenceDependencyHash(string relativePath)
    {
        StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, relativePath);
        if (studio.Workspace.HasFile(relativePath))
            return studio.Workspace.File(relativePath).CaptureSource().Sha256;
        var fullPath = Path.Combine(studio.Workspace.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return AtomicFile.Sha256OfFile(fullPath);
    }

    private bool FillFenceProperties(StudioProperties panel)
    {
        if (_fencePick is not null && studio.Selection is not null)
        {
            ClearFencePick();
            return false;
        }
        if (_fencePick is not { } selected) return false;
        if (!TryGetFreshFencePick(selected, out var current, out _))
        {
            ClearFencePick();
            return false;
        }

        _fencePick = current;
        UpdateFenceRangeMarker(current);
        var start = current.BayCount < 3 ? 0 : Math.Clamp(current.BayIndex, 0, current.BayCount - 3);
        panel.Header($"Забор · {current.LotId}",
            $"{FenceEdgeLabel(current.EdgeRole)} · секция {current.BayIndex + 1} из {current.BayCount}");
        panel.Text($"Источник: {current.RelativePath}", muted: true);
        if (current.BayCount < 3)
        {
            panel.Text("Нельзя выпрямить три секции: у этого пролёта меньше трёх.", muted: true);
            return true;
        }

        panel.Text($"Будут выпрямлены секции {start + 1}–{start + 3}; крайние стойки области останутся на месте.", muted: true);
        panel.Buttons(
            ("Выпрямить 3 секции", () => EditFenceAlignment(current, start, align: true)),
            ("Сбросить выпрямление", () => EditFenceAlignment(current, start, align: false)));
        return true;
    }

    private static string FenceEdgeLabel(string edgeRole) => edgeRole switch
    {
        "front" => "уличная сторона",
        "back" => "задняя сторона",
        "side-minus" => "боковая сторона −",
        "side-plus" => "боковая сторона +",
        _ => "сторона участка"
    };

    private void EditFenceAlignment(FencePick selected, int start, bool align)
    {
        if (studio.IsAiActionBusy)
        {
            studio.ShowBanner("Дождитесь завершения текущего действия Studio, прежде чем менять забор.", error: false);
            return;
        }

        if (!TryGetFreshFencePick(selected, out var current, out var error)
            || current.BayCount < 3 || start < 0 || start > current.BayCount - 3)
        {
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Забор не изменён: " + (error.Length == 0 ? "границы пролёта устарели." : error), error: true);
            return;
        }

        var interiorPosts = new[] { start + 1, start + 2 };
        var file = studio.Workspace.File(current.RelativePath);
        var expectedText = file.Text;
        var label = align ? "выпрямить три секции забора" : "сбросить выпрямление забора";
        string? replacementText = null;
        try
        {
            replacementText = Urman.Studio.Core.AI.FenceAlignmentSource.Rewrite(
                current.RelativePath, expectedText, current.LotId, current.RunId, current.BayCount, interiorPosts, align);
            if (replacementText == expectedText)
            {
                studio.RefreshStatus(align ? "Эти стойки уже выпрямлены" : "Для этих стоек нет выпрямления");
                return;
            }

            studio.Session.ReplaceDraft(current.RelativePath, expectedText, replacementText, label);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or KeyNotFoundException or FormatException or JsonException or InvalidDataException)
        {
            if (replacementText is not null && file.Text == replacementText && studio.Session.History().FirstOrDefault() is { } latest
                && latest.Label == label && latest.Paths.Contains(current.RelativePath, StringComparer.Ordinal))
                studio.ShowBanner("Изменение забора применено, но Studio не завершила обновление вида: " + failure.Message, error: true);
            else
                studio.ShowBanner("Забор не изменён: " + failure.Message, error: true);
        }
    }

    private void UpdateFenceRangeMarker(FencePick pick)
    {
        if (pick.BayCount < 3)
        {
            if (_fenceRangeMarker is not null && GodotObject.IsInstanceValid(_fenceRangeMarker))
                _fenceRangeMarker.Visible = false;
            return;
        }
        if (_viewport is null || !GodotObject.IsInstanceValid(_viewport)) return;
        _fenceRangeMarker ??= new MeshInstance3D
        {
            Name = "StudioFenceRange",
            Mesh = new BoxMesh(),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(.24f, .86f, .62f, .2f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                RenderPriority = 5
            }
        };
        if (_fenceRangeMarker.GetParent() is null) _viewport.AddChild(_fenceRangeMarker);
        var startIndex = Mathf.Clamp(pick.BayIndex, 0, pick.BayCount - 3);
        var delta = pick.RunEnd - pick.RunStart;
        var first = pick.RunStart + delta * (startIndex / (float)pick.BayCount);
        var last = pick.RunStart + delta * ((startIndex + 3) / (float)pick.BayCount);
        var centre = (first + last) * .5f;
        var ground = AgentBAct1HeightField.CollisionGround(centre.X, centre.Y);
        var direction = delta.Normalized();
        var length = first.DistanceTo(last);
        ((BoxMesh)_fenceRangeMarker.Mesh!).Size = new Vector3(.25f, 1.5f, length);
        _fenceRangeMarker.Transform = new Transform3D(
            new Basis(Vector3.Up, Mathf.Atan2(direction.X, direction.Y)),
            new Vector3(centre.X, ground + .75f, centre.Y));
        _fenceRangeMarker.Visible = true;
    }
}
