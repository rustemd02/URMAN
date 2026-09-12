using System;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string KaraSideTrackSlug = "kara-old-forestry-side-track";
    private const string KaraWarmWindowSlug = "kara-warm-window-clearing";
    private const string KaraBranchProfileSlug = "kara-branch-profile";

    private Node3D? _karaSideTrackBranch;
    private StaticBody3D? _karaSideTrackCollision;
    private StaticBody3D? _karaSideTrackBoundaryCollision;
    private Vector3 _karaSideTrackBranchRest;
    private Vector3 _karaSideTrackBranchCleared;
    private Vector3 _karaSideTrackBranchClearedRotation;
    private bool? _karaSideTrackFound;
    private Tween? _karaSideTrackMove;

    private void BuildAct1KaraOptionalDiscoveries()
    {
        var kara = _zoneInstances["kara_urman_night"] as StyleBenchmarkZone
            ?? throw new InvalidOperationException(
                "Connected Act I layout is missing the kara_urman_night interaction zone.");
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var forest = core.GetNode<Node3D>("KaraForestEdge");
        if (!Act1WorldLayout.TryGetPlacement("kara_urman_night", out var placement))
        {
            return;
        }

        var origin = placement.Origin;
        Vector3 KaraPoint(float x, float localZ) =>
            origin + new Vector3(x, 0f, localZ);
        Vector3 KaraGround(float x, float localZ, float lift = 0f)
        {
            var point = KaraPoint(x, localZ);
            point.Y = AgentBAct1HeightField.CollisionGround(point.X, point.Z) + lift;
            return point;
        }

        // The old track leaves the road at the western shoulder, clears the
        // retained FallenLog_K0 by more than the .35 m player radius, and
        // reconnects at the road south of the root field. The gate is a short
        // brush opening: its east guide meets the visible FallenLog_K0, its
        // west guide is only 1.20 m long, and the normal road remains open.
        var routePoints = new[]
        {
            KaraPoint(-2.20f, 10.50f),
            KaraPoint(-3.45f, 9.60f),
            KaraPoint(-4.30f, 8.45f),
            KaraPoint(-4.88f, 7.35f),
            KaraPoint(-5.55f, 5.55f),
            KaraPoint(-5.65f, 2.90f),
            KaraPoint(-5.85f, -0.15f),
            KaraPoint(-6.15f, -2.60f),
            KaraPoint(-5.25f, -4.50f),
            KaraPoint(-3.55f, -4.30f),
            KaraPoint(-2.10f, -6.45f)
        };
        var sideTrack = new Node3D { Name = "KaraOldForestrySideTrack" };
        sideTrack.SetMeta("presentationOnly", true);
        sideTrack.SetMeta("visualOnly", true);
        sideTrack.SetMeta("collisionOwner", "KaraOldForestryGateCollision/KaraOldForestryBoundaryCollision");
        sideTrack.SetMeta("routeRole", "short west side loop returning to the Kara approach road");
        sideTrack.SetMeta("loopPolyline", "(-2.20,-104.50)|(-3.45,-105.40)|(-4.30,-106.55)|(-4.88,-107.65)|(-5.55,-109.45)|(-5.65,-112.10)|(-5.85,-115.15)|(-6.15,-117.60)|(-5.25,-119.50)|(-3.55,-119.30)|(-2.10,-121.45)");
        sideTrack.SetMeta("clearanceMetres", 1.45f);
        sideTrack.SetMeta("minimumClearanceMetres", 1.20f);
        sideTrack.SetMeta("playerCapsuleRadiusMetres", .35f);
        sideTrack.SetMeta("fixedBoundaryCounterpart", "AgentB_KaraEdgeKit/FallenLog_K0");
        forest.AddChild(sideTrack);

        using (var trackCurve = new Curve3D { BakeInterval = .2f })
        {
            foreach (var point in routePoints)
            {
                trackCurve.AddPoint(sideTrack.ToLocal(point));
            }

            var trackSurface = AddVisualLandformSurface(
                sideTrack,
                "KaraOldForestrySideTrackSurface",
                1.45f,
                .032f,
                trackCurve.GetBakedLength(),
                Vector3.Zero,
                "c9d5d9",
                "snow_trampled",
                0f,
                conformToTerrain: true,
                centerline: trackCurve);
            trackSurface.SetMeta("routeRole", "visual side track; joins the existing road at both ends");
            trackSurface.SetMeta("clearanceMetres", 1.45f);
            trackSurface.SetMeta("physicalRoutePolicy", "continuous AgentB terrain; authored gate is the only added route block");
        }

        var stumpWorld = KaraGround(-6.9f, 1.20f);
        var stump = new Node3D { Name = "KaraForestryLowStump", Position = sideTrack.ToLocal(stumpWorld) };
        stump.SetMeta("presentationOnly", true);
        sideTrack.AddChild(stump);
        DiscoveryCylinder(stump, "StumpBody", .18f, .22f, .46f, new(0f, .23f, 0f), "554234");
        DiscoveryCylinder(stump, "StumpTop", .16f, .16f, .025f, new(0f, .472f, 0f), "8b7657");

        var stackedBranches = new Node3D
        {
            Name = "KaraForestryStackedBranches",
            Position = sideTrack.ToLocal(KaraGround(-7.5f, -2.45f))
        };
        stackedBranches.SetMeta("presentationOnly", true);
        sideTrack.AddChild(stackedBranches);
        AddVisualBox(stackedBranches, "BranchStackLower", new(1.15f, .13f, .13f), new(0f, .08f, 0f), "4b3b30", "wood_bark", yawDegrees: 28f);
        AddVisualBox(stackedBranches, "BranchStackUpper", new(.88f, .11f, .11f), new(.10f, .19f, .02f), "5a4636", "wood_bark", yawDegrees: -18f);

        var gateWorld = KaraGround(-4.88f, 7.35f);
        var gateDirection = HorizontalDirection(routePoints[3] - routePoints[2]);
        var gateYaw = DirectionYaw(gateDirection);
        var gate = new Node3D
        {
            Name = "KaraForestryBranchGate",
            Position = forest.ToLocal(gateWorld),
            RotationDegrees = new Vector3(0f, gateYaw, 0f)
        };
        gate.SetMeta("presentationOnly", true);
        gate.SetMeta("visualOnly", true);
        gate.SetMeta("physicalAction", "move the fallen branch from the old track");
        gate.SetMeta("routeRole", "1.48 m clear brush gate tied into the retained FallenLog_K0");
        gate.SetMeta("fixedBoundaryCounterpart", "AgentB_KaraEdgeKit/FallenLog_K0");
        forest.AddChild(gate);

        // The branch spans the 1.48 m opening between two short visible guide
        // rails. The east rail ends in the retained log; neither rail is a
        // hidden wall or an unpaired collider.
        _karaSideTrackBranchRest = Vector3.Zero;
        _karaSideTrackBranchCleared = new Vector3(1.35f, 0f, .35f);
        _karaSideTrackBranch = new Node3D
        {
            Name = "KaraForestryMovedBranch",
            Position = _karaSideTrackBranchRest
        };
        _karaSideTrackBranch.SetMeta("presentationOnly", true);
        _karaSideTrackBranch.SetMeta("visualOnly", true);
        _karaSideTrackBranch.SetMeta("physicalAction", "move the fallen branch from the old track");
        _karaSideTrackBranch.SetMeta("clearanceAfterMoveMetres", 1.45f);
        gate.AddChild(_karaSideTrackBranch);
        AddCoreForestBranch(_karaSideTrackBranch, "FallenBranchMain",
            new(-.85f, .646f, 0f), new(.85f, .794f, 0f), "4a392d", .105f, .075f, 9);
        AddCoreForestBranch(_karaSideTrackBranch, "FallenBranchFork",
            new(-.55f, .68f, 0f), new(.12f, .97f, .206f), "5b4536", .065f, .03f, 8);
        AddVisualBox(_karaSideTrackBranch, "FallenBranchLatch", new(.14f, .16f, .12f),
            new(.66f, .90f, -.10f), "49382d", "metal");

        // Lay the cleared branch along the bank, then seat its actual lower
        // surface on the same terrain triangles used by visible snow.
        var bankStart = gate.ToGlobal(_karaSideTrackBranchCleared + new Vector3(-.85f, 0f, 0f));
        var bankEnd = gate.ToGlobal(_karaSideTrackBranchCleared + new Vector3(.85f, 0f, 0f));
        var bankRise = AgentBAct1HeightField.CollisionGround(bankEnd.X, bankEnd.Z)
            - AgentBAct1HeightField.CollisionGround(bankStart.X, bankStart.Z);
        _karaSideTrackBranchClearedRotation = new Vector3(0f, 0f,
            Mathf.Atan2(bankRise, 1.70f) - Mathf.Atan2(.148f, 1.70f));
        _karaSideTrackBranch.Position = _karaSideTrackBranchCleared;
        _karaSideTrackBranch.Rotation = _karaSideTrackBranchClearedRotation;
        var mainBranch = _karaSideTrackBranch.GetNode<MeshInstance3D>("FallenBranchMain");
        var groundGap = float.PositiveInfinity;
        foreach (var vertex in mainBranch.Mesh.GetFaces())
        {
            var point = mainBranch.GlobalTransform * vertex;
            groundGap = Mathf.Min(groundGap, point.Y - AgentBAct1HeightField.CollisionGround(point.X, point.Z));
        }
        _karaSideTrackBranchCleared.Y -= groundGap;
        _karaSideTrackBranch.Position = _karaSideTrackBranchRest;
        _karaSideTrackBranch.Rotation = Vector3.Zero;

        AddVisualBox(gate, "GateGuideWest", new(.16f, .82f, 1.20f),
            new(.82f, .41f, .58f), "514333", "wood_bark");
        AddVisualBox(gate, "GateGuideEast", new(.16f, .82f, 1.20f),
            new(-.82f, .41f, .58f), "514333", "wood_bark");
        AddVisualBox(gate, "GateGuideWestPost", new(.19f, 1.08f, .19f),
            new(.82f, .54f, .04f), "594a39", "wood");
        AddVisualBox(gate, "GateGuideEastPost", new(.19f, 1.08f, .19f),
            new(-.82f, .54f, .04f), "594a39", "wood");

        _karaSideTrackCollision = new StaticBody3D
        {
            Name = "KaraOldForestryGateCollision",
            Position = kara.ToLocal(gateWorld),
            RotationDegrees = new Vector3(0f, gateYaw, 0f),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        _karaSideTrackCollision.SetMeta("collisionOwner", "Act1ConnectedWorld/KaraOptionalDiscoveries");
        _karaSideTrackCollision.SetMeta("visualCounterpart", "Act1CoreWorldGreybox/KaraForestEdge/KaraForestryBranchGate/KaraForestryMovedBranch");
        _karaSideTrackCollision.SetMeta("clearanceMetres", 1.45f);
        _karaSideTrackCollision.SetMeta("interiorPolicy", "disabled while an interior zone is active");
        kara.AddChild(_karaSideTrackCollision);
        _karaSideTrackCollision.AddChild(new CollisionShape3D
        {
            Name = "FallenBranchCollider",
            Position = new Vector3(0f, .72f, 0f),
            RotationDegrees = new Vector3(0f, 0f, 5f),
            Shape = new BoxShape3D { Size = new Vector3(1.70f, .24f, .18f) }
        });
        _karaSideTrackCollision.AddChild(new CollisionShape3D
        {
            Name = "FallenBranchForkCollider",
            Position = new Vector3(-.24f, .87f, .06f),
            RotationDegrees = new Vector3(0f, -22f, -6f),
            Shape = new BoxShape3D { Size = new Vector3(.78f, .14f, .14f) }
        });

        _karaSideTrackBoundaryCollision = new StaticBody3D
        {
            Name = "KaraOldForestryBoundaryCollision",
            Position = kara.ToLocal(gateWorld),
            RotationDegrees = new Vector3(0f, gateYaw, 0f),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        _karaSideTrackBoundaryCollision.SetMeta("collisionOwner", "Act1ConnectedWorld/KaraOptionalDiscoveries");
        _karaSideTrackBoundaryCollision.SetMeta("visualCounterpart", "Act1CoreWorldGreybox/KaraForestEdge/KaraForestryBranchGate/GateGuideWest|GateGuideEast");
        _karaSideTrackBoundaryCollision.SetMeta("fixedBoundaryCounterpart", "AgentB_KaraEdgeKit/FallenLog_K0");
        _karaSideTrackBoundaryCollision.SetMeta("interiorPolicy", "disabled while an interior zone is active");
        kara.AddChild(_karaSideTrackBoundaryCollision);
        foreach (var (name, x) in new[] { ("West", .82f), ("East", -.82f) })
        {
            _karaSideTrackBoundaryCollision.AddChild(new CollisionShape3D
            {
                Name = $"GateGuide{name}Collider",
                Position = new Vector3(x, .41f, .58f),
                Shape = new BoxShape3D { Size = new Vector3(.16f, .82f, 1.20f) }
            });
            _karaSideTrackBoundaryCollision.AddChild(new CollisionShape3D
            {
                Name = $"GateGuide{name}PostCollider",
                Position = new Vector3(x, .54f, .04f),
                Shape = new BoxShape3D { Size = new Vector3(.19f, 1.08f, .19f) }
            });
        }
        SetKaraCollisionEnabled(_karaSideTrackBoundaryCollision, false);
        SetKaraCollisionEnabled(_karaSideTrackCollision, false);

        // The target sits on the road-facing side of the west-post latch, before
        // the branch collider. It is reachable without crossing the gate.
        var targetWorld = gate.ToGlobal(new Vector3(.64f, .86f, -.30f));
        var sideTarget = DiscoveryTarget(
            kara,
            KaraSideTrackSlug,
            new(.72f, .88f, .62f),
            kara.ToLocal(targetWorld),
            journal: false);
        sideTarget.SetMeta("activePropPath", "Act1CoreWorldGreybox/KaraForestEdge/KaraForestryBranchGate/KaraForestryMovedBranch");
        sideTarget.SetMeta("physicalAction", "move the fallen branch from the old track");
        sideTarget.SetMeta("targetRole", "west-post latch; accessible before crossing the gate");
        sideTarget.SetMeta("targetWorldPosition", targetWorld);
        sideTarget.SetMeta("routeReconnects", "KaraForestEdge road at both endpoints");
        sideTarget.WorldFoleySample = "door_creak";

        // A small real clearing and an ordinary distant facade make the warm
        // window a sightline from a place in the forest, rather than a journal
        // flag. The facade yaw follows the clearing-to-house vector; existing
        // trees and the new branch arc keep it out of the road view.
        var clearingWorld = KaraGround(4.45f, .45f);
        var clearing = new Node3D { Name = "KaraWarmWindowClearing" };
        clearing.SetMeta("presentationOnly", true);
        clearing.SetMeta("visualOnly", true);
        clearing.SetMeta("routeRole", "side platform with a village-facing reverse sightline");
        forest.AddChild(clearing);
        using (var clearingCurve = new Curve3D { BakeInterval = .2f })
        {
            foreach (var point in new[]
            {
                KaraPoint(3.45f, -.95f),
                KaraPoint(4.75f, -.55f),
                KaraPoint(5.35f, .75f),
                KaraPoint(4.55f, 1.65f)
            })
            {
                clearingCurve.AddPoint(clearing.ToLocal(point));
            }

            var clearingSurface = AddVisualLandformSurface(
                clearing,
                "KaraWarmWindowClearingSurface",
                2.35f,
                .028f,
                clearingCurve.GetBakedLength(),
                Vector3.Zero,
                "d7e0e3",
                "snow_trampled",
                0f,
                conformToTerrain: true,
                centerline: clearingCurve);
            clearingSurface.SetMeta("sightlineTarget", "KaraLastWarmWindowHouse/CoreFrontWindow");
            clearingSurface.SetMeta("centralRoutePolicy", "side position; not on the Kara road centreline");
        }
        var windowHouseWorld = KaraGround(-8.80f, 17.80f);
        var windowHouse = AddCoreBuilding(
            forest,
            "KaraLastWarmWindowHouse",
            windowHouseWorld,
            new Vector3(3.40f, 2.20f, 1.40f),
            2.20f,
            143f,
            "4c514a",
            "343c37",
            "8e795d",
            leanToRoof: false,
            porch: false);
        var warmWindow = windowHouse.GetNode<MeshInstance3D>("CoreFrontWindow");
        warmWindow.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml("e1a35e"),
            EmissionEnabled = true,
            Emission = Color.FromHtml("c2763d"),
            EmissionEnergyMultiplier = 1.25f,
            Roughness = .42f
        };
        warmWindow.SetMeta("lightingRole", "actual distant warm window seen from Kara clearing");
        AddVisualBox(windowHouse, "WarmWindowCrossVertical", new(.055f, .70f, .045f),
            new(warmWindow.Position.X, warmWindow.Position.Y, warmWindow.Position.Z + .035f), "b99b72", "wood");
        AddVisualBox(windowHouse, "WarmWindowCrossHorizontal", new(1.10f, .055f, .045f),
            new(warmWindow.Position.X, warmWindow.Position.Y, warmWindow.Position.Z + .040f), "b99b72", "wood");
        windowHouse.SetMeta("sightlineOrigin", "KaraWarmWindowClearing at world (4.45,-114.55)");
        windowHouse.SetMeta("sightlinePolicy", "front window faces the side clearing; main road view stays tree-occluded");

        var warmTarget = DiscoveryTarget(
            kara,
            KaraWarmWindowSlug,
            new(2.35f, 1.55f, 2.35f),
            kara.ToLocal(clearingWorld + Vector3.Up * .72f),
            journal: false);
        warmTarget.SetMeta("activePropPath", "Act1CoreWorldGreybox/KaraForestEdge/KaraWarmWindowClearing/KaraWarmWindowClearingSurface");
        warmTarget.SetMeta("sightlineTarget", "Act1CoreWorldGreybox/KaraForestEdge/KaraLastWarmWindowHouse/CoreFrontWindow");
        warmTarget.SetMeta("physicalAction", "find the last window between the spruces");

        // Two ordinary bent stems keep the profile ambiguous from the front:
        // stem A is the taller trunk with a short branch, while stem B grows
        // from its own ground point before bending into the retained shoulder
        // and long branch. No creature, actor or reveal is created here.
        var profileRoot = KaraGround(6.0f, -2.0f);
        var profileStemA = KaraGround(6.38f, -2.38f);
        var profileStemB = KaraGround(5.62f, -1.62f);

        AddCoreForestBranch(
            forest,
            "KaraBranchProfileTrunk",
            profileStemA + new Vector3(0f, .04f, 0f),
            profileStemA + new Vector3(-.05f, 1.25f, .03f),
            "756d60",
            .14f,
            .10f,
            radialSegments: 8);
        AddCoreForestBranch(
            forest,
            "KaraBranchProfileTrunkUpper",
            profileStemA + new Vector3(-.05f, 1.25f, .03f),
            profileStemA + new Vector3(.10f, 2.25f, .12f),
            "756d60",
            .10f,
            .065f,
            radialSegments: 8);
        AddCoreForestBranch(
            forest,
            "KaraBranchProfileTrunkTip",
            profileStemA + new Vector3(.10f, 2.25f, .12f),
            profileStemA + new Vector3(.08f, 3.10f, .16f),
            "756d60",
            .065f,
            .012f,
            radialSegments: 8);

        // The shoulder grows from a separate, grounded stem.
        AddCoreForestBranch(
            forest,
            "KaraBranchProfileSecondaryStemBase",
            profileStemB + new Vector3(0f, .04f, 0f),
            profileStemB + new Vector3(-.08f, .78f, .02f),
            "756d60",
            .13f,
            .11f,
            radialSegments: 8);
        AddCoreForestBranch(
            forest,
            "KaraBranchProfileSecondaryStemMid",
            profileStemB + new Vector3(-.08f, .78f, .02f),
            profileStemB + new Vector3(-.02f, 1.35f, .04f),
            "756d60",
            .11f,
            .095f,
            radialSegments: 8);
        AddCoreForestBranch(
            forest,
            "KaraBranchProfileSecondaryStemUpper",
            profileStemB + new Vector3(-.02f, 1.35f, .04f),
            profileStemB + new Vector3(-.01f, 1.86f, .06f),
            "756d60",
            .095f,
            .085f,
            radialSegments: 8);

        AddCoreForestBranch(
            forest,
            "KaraBranchProfileShoulder",
            profileStemB + new Vector3(-.01f, 1.86f, .06f),
            profileStemB + new Vector3(-.46f, 2.28f, .10f),
            "756d60",
            .085f,
            .060f,
            radialSegments: 8);
        AddCoreForestBranch(
            forest,
            "KaraBranchProfileShoulderTip",
            profileStemB + new Vector3(-.46f, 2.28f, .10f),
            profileStemB + new Vector3(-.94f, 2.62f, .12f),
            "756d60",
            .060f,
            .042f,
            radialSegments: 8);
        AddCoreForestBranch(
            forest,
            "KaraBranchProfileLongArm",
            profileStemB + new Vector3(-.94f, 2.62f, .12f),
            profileStemB + new Vector3(-1.32f, 2.72f, .16f),
            "756d60",
            .042f,
            .027f,
            radialSegments: 8);
        AddCoreForestBranch(
            forest,
            "KaraBranchProfileLongArmTip",
            profileStemB + new Vector3(-1.32f, 2.72f, .16f),
            profileStemB + new Vector3(-1.72f, 2.74f, .21f),
            "756d60",
            .027f,
            .008f,
            radialSegments: 8);

        AddCoreForestBranch(
            forest,
            "KaraBranchProfileSideTwig",
            profileStemA + new Vector3(.10f, 2.25f, .12f),
            profileStemA + new Vector3(.45f, 2.50f, .02f),
            "756d60",
            .060f,
            .028f,
            radialSegments: 8);
        AddCoreForestBranch(
            forest,
            "KaraBranchProfileSideTwigTip",
            profileStemA + new Vector3(.45f, 2.50f, .02f),
            profileStemA + new Vector3(.85f, 2.80f, -.02f),
            "756d60",
            .028f,
            .008f,
            radialSegments: 8);
        var profileStone = profileRoot + new Vector3(1.05f, 0f, .22f);
        AddVisualStoneCluster(forest, "KaraBranchProfileStone", profileStone, .62f, "62675f", organic: true);
        var profileTarget = DiscoveryTarget(
            kara,
            KaraBranchProfileSlug,
            new(2.10f, 2.25f, 1.65f),
            kara.ToLocal(profileRoot + Vector3.Up * 1.15f),
            journal: true);
        profileTarget.SetMeta("activePropPath", "Act1CoreWorldGreybox/KaraForestEdge/KaraBranchProfileTrunk");
        profileTarget.SetMeta("physicalAction", "check the silhouette from the side");
        profileTarget.SetMeta("opticalAmbiguityOnly", true);
        profileTarget.SetMeta("identityConfirmation", "none");

        // This ordinary E target is the existing KaraRoadAxis endpoint. Its
        // empty TargetZoneId keeps the player in the connected Kara world;
        // only RuntimeBridge advances from the approach scene to forest.
        var endpointWorld = KaraGround(.6f, -7.5f, .72f);
        var endpoint = kara.MakeInteractionBox(
            "KaraForestApproachEndpoint",
            new(1.80f, 1.50f, .55f),
            kara.ToLocal(endpointWorld),
            "39453b",
            "urman.chapter1:interaction/forest-approach-to-forest",
            "Идти дальше к кромке леса");
        endpoint.SetMeta("routeRole", "existing Kara road endpoint; scene transition without a zone teleport");
        endpoint.SetMeta("worldPosition", endpointWorld);
        endpoint.SetMeta("targetZonePolicy", "empty; remain in kara_urman_night");
    }

    private void UpdateAct1KaraOptionalDiscoveries()
    {
        if (_runtimeBridge?.ActiveSceneId is null)
        {
            return;
        }

        var knowledge = _runtimeBridge.SelectRuntimeState().GetProperty("knowledge");
        bool Found(string slug) => knowledge.TryGetProperty(DiscoveryPrefix + slug, out var entry)
            && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";
        var exteriorActive = ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";
        var sideTrackFound = Found(KaraSideTrackSlug);

        if (_karaSideTrackBranch is not null && _karaSideTrackFound != sideTrackFound)
        {
            _karaSideTrackMove?.Kill();
            var destination = sideTrackFound ? _karaSideTrackBranchCleared : _karaSideTrackBranchRest;
            var rotation = sideTrackFound ? _karaSideTrackBranchClearedRotation : Vector3.Zero;
            if (_karaSideTrackFound == false && sideTrackFound && exteriorActive)
            {
                _karaSideTrackMove = CreateTween();
                _karaSideTrackMove.TweenProperty(_karaSideTrackBranch, "position", destination, .55f);
                _karaSideTrackMove.Parallel().TweenProperty(_karaSideTrackBranch, "rotation", rotation, .55f);
            }
            else
            {
                _karaSideTrackBranch.Position = destination;
                _karaSideTrackBranch.Rotation = rotation;
            }
            _karaSideTrackFound = sideTrackFound;
        }

        // Fixed guide rails remain after opening; only the visible branch's
        // matching collider is removed. Both bodies are inert for interiors.
        SetKaraCollisionEnabled(_karaSideTrackBoundaryCollision, exteriorActive);
        SetKaraCollisionEnabled(_karaSideTrackCollision, exteriorActive && !sideTrackFound);
    }

    private static void SetKaraCollisionEnabled(StaticBody3D? body, bool enabled)
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
}
