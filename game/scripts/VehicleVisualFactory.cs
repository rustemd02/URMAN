using Godot;

namespace Urman.Godot;

/// <summary>
/// Original metre-scale stylized meshes. Chassis panels, glazing, wheel arches,
/// passenger space, controls, drivetrain and harness are modelled as geometry.
/// No internet image or historical photograph is represented as a licensed model.
/// </summary>
public static partial class VehicleVisualFactory
{
    public sealed record Visual(Node3D Root, IReadOnlyList<Node3D> Wheels, IReadOnlyList<Node3D> FrontWheels,
        IReadOnlyList<Node3D> HorseLegs, Node3D? HorseHead, Node3D? SteeringWheel, IReadOnlyList<SpotLight3D> Lamps,
        Node3D? SpeedNeedle=null, Node3D? EngineNeedle=null, Label3D? RadioDisplay=null, VehicleHorsePose? HorsePose=null);

    public static Visual Build(VehicleDefinition definition)
    {
        var root = new Node3D { Name = "VehicleVisual" };
        root.SetMeta("assetOrigin", "original procedural geometry authored for URMAN");
        root.SetMeta("artReview", "candidate; requires actual exterior/interior frames");
        return definition.Kind switch
        {
            VehicleKind.Niva => Niva(root),
            VehicleKind.Motorcycle => Motorcycle(root),
            _ => Cart(root)
        };
    }

    private static Visual Niva(Node3D root)
    {
        var mesh = new Batch(root, "Chassis");
        const string paint = "596c59";
        BuildNivaExterior(mesh,paint);
        root.SetMeta("roadVehicleGeometryRevision",2);
        // Practical cabin: two seats, rear bench, floor mat, dash, controls.
        foreach(var x in new[]{-.40f,.40f})
        {
            mesh.Sphere(new(.29f,.075f,.28f),new(x,.69f,.12f),"343630","vinyl");
            mesh.Sphere(new(.29f,.265f,.065f),new(x,.97f,.42f),"343630","vinyl",new(-8,0,0));
            mesh.Sphere(new(.16f,.085f,.055f),new(x,1.27f,.44f),"343630","vinyl");
            foreach(var seam in new[]{-.16f,-.08f,0f,.08f,.16f})
                mesh.Beam(new(x+seam,.748f,-.08f),new(x+seam,.748f,.30f),.0016f,"646356","vinyl");
            mesh.Box(new(.55f,.018f,.72f),new(x,.523f,-.47f),"252824","rubber");
        }
        mesh.Box(new(1.26f,.13f,.42f),new(0,.69f,1.13f),"343630","vinyl");
        mesh.Box(new(1.26f,.43f,.10f),new(0,.91f,1.41f),"343630","vinyl");
        mesh.Box(new(1.38f,.17f,.31f),new(0,1.0f,-.56f),"30342e","rubber");
        mesh.Box(new(.34f,.10f,.02f),new(.10f,.997f,-.392f),"171e19","metal");
        mesh.Box(new(.20f,.038f,.012f),new(.10f,1.008f,-.377f),"9b9851","metal");
        mesh.Box(new(.39f,.16f,.095f),new(-.43f,1.10f,-.455f),"242723","vinyl");
        mesh.Box(new(.40f,.018f,.11f),new(-.43f,1.188f,-.448f),"272b25","vinyl");
        foreach(var vent in new[]{-.08f,.52f})
        {
            mesh.Box(new(.14f,.052f,.018f),new(vent,1.04f,-.392f),"151b18","rubber");
            for(var i=0;i<4;i++)mesh.Box(new(.115f,.004f,.008f),new(vent,1.02f+i*.012f,-.379f),"565b51","metal");
        }
        mesh.Box(new(.34f,.003f,.012f),new(.46f,.943f,-.395f),"84877b","metal");
        mesh.Box(new(.055f,.018f,.023f),new(.46f,.923f,-.385f),"63695d","metal");
        foreach(var x in new[]{-.035f,.235f})mesh.Disc(.017f,.016f,new(x,.989f,-.373f),"596153",true);
        mesh.Box(new(.14f,.014f,.015f),new(.1f,.963f,-.374f),"797f6a","metal");
        mesh.Beam(new(-.40f,.88f,-.53f),new(-.40f,1.04f,-.25f),.047f,"383d34","metal");
        mesh.Beam(new(.015f,.53f,-.20f),new(.015f,.84f,-.13f),.028f,"77796d","metal");
        mesh.Sphere(new(.055f,.055f,.055f),new(.015f,.85f,-.13f),"242922","rubber");
        mesh.Beam(new(-.50f,1.158f,-.705f),new(-.05f,1.228f,-.646f),.006f,"262b26","metal");
        mesh.Beam(new(.18f,1.158f,-.705f),new(.63f,1.228f,-.646f),.006f,"262b26","metal");
        mesh.Beam(new(0,1.171f,1.815f),new(.43f,1.269f,1.758f),.006f,"262b26","metal");
        mesh.Finish();
        var speed=Gauge(root,"Speedometer",new(-.52f,1.102f,-.397f),.061f,"км/ч",160);
        var revs=Gauge(root,"Tachometer",new(-.36f,1.102f,-.397f),.061f,"×1000",8);
        var radioDisplay=CabinLabel(root,"RadioTuningDisplay","101.4",new(.10f,1.008f,-.367f),.00025f,new(.76f,.83f,.43f));
        var steering = new Node3D { Name="SteeringWheel", Position=new(-.40f,1.04f,-.25f), RotationDegrees=new(70,0,0) };
        root.AddChild(steering);
        var sw = new Batch(steering,"SteeringRim");
        sw.Ring(.165f,.021f,Vector3.Zero,"242a25","rubber");
        for(var i=0;i<3;i++)
        {
            var a=i*Mathf.Tau/3; sw.Beam(Vector3.Zero,new(Mathf.Cos(a)*.155f,Mathf.Sin(a)*.155f,0),.025f,"828779","metal");
        }
        sw.Finish();
        var wheels = new List<Node3D>(); var front=new List<Node3D>();
        foreach(var x in new[]{-.78f,.78f}) foreach(var z in new[]{-1.18f,1.10f})
        {var wheel=RoadWheel(root,new(x,.345f,z),.345f,.19f); wheels.Add(wheel);if(z<0)front.Add(wheel);}
        var lamps=Headlights(root,new[]{new Vector3(-.60f,1.01f,-2.04f),new Vector3(.60f,1.01f,-2.04f)});
        return new(root,wheels,front,Array.Empty<Node3D>(),null,steering,lamps,speed,revs,radioDisplay);
    }

