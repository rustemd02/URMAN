using Godot;
using System.Text.Json;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Narrow T1/T3 probes in the production connected world. Compares rendered
/// trunk intersections to real physics, then approaches and walks around a
/// reachable trunk with the ordinary controller. Local fixtures are explicitly
/// positioned; the subsequent movement is physical, not a full-route playtest.
/// Run with protected userdata. URMAN_BOUNDARY_OUTPUT optionally captures the
/// actual player camera into an existing empty evidence directory.
/// </summary>
public partial class Act1BoundaryCollisionSmokeTest : Node
{
    private readonly List<object> _measurements = new();
    private readonly List<string> _failures = new();
    private readonly Dictionary<Mesh, Vector3[]> _meshFaces = new();
    private string? _output;
    private float _walked;

    public override async void _Ready()
    {
        Main? main = null;
        try
        {
            _output = System.Environment.GetEnvironmentVariable("URMAN_BOUNDARY_OUTPUT");
            if (!string.IsNullOrEmpty(_output))
            {
                if (!Path.IsPathFullyQualified(_output) || !Directory.Exists(_output)
                    || Directory.EnumerateFileSystemEntries(_output).Any())
                    throw new InvalidOperationException("Boundary output must be an existing empty absolute directory.");
                if (RenderingServer.GetRenderingDevice() is null)
                    throw new InvalidOperationException("Boundary image evidence requires a real rendering device.");
            }

            main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
            main.InitialZoneId = "village_day";
            main.InitialSpawnPointId = "arrival";
            main.EnableAct1ConnectedWorld = true;
            AddChild(main);
            await Frames(24);
            var player = main.GetNode<FirstPersonController>("Player");
            player.SetModalOpen(false);
            var exterior = main.ConnectedWorld!.GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
            var plants = exterior.GetNode<Node3D>("AgentB_PlantedFoliage").GetChildren().OfType<Node3D>()
                .Where(node => node.HasMeta("plantVariant") && node.GetMeta("plantVariant").AsString().StartsWith("Winter", StringComparison.Ordinal)
                    && node.GetChildren().OfType<MeshInstance3D>().Any()).ToArray();

            var tested = 0;
            var missing = 0;
            var candidates = new List<Node3D>();
            foreach (var plant in plants)
            {
                var point = plant.GlobalPosition;
                // The boundary thicket owns the outer ring. This diagnostic is
                // for the independently approachable village-side trees.
                if (point.X < -58f || point.X > 58f || point.Z < -124f || point.Z > 64f
                    || InInteriorFootprint(point)) continue;
                var mesh = plant.GetChildren().OfType<MeshInstance3D>().First();
                if (mesh.Mesh.GetAabb().Size.Y * plant.Basis.Scale.Y < 3.5f) continue;
                foreach (var direction in new[] { Vector3.Right, Vector3.Back })
                {
                    var center = point + Vector3.Up * .75f;
                    var from = center - direction * 1.25f;
                    var to = center + direction * 1.25f;
                    if (!RenderedHit(mesh, from, to, out var visible)) continue;
                    var ray = PhysicsRayQueryParameters3D.Create(from, to, 3);
                    ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
                    var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                    var collider = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as Node;
                    var distance = hit.Count == 0 ? -1f : hit["position"].AsVector3().DistanceTo(visible);
                    // A nearer wall prevents this particular approach; it does
                    // not establish a pass or a failure for the hidden trunk.
                    if (hit.Count > 0 && from.DistanceTo(hit["position"].AsVector3()) + .08f < from.DistanceTo(visible)) continue;
                    tested++;
                    var matches = hit.Count > 0 && distance <= .08f;
                    if (!matches) missing++;
                    _measurements.Add(new { kind = "trunk-ray", plant = plant.Name.ToString(), root = Point(point),
                        from = Point(from), to = Point(to), visible = Point(visible), collider = collider?.GetPath().ToString(),
                        errorMeters = distance, matches });
                    if (!candidates.Contains(plant)) candidates.Add(plant);
                }
            }
            GD.Print($"act1-boundary: trunk rays={tested} missing-or-misaligned={missing}");
            if (tested == 0 || missing > 0) _failures.Add($"Trunk ray contacts {tested - missing}/{tested}.");

            var selected = candidates.OrderBy(node => new Vector2(node.GlobalPosition.X, node.GlobalPosition.Z)
                .DistanceSquaredTo(new Vector2(4.3f, -16f))).FirstOrDefault()
                ?? throw new InvalidOperationException("No village-side trunk available for a physical approach.");
            GD.Print($"act1-boundary: selected {selected.GetPath()} root={selected.GlobalPosition}");
            await CheckPhysicalTrunk(player, selected);
            // BuildDensifiedPlan's kitchen-garden pass owns the current willow
            // roots. The historical south-house coordinate no longer names a
            // planted tree. Select the unique north-yard planting by its source
            // metadata and parcel sector, never by a generated Plant index or
            // by whether its collider happens to exist.
            var willowSector = new Rect2(-27f, 5f, 3f, 2f);
            var yardWillows = plants.Where(node => node.GetMeta("plantVariant").AsString() == "WinterWillow_1"
                && node.HasMeta("plantPosition") && willowSector.HasPoint(new Vector2(
                    node.GetMeta("plantPosition").AsVector3().X,
                    node.GetMeta("plantPosition").AsVector3().Z))).ToArray();
            if (yardWillows.Length != 1)
                throw new InvalidOperationException("Expected one source-owned north-yard willow; found " + yardWillows.Length
                    + ". Live willows: " + string.Join("; ", plants
                        .Where(node => node.GetMeta("plantVariant").AsString() == "WinterWillow_1")
                        .Select(node => $"{node.Name}@{node.GlobalPosition}")));
            var willow = yardWillows[0];
            var planPosition = willow.GetMeta("plantPosition").AsVector3();
            if (willow.Position.DistanceTo(planPosition) > .001f)
                throw new InvalidOperationException($"Willow transform diverged from its planting owner: {willow.GetPath()} plan={planPosition} actual={willow.Position}.");
            _measurements.Add(new { kind = "willow-fixture-selection", plant = willow.GetPath().ToString(),
                variant = willow.GetMeta("plantVariant").AsString(), planPosition = Point(planPosition),
                root = Point(willow.GlobalPosition), sourceOwner = "AgentBAct1ExteriorLayer.BuildDensifiedPlan/kitchen-garden",
                selection = "unique WinterWillow_1 in north-yard source sector x[-27,-24), z[5,7)" });
            GD.Print($"act1-boundary: current willow {willow.GetPath()} plan={planPosition} root={willow.GlobalPosition}");
            // The current root is north of the yard fence. Start and return on
            // that same open side; circling through the fence is not an approach.
            await CheckPhysicalTrunk(player, willow,
                new[] { ("willow_north", Vector3.Back), ("willow_northeast", new Vector3(.35f, 0, 1).Normalized()) },
                checkCircuit: false);
            await CheckBoundary(player, exterior);
            GD.Print($"act1-boundary: physical-distance={_walked:F2}m edge-clamps={player.EdgeClamps} fall-recoveries={player.FallRecoveries}");
            if (player.EdgeClamps > 0 || player.FallRecoveries > 0)
                _failures.Add("A local approach used emergency world recovery.");
        }
        catch (Exception error)
        {
            _failures.Add(error.ToString());
        }
        finally
        {
            Input.ActionRelease("move_forward");
            if (!string.IsNullOrEmpty(_output) && Directory.Exists(_output))
                File.WriteAllText(Path.Combine(_output, "boundary-receipt.json"), JsonSerializer.Serialize(
                    new { kind = "act1-boundary-physics", measurements = _measurements, failures = _failures,
                        physicallyWalkedMeters = _walked, humanPlaytest = false }, new JsonSerializerOptions { WriteIndented = true }));
            if (main is not null) await GodotSmokeCleanup.ReleaseAsync(main);
        }
        foreach (var failure in _failures) GD.Print($"act1-boundary: FAIL {failure}");
        if (_failures.Count == 0) GD.Print("act1-boundary: PASS targeted trunk contacts, physical approaches and return; whole forest/art acceptance remains open");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private async Task CheckPhysicalTrunk(FirstPersonController player, Node3D plant,
        (string Label, Vector3 Radial)[]? approaches = null, bool checkCircuit = true)
    {
        var root = plant.GlobalPosition;
        // Local fixture position is recorded; all contact/bypass movement below
        // uses Input actions and production MoveAndSlide with gravity enabled.
        foreach (var (label, radial) in approaches
            ?? new[] { ("front", Vector3.Left), ("diagonal", new Vector3(-1, 0, 1).Normalized()) })
        {
            // Keep the fixture within the local pocket and verify its full
            // starting capsule against the current geometry before spawning.
            const float circuitRadius = 1.2f;
            var start = Ground(root + radial * circuitRadius);
            using var capsule = new CapsuleShape3D { Radius = .35f, Height = 1.78f };
            using var query = new PhysicsShapeQueryParameters3D
            {
                Shape = capsule, Transform = new(Basis.Identity, start + Vector3.Up * .9f),
                CollisionMask = 3, Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() }
            };
            if (player.GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count != 0)
            {
                _failures.Add($"{plant.Name}/{label}: fixture capsule overlaps an existing obstacle at {start}.");
                continue;
            }
            player.ApplyZoneSpawn(start, 0);
            await PhysicsFrames(8);
            Aim(player, root);
            await Capture(player, $"trunk_{label}_start");
            var minDistance = float.MaxValue;
            var contacted = false;
            Input.ActionPress("move_forward");
            try
            {
                for (var frame = 0; frame < 52; frame++)
                {
                    Aim(player, root);
                    var before = player.GlobalPosition;
                    await PhysicsFrames(1);
                    _walked += Horizontal(before, player.GlobalPosition);
                    minDistance = Mathf.Min(minDistance, Horizontal(player.GlobalPosition, root));
                    for (var index = 0; index < player.GetSlideCollisionCount(); index++)
                    {
                        var collision = player.GetSlideCollision(index);
                        if ((collision.GetColliderShape() as Node)?.Name.ToString() == $"PlantedStem_{plant.Name}"
                            && Mathf.Abs(collision.GetNormal().Y) < .6f && Horizontal(collision.GetPosition(), root) < .6f)
                            contacted = true;
                    }
                }
            }
            finally { Input.ActionRelease("move_forward"); }
            await Capture(player, $"trunk_{label}_contact");
            _measurements.Add(new { kind = "trunk-walk", label, root = Point(root), fixture = Point(start),
                end = Point(player.GlobalPosition), minDistance, contacted });
            if (!contacted || minDistance < .30f)
                _failures.Add($"{label}: passed into visible trunk (minimum root distance {minDistance:F3}m; contact={contacted}).");
            // Back away along the approach, then pass on both sides without
            // resetting the player. A collider that traps them must fail.
            await Walk(player, start, $"{label}-back-away");
            if (!checkCircuit) continue;
            var tangent = new Vector3(-radial.Z, 0, radial.X);
            foreach (var offset in new[] { radial + tangent, -radial + tangent, -radial - tangent, radial - tangent, radial })
                await Walk(player, Ground(root + offset * circuitRadius), $"{label}-bypass-return");
        }
    }

