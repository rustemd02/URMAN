using Godot;

namespace Urman.Godot;

/// <summary>The road tyre's shared metre-scale profile; no physics or save owner.</summary>
internal static class VehicleWheelGeometry
{
    internal const int EnvelopeSides=64;
    internal static Vector2[] Sections(float radius,float width)=>new[]{
        new Vector2(-width*.49f,radius*.74f),new(-width*.50f,radius*.88f),
        new(-width*.36f,radius*.99f),new(width*.36f,radius*.99f),
        new(width*.50f,radius*.88f),new(width*.49f,radius*.74f)};

    internal static Vector3 TreadSize(float width)=>new(width*.70f,.012f,.032f);
    internal static float TreadCentreRadius(float radius)=>radius-.003f;
    internal static float TreadOuterRadius(float radius)
    {
        var size=TreadSize(1);
        return new Vector2(TreadCentreRadius(radius)+size.Y*.5f,size.Z*.5f).Length();
    }

    // Four rings are the convex hull of the six sidewall rings and the tread's
    // swept rotation. The intermediate .36-width shoulder lies inside this hull.
    // Circumscribing (not inscribing) each 64-gon contains every spin phase.
    internal static Vector3[] EnvelopePoints(float radius,float width)
    {
        var result=new Vector3[EnvelopeSides*4];var count=0;
        foreach(var ring in new[]{new Vector2(-width*.5f,radius*.88f),
            new(-width*.35f,TreadOuterRadius(radius)),new(width*.35f,TreadOuterRadius(radius)),
            new(width*.5f,radius*.88f)})
        for(var i=0;i<EnvelopeSides;i++)
        {
            var angle=Mathf.Tau*i/EnvelopeSides;var circumscribed=ring.Y/Mathf.Cos(Mathf.Pi/EnvelopeSides);
            result[count++]=new(ring.X,Mathf.Cos(angle)*circumscribed,Mathf.Sin(angle)*circumscribed);
        }
        return result;
    }

    internal static bool EnvelopeContains(Vector3 point,float radius,float width,float tolerance=.00002f)
    {
        var x=Math.Abs(point.X);if(x>width*.5f+tolerance)return false;
        var shoulder=Mathf.Clamp((x-width*.35f)/(width*.15f),0,1);
        var radial=Mathf.Lerp(TreadOuterRadius(radius),radius*.88f,shoulder);
        for(var i=0;i<EnvelopeSides;i++)
        {
            var angle=Mathf.Tau*(i+.5f)/EnvelopeSides;
            if(point.Y*Mathf.Cos(angle)+point.Z*Mathf.Sin(angle)>radial+tolerance)return false;
        }
        return true;
    }
}
