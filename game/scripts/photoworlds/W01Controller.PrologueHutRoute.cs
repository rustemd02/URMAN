using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private static Vector3 HutPathStart => StopPoint(StopRoadFar + 1.1f, 1.2f);

    // Sample the actual walk from the shoulder to a stance just outside the moving door.
    private static Vector3 HutTrailPoint(float fraction)
    {
        var hut = PrologueHutPosition;
        var start = HutPathStart;
        var front = new Vector3(start.X - hut.X, 0f, start.Z - hut.Z).Normalized();
        var right = Vector3.Up.Cross(front);
        var step = hut + front * 2.7f - right * .95f;
        var at = start.Lerp(step, Mathf.Clamp(fraction, 0f, 1f));
        at.Y = ForestGroundHeight(at.X, at.Z);
        return at;
    }
}
