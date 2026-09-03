using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Render-only roughness matrix for the existing puddle dressing.
///
/// This deliberately has no production owner: benchmark scenes are loaded only
/// for the duration of the harness, every candidate material is a fresh in-memory
/// ShaderMaterial duplicate, and each source material must remain at 0.90.  A
/// complete contact and source-material proof is required before a candidate row
/// can render.  It never enters RuntimeBridge or writes scenes, GLBs, shaders,
/// saves, campaign data, or PainterlyMaterialLibrary.
/// </summary>
public partial class WetnessCandidateMatrixV2Capture : Node
{
    private const float ExpectedSourceRoughness = 0.90f;
    // Deliberately stricter than v1. Raw production origins are reported, but
    // a candidate can render only after its isolated in-memory presentation
    // instance contacts the exact relief owner within five millimetres.
    private const float ContactToleranceMeters = 0.005f;
    private const int ExpectedClusterCount = 5;
    private const int ExpectedPatchCount = 15;
    private const int PatchesPerCluster = 3;
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;

    private static readonly float[] CandidateRoughnesses = [0.40f, 0.50f, 0.60f];

    private static readonly SceneSpec[] Scenes =
    [
        new("day_street", "res://scenes/zones/style_benchmark_day_street.tscn",
            ["PuddleNear", "PuddleMiddle", "PuddleFar"], "Road", true,
            new Vector3(0f, 1.7f, 12.5f), new Vector3(0f, 1.45f, -7.5f)),
        new("house_old_pc", "res://scenes/zones/style_benchmark_house_pc.tscn",
            [], null, false, Vector3.Zero, Vector3.Zero),
        new("kara_urman_edge", "res://scenes/zones/style_benchmark_kara_urman_night.tscn",
            ["BoundaryWetPatch"], "PathNear", true,
            new Vector3(0f, 1.7f, 12.5f), new Vector3(0.55f, 1.35f, -7.2f)),
        new("zirat_road", "res://scenes/zones/chapter1_zirat_road.tscn",
            ["ZiratWetPatch"], "Road", true,
            new Vector3(0f, 1.7f, 12.5f), new Vector3(0f, 1.45f, -7.5f))
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
            GD.PushError($"wetness-candidate-matrix-v2: harness error: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        var outputDirectory = ProjectSettings.GlobalizePath(
            "res://../docs/urman_knowledge_base/art/wetness_candidate_matrix_v2");
        EnsureOutputDirectory(outputDirectory);
        RemoveMatrixOutputs(outputDirectory);

        var sceneRuns = new List<SceneRun>(Scenes.Length);
        var allPatches = new List<PatchRun>(ExpectedPatchCount);
        var contactReasons = new List<string>();
        var contactPass = true;
        var sourceRoughnessPass = true;
        var observedClusters = 0;
        var observedPatches = 0;

        try
        {
            foreach (var spec in Scenes)
            {
                var packed = ResourceLoader.Load<PackedScene>(spec.ScenePath)
                    ?? throw new InvalidOperationException($"Could not load benchmark scene {spec.ScenePath}.");
                var instance = packed.Instantiate<Node3D>()
                    ?? throw new InvalidOperationException($"Could not instantiate benchmark scene {spec.ScenePath}.");
                AddChild(instance);
                await WaitForPhysicsAsync();

                var sceneRun = new SceneRun(spec, instance);
                sceneRuns.Add(sceneRun);
                var metadataClusters = FindPuddleClusters(instance);
                observedClusters += metadataClusters.Count;
                observedPatches += metadataClusters.Sum(cluster =>
                    cluster.GetChildren().OfType<MeshInstance3D>().Count());
                if (metadataClusters.Count != spec.ClusterNames.Length)
                {
                    contactPass = false;
                    contactReasons.Add($"{spec.Name}: expected {spec.ClusterNames.Length} metadata puddle clusters, found {metadataClusters.Count}.");
                }

                foreach (var clusterName in spec.ClusterNames)
                {
                    var cluster = instance.GetNodeOrNull<Node3D>(clusterName);
                    if (cluster is null)
                    {
                        contactPass = false;
                        contactReasons.Add($"{spec.Name}:{clusterName}: puddle cluster is missing.");
                        continue;
                    }

                    var patchNodes = cluster.GetChildren().OfType<MeshInstance3D>().ToArray();
                    if (patchNodes.Length != PatchesPerCluster)
                    {
                        contactPass = false;
                        contactReasons.Add($"{spec.Name}:{clusterName}: expected {PatchesPerCluster} PuddlePatch meshes, found {patchNodes.Length}.");
                    }

                    foreach (var (patch, patchIndex) in patchNodes.Select((mesh, index) => (mesh, index)))
                    {
                        if (patch.MaterialOverride is not ShaderMaterial sourceMaterial)
                        {
                            contactPass = false;
                            contactReasons.Add($"{spec.Name}:{clusterName}:{patchIndex}: missing ShaderMaterial override.");
                            continue;
                        }

                        var sourceRoughness = ReadRoughness(sourceMaterial);
                        if (!Approximately(sourceRoughness, ExpectedSourceRoughness))
                        {
                            sourceRoughnessPass = false;
                            contactPass = false;
                            contactReasons.Add($"{spec.Name}:{clusterName}:{patchIndex}: source roughness was {Format(sourceRoughness)}, expected {Format(ExpectedSourceRoughness)}.");
                        }

                        var patchRun = new PatchRun(spec.Name, clusterName, patchIndex, patch, sourceMaterial);
                        allPatches.Add(patchRun);
                        sceneRun.Patches.Add(patchRun);
                    }
                }

                if (spec.ReliefName is not null)
                {
                    var relief = instance.GetNodeOrNull<StaticBody3D>(spec.ReliefName);
                    if (relief is null)
                    {
                        contactPass = false;
                        contactReasons.Add($"{spec.Name}:{spec.ReliefName}: relief body is missing.");
                    }
                    else
                    {
                        // Record the raw authored instance before applying any
                        // test-only candidate alignment. This is evidence of a
                        // production-origin issue, never a scene mutation.
                        foreach (var patch in sceneRun.Patches)
                        {
                            var raw = SampleContact(instance, relief, patch);
                            sceneRun.RawContacts.Add(raw);
                            if (!raw.ColliderMatchesRelief)
                            {
                                contactPass = false;
                                contactReasons.Add(raw.Note);
                                continue;
                            }

                            if (!raw.WithinTolerance && raw.GapMeters is float rawGap)
                            {
                                patch.TestOnlyAlignmentMeters = -rawGap;
                                patch.Patch.GlobalPosition += new Vector3(0f, -rawGap, 0f);
                            }
                        }

                        await WaitForPhysicsAsync();
                        foreach (var patch in sceneRun.Patches)
                        {
                            var aligned = SampleContact(instance, relief, patch);
                            sceneRun.Contacts.Add(aligned);
                            if (!aligned.WithinTolerance)
                            {
                                contactPass = false;
                                contactReasons.Add(aligned.Note);
                            }
                        }
                    }
                }

                // No following raycast can see a previous benchmark's collider.
                RemoveChild(instance);
                await WaitForPhysicsAsync();
            }

            if (observedClusters != ExpectedClusterCount)
            {
                contactPass = false;
                contactReasons.Add($"Expected exactly {ExpectedClusterCount} metadata puddle clusters, found {observedClusters}.");
            }
            if (observedPatches != ExpectedPatchCount || allPatches.Count != ExpectedPatchCount)
            {
                contactPass = false;
                contactReasons.Add($"Expected exactly {ExpectedPatchCount} ShaderMaterial-backed PuddlePatch meshes, found observed={observedPatches}, backed={allPatches.Count}.");
            }

            var rows = new List<RowReceipt>(CandidateRoughnesses.Length);
            var captures = new List<CaptureReceipt>();
            var sourceStillPass = allPatches.All(patch => Approximately(ReadRoughness(patch.SourceMaterial), ExpectedSourceRoughness));
            sourceRoughnessPass &= sourceStillPass;
            if (!sourceStillPass)
            {
                contactPass = false;
                contactReasons.Add("Source/runtime puddle roughness changed during isolated contact inspection.");
            }

            if (contactPass && sourceRoughnessPass)
            {
                foreach (var roughness in CandidateRoughnesses)
                {
                    var rowClones = ApplyCandidateRow(allPatches, roughness);
                    var sourceUnchanged = allPatches.All(patch => Approximately(ReadRoughness(patch.SourceMaterial), ExpectedSourceRoughness));
                    var rowIsolationPass = rowClones == ExpectedPatchCount && sourceUnchanged
                        && allPatches.All(patch => patch.Patch.MaterialOverride is ShaderMaterial candidate
                            && Approximately(ReadRoughness(candidate), roughness));
                    var row = new RowReceipt(roughness, rowClones, rowIsolationPass, sourceUnchanged, []);
                    rows.Add(row);
                    if (!rowIsolationPass)
                    {
                        throw new InvalidOperationException($"Candidate row {Format(roughness)} did not prove 15 isolated in-memory ShaderMaterial clones.");
                    }

                    foreach (var sceneRun in sceneRuns.Where(scene => scene.Spec.CaptureCandidate))
                    {
                        var capture = await CaptureAsync(sceneRun, outputDirectory, roughness);
                        row.Captures.Add(capture);
                        captures.Add(capture);
                    }
                }
            }

            var expectedCaptureCount = CandidateRoughnesses.Length * Scenes.Count(scene => scene.CaptureCandidate);
            var candidateIsolationPass = rows.Count == CandidateRoughnesses.Length
                && rows.All(row => row.IsolationPass && row.CloneCount == ExpectedPatchCount && row.SourceStillUnchanged);
            var rendered = contactPass && candidateIsolationPass && captures.Count == expectedCaptureCount;
            var renderReason = rendered
                ? "Matrix renders are in-memory candidate evidence only; roughness acceptance remains OPEN."
                : "No candidate matrix PNGs rendered because the contact/source/isolation gate is OPEN.";

            RestoreSourceOverrides(allPatches);
            var finalSourcePass = allPatches.All(patch =>
                ReferenceEquals(patch.Patch.MaterialOverride, patch.SourceMaterial)
                && Approximately(ReadRoughness(patch.SourceMaterial), ExpectedSourceRoughness));
            sourceRoughnessPass &= finalSourcePass;
            if (!finalSourcePass)
            {
                throw new InvalidOperationException("Source material overrides could not be restored after the in-memory matrix.");
            }

            WriteReceipt(outputDirectory, contactPass, sourceRoughnessPass, observedClusters, observedPatches,
                allPatches.Count, candidateIsolationPass, rendered, renderReason, contactReasons, sceneRuns, rows, captures);
            GD.Print($"wetness-candidate-matrix-v2: status=OPEN contact={(contactPass ? "PASS" : "OPEN")} source={(sourceRoughnessPass ? "PASS" : "OPEN")} rows={rows.Count}/{CandidateRoughnesses.Length} captures={captures.Count}/{expectedCaptureCount} output={outputDirectory}");
        }
        finally
        {
            await ReleaseScenesAsync(sceneRuns);
        }

        GetTree().Quit(0);
    }

