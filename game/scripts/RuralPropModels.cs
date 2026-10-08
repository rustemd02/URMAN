using Godot;

namespace Urman.Godot;

public static class RuralPropModels
{
    /// <summary>VIS-051: the metric body this furniture family is built to, so the
    /// talking group can be tied to a real seated person instead of to a camera.
    /// Seat height, table crown, the reach of a resting hand and the height of a
    /// seated face above the same floor.</summary>
    public const float SeatCrownMetres = .457f;
    public const float TableCrownMetres = .765f;
    public const float HandReachMetres = .62f;
    public const float FaceAboveSeatMetres = .58f;
    public const float HandAboveSeatMetres = .20f;

    /// <summary>VIS-051: the resting place of a cup or a lamp for a person seated at
    /// <paramref name="seated"/> (a point on the room floor), on the table crown,
    /// inside the arc a resting hand actually reaches. Nothing is placed further than
    /// the forearm, so the thing reads as used rather than as studio dressing.</summary>
    public static Vector3 HandRestPoint(Vector3 seated,float yawDegrees,float forward=.45f,float side=0f)
    {
        var yaw=Mathf.DegToRad(yawDegrees);
        var dir=new Vector3(Mathf.Sin(yaw),0f,Mathf.Cos(yaw));
        var across=new Vector3(dir.Z,0f,-dir.X);
        var reach=Mathf.Clamp(forward,.10f,HandReachMetres);
        return seated+Vector3.Up*TableCrownMetres+dir*reach+across*Mathf.Clamp(side,-.35f,.35f);
    }

