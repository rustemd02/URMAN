using System;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string BabaiYardSideGateSlug = "babai-yard-loose-side-gate-board";
    private const string BabaiYardSideGateWoodPath = "res://assets/models/act1/babai-side-gate-wood-pbr-2k-v1.glb";

    // The lower side opening uses the existing posts at z=-1.51 and .12.
    // It clears the house interaction proxy; the original upper gate is the return exit.
    private static readonly Vector3[] BabaiYardSideGateLoopPoints =
    [
        new(-24.4f, 0f, 1.70f), new(-24.35f, 0f, -.70f),
        new(-26.05f, 0f, -.70f), new(-26.05f, 0f, .20f),
        new(-26.05f, 0f, 2.60f), new(-25.20f, 0f, 2.60f),
        new(-24.42f, 0f, 2.40f), new(-24.42f, 0f, 1.70f)
    ];

    private Node3D? _babaiYardSideGatePivot;
    private StaticBody3D? _babaiYardSideFenceCollision;
    private StaticBody3D? _babaiYardSideGateCollision;
    private bool? _babaiYardSideGateFound;
    private Tween? _babaiYardSideGateTween;
    // EX05.3: the drift over the yard's service gap and its physical barrier.
    private MeshInstance3D? _ex05PassageDrift;
    private StaticBody3D? _ex05PassageBarrier;

    private void BuildBabaiYardSideGateExploration()
    {
        var village = _zoneInstances["village_day"] as StyleBenchmarkZone
            ?? throw new InvalidOperationException(
                "Connected Act I layout is missing the village_day interaction zone.");
        var core = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox")
            ?? throw new InvalidOperationException(
                "Act I core world is missing before the Babai side gate is built.");
        var exteriorLayer = core.GetNodeOrNull<AgentBAct1ExteriorLayer>("AgentBExteriorWorld")
            ?? throw new InvalidOperationException(
                "Act I exterior layer is missing before the Babai side gate is built.");

        // Agent B owns the real architecture body. Retire only the continuous
        // W_S rails/brace and their tiny board accents, replacing its posts with matched panel posts;
        // the separate GateBabai opening stays untouched. The replacement lower span
        // below is both visible and collidable, so this opening has one owner.
        RetireAgentBBabaiSideFenceSpan(exteriorLayer);

        var presentation = new Node3D { Name = "BabaiYardSideGateExploration" };
        presentation.SetMeta("presentationOnly", true);
        presentation.SetMeta("visualOnly", true);
        presentation.SetMeta("presentationOwner", nameof(Act1ConnectedWorld));
        presentation.SetMeta("visualZone", "BabaiEbiYard");
        presentation.SetMeta("interactionOwner", "RuntimeBridge / existing InteractionTarget");
        presentation.SetMeta("runtimeStateOwner", "RuntimeBridge");
        presentation.SetMeta(
            "routePolicy",
            "side opening reconnects to the existing HouseExteriorApproach path; GateBabai remains the always-open alternative");
        presentation.SetMeta(
            "loopPolyline",
            string.Join('|', BabaiYardSideGateLoopPoints.Select(
                point => $"{point.X:0.00},{point.Z:0.00}")));
        core.AddChild(presentation);

        var lowerFenceStart = new Vector3(-25.20f, 0f, -6.40f);
        var lowerFenceEnd = new Vector3(-25.20f, 0f, -1.51f);
        // EX05.3: the lower run carries one more opening - a service gap into
        // the lower street, banked over with a drift. Nothing on the authored
        // route uses it, so clearing it is a genuine convenience and never a
        // chore on the critical path, while the snowed-up look still reads as
        // "the yard's own way through is closed for the winter".
        var gapNorth = new Vector3(-25.20f, 0f, -2.30f);
        var gapSouth = new Vector3(-25.20f, 0f, -3.40f);
        AddYardSideFenceVisualSpan(presentation, "LowerFence", lowerFenceStart, gapSouth);
        AddYardSideFenceVisualSpan(presentation, "LowerFenceGapNorth", gapNorth, lowerFenceEnd);
        AddYardSideFenceVisualSpan(presentation, "UpperFence", new(-25.2f, 0, .12f), new(-25.2f, 0, 1.75f));

        var gate = new Node3D
        {
            Name = "BabaiYardLooseSideGate",
            Position = presentation.ToLocal(GroundedYardPoint(new(-25.20f, 0f, -1.47f)))
        };
        gate.SetMeta("presentationOnly", true);
        gate.SetMeta("visualOnly", true);
        gate.SetMeta("physicalAction", "отвести расшатанный щит");
        presentation.AddChild(gate);

        var leafPivot = new Node3D { Name = "LooseBoardHinge" };
        leafPivot.SetMeta("presentationOnly", true);
        gate.AddChild(leafPivot);
        _babaiYardSideGatePivot = leafPivot;
        // The donor retains these exact eight wood members and their local
        // transforms. Its imported PBR replaces only their old box presentation.
        var packedWood = ResourceLoader.Load<PackedScene>(BabaiYardSideGateWoodPath)
            ?? throw new InvalidOperationException($"The Babai side-gate wood is missing: {BabaiYardSideGateWoodPath}.");
        var wood = packedWood.Instantiate<Node3D>()
            ?? throw new InvalidOperationException("The Babai side-gate wood has no 3D root.");
        try
        {
            var nodes = new Node[] { wood }.Concat(FindDescendants<Node>(wood)).ToArray();
            var meshes = nodes.OfType<MeshInstance3D>().ToArray();
            string[] expectedNames = ["LooseBoardPlank0", "LooseBoardPlank1", "LooseBoardPlank2",
                "LooseBoardPlank3", "LooseBoardPlank4", "LooseBoardBattenLow", "LooseBoardBattenHigh", "LooseBoardBrace"];
            if (!IsIdentityTransform(wood.Transform)
                || nodes.Any(node => node is CollisionObject3D or CollisionShape3D or CollisionPolygon3D)
                || meshes.Length != expectedNames.Length || meshes.Any(mesh => mesh.Mesh is null)
                || !meshes.Select(mesh => mesh.Name.ToString()).ToHashSet(StringComparer.Ordinal).SetEquals(expectedNames))
                throw new InvalidOperationException("The Babai side-gate wood must retain exactly its eight named visual members, identity root and no physics.");
            wood.Name = "BabaiSideGateWoodPbr";
            wood.SetMeta("presentationOnly", true);
            wood.SetMeta("assetSource", BabaiYardSideGateWoodPath);
            leafPivot.AddChild(wood);
            var bounds = meshes.Select(mesh => PublicBuildingShell.Bounds(leafPivot, mesh)).Aggregate((a, b) => a.Merge(b));
            if (!bounds.Position.IsFinite() || !bounds.Size.IsFinite()
                || bounds.Position.DistanceTo(new Vector3(-.0495f, .04f, .01f)) > .0002f
                || bounds.End.DistanceTo(new Vector3(.015f, 1.09f, 1.3702f)) > .0002f)
                throw new InvalidOperationException("The Babai side-gate wood differs from its measured donor bounds.");
        }
        catch
        {
            wood.Free();
            throw;
        }
        DiscoveryCylinder(
            gate,
            "LooseBoardHingePin",
            .034f,
            .034f,
            1.00f,
            new(-.07f, .53f, .02f),
            "49382d");
        AddVisualBox(
            leafPivot,
            "LooseBoardLatch",
            new(.16f, .12f, .08f),
            new(-.08f, .83f, 1.24f),
            "49382d",
            "metal");

        // The interaction volume is on the village/east side of the latch. It
        // remains reachable while the board is closed, and the ordinary
        // GateBabai opening remains available as the no-softlock route.
        var target = DiscoveryTarget(
            village,
            BabaiYardSideGateSlug,
            new(.60f, .92f, .62f),
            new(-24.72f, 1.00f, -.47f),
            journal: false);
        target.SetMeta("targetRole", "yard-side gate latch, approached from the village-side yard");
        target.SetMeta("physicalAction", "отвести расшатанный щит");
        target.WorldFoleySample = "door_creak";
        target.SetMeta("targetWorldPosition", target.GlobalPosition);
        target.SetMeta("routeReconnect", "HouseExteriorApproach/BabaiEbiHouseEntryPath");

        // The new body is under the existing logical zone, like the FAP
        // exploration body. Its shapes are inert in both interiors and are
        // restored on every RuntimeStateChanged/load/new-session projection.
        _babaiYardSideFenceCollision = CreateYardCollisionBody(
            village,
            "BabaiYardSideFenceCollision");
        AddYardCollisionBox(
            _babaiYardSideFenceCollision,
            "LowerFenceRailHigh",
            lowerFenceStart,
            gapSouth,
            .18f,
            1.10f,
            .55f);
        AddYardCollisionBox(
            _babaiYardSideFenceCollision,
            "LowerFenceRailLow",
            lowerFenceStart,
            gapSouth,
            .12f,
            .10f,
            .34f);
        AddYardCollisionBox(
            _babaiYardSideFenceCollision,
            "LowerFenceGapNorthRailHigh",
            gapNorth,
            lowerFenceEnd,
            .18f,
            1.10f,
            .55f);
        AddYardCollisionBox(
            _babaiYardSideFenceCollision,
            "LowerFenceGapNorthRailLow",
            gapNorth,
            lowerFenceEnd,
            .12f,
            .10f,
            .34f);

        // EX05.3/EX05.4: the drift banked over the service gap. It is the only
        // EX05 barrier with real physics, so clearing it changes a passage; the
        // visible pile is the barrier - same volume, grounded the same way - and
        // not a decoration beside an invisible wall.
        _ex05PassageDrift = AddVisualLandformSurface(
            presentation,
            "ToolSnowPileServiceGap",
            .72f, .46f, 1.38f,
            new(-25.20f, 0f, -2.85f),
            "eef2f6",
            "snow_ground", 0,
            conformToTerrain: true);
        _ex05PassageDrift.SetMeta("visualOnly", false);
        _ex05PassageDrift.SetMeta("terrainRole", "authored service-gap drift; visible snow surface owns its matching barrier");
        _ex05PassageBarrier = CreateYardCollisionBody(presentation, "Ex05PassageDriftBarrier");
        _ex05PassageBarrier.SetMeta("footstepSurface", "snow");
        var driftContact = new CollisionShape3D
        {
            Name = "ServiceGapDriftBlock",
            Shape = _ex05PassageDrift.Mesh.CreateTrimeshShape(),
            Transform = _ex05PassageBarrier.GlobalTransform.AffineInverse() * _ex05PassageDrift.GlobalTransform
        };
        driftContact.SetMeta("authoredSourceMesh", _ex05PassageDrift.GetPath().ToString());
        _ex05PassageBarrier.AddChild(driftContact);
        SetYardCollisionEnabled(_ex05PassageBarrier, true);

        AddYardCollisionBox(_babaiYardSideFenceCollision, "UpperFence", new(-25.2f, 0, .12f),
            new(-25.2f, 0, 1.75f), .18f, 1.10f, .55f);
        _babaiYardSideGateCollision = CreateYardCollisionBody(
            village,
            "BabaiYardSideGateCollision");
        AddYardCollisionBox(
            _babaiYardSideGateCollision,
            "LooseBoardCollision",
            new(-25.20f, 0f, -1.47f),
            new(-25.20f, 0f, -.09f),
            .14f,
            1.06f,
            .53f);
        SetYardCollisionEnabled(_babaiYardSideFenceCollision, false);
        SetYardCollisionEnabled(_babaiYardSideGateCollision, false);
    }

    private void UpdateBabaiYardSideGateExploration()
    {
        if (_runtimeBridge?.ActiveSceneId is null)
        {
            return;
        }

        var knowledge = _runtimeBridge.SelectRuntimeState().GetProperty("knowledge");
        var found = knowledge.TryGetProperty(
                DiscoveryPrefix + BabaiYardSideGateSlug,
                out var entry)
            && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";
        var exteriorActive = ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";

        if (_babaiYardSideGatePivot is not null && _babaiYardSideGateFound != found)
        {
            _babaiYardSideGateTween?.Kill();
            var openRotation = found ? Mathf.Pi * .5f : 0f;
            if (_babaiYardSideGateFound == false && found && exteriorActive)
            {
                _babaiYardSideGateTween = CreateTween();
                _babaiYardSideGateTween.TweenProperty(
                    _babaiYardSideGatePivot,
                    "rotation:y",
                    openRotation,
                    .45f);
            }
            else
            {
                _babaiYardSideGatePivot.Rotation = new Vector3(0f, openRotation, 0f);
            }

            _babaiYardSideGateFound = found;
        }

        SetYardCollisionEnabled(_babaiYardSideFenceCollision, exteriorActive);
        SetYardCollisionEnabled(
            _babaiYardSideGateCollision,
            exteriorActive && !found);
    }

    private static void RetireAgentBBabaiSideFenceSpan(AgentBAct1ExteriorLayer layer)
    {
        var kit = layer.GetNodeOrNull<Node3D>("AgentB_VillageBuildingsKit")
            ?? throw new InvalidOperationException(
                "Agent B village kit is missing before the Babai side fence opening.");
        var sourceMeshes = FindDescendants<MeshInstance3D>(kit)
            .Where(mesh =>
            {
                var name = mesh.Name.ToString();
                return name is "FenceBabaiW_S_RailHigh0"
                    or "FenceBabaiW_S_RailLow0"
                    or "FenceBabaiW_S_Brace0"
                    || name.StartsWith("FenceBabaiW_S_Board0_", StringComparison.Ordinal);
            })
            .ToArray();
        if (sourceMeshes.Length != 6)
        {
            throw new InvalidOperationException(
                "Agent B Babai side fence source contract changed; expected 2 rails, 1 brace and 3 board accents.");
        }

        var oldPosts = FindDescendants<MeshInstance3D>(kit).Where(mesh =>
            mesh.Name.ToString().StartsWith("FenceBabaiW_S_Post", StringComparison.Ordinal)).ToArray();
        var allSourceMeshes = sourceMeshes.Concat(oldPosts).ToArray();
        foreach (var mesh in allSourceMeshes)
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta(
                "suppressionReason",
                "Act1ConnectedWorld replaces only the lower FenceBabaiW_S rail span with a persistent side-gate opening");
        }

        var architecture = layer.GetNodeOrNull<StaticBody3D>("AgentB_ArchitectureCollision")
            ?? throw new InvalidOperationException(
                "Agent B architecture collision is missing before the Babai side fence opening.");
        var sourceNames = allSourceMeshes
            .Select(mesh => "Col_" + mesh.Name.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var sourceShapes = FindDescendants<CollisionShape3D>(architecture)
            .Where(shape => sourceNames.Contains(shape.Name.ToString()))
            .ToArray();
        if (sourceMeshes.Any(mesh => !sourceShapes.Any(shape => shape.Name == "Col_" + mesh.Name.ToString())))
        {
            throw new InvalidOperationException(
                "Agent B Babai side fence collision does not mirror its visible source span.");
        }

        foreach (var shape in sourceShapes)
        {
            shape.Disabled = true;
            shape.SetMeta(
                "suppressionReason",
                "Act1ConnectedWorld replaces only the lower FenceBabaiW_S rail span with a persistent side-gate opening");
        }
    }

    private static void AddYardSideFenceVisualSpan(
        Node3D parent, string name, Vector3 start, Vector3 end)
    {
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length < .116f) return;
        var center = GroundedYardPoint((start + end) * .5f);
        var yaw = Mathf.Atan2(direction.X, direction.Z);
        var panel = new Node3D { Name = name + "BoardedPanel", Position = center, Rotation = new(0,yaw,0) };
        parent.AddChild(panel);
        // The shown front fence was only two grey rails, interrupted by a much
        // taller loose gate. A straight boarded panel now follows that gate's
        // 1.06 m height; its endpoints leave both existing openings unchanged.
        foreach (var (suffix, height) in new[] { ("RailHigh", .82f), ("RailLow", .30f) })
            AddVisualBox(panel,name+suffix,new(.10f,.10f,length),new(-.015f,height,0),"806e59","wood_fence_rail");
        var count = Math.Max(1,Mathf.FloorToInt((length-.10f)/.16f));
        var pitch = (length-.10f)/count;
        for(var board=0;board<count;board++)
            AddVisualBox(panel,name+"Board"+board,new(.055f,1.04f,Mathf.Min(.13f,pitch-.016f)),
                new(.06f,.55f,-length*.5f+.05f+(board+.5f)*pitch),"8b7966","wood_fence_rail");
        foreach(var sign in new[]{-1f,1f})
        {
            AddVisualBox(panel,name+"Post"+sign,new(.13f,1.18f,.13f),new(0,.59f,sign*(length*.5f-.075f)),"74604b","wood_fence_rail");
            AddVisualBox(panel,name+"Cap"+sign,new(.16f,.05f,.16f),new(0,1.205f,sign*(length*.5f-.08f)),"8b7966","wood_fence_rail");
        }
        panel.SetMeta("routeRole","straight boarded replacement; original side gate and service-gap endpoints retained");
    }

    private StaticBody3D CreateYardCollisionBody(
        Node3D parent,
        string name)
    {
        var body = new StaticBody3D { Name = name };
        body.SetMeta("collisionOwner", nameof(Act1ConnectedWorld));
        body.SetMeta("runtimeStateOwner", "RuntimeBridge");
        body.CollisionLayer = 1u;
        body.CollisionMask = 1u;
        parent.AddChild(body, forceReadableName: true);
        return body;
    }

    private static void AddYardCollisionBox(
        StaticBody3D body,
        string name,
        Vector3 start,
        Vector3 end,
        float thickness,
        float height,
        float centreHeight)
    {
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length < .05f)
        {
            return;
        }

        var center = GroundedYardPoint((start + end) * .5f);
        center.Y += centreHeight;
        var yaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
        body.AddChild(
            new CollisionShape3D
            {
                Name = name,
                Position = body.ToLocal(center),
                RotationDegrees = new Vector3(0f, yaw, 0f),
                Shape = new BoxShape3D
                {
                    Size = new Vector3(thickness, height, length)
                }
            },
            forceReadableName: true);
        if (name.EndsWith("RailHigh", StringComparison.Ordinal) || name == "UpperFence")
        {
            var along = direction.Normalized();
            foreach (var sign in new[] { -1f, 1f })
                body.AddChild(new CollisionShape3D { Name = name + "Post" + sign,
                    Position = body.ToLocal(center + along * (sign * (length * .5f - .075f)) + Vector3.Up * (.615f - centreHeight)),
                    RotationDegrees = new(0, yaw, 0), Shape = new BoxShape3D { Size = new(.16f, 1.23f, .16f) } });
        }
    }

    private static void SetYardCollisionEnabled(StaticBody3D? body, bool enabled)
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

    private static Vector3 GroundedYardPoint(Vector3 point) =>
        new(point.X, AgentBAct1HeightField.CollisionGround(point.X, point.Z), point.Z);
}
