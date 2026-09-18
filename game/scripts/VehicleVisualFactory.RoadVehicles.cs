using Godot;

namespace Urman.Godot;

public static partial class VehicleVisualFactory
{
    // Original metre-scale geometry, observed against the actual B29 frames.
    // The public CC previews are shape references only; no downloaded model
    // archive, rig or texture is represented here as an installed asset.
    private readonly record struct CoachSection(float Z, float HalfWidth, float Bottom, float Top, float Bevel);

    private static void BuildNivaExterior(Batch body, string paint)
    {
        var side = new List<Vector2> { new(-1.88f, .43f) };
        AddArch(side, -1.18f, .405f, .345f);
        side.Add(new(-.60f, .43f)); side.Add(new(.52f, .43f));
        AddArch(side, 1.10f, .405f, .345f);
        side.AddRange(new Vector2[] { new(1.84f,.43f),new(1.84f,1.09f),
            new(.85f,1.10f),new(-.70f,1.13f),new(-1.88f,1.075f) });
        body.ShapedPlate(side,-.811f,.031f,paint,"metal");
        body.ShapedPlate(side,.811f,.031f,paint,"metal");
        body.Box(new(1.50f,.07f,3.35f),new(0,.455f,0),"292e2a","metal");
        // A closed pressed bonnet joins both wings and the scuttle. Its edges
        // have thickness and a small bevel rather than daylight below a plane.
        body.CoachLoft(new CoachSection[] {
            new(-1.90f,.785f,1.017f,1.082f,.018f),
            new(-1.78f,.791f,1.025f,1.107f,.024f),
            new(-.715f,.790f,1.065f,1.128f,.016f) },paint,"metal");
        body.CoachLoft(new CoachSection[] {
            new(-.315f,.718f,1.634f,1.673f,.016f),
            new(-.250f,.753f,1.637f,1.694f,.018f),
            new(1.470f,.753f,1.637f,1.694f,.018f),
            new(1.550f,.714f,1.634f,1.673f,.016f) },paint,"metal");
        foreach(var sign in new[]{-1f,1f})
        {
            // Flat structural pillars, not round bars. The B pillar closes
            // both window edges and the C pillar meets the hatch surround.
            Vector3 P(float z,float y)=>new(sign*Mathf.Lerp(.802f,.738f,(y-1.13f)/.51f),y,z);
            body.ClosedPanel(P(-.72f,1.13f),P(-.655f,1.13f),P(-.230f,1.641f),P(-.300f,1.641f),
                Vector3.Right*sign,paint,"metal");
            body.ClosedPanel(P(.462f,1.13f),P(.557f,1.13f),P(.557f,1.641f),P(.462f,1.641f),
                Vector3.Right*sign,paint,"metal");
            body.ShapedPlate(new Vector2[]{new(1.323f,1.642f),new(1.550f,1.642f),
                new(1.848f,1.086f),new(1.575f,1.086f)},sign*.773f,.052f,paint,"metal");
            body.Beam(new(sign*.748f,1.651f,-.26f),new(sign*.748f,1.651f,1.46f),.010f,"8a9382","metal");
            body.Box(new(.042f,.047f,2.30f),new(sign*.801f,1.119f,.480f),paint,"metal");
            body.Box(new(.024f,.014f,2.24f),new(sign*.828f,1.150f,.480f),"a4ac9d","metal");
            body.Box(new(.021f,.026f,1.13f),new(sign*.835f,.706f,-.075f),"303a32","rubber");
            body.Box(new(.020f,.39f,.009f),new(sign*.832f,.899f,.492f),"243229","metal");
            body.Box(new(.042f,.022f,.145f),new(sign*.840f,1.013f,.300f),"b2b7ab","metal");
            body.Beam(new(sign*.809f,1.142f,-.566f),new(sign*.849f,1.212f,-.51f),.017f,"969f92","metal");
            body.CoachLoft(new CoachSection[]{new(-.59f,.035f,1.16f,1.29f,.015f),
                new(-.46f,.042f,1.158f,1.293f,.018f)},"29342c","metal",new(sign*.85f,0,0));
            body.Panel(new(sign*.892f,1.177f,-.575f),new(sign*.892f,1.273f,-.575f),
                new(sign*.892f,1.273f,-.477f),new(sign*.892f,1.177f,-.477f),Vector3.Right*sign,"76887d","glass");
            foreach(var axle in new[]{-1.18f,1.10f}) body.PressedWheelArch(sign,axle,paint);
            // A narrow, continuous sill closes the bottoms of the door panels.
            body.CoachLoft(new CoachSection[]{new(-.68f,.023f,.416f,.467f,.012f),
                new(.64f,.023f,.416f,.467f,.012f)},"3a4338","metal",new(sign*.814f,0,0));
        }
        body.Panel(new(-.75f,1.15f,-.685f),new(.75f,1.15f,-.685f),
            new(.70f,1.626f,-.284f),new(-.70f,1.626f,-.284f),Vector3.Forward,"83988c","glass");
        foreach(var sign in new[]{-1f,1f})
        {
            Vector3 P(float z,float y)=>new(sign*Mathf.Lerp(.794f,.737f,(y-1.155f)/.468f),y,z);
            body.Panel(P(-.638f,1.155f),P(-.225f,1.623f),P(.454f,1.623f),P(.454f,1.155f),
                Vector3.Right*sign,"83988c","glass");
            body.Panel(P(.563f,1.155f),P(.563f,1.623f),P(1.310f,1.623f),P(1.575f,1.155f),
                Vector3.Right*sign,"83988c","glass");
        }
        body.Beam(new(-.735f,1.64f,-.29f),new(.735f,1.64f,-.29f),.009f,"202922","rubber");
        body.Beam(new(-.778f,1.138f,-.70f),new(.778f,1.138f,-.70f),.008f,"202922","rubber");
        body.Box(new(1.56f,.61f,.055f),new(0,.785f,1.811f),paint,"metal");
        body.Panel(new(-.732f,1.143f,1.807f),new(-.687f,1.616f,1.535f),
            new(.687f,1.616f,1.535f),new(.732f,1.143f,1.807f),Vector3.Back,"83988c","glass");
        body.Beam(new(-.69f,1.622f,1.54f),new(.69f,1.622f,1.54f),.013f,paint,"metal");
        body.Box(new(1.45f,.043f,.055f),new(0,1.115f,1.794f),paint,"metal");
        body.Box(new(.17f,.025f,.028f),new(0,1.035f,1.852f),"a5ac9f","metal");
        body.Box(new(.47f,.12f,.021f),new(0,.777f,1.848f),"d5d7c7","metal");
        foreach(var x in new[]{-.682f,.682f})
        {
            body.Box(new(.16f,.29f,.041f),new(x,.852f,1.862f),"202923","rubber");
            body.Box(new(.137f,.095f,.047f),new(x,.933f,1.865f),"9d763b","metal");
            body.Box(new(.137f,.128f,.047f),new(x,.813f,1.865f),"8f3f32","metal");
        }
        // Recessed optical units sit inside an actual pierced front fascia.
        body.Box(new(.872f,.285f,.10f),new(0,.950f,-1.872f),"26312b","metal");
        for(var i=0;i<6;i++)body.Box(new(.832f,.010f,.018f),new(0,.847f+i*.039f,-1.933f),"929d8b","metal");
        foreach(var x in new[]{-.611f,.611f})
        {
            body.LampSurround(new(x,.963f,-1.941f),new(.181f,.146f),.137f,paint);
            body.Headlamp(new(x,.963f,-1.944f),.128f);
            body.Box(new(.189f,.067f,.048f),new(x,1.114f,-1.917f),"27342a","rubber");
            body.Box(new(.155f,.041f,.010f),new(x,1.114f,-1.947f),"bc9355","metal");
        }
        foreach(var sign in new[]{-1f,1f})
            body.Panel(new(sign*.792f,.818f,-1.941f),new(sign*.827f,.818f,-1.880f),
                new(sign*.827f,1.075f,-1.880f),new(sign*.792f,1.109f,-1.941f),
                new(sign,0,-1),paint,"metal");
        body.Panel(new(-.792f,1.109f,-1.941f),new(.792f,1.109f,-1.941f),
            new(.785f,1.082f,-1.900f),new(-.785f,1.082f,-1.900f),Vector3.Up,paint,"metal");
        body.Box(new(1.57f,.11f,.078f),new(0,.769f,-1.876f),paint,"metal");
        body.Box(new(1.57f,.085f,.065f),new(0,.631f,-1.864f),paint,"metal");
        body.Box(new(.47f,.12f,.024f),new(0,.715f,-1.936f),"dadccd","metal");
        foreach(var z in new[]{-1.974f,1.936f})
        {
            body.CoachLoft(new CoachSection[]{new(z-.055f,.833f,.476f,.565f,.025f),
                new(z,.888f,.465f,.578f,.024f),new(z+.055f,.833f,.476f,.565f,.025f)},"a4ac9c","metal");
            foreach(var x in new[]{-.855f,.855f})body.Box(new(.074f,.10f,.11f),new(x,.52f,z),"29322b","rubber");
        }
    }

