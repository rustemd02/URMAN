using Godot;
using Urman.Core.Narrative;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string ForestPhotoEntryInteractionId = "urman.fullgame:interaction/pw-enter-forest";
    private const string ForestPhotoEntryLandmarkScenePath = "res://assets/photoworlds/forest_entry/pforest-tableau-v1.glb";
    private const float ForestPhotoEntryLandmarkDistance = 5.45f;
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
        var forest = core.GetNode<Node3D>("KaraForestEdge");
        var landmark = ResourceLoader.Load<PackedScene>(ForestPhotoEntryLandmarkScenePath)
            ?.Instantiate<Node3D>()
            ?? throw new InvalidOperationException(
                $"Photo forest entry landmark is missing or could not be instantiated: {ForestPhotoEntryLandmarkScenePath}");
        landmark.Name = "ForestPhotoEntryLandmark";
        landmark.SetMeta("presentationOnly", true);
        landmark.SetMeta("visualOnly", true);
        landmark.SetMeta("collisionOwner", "ForestPhotoEntryContact");
        landmark.SetMeta("assetSource", ForestPhotoEntryLandmarkScenePath);
        forest.AddChild(landmark);

        // The imported tableau is authored along +Z from the photographed
        // camera. Aim that axis down the real road tangent, then filter the
        // same snowbanks and distant trunks that W01 omits in its own scene.
        var direction = PhotoForestApproachDirection;
        landmark.GlobalRotation = new Vector3(0f, Mathf.Atan2(direction.X, direction.Y), 0f);
        foreach (var mesh in FindDescendants<MeshInstance3D>(landmark))
        {
            var name = mesh.Name.ToString();
            if (name.Contains("Snowbank", StringComparison.Ordinal)
                || name.Contains("DistantTrunk", StringComparison.Ordinal))
            {
                mesh.Visible = false;
                continue;
            }

            if (name == "PForest_CentralTrunk_middle" && mesh.Mesh is not null)
                _forestPhotoEntryTrunk = mesh;
        }

        var trunk = _forestPhotoEntryTrunk
            ?? throw new InvalidOperationException(
                $"Photo forest entry landmark has no central witness trunk: {ForestPhotoEntryLandmarkScenePath}");
        var witnessLocalCenter = landmark.ToLocal(trunk.GlobalTransform * trunk.Mesh!.GetAabb().GetCenter());
        var witnessWorldCenter = ToGlobal(PhotoForestApproachEntryPosition)
            + new Vector3(direction.X, 0f, direction.Y) * ForestPhotoEntryLandmarkDistance;
        witnessWorldCenter.Y = (float)AgentBAct1HeightField.CollisionGround(witnessWorldCenter.X, witnessWorldCenter.Z)
            + witnessLocalCenter.Y;
        landmark.GlobalPosition = witnessWorldCenter - landmark.GlobalBasis * witnessLocalCenter;

        // Plant the photographic stems on the unchanged traversal triangles.
        // The mesh source stays intact; this only seats its vertical tree bases
        // on the terrain at their actual transformed XZ positions.
        foreach (var mesh in FindDescendants<MeshInstance3D>(landmark))
        {
            var name = mesh.Name.ToString();
            if (mesh.Mesh is null
                || !(name.StartsWith("PForest_CentralTrunk_", StringComparison.Ordinal)
                    || name.StartsWith("PForest_SplitBirch_", StringComparison.Ordinal))) continue;
            var bounds = mesh.Mesh.GetAabb();
            var basePoint = mesh.GlobalTransform * new Vector3(
                bounds.GetCenter().X, bounds.Position.Y, bounds.GetCenter().Z);
            var ground = AgentBAct1HeightField.CollisionGround(basePoint.X, basePoint.Z);
            mesh.GlobalPosition += Vector3.Up * (ground - basePoint.Y);
        }

        var landmarkContact = new StaticBody3D
        {
            Name = "ForestPhotoEntryContact",
            CollisionLayer = 1u,
            CollisionMask = 0u
        };
        landmarkContact.SetMeta("collisionOwner", "Act1ConnectedWorld/ForestPhotoEntryLandmark");
        landmarkContact.SetMeta("contactPolicy", "visible imported triangles; no canopy boxes");
        forest.AddChild(landmarkContact);
        foreach (var mesh in FindDescendants<MeshInstance3D>(landmark))
        {
            if (mesh.Visible && mesh.Mesh is not null)
                landmarkContact.AddChild(AuthoredSurfaceContact(landmarkContact, mesh));
        }

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
