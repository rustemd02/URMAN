using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class Main
{
    private int _photoScenePreloadGeneration;
    private string? _preparedPhotoScenePath;
    private PackedScene? _preparedPhotoScene;
    private readonly Dictionary<string, int> _photoSceneRequests = new(StringComparer.Ordinal);

    private static string? PhotoScenePath(string sceneId) => sceneId switch
    {
        PrologueSceneId or ForestSceneId => W01ScenePath,
        RiverSceneId => W02ScenePath,
        HousePhotoSceneId => W03ScenePath,
        CommonPhotoSceneId => W04ScenePath,
        CellarPhotoSceneId => W05ScenePath,
        _ => null
    };

    internal static bool IsPhotoWorldScene(string sceneId) => PhotoScenePath(sceneId) is not null;

    internal bool MountPreparedPhotoScene(object session, string sceneId, bool preserveLoadedTransform = false)
    {
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || !EnableFullGamePhotoWorldRouting || _routingFullGameScene || PhotoScenePath(sceneId) is null)
            return false;
        AttachFullGameRouteBridge();
        if (_fullGameRouteBridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(session, bridge.ProjectionSessionIdentity) || bridge.ActiveSceneId != sceneId)
            return false;

        if (preserveLoadedTransform)
        {
            _preserveLoadedPhotoWorldTransform = true;
            _loadedPhotoWorldTransformSession = session;
        }
        _routingFullGameScene = true;
        try
        {
            TryMountPhotoScene(new RuntimeBridge.FullGamePhotoWorldRouteProjection(session, sceneId), bridge);
            return IsPhotoSceneMounted(sceneId);
        }
        finally { _routingFullGameScene = false; }
    }

    internal async Task<bool> CompletePhotoSceneProjectionAsync(object session, string sceneId, bool allowCrouched = false)
    {
        var bridge = _fullGameRouteBridge;
        bool Current() => GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
            && bridge is not null && GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(session, bridge.ProjectionSessionIdentity) && bridge.ActiveSceneId == sceneId
            && IsPhotoSceneMounted(sceneId);
        if (!Current()) return false;
        var tree = GetTree();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        if (!Current()) return false;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        return Current() && tree.GetFirstNodeInGroup("player_controller") is FirstPersonController player
            && GodotObject.IsInstanceValid(player) && player.IsInsideTree() && !player.IsQueuedForDeletion()
            && PhotoVisitPoseHasSupport(player, player.GlobalPosition, allowCrouched);
    }

    private bool TryMountPhotoScene(RuntimeBridge.FullGamePhotoWorldRouteProjection projection, RuntimeBridge bridge)
    {
        switch (projection.ActiveSceneId)
        {
            case PrologueSceneId:
            case ForestSceneId: MountW01(projection, bridge); return true;
            case RiverSceneId: MountW02(projection, bridge); return true;
            case HousePhotoSceneId: MountW03(projection, bridge); return true;
            case CommonPhotoSceneId: MountW04(projection, bridge); return true;
            case CellarPhotoSceneId: MountW05(projection, bridge); return true;
            default: return false;
        }
    }

    private bool IsPhotoSceneMounted(string sceneId) => ActiveZoneScenePath == PhotoScenePath(sceneId)
        && (sceneId switch
        {
            PrologueSceneId or ForestSceneId => IsMountedW01(),
            RiverSceneId => IsMountedW02(),
            HousePhotoSceneId => IsMountedW03(),
            CommonPhotoSceneId => IsMountedW04(),
            CellarPhotoSceneId => IsMountedW05(),
            _ => false
        });

    internal async Task<bool> PreloadPhotoSceneAsync(
        string sceneId, CancellationToken cancellationToken = default, Action<float>? progress = null)
    {
        var path = PhotoScenePath(sceneId);
        if (path is null) return true;

        var generation = ++_photoScenePreloadGeneration;
        bool Current() => GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
            && generation == _photoScenePreloadGeneration && !cancellationToken.IsCancellationRequested;
        if (!Current()) return false;
        var tree = GetTree();
        try
        {
            if (_preparedPhotoScenePath == path && _preparedPhotoScene is { } prepared
                && GodotObject.IsInstanceValid(prepared))
            {
                progress?.Invoke(1f);
                return Current();
            }

            // Keep only the next scene alive through this owner. ResourceLoader
            // owns its thread request, which cannot be cancelled by this task.
            _preparedPhotoScene = null;
            _preparedPhotoScenePath = null;
            _photoSceneRequests[path] = generation;
            var status = ResourceLoader.LoadThreadedGetStatus(path);
            if (status == ResourceLoader.ThreadLoadStatus.Failed)
            {
                // A failed engine token must be consumed before requesting a
                // retry; otherwise Godot retains the failed request.
                ResourceLoader.LoadThreadedGet(path);
                status = ResourceLoader.ThreadLoadStatus.InvalidResource;
            }
            if (status is not (ResourceLoader.ThreadLoadStatus.InProgress or ResourceLoader.ThreadLoadStatus.Loaded))
            {
                var error = ResourceLoader.LoadThreadedRequest(path, "PackedScene", useSubThreads: false);
                if (error != Error.Ok)
                {
                    GD.PushWarning($"Photo scene preload request failed: {path} ({error}).");
                    return false;
                }
            }

            using var loadProgress = new global::Godot.Collections.Array();
            while (Current())
            {
                status = ResourceLoader.LoadThreadedGetStatus(path, loadProgress);
                if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                {
                    // Get would block before Loaded; scene instantiation and
                    // all SceneTree changes remain in the ordinary mount.
                    var packed = ResourceLoader.LoadThreadedGet(path) as PackedScene;
                    ForgetPhotoSceneRequest(path, generation);
                    if (!Current()) return false;
                    if (packed is null || !GodotObject.IsInstanceValid(packed))
                    {
                        GD.PushWarning($"Photo scene preload did not return a PackedScene: {path}.");
                        return false;
                    }
                    _preparedPhotoScene = packed;
                    _preparedPhotoScenePath = path;
                    progress?.Invoke(1f);
                    return Current();
                }
                if (status != ResourceLoader.ThreadLoadStatus.InProgress)
                {
                    if (status == ResourceLoader.ThreadLoadStatus.Failed)
                        ResourceLoader.LoadThreadedGet(path);
                    ForgetPhotoSceneRequest(path, generation);
                    GD.PushWarning($"Photo scene preload failed: {path} ({status}).");
                    return false;
                }
                progress?.Invoke(loadProgress.Count > 0 ? Mathf.Clamp(loadProgress[0].AsSingle(), 0f, 1f) : 0f);
                if (!Current()) return false;
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            return false;
        }
        catch (Exception error)
        {
            if (Current()) GD.PushWarning($"Photo scene preload failed: {path} ({error.GetType().Name}: {error.Message}).");
            return false;
        }
        finally
        {
            // Cancelling an await does not cancel Godot's request. Release its
            // eventual result unless a newer caller has adopted this path.
            if (OwnsPhotoSceneRequest(path, generation))
                _ = ReleaseAbandonedPhotoRequestAsync(path, generation, tree);
        }
    }

    private bool OwnsPhotoSceneRequest(string path, int generation) =>
        _photoSceneRequests.TryGetValue(path, out var owner) && owner == generation;

    private void ForgetPhotoSceneRequest(string path, int generation)
    {
        if (OwnsPhotoSceneRequest(path, generation)) _photoSceneRequests.Remove(path);
    }

    private async Task ReleaseAbandonedPhotoRequestAsync(string path, int generation, SceneTree tree)
    {
        try
        {
            // Main may already have exited. The captured SceneTree owns the
            // frame signal; ownership is checked again before every engine get.
            while (GodotObject.IsInstanceValid(tree) && OwnsPhotoSceneRequest(path, generation))
            {
                var status = ResourceLoader.LoadThreadedGetStatus(path);
                if (status != ResourceLoader.ThreadLoadStatus.InProgress)
                {
                    if (status is ResourceLoader.ThreadLoadStatus.Loaded or ResourceLoader.ThreadLoadStatus.Failed)
                        ResourceLoader.LoadThreadedGet(path);
                    ForgetPhotoSceneRequest(path, generation);
                    return;
                }
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
        }
        catch (Exception error)
        {
            ForgetPhotoSceneRequest(path, generation);
            GD.PushWarning($"Photo scene request release failed: {path} ({error.GetType().Name}: {error.Message}).");
        }
    }

    private PackedScene? TakePreparedPhotoScene(string path)
    {
        if (_preparedPhotoScenePath != path) return null;
        var packed = _preparedPhotoScene;
        _preparedPhotoScene = null;
        _preparedPhotoScenePath = null;
        return packed is not null && GodotObject.IsInstanceValid(packed) ? packed : null;
    }

    private void InvalidatePhotoScenePreload()
    {
        _photoScenePreloadGeneration++;
        _preparedPhotoScene = null;
        _preparedPhotoScenePath = null;
    }
}