    private static Visual BuildMotorcycleModel(Node3D root)
    {
        var b=new Batch(root,"MotorcycleFrame");const string paint="704a3e";
        // A double cradle joins the steering head, engine mounts and swingarm.
        foreach(var x in new[]{-.108f,.108f})
        {
            var points=new[]{new Vector3(x,1.015f,-.48f),new(x,.38f,-.38f),new(x,.29f,.15f),new(x,.42f,.55f)};
            for(var i=0;i<points.Length-1;i++)b.Beam(points[i],points[i+1],.025f,"333c34","metal");
            b.Beam(new(x,1.015f,-.48f),new(x,.89f,.40f),.025f,"333c34","metal");
            b.Beam(new(x,.89f,.40f),new(x,.42f,.55f),.025f,"333c34","metal");
            b.Beam(new(x,.42f,.23f),new(x,.34f,.68f),.027f,"333c34","metal");
        }
        b.Loft(new[]{S(.89f,-.525f,.035f,.055f),S(.936f,-.43f,.155f,.129f),
            S(.954f,-.22f,.211f,.162f),S(.928f,.02f,.190f,.131f),S(.886f,.145f,.060f,.046f)},paint,"metal",20,Vector3.Up);
        b.Cylinder(.041f,.021f,new(0,1.121f,-.21f),"aab0a3","metal");
        foreach(var sign in new[]{-1f,1f})
            b.Sphere(new(.012f,.046f,.116f),new(sign*.200f,.943f,-.13f),"3d3d31","rubber",new(0,sign*8,0));
        // Separate cast crankcase and transverse finned barrels replace the
        // rectangular stack. The exposed twin heads remain inside the hull.
        b.Loft(new[]{S(.473f,-.27f,.081f,.093f),S(.49f,-.18f,.151f,.157f),
            S(.49f,.17f,.145f,.154f),S(.484f,.30f,.078f,.096f)},"939b8b","metal",16,Vector3.Up);
        var steeringBridge=new Vector3(0,1.10f,-.47715f);
        foreach(var sign in new[]{-1f,1f})
        {
            b.Cylinder(.092f,.16f,new(sign*.21f,.543f,-.052f),"4e594e","metal",new(0,0,90));
            for(var fin=0;fin<7;fin++)b.Cylinder(.099f,.010f,new(sign*(.142f+fin*.022f),.543f,-.052f),"7b8777","metal",new(0,0,90));
            b.Sphere(new(.045f,.080f,.077f),new(sign*.318f,.543f,-.052f),"a4ab9e","metal");
            b.Beam(new(sign*.270f,.477f,-.11f),new(sign*.28f,.309f,-.22f),.026f,"a8ad9e","metal");
            b.Beam(new(sign*.28f,.309f,-.22f),new(sign*.27f,.285f,.61f),.029f,"a8ad9e","metal");
            b.Beam(new(sign*.27f,.285f,.40f),new(sign*.27f,.287f,.85f),.050f,"999f90","metal");
            b.Beam(new(sign*.125f,.61f,.02f),new(sign*.13f,.79f,.08f),.032f,"5c6758","metal");
            b.Beam(new(sign*.15f,.343f,.68f),new(sign*.15f,.87f,.48f),.030f,"969f91","metal");
            b.Beam(new(sign*.15f,.65f,.56f),new(sign*.15f,.865f,.48f),.043f,paint,"metal");
            b.Beam(new(sign*.15f,.34f,-.76f),new(sign*.15f,.80f,-.59f),.031f,"b0b8a8","metal");
            b.Beam(new(sign*.15f,.80f,-.59f),new(sign*.15f,1.119f,-.47f),.038f,paint,"metal");
            b.Beam(new(sign*.12f,.81f,-.575f),new(sign*.10f,.77f,-.735f),.015f,"8f9a89","metal");
            // Each handlebar riser overlaps the existing fork and bar end.
            b.Beam(steeringBridge+Vector3.Right*(sign*.15f),new(sign*.13f,1.166f,-.46f),.020f,"b0b8a8","metal");
            b.Beam(new(sign*.13f,1.166f,-.46f),new(sign*.35f,1.177f,-.365f),.016f,"b0b8a8","metal");
            b.Beam(new(sign*.35f,1.177f,-.365f),new(sign*.444f,1.177f,-.346f),.022f,"28332b","rubber");
            b.Beam(new(sign*.333f,1.19f,-.385f),new(sign*.375f,1.40f,-.43f),.010f,"a4ad9c","metal");
            b.Sphere(new(.068f,.045f,.016f),new(sign*.375f,1.43f,-.43f),"7b8a7c","metal");
            b.Beam(new(sign*.30f,1.181f,-.392f),new(sign*.431f,1.185f,-.425f),.006f,"afb5a7","metal");
            b.Beam(new(sign*.10f,.41f,.17f),new(sign*.32f,.39f,.17f),.018f,"8f9c89","metal");
            b.Beam(new(sign*.27f,.39f,.17f),new(sign*.34f,.39f,.17f),.029f,"30382e","rubber");
        }
        b.Beam(steeringBridge+Vector3.Left*.15f,steeringBridge+Vector3.Right*.15f,.018f,"b0b8a8","metal");
        // Upholstery has a tapered nose, a shaped edge and a supported base.
        b.Loft(new[]{S(1.012f,.10f,.064f,.024f),S(1.033f,.23f,.160f,.043f),
            S(1.035f,.59f,.176f,.047f),S(1.021f,.74f,.113f,.026f)},"343a30","vinyl",18,Vector3.Up);
        b.Beam(new(-.12f,.958f,.19f),new(-.12f,.952f,.69f),.018f,"30392f","metal");
        b.Beam(new(.12f,.958f,.19f),new(.12f,.952f,.69f),.018f,"30392f","metal");
        foreach(var sign in new[]{-1f,1f})
        {
            // Front mounts enter the original base rails and the seat underside.
            b.Beam(new(sign*.12f,.95728f,.25f),new(sign*.12f,1.021f,.25f),.018f,"30392f","metal");
            // Rear braces start inside the frame, cross those rails and enter the seat.
            b.Beam(new(sign*.108f,.90f,.3296f),new(sign*.12f,1.016f,.62f),.016f,"30392f","metal");
        }
        b.Beam(new(-.17f,1.057f,.47f),new(.17f,1.057f,.47f),.007f,"626856","leather");
        b.Mudguard(new(0,.34f,-.76f),.385f,.016f,.122f,-73,78,paint);
        b.Mudguard(new(0,.34f,.68f),.387f,.016f,.127f,-70,107,paint);
        foreach(var x in new[]{-.11f,.11f})
        {
            b.Beam(new(x,.34f,-.76f),new(x,.591f,-1.018f),.010f,"8e9b88","metal");
            b.Beam(new(x,.34f,.68f),new(x,.61f,.95f),.013f,"83907e","metal");
        }
        b.Loft(new[]{S(1.064f,-.556f,.062f,.064f),S(1.064f,-.655f,.105f,.105f),
            S(1.064f,-.720f,.106f,.106f)},"73806c","metal",20,Vector3.Up);
        b.Headlamp(new(0,1.064f,-.726f),.099f);
        b.Box(new(.154f,.082f,.047f),new(0,.73f,1.043f),"853d32","metal");
        b.Box(new(.17f,.126f,.015f),new(0,.609f,1.057f),"c1c6b3","metal");
        var instrument=new Node3D{Name="HandlebarInstruments",Position=new(0,1.14f,-.47f),RotationDegrees=new(-38,0,0)};
        // Attach the unchanged gauge housing from behind, below its dial face.
        b.Beam(steeringBridge,instrument.Transform*new Vector3(0,0,-.012f),.010f,"b0b8a8","metal");
        b.Finish();
        root.AddChild(instrument);
        var speed=Gauge(instrument,"Speedometer",Vector3.Zero,.047f,"км/ч",120);
        var wheels=new[]{RoadWheel(root,new(0,.34f,-.76f),.34f,.095f,true),RoadWheel(root,new(0,.34f,.68f),.34f,.105f,true)};
        root.SetMeta("roadVehicleGeometryRevision",2);
        return new(root,wheels,new[]{wheels[0]},Array.Empty<Node3D>(),null,null,
            Headlights(root,new[]{new Vector3(0,1.07f,-.75f)}),speed);
    }

