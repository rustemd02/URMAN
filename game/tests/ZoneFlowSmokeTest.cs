using Godot;

namespace Urman.Godot.Tests;

public partial class ZoneFlowSmokeTest : Node
{
    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn");
        var main = packed?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Zone flow could not instantiate main.");
            return;
        }

        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        foreach (var (zoneId, spawnPointId) in new[]
                 {
                     ("house_old_pc", "entry"),
                     ("kara_urman_night", "village_path"),
                     ("village_day", "from_forest")
                 })
        {
            var transitionStarted = Time.GetTicksUsec();
            main.SwitchZone(zoneId, spawnPointId);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var transitionMilliseconds = (Time.GetTicksUsec() - transitionStarted) / 1000.0;
            var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
            var zoneHost = main.GetNode<Node3D>("ZoneHost");
            if (bridge?.CurrentZoneId != zoneId || zoneHost.GetChildCount() != 1)
            {
                Fail($"Zone flow failed at {zoneId}@{spawnPointId}.");
                return;
            }

            GD.Print($"zone-transition-performance: {zoneId}@{spawnPointId} elapsed={transitionMilliseconds:F1}ms");
        }

        GD.Print("zone-flow-smoke: village_day -> house_old_pc -> kara_urman_night -> village_day");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
