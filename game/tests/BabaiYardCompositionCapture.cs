using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// ACT1-VILLAGE-COMPOSITION, babay's yard: prints what actually stands in the
/// yard (name, position, visibility) and writes overview frames to
/// URMAN_YARD_FRAMES so the clutter list is based on the built world rather
/// than on the builder source. Read-only: no state, no saves.
/// </summary>
public partial class BabaiYardCompositionCapture : Node
{
    private string _output = "/tmp/yard_frames";

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            _output = System.Environment.GetEnvironmentVariable("URMAN_YARD_FRAMES") ?? _output;
            Directory.CreateDirectory(_output);
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await this.StartThroughMainMenuAsync(demo)) throw new InvalidOperationException("no start");
            demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            var world = GetTree().GetFirstNodeInGroup("act1_connected_world");
            var core = world.GetNode<Node3D>("Act1CoreWorldGreybox");

            // Only what actually stands inside the yard's own box: the built
            // world is what the author sees, not the builder source.
            var box = new Rect2(-29f, 3f, 9f, 7f);
            var rows = new List<(string Path, float X, float Z)>();
            foreach (var node in Descendants(core))
            {
                if (!node.IsVisibleInTree()) continue;
                var position = node.GlobalPosition;
                if (!box.HasPoint(new Vector2(position.X, position.Z))) continue;
                // Only the outermost node of a group: children repeat the name.
                if (node.GetParent() is Node3D parent
                    && box.HasPoint(new Vector2(parent.GlobalPosition.X, parent.GlobalPosition.Z))
                    && node.GetParent().Name.ToString() == node.Name.ToString()) continue;
                rows.Add((node.GetPath().ToString().Replace(
                        "/root/Act1Demo/Main/ZoneHost/Act1ConnectedWorld/Act1CoreWorldGreybox/", string.Empty),
                    position.X, position.Z));
            }

            foreach (var row in rows.OrderBy(row => row.Path, StringComparer.Ordinal))
            {
                GD.Print($"yard-inventory: {row.Path} at ({row.X:0.0}, {row.Z:0.0})");
            }

            GD.Print($"yard-inventory: visible rows={rows.Count}");

            // Vantage points: the street in front of the yard, the entry gate,
            // the middle of the yard and the back, all at eye height.
            var yard = new Vector2(-26.0f, 3.0f);
            await Shoot(player, new Vector3(-21.6f, .1f, 3.4f), new Vector2(-27.5f, 3.2f), "01_from_street");
            await Shoot(player, new Vector3(-22.4f, .1f, 1.6f), new Vector2(-29.0f, 4.0f), "02_from_gate");
            await Shoot(player, new Vector3(-25.0f, .1f, 6.6f), new Vector2(-31.0f, 1.5f), "03_inside_east");
            await Shoot(player, new Vector3(-30.4f, .1f, 6.2f), new Vector2(-23.0f, 2.0f), "04_inside_west");
            await Shoot(player, new Vector3(-27.0f, .1f, -2.2f), new Vector2(-26.5f, 6.0f), "05_from_house");
            exit = 0;
            GD.Print($"yard-composition-capture: done -> {_output}");
        }
        catch (Exception error)
        {
            GD.PushError("yard-composition-capture failed: " + error);
        }
        finally
        {
            GetTree().Quit(exit);
        }
    }

    private static IEnumerable<Node3D> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is not Node3D spatial) continue;
            yield return spatial;
            foreach (var nested in Descendants(spatial))
            {
                yield return nested;
            }
        }
    }

    private async Task Shoot(FirstPersonController player, Vector3 from, Vector2 look, string name)
    {
        player.GlobalPosition = from;
        var direction = new Vector3(look.X - from.X, 0f, look.Y - from.Z);
        player.RotationDegrees = new Vector3(0, Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Z)), 0);
        for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        image.Convert(Image.Format.Rgba8);
        var path = Path.Combine(_output, name + ".png");
        image.SavePng(path);
        GD.Print($"yard-composition-capture: saved {path}");
    }
}
