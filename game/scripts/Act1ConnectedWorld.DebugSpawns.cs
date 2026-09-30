using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    /// <summary>
    /// Standing spots for the menu's debug jumps into places that have no
    /// layout spawn point of their own. Every position and facing comes from
    /// the owner that already computed it — the public buildings' measured
    /// outside step, the facility door records, the bathhouse's own entry
    /// flight and the village address registry — so nothing is copied into a
    /// second coordinate table and a moved building takes its jump with it.
    /// </summary>
    private bool TryGetPlaceDebugSpawn(
        string zoneId,
        string spawnPointId,
        out Act1WorldLayout.SpawnTransform spawn)
    {
        spawn = default;
        if (zoneId == "village_day")
        {
            if (spawnPointId == "school" && _squareSchool is not null)
            {
                var outside = _squareSchool.ToGlobal(new Vector3(0, 0, 9f));
                outside.Y = Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(outside.X, outside.Z) + .035f;
                var archiveDoor = _squareSchool.ToGlobal(new Vector3(0, 1.2f, 5.6f));
                spawn = new(outside, YawTowards(archiveDoor - outside));
                return true;
            }
            // Shop, school annex and council: the measured spot outside their own
            // entrance, turned to face that entrance.
            var room = _publicBuildings.FirstOrDefault(building => building.Id == spawnPointId);
            if (room is not null)
            {
                spawn = new(room.Outside, YawTowards(room.Entrance - room.Outside));
                return true;
            }

            // The bathhouse and the mosque: own door record for the facing,
            // own entry point for the feet.
            var doorKey = spawnPointId switch
            {
                "bathhouse" => "bathhouse/entrance",
                "mosque" => "mosque/entrance",
                _ => null
            };
            if (doorKey is null) return false;
            var door = _facilityDoors.FirstOrDefault(candidate => candidate.Key == doorKey);
            if (door is null || StandingSpot(spawnPointId) is not { } standing) return false;
            spawn = new(standing, YawTowards(door.Hinge.GlobalPosition - standing));
            return true;
        }

        if ((zoneId, spawnPointId) is not ("kara_urman_night", "forest-approach")) return false;
        if (FindChild("KaraForestApproachEndpoint", true, false) is not Node3D endpoint
            || !Act1WorldLayout.TryGetWorldSpawn(zoneId, "village_path", out var villagePath))
        {
            return false;
        }

        // Stand on the road a couple of metres before the interaction, facing
        // the forest, instead of inside the box that starts the transition.
        var along = ToGlobal(villagePath.Position).DirectionTo(endpoint.GlobalPosition);
        along.Y = 0;
        if (along.LengthSquared() < .001f) return false;
        along = along.Normalized();
        spawn = new(AddressGround(endpoint.GlobalPosition - along * 2.4f), YawTowards(along));
        return true;
    }

    /// <summary>The entry point each of these two buildings already recorded.</summary>
    private Vector3? StandingSpot(string spawnPointId) => spawnPointId switch
    {
        "bathhouse" when _bathhouse is not null
            && _bathhouse.HasMeta("entryApproach")
            && _bathhouse.GetMeta("entryApproach").VariantType == Variant.Type.Vector3
            => _bathhouse.GetMeta("entryApproach").AsVector3(),
        "mosque" when AddressRegistry is not null
            && AddressRegistry.TryResolve("ADR-MOSQUE", out var record)
            && AddressRegistry.AccessPoints.TryGetValue(record.AccessId, out var access)
            => AddressVector(access.Position),
        _ => null
    };

    /// <summary>Godot yaw for a node whose forward is -Z, turned to face a direction.</summary>
    private static float YawTowards(Vector3 direction) => Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Z));
}
