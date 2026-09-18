using System;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string PorchPaintTinSlug = "babai-yard-porch-paint-tin";
    private const string EntryWallJointSlug = "babai-yard-entry-wall-joint";
    private const string NeedsLightTextId = "urman.chapter1:text/needs-light";

    // EX06.1/EX06.2/EX06.4: the two authored places the portable lantern
    // actually changes. Both details exist in the geometry and stay visible as
    // dark marks in daylight; the ordinary interaction only offers itself when
    // a lit lantern - carried or left standing on a support - reaches the
    // place. Reading them grants the existing knowledge/journal entry, so the
    // lamp stays a light rather than a scanner, and no second persistence owner
    // appears: the found state is the authored knowledge status.
    private void BuildAct1LanternDetails()
    {
        var village = _zoneInstances["village_day"] as StyleBenchmarkZone
            ?? throw new InvalidOperationException(
                "Connected Act I layout is missing the village_day interaction zone.");
        var core = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox")
            ?? throw new InvalidOperationException(
                "Act I core world is missing its presentation root.");
        var facade = core.GetNodeOrNull<Node3D>(
                "Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade")
            ?? throw new InvalidOperationException(
                "Act I authored dwelling facade is missing for the lantern details.");
        // The authored hero dwelling deliberately has no porch - the kit rejects
        // one - so the two places are the entrance step's dark foot and the
        // street wall beside the door. Both are matched by kit mesh suffix so a
        // prefixed hero or parcel family keeps working.
        var step = WidestNamed(facade, "StreetStep");
        var streetWall = WidestNamed(facade, "Street_Wall");
        if (step is null || streetWall is null)
        {
            throw new InvalidOperationException(
                "Act I authored dwelling is missing its entrance step or street wall.");
        }
        var stepBox = LocalBounds(facade, step, step.Mesh!.GetAabb());
        var wallBox = LocalBounds(facade, streetWall, streetWall.Mesh!.GetAabb());

        var details = new Node3D { Name = "Ex06LanternDetails" };
        details.SetMeta("presentationOnly", true);
        details.SetMeta("visualOnly", true);
        details.SetMeta("collisionOwner", "none");
        details.SetMeta("evidenceRole", "authored light details; readable in the portable lantern's pool");
        details.SetMeta("runtimeStateOwner", "RuntimeBridge knowledge/discovery statuses");
        core.AddChild(details);

        var stepMidX = (stepBox.Position.X + stepBox.End.X) * .5f;
        var stepFrontZ = stepBox.End.Z;

        // A: the dark foot of the entrance step. The tin rests on the real
        // ground just beside the step - outside its footprint, so the stone
        // itself never hides it - where the household left it, and only the
        // lamp shows what it is.
        var tinX = stepBox.End.X + .45f;
        var tinZ = stepFrontZ - .14f;
        var tinPlan = facade.ToGlobal(new Vector3(tinX, 0f, tinZ));
        var tinAt = new Vector3(tinX,
            AgentBAct1HeightField.CollisionGround(tinPlan.X, tinPlan.Z) + .016f, tinZ);
        AddVisualBox(details, "PorchPaintTin", new(.17f, .03f, .16f),
            details.ToLocal(facade.ToGlobal(tinAt)), "6f6c64", "metal", yawDegrees: 14f);
        AddVisualBox(details, "PorchPaintDrip", new(.10f, .012f, .09f),
            details.ToLocal(facade.ToGlobal(tinAt + new Vector3(-.01f, .022f, .015f))),
            "cfd2cb", "painted", yawDegrees: 14f);
        AddVisualBox(details, "PorchTrimScrap", new(.15f, .026f, .05f),
            details.ToLocal(facade.ToGlobal(tinAt + new Vector3(-.20f, .012f, .07f))),
            "d5cfc0", "painted", yawDegrees: -22f);
        var tinTarget = DiscoveryTarget(village, PorchPaintTinSlug,
            new(.58f, .46f, .54f),
            village.ToLocal(facade.ToGlobal(tinAt + new Vector3(0f, .15f, 0f))), journal: false);
        GateOnLamp(tinTarget, "paint tin at the entrance step");

        // B: a surface that only reads in raking light - the street wall beside
        // the door carries a long scratch and two filled holes from a lower rail.
        var jointX = (wallBox.Position.X + wallBox.End.X) * .5f - .95f;
        var wallFrontZ = wallBox.End.Z;
        var scratchY = wallBox.Position.Y + wallBox.Size.Y * .38f;
        AddVisualBox(details, "StreetWallScratch", new(.016f, .31f, .007f),
            details.ToLocal(facade.ToGlobal(new Vector3(jointX, scratchY, wallFrontZ + .006f))),
            "453b32", "wood", rollDegrees: 7f);
        AddVisualBox(details, "StreetWallScratchFoot", new(.10f, .012f, .007f),
            details.ToLocal(facade.ToGlobal(new Vector3(jointX + .03f, scratchY - .10f, wallFrontZ + .006f))),
            "453b32", "wood", rollDegrees: 12f);
        foreach (var (offset, suffix) in new[] { (-.17f, "Low"), (-.12f, "High") })
        {
            AddVisualBox(details, $"StreetWallOldHole{suffix}", new(.028f, .028f, .008f),
                details.ToLocal(facade.ToGlobal(new Vector3(jointX + offset, scratchY - .16f, wallFrontZ + .006f))),
                "3a322b", "wood");
        }
        var jointTarget = DiscoveryTarget(village, EntryWallJointSlug,
            new(.36f, .62f, .40f),
            village.ToLocal(facade.ToGlobal(new Vector3(jointX, scratchY - .02f, wallFrontZ + .16f))),
            journal: false);
        GateOnLamp(jointTarget, "street wall scratch joint");
    }

    private void GateOnLamp(InteractionTarget target, string role)
    {
        target.SetMeta("lightGateRole", role);
        target.SetMeta("lightGateTextId", NeedsLightTextId);
        target.PresentationGate = () => PortableLight.IsLit(this, target.GlobalPosition);
    }

    private static MeshInstance3D? WidestNamed(Node3D root, string suffix) =>
        FindDescendants<MeshInstance3D>(root)
            .Where(mesh => mesh.Mesh is not null
                && mesh.Name.ToString().Contains(suffix, StringComparison.Ordinal))
            .OrderByDescending(mesh => mesh.Mesh!.GetAabb().Size.X * mesh.Mesh!.GetAabb().Size.Z)
            .FirstOrDefault();

    /// <summary>A mesh AABB expressed in another node's local space.</summary>
    private static Aabb LocalBounds(Node3D space, Node3D mesh, Aabb bounds)
    {
        var minimum = Vector3.One * float.MaxValue;
        var maximum = Vector3.One * float.MinValue;
        for (var corner = 0; corner < 8; corner++)
        {
            var point = bounds.Position + new Vector3(
                (corner & 1) == 0 ? 0f : bounds.Size.X,
                (corner & 2) == 0 ? 0f : bounds.Size.Y,
                (corner & 4) == 0 ? 0f : bounds.Size.Z);
            var local = space.ToLocal(mesh.GlobalTransform * point);
            minimum = minimum.Min(local);
            maximum = maximum.Max(local);
        }
        return new Aabb(minimum, maximum - minimum);
    }
}
