using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Bounded, render-only A/B receipt for a lighting and fog calibration.
///
/// Each cell owns a disposable scene instance inside its own SubViewport. The
/// candidate duplicates only the WorldEnvironment resource and overrides only
/// WorldEnvironment, DirectionalLight3D and OmniLight3D presentation values on
/// that disposable instance. It never starts the game bootstrap, touches a
/// material, collider, save, narrative owner or the PackedScene source.
/// </summary>
public partial class StyleCalibrationCandidateCapture : Node
{
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;
    private const int WarmupFrames = 8;
    private const int SampleStride = 32;

    private static readonly SceneSpec[] Scenes =
    [
        new(
            "day_street",
            "res://scenes/zones/style_benchmark_day_street.tscn",
            new Vector3(0f, 1.7f, 12.5f),
            new Vector3(0f, 1.45f, -7.5f),
            ["MainDirectionalLight"],
            [],
            new EnvironmentCalibration("a9b2a4", 0.80f, "87918b", 0.0032f),
            new Dictionary<string, LightCalibration>
            {
                ["MainDirectionalLight"] = new("d6d0bd", 1.08f)
            }),
        new(
            "house_old_pc",
            "res://scenes/zones/style_benchmark_house_pc.tscn",
            new Vector3(-1.45f, 1.68f, 1.75f),
            new Vector3(0f, 1.38f, -3.45f),
            [],
            ["WarmTableLamp", "WindowFill", "RoomFill", "CrtScreenGlow"],
            new EnvironmentCalibration("9d9489", 0.54f, null, null),
            new Dictionary<string, LightCalibration>
            {
                ["WarmTableLamp"] = new("c5aa8d", 1.45f),
                ["WindowFill"] = new("9aaeb0", 0.70f),
                ["RoomFill"] = new("958878", 0.52f),
                ["CrtScreenGlow"] = new("78a59a", 0.62f)
            }),
        new(
            "kara_urman_edge",
            "res://scenes/zones/style_benchmark_kara_urman_night.tscn",
            new Vector3(0f, 1.7f, 12.5f),
            new Vector3(0.55f, 1.35f, -7.2f),
            ["MainDirectionalLight"],
            ["DistantWarmWindow", "MoonFill"],
            new EnvironmentCalibration("71858d", 0.55f, "485d65", 0.0042f),
            new Dictionary<string, LightCalibration>
            {
                ["MainDirectionalLight"] = new("7d929e", 0.75f),
                ["DistantWarmWindow"] = new("c79d79", 2.10f),
                ["MoonFill"] = new("718c96", 0.95f)
            })
    ];

