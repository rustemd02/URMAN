using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class Main
{
    private object? _nivaExitSession;
    private PlayerTransform? _nivaExitPreviousTransform;

    // The ride supplies a candidate validated by the ordinary vehicle exit
    // owner. Put the hidden body there before the authored Home checkpoint.
    internal bool TryPlaceNivaPassengerExit(object expectedSession, Vector3 feet, float yawDegrees)
    {
        if (_fullGameRouteBridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(bridge.SessionIdentity, expectedSession)
            || bridge.IsPhotoWorldPresentationDeferred || bridge.ActiveSceneId != NivaArrivalSceneId
            || _connectedWorld is not { IsBuilt: true, ActiveZoneId: "village_day" }
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !feet.IsFinite() || !float.IsFinite(yawDegrees) || !player.CanStandAt(feet)) return false;
        if (ReferenceEquals(_nivaExitSession, expectedSession)) return true;
        _nivaExitSession = expectedSession;
        _nivaExitPreviousTransform = player.CapturePortableTransform();
        player.ApplyZoneSpawn(feet, yawDegrees);
        player.Velocity = Vector3.Zero;
        LastSpawnYawDegrees = yawDegrees;
        return true;
    }

    internal void CancelNivaPassengerExit(object expectedSession)
    {
        if (!ReferenceEquals(_nivaExitSession, expectedSession)) return;
        if (_fullGameRouteBridge is { } bridge && GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(bridge.SessionIdentity, expectedSession))
        {
            // Successful dispatch can precede deferred Main reconciliation.
            // Keep its intent until Home consumes it; never undo that checkpoint.
            if (bridge.ActiveSceneId == HomeSceneId) return;
            if (bridge.ActiveSceneId == NivaArrivalSceneId && _nivaExitPreviousTransform is { } previous
                && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
                player.ApplyPortableTransform(previous);
        }
        _nivaExitSession = null;
        _nivaExitPreviousTransform = null;
    }

    private bool ConsumeNivaPassengerExit(RuntimeBridge.FullGamePhotoWorldRouteProjection projection, RuntimeBridge bridge)
    {
        if (_nivaExitSession is null) return false;
        if (!ReferenceEquals(_nivaExitSession, projection.SessionIdentity))
        {
            _nivaExitSession = null;
            _nivaExitPreviousTransform = null;
            return false;
        }
        if (projection.ActiveSceneId != HomeSceneId || _connectedWorld is not { IsBuilt: true }
            || bridge.CurrentZoneId != "village_day") return false;
        ActiveZoneScenePath = ZoneScenes["village_day"];
        _hasLoadedInitialZone = true;
        _preserveLoadedPhotoWorldTransform = false;
        _loadedPhotoWorldTransformSession = null;
        _lastFullGameRouteSession = projection.SessionIdentity;
        _lastFullGameRouteScene = HomeSceneId;
        _nivaExitSession = null;
        _nivaExitPreviousTransform = null;
        return true;
    }
}
