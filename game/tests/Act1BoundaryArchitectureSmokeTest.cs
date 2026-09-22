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
    private string _scope = "full";
    private float _walked;
    private Vector3[]? _publishedTerrainRaw;
    private Vector3[]? _publishedTerrainPhysics;

    public override async void _Ready()
    {
        Main? main = null;
        try
        {
            _output = System.Environment.GetEnvironmentVariable("URMAN_BOUNDARY_ARCHITECTURE_OUTPUT");
            var scope = System.Environment.GetEnvironmentVariable("URMAN_BOUNDARY_ARCHITECTURE_SCOPE");
            _scope = string.IsNullOrEmpty(scope) ? "full" : scope;
            if (_scope is not ("full" or "clinic" or "house" or "yard" or "fap-shed" or "fap-path"))
                throw new InvalidOperationException("Architecture scope must be full, clinic, house, yard, fap-shed or fap-path.");
            if (!string.IsNullOrEmpty(_output))
            {
                if (!Path.IsPathFullyQualified(_output) || !Directory.Exists(_output)
                    || Directory.EnumerateFileSystemEntries(_output).Any()
                    || RenderingServer.GetRenderingDevice() is null)
                    throw new InvalidOperationException("Capture needs a real renderer and an existing empty absolute directory.");
            }
            if (_scope == "full") await CheckFixtures();
            main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
            main.InitialZoneId = "village_day";
            main.InitialSpawnPointId = "arrival";
            main.EnableAct1ConnectedWorld = true;
            AddChild(main);
            await Frames(24);
            if (main.ConnectedWorld is null || !main.ConnectedWorld.IsBuilt)
                throw new InvalidOperationException("Connected world build did not complete; geometry checks require a fully built live scene.");
            var player = main.GetNode<FirstPersonController>("Player");
            player.SetModalOpen(false);
            if (_scope is "full" or "house") CheckPublishedTerrainBeforeMovement(main.ConnectedWorld!);
            if (_scope == "full")
            {
                await CheckLiveFacades(main.ConnectedWorld!, player);
                await CheckYardSupports(main.ConnectedWorld!, player);
                CheckWellContacts(main.ConnectedWorld!, player);
                CheckMosqueContacts(main.ConnectedWorld!, player);
                CheckFoundationSupports(main.ConnectedWorld!, player);
                await CheckSmallSpaceGeometry(main.ConnectedWorld!, player);
            }
            if (_scope is "full" or "fap-path") await CheckFapPathContacts(main.ConnectedWorld!, player);
            if (_scope == "yard") await CheckSmallSpaceGeometry(main.ConnectedWorld!, player);
            if (_scope == "fap-shed") await CheckLiveFacades(main.ConnectedWorld!, player, "FapAuthoredServiceShed");
            if (_scope is "full" or "clinic")
            {
                await CheckFapEnvelope(main.ConnectedWorld!, player);
                await CheckFapServiceCabinet(main.ConnectedWorld!, player);
                await CheckFapOpticalViews(main.ConnectedWorld!, player);
            }
            if (_scope is "full" or "house")
            {
                if (_scope == "full") await CheckYardHouseRefit(main.ConnectedWorld!, player);
                await CheckHeroHouse(main.ConnectedWorld!, player);
            }
            if (player.EdgeClamps != 0 || player.FallRecoveries != 0)
                _failures.Add("A facade approach triggered emergency player recovery.");
        }
        catch (Exception error) { _failures.Add(error.ToString()); }
        finally
        {
            Input.ActionRelease("move_forward");
            Input.ActionRelease("crouch");
            // A receipt failure must not hide the actual checks or leave this
            // async smoke running forever without cleanup and its exit code.
            foreach (var failure in _failures) GD.Print($"act1-boundary-architecture: FAIL {failure}");
            try
            {
                if (!string.IsNullOrEmpty(_output) && Directory.Exists(_output))
                    File.WriteAllText(Path.Combine(_output, "architecture-receipt.json"), JsonSerializer.Serialize(
                        new { scope = _scope, checks = _receipt, failures = _failures,
                            physicallyWalkedMeters = _walked, humanPlaytest = false },
                        new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error)
            {
                var failure = "Could not write architecture receipt: " + error;
                _failures.Add(failure);
                GD.Print($"act1-boundary-architecture: FAIL {failure}");
            }
            _faces.Clear();
            try { if (main is not null) await GodotSmokeCleanup.ReleaseAsync(main); }
            catch (Exception error)
            {
                var failure = "Could not release architecture smoke scene: " + error;
                _failures.Add(failure);
                GD.Print($"act1-boundary-architecture: FAIL {failure}");
            }
            _capsule.Dispose();
        }
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

    private async Task CheckLiveFacades(Act1ConnectedWorld world, FirstPersonController player, string? onlyPlacement = null)
    {
        foreach (var name in new[]
        {
            "ZiratEastBoundaryHouse", "ArrivalReverseFarCenterHouse", "EastReturnMidFacade",
            "ArrivalForwardWestFacade", "MainStreetForwardWestFacade",
            "BabaiYardAuthoredShed", "FapAuthoredServiceShed", "FapAuthoredFacade"
        })
        {
            if (onlyPlacement is not null && name != onlyPlacement) continue;
            var placement = world.FindChild(name, true, false) as Node3D
                ?? throw new InvalidOperationException($"Missing production placement {name}.");
            var meshes = Descendants(placement).OfType<MeshInstance3D>()
                .Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null
                    && (mesh.Name.ToString().Contains("_Wall", StringComparison.Ordinal)
                        || mesh.Name.ToString() is "FapServiceShed_Body_LOD0" or "FapFacade_Body_LOD0"))
                .OrderByDescending(mesh => mesh.Name.ToString().Contains("Street", StringComparison.Ordinal)).ToArray();
            (MeshInstance3D Mesh, Vector3 Point, Vector3 Normal, Vector3[] Approach)? physical = null;
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
                        CollisionMask = 3, Margin = 0
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
                    {
                        var outward = new Vector3(normal.X, 0, normal.Z).Normalized();
                        if (FacadeApproachIsOpen(world, player, placement, meshes, name, visible, outward, out var approach))
                            physical = (mesh, visible, outward, approach);
                    }
                }
            }
            Check(measured >= 4, $"{name}: only {measured} independently exposed wall probes.");
            GD.Print($"act1-boundary-architecture: {name} live wall probes={measured}");
            if (physical is { } sample) await WalkFacade(player, name, sample.Mesh, sample.Point, sample.Normal,
                placement: placement, approach: sample.Approach);
            else _failures.Add($"{name}: no independently verified face for a physical approach.");
        }
    }

    private bool FacadeApproachIsOpen(Act1ConnectedWorld world, FirstPersonController player,
        Node3D placement, MeshInstance3D[] walls, string name, Vector3 wall, Vector3 normal, out Vector3[] approach)
    {
        approach = Array.Empty<Vector3>();
        var localWalls = walls.Select(mesh => (placement.GlobalTransform.AffineInverse() * mesh.GlobalTransform) * mesh.Mesh.GetAabb()).ToArray();
        var envelope = localWalls[0];
        foreach (var bounds in localWalls.Skip(1)) envelope = envelope.Merge(bounds);
        var centre = placement.ToGlobal(envelope.GetCenter());
        // RenderedHit orients a triangle toward its ray origin. That origin can
        // lie inside a hollow house when it was derived from one wall's AABB.
        // Require a genuinely outward face and a start beyond the whole shell.
        if (normal.Dot(wall - centre) < .05f) return false;
        var start = Ground(wall + normal * 1.5f);
        var localStart = placement.ToLocal(start);
        if (localStart.X > envelope.Position.X - .1f && localStart.X < envelope.End.X + .1f
            && localStart.Z > envelope.Position.Z - .1f && localStart.Z < envelope.End.Z + .1f) return false;

        var side = new Vector3(-normal.Z, 0, normal.X);
        var route = new[] { start, wall + normal * .85f,
            wall + normal * .85f + side * 1.1f, wall + normal * .85f - side * .7f };
        var exclusions = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        foreach (var body in Descendants(world).OfType<StaticBody3D>().Where(body => body.HasMeta("collisionOwner")
                     && body.GetMeta("collisionOwner").AsString() == "act1-exterior-terrain"))
            exclusions.Add(body.GetRid());
        using var capsule = new CapsuleShape3D { Radius = .35f, Height = 1.785f };
        using var query = new PhysicsShapeQueryParameters3D { Shape = capsule, CollisionMask = 3,
            Margin = .002f, Exclude = exclusions };
        bool ClearSegment(Vector3 a, Vector3 b)
        {
            var steps = Math.Max(1, Mathf.CeilToInt(Horizontal(a, b) / .15f));
            for (var i = 0; i <= steps; i++)
            {
                var feet = Ground(a.Lerp(b, i / (float)steps));
                query.Transform = new(Basis.Identity, feet + Vector3.Up * .91f);
                var hits = player.GetWorld3D().DirectSpaceState.IntersectShape(query, 1);
                if (hits.Count != 0) return false;
            }
            return true;
        }
        if (Enumerable.Range(0, route.Length - 1).Any(i => !ClearSegment(route[i], route[i + 1]))) return false;

        // Small diagnostic search for a short approach from the existing road.
        // All selected segments are subsequently walked with normal input. A
        // free pocket behind a fence or inside a house is no longer accepted as
        // a fixture merely because the capsule fits at its destination.
        var roads = new List<Vector3>();
        for (var x = Mathf.FloorToInt(start.X) - 30; x <= Mathf.CeilToInt(start.X) + 30; x++)
        for (var z = Mathf.FloorToInt(start.Z) - 30; z <= Mathf.CeilToInt(start.Z) + 30; z++)
        {
            var info = AgentBAct1HeightField.RoadInfo(x, z);
            if (info.Distance > .35f) continue;
            var at = Ground(new(x, 0, z));
            if (normal.Dot(at - start) < -.25f) continue;
            roads.Add(at);
        }
        var candidates = new List<(Vector3[] Points, string Source)>();
        foreach (var road in roads.OrderBy(at => Horizontal(at, start)).Take(20))
        foreach (var candidate in new[]
        {
            new[] { road, start },
            new[] { road, Ground(new(road.X, 0, start.Z)), start },
            new[] { road, Ground(new(start.X, 0, road.Z)), start }
        })
            candidates.Add((candidate, "road-straight-or-one-corner"));
        // The closed FAP service parcel deliberately needs its east-side U
        // approach. A direct or one-corner search cannot pass the north/west
        // fence. Read the same path that places the visible service boards;
        // every segment still has to fit and be walked in the ordinary state.
        if (name == "FapAuthoredServiceShed"
            && world.FindChild("FapServiceExplorationPresentation", true, false) is Node3D service
            && service.HasMeta("loopWorldPoints"))
        {
            var loop = service.GetMeta("loopWorldPoints").AsVector3Array().Select(Ground).ToArray();
            foreach (var ordered in new[] { loop, loop.Reverse().ToArray() })
            {
                if (ordered.Length < 2 || AgentBAct1HeightField.RoadInfo(ordered[0].X, ordered[0].Z).Distance > .35f) continue;
                for (var count = 2; count < ordered.Length; count++)
                    candidates.Add((ordered.Take(count).Append(start).ToArray(), "authored-fap-service-loop"));
            }
        }
        foreach (var (candidate, routeSource) in candidates)
        {
            if (Enumerable.Range(0, candidate.Length - 1).Any(i => !ClearSegment(candidate[i], candidate[i + 1]))) continue;
            approach = candidate;
            var road = candidate[0];
            _receipt.Add(new { kind = "facade-exterior-road-approach", placement = name,
                wall = Point(wall), outward = Point(normal), shellCentre = Point(centre),
                exteriorStart = Point(start), roadDistance = AgentBAct1HeightField.RoadInfo(road.X, road.Z).Distance,
                polyline = candidate.Select(Point).ToArray(), routeSource,
                proof = "collision-sampled plan; ordinary input execution follows" });
            return true;
        }
        return false;
    }

    private async Task WalkFacade(FirstPersonController player, string label, MeshInstance3D mesh, Vector3 point, Vector3 normal,
        bool alongFace = true, Node3D? placement = null, IReadOnlyList<Vector3>? approach = null)
    {
        var side = new Vector3(-normal.Z, 0, normal.X);
        var start = Ground(point + normal * 1.5f);
        player.ApplyZoneSpawn(approach is { Count: > 0 } ? approach[0] : start, 0);
        player.SetModalOpen(false);
        await PhysicsFrames(8);
        if (approach is { Count: > 1 })
        {
            for (var i = 0; i < approach.Count - 1; i++)
            {
                var parts = Math.Max(1, Mathf.CeilToInt(Horizontal(approach[i], approach[i + 1]) / 2.5f));
                for (var part = 1; part <= parts; part++)
                    await Walk(player, Ground(approach[i].Lerp(approach[i + 1], part / (float)parts)), label + "-from-road");
            }
            var arrived = Horizontal(player.GlobalPosition, start) < .25f;
            Check(arrived, $"{label}: the exterior wall fixture was not reached by ordinary walking from the road.");
            _receipt.Add(new { kind = "facade-exterior-approach-executed", label,
                roadStart = Point(approach[0]), actual = Point(player.GlobalPosition), destination = Point(start), arrived });
            if (!arrived) return;
        }
        Aim(player, point);
        await Capture(player, label + "_approach");
        var contacted = false;
        var contactForwardExtent = 0f;
        var finalMotionSamples = new List<object>();
        var recordedShapes = new HashSet<string>(StringComparer.Ordinal);
        Input.ActionPress("move_forward");
        try
        {
            // At the saved walking speed, 48 ticks stopped two centimetres
            // before one wall. Give the controller time to reach real contact.
            for (var frame = 0; frame < 96; frame++)
            {
                Aim(player, point - normal * .3f);
                var previous = player.GlobalPosition;
                await PhysicsFrames(1);
                _walked += Horizontal(previous, player.GlobalPosition);
                if (frame >= 88) finalMotionSamples.Add(new { frame, previous = Point(previous),
                    actual = Point(player.GlobalPosition - previous), velocity = Point(player.Velocity),
                    floor = player.IsOnFloor(), wall = player.IsOnWall(), crouched = player.IsCrouching });
                for (var i = 0; i < player.GetSlideCollisionCount(); i++)
                {
                    var collision = player.GetSlideCollision(i);
                    var shape = collision.GetColliderShape() as Node;
                    var sourcePath = shape?.HasMeta("authoredSourceMesh") == true
                        ? shape.GetMeta("authoredSourceMesh").AsString() : string.Empty;
                    var source = string.IsNullOrEmpty(sourcePath) ? null : GetNodeOrNull<MeshInstance3D>(sourcePath);
                    var contactPoint = collision.GetPosition();
                    var contactNormal = collision.GetNormal();
                    var sourceIsVisible = source is not null && source.IsVisibleInTree();
                    var visibleError = -1f;
                    if (sourceIsVisible && RenderedHit(source!, contactPoint + contactNormal * .15f,
                            contactPoint - contactNormal * .15f, out var rendered, out _))
                        visibleError = rendered.DistanceTo(contactPoint);
                    // A plinth, door leaf or corner board can meet the capsule
                    // before this wall. Accept only a contact independently
                    // verified against that visible member of this same house.
                    var belongsToFace = source == mesh || source is not null && placement?.IsAncestorOf(source) == true;
                    if (belongsToFace && sourceIsVisible && visibleError is >= 0 and < .055f && contactNormal.Dot(normal) > .4f)
                    {
                        contacted = true;
                        contactForwardExtent = Math.Max(contactForwardExtent, (contactPoint - point).Dot(normal));
                    }
                    var shapePath = shape?.GetPath().ToString() ?? "no-node-shape";
                    if (recordedShapes.Add(shapePath))
                        _receipt.Add(new { kind = "controller-slide-contact", label, shape = shapePath,
                            body = (collision.GetCollider() as Node)?.GetPath().ToString(), source = sourcePath,
                            sourceIsVisible, visibleError, belongsToFace, point = Point(contactPoint), normal = Point(contactNormal) });
                }
            }
        }
        finally { Input.ActionRelease("move_forward"); }
        var separation = (player.GlobalPosition - point).Dot(normal);
        if (!contacted)
        {
            foreach (var recovery in new[] { false, true })
            {
                using var collision = new KinematicCollision3D();
                var blocked = player.TestMove(player.GlobalTransform, -normal * .20f, collision, .001f, recovery, 8);
                var contacts = Enumerable.Range(0, collision.GetCollisionCount()).Select(index =>
                {
                    var shape = collision.GetColliderShape(index) as Node;
                    return new { body = (collision.GetCollider(index) as Node)?.GetPath().ToString(),
                        shape = shape?.GetPath().ToString(),
                        source = shape?.HasMeta("authoredSourceMesh") == true ? shape.GetMeta("authoredSourceMesh").AsString() : null,
                        point = Point(collision.GetPosition(index)), normal = Point(collision.GetNormal(index)) };
                }).ToArray();
                _receipt.Add(new { kind = "wall-stop-diagnostic", label, recovery, blocked, contacts,
                    requestedMotion = Point(-normal * .20f), finalMotionSamples,
                    heldItem = (GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator)?.HeldItem?.ItemId });
                GD.Print($"act1-wall-stop-diagnostic: {label}: recovery={recovery}; blocked={blocked}; {JsonSerializer.Serialize(contacts)}");
            }
        }
        Check(contacted && separation >= .30f && separation < Math.Max(.7f, contactForwardExtent + .7f),
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
        if (approach is { Count: > 1 })
        {
            for (var i = approach.Count - 1; i > 0; i--)
            {
                var parts = Math.Max(1, Mathf.CeilToInt(Horizontal(approach[i], approach[i - 1]) / 2.5f));
                for (var part = 1; part <= parts; part++)
                    await Walk(player, Ground(approach[i].Lerp(approach[i - 1], part / (float)parts)), label + "-return-to-road");
            }
            var returned = Horizontal(player.GlobalPosition, approach[0]) < .25f;
            Check(returned, $"{label}: the ordinary return from the facade to the road failed.");
            _receipt.Add(new { kind = "facade-exterior-return-executed", label, returned,
                actual = Point(player.GlobalPosition), road = Point(approach[0]) });
        }
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

    private void CheckFoundationSupports(Act1ConnectedWorld world, FirstPersonController player)
    {
        // A downward first hit can be a visible plinth, neighbouring building
        // or retained route surface. Isolate the actual terrain body explicitly,
        // without changing any live body's layer or relaxing ground tolerance.
        var terrainOnlyExclusions = new global::Godot.Collections.Array<Rid>(
            Descendants(world).OfType<CollisionObject3D>()
                .Where(body => !body.HasMeta("collisionOwner")
                    || body.GetMeta("collisionOwner").AsString() != "act1-exterior-terrain")
                .Select(body => body.GetRid()));
        terrainOnlyExclusions.Add(player.GetRid());
        var supports = Descendants(world).OfType<MeshInstance3D>()
            .Where(mesh => mesh.HasMeta("foundationSourceMesh") && mesh.IsVisibleInTree()).ToArray();
        Check(supports.Length > 0, "No terrain-scribed foundation support was built.");
        foreach (var mesh in supports)
        {
            var feet = mesh.Mesh.GetFaces().Select(mesh.ToGlobal)
                .GroupBy(p => (Mathf.RoundToInt(p.X * 10000), Mathf.RoundToInt(p.Z * 10000)))
                .Select(group => group.MinBy(p => p.Y)).ToArray();
            var tested = 0;
            var highestGap = float.NegativeInfinity;
            foreach (var foot in feet.Where((_, index) => index % Math.Max(1, feet.Length / 12) == 0))
            {
                using var ray = PhysicsRayQueryParameters3D.Create(foot + Vector3.Up * 2, foot - Vector3.Up * 2, 1);
                ray.Exclude = terrainOnlyExclusions;
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                if (hit.Count == 0 || hit["collider"].AsGodotObject() is not Node body
                    || !body.HasMeta("collisionOwner") || body.GetMeta("collisionOwner").AsString() != "act1-exterior-terrain")
                { _failures.Add($"No real terrain under {mesh.GetPath()} at {foot}."); continue; }
                highestGap = Math.Max(highestGap, foot.Y - hit["position"].AsVector3().Y);
                tested++;
            }
            Check(tested > 0, $"No terrain samples below foundation support {mesh.GetPath()}.");
            if (tested > 0)
                Check(highestGap <= .005f, $"Floating foundation support {mesh.GetPath()}: gap={highestGap:F3}m.");
            _receipt.Add(new { kind = "foundation-terrain-contact", mesh = mesh.GetPath().ToString(), samples = tested,
                maximumGap = tested == 0 ? (float?)null : highestGap });
        }
    }

    private async Task CheckSmallSpaceGeometry(Act1ConnectedWorld world, FirstPersonController player)
    {
        var shed = world.FindChild("BabaiYardAuthoredShed", true, false) as Node3D
            ?? throw new InvalidOperationException("Missing production yard shed.");
        Check(shed.HasMeta("smallSpaceContract"), "The yard retained its closed low shed instead of the supported loft.");
        var ladder = shed.GetNode<LadderTraversal3D>("FixedLoftLadder");
        Check(shed.GlobalBasis.Scale.DistanceTo(Vector3.One) < .002f, "The human-scale loft inherited the old small shed scale.");
        var boards = Descendants(shed).OfType<MeshInstance3D>()
            .Where(mesh => mesh.Name.ToString().StartsWith("HeroYardShed_LoftBoard_", StringComparison.Ordinal)).ToArray();
        Check(boards.Length == 11, $"Loft floor board coverage changed: {boards.Length}.");
        foreach (var board in boards) CheckSupportContact(board, player, "loft-floor");
        foreach (var z in new[] { 1.4f, 0f, -1.5f })
        {
            var feet = shed.ToGlobal(new(-1.15f, .075f, z));
            var crouchFits = player.CanCrouchAt(feet);
            var standingFits = player.CanStandAt(feet);
            Check(crouchFits && !standingFits, $"Underdeck at z={z}: crouch={crouchFits}, standing={standingFits}.");
            _receipt.Add(new { kind = "underdeck-capsule-fit", localZ = z, crouchFits, standingFits });
        }
        var front = Ground(shed.ToGlobal(new(-1.15f, 0, 3.18f)));
        player.ApplyZoneSpawn(front, shed.GlobalRotationDegrees.Y);
        player.SetModalOpen(false);
        await PhysicsFrames(4);
        Check(!world.CanUseSmallSpaceInteraction("urman.chapter1:interaction/discover-underdeck-rattle"),
            "Underdeck observation can be acquired from the exterior approach.");
        if (!player.IsCrouching)
        {
            Input.ActionPress("crouch"); await PhysicsFrames(2); Input.ActionRelease("crouch"); await PhysicsFrames(2);
        }
        // Follow the useful low passage around the existing loose board, then
        // return through the same physical entrances without changing flags.
        var route = new[] { new Vector3(-1.15f, .075f, 1.40f), new(-1.15f, .075f, .30f),
            new(.05f, .075f, .30f), new(.05f, .075f, -1.55f), new(-1.15f, .075f, -1.55f),
            new(-1.15f, .075f, -2.85f) };
        foreach (var point in route) await Walk(player, shed.ToGlobal(point), "underdeck-forward");
        foreach (var point in route.Reverse().Skip(1)) await Walk(player, shed.ToGlobal(point), "underdeck-return");
        await Walk(player, front, "underdeck-front-exit");
        Check(player.CanStandAt(player.GlobalPosition), "The underdeck return does not permit standing outside.");
        if (player.IsCrouching)
        {
            Input.ActionPress("crouch"); await PhysicsFrames(2); Input.ActionRelease("crouch"); await PhysicsFrames(2);
        }
        AimAt(player, shed.ToGlobal(new(0, 2.0f, 0)));
        await Capture(player, "hero_yard_shed_ground_approach");

        // This is an explicitly placed local geometry fixture. Earned ladder
        // input, abort and save behaviour belong to Act1YardMechanismsSmokeTest.
        player.ApplyZoneSpawn(ladder.ToGlobal(ladder.UpperLanding), shed.GlobalRotationDegrees.Y);
        player.SetModalOpen(false);
        await PhysicsFrames(5);
        Check(!player.IsCrouching && player.CanStandAt(player.GlobalPosition), "Loft landing does not fit a standing ordinary player.");
        await Walk(player, shed.ToGlobal(new(0, 1.50f, -.10f)), "loft-centre");
        await Walk(player, shed.ToGlobal(new(0, 1.50f, -1.10f)), "loft-hay-vent");
        Check(!world.CanUseSmallSpaceInteraction("urman.chapter1:interaction/discover-shed-loft-roofline"),
            "The rear hay vent still grants a distant roof-repair observation.");
        var at = shed.ToLocal(player.GlobalPosition);
        Check(at.Y >= 1.40f && at.Y <= 1.56f, $"Loft feet do not settle on the visible board floor: {at}.");
        AimAt(player, shed.ToGlobal(new(0, 2.76f, -2.04f)));
        await Capture(player, "hero_yard_loft_vent_view");
        var repair = shed.GetNode<Node3D>("LoftRoofRepair");
        var plate = repair.GetNode<MeshInstance3D>("BoltedSteelSplice");
        CheckLoftRepairBearing(shed, repair);
        await Walk(player, shed.ToGlobal(new(0, 1.50f, -.10f)), "loft-return-centre");
        await CheckLoftHayPresentation(shed, player);
        await Walk(player, repair.GetMeta("observationAnchor").AsVector3(), "loft-roof-repair-approach");
        var target = Descendants(world.GetZoneInstance("village_day")!).OfType<InteractionTarget>().Single(node =>
            node.InteractionId == "urman.chapter1:interaction/discover-shed-loft-roofline");
        var available = world.CanUseSmallSpaceInteraction(target.InteractionId);
        Check(available && target.GetMeta("activePropPath").AsString() == plate.GetPath().ToString(),
            "The ordinary upper landing cannot inspect the mounted roof splice.");
        AimAt(player, target.GlobalPosition);
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        using (var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, target.GlobalPosition, 5))
        {
            ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
            var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var first = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as Node;
            var seesTarget = first == target || (first is not null && target.IsAncestorOf(first));
            Check(seesTarget, $"The roof repair is not selectable from its standing approach: {first?.GetPath()}.");
            _receipt.Add(new { kind = "loft-mounted-roof-repair-observation", available, seesTarget,
                actualFeet = Point(shed.ToLocal(player.GlobalPosition)), eye = Point(shed.ToLocal(camera.GlobalPosition)),
                target = Point(shed.ToLocal(target.GlobalPosition)), firstCollider = first?.GetPath().ToString(),
                mountedMesh = plate.GetPath().ToString() });
        }
        await Capture(player, "hero_yard_loft_roof_repair");
        AimAt(player, repair.GetMeta("villageViewAim").AsVector3());
        await Capture(player, "hero_yard_loft_village_view");
        _receipt.Add(new { kind = "loft-local-geometry-fixture", feet = Point(at),
            placementMethod = "explicit upper landing fixture; this is not evidence of earned ascent",
            forestArtReview = "requires-current-capture", humanPlaytest = false });
        player.ApplyZoneSpawn(front, 0);
        await PhysicsFrames(3);
        Check(!world.CanUseSmallSpaceInteraction(target.InteractionId), "The roof repair can be acquired from ground level.");
    }

    private async Task CheckLoftHayPresentation(Node3D shed, FirstPersonController player)
    {
        var sources = Descendants(shed).OfType<MeshInstance3D>().ToArray();
        foreach (var (prefix, expectedCount, expectedTexture) in new[]
        {
            ("HeroYardShed_LoftBoard_", 11, "res://assets/textures/painterly/weathered_wood_boards_v4_albedo.png"),
            ("HeroYardShed_HayBinding_", 8, "res://assets/textures/painterly/old_fabric_v3_albedo.png")
        })
        {
            var members = sources.Where(mesh => mesh.Name.ToString().StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Check(members.Length == expectedCount, $"{prefix}: expected {expectedCount} sheltered members, found {members.Length}.");
            foreach (var member in members)
            for (var surface = 0; surface < member.Mesh.GetSurfaceCount(); surface++)
            {
                var material = member.MaterialOverride ?? member.GetActiveMaterial(surface);
                var shader = material as ShaderMaterial;
                var texture = shader?.GetShaderParameter("albedo_texture").AsGodotObject() as Texture2D;
                var snow = shader?.GetShaderParameter("snow_coverage").AsSingle();
                var loaded = shader?.GetShaderParameter("has_albedo_texture").AsBool() == true
                    && texture?.ResourcePath == expectedTexture;
                Check(loaded && snow == 0, $"{member.Name}: sheltered loft surface lacks its texture or retains snow={snow}.");
                _receipt.Add(new { kind = "loft-sheltered-surface-material", mesh = member.GetPath().ToString(), surface,
                    texture = texture?.ResourcePath, loaded, snowCoverage = snow,
                    materialRole = member.GetMeta("painterlyMaterial", "").AsString() });
            }
        }
        var bales = Enumerable.Range(0, 4).Select(index => sources.Single(mesh =>
            mesh.Name == $"HeroYardShed_HayBundle_{index}_LOD0")).ToArray();
        var materials = new HashSet<ulong>();
        foreach (var bale in bales)
        for (var surface = 0; surface < bale.Mesh.GetSurfaceCount(); surface++)
        {
            var actual = bale.MaterialOverride ?? bale.GetActiveMaterial(surface);
            Check(actual is ShaderMaterial, $"{bale.Name}: the hay surface has no runtime shader material.");
            if (actual is not ShaderMaterial shader) continue;
            materials.Add(shader.GetInstanceId());
            var texture = shader.GetShaderParameter("albedo_texture").AsGodotObject() as Texture2D;
            var scale = shader.GetShaderParameter("texture_scale").AsVector2();
            var local = shader.GetShaderParameter("local_wood_texture").AsBool();
            var upright = shader.GetShaderParameter("upright_texture").AsBool();
            var authoredUv = shader.GetShaderParameter("authored_uv_texture").AsBool();
            var woodEnd = shader.GetShaderParameter("cut_wood_end").AsBool();
            var roughness = shader.GetShaderParameter("roughness_value").AsSingle();
            var snow = shader.GetShaderParameter("snow_coverage").AsSingle();
            var wind = shader.GetShaderParameter("wind_sway").AsSingle();
            var loaded = shader.GetShaderParameter("has_albedo_texture").AsBool()
                && texture?.ResourcePath == "res://assets/textures/painterly/hay_fibers_v1_albedo.png";
            Check(loaded && local && upright && !authoredUv && !woodEnd
                && scale.DistanceTo(new Vector2(1.4f, 1.4f)) < .0001f
                && Mathf.Abs(roughness - .96f) < .0001f && snow == 0 && wind == 0,
                $"{bale.Name}: actual material is not the dry, locally projected hay texture.");
            _receipt.Add(new { kind = "loft-hay-actual-material", mesh = bale.GetPath().ToString(), surface,
                materialInstance = shader.GetInstanceId(), loaded, texture = texture?.ResourcePath,
                textureWidth = texture?.GetWidth(), textureHeight = texture?.GetHeight(),
                scale = new[] { scale.X, scale.Y }, local, upright, authoredUv, woodEnd, roughness, snow, wind });
        }
        Check(materials.Count == 1, $"The four hay bundles did not reuse their runtime material: {materials.Count} instances.");
        var observation = shed.ToGlobal(new(0, 1.50f, .15f));
        var fits = player.CanStandAt(observation);
        Check(fits, "The central loft aisle cannot accommodate the two hay observation views.");
        if (!fits) return;
        await Walk(player, observation, "loft-hay-observation-centre");
        await PhysicsFrames(3);
        var standing = !player.IsCrouching && player.IsOnFloor() && player.CanStandAt(player.GlobalPosition)
            && Horizontal(player.GlobalPosition, observation) < .18f;
        Check(standing, "The ordinary central loft approach did not reach a standing hay observation position.");
        if (!standing) return;
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        foreach (var (label, pair) in new[] { ("hero_yard_loft_hay_left", new[] { bales[0], bales[1] }),
                     ("hero_yard_loft_hay_right", new[] { bales[2], bales[3] }) })
        {
            var centres = pair.Select(bale => bale.GlobalTransform * bale.Mesh.GetAabb().GetCenter()).ToArray();
            AimAt(player, (centres[0] + centres[1]) * .5f);
            await Frames(2);
            for (var index = 0; index < pair.Length; index++)
            {
                var bale = pair[index];
                var bounds = bale.Mesh.GetAabb();
                var framed = Enumerable.Range(0, 8).All(corner => camera.IsPositionInFrustum(bale.ToGlobal(bounds.GetEndpoint(corner))));
                var visible = RenderedHit(bale, camera.GlobalPosition, centres[index], out var surfacePoint, out _);
                using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, centres[index], 1);
                ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                var body = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as CollisionObject3D;
                var shape = body?.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
                var source = shape?.GetMeta("authoredSourceMesh", "").AsString();
                var ownContact = source == bale.GetPath().ToString();
                float? error = visible && hit.Count > 0 ? surfacePoint.DistanceTo(hit["position"].AsVector3()) : null;
                Check(framed && visible && ownContact && error < .015f,
                    $"{label}/{bale.Name}: hay is not fully framed and unobstructed from the standing aisle: first={body?.GetPath()}, source={source}.");
                _receipt.Add(new { kind = "loft-hay-observed-surface", label, mesh = bale.GetPath().ToString(),
                    actualFeet = Point(shed.ToLocal(player.GlobalPosition)), eye = Point(camera.GlobalPosition),
                    framed, visible, ownContact, error, firstBody = body?.GetPath().ToString(), source,
                    visiblePoint = visible ? Point(surfacePoint) : null });
            }
            await Capture(player, label);
        }
    }

    private void CheckLoftRepairBearing(Node3D shed, Node3D repair)
    {
        var members = Descendants(shed).OfType<MeshInstance3D>().ToArray();
        MeshInstance3D Source(string name) => members.Single(mesh => mesh.Name == name);
        void Bearing(MeshInstance3D first, MeshInstance3D second, Vector3 localFrom, Vector3 localTo, float minimum)
        {
            var from = shed.ToGlobal(localFrom); var to = shed.ToGlobal(localTo);
            var direction = (to - from).Normalized();
            // Both rays must start outside both solids. A short segment can end
            // inside a thick support, returning its same entry face in reverse
            // and falsely reducing a real overlap to zero.
            var projections = new[] { first, second }.SelectMany(mesh => mesh.Mesh.GetFaces().Select(mesh.ToGlobal))
                .Select(point => (point - from).Dot(direction)).ToArray();
            var lineOrigin = from;
            from = lineOrigin + direction * (projections.Min() - .02f);
            to = lineOrigin + direction * (projections.Max() + .02f);
            var aFar = Vector3.Zero; var bFar = Vector3.Zero;
            var a = RenderedHit(first, from, to, out var aNear, out _)
                && RenderedHit(first, to, from, out aFar, out _);
            var b = RenderedHit(second, from, to, out var bNear, out _)
                && RenderedHit(second, to, from, out bFar, out _);
            float? overlap = a && b ? Math.Min((aFar - from).Dot(direction), (bFar - from).Dot(direction))
                - Math.Max((aNear - from).Dot(direction), (bNear - from).Dot(direction)) : null;
            Check(overlap >= minimum, $"Loft roof repair has no actual bearing between {first.Name} and {second.Name}: {overlap}.");
            _receipt.Add(new { kind = "loft-repair-mesh-bearing", first = first.Name.ToString(), second = second.Name.ToString(),
                from = Point(shed.ToLocal(from)), to = Point(shed.ToLocal(to)), overlap, minimum,
                requestedFrom = Point(localFrom), requestedTo = Point(localTo) });
        }
        foreach (var (name, side) in new[] { ("Left", -1), ("Right", 1) })
        {
            var rafter = repair.GetNode<MeshInstance3D>(name + "Rafter");
            Bearing(rafter, Source($"HeroYardShed_SupportPost_{(side < 0 ? "L" : "R")}Front_LOD0"),
                new(side * 2.06f, 3.52f, 2.04f), new(side * 2.06f, 0, 2.04f), .02f);
            Bearing(rafter, Source("HeroYardShed_Roof_LOD0"),
                new(side * 1.05f, 4.5f, 2.04f), new(side * 1.05f, 3.4f, 2.04f), .004f);
        }
        var timber = repair.GetNode<MeshInstance3D>("FreshTimberScab");
        var plate = repair.GetNode<MeshInstance3D>("BoltedSteelSplice");
        var centre = shed.ToLocal(plate.GlobalPosition);
        Bearing(timber, repair.GetNode<MeshInstance3D>("RightRafter"), centre - Vector3.Back * .15f,
            centre + Vector3.Back * .15f, .002f);
        Bearing(plate, timber, centre - Vector3.Back * .15f, centre + Vector3.Back * .15f, .0015f);
        foreach (var bolt in Descendants(repair).OfType<MeshInstance3D>().Where(mesh => mesh.Name.ToString().StartsWith("Bolt_", StringComparison.Ordinal)))
        {
            var boltCentre = shed.ToLocal(bolt.GlobalPosition);
            Bearing(bolt, plate, boltCentre - Vector3.Back * .08f, boltCentre + Vector3.Back * .08f, .002f);
        }
        foreach (var member in Descendants(repair).OfType<MeshInstance3D>().Where(mesh => !mesh.Name.ToString().StartsWith("Bolt_", StringComparison.Ordinal)))
        {
            var contact = Descendants(shed).OfType<CollisionShape3D>().Single(shape => shape.HasMeta("authoredSourceMesh")
                && shape.GetMeta("authoredSourceMesh").AsString() == member.GetPath().ToString());
            var physical = ((ConcavePolygonShape3D)contact.Shape).GetFaces(); var visible = member.Mesh.GetFaces();
            var same = physical.Length == visible.Length;
            float? error = same && visible.Length > 0 ? visible.Select((point, index) => member.ToGlobal(point).DistanceTo(contact.ToGlobal(physical[index]))).Max() : null;
            Check(!contact.Disabled && same && error < .00002f, $"The mounted roof member {member.Name} lost its real contact: {error}.");
            _receipt.Add(new { kind = "loft-repair-visible-contact", member = member.Name.ToString(), same, error, disabled = contact.Disabled });
        }
    }

    private async Task CheckFapPathContacts(Act1ConnectedWorld world, FirstPersonController player)
    {
        var path = world.FindChild("FapServiceExplorationPresentation", true, false)
            ?? throw new InvalidOperationException("Missing FAP service path.");
        var boards = Descendants(path).OfType<MeshInstance3D>()
            .Where(mesh => mesh.Name.ToString().StartsWith("FapServicePath", StringComparison.Ordinal)).ToArray();
        Check(boards.Length > 0, "The FAP service route has no visible timber supports to inspect.");
        foreach (var board in boards) CheckSupportContact(board, player, "fap-service-path");
        if (!string.IsNullOrEmpty(_output))
        {
            var crate = Descendants(world).OfType<CarryableProp>().Single(prop => prop.ItemId == "carry-gap-crate");
            var start = Ground(new(41.5f, 0, -25f)); // actual east service-loop vertex
            player.ApplyZoneSpawn(start, 0);
            player.SetModalOpen(false);
            await PhysicsFrames(6);
            AimAt(player, crate.GlobalPosition + crate.GlobalBasis.Y * crate.Height * .45f);
            _receipt.Add(new { kind = "fap-path-local-view-fixture", fixture = Point(start), actual = Point(player.GlobalPosition),
                crate = crate.GetPath().ToString(), state = crate.State.ToString(), onFloor = player.IsOnFloor(),
                scope = "ordinary-height view of the authored route obstruction; not a traversal or carry proof" });
            await Capture(player, "fap_service_path_crate_and_planks");
        }
    }

    private async Task CheckFapEnvelope(Act1ConnectedWorld world, FirstPersonController player)
    {
        var facade = world.GetNode<Node3D>("Act1CoreWorldGreybox/FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredFacade");
        var clinic = world.GetZoneInstance("fap_clinic")!;
        var expected = facade.GlobalTransform * new Transform3D(Basis.Identity, new Vector3(0, .40f, 0));
        Check(clinic.GlobalPosition.DistanceTo(expected.Origin) < .002f
            && clinic.GlobalBasis.Scale.DistanceTo(Vector3.One) < .002f, "FAP room is not inside its metric exterior shell.");
        var outer = Descendants(facade).OfType<MeshInstance3D>().ToArray();
        var inner = Descendants(clinic).OfType<MeshInstance3D>().ToArray();
        foreach (var (inside, outside) in new[]
        {
            ("WindowFrontLeftGlass", "WindowLeft_Glass"), ("WindowFrontRightGlass", "WindowRight_Glass"),
            ("WindowLeftGlass", "SideWindowLeft_Glass"), ("WindowRightGlass", "SideWindowRight_Glass"),
            ("DoorInset", "DoorPanel"), ("ServiceDoorPanel", "ServiceDoor")
        })
        {
            var a = inner.Single(mesh => mesh.Name == "FapInteriorShell_" + inside + "_LOD0");
            var b = outer.Single(mesh => mesh.Name == "FapFacade_" + outside + "_LOD0");
            var positionA = a.GlobalTransform * a.Mesh.GetAabb().GetCenter();
            var positionB = b.GlobalTransform * b.Mesh.GetAabb().GetCenter();
            var boundsA = a.GlobalTransform * a.Mesh.GetAabb();
            var boundsB = b.GlobalTransform * b.Mesh.GetAabb();
            var error = positionA.DistanceTo(positionB);
            Check(error < .006f && boundsA.Size.DistanceTo(boundsB.Size) < .012f,
                $"FAP {inside}/{outside} fails facade-room alignment: centre={error:F4}, size={boundsA.Size}/{boundsB.Size}.");
            var normal = outside.StartsWith("SideWindowLeft", StringComparison.Ordinal) ? Vector3.Left
                : outside.StartsWith("SideWindowRight", StringComparison.Ordinal) || outside == "ServiceDoor" ? Vector3.Right : Vector3.Back;
            normal = (facade.GlobalBasis * normal).Normalized();
            using var ray = PhysicsRayQueryParameters3D.Create(positionB + normal * .5f, positionB - normal * .5f, 2);
            ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
            var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var matched = hit.Count > 0 && hit["collider"].AsGodotObject() is StaticBody3D body
                && body.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) is Node shape
                && shape.HasMeta("authoredSourceMesh") && shape.GetMeta("authoredSourceMesh").AsString() == b.GetPath().ToString();
            Check(matched, $"FAP {outside}: the closed visible pane/leaf is missing or its actual reveal is blocked by a wall.");
            _receipt.Add(new { kind = "fap-opening-correspondence", inside, outside, error, matched,
                roomCentre = Point(positionA), facadeCentre = Point(positionB) });
        }
        var door = outer.Single(mesh => mesh.Name == "FapFacade_DoorPanel_LOD0");
        var doorCentre = door.GlobalTransform * door.Mesh.GetAabb().GetCenter();
        var doorBounds = (facade.GlobalTransform.AffineInverse() * door.GlobalTransform) * door.Mesh.GetAabb();
        foreach (var threshold in Descendants(world).OfType<MeshInstance3D>().Where(mesh => mesh.Name.ToString()
                     is "FapEntryPorch_Threshold_LOD0" or "FapInteriorShell_Threshold_LOD0"))
        {
            var bounds = (facade.GlobalTransform.AffineInverse() * threshold.GlobalTransform) * threshold.Mesh.GetAabb();
            var closesGap = bounds.Position.Z <= doorBounds.Position.Z && bounds.End.Z >= doorBounds.End.Z
                && bounds.End.Y >= doorBounds.Position.Y - .002f && bounds.End.Y < doorBounds.Position.Y + .025f
                && bounds.Size.X >= doorBounds.Size.X - .001f;
            Check(closesGap, $"FAP {threshold.Name} leaves a gap beneath the actual door plane: threshold={bounds}, door={doorBounds}.");
            _receipt.Add(new { kind = "fap-door-bottom-support", threshold = threshold.GetPath().ToString(),
                thresholdTop = bounds.End.Y, doorBottom = doorBounds.Position.Y, closesGap });
        }
        var entry = world.GetZoneInstance("village_day")!.GetNode<InteractionTarget>("RoadToFap");
        Check(entry.GlobalPosition.DistanceTo(doorCentre + facade.GlobalBasis.Z * .20f) < .005f,
            "The clinic entrance action is not attached to the current visible door.");
        foreach (var spawnId in new[] { "waiting_room", "default", "unknown-local-test-spawn" })
        {
            Check(world.TryGetWorldSpawn("fap_clinic", spawnId, out var spawn)
                && spawn.Position.DistanceTo(clinic.ToGlobal(new Vector3(0, .05f, 4.8f))) < .005f,
                $"FAP {spawnId} spawn ignores the actual room transform.");
        }
        var porch = world.GetNode<Node3D>("Act1CoreWorldGreybox/FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredEntryPorch");
        var deck = Descendants(porch).OfType<MeshInstance3D>().Single(mesh => mesh.Name == "FapEntryPorch_Deck_LOD0");
        foreach (var jamb in outer.Where(mesh => mesh.Name.ToString() is "FapFacade_DoorFrameLeft_LOD0" or "FapFacade_DoorFrameRight_LOD0"))
        {
            var points = jamb.Mesh.GetFaces().Select(jamb.ToGlobal).Distinct().ToArray();
            var bottom = points.Min(point => point.Y);
            var feet = points.Where(point => point.Y < bottom + .0001f).ToArray();
            var centre = feet.Aggregate(Vector3.Zero, (sum, point) => sum + point) / feet.Length;
            var excludes = new global::Godot.Collections.Array<Rid> { player.GetRid() };
            foreach (var shape in Descendants(world).OfType<CollisionShape3D>().Where(shape => shape.HasMeta("authoredSourceMesh")
                         && shape.GetMeta("authoredSourceMesh").AsString() == jamb.GetPath().ToString()))
                if (shape.GetParent() is CollisionObject3D owner && !excludes.Contains(owner.GetRid())) excludes.Add(owner.GetRid());
            foreach (var foot in feet)
            {
                var at = foot.Lerp(centre, .02f);
                var rendered = RenderedHit(deck, at + Vector3.Up * .015f, at - Vector3.Up * .085f, out var visible, out _);
                using var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * .015f, at - Vector3.Up * .085f, 3);
                query.Exclude = excludes;
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(query);
                var error = rendered ? Math.Abs(visible.Y - at.Y) : -1f;
                var supported = rendered && error < .002f && hit.Count > 0
                    && hit["position"].AsVector3().DistanceTo(visible) < .003f;
                Check(supported, $"FAP {jamb.Name} still floats or overhangs its deck at {foot}: visible support error={error:F4}m.");
                _receipt.Add(new { kind = "fap-jamb-full-foot-support", jamb = jamb.Name.ToString(), foot = Point(foot), supported, error });
            }
        }
        var foundation = outer.Single(mesh => mesh.Name == "FapFacade_Foundation_LOD0");
        foreach (var x in new[] { -5.25f, 0, 5.25f })
        foreach (var z in new[] { -5.25f, 0, 5.25f })
        {
            var at = clinic.ToGlobal(new(x, 0, z));
            var coversFinish = RenderedHit(foundation, at + Vector3.Up * .01f, at - Vector3.Up * .025f, out _, out _);
            var seated = RenderedHit(foundation, at - Vector3.Up * .105f, at - Vector3.Up * .135f, out var bed, out _)
                && Math.Abs(bed.Y - (at.Y - .12f)) < .002f;
            Check(!coversFinish && seated, $"FAP foundation must support the floor underside and leave its finish clear at({x},{z}): covered={coversFinish}, seated={seated}.");
            _receipt.Add(new { kind = "fap-floor-foundation-seat", local = new[] { x, z }, coversFinish, seated });
        }
        var start = Ground(facade.ToGlobal(new(0, 0, 8.42f)));
        player.ApplyZoneSpawn(start, facade.GlobalRotationDegrees.Y);
        player.SetModalOpen(false);
        await PhysicsFrames(4);
        AimAt(player, doorCentre);
        await Capture(player, "fap_aligned_entry_approach");
        await Walk(player, facade.ToGlobal(new(0, .54f, 6.24f)), "fap-real-porch-ascent");
        var onDeck = facade.ToLocal(player.GlobalPosition);
        Check(onDeck.Y > .45f && onDeck.Y < .58f, $"FAP feet do not stand on the physical entry deck: {onDeck}.");
        AimAt(player, doorCentre);
        await Capture(player, "fap_aligned_door_contact");
        await Walk(player, start, "fap-real-porch-descent");
        _receipt.Add(new { kind = "fap-porch-normal-walk", entryDeckFeet = Point(onDeck),
            door = Point(doorCentre), facadeOrigin = Point(facade.GlobalPosition), roomOrigin = Point(clinic.GlobalPosition),
            porchOrigin = Point(porch.GlobalPosition), stepsClimbed = player.StepsClimbed,
            lastStepRejection = player.LastStepRejection });
    }

    private async Task CheckFapServiceCabinet(Act1ConnectedWorld world, FirstPersonController player)
    {
        var clinic = world.GetZoneInstance("fap_clinic")!;
        var target = Descendants(clinic).OfType<InteractionTarget>().Single(node =>
            node.InteractionId == "urman.chapter1:interaction/discover-fap-service-cabinet");
        Check(!world.CanUseSmallSpaceInteraction(target.InteractionId), "The cabinet observation is available from outside the clinic.");
        world.SetActiveLogicalZone("fap_clinic");
        player.ApplyZoneSpawn(clinic.ToGlobal(new(0, .04f, 3.8f)), clinic.GlobalRotationDegrees.Y);
        player.SetModalOpen(false);
        await PhysicsFrames(4);
        await CheckFapFurniture(clinic, player);
        AimAt(player, target.GlobalPosition);
        Check(!world.CanUseSmallSpaceInteraction(target.InteractionId), "The cabinet can be inspected from the clinic entrance.");
        foreach (var point in new[] { new Vector3(.20f, .04f, -3.05f), new Vector3(-2.45f, .04f, -3.05f) })
            await Walk(player, clinic.ToGlobal(point), "fap-cabinet-approach");
        await Walk(player, target.GetMeta("accessAnchor").AsVector3(), "fap-cabinet-service-corner");
        AimAt(player, target.GlobalPosition);
        await PhysicsFrames(2);
        Check(world.CanUseSmallSpaceInteraction(target.InteractionId), "The cabinet's physical service approach cannot inspect its fixing.");
        var contact = clinic.GetNode<StaticBody3D>("FapServiceCabinetCollision");
        Check(contact.CollisionLayer != 0 && Descendants(contact).OfType<CollisionShape3D>().Any(),
            "The inspected cabinet has no physical carcass.");
        await Capture(player, "fap_service_cabinet_fixing");
        await Walk(player, clinic.ToGlobal(new(-2.45f, .04f, -3.05f)), "fap-cabinet-return");
        await Walk(player, clinic.ToGlobal(new(.20f, .04f, -3.05f)), "fap-cabinet-return-centre");
        await Walk(player, clinic.ToGlobal(new(0, .04f, 3.8f)), "fap-cabinet-exit-approach");
        world.SetActiveLogicalZone("village_day");
        Check(contact.CollisionLayer == 0, "The service cabinet remained physical outside the clinic.");
        foreach (var body in Descendants(clinic).OfType<StaticBody3D>().Where(node => node.HasMeta("sourceGroup")))
            Check(body.CollisionLayer == 0, $"FAP furniture {body.Name} remained physical outside the clinic.");
    }

    private async Task CheckFapOpticalViews(Act1ConnectedWorld world, FirstPersonController player)
    {
        var clinic = world.GetZoneInstance("fap_clinic")!;
        var core = world.GetNode<Node3D>("Act1CoreWorldGreybox");
        var facade = core.GetNode<Node3D>("FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredFacade");
        var cards = Descendants(facade).OfType<MeshInstance3D>()
            .Where(mesh => mesh.HasMeta("clinicExteriorGlassOriginalVisibility")).ToArray();
        world.SetActiveLogicalZone("fap_clinic");
        player.SetModalOpen(false);
        await PhysicsFrames(2);
        await Frames(8);
        var probe = clinic.GetNode<ReflectionProbe>("ClinicStaticRoomReflection");
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var environments = Descendants(GetTree().Root).OfType<WorldEnvironment>()
            .Where(node => node.Environment is not null).ToArray();
        var sources = Descendants(GetTree().Root).OfType<MeshInstance3D>()
            .Where(mesh => (mesh.Layers & probe.CullMask) != 0).ToArray();
        var expectedLightNames = new[] { "ColdCeilingLamp", "FapWindowColdFill", "FapDocumentTaskLight", "FapNailaPractical", "FapWindowOppositeFill" };
        var lights = expectedLightNames.Select(name => clinic.GetNode<Light3D>(name)).ToArray();
        var dynamicLights = Descendants(clinic).OfType<Light3D>().Where(light => !lights.Contains(light)).ToArray();
        var mirror = Descendants(clinic.GetNode<Node3D>("ClinicPlainMirror")).OfType<MeshInstance3D>().Single();
        var mirrorAim = clinic.ToLocal(mirror.GlobalTransform * mirror.Mesh.GetAabb().GetCenter());
        Check(core.IsVisibleInTree(), "The actual village is hidden behind the active clinic windows.");
        Check(environments.Length == 1, $"The clinic optics view has {environments.Length} active root environments.");
        Check(cards.Length == 5 && cards.All(mesh => !mesh.Visible), "Matched exterior FAP glazing cards still cover the interior view.");
        Check(probe.IsVisibleInTree() && probe.UpdateMode == ReflectionProbe.UpdateModeEnum.Once,
            "The clinic mirror has no active cached reflection probe.");
        Check(probe.CullMask == 1u << 18 && probe.ReflectionMask == 1u << 19
            && (mirror.Layers & probe.ReflectionMask) != 0,
            "The clinic reflection source/recipient masks differ from their scoped runtime policy.");
        Check(sources.Length == clinic.GetMeta("clinicReflectionSourceCount").AsInt32()
            && sources.All(mesh => clinic.IsAncestorOf(mesh)),
            "The static clinic reflection includes sources outside its registered room meshes.");
        Check(lights.Length == 5 && lights.Length == clinic.GetMeta("clinicReflectionLightCount").AsInt32()
            && lights.All(light => (light.Layers & probe.CullMask) != 0),
            "The cached clinic reflection excludes one or more of its five real room lights.");
        Check(dynamicLights.All(light => (light.Layers & probe.CullMask) == 0),
            "A changing discovery lamp entered the cached static clinic reflection.");
        _receipt.Add(new { kind = "fap-optical-runtime-state", phase = "after-eight-active-process-frames",
            activeZone = world.ActiveZoneId, rootEnvironmentCount = environments.Length,
            rootEnvironmentPaths = environments.Select(node => node.GetPath().ToString()).ToArray(),
            coreVisible = core.IsVisibleInTree(), cameraCurrent = camera.Current,
            probeVisible = probe.IsVisibleInTree(), updateMode = probe.UpdateMode.ToString(), probeInterior = probe.Interior,
            sourceMask = probe.CullMask, reflectionMask = probe.ReflectionMask,
            sourceCount = sources.Length, sourcePaths = sources.Select(node => node.GetPath().ToString()).ToArray(),
            lightSources = lights.Select(light => new { path = light.GetPath().ToString(), layers = light.Layers,
                includedByProbe = (light.Layers & probe.CullMask) != 0 }).ToArray(),
            excludedDynamicLights = dynamicLights.Select(light => new { path = light.GetPath().ToString(), layers = light.Layers }).ToArray(),
            exteriorCards = cards.Select(mesh => new { name = mesh.Name.ToString(), visible = mesh.Visible }).ToArray() });
        var views = new[]
        {
            ("fap_windows_front_pair", new Vector3(0, .04f, 0), new Vector3(0, 1.95f, 5.78f)),
            ("fap_window_left", new Vector3(-3.35f, .04f, -.55f), new Vector3(-5.78f, 1.95f, -.90f)),
            // Stand east of the real partition rail, rather than aim through it.
            // Clearance/settling/ray checks below still use production collisions.
            ("fap_window_right", new Vector3(4.75f, .04f, -.90f), new Vector3(5.78f, 1.95f, -.90f)),
            ("fap_mirror_front", new Vector3(3.8f, .04f, 3.20f), mirrorAim),
            ("fap_mirror_oblique", new Vector3(4.75f, .04f, 3.1f), mirrorAim)
        };
        foreach (var (label, feet, aim) in views)
        {
            var fixture = clinic.ToGlobal(feet);
            var fits = player.CanStandAt(fixture);
            Check(fits, $"The optical view {label} does not fit a standing player at {feet}.");
            if (!fits) continue;
            player.ApplyZoneSpawn(fixture, clinic.GlobalRotationDegrees.Y);
            player.SetModalOpen(false);
            await PhysicsFrames(6);
            var standing = !player.IsCrouching && player.IsOnFloor() && player.CanStandAt(player.GlobalPosition)
                && Horizontal(player.GlobalPosition, fixture) < .08f;
            Check(standing, $"The optical view {label} did not settle on a clear standing floor: {clinic.ToLocal(player.GlobalPosition)}.");
            AimAt(player, clinic.ToGlobal(aim));
            var clear = false;
            string? blocker = null;
            using (var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, clinic.ToGlobal(aim), 1))
            {
                ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                clear = hit.Count == 0 || hit["position"].AsVector3().DistanceTo(clinic.ToGlobal(aim)) < .16f;
                if (hit.Count > 0 && hit["collider"].AsGodotObject() is Node owner) blocker = owner.GetPath().ToString();
            }
            // The front pair deliberately frames the real door between its two
            // panes. A ray to that door is expected, unlike a cupboard or actor.
            if (label != "fap_windows_front_pair")
                Check(clear, $"The optical view {label} is occluded before its actual surface: {blocker}.");
            _receipt.Add(new { kind = "fap-optical-standing-view", label, fixture = Point(feet),
                actualFeet = Point(clinic.ToLocal(player.GlobalPosition)), eye = Point(clinic.ToLocal(camera.GlobalPosition)),
                aim = Point(aim), fits, standing, clear, blocker });
            if (standing) await Capture(player, label);
            if (standing && label == "fap_mirror_oblique")
            {
                await Frames(24);
                await Capture(player, "fap_mirror_oblique_after_settle");
                var original = mirror.MaterialOverride;
                using var diagnostic = new StandardMaterial3D { AlbedoColor = new Color("a7afaa"),
                    Metallic = .93f, Roughness = .24f, MetallicSpecular = .5f };
                try
                {
                    // Same camera, lights and cached probe. This explicitly
                    // labelled diagnostic separates capture from shader causes.
                    mirror.MaterialOverride = diagnostic;
                    await Frames(4);
                    await Capture(player, "fap_mirror_oblique_diagnostic_standard_material");
                }
                finally { mirror.MaterialOverride = original; }
                await Frames(2);
                Check(mirror.MaterialOverride == original, "The mirror diagnostic did not restore its production material.");
            }
        }
        await CheckFapMirrorNormalView(clinic, player, mirror, sources, probe);
        var washBasin = Descendants(clinic).OfType<MeshInstance3D>()
            .Single(mesh => mesh.Name == "FapInteriorWashUnit_Basin_LOD0");
        AimAt(player, washBasin.ToGlobal(washBasin.Mesh.GetAabb().GetCenter()));
        await Capture(player, "fap_wash_basin_close");
        world.SetActiveLogicalZone("village_day");
        await Frames(2);
        var restored = cards.All(mesh => mesh.Visible == mesh.GetMeta("clinicExteriorGlassOriginalVisibility").AsBool());
        Check(restored && !probe.Visible, "Leaving the clinic did not restore exterior glazing and stop the local mirror probe.");
        _receipt.Add(new { kind = "fap-optical-exit-restored", activeZone = world.ActiveZoneId,
            restored, probeVisible = probe.Visible,
            exteriorCards = cards.Select(mesh => new { name = mesh.Name.ToString(), visible = mesh.Visible,
                original = mesh.GetMeta("clinicExteriorGlassOriginalVisibility").AsBool() }).ToArray() });
    }

    private async Task CheckFapMirrorNormalView(Node3D clinic, FirstPersonController player,
        MeshInstance3D mirror, MeshInstance3D[] sources, ReflectionProbe probe)
    {
        // Reach the normal view through the already usable central aisle. The
        // shorter right-side line at x4.75 crosses the actual wash basin.
        foreach (var point in new[] { new Vector3(4.75f, .04f, 3.8f), new(0, .04f, 3.8f),
                     new(0, .04f, 1.56f), new(4.45f, .04f, 1.56f) })
        {
            var destination = clinic.ToGlobal(point);
            var fits = player.CanStandAt(destination);
            Check(fits, $"The normal mirror approach does not fit an ordinary standing player at {point}.");
            if (!fits) return;
            await Walk(player, destination, "fap-mirror-normal-approach");
            if (Horizontal(player.GlobalPosition, destination) > .25f) return;
        }
        await PhysicsFrames(6);
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var bounds = mirror.Mesh.GetAabb();
        var faceLocal = bounds.GetCenter(); faceLocal.X = bounds.Position.X;
        var planePoint = mirror.ToGlobal(faceLocal);
        var normal = (mirror.GlobalBasis.Inverse().Transposed() * Vector3.Left).Normalized();
        if (normal.Dot(clinic.GlobalPosition - planePoint) < 0) normal = -normal;
        var eye = camera.GlobalPosition;
        var aim = eye - normal.Dot(eye - planePoint) * normal;
        var aimLocal = mirror.ToLocal(aim);
        var insideGlass = aimLocal.Y > bounds.Position.Y && aimLocal.Y < bounds.End.Y
            && aimLocal.Z > bounds.Position.Z && aimLocal.Z < bounds.End.Z;
        var standing = !player.IsCrouching && player.IsOnFloor() && player.CanStandAt(player.GlobalPosition);
        Check(standing && insideGlass, "The normal mirror view did not settle at a real standing eye height inside its glass.");
        if (!standing || !insideGlass) return;
        AimAt(player, aim);
        using (var ray = PhysicsRayQueryParameters3D.Create(eye, aim, 1))
        {
            ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
            var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var clear = hit.Count == 0 || hit["position"].AsVector3().DistanceTo(aim) < .16f;
            var blocker = hit.Count == 0 ? null : (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString();
            Check(clear, $"The normal mirror view is blocked before its actual glass: {blocker}.");
            _receipt.Add(new { kind = "fap-mirror-normal-standing-view", standing, insideGlass, clear, blocker,
                feet = Point(clinic.ToLocal(player.GlobalPosition)), eye = Point(eye), aim = Point(aim),
                planePoint = Point(planePoint), normal = Point(normal), probeInterior = probe.Interior });
        }
        var samples = new List<(string Label, Vector3 Point)> { ("eye-projection", aim), ("centre", planePoint) };
        foreach (var (u, v) in new[] { (.22f, .5f), (.78f, .5f), (.5f, .22f), (.5f, .78f) })
            samples.Add(($"u{u:F2}-v{v:F2}", mirror.ToGlobal(new(bounds.Position.X,
                Mathf.Lerp(bounds.Position.Y, bounds.End.Y, v), Mathf.Lerp(bounds.Position.Z, bounds.End.Z, u)))));
        foreach (var sample in samples)
        {
            var incoming = (sample.Point - eye).Normalized();
            var reflected = incoming - 2 * incoming.Dot(normal) * normal;
            var from = sample.Point + normal * .003f; var to = from + reflected * 25f;
            MeshInstance3D? nearest = null; var position = Vector3.Zero; float? distance = null;
            foreach (var source in sources.Where(mesh => mesh.IsVisibleInTree()))
                if (RenderedHit(source, from, to, out var hit, out _))
                {
                    var candidate = from.DistanceTo(hit);
                    if (distance is not null && candidate >= distance) continue;
                    nearest = source; position = hit; distance = candidate;
                }
            _receipt.Add(new { kind = "fap-mirror-static-source-reflected-ray", sample = sample.Label,
                surfacePoint = Point(sample.Point), eye = Point(eye), normal = Point(normal), reflected = Point(reflected),
                captureSourceHit = nearest?.GetPath().ToString(), worldHit = nearest is null ? null : Point(position), distance,
                scope = "rendered triangles of registered static capture sources; not a physical ray or proof of reflection pixels" });
        }
        await Capture(player, "fap_mirror_normal");
        if (mirror.MaterialOverride is not ShaderMaterial original)
        { Check(false, "The mirror roughness diagnostic requires its actual production ShaderMaterial."); return; }
        using var diagnostic = (ShaderMaterial)original.Duplicate();
        diagnostic.SetShaderParameter("roughness_value", .05f);
        try
        {
            mirror.MaterialOverride = diagnostic;
            await Frames(4);
            await Capture(player, "fap_mirror_normal_diagnostic_roughness005");
        }
        finally { mirror.MaterialOverride = original; }
        await Frames(2);
        Check(mirror.MaterialOverride == original, "The normal mirror diagnostic did not restore its exact production material.");
    }

    private async Task CheckFapFurniture(Node3D clinic, FirstPersonController player)
    {
        var meshes = Descendants(clinic).OfType<MeshInstance3D>().Where(mesh => mesh.Mesh is not null).ToArray();
        MeshInstance3D Source(string name) => meshes.Single(mesh => mesh.Name == name);
        Aabb LocalBounds(MeshInstance3D mesh) => (clinic.GlobalTransform.AffineInverse() * mesh.GlobalTransform) * mesh.Mesh.GetAabb();
        var floorY = LocalBounds(Source("FapInteriorShell_Floor_LOD0")).End.Y;
        var mirror = LocalBounds(Descendants(clinic.GetNode<Node3D>("ClinicPlainMirror")).OfType<MeshInstance3D>().Single());
        var mirrorFrameBounds = LocalBounds(Descendants(clinic.GetNode<Node3D>("ClinicPlainMirrorFrame")).OfType<MeshInstance3D>().Single());
        var splash = LocalBounds(Source("FapInteriorWashUnit_Splash_LOD0"));
        var faucet = LocalBounds(Source("FapInteriorWashUnit_FaucetSpout_LOD0"));
        var frameGap = splash.Position.X - mirrorFrameBounds.End.X;
        var frameBearing = Math.Min(mirrorFrameBounds.End.Y, splash.End.Y) - Math.Max(mirrorFrameBounds.Position.Y, splash.Position.Y);
        var faucetClear = !mirror.Grow(.015f).Intersects(faucet);
        Check(Math.Abs(frameGap) < .001f && frameBearing > .8f && mirror.End.X >= mirrorFrameBounds.Position.X
            && mirror.Position.Y < 1.74f && mirror.End.Y > 1.74f && faucetClear,
            "The clinic mirror is not mounted at face height on its actual splash/frame, or intersects the faucet.");
        _receipt.Add(new { kind = "clinic-mirror-human-height-and-bearing", frameGap, frameBearing,
            mirrorLowerEdge = mirror.Position.Y, mirrorUpperEdge = mirror.End.Y, faucetClear,
            faucetVerticalGap = mirror.Position.Y - faucet.End.Y });
        foreach (var finish in meshes.Where(mesh => mesh.Name.ToString().StartsWith("FapInteriorFloor_", StringComparison.Ordinal)
                     || mesh.Name == "FapInteriorShell_EntryMat_LOD0"))
        {
            var bounds = LocalBounds(finish);
            var gap = bounds.Position.Y - floorY;
            Check(Math.Abs(gap) < .0002f && bounds.Size.Y <= .0181f,
                $"FAP floor finish {finish.Name} floats above its floor or has an excessive step: gap={gap:F6}, height={bounds.Size.Y:F6}.");
            _receipt.Add(new { kind = "fap-floor-finish-support", mesh = finish.Name.ToString(), gap, height = bounds.Size.Y });
        }
        foreach (var group in new[] { "Bench", "Cot", "Screen", "Stool", "ReceptionCounter", "RecordsDesk", "Trolley", "Partition", "TallStorage" })
        {
            var bottom = meshes.Where(mesh => mesh.Name.ToString().StartsWith("FapInterior" + group + "_", StringComparison.Ordinal)
                && mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)).Min(mesh => LocalBounds(mesh).Position.Y);
            Check(Math.Abs(bottom - floorY) < .004f, $"FAP {group} has no floor contact: bottom={bottom:F3}, floor={floorY:F3}.");
            _receipt.Add(new { kind = "fap-furniture-floor-support", group, bottom, floorY });
        }
        // Compare independently rendered surfaces with physics at each furniture
        // group. A proxy enclosing its whole footprint cannot satisfy the named
        // surface contacts and the ordinary paths checked below.
        var probes = new[]
        {
            ("Bench_Seat", Vector3.Up), ("Cot_Mattress", Vector3.Up), ("Screen_PanelCenter", Vector3.Back),
            ("Stool_Seat", Vector3.Up), ("ReceptionCounter_Top", Vector3.Up), ("RecordsDesk_Top", Vector3.Up),
            ("Trolley_TopTray", Vector3.Up), ("Partition_Panel", Vector3.Left), ("Cabinet_DoorRight", Vector3.Left),
            ("WashUnit_Body", Vector3.Left), ("Radiator_Body", Vector3.Right), ("SupplyShelf_Low", Vector3.Up),
            ("ExamChart_Panel", Vector3.Right), ("NoticeBoard_Panel", Vector3.Back), ("CoatHookRail", Vector3.Back)
        };
        foreach (var (suffix, localNormal) in probes)
        {
            var mesh = Source("FapInterior" + suffix + "_LOD0");
            var bounds = LocalBounds(mesh);
            var normal = (clinic.GlobalBasis * localNormal).Normalized();
            var centre = clinic.ToGlobal(bounds.GetCenter());
            var half = bounds.Size.Dot(localNormal.Abs()) * .5f;
            var rendered = RenderedHit(mesh, centre + normal * (half + .15f), centre - normal * (half + .15f), out var visible, out var surfaceNormal);
            var error = -1f;
            string? actualOwner = null;
            if (rendered)
            {
                using var ray = PhysicsRayQueryParameters3D.Create(visible + surfaceNormal * .025f, visible - surfaceNormal * .025f, 1);
                ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                if (hit.Count > 0)
                {
                    error = visible.DistanceTo(hit["position"].AsVector3());
                    if (hit["collider"].AsGodotObject() is Node body && body.HasMeta("collisionOwner"))
                        actualOwner = body.GetMeta("collisionOwner").AsString();
                }
            }
            Check(rendered && error >= 0 && error < .025f && actualOwner == "fap-interior-furniture",
                $"FAP {suffix} visible surface has no matching furniture contact: rendered={rendered}, error={error:F3}, owner={actualOwner}.");
            _receipt.Add(new { kind = "fap-furniture-visible-contact", suffix, rendered, error, actualOwner });
        }
        foreach (var name in new[] { "WaitingBenchLeft", "WaitingBenchRight", "DocumentDesk" })
        {
            var legacy = clinic.GetNode<StaticBody3D>(name);
            Check(legacy.CollisionLayer == 0 && legacy.CollisionMask == 0, $"Invisible legacy FAP obstacle remains: {name}.");
        }
        foreach (var name in new[] { "WaitingRoomToDesk", "DeskToOfficialRecord" })
        {
            var selection = clinic.GetNode<InteractionTarget>(name);
            // Runtime hides unavailable actions on layer0. Both inactive and
            // active selection states must stay off the physical player layers.
            Check(selection.CollisionLayer is 0 or 4 && selection.CollisionMask == 0,
                $"FAP {name} still blocks the player's body: layer={selection.CollisionLayer}, mask={selection.CollisionMask}.");
            _receipt.Add(new { kind = "fap-selection-volume", name, layer = selection.CollisionLayer,
                mask = selection.CollisionMask, available = selection.IsSemanticallyAvailable() });
        }
        var desk = LocalBounds(Source("FapInteriorRecordsDesk_Top_LOD0"));
        var lampFoot = clinic.GetNode<MeshInstance3D>("DiscoveryRepairedLamp/WeightedFoot");
        var footBounds = LocalBounds(lampFoot);
        Check(Math.Abs(footBounds.Position.Y - desk.End.Y) < .004f,
            $"FAP lamp floats or sinks after desk grounding: lamp={footBounds.Position.Y:F3}, desk={desk.End.Y:F3}.");
        var supportPoint = clinic.ToGlobal(new(footBounds.GetCenter().X, desk.End.Y, footBounds.GetCenter().Z));
        using (var ray = PhysicsRayQueryParameters3D.Create(supportPoint + Vector3.Up * .015f, supportPoint - Vector3.Up * .045f, 1))
        {
            ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
            var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            Check(hit.Count > 0 && hit["position"].AsVector3().DistanceTo(supportPoint) < .008f,
                "FAP lamp has no physical tabletop immediately beneath its visible foot.");
        }
        _receipt.Add(new { kind = "fap-lamp-table-contact", foot = footBounds.Position.Y, tabletop = desk.End.Y });
        var tray = Source("FapInteriorTrolley_TopTray_LOD0");
        var trayBounds = LocalBounds(tray);
        var handle = Source("FapInteriorTrolley_Handle_LOD0");
        var handlePoints = handle.Mesh.GetFaces().Select(point => clinic.ToLocal(handle.ToGlobal(point))).Distinct().ToArray();
        var handleBottom = handlePoints.Min(point => point.Y);
        var uprightFeet = handlePoints.Where(point => point.Y < handleBottom + .0001f).ToArray();
        Check(uprightFeet.Length >= 8, "FAP trolley handle has no two complete upright feet.");
        foreach (var point in uprightFeet)
        {
            var at = clinic.ToGlobal(point);
            var supported = RenderedHit(tray, at + Vector3.Up * .20f, at - Vector3.Up * .01f, out var top, out _)
                && point.Y >= trayBounds.Position.Y - .002f && point.Y <= trayBounds.End.Y + .002f;
            Check(supported, $"FAP trolley upright foot misses its tray at {point}.");
            _receipt.Add(new { kind = "fap-trolley-upright-bearing", localFoot = Point(point), supported, tray = Point(top) });
        }
        CheckFapWashBasin(clinic, player, Source, LocalBounds);
        var towel = Source("FapInteriorWashUnit_Towel_LOD0");
        var rail = Source("FapInteriorWashUnit_TowelRail_LOD0");
        var railTop = LocalBounds(rail).End.Y;
        var foldedOnBar = towel.Mesh.GetFaces().Select(towel.ToGlobal).Distinct()
            .Where(point => Math.Abs(clinic.ToLocal(point).Y - railTop) < .0005f)
            .Count(point => RenderedHit(rail, point + Vector3.Up * .006f, point - Vector3.Up * .006f, out _, out _));
        Check(foldedOnBar >= 2, "FAP towel has no visible fold bearing on its bar.");
        var rightWall = Source("FapInteriorShell_RightWall_LOD0");
        var railRear = LocalBounds(rail).End.X;
        var wallFastenings = rail.Mesh.GetFaces().Select(rail.ToGlobal).Distinct()
            .Where(point => Math.Abs(clinic.ToLocal(point).X - railRear) < .0005f)
            .Count(point => RenderedHit(rightWall, point - clinic.GlobalBasis.X * .08f,
                point + clinic.GlobalBasis.X * .01f, out _, out _));
        Check(wallFastenings >= 8, "The FAP towel rail has no two real returns into its wall.");
        _receipt.Add(new { kind = "fap-wash-assembly-bearing", foldedOnBar, wallFastenings });
        // The old left bench blocked this empty aisle. Both legs of this route
        // use the ordinary controller after a single explicit entry fixture.
        foreach (var point in new[] { new Vector3(-3.6f, .04f, 3.8f), new Vector3(-3.6f, .04f, .8f), new Vector3(0, .04f, .8f) })
            await Walk(player, clinic.ToGlobal(point), "fap-waiting-aisle");
        await Capture(player, "fap_furniture_waiting_aisle");
        foreach (var (label, start, direction) in new[]
        {
            ("Bench", new Vector3(3.07f, .04f, 3.5f), Vector3.Forward),
            ("Screen", new Vector3(-1.40f, .04f, -.40f), Vector3.Forward),
            ("RecordsDesk", new Vector3(0, .04f, -2.35f), Vector3.Forward),
            ("Trolley", new Vector3(3.4f, .04f, -.65f), Vector3.Forward)
        })
        {
            player.ApplyZoneSpawn(clinic.ToGlobal(start), clinic.GlobalRotationDegrees.Y);
            player.SetModalOpen(false);
            await PhysicsFrames(3);
            var contacted = false;
            Aim(player, clinic.ToGlobal(start + direction * 3f));
            Input.ActionPress("move_forward");
            try
            {
                for (var frame = 0; frame < 70; frame++)
                {
                    var before = player.GlobalPosition;
                    await PhysicsFrames(1);
                    _walked += Horizontal(before, player.GlobalPosition);
                    for (var i = 0; i < player.GetSlideCollisionCount(); i++)
                        if (player.GetSlideCollision(i).GetCollider() is Node body && body.HasMeta("sourceGroup")
                            && body.GetMeta("sourceGroup").AsString() == label) contacted = true;
                }
            }
            finally { Input.ActionRelease("move_forward"); }
            Check(contacted, $"The ordinary player did not contact the visible FAP {label}.");
            _receipt.Add(new { kind = "fap-controller-furniture-contact", label, fixture = Point(start),
                end = Point(clinic.ToLocal(player.GlobalPosition)), contacted });
        }
        player.ApplyZoneSpawn(clinic.ToGlobal(new(0, .04f, 3.8f)), clinic.GlobalRotationDegrees.Y);
        player.SetModalOpen(false);
        await PhysicsFrames(3);
    }

    private void CheckFapWashBasin(Node3D clinic, FirstPersonController player,
        Func<string, MeshInstance3D> source, Func<MeshInstance3D, Aabb> localBounds)
    {
        var body = source("FapInteriorWashUnit_Body_LOD0");
        var basin = source("FapInteriorWashUnit_Basin_LOD0");
        var drain = source("FapInteriorWashUnit_BasinInset_LOD0");
        var bounds = localBounds(basin);
        var drainBounds = localBounds(drain);
        var bowlCentre = drainBounds.GetCenter();
        var from = clinic.ToGlobal(new(bowlCentre.X, bounds.End.Y + .12f, bowlCentre.Z));
        var to = clinic.ToGlobal(new(bowlCentre.X, bounds.Position.Y - .02f, bowlCentre.Z));
        var foundFloor = RenderedHit(basin, from, to, out var floor, out _);
        var depth = foundFloor ? bounds.End.Y - clinic.ToLocal(floor).Y : -1f;
        Check(foundFloor && depth > .13f && depth < .20f && bounds.End.Y is > .85f and < 1.0f,
            $"The FAP wash basin has no real human-scale bowl: rendered={foundFloor}, depth={depth:F3}, rim={bounds.End.Y:F3}.");
        Check(foundFloor && Math.Abs(drainBounds.Position.Y - clinic.ToLocal(floor).Y) < .006f
            && drainBounds.Size.X < .06f && drainBounds.Size.Z < .06f,
            "The FAP waste strainer is not seated on the actual bowl floor.");

        // The concave bowl descends into an open cupboard. Its support datum is
        // the underside of the two side rims, not the lowest point of its AABB.
        var supports = new List<object>();
        foreach (var sign in new[] { -1f, 1f })
        {
            var bearing = new Vector3(bounds.GetCenter().X, .855f, bounds.GetCenter().Z + sign * .4175f);
            var underFound = RenderedHit(basin, clinic.ToGlobal(bearing - Vector3.Up * .02f),
                clinic.ToGlobal(bearing + Vector3.Up * .02f), out var under, out _);
            var supportFound = RenderedHit(body, clinic.ToGlobal(bearing + Vector3.Up * .02f),
                clinic.ToGlobal(bearing - Vector3.Up * .03f), out var support, out _);
            var gap = underFound && supportFound ? clinic.ToLocal(under).Y - clinic.ToLocal(support).Y : float.MaxValue;
            Check(underFound && supportFound && Math.Abs(gap) < .003f,
                $"FAP basin rim {sign} has no actual cabinet bearing: underside={underFound}, body={supportFound}, gap={gap:F4}.");
            supports.Add(new { sign, underFound, supportFound, gap });
        }
        using var ray = PhysicsRayQueryParameters3D.Create(from, to, 1);
        ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        var collisionDepth = hit.Count > 0 ? bounds.End.Y - clinic.ToLocal(hit["position"].AsVector3()).Y : -1f;
        var owner = hit.Count > 0 && hit["collider"].AsGodotObject() is Node contact && contact.HasMeta("collisionOwner")
            ? contact.GetMeta("collisionOwner").AsString() : null;
        Check(collisionDepth > .13f && collisionDepth < .20f && owner == "fap-interior-furniture",
            $"The FAP bowl is capped by an invisible flat collider: depth={collisionDepth:F3}, owner={owner}.");

        var stem = source("FapInteriorWashUnit_FaucetStem_LOD0");
        var stemBounds = localBounds(stem);
        var mount = stemBounds.GetCenter();
        mount.Y = stemBounds.Position.Y;
        var mounted = RenderedHit(basin, clinic.ToGlobal(mount + Vector3.Up * .01f),
            clinic.ToGlobal(mount - Vector3.Up * .015f), out var mountTop, out _)
            && Math.Abs(clinic.ToLocal(mountTop).Y - mount.Y) < .003f;
        var spout = localBounds(source("FapInteriorWashUnit_FaucetSpout_LOD0"));
        var outlet = new Vector3(spout.Position.X + .015f, spout.Position.Y, spout.GetCenter().Z);
        var aimsIntoBowl = RenderedHit(basin, clinic.ToGlobal(outlet),
            clinic.ToGlobal(outlet - Vector3.Up * .5f), out var outletBelow, out _)
            && clinic.ToLocal(outletBelow).Y < bounds.End.Y - .025f;
        Check(mounted && aimsIntoBowl && stemBounds.Size.Y < .2f,
            "The compact FAP mixer is unsupported or points outside the bowl.");
        var lod = source("FapInteriorWashUnit_Basin_LOD1");
        var lodHasBowl = RenderedHit(lod, from, to, out var lodFloor, out _)
            && bounds.End.Y - clinic.ToLocal(lodFloor).Y > .12f;
        Check(lodHasBowl, "The FAP basin LOD closes or removes its visible bowl.");
        _receipt.Add(new { kind = "fap-wash-bowl-volume-and-support", depth, collisionDepth, owner,
            rim = bounds.End.Y, supports, mounted, aimsIntoBowl, lodHasBowl });
    }

    private void CheckSupportContact(MeshInstance3D mesh, FirstPersonController player, string label)
    {
        var faces = mesh.Mesh.GetFaces();
        var upperFaces = Enumerable.Range(0, faces.Length / 3).Select(index =>
        {
            var a = mesh.ToGlobal(faces[index * 3]);
            var b = mesh.ToGlobal(faces[index * 3 + 1]);
            var c = mesh.ToGlobal(faces[index * 3 + 2]);
            var normal = (b - a).Cross(c - a);
            return (A: a, B: b, C: c, Centre: (a + b + c) / 3f, Normal: normal.Normalized(), Area: normal.Length());
        }).Where(face => Mathf.Abs(face.Normal.Y) > .7f && face.Area > .0001f).ToArray();
        if (upperFaces.Length == 0) throw new InvalidOperationException($"No walkable visible face for {mesh.Name}.");
        // A board cut around a junction can have its bounding-box centre in the
        // removed portion. Hay can also occupy a triangle's centre: a 22cm ray
        // began inside a bale and tied its bottom against the board at the same
        // height. Start at ordinary standing height and find an exposed point
        // on a real top triangle. Only a separately verified visible cover is
        // skipped; a missing or misplaced board contact still fails immediately.
        var covered = new List<object>();
        foreach (var face in upperFaces.OrderByDescending(face => face.Centre.Y).ThenByDescending(face => face.Area))
        foreach (var weights in new[] { new Vector3(1f/3, 1f/3, 1f/3), new(.6f,.2f,.2f),
            new(.2f,.6f,.2f), new(.2f,.2f,.6f), new(.8f,.1f,.1f), new(.1f,.8f,.1f), new(.1f,.1f,.8f) })
        {
            var center = face.A * weights.X + face.B * weights.Y + face.C * weights.Z;
            var from = center + Vector3.Up * 1.75f;
            var to = center - Vector3.Up * .22f;
            var visibleHit = RenderedHit(mesh, from, to, out var visible, out _);
            using var ray = PhysicsRayQueryParameters3D.Create(from, to, 3);
            ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
            var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var error = visibleHit && hit.Count > 0 ? visible.DistanceTo(hit["position"].AsVector3()) : -1;
            var body = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as CollisionObject3D;
            var shape = body?.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
            var source = shape is null ? null : shape.GetMeta("authoredSourceMesh", shape.GetMeta("sourceMesh", "")).AsString();
            if (label == "fap-service-path" && visibleHit
                && body is CarryableProp { ItemId: "carry-gap-crate", Kind: CarryableProp.ItemKind.Crate } crate
                && shape is CollisionShape3D { Shape: BoxShape3D } crateShape)
            {
                CheckCrateCoveredFloor(mesh, crate, crateShape, player, from, to, visible,
                    hit["position"].AsVector3(), label, covered);
                return;
            }
            if (!string.IsNullOrEmpty(source) && source != mesh.GetPath().ToString()
                && GetNodeOrNull<MeshInstance3D>(source) is { } cover && cover.IsVisibleInTree()
                && RenderedHit(cover, from, to, out var coverPoint, out _)
                && visibleHit && coverPoint.Y > visible.Y + .015f
                && coverPoint.DistanceTo(hit["position"].AsVector3()) < .015f)
            {
                covered.Add(new { point = Point(visible), cover = source, contact = Point(coverPoint) });
                continue;
            }
            var owned = body is StaticBody3D && source == mesh.GetPath().ToString()
                && body.HasMeta("footstepSurface") && body.GetMeta("footstepSurface").AsString() == "wood";
            Check(owned && error >= 0 && error < .015f, $"{label}/{mesh.Name}: visible floor contact={owned}, error={error:F3}m; first={body?.GetPath()}/{shape?.Name}, source={source}.");
            _receipt.Add(new { kind = "authored-floor-contact", label, mesh = mesh.GetPath().ToString(), owned, error,
                rayFrom = Point(from), rayTo = Point(to), visiblePoint = visibleHit ? Point(visible) : null,
                firstBody = body?.GetPath().ToString(), firstShape = shape?.GetPath().ToString(), sourceMesh = source,
                firstHit = hit.Count == 0 ? null : Point(hit["position"].AsVector3()),
                firstNormal = hit.Count == 0 ? null : Point(hit["normal"].AsVector3()), coveredSamples = covered });
            return;
        }
        Check(false, $"{label}/{mesh.Name}: no independently exposed upper-triangle sample; covers={covered.Count}.");
        _receipt.Add(new { kind = "authored-floor-contact-unavailable", label, mesh = mesh.GetPath().ToString(), coveredSamples = covered });
    }

    private void CheckCrateCoveredFloor(MeshInstance3D floor, CarryableProp crate, CollisionShape3D crateShape,
        FirstPersonController player, Vector3 from, Vector3 to, Vector3 floorPoint, Vector3 firstPoint,
        string label, List<object> previousCovers)
    {
        // CarryableProp owns a body envelope and separate visible lid planks;
        // unlike static kit contacts it does not use authoredSourceMesh. Prove
        // that actual cover before excluding it from a second diagnostic ray.
        var box = (BoxShape3D)crateShape.Shape;
        var half = box.Size * .5f;
        var visuals = Descendants(crate).OfType<MeshInstance3D>()
            .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()).ToArray();
        MeshInstance3D? cover = null;
        var coverPoint = Vector3.Zero;
        var nearest = float.PositiveInfinity;
        foreach (var visual in visuals)
            if (RenderedHit(visual, from, to, out var point, out _) && from.DistanceSquaredTo(point) < nearest)
            { nearest = from.DistanceSquaredTo(point); cover = visual; coverPoint = point; }
        var bounds = visuals.Length == 0 ? new Aabb() : visuals
            .Select(mesh => (crateShape.GlobalTransform.AffineInverse() * mesh.GlobalTransform) * mesh.Mesh.GetAabb())
            .Aggregate((a, b) => a.Merge(b));
        var envelopeError = Math.Max(bounds.Position.DistanceTo(-half), bounds.End.DistanceTo(half));
        var bodyMatches = !crateShape.Disabled && crateShape.GetParent() == crate
            && crate.IsVisibleInTree() && crate.State is CarryableProp.CarryState.World or CarryableProp.CarryState.Placed
            && box.Size.DistanceTo(crate.Size) < .0001f && envelopeError < .015f;
        var coverMatches = cover is not null && coverPoint.Y > floorPoint.Y + .015f
            && coverPoint.DistanceTo(firstPoint) < .015f;

        // Clip every actual board face to the physical box footprint. Checking
        // its lowest box plane against all resulting polygon vertices catches
        // a tilted corner penetrating the floor, beyond just the chosen ray.
        var footprint = new Vector2[] { new(-half.X, -half.Z), new(half.X, -half.Z),
            new(half.X, half.Z), new(-half.X, half.Z) };
        var faces = floor.Mesh.GetFaces();
        var clippedVertices = 0;
        var minimumClearance = float.PositiveInfinity;
        for (var i = 0; i < faces.Length; i += 3)
        {
            var a = crateShape.ToLocal(floor.ToGlobal(faces[i]));
            var b = crateShape.ToLocal(floor.ToGlobal(faces[i + 1]));
            var c = crateShape.ToLocal(floor.ToGlobal(faces[i + 2]));
            var normal = (b - a).Cross(c - a);
            if (Math.Abs(normal.Y) < .000001f) continue;
            var triangle = new Vector2[] { new(a.X, a.Z), new(b.X, b.Z), new(c.X, c.Z) };
            foreach (var polygon in Geometry2D.IntersectPolygons(triangle, footprint))
            foreach (var point in polygon)
            {
                var y = a.Y - (normal.X * (point.X - a.X) + normal.Z * (point.Y - a.Z)) / normal.Y;
                minimumClearance = Math.Min(minimumClearance, -half.Y - y);
                clippedVertices++;
            }
        }
        var supportedAbove = clippedVertices > 0 && minimumClearance is >= -.002f and < .025f;
        var provedCover = bodyMatches && coverMatches && supportedAbove;
        Check(provedCover, $"{label}/{floor.Name}: carry cover is not independently proved; body={bodyMatches}, visible={coverMatches}, no-overlap/support={supportedAbove}, envelope={envelopeError:F4}m, clearance={minimumClearance:F4}m.");
        _receipt.Add(new { kind = "floor-carry-cover-bounds", label, mesh = floor.GetPath().ToString(),
            crate = crate.GetPath().ToString(), collision = crateShape.GetPath().ToString(), crateState = crate.State.ToString(),
            boxSize = Point(box.Size), visualBoundsLow = Point(bounds.Position), visualBoundsHigh = Point(bounds.End), envelopeError,
            visibleCover = cover?.GetPath().ToString(), visibleCoverPoint = cover is null ? null : Point(coverPoint),
            firstHit = Point(firstPoint), floorPoint = Point(floorPoint), clippedVertices,
            minimumClearance = clippedVertices == 0 ? (float?)null : minimumClearance, bodyMatches, coverMatches, supportedAbove });
        if (!provedCover) return;

        using var ray = PhysicsRayQueryParameters3D.Create(from, to, 3);
        ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid(), crate.GetRid() };
        var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        var body = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as CollisionObject3D;
        var shape = body?.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
        var source = shape is null ? null : shape.GetMeta("authoredSourceMesh", shape.GetMeta("sourceMesh", "")).AsString();
        var error = hit.Count == 0 ? -1f : floorPoint.DistanceTo(hit["position"].AsVector3());
        var owned = body is StaticBody3D && source == floor.GetPath().ToString()
            && body.HasMeta("footstepSurface") && body.GetMeta("footstepSurface").AsString() == "wood";
        Check(owned && error is >= 0 and < .015f,
            $"{label}/{floor.Name}: occluded floor contact={owned}, error={error:F3}m; first below proved crate={body?.GetPath()}/{shape?.Name}, source={source}.");
        _receipt.Add(new { kind = "authored-floor-contact-occluded", label, mesh = floor.GetPath().ToString(), owned, error,
            rayFrom = Point(from), rayTo = Point(to), sourcePoint = Point(floorPoint),
            excludedProvedCover = crate.GetPath().ToString(), coverSurface = cover!.GetPath().ToString(),
            firstBodyBelowCover = body?.GetPath().ToString(), firstShapeBelowCover = shape?.GetPath().ToString(), sourceMesh = source,
            firstHitBelowCover = hit.Count == 0 ? null : Point(hit["position"].AsVector3()), previousCovers,
            scope = "hidden board's own physical contact after geometric cover proof; not a visible-floor claim; world collisions unchanged" });
    }

    private void CheckWellContacts(Act1ConnectedWorld world, FirstPersonController player)
    {
        Check(world.GetZoneInstance("village_day")!.GetNodeOrNull<Node>("VillageWellCollision") is null,
            "The arrival well still has its ungrounded 1.2m preview box.");
        var hook = world.GetNode<Node3D>("Act1CoreWorldGreybox/MainStreet/DiscoveryArrivalWellDetail/PolishedHook");
        var reference = hook.GlobalPosition + new Vector3(0, .35f, -1.5f);
        using (var ray = PhysicsRayQueryParameters3D.Create(reference, hook.GlobalPosition, 3))
        {
            ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
            var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var clear = hit.Count == 0 || hit["position"].AsVector3().DistanceTo(hook.GlobalPosition) < .04f;
            Check(clear, $"The actual rear approach to the well hook is obstructed: {(hit.Count == 0 ? "none" : (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString())}.");
            _receipt.Add(new { kind = "arrival-well-hook-ray", from = Point(reference), to = Point(hook.GlobalPosition), clear,
                collider = hit.Count == 0 ? null : (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() });
        }
        foreach (var placement in Descendants(world).OfType<Node3D>().Where(node => node.HasMeta("wellContactMemberCount")))
        {
            var shapes = Descendants(placement).OfType<CollisionShape3D>().Where(shape => shape.HasMeta("authoredSourceMesh"))
                .Select(shape => shape.GetMeta("authoredSourceMesh").AsString()).ToHashSet();
            var members = Descendants(placement).OfType<MeshInstance3D>().Where(mesh => mesh.Mesh is not null
                && mesh.Name.ToString().StartsWith("Well_YardLandmark_", StringComparison.Ordinal)
                && mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)).ToArray();
            Check(members.Length == 16 && members.All(mesh => shapes.Contains(mesh.GetPath().ToString())),
                $"{placement.Name}: the rim/posts/roof/mechanism do not each own an actual contact surface.");
            foreach (var mesh in members.Where(mesh => mesh.Name.ToString().Contains("Rim", StringComparison.Ordinal)
                         || mesh.Name.ToString().Contains("Post", StringComparison.Ordinal)))
            {
                var faces = mesh.Mesh.GetFaces();
                var a = mesh.ToGlobal(faces[0]); var b = mesh.ToGlobal(faces[1]); var c = mesh.ToGlobal(faces[2]);
                var centre = (a + b + c) / 3f;
                using var sphere = new SphereShape3D { Radius = .012f };
                using var query = new PhysicsShapeQueryParameters3D
                { Shape = sphere, Transform = new(Basis.Identity, centre), CollisionMask = 2, Margin = 0 };
                var hits = player.GetWorld3D().DirectSpaceState.IntersectShape(query, 16);
                var owned = hits.Any(hit => hit["collider"].AsGodotObject() is StaticBody3D body
                    && body.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) is Node shape
                    && shape.HasMeta("authoredSourceMesh") && shape.GetMeta("authoredSourceMesh").AsString() == mesh.GetPath().ToString());
                Check(owned, $"{placement.Name}/{mesh.Name}: visible well member has no matching physical contact.");
                _receipt.Add(new { kind = "well-member-contact", placement = placement.Name.ToString(), member = mesh.Name.ToString(), point = Point(centre), owned });
            }
        }
    }

    private async Task CheckYardHouseRefit(Act1ConnectedWorld world, FirstPersonController player)
    {
        var room = world.GetZoneInstance("house_old_pc")!;
        var layer = world.GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        CheckRelocatedYardTrees(layer, player);
        CheckAllBabaiFenceContacts(layer, player);
        var fence = Descendants(layer).OfType<MeshInstance3D>()
            .Where(mesh => mesh.Name.ToString().StartsWith("FenceBabaiS_", StringComparison.Ordinal)).ToArray();
        var active = fence.Where(mesh => mesh.Visible).ToArray();
        Check(fence.Length == 23 && active.Length == 13, "The south-fence wing did not retain its explicit supported member set.");
        var hay = world.FindChild("BabaiYardHaystack", true, false) as Node3D
            ?? throw new InvalidOperationException("Missing yard haystack.");
        foreach (var mesh in active.Concat(Descendants(hay).OfType<MeshInstance3D>()))
        {
            var intruding = mesh.Mesh.GetFaces().Select(mesh.ToGlobal).Select(room.ToLocal)
                .Count(point => Math.Abs(point.X) < 4 && Math.Abs(point.Z) < 3.5f && point.Y > -.15f && point.Y < 3.2f);
            Check(intruding == 0, $"Yard object {mesh.GetPath()} still enters the occupied house ({intruding} vertices).");
            _receipt.Add(new { kind = "yard-occupied-room-clearance", mesh = mesh.GetPath().ToString(), intrudingVertices = intruding });
        }
        foreach (var mesh in fence)
        {
            var contact = layer.GetNodeOrNull<CollisionShape3D>($"AgentB_ArchitectureCollision/Col_{mesh.Name}");
            var physical = contact is not null && !contact.Disabled;
            Check(physical == mesh.Visible, $"South-fence visibility/contact mismatch at {mesh.Name}.");
            if (!mesh.Visible || contact?.Shape is not ConcavePolygonShape3D shape) continue;
            var visible = mesh.Mesh.GetFaces(); var faces = shape.GetFaces();
            var same = visible.Length == faces.Length;
            float? error = same && visible.Length > 0 ? visible.Select((point, index) => mesh.ToGlobal(point).DistanceTo(contact.ToGlobal(faces[index]))).Max() : null;
            Check(same && error < .00002f, $"South-fence member {mesh.Name} lost its actual reshaped contact: error={error}.");
            float? supportGap = null;
            if (mesh.Name.ToString().Contains("_Post0_", StringComparison.Ordinal))
            {
                supportGap = visible.Select(mesh.ToGlobal).Min(point => point.Y - AgentBAct1HeightField.CollisionGround(point.X, point.Z));
                Check(supportGap >= -.12f && supportGap <= .005f, $"South-fence post {mesh.Name} lacks a grounded foot: {supportGap}.");
            }
            _receipt.Add(new { kind = "south-fence-visible-physical-members", mesh = mesh.Name.ToString(), physical, same, error, supportGap });
        }
        var brace = active.Single(mesh => mesh.Name == "FenceBabaiS_Brace0");
        var junction = layer.GetMeta("babaiSouthFenceWallJunction").AsVector3();
        foreach (var (postName, railName, x, height) in new[] {
                     ("FenceBabaiS_Post0_7", "FenceBabaiS_RailLow0", -36.2f, .336f),
                     ("FenceBabaiS_Post0_5", "FenceBabaiS_RailHigh0", junction.X - .12f, .756f) })
        {
            var bearingPoint = new Vector3(x, AgentBAct1HeightField.CollisionGround(x, -6.4f) + height, -6.4f);
            var from = bearingPoint + Vector3.Back * .5f; var to = bearingPoint + Vector3.Forward * .5f;
            var braceHit = RenderedHit(brace, from, to, out var onBrace, out _);
            var postHit = RenderedHit(active.Single(mesh => mesh.Name == postName), from, to, out var onPost, out _);
            var railHit = RenderedHit(active.Single(mesh => mesh.Name == railName), from, to, out var onRail, out _);
            var supported = braceHit && postHit && railHit && Math.Abs(onBrace.Z - onPost.Z) < .12f
                && Math.Abs(onBrace.Z - onRail.Z) < .06f;
            Check(supported, $"South-fence brace end has no common rail/post bearing at {bearingPoint}: brace={braceHit}, post={postHit}, rail={railHit}.");
            _receipt.Add(new { kind = "south-fence-brace-end-bearing", postName, railName, point = Point(bearingPoint),
                braceHit, postHit, railHit, supported, onBrace = Point(onBrace), onPost = Point(onPost), onRail = Point(onRail) });
        }
        var bodyMesh = hay.GetNode<MeshInstance3D>("HayPackedBody");
        var feet = bodyMesh.Mesh.GetFaces().Select(bodyMesh.ToGlobal)
            .Where(point => point.Y - AgentBAct1HeightField.CollisionGround(point.X, point.Z) < .01f).Distinct().ToArray();
        Check(feet.Length >= 18 && feet.All(point => point.Y - AgentBAct1HeightField.CollisionGround(point.X, point.Z) >= -.06f),
            "The packed haystack skirt lacks its full terrain contact.");
        var at = new Vector3(-28.2f, AgentBAct1HeightField.CollisionGround(-28.2f, 4.6f) + .04f, 4.6f);
        var fits = player.CanStandAt(at);
        Check(fits, "The relocated haystack has no clear southern observation point.");
        if (fits)
        {
            player.ApplyZoneSpawn(at, 0); player.SetModalOpen(false);
            await PhysicsFrames(6);
            AimAt(player, hay.GlobalPosition + Vector3.Up * .8f);
            await Capture(player, "hero_yard_haystack_and_boundary");
        }
    }

    private void CheckAllBabaiFenceContacts(AgentBAct1ExteriorLayer layer, FirstPersonController player)
    {
        foreach (var family in new[] { "FenceBabaiN_", "FenceBabaiW_", "FenceBabaiE_", "FenceBabaiS_" })
        {
            var members = Descendants(layer).OfType<MeshInstance3D>()
                .Where(mesh => mesh.Name.ToString().StartsWith(family, StringComparison.Ordinal) && mesh.IsVisibleInTree()).ToArray();
            _receipt.Add(new { kind = "babai-fence-family-coverage", family, visibleMembers = members.Length,
                status = members.Length == 0 ? "not-mounted" : "each-visible-member-tested" });
            foreach (var mesh in members)
            {
                var contact = layer.GetNodeOrNull<CollisionShape3D>($"AgentB_ArchitectureCollision/Col_{mesh.Name}");
                var enabled = contact is not null && !contact.Disabled && contact.GetParent() is StaticBody3D body
                    && (body.CollisionLayer & 1) != 0;
                Check(enabled, $"Visible yard fence {mesh.GetPath()} has no active corresponding contact.");
                if (!enabled || contact?.Shape is not ConcavePolygonShape3D shape) continue;
                var visible = mesh.Mesh.GetFaces(); var physics = shape.GetFaces();
                var sameCount = visible.Length == physics.Length;
                float? error = sameCount && visible.Length > 0 ? Enumerable.Range(0, visible.Length).Max(index =>
                    mesh.ToGlobal(visible[index]).DistanceTo(contact.ToGlobal(physics[index]))) : null;
                Check(sameCount && error < .00002f, $"Yard fence {mesh.Name} contact does not match its actual mesh: error={error}.");
                var hitsOwnSurface = false;
                foreach (var index in Enumerable.Range(0, visible.Length / 3)
                             .OrderByDescending(i => (visible[i * 3 + 1] - visible[i * 3]).Cross(visible[i * 3 + 2] - visible[i * 3]).LengthSquared()).Take(12))
                {
                    var a = mesh.ToGlobal(visible[index * 3]); var b = mesh.ToGlobal(visible[index * 3 + 1]); var c = mesh.ToGlobal(visible[index * 3 + 2]);
                    var center = (a + b + c) / 3; var normal = (b - a).Cross(c - a).Normalized();
                    foreach (var sign in new[] { -1, 1 })
                    {
                        using var ray = PhysicsRayQueryParameters3D.Create(center + normal * .08f * sign, center - normal * .08f * sign, 1);
                        ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
                        var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                        if (hit.Count > 0 && hit["collider"].AsGodotObject() is StaticBody3D owner
                            && owner.ShapeOwnerGetOwner(owner.ShapeFindOwner(hit["shape"].AsInt32())) == contact)
                        { hitsOwnSurface = true; break; }
                    }
                    if (hitsOwnSurface) break;
                }
                Check(hitsOwnSurface, $"Yard fence {mesh.Name} is registered but its visible surface cannot be hit in physics.");
                _receipt.Add(new { kind = "babai-fence-visible-physical-contact", family, mesh = mesh.GetPath().ToString(),
                    enabled, sameCount, maximumError = error, hitsOwnSurface });
            }
        }
    }

    private void CheckRelocatedYardTrees(AgentBAct1ExteriorLayer layer, FirstPersonController player)
    {
        Check(layer.GetMeta("yardWorkTreeRelocations", 0).AsInt32() == 2,
            "The two trees through the yard repair corner were not resolved by the planting plan.");
        var trees = Descendants(layer).OfType<Node3D>().Where(node => node.HasMeta("plantPosition")
            && node.GetChildren().OfType<MeshInstance3D>().Any()).ToArray();
        var stems = Descendants(layer).OfType<CollisionShape3D>().Where(node => node.HasMeta("geometryOwner")).ToArray();
        foreach (var old in new[] { new Vector2(-30.183186f, 1.480051f), new Vector2(-30.125542f, 2.240947f) })
            Check(!trees.Any(tree => new Vector2(tree.GlobalPosition.X, tree.GlobalPosition.Z).DistanceTo(old) < .001f),
                $"A visible tree remains inside the yard repair workspace at {old}.");
        foreach (var at in new[] { new Vector2(-29, 6.4f), new Vector2(-33, 8.2f) })
        {
            var tree = trees.SingleOrDefault(node => new Vector2(node.GlobalPosition.X, node.GlobalPosition.Z).DistanceTo(at) < .001f);
            Check(tree is not null, $"Relocated yard tree is absent from its real new root at {at}.");
            if (tree is null) continue;
            var stem = stems.SingleOrDefault(node => node.GetMeta("geometryOwner").AsString() == tree.GetPath().ToString());
            var lods = tree.GetChildren().OfType<MeshInstance3D>().ToArray();
            var paired = stem is not null && !stem.Disabled && stem.GlobalPosition.DistanceTo(tree.GlobalPosition) < .0001f;
            Check(paired && lods.Length == 3, $"The relocated tree at {at} lost a LOD or left its physical stem behind.");
            if (!paired || stem is null) continue;
            var visual = lods.Single(mesh => mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal));
            var centre = tree.GlobalPosition + Vector3.Up * .7f;
            var from = centre + Vector3.Right * .85f; var to = centre - Vector3.Right * .85f;
            var visibleHit = RenderedHit(visual, from, to, out var visiblePoint, out _);
            using var ray = PhysicsRayQueryParameters3D.Create(from, to, 1);
            ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
            var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            var ownContact = hit.Count > 0 && hit["collider"].AsGodotObject() is CollisionObject3D body
                && body.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) == stem;
            float? error = visibleHit && hit.Count > 0 ? visiblePoint.DistanceTo(hit["position"].AsVector3()) : null;
            Check(visibleHit && ownContact && error < .015f, $"Relocated yard tree at {at} has no actual matching visible trunk contact: {error}.");
            _receipt.Add(new { kind = "yard-work-tree-relocation", root = Point(tree.GlobalPosition), tree = tree.GetPath().ToString(),
                stem = stem.GetPath().ToString(), lodCount = lods.Length, paired, visibleHit, ownContact, error,
                firstCollider = hit.Count == 0 ? null : (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() });
        }
    }

    private void CheckPublishedTerrainBeforeMovement(Act1ConnectedWorld world)
    {
        var layer = world.GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        var terrain = Descendants(layer.GetNode<Node3D>("AgentB_TerrainRoadKit")).OfType<MeshInstance3D>()
            .Single(mesh => mesh.Name == "Terrain_Main");
        var contact = layer.GetNode<CollisionShape3D>("AgentB_TerrainCollision/AgentB_TerrainFaces");
        var physical = ((ConcavePolygonShape3D)contact.Shape).GetFaces();
        var visible = terrain.Mesh.GetFaces();
        var sameCount = physical.Length == visible.Length;
        float? maximumError = sameCount && visible.Length > 0 ? Enumerable.Range(0, visible.Length).Max(index =>
            terrain.ToGlobal(visible[index]).DistanceTo(contact.ToGlobal(physical[index]))) : null;
        Check(sameCount && maximumError < .00002f,
            $"Published terrain diverges before snow refinement: visualVertices={visible.Length}, physicalVertices={physical.Length}, maximumError={maximumError}.");
        _receipt.Add(new { kind = "occupied-terrain-publication-before-movement", sameCount, maximumError,
            visualVertices = visible.Length, physicalVertices = physical.Length });
        var raw = TerrainRawFaces(terrain.Mesh);
        var rawWorld = _publishedTerrainRaw = raw.Select(terrain.ToGlobal).ToArray();
        var physicsWorld = _publishedTerrainPhysics = physical.Select(contact.ToGlobal).ToArray();
        var rawSameCount = raw.Length == physical.Length;
        float? rawPhysicsError = rawSameCount && raw.Length > 0 ? Enumerable.Range(0, raw.Length).Max(index =>
            rawWorld[index].DistanceTo(physicsWorld[index])) : null;
        var nativeSnapped = raw.Select(EngineTerrainSnap).ToArray();
        var managedSnapped = raw.Select(point => point.Snapped(Vector3.One * .0001f)).ToArray();
        float? engineSnapError = rawSameCount && raw.Length > 0 ? Enumerable.Range(0, raw.Length).Max(index =>
            terrain.ToGlobal(nativeSnapped[index]).DistanceTo(physicsWorld[index])) : null;
        float? managedSnapError = rawSameCount && raw.Length > 0 ? Enumerable.Range(0, raw.Length).Max(index =>
            terrain.ToGlobal(managedSnapped[index]).DistanceTo(physicsWorld[index])) : null;
        var snapDifferences = rawSameCount ? Enumerable.Range(0, raw.Length)
            .Where(index => nativeSnapped[index] != managedSnapped[index]
                || terrain.ToGlobal(nativeSnapped[index]).DistanceTo(physicsWorld[index]) >= .00002f)
            .OrderByDescending(index => terrain.ToGlobal(managedSnapped[index]).DistanceTo(physicsWorld[index]))
            .Take(12).Select(index => (object)new { faceVertexIndex = index, raw = Point(raw[index]),
                published = Point(terrain.ToLocal(physicsWorld[index])), nativeExpected = Point(nativeSnapped[index]),
                managedFloatExpected = Point(managedSnapped[index]),
                nativeError = terrain.ToGlobal(nativeSnapped[index]).DistanceTo(physicsWorld[index]),
                managedFloatError = terrain.ToGlobal(managedSnapped[index]).DistanceTo(physicsWorld[index]) }).ToArray()
            : Array.Empty<object>();
        Check(rawSameCount && rawPhysicsError < .0001f && engineSnapError < .00002f,
            $"The actual terrain vertex buffer differs from its published collision beyond the engine snap: rawError={rawPhysicsError}, snapError={engineSnapError}.");
        _receipt.Add(new { kind = "occupied-terrain-raw-buffer-to-physics", rawVertices = raw.Length,
            physicalVertices = physical.Length, rawSameCount, rawPhysicsError, engineSnapError, managedSnapError, snapDifferences,
            engineCommit = "a13da4feb8d8aefc283c3763d33a2f170a18d541", triangleMeshSnapMeters = .0001f,
            nativeFormula = "float(floor(double(component) / double(float(0.0001)) + 0.5) * double(float(0.0001)))" });
    }

    private static Vector3 EngineTerrainSnap(Vector3 point)
    {
        // TriangleMesh::create calls Vector3::snappedf(real_t). In the installed
        // float build, that converts the float component and step to doubles for
        // Math::snapped(double, double), then assigns back to float. C# Snapped
        // instead rounds a float quotient; near a half step it selects a different
        // grid point. Match the native publication without changing any tolerance.
        // Godot a13da4f: triangle_mesh.cpp:137, vector3.cpp:60-69,
        // math_funcs.cpp:121-125; compare GodotSharp/Core/Mathf.cs:1699-1706.
        const double step = (double).0001f;
        return new((float)(Math.Floor((double)point.X / step + .5) * step),
            (float)(Math.Floor((double)point.Y / step + .5) * step),
            (float)(Math.Floor((double)point.Z / step + .5) * step));
    }

    private static Vector3[] TerrainRawFaces(Mesh mesh)
    {
        if (mesh is not ArrayMesh array) throw new InvalidOperationException("Terrain vertex-buffer proof requires its real ArrayMesh.");
        var faces = new List<Vector3>();
        for (var surface = 0; surface < array.GetSurfaceCount(); surface++)
        {
            if (array.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
                throw new InvalidOperationException("The terrain surface is not an indexed triangle list.");
            var attributes = array.SurfaceGetArrays(surface);
            var vertices = attributes[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indices = attributes[(int)Mesh.ArrayType.Index].AsInt32Array();
            if (indices.Length == 0) indices = Enumerable.Range(0, vertices.Length).ToArray();
            if (indices.Length % 3 != 0) throw new InvalidOperationException("The terrain surface has an incomplete triangle.");
            faces.AddRange(indices.Select(index => vertices[index]));
        }
        return faces.ToArray();
    }

    private static double TerrainSignedArea(Vector3 a, Vector3 b, Vector3 c) =>
        (((double)b.X - a.X) * ((double)c.Z - a.Z) - ((double)b.Z - a.Z) * ((double)c.X - a.X)) * .5;

    private static bool TerrainPointInside(Vector3 point, Vector3 a, Vector3 b, Vector3 c, out double heightError)
    {
        var denominator = ((double)b.Z - c.Z) * ((double)a.X - c.X) + ((double)c.X - b.X) * ((double)a.Z - c.Z);
        if (Math.Abs(denominator) < 1e-12) { heightError = 0; return false; }
        var u = (((double)b.Z - c.Z) * ((double)point.X - c.X) + ((double)c.X - b.X) * ((double)point.Z - c.Z)) / denominator;
        var v = (((double)c.Z - a.Z) * ((double)point.X - c.X) + ((double)a.X - c.X) * ((double)point.Z - c.Z)) / denominator;
        var w = 1 - u - v;
        heightError = Math.Abs(point.Y - (a.Y * u + b.Y * v + c.Y * w));
        return u >= -.00002 && v >= -.00002 && w >= -.00002 && heightError < .0002;
    }

    private static (double[] Weights, double OutsideEdgeMeters, double HeightError) TerrainPointResidual(
        Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        var denominator = ((double)b.Z - c.Z) * ((double)a.X - c.X) + ((double)c.X - b.X) * ((double)a.Z - c.Z);
        if (Math.Abs(denominator) < 1e-12) return (Array.Empty<double>(), double.MaxValue, double.MaxValue);
        var u = (((double)b.Z - c.Z) * ((double)point.X - c.X) + ((double)c.X - b.X) * ((double)point.Z - c.Z)) / denominator;
        var v = (((double)c.Z - a.Z) * ((double)point.X - c.X) + ((double)a.X - c.X) * ((double)point.Z - c.Z)) / denominator;
        var w = 1 - u - v;
        static double EdgeLength(Vector3 x, Vector3 y) => Math.Sqrt(((double)x.X - y.X) * ((double)x.X - y.X)
            + ((double)x.Z - y.Z) * ((double)x.Z - y.Z));
        var outside = Math.Max(0, Math.Max(-u * Math.Abs(denominator) / EdgeLength(b, c),
            Math.Max(-v * Math.Abs(denominator) / EdgeLength(c, a), -w * Math.Abs(denominator) / EdgeLength(a, b))));
        return (new[] { u, v, w }, outside, Math.Abs(point.Y - (a.Y * u + b.Y * v + c.Y * w)));
    }

    private void CheckRefinedTerrainCoverage(Vector3[] physical, Vector3[] visible)
    {
        // SnowTrampleField keeps the same support surface and subdivides its
        // triangles for shader displacement. Check their containment, support
        // plane, winding and summed area for every original face. This is paired
        // with the reviewed deterministic indexed-grid subdivision in that
        // owner; it is not a general proof against compensating overlap/hole
        // combinations within one original face.
        const float cellSize = 8;
        var buckets = new Dictionary<(int X, int Z), List<int>>();
        var covered = new double[physical.Length / 3];
        for (var i = 0; i < physical.Length; i += 3)
        {
            var minimum = physical[i].Min(physical[i + 1]).Min(physical[i + 2]);
            var maximum = physical[i].Max(physical[i + 1]).Max(physical[i + 2]);
            for (var x = Mathf.FloorToInt(minimum.X / cellSize); x <= Mathf.FloorToInt(maximum.X / cellSize); x++)
            for (var z = Mathf.FloorToInt(minimum.Z / cellSize); z <= Mathf.FloorToInt(maximum.Z / cellSize); z++)
            {
                if (!buckets.TryGetValue((x, z), out var entries)) buckets[(x, z)] = entries = new List<int>();
                entries.Add(i);
            }
        }
        var unmatched = 0; var reversed = 0; var collapsed = 0; var maximumHeightError = 0d;
        var unmatchedDetails = new List<object>();
        for (var i = 0; i < visible.Length; i += 3)
        {
            var a = visible[i]; var b = visible[i + 1]; var c = visible[i + 2];
            var area = TerrainSignedArea(a, b, c);
            if (Math.Abs(area) < 1e-12) { collapsed++; continue; }
            var center = (a + b + c) / 3;
            var found = false;
            if (buckets.TryGetValue((Mathf.FloorToInt(center.X / cellSize), Mathf.FloorToInt(center.Z / cellSize)), out var candidates))
            foreach (var index in candidates)
            {
                var pa = physical[index]; var pb = physical[index + 1]; var pc = physical[index + 2];
                if (!TerrainPointInside(a, pa, pb, pc, out var ea) || !TerrainPointInside(b, pa, pb, pc, out var eb)
                    || !TerrainPointInside(c, pa, pb, pc, out var ec)) continue;
                if (Math.Sign(area) != Math.Sign(TerrainSignedArea(pa, pb, pc))) reversed++;
                covered[index / 3] += Math.Abs(area);
                maximumHeightError = Math.Max(maximumHeightError, Math.Max(ea, Math.Max(eb, ec)));
                found = true; break;
            }
            if (!found)
            {
                unmatched++;
                if (unmatchedDetails.Count < 12 && candidates is not null)
                {
                    var ranked = candidates.Where(index => Math.Abs(TerrainSignedArea(physical[index], physical[index + 1], physical[index + 2])) >= 1e-12)
                        .Select(index => (Index: index, Residuals: new[] { a, b, c }.Select(point =>
                            TerrainPointResidual(point, physical[index], physical[index + 1], physical[index + 2])).ToArray()))
                        .OrderBy(candidate => candidate.Residuals.Max(residual => Math.Max(residual.OutsideEdgeMeters, residual.HeightError))).Take(2).ToArray();
                    unmatchedDetails.Add(new { visibleIndex = i / 3, vertices = new[] { Point(a), Point(b), Point(c) }, area,
                        closest = ranked.Select(candidate => new { physicalIndex = candidate.Index / 3,
                            vertices = new[] { Point(physical[candidate.Index]), Point(physical[candidate.Index + 1]), Point(physical[candidate.Index + 2]) },
                            residuals = candidate.Residuals.Select(residual => new { weights = residual.Weights,
                                outsideEdgeMeters = residual.OutsideEdgeMeters, heightError = residual.HeightError }).ToArray() }).ToArray() });
                }
            }
        }
        var undercovered = 0; var overcovered = 0; var maximumAreaError = 0d;
        var areaDetails = new List<object>();
        for (var i = 0; i < covered.Length; i++)
        {
            var expected = Math.Abs(TerrainSignedArea(physical[i * 3], physical[i * 3 + 1], physical[i * 3 + 2]));
            var difference = covered[i] - expected;
            maximumAreaError = Math.Max(maximumAreaError, Math.Abs(difference));
            var tolerance = Math.Max(.00005, expected * .00001);
            if (difference < -tolerance) undercovered++;
            if (difference > tolerance) overcovered++;
            if (Math.Abs(difference) > tolerance && areaDetails.Count < 16)
                areaDetails.Add(new { physicalIndex = i,
                    vertices = new[] { Point(physical[i * 3]), Point(physical[i * 3 + 1]), Point(physical[i * 3 + 2]) },
                    expectedArea = expected, coveredArea = covered[i], difference, tolerance });
        }
        Check(unmatched == 0 && reversed == 0 && collapsed == 0 && undercovered == 0 && overcovered == 0,
            $"Snow-refined terrain changed the support surface: unmatched={unmatched}, reversed={reversed}, collapsed={collapsed}, undercovered={undercovered}, overcovered={overcovered}, maximumHeightError={maximumHeightError}, maximumAreaError={maximumAreaError}.");
        _receipt.Add(new { kind = "occupied-terrain-refined-surface-coverage", physicalTriangles = covered.Length,
            visibleTriangles = visible.Length / 3, unmatched, reversed, collapsed, undercovered, overcovered, maximumHeightError, maximumAreaError,
            unmatchedDetails, areaDetails,
            measuredFrom = "original and refined SurfaceGetArrays vertex/index buffers, without TriangleMesh snapping",
            scope = "CPU per-face containment, plane, winding and area plus source-reviewed deterministic subdivision; original physics is separately checked unchanged; bounded snow shader displacement is presentation-only" });
    }

    private void CheckOccupiedHouseTerrain(Act1ConnectedWorld world, Node3D room, Node3D facade)
    {
        var layer = world.GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        var terrain = Descendants(layer.GetNode<Node3D>("AgentB_TerrainRoadKit")).OfType<MeshInstance3D>()
            .Single(mesh => mesh.Name == "Terrain_Main");
        var contact = layer.GetNode<CollisionShape3D>("AgentB_TerrainCollision/AgentB_TerrainFaces");
        var physical = ((ConcavePolygonShape3D)contact.Shape).GetFaces();
        var visible = terrain.Mesh.GetFaces();
        var sameCount = physical.Length == visible.Length;
        var comparable = sameCount && visible.Length > 0;
        float? maximumError = comparable ? Enumerable.Range(0, visible.Length).Max(index =>
            terrain.ToGlobal(visible[index]).DistanceTo(contact.ToGlobal(physical[index]))) : null;
        var comparisonUnavailable = !sameCount ? "visual and physical face arrays have different lengths"
            : visible.Length == 0 ? "terrain has no faces" : null;
        var originalRaw = _publishedTerrainRaw ?? throw new InvalidOperationException("Missing pre-movement raw terrain baseline.");
        var originalPhysics = _publishedTerrainPhysics ?? throw new InvalidOperationException("Missing pre-movement terrain physics baseline.");
        var currentPhysics = physical.Select(contact.ToGlobal).ToArray();
        var physicsCountUnchanged = currentPhysics.Length == originalPhysics.Length;
        float? physicsChange = physicsCountUnchanged && currentPhysics.Length > 0 ? Enumerable.Range(0, currentPhysics.Length)
            .Max(index => currentPhysics[index].DistanceTo(originalPhysics[index])) : null;
        Check(physicsCountUnchanged && physicsChange < .000002f,
            $"Snow refinement modified the original terrain collider: sameCount={physicsCountUnchanged}, error={physicsChange}.");
        _receipt.Add(new { kind = "occupied-terrain-physics-after-refinement", physicsCountUnchanged, physicsChange });
        // Godot's Mesh.GetFaces delegates to TriangleMesh, whose create() snaps
        // every vertex to 0.0001m. Snapping the dense mesh again creates different
        // edge/plane errors from the coarse mesh. Compare the real GPU buffers;
        // the original raw-to-collision snap bound is checked before movement.
        CheckRefinedTerrainCoverage(originalRaw, TerrainRawFaces(terrain.Mesh).Select(terrain.ToGlobal).ToArray());
        Check(room.GetMeta("exteriorTerrainFootprintExcluded", false).AsBool(), "The occupied house has no terrain footprint cut.");
        var groundLayers = Descendants(layer.GetNode<Node3D>("AgentB_TerrainRoadKit")).OfType<MeshInstance3D>()
            .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree())
            .Select(mesh => (Mesh: mesh, Bounds: mesh.GlobalTransform * mesh.Mesh.GetAabb())).ToArray();
        var apron = groundLayers.Single(value => value.Mesh.Name == "Apron_BabaiYard").Mesh;
        CheckOccupiedApronExterior(room, apron);
        var timberFloor = room.GetNode<MeshInstance3D>("Floor/Visible");
        var foundations = Descendants(facade).OfType<MeshInstance3D>().Where(mesh => mesh.Name.ToString()
            is "HeroHouse_Foundation_LOD0" or "HeroHouse_FootingCap_LOD0").ToArray();
        var floorSamples = new List<Vector2>();
        foreach (var x in new[] { -3.8f, 0, 3.8f })
        foreach (var z in new[] { -3.3f, 0, 3.3f }) floorSamples.Add(new(x, z));
        // Exact local region whose real player-view rays found the white apron.
        foreach (var x in new[] { -3.80f, -3.50f, -3.20f })
        foreach (var z in new[] { -3.15f, -2.75f, -2.35f, -1.95f }) floorSamples.Add(new(x, z));
        foreach (var sample in floorSamples)
        {
            var (x, z) = (sample.X, sample.Y);
            var at = room.ToGlobal(new(x, 0, z));
            var from = at + Vector3.Up * 2; var to = at - Vector3.Up * 2;
            var terrainInside = RenderedHit(terrain, from, to, out _, out _);
            var apronInside = RenderedHit(apron, from, to, out _, out _);
            var capInside = foundations.Any(mesh => RenderedHit(mesh, from, to, out _, out _));
            var floorFound = RenderedHit(timberFloor, from, to, out var woodPoint, out _);
            var coveringLayers = new List<string>();
            foreach (var (mesh, bounds) in groundLayers)
            {
                if (at.X < bounds.Position.X || at.X > bounds.End.X || at.Z < bounds.Position.Z || at.Z > bounds.End.Z) continue;
                if (RenderedHit(mesh, from, to, out var groundPoint, out _) && (!floorFound || groundPoint.Y >= woodPoint.Y - .0002f))
                    coveringLayers.Add(mesh.GetPath().ToString());
            }
            Check(floorFound && !terrainInside && !apronInside && !capInside && coveringLayers.Count == 0,
                $"Occupied floor local({x},{z}): wood={floorFound}, terrain={terrainInside}, apron={apronInside}, stone={capInside}, covering={string.Join('|', coveringLayers)}.");
            _receipt.Add(new { kind = "occupied-floor-exterior-clearance", local = new[] { x, z },
                floorFound, terrainInside, apronInside, capInside, coveringLayers,
                firstGroundSurface = floorFound && coveringLayers.Count == 0 ? timberFloor.GetPath().ToString() : null,
                scope = "all visible terrain-road kit layers plus foundation; furniture is checked separately" });
        }
        foreach (var local in new[] { new Vector3(-4.35f,0,-3.2f), new Vector3(-4.35f,0,0), new Vector3(-4.35f,0,3.2f),
                     new Vector3(4.35f,0,-3.2f), new Vector3(4.35f,0,0), new Vector3(4.35f,0,3.2f),
                     new Vector3(0,0,-3.85f), new Vector3(0,0,3.85f) })
        {
            var at = room.ToGlobal(local);
            var expected = AgentBAct1HeightField.CollisionGround(at.X, at.Z);
            var found = RenderedHit(terrain, new(at.X, expected + 1, at.Z), new(at.X, expected - 1, at.Z), out var actual, out _);
            var error = found ? Math.Abs(actual.Y - expected) : -1f;
            Check(found && error < .0001f, $"The terrain cut damaged exterior ground at {at}: error={error:F6}m.");
            _receipt.Add(new { kind = "occupied-footprint-exterior-preserved", point = Point(at), expected, actual = Point(actual), found, error });
        }
        _receipt.Add(new { kind = "occupied-terrain-shared-geometry", sameCount, maximumError, comparisonUnavailable,
            visualVertices = visible.Length, physicalVertices = physical.Length, triangles = physical.Length / 3,
            clippedVertices = contact.GetMeta("terrainCutInputVertices", -1).AsInt32(),
            publishedVertices = contact.GetMeta("terrainCutPublishedVertices", -1).AsInt32(),
            shapeVerticesAfterSet = contact.GetMeta("terrainCutPhysicsVertices", -1).AsInt32() });
    }

    private void CheckOccupiedApronExterior(Node3D room, MeshInstance3D apron)
    {
        var original = apron.GetMeta("occupiedRoomOriginalMesh", default).AsGodotObject() as ArrayMesh
            ?? throw new InvalidOperationException("Missing pre-cut apron resource for an independent exterior comparison.");
        var surfaceCountUnchanged = original.GetSurfaceCount() == apron.Mesh.GetSurfaceCount();
        var materialUnchanged = surfaceCountUnchanged && Enumerable.Range(0, original.GetSurfaceCount()).All(index =>
            original.SurfaceGetMaterial(index)?.GetInstanceId() == apron.Mesh.SurfaceGetMaterial(index)?.GetInstanceId());
        var uvPresenceUnchanged = surfaceCountUnchanged && Enumerable.Range(0, original.GetSurfaceCount()).All(index =>
            (original.SurfaceGetArrays(index)[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil)
            == (apron.Mesh.SurfaceGetArrays(index)[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil));
        Check(surfaceCountUnchanged && materialUnchanged && uvPresenceUnchanged,
            "The occupied apron cut changed its material surface index or added/removed its texture coordinates.");
        var bounds = apron.GlobalTransform * original.GetAabb();
        var samples = 0; var missing = 0; var maximumError = 0f;
        for (var ix = 0; ix <= 24; ix++)
        for (var iz = 0; iz <= 24; iz++)
        {
            var at = new Vector3(Mathf.Lerp(bounds.Position.X, bounds.End.X, ix / 24f), 0,
                Mathf.Lerp(bounds.Position.Z, bounds.End.Z, iz / 24f));
            var local = room.ToLocal(at);
            if (Math.Abs(local.X) < 4.01f && Math.Abs(local.Z) < 3.51f) continue;
            var from = at with { Y = bounds.End.Y + .5f };
            var to = at with { Y = bounds.Position.Y - .5f };
            if (!RenderedHit(original, apron.GlobalTransform, from, to, out var before, out _)) continue;
            samples++;
            if (!RenderedHit(apron, from, to, out var after, out _)) { missing++; continue; }
            maximumError = Math.Max(maximumError, before.DistanceTo(after));
        }
        Check(samples > 50 && missing == 0 && maximumError < .0004f,
            $"The room apron cut damaged the exterior yard: samples={samples}, missing={missing}, error={maximumError:F6}m.");
        _receipt.Add(new { kind = "occupied-apron-exterior-preserved", samples, missing, maximumError,
            surfaceCountUnchanged, materialUnchanged, uvPresenceUnchanged,
            comparison = "actual pre-cut imported resource against the published mesh at exterior ray-grid locations" });
    }

    private void CheckHeroWallFinish(Node3D room, Node3D facade)
    {
        var shell = Descendants(facade).OfType<MeshInstance3D>().ToArray();
        foreach (var (prefix, exterior, inward) in new[]
        {
            ("FrontWall", "Street", Vector3.Forward), ("BackWall", "Rear", Vector3.Back),
            ("LeftWall", "Left", Vector3.Right), ("RightWall", "Right", Vector3.Left)
        })
        {
            var outer = shell.Single(mesh => mesh.Name == $"HeroHouse_{exterior}_Wall_LOD0");
            foreach (var segment in room.GetChildren().OfType<StaticBody3D>()
                .Where(body => body.Name.ToString().StartsWith(prefix, StringComparison.Ordinal)))
            {
                var lining = segment.GetNode<MeshInstance3D>("Visible");
                var center = segment.GlobalPosition;
                var normal = (room.GlobalBasis * inward).Normalized();
                var insideFrom = center + normal * .35f;
                var outsideFrom = center - normal * .35f;
                var insideLining = RenderedHit(lining, insideFrom, outsideFrom, out var innerFinish, out _);
                var insideShell = RenderedHit(outer, insideFrom, outsideFrom, out var innerShell, out _);
                var outsideLining = RenderedHit(lining, outsideFrom, insideFrom, out var outerFinish, out _);
                var outsideShell = RenderedHit(outer, outsideFrom, insideFrom, out var outerShell, out _);
                float? innerClearance = insideLining && insideShell ? (innerFinish - innerShell).Dot(normal) : null;
                float? outerClearance = outsideLining && outsideShell ? (outerFinish - outerShell).Dot(normal) : null;
                Check(innerClearance is > .00165f and < .00235f && outerClearance is > .00165f and < .00235f,
                    $"Hero {segment.Name} finish intersects or replaces its exterior shell: inner={innerClearance}, outer={outerClearance}.");
                _receipt.Add(new { kind = "hero-wall-finish-clearance", wall = segment.Name.ToString(),
                    innerClearance, outerClearance, measured = "actual rendered triangles from both sides; every room wall segment" });
            }
        }
    }

    private async Task CheckHeroHouse(Act1ConnectedWorld world, FirstPersonController player)
    {
        var room = world.GetZoneInstance("house_old_pc") ?? throw new InvalidOperationException("Missing hero room.");
        var facade = world.FindChild("BabaiApproachDwellingFacade", true, false) as Node3D
            ?? throw new InvalidOperationException("Missing hero facade.");
        var expected = StyleBenchmarkInteriorFactory.TransformFromFacade(facade);
        Check(room.GlobalPosition.DistanceTo(expected.Origin) < .002f
            && room.GlobalBasis.X.DistanceTo(expected.Basis.X) < .002f
            && room.GlobalBasis.Z.DistanceTo(expected.Basis.Z) < .002f, "Hero room is not mounted inside its facade.");
        Check(room.GlobalBasis.Scale.DistanceTo(Vector3.One) < .002f, "Hero room/furniture/actors inherit a house scale.");
        var floor = room.GetNode<MeshInstance3D>("Floor/Visible");
        CheckOccupiedHouseTerrain(world, room, facade);
        Check(floor.Mesh is BoxMesh floorBox && floorBox.Size.IsEqualApprox(new Vector3(8.4f, .18f, 7.4f)),
            "Hero room still uses the old 12x10 floor.");
        var shellMeshes = Descendants(facade).OfType<MeshInstance3D>().ToArray();
        CheckHeroWallFinish(room, facade);
        var windows = new[] { ("Front0", "Street_Window1"), ("Front1", "Street_Window2"),
            ("Front2", "Street_Window3"), ("Rear0", "Rear_Window0"), ("Rear1", "Rear_Window1"),
            ("Left0", "Left_Window0"), ("Right0", "Right_Window0") };
        foreach (var (interior, exterior) in windows)
        {
            var inner = room.GetNode<MeshInstance3D>($"HeroRoomWindow{interior}/Glazing/Visible");
            var outer = shellMeshes.Single(mesh => mesh.Name == $"HeroHouse_{exterior}_Glass_LOD0");
            var a = inner.GlobalTransform * inner.Mesh.GetAabb().GetCenter();
            var b = outer.GlobalTransform * outer.Mesh.GetAabb().GetCenter();
            var error = a.DistanceTo(b);
            Check(error < .006f, $"Hero {interior} glazing does not match the exterior: {error:F4}m.");
            _receipt.Add(new { kind = "hero-window-alignment", window = interior, interior = Point(a), exterior = Point(b), error });
        }
        var chimney = shellMeshes.Single(mesh => mesh.Name == "HeroHouse_ChimneyStack_LOD0");
        var chimneyCenter = chimney.GlobalTransform * chimney.Mesh.GetAabb().GetCenter();
        var stoveCenter = room.ToGlobal(StyleBenchmarkInteriorFactory.StoveAnchor);
        Check(Horizontal(chimneyCenter, stoveCenter) < .006f, "Hero stove flue misses its exterior chimney.");
        var furniture = room.GetNode<Node3D>("GeneratedHouseInteriorAct1");
        var preserved = 0;
        foreach (var mesh in Descendants(furniture).OfType<MeshInstance3D>().Where(mesh => mesh.HasMeta("heroRoomSourceTransform")))
        {
            var original = mesh.GetMeta("heroRoomSourceTransform").AsTransform3D().Basis;
            var actual = (room.GlobalTransform.AffineInverse() * mesh.GlobalTransform).Basis;
            var scaleError = new Vector3(original.X.Length(), original.Y.Length(), original.Z.Length())
                .DistanceTo(new(actual.X.Length(), actual.Y.Length(), actual.Z.Length()));
            Check(scaleError < .0005f, $"Hero furniture was resized: {mesh.Name}.");
            preserved++;
        }
        _receipt.Add(new { kind = "hero-house-contract", clearWidth = 8, clearDepth = 7,
            roomOrigin = Point(room.GlobalPosition), rotation = Point(room.GlobalRotationDegrees),
            unchangedFurnitureMembers = preserved, chimneyHorizontalError = Horizontal(chimneyCenter, stoveCenter) });
        Check(preserved > 30, "Hero house did not retain its authored furniture groups.");
        var daybedBack = Descendants(furniture).OfType<MeshInstance3D>().Single(mesh => mesh.Name == "HouseInterior_DaybedBack_LOD0");
        var daybedSeat = Descendants(furniture).OfType<MeshInstance3D>().Single(mesh => mesh.Name == "HouseInterior_DaybedCushion_LOD0");
        var backCentre = room.ToLocal(daybedBack.GlobalTransform * daybedBack.Mesh.GetAabb().GetCenter());
        var seatCentre = room.ToLocal(daybedSeat.GlobalTransform * daybedSeat.Mesh.GetAabb().GetCenter());
        Check(backCentre.X > seatCentre.X + .60f && backCentre.X < 3.90f,
            $"The daybed still presents its back to the room: back={backCentre}, seat={seatCentre}.");
        _receipt.Add(new { kind = "daybed-orientation", back = Point(backCentre), seat = Point(seatCentre),
            facesRoom = backCentre.X > seatCentre.X, artReview = "requires-current-game-frame" });

        world.SetActiveLogicalZone("house_old_pc");
        Check(world.TryGetWorldSpawn("house_old_pc", "entry", out var entry), "No hero entry spawn.");
        Check(entry.Position.DistanceTo(room.ToGlobal(StyleBenchmarkInteriorFactory.Entry)) < .002f,
            "Hero spawn still uses the old origin-only mapping.");
        player.ApplyZoneSpawn(entry.Position, entry.YawDegrees);
        player.SetModalOpen(false);
        await PhysicsFrames(6);
        Check(room.ToLocal(player.GlobalPosition).Y is >= -.04f and <= .10f, "Hero entry does not settle onto its floor.");
        foreach (var body in Descendants(world).OfType<StaticBody3D>().Where(body => body.Name == "AuthoredKitCollisionProxy"))
            Check(body.CollisionLayer == 0, $"An exterior shell remains physical inside the house: {body.GetPath()}.");
        foreach (var body in Descendants(world).OfType<StaticBody3D>()
            .Where(body => body.HasMeta("collisionOwner") && body.GetMeta("collisionOwner").AsString()
                is "act1-exterior-terrain" or "act1-exterior-architecture"))
            Check(body.CollisionLayer == 0, $"Outdoor terrain/architecture remains physical inside the house: {body.GetPath()}.");
        var exteriorWindowPanels = shellMeshes.Where(mesh => mesh.HasMeta("heroWindowExteriorOriginalVisibility")).ToArray();
        Check(exteriorWindowPanels.Length == 14 && exteriorWindowPanels.All(mesh => !mesh.Visible),
            "Exterior window backings still obstruct the real view from the seven room windows.");
        Check(world.GetNode<Node3D>("Act1CoreWorldGreybox").IsVisibleInTree(),
            "The actual village disappeared from the house window view.");
        foreach (var (interior, _) in windows)
        {
            var window = room.GetNode<Node3D>($"HeroRoomWindow{interior}");
            var glazing = window.GetNode<StaticBody3D>("Glazing");
            var visible = glazing.GetNode<MeshInstance3D>("Visible");
            Check(visible.Mesh is QuadMesh && visible.MaterialOverride is ShaderMaterial
                && visible.HasMeta("windowSurface"), $"Hero {interior} retained an opaque placeholder pane.");
            using var ray = PhysicsRayQueryParameters3D.Create(window.ToGlobal(new(.22f,.10f,.11f)),
                window.ToGlobal(new(.22f,.10f,.01f)), 1);
            var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            Check(hit.Count > 0 && hit["collider"].AsGodotObject() == glazing,
                $"Hero {interior} transparent glazing lost its physical closed-window contact.");
            _receipt.Add(new { kind = "hero-window-presentation", window = interior, transparentSheet = visible.Mesh is QuadMesh,
                closedContact = hit.Count > 0 && hit["collider"].AsGodotObject() == glazing,
                exteriorBackingsSuppressed = true, artReview = "not-proven-by-this-check" });
        }
        Aim(player, room.ToGlobal(new(.1f, 0, -.7f)));
        await Capture(player, "hero_house_entry");

        var waypoints = new (string Label, Vector3 Local)[]
        {
            ("entrance-lane", new(-1.95f, .04f, 1.25f)),
            ("mansur-approach", new(.85f, .04f, 1.80f)),
            ("room-middle", new(.1f, .04f, -.70f)),
            ("pc-approach", new(.1f, .04f, -1.45f)),
            ("rinat-approach", new(1.50f, .04f, -1.45f)),
            ("pc-return", new(.1f, .04f, -.70f)),
            ("gulsina-approach", new(-1.75f, .04f, .65f)),
            ("stove-front", new(-1.90f, .04f, -1.15f)),
            ("photo-approach-turn", new(-2.55f, .04f, -1.60f)),
            ("photo-approach", new(-2.75f, .04f, -2.50f)),
            ("return-from-photo", new(-2.55f, .04f, -1.60f)),
            ("return-to-room", new(-1.90f, .04f, -1.15f)),
            ("room-return", new(.1f, .04f, .3f)),
            ("entry-return", new(-1.95f, .04f, 1.25f)),
            ("exit-approach", StyleBenchmarkInteriorFactory.Entry)
        };
        foreach (var (label, local) in waypoints)
        {
            await Walk(player, room.ToGlobal(local), "hero-house/" + label);
            _receipt.Add(new { kind = "hero-room-controller-approach", label, reached = Horizontal(player.GlobalPosition, room.ToGlobal(local)) < .18f,
                actualLocalPosition = Point(room.ToLocal(player.GlobalPosition)) });
            if (label == "pc-approach")
            { Aim(player, room.ToGlobal(StyleBenchmarkInteriorFactory.Entry)); await Capture(player, "hero_house_reverse"); }
            if (label == "room-middle")
                foreach (var (wall, target) in new[]
                {
                    ("front", new Vector3(0, 1.3f, 3.5f)), ("back", new Vector3(0, 1.3f, -3.5f)),
                    ("left", new Vector3(-4, 1.3f, 0)), ("right", new Vector3(4, 1.3f, 0))
                })
                { AimAt(player, room.ToGlobal(target)); await Capture(player, "hero_house_wall_" + wall); }
            if (label == "gulsina-approach")
            { AimAt(player, room.ToGlobal(new(-3.30f, .88f, -1.65f))); await PhotoCornerView("hero_house_photo_corner"); }
            if (label == "stove-front")
            { Aim(player, stoveCenter); await Capture(player, "hero_house_stove_support"); }
            if (label == "photo-approach-turn")
            { AimAt(player, room.ToGlobal(new(-3.55f, .20f, -2.70f))); await PhotoCornerView("hero_house_photo_corner_floor"); }
        }
        await PushAgainst("closed-exit", StyleBenchmarkInteriorFactory.Entry, new(StyleBenchmarkInteriorFactory.DoorX, .04f, 5f),
            position => position.Z < 3.25f);
        await PushAgainst("right-wall", new(3.30f, .04f, 2.65f), new(5.5f, .04f, 2.65f),
            position => position.X < 3.72f);
        player.ApplyZoneSpawn(room.ToGlobal(new(2.72f, .04f, 2.20f)), room.GlobalRotationDegrees.Y);
        player.SetModalOpen(false);
        await PhysicsFrames(3);
        AimAt(player, room.GetNode<Node3D>("HeroRoomWindowFront2").GlobalPosition);
        await Capture(player, "hero_house_window_close");
        world.SetActiveLogicalZone("village_day");
        await PhysicsFrames(2);
        Check(exteriorWindowPanels.All(mesh => mesh.Visible == mesh.GetMeta("heroWindowExteriorOriginalVisibility").AsBool()),
            "Exterior window presentation was not restored after leaving the house.");
        foreach (var body in Descendants(facade).OfType<StaticBody3D>().Where(body => body.Name == "AuthoredKitCollisionProxy"))
            Check(body.CollisionLayer == 2, "The hero facade did not regain collision on exit.");

        async Task PhotoCornerView(string label)
        {
            // The ordinary room path reaches this view. Sample the actual
            // pixels above the timber beside the basket to identify any foreign
            // visible triangle without guessing its mesh owner from its color.
            var camera = player.GetNode<Camera3D>("Head/Camera3D");
            var viewport = camera.GetViewport().GetVisibleRect().Size;
            var pixels = new List<Vector2>();
            foreach (var x in new[] { -3.80f, -3.50f, -3.20f })
            foreach (var z in new[] { -3.15f, -2.75f, -2.35f, -1.95f })
            {
                var point = room.ToGlobal(new(x, .025f, z));
                if (camera.IsPositionBehind(point)) continue;
                var pixel = camera.UnprojectPosition(point) / viewport;
                if (pixel.X is >= 0 and <= 1 && pixel.Y is >= 0 and <= 1) pixels.Add(pixel);
            }
            Act1VisibleSurfaceProbe.Log(GetTree().Root, camera, label + "-floor-owner", pixels.ToArray());
            await Capture(player, label);
        }

        async Task PushAgainst(string label, Vector3 from, Vector3 toward, Func<Vector3, bool> stayedInside)
        {
            player.ApplyZoneSpawn(room.ToGlobal(from), room.GlobalRotationDegrees.Y);
            player.SetModalOpen(false);
            await PhysicsFrames(3);
            Aim(player, room.ToGlobal(toward));
            Input.ActionPress("move_forward");
            try { await PhysicsFrames(65); }
            finally { Input.ActionRelease("move_forward"); }
            var actual = room.ToLocal(player.GlobalPosition);
            Check(stayedInside(actual), $"The player walked through the hero {label}: {actual}.");
            _receipt.Add(new { kind = "hero-room-wall-push", label, actual = Point(actual), stopped = stayedInside(actual) });
        }
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
            var blockers = Enumerable.Range(0, player.GetSlideCollisionCount()).Select(index =>
            {
                var collision = player.GetSlideCollision(index);
                var shape = collision.GetColliderShape() as Node;
                return new
                {
                    body = (collision.GetCollider() as Node)?.GetPath().ToString(),
                    shape = shape?.GetPath().ToString(),
                    source = shape?.HasMeta("authoredSourceMesh") == true ? shape.GetMeta("authoredSourceMesh").AsString() : null,
                    point = Point(collision.GetPosition()), normal = Point(collision.GetNormal())
                };
            }).ToArray();
            _receipt.Add(new { kind = "controller-stall", label, position = Point(player.GlobalPosition), target = Point(target), blockers,
                stepsClimbed = player.StepsClimbed, lastStepRejection = player.LastStepRejection });
            GD.Print($"act1-boundary-stall: {label}: {JsonSerializer.Serialize(blockers)}");
        }
        finally { Input.ActionRelease("move_forward"); }
    }

    private bool RenderedHit(MeshInstance3D mesh, Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal)
        => RenderedHit(mesh.Mesh, mesh.GlobalTransform, from, to, out point, out normal);

    private bool RenderedHit(Mesh mesh, Transform3D transform, Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal)
    {
        if (!_faces.TryGetValue(mesh, out var faces)) _faces[mesh] = faces = mesh.GetFaces();
        var inverse = transform.AffineInverse();
        var localFrom = inverse * from;
        var localTo = inverse * to;
        point = default;
        normal = default;
        var closest = float.MaxValue;
        for (var index = 0; index + 2 < faces.Length; index += 3)
        {
            var result = Geometry3D.SegmentIntersectsTriangle(localFrom, localTo, faces[index], faces[index + 1], faces[index + 2]);
            if (result.VariantType == Variant.Type.Nil) continue;
            var hit = transform * result.AsVector3();
            var distance = from.DistanceSquaredTo(hit);
            if (distance >= closest) continue;
            closest = distance;
            point = hit;
            normal = (transform.Basis.Inverse().Transposed()
                * (faces[index + 1] - faces[index]).Cross(faces[index + 2] - faces[index])).Normalized();
            if (normal.Dot(from - point) < 0) normal = -normal;
        }
        return closest < float.MaxValue;
    }

    private async Task Capture(FirstPersonController player, string label)
    {
        if (string.IsNullOrEmpty(_output)) return;
        await Frames(3);
        if (label is "hero_house_entry" or "hero_house_reverse" or "hero_house_stove_support")
            Act1VisibleSurfaceProbe.Log(GetTree().Root, player.GetNode<Camera3D>("Head/Camera3D"), label,
                new(.5f, .72f), new(.3f, .75f), new(.7f, .70f), new(.5f, .5f));
        if (label == "hero_house_entry")
            Act1VisibleSurfaceProbe.Log(GetTree().Root, player.GetNode<Camera3D>("Head/Camera3D"), label + "-rail-and-outer-volume",
                new(.5f, .765f), new(.8f, .805f), new(.92f, .43f));
        if (label == "hero_house_window_close")
            Act1VisibleSurfaceProbe.Log(GetTree().Root, player.GetNode<Camera3D>("Head/Camera3D"), label,
                new(.3f, .45f), new(.5f, .5f), new(.85f, .55f));
        if (label == "fap_furniture_waiting_aisle")
            Act1VisibleSurfaceProbe.Log(GetTree().Root, player.GetNode<Camera3D>("Head/Camera3D"), label + "-outside-structure-and-floor",
                new(.12f, .35f), new(.10f, .55f), new(.27f, .35f), new(.75f, .80f));
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
    private static void AimAt(FirstPersonController player, Vector3 target)
    {
        var eye = player.GetNode<Camera3D>("Head/Camera3D").GlobalPosition;
        var delta = target - eye;
        player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length())),
            Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
    }
    private void Check(bool condition, string failure) { if (!condition) _failures.Add(failure); }
    private static object Point(Vector3 value) => new { x = value.X, y = value.Y, z = value.Z };
    private async Task PhysicsFrames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
}
