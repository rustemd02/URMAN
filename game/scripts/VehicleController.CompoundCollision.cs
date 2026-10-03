using Godot;
using System.Text.Json.Nodes;

namespace Urman.Godot;

public partial class VehicleController
{
    private sealed record FixedVehicleVolume(CollisionShape3D Node, Transform3D Bind,
        bool Leans = false, float WheelRadius = 0, float WheelWidth = 0, Vector2[]? CartProfile = null);
    private sealed record SupportGroup(string Name, Vector3 Point, CollisionShape3D? Wheel = null);
    private readonly List<FixedVehicleVolume> _fixedVolumes = new();
    private readonly List<SupportGroup> _supportGroups = new();
    private sealed record HoofQueryVolume(string Name, ConvexPolygonShape3D Shape, Vector3[] Vertices, Plane[] Planes);
    private readonly List<HoofQueryVolume> _hoofQueries = new();
    private readonly List<HoofQueryVolume> _lowerLegQueries = new();
    private VehicleHorsePose.PosePlan? _acceptedHorsePose, _pendingHorsePose;
    private VehicleHorsePose.PosePlan? _rejectedHorsePose;
    private string _horseProjectionFailure = string.Empty;
    private JsonObject? _lastHoofContact;
    internal JsonObject? LastHoofContact => _lastHoofContact?.DeepClone().AsObject();
    private float _motorcycleLean;
    private int MinimumSupportedGroups => Definition.Kind == VehicleKind.Niva ? 3 : _supportGroups.Count;

    private static Transform3D LeanTransform(float radians)
        => new(Basis.FromEuler(new(0, 0, radians)),
            new(Mathf.Sin(radians) * .8f, (1 - Mathf.Cos(radians)) * .8f, 0));

    private Transform3D MotorcycleLeanTransform(float? lean = null)
        => Definition.Kind == VehicleKind.Motorcycle
            ? LeanTransform(lean ?? _motorcycleLean)
            : Transform3D.Identity;

    private float RequestedMotorcycleLean(float steering)
        => Definition.Kind == VehicleKind.Motorcycle && Driver is not null
            ? Mathf.Clamp(steering * Math.Min(Math.Abs(Speed) / Definition.MaxForwardSpeed, 1f) * .4f, -.04f, .04f) : 0;

    private void AddFixedVolume(string name, Shape3D shape, Transform3D bind,
        bool leans = false, float wheelRadius = 0, float wheelWidth = 0, Vector2[]? cartProfile = null)
    {
        var node = new CollisionShape3D { Name = name, Shape = shape, Transform = bind };
        node.SetMeta("collisionOwner", "VehicleController compound; same CharacterBody3D");
        AddChild(node);
        _fixedVolumes.Add(new(node, bind, leans, wheelRadius, wheelWidth, cartProfile));
    }

    private void AddBoxVolume(string name, Aabb bounds)
        => AddFixedVolume(name, new BoxShape3D { Size = bounds.Size }, new(Basis.Identity, bounds.GetCenter()));

    private Transform3D LocalMeshTransform(Node3D node)
    {
        var result = Transform3D.Identity;
        for (Node? cursor = node; cursor is not null && cursor != this; cursor = cursor.GetParent())
            if (cursor is Node3D spatial) result = spatial.Transform * result;
        return result;
    }

    private IEnumerable<Vector3> MeshPoints(MeshInstance3D instance)
    {
        var mesh = instance.Mesh ?? throw new InvalidOperationException("Vehicle mesh is missing.");
        var transform = LocalMeshTransform(instance);
        for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            using var arrays = mesh.SurfaceGetArrays(surface);
            foreach (var vertex in arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                yield return transform * vertex;
        }
    }

    // SurfaceGetArrays returns a caller-owned array and copies the surface into it;
    // releasing it here keeps the collision build from leaving those copies behind.
    private static Vector3[] SurfaceVertices(Mesh mesh, int surface)
    {
        using var arrays = mesh.SurfaceGetArrays(surface);
        return arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
    }

    private static Aabb Bounds(IEnumerable<Vector3> points, float padding = .00002f)
    {
        var low = Vector3.One * float.PositiveInfinity;
        var high = Vector3.One * float.NegativeInfinity;
        var count = 0;
        foreach (var point in points) { low = low.Min(point); high = high.Max(point); count++; }
        if (count == 0) throw new InvalidOperationException("Physical volume has no source vertices.");
        return new(low - Vector3.One * padding, high - low + Vector3.One * (2 * padding));
    }

