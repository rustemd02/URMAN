using Godot;
using Urman.Core.Narrative;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    private TaskCompletionSource<bool>? _photoWorldBootstrapCompletion;
    private bool _photoWorldBootstrapStarted;

    internal Task<bool> PhotoWorldBootstrapCompletion => _photoWorldBootstrapCompletion?.Task ?? Task.FromResult(true);

    // Main calls this after its children (including the player) are ready.
    // The bridge reserves loading in its own Ready, before Main can mount.
    internal void BeginPhotoWorldBootstrap(Main main)
    {
        if (_photoWorldBootstrapStarted || _photoWorldBootstrapCompletion is null || _kernel is not { } session) return;
        _photoWorldBootstrapStarted = true;
        _ = BootstrapPhotoWorldAsync(main, session);
    }

    private async Task BootstrapPhotoWorldAsync(Main main, RuntimeKernel session)
    {
        var player = FindPlayer();
        LoadingScreenUi? loading = null;
        var succeeded = false;
        bool Current() => GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
            && ReferenceEquals(session, _kernel) && GodotObject.IsInstanceValid(main) && main.IsInsideTree()
            && !main.IsQueuedForDeletion() && player is not null && GodotObject.IsInstanceValid(player);
        try
        {
            if (!Current()) return;
            if (GetParent()?.GetParent() is not Act1DemoRoot)
                loading = LoadingScreenUi.Show(this, "УРМАН", "Подготавливаем место начала истории");
            if (!await InitializeEntrypointAsync()) return;
            Action<float>? reportProgress = loading is null ? null : loading.SetResourceProgress;
            if (!Current() || ActiveSceneId is not { } sceneId
                || !await main.PreloadPhotoSceneAsync(sceneId, progress: reportProgress)) return;
            if (!Current() || !(await ProjectReservedPhotoWorldAsync(main, session, sceneId)).Ready) return;
            succeeded = true;
            GD.Print($"urman-photoworld-bootstrap-ready: scene={sceneId} supported=true");
        }
        catch (Exception error)
        {
            if (Current()) GD.PushError($"PhotoWorld bootstrap failed: {error.Message}");
        }
        finally
        {
            if (loading is not null && GodotObject.IsInstanceValid(loading)) loading.Hide();
            if (Current())
            {
                NeedsPhysicalRecovery = !succeeded;
                SampleLoadingTime();
                _loadPreparing = false;
                // The host builds its menu after awaiting this completion.
                // Keep that small handoff modal, even with a held movement key.
                if (GetParent()?.GetParent() is Act1DemoRoot) player!.SetModalOpen(true);
                player!.SetSessionTransition(NeedsPhysicalRecovery);
                QueueRuntimeStateChanged();
                if (!succeeded) ShowPhysicalRecoveryMenu();
            }
            _photoWorldBootstrapCompletion?.TrySetResult(succeeded);
        }
    }

    private async Task<(bool Ready, string? VisitStartNode)> ProjectReservedPhotoWorldAsync(
        Main main, RuntimeKernel session, string sceneId, bool preserveLoadedTransform = false,
        bool prepareVisitStart = false)
    {
        var previousProjection = _photoWorldPhysicalProjectionSession;
        _photoWorldPhysicalProjectionSession = session;
        var tree = GetTree();
        bool Current() => GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
            && ReferenceEquals(session, _kernel) && ReferenceEquals(_photoWorldPhysicalProjectionSession, session)
            && GodotObject.IsInstanceValid(main) && main.IsInsideTree() && !main.IsQueuedForDeletion()
            && ActiveSceneId == sceneId;
        try
        {
            if (!Current() || !main.MountPreparedPhotoScene(session, sceneId, preserveLoadedTransform)) return (false, null);
            // A reused world may have hidden its bodies while the new kernel
            // was being prepared. Reproject it inside this reserved scope before
            // validating support; mounting alone only updates its entry pose.
            QueueRuntimeStateChanged();
            // Retire old physical owners and let the new scene attach its own
            // carry owner before projecting saved props. Do not validate against
            // authored props that the saved state is about to move.
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!Current()) return (false, null);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!Current()) return (false, null);
            await ProjectPhysicalWorldAsync();
            if (!Current()) return (false, null);
            var carry = tree.GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
            if (sceneId == "urman.fullgame:scene/pw-cellar"
                && (carry is null || !GodotObject.IsInstanceValid(carry) || carry.IsQueuedForDeletion()
                    || main.GetNodeOrNull<W05Controller>("ZoneHost/W05") is not { } cellar
                    || !cellar.IsAncestorOf(carry)))
                throw new InvalidDataException("The restored cellar's canonical carry owner is not ready.");
            if (prepareVisitStart && carry is not null && GodotObject.IsInstanceValid(carry))
            {
                var registrationDeadline = Time.GetTicksMsec() + 5000;
                while (Current() && GodotObject.IsInstanceValid(carry) && !carry.IsQueuedForDeletion()
                    && carry.ActionInProgress && Time.GetTicksMsec() < registrationDeadline)
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                if (!Current() || !GodotObject.IsInstanceValid(carry) || carry.IsQueuedForDeletion()
                    || !carry.IsRegisteredForSession(session))
                    throw new InvalidDataException("The photo world's canonical item owner is not ready.");
            }
            if (carry is not null && GodotObject.IsInstanceValid(carry) && !carry.IsQueuedForDeletion())
                carry.ProjectLoadedState();
            string? visitStartNode = null;
            string? visitStartWorld = null;
            string? entryNode = null;
            if (prepareVisitStart)
            {
                if (ActivePhotoVisit() is not { } visit || visit.GetProperty("status").GetString() != "prepared")
                    return (false, null);
                visitStartWorld = visit.GetProperty("worldId").GetString()!;
                if (!PhotoWorldState.TryGetVisitRoute(visitStartWorld, out var expectedScene, out entryNode)
                    || expectedScene != sceneId
                    || !PhotoWorldState.TryGetVisitStartNode(session.SelectState(), visitStartWorld, out visitStartNode))
                    return (false, null);
                // The previous visit's placed props are already projected.
                // Select the actual start before requiring its floor support;
                // a clear authored entrance is only an explicit fallback.
                if (!main.TryPlacePlayerAtPhotoVisitStart(session, visitStartWorld, visitStartNode))
                {
                    if (visitStartNode == entryNode
                        || !main.TryPlacePlayerAtPhotoVisitStart(session, visitStartWorld, entryNode))
                        return (false, null);
                    visitStartNode = entryNode;
                }
                if (carry is not null && GodotObject.IsInstanceValid(carry) && !carry.IsQueuedForDeletion())
                    carry.ProjectLoadedState();
            }
            if (!await main.CompletePhotoSceneProjectionAsync(session, sceneId, preserveLoadedTransform))
            {
                if (prepareVisitStart)
                {
                    if (!Current() || visitStartNode == entryNode
                        || !main.TryPlacePlayerAtPhotoVisitStart(session, visitStartWorld!, entryNode!))
                        return (false, null);
                    visitStartNode = entryNode;
                    if (carry is not null && GodotObject.IsInstanceValid(carry) && !carry.IsQueuedForDeletion())
                        carry.ProjectLoadedState();
                    if (!await main.CompletePhotoSceneProjectionAsync(session, sceneId) || !Current())
                        return (false, null);
                }
                else
                {
                    if (!Current() || !preserveLoadedTransform
                        || !TryGetLoadedPhotoSafeNode(session, sceneId, out var worldId, out var safeNodeId)
                        || !main.TryPlacePlayerAtPhotoSafeNode(session, worldId, safeNodeId))
                        return (false, null);
                    // Restore the same saved carry owner at the verified fallback.
                    // A blocked item alone never triggers this relocation; failure
                    // here retains the ordinary load rollback and untouched slot.
                    if (carry is not null && GodotObject.IsInstanceValid(carry) && !carry.IsQueuedForDeletion())
                        carry.ProjectLoadedState();
                    if (!await main.CompletePhotoSceneProjectionAsync(session, sceneId, allowCrouched: true)
                        || !Current()) return (false, null);
                    GD.Print($"urman-photo-loaded-safe-node: world={worldId} node={safeNodeId}");
                }
            }
            if (!Current()) return (false, null);
            if (carry is not null && GodotObject.IsInstanceValid(carry)
                && carry.HeldItem is not null && !carry.HasValidHeldPose)
                throw new InvalidDataException("The loaded carried item has no free physical pose.");
            return (true, visitStartNode);
        }
        finally
        {
            if (ReferenceEquals(_photoWorldPhysicalProjectionSession, session))
                _photoWorldPhysicalProjectionSession = previousProjection;
        }
    }

    private static bool TryGetLoadedPhotoSafeNode(RuntimeKernel session, string sceneId,
        out string worldId, out string safeNodeId)
    {
        worldId = safeNodeId = string.Empty;
        foreach (var candidateWorld in new[] { "W01", "W02", "W03", "W04", "W05" })
        {
            if (!PhotoWorldState.TryGetVisitRoute(candidateWorld, out var candidateScene, out var entryNode)
                || candidateScene != sceneId) continue;
            var visit = session.SelectState().GetProperty("photoworlds").GetProperty("visits").GetProperty("active");
            if (visit.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (visit.GetProperty("worldId").GetString() != candidateWorld
                    || visit.GetProperty("status").GetString() is not ("active" or "returning")
                    || visit.GetProperty("safeNodeId").GetString() is not { } node
                    || !PhotoWorldState.IsVisitSafeNode(candidateWorld, node)) return false;
                worldId = candidateWorld;
                safeNodeId = node;
                return true;
            }
            // Schema-1 and legacy photo saves have no visit ticket. Their
            // explicit authored entry is the only supported fallback.
            worldId = candidateWorld;
            safeNodeId = entryNode;
            return true;
        }
        return false;
    }
}
