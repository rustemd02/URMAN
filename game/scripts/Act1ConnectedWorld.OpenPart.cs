using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// The open part of the village after relayout v3, stage 4 (2026-10-01): a straight
/// Тукай урамы with the cross streets Яңа, Чишмә, Усал, Бакча and Кыр. Households are
/// generic authored parcels in <see cref="AgentBAct1Layout.OpenPartPlotPath"/> (built by
/// AuthoredWorldDirector with their addresses, generator tools/world/generate_open_part.py).
/// This part draws the new streets and clears what the old layout left standing on the
/// new plots and streets: old houses retire through the kit plot, while their loose
/// fences, gates, sheds and trees built by older code are hidden here.
/// </summary>
public partial class Act1ConnectedWorld
{
    internal sealed record OpenPartLot(string Id, string Kind, Vector2 Centre, float Yaw, Vector2 Size);

    private static (FarBankRoad[] Roads, OpenPartLot[] Lots, (Vector2 Centre, float Yaw, Vector2 Half)[] Parcels)? _openPart;

    internal static (FarBankRoad[] Roads, OpenPartLot[] Lots, (Vector2 Centre, float Yaw, Vector2 Half)[] Parcels) OpenPartPlot()
    {
        if (_openPart is { } cached) return cached;
        var path = AgentBAct1Layout.OpenPartPlotPath;
        if (!global::Godot.FileAccess.FileExists(path)) return (_openPart = ([], [], [])).Value;
        using var doc = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(path));
        var root = doc.RootElement;
        var roads = root.GetProperty("roads").EnumerateArray().Select(road => new FarBankRoad(
            road.GetProperty("id").GetString()!, road.GetProperty("street").GetString()!,
            road.GetProperty("points").EnumerateArray().Select(p => new Vector2(p[0].GetSingle(), p[1].GetSingle())).ToArray(),
            road.GetProperty("width").GetSingle())).ToArray();
        var lots = root.GetProperty("lots").EnumerateArray().Select(lot => new OpenPartLot(
            lot.GetProperty("id").GetString()!, lot.GetProperty("kind").GetString()!,
            new Vector2(lot.GetProperty("position")[0].GetSingle(), lot.GetProperty("position")[1].GetSingle()),
            lot.GetProperty("yawDegrees").GetSingle(),
            new Vector2(lot.GetProperty("size")[0].GetSingle(), lot.GetProperty("size")[1].GetSingle()))).ToArray();
        var parcels = root.GetProperty("entities").EnumerateArray()
            .Select(e => e.GetProperty("params"))
            .Where(p => p.TryGetProperty("terrainPad", out _))
            .Select(p => (new Vector2(p.GetProperty("position")[0].GetSingle(), p.GetProperty("position")[2].GetSingle()),
                p.GetProperty("yawDegrees").GetSingle(),
                new Vector2(p.GetProperty("terrainPad").GetProperty("halfSize")[0].GetSingle(),
                    p.GetProperty("terrainPad").GetProperty("halfSize")[1].GetSingle()))).ToArray();
        _openPart = (roads, lots, parcels);
        return _openPart.Value;
    }

    /// <summary>Trodden snow streets of the open part and the straight FAP street; the
    /// main street north of z 40 is drawn with the other northern ribbons.</summary>
    private static void AddOpenPartStreets(Node3D core)
    {
        var root = new Node3D { Name = "OpenPartStreets" };
        root.SetMeta("plotOwner", AgentBAct1Layout.OpenPartPlotPath);
        core.AddChild(root);
        var streets = OpenPartPlot().Roads.Select(road => (road.Id, road.Points, road.Width))
            .Append(("fap-street", AgentBAct1Layout.FapBranchAxis.Concat(AgentBAct1Layout.BridgeApproachAxis.Skip(1)).ToArray(), 4.6f))
            .Append(("mosque-walk", AgentBAct1Layout.MosqueWalkAxis, 1.6f));
        foreach (var (id, points, width) in streets)
        {
            using var curve = new Curve3D();
            foreach (var point in points) curve.AddPoint(new Vector3(point.X, 0f, point.Y));
            AddVisualLandformSurface(root, "OpenPartStreet_" + id, width, .025f, curve.GetBakedLength(),
                new(0f, .02f, 0f), "cbd3d8", "snow_trampled", 0f, true, curve);
        }
    }

    private static bool InsideRect(Vector2 point, Vector2 centre, float yawDegrees, Vector2 half)
    {
        var local = (point - centre).Rotated(Mathf.DegToRad(yawDegrees));
        return Mathf.Abs(local.X) <= half.X && Mathf.Abs(local.Y) <= half.Y;
    }

    private static float SegmentDistance2(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() < 1e-6f ? 0f : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    /// <summary>
    /// Old-layout presentation standing on a new plot or street of the open part is
    /// hidden and loses its contact: fences, gates, sheds, woodpiles, greybox drives and
    /// trees of yards that moved. Interactions, people, addressed buildings, the authored
    /// plots and the kept owners (babai yard, FAP, mosque, square, Tamara Gennadievna's
    /// plot) stay; a node within 1.6 m of an interaction stays too and is reported, so a
    /// discovery never loses its visible object.
    /// </summary>
    private void ClearOpenPartOfLegacyPresentation()
    {
        var (roads, lots, parcels) = OpenPartPlot();
        if (lots.Length == 0) return;
        var clearLots = lots.Where(lot => lot.Kind == "parcel").ToArray();
        var bands = roads.Select(road => (Points: road.Points, Half: road.Width * .5f + .4f))
            .Append((Points: AgentBAct1Layout.FapBranchAxis.Concat(AgentBAct1Layout.BridgeApproachAxis.Skip(1)).ToArray(), Half: 2.7f))
            .Append((Points: AgentBAct1Layout.PlazaDriveAxis, Half: 2.4f)).ToArray();
        bool OnStreet(Vector2 p) => bands.Any(band =>
        {
            for (var i = 1; i < band.Points.Length; i++)
                if (SegmentDistance2(p, band.Points[i - 1], band.Points[i]) < band.Half) return true;
            return false;
        });
        // Мәйдан (relayout stage 3) is cleared like a plot: old yard fences of the arrival stood on it.
        string? LotAt(Vector2 p) => AgentBAct1Layout.InsidePlaza(p) ? "plaza"
            : clearLots.FirstOrDefault(lot => InsideRect(p, lot.Centre, lot.Yaw, lot.Size * .5f - Vector2.One * .2f))?.Id;
        bool OnParcel(Vector2 p) => parcels.Any(parcel => InsideRect(p, parcel.Centre, parcel.Yaw, parcel.Half));
        var targets = FindDescendants<InteractionTarget>(this).Where(t => t.IsInsideTree())
            .Select(t => new Vector2(t.GlobalPosition.X, t.GlobalPosition.Z)).ToArray();
        var keepNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "AuthoredWorldDirector", "AgentB_TerrainCollision", "AgentB_TerrainRoadKit", "SettlementAddressPresentation",
            "VillageMosqueComplex", "SovkhozSquare", "TamaraFenceQuest", "VillageRavine", "RavineFarBank",
            "NorthContinuousWinterRoads", "OpenPartStreets", "Act1NpcPresentation", "CarryCoordinator", "babay-abi-house",
            "FapExterior", "fap-clinic-yard", "VillageForestGorge", "ZiratMemoryField"
        };
        bool Keep(Node node)
        {
            for (var n = node; n is not null && n != this; n = n.GetParent())
            {
                if (n is InteractionTarget or CharacterBody3D or VehicleBody3D or CarryableProp) return true;
                if (n.HasMeta(KitPlacementTakeover.RetiredMeta)) return false;
                if (n.HasMeta("placementOwner")) return true;   // kit pieces the plot keeps or moved
                var name = n.Name.ToString();
                if (keepNames.Contains(name) || name.StartsWith("Babai", StringComparison.Ordinal)) return true;
            }
            return false;
        }
        static bool Foliage(Node node)
        {
            for (var n = node; n is not null; n = n.GetParent())
            {
                var name = n.Name.ToString();
                if (name.Contains("Foliage", StringComparison.Ordinal) || name.Contains("Tree", StringComparison.Ordinal)
                    || name.Contains("Birch", StringComparison.Ordinal) || name.Contains("Conifer", StringComparison.Ordinal)
                    || name.Contains("Shrub", StringComparison.Ordinal)) return true;
            }
            return false;
        }
        bool Clears(Node node, Vector2 p) => OnStreet(p) || (Foliage(node) ? OnParcel(p) || AgentBAct1Layout.InsidePlaza(p) : LotAt(p) is not null);
        var nearTarget = new List<string>();
        bool NearTarget(Node node, Vector2 p)
        {
            if (!targets.Any(t => t.DistanceTo(p) < 1.6f)) return false;
            nearTarget.Add(System.FormattableString.Invariant($"{node.GetPath()}@{p.X:0.0},{p.Y:0.0}"));
            return true;
        }

        // Driveway ribbons to the old holdings; the address graph would otherwise read them as roads.
        foreach (var name in new[] { "ZiratWestHoldingAccess", "EastStreetPlotAccessPath", "ConnectiveWestHouseDrive", "ReturnEastFarmDrive", "Road_FapBranch" })
            foreach (var mesh in FindDescendants<MeshInstance3D>(this).Where(m => m.Name == name).ToArray())
                HidePresentationNode(mesh);
        // Retired kit pieces may have gained contacts after the plot applied (blockers, frontage).
        foreach (var node in FindDescendants<Node3D>(this).Where(n => n.HasMeta(KitPlacementTakeover.RetiredMeta)).ToArray())
            KitPlacementTakeover.Retire(node);

        int hidden = 0, shapes = 0, instances = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
        {
            if (mesh.Mesh is null || !mesh.IsVisibleInTree() || Keep(mesh)) continue;
            var box = mesh.GlobalTransform * mesh.Mesh.GetAabb();
            if (box.Size.Length() > 40f) continue;
            var centre = box.GetCenter();
            var p = new Vector2(centre.X, centre.Z);
            if (!Clears(mesh, p) || NearTarget(mesh, p)) continue;
            HidePresentationNode(mesh); hidden++;
        }
        foreach (var shape in FindDescendants<CollisionShape3D>(this).ToArray())
        {
            if (shape.Disabled || Keep(shape) || shape.Shape is ConcavePolygonShape3D or HeightMapShape3D) continue;
            var p = new Vector2(shape.GlobalPosition.X, shape.GlobalPosition.Z);
            if (!Clears(shape, p) || NearTarget(shape, p)) continue;
            shape.Disabled = true; shapes++;
        }
        foreach (var multi in FindDescendants<MultiMeshInstance3D>(this).ToArray())
        {
            if (multi.Multimesh is not { } mm || Keep(multi)) continue;
            var count = mm.VisibleInstanceCount < 0 ? mm.InstanceCount : mm.VisibleInstanceCount;
            for (var i = 0; i < count; i++)
            {
                var t = mm.GetInstanceTransform(i);
                var at = multi.GlobalTransform * t.Origin;
                var p = new Vector2(at.X, at.Z);
                if (!(OnStreet(p) || OnParcel(p) || AgentBAct1Layout.InsidePlaza(p))) continue;
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * .0001f), t.Origin + Vector3.Down * 40f));
                instances++;
            }
        }
        foreach (var target in FindDescendants<InteractionTarget>(this).Where(t => t.IsInsideTree()))
        {
            var p = new Vector2(target.GlobalPosition.X, target.GlobalPosition.Z);
            if (FindAncestorNamed(target, "AuthoredWorldDirector") || FindAncestorNamed(target, "SettlementAddressPresentation")) continue;
            if (OnParcel(p) || OnStreet(p))
                GD.Print(System.FormattableString.Invariant(
                    $"open-part-conflict|{target.GetPath()}|{p.X:0.0}|{p.Y:0.0}|{(OnStreet(p) ? "street" : "parcel " + LotAt(p))}"));
        }
        foreach (var item in nearTarget.Distinct().Take(60)) GD.Print("open-part-kept-near-interaction|" + item);
        GD.Print($"act1-open-part-clear: hiddenMeshes={hidden} disabledShapes={shapes} sunkInstances={instances} keptNearInteraction={nearTarget.Count}");
    }

    private static bool FindAncestorNamed(Node node, string name)
    {
        for (var n = node; n is not null; n = n.GetParent())
            if (n.Name == name) return true;
        return false;
    }
}
