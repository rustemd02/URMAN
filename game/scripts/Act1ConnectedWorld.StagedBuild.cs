using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    public bool DeferBuildOnReady { get; set; }

    private bool _homeFirstStarted;
    private bool _homeFirstReady;
    private readonly Dictionary<CollisionObject3D, (uint Layer, uint Mask)> _homeFirstCollisionSnapshot = new();
    private readonly Dictionary<InteractionTarget, bool> _homeFirstInteractionPresentationSnapshot = new();
    private readonly HashSet<Node> _homeFirstCollisionWatch = new();

    internal bool HomeFirstReady => _homeFirstReady;

    internal IEnumerable<Action> BuildHomeFirstJobs()
    {
        if (_homeFirstStarted) yield break;
        _homeFirstStarted = true;
        yield return () =>
        {
            WatchHomeFirstSubtree(this);
            PersistentInstanceIdentity = GetInstanceId().ToString(CultureInfo.InvariantCulture);
            SetMeta("worldOwner", nameof(Act1ConnectedWorld));
            SetMeta("persistentInstanceIdentity", PersistentInstanceIdentity);
            SetMeta("persistentPolicy", "built-once; Main.SwitchZone never frees this node");
            SetMeta("visualOnlyPolicy", VisualOnlyPolicy);
            SetMeta("worldSpacePlacementPolicy", "fixed deterministic child origins; connected root remains stable across logical transitions");
        };
        if (!Act1WorldLayout.TryGetPlacement("house_old_pc", out var home))
            throw new InvalidOperationException("Connected Act I layout has no house_old_pc placement.");
        foreach (var job in BuildLogicalPlacementJobs(home)) yield return job;
        yield return () =>
        {
            var zone = _zoneInstances["house_old_pc"];
            _interactionsByZone.Add("house_old_pc", FindDescendants<InteractionTarget>(zone)
                .Where(target => !target.HasMeta("connectedWorldHidden"))
                .Select(target => new InteractionBinding(target, target.ActiveCollisionLayer,
                    _homeFirstCollisionSnapshot.TryGetValue(target, out var collision)
                        ? collision.Mask : target.CollisionMask))
                .ToArray());
            _homeFirstReady = true;
        };
    }

    internal void QuarantineHomeFirstBuild()
    {
        if (!_homeFirstStarted || _homeFirstReady
            || !_environmentsByZone.TryGetValue("house_old_pc", out var environments)) return;
        foreach (var binding in environments) binding.Node.Environment = null;
    }

    internal void PublishHomeFirstBuild()
    {
        if (!_homeFirstReady) throw new InvalidOperationException("Cannot publish a home that has not finished building.");
        foreach (var binding in _environmentsByZone["house_old_pc"]) binding.Node.Environment = binding.Resource;
        foreach (var (target, presentationEnabled) in _homeFirstInteractionPresentationSnapshot)
            if (GodotObject.IsInstanceValid(target)) target.SetPresentationEnabled(presentationEnabled);
        foreach (var (collision, layers) in _homeFirstCollisionSnapshot)
        {
            if (!GodotObject.IsInstanceValid(collision)) continue;
            if (collision is not InteractionTarget) collision.CollisionLayer = layers.Layer;
            collision.CollisionMask = layers.Mask;
        }
        _homeFirstCollisionSnapshot.Clear();
        _homeFirstInteractionPresentationSnapshot.Clear();
        foreach (var node in _homeFirstCollisionWatch)
            if (GodotObject.IsInstanceValid(node)) node.ChildEnteredTree -= OnHomeFirstChildEnteredTree;
        _homeFirstCollisionWatch.Clear();
    }

    internal void KeepHomeEnvironmentActiveDuringRestore()
    {
        if (!_homeFirstReady || !_environmentsByZone.TryGetValue("house_old_pc", out var homeBindings)) return;
        foreach (var (zoneId, bindings) in _environmentsByZone)
            foreach (var binding in bindings)
                if (GodotObject.IsInstanceValid(binding.Node))
                    binding.Node.Environment = zoneId == "house_old_pc" ? binding.Resource : null;
        foreach (var binding in homeBindings) binding.Node.Environment = binding.Resource;
        if (GetNodeOrNull<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld") is { ExteriorEnvironmentActive: true } exterior)
            exterior.SetExteriorPresentationEnabled(false);
    }

    internal void SetHomeFirstEnvironmentEnabled(bool enabled)
    {
        if (!_environmentsByZone.TryGetValue("house_old_pc", out var homeBindings)) return;
        foreach (var binding in homeBindings)
            if (GodotObject.IsInstanceValid(binding.Node))
                binding.Node.Environment = enabled ? binding.Resource : null;
    }

    private void WatchHomeFirstSubtree(Node node)
    {
        if (!_homeFirstCollisionWatch.Add(node)) return;
        node.ChildEnteredTree += OnHomeFirstChildEnteredTree;
        if (node is CollisionObject3D collision)
        {
            _homeFirstCollisionSnapshot.TryAdd(collision, (collision.CollisionLayer, collision.CollisionMask));
            if (collision is InteractionTarget target)
            {
                _homeFirstInteractionPresentationSnapshot.TryAdd(target, target.PresentationEnabled);
                target.SetPresentationEnabled(false);
            }
            collision.CollisionLayer = 0;
            collision.CollisionMask = 0;
        }
        foreach (var child in node.GetChildren()) WatchHomeFirstSubtree(child);
    }

    private void OnHomeFirstChildEnteredTree(Node child) => WatchHomeFirstSubtree(child);

    internal void SetActiveHomeFirstZone()
    {
        if (!_homeFirstReady || !_zoneInstances.TryGetValue("house_old_pc", out var home))
            throw new InvalidOperationException("The staged home is not ready to become active.");
        foreach (var (zoneId, bindings) in _environmentsByZone)
            foreach (var binding in bindings)
                binding.Node.Environment = zoneId == "house_old_pc" ? binding.Resource : null;
        ActiveDirectionalLightCount = 0;
        ActiveLightCount = 0;
        foreach (var (zoneId, lights) in _lightsByZone)
        foreach (var light in lights)
        {
            var enabled = zoneId == "house_old_pc";
            light.Visible = enabled;
            if (!enabled) continue;
            ActiveLightCount++;
            if (light is DirectionalLight3D) ActiveDirectionalLightCount++;
        }
        ActiveInteractionTargetCount = 0;
        foreach (var (zoneId, bindings) in _interactionsByZone)
        foreach (var binding in bindings)
        {
            var enabled = zoneId == "house_old_pc" && binding.Node.IsSemanticallyAvailable();
            binding.Node.CollisionLayer = enabled ? binding.CollisionLayer : 0;
            binding.Node.CollisionMask = enabled ? binding.CollisionMask : 0;
            if (enabled) ActiveInteractionTargetCount++;
        }
        home.Visible = true;
        ActiveZoneId = "house_old_pc";
        SetMeta("activeZoneId", ActiveZoneId);
        SetMeta("activeWorldEnvironmentCount", _environmentsByZone[ActiveZoneId].Count);
        SetMeta("activeExteriorAtmosphere", false);
        SetMeta("activeAtmosphereOwner", $"logical-zone/{ActiveZoneId}");
        SetMeta("activeDirectionalLightCount", ActiveDirectionalLightCount);
        SetMeta("activeLightCount", ActiveLightCount);
        SetMeta("activeInteractionTargetCount", ActiveInteractionTargetCount);
    }

    internal IEnumerable<Action> BuildJobs()
    {
        if (_buildStarted) yield break;
        _buildStarted = true;
        yield return () =>
        {
            PersistentInstanceIdentity = GetInstanceId().ToString(CultureInfo.InvariantCulture);
            SetMeta("worldOwner", nameof(Act1ConnectedWorld));
            SetMeta("persistentInstanceIdentity", PersistentInstanceIdentity);
            SetMeta("persistentPolicy", "built-once; Main.SwitchZone never frees this node");
            SetMeta("visualOnlyPolicy", VisualOnlyPolicy);
            SetMeta("worldSpacePlacementPolicy", "fixed deterministic child origins; connected root remains stable across logical transitions");
        };
        foreach (var placement in Act1WorldLayout.Placements)
        {
            if (_homeFirstReady && placement.ZoneId == "house_old_pc") continue;
            foreach (var job in BuildLogicalPlacementJobs(placement)) yield return job;
        }
        yield return () => BuildConnectorPresentation();
        yield return () =>
        {
            // Keep the legacy framing builders available as rollback/source code,
            // but do not materialize their global presentation root in the main
            // connected-world path. Act1CoreWorldGreybox is the sole global visual
            // owner; route, collision, interaction and RuntimeBridge owners above
            // remain unchanged.
            _dwellingThresholdPlacements = 0;
        };
        yield return () => _dwellingThresholdWorst = 0f;
        yield return () => _dwellingThresholdWorstPlacement = string.Empty;
        foreach (var job in ProfileAuthoredBuildJobs("core", BuildCoreWorldJobs())) yield return job;
        yield return () => AlignFapClinicArchitecture();
        yield return () =>
        {
            GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld")
            .ExcludeOccupiedRoomTerrain(_zoneInstances["house_old_pc"], new Vector2(
                StyleBenchmarkInteriorFactory.ClearWidth * .5f,
                StyleBenchmarkInteriorFactory.ClearDepth * .5f), fitBabaiYard: true);
        };
        yield return () =>
        {
            GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld")
            .RegisterWeatherShelter(_zoneInstances["house_old_pc"],
                new Vector3(BabaiHousePlan.CentreX, 3.70f, BabaiHousePlan.CentreZ),
                new Vector3(BabaiHousePlan.Width*.5f+.2f, 3.8f, BabaiHousePlan.Depth*.5f+.2f), windowView: true);
        };
        yield return () => BuildAct1NpcStaging();
        yield return () => BuildAct1InteriorDiscoveries();
        yield return () => BuildAct1ExteriorDiscoveries();
        yield return () => BuildArrivalBusStop();
        yield return () => BuildFapServiceExploration();
        yield return () => BuildRoadsideDiscoveries();
        yield return () => BuildAct1YardDiscoveries();
        yield return () => BuildAct1LanternDetails();
        yield return () => BuildAct1QuietCareDiscoveries();
        yield return () => BuildAct1OptionalDiscoveries();
        yield return () => BuildAct1CulvertVerandaDiscoveries();
        yield return () => BuildBabaiYardSideGateExploration();
        yield return () => BuildCarryables(GetNode<Node3D>("Act1CoreWorldGreybox"));
        yield return () => BuildAct1KaraOptionalDiscoveries();
        yield return () => BuildAct1BypassDiscoveries();
        yield return () => BuildAct1ImageDiscoveries();
        yield return () => BuildRinatRoadsidePresentation();
        yield return () => BuildWatchingFormsPilot(GetNode<Node3D>("Act1CoreWorldGreybox"));
        yield return () => BuildSovkhozSquare(GetNode<Node3D>("Act1CoreWorldGreybox"));
        yield return () => BuildRearYardGate(GetNode<Node3D>("Act1CoreWorldGreybox"));
        yield return () => ConfigureInvestigationRevisits();
        yield return () => BuildOpeningSleep();
        yield return () => BuildMosqueInterior();
        yield return () => RestyleVillageMosque();
        yield return () => BuildBathhouse();
        yield return () => BuildPublicBuildings();
        yield return () => BuildPolicePost();
        yield return () => BuildFapPlate();
        yield return () => BuildPlateFaces();
        yield return () => RepairStandaloneZiratFenceJunction();
        yield return () => HideOverlappingStructures();
        yield return () => RelocateOverlappingHouses();
        yield return () => ComposeBabaiYard();
        foreach (var job in ProfileAuthoredBuildJobs("authored", BuildAuthoredWorldJobs())) yield return job;
        yield return () => ProfileWorldBuildStep("clear-open", ClearOpenPartOfLegacyPresentation);
        foreach (var job in ProfileAuthoredBuildJobs("relocate-babai", RelocateBabaiHouseholdJobs())) yield return job;
        yield return () =>
        {
            // Relocation rebuilds the base field to fill the old house footprint.
            // Publish the other occupied cuts against that final terrain.
            ExcludePolicePostTerrain();
        };
        yield return () => PrepareCentralMosqueGround();
        yield return () => ExcludeMosqueHallTerrain();
        yield return () =>
        {
            ((StyleBenchmarkZone)_zoneInstances["village_day"]).FitHouseDoorToLeaf(
            FindDescendants<MeshInstance3D>(GetNode<Node3D>(
                "Act1CoreWorldGreybox/Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade"))
                .Single(mesh => mesh.Name == "HeroHouse_StreetDoorClosed_LOD0"));
        };
        yield return () => BindYardSnagWithHollows(GetNode<Node3D>("Act1CoreWorldGreybox"));
        yield return () => ProfileWorldBuildStep("storage", BuildCozyBabaiStorage);
        yield return () => BuildShopUses();
        yield return () => ProfileWorldBuildStep("clear-gorge", ClearGorgeOfLegacyPresentation);
        yield return () => ProfileWorldBuildStep("addresses", BuildAddressRegistry);
        yield return () =>
        {
            // Street faces of the yards: palisadnik, painted gates, board fences.
            ProfileWorldBuildStep("frontages", BuildStreetFrontages);
        };
        yield return () =>
        {
            // One fence system along the real lot lines replaces every older yard fence.
            ProfileWorldBuildStep("timber-fences", RebuildYardFences);
        };
        yield return () => ComposeCleanVillageYards();
        yield return () =>
        {
            // Snow banks flank the street shoulder; built after the address
            // registry and the final yard fences so every addressed door walk and
            // the gates that actually stand break the shoulder (audit 2026-10-05,
            // F1/F2; VIS-079). Built before the fences, they broke at the retired
            // frontage gates instead.
            ProfileWorldBuildStep("snow-banks", () => AddMainStreetSnowBanks(GetNode<Node3D>("Act1CoreWorldGreybox")));
        };
        yield return () =>
        {
            // Tamara Gennadievna's breakable plot fence, boards and people.
            BuildTamaraFenceQuest();
        };
        yield return () => AddressRead += RememberReadAddress;
        yield return () => BuildAct1Vehicles();
        yield return () =>
        {
            // Parallel lane (docs/production/parallel_lanes_2026-10-01.md): mechanics and
            // mini-games add their world content in Act1ConnectedWorld.MechanicsLane.cs only.
            BuildMechanicsLane();
        };
        yield return () =>
        {
            foreach (var placement in Act1WorldLayout.Placements)
            {
                if (_interactionsByZone.ContainsKey(placement.ZoneId)) continue;
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
        };
        yield return () => AttachRuntimeBridge();
        yield return () =>
        {
            // First photo entries use the final relocated world geometry. Their
            // presentation remains owned by this connected world and its session.
            BuildForestPhotoEntry();
        };
        yield return () => BuildCellarPhotoEntry();
        yield return () => UpdateAct1NpcStaging();
        yield return () => UpdateAct1Discoveries();
        yield return () => ConnectorCount = Act1WorldLayout.Connectors.Count;
        yield return () => SetMeta("logicalZoneInstanceCount", LogicalZoneInstanceCount);
        yield return () => SetMeta("placementCount", PlacementCount);
        yield return () => SetMeta("placementIds", string.Join('|', PlacementIds));
        yield return () => SetMeta("zoneIds", string.Join('|', Act1WorldLayout.ZoneIds));
        yield return () =>
        {
            SetMeta("zoneInstanceIds", string.Join('|', _zoneInstances.Values.Select(
            instance => instance.GetInstanceId().ToString(CultureInfo.InvariantCulture))));
        };
        yield return () => SetMeta("connectorCount", ConnectorCount);
        yield return () => SetMeta("connectorIds", string.Join('|', Act1WorldLayout.Connectors.Select(connector => connector.ConnectorId)));
        yield return () => SetMeta("connectorsVisualOnly", ConnectorsAreVisualOnly);
        yield return () => SetMeta("connectorTraversalSurfacesCollidable", ConnectorTraversalSurfacesAreCollidable);
        yield return () => SetMeta("connectorTraversalCollisionCount", ConnectorTraversalCollisionCount);
        yield return () => SetMeta("activeZoneId", string.Empty);
        yield return () => SetMeta("activeWorldEnvironmentCount", 0);
        yield return () => SetMeta("activeDirectionalLightCount", 0);
        yield return () => SetMeta("activeLightCount", 0);
        yield return () => SetMeta("activeInteractionTargetCount", 0);
        yield return () => SetActiveLogicalZone(ActiveZoneId.Length == 0 ? "village_day" : ActiveZoneId);
        yield return () =>
        {
            // Blockers and the hidden-presentation audit both run after mounting and
            // after the first zone's suppressions, so nothing hidden can block.
            BuildAuthoredKitBlockers();
        };
        yield return () => FinalizeStandaloneZiratFenceContacts();
        yield return () => FinalizeFacilityContacts();
        yield return () => FinalizePublicBuildingContacts();
        yield return () => FinalizeArrivalBusStop();
        yield return () => BuildZiratFamily();
        yield return () =>
        {
            // Keep addressed Variant A source dwellings through every authored
            // contact and collision pass, then graft their opaque PBR presentation.
            GraftPrivateVariantAPbrFamily();
        };
        yield return () => GraftH049OutbuildingPbr();
        yield return () => GraftPrivateVariantBOutbuildingPbrFamily();
        yield return () => GraftPrivateVariantCOutbuildingPbrFamily();
        // These callbacks originally ran deferred after Build set IsBuilt.
        // In particular VillagePa fixtures explicitly require that flag.
        if (DeferBuildOnReady) yield return () => IsBuilt = true;
        yield return () => { if (DeferBuildOnReady) DisableBlockersUnderHiddenPresentation(); else CallDeferred(nameof(DisableBlockersUnderHiddenPresentation)); };
        yield return () => { if (DeferBuildOnReady) ReapplyLogicalZonePresentationSuppressions(); else CallDeferred(nameof(ReapplyLogicalZonePresentationSuppressions)); };
        yield return () => { if (DeferBuildOnReady) DressDeferredPaintedWindowSurrounds(); else CallDeferred(nameof(DressDeferredPaintedWindowSurrounds)); };
        yield return () => { if (DeferBuildOnReady) SuppressLegacySignInteraction(); else CallDeferred(nameof(SuppressLegacySignInteraction)); };
        yield return () => { if (DeferBuildOnReady) ReconcileAddressSignsAfterFrontages(); else CallDeferred(nameof(ReconcileAddressSignsAfterFrontages)); };
        yield return () => { if (DeferBuildOnReady) BuildVillageHouseholds(); else CallDeferred(nameof(BuildVillageHouseholds)); };
        yield return () => RegisterFacilityPrecipitationShelters();
        yield return () => ProfileWorldBuildStep("snow-trample-bind", () =>
            GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld").PrepareSnowTrampleSurfaces(this));
        yield return () => IsBuilt = true;
    }

    internal void ActivateStagedBuild()
    {
        if (!IsBuilt) throw new InvalidOperationException("Cannot publish an incomplete connected world.");
        AddToGroup("act1_connected_world");
        AuthoredWorld?.ActivateAfterBuild();
    }

    private IEnumerable<Action> BuildLogicalPlacementJobs(Act1WorldLayout.ZonePlacement placement)
    {
        PackedScene packed = null!;
        Node3D zone = null!;
        try
        {
        yield return StyleBenchmarkZone.HouseBuildJob(placement.ZoneId + "/scene-load", () =>
        {
        packed = ResourceLoader.Load<PackedScene>(placement.ScenePath)
            ?? throw new InvalidOperationException($"Connected Act I zone is missing: {placement.ScenePath}.");
        });
        yield return StyleBenchmarkZone.HouseBuildJob(placement.ZoneId + "/scene-instantiate", () =>
        {
        zone = packed.Instantiate<Node3D>()
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
        if (zone is StyleBenchmarkZone { ZoneKind: StyleBenchmarkZone.BenchmarkKind.HouseOldPc } house)
            house.DeferHouseBuildOnReady = true;
        });
        yield return StyleBenchmarkZone.HouseBuildJob(placement.ZoneId + "/add-child-ready", () =>
        {
            AddChild(zone);
            // Capture before the scheduler clears pending WorldEnvironments.
            // The resource binding must survive the subsequent house-content jobs.
            var environments = FindDescendants<WorldEnvironment>(zone)
                .Select(environment => new EnvironmentBinding(environment,
                    environment.Environment ?? throw new InvalidOperationException(
                        $"Act I zone has an empty WorldEnvironment: {placement.ZoneId}.")))
                .ToArray();
            if (_homeFirstStarted && placement.ZoneId == "house_old_pc")
                foreach (var environment in environments) environment.Node.Environment = null;
            if (_homeFirstReady && ActiveZoneId == "house_old_pc")
                foreach (var environment in environments) environment.Node.Environment = null;
            _environmentsByZone.Add(placement.ZoneId, environments);
        });
        if (zone is StyleBenchmarkZone { ZoneKind: StyleBenchmarkZone.BenchmarkKind.HouseOldPc } stagedHouse)
            foreach (var job in stagedHouse.BuildHouseOldPcJobs()) yield return job;
        yield return StyleBenchmarkZone.HouseBuildJob(placement.ZoneId + "/register", () =>
        {
        _zoneInstances.Add(placement.ZoneId, zone);

        ApplyLogicalZonePresentationSuppressions(zone, placement.ZoneId);

        if (zone is StyleBenchmarkZone village && placement.ZoneId == "village_day")
            village.MakeInteractionBox("TimurHazratNpc", new(.65f, 1.8f, .5f), new(-3.8f, .9f, -19f),
                "506058", "urman.chapter1:interaction/route-to-mosque", "Поговорить с Тимуром хәзрәтом",
                dialogueId: "urman.chapter1:dialogue/timur_restraint");

        _lightsByZone.Add(
            placement.ZoneId,
            FindDescendants<Light3D>(zone)
                .Where(light => !light.HasMeta("connectedWorldHidden"))
                .ToArray());
        });
        }
        finally
        {
            // Cancellation can occur between Instantiate and AddChild. An
            // unparented scene is not covered by the pending world's QueueFree.
            if (zone is not null && GodotObject.IsInstanceValid(zone) && zone.GetParent() is null)
                zone.Free();
        }
    }

    private IEnumerable<Action> BuildCoreWorldJobs()
    {
        Node3D? importedRoot = null;
        yield return () =>
        {
            var packed = ResourceLoader.Load<PackedScene>(VillageExteriorKitScenePath)
                ?? throw new InvalidOperationException(
                    $"Act I village exterior neighbor parcel kit is missing: {VillageExteriorKitScenePath}.");
            importedRoot = packed.Instantiate<Node3D>()
                ?? throw new InvalidOperationException(
                    $"Act I village exterior neighbor parcel kit did not instantiate: {VillageExteriorKitScenePath}.");
        };
        try
        {
            yield return () =>
            {
                var root = importedRoot ?? throw new InvalidOperationException("The staged village kit was not prepared.");
                var kitRoot = root;
                            if (!string.Equals(root.Name.ToString(), VillageExteriorKitRootName, StringComparison.Ordinal))
                            {
                                kitRoot = root.GetNodeOrNull<Node3D>(VillageExteriorKitRootName)
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

                            _activeVillageExteriorKitRoot = kitRoot;
            };
            var core = new Node3D { Name = "Act1CoreWorldGreybox" };
            yield return () =>
            {
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
            };
            yield return () => BuildCoreArrival(CoreVisualZone(core, "Arrival"));
            yield return () => BuildCoreMainStreet(CoreVisualZone(core, "MainStreet"));
            yield return () => BuildCoreBabaiEbiYard(CoreVisualZone(core, "BabaiEbiYard"));
            yield return () => BuildCoreHouseExterior(CoreVisualZone(core, "HouseExteriorApproach"));
            yield return () => BuildCoreConnectiveAndReturn(CoreVisualZone(core, "ConnectiveStreetReturn"));
            yield return () => BuildCoreFapExterior(CoreVisualZone(core, "FapExterior"));
            yield return () => BuildCoreZirat(CoreVisualZone(core, "ZiratMemoryField"));
            yield return () => BuildCoreKaraForestEdge(CoreVisualZone(core, "KaraForestEdge"));
            // Distant minaret silhouette on the western skyline: identifies the
                    // Tatar village without creating a new zone (presentation-only).
                    // Relayout v3, stage 4: the mosque stands west of Мәйдан, fronting the square.
                    var mosque = AgentBAct1Layout.MosqueAnchor;
            var minaretAnchor = new Vector3(mosque.X, (float)AgentBAct1HeightField.Ground(mosque.X, mosque.Y), mosque.Y);
            yield return () => AddDistantMinaret(core, "DistantMinaretSilhouette", minaretAnchor, 1.0f);
            yield return () =>
            {
                // The author reported the bare minaret reading as a lone tower and asked
                // for a real mosque in this part of the village: the hall, dome, closed
                // entrance and courtyard wall now stand around the existing minaret.
                AddVillageMosque(core, minaretAnchor);
            };
            yield return () =>
            {
                // Relayout v3 (author, 2026-10-01): no river. A deep gorge separates the village
                // from the forest; the old suspension bridge is the only crossing.
                AddVillageGorge(core);
            };
            yield return () =>
            {
                // The ravine east of the FAP splits the village in two; its bridge has
                // lost the middle span (author, 2026-09-25).
                AddVillageRavine(core);
            };
            yield return () =>
            {
                // This is the separate full-game PhotoWorlds destination. Keep it out
                // of the original Chapter I build; in the full-game campaign its
                // physical approach is present in the village before the bank route is
                // entered.
                if (GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge bridge
                    && bridge.IsPhotoWorldsCampaign)
                    AddPhotoBankApproach(core);
            };
            yield return () =>
            {
                // Unreachable background layers (T3): mid woodland bands and far snow
                // ridges, all beyond the walkable envelope. The near village rows that
                // used to stand here are gone: the tall Act I forest ring now closes
                // that ground, and a backdrop house left inside the ring would be the
                // one silhouette that betrays the depth behind the trees.
                AddDistantRidge(core, "BackdropFarRidgeWest",
                    new Vector3(-240f, (float)AgentBAct1HeightField.Ground(-240f, -60f), -60f), 220f, 8f, 90f, 8f);
            };
            yield return () =>
            {
                AddDistantRidge(core, "BackdropFarRidgeEast",
                new Vector3(230f, (float)AgentBAct1HeightField.Ground(230f, -70f), -70f), 210f, 7f, 85f, -6f);
            };
            yield return () =>
            {
                AddDistantRidge(core, "BackdropFarRidgeNorth",
                new Vector3(-40f, (float)AgentBAct1HeightField.Ground(-40f, -260f), -260f), 250f, 10f, 110f, 0f);
            };
            yield return () => AddBackdropGround(core);
            yield return () =>
            {
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
            };
            yield return () => core.SetMeta("genericFacetedMassSuppressionCount", 11);
            yield return () => HideCoreBlockoutBuildingVolumes(core);
            yield return () => BuildAct1WetVillageRoadKit(core);
            yield return () => BuildAct1FapClinicKit(core);
            yield return () => BuildAct1ZiratRoadsideKit(core);
            yield return () => BuildAct1KaraForestEdgeKit(core);
            yield return () => BuildAct1AuthoredExteriorKit(core);
            yield return () => BuildAct1SightlineClosurePass(core);
            foreach (var job in ProfileAuthoredBuildJobs("agent-b-exterior", BuildExteriorLayerJobs(core))) yield return job;
            yield return () =>
            {
                AddDistantForestBand(core, "BackdropMidWoodlandWest",
                new Vector3(-150f, (float)AgentBAct1HeightField.Ground(-150f, -30f), -30f), 34, 8.0f, 87f, 1.15f, false);
            };
            yield return () =>
            {
                AddDistantForestBand(core, "BackdropMidWoodlandEast",
                new Vector3(120f, (float)AgentBAct1HeightField.Ground(120f, -40f), -40f), 30, 8.5f, 94f, 1.1f, false);
            };
            yield return () =>
            {
                AddDistantForestBand(core, "BackdropMidForestNorth",
                new Vector3(-70f, (float)AgentBAct1HeightField.Ground(-70f, -170f), -170f), 30, 9.0f, 2f, 1.25f, true);
            };
            yield return () =>
            {
                AddDistantForestBand(core, "BackdropFarForestNorth",
                new Vector3(-20f, 0f, -350f), 36, 9f, 0f, 1.6f, true);
            };
            foreach (var job in BindAuthoredWinterTreeJobs(core)) yield return job;
            yield return () => ApplyAct1DaylightPresentationPass(core);
            yield return () => ReplaceKitWinterShrubs(core);
            yield return () =>
            {
                if (_studioPreview) BuildVillageLifePreviewPlacements(core);
                else BuildVillageLife(core);
            };
        }
        finally
        {
            _activeVillageExteriorKitRoot = null;
            if (importedRoot is not null && GodotObject.IsInstanceValid(importedRoot)) importedRoot.Free();
        }
    }

    private IEnumerable<Action> BuildExteriorLayerJobs(Node3D core)
    {
        var layer = new AgentBAct1ExteriorLayer { Name = "AgentBExteriorWorld" };
        yield return () => core.AddChild(layer);
        foreach (var job in layer.BuildJobs()) yield return job;
        yield return () => SuppressAgentBOverlappingRoadDecor(layer);
    }
}