    private static Node3D RoadWheel(Node3D root,Vector3 position,float radius,float width,bool spokes=false)
    {
        var pivot=new Node3D{Name="Wheel"+root.GetChildCount(),Position=position};root.AddChild(pivot);
        pivot.SetMeta("roadTyreRadius",radius);pivot.SetMeta("roadTyreWidth",width);
        var b=new Batch(pivot,"RoadWheelMesh");
        b.WheelTyre(radius,width,"292f29");
        b.WheelRim(radius*.76f,radius*.63f,width*.90f,"899785");
        var count=spokes?28:5;
        for(var i=0;i<count;i++)
        {
            var a=Mathf.Tau*i/count;var shift=spokes ? .10f : 0f;
            b.Beam(new(0,Mathf.Cos(a+shift)*radius*.17f,Mathf.Sin(a+shift)*radius*.17f),
                new(0,Mathf.Cos(a)*radius*.69f,Mathf.Sin(a)*radius*.69f),spokes ? .004f : .028f,"a0ac98","metal");
        }
        b.Cylinder(radius*.19f,width*.98f,Vector3.Zero,"9faa99","metal",new(0,0,90));
        if(!spokes)foreach(var side in new[]{-1f,1f})for(var i=0;i<5;i++)
        {
            var a=Mathf.Tau*i/5;
            b.Cylinder(.009f,.008f,new(side*width*.5f,Mathf.Cos(a)*radius*.135f,Mathf.Sin(a)*radius*.135f),"424d40","metal",new(0,0,90));
        }
        b.Finish();return pivot;
    }

