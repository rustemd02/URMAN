using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// UIUX-006 focused smoke: settings open menu-safely with keyboard entry
/// focus, explicit apply commits (and persists), and closing without apply
/// rolls the panel back to the live values without touching them.
/// </summary>
public partial class Act1SettingsNavigationSmokeTest : Node
{
    public override async void _Ready()
    {
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Settings navigation smoke could not instantiate the demo entrypoint.");
            return;
        }

        AddChild(demo);
        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var player = demo.DemoMain?.GetNodeOrNull<FirstPersonController>("Player");
        var settings = demo.DemoMain?.GetNodeOrNull<SettingsUi>("SettingsUi");
        if (player is null || settings is null || demo.MainMenu is null)
        {
            Fail("Settings navigation smoke could not find player, settings or menu.");
            return;
        }

        var storeBackup = System.IO.File.Exists(settingsStorePath())
            ? System.IO.File.ReadAllBytes(settingsStorePath())
            : null;
        UserSettingsStore.Delete();

        // 1) Menu-safe open: settings stack on the main menu with keyboard
        //    entry focus, gameplay stays gated.
        demo.MainMenu.SettingsButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        var fov = settings.GetNode<HSlider>("Screen/Panel/Layout/BodyScroll/Body/FovRow/Fov");
        if (!settings.IsOpen || !demo.MainMenuVisible || !player.ModalOpen || !fov.HasFocus()
            || settings.Layer <= demo.MainMenu.Layer)
        {
            Fail($"Settings did not open menu-safely with entry focus (open={settings.IsOpen} menu={demo.MainMenuVisible} modal={player.ModalOpen} focus={fov.HasFocus()}).");
            return;
        }

        // 2) Unapplied edits roll back: moving the slider without Apply and
        //    closing leaves the live FOV untouched; reopening shows it.
        var liveFov = player.CaptureSettings().FieldOfView;
        fov.Value = 88;
        settings._UnhandledInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = true,
            Echo = false
        });
        await Frames(2);
        if (settings.IsOpen
            || demo.MainMenu.SettingsButton?.HasFocus() != true
            || player.CaptureSettings().FieldOfView != liveFov
            || System.IO.File.Exists(settingsStorePath()))
        {
            Fail("Unapplied settings edits leaked into the live configuration.");
            return;
        }

        demo.MainMenu.SettingsButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (!settings.IsOpen || settings.GetNode<HSlider>("Screen/Panel/Layout/BodyScroll/Body/FovRow/Fov").Value != liveFov)
        {
            Fail("Reopened settings did not show the live values after rollback.");
            return;
        }

        // 3) Explicit apply commits: live FOV changes and persists to the
        //    user store, while the main menu remains beneath.
        settings.GetNode<HSlider>("Screen/Panel/Layout/BodyScroll/Body/FovRow/Fov").Value = 88;
        settings.GetNode<Button>("Screen/Panel/Layout/Buttons/Apply").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (player.CaptureSettings().FieldOfView != 88
            || UserSettingsStore.TryLoad()?.FieldOfView != 88
            || !demo.MainMenuVisible)
        {
            Fail("Applied settings did not commit live and persist.");
            return;
        }

        GD.Print("act1-settings-navigation: PASS menu-safe open + entry focus + rollback without apply + explicit apply persists");
        RestoreStore(storeBackup);
        UserSettingsStore.Delete();
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private static string settingsStorePath() =>
        ProjectSettings.GlobalizePath("user://settings.json");

    private static void RestoreStore(byte[]? payload)
    {
        if (payload is null)
        {
            return;
        }

        System.IO.File.WriteAllBytes(settingsStorePath(), payload);
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
