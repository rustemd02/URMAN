using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// UIUX-001 + SAVE-004 focused smoke: the public build opens into the main
/// menu with gameplay input gated; Continue is hidden without any Continue
/// source, becomes available once a checkpoint exists, and restoring lands
/// the session exactly; Settings opens from the menu.
/// </summary>
public partial class Act1MainMenuSmokeTest : Node
{
    public override async void _Ready()
    {
        DeleteSlot(MainMenuUi.ContinueSlot);
        DeleteSlot(MainMenuUi.CheckpointSlot);
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Main menu smoke could not instantiate the demo entrypoint.");
            return;
        }

        AddChild(demo);
        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = demo.DemoMain?.GetNodeOrNull<FirstPersonController>("Player");
        var settings = demo.DemoMain?.GetNodeOrNull<SettingsUi>("SettingsUi");
        if (bridge is null || player is null || settings is null)
        {
            Fail("Main menu smoke could not find bridge, player or settings UI.");
            return;
        }

        // Fresh profile: menu gates the demo, Continue hidden, no intro yet.
        if (!demo.MainMenuVisible
            || demo.IntroVisible
            || !player.ModalOpen
            || demo.MainMenu?.ContinueButton is not { } continueButton
            || continueButton.Visible
            || demo.MainMenu.NewGameButton is not { } newGameButton)
        {
            Fail("Main menu did not gate the fresh demo start with a hidden Continue.");
            return;
        }

        bridge._UnhandledInput(new InputEventAction { Action = "quick_save", Pressed = true });
        await Frames(2);
        if (bridge.HasLoadableSlot(MainMenuUi.ContinueSlot))
        { Fail("Quick-save hotkey wrote a menu-only session."); return; }

        // Settings opens from the menu and closes without leaving the menu.
        demo.MainMenu.SettingsButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (!settings.IsOpen)
        {
            Fail("Main menu Settings action did not open the settings UI.");
            return;
        }

        settings.Close();
        await Frames(2);
        if (settings.IsOpen || !demo.MainMenuVisible)
        {
            Fail("Settings did not return to the main menu.");
            return;
        }

        demo.MainMenu.AboutButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(1);
        await Capture("credits");
        demo.MainMenu._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        if (demo.MainMenu.AboutButton?.HasFocus() != true)
        { Fail("Credits did not return focus to the menu."); return; }
        await Capture("main_menu");
        demo.MainMenu.ApplyAccessibilitySettings(new(TextScale: 1.6));
        await Capture("main_menu_large");
        demo.MainMenu.AboutButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Capture("credits_large");
        demo.MainMenu._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });

        if (!await bridge.SaveSlotAsync(MainMenuUi.ContinueSlot)) return;
        System.IO.File.SetLastWriteTimeUtc(ProjectSettings.GlobalizePath("user://savegames/quick.savegame-v3.json"), DateTime.UtcNow.AddHours(-1));

        // SAVE-004: writing the rolling checkpoint makes Continue available;
        // pressing it restores that exact session.
        if (!await bridge.SaveSlotAsync(MainMenuUi.CheckpointSlot))
        {
            Fail("Main menu smoke could not write the checkpoint slot.");
            return;
        }

        if ((await bridge.FindContinueAsync())?.Slot != MainMenuUi.CheckpointSlot)
        { Fail("An older quick-save hid the newer checkpoint."); return; }
        var expectedZone = bridge.CurrentZoneId;
        await GodotSmokeCleanup.ReleaseAsync(demo);
        demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
        AddChild(demo);
        for (var frame = 0; frame < 900 && demo.MainMenu?.ContinueButton?.Visible != true; frame++) await Frames(1);
        bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        continueButton = demo.MainMenu?.ContinueButton;
        if (bridge is null || continueButton?.Visible != true)
        {
            Fail("Cold-start Continue stayed hidden with a valid checkpoint.");
            return;
        }

        // Drift the live session away so the restore is observable.
        demo.DemoMain?.SwitchZone("kara_urman_night", "village_path");
        await Frames(2);

        continueButton.EmitSignal(BaseButton.SignalName.Pressed);
        var frames = 900;
        while (bridge.CurrentZoneId != expectedZone && frames-- > 0)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (bridge.CurrentZoneId != expectedZone)
        {
            Fail("Continue did not restore the checkpoint session zone.");
            return;
        }

        // Continue returns directly to the saved scene without arrival onboarding.
        if (demo.IntroVisible || demo.MainMenuVisible)
        {
            Fail($"Continue restore left an inconsistent state: intro={demo.IntroVisible} menu={demo.MainMenuVisible}");
            return;
        }

        GD.Print("act1-main-menu: PASS menu gate + settings from menu + checkpoint-based Continue restore");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private static void DeleteSlot(string slot)
    {
        foreach (var suffix in new[] { ".savegame-v3.json", ".savegame-v3.backup.json" })
        {
            var path = ProjectSettings.GlobalizePath($"user://savegames/{slot}{suffix}");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    private async Task Capture(string name)
    {
        var directory = OS.GetEnvironment("URMAN_UI_SHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        await Frames(3);
        RenderingServer.ForceDraw(false);
        using var image = GetViewport().GetTexture().GetImage();
        if (image is null || image.SavePng(System.IO.Path.Combine(directory, name + ".png")) != Error.Ok)
            Fail("Could not capture the actual main-menu state.");
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