    private sealed partial class Batch
    {
        public void Panel(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward,string color,string surface)
            =>Face(Get(color,surface),a,b,c,d,outward);

        public void ClosedPanel(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward,string color,string surface)
        {
            var st=Get(color,surface);var offset=outward.Normalized()*.024f;
            Face(st,a,b,c,d,outward);Face(st,a-offset,d-offset,c-offset,b-offset,-outward);
            var points=new[]{a,b,c,d};var centre=(a+b+c+d)*.25f;
            for(var i=0;i<4;i++)
            {
                var p=points[i];var q=points[(i+1)%4];
                Face(st,p,q,q-offset,p-offset,(p+q)*.5f-centre);
            }
        }

        private void FacingTriangle(SurfaceTool st,Vector3 a,Vector3 b,Vector3 c,Vector3 outward)
        {
            if((b-a).Cross(c-a).Dot(outward)<0)(b,c)=(c,b);
            Triangle(st,a,b,c,reverseWinding:true);
        }

        public void ShapedPlate(IReadOnlyList<Vector2> polygon,float x,float depth,string color,string surface)
        {
            var p=polygon.ToArray();var triangles=Geometry2D.TriangulatePolygon(p);var st=Get(color,surface);
            if(triangles.Length!=(p.Length-2)*3)throw new InvalidDataException("Road vehicle panel is not a simple closed polygon.");
            foreach(var sign in new[]{-1f,1f})for(var i=0;i<triangles.Length;i+=3)
            {
                Vector3 P(int index)=>new(x+sign*depth*.5f,p[index].Y,p[index].X);
                FacingTriangle(st,P(triangles[i]),P(triangles[i+1]),P(triangles[i+2]),Vector3.Right*sign);
            }
            var area=0f;for(var i=0;i<p.Length;i++)area+=p[i].Cross(p[(i+1)%p.Length]);
            for(var i=0;i<p.Length;i++)
            {
                var a=p[i];var b=p[(i+1)%p.Length];var e=b-a;
                Face(st,new(x-depth*.5f,a.Y,a.X),new(x+depth*.5f,a.Y,a.X),
                    new(x+depth*.5f,b.Y,b.X),new(x-depth*.5f,b.Y,b.X),new Vector3(0,-e.X,e.Y)*Math.Sign(area));
            }
        }

