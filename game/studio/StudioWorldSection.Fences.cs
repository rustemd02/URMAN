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

    internal readonly record struct FencePreviewSources(string OpenPart, string FarBank);
    private sealed record FenceAiPreview(
        Act1ConnectedWorld World, FencePreviewSources WorkspaceSources, FencePreviewSources CandidateSources,
        FenceAlignmentRequest Request, FenceAlignmentProposal Proposal, bool Applied);
    private sealed record FenceSupportHandle(int PostIndex, bool Aligned, Vector3 Top);
    private sealed record FenceHandleDrag(
        FencePick Pick, FencePreviewSources Sources, AuthoredSourceSnapshot Source,
        int PostIndex, bool Align, int HandleSlot, Vector2 PressPosition);
    internal sealed record FenceApplyPlan(
        string RelativePath, string BeforeText, string CandidateText, string BeforeHash, string CandidateHash,
        FencePreviewSources BeforeSources, FencePreviewSources CandidateSources, IReadOnlySet<string> ExistingIdentityConflicts)
    {
        public StudioFileWrite Write => new(RelativePath, BeforeHash, new System.Text.UTF8Encoding(false, true).GetBytes(CandidateText));
    }

    private FencePick? _fencePick;
    private MeshInstance3D? _fenceRangeMarker;
    private readonly MeshInstance3D?[] _fenceSupportHandleMarkers = new MeshInstance3D?[2];
    private readonly Button?[] _fenceSupportHandleTargets = new Button?[2];
    private CollisionShape3D? _fenceSupportHandleSource;
    private FenceSupportHandle[]? _fenceSupportHandles;
    private FenceHandleDrag? _fenceHandleDrag;
    private bool FenceSupportToolActive => _measuring || _sculpting || _brushing
        || _placing is not null || _pointPick is not null;
    private Act1ConnectedWorld? _fencePreviewWorld;
    private FencePreviewSources? _fencePreviewedSources;
    private Act1ConnectedWorld? _fencePreviewFailedWorld;
    private FencePreviewSources? _fencePreviewFailedSources;
    private FenceAiPreview? _fenceAiPreview;
    private bool _fenceApplyInProgress;
    private Act1ConnectedWorld? _fenceAiPreviewRestoreFailedWorld;
    private FencePreviewSources? _fenceAiPreviewRestoreFailedSources;

    internal bool HasFenceAiPreview => _fenceAiPreview is not null;
    internal bool HasTransientFencePreview => HasFenceAiPreview || _fenceApplyInProgress;

    private void ClearFencePick()
    {
        CancelFenceHandleDrag();
        _fencePick = null;
        if (_fenceRangeMarker is not null && GodotObject.IsInstanceValid(_fenceRangeMarker))
            _fenceRangeMarker.Visible = false;
        HideFenceSupportHandles();
        RequestViewportRender();
    }

    private bool TryPickFence(Vector2 screen)
    {
        if (_fenceAiPreview is not null)
        {
            studio.ShowBanner("Сначала снимите временный предпросмотр AI забора.", error: false);
            return true;
        }

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
        if (_fenceApplyInProgress || studio.IsWorkspaceActionBusy) return;
        var world = CurrentFenceWorld();
        if (_fenceAiPreview is { } aiPreview)
        {
            var currentSources = TryGetPreviewSources(out var workspaceSources, out _, out _)
                && ReferenceEquals(world, aiPreview.World)
                && workspaceSources == aiPreview.WorkspaceSources
                && FenceSourcesUnchangedOnDisk();
            if (currentSources && aiPreview.Applied)
            {
                RefreshFenceSelection(aiPreview.World);
                return;
            }

            try
            {
                ClearFenceAiPreview(refreshWorkspaceDrift: false);
            }
            catch (Exception error)
            {
                var shouldReport = !ReferenceEquals(aiPreview.World, _fenceAiPreviewRestoreFailedWorld)
                    || aiPreview.WorkspaceSources != _fenceAiPreviewRestoreFailedSources;
                _fenceAiPreviewRestoreFailedWorld = aiPreview.World;
                _fenceAiPreviewRestoreFailedSources = aiPreview.WorkspaceSources;
                if (shouldReport)
                    studio.ShowBanner("Предпросмотр забора не снят: " + error.Message, error: true);
                return;
            }

            world = CurrentFenceWorld();
        }

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
            RequestViewportRender();
        }
        catch (Exception error)
        {
            // A failed rebuild can leave the old collision shapes visible; they are not editable against new source text.
            _fencePreviewWorld = null;
            _fencePreviewedSources = null;
            _fencePreviewFailedWorld = world;
            _fencePreviewFailedSources = sources;
            ClearFenceSelectionAndRefresh();
            RequestViewportRender();
            studio.ShowBanner("Заборы не обновлены: " + error.Message, error: true);
        }
    }

    internal void PreviewFenceProposal(
        FenceAlignmentRequest request, FenceAlignmentProposal proposal, string revision)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(revision);
        FenceAlignmentProtocol.ValidateRequest(request);
        FenceAlignmentProtocol.ValidateProposal(request, proposal);
        if (_fenceAiPreview is not null) ClearFenceAiPreview();
        EnsureFenceRequestCurrent(request, revision);
        if (proposal.PostIndices.Count == 0) return;
        if (_fencePick is not { } selected)
            throw new InvalidOperationException("Сначала выберите свежий пролёт забора.");
        if (!TryGetFreshFencePick(selected, out var current, out var pickError))
            throw new InvalidOperationException("Предпросмотр забора устарел: "
                + (pickError.Length == 0 ? "выберите пролёт заново." : pickError));
        if (current.RelativePath != request.Target.SourcePath || current.LotId != request.Target.LotId
            || current.RunId != request.Target.RunId || current.BayCount != request.Target.BayCount)
            throw new InvalidOperationException("Выбранный пролёт больше не совпадает с запросом.");
        if (!TryGetPreviewSources(out var workspaceSources, out var runtimeSources, out var sourceError))
            throw new InvalidOperationException("Предпросмотр забора недоступен: " + sourceError);

        var before = current.RelativePath switch
        {
            OpenPartFenceFile => workspaceSources.OpenPart,
            FarBankFenceFile => workspaceSources.FarBank,
            _ => throw new InvalidOperationException("Источник пролёта не входит в разрешённые файлы участков.")
        };
        var candidate = FenceAlignmentSource.Rewrite(current.RelativePath, before, current.LotId, current.RunId,
            current.BayCount, proposal.PostIndices, align: true);
        if (candidate == before) return;

        var candidateSources = current.RelativePath == OpenPartFenceFile
            ? workspaceSources with { OpenPart = candidate }
            : workspaceSources with { FarBank = candidate };
        if (current.RelativePath == OpenPartFenceFile) runtimeSources[OpenPartFenceSource] = candidate;
        else runtimeSources[FarBankFenceSource] = candidate;

        var frozenRequest = FenceAlignmentProtocol.ParseRequest(FenceAlignmentProtocol.SerializeRequest(request));
        var frozenProposal = FenceAlignmentProtocol.ParseProposal(
            FenceAlignmentProtocol.SerializeProposal(proposal), frozenRequest);
        var world = CurrentFenceWorld()
            ?? throw new InvalidOperationException("Текущая сцена больше не содержит предпросмотр заборов.");
        _fenceAiPreview = new FenceAiPreview(world, workspaceSources, candidateSources,
            frozenRequest, frozenProposal, Applied: false);
        try
        {
            world.RefreshYardFencesForStudio(runtimeSources);
            _fenceAiPreview = _fenceAiPreview with { Applied = true };
            RefreshFenceSelection(world);
            RequestViewportRender();
        }
        catch (Exception previewFailure)
        {
            try
            {
                ClearFenceAiPreview();
            }
            catch (Exception restoreFailure)
            {
                RequestViewportRender();
                throw new AggregateException("Предпросмотр забора не удался, а исходный вид не удалось восстановить.",
                    previewFailure, restoreFailure);
            }
            throw;
        }
    }

    internal void ClearFenceAiPreview()
        => ClearFenceAiPreview(refreshWorkspaceDrift: true);

    internal FenceApplyPlan BeginFenceApply(
        FenceAlignmentRequest request, FenceAlignmentProposal proposal, string revision)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(revision);
        if (_fenceApplyInProgress || studio.IsWorkspaceActionBusy)
            throw new InvalidOperationException("Дождитесь завершения другого действия Studio.");
        if (_fenceAiPreview is not { Applied: true } preview)
            throw new InvalidOperationException("Сначала покажите предложение в свежем предпросмотре.");

        FenceAlignmentProtocol.ValidateRequest(request);
        FenceAlignmentProtocol.ValidateProposal(request, proposal);
        if (proposal.PostIndices.Count == 0
            || !string.Equals(FenceAlignmentProtocol.SerializeRequest(request), FenceAlignmentProtocol.SerializeRequest(preview.Request), StringComparison.Ordinal)
            || !string.Equals(FenceAlignmentProtocol.SerializeProposal(proposal), FenceAlignmentProtocol.SerializeProposal(preview.Proposal), StringComparison.Ordinal))
            throw new InvalidOperationException("Предложение больше не совпадает с подтверждённым предпросмотром.");
        EnsureFenceRequestCurrent(request, revision);

        if (_fencePick is not { } selected)
            throw new InvalidOperationException("Сначала выберите свежий пролёт забора.");
        if (!TryGetFreshFencePick(selected, out var current, out var pickError))
            throw new InvalidOperationException("Выбранный пролёт устарел: " + (pickError.Length == 0 ? "выберите его заново." : pickError));
        var target = request.Target;
        if (current.RelativePath != target.SourcePath || current.LotId != target.LotId
            || current.EdgeRole != target.EdgeRole || current.RunId != target.RunId
            || current.BayCount != target.BayCount
            || target.BayStart != Math.Clamp(current.BayIndex, 0, current.BayCount - 3)
            || target.StartX != current.RunStart.X || target.StartZ != current.RunStart.Y
            || target.EndX != current.RunEnd.X || target.EndZ != current.RunEnd.Y)
            throw new InvalidOperationException("Выбранный участок, пролёт, геометрия или диапазон секций изменились.");
        if (!TryGetPreviewSources(out var beforeSources, out _, out var sourceError)
            || beforeSources != preview.WorkspaceSources)
            throw new InvalidOperationException("Исходники участков больше не совпадают с предпросмотром: " + sourceError);

        if (!request.Sources.TryGetValue(current.RelativePath, out var frozenHashes))
            throw new InvalidOperationException("Источник выбранного пролёта отсутствует в снимке запроса.");
        var source = studio.Workspace.File(current.RelativePath).CaptureSource();
        if (source.Dirty || source.BaseSha256 is null
            || !string.Equals(source.BaseSha256, source.Sha256, StringComparison.Ordinal)
            || !string.Equals(source.BaseSha256, frozenHashes.BaseHash, StringComparison.Ordinal)
            || !string.Equals(source.Sha256, frozenHashes.CurrentHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Сохранённый источник изменился после снимка. Сохраните его и подготовьте новый запрос.");

        var candidateText = FenceAlignmentSource.Rewrite(current.RelativePath, source.Text, current.LotId,
            current.RunId, current.BayCount, proposal.PostIndices, align: true);
        if (candidateText == source.Text)
            throw new InvalidOperationException("Предложение не меняет выбранный пролёт.");
        var candidateSources = current.RelativePath == OpenPartFenceFile
            ? beforeSources with { OpenPart = candidateText }
            : beforeSources with { FarBank = candidateText };
        if (candidateSources != preview.CandidateSources)
            throw new InvalidOperationException("Кандидат больше не совпадает с показанным предпросмотром.");

        var plan = new FenceApplyPlan(current.RelativePath, source.Text, candidateText,
            source.Sha256, AtomicFile.Sha256(new System.Text.UTF8Encoding(false, true).GetBytes(candidateText)),
            beforeSources, candidateSources, studio.Workspace.IdentityConflicts.ToHashSet(StringComparer.Ordinal));
        _fenceApplyInProgress = true;
        try
        {
            ClearFenceAiPreview(refreshWorkspaceDrift: false);
        }
        catch
        {
            _fenceApplyInProgress = false;
            throw;
        }
        return plan;
    }

    internal Task RefreshFenceApplyAfterPromotionAsync(FenceApplyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!_fenceApplyInProgress || FenceWorkspacePath(plan.RelativePath) is null)
            throw new InvalidOperationException("Транзакция забора потеряла своё разрешённое состояние.");

        StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, plan.RelativePath);
        var diskHash = AtomicFile.Sha256OfFile(Path.Combine(studio.Workspace.Root,
            plan.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var candidateSide = string.Equals(diskHash, plan.CandidateHash, StringComparison.Ordinal);
        if (!candidateSide && !string.Equals(diskHash, plan.BeforeHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Файл забора изменился вне этой транзакции.");
        var expectedText = candidateSide ? plan.CandidateText : plan.BeforeText;
        var expectedSources = candidateSide ? plan.CandidateSources : plan.BeforeSources;

        var file = studio.Workspace.File(plan.RelativePath);
        if (file.Dirty) throw new InvalidOperationException("Черновик источника появился во время транзакции.");
        if (file.ChangedOnDisk())
        {
            var pulled = file.PullFromDisk();
            if (!pulled.Reloaded || pulled.Conflicts.Count != 0)
                throw new InvalidOperationException("Не удалось синхронизировать сохранённый источник забора.");
        }
        if (file.Dirty || !string.Equals(file.Text, expectedText, StringComparison.Ordinal))
            throw new InvalidOperationException("Черновик Studio не совпадает с сохранённой стороной транзакции.");

        studio.Workspace.Reindex();
        if (studio.Workspace.IdentityConflicts.Any(conflict => !plan.ExistingIdentityConflicts.Contains(conflict)))
            throw new InvalidOperationException("Сохранение создало повторяющиеся ID объектов Studio.");
        if (!FenceSourcesUnchangedOnDisk())
            throw new InvalidOperationException("Один из исходников участков изменился вне этой транзакции.");
        if (!TryGetPreviewSources(out var workspaceSources, out var runtimeSources, out var sourceError)
            || workspaceSources != expectedSources)
            throw new InvalidOperationException("Источники мира не совпадают со стороной транзакции: " + sourceError);
        var world = CurrentFenceWorld()
            ?? throw new InvalidOperationException("Текущий игровой мир недоступен для обновления забора.");
        _fencePreviewWorld = null;
        _fencePreviewedSources = null;
        _fencePreviewFailedWorld = world;
        _fencePreviewFailedSources = workspaceSources;
        world.RefreshYardFencesForStudio(runtimeSources);
        _fencePreviewWorld = world;
        _fencePreviewedSources = workspaceSources;
        _fencePreviewFailedWorld = null;
        _fencePreviewFailedSources = null;
        _fenceAiPreviewRestoreFailedWorld = null;
        _fenceAiPreviewRestoreFailedSources = null;
        RefreshFenceSelection(world);
        RequestViewportRender();
        return Task.CompletedTask;
    }

    internal bool FinishFenceApply(FenceApplyPlan plan, bool applied)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!_fenceApplyInProgress) return false;
        var expectedText = applied ? plan.CandidateText : plan.BeforeText;
        var expectedHash = applied ? plan.CandidateHash : plan.BeforeHash;
        var expectedSources = applied ? plan.CandidateSources : plan.BeforeSources;
        try
        {
            StudioFileTransaction.EnsureNoLinks(studio.Workspace.Root, plan.RelativePath);
            var fullPath = Path.Combine(studio.Workspace.Root, plan.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!string.Equals(AtomicFile.Sha256OfFile(fullPath), expectedHash, StringComparison.Ordinal)) return false;
            var file = studio.Workspace.File(plan.RelativePath);
            if (file.ChangedOnDisk())
            {
                if (file.Dirty) return false;
                var pulled = file.PullFromDisk();
                if (!pulled.Reloaded || pulled.Conflicts.Count != 0) return false;
                studio.Workspace.Reindex();
            }
            if (file.Dirty || !string.Equals(file.Text, expectedText, StringComparison.Ordinal)) return false;
            if (!FenceSourcesUnchangedOnDisk()) return false;
            if (studio.Workspace.IdentityConflicts.Any(conflict => !plan.ExistingIdentityConflicts.Contains(conflict))) return false;
            if (!TryGetPreviewSources(out var sources, out _, out _) || sources != expectedSources) return false;
            var world = CurrentFenceWorld();
            if (world is null || !ReferenceEquals(world, _fencePreviewWorld)
                || _fencePreviewedSources != expectedSources || _fenceAiPreview is not null)
                return false;
            _fenceApplyInProgress = false;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or InvalidDataException or KeyNotFoundException)
        {
            return false;
        }
    }

    private void ClearFenceAiPreview(bool refreshWorkspaceDrift)
    {
        if (_fenceAiPreview is not { } preview) return;
        if (!GodotObject.IsInstanceValid(preview.World))
        {
            _fenceAiPreview = null;
            _fenceAiPreviewRestoreFailedWorld = null;
            _fenceAiPreviewRestoreFailedSources = null;
            _fencePreviewWorld = null;
            _fencePreviewedSources = null;
            _fencePreviewFailedWorld = null;
            _fencePreviewFailedSources = null;
            ClearFenceSelectionAndRefresh();
            if (refreshWorkspaceDrift) SyncFencePreview();
            return;
        }

        var workspaceDrift = !TryGetPreviewSources(out var currentWorkspaceSources, out _, out _)
            || currentWorkspaceSources != preview.WorkspaceSources;
        preview.World.RefreshYardFencesForStudio(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OpenPartFenceSource] = preview.WorkspaceSources.OpenPart,
            [FarBankFenceSource] = preview.WorkspaceSources.FarBank
        });
        RequestViewportRender();
        _fenceAiPreview = null;
        _fenceAiPreviewRestoreFailedWorld = null;
        _fenceAiPreviewRestoreFailedSources = null;
        _fencePreviewFailedWorld = null;
        _fencePreviewFailedSources = null;
        var currentWorld = CurrentFenceWorld();
        if (ReferenceEquals(currentWorld, preview.World))
        {
            _fencePreviewWorld = preview.World;
            _fencePreviewedSources = preview.WorkspaceSources;
            try
            {
                RefreshFenceSelection(preview.World);
            }
            catch (Exception error)
            {
                ClearFenceSelectionAndRefresh();
                studio.ShowBanner("Предпросмотр снят, но выбор пролёта пришлось сбросить: " + error.Message, error: false);
            }
        }
        else
        {
            _fencePreviewWorld = null;
            _fencePreviewedSources = null;
            ClearFenceSelectionAndRefresh();
        }
        if (refreshWorkspaceDrift && workspaceDrift) SyncFencePreview();
    }

    private bool FenceSourcesUnchangedOnDisk()
    {
        try
        {
            return studio.Workspace.HasFile(OpenPartFenceFile) && studio.Workspace.HasFile(FarBankFenceFile)
                && !studio.Workspace.File(OpenPartFenceFile).ChangedOnDisk()
                && !studio.Workspace.File(FarBankFenceFile).ChangedOnDisk();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
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
        if (_fenceAiPreview is not null) return false;
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

    private bool IsCurrentAiFencePreview(Act1ConnectedWorld world, FencePreviewSources sources)
    {
        return _fenceAiPreview is { Applied: true } preview
            && ReferenceEquals(world, preview.World)
            && preview.WorkspaceSources == sources
            && FenceSourcesUnchangedOnDisk();
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
        if (!IsCurrentPreview(world, sources) && !IsCurrentAiFencePreview(world, sources))
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
        string zoneId;
        string spawnPointId;
        string? previewStateId = null;
        if (_demo.StudioPreviewMode)
        {
            var world = CurrentFenceWorld();
            var main = _demo.DemoMain;
            if (!_demo.StudioPreviewReady || world is null || !world.IsBuilt
                || !ReferenceEquals(main.ConnectedWorld, world) || !IsUnderNode(world, _demo)
                || !string.Equals(world.ActiveZoneId, "village_day", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(main.InitialSpawnPointId))
                throw new InvalidOperationException("Запрос забора доступен только в готовом предпросмотре деревни Studio.");
            zoneId = world.ActiveZoneId;
            spawnPointId = main.InitialSpawnPointId;
            previewStateId = $"studio-preview:{_demo.GetInstanceId():x}:{world.PersistentInstanceIdentity}";
        }
        else
        {
            var bridges = _demo.FindChildren("*", nameof(RuntimeBridge), true, false).OfType<RuntimeBridge>().ToArray();
            if (bridges.Length != 1 || !GodotObject.IsInstanceValid(bridges[0])
                || !bridges[0].IsInsideTree() || !IsUnderNode(bridges[0], _demo))
                throw new InvalidOperationException("В текущей демонстрационной сцене должен быть ровно один RuntimeBridge.");
            var bridge = bridges[0];
            if (!bridge.IsDebugSession || bridge.SessionIdentity is null
                || !string.Equals(bridge.CurrentZoneId, "village_day", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(bridge.CurrentSpawnPointId))
                throw new InvalidOperationException("Запрос забора доступен только в активной debug-сессии обычной деревни.");
            zoneId = bridge.CurrentZoneId;
            spawnPointId = bridge.CurrentSpawnPointId;
        }

        var context = CampaignWorldCatalog.Discover(studio.Workspace).Resolve(
            PlayPreparation.Campaign,
            _demo.SceneFilePath,
            zoneId,
            spawnPointId,
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
            previewStateId is null ? context.StateId : $"{context.StateId}/{previewStateId}",
            context.World.ScenePath,
            zoneId,
            spawnPointId,
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
        if (_fenceAiPreview is { } aiPreview)
        {
            if (_fencePick is { } previewPick)
                panel.Header($"Забор · {previewPick.LotId}",
                    $"{FenceEdgeLabel(previewPick.EdgeRole)} · AI предпросмотр");
            else
                panel.Header("AI предпросмотр забора", "Изменение временное и ещё не применено к исходнику.");
            panel.Text("Предложение сейчас показано в мире; снимите его перед ручной правкой.", muted: true);
            if (!aiPreview.Applied)
                panel.Text("Studio восстанавливает исходный вид.", muted: true);
            return true;
        }
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
        panel.Text("Перетащите цветную метку одной из двух внутренних стоек на подпись: выпрямление или возврат исходного наклона. Изменение попадёт в черновик только при отпускании.", muted: true);
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
        if (start < 0 || start > selected.BayCount - 3)
        {
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Забор не изменён: диапазон секций устарел.", error: true);
            return;
        }

        EditFenceAlignmentPosts(selected, [start + 1, start + 2], align,
            align ? "выпрямить три секции забора" : "сбросить выпрямление забора", expectedText: null);
    }

    private bool EditFenceAlignmentPosts(
        FencePick selected, IReadOnlyList<int> postIndices, bool align, string label, string? expectedText)
    {
        if (_fenceApplyInProgress || _reloading || studio.IsWorkspaceActionBusy)
        {
            studio.ShowBanner("Дождитесь завершения текущего действия Studio, прежде чем менять забор.", error: false);
            return false;
        }

        if (_fenceAiPreview is not null)
        {
            studio.ShowBanner("Сначала снимите временный предпросмотр AI забора.", error: false);
            return false;
        }

        if (studio.IsAiActionBusy)
        {
            studio.ShowBanner("Дождитесь завершения текущего действия Studio, прежде чем менять забор.", error: false);
            return false;
        }

        if (!TryGetFreshFencePick(selected, out var current, out var error)
            || current.BayCount < 3 || postIndices.Count is < 1 or > 2)
        {
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Забор не изменён: " + (error.Length == 0 ? "границы пролёта устарели." : error), error: true);
            return false;
        }

        var selectedStart = Math.Clamp(current.BayIndex, 0, current.BayCount - 3);
        if (postIndices.Any(index => index <= selectedStart || index >= selectedStart + 3))
        {
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Забор не изменён: стойка выходит за выбранные три секции.", error: true);
            return false;
        }

        var file = studio.Workspace.File(current.RelativePath);
        var currentText = file.Text;
        if (expectedText is not null && !string.Equals(currentText, expectedText, StringComparison.Ordinal))
        {
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Забор не изменён: черновик исходника поменялся во время перетаскивания.", error: true);
            return false;
        }

        expectedText ??= currentText;
        string? replacementText = null;
        try
        {
            replacementText = Urman.Studio.Core.AI.FenceAlignmentSource.Rewrite(
                current.RelativePath, expectedText, current.LotId, current.RunId, current.BayCount, postIndices, align);
            if (replacementText == expectedText)
            {
                studio.RefreshStatus(align ? "Эти стойки уже выпрямлены" : "Для этих стоек нет выпрямления");
                return false;
            }

            studio.Session.ReplaceDraft(current.RelativePath, expectedText, replacementText, label);
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or KeyNotFoundException or FormatException or JsonException or InvalidDataException)
        {
            if (replacementText is not null && file.Text == replacementText && studio.Session.History().FirstOrDefault() is { } latest
                && latest.Label == label && latest.Paths.Contains(current.RelativePath, StringComparer.Ordinal))
                studio.ShowBanner("Изменение забора применено, но Studio не завершила обновление вида: " + failure.Message, error: true);
            else
                studio.ShowBanner("Забор не изменён: " + failure.Message, error: true);
            return false;
        }
    }

    private void EnsureFenceSupportHandleControls()
    {
        if (_container is null || !GodotObject.IsInstanceValid(_container)
            || _viewport is null || !GodotObject.IsInstanceValid(_viewport)) return;

        for (var slot = 0; slot < 2; slot++)
        {
            if (_fenceSupportHandleMarkers[slot] is not { } marker || !GodotObject.IsInstanceValid(marker))
            {
                var tint = slot == 0 ? new Color("83d6a1") : new Color("8fc8e4");
                marker = new MeshInstance3D
                {
                    Name = $"StudioFenceSupportHandle_{slot + 1}",
                    Mesh = new SphereMesh { Radius = .16f, Height = .32f },
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = tint,
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        NoDepthTest = true,
                        RenderPriority = 20
                    },
                    Visible = false
                };
                _viewport.AddChild(marker);
                _fenceSupportHandleMarkers[slot] = marker;
            }

            if (_fenceSupportHandleTargets[slot] is not { } target || !GodotObject.IsInstanceValid(target))
            {
                target = new Button
                {
                    Name = $"StudioFenceSupportDropTarget_{slot + 1}",
                    Text = "Выровнять",
                    TooltipText = "Перетащите сюда маркер стойки. Это действие меняет только одну стойку выбранного забора.",
                    FocusMode = Control.FocusModeEnum.None,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    CustomMinimumSize = new Vector2(150, 34),
                    Size = new Vector2(150, 34),
                    Visible = false
                };
                _container.AddChild(target);
                _fenceSupportHandleTargets[slot] = target;
            }
        }
    }

    private bool TryGetFenceSupportHandles(FencePick pick, out FenceSupportHandle[] handles)
    {
        handles = [];
        if (ReferenceEquals(_fenceSupportHandleSource, pick.Shape)
            && _fenceSupportHandles is { Length: 2 } cached
            && GodotObject.IsInstanceValid(pick.Shape) && pick.Shape.IsInsideTree())
        {
            handles = cached;
            return true;
        }

        var world = CurrentFenceWorld();
        if (world is null || !GodotObject.IsInstanceValid(pick.Shape) || !pick.Shape.IsInsideTree()
            || !IsUnderNode(pick.Shape, world) || pick.BayCount < 3)
        {
            _fenceSupportHandleSource = null;
            _fenceSupportHandles = null;
            return false;
        }

        _fenceSupportHandleSource = null;
        _fenceSupportHandles = null;

        var start = Math.Clamp(pick.BayIndex, 0, pick.BayCount - 3);
        var delta = pick.RunEnd - pick.RunStart;
        if (delta.LengthSquared() < .0001f) return false;
        var along = new Vector3(delta.X, 0f, delta.Y).Normalized();
        if (!float.IsFinite(along.X) || !float.IsFinite(along.Z) || along.LengthSquared() < .99f) return false;

        if (pick.Shape.GetParent() is not CollisionObject3D owner) return false;
        var supportHandles = new FenceSupportHandle[2];
        var supportShapes = new CollisionShape3D?[2];
        var wantedPostIndices = new[] { start + 1, start + 2 };
        foreach (var child in owner.GetChildren())
        {
            if (child is not CollisionShape3D shape || !shape.HasMeta("runId")
                || !string.Equals(shape.GetMeta("runId").AsString(), pick.RunId, StringComparison.Ordinal)
                || !shape.HasMeta("bayIndex")) continue;

            var bayIndex = shape.GetMeta("bayIndex").AsInt32();
            for (var slot = 0; slot < wantedPostIndices.Length; slot++)
            {
                if (bayIndex != wantedPostIndices[slot]) continue;
                if (supportShapes[slot] is not null) return false;
                supportShapes[slot] = shape;
            }
        }

        for (var slot = 0; slot < supportHandles.Length; slot++)
        {
            var postIndex = wantedPostIndices[slot];
            if (supportShapes[slot] is not { } shape) return false;
            if (!IsUnderNode(shape, world) || shape.Shape is not BoxShape3D bay
                || !shape.HasMeta("footStart") || !shape.HasMeta("groundStartY")
                || !shape.HasMeta("postLeanDegrees") || !shape.HasMeta("alignedStartPost")) return false;
            var foot = shape.GetMeta("footStart").AsVector3();
            var ground = shape.GetMeta("groundStartY").AsSingle();
            // The builder stores its already-radian `lean` value in this legacy
            // metadata field; use the same units as PostPointAt.
            var leanRadians = shape.GetMeta("postLeanDegrees").AsSingle();
            var aligned = shape.GetMeta("alignedStartPost").AsBool();
            var rise = ground + bay.Size.Y + .11f - foot.Y;
            var crown = rise + .045f;
            if (!IsFinite(foot) || !float.IsFinite(ground) || !float.IsFinite(leanRadians)
                || !float.IsFinite(rise) || rise <= 0f || !float.IsFinite(crown)
                || (aligned && Mathf.Abs(leanRadians) > .001f)) return false;

            var top = foot + Vector3.Up * crown
                + along * (crown * Mathf.Tan(leanRadians));
            if (!IsFinite(top)) return false;
            supportHandles[slot] = new FenceSupportHandle(postIndex, aligned, top);
        }

        _fenceSupportHandleSource = pick.Shape;
        _fenceSupportHandles = supportHandles;
        handles = supportHandles;
        return true;
    }

    private void UpdateFenceSupportHandleVisuals()
    {
        if (_fencePick is not { } pick || studio.Selection is not null || _fenceAiPreview is not null || _fenceApplyInProgress
            || _reloading || studio.IsAiActionBusy || studio.IsWorkspaceActionBusy
            || FenceSupportToolActive
            || _camera is null || !GodotObject.IsInstanceValid(_camera)
            || _container is null || !GodotObject.IsInstanceValid(_container)
            || !TryGetFenceSupportHandles(pick, out var handles))
        {
            if (_fenceHandleDrag is not null) CancelFenceHandleDrag();
            HideFenceSupportHandles();
            return;
        }

        _fenceSupportHandles = handles;
        EnsureFenceSupportHandleControls();
        var containerSize = _container.Size;
        if (containerSize.X <= 0f || containerSize.Y <= 0f)
        {
            HideFenceSupportHandles();
            return;
        }

        for (var slot = 0; slot < handles.Length; slot++)
        {
            var marker = _fenceSupportHandleMarkers[slot];
            var target = _fenceSupportHandleTargets[slot];
            if (marker is null || target is null || !GodotObject.IsInstanceValid(marker)
                || !GodotObject.IsInstanceValid(target)) continue;

            var handle = handles[slot];
            var cameraLocal = _camera.ToLocal(handle.Top);
            var screen = _camera.UnprojectPosition(handle.Top);
            if (cameraLocal.Z >= 0f || !IsFiniteScreen(screen)
                || screen.X < -32f || screen.Y < -32f
                || screen.X > containerSize.X + 32f || screen.Y > containerSize.Y + 32f)
            {
                marker.Visible = false;
                target.Visible = false;
                continue;
            }

            marker.GlobalPosition = handle.Top;
            marker.Visible = true;
            target.Text = handle.Aligned ? "↶ Вернуть наклон" : "↕ Выровнять";
            target.TooltipText = handle.Aligned
                ? "Перетащите маркер в эту зону, чтобы восстановить исходный наклон стойки."
                : "Перетащите маркер в эту зону, чтобы поставить стойку вертикально.";
            target.Modulate = handle.Aligned ? new Color("e8bd76") : new Color("93d4a5");
            var offset = slot == 0 ? new Vector2(78f, -34f) : new Vector2(78f, 34f);
            var topLeft = screen + offset - target.Size * .5f;
            topLeft.X = Mathf.Clamp(topLeft.X, 2f, Mathf.Max(2f, containerSize.X - target.Size.X - 2f));
            topLeft.Y = Mathf.Clamp(topLeft.Y, 2f, Mathf.Max(2f, containerSize.Y - target.Size.Y - 2f));
            target.Position = topLeft;
            target.Visible = true;
        }

        RequestViewportRender();
    }

    private bool TryBeginFenceHandleDrag(Vector2 screen)
    {
        if (FenceSupportToolActive || _fencePick is not { } selected) return false;
        if (_fenceSupportHandles is not { Length: 2 } cachedHandles) return false;

        var nearestSlot = -1;
        var nearestDistance = 24f;
        for (var slot = 0; slot < cachedHandles.Length; slot++)
        {
            var marker = _fenceSupportHandleMarkers[slot];
            if (marker is null || !GodotObject.IsInstanceValid(marker) || !marker.Visible) continue;
            var distance = _camera.UnprojectPosition(cachedHandles[slot].Top).DistanceTo(screen);
            if (!float.IsFinite(distance) || distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearestSlot = slot;
        }
        if (nearestSlot < 0) return false;

        if (_fenceAiPreview is not null || _fenceApplyInProgress || _reloading
            || studio.IsAiActionBusy || studio.IsWorkspaceActionBusy)
        {
            studio.ShowBanner("Дождитесь завершения текущего действия Studio, прежде чем менять стойку.", error: false);
            return true;
        }

        if (!TryGetFreshFencePick(selected, out var current, out var error))
        {
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Перетаскивание стойки отклонено: "
                + (error.Length == 0 ? "выбранный участок устарел или недоступен." : error), error: true);
            return true;
        }
        if (!TryGetPreviewSources(out var sources, out _, out error)
            || CurrentFenceWorld() is not { } world || !IsCurrentPreview(world, sources)
            || !SameFenceRun(selected, current) || current.BayIndex != selected.BayIndex
            || current.BayCount != selected.BayCount
            || !studio.Workspace.HasFile(current.RelativePath))
        {
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Перетаскивание стойки отклонено: "
                + (error.Length == 0 ? "предпросмотр или источник устарел." : error), error: true);
            return true;
        }
        if (!TryGetFenceSupportHandles(current, out var handles)
            || handles[nearestSlot].PostIndex != cachedHandles[nearestSlot].PostIndex
            || handles[nearestSlot].Aligned != cachedHandles[nearestSlot].Aligned
            || handles[nearestSlot].Top.DistanceTo(cachedHandles[nearestSlot].Top) > .01f)
        {
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Перетаскивание стойки отклонено: опора изменилась после отображения ручки.", error: true);
            return true;
        }

        try
        {
            var source = studio.Workspace.File(current.RelativePath).CaptureSource();
            if (!string.Equals(source.Text,
                    current.RelativePath == OpenPartFenceFile ? sources.OpenPart : sources.FarBank,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("Черновик не совпадает с текущим предпросмотром.");

            var handle = handles[nearestSlot];
            _fenceHandleDrag = new FenceHandleDrag(current, sources, source,
                handle.PostIndex, Align: !handle.Aligned, HandleSlot: nearestSlot, PressPosition: screen);
            _container.GrabFocus();
            UpdateFenceHandleDropHighlight(nearestSlot, false);
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or InvalidDataException)
        {
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Перетаскивание стойки отклонено: " + failure.Message, error: true);
            return true;
        }
    }

    private bool IsFenceHandleDragCurrent(FenceHandleDrag drag, out string error)
    {
        error = "";
        if (_fenceApplyInProgress || _reloading || studio.IsAiActionBusy || studio.IsWorkspaceActionBusy || _fenceAiPreview is not null
            || FenceSupportToolActive
            || studio.Selection is not null || _fencePick is not { } selected
            || !ReferenceEquals(drag.Pick.Shape, selected.Shape) || !SameFenceRun(drag.Pick, selected)
            || selected.BayIndex != drag.Pick.BayIndex)
        {
            error = "выбор, предпросмотр или действие Studio изменились.";
            return false;
        }
        if (!TryGetFreshFencePick(drag.Pick, out var current, out error)
            || !ReferenceEquals(drag.Pick.Shape, current.Shape)
            || !SameFenceRun(drag.Pick, current) || current.BayIndex != drag.Pick.BayIndex
            || current.BayCount != drag.Pick.BayCount) return false;
        if (!TryGetPreviewSources(out var sources, out _, out error) || sources != drag.Sources)
        {
            error = "исходники забора изменились во время перетаскивания.";
            return false;
        }

        try
        {
            if (studio.Workspace.File(current.RelativePath).CaptureSource() != drag.Source)
            {
                error = "черновик забора изменился во время перетаскивания.";
                return false;
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidDataException or InvalidOperationException)
        {
            error = "не удалось подтвердить исходник: " + failure.Message;
            return false;
        }

        return true;
    }

    private void UpdateFenceHandleDragInput(Vector2 screen)
    {
        if (_fenceHandleDrag is not { } drag) return;
        UpdateFenceHandleDropHighlight(drag.HandleSlot, IsInsideFenceHandleDropTarget(drag.HandleSlot, screen));
    }

    private bool HandleFenceSupportDragInput(InputEvent @event)
    {
        if (_fenceHandleDrag is null) return false;
        if (FenceSupportToolActive)
        {
            CancelFenceHandleDrag();
            return false;
        }

        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: global::Godot.Key.Escape }:
                CancelFenceHandleDrag();
                UpdateFenceSupportHandleVisuals();
                _container.AcceptEvent();
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release:
                FinishFenceHandleDrag(release.Position);
                _container.AcceptEvent();
                return true;
            case InputEventMouseMotion motion when motion.ButtonMask.HasFlag(MouseButtonMask.Left):
                UpdateFenceHandleDragInput(motion.Position);
                _container.AcceptEvent();
                return true;
            case InputEventMouseMotion:
                CancelFenceHandleDrag();
                return false;
            default:
                // Keep a grabbed handle isolated from camera navigation and other edits.
                return true;
        }
    }

    private void FinishFenceHandleDrag(Vector2 screen)
    {
        if (_fenceHandleDrag is not { } drag) return;
        var isTarget = drag.PressPosition.DistanceTo(screen) >= DragThresholdPixels
            && IsInsideFenceHandleDropTarget(drag.HandleSlot, screen);
        CancelFenceHandleDrag();
        if (!isTarget)
        {
            UpdateFenceSupportHandleVisuals();
            return;
        }

        if (!IsFenceHandleDragCurrent(drag, out var error))
        {
            ClearFenceSelectionAndRefresh();
            studio.ShowBanner("Перетаскивание стойки отменено: " + error, error: true);
            return;
        }

        if (EditFenceAlignmentPosts(drag.Pick, [drag.PostIndex], drag.Align,
            drag.Align ? "выпрямить одну стойку забора" : "вернуть исходный наклон стойки забора",
            drag.Source.Text))
            studio.RefreshStatus(drag.Align
                ? "Одна стойка выпрямлена; Ctrl/⌘Z отменит последнее действие Studio."
                : "Исходный наклон стойки восстановлен одним действием Studio.");
        UpdateFenceSupportHandleVisuals();
    }

    private bool IsInsideFenceHandleDropTarget(int slot, Vector2 screen)
    {
        if (slot < 0 || slot >= _fenceSupportHandleTargets.Length
            || _fenceSupportHandleTargets[slot] is not { } target
            || !GodotObject.IsInstanceValid(target) || !target.Visible) return false;
        return new Rect2(target.Position, target.Size).Grow(10f).HasPoint(screen);
    }

    private void UpdateFenceHandleDropHighlight(int slot, bool active)
    {
        if (slot < 0 || slot >= _fenceSupportHandleTargets.Length
            || _fenceSupportHandleTargets[slot] is not { } target
            || !GodotObject.IsInstanceValid(target)) return;
        if (active)
        {
            target.Modulate = new Color("fff0a5");
            target.Scale = new Vector2(1.06f, 1.06f);
        }
        else
        {
            var aligned = false;
            if (_fenceHandleDrag is { } drag && drag.HandleSlot == slot)
                aligned = !drag.Align;
            else if (_fenceSupportHandles is { Length: 2 } handles)
                aligned = handles[slot].Aligned;
            target.Modulate = aligned ? new Color("e8bd76") : new Color("93d4a5");
            target.Scale = Vector2.One;
        }
    }

    private void CancelFenceHandleDrag()
    {
        var drag = _fenceHandleDrag;
        _fenceHandleDrag = null;
        if (drag is not null) UpdateFenceHandleDropHighlight(drag.HandleSlot, false);
    }

    private void HideFenceSupportHandles()
    {
        _fenceSupportHandleSource = null;
        _fenceSupportHandles = null;
        foreach (var marker in _fenceSupportHandleMarkers)
            if (marker is not null && GodotObject.IsInstanceValid(marker)) marker.Visible = false;
        foreach (var target in _fenceSupportHandleTargets)
            if (target is not null && GodotObject.IsInstanceValid(target)) target.Visible = false;
    }

    private void DisposeFenceSupportHandles()
    {
        CancelFenceHandleDrag();
        for (var slot = 0; slot < _fenceSupportHandleMarkers.Length; slot++)
        {
            if (_fenceSupportHandleMarkers[slot] is { } marker && GodotObject.IsInstanceValid(marker)) marker.QueueFree();
            if (_fenceSupportHandleTargets[slot] is { } target && GodotObject.IsInstanceValid(target)) target.QueueFree();
            _fenceSupportHandleMarkers[slot] = null;
            _fenceSupportHandleTargets[slot] = null;
        }
    }

    private static bool IsFiniteScreen(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private void UpdateFenceRangeMarker(FencePick pick)
    {
        if (pick.BayCount < 3)
        {
            if (_fenceRangeMarker is not null && GodotObject.IsInstanceValid(_fenceRangeMarker))
                _fenceRangeMarker.Visible = false;
            HideFenceSupportHandles();
            RequestViewportRender();
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
        UpdateFenceSupportHandleVisuals();
        RequestViewportRender();
    }
}
