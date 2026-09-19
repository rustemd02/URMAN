using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal sealed record FacilityPerformanceView(string SampleId, string InteriorId, Node3D Room,
        Vector3 StandingCandidate, Vector3 AimPoint, Node3D ViewSubject, StaticBody3D FloorOwner);

    internal static bool IsFacilityPerformanceSample(string sample) => sample is
        "village_day@school_classroom" or "village_day@council_hall" or "village_day@mosque_hall";

    /// <summary>Read the actual built room owners for explicit performance placement.
    /// These are diagnostic views, not new gameplay spawns or acquired knowledge.</summary>
    internal bool TryGetFacilityPerformanceView(string sample, out FacilityPerformanceView? view)
    {
        view = null;
        if (!IsBuilt || !IsFacilityPerformanceSample(sample)) return false;
        if (sample == "village_day@mosque_hall")
        {
            if (_mosqueRoom is null
                || _mosqueRoom.GetNodeOrNull<StaticBody3D>("MosquePrayerCarpetBody") is not { } floor
                || _mosqueRoom.GetNodeOrNull<Node3D>("Npc_timur_hazrat") is not { } timur) return false;
            view = new(sample, "mosque", _mosqueRoom, _mosqueRoom.ToGlobal(new(1.20f, .06f, 0)),
                timur.GlobalPosition + Vector3.Up * 1.20f, timur, floor);
            return true;
        }
        var id = sample == "village_day@school_classroom" ? "school" : "council";
        var building = _publicBuildings.SingleOrDefault(room => room.Id == id);
        if (building is null || building.Room.GetNodeOrNull<StaticBody3D>("TimberFloorBody") is not { } timber)
            return false;
        var subject = building.Room.GetNodeOrNull<Node3D>(id == "school" ? "Chalkboard" : "ClubStageDeck");
        if (subject is null) return false;
        var localFeet = id == "school" ? new Vector3(-.07f, .06f, .76f) : new Vector3(1.10f, .06f, -.73f);
        var aim = id == "school" ? building.Room.ToGlobal(new(-.90f, 1.10f, 1.60f))
            : subject.GlobalPosition + Vector3.Up;
        view = new(sample, id, building.Room, building.Room.ToGlobal(localFeet), aim, subject, timber);
        return true;
    }
}
