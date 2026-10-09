using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urman.Godot;

/// <summary>
/// Projects one saved conversation invitation into a short walk on the existing
/// village street. RuntimeBridge owns consent/checkpoints; this component owns
/// only the visible actor, its one physical body, and evidence of actual travel.
/// </summary>
public partial class AlsuStreetWalkPresentation : Node3D
{
    public const string CharacterId = "urman.chapter1:character/alsu";
    private const string Group = "alsu_street_walk";
    public static readonly Vector2[] Waypoints =
        [new(.85f, 2.05f), new(.70f, -1.5f), new(.15f, -5.5f), new(.05f, -8.8f)];
    private Node3D _actor = null!;
    private InteractionTarget _target = null!;
    private AnimatableBody3D _body = null!;
    private RuntimeBridge? _bridge;
    private FirstPersonController? _player;
    private object? _session;
    private bool _requested;
    private bool _arrived;
    private int _checkpoint;
    private int _pendingCheckpoint;
    private bool _committing;
    private bool _saving;
    private Task<bool>? _flushTask;
    private bool _exteriorEnabled = true;
    private string _presentationZone = "village_day";
    private bool _physicalValidationPending = true;
    private ulong _validationAfterPhysicsFrame;
    private bool _requireMatchingSupportHeight;
    private float? _authoredAnchorHeight;
    public string? PhysicalPlacementFailure { get; private set; }
    public bool PhysicalAccessReady => _exteriorEnabled && !_physicalValidationPending
        && PhysicalPlacementFailure is null && _session is not null;
    public const string SavePropId = "npc/alsu-street-walk";
    private float _segmentTravel;
    private Skeleton3D _skeleton = null!;
    private RinatFootPlacementModifier _feetModifier = null!;
    private readonly List<Foot> _feet = new();
    private readonly global::Godot.Collections.Array<Rid> _groundExcludes = new();
    // Reused ground probe: both GroundAt and RecordGroundRefusal are synchronous and
    // never nested, so one query object can carry only the mutating From/To just like
    // VehicleHorsePose.Ground does. _groundExcludes is filled once in _Ready and never
    // mutated afterwards, so the exclusion set stays identical to the per-call Create.
    private PhysicsRayQueryParameters3D? _groundRay;
    private AudioStreamPlayer3D _stepSound = null!;
    private bool _stepping;
    private float _stepT;
    private Vector3 _stepStart;
    private Vector3 _stepEnd;
    private float _startYaw;
    private float _endYaw;
    private float _savedYaw;
    private Basis _savedYawBasis;
    private bool _hasSavedYaw;
    private int _swing;
    private Transform3D[] _initialFeet = [];
    private Vector3[] _initialSoles = [];
    private Vector3[] _landingSoles = [];
    private bool _turningStep;
    private bool _humanRig;
    private bool _walkingClip;
    // VIS-046: weight, not pixels. Three things the walk owner keeps for itself:
    // how far the pelvis shifts onto the planted leg, the clip tempo that matches
    // her real footfalls, and the measured slide of a boot that must not move.
    private const float WeightDropMetres = .018f;
    private const float WeightLateralMetres = .020f;
    private const float WeightEasePerSecond = 5f;
    internal const float SupportSoleSlideBudgetMetres = .020f;
    private float _weightDrop;
    private float _weightLateral;
    private Vector3 _weightDirection;
    private float _gaitCycleSeconds;
    private float _motionScale = 1f;
    private float _maxSupportSoleSlide = -1f;
    private Vector3[] _plantedSoleSamples = [];
    private sealed record Foot(int Leg, int Ankle, Transform3D LegRest, Vector3 SoleLocal);

    private float _weightTargetDrop;
    private float _weightTargetLateral;
    private Vector3 _weightTargetDirection;

    public Node3D Actor => _actor;
    public InteractionTarget Target => _target;
    public int Checkpoint => _checkpoint;
    public bool Arrived => _arrived;
    public bool ControlsFacing => _stepping || (_requested && !_arrived);
    public float SessionWalkedMetres { get; private set; }

    public static AlsuStreetWalkPresentation? Current(SceneTree tree) =>
        tree.GetNodesInGroup(Group).OfType<AlsuStreetWalkPresentation>()
            .FirstOrDefault(walk => IsLiveOwner(walk) && walk._actor.IsVisibleInTree());

    public static AlsuStreetWalkPresentation? SessionOwner(SceneTree tree) =>
        tree.GetNodesInGroup(Group).OfType<AlsuStreetWalkPresentation>().FirstOrDefault(IsLiveOwner);

    // SurfaceGetArrays returns a caller-owned array and copies the whole surface into
    // it; releasing it inside the projection keeps that copy out of the finalizer queue.
    private static Vector3[] SurfaceVertices(Mesh mesh, int surface)
    {
        using var arrays = mesh.SurfaceGetArrays(surface);
        return arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
    }

    private static bool IsLiveOwner(AlsuStreetWalkPresentation walk)
    {
        if (!IsInstanceValid(walk) || !walk.IsInsideTree()) return false;
        for (Node? ancestor = walk; ancestor is not null; ancestor = ancestor.GetParent())
            if (!IsInstanceValid(ancestor) || ancestor.IsQueuedForDeletion()) return false;
        return true;
    }

    internal static AlsuStreetWalkPresentation Attach(Node3D actor, InteractionTarget target)
    {
        if (actor.GetNodeOrNull<AlsuStreetWalkPresentation>("AlsuStreetWalk") is { } existing) return existing;
        GeneratedCharacterKitDressing.GroundSolesOnAnchor(actor);
        target.ConfigureRayOnly();
        // The interaction ray must reach the semantic shell before the narrower
        // physical capsule from either side of the person.
        target.GetNode<CollisionShape3D>("InteractionProxyCollisionShape").Shape =
            new BoxShape3D { Size = new(.68f, 1.72f, .68f) };
        var walk = new AlsuStreetWalkPresentation { Name = "AlsuStreetWalk", _actor = actor, _target = target };
        actor.AddChild(walk);
        return walk;
    }

