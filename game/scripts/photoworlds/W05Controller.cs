using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>Scoped physical presentation of the optional P-CELLAR world.</summary>
public partial class W05Controller : Node3D
{
    private const string CellarSceneId = "urman.fullgame:scene/pw-cellar";
    private RuntimeBridge? _bridge;
    private Node3D _world = null!;
    private readonly Dictionary<CollisionObject3D, uint> _worldCollisionLayers = [];
    private readonly List<Light3D> _lights = [];
    private bool _active;
    private object? _session;

    internal bool ActionInProgress => _committing
        || (_carryAttached && _carryCoordinator.ActionInProgress)
        || HasPendingEarnedBoxPlacement;
    internal bool IsCarryingFamilyBox => _carryAttached && _carryCoordinator.HeldItem is not null;

    public override void _Ready()
    {
        AddToGroup("w05_presentation");
        BuildCellarWorld();
        BuildCellarGameplay();
        CollectCellarBodies(_world);
        TryBindRuntimeBridge();
        ApplyProjection();
    }

    public override void _Process(double delta)
    {
        TryBindRuntimeBridge();
        TryAttachCellarCarry();
        ProcessCellarGuard();
        ProcessCellarGameplay(delta);
    }

    public override void _ExitTree()
    {
        CancelCellarActions();
        ReleaseCellarGuard();
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= ApplyProjection;
        _bridge = null;
        _session = null;
        if (!_carryAttached && _carryCoordinator is not null) _carryCoordinator.Free();
        _worldCollisionLayers.Clear();
        _lights.Clear();
    }

    private void TryBindRuntimeBridge()
    {
        var current = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (ReferenceEquals(current, _bridge) && current is not null && GodotObject.IsInstanceValid(current)) return;
        if (_bridge is { } previous && GodotObject.IsInstanceValid(previous))
            previous.RuntimeStateChanged -= ApplyProjection;
        CancelCellarActions();
        ReleaseCellarGuard();
        _bridge = current;
        if (current is not null) current.RuntimeStateChanged += ApplyProjection;
        ApplyProjection();
    }

    private void ApplyProjection()
    {
        var current = TryCurrentCellar(out var bridge, out var session);
        if (!current || !ReferenceEquals(_session, session)) CancelCellarActions();
        _session = current ? session : null;
        _active = current;
        _world.Visible = current;
        foreach (var (body, layer) in _worldCollisionLayers)
        {
            if (!GodotObject.IsInstanceValid(body)) continue;
            if (body is CarryableProp prop) prop.SetPresentationEnabled(current);
            else if (body is InteractionTarget target) target.SetPresentationEnabled(current);
            else body.CollisionLayer = current ? layer : 0u;
        }
        foreach (var light in _lights) light.Visible = current;
        ApplyCellarGameplayProjection(current, bridge);
        if (current) EnsureCellarGuard();
        else ReleaseCellarGuard();
    }

    private bool TryCurrentCellar(out RuntimeBridge bridge, out object session)
    {
        bridge = null!;
        session = null!;
        if (!IsInsideTree() || IsQueuedForDeletion() || _bridge is not { } owner
            || !GodotObject.IsInstanceValid(owner) || owner.IsQueuedForDeletion() || !owner.IsInsideTree()
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), owner)
            || owner.SessionIdentity is not { } identity
            || owner.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: CellarSceneId } route
            || !ReferenceEquals(route.SessionIdentity, identity)) return false;
        bridge = owner;
        session = identity;
        return true;
    }

    private void CollectCellarBodies(Node node)
    {
        if (node is CollisionObject3D body) _worldCollisionLayers.Add(body, body.CollisionLayer);
        foreach (var child in node.GetChildren()) CollectCellarBodies(child);
    }
}