    private static int ApplyCandidateRow(IEnumerable<PatchRun> patches, float roughness)
    {
        var count = 0;
        foreach (var patch in patches)
        {
            var candidate = patch.SourceMaterial.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException($"Could not duplicate source material for {patch.Scene}:{patch.Cluster}:{patch.PatchIndex}.");
            candidate.SetShaderParameter("roughness_value", roughness);
            patch.Patch.MaterialOverride = candidate;
            if (!Approximately(ReadRoughness(candidate), roughness)
                || !Approximately(ReadRoughness(patch.SourceMaterial), ExpectedSourceRoughness))
            {
                throw new InvalidOperationException($"Candidate material isolation failed for {patch.Scene}:{patch.Cluster}:{patch.PatchIndex}.");
            }
            count++;
        }
        return count;
    }

    private static void RestoreSourceOverrides(IEnumerable<PatchRun> patches)
    {
        foreach (var patch in patches)
        {
            patch.Patch.MaterialOverride = patch.SourceMaterial;
        }
    }

    private async Task<CaptureReceipt> CaptureAsync(SceneRun sceneRun, string outputDirectory, float roughness)
    {
        var viewport = new SubViewport
        {
            Name = $"WetnessMatrix_{sceneRun.Spec.Name}_{Format(roughness)}",
            Size = new Vector2I(CaptureWidth, CaptureHeight),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);
        if (sceneRun.Instance.GetParent() is Node previousParent)
        {
            previousParent.RemoveChild(sceneRun.Instance);
        }
        viewport.AddChild(sceneRun.Instance);
        var camera = new Camera3D { Name = "WetnessMatrixCamera", Position = sceneRun.Spec.CameraPosition, Fov = 75f, Current = true };
        viewport.AddChild(camera);
        camera.LookAt(sceneRun.Spec.CameraTarget, Vector3.Up);
        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var image = viewport.GetTexture().GetImage();
        var width = image?.GetWidth() ?? 0;
        var height = image?.GetHeight() ?? 0;
        if (image is null || image.IsEmpty() || width != CaptureWidth || height != CaptureHeight)
        {
            viewport.RemoveChild(sceneRun.Instance);
            viewport.Free();
            throw new InvalidOperationException($"Matrix capture requires a real {CaptureWidth}x{CaptureHeight} frame for {sceneRun.Spec.Name}@{Format(roughness)}.");
        }

        var outputPath = System.IO.Path.Combine(outputDirectory,
            $"godot_{sceneRun.Spec.Name}_wetness_roughness_{FormatForFile(roughness)}_1080p.png");
        var saveError = image.SavePng(outputPath);
        viewport.RemoveChild(sceneRun.Instance);
        viewport.Free();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (saveError != Error.Ok)
        {
            throw new InvalidOperationException($"Could not save {outputPath}: {saveError}.");
        }

        var root = ProjectSettings.GlobalizePath("res://..");
        return new CaptureReceipt(sceneRun.Spec.Name,
            System.IO.Path.GetRelativePath(root, outputPath).Replace('\\', '/'), width, height, roughness, HashFile(outputPath));
    }

