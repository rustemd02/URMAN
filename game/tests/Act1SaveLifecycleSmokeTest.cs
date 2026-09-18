using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// SAVE-003 focused lifecycle smoke: New Game resets narrative state to the
/// canonical arrival without deleting existing slots and without wiping the
/// player's live settings; a valid slot keeps loading the exact saved state
/// after a New Game; an invalid slot fails safely and leaves state untouched.
/// </summary>
public partial class Act1SaveLifecycleSmokeTest : Node
{
    private const string Slot = "godot-save-lifecycle";
    private const string MissingSlot = "godot-save-lifecycle-missing";
    private bool _timeUiProof;

    public override async void _Ready()
    {
        try { await RunAsync(); }
        catch (Exception exception) { Fail(exception.ToString()); }
        finally { DeleteSlot(); }
    }

    private async Task RunAsync()
    {
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>()
            ?? throw new InvalidOperationException("Save lifecycle smoke could not load the demo.");
        AddChild(demo);
        await Frames(12);
        var main = demo.DemoMain;
        var bridge = main.GetNode<RuntimeBridge>("RuntimeBridge");
        var player = main.GetNode<FirstPersonController>("Player");
        var settings = main.GetNode<SettingsUi>("SettingsUi");
        var journal = main.GetNode<JournalUi>("JournalUi");
        var pause = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi
            ?? throw new InvalidOperationException("Save lifecycle smoke could not find pause UI.");
        _timeUiProof = DisplayServer.GetName() != "headless";
        if (_timeUiProof) await RequireForeground();
        else GD.Print("act1-save-lifecycle timing-ui: external/not-run reason=headless; save lifecycle assertions continue");
        Require(demo.MainMenuVisible, "The ordinary menu must precede this timing check.");
        await RequireNoAccrual(bridge, "initial main menu");
        Require(await this.StartThroughMainMenuAsync(demo), "New Game did not expose the ordinary intro.");
        await PressAction("interact");
        Require(!demo.IntroVisible && !player.ModalOpen, "Intro input did not release the player.");
        player.SetPhysicsProcess(false); // Exact save-transform fixture; no traversal or duration claim.
        DeleteSlot();

        var stateBeforeReaders = bridge.SelectRuntimeState().GetRawText();
        await PressAction("journal");
        Require(journal.GetNode<Control>("Screen").Visible && player.ModalOpen,
            "Mapped journal action did not open the real reader.");
        var beforeReading = bridge.PlayTimeSeconds;
        await ObserveForeground(.40);
        Require(!_timeUiProof || bridge.PlayTimeSeconds >= beforeReading + .25,
            "Foreground reading was excluded merely because the player is modal.");
        await PressAction("ui_cancel");
        Require(!journal.GetNode<Control>("Screen").Visible && !player.ModalOpen,
            "Escape did not close the reader before pause.");
        await PressAction("pause");
        Require(pause.IsOpen, "Mapped pause action did not open the actual shell.");
        await RequireNoAccrual(bridge, "pause");
        pause.SettingsButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
        Require(settings.IsOpen && pause.IsOpen, "Pause settings did not open the actual panel.");
        await RequireNoAccrual(bridge, "settings over pause");
        await PressAction("ui_cancel");
        Require(!settings.IsOpen && pause.IsOpen, "Settings did not return to the pause shell.");
        Require(bridge.SelectRuntimeState().GetRawText() == stateBeforeReaders,
            "Reader/pause/timing observation changed narrative state.");

        var savedTransform = player.GlobalPosition;
        var settingsBefore = player.CaptureSettings();
        Require(await bridge.SaveSlotAsync(Slot), "Could not save the lifecycle slot from pause.");
        var original = System.IO.File.ReadAllBytes(SlotPath());
        var saved = new SaveGameV3Codec().Decode(original);
        Require((!_timeUiProof || saved.PlayTimeSeconds > .25) && Math.Abs(saved.PlayTimeSeconds - bridge.PlayTimeSeconds) < .000001,
            "Save did not retain the actually accrued reader time.");
        pause.ResumeButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
        await ObserveForeground(.30);
        Require(!_timeUiProof || bridge.PlayTimeSeconds > saved.PlayTimeSeconds + .15,
            "Active time did not resume before testing a backward save-counter restore.");
        await PressAction("pause");
        Require(pause.IsOpen, "Could not pause for the load timing boundary.");
        var wallBeforeLoad = demo.M10ElapsedSeconds;
        var loadingBoundaries = new List<(string Kind, bool Loading)>();
        void OnBoundary(string kind) => loadingBoundaries.Add((kind,
            (bridge.CapturePlayTimeBlocks() & RuntimeBridge.PlayTimeBlock.Loading) != 0));
        bridge.PlayTimeBoundary += OnBoundary;
        try { Require(await bridge.LoadSlotAsync(Slot), "Lifecycle load failed."); }
        finally { bridge.PlayTimeBoundary -= OnBoundary; }
        Require(loadingBoundaries.SequenceEqual(new[] { ("load-start", true), ("load-restored", false) }),
            "The real load did not bracket the exact loading interval.");
        Require(Math.Abs(bridge.PlayTimeSeconds - saved.PlayTimeSeconds) < .000001,
            "Loading changed the accumulated value stored by the prior save.");
        await RequireNoAccrual(bridge, "pause after load");
        Require(!demo.M10TimingEnabled || demo.M10ElapsedSeconds > wallBeforeLoad,
            "The opt-in process timeline rewound when the save counter was restored.");
        pause.ResumeButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);

