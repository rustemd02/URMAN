using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// One fence system for every household lot (author 2026-10-02: «пересобери все заборы»).
/// Lots come from the open-part and far-bank plot data (position, yaw, width along the street,
/// depth). The street edge of a lot gets a painted picket fence with an open wicket in front
/// of the house entrance; sides and back get a plain board fence. Neighbours share one fence:
/// every edge is sampled at 0.5 m and a stretch already fenced by another lot is skipped.
/// Nothing is fenced on a carriageway, in the babai yard (its authored fences carry the yard
/// mechanics) or on Tamara's plot (her broken fence is a quest). The parcels' own short rails
/// and the old layout's leftover rails are retired.
/// </summary>
public partial class Act1ConnectedWorld
{
    internal sealed record YardLot(string Id, string Kind, Vector2 Centre, float Yaw, Vector2 Size);

    private static readonly Regex LegacyFenceName = new(
        @"Fence|Rail|Picket|Paling|Palisade|Wattle|Holding\w*(Shed|Post)|^Gate_House|^(Woodpile_House|WoodpileBanya)|^HouseA\d",
        RegexOptions.Compiled);
    private static readonly Regex NotAFence = new(@"Window|Portal|Stair|Porch|Balcony|Shelf|Bridge|Guard|Bed|Ladder|Bench|Sign|Rim", RegexOptions.Compiled);

    private static readonly string[] YardFenceProtectedRoots =
    [
        "AuthoredWorldDirector", "YardFences", "TamaraFenceQuest", "VillageRavine", "VillageForestGorge",
        "ZiratRoadsideAuthoredKitPresentation", "AgentB_ZiratKit", "SovkhozSquare", "VillageMosqueComplex",
        "FapClinicAuthoredKitPresentation", "FapServiceExplorationPresentation", "RearYardGate",
        "BabaiYardSideGateExploration", "AgentB_TerrainRoadKit", "AgentB_TerrainCollision", "MainStreetSnowBanks",
        "VillageVehicles", "Act1People", "SettlementAddressPresentation", "WetVillageRoadKitPresentation",
    ];

