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
        new(34.5f, 0f, -26.05f),
        new(34.5f, 0f, -24.4f),
        new(32f, 0f, -24.2f),
        new(28f, 0f, -26.2f)
    ];

    private Node3D? _fapServiceGatePivot;
    private StaticBody3D? _fapServiceGateCollision;
    private StaticBody3D? _fapServiceFenceCollision;
    private bool? _fapServicePathFound;
    private Tween? _fapServiceGateTween;

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
        fapPresentation.AddChild(presentation);

        // A small U-shaped parcel: the existing gate is the west end of the
        // north edge, the west/south runs close the shed-side parcel, and the
        // east side remains the permanent open return. Each visible run and
        // its collider use the same endpoint pair.
        var fenceSegments = new[]
        {
            ("FapServiceFenceNorthEast", new Vector3(35.75f, 0f, -25.2f), new Vector3(40.5f, 0f, -25.2f)),
            ("FapServiceFenceWest", new Vector3(33.25f, 0f, -25.2f), new Vector3(33.25f, 0f, -33.6f)),
            ("FapServiceFenceSouth", new Vector3(33.25f, 0f, -33.6f), new Vector3(40.5f, 0f, -33.6f))
        };
        foreach (var (name, start, end) in fenceSegments)
        {
            AddVisualFenceRun(presentation, name, start, end);
        }

        for (var index = 0; index < FapServiceLoopPoints.Length - 1; index++)
        {
            AddFapServicePathPlanks(
                presentation,
                $"FapServicePath{index}",
                FapServiceLoopPoints[index],
                FapServiceLoopPoints[index + 1]);
        }

        // The gate occupies the west end of the north parcel edge at the
        // authored FAP gate anchor (34.5,-25.2). The latch is on its east post;
        // the player approaches it from the parcel interior, to the south.
        var gateWorld = new Vector3(34.5f, 0f, -25.2f);
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
            new(35.25f, 1.05f, -25.82f),
            journal: false);
        target.SetMeta("targetRole", "inside east-post service-gate latch");
        target.SetMeta("targetWorldPosition", new Vector3(35.69f, 0f, -25.29f));
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
        _fapServiceFenceCollision.AddChild(new CollisionShape3D
        {
            Name = "ServiceShedBody",
            Transform = _fapServiceFenceCollision.GlobalTransform.AffineInverse() * shedBody.GlobalTransform
                * new Transform3D(Basis.Identity, shedBounds.GetCenter()),
            Shape = new BoxShape3D { Size = shedBounds.Size }
        });

        _fapServiceGateCollision = CreateFapCollisionBody(village, "FapServiceGateCollision");
        AddFapCollisionBox(
            _fapServiceGateCollision,
            "GateLeafCollision",
            new(33.25f, 0f, -25.2f),
            new(35.75f, 0f, -25.2f),
            .12f,
            1.05f);
        SetFapCollisionEnabled(_fapServiceFenceCollision, false);
        SetFapCollisionEnabled(_fapServiceGateCollision, false);
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

    private static void AddFapServicePathPlanks(Node3D parent, string name, Vector3 start, Vector3 end)
    {
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length < .25f)
        {
            return;
        }

        var pieceCount = Mathf.Max(1, Mathf.CeilToInt(length / 1.8f));
        var yaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
        for (var index = 0; index < pieceCount; index++)
        {
            var a = start.Lerp(end, index / (float)pieceCount);
            var b = start.Lerp(end, (index + 1f) / pieceCount);
            var pieceDirection = b - a;
            var pieceLength = new Vector2(pieceDirection.X, pieceDirection.Z).Length();
            var center = parent.ToLocal(GroundedFapPoint((a + b) * .5f));
            var plank = AddVisualBox(
                parent,
                $"{name}Plank{index}",
                new Vector3(1.18f, .055f, pieceLength),
                center,
                "685b49",
                "wood",
                yawDegrees: yaw);
            plank.SetMeta("visualOnly", true);
            plank.SetMeta("routeRole", "FAP service-yard reconnecting path; no collision");
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
