using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Production exterior world layer for Act I: five consolidated Agent B
/// kits + deterministic terrain collider + unified rain/day-night sky.
/// Presentation and traversal ownership only. Interaction targets stay
/// with zone scenes; narrative/save state stays with RuntimeBridge.
/// No navigation nodes; no new InteractionTargets; never gates routes.
/// </summary>
public partial class AgentBAct1ExteriorLayer : Node3D
{
    private static readonly (string KitFile, string RootName)[] Kits =
    {
        ("agentb_terrain_road_kit.glb", "AgentB_TerrainRoadKit"),
        ("agentb_village_buildings_kit.glb", "AgentB_VillageBuildingsKit"),
        ("agentb_foliage_kit.glb", "AgentB_FoliageKit"),
        ("agentb_zirat_kit.glb", "AgentB_ZiratKit"),
        ("agentb_kara_edge_kit.glb", "AgentB_KaraEdgeKit")
    };

    private const string KitDirectory = "res://assets/models/agent_b_act1/";
    private WorldEnvironment? _environment;
    private global::Godot.Environment? _environmentResource;
    private DirectionalLight3D? _sun;
    private CpuParticles3D? _rain;
    private SnowTrampleField? _snowTrample;
    private readonly List<OmniLight3D> _karaAccentLights = new();
    private bool _built;
    private bool _exteriorPresentationEnabled;

    public void Build()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        SetMeta("presentationOwner", nameof(Act1ConnectedWorld));
        SetMeta("variantStatus", "production-canonical");
        SetMeta("retiredVariants", "AgentBAct1World");
        SetMeta("runtimeEntryPoint", "res://scenes/act1_demo.tscn");
        SetMeta("visualOnlyPolicy", "terrain+architecture collidable; decor walk-through");
        SetMeta(
            "collisionPolicy",
            "AgentB_TerrainCollision and AgentB_ArchitectureCollision are the only traversal bodies; all decor remains walk-through");
        SetMeta(
            "traversalCollisionOwners",
            "AgentB_TerrainCollision:act1-exterior-terrain|AgentB_ArchitectureCollision:act1-exterior-architecture");
        SetMeta("navigationPolicy", "none");
        SetMeta("interactionPolicy", "none");
        SetMeta("runtimeStateOwnership", "RuntimeBridge");
        foreach (var (kitFile, rootName) in Kits)
        {
            var instance = AgentBKitMaterials.InstantiateKit(this, KitDirectory + kitFile, rootName);
            instance.SetMeta("exteriorKit", kitFile);

            // The village-building kit is a broad authored context kit and
            // includes a complete FAP family. The connected world has a
            // dedicated clinic kit with the approved facade/porch/yard
            // composition, so keep exactly one exterior FAP presentation
            // owner. Suppress the duplicate before building architecture
            // collision; the local FAP interior and its targets remain
            // separate authoritative owners.
            if (string.Equals(kitFile, "agentb_village_buildings_kit.glb", System.StringComparison.Ordinal))
            {
                SuppressDuplicateFapPresentation(instance);
                SuppressDuplicateBabaiHouse(instance);
                SuppressDuplicateBabaiOutbuildings(instance);
                SuppressRouteOccludingHouseA7(instance);
                SuppressDuplicateNearStreetHouses(instance);
                SuppressRouteOccludingHouseA5Architecture(instance);
                SuppressRouteOccludingFarHouse3(instance);
                SuppressRemainingPreviewArchitecture(instance);
            }
            else if (string.Equals(kitFile, "agentb_kara_edge_kit.glb", System.StringComparison.Ordinal))
            {
                SuppressRouteOccludingKaraMasses(instance);
            }
            else if (string.Equals(kitFile, "agentb_zirat_kit.glb", System.StringComparison.Ordinal))
            {
                SuppressZiratRouteFenceRails(instance);
            }
        }

