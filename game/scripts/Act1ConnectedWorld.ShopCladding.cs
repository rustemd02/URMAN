using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// ACT1-PUBLIC.FACADES / author photo reference T3 (09.10.2026, a real Tatarstan
/// village shop): the shop is clad in vertically ribbed profiled steel sheet with
/// painted trims, and a small canopy with a fretwork valance board over the door.
/// Colour decision: a white / light-grey sheet body with blue trims (corner boards,
/// window and door casings, plinth band, top fascia, canopy fascia and valance) under
/// the existing blue metal roof. The village is snow-lit grey; a pale body keeps the
/// dark green АШАМЛЫКЛАР sign and the blue roof readable, and white body plus blue
/// trim is exactly the "blue-and-white pavilion" the card asks for, instead of a
/// salon false front or a porch.
///
/// Every exterior wall slab of the annexed kit shell gets two batched surfaces in one
/// mesh: the sheet (trapezoid ribs every 20 cm, 2.5 cm deep, standing 2 cm in front of
/// the slab, cut around the measured window recesses and the entrance) and the trims
/// (boxes standing in front of the ribs). The slab itself, its collision, windows,
/// doors, sign, address mount and interactions are untouched; the claddings are
/// presentation-only siblings, like the log crowns of the dwellings.
/// </summary>
public partial class Act1ConnectedWorld
{
    private const string ShopSheetColour = "d9dedd";
    private const string ShopTrimColour = "426f99";
    private const float ShopRibPitch = .20f;
    private const float ShopRibDepth = .025f;
    private const float ShopSheetGap = .02f;
    private const float ShopPlinth = .25f;
    private const float ShopFascia = .12f;
    private const float ShopCornerBoard = .12f;
    private const float ShopCasing = .10f;
    private static readonly string[] ShopCladWalls = ["_Street", "_Rear", "_Left", "_SeniEntry", "_SeniOuter", "_SeniRear"];

