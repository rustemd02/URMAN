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
    private readonly Dictionary<(string Variant, string Region), ArrayMesh> _foliageMeshes = new();
    internal static Aabb[] BuildingRoofBounds(Node root) => EnumerateDescendants<MeshInstance3D>(root)
        .Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null
            && mesh.Name.ToString().Contains("Roof", StringComparison.OrdinalIgnoreCase))
        .Select(mesh => mesh.GlobalTransform * mesh.Mesh.GetAabb())
        .Where(bounds => bounds.Size.X > 1 && bounds.Size.Z > 1).ToArray();

    internal static bool UnderBuildingRoof(Vector3 root, Aabb[] roofs) => roofs.Any(bounds =>
        root.X >= bounds.Position.X - .35f && root.X <= bounds.End.X + .35f
        && root.Z >= bounds.Position.Z - .35f && root.Z <= bounds.End.Z + .35f
        && bounds.End.Y > root.Y + .4f);

    internal ArrayMesh FoliageMesh(string variant, string region)
    {
        if (_foliageMeshes.TryGetValue((variant, region), out var cached)) return cached;
        var pine = variant is "WinterPine" or "WinterLightPine" or "WinterFarPine";
        if (!pine && variant is not ("WinterDeadTree" or "WinterLightDeadTree" or "WinterFarDeadTree"))
            throw new InvalidOperationException($"Missing winter foliage geometry: {variant}");
        var source = ResourceLoader.Load<PackedScene>(pine ? "res://assets/models/act1/urman_winter_pine.glb" : "res://assets/models/act1/urman_winter_dead_tree.glb").Instantiate<Node3D>();
        try
        {
            var names = pine ? new[] { "WinterPine", "WinterLightPine", "WinterFarPine" }
                : new[] { "WinterDeadTree", "WinterLightDeadTree", "WinterFarDeadTree" };
            for (var lod = 0; lod < names.Length; lod++)
            {
                var template = EnumerateDescendants<MeshInstance3D>(source).Single(node => node.Name == $"{(pine ? "WinterPine" : "WinterDeadTree")}_LOD{lod}");
                var mesh = (ArrayMesh)template.Mesh!.Duplicate();
                for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
                {
                    var imported = mesh.SurfaceGetMaterial(surface) as StandardMaterial3D;
                    if (pine && (imported is null || imported.ResourceName is not ("AB_bark" or "AB_needles")))
                        throw new InvalidOperationException("Pine material names must remain AB_bark and AB_needles after import.");
                    if (pine && imported!.ResourceName == "AB_needles")
                    {
                        var alpha = imported.AlbedoTexture ?? throw new InvalidOperationException("Pine needles are missing their alpha texture.");
                        mesh.SurfaceSetMaterial(surface, PainterlyMaterialLibrary.ForCutout("455749", alpha, "foliage"));
                    }
                    else mesh.SurfaceSetMaterial(surface, RegionalFoliageMaterial(variant, "bark", region));
                }
                _foliageMeshes[(names[lod], region)] = mesh;
            }
        }
        finally { source.Free(); }
        return _foliageMeshes[(variant, region)];
    }
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
        ConformRoadPresentation();
        // Settle the retained banks into the ground before creating their
        // collision, so the visible low profile and physical boundary agree.
        foreach (var mesh in EnumerateDescendants<MeshInstance3D>(GetNode<Node3D>("AgentB_KaraEdgeKit")).ToArray())
        {
            var name = mesh.Name.ToString();
            if (!name.StartsWith("KaraRoot_") && !name.StartsWith("FallenLog_K") && !name.StartsWith("KaraStone_")) continue;
            var source = mesh.Mesh;
            if (source is null) continue;
            if (name.StartsWith("FallenLog_K"))
            {
                // Deadfall rests on two tapered broken branches. Their feet
                // explain the raised trunk without turning it into a solid wall.
                var points = source.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array()
                    .Select(vertex => mesh.ToGlobal(vertex)).OrderBy(point => point.X).ToArray();
                foreach (var t in new[] { .2f, .8f })
                {
                    var top = points.First().Lerp(points.Last(), t);
                    var bottom = new Vector3(top.X + (t - .5f) * .45f,
                        AgentBAct1HeightField.CollisionGround(top.X, top.Z) - .04f, top.Z + (t < .5f ? -.34f : .27f));
                    var support = new MeshInstance3D { Name = "DeadfallBrokenBranch",
                        Mesh = new CylinderMesh { Height = top.DistanceTo(bottom), TopRadius = .045f, BottomRadius = .11f,
                            RadialSegments = 7, Rings = 1 },
                        MaterialOverride = PainterlyMaterialLibrary.ForColor("655b51", "bark_pine") };
                    AddChild(support);
                    support.GlobalTransform = new Transform3D(new Basis(new Quaternion(Vector3.Up,
                        (top - bottom).Normalized())), (top + bottom) * .5f);
                }
                mesh.MaterialOverride = PainterlyMaterialLibrary.ForColor("655b51", "bark_pine");
                continue;
            }
            var reshaped = new ArrayMesh();
            for (var i = 0; i < source.GetSurfaceCount(); i++)
            {
                var arrays = source.SurfaceGetArrays(i);
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var bounds = mesh.GlobalTransform * source.GetAabb();
                var center = bounds.GetCenter();
                for (var v = 0; v < vertices.Length; v++)
                {
                    var point = mesh.ToGlobal(vertices[v]);
                    var level = Mathf.Clamp((point.Y - bounds.Position.Y) / Mathf.Max(bounds.Size.Y, .01f), 0f, 1f);
                    var shoulder = .95f + .20f * Mathf.Sin(level * Mathf.Pi);
                    point.X = center.X + (point.X - center.X) * shoulder;
                    point.Z = center.Z + (point.Z - center.Z) * shoulder;
                    point.Y = AgentBAct1HeightField.CollisionGround(point.X, point.Z)
                        + Mathf.Lerp(-.60f, name.StartsWith("KaraRoot_") ? .80f : .65f, level);
                    vertices[v] = mesh.ToLocal(point);
                }
                arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                using var surfaceMesh = new ArrayMesh();
                surfaceMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                using var surface = new SurfaceTool();
                surface.CreateFrom(surfaceMesh, 0);
                surface.GenerateNormals();
                surface.SetMaterial(source.SurfaceGetMaterial(i));
                surface.Commit(reshaped);
            }
            mesh.Mesh = reshaped;
            mesh.MaterialOverride = PainterlyMaterialLibrary.ForColor("60685d", "stone");
        }
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

    private void ConformRoadPresentation()
    {
        var kit = GetNode<Node3D>("AgentB_TerrainRoadKit");
        foreach (var mesh in EnumerateDescendants<MeshInstance3D>(kit))
        {
            var name = mesh.Name.ToString();
            if (mesh.Mesh is not ArrayMesh original || (name != "Terrain_Main" && !name.StartsWith("Road_", StringComparison.Ordinal))) continue;
            var result = new ArrayMesh();
            if (name == "Terrain_Main")
            {
                var surface = new SurfaceTool();
                surface.Begin(Mesh.PrimitiveType.Triangles);
                foreach (var vertex in AgentBAct1HeightField.BuildTerrainFaces())
                {
                    surface.SetUV(new Vector2(vertex.X, vertex.Z));
                    surface.AddVertex(mesh.ToLocal(ToGlobal(vertex)));
                }
                surface.Index();
                surface.GenerateNormals();
                surface.Commit(result);
                result.SurfaceSetMaterial(0, original.SurfaceGetMaterial(0));
            }
            else
            {
                for (var index = 0; index < original.GetSurfaceCount(); index++)
                {
                    var arrays = original.SurfaceGetArrays(index).Duplicate(true);
                    var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        var point = ToLocal(mesh.ToGlobal(vertices[i]));
                        point.Y += AgentBAct1HeightField.CollisionGround(point.X, point.Z)
                            - (float)AgentBAct1HeightField.Ground(point.X, point.Z);
                        vertices[i] = mesh.ToLocal(ToGlobal(point));
                    }
                    arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                    var reshaped = new ArrayMesh();
                    reshaped.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                    var surface = new SurfaceTool();
                    surface.CreateFrom(reshaped, 0);
                    surface.GenerateNormals();
                    surface.Commit(result);
                    result.SurfaceSetMaterial(index, original.SurfaceGetMaterial(index));
                }
            }
            mesh.Mesh = result;
            mesh.SetMeta("supportOwner", "AgentB_TerrainCollision");
        }
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

        // Root banks, fallen logs and stones retain physical trimeshes.
        // Keep their matching authored surfaces visible so the player can
        // read the obstacle; presentation suppression must not hide physics.

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
        var kit = GetNode<Node3D>("AgentB_FoliageKit");
        var parts = EnumerateDescendants<Node3D>(kit)
            .Where(node => VariantKey(node.Name.ToString()) is not null && HasMeshInSubtree(node))
            .GroupBy(node => VariantKey(node.Name.ToString())!)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var geometry = new Dictionary<string, (ArrayMesh Mesh, string[] Kinds, Vector3[] LowVertices)>(StringComparer.Ordinal);
        var graded = _foliageMeshes;
        (ArrayMesh Mesh, string[] Kinds, Vector3[] LowVertices) Geometry(string variant)
        {
            if (geometry.TryGetValue(variant, out var cached)) return cached;
            if (!parts.TryGetValue(variant, out var sources))
                throw new InvalidOperationException($"Missing winter foliage geometry: {variant}");
            var meshes = sources.SelectMany(EnumerateSelfAndDescendants<MeshInstance3D>).Distinct()
                .Where(mesh => mesh.Mesh is ArrayMesh).ToArray();
            var sourceVertices = meshes.SelectMany(mesh => Enumerable.Range(0, mesh.Mesh!.GetSurfaceCount())
                .SelectMany(surface => mesh.Mesh!.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                .Select(vertex => ToLocal(mesh.ToGlobal(vertex)))).ToArray();
            Vector3 pivot;
            if (variant.StartsWith("Winter", StringComparison.Ordinal))
            {
                var root = sources.SingleOrDefault(node => node.Name == variant + "_Root")
                    ?? throw new InvalidOperationException($"Winter variant lacks native root pivot: {variant}");
                pivot = ToLocal(root.GlobalPosition);
            }
            else
            {
                var trunk = FindTrunkBase(sources, variant);
                pivot = trunk.HasValue ? new Vector3(trunk.Value.PivotXZ.X, trunk.Value.BaseY, trunk.Value.PivotXZ.Y)
                    : new Vector3(sourceVertices.Average(vertex => vertex.X), sourceVertices.Min(vertex => vertex.Y), sourceVertices.Average(vertex => vertex.Z));
            }
            var groups = new Dictionary<string, List<(MeshInstance3D Mesh, int Surface)>>(StringComparer.Ordinal);
            foreach (var mesh in meshes)
            for (var surface = 0; surface < mesh.Mesh!.GetSurfaceCount(); surface++)
            {
                var name = mesh.Name.ToString();
                var materialName = mesh.Mesh.SurfaceGetMaterial(surface)?.ResourceName ?? "";
                var kind = name.Contains("Snow", StringComparison.OrdinalIgnoreCase) || materialName.Contains("snow", StringComparison.OrdinalIgnoreCase) ? "snow"
                    : name.Contains("Berries", StringComparison.OrdinalIgnoreCase) ? "berries"
                    : new[] { "Trunk", "Branch", "Twig", "Root", "Stump" }.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase)) ? "bark"
                    : name.Contains("Stone", StringComparison.OrdinalIgnoreCase) ? "stone" : "foliage";
                if (!groups.TryGetValue(kind, out var group)) groups[kind] = group = new();
                group.Add((mesh, surface));
            }
            var result = new ArrayMesh();
            foreach (var group in groups.Values)
            {
                using var surface = new SurfaceTool();
                surface.Begin(Mesh.PrimitiveType.Triangles);
                foreach (var part in group)
                {
                    var transform = GlobalTransform.AffineInverse() * part.Mesh.GlobalTransform;
                    transform.Origin -= pivot;
                    surface.AppendFrom(part.Mesh.Mesh!, part.Surface, transform);
                }
                surface.Index();
                surface.Commit(result);
            }
            if (variant.StartsWith("Winter", StringComparison.Ordinal) && !result.GetAabb().Grow(.02f).HasPoint(Vector3.Zero))
                throw new InvalidOperationException($"Native winter root is outside its rebased geometry: {variant}");
            cached = (result, groups.Keys.ToArray(), sourceVertices.Select(vertex => vertex - pivot).Where(vertex => vertex.Y < 3.2f).ToArray());
            geometry[variant] = cached;
            return cached;
        }
        ArrayMesh RegionalMesh(string variant, string region)
        {
            if (graded.TryGetValue((variant, region), out var cached)) return cached;
            var source = Geometry(variant);
            var result = (ArrayMesh)source.Mesh.Duplicate();
            for (var surface = 0; surface < source.Kinds.Length; surface++)
                result.SurfaceSetMaterial(surface, RegionalFoliageMaterial(variant, source.Kinds[surface], region));
            graded[(variant, region)] = result;
            return result;
        }

        var plants = new Node3D { Name = "AgentB_PlantedFoliage" };
        AddChild(plants);
        plants.SetMeta("presentationOnly", true);
        plants.SetMeta("visualOnly", true);
        plants.SetMeta("variationPolicy", "shared rooted meshes; native distance LOD; 12m ground-cover cells");
        kit.Visible = false;
        kit.SetMeta("templateSourceHidden", true);
        kit.SetMeta("templateSourcePolicy", "hidden source library; shared geometry is rebased once per variant");
        var batches = new Dictionary<(Vector2I Cell, string Variant, string Region, int Lod), List<Transform3D>>();
        var roofs = BuildingRoofBounds(GetParent());
        var plan = BuildDensifiedPlan();
        var suppressed = 0;
        var suppressedKara = 0;
        var minimumClearance = float.MaxValue;
        foreach (var (position, sourceVariant) in plan)
        {
            var smallShrub = sourceVariant.StartsWith("Shrub_", StringComparison.Ordinal);
            var groundCover = smallShrub || sourceVariant.StartsWith("Fern_", StringComparison.Ordinal)
                || sourceVariant.StartsWith("Sedge_", StringComparison.Ordinal) || sourceVariant.StartsWith("GrassTuft_", StringComparison.Ordinal);
            var variant = sourceVariant.Replace("WinterLight", "Winter", StringComparison.Ordinal);
            if (smallShrub) variant = "WinterBirdCherry_1";
            if (variant.StartsWith("Birch_", StringComparison.Ordinal)) variant = "WinterBirch_1";
            if (variant.Contains("Spruce", StringComparison.Ordinal) || variant.StartsWith("Pine_", StringComparison.Ordinal))
                variant = position.Y <= -86f ? variant.StartsWith("WinterSpruce_", StringComparison.Ordinal) ? variant : "WinterSpruce_1" : "WinterLinden_1";
            var template = Geometry(variant);
            var road = AgentBAct1HeightField.RoadInfo(position.X, position.Y);
            var clearance = (float)(road.Distance - road.HalfWidth);
            if (clearance < .25f) throw new InvalidOperationException($"Foliage placement enters road: {variant}@{position}");
            minimumClearance = Mathf.Min(minimumClearance, clearance);
            var depth = Mathf.Clamp((-position.Y - 8f) / 118f, 0, 1);
            var horizontal = Mathf.Lerp(1.04f, .90f, depth) * Mathf.Lerp(.92f, 1.08f, DeterministicPhase(position, 2.7f));
            var vertical = Mathf.Lerp(1.02f, .90f, depth) * Mathf.Lerp(.93f, 1.07f, DeterministicPhase(position, 4.9f));
            if (smallShrub) { horizontal *= .38f; vertical *= .35f; }
            if (sourceVariant.StartsWith("Sedge_", StringComparison.Ordinal))
            {
                horizontal *= .62f;
                vertical *= .78f;
            }
            var basis = new Basis(Vector3.Up, Mathf.DegToRad(Mathf.Lerp(-14, 14, DeterministicPhase(position, 8.1f))))
                .Scaled(new Vector3(horizontal, vertical, horizontal));
            var target = new Vector3(position.X, AgentBAct1HeightField.CollisionGround(position.X, position.Y) - .04f, position.Y);
            var karaSuppression = position.Y <= -86f && new[] { "Birch_", "Spruce_", "MossStone_", "Stump_" }
                .Any(prefix => sourceVariant.StartsWith(prefix, StringComparison.Ordinal));
            var hidden = karaSuppression || position.Y > -86f && DeterministicPhase(position, 17.3f) < .75f
                && new[] { "FallenBranch_", "GrassTuft_", "MossStone_" }.Any(prefix => sourceVariant.StartsWith(prefix, StringComparison.Ordinal));
            if (karaSuppression) suppressedKara++;
            var worldRoot = ToGlobal(target);
            hidden |= UnderBuildingRoof(worldRoot, roofs);
            if (!hidden)
            foreach (var sample in template.LowVertices)
            {
                var point = target + basis * sample;
                if (point.Y - target.Y > 2.6f) continue;
                var edge = AgentBAct1HeightField.RoadInfo(point.X, point.Z);
                if (edge.Distance - edge.HalfWidth >= .1) continue;
                hidden = true; break;
            }
            if (hidden) { suppressed++; continue; }
            var region = position.Y <= -86f ? "kara" : position.Y <= -58f ? "zirat" : "village";
            var hasLods = variant.StartsWith("Winter", StringComparison.Ordinal);
            var tiers = hasLods ? new[] { variant, variant.Replace("Winter", "WinterLight", StringComparison.Ordinal), variant.Replace("Winter", "WinterFar", StringComparison.Ordinal) }
                : new[] { variant };
            var tree = groundCover ? null : new Node3D { Name = $"{variant}_Plant{plants.GetChildCount()}", Position = target, Basis = basis };
            if (tree is not null)
            {
                plants.AddChild(tree);
                tree.SetMeta("presentationOnly", true);
                tree.SetMeta("plantVariant", variant);
                tree.SetMeta("plantPosition", target);
            }
            for (var lod = 0; lod < tiers.Length; lod++)
            {
                var mesh = RegionalMesh(tiers[lod], region);
                if (groundCover)
                {
                    var cell = new Vector2I(Mathf.FloorToInt(position.X / 12f), Mathf.FloorToInt(position.Y / 12f));
                    var key = (cell, tiers[lod], region, hasLods ? lod : -1);
                    if (!batches.TryGetValue(key, out var instances)) batches[key] = instances = new();
                    instances.Add(new Transform3D(basis, target));
                }
                else
                {
                    var instance = new MeshInstance3D { Name = $"{variant}_LOD{lod}", Mesh = mesh };
                    tree!.AddChild(instance);
                    ConfigureFoliageRange(instance, hasLods ? lod : -1, false);
                }
            }
        }
        foreach (var (key, transforms) in batches)
        {
            var origin = new Vector3(key.Cell.X * 12f + 6f, 0, key.Cell.Y * 12f + 6f);
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = RegionalMesh(key.Variant, key.Region), InstanceCount = transforms.Count };
            for (var index = 0; index < transforms.Count; index++)
            {
                var transform = transforms[index]; transform.Origin -= origin;
                multi.SetInstanceTransform(index, transform);
            }
            var group = new MultiMeshInstance3D { Name = $"{key.Variant}_Cell{key.Cell.X}_{key.Cell.Y}", Position = origin, Multimesh = multi };
            plants.AddChild(group);
            group.SetMeta("presentationOnly", true);
            group.SetMeta("plantVariant", key.Variant);
            ConfigureFoliageRange(group, key.Lod, true);
        }
        foreach (var region in new[] { "village", "zirat", "kara" })
        foreach (var variant in new[] { "WinterBirch_1", "WinterLinden_1", "WinterBirdCherry_1",
                     "WinterLightBirch_1", "WinterLightLinden_1", "WinterLightBirdCherry_1",
                     "WinterFarBirch_1", "WinterFarLinden_1", "WinterFarBirdCherry_1",
                     "WinterSpruce_1", "WinterLightSpruce_1", "WinterFarSpruce_1" })
            RegionalMesh(variant, region);
        SetMeta("roadEnvelopeSuppressedFoliageEntryCount", suppressed);
        SetMeta("plantedVariantCount", parts.Count);
        SetMeta("plannedFoliageEntryCount", plan.Count);
        SetMeta("plantedFoliageEntryCount", plan.Count);
        SetMeta("plantedFoliageNodeCount", plants.GetChildCount());
        SetMeta("suppressedKaraFoliageEntryCount", suppressedKara);
        SetMeta("minimumFoliageRoadClearance", minimumClearance);
        SetMeta("foliagePlacementPolicy", "roots and full lower silhouette clear road; suppressed entries remain counted separately");
        SetMeta("foliageRebasePolicy", "one layer-space root pivot per shared geometry variant");
        SetMeta("regionalFoliageGradePolicy", "semantic snow/bark/dry ground cover; winter deciduous village, conifers at Kara only");
        foreach (var template in kit.GetChildren()) template.Free();
        kit.SetMeta("templateSourcePolicy", "source owner retained; template instances released after shared geometry extraction");
    }

    internal static void ConfigureFoliageRange(GeometryInstance3D instance, int lod, bool groundCover)
    {
        instance.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        instance.VisibilityRangeBegin = lod switch { 1 => 24, 2 => 60, _ => 0 };
        instance.VisibilityRangeBeginMargin = lod switch { 1 => 2, 2 => 4, _ => 0 };
        instance.VisibilityRangeEnd = lod switch { 0 => 26, 1 => 64, _ => groundCover ? 85 : 0 };
        instance.VisibilityRangeEndMargin = lod switch { 0 => 2, 1 => 4, _ => groundCover ? 5 : 0 };
    }

    private static Material RegionalFoliageMaterial(string variant, string kind, string region)
    {
        var birch = variant.Contains("Birch", StringComparison.Ordinal);
        var conifer = variant.Contains("Spruce", StringComparison.Ordinal);
        return kind switch
        {
            "snow" => PainterlyMaterialLibrary.ForColor("e8edf0", "snow_roof"),
            "berries" => PainterlyMaterialLibrary.ForColor("784239", "rowan_berries"),
            "bark" => PainterlyMaterialLibrary.ForColor(birch ? "c9c2ad" : !conifer ? "9b9487" : region == "kara" ? "504c43" : "685e50", birch ? "bark_birch_winter" : "wood_bark"),
            "stone" => PainterlyMaterialLibrary.ForColor("74766d", "stone"),
            _ => PainterlyMaterialLibrary.ForColor(conifer ? "455749" : "827a65", conifer ? "foliage" : "grass")
        };
    }

    private (Vector2 PivotXZ, float BaseY)? FindTrunkBase(List<Node3D> parts, string variant)
    {
        var trunkMeshes = parts
            .SelectMany(EnumerateSelfAndDescendants<MeshInstance3D>)
            .Where(mesh => mesh.Name.ToString().EndsWith("_Trunk", System.StringComparison.Ordinal))
            .Distinct()
            .ToArray();
        if (trunkMeshes.Length == 0)
        {
            return null;
        }

        var worldVertices = new List<Vector3>();
        foreach (var mesh in trunkMeshes)
        {
            if (mesh.Mesh is not ArrayMesh sourceArrayMesh)
            {
                throw new InvalidOperationException(
                    $"Foliage family '{variant}' requires ArrayMesh trunk geometry.");
            }

            for (var surface = 0; surface < sourceArrayMesh.GetSurfaceCount(); surface++)
            {
                var vertices = sourceArrayMesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                foreach (var vertex in vertices)
                {
                    worldVertices.Add(ToLocal(mesh.ToGlobal(vertex)));
                }
            }
        }

        if (worldVertices.Count == 0)
        {
            throw new InvalidOperationException(
                $"Foliage family '{variant}' declares a trunk without vertices.");
        }

        var minY = worldVertices.Min(vertex => vertex.Y);
        var baseSum = Vector2.Zero;
        var baseVertexCount = 0;
        foreach (var vertex in worldVertices)
        {
            if (vertex.Y > minY + .02f)
            {
                continue;
            }

            baseSum += new Vector2(vertex.X, vertex.Z);
            baseVertexCount++;
        }

        return (baseSum / baseVertexCount, minY);
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
            "WinterFarBirdCherry", "WinterFarSpruce", "WinterFarBirch", "WinterFarLinden", "WinterFarMaple", "WinterFarRowan", "WinterFarWillow",
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
                var cx = Mathf.Cos(angle) * 1.8f;
                var cz = Mathf.Sin(angle) * 1.8f;
                var mask = bandNoise.GetNoise3D(cx, v * 12.0f, cz);
                var detail = detailNoise.GetNoise3D(cx * 1.4f, v * 18.0f, cz * 1.4f);
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
