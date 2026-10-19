using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Urman.Experiments.AgentBAct1;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>Scene-owned W01 route ground with authored prologue and forest actions.</summary>
public partial class W01Controller : Node3D, IAccessibilitySettingsTarget
{
    private const string GroundScenePath = "res://assets/photoworlds/forest_ground/prologue-ground-f00-f16-v1.glb";
    private const string HeroRootTurnScenePath = "res://assets/photoworlds/hero_root/hero-root-turn-v1.glb";
    private const string F14OldRootSupportScenePath = "res://assets/photoworlds/hero_root/f14-old-root-support-v1.glb";
    private const string BranchScenePath = "res://assets/photoworlds/branch/prologue-branch-v1.glb";
    private const string PhotoTableauScenePath = "res://assets/photoworlds/forest_entry/pforest-tableau-v1.glb";
    private const string FoliageKitScenePath = "res://assets/models/agent_b_act1/agentb_foliage_kit.glb";
    private const string ProloguePhotoTexturePath = "res://assets/textures/photobook/p-forest-front-v2.png";
    private const string PrologueSceneId = "urman.fullgame:scene/pw-prologue";
    private const string ForestSceneId = "urman.fullgame:scene/pw-forest";
    private const string ForestReturnInteractionId = "urman.fullgame:interaction/pw-forest-return";
    private const string ProloguePhotoSourceInteractionId = "urman.fullgame:interaction/pw-prologue-photo-source";
    private const string PrologueBranchInteractionId = "urman.fullgame:interaction/pw-prologue-branch-move";
    private const string PrologueEndInteractionId = "urman.fullgame:interaction/pw-prologue-f03-end";
    private const string PrologueSkipInteractionId = "urman.fullgame:interaction/pw-prologue-skip";
    private const string ForestBranchInteractionId = "urman.fullgame:interaction/pw-forest-branch-move";
    private const string ForestCameraBackInteractionId = "urman.fullgame:interaction/pw-forest-group";
    private const string ForestCameraBackFactId = "forest_camera_back_seen";
    private const string ProloguePhotoSourceLabelTextId = "urman.fullgame:text/pw-label-prologue-photo-source";
    private const string PrologueEndLabelTextId = "urman.fullgame:text/pw-label-prologue-f03-end";
    private const string PrologueSkipLabelTextId = "urman.fullgame:text/pw-label-prologue-skip";
    private const string ForestCameraBackTextId = "urman.fullgame:text/pw-observation-forest-camera-back";
    private const string BranchMoveAnimationName = "lift_and_place";
    private const double ForestObservationPollSeconds = 0.2;
    private const float RootPassageCenterHalfWidth = 0.72f;
    private const float RootPassageGroundTolerance = 0.32f;
    private const float RootPassagePlaneHalfDepth = 1.15f;
    private const float RootPassageExitSweepDistance = 0.36f;
    private const float RootPassagePlaneSideEpsilon = 0.05f;
    private const string FreshSnowTexturePath = "res://assets/textures/painterly/snow_fresh_v1_albedo.png";
    private const string TrampledSnowTexturePath = "res://assets/textures/painterly/snow_trampled_v2_albedo.png";
    private const string ForestRootsTexturePath = "res://assets/photoworlds/forest_ground/w01_forest_roots_1k.png";
    private const string ForestLeavesTexturePath = "res://assets/photoworlds/forest_ground/w01_forest_leaves_1k.png";
    private const string PrologueGroundShaderCode = """
        shader_type spatial;
        render_mode diffuse_burley, specular_schlick_ggx;

        uniform sampler2D fresh_snow : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform sampler2D trampled_snow : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform sampler2D forest_roots : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform sampler2D forest_leaves : source_color, filter_linear_mipmap_anisotropic, repeat_enable;

        void fragment() {
            // ForestMasks is exported as COLOR_0; R is the packed walking track.
            // Keep the authored 2.1 m packed core; lower mask coverage belongs
            // to soft snowy shoulders, not another full-strength lane.
            float track = smoothstep(0.72, 1.0, COLOR.r);
            // Packed snow records the narrow walked route; the surrounding snow
            // stays pale. This is authored vertex coverage, not a new path mesh.
            vec3 fresh = texture(fresh_snow, UV2).rgb * vec3(0.94, 0.97, 0.91);
            vec3 packed = texture(trampled_snow, UV2).rgb * vec3(0.68, 0.75, 0.72);
            vec3 snow = mix(fresh, packed, track * 0.94);
            float root_cover = clamp(COLOR.b * 0.48, 0.0, 0.38) * (1.0 - track);
            float leaf_cover = clamp(COLOR.g * 0.30, 0.0, 0.24) * (1.0 - track);
            vec3 roots = texture(forest_roots, UV2).rgb * vec3(0.84, 0.83, 0.76);
            vec3 leaves = texture(forest_leaves, UV2).rgb * vec3(0.90, 0.83, 0.69);
            snow = mix(snow, roots, root_cover);
            snow = mix(snow, leaves, leaf_cover);
            ALBEDO = snow * mix(1.0, 0.88, COLOR.a);
            ROUGHNESS = mix(0.94, 0.86, track);
            SPECULAR = 0.16;
        }
        """;
    private static readonly Vector3 BranchF01 = new(12f, 1f, 50f);
    private static readonly Vector3 PrologueF02 = new(45f, 3f, 95f);
    private static readonly Vector3 PrologueF03 = new(80f, 4f, 140f);
    private static readonly Vector3 BranchMovedRootPosition = new(1.4f, 0.14f, 0f);
    // Hints follow authored landmark centerlines; startup resolves them onto each mesh's triangles.
    private static readonly Vector3 ForestSplitBirchObservationHint = new(4.25f, 2.2f, 46.1f);
    private static readonly Vector3 ForestCrossbarObservationHint = new(3.6f, 1.15f, 45.975f);
    private static readonly Vector3 ForestRootArchObservationHint = new(-6.2f, 1.85f, 46f);
    private static readonly float BranchAcrossPathYaw = Mathf.RadToDeg(Mathf.Atan2(12f, 50f));
    private static readonly Vector3[] PrologueRouteNodes = { Vector3.Zero, BranchF01, PrologueF02, PrologueF03 };
    private static readonly Vector2 MainSpawnRouteF10 = new(0f, 35f);
    private static readonly Vector2 MainSpawnRouteF11 = new(14f, 48f);
    private static readonly Vector2[] ForestExitRouteNodes =
    {
        new(112f, 148f),
        new(132f, 169f),
        new(149f, 190f)
    };
    private static readonly (string Variant, Vector2 Position, float YawDegrees)[] PrologueForestTrees =
    {
        ("WinterBirch_2", new(-7f, 13f), 21f),
        ("WinterSpruce_3", new(13f, 7f), 145f),
        ("WinterLinden_2", new(-6f, 27f), 346f),
        ("WinterSpruce_4", new(18f, 20f), 63f),
        ("WinterSpruce_3", new(-4f, 39f), 229f),
        ("WinterBirch_2", new(21f, 32f), 190f),
        ("WinterSpruce_4", new(-7f, 48f), 127f),
        ("WinterBirch_2", new(30f, 41f), 311f),
        ("WinterSpruce_5", new(-7f, 65f), 26f),
        ("WinterBirch_2", new(8f, 70f), 88f),
        ("WinterLinden_2", new(37f, 58f), 201f),
        ("WinterSpruce_4", new(47f, 72f), 36f),
        ("WinterSpruce_3", new(12f, 82f), 117f),
        ("WinterBirch_2", new(24f, 93f), 290f),
        ("WinterSpruce_5", new(67f, 93f), 173f),
        ("WinterSpruce_4", new(86f, 93f), 52f),
        ("WinterSpruce_6", new(-8f, 100f), 323f),
        ("WinterBirch_2", new(31f, 112f), 34f),
        ("WinterSpruce_4", new(77f, 111f), 257f),
        ("WinterLinden_2", new(43f, 121f), 185f),
        ("WinterSpruce_5", new(85f, 119f), 103f),
        ("WinterBirch_2", new(54f, 133f), 336f),
        ("WinterSpruce_4", new(60f, 149f), 72f),
        ("WinterSpruce_5", new(10f, 145f), 229f),
        ("WinterBirch_2", new(22f, 134f), 13f),
        ("WinterSpruce_6", new(-8f, 132f), 149f),
        ("WinterBirch_2", new(88f, 105f), 298f),
        ("WinterLinden_2", new(-8f, 75f), 271f),
        ("WinterSpruce_6", new(75f, 81f), 9f),
        ("WinterSpruce_5", new(-8f, 88f), 86f)
    };
    private static readonly (Vector2 Position, float YawDegrees)[] PrologueForestCorridorPines =
    {
        (new(20.7f, 41.3f), 34f),
        (new(27.3f, 53.1f), 211f),
        (new(38.0f, 67.6f), 128f),
        (new(21.0f, 80.1f), 303f),
        (new(48.6f, 82.2f), 17f),
        (new(59.3f, 96.3f), 194f),
        (new(42.7f, 109.2f), 79f),
        (new(53.8f, 123.4f), 256f),
        (new(64.8f, 137.6f), 143f)
    };
    private static readonly (string Variant, Vector2 Position, float YawDegrees)[] PrologueForestMidPines =
    {
        ("WinterSpruce_5", new(-11f, 18f), 19f),
        ("WinterSpruce_4", new(16f, 26f), 241f),
        ("WinterSpruce_6", new(-13f, 35f), 327f),
        ("WinterSpruce_3", new(23f, 50f), 97f),
        ("WinterSpruce_5", new(-10f, 59f), 164f),
        ("WinterSpruce_4", new(32f, 68f), 18f),
        ("WinterSpruce_6", new(7f, 74f), 286f),
        ("WinterSpruce_3", new(54f, 72f), 133f),
        ("WinterSpruce_5", new(20f, 88f), 54f),
        ("WinterSpruce_4", new(63f, 89f), 221f),
        ("WinterSpruce_6", new(24f, 105f), 349f),
        ("WinterSpruce_3", new(72f, 103f), 74f),
        ("WinterSpruce_5", new(38f, 125f), 198f),
        ("WinterSpruce_4", new(84f, 129f), 22f),
        ("WinterSpruce_6", new(16f, 108f), 271f),
        ("WinterSpruce_3", new(69f, 138f), 112f)
    };

