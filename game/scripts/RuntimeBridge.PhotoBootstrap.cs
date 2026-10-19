using Godot;
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
            if (!Current() || !await ProjectReservedPhotoWorldAsync(main, session, sceneId)) return;
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

    private async Task<bool> ProjectReservedPhotoWorldAsync(
        Main main, RuntimeKernel session, string sceneId, bool preserveLoadedTransform = false)
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
            if (!Current() || !main.MountPreparedPhotoScene(session, sceneId, preserveLoadedTransform)) return false;
            // Retire old physical owners and let the new scene attach its own
            // carry owner before projecting saved props. Do not validate against
            // authored props that the saved state is about to move.
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!Current()) return false;
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!Current()) return false;
            await ProjectPhysicalWorldAsync();
            if (!Current()) return false;
            var carry = tree.GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
            if (sceneId == "urman.fullgame:scene/pw-cellar"
                && (carry is null || !GodotObject.IsInstanceValid(carry) || carry.IsQueuedForDeletion()
                    || main.GetNodeOrNull<W05Controller>("ZoneHost/W05") is not { } cellar
                    || !cellar.IsAncestorOf(carry)))
                throw new InvalidDataException("The restored cellar's canonical carry owner is not ready.");
            if (carry is not null && GodotObject.IsInstanceValid(carry) && !carry.IsQueuedForDeletion())
                carry.ProjectLoadedState();
            if (!await main.CompletePhotoSceneProjectionAsync(session, sceneId, preserveLoadedTransform) || !Current()) return false;
            if (carry is not null && GodotObject.IsInstanceValid(carry)
                && carry.HeldItem is not null && !carry.HasValidHeldPose)
                throw new InvalidDataException("The loaded carried item has no free physical pose.");
            return true;
        }
        finally
        {
            if (ReferenceEquals(_photoWorldPhysicalProjectionSession, session))
                _photoWorldPhysicalProjectionSession = previousProjection;
        }
    }
}
