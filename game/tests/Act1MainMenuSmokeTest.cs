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
        // The debug zone jump is gated by a file next to the saves: the menu entry
        // must exist while the file is present and disappear without it. Turn it on
        // before the menu is built so both states can be observed in one run.
        var debugFlag = ProjectSettings.GlobalizePath("user://debug-zones.enabled");
        System.IO.File.WriteAllText(debugFlag, "enabled by Act1MainMenuSmokeTest\n");
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

        // Debug zone jump: the entry exists only while the flag file exists. The jump
        // itself is exercised at the end of the run, from the menu the player returns to.
        var debugButton = demo.MainMenu.FindChild("DebugZonesButton", recursive: true, owned: false) as Button;
        if (!MainMenuUi.DebugZonesEnabled || debugButton is null)
        {
            Fail("The debug zone entry is missing while user://debug-zones.enabled exists.");
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
        // Exercise the production decoder/backup path with a malformed logical
        // ID, not a menu-only error fixture. The guard owns this test profile.
        if (!await bridge.SaveSlotAsync(MainMenuUi.CheckpointSlot)) return;
        var checkpointPath = ProjectSettings.GlobalizePath("user://savegames/checkpoint.savegame-v3.json");
        var damaged = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(checkpointPath))!;
        damaged["spawnPoint"] = "bad location";
        System.IO.File.WriteAllText(checkpointPath, damaged.ToJsonString());
        var recoveryCandidate = await bridge.FindContinueAsync();
        if (recoveryCandidate?.Slot != MainMenuUi.CheckpointSlot
            || !recoveryCandidate.Value.Description.Contains("резервная копия", StringComparison.Ordinal))
        { Fail("Continue did not describe the valid checkpoint backup behind a malformed primary."); return; }
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

        var beforeReplayHashes = PlayerSaveHashes();
        if (demo.MainMenu?.ReplayIntroButton is not { Visible: true } replayButton)
        { Fail("The boot menu has no explicit intro replay."); return; }
        replayButton.EmitSignal(BaseButton.SignalName.Pressed);
        for (var frame = 0; frame < 900 && !demo.IntroVisible; frame++) await Frames(1);
        if (!demo.IntroVisible || !bridge.IsDebugSession)
        { Fail("Intro replay did not open in an isolated session."); return; }
        demo._UnhandledInput(new InputEventAction { Action = "interact", Pressed = true });
        for (var frame = 0; frame < 900 && !demo.MainMenuVisible; frame++) await Frames(1);
        continueButton = demo.MainMenu?.ContinueButton;
        for (var frame = 0; frame < 900 && continueButton?.Visible != true; frame++) await Frames(1);
        if (demo.IntroVisible || !demo.MainMenuVisible || continueButton?.Visible != true
            || PlayerSaveHashes() != beforeReplayHashes)
        { Fail("Replay return changed player saves or lost Continue."); return; }

        // Refresh through the ordinary menu owner with unreadable files on disk.
        // Discovery handles this without dispatching a failed LoadSlot operation.
        var savedFiles = System.IO.Directory.EnumerateFiles(ProjectSettings.GlobalizePath("user://savegames"))
            .ToDictionary(path => path, path => (Bytes: System.IO.File.ReadAllBytes(path), Time: System.IO.File.GetLastWriteTimeUtc(path)));
        foreach (var path in savedFiles.Keys) System.IO.File.WriteAllText(path, "{damaged");
        var damagedHashes = PlayerSaveHashes();
        demo.CallDeferred("RefreshMenuContinueAvailability");
        for (var frame = 0; frame < 300 && continueButton.Visible; frame++) await Frames(1);
        var continueHint = demo.MainMenu?.GetNodeOrNull<Label>(
            "Screen/Panel/Layout/ContinueUnavailable") ?? demo.MainMenu?.FindChild("ContinueUnavailable", true, false) as Label;
        if (continueButton.Visible || continueHint is null
            || !continueHint.Text.Contains("не удалось загрузить подходящее сохранение", StringComparison.Ordinal)
            || !continueHint.Text.Contains("Файлы сохранений оставлены без изменений", StringComparison.Ordinal))
        {
            Fail($"The menu did not explain an unloadable save truthfully: '{(continueHint?.Text ?? "<missing label>")}'.");
            return;
        }
        if (await bridge.FindContinueAsync() is not null || PlayerSaveHashes() != damagedHashes)
        { Fail("Unrecoverable menu discovery offered a save or changed its files."); return; }
        await Capture("main_menu_damaged_saves");
        foreach (var (path, saved) in savedFiles)
        {
            System.IO.File.WriteAllBytes(path, saved.Bytes);
            System.IO.File.SetLastWriteTimeUtc(path, saved.Time);
        }
        demo.CallDeferred("RefreshMenuContinueAvailability");
        for (var frame = 0; frame < 300 && !continueButton.Visible; frame++) await Frames(1);
        if (!continueButton.Visible || !continueHint.Text.Contains("резервная копия", StringComparison.Ordinal))
        { Fail("Menu did not restore Continue and its backup label after a working backup returned."); return; }
        await Capture("main_menu_backup_recovery");

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
        while (demo.MainMenuVisible && frames-- > 0)
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

        GD.Print("act1-main-menu: PASS menu gate + settings from menu + malformed-primary backup Continue restore + real unreadable files preserved + truthful backup/error labels + wired Quit affordance");

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

        // Debug zone jump, exercised from the menu the exit probe just returned to:
        // it must place the session in the chosen zone and grant no progression.
        var regularSaveHashes = PlayerSaveHashes();
        var regularKnowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var debugEntry = demo.MainMenu?.FindChild("DebugZonesButton", recursive: true, owned: false) as Button;
        if (debugEntry is null)
        {
            Fail("The rebuilt main menu lost the debug zone entry while its flag file exists.");
            return;
        }

        debugEntry.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        var zonesPanel = demo.MainMenu?.FindChild("DebugZones", recursive: true, owned: false) as Control;
        var karaEntry = zonesPanel?.FindChild("DebugZone_kara_urman_night_village_path", recursive: true, owned: false) as Button;
        if (zonesPanel is null || karaEntry is null)
        {
            Fail("The debug zone panel is missing its night Kara-Urman entry.");
            return;
        }

        karaEntry.EmitSignal(BaseButton.SignalName.Pressed);
        for (var frame = 0; frame < 240 && demo.MainMenuVisible; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var world = demo.DemoMain?.ConnectedWorld;
        if (demo.MainMenuVisible
            || demo.IntroVisible
            || !bridge.IsDebugSession
            || world?.ActiveZoneId != "kara_urman_night"
            || bridge.CurrentZoneId != "kara_urman_night"
            // Entering a zone legitimately writes a *heard* word, so the check is
            // that the jump proves nothing: no word is confirmed and no quest is done.
            || bridge.LearnedVocabulary().Any(entry => entry.Status == "confirmed")
            || bridge.SelectRuntimeState().GetProperty("quests").EnumerateObject()
                .Any(quest => quest.Value.GetProperty("status").GetString() == "completed"))
        {
            Fail(
                $"The debug zone jump did not land in the night Kara edge without granting progress "
                + $"(menu={demo.MainMenuVisible} intro={demo.IntroVisible} world={world?.ActiveZoneId} bridge={bridge.CurrentZoneId}).");
            return;
        }

        GD.Print($"act1-main-menu: debug zone jump reached {bridge.CurrentZoneId}@{bridge.CurrentSpawnPointId} without granting confirmed knowledge or a completed quest");

        // Use the actual world-command/autosave and quick-save/load owners after
        // the menu jump. The guard protects the host; this checks the unguarded
        // product behaviour inside that isolated profile, including both backups.
        if (!await bridge.DispatchInteractionAsync("urman.chapter1:interaction/discover-kara-warm-window-clearing"))
        { Fail("Debug save regression could not execute its available world discovery."); return; }
        var debugCheckpoint = ProjectSettings.GlobalizePath("user://debug-savegames/checkpoint.savegame-v3.json");
        if (!System.IO.File.Exists(debugCheckpoint))
        { Fail("A debug world discovery did not autosave in the debug directory."); return; }
        bridge._UnhandledInput(new InputEventAction { Action = "quick_save", Pressed = true });
        var debugQuick = ProjectSettings.GlobalizePath("user://debug-savegames/quick.savegame-v3.json");
        for (var frame = 0; frame < 900 && !System.IO.File.Exists(debugQuick); frame++) await Frames(1);
        if (!System.IO.File.Exists(debugQuick) || !await bridge.LoadSlotAsync("quick")
            || !bridge.IsDebugSession || bridge.CurrentZoneId != "kara_urman_night"
            || PlayerSaveHashes() != regularSaveHashes)
        { Fail("Debug autosave/F5/F9 touched player saves or escaped its isolated session."); return; }

        System.IO.File.Delete(debugFlag);
        if (MainMenuUi.DebugZonesEnabled)
        {
            Fail("The debug zone entry stayed enabled after its flag file was removed.");
            return;
        }

        // Back to the menu for the exit probe; the menu is rebuilt without the flag.
        if (!await TryShowMainMenu(demo, pauseMenu))
        {
            Fail("Could not return to the main menu after the debug zone jump.");
            return;
        }
        if (demo.MainMenu?.FindChild("DebugZonesButton", recursive: true, owned: false) is not null)
        {
            Fail("The rebuilt main menu still offers the debug zone entry without its flag file.");
            return;
        }

        var playerContinue = await bridge.FindContinueAsync();
        if (playerContinue?.Slot != MainMenuUi.CheckpointSlot || demo.MainMenu?.ContinueButton?.Visible != true)
        { Fail("Debug saves replaced the player's main-menu Continue candidate."); return; }
        demo.MainMenu.ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
        for (var frame = 0; frame < 900 && demo.MainMenuVisible; frame++) await Frames(1);
        if (demo.MainMenuVisible || bridge.IsDebugSession || bridge.CurrentZoneId != expectedZone
            || bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() != regularKnowledge
            || PlayerSaveHashes() != regularSaveHashes)
        { Fail("Continue after debug did not restore the untouched player's session."); return; }
        GD.Print("act1-main-menu: PASS debug autosave/F5/F9 isolated; normal Continue restored original knowledge and byte-identical saves");
        if (!await TryShowMainMenu(demo, pauseMenu))
        { Fail("Could not return to the main menu after the isolated debug save regression."); return; }

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

    private static string PlayerSaveHashes()
    {
        var directory = ProjectSettings.GlobalizePath("user://savegames");
        return string.Join("\n", System.IO.Directory.EnumerateFiles(directory).OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => System.IO.Path.GetFileName(path) + ":" + Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(path)))));
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

    /// <summary>
    /// Walks back to the main menu the way a player does: Escape opens pause, then
    /// "В главное меню" with its confirmation press. Used after a debug zone jump.
    /// </summary>
    private async Task<bool> TryShowMainMenu(Act1DemoRoot demo, PauseMenuUi pauseMenu)
    {
        if (pauseMenu.MainMenuButton is null)
        {
            return false;
        }

        for (var attempt = 0; attempt < 3 && !pauseMenu.IsOpen; attempt++)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true, Echo = false });
            await Frames(2);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false, Echo = false });
            await Frames(2);
            for (var frame = 0; frame < 60 && !pauseMenu.IsOpen; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        if (!pauseMenu.IsOpen)
        {
            return false;
        }

        pauseMenu.MainMenuButton.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        pauseMenu.MainMenuButton.EmitSignal(BaseButton.SignalName.Pressed);
        for (var frame = 0; frame < 300; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (demo.MainMenuVisible && !pauseMenu.IsOpen)
            {
                return true;
            }
        }

        return false;
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
