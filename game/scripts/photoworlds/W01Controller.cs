using System;
using System.Collections.Generic;
using Godot;
using Urman.Experiments.AgentBAct1;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>Scene-owned F00→F03 prologue ground and authored actions for W01.</summary>
public partial class W01Controller : Node3D, IAccessibilitySettingsTarget
{
    private const string GroundScenePath = "res://assets/photoworlds/forest_ground/prologue-ground-f00-f03-v1.glb";
    private const string BranchScenePath = "res://assets/photoworlds/branch/prologue-branch-v1.glb";
    private const string FoliageKitScenePath = "res://assets/models/agent_b_act1/agentb_foliage_kit.glb";
    private const string PrologueSceneId = "urman.fullgame:scene/pw-prologue";
    private const string ForestSceneId = "urman.fullgame:scene/pw-forest";
    private const string PrologueBranchInteractionId = "urman.fullgame:interaction/pw-prologue-branch-move";
    private const string PrologueEndInteractionId = "urman.fullgame:interaction/pw-prologue-f03-end";
    private const string PrologueSkipInteractionId = "urman.fullgame:interaction/pw-prologue-skip";
    private const string ForestBranchInteractionId = "urman.fullgame:interaction/pw-forest-branch-move";
    private const string PrologueEndLabelTextId = "urman.fullgame:text/pw-label-prologue-f03-end";
    private const string PrologueSkipLabelTextId = "urman.fullgame:text/pw-label-prologue-skip";
    private static readonly Vector3 BranchF01 = new(12f, 1f, 50f);
    private static readonly Vector3 PrologueF02 = new(45f, 3f, 95f);
    private static readonly Vector3 PrologueF03 = new(80f, 4f, 140f);
    private static readonly Vector3 BranchMovedRootPosition = new(1.4f, 0.14f, 0f);
    private static readonly float BranchAcrossPathYaw = Mathf.RadToDeg(Mathf.Atan2(12f, 50f));
    private static readonly Vector3[] PrologueRouteNodes = { Vector3.Zero, BranchF01, PrologueF02, PrologueF03 };
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

    private RuntimeBridge? _bridge;
    private Node3D? _branchVisual;
    private Node3D? _movingRoot;
    private Node3D? _gripCenter;
    private Node3D? _groundVisual;
    private Node3D? _forestVisual;
    private StaticBody3D? _groundBody;
    private StaticBody3D? _leftRootBody;
    private StaticBody3D? _rightRootBody;
    private StaticBody3D? _movingBranchBody;
    private InteractionTarget? _branchTarget;
    private InteractionTarget? _prologueEndTarget;
    private InteractionTarget? _prologueSkipTarget;
    private Button? _prologueSkipButton;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public override void _Ready()
    {
        AddToGroup(AccessibilityPresentation.TargetGroup);
        BuildGround();
        BuildForest();
        BuildBranch();
        BuildPrologueUi();
        SetProcess(true);
        TryBindRuntimeBridge();
        ApplyAccessibilitySettings(
            (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.Accessibility
            ?? AccessibilitySettingsSnapshot.Default);
    }

    public override void _Process(double delta) => TryBindRuntimeBridge();

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _accessibility = settings;
        if (_prologueSkipButton is not { } button || !GodotObject.IsInstanceValid(button)) return;
        AccessibilityPresentation.ApplyToControl(button, settings);
        RefitPrologueSkipButton();
    }

    public override void _ExitTree()
    {
        SetProcess(false);
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= ApplyProjection;
        GetViewport().SizeChanged -= RefitPrologueSkipButton;
        _bridge = null;
        _branchTarget = null;
        _prologueEndTarget = null;
        _prologueSkipTarget = null;
        _prologueSkipButton = null;
        _movingBranchBody = null;
        _leftRootBody = null;
        _rightRootBody = null;
        _groundBody = null;
        _groundVisual = null;
        _forestVisual = null;
        _gripCenter = null;
        _movingRoot = null;
        _branchVisual = null;
    }

    private void BuildGround()
    {
        var groundScene = LoadScene(GroundScenePath);
        _groundVisual = groundScene.Instantiate<Node3D>();
        _groundVisual.Name = "F00F03GroundVisual";
        _groundVisual.Visible = false;
        AddChild(_groundVisual);

        _groundBody = new StaticBody3D
        {
            Name = "F00F03GroundContact",
            CollisionLayer = 0u,
            CollisionMask = 3u
        };
        AddChild(_groundBody);
        foreach (var mesh in Descendants<MeshInstance3D>(_groundVisual))
        {
            if (mesh.Mesh is null) continue;
            _groundBody.AddChild(Act1ConnectedWorld.AuthoredSurfaceContact(_groundBody, mesh));
        }
    }

