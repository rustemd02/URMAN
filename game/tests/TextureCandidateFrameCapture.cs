using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// QA-only capture harness for versioned painterly albedo candidates.
///
/// This node never enters the game bootstrap and never dispatches interactions:
/// it instantiates the three style benchmark scenes in temporary SubViewports,
/// duplicates only presentation ShaderMaterials in memory, and swaps the
/// albedo texture on the duplicate. Scene colliders and narrative nodes remain
/// owned by the source scene and are not edited.
/// </summary>
public partial class TextureCandidateFrameCapture : Node
{
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;
    private const string DefaultCandidateVersion = "v2";

    [Export]
    public string CandidateVersion { get; set; } = DefaultCandidateVersion;

    [Export]
    public string OutputSubdirectory { get; set; } = string.Empty;

    private static readonly (string Name, string ScenePath, Vector3 Camera, Vector3 Target)[] Frames =
    [
        ("day_street", "res://scenes/zones/style_benchmark_day_street.tscn", new Vector3(0, 1.7f, 12.5f), new Vector3(0, 1.45f, -7.5f)),
        ("house_old_pc", "res://scenes/zones/style_benchmark_house_pc.tscn", new Vector3(-1.45f, 1.68f, 1.75f), new Vector3(0, 1.38f, -3.45f)),
        ("kara_urman_edge", "res://scenes/zones/style_benchmark_kara_urman_night.tscn", new Vector3(0, 1.7f, 12.5f), new Vector3(0.55f, 1.35f, -7.2f))
    ];

    private static readonly string[] V2Candidates =
    [
        "weathered_wood_boards_v2_albedo.png",
        "aged_plaster_v2_albedo.png",
        "damp_earth_v2_albedo.png",
        "pine_foliage_v2_albedo.png",
        "mossy_stone_v2_albedo.png",
        "old_fabric_v2_albedo.png"
    ];

    private static readonly string[] V3Candidates =
    [
        "weathered_wood_boards_v3_albedo.png",
        "aged_plaster_v3_albedo.png",
        "damp_earth_v3_albedo.png",
        "pine_foliage_v3_albedo.png",
        "mossy_stone_v3_albedo.png",
        "old_fabric_v3_albedo.png"
    ];

    private static readonly string[] V4Candidates =
    [
        "weathered_wood_boards_v4_albedo.png",
        "damp_earth_v4_albedo.png"
    ];

