using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// UIUX-001 focused smoke: the public build opens into the main menu with
/// gameplay input gated; Continue is hidden without a quick slot and, once
/// one exists, the production Continue button restores the session; Settings
/// opens from the menu; the menu carries no runtime/session owner of its own.
/// </summary>
public partial class Act1MainMenuSmokeTest : Node
{
    private const string ContinueSlot = MainMenuUi.ContinueSlot;

    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn");
        var demo = packed?.Instantiate<Act1DemoRoot>();
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

        var hadQuickSlot = bridge.HasLoadableSlot(ContinueSlot);
        var quickBackup = hadQuickSlot
            ? System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath($"user://savegames/{ContinueSlot}.savegame-v3.json"))
            : [];
        DeleteSlot();

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
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!settings.IsOpen)
        {
            Fail("Main menu Settings action did not open the settings UI.");
            return;
        }

        settings.Close();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (settings.IsOpen || !demo.MainMenuVisible)
        {
            Fail("Settings did not return to the main menu.");
            return;
        }

        // A quick slot appears: Continue becomes available and the production
        // Continue button restores the session (arrival village, gate opens).
        if (!await bridge.SaveSlotAsync(ContinueSlot))
        {
            Fail("Main menu smoke could not write the continue slot.");
            return;
        }

        demo.MainMenu.SetContinueAvailable(bridge.HasLoadableSlot(ContinueSlot));
        if (!continueButton.Visible)
        {
            Fail("Continue button stayed hidden after a quick save.");
            return;
        }

        continueButton.EmitSignal(BaseButton.SignalName.Pressed);
        var frames = 900;
        while (!demo.IntroVisible && frames-- > 0)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!demo.IntroVisible
            || demo.MainMenuVisible
            || bridge.CurrentZoneId != "village_day"
            || !player.ModalOpen)
        {
            Fail($"Continue did not restore the session: intro={demo.IntroVisible} menu={demo.MainMenuVisible} zone={bridge.CurrentZoneId} modal={player.ModalOpen}");
            // The intro card itself is a modal: the player must stay gated
            // until it is dismissed, which is asserted by IntroVisible.
            return;
        }

        GD.Print("act1-main-menu: PASS menu gate + hidden Continue on fresh profile + settings from menu + continue restores session");
        RestoreQuickSlot(hadQuickSlot, quickBackup);
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private static void DeleteSlot()
    {
        var path = ProjectSettings.GlobalizePath($"user://savegames/{ContinueSlot}.savegame-v3.json");
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }

        var backup = ProjectSettings.GlobalizePath($"user://savegames/{ContinueSlot}.savegame-v3.backup.json");
        if (System.IO.File.Exists(backup))
        {
            System.IO.File.Delete(backup);
        }
    }

    private static void RestoreQuickSlot(bool existed, byte[]? payload)
    {
        if (!existed)
        {
            DeleteSlot();
            return;
        }

        var path = ProjectSettings.GlobalizePath($"user://savegames/{ContinueSlot}.savegame-v3.json");
        System.IO.File.WriteAllBytes(path, payload!);
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
