using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Urman.Core.Persistence;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Main
{
    private object? _homeRestoreSession;
    private Act1ConnectedWorld? _homeRestoreWorld;
    private string? _homeRestoreTargetZone;
    private string? _homeRestoreTargetSpawn;
    private bool _homeRestoreShellReady;
    private bool _homeRestoreBuildReady;
    private string? _homeRestoreFailure;
    private ulong _homeRestoreStartedAtMsec;
    private long _homeRestoreGeneration;

    internal bool HomeRestoreBuildPending => _homeRestoreSession is not null && !_homeRestoreBuildReady;
    internal string HomeRestorePhotoWaitMessage => _homeRestoreFailure is null
        ? "Деревня ещё загружается. Скоро снимок можно будет открыть снова."
        : "Деревня не загрузилась. Подойдите к выходу из дома, чтобы повторить загрузку.";

    private bool BeginHomeStagedRestore(object session, string zoneId, string spawnPointId)
    {
        if (_homeRestoreSession is not null)
        {
            if (ReferenceEquals(_homeRestoreSession, session) && _homeRestoreWorld is { } current
                && ReferenceEquals(current, _connectedWorld)) return true;
            CancelHomeStagedRestore(retirePartialWorld: true);
        }

        if (_fullGameRouteBridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(bridge.SessionIdentity, session) || bridge.ActiveSceneId != HomeSceneId
            || !IsMountedW01() || _connectedWorld is not null) return false;

        var world = new Act1ConnectedWorld
        {
            Name = Act1WorldLayout.ConnectedWorldRootName,
            Visible = false,
            DeferBuildOnReady = true,
            ProcessMode = ProcessModeEnum.Disabled
        };
        _homeRestoreSession = session;
        _homeRestoreWorld = world;
        _homeRestoreTargetZone = zoneId;
        _homeRestoreTargetSpawn = spawnPointId;
        _homeRestoreShellReady = false;
        _homeRestoreBuildReady = false;
        _homeRestoreFailure = null;
        _homeRestoreStartedAtMsec = Time.GetTicksMsec();
        var generation = ++_homeRestoreGeneration;
        _connectedWorld = world;
        _zoneHost.AddChild(world);
        bridge.SetWorldLocation(zoneId, spawnPointId);
        _ = BuildHomeReturnAsync(session, world, zoneId, spawnPointId, generation);
        return true;
    }

    private bool IsCurrentHomeStagedRestore(object session, Act1ConnectedWorld world, long generation) =>
        IsCurrentHomeRestoreOwner(session, world, generation)
        && ReferenceEquals(_connectedWorld, world);

    private bool IsCurrentHomeRestoreOwner(object session, Act1ConnectedWorld stageWorld, long generation) =>
        IsInsideTree() && !IsQueuedForDeletion()
        && _homeRestoreGeneration == generation
        && ReferenceEquals(_homeRestoreSession, session)
        && ReferenceEquals(_homeRestoreWorld, stageWorld)
        && _fullGameRouteBridge is { } bridge && GodotObject.IsInstanceValid(bridge)
        && ReferenceEquals(bridge.SessionIdentity, session)
        && bridge.ActiveSceneId == HomeSceneId
        && bridge.CurrentZoneId == _homeRestoreTargetZone
        && !bridge.NeedsPhysicalRecovery;

    internal bool TryGetStagedHomeRestoreSaveTransform(
        object expectedSession, FirstPersonController player, out PlayerTransform transform)
    {
        transform = null!;
        if (_homeRestoreTargetZone != "house_old_pc"
            || _homeRestoreSession is not { } session || !ReferenceEquals(session, expectedSession)
            || _homeRestoreWorld is not { } world
            || !IsCurrentHomeStagedRestore(session, world, _homeRestoreGeneration)
            || !Act1WorldLayout.TryGetWorldSpawn("house_old_pc", _homeRestoreTargetSpawn ?? "entry", out var spawn)) return false;
        var home = world.GetZoneInstance("house_old_pc");
        if (_homeRestoreShellReady && home?.HasMeta("babaiRelocated") == true) return false;
        var sourceTransform = player.CapturePortableTransform();
        var sourcePosition = _homeRestoreShellReady ? player.GlobalPosition : world.ToGlobal(spawn.Position);
        if (_homeRestoreShellReady
            && !BabaiRelocation.InsideOldYard(sourcePosition.X, sourcePosition.Z)) return false;
        var worldPosition = BabaiRelocation.Transform * sourcePosition;
        transform = new PlayerTransform(
            new(worldPosition.X, worldPosition.Y, worldPosition.Z),
            new(_homeRestoreShellReady ? sourceTransform.RotationDegrees.X : 0,
                _homeRestoreShellReady
                    ? Mathf.PosMod(sourceTransform.RotationDegrees.Y + BabaiRelocation.YawDegrees, 360f)
                    : Mathf.PosMod(spawn.YawDegrees + BabaiRelocation.YawDegrees, 360f), 0));
        return true;
    }

    internal async Task<bool> WaitForStagedHomeRestoreAsync(object expectedSession)
    {
        var tree = GetTree();
        if (_fullGameRouteBridge is not { } currentBridge || !GodotObject.IsInstanceValid(currentBridge)
            || !ReferenceEquals(currentBridge.SessionIdentity, expectedSession)
            || currentBridge.ActiveSceneId != HomeSceneId) return false;
        while (ReferenceEquals(_homeRestoreSession, expectedSession) && !_homeRestoreShellReady)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!IsInsideTree() || IsQueuedForDeletion()
                || _fullGameRouteBridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
                || !ReferenceEquals(bridge.SessionIdentity, expectedSession)
                || bridge.ActiveSceneId != HomeSceneId) return false;
        }
        return ReferenceEquals(_homeRestoreSession, expectedSession) && _homeRestoreShellReady;
    }

    private async Task BuildHomeReturnAsync(
        object session, Act1ConnectedWorld world, string zoneId, string spawnPointId, long generation)
    {
        try
        {
            if (!await RunHomeRestoreJobsAsync(world.BuildHomeFirstJobs(), session, world, generation, quarantineHome: true))
                return;
            if (!IsCurrentHomeStagedRestore(session, world, generation)) return;

            world.PublishHomeFirstBuild();
            world.ProcessMode = ProcessModeEnum.Inherit;
            world.Visible = true;
            AlsuStreetWalkPresentation.SessionOwner(GetTree())?.DisableContactForZoneDisposal();
            foreach (var child in _zoneHost.GetChildren())
                if (!ReferenceEquals(child, world)) RetireWorldPresentation(child);
            _mountedW01 = null;
            _mountedW02 = null;
            _mountedW03 = null;
            _mountedW04 = null;
            _mountedW05 = null;

            _homeRestoreShellReady = true;
            SwitchZone(zoneId, spawnPointId);
            if (!IsMountedZone(zoneId) || _connectedWorld?.ActiveZoneId != zoneId)
            {
                _homeRestoreShellReady = false;
                throw new InvalidOperationException("The staged home shell did not become the active physical zone.");
            }
            GD.Print($"home-restore-room-ready|elapsed_ms={Time.GetTicksMsec() - _homeRestoreStartedAtMsec}");

            if (!await RunHomeRestoreJobsAsync(world.BuildJobs(), session, world, generation, quarantineHome: false))
                return;
            if (!IsCurrentHomeStagedRestore(session, world, generation)) return;
            world.ActivateStagedBuild();
            world.SetActiveLogicalZone("house_old_pc");
            _homeRestoreBuildReady = true;
            GD.Print($"home-restore-build-ready|elapsed_ms={Time.GetTicksMsec() - _homeRestoreStartedAtMsec}");
        }
        catch (Exception error)
        {
            if (!_homeRestoreShellReady)
            {
                CancelHomeStagedRestore(session, world, generation, retirePartialWorld: true);
                GD.PushError($"Staged home/village construction failed before the room opened: {error}");
            }
            else if (IsCurrentHomeStagedRestore(session, world, generation))
            {
                _homeRestoreFailure = error.Message;
                GD.PushError($"Staged home/village construction failed after the room opened; door retry is available: {error}");
            }
        }
    }

    private async Task<bool> RetryHomeStagedBuildAtDoorAsync(object session)
    {
        if (_homeRestoreFailure is null || _homeRestoreWorld is not { } oldWorld
            || !IsCurrentHomeStagedRestore(session, oldWorld, _homeRestoreGeneration)) return false;
        var generation = _homeRestoreGeneration;
        var replacement = new Act1ConnectedWorld
        {
            Name = Act1WorldLayout.ConnectedWorldRootName,
            Visible = false,
            DeferBuildOnReady = true,
            ProcessMode = ProcessModeEnum.Disabled
        };
        _zoneHost.AddChild(replacement);
        var swapped = false;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var playerTransform = player?.GlobalTransform ?? Transform3D.Identity;
        var playerVelocity = player?.Velocity ?? Vector3.Zero;
        try
        {
            if (!oldWorld.TryTransferHouseholdAudioScheduleTo(replacement, session))
                throw new InvalidOperationException("The current household schedule could not be transferred to the retry build.");
            if (!await RunHomeRestoreJobsAsync(replacement.BuildHomeFirstJobs(), session, replacement, generation,
                    quarantineHome: true, stageWorld: oldWorld)) return false;

            oldWorld.SetHomeFirstEnvironmentEnabled(false);
            if (!await RunHomeRestoreJobsAsync(replacement.BuildJobs(), session, replacement, generation,
                    quarantineHome: false, stageWorld: oldWorld)) return false;
            if (!IsCurrentHomeStagedRestore(session, oldWorld, generation) || !replacement.IsBuilt)
                throw new InvalidOperationException("The door retry lost ownership before the replacement village was ready.");

            replacement.ActivateStagedBuild();
            replacement.SetActiveLogicalZone("house_old_pc");
            replacement.SetHomeFirstEnvironmentEnabled(false);
            var oldHouse = oldWorld.GetZoneInstance("house_old_pc");
            var newHouse = replacement.GetZoneInstance("house_old_pc");
            Transform3D playerDestination = playerTransform;
            Vector3 velocityDestination = playerVelocity;
            if (player is not null && oldHouse is not null && newHouse is not null
                && BabaiRelocation.InsideOldYard(player.GlobalPosition.X, player.GlobalPosition.Z))
            {
                var delta = newHouse.GlobalTransform * oldHouse.GlobalTransform.AffineInverse();
                playerDestination = delta * playerTransform;
                velocityDestination = delta.Basis * playerVelocity;
            }

            oldWorld.Visible = false;
            oldWorld.ProcessMode = ProcessModeEnum.Disabled;
            replacement.PublishHomeFirstBuild();
            replacement.Visible = true;
            replacement.ProcessMode = ProcessModeEnum.Inherit;
            replacement.SetActiveLogicalZone("house_old_pc");
            if (player is not null)
                player.ApplyTraversalTransform(playerDestination, velocityDestination);

            _connectedWorld = replacement;
            _homeRestoreWorld = replacement;
            _homeRestoreGeneration++;
            _homeRestoreBuildReady = true;
            _homeRestoreFailure = null;
            RetireWorldPresentation(oldWorld);
            swapped = true;
            GD.Print("home-restore-build-recovered|zone=house_old_pc");
            return true;
        }
        catch (Exception error)
        {
            if (_homeRestoreGeneration == generation && ReferenceEquals(_homeRestoreWorld, oldWorld))
            {
                _homeRestoreFailure = error.Message;
                GD.PushError($"Home door retry failed; the existing room remains available: {error}");
            }
            return false;
        }
        finally
        {
            if (!swapped && IsCurrentHomeStagedRestore(session, oldWorld, generation))
            {
                if (player is { } currentPlayer && GodotObject.IsInstanceValid(currentPlayer))
                    currentPlayer.ApplyTraversalTransform(playerTransform, playerVelocity);
                oldWorld.Visible = true;
                oldWorld.ProcessMode = ProcessModeEnum.Inherit;
                oldWorld.SetHomeFirstEnvironmentEnabled(true);
                if (GodotObject.IsInstanceValid(replacement) && !replacement.IsQueuedForDeletion())
                    RetireWorldPresentation(replacement);
            }
        }
    }

    private async Task<bool> RunHomeRestoreJobsAsync(
        IEnumerable<Action> source, object session, Act1ConnectedWorld world, long generation,
        bool quarantineHome, Act1ConnectedWorld? stageWorld = null)
    {
        stageWorld ??= world;
        using var jobs = source.GetEnumerator();
        const ulong budgetUsec = 8000;
        var frameStarted = Time.GetTicksUsec();
        var house = world.GetZoneInstance("house_old_pc");
        while (IsCurrentHomeRestoreBuild(session, stageWorld, world, generation))
        {
            var beforeHouse = house is { } beforeNode && GodotObject.IsInstanceValid(beforeNode)
                ? beforeNode.GlobalTransform : Transform3D.Identity;
            var wasRelocated = house?.HasMeta("babaiRelocated") == true;
            bool next;
            using (Act1ConnectedWorld.InvariantNameScope())
            {
                next = jobs.MoveNext();
                if (next) jobs.Current();
            }
            if (!wasRelocated && house is { } relocatedNode && GodotObject.IsInstanceValid(relocatedNode)
                && relocatedNode.HasMeta("babaiRelocated"))
                FollowStagedHouseRelocation(relocatedNode, beforeHouse);
            if (quarantineHome) world.QuarantineHomeFirstBuild();
            else world.KeepHomeEnvironmentActiveDuringRestore();
            if (!next) return true;
            if (Time.GetTicksUsec() - frameStarted >= budgetUsec)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                frameStarted = Time.GetTicksUsec();
            }
        }
        if (ReferenceEquals(stageWorld, world))
            CancelHomeStagedRestore(session, stageWorld, generation, retirePartialWorld: true);
        else if (GodotObject.IsInstanceValid(world) && !world.IsQueuedForDeletion())
            RetireWorldPresentation(world);
        return false;
    }

    private bool IsCurrentHomeRestoreBuild(
        object session, Act1ConnectedWorld stageWorld, Act1ConnectedWorld buildWorld, long generation) =>
        IsCurrentHomeRestoreOwner(session, stageWorld, generation)
        && (ReferenceEquals(stageWorld, buildWorld)
            ? ReferenceEquals(_connectedWorld, buildWorld)
            : buildWorld.IsInsideTree() && !buildWorld.IsQueuedForDeletion());

    private void FollowStagedHouseRelocation(Node3D house, Transform3D beforeHouse)
    {
        if (!_homeRestoreShellReady
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !BabaiRelocation.InsideOldYard(player.GlobalPosition.X, player.GlobalPosition.Z)) return;
        var delta = house.GlobalTransform * beforeHouse.AffineInverse();
        player.ApplyTraversalTransform(delta * player.GlobalTransform, delta.Basis * player.Velocity);
    }

    private void CancelHomeStagedRestore(bool retirePartialWorld)
    {
        var world = _homeRestoreWorld;
        _homeRestoreGeneration++;
        _homeRestoreSession = null;
        _homeRestoreWorld = null;
        _homeRestoreTargetZone = null;
        _homeRestoreTargetSpawn = null;
        _homeRestoreShellReady = false;
        _homeRestoreBuildReady = false;
        _homeRestoreFailure = null;
        if (retirePartialWorld && world is not null && GodotObject.IsInstanceValid(world)
            && !world.IsBuilt && ReferenceEquals(_connectedWorld, world))
        {
            _connectedWorld = null;
            RetireWorldPresentation(world);
        }
    }

    private void CancelHomeStagedRestore(
        object expectedSession, Act1ConnectedWorld expectedWorld, long expectedGeneration, bool retirePartialWorld)
    {
        if (_homeRestoreGeneration != expectedGeneration
            || !ReferenceEquals(_homeRestoreSession, expectedSession)
            || !ReferenceEquals(_homeRestoreWorld, expectedWorld)) return;
        CancelHomeStagedRestore(retirePartialWorld);
    }
}
