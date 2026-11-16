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
        foreach (var job in RelocateBabaiHouseholdJobs()) job();
    }

    private IEnumerable<Action> RelocateBabaiHouseholdJobs()
    {
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var kit = core.GetNode<Node3D>("Act1AuthoredExteriorKitPresentation");
        var transform = BabaiRelocation.Transform;
        var moved = new List<Node3D>();
        var cleared = 0;
        void Move(Node3D node)
        {
            node.GlobalTransform = transform * node.GlobalTransform;
            node.SetMeta("babaiRelocated", true);
            moved.Add(node);
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
            // Keep the same world-space shape/visual bounds and margins.
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
        static bool OwnOnNewPlot(Node3D node)
        {
            if (node is VisualInstance3D visual && node is not Light3D)
            {
                var box = visual.GlobalTransform * visual.GetAabb();
                if (box.Size.Length() > 40f) return false;
                var centre = box.GetCenter();
                return BabaiRelocation.OutsideNewYard(centre.X, centre.Z) <= Mathf.Min(4f, box.Size.Length() * .25f + .3f);
            }
            var origin = node.GlobalPosition;
            return BabaiRelocation.OutsideNewYard(origin.X, origin.Z) <= .3f;
        }

        IEnumerable<Action> PrepareHousehold()
        {
            foreach (var path in new[] { "BabaiYardAuthoredShed", "DistantPerimeterParcels/PerimeterWestStreetShed",
                         "DistantPerimeterParcels/BabaiEastDepthParcel", "BabaiYardAuthoredParcels/BabaiYardWestDepthBanyaYardParcel" })
                yield return () =>
                {
                    if (kit.GetNodeOrNull(path) is not { } outbuilding) return;
                    HidePresentationNode(outbuilding);
                    outbuilding.SetMeta("suppressionReason", "relayout v3 stage 5: babai yard keeps only house, bath, workshop and firewood");
                };
            // Keep the scan order without retaining a second array of all descendants.
            var targets = FindDescendants<Node>(this).GetEnumerator();
            using (targets)
            {
                var done = false;
                while (!done)
                    yield return () =>
                    {
                        for (var count = 0; count < 128; count++)
                        {
                            if (!targets.MoveNext()) { done = true; break; }
                            if (targets.Current is InteractionTarget target
                                && target.Name.ToString() is ("Discovery_shed-loft-roofline" or "Discovery_underdeck-rattle"))
                                RetireInteraction(target, "the loft barn was removed with the other outbuildings");
                        }
                    };
            }
            yield return () => Move(_zoneInstances["house_old_pc"]);
            foreach (var root in core.GetChildren().OfType<Node3D>().Concat(kit.GetChildren().OfType<Node3D>()))
            {
                var name = root.Name.ToString();
                if ((name.StartsWith("Babai", StringComparison.Ordinal) || name is "YardRepairCorner" or "WatchingFormsPilot" or "RearYardGate")
                    && !name.Contains("Neighbor", StringComparison.Ordinal) && name != "BabaiYardAuthoredParcels")
                    yield return () => Move(root);
            }
            if (core.GetNodeOrNull<Node3D>("HouseExteriorApproach") is { } approach)
                foreach (var root in approach.GetChildren().OfType<Node3D>().Where(n => n.Name.ToString().StartsWith("Babai", StringComparison.Ordinal)
                             && !n.Name.ToString().Contains("Neighbor", StringComparison.Ordinal)))
                    yield return () => Move(root);
            if (core.GetNodeOrNull("BabaiEbiYard") is { } oldYard)
                foreach (var junk in oldYard.GetChildren().Where(n => n.Name.ToString() is "BabaiYardBarnVolume" or "BabaiYardBackHouseVolume"
                             or "BabaiYardToolShed" or "BabaiYardHaystack"))
                    yield return () => HidePresentationNode(junk);
            yield return () =>
            {
                if (core.GetNodeOrNull("HouseExteriorApproach/BabaiEbiHouseSideOutbuilding") is { } sideShed)
                    HidePresentationNode(sideShed);
            };
        }

        // A resumable depth-first equivalent of the original recursive Inside.
        // Each eligible node reads GetChildren once. Retain only the active
        // traversal stack, dispose its native arrays on completion or cancellation.
        IEnumerable<Action> CacheBounds(Node[] roots, Dictionary<Node, bool> cache, bool newPlot)
        {
            var frames = new Stack<(Node Node, global::Godot.Collections.Array<Node> Children, int Next, bool Result)>();
            void Push(Node node)
            {
                if (cache.ContainsKey(node)) return;
                if (Skipped(node) || newPlot && node is (InteractionTarget or RuntimeBridge))
                { cache[node] = false; return; }
                var own = node is Node3D spatial && (newPlot ? OwnOnNewPlot(spatial) : OwnInside(spatial));
                frames.Push((node, node.GetChildren(), 0, own));
            }
            try
            {
                foreach (var root in roots)
                {
                    yield return () => Push(root);
                    while (frames.Count > 0)
                        yield return () =>
                        {
                            for (var work = 0; work < 128 && frames.Count > 0; work++)
                            {
                                var frame = frames.Pop();
                                if (frame.Next >= frame.Children.Count)
                                {
                                    cache[frame.Node] = frame.Result;
                                    ((global::Godot.Collections.Array)frame.Children).Dispose();
                                    continue;
                                }
                                var child = frame.Children[frame.Next];
                                if (!cache.TryGetValue(child, out var childInside))
                                {
                                    frames.Push(frame);
                                    Push(child);
                                    continue;
                                }
                                if (!childInside && child is Node3D) frame.Result = false;
                                frame.Next++;
                                frames.Push(frame);
                            }
                        };
                }
            }
            finally
            {
                foreach (var frame in frames) ((global::Godot.Collections.Array)frame.Children).Dispose();
            }
        }

        IEnumerable<Action> VisitCached(Node[] roots, Dictionary<Node, bool> cache, bool clear)
        {
            var pending = new Stack<Node>();
            for (var index = roots.Length - 1; index >= 0; index--) pending.Push(roots[index]);
            while (pending.Count > 0)
                yield return () =>
                {
                    var wholeSubtrees = 0;
                    for (var work = 0; work < 128 && pending.Count > 0 && wholeSubtrees < 4; work++)
                    {
                        var node = pending.Pop();
                        if (Skipped(node) || clear && node is (InteractionTarget or RuntimeBridge)) continue;
                        var eligible = node is Node3D && cache.GetValueOrDefault(node)
                            && (!clear || node.GetParent() is { } parent && parent != core && parent != _zoneInstances["village_day"]);
                        if (eligible)
                        {
                            if (clear)
                            {
                                HidePresentationNode(node);
                                node.SetMeta("suppressionReason", "relayout v3 stage 5: the babai yard now stands here");
                                cleared++;
                            }
                            else Move((Node3D)node);
                            wholeSubtrees++;
                            continue;
                        }
                        var children = node.GetChildren();
                        using var childrenOwner = (global::Godot.Collections.Array)children;
                        for (var index = children.Count - 1; index >= 0; index--) pending.Push(children[index]);
                    }
                };
        }

        foreach (var job in ProfileAuthoredBuildJobs("relocate-babai-prepare", PrepareHousehold())) yield return job;
        var roots = new Node[] { core, _zoneInstances["village_day"] }
            .Concat(GetChildren().Where(child => child.Name.ToString() is "CarryCoordinator" or "Act1People")).ToArray();
        var inside = new Dictionary<Node, bool>();
        // All old-yard bounds finish before the first selected subtree moves.
        foreach (var job in ProfileAuthoredBuildJobs("relocate-babai-old-bounds", CacheBounds(roots, inside, false))) yield return job;
        foreach (var job in ProfileAuthoredBuildJobs("relocate-babai-move", VisitCached(roots, inside, false))) yield return job;

        var bathCentre = _bathhouse!.GlobalPosition;
        for (var start = 0; start < moved.Count; start += 8)
        {
            var from = start;
            yield return () =>
            {
                for (var index = from; index < Math.Min(from + 8, moved.Count); index++)
                {
                    var node = moved[index];
                    Vector3 offset;
                    if (node.Name == "WatchingFormsPilot") offset = new(-13.5f, 0, 0);
                    else if (node == _bathhouse || node.GlobalPosition.DistanceTo(bathCentre) < 4.6f
                             || node.Name.ToString() is "BabaiFirewoodShelter" or "BabaiYardAuthoredWoodpile") offset = new(-7.5f, 0, -.5f);
                    else continue;
                    var before = node.GlobalPosition;
                    var after = before + offset;
                    var lift = AgentBAct1HeightField.CollisionGround(after.X, after.Z) - AgentBAct1HeightField.CollisionGround(before.X, before.Z);
                    node.GlobalPosition = after + Vector3.Up * lift;
                }
            };
        }
        yield return () =>
        {
            if (core.GetNodeOrNull<Node3D>("WatchingFormsPilot/YardDeadSnag") is not { } snag) return;
            var at = snag.GlobalPosition;
            snag.GlobalPosition = at with { Y = AgentBAct1HeightField.CollisionGround(at.X, at.Z) };
        };

        var newRoots = new Node[] { core, _zoneInstances["village_day"] };
        var newInside = new Dictionary<Node, bool>();
        // This second cache is deliberately rebuilt after every move/offset.
        foreach (var job in ProfileAuthoredBuildJobs("relocate-babai-new-bounds", CacheBounds(newRoots, newInside, true))) yield return job;
        foreach (var job in ProfileAuthoredBuildJobs("relocate-babai-clear", VisitCached(newRoots, newInside, true))) yield return job;

        IEnumerable<Action> FoliageJobs()
        {
            foreach (var multi in FindDescendants<MultiMeshInstance3D>(core))
            {
                if (multi.Multimesh is not { } mm) continue;
                var count = mm.VisibleInstanceCount < 0 ? mm.InstanceCount : mm.VisibleInstanceCount;
                for (var start = 0; start < count; start += 512)
                {
                    var from = start;
                    yield return () =>
                    {
                        for (var index = from; index < Math.Min(from + 512, count); index++)
                        {
                            var pose = mm.GetInstanceTransform(index);
                            var at = multi.GlobalTransform * pose.Origin;
                            if (BabaiRelocation.OutsideNewYard(at.X, at.Z) > .5f) continue;
                            mm.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(Vector3.One * .0001f), pose.Origin + Vector3.Down * 40f));
                        }
                    };
                }
            }
        }
        foreach (var job in ProfileAuthoredBuildJobs("relocate-babai-foliage", FoliageJobs())) yield return job;
        IEnumerable<Action> RoadJobs()
        {
            if (core.GetNodeOrNull<Node3D>("AgentBExteriorWorld/AgentB_TerrainRoadKit") is not { } roadKit) yield break;
            foreach (var mesh in FindDescendants<MeshInstance3D>(roadKit).Where(m => m.Name != "Terrain_Main" && m.Mesh is ArrayMesh))
                yield return () =>
                {
                    ClipMeshTriangles(mesh, p => BabaiRelocation.OutsideNewYard(p.X, p.Z) <= 0f);
                    foreach (var contact in FindDescendants<CollisionShape3D>(mesh).Where(c => c.HasMeta("authoredSourceMesh")))
                        contact.Shape = mesh.Mesh.CreateTrimeshShape();
                };
        }
        foreach (var job in ProfileAuthoredBuildJobs("relocate-babai-road", RoadJobs())) yield return job;
        yield return () =>
        {
            if (kit.GetNodeOrNull("NeighborParcels/MainStreet/ForwardWestParcel") is { } h018) HidePresentationNode(h018);
        };
        var facade = kit.GetNode<Node3D>("BabaiApproachDwellingFacade");
        yield return () =>
        {
            core.GetNode<AgentBAct1ExteriorLayer>("AgentBExteriorWorld").ReconcileBuildingFoliage(facade);
            GD.Print($"act1-babai-foliage: owners={facade.GetMeta("roofSuppressedFoliageOwners").AsStringArray().Length} contacts={facade.GetMeta("roofSuppressedFoliageContacts").AsStringArray().Length}");
        };
        yield return () => ProfileWorldBuildStep("relocate-babai-terrain-recut", () =>
            core.GetNode<AgentBAct1ExteriorLayer>("AgentBExteriorWorld").RecutOccupiedRoomTerrain(_zoneInstances["house_old_pc"], new Vector2(
                StyleBenchmarkInteriorFactory.ClearWidth * .5f, StyleBenchmarkInteriorFactory.ClearDepth * .5f)));
        // Recut restores the terrain source; the larger house cut must follow it.
        yield return () => ProfileWorldBuildStep("relocate-babai-rebuilt-house", () => BuildBabaiRebuiltHouse(facade));
        yield return () =>
        {
            SetMeta("babaiRelocatedNodes", moved.Count);
            GD.Print($"act1-babai-relocation: moved={moved.Count} cleared={cleared} door=({BabaiRelocation.DoorX},{BabaiRelocation.DoorZ}) yaw+={BabaiRelocation.YawDegrees:0.00}");
            if (System.Environment.GetEnvironmentVariable("URMAN_BABAI_DEBUG") == "1")
                foreach (var node in moved) GD.Print($"act1-babai-moved: {node.GetPath()}");
        };
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
            using var arrays = source.SurfaceGetArrays(s);
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
