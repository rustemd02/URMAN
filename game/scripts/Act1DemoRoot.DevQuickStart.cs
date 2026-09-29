using Godot;

namespace Urman.Godot;

// Development only, for fast iteration on the prologue:
//   URMAN_START=ride | forest   skips the menu and starts that part at once.
//   F5 (debug builds only)       reloads the scene and replays the current part.
//   F6 (debug builds only)       reloads the scene and starts from the forest.
// Exported release builds are not debug builds, so neither key exists there, and
// without the variable a normal run is unchanged.
public partial class Act1DemoRoot
{
    private static string? _devRestartSection;

    private async void DevQuickStartBoot()
    {
        var section = _devRestartSection ?? System.Environment.GetEnvironmentVariable("URMAN_START");
        _devRestartSection = null;
        if (section is not ("ride" or "forest") || !string.IsNullOrEmpty(DevRideCaptureDir)) return;
        for (var frame = 0; frame < 600 && !(MainMenuVisible && _main is not null && _player is not null); frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!MainMenuVisible) return;
        GD.Print($"dev-quick-start: {section}");
        if (section == "forest")
        {
            await ShowIntroAfterMenuAsync();
            return;
        }
        _mainMenu?.Dismiss();
        _mainMenu = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        BuildPrologueOverlay();
        await RunPrologueNivaRideAsync();
        FadePrologueBlackout(visible: false);
        await PrologueWaitAsync(.3);
        ReleasePrologueOverlay();
    }

    private bool DevQuickKey(InputEvent inputEvent)
    {
        if (!OS.IsDebugBuild() || inputEvent is not InputEventKey { Pressed: true, Echo: false } key) return false;
        if (key.Keycode is not (Key.F5 or Key.F6)) return false;
        _devRestartSection = key.Keycode == Key.F6 ? "forest" : _rideCamera is not null ? "ride" : "forest";
        GD.Print($"dev-quick-start: reload -> {_devRestartSection}");
        PrologueVoice.Stop();
        GetViewport().SetInputAsHandled();
        GetTree().ReloadCurrentScene();
        return true;
    }
}
