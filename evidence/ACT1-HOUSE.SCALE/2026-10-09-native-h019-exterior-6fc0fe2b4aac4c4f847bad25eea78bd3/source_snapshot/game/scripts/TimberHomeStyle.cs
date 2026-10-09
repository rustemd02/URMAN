using Godot;

namespace Urman.Godot;

/// <summary>Metre-scale exterior carpentry from research H01–H05. Decoration is
/// physical timber with light between its members, not an image of a facade.</summary>
public static class TimberHomeStyle
{
    public static uint StableHash(string text)
    {
        uint hash = 2166136261;
        foreach (var c in text) hash = unchecked((hash ^ c) * 16777619);
        return hash;
    }

    // Baked boxes keep metric UV after scaling; AppendFrom(BoxMesh) otherwise
    // stretches one normalized texture over an entire fence bay.
    public static void AppendMetricBox(SurfaceTool s, Transform3D t)
    {
        var half = Vector3.One * .5f;
        foreach (var (n,u,v) in new[] {
            (Vector3.Right,Vector3.Back,Vector3.Up),(Vector3.Left,Vector3.Forward,Vector3.Up),
            (Vector3.Up,Vector3.Right,Vector3.Back),(Vector3.Down,Vector3.Right,Vector3.Forward),
            (Vector3.Back,Vector3.Left,Vector3.Up),(Vector3.Forward,Vector3.Right,Vector3.Up) })
        {
            void V(float x,float y)
            {
                s.SetNormal((t.Basis.Inverse().Transposed()*n).Normalized());
                s.SetUV(new Vector2(x*(t.Basis*u).Length(),y*(t.Basis*v).Length()));
                s.AddVertex(t*(n*.5f+u*x+v*y));
            }
            V(-.5f,-.5f);V(.5f,-.5f);V(.5f,.5f);
            V(-.5f,-.5f);V(.5f,.5f);V(-.5f,.5f);
        }
    }

    // VIS-018/VIS-086/VIS-087 — metric bedding for the runtime joinery, in metres.
    // A board is pressed TrimBedMeters into the plane it stands on, so a light
    // slit can never open behind it, and it then clears that plane by its own
    // thickness minus the bed (the mounting gap stays far below the 3 mm the card
    // allows). TrimEdgeChamfer is the arris on the edges that catch the low winter
    // sun; it is applied to the emitted carpentry members, never to hidden
    // geometry.
    public const float TrimBedMeters = .005f;
    public const float TrimEdgeChamfer = .004f;
    // A runtime-added storey has no wall mesh to measure: its own glass sits
    // 45 mm in front of the added wall box, so the joinery beds 60 mm back from
    // the pane centre - past the wall face - and stands on that wall.
    private const float AddedStoreyBedPlane = -.060f;

    /// <summary>One measured window opening: its plane, its size in world metres
    /// and the plane its surround must be planted on. Nothing here is assumed
    /// about the imported kit's axis convention (VIS-018).</summary>
    private readonly record struct WindowFace(Vector3 Centre, Vector3 Outward, Vector3 Right,
        float Width, float Height, float Plant, bool Casing);

    private static readonly string[] WallFaceParts = ["_Wall_", "_Log", "GableLog"];
    private static readonly string[] WallFaceReject = ["Glass", "Recess", "Jamb", "Rail", "Mullion",
        "Sill", "Kokoshnik", "Towel", "Roof", "Snow", "Gutter", "Rafter", "Downpipe", "Door",
        "Podzor", "Prichelina"];
    private static readonly string[] CasingJamb = ["_Jamb"];
    private static readonly string[] CasingRail = ["_Rail"];

