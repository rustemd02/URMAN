using Godot;
using System.Text.Json;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Real physics for rotated/scaled wall contacts and approach openings, followed
/// by independent mesh/physics comparisons and ordinary controller movement at
/// live village facades. Local fixture positions are explicit, not a playthrough.
/// URMAN_BOUNDARY_ARCHITECTURE_OUTPUT optionally records the actual player view.
/// </summary>
public partial class Act1BoundaryArchitectureSmokeTest : Node
{
    private readonly List<object> _receipt = new();
    private readonly List<string> _failures = new();
    private readonly Dictionary<Mesh, Vector3[]> _faces = new();
    private readonly CapsuleShape3D _capsule = new() { Radius = .35f, Height = 1.8f };
    private string? _output;
    private float _walked;

    public override async void _Ready()
    {
        Main? main = null;
        try
        {
            _output = System.Environment.GetEnvironmentVariable("URMAN_BOUNDARY_ARCHITECTURE_OUTPUT");
            if (!string.IsNullOrEmpty(_output))
            {
                var repo = Path.GetFullPath(ProjectSettings.GlobalizePath("res://.."));
                if (!Path.IsPathFullyQualified(_output) || !Directory.Exists(_output)
                    || Path.GetFullPath(_output).StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || Directory.EnumerateFileSystemEntries(_output).Any()
                    || RenderingServer.GetRenderingDevice() is null)
                    throw new InvalidOperationException("Capture needs a real renderer and an existing empty directory outside the checkout.");
            }
            await CheckFixtures();
            main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
            main.InitialZoneId = "village_day";
            main.InitialSpawnPointId = "arrival";
            main.EnableAct1ConnectedWorld = true;
            AddChild(main);
            await Frames(24);
            var player = main.GetNode<FirstPersonController>("Player");
            player.SetModalOpen(false);
            await CheckLiveFacades(main.ConnectedWorld!, player);
            await CheckYardSupports(main.ConnectedWorld!, player);
            CheckMosqueContacts(main.ConnectedWorld!, player);
            if (player.EdgeClamps != 0 || player.FallRecoveries != 0)
                _failures.Add("A facade approach triggered emergency player recovery.");
        }
        catch (Exception error) { _failures.Add(error.ToString()); }
        finally
        {
            Input.ActionRelease("move_forward");
            if (!string.IsNullOrEmpty(_output) && Directory.Exists(_output))
                File.WriteAllText(Path.Combine(_output, "architecture-receipt.json"), JsonSerializer.Serialize(
                    new { checks = _receipt, failures = _failures, physicallyWalkedMeters = _walked, humanPlaytest = false },
                    new JsonSerializerOptions { WriteIndented = true }));
            _faces.Clear();
            if (main is not null) await GodotSmokeCleanup.ReleaseAsync(main);
            _capsule.Dispose();
        }
        foreach (var failure in _failures) GD.Print($"act1-boundary-architecture: FAIL {failure}");
        GD.Print($"act1-boundary-architecture: {(_failures.Count == 0 ? "PASS" : "FAIL")} {_receipt.Count} recorded checks; ordinary movement={_walked:F2}m; art/human gates remain open");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    private async Task CheckFixtures()
    {
        var cases = new[]
        {
            (Yaw: 0f, Scale: Vector3.One * .9f, InnerYaw: 0f),
            (Yaw: 45f, Scale: Vector3.One * .62f, InnerYaw: 0f),
            (Yaw: 90f, Scale: Vector3.One * 1.22f, InnerYaw: 0f),
            (Yaw: 110f, Scale: Vector3.One * .9f, InnerYaw: 0f),
            (Yaw: 176f, Scale: Vector3.One * .9f, InnerYaw: 0f),
            (Yaw: 90f, Scale: new Vector3(1.85f, .82f, 1f), InnerYaw: 0f),
            (Yaw: 30f, Scale: Vector3.One * .9f, InnerYaw: 20f)
        };
        foreach (var (yaw, scale, innerYaw) in cases)
        {
            var root = new Node3D { Name = "ArchitectureFixture", RotationDegrees = new(0, yaw, 0), Scale = scale };
            AddChild(root);
            try
            {
                var mesh = new MeshInstance3D
                {
                    Name = "FixtureWall", Mesh = new BoxMesh { Size = new(6, 3, .25f) },
                    Position = new(0, 1.5f, 0), RotationDegrees = new(0, innerYaw, 0)
                };
                root.AddChild(mesh);
                var label = $"yaw{yaw}-scale{scale}-child{innerYaw}";
                var (frame, bounds) = Act1ConnectedWorld.AuthoredKitMeshBounds(mesh);
                var side = mesh.GlobalBasis.X.Normalized();
                var normal = mesh.GlobalBasis.Z.Normalized();
                var centre = mesh.GlobalPosition;
                centre.Y = .9f;
                var halfWidth = 3f * mesh.GlobalBasis.X.Length();
                var halfGap = halfWidth / 5f;
                foreach (var doorway in new[] { false, true })
                {
                    var body = new StaticBody3D { Name = "FixturePhysics", CollisionLayer = 2, CollisionMask = 0 };
                    root.AddChild(body);
                    var targets = doorway ? new[] { root.GlobalPosition } : Array.Empty<Vector3>();
                    foreach (var shape in Act1ConnectedWorld.AuthoredKitMeshBlockers(root, mesh.Name, frame, bounds, targets, out _))
                        body.AddChild(shape);
                    await PhysicsFrames(2);
                    var space = root.GetWorld3D().DirectSpaceState;
                    if (!doorway)
                    {
                        foreach (var sign in new[] { -1f, 1f })
                        {
                            var surface = mesh.ToGlobal(new Vector3(sign * 2.5f, -.5f, .125f));
                            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(surface + normal, surface - normal, 2));
                            var error = hit.Count == 0 ? -1f : surface.DistanceTo(hit["position"].AsVector3());
                            Check(hit.Count > 0 && error < .055f, $"{label}: solid face edge {sign}, surface mismatch {error:F4}m");
                            _receipt.Add(new { kind = "fixture-face", label, sign, error });
                            Sweep(space, centre + side * (halfWidth + .50f) * sign + normal * 2,
                                -normal * 4, false, label + "-outside-wall-edge");
                            Sweep(space, centre + side * (halfWidth - .50f) * sign + normal * 2,
                                -normal * 4, true, label + "-inside-wall-edge");
                        }
                    }
                    else
                    {
                        Sweep(space, centre + normal * 2, -normal * 4, false, label + "-opening-centre");
                        foreach (var sign in new[] { -1f, 1f })
                        {
                            var along = side * sign;
                            var inside = centre + along * Math.Max(0, halfGap - .35f - .10f);
                            Sweep(space, inside + normal * 2 - along * .15f,
                                -normal * 4 + along * .30f, false, label + "-opening-inner-edge-diagonal");
                            Sweep(space, centre + along * (halfGap + .35f + .10f) + normal * 2,
                                -normal * 4, true, label + "-opening-jamb");
                        }
                    }
                    body.QueueFree();
                    await PhysicsFrames(2);
                }
            }
            finally { root.QueueFree(); await PhysicsFrames(2); }
        }
    }

