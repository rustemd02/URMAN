using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private MeshInstance3D? _restBenchSnow;
    private StaticBody3D? _restBenchCollision;
    private bool? _restBenchFound;
    private Tween? _restBenchBrush;

    private void BuildRoadsideDiscoveries()
    {
        // Before the cemetery boundary, on the road side of every grave marker.
        var at = new Vector3(-5f, AgentBAct1HeightField.CollisionGround(-5f, -52f), -52f);
        var bench = new Node3D { Name = "DiscoveryRestBench", Position = at };
        GetNode<Node3D>("Act1CoreWorldGreybox/ZiratMemoryField").AddChild(bench);
        for (var i = 0; i < 3; i++)
        {
            AddVisualBox(bench, $"SeatPlank{i}", new(1.8f, .065f, .145f), new(0, .46f, -.155f + i * .155f),
                i == 1 ? "b29c77" : "655648", "wood_furniture");
            foreach (var x in new[] { -.69f, .69f })
                DiscoveryCylinder(bench, $"Nail{i}_{x}", .006f, .006f, .003f,
                    new(x, .494f, -.155f + i * .155f), "655f53");
        }
        foreach (var x in new[] { -.69f, .69f })
        {
            AddVisualBox(bench, $"Foot{x}", new(.11f, .45f, .47f), new(x, .225f, 0), "655648", "wood_furniture");
            AddVisualBox(bench, $"BackPost{x}", new(.075f, .58f, .075f), new(x, .61f, -.25f), "655648", "wood_furniture");
        }
        AddVisualBox(bench, "BackRest", new(1.85f, .19f, .055f), new(0, .79f, -.25f), "72614d", "wood_furniture");
        _restBenchSnow = AddVisualBox(bench, "SnowOnRepairedSeat", new(1.65f, .035f, .29f), new(0, .51f, 0), "dce6e9", "snow_ground");
        var village = (StyleBenchmarkZone)_zoneInstances["village_day"];
        DiscoveryTarget(village, "zirat-outer-rest-bench", new(1.85f, .14f, .48f), at + new Vector3(0, .48f, 0), false);
        _restBenchCollision = new StaticBody3D { Name = "RestBenchCollision", Position = village.ToLocal(at) };
        village.AddChild(_restBenchCollision);
        _restBenchCollision.AddChild(new CollisionShape3D
        {
            Name = "Seat", Position = new(0, .26f, 0),
            Shape = new BoxShape3D { Size = new(1.8f, .52f, .47f) }
        });
        _restBenchCollision.AddChild(new CollisionShape3D
        {
            Name = "Back", Position = new(0, .7f, -.25f),
            Shape = new BoxShape3D { Size = new(1.85f, .38f, .075f) }
        });
    }

    private void UpdateRoadsideDiscoveries(bool found)
    {
        var outside = ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";
        if (_restBenchCollision is not null) _restBenchCollision.CollisionLayer = outside ? 1u : 0u;
        if (_restBenchSnow is null || _restBenchFound == found) return;
        _restBenchBrush?.Kill();
        _restBenchSnow.Visible = !found || _restBenchFound == false;
        if (_restBenchFound == false && found && outside)
        {
            _restBenchBrush = CreateTween();
            _restBenchBrush.TweenProperty(_restBenchSnow, "scale:x", .001f, .5);
            _restBenchBrush.TweenCallback(Callable.From(() => _restBenchSnow.Visible = false));
        }
        else
        {
            _restBenchSnow.Scale = new(found ? .001f : 1f, 1f, 1f);
            _restBenchSnow.Visible = !found;
        }
        _restBenchFound = found;
    }
}
