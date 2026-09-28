using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private Node3D? _collapsedOpeningBridge;
    private StaticBody3D? _intactOpeningBridge;
    private CollisionShape3D? _openingBridgeTrestle;
    private Node3D? _openingFallingSpan;
    private Vector3 _openingSpanRestPosition;

    internal Vector3 OpeningBridgeFocus => ToGlobal(new Vector3(RavineCentreX(RavineBridgeZ),
        OpeningBridgeRoadHeight(RavineCentreX(RavineBridgeZ)), RavineBridgeZ));

    private void BuildOpeningSleep()
    {
        var house = _zoneInstances["house_old_pc"];
        var cushion = FindDescendants<MeshInstance3D>(house)
            .First(mesh => mesh.Name.ToString().Contains("DaybedCushion", StringComparison.Ordinal));
        var bounds = PublicBuildingShell.Bounds(house, cushion);
        var bed = new InteractionTarget
        {
            Name = "FirstNightSleep", InteractionId = "urman.chapter1:interaction/first-night-sleep",
            Prompt = "Лечь спать до утра", CollisionLayer = 4, CollisionMask = 0,
            Position = bounds.GetCenter() + Vector3.Up * .15f
        };
        bed.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(bounds.Size.X, .3f, bounds.Size.Z) } });
        bed.AfterDispatch = async () =>
        {
            if (GetTree().GetFirstNodeInGroup("player_controller")?.GetParent()?.GetParent() is Act1DemoRoot root)
                await root.RunFirstNightAsync();
            return false;
        };
        house.AddChild(bed);
    }

    internal void SetOpeningBridgeCollapse(float progress)
    {
        if (_openingFallingSpan is null) return;
        _openingFallingSpan.Position = _openingSpanRestPosition + new Vector3(0, -7f * progress * progress, 0);
        _openingFallingSpan.Rotation = new(0, 0, .34f * progress);
    }

    internal static float OpeningBridgeRoadHeight(float x)
    {
        var cx = RavineCentreX(RavineBridgeZ);
        var local = Mathf.Clamp(x - cx, -8f, 8f);
        var near = AgentBAct1HeightField.CollisionGround(cx - 5.6f, RavineBridgeZ) + .34f;
        var far = AgentBAct1HeightField.CollisionGround(cx + 5.6f, RavineBridgeZ) + .34f;
        if (local < -5.6f) return Mathf.Lerp(AgentBAct1HeightField.CollisionGround(cx - 8f, RavineBridgeZ), near, (local + 8f) / 2.4f);
        if (local > 5.6f) return Mathf.Lerp(far, AgentBAct1HeightField.CollisionGround(cx + 8f, RavineBridgeZ), (local - 5.6f) / 2.4f);
        return Mathf.Lerp(near, far, (local + 5.6f) / 11.2f);
    }

    private void BuildIntactOpeningBridge(Node3D ravine)
    {
        var cx = RavineCentreX(RavineBridgeZ);
        _intactOpeningBridge = new StaticBody3D { Name = "RavineBridgeIntact", CollisionLayer = 2, CollisionMask = 0 };
        ravine.AddChild(_intactOpeningBridge);
        _intactOpeningBridge.SetMeta("stateOwner", "RuntimeBridge/world.props/act1/opening");
        var abutments = new Node3D { Name = "RavineBridgeAbutments", Position = _collapsedOpeningBridge!.Position };
        ravine.AddChild(abutments);
        foreach (var crib in _collapsedOpeningBridge.GetChildren().OfType<Node3D>()
            .Where(node => node.Name.ToString().Contains("Crib_", StringComparison.Ordinal)).ToArray())
            crib.Reparent(abutments, keepGlobalTransform: false);
        // Boards, support beams and rails share the actual sloping road deck.
        for (var i = 0; i < 54; i++)
        {
            var x = cx - 8f + (i + .5f) * 16f / 54f;
            AddVisualBox(_intactOpeningBridge, $"Deck{i}", new(16f / 54f, .12f, 3.2f),
                new(x, OpeningBridgeRoadHeight(x) - .06f, RavineBridgeZ), i % 4 == 0 ? "76614b" : "62513f", "wood");
        }
        foreach (var (start, end) in new[] { (-8f, -5.6f), (-5.6f, 5.6f), (5.6f, 8f) })
        {
            var a = new Vector3(cx + start, OpeningBridgeRoadHeight(cx + start), RavineBridgeZ);
            var b = new Vector3(cx + end, OpeningBridgeRoadHeight(cx + end), RavineBridgeZ);
            _intactOpeningBridge.AddChild(new CollisionShape3D
            {
                Position = (a + b) * .5f - Vector3.Up * .08f,
                Rotation = new(0, 0, Mathf.Atan2(b.Y - a.Y, b.X - a.X)),
                Shape = new BoxShape3D { Size = new(a.DistanceTo(b), .16f, 3.2f) }
            });
            foreach (var z in new[] { -1.55f, 1.55f })
            {
                AddRavineLog(_intactOpeningBridge, $"Rail{start}_{z}", a + new Vector3(0, .95f, z), b + new Vector3(0, .95f, z), .055f, "6b5a45");
                AddRavineLog(_intactOpeningBridge, $"Support{start}_{z}", a + new Vector3(0, -.22f, z * .7f), b + new Vector3(0, -.22f, z * .7f), .16f, "4f4032");
                _intactOpeningBridge.AddChild(new CollisionShape3D
                {
                    Position = (a + b) * .5f + new Vector3(0, .53f, z),
                    Rotation = new(0, 0, Mathf.Atan2(b.Y - a.Y, b.X - a.X)),
                    Shape = new BoxShape3D { Size = new(a.DistanceTo(b), 1.05f, .12f) }
                });
            }
        }
        for (var i = 0; i <= 10; i++)
        foreach (var z in new[] { -1.55f, 1.55f })
        {
            var x = cx - 8f + i * 1.6f;
            AddVisualBox(_intactOpeningBridge, $"RailPost{i}_{z}", new(.12f, 1.04f, .12f),
                new(x, OpeningBridgeRoadHeight(x) + .48f, RavineBridgeZ + z), "5b4a3a", "wood");
        }
        // Keep the abutments and approach ramps standing when the middle fails.
        _openingSpanRestPosition = new(cx, OpeningBridgeRoadHeight(cx), RavineBridgeZ);
        _openingFallingSpan = new Node3D { Name = "OpeningFallingSpan", Position = _openingSpanRestPosition };
        _intactOpeningBridge.AddChild(_openingFallingSpan);
        foreach (var part in _intactOpeningBridge.GetChildren().OfType<MeshInstance3D>()
            .Where(part => MathF.Abs(part.Position.X - cx) < 5.6f).ToArray())
            part.Reparent(_openingFallingSpan, keepGlobalTransform: true);
    }

    private void UpdateOpeningBridge()
    {
        if (_intactOpeningBridge is null || _collapsedOpeningBridge is null || _openingBridgeTrestle is null) return;
        var broken = _runtimeBridge?.FirstNightPassed ?? true;
        var outdoors = ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";
        _intactOpeningBridge.Visible = !broken;
        _intactOpeningBridge.CollisionLayer = !broken && outdoors ? 2u : 0u;
        _collapsedOpeningBridge.Visible = broken;
        _openingBridgeTrestle.SetDeferred(CollisionShape3D.PropertyName.Disabled, !broken || !outdoors);
    }
}
