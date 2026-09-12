using System;
using System.Globalization;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string MainStreetFencedServiceLaneSlug = "main-street-fenced-service-lane";
    private const string ConnectiveStreetShedBypassSlug = "connective-street-shed-bypass";

    private StaticBody3D? _act1BypassCollision;
    private CollisionShape3D? _mainStreetServiceGateCollision;
    private CollisionShape3D? _connectiveShedBypassGateCollision;
    private Node3D? _mainStreetServiceGateHinge;
    private Node3D? _connectiveShedBypassGateHinge;
    private float _connectiveShedBypassOpenYawRadians;
    private bool? _mainStreetFencedServiceLaneFound;
    private bool? _connectiveStreetShedBypassFound;
    private Tween? _mainStreetServiceGateTween;
    private Tween? _connectiveShedBypassGateTween;

    private void BuildAct1BypassDiscoveries()
    {
        var village = _zoneInstances["village_day"] as StyleBenchmarkZone
            ?? throw new InvalidOperationException(
                "Connected Act I layout is missing the village_day interaction zone.");
        var core = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox")
            ?? throw new InvalidOperationException(
                "Act I core world is missing its presentation root.");
        var mainStreet = core.GetNodeOrNull<Node3D>("MainStreet")
            ?? throw new InvalidOperationException("Act I core world is missing MainStreet.");
        var returnStreet = core.GetNodeOrNull<Node3D>("ConnectiveStreetReturn")
            ?? throw new InvalidOperationException(
                "Act I core world is missing ConnectiveStreetReturn.");

        var mainGate = core.GetNodeOrNull<Node3D>(
                "Act1AuthoredExteriorKitPresentation/NeighborParcels/MainStreet/EastParcel/MainStreetEastNeighborGate")
            ?? throw new InvalidOperationException(
                "The active MainStreet east gate is required for the service lane.");
        var mainFence = core.GetNodeOrNull<Node3D>(
                "Act1AuthoredExteriorKitPresentation/NeighborParcels/MainStreet/EastParcel/MainStreetEastNeighborFence")
            ?? throw new InvalidOperationException(
                "The active MainStreet east fence is required for the service lane.");
        var mainFacade = core.GetNodeOrNull<Node3D>(
                "Act1AuthoredExteriorKitPresentation/NeighborParcels/MainStreet/EastParcel/MainStreetEastNeighborFacade")
            ?? throw new InvalidOperationException(
                "The active MainStreet east facade is required for the service lane.");

        var mainFenceStart = BypassMesh(mainFence, "FenceSegment_EndPost_00_LOD0").GlobalPosition;
        var mainFenceEnd = BypassMesh(mainFence, "FenceSegment_EndPost_02_LOD0").GlobalPosition;
        var mainGateLeft = BypassMesh(mainGate, "Gate_PostLeft_LOD0").GlobalPosition;
        var mainGateRight = BypassMesh(mainGate, "Gate_PostRight_LOD0").GlobalPosition;
        var mainGateLatch = BypassMesh(mainGate, "Gate_LatchPlate_LOD0");
        var mainGateForward = BypassHorizontal(mainGate.GlobalTransform.Basis.Z);

        var mainPresentation = new Node3D { Name = "MainStreetFencedServiceLane" };
        SetBypassPresentationMeta(
            mainPresentation,
            "existing east parcel gate/fence/facade; route-only presentation, no collision owner");
        mainStreet.AddChild(mainPresentation);

        _mainStreetServiceGateHinge = ReparentMainStreetGateLeaf(mainGate);
        _mainStreetServiceGateHinge.SetMeta("activePropPath", mainGate.GetPath().ToString());

        var mainRoadSide = BypassGrounded(mainGateLatch.GlobalPosition - mainGateForward * .80f);
        var mainInside = BypassGrounded(mainGate.GlobalPosition + mainGateForward * .95f);
        var mainLaneStart = BypassGrounded(new Vector3(4f, 0f, -6.65f));
        var mainLaneMid = BypassGrounded(new Vector3(4f, 0f, -9.75f));
        var mainBranch = BypassGrounded(new Vector3(3.30f, 0f, -11.85f));
        var mainRoute = new[] { mainRoadSide, mainInside, mainLaneStart, mainLaneMid, mainBranch };
        mainPresentation.SetMeta("routePolyline", FormatBypassRoute(mainRoute));
        mainPresentation.SetMeta("routeRole", "gate -> sheltered lane between active fence and east facade -> FAP branch");
        AddBypassTrack(mainPresentation, "MainStreetServiceLaneGateCrossing", mainRoadSide, mainInside);
        AddBypassTrack(mainPresentation, "MainStreetServiceLaneTrack0", mainInside, mainLaneStart);
        AddBypassTrack(mainPresentation, "MainStreetServiceLaneTrack1", mainLaneStart, mainLaneMid);
        AddBypassTrack(mainPresentation, "MainStreetServiceLaneTrack2", mainLaneMid, mainBranch);

        var mainTarget = DiscoveryTarget(
            village,
            MainStreetFencedServiceLaneSlug,
            new Vector3(.90f, 1.55f, .90f),
            village.ToLocal(mainRoadSide + Vector3.Up * .72f),
            journal: false);
        mainTarget.SetMeta("activePropPath", mainGateLatch.GetPath().ToString());
        mainTarget.SetMeta("targetWorldPosition", mainRoadSide);
        mainTarget.WorldFoleySample = "door_creak";
        mainTarget.SetMeta("physicalAction", "slide the gate latch and use the service lane");
        mainTarget.SetMeta("routePoints", FormatBypassRoute(mainRoute));
        mainTarget.SetMeta("reconnectsTo", "MainStreet -> village-to-fap-branch");

        var connectiveParcel = core.GetNodeOrNull<Node3D>(
                "Act1AuthoredExteriorKitPresentation/NeighborParcels/ConnectiveStreet/DeepParcel/ConnectiveStreetDeepBanyaYardParcel")
            ?? throw new InvalidOperationException(
                "The active connective authored parcel is required for the shed bypass.");
        var connectiveShed = FindDescendants<Node3D>(connectiveParcel)
            .FirstOrDefault(node => string.Equals(
                node.Name.ToString(),
                "VillageParcel_VariantC_BanyaYard_Outbuilding",
                StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "The active connective parcel is missing its outbuilding.");
        var connectiveOpenBay = BypassMesh(connectiveShed, "VariantC_Utility_OpenBay_LOD0");
        _ = connectiveOpenBay.Mesh
            ?? throw new InvalidOperationException(
                "The active connective open bay has no source mesh.");
        // BuildAct1OptionalDiscoveries runs immediately before this route owner,
        // so use its existing bench node as the physical side-pocket destination.
        // This keeps the route tied to the active OpenBay placement instead of
        // duplicating a brittle world-space endpoint.
        var repairBench = returnStreet.GetNodeOrNull<Node3D>("ConnectiveStreetRepairBench")
            ?? throw new InvalidOperationException(
                "The connective shed bypass requires the existing OpenBay repair bench.");
        var openBayOutward = BypassHorizontal(connectiveOpenBay.GlobalTransform.Basis.Z);

        var connectiveYard = FindDescendants<Node3D>(connectiveParcel)
            .FirstOrDefault(node => string.Equals(
                node.Name.ToString(),
                "VillageParcel_VariantC_BanyaYard_Yard",
                StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "The active connective parcel is missing its yard boundary.");
        PrepareConnectiveVariantCGate(
            connectiveYard,
            out var connectiveGateLeft,
            out var connectiveGateRight,
            out var connectiveGateCenter,
            out var connectiveGateDirection,
            out var connectiveGateTop);

        var shedBounds = BypassWorldBounds(connectiveShed);
        var shedCenter = (shedBounds.Position + shedBounds.End) * .5f;
        var gateCenterGround = BypassGrounded(new Vector3(
            connectiveGateCenter.X,
            0f,
            connectiveGateCenter.Z));
        var gateToShed = BypassHorizontal(shedCenter - gateCenterGround);
        var openSign = connectiveGateDirection.Cross(gateToShed).Y >= 0f ? 1f : -1f;
        _connectiveShedBypassOpenYawRadians = openSign * Mathf.DegToRad(92f);
        _connectiveShedBypassGateHinge = ReparentConnectiveVariantCGateBrace(
            connectiveYard,
            connectiveGateLeft,
            connectiveParcel.GetPath().ToString());
        _connectiveShedBypassGateHinge.SetMeta("openClearanceMeters", 1.47f);
        _connectiveShedBypassGateHinge.SetMeta("headerClearHeightMeters", 2.12f);

        // A side path leaves the return road through the actual gate and
        // reaches the existing yard drive behind the shed. It is a quiet
        // explorable pocket; the same gate leads back to the road.
        var returnRoad = GetConnector("house-to-zirat-return");
        var roadEntry = BypassGrounded(BypassNearestPointOnSegment(
            gateCenterGround,
            returnRoad.Start,
            returnRoad.End));
        var roadward = BypassHorizontal(roadEntry - gateCenterGround);
        if ((roadEntry - gateCenterGround).LengthSquared() < .0001f)
        {
            throw new InvalidOperationException(
                "The connective gate must sit beside the house-to-zirat-return road, not on its centreline.");
        }

        const float gateCrossingOffset = 1.30f;
        var gateRoadSide = BypassGrounded(gateCenterGround + roadward * gateCrossingOffset);
        var gateInside = BypassGrounded(gateCenterGround - roadward * gateCrossingOffset);
        var driveEntry = BypassGrounded(new Vector3(-10f, 0f, -27.2f));
        var shedCorner = BypassGrounded(new Vector3(-11.3f, 0f, -26.7f));
        // OptionalDiscoveries places the bench .85m in front of OpenBay. Stop
        // .82m beyond its centre, leaving the authored .29m bench half-depth
        // plus the player's .35m capsule radius clear of its collider.
        const float repairBenchApproachOffset = .82f;
        var repairBenchApproach = BypassGrounded(
            repairBench.GlobalPosition + openBayOutward * repairBenchApproachOffset);
        EnsureBypassSegmentClearOfBounds(
            gateInside,
            driveEntry,
            shedBounds,
            .45f,
            "gate-inside to ConnectiveWestHouseDrive");
        EnsureBypassSegmentClearOfBounds(driveEntry, shedCorner, shedBounds, .45f, "drive to shed corner");
        EnsureBypassSegmentClearOfBounds(
            shedCorner,
            repairBenchApproach,
            shedBounds,
            .45f,
            "shed corner to OpenBay repair bench");

        var connectiveRoute = new[]
        {
            roadEntry,
            gateRoadSide,
            gateInside,
            driveEntry,
            shedCorner,
            repairBenchApproach
        };
        returnStreet.SetMeta(
            "connectiveShedBypassRoute",
            FormatBypassRoute(connectiveRoute));
        returnStreet.SetMeta(
            "connectiveShedBypassGeometry",
            "VariantC_Yard_GatePostLeft/Right widened to 1.62m centres; GateHeader raised to >=2.06m; source GateBrace hinged; continuous rails split at posts");
        AddBypassTrack(returnStreet, "ConnectiveShedBypassTrack0", roadEntry, gateRoadSide);
        AddBypassTrack(returnStreet, "ConnectiveShedBypassGateCrossing", gateRoadSide, gateInside);
        AddBypassTrack(returnStreet, "ConnectiveShedBypassTrack1", gateInside, driveEntry);
        AddBypassTrack(returnStreet, "ConnectiveShedBypassTrack2", driveEntry, shedCorner);
        AddBypassTrack(returnStreet, "ConnectiveShedBypassTrack3", shedCorner, repairBenchApproach);

        var targetPoint = BypassGrounded(gateRoadSide + roadward * .20f);
        var connectiveTarget = DiscoveryTarget(
            village,
            ConnectiveStreetShedBypassSlug,
            new Vector3(.95f, 1.55f, .95f),
            village.ToLocal(targetPoint + Vector3.Up * .72f),
            journal: false);
        connectiveTarget.SetMeta("activePropPath", connectiveOpenBay.GetPath().ToString());
        connectiveTarget.SetMeta("targetWorldPosition", targetPoint);
        connectiveTarget.WorldFoleySample = "door_creak";
        connectiveTarget.SetMeta("physicalAction", "check the exit behind the shed");
        connectiveTarget.SetMeta("routePoints", FormatBypassRoute(connectiveRoute));
        connectiveTarget.SetMeta("routeRole", "house-to-zirat-return -> OpenBay repair bench side pocket; return through the same gate");

        _act1BypassCollision = CreateBypassCollisionBody(village);
        AddBypassCollisionBox(_act1BypassCollision, "MainStreetServiceFence", mainFenceStart, mainFenceEnd, .18f, 1.18f);
        _mainStreetServiceGateCollision = AddBypassCollisionBox(
            _act1BypassCollision,
            "MainStreetServiceGate",
            mainGateLeft,
            mainGateRight,
            .18f,
            1.30f);
        AddBypassBoundsCollision(
            _act1BypassCollision,
            "MainStreetEastFacade",
            BypassWorldBounds(mainFacade, IsBypassFacadeBlockingMesh));
        AddBypassBoundsCollision(_act1BypassCollision, "ConnectiveServiceShed", shedBounds);
        var connectiveDwelling = connectiveParcel.FindChild("VillageParcel_VariantC_BanyaYard_Dwelling", true, false) as Node3D
            ?? throw new InvalidOperationException("Connective yard is missing its dwelling boundary.");
        AddBypassBoundsCollision(_act1BypassCollision, "ConnectiveServiceDwelling", BypassWorldBounds(connectiveDwelling));
        // Retained posts, header and split rails must keep their physical
        // boundary when the moving brace opens. Their authored mesh bounds
        // preserve the aperture instead of filling it with one fence AABB.
        foreach (var mesh in FindDescendants<MeshInstance3D>(connectiveYard)
            .Where(mesh => mesh.Visible
                && mesh.Name.ToString().StartsWith("VariantC_Yard_", StringComparison.Ordinal)
                && mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)
                && !mesh.Name.ToString().Contains("GateBrace", StringComparison.Ordinal)))
            AddBypassMeshCollision(_act1BypassCollision, mesh);
        foreach (var mesh in FindDescendants<MeshInstance3D>(mainGate)
            .Where(mesh => mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)
                && (mesh.Name.ToString().Contains("Gate_Post", StringComparison.Ordinal)
                    || mesh.Name.ToString().Contains("Gate_Header", StringComparison.Ordinal))))
            AddBypassMeshCollision(_act1BypassCollision, mesh);
        _connectiveShedBypassGateCollision = AddBypassCollisionBox(
            _act1BypassCollision,
            "ConnectiveShedBypassGate",
            connectiveGateLeft,
            connectiveGateRight,
            .18f,
            2.06f);
        // These authored boundaries now border the rear-house discovery route.
        // Triangle collision preserves real door/window apertures; a whole-house
        // box would cover the entry used by the existing interaction target.
        foreach (var path in new[]
        {
            "Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade",
            "Act1AuthoredExteriorKitPresentation/DistantPerimeterParcels/PerimeterWestStreetFacade",
            "Act1AuthoredExteriorKitPresentation/NeighborParcels/HouseExterior/WestSideParcel/HouseExteriorWestNeighborFence"
        })
        {
            var boundary = core.GetNode<Node3D>(path);
            foreach (var mesh in FindDescendants<MeshInstance3D>(boundary).Where(mesh =>
                mesh.Mesh is not null && mesh.Visible
                && mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)
                && (boundary.Name.ToString().EndsWith("Fence", StringComparison.Ordinal)
                    || mesh.Name.ToString().Contains("_Wall_", StringComparison.Ordinal)
                    || mesh.Name.ToString().Contains("_Foundation_", StringComparison.Ordinal)
                    || mesh.Name.ToString().Contains("_Leaf_", StringComparison.Ordinal))))
                _act1BypassCollision.AddChild(new CollisionShape3D
                {
                    Name = "RearBoundary_" + mesh.Name,
                    Transform = _act1BypassCollision.GlobalTransform.AffineInverse() * mesh.GlobalTransform,
                    Shape = mesh.Mesh!.CreateTrimeshShape()
                }, forceReadableName: true);
        }
        SetBypassCollisionEnabled(_act1BypassCollision, false);
    }

    private void UpdateAct1BypassDiscoveries()
    {
        if (_runtimeBridge?.ActiveSceneId is null) return;
        var knowledge = _runtimeBridge.SelectRuntimeState().GetProperty("knowledge");
        bool Found(string slug) => knowledge.TryGetProperty(DiscoveryPrefix + slug, out var entry)
            && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";
        var exterior = ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";

        var mainFound = Found(MainStreetFencedServiceLaneSlug);
        if (_mainStreetServiceGateHinge is not null && _mainStreetFencedServiceLaneFound != mainFound)
        {
            _mainStreetServiceGateTween?.Kill();
            var targetYaw = mainFound ? Mathf.DegToRad(92f) : 0f;
            if (_mainStreetFencedServiceLaneFound == false && mainFound && ActiveZoneId == "village_day")
            {
                _mainStreetServiceGateTween = CreateTween();
                _mainStreetServiceGateTween.TweenProperty(
                    _mainStreetServiceGateHinge,
                    "rotation:y",
                    targetYaw,
                    .45f);
            }
            else
            {
                _mainStreetServiceGateHinge.Rotation = new Vector3(0f, targetYaw, 0f);
            }
            _mainStreetFencedServiceLaneFound = mainFound;
        }

        var connectiveFound = Found(ConnectiveStreetShedBypassSlug);
        if (_connectiveShedBypassGateHinge is not null
            && _connectiveStreetShedBypassFound != connectiveFound)
        {
            _connectiveShedBypassGateTween?.Kill();
            var targetYaw = connectiveFound ? _connectiveShedBypassOpenYawRadians : 0f;
            if (_connectiveStreetShedBypassFound == false && connectiveFound && ActiveZoneId == "village_day")
            {
                _connectiveShedBypassGateTween = CreateTween();
                _connectiveShedBypassGateTween.TweenProperty(
                    _connectiveShedBypassGateHinge,
                    "rotation:y",
                    targetYaw,
                    .45f);
            }
            else
            {
                _connectiveShedBypassGateHinge.Rotation = new Vector3(0f, targetYaw, 0f);
            }
            _connectiveStreetShedBypassFound = connectiveFound;
        }

        SetBypassCollisionEnabled(_act1BypassCollision, exterior);
        if (_mainStreetServiceGateCollision is not null)
            _mainStreetServiceGateCollision.Disabled = !exterior || mainFound;
        if (_connectiveShedBypassGateCollision is not null)
            _connectiveShedBypassGateCollision.Disabled = !exterior || connectiveFound;
    }

    private static void SetBypassPresentationMeta(Node3D node, string routeRole)
    {
        node.SetMeta("presentationOnly", true);
        node.SetMeta("visualOnly", true);
        node.SetMeta("collisionOwner", "none");
        node.SetMeta("navigationOwner", "none");
        node.SetMeta("interactionOwner", "RuntimeBridge / existing InteractionTarget");
        node.SetMeta("runtimeStateOwnership", "RuntimeBridge");
        node.SetMeta("routeRole", routeRole);
    }

    private static Node3D ReparentMainStreetGateLeaf(Node3D gate)
    {
        var hinge = new Node3D
        {
            Name = "MainStreetServiceGateHinge",
            Position = new Vector3(-1.32f, 0f, 0f)
        };
        SetBypassPresentationMeta(hinge, "existing Gate_CrookedTimber leaf hinged at the authored left post");
        gate.AddChild(hinge);

        var moved = 0;
        var leafNames = new[]
        {
            "Gate_Picket_00_LOD0",
            "Gate_Picket_01_LOD0",
            "Gate_Picket_02_LOD0",
            "Gate_Picket_03_LOD0",
            "Gate_Picket_04_LOD0",
            "Gate_Picket_05_LOD0",
            "Gate_BackBrace_LOD0",
            "Gate_CrossBrace_LOD0",
            "Gate_Handle_LOD0"
        };
        foreach (var leafName in leafNames)
        {
            var leaf = FindDescendants<MeshInstance3D>(gate)
                .FirstOrDefault(node => string.Equals(
                    node.Name.ToString(), leafName, StringComparison.Ordinal));
            if (leaf is null) continue;
            foreach (var pair in MeshPair(gate, leaf).ToArray())
            {
                if (pair == hinge) continue;
                var sourceParent = pair.GetParent()
                    ?? throw new InvalidOperationException($"Gate leaf '{pair.Name}' has no parent.");
                var global = pair.GlobalTransform;
                sourceParent.RemoveChild(pair);
                hinge.AddChild(pair);
                pair.GlobalTransform = global;
                moved++;
            }
        }

        if (moved == 0)
        {
            throw new InvalidOperationException(
                "The active MainStreet gate has no reparentable leaf geometry.");
        }
        return hinge;
    }

    private static Node3D ReparentConnectiveVariantCGateBrace(
        Node3D yard,
        Vector3 hingeWorld,
        string sourceParcelPath)
    {
        var brace = BypassMesh(yard, "VariantC_Yard_GateBrace_LOD0");
        var hinge = new Node3D { Name = "ConnectiveVariantCGateHinge" };
        SetBypassPresentationMeta(hinge, "existing VariantC_Yard_GateBrace hinged at widened authored left post");
        hinge.SetMeta("activePropPath", sourceParcelPath);
        yard.AddChild(hinge);
        hinge.GlobalPosition = hingeWorld;

        var moved = 0;
        foreach (var pair in MeshPair(yard, brace).ToArray())
        {
            var sourceParent = pair.GetParent()
                ?? throw new InvalidOperationException($"Gate brace '{pair.Name}' has no parent.");
            var global = pair.GlobalTransform;
            sourceParent.RemoveChild(pair);
            hinge.AddChild(pair);
            pair.GlobalTransform = global;
            moved++;
        }

        if (moved == 0)
        {
            throw new InvalidOperationException(
                "The active VariantC gate has no reparentable GateBrace geometry.");
        }
        return hinge;
    }

    private static void PrepareConnectiveVariantCGate(
        Node3D yard,
        out Vector3 gateLeft,
        out Vector3 gateRight,
        out Vector3 gateCenter,
        out Vector3 gateDirection,
        out float headerTop)
    {
        var left = BypassMesh(yard, "VariantC_Yard_GatePostLeft_LOD0");
        var right = BypassMesh(yard, "VariantC_Yard_GatePostRight_LOD0");
        var header = BypassMesh(yard, "VariantC_Yard_GateHeader_LOD0");
        var oldLeft = left.GlobalPosition;
        var oldRight = right.GlobalPosition;
        var oldCenter = (oldLeft + oldRight) * .5f;
        gateDirection = BypassHorizontal(oldRight - oldLeft);
        const float targetCenterSpacing = 1.62f;
        gateLeft = oldCenter - gateDirection * (targetCenterSpacing * .5f);
        gateRight = oldCenter + gateDirection * (targetCenterSpacing * .5f);
        gateLeft.Y = oldLeft.Y;
        gateRight.Y = oldRight.Y;

        MoveMeshPairWorld(yard, left, gateLeft - oldLeft);
        MoveMeshPairWorld(yard, right, gateRight - oldRight);
        gateCenter = (gateLeft + gateRight) * .5f;

        const float targetHeaderLength = 1.90f;
        var headerBounds = BypassMeshBounds(header);
        var gateGround = AgentBAct1HeightField.CollisionGround(gateCenter.X, gateCenter.Z);
        // Clearance is measured beneath the header, not at its upper face.
        var headerBottom = Mathf.Max(headerBounds.Position.Y, gateGround + 2.12f);
        headerTop = headerBottom + headerBounds.Size.Y;
        foreach (var headerMesh in MeshPair(yard, header).ToArray())
        {
            var centerDelta = gateCenter - headerMesh.GlobalPosition;
            centerDelta.Y = 0f;
            headerMesh.GlobalPosition += centerDelta;
            StretchMeshAlongLocalX(headerMesh, targetHeaderLength);
            var bounds = BypassMeshBounds(headerMesh);
            headerMesh.GlobalPosition += Vector3.Up * (headerBottom - bounds.Position.Y);
        }

        // This stone sill sits in the ground; an exposed vertical lip would
        // stop the first-person capsule even with the leaf fully open.
        var threshold = BypassMesh(yard, "VariantC_Yard_GateThreshold_LOD0");
        var sillBounds = BypassMeshBounds(threshold);
        var sillGround = new[]
        {
            AgentBAct1HeightField.CollisionGround(sillBounds.Position.X, sillBounds.Position.Z),
            AgentBAct1HeightField.CollisionGround(sillBounds.End.X, sillBounds.Position.Z),
            AgentBAct1HeightField.CollisionGround(sillBounds.Position.X, sillBounds.End.Z),
            AgentBAct1HeightField.CollisionGround(sillBounds.End.X, sillBounds.End.Z)
        }.Min();
        MoveMeshPairWorld(yard, threshold, Vector3.Up * (sillGround + .015f - sillBounds.End.Y));

        const float postTopOverlap = .03f;
        foreach (var post in new[] { left, right })
            ExtendPostToTop(yard, post, headerTop - postTopOverlap);

        foreach (var railName in new[]
        {
            "VariantC_Yard_Rail_00_LOD0",
            "VariantC_Yard_Rail_01_LOD0"
        })
        {
            SplitVariantCYardRail(
                yard,
                BypassMesh(yard, railName),
                gateLeft,
                gateRight,
                gateDirection);
        }
    }

    private static void ExtendPostToTop(Node3D root, MeshInstance3D source, float targetTop)
    {
        foreach (var post in MeshPair(root, source).ToArray())
        {
            var before = BypassMeshBounds(post);
            var targetHeight = targetTop - before.Position.Y;
            if (targetHeight <= .10f)
                throw new InvalidOperationException($"Gate post '{post.Name}' cannot reach the raised header.");
            var currentHeight = before.Size.Y;
            if (currentHeight <= .01f)
                throw new InvalidOperationException($"Gate post '{post.Name}' has no usable vertical extent.");
            post.Scale = new Vector3(post.Scale.X, post.Scale.Y * (targetHeight / currentHeight), post.Scale.Z);
            var after = BypassMeshBounds(post);
            post.GlobalPosition += Vector3.Up * (before.Position.Y - after.Position.Y);
        }
    }

    private static void SplitVariantCYardRail(
        Node3D yard,
        MeshInstance3D source,
        Vector3 gateLeft,
        Vector3 gateRight,
        Vector3 gateDirection)
    {
        if (source.Mesh is null)
            throw new InvalidOperationException($"VariantC yard rail '{source.Name}' has no source mesh.");

        var localBounds = source.Mesh.GetAabb();
        var sourceBasis = source.GlobalTransform.Basis;
        var axis = sourceBasis.X.Normalized();
        if (axis.Dot(gateDirection) < 0f)
            axis = -axis;
        var worldCenter = source.GlobalTransform * localBounds.GetCenter();
        var worldLength = localBounds.Size.X * sourceBasis.X.Length();
        if (worldLength < .40f)
            throw new InvalidOperationException($"VariantC yard rail '{source.Name}' is too short to split.");
        var sourceStart = worldCenter - axis * (worldLength * .5f);
        var sourceEnd = worldCenter + axis * (worldLength * .5f);
        if ((sourceEnd - sourceStart).Dot(gateDirection) < 0f)
            (sourceStart, sourceEnd) = (sourceEnd, sourceStart);

        var leftEnd = gateLeft - gateDirection * .10f;
        var rightStart = gateRight + gateDirection * .10f;
        leftEnd.Y = source.GlobalPosition.Y;
        rightStart.Y = source.GlobalPosition.Y;
        var segments = new[]
        {
            (Label: "Left", Start: sourceStart, End: leftEnd),
            (Label: "Right", Start: rightStart, End: sourceEnd)
        };
        var pair = MeshPair(yard, source).ToArray();
        foreach (var sourceMesh in pair)
        {
            sourceMesh.Visible = false;
            sourceMesh.SetMeta("suppressionReason", "source continuous rail split at widened VariantC gate opening");
            foreach (var segment in segments)
            {
                var direction = segment.End - segment.Start;
                var segmentLength = new Vector2(direction.X, direction.Z).Length();
                if (segmentLength < .20f) continue;
                var clone = sourceMesh.Duplicate() as MeshInstance3D
                    ?? throw new InvalidOperationException($"VariantC yard rail '{sourceMesh.Name}' could not be duplicated.");
                var lodSuffix = sourceMesh.Name.ToString().EndsWith("_LOD1", StringComparison.Ordinal)
                    ? "_LOD1"
                    : "_LOD0";
                var baseName = sourceMesh.Name.ToString()[..^5];
                clone.Name = $"{baseName}_{segment.Label}{lodSuffix}";
                yard.AddChild(clone);
                clone.GlobalTransform = sourceMesh.GlobalTransform;
                var ratio = segmentLength / worldLength;
                clone.Scale = new Vector3(clone.Scale.X * ratio, clone.Scale.Y, clone.Scale.Z);
                var cloneCenter = clone.GlobalTransform * localBounds.GetCenter();
                clone.GlobalPosition += (segment.Start + segment.End) * .5f - cloneCenter;
                clone.Visible = true;
                clone.SetMeta("presentationOnly", true);
                clone.SetMeta("visualOnly", true);
                clone.SetMeta("collisionOwner", "none");
                clone.SetMeta("routeRole", "split source VariantC fence rail; gate opening remains clear");
                clone.SetMeta("splitFrom", sourceMesh.Name.ToString());
            }
        }
    }

    private static MeshInstance3D BypassMesh(Node3D root, string name) =>
        FindDescendants<MeshInstance3D>(root)
            .FirstOrDefault(mesh => string.Equals(
                mesh.Name.ToString(), name, StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"Active authored bypass geometry is missing mesh '{name}' under {root.GetPath()}.");

    private static MeshInstance3D? MatchingLod(Node3D root, MeshInstance3D source)
    {
        var name = source.Name.ToString();
        if (!name.EndsWith("_LOD0", StringComparison.Ordinal)) return null;
        var lod1Name = name[..^5] + "_LOD1";
        return FindDescendants<MeshInstance3D>(root)
            .FirstOrDefault(mesh => string.Equals(
                mesh.Name.ToString(), lod1Name, StringComparison.Ordinal));
    }

    private static MeshInstance3D[] MeshPair(Node3D root, MeshInstance3D source)
    {
        var lod1 = MatchingLod(root, source);
        return lod1 is null ? new[] { source } : new[] { source, lod1 };
    }

    private static void MoveMeshPairWorld(Node3D root, MeshInstance3D source, Vector3 delta)
    {
        foreach (var mesh in MeshPair(root, source))
            mesh.GlobalPosition += delta;
    }

    private static void StretchMeshAlongLocalX(MeshInstance3D mesh, float targetWorldLength)
    {
        if (mesh.Mesh is null)
            throw new InvalidOperationException($"Mesh '{mesh.Name}' has no geometry to stretch.");
        var currentWorldLength = mesh.Mesh.GetAabb().Size.X * mesh.GlobalTransform.Basis.X.Length();
        if (currentWorldLength < .01f)
            throw new InvalidOperationException($"Mesh '{mesh.Name}' has no usable local X extent.");
        var ratio = targetWorldLength / currentWorldLength;
        mesh.Scale = new Vector3(mesh.Scale.X * ratio, mesh.Scale.Y, mesh.Scale.Z);
    }

    private static Aabb BypassMeshBounds(MeshInstance3D mesh) =>
        mesh.Mesh is null
            ? throw new InvalidOperationException($"Mesh '{mesh.Name}' has no geometry.")
            : mesh.GlobalTransform * mesh.Mesh.GetAabb();

    private static Vector3 BypassHorizontal(Vector3 value)
    {
        value.Y = 0f;
        return value.LengthSquared() > .0001f ? value.Normalized() : Vector3.Forward;
    }

    private static Vector3 BypassNearestPointOnSegment(
        Vector3 point,
        Vector3 start,
        Vector3 end)
    {
        var direction = end - start;
        direction.Y = 0f;
        var lengthSquared = direction.LengthSquared();
        if (lengthSquared < .0001f) return start;
        var t = Mathf.Clamp((point - start).Dot(direction) / lengthSquared, 0f, 1f);
        var result = start + direction * t;
        result.Y = 0f;
        return result;
    }

    private static void EnsureBypassSegmentClearOfBounds(
        Vector3 start,
        Vector3 end,
        Aabb bounds,
        float clearance,
        string label)
    {
        var minX = bounds.Position.X - clearance;
        var maxX = bounds.End.X + clearance;
        var minZ = bounds.Position.Z - clearance;
        var maxZ = bounds.End.Z + clearance;
        var dx = end.X - start.X;
        var dz = end.Z - start.Z;
        var tMin = 0f;
        var tMax = 1f;
        foreach (var (origin, delta, min, max) in new[]
        {
            (start.X, dx, minX, maxX),
            (start.Z, dz, minZ, maxZ)
        })
        {
            if (Mathf.Abs(delta) < .0001f)
            {
                if (origin < min || origin > max)
                    return;
                continue;
            }
            var a = (min - origin) / delta;
            var b = (max - origin) / delta;
            if (a > b) (a, b) = (b, a);
            tMin = Mathf.Max(tMin, a);
            tMax = Mathf.Min(tMax, b);
            if (tMin > tMax)
                return;
        }
        if (tMin <= tMax && tMax >= 0f && tMin <= 1f)
            throw new InvalidOperationException($"Bypass route segment '{label}' intersects the active shed bounds.");
    }

    private static Vector3 BypassGrounded(Vector3 point) =>
        new(point.X, AgentBAct1HeightField.CollisionGround(point.X, point.Z), point.Z);

    private static string FormatBypassRoute(Vector3[] points) =>
        string.Join('|', points.Select(point =>
            $"{point.X.ToString("0.00", CultureInfo.InvariantCulture)},{point.Z.ToString("0.00", CultureInfo.InvariantCulture)}"));

    private static void AddBypassTrack(Node3D parent, string name, Vector3 start, Vector3 end)
    {
        AddVisualLandformSegment(
            parent,
            name,
            start,
            end,
            1.04f,
            .025f,
            "625747",
            "earth",
            .02f);
        var track = parent.GetNodeOrNull<MeshInstance3D>(name);
        track?.SetMeta("routeRole", "bounded bypass track between active authored props; visual-only");
    }

    private static StaticBody3D CreateBypassCollisionBody(Node3D parent)
    {
        var body = new StaticBody3D { Name = "Act1BypassCollision" };
        body.CollisionLayer = 0u;
        body.CollisionMask = 0u;
        body.SetMeta("collisionOwner", "Act1ConnectedWorld/BypassDiscoveries");
        body.SetMeta("presentationOwner", "Act1ConnectedWorld/Act1CoreWorldGreybox");
        body.SetMeta("interiorPolicy", "disabled while house_old_pc or fap_clinic is active");
        body.SetMeta("routePolicy", "main-street fenced lane and connective shed bypass only");
        parent.AddChild(body, forceReadableName: true);
        return body;
    }

    private static CollisionShape3D AddBypassCollisionBox(
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
            throw new InvalidOperationException($"Bypass collision '{name}' has no usable length.");
        }

        var center = (start + end) * .5f;
        var grounded = BypassGrounded(center);
        grounded.Y += height * .5f;
        var shape = new CollisionShape3D
        {
            Name = name,
            Position = body.ToLocal(grounded),
            RotationDegrees = new Vector3(0f, Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z)), 0f),
            Shape = new BoxShape3D { Size = new Vector3(thickness, height, length) }
        };
        body.AddChild(shape, forceReadableName: true);
        return shape;
    }

    private static void AddBypassBoundsCollision(StaticBody3D body, string name, Aabb bounds)
    {
        var center = (bounds.Position + bounds.End) * .5f;
        var shape = new CollisionShape3D
        {
            Name = name,
            Position = body.ToLocal(center),
            Shape = new BoxShape3D { Size = bounds.Size }
        };
        body.AddChild(shape, forceReadableName: true);
    }

    private static void AddBypassMeshCollision(StaticBody3D body, MeshInstance3D mesh)
    {
        if (mesh.Mesh is null) return;
        var bounds = mesh.Mesh.GetAabb();
        var local = body.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        body.AddChild(new CollisionShape3D
        {
            Name = mesh.Name + "Collision",
            Transform = new Transform3D(local.Basis.Orthonormalized(), local * bounds.GetCenter()),
            Shape = new BoxShape3D { Size = bounds.Size * local.Basis.Scale.Abs() }
        });
        mesh.SetMeta("collisionOwner", body.GetPath().ToString());
    }

    private static void SetBypassCollisionEnabled(StaticBody3D? body, bool enabled)
    {
        if (body is null || !GodotObject.IsInstanceValid(body)) return;
        body.CollisionLayer = enabled ? 1u : 0u;
        body.CollisionMask = enabled ? 1u : 0u;
        foreach (var shape in FindDescendants<CollisionShape3D>(body))
            shape.Disabled = !enabled;
    }

    private static bool IsBypassFacadeBlockingMesh(MeshInstance3D mesh)
    {
        for (Node? current = mesh; current is not null; current = current.GetParent())
        {
            if (current.Name.ToString() is "MainStreetSideWindowReveal"
                or "MainStreetSideWindowShutterHinge")
            {
                return false;
            }
        }
        return true;
    }

    private static Aabb BypassWorldBounds(
        Node3D root,
        Func<MeshInstance3D, bool>? include = null)
    {
        var hasBounds = false;
        var min = Vector3.Zero;
        var max = Vector3.Zero;
        foreach (var mesh in FindDescendants<MeshInstance3D>(root))
        {
            if (include is not null && !include(mesh)) continue;
            if (mesh.Mesh is null) continue;
            var bounds = mesh.GlobalTransform * mesh.Mesh.GetAabb();
            if (!hasBounds)
            {
                min = bounds.Position;
                max = bounds.End;
                hasBounds = true;
                continue;
            }

            min = new Vector3(
                Mathf.Min(min.X, bounds.Position.X),
                Mathf.Min(min.Y, bounds.Position.Y),
                Mathf.Min(min.Z, bounds.Position.Z));
            max = new Vector3(
                Mathf.Max(max.X, bounds.End.X),
                Mathf.Max(max.Y, bounds.End.Y),
                Mathf.Max(max.Z, bounds.End.Z));
        }

        if (!hasBounds)
        {
            throw new InvalidOperationException(
                $"Active authored bypass prop '{root.GetPath()}' has no mesh bounds.");
        }
        return new Aabb(min, max - min);
    }
}
