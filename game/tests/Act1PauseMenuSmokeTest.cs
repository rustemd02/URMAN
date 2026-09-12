using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// UIUX-005 focused smoke: the pause shell opens from the pause action during
/// gameplay, gates input, stacks the settings panel, saves/loads through the
/// bridge, requires confirmation for restart, resumes cleanly, and returns to
/// the main menu — with mouse/modal state restored at every step.
/// </summary>
public partial class Act1PauseMenuSmokeTest : Node
{
    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn");
        var demo = packed?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Pause smoke could not instantiate the demo entrypoint.");
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
            Fail("Pause smoke could not find bridge, player or settings UI.");
            return;
        }

        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Pause smoke could not start through the main menu.");
            return;
        }

        demo._UnhandledInput(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        await Frames(2);
        if (demo.IntroVisible || player.ModalOpen)
        {
            Fail("Pause smoke could not dismiss the intro before pausing.");
            return;
        }

        var pause = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi;
        var journal = demo.DemoMain!.GetNode<JournalUi>("JournalUi");
        journal.Open(bridge);
        if (await TryPause() || journal.GetNode<Control>("Screen").Visible || player.ModalOpen)
        {
            Fail("Escape must close the journal without opening pause over it.");
            return;
        }

        if (pause is null || !await TryPause())
        {
            Fail("Pause smoke could not open the pause shell with the pause action.");
            return;
        }

        if (!pause.IsOpen || !player.ModalOpen)
        {
            Fail("Pause shell did not gate input while open.");
            return;
        }

        pause.SaveButton?.EmitSignal(BaseButton.SignalName.Pressed);
        var saved = false;
        for (var frame = 0; frame < 300 && !saved; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            saved = bridge.HasLoadableSlot(PauseMenuUi.ContinueSlot);
        }

        if (!saved)
        {
            Fail("Pause save action did not write the continue slot.");
            return;
        }

        pause.LoadButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(4);
        if (pause.IsOpen || player.ModalOpen)
        {
            Fail("Pause load action did not resume gameplay.");
            return;
        }

        if (!await TryPause() || !pause.IsOpen)
        {
            Fail("Pause smoke could not reopen the pause shell.");
            return;
        }

        pause.SettingsButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (!settings.IsOpen || !pause.IsOpen)
        {
            Fail("Settings did not stack on top of the open pause shell.");
            return;
        }

        if (!await TryPause())
        {
            Fail("Pause key did not close the stacked settings panel.");
            return;
        }

        if (settings.IsOpen || !pause.IsOpen || !player.ModalOpen)
        {
            Fail($"Closing stacked settings failed: settingsOpen={settings.IsOpen} pauseOpen={pause.IsOpen} modal={player.ModalOpen}");
            return;
        }

        pause.ResumeButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (pause.IsOpen || player.ModalOpen)
        {
            Fail("Resume did not release gameplay input.");
            return;
        }

        if (!await TryPause())
        {
            Fail("Pause smoke could not reopen the shell for the restart check.");
            return;
        }

        pause.RestartButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (!pause.IsOpen || player.ModalOpen != true)
        {
            Fail("Restart confirmation state broke the pause shell.");
            return;
        }

        pause.RestartButton?.EmitSignal(BaseButton.SignalName.Pressed);
        var reset = false;
        for (var frame = 0; frame < 900 && !reset; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            reset = bridge.CurrentZoneId == "village_day" && !pause.IsOpen && !player.ModalOpen;
        }

        if (!reset)
        {
            Fail("Confirmed restart did not reset to the arrival session and resume.");
            return;
        }

        if (!await TryPause())
        {
            Fail("Pause smoke could not reopen the shell for the main-menu check.");
            return;
        }

        pause.MainMenuButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        pause.MainMenuButton?.EmitSignal(BaseButton.SignalName.Pressed);
        var backToMenu = false;
        for (var frame = 0; frame < 300 && !backToMenu; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            backToMenu = demo.MainMenuVisible && !pause.IsOpen;
        }

        if (!backToMenu || !player.ModalOpen)
        {
            Fail("Main menu return did not show the menu and gate gameplay.");
            return;
        }

        GD.Print("act1-pause-menu: PASS pause gate + save/load + settings stack + resume + confirmed restart + menu return");
        DeleteSlot();
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private async Task<bool> TryPause()
    {
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = true,
            Echo = false
        });
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = false,
            Echo = false
        });
        await Frames(2);
        var pause = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi;
        return pause is { IsOpen: true };
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void DeleteSlot()
    {
        foreach (var suffix in new[] { ".savegame-v3.json", ".savegame-v3.backup.json" })
        {
            var path = ProjectSettings.GlobalizePath($"user://savegames/{PauseMenuUi.ContinueSlot}{suffix}");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
