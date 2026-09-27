using Godot;
using Urman.Core.Persistence;
using System.Linq;

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
        if (System.Environment.GetEnvironmentVariable("URMAN_LANG_COLD_PAIR") == "1")
        {
            await VerifyColdLanguageStart();
            return;
        }

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

        // Accessibility choices must survive a cold launch too, not only a save
        // slot: someone who needs reduced motion should not have to set it again
        // every time they start the game. The store serialises the whole
        // snapshot, so this checks the part that has no other coverage here.
        player.ApplySettings(player.CaptureSettings() with
        {
            Accessibility = player.CaptureSettings().Accessibility with { ReducedMotion = true, HighContrast = true, TextScale = 1.25 }
        });
        var storedAccessibility = UserSettingsStore.TryLoad()?.Accessibility;
        if (storedAccessibility is null
            || !storedAccessibility.ReducedMotion
            || !storedAccessibility.HighContrast
            || Math.Abs(storedAccessibility.TextScale - 1.25) > 0.0001)
        {
            Fail($"User settings store did not persist accessibility preferences "
                + $"(reduced={storedAccessibility?.ReducedMotion} contrast={storedAccessibility?.HighContrast} scale={storedAccessibility?.TextScale}).");
            return;
        }

        player.ApplySettings(player.CaptureSettings() with
        {
            Accessibility = player.CaptureSettings().Accessibility with { ReducedMotion = false, HighContrast = false, TextScale = 1.0 }
        });
        var clearedAccessibility = UserSettingsStore.TryLoad()?.Accessibility;
        if (clearedAccessibility is null || clearedAccessibility.ReducedMotion || clearedAccessibility.HighContrast
            || Math.Abs(clearedAccessibility.TextScale - 1.0) > 0.0001)
        {
            Fail("Clearing an accessibility preference did not reach the settings store.");
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

    private async Task VerifyColdLanguageStart()
    {
        const string slot = "language-level-cold-story";
        var savePath = ProjectSettings.GlobalizePath($"user://savegames/{slot}.savegame-v3.json");
        if (!System.IO.File.Exists(savePath))
        {
            Fail("The first Godot process did not leave its story slot for the cold launch.");
            return;
        }
        var savedBytes = System.IO.File.ReadAllBytes(savePath);
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Cold language launch could not instantiate the ordinary Act I menu.");
            return;
        }
        AddChild(demo);
        await Frames(8);
        var player = demo.DemoMain?.GetNodeOrNull<FirstPersonController>("Player");
        var settings = demo.DemoMain?.GetNodeOrNull<SettingsUi>("SettingsUi");
        var bridge = demo.DemoMain?.GetNodeOrNull<RuntimeBridge>("RuntimeBridge");
        if (player?.TatarLanguageLevel != "fluent" || UserSettingsStore.TryLoad()?.TatarLanguageLevel != "fluent"
            || settings is null || bridge is null || demo.MainMenu?.SettingsButton is null)
        {
            Fail("A separate Godot process did not reload the applied fluent profile.");
            return;
        }
        demo.MainMenu.SettingsButton.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        var level = settings.GetNode<OptionButton>("Screen/Panel/Layout/BodyScroll/Body/TatarLanguageLevelRow/TatarLanguageLevel");
        if (!settings.IsOpen || level.Selected != 2)
        {
            Fail("Cold-launched Settings UI did not display the applied fluent level.");
            return;
        }
        settings._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await Frames(2);
        for (var frame = 0; frame < 120 && demo.MainMenu?.NewGameButton?.Disabled == true; frame++) await Frames(1);
        var newGame = demo.MainMenu?.NewGameButton;
        if (newGame is null || newGame.Disabled)
        {
            Fail("The cold-launched main menu did not make New Game available.");
            return;
        }
        newGame.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (demo.MainMenuVisible && newGame.Text == "Начать новую игру")
            newGame.EmitSignal(BaseButton.SignalName.Pressed);
        for (var frame = 0; frame < 240 && demo.MainMenuVisible; frame++) await Frames(1);
        if (demo.MainMenuVisible || !demo.IntroVisible || bridge.IsDebugSession)
        {
            Fail("Cold-launched New Game did not start the ordinary arrival session.");
            return;
        }
        var vocabulary = bridge.SelectRuntimeState().GetProperty("vocabulary");
        var guessed = vocabulary.EnumerateObject().Count(word => word.Value.GetProperty("status").GetString() == "guessed");
        var confirmed = vocabulary.EnumerateObject().Count(word => word.Value.GetProperty("status").GetString() == "confirmed");
        if (guessed != 12 || confirmed != 0 || player.TatarLanguageLevel != "fluent"
            || !System.IO.File.ReadAllBytes(savePath).SequenceEqual(savedBytes))
        {
            Fail($"Cold New Game vocabulary/profile or prior story slot changed (guessed={guessed}, confirmed={confirmed}).");
            return;
        }
        if (!await bridge.LoadSlotAsync(slot))
        {
            Fail("The prior story slot could not be loaded after the cold New Game.");
            return;
        }
        var restored = bridge.SelectRuntimeState().GetProperty("vocabulary");
        var oldGuessed = restored.EnumerateObject().Count(word => word.Value.GetProperty("status").GetString() == "guessed");
        if (restored.GetProperty("urman.chapter1:vocabulary/tt_babai").GetProperty("status").GetString() != "unknown"
            || oldGuessed >= guessed || player.TatarLanguageLevel != "fluent"
            || !System.IO.File.ReadAllBytes(savePath).SequenceEqual(savedBytes))
        {
            Fail("Loading the prior story lost its vocabulary or overwrote the current fluent profile.");
            return;
        }
        GD.Print($"act1-language-cold-verify: PASS pid={System.Environment.ProcessId} profile=fluent new-guessed={guessed} new-confirmed={confirmed} old-guessed={oldGuessed} old-babai=unknown slot-byte-exact=true");
        await GodotSmokeCleanup.ReleaseAsync(demo);
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