    private static Visual Motorcycle(Node3D root) => BuildMotorcycleModel(root);

    private static Visual Cart(Node3D root)
    {
        var cart=new Batch(root,"CartAndHarness");
        // Floor boards, rails, metal brackets and shafts share one stable body.
        for(var i=0;i<7;i++)cart.Box(new(.155f,.065f,2.1f),new(-.51f+i*.17f,.69f,1.18f),"6f6148","wood");
        foreach(var x in new[]{-.60f,.60f})
        {
            for(var i=0;i<3;i++)cart.Box(new(.045f,.115f,2.12f),new(x,.89f+i*.15f,1.18f),"76674c","wood");
            foreach(var z in new[]{.18f,1.18f,2.18f})cart.Box(new(.075f,.68f,.075f),new(x,.93f,z),"514a39","wood");
            cart.Beam(new(x,.59f,1.28f),new(x*.76f,.85f,-2.05f),.060f,"786447","wood");
        }
        for(var i=0;i<3;i++)cart.Box(new(1.18f,.115f,.045f),new(0,.89f+i*.15f,2.22f),"716247","wood");
        cart.Box(new(1.15f,.10f,.30f),new(0,1.02f,.31f),"807056","wood");
        cart.Box(new(.69f,.065f,.28f),new(0,1.10f,.31f),"605445","cloth");
        foreach(var z in new[]{.36f,1.89f})cart.Beam(new(-.82f,.44f,z),new(.82f,.44f,z),.07f,"454d40","metal");
        cart.Beam(new(-.15f,1.24f,.34f),new(-.25f,1.72f,-1.05f),.009f,"3d3529","leather");
        cart.Beam(new(.15f,1.24f,.34f),new(.25f,1.72f,-1.05f),.009f,"3d3529","leather");
        cart.Finish();
        // Occupied-cart driver: a seated winter figure on the bench cushion,
        // inside the retained hull envelope. Presentation only: no collision,
        // no save state; the player capsule stays hidden while driving.
        // The empty bench (unoccupied cart) keeps its exact prior geometry.
        var driverRoot = new Node3D { Name = "CartDriver" };
        root.AddChild(driverRoot);
        var driver = new Batch(driverRoot, "CartDriverBody");
        driver.Sphere(new(.16f, .21f, .13f), new(0, 1.32f, .38f), "4a4238", "cloth");
        driver.Sphere(new(.13f, .10f, .11f), new(0, 1.16f, .40f), "3a332a", "cloth");
        driver.Sphere(new(.085f, .105f, .095f), new(0, 1.585f, .36f), "c9a186", "cloth");
        driver.Sphere(new(.088f, .06f, .098f), new(0, 1.66f, .365f), "2e2a26", "cloth");
        driver.Sphere(new(.089f, .035f, .099f), new(0, 1.615f, .355f), "2e2a26", "cloth");
        foreach (var x in new[] { -.13f, .13f })
        {
            driver.Beam(new(x, 1.28f, .40f), new(x * 1.5f, 1.02f, .72f), .055f, "3f382e", "cloth");
            driver.Beam(new(x * 1.15f, 1.02f, .60f), new(x * 1.15f, .78f, .62f), .06f, "33302a", "cloth");
        }
        driver.Finish();
        driverRoot.Visible = false;
        driverRoot.SetMeta("presentationOwnership", "presentation-only; no collision, no save state");
        var horse=BuildHorse(root);
        var wheels=new List<Node3D>();
        foreach(var x in new[]{-.75f,.75f})foreach(var z in new[]{.36f,1.89f})
            wheels.Add(Wheel(root,new(x,.43f,z),.43f,.075f,true,true));
        return new(root,wheels,Array.Empty<Node3D>(),horse.Legs,horse.Head,null,Array.Empty<SpotLight3D>(),HorsePose:horse.Pose);
    }

