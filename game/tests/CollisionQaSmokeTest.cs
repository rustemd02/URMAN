using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Small physics contract check for every authored full-game zone.
/// It does not claim final collision quality; it proves that a zone has a
/// walkable layer-1 floor, that the player can query it, and that provisional
/// kit proxies stay isolated on layer 2.
/// </summary>
public partial class CollisionQaSmokeTest : Node
{
    private static readonly string[] FullGameZones =
    [
        "fullgame_act2_house",
        "fullgame_act2_river",
        "fullgame_act2_mosque",
        "fullgame_act2_council",
        "fullgame_act3_archive",
        "fullgame_act3_soviet",
        "fullgame_act3_water",
        "fullgame_act4_tukay",
        "fullgame_act4_1552",
        "fullgame_act4_pact",
        "fullgame_act5_boundary",
        "fullgame_act5_epilogue"
    ];

    public override async void _Ready()
    {
        PainterlyMaterialLibrary.SuppressTextureLoadsForHeadlessTests = true;
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/full_game.tscn");
        var main = packed?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Collision QA could not instantiate the full-game scene.");
            return;
        }

        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        foreach (var zoneId in FullGameZones)
        {
            main.SwitchZone(zoneId, "entry");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var zone = main.GetNode<Node3D>("ZoneHost").GetChild(0) as FullGameZone;
            if (zone is null || !HasWalkableFloor(zone) || !HasAuthoredWalkablePath(zone) || !GroundRayHits(zone))
            {
                Fail($"Collision QA failed for {zoneId}: layer-1 floor or authored walkable path is not queryable.");
                return;
            }

            if (!HasIsolatedKitProxy(zone))
            {
                Fail($"Collision QA failed for {zoneId}: provisional kit proxy is not isolated on layer 2.");
                return;
            }
        }

        GD.Print("collision-qa-smoke: 12 full-game zones have queryable layer-1 floors; kit proxies isolated on layer 2");
        await GodotSmokeCleanup.ReleaseAsync(main);
        PainterlyMaterialLibrary.SuppressTextureLoadsForHeadlessTests = false;
        GetTree().Quit(0);
    }

    private static bool HasWalkableFloor(FullGameZone zone)
    {
        var ground = zone.GetNodeOrNull<StaticBody3D>("Ground");
        return ground is not null
               && ground.CollisionLayer == 1
               && ground.CollisionMask == 1
               && ground.GetChildren().OfType<CollisionShape3D>().Count() == 1;
    }

    private static bool GroundRayHits(FullGameZone zone)
    {
        var query = PhysicsRayQueryParameters3D.Create(
            new Vector3(8, 3, 8),
            new Vector3(8, -1, 8));
        query.CollisionMask = 1;
        var result = zone.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return result.Count > 0;
    }

    private static bool HasAuthoredWalkablePath(FullGameZone zone)
    {
        var path = zone.GetNodeOrNull<StaticBody3D>("WalkablePath");
        var surface = path?.GetNodeOrNull<MeshInstance3D>("ReliefSurface");
        return path is not null
               && surface?.Mesh is ArrayMesh mesh
               && mesh.GetSurfaceCount() == 1
               && path.GetMeta("status").AsString() == "authored-road-relief-production-candidate"
               && path.GetMeta("reliefGrid").AsString() == "9x28"
               && path.GetChildren().OfType<CollisionShape3D>().Count() == 216;
    }

    private static bool HasIsolatedKitProxy(FullGameZone zone)
    {
        var kit = zone.GetNodeOrNull<Node3D>("ProductionDressing/GeneratedModularKit");
        if (kit is null)
        {
            return true;
        }

        var proxy = kit.GetNodeOrNull<StaticBody3D>("KitCollisionProxy");
        return proxy is not null
               && proxy.CollisionLayer == 2
               && proxy.CollisionMask == 0
               && proxy.GetChildren().OfType<CollisionShape3D>().Any();
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
