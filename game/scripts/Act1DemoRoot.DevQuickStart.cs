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

    // URMAN_START=newgame: the real campaign start, exactly as the menu's «Новая игра» does it.
    private async void DevNewGameBoot()
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_START") != "newgame") return;
        for (var frame = 0; frame < 3600 && IsInsideTree(); frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_mainMenu is null && _playerSessionSelected) return;
            if (!MainMenuVisible || _main is null || _player is null || _menuBusy
                || _mainMenu is not { IsSaveLookupPending: false }) continue;
            GD.Print("dev-quick-start: newgame");
            await OnMenuStartSessionAsync(startNewGame: true);
        }
    }

    // URMAN_SHOT_DIR=<dir> [URMAN_SHOT_EVERY=sec] [URMAN_SHOT_SECONDS=sec]: once a session has
    // started, save a still of the whole game view on a timer and quit at the end. Development only.
    private async void DevTimedShotsBoot()
    {
        var dir = System.Environment.GetEnvironmentVariable("URMAN_SHOT_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        float Number(string name, float fallback) => float.TryParse(System.Environment.GetEnvironmentVariable(name),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
        var every = Number("URMAN_SHOT_EVERY", 2f);
        var total = Number("URMAN_SHOT_SECONDS", 90f);
        System.IO.Directory.CreateDirectory(dir);
        // macOS stops drawing a covered window, and a capture of it repeats one stale frame.
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, true);
        while (IsInsideTree() && !_playerSessionSelected) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var index = 0;
        for (var elapsed = 0f; elapsed < total && IsInsideTree(); elapsed += every)
        {
            await ToSignal(GetTree().CreateTimer(every), SceneTreeTimer.SignalName.Timeout);
            if (!IsInsideTree()) return;
            GetViewport().GetTexture().GetImage().SavePng($"{dir}/s{index++:000}.png");
        }
        GetTree().Quit();
    }

    private bool DevQuickKey(InputEvent inputEvent)
    {
        if (UsesPhotoWorldsCampaign || !OS.IsDebugBuild()
            || inputEvent is not InputEventKey { Pressed: true, Echo: false } key) return false;
        if (key.Keycode is not (Key.F5 or Key.F6)) return false;
        _devRestartSection = key.Keycode == Key.F6 ? "forest" : _rideCamera is not null ? "ride" : "forest";
        GD.Print($"dev-quick-start: reload -> {_devRestartSection}");
        PrologueVoice.Stop();
        GetViewport().SetInputAsHandled();
        GetTree().ReloadCurrentScene();
        return true;
    }
}
