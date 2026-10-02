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
                pieces.Add(root.GlobalTransform.AffineInverse()*global*new Transform3D(Basis.Identity.Scaled(size),Vector3.Zero));
            }
            var windows=source.Where(m=>m.Name.ToString().Contains("_Window",StringComparison.Ordinal)
                &&m.Name.ToString().Contains("_Glass",StringComparison.Ordinal)).ToArray();
            foreach(var frame in source.Where(m=>new[]{"_Jamb","_Rail","_Verge","_Mullion"}.Any(m.Name.ToString().Contains)))
                frame.MaterialOverride=PainterlyMaterialLibrary.ForColor(paint,"wood_painted_trim");
            void Surround(Transform3D at,float width,float height,int style)
            {
                var mat=paint+"|wood_painted_trim"; var ivory="dfdcc5|wood_painted_trim";
                void Part(string material,Vector3 size,Vector3 centre,float roll=0)
                    =>Box(material,size,at*new Transform3D(new Basis(Vector3.Back,roll),centre));
                foreach(var sign in new[]{-1f,1f})
                {
                    Part(mat,new(.105f,height+.28f,.06f),new(sign*(width*.5f+.075f),0,.12f));
                    Part(ivory,new(.032f,height+.22f,.032f),new(sign*(width*.5f+.025f),0,.159f));
                }
                Part(mat,new(width+.36f,.10f,.09f),new(0,height*.5f+.09f,.135f));
                Part(mat,new(width+.28f,.08f,.17f),new(0,-height*.5f-.09f,.17f));
                Part(ivory,new(width+.46f,.05f,.105f),new(0,height*.5f+.18f,.145f));
                // Built-up fretwork: individual diamond/tulip crowns with real gaps.
                var count=style==3?3:5;
                for(var k=0;k<count;k++)
                {
                    var x=(k-(count-1)*.5f)*.145f;
                    Part(ivory,new(.065f,.065f,.026f),new(x,height*.5f+.27f,.16f),Mathf.Pi/4);
                    if(style==0||style==4)
                        foreach(var sign in new[]{-1f,1f})
                            Part(mat,new(.025f,.105f,.025f),new(x+sign*.034f,height*.5f+.33f,.17f),sign*.40f);
                }
            }
            foreach(var pane in windows)
            {
                var box=pane.Mesh!.GetAabb();
                var at=new Transform3D(pane.GlobalBasis.Orthonormalized(),pane.GlobalTransform*box.GetCenter());
                Surround(at,box.Size.X*pane.GlobalBasis.Scale.X,box.Size.Y*pane.GlobalBasis.Scale.Y,variant);
            }
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
                    foreach(var roof in source.Where(m=>!m.Name.ToString().Contains("Seni",StringComparison.Ordinal)
                        &&new[]{"Roof","Gable","Verge","Eaves","Fascia","Gutter","Chimney","Ridge"}.Any(m.Name.ToString().Contains)))
                        roof.GlobalPosition+=Vector3.Up*added;
                    var w=bounds.Size.X;var d=bounds.Size.Z;var y=bounds.End.Y;
                    var centre=bounds.GetCenter();
                    Box(paint+"|wood_facade",new(w,added,d),root.GlobalTransform*new Transform3D(Basis.Identity,new(centre.X,y+added*.5f,centre.Z)));
                    Box("dfdcc5|wood_painted_trim",new(w+.05f,.12f,d+.05f),root.GlobalTransform*new Transform3D(Basis.Identity,new(centre.X,y+.08f,centre.Z)));
                    foreach(var x in new[]{-.28f,0f,.28f})
                    {
                        var at=root.GlobalTransform*new Transform3D(Basis.Identity,new(centre.X+x*w,y+1.15f,bounds.End.Z+.045f));
                        Box("536f74|glass",new(.86f,1.15f,.03f),at);
                        Surround(at,.86f,1.15f,variant);
                    }
                    root.SetMeta("addedStoreyMetres",added);
                }
            }
            foreach(var (material,pieces) in batches)
            {
                using var s=new SurfaceTool();s.Begin(Mesh.PrimitiveType.Triangles);
                foreach(var t in pieces)AppendMetricBox(s,t);
                s.Index();var bits=material.Split('|');
                root.AddChild(new MeshInstance3D {Name="CarvedTimber_"+bits[0]+"_"+bits[1],Mesh=s.Commit(),
                    MaterialOverride=PainterlyMaterialLibrary.ForColor(bits[0],bits[1])});
            }
            root.SetMeta("householdStyle",variant);root.SetMeta("twoStorey",twoStorey);
        }
    }
}
