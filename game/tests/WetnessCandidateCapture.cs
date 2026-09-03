using System.Globalization;
using System.Text;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Render-only wetness candidate harness.
///
/// The source benchmark scenes are instantiated one at a time so their authored
/// relief colliders can be sampled without cross-scene physics aliasing. Only the
/// fifteen existing puddle-patch ShaderMaterials are duplicated in memory. The
/// source/runtime materials stay at roughness 0.90; the candidate duplicate uses
/// the existing shader uniform at roughness 0.50. The contact gate controls
/// rendering, so an OPEN contact result produces a no-render receipt. This
/// harness never enters the game bootstrap and never changes gameplay state,
/// scenes, saves, narrative data or the runtime material library.
/// </summary>
public partial class WetnessCandidateCapture : Node
{
    private const float CandidateRoughness = 0.50f;
    private const float ExpectedSourceRoughness = 0.90f;
    private const float ContactToleranceMeters = 0.010f;
    private const int ExpectedClusterCount = 5;
    private const int ExpectedPatchCount = 15;
    private const int PatchesPerCluster = 3;
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;

    private static readonly SceneSpec[] Scenes =
    [
        new(
            "day_street",
            "res://scenes/zones/style_benchmark_day_street.tscn",
            ["PuddleNear", "PuddleMiddle", "PuddleFar"],
            "Road",
            true,
            new Vector3(0f, 1.7f, 12.5f),
            new Vector3(0f, 1.45f, -7.5f)),
        new(
            "house_old_pc",
            "res://scenes/zones/style_benchmark_house_pc.tscn",
            [],
            null,
            false,
            Vector3.Zero,
            Vector3.Zero),
        new(
            "kara_urman_edge",
            "res://scenes/zones/style_benchmark_kara_urman_night.tscn",
            ["BoundaryWetPatch"],
            "PathNear",
            true,
            new Vector3(0f, 1.7f, 12.5f),
            new Vector3(0.55f, 1.35f, -7.2f)),
        new(
            "zirat_road",
            "res://scenes/zones/chapter1_zirat_road.tscn",
            ["ZiratWetPatch"],
            "Road",
            true,
            new Vector3(0f, 1.7f, 12.5f),
            new Vector3(0f, 1.45f, -7.5f))
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
            GD.PushError($"wetness-candidate: harness error: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        var outputDirectory = ProjectSettings.GlobalizePath(
            "res://../docs/urman_knowledge_base/art/wetness_candidate");
        EnsureOutputDirectory(outputDirectory);
        RemoveCandidateFrames(outputDirectory);

        var sceneRuns = new List<SceneRun>(Scenes.Length);
        var patches = new List<PatchRun>(ExpectedPatchCount);
        var contacts = new List<ContactSample>(ExpectedPatchCount);
        var observedClusterCount = 0;
        var observedPatchCount = 0;
        var contactPass = true;
        var contactReasons = new List<string>();
        var sourceRoughnessPass = true;

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
            observedClusterCount += metadataClusters.Count;
            observedPatchCount += metadataClusters.Sum(cluster =>
                cluster.GetChildren().OfType<MeshInstance3D>().Count());

            if (metadataClusters.Count != spec.ClusterNames.Length)
            {
                contactPass = false;
                contactReasons.Add(
                    $"{spec.Name}: expected {spec.ClusterNames.Length} metadata puddle clusters, found {metadataClusters.Count}.");
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

                var patchNodes = cluster.GetChildren()
                    .OfType<MeshInstance3D>()
                    .ToArray();
                if (patchNodes.Length != PatchesPerCluster)
                {
                    contactPass = false;
                    contactReasons.Add(
                        $"{spec.Name}:{clusterName}: expected {PatchesPerCluster} PuddlePatch meshes, found {patchNodes.Length}.");
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
                    sourceRoughnessPass &= Approximately(sourceRoughness, ExpectedSourceRoughness);
                    if (!Approximately(sourceRoughness, ExpectedSourceRoughness))
                    {
                        contactPass = false;
                        contactReasons.Add(
                            $"{spec.Name}:{clusterName}:{patchIndex}: source roughness was {Format(sourceRoughness)}, expected {Format(ExpectedSourceRoughness)}.");
                    }

                    var patchRun = new PatchRun(spec.Name, clusterName, patchIndex, patch, sourceMaterial);
                    patches.Add(patchRun);
                    sceneRun.Patches.Add(patchRun);

                    if (spec.ReliefName is null)
                    {
                        continue;
                    }

                    var road = instance.GetNodeOrNull<StaticBody3D>(spec.ReliefName);
                    if (road is null)
                    {
                        contactPass = false;
                        contactReasons.Add($"{spec.Name}:{spec.ReliefName}: relief body is missing.");
                        continue;
                    }

                    var sample = SampleReliefSurface(instance, road, patchRun);
                    contacts.Add(sample);
                    sceneRun.Contacts.Add(sample);
                    if (!sample.WithinTolerance)
                    {
                        contactPass = false;
                        contactReasons.Add(sample.Note);
                    }
                }
            }

            // Keep only one benchmark scene in the active physics world at a
            // time. The instances remain unparented for a later render-only
            // capture; their relief colliders are no longer queryable while the
            // next scene is sampled.
            RemoveChild(instance);
            await WaitForPhysicsAsync();
        }

