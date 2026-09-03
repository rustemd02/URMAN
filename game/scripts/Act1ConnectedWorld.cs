using System.Globalization;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Presentation-only owner for the connected Chapter 1 greybox. It builds the
/// five existing logical zone scenes once, gives them deterministic world
/// placements, and adds non-interactive connective framing. RuntimeBridge
/// remains the only owner of narrative, vocabulary and save state.
/// </summary>
public partial class Act1ConnectedWorld : Node3D
{
    private const string VillageExteriorKitScenePath =
        "res://assets/models/act1/urman_village_exterior_kit.glb";
    private const string VillageExteriorKitRootName = "URMAN_VillageExteriorKit";

    private static readonly string[] VillageExteriorKitComponentNames =
    [
        "DwellingFacade_TimberPlaster",
        "OutbuildingShed_Low",
        "FenceSegment_RoughPicket",
        "Gate_CrookedTimber",
        "Woodpile_StackedLogs",
        "Well_YardLandmark"
    ];

    private sealed record Act1ExteriorParcelComponentPlacement(
        string ComponentName,
        string PlacementName,
        Vector3 LocalAnchor,
        float YawDegrees,
        Vector3 Scale,
        string LogicalAnchor);

    private const string KaraForestEdgeKitScenePath =
        "res://assets/models/act1/urman_kara_forest_edge_kit.glb";
    private const string KaraForestEdgeKitRootName = "URMAN_KaraForestEdgeKit";

    private static readonly string[] KaraForestEdgeKitComponentNames =
    [
        "ForestBank_Left",
        "ForestBank_Right",
        "MixedTreeCluster_Left",
        "MixedTreeCluster_Right",
        "CrookedPineMass",
        "BirchEdgeMass",
        "RootWall_Left",
        "RootWall_Right",
        "FallenLogCluster",
        "MossyBoulderCluster",
        "CrookedStump",
        "DistantForestMass_Low",
        "DistantForestMass_Tall"
    ];

    private const string ZiratRoadsideKitScenePath =
        "res://assets/models/act1/urman_zirat_roadside_kit.glb";
    private const string ZiratRoadsideKitRootName = "URMAN_ZiratRoadsideKit";

    private static readonly string[] ZiratRoadsideKitComponentNames =
    [
        "WetRoadShoulder_Left",
        "WetRoadShoulder_Right",
        "RoadsideDitch",
        "CulvertStoneCluster",
        "ZiratBoundaryFence",
        "ZiratOpenGate",
        "ZiratMarkerGroup_Low",
        "ZiratMarkerGroup_Far",
        "ZiratPathEdge",
        "ZiratBirchShrubMass",
        "ZiratDistantVillageMass"
    ];

    private const string FapClinicKitScenePath =
        "res://assets/models/act1/urman_fap_clinic_kit.glb";
    private const string FapClinicKitRootName = "URMAN_FapClinicKit";

    private static readonly string[] FapClinicKitComponentNames =
    [
        "FapFacade_Main",
        "FapEntryPorch",
        "FapWayfindingBoard",
        "FapServiceShed",
        "FapFenceRun",
        "FapGate",
        "FapBench",
        "FapNoticeBoard",
        "FapRainAwning",
        "FapPathPuddleCluster",
        "FapBirchShrubMass"
    ];

    private const string VillageLandmarkKitScenePath =
        "res://assets/generated/urman_act1_village_landmark_kit.glb";
    private const string VillageLandmarkKitRootName = "URMAN_Act1_VillageLandmarkKit";

    private static readonly string[] VillageLandmarkGroupNames =
    [
        "Arrival",
        "VillageStreet",
        "BabaiYard",
        "HouseExterior",
        "ConnectiveStreet",
        "FapExterior",
        "ReturnStreet",
        "ZiratBoundary",
        "KaraApproach"
    ];

    private const string WetVillageRoadKitScenePath =
        "res://assets/models/act1/urman_wet_village_road_kit.glb";
    private const string WetVillageRoadKitRootName = "URMAN_WetVillageRoadKit";

    private static readonly string[] WetVillageRoadKitComponentNames =
    [
        "RoadCrown_SunkenWet",
        "RoadRuts_PuddleNear",
        "RoadRuts_PuddleFar",
        "MuddyShoulder_Left",
        "MuddyShoulder_Right",
        "RoadsideDitch_Left",
        "RoadsideDitch_Right",
        "CulvertStoneCrossing",
        "GrassSedgeMass_Left",
        "GrassSedgeMass_Right",
        "FernShrubBreak_Left",
        "FernShrubBreak_Right",
        "RoadFenceBreak_Low",
        "RoadCrown_BranchWet",
        "RoadCrown_ApproachWorn"
    ];

    private static readonly (string RelativePath, string Reason)[] ConnectedWorldPresentationSuppressions =
    [
        // Babai yard: preserve Act1CoreWorldGreybox, house_old_pc floor/path,
        // existing InteractionTarget nodes, RuntimeBridge state and spawns.
        ("Act1AuthoredOutdoorBackbone/HouseYardToolCanopy", "legacy Babai yard tool canopy occludes the house approach"),
        ("HouseA_Act1Exterior", "legacy generated house roof/facade duplicates the core house approach"),
        ("URMAN_Act1_VillageLandmarkKit/BabaiYard", "authored Babai yard presentation group duplicates the core yard"),
        ("URMAN_Act1_VillageLandmarkKit/HouseExterior", "authored house-exterior presentation group duplicates the core house approach"),

        // Connective/return: preserve Act1CoreWorldGreybox, Act1Connectors
        // traversal collision, route floors/paths, InteractionTarget nodes,
        // RuntimeBridge state and spawns.
        ("URMAN_Act1_VillageLandmarkKit/ConnectiveStreet", "legacy connective-street dressing occludes the shared return view"),
        ("URMAN_Act1_VillageLandmarkKit/ReturnStreet", "legacy return-street dressing occludes the shared return view"),
        ("ReturnFarHouseWest", "legacy return distant house duplicates the core village horizon mass"),
        ("ReturnFarHouseEast", "legacy return distant house duplicates the core village horizon mass"),

        // FAP/Kara: preserve their logical zone floors/paths, connector
        // collision, InteractionTarget nodes, RuntimeBridge state and spawns.
        ("URMAN_Act1_VillageLandmarkKit/FapExterior", "legacy FAP tree/building group occludes the core FAP exterior"),
        ("URMAN_Act1_VillageLandmarkKit/KaraApproach", "legacy Kara approach dressing occludes the core forest edge"),

        // Zirat: preserve the road, boundary and interaction owners; remove
        // only the proven overhead header, not the gate posts or route.
        ("Act1AuthoredOutdoorBackbone/ZiratApproachOpenGate/GateHeader", "legacy zirat gate header creates an overhead occluder")
    ];

    private sealed record EnvironmentBinding(
        WorldEnvironment Node,
        global::Godot.Environment Resource);

    private sealed record InteractionBinding(
        InteractionTarget Node,
        uint CollisionLayer,
        uint CollisionMask);

    private enum VegetationStyle
    {
        Conifer,
        Birch,
        Broadleaf,
        Shrub
    }

    private readonly Dictionary<string, Node3D> _zoneInstances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<EnvironmentBinding>> _environmentsByZone = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<Light3D>> _lightsByZone = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<InteractionBinding>> _interactionsByZone = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Node3D> _interiorLandmarksByZone = new(StringComparer.Ordinal);
    private RuntimeBridge? _runtimeBridge;
    private bool _runtimeBridgeSubscribed;
    private bool _logicalZonePresentationSuppressionsReapplied;
    private bool _built;

    public IReadOnlyDictionary<string, Node3D> ZoneInstances => _zoneInstances;

    public IReadOnlyList<string> PlacementIds => Act1WorldLayout.PlacementIds;

    public int LogicalZoneInstanceCount => _zoneInstances.Count;

    public int PlacementCount => Act1WorldLayout.Placements.Count;

    public int ConnectorCount { get; private set; }

    // Kept as a diagnostic compatibility property; the connected traversal
    // surfaces are deliberately collidable even though the dressing around
    // them remains visual-only.
    public bool ConnectorsAreVisualOnly => false;

    public bool ConnectorTraversalSurfacesAreCollidable => true;

    public string VisualOnlyPolicy => Act1WorldLayout.VisualOnlyPolicy;

    public string PersistentInstanceIdentity { get; private set; } = string.Empty;

    public string ActiveZoneId { get; private set; } = string.Empty;

    public bool IsBuilt => _built;

    public int ActiveDirectionalLightCount { get; private set; }

    public int ActiveLightCount { get; private set; }

    public int ActiveInteractionTargetCount { get; private set; }

    public int ConnectorTraversalCollisionCount { get; private set; }

    public override void _Ready()
    {
        AddToGroup("act1_connected_world");
        Build();
    }

    public override void _ExitTree()
    {
        if (_runtimeBridgeSubscribed && _runtimeBridge is not null && GodotObject.IsInstanceValid(_runtimeBridge))
        {
            _runtimeBridge.RuntimeStateChanged -= OnRuntimeStateChanged;
        }

        _runtimeBridge = null;
        _runtimeBridgeSubscribed = false;
    }

    public void Build()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        PersistentInstanceIdentity = GetInstanceId().ToString(CultureInfo.InvariantCulture);
        SetMeta("worldOwner", nameof(Act1ConnectedWorld));
        SetMeta("persistentInstanceIdentity", PersistentInstanceIdentity);
        SetMeta("persistentPolicy", "built-once; Main.SwitchZone never frees this node");
        SetMeta("visualOnlyPolicy", VisualOnlyPolicy);
        SetMeta("worldSpacePlacementPolicy", "fixed deterministic child origins; connected root remains stable across logical transitions");

        foreach (var placement in Act1WorldLayout.Placements)
        {
            var packed = ResourceLoader.Load<PackedScene>(placement.ScenePath)
                ?? throw new InvalidOperationException($"Connected Act I zone is missing: {placement.ScenePath}.");
            var zone = packed.Instantiate<Node3D>()
                ?? throw new InvalidOperationException($"Connected Act I zone did not instantiate: {placement.ZoneId}.");

            zone.Name = placement.PlacementId;
            zone.Position = placement.Origin;
            zone.SetMeta("logicalZoneId", placement.ZoneId);
            zone.SetMeta("placementId", placement.PlacementId);
            zone.SetMeta("scenePath", placement.ScenePath);
            zone.SetMeta("worldOrigin", placement.Origin);
            zone.SetMeta("presentationOwner", nameof(Act1ConnectedWorld));
            zone.SetMeta("interactionOwnership", "existing InteractionTarget nodes");
            zone.SetMeta("runtimeStateOwnership", "RuntimeBridge");
            AddChild(zone);
            _zoneInstances.Add(placement.ZoneId, zone);

            ApplyLogicalZonePresentationSuppressions(zone, placement.ZoneId);

            var environments = FindDescendants<WorldEnvironment>(zone)
                .Select(environment => new EnvironmentBinding(
                    environment,
                    environment.Environment
                        ?? throw new InvalidOperationException($"Act I zone has an empty WorldEnvironment: {placement.ZoneId}.")))
                .ToArray();
            _environmentsByZone.Add(placement.ZoneId, environments);
            _lightsByZone.Add(
                placement.ZoneId,
                FindDescendants<Light3D>(zone)
                    .Where(light => !light.HasMeta("connectedWorldHidden"))
                    .ToArray());

            _interactionsByZone.Add(
                placement.ZoneId,
                FindDescendants<InteractionTarget>(zone)
                    .Where(target => !target.HasMeta("connectedWorldHidden"))
                    .Select(target => new InteractionBinding(
                        target,
                        // Existing StyleBenchmarkZone interaction bodies use
                        // Godot's default layer 1. An unavailable target has
                        // already been presentation-gated by InteractionTarget
                        // before this owner sees it, so retain that authored
                        // default for restoration when its state becomes valid.
                        target.CollisionLayer == 0 ? 1u : target.CollisionLayer,
                        target.CollisionMask))
                    .ToArray());
        }

        BuildConnectorPresentation();
        // Keep the legacy framing builders available as rollback/source code,
        // but do not materialize their global presentation root in the main
        // connected-world path. Act1CoreWorldGreybox is the sole global visual
        // owner; route, collision, interaction and RuntimeBridge owners above
        // remain unchanged.
        BuildAct1CoreWorldGreybox();
        AttachRuntimeBridge();
        ConnectorCount = Act1WorldLayout.Connectors.Count;
        SetMeta("logicalZoneInstanceCount", LogicalZoneInstanceCount);
        SetMeta("placementCount", PlacementCount);
        SetMeta("placementIds", string.Join('|', PlacementIds));
        SetMeta("zoneIds", string.Join('|', Act1WorldLayout.ZoneIds));
        SetMeta("zoneInstanceIds", string.Join('|', _zoneInstances.Values.Select(
            instance => instance.GetInstanceId().ToString(CultureInfo.InvariantCulture))));
        SetMeta("connectorCount", ConnectorCount);
        SetMeta("connectorIds", string.Join('|', Act1WorldLayout.Connectors.Select(connector => connector.ConnectorId)));
        SetMeta("connectorsVisualOnly", ConnectorsAreVisualOnly);
        SetMeta("connectorTraversalSurfacesCollidable", ConnectorTraversalSurfacesAreCollidable);
        SetMeta("connectorTraversalCollisionCount", ConnectorTraversalCollisionCount);
        SetMeta("activeZoneId", string.Empty);
        SetMeta("activeWorldEnvironmentCount", 0);
        SetMeta("activeDirectionalLightCount", 0);
        SetMeta("activeLightCount", 0);
        SetMeta("activeInteractionTargetCount", 0);

        SetActiveLogicalZone("village_day");
        CallDeferred(nameof(ReapplyLogicalZonePresentationSuppressions));
    }

    public bool ContainsZone(string zoneId) => _zoneInstances.ContainsKey(zoneId);

    public Node3D? GetZoneInstance(string zoneId) =>
        _zoneInstances.TryGetValue(zoneId, out var instance) ? instance : null;

    public bool TryGetWorldSpawn(
        string zoneId,
        string spawnPointId,
        out Act1WorldLayout.SpawnTransform worldSpawn)
    {
        worldSpawn = default;
        if (!Act1WorldLayout.TryGetWorldSpawn(zoneId, spawnPointId, out var rootRelativeSpawn))
        {
            return false;
        }

        // The layout is expressed relative to this persistent root. The root
        // itself never moves, so this is a deterministic world-space mapping
        // rather than a logical-zone replacement or rebase.
        worldSpawn = new(ToGlobal(rootRelativeSpawn.Position), rootRelativeSpawn.YawDegrees);
        return true;
    }

    public void SetActiveLogicalZone(string zoneId)
    {
        if (!_built)
        {
            Build();
        }

        if (!_zoneInstances.ContainsKey(zoneId))
        {
            throw new InvalidOperationException($"Connected Act I world has no logical zone '{zoneId}'.");
        }

        if (!Act1WorldLayout.TryGetPlacement(zoneId, out var activePlacement))
        {
            throw new InvalidOperationException($"Connected Act I world has no layout placement for '{zoneId}'.");
        }

        var useExteriorAtmosphere = !activePlacement.Interior;

        foreach (var (candidateZoneId, bindings) in _environmentsByZone)
        {
            var isActive = !useExteriorAtmosphere
                && string.Equals(candidateZoneId, zoneId, StringComparison.Ordinal);
            foreach (var binding in bindings)
            {
                // WorldEnvironment has no enabled flag. Clearing the resource
                // is the Godot presentation seam that leaves exactly one
                // environment active while retaining each zone instance.
                binding.Node.Environment = isActive ? binding.Resource : null;
            }
        }

        ActiveDirectionalLightCount = 0;
        ActiveLightCount = 0;
        foreach (var (candidateZoneId, lights) in _lightsByZone)
        {
            var isActive = string.Equals(candidateZoneId, zoneId, StringComparison.Ordinal);
            foreach (var light in lights)
            {
                // The Agent B exterior layer owns the single outdoor sun.
                // Keep authored local omni/point lights for windows and
                // interior accents, but never stack a second directional sun.
                var enabled = isActive
                    && (!useExteriorAtmosphere || light is not DirectionalLight3D);
                light.Visible = enabled;
                if (enabled)
                {
                    ActiveLightCount++;
                    if (light is DirectionalLight3D)
                    {
                        ActiveDirectionalLightCount++;
                    }
                }
            }
        }

        var exteriorLayer = GetNodeOrNull<AgentBAct1ExteriorLayer>(
            "Act1CoreWorldGreybox/AgentBExteriorWorld");
        exteriorLayer?.SetExteriorPresentationEnabled(
            useExteriorAtmosphere,
            string.Equals(zoneId, "kara_urman_night", StringComparison.Ordinal));
        var coreWorld = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox");
        if (coreWorld is not null)
        {
            coreWorld.Visible = useExteriorAtmosphere;
        }

        ActiveZoneId = zoneId;
        SetMeta("activeZoneId", ActiveZoneId);
        SetMeta(
            "activeWorldEnvironmentCount",
            useExteriorAtmosphere ? 0 : _environmentsByZone[zoneId].Count);
        SetMeta("activeExteriorAtmosphere", useExteriorAtmosphere);
        SetMeta(
            "activeAtmosphereOwner",
            useExteriorAtmosphere ? "AgentBExteriorWorld" : $"logical-zone/{zoneId}");
        SetMeta("activeDirectionalLightCount", ActiveDirectionalLightCount);
        SetMeta("activeLightCount", ActiveLightCount);
        foreach (var placement in Act1WorldLayout.Placements)
        {
            if (_zoneInstances.TryGetValue(placement.ZoneId, out var instance))
            {
                var isActive = string.Equals(placement.ZoneId, zoneId, StringComparison.Ordinal);
                // Exterior zones stay visible as continuity framing. Interior
                // roots are visibility-gated so an inactive house/FAP cannot
                // leak into an exterior presentation.
                instance.Visible = !placement.Interior || isActive;
                if (placement.Interior)
                {
                    // Visibility does not remove physics bodies. Keep the
                    // active interior authoritative, but make its inactive
                    // floor/walls inert so they cannot block the exterior
                    // route. InteractionTarget bodies are restored by the
                    // separate interaction-routing pass below.
                    foreach (var body in FindDescendants<StaticBody3D>(instance)
                        .Where(candidate => candidate is not InteractionTarget))
                    {
                        if (!body.HasMeta("connectedWorldOriginalCollisionLayer"))
                        {
                            body.SetMeta("connectedWorldOriginalCollisionLayer", (int)body.CollisionLayer);
                            body.SetMeta("connectedWorldOriginalCollisionMask", (int)body.CollisionMask);
                        }

                        body.CollisionLayer = isActive
                            ? (uint)body.GetMeta("connectedWorldOriginalCollisionLayer").AsInt32()
                            : 0u;
                        body.CollisionMask = isActive
                            ? (uint)body.GetMeta("connectedWorldOriginalCollisionMask").AsInt32()
                            : 0u;
                    }
                }
                if (string.Equals(placement.PlacementId, "village-main-road", StringComparison.Ordinal))
                {
                    // This legacy StyleBenchmarkZone still owns the live
                    // interaction targets and traversal collision, but its
                    // procedural presentation is not part of the production
                    // envelope. Act1CoreWorldGreybox owns the visible road;
                    // keeping the node mounted preserves gameplay ownership.
                    instance.Visible = false;
                    instance.SetMeta("legacyPresentationSuppressed", true);
                }
                instance.SetMeta("activePresentation", isActive);
                instance.SetMeta("visiblePresentation", instance.Visible);
            }

            if (_interiorLandmarksByZone.TryGetValue(placement.ZoneId, out var landmark))
            {
                landmark.Visible = placement.Interior
                    && !string.Equals(placement.ZoneId, zoneId, StringComparison.Ordinal);
            }
        }

        ApplyInteractionRouting();
    }

    private void AttachRuntimeBridge()
    {
        if (_runtimeBridgeSubscribed)
        {
            return;
        }

        if (GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge)
        {
            return;
        }

        _runtimeBridge = bridge;
        _runtimeBridge.RuntimeStateChanged += OnRuntimeStateChanged;
        _runtimeBridgeSubscribed = true;
    }

    private void OnRuntimeStateChanged() => ApplyInteractionRouting();

    private void ApplyInteractionRouting()
    {
        ActiveInteractionTargetCount = 0;
        foreach (var (candidateZoneId, bindings) in _interactionsByZone)
        {
            var isActiveZone = string.Equals(candidateZoneId, ActiveZoneId, StringComparison.Ordinal);
            foreach (var binding in bindings)
            {
                var enabled = isActiveZone && binding.Node.IsAvailable();
                binding.Node.CollisionLayer = enabled ? binding.CollisionLayer : 0;
                binding.Node.CollisionMask = enabled ? binding.CollisionMask : 0;
                if (enabled)
                {
                    ActiveInteractionTargetCount++;
                }
            }
        }

        SetMeta("activeInteractionTargetCount", ActiveInteractionTargetCount);
    }

    private static void ApplyLogicalZonePresentationSuppressions(Node zone, string zoneId)
    {
        if (!GodotObject.IsInstanceValid(zone))
        {
            return;
        }

        // A few benchmark-only props are useful in their isolated camera
        // studies but become accidental route blockers or floating lights
        // when the five scenes share one exterior. Keep the source scenes
        // intact and suppress only those presentation artifacts here.
        if (string.Equals(zoneId, "zirat_road", StringComparison.Ordinal))
        {
            HidePresentationNodes(zone, "ZiratFence");
            // The isolated zirat study adds an 18-tree alternating pine row.
            // The connected world already owns the cemetery boundary and
            // transition silhouettes; keeping the study row makes the route
            // read like a repeated cone benchmark instead of a quiet authored
            // place. Tree bodies are presentation-only despite their
            // decorative collision shapes, so suppress them structurally.
            HideBenchmarkTreeBodies(zone);
        }
        else if (string.Equals(zoneId, "village_day", StringComparison.Ordinal))
        {
            // Keep the compiled VillageSign interaction target, but remove
            // its isolated benchmark board/post from the diagonal return
            // view; the connected composition supplies the readable FAP
            // and house landmarks instead.
            HidePresentationNodes(
                zone,
                "VillageSignPost",
                "VillageSignBoard",
                "VillageSignArrow",
                "VillageSignText",
                "Pine",
                "Birch",
                "UtilityPole",
                "UtilityCable",
                // The connected world keeps the legacy StyleBenchmarkZone
                // mounted for its interaction targets and traversal ground,
                // but its benchmark houses/fences are hidden presentation
                // geometry. Suppress their direct-child colliders as well;
                // otherwise they remain invisible blockers across the
                // authored Agent B house approach.
                "House",
                "Foundation",
                "Fence");
        }
        else if (string.Equals(zoneId, "kara_urman_night", StringComparison.Ordinal))
        {
            HidePresentationNodes(
                zone,
                "DistantWindow",
                "DistantWarmWindow",
                "Pine",
                "Birch",
                "BoundaryMarker",
                "BoundaryPostLeft",
                "BoundaryPostRight",
                "BoundaryThread",
                "BoundaryRibbonLeft",
                "BoundaryRibbonRight",
                "ForestBranchSilhouette",
                "Act2Continuation",
                // The project-original Kara pine module is re-anchored
                // by its benchmark scene into the FAP branch envelope
                // when all exterior zones coexist. First-person audit
                // proved this presentation-only module occludes the
                // branch; the core layer supplies the replacement edge
                // silhouettes. Keep the suppression narrow and leave
                // the authored Kara scene itself loaded.
                "GeneratedModularKit",
                "GeneratedPineA_MidLeft",
                "GeneratedPineA_NearRight");
            HideBenchmarkTreeBodies(zone);
        }
    }

    private void ReapplyLogicalZonePresentationSuppressions()
    {
        if (_logicalZonePresentationSuppressionsReapplied)
        {
            return;
        }

        _logicalZonePresentationSuppressionsReapplied = true;
        foreach (var placement in Act1WorldLayout.Placements)
        {
            if (_zoneInstances.TryGetValue(placement.ZoneId, out var zone)
                && GodotObject.IsInstanceValid(zone))
            {
                ApplyLogicalZonePresentationSuppressions(zone, placement.ZoneId);
            }
        }

        SetMeta("logicalZonePresentationSuppressionsReapplied", true);
    }

    private void BuildConnectorPresentation()
    {
        var connectors = new Node3D { Name = "Act1Connectors" };
        connectors.SetMeta("ownership", nameof(Act1ConnectedWorld));
        connectors.SetMeta("visualOnly", false);
        connectors.SetMeta("collisionOwner", nameof(Act1ConnectedWorld));
        connectors.SetMeta("interactionOwner", "none");
        connectors.SetMeta("runtimeStateOwner", "none");
        connectors.SetMeta(
            "surfacePolicy",
            "SharedVillageGround and RoadSurface own traversal collision; shoulders, fences and framing are visual-only");
        AddChild(connectors);

        AddVisualBox(
            connectors,
            "SharedVillageGround",
            new Vector3(86f, 0.12f, 208f),
            new Vector3(0f, -0.12f, -48f),
            "4d5545",
            "earth");
        AddTraversalBox(
            connectors,
            "SharedVillageGroundTraversalCollision",
            new Vector3(86f, 0.24f, 208f),
            new Vector3(0f, -0.12f, -48f));
        ConnectorTraversalCollisionCount = 1;

        foreach (var connector in Act1WorldLayout.Connectors)
        {
            var segment = new Node3D { Name = connector.ConnectorId };
            segment.SetMeta("visualOnly", false);
            segment.SetMeta("collisionOwner", nameof(Act1ConnectedWorld));
            segment.SetMeta("interactionOwner", "none");
            segment.SetMeta("start", connector.Start);
            segment.SetMeta("end", connector.End);
            segment.SetMeta("traversalSurface", "RoadSurface");
            segment.SetMeta("dressingPolicy", "shoulders and parcel framing are visual-only; route center remains open");
            connectors.AddChild(segment);

            AddVisualStrip(segment, "RoadSurface", connector, connector.Width, 0.026f, "earth");
            // Agent B's authored Road_Main/Road_* meshes provide the visible
            // crown and grade. Keep this legacy strip as the named traversal
            // contract, but hide only its presentation mesh so it cannot
            // flatten the road into a second box over the authored terrain.
            HidePresentationNodes(segment, "RoadSurface");
            segment.SetMeta("roadSurfacePresentation", "suppressed; AgentB_TerrainRoadKit owns visible crown");
            AddTraversalStrip(segment, connector, connector.Width);
            ConnectorTraversalCollisionCount++;
        }
    }

    private void BuildVillageFraming()
    {
        var framing = new Node3D { Name = "Act1DistantVillageFraming" };
        framing.SetMeta("visualOnly", true);
        framing.SetMeta("collisionOwner", "none");
        framing.SetMeta("interactionOwner", "none");
        framing.SetMeta("purpose", "continuous village silhouette, readable landmarks and forest-edge framing");
        framing.SetMeta("compositionPolicy", "near parcel edges, mid village masses, far varied forest line; route center remains open");
        AddChild(framing);

        BuildAuthoredOutdoorBackbone(framing);
        BuildArrivalAndVillageFraming(framing);
        BuildHouseExterior(framing);
        BuildFapExterior(framing);
        BuildZiratExterior(framing);
        BuildKaraEdgeFraming(framing);
        BuildVillageLandmarkKit(framing);
        BuildAct1VillageInfrastructure(framing);
    }

    private static void BuildAct1VillageInfrastructure(Node3D parent)
    {
        var infrastructure = new Node3D { Name = "Act1VillageInfrastructure" };
        infrastructure.SetMeta("presentationOnly", true);
        infrastructure.SetMeta("visualOnly", true);
        infrastructure.SetMeta("collisionOwner", "none");
        infrastructure.SetMeta("navigationOwner", "none");
        infrastructure.SetMeta("interactionOwner", "none");
        infrastructure.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        infrastructure.SetMeta(
            "compositionRole",
            "quiet utility line and low parcel boundaries give the village a human scale without closing the first-person road window");
        parent.AddChild(infrastructure);

        // A staggered line of weathered poles is a stronger village landmark
        // than another row of trees. It remains presentation-only: the route,
        // collision and narrative owners stay untouched.
        var poles = new[]
        {
            new Vector3(-10.5f, 0f, 29.0f),
            new Vector3(10.0f, 0f, 13.0f),
            new Vector3(-9.5f, 0f, -4.5f),
            new Vector3(10.6f, 0f, -24.5f),
            new Vector3(-10.8f, 0f, -44.5f)
        };
        for (var index = 0; index < poles.Length; index++)
        {
            AddVisualUtilityPole(infrastructure, $"VillageUtilityPole{index}", poles[index], 5.8f + index % 2 * 0.25f);
        }

        for (var index = 0; index < poles.Length - 1; index++)
        {
            var start = poles[index] + new Vector3(0f, 5.15f, 0f);
            var end = poles[index + 1] + new Vector3(0f, 5.15f, 0f);
            var sag = (start + end) * 0.5f + new Vector3(0f, -0.34f, 0f);
            AddVisualCable(infrastructure, $"VillageUtilityCable{index}A", start, sag);
            AddVisualCable(infrastructure, $"VillageUtilityCable{index}B", sag, end);
        }

        // These short, broken boundaries turn the empty lateral fields into
        // readable parcels while leaving a clear window toward the next zone.
        AddVisualFenceRun(infrastructure, "ArrivalWestFieldBoundary", new(-22.0f, 0f, 18.5f), new(-34.0f, 0f, 22.0f));
        AddVisualFenceRun(infrastructure, "ArrivalEastFieldBoundary", new(22.0f, 0f, 18.0f), new(34.0f, 0f, 21.5f));
        AddVisualFenceRun(infrastructure, "MainStreetWestFieldBoundary", new(-22.5f, 0f, -4.0f), new(-34.0f, 0f, -1.0f));
        AddVisualFenceRun(infrastructure, "MainStreetEastFieldBoundary", new(22.5f, 0f, -12.5f), new(34.0f, 0f, -9.5f));
        AddVisualFenceRun(infrastructure, "ReturnWestFieldBoundary", new(-22.0f, 0f, -34.0f), new(-32.0f, 0f, -37.5f));
        AddVisualFenceRun(infrastructure, "ReturnEastFieldBoundary", new(22.0f, 0f, -38.0f), new(32.0f, 0f, -41.5f));
    }

    private void ApplyConnectedWorldPresentationSuppressions()
    {
        var framing = GetNodeOrNull<Node3D>("Act1DistantVillageFraming")
            ?? throw new InvalidOperationException("Connected Act I framing is missing before presentation suppression.");

        foreach (var (relativePath, reason) in ConnectedWorldPresentationSuppressions)
        {
            var node = framing.GetNodeOrNull<Node>(relativePath)
                ?? throw new InvalidOperationException(
                    $"Connected Act I presentation suppression target is missing: {relativePath}.");

            if (!HasTrueMeta(node, "visualOnly") && !HasTrueMeta(node, "presentationOnly"))
            {
                throw new InvalidOperationException(
                    $"Connected Act I presentation suppression target is not visual-only: {node.GetPath()}.");
            }

            var protectedNodes = new[] { node }
                .Concat(FindDescendants<Node>(node))
                .Where(IsProtectedGameplayNode)
                .ToArray();
            if (protectedNodes.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Connected Act I presentation suppression target owns protected gameplay nodes: {node.GetPath()} => "
                    + string.Join('|', protectedNodes.Select(protectedNode => protectedNode.GetType().Name)));
            }

            node.SetMeta("connectedWorldSuppressionReason", reason);
            GD.Print(
                $"act1-connected-world: hide presentation path={node.GetPath()} type={node.GetType().Name} reason={reason}");
            HidePresentationNode(node);
        }
    }

    /// <summary>
    /// Presentation envelope for the complete Act I route. Visual zones remain
    /// presentation-only; the mounted AgentBExteriorWorld is the explicit
    /// traversal-surface owner for terrain/architecture collision. Keeping that
    /// exception named here lets first-person review replace visual dressing
    /// without touching route targets, spawns or RuntimeBridge state.
    /// </summary>
    private void BuildAct1CoreWorldGreybox()
    {
        var core = new Node3D { Name = "Act1CoreWorldGreybox" };
        core.SetMeta("presentationOnly", true);
        core.SetMeta("visualOnly", true);
        core.SetMeta("layerRole", "complete Act I first-person spatial envelope");
        core.SetMeta(
            "collisionPolicy",
            "visual zones have no collision; AgentBExteriorWorld owns explicitly tagged terrain/architecture traversal collision");
        core.SetMeta("traversalCollisionOwner", "AgentBExteriorWorld");
        core.SetMeta("navigationPolicy", "no navigation nodes");
        core.SetMeta("interactionPolicy", "no interaction nodes");
        AddChild(core);

        BuildCoreArrival(CoreVisualZone(core, "Arrival"));
        BuildCoreMainStreet(CoreVisualZone(core, "MainStreet"));
        BuildCoreBabaiEbiYard(CoreVisualZone(core, "BabaiEbiYard"));
        BuildCoreHouseExterior(CoreVisualZone(core, "HouseExteriorApproach"));
        BuildCoreConnectiveAndReturn(CoreVisualZone(core, "ConnectiveStreetReturn"));
        BuildCoreFapExterior(CoreVisualZone(core, "FapExterior"));
        BuildCoreZirat(CoreVisualZone(core, "ZiratMemoryField"));
        BuildCoreKaraForestEdge(CoreVisualZone(core, "KaraForestEdge"));

        // The first connected-world pass used generic SphereMesh "faceted
        // masses" as horizon placeholders. In a first-person frame these
        // read as pale/teal walls rather than authored village silhouettes,
        // especially when the player looks down the road. Keep them in source
        // code for layout provenance, but remove every route-scale placeholder
        // from the production presentation envelope; Agent B buildings,
        // authored parcels and forest kits own the replacement silhouettes.
        foreach (var (relativePath, reason) in new (string RelativePath, string Reason)[]
                 {
                     ("Arrival/ArrivalVillageHorizonMass", "route-scale generic horizon mass replaced by authored arrival silhouettes"),
                     ("MainStreet/MainStreetDistantVillageMass", "route-scale generic wall replaced by authored main-street silhouettes"),
                     ("BabaiEbiYard/BabaiYardDistantStreetMass", "generic yard horizon wall replaced by authored yard continuation"),
                     ("HouseExteriorApproach/BabaiEbiHouseStreetMemoryMass", "generic house-memory wall replaced by authored house and neighbor parcels"),
                     ("ConnectiveStreetReturn/ReturnVillageHorizonMass", "generic return horizon wall replaced by authored return parcels"),
                     ("FapExterior/FapClinicReturnVillageMass", "generic FAP return wall replaced by authored village continuation"),
                     ("ZiratMemoryField/ZiratForestTransitionWest", "generic zirat transition mass replaced by authored forest edge"),
                     ("ZiratMemoryField/ZiratForestTransitionEast", "generic zirat transition mass replaced by authored forest edge"),
                     ("ZiratMemoryField/ZiratForestMemoryMass", "generic zirat memory wall replaced by authored distant trees and houses"),
                     ("KaraForestEdge/KaraVillageMemoryMassWest", "generic Kara village wall replaced by authored village-to-forest transition"),
                     ("KaraForestEdge/KaraVillageMemoryMassEast", "generic Kara village wall replaced by authored village-to-forest transition")
                 })
        {
            HideCorePresentationNode(core, relativePath, reason);
        }
        core.SetMeta("genericFacetedMassSuppressionCount", 11);

        HideCoreBlockoutBuildingVolumes(core);

        BuildAct1WetVillageRoadKit(core);
        BuildAct1FapClinicKit(core);
        BuildAct1ZiratRoadsideKit(core);
        BuildAct1KaraForestEdgeKit(core);
        BuildAct1AuthoredExteriorKit(core);
        BuildAct1SightlineClosurePass(core);
        BuildAgentBExteriorWorld(core);
        ApplyAct1DaylightPresentationPass(core);
    }

    private static void HideCoreBlockoutBuildingVolumes(Node3D core)
    {
        // The authored landmark/exterior kits already provide the village
        // facades in these parcels. Keeping the older box volumes underneath
        // them makes first-person views read as stacked greybox houses. This
        // whitelist is presentation-only; route, collision and interiors stay
        // owned by their existing systems.
        foreach (var (relativePath, reason) in new (string RelativePath, string Reason)[]
                 {
                     ("Arrival/ArrivalWestHouseVolume", "authored arrival facade replaces the blockout house volume"),
                     ("Arrival/ArrivalEastBarnVolume", "authored arrival parcel replaces the blockout barn volume"),
                     ("Arrival/ArrivalWestNearSetbackHouse", "authored near arrival facade replaces the blockout setback house"),
                     ("Arrival/ArrivalHorizonWestVolume", "authored far village edge replaces the blockout horizon house"),
                     ("Arrival/ArrivalHorizonEastVolume", "authored far village edge replaces the blockout horizon barn"),
                     ("MainStreet/MainStreetWestHouseVolume", "authored street facade replaces the blockout west house"),
                     ("MainStreet/MainStreetEastBarnVolume", "authored street parcel replaces the blockout east barn"),
                     ("MainStreet/MainStreetWestNearSetbackHouse", "authored near street facade replaces the blockout setback house"),
                     ("MainStreet/MainStreetWestBackVolume", "authored receding street parcel replaces the blockout back house"),
                     ("MainStreet/MainStreetEastBackVolume", "authored receding street parcel replaces the blockout back barn"),
                     ("BabaiEbiYard/BabaiYardBarnVolume", "authored yard parcel replaces the blockout barn"),
                     ("BabaiEbiYard/BabaiYardBackHouseVolume", "authored yard continuation replaces the blockout back house"),
                     ("HouseExteriorApproach/BabaiEbiHouseFullVolume", "authored Babai dwelling facade replaces the remaining hero blockout shell"),
                     ("ConnectiveStreetReturn/ConnectiveWestHouseVolume", "authored connective parcel replaces the blockout house"),
                     ("ConnectiveStreetReturn/ConnectiveEastBarnVolume", "authored connective parcel replaces the blockout barn"),
                     ("ConnectiveStreetReturn/ReturnWestFarmVolume", "authored return parcel replaces the blockout farm volume"),
                     ("ConnectiveStreetReturn/ReturnEastFarmVolume", "authored return parcel replaces the blockout farm volume"),
                     ("FapExterior/FapClinicFullExteriorVolume", "dedicated authored FAP kit replaces the blockout clinic volume"),
                     ("ZiratMemoryField/ZiratVillageMemoryHouseWest", "authored zirat boundary replaces the blockout village house"),
                     ("ZiratMemoryField/ZiratVillageMemoryHouseEast", "authored zirat boundary replaces the blockout village house")
                 })
        {
            HideCorePresentationNode(core, relativePath, reason, required: false);
        }

        core.SetMeta("coreBlockoutBuildingSuppressionCount", 20);
    }

    /// <summary>
    /// Authored exterior world layer (2026-08-21 consolidation): mounts the
    /// five Agent B GLB kits, the deterministic terrain collider and the
    /// unified rain/day-night atmosphere into the production connected
    /// world. Presentation + traversal-surface ownership only; interaction,
    /// narrative and save state remain with RuntimeBridge owners.
    /// </summary>
    private void BuildAgentBExteriorWorld(Node3D core)
    {
        var layer = new AgentBAct1ExteriorLayer { Name = "AgentBExteriorWorld" };
        core.AddChild(layer);
        layer.Build();
        ExcludeInteriorZonesFromExteriorCollision(layer);
    }

    /// <summary>
    /// Final presentation-only cleanup after every authored kit is mounted.
    /// Retire only the proven foreground trees and duplicate zīrat boundary
    /// dressing; the remaining core foliage closes the Kara and lateral
    /// horizons. Regrade the existing kit materials; traversal, navigation,
    /// interaction and RuntimeBridge owners remain untouched.
    /// </summary>
    private static void ApplyAct1DaylightPresentationPass(Node3D core)
    {
        var hiddenFoliageCount = HideExplicitForegroundFoliage(core);
        var hiddenZiratCount = HideZiratPresentationDuplicates(core);
        const string karaForegroundMoundPath =
            "KaraForestEdge/KaraForestEdgeAuthoredKitPresentation/KaraForestBankRight/ForestBank_Right/ForestBank_Right_EarthMound_01";
        var karaForegroundMound = core.GetNodeOrNull<Node>(karaForegroundMoundPath)
            ?? throw new InvalidOperationException(
                $"Act I Kara mound suppression target is missing: {karaForegroundMoundPath}.");
        var karaBank = core.GetNodeOrNull<Node>(
            "KaraForestEdge/KaraForestEdgeAuthoredKitPresentation/KaraForestBankRight")
            ?? throw new InvalidOperationException("Act I Kara bank presentation owner is missing.");
        if (!HasTrueMeta(karaBank, "visualOnly") && !HasTrueMeta(karaBank, "presentationOnly"))
        {
            throw new InvalidOperationException(
                $"Act I Kara mound suppression owner is not presentation-only: {karaBank.GetPath()}.");
        }

        karaForegroundMound.SetMeta("presentationOnly", true);
        HideCorePresentationNode(
            core,
            karaForegroundMoundPath,
            "proven foreground Kara east-bank earth mound obscures the route read; retain the authored bank siblings");
        var materialReboundCount = RegradeAct1DaylightKitMaterials(core);
        core.SetMeta(
            "daylightPresentationPass",
            "retire six oversized foreground tree roots, consolidate duplicate zirat boundary/path dressing, and regrade existing authored daylight kits");
        core.SetMeta("daylightPresentationHiddenFoliageCount", hiddenFoliageCount);
        core.SetMeta("ziratPresentationSuppressionCount", hiddenZiratCount);
        core.SetMeta(
            "ziratPresentationSuppressionDescription",
            "hide duplicate core fence rail/post descendants, roadside fence/gate/path roots and three oversized core conifers; AgentB_ZiratKit remains the visible fence/gate owner and marker groups remain unchanged");
        core.SetMeta("daylightPresentationMaterialReboundCount", materialReboundCount);
    }

    private static int HideExplicitForegroundFoliage(Node3D core)
    {
        var hiddenCount = 0;
        foreach (var (relativePath, reason) in new (string RelativePath, string Reason)[]
                 {
                     ("Arrival/ArrivalNearBirch", "oversized arrival foreground birch blocks the fixed first-person read"),
                     ("Arrival/ArrivalNearBroadleaf", "oversized arrival foreground broadleaf blocks the fixed first-person read"),
                     ("MainStreet/MainStreetNearBirch", "oversized main-street foreground birch blocks the fixed first-person read"),
                     ("MainStreet/MainStreetNearBroadleaf", "oversized main-street foreground broadleaf blocks the fixed first-person read"),
                     ("FapExterior/FapClinicNearBroadleaf", "oversized FAP foreground broadleaf blocks the fixed first-person read"),
                     ("HouseExteriorApproach/BabaiReverseFieldNeighborBirch", "oversized reverse-field foreground birch blocks the fixed first-person read")
                 })
        {
            if (core.GetNodeOrNull<Node3D>(relativePath) is null)
            {
                continue;
            }

            HideCorePresentationNode(core, relativePath, reason, required: false);
            hiddenCount++;
        }

        return hiddenCount;
    }

    private static int HideZiratPresentationDuplicates(Node3D core)
    {
        var hiddenCount = 0;
        foreach (var (parentPath, namePrefix, reason) in new (string ParentPath, string NamePrefix, string Reason)[]
                 {
                     ("ZiratMemoryField", "ZiratOuterWestBoundary", "duplicate core zirat outer fence crowds the central route"),
                     ("ZiratMemoryField", "ZiratOuterEastBoundary", "duplicate core zirat outer fence crowds the central route"),
                     ("ZiratMemoryField", "ZiratEntryWestBoundary", "duplicate core zirat entry fence crowds the central route"),
                     ("ZiratMemoryField", "ZiratEntryEastBoundary", "duplicate core zirat entry fence crowds the central route"),
                     ("ZiratMemoryField/ZiratBoundaryClosureGrouping", "ZiratClosureVillageFenceWest", "duplicate core zirat closure fence crowds the central route"),
                     ("ZiratMemoryField/ZiratBoundaryClosureGrouping", "ZiratClosureVillageFenceEast", "duplicate core zirat closure fence crowds the central route"),
                     ("ZiratMemoryField/ZiratBoundaryClosureGrouping", "ZiratClosureForestFenceWest", "duplicate core zirat closure fence crowds the central route"),
                     ("ZiratMemoryField/ZiratBoundaryClosureGrouping", "ZiratClosureForestFenceEast", "duplicate core zirat closure fence crowds the central route")
                 })
        {
            hiddenCount += HideCorePresentationPrefix(core, parentPath, namePrefix, reason);
        }

        foreach (var (relativePath, reason) in new (string RelativePath, string Reason)[]
                 {
                     ("ZiratMemoryField/ZiratAuthoredMemorialForms/ZiratAuthoredMemorialPathShoulder", "duplicate core memorial path shoulder narrows the quiet central route"),
                     ("ZiratMemoryField/ZiratRoadsideAuthoredKitPresentation/ZiratAuthoredBoundaryFence", "duplicate roadside fence is replaced by the visible AgentB zirat fence owner"),
                     ("ZiratMemoryField/ZiratRoadsideAuthoredKitPresentation/ZiratAuthoredOpenGate", "duplicate roadside gate is replaced by the visible AgentB zirat gate owner"),
                     ("ZiratMemoryField/ZiratRoadsideAuthoredKitPresentation/ZiratAuthoredPathEdge", "duplicate roadside path edge narrows the quiet central route"),
                     ("ZiratMemoryField/ZiratMemoryConiferWest", "oversized core zirat conifer blocks the central route read"),
                     ("ZiratMemoryField/ZiratBoundaryClosureGrouping/ZiratClosureMidConiferWest", "oversized core zirat conifer blocks the central route read"),
                     ("ZiratMemoryField/ZiratBoundaryClosureGrouping/ZiratClosureForestConiferEast", "oversized core zirat conifer blocks the central route read")
                 })
        {
            HideCorePresentationNode(core, relativePath, reason);
            hiddenCount++;
        }

        return hiddenCount;
    }

    private static int HideCorePresentationPrefix(
        Node3D core,
        string parentPath,
        string namePrefix,
        string reason)
    {
        var parent = core.GetNodeOrNull<Node3D>(parentPath)
            ?? throw new InvalidOperationException(
                $"Act I authored exterior kit suppression parent is missing: {parentPath}.");
        var matches = parent.GetChildren()
            .Where(node => node.Name.ToString().StartsWith(namePrefix, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            throw new InvalidOperationException(
                $"Act I authored exterior kit suppression prefix is missing: {parentPath}/{namePrefix}.");
        }

        foreach (var node in matches)
        {
            HideCorePresentationNode(core, $"{parentPath}/{node.Name}", reason);
        }

        return matches.Length;
    }

    private static int RegradeAct1DaylightKitMaterials(Node3D core)
    {
        var grade = new Dictionary<string, Material>(StringComparer.Ordinal)
        {
            // Lift broad building planes one value step so the rainy-day
            // directional light keeps facade silhouettes readable without
            // turning the village into a bright generic asset pack.
            ["AB_plaster"] = PainterlyMaterialLibrary.ForColor("92958a", "plaster"),
            ["AB_plaster_faded"] = PainterlyMaterialLibrary.ForColor("858b80", "plaster"),
            ["AB_timber"] = PainterlyMaterialLibrary.ForColor("806a50", "wood"),
            ["AB_log_wall"] = PainterlyMaterialLibrary.ForColor("967d5e", "wood_facade"),
            ["AB_timber_dark"] = PainterlyMaterialLibrary.ForColor("695541", "wood"),
            ["AB_fade_paint"] = PainterlyMaterialLibrary.ForColor("92816b", "wood_fence"),
            ["AB_roof_iron"] = PainterlyMaterialLibrary.ForColor("76807a", "stone"),
            ["AB_roof_iron_dark"] = PainterlyMaterialLibrary.ForColor("687069", "stone"),
            ["AB_roof_shingle"] = PainterlyMaterialLibrary.ForColor("695b4e", "wood"),
            ["AB_window_warm"] = new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml("d89b5d"),
                EmissionEnabled = true,
                Emission = Color.FromHtml("a86436"),
                EmissionEnergyMultiplier = 1.75f,
                Roughness = 0.55f,
                MetallicSpecular = 0.42f
            },
            ["AB_window_cold"] = new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml("71818a"),
                Roughness = 0.62f,
                MetallicSpecular = 0.28f
            },
            ["URMAN_Plaster_Ochre"] = PainterlyMaterialLibrary.ForColor("8d8774", "plaster"),
            ["URMAN_Plaster_Line"] = PainterlyMaterialLibrary.ForColor("737268", "plaster"),
            ["URMAN_Plaster_Shadow"] = PainterlyMaterialLibrary.ForColor("66685f", "plaster"),
            ["URMAN_Wood_Dark"] = PainterlyMaterialLibrary.ForColor("605044", "wood"),
            ["URMAN_Roof_WetSlate"] = PainterlyMaterialLibrary.ForColor("626b66", "stone"),
            ["FapPaintedSage"] = PainterlyMaterialLibrary.ForColor("7b8b80", "plaster"),
            ["FapPaintedDustyBlue"] = PainterlyMaterialLibrary.ForColor("74838a", "plaster"),
            ["FapPaintedTimber"] = PainterlyMaterialLibrary.ForColor("7c684f", "wood"),
            ["FapOldRoof"] = PainterlyMaterialLibrary.ForColor("5e6862", "stone"),
            ["FapRoofEdge"] = PainterlyMaterialLibrary.ForColor("687169", "stone"),
            ["FapShedWall"] = PainterlyMaterialLibrary.ForColor("778073", "plaster"),
            ["FapFoundationStone"] = PainterlyMaterialLibrary.ForColor("777970", "stone"),
            ["FapPathEarth"] = PainterlyMaterialLibrary.ForColor("4a554d", "earth"),
            ["FapPathEarthDark"] = PainterlyMaterialLibrary.ForColor("3b4842", "earth"),
            ["FapPuddleWater"] = PainterlyMaterialLibrary.ForColor("3c4c4a", "water"),
            ["DampEarth"] = PainterlyMaterialLibrary.ForColor("425448", "earth"),
            ["DampEarthDark"] = PainterlyMaterialLibrary.ForColor("394b40", "earth"),
            ["QuietStone"] = PainterlyMaterialLibrary.ForColor("7e7f73", "stone"),
            ["DistantWall"] = PainterlyMaterialLibrary.ForColor("6f716a", "plaster"),
            ["DistantRoof"] = PainterlyMaterialLibrary.ForColor("5f6761", "stone")
        };

        var rebound = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(core))
        {
            if (mesh.Mesh is null)
            {
                continue;
            }

            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                var source = mesh.Mesh.SurfaceGetMaterial(surface);
                var sourceName = source?.ResourceName ?? string.Empty;
                if (!grade.TryGetValue(sourceName, out var material))
                {
                    continue;
                }

                mesh.SetSurfaceOverrideMaterial(surface, material);
                rebound++;
            }
        }

        return rebound;
    }

    /// <summary>
    /// Interior zone footprints (house/FAP) keep their own authored floors and
    /// interaction anchors; the exterior architecture collision must not wall
    /// off their doorways. Excludes the two interior building volumes from the
    /// exterior collision body while keeping all outdoor volumes intact.
    /// </summary>
    private static void ExcludeInteriorZonesFromExteriorCollision(Node3D layer)
    {
        var architecture = layer.GetNodeOrNull<StaticBody3D>("AgentB_ArchitectureCollision");
        if (architecture is null)
        {
            return;
        }

        // House interior footprint (house_old_pc at (-28, 0, 0), local floor
        // 12x10) and FAP footprint (fap_clinic at (28, -30)). Any collider
        // whose XZ center falls inside these rectangles is a building shell,
        // not an outdoor obstacle, so the interior owners stay authoritative.
        (Vector2 Center, Vector2 HalfSize)[] interiorFootprints =
        {
            (new(-28f, 0f), new(6.5f, 5.5f)),
            (new(28f, -30f), new(6.0f, 5.0f)),
            // Zirat fence wall crossing the production zirat_road route
            // corridor (x -1.6..14.5 at z -63.9..-76.1). The route owner keeps
        // the walkable corridor authoritative; the exterior dressing stays visual.
            (new(6.5f, -70f), new(8.6f, 7.2f))
        };

        // The Babai house exterior volume from the authored kit sits exactly
        // where the production interior zone (house_old_pc at (-28,0,0))
        // mounts its own floor/walls. The interior owner is authoritative;
        // the exterior kit shell must not wall off the approach or doorway.
        Vector2[] excludedVolumeCenters =
        {
            new(-30f, -1f)
        };
        var excluded = 0;
        foreach (var shape in architecture.GetChildren().OfType<CollisionShape3D>().ToArray())
        {
            var center = shape.GlobalTransform.Origin;
        // The Babai house kit volume (AABB x -32.8..-27.2, z -4.3..2.3)
        // coincides with the production interior zone. Match by AABB
        // overlap with the interior rectangle instead of the collider
        // origin, because trimesh origins can sit outside the shell.
        // The zirat fence line (z -63.9 .. -76.1, x -1.6..14.5) crosses the
        // production zirat_road route corridor (x ±2.2 around 0). Its gate is
        // authored at x -1.5..-1.4, but the walkthrough approaches along x=0,
        // so the whole fence wall blocks the physical path. The exterior
        // layer's own zirat dressing stays visual; the route owner keeps the
        // authoritative walkable corridor.
        var debugMesh = shape.Shape?.GetDebugMesh();
            if (debugMesh is not null)
            {
                var aabb = debugMesh.GetAabb();
                var worldMin = shape.GlobalTransform * aabb.Position;
                var worldMax = shape.GlobalTransform * aabb.End;
                var lo = new Vector3(
                    System.Math.Min(worldMin.X, worldMax.X),
                    System.Math.Min(worldMin.Y, worldMax.Y),
                    System.Math.Min(worldMin.Z, worldMax.Z));
                var hi = new Vector3(
                    System.Math.Max(worldMin.X, worldMax.X),
                    System.Math.Max(worldMin.Y, worldMax.Y),
                    System.Math.Max(worldMin.Z, worldMax.Z));
                foreach (var (footprintCenter, halfSize) in interiorFootprints)
                {
                    var fpMinX = footprintCenter.X - halfSize.X;
                    var fpMaxX = footprintCenter.X + halfSize.X;
                    var fpMinZ = footprintCenter.Y - halfSize.Y;
                    var fpMaxZ = footprintCenter.Y + halfSize.Y;
                    var overlaps = lo.X < fpMaxX && hi.X > fpMinX
                        && lo.Z < fpMaxZ && hi.Z > fpMinZ;
                    if (overlaps)
                    {
                        shape.Disabled = true;
                        shape.QueueFree();
                        excluded++;
                    }
                }
            }
            foreach (var volumeCenter in excludedVolumeCenters)
            {
                if (System.Math.Abs(center.X - volumeCenter.X) <= 3.5f
                    && System.Math.Abs(center.Z - volumeCenter.Y) <= 3.5f)
                {
                    // Disabled immediately: QueueFree alone leaves the shape
                    // active for the remainder of the frame, which is exactly
                    // when spawn/first-step queries run.
                    shape.Disabled = true;
                    shape.QueueFree();
                    excluded++;
                }
            }
            foreach (var (footprintCenter, halfSize) in interiorFootprints)
            {
                if (System.Math.Abs(center.X - footprintCenter.X) <= halfSize.X
                    && System.Math.Abs(center.Z - footprintCenter.Y) <= halfSize.Y)
                {
                    shape.Disabled = true;
                    shape.QueueFree();
                    excluded++;
                    break;
                }
            }
        }

        layer.SetMeta("interiorFootprintCollidersExcluded", excluded);
    }

    private static void BuildAct1WetVillageRoadKit(Node3D core)
    {
        var arrivalZone = core.GetNodeOrNull<Node3D>("Arrival")
            ?? throw new InvalidOperationException(
                "Act I core world is missing the Arrival presentation zone.");
        var mainStreetZone = core.GetNodeOrNull<Node3D>("MainStreet")
            ?? throw new InvalidOperationException(
                "Act I core world is missing the MainStreet presentation zone.");
        var returnZone = core.GetNodeOrNull<Node3D>("ConnectiveStreetReturn")
            ?? throw new InvalidOperationException(
                "Act I core world is missing the ConnectiveStreetReturn presentation zone.");
        var ziratZone = core.GetNodeOrNull<Node3D>("ZiratMemoryField")
            ?? throw new InvalidOperationException(
                "Act I core world is missing the ZiratMemoryField presentation zone.");

        var presentation = new Node3D { Name = "WetVillageRoadKitPresentation" };
        presentation.SetMeta("presentationOnly", true);
        presentation.SetMeta("visualOnly", true);
        presentation.SetMeta("assetSource", WetVillageRoadKitScenePath);
        presentation.SetMeta("authoredRoot", WetVillageRoadKitRootName);
        presentation.SetMeta("componentContract", string.Join('|', WetVillageRoadKitComponentNames));
        presentation.SetMeta("collisionOwner", "none");
        presentation.SetMeta("navigationOwner", "none");
        presentation.SetMeta("interactionOwner", "none");
        presentation.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        presentation.SetMeta(
            "roadWindowPolicy",
            "open forward/reverse first-person road window; center route, entry connector and zirat transition remain clear");
        presentation.SetMeta(
            "placementPolicy",
            "extract and rebase named geometry-only roots, then vary crown/rut rhythm, side assignment, yaw and lateral landmarks across Arrival, MainStreet and ReturnStreet");
        presentation.SetMeta(
            "suppressionScope",
            "exact visual shoulder/ditch primitives under Arrival, MainStreet, ConnectiveStreetReturn and ZiratMemoryField only; route/connector collision/path, navigation, interaction, camera/light, spawn, RuntimeBridge and core landmarks remain authoritative");
        presentation.SetMeta("mountOwner", "Act1CoreWorldGreybox presentation only; no narrative or gameplay ownership");
        core.AddChild(presentation);

        var packed = ResourceLoader.Load<PackedScene>(WetVillageRoadKitScenePath)
            ?? throw new InvalidOperationException(
                $"Act I wet village road kit is missing: {WetVillageRoadKitScenePath}.");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException(
                $"Act I wet village road kit did not instantiate: {WetVillageRoadKitScenePath}.");
        var kitRoot = importedRoot;

        // Resolve the authored root before extraction so the neutral preview
        // board arrangement never becomes a connected-world coordinate.
        if (!string.Equals(importedRoot.Name.ToString(), WetVillageRoadKitRootName, StringComparison.Ordinal))
        {
            kitRoot = importedRoot.GetNodeOrNull<Node3D>(WetVillageRoadKitRootName)
                ?? throw new InvalidOperationException(
                    $"Act I wet village road kit is missing authored root '{WetVillageRoadKitRootName}'.");
        }

        RebindWetVillageRoadMaterials(kitRoot);

        var collisionNodes = FindDescendants<Node>(kitRoot)
            .Where(node => node is CollisionObject3D or CollisionShape3D)
            .ToArray();
        if (collisionNodes.Length > 0)
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException(
                $"Act I wet village road kit must be presentation-only; found {collisionNodes.Length} collision nodes.");
        }

        var components = WetVillageRoadKitComponentNames
            .Select(componentName => (
                Name: componentName,
                Root: kitRoot.GetNodeOrNull<Node3D>(componentName)))
            .ToArray();
        var missingComponents = components
            .Where(component => component.Root is null)
            .Select(component => component.Name)
            .ToArray();
        if (missingComponents.Length > 0)
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException(
                $"Act I wet village road kit is missing required direct component roots: {string.Join('|', missingComponents)}.");
        }

        if (!Act1WorldLayout.TryGetPlacement("village_day", out var villagePlacement))
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException("Connected Act I layout is missing village_day placement.");
        }

        var residentialRoad = GetConnector("residential-road-extension");
        var villageDirection = HorizontalDirection(residentialRoad.End - residentialRoad.Start);
        var villageSide = new Vector3(villageDirection.Z, 0f, -villageDirection.X);
        var villageYaw = DirectionYaw(villageDirection);
        var arrivalAnchor = villagePlacement.Origin + new Vector3(0f, 0.035f, 14.0f);
        var mainStreetAnchor = residentialRoad.Start + villageDirection * 13.0f;

        var returnRoad = GetConnector("house-to-zirat-return");
        var returnDirection = HorizontalDirection(returnRoad.End - returnRoad.Start);
        var returnSide = new Vector3(returnDirection.Z, 0f, -returnDirection.X);
        var returnYaw = DirectionYaw(returnDirection);
        var returnStreetAnchor = returnRoad.Start + returnDirection * 28.0f;

        if (!Act1WorldLayout.TryGetPlacement("zirat_road", out var ziratPlacement))
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException("Connected Act I layout is missing zirat_road placement.");
        }

        var ziratApproach = GetConnector("zirat-to-kara-urman");
        var ziratDirection = HorizontalDirection(ziratApproach.End - ziratApproach.Start);
        var ziratSide = new Vector3(ziratDirection.Z, 0f, -ziratDirection.X);
        var ziratYaw = DirectionYaw(ziratDirection);
        var returnTransitionAnchor = ziratPlacement.Origin + new Vector3(0f, 0.035f, -7.0f);

        // Reuse the verified direct roots as restrained near/mid/far beats.
        // Clones are added to the imported authored root before the same
        // existing extraction/rebase helper owns each placed component.
        Node3D CloneComponent(string componentName, string cloneName)
        {
            var source = kitRoot.GetNode<Node3D>(componentName);
            var clone = source.Duplicate() as Node3D
                ?? throw new InvalidOperationException(
                    $"Act I wet village road kit component '{componentName}' could not be duplicated.");
            clone.Name = cloneName;
            kitRoot.AddChild(clone);
            return clone;
        }

        var mainStreetCrown = CloneComponent("RoadCrown_SunkenWet", "RoadCrown_SunkenWet_MainStreetSource");
        var returnStreetCrown = CloneComponent("RoadCrown_SunkenWet", "RoadCrown_SunkenWet_ReturnStreetSource");
        var mainStreetRutsNear = CloneComponent("RoadRuts_PuddleNear", "RoadRuts_PuddleNear_MainStreetSource");
        var returnStreetRutsNear = CloneComponent("RoadRuts_PuddleNear", "RoadRuts_PuddleNear_ReturnStreetSource");
        var returnTransitionRutsNear = CloneComponent("RoadRuts_PuddleNear", "RoadRuts_PuddleNear_ReturnTransitionSource");
        var mainStreetRutsFar = CloneComponent("RoadRuts_PuddleFar", "RoadRuts_PuddleFar_MainStreetSource");
        var returnStreetRutsFar = CloneComponent("RoadRuts_PuddleFar", "RoadRuts_PuddleFar_ReturnStreetSource");
        var returnTransitionRutsFar = CloneComponent("RoadRuts_PuddleFar", "RoadRuts_PuddleFar_ReturnTransitionSource");
        var mainStreetShoulderLeft = CloneComponent("MuddyShoulder_Left", "MuddyShoulder_Left_MainStreetSource");
        var returnStreetShoulderLeft = CloneComponent("MuddyShoulder_Left", "MuddyShoulder_Left_ReturnStreetSource");
        var mainStreetShoulderRight = CloneComponent("MuddyShoulder_Right", "MuddyShoulder_Right_MainStreetSource");
        var returnStreetShoulderRight = CloneComponent("MuddyShoulder_Right", "MuddyShoulder_Right_ReturnStreetSource");
        var mainStreetDitchLeft = CloneComponent("RoadsideDitch_Left", "RoadsideDitch_Left_MainStreetSource");
        var returnStreetDitchLeft = CloneComponent("RoadsideDitch_Left", "RoadsideDitch_Left_ReturnStreetSource");
        var mainStreetDitchRight = CloneComponent("RoadsideDitch_Right", "RoadsideDitch_Right_MainStreetSource");
        var returnStreetDitchRight = CloneComponent("RoadsideDitch_Right", "RoadsideDitch_Right_ReturnStreetSource");
        var returnTransitionGrassRight = CloneComponent("GrassSedgeMass_Right", "GrassSedgeMass_Right_ReturnTransitionSource");

        AttachAct1ExteriorKitComponent(
            presentation,
            components[0].Root!,
            "WetVillageRoadArrivalCrown",
            arrivalAnchor,
            villageYaw,
            Vector3.One,
            "village_day@arrival-road-crown",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[1].Root!,
            "WetVillageRoadArrivalRutsNear",
            arrivalAnchor - villageDirection * 3.6f + villageSide * 0.45f,
            villageYaw - 2f,
            Vector3.One,
            "village_day@arrival-ruts-near",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[2].Root!,
            "WetVillageRoadArrivalRutsFar",
            arrivalAnchor + villageDirection * 4.0f - villageSide * 0.35f,
            villageYaw + 2f,
            Vector3.One,
            "village_day@arrival-ruts-far",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[3].Root!,
            "WetVillageRoadArrivalShoulderLeft",
            arrivalAnchor + villageSide * 3.25f,
            villageYaw,
            Vector3.One,
            "village_day@arrival-shoulder-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[4].Root!,
            "WetVillageRoadArrivalShoulderRight",
            arrivalAnchor - villageSide * 3.25f,
            villageYaw,
            Vector3.One,
            "village_day@arrival-shoulder-right",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[5].Root!,
            "WetVillageRoadArrivalDitchLeft",
            arrivalAnchor + villageSide * 4.85f,
            villageYaw,
            Vector3.One,
            "village_day@arrival-ditch-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[6].Root!,
            "WetVillageRoadArrivalDitchRight",
            arrivalAnchor - villageSide * 4.85f,
            villageYaw,
            Vector3.One,
            "village_day@arrival-ditch-right",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[8].Root!,
            "WetVillageRoadArrivalSedgeLeft",
            arrivalAnchor + villageSide * 6.15f - villageDirection * 1.8f,
            villageYaw,
            Vector3.One * 0.90f,
            "village_day@arrival-sparse-sedge-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[9].Root!,
            "WetVillageRoadArrivalSedgeRight",
            arrivalAnchor - villageSide * 6.15f + villageDirection * 2.2f,
            villageYaw,
            Vector3.One * 0.88f,
            "village_day@arrival-sparse-sedge-right",
            WetVillageRoadKitScenePath);

        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetCrown,
            "WetVillageRoadMainStreetCrown",
            mainStreetAnchor,
            villageYaw + 2f,
            Vector3.One * 0.98f,
            "village_day@main-street-crown-variation",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetRutsNear,
            "WetVillageRoadMainStreetRutsNear",
            mainStreetAnchor - villageDirection * 4.4f - villageSide * 0.55f,
            villageYaw - 3f,
            Vector3.One * 0.94f,
            "village_day@main-street-ruts-near",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetRutsFar,
            "WetVillageRoadMainStreetRutsFar",
            mainStreetAnchor + villageDirection * 4.7f + villageSide * 0.50f,
            villageYaw + 3f,
            Vector3.One * 0.96f,
            "village_day@main-street-ruts-far",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetShoulderLeft,
            "WetVillageRoadMainStreetShoulderLeft",
            mainStreetAnchor + villageSide * 3.30f,
            villageYaw + 2f,
            Vector3.One * 0.96f,
            "village_day@main-street-shoulder-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetShoulderRight,
            "WetVillageRoadMainStreetShoulderRight",
            mainStreetAnchor - villageSide * 3.30f,
            villageYaw + 2f,
            Vector3.One * 0.96f,
            "village_day@main-street-shoulder-right",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetDitchLeft,
            "WetVillageRoadMainStreetDitchLeft",
            mainStreetAnchor + villageSide * 4.90f,
            villageYaw + 2f,
            Vector3.One * 0.94f,
            "village_day@main-street-ditch-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetDitchRight,
            "WetVillageRoadMainStreetDitchRight",
            mainStreetAnchor - villageSide * 4.90f,
            villageYaw + 2f,
            Vector3.One * 0.94f,
            "village_day@main-street-ditch-right",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[10].Root!,
            "WetVillageRoadMainStreetFernLeft",
            mainStreetAnchor + villageSide * 7.0f + villageDirection * 3.0f,
            villageYaw + 2f,
            Vector3.One * 0.84f,
            "village_day@main-street-partial-fern-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[11].Root!,
            "WetVillageRoadMainStreetFernRight",
            mainStreetAnchor - villageSide * 7.0f - villageDirection * 2.6f,
            villageYaw + 2f,
            Vector3.One * 0.82f,
            "village_day@main-street-partial-fern-right",
            WetVillageRoadKitScenePath);

        AttachAct1ExteriorKitComponent(
            presentation,
            returnStreetCrown,
            "WetVillageRoadReturnStreetCrown",
            returnStreetAnchor,
            returnYaw,
            Vector3.One * 0.98f,
            "house_to_zirat@return-street-crown-variation",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            returnStreetRutsNear,
            "WetVillageRoadReturnStreetRutsNear",
            returnStreetAnchor - returnDirection * 4.0f + returnSide * 0.48f,
            returnYaw - 4f,
            Vector3.One * 0.94f,
            "house_to_zirat@return-street-ruts-near",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            returnStreetRutsFar,
            "WetVillageRoadReturnStreetRutsFar",
            returnStreetAnchor + returnDirection * 4.6f - returnSide * 0.46f,
            returnYaw + 4f,
            Vector3.One * 0.92f,
            "house_to_zirat@return-street-ruts-far",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            returnStreetShoulderLeft,
            "WetVillageRoadReturnStreetShoulderLeft",
            returnStreetAnchor + returnSide * 3.20f,
            returnYaw,
            Vector3.One * 0.96f,
            "house_to_zirat@return-street-shoulder-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            returnStreetShoulderRight,
            "WetVillageRoadReturnStreetShoulderRight",
            returnStreetAnchor - returnSide * 3.20f,
            returnYaw,
            Vector3.One * 0.96f,
            "house_to_zirat@return-street-shoulder-right",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            returnStreetDitchLeft,
            "WetVillageRoadReturnStreetDitchLeft",
            returnStreetAnchor + returnSide * 4.80f,
            returnYaw,
            Vector3.One * 0.94f,
            "house_to_zirat@return-street-ditch-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            returnStreetDitchRight,
            "WetVillageRoadReturnStreetDitchRight",
            returnStreetAnchor - returnSide * 4.80f,
            returnYaw,
            Vector3.One * 0.94f,
            "house_to_zirat@return-street-ditch-right",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[7].Root!,
            "WetVillageRoadReturnCulvert",
            returnStreetAnchor + returnSide * 5.05f + returnDirection * 1.5f,
            returnYaw,
            Vector3.One * 0.92f,
            "house_to_zirat@return-street-culvert-landmark",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[12].Root!,
            "WetVillageRoadReturnBrokenFence",
            returnStreetAnchor - returnSide * 6.15f - returnDirection * 1.2f,
            returnYaw + 90f,
            Vector3.One * 0.90f,
            "house_to_zirat@return-street-low-broken-fence",
            WetVillageRoadKitScenePath);

        AttachAct1ExteriorKitComponent(
            presentation,
            components[14].Root!,
            "WetVillageRoadReturnTransitionCrownWorn",
            returnTransitionAnchor,
            ziratYaw - 2f,
            Vector3.One * 0.94f,
            "zirat_road@return-street-transition-crown-variant",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            returnTransitionRutsNear,
            "WetVillageRoadReturnTransitionRutsNear",
            returnTransitionAnchor - ziratDirection * 3.4f + ziratSide * 0.42f,
            ziratYaw - 1f,
            Vector3.One * 0.90f,
            "zirat_road@return-street-transition-ruts-near",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            returnTransitionRutsFar,
            "WetVillageRoadReturnTransitionRutsFar",
            returnTransitionAnchor + ziratDirection * 3.8f - ziratSide * 0.38f,
            ziratYaw + 1f,
            Vector3.One * 0.90f,
            "zirat_road@return-street-transition-ruts-far",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            returnTransitionGrassRight,
            "WetVillageRoadReturnTransitionSedgeRight",
            returnTransitionAnchor - ziratSide * 6.0f + ziratDirection * 2.0f,
            ziratYaw,
            Vector3.One * 0.82f,
            "zirat_road@return-street-transition-sedge-right",
            WetVillageRoadKitScenePath);

        // The FAP branch keeps its own narrower authored segment so the
        // branch road no longer reads as a copy of the main street strip.
        var fapBranch = GetConnector("village-to-fap-branch");
        var fapBranchDirection = HorizontalDirection(fapBranch.End - fapBranch.Start);
        var fapBranchYaw = DirectionYaw(fapBranchDirection);
        var fapBranchAnchor = fapBranch.Start
            + fapBranchDirection * (new Vector2(fapBranch.End.X - fapBranch.Start.X, fapBranch.End.Z - fapBranch.Start.Z).Length() * 0.5f);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[13].Root!,
            "WetVillageRoadFapBranchCrown",
            fapBranchAnchor,
            fapBranchYaw,
            Vector3.One,
            "village_day@fap-branch-crown-variant",
            WetVillageRoadKitScenePath);

        // The authored road replaces only overlapping visual
        // shoulder/ditch/outer-bank primitives. Shared ground, connector/path collision, navigation,
        // interaction targets, camera/light, spawns, RuntimeBridge state and
        // core landmarks stay outside this exact suppression whitelist.
        foreach (var (relativePath, reason) in new (string RelativePath, string Reason)[]
                 {
                     ("Arrival/ArrivalRoadEnvelopeLeftShoulder", "wet road kit replaces the overlapping flat arrival shoulder"),
                     ("Arrival/ArrivalRoadEnvelopeRightShoulder", "wet road kit replaces the overlapping flat arrival shoulder"),
                     ("Arrival/ArrivalRoadEnvelopeLeftWetDitch", "wet road kit replaces the overlapping flat arrival ditch"),
                     ("Arrival/ArrivalRoadEnvelopeRightWetDitch", "wet road kit replaces the overlapping flat arrival ditch"),
                     ("Arrival/ArrivalRoadEnvelopeLeftOuterBank", "AgentB terrain replaces the overlapping flat arrival outer bank"),
                     ("Arrival/ArrivalRoadEnvelopeRightOuterBank", "AgentB terrain replaces the overlapping flat arrival outer bank"),
                     ("Arrival/ArrivalWetShoulderWest", "wet road kit replaces the overlapping arrival shoulder patch"),
                     ("Arrival/ArrivalWetShoulderEast", "wet road kit replaces the overlapping arrival shoulder patch"),
                     ("MainStreet/MainStreetRoadEnvelopeLeftShoulder", "wet road kit replaces the overlapping flat main-street shoulder"),
                     ("MainStreet/MainStreetRoadEnvelopeRightShoulder", "wet road kit replaces the overlapping flat main-street shoulder"),
                     ("MainStreet/MainStreetRoadEnvelopeLeftWetDitch", "wet road kit replaces the overlapping flat main-street ditch"),
                     ("MainStreet/MainStreetRoadEnvelopeRightWetDitch", "wet road kit replaces the overlapping flat main-street ditch"),
                     ("MainStreet/MainStreetRoadEnvelopeLeftOuterBank", "AgentB terrain replaces the overlapping flat main-street outer bank"),
                     ("MainStreet/MainStreetRoadEnvelopeRightOuterBank", "AgentB terrain replaces the overlapping flat main-street outer bank"),
                     ("ConnectiveStreetReturn/ConnectiveReturnRoadEnvelopeLeftShoulder", "wet road kit replaces the overlapping flat return shoulder"),
                     ("ConnectiveStreetReturn/ConnectiveReturnRoadEnvelopeRightShoulder", "wet road kit replaces the overlapping flat return shoulder"),
                     ("ConnectiveStreetReturn/ConnectiveReturnRoadEnvelopeLeftWetDitch", "wet road kit replaces the overlapping flat return ditch"),
                     ("ConnectiveStreetReturn/ConnectiveReturnRoadEnvelopeRightWetDitch", "wet road kit replaces the overlapping flat return ditch"),
                     ("ConnectiveStreetReturn/ConnectiveReturnRoadEnvelopeLeftOuterBank", "AgentB terrain replaces the overlapping flat return outer bank"),
                     ("ConnectiveStreetReturn/ConnectiveReturnRoadEnvelopeRightOuterBank", "AgentB terrain replaces the overlapping flat return outer bank"),
                     ("FapExterior/FapBranchRoadEnvelopeLeftOuterBank", "AgentB terrain replaces the overlapping flat FAP outer bank"),
                     ("FapExterior/FapBranchRoadEnvelopeRightOuterBank", "AgentB terrain replaces the overlapping flat FAP outer bank"),
                     ("ZiratMemoryField/ZiratRoadEnvelopeLeftShoulder", "wet road kit replaces the overlapping flat zirat-transition shoulder"),
                     ("ZiratMemoryField/ZiratRoadEnvelopeRightShoulder", "wet road kit replaces the overlapping flat zirat-transition shoulder"),
                     ("ZiratMemoryField/ZiratRoadEnvelopeLeftWetDitch", "wet road kit replaces the overlapping flat zirat-transition ditch"),
                     ("ZiratMemoryField/ZiratRoadEnvelopeRightWetDitch", "wet road kit replaces the overlapping flat zirat-transition ditch"),
                     ("ZiratMemoryField/ZiratRoadEnvelopeLeftOuterBank", "AgentB terrain replaces the overlapping flat zirat outer bank"),
                     ("ZiratMemoryField/ZiratRoadEnvelopeRightOuterBank", "AgentB terrain replaces the overlapping flat zirat outer bank"),
                     ("KaraForestEdge/KaraApproachRoadEnvelopeLeftOuterBank", "AgentB Kara terrain replaces the overlapping flat approach outer bank"),
                     ("KaraForestEdge/KaraApproachRoadEnvelopeRightOuterBank", "AgentB Kara terrain replaces the overlapping flat approach outer bank")
                 })
        {
            HideCorePresentationNode(core, relativePath, reason);
        }

        // Keep zone variables explicit in this presentation-only owner: the
        // road mount is rooted at Act1CoreWorldGreybox, while these existing
        // zones are the bounded visual suppression/continuity surfaces.
        arrivalZone.SetMeta("wetVillageRoadKitCoverage", "arrival");
        mainStreetZone.SetMeta("wetVillageRoadKitCoverage", "main-street");
        returnZone.SetMeta("wetVillageRoadKitCoverage", "connective-return");
        ziratZone.SetMeta("wetVillageRoadKitCoverage", "return-transition");

        importedRoot.QueueFree();
    }

    private static void BuildAct1FapClinicKit(Node3D core)
    {
        var fapZone = core.GetNodeOrNull<Node3D>("FapExterior")
            ?? throw new InvalidOperationException(
                "Act I core world is missing the FapExterior presentation zone.");
        var presentation = new Node3D { Name = "FapClinicAuthoredKitPresentation" };
        presentation.SetMeta("presentationOnly", true);
        presentation.SetMeta("visualOnly", true);
        presentation.SetMeta("assetSource", FapClinicKitScenePath);
        presentation.SetMeta("authoredRoot", FapClinicKitRootName);
        presentation.SetMeta("componentContract", string.Join('|', FapClinicKitComponentNames));
        presentation.SetMeta("collisionOwner", "none");
        presentation.SetMeta("navigationOwner", "none");
        presentation.SetMeta("interactionOwner", "none");
        presentation.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        presentation.SetMeta(
            "placementPolicy",
            "facade and porch follow the existing FAP approach; shed/fence/gate stay on the lateral service parcel; boards, bench, awning, puddles and birch/shrub dressing remain modest and keep the doorway and route open");
        presentation.SetMeta(
            "boardPolicy",
            "board meshes remain blank neutral authored geometry; one diegetic ФАП label is composed on the wayfinding board, with no quest marker or narrative state");
        presentation.SetMeta(
            "suppressionScope",
            "exact presentation primitives under FapExterior only; route, collision, navigation, interaction, camera/light and RuntimeBridge owners remain authoritative");
        fapZone.AddChild(presentation);

        var packed = ResourceLoader.Load<PackedScene>(FapClinicKitScenePath)
            ?? throw new InvalidOperationException(
                $"Act I FAP clinic kit is missing: {FapClinicKitScenePath}.");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException(
                $"Act I FAP clinic kit did not instantiate: {FapClinicKitScenePath}.");
        var kitRoot = importedRoot;

        // Resolve the authored root before extraction so the neutral preview
        // board offsets never become connected-world coordinates.
        if (!string.Equals(importedRoot.Name.ToString(), FapClinicKitRootName, StringComparison.Ordinal))
        {
            kitRoot = importedRoot.GetNodeOrNull<Node3D>(FapClinicKitRootName)
                ?? throw new InvalidOperationException(
                    $"Act I FAP clinic kit is missing authored root '{FapClinicKitRootName}'.");
        }

        var collisionNodes = FindDescendants<Node>(kitRoot)
            .Where(node => node is CollisionObject3D or CollisionShape3D)
            .ToArray();
        if (collisionNodes.Length > 0)
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException(
                $"Act I FAP clinic kit must be presentation-only; found {collisionNodes.Length} collision nodes.");
        }

        var components = FapClinicKitComponentNames
            .Select(componentName => (
                Name: componentName,
                Root: kitRoot.GetNodeOrNull<Node3D>(componentName)))
            .ToArray();
        var missingComponents = components
            .Where(component => component.Root is null)
            .Select(component => component.Name)
            .ToArray();
        if (missingComponents.Length > 0)
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException(
                $"Act I FAP clinic kit is missing required direct component roots: {string.Join('|', missingComponents)}.");
        }

        if (!Act1WorldLayout.TryGetPlacement("fap_clinic", out var placement))
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException("Connected Act I layout is missing fap_clinic placement.");
        }

        var branch = GetConnector("village-to-fap-branch");
        var front = HorizontalDirection(branch.End - placement.Origin);
        var side = new Vector3(front.Z, 0f, -front.X);
        var yaw = DirectionYaw(front);
        var origin = placement.Origin;

        // The facade and entry stay on the existing clinic parcel, with the
        // branch-facing apron kept clear so the authored landmark reads from
        // the approach without becoming a doorway or route blocker.
        var fapAuthoredFacade = AttachAct1ExteriorKitComponent(
            presentation,
            components[0].Root!,
            "FapAuthoredFacade",
            origin - front * 1.8f,
            yaw,
            Vector3.One,
            "fap_clinic@authored-facade",
            FapClinicKitScenePath);
        ApplyHeroWarmWindow(fapAuthoredFacade);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[1].Root!,
            "FapAuthoredEntryPorch",
            origin + front * 1.6f,
            yaw,
            Vector3.One,
            "fap_clinic@open-entry-porch",
            FapClinicKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[2].Root!,
            "FapAuthoredWayfindingBoard",
            origin + side * -4.7f + front * 3.2f,
            yaw,
            Vector3.One,
            "fap_clinic@blank-wayfinding-board",
            FapClinicKitScenePath);
        var wayfindingBoard = presentation.GetNode<Node3D>("FapAuthoredWayfindingBoard");
        var wayfindingLabel = new Label3D
        {
            Name = "FapWayfindingLabel",
            Text = "ФАП",
            Position = new Vector3(-0.62f, 1.49f, 0.15f),
            Modulate = Color.FromHtml("d2c29f"),
            OutlineModulate = Color.FromHtml("3c352b"),
            FontSize = 42,
            PixelSize = 0.0038f,
            OutlineSize = 4,
            DoubleSided = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled
        };
        wayfindingLabel.SetMeta("wayfindingLandmark", "fap");
        wayfindingLabel.SetMeta("presentationOnly", true);
        wayfindingLabel.SetMeta("visualOnly", true);
        wayfindingLabel.SetMeta("languageReview", "open; standard FAP abbreviation requires final Tatar/local-context review");
        wayfindingBoard.AddChild(wayfindingLabel);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[3].Root!,
            "FapAuthoredServiceShed",
            origin + side * 9.8f - front * 1.5f,
            yaw - 12f,
            Vector3.One,
            "fap_clinic@lateral-service-shed",
            FapClinicKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[4].Root!,
            "FapAuthoredServiceFence",
            origin + side * 9.8f + front * 0.4f,
            yaw + 90f,
            Vector3.One,
            "fap_clinic@lateral-service-fence",
            FapClinicKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[5].Root!,
            "FapAuthoredServiceGate",
            origin + side * 6.5f + front * 4.8f,
            yaw,
            Vector3.One,
            "fap_clinic@open-service-gate",
            FapClinicKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[6].Root!,
            "FapAuthoredBench",
            origin + side * -3.2f + front * 2.8f,
            yaw,
            Vector3.One,
            "fap_clinic@near-entry-bench",
            FapClinicKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[7].Root!,
            "FapAuthoredNoticeBoard",
            origin + side * -5.3f + front * 3.0f,
            yaw,
            Vector3.One,
            "fap_clinic@blank-notice-board",
            FapClinicKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[8].Root!,
            "FapAuthoredRainAwning",
            origin + side * 5.0f + front * 4.8f,
            yaw + 7f,
            Vector3.One,
            "fap_clinic@service-rain-awning",
            FapClinicKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[9].Root!,
            "FapAuthoredPathPuddles",
            origin + side * 1.2f + front * 3.3f,
            yaw,
            Vector3.One * 0.86f,
            "fap_clinic@near-mid-path-puddle-dressing",
            FapClinicKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[10].Root!,
            "FapAuthoredBirchShrubMass",
            origin + side * -14.0f - front * 1.0f,
            yaw,
            Vector3.One * 0.92f,
            "fap_clinic@lateral-birch-shrub-frame",
            FapClinicKitScenePath);

        // The old east-horizon single-slope box sits behind the service parcel
        // and is outside the branch route window. Replace its silhouette with
        // one small HouseA facade before suppressing the core primitive; the
        // existing FAP trees/fence keep the field edge layered without another
        // broad wall.
        var fapEastHorizonAnchor = origin + side * 18.0f - front * 7.0f;
        AddAuthoredHouse(
            presentation,
            "FapEastHorizonAuthoredHouse",
            fapEastHorizonAnchor + side * 0.8f - front * 0.6f + Vector3.Up * 0.95f,
            0.32f,
            yaw - 92f);

        // Replace only exact overlapping core presentation primitives. The
        // connector/route path, zone root, collision, navigation, interaction,
        // camera/light and RuntimeBridge owners stay visible and authoritative.
        HideCorePresentationNode(
            core,
            "FapExterior/FapClinicFullExteriorVolume",
            "authored clinic facade replaces the overlapping core clinic volume");
        HideCorePresentationNode(
            core,
            "FapExterior/FapClinicServiceStep",
            "authored entry porch replaces the overlapping core clinic step");
        HideCorePresentationNode(
            core,
            "FapExterior/FapClinicServiceBarnVolume",
            "authored service shed replaces the overlapping core service barn");
        HideCorePresentationNode(
            core,
            "FapExterior/FapEastNearStorageShed",
            "authored FAP service shed and east-horizon parcel replace the overlapping storage shed");
        HideCorePresentationNode(
            core,
            "FapExterior/FapEastHorizonHouse",
            "small authored east-horizon house replaces the remaining core box silhouette");
        // AddVisualFenceRun emits the rail and posts directly; it does not
        // create a boundary container node. Keep each fail-closed target
        // aligned with those exact visual-only children.
        foreach (var relativePath in new[]
                 {
                     "FapExterior/FapClinicWestBoundaryRail",
                     "FapExterior/FapClinicWestBoundaryPost0",
                     "FapExterior/FapClinicWestBoundaryPost1",
                     "FapExterior/FapClinicWestBoundaryPost2",
                     "FapExterior/FapClinicWestBoundaryPost3",
                     "FapExterior/FapClinicEastBoundaryRail",
                     "FapExterior/FapClinicEastBoundaryPost0",
                     "FapExterior/FapClinicEastBoundaryPost1",
                     "FapExterior/FapClinicEastBoundaryPost2",
                     "FapExterior/FapClinicEastBoundaryPost3",
                     "FapExterior/FapClinicServiceBoundaryRail",
                     "FapExterior/FapClinicServiceBoundaryPost0",
                     "FapExterior/FapClinicServiceBoundaryPost1",
                     "FapExterior/FapClinicServiceBoundaryPost2"
                 })
        {
            HideCorePresentationNode(
                core,
                relativePath,
                "authored service fence replaces the overlapping core boundary mesh");
        }
        HideCorePresentationNode(
            core,
            "FapExterior/FapClinicOpenServiceGate",
            "authored open service gate replaces the overlapping core gate cue");
        HideCorePresentationNode(
            core,
            "FapExterior/FapApproachWayfinding",
            "authored blank wayfinding board replaces the overlapping core landmark cue");
        HideCorePresentationNode(
            core,
            "FapExterior/FapClinicNearBirch",
            "authored birch/shrub mass replaces the overlapping core near-birch cue");

        importedRoot.QueueFree();
    }

    private static void BuildAct1ZiratRoadsideKit(Node3D core)
    {
        var ziratZone = core.GetNodeOrNull<Node3D>("ZiratMemoryField")
            ?? throw new InvalidOperationException(
                "Act I core world is missing the ZiratMemoryField presentation zone.");
        var presentation = new Node3D { Name = "ZiratRoadsideAuthoredKitPresentation" };
        presentation.SetMeta("presentationOnly", true);
        presentation.SetMeta("visualOnly", true);
        presentation.SetMeta("assetSource", ZiratRoadsideKitScenePath);
        presentation.SetMeta("authoredRoot", ZiratRoadsideKitRootName);
        presentation.SetMeta("componentContract", string.Join('|', ZiratRoadsideKitComponentNames));
        presentation.SetMeta("collisionOwner", "none");
        presentation.SetMeta("navigationOwner", "none");
        presentation.SetMeta("interactionOwner", "none");
        presentation.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        presentation.SetMeta(
            "culturalApproval",
            "open; neutral non-inscribed marker geometry requires cultural, religious and local-context review");
        presentation.SetMeta(
            "placementPolicy",
            "shoulders and ditch beside road; path and marker groups beyond route; fence and open gate as lateral boundary; birch and distant village masses as transition; route center remains open");
        ziratZone.AddChild(presentation);

        var packed = ResourceLoader.Load<PackedScene>(ZiratRoadsideKitScenePath)
            ?? throw new InvalidOperationException(
                $"Act I zirat roadside kit is missing: {ZiratRoadsideKitScenePath}.");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException(
                $"Act I zirat roadside kit did not instantiate: {ZiratRoadsideKitScenePath}.");
        var kitRoot = importedRoot;

        // Resolve the authored root before extraction so the neutral preview
        // arrangement offsets never become connected-world coordinates.
        if (!string.Equals(importedRoot.Name.ToString(), ZiratRoadsideKitRootName, StringComparison.Ordinal))
        {
            kitRoot = importedRoot.GetNodeOrNull<Node3D>(ZiratRoadsideKitRootName)
                ?? throw new InvalidOperationException(
                    $"Act I zirat roadside kit is missing authored root '{ZiratRoadsideKitRootName}'.");
        }

        var collisionNodes = FindDescendants<Node>(kitRoot)
            .Where(node => node is CollisionObject3D or CollisionShape3D)
            .ToArray();
        if (collisionNodes.Length > 0)
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException(
                $"Act I zirat roadside kit must be presentation-only; found {collisionNodes.Length} collision nodes.");
        }

        var components = ZiratRoadsideKitComponentNames
            .Select(componentName => (
                Name: componentName,
                Root: kitRoot.GetNodeOrNull<Node3D>(componentName)))
            .ToArray();
        var missingComponents = components
            .Where(component => component.Root is null)
            .Select(component => component.Name)
            .ToArray();
        if (missingComponents.Length > 0)
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException(
                $"Act I zirat roadside kit is missing required direct component roots: {string.Join('|', missingComponents)}.");
        }

        if (!Act1WorldLayout.TryGetPlacement("zirat_road", out var ziratPlacement))
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException("Connected Act I layout is missing zirat_road placement.");
        }

        var origin = ziratPlacement.Origin;
        var roadAnchor = origin + new Vector3(0f, 0f, -1.5f);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[0].Root!,
            "ZiratWetRoadShoulderLeft",
            roadAnchor,
            0f,
            Vector3.One * 1.05f,
            "zirat_road@wet-shoulder-left",
            ZiratRoadsideKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[1].Root!,
            "ZiratWetRoadShoulderRight",
            roadAnchor,
            0f,
            Vector3.One * 1.05f,
            "zirat_road@wet-shoulder-right",
            ZiratRoadsideKitScenePath);

        var ziratRoadBandMaterial = PainterlyMaterialLibrary.ForColor("56544a", "earth");
        foreach (var relativePath in new[]
                 {
                     "ZiratWetRoadShoulderLeft/WetRoadShoulder_Left/WetRoadShoulder_Left_RoadBand_LOD0",
                     "ZiratWetRoadShoulderRight/WetRoadShoulder_Right/WetRoadShoulder_Right_RoadBand_LOD0"
                 })
        {
            var roadBand = presentation.GetNodeOrNull<MeshInstance3D>(relativePath)
                ?? throw new InvalidOperationException(
                    $"Act I zirat roadside kit is missing exact RoadBand LOD0 mesh: {relativePath}.");
            roadBand.MaterialOverride = ziratRoadBandMaterial;
        }

        AttachAct1ExteriorKitComponent(
            presentation,
            components[2].Root!,
            "ZiratRoadsideDitch",
            roadAnchor + new Vector3(0f, 0f, 0.4f),
            0f,
            Vector3.One,
            "zirat_road@roadside-drainage",
            ZiratRoadsideKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[3].Root!,
            "ZiratCulvertStoneCluster",
            origin + new Vector3(0f, 0f, -2.0f),
            0f,
            Vector3.One,
            "zirat_road@culvert-edge",
            ZiratRoadsideKitScenePath);

        // The authored boundary runs along the right side of the route. Its
        // lateral placement keeps the fence/gate out of the first-person road
        // window; the gate is an open presentation cue, never a blocker.
        var boundaryAnchor = origin + new Vector3(6.7f, 0f, -15.0f);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[4].Root!,
            "ZiratAuthoredBoundaryFence",
            boundaryAnchor,
            90f,
            Vector3.One * 0.96f,
            "zirat_road@lateral-boundary-fence",
            ZiratRoadsideKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[5].Root!,
            "ZiratAuthoredOpenGate",
            boundaryAnchor,
            90f,
            Vector3.One * 0.96f,
            "zirat_road@lateral-open-gate",
            ZiratRoadsideKitScenePath);

        var markerAnchor = origin + new Vector3(6.2f, 0f, -9.0f);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[6].Root!,
            "ZiratAuthoredMarkerGroupLow",
            markerAnchor,
            0f,
            Vector3.One * 0.92f,
            "zirat_road@quiet-marker-group-near",
            ZiratRoadsideKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[7].Root!,
            "ZiratAuthoredMarkerGroupFar",
            origin + new Vector3(6.5f, 0f, -10.0f),
            0f,
            Vector3.One * 0.92f,
            "zirat_road@quiet-marker-group-far",
            ZiratRoadsideKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[8].Root!,
            "ZiratAuthoredPathEdge",
            origin + new Vector3(-4.0f, 0f, -10.0f),
            0f,
            Vector3.One * 0.96f,
            "zirat_road@side-path-edge",
            ZiratRoadsideKitScenePath);

        AttachAct1ExteriorKitComponent(
            presentation,
            components[9].Root!,
            "ZiratAuthoredBirchShrubTransition",
            origin + new Vector3(7.8f, 0f, -22.0f),
            0f,
            Vector3.One * 0.94f,
            "zirat_road@birch-forest-transition",
            ZiratRoadsideKitScenePath);
        var eastDistantVillageTransition = components[10].Root!.Duplicate() as Node3D
            ?? throw new InvalidOperationException(
                "Act I zirat roadside kit distant village component could not be duplicated.");
        eastDistantVillageTransition.Name = "ZiratDistantVillageMassEastSource";
        kitRoot.AddChild(eastDistantVillageTransition);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[10].Root!,
            "ZiratAuthoredDistantVillageTransition",
            origin + new Vector3(-22.0f, 0f, 10.0f),
            0f,
            Vector3.One,
            "zirat_road@distant-village-transition",
            ZiratRoadsideKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            eastDistantVillageTransition,
            "ZiratAuthoredDistantVillageTransitionEast",
            origin + new Vector3(22.0f, 0f, 10.0f),
            0f,
            Vector3.One,
            "zirat_road@distant-village-transition-east",
            ZiratRoadsideKitScenePath);

        // Only the old core marker grouping is an exact presentation overlap:
        // the authored low/far groups become the zirat's quiet marker read.
        // Route envelopes, traversal surfaces, zone nodes and gameplay owners
        // remain visible and authoritative.
        HideCorePresentationNode(
            core,
            "ZiratMemoryField/ZiratCoreMarkerGrouping",
            "authored low/far marker groups replace the overlapping core marker presentation");

        importedRoot.QueueFree();
    }

    private static void BuildAct1KaraForestEdgeKit(Node3D core)
    {
        var forestZone = core.GetNodeOrNull<Node3D>("KaraForestEdge")
            ?? throw new InvalidOperationException(
                "Act I core world is missing the KaraForestEdge presentation zone.");
        var presentation = new Node3D { Name = "KaraForestEdgeAuthoredKitPresentation" };
        presentation.SetMeta("presentationOnly", true);
        presentation.SetMeta("visualOnly", true);
        presentation.SetMeta("assetSource", KaraForestEdgeKitScenePath);
        presentation.SetMeta("authoredRoot", KaraForestEdgeKitRootName);
        presentation.SetMeta("componentContract", string.Join('|', KaraForestEdgeKitComponentNames));
        presentation.SetMeta("collisionOwner", "none");
        presentation.SetMeta("navigationOwner", "none");
        presentation.SetMeta("interactionOwner", "none");
        presentation.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        presentation.SetMeta(
            "placementPolicy",
            "side banks/root walls/trees; below-sight-line ground breakup; distant masses beyond the road; central road window preserved");
        forestZone.AddChild(presentation);

        var packed = ResourceLoader.Load<PackedScene>(KaraForestEdgeKitScenePath)
            ?? throw new InvalidOperationException(
                $"Act I Kara forest-edge kit is missing: {KaraForestEdgeKitScenePath}.");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException(
                $"Act I Kara forest-edge kit did not instantiate: {KaraForestEdgeKitScenePath}.");
        var kitRoot = importedRoot;

        // Resolve the authored root before extraction so the neutral preview
        // board translation never becomes a connected-world coordinate.
        if (!string.Equals(importedRoot.Name.ToString(), KaraForestEdgeKitRootName, StringComparison.Ordinal))
        {
            kitRoot = importedRoot.GetNodeOrNull<Node3D>(KaraForestEdgeKitRootName)
                ?? throw new InvalidOperationException(
                    $"Act I Kara forest-edge kit is missing authored root '{KaraForestEdgeKitRootName}'.");
        }

        var collisionNodes = FindDescendants<Node>(kitRoot)
            .Where(node => node is CollisionObject3D or CollisionShape3D)
            .ToArray();
        if (collisionNodes.Length > 0)
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException(
                $"Act I Kara forest-edge kit must be presentation-only; found {collisionNodes.Length} collision nodes.");
        }

        var components = KaraForestEdgeKitComponentNames
            .Select(componentName => (
                Name: componentName,
                Root: kitRoot.GetNodeOrNull<Node3D>(componentName)))
            .ToArray();
        var missingComponents = components
            .Where(component => component.Root is null)
            .Select(component => component.Name)
            .ToArray();
        if (missingComponents.Length > 0)
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException(
                $"Act I Kara forest-edge kit is missing required direct component roots: {string.Join('|', missingComponents)}.");
        }

        if (!Act1WorldLayout.TryGetPlacement("kara_urman_night", out var karaPlacement))
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException("Connected Act I layout is missing kara_urman_night placement.");
        }

        var origin = karaPlacement.Origin;
        AttachAct1ExteriorKitComponent(
            presentation,
            components[0].Root!,
            "KaraForestBankLeft",
            origin + new Vector3(-6.2f, 0f, 3.0f),
            -12f,
            Vector3.One,
            "kara_urman_night@west-road-bank",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[1].Root!,
            "KaraForestBankRight",
            origin + new Vector3(6.6f, 0f, -2.0f),
            18f,
            Vector3.One * 0.98f,
            "kara_urman_night@east-road-bank",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[2].Root!,
            "KaraMixedTreeClusterLeft",
            origin + new Vector3(-10.0f, 0f, -3.5f),
            -18f,
            Vector3.One * 0.96f,
            "kara_urman_night@west-side-tree-mass",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[3].Root!,
            "KaraMixedTreeClusterRight",
            origin + new Vector3(10.0f, 0f, -8.5f),
            22f,
            Vector3.One * 0.94f,
            "kara_urman_night@east-side-tree-mass",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[4].Root!,
            "KaraCrookedPineMass",
            origin + new Vector3(-11.0f, 0f, -10.0f),
            -14f,
            Vector3.One,
            "kara_urman_night@west-side-crooked-pine",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[5].Root!,
            "KaraBirchEdgeMass",
            origin + new Vector3(11.0f, 0f, -14.0f),
            17f,
            Vector3.One * 0.96f,
            "kara_urman_night@east-side-birch-edge",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[6].Root!,
            "KaraRootWallLeft",
            origin + new Vector3(-5.5f, 0f, -1.5f),
            -12f,
            Vector3.One * 0.96f,
            "kara_urman_night@west-root-wall",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[7].Root!,
            "KaraRootWallRight",
            origin + new Vector3(5.5f, 0f, -6.2f),
            20f,
            Vector3.One * 0.92f,
            "kara_urman_night@east-root-wall",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[8].Root!,
            "KaraFallenLogCluster",
            origin + new Vector3(-4.8f, 0f, -5.8f),
            28f,
            Vector3.One * 0.82f,
            "kara_urman_night@west-ground-breakup",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[9].Root!,
            "KaraMossyBoulderCluster",
            origin + new Vector3(4.6f, 0f, -3.8f),
            -20f,
            Vector3.One * 0.86f,
            "kara_urman_night@east-ground-breakup",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[10].Root!,
            "KaraCrookedStump",
            origin + new Vector3(-4.6f, 0f, -7.2f),
            10f,
            Vector3.One * 0.88f,
            "kara_urman_night@west-ground-landmark",
            KaraForestEdgeKitScenePath);
        // The far silhouettes sit just outside the road window: close the
        // horizon with authored tree masses while preserving the central walk
        // line and the fixed-camera reverse-turn envelope.
        AttachAct1ExteriorKitComponent(
            presentation,
            components[11].Root!,
            "KaraDistantForestMassLow",
            origin + new Vector3(-13.5f, 0f, -30.0f),
            -8f,
            Vector3.One * 0.90f,
            "kara_urman_night@far-west-window-mass",
            KaraForestEdgeKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[12].Root!,
            "KaraDistantForestMassTall",
            origin + new Vector3(13.5f, 0f, -32.0f),
            11f,
            Vector3.One * 0.72f,
            "kara_urman_night@far-east-window-mass",
            KaraForestEdgeKitScenePath);

        RegradeKaraEdgeMaterials(presentation);

        // The authored tree kits include a few thin preview bough meshes that
        // read as detached black lines from the fixed first-person review
        // angles. Keep trunks/crowns and suppress only those loose accents.
        var looseBoughCount = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(presentation))
        {
            var name = mesh.Name.ToString();
            if (!name.StartsWith("MixedTreeCluster_Left_AngledBough_", StringComparison.Ordinal)
                && !name.StartsWith("MixedTreeCluster_Right_AngledBough_", StringComparison.Ordinal)
                && !name.StartsWith("CrookedPineMass_SideBough_", StringComparison.Ordinal)
                && !name.StartsWith("BirchEdgeMass_FineBranch_", StringComparison.Ordinal))
            {
                continue;
            }

            mesh.Hide();
            looseBoughCount++;
        }

        presentation.SetMeta("suppressedLooseKaraBoughCount", looseBoughCount);

        // Static composition evidence above keeps every near/mid tree and
        // ground prop outside the 3.5 m road envelope. Hide only the exact
        // core presentation primitives replaced by those authored reads; the
        // road envelope, zone root, legacy owners and gameplay nodes are not
        // suppression targets. The central far-road window remains open.
        foreach (var (relativePath, reason) in new (string RelativePath, string Reason)[]
                 {
                     ("KaraForestEdge/KaraAsymmetricWestBank", "authored left forest bank replaces the overlapping core primitive bank"),
                     ("KaraForestEdge/KaraAsymmetricEastBank", "authored right forest bank replaces the overlapping core primitive bank"),
                     ("KaraForestEdge/KaraRootBankWest", "authored left root wall replaces the overlapping core root cluster"),
                     ("KaraForestEdge/KaraRootBankEast", "authored right root wall replaces the overlapping core root cluster"),
                     ("KaraForestEdge/KaraThresholdRootWest", "authored left threshold root wall replaces the overlapping core root cluster"),
                     ("KaraForestEdge/KaraLateralForestShelfWest", "authored forest bank replaces the overlapping west lateral mass"),
                     ("KaraForestEdge/KaraLateralForestShelfEast", "authored forest bank replaces the overlapping east lateral mass"),
                     ("KaraForestEdge/KaraMixedMassWestNear", "authored left mixed tree cluster replaces the overlapping core tree mass"),
                     ("KaraForestEdge/KaraMixedMassEastNear", "authored right mixed tree cluster replaces the overlapping core tree mass"),
                     ("KaraForestEdge/KaraMixedMassWestMid", "authored left mixed tree cluster replaces the overlapping core tree mass"),
                     ("KaraForestEdge/KaraMixedMassEastMid", "authored right mixed tree cluster replaces the overlapping core tree mass"),
                     ("KaraForestEdge/KaraDistantEdgeWindowWest", "authored far forest mass replaces the overlapping west window primitive"),
                     ("KaraForestEdge/KaraDistantEdgeWindowEast", "authored far forest mass replaces the overlapping east window primitive"),
                     ("KaraForestEdge/KaraDistantForestEdge", "authored far masses preserve a central road window instead of a primitive front wall")
                 })
        {
            HideCorePresentationNode(core, relativePath, reason);
        }

        importedRoot.QueueFree();
    }

    private static void RegradeKaraEdgeMaterials(Node3D presentation)
    {
        var grade = new Dictionary<string, Material>(StringComparer.Ordinal)
        {
            ["DampEarth"] = PainterlyMaterialLibrary.ForColor("34443b", "earth"),
            ["LeafLitter"] = PainterlyMaterialLibrary.ForColor("51493b", "earth"),
            ["PineBark"] = PainterlyMaterialLibrary.ForColor("40352d", "wood_bark"),
            ["WeatheredWood"] = PainterlyMaterialLibrary.ForColor("594a39", "wood"),
            ["CutWood"] = PainterlyMaterialLibrary.ForColor("8b7155", "wood"),
            ["PineFoliage"] = PainterlyMaterialLibrary.ForColor("30483f", "foliage"),
            ["FoliageBlueGreen"] = PainterlyMaterialLibrary.ForColor("48553f", "foliage"),
            ["BirchBark"] = PainterlyMaterialLibrary.ForColor("68705a", "wood_bark"),
            ["BirchLeaves"] = PainterlyMaterialLibrary.ForColor("596047", "foliage"),
            ["Understory"] = PainterlyMaterialLibrary.ForColor("3c4d3e", "foliage"),
            ["RootDark"] = PainterlyMaterialLibrary.ForColor("3f332a", "wood_bark"),
            ["MossGreen"] = PainterlyMaterialLibrary.ForColor("596047", "foliage"),
            ["MossyStone"] = PainterlyMaterialLibrary.ForColor("62675c", "stone"),
            ["DistantBlueGreen"] = PainterlyMaterialLibrary.ForColor("2c403b", "foliage"),
            ["DistantFoliage"] = PainterlyMaterialLibrary.ForColor("243a34", "foliage"),
            ["DistantBark"] = PainterlyMaterialLibrary.ForColor("343630", "wood_bark")
        };

        var rebound = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(presentation))
        {
            var source = mesh.GetActiveMaterial(0);
            var sourceName = source?.ResourceName ?? string.Empty;
            if (!grade.TryGetValue(sourceName, out var material) || mesh.Mesh is null)
            {
                continue;
            }

            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                mesh.SetSurfaceOverrideMaterial(surface, material);
                rebound++;
            }
        }

        presentation.SetMeta("materialGrade", "Kara damp earth/leaf litter/understory remapped to painterly wet palette");
        presentation.SetMeta("materialGradeReboundCount", rebound);
    }

    private static void RebindWetVillageRoadMaterials(Node3D kitRoot)
    {
        var grade = new Dictionary<string, Material>(StringComparer.Ordinal)
        {
            ["WetRoad_RutDark"] = PainterlyMaterialLibrary.ForColor("343d37", "earth"),
            ["WetRoad_MutedOchre"] = PainterlyMaterialLibrary.ForColor("56544a", "earth"),
            ["WetRoad_WornLight"] = PainterlyMaterialLibrary.ForColor("625f54", "earth"),
            ["MuddyShoulder_ClayBreak"] = PainterlyMaterialLibrary.ForColor("625647", "earth"),
            ["MuddyShoulder_WetBrown"] = PainterlyMaterialLibrary.ForColor("4e4c43", "earth"),
            ["Ditch_DampGreenBrown"] = PainterlyMaterialLibrary.ForColor("475247", "earth"),
            ["Puddle_MutedGlint"] = PainterlyMaterialLibrary.ForColor("4d5b56", "water"),
            ["Puddle_ShallowBlueGreen"] = PainterlyMaterialLibrary.ForColor("404f4c", "water"),
            ["Ditch_StillWater"] = PainterlyMaterialLibrary.ForColor("384742", "water")
        };

        var rebound = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(kitRoot))
        {
            if (mesh.Mesh is null)
            {
                continue;
            }

            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                var source = mesh.Mesh.SurfaceGetMaterial(surface);
                var sourceName = source?.ResourceName ?? string.Empty;
                if (!grade.TryGetValue(sourceName, out var material))
                {
                    continue;
                }

                mesh.SetSurfaceOverrideMaterial(surface, material);
                rebound++;
            }
        }

        kitRoot.SetMeta("wetRoadMaterialGrade", "muted painterly earth and dark blue-green water for road crown, worn patches, shoulders, ditches and puddles");
        kitRoot.SetMeta("wetRoadMaterialGradeReboundCount", rebound);
    }

    private static void BuildAct1AuthoredExteriorKit(Node3D core)
    {
        var presentation = new Node3D { Name = "Act1AuthoredExteriorKitPresentation" };
        presentation.SetMeta("presentationOnly", true);
        presentation.SetMeta("visualOnly", true);
        presentation.SetMeta("assetSource", VillageExteriorKitScenePath);
        presentation.SetMeta("collisionOwner", "none");
        presentation.SetMeta("navigationOwner", "none");
        presentation.SetMeta("interactionOwner", "none");
        presentation.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        presentation.SetMeta(
            "placementPolicy",
            "extract named authored roots, clear preview-board offsets, and place only in declared Act I parcels");
        core.AddChild(presentation);

        var packed = ResourceLoader.Load<PackedScene>(VillageExteriorKitScenePath)
            ?? throw new InvalidOperationException(
                $"Act I village exterior kit is missing: {VillageExteriorKitScenePath}.");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException(
                $"Act I village exterior kit did not instantiate: {VillageExteriorKitScenePath}.");
        var kitRoot = importedRoot;

        // Godot may expose the authored GLB root directly or retain an import
        // wrapper. Resolve the named authored root before extracting its
        // components so the neutral preview-board translation never becomes a
        // connected-world coordinate.
        if (!string.Equals(importedRoot.Name.ToString(), VillageExteriorKitRootName, StringComparison.Ordinal))
        {
            kitRoot = importedRoot.GetNodeOrNull<Node3D>(VillageExteriorKitRootName)
                ?? throw new InvalidOperationException(
                    $"Act I village exterior kit is missing authored root '{VillageExteriorKitRootName}'.");
        }

        var collisionNodes = FindDescendants<Node>(kitRoot)
            .Where(node => node is CollisionObject3D or CollisionShape3D)
            .ToArray();
        if (collisionNodes.Length > 0)
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException(
                $"Act I village exterior kit must be presentation-only; found {collisionNodes.Length} collision nodes.");
        }

        var components = VillageExteriorKitComponentNames
            .Select(componentName => (
                Name: componentName,
                Root: kitRoot.GetNodeOrNull<Node3D>(componentName)))
            .ToArray();
        var missingComponents = components
            .Where(component => component.Root is null)
            .Select(component => component.Name)
            .ToArray();
        if (missingComponents.Length > 0)
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException(
                $"Act I village exterior kit is missing required direct component roots: {string.Join('|', missingComponents)}.");
        }

        if (!Act1WorldLayout.TryGetPlacement("house_old_pc", out var housePlacement))
        {
            importedRoot.QueueFree();
            throw new InvalidOperationException("Connected Act I layout is missing house_old_pc placement.");
        }

        var houseApproach = HorizontalDirection(GetConnector("arrival-to-house-yard").End - housePlacement.Origin);
        var houseSide = new Vector3(houseApproach.Z, 0f, -houseApproach.X);
        var houseYaw = DirectionYaw(houseApproach);

        // The six authored roots are extracted once from the neutral source
        // board. Their preview-board origins are removed before a stable
        // placement node owns the world-space translation, yaw and scale.
        var babaiApproachFacade = AttachAct1ExteriorKitComponent(
            presentation,
            components[0].Root!,
            "BabaiApproachDwellingFacade",
            housePlacement.Origin + new Vector3(0f, 0f, -3.4f),
            houseYaw,
            Vector3.One * 0.82f,
            "house_old_pc@babai-approach");
        ApplyHeroWarmWindow(babaiApproachFacade);
        AddBabaiRearFacadeDressing(babaiApproachFacade, Vector3.Zero, 0f);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[1].Root!,
            "BabaiYardAuthoredShed",
            housePlacement.Origin + houseSide * 7.0f + houseApproach * 3.8f,
            houseYaw - 18f,
            Vector3.One * 0.86f,
            "house_old_pc@yard-return-parcel");
        AttachAct1ExteriorKitComponent(
            presentation,
            components[2].Root!,
            "BabaiYardAuthoredFence",
            housePlacement.Origin + houseSide * -11.5f + houseApproach * 0.5f,
            houseYaw - 90f,
            new Vector3(1.85f, 0.82f, 1f),
            "house_old_pc@west-yard-boundary");
        AttachAct1ExteriorKitComponent(
            presentation,
            components[3].Root!,
            "BabaiYardAuthoredGate",
            housePlacement.Origin + houseApproach * 6.0f,
            houseYaw,
            Vector3.One * 0.92f,
            "house_old_pc@yard-entry");
        AttachAct1ExteriorKitComponent(
            presentation,
            components[4].Root!,
            "BabaiYardAuthoredWoodpile",
            housePlacement.Origin + houseSide * -5.7f - houseApproach * 3.0f,
            houseYaw + 90f,
            Vector3.One * 1.05f,
            "house_old_pc@yard-firewood");
        AttachAct1ExteriorKitComponent(
            presentation,
            components[5].Root!,
            "MainStreetArrivalAuthoredWell",
            new Vector3(-6.8f, 0f, 4.6f),
            0f,
            Vector3.One * 0.70f,
            "village_day@arrival-main-street-landmark");

        // One authored near/mid pass closes the first-person arrival and the
        // two most exposed lateral turns. These replace the already-suppressed
        // core volumes at their real anchors; the center road and all route
        // owners remain untouched.
        var arrivalParcels = AddAct1ExteriorParcelSubmount(
            presentation,
            "ArrivalAuthoredParcels",
            Vector3.Zero,
            "village_day@arrival-authored-parcels",
            "near houses, gates and broken boundaries frame the wet entry without closing the road window");
        AddAct1AuthoredExteriorParcel(
            arrivalParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ArrivalWestNearAuthoredFacade",
                new(-12.8f, 0f, 25.8f),
                176f,
                Vector3.One * 0.78f,
                "village_day@arrival-west-near-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "Gate_CrookedTimber",
                "ArrivalWestNearAuthoredGate",
                new(-8.4f, 0f, 22.0f),
                176f,
                Vector3.One * 0.84f,
                "village_day@arrival-west-near-gate"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalWestNearAuthoredFence",
                new(-10.1f, 0f, 23.0f),
                8f,
                new Vector3(1.30f, 0.72f, 1f),
                "village_day@arrival-west-near-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalWestNearAuthoredShed",
                new(-17.0f, 0f, 30.4f),
                170f,
                Vector3.One * 0.54f,
                "village_day@arrival-west-near-shed"));
        AddAct1AuthoredExteriorParcel(
            arrivalParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ArrivalEastNearAuthoredFacade",
                new(12.8f, 0f, 26.2f),
                184f,
                Vector3.One * 0.74f,
                "village_day@arrival-east-near-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalEastNearAuthoredShed",
                new(11.8f, 0f, 25.0f),
                184f,
                Vector3.One * 0.62f,
                "village_day@arrival-east-near-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalEastNearAuthoredFence",
                new(10.2f, 0f, 23.1f),
                172f,
                new Vector3(1.22f, 0.68f, 1f),
                "village_day@arrival-east-near-fence"));

        // The fixed arrival-forward camera starts at z=9 and looks down the
        // road to z=-10. The original near parcels above close the reverse
        // tail, so keep them as the replacement for the suppressed arrival
        // blockout and add one staggered forward pair on the actual visible
        // shoulders. They stay outside the shared road envelope (x ~= +/-6)
        // and give the first-person entry a near/mid village read.
        AddAct1AuthoredExteriorParcel(
            arrivalParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ArrivalForwardWestFacade",
                new(-8.8f, 0f, 0.2f),
                90f,
                Vector3.One * 0.66f,
                "village_day@arrival-forward-west-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "Gate_CrookedTimber",
                "ArrivalForwardWestGate",
                new(-8.4f, 0f, -3.2f),
                4f,
                Vector3.One * 0.72f,
                "village_day@arrival-forward-west-gate"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalForwardWestFence",
                new(-10.0f, 0f, -2.1f),
                8f,
                new Vector3(1.18f, 0.64f, 1f),
                "village_day@arrival-forward-west-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalForwardWestShed",
                new(-14.8f, 0f, -10.8f),
                8f,
                Vector3.One * 0.46f,
                "village_day@arrival-forward-west-shed"));
        AddAct1AuthoredExteriorParcel(
            arrivalParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ArrivalForwardEastFacade",
                new(12.6f, 0f, 1.1f),
                -4f,
                Vector3.One * 0.58f,
                "village_day@arrival-forward-east-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalForwardEastShed",
                new(14.2f, 0f, -8.2f),
                -8f,
                Vector3.One * 0.44f,
                "village_day@arrival-forward-east-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalForwardEastFence",
                new(10.0f, 0f, -2.0f),
                -8f,
                new Vector3(1.12f, 0.62f, 1f),
                "village_day@arrival-forward-east-fence"));
        AddAct1AuthoredExteriorParcel(
            arrivalParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ArrivalFarWestFacade",
                new(-12.0f, 0f, 49.0f),
                168f,
                Vector3.One * 0.44f,
                "village_day@arrival-far-west-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalFarWestShed",
                new(-8.5f, 0f, 52.0f),
                160f,
                Vector3.One * 0.32f,
                "village_day@arrival-far-west-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalFarWestFence",
                new(-15.0f, 0f, 50.0f),
                10f,
                new Vector3(1.08f, 0.46f, 1f),
                "village_day@arrival-far-west-fence"));

        if (Act1WorldLayout.TryGetPlacement("house_old_pc", out var authoredHousePlacement))
        {
            var authoredHouseApproach = HorizontalDirection(
                GetConnector("arrival-to-house-yard").End - authoredHousePlacement.Origin);
            var authoredHouseSide = new Vector3(authoredHouseApproach.Z, 0f, -authoredHouseApproach.X);
            var authoredHouseYaw = DirectionYaw(authoredHouseApproach);
            var babaiYardParcels = AddAct1ExteriorParcelSubmount(
                presentation,
                "BabaiYardAuthoredParcels",
                Vector3.Zero,
                "house_old_pc@authored-yard-parcels",
                "lived-in yard depth uses a side dwelling, shed, fence and well while keeping the hero threshold open");
            AddAct1AuthoredExteriorParcel(
                babaiYardParcels,
                new Act1ExteriorParcelComponentPlacement(
                    "DwellingFacade_TimberPlaster",
                    "BabaiYardWestDepthFacade",
                    authoredHousePlacement.Origin + authoredHouseSide * -10.0f - authoredHouseApproach * 3.8f,
                    authoredHouseYaw + 12f,
                    Vector3.One * 0.48f,
                    "house_old_pc@west-yard-depth-facade"),
                new Act1ExteriorParcelComponentPlacement(
                    "OutbuildingShed_Low",
                    "BabaiYardWestDepthShed",
                    authoredHousePlacement.Origin + authoredHouseSide * -7.0f - authoredHouseApproach * 1.0f,
                    authoredHouseYaw + 8f,
                    Vector3.One * 0.58f,
                    "house_old_pc@west-yard-depth-shed"),
                new Act1ExteriorParcelComponentPlacement(
                    "FenceSegment_RoughPicket",
                    "BabaiYardWestDepthFence",
                    authoredHousePlacement.Origin + authoredHouseSide * -9.5f + authoredHouseApproach * 1.8f,
                    authoredHouseYaw + 90f,
                    new Vector3(1.35f, 0.68f, 1f),
                    "house_old_pc@west-yard-depth-fence"));
            AddAct1AuthoredExteriorParcel(
                babaiYardParcels,
                new Act1ExteriorParcelComponentPlacement(
                    "Well_YardLandmark",
                    "BabaiYardAuthoredWell",
                    authoredHousePlacement.Origin + authoredHouseSide * 2.8f + authoredHouseApproach * 4.0f,
                    authoredHouseYaw,
                    Vector3.One * 0.82f,
                    "house_old_pc@yard-well-landmark"),
                new Act1ExteriorParcelComponentPlacement(
                    "FenceSegment_RoughPicket",
                    "BabaiYardEastDepthFence",
                    authoredHousePlacement.Origin + authoredHouseSide * 8.8f - authoredHouseApproach * 2.4f,
                    authoredHouseYaw - 90f,
                    new Vector3(1.15f, 0.58f, 1f),
                    "house_old_pc@east-yard-depth-fence"));

            // The yard review stop looks across the open shoulder from
            // (-20, 6.5) toward the village edge. Keep the hero threshold and
            // walk chain untouched; these three small authored cues sit well
            // outside that corridor and make the visible yard read inhabited.
            AddAct1AuthoredExteriorParcel(
                babaiYardParcels,
                new Act1ExteriorParcelComponentPlacement(
                    "Well_YardLandmark",
                    "BabaiYardForwardShoulderWell",
                    new(-17.2f, 0f, 6.8f),
                    8f,
                    Vector3.One * 0.68f,
                    "house_old_pc@forward-yard-shoulder-well"),
                new Act1ExteriorParcelComponentPlacement(
                    "FenceSegment_RoughPicket",
                    "BabaiYardForwardShoulderFence",
                    new(-18.7f, 0f, 8.2f),
                    14f,
                    new Vector3(1.12f, 0.56f, 1f),
                    "house_old_pc@forward-yard-shoulder-fence"));
            AddAct1AuthoredExteriorParcel(
                babaiYardParcels,
                new Act1ExteriorParcelComponentPlacement(
                    "OutbuildingShed_Low",
                    "BabaiYardForwardShoulderShed",
                    new(-14.5f, 0f, 10.2f),
                    16f,
                    Vector3.One * 0.48f,
                    "house_old_pc@forward-yard-shoulder-shed"));
        }

        if (Act1WorldLayout.TryGetPlacement("zirat_road", out var authoredZiratPlacement))
        {
            // The village-facing edge of zirat is a transition, not a void:
            // one weathered dwelling and one low service shed establish the
            // last inhabited parcel before the markers while leaving the
            // cemetery road and its cultural silhouette open.
            var ziratEdgeParcels = AddAct1ExteriorParcelSubmount(
                presentation,
                "ZiratVillageEdgeAuthoredParcels",
                Vector3.Zero,
                "zirat_road@village-edge-authored-parcels",
                "quiet village-to-zirat boundary; authored structures sit outside the road and marker field");
            AddAct1AuthoredExteriorParcel(
                ziratEdgeParcels,
                new Act1ExteriorParcelComponentPlacement(
                    "DwellingFacade_TimberPlaster",
                    "ZiratVillageEdgeWestFacade",
                    authoredZiratPlacement.Origin + new Vector3(-14.8f, 0f, 22.5f),
                    90f,
                    Vector3.One * 0.46f,
                    "zirat_road@village-edge-west-facade"),
                new Act1ExteriorParcelComponentPlacement(
                    "FenceSegment_RoughPicket",
                    "ZiratVillageEdgeWestFence",
                    authoredZiratPlacement.Origin + new Vector3(-10.5f, 0f, 20.2f),
                    4f,
                    new Vector3(1.35f, 0.58f, 1f),
                    "zirat_road@village-edge-west-fence"),
                new Act1ExteriorParcelComponentPlacement(
                    "OutbuildingShed_Low",
                    "ZiratVillageEdgeEastShed",
                    authoredZiratPlacement.Origin + new Vector3(13.8f, 0f, 21.0f),
                    -90f,
                    Vector3.One * 0.52f,
                    "zirat_road@village-edge-east-shed"));
            AddAct1AuthoredExteriorParcel(
                ziratEdgeParcels,
                new Act1ExteriorParcelComponentPlacement(
                    "FenceSegment_RoughPicket",
                    "ZiratVillageEdgeEastFence",
                    authoredZiratPlacement.Origin + new Vector3(10.2f, 0f, 19.3f),
                    -6f,
                    new Vector3(1.18f, 0.52f, 1f),
                    "zirat_road@village-edge-east-fence"));

            // The village-facing replacements above also close the reverse
            // view behind the zirat entry. Add a low, side-boundary parcel in
            // the actual forward ray (camera z=-56 -> target z=-75); the
            // existing birch/broadleaf line at x +/-14 remains visible behind
            // it, while the centre markers and quiet path stay unobstructed.
            AddAct1AuthoredExteriorParcel(
                ziratEdgeParcels,
                new Act1ExteriorParcelComponentPlacement(
                    "DwellingFacade_TimberPlaster",
                    "ZiratForwardEdgeWestFacade",
                    new(-13.2f, 0f, -63.2f),
                    86f,
                    Vector3.One * 0.34f,
                    "zirat_road@forward-edge-west-facade"),
                new Act1ExteriorParcelComponentPlacement(
                    "FenceSegment_RoughPicket",
                    "ZiratForwardEdgeWestFence",
                    new(-9.5f, 0f, -61.8f),
                    6f,
                    new Vector3(1.06f, 0.46f, 1f),
                    "zirat_road@forward-edge-west-fence"));
            AddAct1AuthoredExteriorParcel(
                ziratEdgeParcels,
                new Act1ExteriorParcelComponentPlacement(
                    "OutbuildingShed_Low",
                    "ZiratForwardEdgeEastShed",
                    new(13.8f, 0f, -65.8f),
                    -86f,
                    Vector3.One * 0.34f,
                    "zirat_road@forward-edge-east-shed"),
                new Act1ExteriorParcelComponentPlacement(
                    "FenceSegment_RoughPicket",
                    "ZiratForwardEdgeEastFence",
                    new(10.0f, 0f, -64.2f),
                    -6f,
                    new Vector3(1.04f, 0.44f, 1f),
                    "zirat_road@forward-edge-east-fence"));
        }

        BuildAct1AuthoredExteriorNeighborParcels(core, presentation);

        // The authored neighbor groups frame the route center, but their
        // sightlines still open into empty perimeter fields on a 180-degree
        // turn. Reuse the same GLB parcel components for a few restrained
        // far-side homes; these are presentation-only silhouettes, not a new
        // gameplay or collision owner.
        var perimeterParcels = AddAct1ExteriorParcelSubmount(
            presentation,
            "DistantPerimeterParcels",
            Vector3.Zero,
            "village_day@distant-perimeter",
            "far-side authored homes close side/back horizons without entering the route window");
        AddAct1AuthoredExteriorParcel(
            perimeterParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "PerimeterWestArrivalFacade",
                new(-22.0f, 0f, 38.0f),
                90f,
                Vector3.One * 0.52f,
                "village_day@perimeter-west-arrival-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "PerimeterWestArrivalShed",
                new(-19.0f, 0f, 33.5f),
                82f,
                Vector3.One * 0.44f,
                "village_day@perimeter-west-arrival-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "PerimeterWestArrivalFence",
                new(-24.0f, 0f, 34.0f),
                0f,
                new(1.25f, 0.58f, 1f),
                "village_day@perimeter-west-arrival-fence"));
        AddAct1AuthoredExteriorParcel(
            perimeterParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "PerimeterEastArrivalFacade",
                new(22.0f, 0f, 40.0f),
                -90f,
                Vector3.One * 0.50f,
                "village_day@perimeter-east-arrival-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "PerimeterEastArrivalShed",
                new(19.0f, 0f, 35.5f),
                -82f,
                Vector3.One * 0.42f,
                "village_day@perimeter-east-arrival-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "PerimeterEastArrivalFence",
                new(24.0f, 0f, 35.0f),
                0f,
                new(1.20f, 0.56f, 1f),
                "village_day@perimeter-east-arrival-fence"));
        AddAct1AuthoredExteriorParcel(
            perimeterParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "PerimeterWestStreetFacade",
                new(-23.0f, 0f, -7.0f),
                90f,
                Vector3.One * 0.48f,
                "village_day@perimeter-west-street-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "PerimeterWestStreetShed",
                new(-20.0f, 0f, -12.0f),
                84f,
                Vector3.One * 0.40f,
                "village_day@perimeter-west-street-shed"));
        AddAct1AuthoredExteriorParcel(
            perimeterParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "PerimeterEastStreetFacade",
                new(23.0f, 0f, -15.0f),
                -90f,
                Vector3.One * 0.46f,
                "village_day@perimeter-east-street-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "PerimeterEastStreetShed",
                new(20.0f, 0f, -20.0f),
                -84f,
                Vector3.One * 0.38f,
                "village_day@perimeter-east-street-shed"));
        AddAct1AuthoredExteriorParcel(
            perimeterParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "PerimeterEastReturnFacade",
                new(20.0f, 0f, -43.0f),
                -90f,
                Vector3.One * 0.42f,
                "zirat_road@perimeter-east-return-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "PerimeterEastReturnFence",
                new(23.0f, 0f, -47.0f),
                4f,
                new(1.05f, 0.50f, 1f),
                "zirat_road@perimeter-east-return-fence"));

        // The first perimeter pass fixed only the far horizon. These six
        // staggered mid-field parcels close the lateral first-person turns at
        // human scale while leaving the road and branch windows open.
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "WestArrivalMidParcel",
                new(-31.0f, 0f, 20.5f),
                "village_day@west-arrival-mid-parcel",
                "mid-field house and broken fence; arrival side turn"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "WestArrivalMidFacade",
                Vector3.Zero,
                86f,
                Vector3.One * 0.62f,
                "village_day@west-arrival-mid-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "WestArrivalMidShed",
                new(-4.0f, 0f, -3.8f),
                78f,
                Vector3.One * 0.48f,
                "village_day@west-arrival-mid-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "WestArrivalMidFence",
                new(3.4f, 0f, 4.1f),
                12f,
                new(1.35f, 0.64f, 1f),
                "village_day@west-arrival-mid-fence"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "EastArrivalMidParcel",
                new(31.5f, 0f, 22.5f),
                "village_day@east-arrival-mid-parcel",
                "mid-field house and yard edge; arrival side turn"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "EastArrivalMidFacade",
                Vector3.Zero,
                -88f,
                Vector3.One * 0.60f,
                "village_day@east-arrival-mid-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs",
                "EastArrivalMidWoodpile",
                new(-3.6f, 0f, -2.8f),
                -4f,
                Vector3.One * 0.68f,
                "village_day@east-arrival-mid-woodpile"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "EastArrivalMidFence",
                new(-3.2f, 0f, 4.4f),
                178f,
                new(1.28f, 0.62f, 1f),
                "village_day@east-arrival-mid-fence"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "WestStreetMidParcel",
                new(-31.5f, 0f, -9.5f),
                "village_day@west-street-mid-parcel",
                "staggered side house closes the main-street reverse field"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "WestStreetMidFacade",
                Vector3.Zero,
                94f,
                Vector3.One * 0.56f,
                "village_day@west-street-mid-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "WestStreetMidShed",
                new(4.0f, 0f, 3.6f),
                104f,
                Vector3.One * 0.42f,
                "village_day@west-street-mid-shed"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "EastStreetMidParcel",
                new(32.5f, 0f, -14.5f),
                "village_day@east-street-mid-parcel",
                "staggered side house closes the main-street side field"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "EastStreetMidFacade",
                Vector3.Zero,
                -94f,
                Vector3.One * 0.54f,
                "village_day@east-street-mid-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "EastStreetMidShed",
                new(-4.2f, 0f, 3.2f),
                -102f,
                Vector3.One * 0.40f,
                "village_day@east-street-mid-shed"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "WestReturnMidParcel",
                new(-30.0f, 0f, -36.5f),
                "zirat_road@west-return-mid-parcel",
                "low return-street house and fence; quiet transition toward zirat"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "WestReturnMidFacade",
                Vector3.Zero,
                88f,
                Vector3.One * 0.50f,
                "zirat_road@west-return-mid-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "WestReturnMidFence",
                new(4.0f, 0f, 3.8f),
                6f,
                new(1.18f, 0.52f, 1f),
                "zirat_road@west-return-mid-fence"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "EastReturnMidParcel",
                new(31.5f, 0f, -40.0f),
                "zirat_road@east-return-mid-parcel",
                "low return-street house and shed; quiet transition toward zirat"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "EastReturnMidFacade",
                Vector3.Zero,
                -88f,
                Vector3.One * 0.48f,
                "zirat_road@east-return-mid-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "EastReturnMidShed",
                new(-4.0f, 0f, 3.0f),
                -80f,
                Vector3.One * 0.36f,
                "zirat_road@east-return-mid-shed"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "WestArrivalNearParcel",
                new(-17.5f, 0f, 16.5f),
                "village_day@west-arrival-near-parcel",
                "near-side lived-in house and gate; arrival first-person frame"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "WestArrivalNearFacade",
                Vector3.Zero,
                92f,
                Vector3.One * 0.68f,
                "village_day@west-arrival-near-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "Gate_CrookedTimber",
                "WestArrivalNearGate",
                new(3.8f, 0f, 3.8f),
                6f,
                Vector3.One * 0.78f,
                "village_day@west-arrival-near-gate"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "WestArrivalNearFence",
                new(4.0f, 0f, 5.0f),
                8f,
                new(1.15f, 0.66f, 1f),
                "village_day@west-arrival-near-fence"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "EastArrivalNearParcel",
                new(18.0f, 0f, 17.0f),
                "village_day@east-arrival-near-parcel",
                "near-side lived-in house and shed; arrival first-person frame"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "EastArrivalNearFacade",
                Vector3.Zero,
                -92f,
                Vector3.One * 0.66f,
                "village_day@east-arrival-near-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "EastArrivalNearShed",
                new(-4.1f, 0f, 3.0f),
                -84f,
                Vector3.One * 0.48f,
                "village_day@east-arrival-near-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "EastArrivalNearFence",
                new(-4.0f, 0f, 5.1f),
                176f,
                new(1.10f, 0.64f, 1f),
                "village_day@east-arrival-near-fence"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "WestStreetNearParcel",
                new(-18.0f, 0f, -5.0f),
                "village_day@west-street-near-parcel",
                "near-side house closes the main-street lateral field"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "WestStreetNearFacade",
                Vector3.Zero,
                94f,
                Vector3.One * 0.62f,
                "village_day@west-street-near-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs",
                "WestStreetNearWoodpile",
                new(3.6f, 0f, 3.5f),
                10f,
                Vector3.One * 0.66f,
                "village_day@west-street-near-woodpile"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "EastStreetNearParcel",
                new(18.5f, 0f, -14.0f),
                "village_day@east-street-near-parcel",
                "near-side house closes the main-street lateral field"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "EastStreetNearFacade",
                Vector3.Zero,
                -94f,
                Vector3.One * 0.60f,
                "village_day@east-street-near-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "Gate_CrookedTimber",
                "EastStreetNearGate",
                new(-3.9f, 0f, 3.5f),
                174f,
                Vector3.One * 0.72f,
                "village_day@east-street-near-gate"));

        // Close the four lateral review sectors with already-cached authored
        // facade families. These are distant presentation silhouettes only;
        // route, collision and narrative owners remain untouched.
        AddAuthoredHouse(perimeterParcels, "BabaiEastYardDepthHouse", new(-5.5f, 1.1f, 10.5f), 0.32f, 180f);
        AddVisualFenceRun(perimeterParcels, "BabaiEastYardDepthFence", new(-9.0f, 0f, 9.0f), new(-4.0f, 0f, 11.0f));
        AddAuthoredHouse(perimeterParcels, "EastStreetFarHouse", new(39.0f, 1.1f, -18.0f), 0.30f, -90f);
        AddVisualFenceRun(perimeterParcels, "EastStreetFarFence", new(35.0f, 0f, -22.0f), new(41.0f, 0f, -22.0f));
        AddAuthoredHouse(perimeterParcels, "FapEastViewHouse", new(40.0f, 1.1f, -28.0f), 0.28f, 176f);
        AddAuthoredHouse(perimeterParcels, "ZiratEastBoundaryHouse", new(22.0f, 1.0f, -73.0f), 0.25f, -90f);
        AddVisualFenceRun(perimeterParcels, "ZiratEastBoundaryFence", new(19.0f, 0f, -76.0f), new(25.0f, 0f, -76.0f));

        // The fixed lateral review turns still reach three real field edges:
        // the east main-street horizon, the far side of Babai's yard, and the
        // east zirat boundary. Use the existing authored facade/fence family
        // as distant parcel anchors; these remain outside route and collision
        // ownership and are intentionally sparse.
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "EastStreetHorizonParcel",
                new(41.0f, 0f, 24.0f),
                "village_day@east-street-horizon",
                "distant east boundary closes the main-street lateral field"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "EastStreetHorizonFacade",
                Vector3.Zero,
                -92f,
                Vector3.One * 0.34f,
                "village_day@east-street-horizon-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "EastStreetHorizonFence",
                new(4.6f, 0f, 3.8f),
                8f,
                new(1.30f, 0.52f, 1f),
                "village_day@east-street-horizon-fence"));
        AddVisualTree(perimeterParcels, "EastStreetHorizonBirch", new(48.0f, 0f, 31.5f), 8.2f, VegetationStyle.Birch, "596047");
        AddVisualTree(perimeterParcels, "EastStreetHorizonConifer", new(55.0f, 0f, 28.0f), 9.4f, VegetationStyle.Conifer, "30483f");

        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "BabaiEastDepthParcel",
                new(5.0f, 0f, 15.0f),
                "house_old_pc@east-depth-boundary",
                "far east yard parcel closes the Babai reverse turn without narrowing the house path"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "BabaiEastDepthFacade",
                Vector3.Zero,
                178f,
                Vector3.One * 0.30f,
                "house_old_pc@east-depth-boundary-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "BabaiEastDepthFence",
                new(-3.5f, 0f, 3.6f),
                176f,
                new(1.15f, 0.50f, 1f),
                "house_old_pc@east-depth-boundary-fence"));

        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "ZiratEastHorizonParcel",
                new(31.0f, 0f, -78.0f),
                "zirat_road@east-horizon-boundary",
                "distant east field edge preserves the cemetery transition without a wall"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ZiratEastHorizonFacade",
                Vector3.Zero,
                -88f,
                Vector3.One * 0.28f,
                "zirat_road@east-horizon-boundary-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ZiratEastHorizonFence",
                new(4.8f, 0f, 2.8f),
                4f,
                new(1.05f, 0.48f, 1f),
                "zirat_road@east-horizon-boundary-fence"));
        AddVisualTree(perimeterParcels, "ZiratEastHorizonBirch", new(38.0f, 0f, -84.0f), 8.4f, VegetationStyle.Birch, "596047");

        // The fixed MainStreet right turn still looked across the road fence
        // into an unbounded field. A single staggered parcel at the actual
        // camera ray closes that read with a house, shed and broken boundary;
        // it stays outside the route envelope and reuses the existing authored
        // village family instead of adding another procedural wall.
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "EastStreetSideClosureParcel",
                new(25.0f, 0f, -5.5f),
                "village_day@east-street-side-closure",
                "near-mid east parcel closes the MainStreet right-turn field while preserving the road window"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "EastStreetSideClosureFacade",
                Vector3.Zero,
                -100f,
                Vector3.One * 0.58f,
                "village_day@east-street-side-closure-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "EastStreetSideClosureShed",
                new(-4.0f, 0f, 3.3f),
                -88f,
                Vector3.One * 0.42f,
                "village_day@east-street-side-closure-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "EastStreetSideClosureFence",
                new(-2.6f, 0f, 4.2f),
                8f,
                new(1.30f, 0.54f, 1f),
                "village_day@east-street-side-closure-fence"));
        AddVisualTree(perimeterParcels, "EastStreetSideClosureBirch", new(35.0f, 0f, -1.0f), 7.4f, VegetationStyle.Birch, "596047");
        AddVisualTree(perimeterParcels, "EastStreetSideClosureMidBirch", new(38.0f, 0f, -9.0f), 8.0f, VegetationStyle.Birch, "68705a");
        AddVisualTree(perimeterParcels, "EastStreetSideClosureMidConifer", new(46.0f, 0f, -14.0f), 9.3f, VegetationStyle.Conifer, "30483f");
        AddVisualLandformSurface(
            perimeterParcels,
            "EastStreetSideFieldBank",
            7.2f,
            0.42f,
            22.0f,
            new(41.0f, 0.10f, -12.0f),
            "4d594d",
            "earth",
            22f);

        // Zirat keeps a quiet open boundary, but the same right-turn test
        // otherwise ended on a flat field. One low parcel and two varied edge
        // trees provide a human-scale village-to-cemetery transition without
        // turning the cemetery into a generic forest wall.
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "ZiratEastFieldEdgeParcel",
                new(24.0f, 0f, -61.0f),
                "zirat_road@east-field-edge",
                "quiet east cemetery boundary keeps the lateral horizon inhabited and sparse"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ZiratEastFieldEdgeFacade",
                Vector3.Zero,
                -82f,
                Vector3.One * 0.34f,
                "zirat_road@east-field-edge-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ZiratEastFieldEdgeFence",
                new(4.2f, 0f, 3.0f),
                4f,
                new(1.08f, 0.46f, 1f),
                "zirat_road@east-field-edge-fence"));
        AddVisualTree(perimeterParcels, "ZiratEastFieldEdgeBirch", new(31.5f, 0f, -64.5f), 7.1f, VegetationStyle.Birch, "596047");
        AddVisualTree(perimeterParcels, "ZiratEastFieldEdgeConifer", new(34.0f, 0f, -71.0f), 8.0f, VegetationStyle.Conifer, "30483f");
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "ZiratWestFieldEdgeParcel",
                new(-23.0f, 0f, -67.0f),
                "zirat_road@west-field-edge",
                "quiet west cemetery boundary keeps the lateral horizon inhabited without entering the marker field"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ZiratWestFieldEdgeFacade",
                Vector3.Zero,
                92f,
                Vector3.One * 0.30f,
                "zirat_road@west-field-edge-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ZiratWestFieldEdgeFence",
                new(3.8f, 0f, 2.6f),
                2f,
                new Vector3(1.0f, 0.42f, 1f),
                "zirat_road@west-field-edge-fence"));

        // These are exact presentation descendants whose anchors and roles
        // overlap the authored replacements above. Route floors, connector
        // collision, navigation, interactions, spawns and landmark roots stay
        // outside this narrow suppression list.
        HideCorePresentationNode(
            core,
            "HouseExteriorApproach/BabaiEbiHouseFullVolume",
            "authored dwelling facade replaces the overlapping primitive house volume");
        HideCorePresentationNode(
            core,
            "HouseExteriorApproach/BabaiEbiHouseThreshold",
            "authored dwelling porch/threshold replaces the overlapping primitive threshold");
        HideCorePresentationNode(
            core,
            "HouseExteriorApproach/BabaiEbiHouseWindowGlow",
            "authored warm window replaces the overlapping primitive window glow");
        HideCorePresentationNode(
            core,
            "HouseExteriorApproach/BabaiEbiHousePorchWarmth",
            "authored porch replaces the overlapping primitive porch warmth");
        HideCorePresentationNode(
            core,
            "HouseExteriorApproach/BabaiEbiHouseSideOutbuilding",
            "authored yard shed and fence parcels replace the blockout side outbuilding that reads as a blank box in reverse views");
        HideCorePresentationNode(
            core,
            "BabaiEbiYard/BabaiYardToolShed",
            "authored shed replaces the overlapping primitive yard shed");
        HideCorePresentationNode(
            core,
            "BabaiEbiYard/BabaiYardOpenGate",
            "authored gate replaces the overlapping primitive yard gate");
        HideCorePresentationNode(
            core,
            "BabaiEbiYard/BabaiYardWoodpile",
            "authored woodpile replaces the overlapping primitive yard woodpile");
        HideCorePresentationNode(
            core,
            "BabaiEbiYard/BabaiYardWestBoundary",
            "authored fence replaces the selected overlapping primitive west boundary",
            required: false);
        HideCorePresentationNode(
            core,
            "Arrival/ArrivalEastNearParcelShed",
            "authored arrival east shed replaces the overlapping primitive near-parcel shed",
            required: false);

        importedRoot.QueueFree();
    }

    private static void BuildAct1SightlineClosurePass(Node3D core)
    {
        var closure = new Node3D { Name = "Act1AuthoredSightlineClosurePass" };
        closure.SetMeta("presentationOnly", true);
        closure.SetMeta("visualOnly", true);
        closure.SetMeta("assetSource", $"{VillageExteriorKitScenePath}|{KaraForestEdgeKitScenePath}");
        closure.SetMeta("collisionOwner", "none");
        closure.SetMeta("navigationOwner", "none");
        closure.SetMeta("interactionOwner", "none");
        closure.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        closure.SetMeta(
            "compositionRole",
            "one authored near/mid closure layer for exposed arrival, MainStreet, FAP return and Kara lateral sightlines; route center remains open");
        core.AddChild(closure);

        var village = new Node3D { Name = "VillageLateralParcels" };
        village.SetMeta("presentationOnly", true);
        village.SetMeta("visualOnly", true);
        village.SetMeta("collisionOwner", "none");
        village.SetMeta("navigationOwner", "none");
        village.SetMeta("interactionOwner", "none");
        village.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        closure.AddChild(village);

        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "ArrivalWestLateralParcel",
                new(-11.0f, 0f, 16.0f),
                "village_day@arrival-west-lateral-closure",
                "near/mid dwelling, shed and broken fence close the left arrival field without entering the road"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ArrivalWestLateralFacade",
                Vector3.Zero,
                90f,
                Vector3.One * 0.78f,
                "village_day@arrival-west-lateral-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalWestLateralShed",
                new(-4.2f, 0f, 3.6f),
                82f,
                Vector3.One * 0.48f,
                "village_day@arrival-west-lateral-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalWestLateralFence",
                new(3.6f, 0f, 4.2f),
                8f,
                new Vector3(1.24f, 0.62f, 1f),
                "village_day@arrival-west-lateral-fence"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "ArrivalEastLateralParcel",
                new(11.0f, 0f, 17.0f),
                "village_day@arrival-east-lateral-closure",
                "near/mid dwelling, shed and broken fence close the right arrival field without entering the road"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ArrivalEastLateralFacade",
                Vector3.Zero,
                -90f,
                Vector3.One * 0.76f,
                "village_day@arrival-east-lateral-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalEastLateralShed",
                new(4.5f, 0f, 3.8f),
                -84f,
                Vector3.One * 0.46f,
                "village_day@arrival-east-lateral-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalEastLateralFence",
                new(-3.8f, 0f, 4.3f),
                174f,
                new Vector3(1.20f, 0.60f, 1f),
                "village_day@arrival-east-lateral-fence"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "MainStreetEastLateralParcel",
                new(11.0f, 0f, -12.0f),
                "village_day@main-street-east-lateral-closure",
                "one staggered east parcel closes the fixed MainStreet right turn while preserving the branch and road windows"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "MainStreetEastLateralFacade",
                Vector3.Zero,
                -96f,
                Vector3.One * 0.62f,
                "village_day@main-street-east-lateral-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "MainStreetEastLateralShed",
                new(-4.2f, 0f, 3.4f),
                -86f,
                Vector3.One * 0.44f,
                "village_day@main-street-east-lateral-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "MainStreetEastLateralFence",
                new(-2.8f, 0f, 4.2f),
                6f,
                new Vector3(1.26f, 0.58f, 1f),
                "village_day@main-street-east-lateral-fence"));

        // The accepted reverse/lateral frames still expose the outer field
        // beyond the existing lateral parcels. Keep the closure sparse and
        // move only existing authored parcels into the actual mid/far camera
        // rays: the arrival pair frames the reverse village view, one FAP-side
        // shed closes the clinic right ray, and one low dwelling gives the
        // zirat reverse view a readable village edge. Every anchor stays
        // outside the route guard and leaves road/door windows open; the kit
        // is presentation-only and carries no collision.
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "ArrivalReverseWestDomesticParcel",
                new(-11.5f, 0f, 26.0f),
                "village_day@arrival-reverse-west-domestic-facade",
                "mid reverse dwelling silhouette; route center and arrival spawn remain open"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ArrivalReverseWestDomesticFacade",
                Vector3.Zero,
                176f,
                Vector3.One * 0.50f,
                "village_day@arrival-reverse-west-domestic-facade"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "ArrivalReverseEastDomesticParcel",
                new(11.5f, 0f, 27.0f),
                "village_day@arrival-reverse-east-domestic-shed",
                "mid reverse shed silhouette; route center and arrival spawn remain open"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalReverseEastDomesticShed",
                Vector3.Zero,
                184f,
                Vector3.One * 0.50f,
                "village_day@arrival-reverse-east-domestic-shed"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "ArrivalLeftHorizonDomesticParcel",
                new(-32.0f, 0f, 30.5f),
                "village_day@arrival-left-horizon-domestic-facade",
                "far left horizon dwelling silhouette; lateral route window remains open"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ArrivalLeftHorizonDomesticFacade",
                Vector3.Zero,
                88f,
                Vector3.One * 0.34f,
                "village_day@arrival-left-horizon-domestic-facade"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "ArrivalRightHorizonDomesticParcel",
                new(31.0f, 0f, 33.0f),
                "village_day@arrival-right-horizon-domestic-fence",
                "far right horizon fence silhouette; lateral route window remains open"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalRightHorizonDomesticFence",
                Vector3.Zero,
                -8f,
                new Vector3(1.18f, 0.52f, 1f),
                "village_day@arrival-right-horizon-domestic-fence"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "FapReverseWestDomesticFacadeParcel",
                new(9.0f, 0f, -42.0f),
                "fap_clinic@reverse-west-domestic-facade",
                "mid zirat-reverse dwelling silhouette; cemetery route and marker field remain open"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "FapReverseWestDomesticFacade",
                Vector3.Zero,
                90f,
                Vector3.One * 0.44f,
                "fap_clinic@reverse-west-domestic-facade"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "FapReverseWestDomesticFenceParcel",
                new(-21.0f, 0f, 7.0f),
                "fap_clinic@reverse-west-domestic-fence",
                "north-of-return fence silhouette; FAP door and branch approach remain open"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "FapReverseWestDomesticFence",
                Vector3.Zero,
                6f,
                new Vector3(1.16f, 0.50f, 1f),
                "fap_clinic@reverse-west-domestic-fence"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "FapReverseEastDomesticShedParcel",
                new(29.5f, 0f, -21.5f),
                "fap_clinic@reverse-east-domestic-shed",
                "mid FAP-right shed silhouette; clinic door and branch approach remain open"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "FapReverseEastDomesticShed",
                Vector3.Zero,
                -90f,
                Vector3.One * 0.50f,
                "fap_clinic@reverse-east-domestic-shed"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "FapReverseEastDomesticFenceParcel",
                new(17.0f, 0f, 7.0f),
                "fap_clinic@reverse-east-domestic-fence",
                "north-of-return fence silhouette; FAP door and branch approach remain open"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "FapReverseEastDomesticFence",
                Vector3.Zero,
                -6f,
                new Vector3(1.12f, 0.48f, 1f),
                "fap_clinic@reverse-east-domestic-fence"));

        var kara = new Node3D { Name = "KaraLateralForestClosure" };
        kara.SetMeta("presentationOnly", true);
        kara.SetMeta("visualOnly", true);
        kara.SetMeta("assetSource", KaraForestEdgeKitScenePath);
        kara.SetMeta("collisionOwner", "none");
        kara.SetMeta("navigationOwner", "none");
        kara.SetMeta("interactionOwner", "none");
        kara.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        kara.SetMeta(
            "compositionRole",
            "asymmetric forest-bank, root, mixed-tree and distant-profile pairs close Kara lateral field windows while preserving the central path");
        closure.AddChild(kara);

        AddKaraLateralForestClosure(kara);
    }

    private static void AddKaraLateralForestClosure(Node3D parent)
    {
        var packed = ResourceLoader.Load<PackedScene>(KaraForestEdgeKitScenePath)
            ?? throw new InvalidOperationException(
                $"Act I Kara forest-edge closure kit is missing: {KaraForestEdgeKitScenePath}.");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException(
                $"Act I Kara forest-edge closure kit did not instantiate: {KaraForestEdgeKitScenePath}.");
        var kitRoot = importedRoot;

        try
        {
            if (!string.Equals(importedRoot.Name.ToString(), KaraForestEdgeKitRootName, StringComparison.Ordinal))
            {
                kitRoot = importedRoot.GetNodeOrNull<Node3D>(KaraForestEdgeKitRootName)
                    ?? throw new InvalidOperationException(
                        $"Act I Kara forest-edge closure kit is missing authored root '{KaraForestEdgeKitRootName}'.");
            }

            var collisionNodes = FindDescendants<Node>(kitRoot)
                .Where(node => node is CollisionObject3D or CollisionShape3D)
                .ToArray();
            if (collisionNodes.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Act I Kara forest-edge closure kit must be presentation-only; found {collisionNodes.Length} collision nodes.");
            }

            var placements = new[]
            {
                ("ForestBank_Left", "KaraClosureWestBank", new Vector3(-13.0f, 0f, -114.0f), -16f, Vector3.One * 0.76f, "kara_urman_night@west-lateral-bank"),
                ("MixedTreeCluster_Left", "KaraClosureWestMixedTrees", new Vector3(-15.0f, 0f, -121.0f), -20f, Vector3.One * 0.76f, "kara_urman_night@west-lateral-mixed-trees"),
                ("RootWall_Left", "KaraClosureWestRootWall", new Vector3(-12.0f, 0f, -118.0f), -12f, Vector3.One * 0.70f, "kara_urman_night@west-lateral-root-wall"),
                ("DistantForestMass_Low", "KaraClosureWestDistantMass", new Vector3(-20.5f, 0f, -104.0f), -8f, Vector3.One * 0.68f, "kara_urman_night@west-lateral-distant-mass"),
                ("ForestBank_Right", "KaraClosureEastBank", new Vector3(13.0f, 0f, -115.0f), 18f, Vector3.One * 0.76f, "kara_urman_night@east-lateral-bank"),
                ("MixedTreeCluster_Right", "KaraClosureEastMixedTrees", new Vector3(15.0f, 0f, -122.0f), 22f, Vector3.One * 0.76f, "kara_urman_night@east-lateral-mixed-trees"),
                ("RootWall_Right", "KaraClosureEastRootWall", new Vector3(12.0f, 0f, -119.0f), 20f, Vector3.One * 0.70f, "kara_urman_night@east-lateral-root-wall"),
                ("DistantForestMass_Tall", "KaraClosureEastDistantMass", new Vector3(20.5f, 0f, -105.5f), 11f, Vector3.One * 0.68f, "kara_urman_night@east-lateral-distant-mass")
            };

            foreach (var (componentName, placementName, anchor, yaw, scale, logicalAnchor) in placements)
            {
                var component = kitRoot.GetNodeOrNull<Node3D>(componentName)
                    ?? throw new InvalidOperationException(
                        $"Act I Kara forest-edge closure kit is missing required direct component root '{componentName}'.");
                AttachAct1ExteriorKitComponent(
                    parent,
                    component,
                    placementName,
                    anchor,
                    yaw,
                    scale,
                    logicalAnchor,
                    KaraForestEdgeKitScenePath);
            }

            RegradeKaraEdgeMaterials(parent);
            importedRoot.Free();
        }
        catch
        {
            if (GodotObject.IsInstanceValid(importedRoot))
            {
                importedRoot.Free();
            }

            throw;
        }
    }

    private static void BuildAct1AuthoredExteriorNeighborParcels(
        Node3D core,
        Node3D presentation)
    {
        // This is a presentation-only submount. Each named parcel below gets
        // its own fresh PackedScene instance because an extracted GLB component
        // cannot be parented twice. The existing route, zone and RuntimeBridge
        // owners remain outside this mount; the road and camera windows stay
        // deliberately open through the middle of every parcel group.
        var neighborParcels = new Node3D { Name = "NeighborParcels" };
        neighborParcels.SetMeta("presentationOnly", true);
        neighborParcels.SetMeta("visualOnly", true);
        neighborParcels.SetMeta("presentationOnlySubmount", true);
        neighborParcels.SetMeta("assetSource", VillageExteriorKitScenePath);
        neighborParcels.SetMeta("assetInstancePolicy", "fresh imported kit instance per named parcel group");
        neighborParcels.SetMeta("collisionOwner", "none");
        neighborParcels.SetMeta("navigationOwner", "none");
        neighborParcels.SetMeta("interactionOwner", "none");
        neighborParcels.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        neighborParcels.SetMeta(
            "roadWindowPolicy",
            "keep the center route, connector/path corridor, reverse view and zirat landmark sightline open");
        neighborParcels.SetMeta(
            "suppressionScope",
            "exact overlapping visual primitives beneath authored arrival, yard, MainStreet, connective, return and zirat parcels only; no route, landmark, gameplay or runtime owners");
        presentation.AddChild(neighborParcels);

        var mainStreetParcels = new Node3D { Name = "MainStreet" };
        mainStreetParcels.SetMeta("presentationOnly", true);
        mainStreetParcels.SetMeta("visualOnly", true);
        mainStreetParcels.SetMeta("parcelGroup", "MainStreet");
        mainStreetParcels.SetMeta(
            "cameraViewPolicy",
            "two offset opposite-side facades create near/mid framing while preserving an open first-person road window");
        neighborParcels.AddChild(mainStreetParcels);

        var connectiveStreetParcels = new Node3D { Name = "ConnectiveStreet" };
        connectiveStreetParcels.SetMeta("presentationOnly", true);
        connectiveStreetParcels.SetMeta("visualOnly", true);
        connectiveStreetParcels.SetMeta("parcelGroup", "ConnectiveStreet");
        connectiveStreetParcels.SetMeta(
            "cameraViewPolicy",
            "deeper receding facade and side outbuilding preserve village depth in the 180-degree reverse view");
        neighborParcels.AddChild(connectiveStreetParcels);

        var returnStreetParcels = new Node3D { Name = "ReturnStreet" };
        returnStreetParcels.SetMeta("presentationOnly", true);
        returnStreetParcels.SetMeta("visualOnly", true);
        returnStreetParcels.SetMeta("parcelGroup", "ReturnStreet");
        returnStreetParcels.SetMeta(
            "cameraViewPolicy",
            "low distant transition toward zirat; preserve the road corridor and the zirat landmark sightline");
        neighborParcels.AddChild(returnStreetParcels);

        var houseExteriorParcels = new Node3D { Name = "HouseExterior" };
        houseExteriorParcels.SetMeta("presentationOnly", true);
        houseExteriorParcels.SetMeta("visualOnly", true);
        houseExteriorParcels.SetMeta("parcelGroup", "HouseExterior");
        houseExteriorParcels.SetMeta(
            "cameraViewPolicy",
            "one west-side lived-in facade closes the house approach without entering the hero doorway or return path");
        neighborParcels.AddChild(houseExteriorParcels);

        var residentialRoad = GetConnector("residential-road-extension");
        var mainStreetDirection = HorizontalDirection(residentialRoad.End - residentialRoad.Start);
        var mainStreetSide = new Vector3(mainStreetDirection.Z, 0f, -mainStreetDirection.X);
        var mainStreetYaw = DirectionYaw(mainStreetDirection);

        var returnRoad = GetConnector("house-to-zirat-return");
        var returnDirection = HorizontalDirection(returnRoad.End - returnRoad.Start);
        var returnSide = new Vector3(returnDirection.Z, 0f, -returnDirection.X);
        var returnYaw = DirectionYaw(returnDirection);

        // MainStreet deliberately uses the existing near-setback and east-barn
        // anchors, offset along the street and on opposite sides of the route.
        // The placement offsets for fences/gates/sheds line up with their
        // matching core presentation primitives; the added woodpiles remain
        // small parcel details and never enter the road window.
        var mainStreetWest = AddAct1ExteriorParcelSubmount(
            mainStreetParcels,
            "WestParcel",
            new(-11.8f, 0f, 5.2f),
            "village_day@main-street-west-parcel",
            "near west parcel; road-facing facade, partial boundary and open approach");
        AddAct1AuthoredExteriorParcel(
            mainStreetWest,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "MainStreetWestNeighborFacade",
                Vector3.Zero,
                DirectionYaw(-mainStreetSide) - 4f,
                new Vector3(0.76f, 0.80f, 0.76f),
                "village_day@main-street-west-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "MainStreetWestNeighborFence",
                new(3.3f, 0f, -1.45f),
                mainStreetYaw + 6f,
                new Vector3(1.32f, 0.76f, 1f),
                "village_day@main-street-west-partial-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "Gate_CrookedTimber",
                "MainStreetWestNeighborGate",
                new(3.1f, 0f, -1.7f),
                DirectionYaw(-mainStreetSide) - 2f,
                new Vector3(0.78f, 0.92f, 0.80f),
                "village_day@main-street-west-gate"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "MainStreetWestNeighborShed",
                new(-0.8f, 0f, -12.4f),
                mainStreetYaw - 8f,
                new Vector3(0.74f, 0.70f, 0.82f),
                "village_day@main-street-west-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs",
                "MainStreetWestNeighborWoodpile",
                new(-3.8f, 0f, -4.5f),
                mainStreetYaw + 88f,
                Vector3.One * 0.72f,
                "village_day@main-street-west-woodpile"));

        var mainStreetEast = AddAct1ExteriorParcelSubmount(
            mainStreetParcels,
            "EastParcel",
            new(16.2f, 0f, -10.5f),
            "village_day@main-street-east-parcel",
            "mid east parcel; staggered facade and partial boundary keep the road window open");
        AddAct1AuthoredExteriorParcel(
            mainStreetEast,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "MainStreetEastNeighborFacade",
                Vector3.Zero,
                DirectionYaw(mainStreetSide) + 7f,
                new Vector3(0.70f, 0.74f, 0.70f),
                "village_day@main-street-east-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "MainStreetEastNeighborFence",
                new(-7.45f, 0f, 14.75f),
                mainStreetYaw - 7f,
                new Vector3(1.48f, 0.70f, 1f),
                "village_day@main-street-east-partial-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "Gate_CrookedTimber",
                "MainStreetEastNeighborGate",
                new(-7.4f, 0f, 13.3f),
                DirectionYaw(mainStreetSide) + 4f,
                new Vector3(0.72f, 0.86f, 0.74f),
                "village_day@main-street-east-gate"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "MainStreetEastNeighborShed",
                new(-4.5f, 0f, 8.5f),
                mainStreetYaw + 11f,
                new Vector3(0.68f, 0.66f, 0.76f),
                "village_day@main-street-east-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs",
                "MainStreetEastNeighborWoodpile",
                new(3.4f, 0f, 4.0f),
                mainStreetYaw - 86f,
                Vector3.One * 0.64f,
                "village_day@main-street-east-woodpile"));

        // The standard main-street forward frame is at z=2.4 and looks to
        // z=-15. The two existing parcels remain tied to their suppressed
        // blockout anchors; this smaller staggered pair occupies the real
        // forward camera rays at x +/-12..14 without narrowing the road.
        var mainStreetForwardWest = AddAct1ExteriorParcelSubmount(
            mainStreetParcels,
            "ForwardWestParcel",
            Vector3.Zero,
            "village_day@main-street-forward-west",
            "near west facade and boundary sit inside the forward camera ray while the road centre stays open");
        AddAct1AuthoredExteriorParcel(
            mainStreetForwardWest,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "MainStreetForwardWestFacade",
                new(-12.0f, 0f, -4.0f),
                DirectionYaw(-mainStreetSide) - 2f,
                Vector3.One * 0.54f,
                "village_day@main-street-forward-west-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "Gate_CrookedTimber",
                "MainStreetForwardWestGate",
                new(-8.8f, 0f, -6.4f),
                DirectionYaw(-mainStreetSide),
                Vector3.One * 0.68f,
                "village_day@main-street-forward-west-gate"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "MainStreetForwardWestFence",
                new(-10.3f, 0f, -5.5f),
                mainStreetYaw + 8f,
                new Vector3(1.15f, 0.62f, 1f),
                "village_day@main-street-forward-west-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "MainStreetForwardWestShed",
                new(-14.2f, 0f, -14.8f),
                mainStreetYaw - 8f,
                Vector3.One * 0.42f,
                "village_day@main-street-forward-west-shed"));

        var mainStreetForwardEast = AddAct1ExteriorParcelSubmount(
            mainStreetParcels,
            "ForwardEastParcel",
            Vector3.Zero,
            "village_day@main-street-forward-east",
            "mid east facade and shed close the forward side field without entering the branch or road window");
        AddAct1AuthoredExteriorParcel(
            mainStreetForwardEast,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "MainStreetForwardEastFacade",
                new(13.2f, 0f, -11.5f),
                DirectionYaw(mainStreetSide) + 3f,
                Vector3.One * 0.50f,
                "village_day@main-street-forward-east-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "MainStreetForwardEastShed",
                new(10.0f, 0f, -8.6f),
                mainStreetYaw + 10f,
                Vector3.One * 0.40f,
                "village_day@main-street-forward-east-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "MainStreetForwardEastFence",
                new(9.2f, 0f, -10.0f),
                mainStreetYaw - 8f,
                new Vector3(1.12f, 0.60f, 1f),
                "village_day@main-street-forward-east-fence"));

        // The connective parcel is intentionally deeper than the MainStreet
        // pair. Its side shed and fence break up the reverse view without
        // forming a second wall across the route.
        var connectiveDeep = AddAct1ExteriorParcelSubmount(
            connectiveStreetParcels,
            "DeepParcel",
            new(-18.0f, 0f, -29.5f),
            "village_day@connective-street-deep-parcel",
            "deep west parcel; road-facing facade recedes toward the return route");
        AddAct1AuthoredExteriorParcel(
            connectiveDeep,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ConnectiveStreetDeepNeighborFacade",
                Vector3.Zero,
                DirectionYaw(-returnSide) - 12f,
                new Vector3(0.60f, 0.64f, 0.60f),
                "village_day@connective-street-deep-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ConnectiveStreetSideOutbuilding",
                new(6.3f, 0f, -7.0f),
                returnYaw + 15f,
                new Vector3(0.64f, 0.62f, 0.70f),
                "village_day@connective-street-side-outbuilding"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ConnectiveStreetDeepNeighborFence",
                new(5.0f, 0f, 1.8f),
                returnYaw + 18f,
                new Vector3(1.18f, 0.66f, 1f),
                "village_day@connective-street-deep-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs",
                "ConnectiveStreetSideWoodpile",
                new(4.2f, 0f, -3.8f),
                returnYaw + 102f,
                Vector3.One * 0.58f,
                "village_day@connective-street-side-woodpile"));

        // ReturnStreet is a low, distant transition only. It stays on the
        // outer west parcel, away from the connector centerline and far ahead
        // of the zirat landmark, so a reverse turn still reads road -> zirat.
        var returnDistant = AddAct1ExteriorParcelSubmount(
            returnStreetParcels,
            "DistantParcel",
            new(-18.5f, 0f, -48.5f),
            "zirat_road@return-street-distant-parcel",
            "low far parcel; transition toward zirat with road and landmark sightline clear");
        AddAct1AuthoredExteriorParcel(
            returnDistant,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ReturnStreetDistantLowFacade",
                Vector3.Zero,
                DirectionYaw(-returnSide) + 19f,
                new Vector3(0.52f, 0.42f, 0.52f),
                "zirat_road@return-street-low-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ReturnStreetDistantFence",
                new(5.1f, 0f, 4.0f),
                returnYaw - 14f,
                new Vector3(1.34f, 0.50f, 1f),
                "zirat_road@return-street-distant-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs",
                "ReturnStreetDistantWoodpile",
                new(3.7f, 0f, 1.5f),
                returnYaw - 96f,
                Vector3.One * 0.46f,
                "zirat_road@return-street-distant-woodpile"));

        // The Agent B preview-board houses in the deep return/connective view
        // are suppressed after their authored parcel counterparts are mounted.
        // Keep the replacements small and staggered on the outer shoulders so
        // the reverse road and the zirat sightline remain open.
        AddAuthoredHouse(
            returnStreetParcels,
            "ReturnWestFarHouse1Silhouette",
            new(-19.8f, 1.0f, -52.7f),
            0.26f,
            90f);
        AddAuthoredHouse(
            returnStreetParcels,
            "ReturnWestBanyaSilhouette",
            new(-13.6f, 0.78f, -45.5f),
            0.20f,
            84f);
        AddAuthoredHouse(
            returnStreetParcels,
            "ReturnEastFarHouse2Silhouette",
            new(16.8f, 0.92f, -53.0f),
            0.24f,
            -90f);
        AddAuthoredHouse(
            connectiveStreetParcels,
            "ConnectiveEastHouseA6Silhouette",
            new(10.4f, 0.86f, -32.0f),
            0.22f,
            -90f);
        AddAuthoredHouse(
            returnStreetParcels,
            "ReturnEastHouseA8Silhouette",
            new(10.6f, 0.84f, -42.5f),
            0.22f,
            -90f);

        var houseExteriorWest = AddAct1ExteriorParcelSubmount(
            houseExteriorParcels,
            "WestSideParcel",
            new(-38.0f, 0f, -6.0f),
            "house_old_pc@west-side-parcel",
            "authored side facade closes the house reverse view while keeping the hero approach open");
        AddAct1AuthoredExteriorParcel(
            houseExteriorWest,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "HouseExteriorWestNeighborFacade",
                Vector3.Zero,
                88f,
                Vector3.One * 0.56f,
                "house_old_pc@west-side-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "HouseExteriorWestNeighborFence",
                new(4.0f, 0f, 2.8f),
                0f,
                new Vector3(1.22f, 0.58f, 1f),
                "house_old_pc@west-side-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "HouseExteriorWestNeighborShed",
                new(-2.6f, 0f, -4.2f),
                82f,
                Vector3.One * 0.50f,
                "house_old_pc@west-side-shed"));

        // Restricted suppression is intentionally explicit and zone-scoped:
        // these are the exact visual primitives replaced by the parcel reads.
        // HideCorePresentationNode rejects any node that owns route/path,
        // navigation, interaction, camera/light, spawn or RuntimeBridge state.
        foreach (var (relativePath, reason) in new (string RelativePath, string Reason)[]
                 {
                     ("MainStreet/MainStreetWestNearSetbackHouse", "neighbor west facade replaces the exact overlapping near-setback house primitive"),
                     ("MainStreet/MainStreetWestNearFenceRail", "neighbor west fence replaces the exact overlapping rail mesh"),
                     ("MainStreet/MainStreetWestNearFencePost0", "neighbor west fence replaces the exact overlapping post mesh"),
                     ("MainStreet/MainStreetWestNearFencePost1", "neighbor west fence replaces the exact overlapping post mesh"),
                     ("MainStreet/MainStreetWestNearFencePost2", "neighbor west fence replaces the exact overlapping post mesh"),
                     ("MainStreet/MainStreetWestNearFencePost3", "neighbor west fence replaces the exact overlapping post mesh"),
                     ("MainStreet/MainStreetWestParcelGate", "neighbor west gate replaces the exact overlapping parcel gate primitive"),
                     ("MainStreet/MainStreetWestShedVolume", "neighbor west shed replaces the exact overlapping shed primitive"),
                     ("MainStreet/MainStreetEastBarnVolume", "neighbor east facade replaces the exact overlapping east barn primitive"),
                     ("MainStreet/MainStreetEastNearFenceRail", "neighbor east fence replaces the exact overlapping rail mesh"),
                     ("MainStreet/MainStreetEastNearFencePost0", "neighbor east fence replaces the exact overlapping post mesh"),
                     ("MainStreet/MainStreetEastNearFencePost1", "neighbor east fence replaces the exact overlapping post mesh"),
                     ("MainStreet/MainStreetEastNearFencePost2", "neighbor east fence replaces the exact overlapping post mesh"),
                     ("MainStreet/MainStreetEastNearFencePost3", "neighbor east fence replaces the exact overlapping post mesh"),
                     ("MainStreet/MainStreetEastParcelGate", "neighbor east gate replaces the exact overlapping parcel gate primitive"),
                     ("MainStreet/MainStreetEastNearWorkshop", "neighbor east shed replaces the exact overlapping near workshop primitive"),
                     ("MainStreet/MainStreetEastShedVolume", "authored Agent B branch parcel replaces the legacy near-FAP shed that occludes the approach"),
                     ("MainStreet/MainStreetEastBackVolume", "cross-zone FAP approach view is occluded by the legacy east-back building; dedicated FAP parcel owns that sightline"),
                     ("ConnectiveStreetReturn/ConnectiveWestHouseVolume", "deep neighbor facade replaces the exact overlapping connective house primitive"),
                     ("ConnectiveStreetReturn/ConnectiveWestHouseEdgeRail", "deep neighbor fence replaces the exact overlapping house-edge rail mesh"),
                     ("ConnectiveStreetReturn/ConnectiveWestHouseEdgePost0", "deep neighbor fence replaces the exact overlapping house-edge post mesh"),
                     ("ConnectiveStreetReturn/ConnectiveWestHouseEdgePost1", "deep neighbor fence replaces the exact overlapping house-edge post mesh"),
                     ("ConnectiveStreetReturn/ConnectiveWestHouseEdgePost2", "deep neighbor fence replaces the exact overlapping house-edge post mesh"),
                     ("ConnectiveStreetReturn/ConnectiveWestHouseEdgePost3", "deep neighbor fence replaces the exact overlapping house-edge post mesh"),
                     ("ConnectiveStreetReturn/ConnectiveWestShed", "side outbuilding replaces the exact overlapping connective shed primitive"),
                     ("ConnectiveStreetReturn/ReturnWestFarmVolume", "distant low facade replaces the exact overlapping return farm primitive"),
                     ("ConnectiveStreetReturn/ReturnWestParcelFenceRail", "distant fence replaces the exact overlapping return-fence rail mesh"),
                     ("ConnectiveStreetReturn/ReturnWestParcelFencePost0", "distant fence replaces the exact overlapping return-fence post mesh"),
                     ("ConnectiveStreetReturn/ReturnWestParcelFencePost1", "distant fence replaces the exact overlapping return-fence post mesh"),
                     ("ConnectiveStreetReturn/ReturnWestParcelFencePost2", "distant fence replaces the exact overlapping return-fence post mesh"),
                     ("ConnectiveStreetReturn/ReturnWestParcelFencePost3", "distant fence replaces the exact overlapping return-fence post mesh"),
                     ("ConnectiveStreetReturn/ReturnWestParcelFencePost4", "distant fence replaces the exact overlapping return-fence post mesh")
                 })
        {
            HideCorePresentationNode(core, relativePath, reason);
        }
    }

    private static Node3D AddAct1ExteriorParcelSubmount(
        Node3D parent,
        string name,
        Vector3 anchor,
        string logicalAnchor,
        string cameraViewPolicy)
    {
        var parcel = new Node3D
        {
            Name = name,
            Position = anchor
        };
        parcel.SetMeta("presentationOnly", true);
        parcel.SetMeta("visualOnly", true);
        parcel.SetMeta("presentationOnlySubmount", true);
        parcel.SetMeta("assetInstancePolicy", "fresh packed asset instance; components detached before imported root is freed");
        parcel.SetMeta("assetSource", VillageExteriorKitScenePath);
        parcel.SetMeta("logicalAnchor", logicalAnchor);
        parcel.SetMeta("collisionOwner", "none");
        parcel.SetMeta("navigationOwner", "none");
        parcel.SetMeta("interactionOwner", "none");
        parcel.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        parcel.SetMeta("cameraViewPolicy", cameraViewPolicy);
        parent.AddChild(parcel);
        return parcel;
    }

    private static void AddAct1AuthoredExteriorParcel(
        Node3D parcel,
        params Act1ExteriorParcelComponentPlacement[] placements)
    {
        var packed = ResourceLoader.Load<PackedScene>(VillageExteriorKitScenePath)
            ?? throw new InvalidOperationException(
                $"Act I village exterior neighbor parcel kit is missing: {VillageExteriorKitScenePath}.");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException(
                $"Act I village exterior neighbor parcel kit did not instantiate: {VillageExteriorKitScenePath}.");

        try
        {
            var kitRoot = importedRoot;
            if (!string.Equals(importedRoot.Name.ToString(), VillageExteriorKitRootName, StringComparison.Ordinal))
            {
                kitRoot = importedRoot.GetNodeOrNull<Node3D>(VillageExteriorKitRootName)
                    ?? throw new InvalidOperationException(
                        $"Act I village exterior neighbor parcel is missing authored root '{VillageExteriorKitRootName}'.");
            }

            var collisionNodes = FindDescendants<Node>(kitRoot)
                .Where(node => node is CollisionObject3D or CollisionShape3D)
                .ToArray();
            if (collisionNodes.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Act I village exterior neighbor parcel kit must be presentation-only; found {collisionNodes.Length} collision nodes.");
            }

            var missingComponents = placements
                .Where(placement => kitRoot.GetNodeOrNull<Node3D>(placement.ComponentName) is null)
                .Select(placement => placement.ComponentName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (missingComponents.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Act I village exterior neighbor parcel kit is missing required direct component roots: {string.Join('|', missingComponents)}.");
            }

            foreach (var placement in placements)
            {
                var attached = AttachAct1ExteriorKitComponent(
                    parcel,
                    kitRoot.GetNode<Node3D>(placement.ComponentName),
                    placement.PlacementName,
                    placement.LocalAnchor,
                    placement.YawDegrees,
                    placement.Scale,
                    placement.LogicalAnchor,
                    VillageExteriorKitScenePath);
                if (string.Equals(placement.ComponentName, "DwellingFacade_TimberPlaster", StringComparison.Ordinal))
                {
                    AddBabaiRearFacadeDressing(attached, Vector3.Zero, 0f);
                    if (string.Equals(placement.PlacementName, "ArrivalForwardWestFacade", StringComparison.Ordinal))
                    {
                        ApplyHeroWarmWindow(attached);
                    }
                }
            }

            // Every component has now been detached and reparented below the
            // named parcel. The temporary imported source root is never kept as
            // a second presentation owner or as a preview-board transform.
            // All authored components have been detached at this point, so a
            // synchronous free avoids leaving one queued wrapper per parcel in
            // headless shutdown when the connected world owns many parcels.
            importedRoot.Free();
        }
        catch
        {
            if (GodotObject.IsInstanceValid(importedRoot))
            {
                importedRoot.Free();
            }

            throw;
        }
    }

    private static Node3D AttachAct1ExteriorKitComponent(
        Node3D parent,
        Node3D component,
        string placementName,
        Vector3 anchor,
        float yawDegrees,
        Vector3 scale,
        string logicalAnchor,
        string assetSource = VillageExteriorKitScenePath)
    {
        var sourceParent = component.GetParent()
            ?? throw new InvalidOperationException(
                $"Act I authored kit component '{component.Name}' has no authored root parent.");
        var authoredPreviewOrigin = component.Position;
        var importedAncestorBasis = ComposeImportedAncestorBasis(component);
        sourceParent.RemoveChild(component);

        // An extracted GLB subtree still carries the imported scene root as
        // Owner. That owner is no longer an ancestor after extraction, so
        // clear Node.Owner recursively before the runtime reparent; runtime
        // ownership metadata above remains unchanged.
        ClearExtractedSceneOwnership(component);

        // Preserve the imported GLB coordinate/scale basis while removing only
        // the neutral preview-board origin. The placement node owns the world
        // anchor/yaw/scale; the component keeps the source conversion basis.
        //
        // The legacy wet-road kit was authored in Blender with its long road
        // axis on the exported Y channel. Godot imports that GLB as Y-up, so
        // an uncorrected component becomes a vertical wall (the 28 m road
        // length lands in world Y instead of world Z). Apply the one explicit
        // source correction at extraction time; all newer authored kits use
        // the canonical Godot-facing basis and remain unchanged.
        var correctedBasis = importedAncestorBasis;
        if (string.Equals(assetSource, WetVillageRoadKitScenePath, StringComparison.Ordinal))
        {
            correctedBasis = Basis.FromEuler(new Vector3(Mathf.Pi * 0.5f, 0f, 0f)) * correctedBasis;
        }

        component.Transform = new Transform3D(correctedBasis, Vector3.Zero);
        component.SetMeta("presentationOnly", true);
        component.SetMeta("visualOnly", true);
        component.SetMeta("presentationOnlyInstance", true);
        component.SetMeta("assetSource", assetSource);
        component.SetMeta("authoredPreviewOrigin", authoredPreviewOrigin);
        component.SetMeta(
            "rebasedLocalTransform",
            "imported ancestor basis preserved; preview-board origin removed before placement");
        if (string.Equals(assetSource, WetVillageRoadKitScenePath, StringComparison.Ordinal))
        {
            component.SetMeta(
                "sourceAxisCorrection",
                "legacy wet-road GLB Y-length rotated +90deg around local X into Godot Z-length");
        }
        component.SetMeta("collisionOwner", "none");
        component.SetMeta("navigationOwner", "none");
        component.SetMeta("interactionOwner", "none");
        component.SetMeta("logicalAnchor", logicalAnchor);

        var placement = new Node3D
        {
            Name = placementName,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Scale = scale
        };
        placement.SetMeta("presentationOnly", true);
        placement.SetMeta("visualOnly", true);
        placement.SetMeta("collisionOwner", "none");
        placement.SetMeta("navigationOwner", "none");
        placement.SetMeta("interactionOwner", "none");
        placement.SetMeta("assetSource", assetSource);
        placement.SetMeta("logicalAnchor", logicalAnchor);
        parent.AddChild(placement);
        placement.AddChild(component);
        if (string.Equals(component.Name.ToString(), "DwellingFacade_TimberPlaster", StringComparison.Ordinal))
        {
            // The imported dwelling already owns the pitched roof, porch and
            // openings. These shallow, non-colliding planes break its broad
            // plaster face without replacing any authored GLB child.
            AddVisualBox(placement, "FacadePlasterPanelWest", new(1.90f, 0.68f, 0.08f), new(-2.00f, 0.84f, 1.43f), "5b625b", "plaster", rollDegrees: -2f);
            AddVisualBox(placement, "FacadePlasterPanelEast", new(2.20f, 0.82f, 0.08f), new(1.70f, 0.96f, 1.44f), "6a6658", "plaster", rollDegrees: 1.5f);
            AddVisualBox(placement, "FacadeTimberDiagonalBrace", new(0.14f, 1.90f, 0.10f), new(-0.45f, 1.58f, 1.48f), "4b433a", "wood", rollDegrees: -17f);
        }
        return placement;
    }

    private static Basis ComposeImportedAncestorBasis(Node3D component)
    {
        // PackedScene instances are not inside the runtime tree yet, so use
        // local transforms only. Godot composes parent-to-child bases as
        // parentBasis * childBasis; translations are intentionally excluded
        // because every imported root's translation is a preview-board offset.
        var basis = component.Transform.Basis;
        var ancestor = component.GetParent();
        while (ancestor is not null)
        {
            if (ancestor is Node3D ancestorNode)
            {
                basis = ancestorNode.Transform.Basis * basis;
            }

            ancestor = ancestor.GetParent();
        }

        return basis;
    }

    private static void ClearExtractedSceneOwnership(Node node)
    {
        node.Owner = null;
        foreach (var child in node.GetChildren())
        {
            ClearExtractedSceneOwnership(child);
        }
    }

    private static void HideCorePresentationNode(Node3D core, string relativePath, string reason)
        => HideCorePresentationNode(core, relativePath, reason, required: true);

    private static void HideCorePresentationNode(
        Node3D core,
        string relativePath,
        string reason,
        bool required)
    {
        var node = core.GetNodeOrNull<Node>(relativePath)
            ?? (required
                ? throw new InvalidOperationException(
                    $"Act I authored exterior kit suppression target is missing: {relativePath}.")
                : null);
        if (node is null)
        {
            return;
        }
        if (!HasTrueMeta(node, "visualOnly") && !HasTrueMeta(node, "presentationOnly"))
        {
            throw new InvalidOperationException(
                $"Act I authored exterior kit suppression target is not presentation-only: {node.GetPath()}.");
        }

        var protectedNodes = new[] { node }
            .Concat(FindDescendants<Node>(node))
            .Where(IsProtectedGameplayNode)
            .ToArray();
        if (protectedNodes.Length > 0)
        {
            throw new InvalidOperationException(
                $"Act I authored exterior kit suppression target owns protected gameplay nodes: {node.GetPath()} => "
                + string.Join('|', protectedNodes.Select(protectedNode => protectedNode.GetType().Name)));
        }

        node.SetMeta("authoredExteriorKitSuppressionReason", reason);
        HidePresentationNode(node);
    }

    private static Node3D CoreVisualZone(Node3D parent, string name)
    {
        var zone = new Node3D { Name = name };
        zone.SetMeta("presentationOnly", true);
        zone.SetMeta("visualOnly", true);
        parent.AddChild(zone);
        return zone;
    }

    private static void BuildCoreArrival(Node3D parent)
    {
        AddCoreRouteEnvelope(
            parent,
            "ArrivalRoadEnvelope",
            new(0f, 0.02f, 20f),
            new(0f, 0.02f, -1.5f),
            5.6f,
            "69705a",
            "4b5751",
            4.8f);

        AddCoreBuilding(parent, "ArrivalWestHouseVolume", new(-18.0f, 0f, 38.0f), new(9.2f, 3.15f, 6.2f), 3.15f, 180f, "6f6e61", "4b4339", "a18f70", false, true);
        AddCoreBuilding(parent, "ArrivalEastBarnVolume", new(18.4f, 0f, 39.0f), new(10.6f, 2.65f, 6.8f), 2.65f, 180f, "65685f", "403d38", "93876c", true, false);
        AddCoreBuilding(parent, "ArrivalWestNearSetbackHouse", new(-12.8f, 0f, 25.8f), new(6.4f, 2.75f, 4.8f), 2.7f, 176f, "746e5b", "4e4438", "aa9472", false, true);
        AddVisualShed(parent, "ArrivalEastNearParcelShed", new(11.8f, 0f, 25.0f), 0.72f, 184f, "62675f", "403d38");
        AddCoreBuilding(parent, "ArrivalHorizonWestVolume", new(-29.5f, 0f, 49.5f), new(8.2f, 2.35f, 4.8f), 2.35f, 176f, "59615a", "3e4540", "877d67", false, false);
        AddCoreBuilding(parent, "ArrivalHorizonEastVolume", new(29.0f, 0f, 50.5f), new(9.0f, 2.55f, 5.2f), 2.55f, 184f, "5d625a", "41423c", "8f836a", true, false);

        AddVisualFenceRun(parent, "ArrivalWestParcelBoundary", new(-9.2f, 0f, 25.5f), new(-22.0f, 0f, 44.5f));
        AddVisualFenceRun(parent, "ArrivalEastParcelBoundary", new(9.0f, 0f, 25.0f), new(22.8f, 0f, 45.0f));
        AddVisualFenceRun(parent, "ArrivalWestNearSetbackFence", new(-5.2f, 0f, 17.0f), new(-10.2f, 0f, 25.0f));
        AddVisualFenceRun(parent, "ArrivalEastNearSetbackFence", new(5.1f, 0f, 16.5f), new(10.0f, 0f, 24.5f));
        AddVisualFenceRun(parent, "ArrivalWestHorizonBoundary", new(-22.0f, 0f, 44.5f), new(-30.0f, 0f, 50.0f));
        AddVisualFenceRun(parent, "ArrivalEastHorizonBoundary", new(22.8f, 0f, 45.0f), new(30.0f, 0f, 51.0f));
        AddVisualGate(parent, "ArrivalWestNearSetbackGate", new(-6.0f, 0f, 20.0f), 1.55f, 1.15f, 176f);
        AddVisualGate(parent, "ArrivalEastNearSetbackGate", new(6.1f, 0f, 19.6f), 1.45f, 1.10f, 184f);

        AddVisualTree(parent, "ArrivalNearBirch", new(-7.2f, 0f, 27.0f), 6.3f, VegetationStyle.Birch, "68705a");
        AddVisualTree(parent, "ArrivalNearBroadleaf", new(7.1f, 0f, 28.5f), 5.7f, VegetationStyle.Broadleaf, "53634e");
        AddVisualTree(parent, "ArrivalFarConifer", new(-31.0f, 0f, 47.0f), 9.6f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ArrivalFarBirch", new(31.5f, 0f, 48.5f), 8.8f, VegetationStyle.Birch, "596047");

        // The reverse first-person envelope looks through the arrival tail,
        // not into an empty field. The old horizon mass is intentionally
        // retired; these two authored facades keep the road gap readable
        // while a varied far tree line closes the village edge behind them.
        AddVillageFacade(parent, "ArrivalReverseEdgeHouseWest", new(-7.8f, 0f, 52.0f), new(5.8f, 2.75f, 4.5f), 2.6f, 180f, "706b59", "4d443a", "a18a6c", false, true);
        AddVillageFacade(parent, "ArrivalReverseEdgeHouseEast", new(7.9f, 0f, 52.8f), new(5.6f, 2.55f, 4.2f), 2.45f, 184f, "657067", "494239", "958870", true, false);
        AddDistantHouse(parent, new(0f, 0f, 52f), 180f, "ArrivalReverseFarCenterHouse");
        AddDistantHouse(parent, new(14f, 0f, 54f), 176f, "ArrivalReverseFarEastHouse");
        AddVisualFenceRun(parent, "ArrivalReverseFarParcelFence", new(-8f, 0f, 50f), new(8f, 0f, 50f));
        AddVisualTree(parent, "ArrivalReverseEdgeBirchWest", new(-23.0f, 0f, 56.5f), 8.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ArrivalReverseEdgeConiferWest", new(-13.5f, 0f, 58.0f), 10.2f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ArrivalReverseEdgeBroadleafCenter", new(0.5f, 0f, 58.5f), 7.4f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "ArrivalReverseEdgeConiferEast", new(14.4f, 0f, 57.6f), 9.6f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ArrivalReverseEdgeBirchEast", new(24.5f, 0f, 56.8f), 8.6f, VegetationStyle.Birch, "596047");

        AddVisualParcelPatch(parent, "ArrivalWetShoulderWest", new(-5.0f, 0.08f, 30.0f), new(2.8f, 0.12f, 13.5f), "4b5751", 4f);
        AddVisualParcelPatch(parent, "ArrivalWetShoulderEast", new(5.0f, 0.07f, 31.0f), new(2.6f, 0.10f, 12.8f), "53605a", -5f);
        AddCoreFacetedMass(parent, "ArrivalVillageHorizonMass", new(0f, 2.0f, 53.0f), new(18.0f, 2.2f, 2.8f), "4f5b55");

        // Closure pass: low field banks and offset silhouettes give both road
        // directions a near/mid/far edge while leaving the route window open.
        AddVisualLandformSegment(parent, "ArrivalClosureForwardWestBank", new(-18.0f, 0f, 13.0f), new(-24.5f, 0f, -2.5f), 2.25f, 0.30f, "4d594b", "earth", 0.02f);
        AddVisualLandformSegment(parent, "ArrivalClosureForwardEastBank", new(18.0f, 0f, 12.5f), new(24.5f, 0f, -3.0f), 2.05f, 0.27f, "465448", "earth", 0.02f);
        AddVisualLandformSegment(parent, "ArrivalClosureReverseWestBank", new(-24.5f, 0f, 41.5f), new(-34.0f, 0f, 58.5f), 2.75f, 0.40f, "53604e", "earth", 0.02f);
        AddVisualLandformSegment(parent, "ArrivalClosureReverseEastBank", new(24.0f, 0f, 42.0f), new(34.5f, 0f, 58.0f), 2.55f, 0.36f, "4d5a4b", "earth", 0.02f);
        AddVisualTree(parent, "ArrivalClosureForwardWestBirch", new(-24.5f, 0f, -4.0f), 7.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ArrivalClosureForwardEastConifer", new(25.0f, 0f, -6.0f), 9.1f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ArrivalClosureForwardFarWestBroadleaf", new(-35.0f, 0f, -15.0f), 7.4f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "ArrivalClosureForwardFarEastBirch", new(35.5f, 0f, -17.5f), 8.3f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ArrivalClosureReverseWestConifer", new(-34.5f, 0f, 59.5f), 10.0f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ArrivalClosureReverseEastBroadleaf", new(35.0f, 0f, 60.0f), 8.2f, VegetationStyle.Broadleaf, "48553f");

        // A sparse second tree line keeps the reverse horizon inhabited past
        // the parcel houses without closing the road window. The staggered
        // depths and mixed silhouettes avoid a repeated cone rhythm.
        AddVisualTree(parent, "ArrivalReverseHorizonBirchFarWest", new(-28.5f, 0f, 64.0f), 10.4f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ArrivalReverseHorizonBroadleafMidWest", new(-17.0f, 0f, 67.0f), 8.0f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "ArrivalReverseHorizonConiferEast", new(19.5f, 0f, 66.0f), 11.2f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ArrivalReverseHorizonBroadleafFarEast", new(31.0f, 0f, 63.0f), 8.8f, VegetationStyle.Broadleaf, "48553f");

    }

    private static void BuildCoreMainStreet(Node3D parent)
    {
        AddCoreRouteEnvelope(
            parent,
            "MainStreetRoadEnvelope",
            new(0f, 0.02f, 7.5f),
            new(0f, 0.02f, -19.0f),
            5.6f,
            "6d6958",
            "46514b",
            4.9f);

        AddCoreBuilding(parent, "MainStreetWestHouseVolume", new(-15.8f, 0f, -3.0f), new(8.2f, 3.0f, 5.8f), 3.0f, 90f, "707065", "49433b", "a18f70", false, true);
        AddCoreBuilding(parent, "MainStreetEastBarnVolume", new(16.2f, 0f, -10.5f), new(8.8f, 2.7f, 5.7f), 2.7f, -90f, "62675f", "403f3a", "95866b", true, false);
        AddCoreBuilding(parent, "MainStreetWestNearSetbackHouse", new(-11.8f, 0f, 5.2f), new(5.8f, 2.55f, 4.2f), 2.45f, 92f, "786f5e", "4d4037", "b49c78", false, true);
        AddVisualShed(parent, "MainStreetEastNearWorkshop", new(11.7f, 0f, -2.0f), 0.66f, -88f, "5d635c", "3d403b");
        AddCoreBuilding(parent, "MainStreetWestBackVolume", new(-20.5f, 0f, -17.8f), new(7.0f, 2.5f, 5.0f), 2.5f, 92f, "676b61", "454039", "927e62", true, false);
        AddCoreBuilding(parent, "MainStreetEastBackVolume", new(21.0f, 0f, -20.8f), new(8.6f, 2.9f, 5.2f), 2.9f, -88f, "5f655d", "3c403c", "8f846d", false, false);

        AddVisualFenceRun(parent, "MainStreetWestNearFence", new(-8.0f, 0f, 8.5f), new(-9.2f, 0f, -1.0f));
        AddVisualFenceRun(parent, "MainStreetEastNearFence", new(8.0f, 0f, 8.5f), new(9.5f, 0f, 0.0f));
        AddVisualFenceRun(parent, "MainStreetWestBackFence", new(-11.0f, 0f, -9.0f), new(-19.0f, 0f, -16.0f));
        AddVisualFenceRun(parent, "MainStreetEastBackFence", new(11.0f, 0f, -10.0f), new(19.5f, 0f, -17.5f));
        AddVisualGate(parent, "MainStreetWestParcelGate", new(-8.7f, 0f, 3.5f), 1.7f, 1.25f, 90f);
        AddVisualGate(parent, "MainStreetEastParcelGate", new(8.8f, 0f, 2.8f), 1.6f, 1.18f, -90f);
        AddVisualFenceRun(parent, "MainStreetFapBranchParcelEdge", new(5.5f, 0f, -8.8f), new(10.8f, 0f, -13.0f));
        AddVisualLandformSegment(parent, "MainStreetFapBranchApron", new(3.6f, 0.025f, -7.7f), new(8.2f, 0.025f, -11.3f), 1.10f, 0.045f, "6a624d", "earth");

        AddVisualShed(parent, "MainStreetWestShedVolume", new(-12.6f, 0f, -7.2f), 0.78f, 95f, "6f6d60", "433e38");
        AddVisualShed(parent, "MainStreetEastShedVolume", new(12.8f, 0f, -14.8f), 0.72f, -86f, "5d635c", "3d403b");
        AddVisualTree(parent, "MainStreetNearBirch", new(-7.0f, 0f, 13.0f), 6.4f, VegetationStyle.Birch, "68705a");
        AddVisualTree(parent, "MainStreetNearBroadleaf", new(7.2f, 0f, 12.0f), 5.9f, VegetationStyle.Broadleaf, "53634e");
        AddVisualTree(parent, "MainStreetFarConifer", new(-27.5f, 0f, -17.5f), 9.2f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "MainStreetFarBirch", new(28.0f, 0f, -21.0f), 8.6f, VegetationStyle.Birch, "596047");
        AddVisualParcelPatch(parent, "MainStreetWestFieldBreak", new(-23.0f, 0.06f, -5.0f), new(8.0f, 0.24f, 22.0f), "5f6653", -6f);
        AddVisualParcelPatch(parent, "MainStreetEastFieldBreak", new(23.0f, 0.06f, -9.0f), new(8.5f, 0.24f, 24.0f), "56624f", 5f);
        // Low, broken field edges close the side turns without becoming a
        // wall. Mixed tree silhouettes keep the MainStreet horizon authored
        // while preserving the road window and distant sky.
        AddVisualLandformSegment(parent, "MainStreetWestHorizonBank", new(-49f, 0f, 15f), new(-49f, 0f, -30f), 3.4f, 1.25f, "4b594b", "earth", 0.02f);
        AddVisualLandformSegment(parent, "MainStreetEastHorizonBank", new(49f, 0f, 14f), new(49f, 0f, -31f), 3.0f, 1.15f, "465448", "earth", 0.02f);
        AddVisualTree(parent, "MainStreetWestHorizonBirch", new(-48f, 0f, 8f), 8.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "MainStreetWestHorizonBroadleaf", new(-51f, 0f, -10f), 7.3f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "MainStreetEastHorizonConifer", new(50f, 0f, 5f), 9.2f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "MainStreetEastHorizonBirch", new(48f, 0f, -24f), 8.4f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "MainStreetEastFarEdgeBroadleaf", new(50f, 0f, -4f), 7.4f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "MainStreetEastFarEdgeBirch", new(50f, 0f, -18f), 8.0f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "MainStreetEastFarEdgeConifer", new(50f, 0f, -30f), 9.0f, VegetationStyle.Conifer, "30483f");
        AddVisualShrub(parent, "MainStreetWestHorizonShrub", new(-46.5f, 0f, -22f), 1.15f, "48553f");
        AddVisualShrub(parent, "MainStreetEastHorizonShrub", new(46.5f, 0f, -8f), 1.05f, "53634e");
        AddDistantHouse(parent, new(36f, 0f, -4f), -92f, "MainStreetEastLateralHouse");
        AddVisualFenceRun(parent, "MainStreetEastLateralFence", new(32f, 0f, -7f), new(40f, 0f, -7f));
        AddDistantHouse(parent, new(44f, 0f, -12f), -90f, "MainStreetEastFarParcelHouse");
        AddVisualFenceRun(parent, "MainStreetEastFarParcelFence", new(40f, 0f, -15f), new(48f, 0f, -15f));
        AddVisualTree(parent, "MainStreetEastLateralBirch", new(39f, 0f, 3f), 7.4f, VegetationStyle.Birch, "596047");
        AddAuthoredHouse(parent, "MainStreetEastNearMidHouse", new(26f, 1.0f, -8.5f), 0.48f, -94f);
        AddVisualShed(parent, "MainStreetEastNearMidShed", new(22f, 0f, -15f), 0.60f, -88f, "5d635c", "3d403b");
        AddVisualFenceRun(parent, "MainStreetEastNearMidFence", new(20.5f, 0f, -5f), new(30f, 0f, -8f));
        AddVisualLandformSegment(parent, "MainStreetEastNearMidBank", new(21f, 0f, -4f), new(31f, 0f, -9f), 1.25f, 0.18f, "4d594d", "earth", 0.02f);
        AddVisualTree(parent, "MainStreetEastNearMidBirch", new(30f, 0f, -15f), 7.4f, VegetationStyle.Birch, "596047");
        AddVisualLandformSegment(parent, "MainStreetEastOuterFieldBank", new(31f, 0f, -22f), new(38f, 0f, -25f), 1.55f, 0.18f, "4d594d", "earth", 0.02f);
        AddVisualFenceRun(parent, "MainStreetEastOuterFieldFence", new(37f, 0f, -25f), new(44f, 0f, -29f));
        AddVisualTree(parent, "MainStreetEastOuterFieldBroadleaf", new(33f, 0f, -27f), 6.9f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "MainStreetEastOuterFieldBirch", new(39f, 0f, -22.5f), 7.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "MainStreetEastOuterFieldConifer", new(44f, 0f, -29f), 8.4f, VegetationStyle.Conifer, "30483f");
        AddCoreFacetedMass(parent, "MainStreetDistantVillageMass", new(0f, 2.4f, -27.0f), new(17.0f, 2.8f, 2.6f), "4b5952");

        // Closure pass: pull a low, irregular field edge into the side sightlines
        // so the road reads as a village lane instead of a flat strip under sky.
        AddVisualLandformSegment(parent, "MainStreetClosureForwardWestBank", new(-24.0f, 0f, 7.0f), new(-29.0f, 0f, -20.0f), 2.35f, 0.34f, "4b594b", "earth", 0.02f);
        AddVisualLandformSegment(parent, "MainStreetClosureForwardEastBank", new(24.0f, 0f, 6.5f), new(29.5f, 0f, -20.5f), 2.10f, 0.30f, "465448", "earth", 0.02f);
        AddVisualLandformSegment(parent, "MainStreetClosureReverseWestBank", new(-25.0f, 0f, 12.5f), new(-32.0f, 0f, 30.5f), 2.65f, 0.38f, "53604e", "earth", 0.02f);
        AddVisualLandformSegment(parent, "MainStreetClosureReverseEastBank", new(25.0f, 0f, 12.0f), new(32.5f, 0f, 31.5f), 2.45f, 0.34f, "4d5a4b", "earth", 0.02f);
        AddVisualTree(parent, "MainStreetClosureForwardWestBirch", new(-27.0f, 0f, -18.0f), 8.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "MainStreetClosureForwardEastConifer", new(28.5f, 0f, -20.0f), 9.6f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "MainStreetClosureForwardFarWestBroadleaf", new(-35.0f, 0f, -28.0f), 7.9f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "MainStreetClosureForwardFarEastBirch", new(35.5f, 0f, -31.0f), 8.4f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "MainStreetClosureReverseWestConifer", new(-31.0f, 0f, 29.5f), 9.4f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "MainStreetClosureReverseEastBroadleaf", new(32.0f, 0f, 31.0f), 8.0f, VegetationStyle.Broadleaf, "48553f");

    }

    private static void BuildCoreBabaiEbiYard(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("house_old_pc", out var placement))
        {
            return;
        }

        var origin = placement.Origin;
        var approach = HorizontalDirection(GetConnector("arrival-to-house-yard").End - origin);
        var side = new Vector3(approach.Z, 0f, -approach.X);
        var yaw = DirectionYaw(approach);
        var yardConnector = GetConnector("arrival-to-house-yard");

        AddCoreRouteEnvelope(
            parent,
            "BabaiYardArrivalRoadEnvelope",
            yardConnector.Start,
            yardConnector.End,
            yardConnector.Width,
            "69705a",
            "4b5751",
            3.8f);

        AddVisualParcelPatch(parent, "BabaiYardWestGround", origin + side * -6.5f + approach * 0.8f, new(8.4f, 0.16f, 9.2f), "68705a", yaw + 5f);
        AddVisualParcelPatch(parent, "BabaiYardEastWetGround", origin + side * 6.5f - approach * 0.6f, new(7.8f, 0.14f, 8.6f), "53605a", yaw - 7f);
        AddCoreBuilding(parent, "BabaiYardBarnVolume", origin + side * -9.2f - approach * 2.0f, new(6.8f, 2.75f, 5.6f), 2.75f, yaw + 12f, "6e6d61", "4b4239", "948266", true, false);
        AddCoreBuilding(parent, "BabaiYardBackHouseVolume", origin + side * 9.5f - approach * 5.0f, new(7.2f, 2.8f, 5.5f), 2.8f, yaw - 16f, "696b63", "463f39", "8f8068", false, false);
        AddVisualFenceRun(parent, "BabaiYardWestBoundary", origin + side * -11.5f - approach * 5.0f, origin + side * -11.5f + approach * 6.0f);
        AddVisualFenceRun(parent, "BabaiYardEastBoundary", origin + side * 11.5f - approach * 5.0f, origin + side * 11.5f + approach * 5.8f);
        AddVisualFenceRun(parent, "BabaiYardStreetWestBoundary", origin + approach * 6.2f + side * -11.5f, origin + approach * 6.2f + side * -2.3f);
        AddVisualFenceRun(parent, "BabaiYardStreetEastBoundary", origin + approach * 6.2f + side * 2.4f, origin + approach * 6.2f + side * 11.5f);
        AddVisualGate(parent, "BabaiYardOpenGate", origin + approach * 6.0f, 2.2f, 1.35f, yaw);
        AddVisualLandformSegment(parent, "BabaiYardClearEntryPath", origin + approach * 6.0f, origin + approach * 0.6f, 1.45f, 0.045f, "685b49", "earth");
        AddVisualLandformSegment(parent, "BabaiYardToolShedDrive", origin + side * 5.7f + approach * 4.5f, origin + side * 7.0f + approach * 3.8f, 1.05f, 0.035f, "625747", "earth");
        AddVisualShed(parent, "BabaiYardToolShed", origin + side * 7.0f + approach * 3.8f, 0.66f, yaw - 18f, "62655d", "3f403c");
        AddVisualWoodpile(parent, "BabaiYardWoodpile", origin + side * -5.7f - approach * 3.0f, 1.05f, yaw + 90f);
        AddVisualTree(parent, "BabaiYardBirchMass", origin + side * -13.2f - approach * 1.5f, 7.3f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "BabaiYardBroadleafMass", origin + side * 13.4f + approach * 2.0f, 6.6f, VegetationStyle.Broadleaf, "48553f");
        // Keep the yard's continuation beyond the capture position and gate.
        // The previous 15 m anchor put this large mass directly in the yard
        // sightline, where it read as a near dark wall instead of a distant
        // street cue.
        AddCoreFacetedMass(parent, "BabaiYardDistantStreetMass", origin + approach * 24.0f, new(9.0f, 2.3f, 2.2f), "4d5a53");
    }

    private static void BuildCoreHouseExterior(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("house_old_pc", out var placement))
        {
            return;
        }

        var origin = placement.Origin + new Vector3(0f, 0f, -3.4f);
        AddCoreBuilding(parent, "BabaiEbiHouseFullVolume", origin, new(8.6f, 3.45f, 5.9f), 3.45f, 0f, "62645b", "453f39", "8a7a64", false, true);
        AddVisualBox(parent, "BabaiEbiHouseThreshold", new(2.8f, 0.16f, 1.20f), placement.Origin + new Vector3(-2.3f, 0.12f, 0.82f), "66503e", "wood");
        AddVisualBox(parent, "BabaiEbiHouseWindowGlow", new(1.35f, 0.86f, 0.06f), placement.Origin + new Vector3(0.9f, 1.65f, -0.32f), "a88a5f", "glass");
        AddVisualBox(parent, "BabaiEbiHousePorchWarmth", new(0.54f, 0.34f, 0.05f), placement.Origin + new Vector3(-2.3f, 1.72f, 0.58f), "b89462", "glass");
        AddVisualLandformSegment(parent, "BabaiEbiHouseEntryPath", placement.Origin + new Vector3(-2.3f, 0.04f, 5.7f), placement.Origin + new Vector3(-2.3f, 0.04f, 0.92f), 1.42f, 0.045f, "685b49", "earth");
        AddCoreBuilding(parent, "BabaiEbiHouseSideOutbuilding", new(-37.8f, 0f, -5.6f), new(5.5f, 2.3f, 4.3f), 2.3f, 90f, "59625b", "403c37", "817562", true, false);
        AddVisualFenceRun(parent, "BabaiEbiHouseBackFence", new(-36.0f, 0f, -10.2f), new(-20.0f, 0f, -10.2f));
        AddVisualFenceRun(parent, "BabaiEbiHouseApproachFenceWest", new(-36.0f, 0f, 0.8f), new(-34.0f, 0f, 5.4f));
        AddVisualFenceRun(parent, "BabaiEbiHouseApproachFenceEast", new(-22.0f, 0f, 0.8f), new(-22.0f, 0f, 5.3f));
        // The west lateral turn previously ended on a flat field beyond the
        // house wall. A restrained distant parcel keeps that 360-degree read
        // rural and continuous without entering the route or yard envelope.
        AddDistantHouse(parent, new(-40.0f, 0f, 5.5f), 90f, "BabaiWestFieldNeighborHouse");
        AddVisualFenceRun(parent, "BabaiWestFieldNeighborFence", new(-34.0f, 0f, 4.2f), new(-47.0f, 0f, 4.2f));
        AddVisualTree(parent, "BabaiWestFieldNeighborBirch", new(-45.0f, 0f, 9.0f), 7.6f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "BabaiWestFieldNeighborConifer", new(-37.0f, 0f, 12.0f), 8.2f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "BabaiEbiHouseNearBirch", new(-38.0f, 0f, 0.7f), 6.4f, VegetationStyle.Birch, "596047");
        AddDistantHouse(parent, new(-7.0f, 0f, 18.0f), 180f, "BabaiReverseFieldNeighborHouse");
        AddVisualFenceRun(parent, "BabaiReverseFieldNeighborFence", new(-12.0f, 0f, 16.0f), new(-3.0f, 0f, 16.0f));
        AddVisualTree(parent, "BabaiReverseFieldNeighborBirch", new(-3.5f, 0f, 20.5f), 7.5f, VegetationStyle.Birch, "596047");
        AddCoreFacetedMass(parent, "BabaiEbiHouseStreetMemoryMass", new(-30.0f, 2.1f, 8.2f), new(7.0f, 2.0f, 2.0f), "55615a");
    }

    private static void ApplyHeroWarmWindow(Node3D facade)
    {
        var warmWindow = FindDescendants<MeshInstance3D>(facade)
            .FirstOrDefault(mesh => mesh.Name.ToString().Contains("WindowWarmInset", StringComparison.Ordinal));
        if (warmWindow is null)
        {
            return;
        }

        warmWindow.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml("b6814f"),
            EmissionEnabled = true,
            Emission = Color.FromHtml("9b5a32"),
            EmissionEnergyMultiplier = 1.35f,
            Roughness = 0.48f
        };
        warmWindow.SetMeta("presentationOnly", true);
        warmWindow.SetMeta("visualOnly", true);
        warmWindow.SetMeta("lightingRole", "hero dwelling warm window");
    }

    private static void AddBabaiRearFacadeDressing(Node3D parent, Vector3 origin, float yawDegrees)
    {
        var dressing = new Node3D
        {
            Name = "BabaiDwellingRearFacadeDressing",
            Position = origin,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Scale = Vector3.One * 0.82f
        };
        dressing.SetMeta("presentationOnly", true);
        dressing.SetMeta("visualOnly", true);
        dressing.SetMeta("authoredDetailRole", "rear and side lived-in facade detail for first-person 360 review");
        dressing.SetMeta("collisionOwner", "none");
        dressing.SetMeta("interactionOwner", "none");
        parent.AddChild(dressing);

        // The hero dwelling is the one place where a restrained warm source
        // should survive the reverse turn. Keep it local to the presentation
        // facade; distant parcel copies stay material-only and do not create a
        // second global lighting owner.
        if (string.Equals(parent.Name.ToString(), "BabaiApproachDwellingFacade", StringComparison.Ordinal))
        {
            var rearWindowLight = new OmniLight3D
            {
                Name = "BabaiRearWindowWarmLight",
                Position = new Vector3(-0.15f, 2.45f, -0.30f),
                LightColor = Color.FromHtml("c38d62"),
                LightEnergy = 0.38f,
                OmniRange = 4.6f,
                ShadowEnabled = false
            };
            rearWindowLight.SetMeta("presentationOnly", true);
            rearWindowLight.SetMeta("visualOnly", true);
            rearWindowLight.SetMeta("lightingRole", "hero dwelling rear window warmth");
            rearWindowLight.SetMeta("collisionOwner", "none");
            rearWindowLight.SetMeta("interactionOwner", "none");
            rearWindowLight.SetMeta("runtimeStateOwnership", "RuntimeBridge");
            dressing.AddChild(rearWindowLight);
        }

        // Pull the reverse-facing dressing clear of the imported wall plane;
        // the GLB's rear face is deliberately bare, so this small offset keeps
        // the lived-in detail visible from the opposite parcel.
        const float rearZ = -1.50f;
        const string frame = "685f57";
        var warmWindow = AddVisualBox(
            dressing,
            "RearWindowWarmInset",
            new(1.28f, 0.82f, 0.06f),
            new(-1.45f, 2.48f, rearZ),
            "b6814f",
            "glass");
        warmWindow.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml("b6814f"),
            EmissionEnabled = true,
            Emission = Color.FromHtml("9b5a32"),
            EmissionEnergyMultiplier = 1.35f,
            Roughness = 0.48f
        };
        AddVisualBox(dressing, "RearWindowCoolInset", new(1.28f, 0.82f, 0.06f), new(1.28f, 2.48f, rearZ), "65756e", "glass");
        foreach (var (x, suffix) in new[] { (-1.45f, "Warm"), (1.28f, "Cool") })
        {
            AddVisualBox(dressing, $"RearWindow{suffix}Top", new(1.48f, 0.10f, 0.10f), new(x, 2.98f, rearZ - 0.04f), frame, "wood");
            AddVisualBox(dressing, $"RearWindow{suffix}Bottom", new(1.48f, 0.10f, 0.10f), new(x, 1.98f, rearZ - 0.04f), frame, "wood");
            AddVisualBox(dressing, $"RearWindow{suffix}Left", new(0.10f, 1.08f, 0.10f), new(x - 0.69f, 2.48f, rearZ - 0.04f), frame, "wood");
            AddVisualBox(dressing, $"RearWindow{suffix}Right", new(0.10f, 1.08f, 0.10f), new(x + 0.69f, 2.48f, rearZ - 0.04f), frame, "wood");
        }
        AddVisualBox(dressing, "RearTimberHeader", new(7.10f, 0.14f, 0.14f), new(0f, 3.46f, rearZ - 0.06f), frame, "wood");
        AddVisualBox(dressing, "RearTimberBase", new(7.10f, 0.16f, 0.14f), new(0f, 1.62f, rearZ - 0.06f), frame, "wood");
        AddVisualBox(dressing, "RearTimberPostLeft", new(0.14f, 1.86f, 0.14f), new(-3.26f, 2.54f, rearZ - 0.06f), frame, "wood");
        AddVisualBox(dressing, "RearTimberPostRight", new(0.14f, 1.86f, 0.14f), new(3.26f, 2.54f, rearZ - 0.06f), frame, "wood");
        AddVisualBox(dressing, "RearEave", new(7.65f, 0.14f, 0.46f), new(0f, 3.70f, rearZ - 0.25f), "4f453b", "wood", rollDegrees: -3f);

        AddVisualBox(dressing, "RearDoorPanel", new(1.12f, 1.92f, 0.08f), new(-2.55f, 1.05f, rearZ - 0.04f), "4b382c", "wood");
        AddVisualBox(dressing, "RearDoorHeader", new(1.32f, 0.10f, 0.10f), new(-2.55f, 2.06f, rearZ - 0.08f), frame, "wood");
        AddVisualBox(dressing, "RearDoorPostLeft", new(0.10f, 2.02f, 0.10f), new(-3.16f, 1.05f, rearZ - 0.08f), frame, "wood");
        AddVisualBox(dressing, "RearDoorPostRight", new(0.10f, 2.02f, 0.10f), new(-1.94f, 1.05f, rearZ - 0.08f), frame, "wood");

        foreach (var (sideX, suffix, color) in new[]
                 {
                     (3.72f, "East", "a88a5f"),
                     (-3.72f, "West", "65756e")
                 })
        {
            AddVisualBox(dressing, $"SideWindow{suffix}Inset", new(0.06f, 0.82f, 1.18f), new(sideX, 2.46f, 0.10f), color, "glass");
            AddVisualBox(dressing, $"SideWindow{suffix}Top", new(0.10f, 0.10f, 1.38f), new(sideX + (sideX > 0f ? 0.04f : -0.04f), 2.96f, 0.10f), frame, "wood");
            AddVisualBox(dressing, $"SideWindow{suffix}Bottom", new(0.10f, 0.10f, 1.38f), new(sideX + (sideX > 0f ? 0.04f : -0.04f), 1.96f, 0.10f), frame, "wood");
        }
    }

    private static void BuildCoreConnectiveAndReturn(Node3D parent)
    {
        var connector = GetConnector("house-to-zirat-return");
        AddCoreRouteEnvelope(parent, "ConnectiveReturnRoadEnvelope", connector.Start, connector.End, connector.Width, "625f50", "46524b", 3.8f);
        AddVisualLandformSegment(parent, "ConnectiveReturnWestBank", new(-4.5f, 0f, -16.0f), new(-7.0f, 0f, -48.0f), 1.10f, 0.10f, "5a6351", "earth", 0.02f);
        AddVisualLandformSegment(parent, "ConnectiveReturnEastBank", new(8.8f, 0f, -17.0f), new(13.0f, 0f, -49.0f), 1.45f, 0.18f, "53604e", "earth", 0.02f);

        AddCoreBuilding(parent, "ConnectiveWestHouseVolume", new(-18.0f, 0f, -29.5f), new(7.4f, 2.65f, 5.0f), 2.65f, 92f, "6d6b60", "47413a", "9f8b6e", false, true);
        AddCoreBuilding(parent, "ConnectiveEastBarnVolume", new(17.0f, 0f, -34.0f), new(8.4f, 2.5f, 5.6f), 2.5f, -90f, "60655e", "3f413d", "8f826b", true, false);
        AddCoreBuilding(parent, "ReturnWestFarmVolume", new(-18.5f, 0f, -48.5f), new(8.0f, 2.8f, 5.5f), 2.8f, 88f, "67695f", "433e39", "99886d", false, false);
        AddCoreBuilding(parent, "ReturnEastFarmVolume", new(18.8f, 0f, -51.0f), new(8.8f, 2.6f, 5.0f), 2.6f, -88f, "5f645c", "3f423d", "8f836c", true, false);
        AddVisualFenceRun(parent, "ConnectiveWestParcelFence", new(-19.5f, 0f, -22.0f), new(-15.0f, 0f, -31.0f));
        AddVisualFenceRun(parent, "ConnectiveEastParcelFence", new(9.0f, 0f, -24.0f), new(17.0f, 0f, -32.0f));
        AddVisualFenceRun(parent, "ReturnWestParcelFence", new(-12.0f, 0f, -41.5f), new(-6.0f, 0f, -51.0f));
        AddVisualFenceRun(parent, "ReturnEastParcelFence", new(9.0f, 0f, -42.0f), new(18.0f, 0f, -51.0f));
        AddVisualFenceRun(parent, "ConnectiveWestHouseEdge", new(-20.0f, 0f, -24.0f), new(-15.0f, 0f, -33.0f));
        AddVisualFenceRun(parent, "ReturnEastHouseEdge", new(8.8f, 0f, -46.0f), new(16.8f, 0f, -52.5f));
        AddVisualGate(parent, "ConnectiveWestParcelGate", new(-17.0f, 0f, -29.0f), 1.65f, 1.16f, 154f);
        AddVisualLandformSegment(parent, "ConnectiveWestHouseDrive", new(-10.0f, 0.025f, -27.2f), new(-16.0f, 0.025f, -29.2f), 1.12f, 0.04f, "625747", "earth");
        AddVisualLandformSegment(parent, "ReturnEastFarmDrive", new(8.6f, 0.025f, -45.0f), new(16.2f, 0.025f, -49.8f), 1.10f, 0.04f, "625747", "earth");
        AddVisualShed(parent, "ConnectiveWestShed", new(-11.7f, 0f, -36.5f), 0.70f, 92f, "62655d", "3f403b");
        AddVisualShed(parent, "ReturnEastShed", new(11.5f, 0f, -45.0f), 0.68f, -86f, "5d625b", "3c403c");
        AddVisualTree(parent, "ConnectiveWestBirch", new(-25.0f, 0f, -31.0f), 8.1f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ConnectiveEastBroadleaf", new(25.0f, 0f, -36.5f), 7.2f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "ReturnWestConifer", new(-31.0f, 0f, -58.0f), 6.0f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ReturnEastBirch", new(27.0f, 0f, -55.0f), 8.4f, VegetationStyle.Birch, "596047");
        AddCoreFacetedMass(parent, "ReturnVillageHorizonMass", new(14.0f, 1.05f, -72.0f), new(8.0f, 1.05f, 2.0f), "4d5a53");
    }

    private static void BuildCoreFapExterior(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("fap_clinic", out var placement))
        {
            return;
        }

        var branch = GetConnector("village-to-fap-branch");
        var front = HorizontalDirection(branch.End - placement.Origin);
        var side = new Vector3(front.Z, 0f, -front.X);
        var yaw = DirectionYaw(front);
        var coreOrigin = placement.Origin - front * 1.8f;

        AddCoreRouteEnvelope(parent, "FapBranchRoadEnvelope", branch.Start, branch.End, branch.Width, "625747", "46524b", 3.6f);

        AddCoreBuilding(parent, "FapClinicFullExteriorVolume", coreOrigin, new(12.0f, 3.45f, 8.0f), 3.45f, yaw, "858278", "55493f", "b39b72", false, true);
        AddVisualBox(parent, "FapClinicServiceStep", new(3.8f, 0.16f, 1.35f), placement.Origin + front * 1.6f, "62503d", "wood", yaw);
        AddVisualLandformSegment(parent, "FapClinicEntryPath", placement.Origin + front * 3.4f, placement.Origin + front * 1.0f, 1.65f, 0.045f, "685b49", "earth");
        AddCoreBuilding(parent, "FapClinicServiceBarnVolume", placement.Origin + side * 9.8f - front * 1.5f, new(6.5f, 2.45f, 4.8f), 2.45f, yaw - 12f, "666a62", "45413c", "95866b", true, false);
        AddVisualShed(
            parent,
            "FapEastNearStorageShed",
            placement.Origin + side * 12.5f - front * 0.8f,
            0.72f,
            yaw - 90f,
            "5d645d",
            "3f403b");
        // The east/back side of the clinic opens toward a blank field in the
        // first-person 360-degree envelope. Add one varied neighboring parcel
        // and a restrained tree line here; this is presentation-only and does
        // not touch the branch route or any gameplay owner.
        AddCoreBuilding(
            parent,
            "FapEastHorizonHouse",
            placement.Origin + side * 18.0f - front * 7.0f,
            new(7.2f, 2.65f, 4.8f),
            2.65f,
            yaw - 92f,
            "667067",
            "4b443d",
            "a18b6d",
            true,
            false);
        AddVisualTree(parent, "FapEastHorizonBirch", placement.Origin + side * 22.0f - front * 4.0f, 8.6f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "FapEastHorizonConifer", placement.Origin + side * 20.0f - front * 14.0f, 9.1f, VegetationStyle.Conifer, "30483f");
        AddVisualFenceRun(parent, "FapEastHorizonParcelFence", placement.Origin + side * 18.0f - front * 9.0f, placement.Origin + side * 31.0f - front * 9.0f);
        // The opposite lateral review sector previously opened onto an empty
        // field. Keep this parcel outside the branch envelope while giving
        // the first-person right turn a near/mid silhouette and a readable
        // fence continuation.
        AddDistantHouse(
            parent,
            placement.Origin + side * 8.0f - front * 2.5f,
            yaw + 90f,
            "FapWestFieldNeighborHouse");
        AddDistantHouse(
            parent,
            placement.Origin + new Vector3(8.0f, 0f, 7.0f),
            yaw,
            "FapRightFieldHouse");
        AddVisualFenceRun(
            parent,
            "FapWestFieldNeighborFence",
            placement.Origin + side * 7.0f - front * 1.0f,
            placement.Origin + side * 17.0f - front * 6.0f);
        AddVisualTree(parent, "FapWestFieldNeighborBirch", placement.Origin + side * 13.0f - front * 4.0f, 7.8f, VegetationStyle.Birch, "596047");
        // Mirror one restrained parcel into the opposite lateral review
        // sector; the branch side is not visible from every fixed-camera
        // turn, so a single far house and fence keep the horizon inhabited
        // without touching the route envelope.
        AddDistantHouse(
            parent,
            placement.Origin - side * 16.0f - front * 8.0f,
            yaw - 90f,
            "FapOppositeFieldNeighborHouse");
        AddVisualFenceRun(
            parent,
            "FapOppositeFieldNeighborFence",
            placement.Origin - side * 13.0f - front * 5.0f,
            placement.Origin - side * 28.0f - front * 12.0f);
        AddVisualTree(parent, "FapOppositeFieldNeighborBirch", placement.Origin - side * 25.0f - front * 6.0f, 8.2f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "FapOppositeFieldNeighborConifer", placement.Origin - side * 25.0f - front * 24.0f, 6.0f, VegetationStyle.Conifer, "30483f");
        // The capture's fixed right turn looks along the open east field
        // rather than along the branch vector. Anchor two far silhouettes in
        // that actual view window so the clinic remains part of a village
        // parcel instead of ending on a flat horizon.
        AddDistantHouse(parent, placement.Origin + new Vector3(12.0f, 0f, 10.0f), yaw + 18f, "FapEastFieldViewHouse");
        AddVisualFenceRun(parent, "FapEastFieldViewBoundary", placement.Origin + new Vector3(9.0f, 0f, 7.0f), placement.Origin + new Vector3(27.0f, 0f, 8.0f));
        AddVisualTree(parent, "FapEastFieldViewBirch", placement.Origin + new Vector3(18.0f, 0f, 10.0f), 8.6f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "FapEastFieldViewConifer", placement.Origin + new Vector3(25.0f, 0f, 2.0f), 9.1f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "FapEastHorizonFarBirch", placement.Origin + side * 30.0f - front * 3.0f, 8.0f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "FapEastHorizonFarBroadleaf", placement.Origin + side * 27.0f - front * 20.0f, 8.8f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "FapEastHorizonFarConifer", placement.Origin + side * 32.0f - front * 16.0f, 9.6f, VegetationStyle.Conifer, "30483f");
        AddVisualFenceRun(parent, "FapClinicWestBoundary", placement.Origin + side * -9.2f - front * 4.0f, placement.Origin + side * -9.2f + front * 5.8f);
        AddVisualFenceRun(parent, "FapClinicEastBoundary", placement.Origin + side * 10.5f - front * 3.6f, placement.Origin + side * 10.5f + front * 5.4f);
        AddVisualFenceRun(parent, "FapClinicServiceBoundary", placement.Origin + side * 6.5f + front * 5.0f, placement.Origin + side * 12.5f + front * 5.0f);
        AddVisualGate(parent, "FapClinicOpenServiceGate", placement.Origin + side * 6.5f + front * 4.8f, 1.8f, 1.25f, yaw);
        AddVisualStreetLandmark(parent, "FapApproachWayfinding", placement.Origin + side * -4.7f + front * 3.2f, yaw, "ФАП");
        AddVisualTree(parent, "FapClinicNearBirch", placement.Origin + side * -15.5f - front, 6.8f, VegetationStyle.Birch, "68705a");
        AddVisualTree(parent, "FapClinicNearBroadleaf", placement.Origin + side * 13.0f + front * 1.5f, 6.1f, VegetationStyle.Broadleaf, "53634e");
        AddVisualTree(parent, "FapClinicFarConifer", placement.Origin + side * -16.5f - front * 9.0f, 8.8f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "FapClinicFarBirch", placement.Origin + side * 17.0f - front * 10.0f, 8.3f, VegetationStyle.Birch, "596047");
        AddCoreFacetedMass(parent, "FapClinicReturnVillageMass", placement.Origin - front * 18.0f, new(11.0f, 2.4f, 2.4f), "4f5a53");
    }

    private static void BuildCoreZirat(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("zirat_road", out var placement))
        {
            return;
        }

        var origin = placement.Origin;
        AddCoreRouteEnvelope(parent, "ZiratRoadEnvelope", new(0f, 0.02f, -53.5f), new(0f, 0.02f, -89.5f), 4.2f, "5d624f", "46534c", 3.7f);
        AddVisualLandformSegment(parent, "ZiratWestMemoryBank", new(-7.2f, 0f, -54.0f), new(-8.5f, 0f, -88.8f), 1.4f, 0.18f, "53604e", "earth", 0.02f);
        AddVisualLandformSegment(parent, "ZiratEastMemoryBank", new(7.4f, 0f, -54.5f), new(8.8f, 0f, -88.5f), 1.35f, 0.16f, "4f5a4b", "earth", 0.02f);
        AddVisualParcelPatch(parent, "ZiratWestWetParcel", new(-8.4f, 0.06f, -70.0f), new(7.0f, 0.18f, 18.0f), "4f5a4b", -4f);
        AddVisualParcelPatch(parent, "ZiratEastWetParcel", new(8.6f, 0.06f, -74.0f), new(7.4f, 0.16f, 20.0f), "53604f", 6f);
        AddVisualFenceRun(parent, "ZiratOuterWestBoundary", new(-11.5f, 0f, -55.0f), new(-11.5f, 0f, -84.0f));
        AddVisualFenceRun(parent, "ZiratOuterEastBoundary", new(11.5f, 0f, -55.0f), new(11.5f, 0f, -84.0f));
        AddVisualFenceRun(parent, "ZiratEntryWestBoundary", new(-11.5f, 0f, -55.0f), new(-4.0f, 0f, -55.0f));
        AddVisualFenceRun(parent, "ZiratEntryEastBoundary", new(4.0f, 0f, -55.0f), new(11.5f, 0f, -55.0f));
        AddVisualLandformSegment(parent, "ZiratQuietPath", new(0f, 0.025f, -54.0f), new(0f, 0.025f, -88.8f), 1.55f, 0.035f, "4f574e", "earth");
        var ziratMarkerGroup = new Node3D { Name = "ZiratCoreMarkerGrouping", Position = origin };
        ziratMarkerGroup.SetMeta("presentationOnly", true);
        ziratMarkerGroup.SetMeta("culturalPlaceholder", "quiet low marker grouping; no crosses or inscriptions");
        parent.AddChild(ziratMarkerGroup);
        AddGraveMarker(ziratMarkerGroup, new(-2.2f, 0f, 4.8f), new(0.74f, 1.0f, 0.34f), -5f);
        AddGraveMarker(ziratMarkerGroup, new(2.3f, 0f, -2.8f), new(0.62f, 0.82f, 0.30f), 9f);
        AddGraveMarker(ziratMarkerGroup, new(-1.7f, 0f, -10.5f), new(0.56f, 0.70f, 0.28f), -12f);
        var ziratEntryHeader = AddVisualBox(parent, "ZiratMemoryEntryHeader", new(7.0f, 0.16f, 0.16f), new(0f, 2.05f, -55.0f), "5c5241", "wood");
        // This placeholder header is too close to the standard staging point
        // and reads as an accidental overhead beam. Keep the route, banks and
        // respectful field markers; remove only this core presentation prop.
        ziratEntryHeader.Visible = false;
        ziratEntryHeader.SetMeta("suppressedForConnectedRouteOverlap", true);
        // A few asymmetric, low memorial forms make the cemetery edge read as
        // a lived-in village boundary from the side views. Keep them outside
        // the route envelope and separate from the legacy marker grouping,
        // which the authored roadside kit intentionally suppresses.
        var ziratAuthoredMemorialForms = new Node3D { Name = "ZiratAuthoredMemorialForms" };
        ziratAuthoredMemorialForms.SetMeta("presentationOnly", true);
        ziratAuthoredMemorialForms.SetMeta("visualOnly", true);
        ziratAuthoredMemorialForms.SetMeta("culturalPlaceholder", "quiet low marker forms; no crosses or inscriptions");
        parent.AddChild(ziratAuthoredMemorialForms);
        AddGraveMarker(ziratAuthoredMemorialForms, new(-6.3f, 0f, -72.5f), new(0.70f, 0.94f, 0.32f), -8f);
        AddGraveMarker(ziratAuthoredMemorialForms, new(7.2f, 0f, -84.5f), new(0.58f, 0.78f, 0.28f), 13f);
        AddVisualStoneCluster(ziratAuthoredMemorialForms, "ZiratAuthoredMemorialStoneWest", new(-8.0f, 0f, -80.0f), 0.68f, "777669");
        AddVisualLandformSegment(ziratAuthoredMemorialForms, "ZiratAuthoredMemorialPathShoulder", new(-4.8f, 0.025f, -63.0f), new(-6.2f, 0.025f, -83.5f), 0.95f, 0.045f, "4f574e", "earth");
        AddVisualStoneCluster(parent, "ZiratMemoryStoneWest", new(-3.0f, 0f, -62.0f), 0.76f, "777669");
        AddVisualStoneCluster(parent, "ZiratMemoryStoneEast", new(3.4f, 0f, -68.5f), 0.64f, "6d6c62");
        AddVisualStoneCluster(parent, "ZiratMemoryStoneFar", new(-4.5f, 0f, -79.5f), 0.58f, "6d6c62");
        AddVisualTree(parent, "ZiratMemoryBirchWest", new(-14.2f, 0f, -62.0f), 7.2f, VegetationStyle.Birch, "68705a");
        AddVisualTree(parent, "ZiratMemoryBroadleafEast", new(14.0f, 0f, -68.0f), 6.3f, VegetationStyle.Broadleaf, "53634e");
        AddVisualTree(parent, "ZiratMemoryConiferWest", new(-16.5f, 0f, -84.0f), 8.5f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ZiratMemoryBirchEast", new(17.0f, 0f, -88.0f), 7.8f, VegetationStyle.Birch, "596047");
        AddVisualLandformSegment(parent, "ZiratWestFieldReliefNear", new(-16.0f, 0f, -58.0f), new(-22.5f, 0f, -64.5f), 2.5f, 0.30f, "4a5749", "earth", 0.02f);
        AddVisualLandformSegment(parent, "ZiratWestFieldReliefFar", new(-34.0f, 0f, -76.5f), new(-27.0f, 0f, -84.5f), 2.2f, 0.25f, "53604e", "earth", 0.02f);
        AddVisualLandformSegment(parent, "ZiratEastFieldReliefNear", new(18.5f, 0f, -64.0f), new(25.5f, 0f, -69.5f), 2.4f, 0.28f, "48564a", "earth", 0.02f);
        AddVisualLandformSegment(parent, "ZiratEastFieldReliefFar", new(24.0f, 0f, -79.0f), new(31.5f, 0f, -87.0f), 2.0f, 0.22f, "4e5a4c", "earth", 0.02f);
        // The cemetery stays open and quiet, but its lateral turns still need
        // a distant village/woodland edge. Keep this line low and sparse so it
        // reads as a field boundary rather than a generic horror hedge.
        AddVisualLandformSegment(parent, "ZiratWestHorizonBank", new(-43f, 0f, -53f), new(-43f, 0f, -91f), 3.2f, 0.78f, "4b584b", "earth", 0.02f);
        AddVisualLandformSegment(parent, "ZiratEastHorizonBank", new(43f, 0f, -54f), new(43f, 0f, -92f), 3.0f, 0.72f, "465448", "earth", 0.02f);
        AddVisualTree(parent, "ZiratWestHorizonBirch", new(-42f, 0f, -59f), 8.2f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ZiratWestHorizonBroadleaf", new(-44f, 0f, -78f), 6.9f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "ZiratEastHorizonConifer", new(43f, 0f, -64f), 8.8f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ZiratEastHorizonBirch", new(42f, 0f, -86f), 7.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ZiratFarEdgeBroadleaf", new(45f, 0f, -60f), 7.2f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "ZiratFarEdgeBirch", new(45f, 0f, -78f), 8.0f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ZiratFarEdgeConifer", new(45f, 0f, -92f), 9.1f, VegetationStyle.Conifer, "30483f");
        AddVisualShrub(parent, "ZiratWestHorizonShrub", new(-40f, 0f, -88f), 0.95f, "53634e");
        AddVisualShrub(parent, "ZiratEastHorizonShrub", new(40f, 0f, -73f), 0.90f, "48553f");
        AddDistantHouse(parent, new(-28f, 0f, -64f), 90f, "ZiratWestLateralHouse");
        AddDistantHouse(parent, new(28f, 0f, -68f), -90f, "ZiratEastLateralHouse");
        AddVisualFenceRun(parent, "ZiratWestLateralFence", new(-31f, 0f, -67f), new(-24f, 0f, -67f));
        AddVisualFenceRun(parent, "ZiratEastLateralFence", new(24f, 0f, -71f), new(32f, 0f, -71f));
        AddAuthoredHouse(parent, "ZiratEastNearMidHouse", new(23f, 1.0f, -59f), 0.36f, -90f);
        AddVisualShed(parent, "ZiratEastNearMidShed", new(19f, 0f, -76f), 0.56f, -90f, "5d635c", "3d403b");
        AddVisualFenceRun(parent, "ZiratEastNearMidFence", new(16f, 0f, -58f), new(27f, 0f, -62f));
        AddVisualLandformSegment(parent, "ZiratEastNearMidBank", new(16f, 0f, -57f), new(29f, 0f, -62f), 1.20f, 0.16f, "4b584b", "earth", 0.02f);
        AddVisualTree(parent, "ZiratEastNearMidBroadleaf", new(28f, 0f, -78f), 7.1f, VegetationStyle.Broadleaf, "48553f");
        AddVisualLandformSegment(parent, "ZiratEastOuterFieldBank", new(30f, 0f, -67f), new(35f, 0f, -71f), 1.45f, 0.16f, "4b584b", "earth", 0.02f);
        AddVisualFenceRun(parent, "ZiratEastOuterFieldFence", new(31f, 0f, -76f), new(36f, 0f, -84f));
        AddVisualTree(parent, "ZiratEastOuterFieldBirch", new(31f, 0f, -70f), 7.3f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ZiratEastOuterFieldConifer", new(35f, 0f, -85f), 8.2f, VegetationStyle.Conifer, "30483f");
        AddCoreFacetedMass(parent, "ZiratForestTransitionWest", origin + new Vector3(-7.5f, 1.8f, -24.0f), new(4.6f, 1.8f, 2.0f), "3f493f");
        AddCoreFacetedMass(parent, "ZiratForestTransitionEast", origin + new Vector3(8.0f, 1.6f, -27.0f), new(5.0f, 1.6f, 2.2f), "3b5042");
        AddCoreBuilding(parent, "ZiratVillageMemoryHouseWest", new(-17.0f, 0f, -45.5f), new(7.4f, 2.6f, 5.0f), 2.6f, 90f, "686a60", "464039", "97876a", false, false);
        AddCoreBuilding(parent, "ZiratVillageMemoryHouseEast", new(17.5f, 0f, -47.5f), new(8.0f, 2.7f, 5.2f), 2.7f, -90f, "60665d", "3f423d", "8f826b", true, false);
        AddCoreFacetedMass(parent, "ZiratForestMemoryMass", origin + new Vector3(0f, 2.0f, -36.0f), new(11.0f, 1.7f, 2.2f), "43524b");

        // A small authored grouping gives the zirat a visible village boundary
        // instead of a flat field, while keeping the quiet central path clear.
        var ziratClosure = new Node3D { Name = "ZiratBoundaryClosureGrouping", Position = origin };
        ziratClosure.SetMeta("presentationOnly", true);
        ziratClosure.SetMeta("visualOnly", true);
        ziratClosure.SetMeta("culturalPlaceholder", "quiet boundary berms, low markers and vegetation; no crosses or inscriptions");
        ziratClosure.SetMeta("compositionRole", "authored zirat edge between village parcels and forest transition");
        parent.AddChild(ziratClosure);
        AddVisualLandformSegment(ziratClosure, "ZiratClosureBermWest", new(-5.2f, 0f, 11.5f), new(-6.4f, 0f, -15.0f), 1.45f, 0.24f, "4e5a4c", "earth", 0.02f);
        AddVisualLandformSegment(ziratClosure, "ZiratClosureBermEast", new(5.3f, 0f, 11.0f), new(6.8f, 0f, -16.0f), 1.35f, 0.22f, "48564a", "earth", 0.02f);
        AddVisualLandformSegment(ziratClosure, "ZiratClosureForestBermWest", new(-9.0f, 0f, -17.0f), new(-15.0f, 0f, -28.0f), 2.05f, 0.34f, "425044", "earth", 0.02f);
        AddVisualLandformSegment(ziratClosure, "ZiratClosureForestBermEast", new(9.0f, 0f, -18.0f), new(15.5f, 0f, -29.5f), 1.90f, 0.30f, "3f4b40", "earth", 0.02f);
        AddVisualFenceRun(ziratClosure, "ZiratClosureVillageFenceWest", new(-11.5f, 0f, 15.0f), new(-16.8f, 0f, 24.5f));
        AddVisualFenceRun(ziratClosure, "ZiratClosureVillageFenceEast", new(11.5f, 0f, 14.5f), new(17.0f, 0f, 23.5f));
        AddVisualFenceRun(ziratClosure, "ZiratClosureForestFenceWest", new(-11.5f, 0f, -15.0f), new(-16.0f, 0f, -23.5f));
        AddVisualFenceRun(ziratClosure, "ZiratClosureForestFenceEast", new(11.5f, 0f, -15.5f), new(16.4f, 0f, -24.5f));
        AddGraveMarker(ziratClosure, new(-4.4f, 0f, 8.4f), new(0.64f, 0.86f, 0.30f), -7f);
        AddGraveMarker(ziratClosure, new(4.7f, 0f, 6.8f), new(0.56f, 0.76f, 0.28f), 11f);
        AddGraveMarker(ziratClosure, new(-5.2f, 0f, -7.4f), new(0.52f, 0.70f, 0.26f), 9f);
        AddGraveMarker(ziratClosure, new(5.5f, 0f, -13.6f), new(0.60f, 0.80f, 0.28f), -13f);
        AddVisualStoneCluster(ziratClosure, "ZiratClosureStoneWest", new(-7.4f, 0f, 4.8f), 0.64f, "777669");
        AddVisualStoneCluster(ziratClosure, "ZiratClosureStoneEast", new(7.8f, 0f, -10.2f), 0.56f, "6d6c62");
        AddVisualShrub(ziratClosure, "ZiratClosureShrubVillageWest", new(-7.2f, 0f, 12.4f), 0.76f, "596047");
        AddVisualShrub(ziratClosure, "ZiratClosureShrubVillageEast", new(7.4f, 0f, 10.2f), 0.68f, "48553f");
        AddVisualShrub(ziratClosure, "ZiratClosureShrubForestWest", new(-9.2f, 0f, -17.0f), 0.82f, "53634e");
        AddVisualShrub(ziratClosure, "ZiratClosureShrubForestEast", new(9.4f, 0f, -19.0f), 0.74f, "48553f");
        AddVisualTree(ziratClosure, "ZiratClosureVillageBirchWest", new(-18.0f, 0f, 17.0f), 7.4f, VegetationStyle.Birch, "596047");
        AddVisualTree(ziratClosure, "ZiratClosureVillageBroadleafEast", new(18.0f, 0f, 15.5f), 6.8f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(ziratClosure, "ZiratClosureMidConiferWest", new(-17.0f, 0f, -1.0f), 8.8f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(ziratClosure, "ZiratClosureMidBirchEast", new(17.5f, 0f, -5.0f), 8.2f, VegetationStyle.Birch, "596047");
        AddVisualTree(ziratClosure, "ZiratClosureForestBroadleafWest", new(-17.0f, 0f, -20.0f), 8.6f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(ziratClosure, "ZiratClosureForestConiferEast", new(18.5f, 0f, -24.0f), 9.1f, VegetationStyle.Conifer, "30483f");

        // Composition pass: keep the road window open while giving the field
        // a lateral threshold, broken depth rhythm and a darker far edge.
        // Every cue stays outside x +/- 2.5 m, including the zirat transition.
        AddVisualGate(parent, "ZiratLateralOpenThresholdGate", new(-7.2f, 0f, -60.0f), 2.4f, 1.18f, 90f);
        AddVisualLandformSegment(parent, "ZiratNearWestGraveMound", new(-8.0f, 0f, -62.5f), new(-8.8f, 0f, -66.0f), 1.55f, 0.16f, "4a5648", "earth", 0.02f);
        AddVisualStoneCluster(parent, "ZiratNearEastFieldStone", new(8.4f, 0f, -64.8f), 0.72f, "66665d");
        AddVisualFenceRun(parent, "ZiratMidWestAgedBoundary", new(-8.6f, 0f, -68.2f), new(-10.1f, 0f, -73.6f));
        AddVisualLandformSegment(parent, "ZiratMidEastGraveMound", new(8.4f, 0f, -70.4f), new(9.5f, 0f, -75.5f), 1.50f, 0.15f, "465247", "earth", 0.02f);
        AddVisualShrub(parent, "ZiratMidWestAgedShrub", new(-8.7f, 0f, -76.8f), 0.88f, "435344");
        AddVisualStoneCluster(parent, "ZiratMidEastFieldStone", new(9.2f, 0f, -78.8f), 0.68f, "5f6358");
        AddVisualLandformSegment(parent, "ZiratFarWestFieldMound", new(-8.8f, 0f, -83.0f), new(-10.0f, 0f, -89.0f), 1.70f, 0.18f, "3f4e41", "earth", 0.02f);
        AddVisualTree(parent, "ZiratFarBackstopConiferWest", new(-8.5f, 0f, -92.0f), 10.4f, VegetationStyle.Conifer, "263c32");
        AddVisualTree(parent, "ZiratFarBackstopConiferEast", new(8.8f, 0f, -95.0f), 10.0f, VegetationStyle.Conifer, "243b31");
        AddVisualTree(parent, "ZiratFarBackstopBirchEast", new(13.8f, 0f, -91.0f), 9.2f, VegetationStyle.Birch, "354a3c");
    }

    private static void BuildCoreKaraForestEdge(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("kara_urman_night", out var placement))
        {
            return;
        }

        var origin = placement.Origin;
        AddCoreRouteEnvelope(parent, "KaraApproachRoadEnvelope", new(0f, 0.02f, -103.0f), new(0f, 0.02f, -122.5f), 3.5f, "526052", "405047", 3.0f);
        AddVisualLandformSegment(parent, "KaraAsymmetricWestBank", new(-3.6f, 0f, -103.0f), new(-8.6f, 0f, -121.5f), 2.2f, 0.34f, "3b493e", "earth", 0.02f);
        AddVisualLandformSegment(parent, "KaraAsymmetricEastBank", new(3.8f, 0f, -105.0f), new(6.4f, 0f, -117.0f), 1.5f, 0.22f, "3f4c40", "earth", 0.02f);
        AddCoreRootCluster(parent, "KaraRootBankWest", new(-5.7f, 0f, -110.5f), 1.0f, -16f);
        AddCoreRootCluster(parent, "KaraRootBankEast", new(5.2f, 0f, -114.5f), 0.86f, 20f);
        AddCoreRootCluster(parent, "KaraThresholdRootWest", new(-8.0f, 0f, -120.5f), 1.18f, 34f);
        AddCoreFacetedMass(parent, "KaraLateralForestShelfWest", origin + new Vector3(-10.0f, 2.8f, -13.5f), new(5.2f, 2.8f, 2.1f), "2f4439");
        AddCoreFacetedMass(parent, "KaraLateralForestShelfEast", origin + new Vector3(10.5f, 2.4f, -18.5f), new(5.0f, 2.4f, 2.3f), "345044");
        AddCoreFacetedMass(parent, "KaraDistantEdgeWindowWest", origin + new Vector3(-9.0f, 2.2f, -27.0f), new(5.8f, 2.2f, 2.0f), "2e4439");
        AddCoreFacetedMass(parent, "KaraDistantEdgeWindowEast", origin + new Vector3(9.5f, 2.0f, -29.0f), new(5.4f, 2.0f, 2.1f), "385044");
        AddVisualFenceRun(parent, "KaraSideLandmarkFence", new(8.0f, 0f, -106.0f), new(10.5f, 0f, -114.5f));
        AddVisualBox(parent, "KaraSideLandmarkPost", new(0.18f, 2.0f, 0.18f), new(10.3f, 1.0f, -114.3f), "574d3d", "wood", rollDegrees: -3f);
        AddVisualBox(parent, "KaraSideLandmarkHeader", new(2.8f, 0.14f, 0.16f), new(9.3f, 1.92f, -113.0f), "574d3d", "wood", yawDegrees: -16f, rollDegrees: 7f);
        AddVisualTree(parent, "KaraMixedMassWestNear", origin + new Vector3(-9.5f, 0f, 6.5f), 8.1f, VegetationStyle.Broadleaf, "405445");
        AddVisualTree(parent, "KaraMixedMassEastNear", origin + new Vector3(9.0f, 0f, 3.8f), 7.3f, VegetationStyle.Birch, "526052");
        AddVisualTree(parent, "KaraMixedMassWestMid", origin + new Vector3(-11.5f, 0f, -8.0f), 9.6f, VegetationStyle.Conifer, "2e4439");
        AddVisualTree(parent, "KaraMixedMassEastMid", origin + new Vector3(12.0f, 0f, -12.0f), 8.9f, VegetationStyle.Broadleaf, "3b5043");
        var karaFarForestMassWest = AddCoreFacetedMass(parent, "KaraFarForestMassWest", origin + new Vector3(-12.0f, 4.6f, -28.5f), new(8.0f, 4.6f, 2.8f), "2f4439");
        var karaFarForestMassEast = AddCoreFacetedMass(parent, "KaraFarForestMassEast", origin + new Vector3(11.0f, 4.0f, -30.0f), new(7.0f, 4.0f, 2.6f), "3a5042");
        var karaFarForestWindowBase = AddCoreFacetedMass(parent, "KaraFarForestWindowBase", origin + new Vector3(0f, 1.4f, -32.5f), new(15.0f, 1.5f, 2.4f), "34483e");
        // These three new far masses overlap into one eye-level wall from the
        // Kara standard position. Existing side trees and the route envelope
        // preserve the threshold; suppress only the central/overlapping core
        // masses rather than hiding the Kara zone or touching its legacy kit.
        foreach (var mass in new[] { karaFarForestMassWest, karaFarForestMassEast, karaFarForestWindowBase })
        {
            mass.Visible = false;
            mass.SetMeta("suppressedForConnectedRouteOverlap", true);
        }
        AddCoreFacetedMass(parent, "KaraVillageMemoryMassWest", origin + new Vector3(-13.0f, 2.6f, 9.0f), new(7.0f, 2.5f, 2.3f), "4d5b53");
        AddCoreFacetedMass(parent, "KaraVillageMemoryMassEast", origin + new Vector3(13.5f, 2.4f, 8.0f), new(7.5f, 2.3f, 2.4f), "536059");
        AddVisualLandformSegment(parent, "KaraDistantForestEdge", origin + new Vector3(-16.0f, 0f, -36.0f), origin + new Vector3(16.0f, 0f, -36.0f), 1.20f, 0.70f, "2f4439", "earth", 0.02f);
        // Broken side shelves give the forest edge a mid/far depth cue while
        // leaving the approach center open. Each silhouette is paired with a
        // low landform so foliage supports authored ground geometry instead
        // of becoming a repeated horizon wall.
        AddVisualLandformSegment(parent, "KaraDistantWestBrokenShelf", origin + new Vector3(-18.0f, 0f, -19.0f), origin + new Vector3(-23.0f, 0f, -28.0f), 1.80f, 0.30f, "30463b", "earth", 0.02f);
        AddVisualLandformSegment(parent, "KaraDistantEastBrokenShelf", origin + new Vector3(18.0f, 0f, -21.0f), origin + new Vector3(24.0f, 0f, -30.0f), 1.65f, 0.26f, "35493f", "earth", 0.02f);
        AddVisualTree(parent, "KaraDistantWestBirch", origin + new Vector3(-19.0f, 0f, -22.0f), 9.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "KaraDistantEastConifer", origin + new Vector3(21.0f, 0f, -26.0f), 10.6f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "KaraFarWestBroadleaf", origin + new Vector3(-25.5f, 0f, -32.0f), 8.8f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "KaraFarEastBirch", origin + new Vector3(27.0f, 0f, -35.0f), 9.2f, VegetationStyle.Birch, "596047");

        // Deepen the final forest edge with staggered side banks and mixed
        // silhouettes. All new forms stay outside the 3.5 m road envelope;
        // the center and the approach waypoint remain an open visual window.
        var karaClosure = new Node3D { Name = "KaraDeepForestClosureGrouping", Position = origin };
        karaClosure.SetMeta("presentationOnly", true);
        karaClosure.SetMeta("visualOnly", true);
        karaClosure.SetMeta("compositionRole", "dense near/mid/far Kara forest edge with open central approach");
        parent.AddChild(karaClosure);
        AddVisualLandformSegment(karaClosure, "KaraClosureNearBankWest", new(-6.8f, 0f, 10.5f), new(-11.6f, 0f, -7.0f), 2.20f, 0.40f, "3d4b40", "earth", 0.02f);
        AddVisualLandformSegment(karaClosure, "KaraClosureNearBankEast", new(6.9f, 0f, 9.5f), new(11.7f, 0f, -7.5f), 2.00f, 0.35f, "3b4a3f", "earth", 0.02f);
        AddVisualLandformSegment(karaClosure, "KaraClosureDeepShelfWest", new(-14.0f, 0f, -8.5f), new(-22.0f, 0f, -16.5f), 2.35f, 0.46f, "30463b", "earth", 0.02f);
        AddVisualLandformSegment(karaClosure, "KaraClosureDeepShelfEast", new(14.2f, 0f, -9.5f), new(22.5f, 0f, -18.5f), 2.20f, 0.42f, "35493f", "earth", 0.02f);
        // Threshold aperture pass: six existing side masses are kept as dark
        // framing but moved outward/reduced so the route and distant forest
        // remain legible from the fixed first-person camera.
        AddVisualTree(karaClosure, "KaraClosureNearConiferWest", new(-8.4f, 0f, 9.5f), 8.0f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(karaClosure, "KaraClosureNearBirchEast", new(11.0f, 0f, 7.0f), 5.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(karaClosure, "KaraClosureMidBroadleafWest", new(-13.0f, 0f, 2.0f), 8.0f, VegetationStyle.Broadleaf, "405445");
        AddVisualTree(karaClosure, "KaraClosureMidConiferEast", new(12.4f, 0f, -2.5f), 8.4f, VegetationStyle.Conifer, "2e4439");
        AddVisualTree(karaClosure, "KaraClosureDeepBirchWest", new(-14.5f, 0f, -7.5f), 11.1f, VegetationStyle.Birch, "596047");
        AddVisualTree(karaClosure, "KaraClosureDeepBroadleafEast", new(15.0f, 0f, -10.5f), 10.7f, VegetationStyle.Broadleaf, "3b5043");
        AddVisualTree(karaClosure, "KaraClosureFarConiferWest", new(-20.0f, 0f, -15.5f), 10.3f, VegetationStyle.Conifer, "2e4439");
        AddVisualTree(karaClosure, "KaraClosureFarBirchEast", new(21.0f, 0f, -18.5f), 9.9f, VegetationStyle.Birch, "596047");
        AddVisualShrub(karaClosure, "KaraClosureShrubWest", new(-6.8f, 0f, 6.0f), 0.88f, "3f4d40");
        AddVisualShrub(karaClosure, "KaraClosureShrubEast", new(7.0f, 0f, 4.8f), 0.80f, "3b4b3e");

        // Final threshold pass: the side masses compress in depth, then open
        // again into an uneven dark forest window. Every placement stays
        // outside the ±3.5 m route envelope; only the visual frame is new.
        AddVisualLandformSegment(parent, "KaraThresholdBankWest", origin + new Vector3(-6.4f, 0f, 6.5f), origin + new Vector3(-8.1f, 0f, -22.0f), 1.85f, 0.34f, "34443b", "earth", 0.02f);
        AddVisualLandformSegment(parent, "KaraThresholdBankEast", origin + new Vector3(6.8f, 0f, 4.5f), origin + new Vector3(9.0f, 0f, -24.0f), 1.70f, 0.30f, "3b493e", "earth", 0.02f);
        AddCoreRootCluster(parent, "KaraApproachRootFramingWest", origin + new Vector3(-7.0f, 0f, 7.0f), 0.92f, -24f);
        AddCoreRootCluster(parent, "KaraApproachRootFramingEast", origin + new Vector3(7.5f, 0f, 5.5f), 0.84f, 18f);
        AddVisualShrub(parent, "KaraApproachUnderstoryWest", origin + new Vector3(-5.8f, 0f, 6.0f), 1.10f, "34443b");
        AddVisualShrub(parent, "KaraApproachUnderstoryEast", origin + new Vector3(6.4f, 0f, 4.2f), 0.92f, "3b4b3e");
        AddVisualStoneCluster(parent, "KaraApproachStoneWest", origin + new Vector3(-6.3f, 0f, -1.5f), 0.70f, "4b5149");

        AddVisualTree(parent, "KaraThresholdConiferWest", origin + new Vector3(-10.0f, 0f, -7.0f), 8.8f, VegetationStyle.Conifer, "263c32");
        AddVisualTree(parent, "KaraThresholdBroadleafEast", origin + new Vector3(8.2f, 0f, -7.5f), 9.0f, VegetationStyle.Broadleaf, "2e4439");
        AddCoreFacetedMass(parent, "KaraThresholdDarkMassWest", origin + new Vector3(-10.4f, 2.6f, -10.5f), new(3.7f, 2.6f, 2.2f), "263c32");
        AddCoreFacetedMass(parent, "KaraThresholdDarkMassEast", origin + new Vector3(11.3f, 2.4f, -14.5f), new(4.2f, 2.4f, 2.3f), "2c403b");
        AddVisualTree(parent, "KaraThresholdBirchWest", origin + new Vector3(-8.8f, 0f, -13.5f), 10.0f, VegetationStyle.Birch, "405445");
        AddVisualTree(parent, "KaraThresholdConiferEast", origin + new Vector3(9.4f, 0f, -17.0f), 10.8f, VegetationStyle.Conifer, "243b31");

        AddVisualLandformSegment(parent, "KaraDeepForestShelfWest", origin + new Vector3(-8.8f, 0f, -16.0f), origin + new Vector3(-11.0f, 0f, -29.0f), 2.10f, 0.44f, "2f4037", "earth", 0.02f);
        AddVisualLandformSegment(parent, "KaraDeepForestShelfEast", origin + new Vector3(10.4f, 0f, -18.0f), origin + new Vector3(12.6f, 0f, -31.0f), 2.00f, 0.40f, "30463b", "earth", 0.02f);
        AddCoreFacetedMass(parent, "KaraFarDarkForestMassWest", origin + new Vector3(-11.2f, 3.8f, -24.5f), new(5.0f, 3.8f, 2.8f), "243a34");
        AddCoreFacetedMass(parent, "KaraFarDarkForestMassEast", origin + new Vector3(12.2f, 4.1f, -27.5f), new(4.6f, 4.1f, 3.0f), "2a3e37");
        AddVisualTree(parent, "KaraFarForestConiferWest", origin + new Vector3(-12.6f, 0f, -28.5f), 11.6f, VegetationStyle.Conifer, "243b31");
        AddVisualTree(parent, "KaraFarForestBirchEast", origin + new Vector3(13.8f, 0f, -31.5f), 10.4f, VegetationStyle.Birch, "34483f");

        // Two lower return-facing silhouettes keep the turn back toward the
        // zirat/village legible instead of ending the map at a dark wall.
        AddVisualTree(parent, "KaraReturnContinuityBirchWest", origin + new Vector3(-8.6f, 0f, 14.0f), 6.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "KaraReturnContinuityBroadleafEast", origin + new Vector3(9.4f, 0f, 12.5f), 6.2f, VegetationStyle.Broadleaf, "53634e");
        AddVisualShrub(parent, "KaraReturnContinuityUnderstoryWest", origin + new Vector3(-6.0f, 0f, 12.8f), 0.86f, "4a5745");
        AddVisualShrub(parent, "KaraReturnContinuityUnderstoryEast", origin + new Vector3(6.6f, 0f, 11.7f), 0.78f, "48553f");
    }

    private static void AddCoreRouteEnvelope(
        Node3D parent,
        string name,
        Vector3 start,
        Vector3 end,
        float routeWidth,
        string shoulderColor,
        string wetColor,
        float endRouteWidth = -1f)
    {
        var direction = HorizontalDirection(end - start);
        var perpendicular = new Vector3(-direction.Z, 0f, direction.X);
        var startEdge = routeWidth * 0.5f;
        var endEdge = (endRouteWidth > 0f ? endRouteWidth : routeWidth) * 0.5f;
        AddVisualLandformSegment(parent, $"{name}LeftShoulder", start + perpendicular * (startEdge + 0.58f), end + perpendicular * (endEdge + 0.58f), 1.18f, 0.18f, shoulderColor, "earth", 0.02f);
        AddVisualLandformSegment(parent, $"{name}RightShoulder", start - perpendicular * (startEdge + 0.58f), end - perpendicular * (endEdge + 0.58f), 1.18f, 0.16f, shoulderColor, "earth", 0.02f);
        AddVisualLandformSegment(parent, $"{name}LeftWetDitch", start + perpendicular * (startEdge + 1.65f), end + perpendicular * (endEdge + 1.65f), 0.72f, 0.06f, wetColor, "earth", 0.02f);
        AddVisualLandformSegment(parent, $"{name}RightWetDitch", start - perpendicular * (startEdge + 1.65f), end - perpendicular * (endEdge + 1.65f), 0.72f, 0.05f, wetColor, "earth", 0.02f);
        AddVisualLandformSegment(parent, $"{name}LeftOuterBank", start + perpendicular * (startEdge + 2.65f), end + perpendicular * (endEdge + 2.65f), 1.45f, 0.22f, "55614e", "earth", 0.02f);
        AddVisualLandformSegment(parent, $"{name}RightOuterBank", start - perpendicular * (startEdge + 2.65f), end - perpendicular * (endEdge + 2.65f), 1.35f, 0.18f, "4d5a4b", "earth", 0.02f);
    }

    private static Node3D AddCoreBuilding(
        Node3D parent,
        string name,
        Vector3 anchor,
        Vector3 footprint,
        float wallHeight,
        float yawDegrees,
        string wallColor,
        string roofColor,
        string trimColor,
        bool leanToRoof,
        bool porch)
    {
        var building = new Node3D
        {
            Name = name,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        building.SetMeta("presentationOnly", true);
        building.SetMeta("visualOnly", true);
        parent.AddChild(building);

        AddVisualBox(building, "CoreFoundation", new(footprint.X + 0.32f, 0.22f, footprint.Z + 0.32f), new(0f, 0.11f, 0f), "454740", "stone");
        AddVisualBox(building, "CoreWallVolume", new(footprint.X, wallHeight, footprint.Z), new(0f, wallHeight * 0.5f + 0.18f, 0f), wallColor, "plaster");
        if (leanToRoof)
        {
            AddVisualBox(building, "CoreLeanToRoof", new(footprint.X * 0.88f, 0.32f, footprint.Z + 0.42f), new(0.18f, wallHeight + 0.52f, 0f), roofColor, "wood", rollDegrees: 9f);
            AddVisualBox(building, "CoreLeanToRidge", new(0.16f, 0.18f, footprint.Z + 0.5f), new(-footprint.X * 0.28f, wallHeight + 0.86f, 0f), roofColor, "wood");
        }
        else
        {
            AddVisualPitchedRoof(building, "CoreGabledRoof", footprint.X, footprint.Z, wallHeight, 0.92f, 0.24f, roofColor);
        }

        var frontZ = footprint.Z * 0.5f + 0.08f;
        var openingX = -footprint.X * 0.25f;
        AddVisualBox(building, "CoreFrontPlasterPanelWest", new(footprint.X * 0.34f, 0.72f, 0.08f), new(-footprint.X * 0.20f, 0.84f, frontZ + 0.02f), "5b625b", "plaster", rollDegrees: -2f);
        AddVisualBox(building, "CoreFrontPlasterPanelEast", new(footprint.X * 0.38f, 0.84f, 0.08f), new(footprint.X * 0.20f, 0.97f, frontZ + 0.02f), "6a6658", "plaster", rollDegrees: 1.5f);
        AddVisualBox(building, "CoreFrontDiagonalBrace", new(0.14f, wallHeight * 0.54f, 0.10f), new(-footprint.X * 0.05f, wallHeight * 0.55f, frontZ + 0.08f), trimColor, "wood", rollDegrees: -17f);
        AddVisualBox(building, "CoreFrontOpening", new(1.05f, Mathf.Min(2.05f, wallHeight * 0.68f), 0.08f), new(openingX, 1.05f, frontZ + 0.05f), "4b382c", "wood");
        var frontWindow = AddVisualBox(building, "CoreFrontWindow", new(1.25f, 0.82f, 0.06f), new(footprint.X * 0.18f, wallHeight * 0.56f, frontZ + 0.08f), "8a7256", "glass");
        if (string.Equals(name, "BabaiEbiHouseFullVolume", StringComparison.Ordinal))
        {
            frontWindow.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml("b6814f"),
                EmissionEnabled = true,
                Emission = Color.FromHtml("9b5a32"),
                EmissionEnergyMultiplier = 0.72f,
                Roughness = 0.48f
            };
            frontWindow.SetMeta("presentationOnly", true);
            frontWindow.SetMeta("lightingRole", "hero dwelling warm window");
        }
        AddVisualBox(building, "CoreWindowLintel", new(1.45f, 0.10f, 0.10f), new(footprint.X * 0.18f, wallHeight * 0.56f + 0.47f, frontZ + 0.11f), trimColor, "wood");
        AddVisualBox(building, "CoreWindowSill", new(1.45f, 0.10f, 0.10f), new(footprint.X * 0.18f, wallHeight * 0.56f - 0.47f, frontZ + 0.11f), trimColor, "wood");
        var bandY = Mathf.Clamp(wallHeight * 0.42f, 0.86f, 1.18f);
        AddVisualBox(building, "CoreFrontTimberBand", new(footprint.X * 0.88f, 0.12f, 0.10f), new(0f, bandY, frontZ + 0.07f), trimColor, "wood");
        AddVisualBox(building, "CoreFrontEaveBand", new(footprint.X * 0.92f, 0.12f, 0.10f), new(0f, wallHeight + 0.08f, frontZ + 0.04f), trimColor, "wood");
        foreach (var x in new[] { -footprint.X * 0.43f, footprint.X * 0.43f })
        {
            AddVisualBox(building, $"CoreFrontCornerPost{x:0.00}", new(0.12f, wallHeight * 0.78f, 0.12f), new(x, wallHeight * 0.44f, frontZ + 0.08f), trimColor, "wood");
        }
        AddVisualBox(building, "CoreLowerWeatherBreak", new(footprint.X * 0.84f, 0.18f, 0.10f), new(0f, 0.42f, frontZ + 0.07f), "4f514a", "wood");
        foreach (var x in new[] { -footprint.X * 0.5f - 0.07f, footprint.X * 0.5f + 0.07f })
        {
            AddVisualBox(building, $"CoreSideTimberBand{x:0.00}", new(0.10f, 0.10f, footprint.Z * 0.84f), new(x, bandY, 0f), trimColor, "wood");
            AddVisualBox(building, $"CoreSideCornerPost{x:0.00}", new(0.12f, wallHeight * 0.78f, 0.12f), new(x, wallHeight * 0.44f, 0f), trimColor, "wood");
        }
        var sideWindowY = Mathf.Clamp(wallHeight * 0.56f, 1.12f, 1.72f);
        foreach (var (x, suffix) in new[]
                 {
                     (-footprint.X * 0.5f - 0.10f, "West"),
                     (footprint.X * 0.5f + 0.10f, "East")
                 })
        {
            AddVisualBox(building, $"CoreSideWindow{suffix}", new(0.06f, 0.74f, 0.98f), new(x, sideWindowY, footprint.Z * 0.04f), "65756e", "glass");
            AddVisualBox(building, $"CoreSideWindow{suffix}Top", new(0.10f, 0.10f, 1.14f), new(x, sideWindowY + 0.44f, footprint.Z * 0.04f), trimColor, "wood");
            AddVisualBox(building, $"CoreSideWindow{suffix}Bottom", new(0.10f, 0.10f, 1.14f), new(x, sideWindowY - 0.44f, footprint.Z * 0.04f), trimColor, "wood");
        }
        if (porch)
        {
            AddVisualBox(building, "CoreThresholdDeck", new(2.7f, 0.16f, 1.10f), new(openingX, 0.24f, frontZ + 0.44f), "624a37", "wood");
            AddVisualBox(building, "CoreThresholdCanopy", new(3.0f, 0.14f, 0.94f), new(openingX, 2.28f, frontZ + 0.50f), roofColor, "wood", rollDegrees: 6f);
            AddVisualBox(building, "CoreThresholdPostLeft", new(0.12f, 1.85f, 0.12f), new(openingX - 1.04f, 1.12f, frontZ + 0.78f), trimColor, "wood");
            AddVisualBox(building, "CoreThresholdPostRight", new(0.12f, 1.85f, 0.12f), new(openingX + 1.04f, 1.12f, frontZ + 0.78f), trimColor, "wood");
        }

        // The reverse turn sees the same parcels from the back. Keep the
        // detail sparse, but give that side a timber edge instead of a blank
        // wall so the authored village survives a 180-degree inspection.
        var rearZ = -footprint.Z * 0.5f - 0.06f;
        AddVisualBox(building, "CoreRearTimberBand", new(footprint.X * 0.86f, 0.10f, 0.10f), new(0f, bandY, rearZ), trimColor, "wood");
        AddVisualBox(building, "CoreRearEaveBand", new(footprint.X * 0.90f, 0.10f, 0.10f), new(0f, wallHeight + 0.08f, rearZ), trimColor, "wood");
        foreach (var (x, suffix) in new[]
                 {
                     (-footprint.X * 0.22f, "West"),
                     (footprint.X * 0.22f, "East")
                 })
        {
            AddVisualBox(building, $"CoreRearWindow{suffix}", new(0.96f, 0.68f, 0.06f), new(x, sideWindowY, rearZ - 0.04f), "65756e", "glass");
            AddVisualBox(building, $"CoreRearWindow{suffix}Top", new(1.12f, 0.08f, 0.10f), new(x, sideWindowY + 0.41f, rearZ - 0.07f), trimColor, "wood");
            AddVisualBox(building, $"CoreRearWindow{suffix}Bottom", new(1.12f, 0.08f, 0.10f), new(x, sideWindowY - 0.41f, rearZ - 0.07f), trimColor, "wood");
        }
        building.SetMeta("facadeDetail", "weathered timber bands, corner posts and restrained warm window glazing");

        return building;
    }

    private static Node3D AddCoreFacetedMass(Node3D parent, string name, Vector3 center, Vector3 scale, string color)
    {
        var mass = new Node3D { Name = name, Position = center };
        mass.SetMeta("presentationOnly", true);
        mass.SetMeta("visualOnly", true);
        parent.AddChild(mass);
        mass.AddChild(new MeshInstance3D
        {
            Name = "FacetedMass",
            Scale = scale,
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 7, Rings = 3 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "foliage")
        });
        mass.AddChild(new MeshInstance3D
        {
            Name = "FacetedShoulder",
            Position = new Vector3(scale.X * 0.34f, -scale.Y * 0.32f, scale.Z * 0.12f),
            Scale = new Vector3(scale.X * 0.58f, scale.Y * 0.48f, scale.Z * 0.72f),
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 6, Rings = 3 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "foliage")
        });
        return mass;
    }

    private static void AddCoreRootCluster(Node3D parent, string name, Vector3 center, float scale, float yawDegrees)
    {
        var root = new Node3D
        {
            Name = name,
            Position = center,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Scale = Vector3.One * scale
        };
        root.SetMeta("presentationOnly", true);
        root.SetMeta("visualOnly", true);
        parent.AddChild(root);
        AddVisualBox(root, "RootBank", new(2.8f, 0.58f, 1.2f), new(0f, 0.29f, 0f), "3f4b40", "earth", rollDegrees: -7f);
        AddVisualBox(root, "RootRidge", new(1.6f, 0.32f, 0.52f), new(-0.48f, 0.58f, -0.10f), "55483a", "wood_bark", rollDegrees: 18f);
        AddVisualBox(root, "RootHook", new(0.18f, 1.10f, 0.18f), new(0.62f, 0.75f, -0.20f), "4a3d32", "wood_bark", rollDegrees: -28f);
        AddCoreFacetedMass(root, "RootMossMass", new(0.18f, 0.34f, 0.12f), new(0.90f, 0.40f, 0.62f), "405345");
    }

    private static void AddCoreForestBranch(Node3D parent, string name, Vector3 start, Vector3 end, string color)
    {
        var direction = end - start;
        var branch = new MeshInstance3D
        {
            Name = name,
            Position = (start + end) * 0.5f,
            Mesh = new CylinderMesh { TopRadius = 0.055f, BottomRadius = 0.11f, Height = direction.Length(), RadialSegments = 6 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "wood_bark")
        };
        branch.SetMeta("presentationOnly", true);
        branch.SetMeta("visualOnly", true);
        parent.AddChild(branch);
        branch.LookAt(end, Vector3.Up);
        branch.RotateObjectLocal(Vector3.Right, Mathf.Pi * 0.5f);
    }

    private static void BuildVillageLandmarkKit(Node3D parent)
    {
        var packed = ResourceLoader.Load<PackedScene>(VillageLandmarkKitScenePath)
            ?? throw new InvalidOperationException($"Act I village landmark kit is missing: {VillageLandmarkKitScenePath}.");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException($"Act I village landmark kit did not instantiate: {VillageLandmarkKitScenePath}.");
        var kit = importedRoot;

        // Godot keeps the .glb filename as an import wrapper and retains the
        // authored root as its only child. Work with that exact authored root
        // so the contract is independent of the wrapper filename.
        if (!string.Equals(importedRoot.Name.ToString(), VillageLandmarkKitRootName, StringComparison.Ordinal))
        {
            kit = importedRoot.GetNodeOrNull<Node3D>(VillageLandmarkKitRootName)
                ?? throw new InvalidOperationException(
                    $"Act I village landmark kit is missing authored root '{VillageLandmarkKitRootName}'.");
            importedRoot.RemoveChild(kit);
            importedRoot.Free();
        }

        var collisionNodes = FindDescendants<Node>(kit)
            .Where(node => node is CollisionObject3D or CollisionShape3D)
            .ToArray();
        if (collisionNodes.Length > 0)
        {
            kit.QueueFree();
            throw new InvalidOperationException(
                $"Act I village landmark kit must be presentation-only; found {collisionNodes.Length} collision nodes.");
        }

        kit.SetMeta("assetSource", VillageLandmarkKitScenePath);
        kit.SetMeta("presentationOnly", true);
        kit.SetMeta("collisionPolicy", "no collision nodes; existing route floor/path owners remain authoritative");
        kit.SetMeta("navigationPolicy", "no navigation nodes; existing route navigation remains authoritative");
        kit.SetMeta("interactionPolicy", "no interaction nodes; existing zone targets remain authoritative");
        kit.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        kit.SetMeta("groupPlacementPolicy", "authored linear kit groups re-anchored to deterministic connected-route zones");
        kit.SetMeta("groupNames", string.Join('|', VillageLandmarkGroupNames));
        parent.AddChild(kit);

        // The imported ZiratBoundary is the authored replacement for the old
        // marker/gate enclosure. That enclosure is visual-only; its route
        // floor and interaction owners live elsewhere and remain untouched.
        HidePresentationNodes(parent, "ZiratGreyboxEnclosure");

        foreach (var groupName in VillageLandmarkGroupNames)
        {
            if (kit.GetNodeOrNull<Node3D>(groupName) is not Node3D group)
            {
                kit.QueueFree();
                throw new InvalidOperationException(
                    $"Act I village landmark kit is missing required direct group '{groupName}'.");
            }

            group.SetMeta("presentationOnly", true);
            group.SetMeta("collisionOwner", "none");
            group.SetMeta("navigationOwner", "none");
            group.SetMeta("interactionOwner", "none");
        }

        // The authored source is a compact linear strip whose group roots run
        // from Arrival z=-26 to KaraApproach z=65. The connected runtime is a
        // branched world, so preserve each group's authored local details but
        // deliberately re-anchor the group roots to the existing route owners.
        PlaceVillageLandmarkGroup(kit, "Arrival", new(0f, 0f, 22f), 180f, "village_day@arrival");
        PlaceVillageLandmarkGroup(kit, "VillageStreet", new(0f, 0f, 5f), 180f, "village_day@street");
        PlaceVillageLandmarkGroup(kit, "BabaiYard", new(-20f, 0f, 0f), HouseApproachYaw(), "house_old_pc@yard");
        PlaceVillageLandmarkGroup(kit, "HouseExterior", new(-19f, 0f, 8f), HouseApproachYaw(), "house_old_pc@exterior");
        PlaceVillageLandmarkGroup(kit, "ConnectiveStreet", new(0f, 0f, -20f), 180f, "village_day@connective_street");
        PlaceVillageLandmarkGroup(kit, "FapExterior", new(28f, 0f, -30f), FapApproachYaw(), "fap_clinic@exterior");
        PlaceVillageLandmarkGroup(kit, "ReturnStreet", new(0f, 0f, -46f), 180f, "zirat_road@return_street");
        PlaceVillageLandmarkGroup(kit, "ZiratBoundary", new(7f, 0f, -70f), 180f, "zirat_road@boundary");
        PlaceVillageLandmarkGroup(kit, "KaraApproach", new(7f, 0f, -115f), 180f, "kara_urman_night@approach");
    }

    private static void PlaceVillageLandmarkGroup(
        Node3D kit,
        string groupName,
        Vector3 anchor,
        float yawDegrees,
        string logicalAnchor)
    {
        var group = kit.GetNode<Node3D>(groupName);
        var authoredOrigin = group.Position;
        group.Position = anchor;
        group.RotationDegrees = new Vector3(0f, yawDegrees, 0f);
        group.SetMeta("authoredLocalOrigin", authoredOrigin);
        group.SetMeta("worldAnchor", anchor);
        group.SetMeta("worldYawDegrees", yawDegrees);
        group.SetMeta("logicalAnchor", logicalAnchor);
    }

    private static float HouseApproachYaw()
    {
        if (!Act1WorldLayout.TryGetPlacement("house_old_pc", out var placement))
        {
            throw new InvalidOperationException("Connected Act I layout is missing house_old_pc placement.");
        }

        var approach = GetConnector("arrival-to-house-yard");
        return DirectionYaw(HorizontalDirection(approach.End - placement.Origin));
    }

    private static float FapApproachYaw()
    {
        if (!Act1WorldLayout.TryGetPlacement("fap_clinic", out var placement))
        {
            throw new InvalidOperationException("Connected Act I layout is missing fap_clinic placement.");
        }

        var approach = GetConnector("village-to-fap-branch");
        return DirectionYaw(HorizontalDirection(approach.End - placement.Origin));
    }

    private void BuildAuthoredOutdoorBackbone(Node3D parent)
    {
        var backbone = new Node3D { Name = "Act1AuthoredOutdoorBackbone" };
        backbone.SetMeta("visualOnly", true);
        backbone.SetMeta("collisionOwner", "none");
        backbone.SetMeta("interactionOwner", "none");
        backbone.SetMeta("runtimeStateOwner", "none");
        backbone.SetMeta(
            "compositionPolicy",
            "near parcel relief, mid village facades, far landmark silhouettes and mixed vegetation; route center stays open");
        parent.AddChild(backbone);

        BuildRoadsideRelief(backbone);
        BuildVillageStreetComposition(backbone);
        BuildHouseYardComposition(backbone);
        BuildFapComposition(backbone);
        BuildZiratComposition(backbone);
        BuildKaraUrmanComposition(backbone);
    }

    private static void BuildRoadsideRelief(Node3D parent)
    {
        foreach (var connector in Act1WorldLayout.Connectors)
        {
            var direction = HorizontalDirection(connector.End - connector.Start);
            var perpendicular = new Vector3(-direction.Z, 0f, direction.X);
            var routeEdge = connector.Width * 0.5f;
            var prefix = $"{connector.ConnectorId}-relief";
            var reliefHeight = connector.Width <= 3.0f ? 0.10f : 0.16f;

            AddVisualLandformSegment(
                parent,
                $"{prefix}-shoulder-left",
                connector.Start + perpendicular * (routeEdge + 0.48f),
                connector.End + perpendicular * (routeEdge + 0.48f),
                1.05f,
                reliefHeight,
                "746a52",
                "earth");
            AddVisualLandformSegment(
                parent,
                $"{prefix}-shoulder-right",
                connector.Start - perpendicular * (routeEdge + 0.48f),
                connector.End - perpendicular * (routeEdge + 0.48f),
                1.05f,
                reliefHeight * 0.92f,
                "6a624d",
                "earth");

            AddVisualLandformSegment(
                parent,
                $"{prefix}-ditch-left",
                connector.Start + perpendicular * (routeEdge + 1.24f),
                connector.End + perpendicular * (routeEdge + 1.24f),
                0.64f,
                0.045f,
                "414b40",
                "earth",
                0.025f);
            AddVisualLandformSegment(
                parent,
                $"{prefix}-ditch-right",
                connector.Start - perpendicular * (routeEdge + 1.24f),
                connector.End - perpendicular * (routeEdge + 1.24f),
                0.64f,
                0.045f,
                "414b40",
                "earth",
                0.025f);

            AddVisualLandformSegment(
                parent,
                $"{prefix}-berm-left",
                connector.Start + perpendicular * (routeEdge + 2.18f),
                connector.End + perpendicular * (routeEdge + 2.18f),
                1.35f,
                reliefHeight * 1.55f,
                "5e654d",
                "earth",
                0.02f);
            AddVisualLandformSegment(
                parent,
                $"{prefix}-berm-right",
                connector.Start - perpendicular * (routeEdge + 2.18f),
                connector.End - perpendicular * (routeEdge + 2.18f),
                1.35f,
                reliefHeight * 1.35f,
                "596047",
                "earth",
                0.02f);
        }

        AddVisualParcelPatch(parent, "ArrivalFieldWest", new(-17.5f, 0.08f, 20.5f), new(12.5f, 0.16f, 14.0f), "626954", 4f);
        AddVisualParcelPatch(parent, "ArrivalFieldEast", new(18.5f, 0.07f, 21.5f), new(13.0f, 0.14f, 15.0f), "596047", -6f);
        AddVisualParcelPatch(parent, "VillageFieldWest", new(-19.5f, 0.06f, -10.0f), new(11.5f, 0.12f, 18.0f), "6a6d56", -8f);
        AddVisualParcelPatch(parent, "VillageFieldEast", new(19.0f, 0.06f, -9.0f), new(12.0f, 0.12f, 20.0f), "626954", 7f);
        AddVisualParcelPatch(parent, "ZiratApproachWest", new(-10.0f, 0.08f, -76.0f), new(8.0f, 0.16f, 24.0f), "596047", -3f);
        AddVisualParcelPatch(parent, "ZiratApproachEast", new(10.0f, 0.07f, -78.0f), new(8.5f, 0.14f, 26.0f), "646951", 5f);
    }

    private static void BuildVillageStreetComposition(Node3D parent)
    {
        AddVillageFacade(parent, "StreetFacadeWestArrival", new(-16.5f, 0f, 28.5f), new(7.0f, 3.1f, 4.8f), 3.0f, 174f, "756e5b", "4b4036", "b49c78", false, true);
        AddVillageFacade(parent, "StreetFacadeEastArrival", new(17.4f, 0f, 29.8f), new(8.2f, 2.7f, 5.4f), 2.6f, 186f, "69746b", "55463a", "a58e6e", true, false);
        AddVillageFacade(parent, "StreetFacadeWestMid", new(-20.8f, 0f, 5.0f), new(6.1f, 2.6f, 4.2f), 2.5f, 92f, "7b765f", "5b4c3f", "aa9472", false, true);
        AddVillageFacade(parent, "StreetFacadeEastMid", new(20.2f, 0f, 2.2f), new(5.4f, 3.4f, 4.0f), 3.2f, -88f, "6d766d", "4c443c", "9d9879", false, false);
        AddVillageFacade(parent, "StreetFacadeWestReturn", new(-18.4f, 0f, -21.0f), new(7.5f, 2.4f, 3.8f), 2.3f, 90f, "847861", "514238", "b39b72", true, true);
        AddVillageFacade(parent, "StreetFacadeEastReturn", new(18.7f, 0f, -22.5f), new(6.0f, 2.9f, 5.0f), 2.8f, -90f, "69746b", "5b4b3d", "a9906d", false, false);

        AddVisualCanopy(parent, "StreetWoodCanopyWest", new(-10.2f, 0f, 6.4f), 3.2f, 2.8f, 91f, "6d5c47", "4e4539");
        AddVisualCanopy(parent, "StreetWoodCanopyEast", new(10.4f, 0f, -5.4f), 2.5f, 3.4f, -86f, "7a644b", "504238");
        AddVisualShed(parent, "StreetShedWest", new(-12.2f, 0f, -2.8f), 0.74f, 102f, "806d58", "4d3e34");
        AddVisualShed(parent, "StreetShedEast", new(12.6f, 0f, -17.4f), 0.66f, -78f, "69746b", "3e4440");
        AddVisualWoodpile(parent, "StreetWoodpileWest", new(-8.2f, 0f, 2.5f), 1.1f, 90f);
        AddVisualWoodpile(parent, "StreetWoodpileEast", new(8.8f, 0f, -13.6f), 0.88f, -78f);

        AddVisualFenceRun(parent, "StreetParcelWestNear", new(-7.4f, 0f, 10.8f), new(-8.6f, 0f, 17.4f));
        AddVisualFenceRun(parent, "StreetParcelWestFar", new(-15.2f, 0f, -0.8f), new(-21.4f, 0f, 5.8f));
        AddVisualFenceRun(parent, "StreetParcelEastNear", new(7.8f, 0f, 9.7f), new(9.0f, 0f, 15.7f));
        AddVisualFenceRun(parent, "StreetParcelEastFar", new(14.4f, 0f, -3.4f), new(21.6f, 0f, 1.3f));
        AddVisualFenceRun(parent, "StreetReturnWest", new(-8.0f, 0f, -17.2f), new(-16.6f, 0f, -24.4f));
        AddVisualFenceRun(parent, "StreetReturnEast", new(8.2f, 0f, -18.2f), new(16.0f, 0f, -25.6f));

        AddVisualStreetLandmark(parent, "VillageStreetLandmark", new(-4.6f, 0f, -3.0f), 0f);

        AddVisualTree(parent, "StreetNearBirchWest", new(-6.8f, 0f, 13.4f), 5.2f, VegetationStyle.Birch, "68705a");
        AddVisualTree(parent, "StreetNearBroadleafEast", new(7.0f, 0f, 11.8f), 4.7f, VegetationStyle.Broadleaf, "53634e");
        AddVisualTree(parent, "StreetMidBirchWest", new(-17.6f, 0f, 10.5f), 7.0f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "StreetMidBroadleafEast", new(17.8f, 0f, 12.2f), 6.2f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "StreetFarConiferWest", new(-27.8f, 0f, -1.0f), 8.6f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "StreetFarBirchEast", new(28.4f, 0f, -4.8f), 8.1f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "StreetReturnFarBroadleafWest", new(-28.0f, 0f, -25.0f), 7.2f, VegetationStyle.Broadleaf, "4e5b47");
        AddVisualTree(parent, "StreetReturnFarConiferEast", new(28.6f, 0f, -27.4f), 8.8f, VegetationStyle.Conifer, "30483f");

        AddVisualShrub(parent, "StreetShrubNearWest", new(-6.5f, 0f, 9.5f), 0.72f, "596047");
        AddVisualShrub(parent, "StreetShrubNearEast", new(6.3f, 0f, 8.7f), 0.64f, "48553f");
        AddVisualGrassClump(parent, "StreetGrassNearWest", new(-5.7f, 0f, 6.8f), 0.72f, "68705a");
        AddVisualGrassClump(parent, "StreetGrassNearEast", new(5.8f, 0f, 5.6f), 0.62f, "596047");
        AddVisualStoneCluster(parent, "StreetStoneClusterWest", new(-10.8f, 0f, -8.5f), 0.82f, "777669");
        AddVisualStoneCluster(parent, "StreetStoneClusterEast", new(11.4f, 0f, -11.0f), 0.68f, "6d6c62");

        // The reverse arrival view needs a far village edge as well as the
        // near street. Keep the horizon occupied without pinching the road
        // corridor or introducing another gameplay landmark owner.
        AddVillageFacade(parent, "ArrivalReverseHouseWest", new(-14.8f, 0f, 42.0f), new(6.2f, 2.5f, 4.4f), 2.4f, 176f, "746e5b", "4e4438", "aa9472", false, false);
        AddVillageFacade(parent, "ArrivalReverseHouseEast", new(15.6f, 0f, 43.5f), new(7.0f, 2.9f, 4.8f), 2.8f, 184f, "68736b", "51473c", "9d9879", true, true);
        AddVisualShed(parent, "ArrivalReverseShedWest", new(-7.8f, 0f, 40.2f), 0.64f, 172f, "806d58", "4d3e34");
        AddVisualShed(parent, "ArrivalReverseShedEast", new(8.5f, 0f, 41.4f), 0.58f, 188f, "69746b", "3e4440");
        AddVisualFenceRun(parent, "ArrivalReverseFenceWest", new(-8.2f, 0f, 35.5f), new(-19.0f, 0f, 43.4f));
        AddVisualFenceRun(parent, "ArrivalReverseFenceEast", new(8.4f, 0f, 35.8f), new(20.0f, 0f, 44.5f));
        AddVisualTree(parent, "ArrivalReverseBirchWest", new(-22.0f, 0f, 44.0f), 8.0f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ArrivalReverseBroadleafEast", new(23.0f, 0f, 45.5f), 7.2f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "ArrivalReverseConiferFarWest", new(-30.0f, 0f, 48.0f), 9.2f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ArrivalReverseBirchFarEast", new(30.0f, 0f, 49.0f), 8.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ArrivalReverseNearBirchWest", new(-10.5f, 0f, 27.0f), 8.6f, VegetationStyle.Birch, "68705a");
        AddVisualTree(parent, "ArrivalReverseNearBroadleafEast", new(11.2f, 0f, 29.0f), 8.0f, VegetationStyle.Broadleaf, "53634e");

        // The shared terrain ends just beyond the arrival tail. A sparse,
        // varied tree line closes that real boundary in reverse first-person
        // views without becoming a repeated wall or a traversal owner.
        foreach (var (name, position, height, style, color) in new[]
                 {
                     ("ArrivalBoundarySpruceWest", new Vector3(-43.0f, 0f, 58.5f), 10.4f, VegetationStyle.Conifer, "30483f"),
                     ("ArrivalBoundaryBirchWest", new Vector3(-30.5f, 0f, 60.0f), 8.8f, VegetationStyle.Birch, "596047"),
                     ("ArrivalBoundaryBroadleafWest", new Vector3(-17.0f, 0f, 59.0f), 7.6f, VegetationStyle.Broadleaf, "48553f"),
                     ("ArrivalBoundaryBroadleafEast", new Vector3(17.0f, 0f, 59.5f), 7.9f, VegetationStyle.Broadleaf, "48553f"),
                     ("ArrivalBoundaryBirchEast", new Vector3(30.5f, 0f, 60.5f), 9.0f, VegetationStyle.Birch, "596047"),
                     ("ArrivalBoundarySpruceEast", new Vector3(43.0f, 0f, 58.0f), 10.0f, VegetationStyle.Conifer, "30483f")
                 })
        {
            AddVisualTree(parent, name, position, height, style, color);
        }
    }

    private static void BuildHouseYardComposition(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("house_old_pc", out var placement))
        {
            return;
        }

        var approach = GetConnector("arrival-to-house-yard");
        var front = HorizontalDirection(approach.End - placement.Origin);
        var side = new Vector3(front.Z, 0f, -front.X);
        var origin = placement.Origin;
        var yaw = DirectionYaw(front);

        AddVisualParcelPatch(parent, "HouseYardWestLawn", origin + side * -6.8f + front * 1.6f, new(8.5f, 0.16f, 7.4f), "68705a", yaw + 6f);
        AddVisualParcelPatch(parent, "HouseYardEastLawn", origin + side * 6.5f - front * 1.7f, new(7.2f, 0.14f, 8.4f), "596047", yaw - 8f);

        AddVillageFacade(
            parent,
            "HouseYardWestFacade",
            origin + side * -8.0f - front * 1.4f,
            new(5.8f, 2.8f, 4.4f),
            2.7f,
            yaw + 12f,
            "786f5e",
            "4d4037",
            "b49c78",
            false,
            true);
        AddVillageFacade(
            parent,
            "HouseYardEastBarn",
            origin + side * 8.3f - front * 2.1f,
            new(5.0f, 2.2f, 3.6f),
            2.1f,
            yaw - 18f,
            "69746b",
            "3f4540",
            "a28f6e",
            true,
            false);
        AddVisualCanopy(parent, "HouseYardToolCanopy", origin + side * -5.8f + front * 3.2f, 3.4f, 2.8f, yaw - 25f, "6b5844", "4b4238");
        AddVisualShed(parent, "HouseYardSmallShed", origin + side * 7.2f + front * 3.8f, 0.64f, yaw + 28f, "806d58", "4d3e34");
        AddVisualWoodpile(parent, "HouseYardWoodpile", origin + side * -4.9f - front * 2.5f, 0.98f, yaw + 90f);

        AddVisualFenceRun(parent, "HouseYardBackWestFence", origin + side * -10.0f - front * 4.0f, origin + side * -10.0f + front * 5.2f);
        AddVisualFenceRun(parent, "HouseYardBackEastFence", origin + side * 10.0f - front * 4.0f, origin + side * 10.0f + front * 5.2f);
        AddVisualFenceRun(parent, "HouseYardFrontWestFence", origin + front * 5.7f + side * -10.0f, origin + front * 5.7f + side * -2.1f);
        AddVisualFenceRun(parent, "HouseYardFrontEastFence", origin + front * 5.7f + side * 2.0f, origin + front * 5.7f + side * 10.0f);
        AddVisualGate(parent, "HouseYardWestGate", origin + front * 5.7f + side * -1.0f, 1.7f, 1.3f, yaw);
        AddVisualGate(parent, "HouseYardEastGate", origin + front * 4.6f + side * 4.9f, 1.45f, 1.12f, yaw + 4f);

        AddVisualTree(parent, "HouseYardNearBirch", origin + side * -11.5f - front * 2.5f, 6.5f, VegetationStyle.Birch, "68705a");
        AddVisualTree(parent, "HouseYardNearBroadleaf", origin + side * 10.8f + front * 1.3f, 5.8f, VegetationStyle.Broadleaf, "53634e");
        AddVisualTree(parent, "HouseYardMidConifer", origin + side * -14.0f + front * 7.0f, 8.0f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "HouseYardMidBirch", origin + side * 13.0f - front * 8.0f, 7.1f, VegetationStyle.Birch, "596047");
        AddVisualShrub(parent, "HouseYardShrubWest", origin + side * -5.3f + front * 4.5f, 0.74f, "596047");
        AddVisualShrub(parent, "HouseYardShrubEast", origin + side * 5.8f - front * 2.7f, 0.66f, "48553f");
        AddVisualGrassClump(parent, "HouseYardGrassWest", origin + side * -4.2f + front * 1.8f, 0.72f, "68705a");
        AddVisualGrassClump(parent, "HouseYardGrassEast", origin + side * 4.6f + front * 2.2f, 0.60f, "596047");
        AddVisualStoneCluster(parent, "HouseYardStoneWest", origin + side * -6.2f - front * 3.6f, 0.72f, "777669");
        AddVisualStoneCluster(parent, "HouseYardStoneEast", origin + side * 6.0f - front * 3.0f, 0.58f, "6d6c62");
    }

    private static void BuildFapComposition(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("fap_clinic", out var placement))
        {
            return;
        }

        var approach = GetConnector("village-to-fap-branch");
        var front = HorizontalDirection(approach.End - placement.Origin);
        var side = new Vector3(front.Z, 0f, -front.X);
        var origin = placement.Origin;
        var yaw = DirectionYaw(front);

        AddVisualParcelPatch(parent, "FapWestLawn", origin + side * -8.7f + front * 1.8f, new(7.6f, 0.14f, 8.5f), "646951", yaw - 6f);
        AddVisualParcelPatch(parent, "FapEastLawn", origin + side * 8.6f - front * 1.2f, new(8.0f, 0.16f, 7.6f), "596047", yaw + 8f);
        AddVillageFacade(
            parent,
            "FapServiceFacadeWest",
            origin + side * -9.5f - front * 2.2f,
            new(5.6f, 2.35f, 3.8f),
            2.2f,
            yaw + 14f,
            "7d7663",
            "4b4036",
            "b39b72",
            true,
            false);
        AddVillageFacade(
            parent,
            "FapServiceFacadeEast",
            origin + side * 10.0f + front * 4.2f,
            new(6.5f, 2.95f, 4.5f),
            2.8f,
            yaw - 10f,
            "69746b",
            "55463a",
            "a58e6e",
            false,
            true);
        AddVisualCanopy(parent, "FapAmbulanceCanopy", origin + side * 5.0f + front * 5.2f, 3.6f, 2.6f, yaw + 7f, "6a5744", "4c443c");
        AddVisualShed(parent, "FapFuelShed", origin - side * 6.0f - front * 2.8f, 0.70f, yaw - 19f, "806d58", "4d3e34");
        AddVisualWoodpile(parent, "FapWoodpile", origin + side * 7.8f - front * 3.2f, 0.84f, yaw + 90f);

        AddVisualFenceRun(parent, "FapWestParcelFence", origin + side * -11.2f - front * 3.5f, origin + side * -11.2f + front * 5.5f);
        AddVisualFenceRun(parent, "FapEastParcelFence", origin + side * 11.0f - front * 1.5f, origin + side * 11.0f + front * 6.0f);
        AddVisualFenceRun(parent, "FapFrontFenceWest", origin + front * 6.7f + side * -11.0f, origin + front * 6.7f + side * -2.5f);
        AddVisualFenceRun(parent, "FapFrontFenceEast", origin + front * 6.7f + side * 2.8f, origin + front * 6.7f + side * 11.0f);
        AddVisualGate(parent, "FapServiceGate", origin + front * 6.7f + side * -1.1f, 1.8f, 1.28f, yaw);

        AddVisualTree(parent, "FapNearBirch", origin + side * -12.4f + front * 2.5f, 6.5f, VegetationStyle.Birch, "68705a");
        AddVisualTree(parent, "FapNearBroadleaf", origin + side * 12.1f + front * 1.1f, 5.7f, VegetationStyle.Broadleaf, "53634e");
        AddVisualTree(parent, "FapMidConifer", origin + side * -15.2f - front * 8.0f, 8.2f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "FapMidBirch", origin + side * 15.4f - front * 7.5f, 7.4f, VegetationStyle.Birch, "596047");
        AddVisualShrub(parent, "FapShrubWest", origin + side * -7.2f + front * 4.8f, 0.68f, "596047");
        AddVisualShrub(parent, "FapShrubEast", origin + side * 7.0f + front * 3.8f, 0.72f, "48553f");
        AddVisualGrassClump(parent, "FapGrassNearWest", origin + side * -4.5f + front * 2.2f, 0.62f, "68705a");
        AddVisualGrassClump(parent, "FapGrassNearEast", origin + side * 4.1f + front * 1.4f, 0.68f, "596047");
        AddVisualStoneCluster(parent, "FapStoneCluster", origin + side * -5.2f - front * 3.8f, 0.64f, "777669");

        // FAP return is a diagonal view back toward the village. These small
        // service-edge masses give it a legible destination instead of a
        // bare field, while staying outside the branch and loop surfaces.
        AddVillageFacade(parent, "FapReturnHouseWest", new(16.0f, 0f, -12.5f), new(5.8f, 2.4f, 4.0f), 2.3f, 88f, "786f5e", "4d4037", "b49c78", false, true);
        AddVillageFacade(parent, "FapReturnHouseEast", new(33.0f, 0f, -10.5f), new(6.5f, 2.7f, 4.6f), 2.6f, -92f, "69746b", "55463a", "a58e6e", true, false);
        AddVisualShed(parent, "FapReturnShedWest", new(11.5f, 0f, -16.0f), 0.62f, 84f, "806d58", "4d3e34");
        AddVisualShed(parent, "FapReturnShedEast", new(36.0f, 0f, -15.0f), 0.60f, -88f, "69746b", "3e4440");
        AddVisualTree(parent, "FapReturnBirchWest", new(10.8f, 0f, -8.0f), 7.4f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "FapReturnBroadleafEast", new(39.0f, 0f, -7.0f), 6.7f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "FapReturnConiferFarWest", new(8.0f, 0f, -3.0f), 8.6f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "FapReturnBirchFarEast", new(42.0f, 0f, -3.5f), 8.0f, VegetationStyle.Birch, "596047");
    }

    private static void BuildZiratComposition(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("zirat_road", out var placement))
        {
            return;
        }

        var origin = placement.Origin;
        AddVisualParcelPatch(parent, "ZiratQuietWestGround", origin + new Vector3(-10.8f, 0.08f, 0f), new(7.5f, 0.16f, 30f), "596047", -2f);
        AddVisualParcelPatch(parent, "ZiratQuietEastGround", origin + new Vector3(10.4f, 0.07f, -1.5f), new(7.8f, 0.14f, 28f), "626954", 4f);

        AddVillageFacade(parent, "ZiratReturnFarmhouseWest", origin + new Vector3(-15.8f, 0f, 25.5f), new(6.6f, 2.8f, 4.7f), 2.7f, 88f, "786f5e", "4d4037", "b49c78", false, true);
        AddVillageFacade(parent, "ZiratReturnFarmhouseEast", origin + new Vector3(16.4f, 0f, 24.0f), new(5.4f, 2.4f, 4.0f), 2.3f, -92f, "69746b", "55463a", "a58e6e", true, false);
        AddVillageFacade(parent, "ZiratFarFarmhouseWest", origin + new Vector3(-16.8f, 0f, -29.0f), new(6.0f, 2.5f, 4.5f), 2.4f, 92f, "7d7663", "4b4036", "b39b72", false, false);
        AddVillageFacade(parent, "ZiratFarFarmhouseEast", origin + new Vector3(17.2f, 0f, -31.5f), new(7.0f, 3.0f, 5.2f), 2.9f, -88f, "69746b", "4c443c", "9d9879", false, true);

        AddVisualCanopy(parent, "ZiratCaretakerCanopy", origin + new Vector3(-11.3f, 0f, 13.4f), 2.6f, 2.4f, 90f, "6b5844", "4b4238");
        AddVisualWoodpile(parent, "ZiratCaretakerWoodpile", origin + new Vector3(11.4f, 0f, 15.0f), 0.72f, 90f);
        AddVisualFenceRun(parent, "ZiratReturnParcelWest", origin + new Vector3(-14.3f, 0f, 20.0f), origin + new Vector3(-20.5f, 0f, 30.5f));
        AddVisualFenceRun(parent, "ZiratReturnParcelEast", origin + new Vector3(14.4f, 0f, 19.0f), origin + new Vector3(20.6f, 0f, 28.2f));

        var ziratApproachGate = new Node3D
        {
            Name = "ZiratApproachOpenGate",
            Position = origin + new Vector3(0f, 0f, 7.0f)
        };
        ziratApproachGate.SetMeta("visualOnly", true);
        ziratApproachGate.SetMeta("culturalPlaceholder", "open respectful zirat entrance silhouette; no cross; no inscription; route remains open");
        parent.AddChild(ziratApproachGate);
        AddVisualBox(ziratApproachGate, "GatePostWest", new(0.24f, 2.02f, 0.24f), new(-4.25f, 1.01f, 0f), "5e503d", "wood");
        AddVisualBox(ziratApproachGate, "GatePostEast", new(0.24f, 1.86f, 0.24f), new(4.25f, 0.93f, 0f), "5e503d", "wood");
        AddVisualBox(ziratApproachGate, "GateHeader", new(8.70f, 0.18f, 0.18f), new(0f, 2.12f, 0f), "5e503d", "wood");
        AddVisualBox(ziratApproachGate, "GateLeafWest", new(2.55f, 1.02f, 0.10f), new(-5.05f, 0.51f, 0f), "685546", "wood", rollDegrees: -4f);
        AddVisualBox(ziratApproachGate, "GateLeafEast", new(2.55f, 0.94f, 0.10f), new(5.05f, 0.47f, 0f), "685546", "wood", rollDegrees: 4f);

        AddVisualTree(parent, "ZiratNearBirchWest", origin + new Vector3(-13.4f, 0f, 18.6f), 6.6f, VegetationStyle.Birch, "68705a");
        AddVisualTree(parent, "ZiratNearBroadleafEast", origin + new Vector3(13.0f, 0f, 16.8f), 5.8f, VegetationStyle.Broadleaf, "53634e");
        AddVisualTree(parent, "ZiratMidConiferWest", origin + new Vector3(-15.2f, 0f, -5.0f), 8.4f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ZiratMidBirchEast", origin + new Vector3(15.0f, 0f, -8.0f), 7.7f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ZiratFarBroadleafWest", origin + new Vector3(-17.6f, 0f, -34.0f), 8.0f, VegetationStyle.Broadleaf, "4e5b47");
        AddVisualTree(parent, "ZiratFarConiferEast", origin + new Vector3(18.3f, 0f, -37.0f), 9.0f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ZiratSideBirchNorth", origin + new Vector3(-20.0f, 0f, 9.5f), 7.2f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ZiratSideBroadleafSouth", origin + new Vector3(20.4f, 0f, -20.0f), 7.0f, VegetationStyle.Broadleaf, "48553f");

        AddVisualShrub(parent, "ZiratQuietShrubWest", origin + new Vector3(-9.8f, 0f, 8.8f), 0.70f, "596047");
        AddVisualShrub(parent, "ZiratQuietShrubEast", origin + new Vector3(9.6f, 0f, 5.6f), 0.66f, "48553f");
        AddVisualGrassClump(parent, "ZiratQuietGrassWest", origin + new Vector3(-5.2f, 0f, 10.8f), 0.62f, "68705a");
        AddVisualGrassClump(parent, "ZiratQuietGrassEast", origin + new Vector3(5.0f, 0f, 7.6f), 0.58f, "596047");
        AddVisualStoneCluster(parent, "ZiratQuietStoneWest", origin + new Vector3(-7.5f, 0f, 3.4f), 0.54f, "777669");
        AddVisualStoneCluster(parent, "ZiratQuietStoneEast", origin + new Vector3(7.7f, 0f, -1.6f), 0.50f, "6d6c62");
    }

    private static void BuildKaraUrmanComposition(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("kara_urman_night", out var placement))
        {
            return;
        }

        var origin = placement.Origin;
        AddVisualLandformSegment(parent, "KaraEdgeWestShoulder", origin + new Vector3(-4.1f, 0f, 13f), origin + new Vector3(-4.1f, 0f, -25f), 1.15f, 0.16f, "4d5545", "earth");
        AddVisualLandformSegment(parent, "KaraEdgeEastShoulder", origin + new Vector3(4.1f, 0f, 13f), origin + new Vector3(4.1f, 0f, -25f), 1.15f, 0.14f, "4b5245", "earth");
        AddVisualLandformSegment(parent, "KaraEdgeWestRootBank", origin + new Vector3(-9.8f, 0f, -4f), origin + new Vector3(-13.5f, 0f, -25f), 2.0f, 0.28f, "3e493f", "earth", 0.02f);
        AddVisualLandformSegment(parent, "KaraEdgeEastRootBank", origin + new Vector3(9.4f, 0f, -5f), origin + new Vector3(13.5f, 0f, -26f), 2.0f, 0.24f, "3d473d", "earth", 0.02f);

        AddVisualTree(parent, "KaraLineNearBirchWest", origin + new Vector3(-8.8f, 0f, 7.5f), 7.4f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "KaraLineNearBroadleafEast", origin + new Vector3(8.8f, 0f, 6.0f), 6.6f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "KaraLineMidConiferWest", origin + new Vector3(-10.5f, 0f, -1.5f), 9.0f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "KaraLineMidBirchEast", origin + new Vector3(11.0f, 0f, -3.8f), 8.2f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "KaraLineMidBroadleafWest", origin + new Vector3(-8.0f, 0f, -11.6f), 8.0f, VegetationStyle.Broadleaf, "4e5b47");
        AddVisualTree(parent, "KaraLineMidConiferEast", origin + new Vector3(8.4f, 0f, -14.0f), 9.4f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "KaraLineFarBirchWest", origin + new Vector3(-16.0f, 0f, -24.0f), 10.5f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "KaraLineFarBroadleafEast", origin + new Vector3(16.6f, 0f, -26.5f), 9.8f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "KaraLineNearBroadleafWest", origin + new Vector3(-13.0f, 0f, 5.0f), 6.2f, VegetationStyle.Broadleaf, "4e5b47");
        AddVisualTree(parent, "KaraLineNearBirchEast", origin + new Vector3(13.2f, 0f, 3.0f), 6.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "KaraLineFarBroadleafWest", origin + new Vector3(-21.5f, 0f, -31.0f), 8.6f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "KaraLineFarBirchEast", origin + new Vector3(21.8f, 0f, -33.0f), 9.0f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "KaraLineRoadsideBroadleafWest", origin + new Vector3(-5.8f, 0f, -3.5f), 7.0f, VegetationStyle.Broadleaf, "48553f");
        AddVisualTree(parent, "KaraLineRoadsideBirchEast", origin + new Vector3(5.9f, 0f, -6.0f), 7.4f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "KaraLineRoadsideBroadleafFarWest", origin + new Vector3(-5.4f, 0f, -17.5f), 8.2f, VegetationStyle.Broadleaf, "4e5b47");
        AddVisualTree(parent, "KaraLineRoadsideBirchFarEast", origin + new Vector3(5.6f, 0f, -19.0f), 8.4f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "KaraLinePineFarWest", origin + new Vector3(-13.0f, 0f, -18.5f), 10.2f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "KaraLinePineFarEast", origin + new Vector3(13.6f, 0f, -21.0f), 9.4f, VegetationStyle.Conifer, "30483f");

        AddVisualShrub(parent, "KaraEdgeShrubWest", origin + new Vector3(-6.2f, 0f, 3.4f), 0.86f, "3f4d40");
        AddVisualShrub(parent, "KaraEdgeShrubEast", origin + new Vector3(6.4f, 0f, 1.5f), 0.78f, "3b4b3e");
        AddVisualGrassClump(parent, "KaraEdgeGrassWest", origin + new Vector3(-4.8f, 0f, 8.0f), 0.68f, "53634e");
        AddVisualGrassClump(parent, "KaraEdgeGrassEast", origin + new Vector3(4.9f, 0f, 6.4f), 0.64f, "48553f");
        AddVisualStoneCluster(parent, "KaraEdgeStoneWest", origin + new Vector3(-7.0f, 0f, -8.2f), 0.78f, "62675c");
        AddVisualStoneCluster(parent, "KaraEdgeStoneEast", origin + new Vector3(7.4f, 0f, -10.6f), 0.70f, "596057");
    }

    private static void AddVisualLandformSegment(
        Node3D parent,
        string name,
        Vector3 start,
        Vector3 end,
        float width,
        float height,
        string color,
        string surface,
        float baseY = 0f)
    {
        start.Y = baseY;
        end.Y = baseY;
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length <= 0.05f)
        {
            return;
        }

        var yaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
        var midpoint = (start + end) * 0.5f;
        midpoint.Y = baseY + height * 0.5f;
        var mesh = AddVisualLandformSurface(parent, name, width, height, length, midpoint, color, surface, yaw);
        mesh.SetMeta("terrainRole", "visual-only berm/ditch/shoulder; no traversal ownership");
    }

    private static MeshInstance3D AddVisualLandformSurface(
        Node3D parent,
        string name,
        float width,
        float height,
        float length,
        Vector3 center,
        string color,
        string surface,
        float yawDegrees)
    {
        const int crossSections = 5;
        const int lengthSections = 7;
        var vertices = new Vector3[crossSections * lengthSections];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var xProfile = new[] { -0.5f, -0.24f, 0f, 0.24f, 0.5f };
        var phase = name.Length * 0.37f;
        for (var zIndex = 0; zIndex < lengthSections; zIndex++)
        {
            var zT = zIndex / (float)(lengthSections - 1);
            var z = Mathf.Lerp(-length * 0.5f, length * 0.5f, zT);
            var endFade = 0.88f + 0.08f * Mathf.Sin(zT * Mathf.Pi);
            for (var xIndex = 0; xIndex < crossSections; xIndex++)
            {
                var profile = xProfile[xIndex];
                var crown = 1f - Mathf.Abs(profile) * 0.48f;
                var breakup = 0.012f * Mathf.Sin(phase + zT * 5.4f + xIndex * 1.8f);
                var vertexIndex = zIndex * crossSections + xIndex;
                vertices[vertexIndex] = new Vector3(
                    profile * width * endFade,
                    height * (0.18f + 0.82f * crown) + breakup,
                    z);
                normals[vertexIndex] = Vector3.Up;
                uvs[vertexIndex] = new Vector2(profile + 0.5f, zT);
            }
        }

        var indices = new int[(crossSections - 1) * (lengthSections - 1) * 6];
        var index = 0;
        for (var zIndex = 0; zIndex < lengthSections - 1; zIndex++)
        {
            for (var xIndex = 0; xIndex < crossSections - 1; xIndex++)
            {
                var topLeft = zIndex * crossSections + xIndex;
                var topRight = topLeft + 1;
                var bottomLeft = (zIndex + 1) * crossSections + xIndex;
                var bottomRight = bottomLeft + 1;
                indices[index++] = topLeft;
                indices[index++] = bottomLeft;
                indices[index++] = topRight;
                indices[index++] = topRight;
                indices[index++] = bottomLeft;
                indices[index++] = bottomRight;
            }
        }

        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var reliefMesh = new ArrayMesh();
        reliefMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = center,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Mesh = reliefMesh,
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface)
        };
        mesh.SetMeta("visualOnly", true);
        mesh.SetMeta("terrainRole", "visual-only low-poly crown surface; no traversal ownership");
        parent.AddChild(mesh);
        return mesh;
    }

    private static void AddVisualParcelPatch(
        Node3D parent,
        string name,
        Vector3 center,
        Vector3 size,
        string color,
        float yawDegrees)
    {
        // A box reads as a floating greybox slab in first person. Keep the
        // same visual-only ownership, but give each parcel a shallow,
        // irregular low-poly perimeter that settles into the shared ground.
        var patch = new MeshInstance3D
        {
            Name = name,
            Position = center,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Mesh = BuildIrregularParcelPatchMesh(size, name),
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "earth")
        };
        patch.SetMeta("visualOnly", true);
        patch.SetMeta("terrainRole", "visual-only parcel/ground plane break");
        patch.SetMeta("parcelGeometry", "irregular low-poly relief; no traversal ownership");
        parent.AddChild(patch);
    }

    private static ArrayMesh BuildIrregularParcelPatchMesh(Vector3 size, string seedName)
    {
        const int ringCount = 12;
        var vertices = new Vector3[ringCount + 1];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var reliefHeight = Mathf.Clamp(size.Y, 0.03f, 0.30f);
        var indices = new int[ringCount * 3];
        var phase = 0f;
        foreach (var character in seedName)
        {
            phase = (phase * 31f + character) % 997f;
        }

        vertices[0] = new Vector3(0f, reliefHeight * 0.74f, 0f);
        normals[0] = Vector3.Up;
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (var index = 0; index < ringCount; index++)
        {
            var angle = Mathf.Tau * index / ringCount;
            var jitter = 0.86f + 0.07f * Mathf.Sin(phase * 0.013f + index * 1.71f);
            var x = Mathf.Cos(angle) * size.X * 0.5f * jitter;
            var z = Mathf.Sin(angle) * size.Z * 0.5f * jitter;
            var y = reliefHeight * (0.18f + 0.20f * (0.5f + 0.5f * Mathf.Sin(phase * 0.021f + index * 1.37f)));
            vertices[index + 1] = new Vector3(x, y, z);
            normals[index + 1] = Vector3.Up;
            uvs[index + 1] = new Vector2(x / Mathf.Max(size.X, 0.001f) + 0.5f, z / Mathf.Max(size.Z, 0.001f) + 0.5f);
            var triangle = index * 3;
            indices[triangle] = 0;
            indices[triangle + 1] = index == ringCount - 1 ? 1 : index + 2;
            indices[triangle + 2] = index + 1;
        }

        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static Node3D AddVillageFacade(
        Node3D parent,
        string name,
        Vector3 anchor,
        Vector3 footprint,
        float wallHeight,
        float yawDegrees,
        string wallColor,
        string roofColor,
        string trimColor,
        bool singleSlopeRoof,
        bool porch)
    {
        if (!singleSlopeRoof && footprint.X >= 5.4f)
        {
            // Large gabled dwellings use the authored HouseA family so their
            // first-person silhouette is not another flat facade box. Scale
            // follows the authored footprint while staying within the proven
            // near-house range; anchor, yaw and presentation ownership remain
            // the caller's values.
            var authoredScale = Mathf.Clamp(footprint.X / 8.0f, 0.55f, 0.95f);
            var authoredFacade = AddAuthoredHouse(parent, name, anchor, authoredScale, yawDegrees);
            authoredFacade.SetMeta("visualOnly", true);
            authoredFacade.SetMeta("collisionOwner", "none");
            authoredFacade.SetMeta("presentationRole", "varied authored village facade; no gameplay ownership");
            return authoredFacade;
        }

        var facade = new Node3D
        {
            Name = name,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        facade.SetMeta("visualOnly", true);
        facade.SetMeta("collisionOwner", "none");
        facade.SetMeta("presentationRole", "varied authored village facade; no gameplay ownership");
        parent.AddChild(facade);

        AddVisualBox(facade, "Wall", new(footprint.X, wallHeight, footprint.Z), new(0f, wallHeight * 0.5f, 0f), wallColor, "wood_facade");
        if (singleSlopeRoof)
        {
            AddVisualBox(
                facade,
                "LeanToRoof",
                new(footprint.X * 0.82f, 0.28f, footprint.Z + 0.38f),
                new(0.16f, wallHeight + 0.24f, 0f),
                roofColor,
                "wood",
                rollDegrees: 10f);
            AddVisualBox(
                facade,
                "LeanToRidge",
                new(0.16f, 0.18f, footprint.Z + 0.46f),
                new(-footprint.X * 0.28f, wallHeight + 0.56f, 0f),
                roofColor,
                "wood");
        }
        else
        {
            AddVisualPitchedRoof(facade, "GabledRoof", footprint.X, footprint.Z, wallHeight, 0.82f, 0.22f, roofColor);
        }

        var frontZ = footprint.Z * 0.5f + 0.05f;
        var doorX = -footprint.X * 0.25f;
        AddVisualBox(facade, "Door", new(0.92f, Mathf.Min(2.05f, wallHeight * 0.72f), 0.08f), new(doorX, 0.78f, frontZ), "4b382c", "wood");
        AddVisualBox(facade, "DoorLintel", new(1.16f, 0.11f, 0.12f), new(doorX, 1.72f, frontZ + 0.03f), trimColor, "wood");
        AddVisualBox(facade, "Window", new(1.12f, 0.78f, 0.06f), new(footprint.X * 0.18f, wallHeight * 0.56f, frontZ + 0.02f), "6f8079", "glass");
        AddVisualBox(facade, "WindowTrimTop", new(1.30f, 0.10f, 0.10f), new(footprint.X * 0.18f, wallHeight * 0.56f + 0.47f, frontZ + 0.05f), trimColor, "wood");
        AddVisualBox(facade, "WindowTrimBottom", new(1.30f, 0.10f, 0.10f), new(footprint.X * 0.18f, wallHeight * 0.56f - 0.47f, frontZ + 0.05f), trimColor, "wood");

        // First-person review reaches the rear of these parcels as often as
        // the approach sees their front. A restrained rear window pair keeps
        // the turn authored without turning every house into a decorative
        // billboard or adding another gameplay owner.
        var rearZ = -footprint.Z * 0.5f - 0.04f;
        var rearWindowWidth = Mathf.Min(0.92f, footprint.X * 0.22f);
        var rearWindowHeight = Mathf.Min(0.68f, wallHeight * 0.24f);
        var rearWindowY = Mathf.Clamp(wallHeight * 0.56f, 1.15f, 1.72f);
        foreach (var (offset, suffix) in new[] { (-footprint.X * 0.22f, "Left"), (footprint.X * 0.22f, "Right") })
        {
            AddVisualBox(
                facade,
                $"RearWindow{suffix}",
                new(rearWindowWidth, rearWindowHeight, 0.06f),
                new(offset, rearWindowY, rearZ),
                "65756e",
                "glass");
            AddVisualBox(
                facade,
                $"RearWindow{suffix}Top",
                new(rearWindowWidth + 0.16f, 0.08f, 0.10f),
                new(offset, rearWindowY + rearWindowHeight * 0.56f, rearZ - 0.03f),
                trimColor,
                "wood");
            AddVisualBox(
                facade,
                $"RearWindow{suffix}Bottom",
                new(rearWindowWidth + 0.16f, 0.08f, 0.10f),
                new(offset, rearWindowY - rearWindowHeight * 0.56f, rearZ - 0.03f),
                trimColor,
                "wood");
        }

        foreach (var (x, suffix) in new[]
                 {
                     (-footprint.X * 0.5f - 0.05f, "West"),
                     (footprint.X * 0.5f + 0.05f, "East")
                 })
        {
            AddVisualBox(facade, $"SideWindow{suffix}", new(0.06f, rearWindowHeight, 0.86f), new(x, rearWindowY, 0.05f), "65756e", "glass");
            AddVisualBox(facade, $"SideWindow{suffix}Top", new(0.10f, 0.08f, 1.02f), new(x, rearWindowY + rearWindowHeight * 0.56f, 0.05f), trimColor, "wood");
            AddVisualBox(facade, $"SideWindow{suffix}Bottom", new(0.10f, 0.08f, 1.02f), new(x, rearWindowY - rearWindowHeight * 0.56f, 0.05f), trimColor, "wood");
        }

        if (porch)
        {
            AddVisualBox(facade, "Porch", new(2.55f, 0.16f, 1.08f), new(doorX, 0.14f, frontZ + 0.40f), "6a4d38", "wood");
            AddVisualBox(facade, "PorchRoof", new(3.0f, 0.14f, 1.0f), new(doorX, 2.18f, frontZ + 0.48f), roofColor, "wood", rollDegrees: 6f);
            AddVisualBox(facade, "PorchPostLeft", new(0.11f, 1.95f, 0.11f), new(doorX - 1.05f, 1.08f, frontZ + 0.78f), trimColor, "wood");
            AddVisualBox(facade, "PorchPostRight", new(0.11f, 1.95f, 0.11f), new(doorX + 1.05f, 1.08f, frontZ + 0.78f), trimColor, "wood");
        }

        return facade;
    }

    private static void AddVisualCanopy(
        Node3D parent,
        string name,
        Vector3 anchor,
        float width,
        float depth,
        float yawDegrees,
        string roofColor,
        string postColor)
    {
        var canopy = new Node3D
        {
            Name = name,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        canopy.SetMeta("visualOnly", true);
        canopy.SetMeta("collisionOwner", "none");
        canopy.SetMeta("presentationRole", "open yard canopy / shelter");
        parent.AddChild(canopy);

        foreach (var (x, z, height) in new[]
                 {
                     (-width * 0.5f, -depth * 0.5f, 1.95f),
                     (width * 0.5f, -depth * 0.5f, 1.82f),
                     (-width * 0.5f, depth * 0.5f, 1.72f),
                     (width * 0.5f, depth * 0.5f, 1.88f)
                 })
        {
            AddVisualBox(canopy, "Post", new(0.12f, height, 0.12f), new(x, height * 0.5f, z), postColor, "wood_fence");
        }

        AddVisualBox(canopy, "Roof", new(width + 0.30f, 0.18f, depth + 0.32f), new(0f, 2.08f, 0f), roofColor, "wood", rollDegrees: 5f);
        AddVisualBox(canopy, "RoofRidge", new(width * 0.72f, 0.10f, 0.16f), new(0f, 2.26f, 0f), postColor, "wood");
    }

    private static void AddVisualWoodpile(Node3D parent, string name, Vector3 anchor, float scale, float yawDegrees)
    {
        var pile = new Node3D
        {
            Name = name,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Scale = Vector3.One * scale
        };
        pile.SetMeta("visualOnly", true);
        pile.SetMeta("presentationRole", "stacked firewood yard detail");
        parent.AddChild(pile);

        AddVisualBox(pile, "LogBottomA", new(1.65f, 0.18f, 0.24f), new(-0.08f, 0.12f, 0f), "6c5845", "wood_bark", rollDegrees: -4f);
        AddVisualBox(pile, "LogBottomB", new(1.42f, 0.18f, 0.22f), new(0.12f, 0.32f, 0.08f), "7a6048", "wood_bark", rollDegrees: 5f);
        AddVisualBox(pile, "LogTopA", new(1.16f, 0.17f, 0.20f), new(-0.02f, 0.52f, -0.03f), "5d4c3e", "wood_bark", rollDegrees: -8f);
        AddVisualBox(pile, "LogEnd", new(0.20f, 0.26f, 0.26f), new(0.78f, 0.28f, 0.02f), "9b8766", "wood");
    }

    private static void AddVisualStreetLandmark(Node3D parent, string name, Vector3 anchor, float yawDegrees, string labelText = "КЫРЛАЙ")
    {
        var landmark = new Node3D
        {
            Name = name,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        landmark.SetMeta("visualOnly", true);
        landmark.SetMeta("landmarkRole", "village street orientation board; not a quest marker");
        parent.AddChild(landmark);

        AddVisualBox(landmark, "PostLeft", new(0.14f, 1.95f, 0.14f), new(-1.08f, 0.98f, 0f), "594a39", "wood_fence");
        AddVisualBox(landmark, "PostRight", new(0.14f, 1.72f, 0.14f), new(1.08f, 0.86f, 0f), "594a39", "wood_fence");
        AddVisualBox(landmark, "Board", new(2.42f, 1.18f, 0.12f), new(0f, 1.52f, 0f), "75634d", "wood_facade");
        AddVisualBox(landmark, "BoardCap", new(2.62f, 0.12f, 0.16f), new(0f, 2.16f, 0f), "4e4439", "wood");
        var label = new Label3D
        {
            Name = "VillageName",
            Text = labelText,
            Position = new Vector3(-0.75f, 1.48f, 0.08f),
            Modulate = Color.FromHtml("d2c29f"),
            OutlineModulate = Color.FromHtml("3c352b"),
            FontSize = 30,
            PixelSize = 0.0036f,
            OutlineSize = 3,
            DoubleSided = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled
        };
        landmark.AddChild(label);
    }

    private static void AddVisualGrassClump(Node3D parent, string name, Vector3 origin, float size, string color)
    {
        var grass = new Node3D { Name = name, Position = origin };
        grass.SetMeta("visualOnly", true);
        grass.SetMeta("vegetationStyle", "low-poly-grass-tuft");
        parent.AddChild(grass);
        for (var index = 0; index < 4; index++)
        {
            var angle = -18f + index * 12f;
            AddVisualBox(
                grass,
                $"Blade{index}",
                new(0.035f * size, 0.62f * size, 0.09f * size),
                new((index - 1.5f) * 0.08f * size, 0.31f * size, (index % 2 == 0 ? -0.04f : 0.04f) * size),
                color,
                "foliage",
                yawDegrees: angle,
                rollDegrees: index % 2 == 0 ? -10f : 9f);
        }
    }

    private static void AddVisualStoneCluster(Node3D parent, string name, Vector3 origin, float size, string color)
    {
        var stones = new Node3D { Name = name, Position = origin };
        stones.SetMeta("visualOnly", true);
        stones.SetMeta("presentationRole", "small field / road stone cluster");
        parent.AddChild(stones);
        foreach (var (offset, scale) in new[]
                 {
                     (new Vector3(-0.26f, 0.10f, 0.02f), new Vector3(0.52f, 0.28f, 0.40f)),
                     (new Vector3(0.18f, 0.13f, -0.08f), new Vector3(0.42f, 0.34f, 0.34f)),
                     (new Vector3(0.02f, 0.08f, 0.18f), new Vector3(0.30f, 0.20f, 0.28f))
                 })
        {
            stones.AddChild(new MeshInstance3D
            {
                Name = "Stone",
                Position = offset * size,
                Scale = new Vector3(scale.X * size, scale.Y * size, scale.Z * size),
                RotationDegrees = new Vector3(0f, size * 37f, size * 11f),
                Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 7, Rings = 3 },
                MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "stone")
            });
        }
    }

    private void BuildArrivalAndVillageFraming(Node3D parent)
    {
        // Keep the road's vanishing point open. Near/mid parcels carry the
        // composition; trees stay outside the travel corridor so the player
        // reads gates, facades and the next village landmark first.
        AddAuthoredHouse(parent, "ArrivalHouseWest", new(-11f, 1.4f, 23f), 0.82f, 180f);
        AddAuthoredHouse(parent, "ArrivalHouseEast", new(12f, 1.4f, 24f), 0.74f, 180f);
        AddAuthoredHouse(parent, "VillageSideHouseWest", new(-23f, 1.4f, 16f), 0.54f, 90f);
        AddAuthoredHouse(parent, "VillageSideHouseEast", new(23f, 1.4f, 16f), 0.54f, -90f);

        AddDistantHouse(parent, new(-14f, 0f, 4f), 90f, "VillageCenterMidHouseWest");
        AddDistantHouse(parent, new(15f, 0f, 9f), -90f, "VillageCenterMidHouseEast");
        AddVisualShed(parent, "VillageCenterShedWest", new(-12.2f, 0f, 11.4f), 0.82f, 90f, "806d58", "4d3e34");
        AddVisualShed(parent, "VillageCenterShedEast", new(12.4f, 0f, 13.1f), 0.76f, -90f, "69746b", "3e4440");

        AddDistantHouse(parent, new(-25f, 0f, 32f), 180f, "ArrivalFarHouseWest");
        AddDistantHouse(parent, new(26f, 0f, 34f), 180f, "ArrivalFarHouseEast");
        AddDistantHouse(parent, new(0f, 0f, 31f), 180f, "ArrivalRoadEndHouse");
        AddDistantHouse(parent, new(-29f, 0f, -43f), 90f, "ReturnFarHouseWest");
        AddDistantHouse(parent, new(30f, 0f, -45f), -90f, "ReturnFarHouseEast");
        AddDistantHouse(parent, new(-28f, 0f, -65f), 90f, "ZiratFarHouseWest");
        AddDistantHouse(parent, new(24f, 0f, -108f), -90f, "ForestFarHouseEast");

        foreach (var (name, origin, height, style) in new[]
                 {
                     ("ArrivalBirchWest", new Vector3(-15f, 0f, 28f), 6.9f, VegetationStyle.Birch),
                     ("ArrivalBroadleafEast", new Vector3(15f, 0f, 29f), 5.8f, VegetationStyle.Broadleaf),
                     ("ArrivalConiferFarWest", new Vector3(-27f, 0f, 36f), 7.6f, VegetationStyle.Conifer),
                     ("ArrivalConiferFarEast", new Vector3(29f, 0f, 38f), 7.2f, VegetationStyle.Conifer),
                     ("ArrivalBirchMidWest", new Vector3(-20f, 0f, 34f), 7.8f, VegetationStyle.Birch),
                     ("ArrivalBroadleafMidEast", new Vector3(21f, 0f, 35f), 6.4f, VegetationStyle.Broadleaf),
                     ("ReturnBirchWest", new Vector3(-19f, 0f, -42f), 6.8f, VegetationStyle.Birch),
                     ("ReturnBroadleafEast", new Vector3(21f, 0f, -48f), 6.1f, VegetationStyle.Broadleaf),
                     ("ZiratConiferFarWest", new Vector3(-25f, 0f, -82f), 8.1f, VegetationStyle.Conifer),
                     ("ZiratBirchFarEast", new Vector3(26f, 0f, -94f), 7.3f, VegetationStyle.Birch)
                 })
        {
            AddVisualTree(parent, name, origin, height, style);
        }

        // Side parcel fences frame the road without adding a rail across its
        // centerline. Gates and sheds sit on the parcels, not in the sightline.
        AddVisualFenceRun(parent, "ArrivalParcelFenceWest", new(-7f, 0f, 12f), new(-7f, 0f, 23f));
        AddVisualFenceRun(parent, "ArrivalParcelFenceEast", new(7f, 0f, 11f), new(7f, 0f, 22f));
        AddVisualFenceRun(parent, "VillageSideParcelWest", new(-16f, 0f, 8f), new(-16f, 0f, 17f));
        AddVisualFenceRun(parent, "VillageSideParcelEast", new(16f, 0f, 7f), new(16f, 0f, 16f));
        AddVisualGate(parent, "ArrivalGateWest", new(-7f, 0f, 16.8f), 1.9f, 1.35f, 90f);
        AddVisualGate(parent, "ArrivalGateEast", new(7f, 0f, 16.2f), 1.8f, 1.25f, 90f);
        AddVisualShrub(parent, "VillageCenterShrubWest", new(-8.4f, 0f, 10.8f), 0.68f, "48553f");
        AddVisualShrub(parent, "VillageCenterShrubEast", new(8.6f, 0f, 12.4f), 0.58f, "596047");
    }

    private void BuildHouseExterior(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("house_old_pc", out var placement))
        {
            return;
        }

        var approach = GetConnector("arrival-to-house-yard");
        var front = HorizontalDirection(approach.End - placement.Origin);
        var yaw = DirectionYaw(front);
        var side = new Vector3(front.Z, 0f, -front.X);
        var house = AddAuthoredHouse(
            parent,
            "HouseA_Act1Exterior",
            // The canonical house entry spawn faces -Z. The old route-relative
            // offset cancelled its X component and put the imported wall
            // almost exactly on that forward sightline. Keep the house in the
            // yard parcel, but bias the presentation anchor to the east side
            // so the road/apron/fence composition remains readable.
            placement.Origin + new Vector3(8.8f, 1.4f, -5.0f),
            0.50f,
            yaw);
        house.SetMeta("landmarkRole", "babay-abi-yard-house-front");
        house.SetMeta("approachDirection", front);

        AddVisualBox(
            parent,
            "HouseYardApproachApron",
            new Vector3(5.8f, 0.06f, 4.8f),
            placement.Origin + front * 1.9f + new Vector3(0f, 0.04f, 0f),
            "766957",
            "earth",
            yaw);

        AddVisualFenceRun(
            parent,
            "HouseYardFenceLeft",
            placement.Origin + side * -4.3f + front * 0.4f,
            placement.Origin + side * -4.3f + front * 4.4f);
        AddVisualFenceRun(
            parent,
            "HouseYardFenceRight",
            placement.Origin + side * 4.3f + front * 0.4f,
            placement.Origin + side * 4.3f + front * 3.5f);

        AddVisualFenceRun(
            parent,
            "HouseYardFrontFenceLeft",
            placement.Origin + front * 4.7f + side * -4.3f,
            placement.Origin + front * 4.7f + side * -1.2f);
        AddVisualFenceRun(
            parent,
            "HouseYardFrontFenceRight",
            placement.Origin + front * 4.7f + side * 1.2f,
            placement.Origin + front * 4.7f + side * 4.3f);
        AddVisualGate(parent, "HouseYardSideGate", placement.Origin + side * 4.3f + front * 2.2f, 1.55f, 1.25f, DirectionYaw(side));
        AddVisualShed(
            parent,
            "HouseYardBackShed",
            placement.Origin + side * 5.8f - front * 1.0f,
            0.86f,
            yaw,
            "6f6859",
            "3f403b");
        AddVisualTree(parent, "HouseYardBirch", placement.Origin + side * 7.2f - front * 1.8f, 5.1f, VegetationStyle.Birch, "596047");
        AddVisualShrub(parent, "HouseYardShrubNear", placement.Origin + side * 5.1f + front * 0.2f, 0.72f, "48553f");
        AddVisualShrub(parent, "HouseYardShrubFar", placement.Origin + side * -5.0f + front * 3.8f, 0.58f, "596047");
    }

    private void BuildFapExterior(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("fap_clinic", out var placement))
        {
            return;
        }

        var approach = GetConnector("village-to-fap-branch");
        var front = HorizontalDirection(approach.End - placement.Origin);
        var yaw = DirectionYaw(front);
        var facade = new Node3D
        {
            Name = "FapEntranceFacade",
            Position = placement.Origin,
            RotationDegrees = new Vector3(0f, yaw, 0f)
        };
        facade.SetMeta("visualOnly", true);
        facade.SetMeta("landmarkRole", "fap-entrance");
        facade.SetMeta("approachDirection", front);
        facade.SetMeta("culturalPlaceholder", "low-poly village clinic frontage");
        parent.AddChild(facade);

        AddVisualBox(facade, "FapFacadeWall", new(10.5f, 3.2f, 0.28f), new(0f, 1.6f, 0f), "8d887b", "plaster");
        AddVisualBox(facade, "FapRoofLeft", new(5.7f, 0.3f, 6.1f), new(-2.7f, 3.32f, 0f), "625747", "wood", rollDegrees: 17f);
        AddVisualBox(facade, "FapRoofRight", new(5.7f, 0.3f, 6.1f), new(2.7f, 3.32f, 0f), "625747", "wood", rollDegrees: -17f);
        AddVisualBox(facade, "FapDoor", new(1.35f, 2.15f, 0.08f), new(0f, 1.1f, 0.19f), "4b382c", "wood");
        AddVisualBox(facade, "FapDoorFrameLeft", new(0.12f, 2.35f, 0.12f), new(-0.78f, 1.18f, 0.22f), "6d5845", "wood");
        AddVisualBox(facade, "FapDoorFrameRight", new(0.12f, 2.35f, 0.12f), new(0.78f, 1.18f, 0.22f), "6d5845", "wood");
        AddVisualBox(facade, "FapDoorLintel", new(1.7f, 0.14f, 0.12f), new(0f, 2.3f, 0.22f), "6d5845", "wood");
        AddFapWindow(facade, "FapWindowLeft", new(-3.1f, 1.55f, 0.2f));
        AddFapWindow(facade, "FapWindowRight", new(3.1f, 1.55f, 0.2f));
        AddVisualBox(facade, "FapPorch", new(3.6f, 0.18f, 1.25f), new(0f, 0.16f, 0.72f), "6a4d38", "wood");
        AddVisualBox(facade, "FapStep", new(2.7f, 0.16f, 0.56f), new(0f, 0.08f, 1.42f), "574636", "wood");
        AddVisualBox(facade, "FapAwning", new(2.5f, 0.16f, 1.05f), new(0f, 2.55f, 0.72f), "554238", "wood", rollDegrees: 7f);
        AddVisualBox(facade, "FapSignBoard", new(2.2f, 0.55f, 0.08f), new(0f, 2.96f, 0.24f), "786653", "wood");
        var sign = new Label3D
        {
            Name = "FapSignText",
            Text = "ФАП",
            Position = new Vector3(-0.55f, 2.93f, 0.30f),
            Modulate = Color.FromHtml("d2c29f"),
            OutlineModulate = Color.FromHtml("3c352b"),
            FontSize = 44,
            PixelSize = 0.0038f,
            OutlineSize = 4,
            DoubleSided = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled
        };
        sign.SetMeta("wayfindingLandmark", "fap");
        facade.AddChild(sign);

        AddVisualBox(
            parent,
            "FapYardApproachApron",
            new Vector3(6.0f, 0.06f, 4.4f),
            placement.Origin + front * 1.8f + new Vector3(0f, 0.04f, 0f),
            "766957",
            "earth",
            yaw);

        var side = new Vector3(front.Z, 0f, -front.X);
        AddVisualFenceRun(
            parent,
            "FapYardFenceLeft",
            placement.Origin + side * -7.0f + front * 0.2f,
            placement.Origin + side * -7.0f + front * 5.0f);
        AddVisualFenceRun(
            parent,
            "FapYardFenceRight",
            placement.Origin + side * 7.0f + front * 0.3f,
            placement.Origin + side * 7.0f + front * 4.4f);
        AddVisualGate(parent, "FapYardGate", placement.Origin + side * 5.8f + front * 3.8f, 1.8f, 1.3f, DirectionYaw(front));
        AddVisualShed(
            parent,
            "FapServiceShed",
            placement.Origin - side * 5.0f - front * 1.6f,
            0.82f,
            yaw + 8f,
            "69746b",
            "493f36");
        AddVisualTree(parent, "FapYardBirch", placement.Origin + side * 8.6f - front * 0.8f, 5.8f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "FapYardBroadleaf", placement.Origin - side * 8.8f + front * 3.0f, 5.3f, VegetationStyle.Broadleaf, "48553f");
        AddVisualShrub(parent, "FapYardShrubLeft", placement.Origin + side * -8.0f + front * 2.0f, 0.62f, "596047");
        AddVisualShrub(parent, "FapYardShrubRight", placement.Origin + side * 8.0f + front * 1.0f, 0.64f, "48553f");
        AddDistantHouse(parent, placement.Origin + front * 16f + side * 7.5f, yaw + 90f, "FapReturnRoadHouse");
        AddVisualShed(parent, "FapReturnRoadHayShed", placement.Origin + front * 18f - side * 7.0f, 0.72f, yaw - 90f, "806d58", "4d3e34");
    }

    private void BuildZiratExterior(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("zirat_road", out var placement))
        {
            return;
        }

        var enclosure = new Node3D { Name = "ZiratGreyboxEnclosure" };
        enclosure.SetMeta("visualOnly", true);
        enclosure.SetMeta("culturalPlaceholder", "perimeter fence, gate and calm low grave-marker silhouettes; no crosses or inscriptions");
        parent.AddChild(enclosure);

        var origin = placement.Origin;
        AddVisualFenceRun(enclosure, "ZiratPerimeterWest", origin + new Vector3(-8.5f, 0f, 16f), origin + new Vector3(-8.5f, 0f, -14f));
        AddVisualFenceRun(enclosure, "ZiratPerimeterEast", origin + new Vector3(8.5f, 0f, 16f), origin + new Vector3(8.5f, 0f, -14f));
        AddVisualFenceRun(enclosure, "ZiratGateWest", origin + new Vector3(-8.5f, 0f, 16f), origin + new Vector3(-3.6f, 0f, 16f));
        AddVisualFenceRun(enclosure, "ZiratGateEast", origin + new Vector3(3.6f, 0f, 16f), origin + new Vector3(8.5f, 0f, 16f));
        AddVisualBox(enclosure, "ZiratGatePostWest", new(0.24f, 1.8f, 0.24f), origin + new Vector3(-3.6f, 0.9f, 16f), "5e503d", "wood");
        AddVisualBox(enclosure, "ZiratGatePostEast", new(0.24f, 1.8f, 0.24f), origin + new Vector3(3.6f, 0.9f, 16f), "5e503d", "wood");
        AddVisualBox(enclosure, "ZiratGateHeader", new(7.2f, 0.18f, 0.18f), origin + new Vector3(0f, 2.08f, 16f), "5e503d", "wood");
        AddVisualBox(enclosure, "ZiratGateLeafWest", new(2.9f, 1.12f, 0.10f), origin + new Vector3(-5.0f, 0.56f, 15.92f), "685546", "wood", rollDegrees: -3f);
        AddVisualBox(enclosure, "ZiratGateLeafEast", new(2.9f, 1.12f, 0.10f), origin + new Vector3(5.0f, 0.56f, 15.92f), "685546", "wood", rollDegrees: 3f);
        AddVisualBox(enclosure, "ZiratRoadEdgeWest", new(0.18f, 0.08f, 34f), origin + new Vector3(-3.05f, 0.05f, 0f), "3d4237", "earth");
        AddVisualBox(enclosure, "ZiratRoadEdgeEast", new(0.18f, 0.08f, 34f), origin + new Vector3(3.05f, 0.05f, 0f), "3d4237", "earth");

        foreach (var (position, size, yaw) in new[]
                 {
                     (new Vector3(-2.8f, 0f, 13.2f), new Vector3(0.96f, 1.42f, 0.46f), -4f),
                     (new Vector3(2.8f, 0f, 12.4f), new Vector3(0.90f, 1.34f, 0.44f), 8f),
                     (new Vector3(-2.75f, 0f, 7.2f), new Vector3(0.92f, 1.32f, 0.44f), 12f),
                     (new Vector3(2.75f, 0f, 6.2f), new Vector3(0.84f, 1.22f, 0.42f), -13f),
                     (new Vector3(-5.2f, 0f, 7.0f), new Vector3(0.62f, 0.78f, 0.34f), -8f),
                     (new Vector3(5.1f, 0f, 5.2f), new Vector3(0.48f, 0.62f, 0.30f), 11f),
                     (new Vector3(-5.8f, 0f, -2.8f), new Vector3(0.52f, 0.68f, 0.30f), 5f),
                     (new Vector3(5.5f, 0f, -4.4f), new Vector3(0.70f, 0.86f, 0.36f), -12f),
                     (new Vector3(-4.6f, 0f, -10.6f), new Vector3(0.44f, 0.56f, 0.28f), 17f),
                     (new Vector3(6.2f, 0f, -12.5f), new Vector3(0.58f, 0.72f, 0.32f), -6f),
                     (new Vector3(-7.0f, 0f, -17.0f), new Vector3(0.74f, 0.92f, 0.38f), 9f),
                     (new Vector3(7.3f, 0f, -17.5f), new Vector3(0.46f, 0.64f, 0.30f), -15f)
                 })
        {
            AddGraveMarker(enclosure, origin + position, size, yaw);
        }

        foreach (var (position, size, yaw) in new[]
                 {
                     (new Vector3(-3.65f, 0f, 11.6f), new Vector3(0.82f, 1.0f, 0.40f), -7f),
                     (new Vector3(3.65f, 0f, 10.2f), new Vector3(0.76f, 0.92f, 0.38f), 10f),
                     (new Vector3(-3.55f, 0f, 6.8f), new Vector3(0.68f, 0.84f, 0.36f), 14f),
                     (new Vector3(3.6f, 0f, 5.2f), new Vector3(0.72f, 0.88f, 0.38f), -11f)
                 })
        {
            AddGraveMarker(enclosure, origin + position, size, yaw);
        }

        foreach (var (position, height, style) in new[]
                 {
                     (new Vector3(-11f, 0f, 10f), 6.7f, VegetationStyle.Birch),
                     (new Vector3(11f, 0f, 7f), 6.1f, VegetationStyle.Broadleaf),
                     (new Vector3(-12f, 0f, -8f), 7.4f, VegetationStyle.Conifer),
                     (new Vector3(12f, 0f, -13f), 6.8f, VegetationStyle.Birch),
                     (new Vector3(-10f, 0f, -21f), 7.7f, VegetationStyle.Broadleaf),
                     (new Vector3(10f, 0f, -22f), 7.1f, VegetationStyle.Conifer)
                 })
        {
            AddVisualTree(enclosure, "ZiratPerimeterTree", origin + position, height, style);
        }

        foreach (var (position, size, color) in new[]
                 {
                     (new Vector3(-9.5f, 0f, 13.2f), 0.78f, "596047"),
                     (new Vector3(9.4f, 0f, 11.0f), 0.68f, "48553f"),
                     (new Vector3(-9.7f, 0f, 0.6f), 0.92f, "596047"),
                     (new Vector3(9.7f, 0f, -2.4f), 0.76f, "48553f"),
                     (new Vector3(-9.1f, 0f, -14.8f), 0.72f, "596047"),
                     (new Vector3(9.0f, 0f, -17.2f), 0.88f, "48553f")
                 })
        {
            AddVisualShrub(enclosure, "ZiratPerimeterShrub", origin + position, size, color);
        }

        AddDistantHouse(parent, origin + new Vector3(-15f, 0f, -27f), 90f, "ZiratFarHouseSouthWest");
        AddDistantHouse(parent, origin + new Vector3(15f, 0f, -30f), -90f, "ZiratFarHouseSouthEast");
        AddVisualShed(parent, "ZiratCaretakerShed", origin + new Vector3(-13.5f, 0f, 13.0f), 0.68f, 90f, "69746b", "493f36");

        // The village-facing reverse view needs a legible right-side edge as
        // well as the open road. Keep these masses outside the road corridor:
        // they are visual-only low-poly silhouettes, not new traversal or
        // interaction owners.
        AddDistantHouse(parent, origin + new Vector3(12.5f, 0f, 28f), -90f, "ZiratVillageSideHouseEast");
        AddVisualShed(parent, "ZiratVillageSideShedEast", origin + new Vector3(8.4f, 0f, 25.0f), 0.72f, -90f, "69746b", "493f36");
        AddVisualFenceRun(
            parent,
            "ZiratVillageSideFenceEast",
            origin + new Vector3(6.4f, 0f, 21f),
            origin + new Vector3(14.2f, 0f, 31f));
        AddDistantHouse(parent, origin + new Vector3(-12.5f, 0f, 30f), 90f, "ZiratVillageSideHouseWest");
        AddVisualShed(parent, "ZiratVillageSideShedWest", origin + new Vector3(-8.4f, 0f, 25.0f), 0.68f, 90f, "806d58", "4d3e34");
        AddVisualFenceRun(
            parent,
            "ZiratVillageSideFenceWest",
            origin + new Vector3(-6.4f, 0f, 21f),
            origin + new Vector3(-14.2f, 0f, 31f));
        AddVisualTree(parent, "ZiratVillageSideBirchEast", origin + new Vector3(16.0f, 0f, 34f), 6.5f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "ZiratVillageSideBroadleafWest", origin + new Vector3(-14.5f, 0f, 31f), 5.8f, VegetationStyle.Broadleaf, "48553f");
        AddVisualShrub(parent, "ZiratVillageSideShrubEast", origin + new Vector3(13.8f, 0f, 22.5f), 0.82f, "596047");
        AddVisualShrub(parent, "ZiratVillageSideShrubWest", origin + new Vector3(-13.8f, 0f, 22.5f), 0.76f, "48553f");
    }

    private void BuildKaraEdgeFraming(Node3D parent)
    {
        if (!Act1WorldLayout.TryGetPlacement("kara_urman_night", out var placement))
        {
            return;
        }

        var forest = new Node3D { Name = "KaraUrmanDenseEdge" };
        forest.SetMeta("visualOnly", true);
        forest.SetMeta("landmarkRole", "dark forest edge without blocking route");
        parent.AddChild(forest);
        var origin = placement.Origin;

        AddVisualFenceRun(forest, "KaraEdgeFenceWest", origin + new Vector3(-5.6f, 0f, 5f), origin + new Vector3(-5.6f, 0f, -10f));
        AddVisualFenceRun(forest, "KaraEdgeFenceEast", origin + new Vector3(5.6f, 0f, 5f), origin + new Vector3(5.6f, 0f, -10f));

        foreach (var (position, height, style) in new[]
                 {
                     (new Vector3(-5.0f, 0f, 7.5f), 6.8f, VegetationStyle.Conifer),
                     (new Vector3(5.2f, 0f, 6.2f), 6.1f, VegetationStyle.Birch),
                     (new Vector3(-6.8f, 0f, 0.3f), 7.5f, VegetationStyle.Broadleaf),
                     (new Vector3(6.7f, 0f, -1.5f), 7.0f, VegetationStyle.Conifer),
                     (new Vector3(-5.2f, 0f, -7.8f), 8.2f, VegetationStyle.Birch),
                     (new Vector3(5.0f, 0f, -9.0f), 8.5f, VegetationStyle.Broadleaf),
                     (new Vector3(-9.0f, 0f, -14.5f), 9.2f, VegetationStyle.Conifer),
                     (new Vector3(-3.5f, 0f, -16.5f), 7.8f, VegetationStyle.Birch),
                     (new Vector3(4.6f, 0f, -17.2f), 9.4f, VegetationStyle.Conifer),
                     (new Vector3(10.5f, 0f, -19.0f), 8.0f, VegetationStyle.Broadleaf),
                     (new Vector3(-14.5f, 0f, -21.0f), 9.6f, VegetationStyle.Birch),
                     (new Vector3(14.0f, 0f, -23.0f), 8.8f, VegetationStyle.Conifer)
                 })
        {
            AddVisualTree(forest, "KaraUrmanEdgeTree", origin + position, height, style);
        }

        AddAuthoredPine(forest, "KaraEdgePineA_West", origin + new Vector3(-6.8f, 1.45f, -7.6f), 0.92f, -16f);
        AddAuthoredPine(forest, "KaraEdgePineA_East", origin + new Vector3(6.6f, 1.55f, -8.8f), 1.12f, 21f);
        AddAuthoredPine(forest, "KaraEdgePineA_Far", origin + new Vector3(-10.5f, 1.55f, -16.5f), 1.18f, -8f);
    }

    private void AddInteriorLandmark(
        Node3D parent,
        string zoneId,
        string name,
        Vector3 wallSize,
        string wallColor,
        string roofColor)
    {
        if (!Act1WorldLayout.TryGetPlacement(zoneId, out var placement))
        {
            return;
        }

        var landmark = new Node3D
        {
            Name = name,
            Position = placement.Origin
        };
        landmark.SetMeta("visualOnly", true);
        landmark.SetMeta("landmarkForZone", zoneId);
        landmark.SetMeta("hiddenWhenZoneActive", true);
        parent.AddChild(landmark);
        AddVisualBox(landmark, "Wall", wallSize, new(0f, wallSize.Y * 0.5f, 0f), wallColor, "plaster");
        AddVisualBox(
            landmark,
            "Roof",
            new Vector3(wallSize.X + 0.8f, 0.34f, wallSize.Z + 0.8f),
            new(0f, wallSize.Y + 0.28f, 0f),
            roofColor,
            "wood",
            rollDegrees: 16f);
        _interiorLandmarksByZone[zoneId] = landmark;
    }

    private static void AddVisualStrip(
        Node3D parent,
        string name,
        Act1WorldLayout.ConnectorPlacement connector,
        float width,
        float y,
        string surface)
    {
        var start = new Vector3(connector.Start.X, y, connector.Start.Z);
        var end = new Vector3(connector.End.X, y, connector.End.Z);
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length <= 0.05f)
        {
            return;
        }

        var yaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
        var perpendicular = new Vector3(-direction.Z, 0f, direction.X).Normalized();
        var lateralOffset = name.EndsWith("Right", StringComparison.Ordinal)
            ? perpendicular * (connector.Width * 0.5f - 0.08f)
            : name.EndsWith("Left", StringComparison.Ordinal)
                ? perpendicular * (-connector.Width * 0.5f + 0.08f)
                : Vector3.Zero;
        var mesh = AddVisualBox(
            parent,
            name,
            new Vector3(width, 0.035f, length),
            (start + end) * 0.5f + lateralOffset,
            name == "RoadSurface" ? "515952" : "3e4037",
            name == "RoadSurface" ? "earth" : surface);
        mesh.RotationDegrees = new Vector3(0f, yaw, 0f);
    }

    private static void AddVisualFencePosts(Node3D parent, Act1WorldLayout.ConnectorPlacement connector)
    {
        var direction = new Vector3(connector.End.X - connector.Start.X, 0f, connector.End.Z - connector.Start.Z);
        var length = direction.Length();
        if (length <= 8f)
        {
            return;
        }

        var forward = direction.Normalized();
        var perpendicular = new Vector3(-forward.Z, 0f, forward.X);
        var count = Math.Clamp((int)(length / 7f), 2, 8);
        for (var index = 0; index <= count; index++)
        {
            var point = connector.Start.Lerp(connector.End, index / (float)count);
            foreach (var side in new[] { -1f, 1f })
            {
                var post = point + perpendicular * (connector.Width * 0.62f * side);
                AddVisualBox(
                    parent,
                    $"ConnectorFencePost{index}_{(side < 0 ? "L" : "R")}",
                    new Vector3(0.10f, 0.92f, 0.10f),
                    new Vector3(post.X, 0.46f, post.Z),
                    "594a39",
                    "wood");
            }
        }
    }

    private static void AddTraversalStrip(
        Node3D parent,
        Act1WorldLayout.ConnectorPlacement connector,
        float width)
    {
        var start = new Vector3(connector.Start.X, 0f, connector.Start.Z);
        var end = new Vector3(connector.End.X, 0f, connector.End.Z);
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length <= 0.05f)
        {
            return;
        }

        var yaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
        var body = new StaticBody3D
        {
            Name = "RoadTraversalCollision",
            Position = new Vector3((start.X + end.X) * 0.5f, -0.06f, (start.Z + end.Z) * 0.5f),
            RotationDegrees = new Vector3(0f, yaw, 0f)
        };
        body.SetMeta("visualOnly", false);
        body.SetMeta("traversalSurface", true);
        body.SetMeta("collisionOwner", nameof(Act1ConnectedWorld));
        body.SetMeta("interactionOwner", "none");
        body.AddChild(new CollisionShape3D
        {
            Name = "RoadTraversalCollisionShape",
            Shape = new BoxShape3D { Size = new Vector3(width, 0.12f, length) }
        });
        parent.AddChild(body);
    }

    private static void AddTraversalBox(
        Node3D parent,
        string name,
        Vector3 size,
        Vector3 position)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position
        };
        body.SetMeta("visualOnly", false);
        body.SetMeta("traversalSurface", true);
        body.SetMeta("collisionOwner", nameof(Act1ConnectedWorld));
        body.SetMeta("interactionOwner", "none");
        body.AddChild(new CollisionShape3D
        {
            Name = "TraversalCollisionShape",
            Shape = new BoxShape3D { Size = size }
        });
        parent.AddChild(body);
    }

    private static void AddVisualFenceRun(Node3D parent, string name, Vector3 start, Vector3 end)
    {
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length <= 0.05f)
        {
            return;
        }

        var yaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
        var midpoint = (start + end) * 0.5f;
        AddVisualBox(
            parent,
            $"{name}Rail",
            new Vector3(0.12f, 0.10f, length),
            new Vector3(midpoint.X, 0.72f, midpoint.Z),
            "594a39",
            "wood",
            yaw);
        var posts = Math.Clamp((int)(length / 2.8f), 2, 8);
        for (var index = 0; index <= posts; index++)
        {
            var point = start.Lerp(end, index / (float)posts);
            AddVisualBox(
                parent,
                $"{name}Post{index}",
                new Vector3(0.13f, 1.15f, 0.13f),
                new Vector3(point.X, 0.575f, point.Z),
                "594a39",
                "wood");
        }
    }

    private static void AddVisualUtilityPole(Node3D parent, string name, Vector3 origin, float height)
    {
        var pole = new Node3D
        {
            Name = name,
            Position = origin
        };
        pole.SetMeta("visualOnly", true);
        pole.SetMeta("presentationRole", "weathered village utility pole; no collision or interaction");
        parent.AddChild(pole);

        var wood = PainterlyMaterialLibrary.ForColor("493d32", "wood_bark");
        pole.AddChild(new MeshInstance3D
        {
            Name = "PoleShaft",
            Position = new Vector3(0f, height * 0.5f, 0f),
            Mesh = new CylinderMesh
            {
                TopRadius = 0.075f,
                BottomRadius = 0.13f,
                Height = height,
                RadialSegments = 7
            },
            MaterialOverride = wood
        });
        pole.AddChild(new MeshInstance3D
        {
            Name = "PoleCrossbar",
            Position = new Vector3(0f, height - 0.62f, 0f),
            Mesh = new BoxMesh { Size = new Vector3(1.55f, 0.11f, 0.11f) },
            MaterialOverride = wood
        });
        foreach (var x in new[] { -0.54f, 0.0f, 0.54f })
        {
            pole.AddChild(new MeshInstance3D
            {
                Name = "Insulator",
                Position = new Vector3(x, height - 0.48f, 0f),
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.035f,
                    BottomRadius = 0.05f,
                    Height = 0.18f,
                    RadialSegments = 6
                },
                MaterialOverride = wood
            });
        }
    }

    private static void AddVisualCable(Node3D parent, string name, Vector3 start, Vector3 end)
    {
        var direction = end - start;
        if (direction.LengthSquared() <= 0.0001f)
        {
            return;
        }

        var cable = new MeshInstance3D
        {
            Name = name,
            Position = (start + end) * 0.5f,
            Mesh = new CylinderMesh
            {
                TopRadius = 0.016f,
                BottomRadius = 0.016f,
                Height = direction.Length(),
                RadialSegments = 6
            },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("202522")
        };
        cable.SetMeta("visualOnly", true);
        cable.SetMeta("presentationRole", "slack utility wire; no collision or interaction");
        parent.AddChild(cable);
        cable.LookAt(end, Vector3.Up);
        cable.RotateObjectLocal(Vector3.Right, Mathf.Pi * 0.5f);
    }

    private static Node3D AddAuthoredHouse(
        Node3D parent,
        string name,
        Vector3 anchor,
        float uniformScale,
        float yawDegrees)
    {
        var house = GeneratedModularKitDressing.AttachPresentationOnly(
            parent,
            "act1-house-facade",
            ["HouseA_"],
            anchor,
            uniformScale,
            yawDegrees);
        house.Name = name;
        house.SetMeta("presentationRole", "connected-world-authored-house-family");
        house.SetMeta("visualOnly", true);
        return house;
    }

    private static Node3D AddAuthoredPine(
        Node3D parent,
        string name,
        Vector3 anchor,
        float uniformScale,
        float yawDegrees)
    {
        var pine = GeneratedModularKitDressing.AttachPresentationOnly(
            parent,
            "act1-forest-pine",
            ["PineA_"],
            anchor,
            uniformScale,
            yawDegrees);
        pine.Name = name;
        pine.SetMeta("presentationRole", "connected-world-authored-forest-family");
        pine.SetMeta("visualOnly", true);
        return pine;
    }

    private static void AddFapWindow(Node3D parent, string name, Vector3 center)
    {
        AddVisualBox(parent, $"{name}Glass", new(1.45f, 0.95f, 0.06f), center, "6f8079", "glass");
        AddVisualBox(parent, $"{name}FrameTop", new(1.62f, 0.10f, 0.10f), center + new Vector3(0f, 0.54f, 0.04f), "806650", "wood");
        AddVisualBox(parent, $"{name}FrameBottom", new(1.62f, 0.10f, 0.10f), center + new Vector3(0f, -0.54f, 0.04f), "806650", "wood");
        AddVisualBox(parent, $"{name}FrameLeft", new(0.10f, 1.18f, 0.10f), center + new Vector3(-0.81f, 0f, 0.04f), "806650", "wood");
        AddVisualBox(parent, $"{name}FrameRight", new(0.10f, 1.18f, 0.10f), center + new Vector3(0.81f, 0f, 0.04f), "806650", "wood");
        AddVisualBox(parent, $"{name}MullionVertical", new(0.06f, 0.90f, 0.05f), center + new Vector3(0f, 0f, 0.08f), "9d8e72");
        AddVisualBox(parent, $"{name}MullionHorizontal", new(1.34f, 0.06f, 0.05f), center + new Vector3(0f, 0f, 0.08f), "9d8e72");
    }

    private static void AddGraveMarker(Node3D parent, Vector3 position, Vector3 size, float yawDegrees)
    {
        var marker = new Node3D
        {
            Name = "ZiratLowMarker",
            Position = position,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        marker.SetMeta("visualOnly", true);
        marker.SetMeta("culturalPlaceholder", "low marker silhouette; no cross; no inscription");
        parent.AddChild(marker);
        var style = (int)Mathf.Abs(Mathf.Sin(position.X * 1.37f + position.Z * 0.43f) * 10f) % 3;
        switch (style)
        {
            case 0:
                AddVisualBox(marker, "MarkerSlab", size, new(0f, size.Y * 0.5f, 0f), "8d897c", "stone", rollDegrees: -4f);
                break;
            case 1:
                marker.AddChild(new MeshInstance3D
                {
                    Name = "MarkerRounded",
                    Position = new Vector3(0f, size.Y * 0.50f, 0f),
                    Scale = new Vector3(size.X * 0.62f, size.Y * 0.50f, size.Z * 0.62f),
                    Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 8, Rings = 3 },
                    MaterialOverride = PainterlyMaterialLibrary.ForColor("8d897c", "stone")
                });
                break;
            default:
                marker.AddChild(new MeshInstance3D
                {
                    Name = "MarkerPillar",
                    Position = new Vector3(0f, size.Y * 0.50f, 0f),
                    Scale = new Vector3(size.X * 0.62f, size.Y, size.Z * 0.62f),
                    Mesh = new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.52f, Height = 1f, RadialSegments = 7 },
                    MaterialOverride = PainterlyMaterialLibrary.ForColor("8d897c", "stone")
                });
                break;
        }

        AddVisualBox(marker, "MarkerBase", new(size.X + 0.12f, 0.12f, size.Z + 0.10f), new(0f, 0.06f, 0f), "5a5b52", "stone");
    }

    private static Act1WorldLayout.ConnectorPlacement GetConnector(string connectorId)
    {
        foreach (var connector in Act1WorldLayout.Connectors)
        {
            if (string.Equals(connector.ConnectorId, connectorId, StringComparison.Ordinal))
            {
                return connector;
            }
        }

        throw new InvalidOperationException($"Connected Act I layout is missing connector '{connectorId}'.");
    }

    private static Vector3 HorizontalDirection(Vector3 direction)
    {
        direction.Y = 0f;
        return direction.LengthSquared() <= 0.0001f
            ? new Vector3(0f, 0f, 1f)
            : direction.Normalized();
    }

    private static float DirectionYaw(Vector3 direction) =>
        Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));

    private static bool HasTrueMeta(Node node, string key) =>
        node.HasMeta(key) && node.GetMeta(key).AsBool();

    private static bool IsProtectedGameplayNode(Node node) =>
        node is CollisionObject3D
            or CollisionShape3D
            or NavigationRegion3D
            or NavigationLink3D
            or NavigationObstacle3D
            or InteractionTarget
            or RuntimeBridge;

    private static bool IsProtectedLogicalGameplayNode(Node node) =>
        node is NavigationRegion3D
            or NavigationLink3D
            or NavigationObstacle3D
            or InteractionTarget
            or RuntimeBridge
            || HasTrueMeta(node, "traversalSurface")
            || node.HasMeta("collisionOwner")
                && string.Equals(
                    node.GetMeta("collisionOwner").AsString(),
                    nameof(Act1ConnectedWorld),
                    StringComparison.Ordinal);

    private static void HidePresentationNodes(Node root, params string[] names)
    {
        if (!GodotObject.IsInstanceValid(root))
        {
            return;
        }

        var hiddenNames = names.ToHashSet(StringComparer.Ordinal);
        foreach (var node in FindDescendants<Node>(root))
        {
            if (!GodotObject.IsInstanceValid(node))
            {
                continue;
            }

            var nodeName = node.Name.ToString();
            if (hiddenNames.Contains(nodeName)
                || hiddenNames.Any(name => nodeName.StartsWith(name, StringComparison.Ordinal)
                    && nodeName.Length > name.Length
                    && int.TryParse(nodeName[name.Length..], out _)))
            {
                if (TryVerifyPresentationSuppressionTarget(root, node))
                {
                    HidePresentationNode(node);
                }
            }
        }
    }

    private static bool TryVerifyPresentationSuppressionTarget(Node root, Node node)
    {
        if (!GodotObject.IsInstanceValid(root) || !GodotObject.IsInstanceValid(node))
        {
            return false;
        }

        // Never let a presentation whitelist reach a logical zone root or a
        // gameplay owner. InteractionTarget is intentionally present in the
        // Kara list for historical isolation, but remains active here.
        if (node == root
            || IsProtectedLogicalGameplayNode(node)
            || FindDescendants<Node>(node).Any(IsProtectedLogicalGameplayNode)
                && !IsBenchmarkTreeBodyWithDecorativeCollision(root, node))
        {
            return false;
        }

        if (node.HasMeta("visualOnly") && !HasTrueMeta(node, "visualOnly")
            || node.HasMeta("presentationOnly") && !HasTrueMeta(node, "presentationOnly"))
        {
            return false;
        }

        var hasPresentationMetadata = HasTrueMeta(node, "visualOnly")
            || HasTrueMeta(node, "presentationOnly")
            || HasTrueMeta(node, "presentationOnlyInstance")
            || node.HasMeta("presentationOwnership")
                && string.Equals(node.GetMeta("presentationOwnership").AsString(), "presentation-only", StringComparison.Ordinal)
            || node.HasMeta("stylePresentationModule")
                && node.GetMeta("stylePresentationModule").AsString().Length > 0;

        // StyleBenchmarkZone creates a small number of named procedural
        // benchmark props without metadata. They are safe to classify only
        // after the exact connected-world whitelist matched a direct child of
        // that known presentation builder and the protected-node check above
        // passed. Record the verified classification on the candidate before
        // hiding its visual/collision subtree.
        if (!hasPresentationMetadata
            && (root is not StyleBenchmarkZone || node.GetParent() != root))
        {
            return false;
        }

        node.SetMeta("presentationOnly", true);
        node.SetMeta("connectedWorldSuppressionVerified", true);
        return true;
    }

    private static bool IsBenchmarkTreeBodyWithDecorativeCollision(Node root, Node node)
    {
        if (root is not StyleBenchmarkZone
            || node is not StaticBody3D)
        {
            return false;
        }

        var name = node.Name.ToString();
        if (name is not ("Pine" or "Birch")
            && !name.StartsWith("@StaticBody3D@", StringComparison.Ordinal))
        {
            return false;
        }

        var descendants = FindDescendants<Node>(node).ToArray();
        return descendants.OfType<MeshInstance3D>().Count() >= 4
            && descendants.All(child => child is MeshInstance3D or CollisionShape3D);
    }

    private static void HideBenchmarkTreeBodies(Node zone)
    {
        var treeBodies = FindDescendants<StaticBody3D>(zone)
            .Where(body => IsBenchmarkTreeBodyWithDecorativeCollision(zone, body))
            .ToArray();

        foreach (var body in treeBodies)
        {
            if (!TryVerifyPresentationSuppressionTarget(zone, body))
            {
                continue;
            }

            body.SetMeta("suppressionReason", "duplicate benchmark tree family is not part of the connected-world presentation");
            HidePresentationNode(body);
        }

        zone.SetMeta("benchmarkTreePresentationSuppressedCount", treeBodies.Length);
    }

    private static void AddVisualGate(
        Node3D parent,
        string name,
        Vector3 center,
        float width,
        float height,
        float yawDegrees)
    {
        var gate = new Node3D
        {
            Name = name,
            Position = center,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        gate.SetMeta("visualOnly", true);
        gate.SetMeta("presentationRole", "parcel gate; route remains open");
        parent.AddChild(gate);

        AddVisualBox(gate, "PostLeft", new(0.14f, height + 0.32f, 0.14f), new(-width * 0.5f, (height + 0.32f) * 0.5f, 0f), "594a39", "wood");
        AddVisualBox(gate, "PostRight", new(0.14f, height + 0.18f, 0.14f), new(width * 0.5f, (height + 0.18f) * 0.5f, 0f), "594a39", "wood");
        AddVisualBox(gate, "Header", new(width + 0.28f, 0.12f, 0.14f), new(0f, height + 0.24f, 0f), "594a39", "wood");
        AddVisualBox(gate, "Leaf", new(width * 0.86f, height * 0.72f, 0.08f), new(0f, height * 0.42f, 0f), "6d5943", "wood");
        AddVisualBox(gate, "LeafBrace", new(width * 0.70f, 0.08f, 0.10f), new(0f, height * 0.42f, -0.06f), "8a6b50", "wood", rollDegrees: -7f);
    }

    private static void AddVisualShed(
        Node3D parent,
        string name,
        Vector3 anchor,
        float uniformScale,
        float yawDegrees,
        string wallColor,
        string roofColor)
    {
        var shed = new Node3D
        {
            Name = name,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Scale = Vector3.One * uniformScale
        };
        shed.SetMeta("visualOnly", true);
        shed.SetMeta("presentationRole", "near/mid parcel shed silhouette");
        parent.AddChild(shed);

        AddVisualBox(shed, "Wall", new(3.7f, 2.15f, 2.8f), new(0f, 1.08f, 0f), wallColor, "plaster");
        AddVisualBox(shed, "RoofLeft", new(2.2f, 0.26f, 3.25f), new(-0.86f, 2.34f, 0f), roofColor, "wood", rollDegrees: 19f);
        AddVisualBox(shed, "RoofRight", new(2.2f, 0.26f, 3.25f), new(0.86f, 2.34f, 0f), roofColor, "wood", rollDegrees: -19f);
        AddVisualBox(shed, "Door", new(0.92f, 1.58f, 0.08f), new(-0.82f, 0.80f, 1.43f), "4b382c", "wood");
        AddVisualBox(shed, "DoorFrame", new(1.12f, 0.10f, 0.10f), new(-0.82f, 1.68f, 1.47f), "6d5845", "wood");
        AddVisualBox(shed, "LeanTo", new(1.65f, 0.12f, 0.84f), new(1.28f, 1.22f, 1.25f), roofColor, "wood", rollDegrees: 8f);
    }

    private static void AddVisualTree(
        Node3D parent,
        string name,
        Vector3 origin,
        float height,
        VegetationStyle style,
        string foliageColor = "")
    {
        switch (style)
        {
            case VegetationStyle.Conifer:
                AddVisualConifer(parent, name, origin, height, foliageColor.Length == 0 ? "30483f" : foliageColor);
                break;
            case VegetationStyle.Birch:
                AddVisualBirch(parent, name, origin, height, foliageColor.Length == 0 ? "596047" : foliageColor);
                break;
            case VegetationStyle.Broadleaf:
                AddVisualBroadleaf(parent, name, origin, height, foliageColor.Length == 0 ? "48553f" : foliageColor);
                break;
            default:
                AddVisualShrub(parent, name, origin, Mathf.Clamp(height * 0.16f, 0.5f, 1.1f), foliageColor.Length == 0 ? "48553f" : foliageColor);
                break;
        }
    }

    private static void AddVisualConifer(Node3D parent, string name, Vector3 origin, float height, string foliageColor)
    {
        var phase = VegetationHash(origin, 0.7f);
        var lean = Mathf.Lerp(-3.5f, 3.5f, VegetationHash(origin, 1.7f));
        var tree = new Node3D
        {
            Name = name,
            Position = origin,
            RotationDegrees = new Vector3(lean * 0.35f, VegetationHash(origin, 2.3f) * 360f, lean)
        };
        tree.SetMeta("visualOnly", true);
        tree.SetMeta("vegetationStyle", "irregular-low-poly-conifer");
        parent.AddChild(tree);

        var trunkWidth = Mathf.Lerp(0.17f, 0.25f, phase);
        AddVisualBox(tree, "Trunk", new(trunkWidth, height * 0.52f, trunkWidth), new(0f, height * 0.26f, 0f), "40352d", "wood_bark");
        var foliageLobes = new[]
        {
            (Height: 0.44f, Width: 0.30f, Depth: 0.21f, Thickness: 0.095f, Side: -0.06f, Front: 0.03f),
            (Height: 0.60f, Width: 0.26f, Depth: 0.19f, Thickness: 0.085f, Side: 0.08f, Front: -0.04f),
            (Height: 0.75f, Width: 0.22f, Depth: 0.16f, Thickness: 0.075f, Side: -0.04f, Front: 0.05f),
            (Height: 0.88f, Width: 0.15f, Depth: 0.12f, Thickness: 0.065f, Side: 0.07f, Front: -0.02f)
        };
        for (var index = 0; index < foliageLobes.Length; index++)
        {
            var lobe = foliageLobes[index];
            var lobePhase = VegetationHash(origin, 3.1f + index);
            var depthPhase = VegetationHash(origin, 4.7f + index);
            tree.AddChild(new MeshInstance3D
            {
                Name = $"Crown{index}",
                Position = new Vector3(
                    height * (lobe.Side + Mathf.Lerp(-0.045f, 0.045f, lobePhase)),
                    height * (lobe.Height + Mathf.Lerp(-0.015f, 0.015f, phase)),
                    height * (lobe.Front + Mathf.Lerp(-0.045f, 0.045f, depthPhase))),
                Scale = new Vector3(
                    height * lobe.Width * Mathf.Lerp(0.88f, 1.16f, lobePhase),
                    height * lobe.Thickness * Mathf.Lerp(0.88f, 1.14f, phase),
                    height * lobe.Depth * Mathf.Lerp(0.78f, 1.12f, depthPhase)),
                RotationDegrees = new Vector3(
                    Mathf.Lerp(-12f, 12f, lobePhase),
                    lobePhase * 80f + index * 43f,
                    Mathf.Lerp(-16f, 16f, depthPhase)),
                Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = lobePhase > 0.5f ? 6 : 7, Rings = 3 },
                MaterialOverride = PainterlyMaterialLibrary.ForColor(foliageColor, "foliage")
            });
        }

        tree.AddChild(new MeshInstance3D
        {
            Name = "TopSprig",
            Position = new Vector3(height * Mathf.Lerp(-0.035f, 0.035f, phase), height * 0.98f, height * Mathf.Lerp(-0.02f, 0.02f, phase)),
            Scale = new Vector3(height * Mathf.Lerp(0.065f, 0.09f, phase), height * Mathf.Lerp(0.11f, 0.15f, phase), height * 0.07f),
            Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.65f, Height = 1f, RadialSegments = 6 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(foliageColor, "foliage")
        });

        AddVegetationGroundAccents(tree, name, origin, Mathf.Clamp(height * 0.065f, 0.24f, 0.52f), phase);
    }

    private static void AddVisualBirch(Node3D parent, string name, Vector3 origin, float height, string foliageColor)
    {
        var phase = VegetationHash(origin, 15.1f);
        var tree = new Node3D
        {
            Name = name,
            Position = origin,
            RotationDegrees = new Vector3(
                0f,
                VegetationHash(origin, 17.3f) * 360f,
                Mathf.Lerp(-3f, 3f, VegetationHash(origin, 16.7f)))
        };
        tree.SetMeta("visualOnly", true);
        tree.SetMeta("vegetationStyle", "painterly-birch-cluster");
        parent.AddChild(tree);
        var trunkWidth = Mathf.Lerp(0.11f, 0.17f, phase);
        tree.AddChild(new MeshInstance3D
        {
            Name = "Trunk",
            Position = new Vector3(Mathf.Lerp(-0.04f, 0.04f, phase), height * 0.42f, 0f),
            Mesh = new CylinderMesh { TopRadius = trunkWidth * 0.54f, BottomRadius = trunkWidth, Height = height * 0.84f, RadialSegments = 7 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("8d8f80", "wood_bark")
        });
        var branchPhase = VegetationHash(origin, 18.9f);
        AddVisualBox(tree, "BranchLeft", new(0.08f, height * Mathf.Lerp(0.24f, 0.32f, branchPhase), 0.08f), new(-height * Mathf.Lerp(0.07f, 0.14f, branchPhase), height * 0.57f, 0.01f), "8d8f80", "wood_bark", yawDegrees: branchPhase * 36f, rollDegrees: Mathf.Lerp(-34f, -24f, branchPhase));
        AddVisualBox(tree, "BranchRight", new(0.07f, height * Mathf.Lerp(0.20f, 0.29f, phase), 0.07f), new(height * Mathf.Lerp(0.08f, 0.15f, phase), height * 0.63f, -height * 0.025f), "8d8f80", "wood_bark", yawDegrees: 180f - branchPhase * 42f, rollDegrees: Mathf.Lerp(25f, 38f, phase));
        var foliage = PainterlyMaterialLibrary.ForColor(foliageColor, "foliage");
        var clusters = new[]
                 {
                     (new Vector3(-0.28f, height * 0.70f, 0.05f), new Vector3(0.72f, 0.56f, 0.64f)),
                     (new Vector3(0.24f, height * 0.78f, -0.12f), new Vector3(0.68f, 0.60f, 0.58f)),
                     (new Vector3(0.02f, height * 0.93f, 0.10f), new Vector3(0.54f, 0.48f, 0.50f))
                 };
        for (var index = 0; index < clusters.Length; index++)
        {
            var (offset, scale) = clusters[index];
            var clusterPhase = VegetationHash(origin, 20.1f + index);
            tree.AddChild(new MeshInstance3D
            {
                Name = $"LeafCluster{index}",
                Position = offset + new Vector3(
                    Mathf.Lerp(-0.06f, 0.06f, clusterPhase),
                    Mathf.Lerp(-0.025f, 0.025f, phase),
                    Mathf.Lerp(-0.05f, 0.05f, VegetationHash(origin, 23.1f + index))),
                Scale = new Vector3(
                    height * scale.X * Mathf.Lerp(0.18f, 0.23f, clusterPhase),
                    height * scale.Y * Mathf.Lerp(0.18f, 0.23f, phase),
                    height * scale.Z * Mathf.Lerp(0.18f, 0.22f, clusterPhase)),
                RotationDegrees = new Vector3(0f, clusterPhase * 42f, Mathf.Lerp(-5f, 5f, phase)),
                Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = clusterPhase > 0.5f ? 7 : 8, Rings = 4 },
                MaterialOverride = foliage
            });
        }

        AddVegetationGroundAccents(tree, name, origin, Mathf.Clamp(height * 0.055f, 0.20f, 0.42f), phase);
    }

    private static void AddVisualBroadleaf(Node3D parent, string name, Vector3 origin, float height, string foliageColor)
    {
        var phase = VegetationHash(origin, 27.1f);
        var tree = new Node3D
        {
            Name = name,
            Position = origin,
            RotationDegrees = new Vector3(
                0f,
                VegetationHash(origin, 29.3f) * 360f,
                Mathf.Lerp(-4.5f, 4.5f, VegetationHash(origin, 28.7f)))
        };
        tree.SetMeta("visualOnly", true);
        tree.SetMeta("vegetationStyle", "painterly-broadleaf-crown");
        parent.AddChild(tree);
        var trunkWidth = Mathf.Lerp(0.16f, 0.25f, phase);
        tree.AddChild(new MeshInstance3D
        {
            Name = "Trunk",
            Position = new Vector3(Mathf.Lerp(-0.06f, 0.06f, phase), height * 0.30f, 0f),
            RotationDegrees = new Vector3(0f, 0f, Mathf.Lerp(-6f, 4f, phase)),
            Mesh = new CylinderMesh { TopRadius = trunkWidth * 0.50f, BottomRadius = trunkWidth, Height = height * Mathf.Lerp(0.58f, 0.68f, phase), RadialSegments = 7 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("594438", "wood_bark")
        });
        AddVisualBox(tree, "BranchLeft", new(0.10f, height * Mathf.Lerp(0.34f, 0.45f, phase), 0.10f), new(-height * Mathf.Lerp(0.09f, 0.15f, phase), height * 0.57f, 0.01f), "594438", "wood_bark", yawDegrees: VegetationHash(origin, 31.1f) * 30f, rollDegrees: Mathf.Lerp(-38f, -25f, phase));
        AddVisualBox(tree, "BranchRight", new(0.09f, height * Mathf.Lerp(0.29f, 0.40f, phase), 0.09f), new(height * Mathf.Lerp(0.10f, 0.17f, phase), height * 0.62f, -height * 0.02f), "594438", "wood_bark", yawDegrees: 180f - VegetationHash(origin, 32.1f) * 30f, rollDegrees: Mathf.Lerp(27f, 42f, phase));
        var foliage = PainterlyMaterialLibrary.ForColor(foliageColor, "foliage");
        var clusters = new[]
                 {
                     (new Vector3(-0.22f, height * 0.68f, 0.06f), new Vector3(0.66f, 0.54f, 0.62f)),
                     (new Vector3(0.23f, height * 0.73f, -0.08f), new Vector3(0.70f, 0.58f, 0.66f)),
                     (new Vector3(-0.02f, height * 0.88f, 0.02f), new Vector3(0.58f, 0.52f, 0.60f))
                 };
        for (var index = 0; index < clusters.Length; index++)
        {
            var (offset, scale) = clusters[index];
            var clusterPhase = VegetationHash(origin, 34.1f + index);
            tree.AddChild(new MeshInstance3D
            {
                Name = $"LeafMass{index}",
                Position = offset + new Vector3(
                    Mathf.Lerp(-0.08f, 0.08f, clusterPhase),
                    Mathf.Lerp(-0.03f, 0.03f, phase),
                    Mathf.Lerp(-0.06f, 0.06f, VegetationHash(origin, 37.1f + index))),
                Scale = new Vector3(
                    height * scale.X * Mathf.Lerp(0.19f, 0.25f, clusterPhase),
                    height * scale.Y * Mathf.Lerp(0.19f, 0.25f, phase),
                    height * scale.Z * Mathf.Lerp(0.19f, 0.24f, clusterPhase)),
                RotationDegrees = new Vector3(Mathf.Lerp(-4f, 4f, phase), clusterPhase * 48f, Mathf.Lerp(-6f, 6f, clusterPhase)),
                Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = clusterPhase > 0.58f ? 6 : 7, Rings = 3 },
                MaterialOverride = foliage
            });
        }
        AddVegetationGroundAccents(tree, name, origin, Mathf.Clamp(height * 0.07f, 0.26f, 0.54f), phase);
    }

    private static void AddVisualShrub(Node3D parent, string name, Vector3 origin, float size, string foliageColor)
    {
        var phase = VegetationHash(origin, 41.1f);
        var shrub = new Node3D
        {
            Name = name,
            Position = origin,
            RotationDegrees = new Vector3(0f, VegetationHash(origin, 42.7f) * 360f, 0f)
        };
        shrub.SetMeta("visualOnly", true);
        shrub.SetMeta("vegetationStyle", "low-poly-shrub-cluster");
        parent.AddChild(shrub);
        var material = PainterlyMaterialLibrary.ForColor(foliageColor, "foliage");
        var spread = Mathf.Lerp(0.84f, 1.20f, phase);
        var clusters = new[]
                 {
                     (new Vector3(-0.28f, 0.28f, 0.04f), new Vector3(0.54f, 0.38f, 0.48f)),
                     (new Vector3(0.24f, 0.34f, -0.12f), new Vector3(0.48f, 0.42f, 0.52f)),
                     (new Vector3(0.02f, 0.47f, 0.16f), new Vector3(0.42f, 0.36f, 0.40f))
                 };
        for (var index = 0; index < clusters.Length; index++)
        {
            var (offset, scale) = clusters[index];
            var clusterPhase = VegetationHash(origin, 43.1f + index);
            shrub.AddChild(new MeshInstance3D
            {
                Name = $"LeafMass{index}",
                Position = new Vector3(
                    offset.X * size * spread + Mathf.Lerp(-0.05f, 0.05f, clusterPhase) * size,
                    offset.Y * size * Mathf.Lerp(0.88f, 1.16f, phase),
                    offset.Z * size * spread + Mathf.Lerp(-0.05f, 0.05f, VegetationHash(origin, 46.1f + index)) * size),
                Scale = new Vector3(
                    scale.X * size * Mathf.Lerp(0.88f, 1.18f, clusterPhase),
                    scale.Y * size * Mathf.Lerp(0.88f, 1.16f, phase),
                    scale.Z * size * Mathf.Lerp(0.88f, 1.16f, clusterPhase)),
                RotationDegrees = new Vector3(0f, clusterPhase * 50f, Mathf.Lerp(-8f, 8f, phase)),
                Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = clusterPhase > 0.5f ? 7 : 8, Rings = clusterPhase > 0.7f ? 3 : 4 },
                MaterialOverride = material
            });
        }
        AddVegetationGroundAccents(shrub, name, origin, Mathf.Clamp(size * 0.72f, 0.22f, 0.72f), phase);
    }

    private static float VegetationHash(Vector3 origin, float salt)
    {
        var value = Mathf.Sin(origin.X * 12.9898f + origin.Z * 78.233f + salt * 37.719f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private static void AddVegetationGroundAccents(Node3D parent, string name, Vector3 origin, float size, float phase)
    {
        if (phase > 0.34f)
        {
            var mossPhase = VegetationHash(origin, 51.1f);
            var moss = new MeshInstance3D
            {
                Name = $"{name}MossBase",
                Position = new Vector3(
                    Mathf.Lerp(-size * 0.22f, size * 0.22f, mossPhase),
                    size * 0.10f,
                    Mathf.Lerp(-size * 0.24f, size * 0.24f, VegetationHash(origin, 52.7f))),
                Scale = new Vector3(size * Mathf.Lerp(0.72f, 1.12f, mossPhase), size * 0.16f, size * Mathf.Lerp(0.42f, 0.68f, mossPhase)),
                RotationDegrees = new Vector3(0f, mossPhase * 180f, Mathf.Lerp(-10f, 10f, phase)),
                Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 6, Rings = 2 },
                MaterialOverride = PainterlyMaterialLibrary.ForColor(mossPhase > 0.5f ? "53634e" : "465a43", "foliage")
            };
            moss.SetMeta("visualOnly", true);
            moss.SetMeta("vegetationStyle", "low-poly-moss-contact");
            parent.AddChild(moss);
        }

        if (phase < 0.68f)
        {
            return;
        }

        var sedge = new Node3D { Name = $"{name}Sedge", Position = new Vector3(0f, 0f, size * 0.12f) };
        sedge.SetMeta("visualOnly", true);
        sedge.SetMeta("vegetationStyle", "low-poly-sedge-accent");
        parent.AddChild(sedge);
        var bladeCount = VegetationHash(origin, 53.9f) > 0.55f ? 3 : 2;
        for (var index = 0; index < bladeCount; index++)
        {
            var bladePhase = VegetationHash(origin, 55.1f + index);
            var bladeHeight = size * Mathf.Lerp(0.78f, 1.35f, bladePhase);
            AddVisualBox(
                sedge,
                $"Blade{index}",
                new(0.035f * size, bladeHeight, 0.055f * size),
                new(
                    Mathf.Lerp(-size * 0.48f, size * 0.48f, bladePhase),
                    bladeHeight * 0.5f,
                    Mathf.Lerp(-size * 0.18f, size * 0.28f, VegetationHash(origin, 58.1f + index))),
                bladePhase > 0.5f ? "59634a" : "4e5d48",
                "foliage",
                yawDegrees: Mathf.Lerp(-28f, 32f, bladePhase),
                rollDegrees: index % 2 == 0 ? -14f : 11f);
        }
    }

    private static void HidePresentationNode(Node node)
    {
        if (!GodotObject.IsInstanceValid(node))
        {
            return;
        }

        node.SetMeta("connectedWorldHidden", true);
        if (node is VisualInstance3D visual)
        {
            visual.Visible = false;
        }

        if (node is CollisionObject3D collisionObject)
        {
            collisionObject.CollisionLayer = 0;
            collisionObject.CollisionMask = 0;
        }

        if (node is CollisionShape3D collisionShape)
        {
            collisionShape.Disabled = true;
        }

        foreach (var child in node.GetChildren())
        {
            if (GodotObject.IsInstanceValid(child)
                && !IsProtectedLogicalGameplayNode(child))
            {
                HidePresentationNode(child);
            }
        }
    }

    private static void AddDistantHouse(Node3D parent, Vector3 origin, float yawDegrees, string name)
    {
        // Far houses use the same authored HouseA family as the near parcels.
        // Keep one restrained scale so every call site gets a coherent
        // silhouette, LOD fade and presentation-only ownership contract.
        var house = AddAuthoredHouse(parent, name, origin, 0.32f, yawDegrees);
        house.SetMeta("presentationRole", "varied distant village house silhouette");
    }

    private static void AddVisualPine(Node3D parent, Vector3 origin, float height)
    {
        var phase = (int)Mathf.Abs(Mathf.Sin(origin.X * 1.31f + origin.Z * 0.77f) * 1000f) % 5;
        var style = phase switch
        {
            0 => VegetationStyle.Birch,
            1 => VegetationStyle.Broadleaf,
            _ => VegetationStyle.Conifer
        };
        AddVisualTree(parent, "DistantVillageTree", origin, height, style);
    }

    private static MeshInstance3D AddVisualBox(
        Node3D parent,
        string name,
        Vector3 size,
        Vector3 position,
        string color,
        string surface = "",
        float yawDegrees = 0f,
        float rollDegrees = 0f)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = position,
            RotationDegrees = new Vector3(0f, yawDegrees, rollDegrees),
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface)
        };
        mesh.SetMeta("visualOnly", true);
        parent.AddChild(mesh);
        return mesh;
    }

    private static MeshInstance3D AddVisualPitchedRoof(
        Node3D parent,
        string name,
        float width,
        float depth,
        float wallHeight,
        float ridgeRise,
        float overhang,
        string color)
    {
        var roof = new MeshInstance3D
        {
            Name = name,
            Mesh = BuildPitchedRoofMesh(width, depth, wallHeight, ridgeRise, overhang),
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "wood")
        };
        roof.SetMeta("visualOnly", true);
        roof.SetMeta("geometryRole", "authored low-poly pitched roof; no collision ownership");
        parent.AddChild(roof);
        return roof;
    }

    private static ArrayMesh BuildPitchedRoofMesh(
        float width,
        float depth,
        float wallHeight,
        float ridgeRise,
        float overhang)
    {
        var halfWidth = width * 0.5f + overhang;
        var halfDepth = depth * 0.5f + overhang;
        var eaveY = wallHeight + 0.18f;
        var ridgeY = eaveY + ridgeRise;
        var fasciaY = eaveY - 0.16f;
        var leftFront = new Vector3(-halfWidth, eaveY, -halfDepth);
        var leftBack = new Vector3(-halfWidth, eaveY, halfDepth);
        var rightFront = new Vector3(halfWidth, eaveY, -halfDepth);
        var rightBack = new Vector3(halfWidth, eaveY, halfDepth);
        var ridgeFront = new Vector3(0f, ridgeY, -halfDepth);
        var ridgeBack = new Vector3(0f, ridgeY, halfDepth);
        var leftFrontBottom = new Vector3(-halfWidth, fasciaY, -halfDepth);
        var leftBackBottom = new Vector3(-halfWidth, fasciaY, halfDepth);
        var rightFrontBottom = new Vector3(halfWidth, fasciaY, -halfDepth);
        var rightBackBottom = new Vector3(halfWidth, fasciaY, halfDepth);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            if ((b - a).Cross(c - a).Dot(normal) < 0f)
            {
                (b, c) = (c, b);
            }

            var start = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            normals.AddRange(new[] { normal, normal, normal, normal });
            uvs.AddRange(new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            });
            indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            if ((b - a).Cross(c - a).Dot(normal) < 0f)
            {
                (b, c) = (c, b);
            }

            var start = vertices.Count;
            vertices.AddRange(new[] { a, b, c });
            normals.AddRange(new[] { normal, normal, normal });
            uvs.AddRange(new[] { new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0f) });
            indices.AddRange(new[] { start, start + 1, start + 2 });
        }

        var leftNormal = new Vector3(-ridgeRise / Mathf.Max(halfWidth, 0.01f), 1f, 0f).Normalized();
        var rightNormal = new Vector3(ridgeRise / Mathf.Max(halfWidth, 0.01f), 1f, 0f).Normalized();
        AddQuad(leftFront, leftBack, ridgeBack, ridgeFront, leftNormal);
        AddQuad(ridgeFront, ridgeBack, rightBack, rightFront, rightNormal);
        AddTriangle(leftFront, ridgeFront, rightFront, Vector3.Back);
        AddTriangle(leftBack, rightBack, ridgeBack, Vector3.Forward);
        AddQuad(leftFront, leftFrontBottom, leftBackBottom, leftBack, Vector3.Left);
        AddQuad(rightFront, rightBack, rightBackBottom, rightFrontBottom, Vector3.Right);
        AddQuad(leftFrontBottom, rightFrontBottom, rightBackBottom, leftBackBottom, Vector3.Down);

        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static IEnumerable<T> FindDescendants<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }
}
