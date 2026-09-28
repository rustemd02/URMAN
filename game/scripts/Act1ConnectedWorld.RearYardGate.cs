using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    /// <summary>
    /// The deliberate rear passage (R123/W5 composition fix, 2026-09-28):
    /// the authored Babai house volume and the west neighbour parcel sealed
    /// every old route to the rear field, orphaning the rear minaret-view
    /// discovery. The neighbour picket now stops short, and this gate dresses
    /// the opening on the yard's own visual language: two squared posts and
    /// a leaf fixed open toward the field, always passable like GateBabai.
    /// Presentation only; the passage's physical width comes from the
    /// shortened authored fence and its auto-retired trimesh collision.
    /// </summary>
    private void BuildRearYardGate(Node3D core)
    {
        RetireNeighborPicketRun(core);
        var village = _zoneInstances["village_day"] as StyleBenchmarkZone
            ?? throw new InvalidOperationException(
                "Connected Act I layout is missing the village_day interaction zone.");
        var presentation = new Node3D { Name = "RearYardGate" };
        presentation.SetMeta("presentationOnly", true);
        presentation.SetMeta("visualOnly", true);
        presentation.SetMeta("presentationOwner", nameof(Act1ConnectedWorld));
        presentation.SetMeta("visualZone", "BabaiEbiYard");
        presentation.SetMeta("interactionOwner", "none; always-open passage");
        presentation.SetMeta("runtimeStateOwner", "RuntimeBridge");
        presentation.SetMeta(
            "routePolicy",
            "always-open rear gate reconnects the yard to the rear field behind the house");
        core.AddChild(presentation);

        // The neighbour picket's authored replacement: one run west of the
        // gate, in the yard's own rail-and-picket language, ending on the
        // parcel's western edge where the dwelling takes over.
        PicketRun(presentation, "RearNeighborPicketWest", new(-37.9f, 0f, -3.2f), new(-36.65f, 0f, -3.2f));

        // Posts on the opening's edges. Grounded on the real heightfield.
        Post(presentation, "RearGatePostEast", new(-35.45f, 0f, -3.2f));
        Post(presentation, "RearGatePostWest", new(-36.65f, 0f, -3.2f));

        // The open leaf hangs on the east post and swings into the passage
        // toward the field, mirroring GateBabai's fixed-open state.
        var hinge = new Node3D { Name = "RearGateLeafHinge", Position = GroundedYardPoint(new(-35.45f, 0f, -3.2f)) };
        hinge.SetMeta("presentationOnly", true);
        presentation.AddChild(hinge);
        var leaf = new Node3D { Name = "RearGateLeaf", RotationDegrees = new(0, -78, 0) };
        hinge.AddChild(leaf);
        AddVisualBox(leaf, "RearGateLeafBoard", new(.09f, 1.02f, 1.06f), new(0f, .51f, .55f), "6f5a49", "wood");
        AddVisualBox(leaf, "RearGateLeafBrace", new(.06f, .07f, .92f), new(-.05f, .62f, .55f), "846c52", "wood");
        DiscoveryCylinder(leaf, "RearGateLeafHingePin", .028f, .028f, .96f, new(-.05f, .51f, .04f), "42352b");
    }

    // The authored picket component stays placed for the parcel's own book-
    // keeping, but its meshes and the RearBoundary_ trimesh collision built
    // from them are retired so the gate opening is truly walkable.
    private static void RetireNeighborPicketRun(Node3D core)
    {
        var fence = core.GetNodeOrNull<Node3D>(
            "Act1AuthoredExteriorKitPresentation/NeighborParcels/HouseExterior/WestSideParcel/HouseExteriorWestNeighborFence");
        if (fence is null)
        {
            throw new InvalidOperationException(
                "The rear neighbour picket fence is missing; the rear gate cannot replace its span.");
        }
        var retired = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mesh in FindDescendants<MeshInstance3D>(fence))
        {
            mesh.Visible = false;
            mesh.SetMeta("agentBPresentationSuppressed", true);
            mesh.SetMeta("suppressionReason", "RearYardGate replaces the picket run with an authored span plus the deliberate rear gate opening");
            retired.Add("RearBoundary_" + mesh.Name);
        }
        // The bypass collision body is parented to the connected world
        // itself, next to the zone roots - not under the greybox core.
        var bypass = core.GetParent()?.GetNodeOrNull<StaticBody3D>("village-main-road/Act1BypassCollision");
        var disabled = 0;
        if (bypass is { } bypassBody)
        {
            foreach (var shape in FindDescendants<CollisionShape3D>(bypassBody))
            {
                // Name match covers the west-neighbour run; a second picket
                // set comes from the approach dwelling boundary with the
                // same mesh names, so anything RearBoundary_* physically
                // standing inside the gate corridor goes too.
                var name = shape.Name.ToString();
                if (retired.Contains(name))
                {
                    // Removed outright: SetBypassCollisionEnabled re-enables
                    // every shape on zone updates, so a one-off Disabled flag
                    // would not survive the first logical-zone refresh.
                    shape.GetParent()?.RemoveChild(shape);
                    shape.QueueFree();
                    disabled++;
                }
            }
        }
        GD.Print($"act1-rear-gate: retired_meshes={retired.Count} disabled_shapes={disabled} bypass={(bypass is null ? "missing" : bypass.GetPath())}");
    }

    private static void PicketRun(Node3D parent, string name, Vector3 from, Vector3 to)
    {
        var run = new Node3D { Name = name };
        run.SetMeta("presentationOnly", true);
        parent.AddChild(run);
        var length = new Vector2(to.X - from.X, to.Z - from.Z).Length();
        var yaw = Mathf.RadToDeg(Mathf.Atan2(to.X - from.X, to.Z - from.Z));
        foreach (var (tag, height, colour) in new[] { ("RailHigh", .78f, "8a8d85"), ("RailLow", .34f, "7c7f78") })
            AddVisualBox(run, name + tag, new(.10f, .09f, length),
                GroundedYardPoint((from + to) * .5f) + Vector3.Up * height, colour, "wood_fence_rail", yawDegrees: yaw + 90);
        var pickets = Mathf.Max(1, Mathf.RoundToInt(length / .34f));
        for (var index = 0; index <= pickets; index++)
        {
            var t = index / (float)pickets;
            var at = GroundedYardPoint(from + (to - from) * t);
            AddVisualBox(run, $"{name}Picket{index}", new(.075f, .96f, .055f),
                at + Vector3.Up * .48f, "9a9184", "wood", yawDegrees: yaw + 90);
        }
    }

    private static void Post(Node3D parent, string name, Vector3 at)
    {
        var post = new Node3D { Name = name, Position = GroundedYardPoint(at) };
        post.SetMeta("presentationOnly", true);
        parent.AddChild(post);
        AddVisualBox(post, name + "Shaft", new(.11f, 1.18f, .11f), new(0, .59f, 0), "6d5c47", "wood");
        AddVisualBox(post, name + "Cap", new(.17f, .05f, .17f), new(0, 1.20f, 0), "7d6a52", "wood");
    }
}