    private void CladShopFacade(PublicBuildingRoom shop, Node3D storefront)
    {
        var building = shop.Building;
        var scale = building.GlobalBasis.Scale.X;
        var k = 1f / scale; // metres -> kit units of the wall meshes
        var allWalls = FindDescendants<MeshInstance3D>(building)
            .Where(m => m.Name.ToString().Contains("_Wall_LOD0", StringComparison.Ordinal)
                        && m.Mesh is not null && !m.HasMeta("shopCladding")).ToArray();
        var entryLocal = shop.Metric.ToLocal(shop.Entrance); // metres, metric space
        var entryX = entryLocal.X * k;
        var sheetMat = PainterlyMaterialLibrary.ForColor(ShopSheetColour, "metal");
        var trimMat = PainterlyMaterialLibrary.ForColor(ShopTrimColour, "wood_painted_trim");
        int clad = 0, walls = 0;

        foreach (var wall in allWalls)
        {
            var name = wall.Name.ToString();
            if (!ShopCladWalls.Any(side => name.EndsWith(side + "_Wall_LOD0", StringComparison.Ordinal)) || !wall.Visible) continue;
            if (wall.GetParent() is not Node3D parent) continue;
            // Defensive: a log crown laid before the public-facade guard existed would show through the ribs.
            foreach (var crown in parent.GetChildren().OfType<MeshInstance3D>()
                         .Where(m => m.Name.ToString() == name.Replace("_Wall_LOD0", "_LogCrown")))
            { crown.Visible = false; crown.SetMeta("suppressionReason", "profiled-sheet shop facade (ACT1-PUBLIC.FACADES/T3)"); }

            var faces = wall.Mesh!.GetFaces();
            if (faces.Length == 0) continue;
            var min = faces[0];
            var max = faces[0];
            foreach (var p in faces) { min = min.Min(p); max = max.Max(p); }
            var size = max - min;
            var alongX = size.X >= size.Z;
            var thickness = alongX ? size.Z : size.X;
            if (thickness is < .08f or > .4f || size.Y < .8f || Mathf.Max(size.X, size.Z) < .8f) continue;

            var toWall = wall.GlobalTransform.AffineInverse();
            var centres = allWalls.Where(m => m.Visible).Select(m => m.GlobalTransform * m.GetAabb().GetCenter()).ToArray();
            var houseCentre = centres.Aggregate(Vector3.Zero, (a, b) => a + b) / centres.Length;
            var centreLocal = toWall * houseCentre;
            var mid = (min + max) * .5f;
            var outward = (alongX ? mid.Z - centreLocal.Z : mid.X - centreLocal.X) >= 0f ? 1f : -1f;
            var face = alongX ? (outward > 0 ? max.Z : min.Z) : (outward > 0 ? max.X : min.X);
            var a0 = alongX ? min.X : min.Z;
            var a1 = alongX ? max.X : max.Z;
            var y0 = min.Y;
            var y1 = max.Y;

            Vector3 P(float a, float t, float y) => alongX ? new(a, y, face + outward * t) : new(face + outward * t, y, a);
            Aabb ToLocalBox(Node3D owner, Aabb box)
            {
                var local = new Aabb(toWall * (owner.GlobalTransform * box.GetEndpoint(0)), Vector3.Zero);
                for (var i = 1; i < 8; i++) local = local.Expand(toWall * (owner.GlobalTransform * box.GetEndpoint(i)));
                return local;
            }
            (float From, float To) Along(Aabb o) => alongX ? (o.Position.X, o.End.X) : (o.Position.Z, o.End.Z);

            // Openings of this wall: window recesses/glass grouped per window, plus the entrance.
            var sidePrefix = name[..name.IndexOf("Wall_LOD0", StringComparison.Ordinal)];
            var windows = new Dictionary<string, Aabb>(StringComparer.Ordinal);
            foreach (var part in parent.GetChildren().OfType<MeshInstance3D>())
            {
                var partName = part.Name.ToString();
                if (!partName.StartsWith(sidePrefix, StringComparison.Ordinal) || part.Mesh is null) continue;
                if (!(partName.Contains("_Recess", StringComparison.Ordinal) || partName.Contains("_Glass", StringComparison.Ordinal))) continue;
                if (partName.Contains("_Door", StringComparison.Ordinal) || partName.Contains("_Portal", StringComparison.Ordinal)) continue;
                var key = partName[..partName.LastIndexOf('_', partName.IndexOf("_LOD0", StringComparison.Ordinal) - 1)];
                var box = ToLocalBox(part, part.GetAabb());
                windows[key] = windows.TryGetValue(key, out var seen) ? seen.Merge(box) : box;
            }
            var openings = windows.Values.Select(w => (Box: w, Door: false)).ToList();
            if (name.EndsWith("_SeniEntry_Wall_LOD0", StringComparison.Ordinal))
            {
                // The explicit doorway cut by PublicBuildingShell.OpenDoor (+-0.54, 0.299..2.46) plus the frame.
                var doorBuilding = new Aabb(new(entryX - .60f, .299f, -2f), new(1.20f, 2.46f - .299f, 4f));
                openings.Add((ToLocalBox(building, doorBuilding), true));
            }
            // Street portals/closed door only where they are really visible.
            foreach (var part in parent.GetChildren().OfType<MeshInstance3D>().Where(m => m.Visible && m.Mesh is not null
                         && m.Name.ToString().StartsWith(sidePrefix, StringComparison.Ordinal)
                         && m.Name.ToString().Contains("_Portal0_Jamb", StringComparison.Ordinal)))
                openings.Add((ToLocalBox(part, part.GetAabb()), true));

            // Other walls that stand against this one: a board at an end that meets them would float mid-facade.
            var neighbours = allWalls.Where(m => m != wall && m.Visible)
                .Select(m => ToLocalBox(m, m.GetAabb())).ToArray();
            bool Junction(float end, float endSign)
            {
                var probe = P(end - endSign * .06f * k, .05f * k, (y0 + y1) * .5f);
                return neighbours.Any(b => b.HasPoint(probe));
            }
            var ends = new[] { (At: a0, Sign: -1f), (At: a1, Sign: 1f) }
                .Select(e => (e.At, e.Sign, Board: !Junction(e.At, e.Sign))).ToArray();
            var lo = a0 + (ends[0].Board ? ShopCornerBoard * k : 0f);
            var hi = a1 - (ends[1].Board ? ShopCornerBoard * k : 0f);
            if (hi - lo < .3f) continue;

            // ---- profiled sheet ---------------------------------------------------------------
            var sheet = new SurfaceTool();
            sheet.Begin(Mesh.PrimitiveType.Triangles);
            var sheetY0 = y0 + ShopPlinth * k;
            var sheetY1 = y1 - ShopFascia * k;
            var gap = ShopSheetGap * k;
            var rib = ShopRibDepth * k;
            var pitch = ShopRibPitch * k;
            var cuts = openings.Select(o => (Along: Along(o.Box), Y0: o.Box.Position.Y, Y1: o.Box.End.Y)).ToArray();
            // One period: valley flat, rising flank, crown flat, falling flank (each a quarter pitch).
            (float A0, float A1, float D0, float D1)[] period =
            [
                (0f, .25f, 0f, 0f), (.25f, .5f, 0f, 1f), (.5f, .75f, 1f, 1f), (.75f, 1f, 1f, 0f)
            ];
            for (var origin = lo; origin < hi - .001f; origin += pitch)
            {
                foreach (var seg in period)
                {
                    var sa = origin + seg.A0 * pitch;
                    var sb = origin + seg.A1 * pitch;
                    if (sa >= hi) break;
                    var d0 = gap + seg.D0 * rib;
                    var d1 = gap + seg.D1 * rib;
                    if (sb > hi) { d1 = d0 + (d1 - d0) * (hi - sa) / (sb - sa); sb = hi; }
                    // Split at opening edges so each piece is either wholly cut or wholly solid.
                    var marks = new List<float> { sa, sb };
                    foreach (var c in cuts)
                    {
                        if (c.Along.From > sa && c.Along.From < sb) marks.Add(c.Along.From);
                        if (c.Along.To > sa && c.Along.To < sb) marks.Add(c.Along.To);
                    }
                    marks.Sort();
                    for (var m = 0; m + 1 < marks.Count; m++)
                    {
                        var s0 = marks[m];
                        var s1 = marks[m + 1];
                        if (s1 - s0 < .0005f) continue;
                        var centre = (s0 + s1) * .5f;
                        var covering = cuts.Where(c => centre > c.Along.From && centre < c.Along.To)
                            .Select(c => (c.Y0, c.Y1)).ToList();
                        var dd0 = Mathf.Lerp(d0, d1, (s0 - sa) / (sb - sa));
                        var dd1 = Mathf.Lerp(d0, d1, (s1 - sa) / (sb - sa));
                        var slope = new Vector2(sb - sa, d1 - d0);
                        // Normal in (along, outward) space: perpendicular to the flank, facing out.
                        var n2 = new Vector2(-slope.Y, slope.X).Normalized();
                        var normal = alongX ? new Vector3(n2.X, 0, outward * n2.Y) : new Vector3(outward * n2.Y, 0, n2.X);
                        foreach (var (from, to) in SubtractSpans(sheetY0, sheetY1, covering))
                        {
                            if (to - from < .02f) continue;
                            ShopQuad(sheet, P(s0, dd0, from), P(s1, dd1, from), P(s1, dd1, to), P(s0, dd0, to), normal,
                                new(s0 * scale, from * scale), new(s1 * scale, from * scale),
                                new(s1 * scale, to * scale), new(s0 * scale, to * scale));
                        }
                    }
                }
            }
            sheet.SetMaterial(sheetMat);

            // ---- trims ------------------------------------------------------------------------
            var trim = new SurfaceTool();
            trim.Begin(Mesh.PrimitiveType.Triangles);
            void Box(float fromA, float toA, float t0, float t1, float fromY, float toY)
            {
                if (toA - fromA < .004f || toY - fromY < .004f) return;
                var dims = alongX ? new Vector3(toA - fromA, toY - fromY, (t1 - t0) * k) : new Vector3((t1 - t0) * k, toY - fromY, toA - fromA);
                TimberHomeStyle.AppendChamferedBox(trim, new Transform3D(Basis.FromScale(dims),
                    P((fromA + toA) * .5f, (t0 + t1) * .5f * k, (fromY + toY) * .5f)), .008f * k);
            }
            var ext = .07f * k;
            // Corner boards (outer corners reach past the neighbouring wall's board).
            foreach (var e in ends.Where(e => e.Board))
            {
                var inner = e.At - e.Sign * ShopCornerBoard * k;
                var outer = e.At + e.Sign * ext;
                Box(Mathf.Min(inner, outer), Mathf.Max(inner, outer), 0f, .085f, y0, y1);
            }
            var fascia0 = ends[0].Board ? a0 - ext : a0;
            var fascia1 = ends[1].Board ? a1 + ext : a1;
            Box(fascia0, fascia1, .005f, .075f, y1 - ShopFascia * k, y1);
            // Plinth band, interrupted by the entrance.
            var plinthCuts = openings.Where(o => o.Door).Select(o => (Along(o.Box).From, Along(o.Box).To)).ToList();
            foreach (var (from, to) in SubtractSpans(fascia0, fascia1, plinthCuts))
                Box(from, to, .0f, .07f, y0, sheetY0);
            // Casings: windows get jambs, header and a projecting sill; the doorway only jambs and header.
            var casing = ShopCasing * k;
            foreach (var o in openings)
            {
                var (from, to) = Along(o.Box);
                var yb = o.Box.Position.Y;
                var yt = o.Box.End.Y;
                Box(from - casing, from, .01f, .07f, o.Door ? y0 : yb - casing, yt + casing);
                Box(to, to + casing, .01f, .07f, o.Door ? y0 : yb - casing, yt + casing);
                Box(from, to, .01f, .07f, yt, yt + casing);
                if (o.Door) continue;
                Box(from, to, .01f, .07f, yb - casing, yb);
                // The projecting sill board below the bottom casing.
                Box(from - casing - .03f * k, to + casing + .03f * k, .01f, .10f, yb - casing - .03f * k, yb - casing + .02f * k);
            }
            trim.SetMaterial(trimMat);

            var result = new ArrayMesh();
            sheet.Commit(result);
            trim.Commit(result);
            var cladding = new MeshInstance3D
            {
                Name = name.Replace("_Wall_LOD0", "_ProfiledSheet"),
                Mesh = result,
                Transform = wall.Transform,
                VisibilityRangeEnd = wall.VisibilityRangeEnd,
                VisibilityRangeEndMargin = wall.VisibilityRangeEndMargin,
                VisibilityRangeFadeMode = wall.VisibilityRangeFadeMode
            };
            cladding.SetMeta("presentationOnly", true);
            cladding.SetMeta("reference", "author photo T3 09.10.2026: ribbed profiled steel sheet, painted trims");
            parent.AddChild(cladding);
            wall.SetMeta("shopCladding", true);
            walls++;
            clad += result.GetSurfaceCount();
        }

        BuildShopCanopy(shop, storefront, sheetMat, trimMat);
        GD.Print($"act1-shop-cladding: walls={walls} surfaces={clad} colour={ShopSheetColour}/{ShopTrimColour}");
    }