        BuildTerrainCollision();
        BuildArchitectureCollision();
        PlantFoliage();
        BuildEnvironment();
        BuildKaraAccentLights();
        BuildSnow();
        _snowTrample = new SnowTrampleField { Name = "AgentBSnowTrample" };
        AddChild(_snowTrample);
    }

    /// <summary>
    /// Routes the single global exterior atmosphere owner. Interior logical
    /// zones keep their authored WorldEnvironment; the Agent B exterior
    /// environment and sun must be disabled there so two global presentation
    /// owners never compete for the same frame.
    /// </summary>
    public void SetExteriorPresentationEnabled(bool enabled, bool night = false)
    {
        if (_environment is null)
        {
            return;
        }

        _environment.Environment = enabled ? _environmentResource : null;
        _exteriorPresentationEnabled = enabled;
        if (enabled)
        {
            ApplyAtmosphere(night);
        }

        if (_sun is not null)
        {
            _sun.Visible = enabled;
        }

        if (_rain is not null)
        {
            _rain.Emitting = enabled;
        }
        _snowTrample?.SetEnabled(enabled);

        foreach (var light in _karaAccentLights)
        {
            light.Visible = enabled && night;
        }

        SetMeta("exteriorPresentationEnabled", enabled);
        SetMeta("exteriorMood", night ? "kara-night" : "rainy-day");
        SetMeta("exteriorNight", night);
        SetMeta("activeAtmosphereOwner", enabled ? "AgentBExteriorWorld" : "logical-zone");
    }

    private void BuildTerrainCollision()
    {
        var body = new StaticBody3D { Name = "AgentB_TerrainCollision" };
        body.SetMeta("collisionOwner", "act1-exterior-terrain");
        body.CollisionLayer = 1u;
        body.CollisionMask = 1u;
        AddChild(body);
        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(AgentBAct1HeightField.BuildTerrainFaces());
        body.AddChild(new CollisionShape3D { Name = "AgentB_TerrainFaces", Shape = shape });
    }

    private void BuildArchitectureCollision()
    {
        var body = new StaticBody3D { Name = "AgentB_ArchitectureCollision" };
        body.SetMeta("collisionOwner", "act1-exterior-architecture");
        body.CollisionLayer = 1u;
        body.CollisionMask = 1u;
        AddChild(body);
        foreach (var kitName in new[] { "AgentB_VillageBuildingsKit", "AgentB_ZiratKit", "AgentB_KaraEdgeKit" })
        {
            var kitRoot = GetNodeOrNull<Node3D>(kitName);
            if (kitRoot is null)
            {
                continue;
            }

            foreach (var meshInstance in EnumerateDescendants<MeshInstance3D>(kitRoot))
            {
                if (!ShouldCollide(meshInstance.Name.ToString()) || meshInstance.Mesh is null)
                {
                    continue;
                }

                if (meshInstance.HasMeta("agentBPresentationSuppressed"))
                {
                    continue;
                }

                var trimesh = meshInstance.Mesh.CreateTrimeshShape();
                if (trimesh is null)
                {
                    continue;
                }

                body.AddChild(new CollisionShape3D
                {
                    Name = $"Col_{meshInstance.Name}",
                    Shape = trimesh,
                    Transform = meshInstance.GlobalTransform
                });
            }
        }
    }

    private static void SuppressDuplicateFapPresentation(Node3D villageKit)
    {
        var fapMeshes = EnumerateDescendants<MeshInstance3D>(villageKit)
            .Where(mesh => mesh.Name.ToString().StartsWith("Fap_", System.StringComparison.Ordinal))
            .ToArray();
        if (fapMeshes.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B village-building kit is missing the declared Fap_* family; cannot establish a single FAP visual owner.");
        }

        foreach (var mesh in fapMeshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta(
                "suppressionReason",
                "dedicated Act1ConnectedWorld FapClinicAuthoredKitPresentation owns the exterior FAP");
        }

        villageKit.SetMeta("suppressedPresentationFamilies", "Fap_*");
        villageKit.SetMeta("suppressedFapMeshCount", fapMeshes.Length);
        villageKit.SetMeta(
            "fapPresentationOwner",
            "Act1ConnectedWorld/Act1CoreWorldGreybox/FapExterior/FapClinicAuthoredKitPresentation");
    }

    private static void SuppressDuplicateBabaiHouse(Node3D villageKit)
    {
        // The connected world owns the authored Babai/әби dwelling facade;
        // retaining Agent B's second hero-house body leaves a blank rear wall
        // in the required first-person 360-degree yard review.
        var meshes = EnumerateDescendants<MeshInstance3D>(villageKit)
            .Where(mesh => mesh.Name.ToString().StartsWith("HouseBabai_", System.StringComparison.Ordinal))
            .ToArray();
        if (meshes.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B village-building kit is missing the declared HouseBabai_* family; cannot establish one Babai dwelling presentation owner.");
        }

        foreach (var mesh in meshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta(
                "suppressionReason",
                "authored Act1ConnectedWorld Babai dwelling replaces the duplicate Agent B hero house");
        }

        villageKit.SetMeta("suppressedHouseBabaiMeshCount", meshes.Length);
        villageKit.SetMeta(
            "babaiPresentationOwner",
            "Act1ConnectedWorld/Act1CoreWorldGreybox/Act1AuthoredExteriorKitPresentation");
    }

    private static void SuppressDuplicateBabaiOutbuildings(Node3D villageKit)
    {
        var prefixes = new[] { "ShedBabai_", "LeanToBabai_" };
        var meshes = EnumerateDescendants<MeshInstance3D>(villageKit)
            .Where(mesh => prefixes.Any(prefix => mesh.Name.ToString().StartsWith(prefix, System.StringComparison.Ordinal)))
            .ToArray();
        if (meshes.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B village-building kit is missing the declared Babai outbuilding families; cannot establish one yard presentation owner.");
        }

        foreach (var mesh in meshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta(
                "suppressionReason",
                "authored Act1ConnectedWorld yard shed/parcels replace duplicate Agent B Babai outbuildings");
        }

        villageKit.SetMeta("suppressedBabaiOutbuildingMeshCount", meshes.Length);
    }

    private static void SuppressRouteOccludingHouseA7(Node3D villageKit)
    {
        var meshes = EnumerateDescendants<MeshInstance3D>(villageKit)
            .Where(mesh => mesh.Name.ToString().StartsWith("HouseA7_", System.StringComparison.Ordinal))
            .ToArray();
        if (meshes.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B village-building kit is missing the declared HouseA7_* family; cannot clear the return-street camera envelope.");
        }

        foreach (var mesh in meshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta("suppressionReason", "HouseA7 intersects the authored return-street first-person envelope at the production waypoint");
        }

        villageKit.SetMeta("suppressedRoutePresentationFamilies", "HouseA7_*");
        villageKit.SetMeta("suppressedHouseA7MeshCount", meshes.Length);
    }

    private static void SuppressDuplicateNearStreetHouses(Node3D villageKit)
    {
        // The connected core already owns the authored near-street facades.
        // Agent B HouseA1..A4 are a second preview-board row; keeping both
        // makes roofs/chimneys read as floating duplicates in first person.
        // HouseA2's old boundary at x=10.8, z=-3.1..4.1 stands detached
        // beside the replacement EastParcel. Retire its fence/gate with the
        // house; keep other plots' boundaries until individually reconciled.
        var prefixes = new[] { "HouseA1_", "HouseA2_", "HouseA3_", "HouseA4_",
            "Fence_HouseA2_", "Gate_HouseA2_" };
        var meshes = EnumerateDescendants<MeshInstance3D>(villageKit)
            .Where(mesh => prefixes.Any(prefix => mesh.Name.ToString().StartsWith(prefix, System.StringComparison.Ordinal)))
            .ToArray();
        if (meshes.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B village-building kit is missing the declared HouseA1..HouseA4 families; cannot remove duplicate near-street presentation.");
        }

        foreach (var mesh in meshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta("suppressionReason", "connected Act1CoreWorldGreybox owns the authored near-street facades");
        }

        villageKit.SetMeta(
            "suppressedNearStreetPresentationFamilies",
            "HouseA1_*|HouseA2_*|HouseA3_*|HouseA4_*");
        villageKit.SetMeta("suppressedNearStreetHouseMeshCount", meshes.Length);
    }

    private static void SuppressRouteOccludingHouseA5Architecture(Node3D villageKit)
    {
        // HouseA5 is a preview-board building placed across the return view.
        // Remove only its attached architecture; retain the gate, fence and
        // woodpile so the parcel still reads as a lived-in boundary.
        var prefixes = new[]
        {
            "HouseA5_Body",
            "HouseA5_Foundation",
            "HouseA5_LogBand",
            "HouseA5_Roof_",
            "HouseA5_Chimney"
        };
        var meshes = EnumerateDescendants<MeshInstance3D>(villageKit)
            .Where(mesh => prefixes.Any(prefix => mesh.Name.ToString().StartsWith(prefix, System.StringComparison.Ordinal)))
            .ToArray();
        if (meshes.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B village-building kit is missing the declared HouseA5 architecture; cannot clear the return-street floating roof envelope.");
        }

        foreach (var mesh in meshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta("suppressionReason", "HouseA5 preview-board architecture floats across the authored return-street first-person envelope");
        }

        villageKit.SetMeta(
            "suppressedReturnArchitectureFamilies",
            "HouseA5_Body|HouseA5_Foundation|HouseA5_LogBand|HouseA5_Roof_*|HouseA5_Chimney");
        villageKit.SetMeta("suppressedHouseA5ArchitectureMeshCount", meshes.Length);
    }

    private static void SuppressRouteOccludingFarHouse3(Node3D villageKit)
    {
        // FarHouse3 is a preview-board silhouette at (-20, 8), exactly inside
        // the Babai-yard first-person stop. The connected-world parcels own
        // that near/mid composition; retaining this family turns the reverse
        // turn into a blank wall at arm's length.
        var meshes = EnumerateDescendants<MeshInstance3D>(villageKit)
            .Where(mesh => mesh.Name.ToString().StartsWith("FarHouse3_", System.StringComparison.Ordinal))
            .ToArray();
        if (meshes.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B village-building kit is missing the declared FarHouse3_* family; cannot clear the Babai-yard camera envelope.");
        }

        foreach (var mesh in meshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta("suppressionReason", "FarHouse3 preview-board silhouette overlaps the Babai-yard first-person stop");
        }

        villageKit.SetMeta("suppressedFarHouse3MeshCount", meshes.Length);

    }

    private static void SuppressRemainingPreviewArchitecture(Node3D villageKit)
    {
        // These five families are the remaining preview-board buildings in
        // the active village kit. Their embedded anchors sit in the same
        // return/connective parcels already owned by the authored exterior
        // layer: FarHouse1/Banya at the west return, FarHouse2 at the east
        // return, and HouseA6/A8 behind the east parcel fences. Keeping both
        // layers produces pale box duplicates in the first-person turns.
        var prefixes = new[] { "FarHouse1_", "FarHouse2_", "HouseA6_", "HouseA8_", "Banya_" };
        var meshes = EnumerateDescendants<MeshInstance3D>(villageKit)
            .Where(mesh => prefixes.Any(prefix => mesh.Name.ToString().StartsWith(prefix, System.StringComparison.Ordinal)))
            .ToArray();
        if (meshes.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B village-building kit is missing the remaining authored preview architecture families; cannot establish the connected-world exterior owner.");
        }

        foreach (var mesh in meshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta(
                "suppressionReason",
                "active Act1 authored return/connective parcels replace remaining preview-board distant architecture");
        }

        villageKit.SetMeta(
            "suppressedRemainingPreviewArchitectureFamilies",
            "FarHouse1_*|FarHouse2_*|HouseA6_*|HouseA8_*|Banya_*");
        villageKit.SetMeta("suppressedRemainingPreviewArchitectureMeshCount", meshes.Length);
    }

    private static void SuppressZiratRouteFenceRails(Node3D ziratKit)
    {
        // Keep the route-facing rails, west fence, gate/path and duplicate
        // markers/bench hidden. The eastern fence remains lateral and
        // route-safe; the authored roadside kit owns all respectful markers,
        // path and gate presentation. Terrain remains the traversal surface.
        var routeFenceFamilies = new[]
        {
            (Prefix: "Zirat_FenceS_", MetaName: "suppressedZiratFenceSouthMeshCount"),
            (Prefix: "Zirat_FenceN_", MetaName: "suppressedZiratFenceNorthMeshCount"),
            (Prefix: "Zirat_FenceW_", MetaName: "suppressedZiratFenceWestMeshCount"),
            (Prefix: "Zirat_Gate", MetaName: "suppressedZiratGateMeshCount"),
            (Prefix: "Zirat_Path", MetaName: "suppressedZiratPathMeshCount"),
            (Prefix: "Zirat_Marker", MetaName: "suppressedZiratMarkerMeshCount"),
            (Prefix: "Zirat_Bench", MetaName: "suppressedZiratBenchMeshCount")
        };
        var routeFenceTotal = 0;
        foreach (var (prefix, metaName) in routeFenceFamilies)
        {
            var meshes = EnumerateDescendants<MeshInstance3D>(ziratKit)
                .Where(mesh => mesh.Name.ToString().StartsWith(prefix, System.StringComparison.Ordinal))
                .ToArray();
            if (meshes.Length == 0)
            {
                throw new System.InvalidOperationException(
                    $"Agent B zirat kit is missing the declared {prefix} family; cannot clear the route-spanning fence rails.");
            }

            foreach (var mesh in meshes)
            {
                mesh.Visible = false;
                mesh.SetMeta("agentBPresentationSuppressed", true);
                mesh.SetMeta(
                    "suppressionReason",
                    "Agent B zirat preview geometry duplicates the authored roadside kit presentation owner");
            }

            ziratKit.SetMeta(metaName, meshes.Length);
            routeFenceTotal += meshes.Length;
        }

        ziratKit.SetMeta(
            "suppressedZiratRouteFenceFamilies",
            "Zirat_FenceS_*|Zirat_FenceN_*|Zirat_FenceW_*|Zirat_Gate*|Zirat_Path*|Zirat_Marker*|Zirat_Bench*");
        ziratKit.SetMeta("suppressedZiratRouteFenceMeshCount", routeFenceTotal);

        var restoredDepthFamilies = new[]
        {
            (Prefix: "Zirat_FenceE_", MetaName: "restoredZiratFenceEastMeshCount")
        };
        var restoredDepthTotal = 0;
        var restoredDepthMaterialRebound = 0;
        var quietFenceMaterial = Urman.Godot.PainterlyMaterialLibrary.ForColor("665f50", "wood_fence");
        foreach (var (prefix, metaName) in restoredDepthFamilies)
        {
            var meshes = EnumerateDescendants<MeshInstance3D>(ziratKit)
                .Where(mesh => mesh.Name.ToString().StartsWith(prefix, System.StringComparison.Ordinal))
                .ToArray();
            if (meshes.Length == 0)
            {
                throw new System.InvalidOperationException(
                    $"Agent B zirat kit is missing the declared depth {prefix} family; cannot restore the route-safe zirat return layer.");
            }

            foreach (var mesh in meshes)
            {
                mesh.Visible = true;
                mesh.SetMeta("presentationOnly", true);
                mesh.SetMeta("visualOnly", true);
                mesh.SetMeta("presentationRole", "quiet eastern boundary depth");
                if (mesh.Mesh is null)
                {
                    continue;
                }

                for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                {
                    mesh.SetSurfaceOverrideMaterial(surface, quietFenceMaterial);
                    restoredDepthMaterialRebound++;
                }
            }

            ziratKit.SetMeta(metaName, meshes.Length);
            restoredDepthTotal += meshes.Length;
        }

        ziratKit.SetMeta(
            "restoredZiratDepthFamilies",
            "Zirat_FenceE_*");
        ziratKit.SetMeta("restoredZiratDepthMeshCount", restoredDepthTotal);
        ziratKit.SetMeta("restoredZiratDepthMaterialReboundCount", restoredDepthMaterialRebound);
        ziratKit.SetMeta(
            "suppressedZiratDuplicateFamilies",
            "Zirat_FenceS_*|Zirat_FenceN_*|Zirat_FenceW_*|Zirat_Gate*|Zirat_Path*|Zirat_Marker*|Zirat_Bench*");
        ziratKit.SetMeta("suppressedZiratDuplicateMeshCount", routeFenceTotal);
        ziratKit.SetMeta(
            "ziratPresentationOwner",
            "Act1CoreWorldGreybox/ZiratMemoryField/ZiratRoadsideAuthoredKitPresentation");
        ziratKit.SetMeta(
            "ziratDepthPolicy",
            "authored roadside kit owns markers/path/gate; only Agent B eastern fence remains as lateral route-safe walk-through depth");
    }

    private static void SuppressRouteOccludingKaraMasses(Node3D karaKit)
    {
        // KaraMass_* are broad preview-board blobs (near and far) that become
        // opaque walls from the zirat and Kara camera envelopes. Suppress the
        // direct Agent B trunk, cliff and gesture families; authored
        // connected-world Kara dressing remains the bank/edge owner. Draw
        // only is changed, before architecture collision, so traversal and
        // collision ownership stay unchanged.
        var routeOccludingMasses = EnumerateDescendants<MeshInstance3D>(karaKit)
            .Where(mesh => mesh.Name.ToString().StartsWith("KaraMass_", System.StringComparison.Ordinal))
            .ToArray();
        if (routeOccludingMasses.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B Kara kit is missing the declared KaraMass_* family; cannot clear route-scale forest occluders.");
        }

        foreach (var mesh in routeOccludingMasses)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta(
                "suppressionReason",
                "KaraMass_* preview-board forest blobs occlude the zirat and Kara first-person camera envelopes");
        }

        karaKit.SetMeta(
            "suppressedKaraOccludingFamilies",
            "KaraMass_*");
        karaKit.SetMeta("suppressedKaraOccludingMeshCount", routeOccludingMasses.Length);

        var repetitiveSilhouettes = EnumerateDescendants<MeshInstance3D>(karaKit)
            .Where(mesh =>
            {
                var name = mesh.Name.ToString();
                return name.StartsWith("KaraTrunk_", System.StringComparison.Ordinal)
                    || name.StartsWith("CliffRing_", System.StringComparison.Ordinal)
                    || name.StartsWith("KaraGesture_", System.StringComparison.Ordinal);
            })
            .ToArray();
        if (repetitiveSilhouettes.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B Kara kit is missing the declared direct trunk/cliff/gesture silhouette families; cannot clear route-scale forest occluders.");
        }

        foreach (var mesh in repetitiveSilhouettes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta(
                "suppressionReason",
                "repetitive Agent B trunk/cliff/gesture silhouettes amplify the cheap modular read; authored Kara edge owns depth");
        }

        karaKit.SetMeta(
            "suppressedKaraRepetitiveSilhouetteFamilies",
            "KaraTrunk_*|CliffRing_*|KaraGesture_*");
        karaKit.SetMeta("suppressedKaraRepetitiveSilhouetteMeshCount", repetitiveSilhouettes.Length);
        karaKit.SetMeta(
            "karaGesturePresentation",
            "suppressed; authored connected-world Kara forest-edge presentation owns silhouettes and depth");

        // These exact Agent B meshes are the repeated brown faceted boulders,
        // fallen props and root banks occupying the lower foreground of the
        // Kara approach cameras. Hide their draw only; keep the existing
        // collision owner and route envelope unchanged.
        var foregroundBoulders = EnumerateDescendants<MeshInstance3D>(karaKit)
            .Where(mesh => mesh.Name.ToString().StartsWith("KaraRoot_", System.StringComparison.Ordinal))
            .ToArray();
        if (foregroundBoulders.Length != 4)
        {
            throw new System.InvalidOperationException(
                $"Agent B Kara kit foreground boulder owner changed: expected KaraRoot_0..3, found {foregroundBoulders.Length}.");
        }

        foreach (var mesh in foregroundBoulders)
        {
            mesh.Visible = false;
        }

        karaKit.SetMeta("hiddenKaraForegroundBoulderFamily", "KaraRoot_0..3");
        karaKit.SetMeta("hiddenKaraForegroundBoulderMeshCount", foregroundBoulders.Length);

        var clutterFamilies = new[]
        {
            (Prefix: "FallenLog_K", MetaName: "hiddenKaraFallenLogMeshCount"),
            (Prefix: "KaraStone_", MetaName: "hiddenKaraStoneMeshCount")
        };
        var hiddenClutterCount = foregroundBoulders.Length;
        foreach (var (prefix, metaName) in clutterFamilies)
        {
            var clutter = EnumerateDescendants<MeshInstance3D>(karaKit)
                .Where(mesh => mesh.Name.ToString().StartsWith(prefix, System.StringComparison.Ordinal))
                .ToArray();
            if (clutter.Length == 0)
            {
                throw new System.InvalidOperationException(
                    $"Agent B Kara kit is missing the declared foreground clutter family {prefix}; cannot clear the route-facing prop scatter.");
            }

            foreach (var mesh in clutter)
            {
                mesh.Visible = false;
            }

            karaKit.SetMeta(metaName, clutter.Length);
            hiddenClutterCount += clutter.Length;
        }

        karaKit.SetMeta(
            "hiddenKaraForegroundClutterFamilies",
            "KaraRoot_*|FallenLog_K*|KaraStone_*");
        karaKit.SetMeta("hiddenKaraForegroundClutterMeshCount", hiddenClutterCount);
    }

    private static bool ShouldCollide(string name)
    {
        // Verified Agent B contract: decorations stay walk-through.
        if (name.StartsWith("Rut_") || name.StartsWith("Ditch_") || name.StartsWith("MudStrip_")) return false;
        if (name.StartsWith("Puddle_") || name.StartsWith("Apron_") || name.StartsWith("Road_")) return false;
        if (name.Contains("Glass") || name.Contains("Slab") || name.Contains("Gable")) return false;
        if (name.Contains("Win") || name.Contains("Sill")) return false;
        if (name.Contains("LogBand") || name.Contains("Trim") || name.Contains("Sign")) return false;
        if (name.Contains("GateLeaf") || name.Contains("Woodpile") || name.Contains("Leaf")) return false;
        if (name.StartsWith("Zirat_", System.StringComparison.Ordinal)) return false;
        if (name.StartsWith("KaraMass")
            || name.StartsWith("KaraGesture")
            || name.StartsWith("KaraTrunk_")
            || name.StartsWith("CliffRing_")) return false;
        return true;
    }

    private void PlantFoliage()
    {
        var kit = GetNodeOrNull<Node3D>("AgentB_FoliageKit");
        if (kit is null)
        {
            return;
        }

        var templates = new Dictionary<string, List<Node3D>>(System.StringComparer.Ordinal);
        foreach (var node in EnumerateDescendants<Node3D>(kit))
        {
            var key = VariantKey(node.Name.ToString());
            if (key is null || !HasMeshInSubtree(node))
            {
                continue;
            }

            if (!templates.TryGetValue(key, out var list))
            {
                list = new List<Node3D>();
                templates[key] = list;
            }

            list.Add(node);
        }

        var plants = new Node3D { Name = "AgentB_PlantedFoliage" };
        plants.SetMeta("presentationOnly", true);
        plants.SetMeta("visualOnly", true);
        plants.SetMeta(
            "variationPolicy",
            "deterministic per-entry scale/yaw and near-mid-far value grades; no new foliage entries");
        AddChild(plants);
        // The GLB is a source library, not a second world layer. Keep its
        // authored template families available for deterministic extraction,
        // then hide the source root so the template board cannot leak into
        // the playable village or double every planted family.
        kit.Visible = false;
        kit.SetMeta("templateSourceHidden", true);
        kit.SetMeta(
            "templateSourcePolicy",
            "template geometry is hidden after extraction; only deterministic planted copies are visible");

        var plannedEntries = BuildDensifiedPlan();
        var plantedEntryCount = 0;
        var suppressedKaraFoliageEntryCount = 0;
        var minimumRoadClearance = float.MaxValue;
        var invalidPlacements = new List<string>();
        foreach (var (position, variant) in plannedEntries)
        {
            if (!templates.TryGetValue(variant, out var parts))
            {
                continue;
            }

            var roadInfo = AgentBAct1HeightField.RoadInfo(position.X, position.Y);
            var roadClearance = (float)(roadInfo.Distance - roadInfo.HalfWidth);
            if (roadClearance < 0.25f)
            {
                invalidPlacements.Add(
                    $"{variant}@({position.X:F2},{position.Y:F2}) clearance={roadClearance:F2}m");
                continue;
            }

            minimumRoadClearance = Mathf.Min(minimumRoadClearance, roadClearance);

            plantedEntryCount++;

            // Keep the accepted plan and planted-node counts intact while
            // hiding repeated low silhouettes in the village/zirat day
            // envelope. Kara's authored kit owns its large near/mid/far edge,
            // so hide only the repeated high foliage and random stone/stump
            // accents there; sparse fern/grass contact remains.
            var suppressKaraRepeatedFoliage = position.Y <= -86f
                && (variant.StartsWith("Birch_", System.StringComparison.Ordinal)
                    || variant.StartsWith("Spruce_", System.StringComparison.Ordinal)
                    || variant.StartsWith("MossStone_", System.StringComparison.Ordinal)
                    || variant.StartsWith("Stump_", System.StringComparison.Ordinal));
            if (suppressKaraRepeatedFoliage)
            {
                suppressedKaraFoliageEntryCount++;
            }

            var suppressPlantedCopy = position.Y > -86f
                && (variant.StartsWith("FallenBranch_", System.StringComparison.Ordinal)
                    || variant.StartsWith("GrassTuft_", System.StringComparison.Ordinal)
                    || variant.StartsWith("MossStone_", System.StringComparison.Ordinal))
                && DeterministicPhase(position, 17.3f) < 0.75f;
            suppressPlantedCopy |= suppressKaraRepeatedFoliage;

            // Foliage source meshes are intentionally laid out on a Blender
            // preview board. Their imported node origins stay at zero while
            // the vertices retain the board offset (for example Pine_2 is
            // authored around x=48). Rebase from the actual mesh geometry,
            // not from Node3D.GlobalPosition, or a planted tree teleports far
            // outside its declared parcel and can become a route-wide visual
            // occluder.
            var horizontalSum = Vector2.Zero;
            var visualPartCount = 0;
            foreach (var part in parts)
            {
                foreach (var mesh in EnumerateSelfAndDescendants<MeshInstance3D>(part))
                {
                    if (mesh.Mesh is null)
                    {
                        continue;
                    }

                    var meshCenter = mesh.GlobalTransform * mesh.Mesh.GetAabb().GetCenter();
                    horizontalSum += new Vector2(meshCenter.X, meshCenter.Z);
                    visualPartCount++;
                }
            }

            var centroid = visualPartCount > 0
                ? horizontalSum / visualPartCount
                : parts.Aggregate(Vector2.Zero, (sum, part) => sum + new Vector2(part.GlobalPosition.X, part.GlobalPosition.Z)) / parts.Count;
            var groundY = (float)AgentBAct1HeightField.Ground(position.X, position.Y);
            var target = new Vector3(position.X, groundY, position.Y);
            var depth = Mathf.Clamp((-position.Y - 8f) / 118f, 0f, 1f);
            var horizontalScale = Mathf.Lerp(1.04f, 0.90f, depth)
                * Mathf.Lerp(0.92f, 1.08f, DeterministicPhase(position, 2.7f));
            var verticalScale = Mathf.Lerp(1.02f, 0.90f, depth)
                * Mathf.Lerp(0.93f, 1.07f, DeterministicPhase(position, 4.9f));
            if (position.Y <= -86f)
            {
                // Kara gets a slightly broader, lower canopy so the existing
                // edge families feel denser without growing into the road.
                horizontalScale *= 1.04f;
                verticalScale *= 0.98f;
            }

            var yaw = Mathf.Lerp(-14f, 14f, DeterministicPhase(position, 8.1f));
            var yawRadians = Mathf.DegToRad(yaw);
            var cosYaw = Mathf.Cos(yawRadians);
            var sinYaw = Mathf.Sin(yawRadians);
            foreach (var part in parts)
            {
                var sourceGeometrySum = Vector2.Zero;
                var sourceGeometryCount = 0;
                var sourceMeshes = EnumerateSelfAndDescendants<MeshInstance3D>(part)
                    .Where(mesh => mesh.Mesh is not null)
                    .ToArray();
                foreach (var mesh in sourceMeshes)
                {
                    var meshCenter = mesh.GlobalTransform * mesh.Mesh!.GetAabb().GetCenter();
                    sourceGeometrySum += new Vector2(meshCenter.X, meshCenter.Z);
                    sourceGeometryCount++;
                }

                var sourceGeometryCenter = sourceGeometryCount > 0
                    ? sourceGeometrySum / sourceGeometryCount
                    : new Vector2(part.GlobalPosition.X, part.GlobalPosition.Z);
                if (part.Duplicate() is not Node3D copy)
                {
                    continue;
                }

                // Regional bark/leaf grading depends on semantic part names.
                // Preserve those on repeated variants instead of @MeshInstance3D.
                plants.AddChild(copy, forceReadableName: true);
                copy.Visible = !suppressPlantedCopy;
                // Use the source geometry center for both the family offset and
                // the copied root. Remove that part's preview-board offset from
                // its vertices once, so the root stays near the target while
                // the visible world AABB remains unchanged.
                var sourcePosition = part.GlobalPosition;
                var sourceOffset = new Vector2(
                    sourceGeometryCenter.X - centroid.X,
                    sourceGeometryCenter.Y - centroid.Y);
                var rotatedOffset = new Vector2(
                    sourceOffset.X * cosYaw - sourceOffset.Y * sinYaw,
                    sourceOffset.X * sinYaw + sourceOffset.Y * cosYaw);
                var rootRebaseSourceOffset = new Vector2(
                    sourcePosition.X - sourceGeometryCenter.X,
                    sourcePosition.Z - sourceGeometryCenter.Y);
                var rotatedRootRebaseOffset = new Vector2(
                    rootRebaseSourceOffset.X * cosYaw - rootRebaseSourceOffset.Y * sinYaw,
                    rootRebaseSourceOffset.X * sinYaw + rootRebaseSourceOffset.Y * cosYaw);
                var rootRebaseDelta = new Vector3(
                    rotatedRootRebaseOffset.X * horizontalScale,
                    0f,
                    rotatedRootRebaseOffset.Y * horizontalScale);

                copy.GlobalPosition = new Vector3(
                    target.X + rotatedOffset.X * horizontalScale,
                    target.Y + sourcePosition.Y * verticalScale,
                    target.Z + rotatedOffset.Y * horizontalScale);
                copy.RotationDegrees = part.RotationDegrees + new Vector3(0f, yaw, 0f);
                copy.Scale = new Vector3(
                    part.Scale.X * horizontalScale,
                    part.Scale.Y * verticalScale,
                    part.Scale.Z * horizontalScale);

                var copiedMeshes = EnumerateSelfAndDescendants<MeshInstance3D>(copy)
                    .Where(mesh => mesh.Mesh is not null)
                    .ToArray();
                if (copiedMeshes.Length != sourceMeshes.Length)
                {
                    throw new InvalidOperationException(
                        $"Foliage family '{variant}' duplicated with mismatched mesh parts.");
                }

                for (var meshIndex = 0; meshIndex < sourceMeshes.Length; meshIndex++)
                {
                    var sourceMesh = sourceMeshes[meshIndex];
                    var copiedMesh = copiedMeshes[meshIndex];
                    if (sourceMesh.Mesh is not ArrayMesh sourceArrayMesh)
                    {
                        throw new InvalidOperationException(
                            $"Foliage family '{variant}' requires ArrayMesh source geometry.");
                    }

                    // Preserve the old rendered transform exactly: the old
                    // node-origin root was rootRebaseDelta away from this
                    // geometry-centred root, so apply that delta once in the
                    // copied mesh's local vertex space.
                    var localRootRebaseDelta = copiedMesh.GlobalTransform.Basis.Inverse() * rootRebaseDelta;
                    var rebasedMesh = new ArrayMesh();
                    for (var surface = 0; surface < sourceArrayMesh.GetSurfaceCount(); surface++)
                    {
                        var arrays = sourceArrayMesh.SurfaceGetArrays(surface).Duplicate(true);
                        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                        for (var vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                        {
                            vertices[vertexIndex] += localRootRebaseDelta;
                        }

                        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                        rebasedMesh.AddSurfaceFromArrays(
                            sourceArrayMesh.SurfaceGetPrimitiveType(surface),
                            arrays);
                        if (sourceArrayMesh.SurfaceGetMaterial(surface) is Material material)
                        {
                            rebasedMesh.SurfaceSetMaterial(surface, material);
                        }
                    }

                    copiedMesh.Mesh = rebasedMesh;
                }
                RegradeRegionalFoliage(copy, variant, position.Y);
            }
        }

        if (invalidPlacements.Count > 0)
        {
            throw new System.InvalidOperationException(
                "Agent B foliage placements overlap the protected road envelope: "
                + string.Join(" | ", invalidPlacements));
        }

        SetMeta("plantedVariantCount", templates.Count);
        SetMeta("plannedFoliageEntryCount", plannedEntries.Count);
        SetMeta("plantedFoliageEntryCount", plantedEntryCount);
        SetMeta("plantedFoliageNodeCount", plants.GetChildCount());
        SetMeta("suppressedKaraFoliageEntryCount", suppressedKaraFoliageEntryCount);
        SetMeta("minimumFoliageRoadClearance", minimumRoadClearance);
        SetMeta(
            "foliagePlacementPolicy",
            "all planted entries fail closed at >=0.25m from the authored road envelope; foliage remains presentation-only");
        SetMeta(
            "foliageRebasePolicy",
            "planted copies remove source preview-board X/Z offsets from mesh geometry while preserving vertical ground pivots");
        SetMeta(
            "regionalFoliageGradePolicy",
            "all planted foliage uses deterministic near/mid/far values; Kara retains sparse contact planting while authored Kara silhouettes own the edge");
    }

    /// <summary>
    /// Density pass: satellites clustered around every authored entry plus a
    /// two-row perimeter belt that turns the walkable edge into a visible
    /// tree line. Roads cut their own gaps because every generated entry
    /// passes the same road-envelope clearance check as the base plan.
    /// Deterministic seed keeps captures reproducible.
    /// </summary>
    private List<(Vector2 Position, string Variant)> BuildDensifiedPlan()
    {
        var baseEntries = new List<(Vector2, string)>();
        var generated = new List<(Vector2, string)>();
        var rng = new RandomNumberGenerator { Seed = 20260910 };
        var families = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Birch"] = new[] { "Birch_1", "Birch_2", "Birch_3" },
            ["Spruce"] = new[] { "Spruce_1", "Spruce_2" },
            ["Pine"] = new[] { "Pine_1", "Pine_2" },
            ["Shrub"] = new[] { "Shrub_1", "Shrub_2", "Shrub_3" },
            ["Fern"] = new[] { "Fern_0", "Fern_1", "Fern_2" },
            ["Sedge"] = new[] { "Sedge_0", "Sedge_1", "Sedge_2" },
            ["GrassTuft"] = new[] { "GrassTuft_0", "GrassTuft_1", "GrassTuft_2" }
        };

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var (position, variant) in AgentBFoliagePlan.Entries)
        {
            // Act I is winter: authored summer trees become their bare,
            // snow-dusted winter forms; ground cover keeps its kit variants
            // and picks up snow through the material layer.
            var winterVariant = variant switch
            {
                var v when v.StartsWith("Birch_", StringComparison.Ordinal) =>
                    v.EndsWith("_3", StringComparison.Ordinal) ? "WinterBirch_1" : "WinterBirch_2",
                var v when v.StartsWith("Spruce_", StringComparison.Ordinal) => "WinterSpruce_1",
                var v when v.StartsWith("Pine_", StringComparison.Ordinal) => "WinterSpruce_2",
                _ => variant
            };
            // Authored summer trees that now sit on the winter road shoulder
            // are dropped rather than left growing out of the kerb.
            var isAuthoredTree = winterVariant.Contains("Winter", StringComparison.Ordinal);
            if (isAuthoredTree)
            {
                var authoredRoad = AgentBAct1HeightField.RoadInfo(position.X, position.Y);
                if ((float)(authoredRoad.Distance - authoredRoad.HalfWidth) < 2.0f)
                {
                    continue;
                }
            }

            baseEntries.Add((position, winterVariant));
            min = new Vector2(Mathf.Min(min.X, position.X), Mathf.Min(min.Y, position.Y));
            max = new Vector2(Mathf.Max(max.X, position.X), Mathf.Max(max.Y, position.Y));

            var family = variant.Split('_')[0];
            if (!families.TryGetValue(family, out var variants))
            {
                continue;
            }

            var satellites = family switch
            {
                "Birch" or "Spruce" or "Pine" => 3,
                "Shrub" => 4,
                "Fern" => 2,
                "Sedge" => 2,
                _ => 3
            };
            for (var i = 0; i < satellites; i++)
            {
                var angle = rng.RandfRange(0f, Mathf.Tau);
                var distance = rng.RandfRange(1.6f, 4.4f);
                var variantPick = variants[rng.RandiRange(0, variants.Length - 1)];
                generated.Add((position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, variantPick));
            }
        }

        // Roadside green band: a fine grid hugging every road edge (verges,
        // fence lines, yard palisades across the whole village), so the
        // street stops reading as empty. Density falls off with distance
        // from the road edge; a spacing check keeps it from clumping.
        var spacing = new List<Vector2>();
        foreach (var (position, _) in baseEntries)
        {
            spacing.Add(position);
        }

        for (var x = min.X - 4f; x <= max.X + 4f; x += 2.2f)
        {
            for (var z = min.Y - 4f; z <= max.Y + 4f; z += 2.2f)
            {
                var point = new Vector2(
                    x + rng.RandfRange(-0.9f, 0.9f),
                    z + rng.RandfRange(-0.9f, 0.9f));
                var roadInfo = AgentBAct1HeightField.RoadInfo(point.X, point.Y);
                var edge = (float)(roadInfo.Distance - roadInfo.HalfWidth);
                if (edge < 0.45f || edge > 2.7f)
                {
                    continue;
                }

                var tooClose = false;
                foreach (var existing in spacing)
                {
                    if (existing.DistanceTo(point) < 1.7f)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                var roll = rng.Randf();
                var variant = roll switch
                {
                    < 0.44f => "GrassTuft_" + rng.RandiRange(0, 2),
                    < 0.62f => "Sedge_" + rng.RandiRange(0, 2),
                    < 0.74f => "Shrub_" + rng.RandiRange(1, 3),
                    < 0.84f => "WinterRowan_" + rng.RandiRange(1, 2),
                    < 0.92f => "WinterBirdCherry_1",
                    < 0.97f => "WinterLinden_1",
                    _ => "WinterBirch_" + rng.RandiRange(1, 2)
                };
                generated.Add((point, variant));
                spacing.Add(point);
            }
        }

        // Village interior planting: kitchen-garden beds at the hero yard,
        // a clinic yard, and palisade/birch scatter along the residential
        // band between houses. Keep-outs protect house footprints and the
        // spawn apron; the road envelope check still owns the roads.
        var keepOuts = new (Vector2 Center, float Radius)[]
        {
            (new Vector2(-30f, -1f), 5.6f),   // Babai/Ebi house
            (new Vector2(30f, -28f), 6.4f),   // FAP
            (new Vector2(-24.4f, 1.7f), 2.6f),// yard spawn apron
            (new Vector2(24f, -24f), 2.6f)    // clinic spawn apron
        };

        void TryPlant(float x, float z, float minEdge, float maxEdge, float minSpacing, string variant)
        {
            var point = new Vector2(x, z);
            var roadInfo = AgentBAct1HeightField.RoadInfo(point.X, point.Y);
            var edge = (float)(roadInfo.Distance - roadInfo.HalfWidth);
            if (edge < minEdge || edge > maxEdge)
            {
                return;
            }

            foreach (var (center, radius) in keepOuts)
            {
                if (center.DistanceTo(point) < radius)
                {
                    return;
                }
            }

            foreach (var existing in spacing)
            {
                if (existing.DistanceTo(point) < minSpacing)
                {
                    return;
                }
            }

            generated.Add((point, variant));
            spacing.Add(point);
        }

        // 1) Kitchen-garden beds beside the hero house (rows, village-like).
        for (var x = -37f; x <= -21f; x += 1.35f)
        {
            for (var z = -9f; z <= 7f; z += 1.35f)
            {
                var roll = rng.Randf();
                var variant = roll switch
                {
                    < 0.56f => "Sedge_" + rng.RandiRange(0, 2),
                    < 0.82f => "GrassTuft_" + rng.RandiRange(0, 2),
                    < 0.94f => "Shrub_" + rng.RandiRange(1, 3),
                    _ => "WinterWillow_1"
                };
                TryPlant(x + rng.RandfRange(-0.5f, 0.5f), z + rng.RandfRange(-0.5f, 0.5f),
                    0.45f, 24f, 1.15f, variant);
            }
        }

        // 2) Clinic yard: cut grass tufts and a few shrubs, no beds.
        for (var x = 21f; x <= 39f; x += 2.1f)
        {
            for (var z = -37f; z <= -19f; z += 2.1f)
            {
                var roll = rng.Randf();
                var variant = roll switch
                {
                    < 0.64f => "GrassTuft_" + rng.RandiRange(0, 2),
                    < 0.84f => "Sedge_" + rng.RandiRange(0, 2),
                    _ => "Shrub_" + rng.RandiRange(1, 3)
                };
                TryPlant(x + rng.RandfRange(-0.7f, 0.7f), z + rng.RandfRange(-0.7f, 0.7f),
                    0.45f, 24f, 1.6f, variant);
            }
        }

        // 3) Residential band: palisade shrubs, occasional birch, scattered
        //    garden green between the houses on both sides of the street.
        for (var x = -34f; x <= 34f; x += 2.6f)
        {
            for (var z = 9f; z >= -49f; z -= 2.6f)
            {
                if (Mathf.Abs(x) < 7f)
                {
                    continue; // keep the street corridor itself open
                }

                var roll = rng.Randf();
                var variant = roll switch
                {
                    < 0.38f => "GrassTuft_" + rng.RandiRange(0, 2),
                    < 0.60f => "Sedge_" + rng.RandiRange(0, 2),
                    < 0.74f => "Shrub_" + rng.RandiRange(1, 3),
                    < 0.82f => "WinterRowan_" + rng.RandiRange(1, 2),
                    < 0.88f => "WinterMaple_1",
                    < 0.94f => "WinterLinden_" + rng.RandiRange(1, 2),
                    _ => "WinterBirch_" + rng.RandiRange(1, 2)
                };
                TryPlant(x + rng.RandfRange(-1f, 1f), z + rng.RandfRange(-1f, 1f),
                    1.2f, 30f, 2.3f, variant);
            }
        }

        // Rim forest: large silhouette conifers/spruces on the rising west,
        // east and forest rims, so the horizon closes with forest instead of
        // an empty edge. Big, sparse and far — cheap, reads as skyline.
        var rimZones = new (float X0, float X1, float Z0, float Z1)[]
        {
            (-62f, -40f, -120f, 14f),   // west rim
            (44f, 64f, -120f, 14f),     // east rim
            (-30f, 30f, -150f, -124f)   // forest rim behind kara
        };
        foreach (var (x0, x1, z0, z1) in rimZones)
        {
            for (var x = x0; x <= x1; x += 4.0f)
            {
                for (var z = z0; z <= z1; z += 4.0f)
                {
                    if (rng.Randf() > 0.45f)
                    {
                        continue;
                    }

                    var point = new Vector2(x + rng.RandfRange(-1.6f, 1.6f), z + rng.RandfRange(-1.6f, 1.6f));
                    var roadInfo = AgentBAct1HeightField.RoadInfo(point.X, point.Y);
                    if ((float)(roadInfo.Distance - roadInfo.HalfWidth) < 2.0f)
                    {
                        continue;
                    }

                    // Snow-laden spruce belongs to the forest side only;
                    // the village rim is bare deciduous winter woodland.
                    var forestSide = point.Y <= -100f;
                    var variant = forestSide && rng.Randf() < 0.55f
                        ? "WinterSpruce_" + rng.RandiRange(1, 2)
                        : "WinterLightBirch_" + rng.RandiRange(1, 2);
                    generated.Add((point, variant));
                }
            }
        }

        // Two-row perimeter belt: dense conifer/birch line with shrub and
        // ground cover underneath, so the village edge reads as forest edge
        // rather than an invisible wall. Roads reject their own gap.
        var margin = 7f;
        var beltMin = min - new Vector2(margin, margin);
        var beltMax = max + new Vector2(margin, margin);
        const float beltStep = 3.8f;
        for (var row = 0; row < 2; row++)
        {
            var inset = row * 3.0f;
            for (var x = beltMin.X + inset; x <= beltMax.X - inset; x += beltStep)
            {
                EmitBelt(generated, rng, new Vector2(x + rng.RandfRange(-1.1f, 1.1f), beltMin.Y + inset));
                EmitBelt(generated, rng, new Vector2(x + rng.RandfRange(-1.1f, 1.1f), beltMax.Y - inset));
            }
            for (var z = beltMin.Y + inset; z <= beltMax.Y - inset; z += beltStep)
            {
                EmitBelt(generated, rng, new Vector2(beltMin.X + inset, z + rng.RandfRange(-1.1f, 1.1f)));
                EmitBelt(generated, rng, new Vector2(beltMax.X - inset, z + rng.RandfRange(-1.1f, 1.1f)));
            }
        }

        // Generated entries (satellites + belt) must never violate the road
        // envelope; unlike authored plan entries they are filtered out
        // silently so roads keep their own natural gaps through the belt.
        // Trees need real shoulder distance — a trunk half a metre from the
        // kerb reads as "a tree growing on the road". Only low ground cover
        // is allowed to hug the verge.
        var culledTrees = 0;
        foreach (var (position, variant) in generated)
        {
            var roadInfo = AgentBAct1HeightField.RoadInfo(position.X, position.Y);
            var clearance = (float)(roadInfo.Distance - roadInfo.HalfWidth);
            var isTree = variant.Contains("Birch", StringComparison.Ordinal)
                || variant.Contains("Linden", StringComparison.Ordinal)
                || variant.Contains("Maple", StringComparison.Ordinal)
                || variant.Contains("Rowan", StringComparison.Ordinal)
                || variant.Contains("BirdCherry", StringComparison.Ordinal)
                || variant.Contains("Willow", StringComparison.Ordinal)
                || variant.Contains("Spruce", StringComparison.Ordinal);
            var required = isTree ? 2.6f : 0.75f;
            if (clearance >= required)
            {
                baseEntries.Add((position, variant));
            }
            else if (isTree)
            {
                culledTrees++;
            }
        }

        SetMeta("winterRoadClearanceTreeCulls", culledTrees);
        SetMeta("winterRoadClearancePolicy", "trees >= 2.6m, ground cover >= 0.75m from the road envelope");

        return baseEntries;
    }

    private static void EmitBelt(
        List<(Vector2, string)> planned,
        RandomNumberGenerator rng,
        Vector2 position)
    {
        // Village belt is winter deciduous; the forest-side stretch keeps
        // young spruce as the sanctioned village→forest transition.
        var forestSide = position.Y <= -100f;
        var treeRoll = rng.Randf();
        var variant = forestSide && treeRoll < 0.45f
            ? "WinterSpruce_" + rng.RandiRange(1, 2)
            : treeRoll switch
            {
                < 0.30f => "WinterBirch_1",
                < 0.48f => "WinterBirch_2",
                < 0.60f => "WinterLinden_1",
                < 0.70f => "WinterMaple_1",
                < 0.80f => "WinterRowan_" + rng.RandiRange(1, 2),
                < 0.88f => "WinterBirdCherry_1",
                < 0.94f => "WinterWillow_1",
                _ => "Shrub_2"
            };
        planned.Add((position, variant));

        var undergrowth = rng.Randf() switch
        {
            < 0.3f => "Fern_1",
            < 0.55f => "Sedge_1",
            < 0.8f => "Shrub_1",
            _ => "GrassTuft_1"
        };
        planned.Add((position + new Vector2(rng.RandfRange(-1.6f, 1.6f), rng.RandfRange(-1.6f, 1.6f)), undergrowth));
    }

    private static void RegradeRegionalFoliage(Node3D copy, string variant, float routeZ = 0f)
    {
        var isBirch = variant.StartsWith("Birch_", System.StringComparison.Ordinal)
            || variant.Contains("Birch_", System.StringComparison.Ordinal);
        var isConifer = variant.StartsWith("Pine_", System.StringComparison.Ordinal)
            || variant.StartsWith("Spruce_", System.StringComparison.Ordinal)
            || variant.Contains("Spruce_", System.StringComparison.Ordinal);
        var isKara = routeZ <= -86f;
        var isZirat = routeZ <= -58f;
        var depth = Mathf.Clamp((-routeZ - 8f) / 118f, 0f, 1f);
        var foliageColor = isKara
            ? isBirch ? "465643" : isConifer ? "33483e" : "3d5040"
            : isZirat
                ? isBirch ? "56644d" : isConifer ? "3f5444" : "4e6049"
                : depth > 0.35f
                    ? isBirch ? "5d6c51" : isConifer ? "475b49" : "56674d"
                    : isBirch ? "647354" : isConifer ? "4c624d" : "5c6d50";
        var foliageSurface = isBirch ? "leaf_birch" : "foliage";
        var foliage = Urman.Godot.PainterlyMaterialLibrary.ForColor(foliageColor, foliageSurface);
        var foliageShadow = Urman.Godot.PainterlyMaterialLibrary.ForColor(
            isKara ? "2f4138" : isZirat ? "465640" : "506047",
            foliageSurface);
        var foliageLight = Urman.Godot.PainterlyMaterialLibrary.ForColor(
            isKara ? "425440" : isZirat ? "5b694e" : "687857",
            foliageSurface);
        var trunk = Urman.Godot.PainterlyMaterialLibrary.ForColor(
            isKara
                ? isBirch ? "5b5b50" : "48443c"
                : isBirch ? "625f54" : "51483c",
            isBirch ? "bark_birch" : "bark_pine");
        var groundAccent = Urman.Godot.PainterlyMaterialLibrary.ForColor(
            isKara ? "465843" : isZirat ? "596049" : "5e6c50",
            "foliage");
        var stone = Urman.Godot.PainterlyMaterialLibrary.ForColor(
            isKara ? "5d625a" : "696b60",
            "stone");
        var rebound = 0;
        foreach (var mesh in EnumerateSelfAndDescendants<MeshInstance3D>(copy))
        {
            if (mesh.Mesh is null)
            {
                continue;
            }

            var name = mesh.Name.ToString();
            var material = name.Contains("Trunk", System.StringComparison.Ordinal)
                || name.Contains("Root", System.StringComparison.Ordinal)
                || name.Contains("Stump", System.StringComparison.Ordinal)
                || name.Contains("Branch", System.StringComparison.Ordinal)
                ? trunk
                : name.Contains("Stone", System.StringComparison.Ordinal)
                    ? stone
                    : name.Contains("Moss", System.StringComparison.Ordinal)
                        ? groundAccent
                        : name.EndsWith("0", System.StringComparison.Ordinal)
                            ? foliageShadow
                            : name.EndsWith("2", System.StringComparison.Ordinal)
                                ? foliageLight
                                : foliage;
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                mesh.SetSurfaceOverrideMaterial(surface, material);
                rebound++;
            }
        }

        copy.SetMeta("presentationOnly", true);
        copy.SetMeta("visualOnly", true);
        copy.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        copy.SetMeta("regionalFoliageDepthBand", isKara ? "kara" : isZirat ? "zirat" : depth > 0.35f ? "mid" : "near");
        copy.SetMeta("regionalFoliageMaterialReboundCount", rebound);
    }

    private static bool HasMeshInSubtree(Node node) =>
        node is MeshInstance3D || EnumerateDescendants<MeshInstance3D>(node).Any();

    private static IEnumerable<T> EnumerateSelfAndDescendants<T>(Node root) where T : Node
    {
        if (root is T typed)
        {
            yield return typed;
        }

        foreach (var nested in EnumerateDescendants<T>(root))
        {
            yield return nested;
        }
    }

    private static string? VariantKey(string nodeName)
    {
        string[] families =
        {
            "WinterLightBirdCherry", "WinterLightSpruce", "WinterLightBirch",
            "WinterLightLinden", "WinterLightMaple", "WinterLightRowan",
            "WinterLightWillow",
            "WinterBirdCherry", "WinterSpruce", "WinterBirch", "WinterLinden",
            "WinterMaple", "WinterRowan", "WinterWillow",
            "GrassTuft", "FallenBranch", "MossStone", "Birch", "Pine",
            "Spruce", "Shrub", "Fern", "Sedge", "Stump"
        };
        foreach (var family in families)
        {
            if (!nodeName.StartsWith(family + "_", System.StringComparison.Ordinal))
            {
                continue;
            }

            var rest = nodeName[(family.Length + 1)..];
            var index = 0;
            while (index < rest.Length && char.IsDigit(rest[index]))
            {
                index++;
            }

            return index == 0 ? null : $"{family}_{rest[..index]}";
        }

        return null;
    }

    private static float DeterministicPhase(Vector2 position, float seed)
    {
        var value = Mathf.Sin(
            position.X * (12.9898f + seed * 0.17f)
            + position.Y * (78.233f + seed * 0.13f)) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private void BuildEnvironment()
    {
        _sun = new DirectionalLight3D
        {
            Name = "AgentBSun",
            LightColor = Color.FromHtml("c7d3d1"),
            LightEnergy = 1.04f,
            ShadowEnabled = true,
            ShadowOpacity = 0.30f,
            RotationDegrees = new Vector3(-48f, 32f, 0f)
        };
        AddChild(_sun);
        var skyMaterial = new ProceduralSkyMaterial
        {
            // Keep the rainy day cold and readable. A lower-value sky
            // gradient gives the near village a clear silhouette while
            // the directional/ambient fill keeps facades and wet ground
            // readable instead of flattening them into black cutouts.
            SkyTopColor = Color.FromHtml("3b5662"),
            SkyHorizonColor = Color.FromHtml("80999a"),
            GroundBottomColor = Color.FromHtml("1a2729"),
            GroundHorizonColor = Color.FromHtml("5b726f"),
            // Painterly overcast bands: soft, sparse, no volumetrics.
            SkyCover = CreatePainterlySkyCover(),
            SkyCoverModulate = new Color(0.93f, 0.95f, 0.96f, 0.52f)
        };
        var sky = new Sky
        {
            SkyMaterial = skyMaterial
        };
        var environment = new global::Godot.Environment
        {
            BackgroundMode = global::Godot.Environment.BGMode.Sky,
            Sky = sky,
            AmbientLightSource = global::Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.80f,
            TonemapMode = global::Godot.Environment.ToneMapper.Aces,
            TonemapExposure = 0.98f,
            FogEnabled = true,
            FogLightColor = Color.FromHtml("5f7477"),
            FogDensity = 0.0034f,
            FogHeight = 1.0f,
            FogHeightDensity = 0.048f,
            FogAerialPerspective = 0.54f,
            FogSkyAffect = 0.18f,
            FogSunScatter = 0.10f
        };
        _environmentResource = environment;
        _environment = new WorldEnvironment { Name = "AgentBEnvironment", Environment = environment };
        AddChild(_environment);
        SetMeta("atmosphereOwner", "AgentBExteriorWorld");
    }

    /// <summary>
    /// Winter snowfall replaces the former rain system: slow drifting flakes
    /// with a long lifetime, denser and wind-driven at the Kara edge. Same
    /// single weather owner, same toggle path (ART-010 unchanged).
    /// </summary>
    private void BuildSnow()
    {
        _rain = new CpuParticles3D
        {
            Name = "AgentBSnow",
            Emitting = true,
            Amount = 1300,
            Lifetime = 7.5,
            LocalCoords = true,
            Preprocess = 6.0,
            Mesh = CreateSnowflakeMesh(),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(20f, 0.8f, 20f),
            Direction = new Vector3(0.22f, -1f, 0.14f),
            Spread = 26f,
            Gravity = new Vector3(0f, -0.32f, 0f),
            InitialVelocityMin = 0.7f,
            InitialVelocityMax = 1.4f,
            Randomness = 0.55f,
            LifetimeRandomness = 0.5f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.7f
        };
        AddChild(_rain);
    }

    private void BuildKaraAccentLights()
    {
        var root = new Node3D { Name = "KaraAccentLights" };
        root.SetMeta("presentationOnly", true);
        root.SetMeta("visualOnly", true);
        root.SetMeta("lightingOwner", "AgentBExteriorWorld");
        root.SetMeta("routeScope", "kara_urman_night only; disabled in interiors");
        root.SetMeta(
            "lightingPolicy",
            "three retained presentation cues use near-zero energy; no visible point-light hotspots");
        root.SetMeta("collisionOwner", "none");
        root.SetMeta("navigationOwner", "none");
        root.SetMeta("interactionOwner", "none");
        root.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        AddChild(root);

        foreach (var (name, position, color, energy, range) in new[]
                {
                    ("KaraThresholdBounce", new Vector3(-4.2f, 1.35f, -96.0f), "71898d", 0.070f, 16.0f),
                    ("KaraRootBounce", new Vector3(4.4f, 1.55f, -104.0f), "667f82", 0.055f, 14.0f),
                    ("KaraGestureBounce", new Vector3(-2.8f, 1.80f, -117.0f), "71847c", 0.045f, 13.0f)
                })
        {
            var light = new OmniLight3D
            {
                Name = name,
                Position = position,
                LightColor = Color.FromHtml(color),
                LightEnergy = 0f,
                OmniRange = range,
                ShadowEnabled = false,
                Visible = false
            };
            light.SetMeta("presentationOnly", true);
            light.SetMeta("visualOnly", true);
            light.SetMeta("landmarkRole", "kara-night-value-separation");
            light.SetMeta("baseEnergy", energy);
            root.AddChild(light);
            _karaAccentLights.Add(light);
        }

        SetMeta("karaAccentLightCount", _karaAccentLights.Count);
        SetMeta("karaAccentLightPolicy", "restrained cool bounce cues are visible only for exterior Kara presentation; no point-light hotspots");
    }

    /// <summary>
    /// Builds the painterly overcast cover used by the procedural sky.
    /// Sampled on a cylinder so the bands wrap the horizon without a seam.
    /// A low-frequency mask gathers the fBm into 2-3 soft horizontal bands;
    /// the alpha stays soft so the cover reads as painted washes, not clouds
    /// with hard edges. Presentation-only: no lighting or gameplay owners.
    /// </summary>
    private static ImageTexture CreatePainterlySkyCover()
    {
        const int width = 512;
        const int height = 256;
        var bandNoise = new FastNoiseLite
        {
            Seed = 20260909,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 3,
            Frequency = 0.55f
        };
        var detailNoise = new FastNoiseLite
        {
            Seed = 4711,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 4,
            Frequency = 1.9f
        };

        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (var y = 0; y < height; y++)
        {
            // Vertical squash keeps the washes in a narrow, calm band set.
            var v = y / (float)height;
            for (var x = 0; x < width; x++)
            {
                var angle = (x / (float)width) * Mathf.Tau;
                var cx = Mathf.Cos(angle) * 0.7f;
                var cz = Mathf.Sin(angle) * 0.7f;
                var mask = bandNoise.GetNoise3D(cx, v * 5.2f, cz);
                var detail = detailNoise.GetNoise3D(cx * 1.4f, v * 8.0f, cz * 1.4f);
                var bands = Mathf.SmoothStep(-0.15f, 0.45f, mask);
                var wisps = Mathf.SmoothStep(0.02f, 0.62f, detail);
                var alpha = Mathf.Clamp(bands * Mathf.Lerp(0.35f, 1f, wisps), 0f, 0.82f);
                image.SetPixel(x, y, Color.Color8(255, 255, 255, (byte)(alpha * 255f)));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static QuadMesh CreateSnowflakeMesh()
    {
        // Soft round flake facing the camera; unshaded so it stays a light
        // value against dark winter geometry without any glow.
        var quad = new QuadMesh { Size = new Vector2(0.075f, 0.075f) };
        quad.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.93f, 0.95f, 0.98f, 0.62f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        return quad;
    }

    public override void _Process(double delta)
    {
        if (!_built)
        {
            return;
        }

        var camera = GetViewport()?.GetCamera3D();
        var focus = camera?.GlobalPosition ?? GlobalPosition;
        if (_rain is not null)
        {
            _rain.GlobalPosition = focus + new Vector3(0f, 6f, 0f);
            // Rain thickens toward the Kara-Urman forest edge while retaining
            // enough village particles for a readable near/mid/far weather layer.
            // Calm village snowfall thickens into a Kara-edge blizzard.
            var karaPhase = Mathf.Clamp((-70f - focus.Z) / 30f, 0f, 1f);
            _rain.Amount = (int)Mathf.Lerp(1300f, 2600f, karaPhase);
        }
    }

    private void ApplyAtmosphere(bool night)
    {
        if (_environment?.Environment is not { } env)
        {
            return;
        }

        if (night)
        {
            env.AmbientLightSource = global::Godot.Environment.AmbientSource.Color;
            env.AmbientLightColor = Color.FromHtml("718b92");
            env.AmbientLightEnergy = 0.94f;
            env.FogLightColor = Color.FromHtml("506a72");
            env.FogDensity = 0.0048f;
            env.FogHeightDensity = 0.075f;
            env.FogAerialPerspective = 0.58f;
            env.FogSkyAffect = 0.20f;
            env.FogSunScatter = 0.10f;
            env.TonemapExposure = 1.06f;
        }
        else
        {
            env.AmbientLightSource = global::Godot.Environment.AmbientSource.Sky;
            env.AmbientLightEnergy = 0.80f;
            env.FogLightColor = Color.FromHtml("5f7477");
            env.FogDensity = 0.0034f;
            env.FogHeight = 1.0f;
            env.FogHeightDensity = 0.048f;
            env.FogAerialPerspective = 0.54f;
            env.FogSkyAffect = 0.18f;
            env.FogSunScatter = 0.10f;
            env.TonemapExposure = 0.98f;
        }
        if (env.Sky?.SkyMaterial is ProceduralSkyMaterial procedural)
        {
            procedural.SkyTopColor = Color.FromHtml(night ? "162a35" : "3b5662");
            procedural.SkyHorizonColor = Color.FromHtml(night ? "506a72" : "80999a");
            procedural.GroundHorizonColor = Color.FromHtml(night ? "3c5558" : "5b726f");
            procedural.GroundBottomColor = Color.FromHtml(night ? "132021" : "1a2729");
        }

        if (_sun is not null)
        {
            _sun.LightColor = Color.FromHtml(night ? "7f96a0" : "c7d3d1");
            _sun.LightEnergy = night ? 0.86f : 1.04f;
            _sun.ShadowOpacity = night ? 0.20f : 0.30f;
            _sun.RotationDegrees = night
                ? new Vector3(-52f, -28f, 0f)
                : new Vector3(-48f, 32f, 0f);
            _sun.ShadowEnabled = true;
        }

        foreach (var light in _karaAccentLights)
        {
            var baseEnergy = light.GetMeta("baseEnergy").AsSingle();
            light.LightEnergy = night ? baseEnergy : 0f;
            light.Visible = _exteriorPresentationEnabled && night;
        }
    }

    private static IEnumerable<T> EnumerateDescendants<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var nested in EnumerateDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }
}
