using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string FapServiceExplorationSlug = "fap-exterior-service-path";

    // The east side is the always-open return. The west gate is at the west
    // end of the north service-yard edge and is aligned with the authored FAP
    // gate anchor. Before opening, the player enters from the east and reaches
    // the inside latch; after opening, the same loop exits through the gate and
    // reconnects to the branch road. The route clears the authored shed AABB
    // (world X 35.606..39.932, Z -33.323..-29.541 after the -12 degree mount).
    private static readonly Vector3[] FapServiceLoopPoints =
    [
        new(28f, 0f, -26.2f),
        new(32f, 0f, -24.2f),
        new(38f, 0f, -23.8f),
        new(41.5f, 0f, -25.0f),
        new(41.5f, 0f, -28.4f),
        new(40.8f, 0f, -28.4f),
        new(34.5f, 0f, -28.4f),
        new(34.5f, 0f, -27.85f),
        new(34.5f, 0f, -24.4f),
        new(32f, 0f, -24.2f),
        new(28f, 0f, -26.2f)
    ];

    private Node3D? _fapServiceGatePivot;
    private StaticBody3D? _fapServiceGateCollision;
    private StaticBody3D? _fapServiceFenceCollision;
    private bool? _fapServicePathFound;
    private Tween? _fapServiceGateTween;
    private Node3D? _fapServiceCabinetDetail;
    private Aabb _fapServiceCabinetBounds;

    private void AlignFapClinicArchitecture()
    {
        var facade = GetNode<Node3D>("Act1CoreWorldGreybox/FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredFacade");
        var porch = GetNode<Node3D>("Act1CoreWorldGreybox/FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredEntryPorch");
        var door = FindDescendants<MeshInstance3D>(facade).Single(mesh => mesh.Name == "FapFacade_DoorPanel_LOD0");
        var doorCentre = door.GlobalTransform * door.Mesh.GetAabb().GetCenter();
        // Level the complete building against its entrance, then scribe the
        // foundation to the terrain. The room and porch share this same datum.
        var baseY = AgentBAct1HeightField.CollisionGround(doorCentre.X, doorCentre.Z) - .04f;
        facade.GlobalPosition = new(facade.GlobalPosition.X, baseY, facade.GlobalPosition.Z);
        porch.GlobalPosition = new(porch.GlobalPosition.X, baseY, porch.GlobalPosition.Z);
        var clinic = _zoneInstances["fap_clinic"];
        clinic.GlobalTransform = facade.GlobalTransform * new Transform3D(Basis.Identity, new Vector3(0, .40f, 0));
        clinic.SetMeta("exteriorArchitectureOwner", facade.GetPath().ToString());
        clinic.SetMeta("fapEnvelopeContract", "fap-room-metric-v1");
        var presentation = porch.GetParent<Node3D>();
        foreach (var physical in new[] { porch, presentation.GetNode<Node3D>("FapAuthoredBench"),
            presentation.GetNode<Node3D>("FapAuthoredNoticeBoard"), presentation.GetNode<Node3D>("FapAuthoredWayfindingBoard") })
        foreach (var mesh in FindDescendants<MeshInstance3D>(physical).Where(mesh => mesh.Mesh is not null
            && mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)))
        {
            var stone = mesh.Name.ToString().Contains("Step_", StringComparison.Ordinal)
                || mesh.Name.ToString().Contains("Threshold", StringComparison.Ordinal);
            if (stone) mesh.SetMeta("requiresTerrainSupport", true);
            var body = new StaticBody3D { Name = "FapPropContact_" + mesh.Name, CollisionLayer = 2, CollisionMask = 1 };
            body.SetMeta("collisionOwner", "act1-exterior-architecture");
            body.SetMeta("footstepSurface", stone ? "stone" : "wood");
            physical.AddChild(body);
            body.AddChild(AuthoredSurfaceContact(body, mesh));
        }
        var entry = _zoneInstances["village_day"].GetNode<InteractionTarget>("RoadToFap");
        doorCentre = door.GlobalTransform * door.Mesh.GetAabb().GetCenter();
        entry.GlobalPosition = doorCentre + facade.GlobalBasis.Z.Normalized() * .20f;
        entry.CollisionLayer = 4;
        entry.CollisionMask = 0;
        entry.SetMeta("authoredDoorSurface", door.GetPath().ToString());
        entry.SetMeta("placementPolicy", "live door surface after exterior/room alignment");
    }

    private void BuildFapServiceExploration()
    {
        var fapPresentation = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox/FapExterior")
            ?? throw new InvalidOperationException(
                "Act I core world is missing the FapExterior presentation zone.");
        var village = _zoneInstances["village_day"] as StyleBenchmarkZone
            ?? throw new InvalidOperationException(
                "Connected Act I layout is missing the village_day interaction zone.");

        // Remove overlapping presentation cues. The replacement parcel below
        // owns exactly the boundaries whose colliders it creates; the old
        // north/east fence runs are hidden so no fixed visual rail crosses the
        // new route or masquerades as an independent collision owner.
        foreach (var path in new[]
                 {
                     "FapClinicOpenServiceGate",
                     "FapClinicServiceBoundaryRail",
                     "FapClinicServiceBoundaryPost0",
                     "FapClinicServiceBoundaryPost1",
                     "FapClinicServiceBoundaryPost2",
                     "FapClinicEastBoundaryRail",
                     "FapClinicEastBoundaryPost0",
                     "FapClinicEastBoundaryPost1",
                     "FapClinicEastBoundaryPost2",
                     "FapClinicEastBoundaryPost3",
                     "FapClinicAuthoredKitPresentation/FapAuthoredServiceGate",
                     "FapClinicAuthoredKitPresentation/FapAuthoredServiceFence"
                 })
        {
            if (fapPresentation.GetNodeOrNull<Node3D>(path) is { } existing)
            {
                HidePresentationNode(existing);
            }
        }

        var presentation = new Node3D { Name = "FapServiceExplorationPresentation" };
        presentation.SetMeta("presentationOnly", true);
        presentation.SetMeta("visualOnly", true);
        presentation.SetMeta("presentationOwner", nameof(Act1ConnectedWorld));
        presentation.SetMeta("interactionOwner", "RuntimeBridge / existing InteractionTarget");
        presentation.SetMeta("runtimeStateOwner", "RuntimeBridge");
        presentation.SetMeta("returnPolicy", "east side stays open as the permanent service-yard return");
        presentation.SetMeta("loopPolyline", string.Join('|', FapServiceLoopPoints.Select(
            point => $"{point.X:0.0},{point.Z:0.0}")));
        presentation.SetMeta("loopWorldPoints", FapServiceLoopPoints);
        fapPresentation.AddChild(presentation);

        // The service fence is recessed from the public bridge-exit carriageway.
        // A small U-shaped parcel: the existing gate is the west end of the
        // north edge, the west/south runs close the shed-side parcel, and the
        // east side remains the permanent open return. Each visible run and
        // its collider use the same endpoint pair.
        var fenceSegments = new[]
        {
            ("FapServiceFenceNorthEast", new Vector3(35.75f, 0f, -27f), new Vector3(40.5f, 0f, -27f)),
            ("FapServiceFenceWest", new Vector3(33.25f, 0f, -27f), new Vector3(33.25f, 0f, -33.6f)),
            ("FapServiceFenceSouth", new Vector3(33.25f, 0f, -33.6f), new Vector3(40.5f, 0f, -33.6f))
        };
        foreach (var (name, start, end) in fenceSegments)
        {
            AddVisualFenceRun(presentation, name, start, end);
        }

        var drawnSegments = new HashSet<(Vector3, Vector3)>();
        var occupiedBoards = new List<Vector2[]>();
        for (var index = 0; index < FapServiceLoopPoints.Length - 1; index++)
        {
            var a = FapServiceLoopPoints[index];
            var b = FapServiceLoopPoints[index + 1];
            if (!drawnSegments.Add((a, b))) continue;
            drawnSegments.Add((b, a));
            AddFapServicePathPlanks(
                presentation,
                $"FapServicePath{index}",
                FapServiceLoopPoints[index],
                FapServiceLoopPoints[index + 1], occupiedBoards);
        }

        // The gate occupies the west end of the north parcel edge at the
        // authored FAP gate anchor (34.5,-27). The latch is on its east post;
        // the player approaches it from the parcel interior, to the south.
        var gateWorld = new Vector3(34.5f, 0f, -27f);
        var gate = new Node3D
        {
            Name = "FapServiceGateInteractive",
            Position = presentation.ToLocal(GroundedFapPoint(gateWorld))
        };
        gate.SetMeta("presentationOnly", true);
        gate.SetMeta("physicalAction", "lift service-yard latch; swing leaf clear");
        presentation.AddChild(gate);

        AddVisualBox(gate, "PostWest", new(.16f, 1.45f, .16f), new(-1.25f, .72f, 0f), "594a39", "wood");
        AddVisualBox(gate, "PostEast", new(.16f, 1.45f, .16f), new(1.25f, .72f, 0f), "594a39", "wood");
        var hinge = new Node3D { Name = "HingeLeaf", Position = new(-1.25f, 0f, 0f) };
        gate.AddChild(hinge);
        _fapServiceGatePivot = hinge;
        AddVisualBox(hinge, "GateLeaf", new(2.50f, .86f, .11f), new(1.25f, .67f, 0f), "6d5943", "wood");
        AddVisualBox(hinge, "GateBrace", new(2.10f, .075f, .08f), new(1.25f, .68f, 0f), "8a6b50", "wood");
        AddVisualBox(gate, "Latch", new(.14f, .16f, .12f), new(1.25f, .90f, -.08f), "49382d", "metal");

        // The target volume is immediately inside the east latch post.
        var target = DiscoveryTarget(
            village,
            FapServiceExplorationSlug,
            new(.60f, 1.0f, .60f),
            new(35.25f, 1.05f, -27.62f),
            journal: false);
        target.SetMeta("targetRole", "inside east-post service-gate latch");
        target.SetMeta("targetWorldPosition", new Vector3(35.69f, 0f, -27.09f));
        target.WorldFoleySample = "door_creak";

        // Fence shapes mirror the three visible runs; only the horizontal gate
        // leaf is state-gated. The east side remains open, so closing the leaf
        // never soft-locks the player in or out of the service parcel.
        _fapServiceFenceCollision = CreateFapCollisionBody(village, "FapServiceFenceCollision");
        foreach (var (name, start, end) in fenceSegments)
        {
            AddFapCollisionBox(_fapServiceFenceCollision, name, start, end, .16f, 1.15f);
        }
        var shedBody = FindDescendants<MeshInstance3D>(fapPresentation.GetNode<Node3D>(
            "FapClinicAuthoredKitPresentation/FapAuthoredServiceShed"))
            .Single(mesh => mesh.Name == "FapServiceShed_Body_LOD0");
        var shedBounds = shedBody.Mesh.GetAabb();
        var shedContact = new CollisionShape3D
        {
            Name = "ServiceShedBody",
            Transform = _fapServiceFenceCollision.GlobalTransform.AffineInverse() * shedBody.GlobalTransform
                * new Transform3D(Basis.Identity, shedBounds.GetCenter()),
            Shape = new BoxShape3D { Size = shedBounds.Size }
        };
        shedContact.SetMeta("authoredSourceMesh", shedBody.GetPath().ToString());
        _fapServiceFenceCollision.AddChild(shedContact);

        _fapServiceGateCollision = CreateFapCollisionBody(village, "FapServiceGateCollision");
        AddFapCollisionBox(
            _fapServiceGateCollision,
            "GateLeafCollision",
            new(33.25f, 0f, -27f),
            new(35.75f, 0f, -27f),
            .12f,
            1.05f);
        SetFapCollisionEnabled(_fapServiceFenceCollision, false);
        SetFapCollisionEnabled(_fapServiceGateCollision, false);
        BuildFapServiceCabinet();
    }

    private void BuildFapServiceCabinet()
    {
        var clinic = (StyleBenchmarkZone)_zoneInstances["fap_clinic"];
        var members = FindDescendants<MeshInstance3D>(clinic)
            .Where(mesh => mesh.Mesh is not null && mesh.Name.ToString().StartsWith("FapInteriorTallStorage_", StringComparison.Ordinal))
            .ToArray();
        var full = members.Where(mesh => mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)).ToArray();
        var carcass = full.Single(mesh => mesh.Name == "FapInteriorTallStorage_Body_LOD0");
        var door = full.Single(mesh => mesh.Name == "FapInteriorTallStorage_DoorRight_LOD0");
        var floor = FindDescendants<MeshInstance3D>(clinic).Single(mesh => mesh.Name == "FapInteriorShell_Floor_LOD0");
        Aabb LocalBounds(MeshInstance3D mesh) => (clinic.GlobalTransform.AffineInverse() * mesh.GlobalTransform) * mesh.Mesh.GetAabb();
        var floorY = LocalBounds(floor).End.Y;
        var lowest = full.Min(mesh => LocalBounds(mesh).Position.Y);
        var shift = floorY - lowest;
        if (Mathf.Abs(shift) > .25f) throw new InvalidOperationException($"FAP cabinet footing differs from its floor by {shift:F3}m.");
        foreach (var mesh in members)
        {
            mesh.SetMeta("serviceCabinetOriginalTransform", mesh.Transform);
            mesh.GlobalPosition += clinic.GlobalBasis.Y.Normalized() * shift;
            mesh.SetMeta("collisionPolicy", "closed cupboard; exact LOD0 contacts owned by FapServiceCabinetCollision");
        }
        _fapServiceCabinetBounds = LocalBounds(carcass);
        var doorBounds = LocalBounds(door);
        var contact = new StaticBody3D { Name = "FapServiceCabinetCollision", CollisionLayer = 1, CollisionMask = 1 };
        contact.SetMeta("collisionOwner", "fap-interior-cabinet");
        contact.SetMeta("groundingShift", shift);
        clinic.AddChild(contact);
        foreach (var mesh in full) contact.AddChild(AuthoredSurfaceContact(clinic, mesh));

        // A visible lower hinge is ordinary cabinet hardware. Its short service
        // approach lies on the open room side; it neither opens the cupboard
        // nor grants a medical document or information about Marat.
        var fixing = new Node3D
        {
            Name = "FapServiceCabinetFixing",
            Position = new(_fapServiceCabinetBounds.End.X - .045f, floorY + .47f, doorBounds.End.Z + .025f)
        };
        clinic.AddChild(fixing);
        _fapServiceCabinetDetail = fixing;
        var leaf = AddVisualBox(fixing, "LowerHingeLeaf", new(.13f, .17f, .018f), Vector3.Zero, "788481", "metal");
        DiscoveryCylinder(fixing, "LowerHingePin", .008f, .008f, .15f, new(.015f, 0, .014f), "68716d");
        foreach (var sign in new[] { -1f, 1f })
        {
            var screw = DiscoveryCylinder(fixing, "HingeScrew" + sign, .006f, .006f, .008f,
                new(-.036f, sign * .051f, .012f), "545d59");
            screw.RotationDegrees = new(90, 0, 0);
        }
        contact.AddChild(AuthoredSurfaceContact(clinic, leaf));
        var target = DiscoveryTarget(clinic, "fap-service-cabinet", new(.23f, .24f, .14f),
            fixing.Position + Vector3.Back * .035f, journal: true);
        target.SetMeta("activePropPath", fixing.GetPath().ToString());
        target.SetMeta("sourceCabinetPath", carcass.GetPath().ToString());
        target.SetMeta("accessAnchor", clinic.ToGlobal(new(_fapServiceCabinetBounds.End.X + .65f,
            floorY + .04f, doorBounds.End.Z + .15f)));
        target.SetMeta("physicalAction", "walk into the existing service corner and inspect the visible lower cabinet hinge");
        target.SetMeta("plotGate", false);
    }

    private bool CanUseFapServiceCabinet(FirstPersonController player)
    {
        if (ActiveZoneId != "fap_clinic" || _fapServiceCabinetDetail is null) return false;
        var clinic = _zoneInstances["fap_clinic"];
        var local = clinic.ToLocal(player.GlobalPosition);
        var openSide = local.X > _fapServiceCabinetBounds.End.X + .25f
            || local.Z > _fapServiceCabinetBounds.End.Z + .42f;
        if (!openSide) return false;
        if (!_observationRayExclusions.Contains(player.GetRid())) _observationRayExclusions.Add(player.GetRid());
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        return ObservationNear(camera, _fapServiceCabinetDetail.GlobalPosition + clinic.GlobalBasis.Z * .03f, 2.4f, .86f);
    }

    private void UpdateFapServiceExploration()
    {
        if (_runtimeBridge?.ActiveSceneId is null)
        {
            return;
        }

        var knowledge = _runtimeBridge.SelectRuntimeState().GetProperty("knowledge");
        var found = knowledge.TryGetProperty(DiscoveryPrefix + FapServiceExplorationSlug, out var entry)
            && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";
        var exteriorActive = ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";

        if (_fapServiceGatePivot is not null && _fapServicePathFound != found)
        {
            _fapServiceGateTween?.Kill();
            // Fold against the inside west fence, away from the return road.
            var openRotation = found ? Mathf.Pi * .52f : 0f;
            if (_fapServicePathFound == false && found && exteriorActive)
            {
                _fapServiceGateTween = CreateTween();
                _fapServiceGateTween.TweenProperty(
                    _fapServiceGatePivot,
                    "rotation:y",
                    openRotation,
                    .45f);
            }
            else
            {
                _fapServiceGatePivot.Rotation = new Vector3(0f, openRotation, 0f);
            }

            _fapServicePathFound = found;
        }

        // Exterior physics is inert for both interiors. Restore it on every
        // logical-zone change/load through the same RuntimeStateChanged seam.
        SetFapCollisionEnabled(_fapServiceFenceCollision, exteriorActive);
        SetFapCollisionEnabled(_fapServiceGateCollision, exteriorActive && !found);
    }

    private static void AddFapServicePathPlanks(Node3D parent, string name, Vector3 start, Vector3 end, List<Vector2[]> occupiedBoards)
    {
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length < .25f)
        {
            return;
        }

        // Narrow transverse boards follow the actual snow/earth support at
        // their four corners. The former 1.8m horizontal sheets sampled only
        // their centres and had no collision, producing buried or floating
        // ends and snow footsteps on visible timber.
        var pieceCount = Mathf.Max(1, Mathf.CeilToInt(length / .32f));
        var across = new Vector3(direction.Z, 0, -direction.X).Normalized() * .59f;
        var body = parent.GetNodeOrNull<StaticBody3D>("FapServicePathCollision");
        if (body is null)
        {
            body = new StaticBody3D { Name = "FapServicePathCollision", CollisionLayer = 1, CollisionMask = 1 };
            body.SetMeta("collisionOwner", "act1-exterior-architecture");
            body.SetMeta("footstepSurface", "wood");
            parent.AddChild(body);
        }
        for (var index = 0; index < pieceCount; index++)
        {
            var a = start.Lerp(end, (index + .035f) / pieceCount);
            var b = start.Lerp(end, (index + .965f) / pieceCount);
            var outline = new[] { a - across, a + across, b + across, b - across }
                .Select(point => new Vector2(point.X, point.Z)).ToArray();
            var pieces = new List<Vector2[]> { outline };
            // The return branch meets an earlier strip at a shallow angle.
            // Cut the new boards around already laid timber; overlapping full
            // rectangles caused two visible/physical surfaces only 0–2mm apart.
            foreach (var occupied in occupiedBoards)
            {
                var remaining = new List<Vector2[]>();
                foreach (var piece in pieces) remaining.AddRange(Geometry2D.ClipPolygons(piece, occupied));
                pieces = remaining;
                if (pieces.Count == 0) break;
            }
            occupiedBoards.Add(outline);
            for (var part = 0; part < pieces.Count; part++)
            {
                var polygon = pieces[part];
                var area = Mathf.Abs(Enumerable.Range(0, polygon.Length)
                    .Sum(vertex => polygon[vertex].Cross(polygon[(vertex + 1) % polygon.Length]))) * .5f;
                if (area < .001f) continue; // discard millimetre slivers at the sawn joint
                var triangles = Geometry2D.TriangulatePolygon(polygon);
                if (triangles.Length < 3) continue;
                if (Geometry2D.IsPolygonClockwise(polygon))
                    throw new InvalidOperationException("A FAP timber joint produced an unsupported polygon hole.");
                var points = new Vector3[polygon.Length * 2];
                for (var corner = 0; corner < polygon.Length; corner++)
                {
                    var ground = GroundedFapPoint(new(polygon[corner].X, 0, polygon[corner].Y));
                    points[corner] = parent.ToLocal(ground + Vector3.Up * .030f);
                    points[corner + polygon.Length] = parent.ToLocal(ground - Vector3.Up * .025f);
                }
                using var surface = new SurfaceTool();
                surface.Begin(Mesh.PrimitiveType.Triangles);
                foreach (var vertex in triangles) surface.AddVertex(points[vertex]);
                for (var vertex = triangles.Length - 1; vertex >= 0; vertex--)
                    surface.AddVertex(points[triangles[vertex] + polygon.Length]);
                for (var corner = 0; corner < polygon.Length; corner++)
                {
                    var next = (corner + 1) % polygon.Length;
                    foreach (var vertex in new[] { corner, corner + polygon.Length, next + polygon.Length,
                        corner, next + polygon.Length, next }) surface.AddVertex(points[vertex]);
                }
                surface.GenerateNormals();
                var plank = new MeshInstance3D
                {
                    Name = $"{name}Plank{index}Part{part}", Mesh = surface.Commit(),
                    MaterialOverride = PainterlyMaterialLibrary.ForColor("685b49", "wood_furniture")
                };
                plank.SetMeta("routeRole", "FAP service-yard reconnecting timber path with matching contact");
                plank.SetMeta("groundContactPolicy", "cut joints with no overlapping boards; edges seated 25mm into physical terrain");
                parent.AddChild(plank);
                body.AddChild(AuthoredSurfaceContact(parent, plank));
            }
        }
    }

    private static StaticBody3D CreateFapCollisionBody(Node3D parent, string name)
    {
        var body = new StaticBody3D { Name = name };
        body.CollisionLayer = 1u;
        body.CollisionMask = 1u;
        body.SetMeta("collisionOwner", "Act1ConnectedWorld/FapServiceExploration");
        body.SetMeta("presentationOwner", "Act1ConnectedWorld/FapExterior");
        body.SetMeta("interiorPolicy", "disabled while house_old_pc or fap_clinic is active");
        parent.AddChild(body, forceReadableName: true);
        return body;
    }

    private static void AddFapCollisionBox(
        StaticBody3D body,
        string name,
        Vector3 start,
        Vector3 end,
        float thickness,
        float height)
    {
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length < .05f)
        {
            return;
        }

        var center = (start + end) * .5f;
        var grounded = GroundedFapPoint(center);
        grounded.Y += height * .5f;
        var yaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
        body.AddChild(new CollisionShape3D
        {
            Name = name,
            Position = body.ToLocal(grounded),
            RotationDegrees = new Vector3(0f, yaw, 0f),
            Shape = new BoxShape3D { Size = new Vector3(thickness, height, length) }
        }, forceReadableName: true);
    }

    private static void SetFapCollisionEnabled(StaticBody3D? body, bool enabled)
    {
        if (body is null || !GodotObject.IsInstanceValid(body))
        {
            return;
        }

        body.CollisionLayer = enabled ? 1u : 0u;
        body.CollisionMask = enabled ? 1u : 0u;
        foreach (var shape in FindDescendants<CollisionShape3D>(body))
        {
            shape.Disabled = !enabled;
        }
    }

    private static Vector3 GroundedFapPoint(Vector3 point) =>
        new(point.X, AgentBAct1HeightField.CollisionGround(point.X, point.Z), point.Z);
}
