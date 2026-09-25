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
            if (OS.GetEnvironment("URMAN_BUILDING_SURVEY") == "1")
            {
                // One line per registered building: where it stands relative to the roads.
                var world = GetTree().Root.FindChild("Act1ConnectedWorld", true, false) as Act1ConnectedWorld
                    ?? throw new InvalidOperationException("no world");
                var registry = world.AddressRegistry ?? throw new InvalidOperationException("no registry");
                foreach (var building in registry.Buildings.Values.OrderBy(b => b.Position.Z))
                {
                    var (distance, halfWidth) = Urman.Experiments.AgentBAct1.AgentBAct1HeightField.RoadInfo((float)building.Position.X, (float)building.Position.Z);
                    var xs = building.Footprint.Select(p => p.X).DefaultIfEmpty(building.Position.X).ToArray();
                    var zs = building.Footprint.Select(p => p.Z).DefaultIfEmpty(building.Position.Z).ToArray();
                    var address = building.AddressId is { } id && registry.Addresses.TryGetValue(id, out var record)
                        ? record.StreetId + " " + record.HouseNumber : "-";
                    var access = registry.AccessPoints.Values.FirstOrDefault(a => a.BuildingId == building.BuildingId);
                    GD.Print(System.FormattableString.Invariant(
                        $"survey|{building.SourceKey}|{building.Role}|{address}|{building.Position.X:0.0}|{building.Position.Z:0.0}|{xs.Min():0.0}|{xs.Max():0.0}|{zs.Min():0.0}|{zs.Max():0.0}|{distance:0.0}|{halfWidth:0.0}|{(access is null ? "" : $"{access.Position.X:0.0},{access.Position.Z:0.0}")}"));
                }
                var fenceWords = new[] { "Fence", "Picket", "Palisade", "Gate", "Rail", "Post", "Paling", "Boundary" };
                foreach (var mesh in world.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
                {
                    var name = mesh.Name.ToString();
                    if (!mesh.IsVisibleInTree() || mesh.Mesh is null || !fenceWords.Any(w => name.Contains(w, StringComparison.Ordinal))) continue;
                    var box = mesh.GlobalTransform * mesh.GetAabb();
                    var centre = box.GetCenter();
                    var owner = mesh.GetParent() is Node3D parent ? parent.Name.ToString() : "";
                    var collided = mesh.GetParent()?.GetChildren().Any(c => c is CollisionObject3D) == true;
                    GD.Print(System.FormattableString.Invariant(
                        $"fence|{name}|{owner}|{centre.X:0.0}|{centre.Z:0.0}|{box.Size.X:0.0}|{box.Size.Z:0.0}|{box.Size.Y:0.0}|{collided}"));
                }
                GetTree().Quit(0);
                return;
            }
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
            if (OS.GetEnvironment("URMAN_NODE_VIEWS") is { Length: > 0 } nodeViews)
            {
                // node|name:lx:ly:lz:tx:ty:tz;... — camera and target in a node's local space;
                // the player stands at the camera so interiors light up as in play.
                var parts0 = nodeViews.Split('|', 2);
                var anchor = GetTree().Root.FindChild(parts0[0], true, false) as Node3D
                    ?? throw new InvalidOperationException("no node " + parts0[0]);
                var player = GetTree().GetFirstNodeInGroup("player_controller") as Node3D;
                // The camera stands where the player's eyes are; hide the body it would see.
                foreach (var visual in player?.FindChildren("*", nameof(VisualInstance3D), true, false).OfType<VisualInstance3D>() ?? [])
                    visual.Visible = false;
                var eye = new Camera3D { Fov = 75f, Near = .03f, Far = 300f };
                AddChild(eye);
                eye.MakeCurrent();
                foreach (var view in parts0[1].Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = view.Split(':');
                    var v = parts.Skip(1).Select(p => float.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    var from = anchor.ToGlobal(new Vector3(v[0], v[1], v[2]));
                    if (player is not null) player.GlobalPosition = anchor.ToGlobal(new Vector3(v[0], 0.05f, v[2]));
                    for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    eye.GlobalPosition = from;
                    eye.LookAt(anchor.ToGlobal(new Vector3(v[3], v[4], v[5])));
                    for (var i = 0; i < 25; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var frame = GetViewport().GetTexture().GetImage();
                    frame.SavePng(System.IO.Path.Combine(OS.GetEnvironment("URMAN_TOPDOWN_OUTPUT"), parts[0] + ".png"));
                }
                GD.Print("act1-topdown: node views done");
                GetTree().Quit(0);
                return;
            }
            if (OS.GetEnvironment("URMAN_VIEWS") is { Length: > 0 } views)
            {
                // name:fromX:fromZ:toX:toZ;... — eye height (1.7 m) looking at a point.
                var eye = new Camera3D { Fov = 70f, Near = .05f, Far = 400f };
                AddChild(eye);
                eye.MakeCurrent();
                foreach (var view in views.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = view.Split(':');
                    var v = parts.Skip(1).Select(p => float.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    eye.GlobalPosition = new Vector3(v[0], Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(v[0], v[1]) + 1.7f, v[1]);
                    eye.LookAt(new Vector3(v[2], Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(v[2], v[3]) + 1.2f, v[3]));
                    for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var frame = GetViewport().GetTexture().GetImage();
                    frame.SavePng(System.IO.Path.Combine(OS.GetEnvironment("URMAN_TOPDOWN_OUTPUT"), parts[0] + ".png"));
                }
                GD.Print("act1-topdown: views done");
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
            // URMAN_TOPDOWN_RECT="cx:cz:size" frames another part of the village.
            var rect = OS.GetEnvironment("URMAN_TOPDOWN_RECT").Split(':', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var (centreX, centreZ, span) = rect.Length == 3 ? (rect[0], rect[1], rect[2]) : (0f, -15f, 170f);
            var camera = new Camera3D
            {
                Projection = Camera3D.ProjectionType.Orthogonal, Size = span, Near = 1f, Far = 400f,
                Position = new Vector3(centreX, 150f, centreZ), RotationDegrees = new Vector3(-90f, 0f, 0f)
            };
            AddChild(camera);
            camera.MakeCurrent();
            for (var i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = GetViewport().GetTexture().GetImage();
            image.SavePng(System.IO.Path.Combine(OS.GetEnvironment("URMAN_TOPDOWN_OUTPUT"), "topdown.png"));
            GD.Print($"act1-topdown: {image.GetWidth()}x{image.GetHeight()} size={camera.Size} centre=({centreX},{centreZ})");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError("act1-topdown: " + error.Message);
            GetTree().Quit(1);
        }
    }
}
