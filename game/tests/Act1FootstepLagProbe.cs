using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// ACT1-FOOTSTEP.1 diagnostic: walks the arrival street and then off-road,
/// recording per-physics-tick footstep triggers, real cadence intervals and
/// the SnowTrampleField CPU metas. Produces one JSON receipt so the off-road
/// step lag is diagnosed from data before the fix, then re-measured after.
/// </summary>
public partial class Act1FootstepLagProbe : Node
{
    private sealed record Sample(int Tick, bool StepStarted, bool OnRoad, float RoadDistance,
        float Speed, double UpdateMs, double RedrawMs, double MeshMs, int RebuiltTriangles, int Stamps);

    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Footstep lag probe could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        for (var index = 0; index < 10; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var controller = GetTree().GetFirstNodeInGroup("footstep_audio") as FootstepAudioController;
        var field = Descendants(main).OfType<SnowTrampleField>().FirstOrDefault();
        if (player is null || controller is null || field is null)
        {
            Fail("Footstep lag probe could not find player, controller or snow trample field.");
            return;
        }

        player.SetPhysicsProcess(true);
        Input.ActionPress("move_forward");
        var samples = new List<Sample>();
        var lastTriggers = 0;
        var windowSpeeds = new List<float>();
        var windowRoad = new List<float>();
        var reachedOffRoad = false;
        var ticks = 0;
        while (ticks < 1500)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            ticks++;
            var position = new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z);
            var road = AgentBAct1HeightField.RoadInfo(position.X, position.Y);
            var speed = new Vector2(player.Velocity.X, player.Velocity.Z).Length();
            var triggers = controller.StepTriggerCount;
            var sample = new Sample(ticks, triggers > lastTriggers, road.Distance < road.HalfWidth + .6f,
                (float)road.Distance, speed,
                field.GetMeta("snowTrampleUpdateMs", 0f).AsDouble(),
                field.GetMeta("snowTrampleRedrawMs", 0f).AsDouble(),
                field.GetMeta("snowTrampleMeshMs", 0f).AsDouble(),
                field.GetMeta("snowTrampleRebuiltTriangles", 0).AsInt32(),
                field.GetMeta("snowTrampleStampCount", 0).AsInt32());
            samples.Add(sample);
            if (!sample.OnRoad) reachedOffRoad = true;
            lastTriggers = triggers;
            windowSpeeds.Add(speed);
            windowRoad.Add((float)road.Distance);

            if (ticks % 45 != 0) continue;
            var meanSpeed = windowSpeeds.Average();
            var meanRoad = windowRoad.Average();
            windowSpeeds.Clear();
            windowRoad.Clear();
            var yaw = player.RotationDegrees.Y;
            // Before leaving the street: sweep the heading until the feet
            // actually leave the travelled surface, then keep straight.
            if (!reachedOffRoad && ticks > 150
                && meanRoad < AgentBAct1HeightField.RoadInfo(position.X, position.Y).HalfWidth + .6f)
                player.RotationDegrees = new Vector3(0, yaw + 30f, 0);
            else if (meanSpeed < .8f) player.RotationDegrees = new Vector3(0, yaw + 97f, 0);
        }

        Input.ActionRelease("move_forward");
        var onRoad = samples.Where(sample => sample.OnRoad).ToList();
        var offRoad = samples.Where(sample => !sample.OnRoad).ToList();
        var onSteps = StepSummary(onRoad);
        var offSteps = StepSummary(offRoad);
        if (onSteps.steps < 6 || offSteps.steps < 6)
        {
            Fail($"Footstep lag probe walked too few steps: onRoad={onSteps.steps}, offRoad={offSteps.steps}, "
                 + $"triggers={controller.StepTriggerCount}, stamps={field.GetMeta("snowTrampleStampCount", 0).AsInt32()}, "
                 + $"end={player.GlobalPosition}.");
            return;
        }

        var receipt = new
        {
            probe = "act1-footstep-lag-probe",
            ticks,
            player_end = new { player.GlobalPosition.X, player.GlobalPosition.Z },
            on_road = Segment(onRoad, onSteps),
            off_road = Segment(offRoad, offSteps),
            note = "update/redraw/mesh are SnowTrampleField main-thread CPU ms metas; step gap = step interval above 2x median"
        };
        GD.Print("act1-footstep-lag-probe: " + JsonSerializer.Serialize(receipt));
        GD.Print("act1-footstep-lag-probe: PASS on/off-road cadence and snow CPU data collected");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }

    private static (int steps, List<int> intervals) StepSummary(List<Sample> segment)
    {
        var stepTicks = segment.Where(sample => sample.StepStarted).Select(sample => sample.Tick).ToList();
        var intervals = new List<int>();
        for (var index = 1; index < stepTicks.Count; index++)
            intervals.Add(stepTicks[index] - stepTicks[index - 1]);
        return (stepTicks.Count, intervals);
    }

    private static object Segment(List<Sample> segment, (int steps, List<int> intervals) steps)
    {
        var median = steps.intervals.Count > 0 ? Median(steps.intervals) : 0;
        return new
        {
            samples = segment.Count,
            steps = steps.steps,
            step_interval_ticks = steps.intervals.Count > 0
                ? new { median, min = steps.intervals.Min(), max = steps.intervals.Max() }
                : null,
            missed_step_gaps = steps.intervals.Count(gap => gap > median * 2),
            mean_speed = segment.Count > 0 ? segment.Average(sample => sample.Speed) : 0,
            snow_cpu_ms = new
            {
                update_mean = segment.Count > 0 ? segment.Average(sample => sample.UpdateMs) : 0,
                update_max = segment.Count > 0 ? segment.Max(sample => sample.UpdateMs) : 0,
                redraw_mean = segment.Count > 0 ? segment.Average(sample => sample.RedrawMs) : 0,
                mesh_mean = segment.Count > 0 ? segment.Average(sample => sample.MeshMs) : 0,
                rebuilt_triangles_max = segment.Count > 0 ? segment.Max(sample => sample.RebuiltTriangles) : 0
            }
        };
    }

    private static int Median(List<int> values)
    {
        var sorted = values.OrderBy(value => value).ToList();
        return sorted[sorted.Count / 2];
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
