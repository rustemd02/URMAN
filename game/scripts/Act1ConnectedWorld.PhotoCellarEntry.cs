using System;
using System.Linq;
using Godot;
using Urman.Core.Narrative;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string CellarPhotoEntryInteractionId = "urman.fullgame:interaction/pw-enter-cellar";
    private const string CellarPhotoFacadePath =
        "Act1CoreWorldGreybox/Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade";
    private Node3D? _cellarPhotoEntryFacade;
    private MeshInstance3D? _cellarPhotoEntryDoor;
    private MeshInstance3D? _cellarPhotoEntryStep;
    private Marker3D? _cellarPhotoEntryAnchor;
    private InteractionTarget? _cellarPhotoEntryTarget;

    private void BuildCellarPhotoEntry()
    {
        if (_cellarPhotoEntryAnchor is not null
            || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge { IsPhotoWorldsCampaign: true }) return;
        _cellarPhotoEntryFacade = GetNode<Node3D>(CellarPhotoFacadePath);
        if (!TryResolveCellarPhotoEntryMeshes())
            throw new InvalidOperationException("The family house cellar entry requires its real seni door and step.");

        _cellarPhotoEntryAnchor = new Marker3D { Name = "CellarPhotoEntryAnchor" };
        _cellarPhotoEntryFacade.AddChild(_cellarPhotoEntryAnchor);
        _cellarPhotoEntryTarget = new InteractionTarget
        {
            Name = "CellarPhotoEntryTarget",
            InteractionId = CellarPhotoEntryInteractionId,
            CollisionLayer = 4u,
            CollisionMask = 0u,
            PresentationGate = CellarPhotoEntryInteractionWindowOpen
        };
        _cellarPhotoEntryTarget.ConfigureRayOnly();
        _cellarPhotoEntryTarget.AddChild(new CollisionShape3D
        {
            Name = "CellarPhotoEntryRay",
            Shape = new SphereShape3D { Radius = .42f }
        });
        _cellarPhotoEntryAnchor.AddChild(_cellarPhotoEntryTarget);
        _cellarPhotoEntryTarget.SetPresentationEnabled(false);
        AddCellarPhotoStepContact();
        UpdateCellarPhotoEntryPresentation();
    }

    private bool TryResolveCellarPhotoEntryMeshes()
    {
        if (_cellarPhotoEntryFacade is not { } facade || !GodotObject.IsInstanceValid(facade)
            || !facade.IsInsideTree() || facade.IsQueuedForDeletion()) return false;
        if (_cellarPhotoEntryDoor is not { } door || !GodotObject.IsInstanceValid(door)
            || !door.IsInsideTree() || door.IsQueuedForDeletion())
            _cellarPhotoEntryDoor = FindDescendants<MeshInstance3D>(facade)
                .SingleOrDefault(mesh => mesh.Name == "HeroHouse_SeniEntry_Door0_Leaf_LOD0");
        if (_cellarPhotoEntryStep is not { } step || !GodotObject.IsInstanceValid(step)
            || !step.IsInsideTree() || step.IsQueuedForDeletion())
            _cellarPhotoEntryStep = FindDescendants<MeshInstance3D>(facade)
                .SingleOrDefault(mesh => mesh.Name == "HeroHouse_SeniStep_LOD0");
        return _cellarPhotoEntryDoor is { Mesh: not null } && _cellarPhotoEntryStep is { Mesh: not null };
    }

    internal bool TryGetCellarPhotoEntryAnchor(out Node3D anchor)
    {
        anchor = null!;
        if (!IsInsideTree() || IsQueuedForDeletion() || !TryResolveCellarPhotoEntryMeshes()
            || _cellarPhotoEntryAnchor is not { } marker || !GodotObject.IsInstanceValid(marker)
            || !marker.IsInsideTree() || marker.IsQueuedForDeletion()) return false;
        var facade = _cellarPhotoEntryFacade!;
        var step = _cellarPhotoEntryStep!;
        var door = _cellarPhotoEntryDoor!;
        var front = ObservationHorizontal(facade.GlobalBasis.Z).Normalized();
        if (front.LengthSquared() < .5f) return false;
        var stepBounds = step.Mesh!.GetAabb();
        var stepCenter = step.GlobalTransform * stepBounds.GetCenter();
        var frontExtent = 0f;
        for (var index = 0; index < 8; index++)
            frontExtent = Mathf.Max(frontExtent,
                (step.GlobalTransform * stepBounds.GetEndpoint(index) - stepCenter).Dot(front));
        // Stand on terrain before the actual step, away from its raised edge and
        // the closed door. The physical capsule/support guard still owns entry.
        var foot = stepCenter + front * (frontExtent + .40f);
        foot.Y = AgentBAct1HeightField.CollisionGround(foot.X, foot.Z) + .05f;
        var doorCenter = door.GlobalTransform * door.Mesh!.GetAabb().GetCenter();
        var towardDoor = doorCenter - foot;
        towardDoor.Y = 0f;
        marker.GlobalPosition = foot;
        marker.GlobalRotation = new Vector3(0f, Mathf.Atan2(-towardDoor.X, -towardDoor.Z), 0f);
        if (_cellarPhotoEntryTarget is { } target && GodotObject.IsInstanceValid(target)
            && target.IsInsideTree() && !target.IsQueuedForDeletion())
            target.GlobalPosition = doorCenter + front * .08f;
        anchor = marker;
        return true;
    }

    private void AddCellarPhotoStepContact()
    {
        var step = _cellarPhotoEntryStep!;
        var sourcePath = step.GetPath().ToString();
        if (FindDescendants<CollisionShape3D>(this).Any(shape => !shape.IsQueuedForDeletion()
            && shape.HasMeta("authoredSourceMesh") && shape.GetMeta("authoredSourceMesh").AsString() == sourcePath)) return;
        var body = new StaticBody3D { Name = "CellarSeniStepContact", CollisionLayer = 2u, CollisionMask = 0u };
        body.SetMeta("collisionOwner", "act1-exterior-architecture");
        _cellarPhotoEntryFacade!.AddChild(body);
        body.AddChild(AuthoredSurfaceContact(body, step));
    }

    private bool CellarPhotoEntryProjectionReady() =>
        !IsQueuedForDeletion() && ActiveZoneId == "village_day"
        && _runtimeBridge is { IsPhotoWorldsCampaign: true } bridge && GodotObject.IsInstanceValid(bridge)
        && bridge.CurrentZoneId == "village_day" && bridge.SessionIdentity is { } session
        && bridge.CurrentFullGamePhotoWorldRouteProjection() is { ActiveSceneId: "urman.fullgame:scene/pw-home" } route
        && ReferenceEquals(route.SessionIdentity, session)
        && !PhotoWorldState.IsWorldCompleted(session.SelectState(), "W05")
        && bridge.IsInteractionAvailable(CellarPhotoEntryInteractionId);

    private bool CellarPhotoEntryInteractionWindowOpen() =>
        CellarPhotoEntryProjectionReady()
        && !MainMenuUi.AnyUndismissed(GetTree())
        && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController
            { ModalOpen: false, VehicleControlled: false, IsClimbingLadder: false, SessionTransitionActive: false };

    private void UpdateCellarPhotoEntryPresentation()
    {
        if (_cellarPhotoEntryTarget is not { } target || !GodotObject.IsInstanceValid(target)) return;
        var ready = TryGetCellarPhotoEntryAnchor(out _) && CellarPhotoEntryProjectionReady();
        if (_runtimeBridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            target.Prompt = bridge.ResolveText("urman.fullgame:text/pw-label-enter-cellar");
        target.SetPresentationEnabled(ready);
    }
}
