using Godot;
using System.Text.Json;
using Urman.Core.Persistence;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Main : Node3D
{
    private const string W01ScenePath = "res://scenes/photoworlds/W01/W01.tscn";
    private const string W02ScenePath = "res://scenes/photoworlds/W02/W02.tscn";
    private const string W03ScenePath = "res://scenes/photoworlds/W03/W03.tscn";
    private const string W04ScenePath = "res://scenes/photoworlds/W04/W04.tscn";
    private const string W05ScenePath = "res://scenes/photoworlds/W05/W05.tscn";
    private const string PrologueSceneId = "urman.fullgame:scene/pw-prologue";
    private const string NivaArrivalSceneId = "urman.fullgame:scene/pw-niva-arrival";
    private const string HomeSceneId = "urman.fullgame:scene/pw-home";
    private const string ForestSceneId = "urman.fullgame:scene/pw-forest";
    private const string RiverSceneId = "urman.fullgame:scene/pw-river";
    private const string HousePhotoSceneId = "urman.fullgame:scene/pw-house";
    private const string CommonPhotoSceneId = "urman.fullgame:scene/pw-common";
    private const string CellarPhotoSceneId = "urman.fullgame:scene/pw-cellar";
    private const string BankApproachSceneId = "urman.fullgame:scene/pw-bank-approach";
    private const string PhotoEpilogueSceneId = "urman.fullgame:scene/pw-epilogue";

    private static readonly IReadOnlyDictionary<string, string> ZoneScenes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["village_day"] = "res://scenes/zones/style_benchmark_day_street.tscn",
        ["house_old_pc"] = "res://scenes/zones/style_benchmark_house_pc.tscn",
        ["fap_clinic"] = "res://scenes/zones/chapter1_fap_clinic.tscn",
        ["zirat_road"] = "res://scenes/zones/chapter1_zirat_road.tscn",
        ["kara_urman_night"] = "res://scenes/zones/style_benchmark_kara_urman_night.tscn",
        ["fullgame_act2_house"] = "res://scenes/zones/fullgame/act2_house.tscn",
        ["fullgame_act2_river"] = "res://scenes/zones/fullgame/act2_river.tscn",
        ["fullgame_act2_mosque"] = "res://scenes/zones/fullgame/act2_mosque.tscn",
        ["fullgame_act2_council"] = "res://scenes/zones/fullgame/act2_council.tscn",
        ["fullgame_act3_archive"] = "res://scenes/zones/fullgame/act3_archive.tscn",
        ["fullgame_act3_soviet"] = "res://scenes/zones/fullgame/act3_soviet.tscn",
        ["fullgame_act3_water"] = "res://scenes/zones/fullgame/act3_water.tscn",
        ["fullgame_act4_tukay"] = "res://scenes/zones/fullgame/act4_tukay.tscn",
        ["fullgame_act4_1552"] = "res://scenes/zones/fullgame/act4_1552.tscn",
        ["fullgame_act4_pact"] = "res://scenes/zones/fullgame/act4_pact.tscn",
        ["fullgame_act5_boundary"] = "res://scenes/zones/fullgame/act5_boundary.tscn",
        ["fullgame_act5_epilogue"] = "res://scenes/zones/fullgame/act5_epilogue.tscn"
    };

    private Node3D _zoneHost = null!;
    private ColorRect? _zoneTransition;
    private Tween? _zoneTransitionTween;
    private bool _hasLoadedInitialZone;
    private Act1ConnectedWorld? _connectedWorld;
    private bool _zoneSwitchBusy;
    private RuntimeBridge? _fullGameRouteBridge;
    private Node3D? _mountedW01;
    private Node3D? _mountedW02;
    private Node3D? _mountedW03;
    private Node3D? _mountedW04;
    private Node3D? _mountedW05;
    private object? _lastFullGameRouteSession;
    private string? _lastFullGameRouteScene;
    private bool _routingFullGameScene;
    private bool _preserveLoadedPhotoWorldTransform;
    private object? _loadedPhotoWorldTransformSession;

    public string ActiveZoneScenePath { get; private set; } = string.Empty;

    /// <summary>
    /// Read-only presentation diagnostic for the last compact-zone placement.
    /// It lets smoke tests prove the destination-facing contract without
    /// treating a player's subsequent mouse/gamepad look as a failure.
    /// </summary>
    public float LastSpawnYawDegrees { get; private set; }

    public Act1ConnectedWorld? ConnectedWorld => _connectedWorld;

    internal bool CanPrepareW01Visit(FirstPersonController player, string zoneId, string spawnId)
    {
        return _connectedWorld is { ActiveZoneId: "house_old_pc" } world && zoneId == "house_old_pc"
            && Act1WorldLayout.TryGetPlacement(zoneId, out var placement) && placement.SpawnPoints.ContainsKey(spawnId)
            && world.TryGetWorldSpawn(zoneId, spawnId, out _)
            && player.IsOnFloor() && PhotoVisitPoseHasSupport(player, player.GlobalPosition);
    }

    private static bool PhotoVisitPoseHasSupport(FirstPersonController player, Vector3 position)
    {
        if (!player.CanStandAt(position)) return false;
        var from = player.GlobalTransform;
        from.Origin = position + Vector3.Up * .03f;
        using var contact = new KinematicCollision3D();
        return player.TestMove(from, Vector3.Down * .20f, contact, .001f, true, 4)
            && contact.GetCollisionCount() > 0 && contact.GetNormal().Dot(Vector3.Up) >= .7f;
    }

    internal async Task<bool> CompleteW01VisitProjectionAsync(object session, JsonElement? originTicket = null)
    {
        var bridge = _fullGameRouteBridge;
        bool Current() => GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
            && bridge is not null && GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(session, bridge.SessionIdentity) && !bridge.IsPhotoBookProjectionDeferred;
        if (!Current()) return false;
        ReconcileFullGameScenePresentation();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!Current()) return false;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!Current() || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player) return false;
        if (originTicket is not { } ticket)
            return bridge!.ActiveSceneId == ForestSceneId && IsMountedW01()
                && PhotoVisitPoseHasSupport(player, player.GlobalPosition);

        var zone = ticket.GetProperty("originWorldId").GetString()!;
        var safeNode = ticket.GetProperty("originSafeNode").GetString()!;
        if (bridge!.ActiveSceneId != HomeSceneId || _connectedWorld is not { } world
            || world.ActiveZoneId != zone || !Act1WorldLayout.TryGetPlacement(zone, out var placement)
            || !placement.SpawnPoints.ContainsKey(safeNode) || !world.TryGetWorldSpawn(zone, safeNode, out var fallback)) return false;
        var position = ticket.GetProperty("position");
        var original = new Vector3(position.GetProperty("x").GetSingle(), position.GetProperty("y").GetSingle(), position.GetProperty("z").GetSingle());
        var anchor = world.GetZoneInstance(zone)?.GetNodeOrNull<InteractionTarget>("PhotoWorldForestEntryTarget");
        var originalSafe = anchor is not null && anchor.GlobalPosition.DistanceSquaredTo(original) <= 36f
            && PhotoVisitPoseHasSupport(player, original);
        if (!originalSafe && !PhotoVisitPoseHasSupport(player, fallback.Position)) return false;
        var chosen = originalSafe ? original : fallback.Position;
        player.ApplyPortableTransform(new PlayerTransform(new(chosen.X, chosen.Y, chosen.Z),
            new(ticket.GetProperty("pitch").GetDouble(), originalSafe ? ticket.GetProperty("rotationY").GetDouble() : fallback.YawDegrees, 0)));
        if (!originalSafe) player.NotifyTraversal("Вернулся к безопасному месту рядом.");
        if (AtmosphereProfiles.AppliedProfileId != ticket.GetProperty("originProfileId").GetString()) return false;
        if (GetTree().GetFirstNodeInGroup("ambient_audio") is not AmbientAudioDirector audio) return false;
        var snapshot = ticket.GetProperty("originAudioSnapshot");
        audio.SetPhysicalShelter(snapshot.GetProperty("physicalSheltered").GetBoolean());
        audio.SetZone(snapshot.GetProperty("zoneId").GetString()!, snapshot.GetProperty("spawnPointId").GetString());
        return audio.CurrentStemId == snapshot.GetProperty("stemId").GetString();
    }

    [Export]
    public string InitialZoneId { get; set; } = "village_day";

    [Export]
    public string InitialSpawnPointId { get; set; } = "arrival";

    /// <summary>
    /// Presentation-only transition used by the Act 1 demo. Full-game scenes
    /// keep the default false value until their own transition contract exists.
    /// </summary>
    [Export]
    public bool EnableZoneTransitionFade { get; set; }

    /// <summary>
    /// Opt-in presentation mode for connected Act I zones. The full-game
    /// entrypoint enables it only for arrival and home routes that reuse them.
    /// </summary>
    [Export]
    public bool EnableAct1ConnectedWorld { get; set; }

    /// <summary>Routes committed PhotoWorlds scenes to their existing physical presentation.</summary>
    [Export]
    public bool EnableFullGamePhotoWorldRouting { get; set; }

    public override void _Ready()
    {
        ConfigureInteractiveFrameCapture();
        AddToGroup("zone_manager");
        _zoneHost = GetNode<Node3D>("ZoneHost");
        if (EnableZoneTransitionFade)
        {
            BuildZoneTransitionOverlay();
        }

        if (EnableFullGamePhotoWorldRouting)
        {
            AttachFullGameRouteBridge();
            ReconcileFullGameScenePresentation();
        }
        else
        {
            if (EnableAct1ConnectedWorld)
                EnsureConnectedWorld();
            SwitchZone(InitialZoneId, InitialSpawnPointId);
        }
        GD.Print("УРМАН Godot vertical slice ready: Painterly Low-Poly / first person");
    }

    public override void _ExitTree()
    {
        if (_fullGameRouteBridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= OnFullGameRuntimeStateChanged;
        _fullGameRouteBridge = null;
        _mountedW01 = null;
        _mountedW02 = null;
        _mountedW03 = null;
        _mountedW04 = null;
        _mountedW05 = null;
    }

    internal static bool IsKnownZone(string zoneId) => ZoneScenes.ContainsKey(zoneId);

    /// <summary>
    /// Ordinary compact-zone travel captures the departing physical companion
    /// through the existing runtime before disposing her presentation. Load,
    /// new game and explicit review fixtures already own their snapshot and
    /// continue to use the synchronous projection below.
    /// </summary>
    public async Task<bool> SwitchZoneAsync(string zoneId, string spawnPointId)
    {
        if (_zoneSwitchBusy || !ZoneScenes.ContainsKey(zoneId)) return false;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var session = bridge?.SessionIdentity;
        if (bridge is null || session is null) return false;
        _zoneSwitchBusy = true;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        LoadingScreenUi? loading = null;
        try
        {
            player?.SetSessionTransition(true);
            if (DisplayServer.GetName() != "headless")
            {
                loading = LoadingScreenUi.Show(this, "Смена локации", "Готовим новое место в деревне");
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
            var retiringWorld = _connectedWorld is { } currentWorld && !Act1WorldLayout.ContainsZone(zoneId)
                ? currentWorld
                : null;
            if (retiringWorld?.VehicleFleet is { } fleet
                && !await fleet.FlushForSaveAsync()) return false;
            if (retiringWorld is not null
                && (!ReferenceEquals(retiringWorld, _connectedWorld)
                    || !ReferenceEquals(session, bridge.SessionIdentity))) return false;
            if (retiringWorld is not null && !await retiringWorld.FlushFacilitiesForSaveAsync()) return false;
            if ((_connectedWorld is null || retiringWorld is not null)
                && AlsuStreetWalkPresentation.SessionOwner(GetTree()) is { } companion
                && !await companion.FlushForSaveAsync()) return false;
            if (!IsInsideTree() || IsQueuedForDeletion() || !ReferenceEquals(session, bridge.SessionIdentity))
                return false;
            SwitchZone(zoneId, spawnPointId);
            return bridge.CurrentZoneId == zoneId && bridge.CurrentSpawnPointId == spawnPointId;
        }
        finally
        {
            if (loading is { } screen && GodotObject.IsInstanceValid(screen)) screen.Hide();
            if (player is { } controller && GodotObject.IsInstanceValid(controller))
                controller.SetSessionTransition(bridge.NeedsPhysicalRecovery);
            _zoneSwitchBusy = false;
        }
    }

    public void SwitchZone(string zoneId, string spawnPointId)
    {
        if (!ZoneScenes.TryGetValue(zoneId, out var scenePath))
        {
            GD.PushError($"Unknown zone {zoneId}.");
            return;
        }

        if (EnableFullGamePhotoWorldRouting
            && GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge deferredBridge
            && deferredBridge.IsW01PresentationDeferred)
        {
            if (deferredBridge.IsPhotoWorldSaveRestoreInProgress)
            {
                _preserveLoadedPhotoWorldTransform = true;
                _loadedPhotoWorldTransformSession = deferredBridge.ProjectionSessionIdentity;
            }
            return;
        }

        if (EnableAct1ConnectedWorld && Act1WorldLayout.ContainsZone(zoneId))
            EnsureConnectedWorld();
        else
            RetireConnectedWorld();

        if (_connectedWorld is not null)
        {
            if (!_connectedWorld.TryGetWorldSpawn(zoneId, spawnPointId, out var connectedSpawn))
            {
                GD.PushError($"Connected Act I world has no mapped spawn '{zoneId}@{spawnPointId}'; transition aborted.");
                return;
            }

            _connectedWorld.SetActiveLogicalZone(zoneId);
            ActiveZoneScenePath = scenePath;
            if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController connectedPlayer)
            {
                connectedPlayer.ApplyZoneSpawn(connectedSpawn.Position, connectedSpawn.YawDegrees);
                LastSpawnYawDegrees = connectedSpawn.YawDegrees;
            }

            if (GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge connectedBridge)
            {
                connectedBridge.SetWorldLocation(zoneId, spawnPointId);
                if (connectedBridge.IsPhotoWorldsCampaign
                    && connectedBridge.ActiveSceneId == BankApproachSceneId
                    && zoneId == "village_day" && spawnPointId == "pw-bank-approach")
                    AlsuStreetWalkPresentation.SessionOwner(GetTree())?.StagePhotoBankMeeting();
            }

            if (GetTree().GetFirstNodeInGroup("ambient_audio") is AmbientAudioDirector connectedAmbience)
            {
                connectedAmbience.SetZone(zoneId, spawnPointId);
            }

            if (_hasLoadedInitialZone)
            {
                PlayZoneTransition();
            }

            _hasLoadedInitialZone = true;
            GD.Print($"zone-loaded: {zoneId}@{spawnPointId} connected-world={_connectedWorld.PersistentInstanceIdentity}");
            return;
        }

        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed is null)
        {
            GD.PushError($"Zone scene is missing: {scenePath}.");
            return;
        }

        // QueueFree retires the old zone at the end of the frame. Its companion
        // must stop participating in contact tests before the new one projects.
        AlsuStreetWalkPresentation.SessionOwner(GetTree())?.DisableContactForZoneDisposal();
        if (EnableFullGamePhotoWorldRouting
            && GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge restoringBridge
            && restoringBridge.IsPhotoWorldSaveRestoreInProgress)
        {
            _preserveLoadedPhotoWorldTransform = true;
            _loadedPhotoWorldTransformSession = restoringBridge.ProjectionSessionIdentity;
        }
        _mountedW01 = null;
        _mountedW02 = null;
        _mountedW03 = null;
        _mountedW04 = null;
        _mountedW05 = null;
        foreach (var child in _zoneHost.GetChildren())
        {
            if (child is Node3D oldZone) oldZone.Visible = false;
            child.QueueFree();
        }

        var zone = packed.Instantiate<Node3D>();
        if (zone is FullGameZone fullGameZone)
        {
            fullGameZone.ZoneId = zoneId;
        }

        _zoneHost.AddChild(zone);
        RinatPresencePresentation.AttachCompact(zone, zoneId);
        ActiveZoneScenePath = scenePath;
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            var spawn = SpawnTransform(zoneId, spawnPointId);
            player.ApplyZoneSpawn(spawn.Position, spawn.YawDegrees);
            LastSpawnYawDegrees = spawn.YawDegrees;
        }

        if (GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge bridge)
        {
            bridge.SetWorldLocation(zoneId, spawnPointId);
        }

        if (GetTree().GetFirstNodeInGroup("ambient_audio") is AmbientAudioDirector ambience)
        {
            ambience.SetZone(zoneId, spawnPointId);
        }

        if (_hasLoadedInitialZone)
        {
            PlayZoneTransition();
        }

        _hasLoadedInitialZone = true;
        GD.Print($"zone-loaded: {zoneId}@{spawnPointId}");
    }

    private void EnsureConnectedWorld()
    {
        if (_connectedWorld is not null) return;

        AlsuStreetWalkPresentation.SessionOwner(GetTree())?.DisableContactForZoneDisposal();
        _mountedW01 = null;
        _mountedW02 = null;
        _mountedW03 = null;
        _mountedW04 = null;
        _mountedW05 = null;
        foreach (var child in _zoneHost.GetChildren())
        {
            if (child is Node3D oldZone) oldZone.Visible = false;
            child.QueueFree();
        }

        _connectedWorld = new Act1ConnectedWorld
        {
            Name = Act1WorldLayout.ConnectedWorldRootName
        };
        _zoneHost.AddChild(_connectedWorld);
    }

    private void RetireConnectedWorld()
    {
        if (_connectedWorld is not { } retiringWorld) return;
        AlsuStreetWalkPresentation.SessionOwner(GetTree())?.DisableContactForZoneDisposal();
        _connectedWorld = null;
        retiringWorld.Visible = false;
        retiringWorld.QueueFree();
    }

    private void AttachFullGameRouteBridge()
    {
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (ReferenceEquals(_fullGameRouteBridge, bridge)) return;
        if (_fullGameRouteBridge is { } previous && GodotObject.IsInstanceValid(previous))
            previous.RuntimeStateChanged -= OnFullGameRuntimeStateChanged;
        _fullGameRouteBridge = bridge;
        if (_fullGameRouteBridge is { } current)
            current.RuntimeStateChanged += OnFullGameRuntimeStateChanged;
    }

    private void OnFullGameRuntimeStateChanged()
    {
        if (!EnableFullGamePhotoWorldRouting || !IsInsideTree()) return;
        AttachFullGameRouteBridge();
        ReconcileFullGameScenePresentation();
    }

    private void ReconcileFullGameScenePresentation()
    {
        if (_routingFullGameScene || !EnableFullGamePhotoWorldRouting || !IsInsideTree()) return;
        AttachFullGameRouteBridge();
        if (_fullGameRouteBridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { } projection
            || !ReferenceEquals(projection.SessionIdentity, bridge.SessionIdentity))
            return;

        _routingFullGameScene = true;
        try
        {
            if (projection.ActiveSceneId is PrologueSceneId or ForestSceneId)
            {
                MountW01(projection, bridge);
                return;
            }

            if (projection.ActiveSceneId == RiverSceneId)
            {
                MountW02(projection, bridge);
                return;
            }

            if (projection.ActiveSceneId == HousePhotoSceneId)
            {
                MountW03(projection, bridge);
                return;
            }

            if (projection.ActiveSceneId == CommonPhotoSceneId)
            {
                MountW04(projection, bridge);
                return;
            }

            if (projection.ActiveSceneId == CellarPhotoSceneId)
            {
                MountW05(projection, bridge);
                return;
            }

            var targetZone = projection.ActiveSceneId == PhotoEpilogueSceneId
                ? "house_old_pc"
                : projection.ActiveSceneId is NivaArrivalSceneId or BankApproachSceneId
                ? "village_day"
                : bridge.CurrentZoneId == "house_old_pc" ? "house_old_pc" : "village_day";
            var targetSpawn = projection.ActiveSceneId == BankApproachSceneId
                ? "pw-bank-approach"
                : targetZone == "house_old_pc"
                ? (bridge.CurrentZoneId != targetZone || string.IsNullOrWhiteSpace(bridge.CurrentSpawnPointId)
                    ? "entry" : bridge.CurrentSpawnPointId)
                : projection.ActiveSceneId == HomeSceneId && bridge.CurrentZoneId == "village_day"
                    ? (string.IsNullOrWhiteSpace(bridge.CurrentSpawnPointId) ? "arrival" : bridge.CurrentSpawnPointId)
                    : "arrival";

            // Home and bank share the connected village. Rebuild it when
            // returning from a detached photo world or changing the requested
            // physical location.
            var needsZonePresentation = IsMountedW01()
                || IsMountedW02()
                || IsMountedW03()
                || IsMountedW04()
                || IsMountedW05()
                || bridge.CurrentZoneId != targetZone
                || bridge.CurrentSpawnPointId != targetSpawn
                || (EnableAct1ConnectedWorld && Act1WorldLayout.ContainsZone(targetZone)
                    && _connectedWorld?.ActiveZoneId != targetZone)
                || (!EnableAct1ConnectedWorld && !IsMountedZone(targetZone));
            if (needsZonePresentation)
                SwitchZone(targetZone, targetSpawn);

            if (!IsMountedZone(targetZone)) return;
            _hasLoadedInitialZone = true;

            _preserveLoadedPhotoWorldTransform = false;
            _loadedPhotoWorldTransformSession = null;
            _lastFullGameRouteSession = projection.SessionIdentity;
            _lastFullGameRouteScene = projection.ActiveSceneId;
        }
        finally
        {
            _routingFullGameScene = false;
        }
    }

    private void MountW01(RuntimeBridge.FullGamePhotoWorldRouteProjection projection, RuntimeBridge bridge)
    {
        var entryAnchorId = projection.ActiveSceneId == ForestSceneId ? "F10" : "F00";
        var alreadyMounted = IsMountedW01();
        var routeSceneChanged = !string.Equals(_lastFullGameRouteScene, projection.ActiveSceneId, StringComparison.Ordinal);
        var sameSession = ReferenceEquals(_lastFullGameRouteSession, projection.SessionIdentity);

        if (alreadyMounted)
        {
            if (routeSceneChanged && sameSession)
            {
                var resolved = ResolveEntryAnchor(_mountedW01!, entryAnchorId, "F00");
                PlacePlayerAtW01Entry(_mountedW01!, resolved);
            }
            _preserveLoadedPhotoWorldTransform = false;
            _loadedPhotoWorldTransformSession = null;
            SetW01Ambience();
            _hasLoadedInitialZone = true;
            _lastFullGameRouteSession = projection.SessionIdentity;
            _lastFullGameRouteScene = projection.ActiveSceneId;
            return;
        }

        var packed = ResourceLoader.Load<PackedScene>(W01ScenePath);
        if (packed is null)
        {
            GD.PushError($"Full-game W01 presentation is missing: {W01ScenePath}.");
            return;
        }

        var world = packed.Instantiate<Node3D>();
        var resolvedAnchorId = entryAnchorId;
        if (world.GetNodeOrNull<Node3D>($"Anchors/{entryAnchorId}") is null)
        {
            // Named fallback instead of an unnamed/zero spawn: the default
            // entry anchor is the verified safe node for this world.
            const string fallbackAnchorId = "F00";
            if (entryAnchorId != fallbackAnchorId
                && world.GetNodeOrNull<Node3D>($"Anchors/{fallbackAnchorId}") is not null)
            {
                GD.PushWarning($"Full-game W01 presentation has no authored Anchors/{entryAnchorId} entry point; "
                    + $"using named fallback Anchors/{fallbackAnchorId}.");
                resolvedAnchorId = fallbackAnchorId;
            }
            else
            {
                world.Free();
                GD.PushError($"Full-game W01 presentation has no authored Anchors/{entryAnchorId} entry point.");
                return;
            }
        }

        RetireConnectedWorld();
        AlsuStreetWalkPresentation.SessionOwner(GetTree())?.DisableContactForZoneDisposal();
        _mountedW02 = null;
        _mountedW03 = null;
        _mountedW04 = null;
        _mountedW05 = null;
        foreach (var child in _zoneHost.GetChildren())
        {
            if (child is Node3D oldZone) oldZone.Visible = false;
            child.QueueFree();
        }
        _zoneHost.AddChild(world);
        _mountedW01 = world;
        ActiveZoneScenePath = W01ScenePath;

        var preserveLoadedTransform = _preserveLoadedPhotoWorldTransform
            && ReferenceEquals(_loadedPhotoWorldTransformSession, projection.SessionIdentity);
        var placeAtEntry = !preserveLoadedTransform
            && (_lastFullGameRouteSession is null
                || (sameSession && routeSceneChanged)
                || (!sameSession && bridge.CurrentZoneId == "village_day"
                    && bridge.CurrentSpawnPointId == "arrival"));
        _preserveLoadedPhotoWorldTransform = false;
        _loadedPhotoWorldTransformSession = null;
        if (placeAtEntry)
            PlacePlayerAtW01Entry(world, resolvedAnchorId);
        SetW01Ambience();

        _lastFullGameRouteSession = projection.SessionIdentity;
        _lastFullGameRouteScene = projection.ActiveSceneId;
        if (_hasLoadedInitialZone)
            PlayZoneTransition();
        _hasLoadedInitialZone = true;
        GD.Print($"fullgame-scene-loaded: {projection.ActiveSceneId} physical=W01 entry={resolvedAnchorId}");
    }

    private static string ResolveEntryAnchor(Node3D world, string entryAnchorId, string fallbackAnchorId)
    {
        if (world.GetNodeOrNull<Node3D>($"Anchors/{entryAnchorId}") is not null)
            return entryAnchorId;
        if (entryAnchorId != fallbackAnchorId
            && world.GetNodeOrNull<Node3D>($"Anchors/{fallbackAnchorId}") is not null)
        {
            GD.PushWarning($"Full-game presentation has no authored Anchors/{entryAnchorId} entry point; "
                + $"using named fallback Anchors/{fallbackAnchorId}.");
            return fallbackAnchorId;
        }
        return entryAnchorId;
    }

    private bool IsMountedZone(string zoneId)
    {
        if (EnableAct1ConnectedWorld && Act1WorldLayout.ContainsZone(zoneId))
            return _connectedWorld?.ActiveZoneId == zoneId;
        return !IsMountedW01() && !IsMountedW02() && !IsMountedW03() && !IsMountedW05()
            && ZoneScenes.TryGetValue(zoneId, out var path)
            && string.Equals(ActiveZoneScenePath, path, StringComparison.Ordinal);
    }

    private void SetW01Ambience()
    {
        if (GetTree().GetFirstNodeInGroup("ambient_audio") is not AmbientAudioDirector ambience) return;
        ambience.SetPhysicalShelter(false);
        // W01 is outdoors in the same winter pine air as the existing village bed.
        ambience.SetZone("village_day");
    }

    private bool IsMountedW01() => _mountedW01 is { } world
        && GodotObject.IsInstanceValid(world) && !world.IsQueuedForDeletion()
        && ReferenceEquals(world.GetParent(), _zoneHost);

    private void MountW02(RuntimeBridge.FullGamePhotoWorldRouteProjection projection, RuntimeBridge bridge)
    {
        const string entryAnchorId = "R00";
        var alreadyMounted = IsMountedW02();
        var routeSceneChanged = !string.Equals(_lastFullGameRouteScene, projection.ActiveSceneId, StringComparison.Ordinal);
        var sameSession = ReferenceEquals(_lastFullGameRouteSession, projection.SessionIdentity);

        if (alreadyMounted)
        {
            if (routeSceneChanged && sameSession)
            {
                var resolved = ResolveEntryAnchor(_mountedW02!, entryAnchorId, "R08");
                PlacePlayerAtW01Entry(_mountedW02!, resolved);
            }
            _preserveLoadedPhotoWorldTransform = false;
            _loadedPhotoWorldTransformSession = null;
            SetW02Ambience();
            _hasLoadedInitialZone = true;
            _lastFullGameRouteSession = projection.SessionIdentity;
            _lastFullGameRouteScene = projection.ActiveSceneId;
            return;
        }

        var packed = ResourceLoader.Load<PackedScene>(W02ScenePath);
        if (packed is null)
        {
            GD.PushError($"Full-game W02 presentation is missing: {W02ScenePath}.");
            return;
        }

        var world = packed.Instantiate<Node3D>();
        var resolvedPlacementEntryNodeId = entryAnchorId;
        if (world.GetNodeOrNull<Node3D>($"Anchors/{entryAnchorId}") is null)
        {
            const string fallbackAnchorId = "R08";
            if (world.GetNodeOrNull<Node3D>($"Anchors/{fallbackAnchorId}") is null)
            {
                world.Free();
                GD.PushError($"Full-game W02 presentation has no authored Anchors/{entryAnchorId} entry point or named fallback Anchors/{fallbackAnchorId}.");
                return;
            }

            GD.PushWarning($"Full-game W02 presentation has no authored Anchors/{entryAnchorId} entry point; "
                + $"using named fallback Anchors/{fallbackAnchorId}.");
            resolvedPlacementEntryNodeId = fallbackAnchorId;
        }

        RetireConnectedWorld();
        AlsuStreetWalkPresentation.SessionOwner(GetTree())?.DisableContactForZoneDisposal();
        _mountedW01 = null;
        _mountedW03 = null;
        _mountedW04 = null;
        _mountedW05 = null;
        foreach (var child in _zoneHost.GetChildren())
        {
            if (child is Node3D oldZone) oldZone.Visible = false;
            child.QueueFree();
        }
        _zoneHost.AddChild(world);
        _mountedW02 = world;
        ActiveZoneScenePath = W02ScenePath;

        var preserveLoadedTransform = _preserveLoadedPhotoWorldTransform
            && ReferenceEquals(_loadedPhotoWorldTransformSession, projection.SessionIdentity);
        var placeAtEntry = !preserveLoadedTransform
            && (_lastFullGameRouteSession is null
                || (sameSession && routeSceneChanged)
                || (!sameSession && bridge.CurrentZoneId == "village_day"
                    && bridge.CurrentSpawnPointId == "arrival"));
        _preserveLoadedPhotoWorldTransform = false;
        _loadedPhotoWorldTransformSession = null;
        if (placeAtEntry)
            PlacePlayerAtW01Entry(world, resolvedPlacementEntryNodeId);
        SetW02Ambience();

        _lastFullGameRouteSession = projection.SessionIdentity;
        _lastFullGameRouteScene = projection.ActiveSceneId;
        if (_hasLoadedInitialZone)
            PlayZoneTransition();
        _hasLoadedInitialZone = true;
        GD.Print($"fullgame-scene-loaded: {projection.ActiveSceneId} physical=W02 entry={resolvedPlacementEntryNodeId}");
    }

    private bool IsMountedW02() => _mountedW02 is { } world
        && GodotObject.IsInstanceValid(world) && !world.IsQueuedForDeletion()
        && ReferenceEquals(world.GetParent(), _zoneHost);

    private void SetW02Ambience()
    {
        if (GetTree().GetFirstNodeInGroup("ambient_audio") is not AmbientAudioDirector ambience) return;
        ambience.SetPhysicalShelter(false);
        // W02 is the summer bank inside the water print: the manifest's
        // water-edge bed owns this zone key.
        ambience.SetZone("fullgame_act2_river");
    }

    private void MountW03(RuntimeBridge.FullGamePhotoWorldRouteProjection projection, RuntimeBridge bridge)
    {
        const string entryAnchorId = "H00";
        const string fallbackAnchorId = "PW_HOME_PHOTO_ENTRY";
        var alreadyMounted = IsMountedW03();
        var routeSceneChanged = !string.Equals(_lastFullGameRouteScene, projection.ActiveSceneId, StringComparison.Ordinal);
        var sameSession = ReferenceEquals(_lastFullGameRouteSession, projection.SessionIdentity);

        if (alreadyMounted)
        {
            if (routeSceneChanged && sameSession)
            {
                var resolved = ResolveEntryAnchor(_mountedW03!, entryAnchorId, fallbackAnchorId);
                PlacePlayerAtW01Entry(_mountedW03!, resolved);
            }
            _preserveLoadedPhotoWorldTransform = false;
            _loadedPhotoWorldTransformSession = null;
            SetW03Ambience();
            _hasLoadedInitialZone = true;
            _lastFullGameRouteSession = projection.SessionIdentity;
            _lastFullGameRouteScene = projection.ActiveSceneId;
            return;
        }

        var packed = ResourceLoader.Load<PackedScene>(W03ScenePath);
        if (packed is null)
        {
            GD.PushError($"Full-game W03 presentation is missing: {W03ScenePath}.");
            return;
        }

        var world = packed.Instantiate<Node3D>();
        var resolvedAnchorId = ResolveEntryAnchor(world, entryAnchorId, fallbackAnchorId);
        if (world.GetNodeOrNull<Node3D>($"Anchors/{resolvedAnchorId}") is null)
        {
            world.Free();
            GD.PushError($"Full-game W03 presentation has no authored Anchors/{entryAnchorId} entry point or named fallback Anchors/{fallbackAnchorId}.");
            return;
        }

        RetireConnectedWorld();
        AlsuStreetWalkPresentation.SessionOwner(GetTree())?.DisableContactForZoneDisposal();
        _mountedW01 = null;
        _mountedW02 = null;
        _mountedW04 = null;
        _mountedW05 = null;
        foreach (var child in _zoneHost.GetChildren())
        {
            if (child is Node3D oldZone) oldZone.Visible = false;
            child.QueueFree();
        }
        _zoneHost.AddChild(world);
        _mountedW03 = world;
        ActiveZoneScenePath = W03ScenePath;

        var preserveLoadedTransform = _preserveLoadedPhotoWorldTransform
            && ReferenceEquals(_loadedPhotoWorldTransformSession, projection.SessionIdentity);
        var placeAtEntry = !preserveLoadedTransform
            && (_lastFullGameRouteSession is null
                || (sameSession && routeSceneChanged)
                || (!sameSession && bridge.CurrentZoneId == "village_day"
                    && bridge.CurrentSpawnPointId == "arrival"));
        _preserveLoadedPhotoWorldTransform = false;
        _loadedPhotoWorldTransformSession = null;
        if (placeAtEntry)
            PlacePlayerAtW01Entry(world, resolvedAnchorId);
        SetW03Ambience();

        _lastFullGameRouteSession = projection.SessionIdentity;
        _lastFullGameRouteScene = projection.ActiveSceneId;
        if (_hasLoadedInitialZone)
            PlayZoneTransition();
        _hasLoadedInitialZone = true;
        GD.Print($"fullgame-scene-loaded: {projection.ActiveSceneId} physical=W03 entry={resolvedAnchorId}");
    }

    private bool IsMountedW03() => _mountedW03 is { } world
        && GodotObject.IsInstanceValid(world) && !world.IsQueuedForDeletion()
        && ReferenceEquals(world.GetParent(), _zoneHost);

    private void SetW03Ambience()
    {
        if (GetTree().GetFirstNodeInGroup("ambient_audio") is not AmbientAudioDirector ambience) return;
        ambience.SetPhysicalShelter(false);
        // The courtyard uses the existing full-game house ambience key.
        ambience.SetZone("fullgame_act2_house");
    }

    private void MountW04(RuntimeBridge.FullGamePhotoWorldRouteProjection projection, RuntimeBridge bridge)
    {
        const string entryAnchorId = "C00";
        const string fallbackAnchorId = "PW_COMMON_ENTRY";
        var alreadyMounted = IsMountedW04();
        var routeSceneChanged = !string.Equals(_lastFullGameRouteScene, projection.ActiveSceneId, StringComparison.Ordinal);
        var sameSession = ReferenceEquals(_lastFullGameRouteSession, projection.SessionIdentity);

        if (alreadyMounted)
        {
            if (routeSceneChanged && sameSession)
            {
                var resolved = ResolveEntryAnchor(_mountedW04!, entryAnchorId, fallbackAnchorId);
                PlacePlayerAtW01Entry(_mountedW04!, resolved);
            }
            _preserveLoadedPhotoWorldTransform = false;
            _loadedPhotoWorldTransformSession = null;
            SetW04Ambience();
            _hasLoadedInitialZone = true;
            _lastFullGameRouteSession = projection.SessionIdentity;
            _lastFullGameRouteScene = projection.ActiveSceneId;
            return;
        }

        var packed = ResourceLoader.Load<PackedScene>(W04ScenePath);
        if (packed is null)
        {
            GD.PushError($"Full-game W04 presentation is missing: {W04ScenePath}.");
            return;
        }

        var world = packed.Instantiate<Node3D>();
        var resolvedAnchorId = ResolveEntryAnchor(world, entryAnchorId, fallbackAnchorId);
        if (world.GetNodeOrNull<Node3D>($"Anchors/{resolvedAnchorId}") is null)
        {
            world.Free();
            GD.PushError($"Full-game W04 presentation has no authored Anchors/{entryAnchorId} entry point or named fallback Anchors/{fallbackAnchorId}.");
            return;
        }

        RetireConnectedWorld();
        AlsuStreetWalkPresentation.SessionOwner(GetTree())?.DisableContactForZoneDisposal();
        _mountedW01 = null;
        _mountedW02 = null;
        _mountedW03 = null;
        _mountedW05 = null;
        foreach (var child in _zoneHost.GetChildren())
        {
            if (child is Node3D oldZone) oldZone.Visible = false;
            child.QueueFree();
        }
        _zoneHost.AddChild(world);
        _mountedW04 = world;
        ActiveZoneScenePath = W04ScenePath;

        var preserveLoadedTransform = _preserveLoadedPhotoWorldTransform
            && ReferenceEquals(_loadedPhotoWorldTransformSession, projection.SessionIdentity);
        var placeAtEntry = !preserveLoadedTransform
            && (_lastFullGameRouteSession is null
                || (sameSession && routeSceneChanged)
                || (!sameSession && bridge.CurrentZoneId == "village_day"
                    && bridge.CurrentSpawnPointId == "arrival"));
        _preserveLoadedPhotoWorldTransform = false;
        _loadedPhotoWorldTransformSession = null;
        if (placeAtEntry)
            PlacePlayerAtW01Entry(world, resolvedAnchorId);
        SetW04Ambience();

        _lastFullGameRouteSession = projection.SessionIdentity;
        _lastFullGameRouteScene = projection.ActiveSceneId;
        if (_hasLoadedInitialZone)
            PlayZoneTransition();
        _hasLoadedInitialZone = true;
        GD.Print($"fullgame-scene-loaded: {projection.ActiveSceneId} physical=W04 entry={resolvedAnchorId}");
    }

    private bool IsMountedW04() => _mountedW04 is { } world
        && GodotObject.IsInstanceValid(world) && !world.IsQueuedForDeletion()
        && ReferenceEquals(world.GetParent(), _zoneHost);

    private void SetW04Ambience()
    {
        if (GetTree().GetFirstNodeInGroup("ambient_audio") is not AmbientAudioDirector ambience) return;
        ambience.SetPhysicalShelter(false);
        // Reuse the quiet outdoor bed until the dedicated common-frame audio is accepted.
        ambience.SetZone("village_day");
    }

    private void MountW05(RuntimeBridge.FullGamePhotoWorldRouteProjection projection, RuntimeBridge bridge)
    {
        const string entryAnchorId = "B00";
        const string fallbackAnchorId = "PW_CELLAR_ENTRY";
        var alreadyMounted = IsMountedW05();
        var routeSceneChanged = !string.Equals(_lastFullGameRouteScene, projection.ActiveSceneId, StringComparison.Ordinal);
        var sameSession = ReferenceEquals(_lastFullGameRouteSession, projection.SessionIdentity);

        if (alreadyMounted)
        {
            if (routeSceneChanged && sameSession)
            {
                var resolved = ResolveEntryAnchor(_mountedW05!, entryAnchorId, fallbackAnchorId);
                PlacePlayerAtW01Entry(_mountedW05!, resolved);
            }
            _preserveLoadedPhotoWorldTransform = false;
            _loadedPhotoWorldTransformSession = null;
            SetW05Ambience();
            _hasLoadedInitialZone = true;
            _lastFullGameRouteSession = projection.SessionIdentity;
            _lastFullGameRouteScene = projection.ActiveSceneId;
            return;
        }

        var packed = ResourceLoader.Load<PackedScene>(W05ScenePath);
        if (packed is null)
        {
            GD.PushError($"Full-game W05 presentation is missing: {W05ScenePath}.");
            return;
        }

        var world = packed.Instantiate<Node3D>();
        var resolvedAnchorId = ResolveEntryAnchor(world, entryAnchorId, fallbackAnchorId);
        if (world.GetNodeOrNull<Node3D>($"Anchors/{resolvedAnchorId}") is null)
        {
            world.Free();
            GD.PushError($"Full-game W05 presentation has no authored Anchors/{entryAnchorId} entry point or named fallback Anchors/{fallbackAnchorId}.");
            return;
        }

        RetireConnectedWorld();
        AlsuStreetWalkPresentation.SessionOwner(GetTree())?.DisableContactForZoneDisposal();
        _mountedW01 = null;
        _mountedW02 = null;
        _mountedW03 = null;
        _mountedW04 = null;
        foreach (var child in _zoneHost.GetChildren())
        {
            if (child is Node3D oldZone) oldZone.Visible = false;
            child.QueueFree();
        }
        _zoneHost.AddChild(world);
        _mountedW05 = world;
        ActiveZoneScenePath = W05ScenePath;

        var preserveLoadedTransform = _preserveLoadedPhotoWorldTransform
            && ReferenceEquals(_loadedPhotoWorldTransformSession, projection.SessionIdentity);
        var placeAtEntry = !preserveLoadedTransform
            && (_lastFullGameRouteSession is null
                || (sameSession && routeSceneChanged)
                || (!sameSession && bridge.CurrentZoneId == "village_day"
                    && bridge.CurrentSpawnPointId == "arrival"));
        _preserveLoadedPhotoWorldTransform = false;
        _loadedPhotoWorldTransformSession = null;
        if (placeAtEntry)
            PlacePlayerAtW01Entry(world, resolvedAnchorId);
        SetW05Ambience();

        _lastFullGameRouteSession = projection.SessionIdentity;
        _lastFullGameRouteScene = projection.ActiveSceneId;
        if (_hasLoadedInitialZone)
            PlayZoneTransition();
        _hasLoadedInitialZone = true;
        GD.Print($"fullgame-scene-loaded: {projection.ActiveSceneId} physical=W05 entry={resolvedAnchorId}");
    }

    private bool IsMountedW05() => _mountedW05 is { } world
        && GodotObject.IsInstanceValid(world) && !world.IsQueuedForDeletion()
        && ReferenceEquals(world.GetParent(), _zoneHost);

    private void SetW05Ambience()
    {
        if (GetTree().GetFirstNodeInGroup("ambient_audio") is not AmbientAudioDirector ambience) return;
        ambience.SetPhysicalShelter(true);
        // W05 is an indoor cellar; reuse the existing warm sheltered house bed.
        ambience.SetZone("fullgame_act2_house");
    }

    private void PlacePlayerAtW01Entry(Node3D world, string anchorId)
    {
        if (world.GetNodeOrNull<Node3D>($"Anchors/{anchorId}") is not { } anchor)
        {
            GD.PushError($"Full-game W01 presentation has no authored Anchors/{anchorId} entry point.");
            return;
        }
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            player.ApplyZoneSpawn(anchor.GlobalPosition, anchor.GlobalRotationDegrees.Y);
            LastSpawnYawDegrees = anchor.GlobalRotationDegrees.Y;
        }
    }

    private void BuildZoneTransitionOverlay()
    {
        var layer = new CanvasLayer
        {
            Name = "Act1ZoneTransition",
            Layer = 80
        };
        layer.AddToGroup("act1_demo_transition");
        AddChild(layer);
        _zoneTransition = new ColorRect
        {
            Name = "Fade",
            Color = new Color(0.008f, 0.012f, 0.011f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        layer.AddChild(_zoneTransition);
    }

    private void PlayZoneTransition()
    {
        if (_zoneTransition is null || !GodotObject.IsInstanceValid(_zoneTransition))
        {
            return;
        }

        // UIUX-010: reduced motion replaces the animated transition with an
        // instant cut; the overlay clears immediately.
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ReducedMotion: true })
        {
            _zoneTransitionTween?.Kill();
            _zoneTransitionTween = null;
            _zoneTransition.Color = new Color(0.008f, 0.012f, 0.011f, 0f);
            return;
        }

        _zoneTransitionTween?.Kill();
        _zoneTransition.Color = new Color(0.008f, 0.012f, 0.011f, 0.68f);
        _zoneTransitionTween = CreateTween();
        _zoneTransitionTween.TweenProperty(
            _zoneTransition,
            "color",
            new Color(0.008f, 0.012f, 0.011f, 0.18f),
            0.12f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _zoneTransitionTween.TweenProperty(
            _zoneTransition,
            "color",
            new Color(0.008f, 0.012f, 0.011f, 0f),
            0.42f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    private readonly record struct SpawnTransformData(Vector3 Position, float YawDegrees);

    private static SpawnTransformData SpawnTransform(string zoneId, string spawnPointId) => (zoneId, spawnPointId) switch
    {
        // Godot yaw 0 faces -Z. These Act 1 destinations deliberately face
        // the next route landmark rather than preserving the doorway-facing
        // direction from the previous compact zone.
        // The connected world's house door (x≈-26) lies beyond this compact
        // street's 42 m ground, so a player spawned there fell until recovered.
        // Leave the house from the street's own west porch step instead, facing
        // the street (+Z) through the gap between the step and the x=-8.8 fence.
        ("village_day", "from_house") => new(new Vector3(-9.15f, 0.05f, -0.3f), 180f),
        ("village_day", "from_forest") => new(new Vector3(1.8f, 0.05f, -12.5f), 180f),
        // Review spawn in front of the village mosque gate (debug zone jump).
        ("village_day", "mosque") => new(new Vector3(-40f, 0.05f, -34f), 265f),
        ("house_old_pc", _) => new(new Vector3(0, 0.05f, 3.8f), 0f),
        ("fap_clinic", _) => new(new Vector3(0, 0.05f, 4.8f), 0f),
        ("zirat_road", _) => new(new Vector3(0, 0.05f, 16.5f), 0f),
        ("kara_urman_night", _) => new(new Vector3(0, 0.05f, 12), 0f),
        ("fullgame_act2_house", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act2_river", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act2_mosque", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act2_council", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act3_archive", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act3_soviet", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act3_water", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act4_tukay", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act4_1552", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act4_pact", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act5_boundary", _) => new(new Vector3(0, 0.05f, 8), 0f),
        ("fullgame_act5_epilogue", _) => new(new Vector3(0, 0.05f, 8), 0f),
        _ => new(new Vector3(0, 0.05f, 9), 0f)
    };
}
