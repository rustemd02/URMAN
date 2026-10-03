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
