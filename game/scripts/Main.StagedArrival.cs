using System;
using System.Collections.Generic;
using Godot;
using Urman.Core.Persistence;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Main
{
    private object? _arrivalStageSession;
    private Act1ConnectedWorld? _arrivalBuildingWorld;
    private IEnumerator<Action>? _arrivalJobs;
    private string? _arrivalFailure;
    private ulong _arrivalCabinFrame = ulong.MaxValue;
    private ulong _arrivalPublishedPhysics;
    private bool _arrivalPublished, _arrivalPlaced, _arrivalWatchingNodes;
    private int _arrivalJobCount, _arrivalOverruns;
    private ulong _arrivalCpuUsec, _arrivalWorstUsec;
    private readonly List<Node> _arrivalNewNodes = new();
    private readonly List<(Node Node, StringName Group)> _arrivalGroups = new();
    private readonly List<(Node Node, ProcessModeEnum Mode)> _arrivalModes = new();
    private readonly List<(VisualInstance3D Node, uint Layers)> _arrivalVisuals = new();
    private readonly List<(Light3D Node, uint Mask)> _arrivalLights = new();
    private readonly List<(Node Node, bool Paused)> _arrivalAudio = new();
    private readonly List<WorldEnvironment> _arrivalEnvironments = new();

    private bool IsCurrentArrivalStage(object expectedSession) => IsInsideTree()
        && ReferenceEquals(_arrivalStageSession, expectedSession)
        && _fullGameRouteBridge is { } bridge && GodotObject.IsInstanceValid(bridge)
        && ReferenceEquals(bridge.SessionIdentity, expectedSession)
        && !bridge.IsPhotoWorldPresentationDeferred && bridge.ActiveSceneId == NivaArrivalSceneId;

    private bool ReserveStagedArrival(RuntimeBridge.FullGamePhotoWorldRouteProjection projection, RuntimeBridge bridge)
    {
        if (_arrivalStageSession is not null)
        {
            if (IsCurrentArrivalStage(projection.SessionIdentity)) return true;
            CancelStagedArrival();
        }
        if (projection.ActiveSceneId != NivaArrivalSceneId || bridge.IsDebugSession
            || bridge.IsPhotoWorldPresentationDeferred || !IsMountedW01()
            || _lastFullGameRouteScene != PrologueSceneId
            || !ReferenceEquals(_lastFullGameRouteSession, projection.SessionIdentity)) return false;
        _arrivalStageSession = projection.SessionIdentity;
        _arrivalCabinFrame = ulong.MaxValue;
        _arrivalFailure = null;
        bridge.SetWorldLocation("village_day", "arrival");
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            player.SetSessionTransition(true);
        GD.Print("arrival-stage-reserved: live prologue -> niva; awaiting real cabin");
        return true;
    }

    internal bool TryBeginStagedArrival(object expectedSession) => IsCurrentArrivalStage(expectedSession);

    internal void NotifyStagedArrivalCabinReady(object expectedSession)
    {
        if (!IsCurrentArrivalStage(expectedSession) || _arrivalCabinFrame != ulong.MaxValue) return;
        _arrivalCabinFrame = Engine.GetProcessFrames();
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            player.SetSessionTransition(true);
        GD.Print("arrival-cabin-ready: real visual/camera/approach mounted; village jobs may follow");
    }

    internal void ReportStagedArrivalCabinFailure(object expectedSession, string reason)
    {
        if (!IsCurrentArrivalStage(expectedSession) || _arrivalFailure is not null) return;
        FailStagedArrival(new InvalidOperationException($"The arrival cabin could not be mounted: {reason}"));
    }

    internal bool TryGetStagedArrivalSaveTransform(object expectedSession, out PlayerTransform transform)
    {
        transform = null!;
        if (!IsCurrentArrivalStage(expectedSession) || _arrivalFailure is not null
            || !Act1WorldLayout.TryGetWorldSpawn("village_day", "arrival", out var spawn)) return false;
        var position = spawn.Position;
        position.Y = Mathf.Max(position.Y, AgentBAct1HeightField.CollisionGround(position.X, position.Z) + .05f);
        transform = new PlayerTransform(new(position.X, position.Y, position.Z), new(0, spawn.YawDegrees, 0));
        return true;
    }

    internal string? ArrivalVillageFailure(object expectedSession) =>
        IsCurrentArrivalStage(expectedSession) ? _arrivalFailure : null;

    internal bool ArrivalVillageReady(object expectedSession)
    {
        if (!IsCurrentArrivalStage(expectedSession) || !_arrivalPublished || _arrivalFailure is not null
            || Engine.GetPhysicsFrames() < _arrivalPublishedPhysics + 3
            || _connectedWorld is not { IsBuilt: true, VehicleFleet: { } fleet }) return false;
        if (fleet.ProjectionFailure is { } failure)
        {
            FailStagedArrival(new InvalidOperationException(failure));
            return false;
        }
        return !fleet.PlacementValidationDeferred;
    }

    internal bool CompleteStagedArrivalPlacement(object expectedSession)
    {
        if (!IsCurrentArrivalStage(expectedSession)) return false;
        if (_arrivalPlaced) return true;
        if (!ArrivalVillageReady(expectedSession)
            || !TryGetStagedArrivalSaveTransform(expectedSession, out var portable)
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player) return false;
        var feet = new Vector3((float)portable.Position.X, (float)portable.Position.Y, (float)portable.Position.Z);
        using var query = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * 2f, feet - Vector3.Up * 2f, 1u,
            new global::Godot.Collections.Array<Rid> { player.GetRid() });
        using var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || hit["normal"].AsVector3().Y < .7f)
        {
            FailStagedArrival(new InvalidOperationException("Arrival standing place has no walkable floor."));
            return false;
        }
        feet.Y = hit["position"].AsVector3().Y + .05f;
        if (!player.CanStandAt(feet))
        {
            FailStagedArrival(new InvalidOperationException("Arrival standing place is obstructed."));
            return false;
        }
        player.ApplyZoneSpawn(feet, (float)portable.RotationDegrees.Y);
        player.Velocity = Vector3.Zero;
        LastSpawnYawDegrees = (float)portable.RotationDegrees.Y;
        ActiveZoneScenePath = ZoneScenes["village_day"];
        _hasLoadedInitialZone = true;
        _lastFullGameRouteSession = expectedSession;
        _lastFullGameRouteScene = NivaArrivalSceneId;
        _arrivalPlaced = true;
        // The cabin still owns camera, input and its transition hold.
        return true;
    }

    public override void _Process(double delta)
    {
        if (_arrivalStageSession is not { } session) return;
        if (!IsCurrentArrivalStage(session)) { CancelStagedArrival(); return; }
        if (_arrivalNewNodes.Count > 0) QuarantineArrivalNodes();
        if (_arrivalFailure is not null || _arrivalPublished || _arrivalCabinFrame == ulong.MaxValue
            || Engine.GetProcessFrames() <= _arrivalCabinFrame + 2
            || MainMenuUi.AnyUndismissed(GetTree())
            || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
            || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true }) return;
        try
        {
            if (_arrivalJobs is null)
            {
                // The fully rendered cabin owns the view before its old forest retires.
                foreach (var child in _zoneHost.GetChildren()) RetireWorldPresentation(child);
                _mountedW01 = null;
                _arrivalBuildingWorld = new Act1ConnectedWorld
                {
                    Name = Act1WorldLayout.ConnectedWorldRootName,
                    DeferBuildOnReady = true, ProcessMode = ProcessModeEnum.Disabled
                };
                GetTree().NodeAdded += OnArrivalNodeAdded;
                _arrivalWatchingNodes = true;
                _zoneHost.AddChild(_arrivalBuildingWorld);
                _arrivalJobs = _arrivalBuildingWorld.BuildJobs().GetEnumerator();
            }
            // Cooperative budget; the measured worst job includes MoveNext and
            // native resource calls. A single overrun is visible, never called bounded.
            const ulong budgetUsec = 8000;
            var frameStart = Time.GetTicksUsec();
            do
            {
                var started = Time.GetTicksUsec();
                bool next;
                using (Act1ConnectedWorld.InvariantNameScope())
                {
                    next = _arrivalJobs.MoveNext();
                    if (next) _arrivalJobs.Current();
                }
                QuarantineArrivalNodes();
                var elapsed = Time.GetTicksUsec() - started;
                _arrivalCpuUsec += elapsed;
                _arrivalWorstUsec = Math.Max(_arrivalWorstUsec, elapsed);
                if (next) _arrivalJobCount++;
                if (elapsed > 25000 && _arrivalOverruns++ < 20)
                    GD.Print($"arrival-build-overrun|job={_arrivalJobCount}|usec={elapsed}");
                if (!next) { PublishStagedArrival(); break; }
            } while (Time.GetTicksUsec() - frameStart < budgetUsec && IsCurrentArrivalStage(session));
        }
        catch (Exception error) { FailStagedArrival(error); }
    }

    private void OnArrivalNodeAdded(Node node)
    {
        if (_arrivalBuildingWorld is { } world && (ReferenceEquals(node, world) || world.IsAncestorOf(node)))
            _arrivalNewNodes.Add(node);
    }

    private void QuarantineArrivalNodes()
    {
        foreach (var node in _arrivalNewNodes)
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            foreach (var group in node.GetGroups())
            {
                if (group.ToString().StartsWith('_')) continue;
                _arrivalGroups.Add((node, group));
                node.RemoveFromGroup(group);
            }
            if (!ReferenceEquals(node, _arrivalBuildingWorld) && node.ProcessMode != ProcessModeEnum.Inherit)
            {
                _arrivalModes.Add((node, node.ProcessMode));
                node.ProcessMode = ProcessModeEnum.Inherit;
            }
            if (node is VisualInstance3D visual)
            {
                // Duplicating an already built piece must copy its authored
                // layers, not permanently inherit the construction quarantine.
                var layers = visual.HasMeta("arrivalAuthoredLayers")
                    ? (uint)visual.GetMeta("arrivalAuthoredLayers").AsInt64() : visual.Layers;
                visual.SetMeta("arrivalAuthoredLayers", layers);
                _arrivalVisuals.Add((visual, layers)); visual.Layers = 0;
            }
            if (node is Light3D light)
            {
                var mask = light.HasMeta("arrivalAuthoredLightMask")
                    ? (uint)light.GetMeta("arrivalAuthoredLightMask").AsInt64() : light.LightCullMask;
                light.SetMeta("arrivalAuthoredLightMask", mask);
                _arrivalLights.Add((light, mask)); light.LightCullMask = 0;
            }
            if (node is WorldEnvironment environment) _arrivalEnvironments.Add(environment);
            switch (node)
            {
                case AudioStreamPlayer audio: _arrivalAudio.Add((audio, audio.StreamPaused)); audio.StreamPaused = true; break;
                case AudioStreamPlayer3D audio: _arrivalAudio.Add((audio, audio.StreamPaused)); audio.StreamPaused = true; break;
                case AudioStreamPlayer2D audio: _arrivalAudio.Add((audio, audio.StreamPaused)); audio.StreamPaused = true; break;
            }
        }
        _arrivalNewNodes.Clear();
        // Existing environment nodes can be reprojected by later build jobs.
        foreach (var environment in _arrivalEnvironments)
            if (GodotObject.IsInstanceValid(environment)) environment.Environment = null;
    }

    private void PublishStagedArrival()
    {
        var publishStarted = Time.GetTicksUsec();
        _arrivalJobs?.Dispose();
        _arrivalJobs = null;
        StopArrivalNodeWatch();
        foreach (var (node, group) in _arrivalGroups) if (GodotObject.IsInstanceValid(node)) node.AddToGroup(group);
        foreach (var (node, mode) in _arrivalModes) if (GodotObject.IsInstanceValid(node)) node.ProcessMode = mode;
        foreach (var (node, layers) in _arrivalVisuals)
            if (GodotObject.IsInstanceValid(node)) { node.Layers = layers; node.RemoveMeta("arrivalAuthoredLayers"); }
        foreach (var (node, mask) in _arrivalLights)
            if (GodotObject.IsInstanceValid(node)) { node.LightCullMask = mask; node.RemoveMeta("arrivalAuthoredLightMask"); }
        foreach (var (node, paused) in _arrivalAudio)
            if (GodotObject.IsInstanceValid(node))
                switch (node)
                {
                    case AudioStreamPlayer audio: audio.StreamPaused = paused; break;
                    case AudioStreamPlayer3D audio: audio.StreamPaused = paused; break;
                    case AudioStreamPlayer2D audio: audio.StreamPaused = paused; break;
                }
        _connectedWorld = _arrivalBuildingWorld!;
        _connectedWorld.SetActiveLogicalZone("village_day");
        _connectedWorld.ActivateStagedBuild();
        if (_connectedWorld.GetZoneInstance("village_day") is StyleBenchmarkZone village)
            village.AttachNivaStopTargetToConnectedFleet();
        _connectedWorld.ProcessMode = ProcessModeEnum.Inherit;
        _arrivalPublishedPhysics = Engine.GetPhysicsFrames();
        _arrivalPublished = true;
        var publishUsec = Time.GetTicksUsec() - publishStarted;
        _arrivalCpuUsec += publishUsec;
        _arrivalWorstUsec = Math.Max(_arrivalWorstUsec, publishUsec);
        GD.Print($"arrival-build-publish|usec={publishUsec}");
        GD.Print($"arrival-build-ready|jobs={_arrivalJobCount}|cpu_usec={_arrivalCpuUsec}|worst_job_usec={_arrivalWorstUsec}");
        ClearArrivalQuarantine();
    }

    private void FailStagedArrival(Exception error)
    {
        _arrivalFailure = error.Message;
        _arrivalJobs?.Dispose();
        _arrivalJobs = null;
        QuarantineArrivalNodes();
        StopArrivalNodeWatch();
        if (!_arrivalPublished && _arrivalBuildingWorld is { } world && GodotObject.IsInstanceValid(world)) world.QueueFree();
        ClearArrivalQuarantine();
        GD.PushError($"Staged village construction failed; cabin/menu remain available: {error}");
    }

    private void StopArrivalNodeWatch()
    {
        if (!_arrivalWatchingNodes) return;
        GetTree().NodeAdded -= OnArrivalNodeAdded;
        _arrivalWatchingNodes = false;
    }

    private void ClearArrivalQuarantine()
    {
        _arrivalNewNodes.Clear(); _arrivalGroups.Clear(); _arrivalModes.Clear();
        _arrivalVisuals.Clear(); _arrivalLights.Clear(); _arrivalAudio.Clear();
        _arrivalEnvironments.Clear();
    }

    private void CancelStagedArrival()
    {
        _arrivalJobs?.Dispose(); _arrivalJobs = null;
        StopArrivalNodeWatch();
        if (!_arrivalPublished && _arrivalBuildingWorld is { } world && GodotObject.IsInstanceValid(world)) world.QueueFree();
        ClearArrivalQuarantine();
        _arrivalBuildingWorld = null; _arrivalStageSession = null; _arrivalFailure = null;
        _arrivalPublished = _arrivalPlaced = false; _arrivalCabinFrame = ulong.MaxValue;
        _arrivalJobCount = _arrivalOverruns = 0; _arrivalCpuUsec = _arrivalWorstUsec = 0;
    }
}
