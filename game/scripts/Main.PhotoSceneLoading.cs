using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Urman.Core.Narrative;

namespace Urman.Godot;

public partial class Main
{
    private static int _photoSceneRequestSequence;
    private int _photoScenePreloadGeneration;
    private string? _preparedPhotoScenePath;
    private PackedScene? _preparedPhotoScene;
    private int _preparedPhotoGeneration;
    private readonly List<Resource> _preparedPhotoResources = new();
    private static readonly Dictionary<string, int> _photoSceneRequests = new(StringComparer.Ordinal);

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

    internal bool TryPlacePlayerAtPhotoSafeNode(object session, string worldId, string safeNodeId)
    {
        if (!TryGetMountedPhotoSafeNode(session, worldId, safeNodeId, out var player, out var anchor)
            || !player.SessionTransitionActive || player.VehicleControlled || player.IsClimbingLadder)
            return false;

        // A blocked held item alone must reject the load, not relocate a valid
        // player pose. The caller projects the same item again after moving.
        if (PhotoVisitPoseHasSupport(player, player.GlobalPosition, allowCrouched: true)
            || !PhotoVisitPoseHasSupport(player, anchor.GlobalPosition, allowCrouched: true))
            return false;

        player.ApplyZoneSpawn(anchor.GlobalPosition, anchor.GlobalRotationDegrees.Y);
        LastSpawnYawDegrees = anchor.GlobalRotationDegrees.Y;
        return true;
    }

    internal bool TryPlacePlayerAtPhotoVisitStart(object session, string worldId, string safeNodeId)
    {
        if (!TryGetMountedPhotoSafeNode(session, worldId, safeNodeId, out var player, out var anchor)
            || !player.SessionTransitionActive || player.VehicleControlled || player.IsClimbingLadder
            || _fullGameRouteBridge is not { } bridge
            || !PhotoWorldState.TryGetVisitRoute(worldId, out _, out var entryNodeId))
            return false;
        var state = bridge.SelectRuntimeState();
        var activeVisit = state.GetProperty("photoworlds").GetProperty("visits").GetProperty("active");
        if (activeVisit.ValueKind != System.Text.Json.JsonValueKind.Object
            || activeVisit.GetProperty("status").GetString() != "prepared"
            || activeVisit.GetProperty("worldId").GetString() != worldId
            || !PhotoWorldState.TryGetVisitStartNode(state, worldId, out var selectedNodeId)
            || (safeNodeId != selectedNodeId && safeNodeId != entryNodeId)
            || !PhotoVisitPoseHasSupport(player, anchor.GlobalPosition))
            return false;

        // Entry placement follows the canonical saved node after actual props
        // have been restored. It does not depend on the old entry pose fitting.
        player.ApplyZoneSpawn(anchor.GlobalPosition, anchor.GlobalRotationDegrees.Y);
        LastSpawnYawDegrees = anchor.GlobalRotationDegrees.Y;
        return true;
    }

    internal bool CanObservePhotoSafeNode(object session, string worldId, string safeNodeId)
    {
        if (!TryGetMountedPhotoSafeNode(session, worldId, safeNodeId, out var player, out var anchor)
            || _fullGameRouteBridge is not { IsPhotoBookProjectionDeferred: false } bridge
            || !ReferenceEquals(session, bridge.SessionIdentity)
            || player.ModalOpen || player.SessionTransitionActive || player.VehicleControlled || player.IsClimbingLadder
            || player.IsCrouching || !player.IsOnFloor() || player.Velocity.LengthSquared() > .35f * .35f)
            return false;
        var offset = player.GlobalPosition - anchor.GlobalPosition;
        const float reachRadius = 1.2f;
        using var pathCollision = new KinematicCollision3D();
        return offset.X * offset.X + offset.Z * offset.Z <= reachRadius * reachRadius && Mathf.Abs(offset.Y) <= .15f
            && PhotoVisitPoseHasSupport(player, player.GlobalPosition)
            && PhotoVisitPoseHasSupport(player, anchor.GlobalPosition)
            // Nearby on the other side of a wall is not a reached checkpoint.
            && !player.TestMove(player.GlobalTransform, -offset, pathCollision, .001f);
    }

