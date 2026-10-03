using Godot;

namespace Urman.Godot;

/// <summary>
/// One visible Rinat and his ordinary lamp, shared by connected and compact
/// scenes. RuntimeBridge owns every narrative condition and committed effect.
/// This adapter only supplies a live view predicate and a completed physical
/// stop gesture; creating it never confirms the roadside observation.
/// </summary>
public partial class RinatPresencePresentation : Node3D
{
    public const string ObservationId = "urman.chapter1:interaction/observe-rinat-roadside";
    private const string PresenceGroup = "rinat_presence_presentation";
    private Node3D _actor = null!;
    private Node3D _lamp = null!;
    private OmniLight3D _light = null!;
    private AnimatableBody3D _body = null!;
    private AudioStreamPlayer3D _stepSound = null!;
    private bool _pendingLandingStep;
    private bool _landingStartQueued;
    private PauseMenuUi? _pauseMenu;
    private InteractionTarget? _observation;
    private RuntimeBridge? _bridge;
    private Skeleton3D _skeleton = null!;
    private RinatFootPlacementModifier? _footModifier;
    private int _handBone;
    private Vector3 _handLocal;
    private readonly List<(MeshInstance3D Mesh, int Bone, Vector3[] Points)> _stopHandLandmarks = new();
    private bool _compact;
    // The compact presence predicate reads only committed kernel state, and every
    // committed kernel mutation raises RuntimeBridge.RuntimeStateChanged (all
    // DispatchAsync paths call QueueRuntimeStateChanged). So the snapshot is
    // re-read on that event, on the first frame after the bridge is resolved and
    // on a session change, instead of once per frame.
    private bool _presenceDirty = true;
    // The bridge whose RuntimeStateChanged this instance is currently subscribed to.
    private RuntimeBridge? _presenceSource;
    private string _stage = string.Empty;
    private object? _session;
    private Task<bool>? _intervention;
    private object? _interventionSession;
    private TaskCompletionSource<bool>? _interventionCompletion;
    private long _interventionRequest;
    private bool _stepCompleted;
    private bool _rayExclusionsResolved;
    private readonly List<Foot> _feet = new();
    private readonly global::Godot.Collections.Array<Rid> _rayExcludes = new();

    private sealed record Foot(int Leg, int Ankle, Transform3D LegRest,
        Vector3 SoleLocal, Vector3[] SoleCorners);

    public static RinatPresencePresentation? Current(SceneTree tree) =>
        tree.GetNodesInGroup(PresenceGroup).OfType<RinatPresencePresentation>()
            .FirstOrDefault(node => IsInstanceValid(node) && node.IsInsideTree()
                && !node.IsQueuedForDeletion() && !node.GetParent().IsQueuedForDeletion());

    internal static RinatPresencePresentation AttachConnected(Node3D host, Node3D actor,
        StyleBenchmarkZone zirat, Vector3 roadsideAnchor, Vector3 referenceEye)
    {
        var result = Attach(host, actor);
        result.BuildObservation(zirat, roadsideAnchor, referenceEye);
        return result;
    }

    public static void AttachCompact(Node3D zone, string zoneId)
    {
        if (zoneId is not ("zirat_road" or "kara_urman_night")) return;
        var actor = zone.FindChild("Npc_rinat", true, false) as Node3D;
        if (actor is null)
        {
            actor = GeneratedCharacterKitDressing.Attach(zone, "rinat", "Rinat", Vector3.Zero);
            actor.Name = "Npc_rinat";
        }
        actor.GlobalPosition = zone.ToGlobal(zoneId == "zirat_road"
            ? new Vector3(2f, .01f, -14.5f) : new Vector3(2.1f, .01f, -7.4f));
        actor.RotationDegrees = new(0, -30f, 0);
        var result = Attach(zone, actor);
        result._compact = true;
        result.ApplyStage(zoneId == "zirat_road" ? "roadside" : "forest");
        if (zone is StyleBenchmarkZone zirat && zoneId == "zirat_road")
            result.BuildObservation(zirat, actor.GlobalPosition, zone.ToGlobal(new(0, 1.67f, -12.5f)));
        if (zone is StyleBenchmarkZone forest && zoneId == "kara_urman_night")
        {
            var endpoint = forest.MakeInteractionBox("KaraForestApproachEndpoint", new(1.80f, 1.50f, .55f),
                new(.6f, 1.25f, -7.5f), "39453b", "urman.chapter1:interaction/forest-approach-to-forest",
                "Идти дальше к кромке леса", rayOnly: true);
            endpoint.SetMeta("observationReferenceEye", zone.ToGlobal(new(.6f, 1.7f, -6f)));
            endpoint.SetMeta("observationLookAt", endpoint.GlobalPosition);
            endpoint.SetMeta("routeRole", "existing Kara road endpoint; scene transition without a zone teleport");
        }
    }

