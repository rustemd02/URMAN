using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Diagnostic: an orthographic view straight down on the connected village,
/// for reviewing what stands where. Needs a native window. Writes the PNG to
/// URMAN_TOPDOWN_OUTPUT; the frame spans x -70..70 and z -100..70.
/// </summary>
public partial class Act1TopDownCapture : Node
{
    public override async void _Ready()
    {
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await this.StartThroughMainMenuAsync(demo)) throw new InvalidOperationException("demo did not start");
            foreach (var layer in GetTree().Root.FindChildren("*", nameof(CanvasLayer), true, false).OfType<CanvasLayer>())
                layer.Visible = false;
            if (OS.GetEnvironment("URMAN_MINIMAP_SHOTS") is { Length: > 0 } minimapShots)
            {
                // name:x:z;... — the player's own view with the dev minimap open.
                var layer = new CanvasLayer { Layer = 90 };
                AddChild(layer);
                var minimap = new DebugVillageMinimap { Visible = true };
                layer.AddChild(minimap);
                var player = GetTree().GetFirstNodeInGroup("player_controller") as Node3D
                    ?? throw new InvalidOperationException("no player");
                foreach (var shot in minimapShots.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = shot.Split(':');
                    var x = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                    var z = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                    player.GlobalPosition = new Vector3(x, Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(x, z) + .1f, z);
                    for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var frame = GetViewport().GetTexture().GetImage();
                    frame.SavePng(System.IO.Path.Combine(OS.GetEnvironment("URMAN_TOPDOWN_OUTPUT"), parts[0] + ".png"));
                }
                GD.Print("act1-topdown: minimap shots done");
                GetTree().Quit(0);
                return;
            }
            if (OS.GetEnvironment("URMAN_PLATE_SHOTS") is { Length: > 0 } plateCount)
            {
                // Eye-level look at the first N address plates, 2.6 m in front.
                var eye = new Camera3D { Fov = 55f, Near = .05f, Far = 200f };
                AddChild(eye);
                eye.MakeCurrent();
                var plates = GetTree().Root.FindChildren("AddressPlate_*", "", true, false).OfType<Node3D>()
                    .Where(plate => plate.IsVisibleInTree()).OrderBy(plate => plate.Name.ToString()).ToArray();
                var step = Math.Max(1, plates.Length / int.Parse(plateCount));
                for (var index = 0; index < plates.Length; index += step)
                {
                    var plate = plates[index];
                    var facing = plate.GlobalBasis.Z.Normalized();
                    eye.GlobalPosition = plate.GlobalPosition + facing * 2.6f + Vector3.Down * .25f;
                    eye.LookAt(plate.GlobalPosition + Vector3.Down * .2f);
                    for (var i = 0; i < 12; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var frame = GetViewport().GetTexture().GetImage();
                    frame.SavePng(System.IO.Path.Combine(OS.GetEnvironment("URMAN_TOPDOWN_OUTPUT"), plate.Name + ".png"));
                }
                GetTree().Quit(0);
                return;
            }
            var shots = OS.GetEnvironment("URMAN_SHOTS");
            if (shots.Length > 0)
            {
                // name:x:z;... — an oblique look at each spot from the south-east.
                var oblique = new Camera3D { Fov = 60f, Near = .1f, Far = 400f };
                AddChild(oblique);
                oblique.MakeCurrent();
                foreach (var shot in shots.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = shot.Split(':');
                    var x = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                    var z = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                    var ground = Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(x, z);
                    var target = new Vector3(x, ground + 1.5f, z);
                    var reach = parts.Length > 3 ? float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture) : 1f;
                    oblique.GlobalPosition = target + new Vector3(11f, 7f, 11f) * reach;
                    oblique.LookAt(target);
                    for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var frame = GetViewport().GetTexture().GetImage();
                    frame.SavePng(System.IO.Path.Combine(OS.GetEnvironment("URMAN_TOPDOWN_OUTPUT"), parts[0] + ".png"));
                }
                GD.Print("act1-topdown: shots done");
                GetTree().Quit(0);
                return;
            }
            var camera = new Camera3D
            {
                Projection = Camera3D.ProjectionType.Orthogonal, Size = 170f, Near = 1f, Far = 400f,
                Position = new Vector3(0f, 150f, -15f), RotationDegrees = new Vector3(-90f, 0f, 0f)
            };
            AddChild(camera);
            camera.MakeCurrent();
            for (var i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = GetViewport().GetTexture().GetImage();
            image.SavePng(OS.GetEnvironment("URMAN_TOPDOWN_OUTPUT"));
            GD.Print($"act1-topdown: {image.GetWidth()}x{image.GetHeight()} size={camera.Size} centre=(0,-15)");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError("act1-topdown: " + error.Message);
            GetTree().Quit(1);
        }
    }
}