    private ContactSample SampleContact(Node3D scene, StaticBody3D relief, PatchRun patch)
    {
        var center = patch.Patch.GlobalPosition;
        var query = PhysicsRayQueryParameters3D.Create(new Vector3(center.X, 3f, center.Z), new Vector3(center.X, -1f, center.Z));
        query.CollisionMask = 1;
        query.CollideWithAreas = false;
        var result = scene.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0 || !result.ContainsKey("position"))
        {
            return ContactSample.Open(patch, WorldBottom(patch.Patch), null, "No layer-1 relief surface was sampled beneath the puddle patch.");
        }

        var hit = result["position"].AsVector3();
        var collider = result.ContainsKey("collider") ? result["collider"].AsGodotObject() as CollisionObject3D : null;
        var ownerMatches = collider is not null && collider.GetInstanceId() == relief.GetInstanceId()
            && collider.GetMeta("reliefGrid").AsString() == "9x28"
            && collider.GetMeta("reliefCollisionCells").AsInt32() == 216;
        var bottom = WorldBottom(patch.Patch);
        var gap = bottom - hit.Y;
        var pass = ownerMatches && Mathf.Abs(gap) <= ContactToleranceMeters;
        var description = collider is null ? "none" : $"{collider.Name}<{collider.GetType().Name}>";
        var note = pass ? $"Contact within ±{Format(ContactToleranceMeters)} m." :
            $"{patch.Scene}:{patch.Cluster}:{patch.PatchIndex} gap={Format(gap)} m (bottom={Format(bottom)}, relief={Format(hit.Y)}); owner={(ownerMatches ? "expected-relief" : "wrong-relief")}:{description}, tolerance=±{Format(ContactToleranceMeters)} m.";
        return new ContactSample(patch.Scene, patch.Cluster, patch.PatchIndex, bottom, hit.Y, gap, pass, ownerMatches, note);
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
        static void Walk(Node node, ICollection<Node3D> destination)
        {
            if (node is Node3D node3D && node.HasMeta("puddleGeometry")) destination.Add(node3D);
            foreach (var child in node.GetChildren()) Walk(child, destination);
        }
    }

    private async Task WaitForPhysicsAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task ReleaseScenesAsync(IEnumerable<SceneRun> sceneRuns)
    {
        foreach (var sceneRun in sceneRuns)
            if (GodotObject.IsInstanceValid(sceneRun.Instance)) sceneRun.Instance.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        PainterlyMaterialLibrary.ClearCacheForHeadlessTests();
    }

    private void WriteReceipt(string outputDirectory, bool contactPass, bool sourcePass, int clusters, int patches,
        int materialPatches, bool isolationPass, bool rendered, string renderReason, IReadOnlyList<string> contactReasons,
        IReadOnlyList<SceneRun> sceneRuns, IReadOnlyList<RowReceipt> rows, IReadOnlyList<CaptureReceipt> captures)
    {
        var manifest = new
        {
            schema_version = 2,
            kind = "urman.godot_wetness_candidate_matrix",
            version = "v2",
            captured_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            status = "OPEN",
            roughness_acceptance = "OPEN",
            candidate_roughness_matrix = CandidateRoughnesses,
            expected_source_roughness = ExpectedSourceRoughness,
            contact_tolerance_m = ContactToleranceMeters,
            contact_gate = new { status = contactPass ? "PASS" : "OPEN", reason = contactPass ? "All test-only candidate puddle bottoms contact the sampled authored relief within tolerance and exact owner boundary." : "Contact gate did not pass; matrix rendering was suppressed.", tolerance_applies_to = "test-only in-memory candidate instances", details = contactReasons },
            production_puddle_origins = new { status = sceneRuns.SelectMany(scene => scene.RawContacts).All(sample => sample.WithinTolerance) ? "PASS" : "OPEN", changed = false, reason = "Raw authored positions are measured before any candidate-local alignment. No source scene or runtime puddle origin is written by this harness." },
            source_runtime_material = new { status = sourcePass ? "PASS" : "OPEN", expected_roughness = ExpectedSourceRoughness, candidate_clones_are_in_memory_only = true, source_overrides_restored_before_shutdown = sourcePass },
            candidate_material_isolation = new { status = isolationPass ? "PASS" : "OPEN", exact_patch_clone_count_per_row = ExpectedPatchCount, matrix_rows = CandidateRoughnesses.Length, aggregate_shader_material_clones = rows.Sum(row => row.CloneCount) },
            totals = new { clusters, expected_clusters = ExpectedClusterCount, patches, expected_patches = ExpectedPatchCount, material_backed_patches = materialPatches, expected_captures = CandidateRoughnesses.Length * Scenes.Count(scene => scene.CaptureCandidate) },
            rendered,
            render_reason = renderReason,
            scenes = sceneRuns.Select(scene => new { name = scene.Spec.Name, scene = scene.Spec.ScenePath, expected_puddle_clusters = scene.Spec.ClusterNames, relief = scene.Spec.ReliefName, patch_count = scene.Patches.Count, capture_candidate = scene.Spec.CaptureCandidate, contact = new { status = scene.Contacts.Count == 0 || scene.Contacts.All(sample => sample.WithinTolerance) ? "PASS" : "OPEN", raw_production_origin_samples = scene.RawContacts, test_only_candidate_samples = scene.Contacts, test_only_alignment_meters = scene.Patches.Select(patch => new { patch.Cluster, patch.PatchIndex, patch.TestOnlyAlignmentMeters }) } }),
            rows = rows.Select(row => new { candidate_roughness = row.Roughness, candidate_shader_material_clones = row.CloneCount, isolation = row.IsolationPass ? "PASS" : "OPEN", source_still_unchanged = row.SourceStillUnchanged, captures = row.Captures }),
            captures
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        System.IO.File.WriteAllText(System.IO.Path.Combine(outputDirectory, "wetness_candidate_matrix_v2_manifest.json"), JsonSerializer.Serialize(manifest, options) + System.Environment.NewLine);
        System.IO.File.WriteAllText(System.IO.Path.Combine(outputDirectory, "README.md"), BuildReadme(manifest));
    }

    private void WriteErrorReceipt(Exception exception)
    {
        var outputDirectory = ProjectSettings.GlobalizePath("res://../docs/urman_knowledge_base/art/wetness_candidate_matrix_v2");
        try
        {
            EnsureOutputDirectory(outputDirectory);
            RemoveMatrixOutputs(outputDirectory);
            var error = new { schema_version = 2, kind = "urman.godot_wetness_candidate_matrix", version = "v2", status = "ERROR", roughness_acceptance = "OPEN", rendered = false, render_reason = "Harness error; no candidate PNGs are accepted.", error = exception.ToString() };
            System.IO.File.WriteAllText(System.IO.Path.Combine(outputDirectory, "wetness_candidate_matrix_v2_manifest.json"), JsonSerializer.Serialize(error, new JsonSerializerOptions { WriteIndented = true }) + System.Environment.NewLine);
            System.IO.File.WriteAllText(System.IO.Path.Combine(outputDirectory, "README.md"), $"# Wetness roughness matrix v2\n\nStatus: **ERROR**. No candidate PNGs are accepted.\n\n```text\n{exception}\n```\n");
        }
        catch { /* Preserve the first failure. */ }
    }

    private static string BuildReadme(object manifest)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(manifest));
        var root = document.RootElement;
        var contact = root.GetProperty("contact_gate").GetProperty("status").GetString();
        var source = root.GetProperty("source_runtime_material").GetProperty("status").GetString();
        var isolation = root.GetProperty("candidate_material_isolation").GetProperty("status").GetString();
        var origins = root.GetProperty("production_puddle_origins").GetProperty("status").GetString();
        var rendered = root.GetProperty("rendered").GetBoolean();
        var builder = new StringBuilder();
        builder.AppendLine("# Wetness roughness matrix v2");
        builder.AppendLine();
        builder.AppendLine("Status: **OPEN — production-candidate evidence only; wetness/roughness acceptance and art lock are not claimed.**");
        builder.AppendLine();
        builder.AppendLine("The test instantiates each benchmark alone for contact sampling and records its raw authored puddle origins before any test-only candidate action. All production origins must now pass the strict contact gate without candidate-local alignment. It creates fresh in-memory `ShaderMaterial` clones for each matrix row (`0.40`, `0.50`, `0.60`). Source/runtime material roughness remains `0.90`; source overrides are restored before shutdown. No shader, runtime registry, save, narrative, scene, GLB or PainterlyMaterialLibrary owner is changed.");
        builder.AppendLine();
        builder.AppendLine($"- Candidate contact + exact relief-owner gate: **{contact}** (±{Format(ContactToleranceMeters)} m)");
        builder.AppendLine($"- Raw production puddle origins at the same ≤5 mm threshold: **{origins}** (reported only; never changed)");
        builder.AppendLine($"- Source/runtime `roughness_value=0.90`: **{source}**");
        builder.AppendLine($"- Per-row clone isolation: **{isolation}** (15 fresh clones × 3 rows; in-memory only)");
        builder.AppendLine($"- Candidate PNGs: **{(rendered ? "9 written after all pre-render gates passed" : "suppressed; no-render receipt")}**");
        builder.AppendLine();
        builder.AppendLine("Each PNG is only an isolated test candidate. The harness retains candidate-local alignment only as a fail-closed diagnostic; this receipt requires it to remain zero for every production patch. It does not activate production puddle materials and cannot close the wetness, art-lock, temporal-comfort, authored-geometry, cultural or release-hardware gates.");
        builder.AppendLine();
        builder.AppendLine("## Verification");
        builder.AppendLine();
        builder.AppendLine("Run `./eng/capture-wetness-candidate-matrix-v2.sh`. The wrapper validates manifest shape, source roughness, exact contact owner, all three rows, 9 PNG dimensions and SHA-256 values.");
        return builder.ToString();
    }

    private static void EnsureOutputDirectory(string directory)
    {
        if (DirAccess.MakeDirRecursiveAbsolute(directory) != Error.Ok)
            throw new InvalidOperationException($"Could not create wetness-matrix directory {directory}.");
    }

    private static void RemoveMatrixOutputs(string directory)
    {
        foreach (var roughness in CandidateRoughnesses)
        foreach (var spec in Scenes.Where(scene => scene.CaptureCandidate))
        {
            var path = System.IO.Path.Combine(directory, $"godot_{spec.Name}_wetness_roughness_{FormatForFile(roughness)}_1080p.png");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    private static float ReadRoughness(ShaderMaterial material) => material.GetShaderParameter("roughness_value").AsSingle();
    private static bool Approximately(float value, float expected) => Mathf.Abs(value - expected) <= 0.0005f;
    private static string Format(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    private static string FormatForFile(float value) => value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', '_');
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path))).ToLowerInvariant();

    private sealed record SceneSpec(string Name, string ScenePath, string[] ClusterNames, string? ReliefName, bool CaptureCandidate, Vector3 CameraPosition, Vector3 CameraTarget);
    private sealed class SceneRun
    {
        public SceneRun(SceneSpec spec, Node3D instance) { Spec = spec; Instance = instance; }
        public SceneSpec Spec { get; }
        public Node3D Instance { get; }
        public List<PatchRun> Patches { get; } = [];
        public List<ContactSample> RawContacts { get; } = [];
        public List<ContactSample> Contacts { get; } = [];
    }
    private sealed class PatchRun
    {
        public PatchRun(string scene, string cluster, int patchIndex, MeshInstance3D patch, ShaderMaterial sourceMaterial)
        {
            Scene = scene;
            Cluster = cluster;
            PatchIndex = patchIndex;
            Patch = patch;
            SourceMaterial = sourceMaterial;
        }
        public string Scene { get; }
        public string Cluster { get; }
        public int PatchIndex { get; }
        public MeshInstance3D Patch { get; }
        public ShaderMaterial SourceMaterial { get; }
        public float TestOnlyAlignmentMeters { get; set; }
    }
    private sealed class RowReceipt
    {
        public RowReceipt(float roughness, int cloneCount, bool isolationPass, bool sourceStillUnchanged, List<CaptureReceipt> captures) { Roughness = roughness; CloneCount = cloneCount; IsolationPass = isolationPass; SourceStillUnchanged = sourceStillUnchanged; Captures = captures; }
        public float Roughness { get; }
        public int CloneCount { get; }
        public bool IsolationPass { get; }
        public bool SourceStillUnchanged { get; }
        public List<CaptureReceipt> Captures { get; }
    }
    private sealed record ContactSample(string Scene, string Cluster, int PatchIndex, float PuddleBottomY, float? ReliefSurfaceY, float? GapMeters, bool WithinTolerance, bool ColliderMatchesRelief, string Note)
    {
        public static ContactSample Open(PatchRun patch, float bottom, float? relief, string note) => new(patch.Scene, patch.Cluster, patch.PatchIndex, bottom, relief, relief.HasValue ? bottom - relief.Value : null, false, false, note);
    }
    private sealed record CaptureReceipt(string Scene, string Output, int Width, int Height, float CandidateRoughness, string Sha256);
}
