using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Test-only A/B diagnostic for a focused earth/wood candidate pair.
/// Every cell owns its scene and physics world. Candidate textures and puddle
/// roughness are applied only to duplicated presentation materials in memory.
/// Acceptance deliberately remains OPEN even when the technical receipt passes.
/// </summary>
public partial class TextureCandidateAbDiagnostic : Node
{
    private const int TileWidth = 640;
    private const int TileHeight = 360;
    private const float SourcePuddleRoughness = 0.90f;
    private const float CandidatePuddleRoughness = 0.50f;
    private const float ContactToleranceMeters = 0.010f;

    private static readonly EarthScene[] EarthScenes =
    [
        new("day_street", "res://scenes/zones/style_benchmark_day_street.tscn", "Road",
            ["PuddleNear", "PuddleMiddle", "PuddleFar"],
            new(0f, 1.7f, 12.5f), new(0f, 1.45f, -7.5f),
            new(3.8f, 1.7f, 0f), new(-3.8f, 0.45f, 0f)),
        new("kara_urman_edge", "res://scenes/zones/style_benchmark_kara_urman_night.tscn", "PathNear",
            ["BoundaryWetPatch"],
            new(0f, 1.7f, 12.5f), new(0.55f, 1.35f, -7.2f),
            new(3.5f, 1.7f, 9f), new(-3.5f, 0.45f, 9f)),
        new("zirat_road", "res://scenes/zones/chapter1_zirat_road.tscn", "Road",
            ["ZiratWetPatch"],
            new(0f, 1.7f, 12.5f), new(0f, 1.45f, -7.5f),
            new(3.5f, 1.7f, 0f), new(-3.5f, 0.45f, 0f))
    ];

    private static readonly WoodScene[] WoodScenes =
    [
        new("day_street", "res://scenes/zones/style_benchmark_day_street.tscn",
            new(0f, 1.7f, 12.5f), new(0f, 1.45f, -7.5f)),
        new("house_old_pc", "res://scenes/zones/style_benchmark_house_pc.tscn",
            new(-1.45f, 1.68f, 1.75f), new(0f, 1.38f, -3.45f)),
        new("kara_urman_edge", "res://scenes/zones/style_benchmark_kara_urman_night.tscn",
            new(0f, 1.7f, 12.5f), new(0.55f, 1.35f, -7.2f))
    ];