    private static RinatPresencePresentation Attach(Node3D host, Node3D actor)
    {
        var result = new RinatPresencePresentation { Name = "RinatPresencePresentation", _actor = actor };
        host.AddChild(result);
        result.AddToGroup(PresenceGroup);
        result.SetMeta("runtimeStateOwner", "RuntimeBridge; view and motion only");
        result.BuildLampAndContact();
        return result;
    }

    private bool _humanRig;

    private void BuildLampAndContact()
    {
        var visibleBoot = _actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .First(mesh => mesh.Visible && mesh.Name.ToString().EndsWith("_BootLeft_LOD0", StringComparison.Ordinal));
        if (visibleBoot.GetNode<Skeleton3D>(visibleBoot.Skeleton) is { } rig && rig.FindBone("thigh_l") >= 0)
        {
            // Human kit: the kit instance carries every person's rig; his is
            // the one his boots are skinned to. The lamp hangs from the left
            // hand's grip, the stop gesture is the right hand's palm.
            _humanRig = true;
            _skeleton = rig;
            _handBone = _skeleton.FindBone("hand_l");
            _handLocal = new Vector3(0f, .085f, 0f);
            var body = _actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                .First(mesh => mesh.Visible && mesh.Name.ToString().EndsWith("_Body_LOD0", StringComparison.Ordinal));
            var palm = _skeleton.FindBone("hand_r");
            _stopHandLandmarks.Add((body, palm, [Vector3.Zero, new(0f, .09f, 0f)]));
            _stopHandLandmarks.Add((body, palm, [new(.035f, .06f, 0f), new(-.035f, .06f, 0f)]));
        }
        else BuildFirstKitHand();
        BuildLampProp();
    }

    private void BuildFirstKitHand()
    {
        var hand = _actor.FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().Single(mesh => mesh.Visible
                && mesh.Name.ToString() == _actor.GetMeta("characterPrefix").AsString() + "_HeadHandLeft_LOD0");
        _skeleton = hand.GetNode<Skeleton3D>(hand.Skeleton);
        var skin = hand.GetSkinReference()?.GetSkin() ?? hand.Skin
            ?? throw new InvalidOperationException("Rinat's visible hand has no live skin.");
        _handBone = _skeleton.FindBone("Arm.L");
        var bind = FindBind(skin, _skeleton, _handBone);
        _handLocal = skin.GetBindPose(bind) * hand.Mesh.GetAabb().GetCenter();
        CacheStopHandLandmarks();
    }