    /// <summary>Chamfered solid member: the four edges running along the member's
    /// longest metric axis are bevelled. The transform's basis lengths own the
    /// dimensions; its normalized axes supply orientation only, so scaling is not
    /// applied a second time while baking vertices. UVs remain in metric units.</summary>
    public static void AppendChamferedBox(SurfaceTool s, Transform3D t, float chamfer)
    {
        var axis = new[] { t.Basis.X, t.Basis.Y, t.Basis.Z };
        var length = new[] { axis[0].Length(), axis[1].Length(), axis[2].Length() };
        var orientation = new Basis(axis[0] / length[0], axis[1] / length[1], axis[2] / length[2]);
        // Normalizing each authored axis preserves a reflected basis. Keep that
        // reflection in the vertices and normals, and compensate only the winding.
        var reflected = orientation.Determinant() < 0f;
        var long_ = 0;
        if (length[1] > length[long_]) long_ = 1;
        if (length[2] > length[long_]) long_ = 2;
        var u = (long_ + 1) % 3; var v = (long_ + 2) % 3;
        var hu = length[u] * .5f; var hv = length[v] * .5f; var hl = length[long_] * .5f;
        var c = Math.Min(chamfer, Math.Min(hu, hv) * .9f);
        Vector3 Local(float a, float b, float l)
        {
            var p = Vector3.Zero; p[long_] = l; p[u] = a; p[v] = b; return p;
        }
        var ring = new[]
        {
            new Vector2(-hu + c, -hv), new Vector2(hu - c, -hv), new Vector2(hu, -hv + c), new Vector2(hu, hv - c),
            new Vector2(hu - c, hv), new Vector2(-hu + c, hv), new Vector2(-hu, hv - c), new Vector2(-hu, -hv + c)
        };
        void Face(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 normal,
            Func<Vector3, Vector2> uv)
        {
            var n = (orientation.Inverse().Transposed() * normal).Normalized();
            void Emit(Vector3 p)
            {
                s.SetNormal(n); s.SetUV(uv(p)); s.AddVertex(t.Origin + orientation * p);
            }
            // Godot front faces are clockwise seen from the normal side; the two
            // triangles of the quad are wound together, never one flipped alone.
            // A reflected transform reverses both triangles as a pair.
            if (((b - a).Cross(cc - a).Dot(normal) < 0) != reflected)
            { Emit(a); Emit(cc); Emit(b); Emit(a); Emit(d); Emit(cc); }
            else
            { Emit(a); Emit(b); Emit(cc); Emit(a); Emit(cc); Emit(d); }
        }
        for (var i = 0; i < 8; i++)
        {
            var p0 = ring[i]; var p1 = ring[(i + 1) % 8];
            var edge = p1 - p0;
            var normal = new Vector3(0f, 0f, 0f); normal[u] = edge.Y; normal[v] = -edge.X;
            if (normal.LengthSquared() < 1e-12f) continue;
            normal = normal.Normalized();
            var flat = new Vector3(0f, 0f, 0f); flat[u] = p0.X; flat[v] = p0.Y;
            var next = new Vector3(0f, 0f, 0f); next[u] = p1.X; next[v] = p1.Y;
            // Grain runs along the member's length: UV x is the local length
            // coordinate, UV y the section coordinate, both already in metres.
            Face(Local(flat.X, flat.Y, -hl), Local(next.X, next.Y, -hl),
                Local(next.X, next.Y, hl), Local(flat.X, flat.Y, hl), normal,
                p => new Vector2(p[long_], p[u]));
        }
        foreach (var sign in new[] { -1f, 1f })
        {
            var normal = new Vector3(0f, 0f, 0f); normal[long_] = sign;
            Face(Local(-hu, -hv, sign * hl), Local(hu, -hv, sign * hl), Local(hu, hv, sign * hl),
                Local(-hu, hv, sign * hl), normal, p => new Vector2(p[u], p[v]));
        }
    }