        public void CoachLoft(IReadOnlyList<CoachSection> sections,string color,string surface,Vector3 offset=default)
        {
            var rings=new Vector3[sections.Count,8];var st=Get(color,surface);
            for(var i=0;i<sections.Count;i++)
            {
                var s=sections[i];var w=s.HalfWidth;var b=s.Bevel;
                var outline=new[]{new Vector2(-w+b,s.Top),new Vector2(w-b,s.Top),new Vector2(w,s.Top-b),
                    new Vector2(w,s.Bottom+b),new Vector2(w-b,s.Bottom),new Vector2(-w+b,s.Bottom),
                    new Vector2(-w,s.Bottom+b),new Vector2(-w,s.Top-b)};
                for(var j=0;j<8;j++)rings[i,j]=new Vector3(outline[j].X,outline[j].Y,s.Z)+offset;
            }
            for(var i=0;i<sections.Count-1;i++)for(var j=0;j<8;j++)
            {
                var next=(j+1)%8;var centre=new Vector3(0,(sections[i].Bottom+sections[i].Top+sections[i+1].Bottom+sections[i+1].Top)*.25f,0)+offset;
                var normal=(rings[i,j]+rings[i,next]+rings[i+1,j]+rings[i+1,next])*.25f-centre;normal.Z=0;
                Face(st,rings[i,j],rings[i+1,j],rings[i+1,next],rings[i,next],normal);
            }
            foreach(var i in new[]{0,sections.Count-1})
            {
                var s=sections[i];var centre=new Vector3(0,(s.Bottom+s.Top)*.5f,s.Z)+offset;
                for(var j=0;j<8;j++)FacingTriangle(st,centre,rings[i,j],rings[i,(j+1)%8],i==0?Vector3.Forward:Vector3.Back);
            }
        }

