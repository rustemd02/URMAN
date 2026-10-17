using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private const string BagScenePath = "res://assets/photoworlds/bag/bag-v1.glb";
    private const string BagInteractionId = "urman.fullgame:interaction/pw-forest-bag-release";
    private const string BagDialogueId = "urman.fullgame:dialogue/pw-forest-shurale-d07";
    private const string BagLabelTextId = "urman.fullgame:text/pw-label-forest-shurale";
    private const string BagEncounterFactId = "forest_encounter_resolved";
    private const string BagSnagMarkerPath = "Anchors/F14/BagSnagContact";
    private const string BagPlacementMarkerPath = "Anchors/F14/BagPlacementGround";
    private const string BagSnaggedAnimation = "strap_snagged_rest";
    private const string BagReleaseAnimation = "strap_release";
    private const string BagPlacedAnimation = "strap_placed_rest";

    private enum BagActionPhase { Idle, Releasing, MovingToBoardwalk, Committing }

    private Node3D? _bagRoot;
    private Node3D? _bagSnagContact;
    private Node3D? _bagPlacementGround;
    private Node3D? _bagAssetSnagContact;
    private AnimationPlayer? _bagAnimation;
    private InteractionTarget? _bagTarget;
    private object? _bagActionSession;
    private string? _bagActionSceneId;
    private object? _bagPendingDialogueSession;
    private string? _bagPendingDialogueSceneId;
    private BagActionPhase _bagActionPhase;
    private Transform3D _bagPlacementStart;
    private Transform3D _bagPlacementEnd;
    private double _bagPlacementElapsed;
    private double _bagPlacementDuration;
    private bool _bagMissingMarkerWarningSent;

    private void BuildBagPresentation() => EnsureBagPresentation();

    private void ProcessBagPresentation(double delta)
    {
        if (!EnsureBagPresentation()) return;
        if (TryOpenPendingBagDialogue()) return;
        if (_bagActionPhase == BagActionPhase.Idle) return;

        var bridge = _bridge;
        var resolvedDuringCommit = _bagActionPhase == BagActionPhase.Committing
            && bridge is { } currentBridge && GodotObject.IsInstanceValid(currentBridge)
            && currentBridge.PhotoWorldFactObserved(BagEncounterFactId);
        if (!IsBagActionCurrent(bridge, _bagActionSession, _bagActionSceneId,
                allowResolved: resolvedDuringCommit, allowModal: resolvedDuringCommit))
        {
            CancelBagAction();
            ApplyBagProjection();
            return;
        }

        if (_bagActionPhase == BagActionPhase.MovingToBoardwalk)
            AdvanceBagPlacement(delta);
    }

    private void ApplyBagProjection()
    {
        if (!EnsureBagPresentation() || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge))
            return;

        var projection = bridge.CurrentW01BranchProjection();
        var inForest = projection is { ActiveSceneId: ForestSceneId };
        var resolved = inForest && bridge.PhotoWorldFactObserved(BagEncounterFactId);
        _bagTarget!.Prompt = bridge.ResolveText(BagLabelTextId);

        if (_bagActionPhase == BagActionPhase.Committing
            && IsBagActionCurrent(bridge, _bagActionSession, _bagActionSceneId,
                allowResolved: resolved, allowModal: resolved))
        {
            _bagRoot!.Visible = true;
            _bagTarget!.SetPresentationEnabled(false);
            if (resolved) SetBagRestPose(placed: true);
            return;
        }

        if (_bagActionPhase != BagActionPhase.Idle
            && !resolved
            && IsBagActionCurrent(bridge, _bagActionSession, _bagActionSceneId))
        {
            _bagRoot!.Visible = true;
            _bagTarget!.SetPresentationEnabled(false);
            return;
        }

        CancelBagAction();
        _bagRoot!.Visible = inForest;
        _bagTarget!.SetPresentationEnabled(inForest && !resolved);
        if (inForest) SetBagRestPose(resolved);
    }

    private void ExitBagPresentation()
    {
        if (_bagAnimation is { } animation && GodotObject.IsInstanceValid(animation))
        {
            animation.AnimationFinished -= OnBagAnimationFinished;
            animation.Stop();
        }

        _bagActionSession = null;
        _bagActionSceneId = null;
        ClearPendingBagDialogue();
        _bagActionPhase = BagActionPhase.Idle;
        _bagPlacementElapsed = 0;
        _bagPlacementDuration = 0;
        _bagTarget = null;
        _bagAnimation = null;
        _bagAssetSnagContact = null;
        _bagSnagContact = null;
        _bagPlacementGround = null;
        _bagRoot = null;
    }

    private bool EnsureBagPresentation()
    {
        if (_bagRoot is { } current && GodotObject.IsInstanceValid(current)) return true;

        var snagContact = GetNodeOrNull<Node3D>(BagSnagMarkerPath);
        var placementGround = GetNodeOrNull<Node3D>(BagPlacementMarkerPath);
        if (snagContact is null || placementGround is null)
        {
            if (!_bagMissingMarkerWarningSent)
            {
                GD.PushWarning($"W01 bag presentation requires scene-authored markers: {BagSnagMarkerPath} and {BagPlacementMarkerPath}.");
                _bagMissingMarkerWarningSent = true;
            }
            return false;
        }

        var root = LoadScene(BagScenePath).Instantiate<Node3D>();
        root.Name = "W01Bag_PhysicalInstance";
        root.Visible = false;
        AddChild(root);

        var assetSnagContact = RequireDescendant<Node3D>(root, "Strap_SnagContact");
        var animation = Descendants<AnimationPlayer>(root).FirstOrDefault()
            ?? throw new InvalidOperationException("W01 bag asset is missing its authored AnimationPlayer.");
        if (!animation.HasAnimation(BagSnaggedAnimation)
            || !animation.HasAnimation(BagReleaseAnimation)
            || !animation.HasAnimation(BagPlacedAnimation))
            throw new InvalidOperationException("W01 bag asset is missing a required strap animation.");

        _bagRoot = root;
        _bagSnagContact = snagContact;
        _bagPlacementGround = placementGround;
        _bagAssetSnagContact = assetSnagContact;
        _bagAnimation = animation;
        animation.AnimationFinished += OnBagAnimationFinished;
        SetBagRestPose(placed: false);

        var target = new InteractionTarget
        {
            Name = "W01BagRelease_InteractionTarget",
            InteractionId = BagInteractionId,
            Prompt = _bridge?.ResolveText(BagLabelTextId) ?? string.Empty,
            CollisionLayer = 4u,
            CollisionMask = 0u
        };
        target.ConfigureRayOnly();
        target.PresentationBeforeDispatch = BeginBagAction;
        target.AddChild(new CollisionShape3D
        {
            Name = "BagRelease_RayShape",
            Shape = new SphereShape3D { Radius = 0.30f }
        });
        AddChild(target);
        target.GlobalPosition = snagContact.GlobalPosition;
        target.SetPresentationEnabled(false);
        _bagTarget = target;
        return true;
    }

    private bool BeginBagAction()
    {
        if (_bagActionPhase == BagActionPhase.Committing) return false;
        if (_bagActionPhase != BagActionPhase.Idle) return false;
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.CurrentW01BranchProjection() is not { ActiveSceneId: ForestSceneId } projection
            || bridge.PhotoWorldFactObserved(BagEncounterFactId)
            || BagModalOpen()
            || _bagAnimation is not { } animation || !GodotObject.IsInstanceValid(animation))
            return false;

        _bagActionSession = projection.SessionIdentity;
        _bagActionSceneId = projection.ActiveSceneId;
        _bagActionPhase = BagActionPhase.Releasing;
        _bagTarget?.SetPresentationEnabled(false);
        if (_accessibility.ReducedMotion)
        {
            animation.Play(BagReleaseAnimation, fromEnd: true);
            animation.Advance(0.0);
            if (!BeginBagPlacement())
            {
                CancelBagAction();
                ApplyBagProjection();
            }
        }
        else
        {
            animation.Play(BagReleaseAnimation);
        }
        return false;
    }

    private void OnBagAnimationFinished(StringName animationName)
    {
        if (_bagActionPhase != BagActionPhase.Releasing) return;
        if (!IsBagActionCurrent(_bridge, _bagActionSession, _bagActionSceneId))
        {
            CancelBagAction();
            ApplyBagProjection();
            return;
        }

        if (_bagActionPhase == BagActionPhase.Releasing && animationName == BagReleaseAnimation)
        {
            if (!BeginBagPlacement())
            {
                CancelBagAction();
                ApplyBagProjection();
                return;
            }
        }
    }

    private bool BeginBagPlacement()
    {
        if (_bagRoot is not { } root || !GodotObject.IsInstanceValid(root)
            || _bagPlacementGround is not { } ground || !GodotObject.IsInstanceValid(ground)
            || _bagAnimation is not { } animation || !GodotObject.IsInstanceValid(animation)
            || !animation.HasAnimation(BagPlacedAnimation))
            return false;

        _bagPlacementStart = root.GlobalTransform;
        _bagPlacementEnd = CreateBagPlacementTransform(ground);
        _bagPlacementElapsed = 0;
        _bagPlacementDuration = Math.Max(0.001, animation.GetAnimation(BagPlacedAnimation).Length);

        if (_accessibility.ReducedMotion)
        {
            root.GlobalTransform = _bagPlacementEnd;
            animation.Play(BagPlacedAnimation, fromEnd: true);
            animation.Advance(0.0);
            BeginBagCommit();
            return true;
        }

        _bagActionPhase = BagActionPhase.MovingToBoardwalk;
        animation.Play(BagPlacedAnimation);
        return true;
    }

    private void AdvanceBagPlacement(double delta)
    {
        if (_bagRoot is not { } root || !GodotObject.IsInstanceValid(root)
            || _bagAnimation is not { } animation || !GodotObject.IsInstanceValid(animation)
            || _bagPlacementDuration <= 0)
        {
            CancelBagAction();
            ApplyBagProjection();
            return;
        }

        _bagPlacementElapsed = Math.Min(_bagPlacementDuration,
            _bagPlacementElapsed + Math.Max(0, delta));
        var progress = (float)(_bagPlacementElapsed / _bagPlacementDuration);
        var liftStart = _bagPlacementStart.Origin;
        var liftEnd = liftStart + Vector3.Up * 0.22f;
        var sideStart = _bagPlacementEnd.Origin + Vector3.Up * 0.22f;
        var sideStartTransform = new Transform3D(_bagPlacementStart.Basis, liftEnd);
        var sideEndTransform = new Transform3D(_bagPlacementEnd.Basis, sideStart);
        Transform3D pose;

        if (progress < 0.24f)
        {
            var eased = Mathf.SmoothStep(0f, 1f, progress / 0.24f);
            pose = new Transform3D(_bagPlacementStart.Basis,
                liftStart + (liftEnd - liftStart) * eased);
        }
        else if (progress < 0.76f)
        {
            var eased = Mathf.SmoothStep(0f, 1f, (progress - 0.24f) / 0.52f);
            pose = sideStartTransform.InterpolateWith(sideEndTransform, eased);
        }
        else
        {
            var eased = Mathf.SmoothStep(0f, 1f, (progress - 0.76f) / 0.24f);
            pose = new Transform3D(_bagPlacementEnd.Basis,
                sideStart + (_bagPlacementEnd.Origin - sideStart) * eased);
        }

        root.GlobalTransform = progress >= 1f ? _bagPlacementEnd : pose;
        if (progress < 1f) return;

        // End the authored strap motion on the same frame as the root reaches
        // its ground marker; reducer commit follows only after both poses land.
        animation.Seek(_bagPlacementDuration, update: true);
        BeginBagCommit();
    }

    private void BeginBagCommit()
    {
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge)
            && _bagActionSession is { } session && _bagActionSceneId is { } sceneId)
        {
            _bagActionPhase = BagActionPhase.Committing;
            CommitBagActionAsync(bridge, session, sceneId);
            return;
        }

        CancelBagAction();
        ApplyBagProjection();
    }

    private async void CommitBagActionAsync(RuntimeBridge bridge, object session, string sceneId)
    {
        if (!IsBagActionCurrent(bridge, session, sceneId))
        {
            ReconcileBagCommitIfOwned(session, sceneId);
            return;
        }

        bool committed;
        try
        {
            committed = await bridge.DispatchInteractionAsync(BagInteractionId);
        }
        catch (System.Exception exception)
        {
            GD.PushError($"W01 bag interaction failed before commit: {exception.Message}");
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion())
                ReconcileBagCommitIfOwned(session, sceneId);
            return;
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()) return;
        var projection = GodotObject.IsInstanceValid(bridge) ? bridge.CurrentW01BranchProjection() : null;
        if (!committed || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(session, bridge.SessionIdentity)
            || projection is null || projection.ActiveSceneId != sceneId
            || !bridge.PhotoWorldFactObserved(BagEncounterFactId))
        {
            ReconcileBagCommitIfOwned(session, sceneId);
            return;
        }

        var checkpointSaved = false;
        try
        {
            // Full-game PhotoWorld scenes are not ordinary checkpoint scenes;
            // persist the committed encounter before opening its response.
            checkpointSaved = await bridge.SaveCheckpointAsync(force: true);
        }
        catch (System.Exception exception)
        {
            GD.PushError($"W01 bag encounter checkpoint failed: {exception.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(session, bridge.SessionIdentity)
            || !IsBagActionCurrent(bridge, session, sceneId, allowResolved: true, allowModal: true)
            || bridge.CurrentW01BranchProjection() is not { ActiveSceneId: var activeSceneId }
            || activeSceneId != sceneId
            || !bridge.PhotoWorldFactObserved(BagEncounterFactId))
        {
            ReconcileBagCommitIfOwned(session, sceneId);
            return;
        }

        if (!checkpointSaved)
        {
            GD.PushWarning("W01 bag encounter is committed in this session, but its checkpoint was not saved.");
        }

        CancelBagAction();
        SetBagRestPose(placed: true);
        _bagRoot!.Visible = true;
        _bagTarget?.SetPresentationEnabled(false);
        if (BagModalOpen())
        {
            _bagPendingDialogueSession = session;
            _bagPendingDialogueSceneId = sceneId;
            return;
        }

        ClearPendingBagDialogue();
        bridge.OpenDialogueUi(BagDialogueId);
    }

    private bool TryOpenPendingBagDialogue()
    {
        if (_bagPendingDialogueSession is not { } session || _bagPendingDialogueSceneId is not { } sceneId)
            return false;

        var bridge = _bridge;
        if (bridge is null || !GodotObject.IsInstanceValid(bridge))
        {
            ClearPendingBagDialogue();
            return false;
        }

        var projection = bridge.CurrentW01BranchProjection();
        if (projection is null || !ReferenceEquals(session, bridge.SessionIdentity)
            || !ReferenceEquals(projection.SessionIdentity, session)
            || projection.ActiveSceneId != sceneId
            || !bridge.PhotoWorldFactObserved(BagEncounterFactId))
        {
            ClearPendingBagDialogue();
            return false;
        }

        if (BagModalOpen()) return false;

        ClearPendingBagDialogue();
        SetBagRestPose(placed: true);
        _bagRoot!.Visible = true;
        _bagTarget?.SetPresentationEnabled(false);
        bridge.OpenDialogueUi(BagDialogueId);
        return true;
    }

    private void ClearPendingBagDialogue()
    {
        _bagPendingDialogueSession = null;
        _bagPendingDialogueSceneId = null;
    }

    private bool BagModalOpen() =>
        MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: true };

    private void ReconcileBagCommitIfOwned(object session, string sceneId)
    {
        if (!OwnsBagCommit(session, sceneId)) return;

        CancelBagAction();
        ApplyBagProjection();
    }

    private bool OwnsBagCommit(object session, string sceneId) =>
        _bagActionPhase == BagActionPhase.Committing
        && ReferenceEquals(_bagActionSession, session)
        && _bagActionSceneId == sceneId;

    private bool IsBagActionCurrent(
        RuntimeBridge? bridge,
        object? session,
        string? sceneId,
        bool allowResolved = false,
        bool allowModal = false)
    {
        if (bridge is null || !GodotObject.IsInstanceValid(bridge)
            || session is null || sceneId != ForestSceneId
            || !ReferenceEquals(session, bridge.SessionIdentity)
            || (!allowModal && BagModalOpen()))
            return false;

        var projection = bridge.CurrentW01BranchProjection();
        return projection is not null
            && ReferenceEquals(projection.SessionIdentity, session)
            && projection.ActiveSceneId == sceneId
            && (allowResolved || !bridge.PhotoWorldFactObserved(BagEncounterFactId));
    }

    private Transform3D CreateBagPlacementTransform(Node3D ground)
    {
        var basis = Basis.FromEuler(new Vector3(-Mathf.Pi * 0.5f, ground.GlobalRotation.Y, 0f));
        return new Transform3D(basis, ground.GlobalPosition + Vector3.Up * 0.060f);
    }

    private void SetBagRestPose(bool placed)
    {
        if (_bagRoot is not { } root || !GodotObject.IsInstanceValid(root)
            || _bagAssetSnagContact is not { } assetContact || !GodotObject.IsInstanceValid(assetContact)
            || _bagSnagContact is not { } snagContact || !GodotObject.IsInstanceValid(snagContact)
            || _bagAnimation is not { } animation || !GodotObject.IsInstanceValid(animation))
            return;

        animation.Stop();
        if (placed)
        {
            if (_bagPlacementGround is not { } placementGround || !GodotObject.IsInstanceValid(placementGround))
                return;
            root.GlobalTransform = CreateBagPlacementTransform(placementGround);
        }
        else
        {
            root.GlobalTransform = Transform3D.Identity;
            var localContact = root.ToLocal(assetContact.GlobalPosition);
            root.GlobalPosition = snagContact.GlobalPosition - localContact;
        }

        animation.Play(placed ? BagPlacedAnimation : BagSnaggedAnimation, fromEnd: true);
        animation.Advance(0.0);
    }

    private void CancelBagAction()
    {
        if (_bagAnimation is { } animation && GodotObject.IsInstanceValid(animation)) animation.Stop();
        _bagActionPhase = BagActionPhase.Idle;
        _bagActionSession = null;
        _bagActionSceneId = null;
        _bagPlacementElapsed = 0;
        _bagPlacementDuration = 0;
    }
}