    public override void _Ready()
    {
        AddToGroup(Group);
        SetMeta("stateOwner", "RuntimeBridge/npc/alsu; actual supported travel only; no knowledge effects");
        _body = new AnimatableBody3D { Name = "AlsuPhysicalContact", CollisionLayer = 1,
            CollisionMask = 3, SyncToPhysics = false };
        _body.AddChild(new CollisionShape3D { Position = Vector3.Up * .85f,
            Shape = new CapsuleShape3D { Radius = .28f, Height = 1.70f } });
        _body.SetMeta("collisionOwner", "AlsuStreetWalkPresentation/one visible Alsu");
        AddChild(_body);
        _groundExcludes.Add(_body.GetRid());
        _groundExcludes.Add(_target.GetRid());
        _stepSound = new AudioStreamPlayer3D { Name = "AlsuSnowStep", Bus = AudioSettingsService.SfxBus,
            Stream = ResourceLoader.Load<AudioStream>("res://assets/audio/act1/footsteps/step_snow_packed_01.wav"),
            VolumeDb = -17f, UnitSize = 2f, MaxDistance = 10f };
        AddChild(_stepSound);
        PrepareFeet();
        SynchronizeTarget();
    }

    private Act1DemoRoot? _demoPrologueRoot;

    public override void _PhysicsProcess(double delta)
    {
        if (_bridge is null)
        {
            _bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
            if (_bridge is null) return;
            _bridge.RuntimeStateChanged += ReadRuntimeState;
        }
        _player ??= GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (!ReferenceEquals(_session, _bridge.SessionIdentity)) ReadRuntimeState();
        SynchronizeTarget();
        GroundStandingFeet();
        EaseWeightTransfer((float)delta);
        if (_session is null || _player is null) return;
        if (_exteriorEnabled && _physicalValidationPending && _bridge.SessionIdentity is not null
            && ProjectionBarrierReady())
        {
            try { ValidateProjectedPhysicalPose(); }
            catch (InvalidDataException error) { RefuseLivePhysicalPlacement(error); }
            SynchronizeTarget();
        }
        if (!PhysicalAccessReady) return;
        // The demo's prologue block (forest teaser + Niva ride) precedes any
        // villager routine: Alsu does not walk her street segment while the
        // opening cutscene chain is still holding the session.
        if ((_demoPrologueRoot ??= GetTree().GetFirstNodeInGroup("act1_demo_root") as Act1DemoRoot) is { IntroVisible: true }
            || _player.ModalOpen || _player.VehicleControlled || _bridge.CurrentZoneId != "village_day")
        {
            _stepSound.StreamPaused = true;
            return;
        }
        _stepSound.StreamPaused = false;
        if (_committing || _saving) return;
        if (_pendingCheckpoint > _checkpoint)
        {
            if (CanCommitCheckpoint(_pendingCheckpoint)) CommitCheckpoint(_pendingCheckpoint);
            return;
        }
        if (!_requested || _arrived) { PlayGait(false); return; }
        // Finish a started footfall when the companion stops following, then
        // wait in place. A player can leave, come back, or explicitly defer.
        if (!_stepping)
        {
            if (_actor.GlobalPosition.DistanceTo(_player.GlobalPosition) > 4.0f) { PlayGait(false); return; }
            if (!BeginStep()) { PlayGait(false); return; }
        }
        PlayGait(true);
        AdvanceStep((float)delta);
        SynchronizeTarget();
    }

    private void ReadRuntimeState()
    {
        if (_bridge?.SessionIdentity is not { } session) return;
        var freshSession = !ReferenceEquals(_session, session);
        var state = _bridge.SelectRuntimeState();
        var person = state.TryGetProperty("npc", out var people) && people.TryGetProperty(CharacterId, out var alsu)
            ? alsu : default;
        _requested = ReadBool(person, "walk_requested");
        _arrived = ReadBool(person, "walk_arrived");
        var checkpoint = person.ValueKind == JsonValueKind.Object && person.TryGetProperty("walk_checkpoint", out var saved)
            && saved.ValueKind == JsonValueKind.Number && saved.TryGetInt32(out var value)
            ? Math.Clamp(value, 0, Waypoints.Length - 1) : 0;
        if (_arrived) checkpoint = Waypoints.Length - 1;
        if (freshSession)
        {
            // Covers both a loaded session and reconstruction of an existing
            // compact street. The snapshot pose must survive a new component.
            // The save owner calls projection directly and must receive the
            // exception for rollback. A live startup/event cannot throw out of
            // Godot's physics callback; keep the same explicit blocked state.
            try { ProjectLoadedPhysicalState(deferValidation: true); }
            catch (InvalidDataException error) { RefuseLivePhysicalPlacement(error); }
            return;
        }
        else if (checkpoint > _checkpoint)
        {
            _segmentTravel = 0;
            _pendingCheckpoint = checkpoint;
        }
        if (_arrived || (!_requested && _stepping))
        {
            _stepping = false;
            ReleaseSupportedFeet();
        }
        _checkpoint = checkpoint;
        SetMeta("walkCheckpoint", _checkpoint);
        SetMeta("walkRequested", _requested);
        SetMeta("walkArrived", _arrived);
        SynchronizeTarget();
    }

    private static bool ReadBool(JsonElement person, string key) => person.ValueKind == JsonValueKind.Object
        && person.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;

    public bool CanCommitCheckpoint(int next)
    {
        if (_bridge?.SessionIdentity is not { } session || !ReferenceEquals(_session, session)
            || !PhysicalAccessReady
            || !_requested || _arrived || next != _checkpoint + 1 || next != _pendingCheckpoint
            || next >= Waypoints.Length || _stepping || _player is null || _player.ModalOpen
            || _player.VehicleControlled || _bridge.CurrentZoneId != "village_day"
            || _actor.GlobalPosition.DistanceTo(_player.GlobalPosition) > 4.1f) return false;
        var target = Waypoints[next];
        return new Vector2(_actor.GlobalPosition.X, _actor.GlobalPosition.Z).DistanceTo(target) <= .035f
            && _segmentTravel >= Waypoints[next - 1].DistanceTo(target) - .06f;
    }

    private async void CommitCheckpoint(int next)
    {
        var session = _session;
        var committed = false;
        _committing = true;
        try
        {
            committed = await _bridge!.AdvanceAlsuStreetWalkAsync(next);
            if (committed && ReferenceEquals(session, _session)) ReadRuntimeState();
        }
        catch (Exception error) { GD.PushError("Alsu walk checkpoint failed: " + error.Message); }
        finally { if (ReferenceEquals(session, _session)) _committing = false; }
        if (committed && ReferenceEquals(session, _session))
            await _bridge!.SaveSlotAsync(RuntimeBridge.CheckpointSlot);
    }

    // RuntimeBridge calls this last before its existing snapshot capture. Motion
    // stays held through the props dispatch; concurrent save callers share it.
    public Task<bool> FlushForSaveAsync()
    {
        if (_flushTask is { IsCompleted: false }) return _flushTask;
        return _flushTask = FlushActualPoseAsync();
    }