    private RuntimeBridge? _bridge;
    private Node3D? _branchVisual;
    private Node3D? _movingRoot;
    private Node3D? _gripCenter;
    private AnimationPlayer? _branchAnimation;
    private Node3D? _groundVisual;
    private Node3D? _forestVisual;
    private Node3D? _photoTableauVisual;
    private MeshInstance3D? _prologuePhotoCard;
    private InteractionTarget? _prologuePhotoTarget;
    private StaticBody3D? _photoTableauBody;
    private CollisionShape3D? _forestSplitBirchContact;
    private CollisionShape3D? _forestCrossbarContact;
    private CollisionShape3D? _forestRootArchContact;
    private Vector3 _forestSplitBirchObservationPoint;
    private Vector3 _forestCrossbarObservationPoint;
    private Vector3 _forestRootArchObservationPoint;
    private readonly global::Godot.Collections.Array<Rid> _forestObservationRayExclusions = new();
    private double _forestObservationPollElapsed;
    private object? _forestObservationFailedSession;
    private Vector3 _forestObservationFailedPosition;
    private Vector3 _forestObservationFailedForward;
    private bool _forestObservationDispatching;
    private StaticBody3D? _groundBody;
    private StaticBody3D? _leftRootBody;
    private StaticBody3D? _rightRootBody;
    private StaticBody3D? _movingBranchBody;
    private Node3D? _heroRootAtF12;
    private Node3D? _heroRootAtF13;
    private StaticBody3D? _heroRootContactAtF12;
    private StaticBody3D? _heroRootContactAtF13;
    private Node3D? _f14OldRootSupportVisual;
    private StaticBody3D? _f14OldRootSupportContact;
    private Marker3D? _rootLinkFrameF12;
    private Marker3D? _rootLinkFrameF13;
    private readonly KinematicCollision3D _rootLinkExitSweep = new();
    private bool _rootPassageEnabled;
    private object? _rootPassageSessionIdentity;
    private int _rootLinkTrackedPortal = -1;
    private int _rootLinkCooldownPortal = -1;
    private float _rootLinkPreviousSignedDistance;
    private int _rootLinkPreviousPlaneSide;
    private Marker3D? _forestExitFrame;
    private object? _forestExitSessionIdentity;
    private int _forestExitPreviousPlaneSide;
    private bool _forestExitDispatching;
    private InteractionTarget? _branchTarget;
    private InteractionTarget? _prologueEndTarget;
    private InteractionTarget? _prologueSkipTarget;
    private string? _branchMoveInteractionId;
    private string? _branchMoveSceneId;
    private object? _branchMoveSession;
    private bool _branchMoveInProgress;
    private Button? _prologueSkipButton;
    private Viewport? _prologueUiViewport;
    private Label? _prologueHintAccessLabel;
    private string? _prologueHintQuestion;
    private bool _prologueHintAvailable;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public override void _Ready()
    {
        AddToGroup(AccessibilityPresentation.TargetGroup);
        BuildGround();
        BuildRootPassage();
        BuildF14OldRootSupport();
        BuildForest();
        BuildBranch();
        BuildPhotoTableau();
        BuildProloguePhotoSource();
        BuildPrologueUi();
        BuildBagPresentation();
        BuildShuralePresentation();
        _forestExitFrame = GetNode<Marker3D>("Anchors/F16");
        SetProcess(true);
        SetPhysicsProcess(false);
        TryBindRuntimeBridge();
        ApplyAccessibilitySettings(
            (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.Accessibility
            ?? AccessibilitySettingsSnapshot.Default);
    }

    public override void _Process(double delta)
    {
        TryBindRuntimeBridge();
        ProcessForestWorldGuard();
        RefreshPrologueSkipButtonState();
        ProcessBagPresentation(delta);
        ProcessShuralePresentation(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        TryTraverseRootPassage();
        TryExitForestAtF16();
        _forestObservationPollElapsed += delta;
        if (_forestObservationPollElapsed < ForestObservationPollSeconds) return;
        _forestObservationPollElapsed = 0;
        TryObserveForestCameraBack();
        TryObserveRootPassageBack();
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        var wasReducedMotion = _accessibility.ReducedMotion;
        _accessibility = settings;
        ApplyShuraleAccessibilitySettings(settings, wasReducedMotion);
        if (_prologueSkipButton is not { } button || !GodotObject.IsInstanceValid(button)) return;
        AccessibilityPresentation.ApplyToControl(button, settings);
        if (_prologueHintAccessLabel is { } hintLabel)
            AccessibilityPresentation.ApplyToControl(hintLabel, settings);
        RefitPrologueSkipButton();
    }

    public override void _ExitTree()
    {
        SetProcess(false);
        SetPhysicsProcess(false);
        ReleaseForestWorldGuard();
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= ApplyProjection;
        if (_branchAnimation is { } animation && GodotObject.IsInstanceValid(animation))
            animation.AnimationFinished -= OnBranchAnimationFinished;
        ExitBagPresentation();
        ExitShuralePresentation();
        _rootLinkExitSweep.Dispose();
        if (_prologueUiViewport is { } viewport && GodotObject.IsInstanceValid(viewport))
            viewport.SizeChanged -= RefitPrologueSkipButton;
        _prologueUiViewport = null;
        _bridge = null;
        _branchTarget = null;
        _prologueEndTarget = null;
        _prologueSkipTarget = null;
        _prologuePhotoTarget = null;
        _prologuePhotoCard = null;
        _prologueSkipButton = null;
        _prologueHintAccessLabel = null;
        _prologueHintQuestion = null;
        _prologueHintAvailable = false;
        _branchAnimation = null;
        _branchMoveInteractionId = null;
        _branchMoveSceneId = null;
        _branchMoveSession = null;
        _branchMoveInProgress = false;
        _movingBranchBody = null;
        _leftRootBody = null;
        _rightRootBody = null;
        _groundBody = null;
        _groundVisual = null;
        _forestVisual = null;
        _photoTableauVisual = null;
        _photoTableauBody = null;
        _forestSplitBirchContact = null;
        _forestCrossbarContact = null;
        _forestRootArchContact = null;
        _forestObservationRayExclusions.Clear();
        _forestObservationFailedSession = null;
        _forestObservationDispatching = false;
        _rootPassageEnabled = false;
        ResetRootPassageTracking(null);
        ResetForestExitTracking(null);
        _forestExitFrame = null;
        _heroRootContactAtF12 = null;
        _heroRootContactAtF13 = null;
        _f14OldRootSupportContact = null;
        _f14OldRootSupportVisual = null;
        _heroRootAtF12 = null;
        _heroRootAtF13 = null;
        _rootLinkFrameF12 = null;
        _rootLinkFrameF13 = null;
        _gripCenter = null;
        _movingRoot = null;
        _branchVisual = null;
    }

    private void BuildGround()
    {
        var groundScene = LoadScene(GroundScenePath);
        _groundVisual = groundScene.Instantiate<Node3D>();
        _groundVisual.Name = "F00F16GroundVisual";
        _groundVisual.Visible = false;
        AddChild(_groundVisual);

        _groundBody = new StaticBody3D
        {
            Name = "F00F16GroundContact",
            CollisionLayer = 0u,
            CollisionMask = 3u
        };
        AddChild(_groundBody);
        var snowMaterial = CreatePrologueGroundMaterial();
        foreach (var mesh in Descendants<MeshInstance3D>(_groundVisual))
        {
            if (mesh.Mesh is null) continue;
            mesh.MaterialOverride = snowMaterial;
            _groundBody.AddChild(Act1ConnectedWorld.AuthoredSurfaceContact(_groundBody, mesh));
        }
    }

    private void BuildRootPassage()
    {
        _rootLinkFrameF12 = GetNode<Marker3D>("Anchors/F12");
        _rootLinkFrameF13 = GetNode<Marker3D>("Anchors/F13");
        var scene = LoadScene(HeroRootTurnScenePath);
        (_heroRootAtF12, _heroRootContactAtF12) = BuildRootPassageCopy(scene, _rootLinkFrameF12, "F12");
        (_heroRootAtF13, _heroRootContactAtF13) = BuildRootPassageCopy(scene, _rootLinkFrameF13, "F13");
        BuildRootPassageObservationSubjects();
    }

    private void BuildF14OldRootSupport()
    {
        var f14 = GetNode<Marker3D>("Anchors/F14");
        var visual = LoadScene(F14OldRootSupportScenePath).Instantiate<Node3D>();
        visual.Name = "F14_OldRootSupport";
        visual.Visible = false;
        f14.AddChild(visual);

        var contact = new StaticBody3D
        {
            Name = "F14_OldRootSupportContact",
            CollisionLayer = 0u,
            CollisionMask = 3u
        };
        visual.AddChild(contact);
        foreach (var mesh in Descendants<MeshInstance3D>(visual).Where(mesh => mesh.Mesh is not null).ToArray())
            contact.AddChild(Act1ConnectedWorld.AuthoredSurfaceContact(contact, mesh));

        _f14OldRootSupportVisual = visual;
        _f14OldRootSupportContact = contact;
    }

    private (Node3D Visual, StaticBody3D Contact) BuildRootPassageCopy(
        PackedScene scene, Marker3D spatialLinkFrame, string endpointName)
    {
        var visual = scene.Instantiate<Node3D>();
        visual.Name = $"A_HERO_ROOT_{endpointName}";
        visual.Visible = false;
        AddChild(visual);

        var localLinkFrame = RequireDescendant<Node3D>(visual, "SpatialLinkFrame");
        var localFrameTransform = visual.GlobalTransform.AffineInverse() * localLinkFrame.GlobalTransform;
        visual.GlobalTransform = spatialLinkFrame.GlobalTransform * localFrameTransform.AffineInverse();

        var contact = new StaticBody3D
        {
            Name = $"A_HERO_ROOT_{endpointName}_Contact",
            CollisionLayer = 0u,
            CollisionMask = 3u
        };
        visual.AddChild(contact);
        foreach (var mesh in Descendants<MeshInstance3D>(visual).Where(mesh => mesh.Mesh is not null).ToArray())
        {
            var collision = new CollisionShape3D
            {
                Name = $"{mesh.Name}_Contact",
                Shape = mesh.Mesh!.CreateTrimeshShape(),
                Transform = contact.GlobalTransform.AffineInverse() * mesh.GlobalTransform
            };
            contact.AddChild(collision);
        }

        return (visual, contact);
    }

    private void ApplyRootPassageProjection(RuntimeBridge.W01BranchProjection? projection)
    {
        var enabled = projection is { ActiveSceneId: ForestSceneId }
            && _bridge is { } bridge && GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(projection.SessionIdentity, bridge.SessionIdentity);
        var sessionIdentity = enabled ? projection!.SessionIdentity : null;
        if (!enabled || !ReferenceEquals(_rootPassageSessionIdentity, sessionIdentity))
            ResetRootPassageTracking(sessionIdentity);
        _rootPassageEnabled = enabled;
        if (_heroRootAtF12 is { } atF12 && GodotObject.IsInstanceValid(atF12)) atF12.Visible = enabled;
        if (_heroRootAtF13 is { } atF13 && GodotObject.IsInstanceValid(atF13)) atF13.Visible = enabled;
        SetCollisionLayer(_heroRootContactAtF12, enabled ? 1u : 0u);
        SetCollisionLayer(_heroRootContactAtF13, enabled ? 1u : 0u);
        if (_f14OldRootSupportVisual is { } support && GodotObject.IsInstanceValid(support))
            support.Visible = enabled;
        SetCollisionLayer(_f14OldRootSupportContact, enabled ? 1u : 0u);
    }

    private void ResetRootPassageTracking(object? sessionIdentity)
    {
        ResetRootPassageObservation();
        _rootPassageSessionIdentity = sessionIdentity;
        _rootLinkTrackedPortal = -1;
        _rootLinkCooldownPortal = -1;
        _rootLinkPreviousSignedDistance = 0f;
        _rootLinkPreviousPlaneSide = 0;
    }

    private void ResetForestExitTracking(object? sessionIdentity)
    {
        _forestExitSessionIdentity = sessionIdentity;
        _forestExitPreviousPlaneSide = 0;
        _forestExitDispatching = false;
    }

    private void TryExitForestAtF16()
    {
        if (_forestExitFrame is not { } exitFrame || !GodotObject.IsInstanceValid(exitFrame)
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player))
        {
            ResetForestExitTracking(null);
            return;
        }

        var projection = bridge.CurrentW01BranchProjection();
        if (projection is null || projection.ActiveSceneId != ForestSceneId
            || !ReferenceEquals(projection.SessionIdentity, session))
        {
            ResetForestExitTracking(null);
            return;
        }

        if (!ReferenceEquals(_forestExitSessionIdentity, session))
        {
            ResetForestExitTracking(session);
            return;
        }

        if (MainMenuUi.AnyUndismissed(GetTree()) || player.ModalOpen || player.VehicleControlled
            || player.IsClimbingLadder || !player.IsOnFloor())
        {
            _forestExitPreviousPlaneSide = 0;
            return;
        }

        var local = exitFrame.GlobalTransform.AffineInverse() * player.GlobalTransform;
        if (Math.Abs(local.Origin.X) > RootPassageCenterHalfWidth
            || Math.Abs(local.Origin.Y) > RootPassageGroundTolerance
            || Math.Abs(local.Origin.Z) > RootPassagePlaneHalfDepth)
        {
            _forestExitPreviousPlaneSide = 0;
            return;
        }

        var previousSide = _forestExitPreviousPlaneSide;
        var currentSide = RootPassagePlaneSide(local.Origin.Z);
        if (currentSide == 0) return;
        _forestExitPreviousPlaneSide = currentSide;
        if (previousSide != -1 || currentSide != 1 || _forestExitDispatching) return;

        _ = DispatchForestReturnAsync(bridge, session);
    }

    private async Task DispatchForestReturnAsync(RuntimeBridge bridge, object session)
    {
        _forestExitDispatching = true;
        var dispatched = false;
        try
        {
            dispatched = await bridge.DispatchInteractionAsync(ForestReturnInteractionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W01 F16 return dispatch failed: {error.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;

        var projection = GodotObject.IsInstanceValid(bridge) ? bridge.CurrentW01BranchProjection() : null;
        if (!GodotObject.IsInstanceValid(bridge) || !ReferenceEquals(bridge.SessionIdentity, session)
            || projection is null || projection.ActiveSceneId != ForestSceneId
            || !ReferenceEquals(projection.SessionIdentity, session))
        {
            ResetForestExitTracking(null);
            return;
        }

        _forestExitDispatching = dispatched;
    }

    private void TryTraverseRootPassage()
    {
        if (!_rootPassageEnabled
            || _rootLinkFrameF12 is not { } f12 || !GodotObject.IsInstanceValid(f12)
            || _rootLinkFrameF13 is not { } f13 || !GodotObject.IsInstanceValid(f13)
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player))
        {
            ResetRootPassageTracking(null);
            return;
        }

        if (!ReferenceEquals(_rootPassageSessionIdentity, session))
        {
            ResetRootPassageTracking(session);
            return;
        }

        var projection = bridge.CurrentW01BranchProjection();
        if (projection is null || projection.ActiveSceneId != ForestSceneId
            || !ReferenceEquals(projection.SessionIdentity, session)
            || MainMenuUi.AnyUndismissed(GetTree())
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor())
        {
            _rootLinkTrackedPortal = -1;
            _rootLinkPreviousPlaneSide = 0;
            return;
        }

        var f12Local = f12.GlobalTransform.AffineInverse() * player.GlobalTransform;
        var f13Local = f13.GlobalTransform.AffineInverse() * player.GlobalTransform;
        if (_rootLinkCooldownPortal == 0 && IsInsideRootPassageLinkVolume(f12Local)) return;
        if (_rootLinkCooldownPortal == 1 && IsInsideRootPassageLinkVolume(f13Local)) return;
        if (_rootLinkCooldownPortal >= 0)
        {
            _rootLinkCooldownPortal = -1;
            _rootLinkTrackedPortal = -1;
        }

        var portal = -1;
        var localTransform = Transform3D.Identity;
        if (IsInsideRootPassageLinkVolume(f12Local))
        {
            portal = 0;
            localTransform = f12Local;
        }
        else if (IsInsideRootPassageLinkVolume(f13Local))
        {
            portal = 1;
            localTransform = f13Local;
        }

        if (portal < 0)
        {
            _rootLinkTrackedPortal = -1;
            _rootLinkPreviousPlaneSide = 0;
            return;
        }

        var signedDistance = localTransform.Origin.Z;
        if (_rootLinkTrackedPortal != portal)
        {
            _rootLinkTrackedPortal = portal;
            _rootLinkPreviousSignedDistance = signedDistance;
            _rootLinkPreviousPlaneSide = RootPassagePlaneSide(signedDistance);
            return;
        }

        var previousDistance = _rootLinkPreviousSignedDistance;
        _rootLinkPreviousSignedDistance = signedDistance;
        var previousSide = _rootLinkPreviousPlaneSide;
        var currentSide = RootPassagePlaneSide(signedDistance);
        if (currentSide == 0) return;

        _rootLinkPreviousPlaneSide = currentSide;
        var crossedLinkPlane = previousSide != 0 && previousSide != currentSide;
        if (!crossedLinkPlane) return;

        var crossingDirection = signedDistance > previousDistance ? 1f : -1f;

        var sourceFrame = portal == 0 ? f12 : f13;
        var destinationFrame = portal == 0 ? f13 : f12;
        var destinationTransform = destinationFrame.GlobalTransform * localTransform;
        var destinationMotion = destinationFrame.GlobalBasis.Z.Normalized()
            * crossingDirection * RootPassageExitSweepDistance;
        if (player.TestMove(destinationTransform, destinationMotion, _rootLinkExitSweep, .001f, false, 4))
            return;

        var relativeVelocity = sourceFrame.GlobalBasis.Orthonormalized().Inverse() * player.Velocity;
        player.ApplyTraversalTransform(destinationTransform,
            destinationFrame.GlobalBasis.Orthonormalized() * relativeVelocity);
        _rootLinkCooldownPortal = 1 - portal;
        _rootLinkTrackedPortal = 1 - portal;
        _rootLinkPreviousSignedDistance = signedDistance;
        _rootLinkPreviousPlaneSide = currentSide;
        ArmRootPassageObservation(portal == 0 ? crossingDirection : 0f, player);
    }

    private static int RootPassagePlaneSide(float signedDistance)
    {
        if (signedDistance > RootPassagePlaneSideEpsilon) return 1;
        if (signedDistance < -RootPassagePlaneSideEpsilon) return -1;
        return 0;
    }

    private static bool IsInsideRootPassageLinkVolume(Transform3D localTransform)
    {
        var point = localTransform.Origin;
        return Math.Abs(point.X) <= RootPassageCenterHalfWidth
            && Math.Abs(point.Y) <= RootPassageGroundTolerance
            && Math.Abs(point.Z) <= RootPassagePlaneHalfDepth;
    }

    private void BuildForest()
    {
        _forestVisual = new Node3D { Name = "F00F03ForestVisual", Visible = false };
        _forestVisual.SetMeta("presentationOnly", true);
        _forestVisual.SetMeta("sourceAssets", "urman.act1.agentb.foliage_kit; winter.pine.v1");
        _forestVisual.SetMeta("routeClearance", "2.1 m standing lane retained from measured lower-2m triangles for every pine tier; presentation only");
        AddChild(_forestVisual);

        var pineProvider = new AgentBAct1ExteriorLayer();
        ArrayMesh pineMesh;
        ArrayMesh lightPineMesh;
        ArrayMesh farPineMesh;
        try
        {
            pineMesh = pineProvider.FoliageMesh("WinterPine", "kara");
            lightPineMesh = pineProvider.FoliageMesh("WinterLightPine", "kara");
            farPineMesh = pineProvider.FoliageMesh("WinterFarPine", "kara");
        }
        finally
        {
            pineProvider.Free();
        }

        var pineSourceBounds = pineMesh.GetAabb();
        var lightPineSourceBounds = lightPineMesh.GetAabb();
        var farPineSourceBounds = farPineMesh.GetAabb();
        if (pineSourceBounds.Size.Y <= 0f || lightPineSourceBounds.Size.Y <= 0f
            || farPineSourceBounds.Size.Y <= 0f)
            throw new InvalidOperationException("Registered winter pine has no normalized height.");

        var kit = AgentBKitMaterials.InstantiateKit(this, FoliageKitScenePath, "W01FoliageTemplateSource");
        kit.Visible = false;
        var roots = Descendants<Node3D>(kit)
            .Where(node => node.Name.ToString().EndsWith("_Root", StringComparison.Ordinal))
            .ToDictionary(node => node.Name.ToString(), StringComparer.Ordinal);
        var groundMesh = Descendants<MeshInstance3D>(_groundVisual!)
            .FirstOrDefault(node => node.Mesh is not null)
            ?? throw new InvalidOperationException("W01 prologue ground mesh is missing.");
        var groundToForest = _forestVisual.GlobalTransform.AffineInverse() * groundMesh.GlobalTransform;
        var groundBounds = groundToForest * groundMesh.Mesh!.GetAabb();
        var placedTreeRoots = new List<Vector2>();

        // Clip imported triangles at capsule height so low branches count, but crowns do not widen the walk lane.
        float MeasurePineStandingFootprintRadius(
            ArrayMesh sourceMesh, Aabb rootScaleBounds, float minimumTreeHeight, float standingHeight)
        {
            var localTop = rootScaleBounds.Position.Y
                + rootScaleBounds.Size.Y * standingHeight / minimumTreeHeight;
            var maxRadiusSquared = 0f;

            void IncludeBelow(Vector3 point)
            {
                if (point.Y <= localTop)
                    maxRadiusSquared = Mathf.Max(maxRadiusSquared, point.X * point.X + point.Z * point.Z);
            }

            void IncludeCrossing(Vector3 first, Vector3 second)
            {
                var firstBelow = first.Y <= localTop;
                var secondBelow = second.Y <= localTop;
                if (firstBelow == secondBelow) return;
                var t = (localTop - first.Y) / (second.Y - first.Y);
                var point = first.Lerp(second, t);
                maxRadiusSquared = Mathf.Max(maxRadiusSquared, point.X * point.X + point.Z * point.Z);
            }

            void IncludeTriangle(Vector3 first, Vector3 second, Vector3 third)
            {
                IncludeBelow(first);
                IncludeBelow(second);
                IncludeBelow(third);
                IncludeCrossing(first, second);
                IncludeCrossing(second, third);
                IncludeCrossing(third, first);
            }

            for (var surfaceIndex = 0; surfaceIndex < sourceMesh.GetSurfaceCount(); surfaceIndex++)
            {
                using var arrays = sourceMesh.SurfaceGetArrays(surfaceIndex);
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var indexData = arrays[(int)Mesh.ArrayType.Index];
                var indices = indexData.VariantType == Variant.Type.PackedInt32Array
                    ? indexData.AsInt32Array()
                    : Array.Empty<int>();
                if (indices.Length > 0)
                {
                    if (indices.Length % 3 != 0)
                        throw new InvalidOperationException("Generated pine surface is not a triangle list.");
                    for (var index = 0; index < indices.Length; index += 3)
                        IncludeTriangle(vertices[indices[index]], vertices[indices[index + 1]], vertices[indices[index + 2]]);
                }
                else
                {
                    if (vertices.Length % 3 != 0)
                        throw new InvalidOperationException("Generated pine surface has an incomplete triangle.");
                    for (var index = 0; index < vertices.Length; index += 3)
                        IncludeTriangle(vertices[index], vertices[index + 1], vertices[index + 2]);
                }
            }

            if (maxRadiusSquared <= 0f)
                throw new InvalidOperationException("Generated pine has no geometry in the standing-height band.");
            return Mathf.Sqrt(maxRadiusSquared);
        }

        const float minimumPineHeight = 9f;
        const float minimumClusterPineHeight = 12f;
        const float standingLaneHalfWidth = 1.05f;
        var pineStandingFootprintRadius = MeasurePineStandingFootprintRadius(
            pineMesh, pineSourceBounds, minimumPineHeight, 2f);
        var clusteredPineStandingFootprintRadius = Mathf.Max(
            MeasurePineStandingFootprintRadius(lightPineMesh, pineSourceBounds, minimumClusterPineHeight, 2f),
            MeasurePineStandingFootprintRadius(farPineMesh, pineSourceBounds, minimumClusterPineHeight, 2f));

        float RouteDistance(Vector2 point)
        {
            var nearestSquared = float.MaxValue;
            for (var routeIndex = 1; routeIndex < PrologueRouteNodes.Length; routeIndex++)
            {
                var first = PrologueRouteNodes[routeIndex - 1];
                var second = PrologueRouteNodes[routeIndex];
                var start = new Vector2(first.X, first.Z);
                var segment = new Vector2(second.X - first.X, second.Z - first.Z);
                var t = Mathf.Clamp((point - start).Dot(segment) / segment.LengthSquared(), 0f, 1f);
                nearestSquared = Mathf.Min(nearestSquared, point.DistanceSquaredTo(start + segment * t));
            }

            var mainSpawnSegment = MainSpawnRouteF11 - MainSpawnRouteF10;
            var mainSpawnT = Mathf.Clamp((point - MainSpawnRouteF10).Dot(mainSpawnSegment)
                / mainSpawnSegment.LengthSquared(), 0f, 1f);
            nearestSquared = Mathf.Min(nearestSquared,
                point.DistanceSquaredTo(MainSpawnRouteF10 + mainSpawnSegment * mainSpawnT));
            for (var routeIndex = 1; routeIndex < ForestExitRouteNodes.Length; routeIndex++)
            {
                var start = ForestExitRouteNodes[routeIndex - 1];
                var segment = ForestExitRouteNodes[routeIndex] - start;
                var t = Mathf.Clamp((point - start).Dot(segment) / segment.LengthSquared(), 0f, 1f);
                nearestSquared = Mathf.Min(nearestSquared,
                    point.DistanceSquaredTo(start + segment * t));
            }

            return Mathf.Sqrt(nearestSquared);
        }

        bool NearExistingTree(Vector2 position, float distance = 6f)
        {
            var limit = distance * distance;
            return placedTreeRoots.Any(existing => existing.DistanceSquaredTo(position) < limit);
        }

        void AddTree(string variant, Vector2 position, float yawDegrees, int index, string layer)
        {
            var groundY = ForestGroundHeight(position.X, position.Y);
            if (position.X < groundBounds.Position.X || position.X > groundBounds.End.X
                || position.Y < groundBounds.Position.Z || position.Y > groundBounds.End.Z)
                throw new InvalidOperationException($"W01 forest root is outside ground bounds: {position}.");

            if (variant.StartsWith("WinterSpruce_", StringComparison.Ordinal) || variant == "WinterPine")
            {
                var variationColumn = index;
                var variationRow = string.Equals(layer, "near", StringComparison.Ordinal) ? 0 : 1;
                var targetHeight = Mathf.Lerp(
                    string.Equals(layer, "near", StringComparison.Ordinal) ? 12f : minimumPineHeight,
                    string.Equals(layer, "near", StringComparison.Ordinal) ? 19f : 15f,
                    CoordinateHash(variationColumn, variationRow, 0xD1B54A35u) / (float)uint.MaxValue);
                var girthScale = Mathf.Lerp(0.76f, 0.91f,
                    CoordinateHash(variationColumn, variationRow, 0x94D049BBu) / (float)uint.MaxValue);
                var yawOffset = (CoordinateHash(variationColumn, variationRow, 0x369DEA0Fu)
                    / (float)uint.MaxValue - 0.5f) * 32f;
                var heightScale = targetHeight / pineSourceBounds.Size.Y;
                var horizontalScale = heightScale * girthScale;
                var pine = new MeshInstance3D
                {
                    Name = $"PrologueForest_{layer}_{variant}_{index + 1:00}",
                    Mesh = pineMesh,
                    Position = new Vector3(position.X, groundY - pineSourceBounds.Position.Y * heightScale, position.Y),
                    RotationDegrees = new Vector3(0f, yawDegrees + yawOffset, 0f),
                    Scale = new Vector3(horizontalScale, heightScale, horizontalScale)
                };
                pine.SetMeta("presentationOnly", true);
                pine.SetMeta("sourceVariant", "WinterPine");
                pine.SetMeta("sourceAsset", "winter.pine.v1");
                if (variant != "WinterPine") pine.SetMeta("replacesVariant", variant);
                pine.SetMeta("forestLayer", layer);

                var standingFootprintRadius = pineStandingFootprintRadius * horizontalScale;
                if (RouteDistance(position) < standingFootprintRadius + standingLaneHalfWidth)
                    throw new InvalidOperationException($"W01 pine violates the 2.1 m route clearance: {position}.");

                _forestVisual.AddChild(pine);
                placedTreeRoots.Add(position);
                return;
            }

            var rootName = variant + "_Root";
            if (!roots.TryGetValue(rootName, out var template))
                throw new InvalidOperationException($"Registered foliage kit is missing native root: {rootName}");

            var tree = (Node3D)template.Duplicate();
            tree.Name = $"PrologueForest_{layer}_{variant}_{index + 1:00}";
            tree.Position = new Vector3(position.X, groundY, position.Y);
            tree.RotationDegrees = new Vector3(0f, yawDegrees, 0f);
            tree.SetMeta("presentationOnly", true);
            tree.SetMeta("sourceVariant", variant);
            tree.SetMeta("forestLayer", layer);
            _forestVisual.AddChild(tree);
            placedTreeRoots.Add(position);
        }

        for (var index = 0; index < PrologueForestTrees.Length; index++)
        {
            var placement = PrologueForestTrees[index];
            AddTree(placement.Variant, placement.Position, placement.YawDegrees, index, "near");
        }

        var forestTreeIndex = PrologueForestTrees.Length;
        foreach (var placement in PrologueForestMidPines)
        {
            if (NearExistingTree(placement.Position)) continue;
            AddTree(placement.Variant, placement.Position, placement.YawDegrees, forestTreeIndex++, "mid");
        }

        foreach (var (position, yawDegrees) in PrologueForestCorridorPines)
        {
            if (NearExistingTree(position, 8f)) continue;
            AddTree("WinterPine", position, yawDegrees, forestTreeIndex++, "corridor");
        }

        var existingForestRoots = placedTreeRoots.ToArray();
        const float forestCellSize = 24f;
        const float clusterHeightStep = 1.5f;
        const float minimumClusterPineSpacing = 2.9f;
        const float minimumExistingPineSpacing = 3.5f;
        const int clusteredPineRootBudget = 1300;
        static uint CoordinateHash(int column, int row, uint salt)
        {
            unchecked
            {
                var hash = salt + (uint)column * 0x9E3779B9u + (uint)row * 0x85EBCA6Bu;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                return hash ^ (hash >> 16);
            }
        }

        float UnitHash(int column, int row, uint salt)
            => CoordinateHash(column, row, salt) / (float)uint.MaxValue;

        float SignedJitter(int column, int row, uint salt, float radius)
            => (UnitHash(column, row, salt) * 2f - 1f) * radius;

        var clusteredPineCells = new Dictionary<(Vector2I Cell, int HeightBand), List<Transform3D>>();
        var clusteredPineRoots = new List<Vector2>();
        var clusteredPineCandidates = new List<(Vector2 Position, Transform3D Transform, Vector2I Cell, int HeightBand)>();
        var cellColumns = Mathf.CeilToInt(groundBounds.Size.X / forestCellSize);
        var cellRows = Mathf.CeilToInt(groundBounds.Size.Z / forestCellSize);
        for (var cellX = 0; cellX < cellColumns; cellX++)
        {
            for (var cellZ = 0; cellZ < cellRows; cellZ++)
            {
                var cellSeed = CoordinateHash(cellX, cellZ, 0xA511E9B3u);
                // Leave occasional whole-cell gaps, then scatter a varying number of clumps inside every other cell.
                if (cellSeed % 11u == 0u) continue;
                var clusters = 4 + (int)(cellSeed % 2u);
                for (var cluster = 0; cluster < clusters; cluster++)
                {
                    var clusterColumn = cellX * 5 + cluster;
                    var clusterRow = cellZ * 5 + cluster;
                    var center = new Vector2(
                        groundBounds.Position.X + cellX * forestCellSize
                            + Mathf.Lerp(3f, forestCellSize - 3f,
                                UnitHash(clusterColumn, clusterRow, 0x63D83595u)),
                        groundBounds.Position.Z + cellZ * forestCellSize
                            + Mathf.Lerp(3f, forestCellSize - 3f,
                                UnitHash(clusterColumn, clusterRow, 0xB5297A4Du)));
                    var treesInCluster = 5 + (int)(CoordinateHash(clusterColumn, clusterRow, 0xC2B2AE35u) % 3u);
                    var heightBand = (int)(CoordinateHash(clusterColumn, clusterRow, 0x7F4A7C15u) % 8u);
                    var clusterMinimumHeight = minimumClusterPineHeight + heightBand * clusterHeightStep;
                    for (var treeIndex = 0; treeIndex < treesInCluster; treeIndex++)
                    {
                        var keyColumn = clusterColumn * 7 + treeIndex;
                        var keyRow = clusterRow * 7 + treeIndex * 3;
                        var position = center + new Vector2(
                            SignedJitter(keyColumn, keyRow, 0x9E3779B9u, 4.4f),
                            SignedJitter(keyColumn, keyRow, 0x85EBCA6Bu, 4.4f));
                        if (position.X < groundBounds.Position.X || position.X > groundBounds.End.X
                            || position.Y < groundBounds.Position.Z || position.Y > groundBounds.End.Z
                            || existingForestRoots.Any(existing => existing.DistanceSquaredTo(position)
                                < minimumExistingPineSpacing * minimumExistingPineSpacing)
                            || clusteredPineRoots.Any(existing => existing.DistanceSquaredTo(position)
                                < minimumClusterPineSpacing * minimumClusterPineSpacing))
                            continue;

                        var height = Mathf.Lerp(clusterMinimumHeight, clusterMinimumHeight + clusterHeightStep,
                            UnitHash(keyColumn, keyRow, 0xD1B54A35u));
                        var heightScale = height / pineSourceBounds.Size.Y;
                        var horizontalScale = heightScale * Mathf.Lerp(0.72f, 0.98f,
                            UnitHash(keyColumn, keyRow, 0x94D049BBu));
                        var standingFootprintRadius = clusteredPineStandingFootprintRadius * horizontalScale;
                        if (RouteDistance(position) < standingFootprintRadius + standingLaneHalfWidth)
                            continue;

                        var groundY = ForestGroundHeight(position.X, position.Y)
                            - pineSourceBounds.Position.Y * heightScale;
                        var yaw = UnitHash(keyColumn, keyRow, 0x369DEA0Fu) * Mathf.Tau;
                        var basis = new Basis(Vector3.Up, yaw)
                            * Basis.FromScale(new Vector3(horizontalScale, heightScale, horizontalScale));
                        var transform = new Transform3D(basis, new Vector3(position.X, groundY, position.Y));
                        var cell = new Vector2I(Mathf.FloorToInt(position.X / forestCellSize),
                            Mathf.FloorToInt(position.Y / forestCellSize));
                        clusteredPineCandidates.Add((position, transform, cell, heightBand));
                        clusteredPineRoots.Add(position);
                    }
                }
            }
        }

        // Scan the full bounds first; if necessary, stride-sample the full deterministic list instead of filling the first x-major edge.
        var selectedClusterCandidates = clusteredPineCandidates;
        if (clusteredPineCandidates.Count > clusteredPineRootBudget)
        {
            selectedClusterCandidates = new List<(Vector2 Position, Transform3D Transform, Vector2I Cell, int HeightBand)>(
                clusteredPineRootBudget);
            for (var sample = 0; sample < clusteredPineRootBudget; sample++)
            {
                var candidateIndex = (int)((long)sample * clusteredPineCandidates.Count / clusteredPineRootBudget);
                selectedClusterCandidates.Add(clusteredPineCandidates[candidateIndex]);
            }
        }

        foreach (var candidate in selectedClusterCandidates)
        {
            var groupKey = (candidate.Cell, candidate.HeightBand);
            if (!clusteredPineCells.TryGetValue(groupKey, out var transforms))
                clusteredPineCells[groupKey] = transforms = new List<Transform3D>();
            transforms.Add(candidate.Transform);
        }

        void AddClusteredPineTier(
            ArrayMesh mesh, int lod, string variant, Vector3 origin, float treeHeight,
            IReadOnlyList<Transform3D> transforms, Vector2I cell, int heightBand)
        {
            var multi = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = mesh,
                InstanceCount = transforms.Count
            };
            for (var index = 0; index < transforms.Count; index++)
            {
                var transform = transforms[index];
                transform.Origin -= origin;
                multi.SetInstanceTransform(index, transform);
            }

            var meshTop = Mathf.Max(0f, mesh.GetAabb().End.Y);
            var sway = 0f;
            for (var surfaceIndex = 0; surfaceIndex < mesh.GetSurfaceCount(); surfaceIndex++)
                if (mesh.SurfaceGetMaterial(surfaceIndex) is ShaderMaterial material)
                    sway = Mathf.Max(sway, material.GetShaderParameter("wind_sway").AsSingle());
            var displacement = new Vector3(meshTop * sway, 0f, meshTop * sway * 0.6f);
            var forestCell = new MultiMeshInstance3D
            {
                Name = $"PrologueForest_{variant}Cell_{cell.X}_{cell.Y}_H{heightBand}",
                Position = origin,
                Multimesh = multi,
                CastShadow = lod == 2
                    ? GeometryInstance3D.ShadowCastingSetting.Off
                    : GeometryInstance3D.ShadowCastingSetting.On,
                ExtraCullMargin = transforms.Max(transform => (transform.Basis * displacement).Length())
            };
            forestCell.SetMeta("presentationOnly", true);
            forestCell.SetMeta("sourceVariant", variant);
            forestCell.SetMeta("forestLayer", lod == 1 ? "clustered-mid" : "far");
            forestCell.SetMeta("windCullMargin", forestCell.ExtraCullMargin);
            AgentBAct1ExteriorLayer.ConfigureFoliageRange(forestCell, lod, false, treeHeight);
            if (lod == 1)
            {
                // Cluster cells have no LOD0 twin; keep their light mesh visible up close and hand it to LOD2 at the normal 60 m crossover.
                forestCell.VisibilityRangeBegin = 0f;
                forestCell.VisibilityRangeBeginMargin = 0f;
            }
            _forestVisual.AddChild(forestCell);
        }

        foreach (var (groupKey, transforms) in clusteredPineCells)
        {
            var (cell, heightBand) = groupKey;
            var origin = new Vector3(cell.X * forestCellSize + forestCellSize * 0.5f, 0f,
                cell.Y * forestCellSize + forestCellSize * 0.5f);
            var representativeHeight = Mathf.Min(24f, minimumClusterPineHeight
                + (heightBand + 0.5f) * clusterHeightStep);
            AddClusteredPineTier(lightPineMesh, 1, "WinterLightPine",
                origin, representativeHeight, transforms, cell, heightBand);
            AddClusteredPineTier(farPineMesh, 2, "WinterFarPine",
                origin, representativeHeight, transforms, cell, heightBand);
        }

        kit.QueueFree();
    }

    private static ShaderMaterial CreatePrologueGroundMaterial()
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = PrologueGroundShaderCode } };
        material.SetShaderParameter("fresh_snow", LoadTexture(FreshSnowTexturePath));
        material.SetShaderParameter("trampled_snow", LoadTexture(TrampledSnowTexturePath));
        material.SetShaderParameter("forest_roots", LoadTexture(ForestRootsTexturePath));
        material.SetShaderParameter("forest_leaves", LoadTexture(ForestLeavesTexturePath));
        return material;
    }

    private void BuildPhotoTableau()
    {
        _photoTableauVisual = LoadScene(PhotoTableauScenePath).Instantiate<Node3D>();
        _photoTableauVisual.Name = "PForestPhotographedLandmarks";
        _photoTableauVisual.Visible = false;
        AddChild(_photoTableauVisual);
        _photoTableauBody = new StaticBody3D
        {
            Name = "PForestLandmarkContact", CollisionLayer = 0u, CollisionMask = 3u
        };
        AddChild(_photoTableauBody);
        foreach (var mesh in Descendants<MeshInstance3D>(_photoTableauVisual))
        {
            // The current W01 ground and high forest own the surrounding scene.
            // The imported source supplies the recognizable photographed supports.
            if (mesh.Name.ToString().Contains("Snowbank", StringComparison.Ordinal)
                || mesh.Name.ToString().Contains("DistantTrunk", StringComparison.Ordinal))
            {
                mesh.Visible = false;
                continue;
            }
            if (mesh.Mesh is null) continue;
            var contact = Act1ConnectedWorld.AuthoredSurfaceContact(_photoTableauBody, mesh);
            _photoTableauBody.AddChild(contact);
            switch (mesh.Name.ToString())
            {
                case "PForest_SplitBirch_left_stem":
                    _forestSplitBirchContact = contact;
                    _forestSplitBirchObservationPoint = ForestObservationPoint(mesh, ForestSplitBirchObservationHint);
                    break;
                case "PForest_LowCrossbar":
                    _forestCrossbarContact = contact;
                    _forestCrossbarObservationPoint = ForestObservationPoint(mesh, ForestCrossbarObservationHint);
                    break;
                case "PForest_RootArch_modest_natural":
                    _forestRootArchContact = contact;
                    _forestRootArchObservationPoint = ForestObservationPoint(mesh, ForestRootArchObservationHint);
                    break;
            }
        }
    }

    private void BuildProloguePhotoSource()
    {
        var anchor = GetNode<Marker3D>("Anchors/F00/ProloguePhotoSource");
        var cardSize = new Vector2(0.34f, 0.227f);
        var material = new StandardMaterial3D
        {
            AlbedoTexture = LoadTexture(ProloguePhotoTexturePath),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _prologuePhotoCard = new MeshInstance3D
        {
            Name = "F00ProloguePhotoPrint",
            Mesh = new QuadMesh { Size = cardSize },
            MaterialOverride = material,
            Visible = false
        };
        anchor.AddChild(_prologuePhotoCard);

        var target = new InteractionTarget
        {
            Name = "F00ProloguePhotoSourceTarget",
            InteractionId = ProloguePhotoSourceInteractionId,
            CollisionLayer = 4u,
            CollisionMask = 0u
        };
        target.ConfigureRayOnly();
        target.AddChild(new CollisionShape3D
        {
            Name = "F00ProloguePhotoSourceRayShape",
            Shape = new BoxShape3D { Size = new Vector3(cardSize.X, cardSize.Y, 0.018f) }
        });
        anchor.AddChild(target);
        target.SetPresentationEnabled(false);
        target.AfterDispatch = OpenProloguePhotoInspectionAsync;
        _prologuePhotoTarget = target;
    }

    private Task<bool> OpenProloguePhotoInspectionAsync()
    {
        var bridge = _bridge;
        var documentUi = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        var opened = bridge is not null && GodotObject.IsInstanceValid(bridge)
            && bridge.SessionIdentity is not null && bridge.ActiveSceneId == PrologueSceneId
            && documentUi?.OpenProloguePhotoInspection(bridge) == true;
        return Task.FromResult(opened);
    }

    private void BuildBranch()
    {
        var branchScene = LoadScene(BranchScenePath);
        _branchVisual = branchScene.Instantiate<Node3D>();
        _branchVisual.Name = "F01BranchVisual";
        _branchVisual.Position = BranchF01;
        _branchVisual.RotationDegrees = new Vector3(0f, BranchAcrossPathYaw, 0f);
        _branchVisual.Visible = false;
        AddChild(_branchVisual);

        _movingRoot = RequireDescendant<Node3D>(_branchVisual, "PrologueBranch_MovingRoot");
        _branchAnimation = Descendants<AnimationPlayer>(_branchVisual).FirstOrDefault()
            ?? throw new InvalidOperationException("W01 branch asset is missing its authored AnimationPlayer.");
        if (!_branchAnimation.HasAnimation(BranchMoveAnimationName))
            throw new InvalidOperationException($"W01 branch asset is missing animation: {BranchMoveAnimationName}");
        _branchAnimation.AnimationFinished += OnBranchAnimationFinished;
        var branchMesh = RequireDescendant<MeshInstance3D>(_branchVisual, "PrologueBranch_Body");
        var leftRootMesh = RequireDescendant<MeshInstance3D>(_branchVisual, "SIDE_RootSupport_Left");
        var rightRootMesh = RequireDescendant<MeshInstance3D>(_branchVisual, "SIDE_RootSupport_Right");
        _gripCenter = RequireDescendant<Node3D>(_branchVisual, "Branch_GripCenter");

        _movingBranchBody = AddConvexContact(_movingRoot, branchMesh, "MovingBranchContact", animatable: true);
        _leftRootBody = AddConvexContact(_branchVisual, leftRootMesh, "LeftRootSupportContact", animatable: false);
        _rightRootBody = AddConvexContact(_branchVisual, rightRootMesh, "RightRootSupportContact", animatable: false);
    }

    private void TryBindRuntimeBridge()
    {
        if (_bridge is { } current && GodotObject.IsInstanceValid(current))
        {
            return;
        }

        if (!IsInsideTree()
            || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge)
            return;

        _bridge = bridge;
        bridge.RuntimeStateChanged += ApplyProjection;
        ApplyProjection();
    }

    private void ApplyProjection()
    {
        if (!IsInsideTree()
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || _branchVisual is not { } visual || !GodotObject.IsInstanceValid(visual))
            return;

        var projection = bridge.CurrentW01BranchProjection();
        ApplyRootPassageProjection(projection);
        ApplyBagProjection();
        ApplyShuraleProjection();
        if (projection is null || projection.ActiveSceneId != ForestSceneId
            || !ReferenceEquals(_forestObservationFailedSession, projection.SessionIdentity))
            _forestObservationFailedSession = null;
        if (projection is null)
        {
            ResetForestExitTracking(null);
            CancelBranchMoveAnimation();
            visual.Visible = false;
            SetWorldPresentationVisible(false);
            SetProloguePhotoSourceVisible(bridge, false);
            SetCollisionLayer(_groundBody, 0u);
            SetCollisionLayer(_movingBranchBody, 0u);
            SetCollisionLayer(_leftRootBody, 0u);
            SetCollisionLayer(_rightRootBody, 0u);
            _branchTarget?.SetPresentationEnabled(false);
            SetPrologueEndVisible(bridge, false);
            SetPrologueSkipVisible(bridge, false);
            ApplyForestWorldGuardProjection(null);
            SetPhysicsProcess(false);
            return;
        }

        var interaction = projection.ActiveSceneId switch
        {
            PrologueSceneId => (PrologueBranchInteractionId, "Отвести ветку"),
            ForestSceneId => (ForestBranchInteractionId, "Уложить ветку на боковые корни"),
            _ => (string.Empty, string.Empty)
        };
        if (string.IsNullOrEmpty(interaction.Item1))
        {
            ResetForestExitTracking(null);
            CancelBranchMoveAnimation();
            visual.Visible = false;
            SetWorldPresentationVisible(false);
            SetProloguePhotoSourceVisible(bridge, false);
            SetCollisionLayer(_groundBody, 0u);
            SetCollisionLayer(_movingBranchBody, 0u);
            SetCollisionLayer(_leftRootBody, 0u);
            SetCollisionLayer(_rightRootBody, 0u);
            _branchTarget?.SetPresentationEnabled(false);
            SetPrologueEndVisible(bridge, false);
            SetPrologueSkipVisible(bridge, false);
            ApplyForestWorldGuardProjection(null);
            SetPhysicsProcess(false);
            return;
        }

        // A new session or restored save is projected directly into its durable pose.
        // Only a live player action plays lift_and_place before its fact is dispatched.
        CancelBranchMoveAnimation();
        if (_movingRoot is { } movingRoot && GodotObject.IsInstanceValid(movingRoot))
            movingRoot.Position = projection.Moved ? BranchMovedRootPosition : Vector3.Zero;

        visual.Visible = true;
        SetWorldPresentationVisible(true);
        SetProloguePhotoSourceVisible(bridge, projection.ActiveSceneId == PrologueSceneId);
        SetCollisionLayer(_groundBody, 1u);
        SetCollisionLayer(_movingBranchBody, 1u);
        SetCollisionLayer(_leftRootBody, 1u);
        SetCollisionLayer(_rightRootBody, 1u);
        EnsureBranchTarget(interaction.Item1, interaction.Item2);
        _branchTarget?.SetPresentationEnabled(!projection.Moved);
        SetPrologueEndVisible(bridge, projection.ActiveSceneId == PrologueSceneId);
        SetPrologueSkipVisible(bridge, projection.ActiveSceneId == PrologueSceneId);
        ApplyForestWorldGuardProjection(projection);
        SetPhysicsProcess(projection.ActiveSceneId == ForestSceneId);
    }

    private void TryObserveForestCameraBack()
    {
        if (_forestObservationDispatching
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session)
            return;

        var projection = bridge.CurrentW01BranchProjection();
        if (projection is null || projection.ActiveSceneId != ForestSceneId
            || !ReferenceEquals(projection.SessionIdentity, session))
        {
            SetPhysicsProcess(_rootPassageEnabled);
            return;
        }
        if (bridge.PhotoWorldFactObserved(ForestCameraBackFactId))
        {
            SetPhysicsProcess(_rootPassageEnabled);
            return;
        }

        if (MainMenuUi.AnyUndismissed(GetTree())
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor())
            return;

        var camera = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        var f11 = GetNodeOrNull<Marker3D>("Anchors/F11");
        if (camera is null || !GodotObject.IsInstanceValid(camera) || !camera.Current
            || f11 is null || !GodotObject.IsInstanceValid(f11))
            return;

        var playerPosition = player.GlobalPosition;
        var offset = playerPosition - f11.GlobalPosition;
        if (Math.Abs(offset.X) > 1.5f || Math.Abs(offset.Z) > 1.5f)
        {
            _forestObservationFailedSession = null;
            return;
        }

        var forward = (-camera.GlobalBasis.Z).Normalized();
        if (ObservationFailureStillSame(session, playerPosition, forward)) return;

        if (_forestSplitBirchContact is not { } splitBirchContact
            || !GodotObject.IsInstanceValid(splitBirchContact)
            || _forestCrossbarContact is not { } crossbarContact
            || !GodotObject.IsInstanceValid(crossbarContact)
            || _forestRootArchContact is not { } rootArchContact
            || !GodotObject.IsInstanceValid(rootArchContact)
            || _photoTableauBody is not { } tableauBody
            || !GodotObject.IsInstanceValid(tableauBody)
            || _photoTableauVisual is not { Visible: true } tableauVisual
            || !GodotObject.IsInstanceValid(tableauVisual))
            return;

        if (!_forestObservationRayExclusions.Contains(player.GetRid()))
        {
            _forestObservationRayExclusions.Clear();
            _forestObservationRayExclusions.Add(player.GetRid());
        }

        var space = GetWorld3D().DirectSpaceState;
        var eye = camera.GlobalPosition;
        if (!ForestLandmarkFramedAndVisible(camera, eye, _forestSplitBirchObservationPoint,
                splitBirchContact, tableauBody, space)
            || !ForestLandmarkFramedAndVisible(camera, eye, _forestCrossbarObservationPoint,
                crossbarContact, tableauBody, space)
            || !ForestLandmarkFramedAndVisible(camera, eye, _forestRootArchObservationPoint,
                rootArchContact, tableauBody, space))
            return;

        _forestObservationDispatching = true;
        SetPhysicsProcess(_rootPassageEnabled);
        DispatchForestCameraBackObservationAsync(bridge, session, player, playerPosition, forward);
    }

    private bool ForestLandmarkFramedAndVisible(
        Camera3D camera,
        Vector3 eye,
        Vector3 point,
        CollisionShape3D expectedContact,
        StaticBody3D tableauBody,
        PhysicsDirectSpaceState3D space)
    {
        var local = camera.ToLocal(point);
        if (local.Z >= -0.05f) return false;
        var viewport = camera.GetViewport().GetVisibleRect().Size;
        var aspect = viewport.X / Math.Max(viewport.Y, 1f);
        var tangent = Mathf.Tan(Mathf.DegToRad(camera.Fov) * 0.5f);
        var halfWidth = camera.KeepAspect == Camera3D.KeepAspectEnum.Width ? tangent : tangent * aspect;
        var halfHeight = camera.KeepAspect == Camera3D.KeepAspectEnum.Width ? tangent / aspect : tangent;
        if (Math.Abs(local.X / -local.Z) > halfWidth || Math.Abs(local.Y / -local.Z) > halfHeight)
            return false;

        using var ray = PhysicsRayQueryParameters3D.Create(eye, point, 3u, _forestObservationRayExclusions);
        using var hit = space.IntersectRay(ray);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() is not CollisionObject3D collider
            || !ReferenceEquals(collider, tableauBody))
            return false;

        var shapeOwnerId = collider.ShapeFindOwner(hit["shape"].AsInt32());
        return shapeOwnerId >= 0
            && ReferenceEquals(collider.ShapeOwnerGetOwner(shapeOwnerId), expectedContact);
    }

    private async void DispatchForestCameraBackObservationAsync(
        RuntimeBridge bridge,
        object session,
        FirstPersonController player,
        Vector3 playerPosition,
        Vector3 forward)
    {
        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(ForestCameraBackInteractionId);
        }
        catch (Exception exception)
        {
            GD.PushError($"W01 forest camera-back observation failed before commit: {exception.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()) return;
        _forestObservationDispatching = false;
        if (!GodotObject.IsInstanceValid(bridge) || !ReferenceEquals(session, bridge.SessionIdentity))
        {
            ApplyProjection();
            return;
        }

        var projection = bridge.CurrentW01BranchProjection();
        if (committed && projection is { ActiveSceneId: ForestSceneId } current
            && ReferenceEquals(current.SessionIdentity, session)
            && bridge.PhotoWorldFactObserved(ForestCameraBackFactId))
        {
            _forestObservationFailedSession = null;
            SetPhysicsProcess(_rootPassageEnabled);
            if (GodotObject.IsInstanceValid(player)
                && ReferenceEquals(GetTree().GetFirstNodeInGroup("player_controller"), player))
                player.NotifyTraversal(bridge.ResolveText(ForestCameraBackTextId));
            return;
        }

        if (projection is { ActiveSceneId: ForestSceneId } stillCurrent
            && ReferenceEquals(stillCurrent.SessionIdentity, session)
            && !bridge.PhotoWorldFactObserved(ForestCameraBackFactId))
        {
            _forestObservationFailedSession = session;
            _forestObservationFailedPosition = playerPosition;
            _forestObservationFailedForward = forward;
            SetPhysicsProcess(_rootPassageEnabled);
            return;
        }

        ApplyProjection();
    }

    private bool ObservationFailureStillSame(object session, Vector3 position, Vector3 forward)
    {
        if (!ReferenceEquals(_forestObservationFailedSession, session))
        {
            _forestObservationFailedSession = null;
            return false;
        }

        if (position.DistanceTo(_forestObservationFailedPosition) < 0.2f
            && forward.Dot(_forestObservationFailedForward) >= 0.99f)
            return true;

        _forestObservationFailedSession = null;
        return false;
    }

    private static Vector3 ForestObservationPoint(MeshInstance3D mesh, Vector3 hint)
    {
        var faces = mesh.Mesh!.GetFaces();
        var bestDistance = float.PositiveInfinity;
        var bestPoint = Vector3.Zero;
        for (var index = 0; index + 2 < faces.Length; index += 3)
        {
            var localCenter = (faces[index] + faces[index + 1] + faces[index + 2]) / 3f;
            var worldCenter = mesh.GlobalTransform * localCenter;
            var distance = worldCenter.DistanceSquaredTo(hint);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            bestPoint = worldCenter;
        }

        if (float.IsPositiveInfinity(bestDistance))
            throw new InvalidOperationException($"W01 landmark has no triangle observation point: {mesh.Name}");
        var inward = hint - bestPoint;
        return bestPoint + inward.Normalized() * Math.Min(0.015f, inward.Length() * 0.1f);
    }

    private void SetProloguePhotoSourceVisible(RuntimeBridge bridge, bool visible)
    {
        if (_prologuePhotoCard is { } card && GodotObject.IsInstanceValid(card))
            card.Visible = visible;
        if (_prologuePhotoTarget is not { } target || !GodotObject.IsInstanceValid(target)) return;
        target.Prompt = bridge.ResolveText(ProloguePhotoSourceLabelTextId);
        target.SetPresentationEnabled(visible);
    }

    private void CancelBranchMoveAnimation()
    {
        _branchMoveInProgress = false;
        _branchMoveInteractionId = null;
        _branchMoveSceneId = null;
        _branchMoveSession = null;
        if (_branchAnimation is { } animation && GodotObject.IsInstanceValid(animation))
            animation.Stop();
    }

    private bool BeginBranchMove(string interactionId)
    {
        if (_branchMoveInProgress
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session
            || _branchAnimation is not { } animation || !GodotObject.IsInstanceValid(animation)
            || _movingRoot is not { } movingRoot || !GodotObject.IsInstanceValid(movingRoot)
            || !animation.HasAnimation(BranchMoveAnimationName))
            return false;

        var projection = bridge.CurrentW01BranchProjection();
        if (projection is null || projection.Moved
            || (projection.ActiveSceneId != PrologueSceneId && projection.ActiveSceneId != ForestSceneId))
            return false;

        _branchMoveInProgress = true;
        _branchMoveInteractionId = interactionId;
        _branchMoveSceneId = projection.ActiveSceneId;
        _branchMoveSession = session;
        _branchTarget?.SetPresentationEnabled(false);
        if (_accessibility.ReducedMotion)
        {
            animation.Stop();
            movingRoot.Position = BranchMovedRootPosition;
            CommitCompletedBranchMove();
        }
        else
        {
            animation.Play(BranchMoveAnimationName);
        }
        return false;
    }

    private void OnBranchAnimationFinished(StringName animationName)
    {
        if (animationName != BranchMoveAnimationName || !_branchMoveInProgress) return;
        CommitCompletedBranchMove();
    }

    private void CommitCompletedBranchMove()
    {
        if (!_branchMoveInProgress) return;
        var bridge = _bridge;
        var session = _branchMoveSession;
        var interactionId = _branchMoveInteractionId;
        var sceneId = _branchMoveSceneId;
        _branchMoveInProgress = false;
        _branchMoveSession = null;
        _branchMoveInteractionId = null;
        _branchMoveSceneId = null;

        if (bridge is null || !GodotObject.IsInstanceValid(bridge)
            || session is null || string.IsNullOrWhiteSpace(interactionId) || sceneId is null
            || !IsInsideTree() || IsQueuedForDeletion())
        {
            ApplyProjection();
            return;
        }

        DispatchCompletedBranchMoveAsync(bridge, session, interactionId, sceneId);
    }

    private async void DispatchCompletedBranchMoveAsync(
        RuntimeBridge bridge, object session, string interactionId, string sceneId)
    {
        var projection = bridge.CurrentW01BranchProjection();
        if (!ReferenceEquals(session, bridge.SessionIdentity)
            || MainMenuUi.AnyUndismissed(GetTree())
            || projection is null || projection.ActiveSceneId != sceneId || projection.Moved)
        {
            ApplyProjection();
            return;
        }

        try
        {
            await bridge.DispatchInteractionAsync(interactionId);
        }
        catch (Exception exception)
        {
            GD.PushError($"W01 branch interaction failed before commit: {exception.Message}");
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion())
                ApplyProjection();
            return;
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()) return;
        if (!GodotObject.IsInstanceValid(bridge) || !ReferenceEquals(session, bridge.SessionIdentity))
        {
            ApplyProjection();
            return;
        }

        // The reducer remains authoritative. A rejected action must not leave a
        // moved-looking branch with no saved fact; a successful one projects the
        // committed pose through the same route used by load and new-game state.
        ApplyProjection();
    }

    private void SetWorldPresentationVisible(bool visible)
    {
        if (_groundVisual is { } ground && GodotObject.IsInstanceValid(ground)) ground.Visible = visible;
        if (_forestVisual is { } forest && GodotObject.IsInstanceValid(forest)) forest.Visible = visible;
        if (_photoTableauVisual is { } tableau && GodotObject.IsInstanceValid(tableau)) tableau.Visible = visible;
        SetCollisionLayer(_photoTableauBody, visible ? 1u : 0u);
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey { Echo: true }
            || !inputEvent.IsActionPressed("ui_cancel")
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController { ModalOpen: false }
            || _prologueSkipButton is not { Visible: true, Disabled: false }
            || _prologueSkipTarget is not { } target
            || !GodotObject.IsInstanceValid(target)
            || !target.IsAvailable())
            return;

        // Match the existing prologue cancel-to-skip affordance. Dispatch the
        // authored interaction so its narrative gate and destination stay authoritative.
        target.Interact();
        GetViewport().SetInputAsHandled();
    }

    private void SetPrologueEndVisible(RuntimeBridge bridge, bool visible)
    {
        if (!visible)
        {
            _prologueEndTarget?.SetPresentationEnabled(false);
            return;
        }

        var endTarget = _prologueEndTarget;
        if (endTarget is null || !GodotObject.IsInstanceValid(endTarget))
        {
            var marker = GetNode<Marker3D>("Anchors/F03");
            var approach = PrologueRouteNodes[^1] - PrologueRouteNodes[^2];
            var target = new InteractionTarget
            {
                Name = "PrologueEnd_InteractionTarget",
                InteractionId = PrologueEndInteractionId,
                Prompt = bridge.ResolveText(PrologueEndLabelTextId),
                Rotation = new Vector3(0f, Mathf.Atan2(approach.X, approach.Z), 0f),
                CollisionLayer = 4u,
                CollisionMask = 0u
            };
            target.ConfigureRayOnly();
            target.AddChild(new CollisionShape3D
            {
                Name = "PrologueEnd_RayShape",
                // Cover the authored 2.1 m lane at eye height without blocking the player.
                Shape = new BoxShape3D { Size = new Vector3(2.1f, 2.4f, .6f) }
            });
            AddChild(target);
            target.GlobalPosition = marker.GlobalPosition + Vector3.Up * 1.2f;
            _prologueEndTarget = endTarget = target;
        }

        endTarget.SetPresentationEnabled(true);
    }

    private void BuildPrologueUi()
    {
        _prologueSkipButton = GetNode<Button>("PrologueUI/SkipButton");
        _prologueSkipButton.ThemeTypeVariation = UrmanUiTheme.MenuButton;
        _prologueSkipButton.FocusMode = Control.FocusModeEnum.All;
        _prologueSkipButton.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _prologueSkipButton.Pressed += OnPrologueSkipPressed;
        _prologueSkipButton.Visible = false;
        _prologueSkipButton.Disabled = true;
        _prologueHintAccessLabel = new Label
        {
            Name = "HintAccess",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _prologueHintAccessLabel.AddThemeFontSizeOverride("font_size", 18);
        _prologueHintAccessLabel.AddThemeColorOverride("font_color", new Color("f0e3c2"));
        _prologueHintAccessLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _prologueHintAccessLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _prologueHintAccessLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        _prologueSkipButton.GetParent().AddChild(_prologueHintAccessLabel);
        var viewport = GetViewport();
        viewport.SizeChanged += RefitPrologueSkipButton;
        _prologueUiViewport = viewport;
    }

    private void SetPrologueSkipVisible(RuntimeBridge bridge, bool visible)
    {
        if (_prologueSkipButton is not { } button || !GodotObject.IsInstanceValid(button)) return;
        if (visible && _prologueSkipTarget is null)
        {
            _prologueSkipTarget = new InteractionTarget
            {
                Name = "PrologueSkip_DispatchTarget",
                InteractionId = PrologueSkipInteractionId,
                Prompt = bridge.ResolveText(PrologueSkipLabelTextId),
                CollisionLayer = 0u,
                CollisionMask = 0u
            };
            AddChild(_prologueSkipTarget);
        }

        _prologueSkipTarget?.SetPresentationEnabled(visible);
        var hint = visible ? bridge.CurrentPhotoWorldHint() : null;
        _prologueHintQuestion = string.IsNullOrWhiteSpace(hint?.Question) ? null : hint.Question;
        _prologueHintAvailable = _prologueHintQuestion is not null;
        button.Text = bridge.ResolveText(PrologueSkipLabelTextId);
        button.Visible = visible;
        RefreshPrologueSkipButtonState();
    }

    private void OnPrologueSkipPressed()
    {
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController { ModalOpen: false }
            || _prologueSkipButton is not { Visible: true, Disabled: false }
            || _prologueSkipTarget is not { } target || !GodotObject.IsInstanceValid(target))
            return;
        target.Interact();
    }

    private void RefreshPrologueSkipButtonState()
    {
        if (_prologueSkipButton is not { } button || !GodotObject.IsInstanceValid(button)) return;
        button.Disabled = !button.Visible
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController { ModalOpen: false }
            || _prologueSkipTarget?.IsAvailable() != true;
        if (_prologueHintAccessLabel is { } hintLabel && GodotObject.IsInstanceValid(hintLabel))
        {
            hintLabel.Visible = !button.Disabled && _prologueHintAvailable;
            var gamepad = GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player
                && player.CurrentInputDevice == "gamepad";
            var text = $"{_prologueHintQuestion}\nПодробнее — {InputBindingService.ActionHint("journal", gamepad)}";
            if (!string.Equals(hintLabel.Text, text, StringComparison.Ordinal))
                hintLabel.Text = text;
        }
    }

    private void RefitPrologueSkipButton()
    {
        if (_prologueSkipButton is not { } button || !GodotObject.IsInstanceValid(button)) return;
        var viewport = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Clamp(viewport.X * .42f, 280f, 560f);
        var height = Mathf.Max(56f, 52f * (float)_accessibility.TextScale);
        button.AnchorLeft = button.AnchorRight = 1f;
        button.AnchorTop = button.AnchorBottom = 0f;
        button.OffsetLeft = -width - 24f;
        button.OffsetRight = -24f;
        button.OffsetTop = 24f;
        button.OffsetBottom = 24f + height;
        button.CustomMinimumSize = new Vector2(width, height);
        if (_prologueHintAccessLabel is { } hintLabel && GodotObject.IsInstanceValid(hintLabel))
        {
            hintLabel.AnchorLeft = hintLabel.AnchorRight = 1f;
            hintLabel.AnchorTop = hintLabel.AnchorBottom = 0f;
            hintLabel.OffsetLeft = button.OffsetLeft;
            hintLabel.OffsetRight = button.OffsetRight;
            hintLabel.OffsetTop = button.OffsetBottom + 12f;
            hintLabel.OffsetBottom = hintLabel.OffsetTop + Mathf.Max(96f, 96f * (float)_accessibility.TextScale);
        }
    }

    private void EnsureBranchTarget(string interactionId, string prompt)
    {
        if (_branchTarget is { } current && GodotObject.IsInstanceValid(current)
            && current.InteractionId == interactionId)
            return;

        if (_branchTarget is { } previous && GodotObject.IsInstanceValid(previous))
        {
            previous.SetPresentationEnabled(false);
            previous.QueueFree();
        }

        if (_gripCenter is not { } grip || !GodotObject.IsInstanceValid(grip)) return;
        var target = new InteractionTarget
        {
            Name = "Branch_InteractionTarget",
            InteractionId = interactionId,
            Prompt = prompt,
            CollisionLayer = 4u,
            CollisionMask = 0u
        };
        target.ConfigureRayOnly();
        target.PresentationBeforeDispatch = () => BeginBranchMove(interactionId);
        target.AddChild(new CollisionShape3D
        {
            Name = "Branch_GripRayShape",
            Shape = new SphereShape3D { Radius = 0.30f }
        });
        grip.AddChild(target);
        _branchTarget = target;
    }

    private static StaticBody3D AddConvexContact(Node3D owner, MeshInstance3D mesh, string name, bool animatable)
    {
        var sourceMesh = mesh.Mesh
            ?? throw new InvalidOperationException($"W01 contact mesh is missing: {mesh.Name}");
        StaticBody3D body = animatable
            ? new AnimatableBody3D { Name = name, CollisionLayer = 0u, CollisionMask = 0u }
            : new StaticBody3D { Name = name, CollisionLayer = 0u, CollisionMask = 0u };
        owner.AddChild(body);
        body.Transform = owner.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        body.AddChild(new CollisionShape3D
        {
            Name = name + "Shape",
            Shape = sourceMesh.CreateConvexShape()
        });
        return body;
    }

    private static void SetCollisionLayer(StaticBody3D? body, uint layer)
    {
        if (body is { } value && GodotObject.IsInstanceValid(value))
            value.CollisionLayer = layer;
    }

    private static PackedScene LoadScene(string path) =>
        ResourceLoader.Load<PackedScene>(path)
        ?? throw new InvalidOperationException($"W01 scene asset is missing or not imported: {path}");

    private static Texture2D LoadTexture(string path) =>
        ResourceLoader.Load<Texture2D>(path)
        ?? throw new InvalidOperationException($"W01 presentation texture is missing or not imported: {path}");

    private static float ForestGroundHeight(float x, float z)
    {
        var last = PrologueRouteNodes[^1];
        var routeX = z > last.Z ? last.X : PrologueRouteNodes[0].X;
        var routeY = z > last.Z ? last.Y : PrologueRouteNodes[0].Y;
        for (var index = 1; z <= last.Z && index < PrologueRouteNodes.Length; index++)
        {
            var first = PrologueRouteNodes[index - 1];
            var second = PrologueRouteNodes[index];
            if (z > second.Z) continue;
            var t = Mathf.Clamp((z - first.Z) / (second.Z - first.Z), 0f, 1f);
            routeX = Mathf.Lerp(first.X, second.X, t);
            routeY = Mathf.Lerp(first.Y, second.Y, t);
            break;
        }

        return routeY + TerrainRelief(x, z) - TerrainRelief(routeX, z) + TerrainMacroRelief(x, z);
    }

    private static float TerrainRelief(float x, float z)
    {
        var left = MathF.Pow((x - 3.2f) / 5.5f, 2f) + MathF.Pow((z - 24f) / 18f, 2f);
        var right = MathF.Pow((x - 15.5f) / 4.7f, 2f) + MathF.Pow((z - 31f) / 17f, 2f);
        var hollow = MathF.Pow((x + 4f) / 6f, 2f) + MathF.Pow((z - 42f) / 11f, 2f);
        return 0.16f * MathF.Exp(-left * 0.5f)
            + 0.12f * MathF.Exp(-right * 0.5f)
            - 0.06f * MathF.Exp(-hollow * 0.5f);
    }

    private static float TerrainMacroRelief(float x, float z)
    {
        var left = MathF.Pow((x + 4f) / 5f, 2f) + MathF.Pow((z - 12f) / 7f, 2f);
        var right = MathF.Pow((x - 11f) / 5f, 2f) + MathF.Pow((z - 23f) / 8f, 2f);
        var hollow = MathF.Pow((x + 10f) / 6f, 2f) + MathF.Pow((z - 25f) / 8f, 2f);
        var window = TerrainSmoothStep(0f, 6f, z) * (1f - TerrainSmoothStep(24f, 32f, z));
        var routeClearance = TerrainSmoothStep(1.8f, 8f, PrologueLaneDistance(x, z));
        return window * routeClearance * (0.70f * MathF.Exp(-left * 0.5f)
            + 0.85f * MathF.Exp(-right * 0.5f)
            - 0.30f * MathF.Exp(-hollow * 0.5f));
    }

    private static float PrologueLaneDistance(float x, float z)
    {
        var point = new Vector2(x, z);
        var segment = new Vector2(BranchF01.X, BranchF01.Z);
        var t = Mathf.Clamp(point.Dot(segment) / segment.LengthSquared(), 0f, 1f);
        return point.DistanceTo(segment * t);
    }

    private static float TerrainSmoothStep(float edge0, float edge1, float value)
    {
        var t = Mathf.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static T RequireDescendant<T>(Node root, string name) where T : Node
    {
        foreach (var node in Descendants<T>(root))
            if (node.Name.ToString() == name) return node;
        throw new InvalidOperationException($"W01 authored node is missing: {name}");
    }

    private static IEnumerable<T> Descendants<T>(Node root) where T : Node
    {
        if (root is T match) yield return match;
        foreach (var child in root.GetChildren())
        foreach (var descendant in Descendants<T>(child))
            yield return descendant;
    }
}