    internal static IReadOnlyList<YardLot> YardLots()
    {
        var lots = new List<YardLot>();
        foreach (var path in new[] { AgentBAct1Layout.OpenPartPlotPath, AgentBAct1HeightField.FarBankPlotPath })
        {
            if (!global::Godot.FileAccess.FileExists(path)) continue;
            using var doc = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(path));
            if (!doc.RootElement.TryGetProperty("lots", out var items)) continue;
            foreach (var lot in items.EnumerateArray())
                lots.Add(new YardLot(lot.GetProperty("id").GetString()!, lot.GetProperty("kind").GetString()!,
                    new Vector2(lot.GetProperty("position")[0].GetSingle(), lot.GetProperty("position")[1].GetSingle()),
                    lot.GetProperty("yawDegrees").GetSingle(),
                    new Vector2(lot.GetProperty("size")[0].GetSingle(), lot.GetProperty("size")[1].GetSingle())));
        }
        return lots;
    }

    private void RebuildYardFences()
    {
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var lots = YardLots();
        var keep = lots.Where(lot => lot.Kind == "keep").ToArray();
        bool InLot(YardLot lot, Vector2 p, float grow)
        {
            var f = new Vector2(Mathf.Sin(Mathf.DegToRad(lot.Yaw)), Mathf.Cos(Mathf.DegToRad(lot.Yaw)));
            var s = new Vector2(f.Y, -f.X);
            var d = p - lot.Centre;
            return Mathf.Abs(d.Dot(s)) <= lot.Size.X * .5f + grow && Mathf.Abs(d.Dot(f)) <= lot.Size.Y * .5f + grow;
        }

        // 1. Retire every older yard fence: the street frontage runs, the old layout's rails and
        //    any fence-like piece standing on a lot line.
        var retired = 0;
        if (GetNodeOrNull("StreetFrontages") is { } frontages) { HidePresentationNode(frontages); retired++; }
        if (core.GetNodeOrNull("StreetFrontages") is { } coreFrontages) { HidePresentationNode(coreFrontages); retired++; }
        void Retire(Node node)
        {
            var name = node.Name.ToString();
            if (YardFenceProtectedRoots.Contains(name) || node.HasMeta("babaiRelocated") || node is InteractionTarget) return;
            if (node is Node3D spatial && spatial.Visible && LegacyFenceName.IsMatch(name) && !NotAFence.IsMatch(name))
            {
                var at = spatial.GlobalPosition;
                var box = spatial is VisualInstance3D visual ? visual.GlobalTransform * visual.GetAabb() : new Aabb(at, Vector3.Zero);
                var centre = box.Size == Vector3.Zero ? new Vector2(at.X, at.Z) : new Vector2(box.GetCenter().X, box.GetCenter().Z);
                var open = centre.X > -64 && centre.X < 46 && centre.Y > -95 && centre.Y < 160;
                if (open || lots.Any(lot => InLot(lot, centre, .8f)))
                {
                    HidePresentationNode(node);
                    node.SetMeta("suppressionReason", "yard fences rebuilt along lot lines (2026-10-02)");
                    retired++;
                    return;
                }
            }
            foreach (var child in node.GetChildren().ToArray()) Retire(child);
        }
        foreach (var child in core.GetChildren().ToArray()) Retire(child);
        if (_zoneInstances.TryGetValue("village_day", out var street))
            foreach (var child in street.GetChildren().ToArray()) Retire(child);

        // 2. Edges, front edges first so a shared corner keeps its street face.
        var root = new Node3D { Name = "YardFences" };
        root.SetMeta("presentationRole", "village yard fences along the real lot lines; street pickets, board sides and backs");
        core.AddChild(root);
        var body = new StaticBody3D { Name = "YardFenceBody", CollisionLayer = 2, CollisionMask = 0 };
        body.SetMeta("collisionOwner", "authored-kit-blocker");
        root.AddChild(body);
        // Points already fenced by earlier edges; an edge commits its own points when done.
        var claimed = new HashSet<(int, int)>();
        var pending = new List<(int, int)>();
        bool Claim(Vector2 p)
        {
            var key = ((int)Mathf.Round(p.X * 2), (int)Mathf.Round(p.Y * 2));
            for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
                if (claimed.Contains((key.Item1 + dx, key.Item2 + dz))) return false;
            pending.Add(key);
            return true;
        }
        // Fences that stay (ravine rim, cemetery, Tamara's quest fence, the FAP service yard,
        // the mosque yard, the babai yard) and every building: a lot line stops short of them.
        var standing = new List<Rect2>();
        void Standing(Node node, bool fenceOnly)
        {
            foreach (var mesh in FindDescendants<MeshInstance3D>(node))
            {
                if (!mesh.IsVisibleInTree() || mesh.Mesh is null) continue;
                if (fenceOnly && !LegacyFenceName.IsMatch(mesh.Name.ToString()) && !mesh.Name.ToString().Contains("Wall", StringComparison.Ordinal)) continue;
                var box = mesh.GlobalTransform * mesh.GetAabb();
                if (box.Size.Length() > 60) continue;
                standing.Add(new Rect2(box.Position.X - .5f, box.Position.Z - .5f, box.Size.X + 1f, box.Size.Z + 1f));
            }
        }
        foreach (var name in new[] { "VillageRavine", "TamaraFenceQuest", "ZiratMemoryField", "FapExterior", "VillageMosqueComplex", "SovkhozSquare" })
            if (core.GetNodeOrNull(name) is { } fixedRoot) Standing(fixedRoot, true);
        foreach (var moved in FindDescendants<Node3D>(core).Where(n => n.HasMeta("babaiRelocated")).ToArray()) Standing(moved, true);
        var footprints = new List<(string Address, Vector2[] Polygon)>();
        if (AddressRegistry is { } reg)
            foreach (var building in reg.Buildings.Values)
                if (building.Footprint.Count >= 3)
                    footprints.Add((building.AddressId ?? building.BuildingId,
                        building.Footprint.Select(q => new Vector2((float)q.X, (float)q.Z)).ToArray()));
        static bool InPolygon(Vector2[] poly, Vector2 p)
        {
            var c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y)
                    && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y + 1e-6f) + poly[i].X) c = !c;
            return c;
        }
        string currentLot = "", currentKind = "";
        bool Blocked(Vector2 p)
        {
            var (distance, halfWidth) = AgentBAct1HeightField.RoadInfo(p.X, p.Y);
            if (distance < halfWidth + .35) return true;
            if (BabaiRelocation.OutsideNewYard(p.X, p.Y) < .4f) return true;
            if (keep.Any(lot => InLot(lot, p, .4f))) return true;
            if (standing.Any(r => r.HasPoint(p))) return true;
            // A kit lot is only nominal: its real building may reach past it, so it is never crossed.
            return footprints.Any(fp => (fp.Address != currentLot || currentKind != "parcel") && InPolygon(fp.Polygon, p));
        }
        var registry = AddressRegistry;
        string[] streetColours = ["5f7d5a", "4f6f8c", "a8854a", "8a8f86", "6b8a7a", "7d5a4a"];
        var pickets = new Dictionary<string, List<Transform3D>>();
        int runs = 0, gates = 0;
        foreach (var front in new[] { true, false })
        foreach (var lot in lots.Where(lot => lot.Kind != "keep"))
        {
            currentLot = lot.Id; currentKind = lot.Kind;
            var f = new Vector2(Mathf.Sin(Mathf.DegToRad(lot.Yaw)), Mathf.Cos(Mathf.DegToRad(lot.Yaw)));
            var s = new Vector2(f.Y, -f.X);
            var (hw, hd) = (lot.Size.X * .5f, lot.Size.Y * .5f);
            var edges = front
                ? new[] { (lot.Centre + f * hd - s * hw, lot.Centre + f * hd + s * hw, "front") }
                : new[] { (lot.Centre - f * hd - s * hw, lot.Centre - f * hd + s * hw, "back"),
                          (lot.Centre - f * hd - s * hw, lot.Centre + f * hd - s * hw, "side"),
                          (lot.Centre - f * hd + s * hw, lot.Centre + f * hd + s * hw, "side") };
            // The wicket stands in front of the house entrance.
            float? gateAt = null;
            if (front && registry is not null && registry.Addresses.TryGetValue(lot.Id, out var address)
                && registry.AccessPoints.TryGetValue(address.AccessId, out var access))
                gateAt = (new Vector2((float)access.Position.X, (float)access.Position.Z) - (lot.Centre + f * hd - s * hw)).Dot(s);
            var colour = streetColours[Math.Abs(lot.Id.GetHashCode()) % streetColours.Length];
            foreach (var (a, b, kind) in edges)
            {
                var length = a.DistanceTo(b);
                var dir = (b - a) / length;
                var run = new List<Vector2>();
                void Flush()
                {
                    if (run.Count >= 3)
                    {
                        // Any edge that faces a street (a corner lot's side) gets the street pickets.
                        var mid = run[run.Count / 2];
                        var (rd, rh) = AgentBAct1HeightField.RoadInfo(mid.X, mid.Y);
                        BuildFenceRun(root, body, run, kind == "front" || rd - rh < 3.2, colour, pickets);
                        runs++;
                    }
                    run.Clear();
                }
                for (var t = 0f; t <= length + .01f; t += .5f)
                {
                    var p = a + dir * Mathf.Min(t, length);
                    var inGate = gateAt is { } g && Mathf.Abs(t - Mathf.Clamp(g, 1f, length - 1f)) < .8f;
                    if (inGate || Blocked(p) || !Claim(p)) { if (inGate && run.Count > 0) gates++; Flush(); continue; }
                    run.Add(p);
                }
                Flush();
                foreach (var key in pending) claimed.Add(key);
                pending.Clear();
            }
        }
        // One mesh per material: picket instances and boxes alike are baked into a single surface.
        foreach (var (key, transforms) in pickets)
        {
            var box = key.StartsWith("box:", StringComparison.Ordinal);
            var (colour, surface) = box ? (key[4..].Split('|')[0], key[4..].Split('|')[1]) : (key, "wood_painted_green");
            using var unit = new BoxMesh { Size = box ? Vector3.One : new(.075f, 1.12f, .022f) };
            using var tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var t in transforms) tool.AppendFrom(unit, 0, t);
            tool.Index();
            var mesh = tool.Commit();
            root.AddChild(new MeshInstance3D { Name = (box ? "FenceParts_" : "Pickets_") + colour, Mesh = mesh,
                MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, surface) });
        }
        GD.Print($"act1-yard-fences: lots={lots.Count} runs={runs} wickets={gates} retired={retired}");
    }

    /// <summary>One straight fence run along sampled points: posts, rails and pickets on the
    /// street, posts and close boards elsewhere, a thin snow line on top, one blocker box per bay.</summary>
    private static void BuildFenceRun(Node3D root, StaticBody3D body, List<Vector2> run, bool street, string colour,
        Dictionary<string, List<Transform3D>> pickets)
    {
        // All pieces are batched per material (posts, rails, boards, snow) into a few meshes.
        void Box(string material, Vector3 size, Transform3D at)
        {
            if (!pickets.TryGetValue("box:" + material, out var list)) pickets["box:" + material] = list = [];
            list.Add(at * new Transform3D(Basis.Identity.Scaled(size), Vector3.Zero));
        }
        var a = run[0]; var b = run[^1];
        var length = a.DistanceTo(b);
        if (length < .8f) return;
        var dir = (b - a) / length;
        var yaw = Mathf.Atan2(dir.X, dir.Y);
        var basis = new Basis(Vector3.Up, yaw);   // local +Z along the run
        var height = street ? 1.3f : 1.65f;
        var bays = Mathf.Max(1, Mathf.CeilToInt(length / 2.4f));
        var bay = length / bays;
        var wood = street ? colour : "a69a86";
        for (var i = 0; i <= bays; i++)
        {
            var p = a + dir * (i * bay);
            var ground = AgentBAct1HeightField.CollisionGround(p.X, p.Y);
            Box("5a4b3c|wood_fence", new(.11f, height + .15f, .11f),
                new Transform3D(basis, new Vector3(p.X, ground + (height + .15f) * .5f - .05f, p.Y)));
        }
        for (var i = 0; i < bays; i++)
        {
            var p0 = a + dir * (i * bay); var p1 = a + dir * ((i + 1) * bay);
            var mid = (p0 + p1) * .5f;
            var g0 = AgentBAct1HeightField.CollisionGround(p0.X, p0.Y);
            var g1 = AgentBAct1HeightField.CollisionGround(p1.X, p1.Y);
            var gm = (g0 + g1) * .5f;
            var tilt = Mathf.Atan2(g1 - g0, bay);
            var segBasis = basis * new Basis(Vector3.Right, -tilt);
            if (street)
            {
                foreach (var y in new[] { .32f, height - .22f })
                {
                    Box("6a5a48|wood_fence", new(.04f, .07f, bay), new Transform3D(segBasis, new Vector3(mid.X, gm + y, mid.Y)));
                }
                if (!pickets.TryGetValue(colour, out var list)) pickets[colour] = list = [];
                var count = Mathf.Max(1, Mathf.FloorToInt(bay / .15f));
                for (var k = 0; k < count; k++)
                {
                    var t = (k + .5f) / count;
                    var q = p0.Lerp(p1, t);
                    var g = Mathf.Lerp(g0, g1, t);
                    list.Add(new Transform3D(basis, new Vector3(q.X, g + .62f, q.Y) + basis * new Vector3(.03f, 0, 0)));
                }
            }
            else
            {
                Box(wood + "|wood_fence", new(.04f, height - .08f, bay), new Transform3D(segBasis, new Vector3(mid.X, gm + height * .5f, mid.Y)));
                Box("eef2f6|snow_ground", new(.09f, .05f, bay), new Transform3D(segBasis, new Vector3(mid.X, gm + height, mid.Y)));
            }
            body.AddChild(new CollisionShape3D
            {
                Name = $"FenceBay_{body.GetChildCount()}",
                Shape = new BoxShape3D { Size = new(.14f, height, bay) },
                Transform = new Transform3D(segBasis, new Vector3(mid.X, gm + height * .5f, mid.Y))
            });
        }
    }
}
