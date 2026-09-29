using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Diagnostic for the prologue ride's village leg (no rendering needed): drives
/// a Niva-sized box along the route every metre and reports what it touches -
/// physics bodies, visible props without collision, stretches off the roads and
/// steep ground. Exit code 1 when anything blocks the car.
/// </summary>
public partial class Act1RideRouteCheck : Node
{
    private const float BodyWidth = 1.75f, BodyLength = 4.1f, BodyHeight = 1.5f, WheelClearance = .35f;

    // Same surface the ride itself uses: the intact span's deck across the ravine, terrain elsewhere.
    private static bool IsOnBridge(Vector3 at)
        => Mathf.Abs(at.Z - Act1ConnectedWorld.RavineBridgeZ) < 1.6f
           && Mathf.Abs(at.X - (float)AgentBAct1HeightField.RavineCentre(Act1ConnectedWorld.RavineBridgeZ)) <= 8f;

    private static float RideGround(Vector3 at)
        => IsOnBridge(at) ? Act1ConnectedWorld.OpeningBridgeRoadHeight(at.X) : AgentBAct1HeightField.CollisionGround(at.X, at.Z);

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

            var route = Act1DemoRoot.RideRoute;
            var space = GetViewport().World3D.DirectSpaceState;
            var world = GetTree().Root.FindChild("Act1ConnectedWorld", true, false) as Node3D ?? throw new InvalidOperationException("no world");
            var props = world.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                .Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null)
                .Select(mesh => (Mesh: mesh, Box: mesh.GlobalTransform * mesh.GetAabb()))
                .Where(item => item.Box.Size.Y is > .6f and < 14f && item.Box.Size.X < 30f && item.Box.Size.Z < 30f
                    && !item.Mesh.Name.ToString().Contains("Terrain", StringComparison.OrdinalIgnoreCase)
                    && !item.Mesh.Name.ToString().Contains("Ground", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (OS.GetEnvironment("URMAN_ROUTE_SEARCH") is { Length: > 0 } search)
            {
                SearchDetour(space, props, search);
                GetTree().Quit(0);
                return;
            }

            var blocked = new Dictionary<string, Vector3>();
            var passedThrough = new Dictionary<string, Vector3>();
            var offRoad = 0; var worstOff = 0.0; var worstOffAt = Vector3.Zero;
            var steepest = 0f; var steepAt = Vector3.Zero;
            var distance = 0f; var samples = 0; var total = 0f;
            for (var i = 1; i < route.Count; i++) total += route[i - 1].DistanceTo(route[i]);
            for (var i = 1; i < route.Count; i++)
            {
                var a = route[i - 1]; var b = route[i];
                var length = a.DistanceTo(b);
                var heading = (b - a).Normalized();
                var yaw = Mathf.Atan2(-heading.X, -heading.Z);
                for (var step = 0f; step < length; step += 1f, samples++)
                {
                    var at = a + heading * step;
                    var ground = RideGround(at);
                    var onBridge = IsOnBridge(at);
                    var pose = new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)), new Vector3(at.X, ground + WheelClearance + BodyHeight / 2, at.Z));
                    var query = new PhysicsShapeQueryParameters3D
                    {
                        Shape = new BoxShape3D { Size = new Vector3(BodyWidth, BodyHeight, BodyLength) },
                        Transform = pose, CollisionMask = uint.MaxValue, CollideWithBodies = true, CollideWithAreas = false
                    };
                    foreach (var hit in space.IntersectShape(query, 16))
                    {
                        if (hit["collider"].AsGodotObject() is not Node node) continue;
                        var name = node.Name + " < " + node.GetParent()?.Name;
                        // The span itself and the ground it bridges are how the car crosses;
                        // the parked Niva and the arrival walker are hidden/absent during the ride.
                        if (onBridge && (node.Name == "RavineBridgeIntact" || node.Name == "AgentB_TerrainCollision")) continue;
                        if (node.Name.ToString() is "babay-niva" or "DriverDoor" or "AlsuNpc" or "AlsuPhysicalContact") continue;
                        blocked.TryAdd(name, at);
                    }
                    // Axis-aligned bound of the yawed box.
                    var halfExtents = new Vector3(BodyWidth, BodyHeight, BodyLength) * .5f;
                    var reach = new Vector3(
                        Mathf.Abs(pose.Basis.X.X) * halfExtents.X + Mathf.Abs(pose.Basis.Z.X) * halfExtents.Z, halfExtents.Y,
                        Mathf.Abs(pose.Basis.X.Z) * halfExtents.X + Mathf.Abs(pose.Basis.Z.Z) * halfExtents.Z);
                    var car = new Aabb(pose.Origin - reach, reach * 2f);
                    foreach (var (mesh, box) in props)
                        if (car.Intersects(box.Grow(-.15f)) && !blocked.ContainsKey(mesh.Name + " < " + mesh.GetParent()?.Name))
                            passedThrough.TryAdd(mesh.Name + " < " + mesh.GetParent()?.Name, at);
                    var (roadDistance, halfWidth) = AgentBAct1HeightField.RoadInfo(at.X, at.Z);
                    var away = roadDistance - halfWidth;
                    if (away > .4 && !(at.X > 45f && at.Z is > -30f and < 30f))
                    {
                        offRoad++;
                        if (away > worstOff) { worstOff = away; worstOffAt = at; }
                    }
                    var ahead = RideGround(at + heading * 2f);
                    var slope = Mathf.RadToDeg(Mathf.Atan2(Mathf.Abs(ahead - ground), 2f));
                    if (!onBridge && slope > steepest) { steepest = slope; steepAt = at; }
                    distance += 1f;
                }
            }

            GD.Print($"ride-route: {samples} samples over {total:0} m, {offRoad} off-road (worst {worstOff:0.0} m beyond the verge at {worstOffAt}), steepest {steepest:0.0} deg at {steepAt}");
            foreach (var (name, at) in blocked) GD.Print($"ride-route: BLOCKED by physics body {name} near ({at.X:0.0}, {at.Z:0.0})");
            foreach (var (name, at) in passedThrough) GD.Print($"ride-route: passes through visible prop without collision {name} near ({at.X:0.0}, {at.Z:0.0})");
            if (blocked.Count > 0) exit = 1;
            GD.Print(exit == 0 ? "ride-route: PASS (no physics blockers)" : "ride-route: FAIL");
        }
        catch (Exception error)
        {
            GD.PrintErr("ride-route: FAIL " + error);
            exit = 1;
        }
        GetTree().Quit(exit);
    }

    // Grid A* over the real colliders and props (1 m cells, car radius) between two points,
    // preferring the roads; prints a simplified polyline to paste into the ride path.
    // URMAN_ROUTE_SEARCH = "x0,z0,x1,z1,minX,maxX,minZ,maxZ".
    private static void SearchDetour(PhysicsDirectSpaceState3D space, (MeshInstance3D Mesh, Aabb Box)[] props, string spec)
    {
        var v = spec.Split(',').Select(part => float.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var (x0, z0, x1, z1, minX, maxX, minZ, maxZ) = (v[0], v[1], v[2], v[3], (int)v[4], (int)v[5], (int)v[6], (int)v[7]);
        var width = maxX - minX + 1; var depth = maxZ - minZ + 1;
        var small = props.Where(item => Mathf.Max(item.Box.Size.X, item.Box.Size.Z) < 8f).ToArray();
        var free = new bool[width, depth];
        var cylinder = new CylinderShape3D { Radius = 1.55f, Height = 1.3f };
        for (var ix = 0; ix < width; ix++)
        for (var iz = 0; iz < depth; iz++)
        {
            var x = minX + ix; var z = minZ + iz;
            var ground = AgentBAct1HeightField.CollisionGround(x, z);
            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = cylinder, Transform = new Transform3D(Basis.Identity, new Vector3(x, ground + .35f + .65f, z)),
                CollisionMask = uint.MaxValue, CollideWithBodies = true, CollideWithAreas = false
            };
            var clear = !space.IntersectShape(query, 8).Any(hit => hit["collider"].AsGodotObject() is Node node
                && node.Name.ToString() is not ("babay-niva" or "DriverDoor" or "AlsuNpc" or "AlsuPhysicalContact")
                && !node.Name.ToString().Contains("Terrain", StringComparison.Ordinal) && node.Name != "RavineBridgeIntact");
            if (clear)
            {
                var cell = new Aabb(new Vector3(x - 1.4f, ground + .35f, z - 1.4f), new Vector3(2.8f, 1.3f, 2.8f));
                clear = !small.Any(item => cell.Intersects(item.Box));
            }
            free[ix, iz] = clear;
        }

        // ASCII map, north (max Z) at the top: '.' free, '#' blocked.
        for (var iz = depth - 1; iz >= 0; iz--)
        {
            var row = new System.Text.StringBuilder();
            for (var ix = 0; ix < width; ix++) row.Append(free[ix, iz] ? '.' : '#');
            GD.Print($"ride-map z={minZ + iz,4} {row}");
        }

        (int, int) Cell(float x, float z) => ((int)Mathf.Round(x) - minX, (int)Mathf.Round(z) - minZ);
        double Cost(int ix, int iz)
        {
            var (distance, half) = AgentBAct1HeightField.RoadInfo(minX + ix, minZ + iz);
            return 1.0 + 3.0 * Math.Clamp(distance - half - .5, 0, 4);
        }
        var start = Cell(x0, z0); var goal = Cell(x1, z1);
        var best = new Dictionary<(int, int), double> { [start] = 0 };
        var from = new Dictionary<(int, int), (int, int)>();
        var open = new PriorityQueue<(int, int), double>();
        open.Enqueue(start, 0);
        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current == goal) break;
            for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                var next = (current.Item1 + dx, current.Item2 + dz);
                if (next.Item1 < 0 || next.Item2 < 0 || next.Item1 >= width || next.Item2 >= depth || !free[next.Item1, next.Item2]) continue;
                var cost = best[current] + Cost(next.Item1, next.Item2) * (dx != 0 && dz != 0 ? 1.414 : 1.0);
                if (best.TryGetValue(next, out var known) && known <= cost) continue;
                best[next] = cost; from[next] = current;
                open.Enqueue(next, cost + Math.Sqrt(Math.Pow(next.Item1 - goal.Item1, 2) + Math.Pow(next.Item2 - goal.Item2, 2)));
            }
        }
        if (!from.ContainsKey(goal)) { GD.Print("ride-route: SEARCH found no free path"); return; }
        var cells = new List<(int, int)> { goal };
        while (cells[^1] != start) cells.Add(from[cells[^1]]);
        cells.Reverse();
        // Line-of-sight simplification over free cells.
        bool Clear((int, int) a, (int, int) b)
        {
            var steps = Math.Max(Math.Abs(b.Item1 - a.Item1), Math.Abs(b.Item2 - a.Item2)) * 2;
            for (var i = 0; i <= steps; i++)
            {
                var t = steps == 0 ? 0f : i / (float)steps;
                var ix = (int)Mathf.Round(Mathf.Lerp(a.Item1, b.Item1, t)); var iz = (int)Mathf.Round(Mathf.Lerp(a.Item2, b.Item2, t));
                if (!free[ix, iz]) return false;
            }
            return true;
        }
        var simple = new List<(int, int)> { cells[0] };
        var anchor = 0;
        for (var i = 2; i < cells.Count; i++)
            if (!Clear(cells[anchor], cells[i])) { simple.Add(cells[i - 1]); anchor = i - 1; }
        simple.Add(cells[^1]);
        GD.Print("ride-route: SEARCH path " + string.Join(", ", simple.Select(cell => $"new({cell.Item1 + minX}f, 0, {cell.Item2 + minZ}f)")));
        GD.Print($"ride-route: SEARCH cost {best[goal]:0.0}, {cells.Count} cells, {simple.Count} corners");
    }
}