    public void DisableContactForZoneDisposal()
    {
        _saving = true;
        _body.CollisionLayer = 0;
        _body.CollisionMask = 0;
        _target.SetPresentationEnabled(false);
        _target.CollisionMask = 0;
    }

    // The village scenery remains visible through interior windows, but its
    // terrain and architecture have no active physics until the player returns.
    public void SetZonePresentation(string zoneId, bool exterior)
    {
        var returning = exterior && !_exteriorEnabled;
        _presentationZone = zoneId;
        _exteriorEnabled = exterior;
        if (!exterior || returning)
        {
            _physicalValidationPending = true;
            PhysicalPlacementFailure = null;
            _requireMatchingSupportHeight = _session is not null;
            _validationAfterPhysicsFrame = Engine.GetPhysicsFrames() + 2;
            _stepping = false;
            ReleaseSupportedFeet();
            _stepSound.Stop();
            SetPhysicalContact(false);
            SetMeta("physicalSupportValidation", "pending exterior physics");
        }
        SynchronizeTarget();
    }

    private async Task<bool> FlushActualPoseAsync()
    {
        _bridge ??= GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (_bridge?.SessionIdentity is not { } session) return false;
        _saving = true;
        try
        {
            while (_committing)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                if (!ReferenceEquals(session, _bridge.SessionIdentity) || !IsLiveOwner(this)) return false;
            }
            ReadRuntimeState();
            // A fresh session or an exterior zone return can still be waiting
            // for its real support/body barrier. Persist only the validated
            // pose; the existing projection method intentionally preserves
            // the exact pose while the exterior floor is disabled indoors.
            await CompleteLoadedPhysicalProjectionAsync();
            if (!ReferenceEquals(session, _bridge.SessionIdentity) || !IsLiveOwner(this)) return false;
            var position = _actor.GlobalPosition;
            var record = new JsonObject
            {
                ["propId"] = SavePropId, ["schemaVersion"] = 1, ["checkpoint"] = _checkpoint,
                ["position"] = new JsonObject { ["x"] = position.X, ["y"] = position.Y, ["z"] = position.Z },
                ["yaw"] = CaptureYaw(), ["segmentTravel"] = _segmentTravel,
                ["pendingCheckpoint"] = _pendingCheckpoint
            };
            return await _bridge.DispatchWorldPropsAsync(new JsonArray(record))
                && ReferenceEquals(session, _bridge.SessionIdentity);
        }
        finally { _saving = false; }
    }

    private float CaptureYaw()
    {
        // A restored Euler angle can lose a float step when read back from its
        // basis. Retain the source value only for an exactly unchanged basis;
        // ordinary facing and every actual walking turn still update the save.
        if (!_hasSavedYaw || _actor.GlobalBasis != _savedYawBasis)
        {
            _savedYaw = _actor.GlobalRotation.Y;
            _savedYawBasis = _actor.GlobalBasis;
            _hasSavedYaw = true;
        }
        return _savedYaw;
    }

    /// <summary>Called by the save owner before testing the restored player.</summary>
    public void ProjectLoadedPhysicalState(bool deferValidation = true)
    {
        _bridge ??= GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (_bridge?.ProjectionSessionIdentity is not { } session) return;
        var state = _bridge.SelectRuntimeState();
        var person = state.TryGetProperty("npc", out var people) && people.TryGetProperty(CharacterId, out var alsu)
            ? alsu : default;
        var checkpoint = 0;
        if (person.ValueKind == JsonValueKind.Object && person.TryGetProperty("walk_checkpoint", out var savedCheckpoint)
            && (savedCheckpoint.ValueKind != JsonValueKind.Number || !savedCheckpoint.TryGetInt32(out checkpoint)
                || checkpoint < 0 || checkpoint >= Waypoints.Length))
            throw new InvalidDataException("Alsu's saved checkpoint is invalid.");
        var arrived = ReadBool(person, "walk_arrived");
        if (arrived && checkpoint != 3) throw new InvalidDataException("Alsu's saved arrival disagrees with its checkpoint.");
        var point = Waypoints[checkpoint];
        _authoredAnchorHeight ??= _actor.GlobalPosition.Y;
        var position = new Vector3(point.X, _authoredAnchorHeight.Value, point.Y);
        var yaw = checkpoint == 0 ? Mathf.DegToRad(-15) : Mathf.Pi;
        var travel = 0f;
        var pending = checkpoint;
        var actualPose = false;
        if (state.TryGetProperty("world.props", out var props) && props.ValueKind == JsonValueKind.Object
            && props.TryGetProperty(SavePropId, out var record))
        {
            if (record.ValueKind != JsonValueKind.Object || !record.TryGetProperty("schemaVersion", out var schema)
                || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1
                || !record.TryGetProperty("checkpoint", out var saved) || saved.ValueKind != JsonValueKind.Number
                || !saved.TryGetInt32(out var capturedCheckpoint)
                || capturedCheckpoint != checkpoint || !record.TryGetProperty("position", out var at)
                || at.ValueKind != JsonValueKind.Object || !record.TryGetProperty("pendingCheckpoint", out var next)
                || next.ValueKind != JsonValueKind.Number || !next.TryGetInt32(out pending))
                throw new InvalidDataException("Alsu's saved physical pose disagrees with its narrative checkpoint.");
            position = new(ReadFinite(at,"x"), ReadFinite(at,"y"), ReadFinite(at,"z"));
            yaw = ReadFinite(record,"yaw");
            travel = ReadFinite(record,"segmentTravel");
            var offset = new Vector2(position.X,position.Z) - point;
            if (checkpoint == 3)
            {
                if (offset.Length() > .04f || travel > .04f || travel < 0 || pending != 3)
                    throw new InvalidDataException("Alsu's saved final pose is outside its reached meeting point.");
            }
            else
            {
                var segment = Waypoints[checkpoint + 1] - point;
                var length = segment.Length();
                var along = offset.Dot(segment / length);
                var across = (offset - segment.Normalized() * along).Length();
                if (across > .04f || along < -.02f || along > length + .04f
                    || travel < 0 || Math.Abs(travel - Math.Max(0,along)) > .06f
                    || pending < checkpoint || pending > checkpoint + 1
                    || pending == checkpoint + 1 && (length-along > .04f || travel < length-.06f))
                    throw new InvalidDataException("Alsu's saved travel is not on its actually walked segment.");
            }
            actualPose = true;
        }
        _session = session; // Avoid a later RuntimeStateChanged resetting this exact pose.
        _checkpoint = checkpoint;
        _requested = ReadBool(person,"walk_requested");
        _arrived = arrived;
        _segmentTravel = travel;
        _pendingCheckpoint = pending;
        _stepping = false;
        _committing = false;
        SessionWalkedMetres = travel;
        ReleaseSupportedFeet();
        _stepSound.Stop();
        _actor.GlobalPosition = position;
        _actor.GlobalRotation = new(0,yaw,0);
        _savedYaw = yaw;
        _savedYawBasis = _actor.GlobalBasis;
        _hasSavedYaw = true;
        _actor.ForceUpdateTransform();
        _body.ForceUpdateTransform();
        _physicalValidationPending = true;
        PhysicalPlacementFailure = null;
        _requireMatchingSupportHeight = actualPose;
        _validationAfterPhysicsFrame = Engine.GetPhysicsFrames() + 2;
        SetPhysicalContact(false);
        SynchronizeTarget();
        // Every participant first applies its pose. A live session also waits
        // for the same server barrier; scene transforms alone are not evidence
        // that the vehicle/player bodies have reached those transforms.
        if (_exteriorEnabled && !deferValidation && ProjectionBarrierReady()) ValidateProjectedPhysicalPose();
        else SetMeta("physicalSupportValidation", _exteriorEnabled
            ? "pending projected bodies and two physics frames" : "pending exterior physics");
        SetMeta("loadedActualPose", actualPose);
        SetMeta("walkCheckpoint", checkpoint);
        SetMeta("walkArrived", arrived);
    }

    private VehicleFleet? ProjectionFleet() => GetTree().GetNodesInGroup("vehicle_fleet")
        .OfType<VehicleFleet>().FirstOrDefault(fleet => IsInstanceValid(fleet)
            && fleet.IsInsideTree() && !fleet.IsQueuedForDeletion());

    private bool ProjectionBarrierReady() => Engine.GetPhysicsFrames() >= _validationAfterPhysicsFrame
        && ProjectionFleet()?.PlacementValidationDeferred != true;

    /// <summary>The existing save owner awaits this before testing the loaded player.</summary>
    public async Task CompleteLoadedPhysicalProjectionAsync()
    {
        var session = _session;
        void RequireCurrentProjection()
        {
            if (session is null || !IsLiveOwner(this) || _bridge is null || !IsInstanceValid(_bridge)
                || !ReferenceEquals(session, _session)
                || !ReferenceEquals(session, _bridge.ProjectionSessionIdentity))
                throw new InvalidDataException("Alsu's physical projection changed session before validation completed.");
        }
        RequireCurrentProjection();
        // Interior presentation intentionally disables the village floor. Keep
        // the exact saved pose and pending status until its real exterior returns.
        if (!_exteriorEnabled) return;
        while (!ProjectionBarrierReady())
        {
            if (ProjectionFleet()?.ProjectionFailure is { } fleetFailure)
                throw new InvalidDataException("Alsu's physical projection awaits a rejected vehicle placement: " + fleetFailure);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            RequireCurrentProjection();
            if (!_exteriorEnabled) return;
        }
        RequireCurrentProjection();
        if (PhysicalPlacementFailure is { } failure) throw new InvalidDataException(failure);
        if (_physicalValidationPending) ValidateProjectedPhysicalPose();
    }

    internal string DescribeWalkEligibility()
    {
        var state = _bridge?.SelectRuntimeState() ?? default;
        var person = state.ValueKind == JsonValueKind.Object && state.TryGetProperty("npc", out var people)
            && people.TryGetProperty(CharacterId, out var alsu) ? alsu : default;
        return JsonSerializer.Serialize(new
        {
            runtimeRequested = ReadBool(person, "walk_requested"), localRequested = _requested,
            arrived = _arrived, checkpoint = _checkpoint, pendingCheckpoint = _pendingCheckpoint,
            stepping = _stepping, saving = _saving, committing = _committing,
            physicalPending = _physicalValidationPending, PhysicalPlacementFailure, PhysicalAccessReady,
            exterior = _exteriorEnabled, presentationZone = _presentationZone,
            zone = _bridge?.CurrentZoneId, modal = _player?.ModalOpen, vehicle = _player?.VehicleControlled,
            sessionMatchesLive = _session is not null && ReferenceEquals(_session, _bridge?.SessionIdentity),
            sessionMatchesProjection = _session is not null && ReferenceEquals(_session, _bridge?.ProjectionSessionIdentity),
            physicsFrame = Engine.GetPhysicsFrames(), validationAfterPhysicsFrame = _validationAfterPhysicsFrame,
            fleetPending = ProjectionFleet()?.PlacementValidationDeferred,
            actor = _actor.GlobalPosition.ToString(), player = _player?.GlobalPosition.ToString(),
            walkedMetres = SessionWalkedMetres, blocked = GetMeta("walkBlockedOwner", "none").AsString(),
            // VIS-046 evidence, kept beside the eligibility numbers the proof already reads.
            maxSupportSoleSlideMetres = _maxSupportSoleSlide,
            supportSoleSlideBudgetMetres = SupportSoleSlideBudgetMetres,
            walkCadenceScale = _motionScale,
            weightDropMetres = _weightDrop,
            weightLateralMetres = _weightLateral
        });
    }

    private void ValidateProjectedPhysicalPose()
    {
        var position = _actor.GlobalPosition;
        if (!GroundAt(position, out var support, out _)
            || _requireMatchingSupportHeight && Math.Abs(position.Y - support.Y) > .04f)
            throw new InvalidDataException("Alsu's saved feet have no matching physical support.");
        // Only old saves without an actual pose use the authored anchor until
        // its real street support is available. Current saves keep their exact Y.
        if (!_requireMatchingSupportHeight)
        {
            position.Y = support.Y;
            _actor.GlobalPosition = position;
        }
        _actor.ForceUpdateTransform();
        _body.ForceUpdateTransform();
        SetPhysicalContact(true);
        if (!BodyMotionClear(Vector3.Zero, recoveryAsCollision: true))
        {
            SetPhysicalContact(false);
            throw new InvalidDataException("Alsu's saved physical body intersects an obstacle or the loaded player.");
        }
        _physicalValidationPending = false;
        _requireMatchingSupportHeight = true;
        PhysicalPlacementFailure = null;
        if (HasMeta("walkBlockedOwner")) RemoveMeta("walkBlockedOwner");
        if (HasMeta("physicalPlacementFailure")) RemoveMeta("physicalPlacementFailure");
        SetMeta("physicalSupportValidation", "actual exterior support and body verified");
        SynchronizeTarget();
    }

    private void SetPhysicalContact(bool enabled)
    {
        _body.CollisionLayer = enabled ? 1u : 0u;
        _body.CollisionMask = enabled ? 3u : 0u;
        if (!enabled) _target.SetPresentationEnabled(false);
    }

    private void RefuseLivePhysicalPlacement(InvalidDataException error)
    {
        PhysicalPlacementFailure = error.Message;
        _physicalValidationPending = false;
        SetPhysicalContact(false);
        SetMeta("physicalPlacementFailure", error.Message);
        _player?.NotifyTraversal("Алсу пока не может продолжить: место встречи перекрыто. Попробуйте вернуться или загрузить сохранение.");
        GD.PushWarning("Alsu's live physical placement was refused: " + error.Message);
    }

    private static float ReadFinite(JsonElement record,string field)
    {
        if (!record.TryGetProperty(field,out var element) || element.ValueKind != JsonValueKind.Number
            || !element.TryGetSingle(out var value) || !float.IsFinite(value))
            throw new InvalidDataException("Alsu's saved physical field is invalid: "+field);
        return value;
    }

    private bool BeginStep()
    {
        // An inconsistent imported snapshot must not manufacture arrival or
        // index beyond the path. The atomic runtime writer never creates it.
        if (_checkpoint < 0 || _checkpoint >= Waypoints.Length - 1) return false;
        var next = Waypoints[_checkpoint + 1];
        var direction = new Vector3(next.X, _actor.GlobalPosition.Y, next.Y) - _actor.GlobalPosition;
        var remaining = direction.Length();
        if (remaining < .002f) { _pendingCheckpoint = _checkpoint + 1; return false; }
        direction /= remaining;
        _stepStart = _actor.GlobalPosition;
        _stepEnd = _stepStart + direction * Math.Min(.26f, remaining);
        var supported = GroundAt(_stepEnd, out var support, out var supportNormal);
        if (!supported || Math.Abs(support.Y - _stepStart.Y) > .16f)
        {
            RecordGroundRefusal("step-start", _stepEnd, supported, support, supportNormal);
            return false;
        }
        _stepEnd.Y = support.Y;
        if (!BodyMotionClear(_stepEnd - _stepStart)) return false;
        _startYaw = _actor.GlobalRotation.Y;
        _endYaw = _startYaw + Mathf.AngleDifference(_startYaw, Mathf.Atan2(direction.X, direction.Z));
        // Replant both feet during the initial turn; ordinary strides keep one
        // boot planted. This uses the same instance-local foot modifier as Rinat.
        _turningStep = Math.Abs(_endYaw - _startYaw) > .12f;
        if (_turningStep) _endYaw = _startYaw + Math.Clamp(_endYaw - _startYaw, -.65f, .65f);
        _initialFeet = _feet.Select(foot => _feetModifier.AppliedWorldPose(foot.Ankle)).ToArray();
        _initialSoles = _initialFeet.Select((pose, i) => pose * _feet[i].SoleLocal).ToArray();
        _swing = _initialSoles[0].Dot(direction) <= _initialSoles[1].Dot(direction) ? 0 : 1;
        var turn = new Basis(Vector3.Up, _endYaw - _startYaw);
        _landingSoles = _initialSoles.Select(sole => _stepEnd + turn * (sole - _stepStart)).ToArray();
        _stepT = 0;
        _stepping = true;
        // VIS-046: the stride just chosen is the tempo the clip has to follow, and
        // the boot that will hold the body is sampled so its slide can be measured.
        SyncGaitCadence(_turningStep ? .42f : .28f);
        CapturePlantedSoleSamples();
        return true;
    }

    private void AdvanceStep(float delta)
    {
        var nextT = Math.Min(1, _stepT + delta / (_turningStep ? .42f : .28f));
        var t = Mathf.SmoothStep(0, 1, nextT);
        var at = _stepStart.Lerp(_stepEnd, t);
        if (!GroundAt(at, out var ground, out var groundNormal))
        {
            RecordGroundRefusal("step-center", at, false, ground, groundNormal);
            return;
        }
        at.Y = ground.Y;
        var motion = at - _actor.GlobalPosition;
        if (!BodyMotionClear(motion)) return;
        var solePositions = new Vector3[_feet.Count];
        var soleNormals = new Vector3[_feet.Count];
        var footFractions = new float[_feet.Count];
        for (var i = 0; i < _feet.Count; i++)
        {
            var footT = _turningStep
                ? Mathf.SmoothStep(0, 1, Math.Clamp(t * 2 - (i == _swing ? 0 : 1), 0, 1))
                : i == _swing ? t : 0;
            var sole = _turningStep ? _initialSoles[i].Lerp(_landingSoles[i], footT)
                : _initialSoles[i] + (i == _swing ? (_stepEnd - _stepStart) * (2 * t) : Vector3.Zero);
            if (!GroundAt(sole, out var footGround, out var normal))
            {
                RecordGroundRefusal("step-sole-" + i, sole, false, footGround, normal);
                return;
            }
            sole.Y = footGround.Y + Mathf.Sin(Mathf.Pi * footT) * .05f;
            solePositions[i] = sole;
            soleNormals[i] = normal;
            footFractions[i] = footT;
        }
        // Both feet and the full body are validated before displacement or
        // travelled-distance accounting can make this a completed step.
        _stepT = nextT;
        _actor.GlobalPosition = at;
        _actor.GlobalRotation = new(0, Mathf.Lerp(_startYaw, _endYaw, t), 0);
        var distance = new Vector2(motion.X, motion.Z).Length();
        _segmentTravel += distance;
        SessionWalkedMetres += distance;
        for (var i = 0; i < _feet.Count; i++)
        {
            var foot = _feet[i];
            var pose = _initialFeet[i];
            var footT = footFractions[i];
            var sole = solePositions[i];
            var normal = soleNormals[i];
            var facing = new Basis(Vector3.Up, (_endYaw - _startYaw) * footT) * _initialFeet[i].Basis.Orthonormalized();
            pose.Basis = new Basis(new Quaternion(_humanRig ? Vector3.Up : facing.Y.Normalized(), normal)) * facing;
            pose.Origin = sole - pose.Basis * foot.SoleLocal;
            var hip = _skeleton.GlobalTransform * foot.LegRest;
            var restLeg = (_skeleton.GlobalTransform * _skeleton.GetBoneGlobalRest(foot.Ankle)).Origin - hip.Origin;
            var leg = pose.Origin - hip.Origin;
            hip.Basis = new Basis(new Quaternion(restLeg.Normalized(), leg.Normalized())) * hip.Basis;
            hip.Basis = new Basis(hip.Basis.X, hip.Basis.Y * (leg.Length() / restLeg.Length()), hip.Basis.Z);
            _feetModifier.SetWorldPose(foot.Leg, hip);
            _feetModifier.SetWorldPose(foot.Ankle, pose);
        }
        // VIS-046: over the planted boot, in the middle of the stride, where the
        // body actually is when one leg carries it. A turning step puts both feet
        // down, so it has no single-support cue to invent.
        var planted = 1 - _swing;
        var towardsPlanted = solePositions[planted] - at;
        _weightTargetDirection = new Vector3(towardsPlanted.X, 0f, towardsPlanted.Z);
        var singleSupport = Mathf.Sin(Mathf.Pi * t);
        _weightTargetDrop = _turningStep ? 0f : WeightDropMetres * singleSupport;
        _weightTargetLateral = _turningStep ? 0f : WeightLateralMetres * singleSupport;
        ApplyWeightTransfer(delta);
        MeasureSupportSoleSlide();
        SetMeta("actualWalkedMetres", SessionWalkedMetres);
        if (_stepT < 1) return;
        _stepping = false;
        if (!_stepSound.Playing) _stepSound.Play();
        var point = Waypoints[_checkpoint + 1];
        if (new Vector2(at.X, at.Z).DistanceTo(point) < .002f) _pendingCheckpoint = _checkpoint + 1;
    }

    private void SynchronizeTarget()
    {
        // Keep the authored target under its logical zone so routing retains its
        // stable binding, while its live ray follows the actual visible body.
        var position = _actor.GlobalPosition + Vector3.Up * .84f;
        if (!_target.GlobalPosition.IsEqualApprox(position)) _target.GlobalPosition = position;
        // The ray shell is a square column round her: it follows where she
        // stands, not which way she faces, so turning to greet someone leaves
        // every interaction target where it was.
        _target.SetPresentationEnabled(PhysicalAccessReady && _presentationZone == "village_day");
        _target.CollisionMask = 0;
    }

    private bool BodyMotionClear(Vector3 motion, bool recoveryAsCollision = false)
    {
        using var collision = new KinematicCollision3D();
        if (!_body.TestMove(_body.GlobalTransform, motion, collision, .001f, recoveryAsCollision, 4)) return true;
        var clear = Enumerable.Range(0, collision.GetCollisionCount()).All(i => collision.GetNormal(i).Y > .85f);
        if (!clear) SetMeta("walkBlockedOwner", string.Join(" | ", Enumerable.Range(0, collision.GetCollisionCount())
            .Where(i => collision.GetNormal(i).Y <= .85f).Select(i => (collision.GetCollider(i) as Node)?.GetPath().ToString() ?? "unknown-body")));
        var probeKey = motion.IsZeroApprox() ? "projectionCollisionProbeRecorded" : "sweepCollisionProbeRecorded";
        if (!clear && !HasMeta(probeKey))
        {
            SetMeta(probeKey, true);
            var capsule = _body.GetChildren().OfType<CollisionShape3D>().Single();
            GD.Print("alsu-physical-contact: " + JsonSerializer.Serialize(new
            {
                query = motion.IsZeroApprox() ? "projection" : "sweep", motion = motion.ToString(), recoveryAsCollision,
                actorTransform = _actor.GlobalTransform.ToString(), bodyTransform = _body.GlobalTransform.ToString(),
                bodyPhysicsTransform = PhysicsServer3D.BodyGetState(_body.GetRid(), PhysicsServer3D.BodyState.Transform).AsTransform3D().ToString(),
                capsuleTransform = capsule.GlobalTransform.ToString(), capsuleLocal = capsule.Transform.ToString(),
                radius = ((CapsuleShape3D)capsule.Shape).Radius, height = ((CapsuleShape3D)capsule.Shape).Height,
                contacts = Enumerable.Range(0, collision.GetCollisionCount()).Select(i => new
                {
                    owner = (collision.GetCollider(i) as Node)?.GetPath().ToString(),
                    colliderTransform = (collision.GetCollider(i) as Node3D)?.GlobalTransform.ToString(),
                    colliderPhysicsTransform = collision.GetCollider(i) is PhysicsBody3D body
                        ? PhysicsServer3D.BodyGetState(body.GetRid(), PhysicsServer3D.BodyState.Transform).AsTransform3D().ToString() : null,
                    shape = (collision.GetColliderShape(i) as Node)?.GetPath().ToString(),
                    shapeTransform = (collision.GetColliderShape(i) as Node3D)?.GlobalTransform.ToString(),
                    shapeSize = (collision.GetColliderShape(i) as CollisionShape3D)?.Shape is BoxShape3D box ? box.Size.ToString() : null,
                    point = collision.GetPosition(i).ToString(), normal = collision.GetNormal(i).ToString()
                }).ToArray()
            }));
        }
        return clear;
    }

    private PhysicsRayQueryParameters3D GroundRay() => _groundRay ??=
        PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.Zero, 3, _groundExcludes);

    private bool GroundAt(Vector3 at, out Vector3 point, out Vector3 normal)
    {
        var ray = GroundRay();
        ray.From = at + Vector3.Up * .6f;
        ray.To = at - Vector3.Up * .7f;
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        point = hit.Count > 0 ? hit["position"].AsVector3() : at;
        normal = hit.Count > 0 ? hit["normal"].AsVector3() : Vector3.Up;
        return hit.Count > 0 && normal.Y > .85f;
    }

    private void RecordGroundRefusal(string stage, Vector3 at, bool supported, Vector3 support, Vector3 normal)
    {
        SetMeta("walkBlockedOwner", stage + " support=" + supported + " normal=" + normal
            + " heightDelta=" + (support.Y - _actor.GlobalPosition.Y));
        var key = "groundRefusalRecorded-" + stage;
        if (HasMeta(key)) return;
        SetMeta(key, true);
        var ray = GroundRay();
        ray.From = at + Vector3.Up * .6f;
        ray.To = at - Vector3.Up * .7f;
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        var collider = hit.Count > 0 ? hit["collider"].AsGodotObject() as Node3D : null;
        GD.Print("alsu-ground-refusal: " + JsonSerializer.Serialize(new
        {
            stage, at = at.ToString(), supported, support = support.ToString(), normal = normal.ToString(),
            actor = _actor.GlobalPosition.ToString(), heightDelta = support.Y - _actor.GlobalPosition.Y,
            owner = collider?.GetPath().ToString(), ownerTransform = collider?.GlobalTransform.ToString(),
            ownerPhysicsTransform = collider is PhysicsBody3D body
                ? PhysicsServer3D.BodyGetState(body.GetRid(), PhysicsServer3D.BodyState.Transform).AsTransform3D().ToString() : null,
            hit = hit.Count > 0, checkpoint = _checkpoint, pending = _pendingCheckpoint,
            requested = _requested, stepping = _stepping
        }));
    }

    private void PrepareFeet()
    {
        // The kit instance carries every person's rig; hers is the one her
        // visible boots are skinned to.
        var visibleBoot = _actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .First(mesh => mesh.Visible && mesh.Name.ToString().EndsWith("_BootLeft_LOD0", StringComparison.Ordinal));
        if (visibleBoot.GetNode<Skeleton3D>(visibleBoot.Skeleton) is { } rig && rig.FindBone("thigh_l") >= 0)
        {
            PrepareHumanFeet(rig);
            return;
        }
        foreach (var side in new[] { "Left", "Right" })
        {
            var boots = _actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                .Where(mesh => mesh.Visible && mesh.Name.ToString().Contains("_Boot" + side + "_LOD", StringComparison.Ordinal)).ToArray();
            var boot = boots.Single(mesh => mesh.Name.ToString().Contains("_LOD0", StringComparison.Ordinal));
            _skeleton ??= boot.GetNode<Skeleton3D>(boot.Skeleton);
            var leg = _skeleton.FindBone(side == "Left" ? "Leg.L" : "Leg.R");
            var rest = _skeleton.GetBoneGlobalRest(leg);
            var skin = boot.GetSkinReference()?.GetSkin() ?? boot.Skin;
            var bind = FindBind(skin, leg);
            var modelToSkeleton = rest * skin.GetBindPose(bind);
            var vertices = Enumerable.Range(0, boot.Mesh.GetSurfaceCount()).SelectMany(surface =>
                SurfaceVertices(boot.Mesh, surface))
                .Select(vertex => modelToSkeleton * vertex).ToArray();
            var bottom = vertices.Min(vertex => vertex.Y);
            var corners = vertices.Where(vertex => vertex.Y <= bottom + .0002f).Distinct().ToArray();
            var sole = corners.Aggregate(Vector3.Zero, (sum, point) => sum + point) / corners.Length;
            var ankleRest = new Transform3D(Basis.Identity, sole + Vector3.Up * .095f);
            var name = "AlsuWalkFoot" + side;
            _skeleton.AddBone(name);
            var ankle = _skeleton.FindBone(name);
            _skeleton.SetBoneParent(ankle, leg);
            _skeleton.SetBoneRest(ankle, rest.AffineInverse() * ankleRest);
            _skeleton.ResetBonePose(ankle);
            foreach (var mesh in boots)
            {
                var previous = mesh.GetSkinReference()?.GetSkin() ?? mesh.Skin;
                var replacement = (Skin)previous.Duplicate();
                var footBind = FindBind(replacement, leg);
                replacement.SetBindPose(footBind, ankleRest.AffineInverse() * rest * previous.GetBindPose(footBind));
                replacement.SetBindName(footBind, name);
                replacement.SetBindBone(footBind, ankle);
                mesh.Skin = replacement;
            }
            _feet.Add(new Foot(leg, ankle, rest, ankleRest.AffineInverse() * sole));
        }
        _feetModifier = new RinatFootPlacementModifier { Name = "AlsuSupportedFeet", Active = false, Influence = 1 };
        _skeleton.AddChild(_feetModifier);
        SetMeta("walkRig", "instance-local boot binds; original meshes; supported alternating feet");
    }

    // The human kit has a real leg: thigh, calf and foot. The foot bone itself
    // is placed at each footfall and the knee bends to reach it; boots keep
    // their authored skin. Walking plays the Walk clip for arms and body.
    private void PrepareHumanFeet(Skeleton3D skeleton)
    {
        _skeleton = skeleton;
        _humanRig = true;
        _feetModifier = new RinatFootPlacementModifier { Name = "AlsuSupportedFeet", Active = false, Influence = 1 };
        _skeleton.AddChild(_feetModifier);
        foreach (var (side, suffix) in new[] { ("Left", "l"), ("Right", "r") })
        {
            var thigh = _skeleton.FindBone("thigh_" + suffix);
            var calf = _skeleton.FindBone("calf_" + suffix);
            var foot = _skeleton.FindBone("foot_" + suffix);
            if (thigh < 0 || calf < 0 || foot < 0) throw new InvalidOperationException("Alsu's human rig lacks a leg chain.");
            var boot = _actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                .Single(mesh => mesh.Visible && mesh.Name.ToString().EndsWith("_Boot" + side + "_LOD0", StringComparison.Ordinal));
            // Exactly as the renderer skins it: skeleton pose * bind * vertex,
            // expressed in the foot bone's frame.
            var skin = boot.GetSkinReference()?.GetSkin() ?? boot.Skin;
            var bindToFoot = skin.GetBindPose(FindBind(skin, foot));
            var vertices = Enumerable.Range(0, boot.Mesh.GetSurfaceCount()).SelectMany(surface =>
                SurfaceVertices(boot.Mesh, surface))
                .Select(vertex => bindToFoot * vertex).ToArray();
            var footRest = _skeleton.GetBoneGlobalRest(foot);
            var bottom = vertices.Min(vertex => (footRest * vertex).Y);
            var corners = vertices.Where(vertex => (footRest * vertex).Y <= bottom + .004f).ToArray();
            var soleInFoot = corners.Aggregate(Vector3.Zero, (sum, point) => sum + point) / corners.Length;
            _feetModifier.RegisterLegChain(thigh, calf, foot);
            _feet.Add(new Foot(thigh, foot, _skeleton.GetBoneGlobalRest(thigh), soleInFoot));
        }
        SetMeta("walkRig", "human kit: thigh/calf/foot two-bone reach; authored boots; supported alternating feet");
    }

    // Standing on a sloped street, the clip's level stance leaves one boot in
    // the air. While she is not stepping, each foot keeps the clip's pose but
    // drops or rises onto the snow under it, and the knee takes the difference.
    private void GroundStandingFeet()
    {
        if (!_humanRig || _stepping || !_actor.IsVisibleInTree()) return;
        for (var i = 0; i < _feet.Count; i++)
        {
            var foot = _feet[i];
            var pose = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(foot.Ankle);
            var sole = pose * foot.SoleLocal;
            if (!GroundAt(sole, out var ground, out _)) continue;
            var lift = ground.Y - sole.Y;
            if (Math.Abs(lift) > .12f) continue;
            pose.Origin += Vector3.Up * lift;
            _feetModifier.SetWorldPose(foot.Ankle, pose);
        }
    }

    private void PlayGait(bool walking)
    {
        if (!_humanRig || _walkingClip == walking) return;
        _walkingClip = walking;
        GeneratedCharacterKitDressing.PlayClip(_actor, walking ? "Walk" : "Idle");
        if (walking) return;
        // VIS-046: standing is not a slowed-down walk. The clip returns to its
        // authored tempo and the weight cue decays through EaseWeightTransfer, so
        // neither the start nor the stop changes her body height in one jump.
        AnimationCatalog.ResetMotionScale(_actor);
        _motionScale = 1f;
        _gaitCycleSeconds = 0f;
        _weightTargetDrop = 0f;
        _weightTargetLateral = 0f;
    }

    /// <summary>
    /// VIS-046: her boots are placed by this owner, so the clip only carries arms,
    /// spine and head. Left at the library tempo it steps to a rhythm that has
    /// nothing to do with her real footfalls, which is the mannequin tell the card
    /// is about. One walk loop holds a full two-foot cycle.
    /// </summary>
    private void SyncGaitCadence(float footfallSeconds)
    {
        if (!_humanRig || footfallSeconds <= .001f) return;
        if (_gaitCycleSeconds <= 0f
            && !AnimationCatalog.TrySetMotionScale(_actor, 1, out _, out _gaitCycleSeconds)) return;
        if (_gaitCycleSeconds <= 0f) return;
        var scale = _gaitCycleSeconds * .5f / footfallSeconds;
        if (AnimationCatalog.TrySetMotionScale(_actor, scale, out var applied, out _)) _motionScale = applied;
        SetMeta("alsuWalkCadenceScale", _motionScale);
        SetMeta("alsuWalkFootfallSeconds", footfallSeconds);
        SetMeta("alsuWalkClipCycleSeconds", _gaitCycleSeconds);
    }

    private void EaseWeightTransfer(float delta)
    {
        if (!_humanRig || _stepping || delta <= 0f) return;
        // While stepping AdvanceStep owns the cue; here it relaxes to nothing.
        _weightTargetDrop = 0f;
        _weightTargetLateral = 0f;
        ApplyWeightTransfer(delta);
    }

    private void ApplyWeightTransfer(float delta)
    {
        if (_feetModifier is null || !IsInstanceValid(_feetModifier)) return;
        var rate = delta * WeightEasePerSecond;
        _weightDrop = Mathf.MoveToward(_weightDrop, _weightTargetDrop, rate);
        _weightLateral = Mathf.MoveToward(_weightLateral, _weightTargetLateral, rate);
        if (_weightTargetLateral > 0f && _weightTargetDirection.LengthSquared() > .000001f)
            _weightDirection = _weightTargetDirection;
        _feetModifier.SetWeightShift(_weightDrop, _weightDirection, _weightLateral);
        if (Engine.GetPhysicsFrames() % 10 != 0) return;
        SetMeta("walkWeightDropMetres", _weightDrop);
        SetMeta("walkWeightLateralMetres", _weightLateral);
    }

    private void CapturePlantedSoleSamples()
    {
        if (_feetModifier is null || !IsInstanceValid(_feetModifier)) return;
        _plantedSoleSamples = _feet
            .Select(foot => _feetModifier.AppliedWorldPose(foot.Ankle) * foot.SoleLocal)
            .ToArray();
    }

    /// <summary>
    /// VIS-046 asks for a number, not an impression: how far the boot that must be
    /// glued to the snow actually travelled between two applied poses. Read from the
    /// modifier's last applied pose — what the renderer really skinned with.
    /// </summary>
    private void MeasureSupportSoleSlide()
    {
        if (_feetModifier is null || !IsInstanceValid(_feetModifier)
            || _plantedSoleSamples.Length != _feet.Count) return;
        var planted = 1 - _swing;
        var now = _feetModifier.AppliedWorldPose(_feet[planted].Ankle) * _feet[planted].SoleLocal;
        var slide = now.DistanceTo(_plantedSoleSamples[planted]);
        if (slide > _maxSupportSoleSlide) _maxSupportSoleSlide = slide;
        _plantedSoleSamples[planted] = now;
        SetMeta("alsuMaxSupportSoleSlideMetres", _maxSupportSoleSlide);
        SetMeta("alsuSupportSoleSlideBudgetMetres", SupportSoleSlideBudgetMetres);
    }

    private int FindBind(Skin skin, int bone)
    {
        for (var i = 0; i < skin.GetBindCount(); i++)
        {
            var name = skin.GetBindName(i).ToString();
            if ((name.Length > 0 ? _skeleton.FindBone(name) : skin.GetBindBone(i)) == bone) return i;
        }
        throw new InvalidOperationException("Alsu boot skin does not bind its actual visible leg.");
    }

    // A released stance is also a released weight cue: the modifier forgets the feet
    // and the shift together, and this owner drops its own easing values with it, so
    // a load or a zone change cannot leave her pelvis leaning at last frame's angle.
    private void ReleaseSupportedFeet()
    {
        if (_feetModifier is not null && IsInstanceValid(_feetModifier)) _feetModifier.ClearPoses();
        _weightDrop = 0f;
        _weightLateral = 0f;
        _weightTargetDrop = 0f;
        _weightTargetLateral = 0f;
        _weightDirection = Vector3.Zero;
        _weightTargetDirection = Vector3.Zero;
        _plantedSoleSamples = [];
    }

    public override void _ExitTree()
    {
        if (_bridge is not null && IsInstanceValid(_bridge)) _bridge.RuntimeStateChanged -= ReadRuntimeState;
        ReleaseSupportedFeet();
        if (_stepSound is not null && IsInstanceValid(_stepSound)) { _stepSound.Stop(); _stepSound.Stream = null; }
        // The one retained ray query is a C#-owned RefCounted; release it last, after
        // every probe in this node has stopped, exactly like FirstPersonController.Steps.
        _groundRay?.Dispose();
        _groundRay = null;
    }
}
