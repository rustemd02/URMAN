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

        // Deterministic start: no Continue sources at all.
        DeleteSlot(MainMenuUi.ContinueSlot);
        DeleteSlot(MainMenuUi.CheckpointSlot);

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

        // SAVE-004: writing the rolling checkpoint makes Continue available;
        // pressing it restores that exact session.
        if (!await bridge.SaveSlotAsync(MainMenuUi.CheckpointSlot))
        {
            Fail("Main menu smoke could not write the checkpoint slot.");
            return;
        }

        var expectedZone = bridge.CurrentZoneId;
        demo.MainMenu?.SetContinueAvailable(true);
        if (continueButton.Visible != true)
        {
            Fail("Continue button stayed hidden with a checkpoint slot present.");
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

        // After a menu choice the onboarding intro is shown (by design) and
        // the menu itself is gone.
        if (!demo.IntroVisible || demo.MainMenuVisible)
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