    private bool TryGetMountedPhotoSafeNode(object session, string worldId, string safeNodeId,
        out FirstPersonController player, out Marker3D anchor)
    {
        player = null!;
        anchor = null!;
        if (!PhotoWorldState.IsVisitSafeNode(worldId, safeNodeId)
            || !PhotoWorldState.TryGetVisitRoute(worldId, out var sceneId, out _)
            || !GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || _fullGameRouteBridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !bridge.IsInsideTree() || bridge.IsQueuedForDeletion()
            || !ReferenceEquals(session, bridge.ProjectionSessionIdentity) || bridge.ActiveSceneId != sceneId
            || !IsPhotoSceneMounted(sceneId)
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController currentPlayer
            || !GodotObject.IsInstanceValid(currentPlayer) || !currentPlayer.IsInsideTree() || currentPlayer.IsQueuedForDeletion())
            return false;
        var world = worldId switch
        {
            "W01" => _mountedW01,
            "W02" => _mountedW02,
            "W03" => _mountedW03,
            "W04" => _mountedW04,
            "W05" => _mountedW05,
            _ => null
        };
        if (world?.GetNodeOrNull<Marker3D>($"Anchors/{safeNodeId}") is not { } currentAnchor
            || !GodotObject.IsInstanceValid(currentAnchor) || !currentAnchor.IsInsideTree() || currentAnchor.IsQueuedForDeletion())
            return false;
        player = currentPlayer;
        anchor = currentAnchor;
        return true;
    }

