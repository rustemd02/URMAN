using Godot;

namespace Urman.Godot;

/// <summary>Reusable metre-scale meshes. Shape/UVs are authored here; bitmap
/// colour never supplies folds, bevels, tube joints or the hollow inside of a cup.</summary>
public static class RuralPropGeometry
{
    private static readonly Dictionary<string, ArrayMesh> Cache = new();
    private static readonly Dictionary<Vector3, BoxMesh> BoxCache = new();
    private static readonly Dictionary<ulong, Shape3D> ContactCache = new();

    /// <summary>Shared default box with its original dimensions and UVs.
    /// Treat the returned resource as immutable; duplicate before editing it.</summary>
    public static BoxMesh Box(Vector3 size)
    {
        if (!BoxCache.TryGetValue(size, out var mesh))
            BoxCache[size] = mesh = new BoxMesh { Size = size };
        return mesh;
    }

    /// <summary>Contacts follow the actual pieces and preserve the air between
    /// table legs. Shared meshes also share their static convex contact shapes.</summary>
    public static void AttachMemberContacts(Node3D model, StaticBody3D body)
    {
        foreach (var part in model.GetChildren().OfType<MeshInstance3D>())
        {
            if (part.Mesh is null || part.Name.ToString().Contains("Bolt", StringComparison.Ordinal)) continue;
            var key = part.Mesh.GetRid().Id;
            if (!ContactCache.TryGetValue(key, out var shape))
                ContactCache[key] = shape = part.Mesh.CreateConvexShape();
            body.AddChild(new CollisionShape3D { Name = model.Name + "_" + part.Name + "Contact", Shape = shape,
                Transform = body.GlobalTransform.AffineInverse() * part.GlobalTransform });
        }
        model.SetMeta("contactPolicy", "actual furniture members; shared convex shapes; no filled leg-space box");
    }

    // Families whose look depends on a 0..1 face UV or an authored layout keep BoxMesh UVs.
    private static readonly HashSet<string> UvBoundSurfaces = new(StringComparer.Ordinal)
    {
        "frost_window", "wood_cut", "hay_fibers", "cloth_table", "cloth_curtain", "wood_fence_uv", "wood_log_uv", "glass"
    };

    /// <summary>
    /// P2 / VIS-086/098: interior boxes get a small 45° chamfer so every edge catches a
    /// highlight instead of reading as a razor-cut primitive. Thin members (< 3 cm),
    /// UV-bound families and glass keep the plain BoxMesh. One chamfer ring: 44 triangles.
    /// </summary>
    public static Mesh InteriorBox(Vector3 size, string surface = "")
    {
        var thinnest = Mathf.Min(size.X, Mathf.Min(size.Y, size.Z));
        if (thinnest < .03f || UvBoundSurfaces.Contains(surface)) return Box(size);
        return ChamferBox(size, Mathf.Clamp(thinnest * .12f, .003f, .015f));
    }

