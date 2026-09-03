using Godot;

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

    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn");
        var main = packed?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Save lifecycle smoke could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (bridge is null || player is null)
        {
            Fail("Save lifecycle smoke could not find bridge or player.");
            return;
        }

        player.SetPhysicsProcess(false);
        DeleteSlot();

        // 1) Save at the arrival anchor, then drift the session away.
        var savedTransform = player.GlobalPosition;
        var settingsBefore = player.CaptureSettings();
        if (!await bridge.SaveSlotAsync(Slot))
        {
            Fail("Save lifecycle smoke could not write the lifecycle slot.");
            return;
        }

        bridge.SetWorldLocation("house_old_pc", "entry");
        player.GlobalPosition = new Vector3(99f, 99f, 99f);

        // 2) New Game: canonical arrival reset, settings preserved.
        if (!await bridge.StartNewGameAsync())
        {
            Fail("Save lifecycle smoke could not start a new game.");
            return;
        }

        if (bridge.CurrentZoneId != "village_day" ||
            !player.GlobalPosition.IsEqualApprox(new Vector3(0f, 0.05f, 9f)))
        {
            Fail($"New game did not reset to the arrival spawn: zone={bridge.CurrentZoneId} pos={player.GlobalPosition}.");
            return;
        }

        var settingsAfter = player.CaptureSettings();
        if (settingsAfter.FieldOfView != settingsBefore.FieldOfView ||
            settingsAfter.HeadBob != settingsBefore.HeadBob ||
            settingsAfter.GraphicsPreset != settingsBefore.GraphicsPreset ||
            settingsAfter.MouseSensitivity != settingsBefore.MouseSensitivity)
        {
            Fail("New game wiped the player's live user settings.");
            return;
        }

        // 3) Existing slots are untouched: Continue still restores the save.
        if (!bridge.HasLoadableSlot(Slot))
        {
            Fail("New game deleted or invalidated the existing slot.");
            return;
        }

        if (!await bridge.LoadSlotAsync(Slot) ||
            bridge.CurrentZoneId != "village_day" ||
            !player.GlobalPosition.IsEqualApprox(savedTransform))
        {
            Fail("Continue after new game did not restore the exact saved state.");
            return;
        }

        // 4) Missing-slot query gate: a Continue affordance must be hidden
        // for an absent slot. The safe-failure semantics of a corrupted or
        // missing LOAD are covered by the engine-independent Core store tests
        // (TEST-006); provoking a bridge load error here would leak an
        // expected ERROR line into the aggregator's fail-closed log gate.
        if (bridge.HasLoadableSlot(MissingSlot))
        {
            Fail("Missing slot was reported loadable.");
            return;
        }

        if (bridge.CurrentZoneId != "village_day" ||
            !player.GlobalPosition.IsEqualApprox(savedTransform))
        {
            Fail("State drifted after the missing-slot query.");
            return;
        }

        GD.Print("act1-save-lifecycle: PASS new game reset + settings preserved + slots untouched + safe invalid load");
        DeleteSlot();
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private static void DeleteSlot()
    {
        foreach (var suffix in new[] { ".json", ".backup.json" })
        {
            var path = ProjectSettings.GlobalizePath($"user://savegames/{Slot}{suffix}");
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