        // Existing SAVE-003 reset/restore contract. This is an explicit transform
        // fixture, and the expected spawn comes from the current connected world.
        bridge.SetWorldLocation("house_old_pc", "entry");
        player.GlobalPosition = new Vector3(99f, 99f, 99f);
        Require(await bridge.StartNewGameAsync(), "Could not start a fresh normal game.");
        Require(main.ConnectedWorld is { } world
            && world.TryGetWorldSpawn("village_day", "arrival", out var arrival)
            && bridge.CurrentZoneId == "village_day" && player.GlobalPosition.IsEqualApprox(arrival.Position),
            $"New Game did not restore the authored arrival: {bridge.CurrentZoneId} {player.GlobalPosition}.");
        var settingsAfter = player.CaptureSettings();
        Require(settingsAfter.FieldOfView == settingsBefore.FieldOfView && settingsAfter.HeadBob == settingsBefore.HeadBob
            && settingsAfter.GraphicsPreset == settingsBefore.GraphicsPreset
            && settingsAfter.MouseSensitivity == settingsBefore.MouseSensitivity,
            "New Game wiped live user settings.");
        Require(bridge.HasLoadableSlot(Slot), "New Game deleted or invalidated the existing slot.");
        await PressAction("pause");
        Require(pause.IsOpen && await bridge.LoadSlotAsync(Slot)
            && bridge.CurrentZoneId == "village_day" && player.GlobalPosition.IsEqualApprox(savedTransform)
            && Math.Abs(bridge.PlayTimeSeconds - saved.PlayTimeSeconds) < .000001,
            "Continue after New Game did not restore the saved pose and accumulated time.");
        pause.ResumeButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
        Require(!bridge.HasLoadableSlot(MissingSlot), "Missing slot was reported loadable.");
        Require(bridge.CurrentZoneId == "village_day" && player.GlobalPosition.IsEqualApprox(savedTransform),
            "The missing-slot query changed the world.");

        Require(await bridge.StartDebugSessionAsync() && bridge.IsDebugSession && !bridge.HasLoadableSlot(Slot),
            "Debug session exposed the player's slot as its own load target.");
        bridge.SetWorldLocation("kara_urman_night", "village_path");
        Require(await bridge.SaveSlotAsync(Slot) && await bridge.StartNewGameAsync() && !bridge.IsDebugSession
            && bridge.CurrentZoneId == "village_day" && bridge.HasLoadableSlot(Slot)
            && original.SequenceEqual(System.IO.File.ReadAllBytes(SlotPath())),
            "New Game after debug changed the player's save or inherited debug storage/state.");
        await PressAction("pause");
        Require(pause.IsOpen, "Could not open pause before returning to the real main menu.");
        pause.MainMenuButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        pause.MainMenuButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(4);
        Require(demo.MainMenuVisible && !pause.IsOpen, "Both confirmations did not return to the main menu.");
        var stateBeforeMenu = bridge.SelectRuntimeState().GetRawText();
        await RequireNoAccrual(bridge, "returned main menu");
        Require(stateBeforeMenu == bridge.SelectRuntimeState().GetRawText(), "Menu timing changed progression.");
        GD.Print($"act1-save-lifecycle: PASS exact prior save time + new game/settings/slot/debug contracts; timed_ui={(_timeUiProof ? "PASS foreground reader + pause/settings/menu" : "external/not-run headless")}; m10_timing={demo.M10TimingEnabled}; technical UI fixture, human duration not measured");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private async Task RequireForeground()
    {
        Require(DisplayServer.GetName() != "headless", "FOCUS_INVALID: play-time UI proof requires a real window.");
        DisplayServer.WindowMoveToForeground();
        var deadline = Time.GetTicksUsec() + 8_000_000;
        while (!DisplayServer.WindowIsFocused() && Time.GetTicksUsec() < deadline) await Frames(1);
        Require(DisplayServer.WindowIsFocused(), "FOCUS_INVALID: the OS did not focus the test window.");
    }

    private async Task ObserveForeground(double seconds)
    {
        if (!_timeUiProof) { await Frames(2); return; }
        var deadline = Time.GetTicksUsec() + (ulong)(seconds * 1_000_000);
        while (Time.GetTicksUsec() < deadline)
        {
            Require(DisplayServer.WindowIsFocused(), "FOCUS_INVALID: focus was lost during the timed UI interval.");
            await Frames(1);
        }
        Require(DisplayServer.WindowIsFocused(), "FOCUS_INVALID: focus was lost at the timed interval's end.");
    }

    private async Task RequireNoAccrual(RuntimeBridge bridge, string phase)
    {
        if (!_timeUiProof) return;
        var before = bridge.PlayTimeSeconds;
        await ObserveForeground(.30);
        Require(Math.Abs(bridge.PlayTimeSeconds - before) < .000001,
            $"Play time accrued during {phase}: {before:R} -> {bridge.PlayTimeSeconds:R}.");
    }

    private async Task PressAction(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        await Frames(2);
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static string SlotPath() => ProjectSettings.GlobalizePath($"user://savegames/{Slot}.savegame-v3.json");

    private static void DeleteSlot()
    {
        foreach (var folder in new[] { "savegames", "debug-savegames" })
        foreach (var suffix in new[] { ".savegame-v3.json", ".savegame-v3.backup.json" })
        {
            var path = ProjectSettings.GlobalizePath($"user://{folder}/{Slot}{suffix}");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