    private static readonly IReadOnlyDictionary<string, string> CandidateSurface = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["weathered_wood_boards_v2_albedo.png"] = "wood",
        ["aged_plaster_v2_albedo.png"] = "plaster",
        ["damp_earth_v2_albedo.png"] = "earth",
        ["pine_foliage_v2_albedo.png"] = "foliage",
        ["mossy_stone_v2_albedo.png"] = "stone",
        ["old_fabric_v2_albedo.png"] = "fabric",
        ["weathered_wood_boards_v3_albedo.png"] = "wood",
        ["aged_plaster_v3_albedo.png"] = "plaster",
        ["damp_earth_v3_albedo.png"] = "earth",
        ["pine_foliage_v3_albedo.png"] = "foliage",
        ["mossy_stone_v3_albedo.png"] = "stone",
        ["old_fabric_v3_albedo.png"] = "fabric",
        ["weathered_wood_boards_v4_albedo.png"] = "wood",
        ["damp_earth_v4_albedo.png"] = "earth"
    };

    private string CandidateSuffix => $"_{CandidateVersion}_albedo.png";
    private string[] ExpectedCandidates => CandidateVersion switch
    {
        "v2" => V2Candidates,
        "v3" => V3Candidates,
        "v4" => V4Candidates,
        _ => throw new InvalidOperationException($"Unsupported texture candidate version '{CandidateVersion}'.")
    };

    private readonly Dictionary<string, int> _sceneCoverage = new(StringComparer.Ordinal);

    public override async void _Ready()
    {
        // Versioned candidate scenes are intentionally test-only and use their node name as a
        // stable selector so captures cannot silently fall back to the v2 set
        // if an exported C# property is not serialized by an older editor.
        if (Name.ToString().Contains("V3", StringComparison.OrdinalIgnoreCase))
        {
            CandidateVersion = "v3";
            // Keep v3 captures in their own evidence directory even when an
            // older Godot/C# importer drops exported string properties from a
            // test-only scene resource. This prevents a candidate run from
            // overwriting the established v2 benchmark frames.
            OutputSubdirectory = "v3";
        }
        else if (Name.ToString().Contains("V4", StringComparison.OrdinalIgnoreCase))
        {
            CandidateVersion = "v4";
            OutputSubdirectory = "v4";
        }

        var outputDirectory = ProjectSettings.GlobalizePath("res://../docs/urman_knowledge_base/art/texture_candidate_frames");
        if (!string.IsNullOrWhiteSpace(OutputSubdirectory))
        {
            outputDirectory = System.IO.Path.Combine(outputDirectory, OutputSubdirectory);
        }
        var directoryError = DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        if (directoryError != Error.Ok)
        {
            Fail($"Could not create texture-candidate directory: {directoryError}.");
            return;
        }

        foreach (var candidate in ExpectedCandidates)
        {
            var candidatePath = ProjectSettings.GlobalizePath($"res://assets/textures/painterly/{candidate}");
            if (!System.IO.File.Exists(candidatePath))
            {
                Fail($"Texture candidate set is incomplete: missing {candidatePath}.");
                return;
            }

            _sceneCoverage[candidate] = 0;
        }

        foreach (var frame in Frames)
        {
            if (!await CaptureSceneAsync(frame.Name, frame.ScenePath, frame.Camera, frame.Target, outputDirectory))
            {
                return;
            }
        }

        if (!await CaptureSwatchesAsync(outputDirectory))
        {
            return;
        }

        foreach (var candidate in ExpectedCandidates)
        {
            var coverage = _sceneCoverage[candidate];
            if (coverage <= 0)
            {
                Fail($"Texture candidate {candidate} has no scene mesh coverage; owner integration is incomplete.");
                return;
            }

            GD.Print($"texture-candidate-coverage: {candidate} scene_meshes={coverage} scene_coverage=PASS");
        }

        GD.Print($"texture-candidate-frame-capture: version={CandidateVersion} {Frames.Length} scene frames + swatch at {outputDirectory}");
        GetTree().Quit(0);
    }

    private async Task<bool> CaptureSceneAsync(
        string name,
        string scenePath,
        Vector3 cameraPosition,
        Vector3 cameraTarget,
        string outputDirectory)
    {
        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed is null)
        {
            Fail($"Could not load benchmark scene {scenePath}.");
            return false;
        }

        var viewport = CreateViewport($"Capture_{name}");
        AddChild(viewport);
        var sceneInstance = packed.Instantiate<Node3D>();
        viewport.AddChild(sceneInstance);

        // The scene's _Ready() has built the environment by the time this
        // child is attached. Only MeshInstance3D presentation overrides are
        // cloned; StaticBody/CollisionShape/InteractionTarget nodes are left
        // intact and no command/state owner is created.
        var replacementCount = ReplaceSceneMaterials(sceneInstance);
        var expectedImportedModule = name switch
        {
            "day_street" => "HouseA_project_original",
            "kara_urman_edge" => "PineA_project_original",
            _ => string.Empty
        };
        if (expectedImportedModule.Length > 0
            && sceneInstance.GetMeta("styleImportedModules").AsString() != expectedImportedModule)
        {
            viewport.Free();
            Fail($"Benchmark {name} did not materialize expected imported module {expectedImportedModule}.");
            return false;
        }

        var camera = new Camera3D
        {
            Name = "CaptureCamera",
            Position = cameraPosition,
            Fov = 75,
            Current = true
        };
        viewport.AddChild(camera);
        camera.LookAt(cameraTarget, Vector3.Up);

        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var image = viewport.GetTexture().GetImage();
        if (image is null || image.IsEmpty())
        {
            viewport.Free();
            Fail("Texture-candidate capture requires a real rendering driver; dummy/headless rendering cannot produce a frame.");
            return false;
        }

        var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_{name}_texture_candidates_1080p.png");
        var saveError = image.SavePng(outputPath);
        viewport.Free();
        if (saveError != Error.Ok)
        {
            Fail($"Could not save {outputPath}: {saveError}.");
            return false;
        }

        GD.Print($"texture-candidate-frame: {name} {image.GetWidth()}x{image.GetHeight()} replaced_meshes={replacementCount}");
        return true;
    }

    private async Task<bool> CaptureSwatchesAsync(string outputDirectory)
    {
        var viewport = CreateViewport("CandidateSwatches");
        AddChild(viewport);

        var environment = new WorldEnvironment
        {
            Environment = new global::Godot.Environment
            {
                BackgroundMode = global::Godot.Environment.BGMode.Color,
                BackgroundColor = Color.FromHtml("2a302e"),
                AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Color.FromHtml("b8b5a5"),
                AmbientLightEnergy = 0.9f,
                TonemapMode = global::Godot.Environment.ToneMapper.Filmic
            }
        };
        viewport.AddChild(environment);
        viewport.AddChild(new DirectionalLight3D
        {
            Name = "SwatchLight",
            RotationDegrees = new Vector3(-48, -28, 0),
            LightColor = Color.FromHtml("e2d2b0"),
            LightEnergy = 1.4f,
            ShadowEnabled = true
        });

        for (var index = 0; index < ExpectedCandidates.Length; index++)
        {
            var column = index % 3;
            var row = index / 3;
            var swatch = new MeshInstance3D
            {
                Name = $"CandidateSwatch_{ExpectedCandidates[index]}",
                Position = new Vector3((column - 1) * 3.8f, row == 0 ? 1.75f : -1.75f, 0.0f),
                Mesh = new BoxMesh { Size = new Vector3(3.2f, 2.7f, 0.22f) },
                MaterialOverride = CreateSwatchMaterial(ExpectedCandidates[index])
            };
            viewport.AddChild(swatch);
        }

        var camera = new Camera3D
        {
            Name = "SwatchCamera",
            Position = new Vector3(0, 0, 8.0f),
            Fov = 45,
            Current = true
        };
        viewport.AddChild(camera);
        camera.LookAt(new Vector3(0, 0, 0), Vector3.Up);

        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var image = viewport.GetTexture().GetImage();
        if (image is null || image.IsEmpty())
        {
            viewport.Free();
            Fail("Texture swatch requires a real rendering driver; dummy/headless rendering cannot produce a frame.");
            return false;
        }

        var outputPath = System.IO.Path.Combine(outputDirectory, "godot_material_swatches_texture_candidates_1080p.png");
        var saveError = image.SavePng(outputPath);
        viewport.Free();
        if (saveError != Error.Ok)
        {
            Fail($"Could not save {outputPath}: {saveError}.");
            return false;
        }

        GD.Print($"texture-candidate-swatch: all {ExpectedCandidates.Length} {CandidateVersion} candidates rendered; presentation owners are test-only clones.");
        return true;
    }

    private int ReplaceSceneMaterials(Node root)
    {
        var replacements = 0;
        foreach (var child in root.GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                replacements += ReplaceMeshMaterial(mesh);
            }

            replacements += ReplaceSceneMaterials(child);
        }

        return replacements;
    }

    private int ReplaceMeshMaterial(MeshInstance3D mesh)
    {
        if (mesh.MaterialOverride is not ShaderMaterial source)
        {
            return 0;
        }

        var texture = source.GetShaderParameter("albedo_texture").AsGodotObject() as Texture2D;
        if (texture is null)
        {
            return 0;
        }

        var basename = System.IO.Path.GetFileName(texture.ResourcePath);
        if (string.IsNullOrEmpty(basename) || !basename.EndsWith("_albedo.png", StringComparison.Ordinal))
        {
            return 0;
        }

        var candidateName = CandidateNameForVersion(basename, CandidateVersion);
        if (!_sceneCoverage.ContainsKey(candidateName))
        {
            return 0;
        }

        var candidatePath = $"res://assets/textures/painterly/{candidateName}";
        var candidate = ResourceLoader.Load<Texture2D>(candidatePath);
        if (candidate is null)
        {
            Fail($"Could not load candidate texture {candidatePath}.");
            return 0;
        }

        var duplicate = source.Duplicate() as ShaderMaterial;
        if (duplicate is null)
        {
            Fail($"Could not duplicate presentation ShaderMaterial for {mesh.Name}.");
            return 0;
        }

        duplicate.SetShaderParameter("albedo_texture", candidate);
        duplicate.SetShaderParameter("has_albedo_texture", true);
        mesh.MaterialOverride = duplicate;
        _sceneCoverage[candidateName]++;
        return 1;
    }

    private static string CandidateNameForVersion(string basename, string version)
    {
        const string AlbedoSuffix = "_albedo.png";
        var stem = basename.EndsWith(AlbedoSuffix, StringComparison.Ordinal)
            ? basename[..^AlbedoSuffix.Length]
            : basename;
        foreach (var knownVersion in new[] { "_v2", "_v3", "_v4" })
        {
            if (stem.EndsWith(knownVersion, StringComparison.Ordinal))
            {
                stem = stem[..^knownVersion.Length];
                break;
            }
        }

        return $"{stem}_{version}{AlbedoSuffix}";
    }

    private ShaderMaterial CreateSwatchMaterial(string candidateName)
    {
        var surface = CandidateSurface.TryGetValue(candidateName, out var knownSurface) ? knownSurface : string.Empty;
        var template = PainterlyMaterialLibrary.ForColor("857967", surface);
        var duplicate = template.Duplicate() as ShaderMaterial
            ?? throw new InvalidOperationException($"Could not duplicate swatch material for {candidateName}.");
        var texture = ResourceLoader.Load<Texture2D>($"res://assets/textures/painterly/{candidateName}")
            ?? throw new InvalidOperationException($"Could not load swatch texture {candidateName}.");
        duplicate.SetShaderParameter("albedo_texture", texture);
        duplicate.SetShaderParameter("has_albedo_texture", true);
        if (surface.Length == 0)
        {
            duplicate.SetShaderParameter("texture_scale", new Vector2(1.0f, 1.0f));
        }

        return duplicate;
    }

    private static SubViewport CreateViewport(string name) => new()
    {
        Name = name,
        Size = new Vector2I(CaptureWidth, CaptureHeight),
        OwnWorld3D = true,
        RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        Msaa3D = Viewport.Msaa.Msaa2X
    };

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