        public void PressedWheelArch(float sign,float z,string paint)
        {
            var skin=Get(paint,"metal");var lining=Get("222a23","rubber");
            Vector3 P(float x,float radius,float angle)=>new(sign*x,.345f+Mathf.Sin(angle)*radius,z+Mathf.Cos(angle)*radius);
            for(var i=0;i<24;i++)
            {
                var a=Mathf.Pi*i/24;var b=Mathf.Pi*(i+1)/24;var normal=Vector3.Right*sign;
                Face(skin,P(.827f,.449f,a),P(.827f,.449f,b),P(.895f,.428f,b),P(.895f,.428f,a),normal);
                Face(skin,P(.895f,.428f,a),P(.895f,.428f,b),P(.854f,.405f,b),P(.854f,.405f,a),normal);
                var inward=new Vector3(0,-Mathf.Sin((a+b)*.5f),-Mathf.Cos((a+b)*.5f));
                Face(lining,P(.854f,.405f,a),P(.854f,.405f,b),P(.595f,.405f,b),P(.595f,.405f,a),inward);
            }
        }

        public void LampSurround(Vector3 centre,Vector2 half,float aperture,string paint)
        {
            var skin=Get(paint,"metal");var dark=Get("283128","rubber");
            Vector3 Inner(float angle,float depth=0)=>centre+new Vector3(Mathf.Cos(angle)*aperture,Mathf.Sin(angle)*aperture,depth);
            Vector3 Outer(float angle)
            {
                var c=Mathf.Cos(angle);var s=Mathf.Sin(angle);
                var d=Math.Min(half.X/Math.Max(.0001f,Math.Abs(c)),half.Y/Math.Max(.0001f,Math.Abs(s)));
                return centre+new Vector3(c*d,s*d,0);
            }
            for(var i=0;i<32;i++)
            {
                var a=Mathf.Tau*i/32;var b=Mathf.Tau*(i+1)/32;
                Face(skin,Outer(a),Outer(b),Inner(b),Inner(a),Vector3.Forward);
                Face(dark,Inner(a),Inner(b),Inner(b,.075f),Inner(a,.075f),
                    new Vector3(-Mathf.Cos((a+b)*.5f),-Mathf.Sin((a+b)*.5f),0));
            }
        }

