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

    // Authored kit families whose bulk volume must stop the player. Open
    // passages (gates, porches, doors, steps) are excluded by name below.
    //
    // The three parcel variants have to be listed separately from
    // "VillageParcel_": a parcel mounts as <Variant>/<Variant>_Dwelling, so its
    // walls, shed and yard rails are named after the variant
    // (VariantA_TimberGable_Dwelling_Street_Wall_LOD0), and only the small
    // composition dressing keeps the VillageParcel_ prefix. That mismatch is why
    // every parcel dwelling in the village was walk-through while the standalone
    // facade family was not.
    private static readonly string[] AuthoredKitBlockerFamilies =
    [
        "DwellingFacade_",
        "OutbuildingShed_",
        "FenceSegment_",
        "Woodpile_",
        "Well_",
        "VillageParcel_",
        "VariantA_TimberGable_",
        "VariantB_PlasterAnnex_",
        "VariantC_BanyaYard_",
        "VariantA_Yard_",
        "VariantB_Yard_",
        "VariantC_Yard_",
        "BanyaYard",
        "FapFacade_",
        "FapService",
        "CulvertStoneCrossing",
        "RoadFenceBreak_",
        "FapAuthoredServiceShed"
    ];

    private static readonly string[] AuthoredKitClearanceParts =
    [
        "Porch",
        "Step",
        "Awning",
        "Door",
        "Gate",
        "Lamp",
        "Sign",
        "Wayfinding",
        "Window",
        "Canopy"
    ];

    private static readonly string[] VillageExteriorKitComponentNames =
    [
        "DwellingFacade_TimberPlaster",
        "OutbuildingShed_Low",
        "FenceSegment_RoughPicket",
        "Gate_CrookedTimber",
        "Woodpile_StackedLogs",
        "Well_YardLandmark",
        "VillageParcel_VariantA_TimberGable",
        "VillageParcel_VariantB_PlasterAnnex",
        "VillageParcel_VariantC_BanyaYard",
        StyleBenchmarkInteriorFactory.ExteriorComponent
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
    private CarryCoordinator? _carryCoordinator;
    private bool _logicalZonePresentationSuppressionsReapplied;
    private bool _buildStarted;
    // Single-build diagnostics for the authored parcel grounding pass, which runs
    // from a static mount helper. Reset at the start of every blocker pass so a
    // second build in the same process cannot inherit the first one's numbers.
    private static int _dwellingThresholdPlacements;
    private static float _dwellingThresholdWorst;
    private static string _dwellingThresholdWorstPlacement = string.Empty;
    private Node3D? _villageLife;
    private Node3D? _yardCat;
    private Node3D[] _crows = [];
    private CpuParticles3D[] _chimneySmoke = [];
    private (Node3D Node, Basis RestBasis, float Phase)[] _catLegs = [];
    private (Node3D Node, Basis RestBasis, float Side)[] _birdWings = [];
    private Skeleton3D? _residentSkeleton;
    private (int Bone, Quaternion Rotation)[] _residentRest = [];
    private readonly RandomNumberGenerator _lifeRandom = new();
    private FirstPersonController? _lifePlayer;
    private AudioCueUi? _lifeCue;
    private float _lifeWait = 28f;
    private float _lifeTime;
    private int _lifeEvent;
    private int _lastLifeEvent;
    private bool _catAtEastEnd;
    private float _catStartYaw;
    private Vector3 _catStart;
    private Vector3 _catEnd;


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

    public bool IsBuilt { get; private set; }

    public int ActiveDirectionalLightCount { get; private set; }

    public int ActiveLightCount { get; private set; }

    public int ActiveInteractionTargetCount { get; private set; }

    public int ConnectorTraversalCollisionCount { get; private set; }

    public override void _Ready()
    {
        AddToGroup("act1_connected_world");
        Build();
    }

    /// <summary>
    /// Generated node names become resolved NodePaths: they live in metas such as
    /// <c>authoredSourceMesh</c>, in saves and in later <c>GetNode</c> lookups. A
    /// host locale with a comma decimal separator would format
    /// <c>$"MosqueWindowPier{side}_{4.4f}"</c> as <c>Pier1_4,4</c> instead of the
    /// published <c>Pier1_4_4</c>, so construction formats names invariantly and
    /// restores the player's culture for everything else.
    /// </summary>
    internal static IDisposable InvariantNameScope() => new NamingCultureScope();

    private sealed class NamingCultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        internal NamingCultureScope() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
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
        if (_buildStarted)
        {
            return;
        }

        using var naming = InvariantNameScope();
        _buildStarted = true;
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

            if (zone is StyleBenchmarkZone village && placement.ZoneId == "village_day")
                village.MakeInteractionBox("TimurHazratNpc", new(.65f, 1.8f, .5f), new(-3.8f, .9f, -19f),
                    "506058", "urman.chapter1:interaction/route-to-mosque", "Поговорить с Тимуром хәзрәтом",
                    dialogueId: "urman.chapter1:dialogue/timur_restraint");

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
        }

        BuildConnectorPresentation();
        // Keep the legacy framing builders available as rollback/source code,
        // but do not materialize their global presentation root in the main
        // connected-world path. Act1CoreWorldGreybox is the sole global visual
        // owner; route, collision, interaction and RuntimeBridge owners above
        // remain unchanged.
        _dwellingThresholdPlacements = 0;
        _dwellingThresholdWorst = 0f;
        _dwellingThresholdWorstPlacement = string.Empty;
        BuildAct1CoreWorldGreybox();
        AlignFapClinicArchitecture();
        GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld")
            .ExcludeOccupiedRoomTerrain(_zoneInstances["house_old_pc"], new Vector2(
                StyleBenchmarkInteriorFactory.ClearWidth * .5f,
                StyleBenchmarkInteriorFactory.ClearDepth * .5f));
        BuildAct1NpcStaging();
        BuildAct1InteriorDiscoveries();
        BuildAct1ExteriorDiscoveries();
        BuildArrivalBusStop();
        BuildFapServiceExploration();
        BuildRoadsideDiscoveries();
        BuildAct1YardDiscoveries();
        BuildAct1LanternDetails();
        BuildAct1QuietCareDiscoveries();
        BuildAct1OptionalDiscoveries();
        BuildAct1CulvertVerandaDiscoveries();
        BuildBabaiYardSideGateExploration();
        BuildCarryables(GetNode<Node3D>("Act1CoreWorldGreybox"));
        BuildAct1KaraOptionalDiscoveries();
        BuildAct1BypassDiscoveries();
        BuildAct1ImageDiscoveries();
        BuildRinatRoadsidePresentation();
        ConfigureInvestigationRevisits();
        BuildMosqueInterior();
        BuildBathhouse();
        BuildPublicBuildings();
        RepairStandaloneZiratFenceJunction();
        BuildShopUses();
        HideOverlappingStructures();
        RelocateOverlappingHouses();
        ComposeBabaiYard();
        BuildAddressRegistry();
        // Street faces of the yards: palisadnik, painted gates, board fences.
        BuildStreetFrontages();
        AddressRead += RememberReadAddress;
        BuildAct1Vehicles();
        foreach (var placement in Act1WorldLayout.Placements)
        {
            var zone = _zoneInstances[placement.ZoneId];
            _interactionsByZone.Add(
                placement.ZoneId,
                FindDescendants<InteractionTarget>(zone)
                    .Where(target => !target.HasMeta("connectedWorldHidden"))
                    .Select(target => new InteractionBinding(
                        target,
                        // Availability may already have set the live layer to zero.
                        // Restore the authored ray/physics layer, not a layer-1 guess.
                        target.ActiveCollisionLayer,
                        target.CollisionMask))
                    .ToArray());
        }
        AttachRuntimeBridge();
        UpdateAct1NpcStaging();
        UpdateAct1Discoveries();
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
        // Blockers and the hidden-presentation audit both run after mounting and
        // after the first zone's suppressions, so nothing hidden can block.
        BuildAuthoredKitBlockers();
        FinalizeStandaloneZiratFenceContacts();
        FinalizeFacilityContacts();
        FinalizePublicBuildingContacts();
        FinalizeArrivalBusStop();
        BuildZiratFamily();
        CallDeferred(nameof(DisableBlockersUnderHiddenPresentation));
        CallDeferred(nameof(ReapplyLogicalZonePresentationSuppressions));
        // Parcel facades are attached across several build steps, so the painted
        // window surrounds are applied once the frame's construction is finished.
        CallDeferred(nameof(DressDeferredPaintedWindowSurrounds));
        // Interaction boxes are created by the zone scripts, so this runs after
        // the frame is built rather than in the one-shot suppression pass.
        CallDeferred(nameof(SuppressLegacySignInteraction));
        IsBuilt = true;
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
        if ((zoneId is "house_old_pc" or "fap_clinic") && _zoneInstances.TryGetValue(zoneId, out var house)
            && Act1WorldLayout.TryGetPlacement(zoneId, out var housePlacement)
            && (housePlacement.SpawnPoints.TryGetValue(spawnPointId, out var houseSpawn)
                || housePlacement.SpawnPoints.TryGetValue("default", out houseSpawn)))
        {
            worldSpawn = new(house.ToGlobal(houseSpawn.Position), house.GlobalRotationDegrees.Y + houseSpawn.YawDegrees);
            return true;
        }
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
        if (!_buildStarted)
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
        var isKaraNight = string.Equals(zoneId, "kara_urman_night", StringComparison.Ordinal);
        var isZirat = string.Equals(zoneId, "zirat_road", StringComparison.Ordinal);
        exteriorLayer?.SetExteriorPresentationEnabled(
            useExteriorAtmosphere,
            isKaraNight);
        var coreWorld = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox");
        if (coreWorld is not null)
        {
            // The windows look into the same village. Interior atmosphere and
            // collision remain owned separately from this exterior scenery.
            coreWorld.Visible = useExteriorAtmosphere || zoneId is "house_old_pc" or "fap_clinic";
            SetAuthoredKitCollisionEnabled(useExteriorAtmosphere, zoneId);
            TuneConnectedAct1Atmosphere(coreWorld, useExteriorAtmosphere, isKaraNight, isZirat);
        }

        ActiveZoneId = zoneId;
        _carryCoordinator?.SetZonePresentation(zoneId, useExteriorAtmosphere);
        VehicleFleet?.SetZonePresentation(zoneId, useExteriorAtmosphere);
        AlsuStreetWalkPresentation.SessionOwner(GetTree())?.SetZonePresentation(zoneId, useExteriorAtmosphere);
        SetMeta("activeZoneId", ActiveZoneId);
        SetMeta(
            "activeWorldEnvironmentCount",
            useExteriorAtmosphere ? 0 : _environmentsByZone[zoneId].Count);
        SetMeta("activeExteriorAtmosphere", useExteriorAtmosphere);
        GetNodeOrNull<AddressAccessVerifier>("AddressAccessVerification")?.NotifyPresentationChanged();
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

        // Capture the clinic's static reflection only after its actual room,
        // lights and environment are active. The same village is visible through
        // its glazed openings; the exterior contacts remain separately disabled.
        ClinicSurfacePresentation.SetClinicActive(_zoneInstances["fap_clinic"], zoneId == "fap_clinic");
        UpdateAct1Discoveries();
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
        _carryCoordinator?.AttachRuntimeState(bridge);
    }

    private void OnRuntimeStateChanged()
    {
        UpdateAct1NpcStaging();
        UpdateAct1Discoveries();
        ApplyInteractionRouting();
        _carryCoordinator?.ApplyWorldState();
    }

    private void ApplyInteractionRouting()
    {
        ActiveInteractionTargetCount = 0;
        foreach (var (candidateZoneId, bindings) in _interactionsByZone)
        {
            var isActiveZone = string.Equals(candidateZoneId, ActiveZoneId, StringComparison.Ordinal);
            foreach (var binding in bindings)
            {
                // A suppressed legacy target keeps layer 0 for good: this pass
                // re-applies the captured layer on every scene change, so a
                // one-shot suppression is undone the next time the zone routes.
                var enabled = !binding.Node.HasMeta("legacySignSuppression")
                    && (isActiveZone || _runtimeBridge?.IsWorldInteraction(binding.Node.InteractionId) == true)
                    // Routing follows authored availability: a shut world gate
                    // must keep the target aimable, otherwise the player never
                    // learns why the place cannot be read yet.
                    && binding.Node.IsSemanticallyAvailable();
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

        // Isolated wet-season studies place puddle proxies over their flat
        // roads. The connected winter heightfield owns those surfaces now.
        if (zoneId is "village_day" or "zirat_road" or "kara_urman_night")
        foreach (var puddle in zone.GetChildren().OfType<Node3D>().Where(node => node.HasMeta("puddleGeometry")))
            HidePresentationNode(puddle);

        if (zoneId == "kara_urman_night" && zone.GetNodeOrNull<MeshInstance3D>("BoundaryStoneNear") is { Mesh: not null } stone)
        {
            var bounds = stone.GlobalTransform * stone.Mesh.GetAabb();
            var ground = Enumerable.Range(0, 8).Min(corner =>
            {
                var point = bounds.GetEndpoint(corner);
                return AgentBAct1HeightField.CollisionGround(point.X, point.Z);
            });
            stone.GlobalPosition += Vector3.Up * (ground - .04f - bounds.Position.Y);
        }

        // A few benchmark-only props are useful in their isolated camera
        // studies but become accidental route blockers or floating lights
        // when the five scenes share one exterior. Keep the source scenes
        // intact and suppress only those presentation artifacts here.
        if (string.Equals(zoneId, "zirat_road", StringComparison.Ordinal))
        {
            HidePresentationNodes(zone, "ZiratFence");
            HidePresentationNodes(zone, "ZiratRoadsideAuthoredKit");
            // The shared heightfield/road is the visual ground owner. Hide
            // benchmark surfaces and their obsolete flat traversal bodies.
            foreach (var surfaceName in new[] { "Ground", "Road" })
            {
                if (zone.GetNodeOrNull<Node3D>(surfaceName) is { } surface)
                    HidePresentationNode(surface);
            }
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
            // Repeated benchmark trees receive anonymous Godot names, so
            // prefix suppression alone leaves invisible trunks across the road.
            HideBenchmarkTreeBodies(zone);
            // AlignFapClinicArchitecture binds RoadToFap to the live door once
            // the authored exterior exists; the source signpost position never
            // becomes the physical clinic entrance.
            // The benchmark sign's board/post/arrow/text are gone from this
            // composition: the connected world supplies its own readable FAP sign
            // as DiscoveryMainStreetSign with the reverse-word discovery. The
            // legacy sign's interaction box is suppressed separately (see
            // SuppressLegacySignInteraction), because the zone script creates it
            // after this pass runs.
            HidePresentationNodes(
                zone,
                "VillageSignPost",
                "VillageSignBoard",
                "VillageSignArrow",
                "VillageSignText",
                "Ground",
                "Road",
                "Pine",
                "Birch",
                "UtilityPole",
                "UtilityCable",
                // The connected world keeps the legacy StyleBenchmarkZone
                // mounted for its interaction targets, not traversal ground,
                // but its benchmark houses/fences are hidden presentation
                // geometry. Suppress their direct-child colliders as well;
                // otherwise they remain invisible blockers across the
                // authored Agent B house approach.
                "House",
                "Foundation",
                // The isolated HouseA sample overlaps the connected east
                // parcel gate. Its visible shell and provisional layer-2
                // walls are replaced by MainStreetEastNeighborFacade.
                "GeneratedModularKit",
                "Fence");
        }
        else if (string.Equals(zoneId, "kara_urman_night", StringComparison.Ordinal))
        {
            // Shared heightfield owns both visible and collidable ground.
            foreach (var surfaceName in new[] { "Ground", "PathNear", "PathMiddle", "PathFar" })
            {
                if (zone.GetNodeOrNull<Node3D>(surfaceName) is { } surface)
                    HidePresentationNode(surface);
            }
            HidePresentationNodes(
                zone,
                // The connected core mounts this same kit at the same anchors.
                // Keep one exterior presentation owner, not the benchmark copy.
                "KaraForestEdgeAuthoredKit",
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
                // The study's cloth and cord belong to the removed boundary
                // posts, not to the authored winter edge or Rinat's staging.
                "BoundaryCharm",
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
        using var naming = InvariantNameScope();
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
        connectors.SetMeta("collisionOwner", "AgentB_TerrainCollision");
        connectors.SetMeta("interactionOwner", "none");
        connectors.SetMeta("runtimeStateOwner", "none");
        connectors.SetMeta(
            "surfacePolicy",
            "AgentB_TerrainCollision owns exterior traversal; connector nodes are route metadata only");
        AddChild(connectors);

        AddVisualBox(
            connectors,
            "SharedVillageGround",
            new Vector3(86f, 0.12f, 208f),
            new Vector3(0f, -0.12f, -48f),
            "59604d",
            "earth");
        // Legacy visual placeholder only: the heightfield owns collision.
        HidePresentationNodes(connectors, "SharedVillageGround");
        connectors.SetMeta(
            "sharedGroundPresentation",
            "suppressed; AgentBExteriorWorld owns visible relief and matching terrain collision");
        ConnectorTraversalCollisionCount = 0;

        foreach (var connector in Act1WorldLayout.Connectors)
        {
            var segment = new Node3D { Name = connector.ConnectorId };
            segment.SetMeta("visualOnly", false);
            segment.SetMeta("collisionOwner", "AgentB_TerrainCollision");
            segment.SetMeta("interactionOwner", "none");
            segment.SetMeta("start", connector.Start);
            segment.SetMeta("end", connector.End);
            segment.SetMeta("traversalSurface", "RoadSurface");
            segment.SetMeta("dressingPolicy", "shoulders and parcel framing are visual-only; route center remains open");
            connectors.AddChild(segment);

            AddVisualStrip(segment, "RoadSurface", connector, connector.Width, 0.026f, "earth");
            // The wet-village kit provides the authored crown/rut/shoulder
            // presentation where mounted. Keep connector metadata, not a
            // second flat collider above the shared terrain.
            HidePresentationNodes(segment, "RoadSurface");
            segment.SetMeta("roadSurfacePresentation", "suppressed; WetVillageRoadKitPresentation owns authored crown where mounted");
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
            var world = infrastructure.ToGlobal(poles[index]);
            world.Y = AgentBAct1HeightField.CollisionGround(world.X, world.Z) - .04f;
            poles[index] = infrastructure.ToLocal(world);
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
        // EX02: the east holding's south boundary has a 1.2 m gap between the
        // two runs. A carryable crate stands in it - the player either carries
        // the crate aside (EX01) or walks the long way around the fence's east
        // end. Two different solutions, one optional local access.
        AddVisualFenceRun(infrastructure, "ReturnEastFieldBoundaryWest", new(22.0f, 0f, -38.0f), new(25.8f, 0f, -38.8f));
        AddVisualFenceRun(infrastructure, "ReturnEastFieldBoundaryEast", new(27.0f, 0f, -39.2f), new(32.0f, 0f, -41.5f));
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

        // Distant minaret silhouette on the western skyline: identifies the
        // Tatar village without creating a new zone (presentation-only).
        var minaretAnchor = new Vector3(-46f, (float)AgentBAct1HeightField.Ground(-46f, -34f), -34f);
        AddDistantMinaret(core, "DistantMinaretSilhouette", minaretAnchor, 1.0f);
        // The author reported the bare minaret reading as a lone tower and asked
        // for a real mosque in this part of the village: the hall, dome, closed
        // entrance and courtyard wall now stand around the existing minaret.
        AddVillageMosque(core, minaretAnchor);
        // Canon boundary the author confirmed: the river separates the village from
        // the forest, with an old broken bridge as the landmark. The road crosses at
        // the authored culvert, which stays the only passable line.
        AddVillageRiverAndBrokenBridge(core);
        // The ravine east of the FAP splits the village in two; its bridge has
        // lost the middle span (author, 2026-09-25).
        AddVillageRavine(core);

        // Unreachable background layers (T3): mid woodland bands and far snow
        // ridges, all beyond the walkable envelope. The near village rows that
        // used to stand here are gone: the tall Act I forest ring now closes
        // that ground, and a backdrop house left inside the ring would be the
        // one silhouette that betrays the depth behind the trees.
        AddDistantRidge(core, "BackdropFarRidgeWest",
            new Vector3(-240f, (float)AgentBAct1HeightField.Ground(-240f, -60f), -60f), 220f, 8f, 90f, 8f);
        AddDistantRidge(core, "BackdropFarRidgeEast",
            new Vector3(230f, (float)AgentBAct1HeightField.Ground(230f, -70f), -70f), 210f, 7f, 85f, -6f);
        AddDistantRidge(core, "BackdropFarRidgeNorth",
            new Vector3(-40f, (float)AgentBAct1HeightField.Ground(-40f, -260f), -260f), 250f, 10f, 110f, 0f);
        AddBackdropGround(core);
        AddMainStreetSnowBanks(core);

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
        AddDistantForestBand(core, "BackdropMidWoodlandWest",
            new Vector3(-150f, (float)AgentBAct1HeightField.Ground(-150f, -30f), -30f), 34, 8.0f, 87f, 1.15f, false);
        AddDistantForestBand(core, "BackdropMidWoodlandEast",
            new Vector3(120f, (float)AgentBAct1HeightField.Ground(120f, -40f), -40f), 30, 8.5f, 94f, 1.1f, false);
        AddDistantForestBand(core, "BackdropMidForestNorth",
            new Vector3(-70f, (float)AgentBAct1HeightField.Ground(-70f, -170f), -170f), 30, 9.0f, 2f, 1.25f, true);
        AddDistantForestBand(core, "BackdropFarForestNorth",
            new Vector3(-20f, 0f, -350f), 36, 9f, 0f, 1.6f, true);
        BindAuthoredWinterTrees(core);
        ApplyAct1DaylightPresentationPass(core);
        ReplaceKitWinterShrubs(core);
        BuildVillageLife(core);
    }

    /// <summary>
    /// Authored carryables and aimed tool uses share runtime custody and props.
    /// Built after the yard's real openings, so the visible target and barrier
    /// are the same place, not a remote effect triggered at a tool stand.
    /// </summary>
    private void BuildCarryables(Node3D core)
    {
        var props = new List<CarryableProp>
        {
            CarryableProp.Create("carry-log", "Полено", CarryableProp.ItemClass.Light,
                GroundedYardPoint(new(-32.4f, 0f, 4.6f)), 24f, "8a6b50", "wood"),
            CarryableProp.Create("carry-crate", "Ящик", CarryableProp.ItemClass.Medium,
                GroundedYardPoint(new(-27.5f, 0f, 2.8f)), -12f, "7a5c3a", "wood"),
            CarryableProp.Create("carry-bucket", "Ведро", CarryableProp.ItemClass.Bucket,
                GroundedYardPoint(new(-26.6f, 0f, 0.2f)), 8f, "6f6d61", "metal"),
            CarryableProp.Create("carry-board", "Доска", CarryableProp.ItemClass.Bulky,
                GroundedYardPoint(new(-29.3f, 0f, 5.2f)), 14f, "8a6b50", "wood"),
            CarryableProp.Create("carry-lantern", "Аккумуляторный фонарь", CarryableProp.ItemClass.Light,
                GroundedYardPoint(new(-28.4f, 0f, 1.6f)), 0f, "575b57", "metal", CarryableProp.ItemKind.Lantern),
        };
        // EX02: this crate obstructs the timber approach on the existing east
        // service loop. Carry it aside or walk around through the open snow;
        // both approaches reach the same service yard outside the clinic.
        var gapCrate = CarryableProp.Create("carry-gap-crate", "Ящик на настиле",
            CarryableProp.ItemClass.Medium, GroundedYardPoint(new(41.5f, 0f, -27.24f)), 0f, "7a5c3a", "wood",
            size: new(1.16f, .65f, .70f));
        props.Add(gapCrate);
        var coordinator = CarryCoordinator.Create(props);

        // EX05.1/EX05.2: the authored chain of observation. A line of pressed
        // prints runs from the street to the side gate, the snow at the leaf is
        // swept on one side and banked on the other, and the drift itself has
        // been worked through once. These are static authored evidence, not the
        // cosmetic player tracks of SnowTrampleField: the trample mask may reset
        // by its own session-only contract without erasing the trail. Reading it
        // tells the player someone used this opening recently and kept it clear -
        // it does NOT confirm who, and the prints are deliberately characterless.
        var trail = new Node3D { Name = "ExteriorSnowTrail" };
        trail.SetMeta("presentationOnly", true);
        trail.SetMeta("visualOnly", true);
        trail.SetMeta("evidenceRole", "authored snow trail; not SnowTrampleField");
        trail.SetMeta("observation", "someone uses this side opening and keeps it clear; identity unconfirmed");
        trail.SetMeta("snowTramplePolicy", "authored and static; survives the session-only trample reset");
        core.AddChild(trail);
        for (var i = 0; i < 11; i++)
        {
            var t = i / 10f;
            var x = Mathf.Lerp(-21.6f, -24.42f, t);
            var z = Mathf.Lerp(2.30f, 0.55f, t);
            var ground = AgentBAct1HeightField.CollisionGround(x, z);
            for (var foot = 0; foot < 2; foot++)
            {
                var offset = (i % 2 == 0 ? 1f : -1f) * 0.10f;
                AddVisualBox(trail, $"Print{i}_{foot}",
                    new(0.13f, 0.016f, 0.26f),
                    new(x + (foot == 0 ? offset : -offset), ground + 0.012f, z + (foot == 0 ? 0.10f : -0.10f)),
                    "cfc9bd", "snow",
                    yawDegrees: -58f);
            }
        }

        var gateGround = GroundedYardPoint(new Vector3(-25.20f, 0f, -2.85f));
        var gateSnow = _ex05PassageDrift ?? throw new InvalidOperationException("Yard service drift must be built before its shovel target.");
        var gateSwept = AddYardSnowDetail(core, "ToolSnowSweptAtLeaf",
            new(0.62f, 0.03f, 1.30f), gateGround, "c4cdd3", swept: true);
        gateSwept.Visible = false;
        var gateWorked = AddYardSnowDetail(trail, "ToolSnowWorkedEdge",
            new(0.34f, 0.05f, 0.40f), GroundedYardPoint(new(-24.62f, 0f, -2.85f)), "dce3e8");
        // Keep the buried tool beside the solid logs, not inside their hull.
        // The drift, visible handle and recoverable item share this one anchor.
        var buriedToolPoint = GroundedYardPoint(new(-31.6f, 0f, 4.3f));
        var woodSnow = AddYardSnowDetail(core, "ToolSnowPileWood",
            new(0.34f, 0.18f, 0.26f), buriedToolPoint, "eef2f6");
        var axeHandleHint = AddVisualBox(core, "BuriedAxeHandleTip",
            new(.17f, .027f, .027f), buriedToolPoint + new Vector3(-.15f, .025f, .12f), "94775a", "wood");
        axeHandleHint.RotationDegrees = new(0, 62, 0);
        axeHandleHint.SetMeta("itemVisualOwner", "carry-axe");

        // EX05.3: two drifts. The one at the side opening is cleared by the same
        // shovel use, but it is a windrow beside the walked line, not a barrier:
        // the side opening stays the mandatory route it always was, so clearing
        // snow is never a chore on the critical path (the plan forbids that).
        // The drift that really changes a passage is the one plugging the lower
        // fence gap below, which nothing on the authored route uses.
        //
        // EX05.3/EX05.4: the object the woodpile drift was banked over. Its
        // handle shows under the snow as the readable hint; only clearing the
        // drift makes it takeable, and the cleared result is persisted through
        // world.props, so the step stays open after a load while the cosmetic
        // snow dust of SnowTrampleField still resets by its own contract.
        var kindlingAxe = CarryableProp.Create("carry-axe", "Топорик", CarryableProp.ItemClass.Light,
            buriedToolPoint, 62f, "5d5b52", "metal");
        kindlingAxe.SetConcealed(true);
        props.Add(kindlingAxe);
        coordinator.Register(kindlingAxe);

        var shovel = YardTool.Create("shovel", "Лопата", GroundedYardPoint(new(-27.6f, 0f, 0.9f)), 96f, shovel: true);
        // One shovelling along the yard's own fence line clears both drifts on
        // it: the windrow beside the side opening and the one banking the
        // service gap. The gap's drift is the barrier, so this use is what
        // actually opens the passage below.
        shovel.AddUse("fence", "Расчистить снег в служебном проходе", gateSnow,
            visible =>
            {
                gateSwept.Visible = !visible;
                gateWorked.Visible = visible;
                if (_ex05PassageDrift is not null)
                {
                    _ex05PassageDrift.Visible = visible;
                }

                SetYardCollisionEnabled(_ex05PassageBarrier, visible);
            });
        shovel.AddUse("woodpile", "Расчистить снег у поленницы", woodSnow,
            visible =>
            {
                axeHandleHint.Visible = visible;
                if (!kindlingAxe.HasOwnDeviation)
                {
                    kindlingAxe.SetConcealed(visible);
                }
            });
        coordinator.Register(shovel);

        var pole = YardTool.Create("pole", "Шест", GroundedYardPoint(new(-28.1f, 0f, 4.2f)), 12f, shovel: false);
        coordinator.Register(pole);

        // Carried things cross the actual house/FAP portal. They must not be
        // children of the outdoor-only visual root, which is hidden indoors.
        BuildYardMechanisms(core, coordinator);
        AddChild(coordinator);
        _carryCoordinator = coordinator;
        if (_runtimeBridge is not null)
        {
            // The bridge may already be attached when the yard is built, in
            // which case the subscribe pass will not run again.
            coordinator.AttachRuntimeState(_runtimeBridge);
        }
        SetMeta("yardToolCount", 2);
        SetMeta("carryableItemCount", props.Count);
        SetMeta("carryablePolicy",
            "world.custody ownership + world.props placement deviation through the existing snapshot; cosmetic snow stays session-only");
    }

    private void BuildVillageLife(Node3D core)
    {
        _villageLife = new Node3D { Name = "VillageLife" };
        _villageLife.SetMeta("presentationOnly", true);
        _villageLife.SetMeta("collisionOwner", "none");
        _villageLife.SetMeta("event", 0);
        core.AddChild(_villageLife);
        _lifeRandom.Randomize();
        _lifeWait = _lifeRandom.RandfRange(24f, 38f);

        var source = ResourceLoader.Load<PackedScene>(VillageExteriorKitScenePath).Instantiate<Node3D>();
        var cat = FindDescendants<Node3D>(source).Single(node => node.Name == "AmbientCat");
        _yardCat = AttachAct1ExteriorKitComponent(_villageLife, cat, "YardCat", Vector3.Zero,
            0f, Vector3.One, "village_day@babai-cleared-approach");
        _yardCat.GlobalPosition = LifeGround(-28.3f, 6.4f);
        _yardCat.RotationDegrees = new Vector3(0f, 90f, 0f);
        _catLegs = FindDescendants<Node3D>(cat)
            .Where(node => node.Name.ToString().StartsWith("CatLeg", StringComparison.Ordinal))
            .Select(node => (node, node.Basis,
                node.Name == "CatLegFrontL" || node.Name == "CatLegBackR" ? 0f : Mathf.Pi)).ToArray();
        var crow = FindDescendants<Node3D>(source).Single(node => node.Name == "AmbientCrow");
        var bird = AttachAct1ExteriorKitComponent(_villageLife, crow, "Crow0", Vector3.Zero,
            90f, Vector3.One, "village_day@above-street");
        _crows = [bird, (Node3D)bird.Duplicate(), (Node3D)bird.Duplicate()];
        for (var i = 0; i < _crows.Length; i++)
        {
            if (i > 0) { _crows[i].Name = $"Crow{i}"; _villageLife.AddChild(_crows[i]); }
            _crows[i].Visible = false;
        }
        _birdWings = _crows.SelectMany(FindDescendants<Node3D>)
            .Where(node => node.Name == "BirdWingL" || node.Name == "BirdWingR")
            .Select(node => (node, node.Basis, node.Name == "BirdWingL" ? -1f : 1f)).ToArray();
        foreach (var mesh in FindDescendants<MeshInstance3D>(_villageLife))
        for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
        {
            var materialName = mesh.GetActiveMaterial(surface)?.ResourceName ?? string.Empty;
            var isCat = _yardCat.IsAncestorOf(mesh);
            var color = isCat ? materialName.Contains("WetSlate", StringComparison.Ordinal) ? "382f2b"
                    : materialName.Contains("LightFace", StringComparison.Ordinal) ? "b5aaa0" : "65544a"
                : materialName.Contains("WetSlate", StringComparison.Ordinal) ? "242a2e" : "394149";
            mesh.SetSurfaceOverrideMaterial(surface, PainterlyMaterialLibrary.ForColor(color));
        }
        source.Free();

        // One anonymous background resident: the neighbour at the woodpile, on
        // the human kit like the named cast. No NPC, interaction, route or
        // persistent identity; he breathes in his looping idle.
        var residentHost = new Node3D { Name = "ResidentAtFirewood" };
        _villageLife.AddChild(residentHost);
        residentHost.GlobalPosition = LifeGround(-13.3f, -1.5f);
        residentHost.RotationDegrees = new Vector3(0f, -90f, 0f);
        GeneratedCharacterKitDressing.Attach(residentHost, "background_resident", "Resident", Vector3.Zero);

        var radial = new GradientTexture2D
        {
            Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(.5f, 1f),
            Gradient = new Gradient { Colors = [Colors.White, new Color(1f, 1f, 1f, 0f)], Offsets = [0f, 1f] }
        };
        var smokeMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(.50f, .52f, .54f, .36f), AlbedoTexture = radial,
            VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 1f
        };
        var smokeMesh = new QuadMesh { Size = Vector2.One, Material = smokeMaterial };
        var anchors = new List<Vector3>();
        var roofMeshes = FindDescendants<MeshInstance3D>(core).Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()
            && (mesh.Name.ToString().Contains("Roof", StringComparison.OrdinalIgnoreCase)
                || mesh.Name.ToString().Contains("Chimney", StringComparison.OrdinalIgnoreCase))).ToArray();
        foreach (var chimney in FindDescendants<MeshInstance3D>(core)
            .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()
                && mesh.Name.ToString().Contains("Chimney", StringComparison.OrdinalIgnoreCase))
            .OrderBy(mesh => mesh.GlobalPosition.DistanceSquaredTo(new Vector3(-12f, 0f, 8f))))
        {
            var bounds = chimney.GlobalTransform * chimney.Mesh.GetAabb();
            var top = bounds.GetCenter(); top.Y = bounds.End.Y;
            if (top.Z < -40f || top.Z > 40f || Mathf.Abs(top.X) > 42f
                || anchors.Any(anchor => anchor.DistanceTo(top) < 14f)) continue;
            // IsVisibleInTree does not prove that a kit chimney emerges above
            // overlapping authored roofs. Reject covered outlets using real faces.
            var covered = false;
            foreach (var roof in roofMeshes)
            {
                if (roof == chimney) continue;
                var roofBounds = roof.GlobalTransform * roof.Mesh.GetAabb();
                if (top.X < roofBounds.Position.X || top.X > roofBounds.End.X
                    || top.Z < roofBounds.Position.Z || top.Z > roofBounds.End.Z || roofBounds.End.Y <= top.Y + .03f) continue;
                var from = roof.ToLocal(top + Vector3.Up * .03f);
                var direction = roof.GlobalBasis.Inverse() * Vector3.Up;
                var faces = roof.Mesh.GetFaces();
                for (var face = 0; face < faces.Length; face += 3)
                    if (Geometry3D.RayIntersectsTriangle(from, direction, faces[face], faces[face + 1], faces[face + 2]).VariantType != Variant.Type.Nil)
                    { covered = true; break; }
                if (covered) break;
            }
            if (covered) continue;
            anchors.Add(top);
            GD.Print($"village-chimney: {chimney.GetPath()} top={top}");
            var scale = new Curve(); scale.AddPoint(new Vector2(0f, .20f)); scale.AddPoint(new Vector2(1f, 1.65f));
            var smoke = new CpuParticles3D
            {
                Name = $"ChimneySmoke{anchors.Count}", Emitting = true, Amount = 18, Lifetime = 6.5, Preprocess = 4,
                LocalCoords = false, Mesh = smokeMesh, Direction = new Vector3(.20f, 1f, .10f),
                Spread = 8f, Gravity = new Vector3(.08f, .04f, .045f),
                InitialVelocityMin = .45f, InitialVelocityMax = .65f,
                ScaleAmountMin = .8f, ScaleAmountMax = 1.1f, ScaleAmountCurve = scale,
                ColorRamp = new Gradient { Offsets = [0f, .15f, .65f, 1f],
                    Colors = [new Color(1f, 1f, 1f, 0f), Colors.White,
                        new Color(1f, 1f, 1f, .55f), new Color(1f, 1f, 1f, 0f)] },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            _villageLife.AddChild(smoke);
            smoke.GlobalPosition = top;
            smoke.Restart();
            smoke.SetMeta("chimneyOwner", chimney.GetPath().ToString());
            if (anchors.Count == 2) break;
        }
        _chimneySmoke = _villageLife.GetChildren().OfType<CpuParticles3D>().ToArray();
        if (anchors.Count != 2) throw new InvalidOperationException("Village life requires two uncovered chimney tops.");
    }

    private static Vector3 LifeGround(float x, float z) =>
        new(x, AgentBAct1HeightField.CollisionGround(x, z) - .01f, z);

    public override void _Process(double delta)
    {
        UpdatePhysicalInteriorPresentation();
        if (_villageLife is null) return;
        _lifePlayer ??= GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        _lifeCue ??= GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi;
        var inhabited = ActiveZoneId == "village_day";
        _villageLife.Visible = inhabited;
        var moving = inhabited && _lifePlayer is not null && !_lifePlayer.ModalOpen
            && !_lifePlayer.ReducedMotion && _lifeCue?.IsPresenting != true;
        foreach (var smoke in _chimneySmoke)
            smoke.SpeedScale = moving ? 1f : 0f;

        UpdateConversationFacing();
        _villageLife.SetMeta("motionAllowed", moving);
        if (!moving) return;
        var elapsed = (float)Math.Min(delta, .1);
        if (_lifeEvent == 0)
        {
            _lifeWait -= elapsed;
            if (_lifeWait > 0f) return;
            // Avoid consecutive repeats without creating a schedule or NPC simulation.
            _lifeEvent = ((_lastLifeEvent + _lifeRandom.RandiRange(0, 1)) % 3) + 1;
            _lastLifeEvent = _lifeEvent;
            _lifeTime = 0f;
            _catStart = _yardCat!.GlobalPosition;
            _catEnd = LifeGround(_catAtEastEnd ? -28.3f : -26.9f, 6.4f);
            _catStartYaw = _yardCat.Rotation.Y;
        }
        _lifeTime += elapsed;
        var duration = _lifeEvent == 1 ? 6f : _lifeEvent == 2 ? 12f : 9f;
        var t = Mathf.Clamp(_lifeTime / duration, 0f, 1f);
        if (_lifeEvent == 1)
        {
            var position = _catStart.Lerp(_catEnd, Mathf.Clamp((t - .15f) / .85f, 0f, 1f));
            _yardCat!.GlobalPosition = LifeGround(position.X, position.Z);
            _yardCat.Rotation = new Vector3(0f, Mathf.LerpAngle(_catStartYaw,
                _catAtEastEnd ? -Mathf.Pi * .5f : Mathf.Pi * .5f, Mathf.SmoothStep(0f, .15f, t)), 0f);
            var envelope = Mathf.Min(1f, Mathf.Min(t, 1f - t) * 12f);
            foreach (var (leg, rest, phase) in _catLegs)
                leg.Basis = rest * new Basis(Vector3.Right, Mathf.Sin(_lifeTime * 11f + phase) * .23f * envelope);
        }
        else if (_lifeEvent == 2)
        {
            for (var i = 0; i < _crows.Length; i++)
            {
                _crows[i].Visible = t < 1f;
                _crows[i].GlobalPosition = new Vector3(-50f + t * 120f - i * 1.5f, 17f + i * .7f, -5f - i * 2f);
            }
            foreach (var (wing, rest, side) in _birdWings)
                wing.Basis = rest * new Basis(Vector3.Back, side * Mathf.Sin(_lifeTime * 13f) * .5f);
        }
        else if (_residentSkeleton is not null)
        {
            // Briefly brush snow from sleeves beside the woodpile, with feet fixed.
            var envelope = Mathf.SmoothStep(0f, .2f, t) * (1f - Mathf.SmoothStep(.8f, 1f, t));
            for (var index = 0; index < _residentRest.Length; index++)
            {
                var angle = index switch { 0 => .08f, 1 => -.12f,
                    2 => -.48f + .06f * Mathf.Sin(_lifeTime * 5f),
                    _ => -.42f - .06f * Mathf.Sin(_lifeTime * 5f) };
                var (bone, rest) = _residentRest[index];
                _residentSkeleton.SetBonePoseRotation(bone, rest * new Quaternion(Vector3.Right, angle * envelope));
            }
        }
        _villageLife.SetMeta("event", _lifeEvent);
        _villageLife.SetMeta("eventTime", _lifeTime);
        if (t < 1f) return;
        if (_lifeEvent == 1) _catAtEastEnd = !_catAtEastEnd;
        _lifeEvent = 0;
        _lifeWait = _lifeRandom.RandfRange(45f, 120f);
        _villageLife.SetMeta("event", 0);
        _villageLife.SetMeta("nextWait", _lifeWait);
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
        SuppressAgentBOverlappingRoadDecor(layer);
        // The exterior owner suppresses replaced building families before it
        // creates their contacts. Nearby visible fences keep their own collision.
    }

    private static void SuppressAgentBOverlappingRoadDecor(Node3D layer)
    {
        var roadKit = layer.GetNodeOrNull<Node3D>("AgentB_TerrainRoadKit")
            ?? throw new InvalidOperationException(
                "Agent B terrain road kit is missing before authored wet-road presentation cleanup.");
        var requiredNames = new[]
        {
            "MudStrip_West",
            "MudStrip_East",
            "Rut_West",
            "Rut_East",
            "Ditch_West",
            "Ditch_East"
        };
        var meshes = FindDescendants<MeshInstance3D>(roadKit).ToArray();
        var missing = requiredNames
            .Where(name => meshes.All(mesh => !string.Equals(mesh.Name.ToString(), name, StringComparison.Ordinal)))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Agent B terrain road kit is missing the declared overlapping decor meshes: {string.Join('|', missing)}.");
        }

        var hidden = 0;
        foreach (var mesh in meshes.Where(mesh => requiredNames.Contains(mesh.Name.ToString(), StringComparer.Ordinal)))
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta(
                "suppressionReason",
                "continuous Road_Main owns the road surface; separate strip decor is redundant; terrain heightfield/collision remains unchanged");
            hidden++;
        }

        // Winter clearing buries the old wet-season puddle beds and ford
        // stones on the road; lowering the road must not expose them again.
        foreach (var mesh in meshes.Where(mesh => mesh.Name.ToString().StartsWith("Puddle_", StringComparison.Ordinal)
            || mesh.Name.ToString().StartsWith("FordStone_", StringComparison.Ordinal)))
        {
            var center = mesh.GlobalTransform * mesh.Mesh.GetAabb().GetCenter();
            var road = AgentBAct1HeightField.RoadInfo(center.X, center.Z);
            if (road.Distance > road.HalfWidth + .45) continue;
            mesh.Visible = false;
            mesh.SetMeta("suppressionReason", "winter cleared road covers wet-season bed/ford stones");
        }

        roadKit.SetMeta("suppressedOverlappingRoadDecorFamilies", string.Join('|', requiredNames));
        roadKit.SetMeta("suppressedOverlappingRoadDecorMeshCount", hidden);
        roadKit.SetMeta(
            "roadPresentationOwner",
            "continuous Road_Main/Road_FapBranch/Road_HousePath/Road_KaraPath own road surfaces; wet-road kit retains vegetation only");
    }

    /// <summary>
    /// Tunes the existing Agent B environment after its normal zone toggle.
    /// No environment, weather or light owner is added here.
    /// </summary>
    private static void TuneConnectedAct1Atmosphere(
        Node3D core,
        bool enabled,
        bool karaNight,
        bool zirat)
    {
        if (!enabled)
        {
            return;
        }

        var layer = core.GetNodeOrNull<Node3D>("AgentBExteriorWorld");
        var environmentNode = layer?.GetNodeOrNull<WorldEnvironment>("AgentBEnvironment");
        var environment = environmentNode?.Environment;
        if (layer is null || environment is null)
        {
            return;
        }

        // Neutral snow bounce keeps the key-light direction readable.
        environment.AmbientLightEnergy = karaNight ? .52f : zirat ? .64f : .48f;
        environment.AmbientLightSource = global::Godot.Environment.AmbientSource.Color;
        // Keep the snow bounce cool enough to separate shaded faces from the
        // warm low winter key; the existing sun remains the only outdoor key.
        environment.AmbientLightColor = Color.FromHtml(karaNight ? "a1aebb" : zirat ? "c0c8d0" : "a8bfe1");
        environment.AmbientLightSkyContribution = .30f;
        // Frost haze: cold pale blue-grey that the far houses and forest melt
        // into, so distant snow does not read as a flat white wall.
        environment.FogLightColor = karaNight
            ? Color.FromHtml("90a8b8")
            : zirat ? Color.FromHtml("a7b5c1") : Color.FromHtml("b9cfdd");
        environment.FogDensity = karaNight ? .009f : zirat ? .0038f : .0022f;
        environment.FogHeight = karaNight ? 0.95f : 1.0f;
        environment.FogHeightDensity = karaNight ? .05f : zirat ? .03f : .025f;
        environment.FogAerialPerspective = karaNight ? 0.35f : zirat ? 0.60f : 0.64f;
        // Let the procedural sky carry its blue gradient instead of washing
        // every roof and distant facade into the same grey veil.
        environment.FogSkyAffect = karaNight ? 0.08f : zirat ? 0.22f : 0.14f;
        environment.FogSunScatter = karaNight ? 0.07f : zirat ? 0.06f : 0.09f;
        environment.TonemapMode = global::Godot.Environment.ToneMapper.Agx;
        // Snow is the brightest surface in frame; exposure protects its detail.
        environment.TonemapExposure = karaNight ? 1.04f : zirat ? 0.90f : 0.92f;

        // Contact shading stays local; broad halos and full-frame grading
        // are unnecessary after the snow/foliage geometry pass.
        environment.GlowEnabled = false;
        environment.SsaoEnabled = true;
        environment.SsaoIntensity = .75f;
        environment.SsaoRadius = .4f;
        environment.AdjustmentEnabled = false;
        environment.AdjustmentBrightness = 1f;
        environment.AdjustmentSaturation = 1f;
        environment.AdjustmentContrast = 1f;
        GraphicsQuality.ConfigureEnvironment(environment);

        if (environment.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
        {
            sky.SkyTopColor = karaNight
                ? Color.FromHtml("1b2836")
                : zirat ? Color.FromHtml("7f95a8") : Color.FromHtml("6694ad");
            sky.SkyHorizonColor = karaNight
                ? Color.FromHtml("3c4c60")
                : zirat ? Color.FromHtml("c3cdd6") : Color.FromHtml("d3e3ea");
            sky.GroundHorizonColor = karaNight
                ? Color.FromHtml("2c3a4a")
                : zirat ? Color.FromHtml("9aa7b1") : Color.FromHtml("a4b5c1");
            sky.GroundBottomColor = karaNight
                ? Color.FromHtml("141d28")
                : zirat ? Color.FromHtml("6d7883") : Color.FromHtml("71828f");
            // Soft day sun disc/halo from the active sun direction; the kara
            // night keeps a bare cold sky with no disc.
            sky.SunAngleMax = karaNight ? 0f : 3.0f;
            sky.SunCurve = 0.12f;
            sky.SkyCoverModulate = karaNight
                ? new Color(0.60f, 0.68f, 0.76f, 0.26f)
                : zirat ? new Color(0.90f, 0.93f, 0.95f, 0.50f) : new Color(0.86f, 0.94f, 0.98f, 0.68f);
        }

        var sun = layer.GetNodeOrNull<DirectionalLight3D>("AgentBSun");
        if (sun is not null)
        {
            // Phase 3: warm low key for day zones; long readable shadows.
            // Pale winter sun: warm-white on the snow, long blue shadows.
            sun.LightColor = karaNight
                ? Color.FromHtml("9fb6d4")
                : zirat ? Color.FromHtml("e8eef4") : Color.FromHtml("ffe7c9");
            sun.LightEnergy = karaNight ? .55f : zirat ? 1.05f : 1.65f;
            sun.ShadowOpacity = karaNight ? .38f : zirat ? .50f : .82f;
            sun.ShadowEnabled = true;
            sun.RotationDegrees = karaNight
                ? new Vector3(-52f, -28f, 0f)
                : new Vector3(-31f, 42f, 0f);
            GraphicsQuality.ConfigureSun(sun);
        }
        core.SetMeta("unifiedAtmosphereProfile", karaNight
            ? "kara-winter-night-edge"
            : zirat ? "zirat-winter-muted" : "village-winter-frost");
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

        // The capture still showed the presentation layer as a full-width
        // board: several near parcels and duplicate ground props occupied the
        // same camera rays. Retire only those presentation-only siblings after
        // all kits are mounted; the road, authored boundary, far silhouettes
        // and every gameplay owner remain untouched.
        var compositionSuppressionCount = 0;
        foreach (var (relativePath, reason) in new (string RelativePath, string Reason)[]
                 {
                     // Keep the west forward parcel as the single near
                     // street beat; the branch-side east copy stays hidden so
                     // the road window does not become a bilateral wall.
                     ("Act1AuthoredExteriorKitPresentation/NeighborParcels/MainStreet/ForwardEastParcel", "suppress the branch-side duplicate; retain one staggered west MainStreet near parcel"),
                     ("Act1AuthoredExteriorKitPresentation/ArrivalAuthoredParcels/ArrivalForwardWestGate", "remove the presentation-only west Arrival gate that crosses the first-person road window; parcel route/collision owners remain unchanged"),
                     ("Act1AuthoredExteriorKitPresentation/ArrivalAuthoredParcels/ArrivalForwardWestFence", "remove the presentation-only west Arrival fence that crosses the first-person road window; parcel route/collision owners remain unchanged"),
                     ("Act1AuthoredExteriorKitPresentation/ArrivalAuthoredParcels/ArrivalForwardEastFence", "remove the presentation-only fence that spans the Arrival first-person road window; parcel route/collision owners remain unchanged"),
                     ("KaraForestEdge/KaraUrmanDenseEdge", "remove the legacy near Kara framing root whose trunks, crowns and fence band crowd the back/side aperture; authored Kara edge and route remain visible"),
                     ("Act1AuthoredSightlineClosurePass/KaraLateralForestClosure/KaraClosureWestBank", "remove a redundant near Kara bank from the fixed-camera aperture"),
                     ("Act1AuthoredSightlineClosurePass/KaraLateralForestClosure/KaraClosureWestMixedTrees", "remove a redundant near Kara tree cluster from the fixed-camera aperture"),
                     ("Act1AuthoredSightlineClosurePass/KaraLateralForestClosure/KaraClosureWestRootWall", "remove a redundant foreground Kara root wall from the fixed-camera aperture"),
                     ("Act1AuthoredSightlineClosurePass/KaraLateralForestClosure/KaraClosureEastBank", "remove a redundant near Kara bank from the fixed-camera aperture"),
                     ("Act1AuthoredSightlineClosurePass/KaraLateralForestClosure/KaraClosureEastMixedTrees", "remove a redundant near Kara tree cluster from the fixed-camera aperture"),
                     ("Act1AuthoredSightlineClosurePass/KaraLateralForestClosure/KaraClosureEastRootWall", "remove a redundant foreground Kara root wall from the fixed-camera aperture"),
                     ("KaraForestEdge/KaraApproachRootFramingWest", "remove a low foreground root that reads as a route-side boulder"),
                     ("KaraForestEdge/KaraApproachRootFramingEast", "remove a low foreground root that reads as a route-side boulder"),
                     ("KaraForestEdge/KaraApproachUnderstoryWest", "remove a foreground understory clump that occludes the path"),
                     ("KaraForestEdge/KaraApproachUnderstoryEast", "remove a foreground understory clump that occludes the path"),
                     ("KaraForestEdge/KaraApproachStoneWest", "remove an isolated Kara foreground stone; the forest edge remains grounded by its banks"),
                     ("KaraForestEdge/KaraDeepForestClosureGrouping/KaraClosureNearBirchEast", "remove the confirmed near-east canopy occluder from the fixed first-person Kara aperture; retain the closure grouping and route collision"),
                     ("KaraForestEdge/KaraReturnContinuityBirchWest", "remove the confirmed pale return-facing Kara cutout from the fixed first-person aperture; retain the return continuity boundary")
                 })
        {
            if (core.GetNodeOrNull<Node>(relativePath) is null)
            {
                continue;
            }

            HideCorePresentationNode(core, relativePath, reason, required: false);
            compositionSuppressionCount++;
        }

        // Zirat is intentionally quiet: the roadside kit owns one readable
        // boundary/path/marker grouping. Retire the older scattered core
        // memorial props so the cemetery does not become a field of boxes and
        // stones, while retaining the authored route-side kit and distant edge.
        foreach (var (relativePath, reason) in new (string RelativePath, string Reason)[]
                 {
                     ("ZiratMemoryField/ZiratAuthoredMemorialForms", "the authored roadside kit owns the restrained marker grouping"),
                     ("ZiratMemoryField/ZiratBoundaryClosureGrouping", "the authored roadside kit owns the restrained zirat boundary and path read"),
                     ("ZiratMemoryField/ZiratMemoryStoneWest", "remove an isolated core stone from the zirat road sightline"),
                     ("ZiratMemoryField/ZiratMemoryStoneEast", "remove an isolated core stone from the zirat road sightline"),
                     ("ZiratMemoryField/ZiratMemoryStoneFar", "remove an isolated core stone from the zirat road sightline"),
                     ("ZiratMemoryField/ZiratLateralOpenThresholdGate", "remove the duplicate box-like zirat gate; retain the authored open boundary gate"),
                     ("ZiratMemoryField/ZiratNearWestGraveMound", "remove a duplicate low zirat mound from the road window"),
                     ("ZiratMemoryField/ZiratNearEastFieldStone", "remove an isolated zirat field stone from the road window"),
                     ("ZiratMemoryField/ZiratMidEastGraveMound", "remove a duplicate low zirat mound from the road window"),
                     ("ZiratMemoryField/ZiratMidEastFieldStone", "remove an isolated zirat field stone from the road window"),
                     ("ZiratMemoryField/ZiratFarWestFieldMound", "remove a duplicate low zirat mound from the road window")
                 })
        {
            if (core.GetNodeOrNull<Node>(relativePath) is null)
            {
                continue;
            }

            HideCorePresentationNode(core, relativePath, reason, required: false);
            compositionSuppressionCount++;
        }

        core.SetMeta("captureGroundedCompositionSuppressionCount", compositionSuppressionCount);
        var materialReboundCount = RegradeAct1DaylightKitMaterials(core);
        core.SetMeta(
            "daylightPresentationPass",
            "retire legacy near Kara framing plus the Arrival fence crossing the road window, keep authored Kara ground breakup and zirat boundary/path roots visible, and regrade existing authored daylight kits");
        core.SetMeta("daylightPresentationHiddenFoliageCount", hiddenFoliageCount);
        core.SetMeta("ziratPresentationSuppressionCount", hiddenZiratCount);
        core.SetMeta(
            "ziratPresentationSuppressionDescription",
            "hide duplicate core fence rail/post descendants and three oversized core conifers; the authored Wave4 zirat fence/gate/path and marker groups remain visible");
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
            ["AB_log_wall"] = PainterlyMaterialLibrary.ForColor("967d5e", "log_wall"),
            ["AB_timber_dark"] = PainterlyMaterialLibrary.ForColor("695541", "wood"),
            ["AB_fade_paint"] = PainterlyMaterialLibrary.ForColor("92816b", "wood_fence"),
            ["AB_roof_iron"] = PainterlyMaterialLibrary.ForColor("76807a", "roof_metal"),
            ["AB_roof_iron_dark"] = PainterlyMaterialLibrary.ForColor("687069", "roof_metal"),
            ["AB_roof_shingle"] = PainterlyMaterialLibrary.ForColor("695b4e", "roof"),
            ["AB_window_warm"] = new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml("d89b5d"),
                EmissionEnabled = true,
                Emission = Color.FromHtml("a86436"),
                EmissionEnergyMultiplier = 2.0f,
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
            ["URMAN_Wood_Weathered"] = PainterlyMaterialLibrary.ForColor("6f6353", "wood_facade"),
            ["URMAN_Hero_Log"] = PainterlyMaterialLibrary.ForColor("594d40", "wood_log_uv"),
            ["URMAN_Hero_LogEnd"] = PainterlyMaterialLibrary.ForColor("88745a", "wood_cut"),
            ["URMAN_Hero_Trim_Teal"] = PainterlyMaterialLibrary.ForColor("547e76", "wood_painted_trim"),
            ["URMAN_Hero_Trim_Ivory"] = PainterlyMaterialLibrary.ForColor("c8c5b1", "wood_painted_trim"),
            ["URMAN_Hero_RoofSnow"] = PainterlyMaterialLibrary.ForColor("e8edf0", "snow_roof"),
            ["URMAN_Wood_WetShadow"] = PainterlyMaterialLibrary.ForColor("554e40", "wood_facade"),
            ["URMAN_Roof_WetSlate"] = PainterlyMaterialLibrary.ForColor("626b66", "roof"),
            ["URMAN_Roof_MossTone"] = PainterlyMaterialLibrary.ForColor("656d5e", "roof"),
            ["URMAN_Stone_Mossy"] = PainterlyMaterialLibrary.ForColor("75756a", "stone"),
            ["URMAN_Stone_MossFace"] = PainterlyMaterialLibrary.ForColor("636d59", "stone"),
            ["URMAN_Stone_LightFace"] = PainterlyMaterialLibrary.ForColor("858377", "stone"),
            ["FapPaintedSage"] = PainterlyMaterialLibrary.ForColor("7b8b80", "plaster"),
            ["FapPaintedDustyBlue"] = PainterlyMaterialLibrary.ForColor("74838a", "plaster"),
            ["FapPaintedTimber"] = PainterlyMaterialLibrary.ForColor("7c684f", "wood"),
            ["FapOldRoof"] = PainterlyMaterialLibrary.ForColor("5e6862", "roof_metal"),
            ["FapRoofEdge"] = PainterlyMaterialLibrary.ForColor("687169", "roof_metal"),
            ["FapShedWall"] = PainterlyMaterialLibrary.ForColor("778073", "plaster"),
            ["FapFoundationStone"] = PainterlyMaterialLibrary.ForColor("777970", "stone_foundation"),
            ["FapWetStone"] = PainterlyMaterialLibrary.ForColor("60685f", "stone"),
            ["FapDarkTimber"] = PainterlyMaterialLibrary.ForColor("514737", "wood"),
            ["FapDoorWood"] = PainterlyMaterialLibrary.ForColor("74604a", "wood"),
            ["FapDoorInset"] = PainterlyMaterialLibrary.ForColor("514638", "wood"),
            ["FapNoticeBlank"] = PainterlyMaterialLibrary.ForColor("a69779", "plaster"),
            ["FapWayfindingBlank"] = PainterlyMaterialLibrary.ForColor("64746c", "plaster"),
            ["FapRainMetal"] = PainterlyMaterialLibrary.ForColor("68746e", "stone"),
            ["FapRainMetalDark"] = PainterlyMaterialLibrary.ForColor("48544f", "stone"),
            ["FapWindowCool"] = new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml("53686a"),
                Roughness = 0.42f,
                MetallicSpecular = 0.4f
            },
            ["FapWindowWarm"] = new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml("a88b5f"),
                Roughness = 0.5f,
                MetallicSpecular = 0.3f
            },
            ["FapPathEarth"] = PainterlyMaterialLibrary.ForColor("cbd2d4", "snow_trampled"),
            ["FapPathEarthDark"] = PainterlyMaterialLibrary.ForColor("bbc5ca", "snow_trampled"),
            ["FapPuddleWater"] = PainterlyMaterialLibrary.ForColor("313d47", "ice"),
            ["DampEarth"] = PainterlyMaterialLibrary.ForColor("f1f5f9", "snow_ground"),
            ["DampEarthDark"] = PainterlyMaterialLibrary.ForColor("eaf0f5", "snow_ground"),
            ["PathDirt"] = PainterlyMaterialLibrary.ForColor("eef3f7", "snow_ground"),
            ["LeafLitter"] = PainterlyMaterialLibrary.ForColor("f0f4f8", "snow_ground"),
            ["DitchGrass"] = PainterlyMaterialLibrary.ForColor("c8d1d6", "snow_grass"),
            ["RoadGrass"] = PainterlyMaterialLibrary.ForColor("cfd8dc", "snow_grass"),
            ["ZiratGrass"] = PainterlyMaterialLibrary.ForColor("ccd4d8", "snow_grass"),
            // Agent B's actual road/terrain kit is the broad lower-frame
            // surface. Rebind its named strips here so the wet crown, wheel
            // ruts, ditches and grass do not collapse into one dark plane.
            ["AB_terrain"] = PainterlyMaterialLibrary.ForColor("eef2f6", "snow_ground"),
            ["AB_road_pigment"] = PainterlyMaterialLibrary.ForColor("e8edf2", "snow_road"),
            ["AB_earth"] = PainterlyMaterialLibrary.ForColor("f0f4f8", "snow_ground"),
            ["AB_earth_wet"] = PainterlyMaterialLibrary.ForColor("f2f6f9", "snow_ground"),
            ["AB_earth_path"] = PainterlyMaterialLibrary.ForColor("eef3f7", "snow_ground"),
            ["AB_earth_zirat_path"] = PainterlyMaterialLibrary.ForColor("e9eff4", "snow_ground"),
            ["AB_road_crown"] = PainterlyMaterialLibrary.ForColor("e3e9ee", "snow_trampled"),
            ["AB_road_rut"] = PainterlyMaterialLibrary.ForColor("cdd6dd", "snow_road"),
            ["AB_road_kara"] = PainterlyMaterialLibrary.ForColor("b9c4ce", "snow_trampled"),
            ["AB_water_dark"] = PainterlyMaterialLibrary.ForColor("2c3740", "ice"),
            ["AB_grass"] = PainterlyMaterialLibrary.ForColor("827a65", "grass"),
            ["AB_grass_dry"] = PainterlyMaterialLibrary.ForColor("d5d9d2", "snow_grass"),
            // Shared imported birch/shrub surfaces must use the same
            // painterly grade as the connected Kara edge, not raw PBR.
            ["BirchBark"] = PainterlyMaterialLibrary.ForColor("68705a", "bark_birch"),
            ["BirchLeaves"] = PainterlyMaterialLibrary.ForColor("596047", "leaf_birch"),
            ["ShrubGreen"] = PainterlyMaterialLibrary.ForColor("48553f", "foliage"),
            ["QuietStone"] = PainterlyMaterialLibrary.ForColor("7e7f73", "stone"),
            ["DistantWall"] = PainterlyMaterialLibrary.ForColor("6f716a", "plaster"),
            ["DistantRoof"] = PainterlyMaterialLibrary.ForColor("5f6761", "roof"),
            // Village kit leftovers that still rendered raw GLB albedo: the
            // well water read as a bright blue disc and cut/bark/metal parts
            // washed out. Same muted wet-village palette as above.
            // Phase 3 window treatment: dim glass reads as a dark opening
            // (price accent), the authorized warm insets glow on top of it.
            ["URMAN_Window_DimGlass"] = PainterlyMaterialLibrary.ForColor("18211f", "water"),
            ["URMAN_Window_WarmInset"] = new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml("8a6a3c"),
                EmissionEnabled = true,
                Emission = Color.FromHtml("ffb45e"),
                EmissionEnergyMultiplier = 1.6f,
                Roughness = 0.4f
            },
            ["URMAN_Well_DarkWater"] = PainterlyMaterialLibrary.ForColor("2c3740", "ice"),
            ["URMAN_Wood_CutEnd"] = PainterlyMaterialLibrary.ForColor("b39a70", "wood_cut"),
            ["URMAN_Bark_Muted"] = PainterlyMaterialLibrary.ForColor("715943", "bark_pine"),
            ["URMAN_Metal_Dulled"] = PainterlyMaterialLibrary.ForColor("5a5f5c", "iron"),
            // Zirat roadside kit members that are outside the kara-scoped
            // table: the raw albedo left pale stone/shrub groupings and pale
            // roadside fence runs in the cemetery views.
            ["MossGreen"] = PainterlyMaterialLibrary.ForColor("c8d1d6", "snow_grass"),
            ["MossyStone"] = PainterlyMaterialLibrary.ForColor("75756a", "stone"),
            ["WeatheredWood"] = PainterlyMaterialLibrary.ForColor("55493c", "wood"),
            ["WeatheredWoodDark"] = PainterlyMaterialLibrary.ForColor("4b4136", "wood"),
            ["DistantFence"] = PainterlyMaterialLibrary.ForColor("4f463b", "wood_fence"),
            ["DistantFoliage"] = PainterlyMaterialLibrary.ForColor("4a5745", "grass"),
            ["DitchWater"] = PainterlyMaterialLibrary.ForColor("2e3942", "ice"),
            ["WetSheen"] = PainterlyMaterialLibrary.ForColor("f1f5f8", "snow_ground"),
            // FAP kit grounds: raw birch/shrub/vent albedo read washed-out in
            // the clinic approach views.
            ["FapBirchBarkMark"] = PainterlyMaterialLibrary.ForColor("68705a", "bark_birch"),
            ["FapBirchPale"] = PainterlyMaterialLibrary.ForColor("7a806e", "bark_birch"),
            ["FapShrubGreen"] = PainterlyMaterialLibrary.ForColor("53634e", "foliage"),
            ["FapShrubLight"] = PainterlyMaterialLibrary.ForColor("5f6b52", "foliage"),
            ["FapVentDark"] = PainterlyMaterialLibrary.ForColor("3f4441", "stone")
        };

        // Phase 4 palette: per-parcel wall tones so neighboring houses stop
        // reading as clones, plus one restrained blue-green timber accent
        // (VariantB outbuildings only — design_style forbids ornament spread).
        var parcelTints = new (string Marker, string Source, string Color)[]
        {
            // Mounted direct components lose their optional Variant* ancestor.
            // These exact placement names are the camera-facing houses in the
            // MainStreet, connective-street and return evidence frames; keep
            // their restrained warm/cool split before generic variant rules.
            ("MainStreetForwardWestFacade", "URMAN_Plaster_Ochre", "a08d6f"),
            ("MainStreetForwardWestFacade", "URMAN_Wood_Dark", "605044"),
            // Arrival-facing dwellings keep warm plaster while their existing
            // timber trim, gable boards and window surrounds carry a restrained
            // painted blue-green accent; no new geometry or material owner.
            ("ArrivalForwardWestFacade", "URMAN_Wood_Weathered", "597b7d"),
            ("ArrivalForwardEastFacade", "URMAN_Wood_Weathered", "637f84"),
            ("MainStreetEastNeighborFacade", "URMAN_Plaster_Ochre", "7b8d86"),
            ("MainStreetEastNeighborFacade", "URMAN_Wood_Dark", "4f6a63"),
            ("MainStreetEastNeighborFacade", "URMAN_Wood_Weathered", "93a4a9"),
            ("ConnectiveStreetDeepBanyaYardParcel", "URMAN_Plaster_Ochre", "9a8d75"),
            ("ConnectiveStreetDeepBanyaYardParcel", "URMAN_Wood_Dark", "554e40"),
            ("ReturnStreetDistantLowFacade", "URMAN_Plaster_Ochre", "6f716a"),
            ("ReturnStreetDistantLowFacade", "URMAN_Wood_Dark", "4f463b"),
            ("VariantA", "URMAN_Plaster_Ochre", "93876f"),
            ("VariantC", "URMAN_Plaster_Ochre", "9a8d75"),
            ("BabaiEbi", "URMAN_Plaster_Ochre", "a08d6f"),
            ("VariantB", "URMAN_Wood_Dark", "4f6a63"),
            // Painted village trim: whitewashed surrounds on the hero house
            // and one pale blue accent house (design_style: no ornament
            // overload, colour lives on the trim, not on the walls).
            ("BabaiEbi", "URMAN_Wood_Weathered", "bcc4c8"),
            ("VariantA", "URMAN_Wood_Weathered", "93a4a9"),
            // The yard well head is the palest object on the street; mute it
            // so it stops competing with the lit facades.
            ("Well_YardLandmark", "URMAN_Stone_LightFace", "6f695c")
        };

        var rebound = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(core))
        {
            if (mesh.Mesh is null)
            {
                continue;
            }

            var lineage = new System.Text.StringBuilder();
            Node3D? walk = mesh;
            for (var depth = 0; depth < 8 && walk is not null; depth++)
            {
                lineage.Append(walk.Name).Append('/');
                walk = walk.GetParent() as Node3D;
            }
            var path = lineage.ToString();

            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                var source = mesh.Mesh.SurfaceGetMaterial(surface);
                var sourceName = source?.ResourceName ?? string.Empty;
                if (!grade.TryGetValue(sourceName, out var material))
                {
                    continue;
                }

                foreach (var (marker, tintedSource, tint) in parcelTints)
                {
                    if (path.Contains(marker, System.StringComparison.Ordinal)
                        && string.Equals(sourceName, tintedSource, System.StringComparison.Ordinal))
                    {
                        var surfaceKind = (marker, tintedSource) switch
                        {
                            ("ArrivalForwardWestFacade" or "ArrivalForwardEastFacade", "URMAN_Wood_Weathered") => "wood_painted_blue",
                            ("MainStreetEastNeighborFacade" or "VariantB", "URMAN_Wood_Dark") => "wood_painted_green",
                            _ => sourceName.Contains("Wood", System.StringComparison.Ordinal) ? "wood" : "plaster"
                        };
                        // Restrained base hues avoid the former dark tint,
                        // while keeping both paints distinct when Low omits maps.
                        material = PainterlyMaterialLibrary.ForColor(
                            surfaceKind == "wood_painted_blue" ? "9eabb9"
                                : surfaceKind == "wood_painted_green" ? "9da98f" : tint,
                            surfaceKind);
                        break;
                    }
                }

                // The loft boards and bale ties are inside the roof envelope.
                // Their shared outdoor source slots added snow to dry storage;
                // scope the sheltered finish to these members at both LODs.
                var memberName = mesh.Name.ToString();
                if (memberName.StartsWith("HeroYardShed_LoftBoard_", StringComparison.Ordinal))
                {
                    material = PainterlyMaterialLibrary.ForColor("6f6353", "wood", sheltered: true);
                    mesh.SetMeta("painterlyMaterial", "sheltered_loft_wood");
                }
                else if (memberName.StartsWith("HeroYardShed_HayBinding_", StringComparison.Ordinal))
                {
                    material = PainterlyMaterialLibrary.ForColor("605044", "fabric", sheltered: true);
                    mesh.SetMeta("painterlyMaterial", "sheltered_bale_ties");
                }

                // These four supported hay bales inherited the kit's wood-end
                // slot. Their mesh has no UVs; the material library supplies a
                // local projection that stays attached to each rigid bale.
                if (mesh.Name.ToString() is "HeroYardShed_HayBundle_0_LOD0" or "HeroYardShed_HayBundle_0_LOD1"
                    or "HeroYardShed_HayBundle_1_LOD0" or "HeroYardShed_HayBundle_1_LOD1"
                    or "HeroYardShed_HayBundle_2_LOD0" or "HeroYardShed_HayBundle_2_LOD1"
                    or "HeroYardShed_HayBundle_3_LOD0" or "HeroYardShed_HayBundle_3_LOD1")
                {
                    material = PainterlyMaterialLibrary.ForColor("9b978c", "hay_bundle", sheltered: true);
                    mesh.SetMeta("painterlyMaterial", "hay_bundle");
                    mesh.SetMeta("materialProjection", "object-local triplanar; existing hay fiber texture; dry sheltered storage");
                }

                // Public09: the five authored rear seni infill families are
                // opaque planar sheets with doubleSided=true in the GLB. Their
                // inward faces must retain that source contract after grading.
                if (memberName.EndsWith("_SeniRearRoofInfill_LOD0", StringComparison.Ordinal))
                    material = PainterlyMaterialLibrary.PreserveSourceCulling(material, source);

                mesh.SetSurfaceOverrideMaterial(surface, material);
                rebound++;
            }
        }

        return rebound;
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

        // Pull the existing wet road kit into the authored 5.6 m route read.
        // Width is reduced on the presentation roots only; route collision and
        // clearance continue to come from the authoritative connector/world.

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
            new Vector3(0.84f, 1f, 1f),
            "village_day@arrival-road-crown",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[1].Root!,
            "WetVillageRoadArrivalRutsNear",
            arrivalAnchor - villageDirection * 3.6f + villageSide * 0.45f,
            villageYaw - 2f,
            new Vector3(0.90f, 1f, 1f),
            "village_day@arrival-ruts-near",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[2].Root!,
            "WetVillageRoadArrivalRutsFar",
            arrivalAnchor + villageDirection * 4.0f - villageSide * 0.35f,
            villageYaw + 2f,
            new Vector3(0.90f, 1f, 1f),
            "village_day@arrival-ruts-far",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[3].Root!,
            "WetVillageRoadArrivalShoulderLeft",
            arrivalAnchor + villageSide * 2.62f,
            villageYaw,
            new Vector3(0.86f, 1f, 1f),
            "village_day@arrival-shoulder-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[4].Root!,
            "WetVillageRoadArrivalShoulderRight",
            arrivalAnchor - villageSide * 2.62f,
            villageYaw,
            new Vector3(0.86f, 1f, 1f),
            "village_day@arrival-shoulder-right",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[5].Root!,
            "WetVillageRoadArrivalDitchLeft",
            arrivalAnchor + villageSide * 4.12f,
            villageYaw,
            new Vector3(0.78f, 1f, 1f),
            "village_day@arrival-ditch-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[6].Root!,
            "WetVillageRoadArrivalDitchRight",
            arrivalAnchor - villageSide * 4.12f,
            villageYaw,
            new Vector3(0.78f, 1f, 1f),
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
            new Vector3(0.84f, 1f, 1f),
            "village_day@main-street-crown-variation",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetRutsNear,
            "WetVillageRoadMainStreetRutsNear",
            mainStreetAnchor - villageDirection * 4.4f - villageSide * 0.55f,
            villageYaw - 3f,
            new Vector3(0.90f, 1f, 1f),
            "village_day@main-street-ruts-near",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetRutsFar,
            "WetVillageRoadMainStreetRutsFar",
            mainStreetAnchor + villageDirection * 4.7f + villageSide * 0.50f,
            villageYaw + 3f,
            new Vector3(0.90f, 1f, 1f),
            "village_day@main-street-ruts-far",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetShoulderLeft,
            "WetVillageRoadMainStreetShoulderLeft",
            mainStreetAnchor + villageSide * 2.66f,
            villageYaw + 2f,
            new Vector3(0.86f, 1f, 1f),
            "village_day@main-street-shoulder-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetShoulderRight,
            "WetVillageRoadMainStreetShoulderRight",
            mainStreetAnchor - villageSide * 2.66f,
            villageYaw + 2f,
            new Vector3(0.86f, 1f, 1f),
            "village_day@main-street-shoulder-right",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetDitchLeft,
            "WetVillageRoadMainStreetDitchLeft",
            mainStreetAnchor + villageSide * 4.14f,
            villageYaw + 2f,
            new Vector3(0.78f, 1f, 1f),
            "village_day@main-street-ditch-left",
            WetVillageRoadKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            mainStreetDitchRight,
            "WetVillageRoadMainStreetDitchRight",
            mainStreetAnchor - villageSide * 4.14f,
            villageYaw + 2f,
            new Vector3(0.78f, 1f, 1f),
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
            new Vector3(-5.2f, AgentBAct1HeightField.CollisionGround(-5.2f, -48.5f) - .04f, -48.5f),
            0f,
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
            origin - side * 1.30f - front * 4.13f,
            yaw,
            Vector3.One,
            "fap_clinic@authored-facade",
            FapClinicKitScenePath);
        ApplyHeroWarmWindow(fapAuthoredFacade);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[1].Root!,
            "FapAuthoredEntryPorch",
            origin - side * 1.18f + front * 1.6f,
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
            origin + side * 2.3f + front * 3.1f,
            yaw,
            Vector3.One,
            "fap_clinic@near-entry-bench",
            FapClinicKitScenePath);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[7].Root!,
            "FapAuthoredNoticeBoard",
            origin - side * 7.9f + front * 3.2f,
            yaw,
            Vector3.One,
            "fap_clinic@blank-notice-board",
            FapClinicKitScenePath);
        var rainAwning = AttachAct1ExteriorKitComponent(
            presentation,
            components[8].Root!,
            "FapAuthoredRainAwning",
            origin + side * 9.8f - front * 1.5f,
            yaw - 12f,
            Vector3.One,
            "fap_clinic@service-rain-awning",
            FapClinicKitScenePath);
        // Mount the existing brackets under the shed eave. The old free-
        // standing anchor left the downpipe supporting an airborne canopy.
        rainAwning.GlobalTransform = presentation.GetNode<Node3D>("FapAuthoredServiceShed").GlobalTransform
            * new Transform3D(Basis.Identity, new Vector3(0, -.32f, 1.28f));
        var drainFoot = FindDescendants<MeshInstance3D>(rainAwning)
            .Single(mesh => mesh.Name == "FapRainAwning_DrainFoot_LOD0");
        var drainAt = drainFoot.GlobalPosition;
        drainAt.Y = AgentBAct1HeightField.CollisionGround(drainAt.X, drainAt.Z) + .05f;
        drainFoot.GlobalPosition = drainAt;
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
        HideCorePresentationNode(
            core,
            "FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredBirchShrubMass",
            "existing FapClinicNearBirch owns this grove; duplicate preview mass uses isolated polyhedron crowns");

        // FapEastHorizonAuthoredHouse now stands across the ravine, on the far
        // bank (Act1ConnectedWorld.Ravine.cs).

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
            "continuous authored threshold: wet road contact/path edge -> right-side fence and open gate -> low marker group -> birch/shrub forest transition; distant village masses keep the return depth; route center remains open");
        presentation.SetMeta(
            "thresholdLayout",
            "roadAnchor=(0,0,-1.5); path=(4.2,0,-4.2); markerLow=(6.2,0,-5.2); markerFar=(6.8,0,-9.2); boundary=(6.7,0,-8); birch=(7.8,0,-22)");
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

        AttachAct1ExteriorKitComponent(
            presentation,
            components[2].Root!,
            "ZiratRoadsideDitch",
            roadAnchor + new Vector3(0f, 0f, 0.4f),
            0f,
            Vector3.One,
            "zirat_road@roadside-drainage",
            ZiratRoadsideKitScenePath);

        // Retain contact vegetation and cultural components; only these
        // flat duplicate road prisms conflict with the continuous Road_Main.
        foreach (var relativePath in new[]
                 {
                     "ZiratWetRoadShoulderLeft/WetRoadShoulder_Left/WetRoadShoulder_Left_RoadBand_LOD0",
                     "ZiratWetRoadShoulderLeft/WetRoadShoulder_Left/WetRoadShoulder_Left_Relief_00_LOD0",
                     "ZiratWetRoadShoulderLeft/WetRoadShoulder_Left/WetRoadShoulder_Left_WetEdge_00_LOD0",
                     "ZiratWetRoadShoulderRight/WetRoadShoulder_Right/WetRoadShoulder_Right_RoadBand_LOD0",
                     "ZiratWetRoadShoulderRight/WetRoadShoulder_Right/WetRoadShoulder_Right_Relief_00_LOD0",
                     "ZiratWetRoadShoulderRight/WetRoadShoulder_Right/WetRoadShoulder_Right_WetEdge_00_LOD0",
                     "ZiratRoadsideDitch/RoadsideDitch/RoadsideDitch_Relief_00_LOD0",
                     "ZiratRoadsideDitch/RoadsideDitch/RoadsideDitch_Water_00_LOD0"
                 })
        {
            var roadBand = presentation.GetNodeOrNull<MeshInstance3D>(relativePath)
                ?? throw new InvalidOperationException($"Missing duplicate zirat road mesh: {relativePath}.");
            roadBand.Visible = false;
            roadBand.SetMeta("suppressionReason", "continuous terrain-road kit owns road surface");
        }

        // The kit's 3.55 m roadside offset is removed with its preview origin
        // during extraction. Restore that placement, not a barrier on the axis.
        var culvertAnchor = origin + new Vector3(3.55f, 0f, -2.0f);
        culvertAnchor.Y = (float)AgentBAct1HeightField.Ground(culvertAnchor.X, culvertAnchor.Z);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[3].Root!,
            "ZiratCulvertStoneCluster",
            culvertAnchor,
            0f,
            Vector3.One,
            "zirat_road@culvert-edge",
            ZiratRoadsideKitScenePath);

        // The authored boundary runs along the right side of the route. Its
        // lateral placement keeps the fence/gate out of the first-person road
        // window; the gate is an open presentation cue, never a blocker.
        // Keep the central 4.2 m route clear while bringing the existing
        // authored threshold into the forward/right camera rays. With the
        // kit's local fence axis rotated 90 degrees, this anchor makes the
        // fence a side boundary spanning roughly z -70..-86 rather than a
        // distant strip at the Kara connector.
        var boundaryAnchor = origin + new Vector3(6.7f, 0f, -8.0f);
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

        var markerAnchor = origin + new Vector3(6.2f, 0f, -5.2f);
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
            origin + new Vector3(6.8f, 0f, -9.2f),
            0f,
            Vector3.One * 0.92f,
            "zirat_road@quiet-marker-group-far",
            ZiratRoadsideKitScenePath);
        var pathPlacement = AttachAct1ExteriorKitComponent(
            presentation,
            components[8].Root!,
            "ZiratAuthoredPathEdge",
            origin + new Vector3(4.2f, 0f, -4.2f),
            0f,
            Vector3.One * 0.96f,
            "zirat_road@side-path-edge",
            ZiratRoadsideKitScenePath);

        // Preserve the authored path outline, but seat its entire length on
        // the same terrain as the player instead of a single flat anchor.
        var pathRibbon = pathPlacement.GetNode<MeshInstance3D>(
            "ZiratPathEdge/ZiratPathEdge_PathRibbon_00_LOD0");
        var sourcePath = pathRibbon.Mesh
            ?? throw new InvalidOperationException("Zirat path ribbon has no mesh.");
        var groundedPath = new ArrayMesh();
        for (var surface = 0; surface < sourcePath.GetSurfaceCount(); surface++)
        {
            var arrays = sourcePath.SurfaceGetArrays(surface);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            for (var vertex = 0; vertex < vertices.Length; vertex++)
            {
                var world = pathRibbon.ToGlobal(vertices[vertex]);
                var relief = world.Y - pathPlacement.GlobalPosition.Y;
                world.Y = (float)AgentBAct1HeightField.Ground(world.X, world.Z) + 0.015f + relief;
                vertices[vertex] = pathRibbon.ToLocal(world);
            }
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            using var normals = new SurfaceTool();
            using var warpedSurface = new ArrayMesh();
            warpedSurface.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            normals.CreateFrom(warpedSurface, 0);
            normals.GenerateNormals();
            normals.SetMaterial(sourcePath.SurfaceGetMaterial(surface));
            normals.Commit(groundedPath);
        }
        pathRibbon.Mesh = groundedPath;

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

        foreach (var suffix in new[] { "", "East" })
            HideCorePresentationNode(core,
                "ZiratMemoryField/ZiratRoadsideAuthoredKitPresentation/ZiratAuthoredDistantVillageTransition" + suffix,
                "full-depth lateral village houses replace the kit's near-camera backdrop blocks and canopy lumps");

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
            origin + new Vector3(-5.9f, 0f, -5.8f),
            28f,
            Vector3.One * 0.82f,
            "kara_urman_night@west-ground-breakup-outside-route",
            KaraForestEdgeKitScenePath);
        // Review pass: the flat boulder discs read as crude plates when they
        // sit on the road shoulder; move the cluster deeper between trunks
        // and size it up so it reads as one mossy outcrop.
        AttachAct1ExteriorKitComponent(
            presentation,
            components[9].Root!,
            "KaraMossyBoulderCluster",
            origin + new Vector3(9.5f, 0f, -8.5f),
            -35f,
            Vector3.One * 0.95f,
            "kara_urman_night@east-ground-breakup-outside-route",
            KaraForestEdgeKitScenePath);
        // Ground life on the slopes, outside the 3.5 m route envelope: muted
        // stone clusters at trunk roots plus sheltered understory shrubs.
        AddVisualStoneCluster(presentation, "KaraSlopeStoneEast", origin + new Vector3(8.0f, 0f, 7.0f), 0.45f, "5a5f55");
        AddVisualStoneCluster(presentation, "KaraSlopeStoneDeepWest", origin + new Vector3(-6.5f, 0f, -9.0f), 0.55f, "565b52");
        AddVisualShrub(presentation, "KaraRootShrubEast", origin + new Vector3(7.2f, 0f, 1.0f), 0.85f, "2f4439");
        AddVisualShrub(presentation, "KaraRootShrubWest", origin + new Vector3(-7.8f, 0f, -5.0f), 0.8f, "34443b");
        AttachAct1ExteriorKitComponent(
            presentation,
            components[10].Root!,
            "KaraCrookedStump",
            origin + new Vector3(-5.4f, 0f, -7.2f),
            10f,
            Vector3.One * 0.88f,
            "kara_urman_night@west-ground-landmark-outside-route",
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

        // The shared terrain now owns these low slopes. Keep exposed roots
        // and deadwood, but retire their separate polygonal ground pedestals.
        foreach (var side in new[] { "Left", "Right" })
        foreach (var suffix in new[] { "GroundApron_00_LOD0", "GroundApron_01_LOD0",
                     "ThresholdRise_00", "ThresholdRise_01", "SplayedShoulder_00",
                     "SplayedShoulder_01", "RidgeContact_00", "UnderstoryFan_00",
                     "MossPocket_00", "MossPocket_01", "MossPocket_02" })
        {
            var mesh = presentation.GetNode<MeshInstance3D>(
                $"KaraForestBank{side}/ForestBank_{side}/ForestBank_{side}_{suffix}");
            mesh.Visible = false;
            mesh.SetMeta("suppressionReason", "shared terrain owns continuous forest shoulders");
        }
        foreach (var side in new[] { "Left", "Right" })
        foreach (var suffix in new[] { "ContactLobe_00_LOD0", "ContactLobe_01_LOD0",
                     "LeafLitterPatch_00", "LeafLitterPatch_01", "RootPlate_00", "UnderstoryFan_00" })
            presentation.GetNode<MeshInstance3D>(
                $"KaraRootWall{side}/RootWall_{side}/RootWall_{side}_{suffix}").Visible = false;

        // Soil and roots now belong to the shared slopes and rooted tree groups,
        // not rows of detached plates along the road.
        foreach (var mesh in FindDescendants<MeshInstance3D>(presentation).ToArray())
        {
            var name = mesh.Name.ToString();
            if (!name.Contains("_ExposedRoot_", StringComparison.Ordinal)
                && !name.Contains("_RootFinger_", StringComparison.Ordinal)
                && !name.Contains("_RootTangle_", StringComparison.Ordinal)
                && !name.Contains("_RootFork_", StringComparison.Ordinal)) continue;
            mesh.Visible = false;
        }
        foreach (var side in new[] { "Left", "Right" })
        foreach (var suffix in new[] { "Deadwood_00", "Deadwood_01" })
            presentation.GetNode<MeshInstance3D>(
                $"KaraForestBank{side}/ForestBank_{side}/ForestBank_{side}_{suffix}").Visible = false;
        // These enclosing shell crowns are replaced by open branch-canopy stands.
        foreach (var name in new[] { "KaraMixedTreeClusterLeft", "KaraMixedTreeClusterRight",
                     "KaraCrookedPineMass", "KaraBirchEdgeMass" })
            presentation.GetNode<Node3D>(name).Visible = false;
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
            ["LeafLitter"] = PainterlyMaterialLibrary.ForColor("f0f4f8", "snow_ground"),
            ["PineBark"] = PainterlyMaterialLibrary.ForColor("40352d", "bark_pine"),
            ["WeatheredWood"] = PainterlyMaterialLibrary.ForColor("55493c", "wood"),
            ["CutWood"] = PainterlyMaterialLibrary.ForColor("8b7155", "wood"),
            ["PineFoliage"] = PainterlyMaterialLibrary.ForColor("30483f", "foliage"),
            ["FoliageBlueGreen"] = PainterlyMaterialLibrary.ForColor("48553f", "foliage"),
            ["BirchBark"] = PainterlyMaterialLibrary.ForColor("68705a", "bark_birch"),
            ["BirchLeaves"] = PainterlyMaterialLibrary.ForColor("596047", "leaf_birch"),
            ["Understory"] = PainterlyMaterialLibrary.ForColor("3c4d3e", "foliage"),
            ["RootDark"] = PainterlyMaterialLibrary.ForColor("3f332a", "wood_bark"),
            ["MossGreen"] = PainterlyMaterialLibrary.ForColor("c8d1d6", "snow_grass"),
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
            // Give the actual crown a darker, calmer value while the
            // shoulders/ditches use the damp-earth response. This is the
            // visible road/verge break; the presentation scales above only
            // keep the existing kit inside the authoritative route clearance.
            ["WetRoad_RutDark"] = PainterlyMaterialLibrary.ForColor("2f3934", "earth"),
            ["WetRoad_MutedOchre"] = PainterlyMaterialLibrary.ForColor("4b4d43", "earth"),
            ["WetRoad_WornLight"] = PainterlyMaterialLibrary.ForColor("59584b", "earth"),
            ["MuddyShoulder_ClayBreak"] = PainterlyMaterialLibrary.ForColor("746b57", "wet_ground"),
            ["MuddyShoulder_WetBrown"] = PainterlyMaterialLibrary.ForColor("5c5549", "wet_ground"),
            ["Ditch_DampGreenBrown"] = PainterlyMaterialLibrary.ForColor("52624e", "wet_ground"),
            ["Culvert_StoneShadow"] = PainterlyMaterialLibrary.ForColor("50574c", "stone"),
            ["Culvert_WeatheredStone"] = PainterlyMaterialLibrary.ForColor("737467", "stone"),
            ["Puddle_MutedGlint"] = PainterlyMaterialLibrary.ForColor("40564f", "water"),
            ["Puddle_ShallowBlueGreen"] = PainterlyMaterialLibrary.ForColor("354943", "water"),
            ["Ditch_StillWater"] = PainterlyMaterialLibrary.ForColor("33463f", "water"),
            // The kit's organic verge family kept raw PBR albedo outside this
            // table and read as a conspicuous mint-blue mass beside the west
            // street fence (probe-verified FernShrubBreak owner). Bind them to
            // the same muted bog palette as the native foliage owners.
            ["Shrub_BlueGreen"] = PainterlyMaterialLibrary.ForColor("48553f", "foliage"),
            ["Fern_MossGreen"] = PainterlyMaterialLibrary.ForColor("827a65", "grass"),
            ["Fern_LeafLight"] = PainterlyMaterialLibrary.ForColor("918875", "grass"),
            ["Grass_SedgeMuted"] = PainterlyMaterialLibrary.ForColor("827a65", "grass"),
            ["Grass_SedgeDryTips"] = PainterlyMaterialLibrary.ForColor("918875", "grass"),
            ["Moss_WetOlive"] = PainterlyMaterialLibrary.ForColor("4c5a45", "foliage"),
            ["Fence_DampWood"] = PainterlyMaterialLibrary.ForColor("594a39", "wood_fence"),
            ["Fence_CutWood"] = PainterlyMaterialLibrary.ForColor("6d5c48", "wood")
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

    private void BuildAct1AuthoredExteriorKit(Node3D core)
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

        // The six canonical roots and three full-volume parcel variants are
        // resolved once from the neutral source board. Their preview-board
        // origins are removed before stable placement nodes own world-space
        // translation, yaw and scale.
        var babaiApproachFacade = AttachAct1ExteriorKitComponent(
            presentation,
            components[9].Root!,
            "BabaiApproachDwellingFacade",
            housePlacement.Origin + new Vector3(0f, 0f, -3.4f),
            houseYaw,
            Vector3.One,
            "house_old_pc@babai-approach");
        _zoneInstances["house_old_pc"].GlobalTransform =
            StyleBenchmarkInteriorFactory.TransformFromFacade(babaiApproachFacade);
        ApplyHeroWarmWindow(babaiApproachFacade);
        // The playable house keeps its authored closed street door visible. Hiding
        // the leaf left an open hole, and because the interior zone sits behind the
        // same facade the player could see the room through it (author-reported
        // 2026-09-14: "у дома нет вообще никакой двери, я вижу, что изнутри
        // происходит"). The leaf is presentation-only, so the entry interaction
        // still owns the transition.
        var streetDoorLeaf = FindDescendants<MeshInstance3D>(babaiApproachFacade)
            .Single(mesh => mesh.Name == "HeroHouse_StreetDoorClosed_LOD0");
        streetDoorLeaf.Visible = true;
        streetDoorLeaf.MaterialOverride = PainterlyMaterialLibrary.ForColor("5a4433", "wood");
        babaiApproachFacade.SetMeta("streetDoorPolicy", "authored closed leaf visible; entry stays an interaction");
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
        var babaiGate = AttachAct1ExteriorKitComponent(
            presentation,
            components[3].Root!,
            "BabaiYardAuthoredGate",
            housePlacement.Origin + houseApproach * 6.0f,
            houseYaw,
            Vector3.One * 0.92f,
            "house_old_pc@yard-entry");
        foreach (var mesh in FindDescendants<MeshInstance3D>(babaiGate))
        {
            if (mesh.Mesh is not null && mesh.Name.ToString().Contains("Gate_", StringComparison.Ordinal))
            {
                mesh.MaterialOverride = PainterlyMaterialLibrary.ForColor("718078", "wood_carved");
            }
        }
        AttachAct1ExteriorKitComponent(
            presentation,
            components[4].Root!,
            "BabaiYardAuthoredWoodpile",
            housePlacement.Origin + houseSide * -5.7f - houseApproach * 3.0f,
            houseYaw + 90f,
            Vector3.One * 1.05f,
            "house_old_pc@yard-firewood");
        AddVisualCanopy(presentation, "BabaiFirewoodShelter",
            housePlacement.Origin + houseSide * -5.7f - houseApproach * 3.0f,
            2.7f, 2.2f, houseYaw + 90f, "626b66", "605044");
        // Match the legacy FirewoodCollision with the same repaired log kit.
        // The physics owner remains in village_day; only its visible counterpart is placed here.
        var streetFirewood = presentation.GetNode<Node3D>("BabaiYardAuthoredWoodpile").Duplicate() as Node3D
            ?? throw new InvalidOperationException("Cannot duplicate the authored firewood presentation.");
        streetFirewood.Name = "MainStreetPhysicalFirewood";
        presentation.AddChild(streetFirewood);
        streetFirewood.GlobalPosition = new(5f, AgentBAct1HeightField.CollisionGround(5f, 2.3f) - .04f, 2.3f);
        streetFirewood.RotationDegrees = Vector3.Zero;
        streetFirewood.Scale = new(.9f, 1.25f, 1.1f);
        AddVisualSnowShovel(presentation, "BabaiSnowShovel", new(-29.7f, 0f, 4.3f), -20f);
        AddVisualLandformSurface(presentation, "BabaiClearedDoorApproach", 1.15f, .018f, 3.1f,
            new(-28f, .015f, 5.0f), "cbd3d8", "snow_trampled", 0f, true);
        AttachAct1ExteriorKitComponent(
            presentation,
            components[5].Root!,
            "MainStreetArrivalAuthoredWell",
            new Vector3(-4.9f, 0f, 4.6f),
            0f,
            Vector3.One * 0.90f,
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
                "VillageParcel_VariantA_TimberGable",
                "ArrivalWestNearAuthoredTimberGableParcel",
                new(-10.0f, 0f, 25.8f),
                176f,
                Vector3.One,
                "village_day@arrival-west-near-timber-gable-parcel"));
        AddAct1AuthoredExteriorParcel(
            arrivalParcels,
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "ArrivalEastNearAuthoredFacade",
                new(10.0f, 0f, 26.2f),
                184f,
                Vector3.One,
                "village_day@arrival-east-near-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalEastNearAuthoredShed",
                new(9.2f, 0f, 25.0f),
                184f,
                Vector3.One * 0.62f,
                "village_day@arrival-east-near-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalEastNearAuthoredFence",
                new(7.8f, 0f, 23.1f),
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
                new(-7.2f, 0f, 3.2f),
                90f,
                Vector3.One * 0.95f,
                "village_day@arrival-forward-west-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "Gate_CrookedTimber",
                "ArrivalForwardWestGate",
                new(-6.4f, 0f, -3.2f),
                4f,
                Vector3.One * 0.72f,
                "village_day@arrival-forward-west-gate"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalForwardWestFence",
                new(-7.6f, 0f, -2.1f),
                8f,
                new Vector3(1.18f, 0.64f, 1f),
                "village_day@arrival-forward-west-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalForwardWestShed",
                new(-11.4f, 0f, -10.8f),
                8f,
                Vector3.One * 0.46f,
                "village_day@arrival-forward-west-shed"));
        AddAct1AuthoredExteriorParcel(
            arrivalParcels,
            new Act1ExteriorParcelComponentPlacement(
                "VillageParcel_VariantB_PlasterAnnex/VillageParcel_VariantB_PlasterAnnex_Dwelling",
                "ArrivalForwardEastFacade",
                new(7.8f, 0f, -3.6f),
                -4f,
                Vector3.One * 0.98f,
                "village_day@arrival-forward-east-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "ArrivalForwardEastShed",
                new(11.2f, 0f, -13.4f),
                -8f,
                Vector3.One * 0.44f,
                "village_day@arrival-forward-east-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "ArrivalForwardEastFence",
                new(8.4f, 0f, -6.0f),
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
                Vector3.One * 0.95f,
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
                    "VillageParcel_VariantC_BanyaYard",
                    "BabaiYardWestDepthBanyaYardParcel",
                    authoredHousePlacement.Origin + authoredHouseSide * -10.0f - authoredHouseApproach * 3.8f,
                    authoredHouseYaw + 12f,
                    Vector3.One * 0.60f,
                    "house_old_pc@west-yard-depth-banya-yard-parcel"));
            AddAct1AuthoredExteriorParcel(
                babaiYardParcels,
                new Act1ExteriorParcelComponentPlacement(
                    "Well_YardLandmark",
                    "BabaiYardAuthoredWell",
                    new(-20.0f, 0f, 17.0f),
                    authoredHouseYaw,
                    Vector3.One * 0.82f,
                    "house_old_pc@depth-yard-well-landmark"),
                new Act1ExteriorParcelComponentPlacement(
                    "FenceSegment_RoughPicket",
                    "BabaiYardEastDepthFence",
                    new(-10.0f, 0f, 12.0f),
                    authoredHouseYaw - 90f,
                    new Vector3(1.08f, 0.54f, 1f),
                    "house_old_pc@forward-right-yard-boundary"));
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
            // The former ZiratVillageEdgeWestFacade duplicated the standing
            // full-depth ZiratVillageMemoryHouseWest core volume right behind
            // it; the fence boundary below still owns the edge read.
            AddAct1AuthoredExteriorParcel(
                ziratEdgeParcels,
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
                authoredZiratPlacement.Origin + new Vector3(15.8f, 0f, 21.4f),
                -90f,
                Vector3.One * 1.15f,
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

            // Keep the cemetery edge separate from inhabited plots. The
            // former .34-scale forward house/shed were camera props.
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
            // The usable household shed occupies the former overlapping backdrop
            // parcel at (-21.6,-4.7); keep the separate neighbouring shed below.
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "PerimeterWestStreetShed",
                new(-21.6f, 0f, -13.4f),
                84f,
                Vector3.One * 1.15f,
                "village_day@perimeter-west-street-shed"));
        AddAct1AuthoredExteriorParcel(
            perimeterParcels,
                new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "PerimeterEastStreetShed",
                new(18.4f, 0f, -11.0f),
                0f,
                Vector3.One * 0.95f,
                "village_day@perimeter-east-street-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket", "EastStreetPlotFrontWest",
                new(18.2f, 0f, -17f), 0f, new(1.15f, 0.82f, 1f),
                "village_day@east-street-plot-front-west"));
        AddAct1AuthoredExteriorParcel(
            perimeterParcels,
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket", "EastStreetPlotFrontEast",
                new(28f, 0f, -17f), 0f, new(1.15f, 0.82f, 1f),
                "village_day@east-street-plot-front-east"));
        // MainStreetEastNearMidHouse owns this holding, not two miniature
        // camera-facing dwellings. Leave an open entrance between front runs.
        using var holdingPath = new Curve3D();
        holdingPath.AddPoint(new(19f, 0f, -22.5f), Vector3.Zero, new(2f, 0f, 1.4f));
        holdingPath.AddPoint(new(24f, 0f, -17f), new(0f, 0f, -2f), new(0f, 0f, 2f));
        holdingPath.AddPoint(new(23f, 0f, -12f), new(.7f, 0f, -1.5f), new(-.7f, 0f, 1.5f));
        holdingPath.AddPoint(new(21f, 0f, -8.5f), new(.5f, 0f, -1.2f));
        AddVisualLandformSurface(perimeterParcels, "EastStreetPlotAccessPath",
            1.1f, .025f, holdingPath.GetBakedLength(), new(0f, .02f, 0f),
            "cbd3d8", "snow_trampled", 0f, true, holdingPath);
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
                new(-24.0f, 0f, 24.0f),
                "village_day@west-arrival-mid-parcel",
                "mid-field house and broken fence; arrival side turn"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "WestArrivalMidFacade",
                Vector3.Zero,
                82f,
                Vector3.One * 0.64f,
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
                new(27.0f, 0f, 27.0f),
                "village_day@east-arrival-mid-parcel",
                "mid-field house and yard edge; arrival side turn"),
            new Act1ExteriorParcelComponentPlacement(
                "DwellingFacade_TimberPlaster",
                "EastArrivalMidFacade",
                Vector3.Zero,
                -101f,
                Vector3.One * 0.56f,
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
        // This plot is occupied by the playable HeroHouse. A second backdrop
        // dwelling here put its side walls and footing through the real room.
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "EastStreetMidParcel",
                new(25.3f, 0f, -19.2f),
                "village_day@east-street-mid-parcel",
                "staggered side house closes the main-street side field"),
            new Act1ExteriorParcelComponentPlacement(
                "VillageParcel_VariantA_TimberGable/VillageParcel_VariantA_TimberGable_Dwelling",
                "EastStreetMidFacade",
                Vector3.Zero,
                -82f,
                Vector3.One * 0.88f,
                "village_day@east-street-mid-facade"),
            // H1 biography: a working household. Its yard is the one the player
            // walks through, so its firewood stack sits by the house's south
            // gable where the delivery path actually ends, clear of the walking
            // line and of the holding's fence run.
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs",
                "EastStreetMidWoodpile",
                new(1.2f, 0f, -4.6f),
                14f,
                Vector3.One * 0.82f,
                "village_day@east-street-mid-firewood"),
            // H5: this yard gets its own fit. The authored (0,-4.5) offset would
            // drop the boundary straight across the east-holding walking loop at
            // x ~ 20.4, so the kit is pushed east of the house instead, where its
            // nearest corner stays ~1 m clear of the FAP road envelope and the
            // whole rectangle sits east of the holding's walking legs.
            new Act1ExteriorParcelComponentPlacement(
                "VillageParcel_VariantA_TimberGable/VillageParcel_VariantA_TimberGable_Yard",
                "EastStreetMidYard",
                new(6.2f, 0f, -0.2f),
                -82f,
                Vector3.One * 0.88f,
                "village_day@east-street-mid-yard"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "WestReturnMidParcel",
                new(-30.0f, 0f, -36.5f),
                "zirat_road@west-return-mid-parcel",
                "low return-street house and fence; quiet transition toward zirat"),
            new Act1ExteriorParcelComponentPlacement(
                "VillageParcel_VariantB_PlasterAnnex/VillageParcel_VariantB_PlasterAnnex_Dwelling",
                "WestReturnMidFacade",
                Vector3.Zero,
                88f,
                Vector3.One * 0.90f,
                "zirat_road@west-return-mid-facade"),
            // H5: the dwelling's own variant boundary instead of the legacy
            // picket run it used to duplicate. Authored yard offset is (0,-4.5)
            // in kit space, rotated into parcel space by the dwelling's yaw; a
            // kept gate on the return street fits this yard's seasonal
            // biography. Sits 26 m west of the return road, off every walking
            // line.
            new Act1ExteriorParcelComponentPlacement(
                "VillageParcel_VariantB_PlasterAnnex/VillageParcel_VariantB_PlasterAnnex_Yard",
                "WestReturnMidYard",
                new(4.05f, 0f, 0.14f),
                88f,
                Vector3.One * 0.90f,
                "zirat_road@west-return-mid-yard"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "EastReturnMidParcel",
                // The former backdrop anchor cuts through the full clinic.
                // Move this existing dwelling and its shed together onto the
                // next holding; foundation supports follow their new ground.
                new(36.0f, 0f, -54.0f),
                "zirat_road@east-return-mid-parcel",
                "low return-street house and shed; quiet transition toward zirat"),
            new Act1ExteriorParcelComponentPlacement(
                "VillageParcel_VariantC_BanyaYard/VillageParcel_VariantC_BanyaYard_Dwelling",
                "EastReturnMidFacade",
                Vector3.Zero,
                -88f,
                Vector3.One * 0.90f,
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
                "wood storage beside the inhabited house and playable yard shed"),
            // The playable shed now occupies this yard. The former backdrop
            // facade cut through its loft with a second roof and gable collider.
            // Keep the separate woodpile; the actual house and shed own the view.
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs",
                "WestStreetNearWoodpile",
                // Keep the logs inside the storage yard: the former z=3.5
                // placed them directly across the authored path to the house.
                new(3.6f, 0f, .8f),
                10f,
                Vector3.One * 0.66f,
                "village_day@west-street-near-woodpile"));
        // The FAP-branch holding has one full-size house and shared front
        // boundary above; do not recreate an undersized near-camera parcel.

        // Close the four lateral review sectors with already-cached authored
        // facade families. These are distant presentation silhouettes only;
        // route, collision and narrative owners remain untouched.
        // FapRightFieldHouse owns this full-size plot; the old miniature
        // horizon house expanded into its footprint after the kit upgrade.
        AddVisualFenceRun(perimeterParcels, "EastStreetFarFence", new(35.0f, 0f, -22.0f), new(41.0f, 0f, -22.0f));
        // FapEastViewHouse moved across the ravine (Act1ConnectedWorld.Ravine.cs).
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
                new(33.5f, 0f, 18.0f),
                "village_day@east-street-horizon",
                "distant east boundary closes the main-street lateral field"),
            new Act1ExteriorParcelComponentPlacement(
                "VillageParcel_VariantA_TimberGable/VillageParcel_VariantA_TimberGable_Dwelling",
                "EastStreetHorizonFacade",
                Vector3.Zero,
                -106f,
                Vector3.One * 0.88f,
                "village_day@east-street-horizon-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "EastStreetHorizonFence",
                new(4.6f, 0f, 3.8f),
                8f,
                new(1.30f, 0.52f, 1f),
                "village_day@east-street-horizon-fence"));
        AddVisualTree(perimeterParcels, "EastStreetHorizonConifer", new(55.0f, 0f, 28.0f), 9.4f, VegetationStyle.Conifer, "30483f");

        // The former 0.44/0.48-scale VariantB parcel read as a miniature
        // house between full-sized dwellings. Replace it with the existing
        // full-scale gable shed family, a coherent picket service boundary
        // with a yard-side opening, and firewood dressing. The shed door
        // face (+Z local at yaw 0; door/trim/window meshes at z ~+1.2/+1.4)
        // turns toward the house-path and babai depth cameras; the mount
        // keeps HousePathAxis and the hero threshold open. Presentation-only;
        // collision/navigation/interaction owners remain untouched.
        // Review-corrected layout: the service yard moved north of the drawn
        // house-path corridor (connector half-width 2.4 m around z(x)), the
        // west/rear runs were dropped because the existing kit fence and the
        // neighbour fence at z=16 already own those edges, and the remaining
        // L-boundary stays >=0.3 m off the path envelope.
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "BabaiEastDepthParcel",
                new(-5.5f, 0f, 13.5f),
                "house_old_pc@east-depth-service-yard",
                "staggered east service yard; full-scale shed, firewood and boundary close the depth view without narrowing the house path"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "BabaiEastDepthServiceShed",
                new(0.6f, 0f, 0.0f),
                -80f,
                Vector3.One * 0.85f,
                "house_old_pc@east-depth-service-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs",
                "BabaiEastDepthServiceWoodpile",
                new(1.2f, 0f, 1.0f),
                8f,
                Vector3.One,
                "house_old_pc@east-depth-service-firewood"));
        AddVisualFenceRun(perimeterParcels, "BabaiEastDepthServiceBoundaryEast", new(-2.5f, 0f, 12.0f), new(-2.5f, 0f, 15.8f), true);
        AddVisualFenceRun(perimeterParcels, "BabaiEastDepthServiceBoundaryNorth", new(-6.2f, 0f, 15.8f), new(-2.5f, 0f, 15.8f), true);

        AddVisualTree(perimeterParcels, "ZiratEastHorizonBirch", new(38.0f, 0f, -84.0f), 8.4f, VegetationStyle.Birch, "596047");

        // The fixed MainStreet right turn still looked across the road fence
        // into an unbounded field. A single staggered parcel at the actual
        // camera ray closes that read with a house, shed and broken boundary;
        // it stays outside the route envelope and reuses the existing authored
        // village family instead of adding another procedural wall.
        // Review pass: this parcel's facade volume intersected the adjacent
        // full-volume MainStreetEastNearMidHouse (two building shells merged
        // at the road side). The near-mid house owns the closure read; the
        // parcel keeps its shed, broken fence and trees as its yard.
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                perimeterParcels,
                "EastStreetSideClosureParcel",
                new(25.0f, 0f, -5.5f),
                "village_day@east-street-side-closure",
                "near-mid east yard of the near-mid holding; shed and broken fence close the right-turn field while preserving the road window"),
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

        // Retain a quiet, open cemetery boundary beside the full-depth houses;
        // these two edge trees are not a substitute for inhabited plots.
        AddVisualTree(perimeterParcels, "ZiratEastFieldEdgeBirch", new(31.5f, 0f, -64.5f), 7.1f, VegetationStyle.Birch, "596047");
        AddVisualTree(perimeterParcels, "ZiratEastFieldEdgeConifer", new(34.0f, 0f, -71.0f), 8.0f, VegetationStyle.Conifer, "30483f");

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
        HideCorePresentationNode(
            core,
            "Arrival/ArrivalWestNearSetbackGate",
            "authored arrival west gate replaces the overlapping primitive near-setback gate");
        foreach (var (relativePath, reason) in new (string RelativePath, string Reason)[]
                 {
                     ("Arrival/ArrivalWestNearSetbackFenceRail", "authored arrival west fence replaces the overlapping primitive near-setback rail"),
                     ("Arrival/ArrivalWestNearSetbackFencePost0", "authored arrival west fence replaces the overlapping primitive near-setback post"),
                     ("Arrival/ArrivalWestNearSetbackFencePost1", "authored arrival west fence replaces the overlapping primitive near-setback post"),
                     ("Arrival/ArrivalWestNearSetbackFencePost2", "authored arrival west fence replaces the overlapping primitive near-setback post"),
                     ("Arrival/ArrivalWestNearSetbackFencePost3", "authored arrival west fence replaces the overlapping primitive near-setback post"),
                     ("Arrival/ArrivalEastNearSetbackFenceRail", "authored arrival east fence replaces the overlapping primitive near-setback rail"),
                     ("Arrival/ArrivalEastNearSetbackFencePost0", "authored arrival east fence replaces the overlapping primitive near-setback post"),
                     ("Arrival/ArrivalEastNearSetbackFencePost1", "authored arrival east fence replaces the overlapping primitive near-setback post"),
                     ("Arrival/ArrivalEastNearSetbackFencePost2", "authored arrival east fence replaces the overlapping primitive near-setback post"),
                     ("Arrival/ArrivalEastNearSetbackFencePost3", "authored arrival east fence replaces the overlapping primitive near-setback post")
                 })
        {
            HideCorePresentationNode(core, relativePath, reason);
        }

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
        // MainStreetEastNeighborFacade owns the plot at (9.8,-10.5).
        // A separate lateral-camera parcel here intersected its house/yard.

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
                "village_day@arrival-reverse-west-domestic-facade"),
            // H5: a small front boundary in the third variant's language, so
            // the two reverse dwellings do not read as twins. Offset is the
            // authored (0,-4.5) rotated by this facade's yaw and scaled to it;
            // the arrival route centre stays open on the far side.
            new Act1ExteriorParcelComponentPlacement(
                "VillageParcel_VariantC_BanyaYard/VillageParcel_VariantC_BanyaYard_Yard",
                "ArrivalReverseWestDomesticYard",
                new(0.16f, 0f, -2.24f),
                176f,
                Vector3.One * 0.50f,
                "village_day@arrival-reverse-west-domestic-yard"));
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
                new(6.5f, 0f, 3.0f),
                -90f,
                Vector3.One * 1.15f,
                "village_day@arrival-reverse-east-domestic-shed"));
        AddAct1AuthoredExteriorParcel(
            AddAct1ExteriorParcelSubmount(
                village,
                "ArrivalLeftHorizonDomesticParcel",
                new(-24.0f, 0f, 27.5f),
                "village_day@arrival-left-horizon-domestic-facade",
                "mid/far left village-edge dwelling silhouette; lateral route window remains open"),
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
                new(24.0f, 0f, 29.0f),
                "village_day@arrival-right-horizon-domestic-fence",
                "mid/far right village-edge fence silhouette; lateral route window remains open"),
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
                "fap_clinic@reverse-west-domestic-facade"),
            // H1 biography: a partially renovated household - the older plaster
            // body with a newer outbuilding added beside it, which is what
            // reads as "this family is still working on the place". Sited on
            // the dwelling's flank, clear of the cemetery route.
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "FapReverseWestDomesticShed",
                new(2.6f, 0f, -1.4f),
                84f,
                Vector3.One * 0.55f,
                "fap_clinic@reverse-west-domestic-outbuilding"));
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
        // The clinic's service yard already owns a full-scale shed. The
        // half-scale camera prop in front of its approach read as a toy hut.
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
                ("ForestBank_Right", "KaraClosureEastBank", new Vector3(13.0f, 0f, -115.0f), 18f, Vector3.One * 0.76f, "kara_urman_night@east-lateral-bank"),
                ("MixedTreeCluster_Right", "KaraClosureEastMixedTrees", new Vector3(15.0f, 0f, -122.0f), 22f, Vector3.One * 0.76f, "kara_urman_night@east-lateral-mixed-trees"),
                ("RootWall_Right", "KaraClosureEastRootWall", new Vector3(12.0f, 0f, -119.0f), 20f, Vector3.One * 0.70f, "kara_urman_night@east-lateral-root-wall")
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
        // The west anchor now carries one full-volume plaster-annex parcel;
        // the east-side canonical components retain the smaller staggered
        // boundary and outbuilding rhythm without narrowing the road window.
        // ArrivalForwardWestFacade owns this holding. The former WestParcel
        // was a second house in its expanded footprint, not another property.

        var mainStreetEast = AddAct1ExteriorParcelSubmount(
            mainStreetParcels,
            "EastParcel",
            new(9.8f, 0f, -10.5f),
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
                new(-6.6f, 0f, 2.0f),
                90f,
                new Vector3(0.55f, 0.70f, 1f),
                "village_day@main-street-east-partial-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "Gate_CrookedTimber",
                "MainStreetEastNeighborGate",
                new(-6.6f, 0f, 5.2f),
                90f,
                new Vector3(0.72f, 1.15f, 0.74f),
                "village_day@main-street-east-gate"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "MainStreetEastNeighborShed",
                new(-4.5f, 0f, 8.5f),
                mainStreetYaw + 11f,
                Vector3.One * 0.95f,
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
        // forward camera rays at roughly x +/-7..10 without narrowing the road.
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
                new(-8.8f, 0f, -12.8f),
                DirectionYaw(-mainStreetSide) - 2f,
                Vector3.One * 0.95f,
                "village_day@main-street-forward-west-facade"),
            new Act1ExteriorParcelComponentPlacement(
                "Gate_CrookedTimber",
                "MainStreetForwardWestGate",
                new(-6.5f, 0f, -8.8f),
                0f,
                Vector3.One * 0.68f,
                "village_day@main-street-forward-west-gate"),
            new Act1ExteriorParcelComponentPlacement(
                "FenceSegment_RoughPicket",
                "MainStreetForwardWestFence",
                new(-10.5f, 0f, -8.8f),
                0f,
                new Vector3(1.15f, 0.62f, 1f),
                "village_day@main-street-forward-west-fence"),
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low",
                "MainStreetForwardWestShed",
                new(-17.0f, 0f, -14.8f),
                mainStreetYaw - 8f,
                Vector3.One * 0.62f,
                "village_day@main-street-forward-west-shed"));

        // The same EastParcel is visible from both directions. Do not add a
        // second house/fence/shed merely to serve the forward camera.

        // The connective parcel is intentionally deeper than the MainStreet
        // pair. Its side shed and fence break up the reverse view without
        // forming a second wall across the route.
        var connectiveDeep = AddAct1ExteriorParcelSubmount(
            connectiveStreetParcels,
            "DeepParcel",
            new(-11.5f, 0f, -29.5f),
            "village_day@connective-street-deep-parcel",
            "deep west parcel; road-facing facade recedes toward the return route");
        AddAct1AuthoredExteriorParcel(
            connectiveDeep,
            new Act1ExteriorParcelComponentPlacement(
                "VillageParcel_VariantC_BanyaYard",
                "ConnectiveStreetDeepBanyaYardParcel",
                Vector3.Zero,
                DirectionYaw(-returnSide) - 12f,
                new Vector3(0.60f, 0.64f, 0.60f),
                "village_day@connective-street-deep-banya-yard-parcel"));

        // ReturnStreet is a low, distant transition only. It stays on the
        // outer west parcel, away from the connector centerline and far ahead
        // of the zirat landmark, so a reverse turn still reads road -> zirat.
        var returnDistant = AddAct1ExteriorParcelSubmount(
            returnStreetParcels,
            "DistantParcel",
            new(-11.0f, 0f, -48.5f),
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
                new(4.1f, 0f, 4.0f),
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
            new(-12.0f, 1.0f, -52.7f),
            0.26f,
            90f);
        AddAuthoredHouse(
            returnStreetParcels,
            "ReturnWestBanyaSilhouette",
            new(-9.0f, 0.78f, -45.5f),
            0.20f,
            84f);
        AddAuthoredHouse(
            returnStreetParcels,
            "ReturnEastFarHouse2Silhouette",
            new(11.5f, 0.92f, -53.0f),
            0.24f,
            -90f);
        AddAuthoredHouse(
            connectiveStreetParcels,
            "ConnectiveEastHouseA6Silhouette",
            new(8.5f, 0.86f, -32.0f),
            0.22f,
            -90f);
        AddAuthoredHouse(
            returnStreetParcels,
            "ReturnEastHouseA8Silhouette",
            new(8.5f, 0.84f, -42.5f),
            0.22f,
            -90f);

        var houseExteriorWest = AddAct1ExteriorParcelSubmount(
            houseExteriorParcels,
            "WestSideParcel",
            new(-38.0f, (float)AgentBAct1HeightField.Ground(-38f, -6f) + 0.03f, -6.0f),
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
                new(1.4f, 0f, 2.8f),
                0f,
                new Vector3(0.60f, 0.58f, 1f),
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
        if (assetSource == WetVillageRoadKitScenePath
            && (component.Name.ToString().StartsWith("RoadCrown_", StringComparison.Ordinal)
                || component.Name.ToString().StartsWith("RoadRuts_", StringComparison.Ordinal)
                || component.Name.ToString().StartsWith("MuddyShoulder_", StringComparison.Ordinal)
                || component.Name.ToString().StartsWith("RoadsideDitch_", StringComparison.Ordinal)))
        {
            // Keep imported contracts, but render only the continuous terrain-
            // following road owner instead of stacked flat preview-board tiles.
            component.Visible = false;
            component.SetMeta("suppressionReason", "continuous terrain-road kit owns road surfaces");
        }
        if (assetSource is VillageExteriorKitScenePath or FapClinicKitScenePath)
        {
            var groundAnchor = placement.GlobalPosition;
            var yardProp = assetSource == FapClinicKitScenePath
                || component.Name.ToString().StartsWith("Woodpile_", StringComparison.Ordinal)
                || component.Name.ToString().StartsWith("FenceSegment_", StringComparison.Ordinal)
                || component.Name.ToString().StartsWith("Gate_", StringComparison.Ordinal)
                || component.Name.ToString().StartsWith("Well_", StringComparison.Ordinal);
            groundAnchor.Y = yardProp ? AgentBAct1HeightField.CollisionGround(groundAnchor.X, groundAnchor.Z) - .04f
                : (float)AgentBAct1HeightField.Ground(groundAnchor.X, groundAnchor.Z) + .03f;
            placement.GlobalPosition = groundAnchor;
            placement.SetMeta("groundContactPolicy", yardProp ? "yard prop root embedded 4cm in physical terrain" : "dwelling preserves existing threshold alignment");
            if (!yardProp)
            {
                // Dwellings sit on the analytic height because the hero facade and
                // the authored door portal share that threshold value; switching
                // one of them to the jittered collision mesh would pull the door
                // out of its own doorway. Record how far the two surfaces actually
                // differ, so a reported floating house is a measured number rather
                // than an assumption about which function is "right".
                var thresholdDelta = Mathf.Abs(
                    (float)AgentBAct1HeightField.Ground(groundAnchor.X, groundAnchor.Z)
                    - (float)AgentBAct1HeightField.CollisionGround(groundAnchor.X, groundAnchor.Z));
                _dwellingThresholdPlacements++;
                if (thresholdDelta > _dwellingThresholdWorst)
                {
                    _dwellingThresholdWorst = thresholdDelta;
                    _dwellingThresholdWorstPlacement = placementName;
                }
            }
        }
        // The blockers are built in one deferred pass: some authored components
        // are hidden by the connected-world suppressions after mounting, and a
        // collider on a hidden fence would block the player invisibly.
        placement.SetMeta("authoredKitBlockerCandidate", true);
        placement.SetMeta("authoredKitBlockerSource", assetSource);
        return placement;
    }

    /// <summary>
    /// A hidden kit must never block invisibly: the presentation suppressions run
    /// after mounting, so the blockers are re-checked at the end of the world build.
    /// </summary>
    private void DisableBlockersUnderHiddenPresentation()
    {
        using var naming = InvariantNameScope();
        var disabled = 0;
        foreach (var proxy in FindDescendants<StaticBody3D>(this)
            .Where(body => body.HasMeta("collisionOwner") && body.GetMeta("collisionOwner").AsString() == "authored-kit-blocker"))
        {
            if (proxy.IsVisibleInTree())
            {
                continue;
            }

            proxy.CollisionLayer = 0;
            proxy.SetMeta("collisionDisabledReason", "presentation hidden; blocker would block invisibly");
            disabled++;
        }

        SetMeta("hiddenAuthoredBlockerCount", disabled);
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
        // Rear and shared side boundaries tie the existing houses into
        // adjoining holdings, visible when looking across or back down-road.
        AddVisualFenceRun(parent, "ArrivalWestRearHolding", new(-27f, 0f, 14f), new(-27f, 0f, 44.5f));
        AddVisualFenceRun(parent, "ArrivalWestSharedHolding", new(-27f, 0f, 30f), new(-17f, 0f, 30f));
        AddVisualFenceRun(parent, "ArrivalWestHoldingReturn", new(-27f, 0f, 14f), new(-17f, 0f, 14f));
        AddVisualFenceRun(parent, "ArrivalEastRearHolding", new(28f, 0f, 14f), new(28f, 0f, 45f));
        AddVisualFenceRun(parent, "ArrivalEastSharedHolding", new(18f, 0f, 30.5f), new(28f, 0f, 30.5f));
        AddVisualFenceRun(parent, "ArrivalEastHoldingReturn", new(18f, 0f, 14f), new(28f, 0f, 14f));
        BuildArrivalKitchenGardens(parent);
        AddVisualGate(parent, "ArrivalWestNearSetbackGate", new(-6.0f, 0f, 20.0f), 1.55f, 1.15f, 176f);
        // Arrival landmark: a sweep well and wattle run greet the player at
        // the village entrance (authentic winter aвыл pass).
        AddVisualSweepWell(parent, "ArrivalSweepWell", new(-9.6f, 0f, 15.2f), 1.0f, 152f);
        AddVisualWattleFence(parent, "ArrivalWattleRun", new(9.2f, 0f, 13.4f), 6, 96f);
        AddVisualGate(parent, "ArrivalEastNearSetbackGate", new(6.1f, 0f, 19.6f), 1.45f, 1.10f, 184f);

        AddVisualTree(parent, "ArrivalNearBirch", new(-7.2f, 0f, 27.0f), 6.3f, VegetationStyle.Birch, "68705a");
        AddVisualTree(parent, "ArrivalNearBroadleaf", new(7.1f, 0f, 28.5f), 5.7f, VegetationStyle.Broadleaf, "53634e");
        AddVisualTree(parent, "ArrivalFarConifer", new(-31.0f, 0f, 47.0f), 9.6f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "ArrivalFarBirch", new(31.5f, 0f, 48.5f), 8.8f, VegetationStyle.Birch, "596047");

        // The reverse first-person envelope looks through the arrival tail,
        // not into an empty field. The old horizon mass is intentionally
        // retired; these two authored facades keep the road gap readable
        // while a varied far tree line closes the village edge behind them.
        // Keep the reverse edge on the loaded authored HouseA family. The
        // old lean-to fallback made this horizon read as paired boxes; the
        // remaining far houses, fence and varied tree line still close it.
        AddAuthoredHouse(parent, "ArrivalReverseEdgeHouseWest", new(-7.8f, 0f, 52.0f), 0.725f, 180f);
        AddAuthoredHouse(parent, "ArrivalReverseEdgeHouseEast", new(7.9f, 0f, 52.8f), 0.70f, 184f);
        AddDistantHouse(parent, new(-21f, 0f, 61f), 110f, "ArrivalReverseFarCenterHouse");
        AddDistantHouse(parent, new(14f, 0f, 54f), 176f, "ArrivalReverseFarEastHouse");
        AddVisualFenceRun(parent, "ArrivalReverseFarParcelFence", new(-16f, 0f, 50f), new(-5f, 0f, 50f));
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
        AddVisualTree(parent, "ArrivalClosureForwardFarWestBroadleaf", new(-35.0f, 0f, -15.0f), 5.2f, VegetationStyle.Broadleaf, "48553f");
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

        // Uncut growth follows road shoulders and rear fences, not doorways.
        foreach (var stripX in new[] { -7.2f, 7.4f, -26.4f, 27.2f })
        {
            for (var index = 0; index < 44; index++)
            {
                var z = 14f + index * 0.68f;
                var phase = VegetationHash(new Vector3(stripX, 0f, z), 71f);
                if (Mathf.Sin(z * 0.45f + stripX) < -0.40f || phase < 0.12f)
                {
                    continue;
                }
                var anchor = parent.ToGlobal(new Vector3(stripX + (phase - 0.5f) * 2.8f,
                    0f, z + phase * 0.4f));
                anchor.Y = (float)AgentBAct1HeightField.Ground(anchor.X, anchor.Z) + 0.015f;
                AddVisualGrassClump(parent, $"ArrivalUncutVerge{stripX}_{index}",
                    parent.ToLocal(anchor), 0.6f + phase * 0.4f, phase < 0.3f ? "70785c" : "5c7353");
            }
        }

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
        AddVisualFenceRun(parent, "MainStreetEastFarParcelFence", new(40f, 0f, -15f), new(48f, 0f, -15f));
        // The far east side read as orphan fence fragments in a bare field.
        // One full-depth VariantC holding joins them into a worked plot:
        // raised-veranda dwelling, shed with firewood, boundary runs with a
        // north entrance gap, and the existing EastStreetFarFence run as its
        // south edge. Presentation-only; route and collision owners unchanged.
        // Review-corrected layout (measured GLB AABBs): the VariantC dwelling
        // spans 8.63x6.32 m with its veranda on local +X, so at yaw ~0 the
        // veranda faces east into the plot; the storage shed stands as a
        // separate field outbuilding east of the boundary, and the woodpile
        // sits by the north gate gap. Clearances >=0.5 m to both fence runs
        // and to the neighbour fence at z=-15 (x 40..48).
        AddAuthoredHouse(parent, "EastStreetFarHolding", new(35.4f, 1.0f, -16.9f), 0.85f, 2f);
        AddAct1AuthoredExteriorParcel(parent,
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low", "EastStreetFarHoldingShed", new(43.8f, 0f, -20.5f),
                12f, Vector3.One * 0.9f, "village_day@east-street-far-holding-shed"),
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs", "EastStreetFarHoldingWoodpile", new(34.9f, 0f, -14.4f),
                8f, Vector3.One, "village_day@east-street-far-holding-firewood"));
        AddVisualFenceRun(parent, "EastStreetFarHoldingFenceWestOfGate", new(33.5f, 0f, -13.5f), new(37.0f, 0f, -13.5f));
        AddVisualFenceRun(parent, "EastStreetFarHoldingFenceEastOfGate", new(38.6f, 0f, -13.5f), new(41.6f, 0f, -13.5f));
        AddVisualFenceRun(parent, "EastStreetFarHoldingFenceEast", new(41.6f, 0f, -13.5f), new(41.6f, 0f, -22f));
        AddVisualShrub(parent, "EastStreetFarHoldingShrub", new(37.9f, 0f, -21.2f), 0.7f, "48553f");
        AddVisualTree(parent, "EastStreetFarHoldingBirch", new(44.6f, 0f, -17.3f), 8.0f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "MainStreetEastLateralBirch", new(39f, 0f, 3f), 7.4f, VegetationStyle.Birch, "596047");
        AddAuthoredHouse(parent, "MainStreetEastNearMidHouse", new(26f, 1.0f, -8.5f), 0.48f, -94f);
        // PerimeterEastStreetShed is the holding's single storage building.
        AddVisualFenceRun(parent, "MainStreetEastNearMidFence", new(15.5f, 0f, -3f), new(30.7f, 0f, -3f));
        // Join the front picket runs to the rear boundary. The entrance at
        // (24,-17) stays open, north of the diagonal FAP approach.
        AddVisualFenceRun(parent, "MainStreetEastNearMidWestBoundary", new(15.5f, 0f, -17f), new(15.5f, 0f, -3f));
        AddVisualFenceRun(parent, "MainStreetEastNearMidEastBoundary", new(30.7f, 0f, -3f), new(30.7f, 0f, -17f));
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

        // Uncut verge growth follows the same shoulder pattern as the
        // arrival road: muted tufts along the road edge with breathing gaps,
        // kept clear of the two parcel gates and the FAP branch apron.
        foreach (var stripX in new[] { -4.3f, 4.3f })
        {
            for (var index = 0; index < 30; index++)
            {
                var z = 5.5f - index * 0.78f;
                var phase = VegetationHash(new Vector3(stripX, 0f, z), 71f);
                if (Mathf.Sin(z * 0.45f + stripX) < -0.35f || phase < 0.14f)
                {
                    continue;
                }
                if (stripX > 0f && z < -6.5f && z > -13.5f)
                {
                    // FAP branch apron and its parcel edge stay clear.
                    continue;
                }
                if (stripX < 0f && Mathf.Abs(z - 3.5f) < 1.1f)
                {
                    // West parcel gate approach stays open.
                    continue;
                }
                if (stripX > 0f && Mathf.Abs(z - 2.8f) < 1.1f)
                {
                    // East parcel gate approach stays open.
                    continue;
                }
                var anchor = parent.ToGlobal(new Vector3(stripX + (phase - 0.5f) * 1.6f,
                    0f, z + phase * 0.4f));
                anchor.Y = (float)AgentBAct1HeightField.Ground(anchor.X, anchor.Z) + 0.015f;
                AddVisualGrassClump(parent, $"MainStreetUncutVerge{stripX}_{index}",
                    parent.ToLocal(anchor), 0.6f + phase * 0.4f, phase < 0.3f ? "70785c" : "5c7353");
            }
        }

    }

    private static void BuildArrivalKitchenGardens(Node3D parent)
    {
        // The existing fenced holdings are worked land, not spare decoration
        // space. Keep the road/house approaches and gaps between beds clear.
        foreach (var (centerX, rows, name) in new[]
                 { (-22f, 4, "ArrivalWestKitchenGarden"), (22f, 3, "ArrivalEastKitchenGarden") })
        {
            using var soil = new SurfaceTool();
            using var leaves = new SurfaceTool();
            soil.Begin(Mesh.PrimitiveType.Triangles);
            leaves.Begin(Mesh.PrimitiveType.Triangles);
            Vector3 Grounded(float x, float z, float lift)
            {
                var world = parent.ToGlobal(new Vector3(x, 0f, z));
                world.Y = AgentBAct1HeightField.CollisionGround(world.X, world.Z) + lift;
                return parent.ToLocal(world);
            }
            for (var row = 0; row < rows; row++)
            {
                var x = centerX + (row - (rows - 1) * 0.5f) * 2.1f;
                var points = new Vector3[13 * 5];
                for (var along = 0; along <= 12; along++)
                {
                    var taper = Mathf.Sin(along / 12f * Mathf.Pi);
                    for (var across = 0; across < 5; across++)
                    {
                        var side = (across - 2) * 0.5f;
                        points[along * 5 + across] = Grounded(
                            x + side * (0.67f + 0.035f * Mathf.Sin(along * 0.7f + row)),
                            15.5f + along * 0.90f,
                            0.008f + 0.13f * (1f - side * side) * taper);
                    }
                }
                for (var along = 0; along < 12; along++)
                for (var across = 0; across < 4; across++)
                {
                    var a = along * 5 + across;
                    foreach (var index in new[] { a, a + 6, a + 5, a, a + 1, a + 6 })
                        soil.AddVertex(points[index]);
                }
                // Harvested winter beds retain a few flattened dry stalks.
                // Snow covers the soil; no growing summer foliage remains.
                for (var plant = 0; plant < 11; plant++)
                {
                    if ((row == rows - 1 && plant > 5) || (plant + row) % 3 != 0) continue;
                    var z = 16f + plant * 0.88f;
                    var offset = 0.14f * Mathf.Sin(plant * 2.3f + row);
                    var root = Grounded(x + offset, z, 0.013f
                        + 0.13f * (1f - Mathf.Pow(offset / 0.67f, 2f))
                        * Mathf.Sin((z - 15.5f) / 10.8f * Mathf.Pi));
                    for (var blade = 0; blade < 2; blade++)
                    {
                        var angle = blade * Mathf.Tau / 7f + plant * 1.7f;
                        var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        var across = new Vector3(direction.Z, 0f, -direction.X);
                        var length = 0.29f + 0.045f * Mathf.Sin(plant + blade * 2f);
                        var middle = root + direction * length * 0.52f + Vector3.Up * 0.045f;
                        var tip = root + direction * length + Vector3.Up * 0.015f;
                        var left = middle - across * 0.015f;
                        var right = middle + across * 0.015f;
                        foreach (var vertex in new[] { root, left, middle, root, middle, right,
                                     left, tip, middle, middle, tip, right,
                                     middle, left, root, right, middle, root,
                                     middle, tip, left, right, tip, middle })
                            leaves.AddVertex(vertex);
                    }
                }
            }
            soil.GenerateNormals();
            leaves.GenerateNormals();
            foreach (var (suffix, mesh, material) in new[]
                     {
                         ("Beds", soil.Commit(), PainterlyMaterialLibrary.ForColor("d6dce0", "snow_ground")),
                         ("Planting", leaves.Commit(), PainterlyMaterialLibrary.ForColor("746959"))
                     })
            {
                var visual = new MeshInstance3D { Name = name + suffix, Mesh = mesh, MaterialOverride = material };
                visual.SetMeta("presentationOnly", true);
                visual.SetMeta("visualOnly", true);
                visual.SetMeta("collisionOwner", "none");
                visual.SetMeta("interactionOwner", "none");
                parent.AddChild(visual);
            }
        }
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
        // Winter/authenticity pass: sweep well, wattle fence, front-garden
        // palisade and a sled in the yard (decision_log 2026-09-10).
        AddVisualSweepWell(parent, "BabaiYardSweepWell", origin + side * -8.4f + approach * 1.4f, 1.0f, yaw + 24f);
        AddVisualWattleFence(parent, "BabaiYardWattleRun", origin + side * 4.6f + approach * 5.4f, 7, yaw - 6f);
        AddVisualPalisade(parent, "BabaiYardFrontPalisade", origin + side * -1.9f + approach * 5.6f, 14, yaw + 2f);
        AddVisualSled(parent, "BabaiYardSled", origin + side * -3.2f + approach * 4.2f, yaw + 68f);
        // The full room now occupies the old diagonal haystack anchor. Keep
        // this single stack in the front yard, outside the repair approach and
        // both fence runs and the carryable crate; its complete horizontal
        // radius is below .80 m and the crate's south approach stays clear.
        AddVisualHaystack(parent, "BabaiYardHaystack", new(-26.2f, 0f, 3.30f), .58f, yaw - 34f);
        AddVisualWattleFence(parent, "BabaiYardWattleRunEast", origin + side * 9.4f + approach * 2.4f, 6, yaw + 84f);
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
        AddVisualFenceRun(parent, "BabaiEbiHouseApproachFenceEast", new(-22.0f, 0f, 3.0f), new(-22.0f, 0f, 5.3f));
        // The west lateral turn previously ended on a flat field beyond the
        // house wall. A restrained distant parcel keeps that 360-degree read
        // rural and continuous without entering the route or yard envelope.
        AddDistantHouse(parent, new(-40.0f, 0f, 5.5f), 90f, "BabaiWestFieldNeighborHouse");
        AddVisualFenceRun(parent, "BabaiWestFieldNeighborFence", new(-34.0f, 0f, 4.2f), new(-47.0f, 0f, 4.2f));
        AddVisualTree(parent, "BabaiWestFieldNeighborBirch", new(-45.0f, 0f, 9.0f), 7.6f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "BabaiWestFieldNeighborConifer", new(-37.0f, 0f, 12.0f), 8.2f, VegetationStyle.Conifer, "30483f");
        AddVisualTree(parent, "BabaiEbiHouseNearBirch", new(-38.0f, 0f, 0.7f), 6.4f, VegetationStyle.Birch, "596047");
        // Tukay 13 stood 87% inside Tukay 7 (ArrivalWestNearAuthoredTimberGableParcel),
        // two addressed houses in one spot. It moves to the free lot east of the
        // arrival road, its front toward the road, with its own fence and birch.
        AddDistantHouse(parent, new(12.0f, 0f, 41.0f), 270f, "BabaiReverseFieldNeighborHouse");
        AddVisualFenceRun(parent, "BabaiReverseFieldNeighborFence", new(7.6f, 0f, 36.5f), new(7.6f, 0f, 45.5f));
        AddVisualTree(parent, "BabaiReverseFieldNeighborBirch", new(16.5f, 0f, 45.0f), 7.5f, VegetationStyle.Birch, "596047");
        AddCoreFacetedMass(parent, "BabaiEbiHouseStreetMemoryMass", new(-30.0f, 2.1f, 8.2f), new(7.0f, 2.0f, 2.0f), "55615a");
    }

    private static void ApplyHeroWarmWindow(Node3D facade)
    {
        // Joinery color belongs to the hero's authored material slots. This
        // pass owns only the inhabited warm window, not another trim repaint.
        var warmWindow = FindDescendants<MeshInstance3D>(facade)
            .FirstOrDefault(mesh => mesh.Name == "HeroHouse_Street_Window2_Glass_LOD0");
        if (warmWindow is null)
        {
            return;
        }

        warmWindow.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml("b6814f"),
            EmissionEnabled = true,
            Emission = Color.FromHtml("9b5a32"),
            EmissionEnergyMultiplier = 1.55f,
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
            Scale = Vector3.One * (parent.Name == "BabaiApproachDwellingFacade" ? 1f : .82f)
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
                Position = StyleBenchmarkInteriorFactory.RoomOffset + new Vector3(-2.55f, 1.65f, -3.25f),
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

        // Full side/rear joinery now belongs to the Blender dwelling source.
        // Do not layer the obsolete .82-scale windows and beams over it.
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
        // The ravine (x ~ 45-56) now runs where the east horizon trees and the
        // parcel fence stood; the far bank is dressed in Act1ConnectedWorld.Ravine.cs.
        // Separate the full-depth neighbor from the clinic's service shed.
        // The old small-backdrop anchor overlapped both buildings at .9 scale.
        // Kept clear of the ravine's west rim fence.
        AddDistantHouse(
            parent,
            placement.Origin + side * 14.0f - front * 10.0f,
            yaw + 90f,
            "FapWestFieldNeighborHouse");
        AddVisualFenceRun(
            parent,
            "FapWestFieldNeighborFence",
            placement.Origin + side * 10.0f - front * 6.0f,
            placement.Origin + side * 13.0f - front * 6.0f);
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
            placement.Origin - side * 24.0f - front * 10.0f);
        AddVisualTree(parent, "FapOppositeFieldNeighborBirch", placement.Origin - side * 25.0f - front * 6.0f, 8.2f, VegetationStyle.Birch, "596047");
        AddVisualTree(parent, "FapOppositeFieldNeighborConifer", placement.Origin - side * 25.0f - front * 24.0f, 6.0f, VegetationStyle.Conifer, "30483f");
        // The capture's fixed right turn looks along the open east field
        // rather than along the branch vector. Anchor two far silhouettes in
        // that actual view window so the clinic remains part of a village
        // parcel instead of ending on a flat horizon.
        // That east field view is now the ravine and the second half of the
        // village beyond it (Act1ConnectedWorld.Ravine.cs).
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
        // The last western dwelling is a complete holding, not a facade with
        // a fence through its footprint. Leave a 3.5 m entrance at the road side.
        AddVisualFenceRun(parent, "ZiratWestHoldingFrontNorth", new(-19f, 0f, -54f), new(-19f, 0f, -61.5f), true);
        AddVisualFenceRun(parent, "ZiratWestHoldingFrontSouth", new(-19f, 0f, -65f), new(-19f, 0f, -73f), true);
        AddVisualFenceRun(parent, "ZiratWestHoldingNorth", new(-19f, 0f, -54f), new(-38f, 0f, -54f), true);
        AddVisualFenceRun(parent, "ZiratWestHoldingRear", new(-38f, 0f, -54f), new(-38f, 0f, -73f), true);
        AddVisualFenceRun(parent, "ZiratWestHoldingSouth", new(-38f, 0f, -73f), new(-19f, 0f, -73f), true);
        AddAct1AuthoredExteriorParcel(parent,
            new Act1ExteriorParcelComponentPlacement(
                "OutbuildingShed_Low", "ZiratWestHoldingShed", new(-33f, 0f, -57f),
                180f, Vector3.One * .95f, "zirat_road@last-western-holding-storage"),
            new Act1ExteriorParcelComponentPlacement(
                "Woodpile_StackedLogs", "ZiratWestHoldingWoodpile", new(-29f, 0f, -56.5f),
                180f, Vector3.One, "zirat_road@last-western-holding-firewood"));
        using (var access = new Curve3D())
        {
            access.AddPoint(new(-3.8f, 0f, -59.5f), Vector3.Zero, new(-3f, 0f, 0f));
            access.AddPoint(new(-12f, 0f, -59.5f), new(2f, 0f, 0f), new(-2f, 0f, -1f));
            access.AddPoint(new(-19f, 0f, -63.2f), new(2f, 0f, .3f), new(-2f, 0f, -.3f));
            access.AddPoint(new(-24f, 0f, -64.5f), new(1.5f, 0f, 1f), new(-1f, 0f, -1.5f));
            access.AddPoint(new(-25f, 0f, -68.3f), new(.5f, 0f, 1f), new(-1f, 0f, 0f));
            access.AddPoint(new(-28.6f, 0f, -67.4f), new(1f, 0f, 0f), new(-.3f, 0f, 0f));
            access.AddPoint(new(-29.3f, 0f, -67.4f), new(.3f, 0f, 0f));
            AddVisualLandformSurface(parent, "ZiratWestHoldingAccess", 1.15f, .02f,
                access.GetBakedLength(), new(0f, .012f, 0f), "c6cfd5", "snow_trampled", 0f, true, access);
        }
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
        // Staggered near stands grow out of the new shoulders. The western
        // group starts earlier; the eastern opening retains the boundary view.
        var nearStand = new (float X, float Z, float Height, VegetationStyle Style)[]
        {
            (-6.8f, -109f, 10.7f, VegetationStyle.Conifer),
            (-10.8f, -111.5f, 8.4f, VegetationStyle.Birch),
            (-7.6f, -115.7f, 11.8f, VegetationStyle.Conifer),
            (-13f, -118f, 9.3f, VegetationStyle.Conifer),
            (7.8f, -115.2f, 10.1f, VegetationStyle.Conifer),
            (11.4f, -119.8f, 9f, VegetationStyle.Birch),
            (6.2f, -123.7f, 11.5f, VegetationStyle.Conifer),
            (14f, -124.2f, 12.4f, VegetationStyle.Conifer)
        };
        for (var index = 0; index < nearStand.Length; index++)
        {
            var spec = nearStand[index];
            var anchor = new Vector3(spec.X,
                (float)AgentBAct1HeightField.Ground(spec.X, spec.Z), spec.Z);
            AddVisualTree(parent, $"KaraSlopeStand{index}", anchor, spec.Height,
                spec.Style, spec.Style == VegetationStyle.Birch ? "4a5949" : "30473b");
            // Road clearance can reject the tree; its roots must disappear with it.
            if (!parent.HasNode($"KaraSlopeStand{index}")) continue;
            if (index is 3 or 7)
                parent.GetNode<Node3D>($"KaraSlopeStand{index}").SetMeta("winterVariant", "WinterPine");
            // Roots emerge from the trunk and disappear into soil, not a prop row.
            for (var root = 0; root < 3; root++)
            {
                var angle = index * 1.73f + root * 2.14f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var length = 1.1f + ((index + root) % 3) * 0.24f;
                var bend = anchor + direction * length * 0.48f;
                var end = anchor + direction * length;
                bend.Y = (float)AgentBAct1HeightField.Ground(bend.X, bend.Z) + 0.055f;
                end.Y = (float)AgentBAct1HeightField.Ground(end.X, end.Z) - 0.035f;
                AddCoreForestBranch(parent, $"KaraSlopeRoot{index}_{root}Base",
                    anchor + Vector3.Up * 0.12f, bend, "4b4034", 0.12f, 0.065f);
                AddCoreForestBranch(parent, $"KaraSlopeRoot{index}_{root}Tip",
                    bend, end, "4b4034", 0.065f, 0.018f);
            }
        }
        // Regrowth stays in sheltered pockets and beyond the playable threshold,
        // not a uniform grass carpet or another row along the road shoulders.
        var regrowth = new (float X, float Z, float Height)[]
        {
            (-5.1f, -113f, 2.2f), (-9f, -117.9f, 3.4f), (-11.8f, -121.4f, 2.6f),
            (5.6f, -119f, 2.8f), (9.8f, -122f, 1.9f), (7.2f, -127f, 3.8f),
            (1.8f, -132.8f, 4.6f), (-3.2f, -130.6f, 3.7f), (-0.5f, -136f, 5.2f),
            (-7.4f, -135f, 3.1f)
        };
        for (var index = 0; index < regrowth.Length; index++)
        {
            var sapling = regrowth[index];
            AddVisualTree(parent, $"KaraShelteredRegrowth{index}",
                new Vector3(sapling.X, 0f, sapling.Z), sapling.Height,
                VegetationStyle.Conifer, "3c5140");
            if (index is 6 or 7 or 8 or 9)
                parent.GetNodeOrNull<Node3D>($"KaraShelteredRegrowth{index}")
                    ?.SetMeta("winterVariant", "WinterPine");
        }
        foreach (var (name, x, z, radius) in new[]
                 { ("West", -8.7f, -110.5f, 1.2f), ("East", 9.8f, -116.5f, 1.5f),
                   ("Deep", -6f, -125.4f, 1.1f) })
            AddVisualShrub(parent, $"KaraStandUnderstory{name}",
                new Vector3(x, (float)AgentBAct1HeightField.Ground(x, z), z), radius, "40513d");

        // Offset stands wrap the final watershed and both lateral slopes.
        // Anchors stay outside the route, with depth between trunks rather
        // than a single opaque forest plane.
        var watershedStand = new (float X, float Z, float Height)[]
        {
            (-19f, -130f, 10.8f), (-14f, -134f, 12.3f), (-9f, -132f, 9.2f),
            (10f, -133f, 11.4f), (16f, -130f, 9.8f), (22f, -136f, 12.6f),
            (-26f, -141f, 11.7f), (-21f, -145f, 9.6f), (-16f, -140f, 13.1f),
            (-11f, -144f, 10.4f), (-6f, -139f, 11.8f), (-8.5f, -145f, 9.3f),
            (4f, -141f, 12.5f), (9f, -147f, 10.1f), (14f, -141f, 13.2f),
            (19f, -146f, 10.7f), (27f, -142f, 11.2f),
            (-31f, -148f, 12.0f), (-23f, -150f, 13.8f), (-14f, -150f, 11.5f),
            (-5f, -150f, 13.0f), (3f, -151f, 11.4f), (17f, -150f, 12.2f),
            (30f, -150f, 13.4f),
            (19f, -97f, 6.5f), (23f, -105f, 8.8f), (18f, -114f, 7.6f),
            (25f, -122f, 10.2f), (30f, -96f, 10.7f), (34f, -108f, 12.1f),
            (29f, -116f, 9.4f), (36f, -127f, 11.8f), (42f, -100f, 12.9f),
            (47f, -113f, 10.6f), (43f, -122f, 13.4f), (51f, -130f, 11.2f),
            (-20f, -98f, 7.2f), (-24f, -109f, 9.8f), (-19f, -120f, 8.4f),
            (-31f, -101f, 11.6f), (-35f, -112f, 9.1f), (-29f, -126f, 12.4f),
            (-43f, -97f, 10.8f), (-47f, -110f, 12.8f), (-42f, -123f, 11.4f),
            (-51f, -132f, 13.1f)
        };
        for (var index = 0; index < watershedStand.Length; index++)
        {
            var tree = watershedStand[index];
            var anchor = parent.ToGlobal(new Vector3(tree.X, 0f, tree.Z));
            anchor.Y = (float)AgentBAct1HeightField.Ground(anchor.X, anchor.Z);
            AddVisualTree(parent, $"KaraWatershedStand{index}", parent.ToLocal(anchor),
                tree.Height, index >= 24 && index % 4 == 0 ? VegetationStyle.Birch : VegetationStyle.Conifer,
                index < 6 ? "243c33" : index < 17 || Mathf.Abs(tree.X) < 30f ? "344b43" : "40574f");
            if (index is 2 or 3 or 10 or 20)
                parent.GetNodeOrNull<Node3D>($"KaraWatershedStand{index}")?.SetMeta("winterVariant", "WinterPine");
            if (index >= 24 && Mathf.Abs(tree.X) < 36f)
            {
                var shrubAnchor = anchor + new Vector3(tree.X < 0f ? 1.8f : -1.8f, 0f, 1.4f);
                shrubAnchor.Y = (float)AgentBAct1HeightField.Ground(shrubAnchor.X, shrubAnchor.Z);
                AddVisualShrub(parent, $"KaraSlopeUnderstory{index}", parent.ToLocal(shrubAnchor),
                    1.8f + index % 3 * 0.3f, "34493c");
            }
        }
        AddVisualLandformSegment(parent, "KaraAsymmetricWestBank", new(-3.6f, 0f, -103.0f), new(-8.6f, 0f, -121.5f), 2.2f, 0.34f, "3b493e", "earth", 0.02f);
        AddVisualLandformSegment(parent, "KaraAsymmetricEastBank", new(3.8f, 0f, -105.0f), new(6.4f, 0f, -117.0f), 1.5f, 0.22f, "3f4c40", "earth", 0.02f);
        AddCoreRootCluster(parent, "KaraRootBankWest", new(-5.7f, 0f, -110.5f), 1.0f, -16f);
        // Keep the existing root framing outside the warm-window camera
        // position; it remains a visual forest edge cue and has no route or
        // collision ownership.
        AddCoreRootCluster(parent, "KaraRootBankEast", new(7.2f, 0f, -112.2f), 0.78f, 20f);
        AddCoreRootCluster(parent, "KaraThresholdRootWest", new(-8.0f, 0f, -120.5f), 1.18f, 34f);
        AddCoreFacetedMass(parent, "KaraLateralForestShelfWest", origin + new Vector3(-10.0f, 2.8f, -13.5f), new(5.2f, 2.8f, 2.1f), "2f4439");
        AddCoreFacetedMass(parent, "KaraLateralForestShelfEast", origin + new Vector3(10.5f, 2.4f, -18.5f), new(5.0f, 2.4f, 2.3f), "345044");
        AddCoreFacetedMass(parent, "KaraDistantEdgeWindowWest", origin + new Vector3(-9.0f, 2.2f, -27.0f), new(5.8f, 2.2f, 2.0f), "2e4439");
        AddCoreFacetedMass(parent, "KaraDistantEdgeWindowEast", origin + new Vector3(9.5f, 2.0f, -29.0f), new(5.4f, 2.0f, 2.1f), "385044");
        AddVisualFenceRun(parent, "KaraSideLandmarkFence", new(8.0f, 0f, -106.0f), new(10.5f, 0f, -114.5f));
        AddVisualBox(parent, "KaraSideLandmarkPost", new(0.18f, 2.0f, 0.18f), new(10.3f, 1.0f, -114.3f), "574d3d", "wood", rollDegrees: -3f);
        AddVisualBox(parent, "KaraSideLandmarkHeader", new(2.8f, 0.14f, 0.16f), new(9.3f, 1.92f, -113.0f), "574d3d", "wood", yawDegrees: -16f, rollDegrees: 7f);
        AddVisualTree(parent, "KaraMixedMassWestNear", origin + new Vector3(-23.45f, 0f, 12.55f), 6.1f, VegetationStyle.Broadleaf, "405445");
        AddVisualTree(parent, "KaraMixedMassEastNear", origin + new Vector3(9.0f, 0f, 3.8f), 7.3f, VegetationStyle.Birch, "526052");
        AddVisualTree(parent, "KaraMixedMassWestMid", origin + new Vector3(-11.5f, 0f, -8.0f), 9.6f, VegetationStyle.Conifer, "2e4439");
        parent.GetNodeOrNull<Node3D>("KaraMixedMassWestMid")?.SetMeta("winterVariant", "WinterPine");
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
        AddAuthoredWinterTree(karaClosure, "KaraClosureNearConiferWest", new(-8.4f, 0f, 9.5f), 8.0f, "WinterPine");
        AddVisualTree(karaClosure, "KaraClosureNearBirchEast", new(11.0f, 0f, 7.0f), 5.8f, VegetationStyle.Birch, "596047");
        AddAuthoredWinterTree(karaClosure, "KaraClosureMidBroadleafWest", new(-13.0f, 0f, 2.0f), 8.0f, "WinterDeadTree");
        AddAuthoredWinterTree(karaClosure, "KaraClosureMidConiferEast", new(12.4f, 0f, -2.5f), 8.4f, "WinterPine");
        AddVisualTree(karaClosure, "KaraClosureDeepBirchWest", new(-14.5f, 0f, -7.5f), 11.1f, VegetationStyle.Birch, "596047");
        AddVisualTree(karaClosure, "KaraClosureDeepBroadleafEast", new(15.0f, 0f, -10.5f), 10.7f, VegetationStyle.Broadleaf, "3b5043");
        AddAuthoredWinterTree(karaClosure, "KaraClosureFarConiferWest", new(-20.0f, 0f, -15.5f), 10.3f, "WinterPine");
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
        AddVisualTree(parent, "KaraThresholdBirchWest", origin + new Vector3(-8.8f, 0f, -13.5f), 10.0f, VegetationStyle.Birch, "405445");
        AddVisualTree(parent, "KaraThresholdConiferEast", origin + new Vector3(9.4f, 0f, -17.0f), 10.8f, VegetationStyle.Conifer, "243b31");

        AddVisualLandformSegment(parent, "KaraDeepForestShelfWest", origin + new Vector3(-8.8f, 0f, -16.0f), origin + new Vector3(-11.0f, 0f, -29.0f), 2.10f, 0.44f, "2f4037", "earth", 0.02f);
        AddVisualLandformSegment(parent, "KaraDeepForestShelfEast", origin + new Vector3(10.4f, 0f, -18.0f), origin + new Vector3(12.6f, 0f, -31.0f), 2.00f, 0.40f, "30463b", "earth", 0.02f);
        // Replace four solid dark polygon masses with overlapping trees on
        // the same side parcels. The road window remains outside their bounds.
        foreach (var (standName, offset, color) in new[]
        {
            ("KaraThresholdStandWest", new Vector3(-10.4f, 0f, -10.5f), "263c32"),
            ("KaraThresholdStandEast", new Vector3(11.3f, 0f, -14.5f), "2c403b"),
            ("KaraDeepStandWest", new Vector3(-11.2f, 0f, -24.5f), "243a34"),
            ("KaraDeepStandEast", new Vector3(12.2f, 0f, -27.5f), "2a3e37")
        })
        {
            for (var index = 0; index < 3; index++)
            {
                var anchor = origin + offset + new Vector3((index - 1) * 2.2f, 0f, index * 2.1f - 1.8f);
                var worldAnchor = parent.ToGlobal(anchor);
                worldAnchor.Y = (float)AgentBAct1HeightField.Ground(worldAnchor.X, worldAnchor.Z);
                AddVisualTree(parent, $"{standName}{index}", parent.ToLocal(worldAnchor),
                    6.4f + index * 1.15f, index == 1 ? VegetationStyle.Broadleaf : VegetationStyle.Conifer, color);
            }
        }
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
        // A tapered branch keeps the root hook organic at close range while
        // preserving the existing RootHook name and presentation ownership.
        AddCoreForestBranch(root, "RootHook", new(0.62f, 0.12f, -0.20f),
            new(0.78f, 1.12f, -0.28f), "4a3d32", 0.11f, 0.035f,
            radialSegments: 8);
        AddCoreFacetedMass(root, "RootMossMass", new(0.18f, 0.34f, 0.12f), new(0.90f, 0.40f, 0.62f), "405345");
    }

    private static void AddCoreForestBranch(Node3D parent, string name, Vector3 start, Vector3 end,
        string color, float bottomRadius = 0.11f, float topRadius = 0.055f,
        int radialSegments = 6)
    {
        var direction = end - start;
        var branch = new MeshInstance3D
        {
            Name = name,
            Position = (start + end) * 0.5f,
            Mesh = new CylinderMesh { TopRadius = topRadius, BottomRadius = bottomRadius, Height = direction.Length(), RadialSegments = radialSegments },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "wood_bark")
        };
        branch.SetMeta("presentationOnly", true);
        branch.SetMeta("visualOnly", true);
        parent.AddChild(branch);
        // Cylinder +Y is its tapered tip; LookAt points -Z at the endpoint.
        branch.LookAt(parent.ToGlobal(end), Vector3.Up);
        branch.RotateObjectLocal(Vector3.Right, -Mathf.Pi * 0.5f);
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
        // This legacy source backbone must use the same authored HouseA
        // family as the connected-world kit. Keeping the anchors and
        // staggered depths preserves side/reverse closure without reviving
        // the old box-like AddVillageFacade fallback rhythm.
        AddAuthoredHouse(parent, "StreetFacadeWestArrival", new(-16.5f, 0f, 28.5f), 0.875f, 174f);
        AddAuthoredHouse(parent, "StreetFacadeEastArrival", new(17.4f, 0f, 29.8f), 0.95f, 186f);
        AddAuthoredHouse(parent, "StreetFacadeWestMid", new(-20.8f, 0f, 5.0f), 0.7625f, 92f);
        AddAuthoredHouse(parent, "StreetFacadeEastMid", new(20.2f, 0f, 2.2f), 0.675f, -88f);
        AddAuthoredHouse(parent, "StreetFacadeWestReturn", new(-18.4f, 0f, -21.0f), 0.9375f, 90f);
        AddAuthoredHouse(parent, "StreetFacadeEastReturn", new(18.7f, 0f, -22.5f), 0.75f, -90f);

        AddVisualCanopy(parent, "StreetWoodCanopyWest", new(-10.2f, 0f, 6.4f), 3.2f, 2.8f, 91f, "6d5c47", "4e4539");
        AddVisualCanopy(parent, "StreetWoodCanopyEast", new(10.4f, 0f, -5.4f), 2.5f, 3.4f, -86f, "7a644b", "504238");
        AddVisualShed(parent, "StreetShedWest", new(-12.2f, 0f, -2.8f), 0.74f, 102f, "806d58", "4d3e34");
        AddVisualShed(parent, "StreetShedEast", new(12.6f, 0f, -17.4f), 0.66f, -78f, "69746b", "3e4440");
        AddVisualWoodpile(parent, "StreetWoodpileWest", new(-10.2f, 0f, 6.4f), 1.1f, 90f);
        AddVisualWoodpile(parent, "StreetWoodpileEast", new(10.4f, 0f, -5.4f), 0.88f, -78f);
        AddVisualLandformSurface(parent, "StreetWestWoodApproach", .85f, .018f, 4.2f,
            new(-8.1f, .015f, 6.4f), "c8d0d6", "snow_trampled", 90f, true);
        AddVisualLandformSurface(parent, "StreetEastWoodApproach", .65f, .016f, 3.4f,
            new(8.7f, .015f, -5.4f), "d8dfe3", "snow_trampled", 90f, true);

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
        AddAuthoredHouse(parent, "ArrivalReverseHouseWest", new(-14.8f, 0f, 42.0f), 0.775f, 176f);
        AddAuthoredHouse(parent, "ArrivalReverseHouseEast", new(15.6f, 0f, 43.5f), 0.875f, 184f);
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
        float yawDegrees,
        bool conformToTerrain = false,
        Curve3D? centerline = null)
    {
        var snowBank = surface == "snow_ground";
        var crossSections = snowBank ? 9 : 5;
        var lengthSections = conformToTerrain || centerline is not null ? Mathf.CeilToInt(length / .4f) + 1 : 7;
        var vertices = new Vector3[crossSections * lengthSections];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var xProfile = snowBank
            ? new[] { -.5f, -.38f, -.26f, -.12f, 0f, .12f, .26f, .38f, .5f }
            : new[] { -0.5f, -0.24f, 0f, 0.24f, 0.5f };
        var phase = name.Length * 0.37f;
        var localTransform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(yawDegrees)), center);
        var worldTransform = parent.GlobalTransform * localTransform;
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
                if (snowBank)
                {
                    var cap = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(zT * Mathf.Pi)), .7f);
                    var rounded = Mathf.Pow(Mathf.Max(0f, 1f - 4f * profile * profile), 1.4f);
                    vertices[vertexIndex].X *= .94f + .10f * Mathf.Sin(zT * 8f + phase);
                    vertices[vertexIndex].Y = -.025f + cap * rounded
                        * (height + .035f * Mathf.Sin(zT * 9f + phase));
                }
                if (centerline is not null)
                {
                    var distance = zT * length;
                    var tangent = (centerline.SampleBaked(Mathf.Min(length, distance + .05f))
                        - centerline.SampleBaked(Mathf.Max(0f, distance - .05f))).Normalized();
                    var side = new Vector3(tangent.Z, 0f, -tangent.X);
                    vertices[vertexIndex] = centerline.SampleBaked(distance)
                        + side * vertices[vertexIndex].X
                        + Vector3.Up * vertices[vertexIndex].Y;
                }
                if (conformToTerrain)
                {
                    var world = worldTransform * vertices[vertexIndex];
                    var rise = vertices[vertexIndex].Y;
                    if (snowBank)
                    {
                        var road = AgentBAct1HeightField.RoadInfo(world.X, world.Z);
                        rise *= Mathf.SmoothStep(0f, 1f, (float)(road.Distance - road.HalfWidth) / .65f);
                    }
                    world.Y = AgentBAct1HeightField.CollisionGround(world.X, world.Z)
                        + center.Y + rise;
                    vertices[vertexIndex] = worldTransform.AffineInverse() * world;
                }
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
                // Godot front faces are clockwise when seen from above.
                indices[index++] = topLeft;
                indices[index++] = topRight;
                indices[index++] = bottomLeft;
                indices[index++] = topRight;
                indices[index++] = bottomRight;
                indices[index++] = bottomLeft;
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
        if (conformToTerrain)
        {
            using var surfaceTool = new SurfaceTool();
            surfaceTool.CreateFrom(reliefMesh, 0);
            surfaceTool.GenerateNormals();
            reliefMesh = surfaceTool.Commit();
        }
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = center,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Mesh = reliefMesh,
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface),
            // The shared terrain owns relief. Old constant-height ribbons
            // become floating slabs with correct winding; do not render that
            // obsolete overlay. Only explicitly ground-conformed paths remain.
            Visible = conformToTerrain
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
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "grass")
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

        var anchorWorld = canopy.GlobalPosition;
        anchorWorld.Y = AgentBAct1HeightField.CollisionGround(anchorWorld.X, anchorWorld.Z) - .04f;
        canopy.GlobalPosition = anchorWorld;
        foreach (var x in new[] { -width * .5f, width * .5f })
        foreach (var z in new[] { -depth * .5f, depth * .5f })
        {
            var world = canopy.ToGlobal(new Vector3(x, 0, z));
            world.Y = AgentBAct1HeightField.CollisionGround(world.X, world.Z) - .04f;
            var bottom = canopy.ToLocal(world).Y;
            var top = 2.08f + Mathf.Tan(Mathf.DegToRad(5)) * x - .08f;
            AddVisualBox(canopy, "Post", new(.12f, top - bottom, .12f),
                new(x, (top + bottom) * .5f, z), postColor, "wood_fence");
        }

        AddVisualBox(canopy, "Roof", new(width + 0.30f, 0.18f, depth + 0.32f), new(0f, 2.08f, 0f), roofColor, "wood", rollDegrees: 5f);
        AddVisualBox(canopy, "RoofSnow", new(width + .26f, .12f, depth + .28f), new(0, 2.23f, 0), "e8edf0", "snow_roof", rollDegrees: 5f);
        AddVisualBox(canopy, "RoofRidge", new(width * 0.72f, 0.10f, 0.16f), new(0f, 2.26f, 0f), postColor, "wood");
    }

    private static void AddVisualSnowShovel(Node3D parent, string name, Vector3 anchor, float yawDegrees)
    {
        var shovel = new Node3D { Name = name, Position = anchor,
            RotationDegrees = new Vector3(0, yawDegrees, -8) };
        shovel.SetMeta("visualOnly", true);
        shovel.SetMeta("presentationOnly", true);
        shovel.SetMeta("presentationRole", "snow shovel beside the cleared doorstep");
        parent.AddChild(shovel);
        var world = shovel.GlobalPosition;
        world.Y = AgentBAct1HeightField.CollisionGround(world.X, world.Z) - .035f;
        shovel.GlobalPosition = world;
        shovel.AddChild(new MeshInstance3D { Name = "WoodHandle", Position = new(0, .78f, .025f),
            Mesh = new CylinderMesh { TopRadius = .016f, BottomRadius = .019f, Height = 1.05f, RadialSegments = 8 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("897356", "wood") });
        AddVisualBox(shovel, "Grip", new(.16f, .035f, .04f), new(0, 1.33f, .025f), "50534d", "metal");
        // Rounded-corner scoop with a shallow curved cross-section and thickness.
        var outline = new Vector2[] { new(-.17f, 0), new(.17f, 0), new(.23f, .04f),
            new(.23f, .25f), new(.18f, .31f), new(-.18f, .31f), new(-.23f, .25f), new(-.23f, .04f) };
        var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(Vector2 p, float back) => new(p.X, p.Y, .08f * Mathf.Pow(p.X / .23f, 2) + back);
        void Triangle(Vector3 a, Vector3 b, Vector3 c) { tool.AddVertex(a); tool.AddVertex(b); tool.AddVertex(c); }
        for (var i = 0; i < outline.Length; i++)
        {
            var next = (i + 1) % outline.Length;
            var a = Point(outline[i], 0); var b = Point(outline[next], 0);
            var c = Point(outline[i], .018f); var d = Point(outline[next], .018f);
            Triangle(new(0, .15f, 0), a, b); Triangle(new(0, .15f, .018f), d, c);
            Triangle(a, c, b); Triangle(b, c, d);
        }
        tool.Index(); tool.GenerateNormals();
        shovel.AddChild(new MeshInstance3D { Name = "Scoop", Mesh = tool.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("677579", "metal") });
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
        var root = pile.GlobalPosition;
        root.Y = AgentBAct1HeightField.CollisionGround(root.X, root.Z) - .065f;
        pile.GlobalPosition = root;

        AddVisualBox(pile, "LogBottomA", new(1.65f, 0.18f, 0.24f), new(-0.08f, 0.12f, 0f), "6c5845", "wood_bark", rollDegrees: -4f);
        AddVisualBox(pile, "LogBottomB", new(1.42f, 0.18f, 0.22f), new(0.12f, 0.32f, 0.08f), "7a6048", "wood_bark", rollDegrees: 5f);
        AddVisualBox(pile, "LogTopA", new(1.16f, 0.17f, 0.20f), new(-0.02f, 0.52f, -0.03f), "5d4c3e", "wood_bark", rollDegrees: -8f);
        AddVisualBox(pile, "LogEnd", new(0.20f, 0.26f, 0.26f), new(0.78f, 0.28f, 0.02f), "9b8766", "wood");
    }

/// <summary>
    /// Authentic Tatar village yard props (winter Act I): a sweep well with a
    /// long журавль lever, a woven wattle fence, a low front-garden palisade
    /// and a wooden sled. Presentation-only, no collision or interaction
    /// ownership; every prop carries the same muted winter palette.
    /// </summary>
    private static void AddVisualSweepWell(Node3D parent, string name, Vector3 anchor, float scale, float yawDegrees)
    {
        var well = new Node3D
        {
            Name = name,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Scale = Vector3.One * scale
        };
        well.SetMeta("visualOnly", true);
        well.SetMeta("presentationRole", "sweep (журавль) well landmark");
        well.SetMeta("culturalRole", "Tatar village well with lever; no text, no ornament");
        parent.AddChild(well);

        // Stone head with a snow cap.
        AddVisualBox(well, "WellHead", new(1.15f, 0.62f, 1.05f), new(0f, 0.31f, 0f), "6f695c", "stone");
        AddVisualBox(well, "WellSnowCap", new(1.22f, 0.09f, 1.12f), new(0f, 0.66f, 0f), "eef2f6", "snow_ground");
        AddVisualBox(well, "WellMouth", new(0.62f, 0.06f, 0.54f), new(0f, 1.06f, 0f), "2c3740", "ice");
        // Two uprights and the long lever with its counterweight.
        AddVisualBox(well, "WellPostWest", new(0.16f, 2.35f, 0.16f), new(-0.62f, 1.17f, 0f), "5b4a3a", "wood");
        AddVisualBox(well, "WellPostEast", new(0.16f, 2.35f, 0.16f), new(0.62f, 1.17f, 0f), "5b4a3a", "wood");
        AddVisualBox(well, "WellPivot", new(1.55f, 0.12f, 0.14f), new(0f, 2.28f, 0f), "4e4133", "wood");
        AddVisualBox(well, "WellLever", new(0.13f, 0.13f, 3.15f), new(0f, 2.50f, 0.35f), "6a5843", "wood", rollDegrees: -6f);
        AddVisualBox(well, "WellCounterweight", new(0.34f, 0.30f, 0.34f), new(0f, 2.36f, 1.62f), "59503f", "wood_prop");
        AddVisualBox(well, "WellChain", new(0.05f, 1.30f, 0.05f), new(0f, 1.72f, -0.90f), "3f4441", "stone");
        AddVisualBox(well, "WellBucket", new(0.34f, 0.30f, 0.34f), new(0f, 1.02f, -0.90f), "6b5642", "wood_prop");
        AddVisualBox(well, "WellRope", new(0.04f, 0.42f, 0.04f), new(0f, 1.30f, -0.90f), "8a7a5e", "fabric");
    }

    private static void AddVisualWattleFence(Node3D parent, string name, Vector3 anchor, int bays, float yawDegrees)
    {
        var fence = new Node3D
        {
            Name = name,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        fence.SetMeta("visualOnly", true);
        fence.SetMeta("presentationRole", "woven wattle (плетень) boundary");
        parent.AddChild(fence);

        const float bay = 0.75f;
        for (var index = 0; index <= bays; index++)
        {
            AddVisualBox(fence, $"WattleStake{index}", new(0.09f, 1.32f, 0.09f),
                new(index * bay, 0.66f, 0f), "6b5b46", "wood_fence", rollDegrees: (index % 3 - 1) * 1.5f);
        }

        for (var rail = 0; rail < 5; rail++)
        {
            var height = 0.30f + rail * 0.22f;
            var drift = (rail % 2 == 0 ? 1f : -1f) * 0.018f;
            AddVisualBox(fence, $"WattleRod{rail}", new(bays * bay, 0.075f, 0.05f),
                new(bays * bay * 0.5f, height, drift), "7a6a52", "wood_fence", rollDegrees: drift * 60f);
        }

        AddVisualBox(fence, "WattleSnowCap", new(bays * bay, 0.07f, 0.10f),
            new(bays * bay * 0.5f, 1.28f, 0f), "eef2f6", "snow_ground");
    }

    private static void AddVisualPalisade(Node3D parent, string name, Vector3 anchor, int pickets, float yawDegrees)
    {
        var garden = new Node3D
        {
            Name = name,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        garden.SetMeta("visualOnly", true);
        garden.SetMeta("presentationRole", "front-garden palisade (палисадник)");
        parent.AddChild(garden);

        const float step = 0.17f;
        for (var index = 0; index < pickets; index++)
        {
            var height = 0.52f + ((index % 3) - 1) * 0.05f;
            AddVisualBox(garden, $"PalisadePicket{index}", new(0.07f, height, 0.05f),
                new(index * step, height * 0.5f, 0f), "8d7a5c", "wood_fence", rollDegrees: (index % 4 - 1.5f) * 2.2f);
        }

        AddVisualBox(garden, "PalisadeRail", new(pickets * step, 0.06f, 0.05f),
            new(pickets * step * 0.5f, 0.34f, 0.03f), "7c6a50", "wood_fence");
        AddVisualBox(garden, "PalisadeSnow", new(pickets * step, 0.05f, 0.08f),
            new(pickets * step * 0.5f, 0.60f, 0f), "eef2f6", "snow_ground");
    }

    private static void AddVisualSled(Node3D parent, string name, Vector3 anchor, float yawDegrees)
    {
        var sled = new Node3D
        {
            Name = name,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        sled.SetMeta("visualOnly", true);
        sled.SetMeta("presentationRole", "wooden sled (салазки) yard detail");
        parent.AddChild(sled);

        // Keep the bearing height used by yard grounding; the raised noses
        // and open seat make these salazki readable before the repair reveal.
        var runnerProfile = new Vector2[]
        {
            new(-.60f, .22f), new(.24f, .22f), new(.38f, .25f),
            new(.49f, .33f), new(.55f, .45f), new(.56f, .53f)
        };
        foreach (var x in new[] { -.24f, .24f })
        {
            var side = x < 0f ? "West" : "East";
            for (var part = 0; part < runnerProfile.Length - 1; part++)
            {
                var start = new Vector3(x, runnerProfile[part].Y, runnerProfile[part].X);
                var end = new Vector3(x, runnerProfile[part + 1].Y, runnerProfile[part + 1].X);
                var runner = AddVisualBox(sled, $"SledRunner{side}{part}",
                    new(.07f, .09f, start.DistanceTo(end) + .012f), (start + end) * .5f,
                    "5b4a3a", "wood_prop");
                runner.LookAt(sled.ToGlobal(end), Vector3.Up);
            }
            foreach (var z in new[] { -.32f, .35f })
                AddVisualBox(sled, $"SledSupport{side}{z}", new(.065f, .19f, .07f),
                    new(x, .33f, z), "69543f", "wood_prop");
            AddVisualBox(sled, $"SledBackPost{side}", new(.055f, .34f, .055f),
                new(x, .49f, -.42f), "69543f", "wood_prop");
        }
        for (var slat = 0; slat < 4; slat++)
        {
            var x = -.225f + slat * .15f;
            AddVisualBox(sled, $"SledSeatSlat{slat}", new(.13f, .055f, .92f),
                new(x, .42f, .05f), slat % 2 == 0 ? "8c7354" : "766048", "wood_prop");
            // Patchy snow leaves the slat ends and the gaps visible.
            AddVisualBox(sled, $"SledSeatSnow{slat}", new(.105f, .018f, .55f + slat % 2 * .12f),
                new(x, .457f, -.04f + slat % 2 * .06f), "eef2f6", "snow_ground");
        }
        foreach (var y in new[] { .48f, .60f })
            AddVisualBox(sled, $"SledBackRail{y}", new(.62f, .065f, .055f),
                new(0f, y, -.42f), "80684f", "wood_prop");
    }

    /// <summary>
    /// Distant mosque minaret silhouette: a presentation-only skyline
    /// landmark that identifies the Tatar village without creating an
    /// enterable zone. No text, no ornament; cultural review remains open.
    /// </summary>
    private static void AddDistantMinaret(Node3D parent, string name, Vector3 anchor, float scale)
    {
        var minaret = new Node3D
        {
            Name = name,
            Position = anchor,
            Scale = Vector3.One * scale
        };
        minaret.SetMeta("presentationOnly", true);
        minaret.SetMeta("visualOnly", true);
        minaret.SetMeta("collisionOwner", "none");
        minaret.SetMeta("navigationOwner", "none");
        minaret.SetMeta("interactionOwner", "none");
        minaret.SetMeta("presentationRole", "distant minaret silhouette landmark; not enterable");
        minaret.SetMeta("culturalReview", "open — Tatar/islamic presentation must be reviewed by a consultant");
        parent.AddChild(minaret);

        // Restrained eight-sided masonry gives the distant landmark a legible
        // village silhouette without turning it into a generic box or adding
        // religious symbols. Keep every existing child name for route/frame
        // probes and authored presentation checks.
        void AddMinaretOctagon(string childName, float bottomRadius, float topRadius,
            float height, float centerY, string color, string surface)
        {
            var mesh = new MeshInstance3D
            {
                Name = childName,
                Position = new Vector3(0f, centerY, 0f),
                Mesh = new CylinderMesh
                {
                    BottomRadius = bottomRadius,
                    TopRadius = topRadius,
                    Height = height,
                    RadialSegments = 8,
                    Rings = 1
                },
                MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface)
            };
            mesh.SetMeta("visualOnly", true);
            minaret.AddChild(mesh);
        }

        AddMinaretOctagon("MinaretShaft", 0.72f, 0.60f, 14.65f, 9.25f, "7d7a6c", "plaster");
        AddMinaretOctagon("MinaretBase", 1.30f, 1.14f, 2.10f, 1.05f, "6e6b5f", "stone");
        AddMinaretOctagon("MinaretBalcony", 0.99f, 0.91f, 0.22f, 16.66f, "8a8676", "plaster");
        AddMinaretOctagon("MinaretUpperShaft", 0.52f, 0.42f, 2.20f, 17.87f, "84806f", "plaster");
        AddMinaretOctagon("MinaretCap", 0.70f, 0.62f, 0.18f, 19.06f, "6d6a5e", "stone");
        // A slim muted green roof keeps the skyline local and winter-soft;
        // there is no crescent, calligraphy, or invented religious ornament.
        AddMinaretOctagon("MinaretSpire", 0.64f, 0.035f, 1.28f, 19.79f, "52685f", "roof_metal");
        AddMinaretOctagon("MinaretSpireTip", 0.035f, 0.012f, 0.97f, 20.915f, "6d6a5e", "stone");
        AddMinaretOctagon("MinaretSnowCap", 1.05f, 0.97f, 0.11f, 16.825f, "eef2f6", "snow_ground");
        AddMinaretOctagon("MinaretBaseSnow", 1.32f, 1.22f, 0.11f, 2.155f, "eef2f6", "snow_ground");
    }

/// <summary>
    /// The river between the village and the forest, with the old broken bridge.
    /// Canon decision (author, 2026-09-14): the river plus the broken bridge are the
    /// boundary, and the only passable crossing is the authored culvert on the road.
    /// Water and banks are presentation; layer-2 blockers keep the player out of the
    /// water except across the road gap.
    /// </summary>
    private static void AddVillageRiverAndBrokenBridge(Node3D core)
    {
        var river = new Node3D { Name = "VillageForestRiver", Position = Vector3.Zero };
        river.SetMeta("presentationOnly", true);
        river.SetMeta("visualOnly", true);
        river.SetMeta("collisionOwner", "river-blocker");
        river.SetMeta("navigationOwner", "none");
        river.SetMeta("interactionOwner", "none");
        river.SetMeta(
            "presentationRole",
            "canon village/forest boundary: frozen river with snow banks and the old broken bridge; the culvert crossing is the only passable line");
        core.AddChild(river);

        var proxy = new StaticBody3D { Name = "RiverCollisionProxy", CollisionLayer = 2, CollisionMask = 0 };
        proxy.SetMeta("collisionOwner", "river-blocker");
        proxy.SetMeta("collisionStatus", "authored-blocker-layer-2");
        river.AddChild(proxy);

        var water = PainterlyMaterialLibrary.ForColor("33463f", "water");
        var ice = PainterlyMaterialLibrary.ForColor("5c6f74", "ice");
        var bank = PainterlyMaterialLibrary.ForColor("eef2f6", "snow_ground");
        var blockedShapes = 0;
        var openAtRoad = 0;
        // The river runs on under the far bank of the ravine to the east ring.
        for (var x = -60f; x <= 86f; x += 4f)
        {
            // A gentle meander keeps the line from reading as a ruler.
            var z = -88f + 3.2f * Mathf.Sin(x / 12f) + 1.4f * Mathf.Sin(x / 4.3f);
            var road = AgentBAct1HeightField.RoadInfo(x, z);
            var inRoadGap = (float)(road.Distance - road.HalfWidth) < 1.4f;
            var ground = (float)AgentBAct1HeightField.Ground(x, z);
            if (inRoadGap)
            {
                // The road crosses on the culvert: keep the channel visible but
                // leave the corridor free of blockers.
                openAtRoad++;
            }

            var openLead = Mathf.Abs(x) % 12f < 5f;
            var slab = new MeshInstance3D
            {
                Name = $"RiverIce_{x:0}",
                Position = new Vector3(x, ground + .10f, z),
                RotationDegrees = new Vector3(0f, 18f * Mathf.Sin(x / 9f), 0f),
                Mesh = new BoxMesh { Size = new Vector3(4.4f, .34f, 9.6f) },
                MaterialOverride = openLead ? water : ice
            };
            slab.SetMeta("visualOnly", true);
            river.AddChild(slab);
            if (!openLead && Mathf.Abs(x) % 8f < 4f)
            {
                // Broken ice along the channel so the water line never reads as a
                // smooth white floor from the bank.
                var lead = new MeshInstance3D
                {
                    Name = $"RiverLead_{x:0}",
                    Position = new Vector3(x + 1.1f, ground + .22f, z + 1.6f * Mathf.Sin(x / 3.1f)),
                    RotationDegrees = new Vector3(0f, 24f * Mathf.Cos(x / 5f), 0f),
                    Mesh = new BoxMesh { Size = new Vector3(1.4f, .12f, 3.2f) },
                    MaterialOverride = water
                };
                lead.SetMeta("visualOnly", true);
                river.AddChild(lead);
            }

            foreach (var side in new[] { -1f, 1f })
            {
                var bankZ = z + side * 5.7f;
                // Banks sit on their own ground. The old offset took the channel
                // -centre height and added a fixed 2.62 m, which is the carved
                // bank top only where the ravine is actually cut; where the road
                // gap flattens the channel that same number left the bank
                // hanging 2.6 m over open snow with nothing under it.
                var bankGround = (float)AgentBAct1HeightField.CollisionGround(x, bankZ);
                var bankMesh = new MeshInstance3D
                {
                    Name = $"RiverBankSnow_{x:0}_{(side < 0 ? "north" : "south")}",
                    Position = new Vector3(x, bankGround + .10f, bankZ),
                    RotationDegrees = new Vector3(side * 8f, 0f, 0f),
                    Mesh = new BoxMesh { Size = new Vector3(4.6f, .34f, 2.4f) },
                    MaterialOverride = bank
                };
                bankMesh.SetMeta("visualOnly", true);
                river.AddChild(bankMesh);
            }

            if ((float)(road.Distance - road.HalfWidth) > 2.6f)
            {
                blockedShapes += AddForestBankWindfall(river, proxy, x, z);
            }

            if (inRoadGap)
            {
                continue;
            }

            // The bed blocker sits under the ice, inside the ravine: it stops a
            // player who tries to cross the frozen channel without floating in view,
            // and the visible reason stays the river and its banks.
            proxy.AddChild(new CollisionShape3D
            {
                Name = $"RiverBlocker_{x:0}",
                Position = new Vector3(x, ground + .28f, z),
                Shape = new BoxShape3D { Size = new Vector3(4.3f, 1.0f, 8.6f) }
            });
            blockedShapes++;
        }

        // The old broken bridge: two stone abutments and a deck that ends over the
        // water, with a fallen span and a leaning post. Unpassable by design.
        var bridgeX = 15f;
        var bridgeZ = -88f + 3.2f * Mathf.Sin(bridgeX / 12f) + 1.4f * Mathf.Sin(bridgeX / 4.3f);
        // The bridge straddles the channel, so anchor it to the shoulder height.
        var bridgeGround = (float)AgentBAct1HeightField.Ground(bridgeX, bridgeZ + 5.1f);
        var bridge = new Node3D { Name = "ForestBridgeBroken", Position = new Vector3(bridgeX, bridgeGround, bridgeZ) };
        bridge.SetMeta("presentationOnly", true);
        bridge.SetMeta("visualOnly", true);
        bridge.SetMeta("presentationRole", "old broken river bridge: stone abutments and a collapsed span; not crossable");
        river.AddChild(bridge);
        foreach (var side in new[] { -1f, 1f })
        {
            AddVisualBox(bridge, $"BridgeAbutment_{(side < 0 ? "near" : "far")}", new(2.2f, 2.6f, 1.4f),
                new(0f, 1.0f, side * 5.0f), "6f6a5d", "stone");
            proxy.AddChild(new CollisionShape3D
            {
                Name = $"BridgeAbutmentBlocker_{(side < 0 ? "near" : "far")}",
                Position = new Vector3(bridgeX, bridgeGround + 1.0f, bridgeZ + side * 5.0f),
                Shape = new BoxShape3D { Size = new Vector3(2.1f, 2.5f, 1.3f) }
            });
        }

        AddVisualBox(bridge, "BridgeDeckNear", new(1.6f, 0.22f, 3.4f), new(0f, 1.62f, -3.1f), "59493a", "wood", rollDegrees: 1.5f);
        AddVisualBox(bridge, "BridgeDeckFallen", new(1.5f, 0.20f, 3.0f), new(0.55f, 1.05f, 0.4f), "4f4133", "wood", rollDegrees: 34f);
        AddVisualBox(bridge, "BridgeDeckHintFar", new(1.4f, 0.20f, 1.6f), new(0.1f, 1.46f, 3.6f), "57493b", "wood", rollDegrees: -6f);
        AddVisualBox(bridge, "BridgeRailNearLeft", new(0.12f, 0.86f, 3.2f), new(-0.72f, 2.02f, -3.1f), "6d5845", "wood");
        AddVisualBox(bridge, "BridgePostLeaning", new(0.16f, 1.35f, 0.16f), new(0.9f, 1.3f, 1.9f), "6d5845", "wood", rollDegrees: 24f);
        AddVisualBox(bridge, "BridgeRope", new(0.05f, 0.05f, 2.1f), new(0.55f, 1.95f, 0.2f), "b7a07c", "wood", rollDegrees: -8f);

        var stepShapes = AddRiverBankSteps(river, proxy);
        blockedShapes += stepShapes;
        river.SetMeta("riverBankStepCount", stepShapes);
        river.SetMeta("riverBlockerShapeCount", blockedShapes);
        river.SetMeta("riverRoadGapSamples", openAtRoad);
        proxy.SetMeta("riverBlockerShapeCount", blockedShapes + 2);
        GD.Print($"act1-river: ice_slabs={blockedShapes - stepShapes + openAtRoad} blockers={blockedShapes} bank_steps={stepShapes} road_gap_samples={openAtRoad} bridge=broken@({bridgeX:0},{bridgeZ:0})");
    }

/// <summary>
    /// Village mosque complex around the existing minaret: hall with a low dome,
    /// a closed street entrance facing the village, a plinth, a courtyard wall with
    /// a gate opening and an ablution trough. Presentation geometry with layer-2
    /// blockers so the player cannot walk through it; the courtyard interior is not
    /// wired to any scene in Act I, which stays a hook for the next act.
    /// </summary>
    private static void AddVillageMosque(Node3D parent, Vector3 minaretAnchor)
    {
        var complex = new Node3D { Name = "VillageMosqueComplex", Position = minaretAnchor };
        complex.SetMeta("presentationOnly", true);
        complex.SetMeta("visualOnly", true);
        complex.SetMeta("collisionOwner", "mosque-blocker");
        complex.SetMeta("navigationOwner", "none");
        complex.SetMeta("interactionOwner", "none");
        complex.SetMeta(
            "presentationRole",
            "village mosque: hall, dome, closed entrance and courtyard around the authored minaret; interior stays a hook, no scene is wired");
        complex.SetMeta(
            "culturalReview",
            "open — Tatar/Islamic presentation of the mosque must be reviewed by a consultant");
        parent.AddChild(complex);

        var proxy = new StaticBody3D
        {
            Name = "MosqueCollisionProxy",
            CollisionLayer = 2,
            CollisionMask = 0
        };
        proxy.SetMeta("collisionOwner", "mosque-blocker");
        proxy.SetMeta("collisionStatus", "authored-blocker-layer-2");
        complex.AddChild(proxy);

        var blocked = 0;
        void Wall(string name, Vector3 size, Vector3 localPosition, string color, string surface)
        {
            AddVisualBox(complex, name, size, localPosition, color, surface);
            proxy.AddChild(new CollisionShape3D
            {
                Name = $"{name}_Blocker",
                Position = localPosition,
                Shape = new BoxShape3D { Size = new Vector3(
                    Mathf.Max(size.X - .06f, .12f), Mathf.Min(size.Y, 4.2f), Mathf.Max(size.Z - .06f, .12f)) }
            });
            blocked++;
        }

        // Hall shell: 11 x 5.2 x 8.5 m, its east wall facing the village, the
        // minaret standing at that corner. The street wall keeps a 1.5 m door gap.
        const float halfX = 5.5f;
        const float halfZ = 4.25f;
        const float wallHeight = 5.2f;
        var ground = (float)AgentBAct1HeightField.Ground(minaretAnchor.X - 7f, minaretAnchor.Z - 1f);
        var baseY = ground - minaretAnchor.Y;
        var origin = new Vector3(-7f, baseY, -1f);
        Wall("MosqueHallWest", new(0.55f, wallHeight, halfZ * 2f), origin + new Vector3(-halfX, wallHeight * .5f, 0f), "b9b3a2", "plaster");
        Wall("MosqueHallNorth", new(halfX * 2f, wallHeight, 0.55f), origin + new Vector3(0f, wallHeight * .5f, -halfZ), "b9b3a2", "plaster");
        Wall("MosqueHallSouth", new(halfX * 2f, wallHeight, 0.55f), origin + new Vector3(0f, wallHeight * .5f, halfZ), "b9b3a2", "plaster");
        // Street wall in two segments around the entrance gap.
        Wall("MosqueHallEastLeft", new(0.55f, wallHeight, 3.4f), origin + new Vector3(halfX, wallHeight * .5f, -2.55f), "c2bcab", "plaster");
        Wall("MosqueHallEastRight", new(0.55f, wallHeight, 3.4f), origin + new Vector3(halfX, wallHeight * .5f, 2.55f), "c2bcab", "plaster");
        Wall("MosqueHallEastLintel", new(0.55f, 1.6f, 1.7f), origin + new Vector3(halfX, wallHeight - .8f, 0f), "c2bcab", "plaster");
        Wall("MosquePlinth", new(halfX * 2f + .5f, 0.5f, halfZ * 2f + .5f), origin + new Vector3(0f, .25f, 0f), "7c7768", "stone");

        // Roof: two gable slabs and a low eight-sided dome with a snow cap, so the
        // skyline reads as a mosque rather than a barn.
        AddVisualBox(complex, "MosqueRoofWest", new(6.4f, 0.30f, halfZ * 2f + 1.0f),
            origin + new Vector3(-2.6f, wallHeight + 1.05f, 0f), "4d5b56", "roof_metal", rollDegrees: 22f);
        AddVisualBox(complex, "MosqueRoofEast", new(6.4f, 0.30f, halfZ * 2f + 1.0f),
            origin + new Vector3(2.6f, wallHeight + 1.05f, 0f), "4d5b56", "roof_metal", rollDegrees: -22f);
        Wall("MosqueRoofGableSouth", new(0.30f, 1.5f, 1.0f), origin + new Vector3(0f, wallHeight + .55f, halfZ + .7f), "8f8a7b", "plaster");
        Wall("MosqueRoofGableNorth", new(0.30f, 1.5f, 1.0f), origin + new Vector3(0f, wallHeight + .55f, -halfZ - .7f), "8f8a7b", "plaster");
        var dome = new MeshInstance3D
        {
            Name = "MosqueDome",
            Position = origin + new Vector3(0f, wallHeight + .35f, 0f),
            Mesh = new CylinderMesh { BottomRadius = 2.5f, TopRadius = 0.35f, Height = 1.5f, RadialSegments = 8, Rings = 1 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("5d6a63", "roof_metal")
        };
        dome.SetMeta("visualOnly", true);
        complex.AddChild(dome);
        var domeSnow = new MeshInstance3D
        {
            Name = "MosqueDomeSnow",
            Position = origin + new Vector3(0f, wallHeight + 1.12f, 0f),
            Mesh = new CylinderMesh { BottomRadius = 0.62f, TopRadius = 0.10f, Height = 0.14f, RadialSegments = 8, Rings = 1 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("eef2f6", "snow_ground")
        };
        domeSnow.SetMeta("visualOnly", true);
        complex.AddChild(domeSnow);

        // Closed entrance: recessed frame, threshold step and a wooden leaf. The
        // door stays shut in Act I; opening it is the next act's business.
        AddVisualBox(complex, "MosqueEntranceRecess", new(0.22f, 2.3f, 1.55f), origin + new Vector3(halfX - .12f, 1.15f, 0f), "54504a", "plaster");
        AddVisualBox(complex, "MosqueEntranceDoor", new(0.16f, 2.1f, 1.4f), origin + new Vector3(halfX + .02f, 1.05f, 0f), "584433", "wood");
        AddVisualBox(complex, "MosqueEntranceStep", new(1.3f, 0.16f, 2.1f), origin + new Vector3(halfX + .6f, .08f, 0f), "6f6a5d", "stone");
        AddVisualBox(complex, "MosqueEntranceAwning", new(1.1f, 0.14f, 2.3f), origin + new Vector3(halfX + .5f, 2.45f, 0f), "4a5750", "roof_metal", rollDegrees: 10f);

        // Courtyard wall with a gate opening on the village side and a trough at
        // the inner corner, so the complex reads as a place, not a lone box.
        const float yardX = 9.5f;
        const float yardZ = 7.0f;
        Wall("MosqueYardWest", new(0.4f, 1.5f, yardZ * 2f), origin + new Vector3(-yardX, .75f, 0f), "9a9182", "plaster");
        Wall("MosqueYardNorth", new(yardX * 2f, 1.5f, 0.4f), origin + new Vector3(0f, .75f, -yardZ), "9a9182", "plaster");
        Wall("MosqueYardSouth", new(yardX * 2f, 1.5f, 0.4f), origin + new Vector3(0f, .75f, yardZ), "9a9182", "plaster");
        Wall("MosqueYardGateLeft", new(1.1f, 1.5f, 0.4f), origin + new Vector3(halfX + .95f, .75f, -3.6f), "9a9182", "plaster");
        Wall("MosqueYardGateRight", new(1.1f, 1.5f, 0.4f), origin + new Vector3(halfX + .95f, .75f, 3.6f), "9a9182", "plaster");
        AddVisualBox(complex, "MosqueYardGatePostLeft", new(0.22f, 1.8f, 0.22f), origin + new Vector3(halfX + .95f, .9f, -2.9f), "6d5845", "wood");
        AddVisualBox(complex, "MosqueYardGatePostRight", new(0.22f, 1.8f, 0.22f), origin + new Vector3(halfX + .95f, .9f, 2.9f), "6d5845", "wood");
        AddVisualBox(complex, "MosqueAblutionTrough", new(1.5f, 0.62f, 0.7f), origin + new Vector3(-2.6f, .31f, yardZ - 1.1f), "7b7669", "stone");
        AddVisualBox(complex, "MosqueAblutionPost", new(0.16f, 1.35f, 0.16f), origin + new Vector3(-3.4f, .68f, yardZ - 1.1f), "6d5845", "wood");
        AddVisualBox(complex, "MosqueYardSnowBank", new(yardX * 2f - 1.2f, 0.42f, 0.85f), origin + new Vector3(0f, .21f, -yardZ + .8f), "eef2f6", "snow_ground");

        complex.SetMeta("mosqueBlockerShapeCount", blocked);
        proxy.SetMeta("mosqueBlockerShapeCount", blocked);
    }

/// <summary>
    /// Unreachable winter backdrops: layered village, forest and ridge
    /// silhouettes beyond the walkable envelope, so the horizon never reads
    /// as an empty edge. Presentation-only, no collision or interaction.
    /// </summary>
    private static void AddDistantHouseRow(Node3D parent, string name, Vector3 origin, int count,
        float spacing, float yawDegrees, float scale)
    {
        var row = new Node3D { Name = name, Position = origin, RotationDegrees = new Vector3(0f, yawDegrees, 0f) };
        row.SetMeta("presentationOnly", true);
        row.SetMeta("visualOnly", true);
        row.SetMeta("collisionOwner", "none");
        row.SetMeta("navigationOwner", "none");
        row.SetMeta("interactionOwner", "none");
        row.SetMeta("presentationRole", "unreachable distant village row");
        parent.AddChild(row);

        for (var index = 0; index < count; index++)
        {
            var x = (index - (count - 1) * .5f) * spacing + Mathf.Sin(index * 2.37f) * 4.5f;
            var z = Mathf.Sin(index * 1.71f) * 9f;
            var world = row.ToGlobal(new Vector3(x, 0f, z));
            world.Y = BackdropGroundHeight(world.X, world.Z) - .04f;
            var footing = new Node3D { Name = $"House{index}", Position = row.ToLocal(world) };
            row.AddChild(footing);
            footing.RotationDegrees = new Vector3(0, Mathf.Sin(index * 2.71f) * 16, 0);
            footing.SetMeta("groundContactDepth", .04f);
            var width = 6.4f * scale * (0.85f + (index % 3) * 0.12f);
            var depth = 5.4f * scale;
            var height = 2.9f * scale;
            AddVisualBox(footing, $"DistantHouseWall{index}", new(width, height, depth),
                new(0f, height * 0.5f, 0f), index % 2 == 0 ? "8b8c84" : "84837a", "plaster");
            AddVisualPitchedRoof(footing, $"DistantHouseRoof{index}", width, depth, height, 1.25f * scale, .4f, "6e7570");
            var roofSnow = AddVisualPitchedRoof(footing, $"DistantHouseRoofSnow{index}", width, depth, height + .14f, 1.25f * scale, .37f, "e8edf0");
            roofSnow.MaterialOverride = PainterlyMaterialLibrary.ForColor("e8edf0", "snow_roof");
            AddVisualBox(footing, $"DistantHouseChimney{index}", new(0.55f, 1.5f, 0.55f),
                new(width * 0.24f, height + 0.95f, 0f), "6f6a60", "stone");
            AddVisualBox(footing, $"DistantHouseFence{index}", new(width * 0.9f, 1.05f, 0.12f),
                new(0f, 0.52f, depth * 0.62f), "6f6455", "wood_fence");
        }
    }

    private static void AddDistantForestBand(Node3D parent, string name, Vector3 origin, int count,
        float spacing, float yawDegrees, float scale, bool conifer)
    {
        var band = new Node3D { Name = name, Position = origin, RotationDegrees = new Vector3(0f, yawDegrees, 0f) };
        band.SetMeta("presentationOnly", true);
        band.SetMeta("visualOnly", true);
        band.SetMeta("collisionOwner", "none");
        band.SetMeta("navigationOwner", "none");
        band.SetMeta("interactionOwner", "none");
        band.SetMeta("presentationRole", conifer ? "unreachable dark forest band" : "unreachable winter woodland band");
        parent.AddChild(band);

        var roofs = AgentBAct1ExteriorLayer.BuildingRoofBounds(parent);
        var ridgeGeometry = FindDescendants<MeshInstance3D>(parent)
            .Where(mesh => mesh.Name == "RidgeSurface" && mesh.Mesh is not null)
            .Select(mesh => (Vertices: mesh.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array()
                .Select(vertex => mesh.ToGlobal(vertex)).ToArray(),
                Indices: mesh.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Index].AsInt32Array())).ToArray();
        for (var index = 0; index < count; index++)
        {
            var x = (index - (count - 1) * .5f) * spacing + Mathf.Sin(index * 2.37f) * spacing * .44f;
            var height = (conifer ? 7.5f : 6.2f) * scale * (0.8f + (index % 5) * 0.09f);
            var z = (Mathf.Sin(index * 1.71f) * 13f + Mathf.Cos(index * 2.37f) * 7f) * scale;
            var world = band.ToGlobal(new Vector3(x, 0f, z));
            var support = BackdropGroundHeight(world.X, world.Z);
            // A woodland root may lie on the foot of a snow ridge. Sample
            // its rendered triangles as well as the apron beneath it.
            foreach (var geometry in ridgeGeometry)
            for (var i = 0; i < geometry.Indices.Length; i += 3)
            {
                var a = geometry.Vertices[geometry.Indices[i]];
                var b = geometry.Vertices[geometry.Indices[i + 1]];
                var c = geometry.Vertices[geometry.Indices[i + 2]];
                var denominator = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
                var u = ((b.Z - c.Z) * (world.X - c.X) + (c.X - b.X) * (world.Z - c.Z)) / denominator;
                var v = ((c.Z - a.Z) * (world.X - c.X) + (a.X - c.X) * (world.Z - c.Z)) / denominator;
                if (u >= 0 && v >= 0 && u + v <= 1)
                    support = Mathf.Max(support, u * a.Y + v * b.Y + (1 - u - v) * c.Y);
            }
            world.Y = support - .04f;
            if (AgentBAct1ExteriorLayer.UnderBuildingRoof(world, roofs)) continue;
            var footing = new Node3D { Name = $"Tree{index}", Position = band.ToLocal(world) };
            band.AddChild(footing);
            footing.SetMeta("groundContactDepth", .04f);
            var variant = conifer ? "WinterFarSpruce_1" : index % 3 == 0 ? "WinterFarLinden_1" : "WinterFarBirch_1";
            var source = parent.GetNode<AgentBAct1ExteriorLayer>("AgentBExteriorWorld").FoliageMesh(variant, conifer ? "kara" : "village");
            var sourceHeight = source.GetAabb().Size.Y;
            footing.AddChild(new MeshInstance3D
            {
                Name = $"BandTree{index}", Mesh = source,
                Scale = Vector3.One * (height / sourceHeight),
                RotationDegrees = new Vector3(0, index * 137.51f, 0)
            });
        }
    }

    private static void AddDistantRidge(Node3D parent, string name, Vector3 origin, float length,
        float height, float depth, float yawDegrees)
    {
        var ridge = new Node3D { Name = name, Position = origin, RotationDegrees = new Vector3(0f, yawDegrees, 0f) };
        ridge.SetMeta("presentationOnly", true);
        ridge.SetMeta("visualOnly", true);
        ridge.SetMeta("collisionOwner", "none");
        ridge.SetMeta("navigationOwner", "none");
        ridge.SetMeta("interactionOwner", "none");
        ridge.SetMeta("presentationRole", "far snow ridge silhouette");
        parent.AddChild(ridge);

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        const int along = 32, across = 16;
        Vector3 Point(int x, int z)
        {
            var u = x / (float)along * 2f - 1f;
            var v = z / (float)across * 2f - 1f;
            var local = new Vector3(u * length * .5f, 0f, v * depth * .5f);
            var world = ridge.ToGlobal(local);
            var dome = Mathf.Pow(Mathf.Max(0f, 1f - u * u), 1.5f)
                * Mathf.Pow(Mathf.Max(0f, 1f - v * v), 2f);
            world.Y = (float)AgentBAct1HeightField.Ground(world.X, world.Z) + .01f
                + height * dome * (.72f + .18f * Mathf.Sin(u * 8f + v * 3f) + .1f * Mathf.Cos(u * 17f));
            return ridge.ToLocal(world);
        }
        for (var z = 0; z < across; z++)
        for (var x = 0; x < along; x++)
        foreach (var point in new[] { Point(x,z), Point(x+1,z), Point(x,z+1),
                     Point(x+1,z), Point(x+1,z+1), Point(x,z+1) })
            surface.AddVertex(point);
        surface.Index();
        surface.GenerateNormals();
        ridge.AddChild(new MeshInstance3D { Name = "RidgeSurface", Mesh = surface.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("c4cdd3", "snow_ground") });
    }

    private static float BackdropGroundHeight(float x, float z)
    {
        // Match the actual eight-metre apron triangles, not the continuous
        // source noise between their vertices. Interior roots use the collider.
        var minX = AgentBAct1HeightField.MinX; var maxX = AgentBAct1HeightField.MaxX;
        var minZ = AgentBAct1HeightField.MinZ; var maxZ = AgentBAct1HeightField.MaxZ;
        if (x >= minX && x <= maxX && z >= minZ && z <= maxZ)
            return AgentBAct1HeightField.CollisionGround(x, z);
        var bounds = x < minX ? new Vector4(-420, minX, -420, 360)
            : x > maxX ? new Vector4(maxX, 420, -420, 360)
            : z < minZ ? new Vector4(minX, maxX, -420, minZ)
            : new Vector4(minX, maxX, maxZ, 360);
        var dx = (bounds.Y - bounds.X) / Mathf.Ceil((bounds.Y - bounds.X) / 8f);
        var dz = (bounds.W - bounds.Z) / Mathf.Ceil((bounds.W - bounds.Z) / 8f);
        var gx = (x - bounds.X) / dx; var gz = (z - bounds.Z) / dz;
        var u = gx - Mathf.Floor(gx); var v = gz - Mathf.Floor(gz);
        var x0 = bounds.X + Mathf.Floor(gx) * dx; var z0 = bounds.Z + Mathf.Floor(gz) * dz;
        var a = (float)AgentBAct1HeightField.Ground(x0, z0);
        var b = (float)AgentBAct1HeightField.Ground(x0 + dx, z0);
        var c = (float)AgentBAct1HeightField.Ground(x0, z0 + dz);
        var d = (float)AgentBAct1HeightField.Ground(x0 + dx, z0 + dz);
        return u + v <= 1 ? a * (1 - u - v) + b * u + c * v
            : b * (1 - v) + d * (u + v - 1) + c * (1 - u);
    }

    private static void AddBackdropGround(Node3D parent)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Rectangle(float minX, float maxX, float minZ, float maxZ)
        {
            var nx = Mathf.CeilToInt((maxX - minX) / 8f);
            var nz = Mathf.CeilToInt((maxZ - minZ) / 8f);
            Vector3 Point(int x, int z)
            {
                var wx = Mathf.Lerp(minX, maxX, x / (float)nx);
                var wz = Mathf.Lerp(minZ, maxZ, z / (float)nz);
                return parent.ToLocal(new Vector3(wx, (float)AgentBAct1HeightField.Ground(wx, wz), wz));
            }
            for (var z = 0; z < nz; z++)
            for (var x = 0; x < nx; x++)
            foreach (var point in new[] { Point(x,z), Point(x+1,z), Point(x,z+1),
                         Point(x+1,z), Point(x+1,z+1), Point(x,z+1) })
                surface.AddVertex(point);
        }
        Rectangle(-420f, AgentBAct1HeightField.MinX, -420f, 360f);
        Rectangle(AgentBAct1HeightField.MaxX, 420f, -420f, 360f);
        Rectangle(AgentBAct1HeightField.MinX, AgentBAct1HeightField.MaxX, -420f, AgentBAct1HeightField.MinZ);
        Rectangle(AgentBAct1HeightField.MinX, AgentBAct1HeightField.MaxX, AgentBAct1HeightField.MaxZ, 360f);
        surface.Index();
        surface.GenerateNormals();
        var ground = new MeshInstance3D { Name = "BackdropGround", Mesh = surface.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("dce3e8", "snow_ground") };
        ground.SetMeta("presentationOnly", true);
        ground.SetMeta("collisionOwner", "none");
        parent.AddChild(ground);
    }

    /// <summary>
    /// Winter yard props: a haystack and a second wattle run, plus painted
    /// village trim accents (shutters/frames in muted blue-green).
    /// </summary>
    private static void AddSnowBank(Node3D parent, string name, Vector3 anchor, Vector3 to,
        float width, float height, float yawJitter)
    {
        var curve = new Curve3D { BakeInterval = .2f };
        curve.AddPoint(new Vector3(anchor.X, 0f, anchor.Z));
        var middle = (anchor + to) * .5f;
        middle.X += Mathf.Sin(yawJitter) * .12f;
        middle.Y = 0f;
        curve.AddPoint(middle);
        curve.AddPoint(new Vector3(to.X, 0f, to.Z));
        var mesh = AddVisualLandformSurface(parent, name, width, height,
            curve.GetBakedLength(), Vector3.Zero, "e8edf0", "snow_ground", 0f, true, curve);
        mesh.SetMeta("snowBankHeight", height);
        mesh.SetMeta("presentationOnly", true);
        mesh.SetMeta("collisionOwner", "none");
    }

    /// <summary>
    /// Lays unplowed snow banks along the main street by walking the road
    /// centre line (found by sampling RoadInfo) and offsetting to both
    /// shoulders. Presentation only; the cleared lane stays walkable.
    /// </summary>
    private static void AddMainStreetSnowBanks(Node3D parent)
    {
        var root = new Node3D { Name = "MainStreetSnowBanks" };
        root.SetMeta("presentationOnly", true);
        root.SetMeta("visualOnly", true);
        root.SetMeta("collisionOwner", "none");
        root.SetMeta("navigationOwner", "none");
        root.SetMeta("interactionOwner", "none");
        root.SetMeta("presentationRole", "unplowed snow banks flanking the cleared village street");
        parent.AddChild(root);

        for (var z = 16f; z >= -84f; z -= 4.0f)
        {
            var bestX = 0f;
            var bestClearance = float.MaxValue;
            var halfWidth = 3.0f;
            for (var x = -12f; x <= 12f; x += 0.25f)
            {
                var info = AgentBAct1HeightField.RoadInfo(x, z);
                var clearance = (float)(info.Distance - info.HalfWidth);
                if (clearance < bestClearance)
                {
                    bestClearance = clearance;
                    bestX = x;
                    halfWidth = (float)info.HalfWidth;
                }
            }

            if (bestClearance > 1.2f)
            {
                continue; // between roads: no bank here
            }

            foreach (var side in new[] { -1f, 1f })
            {
                var wobble = Mathf.Sin(z * 0.7f + side) * 0.25f;
                var bankX = bestX + side * (halfWidth + .9f + wobble);
                var atBank = AgentBAct1HeightField.RoadInfo(bankX, z);
                // House/FAP approaches cross the bank: keep those gates clear.
                if (atBank.Distance - atBank.HalfWidth <= .7f) continue;
                var bankStart = new Vector3(bankX, 0f, z - 2.5f);
                var bankEnd = new Vector3(bankX + wobble * .5f, 0f, z + 2.5f);
                var bankName = $"StreetBank{(side > 0 ? "E" : "W")}_{Mathf.RoundToInt(z)}";
                var bankHeight = .48f + .11f * Mathf.Sin(z * .31f + side);
                // The maintained footbridge crosses this bank: leave its real
                // aperture instead of drawing a snow ridge through the deck.
                if (side > 0 && bankStart.Z < ZiratCulvertZ + .7f && bankEnd.Z > ZiratCulvertZ - .7f)
                {
                    if (bankStart.Z < ZiratCulvertZ - .7f)
                        AddSnowBank(root, bankName + "Before", bankStart,
                            bankStart.Lerp(bankEnd, (ZiratCulvertZ - .7f - bankStart.Z) / 5f),
                            1.65f, bankHeight, z + side);
                    if (bankEnd.Z > ZiratCulvertZ + .7f)
                        AddSnowBank(root, bankName + "After",
                            bankStart.Lerp(bankEnd, (ZiratCulvertZ + .7f - bankStart.Z) / 5f), bankEnd,
                            1.65f, bankHeight, z + side);
                }
                else AddSnowBank(root, bankName, bankStart, bankEnd, 1.65f, bankHeight, z + side);
            }
        }
        foreach (var side in new[] { -1f, 1f })
            AddSnowBank(root, side < 0 ? "UnplowedWest" : "UnplowedEast",
                new(side * 6.1f, 0f, 14f), new(side * 6.8f, 0f, -12f),
                3.1f, side < 0 ? .32f : .38f, side * 2.3f);

    }

    private static void AddVisualHaystack(Node3D parent, string name, Vector3 anchor, float scale, float yawDegrees)
    {
        var stack = new Node3D { Name = name, Position = anchor, RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Scale = Vector3.One * scale };
        stack.SetMeta("visualOnly", true);
        stack.SetMeta("presentationRole", "winter haystack (стог сена) yard detail");
        parent.AddChild(stack);

        var ground = stack.GlobalPosition;
        ground.Y = AgentBAct1HeightField.CollisionGround(ground.X, ground.Z);
        stack.GlobalPosition = ground;
        // One packed, oval stack, with its lower skirt seated into the actual
        // terrain. The former three boxes and horizontal snow strips looked
        // like shelving and had neither a hay silhouette nor physical contact.
        const int segments = 18;
        ArrayMesh Profile((float Height, float Radius)[] rings, bool seatBase)
        {
            using var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            var points = new Vector3[rings.Length, segments];
            for (var ring = 0; ring < rings.Length; ring++)
            for (var index = 0; index < segments; index++)
            {
                var angle = Mathf.Tau * index / segments;
                var irregularity = 1f + .035f * Mathf.Sin(angle * 3f + .7f)
                    + .022f * Mathf.Cos(angle * 5f);
                var point = new Vector3(Mathf.Cos(angle) * rings[ring].Radius * irregularity,
                    rings[ring].Height, Mathf.Sin(angle) * rings[ring].Radius * .84f * irregularity);
                if (seatBase && ring == 0)
                {
                    var world = stack.ToGlobal(point);
                    world.Y = AgentBAct1HeightField.CollisionGround(world.X, world.Z) - .025f;
                    point = stack.ToLocal(world);
                }
                points[ring, index] = point;
            }
            void Vertex(Vector3 point, float u)
            {
                surface.SetUV(new Vector2(u, point.Y * .75f));
                surface.AddVertex(point);
            }
            for (var ring = 0; ring < rings.Length - 1; ring++)
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                var u = index / (float)segments * 3f;
                var v = (index + 1) / (float)segments * 3f;
                // Godot's clockwise front faces and SurfaceTool normals must
                // both point out of the packed stack.
                Vertex(points[ring, index], u); Vertex(points[ring, next], v); Vertex(points[ring + 1, index], u);
                Vertex(points[ring, next], v); Vertex(points[ring + 1, next], v); Vertex(points[ring + 1, index], u);
            }
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                Vertex(new Vector3(0, rings[^1].Height, 0), .5f);
                Vertex(points[rings.Length - 1, index], 0f);
                Vertex(points[rings.Length - 1, next], 1f);
                if (seatBase)
                {
                    Vertex(new Vector3(0, -.04f, 0), .5f);
                    Vertex(points[0, next], 1f); Vertex(points[0, index], 0f);
                }
            }
            surface.GenerateNormals();
            return surface.Commit();
        }
        var hayMaterial = PainterlyMaterialLibrary.ForColor("9b978c", "hay_fibers", sheltered: true);
        var hay = new MeshInstance3D { Name = "HayPackedBody",
            Mesh = Profile([(0f, 1.30f), (.60f, 1.27f), (1.28f, 1.08f), (1.90f, .76f), (2.34f, .34f), (2.58f, .035f)], true),
            MaterialOverride = hayMaterial };
        stack.AddChild(hay);
        var snow = new MeshInstance3D { Name = "HaySnowCap",
            Mesh = Profile([(1.91f, .79f), (2.37f, .36f), (2.62f, .038f)], false),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("e8edf1", "snow_ground") };
        stack.AddChild(snow);
        var pole = AddVisualBox(stack, "HayPole", new(.10f, 2.85f, .10f), new(0, 1.40f, 0), "6b5b46", "wood");
        var contact = NewKitBlockerProxy();
        stack.AddChild(contact);
        contact.AddChild(AuthoredSurfaceContact(contact, hay));
        contact.AddChild(AuthoredSurfaceContact(contact, snow));
        contact.AddChild(AuthoredSolidContact(contact, pole, "HayPoleContact"));
        stack.SetMeta("collisionOwner", "authored-kit-blocker");
        stack.SetMeta("groundingPolicy", "actual terrain at each lower-ring vertex; packed body and snow share visible triangle contacts");
    }

    private static void AddVisualStreetLandmark(Node3D parent, string name, Vector3 anchor, float yawDegrees, string labelText = "КАРА-УРМАН")
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
        var world = parent.ToGlobal(origin);
        world.Y = AgentBAct1HeightField.CollisionGround(world.X, world.Z) - .04f;
        var grass = new Node3D { Name = name, Position = parent.ToLocal(world) };
        grass.SetMeta("visualOnly", true);
        grass.SetMeta("vegetationStyle", "low-poly-grass-tuft");
        parent.AddChild(grass);
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var index = 0; index < 24; index++)
        {
            var phase = VegetationHash(origin, 73f + index);
            var angle = index * 2.399963f + phase;
            var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var across = new Vector3(-outward.Z, 0f, outward.X);
            var root = outward * (Mathf.Sqrt(index / 24f) * size * 0.30f);
            var height = size * (0.30f + phase * 0.30f);
            var middle = root + Vector3.Up * height * 0.65f + outward * size * 0.09f;
            var tip = root + Vector3.Up * height + outward * size * (0.16f + phase * 0.14f);
            var points = new[] { root - across * size * 0.008f, root + across * size * 0.008f,
                middle - across * size * 0.004f, middle + across * size * 0.004f, tip };
            foreach (var vertex in new[] { 0, 1, 2, 1, 3, 2, 2, 3, 4, 2, 1, 0, 2, 3, 1, 4, 3, 2 })
            {
                surface.SetUV(new Vector2(points[vertex].X, points[vertex].Y));
                surface.AddVertex(points[vertex]);
            }
        }
        surface.GenerateNormals();
        grass.AddChild(new MeshInstance3D { Name = "BentGrassBlades", Mesh = surface.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("8b816d", "grass") });
    }

    private static void AddVisualStoneCluster(Node3D parent, string name, Vector3 origin, float size, string color, bool organic = false)
    {
        var stones = new Node3D { Name = name, Position = origin };
        stones.SetMeta("visualOnly", true);
        stones.SetMeta("presentationRole", "small field / road stone cluster");
        parent.AddChild(stones);
        var root = stones.GlobalPosition;
        root.Y = AgentBAct1HeightField.CollisionGround(root.X, root.Z);
        stones.GlobalPosition = root;
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
                RotationDegrees = new Vector3(organic ? size * 17f : 0f, size * 37f, size * 11f),
                Mesh = new SphereMesh
                {
                    Radius = 1f,
                    Height = 2f,
                    RadialSegments = organic ? 9 : 7,
                    Rings = organic ? 4 : 3
                },
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

    internal sealed record StandaloneFenceRailRecord(PublicFenceMemberRecord Member,
        int PrefixVertices, int LinkVertices, int FarVertices, int FrontCaps, int RearCaps, Material? OriginalOverride);
    internal sealed record StandaloneFencePostRecord(MeshInstance3D Mesh, Mesh Source,
        Transform3D OriginalTransform, bool OriginalVisible, Material? OriginalOverride, string Action)
    {
        internal CollisionShape3D[] Contacts { get; set; } = Array.Empty<CollisionShape3D>();
    }
    internal sealed record StandaloneFenceJunctionRecord(Node3D Shed, MeshInstance3D Wall,
        MeshInstance3D Foundation, MeshInstance3D Recess, Plane FrontCut, Plane RearCut,
        StandaloneFenceRailRecord[] Rails, StandaloneFencePostRecord[] Posts);
    internal StandaloneFenceJunctionRecord? ZiratShedFenceJunction { get; private set; }

    private void RepairStandaloneZiratFenceJunction()
    {
        // StandaloneAccess02 identified these two old presentation rails. Keep
        // their external geometry and paths, ending them against the actual shed
        // instead of running through its right door recess and occupied volume.
        var shed = FindDescendants<Node3D>(this).Single(n => n.Name == "ZiratVillageEdgeEastShed");
        MeshInstance3D Part(string name) => FindDescendants<MeshInstance3D>(shed).Single(m => m.Name == name);
        var wall = Part("OutbuildingShed_Wall_LOD0");
        var foundation = Part("OutbuildingShed_Foundation_LOD0");
        var recess = Part("OutbuildingShed_Door_Recess_LOD0");
        var front = StandaloneFenceWallPlane(wall, shed.GlobalBasis.Z.Normalized());
        var rear = StandaloneFenceWallPlane(wall, -shed.GlobalBasis.Z.Normalized());
        // A small timber joint lies inside the real support. This is geometry,
        // not a query margin; the physical capsule and all collision policy stay unchanged.
        front = new Plane(front.Normal, front.D - .003f);
        rear = new Plane(rear.Normal, rear.D - .003f);
        var recessPoints = StandaloneFenceWorldVertices(recess);
        var wallPoints = StandaloneFenceWorldVertices(wall);
        var attachmentZ = (recessPoints.Max(p => p.Z) + wallPoints.Max(p => p.Z)) * .5f;
        var railRoot = GetNode<MeshInstance3D>("Act1CoreWorldGreybox/ConnectiveStreetReturn/ReturnEastParcelFenceRail");
        var fenceParent = railRoot.GetParent<Node3D>();
        var postPositions = Enumerable.Range(0, 5).Select(i =>
            fenceParent.GetNode<MeshInstance3D>("ReturnEastParcelFencePost" + i).GlobalPosition).ToArray();
        var rails = new List<StandaloneFenceRailRecord>();
        foreach (var mesh in new[] { railRoot, railRoot.GetNode<MeshInstance3D>("ReturnEastParcelFenceLowerRail") })
        {
            var source = mesh.Mesh as ArrayMesh ?? throw new InvalidOperationException("Missing measured Zirat fence rail.");
            GD.Print($"act1-zirat-fence-source: owner={mesh.GetPath()} surfaces={source.GetSurfaceCount()}");
            if (source.GetSurfaceCount() != 1) throw new InvalidOperationException("The measured Zirat rail must retain its single surface.");
            var arrays = source.SurfaceGetArrays(0);
            var rawPoints = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normalValue = arrays[(int)Mesh.ArrayType.Normal];
            var rawNormals = normalValue.VariantType == Variant.Type.Nil ? Array.Empty<Vector3>() : normalValue.AsVector3Array();
            var rawUv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            var tangentValue = arrays[(int)Mesh.ArrayType.Tangent];
            var rawTangents = tangentValue.VariantType == Variant.Type.Nil ? Array.Empty<float>() : tangentValue.AsFloat32Array();
            var indexValue = arrays[(int)Mesh.ArrayType.Index];
            var indices = indexValue.VariantType == Variant.Type.Nil ? Array.Empty<int>() : indexValue.AsInt32Array();
            using var imported = ImporterMesh.FromMesh(source);
            var format = (ulong)source.SurfaceGetFormat(0);
            var indexed = (format & (1UL << (int)Mesh.ArrayType.Index)) != 0;
            var expandedCount = indexed ? indices.Length : rawPoints.Length;
            var failures = new List<string>();
            if (expandedCount != 144) failures.Add("expanded-triangle-stream-count");
            if (rawNormals.Length != rawPoints.Length) failures.Add("normal-count");
            if (rawUv.Length != rawPoints.Length || rawUv.Any(uv => !uv.IsFinite())) failures.Add("uv-count-or-finite");
            if (rawTangents.Length != rawPoints.Length * 4) failures.Add("tangent-count");
            if (indexed != (indices.Length > 0) || indices.Any(i => i < 0 || i >= rawPoints.Length)) failures.Add("index-buffer");
            if (imported.GetSurfaceLodCount(0) != 0) failures.Add("unexpected-LOD");
            if (source.ShadowMesh is not null) failures.Add("unexpected-shadow-mesh");
            if (source.SurfaceGetPrimitiveType(0) != Mesh.PrimitiveType.Triangles) failures.Add("primitive");
            // The producer now publishes metre UVs; require and preserve them
            // through the existing clipping path, alongside normals/tangents.
            var channels = (1UL << (int)Mesh.ArrayType.Vertex) | (1UL << (int)Mesh.ArrayType.Normal) | (1UL << (int)Mesh.ArrayType.Tangent)
                | (1UL << (int)Mesh.ArrayType.TexUV)
                | (indexed ? 1UL << (int)Mesh.ArrayType.Index : 0UL);
            if ((format & ((1UL << (int)Mesh.ArrayType.Max) - 1)) != channels) failures.Add("unexpected-attribute-channel");
            GD.Print($"act1-zirat-fence-source: owner={mesh.GetPath()} rawVertices={rawPoints.Length} normals={rawNormals.Length} tangents={rawTangents.Length} indices={indices.Length} expandedVertices={expandedCount} format={format} indexed={indexed} primitive={source.SurfaceGetPrimitiveType(0)} lods={imported.GetSurfaceLodCount(0)} shadow={source.ShadowMesh is not null} failedTerms={string.Join(',', failures)}");
            if (failures.Count != 0) throw new InvalidOperationException("Unsupported measured Zirat rail source: " + string.Join(',', failures));
            var points = indexed ? indices.Select(i => rawPoints[i]).ToArray() : rawPoints;
            var normals = indexed ? indices.Select(i => rawNormals[i]).ToArray() : rawNormals;
            var uvs = indexed ? indices.Select(i => rawUv[i]).ToArray() : rawUv;
            var tangents = Enumerable.Range(0, rawPoints.Length).Select(i => new Plane(
                new Vector3(rawTangents[i * 4], rawTangents[i * 4 + 1], rawTangents[i * 4 + 2]), rawTangents[i * 4 + 3])).ToArray();
            var expandedTangents = indexed ? indices.Select(i => tangents[i]).ToArray() : tangents;
            FenceCutVertex Vertex(Vector3 point, Vector3 normal, Vector2 uv)
            {
                var tangent = (Math.Abs(normal.Y) < .9f ? Vector3.Up : Vector3.Right).Cross(normal).Normalized();
                return new(point, normal, uv, new Plane(tangent, 1), Colors.White, Vector2.Zero);
            }
            var original = points.Select((p, i) => new FenceCutVertex(p, normals[i], uvs[i],
                expandedTangents[i], Colors.White, Vector2.Zero)).ToArray();
            // Decode any actual index buffer, then validate the triangle stream
            // against the posts before preserving its first two spans.
            const int prefix = 72;
            var toWorld = mesh.GlobalTransform;
            var toLocal = toWorld.AffineInverse();
            var spanCorners = StandaloneFenceSpanCorners(points, toWorld, postPositions);
            var startCorners = spanCorners[2].Take(4).ToArray();
            var a = startCorners.Aggregate(Vector3.Zero, (sum, p) => sum + p) * .25f;
            var b = spanCorners[2].Skip(4).Aggregate(Vector3.Zero, (sum, p) => sum + p) * .25f;
            var slope = (b.Y - a.Y) / (b.X - a.X);
            var x = (front.D - front.Normal.Y * (a.Y - slope * a.X) - front.Normal.Z * attachmentZ)
                / (front.Normal.X + front.Normal.Y * slope);
            var attachment = new Vector3(x, a.Y + slope * (x - a.X), attachmentZ);
            var direction = attachment - a;
            var end = attachment + direction.Normalized() * .20f;
            var halfWidth = startCorners[0].DistanceTo(startCorners[1]) * .5f;
            var halfHeight = startCorners[1].DistanceTo(startCorners[2]) * .5f;
            var across = new Vector3(direction.Z, 0, -direction.X).Normalized() * halfWidth;
            var up = Vector3.Up * halfHeight;
            var corners = new[] { toLocal * startCorners[0], toLocal * startCorners[1], toLocal * startCorners[2], toLocal * startCorners[3],
                toLocal * (end - across - up), toLocal * (end + across - up),
                toLocal * (end + across + up), toLocal * (end - across + up) };
            var order = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6,
                0, 5, 1, 0, 4, 5, 3, 6, 7, 3, 2, 6, 1, 6, 2, 1, 5, 6, 0, 7, 4, 0, 3, 7 };
            var link = new List<FenceCutVertex>();
            for (var i = 0; i < order.Length; i += 3)
            {
                var p = corners[order[i]]; var q = corners[order[i + 1]]; var r = corners[order[i + 2]];
                var normal = -(q - p).Cross(r - p).Normalized();
                for (var corner = 0; corner < 3; corner++)
                {
                    var index = i + corner;
                    var uv = original[prefix + index].Uv;
                    if (index >= 12)
                        uv.Y = original[prefix + 12].Uv.Y + (order[index] < 4 ? 0f : (toLocal * end).DistanceTo(toLocal * a));
                    link.Add(Vertex(corners[order[index]], normal, uv));
                }
            }
            var (near, frontCaps) = ClipCouncilFenceRail(link.ToArray(), Enumerable.Range(0, link.Count).ToArray(),
                StandaloneFenceCutCoordinates(front) * toWorld, 0);
            var (far, rearCaps) = ClipCouncilFenceRail(original, Enumerable.Range(0, original.Length).ToArray(),
                StandaloneFenceCutCoordinates(rear) * toWorld, 0);
            var output = original.Take(prefix).Concat(near).Concat(far).ToArray();
            var published = new global::Godot.Collections.Array(); published.Resize((int)Mesh.ArrayType.Max);
            published[(int)Mesh.ArrayType.Vertex] = output.Select(v => v.Point).ToArray();
            published[(int)Mesh.ArrayType.Normal] = output.Select(v => v.Normal).ToArray();
            published[(int)Mesh.ArrayType.TexUV] = output.Select(v => v.Uv).ToArray();
            published[(int)Mesh.ArrayType.Tangent] = output.SelectMany(v =>
                new[] { v.Tangent.Normal.X, v.Tangent.Normal.Y, v.Tangent.Normal.Z, v.Tangent.D }).ToArray();
            if (indexed) published[(int)Mesh.ArrayType.Index] = Enumerable.Range(0, output.Length).ToArray();
            var result = new ArrayMesh(); result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, published);
            result.SurfaceSetMaterial(0, source.SurfaceGetMaterial(0));
            result.SurfaceSetName(0, source.SurfaceGetName(0));
            mesh.Mesh = result;
            mesh.SetMeta("standaloneFenceJunction", "short wall joint after original second post; actual shed interior removed; external far end retained");
            var record = new PublicFenceMemberRecord(mesh, source, mesh.GlobalTransform, mesh.Visible,
                "rail-ended-in-shed-wall", points.Length / 3, output.Length / 3, frontCaps + rearCaps);
            rails.Add(new(record, prefix, near.Count, far.Count, frontCaps, rearCaps, mesh.MaterialOverride));
        }
        var parent = railRoot.GetParent<Node3D>();
        var posts = Enumerable.Range(0, 5).Select(i =>
        {
            var post = parent.GetNode<MeshInstance3D>("ReturnEastParcelFencePost" + i);
            var source = post.Mesh ?? throw new InvalidOperationException("Missing original Zirat fence post mesh.");
            GD.Print($"act1-zirat-fence-post-source: owner={post.GetPath()} meshType={source.GetClass()} surfaces={source.GetSurfaceCount()}");
            var record = new StandaloneFencePostRecord(post, source, post.GlobalTransform, post.Visible, post.MaterialOverride,
                i == 3 ? "inside-member-hidden" : "retained");
            if (i == 3) { post.Visible = false; post.SetMeta("retirementReason", "StandaloneAccess02: this post is inside the existing Zirat shed footprint"); }
            return record;
        }).ToArray();
        ZiratShedFenceJunction = new(shed, wall, foundation, recess, front, rear, rails.ToArray(), posts);
        GD.Print($"act1-zirat-shed-fence-junction: wall={wall.GetPath()} attachmentZ={attachmentZ} retainedFirstSpans=2 hiddenPost=3 retainedFarPost=4 frontCut={front} rearCut={rear}");
    }

    private static Vector3[][] StandaloneFenceSpanCorners(Vector3[] stream, Transform3D toWorld, Vector3[] posts)
    {
        var spans = new Vector3[4][];
        for (var span = 0; span < spans.Length; span++)
        {
            var faces = stream.Skip(span * 36).Take(36).Select(p => toWorld * p).ToArray();
            var corners = faces.Distinct().ToArray();
            var delta = posts[span + 1] - posts[span]; delta.Y = 0;
            var length = delta.Length(); var along = delta / length;
            var across = new Vector3(along.Z, 0, -along.X);
            var edges = new Dictionary<(Vector3, Vector3), int>();
            for (var i = 0; i < faces.Length; i += 3)
                for (var e = 0; e < 3; e++)
                {
                    var left = faces[i + e]; var right = faces[i + (e + 1) % 3];
                    var key = (left, right); var reverse = (right, left);
                    if (edges.ContainsKey(reverse)) key = reverse;
                    edges[key] = edges.GetValueOrDefault(key) + 1;
                }
            Vector3[] Section(float distance)
            {
                var section = corners.Where(p => Math.Abs((p - posts[span]).Dot(along) - distance) < .00002f).ToArray();
                if (section.Length != 4) throw new InvalidOperationException($"Zirat span {span} has no complete section at its actual post.");
                var lower = section.OrderBy(p => p.Y).Take(2).OrderBy(p => p.Dot(across)).ToArray();
                var upper = section.OrderByDescending(p => p.Y).Take(2).OrderByDescending(p => p.Dot(across)).ToArray();
                return lower.Concat(upper).ToArray();
            }
            if (corners.Length != 8 || edges.Count != 18 || edges.Values.Any(count => count != 2)
                || corners.Any(p => (p - posts[span]).Dot(along) < -.00002f || (p - posts[span]).Dot(along) > length + .00002f))
                throw new InvalidOperationException($"Zirat source triangle span {span} no longer matches the closed member between its actual posts.");
            spans[span] = Section(0).Concat(Section(length)).ToArray();
        }
        return spans;
    }

    private static Vector3[] StandaloneFenceWorldVertices(MeshInstance3D mesh)
        => Enumerable.Range(0, mesh.Mesh.GetSurfaceCount()).SelectMany(i =>
            mesh.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).Select(p => mesh.GlobalTransform * p).ToArray();

    private static Plane StandaloneFenceWallPlane(MeshInstance3D wall, Vector3 outward)
    {
        var points = Enumerable.Range(0, wall.Mesh.GetSurfaceCount()).SelectMany(surface =>
        {
            var arrays = wall.Mesh.SurfaceGetArrays(surface);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var value = arrays[(int)Mesh.ArrayType.Index];
            var indices = value.VariantType == Variant.Type.Nil ? Array.Empty<int>() : value.AsInt32Array();
            return (indices.Length == 0 ? vertices : indices.Select(i => vertices[i])).Select(p => wall.GlobalTransform * p);
        }).ToArray();
        var center = points.Aggregate(Vector3.Zero, (sum, p) => sum + p) / points.Length;
        var candidates = new List<Plane>();
        for (var i = 0; i < points.Length; i += 3)
        {
            var a = points[i]; var normal = (points[i + 1] - a).Cross(points[i + 2] - a).Normalized();
            if (normal.Dot(center - a) > 0) normal = -normal;
            if (normal.Dot(outward) > .99f) candidates.Add(new Plane(normal, normal.Dot(a)));
        }
        if (candidates.Count != 2 || candidates[0].Normal.DistanceTo(candidates[1].Normal) > .0001f
            || Math.Abs(candidates[0].D - candidates[1].D) > .0001f)
            throw new InvalidOperationException("The measured Zirat wall no longer has its complete planar support face.");
        return candidates[0];
    }

    private static Transform3D StandaloneFenceCutCoordinates(Plane outside)
    {
        var normal = -outside.Normal;
        var tangent = Vector3.Up.Cross(normal).Normalized();
        var up = normal.Cross(tangent).Normalized();
        return new Transform3D(new Basis(tangent, up, normal).Transposed(), new Vector3(0, 0, outside.D));
    }

    private void FinalizeStandaloneZiratFenceContacts()
    {
        var repair = ZiratShedFenceJunction ?? throw new InvalidOperationException("Missing Zirat fence projection.");
        var contacts = FindDescendants<CollisionShape3D>(this).Where(s => s.HasMeta("authoredSourceMesh")).ToArray();
        // This exact low foundation is a visible support for the new rail ends.
        // The general flat-slab policy intentionally does not own its triangles.
        var foundationPath = repair.Foundation.GetPath().ToString();
        if (contacts.Any(c => c.GetMeta("authoredSourceMesh").AsString() == foundationPath))
            throw new InvalidOperationException("The Zirat foundation already has a physical owner; refusing a duplicate.");
        var proxy = repair.Shed.GetNode<StaticBody3D>("AuthoredKitCollisionProxy");
        var foundationContact = AuthoredSurfaceContact(proxy, repair.Foundation);
        foundationContact.SetMeta("standaloneFenceFoundation", true);
        proxy.AddChild(foundationContact);
        repair.Shed.SetMeta("authoredKitBlockerCount", repair.Shed.GetMeta("authoredKitBlockerCount").AsInt32() + 1);
        GD.Print($"act1-zirat-foundation-contact: owner={foundationContact.GetPath()} source={foundationPath} policy=exact-existing-mesh-triangles");
        foreach (var post in repair.Posts)
        {
            post.Contacts = contacts.Where(c => c.GetMeta("authoredSourceMesh").AsString() == post.Mesh.GetPath().ToString()).ToArray();
            if (post.Action == "inside-member-hidden" && post.Contacts.Length != 0)
                throw new InvalidOperationException("A hidden Zirat fence post still owns collision.");
        }
        foreach (var member in repair.Rails.Select(r => r.Member))
        {
            member.Contacts = contacts.Where(c => c.GetMeta("authoredSourceMesh").AsString() == member.Mesh.GetPath().ToString()).ToArray();
            foreach (var contact in member.Contacts)
            {
                var owner = contact.GetParent<Node3D>();
                var shape = new ConcavePolygonShape3D { BackfaceCollision = true };
                shape.SetFaces(member.Mesh.Mesh.GetFaces().Select(p => member.Mesh.GlobalBasis * p).ToArray());
                contact.Transform = owner.GlobalTransform.AffineInverse() * new Transform3D(Basis.Identity, member.Mesh.GlobalPosition);
                contact.Shape = shape;
            }
        }
    }

    private static void AddVisualFenceRun(Node3D parent, string name, Vector3 start, Vector3 end, bool picketInfill = false)
    {
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length <= 0.05f)
        {
            return;
        }

        var yaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
        Vector3 Grounded(Vector3 point)
        {
            var world = parent.ToGlobal(point);
            world.Y = AgentBAct1HeightField.CollisionGround(world.X, world.Z) - .04f;
            return parent.ToLocal(world);
        }
        var posts = Math.Clamp((int)(length / 2.8f), 2, 8);
        var across = new Vector3(direction.Z, 0f, -direction.X).Normalized() * 0.06f;
        foreach (var (suffix, railHeight, color) in new[]
                 { ("Rail", 0.72f, "979a92"), ("LowerRail", 0.30f, "888c84") })
        {
            // One mesh keeps existing presentation-suppression names intact;
            // its spans follow the same sampled ground as the posts.
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            var along = 0f;
            for (var span = 0; span < posts; span++)
            {
                var a = Grounded(start.Lerp(end, span / (float)posts)) + Vector3.Up * railHeight;
                var b = Grounded(start.Lerp(end, (span + 1f) / posts)) + Vector3.Up * railHeight;
                var up = Vector3.Up * 0.05f;
                var corners = new[] { a - across - up, a + across - up, a + across + up, a - across + up,
                    b - across - up, b + across - up, b + across + up, b - across + up };
                var spanLength = a.DistanceTo(b);
                // Keep the exact faces/winding. Side UV V follows each sloped
                // span in metres; U crosses its thickness, not the world axes.
                var indices = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6,
                    0, 5, 1, 0, 4, 5, 3, 6, 7, 3, 2, 6, 1, 6, 2, 1, 5, 6, 0, 7, 4, 0, 3, 7 };
                for (var index = 0; index < indices.Length; index++)
                {
                    var vertex = indices[index];
                    var offset = corners[vertex] - (vertex < 4 ? a : b);
                    var cross = offset.Dot(across.Normalized()) + .06f;
                    var height = offset.Y + .05f;
                    surface.SetUV(index < 12
                        ? new Vector2(cross, height)
                        : new Vector2(index < 24 ? cross : height, along + (vertex < 4 ? 0f : spanLength)));
                    surface.AddVertex(corners[vertex]);
                }
                along += spanLength;
            }
            surface.GenerateNormals();
            var rail = new MeshInstance3D { Name = $"{name}{suffix}", Mesh = surface.Commit(),
                MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "wood_fence_uv") };
            rail.SetMeta("visualOnly", true);
            // Replacement passes hide the named Rail subtree. Keep the
            // lower member inside that owner instead of leaving an orphan
            // beam on the ground after the old fence is suppressed.
            if (suffix == "LowerRail")
                parent.GetNode<MeshInstance3D>($"{name}Rail").AddChild(rail);
            else
                parent.AddChild(rail);
        }
        // A continuous slatted boundary makes the neighboring parcel legible
        // from either direction; the existing endpoints retain all openings.
        // Keep field/forest rails open: their long runs include sightline
        // openings that must not become opaque roadside barriers.
        var slats = picketInfill || name.StartsWith("Arrival", StringComparison.Ordinal)
            ? Math.Max(1, (int)(length / 0.24f))
            : 0;
        for (var index = 0; index < slats; index++)
        {
            var point = Grounded(start.Lerp(end, (index + 0.5f) / slats));
            var weathering = VegetationHash(point, 43.7f);
            var slatHeight = Mathf.Lerp(0.78f, 0.98f, weathering);
            AddVisualBox(parent.GetNode<MeshInstance3D>($"{name}Rail"), $"{name}Slat{index}",
                new Vector3(0.075f, slatHeight, 0.16f),
                new Vector3(point.X, point.Y + 0.06f + slatHeight * 0.5f, point.Z),
                weathering < 0.35f ? "9b9d95" : "898d86", "wood_fence_vertical", yaw,
                rollDegrees: Mathf.Lerp(-2.5f, 2.5f, weathering));
        }
        for (var index = 0; index <= posts; index++)
        {
            var point = Grounded(start.Lerp(end, index / (float)posts));
            AddVisualBox(
                parent,
                $"{name}Post{index}",
                new Vector3(0.13f, 1.15f, 0.13f),
                new Vector3(point.X, point.Y + 0.575f, point.Z),
                "93968f",
                "wood_fence_vertical");
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
        // Use the same full-volume architectural source as the near street,
        // not the old modular HouseA shell. Extract only the dwelling, never
        // its preview parcel's overlapping props or boundary. Three variant
        // families rotate by name so one street does not read as cloned
        // facades: Far holdings take the raised-veranda VariantC dwelling,
        // east-side houses the plaster annex VariantB, everything else the
        // timber gable VariantA.
        var variant = name.Contains("FarHolding", StringComparison.Ordinal)
            ? "VillageParcel_VariantC_BanyaYard"
            : name.Contains("East", StringComparison.Ordinal)
                ? "VillageParcel_VariantB_PlasterAnnex"
                : "VillageParcel_VariantA_TimberGable";
        AddAct1AuthoredExteriorParcel(parent,
            new Act1ExteriorParcelComponentPlacement(
                variant + "/" + variant + "_Dwelling", name, anchor,
                yawDegrees, Vector3.One * Mathf.Max(.90f, uniformScale),
                "village-house@" + name));
        var house = parent.GetNode<Node3D>(name);
        house.SetMeta("presentationRole", "connected-world-authored-house-family");
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
        // The marker anchors arrive with the grouping's local Y, which is zero;
        // the zirat field is not flat, so an ungrounded marker floated or sank by
        // up to the local terrain relief. Seat it on the real collision surface
        // through its own global transform, so a tilted or offset parent cannot
        // reintroduce the gap.
        if (marker.IsInsideTree())
        {
            var world = marker.GlobalPosition;
            marker.GlobalPosition = new Vector3(
                world.X,
                (float)AgentBAct1HeightField.CollisionGround(world.X, world.Z) - .02f,
                world.Z);
        }

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
        var support = gate.GlobalPosition;
        support.Y = AgentBAct1HeightField.CollisionGround(support.X, support.Z) - .04f;
        gate.GlobalPosition = support;

        AddVisualBox(gate, "PostLeft", new(0.14f, height + 0.32f, 0.14f), new(-width * 0.5f, (height + 0.32f) * 0.5f, 0f), "594a39", "wood");
        AddVisualBox(gate, "PostRight", new(0.14f, height + 0.18f, 0.14f), new(width * 0.5f, (height + 0.18f) * 0.5f, 0f), "594a39", "wood");
        foreach (var postName in new[] { "PostLeft", "PostRight" })
        {
            var post = gate.GetNode<MeshInstance3D>(postName);
            var point = post.GlobalPosition;
            var halfHeight = ((BoxMesh)post.Mesh).Size.Y * .5f;
            point.Y = AgentBAct1HeightField.CollisionGround(point.X, point.Z) - .04f + halfHeight;
            post.GlobalPosition = point;
        }
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

        var support = shed.GlobalPosition;
        support.Y = AgentBAct1HeightField.CollisionGround(support.X, support.Z) - .04f;
        shed.GlobalPosition = support;
        var wall = AddVisualBox(shed, "Wall", new(3.7f, 2.15f, 2.8f), new(0f, 1.08f, 0f), wallColor, "plaster");
        var arrays = wall.Mesh.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        for (var i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].Y >= 0f) continue;
            var point = wall.ToGlobal(vertices[i]);
            point.Y = AgentBAct1HeightField.CollisionGround(point.X, point.Z) - .04f;
            vertices[i] = wall.ToLocal(point);
        }
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        using var grounded = new ArrayMesh();
        grounded.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        using var normals = new SurfaceTool();
        normals.CreateFrom(grounded, 0);
        normals.GenerateNormals();
        wall.Mesh = normals.Commit();
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
        // Near holdings use the same uneven ground as distant trees. Keeping
        // their old y=0 roots leaves a visible gap above depressed yards.
        var worldAnchor = parent.ToGlobal(origin);
        worldAnchor.Y = AgentBAct1HeightField.CollisionGround(worldAnchor.X, worldAnchor.Z) - .04f;

        // Winter road policy: a tree on the carriageway or its shoulder reads
        // as a mistake. Skip it (presentation only, no gameplay owner).
        var road = AgentBAct1HeightField.RoadInfo(worldAnchor.X, worldAnchor.Z);
        if ((float)(road.Distance - road.HalfWidth) < Mathf.Max(2.2f, height * (style == VegetationStyle.Conifer && worldAnchor.Z <= -86f ? .68f : .43f)))
        {
            return;
        }

        origin = parent.ToLocal(worldAnchor);
        // Winter season lock (decision_log 2026-09-10): conifers are not
        // village trees. Outside the Kara forest side every conifer request
        // resolves to a bare deciduous winter tree.
        var forestSide = worldAnchor.Z <= -86f;
        if (style == VegetationStyle.Conifer && !forestSide)
        {
            style = height >= 5.5f ? VegetationStyle.Birch : VegetationStyle.Broadleaf;
        }

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
        => AddAuthoredWinterTree(parent, name, origin, height,
            VegetationHash(origin, 33.7f) < .5f ? "WinterSpruce_1" : "WinterSpruce_2");

    private static void AddVisualBirch(Node3D parent, string name, Vector3 origin, float height, string foliageColor)
        => AddAuthoredWinterTree(parent, name, origin, height, "WinterBirch_1");

    private static void AddVisualBroadleaf(Node3D parent, string name, Vector3 origin, float height, string foliageColor)
        => AddAuthoredWinterTree(parent, name, origin, height, "WinterLinden_1");

    private static void AddVisualShrub(Node3D parent, string name, Vector3 origin, float size, string foliageColor)
        => AddAuthoredWinterTree(parent, name, origin, size * 1.3f, "WinterBirdCherry_1");

    private static void AddAuthoredWinterTree(Node3D parent, string name, Vector3 origin, float height, string variant)
    {
        var tree = new Node3D { Name = name, Position = origin,
            RotationDegrees = new Vector3(0, VegetationHash(origin, 15.1f) * 360f, 0) };
        parent.AddChild(tree);
        tree.SetMeta("visualOnly", true);
        tree.SetMeta("winterVariant", variant);
        tree.SetMeta("winterHeight", height);
    }

    private static void BindAuthoredWinterTrees(Node3D core)
    {
        var layer = core.GetNode<AgentBAct1ExteriorLayer>("AgentBExteriorWorld");
        var roofs = AgentBAct1ExteriorLayer.BuildingRoofBounds(core);
        foreach (var tree in FindDescendants<Node3D>(core).Where(node => node.HasMeta("winterVariant")).ToArray())
        {
            // Dressing replacements may suppress the empty root before meshes bind.
            if (HasTrueMeta(tree, "connectedWorldHidden"))
            {
                tree.Visible = false;
                continue;
            }
            var root = tree.GlobalPosition;
            root.Y = AgentBAct1HeightField.CollisionGround(root.X, root.Z) - .04f;
            tree.GlobalPosition = root;
            if (AgentBAct1ExteriorLayer.UnderBuildingRoof(root, roofs))
            { tree.Visible = false; tree.SetMeta("roofOverlapSuppressed", true); continue; }
            var variant = tree.GetMeta("winterVariant").AsString();
            var region = root.Z <= -86 ? "kara" : root.Z <= -58 ? "zirat" : "village";
            var source = layer.FoliageMesh(variant, region);
            var scale = tree.GetMeta("winterHeight").AsSingle() / source.GetAabb().Size.Y;
            tree.Scale = Vector3.One * scale;
            var blocksRoad = false;
            for (var surface = 0; surface < source.GetSurfaceCount() && !blocksRoad; surface++)
            foreach (var vertex in source.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
            {
                var point = tree.ToGlobal(vertex);
                if (point.Y > AgentBAct1HeightField.CollisionGround(point.X, point.Z) + 2.5f) continue;
                var road = AgentBAct1HeightField.RoadInfo(point.X, point.Z);
                if (road.Distance - road.HalfWidth >= .1) continue;
                blocksRoad = true; break;
            }
            if (blocksRoad) { tree.Visible = false; tree.SetMeta("roadEnvelopeSuppressed", true); continue; }
            var tiers = new[] { variant, variant.Replace("Winter", "WinterLight", StringComparison.Ordinal), variant.Replace("Winter", "WinterFar", StringComparison.Ordinal) };
            for (var lod = 0; lod < tiers.Length; lod++)
            {
                var mesh = new MeshInstance3D { Name = $"BranchSkeletonLOD{lod}", Mesh = layer.FoliageMesh(tiers[lod], region) };
                tree.AddChild(mesh);
                AgentBAct1ExteriorLayer.ConfigureFoliageRange(mesh, tiers.Length == 1 ? -1 : lod, false);
            }
        }
    }

    private static void ReplaceKitWinterShrubs(Node3D core)
    {
        var layer = core.GetNode<AgentBAct1ExteriorLayer>("AgentBExteriorWorld");
        foreach (var mesh in FindDescendants<MeshInstance3D>(core).Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null).ToArray())
        {
            var names = Enumerable.Range(0, mesh.Mesh.GetSurfaceCount())
                .Select(surface => mesh.Mesh.SurfaceGetMaterial(surface)?.ResourceName ?? "").ToArray();
            if (!names.Any(name => name is "Shrub_BlueGreen" or "ShrubGreen" or "FapShrubGreen" or "FapShrubLight")) continue;
            var bounds = mesh.GlobalTransform * mesh.Mesh.GetAabb();
            var root = bounds.GetCenter();
            root.Y = AgentBAct1HeightField.CollisionGround(root.X, root.Z) - .04f;
            var region = root.Z <= -86 ? "kara" : root.Z <= -58 ? "zirat" : "village";
            var source = layer.FoliageMesh("WinterBirdCherry_1", region);
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++) mesh.SetSurfaceOverrideMaterial(surface, null);
            mesh.MaterialOverride = null;
            mesh.Mesh = source;
            mesh.GlobalTransform = new Transform3D(mesh.GlobalBasis.Orthonormalized()
                .Scaled(Vector3.One * (Mathf.Clamp(bounds.Size.Y, .55f, 1.7f) / source.GetAabb().Size.Y)), root);
            mesh.SetMeta("presentationOnly", true);
            mesh.SetMeta("winterShrubReplacement", true);
            AgentBAct1ExteriorLayer.ConfigureFoliageRange(mesh, 0, true);
            for (var lod = 1; lod <= 2; lod++)
            {
                var child = new MeshInstance3D { Name = $"WinterShrubLOD{lod}",
                    Mesh = layer.FoliageMesh(lod == 1 ? "WinterLightBirdCherry_1" : "WinterFarBirdCherry_1", region) };
                mesh.AddChild(child);
                AgentBAct1ExteriorLayer.ConfigureFoliageRange(child, lod, true);
            }
        }
    }

    private static string DarkerFoliageHex(string hex, float factor)
    {
        var color = Color.FromHtml(hex.Length == 6 ? hex : "48553f");
        return color.Darkened(factor).ToHtml(false);
    }

    private static float VegetationHash(Vector3 origin, float salt)
    {
        var value = Mathf.Sin(origin.X * 12.9898f + origin.Z * 78.233f + salt * 37.719f) * 43758.5453f;
        return value - Mathf.Floor(value);
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
    /// <summary>
    /// Presentation-only repaint of the parcel window surrounds. A Tatar village facade is
    /// identified by its painted nalichnik, while the kit bakes a weathered-wood reveal that
    /// reads as a plain hole in the wall. Only visual meshes are touched: collision, routes,
    /// interactions and runtime state keep their existing owners.
    /// </summary>
    /// <summary>
    /// Removes the one legacy interaction whose visible counterpart is not where
    /// the box is. The whole legacy benchmark zone instance is set invisible,
    /// which hides its board without touching collision, so its sign box stayed
    /// live and gave a dead prompt over empty snow 2.6 m from the connected
    /// world's own readable sign (DiscoveryMainStreetSign, which carries the
    /// reverse-word discovery). The legacy interaction has no effects at all
    /// (labelTextId text/inspect, effects []), and every other target in that
    /// instance is deliberately kept because its connected-world visual stands
    /// where the box is.
    /// </summary>
    private void SuppressLegacySignInteraction()
    {
        using var naming = InvariantNameScope();
        var suppressed = 0;
        foreach (var legacySign in FindDescendants<InteractionTarget>(this)
                     .Where(target => target.InteractionId.EndsWith(":interaction/village-sign", StringComparison.Ordinal))
                     .ToArray())
        {
            HidePresentationNode(legacySign);
            legacySign.SetMeta("legacySignSuppression",
                "board hidden in this composition; the readable sign is DiscoveryMainStreetSign");
            suppressed++;
        }

        SetMeta("legacySignInteractionsSuppressed", suppressed);
        GD.Print($"act1-legacy-sign: suppressed={suppressed}");
    }

    private void DressDeferredPaintedWindowSurrounds()
    {
        using var naming = InvariantNameScope();
        if (FindChild("Act1AuthoredExteriorKitPresentation", true, false) is Node3D presentation)
        {
            DressPaintedWindowSurrounds(presentation);
            BatchPaintedWindowSurrounds(presentation);
        }
    }

    private static void DressPaintedWindowSurrounds(Node3D presentation)
    {
        const string paint = "b8b9b4";
        var material = PainterlyMaterialLibrary.ForColor(paint, "wood_painted_trim");
        var painted = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(presentation))
        {
            var name = mesh.Name.ToString();
            // The hero's teal/ivory slots also stay out of the ivory-only
            // batching pilot, whose membership uses windowSurroundPaint.
            if (name.StartsWith("HeroHouse_", StringComparison.Ordinal)
                || !name.Contains("Window", StringComparison.Ordinal))
            {
                continue;
            }

            var isSurround = name.EndsWith("_Jamb1_LOD0", StringComparison.Ordinal)
                || name.EndsWith("_Jamb-1_LOD0", StringComparison.Ordinal)
                || name.EndsWith("_Rail1_LOD0", StringComparison.Ordinal)
                || name.EndsWith("_Rail-1_LOD0", StringComparison.Ordinal);
            if (!isSurround)
            {
                continue;
            }

            mesh.MaterialOverride = material;
            mesh.SetMeta("windowSurroundPaint", paint);
            mesh.SetMeta("presentationOwnership", "presentation-only");
            painted++;
        }

        presentation.SetMeta("paintedWindowSurroundCount", painted);
        GD.Print($"act1-window-surrounds: painted={painted}");
    }

}
