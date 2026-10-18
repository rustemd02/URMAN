using Godot;

namespace Urman.Godot;

public partial class PhotoBankApproachPresence
{
    private const float LeafFallDuration = 2.7f;
    private const float LeafRevealDuration = .6f;
    private const float LeafFallArc = .18f;
    private const float LeafAlsuSightRadius = 4.5f;

    private enum LeafPhase { Hidden, Falling, Committing, Landed }

    private Node3D? _leafRevealPivot;
    private MeshInstance3D? _winterLeaf;
    private Node3D? _leafInteractionWitness;
    private Node3D? _leafTwig;
    private CollisionObject3D? _leafTwigBody;
    private Node3D? _leafLanding;
    private bool _leafNodesInitialized;
    private bool _warnedMissingLeafNodes;
    private Transform3D _leafInitialTransform;
    private Transform3D _leafFallStart;
    private LeafPhase _leafPhase;
    private object? _leafSession;
    private int _leafObservationGeneration;
    private float _leafFallElapsed;
    private bool _leafLandingShown;
    private bool _leafCommitAwaitingProjection;
    private bool _leafDispatchSucceeded;
    private FirstPersonController? _leafExcludedPlayer;
    private Node3D? _leafExcludedAlsu;
    private readonly global::Godot.Collections.Array<Rid> _leafPlayerRayExclusions = new();
    private readonly global::Godot.Collections.Array<Rid> _leafAlsuRayExclusions = new();

    private bool EnsureLeafPresentationNodes()
    {
        if (_bankApproach is not { } bank || !GodotObject.IsInstanceValid(bank)) return false;
        _leafRevealPivot ??= bank.GetNodeOrNull<Node3D>("PersistentLeafRevealPivot");
        _leafLanding ??= bank.GetNodeOrNull<Node3D>("LeafLanding");
        _leafTwig ??= bank.GetNodeOrNull<Node3D>("PersistentLeafTwig");
        _leafTwigBody ??= bank.GetNodeOrNull<CollisionObject3D>("PersistentLeafTwigBody");
        _leafWitness ??= bank.GetNodeOrNull<Node3D>("LeafWitness");
        _winterLeaf ??= _leafRevealPivot?.GetNodeOrNull<MeshInstance3D>("PersistentWinterLeaf");
        _leafInteractionWitness ??= _leafRevealPivot?.GetNodeOrNull<Node3D>("LeafInteractionWitness");
        if (_leafRevealPivot is null || _winterLeaf is null || _leafInteractionWitness is null
            || _leafTwig is null || _leafTwigBody is null || _leafLanding is null || _leafWitness is null)
        {
            if (!_warnedMissingLeafNodes)
            {
                GD.PushWarning("PhotoBankApproachPresence is waiting for PersistentLeafRevealPivot, PersistentWinterLeaf, LeafInteractionWitness, PersistentLeafTwig, LeafLanding, and LeafWitness.");
                _warnedMissingLeafNodes = true;
            }
            return false;
        }

        if (!_leafNodesInitialized)
        {
            _leafInitialTransform = _leafRevealPivot.GlobalTransform;
            _leafRevealPivot.Visible = false;
            _leafNodesInitialized = true;
        }
        SynchronizeLeafWitnessProxy();
        return true;
    }

    private void ProcessLeafWitness(double delta)
    {
        if (!_leafNodesInitialized || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)) return;
        var route = bridge.CurrentFullGamePhotoWorldRouteProjection();
        if (route is null) return;

        var session = bridge.SessionIdentity;
        if (session is null || !ReferenceEquals(route.SessionIdentity, session)) return;
        if (!ReferenceEquals(_leafSession, session))
        {
            ResetLeafToTwig();
            _leafSession = session;
        }

        SynchronizeLeafWitnessProxy();
        if (bridge.PhotoWorldFactObserved(LeafWitnessFactId))
        {
            SetLeafOnSnow();
            return;
        }

        if (_leafCommitAwaitingProjection)
        {
            var dispatchSucceeded = _leafDispatchSucceeded;
            _leafCommitAwaitingProjection = false;
            if (dispatchSucceeded && bridge.PhotoWorldFactObserved(LeafWitnessFactId)) SetLeafOnSnow();
            else ResetLeafToTwig();
            return;
        }

        if (route.ActiveSceneId != BankSceneId)
        {
            if (_leafPhase is LeafPhase.Falling or LeafPhase.Committing) ResetLeafToTwig();
            return;
        }

        if (_leafPhase == LeafPhase.Landed)
        {
            ResetLeafToTwig();
            return;
        }
        if (_leafPhase == LeafPhase.Committing) return;
        if (!TryGetLeafEventContext(bridge, session, out var player, out var camera, out var alsu))
        {
            if (_leafPhase == LeafPhase.Falling) ResetLeafToTwig();
            return;
        }

