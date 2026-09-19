using Godot;

namespace Urman.Godot;

public static partial class VehicleVisualFactory
{
    // Original geometry; proportions informed by UMN Extension's conformation
    // reference, not copied photographs or an unlicensed downloaded model:
    // https://extension.umn.edu/agriculture/animals-and-livestock/horse/conformation-of-the-horse
    private readonly record struct LoftSection(Vector3 Centre, Vector2 Radius);
    private static LoftSection S(float y,float z,float width,float depth,float x=0)
        =>new(new(x,y,z),new(width,depth));

    private static (IReadOnlyList<Node3D> Legs,Node3D Head,VehicleHorsePose Pose) BuildHorse(Node3D root)
    {
        const string coat="685342";
        var horse=new Node3D{Name="Horse"};root.AddChild(horse);
        horse.SetMeta("anatomySource","UMN Extension/conformation-of-the-horse; original authored mesh profiles");
        horse.SetMeta("physicsOwner","parent VehicleController; articulated legs are presentation only");
        var body=new Batch(horse,"HorseBody");
        // One contoured barrel joins the rounded croup to the deep shoulder;
        // no exposed stack of separate ellipsoids at the chest and hindquarters.
        body.Loft(new[]{S(1.29f,-.74f,.06f,.13f),S(1.28f,-.88f,.25f,.31f),
            S(1.23f,-1.12f,.34f,.37f),S(1.19f,-1.47f,.37f,.38f),
            S(1.21f,-1.80f,.35f,.40f),S(1.26f,-2.05f,.32f,.41f),
            S(1.22f,-2.25f,.25f,.33f),S(1.17f,-2.35f,.12f,.20f)},coat,"horse_coat");
        body.Loft(new[]{S(1.33f,-2.06f,.27f,.26f),S(1.51f,-2.19f,.235f,.235f),
            S(1.76f,-2.32f,.17f,.20f),S(1.95f,-2.42f,.115f,.14f),
            S(2.01f,-2.47f,.095f,.105f)},coat,"horse_coat");
        body.Loft(new[]{S(1.46f,-1.99f,.025f,.065f),S(1.64f,-2.03f,.036f,.105f),
            S(1.84f,-2.15f,.040f,.105f),S(2.02f,-2.29f,.025f,.045f)},"29271f","horse_coat",12);
        body.Loft(new[]{S(1.37f,-.78f,.052f,.058f),S(1.18f,-.60f,.057f,.068f),
            S(.88f,-.53f,.068f,.078f),S(.53f,-.57f,.074f,.068f),
            S(.34f,-.63f,.032f,.035f)},"29271f","horse_coat",12);
        // Collar, hames, girth and breeching follow the horse's actual volume.
        for(var i=0;i<32;i++)
        {
            var a=Mathf.Tau*i/32;var b=Mathf.Tau*(i+1)/32;
            Vector3 Collar(float angle)=>new(Mathf.Cos(angle)*.305f,1.26f+Mathf.Sin(angle)*.315f,-2.225f+Mathf.Sin(angle)*.07f);
            Vector3 Girth(float angle)=>new(Mathf.Cos(angle)*.361f,1.205f+Mathf.Sin(angle)*.403f,-1.81f);
            body.Beam(Collar(a),Collar(b),.027f,"382e23","leather");
            body.Beam(Girth(a),Girth(b),.019f,"352f26","leather");
        }
        foreach(var side in new[]{-1f,1f})
        {
            body.Beam(new(side*.285f,1.12f,-2.225f),new(side*.25f,1.48f,-2.18f),.018f,"8a8169","metal");
            body.Beam(new(side*.30f,1.15f,-2.21f),new(side*.456f,.922f,-2.05f),.020f,"44392a","leather");
            // Leather tug loops actually surround the wooden shaft tips.
            for(var i=0;i<20;i++)
            {
                var a=Mathf.Tau*i/20;var b=Mathf.Tau*(i+1)/20;
                Vector3 Tug(float angle)=>new(side*.456f+Mathf.Cos(angle)*.072f,.85f+Mathf.Sin(angle)*.072f,-2.05f);
                body.Beam(Tug(a),Tug(b),.012f,"44392a","leather");
            }
            body.Beam(new(side*.19f,1.66f,-1.81f),new(side*.34f,1.51f,-1.81f),.022f,"44392a","leather");
            body.Beam(new(side*.34f,1.51f,-1.81f),new(side*.465f,.835f,-1.81f),.020f,"44392a","leather");
            body.Beam(new(side*.348f,1.22f,-1.80f),new(side*.335f,1.18f,-1.02f),.018f,"44392a","leather");
            body.Beam(new(side*.335f,1.18f,-1.02f),new(side*.25f,1.15f,-.81f),.024f,"44392a","leather");
        }
        body.Loft(new[]{S(1.641f,-1.99f,.14f,.024f),S(1.65f,-1.87f,.20f,.034f),
            S(1.632f,-1.69f,.19f,.03f),S(1.624f,-1.61f,.13f,.02f)},"44392a","leather",16);
        body.Beam(new(-.25f,1.15f,-.81f),new(.25f,1.15f,-.81f),.024f,"44392a","leather");
        body.Finish();

        var head=new Node3D{Name="HorseHead",Position=new(0,1.99f,-2.48f)};horse.AddChild(head);
        var h=new Batch(head,"HeadMesh");
        h.Loft(new[]{S(.02f,.075f,.105f,.145f),S(-.035f,-.04f,.155f,.158f),
            S(-.15f,-.21f,.125f,.11f),S(-.26f,-.35f,.108f,.083f),
            S(-.30f,-.405f,.13f,.083f),S(-.315f,-.45f,.112f,.067f)},coat,"horse_coat");
        foreach(var side in new[]{-1f,1f})
        {
            h.Loft(new[]{S(.10f,.018f,.045f,.049f,side*.105f),S(.19f,.025f,.045f,.035f,side*.126f),
                S(.32f,.020f,.024f,.023f,side*.147f),S(.365f,-.002f,.004f,.005f,side*.15f)},"57412f","horse_coat",12);
            h.Sphere(new(.022f,.019f,.016f),new(side*.151f,-.035f,-.092f),"171a15","dial");
            h.Sphere(new(.015f,.023f,.035f),new(side*.112f,-.286f,-.408f),"292922","horse_coat");
            h.Beam(new(side*.146f,-.19f,-.30f),new(side*.150f,.06f,.016f),.012f,"302a21","leather");
            h.Beam(new(side*.15f,.06f,.016f),new(side*.10f,.12f,.08f),.012f,"302a21","leather");
            h.Sphere(new(.021f,.021f,.01f),new(side*.145f,-.21f,-.32f),"a59b7e","metal");
        }
        h.Beam(new(-.14f,-.19f,-.31f),new(.14f,-.19f,-.31f),.014f,"302a21","leather");
        h.Beam(new(-.105f,-.354f,-.46f),new(.105f,-.354f,-.46f),.004f,"40382e","horse_coat");
        h.Finish();

        var legs=new List<Node3D>();var bindings=new List<VehicleHorsePose.LegBinding>();
        foreach(var side in new[]{-1f,1f})foreach(var hind in new[]{false,true})
        {
            var x=side*(hind ? .235f : .225f);var z=hind ? -1.03f : -2.12f;
            var upper=hind ? .59f : .57f;const float lower=.52f;
            var hip=new Node3D{Name="HorseLeg"+legs.Count,Position=new(x,hind?1.14f:1.12f,z)};horse.AddChild(hip);
            var thigh=new Batch(hip,hind?"ThighAndHock":"ForearmAndKnee");
            thigh.Loft(new[]{S(.085f,0,hind ? .145f : .105f,hind ? .17f : .115f),
                S(-upper*.23f,0,hind ? .13f : .095f,hind ? .145f : .11f),
                S(-upper*.66f,0,.061f,.07f),S(-upper,0,.060f,hind ? .078f : .062f)},coat,"horse_coat",14);
            thigh.Finish();
            var knee=new Node3D{Name=hind?"Hock":"Knee",Position=Vector3.Down*upper};hip.AddChild(knee);
            var cannon=new Batch(knee,"CannonAndFetlock");
            cannon.Loft(new[]{S(.026f,0,.060f,hind ? .079f : .061f),S(-.08f,0,.044f,.047f),
                S(-lower*.73f,0,.035f,.040f),S(-lower+.035f,0,.052f,.057f),
                S(-lower-.012f,0,.042f,.045f)},"554331","horse_coat",14);cannon.Finish();
            var hoof=new Node3D{Name="Hoof",Position=Vector3.Down*lower};knee.AddChild(hoof);
            var hoofMesh=new Batch(hoof,"HoofWallAndSole");
            hoofMesh.Loft(new[]{S(0,.044f,.045f,.050f),S(-.028f,.037f,.054f,.062f),
                S(-.105f,.006f,VehicleHorsePose.SoleHalfWidth,VehicleHorsePose.SoleHalfLength),
                S(-VehicleHorsePose.SoleDepth,0,VehicleHorsePose.SoleHalfWidth,VehicleHorsePose.SoleHalfLength)},
                "302d25","hoof",VehicleHorsePose.SoleSegments,crossSectionAcross:Vector3.Back);hoofMesh.Finish();
            legs.Add(hip);bindings.Add(new(hip,knee,hoof,upper,lower,new(x,0,z+(hind ? .025f : -.04f)),hind));
        }
        var pose=new VehicleHorsePose{Name="SupportedHorsePose"};horse.AddChild(pose);
        pose.Configure(horse,bindings,head);
        var reins=new List<Node3D>();var from=new List<Vector3>();var to=new List<Vector3>();
        foreach(var side in new[]{-1f,1f})
        {
            var rein=new Node3D{Name=side<0?"LeftRein":"RightRein"};horse.AddChild(rein);
            var strap=new Batch(rein,"ReinLeather");strap.Cylinder(.009f,1,Vector3.Zero,"3d3529","leather");strap.Finish();
            reins.Add(rein);from.Add(new(side*.25f,1.72f,-1.05f));to.Add(new(side*.145f,-.21f,-.32f));
        }
        pose.ConfigureReins(reins,from,to);
        return (legs,head,pose);
    }

