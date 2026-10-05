using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Straight, wall-to-wall prayer carpet for the Kara-Urman mosque (Act I).
///
/// The previous owner was one 5.30 x 6.05 m box laid parallel to the walls while
/// its woven pattern was rotated to the qibla (bearing 195.1725 deg, i.e. 15.1725
/// deg off the room axes), so the textile's borders cut diagonally across the box
/// and left four bare floor strips: west 3.475 m, east 3.075 m, north 2.500 m and
/// qibla side 0.600 m (including the mihrab bay and the strip in front of the
/// library door). Bare area was 76.36 of 108.43 m2.
///
/// This field covers the complete floor rectangle with one continuous 2 mm
/// ground layer, then builds straight identical 1.60 m mat blocks (two woven
/// mihrab arches each) on the qibla axis with 0.04 m seams, a 0.45 m palas frame
/// against all four walls and a 0.05 m cream guard line inside that frame. No
/// new texture is introduced: the approved green prayer-carpet texture keeps its
/// consumer path, and the frame uses the existing painterly "carpet" family
/// (palas) with authored UVs.
///
/// Integration (project owner, MosqueLayoutV4.cs):
///   line 68: replace the old FacilitySolid("MosquePrayerCarpet", ...) with
///            var carpet = BuildMosquePrayerMatField(room);
///   line 69: delete ApplyMosquePrayerCarpet(carpet);  (this builder owns the
///            required MosquePatternedCarpetSurface child itself)
///   lines 125-156: delete the now-unused ApplyMosquePrayerCarpet method.
/// </summary>
public partial class Act1ConnectedWorld
{
    internal const string MosqueCarpetTexturePath =
        "res://assets/textures/civic/mosque_prayer_carpet_v2_albedo.png";
    // Visible textile and its single contact stay below the doors' 12 mm
    // clearance and below the 3 mm acceptance bound measured on the old root.
    private const float MosqueCarpetThickness = .002f;
    private const float MosqueMatSize = 1.60f;    // one square block = two prayer places
    private const float MosqueMatGap = .04f;      // straight seam between identical mats
    private const float MosqueBorderWidth = .45f; // palas frame along all four walls
    private const float MosqueAccentWidth = .05f; // cream guard line at the frame's inner edge
    private const float MosqueMatLift = .0008f;   // mats rest on the continuous ground, never on the timber

    /// <summary>Builds the whole carpet field and returns the existing support owner.</summary>
    private MeshInstance3D BuildMosquePrayerMatField(Node3D room)
    {
        var timber = room.GetNodeOrNull<MeshInstance3D>("MosqueTimberFloor");
        if (timber?.Mesh is not BoxMesh floorBox)
            throw new InvalidOperationException("Mosque prayer mat field requires the authored box floor.");
        var centre = timber.Position;
        var floorTop = centre.Y + floorBox.Size.Y * .5f;
        var x0 = centre.X - floorBox.Size.X * .5f;
        var x1 = centre.X + floorBox.Size.X * .5f;
        var z0 = centre.Z - floorBox.Size.Z * .5f;
        var z1 = centre.Z + floorBox.Size.Z * .5f;

        // One continuous ground layer covers the complete floor rectangle with
        // zero gaps at all four walls. The node name, 2 mm top and single box
        // contact keep the facilities, grounding, footwear and legacy-save
        // owners unchanged.
        var carpet = FacilitySolid(room, "MosquePrayerCarpet",
            new(floorBox.Size.X, MosqueCarpetThickness, floorBox.Size.Z),
            new(centre.X, floorTop + MosqueCarpetThickness * .5f, centre.Z), "38655f", "fabric");
        carpet.MaterialOverride = PainterlyMaterialLibrary.ForColor("38655f", "fabric", sheltered: true);
        carpet.SetMeta("presentationRole",
            "straight wall-to-wall prayer carpet: continuous ground, 0.45 m palas frame on all four walls, "
            + "1.60 m qibla-aligned mat blocks with 0.04 m seams");
        carpet.SetMeta("carpetFloorRect", new Rect2(x0, z0, x1 - x0, z1 - z0));
        carpet.SetMeta("carpetTopMetres", floorTop + MosqueCarpetThickness);
        carpet.SetMeta("qiblaBearingDegrees", MosqueQiblaBearingDegrees);

        // Read the qibla lane at build time; the room may rotate under the world.
        // The published up direction keeps the exact convention the old surface
        // used, so the mosque layout smoke test still dots it into the hall.
        var qibla = MosqueLocalQibla.Normalized();
        carpet.SetMeta("textureUpDirection", qibla);
        var q = new Vector3(qibla.X, 0, qibla.Z);
        if (q.LengthSquared() < .5f)
            throw new InvalidOperationException("The mosque qibla lane lost its horizontal axis.");
        q = q.Normalized();
        var across = new Vector3(-q.Z, 0, q.X);

        carpet.AddChild(BuildMosquePrayerMats(carpet, floorTop, q, across, x0, x1, z0, z1));
        carpet.AddChild(BuildMosquePrayerCarpetFrame(carpet, floorTop, x0, x1, z0, z1));
        carpet.AddChild(BuildMosquePrayerCarpetFrameLine(carpet, floorTop, x0, x1, z0, z1));
        return carpet;
    }