    private void Sweep(PhysicsDirectSpaceState3D space, Vector3 centre, Vector3 motion, bool blocked, string label)
    {
        using var query = new PhysicsShapeQueryParameters3D
        { Shape = _capsule, Transform = new(Basis.Identity, centre), Motion = motion, CollisionMask = 2, Margin = .005f };
        var startClear = space.IntersectShape(query, 1).Count == 0;
        var fractions = space.CastMotion(query);
        var stopped = fractions.Length == 2 && fractions[0] < .99f;
        Check(startClear && stopped == blocked, $"{label}: capsule startClear={startClear}, blocked={stopped}, expected={blocked}");
        _receipt.Add(new { kind = "capsule-sweep", label, start = Point(centre), motion = Point(motion), startClear,
            safeFraction = fractions.Length == 2 ? fractions[0] : -1, stopped, expectedBlocked = blocked });
    }

    private async Task CheckLiveFacades(Act1ConnectedWorld world, FirstPersonController player)
    {
        foreach (var name in new[]
        {
            "ZiratEastBoundaryHouse", "ArrivalReverseFarCenterHouse",
            "ArrivalForwardWestFacade", "MainStreetForwardWestFacade",
            "BabaiYardAuthoredShed", "FapAuthoredServiceShed", "FapAuthoredFacade"
        })
        {
            var placement = world.FindChild(name, true, false) as Node3D
                ?? throw new InvalidOperationException($"Missing production placement {name}.");
            var meshes = Descendants(placement).OfType<MeshInstance3D>()
                .Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null
                    && (mesh.Name.ToString().Contains("_Wall", StringComparison.Ordinal)
                        || mesh.Name.ToString() is "FapServiceShed_Body_LOD0" or "FapFacade_Body_LOD0"))
                .OrderByDescending(mesh => mesh.Name.ToString().Contains("Street", StringComparison.Ordinal)).ToArray();
            (MeshInstance3D Mesh, Vector3 Point, Vector3 Normal)? physical = null;
            var measured = 0;
            foreach (var mesh in meshes)
            {
                var bounds = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                // Probe a world-axis grid independently of the production box
                // frame. Rendered triangles, not fitted boxes, own the expected
                // hit. Both ends of each face are represented, not only centre.
                foreach (var direction in new[] { Vector3.Right, Vector3.Left, Vector3.Forward, Vector3.Back })
                foreach (var fraction in new[] { -.42f, -.15f, .15f, .42f })
                {
                    var tangent = new Vector3(-direction.Z, 0, direction.X);
                    var width = Math.Abs(tangent.X) * bounds.Size.X + Math.Abs(tangent.Z) * bounds.Size.Z;
                    var middle = bounds.GetCenter() + tangent * width * fraction;
                    middle.Y = bounds.Position.Y + Math.Min(1.0f, bounds.Size.Y * .5f);
                    var extent = Math.Max(bounds.Size.X, bounds.Size.Z) * .5f + 2;
                    var from = middle + direction * extent;
                    var to = middle - direction * extent;
                    if (!RenderedHit(mesh, from, to, out var visible, out var normal)) continue;
                    using var ray = PhysicsRayQueryParameters3D.Create(from, to, 3);
                    ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
                    var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                    if (hit.Count > 0 && from.DistanceTo(hit["position"].AsVector3()) + .08f < from.DistanceTo(visible)) continue;
                    var error = hit.Count == 0 ? -1f : visible.DistanceTo(hit["position"].AsVector3());
                    // The authored blocker is recessed 3cm on each face. A
                    // grazing world-axis ray can miss a thin end completely or
                    // travel >8cm to that same 3cm-recessed plane. Measure the
                    // actual nearest-volume bound at the rendered triangle too,
                    // requiring this mesh's owner rather than a nearby wall.
                    using var contactSphere = new SphereShape3D { Radius = .055f };
                    using var contact = new PhysicsShapeQueryParameters3D
                    {
                        Shape = contactSphere, Transform = new(Basis.Identity, visible),
                        CollisionMask = 2, Margin = 0
                    };
                    var nearby = player.GetWorld3D().DirectSpaceState.IntersectShape(contact, 16);
                    var ownContact = nearby.Any(result =>
                    {
                        if (result["collider"].AsGodotObject() is not StaticBody3D body) return false;
                        var owner = body.ShapeFindOwner(result["shape"].AsInt32());
                        return body.ShapeOwnerGetOwner(owner) is Node shape && shape.HasMeta("authoredSourceMesh")
                            && shape.GetMeta("authoredSourceMesh").AsString() == mesh.GetPath().ToString();
                    });
                    var matches = ownContact;
                    measured++;
                    Check(matches, $"{name}/{mesh.Name}: visible wall and physics differ by {error:F3}m at {visible}.");
                    _receipt.Add(new { kind = "live-wall-ray", placement = name, mesh = mesh.GetPath().ToString(),
                        from = Point(from), to = Point(to), visible = Point(visible), normal = Point(normal),
                        rayErrorMeters = error, ownContactWithinMeters = .055f, matches });
                    if (physical is null && matches && Math.Abs(fraction) < .2f && Math.Abs(normal.Y) < .1f)
                        physical = (mesh, visible, new Vector3(normal.X, 0, normal.Z).Normalized());
                }
            }
            Check(measured >= 4, $"{name}: only {measured} independently exposed wall probes.");
            GD.Print($"act1-boundary-architecture: {name} live wall probes={measured}");
            if (physical is { } sample) await WalkFacade(player, name, sample.Mesh, sample.Point, sample.Normal);
            else _failures.Add($"{name}: no independently verified face for a physical approach.");
        }
    }

    private async Task WalkFacade(FirstPersonController player, string label, MeshInstance3D mesh, Vector3 point, Vector3 normal,
        bool alongFace = true)
    {
        var side = new Vector3(-normal.Z, 0, normal.X);
        var start = Ground(point + normal * 1.5f);
        player.ApplyZoneSpawn(start, 0);
        await PhysicsFrames(8);
        Aim(player, point);
        await Capture(player, label + "_approach");
        var contacted = false;
        Input.ActionPress("move_forward");
        try
        {
            for (var frame = 0; frame < 48; frame++)
            {
                Aim(player, point - normal * .3f);
                var previous = player.GlobalPosition;
                await PhysicsFrames(1);
                _walked += Horizontal(previous, player.GlobalPosition);
                for (var i = 0; i < player.GetSlideCollisionCount(); i++)
                {
                    var shape = player.GetSlideCollision(i).GetColliderShape() as Node;
                    if (shape?.HasMeta("authoredSourceMesh") == true
                        && shape.GetMeta("authoredSourceMesh").AsString() == mesh.GetPath().ToString()) contacted = true;
                }
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        var separation = (player.GlobalPosition - point).Dot(normal);
        Check(contacted && separation >= .30f && separation < .7f,
            $"{label}: ordinary controller wall contact={contacted}, separation={separation:F3}m.");
        _receipt.Add(new { kind = "controller-wall-contact", label, fixture = Point(start), end = Point(player.GlobalPosition), contacted, separation });
        await Capture(player, label + "_contact");
        // A person-sized path along the outside wall would intersect the old
        // inflated/rotated box. Move there and back using normal Input only.
        await Walk(player, Ground(point + normal * .85f), label + "-back-away");
        if (alongFace)
        {
            await Walk(player, Ground(point + normal * .85f + side * 1.1f), label + "-along-face");
            await Walk(player, Ground(point + normal * .85f - side * .7f), label + "-along-return");
        }
        await Walk(player, start, label + "-return");
        await Capture(player, label + "_return");
    }

    private void CheckMosqueContacts(Act1ConnectedWorld world, FirstPersonController player)
    {
        var mosque = world.GetNode<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex");
        foreach (var name in new[] { "MosqueEntranceDoor", "MosqueHallEastLeft", "MosqueHallWest" })
        {
            var mesh = mosque.GetNode<MeshInstance3D>(name);
            var bounds = mesh.GlobalTransform * mesh.Mesh.GetAabb();
            var direction = name == "MosqueHallWest" ? Vector3.Left : Vector3.Right;
            foreach (var height in new[] { .18f, .9f, 1.55f })
            {
                var middle = bounds.GetCenter();
                middle.Y = bounds.Position.Y + height;
                var from = middle + direction * 1.5f;
                var to = middle - direction * 1.5f;
                if (!RenderedHit(mesh, from, to, out var visible, out _))
                    throw new InvalidOperationException($"No rendered mosque face at {name}/{height}.");
                using var ray = PhysicsRayQueryParameters3D.Create(from, to, 2);
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                // An earlier, real obstacle can occlude the lower part of a
                // wall; do not mistake its contact for the wall's own support.
                if (hit.Count > 0 && from.DistanceTo(hit["position"].AsVector3()) + .06f < from.DistanceTo(visible)) continue;
                var error = hit.Count == 0 ? -1 : hit["position"].AsVector3().DistanceTo(visible);
                var matched = hit.Count > 0 && error < .055f
                    && hit["collider"].AsGodotObject() is Node body
                    && body.HasMeta("collisionOwner") && body.GetMeta("collisionOwner").AsString() == "mosque-blocker";
                Check(matched, $"{name}: rendered face has no matching physical support at {height:F2}m, error={error:F3}m.");
                _receipt.Add(new { kind = "mosque-rendered-contact", name, height, visible = Point(visible), error, matched });
            }
        }
    }

    private async Task CheckYardSupports(Act1ConnectedWorld world, FirstPersonController player)
    {
        var canopy = world.FindChild("BabaiFirewoodShelter", true, false) as Node3D
            ?? throw new InvalidOperationException("Missing Babai woodpile shelter.");
        var posts = canopy.GetChildren().OfType<MeshInstance3D>()
            .Where(mesh => mesh.Mesh is BoxMesh box && box.Size.Y > 1 && box.Size.X < .3f && box.Size.Z < .3f).ToArray();
        var poles = Descendants(world).OfType<MeshInstance3D>().Where(mesh => mesh.Name == "PoleShaft"
            && mesh.GetParent().Name.ToString().StartsWith("VillageUtilityPole", StringComparison.Ordinal)).ToArray();
        var woodpile = world.FindChild("BabaiYardAuthoredWoodpile", true, false)
            ?? throw new InvalidOperationException("Missing authored woodpile.");
        var logs = Descendants(woodpile).OfType<MeshInstance3D>()
            .Where(mesh => mesh.Name.ToString().StartsWith("Woodpile_Log_", StringComparison.Ordinal)
                || mesh.Name.ToString().StartsWith("Woodpile_Support", StringComparison.Ordinal)).ToArray();
        Check(posts.Length == 4 && logs.Length == 8,
            $"Authored support coverage changed: posts={posts.Length}, poles={poles.Length}, logs/beams={logs.Length}.");
        // The production connected-world path does not mount the old five-pole
        // framing kit. Record absence rather than claiming those poles passed.
        _receipt.Add(new { kind = "utility-pole-availability", count = poles.Length,
            status = poles.Length == 0 ? "not-mounted-in-current-connected-world" : "contacts-tested-below" });
        (MeshInstance3D Mesh, Vector3 Point, Vector3 Normal)? physicalPost = null;
        foreach (var mesh in posts.Concat(poles).Concat(logs))
        {
            var center = mesh.GlobalTransform * mesh.Mesh.GetAabb().GetCenter();
            if (mesh.Name == "PoleShaft")
            {
                var bottom = mesh.GlobalTransform * mesh.Mesh.GetAabb().Position;
                center.Y = bottom.Y + 1;
            }
            var checkedContact = 0;
            foreach (var direction in new[] { Vector3.Right, Vector3.Left, Vector3.Forward, Vector3.Back })
            {
                var from = center + direction * 1.6f;
                var to = center - direction * 1.6f;
                if (!RenderedHit(mesh, from, to, out var visible, out var normal)) continue;
                using var ray = PhysicsRayQueryParameters3D.Create(from, to, 3);
                ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                if (hit.Count > 0 && from.DistanceTo(hit["position"].AsVector3()) + .04f < from.DistanceTo(visible)) continue;
                var error = hit.Count == 0 ? -1 : hit["position"].AsVector3().DistanceTo(visible);
                var owned = hit.Count > 0 && hit["collider"].AsGodotObject() is StaticBody3D body
                    && body.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) is Node shape
                    && shape.HasMeta("authoredSourceMesh") && shape.GetMeta("authoredSourceMesh").AsString() == mesh.GetPath().ToString();
                checkedContact++;
                Check(owned && error < .045f, $"{mesh.GetPath()}: visible solid contact missing, error={error:F3}m.");
                _receipt.Add(new { kind = "yard-solid-ray", mesh = mesh.GetPath().ToString(), visible = Point(visible), error, owned });
                if (physicalPost is null && posts.Contains(mesh) && owned && error < .045f)
                {
                    var start = Ground(visible + normal * 1.5f);
                    using var capsule = new PhysicsShapeQueryParameters3D
                    {
                        Shape = _capsule, Transform = new(Basis.Identity, start + Vector3.Up * .9f), CollisionMask = 3,
                        Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() }, Margin = .002f
                    };
                    if (player.GetWorld3D().DirectSpaceState.IntersectShape(capsule, 1).Count == 0)
                        physicalPost = (mesh, visible, new Vector3(normal.X, 0, normal.Z).Normalized());
                }
            }
            // An enclosed lower log may be hidden behind other actual logs;
            // it is recorded as occluded, never as a contact pass.
            if (checkedContact == 0) _receipt.Add(new { kind = "occluded-yard-solid", mesh = mesh.GetPath().ToString() });
        }
        if (physicalPost is { } post)
            await WalkFacade(player, "BabaiFirewoodShelterPost", post.Mesh, post.Point, post.Normal, alongFace: false);
        else _failures.Add("No accessible shelter post could be approached from a clear person-sized fixture.");
        // A ray across the visible open space above the logs and below the roof
        // must stay clear. One box around the whole shelter would fail here.
        var gapFrom = canopy.ToGlobal(new(-.65f, 1.4f, 0));
        var gapTo = canopy.ToGlobal(new(.65f, 1.4f, 0));
        using var gapRay = PhysicsRayQueryParameters3D.Create(gapFrom, gapTo, 3);
        gapRay.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        var gap = player.GetWorld3D().DirectSpaceState.IntersectRay(gapRay);
        Check(gap.Count == 0, "The open space under Babai's shelter roof was filled by collision.");
        _receipt.Add(new { kind = "shelter-open-space", from = Point(gapFrom), to = Point(gapTo), clear = gap.Count == 0 });
    }

    private async Task Walk(FirstPersonController player, Vector3 target, string label)
    {
        Input.ActionPress("move_forward");
        try
        {
            for (var frame = 0; frame < 170; frame++)
            {
                if (Horizontal(player.GlobalPosition, target) < .18f) return;
                Aim(player, target);
                var previous = player.GlobalPosition;
                await PhysicsFrames(1);
                _walked += Horizontal(previous, player.GlobalPosition);
            }
            _failures.Add($"{label}: controller stalled at {player.GlobalPosition}, target={target}.");
        }
        finally { Input.ActionRelease("move_forward"); }
    }

    private bool RenderedHit(MeshInstance3D mesh, Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal)
    {
        if (!_faces.TryGetValue(mesh.Mesh, out var faces)) _faces[mesh.Mesh] = faces = mesh.Mesh.GetFaces();
        var localFrom = mesh.ToLocal(from);
        var localTo = mesh.ToLocal(to);
        point = default;
        normal = default;
        var closest = float.MaxValue;
        for (var index = 0; index + 2 < faces.Length; index += 3)
        {
            var result = Geometry3D.SegmentIntersectsTriangle(localFrom, localTo, faces[index], faces[index + 1], faces[index + 2]);
            if (result.VariantType == Variant.Type.Nil) continue;
            var hit = mesh.ToGlobal(result.AsVector3());
            var distance = from.DistanceSquaredTo(hit);
            if (distance >= closest) continue;
            closest = distance;
            point = hit;
            normal = (mesh.GlobalBasis.Inverse().Transposed()
                * (faces[index + 1] - faces[index]).Cross(faces[index + 2] - faces[index])).Normalized();
            if (normal.Dot(from - point) < 0) normal = -normal;
        }
        return closest < float.MaxValue;
    }

    private async Task Capture(FirstPersonController player, string label)
    {
        if (string.IsNullOrEmpty(_output)) return;
        await Frames(3);
        RenderingServer.ForceDraw(false);
        using var image = GetTree().Root.GetTexture().GetImage();
        if (image.SavePng(Path.Combine(_output, label + ".png")) != Error.Ok) throw new IOException($"Cannot capture {label}.");
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        _receipt.Add(new { kind = "camera", label, position = Point(camera.GlobalPosition), rotation = Point(camera.GlobalRotationDegrees),
            fieldOfView = camera.Fov, preset = player.GraphicsPreset, width = image.GetWidth(), height = image.GetHeight() });
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static Vector3 Ground(Vector3 point) => new(point.X, AgentBAct1HeightField.CollisionGround(point.X, point.Z) + .05f, point.Z);
    private static float Horizontal(Vector3 a, Vector3 b) => new Vector2(a.X, a.Z).DistanceTo(new(b.X, b.Z));
    private static void Aim(FirstPersonController player, Vector3 target) =>
        player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-(target.X - player.GlobalPosition.X), -(target.Z - player.GlobalPosition.Z))));
    private void Check(bool condition, string failure) { if (!condition) _failures.Add(failure); }
    private static object Point(Vector3 value) => new { x = value.X, y = value.Y, z = value.Z };
    private async Task PhysicsFrames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
}
