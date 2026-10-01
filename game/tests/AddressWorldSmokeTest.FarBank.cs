using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

public partial class AddressWorldSmokeTest
{
    // The ordinary selected-address proof below walks, reads, saves/loads and
    // returns for every household. These checks also catch a visually joined
    // lane whose centreline never intersects the shared navigation graph.
    private string[] CheckOpenPartBindings(SettlementRegistry registry)
    {
        var ids = CheckPlotBindings(registry, AgentBAct1Layout.OpenPartPlotPath);
        var graph = registry.Graph;
        var origin = graph.NodeAt(graph.Roads["authored/main-axis"].Points[0])!;
        var connected = graph.ReachableNodes(origin, SettlementTravelMode.Foot);
        foreach (var road in Act1ConnectedWorld.OpenPartPlot().Roads)
        {
            var edges = graph.Edges.Values.Where(edge => edge.RoadId == "authored/" + road.Id).ToArray();
            Check(edges.Length > 0 && edges.All(edge => connected.Contains(edge.A) && connected.Contains(edge.B)),
                road.Id + ": entire street joins the main street graph");
        }
        return ids;
    }

    private string[] CheckFarBankBindings(SettlementRegistry registry)
    {
        var ids = CheckPlotBindings(registry, AgentBAct1HeightField.FarBankPlotPath).ToList();
        return CheckFarBankStreetsAndPublic(registry, ids);
    }

    private string[] CheckPlotBindings(SettlementRegistry registry, string path)
    {
        using var plot = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(path));
        var ids = new List<string>();
        foreach (var entity in plot.RootElement.GetProperty("entities").EnumerateArray())
        {
            if (!entity.GetProperty("params").TryGetProperty("address", out var binding)) continue;
            var id = binding.GetProperty("id").GetString()!;
            ids.Add(id);
            var root = _world.AuthoredWorld!.ObjectRoot(entity.GetProperty("id").GetString()!)!;
            Require(registry.TryResolve(id, out var address) && root.GetMeta("address_id").AsString() == id,
                id + ": plot parcel has its stable address");
            Check(address.StreetId == binding.GetProperty("street").GetString()
                && address.HouseNumber == binding.GetProperty("number").GetString()
                && registry.Parcels[address.ParcelId].GameCadastralId == binding.GetProperty("cadastral").GetString(),
                id + ": street, number and cadastral identity follow the source plot");
            Check(root.GetNode<StaticBody3D>("Collision").GetChildren().OfType<CollisionShape3D>()
                .Any(shape => shape.Shape is ConcavePolygonShape3D), id + ": actual parcel surface collision is present");
            Check(Descendants(_world).OfType<AddressSignVisualComponent>().Count(sign => sign.AddressId == id) == 1,
                id + ": exactly one address plate is mounted");
        }
        return ids.ToArray();
    }

    private string[] CheckFarBankStreetsAndPublic(SettlementRegistry registry, List<string> ids)
    {
        var graph = registry.Graph;
        var first = graph.Roads["authored/yar-north"].Points[0];
        var origin = graph.NodeAt(first)!;
        var connected = graph.ReachableNodes(origin, SettlementTravelMode.Foot);
        foreach (var road in Act1ConnectedWorld.FarBankPlot().Roads)
        {
            var edges = graph.Edges.Values.Where(edge => edge.RoadId == "authored/" + road.Id).ToArray();
            Check(edges.Length > 0 && edges.All(edge => connected.Contains(edge.A) && connected.Contains(edge.B)),
                road.Id + ": entire lane joins the far-bank street graph");
        }
        foreach (var spec in Act1ConnectedWorld.FarBankPlot().Buildings)
        {
            var building = _world.FindChild("FarBankPublic_" + spec.Id.Replace("far-", ""), true, false) as Node3D
                ?? throw new InvalidOperationException(spec.Id + ": public building is missing");
            Check(building.GetNode<Label3D>("NameBoardText").Text == spec.Label.ToUpperInvariant(),
                spec.Id + ": board names the correct public building");
            Check(building.GetNode<StaticBody3D>("FarBankPublicBody").CollisionLayer != 0,
                spec.Id + ": walls, foundation and porch have physical collision");
            var approach = building.HasMeta("porchFoot") ? building.GetMeta("porchFoot").AsVector3()
                : building.ToGlobal(new Vector3(0, 0, spec.Size.Y * .5f + 2.1f));
            approach.Y = AgentBAct1HeightField.CollisionGround(approach.X, approach.Z);
            using var probe = new AddressWalkProbe(_world);
            var supported = probe.TrySupport(approach, out var start);
            Check(supported, spec.Id + ": standing approach is supported and clear");
            var reached = supported;
            var feet = start;
            for (var step = 0; step < 60 && reached && building.ToLocal(feet).Z > spec.Size.Y * .5f + .65f; step++)
                reached = probe.TryAdvance(feet, -building.GlobalBasis.Z.Normalized() * .08f, out feet);
            // On the porch: within a capsule radius of the door and up at floor level.
            reached = reached && building.ToLocal(feet).Z <= spec.Size.Y * .5f + .75f && feet.Y >= building.GlobalPosition.Y - .12f;
            Check(reached, spec.Id + ": standing capsule reaches the real porch from outside");
            _checks.Add(new { kind = "far-bank-public", spec.Id, approach = P(approach), porchReached = reached,
                rejection = probe.LastRejection, decorativeExterior = true, interiorImplemented = false });
        }
        return ids.ToArray();
    }
}
