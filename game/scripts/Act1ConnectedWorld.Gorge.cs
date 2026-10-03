using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Relayout v3 (author, 2026-10-01): no river. The village ends in the south at a wide,
/// deep gorge; the only way to the Kara-Urman forest is an old suspension bridge that
/// barely holds. Aidar crosses it; on his way back after the night of the blizzard it
/// gives way behind him (world prop <see cref="SuspensionBridgeStateId"/>). The terrain
/// cut lives in <see cref="AgentBAct1HeightField.RiverChannel"/>; this part dresses the
/// gorge, fences both rims and builds the bridge.
/// </summary>
public partial class Act1ConnectedWorld
{
    internal const string SuspensionBridgeStateId = "act1/suspension-bridge";
    internal const float SuspensionBridgeX = 0f;
    private Node3D? _suspensionIntact;
    private Node3D? _suspensionBroken;
    private StaticBody3D? _suspensionDeckBody;
    private StaticBody3D? _suspensionGapBlocker;
    private bool _suspensionVisitedFar;
    private bool _suspensionCollapsing;
    private readonly Dictionary<Node3D, Transform3D> _suspensionRest = new();

    internal static float GorgeCentreZ(float x) => (float)AgentBAct1HeightField.RiverMeander(x);
    internal static float GorgeNearRim(float x) => GorgeCentreZ(x) + (float)AgentBAct1HeightField.GorgeHalfWidth;
    internal static float GorgeFarRim(float x) => GorgeCentreZ(x) - (float)AgentBAct1HeightField.GorgeHalfWidth;

