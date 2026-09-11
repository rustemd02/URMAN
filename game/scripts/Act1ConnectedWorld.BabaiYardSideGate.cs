using System;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string BabaiYardSideGateSlug = "babai-yard-loose-side-gate-board";

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
        // W_S rails/brace and their tiny board accents, leaving its posts and
        // the separate GateBabai opening untouched. The replacement lower span
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
        AddYardSideFenceVisualSpan(presentation, "LowerFence", lowerFenceStart, lowerFenceEnd);
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
        AddVisualBox(
            leafPivot,
            "LooseBoard",
            new(.12f, 1.06f, 1.38f),
            new(0f, .53f, .69f),
            "70543e",
            "wood");
        AddVisualBox(
            leafPivot,
            "LooseBoardBrace",
            new(.08f, .08f, 1.14f),
            new(-.07f, .64f, .69f),
            "8a6b50",
            "wood");
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
            journal: true);
        target.SetMeta("targetRole", "yard-side gate latch, approached from the village-side yard");
        target.SetMeta("physicalAction", "отвести расшатанный щит");
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
            lowerFenceEnd,
            .12f,
            .90f,
            .76f);
        AddYardCollisionBox(
            _babaiYardSideFenceCollision,
            "LowerFenceRailLow",
            lowerFenceStart,
            lowerFenceEnd,
            .12f,
            .10f,
            .34f);

        AddYardCollisionBox(_babaiYardSideFenceCollision, "UpperFence", new(-25.2f, 0, .12f),
            new(-25.2f, 0, 1.75f), .12f, .9f, .76f);
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

        foreach (var mesh in sourceMeshes)
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
        var sourceNames = sourceMeshes
            .Select(mesh => "Col_" + mesh.Name.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var sourceShapes = FindDescendants<CollisionShape3D>(architecture)
            .Where(shape => sourceNames.Contains(shape.Name.ToString()))
            .ToArray();
        if (sourceShapes.Length != sourceMeshes.Length)
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
        Node3D parent,
        string name,
        Vector3 start,
        Vector3 end)
    {
        var direction = end - start;
        var length = new Vector2(direction.X, direction.Z).Length();
        if (length < .05f)
        {
            return;
        }

        var center = GroundedYardPoint((start + end) * .5f);
        var yaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
        foreach (var (suffix, height, color) in new[]
                 { ("RailHigh", .76f, "594a39"), ("RailLow", .34f, "514737") })
        {
            var rail = AddVisualBox(
                parent,
                name + suffix,
                new(.12f, .10f, length),
                center + Vector3.Up * height,
                color,
                "wood_fence",
                yawDegrees: yaw);
            rail.SetMeta("routeRole", "visible replacement for FenceBabaiW_S lower rail");
        }
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
