using Godot;
using PlayTimeBlock = Urman.Godot.RuntimeBridge.PlayTimeBlock;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private bool _orderlyShutdownRequested;
    private int _orderlyShutdownExitCode;
    private bool _previousAutoAcceptQuit;
    private bool _hasPreviousAutoAcceptQuit;
    private SceneTree? _orderlyShutdownTree;
    private Window? _orderlyShutdownWindow;
    private int _shutdownSavesInFlight;
    private TaskCompletionSource<bool>? _shutdownSavesDrained;

    private void InstallOrderlyShutdownHooks()
    {
        if (StudioPreviewMode || DisplayServer.GetName() == "headless") return;

        _orderlyShutdownTree = GetTree();
        _previousAutoAcceptQuit = _orderlyShutdownTree.AutoAcceptQuit;
        _hasPreviousAutoAcceptQuit = true;
        _orderlyShutdownTree.AutoAcceptQuit = false;
        _orderlyShutdownWindow = GetWindow();
        _orderlyShutdownWindow.CloseRequested += OnWindowCloseRequested;
    }

    private void OnWindowCloseRequested() => RequestOrderlyShutdown();

    private void RequestOrderlyShutdown()
    {
        if (_orderlyShutdownRequested) return;
        _orderlyShutdownRequested = true;

        if (IsInsideTree())
        {
            GetViewport().GuiDisableInput = true;
            _player?.SetModalOpen(true);
            _pauseMenu?.BeginShutdown();
            if (_bridge is null
                && GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge bridge)
            {
                _bridge = bridge;
                _bridge.SaveFeedback += OnSaveFeedback;
            }
        }

        _ = RunOrderlyShutdownAsync();
    }

    private async Task RunOrderlyShutdownAsync()
    {
        SceneTree? tree = null;
        try
        {
            if (!IsInsideTree()) return;
            tree = GetTree();
            await DrainKnownSessionWorkAsync(tree);

            if (_main is { } main && GodotObject.IsInstanceValid(main) && main.IsInsideTree())
            {
                main.QueueFree();
                await ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            else
            {
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }

            AnimationCatalog.ReleaseForFinalShutdown();
            Act1ConnectedWorld.ReleaseRoundLogCacheAfterSceneDisposal();
            VillageWindowMaterials.ReleaseForFinalShutdown();
            WinterParticleSurfaces.ReleaseForFinalShutdown();
            PainterlyMaterialLibrary.ReleaseForFinalShutdown();

            if (DisplayServer.GetName() != "headless")
                await WaitForRenderFlushAsync();

            tree.Quit(_orderlyShutdownExitCode);
        }
        catch (Exception error)
        {
            GD.PushError($"Orderly shutdown failed: {error}");
            if (tree is not null && GodotObject.IsInstanceValid(tree))
                tree.Quit(1);
        }
    }

    private async Task DrainKnownSessionWorkAsync(SceneTree tree)
    {
        while (true)
        {
            if (_main is { } main && GodotObject.IsInstanceValid(main)
                && _startupTask is { IsCompleted: false } startup)
            {
                await startup;
                continue;
            }

            if (_pauseMenu is { } pause && GodotObject.IsInstanceValid(pause))
                await pause.WaitForActiveActionsAsync();

            if (_menuBusy || _menuLookupMenu is not null)
            {
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                continue;
            }

            if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge) && bridge.IsInsideTree()
                && (bridge.CapturePlayTimeBlocks() & PlayTimeBlock.Loading) != 0)
            {
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                continue;
            }

            await WaitForShutdownSavesAsync();
            // One idle process frame lets RuntimeBridge start any already-queued
            // checkpoint write before the stable-state decision below.
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            if (HasKnownSessionWorkPending())
                continue;

            return;
        }
    }

    private bool HasKnownSessionWorkPending()
    {
        if (_menuBusy || _menuLookupMenu is not null || _shutdownSavesInFlight != 0
            || _player is { } player && GodotObject.IsInstanceValid(player) && player.SessionTransitionActive
                && !(_bridge is { } recoveryBridge && GodotObject.IsInstanceValid(recoveryBridge)
                    && recoveryBridge.NeedsPhysicalRecovery))
            return true;

        if (_pauseMenu is { } pause && GodotObject.IsInstanceValid(pause)
            && !pause.WaitForActiveActionsAsync().IsCompleted)
            return true;

        return _bridge is { } bridge && GodotObject.IsInstanceValid(bridge) && bridge.IsInsideTree()
            && (bridge.CapturePlayTimeBlocks() & PlayTimeBlock.Loading) != 0;
    }

    private async Task<bool> WaitForRenderFlushAsync()
    {
        var server = RenderingServer.Singleton;
        var drawn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = Callable.From(() => drawn.TrySetResult(true));
        server.Connect(RenderingServer.SignalName.FramePostDraw, callback);
        try
        {
            if (await Task.WhenAny(drawn.Task, Task.Delay(TimeSpan.FromSeconds(3))) != drawn.Task)
            {
                GD.PushWarning("Orderly shutdown did not receive FramePostDraw within 3 seconds; exiting after cache release.");
                return false;
            }

            await drawn.Task;
            return true;
        }
        finally
        {
            if (server.IsConnected(RenderingServer.SignalName.FramePostDraw, callback))
                server.Disconnect(RenderingServer.SignalName.FramePostDraw, callback);
        }
    }

    private void OnSaveFeedback(bool? saved)
    {
        if (saved is null)
        {
            if (_shutdownSavesInFlight++ == 0)
                _shutdownSavesDrained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return;
        }

        if (_shutdownSavesInFlight <= 0) return;
        if (--_shutdownSavesInFlight == 0)
        {
            _shutdownSavesDrained?.TrySetResult(true);
            _shutdownSavesDrained = null;
        }
    }

    private async Task WaitForShutdownSavesAsync()
    {
        while (_shutdownSavesInFlight > 0)
        {
            var drained = _shutdownSavesDrained?.Task;
            if (drained is null) return;
            await drained;
        }
    }

    private void RestoreOrderlyShutdownHooks()
    {
        if (_orderlyShutdownWindow is { } window && GodotObject.IsInstanceValid(window))
            window.CloseRequested -= OnWindowCloseRequested;
        _orderlyShutdownWindow = null;

        if (_hasPreviousAutoAcceptQuit && _orderlyShutdownTree is { } tree
            && GodotObject.IsInstanceValid(tree))
            tree.AutoAcceptQuit = _previousAutoAcceptQuit;
        _hasPreviousAutoAcceptQuit = false;
        _orderlyShutdownTree = null;
    }
}
