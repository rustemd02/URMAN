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

        // Acceptance row 23 covers the package's licenses and instructions. The
        // credits screen is the only place those attributions reach a player, so
        // assert its content instead of only its reachability: every third-party
        // family the shipped assets actually use must be named, and the Godot
        // license action must produce the engine's own license text.
        var about = demo.MainMenu.FindChild("About", recursive: true, owned: false) as Control;
        var creditsLabel = about?.GetNodeOrNull<RichTextLabel>("Credits");
        var licensesButton = about?.GetNodeOrNull<Button>("Licenses");
        if (about is null || creditsLabel is null || licensesButton is null)
        {
            Fail("The credits screen is missing its text body or the Godot licenses action.");
            return;
        }

        var creditsText = creditsLabel.Text;
        foreach (var attribution in new[]
                 {
                     "Quaternius", "Universal Base Characters", "Stylized Nature MegaKit",
                     "Corsica_S", "Iwan Gabovitch", "Kenney", "CC0 1.0",
                     "callmethefoo", "RIFORKA", "bruno.auzet", "lwdickens",
                     "Magnesus", "soundofsong", "Godot Engine", "Blender", "ImageGen"
                 })
        {
            if (!creditsText.Contains(attribution, StringComparison.Ordinal))
            {
                Fail($"The shipped credits do not name the '{attribution}' asset family it uses.");
                return;
            }
        }

        if (licensesButton.Disabled)
        {
            Fail("The Godot licenses action is disabled before it was ever used.");
            return;
        }

        licensesButton.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(1);
        var licenseText = creditsLabel.Text;
        if (licenseText == creditsText
            || licenseText.Length < creditsText.Length
            || !licenseText.Contains("MIT", StringComparison.Ordinal)
            || !licenseText.Contains("Copyright", StringComparison.Ordinal)
            || !licensesButton.Disabled)
        {
            Fail("The Godot licenses action did not replace the credits with the engine license text.");
            return;
        }
        GD.Print($"act1-main-menu: credits name 16 attribution families; Godot license action returned {licenseText.Length} chars of engine license text");

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

        // M7 error state: when a save exists on disk but none is loadable (for
        // example a save from an older campaign fingerprint), the menu must say
        // so truthfully and reassure that the files were left alone. This checks
        // the rendered message only; the safe-failure behaviour itself is covered
        // by the engine-independent core store tests, and provoking a real load
        // error here would put an expected ERROR line into the fail-closed log.
        demo.MainMenu?.SetContinueAvailable(available: false, description: null, existingSavePresent: true);
        await Frames(2);
        var continueHint = demo.MainMenu?.GetNodeOrNull<Label>(
            "Screen/Panel/Layout/ContinueUnavailable") ?? demo.MainMenu?.FindChild("ContinueUnavailable", true, false) as Label;
        if (continueHint is null
            || !continueHint.Text.Contains("не удалось загрузить подходящее сохранение", StringComparison.Ordinal)
            || !continueHint.Text.Contains("Файлы сохранений оставлены без изменений", StringComparison.Ordinal))
        {
            Fail($"The menu did not explain an unloadable save truthfully: '{(continueHint?.Text ?? "<missing label>")}'.");
            return;
        }

        // Exit affordance: the Quit button must exist, be reachable and be wired to
        // a handler. Actually pressing it is deliberately not exercised here because
        // that would terminate the test process, so "exit really closes the app"
        // stays a human observation; the wiring is checked without pressing.
        var quitButton = demo.MainMenu?.FindChild("QuitButton", true, false) as Button;
        if (quitButton is null || !quitButton.Visible || quitButton.Disabled)
        {
            Fail("The main menu does not offer a reachable Quit affordance.");
            return;
        }
        if (quitButton.GetSignalConnectionList(BaseButton.SignalName.Pressed).Count == 0)
        {
            Fail("The Quit affordance is not connected to any handler.");
            return;
        }

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

        GD.Print("act1-main-menu: PASS menu gate + settings from menu + checkpoint-based Continue restore + truthful unloadable-save message + wired Quit affordance");

        // Exit probe, and the last action of this run. The button captured before
        // Continue is disposed once the session is restored, so reach the menu the
        // way a player does - pause, then "В главное меню" - and press the fresh
        // exit affordance. A real exit closes the process from inside the handler,
        // so the Fail below is reachable only when the button did NOT close the
        // app. That turns "exit really closes the application" from a human
        // observation into a machine-checked one: the run exits 0 with the press
        // marker and without the post-press message when exit works, and exits 1
        // with the failure text when it does not.
        var pauseMenu = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi;
        if (pauseMenu?.MainMenuButton is null)
        {
            Fail("The exit probe could not reach the pause shell that leads back to the menu.");
            return;
        }

        for (var attempt = 0; attempt < 3 && !pauseMenu.IsOpen; attempt++)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true, Echo = false });
            await Frames(2);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false, Echo = false });
            await Frames(2);
            if (!pauseMenu.IsOpen)
            {
                for (var frame = 0; frame < 60 && !pauseMenu.IsOpen; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        if (!pauseMenu.IsOpen)
        {
            Fail("The exit probe could not open the pause shell from the restored session.");
            return;
        }

        pauseMenu.MainMenuButton.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        pauseMenu.MainMenuButton.EmitSignal(BaseButton.SignalName.Pressed);
        var backToMenu = false;
        for (var frame = 0; frame < 300 && !backToMenu; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            backToMenu = demo.MainMenuVisible && !pauseMenu.IsOpen;
        }

        if (!backToMenu)
        {
            Fail($"The exit probe could not return to the main menu: menu={demo.MainMenuVisible} pause={pauseMenu.IsOpen}");
            return;
        }

        var exitButton = demo.MainMenu?.FindChild("QuitButton", recursive: true, owned: false) as Button;
        if (exitButton is null || exitButton.Disabled)
        {
            Fail("The returned main menu does not offer an enabled exit affordance.");
            return;
        }

        GD.Print("act1-main-menu: pressing the menu exit affordance; the post-press marker can only appear if it failed to close");
        exitButton.EmitSignal(BaseButton.SignalName.Pressed);
        for (var frame = 0; frame < 30; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        Fail("The menu exit affordance did not close the application within 30 frames.");
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