        if (_leafPhase == LeafPhase.Hidden)
        {
            if (!AlsuCanSeeTwig(alsu)) return;
            _leafRevealPivot!.GlobalTransform = _leafInitialTransform;
            _leafRevealPivot.Visible = true;
            if (!LeafFullyFramedAndVisible(camera, player))
            {
                _leafRevealPivot.Visible = false;
                return;
            }
            _leafFallStart = _leafInitialTransform;
            _leafFallElapsed = 0;
            _leafLandingShown = false;
            _leafPhase = LeafPhase.Falling;
            _leafObservationGeneration++;
            SynchronizeLeafWitnessProxy();
            return;
        }

        if (!LeafFullyFramedAndVisible(camera, player)) return;
        if (_leafLandingShown)
        {
            _leafPhase = LeafPhase.Committing;
            DispatchLeafWitnessAsync(bridge, session, _leafObservationGeneration);
            return;
        }

        if (player.ReducedMotion)
        {
            // Show the branch and the resulting snow trace long enough to read
            // without the arcing fall or rotation.
            _leafFallElapsed += (float)Math.Max(0, delta);
            _leafRevealPivot!.GlobalTransform = _leafFallElapsed < .5f
                ? _leafInitialTransform : _leafLanding!.GlobalTransform;
            _leafLandingShown = _leafFallElapsed >= 1f;
            SynchronizeLeafWitnessProxy();
            return;
        }