        public void WheelRim(float outer,float inner,float width,string color)
        {
            var st=Get(color,"metal");
            Vector3 P(float x,float r,int i){var a=Mathf.Tau*i/32;return new(x,Mathf.Cos(a)*r,Mathf.Sin(a)*r);}
            for(var i=0;i<32;i++)
            {
                var a=Mathf.Tau*(i+.5f)/32;var radial=new Vector3(0,Mathf.Cos(a),Mathf.Sin(a));
                Face(st,P(-width*.5f,outer,i),P(width*.5f,outer,i),P(width*.5f,outer,i+1),P(-width*.5f,outer,i+1),radial);
                Face(st,P(-width*.5f,inner,i),P(-width*.5f,inner,i+1),P(width*.5f,inner,i+1),P(width*.5f,inner,i),-radial);
                foreach(var sign in new[]{-1f,1f})
                    Face(st,P(sign*width*.5f,outer,i),P(sign*width*.5f,outer,i+1),
                        P(sign*width*.5f,inner,i+1),P(sign*width*.5f,inner,i),Vector3.Right*sign);
            }
        }

        public void WheelTyre(float radius,float width,string color)
        {
            var st=Get(color,"rubber");
            var sections=VehicleWheelGeometry.Sections(radius,width);
            for(var i=0;i<sections.Length-1;i++)for(var segment=0;segment<32;segment++)
            {
                Vector3 P(int s,int a){var angle=Mathf.Tau*a/32;return new(sections[s].X,Mathf.Cos(angle)*sections[s].Y,Mathf.Sin(angle)*sections[s].Y);}
                var normal=new Vector3(0,Mathf.Cos(Mathf.Tau*(segment+.5f)/32),Mathf.Sin(Mathf.Tau*(segment+.5f)/32));
                if(i<2)normal.X=-.65f;if(i>2)normal.X=.65f;
                Face(st,P(i,segment),P(i+1,segment),P(i+1,segment+1),P(i,segment+1),normal);
            }
            for(var i=0;i<32;i++)
            {
                var angle=Mathf.Tau*i/32;
                Box(VehicleWheelGeometry.TreadSize(width),new(0,Mathf.Cos(angle)*VehicleWheelGeometry.TreadCentreRadius(radius),Mathf.Sin(angle)*VehicleWheelGeometry.TreadCentreRadius(radius)),
                    "343c30","rubber",new(Mathf.RadToDeg(angle),0,0));
            }
        }
    }
}
