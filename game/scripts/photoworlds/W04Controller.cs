using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>Physical presentation and locally owned gameplay for P-COMMON.</summary>
public partial class W04Controller : Node3D
{
    private const string CommonSceneId = "urman.fullgame:scene/pw-common";
    private RuntimeBridge? _bridge;
    private readonly Dictionary<CollisionObject3D, uint> _worldCollisionLayers = [];

    public override void _Ready()
    {
        AddToGroup("w04_presentation");
        BuildCommonWorld();
        BuildCommonGameplay();
        CollectWorldCollisionLayers(_world);
        TryBindRuntimeBridge();
        ApplyProjection();
    }

    public override void _Process(double delta)
    {
        TryBindRuntimeBridge();
        ProcessCommonWorldGuard();
        ProcessCommonGameplay(delta);
    }

    public override void _ExitTree()
    {
        ExitCommonGameplay();
        ExitCommonWorldGuard();
        if (_bridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= ApplyProjection;
        _bridge = null;
        _worldCollisionLayers.Clear();
    }

    private void TryBindRuntimeBridge()
    {
        if (_bridge is { } current && GodotObject.IsInstanceValid(current)) return;
        if (GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge) return;
        _bridge = bridge;
        bridge.RuntimeStateChanged += ApplyProjection;
        ApplyProjection();
    }

    private void ApplyProjection()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)) return;
        var route = bridge.CurrentFullGamePhotoWorldRouteProjection();
        var visible = route is not null
            && ReferenceEquals(route.SessionIdentity, bridge.PhotoWorldPresentationIdentity)
            && route.ActiveSceneId == CommonSceneId;
        _world.Visible = visible;
        foreach (var (body, layer) in _worldCollisionLayers)
        {
            if (!GodotObject.IsInstanceValid(body)) continue;
            if (body is InteractionTarget target) target.SetPresentationEnabled(visible);
            else body.CollisionLayer = visible ? layer : 0u;
        }
        ApplyCommonGameplayProjection(route, visible);
        ApplyCommonWorldGuardProjection(route, visible);
    }

    private void CollectWorldCollisionLayers(Node node)
    {
        if (node is CollisionObject3D body) _worldCollisionLayers.Add(body, body.CollisionLayer);
        foreach (var child in node.GetChildren()) CollectWorldCollisionLayers(child);
    }
}
