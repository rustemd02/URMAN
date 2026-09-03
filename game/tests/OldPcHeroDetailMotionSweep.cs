using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Test-only first-person readability matrix for the OldPc hero-detail
/// candidate. It renders disposable Act 3 Soviet clones at near/mid distance,
/// three FOV values and three small head-bob offsets. Production scenes,
/// materials, collision ownership and runtime state are never modified.
/// </summary>
public partial class OldPcHeroDetailMotionSweep : Node
{
    private const int TileWidth = 640;
    private const int TileHeight = 360;
    private const int SheetColumns = 3;
    private const int SheetRows = 3;
    private const int WarmupFrames = 8;
    private const int ReadbackAttempts = 4;
    private const int TeardownFrames = 8;

    private static readonly float[] Fovs = [65f, 75f, 90f];

    private static readonly HashSet<string> RequiredHeroDetails =
    [
        "OldPc_DriveSlot_LOD0",
        "OldPc_DriveSlot_LOD1",
        "OldPc_LabelPlate_LOD0",
        "OldPc_LabelPlate_LOD1"
    ];

    private static readonly SweepRow[] Rows =
    [
        new("bob_up", new Vector3(-0.65f, 1.63f, -2.05f), new Vector3(-0.65f, 1.47f, -3.55f)),
        new("neutral", new Vector3(-0.65f, 1.58f, -2.05f), new Vector3(-0.65f, 1.42f, -3.55f)),
        new("bob_down", new Vector3(-0.65f, 1.53f, -2.05f), new Vector3(-0.65f, 1.37f, -3.55f))
    ];

