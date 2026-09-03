using Godot;

namespace Urman.Godot.Tests;

public partial class StyleFrameCapture : Node
{
    private static readonly (string Name, string ScenePath, Vector3 Camera, Vector3 Target)[] Frames =
    [
        ("day_street", "res://scenes/zones/style_benchmark_day_street.tscn", new Vector3(0, 1.7f, 12.5f), new Vector3(0, 1.45f, -7.5f)),
        ("house_old_pc", "res://scenes/zones/style_benchmark_house_pc.tscn", new Vector3(-1.45f, 1.68f, 1.75f), new Vector3(0, 1.38f, -3.45f)),
        ("kara_urman_edge", "res://scenes/zones/style_benchmark_kara_urman_night.tscn", new Vector3(0, 1.7f, 12.5f), new Vector3(0.55f, 1.35f, -7.2f))
    ];

    public override async void _Ready()
    {
        var outputDirectory = ProjectSettings.GlobalizePath("res://../docs/urman_knowledge_base/art/style_frames");
        var directoryError = DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        if (directoryError != Error.Ok)
        {
            Fail($"Could not create style-frame directory: {directoryError}.");
            return;
        }

        foreach (var frame in Frames)
        {
            if (!await CaptureAsync(frame.Name, frame.ScenePath, frame.Camera, frame.Target, outputDirectory))
            {
                return;
            }
        }

        GD.Print($"style-frame-capture: {Frames.Length} frames at 1920x1080 -> {outputDirectory}");
        GetTree().Quit(0);
    }

    private async Task<bool> CaptureAsync(
        string name,
        string scenePath,
        Vector3 cameraPosition,
        Vector3 cameraTarget,
        string outputDirectory)
    {
        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed is null)
        {
            Fail($"Could not load style benchmark {scenePath}.");
            return false;
        }

        var viewport = new SubViewport
        {
            Name = $"Capture_{name}",
            Size = new Vector2I(1920, 1080),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);
        var sceneInstance = packed.Instantiate<Node3D>();
        viewport.AddChild(sceneInstance);

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

        var expectedImportedModule = name switch
        {
            "day_street" => "HouseA_project_original",
            "house_old_pc" => "OldPc_project_original",
            "kara_urman_edge" => "PineA_project_original",
            _ => string.Empty
        };
        if (expectedImportedModule.Length > 0
            && sceneInstance.GetMeta("styleImportedModules").AsString() != expectedImportedModule)
        {
            viewport.Free();
            Fail($"Style benchmark {name} did not materialize the expected imported module {expectedImportedModule}.");
            return false;
        }

        if (name == "day_street")
        {
            if (sceneInstance.GetMeta("stylePresentationModules").AsString()
                != "WellA_project_original|WoodpileA_project_original")
            {
                viewport.Free();
                Fail("Day street capture did not materialize the authored WellA/WoodpileA presentation modules.");
                return false;
            }

            var sign = sceneInstance.GetNodeOrNull<Label3D>("VillageSignText");
            if (sign is null || sign.Text != "ФАП" || sign.GetMeta("wayfindingLandmark").AsString() != "fap")
            {
                viewport.Free();
                Fail("Day street capture did not materialize the diegetic ФАП wayfinding landmark.");
                return false;
            }

            foreach (var (nodeName, moduleName) in new[]
                     {
                         ("GeneratedWellA", "WellA_project_original"),
                         ("GeneratedWoodpileA", "WoodpileA_project_original")
                     })
            {
                var module = sceneInstance.GetNodeOrNull<Node3D>(nodeName);
                var collisionObjects = module?.FindChildren("*", nameof(CollisionObject3D), recursive: true, owned: false).Count ?? -1;
                var collisionShapes = module?.FindChildren("*", nameof(CollisionShape3D), recursive: true, owned: false).Count ?? -1;
                if (module is null
                    || !module.GetMeta("presentationOnlyInstance").AsBool()
                    || module.GetMeta("stylePresentationModule").AsString() != moduleName
                    || module.GetMeta("collisionShapeCount").AsInt32() != 0
                    || collisionObjects != 0
                    || collisionShapes != 0)
                {
                    viewport.Free();
                    Fail($"Day street capture module {nodeName} must be presentation-only with zero physics descendants.");
                    return false;
                }
            }
        }

        if (name == "house_old_pc")
        {
            var module = sceneInstance.GetNodeOrNull<Node3D>("GeneratedOldPcAct1");
            var collisionObjects = module?.FindChildren("*", nameof(CollisionObject3D), recursive: true, owned: false).Count ?? -1;
            var collisionShapes = module?.FindChildren("*", nameof(CollisionShape3D), recursive: true, owned: false).Count ?? -1;
            var interaction = sceneInstance.GetNodeOrNull<InteractionTarget>("OldPc");
            if (module is null
                || !module.GetMeta("presentationOnlyInstance").AsBool()
                || module.GetMeta("stylePresentationModule").AsString() != "OldPc_project_original"
                || module.GetMeta("visibleMeshCount").AsInt32() != 16
                || module.GetMeta("lod0Count").AsInt32() != 8
                || module.GetMeta("lod1Count").AsInt32() != 8
                || module.GetMeta("collisionShapeCount").AsInt32() != 0
                || module.GetMeta("importedCollisionObjectsRemoved").AsInt32() <= 0
                || module.GetMeta("importedCollisionShapesRemoved").AsInt32() <= 0
                || collisionObjects != 0
                || collisionShapes != 0
                || interaction is null
                || interaction.InteractionId != "urman.chapter1:interaction/oldpc-power"
                || interaction.GetNodeOrNull<CollisionShape3D>("InteractionProxyCollisionShape") is null)
            {
                viewport.Free();
                Fail("House capture did not materialize the presentation-only OldPc module beside the gameplay interaction target.");
                return false;
            }
        }

        var image = viewport.GetTexture().GetImage();
        if (image is null || image.IsEmpty())
        {
            viewport.Free();
            Fail("Style capture requires a real rendering driver; dummy/headless rendering cannot produce a frame.");
            return false;
        }

        var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_{name}_1080p.png");
        var saveError = image.SavePng(outputPath);
        viewport.Free();
        if (saveError != Error.Ok)
        {
            Fail($"Could not save {outputPath}: {saveError}.");
            return false;
        }

        GD.Print($"style-frame: {name} {image.GetWidth()}x{image.GetHeight()}");
        return true;
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