    /// <summary>
    /// One merged surface with every mat block. Blocks are identical squares in a
    /// straight grid whose depth axis is exactly the qibla lane; the grid is
    /// centred on the authored floor centre and clipped to the frame's inner
    /// edge, so the qibla-facing arch heads repeat in unbroken lines. Each block
    /// maps the complete approved texture (square, no distortion, no skew).
    /// </summary>
    private MeshInstance3D BuildMosquePrayerMats(MeshInstance3D carpet, float floorTop,
        Vector3 q, Vector3 across, float x0, float x1, float z0, float z1)
    {
        var innerX0 = x0 + MosqueBorderWidth;
        var innerX1 = x1 - MosqueBorderWidth;
        var innerZ0 = z0 + MosqueBorderWidth;
        var innerZ1 = z1 - MosqueBorderWidth;
        var centre = new Vector2(carpet.Position.X, carpet.Position.Z);
        var matTop = floorTop + MosqueCarpetThickness + MosqueMatLift;
        var toCarpet = -carpet.Position;
        var pitch = MosqueMatSize + MosqueMatGap;
        // Bounding half-extents of the inner rectangle in the qibla frame.
        var halfAcross = ((innerX1 - innerX0) * Math.Abs(across.X) + (innerZ1 - innerZ0) * Math.Abs(across.Z)) * .5f;
        var halfAlong = ((innerX1 - innerX0) * Math.Abs(q.X) + (innerZ1 - innerZ0) * Math.Abs(q.Z)) * .5f;
        var acrossCount = Mathf.CeilToInt((halfAcross + MosqueMatSize * .5f) / pitch);
        var alongCount = Mathf.CeilToInt((halfAlong + MosqueMatSize * .5f) / pitch);

        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var i = -acrossCount; i <= acrossCount; i++)
        for (var j = -alongCount; j <= alongCount; j++)
        {
            var blockAcross = i * pitch;
            var blockAlong = j * pitch;
            var blockCentre = new Vector3(centre.X, 0, centre.Y) + across * blockAcross + q * blockAlong;
            Vector2 Corner(float side, float depth) => new(
                blockCentre.X + side * MosqueMatSize * .5f * across.X + depth * MosqueMatSize * .5f * q.X,
                blockCentre.Z + side * MosqueMatSize * .5f * across.Z + depth * MosqueMatSize * .5f * q.Z);
            var polygon = new List<Vector2>(6) { Corner(-1, -1), Corner(1, -1), Corner(1, 1), Corner(-1, 1) };
            polygon = ClipMosquePolygon(polygon, 0, innerX0, keepGreater: true);
            polygon = ClipMosquePolygon(polygon, 0, innerX1, keepGreater: false);
            polygon = ClipMosquePolygon(polygon, 1, innerZ0, keepGreater: true);
            polygon = ClipMosquePolygon(polygon, 1, innerZ1, keepGreater: false);
            // A cut mat is kept while it still reads as carpet; a sub-5 cm sliver
            // is dropped and the continuous ground shows instead of debris.
            if (polygon.Count < 3 || MosquePolygonArea(polygon) < .03f || MosquePolygonMinSpan(polygon) < .05f)
                continue;
            for (var k = 1; k < polygon.Count - 1; k++)
            {
                Emit(polygon[0], blockAcross, blockAlong);
                Emit(polygon[k], blockAcross, blockAlong);
                Emit(polygon[k + 1], blockAcross, blockAlong);
            }

            void Emit(Vector2 point, float centreAcross, float centreAlong)
            {
                var along = (point.X - centre.X) * q.X + (point.Y - centre.Y) * q.Z;
                var lateral = (point.X - centre.X) * across.X + (point.Y - centre.Y) * across.Z;
                surface.SetNormal(Vector3.Up);
                // Image upward means decreasing V: arch heads point at the qibla.
                surface.SetUV(new Vector2(.5f + (lateral - centreAcross) / MosqueMatSize,
                    .5f - (along - centreAlong) / MosqueMatSize));
                surface.AddVertex(new Vector3(point.X, matTop, point.Y) + toCarpet);
            }
        }

