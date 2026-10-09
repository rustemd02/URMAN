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

    /// <summary>
    /// VIS-088: one fence family is one construction, not one colour. Every value
    /// here is a carpentry measurement the author can edit: board thickness stays
    /// in the 20–40 mm range the card requires, bay length is the real distance
    /// between supporting posts, and the lean of a post is bounded (typically ≤4°,
    /// a settled post of a repaired run up to 8°). Nothing here randomises per
    /// board: within a family every board is the same piece, and only the authored
    /// hand-cut profiles vary by a fixed three-step pattern.
    /// </summary>
    internal sealed record FenceDesign
    {
        public required string Id { get; init; }
        /// <summary>Kept for the existing collision meta consumers (timberStyle).</summary>
        public required int Style { get; init; }
        public required float Height { get; init; }
        public required float BoardThickness { get; init; }
        public required float BoardWidth { get; init; }
        public required float BoardSpacing { get; init; }
        public required float BayLength { get; init; }
        public required float PostSection { get; init; }
        public required float RailThickness { get; init; }
        public required float RailDepth { get; init; }
        /// <summary>Height of the lower bearing rail above this bay's own ground.</summary>
        public required float LowerRailHeight { get; init; }
        /// <summary>How far below the fence crown the upper bearing rail runs.</summary>
        public required float UpperRailDrop { get; init; }
        public required float LeanDegrees { get; init; }
        /// <summary>Settled post of a repaired run; applied to one post, never to all.</summary>
        public float SettledLeanDegrees { get; init; }
        /// <summary>Wide/narrow boards alternate, as on a fence repaired from what was left.</summary>
        public bool MixedBoards { get; init; }
        /// <summary>Horizontal withies woven through dense uprights (плетень).</summary>
        public bool Woven { get; init; }
        /// <summary>Thin uprights hung between open rails instead of a closed run.</summary>
        public bool RailWithUprights { get; init; }
        public bool CapRail { get; init; }
        public bool RepairBay { get; init; }
        /// <summary>The painterly surface the family's own timber is read as. A woven
        /// or railed fence is not painted, so it takes the weathered board surface and
        /// only a wash of the authored colour; the closed picket families keep the
        /// painted trim surface. Swap to "wattle" here once
        /// `assets/textures/painterly/wattle_weave_v1_albedo.png` exists (surface is
        /// already declared in PainterlyMaterialLibrary, the file is not).</summary>
        public string Surface { get; init; } = "wood_painted_trim";
        /// <summary>Pointed (sharpened) picket crowns.</summary>
        public bool PointedTops { get; init; }
        /// <summary>Rounded scalloped palisadnik crowns.</summary>
        public bool ScallopedTops { get; init; }
        /// <summary>Extra horizontal rails beyond the two bearing rails.</summary>
        public float[] ExtraRailHeights { get; init; } = [];
    }

    /// <summary>Five street constructions. Each lot takes one in street order, so a
    /// neighbour is always read as a different fence even before its paint.</summary>
    private static readonly FenceDesign[] StreetDesigns =
    [
        new() { Id = "rough-picket", Style = 0, Height = 1.18f, BoardThickness = .028f, BoardWidth = .095f,
            BoardSpacing = .17f, BayLength = 2.35f, PostSection = .12f, RailThickness = .045f, RailDepth = .075f,
            LowerRailHeight = .27f, UpperRailDrop = .23f, LeanDegrees = 1.2f, PointedTops = true },
        new() { Id = "painted-palisadnik", Style = 1, Height = 1.10f, BoardThickness = .026f, BoardWidth = .095f,
            BoardSpacing = .17f, BayLength = 2.10f, PostSection = .11f, RailThickness = .04f, RailDepth = .07f,
            LowerRailHeight = .26f, UpperRailDrop = .21f, LeanDegrees = .8f, ScallopedTops = true, CapRail = true },
        new() { Id = "repaired-mixed-board", Style = 2, Height = 1.24f, BoardThickness = .034f, BoardWidth = .115f,
            BoardSpacing = .165f, BayLength = 2.45f, PostSection = .13f, RailThickness = .045f, RailDepth = .08f,
            LowerRailHeight = .28f, UpperRailDrop = .24f, LeanDegrees = 2.4f, SettledLeanDegrees = 8f,
            MixedBoards = true, RepairBay = true, PointedTops = true },
        new() { Id = "wattle", Style = 3, Height = 1.20f, BoardThickness = .030f, BoardWidth = .075f,
            BoardSpacing = .16f, BayLength = 2.25f, PostSection = .12f, RailThickness = .032f, RailDepth = .05f,
            LowerRailHeight = .30f, UpperRailDrop = .10f, LeanDegrees = 1.6f, Woven = true,
            Surface = "wood_fence", ExtraRailHeights = [.48f, .92f, 1.10f] },
        new() { Id = "simple-rail", Style = 4, Height = 1.30f, BoardThickness = .024f, BoardWidth = .070f,
            BoardSpacing = .26f, BayLength = 2.60f, PostSection = .14f, RailThickness = .06f, RailDepth = .14f,
            LowerRailHeight = .42f, UpperRailDrop = .10f, LeanDegrees = 3.2f, RailWithUprights = true,
            Surface = "wood_fence", CapRail = true, ExtraRailHeights = [.84f, 1.20f] },
    ];

    /// <summary>The working yard boundary: plain weathered boards, uneven hand-cut
    /// crowns and one repaired bay. Not a street design, so it never appears on the
    /// street face (VIS-019).</summary>
    private static readonly FenceDesign YardBoundaryDesign = new()
    {
        Id = "yard-boundary-board", Style = 5, Height = 1.38f, BoardThickness = .034f, BoardWidth = .11f,
        BoardSpacing = .16f, BayLength = 2.35f, PostSection = .12f, RailThickness = .045f, RailDepth = .075f,
        LowerRailHeight = .27f, UpperRailDrop = .23f, LeanDegrees = 1f, SettledLeanDegrees = 6f,
        RepairBay = true, Surface = "wood_fence"
    };

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
        root.SetMeta("presentationRole", "village yard fences along the real lot lines; five timber designs, open street gates and permeable yard boundaries");
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
        // author photo reference T1 (09.10.2026): painted picket fences — ochre, faded green, faded sky blue, old white.
        string[] paintedStreetColours = ["c9874a", "7f9a74", "8fb0c4", "dcd6c8"];
        // VIS-088: the construction of a street fence is handed out in street order
        // on each frontage, so two neighbouring lots never read as the same build.
        // The phase comes from the frontage itself, which keeps the sequence stable
        // across rebuilds and stops the whole village starting on the same family.
        var designOf = new Dictionary<string, FenceDesign>(StringComparer.Ordinal);
        foreach (var group in lots.Where(l => l.Kind != "keep")
                     .GroupBy(l => Mathf.RoundToInt(l.Yaw / 15f)))
        {
            var ordered = group.OrderBy(l =>
            {
                var front = new Vector2(Mathf.Sin(Mathf.DegToRad(l.Yaw)), Mathf.Cos(Mathf.DegToRad(l.Yaw)));
                return l.Centre.Dot(new Vector2(front.Y, -front.X));
            }).ToArray();
            var phase = ordered.Length == 0 ? 0 : (int)(TimberHomeStyle.StableHash(ordered[0].Id) % (uint)StreetDesigns.Length);
            for (var i = 0; i < ordered.Length; i++) designOf[ordered[i].Id] = StreetDesigns[(i + phase) % StreetDesigns.Length];
        }
        CheckFenceDesignContract();
        var pickets = new Dictionary<string, List<Transform3D>>();
        int runs = 0, gates = 0, wickets = 0, openings = 0;
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
            var design = designOf.TryGetValue(lot.Id, out var assigned) ? assigned : StreetDesigns[0];
            // VIS-019: colour came from the same hash as the design, so the design
            // predicted the paint; draw it from independent bits.
            var colour = streetColours[(int)((TimberHomeStyle.StableHash(lot.Id) >> 8) % (uint)streetColours.Length)];
            // About 45% of the closed picket/board street fences are painted; woven and
            // rail-with-upright families keep the weathered wash. Separate hash strings
            // keep the paint decision per lot and independent of the design and weathered colour.
            if (!design.Woven && !design.RailWithUprights && TimberHomeStyle.StableHash(lot.Id + ":painted") % 100 < 45)
                colour = paintedStreetColours[(int)((TimberHomeStyle.StableHash(lot.Id + ":painted-colour") >> 8) % (uint)paintedStreetColours.Length)];
            foreach (var (a, b, kind) in edges)
            {
                var length = a.DistanceTo(b);
                var dir = (b - a) / length;
                // A cart gate needs two leaves and the room for a cart; anything
                // narrower gets a wicket. Both are real entrances, never a hole.
                var openHalf = length > 5f ? 1.8f : .62f;
                if (front && gateAt is { } opening && length > 4.4f)
                {
                    var gate = a + dir * Mathf.Clamp(opening, 2f, length - 2f);
                    if (length > 5f)
                    {
                        BuildTimberStreetGate(root, body, gate, dir, -f, colour, design, pickets, lot.Id);
                        gates++;
                    }
                    else
                    {
                        BuildYardWicket(root, body, gate, dir, -f, design, colour, pickets, lot.Id);
                        wickets++;
                    }
                }
                var run = new List<Vector2>();
                void Flush()
                {
                    if (run.Count >= 3)
                    {
                        // Any edge that faces a street (a corner lot's side) gets the street pickets.
                        var mid = run[run.Count / 2];
                        var (rd, rh) = AgentBAct1HeightField.RoadInfo(mid.X, mid.Y);
                        BuildFenceRun(root, body, run, kind == "front" || rd - rh < 3.2, colour, pickets, design,
                            TimberHomeStyle.StableHash($"{lot.Id}:{kind}:{runs}"));
                        runs++;
                    }
                    run.Clear();
                }
                for (var t = 0f; t <= length + .01f; t += .5f)
                {
                    var p = a + dir * Mathf.Min(t, length);
                    var inGate = gateAt is { } g && Mathf.Abs(t - Mathf.Clamp(g, 2f, length - 2f)) < openHalf;
                    if (inGate || Blocked(p) || !Claim(p)) { if (inGate && run.Count > 0) openings++; Flush(); continue; }
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
            var parts = key[4..].Split('|');
            using var tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var t in transforms) TimberHomeStyle.AppendMetricBox(tool, t);
            tool.Index();
            root.AddChild(new MeshInstance3D { Name = "FenceParts_" + parts[0] + "_" + parts[1], Mesh = tool.Commit(),
                MaterialOverride = PainterlyMaterialLibrary.ForColor(parts[0], parts[1]) });
        }
        MountTimberGateAddressPlates(root);
        // The deck seating and the yard biography run here because this is the first
        // structural pass after the tool barn was built (and before the kit blockers
        // are baked), so both see their final world poses.
        SeatYardPropsOnSupportDecks(core);
        ComposeEastHoldingWorkYard(core);
        root.SetMeta("entranceOpenings", openings);
        root.SetMeta("entranceBuilt", gates + wickets);
        root.SetMeta("entranceUnbuiltHoles", Mathf.Max(0, openings - gates - wickets));
        GD.Print($"act1-yard-fences: lots={lots.Count} runs={runs} gates={gates} wickets={wickets} "
            + $"openings={openings} holes={Mathf.Max(0, openings - gates - wickets)} retired={retired} "
            + $"designs={StreetDesigns.Length}");
    }

    /// <summary>VIS-088: the card's numbers are a contract, not a suggestion. A drift
    /// outside it fails loudly in the run log instead of quietly becoming slop.</summary>
    internal static void CheckFenceDesignContract()
    {
        foreach (var design in StreetDesigns.Append(YardBoundaryDesign))
        {
            if (design.BoardThickness is < .020f or > .040f)
                GD.PushError($"VIS-088: fence family {design.Id} has board thickness {design.BoardThickness * 1000f:F0} mm, "
                    + "outside the authored 20–40 mm range");
            if (design.LeanDegrees > 4f || design.SettledLeanDegrees > 8f)
                GD.PushError($"VIS-088: fence family {design.Id} leans {design.LeanDegrees:F1}° "
                    + $"(settled {design.SettledLeanDegrees:F1}°); the contract allows 4° typical, 8° rare");
            var gap = design.BoardSpacing - design.BoardWidth;
            if (gap < .045f)
                GD.PushError($"VIS-088: fence family {design.Id} leaves only {gap * 1000f:F0} mm of daylight "
                    + "between boards; that reads as one solid sheet");
        }
    }

    /// <summary>Lowest terrain under a post footprint (centre and four corners) — the visible support.</summary>
    private static float SupportBase(Vector2 centre, Basis basis, float halfX, float halfZ)
    {
        var lowest = AgentBAct1HeightField.CollisionGround(centre.X, centre.Y);
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
        {
            var corner = basis * new Vector3(sx * halfX, 0f, sz * halfZ);
            lowest = Mathf.Min(lowest, AgentBAct1HeightField.CollisionGround(centre.X + corner.X, centre.Y + corner.Z));
        }
        return lowest;
    }

    private static void FencePart(Dictionary<string, List<Transform3D>> parts, string material, Vector3 size, Transform3D at)
    {
        if (!parts.TryGetValue("box:" + material, out var list)) parts["box:" + material] = list = [];
        list.Add(at * new Transform3D(Basis.Identity.Scaled(size), Vector3.Zero));
    }

    /// <summary>A point on a leaning post at height <paramref name="y"/> above its
    /// own foot. Rails and boards hang from these, so a settled post moves the whole
    /// bay with it instead of leaving the timber floating next to an empty post.</summary>
    private static Vector3 PostPointAt(Vector3 foot, Vector3 along, float leanRadians, float y)
        => foot + Vector3.Up * y + along * (y * Mathf.Tan(leanRadians));

    /// <summary>A basis whose local Z follows a real 3D member direction: yaw around
    /// up first, then the pitch that follows the slope of the two bearing points.</summary>
    private static Basis BeamBasis(Vector3 from, Vector3 to)
    {
        var delta = to - from;
        var horizontal = new Vector2(delta.X, delta.Z).Length();
        return new Basis(Vector3.Up, Mathf.Atan2(delta.X, delta.Z))
             * new Basis(Vector3.Right, -Mathf.Atan2(delta.Y, horizontal));
    }

    // Five real carpentry constructions, with daylight between boards on every
    // boundary. No solid sheet pretending to be wood; the metric UV follows each
    // individual piece.
    private static void BuildFenceRun(Node3D root, StaticBody3D body, List<Vector2> run, bool street, string colour,
        Dictionary<string, List<Transform3D>> parts, FenceDesign authored, uint seed)
    {
        // VIS-019: a yard boundary is a working fence, not a copy of the owner's
        // street palisadnik: plain weathered boards with uneven crowns and one bay
        // that was repaired with fresher timber and a patch rail.
        var design = street ? authored : YardBoundaryDesign;
        var a = run[0]; var b = run[^1];
        var length = a.DistanceTo(b);
        if (length < .8f) return;
        var dir = (b - a) / length;
        var along = new Vector3(dir.X, 0f, dir.Y);
        var basis = new Basis(Vector3.Up, Mathf.Atan2(dir.X, dir.Y));
        var bays = Mathf.Max(1, Mathf.CeilToInt(length / design.BayLength));
        var bay = length / bays;
        var repairBay = design.RepairBay && bays >= 2 ? (int)(seed % (uint)bays) : -1;
        // One settled post per run, and only where the biography says the fence was
        // repaired and had time to settle. Never a random tilt on every board.
        var settled = design.RepairBay && bays >= 3 ? (int)((seed >> 3) % (uint)(bays + 1)) : -1;
        // The street colour is an authored wash over the family's own timber: a
        // painted palisadnik takes the painted trim surface, a wattle or a rail fence
        // stays weathered board and only borrows a little of that colour. A yard
        // boundary is never painted: it is the working fence behind the street face.
        var paint = (street ? colour : "9b886b") + "|" + design.Surface;

        var foot = new Vector3[bays + 1];
        var lean = new float[bays + 1];
        var groundY = new float[bays + 1];
        for (var i = 0; i <= bays; i++)
        {
            var p = a + dir * (i * bay);
            groundY[i] = AgentBAct1HeightField.CollisionGround(p.X, p.Y);
            // VIS-009: the foot follows the lowest corner of the post's own footprint
            // (sunk 1.5 cm) instead of a fixed 5 cm below its centre sample, so a
            // post on a cross-slope neither floats at one corner nor sinks deep.
            foot[i] = new Vector3(p.X, SupportBase(p, basis, design.PostSection * .5f, design.PostSection * .5f) - .015f, p.Y);
            lean[i] = Mathf.DegToRad(i == settled ? design.SettledLeanDegrees : design.LeanDegrees);
            // The post: real section, real height, leaning from its own foot. Its
            // length along the tilted axis is the vertical rise over cos(lean), so
            // the crown lands exactly where the rails are hung.
            var rise = groundY[i] + design.Height + .11f - foot[i].Y;
            var centre = PostPointAt(foot[i], along, lean[i], rise * .5f);
            FencePart(parts, paint, new(design.PostSection, rise / Mathf.Cos(lean[i]), design.PostSection),
                new(basis * new Basis(Vector3.Right, lean[i]), centre));
            FencePart(parts, "e6dfc6|wood_painted_trim", new(design.PostSection + .04f, .045f, design.PostSection + .04f),
                new(basis, PostPointAt(foot[i], along, lean[i], rise + .0225f)));
        }

        // Bearing courses above this bay's own ground: the two load-bearing rails
        // plus whatever the family adds (a woven set, a third rail). No continuous
        // horizontal member crosses the readable middle band of the fence: the
        // daylight there is what makes the run read as separate timber.
        var courses = new List<float>();
        foreach (var y in new[] { design.LowerRailHeight, design.Height - design.UpperRailDrop }
                     .Concat(design.ExtraRailHeights))
            if (!courses.Any(existing => Mathf.Abs(existing - y) < .01f)) courses.Add(y);
        for (var i = 0; i < bays; i++)
        {
            var p0 = a + dir * (i * bay); var p1 = a + dir * ((i + 1) * bay);
            var mid = (p0 + p1) * .5f;
            var gm = (groundY[i] + groundY[i + 1]) * .5f;
            foreach (var y in courses)
            {
                var from = PostPointAt(foot[i], along, lean[i], y);
                var to = PostPointAt(foot[i + 1], along, lean[i + 1], y);
                // Wattle has no separate rails: every course is a woven withy that
                // passes in front of one upright and behind the next.
                FencePart(parts, paint, new(design.RailThickness, design.RailDepth, from.DistanceTo(to)),
                    new(BeamBasis(from, to), (from + to) * .5f));
            }

            // The boards, stakes or uprights of this bay.
            var count = Mathf.Max(1, Mathf.FloorToInt(bay / design.BoardSpacing));
            for (var k = 0; k < count; k++)
            {
                var t = (k + .5f) / count;
                var q = p0.Lerp(p1, t);
                var ground = Mathf.Lerp(groundY[i], groundY[i + 1], t);
                var crownBase = ground + design.Height;
                var bottom = ground + design.LowerRailHeight + design.RailDepth * .5f + .01f;
                var top = design switch
                {
                    // Scalloped palisadnik: one smooth low-high-low wave across the bay.
                    { ScallopedTops: true } => crownBase - .16f * Mathf.Sin(t * Mathf.Pi),
                    // Repaired run: replaced boards stand to the rail, the surviving
                    // old ones were cut shorter by hand — three deterministic lengths.
                    { MixedBoards: true } => crownBase - (k % 2 == 0 ? 0f : .10f)
                                             - ((k * 7 + (int)(seed % 5)) % 3) * .02f,
                    // Woven stakes are cut level and stay under the top withy.
                    { Woven: true } => crownBase - .04f,
                    // A rail-and-stake bay carries its uprights between the two lower rails.
                    { RailWithUprights: true } => crownBase - .34f,
                    // Hand-cut boards: three deterministic lengths, never a smooth curve.
                    _ => crownBase - ((k * 7 + (int)(seed % 5)) % 3) * .025f
                };
                if (design.RailWithUprights) bottom = ground + design.LowerRailHeight - .06f;
                // Wattle uprights are driven into the ground, not hung on a rail.
                if (design.Woven) bottom = ground - .04f;
                var width = design.MixedBoards && k % 2 == 1 ? design.BoardWidth * .65f : design.BoardWidth;
                var repaired = i == repairBay && k >= count / 3 && k < count / 3 + 4;
                // The piece stays plumb: it is nailed to rails that follow the posts,
                // so only the two ends of a leaning bay move. A woven stake steps
                // across the fence line by half its own thickness, front and back.
                var across = design.Woven ? (k % 2 == 0 ? .03f : -.03f) : 0f;
                var offset = basis * new Vector3(across, 0f, 0f);
                FencePart(parts, repaired ? "b9ab8e|wood_fence" : paint,
                    new(design.BoardThickness, top - bottom, width),
                    new(basis, new(q.X + offset.X, (top + bottom) * .5f, q.Y + offset.Z)));
                if (design.PointedTops && !(design.MixedBoards && k % 2 == 1))
                    FencePart(parts, paint, new(.035f, .068f, .068f),
                        new(basis * new Basis(Vector3.Right, Mathf.Pi / 4),
                            new(q.X + offset.X, top, q.Y + offset.Z)));
            }
            if (design.CapRail)
            {
                var capFrom = PostPointAt(foot[i], along, lean[i], design.Height);
                var capTo = PostPointAt(foot[i + 1], along, lean[i + 1], design.Height);
                FencePart(parts, "e3d6b8|wood_painted_trim", new(.055f, .06f, bay),
                    new(BeamBasis(capFrom, capTo), (capFrom + capTo) * .5f));
            }
            if (i == repairBay)
            {
                // The patch rail is nailed across the replaced boards on the yard side.
                var patchT = (count / 3 + 2f) / count;
                var seat = design.Height * .55f;
                var patchFrom = PostPointAt(foot[i], along, lean[i], seat);
                var patchTo = PostPointAt(foot[i + 1], along, lean[i + 1], seat);
                var centre = patchFrom.Lerp(patchTo, patchT);
                var side = basis * new Vector3(.033f + design.BoardThickness, 0f, 0f);
                FencePart(parts, "b9ab8e|wood_fence", new(.03f, .09f, .62f),
                    new(BeamBasis(patchFrom, patchTo), centre + side));
            }
            var contact = new CollisionShape3D {
                Name = $"FenceBay_{body.GetChildCount()}", Shape = new BoxShape3D { Size = new(.14f, design.Height, bay) },
                Transform = new Transform3D(basis, new(mid.X, gm + design.Height * .5f, mid.Y)) };
            contact.SetMeta("timberStyle", design.Style); contact.SetMeta("streetFence", street);
            contact.SetMeta("fenceFamily", design.Id); contact.SetMeta("boardThicknessM", design.BoardThickness);
            contact.SetMeta("bayLengthM", bay); contact.SetMeta("postLeanDegrees", lean[i]);
            body.AddChild(contact);
        }
    }

    private static void BuildTimberStreetGate(Node3D root, StaticBody3D body, Vector2 centre, Vector2 along,
        Vector2 inward, string colour, FenceDesign design, Dictionary<string, List<Transform3D>> parts, string addressId)
    {
        var right = new Vector3(along.X, 0, along.Y);
        var back = new Vector3(inward.X, 0, inward.Y);
        var basis = new Basis(right, Vector3.Up, -back);
        var ground = AgentBAct1HeightField.CollisionGround(centre.X, centre.Y);
        var at = new Vector3(centre.X, ground, centre.Y);
        var paint = colour + "|wood_painted_trim";
        // The passage is measured from the two posts' own inner faces, not from the
        // distance between their centres: a cart gate has to clear a person plus the
        // authored 0.15 m (VIS-089), and here it clears a cart.
        const float postHalf = .085f;
        const float postSpacing = 1.72f;
        var clearWidth = (postSpacing - postHalf) * 2f;
        var anchor = new Node3D { Name = "TimberGate_" + root.GetChildCount(), Position = at };
        anchor.SetMeta("clearWidth", clearWidth);
        anchor.SetMeta("inward", back);
        anchor.SetMeta("along", right);
        anchor.SetMeta("addressId", addressId);
        anchor.SetMeta("timberStyle", design.Style);
        anchor.SetMeta("fenceFamily", design.Id);
        anchor.SetMeta("gateVariant", "cart-double-leaf");
        anchor.SetMeta("minPassageM", clearWidth);
        anchor.SetMeta("pathTarget", at - back * .6f);
        root.AddChild(anchor);
        foreach (var sign in new[] { -1f, 1f })
        {
            var post = at + right * sign * postSpacing;
            // VIS-009: each gate post stands on its own ground. Both used the
            // centre sample, so on a slope one post hung 1.72 m away from it.
            var postBase = SupportBase(new Vector2(post.X, post.Z), basis, postHalf, postHalf) - .015f;
            var postTop = ground + 2.4f;
            FencePart(parts, paint, new(.17f, postTop - postBase, .17f), new(basis, new(post.X, (postTop + postBase) * .5f, post.Z)));
            FencePart(parts, "eadcc0|wood_painted_trim", new(.23f, .055f, .23f), new(basis, post + Vector3.Up * 2.42f));
            // Leaves folded 90 degrees into the yard: the street access stays fully open.
            var leaf = post + back * .65f;
            var leafBasis = new Basis(back, Vector3.Up, right);
            foreach (var y in new[] { .40f, 1.38f })
                FencePart(parts, paint, new(1.30f, .09f, .065f), new(leafBasis, leaf + Vector3.Up*y));
            for (var k=0;k<9;k++)
                FencePart(parts, paint, new(.105f, 1.48f, .04f), new(leafBasis, post + back*(.09f+k*.145f)+Vector3.Up*.82f));
            // The diagonal that keeps a 1.3 m leaf square, nailed from the hinge
            // corner to the far bottom rail end.
            var braceAngle = Mathf.Atan2(.98f, 1.30f);
            FencePart(parts, "6f5b45|wood_fence", new(1.55f, .085f, .05f),
                new(leafBasis * new Basis(Vector3.Forward, braceAngle), leaf + Vector3.Up * .89f));
            // Two iron hinge straps wrap the post and bite onto the leaf frame.
            foreach (var y in new[] { .46f, 1.44f })
            {
                FencePart(parts, "4a4d4a|metal", new(.02f, .07f, .34f), new(leafBasis, post + Vector3.Up * y + back * .02f));
                FencePart(parts, "4a4d4a|metal", new(.022f, .055f, .09f), new(basis, post + Vector3.Up * y + right * (-sign * .075f)));
            }
            // The leaf's own handle, on the closing edge, at a standing hand height.
            FencePart(parts, "4a4d4a|metal", new(.045f, .19f, .045f), new(basis,
                post + back * 1.28f + Vector3.Up * 1.06f + right * (-sign * .10f)));
            body.AddChild(new CollisionShape3D { Name=$"OpenGateLeaf{body.GetChildCount()}",
                Shape=new BoxShape3D {Size=new(1.3f,1.55f,.10f)}, Transform=new(leafBasis,leaf+Vector3.Up*.80f) });
        }
        FencePart(parts, paint, new(3.6f,.14f,.19f),new(basis,at+Vector3.Up*2.32f));
        FencePart(parts, "eadcc0|wood_painted_trim",new(3.72f,.065f,.25f),new(basis,at+Vector3.Up*2.44f));
        // A restrained fan over the capka; actual bars cast their own shadows.
        for(var k=0;k<7;k++)
        {
            var angle=(k-3)*.24f;
            FencePart(parts, "eadcc0|wood_painted_trim", new(.035f,.40f,.04f),
                new(basis*new Basis(Vector3.Back,angle),at+Vector3.Up*2.63f+right*Mathf.Sin(angle)*.20f));
        }
    }

    /// <summary>
    /// VIS-089: a wicket is a physical entrance, not a gap in the fence line. One
    /// leaf on two jamb posts, hinged at the near post, braced diagonally, with two
    /// iron straps, a latch pin on the closing jamb and a handle; the leaf stands
    /// open along the yard side so the passage reads and walks as an entrance. The
    /// clear width is measured from the jamb inner faces and is kept at least
    /// 0.15 m wider than the player capsule (0.35 m radius → 0.70 m, so 0.85 m).
    /// The leaf is a real node under a hinge, so the mechanism owner can swing it
    /// without touching the geometry.
    /// </summary>
    private static void BuildYardWicket(Node3D root, StaticBody3D body, Vector2 centre, Vector2 along,
        Vector2 inward, FenceDesign design, string colour, Dictionary<string, List<Transform3D>> parts, string addressId)
    {
        var right = new Vector3(along.X, 0, along.Y);
        var back = new Vector3(inward.X, 0, inward.Y);
        var basis = new Basis(right, Vector3.Up, -back);
        var ground = AgentBAct1HeightField.CollisionGround(centre.X, centre.Y);
        var at = new Vector3(centre.X, ground, centre.Y);
        var paint = colour + "|wood_painted_trim";
        const float jambHalf = .07f;
        const float jambSpacing = .62f;
        var clearWidth = (jambSpacing - jambHalf) * 2f;
        if (clearWidth < .85f)
            GD.PushError($"VIS-089: wicket on {addressId} clears only {clearWidth:F2} m; the contract wants the "
                + "player capsule plus 0.15 m (0.85 m)");
        var jambTop = ground + design.Height + .22f;
        var anchor = new Node3D { Name = "YardWicket_" + root.GetChildCount(), Position = at };
        anchor.SetMeta("presentationRole", "yard wicket: the lot's real entrance, hinged leaf with brace, straps, latch and handle");
        anchor.SetMeta("clearWidth", clearWidth);
        anchor.SetMeta("inward", back);
        anchor.SetMeta("along", right);
        anchor.SetMeta("addressId", addressId);
        anchor.SetMeta("timberStyle", design.Style);
        anchor.SetMeta("fenceFamily", design.Id);
        anchor.SetMeta("wicketVariant", design.Woven ? "wattle-wicket" : design.RailWithUprights ? "rail-wicket"
            : design.MixedBoards ? "repaired-board-wicket" : design.ScallopedTops ? "palisadnik-wicket" : "board-wicket");
        anchor.SetMeta("minPassageM", clearWidth);
        // The snow path has to end here, not at the fence line (VIS-078/089).
        anchor.SetMeta("pathTarget", at - back * .35f);
        root.AddChild(anchor);

        foreach (var sign in new[] { -1f, 1f })
        {
            var jamb = at + right * sign * jambSpacing;
            var footY = SupportBase(new Vector2(jamb.X, jamb.Z), basis, jambHalf, jambHalf) - .015f;
            FencePart(parts, paint, new(jambHalf * 2f, jambTop - footY, jambHalf * 2f),
                new(basis, new(jamb.X, (jambTop + footY) * .5f, jamb.Z)));
            FencePart(parts, "eadcc0|wood_painted_trim", new(.16f, .045f, .16f), new(basis, jamb + Vector3.Up * (jambTop + .0225f)));
            body.AddChild(new CollisionShape3D { Name = $"WicketJamb{body.GetChildCount()}",
                Shape = new BoxShape3D { Size = new(jambHalf * 2f, jambTop - footY, jambHalf * 2f) },
                Transform = new Transform3D(basis, new(jamb.X, (jambTop + footY) * .5f, jamb.Z)) });
        }
        // Lintel across the entrance and a worn sill under the feet: the opening
        // has a top and a floor, so it reads as a doorway rather than a hole.
        FencePart(parts, paint, new(.12f, .14f, jambSpacing * 2f + .14f), new(basis, at + Vector3.Up * (jambTop + .07f)));
        FencePart(parts, "8d8577|stone_foundation", new(.30f, .05f, clearWidth + .10f),
            new(basis, new(at.X, ground + .025f, at.Z)));

        // The hinged leaf. Authored in the hinge's own frame: local +X is the
        // direction the open leaf travels (into the yard), local Y is up, local Z is
        // its thickness and the hinge line itself is local X = 0. The hinge node
        // sits on the near jamb's inner face, so a rotation about its own Y is a
        // real swing: 0° open against the yard, -90° closed across the entrance.
        var hinge = new Node3D { Name = "WicketHinge" + root.GetChildCount() };
        hinge.Position = -right * (jambSpacing - jambHalf);
        hinge.Basis = new Basis(back, Vector3.Up, right);
        anchor.AddChild(hinge);
        var leafWidth = clearWidth - .02f;
        var leafHeight = design.Height + .04f;
        using var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        void LeafBox(Vector3 size, Vector3 at2, Basis? rotated = null)
            => TimberHomeStyle.AppendMetricBox(tool, new Transform3D((rotated ?? Basis.Identity).Scaled(size), at2));
        // Two bearing rails and the boards of this family's own construction.
        foreach (var y in new[] { .30f, leafHeight - .26f })
            LeafBox(new(leafWidth, .07f, design.RailThickness), new(leafWidth * .5f + .01f, y, 0f));
        var boards = Mathf.Max(1, Mathf.FloorToInt(leafWidth / design.BoardSpacing));
        for (var k = 0; k < boards; k++)
        {
            var x = .02f + (k + .5f) * (leafWidth - .04f) / boards;
            var width = design.MixedBoards && k % 2 == 1 ? design.BoardWidth * .65f : design.BoardWidth;
            var crown = leafHeight - ((k * 5 + design.Style) % 3) * .02f;
            LeafBox(new(design.BoardThickness, crown - .10f, width), new(x, .05f + (crown - .10f) * .5f, 0f));
        }
        // The diagonal brace is what keeps a narrow leaf from racking: it runs from
        // the hinge corner up to the far top rail, on the yard face of the boards.
        var rise = leafHeight - .56f;
        var braceLength = Mathf.Sqrt(leafWidth * leafWidth + rise * rise);
        LeafBox(new(braceLength, .075f, .028f),
            new(leafWidth * .5f + .01f, .30f + rise * .5f, design.RailThickness * .5f + .016f),
            new Basis(Vector3.Forward, Mathf.Atan2(rise, leafWidth)));
        // Ironwork: two straps clamped over the hinge post, a latch plate on the
        // closing edge and a handle bar at a standing hand height.
        foreach (var y in new[] { .34f, leafHeight - .34f })
        {
            LeafBox(new(.30f, .06f, .018f), new(.15f, y, design.RailThickness * .5f + .02f));
            LeafBox(new(.055f, .055f, .19f), new(.022f, y, design.RailThickness * .5f + .10f));
        }
        LeafBox(new(.10f, .055f, .05f), new(leafWidth - .04f, leafHeight - .55f, design.RailThickness * .5f + .026f));
        LeafBox(new(.05f, .18f, .05f), new(leafWidth - .06f, 1.05f, -(design.BoardThickness * .5f + .045f)));
        var leaf = new MeshInstance3D { Name = "WicketLeaf", Mesh = tool.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, "wood_painted_trim") };
        hinge.AddChild(leaf);
        leaf.SetMeta("wicketJoinery",
            "two rails, family boards, diagonal brace, two hinge straps, latch plate, handle; metric UV per piece");
        var leafBody = new StaticBody3D { Name = "WicketLeafContact", CollisionLayer = 1, CollisionMask = 0 };
        leafBody.SetMeta("collisionOwner", "authored-kit-blocker");
        leafBody.SetMeta("footstepSurface", "wood");
        leaf.AddChild(leafBody);
        var leafContact = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new(leafWidth, leafHeight, design.RailThickness + .06f) },
            Position = new(leafWidth * .5f + .01f, leafHeight * .5f, 0f)
        };
        leafContact.SetMeta("authoredSourceMesh", leaf.GetPath().ToString());
        leafBody.AddChild(leafContact);
        body.AddChild(new CollisionShape3D { Name = $"WicketHeader{body.GetChildCount()}",
            Shape = new BoxShape3D { Size = new(.16f, .16f, jambSpacing * 2f + .14f) },
            Transform = new Transform3D(basis, at + Vector3.Up * (jambTop + .07f)) });
        // Open against the yard: the passage between the jambs stays completely
        // free, which is what makes the entrance read as an entrance before any
        // interaction prompt appears.
        anchor.SetMeta("hingePoint", hinge.GlobalPosition);
        anchor.SetMeta("leafWidthM", leafWidth);
        anchor.SetMeta("leafOpenDegrees", 0f);
        anchor.SetMeta("leafClosedDegrees", -90f);
        anchor.SetMeta("leafNode", leaf.GetPath().ToString());
    }
}