    private static void AddArch(List<Vector2> side,float center,float radius,float bottom)
    {
        for(var i=0;i<=8;i++){var a=Mathf.Pi-i*Mathf.Pi/8;side.Add(new(center+Mathf.Cos(a)*radius,bottom+Mathf.Sin(a)*radius));}
    }

    private static Label3D CabinLabel(Node3D parent,string name,string text,Vector3 position,float pixelSize,Color color)
    {
        var label=new Label3D{Name=name,Text=text,Position=position,FontSize=32,PixelSize=pixelSize,
            Modulate=color,OutlineSize=0,Shaded=false,DoubleSided=false,NoDepthTest=false};
        parent.AddChild(label);return label;
    }

    private static Node3D Gauge(Node3D parent,string name,Vector3 position,float radius,string unit,int maximum)
    {
        var dial=new Node3D{Name=name,Position=position};parent.AddChild(dial);
        dial.SetMeta("instrumentUnit",unit);dial.SetMeta("instrumentMaximum",maximum);
        var face=new Batch(dial,"GaugeFace");
        face.Cylinder(radius+.003f,.014f,Vector3.Zero,"676d62","metal",new(90,0,0));
        face.Cylinder(radius,.004f,new(0,0,.009f),"151b18","dial",new(90,0,0));
        for(var i=0;i<=32;i++)
        {
            var angle=Mathf.DegToRad(-130+i*260f/32);
            var direction=new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
            var major=i%4==0;
            face.Beam(direction*radius*(major ? .69f : .81f)+Vector3.Back*.013f,
                direction*radius*.91f+Vector3.Back*.013f,major ? .0011f : .00065f,"d4d9bc","dial");
            if(major)CabinLabel(dial,"Scale"+i,(i*maximum/32).ToString(),direction*radius*.51f+Vector3.Back*.016f,
                radius*.003f,new(.84f,.87f,.76f));
        }
        face.Finish();
        CabinLabel(dial,"Unit",unit,new(0,-radius*.35f,.017f),radius*.0024f,new(.78f,.82f,.70f));
        var pointer=new Node3D{Name="Needle",Position=new(0,0,.021f),RotationDegrees=new(0,0,130)};dial.AddChild(pointer);
        var needle=new Batch(pointer,"NeedleMesh");
        needle.Beam(new(0,-radius*.15f,0),new(0,radius*.77f,0),.0016f,"c89363","dial");
        needle.Cylinder(.005f,.003f,Vector3.Zero,"72796a","metal",new(90,0,0));needle.Finish();
        return pointer;
    }

