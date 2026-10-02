using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Raw dump for the village layout audit (tools/world/audit_layout.py): every registered
/// building with its footprint and access, every visible mesh with its bounds and the
/// ground under it, and the road clearance field. Analysis happens offline.
/// </summary>
internal static class Act1LayoutAudit
{
    private static readonly string[] Skip =
    [
        "AgentB_TerrainRoadKit", "AgentB_PlantedFoliage", "AgentB_ForestRing", "KaraForestEdge",
        "BackdropGround", "BackdropFarRidge", "VillageForestGorge", "Act1People", "VillageVehicles",
    ];

    public static void Dump(Node root, string output)
    {
        var world = root.FindChild("Act1ConnectedWorld", true, false) as Act1ConnectedWorld
            ?? throw new InvalidOperationException("no world");
        var registry = world.AddressRegistry ?? throw new InvalidOperationException("no registry");
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        using var stream = File.Create(Path.Combine(output, "layout-audit.json"));
        using var json = new Utf8JsonWriter(stream);
        json.WriteStartObject();
        json.WriteStartArray("buildings");
        foreach (var b in registry.Buildings.Values)
        {
            json.WriteStartObject();
            json.WriteString("id", b.BuildingId); json.WriteString("source", b.SourceKey); json.WriteString("role", b.Role);
            json.WriteString("address", b.AddressId ?? "");
            json.WriteNumber("x", b.Position.X); json.WriteNumber("z", b.Position.Z);
            json.WriteStartArray("footprint");
            foreach (var p in b.Footprint) { json.WriteStartArray(); json.WriteNumberValue(p.X); json.WriteNumberValue(p.Z); json.WriteEndArray(); }
            json.WriteEndArray();
            var access = registry.AccessPoints.Values.FirstOrDefault(a => a.BuildingId == b.BuildingId);
            if (access is not null) { json.WriteNumber("ax", access.Position.X); json.WriteNumber("az", access.Position.Z); json.WriteString("astate", access.State); }
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteStartArray("parcels");
        foreach (var parcel in registry.Parcels.Values)
        {
            json.WriteStartObject();
            json.WriteString("id", parcel.ParcelId); json.WriteString("building", parcel.PrimaryBuildingId);
            json.WriteStartArray("polygon");
            foreach (var p in parcel.Polygon) { json.WriteStartArray(); json.WriteNumberValue(p.X); json.WriteNumberValue(p.Z); json.WriteEndArray(); }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteStartArray("meshes");
        void Walk(Node node, string path)
        {
            var name = node.Name.ToString();
            if (Skip.Contains(name)) return;
            if (node is Node3D n3 && !n3.Visible) return;
            if (node is MeshInstance3D mesh && mesh.Mesh is not null)
            {
                var box = mesh.GlobalTransform * mesh.GetAabb();
                if (box.Size.Length() < 80f)
                {
                    var c = box.GetCenter();
                    json.WriteStartObject();
                    json.WriteString("p", path + "/" + name);
                    json.WriteNumber("x0", Math.Round(box.Position.X, 2)); json.WriteNumber("x1", Math.Round(box.End.X, 2));
                    json.WriteNumber("y0", Math.Round(box.Position.Y, 2)); json.WriteNumber("y1", Math.Round(box.End.Y, 2));
                    json.WriteNumber("z0", Math.Round(box.Position.Z, 2)); json.WriteNumber("z1", Math.Round(box.End.Z, 2));
                    json.WriteNumber("g", Math.Round(AgentBAct1HeightField.CollisionGround(c.X, c.Z), 2));
                    json.WriteEndObject();
                }
            }
            foreach (var child in node.GetChildren()) Walk(child, path + "/" + name);
        }
        Walk(world, "");
        json.WriteEndArray();
        json.WriteStartArray("road");   // [x, z, clearance] every metre; clearance < 0 is carriageway
        for (var x = -64f; x <= 150f; x += 1f)
        for (var z = -100f; z <= 232f; z += 1f)
        {
            var (d, h) = AgentBAct1HeightField.RoadInfo(x, z);
            if (d - h > 3) continue;
            json.WriteStartArray(); json.WriteNumberValue(x); json.WriteNumberValue(z); json.WriteNumberValue(Math.Round(d - h, 2)); json.WriteEndArray();
        }
        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();
        GD.Print($"layout-audit: written {Path.Combine(output, "layout-audit.json")} buildings={registry.Buildings.Count}");
    }
}
