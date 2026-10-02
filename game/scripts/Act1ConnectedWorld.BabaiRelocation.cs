using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Relayout v3, stage 5 (author 2026-10-01/02: «дом бабая вывести к дороге, чтобы прямо на
/// дороге стоял»; «на территории дедушки очень много хозпостроек — все снеси»).
/// The yard is authored by many builders in its old frame, deep behind the street. Once
/// they have all run, the household is carried as one rigid body by
/// <see cref="BabaiRelocation"/>: the house with its interior, the bath, the workshop, gates,
/// fences, yard props, the interactions standing in the yard and their collision. The ground
/// under the new footprint is the old yard's own ground (see AgentBAct1HeightField.Ground),
/// so nothing needs re-seating. The outbuildings are removed, the bath goes to the back of
/// the yard, the watching-forms edge fragment stands beyond the back fence, and whatever
/// stood on the new plot before is cleared.
/// </summary>
public partial class Act1ConnectedWorld
{
    private static readonly string[] BabaiRelocationSkipped =
    [
        "AgentB_TerrainRoadKit", "AgentB_TerrainCollision", "AgentB_PlantedFoliage", "AuthoredWorldDirector",
        "SettlementAddressPresentation", "StreetFrontages", "VillageVehicles", "VillageForestGorge", "VillageRavine",
        "SovkhozSquare", "KaraForestEdge", "TamaraFenceQuest", "MainStreetSnowBanks",
    ];

    private void RelocateBabaiHousehold()
    {
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var kit = core.GetNode<Node3D>("Act1AuthoredExteriorKitPresentation");
        // Outbuildings go: the loft barn, the street shed and the second "banya yard" parcel
        // whose dwelling the real bath already replaced. Their two discoveries go with them.
        foreach (var path in new[] { "BabaiYardAuthoredShed", "DistantPerimeterParcels/PerimeterWestStreetShed",
                     "DistantPerimeterParcels/BabaiEastDepthParcel",
                     "BabaiYardAuthoredParcels/BabaiYardWestDepthBanyaYardParcel" })
            if (kit.GetNodeOrNull(path) is { } outbuilding)
            {
                HidePresentationNode(outbuilding);
                outbuilding.SetMeta("suppressionReason", "relayout v3 stage 5: babai yard keeps only house, bath, workshop and firewood");
            }
        foreach (var target in FindDescendants<InteractionTarget>(this).Where(target =>
                     target.Name.ToString() is "Discovery_shed-loft-roofline" or "Discovery_underdeck-rattle").ToArray())
            RetireInteraction(target, "the loft barn was removed with the other outbuildings");

        var transform = BabaiRelocation.Transform;
        var moved = new List<Node3D>();
        void Move(Node3D node)
        {
            node.GlobalTransform = transform * node.GlobalTransform;
            node.SetMeta("babaiRelocated", true);
            moved.Add(node);
        }
        Move(_zoneInstances["house_old_pc"]);
        // The household's own builder roots move whole, whatever their bounds (the roof and
        // eaves of the house reach past the old yard line).
        foreach (var root in core.GetChildren().OfType<Node3D>().Concat(kit.GetChildren().OfType<Node3D>()).ToArray())
        {
            var name = root.Name.ToString();
            if ((name.StartsWith("Babai", StringComparison.Ordinal) || name is "YardRepairCorner" or "WatchingFormsPilot" or "RearYardGate")
                && !name.Contains("Neighbor", StringComparison.Ordinal) && name != "BabaiYardAuthoredParcels")
                Move(root);
        }
        if (core.GetNodeOrNull<Node3D>("HouseExteriorApproach") is { } approach)
            foreach (var root in approach.GetChildren().OfType<Node3D>().Where(n => n.Name.ToString().StartsWith("Babai", StringComparison.Ordinal)
                         && !n.Name.ToString().Contains("Neighbor", StringComparison.Ordinal)).ToArray())
                Move(root);
        // Blockout volumes and stray scrap of the old yard: the bath, the workshop and the
        // firewood are the yard now.
        if (core.GetNodeOrNull("BabaiEbiYard") is { } oldYard)
            foreach (var junk in oldYard.GetChildren().Where(n => n.Name.ToString() is "BabaiYardBarnVolume" or "BabaiYardBackHouseVolume"
                         or "BabaiYardToolShed" or "BabaiYardHaystack").ToArray())
                HidePresentationNode(junk);
        if (core.GetNodeOrNull("HouseExteriorApproach/BabaiEbiHouseSideOutbuilding") is { } sideShed) HidePresentationNode(sideShed);
        // Everything else that stands wholly inside the old yard moves, at the highest level
        // at which it does: a whole builder root where possible, single pieces of shared
        // owners (one architecture body, one zone) otherwise.
        var inside = new Dictionary<Node, bool>();
        bool Inside(Node node)
        {
            if (inside.TryGetValue(node, out var known)) return known;
            var result = node is Node3D spatial && !Skipped(node) && OwnInside(spatial);
            if (result)
                foreach (var child in node.GetChildren())
                    if (!Inside(child) && child is Node3D) { result = false; }
            // Evaluate every child once so the second pass can pick maximal subtrees.
            foreach (var child in node.GetChildren()) Inside(child);
            inside[node] = result;
            return result;
        }
        bool Skipped(Node node)
        {
            var name = node.Name.ToString();
            return BabaiRelocationSkipped.Contains(name) || name.Contains("Neighbor", StringComparison.Ordinal)
                || node is MultiMeshInstance3D || node.HasMeta("babaiRelocated")
                || node is CollisionShape3D { Shape: ConcavePolygonShape3D or HeightMapShape3D } && !node.HasMeta("authoredSourceMesh");
        }
        static bool OwnInside(Node3D node)
        {
            // Architecture contacts are baked in world space under shapes at the origin:
            // judge a shape by its real bounds, not by its node position.
            if (node is CollisionShape3D { Shape: { } shape } collision && shape is not HeightMapShape3D)
            {
                var box = collision.GlobalTransform * shape.GetDebugMesh().GetAabb();
                if (box.Size.Length() > 40f) return false;
                foreach (var x in new[] { box.Position.X, box.End.X })
                foreach (var z in new[] { box.Position.Z, box.End.Z })
                    if (!BabaiRelocation.InsideOldYard(x, z, .6f)) return false;
                return true;
            }
            if (node is VisualInstance3D visual && node is not Light3D)
            {
                var box = visual.GlobalTransform * visual.GetAabb();
                if (box.Size.Length() > 40f) return false;
                foreach (var x in new[] { box.Position.X, box.End.X })
                foreach (var z in new[] { box.Position.Z, box.End.Z })
                    if (!BabaiRelocation.InsideOldYard(x, z, .6f)) return false;
                return true;
            }
            var origin = node.GlobalPosition;
            return BabaiRelocation.InsideOldYard(origin.X, origin.Z);
        }
        void Collect(Node node)
        {
            if (Skipped(node)) return;
            if (node is Node3D spatial && Inside(node)) { Move(spatial); return; }
            foreach (var child in node.GetChildren().ToArray()) Collect(child);
        }
        foreach (var root in new Node[] { core, _zoneInstances["village_day"] }
                     .Concat(GetChildren().Where(child => child.Name.ToString() is "CarryCoordinator" or "Act1People")))
            Collect(root);

        // The bath (and the firewood and old spinner beside it) stand at the back of the yard,
        // not beside the house on the street; the edge fragment stands beyond the back fence.
        var bathCentre = _bathhouse!.GlobalPosition;
        foreach (var node in moved.ToArray())
        {
            Vector3 offset;
            if (node.Name == "WatchingFormsPilot") offset = new(-13.5f, 0, 0);
            else if (node == _bathhouse || node.GlobalPosition.DistanceTo(bathCentre) < 4.6f
                     || node.Name.ToString() is "BabaiFirewoodShelter" or "BabaiYardAuthoredWoodpile")
                offset = new(-7.5f, 0, -.5f);
            else continue;
            var before = node.GlobalPosition;
            var after = before + offset;
            var lift = AgentBAct1HeightField.CollisionGround(after.X, after.Z) - AgentBAct1HeightField.CollisionGround(before.X, before.Z);
            node.GlobalPosition = after + Vector3.Up * lift;
        }
        if (core.GetNodeOrNull<Node3D>("WatchingFormsPilot") is { } forms)
            foreach (var piece in FindDescendants<Node3D>(forms).Where(n => n.GetParent() is Node3D p && (p.Name == "MenacingEdgeFragment" || p == forms)).ToArray())
            {
                var at = piece.GlobalPosition;
                var floor = AgentBAct1HeightField.CollisionGround(at.X, at.Z);
                if (piece.Name.ToString().StartsWith("EdgeTrunk_", StringComparison.Ordinal)) continue;   // tall trunks carry their own base
                if (piece.Name == "YardDeadSnag") piece.GlobalPosition = at with { Y = floor };
            }

        // Clear the new plot of what stood there before: houses, sheds, fences, trees.
        var cleared = 0;
        var newInside = new Dictionary<Node, bool>();
        bool OnNewPlot(Node node)
        {
            if (newInside.TryGetValue(node, out var known)) return known;
            var result = node is Node3D spatial && !node.HasMeta("babaiRelocated") && !Skipped(node)
                && node is not InteractionTarget && OwnOnNewPlot(spatial);
            foreach (var child in node.GetChildren()) if (!OnNewPlot(child) && child is Node3D) result = false;
            newInside[node] = result;
            return result;
        }
        static bool OwnOnNewPlot(Node3D node)
        {
            if (node is VisualInstance3D visual && node is not Light3D)
            {
                var box = visual.GlobalTransform * visual.GetAabb();
                if (box.Size.Length() > 40f) return false;
                var centre = box.GetCenter();
                // Neighbours that only reach onto the plot go too: no house stands against the fence.
                return BabaiRelocation.OutsideNewYard(centre.X, centre.Z) <= Mathf.Min(4f, box.Size.Length() * .25f + .3f);
            }
            var origin = node.GlobalPosition;
            return BabaiRelocation.OutsideNewYard(origin.X, origin.Z) <= .3f;
        }
        void Clear(Node node)
        {
            if (node.HasMeta("babaiRelocated") || Skipped(node) || node is InteractionTarget or RuntimeBridge) return;
            if (node is Node3D && OnNewPlot(node)
                && node.GetParent() is { } parent && parent != core && parent != _zoneInstances["village_day"])
            {
                HidePresentationNode(node);
                node.SetMeta("suppressionReason", "relayout v3 stage 5: the babai yard now stands here");
                cleared++;
                return;
            }
            foreach (var child in node.GetChildren().ToArray()) Clear(child);
        }
        foreach (var root in new Node[] { core, _zoneInstances["village_day"] }) Clear(root);
        // Foliage instances and road-kit ribbons (the old long house path) on the new plot.
        foreach (var multi in FindDescendants<MultiMeshInstance3D>(core))
        {
            if (multi.Multimesh is not { } mm) continue;
            var count = mm.VisibleInstanceCount < 0 ? mm.InstanceCount : mm.VisibleInstanceCount;
            for (var i = 0; i < count; i++)
            {
                var t = mm.GetInstanceTransform(i);
                var at = multi.GlobalTransform * t.Origin;
                if (BabaiRelocation.OutsideNewYard(at.X, at.Z) > .5f) continue;
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * .0001f), t.Origin + Vector3.Down * 40f));
            }
        }
        if (core.FindChild("URMAN_AgentB_TerrainRoadKit", true, false) is Node3D roadKit)
            foreach (var mesh in FindDescendants<MeshInstance3D>(roadKit).Where(m => m.Name != "Terrain_Main" && m.Mesh is ArrayMesh).ToArray())
            {
                ClipMeshTriangles(mesh, p => BabaiRelocation.OutsideNewYard(p.X, p.Z) <= 0f);
                // A raised shoulder carries its own exact contact: it follows the clipped surface.
                foreach (var contact in FindDescendants<CollisionShape3D>(mesh).Where(c => c.HasMeta("authoredSourceMesh")))
                    contact.Shape = mesh.Mesh.CreateTrimeshShape();
            }

        // H018's old house reached onto the yard; the household lives on its plan plot now.
        if (kit.GetNodeOrNull("NeighborParcels/MainStreet/ForwardWestParcel") is { } h018) HidePresentationNode(h018);
        core.GetNode<AgentBAct1ExteriorLayer>("AgentBExteriorWorld").RecutOccupiedRoomTerrain(_zoneInstances["house_old_pc"], new Vector2(
            StyleBenchmarkInteriorFactory.ClearWidth * .5f, StyleBenchmarkInteriorFactory.ClearDepth * .5f));
        SetMeta("babaiRelocatedNodes", moved.Count);
        GD.Print($"act1-babai-relocation: moved={moved.Count} cleared={cleared} door=({BabaiRelocation.DoorX},{BabaiRelocation.DoorZ}) yaw+={BabaiRelocation.YawDegrees:0.00}");
        if (System.Environment.GetEnvironmentVariable("URMAN_BABAI_DEBUG") == "1")
            foreach (var node in moved) GD.Print($"act1-babai-moved: {node.GetPath()}");
    }

    private static void RetireInteraction(InteractionTarget target, string reason)
    {
        target.SetMeta("connectedWorldHidden", true);
        target.SetMeta("suppressionReason", reason);
        target.Visible = false;
        target.CollisionLayer = 0;
        target.CollisionMask = 0;
    }

    /// <summary>Drops every triangle whose centre satisfies <paramref name="drop"/>.</summary>
    private static void ClipMeshTriangles(MeshInstance3D mesh, Func<Vector3, bool> drop)
    {
        if (mesh.Mesh is not ArrayMesh source) return;
        var result = new ArrayMesh();
        var changed = false;
        for (var s = 0; s < source.GetSurfaceCount(); s++)
        {
            var arrays = source.SurfaceGetArrays(s);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
                ? Enumerable.Range(0, vertices.Length).ToArray() : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            var kept = new List<int>(indices.Length);
            for (var t = 0; t + 2 < indices.Length; t += 3)
            {
                var centre = mesh.ToGlobal((vertices[indices[t]] + vertices[indices[t + 1]] + vertices[indices[t + 2]]) / 3f);
                if (drop(centre)) { changed = true; continue; }
                kept.AddRange([indices[t], indices[t + 1], indices[t + 2]]);
            }
            if (kept.Count == 0) continue;
            arrays[(int)Mesh.ArrayType.Index] = kept.ToArray();
            result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, source.SurfaceGetMaterial(s));
        }
        if (changed) mesh.Mesh = result;
    }
}
