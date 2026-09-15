using Godot;
using System.Text.Json;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Reproduces the zirat kit's floating culvert/shoulder contacts against real
/// terrain physics, then walks the separate usable footbridge in both directions.
/// Run only through eng/run-smoke-guarded.sh. Optional screenshots require an
/// existing empty directory outside the checkout in URMAN_ROADSIDE_OUTPUT.
/// </summary>
public partial class Act1RoadsideGeometrySmokeTest : Node
{
    private readonly List<object> _checks = new();
    private readonly List<string> _failures = new();
    private string? _output;
    private float _walked;

    public override async void _Ready()
    {
        Main? main = null;
        try
        {
            _output = System.Environment.GetEnvironmentVariable("URMAN_ROADSIDE_OUTPUT");
            if (!string.IsNullOrEmpty(_output))
            {
                var repo = Path.GetFullPath(ProjectSettings.GlobalizePath("res://.."));
                if (!Path.IsPathFullyQualified(_output) || !Directory.Exists(_output)
                    || Path.GetFullPath(_output).StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || Directory.EnumerateFileSystemEntries(_output).Any()
                    || RenderingServer.GetRenderingDevice() is null)
                    throw new InvalidOperationException("Roadside capture needs a real renderer and an empty directory outside the checkout.");
            }
            main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
            main.InitialZoneId = "village_day";
            main.InitialSpawnPointId = "arrival";
            main.EnableAct1ConnectedWorld = true;
            AddChild(main);
            await Frames(24);
            var player = main.GetNode<FirstPersonController>("Player");
            player.SetModalOpen(false);
            var world = main.ConnectedWorld!;
            var kit = world.GetNode<Node3D>("Act1CoreWorldGreybox/ZiratMemoryField/ZiratRoadsideAuthoredKitPresentation");
            foreach (var name in new[] { "ZiratCulvertStoneCluster", "ZiratWetRoadShoulderLeft", "ZiratWetRoadShoulderRight", "ZiratRoadsideDitch" })
                CheckContacts(kit.GetNode<Node3D>(name), player);

            // These camera fixtures are identical before/after. The subsequent
            // bridge route uses ordinary movement and gravity, not teleportation.
            await CaptureAt(player, "zirat_stone_culvert", Ground(new(1.7f, 0, -71f)), new(4.8f, -.1f, -72f));
            await CaptureAt(player, "zirat_roadside_shoulder", Ground(new(.5f, 0, -65.7f)), new(3.8f, -.2f, -65f));
            await CaptureAt(player, "zirat_outer_footbridge", Ground(new(1.3f, 0, -67f)), new(3.65f, .2f, -67f));
            player.ApplyZoneSpawn(Ground(new(1.25f, 0, -67f)), -90);
            await PhysicsFrames(8);
            await Walk(player, new(6.1f, 0, -67f), "bridge-west-to-east");
            await Walk(player, new(1.25f, 0, -67f), "bridge-east-to-west");
            Check(player.EdgeClamps == 0 && player.FallRecoveries == 0,
                "The bridge used emergency edge/fall recovery.");
            _checks.Add(new { kind = "recovery", edgeClamps = player.EdgeClamps, fallRecoveries = player.FallRecoveries });
        }
        catch (Exception error) { _failures.Add(error.ToString()); }
        finally
        {
            Input.ActionRelease("move_forward");
            if (!string.IsNullOrEmpty(_output) && Directory.Exists(_output))
                File.WriteAllText(Path.Combine(_output, "roadside-geometry-receipt.json"), JsonSerializer.Serialize(
                    new { checks = _checks, failures = _failures, physicallyWalkedMeters = _walked, humanPlaytest = false },
                    new JsonSerializerOptions { WriteIndented = true }));
            if (main is not null) await GodotSmokeCleanup.ReleaseAsync(main);
        }
        foreach (var failure in _failures) GD.Print($"act1-roadside-geometry: FAIL {failure}");
        GD.Print($"act1-roadside-geometry: {(_failures.Count == 0 ? "PASS" : "FAIL")} {_checks.Count} checks; ordinary bridge movement={_walked:F2}m");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private void CheckContacts(Node3D placement, FirstPersonController player)
    {
        var count = 0;
        foreach (var mesh in Descendants(placement).OfType<MeshInstance3D>().Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()))
        {
            var vertices = mesh.Mesh.GetFaces().Distinct().ToArray();
            // Local component up is not assumed: imported meshes retain their
            // GLB conversion basis. The lowest authored ring owns support.
            var worldVertices = vertices.Select(mesh.ToGlobal).ToArray();
            var bottom = worldVertices.Min(point => point.Y);
            var baseVertices = worldVertices.Where(point => point.Y <= bottom + .006f).ToArray();
            var gaps = new List<float>();
            var worst = Vector3.Zero;
            foreach (var point in baseVertices)
            {
                using var ray = PhysicsRayQueryParameters3D.Create(point + Vector3.Up * 3, point - Vector3.Up * 6, 1);
                ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                if (hit.Count == 0 || hit["collider"].AsGodotObject() is not Node collider
                    || !collider.HasMeta("collisionOwner") || collider.GetMeta("collisionOwner").AsString() != "act1-exterior-terrain")
                {
                    _failures.Add($"{mesh.GetPath()}: no terrain contact below {point}.");
                    continue;
                }
                var gap = point.Y - hit["position"].AsVector3().Y;
                if (gaps.Count == 0 || gap > gaps.Max()) worst = point;
                gaps.Add(gap);
            }
            count++;
            Check(gaps.Count >= 2 && gaps.Max() <= .045f && gaps.Min() >= -.18f,
                $"{mesh.GetPath()}: base-to-terrain gaps [{(gaps.Count > 0 ? gaps.Min() : 999):F3}, {(gaps.Count > 0 ? gaps.Max() : 999):F3}]m at {worst}.");
            _checks.Add(new { kind = "rendered-support-to-terrain-ray", placement = placement.Name.ToString(), mesh = mesh.GetPath().ToString(),
                root = Point(mesh.GlobalPosition), samples = gaps.Count, minimumGap = gaps.Count > 0 ? gaps.Min() : 999,
                maximumGap = gaps.Count > 0 ? gaps.Max() : 999, worstPoint = Point(worst) });
        }
        Check(count > 0, $"{placement.Name}: no visible support meshes were tested.");
        GD.Print($"act1-roadside-geometry: {placement.Name} support meshes={count}");
    }

