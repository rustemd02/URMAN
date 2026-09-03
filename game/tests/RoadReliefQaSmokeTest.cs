using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Focused evidence for the authored benchmark-road pass. It checks that each
/// road/path has a real ArrayMesh relief, non-flat height metadata and one
/// collision cell per relief grid cell. This is presentation QA only; it does
/// not alter gameplay state or claim final production level-art acceptance.
/// </summary>
public partial class RoadReliefQaSmokeTest : Node
{
    private static readonly (string Scene, string[] Roads)[] Cases =
    [
        ("res://scenes/zones/style_benchmark_day_street.tscn", ["Road"]),
        ("res://scenes/zones/style_benchmark_kara_urman_night.tscn", ["PathNear", "PathMiddle", "PathFar"]),
        ("res://scenes/zones/chapter1_zirat_road.tscn", ["Road"]),
        ("res://scenes/zones/fullgame/act2_house.tscn", ["WalkablePath"]),
        ("res://scenes/zones/fullgame/act5_boundary.tscn", ["WalkablePath"])
    ];

    public override async void _Ready()
    {
        foreach (var (scenePath, roadNames) in Cases)
        {
            var packed = ResourceLoader.Load<PackedScene>(scenePath);
            var instance = packed?.Instantiate<Node3D>();
            if (instance is null)
            {
                Fail($"Road relief QA could not load {scenePath}.");
                return;
            }

            AddChild(instance);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (var roadName in roadNames)
            {
                if (!CheckRoad(instance, roadName, scenePath))
                {
                    return;
                }
            }

            if (scenePath.EndsWith("style_benchmark_day_street.tscn", StringComparison.Ordinal)
                && !CheckPuddleClusters(instance))
            {
                return;
            }

            await GodotSmokeCleanup.ReleaseAsync(instance);
        }

        GD.Print("road-relief-qa: 7 authored relief paths have ArrayMesh surfaces and 8x27 collision cells");
        GetTree().Quit(0);
    }

    private bool CheckRoad(Node3D scene, string roadName, string scenePath)
    {
        var road = scene.GetNodeOrNull<StaticBody3D>(roadName);
        var surface = road?.GetNodeOrNull<MeshInstance3D>("ReliefSurface");
        var mesh = surface?.Mesh as ArrayMesh;
        var grid = road?.GetMeta("reliefGrid").AsString();
        var collisionCells = road?.GetMeta("reliefCollisionCells").AsInt32() ?? 0;
        var minHeight = road?.GetMeta("reliefMinHeight").AsSingle() ?? 0f;
        var maxHeight = road?.GetMeta("reliefMaxHeight").AsSingle() ?? 0f;
        var collisionShapes = road?.GetChildren().OfType<CollisionShape3D>().Count() ?? 0;

        if (road is null
            || surface is null
            || mesh is null
            || mesh.GetSurfaceCount() != 1
            || grid != "9x28"
            || collisionCells != 216
            || collisionShapes != collisionCells
            || minHeight >= maxHeight
            || maxHeight - minHeight < 0.035f)
        {
            Fail($"Road relief QA failed for {scenePath}:{roadName}: grid={grid}, surfaces={mesh?.GetSurfaceCount() ?? 0}, collision={collisionShapes}/{collisionCells}, heights={minHeight:0.000}/{maxHeight:0.000}.");
            return false;
        }

        return true;
    }

    private bool CheckPuddleClusters(Node3D scene)
    {
        foreach (var name in new[] { "PuddleNear", "PuddleMiddle", "PuddleFar" })
        {
            var cluster = scene.GetNodeOrNull<Node3D>(name);
            var patches = cluster?.GetChildren().OfType<MeshInstance3D>().ToArray() ?? [];
            if (cluster is null
                || patches.Length != 3
                || cluster.GetMeta("puddleGeometry").AsString() != "low-poly-overlap-proxy"
                || cluster.GetMeta("wetnessMaterialStatus").AsString() != "OPEN-roughness-review")
            {
                Fail($"Road relief QA failed for puddle cluster {name}: expected 3 authored patches with an explicit open roughness gate.");
                return false;
            }
        }

        return true;
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
