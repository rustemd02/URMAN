using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Close first-person evidence for the project-original OldPc hero details.
/// This is a disposable capture only: it does not change the production zone,
/// material library, collision owner, narrative state or interaction runtime.
/// </summary>
public partial class OldPcHeroDetailCapture : Node
{
    private static readonly (string Name, Vector3 Camera, Vector3 Target)[] Frames =
    [
        ("oldpc_close_front", new Vector3(-0.65f, 1.58f, -2.05f), new Vector3(-0.65f, 1.42f, -3.55f)),
        ("oldpc_close_side", new Vector3(0.55f, 1.55f, -2.30f), new Vector3(-0.55f, 1.42f, -3.55f))
    ];

    private static readonly HashSet<string> RequiredHeroDetails =
    [
        "OldPc_DriveSlot_LOD0",
        "OldPc_DriveSlot_LOD1",
        "OldPc_LabelPlate_LOD0",
        "OldPc_LabelPlate_LOD1"
    ];

    public override async void _Ready()
    {
        var outputDirectory = ProjectSettings.GlobalizePath("res://../docs/urman_knowledge_base/art/oldpc_hero_detail_candidate");
        if (DirAccess.MakeDirRecursiveAbsolute(outputDirectory) != Error.Ok)
        {
            Fail($"Could not create OldPc candidate directory: {outputDirectory}");
            return;
        }

        foreach (var frame in Frames)
        {
            if (!await CaptureAsync(frame.Name, frame.Camera, frame.Target, outputDirectory))
            {
                return;
            }
        }

        GD.Print($"oldpc-hero-detail-capture: {Frames.Length} frames at 1920x1080 -> {outputDirectory}");
        GetTree().Quit(0);
    }

    private async Task<bool> CaptureAsync(string name, Vector3 cameraPosition, Vector3 cameraTarget, string outputDirectory)
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/zones/fullgame/act3_soviet.tscn");
        if (packed is null)
        {
            Fail("Could not load the Act 3 Soviet capture scene.");
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
        for (var frame = 0; frame < 10; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var actualHeroDetails = zone
            .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .Select(mesh => mesh.Name.ToString())
            .Where(RequiredHeroDetails.Contains)
            .ToHashSet(StringComparer.Ordinal);
        if (!actualHeroDetails.SetEquals(RequiredHeroDetails))
        {
            viewport.Free();
            Fail($"OldPc close capture is missing hero-detail pairs: {string.Join(", ", actualHeroDetails.OrderBy(name => name, StringComparer.Ordinal))}");
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
            Fail("OldPc candidate capture requires a real rendering driver.");
            return false;
        }

        var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_{name}_1080p.png");
        var saveError = image.SavePng(outputPath);
        viewport.Free();
        if (saveError != Error.Ok)
        {
            Fail($"Could not save {outputPath}: {saveError}");
            return false;
        }

        GD.Print($"oldpc-hero-detail-frame: {name} {image.GetWidth()}x{image.GetHeight()} details={actualHeroDetails.Count}/4");
        return true;
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