        var textile = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            Roughness = .98f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        if (ResourceLoader.Exists(MosqueCarpetTexturePath))
            textile.AlbedoTexture = GD.Load<Texture2D>(MosqueCarpetTexturePath);
        else
            GD.PushWarning("Mosque prayer mat texture awaits asset import: " + MosqueCarpetTexturePath);
        var field = new MeshInstance3D
        {
            Name = "MosquePatternedCarpetSurface",
            Mesh = surface.Commit(),
            MaterialOverride = textile,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        field.SetMeta("textureConsumer", MosqueCarpetTexturePath);
        field.SetMeta("textureTileMetres", new Vector2(MosqueMatSize, MosqueMatSize));
        field.SetMeta("textureUpDirection", q);
        field.SetMeta("matBlockMetres", MosqueMatSize);
        field.SetMeta("matSeamMetres", MosqueMatGap);
        return field;
    }

    /// <summary>
    /// The frame follows the four walls (a carpet border is laid to the room),
    /// while the mat rows follow the qibla lane. Both axes meet because the frame
    /// sits on the continuous ground and the blocks are clipped to its inner
    /// edge, so no wedge between the rotated field and the walls can expose the
    /// timber. Stripes run lengthwise around the frame through authored UVs.
    /// </summary>
    private MeshInstance3D BuildMosquePrayerCarpetFrame(MeshInstance3D carpet, float floorTop,
        float x0, float x1, float z0, float z1)
    {
        var top = floorTop + MosqueCarpetThickness + .001f;
        var toCarpet = -carpet.Position;
        using var border = new SurfaceTool();
        border.Begin(Mesh.PrimitiveType.Triangles);
        void Piece(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Func<Vector2, Vector2> uv)
        {
            void Vertex(Vector2 p)
            {
                border.SetNormal(Vector3.Up);
                border.SetUV(uv(p));
                border.AddVertex(new Vector3(p.X, top, p.Y) + toCarpet);
            }
            Vertex(a); Vertex(b); Vertex(c);
            Vertex(a); Vertex(c); Vertex(d);
        }
        var w = MosqueBorderWidth;
        // South band owns the corner squares; the other three stop against it.
        // UV metres pair with the carpet family scale (0.25, 0.40): one 4 m repeat
        // along each band, the palas bands running lengthwise.
        Piece(new(x0, z0), new(x1, z0), new(x1, z0 + w), new(x0, z0 + w),
            p => new Vector2(p.X, p.Y - z0 + .55f));
        Piece(new(x1, z1), new(x0, z1), new(x0, z1 - w), new(x1, z1 - w),
            p => new Vector2(x0 + x1 - p.X, z1 - p.Y + .55f));
        Piece(new(x0, z0 + w), new(x0 + w, z0 + w), new(x0 + w, z1 - w), new(x0, z1 - w),
            p => new Vector2(p.Y, p.X - x0 + .55f));
        Piece(new(x1, z1 - w), new(x1 - w, z1 - w), new(x1 - w, z0 + w), new(x1, z0 + w),
            p => new Vector2(z1 - p.Y, x1 - p.X + .55f));

        var mesh = border.Commit();
        var material = (ShaderMaterial)PainterlyMaterialLibrary
            .ForColor("8d765c", "carpet", sheltered: true).Duplicate();
        material.SetShaderParameter("authored_uv_texture", true);
        var frame = new MeshInstance3D
        {
            Name = "MosquePrayerCarpetBorder",
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        frame.SetMeta("frameWidthMetres", MosqueBorderWidth);
        frame.SetMeta("frameMaterial", "painterly carpet family (palas); no new texture");
        return frame;
    }

    /// <summary>A 5 cm woven guard line inside the palas frame; the mats stop at
    /// the frame edge and the line sits on the frame's inner centimetres, so the
    /// two never overlap.</summary>
    private MeshInstance3D BuildMosquePrayerCarpetFrameLine(MeshInstance3D carpet, float floorTop,
        float x0, float x1, float z0, float z1)
    {
        var top = floorTop + MosqueCarpetThickness + .0015f;
        var toCarpet = -carpet.Position;
        var w = MosqueBorderWidth;
        var a = MosqueAccentWidth;
        var inner0X = x0 + w - a; var inner1X = x1 - w + a;
        var inner0Z = z0 + w - a; var inner1Z = z1 - w + a;
        using var line = new SurfaceTool();
        line.Begin(Mesh.PrimitiveType.Triangles);
        void Strip(float bx0, float bz0, float bx1, float bz1, bool alongX)
        {
            var corners = new[]
            {
                new Vector2(bx0, bz0), new Vector2(bx1, bz0), new Vector2(bx1, bz1), new Vector2(bx0, bz1)
            };
            foreach (var index in new[] { 0, 1, 2, 0, 2, 3 })
            {
                line.SetNormal(Vector3.Up);
                line.SetUV(alongX ? new Vector2(corners[index].X, corners[index].Y)
                    : new Vector2(corners[index].Y, corners[index].X));
                line.AddVertex(new Vector3(corners[index].X, top, corners[index].Y) + toCarpet);
            }
        }
        Strip(inner0X, inner0Z, inner1X, inner0Z + a, alongX: true);
        Strip(inner0X, inner1Z - a, inner1X, inner1Z, alongX: true);
        Strip(inner0X, inner0Z, inner0X + a, inner1Z, alongX: false);
        Strip(inner1X - a, inner0Z, inner1X, inner1Z, alongX: false);

        return new MeshInstance3D
        {
            Name = "MosquePrayerCarpetBorderLine",
            Mesh = line.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("d8c9a0", "cloth", sheltered: true),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
    }

    /// <summary>Sutherland-Hodgman clip of one convex mat block against an
    /// axis-aligned room half-plane. Vector2 stores room X and Z.</summary>
    private static List<Vector2> ClipMosquePolygon(List<Vector2> polygon, int axis, float limit, bool keepGreater)
    {
        var result = new List<Vector2>(polygon.Count + 1);
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            var da = keepGreater ? a[axis] - limit : limit - a[axis];
            var db = keepGreater ? b[axis] - limit : limit - b[axis];
            if (da >= 0)
                result.Add(a);
            if (da >= 0 != db >= 0)
            {
                var t = da / (da - db);
                result.Add(new Vector2(Mathf.Lerp(a.X, b.X, t), Mathf.Lerp(a.Y, b.Y, t)));
            }
        }
        return result;
    }

    private static float MosquePolygonArea(List<Vector2> polygon)
    {
        var area = 0f;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(area) * .5f;
    }

    private static float MosquePolygonMinSpan(List<Vector2> polygon)
    {
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var minZ = float.PositiveInfinity;
        var maxZ = float.NegativeInfinity;
        foreach (var p in polygon)
        {
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minZ = Math.Min(minZ, p.Y); maxZ = Math.Max(maxZ, p.Y);
        }
        return Math.Min(maxX - minX, maxZ - minZ);
    }
}
