using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class W01Controller
{
    private const string ShuraleScenePath = "res://assets/photoworlds/shurale/shurale_v1.glb";
    private const string ShuraleRootedShootScenePath = "res://assets/photoworlds/shurale/shurale_rooted_work_shoot_v1.glb";
    private const string ShuraleInteractionId = "urman.fullgame:interaction/pw-forest-shurale";
    private const string ShuraleDialogueId = "urman.fullgame:dialogue/pw-forest-shurale-d06";
    private const string ShuraleLabelTextId = "urman.fullgame:text/pw-label-forest-shurale-talk";
    private const string ShuraleInstructionsFactId = "forest_shurale_instructions_heard";
    private const string ShuraleEncounterFactId = "forest_encounter_resolved";
    private const string ShuraleSpeakerRole = "shurale";
    private const string ShuraleAnchorPath = "Anchors/F14/ShuraleRoot";
    private const float ShuraleNoticeDistance = 5f;
    private const float ShuraleHeadGazeInfluence = 0.58f;
    private const float ShuraleNeckGazeInfluence = 0.22f;

    private enum ShuraleAnimationPhase
    {
        Resting,
        Noticing,
        Gesturing,
        Speaking,
        Yielding
    }

    private Node3D? _shuraleRoot;
    private Marker3D? _shuraleAnchor;
    private MeshInstance3D? _shuraleLod0;
    private MeshInstance3D? _shuraleLod1;
    private Node3D? _shuraleWorkBranch;
    private StaticBody3D? _shuraleContact;
    private InteractionTarget? _shuraleTarget;
    private AnimationPlayer? _shuraleAnimation;
    private Node3D? _shuraleGazeTarget;
    private LookAtModifier3D? _shuraleHeadGaze;
    private LookAtModifier3D? _shuraleNeckGaze;
    private object? _shuraleSessionIdentity;
    private bool _shuraleProjectionInitialized;
    private bool _shuraleWasVisible;
    private bool _shuraleInstructionsHeard;
    private bool _shuraleEncounterResolved;
    private bool _shuraleApproachTriggered;
    private bool _shuraleHasDistanceSample;
    private float _shuralePreviousDistance;
    private string? _shuraleCurrentAnimation;
    private string? _shuraleActiveDialogueId;
    private string? _shuraleActiveDialogueNodeId;
    private ShuraleAnimationPhase _shuraleAnimationPhase;

    private void BuildShuralePresentation()
    {
        if (_shuraleRoot is { } existing && GodotObject.IsInstanceValid(existing)) return;

        _shuraleAnchor = GetNode<Marker3D>(ShuraleAnchorPath);
        var root = LoadScene(ShuraleScenePath).Instantiate<Node3D>();
        root.Name = "W01Shurale_PhysicalInstance";
        root.Visible = false;
        AddChild(root);
        root.GlobalTransform = _shuraleAnchor.GlobalTransform;

        // The shoot mesh is authored in F14 space; it joins the fixed root
        // to the left-supported work branch without adding an interaction collider.
        var rootedShoot = LoadScene(ShuraleRootedShootScenePath).Instantiate<Node3D>();
        rootedShoot.Name = "ShuraleRootedWorkShoot";
        root.AddChild(rootedShoot);
        rootedShoot.GlobalTransform = GetNode<Marker3D>("Anchors/F14").GlobalTransform;

        _shuraleLod0 = RequireDescendant<MeshInstance3D>(root, "ShuraleBody_LOD0");
        _shuraleLod1 = RequireDescendant<MeshInstance3D>(root, "ShuraleBody_LOD1");
        ConfigureShuraleLod(_shuraleLod0, _shuraleLod1);
        _shuraleWorkBranch = RequireDescendant<Node3D>(root, "ShuraleWorkBranch");

        var skeleton = _shuraleLod0.GetNodeOrNull<Skeleton3D>(_shuraleLod0.Skeleton)
            ?? throw new InvalidOperationException("W01 Shurale LOD0 is missing its imported Skeleton3D.");
        // Godot replaces glTF bone-name colons with underscores during import.
        var headBone = FindShuraleBone(skeleton, "head", "mixamorig_Head");
        var neckBone = FindShuraleBone(skeleton, "neck_01", "mixamorig_Neck");
        if (headBone < 0)
            throw new InvalidOperationException("W01 Shurale rig is missing head or mixamorig_Head.");
        _shuraleGazeTarget = new Node3D { Name = "W01Shurale_GazeTarget" };
        root.AddChild(_shuraleGazeTarget);
        _shuraleGazeTarget.GlobalPosition = root.GlobalPosition + Vector3.Up * 1.9f
            + root.GlobalBasis * Vector3.Forward * 2f;
        if (neckBone >= 0)
            _shuraleNeckGaze = AddShuraleLookModifier(
                root, skeleton, neckBone, _shuraleGazeTarget, "W01Shurale_NeckGaze", 15f, 8f);
        _shuraleHeadGaze = AddShuraleLookModifier(
            root, skeleton, headBone, _shuraleGazeTarget, "W01Shurale_HeadGaze", 24f, 12f);

        var animation = Descendants<AnimationPlayer>(root).FirstOrDefault()
            ?? throw new InvalidOperationException("W01 Shurale asset is missing its imported AnimationPlayer.");
        foreach (var clip in new[] { "work", "noticed", "gesture", "speak", "yield", "step", "idle" })
        {
            if (!animation.HasAnimation(clip))
                throw new InvalidOperationException($"W01 Shurale asset is missing animation: {clip}");

            animation.GetAnimation(clip).LoopMode = clip is "work" or "idle"
                ? Animation.LoopModeEnum.Linear
                : Animation.LoopModeEnum.None;
        }

        var contact = new StaticBody3D
        {
            Name = "W01Shurale_PhysicalContact",
            CollisionLayer = 0u,
            CollisionMask = 0u,
            Position = new Vector3(0f, 1.175f, 0f)
        };
        contact.AddChild(new CollisionShape3D
        {
            Name = "W01Shurale_BodyCapsule",
            Shape = new CapsuleShape3D { Radius = 0.30f, Height = 2.35f }
        });
        root.AddChild(contact);

        var target = new InteractionTarget
        {
            Name = "W01Shurale_InteractionTarget",
            InteractionId = ShuraleInteractionId,
            DialogueId = ShuraleDialogueId,
            Prompt = _bridge?.ResolveText(ShuraleLabelTextId) ?? string.Empty,
            CollisionLayer = 4u,
            CollisionMask = 0u,
            Position = new Vector3(0f, 1.95f, 0f)
        };
        target.ConfigureRayOnly();
        target.PresentationGate = IsShuraleInteractionGateOpen;
        target.AfterDispatch = () => Task.FromResult(IsShuraleInteractionGateOpen());
        target.AddChild(new CollisionShape3D
        {
            Name = "W01Shurale_InteractionRayShape",
            // The player's interaction ray also sees the layer-2 body capsule.
            // Cover the visible upper torso and face, extending just beyond
            // that body's upper cross-section from every approach direction.
            Shape = new CapsuleShape3D { Radius = 0.36f, Height = 1.3f }
        });
        root.AddChild(target);
        target.SetPresentationEnabled(false);

        _shuraleRoot = root;
        _shuraleContact = contact;
        _shuraleTarget = target;
        _shuraleAnimation = animation;
        animation.AnimationFinished += OnShuraleAnimationFinished;
        SetShuraleWorkBranchVisible(false);
    }

    private void ProcessShuralePresentation(double delta)
    {
        if (_shuraleRoot is not { } root || !GodotObject.IsInstanceValid(root)
            || !root.Visible)
            return;

        var bridge = _bridge;
        var projection = bridge is { } currentBridge && GodotObject.IsInstanceValid(currentBridge)
            ? currentBridge.CurrentW01BranchProjection()
            : null;
        if (projection is not { ActiveSceneId: ForestSceneId } current
            || !ReferenceEquals(current.SessionIdentity, _shuraleSessionIdentity))
        {
            _shuraleHasDistanceSample = false;
            ResetShuraleGaze();
            return;
        }

        ProcessShuraleDialogueAnimation();
        ProcessShuraleGaze(delta, root);
        if (_shuraleApproachTriggered || _shuraleInstructionsHeard || _shuraleEncounterResolved)
            return;

        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player)
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder
            || !player.IsOnFloor())
        {
            _shuraleHasDistanceSample = false;
            return;
        }

        var distance = new Vector2(
            player.GlobalPosition.X - root.GlobalPosition.X,
            player.GlobalPosition.Z - root.GlobalPosition.Z).Length();
        if (!_shuraleHasDistanceSample)
        {
            _shuralePreviousDistance = distance;
            _shuraleHasDistanceSample = true;
            return;
        }

        var enteredNoticeRange = _shuralePreviousDistance > ShuraleNoticeDistance
            && distance <= ShuraleNoticeDistance;
        _shuralePreviousDistance = distance;
        if (enteredNoticeRange) BeginShuraleApproach();
    }

    private void ProcessShuraleDialogueAnimation()
    {
        var dialogueUi = GetShuraleDialogueUi();
        var dialogueId = dialogueUi?.CurrentDialogueId;
        if (dialogueId == ShuraleDialogueId || dialogueId == BagDialogueId)
        {
            if (dialogueId == ShuraleDialogueId) _shuraleApproachTriggered = true;
            var dialogueChanged = _shuraleActiveDialogueId != dialogueId;
            var nodeChanged = _shuraleActiveDialogueNodeId != dialogueUi?.CurrentNodeId;
            _shuraleActiveDialogueId = dialogueId;
            _shuraleActiveDialogueNodeId = dialogueUi?.CurrentNodeId;
            var staticPose = dialogueId == ShuraleDialogueId ? "gesture" : "yield";
            if (_accessibility.ReducedMotion || dialogueUi?.CurrentSpeakerRole != ShuraleSpeakerRole)
            {
                if (_shuraleCurrentAnimation != staticPose || _shuraleAnimationPhase != ShuraleAnimationPhase.Resting)
                    SetShuraleTerminalPose(staticPose);
            }
            else if (dialogueChanged || nodeChanged || _shuraleCurrentAnimation != "speak")
            {
                PlayShuraleClip("speak", ShuraleAnimationPhase.Speaking);
            }

            return;
        }

        if (_shuraleActiveDialogueId is { } endedDialogueId)
        {
            // Open() assigns the dialogue before its first node finishes loading.
            // Keep the current response pose until the UI can identify that node.
            if (dialogueUi is { IsOpen: true } && dialogueId is null) return;

            _shuraleActiveDialogueId = null;
            _shuraleActiveDialogueNodeId = null;
            if (endedDialogueId == ShuraleDialogueId)
            {
                SetShuraleTerminalPose("gesture");
                return;
            }

            if (endedDialogueId == BagDialogueId)
            {
                SetShuraleRestPose(_shuraleInstructionsHeard, _shuraleEncounterResolved);
                return;
            }
        }

        if (!_shuraleEncounterResolved) return;

        if (ShouldHoldShuraleYieldForD07())
        {
            if (_shuraleCurrentAnimation != "yield")
            {
                if (_accessibility.ReducedMotion) SetShuraleTerminalPose("yield");
                else PlayShuraleClip("yield", ShuraleAnimationPhase.Yielding);
            }

            return;
        }

        if (_shuraleCurrentAnimation != "work")
            SetShuraleRestPose(_shuraleInstructionsHeard, encounterResolved: true);
    }

    private DialogueUi? GetShuraleDialogueUi() =>
        GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi dialogueUi
        && GodotObject.IsInstanceValid(dialogueUi)
            ? dialogueUi
            : null;

    private bool ShouldHoldShuraleYieldForD07()
    {
        var session = _shuraleSessionIdentity;
        if (session is not null
            && ((_bagActionPhase == BagActionPhase.Committing
                 && ReferenceEquals(_bagActionSession, session)
                 && _bagActionSceneId == ForestSceneId)
                || (_bagPendingDialogueSession is { } pendingSession
                    && ReferenceEquals(pendingSession, session)
                    && _bagPendingDialogueSceneId == ForestSceneId)))
            return true;

        var dialogueUi = GetShuraleDialogueUi();
        var dialogueId = dialogueUi?.CurrentDialogueId;
        return dialogueId == BagDialogueId
            || dialogueUi is { IsOpen: true } && dialogueId is null;
    }

    private void ApplyShuraleProjection()
    {
        if (_shuraleRoot is not { } root || !GodotObject.IsInstanceValid(root)
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge))
            return;

        var projection = bridge.CurrentW01BranchProjection();
        if (projection is null)
        {
            HideShuralePresentation();
            return;
        }

        if (!ReferenceEquals(_shuraleSessionIdentity, projection.SessionIdentity))
        {
            ResetShuraleSession(projection.SessionIdentity);
        }

        var inForest = projection.ActiveSceneId == ForestSceneId;
        if (!inForest)
        {
            _shuraleInstructionsHeard = bridge.PhotoWorldFactObserved(ShuraleInstructionsFactId);
            _shuraleEncounterResolved = bridge.PhotoWorldFactObserved(ShuraleEncounterFactId);
            _shuraleProjectionInitialized = true;
            HideShuralePresentation();
            return;
        }

        var instructionsHeard = bridge.PhotoWorldFactObserved(ShuraleInstructionsFactId);
        var encounterResolved = bridge.PhotoWorldFactObserved(ShuraleEncounterFactId);
        if (!_shuraleProjectionInitialized)
        {
            _shuraleInstructionsHeard = instructionsHeard;
            _shuraleEncounterResolved = encounterResolved;
            _shuraleProjectionInitialized = true;
            _shuraleApproachTriggered = instructionsHeard || encounterResolved;
            _shuraleHasDistanceSample = false;
            SetShuraleRestPose(instructionsHeard, encounterResolved);
        }
        else if (!_shuraleWasVisible)
        {
            _shuraleInstructionsHeard = instructionsHeard;
            _shuraleEncounterResolved = encounterResolved;
            _shuraleApproachTriggered |= instructionsHeard || encounterResolved;
            _shuraleHasDistanceSample = false;
            SetShuraleRestPose(instructionsHeard, encounterResolved);
        }
        else if (encounterResolved && !_shuraleEncounterResolved)
        {
            _shuraleEncounterResolved = true;
            _shuraleInstructionsHeard = instructionsHeard;
            _shuraleApproachTriggered = true;
            _shuraleHasDistanceSample = false;
            SetShuraleRestPose(instructionsHeard, encounterResolved: true);
        }
        else if (instructionsHeard && !_shuraleInstructionsHeard)
        {
            _shuraleInstructionsHeard = true;
            _shuraleApproachTriggered = true;
            _shuraleHasDistanceSample = false;
            if (GetShuraleDialogueUi()?.CurrentDialogueId != ShuraleDialogueId)
                SetShuraleTerminalPose("gesture");
        }

        root.Visible = true;
        SetShuraleWorkBranchVisible(true);
        _shuraleTarget!.Prompt = bridge.ResolveText(ShuraleLabelTextId);
        _shuraleTarget.SetPresentationEnabled(true);
        SetCollisionLayer(_shuraleContact, 2u);
        _shuraleWasVisible = true;
    }

    private void ApplyShuraleAccessibilitySettings(AccessibilitySettingsSnapshot settings, bool wasReducedMotion)
    {
        if (_shuraleRoot is not { Visible: true } root || !GodotObject.IsInstanceValid(root)) return;

        if (settings.ReducedMotion) ResetShuraleGaze();

        if (!settings.ReducedMotion)
        {
            if (!wasReducedMotion) return;
            // Resume only on the live on→off transition. An unrelated settings
            // refresh must not restart a response or replay a completed notice.
            _shuraleActiveDialogueId = null;
            _shuraleActiveDialogueNodeId = null;
            var activeDialogue = GetShuraleDialogueUi()?.CurrentDialogueId;
            if (activeDialogue == ShuraleDialogueId || activeDialogue == BagDialogueId)
                ProcessShuraleDialogueAnimation();
            else
                SetShuraleRestPose(_shuraleInstructionsHeard, _shuraleEncounterResolved);
            return;
        }

        var dialogueId = GetShuraleDialogueUi()?.CurrentDialogueId;
        if (dialogueId == ShuraleDialogueId)
            SetShuraleTerminalPose("gesture");
        else if (dialogueId == BagDialogueId)
            SetShuraleTerminalPose("yield");
        else if (_shuraleEncounterResolved)
            SetShuraleTerminalPose(ShouldHoldShuraleYieldForD07() ? "yield" : "work");
        else if (_shuraleInstructionsHeard || _shuraleAnimationPhase is
                 ShuraleAnimationPhase.Noticing or ShuraleAnimationPhase.Gesturing or ShuraleAnimationPhase.Speaking)
            SetShuraleTerminalPose("gesture");
    }

    private void ExitShuralePresentation()
    {
        if (_shuraleAnimation is { } animation && GodotObject.IsInstanceValid(animation))
        {
            animation.AnimationFinished -= OnShuraleAnimationFinished;
            animation.Stop();
        }

        ResetShuraleGaze();
        _shuraleRoot = null;
        _shuraleAnchor = null;
        _shuraleLod0 = null;
        _shuraleLod1 = null;
        _shuraleWorkBranch = null;
        _shuraleContact = null;
        _shuraleTarget = null;
        _shuraleAnimation = null;
        _shuraleGazeTarget = null;
        _shuraleHeadGaze = null;
        _shuraleNeckGaze = null;
        _shuraleSessionIdentity = null;
        _shuraleProjectionInitialized = false;
        _shuraleWasVisible = false;
        _shuraleInstructionsHeard = false;
        _shuraleEncounterResolved = false;
        _shuraleApproachTriggered = false;
        _shuraleHasDistanceSample = false;
        _shuraleCurrentAnimation = null;
        _shuraleActiveDialogueId = null;
        _shuraleActiveDialogueNodeId = null;
        _shuraleAnimationPhase = ShuraleAnimationPhase.Resting;
    }

    private void ResetShuraleSession(object sessionIdentity)
    {
        StopShuraleClip();
        ResetShuraleGaze();
        _shuraleSessionIdentity = sessionIdentity;
        _shuraleProjectionInitialized = false;
        _shuraleWasVisible = false;
        _shuraleInstructionsHeard = false;
        _shuraleEncounterResolved = false;
        _shuraleApproachTriggered = false;
        _shuraleHasDistanceSample = false;
        _shuraleActiveDialogueId = null;
        _shuraleActiveDialogueNodeId = null;
    }

    private void HideShuralePresentation()
    {
        if (_shuraleRoot is { } root && GodotObject.IsInstanceValid(root)) root.Visible = false;
        ResetShuraleGaze();
        _shuraleTarget?.SetPresentationEnabled(false);
        SetCollisionLayer(_shuraleContact, 0u);
        StopShuraleClip();
        _shuraleWasVisible = false;
        _shuraleHasDistanceSample = false;
        _shuraleActiveDialogueId = null;
        _shuraleActiveDialogueNodeId = null;
        SetShuraleWorkBranchVisible(false);
    }

    private void SetShuraleRestPose(bool instructionsHeard, bool encounterResolved)
    {
        if (encounterResolved)
        {
            if (ShouldHoldShuraleYieldForD07())
            {
                if (_accessibility.ReducedMotion) SetShuraleTerminalPose("yield");
                else PlayShuraleClip("yield", ShuraleAnimationPhase.Yielding);
            }
            else if (_accessibility.ReducedMotion) SetShuraleTerminalPose("work");
            else PlayShuraleClip("work", ShuraleAnimationPhase.Resting);
        }
        else if (instructionsHeard)
        {
            SetShuraleTerminalPose("gesture");
        }
        else if (_accessibility.ReducedMotion)
        {
            SetShuraleTerminalPose("work");
        }
        else
        {
            PlayShuraleClip("work", ShuraleAnimationPhase.Resting);
        }
    }

    private void BeginShuraleApproach()
    {
        _shuraleApproachTriggered = true;
        if (_accessibility.ReducedMotion)
        {
            SetShuraleTerminalPose("gesture");
            return;
        }

        PlayShuraleClip("noticed", ShuraleAnimationPhase.Noticing);
    }

    private void PlayShuraleClip(string clip, ShuraleAnimationPhase phase)
    {
        if (_shuraleAnimation is not { } animation || !GodotObject.IsInstanceValid(animation)) return;
        animation.Play(clip, customBlend: 0.18);
        _shuraleCurrentAnimation = clip;
        _shuraleAnimationPhase = phase;
    }

    private void SetShuraleTerminalPose(string clip)
    {
        if (_shuraleAnimation is not { } animation || !GodotObject.IsInstanceValid(animation)) return;
        animation.Stop();
        animation.Play(clip, fromEnd: true);
        animation.Advance(0.0);
        animation.Pause();
        _shuraleCurrentAnimation = clip;
        _shuraleAnimationPhase = ShuraleAnimationPhase.Resting;
    }

    private void StopShuraleClip()
    {
        if (_shuraleAnimation is { } animation && GodotObject.IsInstanceValid(animation)) animation.Stop();
        _shuraleCurrentAnimation = null;
        _shuraleAnimationPhase = ShuraleAnimationPhase.Resting;
    }

    private void OnShuraleAnimationFinished(StringName animationName)
    {
        var clip = animationName.ToString();
        if (!string.Equals(clip, _shuraleCurrentAnimation, StringComparison.Ordinal)) return;
        if (_shuraleBridgeIsCurrent() is not { } bridge
            || bridge.CurrentW01BranchProjection() is not { ActiveSceneId: ForestSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, _shuraleSessionIdentity))
        {
            HideShuralePresentation();
            return;
        }

        if (_shuraleAnimationPhase == ShuraleAnimationPhase.Speaking && clip == "speak")
        {
            PauseShuraleCurrentPose();
            return;
        }

        if (_shuraleAnimationPhase == ShuraleAnimationPhase.Yielding && clip == "yield")
        {
            if (ShouldHoldShuraleYieldForD07())
                PauseShuraleCurrentPose();
            else if (_accessibility.ReducedMotion)
                SetShuraleTerminalPose("work");
            else
                PlayShuraleClip("work", ShuraleAnimationPhase.Resting);
            return;
        }

        if (_shuraleAnimationPhase == ShuraleAnimationPhase.Noticing && clip == "noticed")
        {
            if (bridge.PhotoWorldFactObserved(ShuraleInstructionsFactId))
            {
                _shuraleInstructionsHeard = true;
                SetShuraleTerminalPose("gesture");
            }
            else if (_shuraleEncounterResolved || bridge.PhotoWorldFactObserved(ShuraleEncounterFactId))
            {
                _shuraleEncounterResolved = true;
                SetShuraleRestPose(_shuraleInstructionsHeard, encounterResolved: true);
            }
            else
            {
                PlayShuraleClip("gesture", ShuraleAnimationPhase.Gesturing);
            }
            return;
        }

        if (_shuraleAnimationPhase == ShuraleAnimationPhase.Gesturing && clip == "gesture")
        {
            PauseShuraleCurrentPose();
        }
    }

    private RuntimeBridge? _shuraleBridgeIsCurrent()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is null || !ReferenceEquals(bridge.SessionIdentity, _shuraleSessionIdentity))
            return null;
        return bridge;
    }

    private void PauseShuraleCurrentPose()
    {
        if (_shuraleAnimation is { } animation && GodotObject.IsInstanceValid(animation)) animation.Pause();
        _shuraleAnimationPhase = ShuraleAnimationPhase.Resting;
    }

    private bool IsShuraleInteractionGateOpen()
    {
        var bridge = _bridge;
        var projection = bridge is { } currentBridge && GodotObject.IsInstanceValid(currentBridge)
            ? currentBridge.CurrentW01BranchProjection()
            : null;
        return _shuraleRoot is { Visible: true } root && GodotObject.IsInstanceValid(root)
            && projection is { ActiveSceneId: ForestSceneId } current
            && ReferenceEquals(current.SessionIdentity, _shuraleSessionIdentity)
            && !ShuraleModalOpen()
            && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player
            && GodotObject.IsInstanceValid(player)
            && !player.VehicleControlled && !player.IsClimbingLadder;
    }

    private bool ShuraleModalOpen() =>
        MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: true };

    private void ProcessShuraleGaze(double delta, Node3D root)
    {
        if (_shuraleGazeTarget is not { } target || !GodotObject.IsInstanceValid(target)) return;
        var attentiveIdle = _shuraleInstructionsHeard && !_shuraleEncounterResolved
            && _shuraleActiveDialogueId is null && _shuraleCurrentAnimation == "gesture"
            && _shuraleAnimationPhase == ShuraleAnimationPhase.Resting;
        var gazeState = _shuraleCurrentAnimation is "noticed" or "speak" || attentiveIdle;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var inRange = player is not null && GodotObject.IsInstanceValid(player)
            && new Vector2(player.GlobalPosition.X - root.GlobalPosition.X,
                player.GlobalPosition.Z - root.GlobalPosition.Z).Length() <= ShuraleNoticeDistance;
        var gazeActive = !_accessibility.ReducedMotion && gazeState && inRange;
        var focus = gazeActive
            ? player!.GlobalPosition + Vector3.Up * 1.55f
            : root.GlobalPosition + Vector3.Up * 1.9f + root.GlobalBasis * Vector3.Forward * 2f;
        target.GlobalPosition = target.GlobalPosition.Lerp(focus,
            1f - Mathf.Exp(-8f * Mathf.Max(0f, (float)delta)));

        var rate = Mathf.Max(0f, (float)delta) * 3.5f;
        if (_shuraleHeadGaze is { } head && GodotObject.IsInstanceValid(head))
        {
            if (gazeActive) head.Active = true;
            head.Influence = Mathf.MoveToward(head.Influence,
                gazeActive ? ShuraleHeadGazeInfluence : 0f, rate);
            if (!gazeActive && head.Influence <= 0f) head.Active = false;
        }
        if (_shuraleNeckGaze is { } neck && GodotObject.IsInstanceValid(neck))
        {
            if (gazeActive) neck.Active = true;
            neck.Influence = Mathf.MoveToward(neck.Influence,
                gazeActive ? ShuraleNeckGazeInfluence : 0f, rate);
            if (!gazeActive && neck.Influence <= 0f) neck.Active = false;
        }
    }

    private void ResetShuraleGaze()
    {
        if (_shuraleHeadGaze is { } head && GodotObject.IsInstanceValid(head))
        {
            head.Influence = 0f;
            head.Active = false;
        }
        if (_shuraleNeckGaze is { } neck && GodotObject.IsInstanceValid(neck))
        {
            neck.Influence = 0f;
            neck.Active = false;
        }
        if (_shuraleGazeTarget is { } target && GodotObject.IsInstanceValid(target)
            && target.IsInsideTree()
            && _shuraleRoot is { } root && GodotObject.IsInstanceValid(root) && root.IsInsideTree())
            target.GlobalPosition = root.GlobalPosition + Vector3.Up * 1.9f
                + root.GlobalBasis * Vector3.Forward * 2f;
    }

    private static int FindShuraleBone(Skeleton3D skeleton, string v6Name, string meshyName)
    {
        var index = skeleton.FindBone(v6Name);
        return index >= 0 ? index : skeleton.FindBone(meshyName);
    }

    private static LookAtModifier3D AddShuraleLookModifier(
        Node3D actorRoot, Skeleton3D skeleton, int bone, Node3D target,
        string name, float yawLimitDegrees, float pitchLimitDegrees)
    {
        var restBasis = (skeleton.GlobalBasis * skeleton.GetBoneGlobalRest(bone).Basis).Orthonormalized();
        var facing = (actorRoot.GlobalBasis * Vector3.Forward).Normalized();
        var axes = new[]
        {
            (SkeletonModifier3D.BoneAxis.PlusX, restBasis.X),
            (SkeletonModifier3D.BoneAxis.MinusX, -restBasis.X),
            (SkeletonModifier3D.BoneAxis.PlusY, restBasis.Y),
            (SkeletonModifier3D.BoneAxis.MinusY, -restBasis.Y),
            (SkeletonModifier3D.BoneAxis.PlusZ, restBasis.Z),
            (SkeletonModifier3D.BoneAxis.MinusZ, -restBasis.Z)
        };
        var forwardAxis = axes.MaxBy(axis => axis.Item2.Normalized().Dot(facing)).Item1;
        var forwardLocalAxis = forwardAxis switch
        {
            SkeletonModifier3D.BoneAxis.PlusX or SkeletonModifier3D.BoneAxis.MinusX => Vector3.Axis.X,
            SkeletonModifier3D.BoneAxis.PlusY or SkeletonModifier3D.BoneAxis.MinusY => Vector3.Axis.Y,
            _ => Vector3.Axis.Z
        };
        var actorUp = (actorRoot.GlobalBasis * Vector3.Up).Normalized();
        var primaryAxis = Vector3.Axis.X;
        var primaryAlignment = -1f;
        for (var axisIndex = 0; axisIndex < 3; axisIndex++)
        {
            var candidateAxis = (Vector3.Axis)axisIndex;
            if (candidateAxis == forwardLocalAxis) continue;
            var restAxis = candidateAxis switch
            {
                Vector3.Axis.X => restBasis.X,
                Vector3.Axis.Y => restBasis.Y,
                _ => restBasis.Z
            };
            var alignment = Mathf.Abs(restAxis.Normalized().Dot(actorUp));
            if (alignment <= primaryAlignment) continue;
            primaryAlignment = alignment;
            primaryAxis = candidateAxis;
        }
        var modifier = new LookAtModifier3D
        {
            Name = name,
            BoneName = skeleton.GetBoneName(bone).ToString(),
            ForwardAxis = forwardAxis,
            PrimaryRotationAxis = primaryAxis,
            UseSecondaryRotation = true,
            UseAngleLimitation = true,
            SymmetryLimitation = true,
            PrimaryLimitAngle = Mathf.DegToRad(yawLimitDegrees),
            SecondaryLimitAngle = Mathf.DegToRad(pitchLimitDegrees),
            Duration = .24f,
            TransitionType = Tween.TransitionType.Sine,
            EaseType = Tween.EaseType.InOut,
            Relative = true,
            Influence = 0f
        };
        modifier.Active = false;
        skeleton.AddChild(modifier);
        modifier.TargetNode = modifier.GetPathTo(target);
        return modifier;
    }

    private void SetShuraleWorkBranchVisible(bool visible)
    {
        if (_shuraleWorkBranch is { } branch && GodotObject.IsInstanceValid(branch))
            branch.Visible = visible;
    }
}
