namespace Urman.Godot;

// Metres in the existing house_old_pc frame. Keep the entrance, story room
// and full-width stairwell fixed while trimming the unused western strip.
internal static class BabaiHousePlan
{
    public const float West = -5.3f;
    public const float East = 6.5f;
    public const float Back = -7.5f;
    public const float Front = 3.5f;
    public const float Width = East - West;
    public const float Depth = Front - Back;
    public const float CentreX = (West + East) * .5f;
    public const float CentreZ = (Back + Front) * .5f;
    public const float WallWest = West - .1f;
    public const float WallEast = East + .1f;
    public const float WallBack = Back - .1f;
    public const float WallFront = Front + .1f;
    public const float Eave = 7.4f;
    public const float Ridge = 9.9f;
    public const float RoofHalfRun = Width * .5f + .55f;
}
