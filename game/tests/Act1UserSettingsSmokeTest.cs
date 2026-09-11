using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// UIUX-007 / SAVE-006 focused smoke: user preferences persist across a cold
/// launch independently of story saves; a story restore preserves current
/// profile settings; deleting the store never touches narrative state.
/// </summary>
public partial class Act1UserSettingsSmokeTest : Node
{
    private const string StorySlot = "user-settings-story";

    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn");
        var main = packed?.Instantiate<Main>();
        if (main is null)
        {
            Fail("User settings smoke could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        await Frames(3);

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (bridge is null || player is null)
        {
            Fail("User settings smoke could not find bridge or player.");
            return;
        }

        player.SetPhysicsProcess(false);
        UserSettingsStore.Delete();
        DeleteStorySlot();

        // 1) Applying preferences persists them for the next cold launch and
        //    the payload contains no narrative fields (store schema holds the
        //    settings snapshot only).
        player.ApplySettings(player.CaptureSettings() with { FieldOfView = 82 });
        var stored = UserSettingsStore.TryLoad();
        if (stored is null || stored.FieldOfView != 82)
        {
            Fail($"User settings store did not persist the applied FOV (stored={stored?.FieldOfView}).");
            return;
        }

        // 2) A story save carries its own settings snapshot; restoring it
        //    must keep the latest live profile; story progress does not own preferences.
        if (!await bridge.SaveSlotAsync(StorySlot))
        {
            Fail("User settings smoke could not write the story slot.");
            return;
        }

        player.ApplySettings(player.CaptureSettings() with { FieldOfView = 70 });
        if (UserSettingsStore.TryLoad()?.FieldOfView != 70)
        {
            Fail("User settings store did not track the latest applied preferences.");
            return;
        }

        // Separation already proven above (store=70 while the slot held 82).
        // Restoring the slot leaves the latest profile at 70.
        if (!await bridge.LoadSlotAsync(StorySlot)
            || player.CaptureSettings().FieldOfView != 70
            || UserSettingsStore.TryLoad()?.FieldOfView != 70)
        {
            Fail("Story slot restore overwrote the current profile.");
            return;
        }

        // 3) Resetting the preferences store never touches narrative state:
        //    the story slot still restores its full saved session.
        UserSettingsStore.Delete();
        if (UserSettingsStore.TryLoad() is not null)
        {
            Fail("Deleted user settings store reappeared.");
            return;
        }

        player.ApplySettings(player.CaptureSettings() with { FieldOfView = 90 });
        if (!await bridge.LoadSlotAsync(StorySlot)
            || player.CaptureSettings().FieldOfView != 90
            || bridge.CurrentZoneId != "village_day")
        {
            Fail("Story restore broke after the preferences store reset.");
            return;
        }

        GD.Print("act1-user-settings: PASS cold persistence + story-slot settings separation + safe store reset");
        UserSettingsStore.Delete();
        DeleteStorySlot();
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private static void DeleteStorySlot()
    {
        foreach (var suffix in new[] { ".json", ".backup.json" })
        {
            var path = ProjectSettings.GlobalizePath($"user://savegames/{StorySlot}{suffix}");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
