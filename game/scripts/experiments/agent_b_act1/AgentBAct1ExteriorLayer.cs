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
    private AgentBWindStreaks? _windStreaks;
    private bool _sheltered;
    private bool _windowSnowView;
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

    internal void ReconcileBuildingFoliage(Node building)
    {
        // Facilities06: the accessible bath roof is built after PlantFoliage.
        // Apply that same roof rule to this building only, retaining plant IDs
        // and shared meshes. A stem belongs to its exact geometryOwner, not to
        // the whole architecture body that also carries neighbouring scenery.
        var roofs = BuildingRoofBounds(building);
        var plants = GetNode<Node3D>("AgentB_PlantedFoliage");
        var architecture = GetNode<StaticBody3D>("AgentB_ArchitectureCollision");
        var stems = EnumerateDescendants<CollisionShape3D>(architecture).ToArray();
        var owners = new List<string>();
        var contacts = new List<string>();
        var buildingPath = building.GetPath().ToString();
        foreach (var tree in plants.GetChildren().OfType<Node3D>().Where(node =>
            node is not VisualInstance3D && node.HasMeta("plantPosition")))
        {
            if (!UnderBuildingRoof(tree.GlobalPosition, roofs)) continue;
            var owner = tree.GetPath().ToString();
            tree.Visible = false;
            tree.SetMeta("roofSuppressedBy", buildingPath);
            tree.SetMeta("roofSuppressionWorldRoot", tree.GlobalPosition);
            owners.Add(owner);
            foreach (var stem in stems.Where(shape => shape.HasMeta("geometryOwner")
                && shape.GetMeta("geometryOwner").AsString() == owner))
            {
                stem.Disabled = true;
                stem.SetMeta("roofSuppressedBy", buildingPath);
                contacts.Add(stem.GetPath().ToString());
            }
        }
        building.SetMeta("roofSuppressedFoliageOwners", owners.ToArray());
        building.SetMeta("roofSuppressedFoliageContacts", contacts.ToArray());
    }

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
                        mesh.SurfaceSetName(surface, "foliage");
                        var alpha = imported.AlbedoTexture ?? throw new InvalidOperationException("Pine needles are missing their alpha texture.");
                        mesh.SurfaceSetMaterial(surface, PainterlyMaterialLibrary.ForCutout("455749", alpha, "foliage"));
                    }
                    else
                    {
                        mesh.SurfaceSetName(surface, "bark");
                        mesh.SurfaceSetMaterial(surface, RegionalFoliageMaterial(variant, "bark", region));
                    }
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
        SetMeta("visualOnlyPolicy", "terrain, architecture, reachable tree stems and boundary thicket collidable; small decor walk-through");
        SetMeta(
            "collisionPolicy",
            "AgentB_TerrainCollision and AgentB_ArchitectureCollision own traversal; rooted stems join architecture after visible-plant filtering; small decor remains walk-through");
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
                SuppressDuplicateYardWell(instance);
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

        ConformRoadPresentation();
        BuildTerrainCollision();
        BuildKaraGradeSupports();
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
                using var sourceArrays = source.SurfaceGetArrays(0);
                var points = sourceArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array()
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
                using var arrays = source.SurfaceGetArrays(i);
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
        BuildForestBoundaryCollision();
        BuildEnvironment();
        BuildKaraAccentLights();
        BuildSnow();
        BuildWindStreaks();
        _snowTrample = new SnowTrampleField { Name = "AgentBSnowTrample" };
        AddChild(_snowTrample);
    }

    public global::Godot.Environment? ExteriorAtmosphere => _environmentResource;

    /// <summary>True while the exterior WorldEnvironment holds its resource (the one active outdoor environment).</summary>
    public bool ExteriorEnvironmentActive => _environment?.Environment is not null;

    /// <summary>
    /// Routes the single global exterior atmosphere owner. Interior logical
    /// zones keep their authored WorldEnvironment; the Agent B exterior
    /// environment and sun must be disabled there so two global presentation
    /// owners never compete for the same frame.
    /// </summary>
    public void SetExteriorPresentationEnabled(bool enabled, bool night = false, bool windowSnowView = false)
    {
        if (_environment is null)
        {
            return;
        }

        _environment.Environment = enabled ? _environmentResource : null;
        _exteriorPresentationEnabled = enabled;
        _windowSnowView = windowSnowView;
        if (enabled)
        {
            ApplyKaraAccents(night);
        }

        if (_sun is not null)
        {
            _sun.Visible = enabled;
        }

        // A zone switch changes the signed shelter inputs; drop the cached uniforms so
        // the very next pass re-pushes them instead of trusting the previous zone.
        InvalidateSnowPresentationParameters();
        UpdateSnowPresentation(GetViewport()?.GetCamera3D()?.GlobalPosition ?? GlobalPosition);
        _snowTrample?.SetEnabled(enabled && !_sheltered);
        // VIS-076: the streak layer shares the weather gate exactly — an interior or a
        // sheltered spot has no moving air drawn in it, and switching the exterior
        // presentation off is the feature's own disable test.
        _windStreaks?.SetPresentationEnabled(enabled && !_sheltered);

        foreach (var light in _karaAccentLights)
        {
            light.Visible = enabled && night;
        }

        SetMeta("exteriorPresentationEnabled", enabled);
        SetMeta("exteriorMood", night ? "kara-night" : "rainy-day");
        SetMeta("exteriorNight", night);
        SetMeta("activeAtmosphereOwner", enabled ? "AgentBExteriorWorld" : "logical-zone");
        if (_windStreaks is { } streaks) SetMeta("windStreakState", streaks.DescribeWindStreaks());
    }

    public void SetSheltered(bool sheltered)
    {
        _sheltered = sheltered;
        InvalidateSnowPresentationParameters();
        UpdateSnowPresentation(GetViewport()?.GetCamera3D()?.GlobalPosition ?? GlobalPosition);
        _snowTrample?.SetEnabled(_exteriorPresentationEnabled && !sheltered);
        _windStreaks?.SetPresentationEnabled(_exteriorPresentationEnabled && !sheltered);
        SetMeta("physicalSheltered", sheltered);
    }

    /// <summary>URMAN Studio preview: rebuild the terrain's visual surface and collider after author strokes changed.</summary>
    public void RebuildTerrainForStudio()
    {
        if (GetNodeOrNull<CollisionShape3D>("AgentB_TerrainCollision/AgentB_TerrainFaces")?.Shape is ConcavePolygonShape3D shape)
        {
            shape.SetFaces(AgentBAct1HeightField.BuildTerrainFaces());
        }

        var kit = GetNodeOrNull<Node3D>("AgentB_TerrainRoadKit");
        var terrain = kit is null ? null : EnumerateDescendants<MeshInstance3D>(kit).FirstOrDefault(mesh => mesh.Name == "Terrain_Main");
        if (terrain?.Mesh is not ArrayMesh current) return;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var vertex in AgentBAct1HeightField.BuildTerrainFaces())
        {
            surface.SetUV(new Vector2(vertex.X, vertex.Z));
            surface.AddVertex(terrain.ToLocal(ToGlobal(vertex)));
        }

        surface.Index();
        surface.GenerateNormals();
        var result = new ArrayMesh();
        surface.Commit(result);
        result.SurfaceSetMaterial(0, current.SurfaceGetMaterial(0));
        terrain.Mesh = result;
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
        // The western shoulder now carries the Tamara board approach. Its
        // raised snow surface needs contact, not the lower base heightfield.
        var shoulder = EnumerateDescendants<MeshInstance3D>(GetNode<Node3D>("AgentB_TerrainRoadKit"))
            .Single(mesh => mesh.Name == "Grade_Street_West");
        // The base terrain must keep its single-shape contract for articulated
        // hoof support. Give this raised surface its own exact contact body.
        var support = new StaticBody3D { Name = "SurfaceSupport", CollisionLayer = 1u, CollisionMask = 1u };
        shoulder.AddChild(support);
        support.SetMeta("collisionOwner", shoulder.GetPath().ToString());
        var contact = new CollisionShape3D
        {
            Name = "Grade_Street_West_Contact",
            Shape = shoulder.Mesh.CreateTrimeshShape()
        };
        contact.SetMeta("authoredSourceMesh", shoulder.GetPath().ToString());
        support.AddChild(contact);
        shoulder.SetMeta("supportOwner", support.GetPath().ToString());
    }

    private void ConformRoadPresentation()
    {
        var kit = GetNode<Node3D>("AgentB_TerrainRoadKit");
        foreach (var mesh in EnumerateDescendants<MeshInstance3D>(kit))
        {
            var name = mesh.Name.ToString();
            // Legacy yard and field-horizon sheets overlap the walkable Terrain_Main.
            // The current western horizon already belongs to the native forest.
            if (name is "Apron_BabaiYard" or "Grade_Babai_West" or "Ditch_FieldBank_WestStreet")
            {
                mesh.Visible = false;
                mesh.SetMeta("agentBPresentationSuppressed", "canonical-terrain-replaces-preview-snow-sheets");
            }
            var shoulder = name == "Grade_Street_West";
            // Other authored grades only follow the southern gorge down (relayout v3); elsewhere they stay as authored.
            var gorgeOnly = !shoulder && name.StartsWith("Grade_", StringComparison.Ordinal);
            if (mesh.Mesh is not ArrayMesh original || (name != "Terrain_Main" && !name.StartsWith("Road_", StringComparison.Ordinal) && !shoulder && !gorgeOnly)) continue;
            var bounds = (GlobalTransform.AffineInverse() * mesh.GlobalTransform) * original.GetAabb();
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
                // The recessed lane is a property of the shared field, not of this
                // mesh, and the traversal triangles below come from the very same
                // call. Record the numbers the ground was cut with where a capture
                // can read them instead of re-deriving them (VIS-077 proof).
                mesh.SetMeta("snowLaneProfile", System.FormattableString.Invariant(
                    $"owner=AgentBAct1HeightField.RoadProfile depth={AgentBAct1HeightField.LaneDepthFootTrack}..{AgentBAct1HeightField.LaneDepthCarriageway} bank={AgentBAct1HeightField.BankRiseFootTrack}..{AgentBAct1HeightField.BankRiseCarriageway} reach={AgentBAct1HeightField.LaneInfluence}"));
            }
            else
            {
                for (var index = 0; index < original.GetSurfaceCount(); index++)
                {
                    // Both the caller-owned source array and its duplicate must be
                    // released; AddSurfaceFromArrays copies the data it is given.
                    using var sourceArrays = original.SurfaceGetArrays(index);
                    using var arrays = sourceArrays.Duplicate(true);
                    var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    var pathUv = name == "Road_HousePath" ? new Vector2[vertices.Length] : null;
                    var inCut = new bool[vertices.Length];
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        var point = ToLocal(mesh.ToGlobal(vertices[i]));
                        inCut[i] = AgentBAct1HeightField.RiverChannel(point.X, point.Z) < -.05;
                        var ground = AgentBAct1HeightField.CollisionGround(point.X, point.Z);
                        if (shoulder)
                        {
                            // The old presentation-only bank ended at full crest
                            // height. Ease its two ends into walkable ground.
                            var endDistance = Math.Min(point.Z - bounds.Position.Z, bounds.End.Z - point.Z);
                            point.Y = Mathf.Lerp(ground + .005f, point.Y, Mathf.SmoothStep(0, 1, endDistance / 3f));
                        }
                        else if (!gorgeOnly) point.Y += ground - (float)AgentBAct1HeightField.Ground(point.X, point.Z);
                        if (pathUv is not null)
                        {
                            var world = ToGlobal(point);
                            var position = new Vector2(world.X, world.Z);
                            var nearest = float.MaxValue;
                            var across = 0f;
                            var axis = AgentBAct1Layout.HousePathAxis;
                            for (var segment = 0; segment < axis.Length - 1; segment++)
                            {
                                var edge = axis[segment + 1] - axis[segment];
                                var offset = position - axis[segment];
                                var onAxis = axis[segment] + edge * Mathf.Clamp(offset.Dot(edge) / edge.LengthSquared(), 0f, 1f);
                                var delta = position - onAxis;
                                if (delta.LengthSquared() >= nearest) continue;
                                nearest = delta.LengthSquared();
                                across = edge.Normalized().Cross(delta);
                            }
                            pathUv[i] = new Vector2(Mathf.Clamp(.5f + across / 3f, 0f, 1f), 0f);
                        }
                        vertices[i] = mesh.ToLocal(ToGlobal(point));
                    }
                    arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                    if (pathUv is not null) arrays[(int)Mesh.ArrayType.TexUV] = pathUv;
                    // The authored road ribbons and grades predate the southern gorge (relayout v3):
                    // every triangle touching the cut is dropped, so no sheet or slab spans the void
                    // or pierces its slopes; the suspension bridge carries the path across.
                    if (inCut.Any(cut => cut))
                    {
                        var indices = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
                            ? Enumerable.Range(0, vertices.Length).ToArray()
                            : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                        var kept = new List<int>(indices.Length);
                        for (var t = 0; t + 2 < indices.Length; t += 3)
                            if (!inCut[indices[t]] && !inCut[indices[t + 1]] && !inCut[indices[t + 2]])
                                kept.AddRange([indices[t], indices[t + 1], indices[t + 2]]);
                        if (kept.Count == 0) continue;
                        arrays[(int)Mesh.ArrayType.Index] = kept.ToArray();
                    }
                    var reshaped = new ArrayMesh();
                    reshaped.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                    var surface = new SurfaceTool();
                    surface.CreateFrom(reshaped, 0);
                    surface.GenerateNormals();
                    surface.Commit(result);
                    result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, original.SurfaceGetMaterial(index));
                }
            }
            mesh.Mesh = result;
            mesh.SetMeta("supportOwner", "AgentB_TerrainCollision");
        }
    }

    /// <summary>
    /// The occupied room floor replaces the outdoor height field only inside
    /// its actual rotated footprint. Both the rendered terrain and its single
    /// physics owner receive the same clipped faces; the yard remains intact.
    /// </summary>
    public void ExcludeOccupiedRoomTerrain(Node3D room, Vector2 halfSize)
    {
        if (halfSize.X <= 0 || halfSize.Y <= 0
            || Math.Abs(room.GlobalBasis.Y.Normalized().Dot(Vector3.Up)) < .9999f)
            throw new InvalidOperationException("An occupied terrain footprint needs an upright room and positive clear dimensions.");
        var mesh = EnumerateDescendants<MeshInstance3D>(GetNode<Node3D>("AgentB_TerrainRoadKit"))
            .Single(node => node.Name == "Terrain_Main");
        if (mesh.Mesh is not ArrayMesh original || original.GetSurfaceCount() != 1)
            throw new InvalidOperationException("The authoritative terrain must have one triangle surface before an interior cut.");
        var (result, changedTriangles, outputVertices) = ClipGroundFootprint(mesh, original, room, halfSize);
        var terrainToLayer = GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        mesh.Mesh = result;
        // Index/commit is the publishing boundary. Read its final triangle list
        // for physics, rather than retaining a second pre-index representation.
        var publishedFaces = result.GetFaces();
        var physicalFaces = publishedFaces.Select(vertex => terrainToLayer * vertex).ToArray();
        var contact = GetNode<CollisionShape3D>("AgentB_TerrainCollision/AgentB_TerrainFaces");
        if (contact.Shape is not ConcavePolygonShape3D shape)
            throw new InvalidOperationException("The terrain physics owner lost its triangle surface.");
        shape.SetFaces(physicalFaces);
        contact.SetMeta("terrainCutInputVertices", outputVertices);
        contact.SetMeta("terrainCutPublishedVertices", publishedFaces.Length);
        contact.SetMeta("terrainCutPhysicsVertices", shape.GetFaces().Length);
        mesh.SetMeta("occupiedRoomTerrainCut", room.GetPath().ToString());
        mesh.SetMeta("occupiedRoomTerrainHalfSize", halfSize);
        contact.SetMeta("occupiedRoomTerrainCut", room.GetPath().ToString());
        contact.SetMeta("occupiedRoomTerrainHalfSize", halfSize);
        room.SetMeta("exteriorTerrainFootprintExcluded", true);
        room.SetMeta("exteriorTerrainCutHalfSize", halfSize);
        GD.Print($"act1-terrain-room-cut: room={room.GetPath()} half={halfSize} affectedTriangles={changedTriangles} clippedVertices={outputVertices} publishedVertices={publishedFaces.Length} physicsVertices={shape.GetFaces().Length} visualAndPhysics=shared-published-faces");
        // The original yard apron is a separate visible surface. It must share
        // the occupied footprint cut: its uphill edge otherwise rises through
        // the timber floor beside the photograph. Terrain remains its sole
        // support owner; no independent apron collider is introduced.
        var apron = EnumerateDescendants<MeshInstance3D>(GetNode<Node3D>("AgentB_TerrainRoadKit"))
            .Single(node => node.Name == "Apron_BabaiYard");
        if (apron.Mesh is not ArrayMesh apronSource || apronSource.GetSurfaceCount() != 1)
            throw new InvalidOperationException("The Babai yard apron must retain its single authored material surface.");
        var (apronResult, apronChanged, apronVertices) = ClipGroundFootprint(apron, apronSource, room, halfSize);
        // Keep the small imported resource available for a read-only comparison
        // of the retained exterior geometry; it is never rendered or mutated.
        apron.SetMeta("occupiedRoomOriginalMesh", apronSource);
        apron.Mesh = apronResult;
        apron.SetMeta("occupiedRoomTerrainCut", room.GetPath().ToString());
        apron.SetMeta("occupiedRoomTerrainHalfSize", halfSize);
        apron.SetMeta("supportOwner", "AgentB_TerrainCollision");
        GD.Print($"act1-apron-room-cut: mesh={apron.GetPath()} affectedTriangles={apronChanged} clippedVertices={apronVertices} surfaces={apronResult.GetSurfaceCount()} collision=existing-terrain-owner");
        FitBabaiSouthFenceToOccupiedHouse(room, halfSize + new Vector2(.2f, .2f));
    }

    /// <summary>
    /// The occupied room moved after its first cut (relayout v3 stage 5). Rebuild the shared
    /// terrain surface and its single physics owner from the height field, restore the yard
    /// apron, then cut again under the room's present footprint.
    /// </summary>
    public void RecutOccupiedRoomTerrain(Node3D room, Vector2 halfSize)
    {
        var mesh = EnumerateDescendants<MeshInstance3D>(GetNode<Node3D>("AgentB_TerrainRoadKit"))
            .Single(node => node.Name == "Terrain_Main");
        var material = mesh.Mesh.SurfaceGetMaterial(0);
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var vertex in AgentBAct1HeightField.BuildTerrainFaces())
        {
            surface.SetUV(new Vector2(vertex.X, vertex.Z));
            surface.AddVertex(mesh.ToLocal(ToGlobal(vertex)));
        }
        surface.Index();
        surface.GenerateNormals();
        var rebuilt = surface.Commit();
        rebuilt.SurfaceSetMaterial(0, material);
        mesh.Mesh = rebuilt;
        var apron = EnumerateDescendants<MeshInstance3D>(GetNode<Node3D>("AgentB_TerrainRoadKit"))
            .Single(node => node.Name == "Apron_BabaiYard");
        if (apron.HasMeta("occupiedRoomOriginalMesh")) apron.Mesh = apron.GetMeta("occupiedRoomOriginalMesh").As<ArrayMesh>();
        ExcludeOccupiedRoomTerrain(room, halfSize);
    }

    internal static (ArrayMesh Mesh, int ChangedTriangles, int OutputVertices) ClipGroundFootprint(
        MeshInstance3D mesh, ArrayMesh original, Node3D room, Vector2 halfSize)
    {
        using var arrays = original.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var uvValue = arrays[(int)Mesh.ArrayType.TexUV];
        var uv = uvValue.VariantType == Variant.Type.Nil ? Array.Empty<Vector2>() : uvValue.AsVector2Array();
        var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        if (indices.Length == 0) indices = Enumerable.Range(0, vertices.Length).ToArray();
        if (normals.Length != vertices.Length || (uv.Length != 0 && uv.Length != vertices.Length) || indices.Length % 3 != 0)
            throw new InvalidOperationException($"Ground attributes or triangle indices are incomplete: {mesh.Name}.");
        var terrainToRoom = room.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        var output = new List<TerrainCutVertex>(indices.Length + 120);
        var changedTriangles = 0;
        for (var triangle = 0; triangle < indices.Length; triangle += 3)
        {
            var polygon = new List<TerrainCutVertex>(3);
            for (var corner = 0; corner < 3; corner++)
            {
                var index = indices[triangle + corner];
                polygon.Add(new(vertices[index], normals[index], uv.Length == 0 ? Vector2.Zero : uv[index]));
            }
            // Preserve wholly exterior triangles and their attributes exactly.
            if (Enumerable.Range(0, 4).Any(plane => polygon.All(vertex =>
                TerrainFootprintDistance(terrainToRoom * vertex.Position, halfSize, plane) <= 0)))
            {
                output.AddRange(polygon);
                continue;
            }
            changedTriangles++;
            for (var plane = 0; plane < 4 && polygon.Count > 0; plane++)
            {
                AppendTerrainPolygon(output, ClipTerrainPolygon(polygon, terrainToRoom, halfSize, plane, keepInside: false));
                polygon = ClipTerrainPolygon(polygon, terrainToRoom, halfSize, plane, keepInside: true);
            }
        }
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var vertex in output)
        {
            surface.SetNormal(vertex.Normal);
            if (uv.Length != 0) surface.SetUV(vertex.Uv);
            surface.AddVertex(vertex.Position);
        }
        surface.Index();
        var result = surface.Commit();
        result.SurfaceSetMaterial(0, original.SurfaceGetMaterial(0));
        result.SurfaceSetName(0, original.SurfaceGetName(0));
        return (result, changedTriangles, output.Count);
    }

    private void FitBabaiSouthFenceToOccupiedHouse(Node3D room, Vector2 shellHalfSize)
    {
        if (HasMeta("babaiSouthFenceFitted")) return;
        const float left = -36.2f, oldRight = -25.2f, lineZ = -6.4f;
        var start = room.ToLocal(ToGlobal(new Vector3(left, 0, lineZ)));
        var finish = room.ToLocal(ToGlobal(new Vector3(oldRight, 0, lineZ)));
        var direction = finish - start;
        var entry = 0f; var exit = 1f;
        foreach (var (origin, delta, half) in new[] { (start.X, direction.X, shellHalfSize.X), (start.Z, direction.Z, shellHalfSize.Y) })
        {
            if (Math.Abs(delta) < .00001f)
            {
                if (Math.Abs(origin) > half) throw new InvalidOperationException("The retained south fence does not meet the occupied house.");
                continue;
            }
            var a = (-half - origin) / delta; var b = (half - origin) / delta;
            entry = Math.Max(entry, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b));
        }
        if (entry <= 0 || entry >= exit || exit >= 1)
            throw new InvalidOperationException("The occupied house no longer has the expected bounded south-fence junction.");
        var wallX = Mathf.Lerp(left, oldRight, entry);
        var endPostX = wallX - .12f;
        var span = endPostX - left;
        if (span < 1.2f) throw new InvalidOperationException("There is no supported south-fence wing beside the house.");
        var body = GetNode<StaticBody3D>("AgentB_ArchitectureCollision");
        var members = EnumerateDescendants<MeshInstance3D>(GetNode<Node3D>("AgentB_VillageBuildingsKit"))
            .Where(mesh => mesh.Name.ToString().StartsWith("FenceBabaiS_", StringComparison.Ordinal)).ToArray();
        if (members.Length != 23) throw new InvalidOperationException("The south-fence source family changed; refit its members explicitly.");
        foreach (var mesh in members)
        {
            var name = mesh.Name.ToString();
            var contact = body.GetNodeOrNull<CollisionShape3D>($"Col_{name}");
            var post = name.Contains("_Post0_", StringComparison.Ordinal) || name.Contains("_PostCap0_", StringComparison.Ordinal);
            var index = post || name.Contains("_Board0_", StringComparison.Ordinal) ? int.Parse(name[(name.LastIndexOf('_') + 1)..]) : -1;
            if (post && index < 5)
            {
                // These five posts belonged to the narrow preview house. The
                // actual dwelling now forms this part of the yard boundary.
                mesh.Visible = false;
                mesh.SetMeta("agentBPresentationSuppressed", "occupied-house-replaces-south-fence");
                if (contact is not null) contact.Disabled = true;
                continue;
            }
            var source = mesh.Mesh ?? throw new InvalidOperationException($"Fence member {name} lost its mesh.");
            var originalCenter = ToLocal(mesh.GlobalPosition);
            var brace = name.Contains("_Brace", StringComparison.Ordinal);
            var newCenterX = post ? Mathf.Lerp(endPostX, left, (index - 5) / 2f)
                : index >= 0 ? Mathf.Lerp(left + .25f, endPostX - .25f, index / 6f)
                : brace ? (left + endPostX) * .5f : (left + wallX) * .5f;
            var horizontalScale = name.Contains("_Rail", StringComparison.Ordinal) ? (wallX + .03f - left) / 11f
                : brace ? span / 7f : 1f;
            var reshaped = new ArrayMesh();
            for (var s = 0; s < source.GetSurfaceCount(); s++)
            {
                using var sourceArrays = source.SurfaceGetArrays(s);
                using var attributes = sourceArrays.Duplicate(true);
                var points = attributes[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                for (var p = 0; p < points.Length; p++)
                {
                    var at = ToLocal(mesh.ToGlobal(points[p]));
                    at.X = newCenterX + (at.X - originalCenter.X) * horizontalScale;
                    // The authored diagonal is seven metres long, unlike the
                    // eleven-metre rails. Seat its two ends into the actual
                    // terminal posts and the lower/upper rails respectively.
                    if (brace) at.Y += .063f;
                    at.Y += AgentBAct1HeightField.CollisionGround(at.X, at.Z);
                    points[p] = mesh.ToLocal(ToGlobal(at));
                }
                attributes[(int)Mesh.ArrayType.Vertex] = points;
                using var section = new ArrayMesh();
                section.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, attributes);
                using var surface = new SurfaceTool();
                surface.CreateFrom(section, 0);
                surface.GenerateNormals();
                surface.Commit(reshaped);
                reshaped.SurfaceSetMaterial(s, source.SurfaceGetMaterial(s));
            }
            mesh.Mesh = reshaped;
            mesh.SetMeta("southFenceHouseJunction", room.GetPath().ToString());
            if (contact is null)
            {
                contact = new CollisionShape3D { Name = $"Col_{name}" };
                body.AddChild(contact);
            }
            contact.Shape = reshaped.CreateTrimeshShape();
            contact.Transform = body.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
            contact.Disabled = false;
            contact.SetMeta("southFenceHouseJunction", room.GetPath().ToString());
        }
        SetMeta("babaiSouthFenceFitted", true);
        SetMeta("babaiSouthFenceWallJunction", ToGlobal(new Vector3(wallX, AgentBAct1HeightField.CollisionGround(wallX, lineZ), lineZ)));
        GD.Print($"act1-south-fence-house-junction: wallX={wallX:F4} retainedMembers=13 retiredPostsAndCaps=10 supportedWestWing={span:F4}m");
    }

    private readonly record struct TerrainCutVertex(Vector3 Position, Vector3 Normal, Vector2 Uv)
    {
        public TerrainCutVertex Lerp(TerrainCutVertex other, float fraction) => new(
            Position.Lerp(other.Position, fraction), Normal.Lerp(other.Normal, fraction).Normalized(), Uv.Lerp(other.Uv, fraction));
    }

    private static float TerrainFootprintDistance(Vector3 point, Vector2 halfSize, int plane) => plane switch
    {
        0 => point.X + halfSize.X,
        1 => halfSize.X - point.X,
        2 => point.Z + halfSize.Y,
        _ => halfSize.Y - point.Z
    };

    private static List<TerrainCutVertex> ClipTerrainPolygon(List<TerrainCutVertex> polygon,
        Transform3D terrainToRoom, Vector2 halfSize, int plane, bool keepInside)
    {
        var result = new List<TerrainCutVertex>(polygon.Count + 1);
        if (polygon.Count == 0) return result;
        var previous = polygon[^1];
        var previousDistance = TerrainFootprintDistance(terrainToRoom * previous.Position, halfSize, plane);
        var previousKept = keepInside ? previousDistance >= 0 : previousDistance <= 0;
        foreach (var current in polygon)
        {
            var distance = TerrainFootprintDistance(terrainToRoom * current.Position, halfSize, plane);
            var kept = keepInside ? distance >= 0 : distance <= 0;
            if (kept != previousKept)
                result.Add(previous.Lerp(current, previousDistance / (previousDistance - distance)));
            if (kept) result.Add(current);
            previous = current;
            previousDistance = distance;
            previousKept = kept;
        }
        return result;
    }

    private static void AppendTerrainPolygon(List<TerrainCutVertex> output, List<TerrainCutVertex> polygon)
    {
        for (var index = 1; index + 1 < polygon.Count; index++)
        {
            var a = polygon[0];
            var b = polygon[index];
            var c = polygon[index + 1];
            if ((b.Position - a.Position).Cross(c.Position - a.Position).LengthSquared() < 1e-12f) continue;
            output.Add(a);
            output.Add(b);
            output.Add(c);
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

                var contact = new CollisionShape3D
                {
                    Name = $"Col_{meshInstance.Name}",
                    Shape = trimesh,
                    Transform = meshInstance.GlobalTransform
                };
                contact.SetMeta("authoredSourceMesh", meshInstance.GetPath().ToString());
                body.AddChild(contact);
            }
        }
    }

    /// <summary>
    /// Physical block for the boundary thicket recorded by
    /// <see cref="BuildDensifiedPlan"/>. The ridge of young firs is the visible
    /// obstacle; these boxes are its carrier, sized to stay inside the planted
    /// crowns so the player meets a thicket rather than an invisible wall. The
    /// player therefore stops on the settlement envelope, thirty metres short of
    /// the raw terrain edge, and the emergency world clamp stays a fallback
    /// instead of the thing that holds the boundary.
    ///
    /// The shapes join the existing exterior architecture body rather than a new
    /// limiter: the exterior already owns one traversal collider for visible
    /// geometry, and a second world-wide blocker would be a parallel system.
    /// </summary>
    private void BuildForestBoundaryCollision()
    {
        if (_forestBoundarySegments.Count == 0 || _forestRingBand is not { } ring)
        {
            throw new System.InvalidOperationException(
                "Act I forest ring planted no boundary thicket; the walkable edge would be unprotected.");
        }

        var body = GetNode<StaticBody3D>("AgentB_ArchitectureCollision");
        // The thicket is planted on the band's inner rectangle; only samples that
        // really sit on it get a block, so the ring and its carrier cannot drift
        // apart when the envelope changes.
        var blocked = 0;
        foreach (var point in _forestBoundarySegments)
        {
            var onEnvelope = Mathf.Abs(point.X - ring.InnerMin.X) < 2f
                || Mathf.Abs(point.X - ring.InnerMax.X) < 2f
                || Mathf.Abs(point.Y - ring.InnerMin.Y) < 2f
                || Mathf.Abs(point.Y - ring.InnerMax.Y) < 2f;
            if (!onEnvelope)
            {
                continue;
            }

            var ground = (float)AgentBAct1HeightField.CollisionGround(point.X, point.Y);
            body.AddChild(new CollisionShape3D
            {
                Name = $"ForestBoundaryThicket_{blocked}",
                // 2.4 m on both horizontal axes against a 1.9 m planting step and
                // +/-0.45 m jitter: adjacent boxes always overlap, so the worst
                // remaining gap is well under the player capsule's 0.70 m
                // diameter and the thicket cannot be threaded.
                Shape = new BoxShape3D { Size = new Vector3(2.4f, 1.7f, 2.4f) },
                Position = new Vector3(point.X, ground + 0.75f, point.Y)
            });
            blocked++;
        }

        body.SetMeta("forestBoundaryRole",
            "visible winter thicket on the settlement envelope; blocks the walk edge before the terrain seam");
        body.SetMeta("forestBoundaryThicketCount", blocked);
        body.SetMeta("forestRingInnerMin", ring.InnerMin);
        body.SetMeta("forestRingInnerMax", ring.InnerMax);
        body.SetMeta("forestRingOuterMin", ring.OuterMin);
        body.SetMeta("forestRingOuterMax", ring.OuterMax);
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

    private static void SuppressDuplicateYardWell(Node3D villageKit)
    {
        // This preview-board well at (-20.8, -2.2) occupies the real shed's
        // low passage. The two placed Well_YardLandmark instances own wells in
        // the connected village; retaining this roof produces a hidden ceiling.
        var required = new[] { "Well_PostE", "Well_PostW", "Well_Roof" }
            .Concat(Enumerable.Range(0, 8).Select(index => $"Well_Stone{index}")).ToArray();
        var meshes = EnumerateDescendants<MeshInstance3D>(villageKit)
            .Where(mesh => mesh.Name.ToString().StartsWith("Well_", StringComparison.Ordinal)).ToArray();
        if (meshes.Length != required.Length
            || required.Any(name => meshes.All(mesh => mesh.Name != name)))
            throw new InvalidOperationException("The declared duplicate yard-well family changed; inspect its real placement before suppression.");
        foreach (var mesh in meshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta("suppressionReason", "placed village wells replace the preview well inside the occupied yard shed");
        }
        villageKit.SetMeta("suppressedPreviewWellMeshCount", meshes.Length);
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
        var rootedStems = new Dictionary<string, Vector3[]>(StringComparer.Ordinal);
        var graded = _foliageMeshes;
        (ArrayMesh Mesh, string[] Kinds, Vector3[] LowVertices) Geometry(string variant)
        {
            if (geometry.TryGetValue(variant, out var cached)) return cached;
            if (variant is "WinterPine" or "WinterLightPine" or "WinterFarPine")
            {
                var mesh = FoliageMesh(variant, "kara");
                cached = (mesh, Enumerable.Range(0, mesh.GetSurfaceCount()).Select(mesh.SurfaceGetName).ToArray(),
                    Enumerable.Range(0, mesh.GetSurfaceCount()).SelectMany(surface =>
                        SurfaceVertices(mesh, surface)).ToArray());
                geometry[variant] = cached;
                return cached;
            }
            if (!parts.TryGetValue(variant, out var sources))
                throw new InvalidOperationException($"Missing winter foliage geometry: {variant}");
            var meshes = sources.SelectMany(EnumerateSelfAndDescendants<MeshInstance3D>).Distinct()
                .Where(mesh => mesh.Mesh is ArrayMesh).ToArray();
            var sourceVertices = meshes.SelectMany(mesh => Enumerable.Range(0, mesh.Mesh!.GetSurfaceCount())
                .SelectMany(surface => SurfaceVertices(mesh.Mesh!, surface))
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
            if (variant is "WinterPine" or "WinterLightPine" or "WinterFarPine") return FoliageMesh(variant, region);
            if (graded.TryGetValue((variant, region), out var cached)) return cached;
            var source = Geometry(variant);
            var result = (ArrayMesh)source.Mesh.Duplicate();
            for (var surface = 0; surface < source.Kinds.Length; surface++)
            {
                result.SurfaceSetMaterial(surface, RegionalFoliageMaterial(variant, source.Kinds[surface], region));
                result.SurfaceSetName(surface, source.Kinds[surface]);
            }
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
        var architecture = GetNode<StaticBody3D>("AgentB_ArchitectureCollision");
        var stemCount = 0;
        var suppressed = 0;
        var suppressedKara = 0;
        var minimumClearance = float.MaxValue;
        foreach (var (position, sourceVariant) in plan)
        {
            var smallShrub = sourceVariant.StartsWith("Shrub_", StringComparison.Ordinal);
            var woodlandRegrowth = smallShrub && position.X <= -44f && position.X >= -64f && Mathf.Abs(position.Y) <= 24f;
            var groundCover = smallShrub || sourceVariant.StartsWith("Fern_", StringComparison.Ordinal)
                || sourceVariant.StartsWith("Sedge_", StringComparison.Ordinal) || sourceVariant.StartsWith("GrassTuft_", StringComparison.Ordinal);
            var variant = sourceVariant.Replace("WinterLight", "Winter", StringComparison.Ordinal);
            if (smallShrub) variant = woodlandRegrowth ? "WinterSpruce_1" : "WinterBirdCherry_1";
            if (variant.StartsWith("Birch_", StringComparison.Ordinal)) variant = "WinterBirch_1";
            // VIS-084: a tall pine standing in a yard of the newer parcels is the same
            // Christmas-tree read the core ban exists for. Only the lot itself (+1.5 m)
            // counts, so the boundary thicket behind the back fences keeps its spruce.
            if (variant == "WinterPine" && IsInsideResidentialLot(position, 1.5f))
                variant = "WinterLinden_1";
            if (!woodlandRegrowth && (variant.Contains("Spruce", StringComparison.Ordinal) || variant.StartsWith("Pine_", StringComparison.Ordinal)))
                // Conifers are banned inside the residential core, where a fir in
                // a kitchen garden reads as a Christmas decoration. Outside it the
                // winter forest is the tall conifer mass that encloses the
                // village, and rewriting those spruces to bare lindens would leave
                // the skyline open again.
                variant = position.Y <= -86f || !IsInsideSettlementCore(position) && !IsInsideResidentialLot(position, 1.5f)
                    ? variant.StartsWith("WinterSpruce_", StringComparison.Ordinal) ? variant : "WinterSpruce_1"
                    : "WinterLinden_1";
            var template = Geometry(variant);
            var road = AgentBAct1HeightField.RoadInfo(position.X, position.Y);
            var clearance = (float)(road.Distance - road.HalfWidth);
            if (clearance < .25f) throw new InvalidOperationException($"Foliage placement enters road: {variant}@{position}");
            minimumClearance = Mathf.Min(minimumClearance, clearance);
            var depth = Mathf.Clamp((-position.Y - 8f) / 118f, 0, 1);
            var horizontal = Mathf.Lerp(1.04f, .90f, depth) * Mathf.Lerp(.92f, 1.08f, DeterministicPhase(position, 2.7f));
            var vertical = Mathf.Lerp(1.02f, .90f, depth) * Mathf.Lerp(.93f, 1.07f, DeterministicPhase(position, 4.9f));
            if (variant == "WinterPine")
            {
                // The native pine is normalized to one metre. Scale the same
                // rooted mesh into a stand rising behind the readable approach.
                var height = Mathf.Lerp(13f, 21f, Mathf.InverseLerp(-40f, -62f, position.X));
                horizontal *= height * .82f;
                vertical *= height;
            }
            if (!woodlandRegrowth && variant.StartsWith("WinterSpruce_", StringComparison.Ordinal)
                && position.X >= -90f && position.X <= -62f && Mathf.Abs(position.Y) <= 24f)
            {
                // House pilot: broad near crowns overlap taller trees behind them.
                var distance = Mathf.InverseLerp(-62f, -90f, position.X);
                horizontal *= Mathf.Lerp(1.28f, 1.12f, distance)
                    * Mathf.Lerp(.86f, 1.14f, DeterministicPhase(position, 12.7f));
                vertical *= Mathf.Lerp(.78f, 1.2f, distance)
                    * Mathf.Lerp(.86f, 1.14f, DeterministicPhase(position, 19.3f));
            }
            if (variant is "WinterSpruce_4" or "WinterSpruce_5" or "WinterSpruce_6")
            {
                // Broad groves share a skyline instead of making every tree
                // another independent spike in an even-height perimeter.
                var field = .65f * Mathf.Sin(position.X * .055f + position.Y * .075f + 1.2f)
                    + .35f * Mathf.Sin(position.X * -.028f + position.Y * .110f - .3f);
                var grove = Mathf.SmoothStep(-.10f, .65f, field);
                vertical *= Mathf.Lerp(1.10f, 1.65f, grove);
                vertical = Mathf.Min(vertical, 50f / template.Mesh.GetAabb().Size.Y);
                horizontal *= Mathf.Lerp(1f, 1.12f, grove);
            }
            var forestRim = position.X <= -40f || position.X >= 134f || position.Y <= -128f;
            if (forestRim && variant is "WinterLinden_1" or "WinterLinden_2" or "WinterMaple_1")
            {
                // Broad dark forks break the former pale, evenly thin rim.
                horizontal *= variant == "WinterLinden_2" ? 1.65f : 1.35f;
                vertical *= variant == "WinterMaple_1" ? 1.65f : 1.9f;
            }
            if (smallShrub)
            {
                // Near-house woodland overlaps at eye level beneath the tall trunks;
                // the same village shrubs remain low in gardens and verges.
                horizontal *= woodlandRegrowth ? Mathf.Lerp(1.3f, 2.1f, DeterministicPhase(position, 63.5f)) : .38f;
                vertical *= woodlandRegrowth ? Mathf.Lerp(.85f, 1.6f, DeterministicPhase(position, 61.3f)) : .35f;
            }
            if (sourceVariant.StartsWith("Sedge_", StringComparison.Ordinal))
            {
                horizontal *= .62f;
                vertical *= .78f;
            }
            var yawRange = forestRim || variant is "FallenBranch_2" or "WinterSpruce_4" or "WinterSpruce_5" or "WinterSpruce_6" ? 180f : 14f;
            var basis = new Basis(Vector3.Up, Mathf.DegToRad(Mathf.Lerp(-yawRange, yawRange, DeterministicPhase(position, 8.1f))))
                .Scaled(new Vector3(horizontal, vertical, horizontal));
            // Turn this rooted birch away from the rear-house minaret view.
            // Match its placement, not the generated Plant child number.
            if (variant == "WinterBirch_2"
                && position.DistanceSquaredTo(new Vector2(-33.82144f, -12.23175f)) < .01f)
                basis = new Basis(Vector3.Up, Mathf.Pi * .5f) * basis;
            var target = new Vector3(position.X, AgentBAct1HeightField.CollisionGround(position.X, position.Y) - .04f, position.Y);
            if (variant == "FallenBranch_2")
            {
                var run = basis.X.Normalized() * 4.9f * horizontal;
                var rise = AgentBAct1HeightField.CollisionGround(target.X + run.X, target.Z + run.Z)
                    - AgentBAct1HeightField.CollisionGround(target.X - run.X, target.Z - run.Z);
                basis = new Basis(basis.Z.Normalized(), Mathf.Atan(rise / (run.Length() * 2f))) * basis;
                // Seat the broken lower limbs on the actual slope, not on an
                // imaginary horizontal plane across the rising forest bank.
                var lowest = template.LowVertices.Min(vertex =>
                {
                    var point = target + basis * vertex;
                    return point.Y - AgentBAct1HeightField.CollisionGround(point.X, point.Z);
                });
                target.Y -= lowest + .04f;
            }
            var karaSuppression = position.Y <= -86f && new[] { "Birch_", "Spruce_", "MossStone_", "Stump_" }
                .Any(prefix => sourceVariant.StartsWith(prefix, StringComparison.Ordinal));
            var hidden = karaSuppression || sourceVariant != "FallenBranch_2" && position.Y > -86f && DeterministicPhase(position, 17.3f) < .75f
                && new[] { "FallenBranch_", "GrassTuft_", "MossStone_" }.Any(prefix => sourceVariant.StartsWith(prefix, StringComparison.Ordinal));
            if (karaSuppression) suppressedKara++;
            var worldRoot = ToGlobal(target);
            hidden |= UnderBuildingRoof(worldRoot, roofs);
            if (!hidden)
            foreach (var sample in template.LowVertices)
            {
                var point = target + basis * sample;
                if (variant == "FallenBranch_2" && UnderBuildingRoof(ToGlobal(point), roofs))
                { hidden = true; break; }
                if (point.Y - target.Y > 2.6f) continue;
                var edge = AgentBAct1HeightField.RoadInfo(point.X, point.Z);
                if (edge.Distance - edge.HalfWidth >= .1) continue;
                hidden = true; break;
            }
            if (hidden) { suppressed++; continue; }
            if (variant == "WinterPine" && position.X >= -64f && position.X <= -44f && Mathf.Abs(position.Y) <= 24f)
            {
                // Rooted, irregular lean breaks the vertical colonnade. Use the
                // same basis for all LODs and the baked trunk collision below.
                var direction = Mathf.Tau * DeterministicPhase(position, 71.3f);
                var lean = new Basis(new Vector3(Mathf.Cos(direction), 0, Mathf.Sin(direction)),
                    Mathf.DegToRad(Mathf.Lerp(2f, 7f, DeterministicPhase(position, 73.7f)))) * basis;
                var canopy = GlobalTransform * (new Transform3D(lean, target) * template.Mesh.GetAabb());
                var clear = !roofs.Any(roof => roof.Grow(.35f).Intersects(canopy))
                    && template.LowVertices.All(sample =>
                    {
                        var point = target + lean * sample;
                        if (point.Y - target.Y > 2.6f) return true;
                        var edge = AgentBAct1HeightField.RoadInfo(point.X, point.Z);
                        return edge.Distance - edge.HalfWidth >= .1f;
                    });
                // Preserve every accepted planting and its ID at a tight path
                // or roof: keep the original upright basis if lean cannot fit.
                if (clear) basis = lean;
            }
            var region = forestRim || variant == "FallenBranch_2" || position.Y <= -86f ? "kara" : position.Y <= -58f ? "zirat" : "village";
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
                    // VIS-029: the far tier only ever exists beyond its own begin
                    // distance, and on every authored profile that is inside fog the
                    // shadow of that trunk cannot be read. The near silhouette the
                    // author judges is untouched: tiers 0 and 1 keep casting, and the
                    // switch distances are the ones the parity audit measures.
                    if (hasLods && SuppressFarTierShadow(lod))
                        instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                    tree!.AddChild(instance);
                    ConfigureFoliageRange(instance, hasLods ? lod : -1, false,
                        template.Mesh.GetAabb().Size.Y * vertical);
                }
            }
            // A visible, independently approachable trunk must stop the player.
            // Build after all suppression, from the rooted wood rather than the
            // crown's AABB. Outer-ring trees are already behind the thicket's
            // physical carrier; shrubs and ground cover stay walk-through.
            if (tree is not null && (variant == "FallenBranch_2" || hasLods && template.Mesh.GetAabb().Size.Y * vertical >= 3.5f)
                && _forestRingBand is { } ring
                && (variant == "FallenBranch_2"
                    || position.X > ring.InnerMin.X + 2f && position.X < ring.InnerMax.X - 2f
                    && position.Y > ring.InnerMin.Y + 2f && position.Y < ring.InnerMax.Y - 2f))
            {
                if (!rootedStems.TryGetValue(variant, out var faces))
                {
                    var bark = Array.IndexOf(template.Kinds, "bark");
                    if (variant == "FallenBranch_2")
                    {
                        using var arrays = template.Mesh.SurfaceGetArrays(bark);
                        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                        faces = arrays[(int)Mesh.ArrayType.Index].AsInt32Array().Select(index => vertices[index]).ToArray();
                    }
                    else faces = RootedStemFaces(template.Mesh, bark);
                    rootedStems[variant] = faces;
                }
                if (faces.Length == 0)
                    throw new InvalidOperationException($"Visible winter tree has no rooted stem geometry: {variant}");
                // Bake the real yaw and non-uniform plant scale into these few
                // stem faces. The physics shape itself keeps a unit basis.
                var baked = faces.Select(vertex => basis * vertex).ToArray();
                if (variant == "WinterPine")
                    // Native pine vertices are normalized: select the physical
                    // height band after scaling, retaining crossing triangles.
                    baked = Enumerable.Range(0, baked.Length / 3)
                        .Where(triangle => Enumerable.Range(0, 3).Any(corner => baked[triangle * 3 + corner].Y <= 2.6f))
                        .SelectMany(triangle => Enumerable.Range(0, 3).Select(corner => baked[triangle * 3 + corner])).ToArray();
                var shape = new ConcavePolygonShape3D();
                shape.SetFaces(baked);
                var collider = new CollisionShape3D
                {
                    Name = $"PlantedStem_{tree.Name}", Shape = shape, Position = target
                };
                architecture.AddChild(collider);
                collider.SetMeta("plantVariant", variant);
                collider.SetMeta("plantPosition", target);
                collider.SetMeta("geometryOwner", tree.GetPath().ToString());
                stemCount++;
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
            // VIS-030: the cell is culled as one AABB built from the instances, but the
            // wind shader moves vertices by up to |gust| (<=1) x wind_sway x height,
            // 1.17x that with the z component. Cover that real reach, measured from the
            // tallest instance in this cell, instead of an arbitrary large box.
            var meshTop = Mathf.Max(0f, multi.Mesh.GetAabb().End.Y);
            var sway = 0f;
            for (var surfaceIndex = 0; surfaceIndex < multi.Mesh.GetSurfaceCount(); surfaceIndex++)
                if (multi.Mesh.SurfaceGetMaterial(surfaceIndex) is ShaderMaterial material)
                    sway = Mathf.Max(sway, material.GetShaderParameter("wind_sway").AsSingle());
            // The shader displaces local X/Z, then the instance basis scales and
            // rotates that vector. Y scale is unrelated to that reach on tilted,
            // non-uniformly scaled plants. Read the material rather than guess by name.
            var displacement = new Vector3(meshTop * sway, 0f, meshTop * sway * .6f);
            group.ExtraCullMargin = transforms.Max(transform => (transform.Basis * displacement).Length());
            group.SetMeta("windCullMargin", group.ExtraCullMargin);
            // VIS-029: knee-high grass, fern and sedge add shadow-pass work without a
            // readable shadow, and so does every ground-cover cell past its first LOD
            // (> 24 m). Near shrubs keep their contact shadow.
            var lowCover = key.Variant.Contains("Grass", StringComparison.Ordinal)
                || key.Variant.Contains("Fern", StringComparison.Ordinal)
                || key.Variant.Contains("Sedge", StringComparison.Ordinal);
            if (lowCover || key.Lod >= 1)
                group.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            plants.AddChild(group);
            group.SetMeta("presentationOnly", true);
            group.SetMeta("plantVariant", key.Variant);
            ConfigureFoliageRange(group, key.Lod, true, 0f);
        }
        foreach (var region in new[] { "village", "zirat", "kara" })
        foreach (var variant in new[] { "WinterBirch_1", "WinterLinden_1", "WinterBirdCherry_1",
                     "WinterLightBirch_1", "WinterLightLinden_1", "WinterLightBirdCherry_1",
                     "WinterFarBirch_1", "WinterFarLinden_1", "WinterFarBirdCherry_1",
                     "WinterSpruce_1", "WinterLightSpruce_1", "WinterFarSpruce_1" })
            RegionalMesh(variant, region);
        // VIS-027/029/030: silhouette parity across every authored tier switch, the
        // measured culling volume of every plant cell and the structural census of
        // this layer. Runs after every mesh and instance exists, so it reads what the
        // frame will actually contain.
        AuditFoliageLodAndCullingBounds(plants);
        SetMeta("roadEnvelopeSuppressedFoliageEntryCount", suppressed);
        SetMeta("plantedVariantCount", parts.Count);
        SetMeta("plannedFoliageEntryCount", plan.Count);
        SetMeta("plantedFoliageEntryCount", plan.Count);
        SetMeta("plantedFoliageNodeCount", plants.GetChildCount());
        SetMeta("suppressedKaraFoliageEntryCount", suppressedKara);
        SetMeta("minimumFoliageRoadClearance", minimumClearance);
        SetMeta("foliagePlacementPolicy", "roots and full lower silhouette clear road; suppressed entries remain counted separately");
        SetMeta("foliageRebasePolicy", "one layer-space root pivot per shared geometry variant");
        SetMeta("createdPlantedStemCollisionCount", stemCount);
        SetMeta("plantedStemCollisionPolicy", "root-connected lower wood of visible large plants inside thicket; existing architecture body; interior exclusions remain authoritative");
        SetMeta("regionalFoliageGradePolicy", "semantic snow/bark/dry ground cover; winter deciduous village, conifers at Kara only");
        foreach (var template in kit.GetChildren()) template.Free();
        kit.SetMeta("templateSourcePolicy", "source owner retained; template instances released after shared geometry extraction");
    }

    // SurfaceGetArrays builds a new caller-owned Array on every call, and each call
    // copies the whole surface into managed packed arrays. Releasing it here keeps the
    // callers that only want the vertices from leaving that copy to the finalizer.
    private static Vector3[] SurfaceVertices(Mesh mesh, int surface)
    {
        using var arrays = mesh.SurfaceGetArrays(surface);
        return arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
    }

    private static Vector3[] RootedStemFaces(ArrayMesh mesh, int barkSurface)
    {
        if (barkSurface < 0) return Array.Empty<Vector3>();
        using var arrays = mesh.SurfaceGetArrays(barkSurface);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        if (indices.Length == 0) indices = Enumerable.Range(0, vertices.Length).ToArray();
        // The authored winter mesh combines separate tubes for stems, branches
        // and twigs in one bark surface. Only components touching the root are
        // load-bearing stems. Weld by position across UV/normal seams, without
        // bridging the separate branch tubes or filling their empty spaces.
        var incident = new Dictionary<Vector3, List<int>>();
        for (var triangle = 0; triangle + 2 < indices.Length; triangle += 3)
        for (var corner = 0; corner < 3; corner++)
        {
            var vertex = vertices[indices[triangle + corner]];
            if (!incident.TryGetValue(vertex, out var faces)) incident[vertex] = faces = new();
            faces.Add(triangle);
        }
        // Large planted variants have one stem at the native root pivot. A
        // willow's hanging branches can also touch the snow, far from that
        // pivot: seeding every ground-level vertex would turn those branches
        // into a wide root collider and let an adjacent interior prune it.
        var root = incident.Keys.Where(vertex => vertex.Y <= .01f)
            .OrderBy(vertex => new Vector2(vertex.X, vertex.Z).LengthSquared()).Take(1);
        var pending = new Stack<Vector3>(root);
        var reached = new HashSet<int>();
        while (pending.TryPop(out var vertex))
        foreach (var triangle in incident[vertex])
        {
            if (!reached.Add(triangle)) continue;
            for (var corner = 0; corner < 3; corner++) pending.Push(vertices[indices[triangle + corner]]);
        }
        // Keep every triangle crossing the player's height band so there is no
        // cut in the contact surface. High stems and crowns need no extra
        // traversal triangles for the ordinary non-jumping controller.
        return reached.Order().Where(triangle => Enumerable.Range(0, 3)
                .Any(corner => vertices[indices[triangle + corner]].Y <= 2.6f))
            .SelectMany(triangle => Enumerable.Range(0, 3).Select(corner => vertices[indices[triangle + corner]]))
            .ToArray();
    }

    internal static void ConfigureFoliageRange(GeometryInstance3D instance, int lod, bool groundCover, float treeHeight)
    {
        // The same distance cannot serve a sapling and a thirty-metre canopy.
        // Use projected-size parity while preserving matching LOD fade bands.
        //
        // VIS-027/VIS-029: the band widths and their overlap stay exactly as they
        // were — every tier still hands over to the next inside a cross-fade, so the
        // switch is a fade and not a pop, and the crossover moves with the tree's
        // own height. Only the ceiling of the multiplier is bounded now, because the
        // old 3x let a full-detail crown keep its near tier out to 78 m, past the
        // fog line of every authored profile. The bands below are multiplied by the
        // same factor for all three tiers, which is what keeps the parity gate
        // meaningful: a tier swap cannot be hidden by moving only one band. The
        // crossover itself is verified per plant in AuditFoliageLodAndCullingBounds,
        // where all three instances of one tree are visible at once.
        var rangeScale = groundCover ? 1f : Mathf.Clamp(treeHeight / 8f, 1f, LodRangeScaleCeiling);
        instance.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        instance.VisibilityRangeBegin = rangeScale * (lod switch { 1 => 24, 2 => 60, _ => 0 });
        instance.VisibilityRangeBeginMargin = rangeScale * (lod switch { 1 => 2, 2 => 4, _ => 0 });
        instance.VisibilityRangeEnd = rangeScale * (lod switch { 0 => 26, 1 => 64, _ => groundCover ? 85 : 0 });
        instance.VisibilityRangeEndMargin = rangeScale * (lod switch { 0 => 2, 1 => 4, _ => groundCover ? 5 : 0 });
        instance.SetMeta("visibilityRangeTier", lod);
        instance.SetMeta("visibilityRangeScale", rangeScale);
    }

    private static Material RegionalFoliageMaterial(string variant, string kind, string region)
    {
        var birch = variant.Contains("Birch", StringComparison.Ordinal);
        var pine = variant.Contains("Pine", StringComparison.Ordinal);
        var conifer = pine || variant.Contains("Spruce", StringComparison.Ordinal);
        return kind switch
        {
            "snow" => PainterlyMaterialLibrary.ForColor("e8edf0", "snow_roof"),
            "berries" => PainterlyMaterialLibrary.ForColor("784239", "rowan_berries"),
            "bark" => PainterlyMaterialLibrary.ForColor(
                birch ? region == "kara" ? "90978c" : "c9c2ad"
                    : !conifer ? region == "kara" ? "575953" : "9b9487"
                    : region == "kara" ? "504c43" : "685e50",
                birch ? "bark_birch_winter" : pine ? "bark_pine" : "wood_bark", sheltered: region == "kara"),
            "stone" => PainterlyMaterialLibrary.ForColor("74766d", "stone"),
            // Winter conifers already carry shaped snow caps. A second shader
            // blanket bleaches every bough into a bright plate against the sky.
            _ => PainterlyMaterialLibrary.ForColor(conifer ? "394c50" : "827a65",
                conifer ? "foliage" : "grass", sheltered: conifer)
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
                var vertices = SurfaceVertices(sourceArrayMesh, surface);
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
        // VIS-085 evidence list: rebuilt with the plan, never carried over from a
        // previous world instance.
        _forestRingTrees.Clear();
        _outerRowUnderstorySkipped = 0;
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
            var authoredRoad = AgentBAct1HeightField.RoadInfo(position.X, position.Y);
            // New connected streets can replace a former grassy gap. Cull the
            // actual source shrub/stone as well as trees before its rooted
            // visual and contact are built; PlantFoliage keeps its strict guard.
            var sourceClear = (float)(authoredRoad.Distance - authoredRoad.HalfWidth) >= (isAuthoredTree ? 2.0f : .75f);
            if (isAuthoredTree && !sourceClear) continue;
            // Ground-cover removal must not consume a different random stream:
            // later garden fixtures retain their authored roots and positions.
            if (sourceClear) baseEntries.Add((position, winterVariant));
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

            // Far-bank yards (relayout v3) are kept clear like the house keep-outs.
            if (point.X > 56f && AgentBAct1HeightField.InsideHouseholdClearance(point))
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
            (-62f, -40f, -120f, 214f),  // west rim (runs on with the northern expansion)
            (134f, 140f, -120f, 214f),  // east rim, behind the far-bank quarter (relayout v3)
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
                    // VIS-026: the rim behind Kara read as one even wall from the
                    // street. Two near-layer openings (x -12 and +9, 6 m wide) push
                    // their trees 11 m deeper instead of deleting them: the count and
                    // the random stream stay, the far layer thickens behind each gap,
                    // and the opening shows depth, not the edge of the map.
                    if (x0 == -30f && point.Y > -136f
                        && (Mathf.Abs(point.X + 12f) < 3f || Mathf.Abs(point.X - 9f) < 3f))
                        point.Y -= 11f;
                    var roadInfo = AgentBAct1HeightField.RoadInfo(point.X, point.Y);
                    if ((float)(roadInfo.Distance - roadInfo.HalfWidth) < 2.0f)
                    {
                        continue;
                    }

                    // Keep the original random stream: species changes must not
                    // move later roots, garden fixtures or boundary contacts.
                    var forestSide = point.Y <= -100f;
                    var variant = forestSide && rng.Randf() < 0.55f
                        ? "WinterSpruce_" + rng.RandiRange(1, 2)
                        : rng.RandiRange(1, 4) switch
                        {
                            1 => "WinterLightBirch_2",
                            2 => "WinterLinden_2",
                            3 => "WinterMaple_1",
                            _ => "WinterLinden_1"
                        };
                    // The western woodland reaches the village as layered
                    // mature crowns, with bare forks remaining between groves.
                    // Reuse the imported pine's cutout needles and native LODs;
                    // the kit's solid crown plates stay in the distant belt.
                    if (point.X <= -40f && DeterministicPhase(point, 43.1f) < .72f)
                        variant = "WinterPine";
                    generated.Add((point, variant));
                    if ((point.X <= -52f || point.X >= 140f || point.Y <= -130f)
                        && !InsideMosqueKeepOut(point) && DeterministicPhase(point, 31.7f) > .55f)
                        generated.Add((point + new Vector2(-.8f, 1.2f), "FallenBranch_2"));
                }
            }
        }

        // Pilot woodland: uneven patches of the existing winter regrowth close
        // the park-like snow gaps beneath the tall crowns. Append without RNG
        // calls so original roots and the later forest belt keep their positions.
        // Existing groups keep their spacing priority; fill the formerly
        // omitted groups afterward without moving roots or consuming RNG.
        foreach (var (point, source) in generated.OrderBy(entry =>
                     Mathf.Sin(entry.Item1.X * .31f + entry.Item1.Y * .17f) < -.2f).ToArray())
        {
            if (!source.StartsWith("Winter", StringComparison.Ordinal) || point.X > -44f || point.X < -62f || Mathf.Abs(point.Y) > 22f
                || InsideMosqueKeepOut(point)) continue;
            var direction = DeterministicPhase(point, 57.7f) * Mathf.Tau;
            var cluster = point + new Vector2(Mathf.Cos(direction), Mathf.Sin(direction)) * 1.3f;
            for (var i = 0; i < 4; i++)
            {
                // Overlap crowns on one side of the parent trunk instead of
                // distributing four isolated miniature trees around its perimeter.
                var angle = DeterministicPhase(point, 59f + i) * Mathf.Tau;
                var radius = Mathf.Lerp(.35f, 1.0f, DeterministicPhase(point, 67f + i));
                var shrub = cluster + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (InsideMosqueKeepOut(shrub)) continue;
                TryPlant(shrub.X, shrub.Y, 2.6f, 1000f, .6f, "Shrub_1");
            }
        }

        // Act I forest ring. The author decided the village must be enclosed by a
        // very tall, dense, deliberately frightening forest with nothing visible
        // beyond it from any reachable position; this replaces the earlier
        // six-row belt that only reached 7.4 m.
        //
        // The band is deliberately deeper than the walkable terrain window on
        // three sides: that surface simply ends at X -64/66 and Z -152, so a
        // mass planted only inside it would still expose the raw seam past the
        // trees. West and east run 26-28 m past the seam and the south 6 m past
        // it; the north edge gets its own 40 m so it clears the +Z 104 seam too,
        // because that is the direction the arrival view looks into. Every
        // outward view therefore ends in trunks and snow-laden boughs rather than
        // in the terrain boundary.
        //
        // Rows run from the outer rectangle inward, so row 0 closes the skyline
        // and the last row lands exactly on the settlement envelope where the
        // player can stand next to it.
        const int beltRows = 9;
        const float beltInset = ForestRingDepth / (beltRows - 1);
        var ringOuterMin = ForestRingInnerMin - new Vector2(ForestRingDepth, ForestRingDepth);
        var ringOuterMax = ForestRingInnerMax + new Vector2(ForestRingDepth, ForestRingNorthDepth);
        _forestRingBand = (ForestRingInnerMin, ForestRingInnerMax, ringOuterMin, ringOuterMax);
        for (var row = 0; row < beltRows; row++)
        {
            var inset = row * beltInset;
            // The rows the player can walk up to carry the finest step; the
            // outer rows only have to close the skyline from a distance.
            var beltStep = row < 4 ? 4.0f : 3.4f;
            var rowMin = ringOuterMin + new Vector2(inset, inset);
            var rowMax = ringOuterMax - new Vector2(inset, inset);
            // VIS-085: the belt is emitted on a fixed step, so even with its ±1.2 m
            // jitter the trunk interval keeps one dominant period the eye counts as
            // a wall. BreakRowPeriod moves each stem along its own row only — the
            // row's normal position, and therefore the closure of the skyline, is
            // untouched — and it is a pure function of the point, so no RNG value is
            // consumed and every later root, garden fixture and boundary contact
            // keeps its authored place.
            for (var x = rowMin.X; x <= rowMax.X; x += beltStep)
            {
                var southStem = new Vector2(x + rng.RandfRange(-1.2f, 1.2f), rowMin.Y);
                EmitBelt(generated, rng, BreakRowPeriod(southStem, row, 0), row, beltRows, southStem, 0);
                var northStem = new Vector2(x + rng.RandfRange(-1.2f, 1.2f), rowMax.Y);
                EmitBelt(generated, rng, BreakRowPeriod(northStem, row, 0), row, beltRows, northStem, 1);
            }
            for (var z = rowMin.Y + beltStep; z <= rowMax.Y - beltStep; z += beltStep)
            {
                var westStem = new Vector2(rowMin.X, z + rng.RandfRange(-1.2f, 1.2f));
                EmitBelt(generated, rng, BreakRowPeriod(westStem, row, 1), row, beltRows, westStem, 2);
                var eastStem = new Vector2(rowMax.X, z + rng.RandfRange(-1.2f, 1.2f));
                EmitBelt(generated, rng, BreakRowPeriod(eastStem, row, 1), row, beltRows, eastStem, 3);
            }
        }

        // The arrival road is the ring opening the player can actually walk to,
        // and the author ruled out a straight tunnel with a view past the ring.
        // The road surface already ends at z 40, so past its end the corridor is
        // closed with staggered groups; the entrance vista then terminates in
        // forest instead of running on to the terrain edge. Groups sit behind the
        // road end, so a trunk never stands in the kerb line itself.
        for (var z = ForestRingInnerMax.Y - 14f; z <= ForestRingInnerMax.Y; z += 3.4f)
        {
            var stagger = Mathf.Sin(z * 0.63f) * 3.6f;
            for (var x = -17f; x <= 17f; x += 3.4f)
            {
                var candidate = new Vector2(
                    x + stagger + rng.RandfRange(-1.1f, 1.1f),
                    z + rng.RandfRange(-1.1f, 1.1f));
                if (InsideArrivalClosureKeepOut(candidate)
                    || AgentBAct1Layout.InsideSquareBuildingClearance(candidate)
                    || AgentBAct1HeightField.InsideHouseholdClearance(candidate))
                {
                    continue;
                }

                // The arrival closure keeps its authored composition: its keep-outs
                // are evaluated on this exact point, so it is deliberately not run
                // through BreakRowPeriod. Its own sine stagger already breaks the
                // interval; VIS-085 measures that band separately (edge 4).
                EmitBelt(generated, rng, candidate, row: 3, rowCount: beltRows, jittered: candidate, edge: 4);
            }
        }

        // Boundary thicket: the ring has to be walkable-up-to and stop the
        // player at something they can see. A dense line of young snow-laden
        // firs on the settlement envelope does that: it reads as impassable
        // regrowth at body height, it is the winter undergrowth the plan names
        // as the lower closure, and its collision boxes hide inside the visible
        // thicket instead of standing in open ground. The tall stand is planted
        // immediately behind it, so the last thing seen in any outward
        // direction is needles, snow and trunks.
        const float boundaryStep = 1.9f;
        foreach (var (edgeA, edgeB, outward) in new (Vector2, Vector2, Vector2)[]
                 {
                     (ForestRingInnerMin,
                      new Vector2(ForestRingInnerMax.X, ForestRingInnerMin.Y), new Vector2(0f, -1f)),
                     (new Vector2(ForestRingInnerMax.X, ForestRingInnerMin.Y), ForestRingInnerMax, new Vector2(1f, 0f)),
                     (ForestRingInnerMax,
                      new Vector2(ForestRingInnerMin.X, ForestRingInnerMax.Y), new Vector2(0f, 1f)),
                     (new Vector2(ForestRingInnerMin.X, ForestRingInnerMax.Y), ForestRingInnerMin, new Vector2(-1f, 0f))
                 })
        {
            var length = edgeA.DistanceTo(edgeB);
            var direction = (edgeB - edgeA) / length;
            for (var travelled = 0f; travelled <= length; travelled += boundaryStep)
            {
                var point = edgeA + direction * travelled
                    + new Vector2(rng.RandfRange(-0.45f, 0.45f), rng.RandfRange(-0.45f, 0.45f));
                if (InsideMosqueKeepOut(point))
                {
                    continue;
                }

                // Every point below carries a body-height collision box. Keep
                // a body-height visible fir there; small debris belongs only
                // behind that line, where it cannot masquerade as a barrier.
                var thicket = rng.Randf() < .5f ? "WinterSpruce_1" : "WinterSpruce_2";
                generated.Add((point, thicket));
                _forestBoundarySegments.Add(point);

                // A second thicket line three metres deeper, staggered half a
                // step. One irregular line still left eye-level slots on the
                // sweep: between two stems the view reached ground beyond the
                // ring. Two staggered lines plus the interstitial understory
                // below close the 0-3 m band from the last standable position.
                var deepPoint = point + outward * 3f
                    + direction * 0.95f
                    + new Vector2(rng.RandfRange(-0.7f, 0.7f), rng.RandfRange(-0.7f, 0.7f));
                if (!InsideMosqueKeepOut(deepPoint))
                {
                    var deepRoll = rng.Randf();
                    generated.Add((deepPoint, deepRoll switch
                    {
                        < 0.34f => "WinterSpruce_2",
                        < 0.62f => "WinterSpruce_1",
                        < 0.74f => "WinterRowan_2",
                        < 0.84f => "WinterBirdCherry_1",
                        < 0.92f => "Stump_0",
                        _ => "MossStone_0"
                    }));
                }

                // Interstitial understory between the thicket stems. The sweep
                // found the eye-level band between two young firs could still
                // show ground beyond the ring, so the 0-2 m layer is filled at
                // the midpoints too: this is the "lower tier closes at player
                // height" rule, and it is what the boundary looks like from the
                // last place the player can stand.
                foreach (var side in new[] { -0.95f, 0.95f })
                {
                    var fillerPoint = point + direction * side
                        + new Vector2(rng.RandfRange(-0.6f, 0.6f), rng.RandfRange(-0.6f, 0.6f));
                    if (InsideMosqueKeepOut(fillerPoint))
                    {
                        continue;
                    }

                    var fillerRoll = rng.Randf();
                    generated.Add((fillerPoint, fillerRoll switch
                    {
                        < 0.30f => "WinterSpruce_1",
                        < 0.52f => "Shrub_2",
                        < 0.70f => "WinterBirdCherry_1",
                        < 0.84f => "MossStone_0",
                        _ => "Fern_0"
                    }));
                }
            }
        }
        SetMeta("forestBoundarySegmentCount", _forestBoundarySegments.Count);

        // This north-yard root is an authored composition/contact fixture,
        // recorded in the 2026-09-27 boundary receipt. Roadside RNG must not
        // move it or replace it when a connected street changes the verges.
        generated.RemoveAll(entry => entry.Item2 == "WinterWillow_1"
            && entry.Item1.X >= -27f && entry.Item1.X < -24f
            && entry.Item1.Y >= 5f && entry.Item1.Y < 7f);
        generated.Add((new Vector2(-25.704922f, 5.9302864f), "WinterWillow_1"));

        // Generated entries (satellites, ring, boundary thicket) must never
        // violate the road envelope; unlike authored plan entries they are
        // filtered out silently so roads keep their own natural gaps.
        // Trees need real shoulder distance — a trunk half a metre from the
        // kerb reads as "a tree growing on the road". Only low ground cover
        // is allowed to hug the verge.
        var culledTrees = 0;
        var yardTreeRelocations = 0;
        foreach (var (seedPosition, variant) in generated)
        {
            var position = AgentBFoliagePlan.ResolveYardWorkTree(seedPosition, variant);
            if (position != seedPosition) yardTreeRelocations++;
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
        SetMeta("yardWorkTreeRelocations", yardTreeRelocations);
        SetMeta("winterRoadClearancePolicy", "trees >= 2.6m, ground cover >= 0.75m from the road envelope");

        // Batching the belt into MultiMeshes was measured and reverted: it removes
        // node count but draws every instance of a cell whenever the cell is in
        // view, which cost more than the per-tree nodes it replaced (high preset:
        // 108 -> 95 fps, p95 9.9 -> 13.0 ms). Per-tree nodes cull individually.
        return baseEntries;
    }

    /// <summary>
    /// The village mosque complex stands on the settlement envelope's west edge:
    /// its courtyard wall reaches x −62.5 while the ring's inner row runs at
    /// x −62, so a circular keepout around the hall alone leaves a strip of
    /// courtyard planted with firs. This rectangle covers hall and courtyard, and
    /// it is honoured by the boundary thicket as well as the ring. Only the
    /// innermost row is affected; the rows behind it still close the skyline, so
    /// the mosque keeps its forest backdrop without trees standing in its yard.
    /// </summary>
    // Relative to the minaret corner, which moved west of Мәйдан in relayout v3.
    internal static readonly Vector2 MosqueKeepOutMin = AgentBAct1Layout.MosqueAnchor + new Vector2(-17.5f, -9f);

    internal static readonly Vector2 MosqueKeepOutMax = AgentBAct1Layout.MosqueAnchor + new Vector2(3.5f, 7f);

    internal static bool InsideMosqueKeepOut(Vector2 point) =>
        point.X >= MosqueKeepOutMin.X && point.X <= MosqueKeepOutMax.X
        && point.Y >= MosqueKeepOutMin.Y && point.Y <= MosqueKeepOutMax.Y;

    /// <summary>
    /// The two arrival reverse-edge dwellings stand inside the north entrance
    /// closure band. Crowns over a roof read as a tree growing through the house,
    /// so the closure skips their footprints; the ring behind them still closes
    /// the view.
    /// </summary>
    internal static readonly (Vector2 Min, Vector2 Max)[] ArrivalClosureKeepOuts =
    [
        (new Vector2(-11.7f, 48.1f), new Vector2(-3.9f, 55.9f)),
        (new Vector2(4.0f, 48.9f), new Vector2(11.8f, 56.7f))
    ];

    internal static bool InsideArrivalClosureKeepOut(Vector2 point)
    {
        foreach (var (min, max) in ArrivalClosureKeepOuts)
        {
            if (point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Settlement envelope and the ring band planted around it, in world X/Z.
    /// <see cref="BuildDensifiedPlan"/> fills the band; the band rectangle is
    /// reported in the layer metadata so a capture can be related to it.
    /// </summary>
    private static (Vector2 InnerMin, Vector2 InnerMax, Vector2 OuterMin, Vector2 OuterMax)? _forestRingBand;

    /// <summary>
    /// VIS-085: every ring trunk this build planted, with both its pre-break and
    /// post-break position and the row/edge it belongs to. The rhythm audit needs the
    /// pair because "no repeating trunk interval in the first 20 m" is only
    /// falsifiable against the placement it replaced, and one run has to carry both
    /// numbers. Presentation data only: nothing reads it back into the world.
    /// </summary>
    private readonly List<(Vector2 Jittered, Vector2 Planted, string Variant, int Row, int Edge)> _forestRingTrees = new();

    /// <summary>Inner settlement envelope of the Act I forest ring, world X/Z.</summary>
    internal static readonly Vector2 ForestRingInnerMin = new(-62f, -128f);

    // East edge sits past the ravine's far bank: the second half of the
    // village stands between the ravine and the ring (author, 2026-09-25).
    internal static readonly Vector2 ForestRingInnerMax = new(138f, AgentBAct1HeightField.MaxZ);

    /// <summary>Ring depth in metres: how far the forest runs past the envelope.</summary>
    internal const float ForestRingDepth = 30.4f;

    /// <summary>
    /// Extra ring depth on the north edge. The arrival view looks that way, so
    /// the band has to clear the +Z terrain seam by a wider margin than the
    /// sides, not merely reach it.
    /// </summary>
    internal const float ForestRingNorthDepth = 40f;

    /// <summary>
    /// Residential core: the yards, streets and public buildings the player
    /// walks between. Conifers are banned inside it because a fir in a kitchen
    /// garden reads as a Christmas decoration; everywhere outside it the winter
    /// forest is the deliberate conifer mass that encloses the village.
    /// </summary>
    private static readonly Vector2 SettlementCoreMin = new(-58f, -124f);

    private static readonly Vector2 SettlementCoreMax = new(58f, 46f);

    private static bool IsInsideSettlementCore(Vector2 point) =>
        point.X > SettlementCoreMin.X && point.X < SettlementCoreMax.X
        && point.Y > SettlementCoreMin.Y && point.Y < SettlementCoreMax.Y;

    private static IReadOnlyList<global::Urman.Godot.Act1ConnectedWorld.YardLot>? _residentialLots;

    /// <summary>
    /// VIS-084: the core box predates the north street (lots to z 143) and the far
    /// bank (x 63–124). A point within <paramref name="grow"/> metres of any authored
    /// house lot is residential too, wherever it lies.
    /// </summary>
    private static bool IsInsideResidentialLot(Vector2 point, float grow)
    {
        _residentialLots ??= global::Urman.Godot.Act1ConnectedWorld.YardLots();
        foreach (var lot in _residentialLots)
        {
            var forward = new Vector2(Mathf.Sin(Mathf.DegToRad(lot.Yaw)), Mathf.Cos(Mathf.DegToRad(lot.Yaw)));
            var side = new Vector2(forward.Y, -forward.X);
            var offset = point - lot.Centre;
            if (Mathf.Abs(offset.Dot(side)) <= lot.Size.X * .5f + grow
                && Mathf.Abs(offset.Dot(forward)) <= lot.Size.Y * .5f + grow) return true;
        }
        return false;
    }

    /// <summary>
    /// Positions of the boundary thicket planted on the settlement envelope.
    /// <see cref="BuildDensifiedPlan"/> records them and
    /// <see cref="BuildForestBoundaryCollision"/> gives each one its physical
    /// block, so the walkable edge and its visible thicket are the same line.
    /// </summary>
    private readonly List<Vector2> _forestBoundarySegments = new();

    private void EmitBelt(
        List<(Vector2, string)> planned,
        RandomNumberGenerator rng,
        Vector2 position,
        int row,
        int rowCount,
        Vector2 jittered,
        int edge)
    {
        if (InsideMosqueKeepOut(position))
        {
            return;
        }

        // Trees never stand in the river channel: the ravine is the village/
        // forest boundary and must stay readable as water and banks. That is
        // only true over the span the player can reach. Where the ravine leaves
        // the ring the forest closes straight across it, because otherwise the
        // ice corridor would be the one outward view — and the one walk — left
        // open along the terrain edge.
        if (position.X > -58f && position.X < 86f
            && System.Math.Abs(position.Y - (float)AgentBAct1HeightField.RiverMeander(position.X)) < 9f)
        {
            return;
        }

        // The ring is one continuous tall conifer mass: the bough ladder rises
        // toward the outside where it has to close the skyline, the young firs
        // stay as regrowth between the tall trunks, and bare winter deciduous
        // trees break the silhouette so the wall does not read as one repeated
        // tile.
        var outward = rowCount <= 1 ? 1f : 1f - row / (float)(rowCount - 1);
        var treeRoll = rng.Randf();
        string variant;
        if (treeRoll < 0.52f + 0.22f * outward)
        {
            variant = rng.Randf() switch
            {
                < 0.24f => "WinterSpruce_6",
                < 0.58f => "WinterSpruce_5",
                < 0.88f => "WinterSpruce_4",
                _ => "WinterSpruce_3"
            };
        }
        else if (treeRoll < 0.78f)
        {
            variant = row >= 4 ? "WinterSpruce_3" : "WinterSpruce_2";
        }
        else
        {
            variant = treeRoll switch
            {
                < 0.85f => "WinterBirch_1",
                < 0.90f => "WinterBirch_2",
                < 0.94f => "WinterLinden_1",
                < 0.97f => "WinterMaple_1",
                _ => "WinterSpruce_1"
            };
        }

        planned.Add((position, variant));
        _forestRingTrees.Add((jittered, position, variant, row, edge));

        var undergrowth = rng.Randf() switch
        {
            < 0.3f => "Fern_1",
            < 0.55f => "Sedge_1",
            < 0.8f => "Shrub_1",
            _ => "GrassTuft_1"
        };
        var undergrowthPoint = position + new Vector2(rng.RandfRange(-1.6f, 1.6f), rng.RandfRange(-1.6f, 1.6f));
        // VIS-029/085: every rng call still happens, only the entry is dropped. If
        // the draws were skipped as well, every later root, garden fixture and
        // boundary contact in this build would move, which the plan explicitly
        // forbids. The outer two rows are seen only as skyline behind the tall
        // trunks, so their 0-2 m understory is the layer that carries cost and
        // carries no read.
        if (!SuppressOuterRowUnderstory(row, rowCount)) planned.Add((undergrowthPoint, undergrowth));
        else _outerRowUnderstorySkipped++;
        // Every ring row gets a second understory plant. The wall has to read
        // from the ground up: between the tall trunks the eye must meet needles
        // and snow rather than the open field behind.
        var filler = rng.Randf() switch
        {
            < 0.34f => "WinterSpruce_" + rng.RandiRange(1, 2),
            < 0.6f => "Shrub_2",
            < 0.8f => "Fern_0",
            _ => "Sedge_2"
        };
        var fillerPoint = position + new Vector2(rng.RandfRange(-2.4f, 2.4f), rng.RandfRange(-2.4f, 2.4f));
        if (!SuppressOuterRowUnderstory(row, rowCount)) planned.Add((fillerPoint, filler));
        else _outerRowUnderstorySkipped++;
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
    /// Persistent wind-driven snow; the opening night escalates the storm. Same
    /// single weather owner, same toggle path (ART-010 unchanged).
    /// </summary>
    internal void SetOpeningBlizzard(bool active)
    {
        if (_rain is null) return;
        _rain.Direction = new Vector3(-1f, active ? -.18f : -.28f, .22f);
        _rain.InitialVelocityMin = active ? 11f : 6f;
        _rain.InitialVelocityMax = active ? 16f : 10f;
        _rain.Spread = active ? 12f : 18f;
        _rain.EmissionBoxExtents = new Vector3(12f, 3f, 12f);
        _rain.Amount = active ? 5200 : 4000;
        ((ShaderMaterial)((QuadMesh)_rain.Mesh).Material).SetShaderParameter("snow_velocity",
            _rain.Direction.Normalized() * ((_rain.InitialVelocityMin + _rain.InitialVelocityMax) * .5f));
        _rain.Restart();
    }

    private void BuildSnow()
    {
        _rain = new CpuParticles3D
        {
            Name = "AgentBSnow",
            Emitting = true,
            Amount = 4000,
            // Keep the fixed particle budget around the view instead of spending
            // most of a five-second flight below the terrain/downwind of the player.
            Lifetime = 3.0,
            LocalCoords = false,
            Preprocess = 3.0,
            Mesh = CreateSnowflakeMesh(),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            Gravity = new Vector3(0f, -0.32f, 0f),
            Randomness = 0.55f,
            LifetimeRandomness = 0.5f,
            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 1.5f,
            ColorRamp = WinterParticleSurfaces.Fade()
        };
        AddChild(_rain);
        SetOpeningBlizzard(false);
        // The emitter and its fresh ShaderMaterial are created only here (and _rain.Mesh
        // is never reassigned), so this is the single place that can invalidate the
        // pushed snow uniforms without retaining a reference to the material itself.
        InvalidateSnowPresentationParameters();
    }

    private void BuildWindStreaks()
    {
        // VIS-076 (V4): the rare world-space abstraction of moving air. It is built
        // next to the snow emitter because it reads that emitter's own wind, so the
        // opening blizzard and the ordinary drift move the ribbons the same way they
        // move the flakes, and a second weather state can never appear.
        _windStreaks = new AgentBWindStreaks(() => ExteriorWindVector) { Name = "AgentBWindStreaks" };
        _windStreaks.SetMeta("presentationOwner", nameof(Act1ConnectedWorld));
        AddChild(_windStreaks);
        _windStreaks.BuildPool();
        _windStreaks.SetPresentationEnabled(_exteriorPresentationEnabled && !_sheltered);
        _windStreaks.SetRegionMode(IsForestRimRegion(new Vector2(GlobalPosition.X, GlobalPosition.Z)));
        SetMeta("windStreakOwner", _windStreaks.Name);
        SetMeta("windStreakPolicy", AgentBWindStreaks.WindStreakMode switch
        {
            "off" => "session-disabled (disable test), the frame equals the pre-feature frame",
            "on" => "session-forced for a capture run; never set by an ordinary launch",
            _ => "auto: exterior presentation, wind above the gate, reduced motion respected"
        });
        SetMeta("windStreakBudget", "pool 16, village cap 8, forest-rim cap 16, lifetime 0.6-2.0 s");
    }

    /// <summary>
    /// The world's wind, published by the single weather owner: the same direction and
    /// mean speed the snow emitter integrates and the same vector <c>SetOpeningBlizzard</c>
    /// pushes into the flake shader as <c>snow_velocity</c>. Read-only for everyone else.
    /// </summary>
    internal Vector3 ExteriorWindVector
    {
        get
        {
            if (_rain is not { } snow) return Vector3.Zero;
            var direction = snow.Direction;
            if (direction == Vector3.Zero) return Vector3.Zero;
            var mean = (snow.InitialVelocityMin + snow.InitialVelocityMax) * .5f;
            return direction.Normalized() * mean * (snow.Emitting ? 1f : 0f);
        }
    }

    /// <summary>
    /// Same region predicate PlantFoliage uses to grade a plant as Kara rim rather than
    /// village: kept here so the wind register and the plant register cannot diverge.
    /// </summary>
    internal static bool IsForestRimRegion(Vector2 worldXZ) =>
        worldXZ.X <= -40f || worldXZ.X >= 134f || worldXZ.Y <= -128f;

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
                    ("KaraGestureBounce", new Vector3(-2.8f, 1.80f, -117.0f), "71847c", 0.045f, 13.0f),
                    // VIS-074 / H2-2: the forest's one anomaly family. A dim amber that
                    // belongs to nothing, deep enough that the 0.045 fog leaves only a
                    // smear between trunks; never red, never near the path.
                    ("KaraDeepAmberAnomaly", new Vector3(6.5f, 2.10f, -134.0f), "c88a3e", 0.11f, 9.0f)
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
            if (name == "KaraDeepAmberAnomaly")
            {
                // Height is above its own ground, so the glow is never buried in a rise.
                light.Position = position with { Y = (float)AgentBAct1HeightField.Ground(position.X, position.Z) + position.Y };
                light.SetMeta("landmarkRole", "kara-night-anomaly-amber");
            }
            root.AddChild(light);
            _karaAccentLights.Add(light);
        }

        SetMeta("karaAccentLightCount", _karaAccentLights.Count);
        SetMeta("karaAccentLightPolicy", "restrained cool bounce cues plus one dim deep amber anomaly (VIS-074), visible only for exterior Kara presentation; no point-light hotspots, no red");
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

    private static QuadMesh CreateSnowflakeMesh() => WinterParticleSurfaces.Snow(.026f, .50f, roomExclusion: true);

    private readonly List<(Node3D Owner, Vector3 Centre, Vector3 Half)> _weatherShelters = new();
    private int _windowWeatherShelter = -1;
    public void RegisterWeatherShelter(Node3D owner, Vector3 centre, Vector3 half, bool windowView = false)
    {
        if (windowView)
        {
            _windowWeatherShelter = _weatherShelters.Count;
            // The selected shelter may have changed; never compare against the old one.
            InvalidateSnowPresentationParameters();
        }
        _weatherShelters.Add((owner, centre, half));
    }
    private bool WeatherSheltered(Vector3 point)
        => _weatherShelters.Any(shelter => GodotObject.IsInstanceValid(shelter.Owner)
            && Inside(shelter.Owner.ToLocal(point) - shelter.Centre, shelter.Half));
    private static bool Inside(Vector3 p, Vector3 h)
        => Mathf.Abs(p.X) < h.X && Mathf.Abs(p.Y) < h.Y && Mathf.Abs(p.Z) < h.Z;

    // Last uniforms pushed for the snow shelter. A null entry means "not written
    // yet" and forces a write; everything is reset when the emitter's material is
    // (re)created or the atmosphere owner switches zone or shelter. No Material
    // reference is kept, so this cache adds nothing to the shutdown resource count.
    private bool? _snowShelterEnabled;
    private Transform3D? _snowShelterOwner;
    private Vector3? _snowShelterCentre;
    private Vector3? _snowShelterHalf;

    /// <summary>Forget the pushed snow uniforms so the next pass rewrites all of them.</summary>
    private void InvalidateSnowPresentationParameters()
    {
        _snowShelterEnabled = null;
        _snowShelterOwner = null;
        _snowShelterCentre = null;
        _snowShelterHalf = null;
    }

    private void UpdateSnowPresentation(Vector3 focus)
    {
        if (_rain is null) return;
        var material = (ShaderMaterial)((QuadMesh)_rain.Mesh).Material;
        var windowView = false;
        var validShelter = _windowWeatherShelter >= 0
            && GodotObject.IsInstanceValid(_weatherShelters[_windowWeatherShelter].Owner);
        if (_snowShelterEnabled != validShelter)
        {
            material.SetShaderParameter("shelter_enabled", validShelter);
            _snowShelterEnabled = validShelter;
        }
        if (validShelter)
        {
            // Pilot: only the real glazed main room. The sealed underground wing
            // and other logical interiors retain their existing weather policy.
            var shelter = _weatherShelters[_windowWeatherShelter];
            // Each uniform is a pure function of one authored value, so re-pushing an
            // equal value is unobservable; the AffineInverse is only needed when the
            // owner transform itself changed.
            var owner = shelter.Owner.GlobalTransform;
            if (_snowShelterOwner != owner)
            {
                material.SetShaderParameter("shelter_from_world", owner.AffineInverse());
                _snowShelterOwner = owner;
            }
            if (_snowShelterCentre != shelter.Centre)
            {
                material.SetShaderParameter("shelter_centre", shelter.Centre);
                _snowShelterCentre = shelter.Centre;
            }
            if (_snowShelterHalf != shelter.Half)
            {
                material.SetShaderParameter("shelter_half", shelter.Half);
                _snowShelterHalf = shelter.Half;
            }
            windowView = _windowSnowView && Inside(shelter.Owner.ToLocal(focus) - shelter.Centre, shelter.Half);
        }
        var enabled = windowView || (_exteriorPresentationEnabled && !_sheltered && !WeatherSheltered(focus));
        // Compared against the live properties and meta, so an external change is
        // still re-asserted exactly as the previous unconditional writes did.
        if (_rain.Emitting != enabled) _rain.Emitting = enabled;
        if (_rain.Visible != enabled) _rain.Visible = enabled;
        if (_rain.GetMeta("windowSnowView", false).AsBool() != windowView)
            _rain.SetMeta("windowSnowView", windowView);
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
            UpdateSnowPresentation(focus);
            // Spawn upwind; world-space flakes keep their trajectory as the player turns.
            _rain.GlobalPosition = focus + new Vector3(10f, 2f, -2f);
        }

        if (_windStreaks is not null)
        {
            // VIS-076: the register follows the viewer's own region, so the same gust
            // is delicate over the village and stranger at the Kara rim; the tint is
            // read from the atmosphere owner's fog colour, never written there.
            _windStreaks.SetRegionMode(IsForestRimRegion(new Vector2(focus.X, focus.Z)));
            if (_environmentResource is { } atmosphere && atmosphere.FogEnabled)
                _windStreaks.SetWorldTint(atmosphere.FogLightColor, "exterior-fog");
            _windStreaks.Refresh(focus, delta);
        }
    }

    // VIS-006 §5.1: environment, sky and sun values are owned by the authored
    // atmosphere profile (Act1ConnectedWorld.TuneConnectedAct1Atmosphere), which
    // runs right after this on every zone switch and wrote every property this
    // method used to write. Only the Kara accent lights, which the profile does
    // not touch, are still set here.
    private void ApplyKaraAccents(bool night)
    {
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
