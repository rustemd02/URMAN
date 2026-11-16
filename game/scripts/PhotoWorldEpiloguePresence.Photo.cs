using System;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class PhotoWorldEpiloguePresence
{
    private object? _familyPhotoRequest;

    private bool BeginFamilyPhoto()
    {
        if (_familyPhotoRequest is not null || !CanFrameFamilyTablePhoto()
            || _bridge is not { } bridge || !IsEpilogueCurrent(bridge, out var session)
            || HasNowPhoto(bridge)
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.GetNodeOrNull<Camera3D>("Head/Camera3D") is not { Current: true } camera)
            return false;

        var request = new object();
        _familyPhotoRequest = request;
        _ = TakeFamilyPhotoAsync(bridge, session, player, camera, request);
        // The bridge owns exposure -> stored pixels -> authored S-NOW action.
        // The ordinary target must never dispatch the grant ahead of exposure.
        return false;
    }

    private bool FamilyPhotoRequestCurrent(RuntimeBridge bridge, object session,
        FirstPersonController player, object request) =>
        GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
        && ReferenceEquals(_familyPhotoRequest, request)
        && GodotObject.IsInstanceValid(bridge) && ReferenceEquals(_bridge, bridge)
        && ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
        && ReferenceEquals(session, bridge.SessionIdentity)
        && IsEpilogueCurrent(bridge, out var current) && ReferenceEquals(current, session)
        && !bridge.IsPhotoBookProjectionDeferred
        && GodotObject.IsInstanceValid(player) && player.IsInsideTree() && !player.IsQueuedForDeletion()
        && ReferenceEquals(GetTree().GetFirstNodeInGroup("player_controller"), player);

    private async Task TakeFamilyPhotoAsync(RuntimeBridge bridge, object session,
        FirstPersonController player, Camera3D camera, object request)
    {
        var taken = false;
        try
        {
            taken = await bridge.TakeFamilyNowPhotoAsync(camera,
                () => FamilyPhotoRequestCurrent(bridge, session, player, request)
                    && CanFrameFamilyTablePhoto());
        }
        catch (Exception error)
        {
            GD.PushWarning($"The family photo could not be developed: {error.Message}");
        }

        var current = FamilyPhotoRequestCurrent(bridge, session, player, request);
        if (GodotObject.IsInstanceValid(this) && ReferenceEquals(_familyPhotoRequest, request))
            _familyPhotoRequest = null;
        if (!current) return;
        if (taken && HasNowPhoto(bridge)) ShowNewFamilyPhoto();
        else if (IsInteractionWindowOpen())
            player.NotifyTraversal("Снимок не получился. Наведите камеру на близких и чай и попробуйте ещё раз.");
    }
}
