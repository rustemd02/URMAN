using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Table-driven MAP-010 spawn matrix. Every declared spawn in
/// <see cref="Act1WorldLayout"/> is applied through the production
/// <c>Main.SwitchZone</c> connected-world path and must land the player at the
/// declared transform, face the declared yaw, keep RuntimeBridge in sync, and
/// place the player over floor and outside geometry — no teleport helpers and
/// no test-owned state.
/// </summary>
public partial class Act1SpawnMatrixSmokeTest : Node
{
    private const float PositionTolerance = 0.02f;
    private const float FloorProbeHeight = 1.5f;
    private const float FloorProbeDistance = 2.0f;
    private const float ClutterProbeHeight = 1.2f;
    private const float ClutterProbeRadius = 0.35f;

    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn");
        var main = packed?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Spawn matrix could not instantiate main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (player is null || bridge is null)
        {
            Fail("Spawn matrix could not find the production player or runtime bridge.");
            return;
        }

        // Freeze gameplay physics so each declared transform is asserted
        // exactly as applied, without gravity settling between rows.
        player.SetPhysicsProcess(false);

        var checkedRows = 0;
        foreach (var placement in Act1WorldLayout.Placements)
        {
            foreach (var (spawnPointId, spawn) in placement.SpawnPoints)
            {
                main.SwitchZone(placement.ZoneId, spawnPointId);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

                if (bridge.CurrentZoneId != placement.ZoneId)
                {
                    Fail($"Spawn matrix {placement.ZoneId}@{spawnPointId}: RuntimeBridge zone drifted to {bridge.CurrentZoneId}.");
                    return;
                }

                var position = player.GlobalPosition;
                // Layout spawn positions are placement-local; the connected
                // world applies them as origin + local (TryGetWorldSpawn).
                var expectedWorld = placement.Origin + spawn.Position;
                if (position.DistanceTo(expectedWorld) > PositionTolerance)
                {
                    Fail($"Spawn matrix {placement.ZoneId}@{spawnPointId}: player at {position}, expected {expectedWorld}.");
                    return;
                }

                if (Mathf.Abs(Mathf.Wrap(main.LastSpawnYawDegrees - spawn.YawDegrees, -180f, 180f)) > 0.01f)
                {
                    Fail($"Spawn matrix {placement.ZoneId}@{spawnPointId}: yaw {main.LastSpawnYawDegrees} != declared {spawn.YawDegrees}.");
                    return;
                }

                if (!FloorBelow(player, position, out var floorDistance))
                {
                    Fail($"Spawn matrix {placement.ZoneId}@{spawnPointId}: no floor within {FloorProbeDistance}m below spawn (void spawn).");
                    return;
                }

                if (ClutterAtChestHeight(player, position))
                {
                    Fail($"Spawn matrix {placement.ZoneId}@{spawnPointId}: geometry intersects the player capsule at chest height (clipping spawn).");
                    return;
                }

                checkedRows++;
                GD.Print(
                    $"act1-spawn-matrix: {placement.ZoneId}@{spawnPointId} pos=({position.X:F2},{position.Y:F2},{position.Z:F2}) yaw={spawn.YawDegrees:F0} floor={floorDistance:F2}m");
            }
        }

        var expectedRows = Act1WorldLayout.Placements.Sum(p => p.SpawnPoints.Count);
        if (checkedRows != expectedRows)
        {
            Fail($"Spawn matrix checked {checkedRows} rows, expected {expectedRows}.");
            return;
        }

        GD.Print($"act1-spawn-matrix: PASS {checkedRows}/{expectedRows} declared spawns applied, zone-synced, floored, unclipped");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private bool FloorBelow(Node3D context, Vector3 position, out float distance)
    {
        var space = context.GetWorld3D().DirectSpaceState;
        var from = position + new Vector3(0f, FloorProbeHeight, 0f);
        var query = PhysicsRayQueryParameters3D.Create(from, from + new Vector3(0f, -(FloorProbeHeight + FloorProbeDistance), 0f));
        var hit = space.IntersectRay(query);
        if (hit.Count == 0)
        {
            distance = float.PositiveInfinity;
            return false;
        }

        distance = from.Y - (float)hit["position"].AsVector3().Y;
        return distance <= FloorProbeHeight + FloorProbeDistance;
    }

    private bool ClutterAtChestHeight(Node3D context, Vector3 position)
    {
        var space = context.GetWorld3D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new SphereShape3D { Radius = ClutterProbeRadius },
            Transform = new Transform3D(Basis.Identity, position + new Vector3(0f, ClutterProbeHeight, 0f)),
            CollisionMask = uint.MaxValue,
            CollideWithAreas = false,
            CollideWithBodies = true
        };
        return space.IntersectShape(query, maxResults: 1).Count == 0;
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