    public override async void _Ready()
    {
        try
        {
            var outputDirectory = ProjectSettings.GlobalizePath(
                "res://../docs/urman_knowledge_base/art/oldpc_hero_detail_motion_sweep");
            if (DirAccess.MakeDirRecursiveAbsolute(outputDirectory) != Error.Ok)
            {
                Fail($"Could not create OldPc motion-sweep directory: {outputDirectory}");
                return;
            }

            var packed = ResourceLoader.Load<PackedScene>("res://scenes/zones/fullgame/act3_soviet.tscn");
            if (packed is null)
            {
                Fail("Could not load Act 3 Soviet scene for OldPc motion sweep.");
                return;
            }

            var context = await PrepareCaptureContextAsync(packed);
            if (context is null)
            {
                return;
            }

            try
            {
                var distances = new[]
                {
                    (Name: "near", CameraZ: -2.05f, TargetZ: -3.55f),
                    (Name: "mid", CameraZ: 0.10f, TargetZ: -3.55f)
                };
                var sheets = new List<object>(distances.Length);
                foreach (var distance in distances)
                {
                    var result = await CaptureDistanceAsync(context, distance.Name, distance.CameraZ, distance.TargetZ, outputDirectory);
                    if (result is null)
                    {
                        return;
                    }

                    sheets.Add(result);
                }

                var manifestPath = System.IO.Path.Combine(outputDirectory, "oldpc_hero_detail_motion_manifest.json");
                var manifest = JsonSerializer.Serialize(
                    new
                    {
                        schema_version = 1,
                        kind = "urman.godot_oldpc_hero_detail_motion_sweep",
                        status = "OPEN",
                        technical_status = "PASS",
                        renderer_expectation = "Godot 4.7.1 .NET Forward+ / Metal or equivalent real 3D driver",
                        tile = new { width = TileWidth, height = TileHeight },
                        sheet = new { columns = SheetColumns, rows = SheetRows, width = 1920, height = 1080 },
                        fovs = Fovs,
                        rows = Rows.Select(row => row.Name).ToArray(),
                        required_hero_details = RequiredHeroDetails.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                        source_collision_policy = "production scene unchanged; disposable clone removes only CollisionShape3D and zeros physics layers",
                        capture_world_policy = "one isolated SubViewport/FullGameZone reused for near+mid to avoid renderer resource churn",
                        sheets
                    },
                    new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(manifestPath, manifest + System.Environment.NewLine);

                GD.Print($"oldpc-hero-detail-motion-sweep: {sheets.Count} contact sheets at 1920x1080 -> {outputDirectory}");
                GetTree().Quit(0);
            }
            finally
            {
                if (GodotObject.IsInstanceValid(context.Viewport))
                {
                    context.Viewport.Free();
                    await WaitForFramesAsync(TeardownFrames);
                }
            }
        }
        catch (Exception exception)
        {
            Fail($"OldPc motion sweep failed: {exception}");
        }
    }

    private async Task<CaptureContext?> PrepareCaptureContextAsync(PackedScene packed)
    {
        var viewport = new SubViewport
        {
            Name = "OldPcMotionWorld",
            Size = new Vector2I(TileWidth, TileHeight),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);

        var zone = packed.Instantiate<FullGameZone>();
        viewport.AddChild(zone);
        await WaitForFramesAsync(WarmupFrames);

        var details = zone
            .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .Select(mesh => mesh.Name.ToString())
            .Where(RequiredHeroDetails.Contains)
            .ToHashSet(StringComparer.Ordinal);
        if (!details.SetEquals(RequiredHeroDetails))
        {
            viewport.Free();
            await WaitForFramesAsync(TeardownFrames);
            Fail($"OldPc motion sweep is missing details: {string.Join(", ", details.OrderBy(name => name, StringComparer.Ordinal))}");
            return null;
        }

        StripPhysicsForRenderOnlyCapture(zone);
        var camera = new Camera3D
        {
            Name = "OldPcMotionCamera",
            Current = true
        };
        viewport.AddChild(camera);
        return new CaptureContext(viewport, zone, camera, details.Count);
    }

    private async Task<object?> CaptureDistanceAsync(
        CaptureContext context,
        string distance,
        float cameraZ,
        float targetZ,
        string outputDirectory)
    {
        var sheet = Image.CreateEmpty(TileWidth * SheetColumns, TileHeight * SheetRows, false, Image.Format.Rgba8);
        sheet.Fill(new Color(0.035f, 0.043f, 0.045f, 1f));
        var frames = new List<object>(SheetRows * SheetColumns);

        for (var row = 0; row < SheetRows; row++)
        {
            var rowSpec = Rows[row];
            for (var column = 0; column < SheetColumns; column++)
            {
                var cameraPosition = new Vector3(-0.65f, rowSpec.Camera.Y, cameraZ);
                var target = new Vector3(-0.65f, rowSpec.Target.Y, targetZ);
                context.Camera.Position = cameraPosition;
                context.Camera.Fov = Fovs[column];
                context.Camera.LookAt(target, Vector3.Up);
                await WaitForFramesAsync(WarmupFrames);

                var tile = await ReadbackTileAsync(context.Viewport, distance, rowSpec.Name, Fovs[column]);
                if (tile is null)
                {
                    return null;
                }

                sheet.BlitRect(
                    tile.Image,
                    new Rect2I(0, 0, TileWidth, TileHeight),
                    new Vector2I(column * TileWidth, row * TileHeight));
                frames.Add(new
                {
                    row = rowSpec.Name,
                    column,
                    fov = Fovs[column],
                    details_found = context.DetailCount,
                    luma_span = tile.LumaSpan,
                    camera = new { x = cameraPosition.X, y = cameraPosition.Y, z = cameraPosition.Z },
                    target = new { x = target.X, y = target.Y, z = target.Z }
                });
            }
        }

        var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_oldpc_{distance}_motion_1080p.png");
        if (sheet.SavePng(outputPath) != Error.Ok)
        {
            Fail($"Could not save OldPc motion sheet: {outputPath}");
            return null;
        }

        GD.Print($"oldpc-hero-detail-motion-sheet: {distance} {sheet.GetWidth()}x{sheet.GetHeight()} details={context.DetailCount}/4");
        var repoRoot = ProjectSettings.GlobalizePath("res://..");
        return new
        {
            name = distance,
            output = System.IO.Path.GetRelativePath(repoRoot, outputPath).Replace('\\', '/'),
            frames
        };
    }

    private async Task<ReadbackTile?> ReadbackTileAsync(
        SubViewport viewport,
        string distance,
        string row,
        float fov)
    {
        Image? readback = null;
        var acceptedLumaSpan = 0f;
        for (var attempt = 0; attempt < ReadbackAttempts; attempt++)
        {
            var image = viewport.GetTexture().GetImage();
            if (image is not null
                && !image.IsEmpty()
                && image.GetWidth() == TileWidth
                && image.GetHeight() == TileHeight)
            {
                // The Forward+ readback may be RGB/RGBE. Normalize before
                // sampling; GetPixel on the HDR format is not a valid
                // non-empty test and can falsely look uniform.
                image.Convert(Image.Format.Rgba8);
                // GetImage() may remain backed by the SubViewport texture.
                // Copy the pixels before freeing that viewport; otherwise a
                // later contact-sheet blit can observe a cleared/reused
                // render target (blank tiles).
                var sourceLumaSpan = RenderedLumaSpan(image);
                var copy = image.Duplicate() as Image;
                var lumaSpan = copy is null ? 0f : RenderedLumaSpan(copy);
                GD.Print($"oldpc-hero-detail-readback: {distance}/{row}/{fov} attempt={attempt + 1} source_luma_span={sourceLumaSpan:0.000} copy_luma_span={lumaSpan:0.000}");
                if (copy is not null && lumaSpan >= 0.08f)
                {
                    readback = copy;
                    acceptedLumaSpan = lumaSpan;
                    break;
                }
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (readback is null)
        {
            Fail($"OldPc motion tile requires a real non-empty {TileWidth}x{TileHeight} render for {distance}/{row}/{fov}");
            return null;
        }

        return new ReadbackTile(readback, acceptedLumaSpan);
    }

    private async Task WaitForFramesAsync(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static float RenderedLumaSpan(Image image)
    {
        var minLuma = 1f;
        var maxLuma = 0f;
        const int sampleColumns = 12;
        const int sampleRows = 8;
        for (var y = 0; y < sampleRows; y++)
        {
            for (var x = 0; x < sampleColumns; x++)
            {
                var sample = image.GetPixel(
                    x * (TileWidth - 1) / (sampleColumns - 1),
                    y * (TileHeight - 1) / (sampleRows - 1));
                var luma = Mathf.Clamp(sample.R * 0.2126f + sample.G * 0.7152f + sample.B * 0.0722f, 0f, 1f);
                minLuma = Mathf.Min(minLuma, luma);
                maxLuma = Mathf.Max(maxLuma, luma);
            }
        }

        // A cleared SubViewport readback is uniform. The Act 3 Soviet scene
        // necessarily contains sky, ground, trees and the OldPc hero prop, so
        // a small luminance span is a conservative technical non-empty gate,
        // not an art-acceptance score.
        return maxLuma - minLuma;
    }

    private static void StripPhysicsForRenderOnlyCapture(Node root)
    {
        if (root is CollisionObject3D collisionObject)
        {
            collisionObject.CollisionLayer = 0;
            collisionObject.CollisionMask = 0;
            collisionObject.InputRayPickable = false;
        }

        foreach (var child in root.GetChildren().ToArray())
        {
            if (child is CollisionShape3D collisionShape)
            {
                // The disposable clone removes the node from the physics
                // tree. Do not manually Dispose() the Shape3D resource here:
                // imported PackedScene subresources may be shared by the
                // next clone, and Godot's ref-counting owns their RID.
                collisionShape.Shape = null;
                collisionShape.Free();
                continue;
            }

            StripPhysicsForRenderOnlyCapture(child);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }

    private sealed record SweepRow(string Name, Vector3 Camera, Vector3 Target);

    private sealed record CaptureContext(SubViewport Viewport, FullGameZone Zone, Camera3D Camera, int DetailCount);

    private sealed record ReadbackTile(Image Image, float LumaSpan);
}