    public static void DressParcel(Node3D visual, string stableId)
    {
        var seed=StableHash(stableId); var variant=(int)(seed%5);
        string[] paints=["6e9680","6993a2","b6aa83","977765","82946d"];
        var paint=paints[variant];
        foreach(var dwelling in visual.FindChildren("*","Node3D",true,false).OfType<Node3D>()
            .Where(n=>n.Name.ToString().EndsWith("_Dwelling",StringComparison.Ordinal)).ToArray())
        {
            var source=dwelling.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>()
                .Where(m=>m.Mesh is not null&&m.IsVisibleInTree()).ToArray();
            if(source.Length==0)continue;
            var root=new Node3D {Name="TatarCarpentry"};dwelling.AddChild(root);
            // Work in world metres, so narrowly scaled parcels keep full-size joinery.
            root.GlobalBasis=dwelling.GlobalBasis.Orthonormalized();
            var batches=new Dictionary<string,List<Transform3D>>();
            void Box(string material,Vector3 size,Transform3D global)
            {
                if(!batches.TryGetValue(material,out var pieces))batches[material]=pieces=[];
                // The declared metric size is applied here exactly once. Callers pass
                // orientation and position only; the baker consumes these basis lengths.
                pieces.Add(root.GlobalTransform.AffineInverse()*global*new Transform3D(Basis.Identity.Scaled(size),Vector3.Zero));
            }
            var windows=source.Where(m=>m.Name.ToString().Contains("_Window",StringComparison.Ordinal)
                &&m.Name.ToString().Contains("_Glass",StringComparison.Ordinal)).ToArray();
            foreach(var frame in source.Where(m=>new[]{"_Jamb","_Rail","_Verge","_Mullion"}.Any(m.Name.ToString().Contains)))
                frame.MaterialOverride=PainterlyMaterialLibrary.ForColor(paint,"wood_painted_trim");
            // VIS-018: the facade plane is measured per dwelling from the wall and
            // hewn-course members, never guessed from the glass plane. Every pane
            // then knows how far in front of the glass the visible wall face is.
            var envelope=EnvelopeCentre(source);
            var measured=0; var duplicated=0;
            foreach(var pane in windows)
            {
                if(!TryMeasureWindowFace(pane,source,envelope,out var face))continue;
                if(face.Casing)duplicated++;
                Surround(face,variant,paint,Box);
                measured++;
            }
            LightOccupiedWindows(dwelling, stableId);
            // A small minority has a real added storey. The existing pitched roof
            // and its snow/flashings move together; collision is baked afterwards.
            var twoStorey=seed%17==0;
            if(twoStorey)
            {
                var walls=source.Where(m=>m.Name.ToString().Contains("_Wall",StringComparison.Ordinal)
                    &&!m.Name.ToString().Contains("Seni",StringComparison.Ordinal)).ToArray();
                if(walls.Length>0)
                {
                    var inv=root.GlobalTransform.AffineInverse();
                    var bounds=inv*walls[0].GlobalTransform*walls[0].GetAabb();
                    foreach(var m in walls.Skip(1))bounds=bounds.Merge(inv*m.GlobalTransform*m.GetAabb());
                    const float added=2.40f;
                    // Match part tokens, never the variant tag: every VariantA mesh is
                    // named "..._TimberGable_Dwelling_...", so a bare "Gable" substring
                    // lifted the foundation, walls and windows of the whole main volume
                    // 2.4 m and left the house standing with no ground floor (Tukay 4).
                    // "Eave" (not "Eaves") is the part name; RafterTail belongs to
                    // the same lifted roof group.
                    foreach(var roof in source.Where(m=>!m.Name.ToString().Contains("Seni",StringComparison.Ordinal)
                        &&new[]{"Roof","BoardedGable","Verge","Eave","Gutter","Chimney","Ridge","RafterTail"}.Any(m.Name.ToString().Contains)))
                        roof.GlobalPosition+=Vector3.Up*added;
                    var w=bounds.Size.X;var d=bounds.Size.Z;var y=bounds.End.Y;
                    var centre=bounds.GetCenter();
                    Box(paint+"|wood_facade",new(w,added,d),root.GlobalTransform*new Transform3D(Basis.Identity,new(centre.X,y+added*.5f,centre.Z)));
                    Box("dfdcc5|wood_painted_trim",new(w+.05f,.12f,d+.05f),root.GlobalTransform*new Transform3D(Basis.Identity,new(centre.X,y+.08f,centre.Z)));
                    foreach(var x in new[]{-.28f,0f,.28f})
                    {
                        var at=root.GlobalTransform*new Transform3D(Basis.Identity,new(centre.X+x*w,y+1.15f,bounds.End.Z+.045f));
                        Box("536f74|glass",new(.86f,1.15f,.03f),at);
                        // No wall mesh to measure here: the pane's own face plane and
                        // the storey wall's outward direction are known from the box
                        // that was just added, so the frame is planted on that wall.
                        Surround(new WindowFace(at.Origin, root.GlobalBasis*Vector3.Back, root.GlobalBasis*Vector3.Right,
                            .86f, 1.15f, AddedStoreyBedPlane, Casing: false), variant, paint, Box);
                    }
                    root.SetMeta("addedStoreyMetres",added);
                }
            }
            foreach(var (material,pieces) in batches)
            {
                using var s=new SurfaceTool();s.Begin(Mesh.PrimitiveType.Triangles);
                foreach(var t in pieces)AppendChamferedBox(s,t,TrimEdgeChamfer);
                s.Index();var bits=material.Split('|');
                var mesh = new MeshInstance3D {Name="CarvedTimber_"+bits[0]+"_"+bits[1],Mesh=s.Commit(),
                    MaterialOverride=bits[1]=="glass" ? VillageWindowMaterials.For(stableId) : PainterlyMaterialLibrary.ForColor(bits[0],bits[1])};
                if(bits[1]=="glass") mesh.SetMeta("occupiedWindow",true);
                root.AddChild(mesh);
            }
            root.SetMeta("householdStyle",variant);root.SetMeta("twoStorey",twoStorey);
            // VIS-018 audit trail: how many panes were measured, and how many of
            // them already carry the kit's own four-member casing (those get only
            // the crown layer, never a second frame inside the same reveal).
            root.SetMeta("surroundMeasuredPanes",measured);
            root.SetMeta("surroundAuthoredCasingPanes",duplicated);
        }
    }

