using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Diagnostic (no rendering): for every north-street household it scans the yard
/// for a free standing spot (physics bodies and visible props kept clear, ground
/// nearly flat, off the gate-to-door line) and reports it as a "resident-spot"
/// line; for every bench it reports the seat point and the facing (away from the
/// backrest) so a seated resident can be placed. Feeds tools/world/generate_residents.py.
/// </summary>
public partial class Act1ResidentSpotCheck : Node
{
    public override async void _Ready()
    {
        var exit = 0;
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await this.StartThroughMainMenuAsync(demo)) throw new InvalidOperationException("demo did not start");
            if (demo.PrologueActive)
            {
                demo._Input(new InputEventAction { Action = "ui_cancel", Pressed = true });
                for (var frame = 0; frame < 1200 && demo.PrologueActive; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            for (var i = 0; i < 120; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            var director = AuthoredWorldDirector.Current(GetTree()) ?? throw new InvalidOperationException("no director");
            var space = GetViewport().World3D.DirectSpaceState;
            var world = GetTree().Root.FindChild("Act1ConnectedWorld", true, false) as Node3D ?? throw new InvalidOperationException("no world");
            var props = world.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                .Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null)
                .Select(mesh => (Mesh: mesh, Box: mesh.GlobalTransform * mesh.GetAabb()))
                .Where(item => item.Box.Size.Y is > .5f and < 14f && Mathf.Max(item.Box.Size.X, item.Box.Size.Z) < 30f
                    && !item.Mesh.Name.ToString().Contains("Terrain", StringComparison.OrdinalIgnoreCase)
                    && !item.Mesh.Name.ToString().Contains("Ground", StringComparison.OrdinalIgnoreCase)
                    && !item.Mesh.Name.ToString().Contains("Snow", StringComparison.OrdinalIgnoreCase)
                    && !item.Mesh.Name.ToString().Contains("Road", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var cylinder = new CylinderShape3D { Radius = .45f, Height = 1.7f };

            bool Free(Vector3 at, out string why)
            {
                why = "";
                var ground = AgentBAct1HeightField.CollisionGround(at.X, at.Z);
                var slope = Mathf.Max(Mathf.Abs(AgentBAct1HeightField.CollisionGround(at.X + 1, at.Z) - ground),
                    Mathf.Abs(AgentBAct1HeightField.CollisionGround(at.X, at.Z + 1) - ground));
                if (slope > .35f) { why = "slope"; return false; }
                var query = new PhysicsShapeQueryParameters3D
                {
                    Shape = cylinder, Transform = new Transform3D(Basis.Identity, new Vector3(at.X, ground + .12f + .85f, at.Z)),
                    CollisionMask = uint.MaxValue, CollideWithBodies = true, CollideWithAreas = false
                };
                foreach (var hit in space.IntersectShape(query, 8))
                {
                    var name = (hit["collider"].AsGodotObject() as Node)?.Name.ToString() ?? "";
                    if (name.Contains("Terrain", StringComparison.Ordinal) || name == "Player") continue;
                    why = "body:" + name; return false;
                }
                var cell = new Aabb(new Vector3(at.X - .6f, ground + .15f, at.Z - .6f), new Vector3(1.2f, 1.6f, 1.2f));
                foreach (var (mesh, box) in props)
                    if (cell.Intersects(box.Grow(-.05f))) { why = "prop:" + mesh.Name; return false; }
                return true;
            }

            var houses = director.ObjectIds.Where(id => id.EndsWith("-house", StringComparison.Ordinal) && id.Contains("north-street", StringComparison.Ordinal)).Order().ToArray();
            foreach (var id in houses)
            {
                var root = director.ObjectRoot(id)!;
                var slug = id.Split('/')[^1].Replace("-house", "");
                var yaw = root.RotationDegrees.Y;
                var front = new Vector3(Mathf.Sin(Mathf.DegToRad(yaw)), 0, Mathf.Cos(Mathf.DegToRad(yaw)));
                var side = new Vector3(front.Z, 0, -front.X);
                var found = new List<string>();
                foreach (var forward in new[] { 4.5f, 5.5f, 6.5f, 3.5f, 7.5f })
                foreach (var lateral in new[] { 2.4f, -2.4f, 3.2f, -3.2f, 3.9f, -3.9f })
                {
                    var at = root.GlobalPosition + front * forward + side * lateral;
                    if (Free(at, out _)) found.Add($"{at.X:0.00},{at.Z:0.00}");
                    if (found.Count >= 2) break;
                }
                GD.Print($"resident-spot|{slug}|{root.GlobalPosition.X:0.00}|{root.GlobalPosition.Z:0.00}|{yaw:0.0}|{string.Join(";", found)}");
            }

            foreach (var id in director.ObjectIds.Where(id => id.Contains("bench", StringComparison.Ordinal)).Order())
            {
                var root = director.ObjectRoot(id)!;
                var seat = root.FindChildren("*Seat*", "", true, false).OfType<Node3D>().FirstOrDefault();
                var back = root.FindChildren("*Back*", "", true, false).OfType<Node3D>().FirstOrDefault();
                if (seat is null || back is null) { GD.Print($"bench-spot|{id.Split('/')[^1]}|no seat/back nodes"); continue; }
                var box = seat is MeshInstance3D seatMesh ? seatMesh.GlobalTransform * seatMesh.GetAabb() : new Aabb(seat.GlobalPosition, Vector3.One);
                var backBox = back is MeshInstance3D backMesh ? backMesh.GlobalTransform * backMesh.GetAabb() : new Aabb(back.GlobalPosition, Vector3.One);
                var centre = box.GetCenter();
                var towardBack = (backBox.GetCenter() - centre) with { Y = 0 };
                var facing = (-towardBack).Normalized();
                var facingYaw = Mathf.RadToDeg(Mathf.Atan2(facing.X, facing.Z));
                var ground = AgentBAct1HeightField.CollisionGround(centre.X, centre.Z);
                GD.Print($"bench-spot|{id.Split('/')[^1]}|{centre.X:0.00}|{centre.Z:0.00}|seatTop={box.End.Y - ground:0.00}|faceYaw={facingYaw:0.0}");
            }
            // Free spots around named anchors: URMAN_SPOT_ANCHORS="name:x:z;..." (square well, board, ...).
            if (OS.GetEnvironment("URMAN_SPOT_ANCHORS") is { Length: > 0 } anchors)
                foreach (var anchor in anchors.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = anchor.Split(':');
                    var centre = new Vector3(float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), 0,
                        float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
                    var found = new List<string>();
                    foreach (var radius in new[] { 1.6f, 2.2f, 2.8f, 3.4f })
                    for (var step = 0; step < 12; step++)
                    {
                        var angle = step * Mathf.Tau / 12f;
                        var at = centre + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                        if (Free(at, out _)) found.Add($"{at.X:0.00},{at.Z:0.00}");
                    }
                    GD.Print($"anchor-spot|{parts[0]}|{string.Join(";", found.Take(10))}");
                }

            // Built residents: did every routine block arrive and play its occupation?
            foreach (var id in director.ObjectIds.Where(id => id.Contains("/residents/", StringComparison.Ordinal)).Order())
            {
                var (block, status, _) = director.RoutineStatus(id);
                var root = director.ObjectRoot(id);
                GD.Print($"resident-built|{id.Split('/')[^1]}|block={block}|{status}|at=({root?.GlobalPosition.X:0.0},{root?.GlobalPosition.Z:0.0})|clip={root?.GetChildren().OfType<Node3D>().FirstOrDefault()?.GetMetaOrDefault("animationClip")}");
            }
            // Greeting: walk up to a few residents and read what they said.
            var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController ?? throw new InvalidOperationException("no player");
            foreach (var id in new[] { "m-e1-a", "kid-1", "lookout-sitter", "m-e3b-a" })
            {
                var resident = director.ObjectRoot("urman.world:act1/residents/" + id);
                if (resident is null) { GD.Print($"greeting-check|{id}|missing"); continue; }
                player.GlobalPosition = resident.GlobalPosition + new Vector3(id == "lookout-sitter" ? -1.4f : 1.4f, .1f, 0);
                // The shared pause between two greetings is real time (7 s); the resident keeps trying while we stand close.
                var waitUntil = Time.GetTicksMsec() + 17000; // two neighbours within reach take turns 7 s apart
                var frames = 0; var modalFrames = 0; var startedAt = Time.GetTicksMsec();
                while (!resident.HasMeta("lastGreeting") && Time.GetTicksMsec() < waitUntil)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    frames++; if (player.ModalOpen) modalFrames++;
                }
                GD.Print($"greeting-wait|{id}|frames={frames}|modalFrames={modalFrames}|ms={Time.GetTicksMsec() - startedAt}");
                var said = resident.HasMeta("lastGreeting") ? resident.GetMeta("lastGreeting").AsString() : "NOTHING";
                GD.Print($"greeting-check|{id}|{said}|yaw={resident.RotationDegrees.Y:0}|dist={resident.GlobalPosition.DistanceTo(player.GlobalPosition):0.0}|player=({player.GlobalPosition.X:0.0},{player.GlobalPosition.Y:0.0},{player.GlobalPosition.Z:0.0})|npc=({resident.GlobalPosition.X:0.0},{resident.GlobalPosition.Y:0.0},{resident.GlobalPosition.Z:0.0})|modal={player.ModalOpen}");
                player.GlobalPosition = resident.GlobalPosition + new Vector3(12f, .1f, 0);
                for (var i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            GD.Print("resident-spots: done");
        }
        catch (Exception error)
        {
            GD.PrintErr("resident-spots: FAIL " + error);
            exit = 1;
        }
        GetTree().Quit(exit);
    }
}

internal static class NodeMetaExtensions
{
    public static string GetMetaOrDefault(this Node node, string key) => node.HasMeta(key) ? node.GetMeta(key).AsString() : "";
}
