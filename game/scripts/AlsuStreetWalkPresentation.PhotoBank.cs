using Godot;
using System.Text.Json;

namespace Urman.Godot;

public partial class AlsuStreetWalkPresentation
{
    private const string PhotoBankSceneId = "urman.fullgame:scene/pw-bank-approach";
    private const string PhotoHomeSceneId = "urman.fullgame:scene/pw-home";
    private const string PhotoEpilogueSceneId = "urman.fullgame:scene/pw-epilogue";
    private const string PhotoBankQuestId = "urman.fullgame:quest/pw-w04";
    private const string PhotoBankMeetingTag = "bank-meeting-v1";
    private const string LegacyPoseTag = "legacy-v1";
    private const string PhotoBankMeetingMarkerPath = "Act1CoreWorldGreybox/PhotoBankApproach/AlsuMeeting";

    private bool _runtimeStateSubscribed;
    private bool _photoBankMeetingMode;
    private bool _photoBankGroundSnapPending;
    private string PhotoWorldPoseForSave => _photoBankMeetingMode ? PhotoBankMeetingTag : LegacyPoseTag;

    private bool EnsureRuntimeStateSubscription()
    {
        if (_bridge is null || !GodotObject.IsInstanceValid(_bridge))
        {
            _bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
            _runtimeStateSubscribed = false;
        }
        if (_bridge is null) return false;
        if (!_runtimeStateSubscribed)
        {
            _bridge.RuntimeStateChanged += ReadRuntimeState;
            _runtimeStateSubscribed = true;
        }
        return true;
    }

    /// <summary>
    /// Stages the existing, sole Alsu actor at the authored bank meeting marker
    /// during the full-game return from W04. Narrative walk state is untouched.
    /// </summary>
    internal bool StagePhotoBankMeeting()
    {
        if (!EnsureRuntimeStateSubscription()) return false;
        if (_bridge?.SessionIdentity is { } liveSession && !ReferenceEquals(_session, liveSession))
            ReadRuntimeState();
        if (_bridge is not { IsPhotoWorldsCampaign: true } bridge
            || bridge.SessionIdentity is not { } session
            || !ReferenceEquals(session, bridge.ProjectionSessionIdentity)
            || !ReferenceEquals(session, _session)
            || !IsLiveOwner(this)
            || _saving || _committing
            || bridge.CurrentZoneId != "village_day"
            || bridge.CurrentSpawnPointId != "pw-bank-approach"
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: PhotoBankSceneId } route
            || !ReferenceEquals(route.SessionIdentity, session)
            || !W04Completed(bridge.SelectRuntimeState())
            || !TryGetPhotoBankMeetingMarker(out var marker))
            return false;

        if (_photoBankMeetingMode)
        {
            if (new Vector2(_actor.GlobalPosition.X, _actor.GlobalPosition.Z)
                    .DistanceTo(new Vector2(marker.GlobalPosition.X, marker.GlobalPosition.Z)) > .04f
                || Math.Abs(Mathf.AngleDifference(_actor.GlobalRotation.Y, marker.GlobalRotation.Y)) > .02f)
                return false;
            SynchronizeTarget();
            return true;
        }