    /// <summary>VIS-051: does this piece actually cover the seated person's face from
    /// the guest's eye line? Only a real overlap along that line counts; a thing that
    /// merely sits behind the person is background, not an obstacle.</summary>
    public static bool CoversFace(Vector3 face,Vector3 eyeLineEnd,Aabb piece,float tolerance=.02f)
    {
        var to=eyeLineEnd-face;
        var length=to.Length();
        if(length<.01f)return false;
        to/=length;
        var centre=piece.GetCenter();
        var closest=(centre-face).Dot(to);
        if(closest<0f||closest>length)return false;
        var hit=face+to*closest;
        return Mathf.Abs(hit.X-centre.X)<piece.Size.X*.5f+tolerance
            &&Mathf.Abs(hit.Y-centre.Y)<piece.Size.Y*.5f+tolerance
            &&Mathf.Abs(hit.Z-centre.Z)<piece.Size.Z*.5f+tolerance;
    }
    private static Material M(string kind,string tint="ffffff")=>RuralPropMaterials.Surface(kind,tint);
    private static Node3D Root(Node3D parent,string name,Vector3 at,float yaw=0)
    {
        var node=new Node3D{Name=name,Position=at,RotationDegrees=new(0,yaw,0)};
        node.SetMeta("modelOwner",nameof(RuralPropModels));node.SetMeta("assetContract","TX29 metric UV / actual geometry / reused surfaces with explicit pending replacements");
        parent.AddChild(node);return node;
    }
    public static Node3D Chair(Node3D parent,string name,Vector3 at,float yaw=0)
    {
        var r=Root(parent,name,at,yaw);var wood=M("plywood");var steel=M("steel","60756c");var rubber=M("rubber");
        RuralPropGeometry.Part(r,"BentPlywoodSeat",RuralPropGeometry.BowedPanel(.42f,.39f,.018f,.048f,.012f),new(0,.457f,0),wood,new(-90,0,0));
        RuralPropGeometry.Part(r,"BentPlywoodBack",RuralPropGeometry.BowedPanel(.40f,.27f,.018f,.048f,.024f),new(0,.756f,-.20f),wood,new(-8,0,0));
        foreach(var x in new[]{-.17f,.17f})
        {
            RuralPropGeometry.Tube(r,"FrontLeg",new(x,.025f,.17f),new(x,.438f,.14f),.012f,steel);
            RuralPropGeometry.Tube(r,"RearLeg",new(x,.025f,-.21f),new(x,.44f,-.15f),.012f,steel);
            RuralPropGeometry.Tube(r,"SeatRail",new(x,.428f,.15f),new(x,.428f,-.17f),.012f,steel);
            RuralPropGeometry.Tube(r,"BackUpright",new(x,.44f,-.17f),new(x,.87f,-.23f),.011f,steel);
            foreach(var z in new[]{.17f,-.21f})RuralPropGeometry.Block(r,"RubberFoot",new(.031f,.028f,.031f),new(x,.014f,z),rubber,.005f);
            foreach(var y in new[]{.67f,.83f})RuralPropGeometry.Tube(r,"BackRivet",new(x,y,-.177f),new(x,y,-.173f),.006f,M("metal"),12);
            RuralPropGeometry.Tube(r,"SideBrace",new(x,.22f,.163f),new(x,.22f,-.183f),.009f,steel);
        }
        RuralPropGeometry.Tube(r,"CrossBrace",new(-.17f,.41f,-.12f),new(.17f,.41f,-.12f),.011f,steel);
        return r;
    }
    public static Node3D Desk(Node3D parent,string name,Vector3 at,float yaw,float w,float d,float h)
    {
        var r=Root(parent,name,at,yaw);var board=M("plywood");var steel=M("steel","65766d");
        RuralPropGeometry.Block(r,"LaminatedTop",new(w,.035f,d),new(0,h-.0175f,0),M("laminate"),.007f);
        foreach(var z in new[]{-d*.5f+.0025f,d*.5f-.0025f})RuralPropGeometry.Block(r,"EdgeBand",new(w-.01f,.029f,.006f),new(0,h-.0175f,z),M("wood","9c8269"),.002f);
        foreach(var x in new[]{-w*.5f+.075f,w*.5f-.075f})
        {
            foreach(var z in new[]{-d*.5f+.085f,d*.5f-.085f})
            {
                RuralPropGeometry.Tube(r,"SteelLeg",new(x,.03f,z),new(x,h-.045f,z),.018f,steel);
                RuralPropGeometry.Block(r,"LegCap",new(.039f,.027f,.039f),new(x,.014f,z),M("rubber"),.006f);
            }
            RuralPropGeometry.Tube(r,"EndBrace",new(x,.22f,-d*.5f+.085f),new(x,.22f,d*.5f-.085f),.014f,steel);
            RuralPropGeometry.Tube(r,"TopRail",new(x,h-.058f,-d*.5f+.08f),new(x,h-.058f,d*.5f-.08f),.014f,steel);
            foreach(var z in new[]{-d*.5f+.035f,d*.5f-.035f})RuralPropGeometry.Tube(r,"CountersunkBolt",new(x,h-.003f,z),new(x,h+.001f,z),.005f,M("metal"),12);
        }
        RuralPropGeometry.Block(r,"ModestyPanel",new(w-.14f,.20f,.015f),new(0,h-.16f,-d*.5f+.08f),board,.003f);
        RuralPropGeometry.Tube(r,"CrossRail",new(-w*.5f+.075f,.20f,-d*.5f+.085f),new(w*.5f-.075f,.20f,-d*.5f+.085f),.014f,steel);
        return r;
    }
    public static Node3D Stool(Node3D parent,string name,Vector3 at)
    {
        var r=Root(parent,name,at);var wood=M("wood");
        RuralPropGeometry.Block(r,"Seat",new(.32f,.038f,.32f),new(0,.443f,0),wood,.018f);
        foreach(var x in new[]{-.12f,.12f})foreach(var z in new[]{-.12f,.12f})
            RuralPropGeometry.Tube(r,"TurnedLeg",new(x*1.15f,0f,z*1.15f),new(x,.425f,z),.023f,wood);
        foreach(var x in new[]{-.13f,.13f})RuralPropGeometry.Tube(r,"SideStretcher",new(x,.18f,-.13f),new(x,.18f,.13f),.012f,wood);
        foreach(var z in new[]{-.13f,.13f})RuralPropGeometry.Tube(r,"CrossStretcher",new(-.13f,.15f,z),new(.13f,.15f,z),.012f,wood);
        return r;
    }
    public static Node3D Bench(Node3D parent,string name,Vector3 at,float yaw=0,float width=1.8f,bool backrest=true)
    {
        var r=Root(parent,name,at,yaw);var wood=M("wood");var steel=M("steel","546459");
        foreach(var z in new[]{-.14f,0,.14f})RuralPropGeometry.Block(r,"SeatPlank",new(width,.04f,.125f),new(0,.46f,z),wood,.008f);
        if(backrest) foreach(var y in new[]{.68f,.85f})RuralPropGeometry.Block(r,"BackPlank",new(width,.13f,.033f),new(0,y,-.24f),wood,.008f,new(-8,0,0));
        foreach(var x in new[]{-width*.36f,width*.36f})
        {
            // The tube feet are housed into their own foot plate instead of ending
            // in the air above it, and the plate lies flat on the floor it carries.
            RuralPropGeometry.Tube(r,"FrontLeg",new(x,.010f,.20f),new(x,.44f,.14f),.023f,steel);
            RuralPropGeometry.Tube(r,"RearLeg",new(x,.010f,-.26f),new(x,backrest ? .93f : .44f,-.26f),.023f,steel);
            RuralPropGeometry.Tube(r,"SeatSupport",new(x,.42f,-.26f),new(x,.42f,.20f),.024f,steel);
            RuralPropGeometry.Tube(r,"Brace",new(x,.12f,-.25f),new(x,.40f,.16f),.014f,steel);
            foreach(var z in new[]{-.25f,.20f})RuralPropGeometry.Block(r,"FootPlate",new(.12f,.012f,.10f),new(x,.006f,z),steel,.003f);
        }
        return r;
    }
    public static Node3D Radiator(Node3D parent,string name,Vector3 at,float width=1.1f,float yaw=0)
    {
        var r=Root(parent,name,at,yaw);var enamel=M("steel","e8e3d6");var n=Mathf.Max(3,Mathf.RoundToInt(width/.085f));
        // MC-140 style double column sections, open channels, rounded headers.
        for(var i=0;i<n;i++)
        {
            var x=(i-(n-1)*.5f)*.085f;
            foreach(var z in new[]{-.06f,.06f})
            {
                RuralPropGeometry.Tube(r,"SectionColumn",new(x,.06f,z),new(x,.54f,z),.027f,enamel);
            }
            RuralPropGeometry.Block(r,"UpperHeader",new(.069f,.073f,.16f),new(x,.525f,0),enamel,.018f);
            RuralPropGeometry.Block(r,"LowerHeader",new(.069f,.073f,.16f),new(x,.075f,0),enamel,.018f);
        }
        foreach(var y in new[]{.075f,.525f})RuralPropGeometry.Tube(r,"NippleRail",new(-width*.5f-.035f,y,0),new(width*.5f+.055f,y,0),.021f,enamel);
        RuralPropGeometry.Tube(r,"FeedPipe",new(width*.5f+.065f,.075f,0),new(width*.5f+.065f,-.12f,0),.015f,enamel);
        RuralPropGeometry.Tube(r,"Valve",new(width*.5f+.06f,.525f,0),new(width*.5f+.11f,.525f,0),.032f,M("metal"));
        RuralPropGeometry.Block(r,"ValveKnob",new(.03f,.055f,.055f),new(width*.5f+.13f,.525f,0),M("plastic","ded9cb"),.008f);
        return r;
    }
    public static Node3D Cup(Node3D parent,string name,Vector3 at)
    {
        var r=Root(parent,name,at);var ceramic=M("ceramic","ede7d9");
        RuralPropGeometry.Part(r,"HollowCup",RuralPropGeometry.Lathe("tea-cup",[new(0,0f),new(.034f,0f),new(.040f,.008f),new(.045f,.068f),new(.044f,.074f),new(.041f,.074f),new(.037f,.013f),new(0,.013f)]),Vector3.Zero,ceramic);
        var handle=new Vector3[17];for(var i=0;i<handle.Length;i++){var a=-Mathf.Pi*.5f+i/(float)(handle.Length-1)*Mathf.Pi;handle[i]=new(.042f+Mathf.Cos(a)*.029f,.041f+Mathf.Sin(a)*.025f,0);}
        for(var i=0;i<handle.Length-1;i++)RuralPropGeometry.Tube(r,"Handle",handle[i],handle[i+1],.005f,ceramic,12);
        return r;
    }
    public static Node3D Teapot(Node3D parent,string name,Vector3 at)
    {
        var r=Root(parent,name,at);var ceramic=M("ceramic","dcd5bd");
        RuralPropGeometry.Part(r,"PotBody",RuralPropGeometry.Lathe("teapot-body",[new(0,0),new(.05f,0),new(.07f,.01f),new(.09f,.045f),new(.083f,.09f),new(.055f,.123f),new(.050f,.125f)]),Vector3.Zero,ceramic);
        RuralPropGeometry.Part(r,"Lid",RuralPropGeometry.Lathe("teapot-lid",[new(0,.142f),new(.012f,.142f),new(.018f,.132f),new(.045f,.13f),new(.053f,.125f)]),Vector3.Zero,ceramic);
        RuralPropGeometry.Tube(r,"SpoutBase",new(.07f,.045f,0),new(.115f,.075f,0),.024f,ceramic);
        RuralPropGeometry.Tube(r,"SpoutMouth",new(.115f,.075f,0),new(.145f,.11f,0),.016f,ceramic);
        for(var i=0;i<18;i++)
        { var a=-Mathf.Pi*.5f+i/18f*Mathf.Pi;var b=-Mathf.Pi*.5f+(i+1)/18f*Mathf.Pi;
            RuralPropGeometry.Tube(r,"Handle",new(-.07f-Mathf.Cos(a)*.055f,.065f+Mathf.Sin(a)*.050f,0),new(-.07f-Mathf.Cos(b)*.055f,.065f+Mathf.Sin(b)*.050f,0),.008f,ceramic,12); }
        return r;
    }
    public static Node3D Samovar(Node3D parent,string name,Vector3 at)
    {
        var r=Root(parent,name,at);var brass=M("brass");
        RuralPropGeometry.Part(r,"Body",RuralPropGeometry.Lathe("samovar",[new(0,.03f),new(.08f,.03f),new(.075f,.09f),new(.125f,.14f),new(.135f,.23f),new(.10f,.31f),new(.115f,.33f),new(.11f,.345f),new(.06f,.36f),new(.04f,.36f),new(.04f,.44f)]),Vector3.Zero,brass);
        foreach(var x in new[]{-.07f,.07f})foreach(var z in new[]{-.07f,.07f})RuralPropGeometry.Block(r,"Foot",new(.025f,.06f,.025f),new(x,.03f,z),brass,.006f);
        foreach(var side in new[]{-1f,1f})
        {
            RuralPropGeometry.Tube(r,"HandleBracket",new(side*.10f,.27f,0),new(side*.16f,.27f,0),.01f,brass);
            RuralPropGeometry.Tube(r,"WoodGrip",new(side*.16f,.25f,-.035f),new(side*.16f,.25f,.035f),.014f,M("wood"));
        }
        RuralPropGeometry.Tube(r,"Tap",new(0,.14f,.11f),new(0,.14f,.18f),.012f,brass);
        RuralPropGeometry.Tube(r,"TapSpout",new(0,.14f,.18f),new(0,.11f,.18f),.011f,brass);
        RuralPropGeometry.Tube(r,"TapKey",new(-.025f,.16f,.15f),new(.025f,.16f,.15f),.007f,brass);
        return r;
    }
}