        if (observedClusterCount != ExpectedClusterCount)
        {
            contactPass = false;
            contactReasons.Add(
                $"Expected exactly {ExpectedClusterCount} metadata puddle clusters, found {observedClusterCount}.");
        }

        if (observedPatchCount != ExpectedPatchCount)
        {
            contactPass = false;
            contactReasons.Add(
                $"Expected exactly {ExpectedPatchCount} PuddlePatch meshes, found {observedPatchCount}.");
        }

        if (patches.Count != ExpectedPatchCount)
        {
            contactPass = false;
            contactReasons.Add(
                $"Expected exactly {ExpectedPatchCount} ShaderMaterial-backed patches, found {patches.Count}.");
        }

        var sourceRuntimeStatus = sourceRoughnessPass ? "PASS" : "OPEN";
        var captures = new List<CaptureReceipt>();
        var candidateCloneCount = 0;
        var candidateIsolationPass = false;

        if (patches.Count == ExpectedPatchCount && sourceRoughnessPass)
        {
            foreach (var patch in patches)
            {
                var candidateMaterial = patch.SourceMaterial.Duplicate() as ShaderMaterial
                    ?? throw new InvalidOperationException(
                        $"Could not duplicate puddle ShaderMaterial for {patch.Scene}:{patch.Cluster}:{patch.PatchIndex}.");
                candidateMaterial.SetShaderParameter("roughness_value", CandidateRoughness);
                patch.Patch.MaterialOverride = candidateMaterial;

                var candidateRoughness = ReadRoughness(candidateMaterial);
                var sourceStillUnchanged = Approximately(
                    ReadRoughness(patch.SourceMaterial),
                    ExpectedSourceRoughness);
                if (!Approximately(candidateRoughness, CandidateRoughness) || !sourceStillUnchanged)
                {
                    throw new InvalidOperationException(
                        $"Puddle material isolation failed for {patch.Scene}:{patch.Cluster}:{patch.PatchIndex}: source={Format(ReadRoughness(patch.SourceMaterial))}, candidate={Format(candidateRoughness)}.");
                }

                candidateCloneCount++;
            }

            var sourceMaterialsStillUnchanged = patches.All(patch => Approximately(
                ReadRoughness(patch.SourceMaterial),
                ExpectedSourceRoughness));
            sourceRoughnessPass &= sourceMaterialsStillUnchanged;

            if (!sourceMaterialsStillUnchanged)
            {
                contactPass = false;
                contactReasons.Add("Source/runtime puddle roughness changed after candidate cloning.");
            }

            candidateIsolationPass = candidateCloneCount == ExpectedPatchCount && sourceMaterialsStillUnchanged;
        }