    private async Task Walk(FirstPersonController player, Vector3 target, string label)
    {
        var start = player.GlobalPosition;
        var reached = false;
        Input.ActionPress("move_forward");
        try
        {
            for (var frame = 0; frame < 240; frame++)
            {
                if (Horizontal(player.GlobalPosition, target) < .18f) { reached = true; break; }
                Aim(player, target, false);
                var before = player.GlobalPosition;
                await PhysicsFrames(1);
                _walked += Horizontal(before, player.GlobalPosition);
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        Check(reached, $"{label}: controller stalled at {player.GlobalPosition}, target={target}.");
        _checks.Add(new { kind = "physical-bridge-route", label, start = Point(start), end = Point(player.GlobalPosition), target = Point(target), reached });
    }

    private async Task CaptureAt(FirstPersonController player, string name, Vector3 position, Vector3 target)
    {
        if (string.IsNullOrEmpty(_output)) return;
        player.ApplyZoneSpawn(position, 0);
        await PhysicsFrames(8);
        Aim(player, target, true);
        await Frames(3);
        RenderingServer.ForceDraw(false);
        using var image = GetTree().Root.GetTexture().GetImage();
        if (image.SavePng(Path.Combine(_output, name + ".png")) != Error.Ok) throw new IOException($"Cannot capture {name}.");
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        _checks.Add(new { kind = "camera", name, position = Point(camera.GlobalPosition), rotation = Point(camera.GlobalRotationDegrees),
            fieldOfView = camera.Fov, width = image.GetWidth(), height = image.GetHeight(), preset = player.GraphicsPreset });
    }

    private static void Aim(FirstPersonController player, Vector3 target, bool pitch)
    {
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var delta = target - camera.GlobalPosition;
        player.ApplySmokeLook(pitch ? Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length())) : 0,
            Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
    }
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static Vector3 Ground(Vector3 point) => new(point.X, AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .05f, point.Z);
    private static float Horizontal(Vector3 a, Vector3 b) => new Vector2(a.X, a.Z).DistanceTo(new(b.X, b.Z));
    private static object Point(Vector3 point) => new { x = point.X, y = point.Y, z = point.Z };
    private void Check(bool condition, string message) { if (!condition) _failures.Add(message); }
    private async Task PhysicsFrames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
}