    private void BuildLampProp()
    {

        // Reuse the actual lantern geometry/materials. Only its visual children
        // move to this prop: no CarryableProp, custody ID or second item enters
        // the tree or the player's save/inventory.
        var source = CarryableProp.Create("rinat-lamp-visual-source", "", CarryableProp.ItemClass.Light,
            Vector3.Zero, 0f, "676b59", "metal", CarryableProp.ItemKind.Lantern);
        _lamp = new Node3D { Name = "RinatHandLamp" };
        _actor.AddChild(_lamp);
        foreach (var child in source.GetChildren().ToArray())
        {
            if (child is not (MeshInstance3D or Light3D)) continue;
            source.RemoveChild(child);
            _lamp.AddChild(child);
            ((Node3D)child).Visible = true;
        }
        source.Free();
        _lamp.SetMeta("assetSource", "CarryableProp.Geometry.cs/Lantern; visual children only");
        _lamp.SetMeta("interactionOwnership", "none; Rinat's lamp is not a spare player item");
        _light = _lamp.GetNode<OmniLight3D>("PortableLight");
        _light.LightEnergy = 1.15f;
        _light.OmniRange = 4.8f;

        _body = new AnimatableBody3D { Name = "RinatPhysicalContact", CollisionLayer = 0u,
            CollisionMask = 1u, SyncToPhysics = false };
        _body.SetMeta("collisionOwner", "RinatPresencePresentation/visible exterior actor");
        _body.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = .26f, Height = 1.70f },
            Position = Vector3.Up * .85f });
        _actor.AddChild(_body);
        _stepSound = new AudioStreamPlayer3D { Name = "RinatLandingStep", Bus = AudioSettingsService.SfxBus,
            Stream = ResourceLoader.Load<AudioStream>("res://assets/audio/act1/footsteps/step_snow_packed_01.wav"),
            VolumeDb = -15f, UnitSize = 2f, MaxDistance = 12f };
        _actor.AddChild(_stepSound);
        _stepSound.Finished += OnLandingStepFinished;
        _rayExcludes.Add(_body.GetRid());
        UpdateLamp();
        _lamp.Visible = false;
    }

    private void BuildObservation(StyleBenchmarkZone zone, Vector3 actorAt, Vector3 referenceEye)
    {
        _observation = zone.MakeInteractionBox("RinatRoadsideObservation", new(.70f, 1.7f, .65f),
            zone.ToLocal(actorAt + Vector3.Up * .86f), "59634f", ObservationId,
            "Рассмотреть свет у дороги", rayOnly: true);
        _observation.SetMeta("observationReferenceEye", referenceEye);
        _observation.SetMeta("observationLookAt", actorAt + Vector3.Up * 1.15f);
        _observation.SetMeta("observationLabelId", "urman.chapter1:text/observe-rinat-roadside");
        _observation.SetMeta("observationRequiresLiveView", true);
        _observation.SetMeta("observationBlockedHint", "Подойди со стороны дороги и рассмотри человека с фонарём.");
    }

    internal void ApplyStage(string stage)
    {
        if (_stage == stage) return;
        _stage = stage;
        ResetStep();
        _lamp.Visible = stage is "roadside" or "forest";
        _body.CollisionLayer = _lamp.Visible ? 1u : 0u;
        _actor.SetMeta("rinatStage", stage);
    }

    public override void _Process(double delta)
    {
        if (_bridge is null) _bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (_bridge is { } source && !ReferenceEquals(source, _presenceSource))
        {
            // Subscribing here (and dropping the previous source) keeps the handler
            // attached after a re-entry into the tree, and the first pass after the
            // subscription reads every commit that predates it.
            if (_presenceSource is not null && IsInstanceValid(_presenceSource))
                _presenceSource.RuntimeStateChanged -= MarkPresenceDirty;
            _presenceSource = source;
            source.RuntimeStateChanged += MarkPresenceDirty;
            _presenceDirty = true;
        }
        if (_bridge?.SessionIdentity is { } session && !ReferenceEquals(_session, session))
        {
            _session = session;
            ResetStep();
            _presenceDirty = true;
        }
        if (_compact && _bridge?.ActiveSceneId is not null && _presenceDirty)
        {
            _presenceDirty = false;
            var state = _bridge.SelectRuntimeState();
            var present = state.TryGetProperty("npc", out var people)
                && people.TryGetProperty("urman.chapter1:character/rinat", out var rinat)
                && rinat.TryGetProperty("alerted", out var alerted) && alerted.ValueKind == System.Text.Json.JsonValueKind.True
                && state.TryGetProperty("knowledge", out var knowledge)
                && knowledge.TryGetProperty("urman.chapter1:knowledge/route_kara_urman_edge_hint", out var route)
                && route.GetProperty("status").GetString() == "confirmed";
            // Comparing against the live property keeps the previous per-frame
            // re-assertion while skipping the write when it already holds the value;
            // no other code writes these two properties.
            if (_actor.Visible != present) _actor.Visible = present;
            var collisionLayer = present ? 1u : 0u;
            if (_body.CollisionLayer != collisionLayer) _body.CollisionLayer = collisionLayer;
        }
        UpdateLamp();
        ResolvePauseMenu();
    }

    private void MarkPresenceDirty() => _presenceDirty = true;

    // The retained player belongs to this actor. world_foley is deliberately
    // unsuitable: its lifecycle reset destroys every member of that group.
    // The same small replay entrypoint is used by the actual foot landing and
    // the audio diagnostic; it neither moves the actor nor changes progress.
    internal void RequestLandingStep()
    {
        if (DisplayServer.GetName() == "headless") return;
        ResolvePauseMenu();
        _pendingLandingStep = true;
        UpdateLandingStepAudioPause(_pauseMenu?.IsOpen == true);
    }

    private void ResolvePauseMenu()
    {
        var menu = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi;
        if (ReferenceEquals(menu, _pauseMenu)) return;
        if (_pauseMenu is not null && IsInstanceValid(_pauseMenu))
            _pauseMenu.PauseChanged -= UpdateLandingStepAudioPause;
        _pauseMenu = menu;
        if (_pauseMenu is null) return;
        _pauseMenu.PauseChanged += UpdateLandingStepAudioPause;
        UpdateLandingStepAudioPause(_pauseMenu.IsOpen);
    }

    private void UpdateLandingStepAudioPause(bool paused)
    {
        if (_stepSound is null || !IsInstanceValid(_stepSound)) return;
        // A retained AudioStreamPlayer3D can still expose its previous playback
        // object after Stop. A positive cursor proves that this new request
        // actually started; HasStreamPlayback does not establish that boundary.
        if (_landingStartQueued && (!_stepSound.Playing || _stepSound.GetPlaybackPosition() > 0f))
            _landingStartQueued = false;
        if (paused && _landingStartQueued)
        {
            // PauseMenuUi publishes synchronously before the next native tick.
            // Cancel the queued Play now and keep precisely one landing for
            // Resume. Waiting for _Process leaked the first audio buffer.
            _stepSound.Stop();
            _landingStartQueued = false;
            _pendingLandingStep = true;
        }
        _stepSound.StreamPaused = paused;
        if (paused || !_pendingLandingStep) return;
        _pendingLandingStep = false;
        _landingStartQueued = true;
        _stepSound.Play();
    }

    private void OnLandingStepFinished()
    {
        // A completed landing has no request left to defer. Otherwise a much
        // later pause sees the reset cursor and incorrectly replays an old step.
        _landingStartQueued = false;
    }

    private void UpdateLamp()
    {
        if (_lamp is null || !IsInstanceValid(_actor) || !IsInstanceValid(_skeleton)) return;
        var hand = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_handBone) * _handLocal;
        // Gravity keeps the hanging body vertical while its grip follows the
        // live skinned glove during the authored idle and stop animation.
        _lamp.GlobalTransform = new Transform3D(_actor.GlobalBasis.Orthonormalized(), hand - Vector3.Up * .381f);
    }

    public bool CanObserveRoadside()
    {
        _bridge ??= GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var camera = PlayerCamera();
        if (_bridge?.CurrentZoneId != "zirat_road" || _stage != "roadside" || camera is null
            || !_actor.IsVisibleInTree() || !_lamp.IsVisibleInTree() || !_light.IsVisibleInTree()) return false;
        var chest = _actor.GlobalPosition + Vector3.Up * 1.15f;
        var direction = chest - camera.GlobalPosition;
        return direction.Length() is > .5f and < 3.7f
            && (-camera.GlobalBasis.Z).Normalized().Dot(direction.Normalized()) > .89f
            && VisiblePoint(camera, _actor.GlobalPosition + Vector3.Up * 1.62f)
            && VisiblePoint(camera, _light.GlobalPosition);
    }

    private void CacheStopHandLandmarks()
    {
        var prefix = _actor.GetMeta("characterPrefix").AsString();
        foreach (var part in new[] { "HeadHandRight", "HeadHandThumbRight" })
        {
            var mesh = _actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                .Single(item => item.Visible && item.Name.ToString() == $"{prefix}_{part}_LOD0");
            var skin = mesh.GetSkinReference()?.GetSkin() ?? mesh.Skin
                ?? throw new InvalidOperationException("Rinat's stop hand has no rigid skin.");
            var bone = _skeleton.FindBone("Hand.R");
            if (bone < 0) throw new InvalidOperationException("Rinat's stop hand has no authored Hand.R bone.");
            var inverseBind = skin.GetBindPose(FindBind(skin, _skeleton, bone));
            // Both meshes are rigidly bound to the existing wrist. Cache their
            // local bounds once; multiplying by the current bone pose follows
            // the same skin transform as the renderer without baking per frame.
            var bounds = mesh.Mesh.GetAabb();
            var points = Enumerable.Range(0, 8).Select(index => inverseBind * bounds.GetEndpoint(index))
                .Prepend(inverseBind * bounds.GetCenter()).ToArray();
            _stopHandLandmarks.Add((mesh, bone, points));
        }
    }

    /// <summary>Live visual proof; this query never advances narrative state.</summary>
    public bool CanSeeInterventionGesture()
    {
        var camera = PlayerCamera();
        if (camera is null || _stage != "forest" || !_actor.IsVisibleInTree()
            || !_lamp.IsVisibleInTree() || _stopHandLandmarks.Count != 2
            || !FacesObserver(camera.GlobalPosition)
            || !VisiblePoint(camera, _actor.GlobalPosition + Vector3.Up * 1.55f)
            || !VisiblePoint(camera, _light.GlobalPosition)) return false;
        foreach (var landmark in _stopHandLandmarks)
        {
            if (!landmark.Mesh.IsVisibleInTree()) return false;
            // RinatFootPlacementModifier changes only the legs and ankles.
            // The wrist/spine animation pose is unmodified by that modifier.
            var pose = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(landmark.Bone);
            foreach (var point in landmark.Points)
                if (!VisiblePoint(camera, pose * point)) return false;
        }
        return true;
    }

    private bool FacesObserver(Vector3 observer)
    {
        var towardsObserver = observer - _actor.GlobalPosition;
        towardsObserver.Y = 0f;
        var forward = _actor.GlobalBasis.Z;
        forward.Y = 0f;
        // Generated characters face +Z (the same convention as the verified
        // conversation turns and AnimateStep's atan2 heading). Own-body ray
        // exclusion must not make the gesture readable through his back.
        return towardsObserver.LengthSquared() > .01f
            && forward.Normalized().Dot(towardsObserver.Normalized()) > .35f;
    }

    private Camera3D? PlayerCamera() => (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)
        ?.GetNodeOrNull<Camera3D>("Head/Camera3D");

    private bool VisiblePoint(Camera3D camera, Vector3 point)
    {
        if (!camera.IsPositionInFrustum(point)) return false;
        ResolveRayExclusions();
        using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, point, 3u, _rayExcludes);
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        return hit.Count == 0 || hit["position"].AsVector3().DistanceTo(point) < .10f;
    }

    private void ResolveRayExclusions()
    {
        if (_rayExclusionsResolved) return;
        _rayExclusionsResolved = true;
        // Resolve once after scene construction, not on every interaction ray.
        // C# script types are tested directly; no reliance on optional groups.
        foreach (var target in Descendants(GetTree().Root).OfType<InteractionTarget>())
            if (!_rayExcludes.Contains(target.GetRid())) _rayExcludes.Add(target.GetRid());
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            _rayExcludes.Add(player.GetRid());
    }

    private static IEnumerable<Node> Descendants(Node parent)
    {
        var count = parent.GetChildCount();
        for (var index = 0; index < count; index++)
        {
            var child = parent.GetChild(index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    public Task<bool> PresentRinatInterventionAsync(object sessionIdentity)
    {
        if (ReferenceEquals(_interventionSession, sessionIdentity) && _intervention is { IsCompleted: false })
            return _intervention;
        CancelIntervention();
        _session = sessionIdentity;
        _interventionSession = sessionIdentity;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _interventionCompletion = completion;
        _intervention = completion.Task;
        _ = CompleteIntervention(sessionIdentity, _interventionRequest, completion);
        return completion.Task;
    }

    private async Task CompleteIntervention(object sessionIdentity, long request, TaskCompletionSource<bool> completion)
    {
        try
        {
            completion.TrySetResult(await PerformIntervention(sessionIdentity, request));
        }
        catch (Exception exception)
        {
            if (request == _interventionRequest) completion.TrySetException(exception);
            else completion.TrySetResult(false);
        }
    }

    private async Task<bool> PerformIntervention(object sessionIdentity, long request)
    {
        _bridge ??= GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (!ValidIntervention(sessionIdentity, request)) return false;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null) return false;
        PrepareFeet();
        while (ValidIntervention(sessionIdentity, request))
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (!ValidIntervention(sessionIdentity, request)) return false;
            if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }) continue;
            var direction = player.GlobalPosition - _actor.GlobalPosition;
            direction.Y = 0f;
            if (direction.Length() > 3.1f)
            {
                // A player can explore beyond the entry spawn before starting
                // the finale. Rinat approaches with real planted/swinging feet
                // and a body sweep; a changed camera never causes a teleport.
                GeneratedCharacterKitDressing.PlayClip(_actor, "Idle");
                _stepCompleted = false;
                _actor.SetMeta("rinatInterventionPhase", "approaching-player");
                foreach (var yaw in new[] { 0f, .55f, -.55f, 1.05f, -1.05f })
                {
                    var heading = direction.Normalized().Rotated(Vector3.Up, yaw);
                    if (!CanTakeStep(heading, .30f)) continue;
                    await AnimateStep(sessionIdentity, request, heading, .30f, .48f);
                    break;
                }
                continue;
            }
            // Reorient only after a completed step if the observer has moved
            // behind him. The existing awaited step owns the whole turn and
            // planted/swinging feet; no new tween starts on every frame.
            if (_stepCompleted && !FacesObserver(player.GlobalPosition))
                _stepCompleted = false;
            if (!_stepCompleted)
            {
                // Leave space beside a close observer. Displacement is tangent
                // to their actual position; the torso still faces them when the
                // step lands, instead of making a quarter-turn on every retry.
                var closeObserver = direction.Length() <= 1.05f;
                var heading = closeObserver ? Vector3.Up.Cross(direction).Normalized() : direction.Normalized();
                if (!CanTakeStep(heading, .16f))
                {
                    heading = closeObserver ? -heading : direction.Normalized().Rotated(Vector3.Up, .8f);
                    if (!CanTakeStep(heading, .16f)) continue;
                }
                GeneratedCharacterKitDressing.PlayClip(_actor, "Tension");
                _actor.SetMeta("rinatInterventionPhase", "physical-step-and-stop-hand");
                if (!await AnimateStep(sessionIdentity, request, heading, .16f, 1.05f, player.GlobalPosition)) continue;
                _stepCompleted = true;
                _actor.SetMeta("rinatInterventionPhase", "step-landed-stop-hand");
            }
            // No frame-count timeout: looking away cannot permanently consume
            // the only cue-start event. The player may turn back or return.
            if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }) continue;
            if (CanSeeInterventionGesture())
            {
                _actor.SetMeta("rinatInterventionPhase", "visible-landed-stop-hand");
                return true;
            }
        }
        return false;
    }

    private bool CanTakeStep(Vector3 direction, float distance)
    {
        var destination = _actor.GlobalPosition + direction * distance;
        if (!GroundAt(destination, out var support, out _) || Math.Abs(support.Y - _actor.GlobalPosition.Y) > .16f)
            return false;
        destination.Y = support.Y;
        return BodyMotionClear(destination - _actor.GlobalPosition);
    }

    private bool BodyMotionClear(Vector3 motion)
    {
        using var collision = new KinematicCollision3D();
        if (!_body.TestMove(_body.GlobalTransform, motion, collision, .001f, false, 4)) return true;
        // A sloping floor is a support; walls, fence rails and the player are
        // genuine blockers. Never force the actor through a failed body sweep.
        return Enumerable.Range(0, collision.GetCollisionCount()).All(index => collision.GetNormal(index).Y > .85f);
    }

    private async Task<bool> AnimateStep(object sessionIdentity, long request, Vector3 direction, float distance,
        float duration, Vector3? observerAt = null)
    {
        var start = _actor.GlobalPosition;
        var startYaw = _actor.GlobalRotation.Y;
        var facing = observerAt.HasValue ? observerAt.Value - (start + direction * distance) : direction;
        facing.Y = 0f;
        var turnAngle = Mathf.AngleDifference(startYaw, Mathf.Atan2(facing.X, facing.Z));
        var sideStep = observerAt.HasValue && Mathf.Abs(facing.Normalized().Dot(direction)) < .5f;
        // A half-turn around feet left in the old world stance crossed the
        // legs. Replant in small supported turns while preserving the one
        // requested body displacement and the live pose between each landing.
        var parts = Math.Max(1, Mathf.CeilToInt(Mathf.Abs(turnAngle) / Mathf.DegToRad(45f)));
        for (var part = 0; part < parts; part++)
        {
            var targetYaw = startYaw + turnAngle * ((part + 1f) / parts);
            if (!await AnimateSupportedStep(sessionIdentity, request, direction, distance / parts,
                    Math.Max(duration / parts, .42f), targetYaw, sideStep)) return false;
        }
        _actor.SetMeta("rinatInterventionStepDistance", start.DistanceTo(_actor.GlobalPosition));
        return true;
    }

    private async Task<bool> AnimateSupportedStep(object sessionIdentity, long request, Vector3 direction,
        float distance, float duration, float targetYaw, bool sideStep)
    {
        var start = _actor.GlobalPosition;
        var initial = _feet.Select(foot => _footModifier!.AppliedWorldPose(foot.Ankle)).ToArray();
        var startYaw = _actor.GlobalRotation.Y;
        // Godot reads Euler Y back in its wrapped range between landings.
        // Keep this part on the same short arc when the turn crosses +/-PI.
        targetYaw = startYaw + Mathf.AngleDifference(startYaw, targetYaw);
        var replant = sideStep || Mathf.Abs(Mathf.AngleDifference(startYaw, targetYaw)) > .04f;
        var soles = initial.Select((pose, index) => pose * _feet[index].SoleLocal).ToArray();
        var turn = new Basis(Vector3.Up, targetYaw - startYaw);
        var sideLandings = soles.Select(sole => start + direction * distance + turn * (sole - start)).ToArray();
        var swing = initial[0].Origin.Dot(direction) <= initial[1].Origin.Dot(direction) ? 0 : 1;
        // Start with the foot whose path leaves more space beside the planted
        // boot. For a lateral step this is the ordinary leading foot.
        if (replant)
        {
            static float Clearance(Vector3 from, Vector3 to, Vector3 planted)
            {
                var path = new Vector2(to.X - from.X, to.Z - from.Z);
                var offset = new Vector2(planted.X - from.X, planted.Z - from.Z);
                var along = path.LengthSquared() > .000001f
                    ? Mathf.Clamp(offset.Dot(path) / path.LengthSquared(), 0f, 1f) : 0f;
                return (offset - path * along).LengthSquared();
            }
            swing = Clearance(soles[0], sideLandings[0], soles[1])
                >= Clearance(soles[1], sideLandings[1], soles[0]) ? 0 : 1;
        }
        var elapsed = 0f;
        while (elapsed < duration)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (!ValidIntervention(sessionIdentity, request)) return false;
            if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }) continue;
            elapsed = Math.Min(duration, elapsed + (float)GetPhysicsProcessDeltaTime());
            var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            var at = start + direction * (distance * t);
            if (!GroundAt(at, out var bodyGround, out _)) return StepRefused("body-ground", at);
            at.Y = bodyGround.Y;
            if (!BodyMotionClear(at - _actor.GlobalPosition)) return StepRefused("body-motion", at);
            _actor.GlobalPosition = at;
            _actor.GlobalRotation = new(0f, Mathf.Lerp(startYaw, targetYaw, t), 0f);
            for (var index = 0; index < _feet.Count; index++)
            {
                var foot = _feet[index];
                var pose = initial[index];
                var footT = replant
                    ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t * 2f - (index == swing ? 0f : 1f), 0f, 1f))
                    : index == swing ? t : 0f;
                var sole = replant ? soles[index].Lerp(sideLandings[index], footT)
                    : soles[index] + (index == swing ? direction * (distance * 2f * t) : Vector3.Zero);
                if (!GroundAt(sole, out var support, out var normal)) return StepRefused("sole-ground-" + index, sole);
                sole.Y = support.Y + Mathf.Sin(Mathf.Pi * footT) * .055f;
                var flatBasis = replant
                    ? initial[index].Basis.Orthonormalized().Slerp((turn * initial[index].Basis).Orthonormalized(), footT)
                    : index == swing
                    ? initial[index].Basis.Orthonormalized().Slerp(_skeleton.GlobalBasis.Orthonormalized(), t)
                    : initial[index].Basis;
                // The first kit's ankle bone points up; a human foot bone points
                // along the foot, so tilt it by the slope from world up instead.
                pose.Basis = new Basis(new Quaternion(_humanRig ? Vector3.Up : flatBasis.Y.Normalized(), normal)) * flatBasis;
                pose.Origin = sole - pose.Basis * foot.SoleLocal;
                var hip = _skeleton.GlobalTransform * foot.LegRest;
                var oldLeg = (_skeleton.GlobalTransform * _skeleton.GetBoneGlobalRest(foot.Ankle)).Origin - hip.Origin;
                var newLeg = pose.Origin - hip.Origin;
                hip.Basis = new Basis(new Quaternion(oldLeg.Normalized(), newLeg.Normalized())) * hip.Basis;
                hip.Basis = new Basis(hip.Basis.X, hip.Basis.Y * (newLeg.Length() / oldLeg.Length()), hip.Basis.Z);
                _footModifier!.SetWorldPose(foot.Leg, hip);
                _footModifier.SetWorldPose(foot.Ankle, pose);
            }
            UpdateLamp();
        }
        var finalRevision = _footModifier!.RequestedRevision;
        while (_footModifier.AppliedRevision < finalRevision)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!ValidIntervention(sessionIdentity, request)) return false;
        }
        foreach (var foot in _feet)
        {
            var pose = _footModifier.AppliedWorldPose(foot.Ankle);
            foreach (var corner in foot.SoleCorners)
            {
                var sole = pose * corner;
                if (!GroundAt(sole, out var contact, out _) || Math.Abs(sole.Y - contact.Y) > .020f)
                {
                    _actor.SetMeta("rinatInterventionPhase", "awaiting-valid-foot-support");
                    return StepRefused($"landing-support gap={sole.Y - contact.Y:0.000}", sole);
                }
            }
        }
        RequestLandingStep();
        return true;
    }

    private bool StepRefused(string stage, Vector3 at)
    {
        _actor.SetMeta("rinatStepRefusal", stage);
        GD.Print($"rinat-step-refused: {stage} at={at} actor={_actor.GlobalPosition}");
        return false;
    }

    private bool ValidIntervention(object session, long request) => request == _interventionRequest
        && IsInstanceValid(this) && IsInsideTree() && IsInstanceValid(_actor)
        && !IsQueuedForDeletion() && !GetParent().IsQueuedForDeletion()
        && _stage == "forest" && _actor.IsVisibleInTree()
        && _bridge?.CanPresentRinatIntervention(session) == true;

    private void PrepareFeet()
    {
        if (_feet.Count != 0) return;
        if (_humanRig)
        {
            PrepareHumanFeet();
            return;
        }
        foreach (var side in new[] { "Left", "Right" })
        {
            var boots = _actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                .Where(mesh => mesh.Visible && mesh.Name.ToString().Contains("_Boot" + side + "_LOD", StringComparison.Ordinal)).ToArray();
            var boot = boots.Single(mesh => mesh.Name.ToString().Contains("_LOD0", StringComparison.Ordinal));
            var leg = _skeleton.FindBone(side == "Left" ? "Leg.L" : "Leg.R");
            var legRest = _skeleton.GetBoneGlobalRest(leg);
            var sourceSkin = boot.GetSkinReference()?.GetSkin() ?? boot.Skin;
            var bind = FindBind(sourceSkin, _skeleton, leg);
            var modelToSkeleton = legRest * sourceSkin.GetBindPose(bind);
            var vertices = Enumerable.Range(0, boot.Mesh.GetSurfaceCount()).SelectMany(surface =>
                boot.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                .Select(vertex => modelToSkeleton * vertex).ToArray();
            var bottom = vertices.Min(vertex => vertex.Y);
            var corners = vertices.Where(vertex => vertex.Y <= bottom + .0002f).Distinct().ToArray();
            var sole = corners.Aggregate(Vector3.Zero, (sum, point) => sum + point) / corners.Length;
            var ankleRest = new Transform3D(Basis.Identity, sole + Vector3.Up * .095f);
            var name = "RinatStepFoot" + side;
            _skeleton.AddBone(name);
            var ankle = _skeleton.FindBone(name);
            _skeleton.SetBoneParent(ankle, leg);
            _skeleton.SetBoneRest(ankle, legRest.AffineInverse() * ankleRest);
            _skeleton.ResetBonePose(ankle);
            foreach (var mesh in boots)
            {
                var previous = mesh.GetSkinReference()?.GetSkin() ?? mesh.Skin;
                var skin = (Skin)previous.Duplicate();
                var footBind = FindBind(skin, _skeleton, leg);
                skin.SetBindPose(footBind, ankleRest.AffineInverse() * legRest * previous.GetBindPose(footBind));
                skin.SetBindName(footBind, name);
                skin.SetBindBone(footBind, ankle);
                mesh.Skin = skin;
            }
            var inverse = ankleRest.AffineInverse();
            _feet.Add(new Foot(leg, ankle, legRest, inverse * sole, corners.Select(point => inverse * point).ToArray()));
        }
        _actor.SetMeta("rinatStepRig", "instance-local ankle binds; original meshes and authored Tension retained");
        _footModifier = new RinatFootPlacementModifier { Name = "RinatFootPlacement", Active = false, Influence = 1f };
        _skeleton.AddChild(_footModifier);
    }

    // Human kit: thigh, calf and foot. The foot bone is placed at each landing
    // and the knee bends to reach it; the boots keep their authored skin.
    private void PrepareHumanFeet()
    {
        _footModifier = new RinatFootPlacementModifier { Name = "RinatFootPlacement", Active = false, Influence = 1f };
        _skeleton.AddChild(_footModifier);
        foreach (var (side, suffix) in new[] { ("Left", "l"), ("Right", "r") })
        {
            var thigh = _skeleton.FindBone("thigh_" + suffix);
            var calf = _skeleton.FindBone("calf_" + suffix);
            var foot = _skeleton.FindBone("foot_" + suffix);
            var boot = _actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                .Single(mesh => mesh.Visible && mesh.Name.ToString().EndsWith("_Boot" + side + "_LOD0", StringComparison.Ordinal));
            var skin = boot.GetSkinReference()?.GetSkin() ?? boot.Skin;
            var bindToFoot = skin.GetBindPose(FindBind(skin, _skeleton, foot));
            var vertices = Enumerable.Range(0, boot.Mesh.GetSurfaceCount()).SelectMany(surface =>
                boot.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                .Select(vertex => bindToFoot * vertex).ToArray();
            var footRest = _skeleton.GetBoneGlobalRest(foot);
            var bottom = vertices.Min(vertex => (footRest * vertex).Y);
            var corners = vertices.Where(vertex => (footRest * vertex).Y <= bottom + .004f).Distinct().ToArray();
            var sole = corners.Aggregate(Vector3.Zero, (sum, point) => sum + point) / corners.Length;
            _footModifier.RegisterLegChain(thigh, calf, foot);
            _feet.Add(new Foot(thigh, foot, _skeleton.GetBoneGlobalRest(thigh), sole, corners));
        }
        _actor.SetMeta("rinatStepRig", "human kit: thigh/calf/foot two-bone reach; authored boots and Tension");
    }

    private static int FindBind(Skin skin, Skeleton3D skeleton, int bone)
    {
        for (var index = 0; index < skin.GetBindCount(); index++)
        {
            var name = skin.GetBindName(index).ToString();
            if ((name.Length > 0 ? skeleton.FindBone(name) : skin.GetBindBone(index)) == bone) return index;
        }
        throw new InvalidOperationException("Rinat skin does not bind the expected visible limb.");
    }

    private bool GroundAt(Vector3 at, out Vector3 point, out Vector3 normal)
    {
        ResolveRayExclusions();
        using var ray = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * .55f,
            at - Vector3.Up * .65f, 1u, _rayExcludes);
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        point = hit.Count == 0 ? at : hit["position"].AsVector3();
        normal = hit.Count == 0 ? Vector3.Up : hit["normal"].AsVector3();
        return hit.Count != 0 && normal.Dot(Vector3.Up) > .85f;
    }

    private void ResetStep() => CancelIntervention();

    public void CancelIntervention()
    {
        // A destroyed compact zone may never deliver its next SceneTree
        // signal. Complete the caller immediately and separately invalidate
        // the coroutine, including cancellation within the same runtime session.
        _interventionRequest++;
        _interventionCompletion?.TrySetResult(false);
        _interventionCompletion = null;
        _interventionSession = null;
        _stepCompleted = false;
        _intervention = null;
        _pendingLandingStep = false;
        _landingStartQueued = false;
        if (_footModifier is not null && IsInstanceValid(_footModifier)) _footModifier.ClearPoses();
        if (_stepSound is not null && IsInstanceValid(_stepSound))
        {
            _stepSound.Stop();
            _stepSound.StreamPaused = false;
        }
        if (_actor is not null && IsInstanceValid(_actor)) _actor.SetMeta("rinatInterventionPhase", "idle");
    }

    public override void _ExitTree()
    {
        if (_presenceSource is not null && IsInstanceValid(_presenceSource))
        {
            _presenceSource.RuntimeStateChanged -= MarkPresenceDirty;
        }
        _presenceSource = null;
        if (_pauseMenu is not null && IsInstanceValid(_pauseMenu))
            _pauseMenu.PauseChanged -= UpdateLandingStepAudioPause;
        _pauseMenu = null;
        ResetStep();
        if (_stepSound is not null && IsInstanceValid(_stepSound))
        {
            _stepSound.Finished -= OnLandingStepFinished;
            _stepSound.Stream = null;
        }
    }
}
