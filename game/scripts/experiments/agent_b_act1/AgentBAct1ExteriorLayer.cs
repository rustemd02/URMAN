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
                RegradeKaraGesture(instance);
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
        BuildRain();
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
        var prefixes = new[] { "HouseA1_", "HouseA2_", "HouseA3_", "HouseA4_" };
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
        var families = new[]
        {
            (Prefix: "Zirat_FenceS_", MetaName: "suppressedZiratFenceSouthMeshCount"),
            (Prefix: "Zirat_FenceN_", MetaName: "suppressedZiratFenceNorthMeshCount")
        };
        var total = 0;
        foreach (var (prefix, metaName) in families)
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
                    "south/north zirat fence rails span the authoritative route; west/east side fences and the open gate remain visible");
            }

            ziratKit.SetMeta(metaName, meshes.Length);
            total += meshes.Length;
        }

        ziratKit.SetMeta("suppressedZiratRouteFenceFamilies", "Zirat_FenceS_|Zirat_FenceN_");
        ziratKit.SetMeta("suppressedZiratRouteFenceMeshCount", total);
    }

    private static void SuppressRouteOccludingKaraMasses(Node3D karaKit)
    {
        // These preview-board families are route-scale occluders; the authored
        // Kara edge composition owns the visible banks and trees.
        var meshes = EnumerateDescendants<MeshInstance3D>(karaKit)
            .Where(mesh =>
                mesh.Name.ToString().StartsWith("KaraMass_", System.StringComparison.Ordinal)
                || mesh.Name.ToString().StartsWith("KaraTrunk_", System.StringComparison.Ordinal)
                || mesh.Name.ToString().StartsWith("CliffRing_", System.StringComparison.Ordinal)
                || mesh.Name.ToString().StartsWith("KaraGesture_", System.StringComparison.Ordinal))
            .ToArray();
        if (meshes.Length == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B Kara kit is missing the declared preview forest families; cannot clear route-scale forest occluders.");
        }

        foreach (var mesh in meshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta("suppressionReason", "Kara preview-board forest families occlude the authored Kara first-person envelope");
        }

        karaKit.SetMeta("suppressedPresentationFamilies", "KaraMass_*|KaraTrunk_*|CliffRing_*|KaraGesture_*");
        karaKit.SetMeta("suppressedKaraPreviewMeshCount", meshes.Length);

        // A/B owner proof: these exact Agent B meshes are the repeated brown
        // faceted boulders occupying the lower foreground of the Kara approach
        // cameras. Hide their draw only; keep the existing collision owner and
        // route envelope unchanged.
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
    }

    private static void RegradeKaraGesture(Node3D karaKit)
    {
        var gestureMaterial = Urman.Godot.PainterlyMaterialLibrary.ForColor("4d5548", "wood_bark");
        var rebound = 0;
        foreach (var mesh in EnumerateDescendants<MeshInstance3D>(karaKit)
                     .Where(mesh => mesh.Name.ToString().StartsWith("KaraGesture_", System.StringComparison.Ordinal)))
        {
            if (mesh.Mesh is null)
            {
                continue;
            }

            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                mesh.SetSurfaceOverrideMaterial(surface, gestureMaterial);
                rebound++;
            }
        }

        if (rebound == 0)
        {
            throw new System.InvalidOperationException(
                "Agent B Kara kit is missing the declared KaraGesture_* silhouette family.");
        }

        karaKit.SetMeta("karaGestureMaterialGrade", "dark marsh bark preserves the authored mystic silhouette without black preview contrast");
        karaKit.SetMeta("karaGestureMaterialReboundCount", rebound);
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

        var plantedEntryCount = 0;
        var minimumRoadClearance = float.MaxValue;
        var invalidPlacements = new List<string>();
        foreach (var (position, variant) in AgentBFoliagePlan.Entries)
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
            // hiding only repeated low silhouettes in the village/zirat day
            // envelope. The current plan's X-shaped accents are GrassTuft
            // entries; FallenBranch remains covered if a future plan restores
            // that family. Kara and its authored boulders stay untouched.
            var thinRepeatedGroundAccent = position.Y > -86f
                && (variant.StartsWith("FallenBranch_", System.StringComparison.Ordinal)
                    || variant.StartsWith("GrassTuft_", System.StringComparison.Ordinal)
                    || variant.StartsWith("MossStone_", System.StringComparison.Ordinal))
                && DeterministicPhase(position, 17.3f) < 0.75f;

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

                plants.AddChild(copy);
                copy.Visible = !thinRepeatedGroundAccent;
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
                    sourcePosition.Y,
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
        SetMeta("plannedFoliageEntryCount", AgentBFoliagePlan.Entries.Count);
        SetMeta("plantedFoliageEntryCount", plantedEntryCount);
        SetMeta("plantedFoliageNodeCount", plants.GetChildCount());
        SetMeta("minimumFoliageRoadClearance", minimumRoadClearance);
        SetMeta(
            "foliagePlacementPolicy",
            "all planted entries fail closed at >=0.25m from the authored road envelope; foliage remains presentation-only");
        SetMeta(
            "foliageRebasePolicy",
            "planted copies remove source preview-board X/Z offsets from mesh geometry while preserving vertical ground pivots");
        SetMeta(
            "regionalFoliageGradePolicy",
            "all planted foliage uses deterministic near/mid/far values; Kara broadens and darkens existing families without adding entries");
    }

    private static void RegradeRegionalFoliage(Node3D copy, string variant, float routeZ = 0f)
    {
        var isBirch = variant.StartsWith("Birch_", System.StringComparison.Ordinal);
        var isConifer = variant.StartsWith("Pine_", System.StringComparison.Ordinal)
            || variant.StartsWith("Spruce_", System.StringComparison.Ordinal);
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
        var foliage = Urman.Godot.PainterlyMaterialLibrary.ForColor(foliageColor, "foliage");
        var foliageShadow = Urman.Godot.PainterlyMaterialLibrary.ForColor(
            isKara ? "2f4138" : isZirat ? "465640" : "506047",
            "foliage");
        var foliageLight = Urman.Godot.PainterlyMaterialLibrary.ForColor(
            isKara ? "425440" : isZirat ? "5b694e" : "687857",
            "foliage");
        var trunk = Urman.Godot.PainterlyMaterialLibrary.ForColor(
            isKara
                ? isBirch ? "5b5b50" : "48443c"
                : isBirch ? "625f54" : "51483c",
            "wood_bark");
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
            LightColor = Color.FromHtml("c5d0d2"),
            LightEnergy = 1.08f,
            ShadowEnabled = true,
            ShadowOpacity = 0.40f,
            RotationDegrees = new Vector3(-48f, 32f, 0f)
        };
        AddChild(_sun);
        var sky = new Sky
        {
            SkyMaterial = new ProceduralSkyMaterial
            {
                // Keep the rainy day cold and readable. A lower-value sky
                // gradient gives the near village a clear silhouette while
                // the directional/ambient fill keeps facades and wet ground
                // readable instead of flattening them into black cutouts.
                SkyTopColor = Color.FromHtml("3f5662"),
                SkyHorizonColor = Color.FromHtml("819194"),
                GroundBottomColor = Color.FromHtml("202a2c"),
                GroundHorizonColor = Color.FromHtml("53686c")
            }
        };
        var environment = new global::Godot.Environment
        {
            BackgroundMode = global::Godot.Environment.BGMode.Sky,
            Sky = sky,
            AmbientLightSource = global::Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.76f,
            TonemapMode = global::Godot.Environment.ToneMapper.Aces,
            TonemapExposure = 0.94f,
            FogEnabled = true,
            FogLightColor = Color.FromHtml("60737a"),
            FogDensity = 0.0038f,
            FogHeight = 0.8f,
            FogHeightDensity = 0.065f,
            FogAerialPerspective = 0.38f,
            FogSkyAffect = 0.34f,
            FogSunScatter = 0.08f
        };
        _environmentResource = environment;
        _environment = new WorldEnvironment { Name = "AgentBEnvironment", Environment = environment };
        AddChild(_environment);
        SetMeta("atmosphereOwner", "AgentBExteriorWorld");
    }

    private void BuildRain()
    {
        _rain = new CpuParticles3D
        {
            Name = "AgentBRain",
            Emitting = true,
            Amount = 1700,
            Lifetime = 1.15,
            LocalCoords = true,
            Preprocess = 2.0,
            Mesh = CreateRainMesh(),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(16f, 0.5f, 16f),
            Direction = new Vector3(0.12f, -1f, 0.08f),
            Spread = 6f,
            Gravity = new Vector3(0f, -6f, 0f),
            InitialVelocityMin = 10f,
            InitialVelocityMax = 13f
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
            "three low-energy cool bounce cues reveal roots, gesture branch and road edge without a horror spotlight");
        root.SetMeta("collisionOwner", "none");
        root.SetMeta("navigationOwner", "none");
        root.SetMeta("interactionOwner", "none");
        root.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        AddChild(root);

        foreach (var (name, position, color, energy, range) in new[]
                {
                    ("KaraThresholdBounce", new Vector3(-4.2f, 1.35f, -96.0f), "71898d", 1.15f, 13.0f),
                    ("KaraRootBounce", new Vector3(4.4f, 1.55f, -104.0f), "667f82", 0.95f, 11.0f),
                    ("KaraGestureBounce", new Vector3(-2.8f, 1.80f, -117.0f), "71847c", 0.72f, 10.5f)
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
        SetMeta("karaAccentLightPolicy", "cool low-energy lights are visible only for exterior Kara presentation");
    }

    private static QuadMesh CreateRainMesh()
    {
        var quad = new QuadMesh { Size = new Vector2(0.022f, 0.66f) };
        quad.Material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.64f, 0.72f, 0.77f, 0.42f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
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
            // Rain thickens toward the Kara-Urman forest edge: village keeps a
            // light drizzle, the approach doubles the particle budget.
            var karaPhase = Mathf.Clamp((-70f - focus.Z) / 30f, 0f, 1f);
            _rain.Amount = (int)Mathf.Lerp(1700f, 2400f, karaPhase);
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
            env.AmbientLightColor = Color.FromHtml("7b9096");
            env.AmbientLightEnergy = 0.90f;
            env.FogLightColor = Color.FromHtml("5a6f76");
            env.FogDensity = 0.0038f;
            env.FogHeightDensity = 0.075f;
            env.TonemapExposure = 1.08f;
        }
        else
        {
            env.AmbientLightSource = global::Godot.Environment.AmbientSource.Sky;
            env.AmbientLightEnergy = 0.76f;
            env.FogLightColor = Color.FromHtml("60737a");
            env.FogDensity = 0.0038f;
            env.FogHeightDensity = 0.065f;
            env.FogAerialPerspective = 0.38f;
            env.FogSkyAffect = 0.34f;
            env.FogSunScatter = 0.08f;
            env.TonemapExposure = 0.94f;
        }
        if (env.Sky?.SkyMaterial is ProceduralSkyMaterial procedural)
        {
            procedural.SkyTopColor = Color.FromHtml(night ? "172631" : "3f5662");
            procedural.SkyHorizonColor = Color.FromHtml(night ? "63727a" : "819194");
            procedural.GroundHorizonColor = Color.FromHtml(night ? "47595a" : "53686c");
            procedural.GroundBottomColor = Color.FromHtml(night ? "182221" : "202a2c");
        }

        if (_sun is not null)
        {
            _sun.LightColor = Color.FromHtml(night ? "859aa1" : "c5d0d2");
            _sun.LightEnergy = night ? 0.90f : 1.08f;
            _sun.ShadowOpacity = night ? 0.24f : 0.40f;
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