    private sealed partial class Batch
    {
        private readonly record struct LoftVertex(Vector3 Point,Vector3 Normal,Vector2 Uv);

        // Smooth normals follow both the cross-section and its changing radius.
        // Every new triangle is explicitly clockwise from its outward side.
        public void Loft(IReadOnlyList<LoftSection> sections,string color,string surface,int sides=20,Vector3? crossSectionAcross=null)
        {
            if(sections.Count<2)throw new ArgumentException("A contoured part needs at least two sections.");
            var points=new Vector3[sections.Count,sides];var vertices=new LoftVertex[sections.Count,sides];
            var axes=new Vector3[sections.Count];
            var acrosses=new Vector3[sections.Count];
            for(var ring=0;ring<sections.Count;ring++)
            {
                var tangent=sections[Math.Min(ring+1,sections.Count-1)].Centre-sections[Math.Max(0,ring-1)].Centre;
                axes[ring]=tangent.Normalized();var across=crossSectionAcross??axes[ring].Cross(Vector3.Right).Normalized();
                acrosses[ring]=across;
                for(var side=0;side<sides;side++)
                {
                    var angle=Mathf.Tau*side/sides;
                    points[ring,side]=sections[ring].Centre+Vector3.Right*(Mathf.Cos(angle)*sections[ring].Radius.X)
                        +across*(Mathf.Sin(angle)*sections[ring].Radius.Y);
                }
            }
            for(var ring=0;ring<sections.Count;ring++)for(var side=0;side<sides;side++)
            {
                var tangent=points[Math.Min(ring+1,sections.Count-1),side]-points[Math.Max(ring-1,0),side];
                var around=points[ring,(side+1)%sides]-points[ring,(side+sides-1)%sides];
                var normal=tangent.Cross(around).Normalized();
                if(normal.Dot(points[ring,side]-sections[ring].Centre)<0)normal=-normal;
                vertices[ring,side]=new(points[ring,side],normal,new(side/(float)sides,ring/(float)(sections.Count-1)));
            }
            var st=Get(color,surface);
            for(var ring=0;ring<sections.Count-1;ring++)for(var side=0;side<sides;side++)
            {
                var next=(side+1)%sides;
                SmoothTriangle(st,vertices[ring,side],vertices[ring+1,side],vertices[ring+1,next]);
                SmoothTriangle(st,vertices[ring,side],vertices[ring+1,next],vertices[ring,next]);
            }
            foreach(var ring in new[]{0,sections.Count-1})for(var side=0;side<sides;side++)
            {
                var normal=Vector3.Right.Cross(acrosses[ring]).Normalized();
                if(normal.Dot(axes[ring])<0)normal=-normal;
                normal*=ring==0?-1:1;
                SmoothTriangle(st,new(sections[ring].Centre,normal,Vector2.Zero),
                    new(points[ring,side],normal,Vector2.Zero),new(points[ring,(side+1)%sides],normal,Vector2.Zero));
            }
        }

        private void SmoothTriangle(SurfaceTool st,LoftVertex a,LoftVertex b,LoftVertex c)
        {
            if((b.Point-a.Point).Cross(c.Point-a.Point).Dot(a.Normal+b.Normal+c.Normal)<0)(b,c)=(c,b);
            foreach(var vertex in new[]{a,c,b})
            {
                var tangent=Vector3.Right.Slide(vertex.Normal).Normalized();
                if(tangent.LengthSquared()<.01f)tangent=vertex.Normal.Cross(Vector3.Forward).Normalized();
                st.SetNormal(vertex.Normal);st.SetTangent(new Plane(tangent,1));st.SetUV(vertex.Uv);st.AddVertex(vertex.Point);
            }
            _expectedCorners+=3;_manualCorners+=3;
        }

        private void Face(SurfaceTool st,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward)
        {
            if((b-a).Cross(c-a).Dot(outward)<0)(b,d)=(d,b);
            Triangle(st,a,b,c,reverseWinding:true);Triangle(st,a,c,d,reverseWinding:true);
        }
    }
}
