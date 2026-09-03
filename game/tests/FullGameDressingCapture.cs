using Godot;

namespace Urman.Godot.Tests;

public partial class FullGameDressingCapture : Node
{
    private static readonly IReadOnlyDictionary<string, string> SceneByZone = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["fullgame_act2_house"] = "res://scenes/zones/fullgame/act2_house.tscn",
        ["fullgame_act2_river"] = "res://scenes/zones/fullgame/act2_river.tscn",
        ["fullgame_act3_archive"] = "res://scenes/zones/fullgame/act3_archive.tscn",
        ["fullgame_act3_soviet"] = "res://scenes/zones/fullgame/act3_soviet.tscn",
        ["fullgame_act4_pact"] = "res://scenes/zones/fullgame/act4_pact.tscn",
        ["fullgame_act5_boundary"] = "res://scenes/zones/fullgame/act5_boundary.tscn",
        ["fullgame_act5_epilogue"] = "res://scenes/zones/fullgame/act5_epilogue.tscn"
    };

    private static readonly (string Name, string ZoneId, Vector3 Camera, Vector3 Target)[] Frames =
    [
        ("act2_house", "fullgame_act2_house", new Vector3(0, 1.7f, 11.5f), new Vector3(-2.5f, 1.35f, -6.2f)),
        ("act2_river", "fullgame_act2_river", new Vector3(0, 1.7f, 12.5f), new Vector3(0, 1.35f, -6.2f)),
        ("act3_archive", "fullgame_act3_archive", new Vector3(0, 1.7f, 11.5f), new Vector3(0, 1.35f, -6.0f)),
        ("act3_soviet", "fullgame_act3_soviet", new Vector3(0, 1.7f, 11.5f), new Vector3(-0.65f, 1.35f, -3.65f)),
        ("act4_pact", "fullgame_act4_pact", new Vector3(0, 1.7f, 11.5f), new Vector3(0, 1.3f, -5.8f)),
        ("act5_boundary", "fullgame_act5_boundary", new Vector3(0, 1.7f, 12.5f), new Vector3(0, 1.35f, -7.2f)),
        ("act5_epilogue", "fullgame_act5_epilogue", new Vector3(0, 1.7f, 12.5f), new Vector3(-3.8f, 1.35f, -7.5f))
    ];

    public override async void _Ready()
    {
        var outputDirectory = ProjectSettings.GlobalizePath("res://../docs/urman_knowledge_base/art/fullgame_frames");
        var directoryError = DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        if (directoryError != Error.Ok)
        {
            Fail($"Could not create full-game frame directory: {directoryError}.");
            return;
        }

        foreach (var frame in Frames)
        {
            if (!await CaptureAsync(frame.Name, frame.ZoneId, frame.Camera, frame.Target, outputDirectory))
            {
                return;
            }
        }

        GD.Print($"fullgame-dressing-capture: {Frames.Length} frames at 1920x1080 -> {outputDirectory}");
        GetTree().Quit(0);
    }

    private async Task<bool> CaptureAsync(
        string name,
        string zoneId,
        Vector3 cameraPosition,
        Vector3 cameraTarget,
        string outputDirectory)
    {
        if (!SceneByZone.TryGetValue(zoneId, out var scenePath))
        {
            Fail($"No authored full-game capture scene is registered for {zoneId}.");
            return false;
        }

        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed is null)
        {
            Fail($"Could not load full-game zone scene: {scenePath}.");
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

        var zone = packed.Instantiate<FullGameZone>();
        viewport.AddChild(zone);

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
            Fail("Full-game dressing capture requires a real rendering driver.");
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

        GD.Print($"fullgame-frame: {name} {image.GetWidth()}x{image.GetHeight()}");
        return true;
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
