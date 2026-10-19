using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>Physical presentation of the family courtyard inside P-HOUSE.</summary>
public partial class W03Controller : Node3D
{
    private const string HouseSceneId = "urman.fullgame:scene/pw-house";
    private RuntimeBridge? _bridge;
    private readonly Dictionary<CollisionObject3D, uint> _worldCollisionLayers = [];

    public override void _Ready()
    {
        AddToGroup("w03_presentation");
        BuildHouseWorld();
        BuildHouseGameplay();
        CollectWorldCollisionLayers(_world);
        TryBindRuntimeBridge();
        ApplyProjection();
    }

    public override void _Process(double delta)
    {
        TryBindRuntimeBridge();
        ProcessHouseWorldGuard();
        ProcessHouseGameplay(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        ProcessHouseSpatialLinks();
    }

    public override void _ExitTree()
    {
        ExitHouseGameplay();
        ExitHouseWorldGuard();
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
        // Standalone scene inspection keeps the authored courtyard visible.
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)) return;
        var route = bridge.CurrentFullGamePhotoWorldRouteProjection();
        var visible = route is not null
            && ReferenceEquals(route.SessionIdentity, bridge.PhotoWorldPresentationIdentity)
            && route.ActiveSceneId == HouseSceneId;
        _world.Visible = visible;
        foreach (var (body, layer) in _worldCollisionLayers)
        {
            if (!GodotObject.IsInstanceValid(body)) continue;
            if (body is InteractionTarget target) target.SetPresentationEnabled(visible);
            else body.CollisionLayer = visible ? layer : 0u;
        }
        ApplyHouseGameplayProjection(route, visible);
        ApplyHouseWorldGuardProjection(route, visible);
    }

    private void CollectWorldCollisionLayers(Node node)
    {
        if (node is CollisionObject3D body) _worldCollisionLayers.Add(body, body.CollisionLayer);
        foreach (var child in node.GetChildren()) CollectWorldCollisionLayers(child);
    }
}