        sourceRuntimeStatus = sourceRoughnessPass ? "PASS" : "OPEN";
        if (candidateIsolationPass && contactPass)
        {
            foreach (var sceneRun in sceneRuns.Where(run => run.Spec.CaptureCandidate))
            {
                captures.Add(await CaptureAsync(sceneRun, outputDirectory));
            }
        }

        var status = "OPEN";
        var rendered = captures.Count == Scenes.Count(spec => spec.CaptureCandidate);
        var renderReason = contactPass
            ? "Candidate renders are evidence only; roughness acceptance remains OPEN."
            : "No candidate PNGs rendered because the puddle/relief contact gate is OPEN.";
        await ReleaseScenesAsync(sceneRuns);

        WriteReceipt(
            outputDirectory,
            status,
            contactPass,
            sourceRuntimeStatus,
            observedClusterCount,
            observedPatchCount,
            patches.Count,
            candidateCloneCount,
            candidateIsolationPass,
            rendered,
            renderReason,
            contactReasons,
            sceneRuns,
            captures);

        GD.Print(
            $"wetness-candidate: status={status} contact={(contactPass ? "PASS" : "OPEN")} " +
            $"clusters={observedClusterCount}/{ExpectedClusterCount} patches={observedPatchCount}/{ExpectedPatchCount} " +
            $"clones={candidateCloneCount} rendered={(rendered ? "yes" : "no")} output={outputDirectory}");
        GetTree().Quit(0);
    }

    private async Task<CaptureReceipt> CaptureAsync(SceneRun sceneRun, string outputDirectory)
    {
        var viewport = new SubViewport
        {
            Name = $"WetnessCapture_{sceneRun.Spec.Name}",
            Size = new Vector2I(CaptureWidth, CaptureHeight),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);

        var instance = sceneRun.Instance;
        if (instance.GetParent() is Node parent)
        {
            parent.RemoveChild(instance);
        }

        viewport.AddChild(instance);
        var camera = new Camera3D
        {
            Name = "WetnessCandidateCamera",
            Position = sceneRun.Spec.CameraPosition,
            Fov = 75f,
            Current = true
        };
        viewport.AddChild(camera);
        camera.LookAt(sceneRun.Spec.CameraTarget, Vector3.Up);

        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var image = viewport.GetTexture().GetImage();
        var imageWidth = image?.GetWidth() ?? 0;
        var imageHeight = image?.GetHeight() ?? 0;
        if (image is null || image.IsEmpty() || imageWidth != CaptureWidth || imageHeight != CaptureHeight)
        {
            viewport.Free();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            throw new InvalidOperationException(
                $"Wetness candidate capture requires a real {CaptureWidth}x{CaptureHeight} frame for {sceneRun.Spec.Name}.");
        }

        var outputPath = System.IO.Path.Combine(
            outputDirectory,
            $"godot_{sceneRun.Spec.Name}_wetness_candidate_1080p.png");
        var saveError = image.SavePng(outputPath);
        viewport.Free();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (saveError != Error.Ok)
        {
            throw new InvalidOperationException($"Could not save {outputPath}: {saveError}.");
        }

        var repoRoot = ProjectSettings.GlobalizePath("res://..");
        return new CaptureReceipt(
            sceneRun.Spec.Name,
            System.IO.Path.GetRelativePath(repoRoot, outputPath).Replace('\\', '/'),
            imageWidth,
            imageHeight);
    }

    private ContactSample SampleReliefSurface(Node3D scene, StaticBody3D road, PatchRun patch)
    {
        var center = patch.Patch.GlobalPosition;
        var query = PhysicsRayQueryParameters3D.Create(
            new Vector3(center.X, 3f, center.Z),
            new Vector3(center.X, -1f, center.Z));
        query.CollisionMask = 1;
        query.CollideWithAreas = false;

        var result = scene.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0 || !result.ContainsKey("position"))
        {
            return ContactSample.Open(
                patch,
                WorldBottom(patch.Patch),
                null,
                "No layer-1 relief surface was sampled beneath the puddle patch.");
        }

        var hitPosition = result["position"].AsVector3();
        var collider = result.ContainsKey("collider")
            ? result["collider"].AsGodotObject() as CollisionObject3D
            : null;
        var colliderMatchesRelief = collider is not null
            && collider.GetInstanceId() == road.GetInstanceId()
            && collider.GetMeta("reliefGrid").AsString() == "9x28"
            && collider.GetMeta("reliefCollisionCells").AsInt32() == 216;
        var colliderDescription = collider is null
            ? "none"
            : $"{collider.Name}<{collider.GetType().Name}>";
        var puddleBottom = WorldBottom(patch.Patch);
        var gap = puddleBottom - hitPosition.Y;
        var withinTolerance = colliderMatchesRelief
            && Mathf.Abs(gap) <= ContactToleranceMeters;
        var note = withinTolerance
            ? "Contact within ±0.010 m."
            : $"{patch.Scene}:{patch.Cluster}:{patch.PatchIndex} contact gap={gap.ToString("0.000", CultureInfo.InvariantCulture)} m " +
              $"(bottom={puddleBottom.ToString("0.000", CultureInfo.InvariantCulture)}, relief={hitPosition.Y.ToString("0.000", CultureInfo.InvariantCulture)}); " +
              $"collider={(colliderMatchesRelief ? "expected-relief" : "wrong-relief")}:{colliderDescription}, tolerance=±{Format(ContactToleranceMeters)} m.";

        return new ContactSample(
            patch.Scene,
            patch.Cluster,
            patch.PatchIndex,
            puddleBottom,
            hitPosition.Y,
            gap,
            withinTolerance,
            colliderMatchesRelief,
            note);
    }

    private static float WorldBottom(MeshInstance3D mesh)
    {
        var aabb = mesh.GetAabb();
        var max = aabb.Position + aabb.Size;
        var minimum = float.MaxValue;
        for (var x = 0; x < 2; x++)
        {
            for (var y = 0; y < 2; y++)
            {
                for (var z = 0; z < 2; z++)
                {
                    var local = new Vector3(
                        x == 0 ? aabb.Position.X : max.X,
                        y == 0 ? aabb.Position.Y : max.Y,
                        z == 0 ? aabb.Position.Z : max.Z);
                    minimum = Mathf.Min(minimum, (mesh.GlobalTransform * local).Y);
                }
            }
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
            if (node is Node3D node3D && node.HasMeta("puddleGeometry"))
            {
                destination.Add(node3D);
            }

            foreach (var child in node.GetChildren())
            {
                Walk(child, destination);
            }
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
        {
            if (GodotObject.IsInstanceValid(sceneRun.Instance))
            {
                sceneRun.Instance.QueueFree();
            }
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        PainterlyMaterialLibrary.ClearCacheForHeadlessTests();
    }

    private static float ReadRoughness(ShaderMaterial material) =>
        material.GetShaderParameter("roughness_value").AsSingle();

    private static bool Approximately(float value, float expected) =>
        Mathf.Abs(value - expected) <= 0.0005f;

    private static string Format(float value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);

    private static void EnsureOutputDirectory(string outputDirectory)
    {
        if (DirAccess.MakeDirRecursiveAbsolute(outputDirectory) != Error.Ok)
        {
            throw new InvalidOperationException($"Could not create wetness-candidate directory {outputDirectory}.");
        }
    }

    private static void RemoveCandidateFrames(string outputDirectory)
    {
        foreach (var spec in Scenes.Where(spec => spec.CaptureCandidate))
        {
            var path = System.IO.Path.Combine(
                outputDirectory,
                $"godot_{spec.Name}_wetness_candidate_1080p.png");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    private void WriteReceipt(
        string outputDirectory,
        string status,
        bool contactPass,
        string sourceRuntimeStatus,
        int observedClusterCount,
        int observedPatchCount,
        int materialPatchCount,
        int candidateCloneCount,
        bool candidateIsolationPass,
        bool rendered,
        string renderReason,
        IReadOnlyList<string> contactReasons,
        IReadOnlyList<SceneRun> sceneRuns,
        IReadOnlyList<CaptureReceipt> captures)
    {
        var manifest = new
        {
            schema_version = 1,
            kind = "urman.godot_wetness_candidate_capture",
            captured_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            status,
            roughness_acceptance = "OPEN",
            candidate_roughness = CandidateRoughness,
            expected_source_roughness = ExpectedSourceRoughness,
            contact_tolerance_m = ContactToleranceMeters,
            contact_gate = new
            {
                status = contactPass ? "PASS" : "OPEN",
                reason = contactPass
                    ? "All puddle bottoms contact the sampled authored relief within tolerance."
                    : "Contact gate did not pass; candidate rendering was suppressed.",
                details = contactReasons
            },
            source_runtime_material = new
            {
                status = sourceRuntimeStatus,
                expected_roughness = ExpectedSourceRoughness,
                candidate_clones_are_in_memory_only = true
            },
            candidate_material_isolation = new
            {
                status = candidateIsolationPass ? "PASS" : "OPEN",
                candidate_roughness = CandidateRoughness,
                exact_patch_clone_count = candidateCloneCount == ExpectedPatchCount
            },
            totals = new
            {
                clusters = observedClusterCount,
                expected_clusters = ExpectedClusterCount,
                patches = observedPatchCount,
                expected_patches = ExpectedPatchCount,
                material_backed_patches = materialPatchCount,
                candidate_shader_material_clones = candidateCloneCount
            },
            rendered,
            render_reason = renderReason,
            scenes = sceneRuns.Select(sceneRun => new
            {
                name = sceneRun.Spec.Name,
                scene = sceneRun.Spec.ScenePath,
                expected_puddle_clusters = sceneRun.Spec.ClusterNames,
                relief = sceneRun.Spec.ReliefName,
                contact = new
                {
                    status = sceneRun.Contacts.Count == 0 || sceneRun.Contacts.All(sample => sample.WithinTolerance)
                        ? "PASS"
                        : "OPEN",
                    samples = sceneRun.Contacts
                },
                patch_count = sceneRun.Patches.Count,
                capture_candidate = sceneRun.Spec.CaptureCandidate,
                capture = captures.FirstOrDefault(capture => capture.Scene == sceneRun.Spec.Name)
            }),
            captures
        };

        var manifestPath = System.IO.Path.Combine(outputDirectory, "wetness_candidate_manifest.json");
        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(manifestPath, manifestJson + System.Environment.NewLine);

        var readmePath = System.IO.Path.Combine(outputDirectory, "README.md");
        System.IO.File.WriteAllText(readmePath, BuildReadme(manifest, contactReasons));
    }

    private void WriteErrorReceipt(Exception exception)
    {
        var outputDirectory = ProjectSettings.GlobalizePath(
            "res://../docs/urman_knowledge_base/art/wetness_candidate");
        try
        {
            EnsureOutputDirectory(outputDirectory);
            RemoveCandidateFrames(outputDirectory);
            var manifestPath = System.IO.Path.Combine(outputDirectory, "wetness_candidate_manifest.json");
            var manifest = new
            {
                schema_version = 1,
                kind = "urman.godot_wetness_candidate_capture",
                captured_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                status = "ERROR",
                roughness_acceptance = "OPEN",
                rendered = false,
                render_reason = "Harness error; no candidate PNGs are accepted.",
                error = exception.ToString()
            };
            System.IO.File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + System.Environment.NewLine);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(outputDirectory, "README.md"),
                $"# Wetness candidate capture\n\nStatus: **ERROR**. No candidate PNGs were rendered.\n\n```text\n{exception}\n```\n");
        }
        catch
        {
            // Preserve the original Godot error as the actionable failure.
        }
    }

    private static string BuildReadme(object manifest, IReadOnlyList<string> contactReasons)
    {
        // The JSON manifest is the machine-readable receipt. Keep this README
        // short and explicit so an OPEN no-render result cannot be mistaken for
        // material or art acceptance.
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var contactGate = root.GetProperty("contact_gate").GetProperty("status").GetString();
        var rendered = root.GetProperty("rendered").GetBoolean();
        var totals = root.GetProperty("totals");
        var builder = new StringBuilder();
        builder.AppendLine("# Wetness candidate capture");
        builder.AppendLine();
        builder.AppendLine("Status: **OPEN — candidate evidence only; roughness acceptance is not claimed.**");
        builder.AppendLine();
        builder.AppendLine("This harness instantiates the three mandatory style benchmarks plus `chapter1_zirat_road.tscn` one at a time, so relief raycasts cannot hit a collider from another benchmark. It duplicates only the fifteen existing puddle `ShaderMaterial` overrides in memory and sets the duplicate `roughness_value` to `0.50`; source/runtime material roughness remains `0.90`.");
        builder.AppendLine();
        builder.AppendLine($"- Contact gate: **{contactGate}** (tolerance ±{Format(ContactToleranceMeters)} m)");
        builder.AppendLine($"- Puddle clusters: `{totals.GetProperty("clusters").GetInt32()}/{ExpectedClusterCount}`");
        builder.AppendLine($"- Puddle patches: `{totals.GetProperty("patches").GetInt32()}/{ExpectedPatchCount}`");
        builder.AppendLine($"- Candidate ShaderMaterial clones: `{totals.GetProperty("candidate_shader_material_clones").GetInt32()}`");
        builder.AppendLine($"- Candidate material isolation: **{root.GetProperty("candidate_material_isolation").GetProperty("status").GetString()}** (in-memory only)");
        builder.AppendLine($"- Candidate renders: **{(rendered ? "written after contact PASS" : "suppressed; no-render receipt")}**");
        builder.AppendLine();

        if (!rendered)
        {
            builder.AppendLine("No 1080p candidate PNGs were rendered because the puddle-bottom/relief contact gate is OPEN. This is an intentional fail-closed result for the known floating-puddle candidate; fix alignment before judging wet roughness.");
            builder.AppendLine();
        }
        else
        {
            builder.AppendLine("The day-street, Kara-Urman-edge and Zirat-road 1080p images are test-only roughness candidates. They do not activate runtime materials or close the wetness/art-lock gate.");
            builder.AppendLine();
        }

        builder.AppendLine("## Contact receipt");
        builder.AppendLine();
        builder.AppendLine("See `wetness_candidate_manifest.json` for every patch's sampled relief height, puddle bottom, gap and collider check.");
        if (contactReasons.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Open reasons:");
            foreach (var reason in contactReasons.Distinct(StringComparer.Ordinal))
            {
                builder.AppendLine($"- {reason}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Verification command: `./eng/capture-wetness-candidate.sh`");
        return builder.ToString();
    }

    private sealed record SceneSpec(
        string Name,
        string ScenePath,
        string[] ClusterNames,
        string? ReliefName,
        bool CaptureCandidate,
        Vector3 CameraPosition,
        Vector3 CameraTarget);

    private sealed class SceneRun
    {
        public SceneRun(SceneSpec spec, Node3D instance)
        {
            Spec = spec;
            Instance = instance;
        }

        public SceneSpec Spec { get; }
        public Node3D Instance { get; }
        public List<PatchRun> Patches { get; } = [];
        public List<ContactSample> Contacts { get; } = [];
    }

    private sealed record PatchRun(
        string Scene,
        string Cluster,
        int PatchIndex,
        MeshInstance3D Patch,
        ShaderMaterial SourceMaterial);

    private sealed record ContactSample(
        string Scene,
        string Cluster,
        int PatchIndex,
        float PuddleBottomY,
        float? ReliefSurfaceY,
        float? GapMeters,
        bool WithinTolerance,
        bool ColliderMatchesRelief,
        string Note)
    {
        public static ContactSample Open(
            PatchRun patch,
            float puddleBottomY,
            float? reliefSurfaceY,
            string note) =>
            new(
                patch.Scene,
                patch.Cluster,
                patch.PatchIndex,
                puddleBottomY,
                reliefSurfaceY,
                reliefSurfaceY.HasValue ? puddleBottomY - reliefSurfaceY.Value : null,
                false,
                false,
                note);
    }

    private sealed record CaptureReceipt(string Scene, string Output, int Width, int Height);
}