    private static string[] Versions => ResolveVersions();

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception exception)
        {
            WriteErrorReceipt(exception);
            GD.PushError($"texture-candidate-ab-diagnostic: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        var outputDirectory = OutputDirectory();
        EnsureFreshOutputDirectory(outputDirectory);
        var candidateHashes = VerifyCandidateInputs();
        var captures = new List<CaptureReceipt>();
        var contactSamples = new List<ContactSample>();

        foreach (var scene in EarthScenes)
        {
            foreach (var view in new[] { "along", "across" })
            {
                var sheet = Image.CreateEmpty(TileWidth * Versions.Length * 2, TileHeight, false, Image.Format.Rgba8);
                sheet.Fill(Color.FromHtml("171b1a"));
                var column = 0;
                var sheetCells = new List<CellReceipt>();
                foreach (var version in Versions)
                {
                    foreach (var wetness in new[] { false, true })
                    {
                        var camera = view == "along" ? scene.AlongCamera : scene.AcrossCamera;
                        var target = view == "along" ? scene.AlongTarget : scene.AcrossTarget;
                        var cell = await CaptureEarthCellAsync(scene, version, wetness, camera, target, contactSamples);
                        sheet.BlitRect(cell.Image, new Rect2I(0, 0, TileWidth, TileHeight), new Vector2I(column * TileWidth, 0));
                        sheetCells.Add(cell.Receipt with { Column = column });
                        column++;
                    }
                }

                var fileName = $"godot_{scene.Name}_earth_{string.Join("_", Versions)}_{view}_ab.png";
                captures.Add(SaveCapture(outputDirectory, fileName, "earth", scene.Name, view, sheet, sheetCells));
            }
        }

        foreach (var scene in WoodScenes)
        {
            var sheet = Image.CreateEmpty(TileWidth * Versions.Length, TileHeight, false, Image.Format.Rgba8);
            sheet.Fill(Color.FromHtml("171b1a"));
            var sheetCells = new List<CellReceipt>();
            for (var column = 0; column < Versions.Length; column++)
            {
                var cell = await CaptureWoodCellAsync(scene, Versions[column]);
                sheet.BlitRect(cell.Image, new Rect2I(0, 0, TileWidth, TileHeight), new Vector2I(column * TileWidth, 0));
                sheetCells.Add(cell.Receipt with { Column = column });
            }

            var fileName = $"godot_{scene.Name}_wood_{string.Join("_", Versions)}_ab.png";
            captures.Add(SaveCapture(outputDirectory, fileName, "wood", scene.Name, "benchmark", sheet, sheetCells));
        }

        var expectedContactSamples = EarthScenes.Sum(scene => scene.ClusterNames.Length * 3) * 2 * 4;
        if (contactSamples.Count != expectedContactSamples || contactSamples.Any(sample => !sample.Pass))
        {
            throw new InvalidOperationException(
                $"Fail-closed contact receipt: {contactSamples.Count}/{expectedContactSamples} samples, " +
                $"failures={contactSamples.Count(sample => !sample.Pass)}.");
        }

        var receipt = new
        {
            schema_version = 1,
            kind = "urman.texture_candidate_ab_diagnostic",
            captured_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            status = "OPEN",
            technical_status = "PASS",
            acceptance = new Dictionary<string, string>
            {
                [$"earth_{Versions[0]}_vs_{Versions[1]}"] = "OPEN",
                ["earth_relief_only_vs_relief_plus_wetness"] = "OPEN",
                [$"wood_{Versions[0]}_vs_{Versions[1]}"] = "OPEN",
                ["reason"] = "Deterministic capture and isolation evidence cannot decide painterly double-relief, striping, wetness response or final art acceptance."
            },
            non_mutation = new
            {
                test_only = true,
                candidate_materials_are_in_memory_clones = true,
                runtime_material_owner_changed = false,
                shader_changed = false,
                collider_changed = false,
                save_or_narrative_changed = false
            },
            renderer = RenderingServer.GetRenderingDevice() is null ? "real-driver-required" : "Godot RenderingDevice",
            tile = new { width = TileWidth, height = TileHeight },
            earth_columns = Versions.SelectMany(version => new[] { $"{version} relief-only", $"{version} relief+wetness" }).ToArray(),
            wood_columns = Versions,
            source_puddle_roughness = SourcePuddleRoughness,
            candidate_puddle_roughness = CandidatePuddleRoughness,
            contact_tolerance_m = ContactToleranceMeters,
            expected_contact_samples = expectedContactSamples,
            passing_contact_samples = contactSamples.Count(sample => sample.Pass),
            candidate_inputs = candidateHashes,
            contact_samples = contactSamples,
            captures
        };

        WriteNewJson(System.IO.Path.Combine(outputDirectory, "texture_candidate_ab_diagnostic_manifest.json"), receipt);
        WriteNewText(System.IO.Path.Combine(outputDirectory, "README.md"), BuildReadme(captures, candidateHashes));
        await CleanupAsync();
        GD.Print($"texture-candidate-ab-diagnostic: status=OPEN technical=PASS captures={captures.Count} contacts={contactSamples.Count}/{expectedContactSamples} output={outputDirectory}");
        GetTree().Quit(0);
    }

    private async Task<CellResult> CaptureEarthCellAsync(
        EarthScene spec,
        string version,
        bool wetness,
        Vector3 cameraPosition,
        Vector3 cameraTarget,
        ICollection<ContactSample> contactSamples)
    {
        var setup = await InstantiateIsolatedSceneAsync(spec.ScenePath, $"Earth_{spec.Name}_{version}_{wetness}");
        var relief = setup.Instance.GetNodeOrNull<StaticBody3D>(spec.ReliefName)
            ?? throw new InvalidOperationException($"{spec.Name}: expected relief body {spec.ReliefName} is missing.");
        if (relief.GetMeta("reliefGrid").AsString() != "9x28" || relief.GetMeta("reliefCollisionCells").AsInt32() != 216)
        {
            throw new InvalidOperationException($"{spec.Name}:{spec.ReliefName}: authored relief metadata mismatch.");
        }

        var earthReplacements = ReplaceSurfaceMaterials(setup.Instance, "earth", version);
        if (earthReplacements <= 0)
        {
            throw new InvalidOperationException($"{spec.Name}:{version}: no earth presentation owner was replaced.");
        }

        var clusters = FindPuddleClusters(setup.Instance);
        if (clusters.Count != spec.ClusterNames.Length ||
            !clusters.Select(cluster => cluster.Name.ToString()).Order().SequenceEqual(spec.ClusterNames.Order()))
        {
            throw new InvalidOperationException($"{spec.Name}: puddle-cluster isolation mismatch.");
        }

        var puddleClones = 0;
        foreach (var clusterName in spec.ClusterNames)
        {
            var cluster = setup.Instance.GetNode<Node3D>(clusterName);
            var patches = cluster.GetChildren().OfType<MeshInstance3D>().ToArray();
            if (patches.Length != 3)
            {
                throw new InvalidOperationException($"{spec.Name}:{clusterName}: expected exactly three puddle patches.");
            }

            foreach (var (patch, patchIndex) in patches.Select((patch, index) => (patch, index)))
            {
                if (patch.MaterialOverride is not ShaderMaterial source || !Approximately(ReadRoughness(source), SourcePuddleRoughness))
                {
                    throw new InvalidOperationException($"{spec.Name}:{clusterName}:{patchIndex}: source puddle roughness is not 0.90.");
                }

                contactSamples.Add(SampleContact(setup.Instance, relief, spec, version, wetness, patch, clusterName, patchIndex));
                if (wetness)
                {
                    var duplicate = source.Duplicate() as ShaderMaterial
                        ?? throw new InvalidOperationException($"{spec.Name}:{clusterName}:{patchIndex}: material clone failed.");
                    duplicate.SetShaderParameter("roughness_value", CandidatePuddleRoughness);
                    if (!Approximately(ReadRoughness(duplicate), CandidatePuddleRoughness) ||
                        !Approximately(ReadRoughness(source), SourcePuddleRoughness))
                    {
                        throw new InvalidOperationException($"{spec.Name}:{clusterName}:{patchIndex}: material isolation failed.");
                    }
                    patch.MaterialOverride = duplicate;
                    puddleClones++;
                }
                else
                {
                    patch.Visible = false;
                }
            }
        }

        var expectedClones = wetness ? spec.ClusterNames.Length * 3 : 0;
        if (puddleClones != expectedClones)
        {
            throw new InvalidOperationException($"{spec.Name}:{version}: puddle clone count {puddleClones}/{expectedClones}.");
        }

        return await FinishCellAsync(setup, cameraPosition, cameraTarget,
            new CellReceipt(0, version, wetness ? "relief+wetness" : "relief-only", earthReplacements, puddleClones));
    }

    private async Task<CellResult> CaptureWoodCellAsync(WoodScene spec, string version)
    {
        var setup = await InstantiateIsolatedSceneAsync(spec.ScenePath, $"Wood_{spec.Name}_{version}");
        var replacements = ReplaceSurfaceMaterials(setup.Instance, "wood", version);
        if (replacements <= 0)
        {
            throw new InvalidOperationException($"{spec.Name}:{version}: no wood presentation owner was replaced.");
        }
        return await FinishCellAsync(setup, spec.Camera, spec.Target,
            new CellReceipt(0, version, "wood", replacements, 0));
    }

    private async Task<SceneSetup> InstantiateIsolatedSceneAsync(string scenePath, string name)
    {
        var packed = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException($"Could not load {scenePath}.");
        var viewport = new SubViewport
        {
            Name = name,
            Size = new Vector2I(TileWidth, TileHeight),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);
        var instance = packed.Instantiate<Node3D>();
        viewport.AddChild(instance);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        return new SceneSetup(viewport, instance);
    }

    private async Task<CellResult> FinishCellAsync(SceneSetup setup, Vector3 position, Vector3 target, CellReceipt receipt)
    {
        var camera = new Camera3D { Name = "DiagnosticCamera", Position = position, Fov = 75f, Current = true };
        setup.Viewport.AddChild(camera);
        camera.LookAt(target, Vector3.Up);
        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var source = setup.Viewport.GetTexture().GetImage();
        if (source is null || source.IsEmpty())
        {
            throw new InvalidOperationException("A/B diagnostic requires a real rendering driver.");
        }
        // Viewport readback may be RGB8 on Metal while the contact sheet is
        // RGBA8. Convert before blitting; Image.BlitRect rejects mixed formats
        // and would otherwise emit an engine ERROR into an otherwise valid run.
        if (source.GetFormat() != Image.Format.Rgba8)
        {
            source.Convert(Image.Format.Rgba8);
        }
        var image = source;
        if (image.GetWidth() != TileWidth || image.GetHeight() != TileHeight)
        {
            throw new InvalidOperationException($"A/B readback size is {image.GetWidth()}x{image.GetHeight()}, expected {TileWidth}x{TileHeight}.");
        }
        setup.Viewport.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return new CellResult(image, receipt);
    }

    private ContactSample SampleContact(Node3D scene, StaticBody3D relief, EarthScene spec, string version, bool wetness,
        MeshInstance3D patch, string cluster, int patchIndex)
    {
        var center = patch.GlobalPosition;
        var query = PhysicsRayQueryParameters3D.Create(new Vector3(center.X, 3f, center.Z), new Vector3(center.X, -1f, center.Z));
        query.CollisionMask = 1;
        query.CollideWithAreas = false;
        var hit = scene.GetWorld3D().DirectSpaceState.IntersectRay(query);
        var collider = hit.ContainsKey("collider") ? hit["collider"].AsGodotObject() as CollisionObject3D : null;
        var expectedCollider = collider is not null && collider.GetInstanceId() == relief.GetInstanceId();
        var reliefY = hit.ContainsKey("position") ? hit["position"].AsVector3().Y : float.NaN;
        var bottom = WorldBottom(patch);
        var gap = bottom - reliefY;
        var pass = expectedCollider && !float.IsNaN(reliefY) && Mathf.Abs(gap) <= ContactToleranceMeters;
        return new ContactSample(spec.Name, version, wetness ? "relief+wetness" : "relief-only", cluster, patchIndex,
            bottom, reliefY, gap, expectedCollider, pass);
    }

    private static int ReplaceSurfaceMaterials(Node root, string surface, string version)
    {
        var replacements = 0;
        Walk(root);
        return replacements;

        void Walk(Node node)
        {
            if (node is MeshInstance3D mesh && mesh.MaterialOverride is ShaderMaterial source)
            {
                var texture = source.GetShaderParameter("albedo_texture").AsGodotObject() as Texture2D;
                var basename = texture is null ? string.Empty : System.IO.Path.GetFileName(texture.ResourcePath);
                var stem = surface == "earth" ? "damp_earth" : "weathered_wood_boards";
                if (basename.StartsWith(stem, StringComparison.Ordinal) && basename.EndsWith("_albedo.png", StringComparison.Ordinal))
                {
                    var candidatePath = $"res://assets/textures/painterly/{stem}_{version}_albedo.png";
                    var candidate = ResourceLoader.Load<Texture2D>(candidatePath)
                        ?? throw new InvalidOperationException($"Could not load {candidatePath}.");
                    var duplicate = source.Duplicate() as ShaderMaterial
                        ?? throw new InvalidOperationException($"Could not clone {surface} material on {mesh.Name}.");
                    duplicate.SetShaderParameter("albedo_texture", candidate);
                    duplicate.SetShaderParameter("has_albedo_texture", true);
                    mesh.MaterialOverride = duplicate;
                    replacements++;
                }
            }
            foreach (var child in node.GetChildren()) Walk(child);
        }
    }

    private static List<Node3D> FindPuddleClusters(Node root)
    {
        var result = new List<Node3D>();
        Walk(root);
        return result;
        void Walk(Node node)
        {
            if (node is Node3D node3D && node3D.HasMeta("puddleGeometry")) result.Add(node3D);
            foreach (var child in node.GetChildren()) Walk(child);
        }
    }

    private static float WorldBottom(MeshInstance3D mesh)
    {
        var aabb = mesh.GetAabb();
        var max = aabb.Position + aabb.Size;
        var minimum = float.MaxValue;
        for (var x = 0; x < 2; x++) for (var y = 0; y < 2; y++) for (var z = 0; z < 2; z++)
        {
            var local = new Vector3(x == 0 ? aabb.Position.X : max.X, y == 0 ? aabb.Position.Y : max.Y, z == 0 ? aabb.Position.Z : max.Z);
            minimum = Mathf.Min(minimum, (mesh.GlobalTransform * local).Y);
        }
        return minimum;
    }

    private static IReadOnlyList<object> VerifyCandidateInputs()
    {
        var repoRoot = ProjectSettings.GlobalizePath("res://..");
        var inputs = new List<object>();
        foreach (var file in Versions.SelectMany(version => new[]
        {
            $"damp_earth_{version}_albedo.png",
            $"weathered_wood_boards_{version}_albedo.png"
        }))
        {
            var absolute = ProjectSettings.GlobalizePath($"res://assets/textures/painterly/{file}");
            if (!System.IO.File.Exists(absolute)) throw new InvalidOperationException($"Missing candidate {absolute}.");
            inputs.Add(new { file = System.IO.Path.GetRelativePath(repoRoot, absolute).Replace('\\', '/'), sha256 = HashFile(absolute) });
        }
        return inputs;
    }

    private static CaptureReceipt SaveCapture(string outputDirectory, string fileName, string family, string scene,
        string view, Image image, IReadOnlyList<CellReceipt> cells)
    {
        var path = System.IO.Path.Combine(outputDirectory, fileName);
        if (System.IO.File.Exists(path)) throw new IOException($"Refusing to overwrite {path}.");
        var error = image.SavePng(path);
        if (error != Error.Ok) throw new IOException($"Could not save {path}: {error}.");
        return new CaptureReceipt(family, scene, view, fileName, image.GetWidth(), image.GetHeight(), HashFile(path), cells);
    }

    private static string BuildReadme(IReadOnlyList<CaptureReceipt> captures, IReadOnlyList<object> candidateHashes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Texture candidate A/B diagnostic");
        builder.AppendLine();
        builder.AppendLine("Status: **OPEN — technical capture PASS, production/material acceptance not decided.**");
        builder.AppendLine();
        builder.AppendLine("This is a test-only diagnostic. Each cell uses a fresh benchmark instance and its own physics world. It clones presentation materials in memory, verifies the expected 9×28/216-cell relief collider by instance identity, checks every puddle patch within ±0.010 m, and never changes runtime material owners, shaders, collisions, saves or narrative.");
        builder.AppendLine();
        builder.AppendLine($"Earth sheets use columns: {string.Join(", ", Versions.SelectMany(version => new[] { $"`{version} relief-only`", $"`{version} relief+wetness`" }))}. Day, Kara-Urman and Zirat are captured both along and across the road. Wood sheets use columns {string.Join(", ", Versions.Select(version => $"`{version}`"))} in the day, house/old-PC and Kara-Urman benchmarks.");
        builder.AppendLine();
        builder.AppendLine("The result stays OPEN because images require human review for double relief, 20–30 m repetition, triplanar striping, warm/neutral lighting response and wetness acceptance.");
        builder.AppendLine();
        builder.AppendLine("## Exact capture hashes");
        builder.AppendLine();
        foreach (var capture in captures) builder.AppendLine($"- `{capture.Output}` — `{capture.Sha256}`");
        builder.AppendLine();
        builder.AppendLine("Candidate input hashes and all collider/contact samples are recorded in `texture_candidate_ab_diagnostic_manifest.json`.");
        builder.AppendLine();
        builder.AppendLine("Verification: `./eng/capture-texture-ab-diagnostic.sh`");
        return builder.ToString();
    }

    private static async Task CleanupAsync()
    {
        await Task.Yield();
        PainterlyMaterialLibrary.ClearCacheForHeadlessTests();
    }

    private void WriteErrorReceipt(Exception exception)
    {
        try
        {
            var outputDirectory = OutputDirectory();
            DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
            var manifestPath = System.IO.Path.Combine(outputDirectory, "texture_candidate_ab_diagnostic_manifest.json");
            if (!System.IO.File.Exists(manifestPath))
            {
                WriteNewJson(manifestPath, new
                {
                    schema_version = 1,
                    kind = "urman.texture_candidate_ab_diagnostic",
                    captured_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    status = "OPEN",
                    technical_status = "ERROR",
                    rendered = false,
                    reason = exception.Message,
                    acceptance = "OPEN",
                    non_mutation = new { test_only = true, runtime_owners_changed = false }
                });
            }
            var readmePath = System.IO.Path.Combine(outputDirectory, "README.md");
            if (!System.IO.File.Exists(readmePath))
            {
                WriteNewText(readmePath, $"# Texture candidate A/B diagnostic\n\nStatus: **OPEN — fail-closed, no accepted capture.**\n\nExact reason: `{exception.Message.Replace("`", "'")}`\n\nNo runtime/shader/collider/save/narrative owner was changed.\n");
            }
        }
        catch { }
    }

    private static string OutputDirectory()
    {
        var configured = System.Environment.GetEnvironmentVariable("URMAN_TEXTURE_AB_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(configured) && System.IO.Path.IsPathRooted(configured))
            return configured;
        return ProjectSettings.GlobalizePath(string.IsNullOrWhiteSpace(configured)
            ? "res://../docs/urman_knowledge_base/art/texture_candidate_ab_diagnostic_v3"
            : configured);
    }

    private static string[] ResolveVersions()
    {
        var configured = System.Environment.GetEnvironmentVariable("URMAN_TEXTURE_AB_VERSIONS");
        var versions = string.IsNullOrWhiteSpace(configured)
            ? ["v3", "v4"]
            : configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (versions.Length != 2 || versions.Any(version => version is not ("v3" or "v4" or "v5" or "v6")))
            throw new InvalidOperationException("Texture A/B diagnostic requires exactly two supported versions (v3, v4, v5 or v6).");
        return versions;
    }

    private static void EnsureFreshOutputDirectory(string outputDirectory)
    {
        if (System.IO.Directory.Exists(outputDirectory) && System.IO.Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            throw new IOException($"Refusing to overwrite non-empty diagnostic directory {outputDirectory}.");
        if (DirAccess.MakeDirRecursiveAbsolute(outputDirectory) != Error.Ok)
            throw new IOException($"Could not create {outputDirectory}.");
    }

    private static void WriteNewJson(string path, object value) => WriteNewText(path,
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + System.Environment.NewLine);

    private static void WriteNewText(string path, string text)
    {
        using var stream = new System.IO.FileStream(path, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None);
        using var writer = new System.IO.StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
    }

    private static float ReadRoughness(ShaderMaterial material) => material.GetShaderParameter("roughness_value").AsSingle();
    private static bool Approximately(float value, float expected) => Mathf.Abs(value - expected) <= 0.0005f;
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path))).ToLowerInvariant();

    private sealed record EarthScene(string Name, string ScenePath, string ReliefName, string[] ClusterNames,
        Vector3 AlongCamera, Vector3 AlongTarget, Vector3 AcrossCamera, Vector3 AcrossTarget);
    private sealed record WoodScene(string Name, string ScenePath, Vector3 Camera, Vector3 Target);
    private sealed record SceneSetup(SubViewport Viewport, Node3D Instance);
    private sealed record CellResult(Image Image, CellReceipt Receipt);
    private sealed record CellReceipt(int Column, string Version, string Variant, int MaterialReplacements, int PuddleMaterialClones);
    private sealed record CaptureReceipt(string Family, string Scene, string View, string Output, int Width, int Height,
        string Sha256, IReadOnlyList<CellReceipt> Cells);
    private sealed record ContactSample(string Scene, string Version, string Variant, string Cluster, int Patch,
        float PuddleBottomY, float ReliefY, float GapMeters, bool ColliderMatchesExpectedRelief, bool Pass);
}