        _leafFallElapsed = Math.Min(LeafRevealDuration + LeafFallDuration,
            _leafFallElapsed + (float)Math.Max(0, delta));
        if (_leafFallElapsed < LeafRevealDuration)
        {
            var opening = Mathf.Lerp(.08f, 1f,
                Mathf.SmoothStep(0f, 1f, _leafFallElapsed / LeafRevealDuration));
            _leafRevealPivot!.GlobalTransform = new Transform3D(
                _leafInitialTransform.Basis.Scaled(new Vector3(opening, opening, opening)),
                _leafInitialTransform.Origin);
            SynchronizeLeafWitnessProxy();
            return;
        }
        var progress = Mathf.Clamp((_leafFallElapsed - LeafRevealDuration) / LeafFallDuration, 0f, 1f);
        var eased = Mathf.SmoothStep(0f, 1f, progress);
        var pose = _leafFallStart.InterpolateWith(_leafLanding!.GlobalTransform, eased);
        pose.Origin += Vector3.Up * (LeafFallArc * Mathf.Sin(Mathf.Pi * progress));
        _leafRevealPivot!.GlobalTransform = pose;
        if (progress >= 1f)
        {
            _leafRevealPivot.GlobalTransform = _leafLanding.GlobalTransform;
            _leafLandingShown = true;
        }
        SynchronizeLeafWitnessProxy();
    }

    private bool TryGetLeafEventContext(
        RuntimeBridge bridge, object session,
        out FirstPersonController player, out Camera3D camera, out Node3D alsu)
    {
        player = null!;
        camera = null!;
        alsu = null!;
        var currentWalk = AlsuStreetWalkPresentation.SessionOwner(GetTree());
        if (currentWalk is null || !GodotObject.IsInstanceValid(currentWalk)) return false;
        var currentAlsu = currentWalk.Actor;
        var route = bridge.CurrentFullGamePhotoWorldRouteProjection();
        if (!bridge.IsPhotoWorldsCampaign || bridge.CurrentZoneId != "village_day"
            || route is not { ActiveSceneId: BankSceneId }
            || !ReferenceEquals(route.SessionIdentity, session)
            || !ReferenceEquals(bridge.SessionIdentity, session)
            || !string.Equals(bridge.ActiveSceneId, BankSceneId, StringComparison.Ordinal)
            || !IsInteractionReady(bridge, LeafWitnessInteractionId)
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController currentPlayer
            || !GodotObject.IsInstanceValid(currentPlayer)
            || currentPlayer.ModalOpen || currentPlayer.VehicleControlled || currentPlayer.IsClimbingLadder
            || !currentPlayer.IsOnFloor() || currentPlayer.HasPhotoBookGrip
            || LeafModalOpen()
            || currentPlayer.GetNodeOrNull<Camera3D>("Head/Camera3D") is not { Current: true } currentCamera
            || currentCamera.GetViewport().GetCamera3D() != currentCamera
            || !currentWalk.PhysicalAccessReady
            || !GodotObject.IsInstanceValid(currentAlsu)
            || !currentAlsu.IsVisibleInTree())
            return false;

        player = currentPlayer;
        camera = currentCamera;
        alsu = currentAlsu;
        return alsu.GlobalPosition.DistanceTo(_leafTwig!.GlobalPosition) <= LeafAlsuSightRadius;
    }

    private bool LeafModalOpen() => MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true };

    private bool LeafFullyFramedAndVisible(Camera3D camera, FirstPersonController player)
    {
        if (_winterLeaf is not { } leaf || !GodotObject.IsInstanceValid(leaf) || !leaf.IsVisibleInTree()) return false;
        if (camera.GlobalPosition.DistanceTo(leaf.GlobalPosition) > 6f) return false;
        var bounds = leaf.GetAabb();
        for (var corner = 0; corner < 8; corner++)
        {
            var point = new Vector3(
                (corner & 1) == 0 ? bounds.Position.X : bounds.End.X,
                (corner & 2) == 0 ? bounds.Position.Y : bounds.End.Y,
                (corner & 4) == 0 ? bounds.Position.Z : bounds.End.Z);
            if (!camera.IsPositionInFrustum(leaf.GlobalTransform * point)) return false;
        }

        EnsureLeafPlayerRayExclusion(player);
        var center = leaf.GlobalTransform * bounds.GetCenter();
        using var query = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, center, 3u, _leafPlayerRayExclusions);
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0;
    }

    private bool AlsuCanSeeTwig(Node3D actor)
    {
        var eye = actor.GlobalPosition + Vector3.Up * 1.5f;
        var twig = _leafTwig!.GlobalPosition;
        var direction = twig - eye;
        if (direction.LengthSquared() < .01f || actor.GlobalBasis.Z.Normalized().Dot(direction.Normalized()) <= 0f)
            return false;

        EnsureLeafAlsuRayExclusion(actor);
        using var query = PhysicsRayQueryParameters3D.Create(eye, twig, 3u, _leafAlsuRayExclusions);
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0
            || hit["collider"].AsGodotObject() is CollisionObject3D actual
                && ReferenceEquals(actual, _leafTwigBody);
    }

    private void EnsureLeafPlayerRayExclusion(FirstPersonController player)
    {
        if (ReferenceEquals(_leafExcludedPlayer, player)) return;
        _leafPlayerRayExclusions.Clear();
        _leafPlayerRayExclusions.Add(player.GetRid());
        _leafExcludedPlayer = player;
    }

    private void EnsureLeafAlsuRayExclusion(Node3D actor)
    {
        if (ReferenceEquals(_leafExcludedAlsu, actor)) return;
        _leafAlsuRayExclusions.Clear();
        if (actor.GetNodeOrNull<CollisionObject3D>("AlsuStreetWalk/AlsuPhysicalContact") is { } body)
            _leafAlsuRayExclusions.Add(body.GetRid());
        _leafExcludedAlsu = actor;
    }

    private async void DispatchLeafWitnessAsync(RuntimeBridge bridge, object session, int generation)
    {
        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(LeafWitnessInteractionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"PhotoBank leaf observation failed: {error.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || generation != _leafObservationGeneration || !ReferenceEquals(_leafSession, session)
            || !GodotObject.IsInstanceValid(bridge) || !ReferenceEquals(_bridge, bridge)) return;
        if (bridge.SessionIdentity is { } currentSession && !ReferenceEquals(currentSession, session)) return;

        if (bridge.SessionIdentity is null || bridge.CurrentFullGamePhotoWorldRouteProjection() is null)
        {
            _leafDispatchSucceeded = committed;
            _leafCommitAwaitingProjection = true;
            return;
        }

        if (committed && bridge.PhotoWorldFactObserved(LeafWitnessFactId)) SetLeafOnSnow();
        else ResetLeafToTwig();
    }

    private void SetLeafOnSnow()
    {
        if (_leafRevealPivot is not { } pivot || !GodotObject.IsInstanceValid(pivot)
            || _leafLanding is not { } landing || !GodotObject.IsInstanceValid(landing)) return;
        pivot.GlobalTransform = landing.GlobalTransform;
        pivot.Visible = true;
        _leafPhase = LeafPhase.Landed;
        _leafFallElapsed = LeafFallDuration;
        _leafLandingShown = true;
        _leafCommitAwaitingProjection = false;
        SynchronizeLeafWitnessProxy();
    }

    private void ResetLeafToTwig()
    {
        _leafObservationGeneration++;
        _leafPhase = LeafPhase.Hidden;
        _leafFallElapsed = 0;
        _leafLandingShown = false;
        _leafCommitAwaitingProjection = false;
        _leafDispatchSucceeded = false;
        if (_leafRevealPivot is { } pivot && GodotObject.IsInstanceValid(pivot))
        {
            pivot.GlobalTransform = _leafInitialTransform;
            pivot.Visible = false;
        }
        SynchronizeLeafWitnessProxy();
    }

    private void SynchronizeLeafWitnessProxy()
    {
        if (_leafWitness is { } proxy && GodotObject.IsInstanceValid(proxy)
            && _leafInteractionWitness is { } witness && GodotObject.IsInstanceValid(witness))
            proxy.GlobalTransform = witness.GlobalTransform;
    }
}