    private void BuildForest()
    {
        _forestVisual = new Node3D { Name = "F00F03ForestVisual", Visible = false };
        _forestVisual.SetMeta("presentationOnly", true);
        _forestVisual.SetMeta("sourceAsset", "urman.act1.agentb.foliage_kit");
        _forestVisual.SetMeta("routeClearance", "authored 2.1 m path retained; trees are non-colliding presentation");
        AddChild(_forestVisual);

        var kit = AgentBKitMaterials.InstantiateKit(this, FoliageKitScenePath, "W01FoliageTemplateSource");
        kit.Visible = false;
        var roots = Descendants<Node3D>(kit)
            .Where(node => node.Name.ToString().EndsWith("_Root", StringComparison.Ordinal))
            .ToDictionary(node => node.Name.ToString(), StringComparer.Ordinal);

        for (var index = 0; index < PrologueForestTrees.Length; index++)
        {
            var placement = PrologueForestTrees[index];
            var rootName = placement.Variant + "_Root";
            if (!roots.TryGetValue(rootName, out var template))
                throw new InvalidOperationException($"Registered foliage kit is missing native root: {rootName}");

            var tree = (Node3D)template.Duplicate();
            tree.Name = $"PrologueForest_{placement.Variant}_{index + 1:00}";
            tree.Position = new Vector3(placement.Position.X,
                ForestGroundHeight(placement.Position.X, placement.Position.Y), placement.Position.Y);
            tree.RotationDegrees = new Vector3(0f, placement.YawDegrees, 0f);
            tree.SetMeta("presentationOnly", true);
            tree.SetMeta("sourceVariant", placement.Variant);
            _forestVisual.AddChild(tree);
        }

        kit.QueueFree();
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
            SetProcess(false);
            return;
        }

        if (!IsInsideTree()
            || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge)
            return;

        _bridge = bridge;
        bridge.RuntimeStateChanged += ApplyProjection;
        SetProcess(false);
        ApplyProjection();
    }

    private void ApplyProjection()
    {
        if (!IsInsideTree()
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || _branchVisual is not { } visual || !GodotObject.IsInstanceValid(visual))
            return;

        var projection = bridge.CurrentW01BranchProjection();
        if (projection is null)
        {
            visual.Visible = false;
            SetWorldPresentationVisible(false);
            SetCollisionLayer(_groundBody, 0u);
            SetCollisionLayer(_movingBranchBody, 0u);
            SetCollisionLayer(_leftRootBody, 0u);
            SetCollisionLayer(_rightRootBody, 0u);
            _branchTarget?.SetPresentationEnabled(false);
            SetPrologueEndVisible(bridge, false);
            SetPrologueSkipVisible(bridge, false);
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
            visual.Visible = false;
            SetWorldPresentationVisible(false);
            SetCollisionLayer(_groundBody, 0u);
            SetCollisionLayer(_movingBranchBody, 0u);
            SetCollisionLayer(_leftRootBody, 0u);
            SetCollisionLayer(_rightRootBody, 0u);
            _branchTarget?.SetPresentationEnabled(false);
            SetPrologueEndVisible(bridge, false);
            SetPrologueSkipVisible(bridge, false);
            return;
        }

        // The authored rest_moved clip translates this root by exactly (1.4, .14, 0).
        // Snap on every validated projection so new sessions and restored saves agree.
        if (_movingRoot is { } movingRoot && GodotObject.IsInstanceValid(movingRoot))
            movingRoot.Position = projection.Moved ? BranchMovedRootPosition : Vector3.Zero;

        visual.Visible = true;
        SetWorldPresentationVisible(true);
        SetCollisionLayer(_groundBody, 1u);
        SetCollisionLayer(_movingBranchBody, 1u);
        SetCollisionLayer(_leftRootBody, 1u);
        SetCollisionLayer(_rightRootBody, 1u);
        EnsureBranchTarget(interaction.Item1, interaction.Item2);
        _branchTarget?.SetPresentationEnabled(!projection.Moved);
        SetPrologueEndVisible(bridge, projection.ActiveSceneId == PrologueSceneId);
        SetPrologueSkipVisible(bridge, projection.ActiveSceneId == PrologueSceneId);
    }

    private void SetWorldPresentationVisible(bool visible)
    {
        if (_groundVisual is { } ground && GodotObject.IsInstanceValid(ground)) ground.Visible = visible;
        if (_forestVisual is { } forest && GodotObject.IsInstanceValid(forest)) forest.Visible = visible;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey { Echo: true }
            || !inputEvent.IsActionPressed("ui_cancel")
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
            var target = new InteractionTarget
            {
                Name = "PrologueEnd_InteractionTarget",
                InteractionId = PrologueEndInteractionId,
                Prompt = bridge.ResolveText(PrologueEndLabelTextId),
                CollisionLayer = 4u,
                CollisionMask = 0u
            };
            target.ConfigureRayOnly();
            target.AddChild(new CollisionShape3D
            {
                Name = "PrologueEnd_RayShape",
                Shape = new SphereShape3D { Radius = 0.55f }
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
        GetViewport().SizeChanged += RefitPrologueSkipButton;
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
        button.Text = bridge.ResolveText(PrologueSkipLabelTextId);
        button.Visible = visible;
        button.Disabled = !visible || _prologueSkipTarget?.IsAvailable() != true;
    }

    private void OnPrologueSkipPressed()
    {
        if (_prologueSkipTarget is { } target && GodotObject.IsInstanceValid(target)) target.Interact();
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
        target.AddChild(new CollisionShape3D
        {
            Name = "Branch_GripRayShape",
            Shape = new SphereShape3D { Radius = 0.16f }
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

        return routeY + TerrainRelief(x, z) - TerrainRelief(routeX, z);
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