    private static Material TrimMaterial(string color,string surface)
    {
        // Skin, vinyl and harness have their own restrained finish. Reusing the
        // image-backed cloth material gave the animal broad board-like stripes.
        if(surface=="horse_coat")
        {
            var shader=new Shader{Code="""
                shader_type spatial;
                render_mode diffuse_burley;
                uniform vec4 coat_color : source_color;
                float grain(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
                void fragment() {
                    vec2 cells=UV*vec2(640.0,170.0);
                    float fade=1.0-smoothstep(0.25,1.2,max(fwidth(cells.x),fwidth(cells.y)));
                    float hair=(grain(floor(cells))-0.5)*0.035*fade;
                    ALBEDO=coat_color.rgb*(1.0+hair);
                    ROUGHNESS=0.87;SPECULAR=0.16;
                }
                """};
            var material=new ShaderMaterial{Shader=shader};material.SetShaderParameter("coat_color",new Color(color));
            material.SetMeta("vehicleSurface","horse_coat");return material;
        }
        return new StandardMaterial3D{AlbedoColor=new Color(color),Roughness=surface=="vinyl" ? .82f : .92f,
            Metallic=0,MetallicSpecular=surface=="dial" ? .1f : .2f};
    }

    private static Node3D Wheel(Node3D root,Vector3 position,float radius,float width,bool spokes=false,bool wood=false)
    {
        var pivot=new Node3D{Name="Wheel"+root.GetChildCount(),Position=position};root.AddChild(pivot);
        var b=new Batch(pivot,"WheelMesh");
        if(spokes)
        {
            b.Annulus(radius,radius*.81f,width,wood?"393a2f":"272e26",wood?"metal":"rubber");
            b.Annulus(radius*.82f,radius*.73f,width+.006f,wood?"8d7756":"7d8677",wood?"wood":"metal");
        }
        else
        {
            b.Cylinder(radius,width,Vector3.Zero,"272e26","rubber",new(0,0,90));
            b.Cylinder(radius*.66f,width+.006f,Vector3.Zero,"7d8677","metal",new(0,0,90));
        }
        if(spokes)
        {
            // Actual open wheel: the background remains visible between spokes.
            for(var i=0;i<12;i++){var a=Mathf.Tau*i/12;b.Beam(new(0,Mathf.Cos(a)*.07f,Mathf.Sin(a)*.07f),
                new(0,Mathf.Cos(a)*radius*.80f,Mathf.Sin(a)*radius*.80f),wood ? .028f : .010f,wood?"aa8f64":"b4bda7",wood?"wood":"metal");}
        }
        b.Cylinder(radius*.20f,width+.035f,Vector3.Zero,"8a907e","metal",new(0,0,90));
        for(var i=0;i<18&&!wood;i++){var a=Mathf.Tau*i/18;
            b.Box(new(width+.012f,.042f,.055f),new(0,Mathf.Cos(a)*(radius-.016f),Mathf.Sin(a)*(radius-.016f)),
                "333b2f","rubber",new(Mathf.RadToDeg(a),0,0));}
        b.Finish();return pivot;
    }

    private static IReadOnlyList<SpotLight3D> Headlights(Node3D root,IEnumerable<Vector3> positions)
    {
        var lamps=new List<SpotLight3D>();
        foreach(var position in positions)
        {
            var light=new SpotLight3D{Name="Headlight"+lamps.Count,Position=position,RotationDegrees=new(-4,0,0),
                LightColor=new(1f,.84f,.57f),LightEnergy=2.1f,SpotRange=26f,SpotAngle=29f,
                ShadowEnabled=true,Visible=false};
            root.AddChild(light);lamps.Add(light);
        }
        return lamps;
    }

