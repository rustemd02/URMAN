using Godot;

namespace Urman.Godot.Tests;

public partial class PersistenceSmokeTest : Node
{
    private const string Slot = "godot-smoke";

    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn");
        if (packed is null)
        {
            Fail("Persistence smoke could not load the main scene.");
            return;
        }

        var main = packed.Instantiate();
        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (bridge is null || player is null)
        {
            Fail("Persistence smoke could not find runtime bridge or player.");
            return;
        }

        Cleanup();
        if (bridge.IsSlotAvailable(Slot))
        {
            Fail("Persistence smoke reported an available slot before saving.");
            return;
        }

        player.GlobalPosition = new Vector3(2.5f, 0.4f, -3.25f);
        if (!await bridge.SaveSlotAsync(Slot))
        {
            Fail("Persistence smoke could not write SaveGameV3.");
            return;
        }

        if (!bridge.IsSlotAvailable(Slot))
        {
            Fail("Persistence smoke did not report the slot after saving.");
            return;
        }

        var zoneManager = GetTree().GetFirstNodeInGroup("zone_manager") as Main;
        zoneManager?.SwitchZone("house_old_pc", "entry");
        player.GlobalPosition = new Vector3(99, 99, 99);
        if (!await bridge.LoadSlotAsync(Slot) || bridge.CurrentZoneId != "village_day" ||
            !player.GlobalPosition.IsEqualApprox(new Vector3(2.5f, 0.4f, -3.25f)))
        {
            Fail("Persistence smoke did not restore the player transform.");
            return;
        }

        if (!bridge.IsSlotAvailable(Slot))
        {
            Fail("Persistence smoke lost slot availability after loading.");
            return;
        }

        GD.Print("persistence-smoke: SaveGameV3 user:// roundtrip");
        // The persistence test instantiates the real painterly scene, so release
        // its nodes and cached shader materials before quitting to keep headless
        // leak diagnostics meaningful for the next smoke process.
        await GodotSmokeCleanup.ReleaseAsync(main);
        Cleanup();
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        Cleanup();
        GetTree().Quit(1);
    }

    private static void Cleanup()
    {
        var directory = ProjectSettings.GlobalizePath("user://savegames");
        foreach (var name in new[] { $"{Slot}.savegame-v3.json", $"{Slot}.savegame-v3.backup.json" })
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
