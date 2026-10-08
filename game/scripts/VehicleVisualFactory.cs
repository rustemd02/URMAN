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
        Node3D? SpeedNeedle=null, Node3D? EngineNeedle=null, Label3D? RadioDisplay=null, VehicleHorsePose? HorsePose=null,
        // The Niva is built with Charm:null on purpose: VehicleMirrorCharm
        // (VehicleVisualFactory.MirrorCharm.cs) drives the authored charm
        // itself, so VehicleController.SwingCharm must keep skipping it.
        Node3D? Charm=null);

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

    /// <summary>Rest tilt of the Niva steering wheel onto its column (face up toward the driver).</summary>
    public const float NivaSteeringTiltDegrees = -28f;

    private static Visual Niva(Node3D root)
    {
        // Body, cabin, wheel, charm and the car radio are the Blender-authored
        // model (tools/blender/generate_niva.py); gauges, the radio display,
        // plates and the moving pivots stay live runtime nodes.
        NivaModelPart(root,"NivaBody","NivaBody");
        NivaModelPart(root,"NivaInterior","NivaInterior");
        NivaModelPart(root,"NivaRadio","NivaRadio");
        root.SetMeta("roadVehicleGeometryRevision",4);
        root.SetMeta("assetOrigin","project-original Blender model urman_niva.glb");
        var speed=Gauge(root,"Speedometer",new(-.52f,1.102f,-.397f),.061f,"км/ч",160);
        var revs=Gauge(root,"Tachometer",new(-.36f,1.102f,-.397f),.061f,"×1000",8);
        var radioDisplay=CabinLabel(root,"RadioTuningDisplay","101.4",new(.10f,1.008f,-.362f),.00025f,new(.76f,.83f,.43f));
        var steering = new Node3D { Name="SteeringWheel", Position=new(-.40f,1.10f,-.16f), RotationDegrees=new(NivaSteeringTiltDegrees,0,0) };
        root.AddChild(steering);
        NivaModelPart(steering,"NivaSteering","SteeringRim");
        // Shamail and tasbih hang under the rear-view mirror. VehicleMirrorCharm
        // (VehicleVisualFactory.MirrorCharm.cs) is a self-driven, measured damped
        // pendulum; it is deliberately NOT handed back as Visual.Charm, which
        // disables the legacy VehicleController.SwingCharm clamp-and-ring from
        // this side without editing the physics lane's controller file. Do not
        // re-attach it there; the pendulum owns its own state and limits.
        var charm = new VehicleMirrorCharm { Name="MirrorCharm", Position=new(.07f,1.548f,-.35f), Scale=Vector3.One*.82f };
        root.AddChild(charm);
        NivaModelPart(charm,"NivaCharm","MirrorCharmMesh");
        NivaPlate(root,"FrontPlate",new(0,.63f,-2.0800f),180f);
        NivaPlate(root,"RearPlate",new(0,.80f,1.877f),0f);
        // Cabin presentation (windscreen snow, live wipers, high-beam dash tell)
        // and the cabin audio mix are self-driven presentation nodes: they read
        // the controller's public state, own no physics or save data and are
        // restored/removed exactly on exit (VehicleVisualFactory.Cabin.cs,
        // VehicleCabinAudio.cs).
        root.AddChild(new VehicleNivaCabin { Name="CabinPresentation" });
        root.AddChild(new VehicleCabinAudio { Name="CabinAudio" });
        var wheels = new List<Node3D>(); var front=new List<Node3D>(); var rear=new List<Node3D>();
        foreach(var x in new[]{-.78f,.78f}) foreach(var z in new[]{-1.18f,1.10f})
        {var wheel=NivaWheel(root,new(x,.345f,z)); wheels.Add(wheel);if(z<0)front.Add(wheel);else rear.Add(wheel);}
        // Two continuous pressed-snow ruts behind the rear wheels. The decal
        // skips the packed carriageway, ice and water, interiors and bridge
        // decks (VehicleSnowTracks.cs owns the budget and exclusion contract).
        root.AddChild(new VehicleSnowTracks { Name="VehicleSnowTracks", TrackedWheels=rear.ToArray(), WheelRadius=.345f });
        // Five small immersion details (VehicleImmersionDetails.cs): session-only
        // presentation that reads the controller's public state and self-ticks.
        root.AddChild(new VehicleImmersionDetails { Name="ImmersionDetails" });
        var lamps=Headlights(root,new[]{new Vector3(-.60f,1.01f,-2.04f),new Vector3(.60f,1.01f,-2.04f)});
        return new(root,wheels,front,Array.Empty<Node3D>(),null,steering,lamps,speed,revs,radioDisplay,Charm:null);
    }

    /// <summary>Russian plate of a Tatarstan car: series, number and region 116.</summary>
    private static void NivaPlate(Node3D root,string name,Vector3 position,float yaw)
    {
        var plate=new Node3D{Name=name,Position=position,RotationDegrees=new(0,yaw,0)};root.AddChild(plate);
        CabinLabel(plate,"Number","Е 214 КМ",new(-.045f,-.004f,.006f),.0021f,new(.08f,.08f,.08f));
        CabinLabel(plate,"Region","116",new(.205f,.012f,.006f),.0012f,new(.08f,.08f,.08f));
        CabinLabel(plate,"Country","RUS",new(.205f,-.03f,.006f),.00055f,new(.08f,.08f,.08f));
    }

    // Same pivot contract as RoadWheel (metadata, axle on X); the mesh is the
    // authored stamped steel wheel with its hubcap and tyre.
    private static Node3D NivaWheel(Node3D root,Vector3 position)
    {
        var pivot=new Node3D{Name="Wheel"+root.GetChildCount(),Position=position};root.AddChild(pivot);
        pivot.SetMeta("roadTyreRadius",.345f);pivot.SetMeta("roadTyreWidth",.19f);
        NivaModelPart(pivot,"NivaWheel","RoadWheelMesh");
        return pivot;
    }

    private static Visual Motorcycle(Node3D root) => BuildMotorcycleModel(root);

    private static Visual Cart(Node3D root)
    {
        var cart=new Batch(root,"CartAndHarness");
        // VIS-049: the shaft tips are the same authored points the harness tug
        // rings are built around, recorded on the root so the runtime can prove
        // the animal is actually hitched to the cart.
        root.SetMeta("shaftTipLeft",ShaftTip(-1f));
        root.SetMeta("shaftTipRight",ShaftTip(1f));
        // Floor boards, rails, metal brackets and shafts share one stable body.
        for(var i=0;i<7;i++)cart.Box(new(.155f,.065f,2.1f),new(-.51f+i*.17f,.69f,1.18f),"6f6148","wood");
        foreach(var x in new[]{-.60f,.60f})
        {
            for(var i=0;i<3;i++)cart.Box(new(.045f,.115f,2.12f),new(x,.89f+i*.15f,1.18f),"76674c","wood");
            foreach(var z in new[]{.18f,1.18f,2.18f})cart.Box(new(.075f,.68f,.075f),new(x,.93f,z),"514a39","wood");
            cart.Beam(new(x,.59f,1.28f),ShaftTip(Mathf.Sign(x)),.060f,"786447","wood");
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
            // VIS-105: the six transport finishes resolve through one contract
            // (VehicleVisualFactory.Materials.cs), so a procedural cart, the
            // motorcycle and the Blender Niva answer light identically. Animal
            // coat, hide, vinyl, hoof and instrument dial keep their own
            // restrained finish; wood and cloth stay village families.
            Material material=surface is "horse_coat" or "vinyl" or "leather" or "hoof" or "dial"
                ?TrimMaterial(color,surface)
                : surface switch
                {
                    "paint" or "metal" or "chrome" or "rubber" or "plastic" or "glass"
                        => ForFinish(color,surface),
                    _ => PainterlyMaterialLibrary.ForColor(color,surface,sheltered:true)
                };
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
            using var arrays=source.SurfaceGetArrays(0);
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
                using var arrays=mesh.SurfaceGetArrays(surface);
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