    private sealed partial class Batch
    {
        private readonly Node3D _parent; private readonly string _name;
        private readonly Dictionary<string,(SurfaceTool Tool,Material Material)> _surfaces=new(StringComparer.Ordinal);
        private int _expectedCorners;
        private int _manualCorners;
        public Batch(Node3D parent,string name){_parent=parent;_name=name;}
        private SurfaceTool Get(string color,string surface)
        {
            var key=color+"|"+surface;
            if(_surfaces.TryGetValue(key,out var found))return found.Tool;
            Material material=surface=="glass"
                ? new StandardMaterial3D{AlbedoColor=new(.48f,.58f,.55f,.22f),Transparency=BaseMaterial3D.TransparencyEnum.Alpha,
                    Roughness=.18f,MetallicSpecular=.55f,CullMode=BaseMaterial3D.CullModeEnum.Disabled}
                : surface is "horse_coat" or "vinyl" or "leather" or "hoof" or "dial"
                    ?TrimMaterial(color,surface):PainterlyMaterialLibrary.ForColor(color,surface,sheltered:true);
            var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);st.SetMaterial(material);
            _surfaces.Add(key,(st,material));return st;
        }
        private void Add(Mesh mesh,Vector3 position,Vector3 rotation,string color,string surface,Vector3? scale=null)
        {
            var basis=Basis.FromEuler(rotation*Mathf.Pi/180f).Scaled(scale??Vector3.One);
            _expectedCorners+=AppendTriangles(Get(color,surface),mesh,new Transform3D(basis,position));
        }
        public void Box(Vector3 size,Vector3 pos,string color,string surface,Vector3 rotation=default)
            =>Add(new BoxMesh{Size=size},pos,rotation,color,surface);
        public void Sphere(Vector3 radii,Vector3 pos,string color,string surface,Vector3 rotation=default)
            =>Add(new SphereMesh{Radius=1,Height=2,RadialSegments=12,Rings=6},pos,rotation,color,surface,radii);
        public void Cylinder(float radius,float height,Vector3 pos,string color,string surface,Vector3 rotation=default)
            =>Add(new CylinderMesh{TopRadius=radius,BottomRadius=radius,Height=height,RadialSegments=18,Rings=1},pos,rotation,color,surface);
        public void Sleeve(float radius,float height,Vector3 pos,string color,string surface,Vector3 rotation=default)
            =>Add(new CylinderMesh{TopRadius=radius,BottomRadius=radius,Height=height,RadialSegments=24,Rings=1,
                CapTop=false,CapBottom=false},pos,rotation,color,surface);
        public void Mudguard(Vector3 centre,float radius,float thickness,float halfWidth,float fromDegrees,float toDegrees,string color)
        {
            var st=Get(color,"metal");
            Vector3 P(float x,float r,float angle)=>centre+new Vector3(x,Mathf.Cos(angle)*r,Mathf.Sin(angle)*r);
            for(var i=0;i<20;i++)
            {
                var a=Mathf.DegToRad(Mathf.Lerp(fromDegrees,toDegrees,i/20f));
                var b=Mathf.DegToRad(Mathf.Lerp(fromDegrees,toDegrees,(i+1)/20f));
                var lo=P(-halfWidth,radius,a);var ln=P(-halfWidth,radius,b);
                var ro=P(halfWidth,radius,a);var rn=P(halfWidth,radius,b);
                var li=P(-halfWidth,radius-thickness,a);var lj=P(-halfWidth,radius-thickness,b);
                var ri=P(halfWidth,radius-thickness,a);var rj=P(halfWidth,radius-thickness,b);
                var radial=new Vector3(0,Mathf.Cos((a+b)*.5f),Mathf.Sin((a+b)*.5f));
                Face(st,lo,ro,rn,ln,radial);Face(st,li,lj,rj,ri,-radial);
                Face(st,lo,ln,lj,li,Vector3.Left);Face(st,ro,ri,rj,rn,Vector3.Right);
                if(i==0)Face(st,lo,li,ri,ro,new(0,Mathf.Sin(a),-Mathf.Cos(a)));
                if(i==19)Face(st,ln,rn,rj,lj,new(0,-Mathf.Sin(b),Mathf.Cos(b)));
            }
        }
        public void Disc(float radius,float height,Vector3 pos,string color,bool forward)
            =>Cylinder(radius,height,pos,color,"metal",forward?new(90,0,0):Vector3.Zero);
        public void Ring(float radius,float tube,Vector3 center,string color,string surface)
        {
            for(var i=0;i<24;i++){var a=Mathf.Tau*i/24;var b=Mathf.Tau*(i+1)/24;
                Beam(center+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0),
                    center+new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,0),tube,color,surface);}
        }
        public void Headlamp(Vector3 center,float radius)
        {
            Ring(radius+.005f,.012f,center,"222a24","rubber");
            Ring(radius,.005f,center+Vector3.Forward*.003f,"aeb6a6","metal");
            // The reflector recedes into the front wing. Its concentric surface
            // and curved clear lens catch light independently of the dark rim.
            var st=Get("bfc5af","metal");
            Vector3 Point(float fraction,float angle)=>center+new Vector3(
                Mathf.Cos(angle)*radius*fraction,Mathf.Sin(angle)*radius*fraction,.044f*(1-fraction*fraction));
            for(var i=0;i<24;i++)
            {
                var a=Mathf.Tau*i/24;var b=Mathf.Tau*(i+1)/24;
                Triangle(st,Point(1f/3,a),Point(0,a),Point(1f/3,b),reverseWinding:true);
                for(var ring=1;ring<3;ring++)
                {
                    var inner=ring/3f;var outer=(ring+1)/3f;
                    Triangle(st,Point(outer,a),Point(inner,a),Point(inner,b),reverseWinding:true);
                    Triangle(st,Point(outer,a),Point(inner,b),Point(outer,b),reverseWinding:true);
                }
            }
            Sphere(new(radius*.94f,radius*.94f,.012f),center+Vector3.Forward*.002f,"d7e1cf","glass");
            for(var line=-3;line<=3;line++)
            {
                var x=radius*line*.22f;var y=Mathf.Sqrt(radius*radius*.85f-x*x);
                Beam(center+new Vector3(x,-y,-.012f),center+new Vector3(x,y,-.012f),.0012f,"b5bba8","metal");
            }
        }
        public void Annulus(float outer,float inner,float width,string color,string surface)
        {
            var st=Get(color,surface);
            for(var i=0;i<24;i++)
            {
                var a=Mathf.Tau*i/24;var b=Mathf.Tau*(i+1)/24;
                Vector3 P(float x,float r,float angle)=>new(x,Mathf.Cos(angle)*r,Mathf.Sin(angle)*r);
                var lo=P(-width*.5f,outer,a);var ln=P(-width*.5f,outer,b);
                var ro=P(width*.5f,outer,a);var rn=P(width*.5f,outer,b);
                var li=P(-width*.5f,inner,a);var lj=P(-width*.5f,inner,b);
                var ri=P(width*.5f,inner,a);var rj=P(width*.5f,inner,b);
                Triangle(st,lo,ro,rn);Triangle(st,lo,rn,ln);
                Triangle(st,li,lj,rj);Triangle(st,li,rj,ri);
                Triangle(st,lo,ln,lj);Triangle(st,lo,lj,li);
                Triangle(st,ro,ri,rj);Triangle(st,ro,rj,rn);
            }
        }
        public void Beam(Vector3 from,Vector3 to,float radius,string color,string surface)
        {
            var direction=to-from;var length=direction.Length();if(length<.0001f)return;
            var mesh=new CylinderMesh{TopRadius=radius,BottomRadius=radius,Height=length,RadialSegments=8,Rings=1};
            var basis=new Basis(new Quaternion(Vector3.Up,direction/length));
            _expectedCorners+=AppendTriangles(Get(color,surface),mesh,new Transform3D(basis,(from+to)*.5f));
        }
        private static int AppendTriangles(SurfaceTool target,Mesh source,Transform3D transform)
        {
            // Primitive meshes are indexed. Expand their triangles explicitly so
            // later authored fenders/rings cannot be omitted by a retained index
            // buffer. Every surface follows the same non-indexed vertex contract.
            var arrays=source.SurfaceGetArrays(0);
            var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals=arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var uvs=arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            var indices=arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            var normalBasis=transform.Basis.Inverse().Transposed();
            var count=indices.Length>0?indices.Length:vertices.Length;
            for(var corner=0;corner<count;corner++)
            {
                var index=indices.Length>0?indices[corner]:corner;
                var normal=(normalBasis*normals[index]).Normalized();
                var tangent=(transform.Basis*Vector3.Right).Slide(normal).Normalized();
                if(tangent.LengthSquared()<.01f)tangent=normal.Cross(Vector3.Forward).Normalized();
                target.SetNormal(normal);target.SetTangent(new Plane(tangent,1));
                target.SetUV(uvs.Length>index?uvs[index]:Vector2.Zero);
                target.AddVertex(transform*vertices[index]);
            }
            return count;
        }
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,string surface)
        {var st=Get("83988c",surface);Triangle(st,a,b,c);Triangle(st,a,c,d);}
        public void Profile(string name,IReadOnlyList<Vector2> polygon,float x,float depth,string color,string surface)
        {
            var p=polygon.ToArray();var triangles=Geometry2D.TriangulatePolygon(p);var st=Get(color,surface);
            foreach(var sign in new[]{-1,1})for(var i=0;i<triangles.Length;i+=3)
            {
                Vector3 Point(int index)=>new(x+sign*depth*.5f,p[index].Y,p[index].X);
                var a=Point(triangles[i]);var b=Point(triangles[i+1]);var c=Point(triangles[i+2]);
                if(sign<0)Triangle(st,a,c,b);else Triangle(st,a,b,c);
            }
            for(var i=0;i<p.Length;i++)
            {
                var a=p[i];var b=p[(i+1)%p.Length];
                var aa=new Vector3(x-depth*.5f,a.Y,a.X);var ab=new Vector3(x+depth*.5f,a.Y,a.X);
                var ba=new Vector3(x-depth*.5f,b.Y,b.X);var bb=new Vector3(x+depth*.5f,b.Y,b.X);
                Triangle(st,aa,ab,bb);Triangle(st,aa,bb,ba);
            }
        }
        private void Triangle(SurfaceTool st,Vector3 a,Vector3 b,Vector3 c,bool reverseWinding=false)
        {
            var normal=(b-a).Cross(c-a).Normalized();var tangent=Vector3.Right.Slide(normal).Normalized();
            if(tangent.LengthSquared()<.01f)tangent=normal.Cross(Vector3.Forward).Normalized();
            st.SetNormal(normal);st.SetTangent(new Plane(tangent,1));st.SetUV(Vector2.Zero);
            // The reflector supplies an outward mathematical cross normal;
            // Godot's clockwise front face needs the reversed vertex order.
            // Existing profile/annulus ordering is unchanged by this opt-in.
            st.AddVertex(a);st.AddVertex(reverseWinding?c:b);st.AddVertex(reverseWinding?b:c);
            _expectedCorners+=3;_manualCorners+=3;
        }
        public void Finish()
        {
            var mesh=new ArrayMesh();foreach(var (st,_) in _surfaces.Values){st.Index();st.Commit(mesh);st.Dispose();}
            var corners=0;
            for(var surface=0;surface<mesh.GetSurfaceCount();surface++)
            {
                var arrays=mesh.SurfaceGetArrays(surface);
                var indices=arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                corners+=indices.Length>0?indices.Length:arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
            }
            if(corners!=_expectedCorners||corners%3!=0)
                throw new InvalidDataException("Vehicle geometry lost triangle indices in "+_name);
            mesh.SetMeta("expectedTriangleCorners",_expectedCorners);mesh.SetMeta("manualTriangleCorners",_manualCorners);
            var instance=new MeshInstance3D{Name=_name,Mesh=mesh};
            _parent.AddChild(instance);
        }
    }
}