    /// <summary>A low faceted boulder: a coarse sphere with every corner pushed in or out by a
    /// hash of its position (so seams stay closed), flat-shaded like the rest of the kit.</summary>
    private static ArrayMesh GorgeRockMesh(int seed)
    {
        var arrays = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 7, Rings = 4 }.GetMeshArrays();
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        for (var i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            var h = Mathf.Sin(Mathf.Snapped(v.X, .001f) * 12.9898f + Mathf.Snapped(v.Y, .001f) * 78.233f
                + Mathf.Snapped(v.Z, .001f) * 37.719f + seed * 1.618f) * 43758.547f;
            h -= Mathf.Floor(h);
            vertices[i] = v * (.72f + .5f * h);
        }
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = default;
        arrays[(int)Mesh.ArrayType.Tangent] = default;
        using var raw = new ArrayMesh();
        raw.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        using var surface = new SurfaceTool();
        surface.CreateFrom(raw, 0);
        surface.Deindex();
        surface.GenerateNormals();
        return surface.Commit();
    }

    private void AddVillageGorge(Node3D core)
    {
        var gorge = new Node3D { Name = "VillageForestGorge" };
        gorge.SetMeta("presentationRole", "deep gorge between the village and the Kara-Urman forest; one suspension bridge");
        gorge.SetMeta("collisionOwner", "terrain walls; rim fences on layer 2");
        core.AddChild(gorge);
        var rock = PainterlyMaterialLibrary.ForColor("4a4641", "stone");
        // One frozen stream winds along the bottom, following the bed; rock shelves break the walls.
        using (var bed = new Curve3D { BakeInterval = .5f })
        {
            for (var x = AgentBAct1HeightField.MinX + 2f; x <= AgentBAct1HeightField.MaxX - 2f; x += 2.5f)
                bed.AddPoint(new Vector3(x, 0f, GorgeCentreZ(x) + .8f * Mathf.Sin(x / 7f)));
            var stream = AddVisualLandformSurface(gorge, "GorgeStream", 1.5f, .03f, bed.GetBakedLength(),
                new(0f, .04f, 0f), "5c6f74", "ice", 0f, true, bed);
            stream.SetMeta("visualOnly", true);
        }
        for (var x = AgentBAct1HeightField.MinX + 2f; x <= AgentBAct1HeightField.MaxX - 2f; x += 5f)
        {
            var z = GorgeCentreZ(x);
            foreach (var side in new[] { -1f, 1f })
            {
                if (Mathf.Abs(Mathf.Sin(x * .37f + side)) < .45f) continue;
                // Rock breaks out of the wall partway down: a faceted outcrop half buried in the
                // slope and tilted with it, not a box laid on top of the snow.
                var depth = .55f + .25f * Mathf.Sin(x * 1.7f + side);
                var wallZ = z + side * (float)AgentBAct1HeightField.GorgeHalfWidth * depth;
                var size = new Vector3(1.5f + .5f * Mathf.Sin(x * 2.3f), .8f + .25f * Mathf.Cos(x), 1.1f + .3f * Mathf.Sin(x * .9f));
                var shelf = new MeshInstance3D
                {
                    Name = $"GorgeRock_{x:0}_{(side < 0 ? "far" : "near")}",
                    Position = new Vector3(x, (float)AgentBAct1HeightField.Ground(x, wallZ) - size.Y * .35f, wallZ),
                    RotationDegrees = new Vector3(side * 34f, 47f * Mathf.Sin(x), 0),
                    Scale = size, Mesh = GorgeRockMesh((int)(x * 7 + side * 3)), MaterialOverride = rock
                };
                shelf.SetMeta("visualOnly", true);
                gorge.AddChild(shelf);
            }
        }
        // Rim fences keep the edge readable and keep a walker out of the cut. Both rims
        // leave a gap only where the bridge lands; the ravine mouth closes the east end.
        var fence = new StaticBody3D { Name = "GorgeRimFenceBody", CollisionLayer = 2, CollisionMask = 0 };
        fence.SetMeta("collisionOwner", "gorge-rim-fence");
        gorge.AddChild(fence);
        foreach (var near in new[] { true, false })
        {
            var offset = near ? 1.6f : -1.6f;
            var start = AgentBAct1HeightField.MinX + 2f;
            for (var x = start; x < AgentBAct1HeightField.MaxX - 2f; x += 4f)
            {
                var x1 = Mathf.Min(x + 4f, AgentBAct1HeightField.MaxX - 2f);
                if (Mathf.Max(x, x1) > SuspensionBridgeX - 1.6f && Mathf.Min(x, x1) < SuspensionBridgeX + 1.6f) continue;
                var a = new Vector3(x, 0, (near ? GorgeNearRim(x) : GorgeFarRim(x)) + offset);
                var b = new Vector3(x1, 0, (near ? GorgeNearRim(x1) : GorgeFarRim(x1)) + offset);
                AddVisualFenceRun(gorge, $"GorgeRimFence_{(near ? "near" : "far")}_{x:0}", a, b, false);
                var mid = (a + b) * .5f; mid.Y = AgentBAct1HeightField.CollisionGround(mid.X, mid.Z) + .6f;
                fence.AddChild(new CollisionShape3D
                {
                    Name = $"GorgeRimFenceShape_{(near ? "near" : "far")}_{x:0}", Position = mid,
                    Rotation = new Vector3(0, Mathf.Atan2(b.X - a.X, b.Z - a.Z), 0),
                    Shape = new BoxShape3D { Size = new Vector3(.18f, 1.2f, a.DistanceTo(b) + .1f) }
                });
            }
        }
        BuildSuspensionBridge(gorge);
        GD.Print($"act1-gorge: halfWidth={AgentBAct1HeightField.GorgeHalfWidth} depth={AgentBAct1HeightField.GorgeDepth} bridge=suspension@({SuspensionBridgeX},{GorgeCentreZ(SuspensionBridgeX):0.0})");
    }

    private static float SuspensionDeckHeight(float t, float nearY, float farY) =>
        Mathf.Lerp(nearY, farY, t) - .75f * Mathf.Sin(Mathf.Pi * t);

    private void BuildSuspensionBridge(Node3D gorge)
    {
        var x = SuspensionBridgeX;
        var nearZ = GorgeNearRim(x) + .8f;
        var farZ = GorgeFarRim(x) - .8f;
        var nearY = (float)AgentBAct1HeightField.CollisionGround(x, nearZ) + .12f;
        var farY = (float)AgentBAct1HeightField.CollisionGround(x, farZ) + .12f;
        var wood = PainterlyMaterialLibrary.ForColor("5c4a38", "wood");
        var old = PainterlyMaterialLibrary.ForColor("4a3d30", "wood");
        var rope = PainterlyMaterialLibrary.ForColor("9c8a6a", "fabric");

        var root = new Node3D { Name = "SuspensionBridge" };
        root.SetMeta("presentationRole", "old rope-and-plank suspension bridge over the gorge; barely holds");
        gorge.AddChild(root);
        // Anchor posts on both rims stay after the collapse.
        foreach (var (z, y, end) in new[] { (nearZ, nearY, "near"), (farZ, farY, "far") })
            foreach (var side in new[] { -1f, 1f })
                AddVisualBox(root, $"SuspensionPost_{end}_{(side < 0 ? "w" : "e")}", new(.24f, 2.0f, .24f), new(x + side * .95f, y + .9f, z), "574d3d", "wood",
                    rollDegrees: side * 3f);

        _suspensionIntact = new Node3D { Name = "SuspensionBridgeIntact" };
        root.AddChild(_suspensionIntact);
        _suspensionDeckBody = new StaticBody3D { Name = "SuspensionDeckBody", CollisionLayer = 1, CollisionMask = 0 };
        _suspensionDeckBody.SetMeta("footstepSurface", "wood");
        _suspensionIntact.AddChild(_suspensionDeckBody);
        var length = nearZ - farZ;
        var planks = Mathf.CeilToInt(length / .36f);
        for (var i = 0; i < planks; i++)
        {
            var t = (i + .5f) / planks;
            var z = Mathf.Lerp(nearZ, farZ, t);
            var y = SuspensionDeckHeight(t, nearY, farY);
            // A few planks are missing or loose: it is held together by habit.
            if (i % 11 == 7) continue;
            var loose = i % 7 == 3;
            var plank = new MeshInstance3D
            {
                Name = $"SuspensionPlank_{i:00}", Position = new Vector3(x + (loose ? .12f : 0f), y, z),
                RotationDegrees = new Vector3(0, loose ? 9f : (i % 3 - 1) * 2f, loose ? 4f : 0f),
                Mesh = new BoxMesh { Size = new Vector3(1.4f, .06f, .3f) }, MaterialOverride = i % 4 == 0 ? old : wood
            };
            _suspensionIntact.AddChild(plank);
        }
        const int segments = 14;
        for (var i = 0; i < segments; i++)
        {
            float T(int k) => k / (float)segments;
            var a = new Vector3(x, SuspensionDeckHeight(T(i), nearY, farY), Mathf.Lerp(nearZ, farZ, T(i)));
            var b = new Vector3(x, SuspensionDeckHeight(T(i + 1), nearY, farY), Mathf.Lerp(nearZ, farZ, T(i + 1)));
            var mid = (a + b) * .5f;
            var tilt = Mathf.Atan2(b.Y - a.Y, a.Z - b.Z);
            _suspensionDeckBody.AddChild(new CollisionShape3D
            {
                Name = $"SuspensionDeckShape_{i:00}", Position = mid - Vector3.Up * .03f, Rotation = new Vector3(tilt, 0, 0),
                Shape = new BoxShape3D { Size = new Vector3(1.4f, .08f, a.DistanceTo(b) + .04f) }
            });
            foreach (var side in new[] { -1f, 1f })
            {
                // Hand ropes double as the walls of the walk: nobody steps off the side.
                AddRavineLog(_suspensionIntact, $"SuspensionHandRope_{(side < 0 ? "w" : "e")}_{i:00}",
                    a + new Vector3(side * .78f, .95f, 0), b + new Vector3(side * .78f, .95f, 0), .025f, "9c8a6a");
                _suspensionDeckBody.AddChild(new CollisionShape3D
                {
                    Name = $"SuspensionRopeWall_{(side < 0 ? "w" : "e")}_{i:00}", Position = mid + new Vector3(side * .8f, .5f, 0),
                    Rotation = new Vector3(tilt, 0, 0), Shape = new BoxShape3D { Size = new Vector3(.08f, 1.0f, a.DistanceTo(b) + .04f) }
                });
                if (i % 2 == 0)
                    AddRavineLog(_suspensionIntact, $"SuspensionHanger_{(side < 0 ? "w" : "e")}_{i:00}",
                        a + new Vector3(side * .7f, 0, 0), a + new Vector3(side * .78f, .95f, 0), .012f, "9c8a6a");
            }
        }
        foreach (var child in _suspensionIntact.GetChildren().OfType<MeshInstance3D>().Where(m => m.Name.ToString().StartsWith("SuspensionHand")))
            child.MaterialOverride = rope;

        // After the collapse: two short ends hang from the posts, the rest is gone.
        _suspensionBroken = new Node3D { Name = "SuspensionBridgeBroken", Visible = false };
        root.AddChild(_suspensionBroken);
        foreach (var (z, y, dir, end) in new[] { (nearZ, nearY, -1f, "near"), (farZ, farY, 1f, "far") })
            for (var k = 0; k < 6; k++)
                AddVisualBox(_suspensionBroken, $"SuspensionHangingPlank_{end}_{k}", new(1.3f, .06f, .3f),
                    new(x + (k % 2 == 0 ? .1f : -.1f), y - .45f - k * .5f, z + dir * .25f), "4a3d30", "wood", rollDegrees: k * 7f);
        // Once it is gone, the two posts with the torn hand ropes close the gap in each rim fence.
        _suspensionGapBlocker = new StaticBody3D { Name = "SuspensionGapBlocker", CollisionLayer = 0, CollisionMask = 0 };
        _suspensionBroken.AddChild(_suspensionGapBlocker);
        foreach (var (z, y, end) in new[] { (nearZ - .1f, nearY, "near"), (farZ + .1f, farY, "far") })
        {
            AddRavineLog(_suspensionBroken, $"SuspensionTornRope_{end}", new Vector3(x - .95f, y + .95f, z), new Vector3(x + .95f, y + .7f, z), .03f, "9c8a6a");
            _suspensionGapBlocker.AddChild(new CollisionShape3D
            {
                Name = $"SuspensionGapShape_{end}", Position = new Vector3(x, y + .6f, z),
                Shape = new BoxShape3D { Size = new Vector3(3.4f, 1.2f, .3f) }
            });
        }
        foreach (var part in _suspensionIntact.GetChildren().OfType<Node3D>()) _suspensionRest[part] = part.Transform;
        root.SetMeta("deckPlanks", planks);
    }

    /// <summary>Applies the saved state: intact (walkable) or broken (gone, collision off).</summary>
    private void UpdateSuspensionBridge()
    {
        if (_suspensionIntact is null || _suspensionBroken is null || _suspensionDeckBody is null || _suspensionCollapsing) return;
        var broken = SuspensionBridgeBroken;
        if (!broken)
            foreach (var (part, rest) in _suspensionRest) part.Transform = rest;   // a new session restores the deck
        _suspensionIntact.Visible = !broken;
        _suspensionBroken.Visible = broken;
        _suspensionDeckBody.CollisionLayer = broken ? 0u : 1u;
        if (_suspensionGapBlocker is not null) _suspensionGapBlocker.CollisionLayer = broken ? 2u : 0u;
    }

    internal bool SuspensionBridgeBroken => _runtimeBridge?.SelectWorldProps() is { } props
        && props.TryGetProperty(SuspensionBridgeStateId, out var state) && state.ValueKind == JsonValueKind.Object
        && state.TryGetProperty("broken", out var broken) && broken.ValueKind == JsonValueKind.True;

    /// <summary>Per frame: once Aidar has stood on the forest side after the night of the
    /// blizzard, stepping back onto the village rim brings the bridge down behind him.</summary>
    private void WatchSuspensionBridge()
    {
        if (_suspensionIntact is null || _suspensionCollapsing || _runtimeBridge is null) return;
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player || ActiveZoneId is not ("village_day" or "zirat_road" or "kara_urman_night")) return;
        // Ordered cheap-to-expensive: both properties read the same immutable kernel
        // snapshot, but SuspensionBridgeBroken additionally walks into the nested
        // "broken" object. All four conditions are conjunctive guards with no side
        // effects, so the set of frames that reaches the collapse code is identical.
        if (!_runtimeBridge.FirstNightPassed) return;
        if (SuspensionBridgeBroken) return;
        var z = player.GlobalPosition.Z;
        var x = player.GlobalPosition.X;
        if (z < GorgeFarRim(x) - 1f) _suspensionVisitedFar = true;
        else if (_suspensionVisitedFar && z > GorgeNearRim(x) + 1.5f && Mathf.Abs(x - SuspensionBridgeX) < 6f)
            CollapseSuspensionBridge();
    }

    private async void CollapseSuspensionBridge()
    {
        _suspensionCollapsing = true;
        _suspensionDeckBody!.CollisionLayer = 0u;
        var centre = new Vector3(SuspensionBridgeX, (float)AgentBAct1HeightField.CollisionGround(SuspensionBridgeX, GorgeNearRim(0)), GorgeCentreZ(0));
        UiFoley.PlayWorld(this, centre, "forest/branch_crack", -6f, 60f, 1f);
        var fall = CreateTween().SetParallel();
        var index = 0;
        foreach (var part in _suspensionIntact!.GetChildren().OfType<Node3D>())
        {
            var delay = .04f * (index++ % 30);
            fall.TweenProperty(part, "position:y", part.Position.Y - 11f, 1.6f).SetDelay(delay)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            fall.TweenProperty(part, "rotation:z", part.Rotation.Z + (index % 2 == 0 ? .9f : -.7f), 1.6f).SetDelay(delay);
        }
        await ToSignal(fall, Tween.SignalName.Finished);
        UiFoley.PlayWorld(this, centre + Vector3.Down * 8f, "forest/wind_gust", -10f, 60f, 1f);
        if (_runtimeBridge is not null)
            await _runtimeBridge.DispatchWorldPropsAsync(new JsonArray { new JsonObject { ["propId"] = SuspensionBridgeStateId, ["broken"] = true } });
        _suspensionCollapsing = false;
        UpdateSuspensionBridge();
    }

    /// <summary>Presentation and props authored when this was a 9 m river channel (trees, banks,
    /// rocks, the old kara-side dressing) can now stand inside the gorge. Hide everything whose
    /// centre lies within the cut, except the gorge's own dressing, the bridge and the terrain;
    /// disable any collision shape there for the same reason. Foliage multimeshes lose only the
    /// instances that stand in the cut.</summary>
    private void ClearGorgeOfLegacyPresentation()
    {
        static bool Inside(Vector3 p) =>
            Mathf.Abs(p.Z - GorgeCentreZ(p.X)) < (float)AgentBAct1HeightField.GorgeHalfWidth - .6f;
        bool Keep(Node node)
        {
            for (var n = node; n is not null && n != this; n = n.GetParent())
            {
                var name = n.Name.ToString();
                if (name is "VillageForestGorge" or "AgentB_TerrainCollision" or "AgentB_TerrainRoadKit" || n is InteractionTarget) return true;
            }
            return false;
        }
        int hidden = 0, shapes = 0, instances = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
        {
            if (mesh.Mesh is null || !mesh.IsVisibleInTree() || Keep(mesh)) continue;
            var centre = mesh.GlobalTransform * mesh.Mesh.GetAabb().GetCenter();
            if (mesh.Mesh.GetAabb().Size.Length() > 60f)
            {
                var box = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                if (box.Position.Z < GorgeCentreZ(box.GetCenter().X) && box.End.Z > GorgeCentreZ(box.GetCenter().X)
                    && box.Position.X < 60 && box.End.X > -60 && mesh.Name != "Terrain_Main")
                    GD.Print($"act1-gorge-clear: large mesh spans the gorge: {mesh.GetPath()} box={box.Position}..{box.End}");
                continue;
            }
            if (!Inside(centre))
            {
                var b2 = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                var cz = GorgeCentreZ(b2.GetCenter().X);
                if (System.Environment.GetEnvironmentVariable("URMAN_GORGE_DEBUG") == "1" && b2.Position.Z < cz + 4 && b2.End.Z > cz - 4
                    && b2.End.X > -50 && b2.Position.X < 50 && b2.End.Y > -2f)
                    GD.Print($"act1-gorge-clear: crosses the cut: {mesh.GetPath()} box={b2.Position}..{b2.End}");
                continue;
            }
            mesh.Visible = false; hidden++;
        }
        // The authored terrain/road kit predates the gorge: any of its pieces that are not the
        // rebuilt terrain or a conformed road ribbon and reach into the cut are hidden too.
        if (FindChild("AgentB_TerrainRoadKit", true, false) is Node3D roadKit)
            foreach (var mesh in FindDescendants<MeshInstance3D>(roadKit).ToArray())
            {
                if (mesh.Mesh is null || !mesh.IsVisibleInTree() || mesh.Name == "Terrain_Main") continue;
                var box = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                var cz = GorgeCentreZ(box.GetCenter().X);
                if (box.Position.Z > cz + (float)AgentBAct1HeightField.GorgeHalfWidth - .6f
                    || box.End.Z < cz - (float)AgentBAct1HeightField.GorgeHalfWidth + .6f) continue;
                if (mesh.Name.ToString().StartsWith("Road_", StringComparison.Ordinal)
                    || mesh.Name.ToString().StartsWith("Grade_", StringComparison.Ordinal)) continue;   // conformed down into the cut
                GD.Print($"act1-gorge-clear: road kit piece in the cut hidden: {mesh.Name} box={box.Position}..{box.End}");
                mesh.Visible = false; hidden++;
            }
        foreach (var shape in FindDescendants<CollisionShape3D>(this).ToArray())
        {
            if (shape.Disabled || Keep(shape) || !Inside(shape.GlobalPosition)) continue;
            if (shape.Shape is ConcavePolygonShape3D or HeightMapShape3D) continue;
            shape.Disabled = true; shapes++;
        }
        foreach (var multi in FindDescendants<MultiMeshInstance3D>(this).ToArray())
        {
            if (multi.Multimesh is not { } mm || Keep(multi)) continue;
            var count = mm.VisibleInstanceCount < 0 ? mm.InstanceCount : mm.VisibleInstanceCount;
            for (var i = 0; i < count; i++)
            {
                var t = mm.GetInstanceTransform(i);
                if (!Inside(multi.GlobalTransform * t.Origin)) continue;
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * .0001f), t.Origin + Vector3.Down * 40f));
                instances++;
            }
        }
        if (System.Environment.GetEnvironmentVariable("URMAN_GORGE_DEBUG") == "1")
            foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
            {
                if (mesh.Mesh is null || !mesh.IsVisibleInTree() || mesh.Name == "Terrain_Main") continue;
                var box = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                if (box.End.X < -40 || box.Position.X > 40) continue;
                var cz = GorgeCentreZ(box.GetCenter().X);
                if (box.Position.Z > cz + 7.4f || box.End.Z < cz - 7.4f) continue;
                for (var t = 0f; t <= 1f; t += .25f)
                {
                    var p = box.Position + box.Size * new Vector3(t, 0, t);
                    if (Inside(p) && box.Position.Y > AgentBAct1HeightField.CollisionGround(p.X, p.Z) + .4f)
                    { GD.Print($"act1-gorge-float: {mesh.GetPath()} box={box.Position}..{box.End}"); break; }
                }
            }
        SetMeta("gorgeClearedMeshes", hidden);
        GD.Print($"act1-gorge-clear: hidden={hidden} shapesDisabled={shapes} foliageInstances={instances}");
    }
}