    public static ArrayMesh ChamferBox(Vector3 size, float chamfer)
    {
        var key = $"chamfer:{size}:{chamfer}";
        if (Cache.TryGetValue(key, out var saved)) return saved;
        var h = size * .5f;
        chamfer = Mathf.Min(chamfer, Mathf.Min(h.X, Mathf.Min(h.Y, h.Z)) * .9f);
        // Three points per corner, one on each face that meets there.
        Vector3 OnX(int x, int y, int z) => new(x * h.X, y * (h.Y - chamfer), z * (h.Z - chamfer));
        Vector3 OnY(int x, int y, int z) => new(x * (h.X - chamfer), y * h.Y, z * (h.Z - chamfer));
        Vector3 OnZ(int x, int y, int z) => new(x * (h.X - chamfer), y * (h.Y - chamfer), z * h.Z);
        using var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            // Godot front faces are clockwise seen from outside.
            if ((b - a).Cross(c - a).Dot(outward) > 0f) (b, c) = (c, b);
            foreach (var point in new[] { a, b, c })
            {
                tool.SetNormal(outward);
                var axis = outward.Abs();
                tool.SetUV(axis.X >= axis.Y && axis.X >= axis.Z ? new Vector2(point.Z, point.Y)
                    : axis.Y >= axis.Z ? new Vector2(point.X, point.Z) : new Vector2(point.X, point.Y));
                tool.AddVertex(point);
            }
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            Triangle(a, b, c, outward);
            Triangle(a, c, d, outward);
        }
        foreach (var sign in new[] { -1, 1 })
        {
            Quad(OnX(sign, -1, -1), OnX(sign, 1, -1), OnX(sign, 1, 1), OnX(sign, -1, 1), new Vector3(sign, 0, 0));
            Quad(OnY(-1, sign, -1), OnY(1, sign, -1), OnY(1, sign, 1), OnY(-1, sign, 1), new Vector3(0, sign, 0));
            Quad(OnZ(-1, -1, sign), OnZ(1, -1, sign), OnZ(1, 1, sign), OnZ(-1, 1, sign), new Vector3(0, 0, sign));
        }
        foreach (var a in new[] { -1, 1 })
        foreach (var b in new[] { -1, 1 })
        {
            // Edge strips along Z (between X and Y faces), along Y (X/Z) and along X (Y/Z).
            Quad(OnX(a, b, -1), OnX(a, b, 1), OnY(a, b, 1), OnY(a, b, -1), new Vector3(a, b, 0).Normalized());
            Quad(OnX(a, -1, b), OnX(a, 1, b), OnZ(a, 1, b), OnZ(a, -1, b), new Vector3(a, 0, b).Normalized());
            Quad(OnY(-1, a, b), OnY(1, a, b), OnZ(1, a, b), OnZ(-1, a, b), new Vector3(0, a, b).Normalized());
        }
        foreach (var x in new[] { -1, 1 })
        foreach (var y in new[] { -1, 1 })
        foreach (var z in new[] { -1, 1 })
            Triangle(OnX(x, y, z), OnY(x, y, z), OnZ(x, y, z), new Vector3(x, y, z).Normalized());
        var mesh = tool.Commit();
        mesh.ResourceName = "Rural_ChamferBox";
        Cache[key] = mesh;
        return mesh;
    }

    public static ArrayMesh BevelBox(Vector3 size, float radius = .008f)
    {
        radius = Mathf.Min(radius, Mathf.Min(size.X, Mathf.Min(size.Y,size.Z))*.48f);
        var key = $"box:{size}:{radius}";
        if (Cache.TryGetValue(key,out var saved)) return saved;
        using var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        var h=size*.5f; var c=h-Vector3.One*radius;
        float[] Samples(float half) => [-half,-half+radius*.22f,-half+radius*.58f,-half+radius,half-radius,half-radius*.58f,half-radius*.22f,half];
        foreach (var (normal,u,v) in new[] {
            (Vector3.Right,Vector3.Back,Vector3.Up), (Vector3.Left,Vector3.Forward,Vector3.Up),
            (Vector3.Up,Vector3.Right,Vector3.Back), (Vector3.Down,Vector3.Right,Vector3.Forward),
            (Vector3.Back,Vector3.Left,Vector3.Up), (Vector3.Forward,Vector3.Right,Vector3.Up) })
        {
            var us=Samples(h.Dot(u.Abs())); var vs=Samples(h.Dot(v.Abs()));
            void Add(int i,int j)
            {
                var p=normal*h.Dot(normal.Abs())+u*us[i]+v*vs[j];
                var core=new Vector3(Mathf.Clamp(p.X,-c.X,c.X),Mathf.Clamp(p.Y,-c.Y,c.Y),Mathf.Clamp(p.Z,-c.Z,c.Z));
                var n=(p-core).Normalized();
                s.SetNormal(n); s.SetUV(new Vector2(us[i],vs[j])); s.AddVertex(core+n*radius);
            }
            for(var j=0;j<vs.Length-1;j++) for(var i=0;i<us.Length-1;i++)
            { Add(i,j);Add(i+1,j);Add(i+1,j+1);Add(i,j);Add(i+1,j+1);Add(i,j+1); }
        }
        var mesh=s.Commit();mesh.ResourceName="Rural_BevelBox_MetricUV";Cache[key]=mesh;return mesh;
    }

    /// <summary>Rounded plywood/padded panel in XY, thickness along Z. Bow is
    /// real crosswise curvature; both faces and the narrow edge have metric UV.</summary>
    public static ArrayMesh BowedPanel(float width,float height,float thickness,float corner,float bow)
    {
        var key=$"panel:{width}:{height}:{thickness}:{corner}:{bow}";
        if(Cache.TryGetValue(key,out var saved))return saved;
        using var s=new SurfaceTool();s.Begin(Mesh.PrimitiveType.Triangles);
        const int nx=24,ny=16;
        Vector3 P(int i,int j,float side)
        {
            var y=(j/(float)ny-.5f)*height;
            var end=Mathf.Max(0,Mathf.Abs(y)-(height*.5f-corner));
            var limit=width*.5f-corner+Mathf.Sqrt(Mathf.Max(0,corner*corner-end*end));
            var x=(i/(float)nx*2f-1f)*limit;
            return new(x,y,bow*Mathf.Pow(x/(width*.5f),2)+side*thickness*.5f);
        }
        void V(Vector3 p,Vector3 n,Vector2 uv){s.SetNormal(n);s.SetUV(uv);s.AddVertex(p);}
        void Face(int i,int j,float side)
        {
            var p=P(i,j,side);var n=new Vector3(-2f*bow*p.X/(width*width*.25f),0,1).Normalized()*side;
            V(p,n,new(p.X,p.Y));
        }
        foreach(var side in new[]{-1f,1f})for(var j=0;j<ny;j++)for(var i=0;i<nx;i++)
        {
            if(side>0){Face(i,j,side);Face(i,j+1,side);Face(i+1,j+1,side);Face(i,j,side);Face(i+1,j+1,side);Face(i+1,j,side);}
            else{Face(i,j,side);Face(i+1,j,side);Face(i+1,j+1,side);Face(i,j,side);Face(i+1,j+1,side);Face(i,j+1,side);}
        }
        var border=new List<(int,int)>();
        for(var i=0;i<=nx;i++)border.Add((i,0));
        for(var j=1;j<=ny;j++)border.Add((nx,j));
        for(var i=nx-1;i>=0;i--)border.Add((i,ny));
        for(var j=ny-1;j>0;j--)border.Add((0,j));
        float walked=0;
        for(var k=0;k<border.Count;k++)
        {
            var (i,j)=border[k];var (ii,jj)=border[(k+1)%border.Count];
            var a=P(i,j,-1);var b=P(ii,jj,-1);var c=P(ii,jj,1);var d=P(i,j,1);
            var n=(b-a).Cross(Vector3.Back).Normalized();var next=walked+a.DistanceTo(b);
            V(a,n,new(walked,0));V(d,n,new(walked,thickness));V(c,n,new(next,thickness));
            V(a,n,new(walked,0));V(c,n,new(next,thickness));V(b,n,new(next,0));walked=next;
        }
        var mesh=s.Commit();mesh.ResourceName="Rural_BowedPanel_MetricUV";Cache[key]=mesh;return mesh;
    }

    public static MeshInstance3D Part(Node3D parent,string name,Mesh mesh,Vector3 at,Material material,Vector3? rotation=null)
    {
        var node=new MeshInstance3D{Name=name,Mesh=mesh,Position=at,MaterialOverride=material,RotationDegrees=rotation??Vector3.Zero};
        node.SetMeta("surfaceUVContract","metres; texture repeat set by material");parent.AddChild(node);return node;
    }
    public static MeshInstance3D Block(Node3D parent,string name,Vector3 size,Vector3 at,Material material,float bevel=.008f,Vector3? rotation=null)
        => Part(parent,name,BevelBox(size,bevel),at,material,rotation);

    public static MeshInstance3D Tube(Node3D parent,string name,Vector3 a,Vector3 b,float radius,Material material,int sides=20)
    {
        var length=a.DistanceTo(b);var key=$"tube:{length}:{radius}:{sides}";
        // Built-in cylinder has continuous cylindrical UV; adjust it to physical lengths.
        if(!Cache.TryGetValue(key,out var mesh))
        {
            var source=new CylinderMesh{TopRadius=radius,BottomRadius=radius,Height=length,RadialSegments=sides,Rings=1};
            var arrays=source.GetMeshArrays();var uv=arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            for(var i=0;i<uv.Length;i++)uv[i]*=new Vector2(Mathf.Tau*radius,length);
            arrays[(int)Mesh.ArrayType.TexUV]=uv;mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);Cache[key]=mesh;
        }
        var node=Part(parent,name,mesh,(a+b)*.5f,material);
        var direction=(b-a).Normalized();var x=Mathf.Abs(direction.Dot(Vector3.Right))<.95f?Vector3.Right:Vector3.Back;
        var z=x.Cross(direction).Normalized();x=direction.Cross(z).Normalized();node.Basis=new Basis(x,direction,z);return node;
    }

    /// <summary>Open/closed lathed profile as radius,height, with circumference
    /// and profile arc length UVs. Profiles can fold inward for a real hollow rim.</summary>
    public static ArrayMesh Lathe(string name,Vector2[] profile,int segments=48)
    {
        var key=$"lathe:{name}:{segments}";if(Cache.TryGetValue(key,out var saved))return saved;
        using var s=new SurfaceTool();s.Begin(Mesh.PrimitiveType.Triangles);
        var lengths=new float[profile.Length];for(var j=1;j<profile.Length;j++)lengths[j]=lengths[j-1]+profile[j].DistanceTo(profile[j-1]);
        void V(int i,int j)
        {
            var angle=i/(float)segments*Mathf.Tau;
            var prev=profile[Mathf.Max(0,j-1)];var next=profile[Mathf.Min(profile.Length-1,j+1)];var tangent=next-prev;
            var n=new Vector3(Mathf.Cos(angle)*tangent.Y,-tangent.X,Mathf.Sin(angle)*tangent.Y).Normalized();
            s.SetNormal(n);s.SetUV(new(i/(float)segments*Mathf.Tau*profile[j].X,lengths[j]));
            s.AddVertex(new(Mathf.Cos(angle)*profile[j].X,profile[j].Y,Mathf.Sin(angle)*profile[j].X));
        }
        for(var j=0;j<profile.Length-1;j++)for(var i=0;i<segments;i++)
        { V(i,j);V(i+1,j+1);V(i,j+1);V(i,j);V(i+1,j);V(i+1,j+1); }
        var mesh=s.Commit();mesh.ResourceName="Rural_"+name+"_MetricUV";Cache[key]=mesh;return mesh;
    }

    /// <summary>VIS-014: the carrying points a member model actually stands on,
    /// measured from the bottom of each support member instead of from the model
    /// root. A four legged sawhorse reports four world points; hidden members and
    /// rotated plates report none, because neither carries anything.</summary>
    public static List<Vector3> SupportFeet(Node3D model, params string[] nameContains)
    {
        var feet = new List<Vector3>();
        foreach (var part in SupportMembers(model, nameContains))
        {
            var box = part.GlobalTransform * part.GetAabb();
            var centre = box.GetCenter();
            feet.Add(new Vector3(centre.X, box.Position.Y, centre.Z));
        }
        return feet;
    }

    /// <summary>VIS-014: the visible rectangular support members of a model
    /// (legs, posts, stools). Straight members only: a leaning or rotated piece
    /// is not a foot, and seating it by its axis-aligned box would be a guess.</summary>
    public static List<MeshInstance3D> SupportMembers(Node3D model, params string[] nameContains)
    {
        var found = new List<MeshInstance3D>();
        foreach (var part in FindMembers(model))
        {
            var name = part.Name.ToString();
            var isSupport = false;
            foreach (var token in nameContains)
                if (name.Contains(token, StringComparison.Ordinal)) { isSupport = true; break; }
            if (!isSupport) continue;
            if (Mathf.Abs(part.RotationDegrees.X) > .01f || Mathf.Abs(part.RotationDegrees.Z) > .01f) continue;
            found.Add(part);
        }
        return found;
    }

    private static IEnumerable<MeshInstance3D> FindMembers(Node3D model)
    {
        foreach (var child in model.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.Mesh is not null && mesh.IsVisibleInTree()) yield return mesh;
            if (child is Node3D branch)
                foreach (var nested in FindMembers(branch)) yield return nested;
        }
    }

    /// <summary>VIS-014: seats one rectangular support member so its foot rests on
    /// the surface that actually carries it (a plank deck, a threshold, a shelf),
    /// keeping its top exactly where its load expects it. A member that pierced the
    /// deck is trimmed — the buried part is duplicate support, the deck itself
    /// carries the load — and a member that floated is let down to it. The matching
    /// box contact follows the corrected member. Returns the signed deviation that
    /// was corrected (positive = closed air gap in metres, negative = buried depth
    /// removed); zero when the member already sat within the tolerance, and zero
    /// without any change when the piece is not a centred rectangular member.</summary>
    public static float SeatSupportFoot(MeshInstance3D part, float supportY, float clearance, float tolerance,
        HashSet<ulong>? editedShapes = null)
    {
        if (part.Mesh is null) return 0f;
        var before = part.GlobalTransform * part.GetAabb();
        var deviation = before.Position.Y - supportY;
        if (Mathf.Abs(deviation) <= tolerance) return 0f;
        // The origin has to be the member's own centre: that is what makes the
        // scale-and-reseat arithmetic below exact instead of approximate.
        var worldCentre = part.ToGlobal(part.GetAabb().GetCenter());
        if (Mathf.Abs(worldCentre.Y - part.GlobalPosition.Y) > .004f) return 0f;
        var oldHeight = before.Size.Y;
        var foot = supportY + clearance;
        var height = before.End.Y - foot;
        if (height < .03f || oldHeight < .03f) return 0f;
        var scale = height / oldHeight;
        part.Scale = new Vector3(part.Scale.X, part.Scale.Y * scale, part.Scale.Z);
        part.GlobalPosition = new Vector3(part.GlobalPosition.X, foot + height * .5f, part.GlobalPosition.Z);
        foreach (var body in part.GetChildren().OfType<CollisionObject3D>())
        foreach (var shape in body.GetChildren().OfType<CollisionShape3D>())
        {
            if (shape.Shape is not BoxShape3D box) continue;
            var key = (ulong)shape.Shape.GetRid().Id;
            // A shared convex resource is reused across instances (AttachMemberContacts);
            // editing it once per pass keeps every other user of that shape intact.
            if (editedShapes is not null && !editedShapes.Add(key)) continue;
            box.Size = new Vector3(box.Size.X, box.Size.Y * scale, box.Size.Z);
        }
        part.SetMeta("supportSeatPolicy", "foot on the surface that carries it, top unchanged, contact follows the member");
        part.SetMeta("supportSeatDeviationM", deviation);
        return deviation;
    }

    public static Mesh DrapedCloth(float width,float depth,float drop)
    {
        var key=$"cloth:{width}:{depth}:{drop}";if(Cache.TryGetValue(key,out var saved))return saved;
        using var s=new SurfaceTool();s.Begin(Mesh.PrimitiveType.Triangles);
        const int nx=64,nz=48;
        Vector3 P(int i,int j)
        {
            var x=(i/(float)nx-.5f)*(width+2*drop);var z=(j/(float)nz-.5f)*(depth+2*drop);
            var dx=Mathf.Max(0,Mathf.Abs(x)-width*.5f);var dz=Mathf.Max(0,Mathf.Abs(z)-depth*.5f);
            var hang=Mathf.Sqrt(dx*dx+dz*dz);var wrinkle=Mathf.Sin(x*32+z*3)*Mathf.Sin(z*29-x*2);
            return new(Mathf.Clamp(x,-width*.5f,width*.5f)+Mathf.Sign(x)*dx*.10f+wrinkle*dx*.06f,
                -.97f*hang+wrinkle*(.0008f+hang*.027f),
                Mathf.Clamp(z,-depth*.5f,depth*.5f)+Mathf.Sign(z)*dz*.10f+wrinkle*dz*.06f);
        }
        void V(int i,int j){s.SetUV(new(i/(float)nx*(width+2*drop),j/(float)nz*(depth+2*drop)));s.AddVertex(P(i,j));}
        for(var j=0;j<nz;j++)for(var i=0;i<nx;i++)
        {V(i,j);V(i,j+1);V(i+1,j+1);V(i,j);V(i+1,j+1);V(i+1,j);}
        s.Index();s.GenerateNormals();var mesh=s.Commit();mesh.ResourceName="Rural_DrapedCloth_MetricUV";Cache[key]=mesh;return mesh;
    }
}