    private async Task CheckBoundary(FirstPersonController player, AgentBAct1ExteriorLayer exterior)
    {
        var architecture = exterior.GetNode<StaticBody3D>("AgentB_ArchitectureCollision");
        var boundary = architecture.GetChildren().OfType<CollisionShape3D>()
            .Where(shape => shape.Name.ToString().StartsWith("ForestBoundaryThicket_", StringComparison.Ordinal))
            .OrderBy(shape => new Vector2(shape.GlobalPosition.X, shape.GlobalPosition.Z).DistanceSquaredTo(new(62f, 0f)))
            .First();
        var start = Ground(boundary.GlobalPosition + Vector3.Left * 3f);
        player.ApplyZoneSpawn(start, -90f);
        await PhysicsFrames(8);
        await Capture(player, "east_boundary_start");
        Input.ActionPress("move_forward");
        var contacted = false;
        try
        {
            for (var frame = 0; frame < 65; frame++)
            {
                var before = player.GlobalPosition;
                await PhysicsFrames(1);
                _walked += Horizontal(before, player.GlobalPosition);
                for (var index = 0; index < player.GetSlideCollisionCount(); index++)
                    if ((player.GetSlideCollision(index).GetColliderShape() as Node)?.Name.ToString()
                        .StartsWith("ForestBoundaryThicket_", StringComparison.Ordinal) == true) contacted = true;
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        await Capture(player, "east_boundary_contact");
        _measurements.Add(new { kind = "boundary-walk", fixture = Point(start), end = Point(player.GlobalPosition), contacted });
        if (!contacted || player.GlobalPosition.X > boundary.GlobalPosition.X)
            _failures.Add("East boundary was not stopped by its physical thicket.");
        await Walk(player, start, "east-boundary-return");
    }

    private async Task Walk(FirstPersonController player, Vector3 target, string label)
    {
        var initial = Horizontal(player.GlobalPosition, target);
        Input.ActionPress("move_forward");
        try
        {
            for (var frame = 0; frame < Math.Max(90, initial / player.WalkSpeed * 60 * 2); frame++)
            {
                if (Horizontal(player.GlobalPosition, target) < .20f) return;
                Aim(player, target);
                var before = player.GlobalPosition;
                await PhysicsFrames(1);
                _walked += Horizontal(before, player.GlobalPosition);
            }
            _failures.Add($"{label}: physical return stalled at {player.GlobalPosition}, target={target}.");
        }
        finally { Input.ActionRelease("move_forward"); }
    }

    private async Task Capture(FirstPersonController player, string label)
    {
        if (string.IsNullOrEmpty(_output)) return;
        await Frames(3);
        RenderingServer.ForceDraw(false);
        using var image = GetTree().Root.GetTexture().GetImage();
        if (image.SavePng(Path.Combine(_output, label + ".png")) != Error.Ok)
            throw new InvalidOperationException($"Could not capture {label}.");
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        _measurements.Add(new { kind = "camera", label, position = Point(camera.GlobalPosition),
            rotation = Point(camera.GlobalRotationDegrees), fieldOfView = camera.Fov,
            width = image.GetWidth(), height = image.GetHeight(), preset = player.GraphicsPreset, zone = "village_day" });
    }

    private bool RenderedHit(MeshInstance3D mesh, Vector3 from, Vector3 to, out Vector3 hit)
    {
        var localFrom = mesh.ToLocal(from);
        var localTo = mesh.ToLocal(to);
        hit = default;
        var closest = float.MaxValue;
        if (!_meshFaces.TryGetValue(mesh.Mesh, out var faces))
        {
            // Needles and snow are visual dressing, not a reference surface for
            // a trunk collision. The semantic surface names keep
            // this comparison independent of how physics extracts rooted wood.
            var source = (ArrayMesh)mesh.Mesh;
            var bark = Enumerable.Range(0, source.GetSurfaceCount()).Single(surface => source.SurfaceGetName(surface) == "bark");
            var arrays = source.SurfaceGetArrays(bark);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            _meshFaces[mesh.Mesh] = faces = indices.Length == 0 ? vertices : indices.Select(index => vertices[index]).ToArray();
        }
        for (var index = 0; index + 2 < faces.Length; index += 3)
        {
            var result = Geometry3D.SegmentIntersectsTriangle(localFrom, localTo, faces[index], faces[index + 1], faces[index + 2]);
            if (result.VariantType == Variant.Type.Nil) continue;
            var point = mesh.ToGlobal(result.AsVector3());
            var distance = point.DistanceSquaredTo(from);
            if (distance >= closest) continue;
            closest = distance;
            hit = point;
        }
        return closest < float.MaxValue;
    }

    private static bool InInteriorFootprint(Vector3 point) =>
        Mathf.Abs(point.X + 28f) < 7f && Mathf.Abs(point.Z) < 6f
        || Mathf.Abs(point.X - 28f) < 6.5f && Mathf.Abs(point.Z + 30f) < 5.5f
        || Mathf.Abs(point.X - 6.5f) < 9.1f && Mathf.Abs(point.Z + 70f) < 7.7f;

    private static Vector3 Ground(Vector3 point) => new(point.X, AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .05f, point.Z);
    private static object Point(Vector3 point) => new { x = point.X, y = point.Y, z = point.Z };
    private static float Horizontal(Vector3 a, Vector3 b) => new Vector2(a.X, a.Z).DistanceTo(new(b.X, b.Z));
    private static void Aim(FirstPersonController player, Vector3 point) =>
        player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-(point.X - player.GlobalPosition.X), -(point.Z - player.GlobalPosition.Z))));
    private async Task PhysicsFrames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
}