        _photoBankMeetingMode = true;
        _stepping = false;
        ReleaseSupportedFeet();
        _stepSound.Stop();
        _actor.GlobalPosition = marker.GlobalPosition;
        _actor.GlobalRotation = new(0f, marker.GlobalRotation.Y, 0f);
        _actor.ForceUpdateTransform();
        _body.ForceUpdateTransform();
        _savedYaw = _actor.GlobalRotation.Y;
        _savedYawBasis = _actor.GlobalBasis;
        _hasSavedYaw = true;
        _physicalValidationPending = true;
        PhysicalPlacementFailure = null;
        // The authored marker is intentionally 5 cm above its terrain datum;
        // the existing projection barrier snaps this new stage to real support.
        _photoBankGroundSnapPending = true;
        _requireMatchingSupportHeight = false;
        _validationAfterPhysicsFrame = Engine.GetPhysicsFrames() + 2;
        SetPhysicalContact(false);
        SynchronizeTarget();
        SetMeta("photoWorldPose", PhotoBankMeetingTag);
        return true;
    }

    private bool ProjectLoadedPhotoBankMeetingPose(JsonElement state, JsonElement person,
        object session, bool deferValidation)
    {
        if (!state.TryGetProperty("world.props", out var props) || props.ValueKind != JsonValueKind.Object
            || !props.TryGetProperty(SavePropId, out var record) || record.ValueKind != JsonValueKind.Object
            || !record.TryGetProperty("photoWorldPose", out var tag))
        {
            _photoBankMeetingMode = false;
            _photoBankGroundSnapPending = false;
            return false;
        }

        if (tag.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Alsu's saved presentation mode is invalid.");
        var mode = tag.GetString();
        var groundSnapPending = false;
        if (record.TryGetProperty("photoBankGroundSnapPending", out var snapPending))
        {
            if (snapPending.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidDataException("Alsu's saved bank support state is invalid.");
            groundSnapPending = snapPending.GetBoolean();
        }
        if (mode == LegacyPoseTag)
        {
            if (groundSnapPending)
                throw new InvalidDataException("Alsu's legacy pose cannot have a pending bank support snap.");
            _photoBankMeetingMode = false;
            _photoBankGroundSnapPending = false;
            return false;
        }
        if (mode != PhotoBankMeetingTag)
            throw new InvalidDataException("Alsu's saved presentation mode is unsupported.");

        _bridge ??= GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (_bridge is not { IsPhotoWorldsCampaign: true } bridge
            || !ReferenceEquals(session, bridge.ProjectionSessionIdentity)
            || bridge.ActiveSceneId is not (PhotoBankSceneId or PhotoHomeSceneId or PhotoEpilogueSceneId)
            || !W04Completed(state))
            throw new InvalidDataException("Alsu's saved bank meeting pose is outside its completed full-game route.");
        if (!TryGetPhotoBankMeetingMarker(out var marker))
            throw new InvalidDataException("Alsu's saved bank meeting marker is unavailable.");

        var checkpoint = 0;
        if (person.ValueKind == JsonValueKind.Object && person.TryGetProperty("walk_checkpoint", out var savedCheckpoint)
            && (savedCheckpoint.ValueKind != JsonValueKind.Number || !savedCheckpoint.TryGetInt32(out checkpoint)
                || checkpoint < 0 || checkpoint >= Waypoints.Length))
            throw new InvalidDataException("Alsu's saved checkpoint is invalid.");
        var arrived = ReadBool(person, "walk_arrived");
        if (arrived && checkpoint != Waypoints.Length - 1)
            throw new InvalidDataException("Alsu's saved arrival disagrees with its checkpoint.");

        if (!record.TryGetProperty("schemaVersion", out var schema)
            || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1
            || !record.TryGetProperty("checkpoint", out var saved) || saved.ValueKind != JsonValueKind.Number
            || !saved.TryGetInt32(out var capturedCheckpoint) || capturedCheckpoint != checkpoint
            || !record.TryGetProperty("position", out var at) || at.ValueKind != JsonValueKind.Object
            || !record.TryGetProperty("pendingCheckpoint", out var next)
            || next.ValueKind != JsonValueKind.Number || !next.TryGetInt32(out var pending))
            throw new InvalidDataException("Alsu's saved bank pose disagrees with its street-walk state.");

        var savedPosition = new Vector3(ReadFinite(at, "x"), ReadFinite(at, "y"), ReadFinite(at, "z"));
        var savedYaw = ReadFinite(record, "yaw");
        var travel = ReadFinite(record, "segmentTravel");
        var expectedYaw = marker.GlobalRotation.Y;
        if (new Vector2(savedPosition.X, savedPosition.Z)
                .DistanceTo(new Vector2(marker.GlobalPosition.X, marker.GlobalPosition.Z)) > .04f
            || groundSnapPending && Math.Abs(savedPosition.Y - marker.GlobalPosition.Y) > .005f
            || Math.Abs(Mathf.AngleDifference(savedYaw, expectedYaw)) > .02f)
            throw new InvalidDataException("Alsu's saved bank pose no longer matches its authored meeting point.");

        if (checkpoint == Waypoints.Length - 1)
        {
            if (pending != checkpoint || travel < 0 || travel > .04f)
                throw new InvalidDataException("Alsu's saved bank pose disagrees with her reached street checkpoint.");
        }
        else
        {
            var maxTravel = checkpoint < Waypoints.Length - 1
                ? (Waypoints[checkpoint + 1] - Waypoints[checkpoint]).Length() : 0f;
            if (pending < checkpoint || pending > Math.Min(checkpoint + 1, Waypoints.Length - 1)
                || travel < 0 || travel > maxTravel + .06f
                || pending == checkpoint + 1 && travel < maxTravel - .06f)
                throw new InvalidDataException("Alsu's saved bank pose disagrees with her street-walk progress.");
        }

        _session = session;
        _photoBankMeetingMode = true;
        _checkpoint = checkpoint;
        _requested = ReadBool(person, "walk_requested");
        _arrived = arrived;
        _segmentTravel = travel;
        _pendingCheckpoint = pending;
        _stepping = false;
        _committing = false;
        SessionWalkedMetres = travel;
        ReleaseSupportedFeet();
        _stepSound.Stop();
        _actor.GlobalPosition = new Vector3(marker.GlobalPosition.X, savedPosition.Y, marker.GlobalPosition.Z);
        _actor.GlobalRotation = new(0f, expectedYaw, 0f);
        _savedYaw = expectedYaw;
        _savedYawBasis = _actor.GlobalBasis;
        _hasSavedYaw = true;
        _actor.ForceUpdateTransform();
        _body.ForceUpdateTransform();
        _physicalValidationPending = true;
        PhysicalPlacementFailure = null;
        // Only an unvalidated stage saved at the authored marker may use the
        // existing one-time snap; validated saves keep their saved support Y.
        _photoBankGroundSnapPending = groundSnapPending;
        _requireMatchingSupportHeight = !groundSnapPending;
        _validationAfterPhysicsFrame = Engine.GetPhysicsFrames() + 2;
        SetPhysicalContact(false);
        SynchronizeTarget();
        if (_exteriorEnabled && !deferValidation && ProjectionBarrierReady()) ValidateProjectedPhysicalPose();
        else SetMeta("physicalSupportValidation", _exteriorEnabled
            ? "pending projected bodies and two physics frames" : "pending exterior physics");
        SetMeta("photoWorldPose", PhotoBankMeetingTag);
        SetMeta("loadedActualPose", true);
        SetMeta("walkCheckpoint", checkpoint);
        SetMeta("walkArrived", arrived);
        return true;
    }

    private bool TryGetPhotoBankMeetingMarker(out Marker3D marker)
    {
        marker = null!;
        if (GetTree().GetFirstNodeInGroup("act1_connected_world") is not Act1ConnectedWorld world
            || world.GetNodeOrNull<Marker3D>(PhotoBankMeetingMarkerPath) is not { } found
            || !GodotObject.IsInstanceValid(found) || !found.IsInsideTree())
            return false;
        marker = found;
        return true;
    }

    private static bool W04Completed(JsonElement state) =>
        state.TryGetProperty("quests", out var quests)
        && quests.ValueKind == JsonValueKind.Object
        && quests.TryGetProperty(PhotoBankQuestId, out var quest)
        && quest.ValueKind == JsonValueKind.Object
        && quest.TryGetProperty("status", out var status)
        && status.ValueKind == JsonValueKind.String
        && status.GetString() == "completed";
}