    private string _outputDirectory = string.Empty;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception exception)
        {
            WriteFailureReceipt(exception);
            GD.PushError($"style-calibration-candidate: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        RequireRealRenderingDevice();
        _outputDirectory = ProjectSettings.GlobalizePath(
            "res://../docs/urman_knowledge_base/art/style_calibration_candidate");
        EnsureFreshOutputDirectory(_outputDirectory);

        var captures = new List<CaptureReceipt>(Scenes.Length * 2);
        foreach (var scene in Scenes)
        {
            captures.Add(await CaptureAsync(scene, "baseline"));
            captures.Add(await CaptureAsync(scene, "candidate"));
        }

        var receipt = new
        {
            schema_version = 1,
            kind = "urman.godot_style_calibration_candidate",
            captured_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            status = "OPEN",
            technical_status = "PASS",
            acceptance = "OPEN",
            rendered = true,
            renderer_requirement = "Godot 4.7.1 .NET Forward+ with a real Metal or equivalent RenderingDevice",
            resolution = new { width = CaptureWidth, height = CaptureHeight },
            calibration_scope = new
            {
                test_only = true,
                candidate_environment_resource_cloned = true,
                overridden_node_types = new[] { "WorldEnvironment", "DirectionalLight3D", "OmniLight3D" },
                runtime_scene_changed = false,
                packed_scene_changed = false,
                materials_changed = false,
                shader_changed = false,
                glb_or_registry_changed = false,
                collision_changed = false,
                save_or_narrative_changed = false
            },
            candidate_values = Scenes.Select(scene => new
            {
                scene = scene.Name,
                environment = scene.CandidateEnvironment,
                lights = scene.CandidateLights
            }),
            captures
        };

        WriteJson("style_calibration_candidate_manifest.json", receipt);
        WriteText("README.md", BuildReadme(captures));
        GD.Print($"style-calibration-candidate: OPEN technical=PASS captures={captures.Count} at {CaptureWidth}x{CaptureHeight}");
        GetTree().Quit(0);
    }

    private async Task<CaptureReceipt> CaptureAsync(SceneSpec spec, string variant)
    {
        var packed = ResourceLoader.Load<PackedScene>(spec.ScenePath)
            ?? throw new InvalidOperationException($"Could not load benchmark {spec.ScenePath}.");
        var viewport = new SubViewport
        {
            Name = $"StyleCalibration_{spec.Name}_{variant}",
            Size = new Vector2I(CaptureWidth, CaptureHeight),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);

        try
        {
            var scene = packed.Instantiate<Node3D>();
            viewport.AddChild(scene);
            await WaitForFramesAsync(2);
            var physicsNodesBefore = CountPhysicsNodes(scene);
            var isolation = ValidatePresentationSetup(scene, viewport, spec, physicsNodesBefore);
            var sanitization = SanitizePhysicsForRenderOnlyCapture(scene);
            if (sanitization.VisualMeshCount <= 0
                || sanitization.CollisionShapeCountAfter != 0
                || sanitization.ActivePhysicsQueryOwnerCount != 0)
            {
                throw new InvalidOperationException(
                    $"{spec.Name}:{variant}: render-only physics sanitization removed authored visuals or left physics queries active "
                    + $"(visual_meshes={sanitization.VisualMeshCount}, "
                    + $"collision_shapes_before={sanitization.CollisionShapeCountBefore}, "
                    + $"collision_shapes_after={sanitization.CollisionShapeCountAfter}, "
                    + $"active_physics_query_owners={sanitization.ActivePhysicsQueryOwnerCount}).");
            }
            isolation = isolation with
            {
                physics_nodes_after = CountPhysicsNodes(scene),
                collision_shape_count_before = sanitization.CollisionShapeCountBefore,
                collision_shapes_removed = sanitization.CollisionShapesRemoved,
                collision_shape_count_after = sanitization.CollisionShapeCountAfter,
                active_physics_query_owner_count = sanitization.ActivePhysicsQueryOwnerCount,
                visual_mesh_count = sanitization.VisualMeshCount
            };
            if (variant == "candidate")
            {
                isolation = ApplyCandidateCalibration(scene, spec, isolation);
            }

            var camera = new Camera3D
            {
                Name = "StyleCalibrationCamera",
                Position = spec.Camera,
                Fov = 75f,
                Current = true
            };
            viewport.AddChild(camera);
            camera.LookAt(spec.Target, Vector3.Up);
            await WaitForFramesAsync(WarmupFrames);

            var image = viewport.GetTexture().GetImage();
            ValidateImage(image, spec.Name, variant);
            var outputName = $"godot_{spec.Name}_{variant}_1080p.png";
            var outputPath = System.IO.Path.Combine(_outputDirectory, outputName);
            if (image!.SavePng(outputPath) != Error.Ok)
            {
                throw new InvalidOperationException($"Could not save {outputPath}.");
            }

            return new CaptureReceipt(
                spec.Name,
                variant,
                ToRepositoryPath(outputPath),
                CaptureWidth,
                CaptureHeight,
                HashFile(outputPath),
                isolation,
                CountPhysicsNodes(scene));
        }
        finally
        {
            viewport.Free();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static IsolationReceipt ValidatePresentationSetup(Node3D scene, SubViewport viewport, SceneSpec spec, int physicsNodesBefore)
    {
        // World3D wrappers are not guaranteed to preserve managed-reference
        // equality across the native boundary. OwnWorld3D plus viewport
        // parentage is the stable isolation contract here.
        if (!viewport.OwnWorld3D || scene.GetViewport().GetInstanceId() != viewport.GetInstanceId() || scene.GetWorld3D() is null)
        {
            throw new InvalidOperationException($"{spec.Name}: scene did not receive the isolated SubViewport World3D.");
        }

        var worlds = FindNodes<WorldEnvironment>(scene);
        if (worlds.Count != 1 || worlds[0].Environment is null)
        {
            throw new InvalidOperationException($"{spec.Name}: expected exactly one WorldEnvironment with an Environment resource.");
        }

        var directionals = FindNodes<DirectionalLight3D>(scene);
        var omnis = FindNodes<OmniLight3D>(scene);
        RequireExactNames(spec.Name, "DirectionalLight3D", directionals.Select(light => light.Name.ToString()), spec.DirectionalLights);
        RequireExactNames(spec.Name, "OmniLight3D", omnis.Select(light => light.Name.ToString()), spec.OmniLights);

        return new IsolationReceipt(
            isolated_subviewport: true,
            environment_clone_created: false,
            environment_resource_id_before: worlds[0].Environment.GetInstanceId(),
            environment_resource_id_after: worlds[0].Environment.GetInstanceId(),
            directional_lights_overridden: Array.Empty<string>(),
            omni_lights_overridden: Array.Empty<string>(),
            physics_nodes_before: physicsNodesBefore,
            physics_nodes_after: physicsNodesBefore,
            collision_shape_count_before: 0,
            collision_shapes_removed: 0,
            collision_shape_count_after: 0,
            active_physics_query_owner_count: 0,
            visual_mesh_count: 0);
    }

    private static IsolationReceipt ApplyCandidateCalibration(Node3D scene, SceneSpec spec, IsolationReceipt before)
    {
        var world = FindNodes<WorldEnvironment>(scene).Single();
        var sourceEnvironment = world.Environment
            ?? throw new InvalidOperationException($"{spec.Name}: candidate has no Environment to clone.");
        var candidateEnvironment = sourceEnvironment.Duplicate(true) as global::Godot.Environment;
        if (candidateEnvironment is null || candidateEnvironment.GetInstanceId() == sourceEnvironment.GetInstanceId())
        {
            throw new InvalidOperationException($"{spec.Name}: WorldEnvironment resource isolation failed.");
        }

        world.Environment = candidateEnvironment;
        candidateEnvironment.AmbientLightColor = Color.FromHtml(spec.CandidateEnvironment.AmbientColor);
        candidateEnvironment.AmbientLightEnergy = spec.CandidateEnvironment.AmbientEnergy;
        if (spec.CandidateEnvironment.FogLightColor is not null)
        {
            candidateEnvironment.FogLightColor = Color.FromHtml(spec.CandidateEnvironment.FogLightColor);
        }
        if (spec.CandidateEnvironment.FogDensity is not null)
        {
            candidateEnvironment.FogDensity = spec.CandidateEnvironment.FogDensity.Value;
        }

        var overriddenDirectional = OverrideLights(
            FindNodes<DirectionalLight3D>(scene).Cast<Light3D>(), spec);
        var overriddenOmni = OverrideLights(
            FindNodes<OmniLight3D>(scene).Cast<Light3D>(), spec);
        if (overriddenDirectional.Length + overriddenOmni.Length != spec.CandidateLights.Count)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: candidate light override count {overriddenDirectional.Length + overriddenOmni.Length}/{spec.CandidateLights.Count}.");
        }
        var physicsAfter = CountPhysicsNodes(scene);
        if (physicsAfter != before.physics_nodes_after)
        {
            throw new InvalidOperationException($"{spec.Name}: candidate calibration changed physics-node ownership.");
        }

        return before with
        {
            environment_clone_created = true,
            environment_resource_id_after = candidateEnvironment.GetInstanceId(),
            directional_lights_overridden = overriddenDirectional,
            omni_lights_overridden = overriddenOmni,
            physics_nodes_after = physicsAfter
        };
    }

    private static string[] OverrideLights(IEnumerable<Light3D> lights, SceneSpec spec)
    {
        var overridden = new List<string>();
        foreach (var light in lights)
        {
            if (!spec.CandidateLights.TryGetValue(light.Name.ToString(), out var calibration))
            {
                throw new InvalidOperationException($"{spec.Name}: attempted to override unexpected presentation light {light.Name}.");
            }

            light.LightColor = Color.FromHtml(calibration.Color);
            light.LightEnergy = calibration.Energy;
            if (!Approximately(light.LightEnergy, calibration.Energy))
            {
                throw new InvalidOperationException($"{spec.Name}:{light.Name}: candidate light override did not stick.");
            }
            overridden.Add(light.Name.ToString());
        }

        return overridden.Order().ToArray();
    }

    private async Task WaitForFramesAsync(int frames)
    {
        for (var index = 0; index < frames; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void ValidateImage(Image? image, string scene, string variant)
    {
        if (image is null || image.IsEmpty() || image.GetWidth() != CaptureWidth || image.GetHeight() != CaptureHeight)
        {
            throw new InvalidOperationException($"{scene}:{variant}: a real {CaptureWidth}x{CaptureHeight} rendering driver is required.");
        }

        var blackPixels = 0L;
        var samples = 0L;
        for (var y = 0; y < image.GetHeight(); y += SampleStride)
        {
            for (var x = 0; x < image.GetWidth(); x += SampleStride)
            {
                var color = image.GetPixel(x, y);
                var luminance = color.R * 0.2126f + color.G * 0.7152f + color.B * 0.0722f;
                if (luminance <= 0.01f)
                {
                    blackPixels++;
                }
                samples++;
            }
        }

        if (samples == 0 || (double)blackPixels / samples >= 0.995d)
        {
            throw new InvalidOperationException($"{scene}:{variant}: render readback is almost entirely black.");
        }
    }

    private static void RequireRealRenderingDevice()
    {
        if (RenderingServer.GetRenderingDevice() is null)
        {
            throw new InvalidOperationException("A real Forward+/Metal RenderingDevice is required; headless or dummy rendering is not evidence.");
        }
    }

    private static void RequireExactNames(string scene, string nodeType, IEnumerable<string> actual, IEnumerable<string> expected)
    {
        var actualNames = actual.Order().ToArray();
        var expectedNames = expected.Order().ToArray();
        if (!actualNames.SequenceEqual(expectedNames, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{scene}: {nodeType} isolation contract changed. actual=[{string.Join(",", actualNames)}] expected=[{string.Join(",", expectedNames)}].");
        }
    }

    private static List<T> FindNodes<T>(Node root) where T : Node
    {
        var result = new List<T>();
        Collect(root, result);
        return result;
    }

    private static void Collect<T>(Node node, ICollection<T> result) where T : Node
    {
        if (node is T typed)
        {
            result.Add(typed);
        }
        foreach (var child in node.GetChildren())
        {
            Collect(child, result);
        }
    }

    private static int CountPhysicsNodes(Node root)
    {
        var count = root is CollisionObject3D or CollisionShape3D ? 1 : 0;
        foreach (var child in root.GetChildren())
        {
            count += CountPhysicsNodes(child);
        }
        return count;
    }

    /// <summary>
    /// Keeps MeshInstance3D descendants in the disposable A/B clone while
    /// removing its physics-query and Shape3D owners. Production scenes and
    /// gameplay collision authority are never modified.
    /// </summary>
    private static RenderOnlySanitization SanitizePhysicsForRenderOnlyCapture(Node root)
    {
        var collisionShapeCountBefore = CountCollisionShapes(root);
        var collisionShapesRemoved = 0;
        DisablePhysicsQueriesAndRemoveShapes(root, ref collisionShapesRemoved);
        return new RenderOnlySanitization(
            CountVisualMeshes(root),
            collisionShapeCountBefore,
            collisionShapesRemoved,
            CountCollisionShapes(root),
            CountActivePhysicsQueryOwners(root));
    }

    private static void DisablePhysicsQueriesAndRemoveShapes(Node root, ref int collisionShapesRemoved)
    {
        if (root is CollisionObject3D collisionObject)
        {
            collisionObject.CollisionLayer = 0;
            collisionObject.CollisionMask = 0;
        }

        foreach (var child in root.GetChildren().ToArray())
        {
            if (child is CollisionShape3D collisionShape)
            {
                var importedShape = collisionShape.Shape;
                collisionShape.Shape = null;
                importedShape?.Dispose();
                collisionShape.Free();
                collisionShapesRemoved++;
                continue;
            }

            DisablePhysicsQueriesAndRemoveShapes(child, ref collisionShapesRemoved);
        }
    }

    private static int CountVisualMeshes(Node root) =>
        (root is MeshInstance3D ? 1 : 0) + root.GetChildren().Sum(CountVisualMeshes);

    private static int CountCollisionShapes(Node root) =>
        (root is CollisionShape3D ? 1 : 0) + root.GetChildren().Sum(CountCollisionShapes);

    private static int CountActivePhysicsQueryOwners(Node root)
    {
        var activeHere = root is CollisionObject3D collisionObject
            && (collisionObject.CollisionLayer != 0 || collisionObject.CollisionMask != 0)
            ? 1
            : 0;
        return activeHere + root.GetChildren().Sum(CountActivePhysicsQueryOwners);
    }

    private void EnsureFreshOutputDirectory(string outputDirectory)
    {
        var makeDirectory = DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        if (makeDirectory != Error.Ok)
        {
            throw new InvalidOperationException($"Could not create candidate output directory: {makeDirectory}.");
        }

        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "README.md",
            "style_calibration_candidate_manifest.json"
        };
        foreach (var scene in Scenes)
        {
            known.Add($"godot_{scene.Name}_baseline_1080p.png");
            known.Add($"godot_{scene.Name}_candidate_1080p.png");
        }

        var existing = Directory.GetFiles(outputDirectory).Select(System.IO.Path.GetFileName).ToArray();
        var unexpected = existing.Where(file => file is not null && !known.Contains(file)).ToArray();
        if (unexpected.Length > 0)
        {
            throw new InvalidOperationException($"Refusing to overwrite unexpected candidate artifacts: {string.Join(", ", unexpected)}.");
        }
        foreach (var file in existing)
        {
            if (file is not null)
            {
                System.IO.File.Delete(System.IO.Path.Combine(outputDirectory, file));
            }
        }
    }

    private void WriteFailureReceipt(Exception exception)
    {
        if (string.IsNullOrEmpty(_outputDirectory))
        {
            _outputDirectory = ProjectSettings.GlobalizePath(
                "res://../docs/urman_knowledge_base/art/style_calibration_candidate");
        }

        if (DirAccess.MakeDirRecursiveAbsolute(_outputDirectory) != Error.Ok)
        {
            return;
        }

        foreach (var scene in Scenes)
        {
            DeleteIfPresent($"godot_{scene.Name}_baseline_1080p.png");
            DeleteIfPresent($"godot_{scene.Name}_candidate_1080p.png");
        }
        var receipt = new
        {
            schema_version = 1,
            kind = "urman.godot_style_calibration_candidate",
            captured_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            status = "OPEN",
            technical_status = "ERROR",
            acceptance = "OPEN",
            rendered = false,
            fail_closed = true,
            reason = exception.Message,
            calibration_scope = new
            {
                test_only = true,
                allowed_node_types = new[] { "WorldEnvironment", "DirectionalLight3D", "OmniLight3D" },
                runtime_scene_changed = false,
                packed_scene_changed = false,
                materials_changed = false,
                shader_changed = false,
                glb_or_registry_changed = false,
                collision_changed = false,
                save_or_narrative_changed = false
            }
        };
        WriteJson("style_calibration_candidate_manifest.json", receipt);
        WriteText("README.md", "# Style calibration candidate\n\nStatus: **OPEN — fail-closed; no accepted capture was produced.**\n\n" +
            $"Reason: `{exception.Message.Replace('`', '\'')}`\n\n" +
            "The harness did not modify production scenes, materials, shaders, collision, GLB/registry, saves or narrative owners.\n");
    }

    private void DeleteIfPresent(string fileName)
    {
        var path = System.IO.Path.Combine(_outputDirectory, fileName);
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }
    }

    private void WriteJson(string fileName, object value) =>
        WriteText(fileName, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + System.Environment.NewLine);

    private void WriteText(string fileName, string text) =>
        System.IO.File.WriteAllText(System.IO.Path.Combine(_outputDirectory, fileName), text);

    private static string BuildReadme(IEnumerable<CaptureReceipt> captures)
    {
        var lines = new List<string>
        {
            "# Style calibration candidate",
            string.Empty,
            "Status: **OPEN — bounded technical A/B evidence only; this is not an art-lock decision.**",
            string.Empty,
            "Each 1 920 × 1 080 pair renders one isolated `SubViewport` scene. The candidate duplicates the scene's `WorldEnvironment` resource and overrides only its presentation `WorldEnvironment`, `DirectionalLight3D` and `OmniLight3D` values. The disposable clone preserves visual descendants, zeros physics layers/masks and removes only collision shapes; production collision is untouched. Production scenes, material owners, shaders, GLB/registry, saves and narrative are untouched.",
            string.Empty,
            "| Scene | Baseline SHA-256 | Candidate SHA-256 |",
            "|---|---|---|"
        };
        foreach (var scene in Scenes)
        {
            var baseline = captures.Single(capture => capture.scene == scene.Name && capture.variant == "baseline");
            var candidate = captures.Single(capture => capture.scene == scene.Name && capture.variant == "candidate");
            lines.Add($"| {scene.Name} | `{baseline.sha256}` | `{candidate.sha256}` |");
        }
        lines.Add(string.Empty);
        lines.Add("Run `./eng/capture-style-calibration-candidate.sh` on a real Metal/Forward+ host. The wrapper validates the manifest, all six 1080p PNGs and SHA-256 values. Visual, cultural, motion, release-hardware and final art-lock review remain OPEN.");
        lines.Add(string.Empty);
        return string.Join(System.Environment.NewLine, lines);
    }

    private static string ToRepositoryPath(string absolutePath) =>
        System.IO.Path.GetRelativePath(ProjectSettings.GlobalizePath("res://.."), absolutePath).Replace('\\', '/');

    private static string HashFile(string path) =>
        Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path))).ToLowerInvariant();

    private static bool Approximately(float first, float second) => Mathf.Abs(first - second) <= 0.0001f;

    private sealed record SceneSpec(
        string Name,
        string ScenePath,
        Vector3 Camera,
        Vector3 Target,
        string[] DirectionalLights,
        string[] OmniLights,
        EnvironmentCalibration CandidateEnvironment,
        Dictionary<string, LightCalibration> CandidateLights);

    private sealed record EnvironmentCalibration(string AmbientColor, float AmbientEnergy, string? FogLightColor, float? FogDensity);

    private sealed record LightCalibration(string Color, float Energy);

    private sealed record IsolationReceipt(
        bool isolated_subviewport,
        bool environment_clone_created,
        ulong environment_resource_id_before,
        ulong environment_resource_id_after,
        string[] directional_lights_overridden,
        string[] omni_lights_overridden,
        int physics_nodes_before,
        int physics_nodes_after,
        int collision_shape_count_before,
        int collision_shapes_removed,
        int collision_shape_count_after,
        int active_physics_query_owner_count,
        int visual_mesh_count);

    private sealed record RenderOnlySanitization(
        int VisualMeshCount,
        int CollisionShapeCountBefore,
        int CollisionShapesRemoved,
        int CollisionShapeCountAfter,
        int ActivePhysicsQueryOwnerCount);

    private sealed record CaptureReceipt(
        string scene,
        string variant,
        string output,
        int width,
        int height,
        string sha256,
        IsolationReceipt isolation,
        int physics_nodes_after_capture);
}
