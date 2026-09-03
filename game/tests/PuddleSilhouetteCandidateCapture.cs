using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Render-only comparison for the existing puddle proxy silhouette.
///
/// Every benchmark is instantiated independently. The candidate replaces only
/// the Mesh property of the existing fifteen PuddlePatch nodes in memory; it
/// retains the source ShaderMaterial (roughness 0.90), exact road-relief
/// collider, node tree, collision shapes and production scene files.
/// </summary>
public partial class PuddleSilhouetteCandidateCapture : Node
{
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;
    private const int TileWidth = CaptureWidth / 2;
    private const int TileHeight = CaptureHeight / 2;
    private const int ExpectedPatchCount = 15;
    private const int PatchesPerCluster = 3;
    private const float ContactToleranceMeters = 0.005f;
    private const float ExpectedSourceRoughness = 0.90f;
    private const float SourceCylinderBottom = -0.006f;

    private static readonly SceneSpec[] Scenes =
    [
        new("day_street", "res://scenes/zones/style_benchmark_day_street.tscn", "Road",
            ["PuddleNear", "PuddleMiddle", "PuddleFar"],
            new Vector3(0f, 1.7f, 12.5f), new Vector3(0f, 1.45f, -7.5f),
            new Vector3(0.25f, 0.72f, 6.4f), new Vector3(-0.65f, 0.07f, 5.2f)),
        new("kara_urman_edge", "res://scenes/zones/style_benchmark_kara_urman_night.tscn", "PathNear",
            ["BoundaryWetPatch"],
            new Vector3(0f, 1.7f, 12.5f), new Vector3(0.55f, 1.35f, -7.2f),
            new Vector3(0.72f, 0.68f, 6.4f), new Vector3(0.72f, 0.07f, 4.25f)),
        new("zirat_road", "res://scenes/zones/chapter1_zirat_road.tscn", "Road",
            ["ZiratWetPatch"],
            new Vector3(0f, 1.7f, 12.5f), new Vector3(0f, 1.45f, -7.5f),
            new Vector3(-0.65f, 0.68f, 8.75f), new Vector3(-0.72f, 0.08f, 6.8f))
    ];

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception exception)
        {
            WriteErrorReceipt(exception);
            GD.PushError($"puddle-silhouette-candidate: harness error: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        var outputDirectory = ProjectSettings.GlobalizePath(
            "res://../docs/urman_knowledge_base/art/puddle_silhouette_candidate");
        EnsureOutputDirectory(outputDirectory);
        RemoveCandidateOutputs(outputDirectory);

        var sceneReceipts = new List<SceneReceipt>(Scenes.Length);
        var captures = new List<CaptureReceipt>(Scenes.Length);
        var totalPatches = 0;
        var totalReplacements = 0;

        try
        {
            foreach (var spec in Scenes)
            {
                var sceneReceipt = await CaptureDiagnosticAsync(spec, outputDirectory);
                sceneReceipts.Add(sceneReceipt);
                captures.Add(sceneReceipt.Capture);
                totalPatches += sceneReceipt.PatchCount;
                totalReplacements += sceneReceipt.CandidateMeshReplacements;
            }

            var contactsPass = sceneReceipts.All(receipt =>
                receipt.SourceContacts.All(contact => contact.WithinTolerance && contact.ColliderMatchesRelief)
                && receipt.CandidateContacts.All(contact => contact.WithinTolerance && contact.ColliderMatchesRelief));
            var sourcePass = sceneReceipts.All(receipt => receipt.SourceMaterialPass && receipt.SourceMeshPass);
            var isolationPass = sceneReceipts.All(receipt => receipt.OnlyMeshesReplaced
                && receipt.CollisionObjectCountBefore == receipt.CollisionObjectCountAfter
                && receipt.MaterialReferencesPreserved
                && receipt.SourceMeshesRestored);
            var rendered = contactsPass && sourcePass && isolationPass
                && totalPatches == ExpectedPatchCount && totalReplacements == ExpectedPatchCount
                && captures.Count == Scenes.Length;
            if (!rendered)
            {
                throw new InvalidOperationException(
                    $"Silhouette candidate gate failed: contacts={contactsPass}, source={sourcePass}, isolation={isolationPass}, " +
                    $"patches={totalPatches}/{ExpectedPatchCount}, replacements={totalReplacements}/{ExpectedPatchCount}, captures={captures.Count}/{Scenes.Length}.");
            }

            WriteReceipt(outputDirectory, sceneReceipts, captures, totalPatches, totalReplacements);
            GD.Print($"puddle-silhouette-candidate: status=OPEN contact=PASS source=PASS patches={totalPatches} mesh-replacements={totalReplacements} captures={captures.Count} output={outputDirectory}");
        }
        finally
        {
            await ReleaseAllChildrenAsync();
        }

        GetTree().Quit(0);
    }

    private async Task<SceneReceipt> CaptureDiagnosticAsync(SceneSpec spec, string outputDirectory)
    {
        var packed = ResourceLoader.Load<PackedScene>(spec.ScenePath)
            ?? throw new InvalidOperationException($"Could not load benchmark scene {spec.ScenePath}.");
        var instance = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException($"Could not instantiate benchmark scene {spec.ScenePath}.");
        AddChild(instance);
        await WaitForPhysicsAsync();

        try
        {
            var relief = instance.GetNodeOrNull<StaticBody3D>(spec.ReliefName)
                ?? throw new InvalidOperationException($"{spec.Name}: required relief {spec.ReliefName} is missing.");
            RequireReliefContract(spec, relief);
            var patches = FindPatches(instance, spec);
            var collisionBefore = CountCollisionObjects(instance);
            var sourceMaterialPass = patches.All(patch => patch.Patch.MaterialOverride is ShaderMaterial material
                && ReferenceEquals(material, patch.SourceMaterial)
                && Approximately(ReadRoughness(material), ExpectedSourceRoughness));
            var sourceMeshPass = patches.All(patch => patch.SourceMesh is CylinderMesh
                && Approximately(patch.SourceMesh.GetAabb().Position.Y, SourceCylinderBottom));
            if (!sourceMaterialPass || !sourceMeshPass)
            {
                throw new InvalidOperationException($"{spec.Name}: source puddles must remain CylinderMesh + source roughness {ExpectedSourceRoughness:0.00}.");
            }

            var sourceContacts = patches.Select(patch => SampleContact(instance, relief, patch)).ToList();
            RequireContacts(spec.Name, "source", sourceContacts);
            var sourceOverview = await RenderTileAsync(instance, spec.OverviewCameraPosition, spec.OverviewCameraTarget, $"{spec.Name}_source_overview");
            var sourceDetail = await RenderTileAsync(instance, spec.DetailCameraPosition, spec.DetailCameraTarget, $"{spec.Name}_source_detail");

            var replacements = ApplyCandidateMeshes(patches);
            var collisionAfter = CountCollisionObjects(instance);
            var materialsPreserved = patches.All(patch => ReferenceEquals(patch.Patch.MaterialOverride, patch.SourceMaterial)
                && Approximately(ReadRoughness(patch.SourceMaterial), ExpectedSourceRoughness));
            var onlyMeshesReplaced = replacements == patches.Count
                && patches.All(patch => patch.Patch.Mesh is ArrayMesh
                    && patch.Patch.GetChildCount() == 0
                    && patch.Patch.GetChildren().All(child => child is not CollisionObject3D and not CollisionShape3D));
            if (collisionBefore != collisionAfter || !materialsPreserved || !onlyMeshesReplaced)
            {
                throw new InvalidOperationException($"{spec.Name}: test candidate changed a material or collision owner instead of only replacing meshes.");
            }

            var candidateContacts = patches.Select(patch => SampleContact(instance, relief, patch)).ToList();
            RequireContacts(spec.Name, "candidate", candidateContacts);
            var candidateOverview = await RenderTileAsync(instance, spec.OverviewCameraPosition, spec.OverviewCameraTarget, $"{spec.Name}_candidate_overview");
            var candidateDetail = await RenderTileAsync(instance, spec.DetailCameraPosition, spec.DetailCameraTarget, $"{spec.Name}_candidate_detail");

            var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_{spec.Name}_puddle_silhouette_diagnostic_1080p.png");
            var output = ComposeDiagnosticSheet(sourceOverview, candidateOverview, sourceDetail, candidateDetail);
            var saveError = output.SavePng(outputPath);
            if (saveError != Error.Ok)
            {
                throw new InvalidOperationException($"Could not save puddle silhouette diagnostic {outputPath}: {saveError}.");
            }

            RestoreSourceMeshes(patches);
            var sourceMeshesRestored = patches.All(patch => ReferenceEquals(patch.Patch.Mesh, patch.SourceMesh));
            if (!sourceMeshesRestored)
            {
                throw new InvalidOperationException($"{spec.Name}: source mesh references could not be restored after the test-only capture.");
            }

            var root = ProjectSettings.GlobalizePath("res://..");
            var capture = new CaptureReceipt(
                spec.Name,
                System.IO.Path.GetRelativePath(root, outputPath).Replace('\\', '/'),
                CaptureWidth,
                CaptureHeight,
                HashFile(outputPath),
                new[]
                {
                    new TileReceipt("source_flat_overview", TileWidth, TileHeight, HashImage(sourceOverview)),
                    new TileReceipt("candidate_faceted_overview", TileWidth, TileHeight, HashImage(candidateOverview)),
                    new TileReceipt("source_flat_detail", TileWidth, TileHeight, HashImage(sourceDetail)),
                    new TileReceipt("candidate_faceted_detail", TileWidth, TileHeight, HashImage(candidateDetail))
                });
            return new SceneReceipt(spec.Name, spec.ScenePath, spec.ReliefName, patches.Count, replacements,
                sourceMaterialPass, sourceMeshPass, materialsPreserved, onlyMeshesReplaced,
                collisionBefore, collisionAfter, sourceMeshesRestored, sourceContacts, candidateContacts, capture);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(instance)) instance.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static List<PatchRun> FindPatches(Node3D instance, SceneSpec spec)
    {
        var clusters = FindPuddleClusters(instance);
        if (clusters.Count != spec.ClusterNames.Length || !clusters.Select(cluster => cluster.Name.ToString()).Order().SequenceEqual(spec.ClusterNames.Order()))
        {
            throw new InvalidOperationException($"{spec.Name}: expected only {string.Join(", ", spec.ClusterNames)} puddle clusters.");
        }

        var patches = new List<PatchRun>(spec.ClusterNames.Length * PatchesPerCluster);
        foreach (var clusterName in spec.ClusterNames)
        {
            var cluster = instance.GetNodeOrNull<Node3D>(clusterName)
                ?? throw new InvalidOperationException($"{spec.Name}: cluster {clusterName} is missing.");
            var meshPatches = cluster.GetChildren().OfType<MeshInstance3D>().ToArray();
            // Godot deterministically suffixes sibling display names after the
            // first `PuddlePatch`; the stable contract is the exact three
            // direct MeshInstance3D children, as in the wetness-matrix test.
            if (meshPatches.Length != PatchesPerCluster)
            {
                throw new InvalidOperationException($"{spec.Name}:{clusterName}: expected exactly three PuddlePatch meshes.");
            }
            foreach (var (patch, patchIndex) in meshPatches.Select((patch, index) => (patch, index)))
            {
                if (patch.Mesh is null || patch.MaterialOverride is not ShaderMaterial sourceMaterial)
                {
                    throw new InvalidOperationException($"{spec.Name}:{clusterName}:{patchIndex}: mesh/material contract is incomplete.");
                }
                patches.Add(new PatchRun(spec.Name, clusterName, patchIndex, patch, patch.Mesh, sourceMaterial));
            }
        }
        return patches;
    }

    private static int ApplyCandidateMeshes(IEnumerable<PatchRun> patches)
    {
        var replacements = 0;
        foreach (var patch in patches)
        {
            patch.Patch.Mesh = CreateFacetedPuddleMesh();
            replacements++;
        }
        return replacements;
    }

    private static void RestoreSourceMeshes(IEnumerable<PatchRun> patches)
    {
        foreach (var patch in patches) patch.Patch.Mesh = patch.SourceMesh;
    }

    /// <summary>
    /// A shallow, open faceted surface: outer vertices retain the source mesh
    /// bottom at -6 mm, while a small raised inner ring removes the cylinder's
    /// vertical wall. It deliberately has no bottom face, side-wall geometry,
    /// collider or material assignment.
    /// </summary>
    private static ArrayMesh CreateFacetedPuddleMesh()
    {
        Vector2[] boundary =
        [
            new(-0.50f, -0.08f), new(-0.37f, -0.34f), new(-0.11f, -0.48f), new(0.25f, -0.43f), new(0.49f, -0.18f),
            new(0.45f, 0.16f), new(0.24f, 0.40f), new(-0.07f, 0.47f), new(-0.39f, 0.31f), new(-0.53f, 0.08f)
        ];
        float[] outerHeights = [-0.0060f, -0.0055f, -0.0058f, -0.0052f, -0.0059f, -0.0054f, -0.0057f, -0.0053f, -0.0058f, -0.0055f];
        float[] innerHeights = [0.0015f, 0.0030f, 0.0020f, 0.0038f, 0.0018f, 0.0033f, 0.0024f, 0.0036f, 0.0019f, 0.0031f];
        var vertices = new List<Vector3>(90);
        var normals = new List<Vector3>(90);
        var uvs = new List<Vector2>(90);
        var center = new Vector3(0.01f, 0.0026f, -0.015f);
        for (var index = 0; index < boundary.Length; index++)
        {
            var next = (index + 1) % boundary.Length;
            var outer = new Vector3(boundary[index].X, outerHeights[index], boundary[index].Y);
            var outerNext = new Vector3(boundary[next].X, outerHeights[next], boundary[next].Y);
            var inner = new Vector3(boundary[index].X * 0.64f, innerHeights[index], boundary[index].Y * 0.64f);
            var innerNext = new Vector3(boundary[next].X * 0.64f, innerHeights[next], boundary[next].Y * 0.64f);
            AddTriangle(vertices, normals, uvs, outer, outerNext, inner);
            AddTriangle(vertices, normals, uvs, inner, outerNext, innerNext);
            AddTriangle(vertices, normals, uvs, inner, innerNext, center);
        }

        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static void AddTriangle(ICollection<Vector3> vertices, ICollection<Vector3> normals, ICollection<Vector2> uvs,
        Vector3 a, Vector3 b, Vector3 c)
    {
        var normal = (b - a).Cross(c - a).Normalized();
        if (normal.Y < 0f)
        {
            (b, c) = (c, b);
            normal = -normal;
        }
        foreach (var vertex in new[] { a, b, c })
        {
            vertices.Add(vertex);
            normals.Add(normal);
            uvs.Add(new Vector2(vertex.X + 0.5f, vertex.Z + 0.5f));
        }
    }

    private async Task<Image> RenderTileAsync(Node3D scene, Vector3 cameraPosition, Vector3 cameraTarget, string name)
    {
        var parent = scene.GetParent() ?? throw new InvalidOperationException("Diagnostic scene must have a parent before rendering.");
        var viewport = new SubViewport
        {
            Name = name,
            Size = new Vector2I(TileWidth, TileHeight),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);
        parent.RemoveChild(scene);
        viewport.AddChild(scene);
        var camera = new Camera3D { Name = "PuddleSilhouetteCamera", Position = cameraPosition, Fov = 75f, Current = true };
        viewport.AddChild(camera);
        camera.LookAt(cameraTarget, Vector3.Up);
        for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var image = viewport.GetTexture().GetImage();
        viewport.RemoveChild(scene);
        parent.AddChild(scene);
        viewport.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (image is null || image.IsEmpty() || image.GetWidth() != TileWidth || image.GetHeight() != TileHeight)
        {
            throw new InvalidOperationException($"{name}: real {TileWidth}x{TileHeight} render required.");
        }
        if (image.GetFormat() != Image.Format.Rgba8) image.Convert(Image.Format.Rgba8);
        return image;
    }

    private static Image ComposeDiagnosticSheet(Image sourceOverview, Image candidateOverview, Image sourceDetail, Image candidateDetail)
    {
        var sheet = Image.CreateEmpty(CaptureWidth, CaptureHeight, false, Image.Format.Rgba8);
        sheet.Fill(Color.FromHtml("171b1a"));
        sheet.BlitRect(sourceOverview, new Rect2I(0, 0, TileWidth, TileHeight), new Vector2I(0, 0));
        sheet.BlitRect(candidateOverview, new Rect2I(0, 0, TileWidth, TileHeight), new Vector2I(TileWidth, 0));
        sheet.BlitRect(sourceDetail, new Rect2I(0, 0, TileWidth, TileHeight), new Vector2I(0, TileHeight));
        sheet.BlitRect(candidateDetail, new Rect2I(0, 0, TileWidth, TileHeight), new Vector2I(TileWidth, TileHeight));
        return sheet;
    }

    private static ContactSample SampleContact(Node3D scene, StaticBody3D relief, PatchRun patch)
    {
        var center = patch.Patch.GlobalPosition;
        var query = PhysicsRayQueryParameters3D.Create(new Vector3(center.X, 3f, center.Z), new Vector3(center.X, -1f, center.Z));
        query.CollisionMask = 1;
        query.CollideWithAreas = false;
        var result = scene.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0 || !result.ContainsKey("position"))
        {
            return ContactSample.Open(patch, WorldBottom(patch.Patch), "No layer-1 relief surface was sampled beneath puddle patch.");
        }
        var hit = result["position"].AsVector3();
        var collider = result.ContainsKey("collider") ? result["collider"].AsGodotObject() as CollisionObject3D : null;
        var ownerMatches = collider is not null && collider.GetInstanceId() == relief.GetInstanceId()
            && collider.GetMeta("reliefGrid").AsString() == "9x28"
            && collider.GetMeta("reliefCollisionCells").AsInt32() == 216;
        var bottom = WorldBottom(patch.Patch);
        var gap = bottom - hit.Y;
        var pass = ownerMatches && Mathf.Abs(gap) <= ContactToleranceMeters;
        var owner = collider is null ? "none" : $"{collider.Name}<{collider.GetType().Name}>";
        var note = pass ? $"Contact within ±{Format(ContactToleranceMeters)} m." :
            $"{patch.Scene}:{patch.Cluster}:{patch.PatchIndex} gap={Format(gap)} m, owner={(ownerMatches ? "expected-relief" : "wrong-relief")}:{owner}.";
        return new ContactSample(patch.Scene, patch.Cluster, patch.PatchIndex, bottom, hit.Y, gap, pass, ownerMatches, note);
    }

    private static void RequireContacts(string scene, string stage, IEnumerable<ContactSample> contacts)
    {
        var failures = contacts.Where(contact => !contact.WithinTolerance || !contact.ColliderMatchesRelief).ToArray();
        if (failures.Length > 0)
        {
            throw new InvalidOperationException($"{scene}:{stage}: exact-owner contact failed: {string.Join(" | ", failures.Select(failure => failure.Note))}");
        }
    }

    private static float WorldBottom(MeshInstance3D mesh)
    {
        var aabb = mesh.GetAabb();
        var maximum = aabb.Position + aabb.Size;
        var minimum = float.MaxValue;
        for (var x = 0; x < 2; x++)
        for (var y = 0; y < 2; y++)
        for (var z = 0; z < 2; z++)
        {
            var local = new Vector3(x == 0 ? aabb.Position.X : maximum.X, y == 0 ? aabb.Position.Y : maximum.Y, z == 0 ? aabb.Position.Z : maximum.Z);
            minimum = Mathf.Min(minimum, (mesh.GlobalTransform * local).Y);
        }
        return minimum;
    }

    private static List<Node3D> FindPuddleClusters(Node root)
    {
        var clusters = new List<Node3D>();
        Walk(root, clusters);
        return clusters;
        static void Walk(Node node, ICollection<Node3D> found)
        {
            if (node is Node3D node3D && node.HasMeta("puddleGeometry")) found.Add(node3D);
            foreach (var child in node.GetChildren()) Walk(child, found);
        }
    }

    private static int CountCollisionObjects(Node root)
    {
        var count = 0;
        Walk(root);
        return count;
        void Walk(Node node)
        {
            if (node is CollisionObject3D) count++;
            foreach (var child in node.GetChildren()) Walk(child);
        }
    }

    private static void RequireReliefContract(SceneSpec spec, StaticBody3D relief)
    {
        if (relief.GetMeta("reliefGrid").AsString() != "9x28" || relief.GetMeta("reliefCollisionCells").AsInt32() != 216)
        {
            throw new InvalidOperationException($"{spec.Name}:{spec.ReliefName}: exact road-relief contract mismatch.");
        }
    }

    private async Task WaitForPhysicsAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task ReleaseAllChildrenAsync()
    {
        foreach (var child in GetChildren()) if (GodotObject.IsInstanceValid(child)) child.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        PainterlyMaterialLibrary.ClearCacheForHeadlessTests();
    }

    private static void EnsureOutputDirectory(string directory)
    {
        if (DirAccess.MakeDirRecursiveAbsolute(directory) != Error.Ok)
        {
            throw new InvalidOperationException($"Could not create puddle silhouette candidate directory {directory}.");
        }
    }

    private static void RemoveCandidateOutputs(string directory)
    {
        foreach (var spec in Scenes)
        {
            var output = System.IO.Path.Combine(directory, $"godot_{spec.Name}_puddle_silhouette_diagnostic_1080p.png");
            if (System.IO.File.Exists(output)) System.IO.File.Delete(output);
        }
    }

    private void WriteReceipt(string outputDirectory, IReadOnlyList<SceneReceipt> scenes, IReadOnlyList<CaptureReceipt> captures,
        int totalPatches, int totalReplacements)
    {
        var manifest = new
        {
            schema_version = 1,
            kind = "urman.godot_puddle_silhouette_candidate",
            version = "v1",
            captured_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            status = "OPEN",
            silhouette_acceptance = "OPEN",
            renderer = RenderingServer.GetRenderingDevice() is null ? "real-driver-required" : "Godot RenderingDevice",
            non_mutation = new
            {
                test_only = true,
                production_scenes_changed = false,
                runtime_or_shader_changed = false,
                material_roughness_changed = false,
                source_runtime_roughness = ExpectedSourceRoughness,
                collider_added_or_changed = false,
                saves_or_narrative_changed = false
            },
            diagnostic_layout = new
            {
                width = CaptureWidth,
                height = CaptureHeight,
                tile_width = TileWidth,
                tile_height = TileHeight,
                cells = new[] { "source_flat_overview", "candidate_faceted_overview", "source_flat_detail", "candidate_faceted_detail" },
                reading_order = "left-to-right, top-to-bottom; source flat cylinder is left, faceted candidate is right"
            },
            exact_relief_contact = new
            {
                status = "PASS",
                tolerance_m = ContactToleranceMeters,
                raw_production_origins_changed = false,
                source_and_candidate_samples_pass = true
            },
            candidate_mesh = new
            {
                source_mesh = nameof(CylinderMesh),
                candidate_mesh = nameof(ArrayMesh),
                profile = "open shallow faceted irregular surface; no bottom or vertical rim",
                source_bottom_local_y = SourceCylinderBottom,
                candidate_mesh_replacements = totalReplacements,
                expected_replacements = ExpectedPatchCount,
                materials_preserved = true,
                collision_objects_added = 0,
                source_mesh_references_restored = true
            },
            totals = new { patches = totalPatches, expected_patches = ExpectedPatchCount, captures = captures.Count, expected_captures = Scenes.Length },
            scenes,
            captures
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        System.IO.File.WriteAllText(System.IO.Path.Combine(outputDirectory, "puddle_silhouette_candidate_manifest.json"), JsonSerializer.Serialize(manifest, options) + System.Environment.NewLine);
        System.IO.File.WriteAllText(System.IO.Path.Combine(outputDirectory, "README.md"), BuildReadme(manifest));
    }

    private void WriteErrorReceipt(Exception exception)
    {
        var outputDirectory = ProjectSettings.GlobalizePath("res://../docs/urman_knowledge_base/art/puddle_silhouette_candidate");
        try
        {
            EnsureOutputDirectory(outputDirectory);
            RemoveCandidateOutputs(outputDirectory);
            var manifest = new { schema_version = 1, kind = "urman.godot_puddle_silhouette_candidate", version = "v1", status = "ERROR", silhouette_acceptance = "OPEN", rendered = false, error = exception.ToString() };
            System.IO.File.WriteAllText(System.IO.Path.Combine(outputDirectory, "puddle_silhouette_candidate_manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + System.Environment.NewLine);
            System.IO.File.WriteAllText(System.IO.Path.Combine(outputDirectory, "README.md"), $"# Puddle silhouette candidate\n\nStatus: **ERROR**. No candidate PNGs are accepted.\n\n```text\n{exception}\n```\n");
        }
        catch { /* Keep the first failure. */ }
    }

    private static string BuildReadme(object manifest)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(manifest));
        var root = document.RootElement;
        var captures = root.GetProperty("captures").GetArrayLength();
        var builder = new StringBuilder();
        builder.AppendLine("# Puddle silhouette candidate v1");
        builder.AppendLine();
        builder.AppendLine("Status: **OPEN — diagnostic production-candidate evidence only; wetness, geometry acceptance and art lock are not claimed.**");
        builder.AppendLine();
        builder.AppendLine("Each 1 920 × 1 080 contact sheet compares the existing flat `CylinderMesh` puddle proxy with a test-only irregular shallow `ArrayMesh`. The source view is on the left, the candidate view is on the right; the upper row is the fixed first-person overview and the lower row is a close diagnostic view. The material reference and source `roughness_value=0.90` are identical on both sides.");
        builder.AppendLine();
        builder.AppendLine($"- Exact relief-collider contact: **PASS** (±{Format(ContactToleranceMeters)} m, source and candidate)");
        builder.AppendLine("- Candidate mesh operations: **15/15** in-memory `Mesh` replacements; no colliders added");
        builder.AppendLine("- Source scenes, shader, PainterlyMaterialLibrary, roughness, saves and narrative state: **unchanged**");
        builder.AppendLine($"- Diagnostic PNGs: **{captures}/3**");
        builder.AppendLine();
        builder.AppendLine("The candidate intentionally has no bottom face or vertical cylindrical side wall. It remains a bounded silhouette experiment: visual assessment must still decide whether it reads as damp, avoids a plate/rim silhouette, survives traversal, and stays compatible with the Painterly Low-Poly 3D style bible.");
        builder.AppendLine();
        builder.AppendLine("## Verification");
        builder.AppendLine();
        builder.AppendLine("Run `./eng/capture-puddle-silhouette-candidate.sh`. The wrapper builds the C# project, runs the real Metal/Forward+ capture, pins benchmark-scene hashes, validates exact owner/contact samples, confirms material/collision isolation and decodes the output PNG dimensions/SHA-256 values.");
        return builder.ToString();
    }

    private static float ReadRoughness(ShaderMaterial material) => material.GetShaderParameter("roughness_value").AsSingle();
    private static bool Approximately(float value, float expected) => Mathf.Abs(value - expected) <= 0.0005f;
    private static string Format(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path))).ToLowerInvariant();
    private static string HashImage(Image image) => Convert.ToHexString(SHA256.HashData(image.GetData().ToArray())).ToLowerInvariant();

    private sealed record SceneSpec(string Name, string ScenePath, string ReliefName, string[] ClusterNames,
        Vector3 OverviewCameraPosition, Vector3 OverviewCameraTarget, Vector3 DetailCameraPosition, Vector3 DetailCameraTarget);
    private sealed record PatchRun(string Scene, string Cluster, int PatchIndex, MeshInstance3D Patch, Mesh SourceMesh, ShaderMaterial SourceMaterial);
    private sealed record ContactSample(string Scene, string Cluster, int PatchIndex, float PuddleBottomY, float ReliefSurfaceY,
        float GapMeters, bool WithinTolerance, bool ColliderMatchesRelief, string Note)
    {
        public static ContactSample Open(PatchRun patch, float bottom, string note) => new(patch.Scene, patch.Cluster, patch.PatchIndex, bottom, float.NaN, float.NaN, false, false, note);
    }
    private sealed record TileReceipt(string Cell, int Width, int Height, string PixelSha256);
    private sealed record CaptureReceipt(string Scene, string Output, int Width, int Height, string Sha256, TileReceipt[] Tiles);
    private sealed record SceneReceipt(string Name, string Scene, string Relief, int PatchCount, int CandidateMeshReplacements,
        bool SourceMaterialPass, bool SourceMeshPass, bool MaterialReferencesPreserved, bool OnlyMeshesReplaced,
        int CollisionObjectCountBefore, int CollisionObjectCountAfter, bool SourceMeshesRestored,
        List<ContactSample> SourceContacts, List<ContactSample> CandidateContacts, CaptureReceipt Capture);
}
