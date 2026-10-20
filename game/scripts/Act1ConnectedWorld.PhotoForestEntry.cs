using Godot;
using Urman.Core.Narrative;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string ForestPhotoEntryInteractionId = "urman.fullgame:interaction/pw-enter-forest";
    private Marker3D? _forestPhotoEntryAnchor;
    private InteractionTarget? _forestPhotoEntryTarget;
    private MeshInstance3D? _forestPhotoEntryTrunk;

    // The legacy endpoint and the photo stance share the authored road rather
    // than independently spelling its terminal coordinates in different zones.
    internal static Vector3 KaraForestApproachEndpointPosition
    {
        get
        {
            var end = AgentBAct1Layout.KaraRoadAxis[^1];
            return new Vector3(end.X, AgentBAct1HeightField.CollisionGround(end.X, end.Y) + 1.25f, end.Y);
        }
    }

    private static Vector2 PhotoForestApproachDirection =>
        (AgentBAct1Layout.KaraRoadAxis[^1] - AgentBAct1Layout.KaraRoadAxis[^2]).Normalized();

    internal static Vector3 PhotoForestApproachEntryPosition
    {
        get
        {
            var foot = AgentBAct1Layout.KaraRoadAxis[^1] - PhotoForestApproachDirection * 2.4f;
            return new Vector3(foot.X, AgentBAct1HeightField.CollisionGround(foot.X, foot.Y) + .05f, foot.Y);
        }
    }

    internal static float PhotoForestApproachEntryYawDegrees =>
        Mathf.RadToDeg(Mathf.Atan2(-PhotoForestApproachDirection.X, -PhotoForestApproachDirection.Y));

    private void BuildForestPhotoEntry()
    {
        if (_forestPhotoEntryAnchor is not null
            || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge { IsPhotoWorldsCampaign: true }) return;
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        _forestPhotoEntryTrunk = core.GetNode<MeshInstance3D>("KaraForestEdge/KaraBranchProfileTrunk");
        var entry = new Node3D { Name = "PhotoForestEntry" };
        core.AddChild(entry);
        _forestPhotoEntryAnchor = new Marker3D { Name = "ForestPhotoEntryAnchor" };
        entry.AddChild(_forestPhotoEntryAnchor);
        _forestPhotoEntryTarget = new InteractionTarget
        {
            Name = "ForestPhotoEntryTarget",
            InteractionId = ForestPhotoEntryInteractionId,
            CollisionLayer = 4u,
            CollisionMask = 0u,
            PresentationGate = ForestPhotoEntryInteractionWindowOpen
        };
        _forestPhotoEntryTarget.ConfigureRayOnly();
        _forestPhotoEntryTarget.AddChild(new CollisionShape3D
        {
            Name = "ForestPhotoEntryRay",
            Shape = new SphereShape3D { Radius = .58f }
        });
        _forestPhotoEntryAnchor.AddChild(_forestPhotoEntryTarget);
        _forestPhotoEntryTarget.SetPresentationEnabled(false);
        TryGetForestPhotoEntryAnchor(out _);
    }

    internal bool TryGetForestPhotoEntryAnchor(out Node3D anchor)
    {
        anchor = null!;
        if (!IsInsideTree() || IsQueuedForDeletion()
            || _forestPhotoEntryAnchor is not { } marker || !GodotObject.IsInstanceValid(marker)
            || !marker.IsInsideTree() || marker.IsQueuedForDeletion()
            || _forestPhotoEntryTrunk is not { } trunk || !GodotObject.IsInstanceValid(trunk)
            || !trunk.IsInsideTree() || trunk.IsQueuedForDeletion()
            || trunk.Mesh is not { } mesh
            || _forestPhotoEntryTarget is not { } target || !GodotObject.IsInstanceValid(target)
            || !target.IsInsideTree() || target.IsQueuedForDeletion()) return false;
        // The supported book stance is on the road. The ray/facing witness is
        // the actual visible stem, not an invisible point at the player's feet.
        var center = trunk.GlobalTransform * mesh.GetAabb().GetCenter();
        marker.GlobalPosition = ToGlobal(PhotoForestApproachEntryPosition);
        var towardTrunk = center - marker.GlobalPosition;
        towardTrunk.Y = 0f;
        if (towardTrunk.LengthSquared() < .001f) return false;
        marker.GlobalRotation = new Vector3(0f, Mathf.Atan2(-towardTrunk.X, -towardTrunk.Z), 0f);
        target.GlobalPosition = center;
        anchor = marker;
        return true;
    }

    private bool ForestPhotoEntryProjectionReady() =>
        !IsQueuedForDeletion() && ActiveZoneId == "village_day"
        && _runtimeBridge is { IsPhotoWorldsCampaign: true } bridge && GodotObject.IsInstanceValid(bridge)
        && bridge.CurrentZoneId == "village_day" && bridge.SessionIdentity is { } session
        && bridge.CurrentFullGamePhotoWorldRouteProjection() is { ActiveSceneId: "urman.fullgame:scene/pw-home" } route
        && ReferenceEquals(route.SessionIdentity, session)
        && !PhotoWorldState.IsWorldCompleted(session.SelectState(), "W01")
        && bridge.IsInteractionAvailable(ForestPhotoEntryInteractionId);

    private bool ForestPhotoEntryInteractionWindowOpen() =>
        ForestPhotoEntryProjectionReady()
        && !MainMenuUi.AnyUndismissed(GetTree())
        && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController
            { ModalOpen: false, VehicleControlled: false, IsClimbingLadder: false, SessionTransitionActive: false };

    private void UpdateForestPhotoEntryPresentation()
    {
        if (_forestPhotoEntryTarget is not { } target || !GodotObject.IsInstanceValid(target)) return;
        var ready = TryGetForestPhotoEntryAnchor(out _) && ForestPhotoEntryProjectionReady();
        if (_runtimeBridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            target.Prompt = bridge.ResolveText("urman.fullgame:text/pw-label-enter-forest");
        target.SetPresentationEnabled(ready);
    }
}