    /// <summary>World centre of the dwelling's own visible envelope. Used only to
    /// decide which side of a pane the street is on.</summary>
    private static Vector3 EnvelopeCentre(MeshInstance3D[] source)
    {
        Aabb? box = null;
        foreach (var m in source)
        {
            var name = m.Name.ToString();
            if (name.Contains("Glass", StringComparison.Ordinal) || name.Contains("Snow", StringComparison.Ordinal))
                continue;
            var b = m.GlobalTransform * m.GetAabb();
            box = box is { } merged ? merged.Merge(b) : b;
        }
        return box is { } measured ? measured.GetCenter() : Vector3.Zero;
    }

    private static (float Min, float Max) Span(Aabb b, Vector3 axis)
    {
        var min = float.MaxValue; var max = float.MinValue;
        for (var i = 0; i < 8; i++)
        {
            var d = axis.Dot(b.GetEndpoint(i));
            if (d < min) min = d;
            if (d > max) max = d;
        }
        return (min, max);
    }

    /// <summary>Measures one authored pane. The thin axis of the pane's own box is
    /// its normal; the outward sign comes from the dwelling envelope, so an
    /// imported axis convention can never bury the joinery inside the wall (the
    /// defect this card exists for).</summary>
    private static bool TryMeasureWindowFace(MeshInstance3D pane, MeshInstance3D[] source, Vector3 envelope,
        out WindowFace face)
    {
        face = default;
        var box = pane.Mesh!.GetAabb();
        var centre = pane.GlobalTransform * box.GetCenter();
        var scale = pane.GlobalBasis.Scale;
        var size = new[]
        {
            Math.Abs(box.Size.X * scale.X), Math.Abs(box.Size.Y * scale.Y), Math.Abs(box.Size.Z * scale.Z)
        };
        var thin = 0;
        if (size[1] < size[thin]) thin = 1;
        if (size[2] < size[thin]) thin = 2;
        var other = new[] { 0, 1, 2 }.Where(i => i != thin).ToArray();
        var width = Math.Min(size[other[0]], size[other[1]]);
        var height = Math.Max(size[other[0]], size[other[1]]);
        if (width < .1f || height < .1f || !centre.IsFinite()) return false;
        var local = new[] { pane.GlobalBasis.X, pane.GlobalBasis.Y, pane.GlobalBasis.Z };
        if (local[thin].LengthSquared() < 1e-12f) return false;
        var normal = local[thin].Normalized();
        var outward = normal.Dot(centre - envelope) >= 0 ? normal : -normal;
        var right = Vector3.Up.Cross(outward);
        if (right.LengthSquared() < 1e-8f) return false;
        right = right.Normalized();
        // The visible facade plane: the frontmost wall or hewn-course member that
        // actually stands behind this opening, measured from the glass centre.
        var plant = float.MinValue; var found = false;
        foreach (var m in source)
        {
            if (ReferenceEquals(m, pane)) continue;
            var name = m.Name.ToString();
            if (!WallFaceParts.Any(part => name.Contains(part, StringComparison.Ordinal))) continue;
            if (WallFaceReject.Any(part => name.Contains(part, StringComparison.Ordinal))) continue;
            var b = m.GlobalTransform * m.GetAabb();
            var (rMin, rMax) = Span(b, right); var (yMin, yMax) = Span(b, Vector3.Up);
            if (rMin > centre.Dot(right) - width * .5f || rMax < centre.Dot(right) + width * .5f) continue;
            if (yMin > centre.Y - height * .5f || yMax < centre.Y + height * .5f) continue;
            var d = Span(b, outward).Max - outward.Dot(centre);
            if (d <= .001f || d > .6f) continue;
            if (d > plant) { plant = d; found = true; }
        }
        if (!found) plant = 0f;
        // Does the kit already carry a casing on this opening? Then the runtime
        // paints it and adds only the crown layer, planted on the casing's own
        // front face (VIS-018: no second frame inside one reveal).
        var stem = pane.Name.ToString();
        var cut = stem.IndexOf("_Glass", StringComparison.Ordinal);
        if (cut > 0) stem = stem[..cut];
        var jamb = false; var rail = false; var frame = float.MinValue;
        foreach (var m in source)
        {
            var name = m.Name.ToString();
            if (!name.StartsWith(stem + "_", StringComparison.Ordinal)) continue;
            if (CasingJamb.Any(part => name.Contains(part, StringComparison.Ordinal))) jamb = true;
            else if (CasingRail.Any(part => name.Contains(part, StringComparison.Ordinal))) rail = true;
            else continue;
            var d = Span(m.GlobalTransform * m.GetAabb(), outward).Max - outward.Dot(centre);
            if (d > frame) frame = d;
        }
        var casing = jamb && rail && frame > plant;
        if (casing) plant = frame;
        face = new WindowFace(centre, outward, right, width, height, plant, casing);
        return true;
    }