    private void BuildCompoundCollision()
    {
        if (Definition.Kind == VehicleKind.Niva)
        {
            // Keep the accepted Niva body and its existing two steering tyres exact.
            AddBoxVolume("ChassisCollision", new(Definition.HullCenter - Definition.HullSize * .5f, Definition.HullSize));
            return;
        }
        var meshes = _visual.Root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
        if (Definition.Kind == VehicleKind.Motorcycle)
        {
            var source = meshes.Where(mesh => !_visual.Wheels.Any(wheel => wheel.IsAncestorOf(mesh)))
                .SelectMany(MeshPoints).ToArray();
            // Upper parts keep their full authored silhouette at every permitted bank.
            // The small analytic arc bound covers values between the sampled angles.
            var radius = source.Max(point => (point - Vector3.Up * .8f).Length());
            var padding = radius * (1 - Mathf.Cos(.002f)) + .00002f;
            var banked = Enumerable.Range(0, 21).SelectMany(index =>
                source.Select(point => LeanTransform(-.04f + index * .004f) * point));
            var upper = Bounds(banked, padding);
            if (upper.Position.Y <= .20f) throw new InvalidOperationException("Motorcycle upper geometry unexpectedly reaches the ground.");
            AddBoxVolume("ChassisCollision", upper);
            foreach (var wheel in _visual.Wheels.Where(wheel => !_visual.FrontWheels.Contains(wheel)))
                AddFixedRoadWheel(wheel, "RearWheelCollision", true);
            return;
        }

        foreach (var name in new[] { "CartAndHarness", "HorseBody" })
        {
            var source = meshes.Where(mesh => mesh.Name == name).SelectMany(MeshPoints).ToArray();
            AddBoxVolume(name == "CartAndHarness" ? "ChassisCollision" : "HorseBodyCollision", Bounds(source));
        }

        var head = _visual.HorseHead ?? throw new InvalidOperationException("Horse head binding is missing.");
        var headBind = LocalMeshTransform(head);
        var headPoints = meshes.Where(mesh => head.IsAncestorOf(mesh)).SelectMany(MeshPoints)
            .Select(point => headBind.AffineInverse() * point).ToArray();
        var headRadius = headPoints.Max(point => point.Length());
        var headEnvelope = new List<Vector3>();
        for (var pitch = -7; pitch <= 0; pitch++)
        for (var yaw = -14; yaw <= 14; yaw++)
        {
            var turn = Basis.FromEuler(new(Mathf.DegToRad(pitch), Mathf.DegToRad(yaw), 0));
            headEnvelope.AddRange(headPoints.Select(point => headBind * (turn * point)));
        }
        AddBoxVolume("HorseHeadCollision", Bounds(headEnvelope,
            2 * headRadius * (1 - Mathf.Cos(Mathf.DegToRad(.5f))) + .00002f));

        // The two reins are posed only after placement; cover their authored
        // driver/head endpoints without sampling their unposed cylinder bind pose.
        AddBoxVolume("ReinsCollision", new(new(-.32f, 1.16f, -3.39f), new(.64f, .96f, 3.84f)));
        // The upper limbs retain their body volume. Each lower hoof is queried
        // at its accepted articulated pose; a whole possible stride is not an
        // obstacle. Ground IK never moves a native support shape down each tick.
        AddBoxVolume("HorseLegMovementCollision", new(new(-.42f, .25f, -2.71f), new(.84f, 1.05f, 2.255f)));
        var legs = _visual.HorseLegs;
        for (var index = 0; index < legs.Count; index++)
        {
            var hip = legs[index].Position;
            var hind = hip.Z > -1.5f;
            var sole = new Vector3(hip.X, 0, hip.Z + (hind ? .025f : -.04f));
            var kneeNode = legs[index].FindChildren(hind ? "Hock" : "Knee", "Node3D", true, false).OfType<Node3D>().Single();
            var hoofNode = kneeNode.FindChildren("Hoof", "Node3D", true, false).OfType<Node3D>().Single();
            var hoofMesh = hoofNode.FindChildren("HoofWallAndSole", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Single();
            var points = MeshVertices(hoofMesh, hoofNode);
            _hoofQueries.Add(new("HoofContactQuery" + index, new ConvexPolygonShape3D { Points = points }, points, HoofPlanes(points)));
            // The cannon and fetlock hang below the fixed limb box and swing with
            // the knee, not with the flat-footed hoof: as a hoof-frame hull they
            // left the physical union and could clip through low obstacles. Carry
            // the visible lower leg in its own knee-frame volume and treat terrain
            // under it as support, exactly as the hoof hull does, so the sole stays
            // the only surface that rests on the ground.
            var cannonMesh = kneeNode.FindChildren("CannonAndFetlock", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Single();
            var cannonPoints = MeshVertices(cannonMesh, kneeNode);
            if (cannonPoints.Length < 4) throw new InvalidOperationException("The articulated lower leg has no volume.");
            _lowerLegQueries.Add(new("LowerLegContactQuery" + index, new ConvexPolygonShape3D { Points = cannonPoints },
                cannonPoints, HoofPlanes(cannonPoints)));
            _supportGroups.Add(new((hind ? "hind-hoof-" : "fore-hoof-") + index, sole));

            Vector3[] MeshVertices(MeshInstance3D mesh, Node3D frame)
            {
                var toFrame = Transform3D.Identity;
                for (Node? cursor = mesh; cursor is not null && cursor != frame; cursor = cursor.GetParent())
                    if (cursor is Node3D spatial) toFrame = spatial.Transform * toFrame;
                return Enumerable.Range(0, mesh.Mesh!.GetSurfaceCount())
                    .SelectMany(surface => SurfaceVertices(mesh.Mesh!, surface))
                    .Select(point => toFrame * point).Distinct().ToArray();
            }
        }
        if (legs.Count != 4) throw new InvalidOperationException("Horse requires four supported rest-sole bindings.");
        foreach (var wheel in _visual.Wheels) AddFixedRoadWheel(wheel, "CartWheelCollision" + _fixedVolumes.Count, false);
    }

    private void AddFixedRoadWheel(Node3D wheel, string name, bool leans)
    {
        float radius, width;
        Vector2[]? profile = null;
        if (wheel.HasMeta("roadTyreRadius"))
        {
            radius = wheel.GetMeta("roadTyreRadius").AsSingle();
            width = wheel.GetMeta("roadTyreWidth").AsSingle();
        }
        else
        {
            var inverse = LocalMeshTransform(wheel).AffineInverse();
            var points = wheel.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                .SelectMany(MeshPoints).Select(point => inverse * point).ToArray();
            radius = points.Max(point => new Vector2(point.Y, point.Z).Length());
            width = 2 * points.Max(point => Math.Abs(point.X));
            profile = CartWheelProfile(points);
        }
        AddFixedVolume(name, new ConvexPolygonShape3D { Points = profile is null
                ? VehicleWheelGeometry.EnvelopePoints(radius, width) : CartWheelPoints(profile) },
            new(Basis.Identity, wheel.Position), leans, radius, width, profile);
    }

    private static Vector2[] CartWheelProfile(IEnumerable<Vector3> vertices)
    {
        // Upper convex envelope of the actual axial/radial coordinates. The
        // wooden rim reaches full radius at its edge; it is not a road tyre.
        var ordered = vertices.GroupBy(point => point.X).Select(group =>
            new Vector2(group.Key, group.Max(point => new Vector2(point.Y, point.Z).Length())))
            .OrderBy(point => point.X);
        var upper = new List<Vector2>();
        foreach (var point in ordered)
        {
            while (upper.Count >= 2 && (upper[^1] - upper[^2]).Cross(point - upper[^1]) >= 0)
                upper.RemoveAt(upper.Count - 1);
            upper.Add(point);
        }
        if (upper.Count < 2) throw new InvalidOperationException("Cart wheel has no axial profile.");
        return upper.ToArray();
    }

    private static Vector3[] CartWheelPoints(Vector2[] profile)
        => profile.SelectMany(ring => Enumerable.Range(0, VehicleWheelGeometry.EnvelopeSides).Select(index =>
        {
            var angle = Mathf.Tau * index / VehicleWheelGeometry.EnvelopeSides;
            var radius = ring.Y / Mathf.Cos(Mathf.Pi / VehicleWheelGeometry.EnvelopeSides);
            return new Vector3(ring.X, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        })).ToArray();

    private static bool CartWheelContains(Vector2[] profile, Vector3 point, float tolerance)
    {
        if (point.X < profile[0].X - tolerance || point.X > profile[^1].X + tolerance) return false;
        var radius = 0f;
        for (var index = 1; index < profile.Length; index++)
            if (point.X <= profile[index].X + tolerance)
            {
                var from = profile[index - 1]; var to = profile[index];
                radius = Mathf.Lerp(from.Y, to.Y, Mathf.Clamp((point.X - from.X) / (to.X - from.X), 0, 1));
                break;
            }
        for (var index = 0; index < VehicleWheelGeometry.EnvelopeSides; index++)
        {
            var angle = Mathf.Tau * (index + .5f) / VehicleWheelGeometry.EnvelopeSides;
            if (point.Y * Mathf.Cos(angle) + point.Z * Mathf.Sin(angle) > radius + tolerance) return false;
        }
        return true;
    }

    private void BuildSupportTopology()
    {
        if (Definition.Kind == VehicleKind.Niva)
        {
            var bottom = Definition.HullCenter.Y - Definition.HullSize.Y * .5f;
            foreach (var x in new[] { -.36f, .36f }) foreach (var z in new[] { -.38f, .38f })
                _supportGroups.Add(new("niva-existing-" + _supportGroups.Count,
                    new(Definition.HullCenter.X + Definition.HullSize.X * x, bottom, Definition.HullCenter.Z + Definition.HullSize.Z * z)));
            return;
        }
        foreach (var volume in _fixedVolumes.Where(volume => volume.WheelRadius > 0))
            _supportGroups.Add(new(volume.Node.Name.ToString(), volume.Bind.Origin - Vector3.Up *
                ((volume.CartProfile?.Max(point => point.Y) ?? VehicleWheelGeometry.TreadOuterRadius(volume.WheelRadius))
                    / Mathf.Cos(Mathf.Pi / VehicleWheelGeometry.EnvelopeSides)), volume.Node));
        foreach (var wheel in _steeringVolumes)
            _supportGroups.Add(new(wheel.Collider.Name.ToString(), wheel.Centre - Vector3.Up *
                (VehicleWheelGeometry.TreadOuterRadius(wheel.Radius) / Mathf.Cos(Mathf.Pi / VehicleWheelGeometry.EnvelopeSides)), wheel.Collider));
        if (Definition.Kind == VehicleKind.Motorcycle && _supportGroups.Count != 2
            || Definition.Kind == VehicleKind.HorseCart && _supportGroups.Count != 8)
            throw new InvalidOperationException("Vehicle support topology does not match its wheel/hoof attachments.");
    }

    private IEnumerable<(SupportGroup Group, Vector3 Point)> AppliedSupportGroups(float? steering = null, float? lean = null,
        Transform3D? atPose = null, bool restBindings = false)
    {
        var transforms = CollisionVolumes(Transform3D.Identity, steering ?? _steering, leanRadians: lean)
            .ToDictionary(volume => volume.Name, volume => volume.Transform, StringComparer.Ordinal);
        var pose = atPose ?? (IsInsideTree() ? GlobalTransform : Transform3D.Identity);
        var horseFrame = restBindings ? null : HorseFrameForPose(pose);
        foreach (var group in _supportGroups)
        {
            if (horseFrame is {} horse && group.Wheel is null
                && int.TryParse(group.Name.AsSpan(group.Name.LastIndexOf('-') + 1), out var legIndex)
                && legIndex < horse.Legs.Count)
            { yield return (group, pose.AffineInverse() * horse.Legs[legIndex].Sole); continue; }
            if (group.Wheel?.Shape is not ConvexPolygonShape3D convex) { yield return (group, group.Point); continue; }
            var transform = transforms[group.Name];
            var points = convex.Points.Select(point => transform * point).ToArray();
            var bottom = points.Min(point => point.Y);
            // These are the actual current convex points, after steering/bank.
            // Average the coplanar bottom edge instead of a nominal tyre radius.
            var lowest = points.Where(point => point.Y <= bottom + .000001f).ToArray();
            yield return (group, lowest.Aggregate(Vector3.Zero, (sum, point) => sum + point) / lowest.Length);
        }
    }

    internal void GroundAuthoredSpawn(Func<Vector3, float> ground)
    {
        if (IsInsideTree()) throw new InvalidOperationException("Authored grounding precedes the initial physics projection.");
        var basis = Basis.FromEuler(new(0, Mathf.DegToRad(Definition.YawDegrees), 0));
        var spawn = Definition.Spawn;
        var originY = AppliedSupportGroups(0, 0).Max(group => ground(spawn + basis * group.Point) - group.Point.Y) + .025f;
        Definition = Definition with { Spawn = new(spawn.X, originY, spawn.Z) };
    }

    private static bool ContainsSimpleShape(Shape3D shape, Vector3 point, float tolerance)
    {
        if (shape is BoxShape3D box)
        {
            var half = box.Size * .5f + Vector3.One * tolerance;
            return Math.Abs(point.X) <= half.X && Math.Abs(point.Y) <= half.Y && Math.Abs(point.Z) <= half.Z;
        }
        if (shape is CapsuleShape3D capsule)
        {
            var half = capsule.Height * .5f - capsule.Radius;
            var centre = new Vector3(0, Mathf.Clamp(point.Y, -half, half), 0);
            return point.DistanceTo(centre) <= capsule.Radius + tolerance;
        }
        throw new InvalidOperationException("Unknown compound shape " + shape.GetClass());
    }

    private VehicleHorsePose.PosePlan? HorseFrameForPose(Transform3D pose)
    {
        if (_hoofQueries.Count == 0 || !IsInsideTree() || _visual.HorsePose is not {} horse) return null;
        if (_rejectedHorsePose is {} rejected && rejected.OwnerPose == pose) return rejected;
        if (_acceptedHorsePose is {} accepted && accepted.OwnerPose == pose) return accepted;
        return horse.PreparePose(this, pose, 0, Speed, _steering, HorseState,
            rest: _acceptedHorsePose is null || !PlacementAvailable || pose.Origin.DistanceSquaredTo(GlobalPosition) > .25f);
    }

    private static Plane[] HoofPlanes(Vector3[] points)
    {
        var planes = new List<Plane>();
        for (var a = 0; a < points.Length - 2; a++)
        for (var b = a + 1; b < points.Length - 1; b++)
        for (var c = b + 1; c < points.Length; c++)
        {
            var cross = (points[b] - points[a]).Cross(points[c] - points[a]);
            if (cross.LengthSquared() < 1e-12f) continue;
            var normal = cross.Normalized(); var plane = new Plane(normal, normal.Dot(points[a]));
            var positive = false; var negative = false;
            foreach (var point in points)
            {
                var distance = plane.DistanceTo(point);
                positive |= distance > .000002f; negative |= distance < -.000002f;
                if (positive && negative) break;
            }
            if (positive && negative) continue;
            if (positive) plane = new Plane(-normal, -plane.D);
            if (!planes.Any(existing => existing.Normal.Dot(plane.Normal) > .999999f && Math.Abs(existing.D - plane.D) < .000002f)) planes.Add(plane);
        }
        if (planes.Count < 4) throw new InvalidOperationException("The authored hoof has no closed convex contact volume.");
        return planes.ToArray();
    }

    private global::Godot.Collections.Array<Rid> HoofExcluded(VehicleHorsePose.PosePlan frame, int index,
        global::Godot.Collections.Array<Rid> excluded)
    {
        var result = new global::Godot.Collections.Array<Rid>(excluded);
        var leg = frame.Legs[index]; var rid = leg.GroundRid;
        var points = _hoofQueries[index].Vertices.Select(point => leg.HoofPose * point).ToArray();
        if (points.All(point => (point - leg.Sole).Dot(leg.Normal) >= -.012f)
            && HoofSupportExclusionClear(rid, points, 0)) result.Add(rid);
        return result;
    }

    /// <summary>
    /// The articulated lower leg stands on the same terrain as its hoof, so a
    /// supported ground body is excluded for it as well; anything else stays an
    /// obstacle because the visible cannon must not pass through it.
    /// </summary>
    private global::Godot.Collections.Array<Rid> LowerLegExcluded(VehicleHorsePose.PosePlan frame, int index,
        global::Godot.Collections.Array<Rid> excluded)
    {
        var result = new global::Godot.Collections.Array<Rid>(excluded);
        var leg = frame.Legs[index]; var rid = leg.GroundRid;
        var points = _lowerLegQueries[index].Vertices.Select(point => leg.KneePose * point).ToArray();
        if (points.All(point => (point - leg.Sole).Dot(leg.Normal) >= -.012f)
            && HoofSupportExclusionClear(rid, points, 0)) result.Add(rid);
        return result;
    }

    private static bool HoofSupportExclusionClear(Rid rid, IEnumerable<Vector3> envelope, float clearance)
    {
        if (rid == default || PhysicsServer3D.BodyGetShapeCount(rid) != 1) return false;
        var body = GodotObject.InstanceFromId(PhysicsServer3D.BodyGetObjectInstanceId(rid)) as CollisionObject3D;
        if (body is null) return false;
        var shapeOwner = body.ShapeFindOwner(0);
        if (body.ShapeOwnerGetOwner(shapeOwner) is not CollisionShape3D shape) return false;
        // Do not exclude an arbitrary compound RID: it could also own a wall.
        // For a box use its actual top plane, never another pose's sole plane.
        if (shape.Shape is BoxShape3D box)
        {
            var normal = shape.GlobalBasis.Y.Normalized();
            var top = shape.ToGlobal(Vector3.Up * box.Size.Y * .5f);
            return envelope.All(point => (point - top).Dot(normal) >= clearance);
        }
        return shape.Shape is ConcavePolygonShape3D && body.Name == "AgentB_TerrainCollision";
    }

    private bool HoofEndpointClear(VehicleHorsePose.PosePlan frame, out JsonObject? contact, bool requireSupport = true)
    {
        contact = null;
        var baseExcluded = PlacementExcluded();
        using var baseExcludedOwner = (global::Godot.Collections.Array)baseExcluded;
        for (var index = 0; index < _hoofQueries.Count; index++)
        {
            var leg = frame.Legs[index];
            var excluded = HoofExcluded(frame, index, baseExcluded);
            using var excludedOwner = (global::Godot.Collections.Array)excluded;
            using var query = new PhysicsShapeQueryParameters3D { Shape = _hoofQueries[index].Shape,
                Transform = leg.HoofPose, CollisionMask = CollisionMask, Margin = 0,
                Exclude = excluded };
            var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 1);
            using var hitsOwner = (global::Godot.Collections.Array)hits;
            if (hits.Count == 0)
            {
                if (requireSupport && (!leg.Reachable || !leg.Moving && !leg.Grounded))
                {
                    contact = new() { ["foot"] = index, ["reason"] = leg.Rejection, ["kind"] = "support-or-reach",
                        ["vehicleShape"] = _hoofQueries[index].Name, ["colliderPath"] = leg.Collider };
                    return false;
                }
                continue;
            }
            using var hit = hits[0];
            contact = DescribePlacementContact(hit);
            contact["vehicleShape"] = _hoofQueries[index].Name; contact["foot"] = index;
            contact["kind"] = "hoof-endpoint"; return false;
        }
        // The articulated lower leg is visible geometry too: an obstacle inside
        // the knee-frame cannon volume would clip through it.
        for (var index = 0; index < _lowerLegQueries.Count; index++)
        {
            var excluded = LowerLegExcluded(frame, index, baseExcluded);
            using var excludedOwner = (global::Godot.Collections.Array)excluded;
            using var query = new PhysicsShapeQueryParameters3D { Shape = _lowerLegQueries[index].Shape,
                Transform = frame.Legs[index].KneePose, CollisionMask = CollisionMask, Margin = 0,
                Exclude = excluded };
            var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 1);
            using var hitsOwner = (global::Godot.Collections.Array)hits;
            if (hits.Count == 0) continue;
            using var hit = hits[0];
            contact = DescribePlacementContact(hit);
            contact["vehicleShape"] = _lowerLegQueries[index].Name; contact["foot"] = index;
            contact["kind"] = "lower-leg-endpoint"; return false;
        }
        return true;
    }

    private bool HoofSegmentClear(VehicleHorsePose.PosePlan from, VehicleHorsePose.PosePlan to, out JsonObject? contact)
    {
        // Identical contact geometry has no swept path. Still validate the
        // current endpoint against this tick's world and refreshed support.
        if (from.Legs.Zip(to.Legs, (a, b) => a.HoofPose == b.HoofPose && a.KneePose == b.KneePose
            && a.Sole == b.Sole && a.Normal == b.Normal && a.GroundRid == b.GroundRid).All(same => same))
            return HoofEndpointClear(to, out contact);
        if (!HoofEndpointClear(from, out contact, requireSupport: false) || !HoofEndpointClear(to, out contact)) return false;
        var space = GetWorld3D().DirectSpaceState;
        if (!LowerLegSegmentClear(from, to, out contact)) return false;
        for (var index = 0; index < _hoofQueries.Count; index++)
        {
            var volume = _hoofQueries[index]; var a = from.Legs[index]; var b = to.Legs[index];
            if (a.HoofPose == b.HoofPose) continue;
            var motion = b.HoofPose.Origin - a.HoofPose.Origin;
            var angle = a.HoofPose.Basis == b.HoofPose.Basis ? 0
                : a.HoofPose.Basis.GetRotationQuaternion().AngleTo(b.HoofPose.Basis.GetRotationQuaternion());
            var radius = volume.Vertices.Max(point => point.Length());
            // Cover intermediate rotation as well as translation. The derived
            // radius bound supplements this query; it never shrinks body margin.
            var rotationBound = 2 * radius * Mathf.Sin(Math.Min(Mathf.Pi, angle) * .5f);
            var excluded = PlacementExcluded();
            using var excludedOwner = (global::Godot.Collections.Array)excluded;
            var envelope = volume.Vertices.Select(point => a.HoofPose * point)
                .SelectMany(point => new[] { point, point + motion }).ToArray();
            // Same two-RID set, same first-occurrence order and the same
            // HoofSupportExclusionClear/excluded.Contains guards as
            // `new[] { a.GroundRid, b.GroundRid }.Distinct()`: the second RID is
            // skipped only when it equals the first, exactly like Distinct().
            if (HoofSupportExclusionClear(a.GroundRid, envelope, rotationBound) && !excluded.Contains(a.GroundRid))
                excluded.Add(a.GroundRid);
            if (b.GroundRid != a.GroundRid
                && HoofSupportExclusionClear(b.GroundRid, envelope, rotationBound) && !excluded.Contains(b.GroundRid))
                excluded.Add(b.GroundRid);
            using var query = new PhysicsShapeQueryParameters3D { Shape = volume.Shape, Transform = a.HoofPose,
                Motion = motion, CollisionMask = CollisionMask, Margin = rotationBound, Exclude = excluded };
            // CastMotion ignores initial overlaps; check the rotational envelope first.
            var initial = space.IntersectShape(query, 1);
            using var initialOwner = (global::Godot.Collections.Array)initial;
            var fractions = space.CastMotion(query);
            if (fractions.Length != 2 || fractions.Any(value => !float.IsFinite(value) || value < 0 || value > 1))
            {
                contact = new() { ["kind"] = "invalid-hoof-sweep-result", ["foot"] = index,
                    ["vehicleShape"] = volume.Name, ["reason"] = "The native sweep did not return two finite fractions." };
                return false;
            }
            if (initial.Count == 0 && fractions[0] >= 1) continue;
            var fraction = initial.Count != 0 ? 0 : fractions[0];
            var identificationFraction = fractions.Length > 1 ? fractions[1] : fraction;
            query.Transform = new(a.HoofPose.Basis, a.HoofPose.Origin + motion * identificationFraction);
            using var rest = space.GetRestInfo(query);
            using var initialHit = initial.Count != 0 ? initial[0] : null;
            contact = initialHit is not null ? DescribePlacementContact(initialHit) : new JsonObject();
            if (initial.Count == 0)
            {
                var identified = space.IntersectShape(query, 1);
                using var identifiedOwner = (global::Godot.Collections.Array)identified;
                if (identified.Count != 0)
                {
                    using var hit = identified[0];
                    contact = DescribePlacementContact(hit);
                }
            }
            contact["vehicleShape"] = volume.Name; contact["foot"] = index;
            contact["kind"] = "hoof-sweep"; contact["safeFraction"] = fraction;
            contact["motion"] = motion.ToString(); contact["rotationBound"] = rotationBound;
            if (rest.Count != 0)
            {
                contact["point"] = rest["point"].AsVector3().ToString();
                contact["normal"] = rest["normal"].AsVector3().ToString();
                var collider = GodotObject.InstanceFromId(rest["collider_id"].AsUInt64()) as Node;
                contact["colliderPath"] = collider?.GetPath().ToString();
            }
            return false;
        }
        return true;
    }

    /// <summary>
    /// The articulated cannon sweeps between two accepted poses just like the
    /// hoof, so an obstacle between them stops the stride instead of passing
    /// through the visible lower leg.
    /// </summary>
    private bool LowerLegSegmentClear(VehicleHorsePose.PosePlan from, VehicleHorsePose.PosePlan to, out JsonObject? contact)
    {
        contact = null;
        var space = GetWorld3D().DirectSpaceState;
        for (var index = 0; index < _lowerLegQueries.Count; index++)
        {
            var volume = _lowerLegQueries[index];
            var a = from.Legs[index].KneePose; var b = to.Legs[index].KneePose;
            if (a == b) continue;
            var motion = b.Origin - a.Origin;
            var angle = a.Basis == b.Basis ? 0 : a.Basis.GetRotationQuaternion().AngleTo(b.Basis.GetRotationQuaternion());
            var radius = volume.Vertices.Max(point => point.Length());
            var rotationBound = 2 * radius * Mathf.Sin(Math.Min(Mathf.Pi, angle) * .5f);
            var baseExcluded = PlacementExcluded();
            using var baseExcludedOwner = (global::Godot.Collections.Array)baseExcluded;
            var excluded = LowerLegExcluded(from, index, baseExcluded);
            using var excludedOwner = (global::Godot.Collections.Array)excluded;
            var envelope = volume.Vertices.Select(point => a * point)
                .SelectMany(point => new[] { point, point + motion }).ToArray();
            // Same as `new[] { from, to }.Distinct()`: same set, same
            // first-occurrence order, second RID skipped only when equal.
            if (HoofSupportExclusionClear(from.Legs[index].GroundRid, envelope, rotationBound)
                && !excluded.Contains(from.Legs[index].GroundRid)) excluded.Add(from.Legs[index].GroundRid);
            if (to.Legs[index].GroundRid != from.Legs[index].GroundRid
                && HoofSupportExclusionClear(to.Legs[index].GroundRid, envelope, rotationBound)
                && !excluded.Contains(to.Legs[index].GroundRid)) excluded.Add(to.Legs[index].GroundRid);
            using var query = new PhysicsShapeQueryParameters3D { Shape = volume.Shape, Transform = a,
                Motion = motion, CollisionMask = CollisionMask, Margin = rotationBound, Exclude = excluded };
            var initial = space.IntersectShape(query, 1);
            using var initialOwner = (global::Godot.Collections.Array)initial;
            var fractions = space.CastMotion(query);
            if (fractions.Length != 2 || fractions.Any(value => !float.IsFinite(value) || value < 0 || value > 1))
            {
                contact = new() { ["kind"] = "invalid-lower-leg-sweep-result", ["foot"] = index,
                    ["vehicleShape"] = volume.Name, ["reason"] = "The native sweep did not return two finite fractions." };
                return false;
            }
            if (initial.Count == 0 && fractions[0] >= 1) continue;
            var fraction = initial.Count != 0 ? 0 : fractions[0];
            var identificationFraction = fractions.Length > 1 ? fractions[1] : fraction;
            query.Transform = new(a.Basis, a.Origin + motion * identificationFraction);
            using var initialHit = initial.Count != 0 ? initial[0] : null;
            contact = initialHit is not null ? DescribePlacementContact(initialHit) : new JsonObject();
            if (initial.Count == 0)
            {
                var identified = space.IntersectShape(query, 1);
                using var identifiedOwner = (global::Godot.Collections.Array)identified;
                if (identified.Count != 0)
                {
                    using var hit = identified[0];
                    contact = DescribePlacementContact(hit);
                }
            }
            contact["vehicleShape"] = volume.Name; contact["foot"] = index;
            contact["kind"] = "lower-leg-sweep"; contact["safeFraction"] = fraction;
            contact["motion"] = motion.ToString(); contact["rotationBound"] = rotationBound;
            return false;
        }
        return true;
    }

    private VehicleHorsePose.PosePlan SupportedHorseFrame(VehicleHorsePose horse, VehicleHorsePose.PosePlan candidate,
        bool freshlyPrepared = false)
    {
        // PreparePose already checked the full footprint this tick. Reuse it
        // only if projection did not move any sole; rebased and swinging plans
        // still need their separate clearance projection.
        if (freshlyPrepared && candidate.Legs.All(leg => !leg.Moving && leg.Grounded && leg.SupportAtSole)) return candidate;
        // The same full oval support query checks the ground under an airborne
        // foot too; a swing may clear that surface but may not pass through it.
        // Legs PreparePose already proved planted at their own sole in this
        // same tick skip only their repeated ray (identical state, see
        // SettlePose); that reuse is limited to plans this tick's PreparePose
        // produced, because a rebased plan re-runs the ray with another
        // HorsePose.Basis and Ground's corner validation depends on it.
        var support = horse.SettlePose(this, candidate, reuseFreshSupport: freshlyPrepared);
        for (var index = 0; index < candidate.Legs.Count; index++)
        {
            var leg = candidate.Legs[index]; var floor = support.Legs[index];
            if (!floor.Grounded || (leg.Sole - floor.Sole).Dot(floor.Normal) < -.012f)
            { leg.Reachable = false; leg.Rejection = "hoof path has no coherent terrain clearance"; leg.Collider = floor.Collider; }
            else { leg.GroundRid = floor.GroundRid; leg.Collider = floor.Collider; }
        }
        return candidate;
    }

    private float PrepareHorseMovement(Transform3D target, float dt)
    {
        if (_visual.HorsePose is not {} horse || _hoofQueries.Count == 0) return 1;
        var start = _acceptedHorsePose ?? SupportedHorseFrame(horse,
            horse.PreparePose(this, GlobalTransform, 0, 0, _steering, HorseState, rest: true), freshlyPrepared: true);
        var end = SupportedHorseFrame(horse, horse.PreparePose(this, target, dt, Speed, _steering, HorseState), freshlyPrepared: true);
        var travel = start.Legs.Zip(end.Legs, (a, b) => a.HoofPose.Origin.DistanceTo(b.HoofPose.Origin)).Max();
        var angle = start.Legs.Zip(end.Legs, (a, b) =>
            a.HoofPose.Basis.GetRotationQuaternion().AngleTo(b.HoofPose.Basis.GetRotationQuaternion())).Max();
        var steps = Math.Max(1, (int)Math.Ceiling(Math.Max(travel / .01f, angle / Mathf.DegToRad(.25f))));
        var last = start; var accepted = 0f;
        for (var step = 1; step <= steps; step++)
        {
            var fraction = (float)step / steps;
            var pose = GlobalTransform.InterpolateWith(target, fraction);
            var candidate = SupportedHorseFrame(horse, horse.PreparePose(this, pose, dt * fraction, Speed, _steering, HorseState), freshlyPrepared: true);
            if (!HoofSegmentClear(last, candidate, out var contact))
            {
                _lastHoofContact = contact; CollisionStops++;
                var settled = horse.SettlePose(this, last);
                if (HoofSegmentClear(last, settled, out _)) last = settled;
                _pendingHorsePose = last;
                Notice("Лошадь остановилась перед препятствием. Можно осадить её назад.");
                return accepted;
            }
            last = candidate; accepted = fraction;
        }
        _pendingHorsePose = last; return 1;
    }

    internal void CommitHorsePose(VehicleHorsePose horse, VehicleHorsePose.PosePlan candidate)
    {
        var wanted = SupportedHorseFrame(horse, _pendingHorsePose is {} pending
            ? horse.RebasePose(pending, GlobalTransform) : candidate, freshlyPrepared: _pendingHorsePose is null);
        _pendingHorsePose = null;
        var start = _acceptedHorsePose;
        JsonObject? contact;
        var clear = start is null ? HoofEndpointClear(wanted, out contact)
            : HoofSegmentClear(start, wanted, out contact);
        if (!clear)
        {
            _lastHoofContact = contact; Speed = 0;
            if (start is null) { RejectHorseProjection(horse, wanted); return; }
            var settled = horse.SettlePose(this, horse.RebasePose(start, GlobalTransform));
            if (!HoofSegmentClear(start, settled, out _))
            {
                // Undo only this unaccepted movement through the same body's
                // collision solver. Never assign a saved or invented transform.
                var from = start.OwnerPose;
                MoveAndCollide(from.Origin - GlobalPosition, safeMargin: SafeMargin);
                var currentForward = new Vector2(GlobalBasis.Z.X, GlobalBasis.Z.Z).Normalized();
                var oldForward = new Vector2(from.Basis.Z.X, from.Basis.Z.Z).Normalized();
                var reverseYaw = -Mathf.Atan2(currentForward.Cross(oldForward), currentForward.Dot(oldForward));
                if (Math.Abs(reverseYaw) > .000001f && CanRotate(reverseYaw)) RotateY(reverseYaw);
                settled = horse.SettlePose(this, horse.RebasePose(start, GlobalTransform));
                if (!HoofSegmentClear(start, settled, out _))
                {
                    RejectHorseProjection(horse, settled);
                    return;
                }
            }
            wanted = settled;
        }
        _acceptedHorsePose = wanted; horse.PublishPose(wanted);
    }

    private void RejectHorseProjection(VehicleHorsePose horse, VehicleHorsePose.PosePlan actual)
    {
        // The solver could not return to the previous pose. Publish the exact
        // current, unstretched limbs so diagnostics and queries still describe
        // the visible geometry, but never promote this projection to valid state.
        _acceptedHorsePose = _pendingHorsePose = null;
        _rejectedHorsePose = actual;
        _horseProjectionFailure = "Лошадь не может безопасно поставить ногу. Выйдите из телеги и освободите место.";
        horse.PublishPose(actual);
        horse.MarkProjectionRejected(_horseProjectionFailure);
        SetPlacementAvailability(false, _horseProjectionFailure);
        Notice(_horseProjectionFailure);
    }

    private bool TryRepairHorseProjection()
    {
        if (_horseProjectionFailure.Length == 0) return true;
        if (_visual.HorsePose is not {} horse || _rejectedHorsePose is not {} previous) return false;
        var candidate = SupportedHorseFrame(horse,
            horse.PreparePose(this, GlobalTransform, 0, 0, _steering, HorseState, rest: true), freshlyPrepared: true);
        if (!HoofSegmentClear(previous, candidate, out _) || !ValidatePhysicalPlacement(out _)) return false;
        _rejectedHorsePose = null; _horseProjectionFailure = string.Empty;
        _acceptedHorsePose = candidate; horse.PublishPose(candidate);
        return true;
    }

    internal JsonObject DescribeCompoundCollision()
    {
        static JsonArray V(Vector3 value) => new(value.X, value.Y, value.Z);
        var volumes = new JsonArray();
        foreach (var volume in CollisionVolumes(GlobalTransform, _steering))
            volumes.Add(new JsonObject { ["name"] = volume.Name, ["shape"] = volume.Shape.GetClass().ToString(),
                ["contactOwner"] = _hoofQueries.Any(hoof => hoof.Name == volume.Name) ? "current hoof query"
                    : _lowerLegQueries.Any(leg => leg.Name == volume.Name) ? "current articulated lower leg" : "native body",
                ["origin"] = V(volume.Transform.Origin), ["basisX"] = V(volume.Transform.Basis.X),
                ["basisY"] = V(volume.Transform.Basis.Y), ["basisZ"] = V(volume.Transform.Basis.Z) });
        var supports = new JsonArray();
        foreach (var applied in AppliedSupportGroups()) supports.Add(new JsonObject {
            ["group"] = applied.Group.Name, ["bindLocal"] = V(applied.Group.Point), ["local"] = V(applied.Point),
            ["source"] = applied.Group.Wheel is null && _hoofQueries.Count > 0 ? "accepted articulated sole" : "actual current convex bottom vertices" });
        foreach (var frame in _lowerLegQueries) supports.Add(new JsonObject {
            ["group"] = frame.Name, ["bindLocal"] = V(Vector3.Zero), ["local"] = V(Vector3.Zero),
            ["source"] = "articulated lower leg; clearance volume only, never a support group" });
        return new JsonObject { ["sameBodyRid"] = GetRid().ToString(), ["volumes"] = volumes, ["supports"] = supports,
            ["leanRadians"] = _motorcycleLean,
            ["nativeBodyShapeCount"] = _fixedVolumes.Count + _steeringVolumes.Count,
            ["hoofQueryCount"] = _hoofQueries.Count + _lowerLegQueries.Count,
            ["lowerLegQueryCount"] = _lowerLegQueries.Count, ["wholeHoofBodyRidContainment"] = false,
            ["hoofProjectionValid"] = _horseProjectionFailure.Length == 0,
            ["hoofProjectionFailure"] = _horseProjectionFailure,
            ["lastHoofContact"] = _lastHoofContact?.DeepClone(),
            ["requiredGroups"] = MinimumSupportedGroups, ["totalGroups"] = _supportGroups.Count,
            ["policy"] = Definition.Kind == VehicleKind.Niva ? "existing three of four" : "every named wheel and hoof group required",
            ["scope"] = "One native body holds upper geometry and road/cart wheels. Four actual articulated hoof volumes constrain movement by guarded queries, with separate coherent full-sole support; they are not native body shapes." };
    }

    private static JsonObject? DescribeContactFaces(CollisionShape3D? source, Vector3 contact)
    {
        if (source?.Shape is not ConcavePolygonShape3D concave || !source.IsInsideTree()) return null;
        static JsonArray V(Vector3 value) => new(value.X, value.Y, value.Z);
        static float EdgeDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            var edge = b - a;
            if (edge.LengthSquared() <= .000000000001f) return point.DistanceTo(a);
            return point.DistanceTo(a + edge * Mathf.Clamp((point - a).Dot(edge) / edge.LengthSquared(), 0, 1));
        }
        var faces = concave.GetFaces(); var result = new JsonArray(); var matching = 0;
        var transform = source.GlobalTransform;
        for (var index = 0; index + 2 < faces.Length; index += 3)
        {
            var a = transform * faces[index]; var b = transform * faces[index + 1]; var c = transform * faces[index + 2];
            var low = a.Min(b).Min(c); var high = a.Max(b).Max(c);
            var nearest = new Vector3(Mathf.Clamp(contact.X, low.X, high.X), Mathf.Clamp(contact.Y, low.Y, high.Y), Mathf.Clamp(contact.Z, low.Z, high.Z));
            if (nearest.DistanceSquaredTo(contact) > .01f) continue;
            matching++; if (result.Count >= 16) continue;
            var normal = (b - a).Cross(c - a).Normalized();
            result.Add(new JsonObject { ["faceIndex"] = index / 3, ["a"] = V(a), ["b"] = V(b), ["c"] = V(c),
                ["normalFromWinding"] = V(normal), ["absoluteNormalY"] = Math.Abs(normal.Y),
                ["planeResidual"] = Math.Abs((contact - a).Dot(normal)),
                ["minimumEdgeDistance"] = Math.Min(EdgeDistance(contact, a, b), Math.Min(EdgeDistance(contact, b, c), EdgeDistance(contact, c, a))) });
        }
        return new JsonObject { ["source"] = source.GetPath().ToString(), ["actualFaceCount"] = faces.Length / 3,
            ["aabbRadiusMetres"] = .10f, ["matchingFaces"] = matching, ["saturated"] = matching > result.Count,
            ["faces"] = result, ["scope"] = "Actual current physics shape faces; diagnostics only, no terrain mutation." };
    }
}