    /// <summary>A small mono-pitch sheet canopy over the entrance with a scalloped
    /// fretwork valance board on its front edge (photo T3). Metres, storefront space.</summary>
    private void BuildShopCanopy(PublicBuildingRoom shop, Node3D storefront, Material sheetMat, Material trimMat)
    {
        var building = shop.Building;
        var scale = building.GlobalBasis.Scale.X;
        MeshInstance3D Source(string suffix) => FindDescendants<MeshInstance3D>(building)
            .First(mesh => mesh.Name.ToString().EndsWith(suffix, StringComparison.Ordinal));
        var entry = storefront.ToLocal(shop.Entrance);
        var wallTop = PublicBuildingShell.Bounds(building, Source("_SeniEntry_Wall_LOD0")).End.Y * scale;
        var annex = (PublicBuildingShell.Bounds(building, Source("_SeniOuter_Wall_LOD0")).Position.X
                     - PublicBuildingShell.Bounds(building, Source("_Right_Wall_LOD0")).End.X) * scale;
        var width = Mathf.Clamp(annex, 1.5f, 2.0f);
        const float depth = .85f;
        const float drop = .11f;
        const float thick = .035f;
        var head = entry.Y + (2.46f - .31f) * scale; // top of the doorway
        var roofTop = Mathf.Max(head + .10f, Mathf.Min(head + .20f, wallTop - .03f));
        var angle = Mathf.Atan2(drop, depth);
        var length = Mathf.Sqrt(depth * depth + drop * drop);
        var rot = new Basis(Vector3.Right, angle);
        Vector3 Along(float z) => new(entry.X, roofTop - drop * z / depth, entry.Z + z);

        var roof = new SurfaceTool();
        roof.Begin(Mesh.PrimitiveType.Triangles);
        var trim = new SurfaceTool();
        trim.Begin(Mesh.PrimitiveType.Triangles);
        // Sheet panel with standing ribs along the fall.
        var panelCentre = Along(depth * .5f) - Vector3.Up * thick * .5f;
        TimberHomeStyle.AppendChamferedBox(roof, new Transform3D(new Basis(rot.X * width, rot.Y * thick, rot.Z * length), panelCentre), .004f);
        const int ribs = 9;
        for (var i = 0; i < ribs; i++)
        {
            var x = -width * .5f + .08f + (width - .16f) * i / (ribs - 1);
            var centre = panelCentre + rot.Y * (thick * .5f + .0125f) + Vector3.Right * x;
            TimberHomeStyle.AppendChamferedBox(roof, new Transform3D(new Basis(rot.X * .03f, rot.Y * .025f, rot.Z * (length - .06f)), centre), .006f);
        }
        // Rake boards and the front fascia in the trim colour.
        foreach (var side in new[] { -1f, 1f })
            TimberHomeStyle.AppendChamferedBox(trim, new Transform3D(new Basis(rot.X * .035f, rot.Y * .07f, rot.Z * length),
                panelCentre + Vector3.Right * side * (width * .5f + .0175f) + rot.Y * .0075f), .006f);
        var front = Along(depth) - Vector3.Up * (thick * .5f);
        TimberHomeStyle.AppendChamferedBox(trim, new Transform3D(Basis.FromScale(new(width + .07f, .13f, .03f)),
            front + new Vector3(0, -.045f, .012f)), .006f);
        // Scalloped valance: a row of alternating-height teeth under the fascia.
        var teeth = Mathf.RoundToInt(width / .10f);
        var toothPitch = width / teeth;
        var valanceTop = front.Y - .045f - .065f;
        for (var i = 0; i < teeth; i++)
        {
            var h = i % 2 == 0 ? .095f : .055f;
            TimberHomeStyle.AppendChamferedBox(trim, new Transform3D(Basis.FromScale(new(toothPitch * .72f, h, .022f)),
                new(entry.X - width * .5f + toothPitch * (i + .5f), valanceTop - h * .5f, front.Z + .012f)), .004f);
        }
        // Two diagonal brackets from the wall to the underside of the canopy.
        foreach (var side in new[] { -1f, 1f })
        {
            var wallPoint = new Vector3(entry.X + side * (width * .5f - .14f), roofTop - thick - .40f, entry.Z + .05f);
            var roofPoint = Along(depth * .8f) - Vector3.Up * thick;
            roofPoint.X = wallPoint.X;
            var v = roofPoint - wallPoint;
            var brace = new Basis(Vector3.Right, Mathf.Atan2(-v.Y, v.Z));
            TimberHomeStyle.AppendChamferedBox(trim, new Transform3D(new Basis(brace.X * .045f, brace.Y * .04f, brace.Z * v.Length()),
                (wallPoint + roofPoint) * .5f), .006f);
        }
        roof.SetMaterial(PainterlyMaterialLibrary.ForColor("416a8b", "roof_metal"));
        trim.SetMaterial(trimMat);
        var mesh = new ArrayMesh();
        roof.Commit(mesh);
        trim.Commit(mesh);
        var canopy = new MeshInstance3D { Name = "ShopDoorCanopy", Mesh = mesh };
        canopy.SetMeta("presentationOnly", true);
        canopy.SetMeta("reference", "author photo T3 09.10.2026: small door canopy with scalloped valance board");
        storefront.AddChild(canopy);
    }

    private static void ShopQuad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
        Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
    {
        void Tri((Vector3 P, Vector2 U) p, (Vector3 P, Vector2 U) q, (Vector3 P, Vector2 U) r)
        {
            // Same front-face rule as TimberHomeStyle.AppendChamferedBox.
            if ((q.P - p.P).Cross(r.P - p.P).Dot(normal) < 0f) (q, r) = (r, q);
            foreach (var v in new[] { p, q, r }) { surface.SetNormal(normal); surface.SetUV(v.U); surface.AddVertex(v.P); }
        }
        Tri((a, ua), (b, ub), (c, uc));
        Tri((a, ua), (c, uc), (d, ud));
    }
}