    /// <summary>The household's own crown and, where the kit has no casing, the
    /// full frame. Every member is bedded TrimBedMeters into the plane it stands
    /// on, so it is nailed to the facade instead of hovering in front of it.</summary>
    private static void Surround(WindowFace face, int style, string paint,
        Action<string, Vector3, Transform3D> box)
    {
        var mat = paint + "|wood_painted_trim"; var ivory = "dfdcc5|wood_painted_trim";
        var width = face.Width; var height = face.Height;
        var frame = new Basis(face.Right, Vector3.Up, face.Outward);
        void Part(string material, Vector3 size, float alongRight, float up, float layer, float roll = 0f)
        {
            var basis = roll == 0f ? frame : new Basis(face.Outward, roll) * frame;
            var origin = face.Centre + face.Right * alongRight + Vector3.Up * up
                + face.Outward * (face.Plant + layer + size.Z * .5f - TrimBedMeters);
            // Box owns dimensions; pass this member's orientation without pre-scaling it.
            box(material, size, new Transform3D(basis, origin));
        }
        if (!face.Casing)
        {
            foreach (var sign in new[] { -1f, 1f })
            {
                Part(mat, new(.105f, height + .28f, .06f), sign * (width * .5f + .075f), 0f, 0f);
                Part(ivory, new(.032f, height + .22f, .032f), sign * (width * .5f + .025f), 0f, .06f);
            }
            Part(mat, new(width + .36f, .10f, .06f), 0f, height * .5f + .09f, 0f);
            Part(ivory, new(width + .28f, .08f, .17f), 0f, -height * .5f - .09f, 0f);
        }
        // Crown board (карниз) and its fretwork: on an authored casing this is the
        // only layer added, and it stands on the casing's own front face.
        var crownBase = face.Casing ? 0f : .06f;
        Part(ivory, new(width + .46f, .05f, .105f), 0f, height * .5f + .18f, crownBase);
        var count = style == 3 ? 3 : 5;
        for (var k = 0; k < count; k++)
        {
            var x = (k - (count - 1) * .5f) * .145f;
            Part(ivory, new(.065f, .065f, .026f), x, height * .5f + .27f, crownBase + .105f, Mathf.Pi / 4);
            if (style == 0 || style == 4)
                foreach (var sign in new[] { -1f, 1f })
                    Part(mat, new(.025f, .105f, .025f), x + sign * .034f, height * .5f + .33f,
                        crownBase + .105f, sign * .40f);
        }
    }

    // All residential panes, including side/rear windows; never one material per house.
    public static void LightOccupiedWindows(Node3D dwelling, string stableId)
    {
        var panes=dwelling.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>()
            .Where(m=>m.Mesh is not null && m.Name.ToString().Contains("Window",StringComparison.Ordinal)
                && m.Name.ToString().Contains("_Glass",StringComparison.Ordinal))
            .OrderBy(m=>m.Name.ToString(),StringComparer.Ordinal).ToArray();
        var warmGlass=VillageWindowMaterials.For(stableId);
        for(var i=0;i<panes.Length;i++)
            {
                panes[i].MaterialOverride=warmGlass;
                panes[i].SetMeta("occupiedWindow",true);
                panes[i].SetMeta("lightingRole","occupied residential window; surface glow without added lights");
            }
    }

}