    private bool TryMountPhotoScene(RuntimeBridge.FullGamePhotoWorldRouteProjection projection, RuntimeBridge bridge)
    {
        var path = PhotoScenePath(projection.ActiveSceneId);
        var preparedGeneration = _preparedPhotoGeneration;
        try
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
        finally
        {
            // AddChild runs the controller's synchronous _Ready builders. Hold
            // their resources until those builders have taken their own refs.
            if (path is not null && _preparedPhotoScenePath == path
                && _preparedPhotoGeneration == preparedGeneration)
                ClearPreparedPhotoScene();
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
        var generation = Interlocked.Increment(ref _photoSceneRequestSequence);
        _photoScenePreloadGeneration = generation;
        if (path is null)
        {
            ClearPreparedPhotoScene();
            return true;
        }
        bool Current() => GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
            && generation == _photoScenePreloadGeneration && !cancellationToken.IsCancellationRequested;
        if (!Current())
        {
            ClearPreparedPhotoScene();
            return false;
        }
        var tree = GetTree();
        var succeeded = false;
        var resources = new List<Resource>();
        try
        {
            if (_preparedPhotoScenePath == path && _preparedPhotoScene is { } prepared
                && GodotObject.IsInstanceValid(prepared)
                && _preparedPhotoResources.All(resource => GodotObject.IsInstanceValid(resource)))
            {
                _preparedPhotoGeneration = generation;
                progress?.Invoke(1f);
                succeeded = Current();
                return succeeded;
            }

            ClearPreparedPhotoScene();
            var paths = PhotoSceneResourcePaths(sceneId, path).Distinct(StringComparer.Ordinal).ToArray();
            resources.Capacity = paths.Length;
            for (var index = 0; index < paths.Length; index++)
            {
                var completed = index;
                var resource = await PreloadPhotoResourceAsync(paths[index], index == 0 ? "PackedScene" : string.Empty,
                    generation, tree, Current, ratio => progress?.Invoke((completed + ratio) / paths.Length));
                if (resource is null || !Current()) return false;
                resources.Add(resource);
            }
            if (resources[0] is not PackedScene packed)
            {
                GD.PushWarning($"Photo scene preload did not return a PackedScene: {path}.");
                return false;
            }
            _preparedPhotoResources.AddRange(resources);
            _preparedPhotoScene = packed;
            _preparedPhotoScenePath = path;
            _preparedPhotoGeneration = generation;
            progress?.Invoke(1f);
            succeeded = Current();
            return succeeded;
        }
        catch (Exception error)
        {
            if (Current()) GD.PushWarning($"Photo scene preload failed: {path} ({error.GetType().Name}: {error.Message}).");
            return false;
        }
        finally
        {
            resources.Clear();
            if (!succeeded && _preparedPhotoGeneration == generation) ClearPreparedPhotoScene();
        }
    }

    private static IEnumerable<string> PhotoSceneResourcePaths(string sceneId, string scenePath)
    {
        yield return scenePath;
        var dynamicPaths = sceneId switch
        {
            PrologueSceneId or ForestSceneId => W01Controller.DynamicResourcePaths(),
            RiverSceneId => PainterlyMaterialLibrary.DynamicResourcePaths(new[]
                { "grass", "wood_cut", "grass_tuft", "wood_carved", "leaf_birch", "cloth", "paper" }),
            HousePhotoSceneId => W03Controller.DynamicResourcePaths(),
            CommonPhotoSceneId => W04Controller.DynamicResourcePaths(),
            CellarPhotoSceneId => W05Controller.DynamicResourcePaths(),
            _ => Array.Empty<string>()
        };
        foreach (var path in dynamicPaths) yield return path;
    }

    private async Task<Resource?> PreloadPhotoResourceAsync(string path, string typeHint, int generation,
        SceneTree tree, Func<bool> current, Action<float> progress)
    {
        if (!current()) return null;
        try
        {
            _photoSceneRequests[path] = generation;
            var status = ResourceLoader.LoadThreadedGetStatus(path);
            if (status == ResourceLoader.ThreadLoadStatus.Failed)
            {
                // A failed engine token must be consumed before requesting a
                // retry; otherwise Godot retains the failed request.
                if (!OwnsPhotoSceneRequest(path, generation)) return null;
                ResourceLoader.LoadThreadedGet(path);
                status = ResourceLoader.ThreadLoadStatus.InvalidResource;
            }
            if (status is not (ResourceLoader.ThreadLoadStatus.InProgress or ResourceLoader.ThreadLoadStatus.Loaded))
            {
                var error = ResourceLoader.LoadThreadedRequest(path, typeHint, useSubThreads: false);
                if (error != Error.Ok)
                {
                    GD.PushWarning($"Photo resource preload request failed: {path} ({error}).");
                    return null;
                }
            }

            using var loadProgress = new global::Godot.Collections.Array();
            while (current())
            {
                if (!OwnsPhotoSceneRequest(path, generation)) return null;
                status = ResourceLoader.LoadThreadedGetStatus(path, loadProgress);
                if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                {
                    // Get would block before Loaded; scene instantiation and
                    // all SceneTree changes remain in the ordinary mount.
                    if (!OwnsPhotoSceneRequest(path, generation)) return null;
                    var resource = ResourceLoader.LoadThreadedGet(path);
                    ForgetPhotoSceneRequest(path, generation);
                    if (!current()) return null;
                    if (resource is null || !GodotObject.IsInstanceValid(resource))
                    {
                        GD.PushWarning($"Photo resource preload returned no resource: {path}.");
                        return null;
                    }
                    progress(1f);
                    return current() ? resource : null;
                }
                if (status != ResourceLoader.ThreadLoadStatus.InProgress)
                {
                    if (status == ResourceLoader.ThreadLoadStatus.Failed && OwnsPhotoSceneRequest(path, generation))
                        ResourceLoader.LoadThreadedGet(path);
                    ForgetPhotoSceneRequest(path, generation);
                    GD.PushWarning($"Photo resource preload failed: {path} ({status}).");
                    return null;
                }
                progress(loadProgress.Count > 0 ? Mathf.Clamp(loadProgress[0].AsSingle(), 0f, 1f) : 0f);
                if (!current()) return null;
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            return null;
        }
        catch (Exception error)
        {
            if (current()) GD.PushWarning($"Photo resource preload failed: {path} ({error.GetType().Name}: {error.Message}).");
            return null;
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
                    if (OwnsPhotoSceneRequest(path, generation)
                        && (status is ResourceLoader.ThreadLoadStatus.Loaded or ResourceLoader.ThreadLoadStatus.Failed))
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
        return packed is not null && GodotObject.IsInstanceValid(packed) ? packed : null;
    }

    private void ClearPreparedPhotoScene()
    {
        _preparedPhotoScene = null;
        _preparedPhotoScenePath = null;
        _preparedPhotoResources.Clear();
    }

    private void InvalidatePhotoScenePreload()
    {
        _photoScenePreloadGeneration = Interlocked.Increment(ref _photoSceneRequestSequence);
        ClearPreparedPhotoScene();
    }
}
